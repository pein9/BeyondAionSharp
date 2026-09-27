# NI-10 attach contract: the runner reads an operator's world and can never change it. No Docker, servers or bots.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '../e2e/run-artifact-owner.ps1')
. (Join-Path $PSScriptRoot 'attach-controller.ps1')
$checks = 0
function Expect-Refused([scriptblock]$Action, [string]$Because) {
	$threw = $false
	try { & $Action | Out-Null } catch { $threw = $true }
	if (-not $threw) { throw "Accepted: $Because" }
	$script:checks++
}

# Only reads pass the gate; every lifecycle, build, write and follow form is refused before Docker runs.
$id = 'a' * 64
$slotOneSql = $script:AttachIdentitySql
Set-AttachIdentitySlot 2
if ($script:AttachAccountName -cne 'niishalgen2' -or $script:AttachCharacterName -cne 'Ishalgenbottwo' -or
	$script:AttachIdentitySql -cnotmatch "name = 'niishalgen2'" -or
	$script:AttachIdentitySql -cnotmatch "name = 'Ishalgenbottwo'") { throw 'Second attach identity was not isolated.' }
Assert-AttachDockerReadOnly @('exec', '-e', "AION_ATTACH_SQL=$script:AttachIdentitySql", $id, 'sh', '-c', $script:AttachIdentityShell)
Expect-Refused { Set-AttachIdentitySlot 10 } 'an unallocated identity slot'
Set-AttachIdentitySlot 1
if ($script:AttachIdentitySql -cne $slotOneSql) { throw 'Default attach identity changed after slot selection.' }
$checks += 2
foreach ($allowed in @(@('ps', '--all'), @('inspect', $id), @('logs', '--timestamps', $id),
	@('exec', '-e', "AION_ATTACH_SQL=$script:AttachIdentitySql", $id, 'sh', '-c', $script:AttachIdentityShell))) {
	Assert-AttachDockerReadOnly $allowed
	$checks++
}
foreach ($refused in @(@('compose', '-p', 'aion', 'up', '-d'), @('compose', '-p', 'aion', 'down'), @('compose', 'build'),
	@('start', $id), @('stop', $id), @('restart', $id), @('kill', $id), @('rm', '-f', $id), @('update', $id),
	@('cp', 'x', "${id}:/app"), @('build', '.'), @('volume', 'rm', 'aion_aion-mysql-data'), @('network', 'rm', 'aion_default'),
	@('logs', '--follow', $id), @('logs', '-f', $id), @('exec', $id, 'sh', '-c', 'mysql -e "DROP DATABASE aion_gs"'),
	@('exec', '-e', 'AION_ATTACH_SQL=DELETE FROM aion_gs.players', $id, 'sh', '-c', $script:AttachIdentityShell),
	@('exec', '-e', "AION_ATTACH_SQL=$script:AttachIdentitySql", $id, 'sh', '-c', 'mysql -e "$AION_ATTACH_SQL"; reboot'), @())) {
	Expect-Refused { Assert-AttachDockerReadOnly $refused } "docker $($refused -join ' ')"
}
if ($script:AttachIdentitySql -cmatch '(?i)\b(insert|update|delete|drop|alter|create|replace|truncate|grant)\b') { throw 'Identity SQL is not read-only.' }
$checks++

# Static guard: the entry script never calls Docker itself; the controller calls it only inside the gate.
foreach ($file in @('attach-live.ps1', 'attach-controller.ps1')) {
	$ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot $file), [ref]$null, [ref]$null)
	$calls = @($ast.FindAll({ param($node) $node -is [Management.Automation.Language.CommandAst] -and $node.GetCommandName() -ceq 'docker' }, $true))
	$expected = if ($file -ceq 'attach-controller.ps1') { 2 } else { 0 }
	if ($calls.Count -ne $expected) { throw "$file calls docker directly $($calls.Count) times (expected $expected)." }
	foreach ($call in $calls) {
		$owner = $call.Parent
		while ($null -ne $owner -and $owner -isnot [Management.Automation.Language.FunctionDefinitionAst]) { $owner = $owner.Parent }
		if ($null -eq $owner -or $owner.Name -cne 'Invoke-AttachDocker') { throw "$file calls docker outside Invoke-AttachDocker." }
	}
	$checks++
}

# Target discovery reads only the selected project, keeps no secrets, and needs a running world.
function New-AttachFixture([string]$Service, [string]$Letter, [hashtable]$Ports, [bool]$Running = $true) {
	$portMap = [ordered]@{}
	foreach ($key in $Ports.Keys) { $portMap[$key] = @([pscustomobject]@{ HostIp = '0.0.0.0'; HostPort = [string]$Ports[$key] }) }
	return [pscustomobject]@{
		Id = $Letter * 64; Name = "/aion-$Service"; Image = "sha256:$Letter"; RestartCount = 0
		State = [pscustomobject]@{ Running = $Running; StartedAt = '2026-09-26T13:17:25Z' }
		Config = [pscustomobject]@{ Image = "aion-$Service"; Labels = [pscustomobject]@{ 'com.docker.compose.project' = 'aion'; 'com.docker.compose.service' = $Service }
			Env = @('DB_PASSWORD=secret-db', 'GAMESERVER_ADMIN_API_TOKEN=secret-token', 'RESPAWN_TIME_MULTIPLIER=1.0', 'MYSQL_ROOT_PASSWORD=secret-root') }
		NetworkSettings = [pscustomobject]@{ Ports = [pscustomobject]$portMap }
		Mounts = @()
	}
}
$fixtures = @((New-AttachFixture 'loginserver' 'b' @{ '2106/tcp' = 2106 }), (New-AttachFixture 'gameserver' 'c' @{ '7777/tcp' = 7777 }),
	(New-AttachFixture 'chatserver' 'd' @{ '10241/tcp' = 10241 }), (New-AttachFixture 'mysql' 'e' @{ '3306/tcp' = 3306 }))
