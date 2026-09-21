[CmdletBinding()]
param(
	[Parameter(Mandatory)]
	[ValidatePattern('^[a-z0-9][a-z0-9_-]*$')]
	[string]$Run,

	[Parameter(Mandatory)]
	[ValidateSet('Fast', 'Full')]
	[string]$Tier,

	[Parameter(Mandatory)]
	[ValidatePattern('^(shard-[0-9]{2}|reset-[A-Za-z0-9_-]+)$')]
	[string]$ProcessKey,

	[ValidateRange(1, 100)]
	[int]$ShardCount = 1,

	[int]$Seed = 1,

	[string]$SimulationRunId = $Run,

	[string]$RunRoot,
	[switch]$CodeCoverage
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $repoRoot 'scripts/e2e/run-artifact-owner.ps1')
. (Join-Path $repoRoot 'scripts/e2e/run-report.ps1')
. (Join-Path $PSScriptRoot 'code-coverage.ps1')
$collectCoverage = $CodeCoverage -or $Tier -eq 'Full'
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
	throw "SIM run directory already exists: $runPath"
}

$manifestPath = Join-Path $repoRoot 'parity-artifacts/e2e/scenarios.json'
$tierLimit = if ($Tier -eq 'Fast') { 0 } else { 1 }
$selected = @(
	Get-Content -Raw -LiteralPath $manifestPath |
		ConvertFrom-Json |
		Where-Object {
			$scenarioTier = switch ($_.tier) { 'Fast' { 0 } 'Full' { 1 } 'Soak' { 2 } default { 99 } }
			$_.modes -contains 'Sim' -and $scenarioTier -le $tierLimit
		}
)
$scenarioIds = [Collections.Generic.List[string]]::new()
$sharedScenario = 0
foreach ($scenario in $selected) {
	$scenarioProcess = if ($scenario.resetEpoch) {
		"reset-$($scenario.id)"
	} else {
		$key = 'shard-{0:D2}' -f ($sharedScenario % $ShardCount)
		$sharedScenario++
		$key
	}
	if ($scenarioProcess -eq $ProcessKey) { $scenarioIds.Add([string]$scenario.id) }
}
if ($scenarioIds.Count -eq 0) {
	throw "SIM $Tier process '$ProcessKey' has no scenarios in $manifestPath"
}

New-Item -ItemType Directory -Path $runPath | Out-Null
Register-AionRunArtifactOwner -Directory $runPath
$transcriptPath = Join-Path $runPath 'transcript.log'
$consolePath = Join-Path $runPath 'test-console.log'
$dockerPath = Join-Path $runPath 'docker-info.log'
$metadataPath = Join-Path $runPath 'run.json'
$startedAt = [DateTimeOffset]::UtcNow
$stopwatch = [Diagnostics.Stopwatch]::StartNew()
$status = 'failed'
$failureMessage = $null
$transcriptStarted = $false

$previousIntegration = $env:AION_SIM_DB_INTEGRATION
$previousRunId = $env:AION_SIM_RUN_ID
$previousShard = $env:AION_SIM_SHARD
$previousShardCount = $env:AION_SIM_SHARD_COUNT
$previousProcessKey = $env:AION_SIM_PROCESS_KEY
$previousTier = $env:AION_SIM_TIER
$previousSeed = $env:AION_SIM_SEED
$previousRunDirectory = $env:AION_E2E_RUN_DIR
$previousQuestPlanRoot = $env:AION_E2E_QUEST_PLAN_ROOT

