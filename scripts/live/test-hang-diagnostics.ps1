# Deliberate local LIVE faults, not a green gameplay scenario. No player bots or host MySQL.
[CmdletBinding()]
param(
	[Parameter(Mandatory)][ValidatePattern('^[a-z0-9][a-z0-9_-]{0,49}$')][string]$Run,
	[string]$RunRoot = (Join-Path $PSScriptRoot '../../run/p10-04-hangs'),
	[switch]$SkipImageBuild
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $PSScriptRoot 'lifecycle-controller.ps1')
. (Join-Path $repoRoot 'scripts/e2e/run-artifact-owner.ps1')
$project = "aion-bots-$Run"
$composeFile = Join-Path $repoRoot 'docker/docker-compose.bots.yml'
$composeArgs = @('compose', '-f', $composeFile, '-p', $project)
$root = [IO.Path]::GetFullPath($RunRoot)
$directory = [IO.Path]::GetFullPath((Join-Path $root $Run))
if ((Split-Path $directory -Parent) -cne $root -or (Test-Path -LiteralPath $directory)) { throw 'Hang probe requires a new child artifact directory.' }
$existing = @(& docker ps -aq --filter "label=com.docker.compose.project=$project")
if ($LASTEXITCODE -ne 0 -or $existing.Count -ne 0) { throw 'Hang probe project already exists or Docker is unavailable.' }
$networks = @(& docker network ls -q --filter "name=^${project}_default$")
if ($LASTEXITCODE -ne 0 -or $networks.Count -ne 0) { throw 'Hang probe network already exists or Docker is unavailable.' }
New-Item -ItemType Directory -Path $directory,(Join-Path $directory 'logs/gs'),(Join-Path $directory 'logs/ls'),(Join-Path $directory 'logs/cs') | Out-Null
Register-AionRunArtifactOwner -Directory $directory
Copy-Item -LiteralPath $PSCommandPath -Destination (Join-Path $directory 'probe-source.ps1')
$settings = @{
	AION_E2E_RUN_DIR=$directory; AION_RUN_ID=$Run; AION_PACKET_TAP='false';
	AION_BOT_OVERLAY_DIR=(Join-Path $repoRoot 'docker/bots/overlay');
	AION_BOT_LOGIN_PORT='32106'; AION_BOT_CHAT_PORT='31241'; AION_BOT_GAME_PORT='37777'; AION_BOT_ADMIN_PORT='37780'
}
$previous = @{}
foreach ($key in $settings.Keys) { $previous[$key]=[Environment]::GetEnvironmentVariable($key); [Environment]::SetEnvironmentVariable($key,$settings[$key]) }
$watcher=$null; $outputFile=$null; $errorFile=$null; $outputCopy=$null; $errorCopy=$null
$created=$false; $validated=$false; $failure=$null; $watcherExit=$null
$cleanupErrors=[Collections.Generic.List[string]]::new()
$paused=[Collections.Generic.HashSet[string]]::new()
$evidence=[Collections.Generic.List[object]]::new()
$stop=Join-Path $directory 'watcher.stop'
$services=@{gs='gameserver';ls='loginserver';cs='chatserver'}

