[CmdletBinding()]
param()
# Contract for scripts/sim/sim-snapshot.ps1 (NA-03) against a fake Docker: it only ever touches owned
# aion_gs_sim_ni08_* schemas, never overwrites or restores an edited snapshot, and a restore is a fresh copy.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
function Assert-True([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Assert-Throws([scriptblock]$Block, [string]$Like, [string]$Message) {
	try { & $Block; throw "NOT-THROWN" }
	catch { if ($_.Exception.Message -eq 'NOT-THROWN' -or $_.Exception.Message -notlike $Like) { throw "$Message ($($_.Exception.Message))" } }
}
$script = Join-Path $PSScriptRoot 'sim-snapshot.ps1'
$root = Join-Path ([IO.Path]::GetTempPath()) ('aion-sim-snapshot-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
$log = Join-Path $root 'docker.log'
$fake = Join-Path $root 'fake-docker.ps1'
Set-Content -LiteralPath $fake -Value @"
Add-Content -LiteralPath '$log' -Value (`$args -join ' ')
if ((`$args -join ' ') -like '*FAIL-IMPORT*') { exit 1 }
if (`$args[0] -eq 'ps') { 'aion-mysql' }
# A dump copied out of the container: Capture hashes the file it gets.
if (`$args[0] -eq 'cp' -and `$args[1] -like 'aion-mysql:*') { [IO.File]::WriteAllBytes(`$args[2], [byte[]](5, 6, 7, 8)) }
exit 0
"@
try {
	$snapshots = Join-Path $root 'snapshots'
	$munin = Join-Path $snapshots 'munin'
	New-Item -ItemType Directory -Path $munin | Out-Null
	[IO.File]::WriteAllBytes((Join-Path $munin 'dump.sql.gz'), [byte[]](1, 2, 3, 4))
	$hash = (Get-FileHash -Algorithm SHA256 (Join-Path $munin 'dump.sql.gz')).Hash.ToLowerInvariant()
	[ordered]@{ schemaVersion = 1; name = 'munin'; characterId = 4242; elapsedMillis = 1000; dumpSha256 = $hash } |
		ConvertTo-Json | Set-Content -LiteralPath (Join-Path $munin 'snapshot.json')

	# Restore: a fresh owned schema, the dump copied in and imported, and the resume environment.
	$restored = & $script -Action Restore -Name munin -Docker $fake -SnapshotRoot $snapshots | ConvertFrom-Json
	Assert-True ($restored.database -match '^aion_gs_sim_ni08_[0-9a-f]{32}$') 'Restore did not create an owned schema.'
	Assert-True ($restored.characterId -eq 4242 -and $restored.environment.NI08_RESUME_CHARACTER -eq '4242') 'Restore lost the retained character.'
	Assert-True ($restored.environment.AION_SIM_NI08_ELAPSED_MS -eq '21000') 'Restored game time must move forward past the capture.'
	$calls = @(Get-Content -LiteralPath $log)
	Assert-True ($calls[0].Contains("CREATE DATABASE ``$($restored.database)``;")) 'Restore must create its own schema first.'
	Assert-True ($calls[1] -like "cp *dump.sql.gz aion-mysql:/tmp/$($restored.database).sql.gz") 'Restore did not copy the dump in.'
	Assert-True ($calls[2] -like "*gunzip < /tmp/$($restored.database).sql.gz | mysql -uroot $($restored.database)*") 'Restore did not import into the new schema.'
	$again = & $script -Action Restore -Name munin -Docker $fake -SnapshotRoot $snapshots | ConvertFrom-Json
	Assert-True ($again.database -ne $restored.database) 'Every restore must be a fresh copy.'
	Assert-True ($restored.environment.PSObject.Properties.Name -notcontains 'AF_HM_PROGRESS') 'Historical snapshots gained a Haramel selector.'
	Assert-True ($restored.environment.PSObject.Properties.Name -notcontains 'PC_CAPITAL') 'Historical snapshots gained a capital selector.'
	Assert-True ($restored.environment.PSObject.Properties.Name -notcontains 'RC_CAPITAL') 'Historical snapshots gained later capital scope.'

	# Later capital prefixes retain explicit scope and an immutable packet-state receipt.
	$later = Join-Path $snapshots 'altgard-rc-prefix'
	New-Item -ItemType Directory -Path $later | Out-Null
	Copy-Item -LiteralPath (Join-Path $munin 'dump.sql.gz') -Destination $later
	$laterReceipt = Join-Path $later 'later-capital-checkpoint.json'
	[ordered]@{ schemaVersion=1; segment='l1'; characterId=4242; verified=$true;
		before=@{ characterId=4242 }; after=@{ characterId=4242 } } |
		ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $laterReceipt
	$laterMetadata = [ordered]@{ schemaVersion=1; name='altgard-rc-prefix'; source='natural-altgard-l1';
		characterId=4242; elapsedMillis=3000; dumpSha256=$hash; laterCapital=$true;
		laterCapitalCheckpointSha256=(Get-FileHash -Algorithm SHA256 $laterReceipt).Hash.ToLowerInvariant() }
	$laterMetadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $later 'snapshot.json')
	$laterRestored = & $script -Action Restore -Name altgard-rc-prefix -Docker $fake -SnapshotRoot $snapshots | ConvertFrom-Json
	Assert-True ($laterRestored.environment.RC_CAPITAL -eq '1' -and $laterRestored.environment.NA_ASCENSION -eq '1') 'Later capital restore lost its explicit scope.'
	Assert-True ($laterRestored.environment.PSObject.Properties.Name -notcontains 'PC_CAPITAL') 'Later capital restore selected the first-pass diagnostic.'
	Remove-Item -LiteralPath $log
	$laterMetadata.source = 'natural-altgard-l5'
	$laterMetadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $later 'snapshot.json')
	Assert-Throws { & $script -Action Restore -Name altgard-rc-prefix -Docker $fake -SnapshotRoot $snapshots } '*wrong identity or segment*' 'Later capital restore accepted the wrong segment.'
	Assert-True (-not (Test-Path -LiteralPath $log)) 'Rejected later capital segment touched MySQL.'
	Set-Content -LiteralPath $laterReceipt -Value '{}'
	Assert-Throws { & $script -Action Restore -Name altgard-rc-prefix -Docker $fake -SnapshotRoot $snapshots } '*receipt hash changed*' 'Edited later capital state was restored.'
	Remove-Item -LiteralPath $laterReceipt
	Assert-Throws { & $script -Action Restore -Name altgard-rc-prefix -Docker $fake -SnapshotRoot $snapshots } '*missing its later capital receipt*' 'Missing later capital state was restored.'
	Assert-True (-not (Test-Path -LiteralPath $log)) 'Edited/missing later capital state touched MySQL.'
	Assert-Throws { & $script -Action Capture -Name later-conflict -LaterCapital -CapitalStage first -Docker $fake -SnapshotRoot $snapshots -NoBuild } '*LaterCapital requires*' 'Later capital capture combined unrelated scopes.'
	Assert-Throws { & $script -Action Capture -Name continuous-conflict -ContinuousJourney -Bridge -LaterCapital -Docker $fake -SnapshotRoot $snapshots -NoBuild } '*ContinuousJourney requires*' 'Continuous capture combined a bridge starting scope.'
	Assert-Throws { & $script -Action Capture -Name continuous-conflict -ContinuousJourney -Docker $fake -SnapshotRoot $snapshots -NoBuild } '*ContinuousJourney requires*' 'Continuous capture omitted the revised scope.'
	$laterMetadata.continuousJourney = $true
	$laterMetadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $later 'snapshot.json')
	Assert-Throws { & $script -Action Restore -Name altgard-rc-prefix -Docker $fake -SnapshotRoot $snapshots } '*unchanged completion receipt*' 'Continuous restore accepted a missing completion receipt.'

	# Both capital endpoints require immutable relog evidence and select only the capital segment.
	foreach ($stage in @('start', 'first')) {
		$capitalName = "pandaemonium-capital-$stage"
		$capital = Join-Path $snapshots $capitalName
		New-Item -ItemType Directory -Path $capital | Out-Null
		Copy-Item -LiteralPath (Join-Path $munin 'dump.sql.gz') -Destination $capital
		$capitalReceipt = Join-Path $capital 'capital-stage-completion.json'
		[ordered]@{ Stage=$stage; CharacterId=4242; verified=$true } | ConvertTo-Json | Set-Content -LiteralPath $capitalReceipt
		$capitalMetadata = [ordered]@{ schemaVersion=1; name=$capitalName; source="natural-capital-$stage"; characterId=4242;
			elapsedMillis=3000; dumpSha256=$hash; capitalReceiptSha256=(Get-FileHash -Algorithm SHA256 $capitalReceipt).Hash.ToLowerInvariant() }
		$capitalMetadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $capital 'snapshot.json')
		$capitalRestored = & $script -Action Restore -Name $capitalName -Docker $fake -SnapshotRoot $snapshots | ConvertFrom-Json
		Assert-True ($capitalRestored.environment.NA_ASCENSION -eq '1' -and $capitalRestored.environment.PC_CAPITAL -eq 'first') 'Capital restore selected the whole journey.'
		Assert-True ($capitalRestored.environment.AION_SIM_NI08_ELAPSED_MS -eq '23000') 'Capital restore rewound game time.'
		Remove-Item -LiteralPath $log
		$capitalMetadata.characterId = 4243
		$capitalMetadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $capital 'snapshot.json')
		Assert-Throws { & $script -Action Restore -Name $capitalName -Docker $fake -SnapshotRoot $snapshots } '*wrong identity or stage*' 'Capital restore accepted a different identity.'
		Assert-True (-not (Test-Path -LiteralPath $log)) 'Rejected capital identity touched MySQL.'
		Set-Content -LiteralPath $capitalReceipt -Value '{"Stage":"first","CharacterId":4242,"verified":false}'
		Assert-Throws { & $script -Action Restore -Name $capitalName -Docker $fake -SnapshotRoot $snapshots } '*receipt hash changed*' 'Edited capital evidence was restored.'
		Assert-True (-not (Test-Path -LiteralPath $log)) 'Edited capital evidence touched MySQL.'
		Remove-Item -LiteralPath $capitalReceipt
		Assert-Throws { & $script -Action Restore -Name $capitalName -Docker $fake -SnapshotRoot $snapshots } '*missing its capital receipt*' 'Missing capital evidence was restored.'
	}
	Assert-Throws { & $script -Action Capture -Name capital-conflict -CapitalStage first -AltgardLeg1 -Docker $fake -SnapshotRoot $snapshots -NoBuild } '*cannot be combined*' 'Capital capture combined unrelated scopes.'
	Assert-Throws { & $script -Action Capture -Name capital-conflict -CapitalStage start -CapitalRelogAt '2912:3:1' -Docker $fake -SnapshotRoot $snapshots -NoBuild } '*requires CapitalStage first*' 'Starting capture injected a capital-only interruption.'

	# Exercise the real runner function with a fake dotnet command. Empty Windows environment
	# values are still non-null C# scopes: unrelated selectors must be absent in the child.
	$parseErrors = $null
	$parseTokens = $null
	$ast = [System.Management.Automation.Language.Parser]::ParseFile($script, [ref]$parseTokens, [ref]$parseErrors)
	$runner = $ast.Find({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Invoke-NaturalJourney' }, $true)
	. ([scriptblock]::Create($runner.Extent.Text))
	$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
	$Seed = 1
	$pcMockEnvPath = Join-Path $root 'runner-env.json'
	function dotnet {
		[ordered]@{ capital=[Environment]::GetEnvironmentVariable('PC_CAPITAL'); ascension=[Environment]::GetEnvironmentVariable('NA_ASCENSION');
			altgard=[Environment]::GetEnvironmentVariable('AF_ALTGARD'); coin=[Environment]::GetEnvironmentVariable('AF_CG_RECEIPTS');
			haramel=[Environment]::GetEnvironmentVariable('AF_HM_PROGRESS'); stop=[Environment]::GetEnvironmentVariable('NI08_STOP_AT');
			later=[Environment]::GetEnvironmentVariable('RC_CAPITAL') } |
			ConvertTo-Json | Set-Content -LiteralPath $pcMockEnvPath
		$global:LASTEXITCODE = 0
	}
	$env:AF_ALTGARD = 'l12'
	$env:AF_CG_RECEIPTS = 'old-coins'
	$env:AF_HM_PROGRESS = 'old-haramel'
	$env:RC_CAPITAL = '1'
	Remove-Item -LiteralPath Env:NI08_STOP_AT -ErrorAction SilentlyContinue
	try {
		Invoke-NaturalJourney 'aion_gs_sim_ni08_mock' 'mock-capital' (Join-Path $root 'runner') @{ NA_ASCENSION='1'; PC_CAPITAL='start' }
		$child = Get-Content -Raw -LiteralPath $pcMockEnvPath | ConvertFrom-Json
		Assert-True ($child.capital -eq 'start' -and $child.ascension -eq '1') 'Runner lost its explicit capital scope.'
		Assert-True ($null -eq $child.altgard -and $null -eq $child.coin -and $null -eq $child.haramel -and $null -eq $child.stop -and $null -eq $child.later) 'Runner left non-null conflicting Windows scopes.'
		Assert-True ($env:AF_ALTGARD -eq 'l12' -and $env:AF_CG_RECEIPTS -eq 'old-coins' -and $env:AF_HM_PROGRESS -eq 'old-haramel') 'Runner did not restore existing environment values.'
		Assert-True ($null -eq [Environment]::GetEnvironmentVariable('NI08_STOP_AT')) 'Runner restored an absent scope as an empty string.'
		Assert-True ($env:RC_CAPITAL -eq '1') 'Runner did not restore the parent later capital scope.'
		Invoke-NaturalJourney 'aion_gs_sim_ni08_mock' 'mock-later' (Join-Path $root 'runner-later') @{ AF_ALTGARD='l5'; RC_CAPITAL='1' }
		$child = Get-Content -Raw -LiteralPath $pcMockEnvPath | ConvertFrom-Json
		Assert-True ($child.later -eq '1' -and $child.altgard -eq 'l5' -and $null -eq $child.capital) 'Runner combined later capital with the first-pass checkpoint.'
	} finally { Remove-Item Function:dotnet }

	# AX-03: a leg played from the Haramel endpoint selects itself, and only l12 keeps the Haramel receipt.
	$legEnvironment = $ast.Find({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Get-LegEnvironment' }, $true)
	. ([scriptblock]::Create($legEnvironment.Extent.Text))
	$haramelBase = [ordered]@{ AION_SIM_NI08_DATABASE='aion_gs_sim_ni08_mock'; NI08_RESUME_CHARACTER='4242'; AF_ALTGARD='l12';
		AF_HM_PROGRESS='haramel-progress.json'; NA_ASCENSION='1'; RC_CAPITAL='1' }
	$ax = Get-LegEnvironment $haramelBase 'ax'
	Assert-True ($ax.AF_ALTGARD -eq 'ax' -and -not $ax.ContainsKey('AF_HM_PROGRESS')) 'A leg after Haramel kept the l12 selector or its receipt.'
	Assert-True ($ax.RC_CAPITAL -eq '1' -and $ax.NA_ASCENSION -eq '1' -and $ax.NI08_RESUME_CHARACTER -eq '4242') 'A leg after Haramel lost the retained character or its carried scope.'
	Assert-True ($haramelBase.AF_ALTGARD -eq 'l12' -and $haramelBase.AF_HM_PROGRESS -eq 'haramel-progress.json') 'Selecting a leg edited the restored base environment.'
	$again = Get-LegEnvironment $haramelBase 'l12'
	Assert-True ($again.AF_ALTGARD -eq 'l12' -and $again.AF_HM_PROGRESS -eq 'haramel-progress.json') 'Leg l12 lost its Haramel receipt.'
	$first = Get-LegEnvironment ([ordered]@{ NI08_RESUME_CHARACTER='4242' }) 'l1'
	Assert-True ($first.AF_ALTGARD -eq '1' -and $first.Count -eq 2) 'Leg 1 lost its historical selector.'
	Assert-Throws { & $script -Action Capture -Name unknown-leg -AltgardLeg1 -Leg zz -Docker $fake -SnapshotRoot $snapshots -NoBuild } '*zz*' 'An unknown leg was accepted.'

	# Haramel requires its immutable receipt and resumes the endpoint with its original budgets.
	$haramel = Join-Path $snapshots 'altgard-haramel-l12'
	New-Item -ItemType Directory -Path $haramel | Out-Null
	Copy-Item -LiteralPath (Join-Path $munin 'dump.sql.gz') -Destination $haramel
	$receipt = Join-Path $haramel 'haramel-progress.json'
	Set-Content -LiteralPath $receipt -Value '{"characterId":4242,"revives":2}'
	[ordered]@{ schemaVersion=1; name='altgard-haramel-l12'; source='natural-altgard-l12'; characterId=4242;
		elapsedMillis=2000; dumpSha256=$hash; haramelProgressSha256=(Get-FileHash -Algorithm SHA256 $receipt).Hash.ToLowerInvariant() } |
		ConvertTo-Json | Set-Content -LiteralPath (Join-Path $haramel 'snapshot.json')
	$haramelRestored = & $script -Action Restore -Name altgard-haramel-l12 -Docker $fake -SnapshotRoot $snapshots | ConvertFrom-Json
	Assert-True ($haramelRestored.environment.AF_ALTGARD -eq 'l12' -and $haramelRestored.environment.AF_HM_PROGRESS -eq $receipt) 'Haramel restore lost its selector or original receipt.'
	Assert-True ($haramelRestored.environment.AION_SIM_NI08_ELAPSED_MS -eq '22000') 'Haramel restore rewound game time.'
	Remove-Item -LiteralPath $log
	Set-Content -LiteralPath $receipt -Value '{"characterId":4242,"revives":0}'
	Assert-Throws { & $script -Action Restore -Name altgard-haramel-l12 -Docker $fake -SnapshotRoot $snapshots } '*receipt hash changed*' 'Edited Haramel budgets were restored.'
	Assert-True (-not (Test-Path -LiteralPath $log)) 'Edited receipts touched MySQL.'
	Remove-Item -LiteralPath $receipt
	Assert-Throws { & $script -Action Restore -Name altgard-haramel-l12 -Docker $fake -SnapshotRoot $snapshots } '*missing its Haramel receipt*' 'Missing Haramel budgets were restored.'
	Assert-True (-not (Test-Path -LiteralPath $log)) 'Missing receipts touched MySQL.'

	# AX-13: an Abyss-entry endpoint resumes on its own leg, and a later leg still selects itself.
	$abyss = Join-Path $snapshots 'morheim-abyss-entry-mock'
	New-Item -ItemType Directory -Path $abyss | Out-Null
	Copy-Item -LiteralPath (Join-Path $munin 'dump.sql.gz') -Destination $abyss
	[ordered]@{ schemaVersion=1; name='morheim-abyss-entry-mock'; source='natural-altgard-ax'; characterId=4242;
		elapsedMillis=3000; dumpSha256=$hash } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $abyss 'snapshot.json')
	$abyssRestored = & $script -Action Restore -Name morheim-abyss-entry-mock -Docker $fake -SnapshotRoot $snapshots | ConvertFrom-Json
	Assert-True ($abyssRestored.environment.AF_ALTGARD -eq 'ax' -and $abyssRestored.environment.NI08_RESUME_CHARACTER -eq '4242') 'An Abyss-entry endpoint did not resume on its own leg.'
	Assert-True ($abyssRestored.environment.AION_SIM_NI08_ELAPSED_MS -eq '23000') 'An Abyss-entry restore rewound game time.'
	$abyssBase = [ordered]@{}
	foreach ($property in $abyssRestored.environment.PSObject.Properties) { $abyssBase[$property.Name] = [string]$property.Value }
	Assert-True ((Get-LegEnvironment $abyssBase 'l12').AF_ALTGARD -eq 'l12') 'A leg after the Abyss-entry endpoint kept the ax selector.'

	# CP-03: Replay plays one scope on a fresh or a restored owned schema, captures nothing, writes its evidence under
	# <ReplayRoot>/<Item>/<Run>/ and drops the schema whether the journey passes or fails. The journey is a fake dotnet.
	$replays = Join-Path $root 'cp'
	$replayEnvPath = Join-Path $root 'replay-env.json'
	$buildLog = Join-Path $root 'builds.log'
	$seenNames = @('AION_SIM_NI08_DATABASE', 'AION_SIM_NI08_ELAPSED_MS', 'AION_SIM_RUN_ID', 'AION_SIM_SEED', 'AION_NI07_COMBAT_DIR',
		'NI07_FULL_JOURNEY', 'NI08_STOP_AT', 'NI08_RESUME_CHARACTER', 'NA_ASCENSION', 'AF_ALTGARD', 'AF_HM_PROGRESS', 'PC_CAPITAL',
		'RC_CAPITAL', 'NI07_STOP_AFTER_Q2004', 'NI07_STOP_AFTER_Q2005', 'NA_HELP_ITEMS', 'AION_BOT_DASHBOARD_PORT', 'AION_SIM_PROCESS_KEY',
		'CP_CLASS')
	function dotnet {
		if ($args[0] -eq 'build') { Add-Content -LiteralPath $buildLog -Value 'build'; $global:LASTEXITCODE = 0; return }
		$seen = [ordered]@{}
		foreach ($name in $seenNames) { $seen[$name] = [Environment]::GetEnvironmentVariable($name) }
		$seen | ConvertTo-Json | Set-Content -LiteralPath $replayEnvPath
		$global:LASTEXITCODE = [int]$env:CP03_FAKE_JOURNEY_EXIT
	}
	function Get-SnapshotListing { @(Get-ChildItem -LiteralPath $snapshots -Recurse -Name | Sort-Object) -join '|' }
	# One Replay: its child environment, its docker calls and its receipt.
	function Invoke-Replay([string]$RunId, [hashtable]$Scope) {
		if (Test-Path -LiteralPath $log) { Remove-Item -LiteralPath $log }
		& $script -Action Replay -Item CP-TEST -Run $RunId -Docker $fake -SnapshotRoot $snapshots -ReplayRoot $replays -NoBuild @Scope | Out-Null
		[pscustomobject]@{
			child = Get-Content -Raw -LiteralPath $replayEnvPath | ConvertFrom-Json
			calls = @(Get-Content -LiteralPath $log)
			evidence = Join-Path (Join-Path $replays 'CP-TEST') $RunId
			receipt = Get-Content -Raw -LiteralPath (Join-Path (Join-Path (Join-Path $replays 'CP-TEST') $RunId) 'replay.json') | ConvertFrom-Json
		}
	}
	$replayPrior = @{}
	foreach ($name in @('NA_HELP_ITEMS', 'AION_BOT_DASHBOARD_PORT', 'AION_SIM_PROCESS_KEY', 'CP_CLASS', 'CP03_FAKE_JOURNEY_EXIT')) {
		$replayPrior[$name] = [Environment]::GetEnvironmentVariable($name)
	}
	$env:NA_HELP_ITEMS = '0'
	$env:AION_BOT_DASHBOARD_PORT = '0'
	$env:AION_SIM_PROCESS_KEY = 'shard-09'
	$env:CP_CLASS = 'warrior'
	$env:CP03_FAKE_JOURNEY_EXIT = '0'
	$listing = Get-SnapshotListing
	try {
		# A historical Restore prints exactly what it prints today.
		$plain = & $script -Action Restore -Name munin -Docker $fake -SnapshotRoot $snapshots | ConvertFrom-Json
		Assert-True (($plain.PSObject.Properties.Name -join ',') -eq 'snapshot,database,characterId,elapsedMillis,environment') 'Restore changed what it prints.'
		Assert-True (($plain.environment.PSObject.Properties.Name -join ',') -eq 'AION_SIM_NI08_DATABASE,NI08_RESUME_CHARACTER,AION_SIM_NI08_ELAPSED_MS') 'Restore changed the environment it prints.'
		Assert-True ($plain.snapshot -eq 'munin' -and $plain.elapsedMillis -eq 1000) 'Restore changed its values.'

		# Scope m: a fresh character on a fresh owned schema, no scope selector, and no inherited switch.
		$m = Invoke-Replay 'm1' @{}
		$mDb = $m.child.AION_SIM_NI08_DATABASE
		Assert-True ($mDb -match '^aion_gs_sim_ni08_[0-9a-f]{32}$') 'Replay did not play on an owned schema.'
		Assert-True (@($m.calls | Where-Object { $_.Contains("CREATE DATABASE ``$mDb``") }).Count -eq 1) 'The fresh schema was not created through the given docker command.'
		Assert-True (@($m.calls | Where-Object { $_ -like "cp *aion_gs.sql aion-mysql:/tmp/$mDb.sql" }).Count -eq 1) 'The fresh schema did not get the game schema.'
		Assert-True ($m.calls[-1].Contains("DROP DATABASE IF EXISTS ``$mDb``;")) 'Replay did not drop its fresh schema.'
		Assert-True (@($m.calls | Where-Object { $_ -like '*mysqldump*' }).Count -eq 0) 'Replay dumped a schema.'
		Assert-True ($m.child.NI07_FULL_JOURNEY -eq '1' -and $m.child.AION_SIM_RUN_ID -eq 'm1' -and $m.child.AION_SIM_SEED -eq '1') 'Replay lost the journey switch, its run id or its seed.'
		Assert-True ($m.child.AION_NI07_COMBAT_DIR -eq $m.evidence -and $m.child.AION_SIM_NI08_ELAPSED_MS -eq '0') 'Replay did not send its evidence to its own folder.'
		Assert-True ($null -eq $m.child.NA_ASCENSION -and $null -eq $m.child.PC_CAPITAL -and $null -eq $m.child.RC_CAPITAL -and
			$null -eq $m.child.AF_ALTGARD -and $null -eq $m.child.AF_HM_PROGRESS -and $null -eq $m.child.NI08_STOP_AT -and
			$null -eq $m.child.NI08_RESUME_CHARACTER -and $null -eq $m.child.NI07_STOP_AFTER_Q2004) 'A fresh Replay inherited a scope.'
		Assert-True ($null -eq $m.child.NA_HELP_ITEMS -and $null -eq $m.child.AION_BOT_DASHBOARD_PORT -and $null -eq $m.child.AION_SIM_PROCESS_KEY) 'The journey inherited the help-item switch, the dashboard port or the process key.'
		Assert-True ($env:NA_HELP_ITEMS -eq '0' -and $env:AION_BOT_DASHBOARD_PORT -eq '0' -and $env:AION_SIM_PROCESS_KEY -eq 'shard-09') 'Replay did not put the parent environment back.'
		Assert-True ($null -eq $m.child.CP_CLASS -and $env:CP_CLASS -eq 'warrior') 'The journey inherited a class line, or Replay did not put it back.'
		Assert-True ($m.receipt.passed -and $m.receipt.schemaDropped -and $m.receipt.item -eq 'CP-TEST' -and $m.receipt.run -eq 'm1' -and
			$null -eq $m.receipt.from -and $m.receipt.database -eq $mDb -and @($m.receipt.environment.PSObject.Properties).Count -eq 0) 'The fresh Replay receipt is wrong.'
		Assert-True (Test-Path -LiteralPath (Join-Path $m.evidence 'journey.log')) 'Replay kept no journey log.'
		Assert-True ((Get-SnapshotListing) -eq $listing) 'A fresh Replay wrote under the snapshot root.'

		# Scopes p and b, and both stops. Status 5 is a completed quest.
		$p = Invoke-Replay 'p1' @{ CapitalStage = 'start' }
		Assert-True ($p.child.PC_CAPITAL -eq 'start' -and $p.child.NA_ASCENSION -eq '1' -and $null -eq $p.child.NI08_RESUME_CHARACTER) 'Replay lost the capital-start scope.'
		Assert-True ($p.receipt.environment.PC_CAPITAL -eq 'start' -and $p.calls[-1].Contains("DROP DATABASE IF EXISTS ``$($p.child.AION_SIM_NI08_DATABASE)``;")) 'The capital-start Replay kept its schema or lost its receipt.'
		$b = Invoke-Replay 'b1' @{ Bridge = $true; LaterCapital = $true }
		Assert-True ($b.child.NA_ASCENSION -eq '1' -and $b.child.RC_CAPITAL -eq '1' -and $null -eq $b.child.PC_CAPITAL) 'Replay lost the bridge scope.'
		$stopped = Invoke-Replay 's1' @{ StopAt = '2132:5:0' }
		Assert-True ($stopped.child.NI08_STOP_AT -eq '2132:5:0' -and $null -eq $stopped.child.NI07_STOP_AFTER_Q2004) 'Replay did not pass a status-5 stop boundary.'
		$after = Invoke-Replay 's2' @{ StopAfterQuest = 2004 }
		Assert-True ($after.child.NI07_STOP_AFTER_Q2004 -eq '1' -and $null -eq $after.child.NI07_STOP_AFTER_Q2005 -and $null -eq $after.child.NI08_STOP_AT) 'Replay did not pass the stop after a campaign quest.'
		Assert-True ((Get-SnapshotListing) -eq $listing) 'A fresh Replay scope wrote under the snapshot root.'

		# -From: Restore-Snapshot gives the schema and the resume environment, and the copy is dropped.
		$resumed = Invoke-Replay 'r1' @{ From = 'munin' }
		$rDb = $resumed.child.AION_SIM_NI08_DATABASE
		Assert-True ($resumed.calls[0].Contains("CREATE DATABASE ``$rDb``;") -and $resumed.calls[2] -like "*gunzip < /tmp/$rDb.sql.gz | mysql -uroot $rDb*") 'Replay did not restore the snapshot into its own schema.'
		Assert-True ($resumed.calls.Count -eq 4 -and $resumed.calls[-1].Contains("DROP DATABASE IF EXISTS ``$rDb``;")) 'Replay did not drop its restored schema, or did more than restore, play and drop.'
		Assert-True ($resumed.child.NI08_RESUME_CHARACTER -eq '4242' -and $resumed.child.AION_SIM_NI08_ELAPSED_MS -eq '21000') 'Replay lost the retained character or its clock.'
		Assert-True ($resumed.receipt.from -eq 'munin' -and $resumed.receipt.characterId -eq 4242 -and $resumed.receipt.environment.NI08_RESUME_CHARACTER -eq '4242') 'The restored Replay receipt is wrong.'
		$leg = Invoke-Replay 'l1' @{ AltgardLeg1 = $true; Leg = 'l12'; From = 'morheim-abyss-entry-mock' }
		Assert-True ($leg.child.AF_ALTGARD -eq 'l12' -and $leg.child.NI08_RESUME_CHARACTER -eq '4242' -and $null -eq $leg.child.AF_HM_PROGRESS) 'A leg Replay did not select its leg on the restored character.'
		Assert-True ($leg.calls[-1].Contains("DROP DATABASE IF EXISTS ``$($leg.child.AION_SIM_NI08_DATABASE)``;")) 'A leg Replay kept its schema.'
		Assert-True ((Get-SnapshotListing) -eq $listing) 'A restored Replay wrote under the snapshot root.'

		# Without -NoBuild the Replay builds once before it plays.
		if (Test-Path -LiteralPath $buildLog) { Remove-Item -LiteralPath $buildLog }
		& $script -Action Replay -Item CP-TEST -Run built1 -Docker $fake -SnapshotRoot $snapshots -ReplayRoot $replays | Out-Null
		Assert-True (@(Get-Content -LiteralPath $buildLog).Count -eq 1) 'Replay did not build once.'

		# A failed journey: both forms drop the schema, keep the evidence and capture nothing.
		$env:CP03_FAKE_JOURNEY_EXIT = '1'
		foreach ($failure in @(@{ run = 'f1'; scope = @{} }, @{ run = 'f2'; scope = @{ From = 'munin' } })) {
			Assert-Throws { Invoke-Replay $failure.run $failure.scope } '*Natural journey failed*' 'A failed journey was reported as a pass.'
			$failed = Get-Content -Raw -LiteralPath $replayEnvPath | ConvertFrom-Json
			$failedCalls = @(Get-Content -LiteralPath $log)
			Assert-True ($failedCalls[-1].Contains("DROP DATABASE IF EXISTS ``$($failed.AION_SIM_NI08_DATABASE)``;")) 'A failed Replay kept its schema.'
			$failedReceipt = Get-Content -Raw -LiteralPath (Join-Path (Join-Path (Join-Path $replays 'CP-TEST') $failure.run) 'replay.json') | ConvertFrom-Json
			Assert-True (-not $failedReceipt.passed -and $failedReceipt.schemaDropped) 'A failed Replay receipt does not say what happened.'
		}
		$env:CP03_FAKE_JOURNEY_EXIT = '0'
		Assert-True ((Get-SnapshotListing) -eq $listing) 'A failed Replay wrote under the snapshot root.'

		# What Replay refuses, before it touches MySQL.
		Remove-Item -LiteralPath $log
		$refuse = @{ Action = 'Replay'; Docker = $fake; SnapshotRoot = $snapshots; ReplayRoot = $replays; NoBuild = $true }
		Assert-Throws { & $script @refuse -Item CP-TEST -Run m1 } '*already exists*' 'Two Replay attempts shared an evidence folder.'
		Assert-Throws { & $script @refuse -Run noitem } '*needs -Item*' 'Replay ran without its checklist item.'
		Assert-Throws { & $script @refuse -Item CP-TEST -Run named -Name munin } '*takes no -Name*' 'Replay accepted a snapshot name to capture.'
		Assert-Throws { & $script @refuse -Item CP-TEST -Run both -StopAt '2004:5:0' -StopAfterQuest 2004 } '*two different stops*' 'Replay accepted two stops.'
		Assert-Throws { & $script @refuse -Item CP-TEST -Run badstop -StopAt '2004-5-0' } '*StopAt*' 'Replay accepted a malformed stop boundary.'
		Assert-Throws { & $script @refuse -Item CP-TEST -Run badquest -StopAfterQuest 2003 } '*StopAfterQuest*' 'Replay accepted a stop after a quest that has none.'
		Assert-Throws { & $script @refuse -Item '../CP' -Run baditem } '*Item*' 'Replay accepted an unsafe item id.'
		Assert-Throws { & $script @refuse -Item CP-TEST -Run '../up' } '*run id*' 'Replay accepted an unsafe run id.'
		Assert-Throws { & $script @refuse -Item CP-TEST -Run bridged -Bridge -From munin } '*cannot start from a snapshot*' 'A fresh scope started from a snapshot.'
		Assert-Throws { & $script @refuse -Item CP-TEST -Run mixed -CapitalStage start -AltgardLeg1 } '*cannot be combined*' 'Replay combined unrelated scopes.'
		Assert-Throws { & $script @refuse -Item CP-TEST -Run later -LaterCapital } '*LaterCapital requires*' 'Replay accepted the later capital without a bridge or a leg.'
		Assert-Throws { & $script -Action Restore -Name munin -Docker $fake -SnapshotRoot $snapshots -StopAt '2004:5:0' } '*belong to Replay*' 'Restore accepted a Replay stop.'
		Assert-Throws { & $script -Action Capture -Name replay-capture -Item CP-TEST -Docker $fake -SnapshotRoot $snapshots -NoBuild } '*belong to Replay*' 'Capture accepted a Replay item.'
		Assert-True (-not (Test-Path -LiteralPath $log)) 'A refused Replay touched MySQL.'
		Assert-True ((Get-SnapshotListing) -eq $listing) 'A refused Replay wrote under the snapshot root.'
	}
	finally {
		Remove-Item Function:dotnet
		foreach ($name in $replayPrior.Keys) {
			if ($null -eq $replayPrior[$name]) { Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue }
			else { [Environment]::SetEnvironmentVariable($name, $replayPrior[$name]) }
		}
	}

	# CP-04: the neutral gate replays named scopes and compares each trace with its recorded baseline. The journey is a
	# fake dotnet that writes a small trace; git is a fake that is clean unless the flag file exists.
	$gate = Join-Path $PSScriptRoot 'run-neutral-gate.ps1'
	$gateRoot = Join-Path $root 'gate'
	$gateBaseline = Join-Path $root 'natural-neutral-baseline.json'
	$gateTraces = Join-Path $root 'baseline'
	$gateSecond = Join-Path $root 'second-copy'
	$gateBuildLog = Join-Path $root 'gate-builds.log'
	$gateDirty = Join-Path $root 'dirty.flag'
	$gateSha = '1111111111111111111111111111111111111111'
	$fakeGit = Join-Path $root 'fake-git.ps1'
	Set-Content -LiteralPath $fakeGit -Value @"
if (`$args -contains 'rev-parse') { '$gateSha' }
elseif (`$args -contains 'status' -and (Test-Path -LiteralPath '$gateDirty')) { ' M tests/Aion.Bots/Scenarios/NaturalIshalgenJourney.cs' }
exit 0
"@
	# Each journey takes the next variant: same (the default), changed (one field differs) or crash (the journey fails).
	$global:cp04Variants = @()
	$global:cp04Journeys = 0
	function dotnet {
		if ($args[0] -eq 'build') { Add-Content -LiteralPath $gateBuildLog -Value 'build'; $global:LASTEXITCODE = 0; return }
		$variant = if ($global:cp04Journeys -lt $global:cp04Variants.Count) { $global:cp04Variants[$global:cp04Journeys] } else { 'same' }
		$global:cp04Journeys++
		if ($variant -eq 'crash') { $global:LASTEXITCODE = 1; return }
		$runId = $env:AION_SIM_RUN_ID
		$common = [ordered]@{ ts = (Get-Date).ToUniversalTime().ToString('o'); vt = '00:00:01.000'; run = $runId; bot = 'b01'; account = 'sim-player-41' }
		$records = @(
			($common + [ordered]@{ step = 'ni08-run'; dir = 'action'; packet = 'natural-run-context'; fields = @{ context = @{ Run = $runId; Build = [Guid]::NewGuid().ToString() } } }),
			($common + [ordered]@{ step = 'ni07-q2001-kill-1'; dir = 'action'; packet = 'combat-decision'; fields = [ordered]@{ action = 'cast-target'; hp = $(if ($variant -eq 'changed') { 179 } else { 180 }); inEmergency = $false } }),
			($common + [ordered]@{ step = 'ni07-q2001-kill-1'; dir = 'action'; packet = 'combat-hot-potion'; fields = [ordered]@{ itemId = 162000002 } }))
		Set-Content -LiteralPath (Join-Path $env:AION_NI07_COMBAT_DIR "$runId.trace.jsonl") -Value ($records | ForEach-Object { $_ | ConvertTo-Json -Compress -Depth 5 })
		$global:LASTEXITCODE = 0
	}
	# One gate run: its exit code, its verdict and how many journeys it played.
	function Invoke-Gate([string]$RunId, [hashtable]$Arguments, [string[]]$Variants = @()) {
		$global:cp04Variants = $Variants
		$global:cp04Journeys = 0
		if (Test-Path -LiteralPath $log) { Remove-Item -LiteralPath $log }
		& $gate -Item CP-TEST -Run $RunId -Docker $fake -Git $fakeGit -SnapshotRoot $snapshots -ReplayRoot $gateRoot `
			-BaselineFile $gateBaseline -BaselineRoot $gateTraces -SecondCopyRoot $gateSecond @Arguments | Out-Null
		$code = $LASTEXITCODE
		[pscustomobject]@{
			code = $code; journeys = $global:cp04Journeys; folder = Join-Path (Join-Path $gateRoot 'CP-TEST') $RunId
			verdict = Get-Content -Raw -LiteralPath (Join-Path (Join-Path (Join-Path $gateRoot 'CP-TEST') $RunId) 'verdict.json') | ConvertFrom-Json
		}
	}
	function Get-GateTraces([string]$ReplayRun) { @(Get-ChildItem -LiteralPath (Join-Path (Join-Path $gateRoot 'CP-TEST') $ReplayRun) -Filter '*.trace.jsonl' -File) }
	# Scopes l1, c, hm and ax start from snapshots; give the gate's fixture root the two it replays.
	foreach ($mock in @('altgard', 'altgard-rc-l3')) {
		$mockDirectory = Join-Path $snapshots $mock
		New-Item -ItemType Directory -Path $mockDirectory | Out-Null
		Copy-Item -LiteralPath (Join-Path $munin 'dump.sql.gz') -Destination $mockDirectory
		[ordered]@{ schemaVersion = 1; name = $mock; characterId = 4242; elapsedMillis = 1000; dumpSha256 = $hash } |
			ConvertTo-Json | Set-Content -LiteralPath (Join-Path $mockDirectory 'snapshot.json')
	}
	$gatePriorHelp = [Environment]::GetEnvironmentVariable('NA_HELP_ITEMS')
	$env:NA_HELP_ITEMS = '0'
	try {
		# Without a baseline nothing can be compared, and a class scope is refused by its name.
		Assert-Throws { Invoke-Gate 'none1' @{ Set = 'mage'; NoBuild = $true } } "*'mage' has no baseline row*" 'A class scope with no baseline row was played.'
		Assert-Throws { Invoke-Gate 'none2' @{ Set = 'all'; NoBuild = $true } } '*Set all is empty*' 'An empty set all was played.'
		Assert-Throws { Invoke-Gate 'none3' @{ Set = 'p+zz'; NoBuild = $true } } "*Unknown scope 'zz'*" 'An unknown scope was accepted.'

		# -Record with uncommitted runtime changes is refused before anything is played.
		Set-Content -LiteralPath $gateDirty -Value 'dirty'
		$refused = Invoke-Gate 'rec0' @{ Set = 'p+c'; Record = $true; NoBuild = $true }
		Assert-True ($refused.code -eq 2 -and $refused.verdict.verdict -eq 'refused-dirty-record' -and $refused.journeys -eq 0) 'A record run from a changed tree was not refused.'
		Assert-True (-not (Test-Path -LiteralPath $gateBaseline) -and -not (Test-Path -LiteralPath $log)) 'A refused record run wrote a baseline or touched MySQL.'
		Remove-Item -LiteralPath $gateDirty

		# -Record: each scope twice, one build, the traces kept in both places, and the baseline file written once.
		$recorded = Invoke-Gate 'rec1' @{ Set = 'p+c+l1'; Record = $true }
		Assert-True ($recorded.code -eq 0 -and $recorded.verdict.verdict -eq 'pass' -and $recorded.verdict.mode -eq 'record' -and $recorded.journeys -eq 6) 'Three scopes were not each recorded twice.'
		Assert-True (@(Get-Content -LiteralPath $gateBuildLog).Count -eq 1) 'The gate did not build exactly once.'
		$baselineText = Get-Content -Raw -LiteralPath $gateBaseline
		$document = $baselineText | ConvertFrom-Json
		Assert-True ((@($document.scopes.scope) -join ',') -eq 'p,l1,c') 'The baseline rows are not in the order of the scope table.'
		$rowP = $document.scopes[0]
		$rowC = $document.scopes[2]
		Assert-True ($rowP.records -eq 2 -and $rowP.sha256 -match '^[0-9a-f]{64}$' -and $rowP.commit -eq $gateSha -and $rowP.line -eq 'priest-cleric') 'A baseline row lost its count, its hash or its commit.'
		Assert-True ($rowP.replay.CapitalStage -eq 'start' -and $null -eq $rowP.snapshot -and $rowC.snapshot -eq 'altgard-rc-l3' -and $rowC.replay.Leg -eq 'l4') 'A baseline row does not say what it replays.'
		Assert-True ($document.scopes[1].replay.LaterCapital -eq $true -and $document.scopes[1].snapshot -eq 'altgard') 'Scope l1 is not the later-capital leg from altgard.'
		Assert-True ($rowP.counts.lifePotions -eq 1 -and $rowP.counts.records -eq 3 -and $rowP.counts.deaths -eq 0) 'A baseline row lost its counts.'
		Assert-True ($rowP.environment.AION_SIM_SEED -eq '1' -and $null -eq $rowP.environment.NA_HELP_ITEMS -and
			($rowP.environment.PSObject.Properties.Name -join ',') -eq 'AION_SIM_SEED,NA_HELP_ITEMS,AION_BOT_DASHBOARD_PORT,AION_SIM_PROCESS_KEY') 'A baseline row lost its pinned environment.'
		Assert-True ($rowP.sha256 -eq $rowC.sha256) 'Two scopes with the same records hashed differently.'
		foreach ($copy in @($gateTraces, $gateSecond)) {
			Assert-True ((@(Get-ChildItem -LiteralPath (Join-Path $copy $gateSha) -Name | Sort-Object) -join ',') -eq 'c.trace.jsonl,l1.trace.jsonl,p.trace.jsonl') "The recorded traces are not all in $copy."
		}
		Assert-True (@(Get-GateTraces 'rec1-p-pass1').Count -eq 0 -and @(Get-GateTraces 'rec1-p-pass2').Count -eq 0) 'A recorded pass left its trace in the evidence folder.'
		Assert-True ($env:NA_HELP_ITEMS -eq '0') 'The gate did not put the parent environment back.'

		# -Record of two different passes writes nothing, keeps both traces and names the first difference.
		$unstable = Invoke-Gate 'rec2' @{ Set = 'm'; Record = $true; NoBuild = $true } @('same', 'changed')
		Assert-True ($unstable.code -eq 1 -and $unstable.verdict.verdict -eq 'fail' -and $unstable.journeys -eq 2) 'Two different passes were recorded.'
		Assert-True ($unstable.verdict.scopes[0].reason -eq 'the two passes differ' -and $unstable.verdict.scopes[0].firstDifference -like '*fields: fields.hp*') 'An unrepeatable scope does not say where its passes part.'
		Assert-True ((Get-Content -Raw -LiteralPath $gateBaseline) -eq $baselineText) 'A failed record run changed the baseline file.'
		Assert-True (@(Get-GateTraces 'rec2-m-pass1').Count -eq 1 -and @(Get-GateTraces 'rec2-m-pass2').Count -eq 1) 'An unrepeatable scope lost its two traces.'
		Assert-True (-not (Test-Path -LiteralPath (Join-Path (Join-Path $gateTraces $gateSha) 'm.trace.jsonl'))) 'An unrepeatable scope was kept as a baseline trace.'
		# A scope in a set that fails is not written either, even if its own passes repeat.
		$mixed = Invoke-Gate 'rec3' @{ Set = 'b+m'; Record = $true; NoBuild = $true } @('same', 'same', 'same', 'changed')
		Assert-True ($mixed.code -eq 1 -and $mixed.verdict.scopes[0].verdict -eq 'pass' -and (Get-Content -Raw -LiteralPath $gateBaseline) -eq $baselineText) 'A record run that failed wrote part of its set.'
		# The ignore list is recorded with the row, and a Priest or Cleric row is not replaced without -ReRecord.
		$ignored = Invoke-Gate 'rec4' @{ Set = 'm'; Record = $true; NoBuild = $true; Ignore = @('combat-decision:fields.hp') } @('same', 'changed')
		Assert-True ($ignored.code -eq 0 -and $ignored.verdict.verdict -eq 'pass') 'An ignored field kept two passes apart.'
		$document = Get-Content -Raw -LiteralPath $gateBaseline | ConvertFrom-Json
		Assert-True ((@($document.scopes.scope) -join ',') -eq 'p,m,l1,c' -and (@($document.scopes[1].ignore) -join ',') -eq 'combat-decision:fields.hp' -and @($document.scopes[0].ignore).Count -eq 0) 'A recorded ignore list was lost or spread to another row.'
		Assert-True ($document.scopes[0].sha256 -eq $rowP.sha256 -and $document.scopes[3].sha256 -eq $rowC.sha256) 'Recording one scope changed another row.'
		Assert-Throws { Invoke-Gate 'rec5' @{ Set = 'p'; Record = $true; NoBuild = $true } } "*'p' already has a baseline row*" 'A Priest scope was recorded a second time without the operator.'
		$again = Invoke-Gate 'rec6' @{ Set = 'p'; Record = $true; ReRecord = $true; NoBuild = $true }
		Assert-True ($again.code -eq 0 -and $again.journeys -eq 2) 'The operator could not record a scope again.'
		$baselineText = Get-Content -Raw -LiteralPath $gateBaseline

		# The gate: pass. The candidate trace is deleted, its evidence stays, and a changed tree is allowed and recorded.
		Set-Content -LiteralPath $gateDirty -Value 'dirty'
		$pass = Invoke-Gate 'cmp1' @{ Set = 'p+c'; NoBuild = $true }
		Remove-Item -LiteralPath $gateDirty
		Assert-True ($pass.code -eq 0 -and $pass.verdict.verdict -eq 'pass' -and $pass.verdict.mode -eq 'compare' -and $pass.journeys -eq 2) 'An unchanged journey did not pass the gate.'
		Assert-True (@($pass.verdict.uncommitted).Count -eq 1 -and $pass.verdict.commit -eq $gateSha) 'The verdict does not say which code it compared.'
		Assert-True ($pass.verdict.scopes[0].sha256 -eq $rowP.sha256 -and $pass.verdict.scopes[0].baselineSha256 -eq $rowP.sha256 -and $pass.verdict.scopes[1].scope -eq 'c') 'The verdict lost a scope or its hashes.'
		Assert-True (@(Get-GateTraces 'cmp1-p').Count -eq 0 -and (Test-Path -LiteralPath (Join-Path (Join-Path (Join-Path $gateRoot 'CP-TEST') 'cmp1-p') 'replay.json'))) 'A passing candidate trace was kept, or its evidence was lost.'
		Assert-True ((Get-Content -Raw -LiteralPath $gateBaseline) -eq $baselineText) 'A comparison changed the baseline file.'
		# The row's own ignore list is used, so scope m passes with the field it was recorded without.
		$ignoredPass = Invoke-Gate 'cmp2' @{ Set = 'm'; NoBuild = $true } @('changed')
		Assert-True ($ignoredPass.code -eq 0 -and $ignoredPass.verdict.verdict -eq 'pass') 'A comparison did not use the ignore list of its row.'
		Assert-Throws { Invoke-Gate 'cmp3' @{ Set = 'p'; NoBuild = $true; Ignore = @('fields.hp') } } '*Ignore is recorded*' 'A comparison took an ignore list of its own.'

		# The gate: fail. The candidate is kept and the first differing record is in the verdict. The recorded trace was
		# lost from run/ and comes back from the second copy.
		Remove-Item -LiteralPath (Join-Path (Join-Path $gateTraces $gateSha) 'c.trace.jsonl')
		$fail = Invoke-Gate 'cmp4' @{ Set = 'p+c'; NoBuild = $true } @('same', 'changed')
		Assert-True ($fail.code -eq 1 -and $fail.verdict.verdict -eq 'fail' -and $fail.verdict.scopes[0].verdict -eq 'pass' -and $fail.verdict.scopes[1].verdict -eq 'fail') 'A changed journey passed the gate.'
		Assert-True ($fail.verdict.scopes[1].firstDifference -like '*different: first at record 0*' -and $fail.verdict.scopes[1].firstDifference -like '*fields: fields.hp*') 'A failed scope does not name its first differing record.'
		Assert-True (@(Get-GateTraces 'cmp4-c').Count -eq 1 -and $fail.verdict.scopes[1].candidateTrace -eq @(Get-GateTraces 'cmp4-c')[0].FullName) 'A failing candidate trace was not kept.'
		Assert-True (Test-Path -LiteralPath (Join-Path (Join-Path $gateTraces $gateSha) 'c.trace.jsonl')) 'A lost baseline trace was not restored from the second copy.'
		# A journey that fails is a failed scope, not a crash of the gate.
		$crashed = Invoke-Gate 'cmp5' @{ Set = 'p'; NoBuild = $true } @('crash')
		Assert-True ($crashed.code -eq 1 -and $crashed.verdict.scopes[0].reason -like 'replay failed:*Natural journey failed*') 'A failed journey was not reported as a failed scope.'

		# Set all is every Priest and Cleric scope that was kept, and says which were not.
		$all = Invoke-Gate 'cmp6' @{ Set = 'all'; NoBuild = $true } @('same', 'changed')
		Assert-True ($all.code -eq 0 -and $all.journeys -eq 4 -and (@($all.verdict.scopes.scope) -join ',') -eq 'p,m,l1,c') 'Set all did not play the kept scopes in table order.'
		Assert-True ((@($all.verdict.leftOutOfAll) -join ',') -eq 'b,hm,ax') 'Set all does not say which scopes it left out.'
		Assert-Throws { Invoke-Gate 'cmp7' @{ Set = 'all+scout'; NoBuild = $true } } "*'scout' has no baseline row*" 'A class scope with no baseline row was played beside set all.'
		Assert-Throws { Invoke-Gate 'cmp1' @{ Set = 'p'; NoBuild = $true } } '*already exists*' 'Two gate runs shared an evidence folder.'
		Assert-True ((Get-ChildItem -LiteralPath $snapshots -Directory -Name | Where-Object { $_ -like '_*' }) -eq $null) 'The gate wrote a capture or verify folder under the snapshot root.'
	}
	finally {
		Remove-Item Function:dotnet
		Remove-Variable -Name cp04Variants, cp04Journeys -Scope Global -ErrorAction SilentlyContinue
		if ($null -eq $gatePriorHelp) { Remove-Item -LiteralPath Env:NA_HELP_ITEMS -ErrorAction SilentlyContinue }
		else { $env:NA_HELP_ITEMS = $gatePriorHelp }
	}

	# CP-28: the class line. Capture and Replay take -Class and set CP_CLASS; a snapshot of another line records it and
	# Restore prints it back; the accepted line is recorded and printed nowhere; no line sets NA_HELP_ITEMS. The journey
	# is a fake dotnet that writes the receipts a capture reads, and git is a fake with a clean tree.
	$lineEnvPath = Join-Path $root 'line-env.jsonl'
	$lineSha = '2222222222222222222222222222222222222222'
	function git { if ($args -contains 'rev-parse') { $lineSha } }
	function dotnet {
		if ($args[0] -eq 'build') { $global:LASTEXITCODE = 0; return }
		$seen = [ordered]@{}
		foreach ($name in @('CP_CLASS', 'NA_HELP_ITEMS', 'PC_CAPITAL', 'NA_ASCENSION', 'AF_ALTGARD', 'NI08_RESUME_CHARACTER')) {
			$seen[$name] = [Environment]::GetEnvironmentVariable($name)
		}
		Add-Content -LiteralPath $lineEnvPath -Value ($seen | ConvertTo-Json -Compress)
		$out = $env:AION_NI07_COMBAT_DIR
		@{ Next = @{ Outcome = 'complete' }; CharacterId = 4242 } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $out 'completion.json')
		@{ CharacterId = 4242; ElapsedMillis = 7000 } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $out 'completion-clock.json')
		if ($env:PC_CAPITAL) {
			@{ verified = $true; Stage = $env:PC_CAPITAL; CharacterId = 4242; ElapsedMillis = 5000 } | ConvertTo-Json |
				Set-Content -LiteralPath (Join-Path $out 'capital-stage-completion.json')
		}
		if ($env:AF_ALTGARD) {
			$legId = if ($env:AF_ALTGARD -eq '1') { 'l1' } else { $env:AF_ALTGARD }
			@{ verified = $true; CharacterId = 4242; ElapsedMillis = 1000 } | ConvertTo-Json |
				Set-Content -LiteralPath (Join-Path $out "altgard-$legId-completion.json")
		}
		$global:LASTEXITCODE = 0
	}
	# The child environments of the journeys played since the last call.
	function Get-LineJourneys {
		$seen = @(if (Test-Path -LiteralPath $lineEnvPath) { Get-Content -LiteralPath $lineEnvPath | ForEach-Object { $_ | ConvertFrom-Json } })
		if (Test-Path -LiteralPath $lineEnvPath) { Remove-Item -LiteralPath $lineEnvPath }
		, $seen
	}
	function Get-SnapshotMetadata([string]$SnapshotName) { Get-Content -Raw -LiteralPath (Join-Path (Join-Path $snapshots $SnapshotName) 'snapshot.json') | ConvertFrom-Json }
	function Restore-Line([string]$SnapshotName) { & $script -Action Restore -Name $SnapshotName -Docker $fake -SnapshotRoot $snapshots | ConvertFrom-Json }
	$capture = @{ Action = 'Capture'; Docker = $fake; SnapshotRoot = $snapshots; NoBuild = $true }
	$replay = @{ Action = 'Replay'; Item = 'CP-28'; Docker = $fake; SnapshotRoot = $snapshots; ReplayRoot = $replays; NoBuild = $true }
	$linePriorHelp = [Environment]::GetEnvironmentVariable('NA_HELP_ITEMS')
	$linePriorClass = [Environment]::GetEnvironmentVariable('CP_CLASS')
	$env:NA_HELP_ITEMS = '0'
	$env:CP_CLASS = 'scout'
	try {
		# A historical restore gains no selector: munin prints its three variables and nothing else.
		$historical = Restore-Line 'munin'
		Assert-True (($historical.environment.PSObject.Properties.Name -join ',') -eq 'AION_SIM_NI08_DATABASE,NI08_RESUME_CHARACTER,AION_SIM_NI08_ELAPSED_MS') 'A historical restore gained a class or help-item selector.'

		# The accepted line, named or not, is captured as before: no CP_CLASS in the journey, no classLine in the snapshot.
		& $script @capture -Name line-default | Out-Null
		& $script @capture -Name line-named -Class priest-cleric | Out-Null
		$played = Get-LineJourneys
		Assert-True ($played.Count -eq 2 -and $null -eq $played[0].CP_CLASS -and $null -eq $played[1].CP_CLASS) 'A capture of the accepted line set CP_CLASS.'
		foreach ($accepted in @('line-default', 'line-named')) {
			$metadata = Get-SnapshotMetadata $accepted
			Assert-True (($metadata.PSObject.Properties.Name -join ',') -eq 'schemaVersion,name,source,run,seed,gitSha,capturedUtc,characterId,elapsedMillis,dumpSha256') "The accepted line's snapshot $accepted changed what it records."
			Assert-True ($metadata.source -eq 'natural-ishalgen-journey' -and $metadata.gitSha -eq $lineSha -and $metadata.elapsedMillis -eq 7000) "The accepted line's snapshot $accepted lost its values."
			Assert-True (((Restore-Line $accepted).environment.PSObject.Properties.Name -join ',') -eq 'AION_SIM_NI08_DATABASE,NI08_RESUME_CHARACTER,AION_SIM_NI08_ELAPSED_MS') "The accepted line's snapshot $accepted restores with a selector."
		}

		# A recorded line round-trips: Capture sets CP_CLASS and records the line, Restore prints it, a run from it plays it.
		& $script @capture -Name mage-munin -Class mage | Out-Null
		$played = Get-LineJourneys
		Assert-True ($played.Count -eq 1 -and $played[0].CP_CLASS -eq 'mage' -and $null -eq $played[0].NI08_RESUME_CHARACTER) 'A capture of another line did not set CP_CLASS.'
		$mage = Get-SnapshotMetadata 'mage-munin'
		Assert-True ($mage.classLine -eq 'mage' -and $mage.PSObject.Properties.Name[-1] -eq 'classLine' -and $mage.source -eq 'natural-ishalgen-journey') 'A snapshot of another line did not record it.'
		$mageRestored = Restore-Line 'mage-munin'
		Assert-True (($mageRestored.environment.PSObject.Properties.Name -join ',') -eq 'AION_SIM_NI08_DATABASE,NI08_RESUME_CHARACTER,AION_SIM_NI08_ELAPSED_MS,CP_CLASS' -and
			$mageRestored.environment.CP_CLASS -eq 'mage') 'Restore did not print the recorded line.'
		& $script @replay -Run from-mage -From mage-munin | Out-Null
		& $script @replay -Run from-mage-named -From mage-munin -Class mage | Out-Null
		$played = Get-LineJourneys
		Assert-True ($played.Count -eq 2 -and $played[0].CP_CLASS -eq 'mage' -and $played[1].CP_CLASS -eq 'mage' -and $played[0].NI08_RESUME_CHARACTER -eq '4242') 'A run from a recorded line did not play that line.'
		$fromMage = Get-Content -Raw -LiteralPath (Join-Path (Join-Path (Join-Path $replays 'CP-28') 'from-mage') 'replay.json') | ConvertFrom-Json
		Assert-True ($fromMage.environment.CP_CLASS -eq 'mage' -and $fromMage.passed) 'A Replay receipt lost its class line.'
		# A fresh Replay of another line, and of the accepted one.
		& $script @replay -Run fresh-warrior -Class warrior | Out-Null
		& $script @replay -Run fresh-accepted -Class priest-cleric | Out-Null
		$played = Get-LineJourneys
		Assert-True ($played[0].CP_CLASS -eq 'warrior' -and $null -eq $played[1].CP_CLASS) 'A fresh Replay did not set the line it was given, or set the accepted one.'
		$freshAccepted = Get-Content -Raw -LiteralPath (Join-Path (Join-Path (Join-Path $replays 'CP-28') 'fresh-accepted') 'replay.json') | ConvertFrom-Json
		Assert-True (@($freshAccepted.environment.PSObject.Properties).Count -eq 0) 'A Replay of the accepted line changed its receipt.'
		# A leg captured from a recorded line carries the line into the new snapshot.
		& $script @capture -Name mage-leg -AltgardLeg1 -From mage-munin | Out-Null
		$played = Get-LineJourneys
		Assert-True ($played[0].CP_CLASS -eq 'mage' -and $played[0].AF_ALTGARD -eq '1') 'A leg from a recorded line did not play that line.'
		Assert-True ((Get-SnapshotMetadata 'mage-leg').classLine -eq 'mage' -and (Restore-Line 'mage-leg').environment.CP_CLASS -eq 'mage') 'A leg snapshot lost the line of its base.'
		& $script @capture -Name default-leg -AltgardLeg1 -From line-default | Out-Null
		Assert-True ((Get-SnapshotMetadata 'default-leg').PSObject.Properties.Name -notcontains 'classLine' -and $null -eq (Get-LineJourneys)[0].CP_CLASS) "A leg of the accepted line recorded or set a class line."

		# The capital snapshots. The accepted line's restores on the first pass, as it always did; a Chanter's has no
		# capital leg and restores on the start stage, where Verify re-checks the endpoint and stops.
		& $script @capture -Name capital-accepted -CapitalStage start | Out-Null
		& $script @capture -Name chanter-start -CapitalStage start -Class priest-chanter | Out-Null
		$played = Get-LineJourneys
		Assert-True ($null -eq $played[0].CP_CLASS -and $played[1].CP_CLASS -eq 'priest-chanter' -and $played[1].PC_CAPITAL -eq 'start' -and $played[1].NA_ASCENSION -eq '1') 'A capital capture lost its line or its stage.'
		$acceptedCapital = Restore-Line 'capital-accepted'
		Assert-True (($acceptedCapital.environment.PSObject.Properties.Name -join ',') -eq 'AION_SIM_NI08_DATABASE,NI08_RESUME_CHARACTER,AION_SIM_NI08_ELAPSED_MS,NA_ASCENSION,PC_CAPITAL' -and
			$acceptedCapital.environment.PC_CAPITAL -eq 'first') "The accepted line's capital snapshot restores differently."
		Assert-True ((Get-SnapshotMetadata 'capital-accepted').PSObject.Properties.Name -notcontains 'classLine') "The accepted line's capital snapshot recorded a class line."
		$chanter = Get-SnapshotMetadata 'chanter-start'
		Assert-True ($chanter.classLine -eq 'priest-chanter' -and $chanter.source -eq 'natural-capital-start') 'A Chanter capital snapshot did not record its line.'
		$chanterRestored = Restore-Line 'chanter-start'
		Assert-True ($chanterRestored.environment.PC_CAPITAL -eq 'start' -and $chanterRestored.environment.NA_ASCENSION -eq '1' -and
			$chanterRestored.environment.CP_CLASS -eq 'priest-chanter') 'A Chanter capital snapshot did not restore on the start stage with its line.'
		& $script -Action Verify -Name chanter-start -Run verify-chanter -Docker $fake -SnapshotRoot $snapshots | Out-Null
		$played = Get-LineJourneys
		Assert-True ($played.Count -eq 1 -and $played[0].PC_CAPITAL -eq 'start' -and $played[0].CP_CLASS -eq 'priest-chanter' -and $played[0].NI08_RESUME_CHARACTER -eq '4242') 'Verify did not take its environment from Restore.'
		Assert-True (Test-Path -LiteralPath (Join-Path (Join-Path (Join-Path $snapshots '_verify') 'verify-chanter') 'capital-stage-completion.json')) 'Verify kept no evidence under the snapshot root.'
		& $script @capture -Name capital-accepted-first -CapitalStage first -From capital-accepted | Out-Null
		$played = Get-LineJourneys
		Assert-True ($played[0].PC_CAPITAL -eq 'first' -and $null -eq $played[0].CP_CLASS -and (Get-SnapshotMetadata 'capital-accepted-first').from -eq 'capital-accepted') "The accepted line's first capital pass changed."

		# No Capture, Replay, Restore or Verify of any line set NA_HELP_ITEMS, and the parent's values came back.
		foreach ($restorable in @('munin', 'line-default', 'mage-munin', 'mage-leg', 'capital-accepted', 'chanter-start')) {
			Assert-True ((Restore-Line $restorable).environment.PSObject.Properties.Name -notcontains 'NA_HELP_ITEMS') "Restore of $restorable printed NA_HELP_ITEMS."
		}
		foreach ($receiptFile in @(Get-ChildItem -LiteralPath (Join-Path $replays 'CP-28') -Recurse -Filter 'replay.json')) {
			$receiptEnvironment = (Get-Content -Raw -LiteralPath $receiptFile.FullName | ConvertFrom-Json).environment
			Assert-True (@($receiptEnvironment.PSObject.Properties | ForEach-Object { $_.Name }) -notcontains 'NA_HELP_ITEMS') 'A Replay set NA_HELP_ITEMS.'
		}
		Assert-True ($env:NA_HELP_ITEMS -eq '0' -and $env:CP_CLASS -eq 'scout') 'A run did not put the parent environment back.'

		# What is refused, before MySQL is touched: an unknown line, a line on Restore, another line than the snapshot's.
		if (Test-Path -LiteralPath $log) { Remove-Item -LiteralPath $log }
		$journeysBefore = (Get-LineJourneys).Count
		Assert-Throws { & $script @capture -Name unknown-line -Class paladin } "*Unknown class line 'paladin'*" 'Capture accepted an unknown class line.'
		Assert-Throws { & $script @replay -Run unknown-line -Class Mage } "*Unknown class line 'Mage'*" 'Replay accepted a line id in another case.'
		Assert-Throws { & $script -Action Restore -Name mage-munin -Class mage -Docker $fake -SnapshotRoot $snapshots } '*belongs to Capture and Replay*' 'Restore accepted a class line.'
		Assert-Throws { & $script -Action Verify -Name mage-munin -Class mage -Docker $fake -SnapshotRoot $snapshots } '*belongs to Capture and Replay*' 'Verify accepted a class line.'
		Assert-True (-not (Test-Path -LiteralPath $log) -and -not (Test-Path -LiteralPath (Join-Path $snapshots 'unknown-line'))) 'A refused class line touched MySQL or wrote a snapshot.'
		# These restore the base first, then refuse and drop the copy without playing.
		Assert-Throws { & $script @replay -Run wrong-line -From mage-munin -Class warrior } '*holds class line mage; -Class warrior cannot play it*' 'A run changed the line of a snapshot.'
		Assert-Throws { & $script @replay -Run wrong-line-2 -From munin -Class mage } '*holds class line priest-cleric; -Class mage cannot play it*' 'A run gave a line to a snapshot of the accepted line.'
		Assert-Throws { & $script @capture -Name wrong-leg -AltgardLeg1 -From mage-munin -Class scout } '*holds class line mage*' 'A leg capture changed the line of its base.'
		Assert-Throws { & $script @capture -Name chanter-first -CapitalStage first -From chanter-start } '*priest-chanter has no capital leg*' 'A Chanter first capital pass was captured.'
		Assert-Throws { & $script @replay -Run chanter-first -CapitalStage first -From chanter-start } '*priest-chanter has no capital leg*' 'A Chanter first capital pass was replayed.'
		$refusedCalls = @(Get-Content -LiteralPath $log)
		Assert-True ($refusedCalls[-1] -like '*DROP DATABASE IF EXISTS*' -and @($refusedCalls | Where-Object { $_ -like '*DROP DATABASE*' }).Count -eq 5) 'A refused run kept its restored schema.'
		Assert-True ((Get-LineJourneys).Count -eq 0 -and $journeysBefore -eq 0) 'A refused run was played.'
		foreach ($absent in @('wrong-leg', 'chanter-first')) {
			Assert-True (-not (Test-Path -LiteralPath (Join-Path $snapshots $absent))) "A refused capture wrote the snapshot $absent."
		}
		# A snapshot that records a line this plan does not hold is not restored.
		$foreign = Join-Path $snapshots 'foreign-line'
		Copy-Item -LiteralPath (Join-Path $snapshots 'mage-munin') -Destination $foreign -Recurse
		$foreignMetadata = Get-SnapshotMetadata 'foreign-line'
		$foreignMetadata.classLine = 'paladin'
		$foreignMetadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $foreign 'snapshot.json')
		Remove-Item -LiteralPath $log
		Assert-Throws { Restore-Line 'foreign-line' } "*records an unknown class line 'paladin'*" 'A snapshot of an unknown line was restored.'
		Assert-True (-not (Test-Path -LiteralPath $log)) 'A snapshot of an unknown line touched MySQL.'
	}
	finally {
		Remove-Item Function:dotnet
		Remove-Item Function:git
		if ($null -eq $linePriorHelp) { Remove-Item -LiteralPath Env:NA_HELP_ITEMS -ErrorAction SilentlyContinue } else { $env:NA_HELP_ITEMS = $linePriorHelp }
		if ($null -eq $linePriorClass) { Remove-Item -LiteralPath Env:CP_CLASS -ErrorAction SilentlyContinue } else { $env:CP_CLASS = $linePriorClass }
	}

	# An edited dump is refused before anything is created.
	if (Test-Path -LiteralPath $log) { Remove-Item -LiteralPath $log }
	[IO.File]::WriteAllBytes((Join-Path $munin 'dump.sql.gz'), [byte[]](9, 9, 9))
	Assert-Throws { & $script -Action Restore -Name munin -Docker $fake -SnapshotRoot $snapshots } '*hash changed*' 'Edited snapshot was restored.'
	Assert-True (-not (Test-Path -LiteralPath $log)) 'A refused restore must not touch MySQL.'

	# Capture never overwrites an existing snapshot.
	Assert-Throws { & $script -Action Capture -Name munin -Docker $fake -SnapshotRoot $snapshots -NoBuild } '*already exists*' 'Capture overwrote a snapshot.'

	# Drop only drops owned schemas.
	Assert-Throws { & $script -Action Drop -Database 'aion_gs' -Docker $fake } '*does not own*' 'Drop accepted the game schema.'
	Assert-Throws { & $script -Action Drop -Database 'aion_gs_sim_other' -Docker $fake } '*does not own*' 'Drop accepted a schema it does not own.'
	& $script -Action Drop -Database 'aion_gs_sim_ni08_abc' -Docker $fake
	Assert-True (@(Get-Content -LiteralPath $log)[-1].Contains('DROP DATABASE IF EXISTS `aion_gs_sim_ni08_abc`;')) 'Drop did not drop the owned schema.'

	# Snapshot names are plain.
	Assert-Throws { & $script -Action Restore -Name '../aion' -Docker $fake -SnapshotRoot $snapshots } '*Name*' 'An unsafe snapshot name was accepted.'
	Write-Host 'sim-snapshot contract passed.'
}
finally {
	$resolvedRoot = [IO.Path]::GetFullPath($root)
	if ((Split-Path -Parent $resolvedRoot) -ne [IO.Path]::GetTempPath().TrimEnd([IO.Path]::DirectorySeparatorChar) -or
		(Split-Path -Leaf $resolvedRoot) -notmatch '^aion-sim-snapshot-[a-f0-9]{32}$') { throw 'Refusing unsafe snapshot-test cleanup.' }
	Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
}