function Write-RunMetadata {
	param([string]$GitSha)

	$metadata = [ordered]@{
		run = $SimulationRunId
		mode = 'SIM'
		tier = $Tier
		processKey = $ProcessKey
		shardCount = $ShardCount
		status = $script:status
		failure = $script:failureMessage
		gitSha = $GitSha
		seed = $Seed
		virtualEpoch = $(switch ($ProcessKey) {
			'reset-L6' { '2020-12-16T08:58:00.0000000+00:00' }
			'reset-L8C' { '2026-12-16T12:00:00.0000000+00:00' }
			'reset-L8' { '2026-08-09T23:50:00.0000000+00:00' }
			default { '2026-09-16T08:59:00.0000000+00:00' }
		})
		timeZone = 'UTC'
		configProfile = "sim-$($Tier.ToLowerInvariant())"
		codeCoverageEnabled = [bool]$collectCoverage
		scenarioConfigOverrides = $(if ($scenarioIds.Contains('L4')) {
			@{ L4 = @{ 'gameserver.security.passkey.enable' = $true; 'gameserver.security.passkey.wrong.maxcount' = 5 } }
		} elseif ($scenarioIds.Contains('L8C')) {
			@{ L8C = @{ 'gameserver.event.advent_calendar.enable' = $true; easter = 0; faction = 0; lock = 0; questrestart = 0; symphony = 0 } }
		} elseif ($scenarioIds.Contains('L8')) {
			@{ L8 = @{ 'gameserver.event.service.disabled_events' = 'Beyond Aion Server Buffs,Increased Gathering & Crafting XP Rates,Increased Drop Rates,Increased Drop Rates 50%' } }
		} elseif ($scenarioIds.Contains('SWEEP-GATHER')) {
			@{ 'SWEEP-GATHER' = @{ 'gameserver.gather.fail.chance' = 0 } }
		} elseif ($scenarioIds.Contains('SWEEP-CRAFT')) {
			@{ 'SWEEP-CRAFT' = @{ 'gameserver.craft.fail.chance' = 0; setup = 'Per-row components, skill, DP, recipe and empty craft cooldowns; verified products removed after each row' } }
		} elseif ($scenarioIds.Contains('SWEEP-TELEPORT')) {
			@{ 'SWEEP-TELEPORT' = @{ setup = 'Ordinary level-65 subjects; funds, route quests, approach positions and temporary shipped siege/base ownership/spawns and Panesterra faction; original ownership, activity and player faction restored per row' } }
		} elseif ($scenarioIds.Contains('SWEEP-SKILL')) {
			@{ 'SWEEP-SKILL' = @{ setup = 'Four ordinary race-specific subjects, including same-race party helpers; director class/daeva eligibility/level, skill learning, temporary mastery only for classless skill rows, equipment, consumables, HP/MP/DP, player/dead targets, compatible spirits/robot mode, chain/counter windows and shipped crowd-control effects; independent-row cooldown/effect/summoned-object cleanup; natural combat/respawn waits; normal casts, pet follow-up casts, passive application and gathering/morphing required; profession failure chance set to zero for action execution and restored afterward' } }
		} elseif ($scenarioIds.Contains('SWEEP-TRADE')) {
			@{ 'SWEEP-TRADE' = @{ setup = 'Ordinary race-specific subjects; level, legion, kinah/AP/rank, required materials and dialog prerequisites; shipped siege/base spawns, matching Panesterra faction and director game-calendar changes; director clears nearby hostile NPCs when a vendor is fighting, then waits for normal AI return; previous-row supplied inventory/products cleaned between independent catalogs' } }
		} else { @{} })
		scenarios = $scenarioIds
		startedAt = $startedAt.ToString('O')
		finishedAt = [DateTimeOffset]::UtcNow.ToString('O')
		wallSeconds = [Math]::Round($stopwatch.Elapsed.TotalSeconds, 3)
	}
	[IO.File]::WriteAllText(
		$metadataPath,
		($metadata | ConvertTo-Json -Depth 4) + "`n",
		[Text.UTF8Encoding]::new($false))
}

function Restore-Environment([string]$Name, [AllowNull()][string]$PreviousValue) {
	if ($null -eq $PreviousValue) { Remove-Item "Env:$Name" -ErrorAction SilentlyContinue }
	else { Set-Item "Env:$Name" $PreviousValue }
}

function Test-RunDataSweeps {
	foreach ($sweep in @(
		@{ Scenario = 'SWEEP-GATHER'; Name = 'gatherables' },
		@{ Scenario = 'SWEEP-CRAFT'; Name = 'recipes' },
		@{ Scenario = 'SWEEP-BIND'; Name = 'bindpoints' },
		@{ Scenario = 'SWEEP-TELEPORT'; Name = 'teleporters' },
		@{ Scenario = 'SWEEP-TRADE'; Name = 'tradelists' },
		@{ Scenario = 'SWEEP-SKILL'; Name = 'skills' }
	)) {
		if ($scenarioIds.Contains($sweep.Scenario)) {
			& python (Join-Path $repoRoot 'scripts/e2e/report-data-sweep.py') `
				--report (Join-Path $runPath "data-sweeps/$($sweep.Name).json") `
				--baseline (Join-Path $repoRoot "parity-artifacts/e2e/data-sweeps/$($sweep.Name)-baseline.json")
			if ($LASTEXITCODE -ne 0) {
				throw "$($sweep.Name) data-sweep coverage failed validation or regressed against its baseline."
			}
		}
	}
}