function Invoke-ProbeDocker([string[]]$Arguments) {
	$result=@(& docker @Arguments)
	if ($LASTEXITCODE -ne 0) { throw "Hang probe Docker command '$($Arguments[0])' failed ($LASTEXITCODE)." }
	return $result
}
function Read-ProbeContainer([string]$Service) {
	$ids=@(Invoke-ProbeDocker (@($composeArgs)+@('ps','--all','--quiet',$Service)))
	if ($ids.Count -ne 1 -or $ids[0] -cnotmatch '^[0-9a-f]{64}$') { throw 'Expected one full owned container id.' }
	$container=@((Invoke-ProbeDocker @('inspect',$ids[0])) | ConvertFrom-Json)[0]
	$nets=@($container.NetworkSettings.Networks.PSObject.Properties.Name)
	if ($container.Id -cne $ids[0] -or $container.Config.Labels.'com.docker.compose.project' -cne $project -or
		$container.Config.Labels.'com.docker.compose.service' -cne $Service -or $nets.Count -ne 1 -or $nets[0] -cne "${project}_default") { throw 'Hang probe target ownership mismatch.' }
	return $container
}
function Assert-ProbeIdentity([object]$Expected,[object]$Actual) {
	if ($Expected.Id -cne $Actual.Id -or $Expected.Image -cne $Actual.Image -or
		$Expected.State.StartedAt -cne $Actual.State.StartedAt -or -not $Actual.State.Running) { throw 'Hang probe server identity/process changed.' }
}
function Read-ProbeHeartbeat([string]$Server) {
	$path=Join-Path $directory "logs/$Server/$Server.events.jsonl"
	if (-not (Test-Path -LiteralPath $path)) { return $null }
	# Shared reader: the server must retain append access during collection.
	$stream=[IO.FileStream]::new($path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
	$reader=[IO.StreamReader]::new($stream); $latest=$null
	try {
		while ($null -ne ($line=$reader.ReadLine())) {
			try { $record=$line | ConvertFrom-Json -DateKind String } catch { continue } # incomplete trailing write
			if ($record.run -ceq $Run -and $record.srv -ceq $Server -and $record.tpl -like 'Server heartbeat:*') { $latest=[DateTimeOffset]$record.ts }
		}
	} finally { $reader.Dispose() }
	return $latest
}
function Assert-WatcherAlive {
	if ($null -ne $watcher -and $watcher.HasExited) { throw "Watcher exited early ($($watcher.ExitCode))." }
}
function Wait-ProbeHeartbeat([string]$Server,[DateTimeOffset]$After) {
	$deadline=[DateTimeOffset]::UtcNow.AddSeconds(25)
	do {
		Assert-WatcherAlive
		$beat=Read-ProbeHeartbeat $Server
		if ($null -ne $beat -and $beat -gt $After) { return $beat }
		Start-Sleep -Milliseconds 200
	} while ([DateTimeOffset]::UtcNow -lt $deadline)
	throw "No fresh heartbeat from $Server."
}
function Stop-ProbeWatcher {
	if ($null -eq $watcher) { return }
	if (-not $watcher.HasExited) {
		[IO.File]::WriteAllText($stop,'stop')
		if (-not $watcher.WaitForExit(30000)) { $watcher.Kill($true); throw 'Watcher exceeded its shutdown deadline.' }
	}
	$script:watcherExit=$watcher.ExitCode
	$outputCopy.GetAwaiter().GetResult() | Out-Null
	$errorCopy.GetAwaiter().GetResult() | Out-Null
}

Push-Location $repoRoot
try {
	$sha=([string](& git rev-parse HEAD)).Trim()
	if ($LASTEXITCODE -ne 0 -or $sha -cnotmatch '^[a-f0-9]{40}$') { throw 'Missing source revision.' }
	& dotnet build tools/Aion.LogWatch --nologo *> (Join-Path $directory 'build.log')
	if ($LASTEXITCODE -ne 0) { throw 'Watcher build failed.' }
	Write-LifecycleJson (Join-Path $directory 'bots-run.json') ([ordered]@{run=$Run;gitSha=$sha;seed=1;configProfile='docker-hang-probe';
		scenarios=@('P10-04-fault-injection');bots=0;scriptSha256=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash;
		watcherSha256=(Get-FileHash -LiteralPath 'tools/Aion.LogWatch/bin/Debug/net10.0/Aion.LogWatch.dll' -Algorithm SHA256).Hash})
	if (-not $SkipImageBuild) { Invoke-ProbeDocker (@($composeArgs)+@('build','loginserver','chatserver','gameserver')) | Out-Null }
	$created=$true
	Invoke-ProbeDocker (@($composeArgs)+@('up','-d')) | Out-Null
	& (Join-Path $PSScriptRoot 'wait-ready.ps1') -ProjectName $project -RunDirectory $directory -ComposeFile $composeFile
	foreach ($server in @('gs','ls','cs')) {
		$container=Read-ProbeContainer $services[$server]
		$before=[DateTimeOffset]::UtcNow
		Wait-ProbeHeartbeat $server $before.AddSeconds(-15) | Out-Null
		# Healthy control: the same pinned tool must produce actual managed frames.
		$stacks=Invoke-ProbeDocker @('exec',$container.Id,'timeout','--signal=KILL','8s','env','DOTNET_ROLL_FORWARD=Major',
			'/opt/aion-diagnostics/dotnet-stack','report','--process-id','1')
		$text=$stacks -join "`n"
		[IO.File]::WriteAllText((Join-Path $directory "healthy-$server-stacks.log"),$text)
		if ($text -notmatch 'Thread ' -or $text -notmatch 'Aion\.') { throw "Healthy $server managed stack control lacks thread/application frames." }
		Assert-ProbeIdentity $container (Read-ProbeContainer $services[$server])
	}
	$start=[Diagnostics.ProcessStartInfo]::new('dotnet'); $start.UseShellExecute=$false; $start.CreateNoWindow=$true
	$start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
	foreach ($arg in @('tools/Aion.LogWatch/bin/Debug/net10.0/Aion.LogWatch.dll','--run',$Run,'--run-dir',$directory,
		'--project',$project,'--compose-file',$composeFile,'--mode','enforce','--stop-file',$stop,'--ledger',(Join-Path $directory 'injected-problems.json'))) { $start.ArgumentList.Add($arg) }
	$outputFile=[IO.FileStream]::new((Join-Path $directory 'watcher.stdout.log'),[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::Read)
	$errorFile=[IO.FileStream]::new((Join-Path $directory 'watcher.stderr.log'),[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::Read)
	$watcher=[Diagnostics.Process]::Start($start)
	$outputCopy=$watcher.StandardOutput.BaseStream.CopyToAsync($outputFile); $errorCopy=$watcher.StandardError.BaseStream.CopyToAsync($errorFile)
	foreach ($server in @('gs','ls','cs')) {
		Assert-WatcherAlive
		$container=Read-ProbeContainer $services[$server]
		$beat=Wait-ProbeHeartbeat $server ([DateTimeOffset]::UtcNow.AddSeconds(-12))
		Assert-ProbeIdentity $container (Read-ProbeContainer $services[$server])
		$injected=[DateTimeOffset]::UtcNow
		# Add before dispatch so an uncertain Docker reply still enters guarded cleanup.
		$paused.Add($container.Id) | Out-Null
		Invoke-ProbeDocker @('pause',$container.Id) | Out-Null
		$confirmed=Read-ProbeContainer $services[$server]
		Assert-ProbeIdentity $container $confirmed
		if (-not $confirmed.State.Paused) { throw 'Pause was not observed.' }
		Write-Host "Hang probe: $server paused; waiting for a real missed heartbeat."
		$artifact=Join-Path $directory "hangs/$server/collection.json"
		$deadline=$injected.AddSeconds(45)
		while (-not (Test-Path -LiteralPath $artifact)) {
			Assert-WatcherAlive
			if (Test-Path -LiteralPath (Join-Path $directory "hangs/$server/failure.json")) { throw "Diagnostic collection failed for $server." }
			if ([DateTimeOffset]::UtcNow -gt $deadline) { throw "No bounded diagnostic collection for $server." }
			Start-Sleep -Milliseconds 200
		}
		$collection=Get-Content -LiteralPath $artifact -Raw | ConvertFrom-Json
		$observation=Get-Content -LiteralPath (Join-Path $directory "hangs/$server/observation.json") -Raw | ConvertFrom-Json
		$lastSample=$observation.lastHeartbeatRecord | ConvertFrom-Json
		if ($lastSample.tpl -cnotlike 'Server heartbeat:*' -or [DateTimeOffset]$lastSample.ts -ne [DateTimeOffset]$observation.lastHeartbeatUtc) { throw 'Diagnostic context is not the actual last heartbeat event.' }
		$identity=$collection.steps.identity.standardOutput | ConvertFrom-Json
		if ($collection.containerId -cne $container.Id -or $collection.status -cne 'partial' -or -not $identity.paused -or
			$collection.steps.'managed-stacks'.failure -notlike '*not an unpaused running process*' -or
			([DateTimeOffset]$observation.detectedUtc-[DateTimeOffset]$observation.lastHeartbeatUtc).TotalSeconds -lt 20) { throw 'Hang evidence does not prove the expected paused-process diagnosis.' }
		foreach ($name in @('select','identity','threads','resources','identity-before-stack')) {
			$step=$collection.steps.$name
			if ($step.exitCode -ne 0 -or $step.timedOut -or $step.truncated -or $null -ne $step.failure) { throw "Unexpected $server diagnostic failure: $name." }
		}
		Assert-ProbeIdentity $container (Read-ProbeContainer $services[$server])
		Invoke-ProbeDocker @('unpause',$container.Id) | Out-Null
		$paused.Remove($container.Id) | Out-Null
		$resumed=[DateTimeOffset]::UtcNow
		$recovered=Wait-ProbeHeartbeat $server $resumed
		Assert-ProbeIdentity $container (Read-ProbeContainer $services[$server])
		$evidence.Add([ordered]@{server=$server;containerId=$container.Id;image=$container.Image;processStartedUtc=$container.State.StartedAt;
			lastBeatBeforePause=$beat;pausedUtc=$injected;detectedUtc=$observation.detectedUtc;resumedUtc=$resumed;freshHeartbeatUtc=$recovered})
		Write-Host "Hang probe: $server diagnosed and resumed with a fresh heartbeat."
	}
	Assert-WatcherAlive
	Stop-ProbeWatcher
	$summary=Get-Content -LiteralPath (Join-Path $directory 'logwatch-summary.json') -Raw | ConvertFrom-Json
	if ($watcherExit -ne 1 -or -not $summary.failed -or $summary.total -ne 4 -or $summary.suppressed -ne 1 -or
		$summary.new -ne 1 -or $summary.known -ne 0 -or $summary.regressed -ne 0 -or $summary.repeated -ne 2 -or
		@($summary.hangDiagnostics).Count -ne 3 -or @($summary.hangDiagnostics | Where-Object status -CNE 'partial').Count -ne 0) { throw 'Watcher did not retain exactly the three deliberate heartbeat failures and known boot allowance.' }
	$validated=$true
} catch { $failure=$_.Exception.ToString(); Write-Warning $failure }
finally {
	foreach ($id in @($paused)) {
		try {
			$owned=@((Invoke-ProbeDocker @('inspect',$id)) | ConvertFrom-Json)[0]
			if ($owned.Id -cne $id -or $owned.Config.Labels.'com.docker.compose.project' -cne $project) { throw 'Refusing unpause of a foreign container.' }
			if ($owned.State.Paused) { Invoke-ProbeDocker @('unpause',$id) | Out-Null }
		} catch { $cleanupErrors.Add($_.Exception.Message) }
	}
	try { Stop-ProbeWatcher } catch { $cleanupErrors.Add($_.Exception.Message) }
	if ($null -ne $watcher) { $watcher.Dispose() }
	if ($null -ne $outputFile) { $outputFile.Dispose() }; if ($null -ne $errorFile) { $errorFile.Dispose() }
	if ($created) {
		try { Invoke-ProbeDocker (@($composeArgs)+@('logs','--no-color','--timestamps')) | Set-Content -LiteralPath (Join-Path $directory 'docker.log') }
		catch { $cleanupErrors.Add($_.Exception.Message) }
		try { Invoke-ProbeDocker (@($composeArgs)+@('down','--volumes','--timeout','15')) | Out-Null }
		catch { $cleanupErrors.Add($_.Exception.Message) }
	}
	foreach ($key in $settings.Keys) { [Environment]::SetEnvironmentVariable($key,$previous[$key]) }
	Pop-Location
}
$rawProblems=foreach ($server in @('gs','ls','cs')) {
	$rows=@(Get-Content -LiteralPath (Join-Path $directory "logs/$server/$server.problems.jsonl") -ErrorAction SilentlyContinue | Where-Object {$_} | ConvertFrom-Json)
	if (($server -eq 'gs' -and ($rows.Count -ne 1 -or $rows[0].fp -cne '231c488f')) -or ($server -ne 'gs' -and $rows.Count -ne 0)) { $validated=$false; $failure+=" Unexpected raw $server problem count/fingerprint after cleanup." }
	[ordered]@{server=$server;count=$rows.Count;fingerprints=@($rows | ForEach-Object fp)}
}
$passed=$validated -and $null -eq $failure -and $cleanupErrors.Count -eq 0
Write-LifecycleJson (Join-Path $directory 'hang-probe-result.json') ([ordered]@{schemaVersion=1;run=$Run;passed=$passed;watcherExitCode=$watcherExit;
	botCount=0;failure=$failure;cleanupErrors=$cleanupErrors.ToArray();evidence=$evidence.ToArray();rawProblems=@($rawProblems)})
if (-not $passed) { throw "Hang fault-injection validation failed. Evidence: $directory" }
Write-Host "Hang fault-injection validation passed; watcher deliberately exited 1. Isolated stack removed. Evidence: $directory"
