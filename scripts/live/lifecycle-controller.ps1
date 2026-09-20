# P10-03: one owned game-server SIGKILL, with read-only Docker SQL assertions.
Set-StrictMode -Version Latest

function Write-LifecycleJson([string]$Path, [object]$Value) {
	$json = $Value | ConvertTo-Json -Depth 10
	[IO.File]::WriteAllText($Path + '.tmp', $json, [Text.UTF8Encoding]::new($false))
	[IO.File]::Move($Path + '.tmp', $Path) # Unique run artifacts; never overwrite a prior phase.
}

function Assert-LifecyclePositionRequest([object]$Request, [string]$Run) {
	if ($Request.schemaVersion -ne 1 -or $Request.run -cne $Run -or
		($Request.characterId -isnot [long] -and $Request.characterId -isnot [int]) -or
		$Request.characterId -le 0 -or $Request.characterId -gt [int]::MaxValue -or
		$Request.characterName -cnotmatch '^[A-Za-z]{3,50}$' -or $Request.worldId -ne 220010000) {
		throw 'Invalid lifecycle character identity.'
	}
	foreach ($point in @($Request.initial, $Request.saved)) {
		foreach ($axis in @('x', 'y', 'z')) {
			$value = $point.$axis
			if ($value -is [string] -or $value -is [bool] -or $null -eq $value -or
				-not [double]::IsFinite([double]$value) -or [Math]::Abs([double]$value) -gt 10000) { throw 'Invalid lifecycle position.' }
		}
	}
	if (Test-LifecyclePosition $Request.initial $Request.saved) { throw 'Lifecycle movement did not change position.' }
}

function Test-LifecyclePosition([object]$Actual, [object]$Expected) {
	return [Math]::Abs([double]$Actual.x - [double]$Expected.x) -le 0.1 -and
		[Math]::Abs([double]$Actual.y - [double]$Expected.y) -le 0.1 -and
		[Math]::Abs([double]$Actual.z - [double]$Expected.z) -le 0.1
}

function Get-LifecycleContainer([string[]]$ComposeArguments, [string]$ProjectName) {
	if ($ProjectName -cnotmatch '^aion-bots-[a-z0-9][a-z0-9_-]*$') { throw 'Lifecycle faults require an isolated bot project.' }
	Assert-LifecycleComposeArguments $ComposeArguments $ProjectName
	$ids = @(& docker @ComposeArguments ps --all --quiet gameserver)
	if ($LASTEXITCODE -ne 0 -or $ids.Count -ne 1 -or $ids[0] -cnotmatch '^[0-9a-f]{64}$') { throw 'Expected one exact game-server container id.' }
	$objects = @((& docker inspect $ids[0]) | ConvertFrom-Json)
	if ($LASTEXITCODE -ne 0 -or $objects.Count -ne 1) { throw 'Game-server inspection failed.' }
	$container = $objects[0]
	$networks = @($container.NetworkSettings.Networks.PSObject.Properties)
	if ($container.Id -cne $ids[0] -or -not $container.State.Running -or
		$container.Config.Labels.'com.docker.compose.project' -cne $ProjectName -or
		$container.Config.Labels.'com.docker.compose.service' -cne 'gameserver' -or
		$networks.Count -ne 1 -or $networks[0].Name -cne "${ProjectName}_default") {
		throw 'Game-server container is not running exclusively in this isolated project.'
	}
	return $container
}

function Assert-LifecycleComposeArguments([string[]]$Arguments, [string]$ProjectName) {
	if ($Arguments.Count -notin @(5, 7) -or $Arguments[0] -cne 'compose' -or
		$Arguments[1] -cne '-f' -or [string]::IsNullOrWhiteSpace($Arguments[2]) -or
		$Arguments[3] -cne '-p' -or $Arguments[4] -cne $ProjectName -or
		($Arguments.Count -eq 7 -and ($Arguments[5] -cne '--profile' -or $Arguments[6] -cne 'bot-runner'))) {
		throw 'Lifecycle Docker arguments must select only the owning isolated compose project.'
	}
}

