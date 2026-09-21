# Injectable sequential retry controller. Loading this file starts no work.
# The caller must validate retained evidence before calling a recovered attempt FLAKY.
function Invoke-AionScenarioAttempts {
	[CmdletBinding()]
	param(
		[Parameter(Mandatory)][ValidateSet('SIM', 'LIVE')][string]$Mode,
		[Parameter(Mandatory)][ValidatePattern('^[a-z0-9][a-z0-9_-]*$')][string]$Run,
		[Parameter(Mandatory)][scriptblock]$Execute,
		[Parameter(Mandatory)][scriptblock]$BeforeRetry,
		[Parameter(Mandatory)][scriptblock]$RecordAttempt,
		[Parameter(Mandatory)][scriptblock]$RecordResult
	)
	$attempts = [Collections.Generic.List[object]]::new()
	$limit = if ($Mode -eq 'LIVE') { 2 } else { 1 }
	for ($number = 1; $number -le $limit; $number++) {
		$attemptRun = if ($number -eq 1) { $Run } else { "$Run-retry1" }
		if ($number -eq 2) {
			try {
				# Must prove the first attempt is terminal and its owned stack is gone.
				# Failure here never permits an overlapping retry or a third attempt.
				$null = & $BeforeRetry $attempts[0] $attemptRun
			} catch {
				$blocked = $_.Exception.ToString()
				& $RecordResult ([pscustomobject]@{ mode = $Mode; run = $Run; status = 'failed';
					attempts = $attempts.ToArray(); retryBlockedError = $blocked })
				throw "Retry safety check failed: $blocked`nOriginal failure: $($attempts[0].error)"
			}
		}
		$started = [DateTimeOffset]::UtcNow
		$timer = [Diagnostics.Stopwatch]::StartNew()
		$failure = $null
		try { $null = & $Execute $attemptRun $number }
		catch { $failure = $_ }
		$attempt = [pscustomobject]@{ number = $number; run = $attemptRun;
			status = $(if ($null -eq $failure) { 'passed' } else { 'failed' });
			startedUtc = $started.ToString('O'); durationSeconds = $timer.Elapsed.TotalSeconds;
			error = $(if ($null -eq $failure) { $null } else { $failure.Exception.ToString() }) }
		$attempts.Add($attempt)
		# Persist the original failure before any retry. Recording failures abort execution.
		& $RecordAttempt $attempt
		if ($null -eq $failure -or $number -eq $limit) {
			$status = if ($null -ne $failure) { 'failed' } elseif ($number -eq 2) { 'flaky' } else { 'passed' }
			& $RecordResult ([pscustomobject]@{ mode = $Mode; run = $Run; status = $status;
				attempts = $attempts.ToArray(); retryBlockedError = $null })
			if ($null -ne $failure) {
				throw (($attempts | ForEach-Object { "Attempt $($_.number) ($($_.run)): $($_.error)" }) -join "`n")
			}
			return
		}
	}
}
