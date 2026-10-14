# RestartAll decisions with Docker, git, MySQL and the servers mocked: which images are rebuilt, when a
# deploy is skipped or blocked, and when images roll back. Touches no Docker, git or live state.
#   powershell -NoProfile -ExecutionPolicy Bypass -File docker\test-RestartAll.ps1
$ErrorActionPreference = 'Stop'
$ops = Join-Path ([System.IO.Path]::GetTempPath()) ('restartall-test-' + [guid]::NewGuid().ToString('N'))
. (Join-Path $PSScriptRoot 'RestartAll.ps1') -OpsRoot $ops
New-Item -ItemType Directory -Force -Path $ops | Out-Null
$EnvFile = Join-Path $ops '.env'
Set-Content -LiteralPath $EnvFile -Value 'SERVER_HOST=127.0.0.1'

$checks = 0
$scenario = ''
$calls = @()
$logLines = @()
$fx = $null

# ---- mocks -----------------------------------------------------------------------------------

function Write-Log {
    param([string]$Message, [string]$Level = 'INFO')
    $script:logLines += "$Level $Message"
}
function Wait-Docker { }
function Start-Database { }
function Test-DatabaseRunning { return $true }
function Backup-Databases {
    $script:calls += 'backup'
    if ($script:fx.BackupFails) { throw 'mock backup failure' }
}
function Test-SchemaGate([string]$Commit) {
    $script:calls += "gate $Commit"
    return $script:fx.Gate
}
function Test-ServersHealthy([int]$TimeoutSeconds) {
    $script:calls += 'health'
    $result = $script:fx.Health[$script:fx.HealthIndex]
    $script:fx.HealthIndex++
    return $result
}

# A linear history: a commit is an ancestor of every commit after it in $fx.History.
function Test-FixtureAncestor([string]$Ancestor, [string]$Descendant) {
    $a = [array]::IndexOf($script:fx.History, $Ancestor)
    $d = [array]::IndexOf($script:fx.History, $Descendant)
    return ($a -ge 0 -and $d -ge 0 -and $a -le $d)
}

function Invoke-Git {
    param([string[]]$Arguments, [switch]$AllowFailure, [switch]$Quiet)
    $line = $Arguments -join ' '
    $script:calls += "git $line"
    $out = @()
    $code = 0
    switch -Regex ($line) {
        '^rev-parse --abbrev-ref HEAD$' { $out = @($script:fx.Branch); break }
        '^status --porcelain$' { $out = @($script:fx.Dirty); break }
        '^rev-parse HEAD$' { $out = @($script:fx.Head); break }
        '^rev-parse origin/main$' { $out = @($script:fx.Upstream); break }
        '^merge-base --is-ancestor (\S+) (\S+)$' { if (-not (Test-FixtureAncestor $Matches[1] $Matches[2])) { $code = 1 }; break }
        '^rev-list --count' { $out = @('1'); break }
        '^merge --ff-only' { $script:fx.Head = $script:fx.Upstream; break }
        '^diff --name-only' { $out = @($script:fx.Changed); break }
    }
    if ($code -ne 0 -and -not $AllowFailure) { throw "git $line failed" }
    return [pscustomobject]@{ ExitCode = $code; Lines = $out; Text = ($out -join "`n") }
}

function Invoke-Docker {
    param([string[]]$Arguments, [switch]$AllowFailure, [switch]$Quiet)
    $line = $Arguments -join ' '
    $script:calls += "docker $line"
    $code = 0
    if ($line -match ' plain build ') { $code = $script:fx.BuildExit }
    if ($code -ne 0 -and -not $AllowFailure) { throw "docker $line failed" }
    return [pscustomobject]@{ ExitCode = $code; Lines = @(); Text = '' }
}

# ---- harness ---------------------------------------------------------------------------------

