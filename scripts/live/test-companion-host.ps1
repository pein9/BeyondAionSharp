# CH-01 companion host contract: the runner reads an operator's world, refuses one that makes no companions, and can
# never change it. Docker is a mock here; no server or bot is started.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '../e2e/run-artifact-owner.ps1')
. (Join-Path $PSScriptRoot 'attach-controller.ps1')
. (Join-Path $PSScriptRoot 'companion-host-controller.ps1')
$checks = 0
function Expect-Refused([scriptblock]$Action, [string]$Because) {
	$threw = $false
	try { & $Action | Out-Null } catch { $threw = $true }
	if (-not $threw) { throw "Accepted: $Because" }
	$script:checks++
}

# Static guard: neither the entry script nor its controller calls Docker; every read goes through the attach gate.
foreach ($file in @('companion-host.ps1', 'companion-host-controller.ps1')) {
	$ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot $file), [ref]$null, [ref]$null)
	$calls = @($ast.FindAll({ param($node) $node -is [Management.Automation.Language.CommandAst] -and $node.GetCommandName() -ceq 'docker' }, $true))
	if ($calls.Count -ne 0) { throw "$file calls docker directly $($calls.Count) times (expected 0)." }
	$checks++
}

# The seats are the accounts the host program logs in on (LiveBotRunner.CompanionSeatAccount).
if ((Get-CompanionSeatAccounts 5) -join ',' -cne 'companion1,companion2,companion3,companion4,companion5') { throw 'Unexpected seat accounts.' }
if ((Get-CompanionSeatAccounts 1) -join ',' -cne 'companion1') { throw 'Unexpected single seat account.' }
if (@(Get-CompanionSeatAccounts 100)[99] -cne 'companion100') { throw 'Unexpected hundredth seat account.' }
$checks += 3
Expect-Refused { Get-CompanionSeatAccounts 0 } 'a host with no seat'
Expect-Refused { Get-CompanionSeatAccounts 101 } 'more seats than the host has subject numbers for'
$program = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '../../tools/Aion.LiveBots/LiveCompanionHostScenario.cs')
if ($program -cnotmatch 'CompanionSeatAccount\(int seat\) => \$"companion\{seat\}";' -or $program -cnotmatch 'CompanionHostMaximumSeats = 100;') {
	throw 'The host program and the script no longer agree on the seat accounts.'
}
$checks++

function New-HostFixture([string]$Service, [string]$Letter, [hashtable]$Ports, [string[]]$Environment = @()) {
	$portMap = [ordered]@{}
	foreach ($key in $Ports.Keys) { $portMap[$key] = @([pscustomobject]@{ HostIp = '0.0.0.0'; HostPort = [string]$Ports[$key] }) }
	return [pscustomobject]@{
		Id = $Letter * 64; Name = "/aion-$Service"; Image = "sha256:$Letter"; RestartCount = 0
		State = [pscustomobject]@{ Running = $true; StartedAt = '2026-10-13T13:17:25Z' }
		Config = [pscustomobject]@{ Image = "aion-$Service"; Labels = [pscustomobject]@{ 'com.docker.compose.project' = 'aion'; 'com.docker.compose.service' = $Service }
			Env = @(@('DB_PASSWORD=secret-db', 'MYSQL_ROOT_PASSWORD=secret-root') + $Environment) }
		NetworkSettings = [pscustomobject]@{ Ports = [pscustomobject]$portMap }
		Mounts = @()
	}
}
$gameEnvironment = @()
function Get-HostFixtures {
	return @((New-HostFixture 'loginserver' 'b' @{ '2106/tcp' = 2106 }), (New-HostFixture 'gameserver' 'c' @{ '7777/tcp' = 7777 } $gameEnvironment),
		(New-HostFixture 'chatserver' 'd' @{ '10241/tcp' = 10241 }), (New-HostFixture 'mysql' 'e' @{ '3306/tcp' = 3306 }))
}
$dockerCalls = [Collections.Generic.List[string]]::new()
function docker {
	$dockerCalls.Add($args -join ' ')
	$global:LASTEXITCODE = 0
	switch -CaseSensitive ($args[0]) {
		'ps' {
			if ($args -notcontains 'label=com.docker.compose.project=aion') { throw 'Target selection must filter by the compose project.' }
			return @(Get-HostFixtures | ForEach-Object Id)
		}
		'inspect' { $requested = $args; return @(Get-HostFixtures | Where-Object { $requested -contains $_.Id }) | ConvertTo-Json -Depth 8 -AsArray }
		default { throw "Mock refused docker $($args -join ' ')" }
	}
}

