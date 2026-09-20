# Zero-bot prerequisite for BA-001: two GS/Chat pairs, one Login, Docker-only databases.
[CmdletBinding()]
param(
	[Parameter(Mandatory)][ValidatePattern('^[a-z0-9][a-z0-9_-]{0,49}$')][string]$Run,
	[string]$RunRoot = (Join-Path $PSScriptRoot '../../run/p10-09-topology'),
	[switch]$SkipImageBuild
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $PSScriptRoot 'lifecycle-controller.ps1')
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

Push-Location $repoRoot
try {
	$sha=([string](& git rev-parse HEAD)).Trim()
	if ($LASTEXITCODE -ne 0 -or $sha -cnotmatch '^[a-f0-9]{40}$') { throw 'Missing source revision.' }
	& dotnet build tools/Aion.LogWatch --nologo *> (Join-Path $directory 'build.log')
	if ($LASTEXITCODE -ne 0) { throw 'Watcher build failed.' }
	Write-LifecycleJson (Join-Path $directory 'bots-run.json') ([ordered]@{run=$Run;gitSha=$sha;seed=1;virtualEpoch=$null;timeZone='Etc/UTC';
		configProfile='docker-cross-server';scenarios=@('P10-09-topology');bots=0;
		scriptSha256=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash;
		watcherSha256=(Get-FileHash -LiteralPath 'tools/Aion.LogWatch/bin/Debug/net10.0/Aion.LogWatch.dll' -Algorithm SHA256).Hash})
	# Archive the working patch because this proof may precede its commit.
	& git diff --binary HEAD -- docker tools/Aion.LogWatch scripts/live/wait-ready.ps1 *> (Join-Path $directory 'source.patch')
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
	Write-Host 'Both GS/Chat pairs registered. Observing five independent heartbeat producers for 35 seconds; zero bots.'
	& dotnet tools/Aion.LogWatch/bin/Debug/net10.0/Aion.LogWatch.dll --run $Run --run-dir $directory --project $project --compose-file $composeFile `
		--mode enforce --duration-seconds 35 --second-game-server true --allowlist $allowlistPath --ledger (Join-Path $directory 'known-problems.json') `
		*> (Join-Path $directory 'watcher.log')
	$watcherExit=$LASTEXITCODE
	if ($watcherExit -ne 0) { throw "Topology watcher failed ($watcherExit)." }
	foreach ($server in $identities.Keys) {
		$actual=Read-TopologyContainer $identities[$server].service
		if (($actual | ConvertTo-Json -Compress) -cne ($identities[$server] | ConvertTo-Json -Compress)) { throw "Server identity changed: $server" }
	}
	$validated=$true
} catch { $failure=$_.Exception.ToString() }
finally {
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
	botCount=0;failure=$failure;cleanupErrors=$cleanupErrors.ToArray();identities=$identities;databases=$databases;rawProblems=@($rawProblems)})
if (-not $passed) { throw "Cross-server topology validation failed. Evidence: $directory" }
Write-Host "Cross-server topology passed; isolated Docker stack removed. This does not prove a player transfer. Evidence: $directory"
