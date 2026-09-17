[CmdletBinding()]
param(
	[ValidatePattern('^[a-z0-9][a-z0-9_-]*$')]
	[string]$Run = ("fast-" + [DateTimeOffset]::Now.ToString('yyyyMMdd-HHmmss')),

	[int]$Seed = 1,

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
	throw "Fast-run directory already exists: $runPath"
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

$manifestPath = Join-Path $repoRoot 'parity-artifacts/e2e/scenarios.json'
$scenarioIds = @(
	Get-Content -Raw -LiteralPath $manifestPath |
		ConvertFrom-Json |
		Where-Object { $_.tier -eq 'Fast' -and $_.modes -contains 'Sim' } |
		ForEach-Object { $_.id }
)
if ($scenarioIds.Count -eq 0) {
	throw "The scenario manifest contains no SIM Fast scenarios: $manifestPath"
}

$previousIntegration = $env:AION_SIM_DB_INTEGRATION
$previousRunId = $env:AION_SIM_RUN_ID
$previousShard = $env:AION_SIM_SHARD
$previousSeed = $env:AION_SIM_SEED
$previousRunDirectory = $env:AION_E2E_RUN_DIR

function Write-RunMetadata {
	param([string]$GitSha)

	$metadata = [ordered]@{
		run = $Run
		mode = 'SIM'
		tier = 'Fast'
		status = $script:status
		failure = $script:failureMessage
		gitSha = $GitSha
		seed = $Seed
		virtualEpoch = '2026-09-16T08:59:00.0000000+00:00'
		timeZone = 'UTC'
		configProfile = 'sim-fast'
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
			throw "Docker is unavailable; SIM Fast requires Docker MySQL. See $dockerPath"
		}

		$env:AION_SIM_DB_INTEGRATION = '1'
		$env:AION_SIM_RUN_ID = $Run
		$env:AION_SIM_SHARD = '0'
		$env:AION_SIM_SEED = $Seed.ToString([Globalization.CultureInfo]::InvariantCulture)
		$env:AION_E2E_RUN_DIR = $runPath
		Write-RunMetadata -GitSha $gitSha

		$testArguments = @(
			'test', 'tests/Aion.Simulation.Tests/Aion.Simulation.Tests.csproj',
			'--no-restore', '--nologo',
			'--filter', 'FullyQualifiedName~SimulationFastScenarioTests',
			'--results-directory', $runPath,
			'--logger', 'console;verbosity=normal',
			'--logger', 'trx;LogFileName=fast.trx'
		)
		& dotnet @testArguments 2>&1 | Tee-Object -LiteralPath $consolePath
		$testExitCode = $LASTEXITCODE
		if ($testExitCode -ne 0) {
			throw "SIM Fast tests failed with exit code $testExitCode."
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

	if ($null -eq $previousIntegration) { Remove-Item Env:AION_SIM_DB_INTEGRATION -ErrorAction SilentlyContinue }
	else { $env:AION_SIM_DB_INTEGRATION = $previousIntegration }
	if ($null -eq $previousRunId) { Remove-Item Env:AION_SIM_RUN_ID -ErrorAction SilentlyContinue }
	else { $env:AION_SIM_RUN_ID = $previousRunId }
	if ($null -eq $previousShard) { Remove-Item Env:AION_SIM_SHARD -ErrorAction SilentlyContinue }
	else { $env:AION_SIM_SHARD = $previousShard }
	if ($null -eq $previousSeed) { Remove-Item Env:AION_SIM_SEED -ErrorAction SilentlyContinue }
	else { $env:AION_SIM_SEED = $previousSeed }
	if ($null -eq $previousRunDirectory) { Remove-Item Env:AION_E2E_RUN_DIR -ErrorAction SilentlyContinue }
	else { $env:AION_E2E_RUN_DIR = $previousRunDirectory }

	if ($transcriptStarted) { Stop-Transcript | Out-Null }
}

Write-Host "SIM Fast run $Run passed. Artifacts: $runPath"
