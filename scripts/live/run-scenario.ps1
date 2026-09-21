[CmdletBinding()]
param(
	[Parameter(Mandatory)][string]$Scenario,
	[ValidatePattern('^[a-z0-9][a-z0-9_-]*$')][string]$Run = ('live-' + [DateTimeOffset]::Now.ToString('yyyyMMdd-HHmmss')),
	[ValidateRange(0, 1000)][int]$Bots = 0,
	[int]$Seed = 1,
	[ValidateRange(0, 3600)][int]$StepTimeoutSeconds = 0,
	[switch]$PacketTap,
	[switch]$SkipImageBuild,
	[ValidateSet('Host', 'Docker')][string]$BotExecution = 'Host',
	[string]$RunRoot,
	[string]$ProblemLedger,
	[string]$FlakeLedger = (Join-Path $PSScriptRoot '../../parity-artifacts/e2e/flaky.json'),
	[switch]$PlanOnly
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$e2eRoot = Join-Path $repoRoot 'scripts/e2e'
. (Join-Path $e2eRoot 'full-suite.ps1')
. (Join-Path $e2eRoot 'full-live-retry.ps1')
. (Join-Path $e2eRoot 'run-artifact-owner.ps1')
. (Join-Path $e2eRoot 'run-report.ps1')
$manifest = Get-Content -Raw (Join-Path $repoRoot 'parity-artifacts/e2e/scenarios.json') | ConvertFrom-Json
$matches = @(Get-FullSuitePlan -Manifest $manifest | Where-Object { $_.kind -eq 'Live' -and $_.scenario -ceq $Scenario })
if ($matches.Count -ne 1) { throw 'Select one exact LIVE breadth scenario id; SOAK acceptance uses run-full.ps1 -Suite Soak.' }
$step = $matches[0]
if ($Bots -gt 0) { $step.bots = $Bots }
if ($StepTimeoutSeconds -gt 0) { $step.stepTimeoutSeconds = $StepTimeoutSeconds }
if ($PlanOnly) {
	[ordered]@{ mode='LIVE_RETRY'; suite='Standalone'; seed=$Seed; botExecution=$BotExecution; steps=@($step);
		fullHistoryWindow=$false; maxAttempts=2 } | ConvertTo-Json -Depth 8
	return
}
if ([string]::IsNullOrWhiteSpace($RunRoot)) { $RunRoot = Join-Path $repoRoot 'run' }
$root = [IO.Path]::GetFullPath($RunRoot)
$runPath = [IO.Path]::GetFullPath((Join-Path $root $Run))
if ($root -eq $repoRoot -or [string]::IsNullOrWhiteSpace((Split-Path $root -Leaf)) -or
	[IO.Path]::GetFullPath((Split-Path $runPath -Parent)) -ne $root) { throw 'Unsafe standalone output directory.' }
if (Test-Path -LiteralPath $runPath) { throw "Standalone run already exists: $runPath" }
New-Item -ItemType Directory -Path $runPath | Out-Null
Register-AionRunArtifactOwner -Directory $runPath
$gitSha = & git -C $repoRoot rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Cannot record standalone build revision.' }
$FlakeLedger = [IO.Path]::GetFullPath($FlakeLedger)
& python (Join-Path $e2eRoot 'flake-history.py') snapshot --root $runPath --ledger $FlakeLedger
if ($LASTEXITCODE -ne 0) { throw 'Cannot freeze standalone flake policy; no children started.' }
[ordered]@{ run=$Run; suite='Standalone'; gitSha=$gitSha; seed=$Seed; botExecution=$BotExecution;
	retryPolicyVersion=1; flakeLedgerSha256=(Get-FileHash (Join-Path $runPath 'flaky-at-start.json')).Hash.ToLowerInvariant();
	steps=@($step) } | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $runPath 'suite-plan.json') -Encoding utf8NoBOM
$failure = $null
$started = [DateTimeOffset]::UtcNow
$timer = [Diagnostics.Stopwatch]::StartNew()
$runLive = Join-Path $PSScriptRoot 'run-live.ps1'
$state = @{ imagesReady=[bool]$SkipImageBuild }
try {
	Invoke-FullSuitePlan -Plan @($step) -Record {
		param($result)
		$result | ConvertTo-Json -Depth 8 -Compress | Add-Content (Join-Path $runPath 'suite-results.jsonl') -Encoding utf8NoBOM
	} -Execute {
		param($selected, $context)
		Invoke-AionFullLiveStep -Step $selected -Context $context -Run $Run -RunRoot $runPath -Admit {
			& python (Join-Path $e2eRoot 'flake-history.py') admit --root $runPath --step $selected.id
			if ($LASTEXITCODE -ne 0) { throw 'Standalone scenario admission failed (possibly quarantined).' }
		} -Execute {
			param($attemptRun, $number)
			& $runLive -Run $attemptRun -Scenario $selected.scenario -Bots $selected.bots -Seed $Seed `
				-StepTimeoutSeconds $selected.stepTimeoutSeconds -WatcherMode enforce -RunRoot $runPath `
				-PacketTap:$PacketTap -SkipImageBuild:$state.imagesReady -BotExecution $BotExecution -ProblemLedger $ProblemLedger
			$state.imagesReady = $true
		}
		& python (Join-Path $e2eRoot 'flake-history.py') verify --root $runPath --step $selected.id
		if ($LASTEXITCODE -ne 0) { throw 'Standalone retry evidence validation failed.' }
	}
} catch { $failure = $_; throw }
finally {
	try {
		Write-AionRunReport -RunDirectory $runPath -Run $Run -Mode LIVE_RETRY -Scenarios @($Scenario) `
			-Status $(if ($null -eq $failure) { 'passed' } else { 'failed' }) -StartedUtc $started -DurationSeconds $timer.Elapsed.TotalSeconds `
			-Failure $(if ($null -eq $failure) { $null } else { $failure.Exception.ToString() }) -GitSha $gitSha -Seed $Seed
	} catch {
		if ($null -eq $failure) { throw }
		Write-Warning "Could not finalize standalone report: $_. Original failure preserved."
	} finally {
		& python (Join-Path $e2eRoot 'flake-history.py') record --root $runPath --ledger $FlakeLedger
		if ($LASTEXITCODE -ne 0) {
			@{ error='Standalone flake history was not persisted; review this run.' } | ConvertTo-Json |
				Set-Content (Join-Path $runPath 'flake-history-error.json') -Encoding utf8NoBOM
			& python (Join-Path $e2eRoot 'report-run.py') $runPath
			if ($null -eq $failure) { throw 'Standalone flake history persistence failed.' }
			Write-Warning 'Standalone flake history persistence failed; original failure preserved.'
		}
	}
}
Write-Host "Standalone LIVE scenario passed. This is not a Full run. Artifacts: $runPath"
