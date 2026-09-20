# One owned Chat SIGKILL; no application state edits, proxy packets or retry acceleration.
Set-StrictMode -Version Latest

function Get-ChatFaultContainer([string[]]$ComposeArguments, [string]$ProjectName) {
	Assert-LifecycleComposeArguments $ComposeArguments $ProjectName
	if ($ProjectName -cnotmatch '^aion-bots-[a-z0-9][a-z0-9_-]*$') { throw 'Chat faults require an isolated bot project.' }
	$ids=@(& docker @ComposeArguments ps --all --quiet chatserver)
	if ($LASTEXITCODE -ne 0 -or $ids.Count -ne 1 -or $ids[0] -cnotmatch '^[a-f0-9]{64}$') { throw 'Expected one exact Chat container.' }
	$objects=@((& docker inspect $ids[0]) | ConvertFrom-Json)
	if ($LASTEXITCODE -ne 0 -or $objects.Count -ne 1) { throw 'Chat inspection failed.' }
	$container=$objects[0]; $networks=@($container.NetworkSettings.Networks.PSObject.Properties)
	if ($container.Id -cne $ids[0] -or $container.Image -cnotmatch '^sha256:[a-f0-9]{64}$' -or
		-not $container.State.Running -or $container.State.Paused -or $container.RestartCount -ne 0 -or
		$container.Config.Labels.'com.docker.compose.project' -cne $ProjectName -or
		$container.Config.Labels.'com.docker.compose.service' -cne 'chatserver' -or
		$networks.Count -ne 1 -or $networks[0].Name -cne "${ProjectName}_default") { throw 'Chat ownership/state mismatch.' }
	return $container
}

function Assert-ChatFaultRequest([object]$Request, [string]$Run) {
	if ($Request.schemaVersion -ne 1 -or $Request.run -cne $Run -or $Request.subjects.Count -ne 2) { throw 'Invalid Chat fault request.' }
	foreach ($id in $Request.subjects) {
		if (($id -isnot [int] -and $id -isnot [long]) -or $id -le 0 -or $id -gt [int]::MaxValue) { throw 'Invalid Chat subject id.' }
	}
	if ($Request.subjects[0] -eq $Request.subjects[1]) { throw 'Chat subjects must be distinct.' }
}

function Assert-ChatOutageReceipt([object]$Receipt, [object]$Request, [string]$Run) {
	Assert-ChatFaultRequest $Request $Run
	if ($Receipt.schemaVersion -ne 1 -or $Receipt.run -cne $Run -or $Receipt.characterId -ne $Request.subjects[0] -or
		($Receipt.gameReplies -isnot [int] -and $Receipt.gameReplies -isnot [long]) -or $Receipt.gameReplies -lt 2 -or
		($Receipt.elapsedSeconds -isnot [double] -and $Receipt.elapsedSeconds -isnot [int] -and $Receipt.elapsedSeconds -isnot [long]) -or
		-not [double]::IsFinite($Receipt.elapsedSeconds) -or $Receipt.elapsedSeconds -lt 3 -or $Receipt.elapsedSeconds -gt 30) {
		throw 'Invalid bot outage observation.'
	}
}

function Read-ChatFaultLog([string[]]$ComposeArguments, [string]$Service, [DateTimeOffset]$Since) {
	$log=(& docker @ComposeArguments logs --since $Since.ToString('O') --no-color --no-log-prefix $Service | Out-String)
	if ($LASTEXITCODE -ne 0) { throw "Cannot read fresh $Service log." }
	return $log
}

