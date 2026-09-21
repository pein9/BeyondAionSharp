# BA-001 topology prerequisite; optional two-bot diagnostic submits a real transfer request.
[CmdletBinding()]
param(
	[Parameter(Mandatory)][ValidatePattern('^[a-z0-9][a-z0-9_-]{0,49}$')][string]$Run,
	[string]$RunRoot = (Join-Path $PSScriptRoot '../../run/p10-09-topology'),
	[switch]$SkipImageBuild,
	[switch]$TransferAttempt
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $PSScriptRoot 'lifecycle-controller.ps1')
. (Join-Path $PSScriptRoot 'transfer-controller.ps1')
. (Join-Path $repoRoot 'scripts/e2e/run-artifact-owner.ps1')
$project="aion-bots-$Run"
$composeFile=Join-Path $repoRoot 'docker/docker-compose.bots.yml'
$composeArgs=@('compose','-f',$composeFile,'-p',$project,'--profile','cross-server')
$root=[IO.Path]::GetFullPath($RunRoot)
$directory=[IO.Path]::GetFullPath((Join-Path $root $Run))
if ((Split-Path $directory -Parent) -cne $root -or (Test-Path -LiteralPath $directory)) { throw 'Topology probe requires a new child artifact directory.' }
$existing=@(& docker ps -aq --filter "label=com.docker.compose.project=$project")
if ($LASTEXITCODE -ne 0 -or $existing.Count -ne 0) { throw 'Topology project already exists or Docker is unavailable.' }
$networks=@(& docker network ls -q --filter "name=^${project}_default$")
if ($LASTEXITCODE -ne 0 -or $networks.Count -ne 0) { throw 'Topology network already exists or Docker is unavailable.' }
$services=[ordered]@{gs='gameserver';gs2='gameserver2';ls='loginserver';cs='chatserver';cs2='chatserver2'}
New-Item -ItemType Directory -Path $directory | Out-Null
foreach ($server in $services.Keys) { New-Item -ItemType Directory -Path (Join-Path $directory "logs/$server") | Out-Null }
Register-AionRunArtifactOwner -Directory $directory
Copy-Item -LiteralPath $PSCommandPath -Destination (Join-Path $directory 'probe-source.ps1')
$settings=@{
	AION_E2E_RUN_DIR=$directory; AION_RUN_ID=$Run; AION_PACKET_TAP='false'; AION_BOT_SECOND_GS='true';
	AION_BOT_OVERLAY_DIR=(Join-Path $repoRoot 'docker/bots/overlay');
	AION_BOT_LOGIN_PORT='42106'; AION_BOT_CHAT_PORT='41241'; AION_BOT_CHAT2_PORT='41242';
	AION_BOT_GAME_PORT='47777'; AION_BOT_ADMIN_PORT='47780'; AION_BOT_GAME2_PORT='47778'; AION_BOT_ADMIN2_PORT='47781'
}
$previous=@{}
foreach ($key in $settings.Keys) { $previous[$key]=[Environment]::GetEnvironmentVariable($key); [Environment]::SetEnvironmentVariable($key,$settings[$key]) }
$created=$false; $validated=$false; $failure=$null; $watcherExit=$null
$cleanupErrors=[Collections.Generic.List[string]]::new()
$identities=@{}
$databases=@{}
$watcher=$null; $watcherOut=$null; $watcherErr=$null
$stopFile=Join-Path $directory 'watcher.stop'
$botCount=if ($TransferAttempt) {2} else {0}

function Invoke-TopologyDocker([string[]]$Arguments) {
	$result=@(& docker @Arguments)
	if ($LASTEXITCODE -ne 0) { throw "Topology Docker command '$($Arguments[0])' failed ($LASTEXITCODE)." }
	return $result
}
function Read-TopologyContainer([string]$Service) {
	$ids=@(Invoke-TopologyDocker (@($composeArgs)+@('ps','--all','--quiet',$Service)))
	if ($ids.Count -ne 1 -or $ids[0] -cnotmatch '^[0-9a-f]{64}$') { throw 'Expected one full owned container id.' }
	$container=@((Invoke-TopologyDocker @('inspect',$ids[0])) | ConvertFrom-Json -DateKind String)[0]
	$nets=@($container.NetworkSettings.Networks.PSObject.Properties.Name)
	if ($container.Id -cne $ids[0] -or $container.Config.Labels.'com.docker.compose.project' -cne $project -or
		$container.Config.Labels.'com.docker.compose.service' -cne $Service -or $nets.Count -ne 1 -or $nets[0] -cne "${project}_default" -or
		-not $container.State.Running -or $container.RestartCount -ne 0) { throw 'Topology target ownership/health mismatch.' }
	return [ordered]@{id=$container.Id;image=$container.Image;startedAt=$container.State.StartedAt;service=$Service}
}
function Stop-TopologyWatcher {
	if ($null -eq $watcher) { return }
	if (-not $watcher.HasExited) {
		[IO.File]::WriteAllText($stopFile,'stop')
		if (-not $watcher.WaitForExit(30000)) { $watcher.Kill($true); throw 'Topology watcher exceeded shutdown deadline.' }
	}
	$script:watcherExit=$watcher.ExitCode
	[IO.File]::WriteAllText((Join-Path $directory 'watcher.log'),$watcherOut.GetAwaiter().GetResult())
	[IO.File]::WriteAllText((Join-Path $directory 'watcher.stderr.log'),$watcherErr.GetAwaiter().GetResult())
}

