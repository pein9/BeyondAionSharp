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
		'RC_CAPITAL', 'NI07_STOP_AFTER_Q2004', 'NI07_STOP_AFTER_Q2005', 'NA_HELP_ITEMS', 'AION_BOT_DASHBOARD_PORT', 'AION_SIM_PROCESS_KEY')
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
	foreach ($name in @('NA_HELP_ITEMS', 'AION_BOT_DASHBOARD_PORT', 'AION_SIM_PROCESS_KEY', 'CP03_FAKE_JOURNEY_EXIT')) {
		$replayPrior[$name] = [Environment]::GetEnvironmentVariable($name)
	}
	$env:NA_HELP_ITEMS = '0'
	$env:AION_BOT_DASHBOARD_PORT = '0'
	$env:AION_SIM_PROCESS_KEY = 'shard-09'
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
