# SIM database snapshots for the natural journey (docs/natural-ascension-altgard.md, NA-03).
#
#   Capture: play the natural Ishalgen journey once (NI-07, seed N) against an owned throwaway schema, then dump
#            that schema at the journey's natural endpoint into run/snapshots/<name>/ (git-ignored).
#            With -Bridge (NA-25) the journey continues over the Ascension bridge and the dump is taken at the
#            verified bridge endpoint in Altgard (the `altgard` snapshot).
#            With -AltgardLeg1 (AF-09) the capture starts from a restored `altgard` snapshot instead of a new character,
#            plays an Altgard leg (-Leg l1, the default; -Leg l2 -From altgard-l12 for Leg 2, AM-08; -Leg l3 -From altgard-l2 for Leg 3, AC-07; -Leg l4 -From altgard-l3 for Leg 4, AB-09; -Leg l5 -From altgard-l4 for Leg 5, AK-09; -Leg l6 -From altgard-l5 for Leg 6, AG-08) and dumps its endpoint.
#            The Leg 1 form:
#            Capital (PC-07): -CapitalStage start -Name pandaemonium-capital-start;
#            then -CapitalStage first -From pandaemonium-capital-start -Name pandaemonium-capital-first.
#            Leg 7 (AE-07): -AltgardLeg1 -Leg l7 -From altgard-l6 -Name altgard-l7.
#            Leg 8 (AO-05): -AltgardLeg1 -Leg l8 -From altgard-l7 -Name altgard-l8.
#            Leg 9 (AH-05): -AltgardLeg1 -Leg l9 -From altgard-l8 -Name altgard-l9.
#            Leg 10 (BC-07): -AltgardLeg1 -Leg l10 -From altgard-l9 -Name altgard-l10.
#            Leg 11 (ND-07): -AltgardLeg1 -Leg l11 -From altgard-l10 -Name altgard-l11.
#            Leg 12 (HM-07): -AltgardLeg1 -Leg l12 -From altgard-coingear -Name altgard-haramel-l12.
#            Morheim and Abyss entry (AX-03): -AltgardLeg1 -Leg ax -From altgard-rc-complete-s1 -Name <new name>.
#            plays Altgard Leg 1 (docs/natural-altgard-leveling.md) and dumps its verified endpoint (`altgard-l12`).
#   Restore: load a snapshot into a fresh owned schema and print the environment a resumed run needs.
#   Verify:  restore, resume the retained character once and require the journey endpoint to be reached again
#            (for `munin`: 41 quests, level 9 at Munin, Q2008 START/0), then drop the schema.
#   Drop:    drop one owned snapshot schema.
#   Replay:  play one scope and capture nothing (docs/natural-class-profiles.md, CP-03). The schema is a fresh owned one,
#            or a restored snapshot when the scope starts from one (-AltgardLeg1, -CapitalStage first, or -From alone,
#            which resumes the snapshot on its own environment). The scope switches are Capture's. -StopAt
#            <questId:status:packedVars> stops at a checkpoint and -StopAfterQuest <2004..2007> after a campaign quest.
#            The evidence goes to run/cp/<Item>/<Run>/, nothing is written under run/snapshots, and the schema is
#            dropped whether the run passes or fails. A working tree with uncommitted changes is allowed and recorded.
#
# Class lines (docs/natural-class-profiles.md, CP-28): -Class <line id> on Capture and Replay plays another class line
# (CP_CLASS). A snapshot of another line records it as classLine, Restore prints it back as CP_CLASS, and a run that
# starts from a snapshot plays the snapshot's line. The accepted line, priest-cleric, is recorded and printed nowhere,
# so its snapshots and their restores are as they always were. No line sets NA_HELP_ITEMS: the help supply is on.
#
# Snapshots are only ever made by natural play and are never edited. Every restore is a fresh copy because the
# Ascension class choice is irreversible. Only aion_gs_sim_ni08_* schemas on the development MySQL are touched.
param(
	[Parameter(Mandatory)]
	[ValidateSet('Capture', 'Restore', 'Verify', 'Drop', 'Replay')]
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
	[switch]$LaterCapital,
	[switch]$ContinuousJourney,
	[string]$From = 'altgard',
	[ValidateSet('l1', 'l2', 'l3', 'l4', 'l5', 'l6', 'l7', 'l8', 'l9', 'l10', 'l11', 'cg', 'l12', 'ax')]
	[string]$Leg = 'l1',
	[ValidateSet('start', 'first')]
	[string]$CapitalStage,
	[ValidatePattern('^\d+:[34]:\d+$')]
	[string]$CapitalRelogAt,
	# Capture and Replay: the class line to play; unset is the accepted line, or the line of the snapshot started from.
	[string]$Class,
	# Replay only: the stop boundary NI08_STOP_AT; a completed quest reads as status 5 with packed vars 0.
	[ValidatePattern('^\d+:\d+:\d+$')]
	[string]$StopAt,
	[ValidateSet(2004, 2005, 2006, 2007)]
	[int]$StopAfterQuest,
	# Replay only: the checklist item the run belongs to, and where its evidence folder is made.
	[ValidatePattern('^[A-Za-z0-9][A-Za-z0-9-]*$')]
	[string]$Item,
	[string]$ReplayRoot,
	[switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not $SnapshotRoot) { $SnapshotRoot = Join-Path $repoRoot 'run/snapshots' }
$ownedPattern = '^aion_gs_sim_ni08_[a-z0-9_]+$'
$playsScope = $Action -in @('Capture', 'Replay')
if ($CapitalStage -and ($Bridge -or $AltgardLeg1 -or -not $playsScope)) {
	throw 'CapitalStage is a contained Capture scope and cannot be combined with Bridge or AltgardLeg1.'
}
if ($CapitalRelogAt -and $CapitalStage -ne 'first') { throw 'CapitalRelogAt requires CapitalStage first.' }
if ($ContinuousJourney -and (-not $playsScope -or $Bridge -or $AltgardLeg1 -or $CapitalStage -or -not $LaterCapital)) {
	throw 'ContinuousJourney requires a revised fresh Capture with LaterCapital and no other starting scope.'
}
if ($LaterCapital -and (-not $playsScope -or $CapitalStage -or (-not $Bridge -and -not $AltgardLeg1 -and -not $ContinuousJourney))) {
	throw 'LaterCapital requires a bridge or Altgard Capture, without a contained capital checkpoint.'
}
if ($Action -ne 'Replay' -and ($StopAt -or $StopAfterQuest -or $Item -or $ReplayRoot)) {
	throw 'StopAt, StopAfterQuest, Item and ReplayRoot belong to Replay.'
}
# Read here: inside a function $PSBoundParameters is the function's own.
$fromGiven = $PSBoundParameters.ContainsKey('From')
# CP-28: the class lines of docs/natural-class-profiles.md. A line the journey does not hold yet fails later, in
# NaturalClassLine.Parse, with a message that names it. Only the accepted line plays the capital pass.
$classLines = @('priest-cleric', 'priest-chanter', 'warrior', 'scout', 'mage', 'engineer', 'artist')
$defaultClassLine = 'priest-cleric'
$capitalClassLines = @('priest-cleric')
if ($Class -and -not $playsScope) { throw 'Class belongs to Capture and Replay; a restored snapshot plays the line it recorded.' }
if ($Class -and $Class -cnotin $classLines) { throw "Unknown class line '$Class'. Known lines: $($classLines -join ', ')." }

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
		'AION_SIM_SEED', 'AION_NI07_COMBAT_DIR', 'NI07_FULL_JOURNEY', 'NI08_STOP_AT', 'NI08_RELOG_AT', 'NI08_RESUME_CHARACTER',
		'NA_ASCENSION', 'AF_ALTGARD', 'AF_ONLY', 'AF_CG_RECEIPTS', 'AF_HM_PROGRESS', 'PC_CAPITAL', 'RC_CAPITAL',
		'NI07_STOP_AFTER_Q2004', 'NI07_STOP_AFTER_Q2005', 'NI07_STOP_AFTER_Q2006', 'NI07_STOP_AFTER_Q2007', 'NI07_STOP_ON_DEATH', 'NI07_OPTIMIZE_HUBS',
		'AX_ARENA_FIRST_TRY', 'AX_RING_FIRST_TRY',
		# CP-03: an inherited NA_HELP_ITEMS=0 would turn the help supply off, and the other two would move the bot
		# monitor or the SIM process key. Cleared, the supply is on and the monitor is at its default port.
		'NA_HELP_ITEMS', 'AION_BOT_DASHBOARD_PORT', 'AION_SIM_PROCESS_KEY',
		# CP-15: an inherited CP_CLASS would play another class line. Cleared, the journey plays the accepted line.
		'CP_CLASS')
	$prior = @{}
	foreach ($variable in $names) {
		$prior[$variable] = [Environment]::GetEnvironmentVariable($variable)
		# PowerShell coerces $null to an empty string at this .NET overload on Windows.
		# The bot's optional scopes require absent variables, so remove them through the provider.
		Remove-Item -LiteralPath "Env:$variable" -ErrorAction SilentlyContinue
	}
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
		foreach ($variable in $names) {
			if ($null -eq $prior[$variable]) { Remove-Item -LiteralPath "Env:$variable" -ErrorAction SilentlyContinue }
			else { [Environment]::SetEnvironmentVariable($variable, $prior[$variable]) }
		}
	}
}

