<#
.SYNOPSIS
    Nightly restart of the live Aion Docker stack, deploying new commits on main when it is safe to.

.DESCRIPTION
    Run by the "Aion RestartAll" scheduled task (docker\Register-RestartAllTask.ps1) at 3 AM, or by hand.

      1. Waits for Docker and makes sure MySQL is running. This script never restarts MySQL.
      2. Fast-forwards this checkout to origin/main, but only when it is on main with no modified
         or untracked files. Docker builds from the files on disk, so uncommitted work would
         otherwise ship to the live server. If not, the update is skipped (exit code 4).
         An update that changes this script hands the rest of the run to the new copy.
      3. Picks the server images to rebuild by diffing the paths each image is built from
         between the commit it was last deployed from and the new HEAD.
      4. Schema gate: loads the repo's schema files into a throwaway MySQL and compares them with
         the live databases. On any difference no new code is deployed (exit code 2) until the
         migration has been applied by hand. The servers still restart.
      5. Backs up aion_ls, aion_gs and aion_cs to <OpsRoot>\db (gzip, kept 14 days).
      6. Builds the changed images while the old servers keep running, after tagging the
         current images :previous.
      7. Stops game, chat and login (the game server saves players first) and starts them again
         in login -> chat -> game order.
      8. Health check. New images that fail it are rolled back to :previous (exit code 1).
      9. Builds and starts the companion host (docker\companionhost), the client program that
         plays the companions players ask for. It is no server: whatever happens to it changes
         neither the exit code nor a rollback, and it is only built from a tree the servers
         were deployed from.
     10. Records the deployed commit of each rebuilt server, then prunes dangling images, build
         cache older than a week, and old backups and logs. Volumes are never touched.

    Exit codes, shown as "Last Run Result" in Task Scheduler:
      0  restarted; new code, if any, deployed
      1  failed, see the log; the servers may be on rolled-back images or down
      2  restarted, but new code was not deployed: the live schema differs from the repo
      3  another RestartAll run is in progress
      4  restarted, but the update was skipped: not on main, uncommitted changes, or main
         has diverged from origin/main

.PARAMETER PlanOnly
    Fetch, report what a real run would rebuild, and run the schema gate. Merges, builds,
    starts and stops nothing.
.PARAMETER RestartOnly
    No git, no schema gate and no build: back up, restart the servers on their current images,
    and health-check them.
.PARAMETER ForceRebuild
    Rebuild all three server images even if their inputs did not change.
.PARAMETER OpsRoot
    Where backups (db\), logs (logs\) and the deploy record (deployed.json) are kept. It is
    outside the repository so that no git operation can remove them.
#>
[CmdletBinding()]
param(
    [switch]$PlanOnly,
    [switch]$RestartOnly,
    [switch]$ForceRebuild,
    [string]$OpsRoot = (Join-Path $env:USERPROFILE 'Documents\AionBackups'),
    [int]$BackupRetentionDays = 14,
    [int]$LogRetentionDays = 30,
    [int]$HealthTimeoutSeconds = 300
)

$ErrorActionPreference = 'Stop'

$DockerDir   = $PSScriptRoot
$RepoRoot    = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$ComposeFile = Join-Path $DockerDir 'docker-compose.yml'
$EnvFile     = Join-Path $DockerDir '.env'
$BackupDir   = Join-Path $OpsRoot 'db'
$LogDir      = Join-Path $OpsRoot 'logs'
$StatePath   = Join-Path $OpsRoot 'deployed.json'

$Servers = @('loginserver', 'chatserver', 'gameserver')

# What each server image is built from is read from its Dockerfile (Get-ServerInputs). Each copies
# all of src/, but publishes only its own project and what that references, so a change in another
# server's project below does not count for it; one in Aion.Commons or a props file counts for all.
$ServerProjects = @{
    loginserver = 'src/Aion.LoginServer/'
    chatserver  = 'src/Aion.ChatServer/'
    gameserver  = 'src/Aion.GameServer/'
}

# Lines each server logs once it is serving clients.
$ReadyMarkers = @{
    loginserver = @('Aion Login Client Server listening', 'Aion GameServer Bridge listening')
    chatserver  = @('Aion Chat Client Server listening', 'Aion Chat GameServer Bridge listening')
    gameserver  = @('Authenticated with login server')
}

$SchemaFiles = [ordered]@{
    aion_ls = 'login-server/sql/aion_ls.sql'
    aion_gs = 'game-server/sql/aion_gs.sql'
    aion_cs = 'chat-server/sql/aion_cs.sql'
}
$SchemaCheckContainer = 'aion-schema-check'

$script:LogFile = $null
$script:BuildLogFile = $null
$script:ExitCode = 0
$script:HandOver = $false

# ---------------------------------------------------------------------------------------------
# Logging and process helpers
# ---------------------------------------------------------------------------------------------

function Write-Log {
    param([string]$Message, [ValidateSet('INFO', 'WARN', 'ERROR', 'STEP')][string]$Level = 'INFO')
    $line = '{0} {1,-5} {2}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $Level, $Message
    $color = switch ($Level) { 'WARN' { 'Yellow' } 'ERROR' { 'Red' } 'STEP' { 'Cyan' } default { 'Gray' } }
    Write-Host $line -ForegroundColor $color
    if ($script:LogFile) { Add-Content -LiteralPath $script:LogFile -Value $line -Encoding UTF8 }
}