function Invoke-ChatFault {
	param([string[]]$ComposeArguments,[string]$ProjectName,[string]$Run,[string]$RunDirectory,[object]$Request,[scriptblock]$CheckOwner)
	if ($ProjectName -cne "aion-bots-$Run") { throw 'Chat run/project mismatch.' }
	Assert-ChatFaultRequest $Request $Run
	& $CheckOwner
	$container=Get-ChatFaultContainer $ComposeArguments $ProjectName
	$game=Get-LifecycleContainer $ComposeArguments $ProjectName
	$armed=[DateTimeOffset]::FromUnixTimeSeconds([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())
	$deadline=$armed.AddSeconds(180)
	$planPath=Join-Path $RunDirectory 'chat-server-crash-plan.json'
	Write-LifecycleJson $planPath ([ordered]@{schemaVersion=1;run=$Run;project=$ProjectName;containerId=$container.Id;
		armedUtc=$armed.ToString('O');killDeadlineUtc=$armed.AddSeconds(30).ToString('O');recoveryDeadlineUtc=$deadline.ToString('O')})
	$hash=[Convert]::ToHexStringLower([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes([IO.File]::ReadAllText($planPath))))
	$receiptPath=Join-Path $RunDirectory 'chat-server-crash-armed.json'
	while (-not (Test-Path -LiteralPath $receiptPath)) {
		& $CheckOwner
		if ([DateTimeOffset]::UtcNow -gt $armed.AddSeconds(10)) { throw 'Watcher did not arm Chat fault.' }
		Start-Sleep -Milliseconds 100
	}
	$receipt=Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
	if ($receipt.schemaVersion -ne 1 -or $receipt.run -cne $Run -or $receipt.planSha256 -cne $hash) { throw 'Chat watcher receipt mismatch.' }
	& $CheckOwner
	$verified=Get-ChatFaultContainer $ComposeArguments $ProjectName
	if ($verified.Id -cne $container.Id -or $verified.Image -cne $container.Image -or
		$verified.State.StartedAt -cne $container.State.StartedAt -or [DateTimeOffset]::UtcNow -gt $armed.AddSeconds(25)) { throw 'Chat identity/deadline changed before kill.' }
	$killedAfter=[DateTimeOffset]::UtcNow
	& docker kill --signal KILL $container.Id | Out-Null
	if ($LASTEXITCODE -ne 0) { throw 'Chat SIGKILL failed.' }
	$dead=@((& docker inspect $container.Id) | ConvertFrom-Json)
	if ($LASTEXITCODE -ne 0 -or $dead.Count -ne 1 -or $dead[0].Id -cne $container.Id -or $dead[0].Image -cne $container.Image -or
		$dead[0].State.Running -or $dead[0].State.ExitCode -ne 137 -or $dead[0].State.OOMKilled) { throw 'Chat did not exit by planned SIGKILL.' }
	# Confirm the real GS reconnect supervisor has observed this outage before the negative auth request.
	while (-not (Read-ChatFaultLog $ComposeArguments 'gameserver' $killedAfter).Contains('Could not connect to chat server at')) {
		& $CheckOwner
		if ([DateTimeOffset]::UtcNow -gt $armed.AddSeconds(60)) { throw 'GS did not observe the Chat outage.' }
		Start-Sleep -Milliseconds 300
	}
	Write-LifecycleJson (Join-Path $RunDirectory 'chat-server-killed.json') ([ordered]@{schemaVersion=1;run=$Run;containerId=$container.Id;
		imageId=$container.Image;exitCode=137;killedAfterUtc=$killedAfter.ToString('O');gameId=$game.Id;gameStartedAt=$game.State.StartedAt})
	$outagePath=Join-Path $RunDirectory 'chat-outage-observed.json'
	while (-not (Test-Path -LiteralPath $outagePath)) {
		& $CheckOwner
		if ([DateTimeOffset]::UtcNow -gt $armed.AddSeconds(90)) { throw 'Bot did not prove the Chat outage.' }
		Start-Sleep -Milliseconds 100
	}
	$outage=Get-Content -LiteralPath $outagePath -Raw | ConvertFrom-Json
	Assert-ChatOutageReceipt $outage $Request $Run
	& $CheckOwner
	# The exact dead container must still be the stopped process selected above.
	$stillDead=@((& docker inspect $container.Id) | ConvertFrom-Json)
	if ($LASTEXITCODE -ne 0 -or $stillDead.Count -ne 1 -or $stillDead[0].Id -cne $container.Id -or
		$stillDead[0].Image -cne $container.Image -or $stillDead[0].State.Running -or $stillDead[0].State.ExitCode -ne 137 -or
		$stillDead[0].State.StartedAt -cne $container.State.StartedAt -or $stillDead[0].State.OOMKilled -or $stillDead[0].RestartCount -ne 0) { throw 'Dead Chat identity changed.' }
	& docker start $container.Id | Out-Null
	if ($LASTEXITCODE -ne 0) { throw 'Chat restart failed.' }
	while ($true) {
		& $CheckOwner
		if ([DateTimeOffset]::UtcNow -gt $deadline) { throw 'Chat recovery deadline exceeded.' }
		$restarted=Get-ChatFaultContainer $ComposeArguments $ProjectName
		$gameNow=Get-LifecycleContainer $ComposeArguments $ProjectName
		if ($restarted.Id -cne $container.Id -or $restarted.Image -cne $container.Image -or
			[DateTimeOffset]$restarted.State.StartedAt -le $killedAfter -or $gameNow.Id -cne $game.Id -or
			$gameNow.Image -cne $game.Image -or $gameNow.State.StartedAt -cne $game.State.StartedAt) { throw 'Chat recovery changed process ownership or restarted GS.' }
		$gameLog=Read-ChatFaultLog $ComposeArguments 'gameserver' $killedAfter
		$chatLog=Read-ChatFaultLog $ComposeArguments 'chatserver' $killedAfter
		$heartbeat=$null
		$heartbeatPath=Join-Path $RunDirectory 'logs/cs/cs.events.jsonl'
		if (Test-Path -LiteralPath $heartbeatPath) {
			foreach ($line in @(Get-Content -LiteralPath $heartbeatPath -Tail 100)) {
				try { $record=$line | ConvertFrom-Json -DateKind String } catch { continue } # A concurrently appended final line may be incomplete.
				if ($record.srv -ceq 'cs' -and $record.run -ceq $Run -and $record.tpl -clike 'Server heartbeat*' -and
					[DateTimeOffset]$record.ts -ge ([DateTimeOffset]$restarted.State.StartedAt).AddSeconds(1) -and
					[DateTimeOffset]$record.ts -gt [DateTimeOffset]::UtcNow.AddSeconds(-20) -and
					[DateTimeOffset]$record.ts -le [DateTimeOffset]::UtcNow) { $heartbeat=$record.ts }
			}
		}
		if ($gameLog.Contains('Authenticated with chat server;') -and $chatLog.Contains('Gameserver #1 is now online') -and $null -ne $heartbeat) { break }
		Start-Sleep -Milliseconds 300
	}
	Write-LifecycleJson (Join-Path $RunDirectory 'chat-server-restarted.json') ([ordered]@{schemaVersion=1;run=$Run;containerId=$container.Id;
		imageId=$container.Image;gameId=$game.Id;gameStartedAt=$game.State.StartedAt;startedAt=$restarted.State.StartedAt;
		heartbeatUtc=$heartbeat;readyUtc=[DateTimeOffset]::UtcNow.ToString('O')})
}

function Invoke-ChatFaultBot {
	param([string]$FileName,[string[]]$Arguments,[string[]]$ComposeArguments,[string]$ProjectName,[string]$Run,[string]$RunDirectory,[Diagnostics.Process]$Watcher)
	$start=[Diagnostics.ProcessStartInfo]::new($FileName); $start.UseShellExecute=$false; $start.CreateNoWindow=$true
	$start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
	foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
	$child=$null; $phase='waiting-for-request'; $failure=$null; $output=$null; $errors=$null
	$deadline=[DateTimeOffset]::UtcNow.AddMinutes(5)
	try {
		$child=[Diagnostics.Process]::Start($start)
		$output=$child.StandardOutput.ReadToEndAsync(); $errors=$child.StandardError.ReadToEndAsync()
		$checkOwner={
			if ($Watcher.HasExited) { throw 'Watcher exited during Chat fault.' }
			if ($child.HasExited -and $phase -ne 'restarted') { throw "Chat bot exited early ($($child.ExitCode))." }
			if ([DateTimeOffset]::UtcNow -gt $deadline) { throw 'Chat controller exceeded five minutes.' }
		}
		while (-not $child.HasExited) {
			& $checkOwner
			$path=Join-Path $RunDirectory 'chat-crash-request.json'
			if ($phase -eq 'waiting-for-request' -and (Test-Path -LiteralPath $path)) {
				$request=Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
				Assert-ChatFaultRequest $request $Run
				Invoke-ChatFault -ComposeArguments $ComposeArguments -ProjectName $ProjectName -Run $Run -RunDirectory $RunDirectory -Request $request -CheckOwner $checkOwner
				$phase='restarted'
			}
			Start-Sleep -Milliseconds 100
		}
		if ($child.ExitCode -ne 0 -or $phase -ne 'restarted') { throw "Chat fault bot failed ($($child.ExitCode)), phase $phase." }
		return 0
	} catch { $failure=$_.ToString(); throw }
	finally {
		if ($null -ne $child) {
			try {
				if (-not $child.HasExited) { $child.Kill($true); if (-not $child.WaitForExit(10000)) { throw 'Chat bot cleanup timed out.' } }
				if ($null -ne $output) { [IO.File]::WriteAllText((Join-Path $RunDirectory 'chat-fault-bot.stdout.log'),$output.GetAwaiter().GetResult()) }
				if ($null -ne $errors) { [IO.File]::WriteAllText((Join-Path $RunDirectory 'chat-fault-bot.stderr.log'),$errors.GetAwaiter().GetResult()) }
			} finally { $child.Dispose() }
		}
		Write-LifecycleJson (Join-Path $RunDirectory 'chat-fault-controller.json') ([ordered]@{schemaVersion=1;run=$Run;phase=$phase;failure=$failure})
	}
}
