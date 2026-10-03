[CmdletBinding()]
param(
	[ValidatePattern('^[a-z0-9][a-z0-9_-]*$')]
	[string]$Run = ('hm05-cold-' + (Get-Date -Format 'yyyyMMdd-HHmmss')),
	[int]$Seed = 1,
	[switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output = Join-Path $repoRoot "run/$Run"
if (Test-Path -LiteralPath $output) { throw "Evidence directory already exists: $output" }
$database = 'aion_gs_sim_ni08_' + [Guid]::NewGuid().ToString('N')
$envNames = @('AION_SIM_DB_INTEGRATION', 'AION_SIM_NI08_DATABASE', 'AION_SIM_NI08_ELAPSED_MS',
	'AION_SIM_RUN_ID', 'AION_SIM_SEED', 'AION_SIM_PROCESS_KEY', 'AION_SIM_HARAMEL_RESTART_PHASE',
	'AION_SIM_HARAMEL_RESTART_RECEIPT', 'AION_E2E_RUN_DIR')
$prior = @{}
foreach ($name in $envNames) { $prior[$name] = [Environment]::GetEnvironmentVariable($name) }
$created = $false
Push-Location $repoRoot
try {
	New-Item -ItemType Directory -Path $output | Out-Null
	if (!$NoBuild) {
		& dotnet build tests/Aion.Simulation.Tests -v quiet *> (Join-Path $output 'build.log')
		if ($LASTEXITCODE -ne 0) { throw 'Build failed; see build.log.' }
	}
	$created = $true # The GUID schema is owned even if import fails partway through.
	& pwsh -NoProfile -File scripts/sim/new-sim-db.ps1 -Action Create -DatabaseName $database | Out-Null
	if ($LASTEXITCODE -ne 0) { throw 'Could not create the owned HM-05 schema.' }
	foreach ($name in $envNames) { [Environment]::SetEnvironmentVariable($name, $null) }
	$env:AION_SIM_DB_INTEGRATION = '1'
	$env:AION_SIM_NI08_DATABASE = $database
	$env:AION_SIM_NI08_ELAPSED_MS = '0'
	$env:AION_SIM_SEED = "$Seed"
	$env:AION_SIM_PROCESS_KEY = 'hm05-cold'
	$env:AION_SIM_HARAMEL_RESTART_RECEIPT = Join-Path $output 'receipt.json'
	foreach ($phase in @('prepare', 'resume')) {
		$env:AION_SIM_HARAMEL_RESTART_PHASE = $phase
		$env:AION_SIM_RUN_ID = "$Run-$phase"
		& dotnet test tests/Aion.Simulation.Tests --no-build --filter 'FullyQualifiedName~HaramelColdRestartPreservesPaidSoupAndRecoversThroughTheActualExit' --logger 'console;verbosity=normal' *> (Join-Path $output "$phase.log")
		if ($LASTEXITCODE -ne 0) { throw "$phase failed; see $output/$phase.log" }
		if ($phase -eq 'prepare') {
			$receipt = Get-Content -Raw -LiteralPath $env:AION_SIM_HARAMEL_RESTART_RECEIPT | ConvertFrom-Json
			if ($receipt.CharacterId -le 0 -or $receipt.MapId -ne 300200000 -or $receipt.PackedCount -ne 1) {
				throw 'Preparation did not save the paid soup and partial kill state.'
			}
			$env:AION_SIM_NI08_ELAPSED_MS = "$([long]$receipt.ElapsedMillis + 20000)"
		}
	}
	Write-Output "HM-05 cold restart passed: character $($receipt.CharacterId), two actual processes; evidence $output"
}
finally {
	try {
		if ($created) {
			& pwsh -NoProfile -File scripts/sim/new-sim-db.ps1 -Action Drop -DatabaseName $database
			if ($LASTEXITCODE -ne 0) { throw "Owned HM-05 schema cleanup failed: $database" }
			Write-Output "Dropped owned schema $database"
		}
	}
	finally {
		foreach ($name in $envNames) { [Environment]::SetEnvironmentVariable($name, $prior[$name]) }
		Pop-Location
	}
}