# Raises the run's exit code; a more serious outcome is never overwritten by a milder one.
function Set-ExitCode([int]$Code) {
    $rank = @{ 0 = 0; 4 = 1; 2 = 2; 1 = 3 }
    if ($rank[$Code] -gt $rank[$script:ExitCode]) { $script:ExitCode = $Code }
}

# Runs a native command, capturing stdout and stderr as text. Windows PowerShell turns redirected
# stderr into errors, which would stop the script under 'Stop', so the preference is relaxed here
# and the exit code is checked instead.
function Invoke-Native {
    param(
        [string]$FilePath,
        [string[]]$ArgumentList = @(),
        [switch]$AllowFailure,
        [switch]$Quiet
    )
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $lines = @(& $FilePath @ArgumentList 2>&1 | ForEach-Object {
            $text = "$_"
            if (-not $Quiet) { Write-Log "    $text" }
            $text
        })
        $exitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previous
    }
    if ($exitCode -ne 0 -and -not $AllowFailure) {
        throw ('{0} {1} failed (exit code {2})' -f $FilePath, ($ArgumentList -join ' '), $exitCode)
    }
    return [pscustomobject]@{ ExitCode = $exitCode; Lines = $lines; Text = ($lines -join "`n") }
}

function Invoke-Git {
    param([string[]]$Arguments, [switch]$AllowFailure, [switch]$Quiet)
    Invoke-Native -FilePath 'git' -ArgumentList (@('-C', $RepoRoot) + $Arguments) -AllowFailure:$AllowFailure -Quiet:$Quiet
}

function Invoke-Docker {
    param([string[]]$Arguments, [switch]$AllowFailure, [switch]$Quiet)
    Invoke-Native -FilePath 'docker' -ArgumentList $Arguments -AllowFailure:$AllowFailure -Quiet:$Quiet
}

function Invoke-Compose {
    param([string[]]$Arguments, [switch]$AllowFailure, [switch]$Quiet)
    $base = @('compose', '-f', $ComposeFile, '--env-file', $EnvFile, '--progress', 'plain')
    Invoke-Docker -Arguments ($base + $Arguments) -AllowFailure:$AllowFailure -Quiet:$Quiet
}

function Get-GitLine([string[]]$Arguments) {
    return (Invoke-Git -Arguments $Arguments -Quiet).Text.Trim()
}

function Get-ShortSha([string]$Sha) {
    if ($Sha.Length -gt 9) { return $Sha.Substring(0, 9) }
    return $Sha
}

# ---------------------------------------------------------------------------------------------
# Docker and containers
# ---------------------------------------------------------------------------------------------

function Wait-Docker {
    param([int]$TimeoutSeconds = 600)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $triedStart = $false
    while ($true) {
        $probe = Invoke-Docker -Arguments @('info', '--format', '{{.ServerVersion}}') -AllowFailure -Quiet
        if ($probe.ExitCode -eq 0) {
            Write-Log "Docker engine $($probe.Text.Trim()) is ready."
            return
        }
        if (-not $triedStart) {
            $triedStart = $true
            $desktop = Join-Path $env:ProgramFiles 'Docker\Docker\Docker Desktop.exe'
            if ((Test-Path -LiteralPath $desktop) -and -not (Get-Process -Name 'Docker Desktop' -ErrorAction SilentlyContinue)) {
                Write-Log 'Docker is not responding and Docker Desktop is not running; starting it.' 'WARN'
                Start-Process -FilePath $desktop
            } else {
                Write-Log 'Docker is not responding yet; waiting.' 'WARN'
            }
        }
        if ((Get-Date) -gt $deadline) { throw "Docker did not become ready within $TimeoutSeconds seconds." }
        Start-Sleep -Seconds 10
    }
}

function Get-ContainerState([string]$Name) {
    $format = '{{.State.Status}}|{{.RestartCount}}|{{.State.StartedAt}}|{{if .State.Health}}{{.State.Health.Status}}{{end}}'
    $result = Invoke-Docker -Arguments @('inspect', '--format', $format, $Name) -AllowFailure -Quiet
    if ($result.ExitCode -ne 0) { return $null }
    $parts = $result.Text.Trim().Split('|')
    return [pscustomobject]@{ Status = $parts[0]; RestartCount = [int]$parts[1]; StartedAt = $parts[2]; Health = $parts[3] }
}

function Test-DatabaseRunning {
    $state = Get-ContainerState 'aion-mysql'
    return ($null -ne $state -and $state.Status -eq 'running' -and $state.Health -eq 'healthy')
}

function Start-Database {
    $state = Get-ContainerState 'aion-mysql'
    if ($null -eq $state -or $state.Status -ne 'running') {
        Write-Log 'MySQL is not running; starting it.' 'WARN'
        $null = Invoke-Compose -Arguments @('up', '-d', 'mysql')
    }
    $deadline = (Get-Date).AddSeconds(300)
    while (-not (Test-DatabaseRunning)) {
        if ((Get-Date) -gt $deadline) { throw 'MySQL did not become healthy within 300 seconds.' }
        Start-Sleep -Seconds 3
    }
    Write-Log 'MySQL is running and healthy.'
}

