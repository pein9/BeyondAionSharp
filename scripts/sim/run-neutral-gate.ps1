# The neutral gate (docs/natural-class-profiles.md, section 8, CP-04): replay named scopes of the natural journey at
# seed 1 and compare each trace with its recorded baseline. One command, one verdict.
#
#   run-neutral-gate.ps1 -Set p+c [-Item CP-13] [-Run <id>]
#       Build once, replay each scope through sim-snapshot.ps1 -Action Replay, and compare the normalized trace with
#       the scope's row in parity-artifacts/e2e/natural-neutral-baseline.json. A passing candidate trace is deleted and
#       only its verdict kept; a failing one is kept with the first differing record. Exit code 0 only on pass.
#   run-neutral-gate.ps1 -Set p+c -Record [-Item CP-08] [-Run <id>] [-Ignore <field path>...] [-ReRecord]
#       Refuse uncommitted changes under src, tests, game-server and parity-artifacts. Play each scope twice, compare the
#       two passes, and only when every scope repeats: copy the traces to run/cp/baseline/<sha>/ and to the second copy
#       beside the repository, then write the baseline file once. A Priest or Cleric scope that already has a row is
#       not recorded again without -ReRecord, which is the operator's decision (rule (j) and the re-record rule).
#
# The verdict is written to run/cp/<Item>/<Run>/verdict.json: pass, fail or refused-dirty-record.
# Set names are joined with +. `all` is every Priest and Cleric scope that has a baseline row.
param(
	[Parameter(Mandatory)]
	[string]$Set,
	[switch]$Record,
	[switch]$ReRecord,
	[ValidatePattern('^[A-Za-z0-9][A-Za-z0-9-]*$')]
	[string]$Item = 'gate',
	[ValidatePattern('^[A-Za-z0-9][A-Za-z0-9-]*$')]
	[string]$Run = ('gate-' + (Get-Date -Format 'yyyyMMdd-HHmmss')),
	# Field paths left out of the comparison, as compare_traces.py --ignore takes them. Recorded with the scope's row.
	[string[]]$Ignore = @(),
	[string]$Docker = 'docker',
	[string]$Git = 'git',
	[string]$Python = 'python',
	[string]$SnapshotRoot,
	[string]$ReplayRoot,
	[string]$BaselineFile,
	[string]$BaselineRoot,
	[string]$SecondCopyRoot,
	[switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not $SnapshotRoot) { $SnapshotRoot = Join-Path $repoRoot 'run/snapshots' }
if (-not $ReplayRoot) { $ReplayRoot = Join-Path $repoRoot 'run/cp' }
if (-not $BaselineFile) { $BaselineFile = Join-Path $repoRoot 'parity-artifacts/e2e/natural-neutral-baseline.json' }
if (-not $BaselineRoot) { $BaselineRoot = Join-Path $repoRoot 'run/cp/baseline' }
if (-not $SecondCopyRoot) { $SecondCopyRoot = Join-Path (Split-Path -Parent $repoRoot) 'BeyondAionSharp-cp-baseline' }
$snapshotScript = Join-Path $PSScriptRoot 'sim-snapshot.ps1'
$compareScript = Join-Path $PSScriptRoot 'trace/compare_traces.py'

# The scope table. Data: what each scope replays, all at seed 1. A class scope is off until its row stands in the
# baseline file; sim-snapshot.ps1 learns -Class in CP-28, before the first class scope is recorded.
$scopeTable = [ordered]@{
	p        = @{ line = 'priest-cleric'; replay = [ordered]@{ CapitalStage = 'start' } }
	m        = @{ line = 'priest-cleric'; replay = [ordered]@{} }
	b        = @{ line = 'priest-cleric'; replay = [ordered]@{ Bridge = $true } }
	l1       = @{ line = 'priest-cleric'; replay = [ordered]@{ AltgardLeg1 = $true; Leg = 'l1'; From = 'altgard'; LaterCapital = $true } }
	c        = @{ line = 'priest-cleric'; replay = [ordered]@{ AltgardLeg1 = $true; Leg = 'l4'; From = 'altgard-rc-l3' } }
	hm       = @{ line = 'priest-cleric'; replay = [ordered]@{ AltgardLeg1 = $true; Leg = 'l12'; From = 'altgard-coingear' } }
	ax       = @{ line = 'priest-cleric'; replay = [ordered]@{ AltgardLeg1 = $true; Leg = 'ax'; From = 'altgard-rc-complete-s1' } }
	mage     = @{ line = 'mage'; replay = [ordered]@{ Class = 'mage'; StopAt = '2004:5:0' } }
	warrior  = @{ line = 'warrior'; replay = [ordered]@{ Class = 'warrior'; StopAt = '2004:5:0' } }
	artist   = @{ line = 'artist'; replay = [ordered]@{ Class = 'artist'; StopAt = '2004:5:0' } }
	engineer = @{ line = 'engineer'; replay = [ordered]@{ Class = 'engineer'; StopAt = '2004:5:0' } }
	scout    = @{ line = 'scout'; replay = [ordered]@{ Class = 'scout'; StopAt = '2004:5:0' } }
}
# The environment every gate run is pinned to. Null is unset: help items on, the bot monitor at its default port.
$pinned = [ordered]@{ AION_SIM_SEED = '1'; NA_HELP_ITEMS = $null; AION_BOT_DASHBOARD_PORT = $null; AION_SIM_PROCESS_KEY = $null }

function Invoke-Python([string[]]$Arguments) {
	$output = & $Python @Arguments
	[pscustomobject]@{ code = $LASTEXITCODE; text = ($output -join "`n") }
}

function Get-TraceDigest([string]$Trace, [string[]]$IgnorePaths) {
	$arguments = @($compareScript, '--digest', $Trace, '--json')
	foreach ($path in $IgnorePaths) { $arguments += @('--ignore', $path) }
	$result = Invoke-Python $arguments
	if ($result.code -ne 0) { throw "compare_traces.py --digest failed for $Trace" }
	$result.text | ConvertFrom-Json
}

function Get-TraceDifference([string]$TraceA, [string]$TraceB, [string[]]$IgnorePaths) {
	$arguments = @($compareScript, $TraceA, $TraceB)
	foreach ($path in $IgnorePaths) { $arguments += @('--ignore', $path) }
	$result = Invoke-Python $arguments
	if ($result.code -notin @(0, 1)) { throw "compare_traces.py failed for $TraceA and $TraceB" }
	$result.text
}

function Read-Baseline {
	if (-not (Test-Path -LiteralPath $BaselineFile)) { return [ordered]@{ schemaVersion = 1; scopes = @() } }
	$document = Get-Content -Raw -LiteralPath $BaselineFile | ConvertFrom-Json
	if ($document.schemaVersion -ne 1) { throw "Unsupported neutral baseline schema in $BaselineFile" }
	[ordered]@{ schemaVersion = 1; scopes = @($document.scopes) }
}

# One replay of one scope. Returns the evidence folder and its single trace, or the reason there is none.
function Invoke-ScopeReplay([string]$Scope, [string]$ReplayRun) {
	$evidence = Join-Path (Join-Path $ReplayRoot $Item) $ReplayRun
	$arguments = @{ Action = 'Replay'; Item = $Item; Run = $ReplayRun; NoBuild = $true; Docker = $Docker
		SnapshotRoot = $SnapshotRoot; ReplayRoot = $ReplayRoot }
	foreach ($key in $scopeTable[$Scope].replay.Keys) { $arguments[$key] = $scopeTable[$Scope].replay[$key] }
	$failure = $null
	try { & $snapshotScript @arguments | Out-Null }
	catch { $failure = "replay failed: $($_.Exception.Message)" }
	$traces = @()
	if (Test-Path -LiteralPath $evidence) { $traces = @(Get-ChildItem -LiteralPath $evidence -Filter '*.trace.jsonl' -File) }
	if (-not $failure -and $traces.Count -ne 1) { $failure = "replay left $($traces.Count) traces in $evidence" }
	[pscustomobject]@{ evidence = $evidence; trace = $(if ($traces.Count -eq 1) { $traces[0].FullName } else { $null }); failure = $failure }
}

$known = @($scopeTable.Keys)
$priestCleric = @($known | Where-Object { $scopeTable[$_].line -eq 'priest-cleric' })
$baseline = Read-Baseline
$rows = @{}
foreach ($row in $baseline.scopes) { $rows[[string]$row.scope] = $row }

# Resolve the set. In a record run `all` is the seven Priest and Cleric scopes; otherwise the ones that were kept.
$named = @($Set.Split('+', [StringSplitOptions]::RemoveEmptyEntries) | ForEach-Object { $_.Trim() })
if (-not $named.Count) { throw 'Set names no scope.' }
$scopes = [Collections.Generic.List[string]]::new()
$leftOut = @()
foreach ($name in $named) {
	$expanded = @($name)
	if ($name -eq 'all') {
		$expanded = @($priestCleric | Where-Object { $Record -or $rows.ContainsKey($_) })
		$leftOut = @($priestCleric | Where-Object { $_ -notin $expanded })
		if (-not $expanded.Count) { throw "Set all is empty: no Priest or Cleric scope has a baseline row in $BaselineFile." }
	}
	elseif ($name -notin $known) { throw "Unknown scope '$name'. Known scopes: $($known -join ', '), and the set all." }
	foreach ($scope in $expanded) { if (-not $scopes.Contains($scope)) { $scopes.Add($scope) } }
}
foreach ($scope in $scopes) {
	if (-not $Record -and -not $rows.ContainsKey($scope)) {
		throw "Scope '$scope' has no baseline row in $BaselineFile; record it first with -Record."
	}
	if ($Record -and -not $ReRecord -and $rows.ContainsKey($scope) -and $scopeTable[$scope].line -eq 'priest-cleric') {
		throw "Scope '$scope' already has a baseline row. A Priest or Cleric scope is recorded again only on the operator's decision: pass -ReRecord."
	}
}
if ($Ignore.Count -and -not $Record) { throw 'Ignore is recorded with a baseline; a comparison uses the list its row holds.' }

$gateFolder = Join-Path (Join-Path $ReplayRoot $Item) $Run
if (Test-Path -LiteralPath $gateFolder) { throw "Gate evidence already exists: $gateFolder. Choose a new -Run." }
$sha = (& $Git -C $repoRoot rev-parse HEAD | Out-String).Trim()
if ($sha -notmatch '^[0-9a-f]{40}$') { throw "Could not read the commit: $sha" }
$uncommitted = @(& $Git -C $repoRoot status --porcelain -- src tests game-server parity-artifacts)
$verdict = [ordered]@{
	schemaVersion = 1; item = $Item; run = $Run; mode = $(if ($Record) { 'record' } else { 'compare' })
	set = $Set; scopes = @(); leftOutOfAll = $leftOut; commit = $sha; uncommitted = $uncommitted
	environment = $pinned; verdict = $null
}
function Save-Verdict([string]$Outcome) {
	$verdict.verdict = $Outcome
	New-Item -ItemType Directory -Force -Path $gateFolder | Out-Null
	$verdict | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $gateFolder 'verdict.json') -Encoding utf8
	Write-Host "Neutral gate $Set ($($verdict.mode)): $($Outcome.ToUpperInvariant()). Verdict: $(Join-Path $gateFolder 'verdict.json')"
}