function Read-LifecyclePlayerRow([string[]]$ComposeArguments, [int]$CharacterId) {
	if ($CharacterId -le 0) { throw 'A positive character id is required.' }
	$password = if ([string]::IsNullOrWhiteSpace($env:AION_BOT_DB_PASSWORD)) { 'aion-bots' } else { $env:AION_BOT_DB_PASSWORD }
	$query = "SELECT JSON_OBJECT('id',id,'name',name,'worldId',world_id,'x',x,'y',y,'z',z,'online',online) FROM aion_gs.players WHERE id=$CharacterId"
	$rows = @(& docker @ComposeArguments exec -T -e "MYSQL_PWD=$password" mysql mysql -uroot -Nse $query)
	if ($LASTEXITCODE -ne 0 -or $rows.Count -ne 1) { throw 'Expected exactly one Docker player row.' }
	return ($rows[0] | ConvertFrom-Json)
}

function Wait-LifecycleRestart {
	param([string[]]$ComposeArguments, [string]$ProjectName, [object]$ExpectedContainer,
		[DateTimeOffset]$KilledAfter, [DateTimeOffset]$Deadline, [scriptblock]$CheckOwner)
	# Docker filters on log ingestion timestamps, not timestamp-like text in application messages.
	# A listening socket or a startup line from the first boot is not recovery evidence.
	while ([DateTimeOffset]::UtcNow -lt $Deadline) {
		& $CheckOwner
		$container = Get-LifecycleContainer $ComposeArguments $ProjectName
		if ($container.Id -cne $ExpectedContainer.Id -or $container.Image -cne $ExpectedContainer.Image) {
			throw 'Game-server identity changed during restart.'
		}
		if ([DateTimeOffset]$container.State.StartedAt -le $KilledAfter) { throw 'Game-server process has not restarted.' }
		$gameLog = (& docker @ComposeArguments logs --since $KilledAfter.ToString('O') --no-color --no-log-prefix gameserver | Out-String)
		if ($LASTEXITCODE -ne 0) { throw 'Could not read fresh game-server startup logs.' }
		$loginLog = (& docker @ComposeArguments logs --since $KilledAfter.ToString('O') --no-color --no-log-prefix loginserver | Out-String)
		if ($LASTEXITCODE -ne 0) { throw 'Could not read fresh login-server registration logs.' }
		if ($gameLog.Contains('Game server started in ') -and $loginLog.Contains('Gameserver #1 is now online')) {
			return [ordered]@{ startedUtc = $container.State.StartedAt; logsSinceUtc = $KilledAfter.ToString('O');
				gameStartup = $true; loginRegistration = $true }
		}
		Start-Sleep -Milliseconds 500
	}
	throw 'Fresh game-server startup and login registration were not observed before the recovery deadline.'
}

