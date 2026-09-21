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
	Write-Host 'Full flake finalization passed: history after clean/FLAKY/failed runs, non-green persistence failures, and soak build identity. All child commands mocked.'
} finally {
	$resolved = [IO.Path]::GetFullPath($testRoot)
	if ((Split-Path $resolved -Parent) -ne [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/') -or
		(Split-Path $resolved -Leaf) -notmatch '^aion-full-flake-[a-f0-9]{32}$') { throw 'Unsafe Full flake test cleanup path.' }
	Remove-Item -LiteralPath $resolved -Recurse -Force
}
