# Capacity telemetry requires an actual last-GC snapshot in every measurement window.
# Read existing observations only: never force GC, allocate pressure, or synthesize a zero heap.
function Read-SoakHeartbeatTail([string]$Path) {
	if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return @() }
	$stream = [IO.FileStream]::new($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read,
		[IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete)
	try {
		# Finite snapshot, even while a producer keeps appending. Drop both partial edge records.
		$length = $stream.Length
		$count = [int][Math]::Min(262144L, $length)
		$offset = $length - $count
		[void]$stream.Seek($offset, [IO.SeekOrigin]::Begin)
		$bytes = [byte[]]::new($count)
		$read = 0
		while ($read -lt $count) {
			$n = $stream.Read($bytes, $read, $count - $read)
			if ($n -eq 0) { throw 'Server log changed length during readiness snapshot.' }
			$read += $n
		}
		$text = [Text.Encoding]::UTF8.GetString($bytes)
		if ($offset -gt 0) {
			$first = $text.IndexOf("`n")
			if ($first -lt 0) { return @() }
			$text = $text.Substring($first + 1)
		}
		$last = $text.LastIndexOf("`n")
		if ($last -lt 0) { return @() }
		return @($text.Substring(0, $last).Split("`n") | Where-Object { $_.Length -gt 0 })
	}
	finally { $stream.Dispose() }
}

function Get-SoakHeapReadiness([string]$RunDirectory, [string]$Run, [DateTimeOffset]$Now) {
	$servers = @()
	foreach ($server in @('ls', 'cs', 'gs')) {
		$sample = $null
		$lines = @(Read-SoakHeartbeatTail (Join-Path $RunDirectory "logs/$server/$server.events.jsonl"))
		for ($i = $lines.Count - 1; $i -ge 0; $i--) {
			$document = [Text.Json.JsonDocument]::Parse([string]$lines[$i])
			try {
				$row = $document.RootElement
				if ($row.GetProperty('cat').GetString() -ne 'Aion.Commons.Diagnostics.ServerHeartbeatService') { continue }
				if ($row.GetProperty('run').GetString() -cne $Run -or $row.GetProperty('srv').GetString() -cne $server) {
					throw 'Heap readiness heartbeat belongs to another run/server.'
				}
				$message = $row.GetProperty('msg').GetString()
				if ($message.StartsWith('Scheduled timer census: ')) { continue }
				if ($message -notmatch '^Server heartbeat: connections=\d+, packetQueueDepth=\d+, armedTimers=\d+, workingSetBytes=(\d+), lastGcHeapBytes=(\d+), lastGcIndex=(\d+), dispatcherWrites=') {
					throw 'Missing or changed heap readiness heartbeat schema.'
				}
				$working = [long]$Matches[1]; $heap = [long]$Matches[2]; $index = [long]$Matches[3]
				if ($working -eq 0 -or ($index -eq 0 -and $heap -ne 0)) { throw 'Invalid heap readiness observation.' }
				$timestamp = $row.GetProperty('ts')
				if ($timestamp.GetString() -notmatch '(Z|[+-]\d{2}:\d{2})$') { throw 'Heap readiness timestamp needs an explicit offset.' }
				$at = $timestamp.GetDateTimeOffset()
				$age = ($Now - $at).TotalSeconds
				if ($age -lt -1) { throw 'Heap readiness heartbeat is in the future.' }
				$reason = if ($age -gt 30) { 'stale-heartbeat' } elseif ($index -eq 0) { 'no-completed-collection' } else { 'ready' }
				$sample = [pscustomobject]@{ server = $server; ready = $reason -eq 'ready'; reason = $reason;
					observedUtc = $at.ToString('O'); lastGcIndex = $index; lastGcHeapBytes = if ($index -eq 0) { $null } else { $heap } }
				break
			}
			finally { $document.Dispose() }
		}
		if ($null -eq $sample) {
			$sample = [pscustomobject]@{ server = $server; ready = $false; reason = 'missing-heartbeat';
				observedUtc = $null; lastGcIndex = $null; lastGcHeapBytes = $null }
		}
		$servers += $sample
	}
	return [pscustomobject]@{ schemaVersion = 1; run = $Run; ready = @($servers | Where-Object { -not $_.ready }).Count -eq 0;
		servers = $servers; overallSoakAccepted = $false }
}

function Wait-SoakHeapReadiness {
	param([string]$RunDirectory, [string]$Run, [ValidateRange(0, 7200)][int]$TimeoutSeconds = 5400,
		[scriptblock]$CheckWatcher = {})
	$started = [DateTimeOffset]::UtcNow
	$clock = [Diagnostics.Stopwatch]::StartNew()
	$nextNotice = 0
	$result = $null
	$failure = $null
	try {
		do {
			& $CheckWatcher
			$result = Get-SoakHeapReadiness $RunDirectory $Run ([DateTimeOffset]::UtcNow)
			if ($result.ready) {
				Write-Host ('Soak heap observations ready after {0:n1}s; measured workload has not started.' -f $clock.Elapsed.TotalSeconds)
				return
			}
			if ($clock.Elapsed.TotalSeconds -ge $TimeoutSeconds) { throw "Soak heap observations unavailable after $TimeoutSeconds seconds." }
			if ($clock.Elapsed.TotalSeconds -ge $nextNotice) {
				$missing = @($result.servers | Where-Object { -not $_.ready } | ForEach-Object { "$($_.server):$($_.reason)" })
				Write-Host ('Waiting for natural heap observations ({0:n0}s): {1}' -f $clock.Elapsed.TotalSeconds, ($missing -join ', '))
				$nextNotice = $clock.Elapsed.TotalSeconds + 60
			}
			Start-Sleep -Milliseconds ([int][Math]::Min(5000, [Math]::Max(1, ($TimeoutSeconds - $clock.Elapsed.TotalSeconds) * 1000)))
		} while ($true)
	}
	catch { $failure = $_; throw }
	finally {
		[pscustomobject]@{ schemaVersion = 1; run = $Run; startedUtc = $started.ToString('O');
			completedUtc = [DateTimeOffset]::UtcNow.ToString('O'); elapsedSeconds = $clock.Elapsed.TotalSeconds;
			timeoutSeconds = $TimeoutSeconds; status = if ($null -eq $failure) { 'ready' } else { 'failed' };
			failure = if ($null -eq $failure) { $null } else { $failure.ToString() }; observations = $result;
			overallSoakAccepted = $false } | ConvertTo-Json -Depth 8 |
			Set-Content -LiteralPath (Join-Path $RunDirectory 'soak-heap-readiness.json') -Encoding utf8NoBOM
	}
}