function Get-EnvValue([string]$Key, [string]$Default) {
    foreach ($line in Get-Content -LiteralPath $EnvFile) {
        if ($line -match "^\s*$Key\s*=\s*([^#\s]+)") { return $Matches[1].Trim('"', "'") }
    }
    return $Default
}

function Test-TcpPort([int]$Port) {
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $attempt = $client.BeginConnect('127.0.0.1', $Port, $null, $null)
        if (-not $attempt.AsyncWaitHandle.WaitOne(3000)) { return $false }
        $client.EndConnect($attempt)
        return $true
    } catch {
        return $false
    } finally {
        $client.Close()
    }
}

# ---------------------------------------------------------------------------------------------
# Git: update and change detection
# ---------------------------------------------------------------------------------------------

function Test-GitAncestor([string]$Ancestor, [string]$Descendant) {
    return (Invoke-Git -Arguments @('merge-base', '--is-ancestor', $Ancestor, $Descendant) -AllowFailure -Quiet).ExitCode -eq 0
}

# Returns the commit to deploy, or $null when the update has to be skipped.
function Update-Checkout {
    $branch = Get-GitLine @('rev-parse', '--abbrev-ref', 'HEAD')
    if ($branch -ne 'main') {
        Write-Log "The checkout is on '$branch', not main: skipping the update and any rebuild." 'WARN'
        return $null
    }
    $dirty = @((Invoke-Git -Arguments @('status', '--porcelain') -Quiet).Lines | Where-Object { $_ })
    if ($dirty.Count -gt 0) {
        Write-Log "The checkout has $($dirty.Count) uncommitted change(s): skipping the update and any rebuild." 'WARN'
        foreach ($line in ($dirty | Select-Object -First 10)) { Write-Log "    $line" 'WARN' }
        return $null
    }

    $head = Get-GitLine @('rev-parse', 'HEAD')
    $fetch = Invoke-Git -Arguments @('fetch', '--prune', 'origin') -AllowFailure -Quiet
    if ($fetch.ExitCode -ne 0) {
        Write-Log "git fetch failed ($($fetch.Text.Trim())); continuing with local main at $(Get-ShortSha $head)." 'WARN'
        return $head
    }
    $upstream = Get-GitLine @('rev-parse', 'origin/main')
    if (Test-GitAncestor $upstream $head) {
        if ($upstream -eq $head) { Write-Log "main is up to date with origin/main at $(Get-ShortSha $head)." }
        else { Write-Log "main is ahead of origin/main; deploying local main at $(Get-ShortSha $head)." }
        return $head
    }
    if (-not (Test-GitAncestor $head $upstream)) {
        Write-Log "main ($(Get-ShortSha $head)) and origin/main ($(Get-ShortSha $upstream)) have diverged: skipping the update and any rebuild." 'WARN'
        return $null
    }
    $count = Get-GitLine @('rev-list', '--count', "$head..$upstream")
    if ($PlanOnly) {
        Write-Log "A real run would fast-forward main $(Get-ShortSha $head) -> $(Get-ShortSha $upstream) ($count commits)."
        return $upstream
    }
    $null = Invoke-Git -Arguments @('merge', '--ff-only', 'origin/main') -Quiet
    Write-Log "Fast-forwarded main $(Get-ShortSha $head) -> $(Get-ShortSha $upstream) ($count commits)."
    return $upstream
}

# PowerShell reads this whole script before running it, so a run whose update changes the script would go on with
# the old copy. Such a run stops after the update and the new copy does the rest, with the same parameters.
function Test-ScriptChanged([string]$From, [string]$To) {
    return (Invoke-Git -Arguments @('diff', '--quiet', $From, $To, '--', 'docker/RestartAll.ps1') -AllowFailure -Quiet).ExitCode -ne 0
}

function Get-HandOverArguments([System.Collections.IDictionary]$Parameters) {
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $DockerDir 'RestartAll.ps1'))
    foreach ($name in $Parameters.Keys) {
        $value = $Parameters[$name]
        if ($value -is [System.Management.Automation.SwitchParameter]) {
            if ($value.IsPresent) { $arguments += "-$name" }
        } else {
            $arguments += "-$name"
            $arguments += [string]$value
        }
    }
    return $arguments
}

function Invoke-HandOver([System.Collections.IDictionary]$Parameters) {
    $arguments = Get-HandOverArguments $Parameters
    & powershell.exe @arguments | Out-Host
    return $LASTEXITCODE
}

# The repository paths a server image is built from: the sources of its Dockerfile's COPY and ADD
# lines (not those copied from another stage), its docker/<server>/ folder and .dockerignore. Read
# at the commit being deployed, so a Dockerfile that starts copying something new is followed the
# same night.
function Get-ServerInputs([string]$Server, [string]$Commit) {
    $inputs = @("docker/$Server/", '.dockerignore')
    $dockerfile = (Invoke-Git -Arguments @('show', "${Commit}:docker/$Server/Dockerfile") -Quiet).Lines
    foreach ($line in $dockerfile) {
        if ($line -notmatch '^\s*(?:COPY|ADD)\s+(.+)$') { continue }
        $arguments = $Matches[1].Trim()
        if ($arguments -match '(^|\s)--from=') { continue }
        # Both the shell form and the JSON form (["src/", "./src/"]); flags such as --chown are not paths.
        $words = @(($arguments -replace '[\[\]",]', ' ') -split '\s+' | Where-Object { $_ -and -not $_.StartsWith('--') })
        if ($words.Count -lt 2) { continue }
        $inputs += @($words[0..($words.Count - 2)] | ForEach-Object { $_ -replace '^\./', '' })
    }
    return $inputs
}

