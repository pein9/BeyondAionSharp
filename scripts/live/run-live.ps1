[CmdletBinding()]
param(
	[ValidatePattern('^[a-z0-9][a-z0-9_-]*$')]
	[string]$Run = ("r" + [DateTimeOffset]::Now.ToString('yyyyMMdd-HHmmss')),

	[string[]]$Scenario = @('connect'),

	[ValidateRange(1, 1000)]
	[int]$Bots = 1,

	[ValidateSet('enforce', 'record')]
	[string]$WatcherMode = 'enforce',

	[ValidateRange(1, 3600)]
	[int]$ReadyTimeoutSeconds = 300,

	[ValidateRange(1, 3600)]
	[int]$ConnectTimeoutSeconds = 10,

	[ValidateRange(1, 3600)]
	[int]$StepTimeoutSeconds = 15,

	[int]$Seed = 1,

	[switch]$PacketTap,

	[switch]$FullRun,

	[switch]$Keep,

	[switch]$SkipImageBuild,

	[string]$RunRoot,

	[string]$ComposeFile = (Join-Path $PSScriptRoot '../../docker/docker-compose.bots.yml')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if ([string]::IsNullOrWhiteSpace($RunRoot)) {
	$RunRoot = if ([string]::IsNullOrWhiteSpace($env:AION_E2E_RUN_ROOT)) {
		Join-Path $repoRoot 'run'
	} else {
		$env:AION_E2E_RUN_ROOT
	}
}
$runRootPath = [IO.Path]::GetFullPath($RunRoot)
if ($runRootPath -eq $repoRoot -or [string]::IsNullOrWhiteSpace((Split-Path $runRootPath -Leaf))) {
	throw "Refusing unsafe run root: $runRootPath"
}
$runPath = [IO.Path]::GetFullPath((Join-Path $runRootPath $Run))
if ([IO.Path]::GetFullPath((Split-Path $runPath -Parent)) -ne $runRootPath) {
	throw "Run directory escaped its root: $runPath"
}
if (Test-Path -LiteralPath $runPath) {
	throw "Run directory already exists: $runPath"
}
$composeFilePath = [IO.Path]::GetFullPath($ComposeFile)
if (-not (Test-Path -LiteralPath $composeFilePath -PathType Leaf)) {
	throw "Compose file does not exist: $composeFilePath"
}
if ($Scenario.Count -eq 0 -or $Scenario.Where({ [string]::IsNullOrWhiteSpace($_) }).Count -ne 0) {
	throw 'At least one non-empty scenario is required.'
}

$projectName = "aion-bots-$Run"
$composeArgs = @('compose', '-f', $composeFilePath, '-p', $projectName)
$watcherProcess = $null
$watcherExitCode = $null
$botExitCode = $null
$stackCreated = $false
$stackStopped = $false
$runStartedAt = [DateTimeOffset]::UtcNow
$stopFile = Join-Path $runPath 'watcher.stop'
$watcherStdout = Join-Path $runPath 'watcher.stdout.log'
$watcherStderr = Join-Path $runPath 'watcher.stderr.log'
$dockerDiagnostics = Join-Path $runPath 'docker.stderr.log'
$previousRunDirectory = $env:AION_E2E_RUN_DIR
$previousRunId = $env:AION_RUN_ID
$previousPacketTap = $env:AION_PACKET_TAP
$previousQuestPlanRoot = $env:AION_E2E_QUEST_PLAN_ROOT
$previousOverlayDirectory = $env:AION_BOT_OVERLAY_DIR
$configProfile = 'docker-bots'

function Invoke-CheckedNative([string]$Command, [string[]]$Arguments, [string]$Description) {
	& $Command @Arguments
	if ($LASTEXITCODE -ne 0) {
		throw "$Description failed with exit code $LASTEXITCODE."
	}
}

function Stop-Watcher {
	if ($null -eq $script:watcherProcess) { return }
	if (-not $script:watcherProcess.HasExited) {
		New-Item -ItemType File -Path $script:stopFile -Force | Out-Null
		if (-not $script:watcherProcess.WaitForExit(30000)) {
			$script:watcherProcess.Kill($true)
			$script:watcherProcess.WaitForExit()
		}
	}
	$script:watcherExitCode = $script:watcherProcess.ExitCode
	$script:watcherProcess.Dispose()
	$script:watcherProcess = $null
}

function Collect-DockerArtifacts {
	$containerDirectory = Join-Path $script:runPath 'logs/containers'
	New-Item -ItemType Directory -Path $containerDirectory -Force | Out-Null
	foreach ($service in @('loginserver', 'chatserver', 'gameserver', 'mysql')) {
		$path = Join-Path $containerDirectory "$service.log"
		& docker @script:composeArgs logs --no-color --no-log-prefix --timestamps $service 2>> $script:dockerDiagnostics |
			Set-Content -LiteralPath $path -Encoding utf8NoBOM
		if ($LASTEXITCODE -ne 0) {
			throw "Could not collect $service container logs (exit $LASTEXITCODE)."
		}
	}

	$until = [DateTimeOffset]::UtcNow.ToString('O')
	$eventsPath = Join-Path $script:runPath 'events.jsonl'
	$eventsErrorPath = Join-Path $script:runPath 'events.stderr.log'
	& docker @script:composeArgs events --json --since $script:runStartedAt.ToString('O') --until $until 2> $eventsErrorPath |
		Set-Content -LiteralPath $eventsPath -Encoding utf8NoBOM
	$eventsExitCode = $LASTEXITCODE
	$eventsError = if (Test-Path -LiteralPath $eventsErrorPath) { (Get-Content -Raw -LiteralPath $eventsErrorPath).Trim() } else { '' }
	if (-not [string]::IsNullOrEmpty($eventsError) -and $eventsError -ne 'EOF') {
		Add-Content -LiteralPath $script:dockerDiagnostics -Value $eventsError -Encoding utf8NoBOM
	}
	Remove-Item -LiteralPath $eventsErrorPath -ErrorAction SilentlyContinue
	# Docker Compose on Windows reports the normal --until stream terminator as stderr "EOF" and exit 1,
	# after writing the complete JSON event stream. Accept exactly that pair, but no other nonzero result.
	if ($eventsExitCode -ne 0 -and $eventsError -ne 'EOF') {
		throw "Could not collect Docker events (exit $eventsExitCode): $eventsError"
	}
}

function Compress-File([string]$Source, [string]$Destination) {
	$input = [IO.File]::OpenRead($Source)
	try {
		$output = [IO.File]::Create($Destination)
		try {
			$gzip = [IO.Compression.GZipStream]::new($output, [IO.Compression.CompressionLevel]::Optimal, $true)
			try { $input.CopyTo($gzip) }
			finally { $gzip.Dispose() }
		}
		finally { $output.Dispose() }
	}
	finally { $input.Dispose() }
}

function Compress-RollingJsonLines([string]$Path) {
	if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return }
	$maximumBytes = 200MB
	$file = Get-Item -LiteralPath $Path
	if ($file.Length -le $maximumBytes) {
		Compress-File $file.FullName ($file.FullName + '.gz')
		Remove-Item -LiteralPath $file.FullName
		return
	}

	$encoding = [Text.UTF8Encoding]::new($false)
	$reader = [IO.StreamReader]::new($file.FullName, $encoding, $true)
	$partPaths = [Collections.Generic.List[string]]::new()
	$writer = $null
	$partBytes = 0L
	$partNumber = 0
	try {
		while (($line = $reader.ReadLine()) -ne $null) {
			$lineBytes = $encoding.GetByteCount($line) + 1
			if ($null -eq $writer -or ($partBytes -gt 0 -and $partBytes + $lineBytes -gt $maximumBytes)) {
				if ($null -ne $writer) { $writer.Dispose() }
				$partNumber++
				$partPath = Join-Path $file.DirectoryName ("{0}.part{1:D3}{2}" -f $file.BaseName, $partNumber, $file.Extension)
				$partPaths.Add($partPath)
				$writer = [IO.StreamWriter]::new($partPath, $false, $encoding)
				$writer.NewLine = "`n"
				$partBytes = 0
			}
			$writer.WriteLine($line)
			$partBytes += $lineBytes
		}
	}
	finally {
		if ($null -ne $writer) { $writer.Dispose() }
		$reader.Dispose()
	}
	foreach ($partPath in $partPaths) {
		Compress-File $partPath ($partPath + '.gz')
		Remove-Item -LiteralPath $partPath
	}
	Remove-Item -LiteralPath $file.FullName
}

