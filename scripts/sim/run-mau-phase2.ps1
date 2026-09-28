param(
    [Parameter(Mandatory = $true)][ValidateSet('GeneratorToRae', 'RaeToHatata')][string]$Course,
    [Parameter(Mandatory = $true)][string]$Seeds,
    [Parameter(Mandatory = $true)][ValidateSet('development', 'regression', 'evaluation')][string]$Split,
    [Parameter(Mandatory = $true)][ValidateRange(1, 2)][int]$Repeats,
    [ValidateSet('IsolatedStalker', 'TwoAttackerPull', 'MovingPatrol', 'BlockedGeneratorRejoin',
        'HatataAlone', 'HatataWithAdd')][string]$Encounter,
    [string]$PolicyFile,
    [Parameter(Mandatory = $true)][ValidatePattern('^[a-z0-9-]{1,16}$')][string]$Tag
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$project = Join-Path $repoRoot 'tests/Aion.Simulation.Tests/Aion.Simulation.Tests.csproj'
$outputRoot = Join-Path $repoRoot 'run/bot-learning-phase2/current'
$summaryScript = Join-Path $repoRoot 'scripts/sim/trace/summarize_mau_course.py'
$decisionScript = Join-Path $repoRoot 'scripts/sim/trace/make_mau_decisions.py'
$courseSlug = if ($Course -eq 'GeneratorToRae') { 'gen' } else { 'hat' }
$encounterSlug = switch ($Encounter) {
    'IsolatedStalker' { 'stalker' }
    'TwoAttackerPull' { 'twoadd' }
    'MovingPatrol' { 'patrol' }
    'BlockedGeneratorRejoin' { 'rejoin' }
    'HatataAlone' { 'hatata1' }
    'HatataWithAdd' { 'hatata2' }
    default { 'full' }
}
if ($Encounter -and (($Encounter -eq 'BlockedGeneratorRejoin') -ne ($Course -eq 'GeneratorToRae'))) {
    throw 'BlockedGeneratorRejoin requires GeneratorToRae; other short encounters require RaeToHatata.'
}
$policyPath = if ($PolicyFile) { (Resolve-Path -LiteralPath $PolicyFile).Path } else { $null }
$policySlug = if ($policyPath) { 'cand' } else { 'base' }
$lock = Get-Content -LiteralPath (Join-Path $repoRoot 'docs/bot-learning-phase0-lock.json') -Raw | ConvertFrom-Json
$allowed = @($lock.seedSplit.$Split)
$seedList = @($Seeds.Split(',') | ForEach-Object {
    $parsed = 0
    if (-not [int]::TryParse($_.Trim(), [ref]$parsed) -or $parsed -le 0) { throw "Invalid seed: $_" }
    $parsed
})
if ($seedList.Count -ne ($seedList | Select-Object -Unique).Count) { throw 'Duplicate seeds are not allowed.' }
foreach ($seed in $seedList) {
    if ($seed -notin $allowed) { throw "Seed $seed is outside the $Split split." }
}
if ($Split -eq 'evaluation') {
    $freezePath = Join-Path $repoRoot 'docs/bot-learning-phase2-frozen.json'
    if (-not (Test-Path -LiteralPath $freezePath)) { throw 'Evaluation stays sealed until the candidate freeze exists.' }
    $freeze = Get-Content -LiteralPath $freezePath -Raw | ConvertFrom-Json
    if ($freeze.status -ne 'frozen') { throw 'The Phase 2 candidate is not frozen.' }
    if ($policyPath) {
        $actual = (Get-FileHash -LiteralPath $policyPath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -ne $freeze.policySha256) { throw 'Evaluation candidate differs from the frozen policy.' }
    }
    foreach ($source in $freeze.sourceHashes.PSObject.Properties) {
        $file = Join-Path $repoRoot $source.Name
        if (-not (Test-Path -LiteralPath $file)) { throw "Frozen evaluation source is missing: $($source.Name)" }
        $actual = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -ne $source.Value) { throw "Frozen evaluation source changed: $($source.Name)" }
    }
}
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

$results = @()
foreach ($seed in $seedList) {
    for ($repeat = 1; $repeat -le $Repeats; $repeat++) {
        $run = 'p2-{0}-{1}-{2}-s{3}-r{4:d2}-{5}' -f $courseSlug, $encounterSlug, $policySlug, $seed, $repeat, $Tag
        if ($run.Length -gt 58) { throw "Run ID is too long for the SIM database: $run" }
        $directory = Join-Path $outputRoot $run
        if (Test-Path -LiteralPath $directory) { throw "Run directory already exists: $directory" }
        New-Item -ItemType Directory -Path $directory | Out-Null
        $log = Join-Path $directory 'test.log'
        $env:AION_SIM_DB_INTEGRATION = '1'
        $env:AION_SIM_SEED = [string]$seed
        $env:AION_MAU_COURSE = $Course
        $env:AION_MAU_ENCOUNTER = $Encounter
        $env:AION_MAU_POLICY_FILE = $policyPath
        $env:AION_SIM_RUN_ID = $run
        $env:AION_MAU_TRACE_DIR = $directory
        Write-Host "RUN $run (dashboard http://127.0.0.1:17880/ while active)"
        & dotnet test $project --no-build --filter 'FullyQualifiedName~NaturalMauCourseUsesFrozenPriestAndCurrentPolicy' --logger 'console;verbosity=minimal' *> $log
        $testExit = $LASTEXITCODE
        $status = 'missing-trace'
        if (Test-Path -LiteralPath (Join-Path $directory "$run.trace.jsonl")) {
            & python $summaryScript $directory
            if ($LASTEXITCODE -eq 0) {
                & python $decisionScript $directory
                if ($LASTEXITCODE -eq 0) {
                    $status = (Get-Content -LiteralPath (Join-Path $directory 'summary.json') -Raw | ConvertFrom-Json).status
                    if ($Split -eq 'evaluation') {
                        $actualModule = (Get-Content -LiteralPath (Join-Path $directory 'summary.json') -Raw | ConvertFrom-Json).moduleId
                        if ($actualModule -ne $freeze.moduleId) { throw "Evaluation module differs from freeze: $actualModule" }
                    }
                } else { $status = 'decision-error' }
            } else { $status = 'summary-error' }
        }
        $results += [pscustomobject]@{ run = $run; course = $Course; encounter = $Encounter;
            seed = $seed; split = $Split; policy = $policySlug; repeat = $repeat;
            testExit = $testExit; status = $status; log = $log }
        $results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $outputRoot "batch-$courseSlug-$encounterSlug-$policySlug-$Tag.json") -Encoding utf8
        Write-Host "$run testExit=$testExit status=$status"
    }
}