function Test-PathFeedsServer([string]$Path, [string]$Server, [string[]]$Inputs) {
    $ordinal = [System.StringComparison]::Ordinal
    foreach ($other in $Servers) {
        if ($other -ne $Server -and $Path.StartsWith($ServerProjects[$other], $ordinal)) { return $false }
    }
    foreach ($source in $Inputs) {
        if ($source -eq '.' -or $source -eq '') { return $true }
        if ($source -match '[*?\[]') {
            # As in Docker, a wildcard stays within one path segment; a folder it matches is copied whole.
            $depth = $source.TrimEnd('/').Split('/').Count
            $segments = $Path.Split('/')
            if ($segments.Count -ge $depth -and (($segments[0..($depth - 1)] -join '/') -clike $source.TrimEnd('/'))) { return $true }
        } elseif ($source.EndsWith('/')) {
            if ($Path.StartsWith($source, $ordinal)) { return $true }
        } elseif ($Path -ceq $source -or $Path.StartsWith("$source/", $ordinal)) {
            return $true
        }
    }
    return $false
}

function Read-DeployState {
    $state = @{}
    if (Test-Path -LiteralPath $StatePath) {
        $json = Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json
        foreach ($entry in $json.PSObject.Properties) {
            $state[$entry.Name] = [ordered]@{ commit = [string]$entry.Value.commit; deployedAt = [string]$entry.Value.deployedAt }
        }
    }
    return $state
}

function Save-DeployState([string[]]$Deployed, [string]$Commit) {
    $state = Read-DeployState
    $now = (Get-Date).ToString('o')
    foreach ($server in $Deployed) { $state[$server] = [ordered]@{ commit = $Commit; deployedAt = $now } }
    $sorted = [ordered]@{}
    foreach ($key in ($state.Keys | Sort-Object)) { $sorted[$key] = $state[$key] }
    $sorted | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $StatePath -Encoding UTF8
}

function Get-ServersToRebuild([string]$Target, [hashtable]$State) {
    $rebuild = @()
    foreach ($server in $Servers) {
        if ($ForceRebuild) {
            Write-Log "${server}: rebuild forced."
            $rebuild += $server
            continue
        }
        $deployed = if ($State.ContainsKey($server)) { $State[$server].commit } else { '' }
        if (-not $deployed) {
            Write-Log "${server}: no recorded deployment; rebuilding."
            $rebuild += $server
            continue
        }
        if ((Invoke-Git -Arguments @('cat-file', '-e', "$deployed^{commit}") -AllowFailure -Quiet).ExitCode -ne 0) {
            Write-Log "${server}: recorded commit $(Get-ShortSha $deployed) is not in this repository; rebuilding." 'WARN'
            $rebuild += $server
            continue
        }
        $inputs = Get-ServerInputs $server $Target
        $changed = @((Invoke-Git -Arguments @('diff', '--name-only', $deployed, $Target) -Quiet).Lines |
            Where-Object { $_ -and (Test-PathFeedsServer $_ $server $inputs) })
        if ($changed.Count -gt 0) {
            Write-Log "${server}: $($changed.Count) input file(s) changed since $(Get-ShortSha $deployed); rebuilding. First few:"
            foreach ($path in ($changed | Select-Object -First 5)) { Write-Log "    $path" }
            $rebuild += $server
        } else {
            Write-Log "${server}: up to date (deployed from $(Get-ShortSha $deployed))."
        }
    }
    return $rebuild
}

# ---------------------------------------------------------------------------------------------
# Schema gate
# ---------------------------------------------------------------------------------------------

function Get-SchemaText([string]$Container, [string]$Database) {
    # $MYSQL_ROOT_PASSWORD is expanded inside the container, so no password reaches this host's
    # command lines or the log. Both containers are MySQL images that set it.
    $dump = 'MYSQL_PWD=$MYSQL_ROOT_PASSWORD mysqldump -uroot --no-data --skip-comments --skip-dump-date --compact ' + $Database + ' 2>/dev/null'
    $result = Invoke-Docker -Arguments @('exec', $Container, 'bash', '-c', $dump) -Quiet
    return (($result.Lines | ForEach-Object { $_ -replace ' AUTO_INCREMENT=\d+', '' }) -join "`n")
}

