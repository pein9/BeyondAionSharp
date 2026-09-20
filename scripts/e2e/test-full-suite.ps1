[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'full-suite.ps1')
function Assert-True([bool]$Condition, [string]$Message) {
	if (-not $Condition) { throw $Message }
}
function Assert-Throws([scriptblock]$Action, [string]$Pattern) {
	try { & $Action } catch {
		if ($_.Exception.Message -notlike $Pattern) { throw }
		return
	}
	throw "Expected failure: $Pattern"
}
$manifest = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '../../parity-artifacts/e2e/scenarios.json') | ConvertFrom-Json
$breadth = @(Get-FullSuitePlan -Manifest $manifest)
$expectedLive = @($manifest | Where-Object { $_.modes -contains 'Live' -and $_.tier -ne 'Soak' } | ForEach-Object id | Sort-Object)
$actualLive = @($breadth | Where-Object kind -EQ Live | ForEach-Object scenario | Sort-Object)
Assert-True (($actualLive -join ',') -ceq ($expectedLive -join ',')) 'Breadth must execute every LIVE manifest scenario exactly once.'
$expectedSim = @($manifest | Where-Object { $_.modes -contains 'Sim' -and $_.tier -ne 'Soak' } | ForEach-Object id | Sort-Object)
$actualSim = @($breadth | Where-Object kind -EQ Sim | ForEach-Object scenarios | Sort-Object)
Assert-True (($actualSim -join ',') -ceq ($expectedSim -join ',')) 'Breadth lost or duplicated SIM coverage.'
Assert-True (@($breadth | Where-Object kind -EQ Soak).Count -eq 0) 'Breadth unexpectedly launches a soak.'
$liveSteps = @($breadth | Where-Object kind -EQ Live)
Assert-True ($liveSteps[0].scenario -ceq 'L0' -and $liveSteps[-1].scenario -ceq 'canaries') 'L0/canary ordering changed.'
Assert-True ($breadth[-1].kind -eq 'QuestCoverage') 'Coverage must follow the last breadth child.'
$l0Index = [Array]::FindIndex($breadth, [Predicate[object]]{ param($step) $step.id -eq 'live-l0' })
Assert-True ($breadth[$l0Index + 1].kind -eq 'PacketParity') 'Packet parity must immediately follow LIVE L0.'
Assert-True (($liveSteps | Where-Object scenario -EQ Q4I).stepTimeoutSeconds -eq 1800) 'Quest plan deadline regressed.'

foreach ($shards in @(1, 2, 100)) {
	$plan = @(Get-FullSuitePlan -Manifest $manifest -SimShards $shards)
	$sim = @($plan | Where-Object kind -EQ Sim)
	$shared = 0
	foreach ($scenario in $manifest) {
		if ($scenario.modes -notcontains 'Sim' -or $scenario.tier -eq 'Soak') { continue }
		$key = if ($scenario.resetEpoch) { "reset-$($scenario.id)" } else { 'shard-{0:D2}' -f ($shared++ % $shards) }
		$owner = @($sim | Where-Object { $_.scenarios -ccontains $scenario.id })
		Assert-True ($owner.Count -eq 1 -and $owner[0].processKey -ceq $key) "Wrong process for $($scenario.id) at $shards shards."
	}
}
$all = @(Get-FullSuitePlan -Manifest $manifest -Suite All)
$soak = @($all | Where-Object kind -EQ Soak)
Assert-True (($soak.bots -join ',') -eq '50,200,500') 'Capacity matrix changed.'
Assert-True (@($soak | Where-Object durationSeconds -NE 7200).Count -eq 0) 'Acceptance soaks must be two hours.'
Assert-True ($all.Count -eq $breadth.Count + 3) 'All omitted or duplicated breadth steps.'
$short = @(Get-FullSuitePlan -Manifest $manifest -Suite Soak -SoakBots 200 -SoakSeconds 30)
Assert-True ($short.Count -eq 1 -and $short[0].bots -eq 200 -and $short[0].durationSeconds -eq 30) 'Focused diagnostic soak selection failed.'
Assert-Throws { Get-FullSuitePlan -Manifest $manifest -SoakBots @(50, 50) } '*distinct soak population*'
Assert-Throws { Get-FullSuitePlan -Manifest ($manifest + $manifest[0]) } '*duplicate scenario id*'
Assert-Throws { Get-FullSuitePlan -Manifest @($manifest | Where-Object id -NE L0) } '*L0 in both*'
$new = [pscustomobject]@{ id = 'OPS-NEW'; tier = 'Full'; modes = @('Live'); resetEpoch = $false }
$expanded = @(Get-FullSuitePlan -Manifest ($manifest + $new))
Assert-True (@($expanded | Where-Object id -EQ live-ops-new).Count -eq 1) 'New manifest scenario requires manual runner edits.'

# Exercise failure semantics without launching Docker, databases or real children.
$calls = [Collections.Generic.List[string]]::new()
$results = [Collections.Generic.List[object]]::new()
Invoke-FullSuitePlan -Plan $short -Execute { param($step) $calls.Add($step.id) } -Record { param($result) $results.Add($result) }
Assert-True ($calls.Count -eq 1 -and $results.Count -eq 1 -and $results[0].status -eq 'Passed') 'Successful step was not recorded.'
$calls.Clear(); $results.Clear()
Assert-Throws {
	Invoke-FullSuitePlan -Plan $soak -Execute {
		param($step)
		$calls.Add($step.id)
		if ($step.bots -eq 200) { throw 'injected child failure' }
	} -Record { param($result) $results.Add($result) }
} '*injected child failure*'
Assert-True ($calls.Count -eq 2 -and $results.Count -eq 2) 'Failure was retried or later children ran.'
Assert-True ($results[0].status -eq 'Passed' -and $results[1].status -eq 'Failed' -and
	$results[1].error -like '*injected child failure*') 'Failure detail was lost or labeled passed.'

