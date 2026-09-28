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

	# An edited dump is refused before anything is created.
	Remove-Item -LiteralPath $log
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
	Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
}
