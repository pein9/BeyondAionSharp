# NR-47: a round (docs/natural-all-classes-ntc.md, rule (w), Survey C1 parts 7 and 8).
#
# Several bots, each playing alone, at the same time. A round is one world or several worlds side by side. A world is
# one process with a throwaway schema of its own; its bots take turns on its clock (SimulationTurnTable).
#
#   run-round.ps1 -Round <file> -Item NR-47 -Run <id> [-Capture <name>] [-From <name>] [-Parallel 8] [-NoBuild]
#
# The round file:
#   { "worlds": [ { "bots": [ { "line": "mage", "startAfterMinutes": 0, "stage": null, "stopAt": null, "name": null, "capture": "munin-mage-r1" } ] } ] }
#   line               the class line (NaturalClassLine).
#   stage              NR-R1a: how far the bot plays. Leave it out for the plain journey, to Munin. "bridge" goes on through
#                      the trial, the class choice, the ceremony, the capital pass and the dispatch to the Altgard bind, as
#                      sim-snapshot.ps1 -Bridge does for one bot. The bot's folder then holds bridge-completion.json.
#   startAfterMinutes  game minutes after the world begins; ten apart is the operator's figure.
#   stopAt             a diagnostic stop boundary (questId:status:packedVars). Leave it out to play to the stage's end.
#   name               the character's name when it is not the line's (the Cleric's and the Chanter's lines share one).
#   capture            with -Capture: the name of this character's own capture, written when the bot reached its end.
#
# -Capture <name>  When a world's last bot has ended its schema is dumped once: the round snapshot <name>-w<n>, with a
#                  record of every character in it, reached or stopped short. A bot's capture is a small record that
#                  points at the round snapshot and the character; the dump is not copied. sim-snapshot.ps1 restores
#                  and verifies such a capture like any other.
# -From <name>     Each world is restored from the round snapshot <name>-w<n>. A bot whose line has a character there
#                  resumes it, on its account and under its name; a bot whose line has none creates one.
#
# Evidence: <ReplayRoot>/<Item>/<Run>/w<n>/<line>/ for each bot (trace, the journey's receipts, outcome.json), and
# <ReplayRoot>/<Item>/<Run>/round.json with one outcome record for each bot. A bot that stops is an outcome, not a
# failure of the round: the exit code is 0 when every world played to its end.
param(
	[Parameter(Mandatory)]
	[string]$Round,
	[ValidatePattern('^[A-Za-z0-9][A-Za-z0-9-]*$')]
	[string]$Item = 'round',
	[ValidatePattern('^[A-Za-z0-9][A-Za-z0-9-]*$')]
	[string]$Run = ('round-' + (Get-Date -Format 'yyyyMMdd-HHmmss')),
	[ValidatePattern('^[a-z0-9][a-z0-9-]*$')]
	[string]$Capture,
	[ValidatePattern('^[a-z0-9][a-z0-9-]*$')]
	[string]$From,
	[int]$Seed = 1,
	[ValidateRange(1, 16)]
	[int]$Parallel = 8,
	[string]$Docker = 'docker',
	[string]$Git = 'git',
	[string]$ContainerName = 'aion-mysql',
	[string]$RootPassword = 'aion',
	[string]$SnapshotRoot,
	[string]$ReplayRoot,
	[string]$PowerShell = 'pwsh',
	[switch]$NoBuild,
	# Internal: play world <n> of the round in this process. The round starts one such process for each world.
	[int]$World = 0,
	[string]$BuiltSha
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not $SnapshotRoot) { $SnapshotRoot = Join-Path $repoRoot 'run/snapshots' }
if (-not $ReplayRoot) { $ReplayRoot = Join-Path $repoRoot 'run/cp' }
. (Join-Path $PSScriptRoot 'sim-run-marker.ps1')
$markerRoot = Get-SimRunMarkerRoot $repoRoot
$ownedPattern = '^aion_gs_sim_ni08_[a-z0-9_]+$'
$evidence = Join-Path (Join-Path $ReplayRoot $Item) $Run

function Invoke-Docker([string[]]$Arguments) {
	& $Docker @Arguments
	if ($LASTEXITCODE -ne 0) { throw "docker $($Arguments -join ' ') exited with $LASTEXITCODE" }
}

function Remove-OwnedDatabase([string]$Db) {
	if ($Db -notmatch $ownedPattern) { throw "Refusing to drop a schema this script does not own: $Db" }
	Invoke-Docker @('exec', '-e', "MYSQL_PWD=$RootPassword", $ContainerName, 'mysql', '-uroot', '-e', "DROP DATABASE IF EXISTS ``$Db``;")
}

