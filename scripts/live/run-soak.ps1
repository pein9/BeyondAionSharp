[CmdletBinding()]
param(
	[ValidatePattern('^[a-z0-9][a-z0-9_-]*$')][string]$Run = ('soak-' + [DateTimeOffset]::Now.ToString('yyyyMMdd-HHmmss')),
	[ValidateSet(50, 200, 500)][int]$Bots = 200,
	[ValidateRange(1, 7200)][int]$DurationSeconds = 7200,
	[int]$Seed = 1,
	[string]$RunRoot,
	[switch]$FullRun,
	[switch]$PacketTap,
	[switch]$SkipImageBuild,
	[ValidateSet('Host', 'Docker')][string]$BotExecution = 'Host'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $repoRoot 'scripts/e2e/run-report.ps1')
if ([string]::IsNullOrWhiteSpace($RunRoot)) {
	$RunRoot = if ([string]::IsNullOrWhiteSpace($env:AION_E2E_RUN_ROOT)) { Join-Path $repoRoot 'run' } else { $env:AION_E2E_RUN_ROOT }
}
$RunRoot = [IO.Path]::GetFullPath($RunRoot)
$runPath = [IO.Path]::GetFullPath((Join-Path $RunRoot $Run))
if ($RunRoot -eq $repoRoot -or [IO.Path]::GetFullPath((Split-Path $runPath -Parent)) -ne $RunRoot) { throw 'Unsafe soak output directory.' }
if (Test-Path -LiteralPath $runPath) { throw "Soak output already exists: $runPath" }
$failure = $null
$success = $false
$started = [DateTimeOffset]::UtcNow
$soakTimer = [Diagnostics.Stopwatch]::StartNew()
$runLive = Join-Path $PSScriptRoot 'run-live.ps1'
Push-Location $repoRoot
try {
	try {
		& $runLive -Run $Run -RunRoot $RunRoot -FullRun:$FullRun -Scenario SOAK -Bots $Bots `
			-SoakSeconds $DurationSeconds -Seed $Seed -StepTimeoutSeconds 1800 -WatcherMode enforce `
			-PacketTap:$PacketTap -SkipImageBuild:$SkipImageBuild -BotExecution $BotExecution
		$success = $true
	}
	catch { $failure = $_ }
	finally {
		if (Test-Path -LiteralPath $runPath -PathType Container) {
			[pscustomobject]@{ schemaVersion = 1; run = $Run; bots = $Bots; seed = $Seed; seconds = $DurationSeconds; botExecution = $BotExecution;
				startedUtc = $started.ToString('O'); completedUtc = [DateTimeOffset]::UtcNow.ToString('O'); success = $success;
				failure = if ($null -eq $failure) { $null } else { $failure.ToString() } } |
				ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $runPath 'soak-execution.json') -Encoding utf8NoBOM
		}
	}
	if (-not (Test-Path -LiteralPath $runPath -PathType Container)) { throw $failure }
	# run-live built this tool in the same artifact directory before starting its bots.
	& dotnet run --project tools/Aion.LiveBots --no-build -- --soak-evidence $runPath
	$workloadCode = $LASTEXITCODE
	& python (Join-Path $repoRoot 'scripts/e2e/soak-acceptance.py') $runPath
	$acceptanceCode = $LASTEXITCODE
	if ($null -ne $failure) { throw $failure }
	if ($workloadCode -ne 0 -or $acceptanceCode -ne 0) { throw "Soak acceptance failed. See $runPath/soak-acceptance.json" }
	Write-Host "Soak $Run accepted for $Bots subjects/$DurationSeconds seconds. This is one population, not the complete Phase 10 matrix."
}
catch { $failure = $_; throw }
finally {
	Pop-Location
	if (Test-Path -LiteralPath $runPath -PathType Container) {
		try {
			# Preserve the actual single-attempt build revision, not an empty wrapper default.
			$liveResult = Get-Content -Raw -LiteralPath (Join-Path $runPath 'runner-result.json') | ConvertFrom-Json
			Write-AionRunReport -RunDirectory $runPath -Run $Run -Mode LIVE -Scenarios @('SOAK') `
				-Status $(if ($null -eq $failure) { 'passed' } else { 'failed' }) `
				-StartedUtc $started -DurationSeconds $soakTimer.Elapsed.TotalSeconds `
				-Failure $(if ($null -eq $failure) { $null } else { $failure.Exception.ToString() }) -Seed $Seed -GitSha $liveResult.gitSha
		} catch {
			if ($null -eq $failure) { throw }
			Write-Warning "Could not finalize report: $_. Original soak failure is preserved."
		}
	}
}
