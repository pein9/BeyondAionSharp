[CmdletBinding()]
param(
	[ValidatePattern('^[a-z0-9][a-z0-9_-]*$')]
	[string]$Run = ("full-" + [DateTimeOffset]::Now.ToString('yyyyMMdd-HHmmss')),

	[switch]$PacketTap,

	[switch]$SkipImageBuild,

	[ValidateRange(1, 100)]
	[int]$SimShards = 2,

	[int]$Seed = 1
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$runRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot "run/$Run"))
if (Test-Path -LiteralPath $runRoot) {
	throw "Full-run directory already exists: $runRoot"
}
New-Item -ItemType Directory -Path $runRoot | Out-Null

$runLive = Join-Path $repoRoot 'scripts/live/run-live.ps1'
$runSimTier = Join-Path $repoRoot 'scripts/sim/run-sim-tier.ps1'
$compareL0Packets = Join-Path $repoRoot 'scripts/e2e/compare-l0-packets.ps1'
$manifest = Get-Content -Raw -LiteralPath (Join-Path $repoRoot 'parity-artifacts/e2e/scenarios.json') | ConvertFrom-Json
$simProcesses = [Collections.Generic.List[string]]::new()
$sharedScenario = 0
foreach ($scenario in $manifest) {
	$tier = switch ($scenario.tier) { 'Fast' { 0 } 'Full' { 1 } 'Soak' { 2 } default { 99 } }
	if ($scenario.modes -notcontains 'Sim' -or $tier -gt 1) { continue }
	$processKey = if ($scenario.resetEpoch) {
		"reset-$($scenario.id)"
	} else {
		$key = 'shard-{0:D2}' -f ($sharedScenario % $SimShards)
		$sharedScenario++
		$key
	}
	if (-not $simProcesses.Contains($processKey)) { $simProcesses.Add($processKey) }
}
if ($simProcesses.Count -eq 0) {
	throw 'The scenario manifest contains no SIM Full scenarios.'
}

Push-Location $repoRoot
try {
	foreach ($processKey in $simProcesses) {
		$artifactName = "sim-$($processKey.ToLowerInvariant())"
		& $runSimTier -Run $artifactName -Tier Full -ProcessKey $processKey -ShardCount $SimShards `
			-Seed $Seed -SimulationRunId $Run -RunRoot $runRoot
	}

	& $runLive -Run "$Run-l0" -Scenario 'L0' -WatcherMode 'enforce' -RunRoot $runRoot -FullRun `
		-PacketTap:$PacketTap -SkipImageBuild:$SkipImageBuild
	$simL0Packets = @(Get-ChildItem -LiteralPath $runRoot -Directory -Filter 'sim-*' |
		ForEach-Object { Join-Path $_.FullName 'l0-packets.json' } |
		Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })
	if ($simL0Packets.Count -ne 1) {
		throw "Expected one SIM L0 packet artifact, found $($simL0Packets.Count)."
	}
	& $compareL0Packets -SimPacketPath $simL0Packets[0] `
		-LiveTraceDirectory (Join-Path $runRoot "$Run-l0/bots") `
		-OutputPath (Join-Path $runRoot 'l0-packet-parity.json')

	& $runLive -Run "$Run-m1" -Scenario 'M1' -WatcherMode 'enforce' -RunRoot $runRoot -FullRun `
		-PacketTap:$PacketTap -SkipImageBuild
	& $runLive -Run "$Run-m6" -Scenario 'M6' -WatcherMode 'enforce' -RunRoot $runRoot -FullRun `
		-PacketTap:$PacketTap -SkipImageBuild
	& $runLive -Run "$Run-c1" -Scenario 'C1' -WatcherMode 'enforce' -RunRoot $runRoot -FullRun `
		-PacketTap:$PacketTap -SkipImageBuild -StepTimeoutSeconds 30
	& $runLive -Run "$Run-q1" -Scenario 'Q1' -WatcherMode 'enforce' -RunRoot $runRoot -FullRun `
		-PacketTap:$PacketTap -SkipImageBuild -StepTimeoutSeconds 120
	& $runLive -Run "$Run-q2" -Scenario 'Q2' -WatcherMode 'enforce' -RunRoot $runRoot -FullRun `
		-PacketTap:$PacketTap -SkipImageBuild -StepTimeoutSeconds 120
	& $runLive -Run "$Run-q3" -Scenario 'Q3' -WatcherMode 'enforce' -RunRoot $runRoot -FullRun `
		-PacketTap:$PacketTap -SkipImageBuild -StepTimeoutSeconds 420
	& $runLive -Run "$Run-q4p" -Scenario 'Q4P' -WatcherMode 'enforce' -RunRoot $runRoot -FullRun `
		-PacketTap:$PacketTap -SkipImageBuild -StepTimeoutSeconds 1800
	& $runLive -Run "$Run-q4i" -Scenario 'Q4I' -WatcherMode 'enforce' -RunRoot $runRoot -FullRun `
		-PacketTap:$PacketTap -SkipImageBuild -StepTimeoutSeconds 1800
	& $runLive -Run "$Run-e1" -Scenario 'E1' -WatcherMode 'enforce' -RunRoot $runRoot -FullRun `
		-PacketTap:$PacketTap -SkipImageBuild -StepTimeoutSeconds 360
	& $runLive -Run "$Run-e2" -Scenario 'E2' -WatcherMode 'enforce' -RunRoot $runRoot -FullRun `
		-PacketTap:$PacketTap -SkipImageBuild -StepTimeoutSeconds 60
	& $runLive -Run "$Run-e3" -Scenario 'E3' -WatcherMode 'enforce' -RunRoot $runRoot -FullRun `
		-PacketTap:$PacketTap -SkipImageBuild -StepTimeoutSeconds 60
	& $runLive -Run "$Run-e4" -Scenario 'E4' -WatcherMode 'enforce' -RunRoot $runRoot -FullRun `
		-PacketTap:$PacketTap -SkipImageBuild -StepTimeoutSeconds 60
	& $runLive -Run "$Run-e5" -Scenario 'E5' -WatcherMode 'enforce' -RunRoot $runRoot -FullRun `
		-PacketTap:$PacketTap -SkipImageBuild -StepTimeoutSeconds 90
	& $runLive -Run "$Run-e6" -Scenario 'E6' -WatcherMode 'enforce' -RunRoot $runRoot -FullRun `
		-PacketTap:$PacketTap -SkipImageBuild -StepTimeoutSeconds 60
	& $runLive -Run "$Run-e7" -Scenario 'E7' -WatcherMode 'enforce' -RunRoot $runRoot -FullRun `
		-PacketTap:$PacketTap -SkipImageBuild -StepTimeoutSeconds 60
	& $runLive -Run "$Run-capital" -Scenario 'CAPITAL' -WatcherMode 'enforce' -RunRoot $runRoot -FullRun `
		-PacketTap:$PacketTap -SkipImageBuild -StepTimeoutSeconds 600

	# The first child built the same three server images when a rebuild was requested.
	& $runLive -Run "$Run-canaries" -Scenario 'canaries' -WatcherMode 'enforce' -RunRoot $runRoot -FullRun `
		-PacketTap:$PacketTap -SkipImageBuild

	$coverageReport = Join-Path $runRoot 'quest-coverage.json'
	& python scripts/e2e/report-quest-coverage.py --run-root $runRoot --output $coverageReport
	if ($LASTEXITCODE -ne 0) {
		throw "Quest coverage regressed. See $coverageReport"
	}
}
finally {
	Pop-Location
}

Write-Host "Full SIM + LIVE run $Run passed. Artifacts: $runRoot"
