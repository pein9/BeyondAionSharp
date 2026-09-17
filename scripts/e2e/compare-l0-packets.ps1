[CmdletBinding()]
param(
	[Parameter(Mandatory)]
	[string]$SimPacketPath,

	[Parameter(Mandatory)]
	[string]$LiveTraceDirectory,

	[Parameter(Mandatory)]
	[string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$simPath = [IO.Path]::GetFullPath($SimPacketPath)
$livePath = [IO.Path]::GetFullPath($LiveTraceDirectory)
$output = [IO.Path]::GetFullPath($OutputPath)
if (-not (Test-Path -LiteralPath $simPath -PathType Leaf)) { throw "SIM packet artifact is missing: $simPath" }
if (-not (Test-Path -LiteralPath $livePath -PathType Container)) { throw "LIVE trace directory is missing: $livePath" }

# These are outside the phase gate: login/auth setup, the explicitly excluded pong, LIVE-only chat setup,
# broadcasts whose decoded object id is unavailable, and unsolicited known-list refreshes rather than CM replies.
$excluded = @(
	'SM_KEY', 'SM_VERSION_CHECK', 'SM_L2AUTH_LOGIN_CHECK', 'SM_ACCOUNT_PROPERTIES',
	'SM_PONG', 'SM_CHAT_INIT', 'SM_EMOTION', 'SM_NEARBY_QUESTS'
)

$simRows = [Collections.Generic.List[object]]::new()
$simBots = @(Get-Content -Raw -LiteralPath $simPath | ConvertFrom-Json)
foreach ($bot in $simBots) {
	foreach ($packet in $bot.packets) {
		if ($packet.Packet -in $excluded) { continue }
		if ($null -ne $packet.ObjectId -and [int]$packet.ObjectId -ne [int]$bot.characterId) { continue }
		$simRows.Add([pscustomobject]@{ Bot = [string]$bot.bot; Packet = [string]$packet.Packet })
	}
}

$liveRows = [Collections.Generic.List[object]]::new()
$liveTraces = @(Get-ChildItem -LiteralPath $livePath -File -Filter '*.trace.jsonl' | Sort-Object Name)
if ($liveTraces.Count -eq 0) { throw "LIVE trace directory contains no bot traces: $livePath" }
foreach ($trace in $liveTraces) {
	$records = @(Get-Content -LiteralPath $trace.FullName | ForEach-Object { $_ | ConvertFrom-Json })
	$created = @($records | Where-Object { $_.dir -eq '<' -and $_.packet -eq 'SM_CREATE_CHARACTER' })
	if ($created.Count -ne 1) { throw "Expected one SM_CREATE_CHARACTER in $($trace.FullName), found $($created.Count)." }
	$characterId = [int]$created[0].fields.character.objectId
	foreach ($record in $records) {
		if ($record.dir -ne '<' -or $record.packet -in $excluded) { continue }
		$objectId = $record.fields.PSObject.Properties['objectId']
		if ($null -ne $objectId -and [int]$objectId.Value -ne $characterId) { continue }
		$liveRows.Add([pscustomobject]@{ Bot = [string]$record.bot; Packet = [string]$record.packet })
	}
}

function Get-Multiset([Collections.Generic.List[object]]$Rows) {
	$counts = [ordered]@{}
	foreach ($group in $Rows | Group-Object Bot, Packet | Sort-Object Name) {
		$counts[$group.Name] = $group.Count
	}
	return $counts
}

$simCounts = Get-Multiset $simRows
$liveCounts = Get-Multiset $liveRows
$allKeys = @($simCounts.Keys + $liveCounts.Keys) | Sort-Object -Unique
$differences = @(
	foreach ($key in $allKeys) {
		$simCount = if ($simCounts.Contains($key)) { [int]$simCounts[$key] } else { 0 }
		$liveCount = if ($liveCounts.Contains($key)) { [int]$liveCounts[$key] } else { 0 }
		if ($simCount -ne $liveCount) {
			[ordered]@{ key = $key; sim = $simCount; live = $liveCount }
		}
	}
)

$result = [ordered]@{
	status = if ($differences.Count -eq 0) { 'passed' } else { 'failed' }
	excluded = $excluded
	sim = $simCounts
	live = $liveCounts
	differences = $differences
}
$outputDirectory = Split-Path $output -Parent
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
	New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}
[IO.File]::WriteAllText(
	$output,
	($result | ConvertTo-Json -Depth 6) + "`n",
	[Text.UTF8Encoding]::new($false))

if ($differences.Count -ne 0) {
	$summary = $differences | ForEach-Object { "$($_.key): SIM=$($_.sim), LIVE=$($_.live)" }
	throw "L0 SIM/LIVE server-packet multiset differs: $($summary -join '; '). See $output"
}

Write-Host "L0 SIM/LIVE server-packet multiset matched. Artifact: $output"
