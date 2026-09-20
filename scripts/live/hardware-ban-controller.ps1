# P10-09 B4: exact seasonal fixtures and one owned Login restart. Docker MySQL only.
Set-StrictMode -Version Latest

function Get-HardwareContainer {
	param([string[]]$ComposeArguments,[string]$ProjectName,[ValidateSet('mysql','loginserver','gameserver','chatserver')][string]$Service)
	Assert-LifecycleComposeArguments $ComposeArguments $ProjectName
	if ($ProjectName -cnotmatch '^aion-bots-[a-z0-9][a-z0-9_-]*$') { throw 'Hardware journey requires an isolated bot project.' }
	$ids=@(& docker @ComposeArguments ps --all --quiet $Service)
	if ($LASTEXITCODE -ne 0 -or $ids.Count -ne 1 -or $ids[0] -cnotmatch '^[a-f0-9]{64}$') { throw "Expected one exact $Service container." }
	$objects=@((& docker inspect $ids[0]) | ConvertFrom-Json)
	if ($LASTEXITCODE -ne 0 -or $objects.Count -ne 1) { throw "Cannot inspect $Service." }
	$c=$objects[0]; $networks=@($c.NetworkSettings.Networks.PSObject.Properties)
	if ($c.Id -cne $ids[0] -or $c.Image -cnotmatch '^sha256:[a-f0-9]{64}$' -or -not $c.State.Running -or
		$c.State.Paused -or $c.State.OOMKilled -or $c.RestartCount -ne 0 -or
		$c.Config.Labels.'com.docker.compose.project' -cne $ProjectName -or $c.Config.Labels.'com.docker.compose.service' -cne $Service -or
		$networks.Count -ne 1 -or $networks[0].Name -cne "${ProjectName}_default") { throw "Invalid $Service ownership/state." }
	return $c
}

function Invoke-HardwareSql([string[]]$ComposeArguments,[string]$ProjectName,[string]$Query) {
	$c=Get-HardwareContainer $ComposeArguments $ProjectName 'mysql'
	$password=if ([string]::IsNullOrWhiteSpace($env:AION_BOT_DB_PASSWORD)) { 'aion-bots' } else { $env:AION_BOT_DB_PASSWORD }
	$result=@(& docker exec -e "MYSQL_PWD=$password" $c.Id mysql -uroot -Nse $Query)
	if ($LASTEXITCODE -ne 0) { throw 'Owned Docker hardware SQL failed.' }
	return $result
}

function New-HardwareBanFixture([int]$Year) {
	if ($Year -lt 2027 -or $Year -gt 2037) { throw 'Hardware fixture year must fit MySQL TIMESTAMP and both future seasons.' }
	$winter=[DateTimeOffset]::new($Year,1,15,17,0,0,[TimeSpan]::Zero).ToUnixTimeMilliseconds()
	$summer=[DateTimeOffset]::new($Year,7,15,16,0,0,[TimeSpan]::Zero).ToUnixTimeMilliseconds()
	return [pscustomobject]@{ year=$Year; mac=@(
		[pscustomobject]@{key='02-00-00-00-00-01';epoch=$winter;details='winter'},
		[pscustomobject]@{key='02-00-00-00-00-02';epoch=$summer;details='summer'});
		hdd=@([pscustomobject]@{key='E2E-B03';epoch=$winter},[pscustomobject]@{key='E2E-B04';epoch=$summer}) }
}