$gitSha = ''
try {
	Start-Transcript -LiteralPath $transcriptPath | Out-Null
	$transcriptStarted = $true
	Push-Location $repoRoot
	try {
		$gitSha = (& git rev-parse HEAD).Trim()
		if ($LASTEXITCODE -ne 0) { throw 'Could not resolve the run Git SHA.' }

		& docker info --format '{{.ServerVersion}}' *> $dockerPath
		if ($LASTEXITCODE -ne 0) {
			throw "Docker is unavailable; SIM $Tier requires Docker MySQL. See $dockerPath"
		}

		$env:AION_SIM_DB_INTEGRATION = '1'
		$env:AION_SIM_RUN_ID = $SimulationRunId
		$env:AION_SIM_SHARD = $ProcessKey
		$env:AION_SIM_SHARD_COUNT = $ShardCount.ToString([Globalization.CultureInfo]::InvariantCulture)
		$env:AION_SIM_PROCESS_KEY = $ProcessKey
		$env:AION_SIM_TIER = $Tier
		$env:AION_SIM_SEED = $Seed.ToString([Globalization.CultureInfo]::InvariantCulture)
		$env:AION_E2E_RUN_DIR = $runPath
		if ($scenarioIds.Contains('Q4P') -or $scenarioIds.Contains('Q4I')) {
			$questPlanRoot = Join-Path $runPath 'quest-plans'
			if ($scenarioIds.Contains('Q4P')) {
				& python scripts/e2e/compile-quest-plans.py --output (Join-Path $questPlanRoot 'Poeta') --runnable-only --zone Poeta
				if ($LASTEXITCODE -ne 0) { throw 'Poeta quest-plan compilation failed.' }
			}
			if ($scenarioIds.Contains('Q4I')) {
				& python scripts/e2e/compile-quest-plans.py --output (Join-Path $questPlanRoot 'Ishalgen') --runnable-only --zone Ishalgen
				if ($LASTEXITCODE -ne 0) { throw 'Ishalgen quest-plan compilation failed.' }
			}
			$env:AION_E2E_QUEST_PLAN_ROOT = $questPlanRoot
		}
		Write-RunMetadata -GitSha $gitSha

		if ($collectCoverage) {
			# Freeze the exact compiled/source inputs before the collector instruments this process's copy.
			& dotnet build tests/Aion.Simulation.Tests/Aion.Simulation.Tests.csproj --no-restore --nologo
			if ($LASTEXITCODE -ne 0) { throw 'Coverage test-host build failed.' }
			Initialize-AionSimCoverage -RepoRoot $repoRoot -RunDirectory $runPath -Run $SimulationRunId `
				-GitSha $gitSha -Seed $Seed -Tier $Tier -Scenarios $scenarioIds.ToArray()
		}
		$testArguments = @(
			'test', 'tests/Aion.Simulation.Tests/Aion.Simulation.Tests.csproj',
			'--no-restore', '--nologo',
			'--filter', 'FullyQualifiedName~SimulationFastScenarioTests',
			'--results-directory', $runPath,
			'--logger', 'console;verbosity=normal',
			'--logger', 'trx;LogFileName=simulation.trx'
		)
		if ($collectCoverage) {
			$testArguments += @('--no-build', '--collect', 'XPlat Code Coverage', '--settings', (Join-Path $runPath 'coverage.runsettings'))
		}
		& dotnet @testArguments 2>&1 | Tee-Object -LiteralPath $consolePath
		$testExitCode = $LASTEXITCODE
		if ($testExitCode -ne 0) {
			throw "SIM $Tier process '$ProcessKey' failed with exit code $testExitCode."
		}
		$status = 'passed'
		# Sweep validators require successful test-process provenance. A sweep
		# rejection below is still caught and replaces this provisional status.
		Write-RunMetadata -GitSha $gitSha
		Test-RunDataSweeps
	}
	finally {
		Pop-Location
	}
}
catch {
	$status = 'failed'
	$failureMessage = $_.Exception.ToString()
	throw
}
finally {
	$stopwatch.Stop()
	if ($collectCoverage -and (Test-Path -LiteralPath (Join-Path $runPath 'code-coverage-request.json'))) {
		try { Complete-AionSimCoverage -RepoRoot $repoRoot -RunDirectory $runPath }
		catch { Write-Warning "Coverage finalization failed: $_. The report must reject missing/invalid coverage evidence." }
	}
	Write-RunMetadata -GitSha $gitSha
	Restore-Environment 'AION_SIM_DB_INTEGRATION' $previousIntegration
	Restore-Environment 'AION_SIM_RUN_ID' $previousRunId
	Restore-Environment 'AION_SIM_SHARD' $previousShard
	Restore-Environment 'AION_SIM_SHARD_COUNT' $previousShardCount
	Restore-Environment 'AION_SIM_PROCESS_KEY' $previousProcessKey
	Restore-Environment 'AION_SIM_TIER' $previousTier
	Restore-Environment 'AION_SIM_SEED' $previousSeed
	Restore-Environment 'AION_E2E_RUN_DIR' $previousRunDirectory
	Restore-Environment 'AION_E2E_QUEST_PLAN_ROOT' $previousQuestPlanRoot

	if ($transcriptStarted) { Stop-Transcript | Out-Null }
	try {
		Write-AionRunReport -RunDirectory $runPath -Run $SimulationRunId -Mode SIM -Scenarios $scenarioIds.ToArray() `
			-Status $status -StartedUtc $startedAt -DurationSeconds $stopwatch.Elapsed.TotalSeconds `
			-Failure $failureMessage -GitSha $gitSha -Seed $Seed
	} catch {
		if ($status -eq 'passed') { throw }
		Write-Warning "Could not finalize report: $_. Original SIM failure is preserved."
	}
}

Write-Host "SIM $Tier process $ProcessKey passed. Artifacts: $runPath"