function Invoke-LifecycleCrash {
	param([string[]]$ComposeArguments, [string]$ProjectName, [string]$Run, [string]$RunDirectory,
		[scriptblock]$CheckOwner)
	if ($ProjectName -cne "aion-bots-$Run") { throw 'Lifecycle run/project mismatch.' }
	& $CheckOwner
	$container = Get-LifecycleContainer $ComposeArguments $ProjectName
	$armed = [DateTimeOffset]::FromUnixTimeSeconds([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())
	$planPath = Join-Path $RunDirectory 'game-server-crash-plan.json'
	Write-LifecycleJson $planPath ([ordered]@{ schemaVersion = 1; run = $Run; project = $ProjectName;
		containerId = $container.Id; armedUtc = $armed.ToString('O'); killDeadlineUtc = $armed.AddSeconds(30).ToString('O');
		recoveryDeadlineUtc = $armed.AddSeconds(180).ToString('O') })
	$hash = [Convert]::ToHexStringLower([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes([IO.File]::ReadAllText($planPath))))
	$receiptPath = Join-Path $RunDirectory 'game-server-crash-armed.json'
	while (-not (Test-Path -LiteralPath $receiptPath)) {
		& $CheckOwner
		if ([DateTimeOffset]::UtcNow -gt $armed.AddSeconds(10)) { throw 'Watcher did not arm the exact crash plan.' }
		Start-Sleep -Milliseconds 100
	}
	$receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
	if ($receipt.schemaVersion -ne 1 -or $receipt.run -cne $Run -or $receipt.planSha256 -cne $hash) { throw 'Watcher crash-plan receipt mismatch.' }
	& $CheckOwner
	$verified = Get-LifecycleContainer $ComposeArguments $ProjectName
	if ($verified.Id -cne $container.Id -or $verified.Image -cne $container.Image -or [DateTimeOffset]::UtcNow -gt $armed.AddSeconds(25)) {
		throw 'Game-server identity changed or kill deadline expired before injection.'
	}
	& $CheckOwner
	$killedAfter = [DateTimeOffset]::UtcNow
	& docker kill --signal KILL $container.Id | Out-Null
	if ($LASTEXITCODE -ne 0) { throw 'Planned Docker SIGKILL failed.' }
	$dead = @((& docker inspect $container.Id) | ConvertFrom-Json)
	if ($LASTEXITCODE -ne 0 -or $dead.Count -ne 1 -or $dead[0].Id -cne $container.Id -or $dead[0].Image -cne $container.Image -or $dead[0].State.Running -or
		$dead[0].State.ExitCode -ne 137 -or $dead[0].State.OOMKilled) { throw 'Game server did not exit by the planned SIGKILL.' }
	Write-LifecycleJson (Join-Path $RunDirectory 'game-server-killed.json') ([ordered]@{
		schemaVersion = 1; run = $Run; containerId = $container.Id; imageId = $container.Image;
		planSha256 = $hash; killedAfterUtc = $killedAfter.ToString('O'); observedUtc = [DateTimeOffset]::UtcNow.ToString('O'); exitCode = 137 })
	& docker start $container.Id | Out-Null
	if ($LASTEXITCODE -ne 0) { throw 'Game-server restart failed.' }
	$readiness = Wait-LifecycleRestart -ComposeArguments $ComposeArguments -ProjectName $ProjectName -ExpectedContainer $container `
		-KilledAfter $killedAfter -Deadline $armed.AddSeconds(180) -CheckOwner $CheckOwner
	& $CheckOwner
	$restarted = Get-LifecycleContainer $ComposeArguments $ProjectName
	if ($restarted.Id -cne $container.Id -or $restarted.Image -cne $container.Image -or [DateTimeOffset]::UtcNow -gt $armed.AddSeconds(180)) {
		throw 'Game-server restart identity or deadline mismatch.'
	}
	Write-LifecycleJson (Join-Path $RunDirectory 'game-server-restarted.json') ([ordered]@{
		schemaVersion = 1; run = $Run; containerId = $container.Id; imageId = $container.Image;
		readyUtc = [DateTimeOffset]::UtcNow.ToString('O'); readiness = $readiness })
}

function Invoke-LifecycleBot {
	param([string]$FileName, [string[]]$Arguments, [string[]]$ComposeArguments, [string]$ProjectName,
		[string]$Run, [string]$RunDirectory, [Diagnostics.Process]$Watcher)
	if ($ProjectName -cne "aion-bots-$Run" -or $Run -cnotmatch '^[a-z0-9][a-z0-9_-]*$') { throw 'Lifecycle run/project mismatch.' }
	Assert-LifecycleComposeArguments $ComposeArguments $ProjectName
	$start = [Diagnostics.ProcessStartInfo]::new($FileName)
	$start.UseShellExecute = $false; $start.CreateNoWindow = $true
	$start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
	foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
	$stdout = [IO.FileStream]::new((Join-Path $RunDirectory 'lifecycle-bot.stdout.log'), [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
	$stderr = [IO.FileStream]::new((Join-Path $RunDirectory 'lifecycle-bot.stderr.log'), [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
	$child = $null; $phase = 'waiting-for-position'; $failure = $null; $request = $null
	$outputCopy = $null; $errorCopy = $null
	$samples = [Collections.Generic.List[object]]::new()
	$clock = [Diagnostics.Stopwatch]::StartNew(); $nextSample = 0.0
	try {
		$child = [Diagnostics.Process]::Start($start)
		if ($null -eq $child) { throw 'Could not start lifecycle bot.' }
		$outputCopy = $child.StandardOutput.BaseStream.CopyToAsync($stdout)
		$errorCopy = $child.StandardError.BaseStream.CopyToAsync($stderr)
		$checkOwner = {
			if ($Watcher.HasExited) { throw 'Watcher exited during lifecycle test.' }
			if ($child.HasExited -and $phase -ne 'restarted') { throw "Lifecycle bot exited early ($($child.ExitCode))." }
			if ($clock.Elapsed.TotalSeconds -gt 1500) { throw 'Lifecycle controller exceeded 25 minutes.' }
		}
		while (-not $child.HasExited) {
			& $checkOwner
			$positionPath = Join-Path $RunDirectory 'lifecycle-position.json'
			if ($phase -eq 'waiting-for-position' -and (Test-Path -LiteralPath $positionPath)) {
				$request = Get-Content -LiteralPath $positionPath -Raw | ConvertFrom-Json
				Assert-LifecyclePositionRequest $request $Run
				$phase = 'waiting-for-periodic-save'
			}
			if ($phase -eq 'waiting-for-periodic-save' -and $clock.Elapsed.TotalSeconds -ge $nextSample) {
				$row = Read-LifecyclePlayerRow $ComposeArguments $request.characterId
				if ($row.id -ne $request.characterId -or $row.name -cne $request.characterName -or $row.worldId -ne $request.worldId -or $row.online -ne 1) { throw 'Lifecycle subject identity/online state changed.' }
				$samples.Add([ordered]@{ observedUtc = [DateTimeOffset]::UtcNow.ToString('O'); row = $row })
				if ($samples.Count -eq 1 -and -not (Test-LifecyclePosition $row $request.initial)) { throw 'First database observation is not the pre-movement position.' }
				if (Test-LifecyclePosition $row $request.saved) {
					if ($samples.Count -lt 2) { throw 'No delayed persistence transition observed.' }
					Write-LifecycleJson (Join-Path $RunDirectory 'lifecycle-saved.json') ([ordered]@{ schemaVersion = 1; run = $Run; row = $row; observedUtc = [DateTimeOffset]::UtcNow.ToString('O') })
					$phase = 'waiting-for-crash-request'
					Write-Host 'Lifecycle: ordinary periodic position save observed; waiting for unsaved movement.'
				}
				$nextSample = $clock.Elapsed.TotalSeconds + 5
			}
			$crashPath = Join-Path $RunDirectory 'game-server-crash-request.json'
			if ($phase -eq 'waiting-for-crash-request' -and (Test-Path -LiteralPath $crashPath)) {
				$crash = Get-Content -LiteralPath $crashPath -Raw | ConvertFrom-Json
				Assert-LifecyclePositionRequest $crash $Run
				if ($crash.characterId -ne $request.characterId -or $crash.characterName -cne $request.characterName -or
					-not (Test-LifecyclePosition $crash.initial $request.saved)) { throw 'Crash request does not continue the saved checkpoint.' }
				$row = Read-LifecyclePlayerRow $ComposeArguments $request.characterId
				if ($row.id -ne $request.characterId -or $row.name -cne $request.characterName -or $row.worldId -ne $request.worldId -or
					-not (Test-LifecyclePosition $row $request.saved) -or $row.online -ne 1) { throw 'Unsaved movement was already persisted or subject identity/online state changed.' }
				Write-LifecycleJson (Join-Path $RunDirectory 'lifecycle-before-kill.json') $row
				Invoke-LifecycleCrash -ComposeArguments $ComposeArguments -ProjectName $ProjectName -Run $Run -RunDirectory $RunDirectory -CheckOwner $checkOwner
				$phase = 'restarted'
			}
			Start-Sleep -Milliseconds 100
		}
		$child.WaitForExit(); $outputCopy.GetAwaiter().GetResult() | Out-Null; $errorCopy.GetAwaiter().GetResult() | Out-Null
		if ($child.ExitCode -ne 0) { throw "Lifecycle bot failed ($($child.ExitCode)); see lifecycle-bot.stderr.log." }
		if ($phase -ne 'restarted') { throw 'Lifecycle bot ended without the required crash/restart.' }
		return 0
	} catch { $failure = $_; throw }
	finally {
		$cleanupErrors = [Collections.Generic.List[string]]::new()
		if ($null -ne $child) {
			try {
				if (-not $child.HasExited) { $child.Kill($true); $child.WaitForExit() }
				foreach ($copy in @($outputCopy, $errorCopy)) {
					if ($null -ne $copy) {
						try { $copy.GetAwaiter().GetResult() | Out-Null } catch { $cleanupErrors.Add($_.ToString()) }
					}
				}
			} catch { $cleanupErrors.Add($_.ToString()) }
			finally { $child.Dispose() }
		}
		$stdout.Dispose(); $stderr.Dispose()
		Write-LifecycleJson (Join-Path $RunDirectory 'lifecycle-controller.json') ([ordered]@{
			schemaVersion = 1; run = $Run; phase = $phase; elapsedSeconds = $clock.Elapsed.TotalSeconds;
			failure = if ($null -eq $failure) { $null } else { $failure.ToString() };
			cleanupErrors = @($cleanupErrors.ToArray()); samples = @($samples.ToArray()) })
		if ($null -eq $failure -and $cleanupErrors.Count -gt 0) { throw "Lifecycle cleanup failed: $($cleanupErrors -join '; ')" }
	}
}
