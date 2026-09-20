[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
& (Join-Path $PSScriptRoot 'test-live-build-provenance.ps1')
& (Join-Path $PSScriptRoot 'test-stop-watcher.ps1')
& (Join-Path $PSScriptRoot 'test-soak-heap-readiness.ps1')
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'run-soak.ps1'), [ref]$tokens, [ref]$errors)
if ($errors.Count -ne 0) { throw 'Soak runner did not parse.' }
$outer = @($ast.EndBlock.Statements | Where-Object { $_ -is [Management.Automation.Language.TryStatementAst] })
if ($outer.Count -ne 1) { throw 'Expected one public runner execution block.' }
# Execute the actual dispatch body, substituting only child programs. Never invoke Docker.
$dispatch = [scriptblock]::Create($outer[0].Body.Extent.Text.Trim().Substring(1).TrimEnd().TrimEnd('}'))
$testRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) ('aion-soak-runner-test-' + [Guid]::NewGuid().ToString('N'))))
New-Item -ItemType Directory -Path $testRoot | Out-Null
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$RunRoot = $testRoot; $Bots = 200; $DurationSeconds = 7200; $Seed = 73
$FullRun = $true; $PacketTap = $true; $SkipImageBuild = $true
$script:called = [Collections.Generic.List[string]]::new()
function dotnet {
	param([Parameter(ValueFromRemainingArguments = $true)][object[]]$Arguments)
	$script:called.Add('workload')
	if (($Arguments -join ' ') -notlike '*--soak-evidence*') { throw 'Missing evidence command.' }
	$script:LASTEXITCODE = if ($script:mode -eq 'workload-fails') { 1 } else { 0 }
}
function python {
	param([Parameter(ValueFromRemainingArguments = $true)][object[]]$Arguments)
	$script:called.Add('acceptance')
	if (($Arguments -join ' ') -notlike '*soak-acceptance.py*') { throw 'Missing acceptance command.' }
	$script:LASTEXITCODE = if ($script:mode -eq 'acceptance-fails') { 1 } else { 0 }
}
$runLive = {
	param($Run, $RunRoot, [switch]$FullRun, $Scenario, $Bots, $SoakSeconds, $Seed, $StepTimeoutSeconds,
		$WatcherMode, [switch]$PacketTap, [switch]$SkipImageBuild)
	$script:called.Add('live')
	if ($Scenario -ne 'SOAK' -or $Bots -ne 200 -or $SoakSeconds -ne 7200 -or $Seed -ne 73 -or
		$StepTimeoutSeconds -ne 1800 -or $WatcherMode -ne 'enforce' -or -not ($FullRun -and $PacketTap -and $SkipImageBuild)) {
		throw 'Soak child settings were lost.'
	}
	New-Item -ItemType Directory -Path (Join-Path $RunRoot $Run) | Out-Null
	if ($script:mode -eq 'live-fails') { throw 'intentional LIVE failure' }
}
try {
	foreach ($script:mode in @('passed', 'live-fails', 'workload-fails', 'acceptance-fails')) {
		$Run = $script:mode; $runPath = Join-Path $testRoot $Run
		$failure = $null; $success = $false; $started = [DateTimeOffset]::UtcNow
		$script:called.Clear(); $threw = $false
		try { . $dispatch } catch { $threw = $true }
		if ($threw -ne ($script:mode -ne 'passed')) { throw "Wrong exit semantics for $script:mode" }
		if (($script:called -join ',') -ne 'live,workload,acceptance') { throw 'Failure did not retain both evidence reports.' }
		$execution = Get-Content -LiteralPath (Join-Path $runPath 'soak-execution.json') -Raw | ConvertFrom-Json
		if ($execution.success -ne ($script:mode -ne 'live-fails') -or $execution.seed -ne 73 -or $execution.run -ne $Run) {
			throw 'Terminal invocation journal is wrong.'
		}
	}
	Write-Host 'Soak runner contract passed: propagation, terminal journal, and every failure boundary.'
}
finally {
	$parent = [IO.Path]::GetFullPath((Split-Path $testRoot -Parent)).TrimEnd([IO.Path]::DirectorySeparatorChar)
	if ($parent -ne [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) -or
		(Split-Path $testRoot -Leaf) -notmatch '^aion-soak-runner-test-[a-f0-9]{32}$') { throw 'Unsafe contract-test cleanup path.' }
	Remove-Item -LiteralPath $testRoot -Recurse -Force
}