function Assert-That([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        Write-Host "Calls in '$script:scenario':`n  $($script:calls -join "`n  ")"
        throw "FAILED [$script:scenario]: $Message"
    }
    $script:checks++
}
function Test-Call([string]$Pattern) { return @($script:calls | Where-Object { $_ -match $Pattern }).Count -gt 0 }
function Get-CallCount([string]$Pattern) { return @($script:calls | Where-Object { $_ -match $Pattern }).Count }
function Get-StateCommit([string]$Server) { return (Read-DeployState)[$Server].commit }

function Invoke-Scenario {
    param([string]$Name, [hashtable]$Fixture = @{}, [string]$StateCommit = 'h1', [switch]$Plan, [switch]$Restart, [switch]$Force)
    $script:scenario = $Name
    $script:calls = @()
    $script:logLines = @()
    $script:fx = @{
        Branch = 'main'; Dirty = @(); History = @('h1', 'h2'); Head = 'h1'; Upstream = 'h1'; Changed = @()
        Gate = $true; Health = @($true); HealthIndex = 0; BuildExit = 0; BackupFails = $false
    }
    foreach ($key in $Fixture.Keys) { $script:fx[$key] = $Fixture[$key] }
    Remove-Item -LiteralPath $StatePath -ErrorAction SilentlyContinue
    if ($StateCommit) { Save-DeployState -Deployed $Servers -Commit $StateCommit }
    $script:PlanOnly = [bool]$Plan
    $script:RestartOnly = [bool]$Restart
    $script:ForceRebuild = [bool]$Force
    Invoke-RestartAll
}

try {
    # ---- which paths feed which image ---------------------------------------------------------
    $feeds = [ordered]@{
        'src/Aion.GameServer/Services/QuestService.cs' = 'gameserver'
        'src/Aion.LoginServer/Program.cs'              = 'loginserver'
        'src/Aion.ChatServer/Program.cs'               = 'chatserver'
        'src/Aion.Commons/Network/Packet.cs'           = 'loginserver chatserver gameserver'
        'src/Directory.Build.props'                    = 'loginserver chatserver gameserver'
        '.dockerignore'                                = 'loginserver chatserver gameserver'
        'game-server/data/static_data/npcs.xml'        = 'gameserver'
        'game-server/config/main/custom.properties'    = 'gameserver'
        'login-server/config/network.properties'       = 'loginserver'
        'chat-server/config/chatserver.properties'     = 'chatserver'
        'docker/gameserver/entrypoint.sh'              = 'gameserver'
        'docker/loginserver/Dockerfile'                = 'loginserver'
        'docker/docker-compose.yml'                    = ''
        'docker/RestartAll.ps1'                        = ''
        'game-server/sql/aion_gs.sql'                  = ''
        'docs/natural-ishalgen-status.md'              = ''
        'src-other/readme.md'                          = ''
    }
    $scenario = 'path mapping'
    foreach ($path in $feeds.Keys) {
        $actual = (@($Servers | Where-Object { Test-PathFeedsServer $path $_ }) -join ' ')
        Assert-That ($actual -ceq $feeds[$path]) "$path feeds '$actual', expected '$($feeds[$path])'"
    }

    # ---- nightly runs -------------------------------------------------------------------------
    Invoke-Scenario 'up to date'
    Assert-That ($ExitCode -eq 0) "exit code $ExitCode"
    Assert-That (-not (Test-Call 'plain build .*server')) 'built although nothing changed'
    # The companion host: built from the tree the servers run from, and started after their health check.
    Assert-That (Test-Call 'plain build companionhost$') 'companion host not built from the deployed tree'
    Assert-That (Test-Call 'plain up -d --no-deps --no-build companionhost$') 'companion host not started'
    Assert-That ([array]::IndexOf($calls, 'health') -lt [array]::IndexOf($calls, ($calls | Where-Object { $_ -match 'plain build companionhost' } | Select-Object -First 1))) 'companion host handled before the servers were healthy'
    Assert-That (-not (Test-Call '^git merge --ff-only')) 'merged although up to date'
    Assert-That (Test-Call '^gate h1$') 'schema gate did not run on HEAD'
    Assert-That (Test-Call '^backup$') 'no backup'
    Assert-That (Test-Call 'plain stop gameserver chatserver loginserver$') 'servers not stopped game-first'
    Assert-That (Test-Call 'plain up -d loginserver chatserver gameserver$') 'servers not started'
    Assert-That ([array]::IndexOf($calls, 'backup') -lt [array]::IndexOf($calls, ($calls | Where-Object { $_ -match 'plain stop' } | Select-Object -First 1))) 'backup ran after the stop'

    Invoke-Scenario 'game change on origin' @{ Upstream = 'h2'; Changed = @('src/Aion.GameServer/A.cs', 'docs/x.md') }
    Assert-That ($ExitCode -eq 0) "exit code $ExitCode"
    Assert-That (Test-Call '^git merge --ff-only origin/main$') 'did not fast-forward'
    Assert-That (Test-Call 'plain build gameserver$') 'gameserver not built alone'
    Assert-That (Test-Call '^docker tag aion-gameserver:latest aion-gameserver:previous$') 'current game image not kept as :previous'
    Assert-That (-not (Test-Call 'tag aion-loginserver')) 'login image touched'
    Assert-That (Test-Call '^gate h2$') 'schema gate did not run on the new commit'
    Assert-That ((Get-StateCommit 'gameserver') -eq 'h2' -and (Get-StateCommit 'loginserver') -eq 'h1') 'deploy record wrong'
    $gateAt = [array]::IndexOf($calls, 'gate h2')
    $buildAt = [array]::IndexOf($calls, ($calls | Where-Object { $_ -match 'plain build' } | Select-Object -First 1))
    Assert-That ($gateAt -lt $buildAt) 'built before the schema gate passed'

    Invoke-Scenario 'shared library change' @{ Upstream = 'h2'; Changed = @('src/Aion.Commons/B.cs') }
    Assert-That (Test-Call 'plain build loginserver chatserver gameserver$') 'shared change did not rebuild all three'

    Invoke-Scenario 'no deploy record' -StateCommit ''
    Assert-That (Test-Call 'plain build loginserver chatserver gameserver$') 'missing record did not rebuild all three'
    Assert-That ((Get-StateCommit 'chatserver') -eq 'h1') 'record not written'

    Invoke-Scenario 'force rebuild' -Force
    Assert-That (Test-Call 'plain build loginserver chatserver gameserver$') 'force did not rebuild all three'

    # ---- skipped updates (exit 4) ---------------------------------------------------------------
    Invoke-Scenario 'uncommitted changes' @{ Upstream = 'h2'; Changed = @('src/Aion.GameServer/A.cs'); Dirty = @(' M src/Aion.GameServer/A.cs') }
    Assert-That ($ExitCode -eq 4) "exit code $ExitCode"
    Assert-That (-not (Test-Call '^git fetch') -and -not (Test-Call '^git merge --ff-only')) 'touched git with a dirty tree'
    Assert-That (-not (Test-Call 'plain build')) 'built a dirty tree'
    Assert-That (Test-Call 'plain up -d') 'did not restart'
    Assert-That (Test-Call 'plain up -d --no-deps --no-build companionhost$') 'companion host not started on its current image'

    Invoke-Scenario 'not on main' @{ Branch = 'feature/x'; Upstream = 'h2'; Changed = @('src/Aion.GameServer/A.cs') }
    Assert-That ($ExitCode -eq 4) "exit code $ExitCode"
    Assert-That (-not (Test-Call 'plain build')) 'built another branch'

    Invoke-Scenario 'diverged' @{ Upstream = 'x9'; Changed = @('src/Aion.GameServer/A.cs') }
    Assert-That ($ExitCode -eq 4) "exit code $ExitCode"
    Assert-That (-not (Test-Call '^git merge --ff-only') -and -not (Test-Call 'plain build')) 'deployed a diverged main'

    # ---- blocked and failed deploys -------------------------------------------------------------
    Invoke-Scenario 'schema drift' @{ Upstream = 'h2'; Changed = @('src/Aion.GameServer/A.cs'); Gate = $false }
    Assert-That ($ExitCode -eq 2) "exit code $ExitCode"
    Assert-That (-not (Test-Call 'plain build')) 'built despite schema drift'
    Assert-That (Test-Call 'plain up -d') 'did not restart'
    Assert-That ((Get-StateCommit 'gameserver') -eq 'h1') 'recorded a blocked deploy'

    Invoke-Scenario 'unhealthy new image' @{ Upstream = 'h2'; Changed = @('src/Aion.GameServer/A.cs'); Health = @($false, $true) }
    Assert-That ($ExitCode -eq 1) "exit code $ExitCode"
    Assert-That (Test-Call '^docker tag aion-gameserver:previous aion-gameserver:latest$') 'did not roll back'
    Assert-That ((Get-CallCount 'plain up -d loginserver') -eq 2) 'did not restart on the rolled-back image'
    Assert-That (-not (Test-Call 'plain build companionhost')) 'companion host built from a tree that was rolled back'
    Assert-That (Test-Call 'plain up -d --no-deps --no-build companionhost$') 'companion host not started after the rollback'
    Assert-That ((Get-StateCommit 'gameserver') -eq 'h1') 'recorded a rolled-back deploy'

    Invoke-Scenario 'build failure' @{ Upstream = 'h2'; Changed = @('src/Aion.GameServer/A.cs'); BuildExit = 1 }
    Assert-That ($ExitCode -eq 1) "exit code $ExitCode"
    Assert-That (Test-Call '^docker tag aion-gameserver:previous aion-gameserver:latest$') 'did not restore the previous image'
    Assert-That (Test-Call 'plain up -d') 'did not restart on the current images'
    Assert-That ((Get-StateCommit 'gameserver') -eq 'h1') 'recorded a failed build'
    Assert-That (-not (Test-Call 'plain build companionhost')) 'companion host built although the servers were not'

    Invoke-Scenario 'backup failure' @{ Upstream = 'h2'; Changed = @('src/Aion.GameServer/A.cs'); BackupFails = $true }
    Assert-That ($ExitCode -eq 1) "exit code $ExitCode"
    Assert-That (-not (Test-Call 'plain stop') -and -not (Test-Call 'plain build')) 'continued without a backup'

    Invoke-Scenario 'unhealthy without a deploy' @{ Health = @($false) }
    Assert-That ($ExitCode -eq 1) "exit code $ExitCode"
    Assert-That (-not (Test-Call 'tag .*:previous .*:latest')) 'rolled back with nothing deployed'
    Assert-That (-not (Test-Call 'companionhost')) 'companion host touched while the servers were unhealthy'

    # ---- other modes ----------------------------------------------------------------------------
    Invoke-Scenario 'restart only' @{ Upstream = 'h2'; Changed = @('src/Aion.GameServer/A.cs') } -Restart
    Assert-That ($ExitCode -eq 0) "exit code $ExitCode"
    Assert-That (-not (Test-Call '^git ') -and -not (Test-Call '^gate')) 'restart-only touched git or ran the gate'
    Assert-That ((Test-Call '^backup$') -and (Test-Call 'plain up -d')) 'restart-only did not back up and restart'
    Assert-That ((Test-Call 'plain up -d --no-deps --no-build companionhost$') -and -not (Test-Call 'plain build')) 'restart-only did not start the companion host on its current image'

    Invoke-Scenario 'plan only' @{ Upstream = 'h2'; Changed = @('src/Aion.GameServer/A.cs') } -Plan
    Assert-That ($ExitCode -eq 0) "exit code $ExitCode"
    Assert-That (-not (Test-Call '^git merge --ff-only') -and -not (Test-Call 'plain (build|stop|up)') -and -not (Test-Call '^backup$')) 'plan-only changed something'
    Assert-That (Test-Call '^gate h2$') 'plan-only did not check the schema of the commit it would deploy'
    Assert-That ((Get-StateCommit 'gameserver') -eq 'h1') 'plan-only recorded a deploy'

    Write-Host "test-RestartAll: $checks checks passed."
} finally {
    Remove-Item -LiteralPath $ops -Recurse -Force -ErrorAction SilentlyContinue
}
