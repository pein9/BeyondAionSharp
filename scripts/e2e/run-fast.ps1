[CmdletBinding()]
param(
	[ValidatePattern('^[a-z0-9][a-z0-9_-]*$')]
	[string]$Run = ("fast-" + [DateTimeOffset]::Now.ToString('yyyyMMdd-HHmmss')),

	[int]$Seed = 1,

	[string]$RunRoot,
	[switch]$CodeCoverage
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$runSimTier = Join-Path $PSScriptRoot '../sim/run-sim-tier.ps1'
& $runSimTier -Run $Run -Tier Fast -ProcessKey 'shard-00' -ShardCount 1 -Seed $Seed -RunRoot $RunRoot -CodeCoverage:$CodeCoverage