# True when the live aion_ls, aion_gs and aion_cs tables are exactly what the schema files at
# $Commit create.
function Test-SchemaGate([string]$Commit) {
    Write-Log "Comparing the live databases with the schema files at $(Get-ShortSha $Commit)."
    $image = (Invoke-Docker -Arguments @('inspect', '--format', '{{.Image}}', 'aion-mysql') -Quiet).Text.Trim()
    $archive = Join-Path $env:TEMP ("aion-schema-{0}.tar" -f $PID)
    $null = Invoke-Docker -Arguments @('rm', '-f', $SchemaCheckContainer) -AllowFailure -Quiet
    try {
        $null = Invoke-Git -Arguments (@('archive', '--output', $archive, $Commit) + @($SchemaFiles.Values)) -Quiet
        $null = Invoke-Docker -Arguments @('run', '-d', '--name', $SchemaCheckContainer, '-e', 'MYSQL_ROOT_PASSWORD=check', $image) -Quiet
        $deadline = (Get-Date).AddSeconds(180)
        while ((Invoke-Docker -Arguments @('exec', $SchemaCheckContainer, 'mysql', '-h127.0.0.1', '-uroot', '-pcheck', '-e', 'SELECT 1') -AllowFailure -Quiet).ExitCode -ne 0) {
            if ((Get-Date) -gt $deadline) { throw 'The throwaway schema-check MySQL did not start within 180 seconds.' }
            Start-Sleep -Seconds 3
        }
        $null = Invoke-Docker -Arguments @('cp', $archive, "${SchemaCheckContainer}:/tmp/schema.tar") -Quiet
        $null = Invoke-Docker -Arguments @('exec', $SchemaCheckContainer, 'tar', '-xf', '/tmp/schema.tar', '-C', '/tmp') -Quiet
        foreach ($database in $SchemaFiles.Keys) {
            $load = "MYSQL_PWD=`$MYSQL_ROOT_PASSWORD mysql -uroot -e 'CREATE DATABASE $database CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci'" +
                " && MYSQL_PWD=`$MYSQL_ROOT_PASSWORD mysql -uroot $database < /tmp/$($SchemaFiles[$database])"
            $null = Invoke-Docker -Arguments @('exec', $SchemaCheckContainer, 'bash', '-c', $load) -Quiet
        }

        $allMatch = $true
        foreach ($database in $SchemaFiles.Keys) {
            $live = Get-SchemaText 'aion-mysql' $database
            $repo = Get-SchemaText $SchemaCheckContainer $database
            if ($live -ceq $repo) {
                Write-Log "  $database matches $($SchemaFiles[$database])."
                continue
            }
            $allMatch = $false
            Write-Log "  $database DIFFERS from $($SchemaFiles[$database]). Lines only in live (<) and only in the repo (>):" 'ERROR'
            $diff = @(Compare-Object ($live -split "`n") ($repo -split "`n") -CaseSensitive)
            if ($diff.Count -eq 0) { Write-Log '    (same lines in a different order, for example a moved column)' 'ERROR' }
            foreach ($line in ($diff | Select-Object -First 40)) {
                $side = if ($line.SideIndicator -eq '<=') { '<' } else { '>' }
                Write-Log "    $side $($line.InputObject)" 'ERROR'
            }
        }
        return $allMatch
    } finally {
        $null = Invoke-Docker -Arguments @('rm', '-f', $SchemaCheckContainer) -AllowFailure -Quiet
        Remove-Item -LiteralPath $archive -Force -ErrorAction SilentlyContinue
    }
}

# ---------------------------------------------------------------------------------------------
# Backup, build, restart, health
# ---------------------------------------------------------------------------------------------

