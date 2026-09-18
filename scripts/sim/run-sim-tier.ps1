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

	[string]$RunRoot
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
		virtualEpoch = '2026-09-16T08:59:00.0000000+00:00'
		timeZone = 'UTC'
		configProfile = "sim-$($Tier.ToLowerInvariant())"
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

		$testArguments = @(
			'test', 'tests/Aion.Simulation.Tests/Aion.Simulation.Tests.csproj',
			'--no-restore', '--nologo',
			'--filter', 'FullyQualifiedName~SimulationFastScenarioTests',
			'--results-directory', $runPath,
			'--logger', 'console;verbosity=normal',
			'--logger', 'trx;LogFileName=simulation.trx'
		)
		& dotnet @testArguments 2>&1 | Tee-Object -LiteralPath $consolePath
		$testExitCode = $LASTEXITCODE
		if ($testExitCode -ne 0) {
			throw "SIM $Tier process '$ProcessKey' failed with exit code $testExitCode."
		}
		$status = 'passed'
	}
	finally {
		Pop-Location
	}
}
catch {
	$failureMessage = $_.Exception.Message
	throw
}
finally {
	$stopwatch.Stop()
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
}

Write-Host "SIM $Tier process $ProcessKey passed. Artifacts: $runPath"