$dockerCalls = [Collections.Generic.List[string]]::new()
$identityJson = '{"account":{"id":7,"accessLevel":0,"activated":1},"characters":[{"id":100,"name":"Ishalgenbot","accountName":"niishalgen","online":0}]}'
function docker {
	$dockerCalls.Add($args -join ' ')
	$global:LASTEXITCODE = 0
	switch -CaseSensitive ($args[0]) {
		'ps' {
			if ($args -notcontains 'label=com.docker.compose.project=aion') { throw 'Target selection must filter by the compose project.' }
			return @($fixtures | ForEach-Object Id)
		}
		'inspect' { $requested = $args; return @($fixtures | Where-Object { $requested -contains $_.Id }) | ConvertTo-Json -Depth 8 -AsArray }
		'exec' { return $identityJson }
		default { throw "Mock refused docker $($args -join ' ')" }
	}
}
$target = Get-AttachTarget 'aion'
if ((Get-AttachPort $target 'loginserver' '2106/tcp') -ne 2106 -or (Get-AttachPort $target 'gameserver' '7777/tcp') -ne 7777 -or
	(Get-AttachPort $target 'chatserver' '10241/tcp') -ne 10241) { throw 'Published ports were not read.' }
$serialized = $target | ConvertTo-Json -Depth 12
if ($serialized -match 'secret-') { throw 'Target evidence kept a secret environment value.' }
if ($serialized -notmatch 'RESPAWN_TIME_MULTIPLIER') { throw 'Target evidence lost the world profile.' }
$checks += 3
$identity = Read-AttachIdentity $target
Assert-AttachIdentityUsable $identity
if (-not $identity.checked -or $identity.characters[0].name -cne 'Ishalgenbot') { throw 'Identity oracle was not read.' }
$checks++
$saved = $fixtures
$fixtures = @($saved | Where-Object { $_.Config.Labels.'com.docker.compose.service' -cne 'gameserver' })
Expect-Refused { Get-AttachTarget 'aion' } 'a world without a game server'
$fixtures = @($saved | ForEach-Object { if ($_.Config.Labels.'com.docker.compose.service' -ceq 'loginserver') { New-AttachFixture 'loginserver' 'b' @{ '2106/tcp' = 2106 } $false } else { $_ } })
Expect-Refused { Get-AttachTarget 'aion' } 'a stopped login server'
$fixtures = @($saved; New-AttachFixture 'gameserver' 'f' @{})
Expect-Refused { Get-AttachTarget 'aion' } 'two game servers in one project'
$fixtures = @()
Expect-Refused { Get-AttachTarget 'aion' } 'a project with no containers'
Expect-Refused { Get-AttachTarget 'Aion;rm' } 'an invalid project name'
$fixtures = $saved
if (@($dockerCalls | Where-Object { $_ -notmatch '^(ps|inspect|exec -e AION_ATTACH_SQL=SELECT) ' }).Count -ne 0) { throw 'Discovery issued a non-read Docker command.' }
$checks++

# The Priest must be the ordinary, offline, sole character of its own account.
foreach ($unsafe in @(
	'{"account":{"id":7,"accessLevel":3,"activated":1},"characters":[]}',
	'{"account":null,"characters":[{"id":5,"name":"Ishalgenbot","accountName":"rrfarmer","online":0}]}',
	'{"account":{"id":7,"accessLevel":0,"activated":1},"characters":[{"id":100,"name":"Ishalgenbot","accountName":"niishalgen","online":1}]}',
	'{"account":{"id":7,"accessLevel":0,"activated":1},"characters":[{"id":100,"name":"Ishalgenbot","accountName":"niishalgen","online":0},{"id":101,"name":"Other","accountName":"niishalgen","online":0}]}')) {
	$identityJson = $unsafe
	Expect-Refused { Assert-AttachIdentityUsable (Read-AttachIdentity $target) } "identity $unsafe"
}
$identityJson = '{"account":null,"characters":null}'
Assert-AttachIdentityUsable (Read-AttachIdentity $target)
$checks++
$withoutDatabase = [pscustomobject]@{ project = 'aion'; services = [pscustomobject]@{} }
if ((Read-AttachIdentity $withoutDatabase).checked) { throw 'A world without MySQL must record an unchecked identity.' }
$checks++

# Operator restarts are recorded as world changes, never hidden.
$after = Get-AttachTarget 'aion'
if (@(Compare-AttachTarget $target $after).Count -ne 0) { throw 'An untouched world reported changes.' }
$after.services.gameserver.startedAt = '2026-09-26T15:00:00Z'
$after.services.PSObject.Properties.Remove('chatserver')
$changes = @(Compare-AttachTarget $target $after)
if ($changes.Count -ne 2 -or $changes -notcontains 'chatserver: container removed') { throw "Unexpected change report: $changes" }
$checks++

Write-Host "NI-10 attach contract passed ($checks assertions); no Docker, servers or bots started."
