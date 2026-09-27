# NI-10: read-only view of an operator's already-running world. Loading this file has no side effects.
# Every Docker call goes through Invoke-AttachDocker, which admits only ps/inspect/logs and one fixed SELECT.
# Nothing here can build, start, stop, restart, recreate, reconfigure or write to the target world.
Set-StrictMode -Version Latest

# Only numbered identities generated here may enter the fixed read-only query.
function Set-AttachIdentitySlot([int]$Slot) {
	if ($Slot -lt 1 -or $Slot -gt 9) { throw 'Attach identity slot must be 1 through 9.' }
	$suffix = if ($Slot -eq 1) { '' } else { "$Slot" }
	$script:AttachAccountName = "niishalgen$suffix"
	$nameSuffix = @('', '', 'two', 'three', 'four', 'five', 'six', 'seven', 'eight', 'nine')[$Slot]
	$script:AttachCharacterName = "Ishalgenbot$nameSuffix"
	$script:AttachIdentitySql = "SELECT JSON_OBJECT(" +
		"'account', (SELECT JSON_OBJECT('id', id, 'accessLevel', access_level, 'activated', activated) FROM aion_ls.account_data WHERE name = '$script:AttachAccountName'), " +
		"'characters', (SELECT JSON_ARRAYAGG(JSON_OBJECT('id', id, 'name', name, 'accountName', account_name, 'race', race, " +
		"'playerClass', player_class, 'exp', exp, 'online', online, 'worldId', world_id, 'x', x, 'y', y, 'z', z, " +
		"'lastOnline', last_online, 'deletionDate', deletion_date)) FROM aion_gs.players WHERE account_name = '$script:AttachAccountName' OR name = '$script:AttachCharacterName'))"
}
Set-AttachIdentitySlot 1
$script:AttachIdentityShell = 'MYSQL_PWD="$MYSQL_ROOT_PASSWORD" exec mysql -uroot -N -B --default-character-set=utf8mb4 -e "$AION_ATTACH_SQL"'
$script:AttachEnvironmentAllowlist = @('RESPAWN_TIME_MULTIPLIER', 'SERVER_HOST', 'GAME_CLIENT_PORT', 'CHAT_CLIENT_PORT',
	'AION_RECORD', 'AION_RECORD_ACCOUNTS', 'AION_PACKET_TAP', 'GAMESERVER_ADMIN_API_ENABLED')

function Assert-AttachDockerReadOnly([string[]]$Arguments) {
	if ($null -eq $Arguments -or $Arguments.Count -eq 0) { throw 'Refusing an empty Docker command.' }
	switch -CaseSensitive ($Arguments[0]) {
		'ps' { return }
		'inspect' { return }
		'logs' { if ($Arguments -notcontains '--follow' -and $Arguments -notcontains '-f') { return } }
		'exec' {
			if ($Arguments.Count -eq 7 -and $Arguments[1] -ceq '-e' -and $Arguments[2] -ceq "AION_ATTACH_SQL=$script:AttachIdentitySql" -and
				$Arguments[3] -cmatch '^[0-9a-f]{64}$' -and $Arguments[4] -ceq 'sh' -and $Arguments[5] -ceq '-c' -and
				$Arguments[6] -ceq $script:AttachIdentityShell) { return }
		}
	}
	throw "Attach mode is read-only; refusing 'docker $($Arguments -join ' ')'."
}

function Invoke-AttachDocker([string[]]$Arguments, [switch]$MergeErrors) {
	Assert-AttachDockerReadOnly $Arguments
	# Container logs keep the server's stderr lines; JSON reads must not mix in Docker warnings.
	$output = if ($MergeErrors) { & docker @Arguments 2>&1 } else { & docker @Arguments }
	if ($LASTEXITCODE -ne 0) { throw "docker $($Arguments[0]) failed with exit code $LASTEXITCODE." }
	return $output
}

