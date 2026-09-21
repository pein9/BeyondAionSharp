[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'run-full.ps1'), [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw 'Full runner parse failed.' }
$outer = @($ast.EndBlock.Statements | Where-Object { $_ -is [Management.Automation.Language.TryStatementAst] })
if ($outer.Count -ne 1) { throw 'Expected one Full execution/finalization boundary.' }
$finalize = [scriptblock]::Create("param(`$PSScriptRoot)`n" + $outer[0].Finally.Extent.Text.Trim().TrimStart('{').TrimEnd('}'))
$runnerScriptRoot = $PSScriptRoot
$testRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) ('aion-full-flake-' + [Guid]::NewGuid().ToString('N'))))
New-Item -ItemType Directory -Path $testRoot | Out-Null
$state = @{ rejectReport=$false; historyFails=$false; calls=[Collections.Generic.List[string]]::new() }
function Write-AionRunReport {
	$state.calls.Add('report')
	if ($state.rejectReport) { throw 'FLAKY is not a clean Full pass' }
}
function python {
	if ($args[0] -like '*flake-history.py' -and $args[1] -eq 'record') {
		$state.calls.Add('history')
		$global:LASTEXITCODE = if ($state.historyFails) { 2 } else { 0 }
	} elseif ($args[0] -like '*report-run.py') {
		$state.calls.Add('rerender')
		$global:LASTEXITCODE = 1
	} else { throw 'Unexpected finalizer subprocess.' }
}
try {
	$Run='full-test'; $suiteStarted=[DateTimeOffset]::UtcNow; $suiteTimer=[Diagnostics.Stopwatch]::StartNew()
	$gitSha='contract'; $Seed=1
	foreach ($mode in @('passed', 'flaky', 'history-failed', 'original-failed')) {
		$state.calls.Clear(); $state.rejectReport=$mode -eq 'flaky'; $state.historyFails=$mode -eq 'history-failed'
		$suiteFailure = if ($mode -eq 'original-failed') { [Management.Automation.ErrorRecord]::new([Exception]::new('original suite failure'), 'original', 'NotSpecified', $null) } else { $null }
		$runRoot = Join-Path $testRoot $mode
		New-Item -ItemType Directory -Path $runRoot | Out-Null
		$caught = $null
		Push-Location $testRoot
		try { . $finalize $runnerScriptRoot } catch { $caught = $_ }
		$expected = if ($state.historyFails) { 'report,history,rerender' } else { 'report,history' }
		if (($state.calls -join ',') -cne $expected) { throw "Full finalizer skipped history after $mode. Calls: $($state.calls -join ','); error: $caught" }
		if (($null -ne $caught) -ne ($mode -in @('flaky', 'history-failed'))) { throw "Wrong Full finalizer exit for $mode." }
		if ($state.historyFails -and -not (Test-Path (Join-Path $runRoot 'flake-history-error.json'))) { throw 'History loss would leave a green report.' }
	}
	# Also execute the actual soak finalizer with a retained child SHA, not a duplicate call implementation.
	$soakAst = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot '../live/run-soak.ps1'), [ref]$tokens, [ref]$errors)
	$soakOuter = @($soakAst.EndBlock.Statements | Where-Object { $_ -is [Management.Automation.Language.TryStatementAst] })
	$soakFinalize = [scriptblock]::Create($soakOuter[0].Finally.Extent.Text.Trim().TrimStart('{').TrimEnd('}'))
	$runPath = Join-Path $testRoot 'soak'
	New-Item -ItemType Directory -Path $runPath | Out-Null
	@{ gitSha='child-built-revision' } | ConvertTo-Json | Set-Content (Join-Path $runPath 'runner-result.json')
	$state.sha=$null
	function Write-AionRunReport { param($GitSha) $state.sha=$GitSha }
	$failure=$null; $started=[DateTimeOffset]::UtcNow; $soakTimer=[Diagnostics.Stopwatch]::StartNew()
	Push-Location $testRoot
	. $soakFinalize
	if ($state.sha -cne 'child-built-revision') { throw 'Soak wrapper discarded the child build revision.' }
	$standalonePath = Join-Path $runnerScriptRoot '../live/run-scenario.ps1'
	$standaloneAst = [Management.Automation.Language.Parser]::ParseFile($standalonePath, [ref]$tokens, [ref]$errors)
	if ($errors.Count) { throw 'Standalone runner parse failed.' }
	$standaloneOuter = @($standaloneAst.EndBlock.Statements | Where-Object { $_ -is [Management.Automation.Language.TryStatementAst] })
	$standaloneFinalize = [scriptblock]::Create($standaloneOuter[0].Finally.Extent.Text.Trim().TrimStart('{').TrimEnd('}'))
	$e2eRoot=$runnerScriptRoot; $FlakeLedger=Join-Path $testRoot 'private-ledger.json'; $Scenario='L0'; $timer=[Diagnostics.Stopwatch]::StartNew()
	function Write-AionRunReport {
		param($Mode)
		if ($Mode -cne 'LIVE_RETRY') { throw 'Standalone masqueraded as Full.' }
		$state.calls.Add('report')
		if ($state.rejectReport) { throw 'FLAKY is non-green' }
	}
	foreach ($reject in @($false, $true)) {
		$state.calls.Clear(); $state.rejectReport=$reject; $state.historyFails=$false
		$caught=$null
		try { . $standaloneFinalize } catch { $caught=$_ }
		if (($state.calls -join ',') -cne 'report,history' -or ($null -ne $caught) -ne $reject) { throw 'Standalone skipped history on FLAKY.' }
	}
	$planned = & $standalonePath -Scenario L0 -PlanOnly -Seed 73 -BotExecution Docker | ConvertFrom-Json
	if ($planned.mode -cne 'LIVE_RETRY' -or $planned.fullHistoryWindow -ne $false -or $planned.seed -ne 73 -or
		$planned.steps.Count -ne 1 -or $planned.steps[0].bots -ne 2 -or $planned.maxAttempts -ne 2) { throw 'Standalone planning lost scope, seed, population or retry policy.' }
	$rejected=$false
	try { & $standalonePath -Scenario SOAK -PlanOnly } catch { $rejected=$true }
	if (-not $rejected) { throw 'Standalone must not silently substitute for soak acceptance.' }
	& {
		$invoke = $standaloneAst.Find({ param($node) $node -is [Management.Automation.Language.CommandAst] -and $node.GetCommandName() -eq 'Invoke-FullSuitePlan' }, $true)
		$dispatch=$null
		for ($i=0; $i -lt $invoke.CommandElements.Count-1; $i++) {
			if ($invoke.CommandElements[$i] -is [Management.Automation.Language.CommandParameterAst] -and $invoke.CommandElements[$i].ParameterName -eq 'Execute') {
				$dispatch=$invoke.CommandElements[$i+1].ScriptBlock.GetScriptBlock()
			}
		}
		function python { $global:LASTEXITCODE=0 }
		function Invoke-AionFullLiveStep {
			param($Step, $Context, $Run, $RunRoot, $Execute, $Admit)
			& $Admit
			& $Execute "$Run-l0" 1
		}
		$Seed=73; $PacketTap=$true; $BotExecution='Docker'; $ProblemLedger=Join-Path $testRoot 'problem-ledger.json'
		$state.imagesReady=$false; $state.childChecked=$false
		$runLive = {
			param($Run, $Scenario, $Bots, $Seed, $StepTimeoutSeconds, $WatcherMode, $RunRoot, [switch]$FullRun,
				[switch]$PacketTap, [switch]$SkipImageBuild, $BotExecution, $ProblemLedger)
			if ($Scenario -cne 'L0' -or $Bots -ne 2 -or $Seed -ne 73 -or $StepTimeoutSeconds -ne 15 -or $WatcherMode -cne 'enforce' -or
				$FullRun -or -not $PacketTap -or $SkipImageBuild -or $BotExecution -cne 'Docker' -or $ProblemLedger -cne (Join-Path $testRoot 'problem-ledger.json')) {
				throw 'Standalone dispatch changed population/identity/settings or incorrectly advertised Full.'
			}
			$state.childChecked=$true
		}
		& $dispatch $planned.steps[0] @{}
		if (-not $state.childChecked) { throw 'Standalone public dispatch never invoked the child.' }
	}
	Write-Host 'Full flake finalization passed: history after clean/FLAKY/failed runs, non-green persistence failures, and soak build identity. All child commands mocked.'
	Write-Host 'Standalone finalization/planning passed: explicit LIVE_RETRY scope, non-Full history, FLAKY persistence and one exact LIVE scenario.'
} finally {
	$resolved = [IO.Path]::GetFullPath($testRoot)
	if ((Split-Path $resolved -Parent) -ne [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/') -or
		(Split-Path $resolved -Leaf) -notmatch '^aion-full-flake-[a-f0-9]{32}$') { throw 'Unsafe Full flake test cleanup path.' }
	Remove-Item -LiteralPath $resolved -Recurse -Force
}
