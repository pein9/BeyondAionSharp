# Full LIVE orchestration. Child run-live/run-soak scripts remain single-attempt primitives.
. (Join-Path $PSScriptRoot 'live-retry.ps1')

function Invoke-AionFullLiveStep {
	param([object]$Step, [hashtable]$Context, [string]$Run, [string]$RunRoot,
		[scriptblock]$Execute, [scriptblock]$Admit)
	if ($Step.kind -notin @('Live', 'Soak') -or $Step.id -notmatch '^[a-z0-9][a-z0-9_-]*$' -or $Run -notmatch '^[a-z0-9][a-z0-9_-]*$') {
		throw 'Invalid Full LIVE step identity.'
	}
	$baseRun = "$Run-$(if ($Step.kind -eq 'Live') { $Step.scenario.ToLowerInvariant() } else { $Step.id })"
	$attemptRoot = Join-Path $RunRoot 'live-attempts'
	New-Item -ItemType Directory -Path $attemptRoot -Force | Out-Null
	$receipt = Join-Path $attemptRoot "$($Step.id).json"
	if ((Test-Path -LiteralPath $receipt) -or (Test-Path -LiteralPath (Join-Path $attemptRoot "$($Step.id).jsonl"))) {
		throw 'Attempt receipts already exist; refusing to overwrite or rerun this step.'
	}
	& $Admit
	Invoke-AionScenarioAttempts -Mode LIVE -Run $baseRun -Execute $Execute -BeforeRetry {
		param($first, $retryRun)
		# The single-attempt runner has returned and reaped its child. Docker must also
		# prove the old owned project is gone before the fresh project can start.
		$containers = @(& docker ps -a --filter "label=com.docker.compose.project=aion-bots-$($first.run)" --format '{{.ID}}')
		if ($LASTEXITCODE -ne 0) { throw 'Cannot verify previous Docker stack removal; no retry started.' }
		if ($containers.Count -ne 0) { throw 'Previous Docker stack remains; no retry started.' }
		[ordered]@{ priorRun = $first.run; retryRun = $retryRun; passed = $true;
			checkedUtc = [DateTimeOffset]::UtcNow.ToString('O'); containers = @($containers) } |
			ConvertTo-Json | Set-Content -LiteralPath (Join-Path $attemptRoot "$($Step.id).retry-admission.json") -Encoding utf8NoBOM
	} -RecordAttempt {
		param($attempt)
		$attempt | ConvertTo-Json -Depth 8 -Compress |
			Add-Content -LiteralPath (Join-Path $attemptRoot "$($Step.id).jsonl") -Encoding utf8NoBOM
	} -RecordResult {
		param($result)
		$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $receipt -Encoding utf8NoBOM
		$Context.status = $result.status
		$Context.attemptReceipt = "live-attempts/$($Step.id).json"
		$Context.attemptReceiptSha256 = (Get-FileHash -LiteralPath $receipt -Algorithm SHA256).Hash.ToLowerInvariant()
		$Context.selectedChild = $result.attempts[-1].run
	}
}
