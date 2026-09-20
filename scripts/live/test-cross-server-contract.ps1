# Read-only Compose resolution plus mocked target selection; starts no containers or bots.
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$probe=Join-Path $PSScriptRoot 'test-cross-server-topology.ps1'
$errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile($probe,[ref]$null,[ref]$errors)
if ($errors.Count -ne 0) { throw "Topology probe parse failed: $errors" }
$functions=@($ast.FindAll({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq 'Read-TopologyContainer'},$true))
if ($functions.Count -ne 1) { throw 'Missing/duplicate ownership function.' }
. ([scriptblock]::Create($functions[0].Extent.Text))
$project='aion-bots-contract'; $composeArgs=@('compose','-f','fixture.yml','-p',$project,'--profile','cross-server')
$id='a'*64; $ids=@($id); $checks=0
function Invoke-TopologyDocker([string[]]$Arguments) {
	if ($Arguments[0] -ceq 'compose') {
		if (($Arguments -join '|') -cne 'compose|-f|fixture.yml|-p|aion-bots-contract|--profile|cross-server|ps|--all|--quiet|gameserver2') { throw 'Unexpected selection arguments.' }
		return $ids
	}
	if ($Arguments.Count -ne 2 -or $Arguments[0] -cne 'inspect' -or $Arguments[1] -cne $id) { throw 'Unexpected inspection target.' }
	return ($fixture | ConvertTo-Json -Depth 8)
}
function New-TopologyFixture {
	return [pscustomobject]@{Id=$id;Image='sha256:image';RestartCount=0;State=[pscustomobject]@{Running=$true;StartedAt='2026-09-20T00:00:00Z'};
		Config=[pscustomobject]@{Labels=[pscustomobject]@{'com.docker.compose.project'=$project;'com.docker.compose.service'='gameserver2'}};
		NetworkSettings=[pscustomobject]@{Networks=[pscustomobject]@{"${project}_default"=[pscustomobject]@{}}}}
}
function Assert-TopologyContract([bool]$Condition) {
	if (-not $Condition) { throw 'Topology contract assertion failed.' }
	$script:checks++
}
$fixture=New-TopologyFixture
$selected=Read-TopologyContainer 'gameserver2'
Assert-TopologyContract ($selected.id -ceq $id -and $selected.service -ceq 'gameserver2')
foreach ($defect in @('empty','duplicate','short','id','project','service','network','shared-network','stopped','restart')) {
	$fixture=New-TopologyFixture; $ids=@($id)
	switch ($defect) {
		'empty' {$ids=@()}
		'duplicate' {$ids=@($id,$id)}
		'short' {$ids=@('aion-mysql')}
		'id' {$fixture.Id='b'*64}
		'project' {$fixture.Config.Labels.'com.docker.compose.project'='aion'}
		'service' {$fixture.Config.Labels.'com.docker.compose.service'='gameserver'}
		'network' {$fixture.NetworkSettings.Networks=[pscustomobject]@{aion_default=[pscustomobject]@{}}}
		'shared-network' {$fixture.NetworkSettings.Networks | Add-Member -NotePropertyName aion_default -NotePropertyValue ([pscustomobject]@{})}
		'stopped' {$fixture.State.Running=$false}
		'restart' {$fixture.RestartCount=1}
	}
	$threw=$false
	try { Read-TopologyContainer 'gameserver2' | Out-Null } catch { $threw=$true }
	Assert-TopologyContract $threw
}
$names=@('AION_E2E_RUN_DIR','AION_BOT_SECOND_GS','COMPOSE_PROFILES')
$saved=@{}
foreach ($name in $names) { $saved[$name]=[Environment]::GetEnvironmentVariable($name) }
try {
	$env:AION_E2E_RUN_DIR=Join-Path $repoRoot 'run/topology-contract-not-created'
	$env:AION_BOT_SECOND_GS='false'; $env:COMPOSE_PROFILES=''
	$argsBase=@('compose','-f',(Join-Path $repoRoot 'docker/docker-compose.bots.yml'),'-p','aion-bots-contract')
	$default=@(& docker @argsBase config --services)
	if ($LASTEXITCODE -ne 0) { throw 'Compose default resolution failed.' }
	Assert-TopologyContract ($default.Count -eq 4 -and 'gameserver2' -notin $default -and 'chatserver2' -notin $default)
	$env:AION_BOT_SECOND_GS='true'
	$config=(& docker @argsBase --profile cross-server config --format json | ConvertFrom-Json)
	if ($LASTEXITCODE -ne 0) { throw 'Compose topology resolution failed.' }
	Assert-TopologyContract (@($config.services.PSObject.Properties).Count -eq 6)
	Assert-TopologyContract ($config.services.mysql.environment.AION_BOT_SECOND_GS -ceq 'true')
	Assert-TopologyContract ($config.services.gameserver2.environment.CHAT_ADDRESS -ceq 'chatserver2:9021')
	Assert-TopologyContract ($config.services.gameserver2.image -ceq $config.services.gameserver.image)
	Assert-TopologyContract ($config.services.chatserver2.image -ceq $config.services.chatserver.image)
	foreach ($pair in @(@('gameserver','gameserver2','gs2'),@('chatserver','chatserver2','cs2'))) {
		$first=$config.services.($pair[0]); $second=$config.services.($pair[1])
		foreach ($port in $second.ports) { Assert-TopologyContract ($port.published -notin @($first.ports | ForEach-Object published)) }
		$logs=@($second.volumes | Where-Object target -Like '*/log')
		Assert-TopologyContract ($logs.Count -eq 1 -and $logs[0].source -like "*/logs/$($pair[2])")
		$overlay=@($second.volumes | Where-Object target -CEQ '/app/instance-overlay')
		Assert-TopologyContract ($overlay.Count -eq 1 -and $overlay[0].read_only)
	}
} finally { foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name,$saved[$name]) } }
. (Join-Path $PSScriptRoot 'transfer-controller.ps1')
function New-TransferFixture {
	return [pscustomobject]@{
		source=[pscustomobject]@{players=@([pscustomobject]@{id=101;accountId=11;online=0},[pscustomobject]@{id=102;accountId=12;online=0});inventory=@([pscustomobject]@{id=103})};
		target=[pscustomobject]@{players=@();inventory=@()};
		accounts=@([pscustomobject]@{id=11;accessLevel=0;activated=1},[pscustomobject]@{id=12;accessLevel=0;activated=1})
	}
}
$fixture=New-TransferFixture
Assert-TransferSetup $fixture.source $fixture.target $fixture.accounts
$checks++
foreach ($defect in @('source-empty','target-player','target-item','inventory-empty','online','staff','inactive','account-missing','same-account','negative','fraction','overflow','string')) {
	$fixture=New-TransferFixture
	switch ($defect) {
		'source-empty' {$fixture.source.players=@()}
		'target-player' {$fixture.target.players=@([pscustomobject]@{id=999})}
		'target-item' {$fixture.target.inventory=@([pscustomobject]@{id=999})}
		'inventory-empty' {$fixture.source.inventory=@()}
		'online' {$fixture.source.players[0].online=1}
		'staff' {$fixture.accounts[0].accessLevel=9}
		'inactive' {$fixture.accounts[0].activated=0}
		'account-missing' {$fixture.accounts=@()}
		'same-account' {$fixture.source.players[1].accountId=11}
		'negative' {$fixture.source.players[0].id=-1}
		'fraction' {$fixture.source.players[0].id=1.5}
		'overflow' {$fixture.source.players[0].id=[long]::MaxValue}
		'string' {$fixture.source.players[0].accountId='11'}
	}
	$threw=$false
	try { Assert-TransferSetup $fixture.source $fixture.target $fixture.accounts } catch { $threw=$true }
	Assert-TopologyContract $threw
}
$queries=[Collections.Generic.List[string]]::new()
$sql={param($query) $queries.Add($query); if ($query -match '\.players ORDER BY id$') { '{"id":101}' } elseif ($query -match '\.inventory ORDER BY item_unique_id$') { '{"id":102}' } else { throw 'Unexpected SQL.' } }
foreach ($database in @('aion_gs','aion_gs2')) {
	$rows=Read-TransferRows $sql $database
	Assert-TopologyContract ($rows.players.Count -eq 1 -and $rows.inventory.Count -eq 1)
}
$threw=$false
try { Read-TransferRows $sql 'other_database' | Out-Null } catch { $threw=$true }
Assert-TopologyContract ($threw -and $queries.Count -eq 4)
Write-Host "Cross-server contract passed ($checks assertions); no containers, databases or bots started."