function Get-HardwareFixtureHash([object]$Fixture,[ValidateSet('mac','hdd')][string]$Kind) {
	# Independent encoding of production fingerprint v1; fixture keys are already ordinal-sorted.
	$stream=[IO.MemoryStream]::new(); $writer=[IO.BinaryWriter]::new($stream,[Text.Encoding]::Unicode,$true)
	try {
		$rows=@($Fixture.$Kind)
		$writer.Write([byte]1); $writer.Write([byte]$(if ($Kind -ceq 'mac') {9} else {10})); $writer.Write([int]$rows.Count)
		foreach ($row in $rows) {
			$writer.Write([int]$row.key.Length); $writer.Write([Text.Encoding]::Unicode.GetBytes($row.key)); $writer.Write([long]$row.epoch)
			if ($Kind -ceq 'mac') { $writer.Write([int]$row.details.Length); $writer.Write([Text.Encoding]::Unicode.GetBytes($row.details)) }
		}
		$writer.Flush()
		return [Convert]::ToHexStringLower([Security.Cryptography.SHA256]::HashData($stream.ToArray()))
	} finally { $writer.Dispose(); $stream.Dispose() }
}

function Read-HardwareDatabase([string[]]$ComposeArguments,[string]$ProjectName,[object]$Fixture) {
	$query="SELECT JSON_OBJECT('zone',@@session.time_zone,'mac',COALESCE((SELECT JSON_ARRAYAGG(JSON_OBJECT('key',address,'epoch',CAST(UNIX_TIMESTAMP(time)*1000 AS SIGNED),'details',details)) FROM aion_ls.banned_mac),JSON_ARRAY()),'hdd',COALESCE((SELECT JSON_ARRAYAGG(JSON_OBJECT('key',serial,'epoch',CAST(UNIX_TIMESTAMP(time)*1000 AS SIGNED))) FROM aion_ls.banned_hdd),JSON_ARRAY()))"
	$rows=@(Invoke-HardwareSql $ComposeArguments $ProjectName $query)
	if ($rows.Count -ne 1) { throw 'Expected one hardware database observation.' }
	$state=$rows[0] | ConvertFrom-Json
	Assert-HardwareDatabase $state $Fixture
	return $state
}

function Assert-HardwareDatabase([object]$State,[object]$Fixture) {
	if ($State.zone -cne 'America/New_York') { throw 'Hardware database session is not America/New_York.' }
	foreach ($kind in @('mac','hdd')) {
		if (@($State.$kind).Count -ne 2) { throw "Unexpected $kind row count." }
		foreach ($expected in $Fixture.$kind) {
			$matches=@($State.$kind | Where-Object key -CEQ $expected.key)
			if ($matches.Count -ne 1 -or $matches[0].epoch -ne $expected.epoch -or
				($kind -ceq 'mac' -and $matches[0].details -cne $expected.details)) { throw "Persisted $kind identity/epoch/details changed." }
		}
	}
}

function Initialize-HardwareBanFixture([string[]]$ComposeArguments,[string]$ProjectName,[string]$Run,[string]$RunDirectory) {
	if ($ProjectName -cne "aion-bots-$Run") { throw 'Hardware fixture run/project mismatch.' }
	$c=Get-HardwareContainer $ComposeArguments $ProjectName 'mysql'
	$all=@(& docker @ComposeArguments ps --all --quiet)
	if ($LASTEXITCODE -ne 0 -or $all.Count -ne 1 -or $all[0] -cne $c.Id) { throw 'Seed only before any server container exists.' }
	$empty=@(Invoke-HardwareSql $ComposeArguments $ProjectName 'SELECT (SELECT COUNT(*) FROM aion_ls.banned_mac)+(SELECT COUNT(*) FROM aion_ls.banned_hdd)')
	if ($empty.Count -ne 1 -or $empty[0] -cne '0') { throw 'Hardware fixture requires empty fresh ban tables.' }
	$fixture=New-HardwareBanFixture ([DateTimeOffset]::UtcNow.Year+1)
	$year=$fixture.year
	# Change this isolated DB's default BEFORE server pools open; no host clock or DB changes.
	$query="SET GLOBAL time_zone='America/New_York'; SET SESSION time_zone='America/New_York'; INSERT INTO aion_ls.banned_mac(address,time,details) VALUES ('02-00-00-00-00-01','$year-01-15 12:00:00','winter'),('02-00-00-00-00-02','$year-07-15 12:00:00','summer'); INSERT INTO aion_ls.banned_hdd(serial,time) VALUES ('E2E-B03','$year-01-15 12:00:00'),('E2E-B04','$year-07-15 12:00:00');"
	Invoke-HardwareSql $ComposeArguments $ProjectName $query | Out-Null
	$state=Read-HardwareDatabase $ComposeArguments $ProjectName $fixture
	Write-LifecycleJson (Join-Path $RunDirectory 'hardware-fixture.json') ([ordered]@{schemaVersion=1;run=$Run;fixture=$fixture;database=$state;
		macSha256=(Get-HardwareFixtureHash $fixture 'mac');hddSha256=(Get-HardwareFixtureHash $fixture 'hdd');seededUtc=[DateTimeOffset]::UtcNow.ToString('O')})
}

