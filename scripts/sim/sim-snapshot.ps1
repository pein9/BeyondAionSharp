# SIM database snapshots for the natural journey (docs/natural-ascension-altgard.md, NA-03).
#
#   Capture: play the natural Ishalgen journey once (NI-07, seed N) against an owned throwaway schema, then dump
#            that schema at the journey's natural endpoint into run/snapshots/<name>/ (git-ignored).
#            With -Bridge (NA-25) the journey continues over the Ascension bridge and the dump is taken at the
#            verified bridge endpoint in Altgard (the `altgard` snapshot).
#            With -AltgardLeg1 (AF-09) the capture starts from a restored `altgard` snapshot instead of a new character,
#            plays an Altgard leg (-Leg l1, the default; -Leg l2 -From altgard-l12 for Leg 2, AM-08; -Leg l3 -From altgard-l2 for Leg 3, AC-07; -Leg l4 -From altgard-l3 for Leg 4, AB-09; -Leg l5 -From altgard-l4 for Leg 5, AK-09; -Leg l6 -From altgard-l5 for Leg 6, AG-08) and dumps its endpoint.
#            The Leg 1 form:
#            Leg 7 (AE-07): -AltgardLeg1 -Leg l7 -From altgard-l6 -Name altgard-l7.
#            Leg 8 (AO-05): -AltgardLeg1 -Leg l8 -From altgard-l7 -Name altgard-l8.
#            Leg 9 (AH-05): -AltgardLeg1 -Leg l9 -From altgard-l8 -Name altgard-l9.
#            Leg 10 (BC-07): -AltgardLeg1 -Leg l10 -From altgard-l9 -Name altgard-l10.
#            Leg 11 (ND-07): -AltgardLeg1 -Leg l11 -From altgard-l10 -Name altgard-l11.
#            plays Altgard Leg 1 (docs/natural-altgard-leveling.md) and dumps its verified endpoint (`altgard-l12`).
#   Restore: load a snapshot into a fresh owned schema and print the environment a resumed run needs.
#   Verify:  restore, resume the retained character once and require the journey endpoint to be reached again
#            (for `munin`: 41 quests, level 9 at Munin, Q2008 START/0), then drop the schema.
#   Drop:    drop one owned snapshot schema.
#
# Snapshots are only ever made by natural play and are never edited. Every restore is a fresh copy because the
# Ascension class choice is irreversible. Only aion_gs_sim_ni08_* schemas on the development MySQL are touched.
param(
	[Parameter(Mandatory)]
	[ValidateSet('Capture', 'Restore', 'Verify', 'Drop')]
	[string]$Action,
	[ValidatePattern('^[a-z0-9][a-z0-9-]*$')]
	[string]$Name,
	[string]$Database,
	[int]$Seed = 1,
	[string]$Run,
	[string]$Docker = 'docker',
	[string]$ContainerName = 'aion-mysql',
	[string]$RootPassword = 'aion',
	[string]$SnapshotRoot,
	[switch]$Bridge,
	[switch]$AltgardLeg1,
	[string]$From = 'altgard',
	[ValidateSet('l1', 'l2', 'l3', 'l4', 'l5', 'l6', 'l7', 'l8', 'l9', 'l10', 'l11', 'cg')]
	[string]$Leg = 'l1',
	[switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not $SnapshotRoot) { $SnapshotRoot = Join-Path $repoRoot 'run/snapshots' }
$ownedPattern = '^aion_gs_sim_ni08_[a-z0-9_]+$'

function Invoke-Docker([string[]]$Arguments) {
	& $Docker @Arguments
	if ($LASTEXITCODE -ne 0) { throw "docker $($Arguments -join ' ') exited with $LASTEXITCODE" }
}

function New-OwnedDatabaseName { 'aion_gs_sim_ni08_' + [Guid]::NewGuid().ToString('N') }

function Remove-OwnedDatabase([string]$Db) {
	if ($Db -notmatch $ownedPattern) { throw "Refusing to drop a schema this script does not own: $Db" }
	Invoke-Docker @('exec', '-e', "MYSQL_PWD=$RootPassword", $ContainerName, 'mysql', '-uroot', '-e', "DROP DATABASE IF EXISTS ``$Db``;")
}

function Get-SnapshotDirectory {
	if (-not $Name) { throw "$Action needs -Name." }
	Join-Path $SnapshotRoot $Name
}

function Invoke-NaturalJourney([string]$Db, [string]$RunId, [string]$Evidence, [hashtable]$Extra) {
	$names = @('AION_SIM_DB_INTEGRATION', 'AION_SIM_NI08_DATABASE', 'AION_SIM_NI08_ELAPSED_MS', 'AION_SIM_RUN_ID',
		'AION_SIM_SEED', 'AION_NI07_COMBAT_DIR', 'NI07_FULL_JOURNEY', 'NI08_STOP_AT', 'NI08_RELOG_AT', 'NI08_RESUME_CHARACTER', 'NA_ASCENSION', 'AF_ALTGARD', 'AF_CG_RECEIPTS')
	$prior = @{}
	foreach ($variable in $names) { $prior[$variable] = [Environment]::GetEnvironmentVariable($variable); [Environment]::SetEnvironmentVariable($variable, $null) }
	try {
		$env:AION_SIM_DB_INTEGRATION = '1'
		$env:AION_SIM_NI08_DATABASE = $Db
		$env:AION_SIM_NI08_ELAPSED_MS = '0'
		$env:AION_SIM_SEED = "$Seed"
		$env:AION_SIM_RUN_ID = $RunId
		$env:AION_NI07_COMBAT_DIR = $Evidence
		$env:NI07_FULL_JOURNEY = '1'
		foreach ($key in $Extra.Keys) { [Environment]::SetEnvironmentVariable($key, $Extra[$key]) }
		New-Item -ItemType Directory -Force -Path $Evidence | Out-Null
		& dotnet test (Join-Path $repoRoot 'tests/Aion.Simulation.Tests') --no-build `
			--filter 'FullyQualifiedName~NaturalIshalgenPriestCompletesFrozenJourneyWithoutSetup' `
			--logger 'console;verbosity=normal' *> (Join-Path $Evidence 'journey.log')
		if ($LASTEXITCODE -ne 0) { throw "Natural journey failed; see $Evidence/journey.log" }
	}
	finally {
		foreach ($variable in $names) { [Environment]::SetEnvironmentVariable($variable, $prior[$variable]) }
	}
}

function Restore-Snapshot([string]$SnapshotName = $Name) {
	if (-not $SnapshotName) { throw "$Action needs -Name." }
	$directory = Join-Path $SnapshotRoot $SnapshotName
	$metadata = Get-Content -Raw -LiteralPath (Join-Path $directory 'snapshot.json') | ConvertFrom-Json
	$dump = Join-Path $directory 'dump.sql.gz'
	$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $dump).Hash.ToLowerInvariant()
	if ($hash -ne $metadata.dumpSha256) { throw "Snapshot $Name dump hash changed; refusing to restore an edited snapshot." }
	$db = New-OwnedDatabaseName
	$remote = "/tmp/$db.sql.gz"
	Invoke-Docker @('exec', '-e', "MYSQL_PWD=$RootPassword", $ContainerName, 'mysql', '-uroot', '-e', "CREATE DATABASE ``$db``;")
	try {
		Invoke-Docker @('cp', $dump, "${ContainerName}:$remote")
		Invoke-Docker @('exec', '-e', "MYSQL_PWD=$RootPassword", $ContainerName, 'sh', '-c', "gunzip < $remote | mysql -uroot $db && rm -f $remote")
	}
	catch {
		Remove-OwnedDatabase $db
		throw
	}
	[pscustomobject]@{
		snapshot = $SnapshotName
		database = $db
		characterId = [int]$metadata.characterId
		elapsedMillis = [long]$metadata.elapsedMillis
		# Environment for a resumed natural run on this copy (game time moves forward past the capture).
		environment = [ordered]@{
			AION_SIM_NI08_DATABASE = $db
			NI08_RESUME_CHARACTER = "$($metadata.characterId)"
			AION_SIM_NI08_ELAPSED_MS = "$([long]$metadata.elapsedMillis + 20000)"
		}
	}
}

