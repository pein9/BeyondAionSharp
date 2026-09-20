[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '../e2e/run-artifact-owner.ps1')
$runner = Join-Path $PSScriptRoot 'run-live.ps1'
$tokens = $null
$parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($runner, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -ne 0) { throw 'LIVE runner did not parse.' }
$function = $ast.Find({ param($node)
    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Remove-OldRuns'
}, $true)
if ($null -eq $function) { throw 'LIVE retention function not found.' }
$retention = $function.Body.GetScriptBlock()
$testRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) ('aion-retention-test-' + [Guid]::NewGuid().ToString('N'))))
New-Item -ItemType Directory -Path $testRoot | Out-Null
try {
    $script:runRootPath = $testRoot
    foreach ($index in 0..39) {
        $child = Join-Path $testRoot "child-$index"
        New-Item -ItemType Directory -Path $child | Out-Null
        [pscustomobject]@{ schemaVersion = 1; machine = [Environment]::MachineName;
            processId = [int]::MaxValue; processStartUtcTicks = 1 } |
            ConvertTo-Json | Set-Content -LiteralPath (Join-Path $child 'run-owner.json')
    }
    foreach ($name in @('active', 'legacy', 'foreign')) {
        $child = Join-Path $testRoot $name
        New-Item -ItemType Directory -Path $child | Out-Null
        if ($name -ne 'legacy') { Register-AionRunArtifactOwner -Directory $child }
        if ($name -eq 'foreign') {
            $record = Get-Content -LiteralPath (Join-Path $child 'run-owner.json') -Raw | ConvertFrom-Json
            $record.machine = 'another-host'
            $record | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $child 'run-owner.json')
        }
        (Get-Item -LiteralPath $child).LastWriteTimeUtc = [DateTime]::UnixEpoch
    }
    if (Test-AionRunArtifactOwnerExited -Directory (Join-Path $testRoot 'active')) { throw 'Live process identity was treated as exited.' }
    $activeRecord = Join-Path $testRoot 'active/run-owner.json'
    $originalRecord = Get-Content -LiteralPath $activeRecord -Raw
    $refusedOverwrite = $false
    try { Register-AionRunArtifactOwner -Directory (Join-Path $testRoot 'active') }
    catch { $refusedOverwrite = $true }
    if (-not $refusedOverwrite -or (Get-Content -LiteralPath $activeRecord -Raw) -cne $originalRecord) {
        throw 'Owner registration must refuse to replace existing provenance.'
    }
    $legacyRecord = Join-Path $testRoot 'legacy/run-owner.json'
    'not-json' | Set-Content -LiteralPath $legacyRecord
    if (Test-AionRunArtifactOwnerExited -Directory (Join-Path $testRoot 'legacy')) { throw 'Malformed owner was treated as exited.' }
    Remove-Item -LiteralPath $legacyRecord
    if (-not (Test-AionRunArtifactOwnerExited -Directory (Join-Path $testRoot 'child-0'))) { throw 'Absent process was not recognized.' }
    # A reused PID does not keep old artifacts alive. Compare process creation time, not just the numeric PID.
    $reused = Join-Path $testRoot 'child-0/run-owner.json'
    $record = Get-Content -LiteralPath $reused -Raw | ConvertFrom-Json
    $record.processId = $PID
    $record | ConvertTo-Json | Set-Content -LiteralPath $reused
    if (-not (Test-AionRunArtifactOwnerExited -Directory (Join-Path $testRoot 'child-0'))) { throw 'PID reuse was treated as the same owner.' }
    $script:runPath = Join-Path $testRoot 'child-39'
    $FullRun = $true
    & $retention
    if (@(Get-ChildItem -LiteralPath $testRoot -Directory).Count -ne 43) {
        throw 'Full-run children pruned sibling scenario evidence.'
    }
    $FullRun = $false
    & $retention
    if (@(Get-ChildItem -LiteralPath $testRoot -Directory).Count -ne 23 -or -not (Test-Path -LiteralPath $script:runPath)) {
        throw 'Standalone retention must keep active/unknown owners plus the current run and nineteen completed runs.'
    }
    foreach ($name in @('active', 'legacy', 'foreign')) {
        if (-not (Test-Path -LiteralPath (Join-Path $testRoot $name))) { throw "Retention removed protected $name artifacts." }
    }
    Write-Host 'LIVE retention passed: Full siblings, active/unknown owners, PID reuse and completed-run limit.'
}
finally {
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if ([IO.Path]::GetFullPath((Split-Path $testRoot -Parent)).TrimEnd([IO.Path]::DirectorySeparatorChar) -ne $tempRoot -or
        (Split-Path $testRoot -Leaf) -notmatch '^aion-retention-test-[a-f0-9]{32}$') {
        throw "Unsafe retention-test cleanup target: $testRoot"
    }
    Remove-Item -LiteralPath $testRoot -Recurse -Force
}
