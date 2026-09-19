[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
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
        New-Item -ItemType Directory -Path (Join-Path $testRoot "child-$index") | Out-Null
    }
    $script:runPath = Join-Path $testRoot 'child-39'
    $FullRun = $true
    & $retention
    if (@(Get-ChildItem -LiteralPath $testRoot -Directory).Count -ne 40) {
        throw 'Full-run children pruned sibling scenario evidence.'
    }
    $FullRun = $false
    & $retention
    if (@(Get-ChildItem -LiteralPath $testRoot -Directory).Count -ne 20 -or -not (Test-Path -LiteralPath $script:runPath)) {
        throw 'Standalone retention must keep the current run plus nineteen other runs.'
    }
    Write-Host 'LIVE retention passed: full matrices retain every child; standalone runs retain twenty.'
}
finally {
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if ([IO.Path]::GetFullPath((Split-Path $testRoot -Parent)).TrimEnd([IO.Path]::DirectorySeparatorChar) -ne $tempRoot -or
        (Split-Path $testRoot -Leaf) -notmatch '^aion-retention-test-[a-f0-9]{32}$') {
        throw "Unsafe retention-test cleanup target: $testRoot"
    }
    Remove-Item -LiteralPath $testRoot -Recurse -Force
}
