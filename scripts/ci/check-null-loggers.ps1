[CmdletBinding()]
param(
    [string]$SourceRoot = "src"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$sourcePath = (Resolve-Path (Join-Path $repoRoot $SourceRoot)).Path
$bridgePath = [IO.Path]::GetFullPath((Join-Path $repoRoot "src\Aion.Commons\Logging\AionLog.cs"))
$pattern = '\bNullLogger(?:Factory)?\b'

$violations = @(
    Get-ChildItem -LiteralPath $sourcePath -Recurse -File -Filter "*.cs" |
        Where-Object {
            $fullPath = [IO.Path]::GetFullPath($_.FullName)
            $fullPath -ne $bridgePath -and
            $fullPath -notmatch '[\\/](?:bin|obj)[\\/]'
        } |
        Select-String -Pattern $pattern
)

if ($violations.Count -gt 0) {
    Write-Error ("Null logger ratchet failed. Replace source NullLogger/NullLoggerFactory uses with AionLog.For(...):`n" +
        (($violations | ForEach-Object {
            $relativePath = [IO.Path]::GetRelativePath($repoRoot, $_.Path)
            "  ${relativePath}:$($_.LineNumber): $($_.Line.Trim())"
        }) -join "`n"))
}

Write-Host "Null logger ratchet passed: no NullLogger or NullLoggerFactory uses outside AionLog.cs."
