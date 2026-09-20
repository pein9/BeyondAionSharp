# BA-001 diagnostic: normal L0 clients and the existing operator transfer queue.
# No invented bridge frames, scheduler acceleration, account reactivation or character repair.
Set-StrictMode -Version Latest

function Read-TransferRows([scriptblock]$Sql, [ValidateSet('aion_gs','aion_gs2')][string]$Database) {
	$players=@(& $Sql "SELECT JSON_OBJECT('id',id,'accountId',account_id,'name',name,'race',race,'class',player_class,'exp',exp,'world',world_id,'x',x,'y',y,'z',z,'online',online,'transferTime',last_transfer_time) FROM $Database.players ORDER BY id" | ForEach-Object { $_ | ConvertFrom-Json -DateKind String })
	$items=@(& $Sql "SELECT JSON_OBJECT('id',item_unique_id,'owner',item_owner,'template',item_id,'count',item_count,'equipped',is_equipped,'slot',slot,'location',item_location) FROM $Database.inventory ORDER BY item_unique_id" | ForEach-Object { $_ | ConvertFrom-Json -DateKind String })
	return [ordered]@{players=$players;inventory=$items}
}

function Assert-TransferSetup([object]$Source, [object]$Target, [object[]]$Accounts) {
	if ($Source.players.Count -ne 2 -or $Target.players.Count -ne 0 -or $Target.inventory.Count -ne 0) { throw 'Transfer requires exactly two source controls and an empty target world.' }
	if ($Source.inventory.Count -eq 0) { throw 'Source inventory evidence is empty.' }
	if (@($Source.players | ForEach-Object accountId | Sort-Object -Unique).Count -ne 2) { throw 'Transfer controls must use distinct subject accounts.' }
	foreach ($player in $Source.players) {
		foreach ($id in @($player.id,$player.accountId)) {
			if (($id -isnot [int] -and $id -isnot [long]) -or $id -le 0 -or $id -gt [int]::MaxValue) { throw 'Invalid transfer identity.' }
		}
		if ($player.online -ne 0) { throw 'Transfer setup player must be persisted and offline.' }
		$account=@($Accounts | Where-Object id -EQ $player.accountId)
		if ($account.Count -ne 1 -or $account[0].accessLevel -ne 0 -or $account[0].activated -ne 1) { throw 'Transfer subject must be an active ordinary account.' }
	}
}

