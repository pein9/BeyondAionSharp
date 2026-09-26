param([Parameter(Mandatory)][string]$Directory)

$ErrorActionPreference = 'Stop'
$before = (Get-Content -Raw -LiteralPath (Join-Path $Directory 'before-restart/resume-receipt.json') | ConvertFrom-Json).Checkpoint
$after = Get-Content -Raw -LiteralPath (Join-Path $Directory 'after-restart/login-observation.json') | ConvertFrom-Json
foreach ($field in @('CharacterId', 'MapId', 'Level')) {
	if ($before.$field -ne $after.$field) { throw "Resume changed $field." }
}
# Match the ordinary SIM persistence check: SQL FLOAT round trips are not bit-exact.
foreach ($axis in @('X', 'Y', 'Z')) {
	if ([Math]::Abs($before.Position.$axis - $after.Position.$axis) -gt 0.05) { throw "Resume changed position $axis." }
}
if ($before.Position.Heading -ne $after.Position.Heading) { throw 'Resume changed heading.' }
function Same($Name, $BeforeRows, $AfterRows) {
	$expected = ConvertTo-Json -InputObject @($BeforeRows) -Depth 8 -Compress
	$actual = ConvertTo-Json -InputObject @($AfterRows) -Depth 8 -Compress
	if ($expected -cne $actual) { throw "Resume changed $Name. Expected $expected; observed $actual" }
}
Same 'completed journal' ($before.CompletedQuestIds | Sort-Object) ($after.CompletedQuestIds | Sort-Object)
Same 'active journal' ($before.Quests | Where-Object Status -In 3,4 | Sort-Object QuestId | Select-Object QuestId,Status,StepAndFlags,CompleteCount) `
	($after.Quests | Where-Object Status -In 3,4 | Sort-Object QuestId | Select-Object QuestId,Status,StepAndFlags,CompleteCount)
Same 'inventory' ($before.Inventory | Sort-Object ObjectId) ($after.Inventory | Sort-Object ObjectId)
# SM_SKILL_LIST includes a time-dependent flag; compare persisted proficiency, not that wire timestamp.
Same 'skills' ($before.Skills | Sort-Object SkillId | Select-Object SkillId,Level,ProfessionBarSize,SkillType) `
	($after.Skills | Sort-Object SkillId | Select-Object SkillId,Level,ProfessionBarSize,SkillType)
[pscustomobject]@{
	CharacterId = $after.CharacterId
	CompletedCount = @($after.CompletedQuestIds).Count
	Verified = @('identity', 'map', 'position', 'level', 'active journal', 'completed journal', 'inventory', 'skills')
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $Directory 'persistence-verification.json')
Write-Host "NI-08 persistence verified for character $($after.CharacterId)."