function Get-AttachTarget([string]$Project) {
	if ($Project -cnotmatch '^[a-z0-9][a-z0-9_-]*$') { throw "Invalid target compose project: $Project" }
	$ids = @(Invoke-AttachDocker @('ps', '--all', '--no-trunc', '--filter', "label=com.docker.compose.project=$Project", '--format', '{{.ID}}') |
		Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
	if ($ids.Count -eq 0) { throw "No containers belong to compose project '$Project'. Start your server first; attach mode never starts one." }
	foreach ($id in $ids) { if ($id -cnotmatch '^[0-9a-f]{64}$') { throw "Unexpected container id: $id" } }
	$containers = @((Invoke-AttachDocker (@('inspect') + $ids)) | Out-String | ConvertFrom-Json)
	$services = [ordered]@{}
	foreach ($container in $containers) {
		$labels = $container.Config.Labels
		if ($labels.'com.docker.compose.project' -cne $Project) { throw "Container $($container.Id) left project '$Project' during inspection." }
		$service = [string]$labels.'com.docker.compose.service'
		if ($services.Contains($service)) { throw "Project '$Project' has more than one '$service' container." }
		$environment = [ordered]@{}
		foreach ($entry in @($container.Config.Env)) {
			$name, $value = ([string]$entry).Split('=', 2)
			if ($script:AttachEnvironmentAllowlist -ccontains $name) { $environment[$name] = $value }
		}
		$ports = [ordered]@{}
		if ($null -ne $container.NetworkSettings.Ports) {
			foreach ($port in $container.NetworkSettings.Ports.PSObject.Properties) {
				$binding = @($port.Value | Where-Object { $null -ne $_ -and $_.HostIp -in @('0.0.0.0', '127.0.0.1', '') }) | Select-Object -First 1
				if ($null -ne $binding) { $ports[$port.Name] = [int]$binding.HostPort }
			}
		}
		$services[$service] = [pscustomobject][ordered]@{
			service = $service; id = [string]$container.Id; name = ([string]$container.Name).TrimStart('/')
			image = [string]$container.Image; imageTag = [string]$container.Config.Image
			running = [bool]$container.State.Running; startedAt = ([string]$container.State.StartedAt)
			restartCount = [int]$container.RestartCount; ports = [pscustomobject]$ports
			environment = [pscustomobject]$environment
			configOverlays = @($container.Mounts | Where-Object { $_.Destination -like '/app/*overlay*' } | ForEach-Object Destination)
		}
	}
	foreach ($required in @('loginserver', 'gameserver')) {
		if (-not $services.Contains($required)) { throw "Project '$Project' has no $required service." }
		if (-not $services[$required].running) { throw "Project '$Project' $required is not running. Attach mode never starts it." }
	}
	return [pscustomobject][ordered]@{ schemaVersion = 1; project = $Project; observedUtc = [DateTimeOffset]::UtcNow.ToString('O'); services = [pscustomobject]$services }
}

function Get-AttachPort([object]$Target, [string]$Service, [string]$ContainerPort) {
	$entry = $Target.services.PSObject.Properties[$Service]
	if ($null -eq $entry -or $null -eq $entry.Value.ports.PSObject.Properties[$ContainerPort]) { return 0 }
	return [int]$entry.Value.ports.$ContainerPort
}

function Read-AttachIdentity([object]$Target) {
	$mysql = $Target.services.PSObject.Properties['mysql']
	if ($null -eq $mysql -or -not $mysql.Value.running) {
		return [pscustomobject]@{ checked = $false; reason = "Project '$($Target.project)' has no running mysql service to read." }
	}
	$raw = (Invoke-AttachDocker @('exec', '-e', "AION_ATTACH_SQL=$script:AttachIdentitySql", $mysql.Value.id, 'sh', '-c', $script:AttachIdentityShell) |
		Out-String).Trim()
	$state = $raw | ConvertFrom-Json
	return [pscustomobject][ordered]@{ checked = $true; observedUtc = [DateTimeOffset]::UtcNow.ToString('O'); account = $state.account
		characters = @(if ($null -ne $state.characters) { $state.characters }) }
}

function Assert-AttachIdentityUsable([object]$Identity) {
	if (-not $Identity.checked) { return }
	if ($null -ne $Identity.account -and [int]$Identity.account.accessLevel -ne 0) {
		throw "Account $script:AttachAccountName has access level $($Identity.account.accessLevel); the natural journey needs an ordinary account."
	}
	foreach ($character in @($Identity.characters)) {
		if ($character.name -ceq $script:AttachCharacterName -and $character.accountName -cne $script:AttachAccountName) {
			throw "The name $script:AttachCharacterName belongs to account '$($character.accountName)'; refusing to use another player's character."
		}
		if ($character.accountName -ceq $script:AttachAccountName -and [int]$character.online -ne 0) {
			throw "$($character.name) is already online; stop the other session (attach-live.ps1 -Stop) first."
		}
	}
	if (@($Identity.characters | Where-Object accountName -ceq $script:AttachAccountName).Count -gt 1) {
		throw "Account $script:AttachAccountName holds more than one character; the retained journey needs exactly its one Priest."
	}
}

# The operator may restart their own world during a run. That is recorded, never treated as the runner's doing.
function Compare-AttachTarget([object]$Before, [object]$After) {
	$changes = [Collections.Generic.List[string]]::new()
	foreach ($entry in $Before.services.PSObject.Properties) {
		$now = $After.services.PSObject.Properties[$entry.Name]
		if ($null -eq $now) { $changes.Add("$($entry.Name): container removed"); continue }
		foreach ($field in @('id', 'image', 'startedAt', 'running', 'restartCount')) {
			if ([string]$entry.Value.$field -cne [string]$now.Value.$field) {
				$changes.Add("$($entry.Name): $field $($entry.Value.$field) -> $($now.Value.$field)")
			}
		}
	}
	return @($changes)
}

function Save-AttachLogs([object]$Target, [DateTimeOffset]$Since, [DateTimeOffset]$Until, [string]$Directory) {
	New-Item -ItemType Directory -Path $Directory -Force | Out-Null
	$summary = [ordered]@{}
	foreach ($entry in $Target.services.PSObject.Properties) {
		$path = Join-Path $Directory "$($entry.Name).log"
		$lines = @(Invoke-AttachDocker @('logs', '--timestamps', '--since', $Since.ToString('O'), '--until', $Until.ToString('O'), $entry.Value.id) -MergeErrors |
			ForEach-Object { [string]$_ })
		[IO.File]::WriteAllLines($path, [string[]]$lines, [Text.UTF8Encoding]::new($false))
		# .NET console logger level prefixes ("fail:", "crit:", "warn:") after the Docker timestamp.
		$summary[$entry.Name] = [pscustomobject][ordered]@{
			lines = $lines.Count
			errors = @($lines | Where-Object { $_ -cmatch '^\S+\s+(fail|crit):' }).Count
			warnings = @($lines | Where-Object { $_ -cmatch '^\S+\s+warn:' }).Count
		}
	}
	return [pscustomobject]$summary
}

function Get-ActiveAttachRuns([string]$RunRoot, [string]$Project) {
	if (-not (Test-Path -LiteralPath $RunRoot -PathType Container)) { return @() }
	return @(Get-ChildItem -LiteralPath $RunRoot -Directory | Where-Object {
		$targetFile = Join-Path $_.FullName 'attach-target.json'
		(Test-Path -LiteralPath $targetFile) -and -not (Test-AionRunArtifactOwnerExited -Directory $_.FullName) -and
			(Get-Content -Raw -LiteralPath $targetFile | ConvertFrom-Json).target -ceq $Project
	})
}