Push-Location $repoRoot
try {
	switch ($Action) {
		'Drop' {
			if (-not $Database) { throw 'Drop needs -Database.' }
			Remove-OwnedDatabase $Database
		}
		'Capture' {
			$directory = Get-SnapshotDirectory
			if (Test-Path -LiteralPath $directory) { throw "Snapshot already exists: $directory (snapshots are never overwritten)" }
			if (-not $Run) { $Run = "snapshot-$Name-s$Seed" }
			$evidence = Join-Path $repoRoot "run/snapshots/_capture/$Run"
			if (-not $NoBuild) {
				& dotnet build (Join-Path $repoRoot 'tests/Aion.Simulation.Tests') -v quiet *> $null
				if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
			}
			if ($AltgardLeg1) {
				# AF-09: Altgard Leg 1 resumes the character of a restored `altgard` snapshot; the new dump is taken at the
				# verified Leg 1 endpoint. Its clock continues the base snapshot's: the resume offset plus the run's own time.
				$base = Restore-Snapshot $From
				$db = $base.database
				try {
					$extra = @{ AF_ALTGARD = $(if ($Leg -eq 'l1') { '1' } else { $Leg }) }
					foreach ($key in $base.environment.Keys) { $extra[$key] = $base.environment[$key] }
					Invoke-NaturalJourney $db $Run $evidence $extra
					$legFile = Join-Path $evidence "altgard-$Leg-completion.json"
					if (-not (Test-Path -LiteralPath $legFile)) { throw "Altgard leg $Leg did not complete; nothing was captured." }
					$legResult = Get-Content -Raw -LiteralPath $legFile | ConvertFrom-Json
					if (-not $legResult.verified -or $legResult.CharacterId -ne $base.characterId) {
						throw "The Altgard leg $Leg endpoint was not verified for the restored character; nothing was captured."
					}
					New-Item -ItemType Directory -Path $directory | Out-Null
					$remote = "/tmp/$db.sql.gz"
					Invoke-Docker @('exec', '-e', "MYSQL_PWD=$RootPassword", $ContainerName, 'sh', '-c',
						"mysqldump -uroot --single-transaction --no-tablespaces --routines --triggers $db | gzip > $remote")
					Invoke-Docker @('cp', "${ContainerName}:$remote", (Join-Path $directory 'dump.sql.gz'))
					Invoke-Docker @('exec', $ContainerName, 'rm', '-f', $remote)
					Copy-Item -LiteralPath $legFile -Destination $directory
					[ordered]@{
						schemaVersion = 1
						name = $Name
						source = "natural-altgard-$Leg"
						from = $From
						run = $Run
						seed = $Seed
						gitSha = (& git -C $repoRoot rev-parse HEAD).Trim()
						capturedUtc = (Get-Date).ToUniversalTime().ToString('o')
						characterId = [int]$legResult.CharacterId
						elapsedMillis = [long]$base.environment.AION_SIM_NI08_ELAPSED_MS + [long]$legResult.ElapsedMillis
						dumpSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $directory 'dump.sql.gz')).Hash.ToLowerInvariant()
					} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $directory 'snapshot.json') -Encoding utf8
					Write-Host "Captured snapshot $Name (character $($legResult.CharacterId)) in $directory"
				}
				finally {
					Remove-OwnedDatabase $db
				}
				break
			}
			$db = New-OwnedDatabaseName
			& pwsh -NoProfile -File (Join-Path $PSScriptRoot 'new-sim-db.ps1') -Action Create -DatabaseName $db | Out-Null
			if ($LASTEXITCODE -ne 0) { throw 'Could not create the owned capture schema.' }
			try {
				Invoke-NaturalJourney $db $Run $evidence $(if ($Bridge) { @{ NA_ASCENSION = '1' } } else { @{} })
				$clock = Get-Content -Raw -LiteralPath (Join-Path $evidence 'completion-clock.json') | ConvertFrom-Json
				$completion = Get-Content -Raw -LiteralPath (Join-Path $evidence 'completion.json') | ConvertFrom-Json
				if ($completion.Next.Outcome -ne 'complete' -or $clock.CharacterId -ne $completion.CharacterId) {
					throw 'The journey did not reach its natural endpoint; nothing was captured.'
				}
				if ($Bridge) {
					# NA-25: the dump must be the verified bridge endpoint, with the bridge's own clock.
					$bridgeFile = Join-Path $evidence 'bridge-completion.json'
					if (-not (Test-Path -LiteralPath $bridgeFile)) { throw 'The Ascension bridge did not complete; nothing was captured.' }
					$bridgeCompletion = Get-Content -Raw -LiteralPath $bridgeFile | ConvertFrom-Json
					if (-not $bridgeCompletion.verified -or $bridgeCompletion.CharacterId -ne $completion.CharacterId) {
						throw 'The Ascension bridge endpoint was not verified; nothing was captured.'
					}
					$clock = $bridgeCompletion
				}
				New-Item -ItemType Directory -Path $directory | Out-Null
				$remote = "/tmp/$db.sql.gz"
				Invoke-Docker @('exec', '-e', "MYSQL_PWD=$RootPassword", $ContainerName, 'sh', '-c',
					"mysqldump -uroot --single-transaction --no-tablespaces --routines --triggers $db | gzip > $remote")
				Invoke-Docker @('cp', "${ContainerName}:$remote", (Join-Path $directory 'dump.sql.gz'))
				Invoke-Docker @('exec', $ContainerName, 'rm', '-f', $remote)
				Copy-Item -LiteralPath (Join-Path $evidence 'completion.json') -Destination $directory
				if ($Bridge) { Copy-Item -LiteralPath (Join-Path $evidence 'bridge-completion.json') -Destination $directory }
				[ordered]@{
					schemaVersion = 1
					name = $Name
					source = $(if ($Bridge) { 'natural-journey-ascension-bridge' } else { 'natural-ishalgen-journey' })
					run = $Run
					seed = $Seed
					gitSha = (& git -C $repoRoot rev-parse HEAD).Trim()
					capturedUtc = (Get-Date).ToUniversalTime().ToString('o')
					characterId = [int]$completion.CharacterId
					elapsedMillis = [long]$clock.ElapsedMillis
					dumpSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $directory 'dump.sql.gz')).Hash.ToLowerInvariant()
				} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $directory 'snapshot.json') -Encoding utf8
				Write-Host "Captured snapshot $Name (character $($completion.CharacterId)) in $directory"
			}
			finally {
				Remove-OwnedDatabase $db
			}
		}
		'Restore' {
			Restore-Snapshot | ConvertTo-Json -Depth 4
		}
		'Verify' {
			$restored = Restore-Snapshot
			try {
				if (-not $Run) { $Run = "snapshot-$Name-verify-" + (Get-Date -Format 'yyyyMMddHHmmss') }
				$evidence = Join-Path $repoRoot "run/snapshots/_verify/$Run"
				$extra = @{}
				foreach ($key in $restored.environment.Keys) { $extra[$key] = $restored.environment[$key] }
				Invoke-NaturalJourney $restored.database $Run $evidence $extra
				$completion = Get-Content -Raw -LiteralPath (Join-Path $evidence 'completion.json') | ConvertFrom-Json
				if ($completion.CharacterId -ne $restored.characterId -or $completion.Next.Outcome -ne 'complete') {
					throw 'The restored character did not reach the snapshot endpoint.'
				}
				Write-Host "Verified snapshot ${Name}: character $($restored.characterId) resumed at its endpoint. Evidence: $evidence"
			}
			finally {
				Remove-OwnedDatabase $restored.database
			}
		}
	}
}
finally {
	Pop-Location
}