function Backup-Databases {
    New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null
    $destination = Join-Path $BackupDir ('aion-{0}.sql.gz' -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
    $dump = 'set -o pipefail; MYSQL_PWD=$MYSQL_ROOT_PASSWORD mysqldump -uroot --single-transaction --routines --triggers --events --hex-blob ' +
        '--databases aion_ls aion_gs aion_cs | gzip -c > /tmp/aion-backup.sql.gz && gzip -t /tmp/aion-backup.sql.gz'
    try {
        $null = Invoke-Docker -Arguments @('exec', 'aion-mysql', 'bash', '-c', $dump)
        $null = Invoke-Docker -Arguments @('cp', 'aion-mysql:/tmp/aion-backup.sql.gz', $destination) -Quiet
    } finally {
        $null = Invoke-Docker -Arguments @('exec', 'aion-mysql', 'rm', '-f', '/tmp/aion-backup.sql.gz') -AllowFailure -Quiet
    }
    $size = (Get-Item -LiteralPath $destination).Length
    if ($size -lt 1024) { throw "The backup $destination is only $size bytes." }
    Write-Log ('Backed up aion_ls, aion_gs and aion_cs to {0} ({1:N0} KB).' -f $destination, ($size / 1KB))
}

function Restore-PreviousImages([string[]]$ToRestore) {
    foreach ($server in $ToRestore) {
        $image = "aion-$server"
        if ((Invoke-Docker -Arguments @('image', 'inspect', "${image}:previous") -AllowFailure -Quiet).ExitCode -eq 0) {
            $null = Invoke-Docker -Arguments @('tag', "${image}:previous", "${image}:latest") -Quiet
            Write-Log "${image}:latest is back on the :previous image." 'WARN'
        } else {
            Write-Log "There is no ${image}:previous image to go back to." 'ERROR'
        }
    }
}

# Returns $true when every image built. The full build output goes to its own log file.
function Build-Images([string[]]$ToBuild) {
    foreach ($server in $ToBuild) {
        $image = "aion-$server"
        if ((Invoke-Docker -Arguments @('image', 'inspect', "${image}:latest") -AllowFailure -Quiet).ExitCode -eq 0) {
            $null = Invoke-Docker -Arguments @('tag', "${image}:latest", "${image}:previous") -Quiet
        }
    }
    $started = Get-Date
    $result = Invoke-Compose -Arguments (@('build') + $ToBuild) -AllowFailure -Quiet
    if ($script:BuildLogFile) { $result.Lines | Set-Content -LiteralPath $script:BuildLogFile -Encoding UTF8 }
    $minutes = [math]::Round(((Get-Date) - $started).TotalMinutes, 1)
    if ($result.ExitCode -ne 0) {
        Write-Log "docker compose build failed after $minutes min (exit code $($result.ExitCode)). Last lines, full output in $($script:BuildLogFile):" 'ERROR'
        foreach ($line in ($result.Lines | Select-Object -Last 40)) { Write-Log "    $line" 'ERROR' }
        return $false
    }
    Write-Log "Built $($ToBuild -join ', ') in $minutes min (full output in $($script:BuildLogFile))."
    return $true
}

function Restart-Servers {
    Write-Log 'Stopping game, chat and login. The game server saves players first (up to 90 s).'
    $null = Invoke-Compose -Arguments @('stop', 'gameserver', 'chatserver', 'loginserver')
    Write-Log 'Starting login -> chat -> game.'
    $null = Invoke-Compose -Arguments @('up', '-d', 'loginserver', 'chatserver', 'gameserver')
}

# Each server must be running, have logged its ready lines since it started, and have its client
# port open; then all three must stay up, without restarting, for 30 seconds.
function Test-ServersHealthy([int]$TimeoutSeconds) {
    $ports = @{
        loginserver = [int](Get-EnvValue 'LOGIN_CLIENT_PORT' '2106')
        chatserver  = [int](Get-EnvValue 'CHAT_CLIENT_PORT' '10241')
        gameserver  = [int](Get-EnvValue 'GAME_CLIENT_PORT' '7777')
    }
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ($true) {
        $waiting = @()
        $seen = @{}
        foreach ($server in $Servers) {
            $name = "aion-$server"
            $state = Get-ContainerState $name
            if ($null -eq $state -or $state.Status -ne 'running') {
                $waiting += "$server is $(if ($state) { $state.Status } else { 'missing' })"
                continue
            }
            $seen[$server] = $state
            $logs = (Invoke-Docker -Arguments @('logs', '--since', $state.StartedAt, $name) -AllowFailure -Quiet).Text
            foreach ($marker in $ReadyMarkers[$server]) {
                if ($logs.IndexOf($marker, [System.StringComparison]::Ordinal) -lt 0) { $waiting += "$server has not logged '$marker'" }
            }
            if (-not (Test-TcpPort $ports[$server])) { $waiting += "$server port $($ports[$server]) is not accepting connections" }
        }

        if ($waiting.Count -eq 0) {
            Write-Log 'All servers report ready; watching them for 30 s.'
            Start-Sleep -Seconds 30
            $stable = $true
            foreach ($server in $Servers) {
                $state = Get-ContainerState "aion-$server"
                if ($null -eq $state -or $state.Status -ne 'running' -or $state.StartedAt -ne $seen[$server].StartedAt) {
                    Write-Log "$server stopped or restarted while being watched." 'WARN'
                    $stable = $false
                }
            }
            if ($stable) {
                $gameLogs = (Invoke-Docker -Arguments @('logs', '--since', $seen['gameserver'].StartedAt, 'aion-gameserver') -AllowFailure -Quiet).Lines
                $failures = @($gameLogs | Where-Object { $_ -match '^(fail|crit):' }).Count
                if ($failures -gt 0) { Write-Log "The game server has logged $failures 'fail:' or 'crit:' entries since it started; worth a look." 'WARN' }
                Write-Log ('Healthy: login :{0}, chat :{1}, game :{2}.' -f $ports.loginserver, $ports.chatserver, $ports.gameserver)
                return $true
            }
        }

        if ((Get-Date) -gt $deadline) {
            Write-Log "The servers were not healthy within $TimeoutSeconds s:" 'ERROR'
            foreach ($reason in $waiting) { Write-Log "    $reason" 'ERROR' }
            return $false
        }
        Start-Sleep -Seconds 5
    }
}

# The companion host is the client program that plays the companions players ask for with
# .companion (docs/companion-live-host.md). It is not one of the three servers and never decides
# the result of a run. $Build: this run deployed, or would have deployed, the tree on disk; the
# host's image is then built from it too, which Docker's layer cache makes a no-op when nothing
# it is built from changed. A host that is running and unchanged is left alone: its seats log in
# again by themselves once the servers are back.
function Update-CompanionHost([bool]$Build, [bool]$ServersHealthy) {
    try {
        if (-not $ServersHealthy) {
            Write-Log 'The servers are not healthy; the companion host is left as it is.' 'WARN'
            return
        }
        if ($Build) {
            $buildStarted = Get-Date
            $built = Invoke-Compose -Arguments @('build', 'companionhost') -AllowFailure -Quiet
            if ($script:BuildLogFile) {
                Add-Content -LiteralPath $script:BuildLogFile -Value (@('', '==== companionhost ====') + $built.Lines) -Encoding UTF8
            }
            $minutes = [math]::Round(((Get-Date) - $buildStarted).TotalMinutes, 1)
            if ($built.ExitCode -ne 0) {
                Write-Log "The companion host image did not build (exit code $($built.ExitCode)) after $minutes min; keeping the one that is there. Last lines, full output in $($script:BuildLogFile):" 'WARN'
                foreach ($line in ($built.Lines | Select-Object -Last 30)) { Write-Log "    $line" 'WARN' }
            } else {
                # bake-navmeshes.sh prints these only when its stage ran, that is when something it bakes from changed.
                $bake = @($built.Lines | Where-Object { $_ -match 'Baking \d+ navmesh|Navmeshes in the image' } |
                    ForEach-Object { ($_ -replace '^#\d+\s+[\d.]+\s+', '').Trim() })
                $note = if ($bake.Count -gt 0) { " ($($bake -join '; '))" } else { '' }
                Write-Log "Companion host image built in $minutes min$note."
            }
        }
        # --no-build: an image that is missing is never built from a tree this run did not deploy.
        $started = Invoke-Compose -Arguments @('up', '-d', '--no-deps', '--no-build', 'companionhost') -AllowFailure -Quiet
        if ($started.ExitCode -ne 0) {
            Write-Log "The companion host did not start (exit code $($started.ExitCode)); players get no companions until it does." 'WARN'
        } else {
            Write-Log 'The companion host is up; its seats log in by themselves.'
        }
    } catch {
        Write-Log "The companion host step failed: $($_.Exception.Message)" 'WARN'
    }
}

function Remove-OldFiles([string]$Directory, [string]$Filter, [int]$Days, [int]$KeepNewest) {
    if (-not (Test-Path -LiteralPath $Directory)) { return }
    $cutoff = (Get-Date).AddDays(-$Days)
    $old = @(Get-ChildItem -LiteralPath $Directory -Filter $Filter -File |
        Sort-Object LastWriteTime -Descending |
        Select-Object -Skip $KeepNewest |
        Where-Object { $_.LastWriteTime -lt $cutoff })
    foreach ($file in $old) { Remove-Item -LiteralPath $file.FullName -Force }
    if ($old.Count -gt 0) { Write-Log "Removed $($old.Count) file(s) older than $Days days from $Directory." }
}

function Invoke-Cleanup {
    try {
        $images = Invoke-Docker -Arguments @('image', 'prune', '-f') -Quiet
        $cache = Invoke-Docker -Arguments @('builder', 'prune', '-f', '--filter', 'until=168h') -Quiet
        $totals = @($images.Lines | Where-Object { $_ -match 'Total reclaimed space' }) + @($cache.Lines | Where-Object { $_ -match '^Total' })
        if ($totals.Count -gt 0) {
            Write-Log ('Pruned dangling images and week-old build cache: {0}' -f (($totals | ForEach-Object { ($_ -replace '\s+', ' ').Trim() }) -join '; '))
        }
        Remove-OldFiles -Directory $BackupDir -Filter 'aion-*.sql.gz' -Days $BackupRetentionDays -KeepNewest 7
        Remove-OldFiles -Directory $LogDir -Filter 'RestartAll-*.log' -Days $LogRetentionDays -KeepNewest 10
    } catch {
        Write-Log "Cleanup failed: $($_.Exception.Message)" 'WARN'
    }
}

# ---------------------------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------------------------

function Invoke-RestartAll {
    $script:ExitCode = 0
    $script:HandOver = $false
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
    $suffix = if ($PlanOnly) { '-plan' } else { '' }
    $script:LogFile = Join-Path $LogDir "RestartAll-$stamp$suffix.log"
    $script:BuildLogFile = Join-Path $LogDir "RestartAll-$stamp-build.log"

    $mutex = New-Object System.Threading.Mutex($false, 'Local\AionRestartAll')
    $ownsMutex = $false
    try { $ownsMutex = $mutex.WaitOne(0) } catch [System.Threading.AbandonedMutexException] { $ownsMutex = $true }
    if (-not $ownsMutex) {
        Write-Log 'Another RestartAll run is in progress; exiting.' 'WARN'
        $mutex.Dispose()
        $script:ExitCode = 3
        return
    }

    # Docker and git write UTF-8; without this, Windows PowerShell decodes them with the OEM code page.
    $consoleEncoding = $null
    try { $consoleEncoding = [Console]::OutputEncoding; [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

    try {
        $mode = if ($PlanOnly) { 'plan only' } elseif ($RestartOnly) { 'restart only' } elseif ($ForceRebuild) { 'force rebuild' } else { 'nightly' }
        Write-Log "RestartAll ($mode) for $RepoRoot" 'STEP'
        if (-not (Test-Path -LiteralPath $EnvFile)) { throw "$EnvFile is missing; create it with docker\deploy.ps1 first." }
        Wait-Docker
        if ($PlanOnly) { $databaseUp = Test-DatabaseRunning } else { Start-Database; $databaseUp = $true }

        $target = $null
        $toBuild = @()
        # Whether the tree on disk is one this run deploys from; the companion host is built from no other.
        $deployTree = $false
        if (-not $RestartOnly) {
            # A failure here must not cost the nightly restart, so it only cancels the deploy.
            try {
                Write-Log 'Updating the checkout' 'STEP'
                $before = Get-GitLine @('rev-parse', 'HEAD')
                $target = Update-Checkout
                if ($target -and -not $PlanOnly -and $target -ne $before -and (Test-ScriptChanged $before $target)) {
                    Write-Log 'This update changed RestartAll.ps1; the new copy does the rest of the run.' 'STEP'
                    $script:HandOver = $true
                    return
                }
                if ($target) {
                    Write-Log 'Choosing images to rebuild' 'STEP'
                    $toBuild = @(Get-ServersToRebuild -Target $target -State (Read-DeployState))
                    $deployTree = $true
                } else {
                    Set-ExitCode 4
                }

                Write-Log 'Schema gate' 'STEP'
                if (-not $databaseUp) {
                    Write-Log 'MySQL is not running, so the schema gate was skipped.' 'WARN'
                } else {
                    $gateCommit = if ($target) { $target } else { Get-GitLine @('rev-parse', 'HEAD') }
                    if (-not (Test-SchemaGate -Commit $gateCommit)) {
                        Set-ExitCode 2
                        if ($toBuild.Count -gt 0) {
                            Write-Log "Not deploying $($toBuild -join ', '): apply the migration to the live databases by hand, then run RestartAll again." 'ERROR'
                        }
                        $toBuild = @()
                        $deployTree = $false
                    }
                }
            } catch {
                Write-Log "Update failed: $($_.Exception.Message). Restarting on the current images." 'ERROR'
                Set-ExitCode 1
                $toBuild = @()
                $deployTree = $false
            }
        }

        if ($PlanOnly) {
            $plan = if ($toBuild.Count -gt 0) { "rebuild $($toBuild -join ', ')" } else { 'rebuild nothing' }
            Write-Log "Plan: back up, $plan, restart login, chat and game, then the companion host." 'STEP'
            return
        }

        Write-Log 'Backing up the databases' 'STEP'
        Backup-Databases

        $deployed = @()
        if ($toBuild.Count -gt 0) {
            Write-Log "Building $($toBuild -join ', ') while the servers keep running" 'STEP'
            if (Build-Images -ToBuild $toBuild) {
                $deployed = $toBuild
            } else {
                Restore-PreviousImages -ToRestore $toBuild
                Set-ExitCode 1
            }
        }

        Write-Log 'Restarting the servers' 'STEP'
        try {
            Restart-Servers
            Write-Log 'Health check' 'STEP'
            $healthy = Test-ServersHealthy -TimeoutSeconds $HealthTimeoutSeconds
        } catch {
            Write-Log "The restart failed: $($_.Exception.Message)" 'ERROR'
            $healthy = $false
        }
        if (-not $healthy -and $deployed.Count -gt 0) {
            Write-Log "The new $($deployed -join ', ') image(s) did not come up healthy; rolling back to :previous." 'ERROR'
            Restore-PreviousImages -ToRestore $deployed
            $null = Invoke-Compose -Arguments @('up', '-d', 'loginserver', 'chatserver', 'gameserver')
            $deployed = @()
            Set-ExitCode 1
            $healthy = Test-ServersHealthy -TimeoutSeconds $HealthTimeoutSeconds
            if ($healthy) { Write-Log 'Rollback done: the servers are healthy on their previous images.' 'WARN' }
        }
        if (-not $healthy) {
            Write-Log 'The servers are NOT healthy. See: docker compose -f docker\docker-compose.yml logs' 'ERROR'
            Set-ExitCode 1
        }

        Write-Log 'Companion host' 'STEP'
        # Servers that were to be rebuilt and are not on their new images run the tree before this one.
        Update-CompanionHost -Build ($deployTree -and ($toBuild.Count -eq 0 -or $deployed.Count -gt 0)) -ServersHealthy $healthy

        if ($deployed.Count -gt 0) {
            Save-DeployState -Deployed $deployed -Commit $target
            Write-Log "Deployed $($deployed -join ', ') at $(Get-ShortSha $target)."
        }

        Write-Log 'Cleaning up' 'STEP'
        Invoke-Cleanup
    } catch {
        Write-Log "RestartAll failed: $($_.Exception.Message)" 'ERROR'
        Set-ExitCode 1
    } finally {
        if ($script:HandOver) { Write-Log 'Handing over; the new copy writes a log of its own.' 'STEP' }
        else { Write-Log "RestartAll finished with exit code $($script:ExitCode)." 'STEP' }
        if ($consoleEncoding) { try { [Console]::OutputEncoding = $consoleEncoding } catch { } }
        $mutex.ReleaseMutex()
        $mutex.Dispose()
    }
}

# Dot-sourcing (as test-RestartAll.ps1 does) only loads the functions.
if ($MyInvocation.InvocationName -ne '.') {
    Invoke-RestartAll
    # After the mutex is released, so the new copy can take it.
    if ($script:HandOver) { exit (Invoke-HandOver -Parameters $PSBoundParameters) }
    exit $script:ExitCode
}
