param(
	[string]$Run = ('ni08-' + (Get-Date -Format 'yyyyMMdd-HHmmss')),
	[string]$StopAt = '2007:3:6',
	[string]$RelogAt,
	[int]$Seed = 1,
	[ValidateRange(0, 65535)][int]$DashboardPort = 17880
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if ($Run -notmatch '^[a-zA-Z0-9_-]+$') { throw 'Run must be a simple directory name.' }
if ($StopAt -notmatch '^\d+:\d+:\d+$') { throw 'StopAt must be questId:status:packedVars.' }
if ($RelogAt -and $RelogAt -notmatch '^\d+:\d+:\d+$') { throw 'RelogAt must be questId:status:packedVars.' }
$output = Join-Path $repoRoot "run/natural-resume/$Run"
if (Test-Path -LiteralPath $output) { throw "Evidence directory already exists: $output" }
$database = 'aion_gs_sim_ni08_' + [Guid]::NewGuid().ToString('N')
$envNames = @('AION_SIM_DB_INTEGRATION', 'AION_SIM_NI08_DATABASE', 'AION_SIM_NI08_ELAPSED_MS',
	'AION_SIM_RUN_ID', 'AION_SIM_SEED', 'AION_NI07_COMBAT_DIR', 'AION_BOT_DASHBOARD_PORT',
	'NI07_FULL_JOURNEY', 'NI08_STOP_AT', 'NI08_RELOG_AT', 'NI08_RESUME_CHARACTER',
	'NI07_STOP_AFTER_Q2004', 'NI07_STOP_AFTER_Q2005', 'NI07_STOP_AFTER_Q2006', 'NI07_STOP_AFTER_Q2007')
$prior = @{}
foreach ($name in $envNames) { $prior[$name] = [Environment]::GetEnvironmentVariable($name) }
$created = $false
Push-Location $repoRoot
try {
	New-Item -ItemType Directory -Path $output | Out-Null
	& dotnet build tests/Aion.Simulation.Tests -v quiet *> (Join-Path $output 'build.log')
	if ($LASTEXITCODE -ne 0) { throw 'Build failed; see build.log.' }
	$created = $true # The GUID schema is ours even if import fails partway through.
	& pwsh -NoProfile -File scripts/sim/new-sim-db.ps1 -Action Create -DatabaseName $database | Out-Null
	if ($LASTEXITCODE -ne 0) { throw 'Could not create owned NI-08 simulation database.' }
	foreach ($name in $envNames) { [Environment]::SetEnvironmentVariable($name, $null) }
	$env:AION_SIM_DB_INTEGRATION = '1'
	$env:AION_SIM_NI08_DATABASE = $database
	$env:AION_SIM_NI08_ELAPSED_MS = '0'
	$env:AION_SIM_SEED = "$Seed"
	$env:NI07_FULL_JOURNEY = '1'
	$env:AION_BOT_DASHBOARD_PORT = "$DashboardPort"
	$env:NI08_STOP_AT = $StopAt
	$env:NI08_RELOG_AT = $RelogAt
	foreach ($phase in @('before-restart', 'after-restart')) {
		$env:AION_SIM_RUN_ID = "$Run-$phase"
		$env:AION_NI07_COMBAT_DIR = Join-Path $output $phase
		if ($DashboardPort) { Write-Host "Watch $phase at http://127.0.0.1:$DashboardPort/ after the SIM server boots." }
		& dotnet test tests/Aion.Simulation.Tests --no-build --filter 'FullyQualifiedName~NaturalIshalgenPriestCompletesFrozenJourneyWithoutSetup' --logger 'console;verbosity=normal' *> (Join-Path $output "$phase.log")
		if ($LASTEXITCODE -ne 0) { throw "$phase failed; see $output/$phase.log" }
		if ($phase -eq 'before-restart') {
			$receipt = Get-Content -Raw -LiteralPath (Join-Path $env:AION_NI07_COMBAT_DIR 'resume-receipt.json') | ConvertFrom-Json
			if ($receipt.CharacterId -le 0) { throw 'Checkpoint did not retain a character identity.' }
			$env:NI08_STOP_AT = $null
			$env:NI08_RELOG_AT = $null
			$env:NI08_RESUME_CHARACTER = "$($receipt.CharacterId)"
			# Keep game time monotonic across the two actual server/test-host processes and honor reentry.
			$env:AION_SIM_NI08_ELAPSED_MS = "$([long]$receipt.ElapsedMillis + 20000)"
		}
	}
	& pwsh -NoProfile -File scripts/sim/assert-natural-resume.ps1 -Directory $output
	if ($LASTEXITCODE -ne 0) { throw 'Restarted login did not match persisted state.' }
	$completed = Get-Content -Raw -LiteralPath (Join-Path $env:AION_NI07_COMBAT_DIR 'completion.json') | ConvertFrom-Json
	if ($completed.CharacterId -ne $receipt.CharacterId -or $completed.Next.Outcome -ne 'complete') {
		throw 'Restarted journey did not finish as the original character.'
	}
	Write-Host "NI-08 passed: same character $($receipt.CharacterId) completed after a fresh server and bot process. Evidence: $output"
}
finally {
	try {
		if ($created) {
			& pwsh -NoProfile -File scripts/sim/new-sim-db.ps1 -Action Drop -DatabaseName $database
			if ($LASTEXITCODE -ne 0) { throw "NI-08 owned database cleanup failed: $database" }
		}
	}
	finally {
		foreach ($name in $envNames) { [Environment]::SetEnvironmentVariable($name, $prior[$name]) }
		Pop-Location
	}
}
