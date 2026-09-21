[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'live-retry.ps1')
function Assert-True([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Exercise([string]$Mode, [int]$Failures, [bool]$GuardFails = $false, [bool]$RecordFails = $false) {
	$state = @{ calls = [Collections.Generic.List[string]]::new(); guards = 0;
		attempts = [Collections.Generic.List[object]]::new(); results = [Collections.Generic.List[object]]::new(); error = $null }
	try {
		Invoke-AionScenarioAttempts -Mode $Mode -Run contract-l0 -Execute {
			param($attemptRun, $number)
			$state.calls.Add($attemptRun)
			if ($number -le $Failures) { throw "original failure $number" }
		} -BeforeRetry {
			param($first, $secondRun)
			$state.guards++
			Assert-True ($first.status -ceq 'failed' -and $secondRun -ceq 'contract-l0-retry1') 'Retry guard identity lost.'
			Assert-True ($state.attempts.Count -eq 1) 'Original attempt must be persisted before retry admission.'
			if ($GuardFails) { throw 'stack still running' }
		} -RecordAttempt {
			param($attempt)
			$state.attempts.Add($attempt)
			if ($RecordFails) { throw 'cannot persist original evidence' }
		} -RecordResult { param($result) $state.results.Add($result) }
	} catch { $state.error = $_.Exception.ToString() }
	return $state
}
foreach ($mode in @('SIM', 'LIVE')) {
	$state = Exercise $mode 0
	Assert-True ($state.calls.Count -eq 1 -and $state.guards -eq 0 -and $null -eq $state.error) 'Success must not retry.'
	Assert-True ($state.results.Count -eq 1 -and $state.results[0].status -ceq 'passed') 'Success result lost.'
}
$state = Exercise SIM 2
Assert-True ($state.calls.Count -eq 1 -and $state.guards -eq 0 -and $state.error -like '*original failure 1*') 'SIM must never retry.'
Assert-True ($state.results[0].status -ceq 'failed') 'SIM failure mislabeled.'
$state = Exercise LIVE 1
Assert-True (($state.calls -join ',') -ceq 'contract-l0,contract-l0-retry1') 'Retry must have a distinct identity.'
Assert-True ($state.guards -eq 1 -and $null -eq $state.error -and $state.results[0].status -ceq 'flaky') 'Recovered LIVE failure is FLAKY.'
Assert-True ($state.attempts[0].error -like '*original failure 1*' -and $state.attempts[1].status -ceq 'passed') 'Both attempts must remain intact.'
$state = Exercise LIVE 5
Assert-True ($state.calls.Count -eq 2 -and $state.guards -eq 1 -and $state.results[0].status -ceq 'failed') 'LIVE must stop after the second failure.'
Assert-True ($state.error -like '*original failure 1*' -and $state.error -like '*original failure 2*') 'Terminal error must preserve both causes.'
$state = Exercise LIVE 1 $true
Assert-True ($state.calls.Count -eq 1 -and $state.results[0].retryBlockedError -like '*stack still running*') 'Unsafe cleanup must prevent retry.'
Assert-True ($state.error -like '*stack still running*' -and $state.error -like '*original failure 1*') 'Admission failure lost original failure.'
$state = Exercise LIVE 1 $false $true
Assert-True ($state.calls.Count -eq 1 -and $state.guards -eq 0 -and $state.error -like '*cannot persist*') 'Lost original receipts must stop before retry.'
Assert-True ($state.results.Count -eq 0) 'Recording failure must not produce a recovered result.'
Write-Host 'LIVE retry contract passed: one sequential retry, distinct identity, retained original failure, safety admission, no SIM retries. No bots/DB/processes started.'

# Exercise the actual Full wrapper and its Docker query with mocked Docker only.
. (Join-Path $PSScriptRoot 'full-live-retry.ps1')
$wrapperRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) ('aion-retry-wrapper-' + [Guid]::NewGuid().ToString('N'))))
New-Item -ItemType Directory -Path $wrapperRoot | Out-Null
$dockerState = @{ calls = 0; mode = 'gone' }
function docker {
	$dockerState.calls++
	Assert-True (($args -join ' ') -like 'ps -a --filter label=com.docker.compose.project=aion-bots-*-l0 --format {{.ID}}') 'Retry queried the wrong Docker project.'
	$global:LASTEXITCODE = if ($dockerState.mode -eq 'unavailable') { 1 } else { 0 }
	if ($dockerState.mode -eq 'remaining') { 'owned-container' }
}
try {
	foreach ($mode in @('gone', 'remaining', 'unavailable', 'quarantine')) {
		$dockerState.mode = $mode; $dockerState.calls = 0
		$state = @{ calls = [Collections.Generic.List[string]]::new(); error = $null }
		$context = @{}
		$root = Join-Path $wrapperRoot $mode
		$step = [pscustomobject]@{ id='live-l0'; kind='Live'; scenario='L0' }
		try {
			Invoke-AionFullLiveStep -Step $step -Context $context -Run $mode -RunRoot $root -Admit {
				if ($mode -eq 'quarantine') { throw 'quarantined' }
			} -Execute {
				param($attemptRun, $number)
				$state.calls.Add($attemptRun)
				if ($number -eq 1) { throw 'original child failure' }
			}
		} catch { $state.error = $_.Exception.ToString() }
		if ($mode -eq 'quarantine') {
			Assert-True ($state.calls.Count -eq 0 -and $dockerState.calls -eq 0) 'Quarantine must prevent all child execution.'
			continue
		}
		$receiptPath = Join-Path $root $context.attemptReceipt
		$receipt = Get-Content -Raw -LiteralPath $receiptPath | ConvertFrom-Json
		$journal = @(Get-Content -LiteralPath (Join-Path $root 'live-attempts/live-l0.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
		Assert-True ($journal.Count -eq $state.calls.Count -and $receipt.attempts.Count -eq $journal.Count) 'Attempt journal/receipt incomplete.'
		Assert-True ($context.attemptReceiptSha256 -ceq (Get-FileHash -LiteralPath $receiptPath).Hash.ToLowerInvariant()) 'Terminal suite receipt hash lost.'
		if ($mode -eq 'gone') {
			Assert-True ($state.calls.Count -eq 2 -and $context.status -ceq 'flaky' -and $null -eq $state.error) 'Safe Full retry did not recover as FLAKY.'
			$guard = Get-Content -Raw (Join-Path $root 'live-attempts/live-l0.retry-admission.json') | ConvertFrom-Json
			Assert-True ($guard.passed -and $guard.containers.Count -eq 0 -and $guard.retryRun -ceq 'gone-l0-retry1') 'Fresh-stack admission receipt missing.'
		} else {
			Assert-True ($state.calls.Count -eq 1 -and $context.status -ceq 'failed' -and $state.error -like '*original child failure*') 'Unsafe stack started a second child or lost original failure.'
		}
		$before = (Get-FileHash -LiteralPath $receiptPath).Hash
		$rejected = $false
		try { Invoke-AionFullLiveStep -Step $step -Context @{} -Run $mode -RunRoot $root -Admit {} -Execute { throw 'must not run' } }
		catch { $rejected = $_.Exception.Message -like '*already exist*' }
		Assert-True ($rejected -and $before -ceq (Get-FileHash -LiteralPath $receiptPath).Hash) 'Re-entry overwrote original attempt evidence.'
	}
	Write-Host 'Full LIVE wrapper contract passed: exact Docker project guard, retained journal/receipt, quarantine and re-entry rejection. Docker mocked.'
} finally {
	$resolved = [IO.Path]::GetFullPath($wrapperRoot)
	if ((Split-Path $resolved -Parent) -ne [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/') -or
		(Split-Path $resolved -Leaf) -notmatch '^aion-retry-wrapper-[a-f0-9]{32}$') { throw 'Unsafe retry test cleanup path.' }
	Remove-Item -LiteralPath $resolved -Recurse -Force
}
