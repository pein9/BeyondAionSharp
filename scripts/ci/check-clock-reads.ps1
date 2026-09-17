[CmdletBinding()]
param(
    [string]$SourceRoot = "src/Aion.GameServer",
    [string]$BaselinePath = "scripts/ci/clock-read-baseline.json",
    [switch]$UpdateBaseline
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../.."))

function Resolve-RepositoryPath([string]$Path)
{
    if ([IO.Path]::IsPathRooted($Path))
    {
        return [IO.Path]::GetFullPath($Path)
    }

    return [IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
}

$sourcePath = Resolve-RepositoryPath $SourceRoot
$resolvedBaselinePath = Resolve-RepositoryPath $BaselinePath
if (-not (Test-Path -LiteralPath $sourcePath -PathType Container))
{
    throw "Clock-read source root was not found: $sourcePath"
}

# These are direct wall/monotonic clock reads used by game-server source today. P4-04 routes gameplay
# time through SystemClock; genuine infrastructure reads remain in the baseline until the analyzer
# allowlist replaces this ratchet at the documented floor.
$patterns = [ordered]@{
    "DateTimeOffset.UtcNow"    = '\bDateTimeOffset\s*\.\s*UtcNow\b'
    "DateTimeOffset.Now"       = '\bDateTimeOffset\s*\.\s*Now\b'
    "DateTime.UtcNow"          = '\bDateTime\s*\.\s*UtcNow\b'
    "DateTime.Now"             = '\bDateTime\s*\.\s*Now\b'
    "DateTime.Today"           = '\bDateTime\s*\.\s*Today\b'
    "Environment.TickCount64"  = '\bEnvironment\s*\.\s*TickCount64\b'
    "Environment.TickCount"    = '\bEnvironment\s*\.\s*TickCount(?!64)\b'
    "Stopwatch.GetTimestamp"   = '\bStopwatch\s*\.\s*GetTimestamp\b'
    "Stopwatch.StartNew"       = '\bStopwatch\s*\.\s*StartNew\b'
}

$files = @(
    Get-ChildItem -LiteralPath $sourcePath -Recurse -File -Filter "*.cs" |
        Where-Object { $_.FullName -notmatch '[\\/](?:bin|obj)[\\/]' }
)
$sources = @($files | ForEach-Object {
    [pscustomobject]@{
        Path = $_.FullName
        Text = [IO.File]::ReadAllText($_.FullName)
    }
})

$currentCounts = [ordered]@{}
$matchedFiles = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$currentTotal = 0
foreach ($entry in $patterns.GetEnumerator())
{
    $count = 0
    $regex = [regex]::new($entry.Value)
    foreach ($source in $sources)
    {
        $matches = $regex.Matches($source.Text)
        $count += $matches.Count
        if ($matches.Count -gt 0)
        {
            $null = $matchedFiles.Add($source.Path)
        }
    }
    $currentCounts[$entry.Key] = $count
    $currentTotal += $count
}

Write-Host "Clock-read inventory: $currentTotal direct reads across $($matchedFiles.Count) files."
foreach ($name in $currentCounts.Keys)
{
    Write-Host ("  {0,-28} {1,5}" -f $name, $currentCounts[$name])
}

if ($UpdateBaseline)
{
    $baseline = [ordered]@{
        schemaVersion = 1
        sourceRoot = $SourceRoot
        total = $currentTotal
        files = $matchedFiles.Count
        apis = $currentCounts
    }
    [IO.Directory]::CreateDirectory((Split-Path -Parent $resolvedBaselinePath)) | Out-Null
    $json = $baseline | ConvertTo-Json -Depth 4
    [IO.File]::WriteAllText(
        $resolvedBaselinePath,
        $json + [Environment]::NewLine,
        [Text.UTF8Encoding]::new($false))
    Write-Host "Updated clock-read baseline: $resolvedBaselinePath"
    exit 0
}

if (-not (Test-Path -LiteralPath $resolvedBaselinePath -PathType Leaf))
{
    throw "Clock-read baseline was not found: $resolvedBaselinePath"
}

$baseline = Get-Content -LiteralPath $resolvedBaselinePath -Raw | ConvertFrom-Json
if ($baseline.schemaVersion -ne 1)
{
    throw "Unsupported clock-read baseline schema version: $($baseline.schemaVersion)"
}
if ([string]$baseline.sourceRoot -ne $SourceRoot)
{
    throw "Clock-read baseline covers '$($baseline.sourceRoot)', but the check requested '$SourceRoot'."
}

$violations = [Collections.Generic.List[string]]::new()
foreach ($name in $currentCounts.Keys)
{
    $property = $baseline.apis.PSObject.Properties[$name]
    if ($null -eq $property)
    {
        $violations.Add("Clock-read baseline is missing API '$name'.")
        continue
    }

    $allowed = [int]$property.Value
    $actual = [int]$currentCounts[$name]
    if ($actual -gt $allowed)
    {
        $violations.Add("Direct clock reads increased for ${name}: $allowed -> $actual.")
    }
}

$allowedTotal = [int]$baseline.total
if ($currentTotal -gt $allowedTotal)
{
    $violations.Add("Total direct clock reads increased: $allowedTotal -> $currentTotal.")
}

if ($violations.Count -gt 0)
{
    foreach ($violation in $violations)
    {
        [Console]::Error.WriteLine($violation)
    }
    exit 1
}

if ($currentTotal -lt $allowedTotal)
{
    Write-Host "Clock-read debt decreased; update the checked-in baseline after reviewing the migration: $allowedTotal -> $currentTotal."
}
Write-Host "Clock-read ratchet passed."
