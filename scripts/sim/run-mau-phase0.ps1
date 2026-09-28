param(
    [Parameter(Mandatory = $true)][ValidateSet('GeneratorToRae', 'RaeToHatata')][string]$Course,
    [Parameter(Mandatory = $true)][string]$Seeds,
    [Parameter(Mandatory = $true)][ValidatePattern('^[a-z0-9-]+$')][string]$Tag
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$project = Join-Path $repoRoot 'tests/Aion.Simulation.Tests/Aion.Simulation.Tests.csproj'
$outputRoot = Join-Path $repoRoot 'run/bot-learning-phase0/current'
$summaryScript = Join-Path $repoRoot 'scripts/sim/trace/summarize_mau_course.py'
$courseSlug = if ($Course -eq 'GeneratorToRae') { 'generator' } else { 'hatata' }
$seedList = @($Seeds.Split(',') | ForEach-Object {
    $parsed = 0
    if (-not [int]::TryParse($_.Trim(), [ref]$parsed) -or $parsed -le 0) { throw "Invalid seed: $_" }
    $parsed
})
if ($seedList.Count -ne ($seedList | Select-Object -Unique).Count) { throw 'Duplicate seeds are not allowed.' }
$lock = Get-Content -LiteralPath (Join-Path $repoRoot 'docs/bot-learning-phase0-lock.json') -Raw | ConvertFrom-Json
$reservedSeeds = @($lock.seedSplit.regression) + @($lock.seedSplit.development) + @($lock.seedSplit.evaluation)
foreach ($seed in $seedList) {
    if ($seed -notin $reservedSeeds) { throw "Seed $seed is outside the frozen Phase 0 split." }
}
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

$results = @()
foreach ($seed in $seedList) {
    $run = "phase0-$courseSlug-s$seed-$Tag"
    $directory = Join-Path $outputRoot $run
    if (Test-Path -LiteralPath $directory) { throw "Run directory already exists: $directory" }
    New-Item -ItemType Directory -Path $directory | Out-Null
    $log = Join-Path $directory 'test.log'
    $env:AION_SIM_DB_INTEGRATION = '1'
    $env:AION_SIM_SEED = [string]$seed
    $env:AION_MAU_COURSE = $Course
    $env:AION_SIM_RUN_ID = $run
    $env:AION_MAU_TRACE_DIR = $directory
    Write-Host "RUN $run (dashboard http://127.0.0.1:17880/ while active)"
    & dotnet test $project --no-build --filter 'FullyQualifiedName~NaturalMauCourseUsesFrozenPriestAndCurrentPolicy' --logger 'console;verbosity=minimal' *> $log
    $testExit = $LASTEXITCODE
    $summaryStatus = 'missing-trace'
    if (Test-Path -LiteralPath (Join-Path $directory "$run.trace.jsonl")) {
        try {
            & python $summaryScript $directory
            if ($LASTEXITCODE -eq 0) {
                $summaryStatus = (Get-Content -LiteralPath (Join-Path $directory 'summary.json') -Raw | ConvertFrom-Json).status
            } else { $summaryStatus = 'summary-error' }
        } catch { $summaryStatus = 'summary-error'; Write-Warning $_ }
    }
    $results += [pscustomobject]@{ run = $run; course = $Course; seed = $seed; testExit = $testExit; status = $summaryStatus; log = $log }
    $results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $outputRoot "batch-$courseSlug-$Tag.json") -Encoding utf8
    Write-Host "$run testExit=$testExit status=$summaryStatus"
}