function Invoke-LiveTransferAttempt {
	param([string]$Directory,[string]$Run,[string]$RepoRoot,[string]$GitSha,[scriptblock]$Sql,[scriptblock]$CheckWatcher)
	$result=[ordered]@{schemaVersion=1;run=$Run;passed=$false;botCount=2;status='setup';failure=$null;taskId=$null;
		before=$null;after=$null;samples=@();note='A successful queue status alone is not a complete transfer journey.'}
	$botProcess=$null
	try {
		$botDirectory=$Directory
		# Keep root traces/problems visible to the running watcher; retain the parent metadata separately.
		Copy-Item -LiteralPath (Join-Path $Directory 'bots-run.json') -Destination (Join-Path $Directory 'topology-run.json')
		# Reuse the full, proven L0 flow: two ordinary clients, create, enter, Chat, walk, quit, relog.
		$start=[Diagnostics.ProcessStartInfo]::new('dotnet'); $start.UseShellExecute=$false; $start.CreateNoWindow=$true
		$start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true; $start.WorkingDirectory=$RepoRoot
		foreach ($arg in @('tools/Aion.LiveBots/bin/Debug/net10.0/Aion.LiveBots.dll','--run',$Run,'--output',$botDirectory,
			'--scenario','L0','--bots','2','--step-timeout-seconds','30','--seed','1','--git-sha',$GitSha,'--profile','docker-cross-server-transfer','--time-zone','Etc/UTC')) { $start.ArgumentList.Add($arg) }
		$botProcess=[Diagnostics.Process]::Start($start)
		$stdout=$botProcess.StandardOutput.ReadToEndAsync(); $stderr=$botProcess.StandardError.ReadToEndAsync()
		try {
			$deadline=[DateTimeOffset]::UtcNow.AddMinutes(3)
			while (-not $botProcess.WaitForExit(200)) {
				& $CheckWatcher
				if ([DateTimeOffset]::UtcNow -gt $deadline) { throw 'Transfer L0 setup exceeded its deadline.' }
			}
			if ($botProcess.ExitCode -ne 0) { throw "Transfer L0 setup failed ($($botProcess.ExitCode))." }
		} finally {
			if (-not $botProcess.HasExited) { $botProcess.Kill($true); if (-not $botProcess.WaitForExit(10000)) { throw 'Transfer setup process cleanup failed.' } }
			[IO.File]::WriteAllText((Join-Path $Directory 'setup.stdout.log'),$stdout.GetAwaiter().GetResult())
			[IO.File]::WriteAllText((Join-Path $Directory 'setup.stderr.log'),$stderr.GetAwaiter().GetResult())
		}
		# The last L0 selection-screen connection closes when its process exits. Require DB offline state below.
		$source=Read-TransferRows $Sql 'aion_gs'; $target=Read-TransferRows $Sql 'aion_gs2'
		$accountQuery="SELECT JSON_OBJECT('id',id,'name',name,'accessLevel',access_level,'activated',activated) FROM aion_ls.account_data ORDER BY id"
		$accounts=@(& $Sql $accountQuery | ForEach-Object { $_ | ConvertFrom-Json })
		Assert-TransferSetup $source $target $accounts
		$result.before=[ordered]@{source=$source;target=$target;accounts=$accounts}
		$player=$source.players[0]
		# The only DB write is the real operator queue. IDs come from validated rows on this fresh owned stack.
		$queued=@(& $Sql "INSERT INTO aion_ls.player_transfers (source_server,target_server,source_account_id,target_account_id,player_id) SELECT 1,2,$([int]$player.accountId),$([int]$player.accountId),$([int]$player.id) WHERE NOT EXISTS (SELECT 1 FROM aion_ls.player_transfers); SELECT LAST_INSERT_ID();")
		if ($queued.Count -ne 1 -or $queued[0] -cnotmatch '^[1-9][0-9]*$') { throw 'Transfer queue did not return one task id.' }
		$taskId=[int]$queued[0]; $result.taskId=$taskId; $result.status='waiting-for-scheduler'
		Write-LifecycleJson (Join-Path $Directory 'transfer-request.json') ([ordered]@{taskId=$taskId;source=$player;sourceServer=1;targetServer=2;targetAccount=$player.accountId})
		Write-Host "BA-001: queued real task $taskId; waiting for the normal seven-minute scheduler (two setup bots have exited)."
		$samples=[Collections.Generic.List[object]]::new(); $activeAt=$null; $deadline=[DateTimeOffset]::UtcNow.AddSeconds(500)
		$lastStatus=-1
		do {
			& $CheckWatcher
			$rows=@(& $Sql "SELECT JSON_OBJECT('id',id,'status',status,'comment',comment) FROM aion_ls.player_transfers WHERE id=$taskId")
			if ($rows.Count -ne 1) { throw 'Transfer task disappeared.' }
			$task=$rows[0] | ConvertFrom-Json
			$samples.Add([ordered]@{at=[DateTimeOffset]::UtcNow.ToString('O');task=$task})
			if ($task.status -ne $lastStatus) { Write-Host "BA-001 task $taskId status: $($task.status)"; $lastStatus=$task.status }
			if ($task.status -eq 1 -and $null -eq $activeAt) { $activeAt=[DateTimeOffset]::UtcNow }
			if ($task.status -in @(2,3)) { break }
			if ($null -ne $activeAt -and ([DateTimeOffset]::UtcNow-$activeAt).TotalSeconds -ge 60) { break }
			Start-Sleep -Seconds 2
		} while ([DateTimeOffset]::UtcNow -lt $deadline)
		$result.samples=$samples.ToArray()
		$result.after=[ordered]@{source=(Read-TransferRows $Sql 'aion_gs');target=(Read-TransferRows $Sql 'aion_gs2');
			accounts=@(& $Sql $accountQuery | ForEach-Object { $_ | ConvertFrom-Json });task=$task}
		$result.status=switch ($task.status) { 0 {'scheduler-timeout'} 1 {'stalled-active'} 2 {'queue-done-needs-journey-verification'} 3 {'transfer-error'} default {'invalid-status'} }
		# This diagnostic must never claim the full BA-001 gate: target login, all data and control cases
		# still require the real journey. In particular, a stalled task is FAILED, not an expected pass.
		throw "BA-001 transfer diagnostic incomplete: $($result.status). See transfer-attempt.json."
	} catch { $result.failure=$_.Exception.ToString() }
	finally {
		if ($null -ne $botProcess) { $botProcess.Dispose() }
		Write-LifecycleJson (Join-Path $Directory 'transfer-attempt.json') $result
	}
	return [pscustomobject]$result
}