if ($Record -and $uncommitted.Count) {
	# Baselines belong to one commit. A record run from a changed tree would not say which code it recorded.
	Save-Verdict 'refused-dirty-record'
	exit 2
}

$pinnedNames = @('NA_HELP_ITEMS', 'AION_BOT_DASHBOARD_PORT', 'AION_SIM_PROCESS_KEY')
$prior = @{}
foreach ($name in $pinnedNames) {
	$prior[$name] = [Environment]::GetEnvironmentVariable($name)
	Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue
}
try {
	if (-not $NoBuild) {
		& dotnet build (Join-Path $repoRoot 'tests/Aion.Simulation.Tests') -v quiet *> $null
		if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
	}
	$results = @()
	$recorded = @()
	foreach ($scope in $scopes) {
		$result = [ordered]@{ scope = $scope; verdict = 'fail'; reason = $null; records = $null; sha256 = $null }
		if ($Record) {
			$first = Invoke-ScopeReplay $scope "$Run-$scope-pass1"
			$second = if ($first.failure) { $null } else { Invoke-ScopeReplay $scope "$Run-$scope-pass2" }
			if ($first.failure) { $result.reason = "pass 1: $($first.failure)" }
			elseif ($second.failure) { $result.reason = "pass 2: $($second.failure)" }
			else {
				$one = Get-TraceDigest $first.trace $Ignore
				$two = Get-TraceDigest $second.trace $Ignore
				$result.records = $one.records
				$result.sha256 = $one.sha256
				if ($one.records -eq $two.records -and $one.sha256 -eq $two.sha256) {
					$result.verdict = 'pass'
					$recorded += [pscustomobject]@{ scope = $scope; digest = $one; first = $first.trace; second = $second.trace }
				}
				else {
					# Both traces stay where they are, with the first record that differs.
					$result.reason = 'the two passes differ'
					$result.firstDifference = Get-TraceDifference $first.trace $second.trace $Ignore
				}
			}
		}
		else {
			$row = $rows[$scope]
			$rowIgnore = @($row.ignore)
			$result.baselineRecords = $row.records
			$result.baselineSha256 = $row.sha256
			$result.baselineCommit = $row.commit
			$candidate = Invoke-ScopeReplay $scope "$Run-$scope"
			if ($candidate.failure) { $result.reason = $candidate.failure }
			else {
				$digest = Get-TraceDigest $candidate.trace $rowIgnore
				$result.records = $digest.records
				$result.sha256 = $digest.sha256
				if ($digest.records -eq $row.records -and $digest.sha256 -eq $row.sha256) {
					$result.verdict = 'pass'
					Remove-Item -LiteralPath $candidate.trace
				}
				else {
					$result.reason = 'the trace differs from its baseline'
					$result.candidateTrace = $candidate.trace
					# The recorded trace is under run/, which is not committed. If it is gone, the second copy puts it back.
					$recordedTrace = Join-Path (Join-Path $BaselineRoot $row.commit) "$scope.trace.jsonl"
					$secondCopy = Join-Path (Join-Path $SecondCopyRoot $row.commit) "$scope.trace.jsonl"
					if (-not (Test-Path -LiteralPath $recordedTrace) -and (Test-Path -LiteralPath $secondCopy)) {
						New-Item -ItemType Directory -Force -Path (Split-Path -Parent $recordedTrace) | Out-Null
						Copy-Item -LiteralPath $secondCopy -Destination $recordedTrace
					}
					$result.firstDifference = if (Test-Path -LiteralPath $recordedTrace) { Get-TraceDifference $recordedTrace $candidate.trace $rowIgnore }
						else { "The recorded trace is missing: $recordedTrace" }
				}
			}
		}
		$results += [pscustomobject]$result
	}
	$verdict.scopes = $results
	$passed = @($results | Where-Object { $_.verdict -ne 'pass' }).Count -eq 0
	if ($Record -and $passed) {
		# Every named scope repeated. Keep the traces in both places first, then write the baseline file once: it is under
		# parity-artifacts, and an earlier write would make the next pass of this same run refuse.
		foreach ($entry in $recorded) {
			$counts = Invoke-Python @($compareScript, '--counts', $entry.first, '--json')
			if ($counts.code -ne 0) { throw "compare_traces.py --counts failed for $($entry.first)" }
			foreach ($root in @($BaselineRoot, $SecondCopyRoot)) {
				$folder = Join-Path $root $sha
				New-Item -ItemType Directory -Force -Path $folder | Out-Null
				Copy-Item -LiteralPath $entry.first -Destination (Join-Path $folder "$($entry.scope).trace.jsonl") -Force
			}
			Remove-Item -LiteralPath $entry.first, $entry.second
			$rows[$entry.scope] = [ordered]@{
				scope = $entry.scope; line = $scopeTable[$entry.scope].line; replay = $scopeTable[$entry.scope].replay
				snapshot = $(if ($scopeTable[$entry.scope].replay.Contains('From')) { $scopeTable[$entry.scope].replay.From } else { $null })
				commit = $sha; environment = $pinned; ignore = @($Ignore)
				records = $entry.digest.records; sha256 = $entry.digest.sha256
				counts = ($counts.text | ConvertFrom-Json)
			}
		}
		$document = [ordered]@{ schemaVersion = 1; scopes = @($known | Where-Object { $rows.ContainsKey($_) } | ForEach-Object { $rows[$_] }) }
		$document | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $BaselineFile -Encoding utf8
	}
	Save-Verdict $(if ($passed) { 'pass' } else { 'fail' })
	if (-not $passed) {
		foreach ($failed in @($results | Where-Object { $_.verdict -ne 'pass' })) { Write-Host "  $($failed.scope): $($failed.reason)" }
		exit 1
	}
	exit 0
}
finally {
	foreach ($name in $pinnedNames) {
		if ($null -eq $prior[$name]) { Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue }
		else { [Environment]::SetEnvironmentVariable($name, $prior[$name]) }
	}
}