function Get-Field($Object, [string]$Name) {
	if ($null -ne $Object -and $Object.PSObject.Properties.Name -contains $Name) { $Object.$Name } else { $null }
}

$roundFile = Get-Content -Raw -LiteralPath $Round | ConvertFrom-Json
$worlds = @(Get-Field $roundFile 'worlds')
if (-not $worlds.Count) { throw 'The round file names no world.' }
foreach ($entry in $worlds) { if (-not @(Get-Field $entry 'bots').Count) { throw 'A world of the round names no bot.' } }

if ($World -gt 0) {
	# ---- One world of the round, in this process. ----
	if ($World -gt $worlds.Count) { throw "The round has no world $World." }
	$bots = @($worlds[$World - 1].bots)
	$worldEvidence = Join-Path $evidence "w$World"
	New-Item -ItemType Directory -Force -Path $worldEvidence | Out-Null
	$db = 'aion_gs_sim_ni08_' + [Guid]::NewGuid().ToString('N')
	$elapsed = 0L
	$restoredFrom = $null
	$created = $false
	try {
		if ($From) {
			$restoredFrom = "$From-w$World"
			$source = Join-Path $SnapshotRoot $restoredFrom
			$record = Get-Content -Raw -LiteralPath (Join-Path $source 'snapshot.json') | ConvertFrom-Json
			$dump = Join-Path $source 'dump.sql.gz'
			if ($record.source -ne 'natural-round') { throw "$restoredFrom is not a round snapshot." }
			if ((Get-FileHash -Algorithm SHA256 -LiteralPath $dump).Hash.ToLowerInvariant() -ne $record.dumpSha256) {
				throw "Round snapshot $restoredFrom dump hash changed; refusing to restore an edited snapshot."
			}
			$remote = "/tmp/$db.sql.gz"
			Invoke-Docker @('exec', '-e', "MYSQL_PWD=$RootPassword", $ContainerName, 'mysql', '-uroot', '-e', "CREATE DATABASE ``$db``;")
			$created = $true
			Invoke-Docker @('cp', $dump, "${ContainerName}:$remote")
			Invoke-Docker @('exec', '-e', "MYSQL_PWD=$RootPassword", $ContainerName, 'sh', '-c', "gunzip < $remote | mysql -uroot $db && rm -f $remote")
			# Game time moves forward past the capture, as it does for a snapshot of one character.
			$elapsed = [long]$record.elapsedMillis + 20000
			$bots = @($bots | ForEach-Object {
				$bot = $_
				$kept = @($record.characters | Where-Object { $_.line -eq $bot.line -and $_.characterId -gt 0 })
				if ($kept.Count -gt 1) { throw "Round snapshot $restoredFrom holds two characters of line $($bot.line)." }
				$next = [ordered]@{}
				foreach ($property in $bot.PSObject.Properties) { $next[$property.Name] = $property.Value }
				if ($kept.Count -eq 1) {
					$next.resumeCharacter = [int]$kept[0].characterId
					$next.account = [int]$kept[0].account
					$next.name = [string]$kept[0].name
				}
				[pscustomobject]$next
			})
		}
		else {
			& $PowerShell -NoProfile -File (Join-Path $PSScriptRoot 'new-sim-db.ps1') -Action Create -DatabaseName $db -Docker $Docker | Out-Null
			if ($LASTEXITCODE -ne 0) { throw 'Could not create the owned schema of this world.' }
			$created = $true
		}
		$worldFile = Join-Path $worldEvidence 'world.json'
		[ordered]@{ bots = $bots } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $worldFile -Encoding utf8
		@{ database = $db; owned = $true; restoredFrom = $restoredFrom; elapsedMillis = $elapsed } |
			ConvertTo-Json | Set-Content -LiteralPath (Join-Path $worldEvidence 'schema-provenance.json')

		# The world's settings are this process's environment. Inherited switches that would change play are cleared.
		foreach ($name in @('NA_HELP_ITEMS', 'AION_BOT_DASHBOARD_PORT', 'AION_SIM_PROCESS_KEY', 'CP_CLASS', 'NI08_RESUME_CHARACTER',
			'NI08_RESUME_ACCOUNT', 'NI08_RESUME_NAME', 'NI08_STOP_AT', 'NI08_RELOG_AT', 'AF_ALTGARD', 'NA_ASCENSION', 'PC_CAPITAL', 'RC_CAPITAL')) {
			Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue
		}
		$env:AION_SIM_DB_INTEGRATION = '1'
		$env:AION_SIM_NI08_DATABASE = $db
		$env:AION_SIM_NI08_ELAPSED_MS = "$elapsed"
		$env:AION_SIM_SEED = "$Seed"
		$env:AION_SIM_RUN_ID = "$Run-w$World"
		$env:NR_ROUND_FILE = $worldFile
		$env:AION_NI07_COMBAT_DIR = $worldEvidence
		$marker = New-SimRunMarker $markerRoot "$Run-w$World"
		try {
			& dotnet test (Join-Path $repoRoot 'tests/Aion.Simulation.Tests') --no-build `
				--filter 'FullyQualifiedName~NaturalRoundPlaysItsBotsInOneWorld' `
				--logger 'console;verbosity=normal' *> (Join-Path $worldEvidence 'journey.log')
			if ($LASTEXITCODE -ne 0) { throw "World $World did not play to its end; see $worldEvidence/journey.log" }
		}
		finally { Remove-SimRunMarker $marker }

		$outcomeFile = Join-Path $worldEvidence 'round-outcome.json'
		if (-not (Test-Path -LiteralPath $outcomeFile)) { throw "World $World left no outcome record." }
		$outcome = Get-Content -Raw -LiteralPath $outcomeFile | ConvertFrom-Json
		if ($Capture) {
			# The world's schema, once, with every character in it as its bot left it.
			$name = "$Capture-w$World"
			$directory = Join-Path $SnapshotRoot $name
			if (Test-Path -LiteralPath $directory) { throw "Snapshot already exists: $directory (snapshots are never overwritten)" }
			New-Item -ItemType Directory -Path $directory | Out-Null
			$remote = "/tmp/$db.sql.gz"
			Invoke-Docker @('exec', '-e', "MYSQL_PWD=$RootPassword", $ContainerName, 'sh', '-c',
				"mysqldump -uroot --single-transaction --no-tablespaces --routines --triggers $db | gzip > $remote")
			Invoke-Docker @('cp', "${ContainerName}:$remote", (Join-Path $directory 'dump.sql.gz'))
			Invoke-Docker @('exec', $ContainerName, 'rm', '-f', $remote)
			Copy-Item -LiteralPath $outcomeFile -Destination $directory
			$captured = (Get-Date).ToUniversalTime().ToString('o')
			$characters = @($outcome.bots | ForEach-Object {
				[ordered]@{ seat = $_.Bot; line = $_.Line; stage = (Get-Field $_ 'Stage'); account = $_.Account; name = $_.Name; characterId = $_.CharacterId
					outcome = $_.Outcome; level = $_.Level; completedQuests = $_.CompletedQuests; step = $_.Step; message = $_.Message }
			})
			[ordered]@{
				schemaVersion = 1; name = $name; source = 'natural-round'; round = $Capture; world = $World; from = $restoredFrom
				run = "$Run-w$World"; seed = $Seed; gitSha = $BuiltSha; capturedUtc = $captured
				elapsedMillis = [long]$outcome.worldElapsedMillis
				dumpSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $directory 'dump.sql.gz')).Hash.ToLowerInvariant()
				roundOutcomeSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $outcomeFile).Hash.ToLowerInvariant()
				characters = $characters
			} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $directory 'snapshot.json') -Encoding utf8
			# A bot's own capture: a record that points at the round snapshot and its character.
			for ($index = 0; $index -lt $bots.Count; $index++) {
				$own = Get-Field $bots[$index] 'capture'
				$result = $outcome.bots[$index]
				if (-not $own -or $result.Outcome -ne 'reached' -or $result.CharacterId -le 0) { continue }
				$ownDirectory = Join-Path $SnapshotRoot $own
				if (Test-Path -LiteralPath $ownDirectory) { throw "Snapshot already exists: $ownDirectory (snapshots are never overwritten)" }
				New-Item -ItemType Directory -Path $ownDirectory | Out-Null
				[ordered]@{
					schemaVersion = 1; name = $own; source = 'natural-round-character'; roundSnapshot = $name
					classLine = $result.Line; characterId = $result.CharacterId; account = $result.Account; characterName = $result.Name
					stage = (Get-Field $result 'Stage'); level = $result.Level; completedQuests = $result.CompletedQuests
					run = "$Run-w$World"; seed = $Seed; gitSha = $BuiltSha; capturedUtc = $captured
				} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ownDirectory 'snapshot.json') -Encoding utf8
			}
		}
		Write-Host "World $World of round $Run played to its end. Evidence: $worldEvidence"
	}
	finally {
		if ($created) {
			Remove-OwnedDatabase $db
			@{ database = $db; owned = $true; dropped = $true } | ConvertTo-Json |
				Set-Content -LiteralPath (Join-Path $worldEvidence 'schema-cleanup.json')
		}
	}
	exit 0
}