# The environment of a leg played from a restored base. AX-03: the base may be the Haramel endpoint, whose own
# environment names leg l12 and its receipt. The leg asked for wins, and only l12 may read the Haramel receipt.
function Get-LegEnvironment([Collections.IDictionary]$BaseEnvironment, [string]$LegId) {
	$extra = @{}
	foreach ($key in $BaseEnvironment.Keys) { $extra[$key] = $BaseEnvironment[$key] }
	$extra.AF_ALTGARD = $(if ($LegId -eq 'l1') { '1' } else { $LegId })
	if ($LegId -ne 'l12') { $extra.Remove('AF_HM_PROGRESS') }
	$extra
}

# CP-28: settle the class line of a run and return it. $Extra holds CP_CLASS already when the run starts from a snapshot
# of another line ($BaseName); such a run plays that line and -Class may only repeat it. A fresh run takes -Class. The
# accepted line sets nothing, so its child environment and its receipts stay as they were.
function Set-ClassLine([hashtable]$Extra, [string]$BaseName) {
	$line = if ($Extra.ContainsKey('CP_CLASS')) { [string]$Extra['CP_CLASS'] } else { $defaultClassLine }
	if ($BaseName) {
		if ($Class -and $Class -ne $line) { throw "Snapshot $BaseName holds class line $line; -Class $Class cannot play it." }
	} elseif ($Class -and $Class -ne $defaultClassLine) {
		$Extra['CP_CLASS'] = $Class
		$line = $Class
	}
	if ($CapitalStage -eq 'first' -and $line -notin $capitalClassLines) {
		throw "Class line $line has no capital leg; -CapitalStage first is the accepted line's."
	}
	$line
}