function Remove-OldRuns {
	# Full-run children share one matrix root. They are scenarios of the same run,
	# not historical runs: pruning them destroys receipts before coverage aggregation.
	if ($FullRun) { return }
	$otherRuns = Get-ChildItem -LiteralPath $script:runRootPath -Directory |
		Where-Object { $_.FullName -ne $script:runPath } |
		Sort-Object LastWriteTimeUtc -Descending
	foreach ($oldRun in @($otherRuns | Select-Object -Skip 19)) {
		$resolved = [IO.Path]::GetFullPath($oldRun.FullName)
		if ([IO.Path]::GetFullPath((Split-Path $resolved -Parent)) -ne $script:runRootPath -or $resolved -eq $script:runRootPath) {
			throw "Refusing to remove unexpected retention target: $resolved"
		}
		Remove-Item -LiteralPath $resolved -Recurse -Force
	}
}

New-Item -ItemType Directory -Path $runPath,(Join-Path $runPath 'logs/gs'),(Join-Path $runPath 'logs/ls'),(Join-Path $runPath 'logs/cs') | Out-Null
$env:AION_E2E_RUN_DIR = $runPath
$env:AION_RUN_ID = $Run
$env:AION_PACKET_TAP = $PacketTap.IsPresent.ToString().ToLowerInvariant()

