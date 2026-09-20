# Pure planning and injectable execution. Loading this file never starts a process.
Set-StrictMode -Version Latest

function Get-FullSuitePlan {
	[CmdletBinding()]
	param(
		[Parameter(Mandatory)][object[]]$Manifest,
		[ValidateSet('Breadth', 'Soak', 'All')][string]$Suite = 'Breadth',
		[ValidateRange(1, 100)][int]$SimShards = 2,
		[ValidateSet(50, 200, 500)][int[]]$SoakBots = @(50, 200, 500),
		[ValidateRange(1, 7200)][int]$SoakSeconds = 7200
	)
	$ids = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
	foreach ($scenario in $Manifest) {
		if ($scenario.id -notmatch '^[a-zA-Z0-9][a-zA-Z0-9_-]*$' -or -not $ids.Add($scenario.id)) {
			throw "Invalid or duplicate scenario id: $($scenario.id)"
		}
		if ($scenario.tier -notin @('Fast', 'Full', 'Soak')) { throw "Unknown tier: $($scenario.tier)" }
	}
	if ($SoakBots.Count -eq 0 -or @($SoakBots | Select-Object -Unique).Count -ne $SoakBots.Count) {
		throw 'Select at least one distinct soak population.'
	}
	$steps = [Collections.Generic.List[object]]::new()
	if ($Suite -ne 'Soak') {
		$processes = [ordered]@{}
		$shared = 0
		foreach ($scenario in $Manifest) {
			if ($scenario.modes -notcontains 'Sim' -or $scenario.tier -eq 'Soak') { continue }
			$key = if ($scenario.resetEpoch) { "reset-$($scenario.id)" } else { 'shard-{0:D2}' -f ($shared++ % $SimShards) }
			if (-not $processes.Contains($key)) { $processes[$key] = [Collections.Generic.List[string]]::new() }
			$processes[$key].Add($scenario.id)
		}
		if ($processes.Count -eq 0) { throw 'The manifest contains no SIM breadth scenarios.' }
		foreach ($key in $processes.Keys) {
			$steps.Add([pscustomobject]@{ kind = 'Sim'; id = "sim-$($key.ToLowerInvariant())"; processKey = $key; scenarios = @($processes[$key]) })
		}
		$live = @($Manifest | Where-Object { $_.modes -contains 'Live' -and $_.tier -ne 'Soak' })
		if (@($live | Where-Object id -CEQ 'L0').Count -ne 1 -or
			@($Manifest | Where-Object { $_.id -ceq 'L0' -and $_.modes -contains 'Sim' }).Count -ne 1) {
			throw 'Breadth requires L0 in both SIM and LIVE for packet parity.'
		}
		# L0 supplies the comparison; canaries run last. New LIVE breadth scenarios
		# enter automatically, instead of silently falling outside a hard-coded list.
		$live = @($live | Where-Object id -CEQ 'L0') + @($live | Where-Object { $_.id -cne 'L0' -and $_.id -cne 'canaries' }) +
			@($live | Where-Object id -CEQ 'canaries')
		$timeouts = @{ C1 = 30; Q1 = 120; Q2 = 120; Q3 = 420; Q4P = 1800; Q4I = 1800; E1 = 360;
			E2 = 60; E3 = 60; E4 = 60; E5 = 90; E6 = 60; E7 = 60; CAPITAL = 600;
			S1 = 300; S2 = 180; S3 = 60; S4 = 60; S5 = 180; S6 = 180; S7 = 180;
			L0 = 15; B2 = 30; B2F = 180; B3 = 120; M1 = 15; M6 = 15; O1 = 1200; connect = 15; canaries = 15 }
		foreach ($scenario in $live) {
			$seconds = if ($timeouts.ContainsKey($scenario.id)) { $timeouts[$scenario.id] } else { 600 }
			$steps.Add([pscustomobject]@{ kind = 'Live'; id = "live-$($scenario.id.ToLowerInvariant())";
				scenario = $scenario.id; stepTimeoutSeconds = $seconds })
			if ($scenario.id -ceq 'L0') { $steps.Add([pscustomobject]@{ kind = 'PacketParity'; id = 'l0-packet-parity' }) }
		}
		$steps.Add([pscustomobject]@{ kind = 'QuestCoverage'; id = 'quest-coverage' })
	}
	if ($Suite -ne 'Breadth') {
		foreach ($count in $SoakBots) {
			$steps.Add([pscustomobject]@{ kind = 'Soak'; id = "soak-$count"; bots = $count; durationSeconds = $SoakSeconds })
		}
	}
	return $steps.ToArray()
}

function Invoke-FullSuitePlan {
	[CmdletBinding()]
	param(
		[Parameter(Mandatory)][object[]]$Plan,
		[Parameter(Mandatory)][scriptblock]$Execute,
		[Parameter(Mandatory)][scriptblock]$Record
	)
	if ($Plan.Count -eq 0) { throw 'Cannot execute an empty suite.' }
	foreach ($step in $Plan) {
		$started = [DateTimeOffset]::UtcNow
		$elapsed = [Diagnostics.Stopwatch]::StartNew()
		try { & $Execute $step }
		catch {
			& $Record ([pscustomobject]@{ id = $step.id; kind = $step.kind; status = 'Failed';
				startedUtc = $started.ToString('O'); durationSeconds = $elapsed.Elapsed.TotalSeconds; error = $_.Exception.ToString() })
			throw
		}
		& $Record ([pscustomobject]@{ id = $step.id; kind = $step.kind; status = 'Passed';
			startedUtc = $started.ToString('O'); durationSeconds = $elapsed.Elapsed.TotalSeconds; error = $null })
	}
}
