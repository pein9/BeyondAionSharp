[CmdletBinding()]
param(
    [string]$ExpectedPath = "parity-artifacts/e2e/custom-quest-handler-drafts.json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../.."))
$expected = [IO.Path]::GetFullPath((Join-Path $repoRoot $ExpectedPath))
if (-not (Test-Path -LiteralPath $expected -PathType Leaf))
{
    throw "Custom quest draft baseline was not found: $expected"
}

$temporary = Join-Path ([IO.Path]::GetTempPath()) ("custom-quest-drafts-{0}.json" -f [Guid]::NewGuid().ToString("N"))
try
{
    & dotnet run --project (Join-Path $repoRoot "tools/Aion.QuestPlanExtractor/Aion.QuestPlanExtractor.csproj") `
        --no-restore -- `
        --repo $repoRoot `
        --output $temporary
    if ($LASTEXITCODE -ne 0)
    {
        throw "Custom quest draft extraction failed with exit code $LASTEXITCODE."
    }

    $expectedHash = (Get-FileHash -LiteralPath $expected -Algorithm SHA256).Hash
    $actualHash = (Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash
    if ($expectedHash -ne $actualHash)
    {
        throw "Custom quest handler drafts drifted. Regenerate $ExpectedPath with tools/Aion.QuestPlanExtractor."
    }
    Write-Host "Custom quest handler drafts match the Roslyn extraction baseline."
}
finally
{
    Remove-Item -LiteralPath $temporary -Force -ErrorAction SilentlyContinue
}