# A world makes companions only when it was started with them on and takes the host's accounts for seats: all of them
# by the pattern companion*, or as many as it names outright from companion1 on.
$allSeats = 'AION_COMPANION_ACCOUNTS=companion*'
foreach ($off in @(@(), @('AION_COMPANIONS=false', $allSeats), @('AION_COMPANIONS=', $allSeats), @('AION_COMPANIONS=yes', $allSeats),
	@('AION_COMPANIONS=true'), @('AION_COMPANIONS=true', 'AION_COMPANION_ACCOUNTS='),
	@('AION_COMPANIONS=true', 'AION_COMPANION_ACCOUNTS=companion2,companion3'),
	@('AION_COMPANIONS=true', 'AION_COMPANION_ACCOUNTS=Companion*'),
	@('AION_COMPANIONS=true', 'AION_COMPANION_ACCOUNTS=Companion1,companion2'))) {
	$gameEnvironment = $off
	Expect-Refused { Get-CompanionSeatLimit (Get-AttachTarget 'aion') 50 } "a world started with [$($off -join ' ')]"
}
$gameEnvironment = @('AION_COMPANIONS=true', 'AION_COMPANION_ACCOUNTS=companion1,companion2,companion3,companion5')
if ((Get-CompanionSeatLimit (Get-AttachTarget 'aion') 50) -ne 3) { throw 'A world that names three seats in a row has three.' }
$gameEnvironment = @('AION_COMPANIONS=True', 'AION_COMPANION_ACCOUNTS= companion1 , companion* ')
$target = Get-AttachTarget 'aion'
if ((Get-CompanionSeatLimit $target 50) -ne 50 -or (Get-CompanionSeatLimit $target 7) -ne 7) { throw 'A world that names the pattern has as many seats as the host asks for.' }
$checks += 3
$serialized = $target | ConvertTo-Json -Depth 12
if ($serialized -match 'secret-') { throw 'Target evidence kept a secret environment value.' }
if ($serialized -notmatch 'AION_COMPANION_ACCOUNTS') { throw 'Target evidence lost the companion settings.' }
$checks += 2

# The entry script against the mock: a refused world leaves no run behind, and nothing but reads reaches Docker.
$runRoot = Join-Path ([IO.Path]::GetTempPath()) ('companion-host-test-' + [Guid]::NewGuid().ToString('N'))
try {
	$entry = Join-Path $PSScriptRoot 'companion-host.ps1'
	$gameEnvironment = @('AION_COMPANIONS=false')
	Expect-Refused { & $entry -Target aion -RunRoot $runRoot -Run refused -SkipBuild -DashboardPort 0 } 'a host on a world with companions off'
	$gameEnvironment = @('AION_COMPANIONS=true', 'AION_COMPANION_ACCOUNTS=somebody')
	Expect-Refused { & $entry -Target aion -RunRoot $runRoot -Run refused -MaxSeats 2 -SkipBuild -DashboardPort 0 } 'a host on a world that takes none of its accounts'
	if (Test-Path -LiteralPath (Join-Path $runRoot 'refused')) { throw 'A refused world left a run directory behind.' }
	$checks++
	Expect-Refused { & $entry -Target 'Aion;rm' -RunRoot $runRoot -SkipBuild } 'an invalid project name'
	Expect-Refused { & $entry -Target aion -RunRoot $runRoot -MaxSeats 101 -SkipBuild } 'a hundred and one seats'
	# Nothing is attached, so a stop has nothing to do and touches nothing.
	& $entry -Target aion -RunRoot $runRoot -Stop 6>$null | Out-Null
	if (Test-Path -LiteralPath $runRoot) { throw 'A stop with no host attached created its run root.' }
	$checks++
}
finally { if (Test-Path -LiteralPath $runRoot) { Remove-Item -LiteralPath $runRoot -Recurse -Force } }
if ($dockerCalls.Count -eq 0 -or @($dockerCalls | Where-Object { $_ -notmatch '^(ps|inspect) ' }).Count -ne 0) { throw 'The host runner issued a non-read Docker command.' }
$checks++

Write-Host "CH-01 companion host contract passed ($checks assertions); no Docker, servers or bots started."
