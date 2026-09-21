[CmdletBinding()]
param(
	[ValidatePattern('^[a-z0-9][a-z0-9_-]*$')]
	[string]$Run = ('full-' + [DateTimeOffset]::Now.ToString('yyyyMMdd-HHmmss')),
	[switch]$PacketTap,
	[switch]$SkipImageBuild,
	[ValidateSet('Host', 'Docker')][string]$BotExecution = 'Host',
	[ValidateRange(1, 100)][int]$SimShards = 2,
	[int]$Seed = 1,
	[ValidateSet('Breadth', 'Soak', 'All')][string]$Suite = 'Breadth',
	[ValidateSet(50, 200, 500)][int[]]$SoakBots = @(50, 200, 500),
	[ValidateRange(1, 7200)][int]$SoakSeconds = 7200,
	[switch]$PlanOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'full-suite.ps1')
. (Join-Path $PSScriptRoot 'run-artifact-owner.ps1')
. (Join-Path $PSScriptRoot 'run-report.ps1')
. (Join-Path $PSScriptRoot 'full-live-retry.ps1')
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$manifest = Get-Content -Raw -LiteralPath (Join-Path $repoRoot 'parity-artifacts/e2e/scenarios.json') | ConvertFrom-Json
$plan = @(Get-FullSuitePlan -Manifest $manifest -Suite $Suite -SimShards $SimShards -SoakBots $SoakBots -SoakSeconds $SoakSeconds)
$runSoak = Join-Path $repoRoot 'scripts/live/run-soak.ps1'
if ($PlanOnly) {
	# Planning has no filesystem, Docker, process, or database side effects.
	[pscustomobject]@{ suite = $Suite; seed = $Seed; simShards = $SimShards; botExecution = $BotExecution;
		soakRunnerAvailable = (Test-Path -LiteralPath $runSoak -PathType Leaf); steps = $plan } | ConvertTo-Json -Depth 12
	return
}
if ($Suite -ne 'Breadth' -and -not (Test-Path -LiteralPath $runSoak -PathType Leaf)) {
	throw 'Soak is unavailable until P10-02 supplies scripts/live/run-soak.ps1. No suite was started.'
}
$runRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot "run/$Run"))
if (Test-Path -LiteralPath $runRoot) { throw "Full-run directory already exists: $runRoot" }
New-Item -ItemType Directory -Path $runRoot | Out-Null
Register-AionRunArtifactOwner -Directory $runRoot
$gitSha = & git -C $repoRoot rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Cannot record Full-run git SHA.' }
& python (Join-Path $PSScriptRoot 'flake-history.py') snapshot --root $runRoot
if ($LASTEXITCODE -ne 0) { throw 'Cannot freeze Full flake policy; no children started.' }
[pscustomobject]@{ run = $Run; suite = $Suite; gitSha = $gitSha; seed = $Seed; botExecution = $BotExecution;
	retryPolicyVersion = 1; flakeLedgerSha256 = (Get-FileHash (Join-Path $runRoot 'flaky-at-start.json') -Algorithm SHA256).Hash.ToLowerInvariant();
	simShards = $SimShards; timeZone = [TimeZoneInfo]::Local.Id; steps = $plan } |
	ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $runRoot 'suite-plan.json') -Encoding utf8NoBOM