Push-Location $repoRoot
try {
	$sha=([string](& git rev-parse HEAD)).Trim()
	if ($LASTEXITCODE -ne 0 -or $sha -cnotmatch '^[a-f0-9]{40}$') { throw 'Missing source revision.' }
	& dotnet build tools/Aion.LogWatch --nologo *> (Join-Path $directory 'build.log')
	if ($LASTEXITCODE -ne 0) { throw 'Watcher build failed.' }
	if ($TransferAttempt) {
		& dotnet build tools/Aion.LiveBots --nologo *> (Join-Path $directory 'bots-build.log')
		if ($LASTEXITCODE -ne 0) { throw 'Transfer bot build failed.' }
		Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'transfer-controller.ps1') -Destination (Join-Path $directory 'transfer-controller.ps1')
		Copy-Item -LiteralPath (Join-Path $repoRoot 'tools/Aion.LiveBots/LiveLoginSelection.cs') -Destination (Join-Path $directory 'LiveLoginSelection.cs')
	}
	Write-LifecycleJson (Join-Path $directory 'bots-run.json') ([ordered]@{run=$Run;gitSha=$sha;seed=1;virtualEpoch=$null;timeZone='Etc/UTC';
		configProfile='docker-cross-server';scenarios=@($(if ($TransferAttempt) {'BA-001-transfer-diagnostic'} else {'P10-09-topology'}));bots=$botCount;
		scriptSha256=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash;
		botExecutableSha256=$(if ($TransferAttempt) {(Get-FileHash -LiteralPath 'tools/Aion.LiveBots/bin/Debug/net10.0/Aion.LiveBots.dll' -Algorithm SHA256).Hash} else {$null});
		watcherSha256=(Get-FileHash -LiteralPath 'tools/Aion.LogWatch/bin/Debug/net10.0/Aion.LogWatch.dll' -Algorithm SHA256).Hash})
	# Archive the working patch because this proof may precede its commit.
	& git diff --binary HEAD -- src tests docker tools/Aion.LogWatch tools/Aion.LiveBots scripts/live docs/upstream-porting.md *> (Join-Path $directory 'source.patch')
	foreach ($path in @('docker/bots/seed/20-second-game-server.sh','docker/bots/overlay-gs2/99-instance.properties','docker/bots/overlay-cs2/99-instance.properties')) {
		Copy-Item -LiteralPath $path -Destination (Join-Path $directory (($path -replace '/','_')))
	}
	if (-not $SkipImageBuild) { Invoke-TopologyDocker (@($composeArgs)+@('build','loginserver','chatserver','gameserver')) | Out-Null }
	$created=$true
	Invoke-TopologyDocker (@($composeArgs)+@('up','-d')) | Out-Null
	& (Join-Path $PSScriptRoot 'wait-ready.ps1') -ProjectName $project -RunDirectory $directory -ComposeFile $composeFile -SecondGameServer
	foreach ($server in $services.Keys) { $identities[$server]=Read-TopologyContainer $services[$server] }
	$identities.mysql=Read-TopologyContainer 'mysql'
	if ($identities.gs.image -cne $identities.gs2.image -or $identities.cs.image -cne $identities.cs2.image) { throw 'Paired servers must use identical images.' }
	foreach ($pair in @(@('gs','game','mygs','aion_gs'),@('gs2','game','mygs','aion_gs2'),@('cs','chat','mycs','aion_cs'),@('cs2','chat','mycs','aion_cs2'))) {
		# Inspect only the DB URL, never the generated credentials. The last overlay value wins.
		$lines=@(Invoke-TopologyDocker @('exec',$identities[$pair[0]].id,'grep','^database.url',"/app/$($pair[1])-server/config/$($pair[2]).properties"))
		if ($lines.Count -eq 0 -or $lines[-1] -cnotmatch ("/"+[regex]::Escape($pair[3])+"[?]")) { throw "Wrong database for $($pair[0])." }
		$databases[$pair[0]]=$pair[3]
	}
	$allowlist=@(Get-Content -Raw -LiteralPath 'parity-artifacts/e2e/log-allowlist.json' | ConvertFrom-Json)
	$boot=@($allowlist | Where-Object fp -CEQ '231c488f')
	if ($boot.Count -ne 1 -or $boot[0].maxCount -ne 1) { throw 'Boot allowance prerequisite changed.' }
	$boot[0].servers=@('gs','gs2'); $boot[0].maxCount=2
	$boot[0].reason+=' P10-09 topology has exactly one boot of each of two game servers; raw counts are also checked per instance.'
	$allowlistPath=Join-Path $directory 'topology-log-allowlist.json'
	Write-LifecycleJson $allowlistPath $allowlist
	Write-Host "Both GS/Chat pairs registered. Watching five heartbeat producers; at most $botCount bots."
	$start=[Diagnostics.ProcessStartInfo]::new('dotnet'); $start.UseShellExecute=$false; $start.CreateNoWindow=$true
	$start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
	foreach ($arg in @('tools/Aion.LogWatch/bin/Debug/net10.0/Aion.LogWatch.dll','--run',$Run,'--run-dir',$directory,'--project',$project,
		'--compose-file',$composeFile,'--mode','enforce','--stop-file',$stopFile,'--second-game-server','true',
		'--allowlist',$allowlistPath,'--ledger',(Join-Path $directory 'known-problems.json'))) { $start.ArgumentList.Add($arg) }
	$watcher=[Diagnostics.Process]::Start($start)
	$watcherOut=$watcher.StandardOutput.ReadToEndAsync(); $watcherErr=$watcher.StandardError.ReadToEndAsync()
	if ($TransferAttempt) {
		$sql={param([string]$Query)
			$mysql=Read-TopologyContainer 'mysql'
			if ($mysql.id -cne $identities.mysql.id -or $mysql.startedAt -cne $identities.mysql.startedAt) { throw 'Transfer MySQL identity changed.' }
			$password=if ([string]::IsNullOrWhiteSpace($env:AION_BOT_DB_PASSWORD)) {'aion-bots'} else {$env:AION_BOT_DB_PASSWORD}
			Invoke-TopologyDocker @('exec','-e',"MYSQL_PWD=$password",$mysql.id,'mysql','-uroot','-Nse',$Query)
		}
		$transfer=Invoke-LiveTransferAttempt -Directory $directory -Run $Run -RepoRoot $repoRoot -GitSha $sha -Sql $sql -CheckWatcher {
			if ($watcher.HasExited) { throw "Transfer watcher exited early ($($watcher.ExitCode))." }
		}
		if (-not $transfer.passed) { throw $transfer.failure }
	} else {
		$deadline=[DateTimeOffset]::UtcNow.AddSeconds(35)
		while ([DateTimeOffset]::UtcNow -lt $deadline) {
			if ($watcher.HasExited) { throw "Topology watcher exited early ($($watcher.ExitCode))." }
			Start-Sleep -Milliseconds 200
		}
	}
	Stop-TopologyWatcher
	if ($watcherExit -ne 0) { throw "Topology watcher failed ($watcherExit)." }
	foreach ($server in $identities.Keys) {
		$actual=Read-TopologyContainer $identities[$server].service
		if (($actual | ConvertTo-Json -Compress) -cne ($identities[$server] | ConvertTo-Json -Compress)) { throw "Server identity changed: $server" }
	}
	$validated=$true
} catch { $failure=$_.Exception.ToString() }
finally {
	try { Stop-TopologyWatcher } catch { $cleanupErrors.Add($_.Exception.Message) }
	if ($null -ne $watcher) { $watcher.Dispose() }
	if ($created) {
		try { Invoke-TopologyDocker (@($composeArgs)+@('logs','--no-color','--timestamps')) | Set-Content -LiteralPath (Join-Path $directory 'docker.log') }
		catch { $cleanupErrors.Add($_.Exception.Message) }
		try { Invoke-TopologyDocker (@($composeArgs)+@('down','--volumes','--timeout','15')) | Out-Null }
		catch { $cleanupErrors.Add($_.Exception.Message) }
	}
	foreach ($key in $settings.Keys) { [Environment]::SetEnvironmentVariable($key,$previous[$key]) }
	Pop-Location
}
$rawProblems=foreach ($server in $services.Keys) {
	$producer=$server -replace '2$',''
	$path=Join-Path $directory "logs/$server/$producer.problems.jsonl"
	$rows=@(if (Test-Path -LiteralPath $path) { Get-Content -LiteralPath $path | Where-Object {$_} | ConvertFrom-Json })
	if (($server -like 'gs*' -and ($rows.Count -ne 1 -or $rows[0].fp -cne '231c488f')) -or ($server -notlike 'gs*' -and $rows.Count -ne 0)) { $validated=$false; $failure+=" Unexpected raw $server problems after cleanup." }
	[ordered]@{server=$server;count=$rows.Count;fingerprints=@($rows | ForEach-Object fp)}
}
$passed=$validated -and $null -eq $failure -and $cleanupErrors.Count -eq 0
Write-LifecycleJson (Join-Path $directory 'topology-result.json') ([ordered]@{schemaVersion=1;run=$Run;passed=$passed;watcherExitCode=$watcherExit;
	botCount=$botCount;failure=$failure;cleanupErrors=$cleanupErrors.ToArray();identities=$identities;databases=$databases;rawProblems=@($rawProblems)})
if (-not $passed) { throw "Cross-server topology validation failed. Evidence: $directory" }
Write-Host "Cross-server topology passed; isolated Docker stack removed. This does not prove a player transfer. Evidence: $directory"
