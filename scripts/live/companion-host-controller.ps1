# CH-01: what the companion host asks of the world it attaches to (docs/companion-live-host.md). Loading this file has
# no side effects and it calls no Docker itself: it reads what attach-controller.ps1 read.
Set-StrictMode -Version Latest

# The same names as LiveBotRunner.CompanionSeatAccount; the server is told them in AION_COMPANION_ACCOUNTS.
function Get-CompanionSeatAccounts([int]$Seats) {
	if ($Seats -lt 1 -or $Seats -gt 5) { throw 'The companion host has one to five seats.' }
	return @(1..$Seats | ForEach-Object { "companion$_" })
}

# The game server makes a companion only when it was started with companions on, and only on an account it was told
# is a seat. A host on a world without them would wait for ever, so it is refused before it starts.
function Assert-CompanionHostTarget([object]$Target, [int]$Seats) {
	$accounts = @(Get-CompanionSeatAccounts $Seats)
	$how = "Start the world with AION_COMPANIONS=true and AION_COMPANION_ACCOUNTS=$($accounts -join ',') (RUNNING.md)."
	$environment = $Target.services.gameserver.environment
	$switch = $environment.PSObject.Properties['AION_COMPANIONS']
	$on = $false
	if ($null -eq $switch -or -not [bool]::TryParse([string]$switch.Value, [ref]$on) -or -not $on) {
		throw "The game server of '$($Target.project)' has companions off. $how"
	}
	$named = $environment.PSObject.Properties['AION_COMPANION_ACCOUNTS']
	$known = @(if ($null -ne $named) { ([string]$named.Value).Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_.Length -gt 0 } })
	$missing = @($accounts | Where-Object { $known -cnotcontains $_ })
	if ($missing.Count -gt 0) {
		throw "The game server of '$($Target.project)' does not know the seat account(s) $($missing -join ', '). $how"
	}
}
