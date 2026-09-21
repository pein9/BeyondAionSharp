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
