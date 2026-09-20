[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'soak-heap-readiness.ps1')
$heapTestRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) ('aion-heap-ready-test-' + [Guid]::NewGuid().ToString('N'))))
New-Item -ItemType Directory -Path $heapTestRoot | Out-Null
$heapTestNow = [DateTimeOffset]::UtcNow
function Assert-HeapReady([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Assert-HeapThrows([scriptblock]$Action, [string]$Pattern) {
	try { & $Action } catch { if ($_.Exception.Message -notlike $Pattern) { throw }; return }
	throw "Expected failure: $Pattern"
}
function Write-HeapFixture([string]$Server, [long]$Index = 1, [long]$Heap = 1000,
	[DateTimeOffset]$At = $heapTestNow, [string]$Run = 'test', [string]$Identity = $Server) {
	$folder = Join-Path $heapTestRoot "logs/$Server"
	New-Item -ItemType Directory -Path $folder -Force | Out-Null
	$row = [pscustomobject]@{ ts = $At.ToString('O'); run = $Run; srv = $Identity;
		cat = 'Aion.Commons.Diagnostics.ServerHeartbeatService';
		msg = "Server heartbeat: connections=1, packetQueueDepth=0, armedTimers=0, workingSetBytes=1234, lastGcHeapBytes=$Heap, lastGcIndex=$Index, dispatcherWrites=null" }
	$row | ConvertTo-Json -Compress | Set-Content -LiteralPath (Join-Path $folder "$Server.events.jsonl") -Encoding utf8NoBOM
}
try {
	Assert-HeapReady (-not (Get-SoakHeapReadiness $heapTestRoot test $heapTestNow).ready) 'Missing logs passed readiness.'
	foreach ($server in @('ls','cs','gs')) { Write-HeapFixture $server }
	Assert-HeapReady (Get-SoakHeapReadiness $heapTestRoot test $heapTestNow).ready 'Fresh measured heaps were not ready.'
	Write-HeapFixture cs 0 0
	$missing = Get-SoakHeapReadiness $heapTestRoot test $heapTestNow
	Assert-HeapReady (-not $missing.ready -and $null -eq $missing.servers[1].lastGcHeapBytes -and
		$missing.servers[1].reason -eq 'no-completed-collection') 'No collection was invented as a zero heap.'
	Write-HeapFixture cs 1 0
	Assert-HeapReady (Get-SoakHeapReadiness $heapTestRoot test $heapTestNow).ready 'Real measured zero was rejected.'
	Write-HeapFixture cs 0 1000
	Assert-HeapThrows { Get-SoakHeapReadiness $heapTestRoot test $heapTestNow } '*Invalid heap readiness observation*'
	Write-HeapFixture cs -At $heapTestNow.AddSeconds(-31)
	Assert-HeapReady (-not (Get-SoakHeapReadiness $heapTestRoot test $heapTestNow).ready) 'Stale heartbeat passed.'
	Write-HeapFixture cs -At $heapTestNow.AddSeconds(10)
	Assert-HeapThrows { Get-SoakHeapReadiness $heapTestRoot test $heapTestNow } '*in the future*'
	Write-HeapFixture cs -Run wrong
	Assert-HeapThrows { Get-SoakHeapReadiness $heapTestRoot test $heapTestNow } '*another run/server*'
	Write-HeapFixture cs -Identity gs
	Assert-HeapThrows { Get-SoakHeapReadiness $heapTestRoot test $heapTestNow } '*another run/server*'
	Write-HeapFixture cs -At $heapTestNow.ToOffset([TimeSpan]::FromHours(-4))
	Assert-HeapReady (Get-SoakHeapReadiness $heapTestRoot test $heapTestNow).ready 'UTC offset was discarded.'
	$chatPath = Join-Path $heapTestRoot 'logs/cs/cs.events.jsonl'
	$good = Get-Content -LiteralPath $chatPath -Raw
	# The latest observation wins even if an older observation would have passed.
	Write-HeapFixture cs 0 0
	$uncollected = Get-Content -LiteralPath $chatPath -Raw
	Set-Content -LiteralPath $chatPath -Value ($good + $uncollected) -NoNewline -Encoding utf8NoBOM
	Assert-HeapReady (-not (Get-SoakHeapReadiness $heapTestRoot test $heapTestNow).ready) 'Older observed GC hid the latest unavailable snapshot.'
	# Shared finite snapshot tolerates a producer and ignores its incomplete last record.
	$writer = [IO.FileStream]::new($chatPath, [IO.FileMode]::Create, [IO.FileAccess]::ReadWrite, [IO.FileShare]::ReadWrite)
	try {
		$payload = [Text.Encoding]::UTF8.GetBytes(('x' * 300000) + "`n" + $good + '{unfinished')
		$writer.Write($payload); $writer.Flush()
		Assert-HeapReady (Get-SoakHeapReadiness $heapTestRoot test $heapTestNow).ready 'Active-log bounded snapshot failed.'
		$writer.WriteByte(32); $writer.Flush()
	}
	finally { $writer.Dispose() }
	Write-HeapFixture cs
	Add-Content -LiteralPath $chatPath -Value '{"cat":"Aion.Commons.Diagnostics.ServerHeartbeatService","run":"test","srv":"cs","msg":"Scheduled timer census: {}"}' -Encoding utf8NoBOM
	Assert-HeapReady (Get-SoakHeapReadiness $heapTestRoot test $heapTestNow).ready 'Supplemental census hid a real heartbeat.'
	foreach ($server in @('ls','cs','gs')) { Write-HeapFixture $server -At ([DateTimeOffset]::UtcNow) }
	Wait-SoakHeapReadiness -RunDirectory $heapTestRoot -Run test -TimeoutSeconds 0
	$journal = Get-Content (Join-Path $heapTestRoot 'soak-heap-readiness.json') -Raw | ConvertFrom-Json
	Assert-HeapReady ($journal.status -eq 'ready' -and -not $journal.overallSoakAccepted) 'Readiness claimed capacity acceptance.'
	Write-HeapFixture cs 0 0 -At ([DateTimeOffset]::UtcNow)
	Assert-HeapThrows { Wait-SoakHeapReadiness -RunDirectory $heapTestRoot -Run test -TimeoutSeconds 0 } '*unavailable after 0 seconds*'
	$journal = Get-Content (Join-Path $heapTestRoot 'soak-heap-readiness.json') -Raw | ConvertFrom-Json
	Assert-HeapReady ($journal.status -eq 'failed' -and -not $journal.observations.ready) 'Timeout left a stale ready report.'
	Assert-HeapThrows { Wait-SoakHeapReadiness -RunDirectory $heapTestRoot -Run test -TimeoutSeconds 0 -CheckWatcher { throw 'watcher exited' } } '*watcher exited*'
	# Exercise an actual waiting iteration without a real delay or manufacturing a collection.
	function Start-Sleep {
		param([int]$Milliseconds)
		Assert-HeapReady ($Milliseconds -gt 0 -and $Milliseconds -le 5000) 'Unbounded poll delay.'
		Write-HeapFixture cs -At ([DateTimeOffset]::UtcNow)
	}
	Wait-SoakHeapReadiness -RunDirectory $heapTestRoot -Run test -TimeoutSeconds 10
	Remove-Item Function:Start-Sleep
	# Parse the real call site: full-duration SOAK only, after watcher launch and before any bot launch.
	$tokens = $null; $errors = $null
	$ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'run-live.ps1'), [ref]$tokens, [ref]$errors)
	Assert-HeapReady ($errors.Count -eq 0) 'LIVE runner did not parse.'
	$guards = @($ast.FindAll({ param($node) $node -is [Management.Automation.Language.IfStatementAst] -and
		$node.Clauses[0].Item1.Extent.Text -eq '$Scenario -contains ''SOAK'' -and $SoakSeconds -eq 7200' }, $true))
	Assert-HeapReady ($guards.Count -eq 1 -and $guards[0].Extent.Text.Contains('Wait-SoakHeapReadiness')) 'Capacity-only readiness guard missing.'
	$source = $ast.Extent.Text
	Assert-HeapReady ($source.IndexOf('$watcherProcess = Start-Process') -lt $guards[0].Extent.StartOffset -and
		$guards[0].Extent.EndOffset -lt $source.IndexOf('& dotnet @botArguments')) 'Readiness is outside watcher/bot lifecycle boundaries.'
	# A dynamically extracted script block has no file-backed PSScriptRoot; preserve only that source anchor.
	$guard = [scriptblock]::Create($guards[0].Extent.Text.Replace('$PSScriptRoot', "'" + $PSScriptRoot.Replace("'", "''") + "'"))
	$runPath = $heapTestRoot; $Run = 'test'; $SoakHeapReadyTimeoutSeconds = 0
	$watcherProcess = [pscustomobject]@{ HasExited = $false }
	foreach ($case in @(@{ scenario = 'SOAK'; seconds = 7200; ready = $true },
		@{ scenario = 'SOAK'; seconds = 1200; ready = $false }, @{ scenario = 'L0'; seconds = 7200; ready = $false })) {
		$Scenario = @($case.scenario); $SoakSeconds = $case.seconds
		Remove-Item -LiteralPath (Join-Path $heapTestRoot 'soak-heap-readiness.json') -Force
		. $guard
		Assert-HeapReady ((Test-Path -LiteralPath (Join-Path $heapTestRoot 'soak-heap-readiness.json')) -eq $case.ready) 'Actual runner guard selected the wrong scenario/duration.'
		if (-not $case.ready) { '{}' | Set-Content -LiteralPath (Join-Path $heapTestRoot 'soak-heap-readiness.json') }
	}
	Write-Host 'Soak heap readiness passed: availability, identity, freshness, shared bounded reads, timeout, watcher exit and capacity-only integration.'
}
finally {
	$parent = [IO.Path]::GetFullPath((Split-Path $heapTestRoot -Parent)).TrimEnd([IO.Path]::DirectorySeparatorChar)
	if ($parent -ne [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) -or
		(Split-Path $heapTestRoot -Leaf) -notmatch '^aion-heap-ready-test-[a-f0-9]{32}$') { throw 'Unsafe readiness test cleanup path.' }
	Remove-Item -LiteralPath $heapTestRoot -Recurse -Force
}
