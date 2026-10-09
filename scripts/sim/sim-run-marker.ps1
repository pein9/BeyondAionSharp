# NR-45: runs side by side (docs/natural-all-classes-ntc.md, rule (w), Survey C1 part 10).
#
# A SIM run in progress is marked by a file under run/.sim-running/. While a mark is live nothing is built: a build
# would fail on the files the run holds, or change what a later run of the same batch loads.
#
# sim-snapshot.ps1 and run-neutral-gate.ps1 dot-source this file. Run as a script it lists the live runs and exits 1
# when there is one, so that anything else that builds can ask first:
#
#   pwsh -NoProfile -File scripts/sim/sim-run-marker.ps1
param([string]$MarkerRoot)

function Get-SimRunMarkerRoot([string]$RepoRoot) { Join-Path $RepoRoot 'run/.sim-running' }

# The live marks. A mark whose process is gone, or whose process id now belongs to another process, is removed.
function Get-LiveSimRuns([string]$Root) {
	if (-not (Test-Path -LiteralPath $Root)) { return @() }
	$live = @()
	foreach ($file in @(Get-ChildItem -LiteralPath $Root -Filter '*.json' -File)) {
		$mark = $null
		try { $mark = Get-Content -Raw -LiteralPath $file.FullName | ConvertFrom-Json } catch { }
		$process = if ($mark) { Get-Process -Id ([int]$mark.pid) -ErrorAction SilentlyContinue } else { $null }
		$same = $process -and [math]::Abs(($process.StartTime.ToUniversalTime() -
			([datetime]$mark.processStartUtc).ToUniversalTime()).TotalSeconds) -lt 2
		if ($same) { $live += $mark }
		else { Remove-Item -LiteralPath $file.FullName -ErrorAction SilentlyContinue }
	}
	$live
}

# Marks this process as playing a run. Returns the mark's path for Remove-SimRunMarker.
function New-SimRunMarker([string]$Root, [string]$RunId) {
	New-Item -ItemType Directory -Force -Path $Root | Out-Null
	$path = Join-Path $Root "$PID-$([Guid]::NewGuid().ToString('N')).json"
	[ordered]@{
		pid = $PID
		processStartUtc = (Get-Process -Id $PID).StartTime.ToUniversalTime().ToString('o')
		run = $RunId
		markedUtc = (Get-Date).ToUniversalTime().ToString('o')
	} | ConvertTo-Json | Set-Content -LiteralPath $path -Encoding utf8
	$path
}

function Remove-SimRunMarker([string]$Path) {
	if ($Path) { Remove-Item -LiteralPath $Path -ErrorAction SilentlyContinue }
}

function Assert-NoSimRunBeforeBuild([string]$Root) {
	$live = @(Get-LiveSimRuns $Root)
	if ($live.Count) {
		$names = ($live | ForEach-Object { "$($_.run) in process $($_.pid)" }) -join '; '
		throw "A SIM run is in progress ($names). Nothing is built while a run is going; pass -NoBuild to play the build that is there."
	}
}

if ($MyInvocation.InvocationName -ne '.') {
	if (-not $MarkerRoot) { $MarkerRoot = Get-SimRunMarkerRoot ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))) }
	$running = @(Get-LiveSimRuns $MarkerRoot)
	if (-not $running.Count) { Write-Host 'No SIM run is in progress.'; exit 0 }
	foreach ($mark in $running) { Write-Host "SIM run in progress: $($mark.run), process $($mark.pid), since $($mark.markedUtc)" }
	exit 1
}