# ---- The round: one build, then its worlds side by side. ----
if (Test-Path -LiteralPath $evidence) { throw "Round evidence already exists: $evidence. Choose a new -Run." }
if ($Capture) {
	# A capture is of committed code, as every capture is.
	$changes = @(& $Git -C $repoRoot status --porcelain -- src tests game-server parity-artifacts scripts/sim)
	if ($changes.Count) { throw 'A captured round requires committed runtime and script code; commit those changes first.' }
	for ($n = 1; $n -le $worlds.Count; $n++) {
		$taken = @("$Capture-w$n") + @($worlds[$n - 1].bots | ForEach-Object { Get-Field $_ 'capture' } | Where-Object { $_ })
		foreach ($name in $taken) {
			if ($name -notmatch '^[a-z0-9][a-z0-9-]*$') { throw "Not a snapshot name: $name" }
			if (Test-Path -LiteralPath (Join-Path $SnapshotRoot $name)) { throw "Snapshot already exists: $name (snapshots are never overwritten)" }
		}
	}
}
if ($From) {
	for ($n = 1; $n -le $worlds.Count; $n++) {
		if (-not (Test-Path -LiteralPath (Join-Path (Join-Path $SnapshotRoot "$From-w$n") 'snapshot.json'))) {
			throw "The round resumes from $From, which has no world $n."
		}
	}
}
$sha = (& $Git -C $repoRoot rev-parse HEAD | Out-String).Trim()
if ($sha -notmatch '^[0-9a-f]{40}$') { throw "Could not read the commit: $sha" }
if (-not $NoBuild) {
	Assert-NoSimRunBeforeBuild $markerRoot
	& dotnet build (Join-Path $repoRoot 'tests/Aion.Simulation.Tests') -v quiet *> $null
	if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}
