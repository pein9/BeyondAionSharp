# CH-01, the companion host (docs/companion-live-host.md): the program beside a world you already run that plays the
# companions a player asks for with .companion. Its seats are accounts that wait at character selection; the server
# makes a companion on one of them, and the seat enters the world with it and plays it. The world stays yours: this
# script only reads Docker state and logs, and the host only speaks the client protocol. It never builds, starts, stops,
# restarts or recreates a container, never writes to the database, and never uses the admin API. The world must have
# been started with AION_COMPANIONS=true and the seat accounts in AION_COMPANION_ACCOUNTS (RUNNING.md). Stop the host
# with Ctrl+C, or from another terminal with -Stop: its companions leave, and the server deletes them.
#
#   pwsh -NoProfile -File scripts/live/companion-host.ps1 -Target aion
#   pwsh -NoProfile -File scripts/live/companion-host.ps1 -Target aion -Stop
[CmdletBinding()]
param(
	[Parameter(Mandatory)]
	[ValidatePattern('^[a-z0-9][a-z0-9_-]*$')]
	[string]$Target,

	[ValidatePattern('^[a-z0-9][a-z0-9_-]*$')]
	[string]$Run = ('ch-' + [DateTimeOffset]::Now.ToString('yyyyMMdd-HHmmss')),
	[ValidateRange(1, 5)][int]$Seats = 5,

	[switch]$Stop,

	[string]$RunRoot,

	[string]$ServerHost = '127.0.0.1',
	[ValidateRange(0, 65535)][int]$LoginPort = 0,
	[ValidateRange(0, 65535)][int]$GamePort = 0,
	[ValidateRange(0, 65535)][int]$ChatPort = 0,

	[ValidateRange(0, 65535)][int]$DashboardPort = 17880,
	[ValidateRange(1, 3600)][int]$StepTimeoutSeconds = 120,
	[ValidateRange(1, 3600)][int]$ConnectTimeoutSeconds = 10,
	[int]$Seed = 1,

	[switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $repoRoot 'scripts/e2e/run-artifact-owner.ps1')
. (Join-Path $PSScriptRoot 'attach-controller.ps1')
. (Join-Path $PSScriptRoot 'companion-host-controller.ps1')

if ([string]::IsNullOrWhiteSpace($RunRoot)) { $RunRoot = Join-Path $repoRoot 'run/companion-host' }
$runRootPath = [IO.Path]::GetFullPath($RunRoot)
if ($runRootPath -eq $repoRoot -or [string]::IsNullOrWhiteSpace((Split-Path $runRootPath -Leaf))) {
	throw "Refusing unsafe run root: $runRootPath"
}

# Start-Process joins its arguments verbatim; quote the ones with spaces (for example the time zone name).
function ConvertTo-HostArgumentLine([string[]]$Arguments) {
	return ($Arguments | ForEach-Object { if ($_ -match '[\s"]') { '"' + $_.Replace('"', '\"') + '"' } else { $_ } }) -join ' '
}

function Write-HostJson([string]$Path, [object]$Value) {
	[IO.File]::WriteAllText($Path + '.tmp', ($Value | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
	[IO.File]::Move($Path + '.tmp', $Path, $true)
}

if ($Stop) {
	$active = @(Get-ActiveAttachRuns -RunRoot $runRootPath -Project $Target)
	if ($PSBoundParameters.ContainsKey('Run')) { $active = @($active | Where-Object Name -ceq $Run) }
	if ($active.Count -eq 0) { Write-Host "No companion host is attached to '$Target'."; return }
	foreach ($activeRun in $active) {
		New-Item -ItemType File -Path (Join-Path $activeRun.FullName 'attach.stop') -Force | Out-Null
		Write-Host "Asked $($activeRun.Name) to take its companions out and exit."
	}
	$deadline = [DateTimeOffset]::UtcNow.AddSeconds(90)
	while ([DateTimeOffset]::UtcNow -lt $deadline -and
		@($active | Where-Object { -not (Test-AionRunArtifactOwnerExited -Directory $_.FullName) }).Count -gt 0) {
		Start-Sleep -Seconds 1
	}
	$remaining = @($active | Where-Object { -not (Test-AionRunArtifactOwnerExited -Directory $_.FullName) })
	if ($remaining.Count -gt 0) { throw "Still running after 90 seconds: $($remaining.Name -join ', ')." }
	Write-Host 'Stopped. The server was not touched.'
	return
}

$runPath = [IO.Path]::GetFullPath((Join-Path $runRootPath $Run))
if ([IO.Path]::GetFullPath((Split-Path $runPath -Parent)) -ne $runRootPath) { throw "Run directory escaped its root: $runPath" }
if (Test-Path -LiteralPath $runPath) { throw "Run directory already exists: $runPath" }
$already = @(Get-ActiveAttachRuns -RunRoot $runRootPath -Project $Target)
if ($already.Count -gt 0) {
	throw "A companion host is already attached to '$Target' ($($already.Name -join ', ')). Stop it first: companion-host.ps1 -Target $Target -Stop"
}

# Read the world before creating anything, so a refused target leaves no run behind.
$before = Get-AttachTarget $Target
Assert-CompanionHostTarget $before $Seats
$seatAccounts = @(Get-CompanionSeatAccounts $Seats)
if ($LoginPort -eq 0) { $LoginPort = Get-AttachPort $before 'loginserver' '2106/tcp' }
if ($GamePort -eq 0) { $GamePort = Get-AttachPort $before 'gameserver' '7777/tcp' }
if ($ChatPort -eq 0) { $ChatPort = Get-AttachPort $before 'chatserver' '10241/tcp' }
if ($LoginPort -eq 0 -or $GamePort -eq 0) { throw "Could not find published login/game ports for '$Target'; pass -LoginPort and -GamePort." }
if ($ChatPort -eq 0) { $ChatPort = 10241 } # A companion does not open Chat; the option only needs a value.
if ($DashboardPort -ne 0) {
	$probe = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $DashboardPort)
	try { $probe.Start() }
	catch { throw "Monitor port $DashboardPort is in use (a SIM round or another bot?). Stop it or pass -DashboardPort 0 or another port." }
	finally { $probe.Stop() }
}

New-Item -ItemType Directory -Path $runPath | Out-Null
Register-AionRunArtifactOwner -Directory $runPath
$startedAt = [DateTimeOffset]::UtcNow
$stopFile = Join-Path $runPath 'attach.stop'
$botDirectory = Join-Path $runPath 'bot'
$gitSha = ([string](& git -C $repoRoot rev-parse HEAD)).Trim()
Write-HostJson (Join-Path $runPath 'attach-target.json') ([ordered]@{
	schemaVersion = 1; scenario = 'CH-01'; run = $Run; target = $Target; startedUtc = $startedAt.ToString('O'); gitSha = $gitSha
	seatAccounts = $seatAccounts
	endpoints = [ordered]@{ host = $ServerHost; login = $LoginPort; game = $GamePort; chat = $ChatPort }
	dashboard = $(if ($DashboardPort -eq 0) { $null } else { "http://127.0.0.1:$DashboardPort/" })
	policy = 'Read-only attach: no Docker lifecycle, database writes or admin API; the host uses only client packets.'
})
Write-HostJson (Join-Path $runPath 'target-before.json') $before

$botExitCode = $null
$failure = $null
$bot = $null
try {
	if (-not $SkipBuild) {
		& dotnet build (Join-Path $repoRoot 'tools/Aion.LiveBots/Aion.LiveBots.csproj') --nologo -v quiet -clp:ErrorsOnly
		if ($LASTEXITCODE -ne 0) { throw "Live bot build failed with exit code $LASTEXITCODE." }
	}
	# Run a private copy so a host that plays for hours never locks the checkout's build output.
	$builtBot = Join-Path $repoRoot 'tools/Aion.LiveBots/bin/Debug/net10.0'
	if (-not (Test-Path -LiteralPath (Join-Path $builtBot 'Aion.LiveBots.dll'))) { throw "No built bot at $builtBot." }
	Copy-Item -LiteralPath $builtBot -Destination $botDirectory -Recurse
	$timeZone = if ([string]::IsNullOrWhiteSpace($env:TZ)) { [TimeZoneInfo]::Local.Id } else { $env:TZ }
	$botArguments = @(
		(Join-Path $botDirectory 'Aion.LiveBots.dll'),
		'--run', $Run, '--output', $runPath, '--scenario', 'CH-01', '--bots', "$Seats",
		'--attach-target', $Target, '--stop-file', $stopFile,
		'--host', $ServerHost, '--login-port', "$LoginPort", '--game-port', "$GamePort", '--chat-port', "$ChatPort",
		'--connect-timeout-seconds', "$ConnectTimeoutSeconds", '--step-timeout-seconds', "$StepTimeoutSeconds",
		'--seed', "$Seed", '--git-sha', $gitSha, '--profile', "companion-host-$Target", '--time-zone', $timeZone,
		'--dashboard-port', "$DashboardPort"
	)
	$stdout = Join-Path $runPath 'bot.stdout.log'
	$stderr = Join-Path $runPath 'bot.stderr.log'
	# Same console, so Ctrl+C reaches the host too; it then takes its companions out before exiting.
	$bot = Start-Process -FilePath 'dotnet' -ArgumentList (ConvertTo-HostArgumentLine $botArguments) -WorkingDirectory $repoRoot `
		-NoNewWindow -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
	$null = $bot.Handle # Keeps the exit code readable after the process ends.
	Write-Host "Companion host attached to '$Target' (login $ServerHost`:$LoginPort, game $GamePort) with seats $($seatAccounts -join ', '). Run: $runPath"
	if ($DashboardPort -ne 0) { Write-Host "Live map: http://127.0.0.1:$DashboardPort/" }
	Write-Host "In game, once a seat is waiting: .companion cleric | .companion list | .companion dismiss. Stop: Ctrl+C, or companion-host.ps1 -Target $Target -Stop"
	$shown = @{ $stdout = 0L; $stderr = 0L }
	function Show-BotOutput {
		foreach ($path in @($stdout, $stderr)) {
			if (-not (Test-Path -LiteralPath $path)) { continue }
			$stream = [IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
			try {
				if ($stream.Length -le $shown[$path]) { continue }
				$stream.Position = $shown[$path]
				$reader = [IO.StreamReader]::new($stream)
				$text = $reader.ReadToEnd()
				$shown[$path] = $stream.Length
				if ($text.Length -gt 0) { [Console]::Out.Write($text) }
			}
			finally { $stream.Dispose() }
		}
	}
	while (-not $bot.HasExited) {
		Show-BotOutput
		Start-Sleep -Milliseconds 1000
	}
	$bot.WaitForExit()
	Show-BotOutput
	$botExitCode = $bot.ExitCode
}
catch { $failure = $_ }
finally {
	# Ctrl+C or an error here: make sure the companions leave the world before the evidence is collected.
	if ($null -ne $bot -and -not $bot.HasExited) {
		New-Item -ItemType File -Path $stopFile -Force | Out-Null
		if (-not $bot.WaitForExit(90000)) {
			Write-Warning 'The host did not exit within 90 seconds of the stop request; terminating the host process only.'
			$bot.Kill($true)
			$bot.WaitForExit()
		}
		$botExitCode = $bot.ExitCode
	}
	$endedAt = [DateTimeOffset]::UtcNow
	if (($null -eq $bot -or $bot.HasExited) -and (Test-Path -LiteralPath $botDirectory)) {
		Remove-Item -LiteralPath $botDirectory -Recurse -Force -ErrorAction SilentlyContinue
	}
	$report = [ordered]@{ schemaVersion = 1; scenario = 'CH-01'; run = $Run; target = $Target; gitSha = $gitSha
		startedUtc = $startedAt.ToString('O'); endedUtc = $endedAt.ToString('O')
		durationSeconds = [Math]::Round(($endedAt - $startedAt).TotalSeconds, 1); botExitCode = $botExitCode }
	try {
		$after = Get-AttachTarget $Target
		Write-HostJson (Join-Path $runPath 'target-after.json') $after
		$changes = @(Compare-AttachTarget $before $after)
		$report.worldLifecycleUnchanged = $changes.Count -eq 0
		$report.worldLifecycleChanges = $changes
		$report.serverLogs = Save-AttachLogs $after $startedAt $endedAt (Join-Path $runPath 'logs/containers')
	}
	catch { $report.evidenceError = $_.Exception.Message }
	$hostResult = Join-Path $runPath 'companion-host.json'
	if (Test-Path -LiteralPath $hostResult) { $report.host = Get-Content -Raw -LiteralPath $hostResult | ConvertFrom-Json }
	# The host has no end of its own: it runs until it is stopped, and 3 is its ordinary exit.
	$report.outcome = switch ($botExitCode) { 3 { 'stopped' } $null { 'not-started' } default { 'failed' } }
	if ($null -ne $failure) { $report.runnerError = $failure.Exception.Message }
	Write-HostJson (Join-Path $runPath 'companion-host-report.json') $report
	Write-Host ''
	Write-Host "Companion host $($report.outcome) after $([TimeSpan]::FromSeconds($report.durationSeconds).ToString('hh\:mm\:ss')); world lifecycle unchanged: $(if ($report.Contains('worldLifecycleUnchanged')) { $report.worldLifecycleUnchanged } else { 'unknown' })."
	Write-Host "Evidence: $runPath"
}

if ($null -ne $failure) { throw $failure }
if ($botExitCode -ne 3) { throw "The companion host failed with exit code $botExitCode; see $runPath" }
