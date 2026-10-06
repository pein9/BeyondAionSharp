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