function Read-HardwareLogLines([string]$Path) {
	# Docker's bind-mounted producer is still writing. File.ReadLines defaults to FileShare.Read on Windows.
	$stream=[IO.File]::Open($Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
	$reader=[IO.StreamReader]::new($stream)
	try { while (-not $reader.EndOfStream) { $reader.ReadLine() } } finally { $reader.Dispose() }
}

function Read-HardwareSnapshots([string]$RunDirectory,[string]$Run,[object]$Fixture,[DateTimeOffset]$Since,[int]$AfterGeneration) {
	$path=Join-Path $RunDirectory 'logs/gs/gs.events.jsonl'
	if (-not (Test-Path -LiteralPath $path)) { return $null }
	$found=@{}
	foreach ($line in (Read-HardwareLogLines $path)) {
		try { $record=$line | ConvertFrom-Json -DateKind String } catch { continue } # Watcher independently rejects malformed records.
		if ($record.srv -cne 'gs' -or $record.run -cne $Run -or [DateTimeOffset]$record.ts -lt $Since -or
			$record.tpl -cnotlike 'Applied Login hardware-ban snapshot:*') { continue }
		if ($record.msg -cnotmatch '^Applied Login hardware-ban snapshot: kind=(mac|hdd), generation=([0-9]+), entries=([0-9]+), distinctEntries=([0-9]+), sha256=([a-f0-9]{64})$') { throw 'Invalid applied hardware snapshot diagnostic.' }
		$kind=$Matches[1]; $generation=[int]$Matches[2]; $entries=[int]$Matches[3]; $distinct=[int]$Matches[4]; $hash=$Matches[5]
		if ($generation -le $AfterGeneration) { continue }
		if ($entries -ne 2 -or $distinct -ne 2 -or $hash -cne (Get-HardwareFixtureHash $Fixture $kind)) { throw "Applied $kind snapshot differs from fixture." }
		$found[$kind]=[ordered]@{generation=$generation;sha256=$hash;ts=$record.ts;entries=$entries;distinctEntries=$distinct}
	}
	if ($found.Count -ne 2) { return $null }
	if ($found.mac.generation -ne $found.hdd.generation) { return $null }
	return [ordered]@{generation=$found.mac.generation;mac=$found.mac;hdd=$found.hdd}
}

function Assert-HardwareFaultRequest([object]$Request,[string]$Run) {
	if ($Request.schemaVersion -ne 1 -or $Request.run -cne $Run -or
		($Request.characterId -isnot [int] -and $Request.characterId -isnot [long]) -or $Request.characterId -le 0 -or
		$Request.characterId -gt [int]::MaxValue -or ($Request.refusedBots -join ',') -cne 'b01,b02,b03,b04') { throw 'Invalid hardware restart request.' }
}

function Assert-HardwareSameProcess([object]$Before,[object]$After) {
	if ($Before.Id -cne $After.Id -or $Before.Image -cne $After.Image -or $Before.State.StartedAt -cne $After.State.StartedAt) { throw 'An unfaulted hardware-journey process changed.' }
}

function Assert-HardwareOutage([object]$Receipt,[object]$Request,[string]$Run) {
	Assert-HardwareFaultRequest $Request $Run
	if ($Receipt.schemaVersion -ne 1 -or $Receipt.run -cne $Run -or $Receipt.characterId -ne $Request.characterId -or
		($Receipt.gameReplies -isnot [int] -and $Receipt.gameReplies -isnot [long]) -or $Receipt.gameReplies -lt 2 -or
		($Receipt.elapsedSeconds -isnot [double] -and $Receipt.elapsedSeconds -isnot [int] -and $Receipt.elapsedSeconds -isnot [long]) -or
		-not [double]::IsFinite($Receipt.elapsedSeconds) -or $Receipt.elapsedSeconds -lt 3 -or $Receipt.elapsedSeconds -gt 30) { throw 'Invalid live Game outage observation.' }
}

function Invoke-HardwareLoginFault {
	param([string[]]$ComposeArguments,[string]$ProjectName,[string]$Run,[string]$RunDirectory,[object]$Request,[scriptblock]$CheckOwner)
	if ($ProjectName -cne "aion-bots-$Run") { throw 'Hardware fault run/project mismatch.' }
	Assert-HardwareFaultRequest $Request $Run
	$fixtureReceipt=Get-Content -Raw -LiteralPath (Join-Path $RunDirectory 'hardware-fixture.json') | ConvertFrom-Json
	if ($fixtureReceipt.schemaVersion -ne 1 -or $fixtureReceipt.run -cne $Run) { throw 'Hardware fixture receipt mismatch.' }
	$fixture=New-HardwareBanFixture $fixtureReceipt.fixture.year
	$before=Read-HardwareSnapshots $RunDirectory $Run $fixture ([DateTimeOffset]$fixtureReceipt.seededUtc) 0
	if ($null -eq $before) { throw 'No complete initial hardware synchronization.' }
	$beforeDb=Read-HardwareDatabase $ComposeArguments $ProjectName $fixture
	$login=Get-HardwareContainer $ComposeArguments $ProjectName 'loginserver'
	$unchanged=@{}; foreach ($service in @('gameserver','chatserver','mysql')) { $unchanged[$service]=Get-HardwareContainer $ComposeArguments $ProjectName $service }
	$processEvidence=@{}
	foreach ($service in $unchanged.Keys) { $c=$unchanged[$service]; $processEvidence[$service]=[ordered]@{containerId=$c.Id;imageId=$c.Image;startedAt=$c.State.StartedAt} }
	$armed=[DateTimeOffset]::FromUnixTimeSeconds([DateTimeOffset]::UtcNow.ToUnixTimeSeconds()); $deadline=$armed.AddSeconds(180)
	$planPath=Join-Path $RunDirectory 'login-server-crash-plan.json'
	Write-LifecycleJson $planPath ([ordered]@{schemaVersion=1;run=$Run;project=$ProjectName;containerId=$login.Id;
		armedUtc=$armed.ToString('O');killDeadlineUtc=$armed.AddSeconds(30).ToString('O');recoveryDeadlineUtc=$deadline.ToString('O')})
	$hash=[Convert]::ToHexStringLower([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes([IO.File]::ReadAllText($planPath))))
	$receiptPath=Join-Path $RunDirectory 'login-server-crash-armed.json'
	while (-not (Test-Path -LiteralPath $receiptPath)) { & $CheckOwner; if ([DateTimeOffset]::UtcNow -gt $armed.AddSeconds(10)) { throw 'Watcher did not arm Login crash.' }; Start-Sleep -Milliseconds 100 }
	$receipt=Get-Content -Raw -LiteralPath $receiptPath | ConvertFrom-Json
	if ($receipt.schemaVersion -ne 1 -or $receipt.run -cne $Run -or $receipt.planSha256 -cne $hash) { throw 'Login watcher receipt mismatch.' }
	& $CheckOwner
	Assert-HardwareSameProcess $login (Get-HardwareContainer $ComposeArguments $ProjectName 'loginserver')
	if ([DateTimeOffset]::UtcNow -gt $armed.AddSeconds(25)) { throw 'Login kill deadline expired.' }
	$killedAfter=[DateTimeOffset]::UtcNow
	& docker kill --signal KILL $login.Id | Out-Null
	if ($LASTEXITCODE -ne 0) { throw 'Login SIGKILL failed.' }
	$dead=@((& docker inspect $login.Id) | ConvertFrom-Json)
	if ($LASTEXITCODE -ne 0 -or $dead.Count -ne 1 -or $dead[0].Id -cne $login.Id -or $dead[0].Image -cne $login.Image -or
		$dead[0].State.Running -or $dead[0].State.ExitCode -ne 137 -or $dead[0].State.OOMKilled) { throw 'Login did not stop by planned SIGKILL.' }
	# A failed-connect log is not guaranteed: an EOF is silent and a pending OS connect can outlast the outage.
	# The bot proves its Game socket remains responsive while this exact Login container is stopped.
	Write-LifecycleJson (Join-Path $RunDirectory 'login-server-killed.json') ([ordered]@{schemaVersion=1;run=$Run;containerId=$login.Id;imageId=$login.Image;exitCode=137;killedAfterUtc=$killedAfter.ToString('O');unfaultedProcesses=$processEvidence})
	$outagePath=Join-Path $RunDirectory 'login-outage-observed.json'
	while (-not (Test-Path -LiteralPath $outagePath)) { & $CheckOwner; if ([DateTimeOffset]::UtcNow -gt $armed.AddSeconds(90)) { throw 'Control did not observe live Game during Login outage.' }; Start-Sleep -Milliseconds 100 }
	$outage=Get-Content -Raw -LiteralPath $outagePath | ConvertFrom-Json
	Assert-HardwareOutage $outage $Request $Run
	& $CheckOwner
	$stillDead=@((& docker inspect $login.Id) | ConvertFrom-Json)
	if ($LASTEXITCODE -ne 0 -or $stillDead.Count -ne 1 -or $stillDead[0].Id -cne $login.Id -or $stillDead[0].Image -cne $login.Image -or
		$stillDead[0].State.Running -or $stillDead[0].State.ExitCode -ne 137 -or $stillDead[0].State.OOMKilled -or
		$stillDead[0].State.StartedAt -cne $login.State.StartedAt -or $stillDead[0].RestartCount -ne 0) { throw 'Stopped Login identity changed.' }
	& docker start $login.Id | Out-Null
	if ($LASTEXITCODE -ne 0) { throw 'Login restart failed.' }
	$after=$null
	while ([DateTimeOffset]::UtcNow -lt $deadline) {
		& $CheckOwner
		$restarted=Get-HardwareContainer $ComposeArguments $ProjectName 'loginserver'
		if ($restarted.Id -cne $login.Id -or $restarted.Image -cne $login.Image -or [DateTimeOffset]$restarted.State.StartedAt -le $killedAfter) { throw 'Login restart identity changed.' }
		foreach ($service in $unchanged.Keys) { Assert-HardwareSameProcess $unchanged[$service] (Get-HardwareContainer $ComposeArguments $ProjectName $service) }
		$after=Read-HardwareSnapshots $RunDirectory $Run $fixture ([DateTimeOffset]$restarted.State.StartedAt) $before.generation
		$heartbeat=$null
		foreach ($line in @(Get-Content -LiteralPath (Join-Path $RunDirectory 'logs/ls/ls.events.jsonl') -Tail 100)) {
			try { $record=$line | ConvertFrom-Json -DateKind String } catch { continue }
			if ($record.srv -ceq 'ls' -and $record.run -ceq $Run -and $record.tpl -clike 'Server heartbeat*' -and
				[DateTimeOffset]$record.ts -ge ([DateTimeOffset]$restarted.State.StartedAt).AddSeconds(1) -and
				[DateTimeOffset]$record.ts -gt [DateTimeOffset]::UtcNow.AddSeconds(-20) -and [DateTimeOffset]$record.ts -le [DateTimeOffset]::UtcNow) { $heartbeat=$record.ts }
		}
		if ($null -ne $after -and $null -ne $heartbeat) { break }
		Start-Sleep -Milliseconds 300
	}
	if ($null -eq $after -or $null -eq $heartbeat) { throw 'No fresh Login heartbeat and complete hardware synchronization before deadline.' }
	$afterDb=Read-HardwareDatabase $ComposeArguments $ProjectName $fixture
	Write-LifecycleJson (Join-Path $RunDirectory 'login-server-restarted.json') ([ordered]@{schemaVersion=1;run=$Run;containerId=$login.Id;imageId=$login.Image;
		startedAt=$restarted.State.StartedAt;heartbeatUtc=$heartbeat;before=$before;after=$after;beforeDatabase=$beforeDb;afterDatabase=$afterDb;unfaultedProcesses=$processEvidence;readyUtc=[DateTimeOffset]::UtcNow.ToString('O')})
}

function Invoke-HardwareBanBot {
	param([string]$FileName,[string[]]$Arguments,[string[]]$ComposeArguments,[string]$ProjectName,[string]$Run,[string]$RunDirectory,[Diagnostics.Process]$Watcher)
	$start=[Diagnostics.ProcessStartInfo]::new($FileName); $start.UseShellExecute=$false; $start.CreateNoWindow=$true
	$start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
	foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
	$child=$null; $phase='waiting-for-request'; $failure=$null; $output=$null; $errors=$null; $deadline=[DateTimeOffset]::UtcNow.AddMinutes(6)
	try {
		$child=[Diagnostics.Process]::Start($start); $output=$child.StandardOutput.ReadToEndAsync(); $errors=$child.StandardError.ReadToEndAsync()
		$checkOwner={ if ($Watcher.HasExited) { throw 'Watcher exited during hardware journey.' }; if ($child.HasExited -and $phase -ne 'restarted') { throw "Hardware bot exited early ($($child.ExitCode))." }; if ([DateTimeOffset]::UtcNow -gt $deadline) { throw 'Hardware controller exceeded six minutes.' } }
		while (-not $child.HasExited) {
			& $checkOwner
			$path=Join-Path $RunDirectory 'login-crash-request.json'
			if ($phase -eq 'waiting-for-request' -and (Test-Path -LiteralPath $path)) {
				$request=Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
				Invoke-HardwareLoginFault $ComposeArguments $ProjectName $Run $RunDirectory $request $checkOwner
				$phase='restarted'
			}
			Start-Sleep -Milliseconds 100
		}
		if ($child.ExitCode -ne 0 -or $phase -ne 'restarted') { throw "Hardware bot failed ($($child.ExitCode)), phase $phase." }
		return 0
	} catch { $failure=$_.ToString(); throw }
	finally {
		if ($null -ne $child) {
			try {
				if (-not $child.HasExited) { $child.Kill($true); if (-not $child.WaitForExit(10000)) { throw 'Hardware bot cleanup timed out.' } }
				if ($null -ne $output) { [IO.File]::WriteAllText((Join-Path $RunDirectory 'hardware-bot.stdout.log'),$output.GetAwaiter().GetResult()) }
				if ($null -ne $errors) { [IO.File]::WriteAllText((Join-Path $RunDirectory 'hardware-bot.stderr.log'),$errors.GetAwaiter().GetResult()) }
			} finally { $child.Dispose() }
		}
		Write-LifecycleJson (Join-Path $RunDirectory 'hardware-controller.json') ([ordered]@{schemaVersion=1;run=$Run;phase=$phase;failure=$failure})
	}
}