function Restore-Snapshot([string]$SnapshotName = $Name) {
	if (-not $SnapshotName) { throw "$Action needs -Name." }
	$directory = Join-Path $SnapshotRoot $SnapshotName
	$metadata = Get-Content -Raw -LiteralPath (Join-Path $directory 'snapshot.json') | ConvertFrom-Json
	$dump = Join-Path $directory 'dump.sql.gz'
	$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $dump).Hash.ToLowerInvariant()
	if ($hash -ne $metadata.dumpSha256) { throw "Snapshot $Name dump hash changed; refusing to restore an edited snapshot." }
	# CP-28: only a snapshot of another class line records one.
	$classLine = if ($metadata.PSObject.Properties.Name -contains 'classLine') { [string]$metadata.classLine } else { $null }
	if ($classLine -and $classLine -cnotin $classLines) {
		throw "Snapshot $SnapshotName records an unknown class line '$classLine'; refusing to restore."
	}
	$haramelProgress = $null
	if ($metadata.PSObject.Properties.Name -contains 'continuousJourney' -and $metadata.continuousJourney) {
		$continuousFile = Join-Path $directory 'continuous-completion.json'
		if (-not (Test-Path -LiteralPath $continuousFile) -or $metadata.PSObject.Properties.Name -notcontains 'continuousCompletionSha256' -or
			(Get-FileHash -Algorithm SHA256 -LiteralPath $continuousFile).Hash.ToLowerInvariant() -ne $metadata.continuousCompletionSha256) {
			throw 'Continuous snapshot is missing its unchanged completion receipt.'
		}
		$continuous = Get-Content -Raw -LiteralPath $continuousFile | ConvertFrom-Json
		if (-not $continuous.verified -or -not $continuous.CreatedCharacter -or -not $continuous.LaterCapital -or
			$continuous.CharacterId -ne $metadata.characterId -or $metadata.source -ne 'natural-altgard-l12' -or $continuous.stages.Count -ne 14) {
			throw 'Continuous snapshot has an invalid journey identity or scope.'
		}
	}
	$laterCapital = $metadata.PSObject.Properties.Name -contains 'laterCapital' -and $metadata.laterCapital
	if ($laterCapital) {
		$laterReceipt = Join-Path $directory 'later-capital-checkpoint.json'
		if (-not (Test-Path -LiteralPath $laterReceipt) -or $metadata.PSObject.Properties.Name -notcontains 'laterCapitalCheckpointSha256') {
			throw "Snapshot $SnapshotName is missing its later capital receipt; refusing to restore."
		}
		if ((Get-FileHash -Algorithm SHA256 -LiteralPath $laterReceipt).Hash.ToLowerInvariant() -ne $metadata.laterCapitalCheckpointSha256) {
			throw "Snapshot $SnapshotName later capital receipt hash changed; refusing to restore."
		}
		$expectedSegment = if ($metadata.source -eq 'natural-journey-ascension-bridge') { 'bridge' }
			elseif ($metadata.source -match '^natural-altgard-(l(?:[1-9]|1[0-2])|cg|ax)$') { $Matches[1] }
			else { throw 'Later capital snapshot has an unsupported source.' }
		$retained = Get-Content -Raw -LiteralPath $laterReceipt | ConvertFrom-Json
		if (-not $retained.verified -or $retained.schemaVersion -ne 1 -or $retained.segment -ne $expectedSegment -or
			$retained.characterId -ne $metadata.characterId -or $retained.before.characterId -ne $metadata.characterId -or
			$retained.after.characterId -ne $metadata.characterId) {
			throw "Snapshot $SnapshotName later capital receipt has the wrong identity or segment."
		}
	}
	if ($metadata.PSObject.Properties.Name -contains 'source' -and $metadata.source -like 'natural-capital-*') {
		$capitalReceipt = Join-Path $directory 'capital-stage-completion.json'
		if ($metadata.source -notin @('natural-capital-start', 'natural-capital-first') -or
			-not (Test-Path -LiteralPath $capitalReceipt) -or $metadata.PSObject.Properties.Name -notcontains 'capitalReceiptSha256') {
			throw "Snapshot $SnapshotName is missing its capital receipt; refusing to restore."
		}
		if ((Get-FileHash -Algorithm SHA256 -LiteralPath $capitalReceipt).Hash.ToLowerInvariant() -ne $metadata.capitalReceiptSha256) {
			throw "Snapshot $SnapshotName capital receipt hash changed; refusing to restore."
		}
		$capitalResult = Get-Content -Raw -LiteralPath $capitalReceipt | ConvertFrom-Json
		if (-not $capitalResult.verified -or $capitalResult.CharacterId -ne $metadata.characterId -or
			"natural-capital-$($capitalResult.Stage)" -ne $metadata.source) {
			throw "Snapshot $SnapshotName capital receipt has the wrong identity or stage; refusing to restore."
		}
	}
	if ($metadata.PSObject.Properties.Name -contains 'source' -and $metadata.source -eq 'natural-altgard-l12') {
		$haramelProgress = Join-Path $directory 'haramel-progress.json'
		if (-not (Test-Path -LiteralPath $haramelProgress) -or
			$metadata.PSObject.Properties.Name -notcontains 'haramelProgressSha256') {
			throw "Snapshot $SnapshotName is missing its Haramel receipt or hash; refusing to reset its journey budget."
		}
		if ((Get-FileHash -Algorithm SHA256 -LiteralPath $haramelProgress).Hash.ToLowerInvariant() -ne $metadata.haramelProgressSha256) {
			throw "Snapshot $SnapshotName Haramel receipt hash changed; refusing to restore edited receipts."
		}
	}
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
	$environment = [ordered]@{
		AION_SIM_NI08_DATABASE = $db
		NI08_RESUME_CHARACTER = "$($metadata.characterId)"
		AION_SIM_NI08_ELAPSED_MS = "$([long]$metadata.elapsedMillis + 20000)"
	}
	if ($haramelProgress) {
		$environment.AF_ALTGARD = 'l12'
		$environment.AF_HM_PROGRESS = $haramelProgress
	}
	# AX-13: the Abyss-entry endpoint stands in Morheim, which only its own leg accepts. A run resumed there
	# checks the endpoint from the fresh login, relogs once more and writes altgard-ax-endpoint-resume.json.
	if ($metadata.PSObject.Properties.Name -contains 'source' -and $metadata.source -eq 'natural-altgard-ax') {
		$environment.AF_ALTGARD = 'ax'
	}
	if ($metadata.PSObject.Properties.Name -contains 'source' -and $metadata.source -like 'natural-capital-*') {
		$environment.NA_ASCENSION = '1'
		# CP-28: a line with no capital leg resumes its capital snapshot on the start stage, which re-checks the
		# endpoint and stops; the accepted line resumes on the first pass, as it always did.
		$environment.PC_CAPITAL = $(if ($classLine -and $classLine -notin $capitalClassLines) { 'start' } else { 'first' })
	}
	if ($laterCapital) {
		$environment.NA_ASCENSION = '1'
		$environment.RC_CAPITAL = '1'
	}
	if ($classLine) { $environment.CP_CLASS = $classLine }
	[pscustomobject]@{
		snapshot = $SnapshotName
		database = $db
		characterId = [int]$metadata.characterId
		elapsedMillis = [long]$metadata.elapsedMillis
		# Environment for a resumed natural run on this copy (game time moves forward past the capture).
		environment = $environment
	}
}

