# CH-01: what the companion host asks of the world it attaches to (docs/companion-live-host.md). Loading this file has
# no side effects and it calls no Docker itself: it reads what attach-controller.ps1 read.
Set-StrictMode -Version Latest

# The same names as LiveBotRunner.CompanionSeatAccount; the server is told them in AION_COMPANION_ACCOUNTS, outright or
# by the pattern companion* (companion and then digits).
function Get-CompanionSeatAccounts([int]$Seats) {
	if ($Seats -lt 1 -or $Seats -gt 100) { throw 'The companion host has one to a hundred seats.' }
	return @(1..$Seats | ForEach-Object { "companion$_" })
}

# The Docker stack runs a host of its own, the service companionhost, on the same seat accounts. Two hosts would log
# the same seats in against each other, so this script's host is refused while that one runs.
function Assert-NoStackCompanionHost([object]$Target) {
	$stack = $Target.services.PSObject.Properties['companionhost']
	if ($null -ne $stack -and $stack.Value.running) {
		throw "The stack of '$($Target.project)' runs its own companion host (service companionhost). Stop that one first: docker compose -f docker/docker-compose.yml stop companionhost"
	}
}

# The game server makes a companion only when it was started with companions on, and only on an account it takes for a
# seat. How many seats the host may log in on this world: all it asks for when the world names the pattern, else as many
# as the world names outright from companion1 on. A host on a world that takes none would wait for ever, so it is refused
# before it starts.
function Get-CompanionSeatLimit([object]$Target, [int]$MaxSeats) {
	$accounts = @(Get-CompanionSeatAccounts $MaxSeats)
	$how = "Start the world with AION_COMPANIONS=true and AION_COMPANION_ACCOUNTS=companion* (RUNNING.md)."
	$environment = $Target.services.gameserver.environment
	$switch = $environment.PSObject.Properties['AION_COMPANIONS']
	$on = $false
	if ($null -eq $switch -or -not [bool]::TryParse([string]$switch.Value, [ref]$on) -or -not $on) {
		throw "The game server of '$($Target.project)' has companions off. $how"
	}
	$named = $environment.PSObject.Properties['AION_COMPANION_ACCOUNTS']
	$known = @(if ($null -ne $named) { ([string]$named.Value).Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_.Length -gt 0 } })
	if ($known -ccontains 'companion*') { return $MaxSeats }
	$limit = 0
	foreach ($account in $accounts) {
		if ($known -cnotcontains $account) { break }
		$limit++
	}
	if ($limit -eq 0) { throw "The game server of '$($Target.project)' takes none of the host's accounts for a seat. $how" }
	return $limit
}