New-Item -ItemType Directory -Force -Path $evidence | Out-Null
Copy-Item -LiteralPath $Round -Destination (Join-Path $evidence 'round-file.json')
$shared = @('-NoProfile', '-File', $PSCommandPath, '-Round', (Resolve-Path -LiteralPath $Round).Path, '-Item', $Item, '-Run', $Run,
	'-Seed', "$Seed", '-Docker', $Docker, '-Git', $Git, '-ContainerName', $ContainerName, '-RootPassword', $RootPassword,
	'-SnapshotRoot', $SnapshotRoot, '-ReplayRoot', $ReplayRoot, '-PowerShell', $PowerShell, '-BuiltSha', $sha)
if ($Capture) { $shared += @('-Capture', $Capture) }
if ($From) { $shared += @('-From', $From) }
$started = Get-Date
$finished = @(1..$worlds.Count | ForEach-Object -ThrottleLimit $Parallel -Parallel {
	$output = & $using:PowerShell @($using:shared) -World $_ 2>&1
	[pscustomobject]@{ world = $_; code = $LASTEXITCODE; last = ($output | Select-Object -Last 1 | Out-String).Trim() }
} | Sort-Object world)

$records = @()
$failed = @()
foreach ($done in $finished) {
	$outcomeFile = Join-Path (Join-Path $evidence "w$($done.world)") 'round-outcome.json'
	if ($done.code -ne 0 -or -not (Test-Path -LiteralPath $outcomeFile)) {
		$failed += "world $($done.world): $($done.last)"
		continue
	}
	$outcome = Get-Content -Raw -LiteralPath $outcomeFile | ConvertFrom-Json
	foreach ($bot in $outcome.bots) {
		$records += [ordered]@{ world = $done.world; seat = $bot.Bot; line = $bot.Line; stage = (Get-Field $bot 'Stage'); outcome = $bot.Outcome; level = $bot.Level
			completedQuests = $bot.CompletedQuests; gameMinutes = [math]::Round(($bot.EndedAtMillis - $bot.StartedAtMillis) / 60000.0, 1)
			step = $bot.Step; message = $bot.Message; account = $bot.Account; name = $bot.Name; characterId = $bot.CharacterId }
		Write-Host ("  w{0} {1} {2}: {3}, level {4}, {5} quests{6}" -f $done.world, $bot.Bot, $bot.Line, $bot.Outcome, $bot.Level,
			$bot.CompletedQuests, $(if ($bot.Message) { ": $($bot.Message)" } else { '' }))
	}
}
[ordered]@{
	schemaVersion = 1; item = $Item; run = $Run; seed = $Seed; commit = $sha; capture = $Capture; from = $From
	worlds = $worlds.Count; wallSeconds = [int]((Get-Date) - $started).TotalSeconds; failedWorlds = $failed; bots = $records
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $evidence 'round.json') -Encoding utf8
if ($failed.Count) {
	Write-Host "Round ${Run}: a world did not play to its end."
	foreach ($line in $failed) { Write-Host "  $line" }
	exit 1
}
Write-Host "Round $Run played: $($records.Count) bots in $($worlds.Count) worlds. Evidence: $evidence"
exit 0