$failure = $null
try {
	Push-Location $repoRoot
	try {
		if ($Scenario -contains 'L4') {
			if ($Scenario.Count -ne 1) { throw 'The passkey profile must run L4 in its own isolated stack.' }
			$profileDirectory = Join-Path $runPath 'config-overlay'
			New-Item -ItemType Directory -Path $profileDirectory | Out-Null
			$sourceOverlay = if ([string]::IsNullOrWhiteSpace($previousOverlayDirectory)) {
				Join-Path $repoRoot 'docker/bots/overlay'
			} elseif ([IO.Path]::IsPathRooted($previousOverlayDirectory)) { $previousOverlayDirectory }
			else { Join-Path (Split-Path $composeFilePath -Parent) $previousOverlayDirectory }
			Get-ChildItem -LiteralPath $sourceOverlay -File | Copy-Item -Destination $profileDirectory
			Set-Content -LiteralPath (Join-Path $profileDirectory '99-passkey.properties') -Encoding utf8NoBOM -Value @(
				'gameserver.security.passkey.enable=true', 'gameserver.security.passkey.wrong.maxcount=5'
			)
			$env:AION_BOT_OVERLAY_DIR = $profileDirectory
			$configProfile = 'docker-bots-passkey'
		}
		if ($Scenario -contains 'Q4P' -or $Scenario -contains 'Q4I') {
			$questPlanRoot = Join-Path $runPath 'quest-plans'
			if ($Scenario -contains 'Q4P') {
				Invoke-CheckedNative 'python' @('scripts/e2e/compile-quest-plans.py', '--output', (Join-Path $questPlanRoot 'Poeta'), '--runnable-only', '--zone', 'Poeta') 'Poeta quest-plan compilation'
			}
			if ($Scenario -contains 'Q4I') {
				Invoke-CheckedNative 'python' @('scripts/e2e/compile-quest-plans.py', '--output', (Join-Path $questPlanRoot 'Ishalgen'), '--runnable-only', '--zone', 'Ishalgen') 'Ishalgen quest-plan compilation'
			}
			$env:AION_E2E_QUEST_PLAN_ROOT = $questPlanRoot
		}
		Invoke-CheckedNative 'dotnet' @('build', 'tools/Aion.LiveBots/Aion.LiveBots.csproj', '--nologo') 'Live bot build'
		Invoke-CheckedNative 'dotnet' @('build', 'tools/Aion.LogWatch/Aion.LogWatch.csproj', '--nologo') 'Log watcher build'

		$upArguments = [Collections.Generic.List[string]]::new()
		$upArguments.AddRange([string[]]$composeArgs)
		$upArguments.Add('up')
		$upArguments.Add('-d')
		if (-not $SkipImageBuild) { $upArguments.Add('--build') }
		$stackCreated = $true
		Invoke-CheckedNative 'docker' $upArguments.ToArray() 'Bot stack startup'

		& (Join-Path $repoRoot 'scripts/live/wait-ready.ps1') -ProjectName $projectName -RunDirectory $runPath -TimeoutSeconds $ReadyTimeoutSeconds -ComposeFile $composeFilePath

		$watcherArguments = @(
			'run', '--project', 'tools/Aion.LogWatch', '--no-build', '--',
			'--run', $Run, '--run-dir', $runPath, '--project', $projectName,
			'--compose-file', $composeFilePath, '--mode', $WatcherMode, '--stop-file', $stopFile,
			'--full-run', $FullRun.IsPresent.ToString().ToLowerInvariant()
		)
		$watcherProcess = Start-Process -FilePath 'dotnet' -ArgumentList $watcherArguments -WorkingDirectory $repoRoot `
			-RedirectStandardOutput $watcherStdout -RedirectStandardError $watcherStderr -WindowStyle Hidden -PassThru

		$gitSha = (& git rev-parse HEAD).Trim()
		if ($LASTEXITCODE -ne 0) { throw 'Could not resolve the run Git SHA.' }
		$timeZone = if ([string]::IsNullOrWhiteSpace($env:TZ)) { [TimeZoneInfo]::Local.Id } else { $env:TZ }
		$botArguments = @(
			'run', '--project', 'tools/Aion.LiveBots', '--no-build', '--',
			'--run', $Run, '--output', $runPath, '--host', '127.0.0.1', '--game-port', '17777',
			'--bots', $Bots.ToString(), '--scenario', ($Scenario -join ','),
			'--connect-timeout-seconds', $ConnectTimeoutSeconds.ToString(),
			'--step-timeout-seconds', $StepTimeoutSeconds.ToString(), '--seed', $Seed.ToString(),
			'--git-sha', $gitSha, '--profile', $configProfile, '--time-zone', $timeZone
		)
		& dotnet @botArguments
		$botExitCode = $LASTEXITCODE
		Stop-Watcher
		if ($botExitCode -ne 0) { throw "Live bots failed with exit code $botExitCode." }
		if ($watcherExitCode -ne 0) { throw "Log watcher failed with exit code $watcherExitCode." }
		if (@($Scenario | Where-Object { $_ -match '^G[1-6]$' }).Count -gt 0) {
			# Independent post-logout persistence invariant, inside this run's Docker DB only.
			# The bot never reads this data to decide an action or to populate its world model.
			$gearQuery = 'SELECT JSON_OBJECT(''inventoryRows'', (SELECT COUNT(*) FROM aion_gs.inventory), ''stoneRows'', (SELECT COUNT(*) FROM aion_gs.item_stones), ''orphanRows'', (SELECT COUNT(*) FROM aion_gs.item_stones s LEFT JOIN aion_gs.inventory i ON i.item_unique_id=s.item_unique_id WHERE i.item_unique_id IS NULL))'
			$gearPassword = if ([string]::IsNullOrWhiteSpace($env:AION_BOT_DB_PASSWORD)) { 'aion-bots' } else { $env:AION_BOT_DB_PASSWORD }
			$gearResult = (& docker @composeArgs exec -T -e "MYSQL_PWD=$gearPassword" mysql mysql -uroot -Nse $gearQuery | Out-String).Trim()
			if ($LASTEXITCODE -ne 0) { throw 'Docker gear persistence invariant query failed.' }
			$gearState = $gearResult | ConvertFrom-Json
			$gearResult | Set-Content -LiteralPath (Join-Path $runPath 'gear-persistence-oracle.json') -Encoding utf8
			if ($gearState.inventoryRows -le 0) { throw 'Gear persistence oracle found no saved inventory.' }
			if ($gearState.orphanRows -ne 0) { throw "Gear persistence oracle found $($gearState.orphanRows) orphaned item-stone rows." }
		}
	}
	finally {
		Pop-Location
	}
}
catch {
	$failure = $_
}
finally {
	try { Stop-Watcher }
	catch { if ($null -eq $failure) { $failure = $_ } }

	if ($stackCreated) {
		try {
			if (-not $Keep) {
				Invoke-CheckedNative 'docker' (@($composeArgs) + @('stop')) 'Bot stack stop'
				$stackStopped = $true
			}
			Collect-DockerArtifacts
			Compress-RollingJsonLines (Join-Path $runPath 'events.jsonl')
			if ($stackStopped) {
				Compress-RollingJsonLines (Join-Path $runPath 'logs/gs/packet-tap.jsonl')
			}
		}
		catch { if ($null -eq $failure) { $failure = $_ } }
		finally {
			if (-not $Keep) {
				try { Invoke-CheckedNative 'docker' (@($composeArgs) + @('down', '-v', '--remove-orphans')) 'Bot stack removal' }
				catch { if ($null -eq $failure) { $failure = $_ } }
			}
		}
	}

	if ($null -eq $previousRunDirectory) { Remove-Item Env:AION_E2E_RUN_DIR -ErrorAction SilentlyContinue }
	else { $env:AION_E2E_RUN_DIR = $previousRunDirectory }
	if ($null -eq $previousRunId) { Remove-Item Env:AION_RUN_ID -ErrorAction SilentlyContinue }
	else { $env:AION_RUN_ID = $previousRunId }
	if ($null -eq $previousPacketTap) { Remove-Item Env:AION_PACKET_TAP -ErrorAction SilentlyContinue }
	else { $env:AION_PACKET_TAP = $previousPacketTap }
	if ($null -eq $previousQuestPlanRoot) { Remove-Item Env:AION_E2E_QUEST_PLAN_ROOT -ErrorAction SilentlyContinue }
	else { $env:AION_E2E_QUEST_PLAN_ROOT = $previousQuestPlanRoot }
	if ($null -eq $previousOverlayDirectory) { Remove-Item Env:AION_BOT_OVERLAY_DIR -ErrorAction SilentlyContinue }
	else { $env:AION_BOT_OVERLAY_DIR = $previousOverlayDirectory }

	try { Remove-OldRuns }
	catch { if ($null -eq $failure) { $failure = $_ } }
}

if ($null -ne $failure) {
	throw $failure
}

Write-Host "LIVE run $Run passed. Artifacts: $runPath"
if ($Keep) {
	Write-Host "Compose project $projectName is still running for inspection."
}
