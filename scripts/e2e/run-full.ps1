[CmdletBinding()]
param(
	[ValidatePattern('^[a-z0-9][a-z0-9_-]*$')]
	[string]$Run = ("full-" + [DateTimeOffset]::Now.ToString('yyyyMMdd-HHmmss')),

	[switch]$PacketTap,

	[switch]$SkipImageBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$runRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot "run/$Run"))
if (Test-Path -LiteralPath $runRoot) {
	throw "Full-run directory already exists: $runRoot"
}
New-Item -ItemType Directory -Path $runRoot | Out-Null

$runLive = Join-Path $repoRoot 'scripts/live/run-live.ps1'
Push-Location $repoRoot
try {
	& $runLive -Run "$Run-l0" -Scenario 'L0' -WatcherMode 'enforce' -RunRoot $runRoot -FullRun `
		-PacketTap:$PacketTap -SkipImageBuild:$SkipImageBuild

	# The first child built the same three server images when a rebuild was requested.
	& $runLive -Run "$Run-canaries" -Scenario 'canaries' -WatcherMode 'enforce' -RunRoot $runRoot -FullRun `
		-PacketTap:$PacketTap -SkipImageBuild
}
finally {
	Pop-Location
}

Write-Host "Full LIVE run $Run passed. Artifacts: $runRoot"