# Execute the actual public runner's dispatch block with recording children. This
# catches dropped arguments (especially the previously missing LIVE seed) rather
# than merely checking a second implementation of the intended call contract.
$tokens = $null
$parseErrors = $null
$runnerAst = [Management.Automation.Language.Parser]::ParseFile(
	(Join-Path $PSScriptRoot 'run-full.ps1'), [ref]$tokens, [ref]$parseErrors)
Assert-True ($parseErrors.Count -eq 0) 'Public runner must parse.'
$invoke = $runnerAst.Find({ param($node)
	$node -is [Management.Automation.Language.CommandAst] -and $node.GetCommandName() -eq 'Invoke-FullSuitePlan'
}, $true)
$dispatch = $null
for ($i = 0; $i -lt $invoke.CommandElements.Count - 1; $i++) {
	if ($invoke.CommandElements[$i] -is [Management.Automation.Language.CommandParameterAst] -and
		$invoke.CommandElements[$i].ParameterName -eq 'Execute') {
		$dispatch = $invoke.CommandElements[$i + 1].ScriptBlock.GetScriptBlock()
	}
}
Assert-True ($null -ne $dispatch) 'Public runner dispatch block missing.'
$recorded = [Collections.Generic.List[object]]::new()
$runSimTier = {
	param($Run, $Tier, $ProcessKey, $ShardCount, $Seed, $SimulationRunId, $RunRoot)
	$recorded.Add([pscustomobject]@{ kind = 'Sim'; seed = $Seed; run = $Run; root = $RunRoot; shards = $ShardCount; parent = $SimulationRunId })
}
$runLive = {
	param($Run, $Scenario, $WatcherMode, $RunRoot, [switch]$FullRun, [switch]$PacketTap, [switch]$SkipImageBuild, $StepTimeoutSeconds, $Seed, $BotExecution)
	$recorded.Add([pscustomobject]@{ kind = 'Live'; seed = $Seed; run = $Run; root = $RunRoot;
		full = [bool]$FullRun; tap = [bool]$PacketTap; skipBuild = [bool]$SkipImageBuild; watcher = $WatcherMode; execution = $BotExecution })
}
$runSoak = {
	param($Run, $RunRoot, [switch]$FullRun, $Bots, $DurationSeconds, $Seed, [switch]$PacketTap, [switch]$SkipImageBuild, $BotExecution)
	$recorded.Add([pscustomobject]@{ kind = 'Soak'; seed = $Seed; run = $Run; root = $RunRoot;
		full = [bool]$FullRun; tap = [bool]$PacketTap; skipBuild = [bool]$SkipImageBuild; bots = $Bots; seconds = $DurationSeconds; execution = $BotExecution })
}
$Run = 'contract'
$runRoot = 'contract-artifacts'
$Seed = 73
$SimShards = 2
$PacketTap = $true
$Suite = 'All'
$BotExecution = 'Docker'
$suiteState = @{ imagesReady = $false }
$dispatchPlan = @($breadth[0], $liveSteps[0], $liveSteps[1], $soak[1])
Invoke-FullSuitePlan -Plan $dispatchPlan -Execute $dispatch -Record { param($result) }
Assert-True ($recorded.Count -eq 4 -and @($recorded | Where-Object seed -NE 73).Count -eq 0) 'Child seed propagation failed.'
Assert-True (@($recorded | Where-Object root -NE $runRoot).Count -eq 0) 'Children escaped the shared artifact root.'
Assert-True ($recorded[0].shards -eq 2 -and $recorded[0].parent -eq 'contract') 'SIM identity propagation failed.'
Assert-True (-not $recorded[1].skipBuild -and $recorded[2].skipBuild -and $recorded[3].skipBuild) 'Images must build only once.'
Assert-True ($recorded[1].full -and $recorded[1].tap -and $recorded[1].watcher -eq 'enforce') 'LIVE observability or retention contract lost.'
Assert-True ($recorded[3].full -and $recorded[3].tap -and $recorded[3].bots -eq 200 -and $recorded[3].seconds -eq 7200) 'Soak invocation contract lost.'
Assert-True (@($recorded | Where-Object { $_.kind -ne 'Sim' -and $_.execution -cne 'Docker' }).Count -eq 0) 'Docker bot execution was not forwarded to every LIVE/soak child.'

$planned = & (Join-Path $PSScriptRoot 'run-full.ps1') -Suite All -PlanOnly -Run p10-plan-only-test -Seed 73 | ConvertFrom-Json
Assert-True ($planned.seed -eq 73 -and $planned.steps.Count -eq $all.Count) 'Public plan-only contract failed.'
if (-not $planned.soakRunnerAvailable) {
	Assert-Throws { & (Join-Path $PSScriptRoot 'run-full.ps1') -Suite Soak -Run p10-unavailable-test } '*Soak is unavailable*'
	Assert-True (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot '../../run/p10-unavailable-test'))) 'Unavailable soak mutated run output.'
}
Write-Host "Full-suite contract passed: $($actualSim.Count) SIM scenarios, $($actualLive.Count) LIVE scenarios, 3 soak populations; failure and planning contracts verified."
