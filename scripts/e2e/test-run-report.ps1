[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'run-report.ps1')
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('aion-run-report-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
try {
	# Real public finalizer and Python renderer, but no scenarios, bots, DB or Docker.
	$started = [DateTimeOffset]::UtcNow
	Write-AionRunReport -RunDirectory $testRoot -Run run-report-test -Mode SIM -Scenarios @('Q1', 'Q2') `
		-Status failed -StartedUtc $started -DurationSeconds 3 -Failure 'original setup failure' -GitSha contract -Seed 71
	$report = Get-Content -Raw -LiteralPath (Join-Path $testRoot 'report.json') | ConvertFrom-Json
	if ($report.status -ne 'failed' -or $report.counts.skipped -ne 2 -or $report.runner.seed -ne 71 -or
		$report.runner.gitSha -ne 'contract' -or $report.runner.error -ne 'original setup failure') { throw 'Failed-run report lost evidence.' }
	$threw = $false
	try {
		Write-AionRunReport -RunDirectory $testRoot -Run run-report-test -Mode SIM -Scenarios @('Q1') `
			-Status passed -StartedUtc $started -DurationSeconds 3
	} catch { $threw = $true }
	if (-not $threw) { throw 'Missing scenario evidence was accepted.' }
	$rows = @(
		@{ schemaVersion=1; run='run-report-test'; mode='SIM'; scenario='Q1'; event='started'; status='running';
			startedUtc=$started.ToString('O'); timestampUtc=$started.ToString('O'); durationSeconds=$null; exitCode=$null; error=$null },
		@{ schemaVersion=1; run='run-report-test'; mode='SIM'; scenario='Q1'; event='completed'; status='passed';
			startedUtc=$started.ToString('O'); timestampUtc=$started.ToString('O'); durationSeconds=1.5; exitCode=0; error=$null }
	)
	$rows | ForEach-Object { $_ | ConvertTo-Json -Compress } | Set-Content -LiteralPath (Join-Path $testRoot 'scenario-results.jsonl') -Encoding utf8NoBOM
	$header = @{ schemaVersion=1; event='run-started'; run='run-report-test'; mode='SIM'; seed=1; gitSha=''; profile='sim-fast'; startedUtc=$started.ToString('O') }
	foreach ($name in @('ledger', 'allowlist')) {
		$path = Join-Path $testRoot "sim-$name-at-start.json"
		'[]' | Set-Content -LiteralPath $path -Encoding utf8NoBOM
		$header["$($name)Sha256"] = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
	}
	@($header,
		@{ event='policy-started'; run='run-report-test'; scenario='Q1'; policy=1 },
		@{ event='policy-completed'; run='run-report-test'; scenario='Q1'; policy=1; assertedClean=$true; assertionPassed=$true; virtualMillis=100; observations=@() },
		@{ event='run-completed'; run='run-report-test'; policiesStarted=1; policiesCompleted=1; activePolicies=@() }
	) | ForEach-Object { $_ | ConvertTo-Json -Depth 5 -Compress } | Set-Content -LiteralPath (Join-Path $testRoot 'sim-problems.jsonl') -Encoding utf8NoBOM
	Write-AionRunReport -RunDirectory $testRoot -Run run-report-test -Mode SIM -Scenarios @('Q1') `
		-Status passed -StartedUtc $started -DurationSeconds 3
	$report = Get-Content -Raw -LiteralPath (Join-Path $testRoot 'report.json') | ConvertFrom-Json
	if ($report.status -ne 'passed' -or $report.counts.passed -ne 1) { throw 'Complete scenario was not reported.' }

	# Every public owner must finalize after its try body, not only on success.
	foreach ($relative in @('run-full.ps1', '../sim/run-sim-tier.ps1', '../live/run-live.ps1', '../live/run-soak.ps1')) {
		$tokens = $null; $parseErrors = $null
		$ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot $relative), [ref]$tokens, [ref]$parseErrors)
		if ($parseErrors.Count) { throw "Runner parse failed: $relative" }
		$calls = @($ast.FindAll({ param($node)
			$node -is [Management.Automation.Language.CommandAst] -and $node.GetCommandName() -eq 'Write-AionRunReport'
		}, $true))
		if ($calls.Count -ne 1) { throw "Runner lacks one report finalizer: $relative" }
		$insideFinally = $false
		for ($parent = $calls[0].Parent; $null -ne $parent; $parent = $parent.Parent) {
			if ($parent -is [Management.Automation.Language.TryStatementAst] -and $null -ne $parent.Finally -and
				$calls[0].Extent.StartOffset -ge $parent.Finally.Extent.StartOffset -and $calls[0].Extent.EndOffset -le $parent.Finally.Extent.EndOffset) {
				$insideFinally = $true
			}
		}
		if (-not $insideFinally) { throw "Report would be skipped on failure: $relative" }
	}
	Write-Host 'Run reporting contract passed: actual success/failure rendering, missing evidence rejection, and all runner finally hooks.'
} finally {
	$resolved = [IO.Path]::GetFullPath($testRoot)
	if ((Split-Path $resolved -Parent) -ne [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/') -or
		(Split-Path $resolved -Leaf) -notmatch '^aion-run-report-[a-f0-9]{32}$') { throw 'Unsafe reporting test cleanup path.' }
	Remove-Item -LiteralPath $resolved -Recurse -Force
}