$suiteState = @{ imagesReady = [bool]$SkipImageBuild }
$runLive = Join-Path $repoRoot 'scripts/live/run-live.ps1'
$runSimTier = Join-Path $repoRoot 'scripts/sim/run-sim-tier.ps1'
$suiteStarted = [DateTimeOffset]::UtcNow
$suiteTimer = [Diagnostics.Stopwatch]::StartNew()
$suiteFailure = $null
Push-Location $repoRoot
try {
	Invoke-FullSuitePlan -Plan $plan -Record {
		param($result)
		$result | ConvertTo-Json -Depth 8 -Compress | Add-Content -LiteralPath (Join-Path $runRoot 'suite-results.jsonl') -Encoding utf8NoBOM
	} -Execute {
		param($step, $context)
		Write-Host "Full $Suite step $($step.id)"
		switch ($step.kind) {
			'Sim' {
				& $runSimTier -Run $step.id -Tier Full -ProcessKey $step.processKey -ShardCount $SimShards `
					-Seed $Seed -SimulationRunId $Run -RunRoot $runRoot
			}
			'Live' {
				Invoke-AionFullLiveStep -Step $step -Context $context -Run $Run -RunRoot $runRoot -Admit {
					& python (Join-Path $PSScriptRoot 'flake-history.py') admit --root $runRoot --step $step.id
					if ($LASTEXITCODE -ne 0) { throw "LIVE scenario admission failed (possibly quarantined): $($step.scenario)" }
				} -Execute {
					param($attemptRun, $number)
					& $runLive -Run $attemptRun -Scenario $step.scenario `
					-Bots $step.bots -WatcherMode enforce -RunRoot $runRoot -FullRun -PacketTap:$PacketTap `
					-SkipImageBuild:$suiteState.imagesReady -StepTimeoutSeconds $step.stepTimeoutSeconds -Seed $Seed -BotExecution $BotExecution
					$suiteState.imagesReady = $true
				}
				& python (Join-Path $PSScriptRoot 'flake-history.py') verify --root $runRoot --step $step.id
				if ($LASTEXITCODE -ne 0) { throw 'LIVE retry evidence validation failed.' }
			}
			'PacketParity' {
				$simPackets = @(Get-ChildItem -LiteralPath $runRoot -Directory -Filter 'sim-*' |
					ForEach-Object { Join-Path $_.FullName 'l0-packets.json' } | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })
				if ($simPackets.Count -ne 1) { throw "Expected one SIM L0 packet artifact, found $($simPackets.Count)." }
				$l0Child = & python (Join-Path $PSScriptRoot 'flake-history.py') select --root $runRoot --step live-l0
				if ($LASTEXITCODE -ne 0) { throw 'No validated L0 attempt for packet parity.' }
				& (Join-Path $PSScriptRoot 'compare-l0-packets.ps1') -SimPacketPath $simPackets[0] `
					-LiveTraceDirectory (Join-Path $runRoot "$l0Child/bots") -OutputPath (Join-Path $runRoot 'l0-packet-parity.json')
			}
			'QuestCoverage' {
				$report = Join-Path $runRoot 'quest-coverage.json'
				& python scripts/e2e/report-quest-coverage.py --run-root $runRoot --output $report
				if ($LASTEXITCODE -ne 0) { throw "Quest coverage regressed. See $report" }
			}
			'Soak' {
				Invoke-AionFullLiveStep -Step $step -Context $context -Run $Run -RunRoot $runRoot -Admit {
					& python (Join-Path $PSScriptRoot 'flake-history.py') admit --root $runRoot --step $step.id
					if ($LASTEXITCODE -ne 0) { throw 'SOAK scenario admission failed (possibly quarantined).' }
				} -Execute {
					param($attemptRun, $number)
					& $runSoak -Run $attemptRun -RunRoot $runRoot -FullRun -Bots $step.bots `
					-DurationSeconds $step.durationSeconds -Seed $Seed -PacketTap:$PacketTap -SkipImageBuild:$suiteState.imagesReady -BotExecution $BotExecution
					$suiteState.imagesReady = $true
				}
				& python (Join-Path $PSScriptRoot 'flake-history.py') verify --root $runRoot --step $step.id
				if ($LASTEXITCODE -ne 0) { throw 'SOAK retry evidence validation failed.' }
			}
			'PacketCoverage' {
				& python scripts/e2e/report-packet-coverage.py --run-root $runRoot
				if ($LASTEXITCODE -ne 0) { throw "Packet coverage validation/regression failure. See $runRoot/packet-coverage-comparison.json" }
			}
			default { throw "Unsupported suite step: $($step.kind)" }
		}
	}
}
catch { $suiteFailure = $_; throw }
finally {
	Pop-Location
	try {
		Write-AionRunReport -RunDirectory $runRoot -Run $Run -Mode FULL -Scenarios @() `
			-Status $(if ($null -eq $suiteFailure) { 'passed' } else { 'failed' }) `
			-StartedUtc $suiteStarted -DurationSeconds $suiteTimer.Elapsed.TotalSeconds `
			-Failure $(if ($null -eq $suiteFailure) { $null } else { $suiteFailure.Exception.ToString() }) -GitSha $gitSha -Seed $Seed
	} catch {
		if ($null -eq $suiteFailure) { throw }
		Write-Warning "Could not finalize report: $_. Original suite failure is preserved."
	} finally {
		& python (Join-Path $PSScriptRoot 'flake-history.py') record --root $runRoot
		if ($LASTEXITCODE -ne 0) {
			[ordered]@{ error = 'Full flake history was not persisted; review before counting this run.' } |
				ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runRoot 'flake-history-error.json') -Encoding utf8NoBOM
			& python (Join-Path $PSScriptRoot 'report-run.py') $runRoot
			if ($null -eq $suiteFailure) { throw 'Full flake history persistence failed.' }
			Write-Warning 'Full flake history persistence failed; original suite failure is preserved.'
		}
	}
}

Write-Host "$Suite suite $Run passed. Artifacts: $runRoot"
if ($Suite -ne 'All' -or $SoakSeconds -ne 7200 -or ((@($SoakBots | Sort-Object) -join ',') -ne '50,200,500')) {
	Write-Host 'This selection is not the complete Phase 10 acceptance matrix.'
}
