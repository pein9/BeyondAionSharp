# Read-only reconstruction through the production planner, not a parallel scenario list.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$PlanPath)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'full-suite.ps1')
$plan = Get-Content -Raw -LiteralPath $PlanPath | ConvertFrom-Json
if ($plan.suite -notin @('Breadth', 'All')) { throw 'Promotion requires complete breadth, not a standalone/soak selection.' }
$manifest = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '../../parity-artifacts/e2e/scenarios.json') | ConvertFrom-Json
$selection = @{ Manifest=$manifest; Suite=$plan.suite; SimShards=$plan.simShards }
if ($plan.suite -eq 'All') {
	$soaks = @($plan.steps | Where-Object kind -CEQ 'Soak')
	$durations = @($soaks.durationSeconds | Select-Object -Unique)
	if ($durations.Count -ne 1) { throw 'All requires a consistent planned soak duration.' }
	$selection.SoakBots = @($soaks.bots)
	$selection.SoakSeconds = $durations[0]
}
ConvertTo-Json -Depth 12 -InputObject @(Get-FullSuitePlan @selection)