# Preserve packet-observed state as an immutable receipt, never as SQL/quest setup instructions.
function Save-LaterCapitalCheckpoint([string]$Directory, [string]$Evidence, [Collections.IDictionary]$Metadata) {
	$receipt = Join-Path $Evidence 'later-capital-checkpoint.json'
	if (-not (Test-Path -LiteralPath $receipt)) { throw 'Later capital capture requires its verified checkpoint receipt.' }
	$retained = Get-Content -Raw -LiteralPath $receipt | ConvertFrom-Json
	if (-not $retained.verified -or $retained.characterId -ne $Metadata.characterId) {
		throw 'Later capital capture has an unverified or different character receipt.'
	}
	Copy-Item -LiteralPath $receipt -Destination $Directory
	$Metadata.laterCapital = $true
	$Metadata.laterCapitalCheckpointSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $receipt).Hash.ToLowerInvariant()
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
			$runtimeChanges = @(& git -C $repoRoot status --porcelain -- src tests game-server parity-artifacts scripts/sim/sim-snapshot.ps1)
			if ($runtimeChanges.Count) { throw 'Capture requires committed runtime and snapshot code; commit those changes first.' }
			if (-not $Run) { $Run = "snapshot-$Name-s$Seed" }
			$evidence = Join-Path $SnapshotRoot "_capture/$Run"
			if (-not $NoBuild) {
				& dotnet build (Join-Path $repoRoot 'tests/Aion.Simulation.Tests') -v quiet *> $null
				if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
			}
			if ($CapitalStage) {
				# A new natural prefix stops at the ceremony. The first pass restores that immutable
				# start through the public Restore action and captures the same contained run's endpoint.
				$extra = @{ NA_ASCENSION = '1'; PC_CAPITAL = $CapitalStage }
				$baseElapsed = 0L
				if ($CapitalStage -eq 'first') {
					$base = & $PSCommandPath -Action Restore -Name $From -Docker $Docker -ContainerName $ContainerName `
						-RootPassword $RootPassword -SnapshotRoot $SnapshotRoot | ConvertFrom-Json
					$db = $base.database
					foreach ($property in $base.environment.PSObject.Properties) { $extra[$property.Name] = [string]$property.Value }
					$baseElapsed = [long]$base.environment.AION_SIM_NI08_ELAPSED_MS
					if ($CapitalRelogAt) { $extra.NI08_RELOG_AT = $CapitalRelogAt }
				} else {
					$db = New-OwnedDatabaseName
					& pwsh -NoProfile -File (Join-Path $PSScriptRoot 'new-sim-db.ps1') -Action Create -DatabaseName $db -Docker $Docker | Out-Null
					if ($LASTEXITCODE -ne 0) { throw 'Could not create the owned capital capture schema.' }
				}
				try {
					$line = Set-ClassLine $extra $(if ($CapitalStage -eq 'first') { $From } else { $null })
					Invoke-NaturalJourney $db $Run $evidence $extra
					$capitalFile = Join-Path $evidence 'capital-stage-completion.json'
					$capitalResult = Get-Content -Raw -LiteralPath $capitalFile | ConvertFrom-Json
					if (-not $capitalResult.verified -or $capitalResult.Stage -ne $CapitalStage -or
						($CapitalStage -eq 'first' -and $capitalResult.CharacterId -ne $base.characterId)) {
						throw 'The capital checkpoint was not verified for its stage and retained identity; nothing was captured.'
					}
					New-Item -ItemType Directory -Path $directory | Out-Null
					$remote = "/tmp/$db.sql.gz"
					Invoke-Docker @('exec', '-e', "MYSQL_PWD=$RootPassword", $ContainerName, 'sh', '-c',
						"mysqldump -uroot --single-transaction --no-tablespaces --routines --triggers $db | gzip > $remote")
					Invoke-Docker @('cp', "${ContainerName}:$remote", (Join-Path $directory 'dump.sql.gz'))
					Invoke-Docker @('exec', $ContainerName, 'rm', '-f', $remote)
					Copy-Item -LiteralPath $capitalFile -Destination $directory
					$capitalMetadata = [ordered]@{
						schemaVersion = 1; name = $Name; source = "natural-capital-$CapitalStage"
						from = $(if ($CapitalStage -eq 'first') { $From } else { $null })
						run = $Run; seed = $Seed; gitSha = (& git -C $repoRoot rev-parse HEAD).Trim()
						capturedUtc = (Get-Date).ToUniversalTime().ToString('o'); characterId = [int]$capitalResult.CharacterId
						elapsedMillis = $baseElapsed + [long]$capitalResult.ElapsedMillis
						dumpSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $directory 'dump.sql.gz')).Hash.ToLowerInvariant()
						capitalReceiptSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $capitalFile).Hash.ToLowerInvariant()
					}
					if ($line -ne $defaultClassLine) { $capitalMetadata.classLine = $line }
					$capitalMetadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $directory 'snapshot.json') -Encoding utf8
					Write-Host "Captured capital $CapitalStage snapshot $Name (character $($capitalResult.CharacterId)) in $directory"
				} finally { Remove-OwnedDatabase $db }
				break
			}
			if ($AltgardLeg1) {
				# AF-09: Altgard Leg 1 resumes the character of a restored `altgard` snapshot; the new dump is taken at the
				# verified Leg 1 endpoint. Its clock continues the base snapshot's: the resume offset plus the run's own time.
				$base = Restore-Snapshot $From
				$db = $base.database
				try {
					$extra = Get-LegEnvironment $base.environment $Leg
					if ($LaterCapital) { $extra.RC_CAPITAL = '1' }
					$line = Set-ClassLine $extra $From
					Invoke-NaturalJourney $db $Run $evidence $extra
					$legFile = Join-Path $evidence "altgard-$Leg-completion.json"
					if (-not (Test-Path -LiteralPath $legFile)) { throw "Altgard leg $Leg did not complete; nothing was captured." }
					$legResult = Get-Content -Raw -LiteralPath $legFile | ConvertFrom-Json
					if (-not $legResult.verified -or $legResult.CharacterId -ne $base.characterId) {
						throw "The Altgard leg $Leg endpoint was not verified for the restored character; nothing was captured."
					}
					$haramelProgressFile = Join-Path $evidence 'haramel-progress.json'
					if ($Leg -eq 'l12' -and (-not (Test-Path -LiteralPath $haramelProgressFile) -or
						(Get-Content -Raw -LiteralPath $haramelProgressFile | ConvertFrom-Json).characterId -ne $base.characterId)) {
						throw 'The verified Haramel endpoint needs its original receipt; nothing was captured.'
					}
					New-Item -ItemType Directory -Path $directory | Out-Null
					$remote = "/tmp/$db.sql.gz"
					Invoke-Docker @('exec', '-e', "MYSQL_PWD=$RootPassword", $ContainerName, 'sh', '-c',
						"mysqldump -uroot --single-transaction --no-tablespaces --routines --triggers $db | gzip > $remote")
					Invoke-Docker @('cp', "${ContainerName}:$remote", (Join-Path $directory 'dump.sql.gz'))
					Invoke-Docker @('exec', $ContainerName, 'rm', '-f', $remote)
					Copy-Item -LiteralPath $legFile -Destination $directory
					$legMetadata = [ordered]@{
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
					}
					if ($Leg -eq 'l12') {
						Copy-Item -LiteralPath $haramelProgressFile -Destination $directory
						$legMetadata.haramelProgressSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $haramelProgressFile).Hash.ToLowerInvariant()
					}
					if ($extra.ContainsKey('RC_CAPITAL') -and $extra.RC_CAPITAL -eq '1') {
						Save-LaterCapitalCheckpoint $directory $evidence $legMetadata
					}
					if ($line -ne $defaultClassLine) { $legMetadata.classLine = $line }
					$legMetadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $directory 'snapshot.json') -Encoding utf8
					Write-Host "Captured snapshot $Name (character $($legResult.CharacterId)) in $directory"
				}
				finally {
					Remove-OwnedDatabase $db
				}
				break
			}
			$db = New-OwnedDatabaseName
			& pwsh -NoProfile -File (Join-Path $PSScriptRoot 'new-sim-db.ps1') -Action Create -DatabaseName $db -Docker $Docker | Out-Null
			if ($LASTEXITCODE -ne 0) { throw 'Could not create the owned capture schema.' }
			try {
				$prefixExtra = if ($Bridge) { @{ NA_ASCENSION = '1' } } else { @{} }
				if ($ContinuousJourney) {
					$prefixExtra = @{ NA_ASCENSION = '1'; AF_ALTGARD = 'all' }
					New-Item -ItemType Directory -Force -Path $evidence | Out-Null
					@{ database=$db; owned=$true; createdFresh=$true; restored=$false; elapsedMillis=0 } |
						ConvertTo-Json | Set-Content -LiteralPath (Join-Path $evidence 'schema-provenance.json')
				}
				if ($LaterCapital) { $prefixExtra.RC_CAPITAL = '1' }
				$line = Set-ClassLine $prefixExtra $null
				Invoke-NaturalJourney $db $Run $evidence $prefixExtra
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
				if ($ContinuousJourney) {
					$continuousFile = Join-Path $evidence 'continuous-completion.json'
					$clock = Get-Content -Raw -LiteralPath $continuousFile | ConvertFrom-Json
					if (-not $clock.verified -or -not $clock.CreatedCharacter -or -not $clock.LaterCapital -or
						$clock.CharacterId -ne $completion.CharacterId -or $clock.stages.Count -ne 14) {
						throw 'The revised continuous journey did not reach its verified fresh-character endpoint.'
					}
				}
				New-Item -ItemType Directory -Path $directory | Out-Null
				$remote = "/tmp/$db.sql.gz"
				Invoke-Docker @('exec', '-e', "MYSQL_PWD=$RootPassword", $ContainerName, 'sh', '-c',
					"mysqldump -uroot --single-transaction --no-tablespaces --routines --triggers $db | gzip > $remote")
				Invoke-Docker @('cp', "${ContainerName}:$remote", (Join-Path $directory 'dump.sql.gz'))
				Invoke-Docker @('exec', $ContainerName, 'rm', '-f', $remote)
				Copy-Item -LiteralPath (Join-Path $evidence 'completion.json') -Destination $directory
				if ($Bridge) { Copy-Item -LiteralPath (Join-Path $evidence 'bridge-completion.json') -Destination $directory }
				if ($ContinuousJourney) {
					foreach ($receipt in @('continuous-completion.json', 'altgard-l12-completion.json', 'haramel-progress.json')) {
						Copy-Item -LiteralPath (Join-Path $evidence $receipt) -Destination $directory
					}
				}
				$prefixMetadata = [ordered]@{
					schemaVersion = 1
					name = $Name
					source = $(if ($ContinuousJourney) { 'natural-altgard-l12' } elseif ($Bridge) { 'natural-journey-ascension-bridge' } else { 'natural-ishalgen-journey' })
					run = $Run
					seed = $Seed
					gitSha = (& git -C $repoRoot rev-parse HEAD).Trim()
					capturedUtc = (Get-Date).ToUniversalTime().ToString('o')
					characterId = [int]$completion.CharacterId
					elapsedMillis = [long]$clock.ElapsedMillis
					dumpSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $directory 'dump.sql.gz')).Hash.ToLowerInvariant()
				}
				if ($LaterCapital) { Save-LaterCapitalCheckpoint $directory $evidence $prefixMetadata }
				if ($ContinuousJourney) {
					$prefixMetadata.continuousJourney = $true
					$prefixMetadata.continuousCompletionSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $continuousFile).Hash.ToLowerInvariant()
					$prefixMetadata.haramelProgressSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $directory 'haramel-progress.json')).Hash.ToLowerInvariant()
				}
				if ($line -ne $defaultClassLine) { $prefixMetadata.classLine = $line }
				$prefixMetadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $directory 'snapshot.json') -Encoding utf8
				Write-Host "Captured snapshot $Name (character $($completion.CharacterId)) in $directory"
			}
			finally {
				Remove-OwnedDatabase $db
				if ($ContinuousJourney) {
					@{ database=$db; owned=$true; dropped=$true } | ConvertTo-Json |
						Set-Content -LiteralPath (Join-Path $evidence 'schema-cleanup.json')
				}
			}
		}
		'Replay' {
			if (-not $Item) { throw 'Replay needs -Item, the checklist item its evidence belongs to.' }
			if ($Name) { throw 'Replay captures nothing and takes no -Name; the snapshot to start from is -From.' }
			if ($CapitalRelogAt) { throw 'CapitalRelogAt belongs to the capital capture.' }
			if ($StopAt -and $StopAfterQuest) { throw 'StopAt and StopAfterQuest are two different stops; give one.' }
			if (-not $Run) { $Run = 'replay-' + (Get-Date -Format 'yyyyMMdd-HHmmss') }
			if ($Run -notmatch '^[A-Za-z0-9][A-Za-z0-9-]*$') { throw "Replay run id must be letters, digits and dashes: $Run" }
			if (-not $ReplayRoot) { $ReplayRoot = Join-Path $repoRoot 'run/cp' }
			$evidence = Join-Path (Join-Path $ReplayRoot $Item) $Run
			if (Test-Path -LiteralPath $evidence) { throw "Replay evidence already exists: $evidence. Choose a new -Run." }
			# A scope that starts from a snapshot: an Altgard leg, the capital first pass, or -From by itself, which
			# resumes the snapshot on the environment its Restore prints. Every other scope creates its character.
			$restores = $AltgardLeg1 -or $CapitalStage -eq 'first' -or $fromGiven
			if ($restores -and ($Bridge -or $ContinuousJourney -or $CapitalStage -eq 'start')) {
				throw 'This scope creates its own character and cannot start from a snapshot; leave out -From.'
			}
			if (-not $NoBuild) {
				& dotnet build (Join-Path $repoRoot 'tests/Aion.Simulation.Tests') -v quiet *> $null
				if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
			}
			$extra = @{}
			$base = $null
			$receipt = $null
			if ($restores) {
				$base = Restore-Snapshot $From
				$db = $base.database
				if ($AltgardLeg1) { $extra = Get-LegEnvironment $base.environment $Leg }
				else { foreach ($key in $base.environment.Keys) { $extra[$key] = $base.environment[$key] } }
			} else {
				$db = New-OwnedDatabaseName
				& pwsh -NoProfile -File (Join-Path $PSScriptRoot 'new-sim-db.ps1') -Action Create -DatabaseName $db -Docker $Docker | Out-Null
				if ($LASTEXITCODE -ne 0) { throw 'Could not create the owned replay schema.' }
			}
			try {
				Set-ClassLine $extra $(if ($restores) { $From } else { $null }) | Out-Null
				if ($CapitalStage) { $extra.NA_ASCENSION = '1'; $extra.PC_CAPITAL = $CapitalStage }
				if ($Bridge) { $extra.NA_ASCENSION = '1' }
				if ($ContinuousJourney) { $extra.NA_ASCENSION = '1'; $extra.AF_ALTGARD = 'all' }
				if ($LaterCapital) { $extra.RC_CAPITAL = '1' }
				if ($StopAt) { $extra.NI08_STOP_AT = $StopAt }
				if ($StopAfterQuest) { $extra["NI07_STOP_AFTER_Q$StopAfterQuest"] = '1' }
				New-Item -ItemType Directory -Force -Path $evidence | Out-Null
				if ($ContinuousJourney) {
					@{ database=$db; owned=$true; createdFresh=$true; restored=$false; elapsedMillis=0 } |
						ConvertTo-Json | Set-Content -LiteralPath (Join-Path $evidence 'schema-provenance.json')
				}
				$receiptPath = Join-Path $evidence 'replay.json'
				$receipt = [ordered]@{
					schemaVersion = 1; item = $Item; run = $Run; seed = $Seed
					from = $(if ($restores) { $From } else { $null })
					characterId = $(if ($restores) { $base.characterId } else { $null })
					environment = [ordered]@{}
					gitSha = (& git -C $repoRoot rev-parse HEAD).Trim()
					uncommitted = @(& git -C $repoRoot status --porcelain -- src tests game-server parity-artifacts scripts/sim)
					startedUtc = (Get-Date).ToUniversalTime().ToString('o')
					database = $db; passed = $false; schemaDropped = $false
				}
				foreach ($key in ($extra.Keys | Sort-Object)) { $receipt.environment[$key] = [string]$extra[$key] }
				$receipt | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $receiptPath -Encoding utf8
				Invoke-NaturalJourney $db $Run $evidence $extra
				$receipt.passed = $true
				Write-Host "Replayed $Item run $Run; nothing was captured. Evidence: $evidence"
			}
			finally {
				Remove-OwnedDatabase $db
				if ($receipt) {
					$receipt.schemaDropped = $true
					$receipt | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $receiptPath -Encoding utf8
				}
			}
		}
		'Restore' {
			Restore-Snapshot | ConvertTo-Json -Depth 4
		}
		'Verify' {
			$restored = Restore-Snapshot
			try {
				if (-not $Run) { $Run = "snapshot-$Name-verify-" + (Get-Date -Format 'yyyyMMddHHmmss') }
				$evidence = Join-Path $SnapshotRoot "_verify/$Run"
				$extra = @{}
				foreach ($key in $restored.environment.Keys) { $extra[$key] = $restored.environment[$key] }
				Invoke-NaturalJourney $restored.database $Run $evidence $extra
				$haramel = $extra.ContainsKey('AF_ALTGARD') -and $extra.AF_ALTGARD -eq 'l12'
				$abyssEntry = $extra.ContainsKey('AF_ALTGARD') -and $extra.AF_ALTGARD -eq 'ax'
				$capital = $extra.ContainsKey('PC_CAPITAL')
				$completionFile = if ($haramel) { 'altgard-l12-completion.json' } elseif ($abyssEntry) { 'altgard-ax-endpoint-resume.json' }
					elseif ($capital) { 'capital-stage-completion.json' } else { 'completion.json' }
				$completion = Get-Content -Raw -LiteralPath (Join-Path $evidence $completionFile) | ConvertFrom-Json
				if ($completion.CharacterId -ne $restored.characterId -or
					$(if ($haramel -or $abyssEntry -or $capital) { -not $completion.verified } else { $completion.Next.Outcome -ne 'complete' })) {
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
