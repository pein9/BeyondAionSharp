# One continuous, virtual-time SIM journey from creation through the approved Haramel endpoint.
# The fixture owns and drops its fresh schema. No snapshot restore, LIVE stack or administrative progression.
param(
	[ValidatePattern('^[a-z0-9][a-z0-9-]*$')]
	[string]$Run = ('natural-complete-' + (Get-Date -Format 'yyyyMMddHHmmss')),
	[int]$Seed = 1,
	[switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence = Join-Path $repoRoot "run/natural-complete/$Run"
if (Test-Path -LiteralPath $evidence) { throw "Evidence already exists: $evidence. Choose a new run name." }
$variables = @('AION_SIM_DB_INTEGRATION', 'AION_SIM_NI08_DATABASE', 'AION_SIM_NI08_ELAPSED_MS', 'AION_SIM_PROCESS_KEY',
	'AION_SIM_RUN_ID', 'AION_SIM_SEED', 'AION_NI07_COMBAT_DIR', 'AION_BOT_DASHBOARD_PORT', 'NI07_FULL_JOURNEY',
	'NI07_STOP_AFTER_Q2004', 'NI07_STOP_AFTER_Q2005', 'NI07_STOP_AFTER_Q2006', 'NI07_STOP_AFTER_Q2007', 'NI07_STOP_ON_DEATH',
	'NI07_OPTIMIZE_HUBS', 'NI08_RESUME_CHARACTER', 'NI08_STOP_AT', 'NI08_RELOG_AT', 'NA_ASCENSION', 'AF_ALTGARD',
	'AF_ONLY', 'AF_CG_RECEIPTS', 'AF_HM_PROGRESS', 'NA_HELP_ITEMS')
$prior = @{}
foreach ($variable in $variables) { $prior[$variable] = [Environment]::GetEnvironmentVariable($variable) }
try {
	foreach ($variable in $variables) { Remove-Item -LiteralPath "Env:$variable" -ErrorAction SilentlyContinue }
	New-Item -ItemType Directory -Path $evidence | Out-Null
	if (-not $NoBuild) {
		& dotnet build (Join-Path $repoRoot 'tests/Aion.Simulation.Tests') -nologo *> (Join-Path $evidence 'build.log')
		if ($LASTEXITCODE -ne 0) { throw "Build failed; see $evidence/build.log" }
	}
	$env:AION_SIM_DB_INTEGRATION = '1'
	$env:AION_SIM_RUN_ID = $Run
	$env:AION_SIM_SEED = "$Seed"
	$env:AION_NI07_COMBAT_DIR = $evidence
	$env:AION_BOT_DASHBOARD_PORT = '17880'
	$env:NI07_FULL_JOURNEY = '1'
	$env:NA_ASCENSION = '1'
	$env:AF_ALTGARD = 'all'
	$env:NA_HELP_ITEMS = '1'
	Write-Output "Continuous SIM evidence: $evidence"
	Write-Output 'Bot monitor: http://127.0.0.1:17880/'
	& dotnet test (Join-Path $repoRoot 'tests/Aion.Simulation.Tests') --no-build -nologo `
		--filter 'FullyQualifiedName~NaturalIshalgenPriestCompletesFrozenJourneyWithoutSetup' `
		--logger 'console;verbosity=normal' *> (Join-Path $evidence 'journey.log')
	if ($LASTEXITCODE -ne 0) { throw "Continuous SIM failed; original evidence retained at $evidence/journey.log" }
	$report = Get-Content -Raw -LiteralPath (Join-Path $evidence 'continuous-completion.json') | ConvertFrom-Json
	if (-not $report.verified -or -not $report.CreatedCharacter -or $report.stages.Count -ne 14) {
		throw 'The test did not prove the complete created-character journey.'
	}
	Write-Output "Verified character $($report.CharacterId), level $($report.Endpoint.Level), $($report.Endpoint.CompletedQuestIds.Count) completed quests, $($report.Deaths) deaths, $($report.ElapsedMillis) game ms."
}
finally {
	foreach ($variable in $variables) {
		if ($null -eq $prior[$variable]) { Remove-Item -LiteralPath "Env:$variable" -ErrorAction SilentlyContinue }
		else { [Environment]::SetEnvironmentVariable($variable, $prior[$variable]) }
	}
}
