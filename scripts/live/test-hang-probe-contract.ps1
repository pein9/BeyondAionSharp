# Read the real LIVE probe's ownership functions without executing its Docker workflow.
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$probe=Join-Path $PSScriptRoot 'test-hang-diagnostics.ps1'
$errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile($probe,[ref]$null,[ref]$errors)
if ($errors.Count -ne 0) { throw "Hang probe parse failed: $errors" }
foreach ($name in @('Read-ProbeContainer','Assert-ProbeIdentity')) {
	$functions=@($ast.FindAll({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq $name},$true))
	if ($functions.Count -ne 1) { throw "Missing/duplicate probe function: $name" }
	. ([scriptblock]::Create($functions[0].Extent.Text))
}
$project='aion-bots-contract'; $composeArgs=@('compose','-f','fixture.yml','-p',$project)
$id='a'*64; $ids=@($id); $checks=0
function docker { throw 'This contract must never execute Docker.' }
function Invoke-ProbeDocker([string[]]$Arguments) {
	if ($Arguments[0] -ceq 'compose') {
		if (($Arguments -join '|') -cne 'compose|-f|fixture.yml|-p|aion-bots-contract|ps|--all|--quiet|gameserver') { throw 'Unexpected selection arguments.' }
		return $ids
	}
	if ($Arguments.Count -ne 2 -or $Arguments[0] -cne 'inspect' -or $Arguments[1] -cne $id) { throw 'Unexpected inspection target.' }
	return ($fixture | ConvertTo-Json -Depth 8)
}
function New-ProbeFixture {
	return [pscustomobject]@{Id=$id;Image='sha256:image';State=[pscustomobject]@{Running=$true;StartedAt=[DateTime]::SpecifyKind([DateTime]'2026-09-20',[DateTimeKind]::Utc)};
		Config=[pscustomobject]@{Labels=[pscustomobject]@{'com.docker.compose.project'=$project;'com.docker.compose.service'='gameserver'}};
		NetworkSettings=[pscustomobject]@{Networks=[pscustomobject]@{"${project}_default"=[pscustomobject]@{}}}}
}
function Expect-ProbeFailure([scriptblock]$Action) {
	$threw=$false
	try { & $Action | Out-Null } catch { $threw=$true }
	if (-not $threw) { throw 'An unsafe probe condition was accepted.' }
	$script:checks++
}
$fixture=New-ProbeFixture
$selected=Read-ProbeContainer 'gameserver'
Assert-ProbeIdentity $fixture $selected
$checks++
foreach ($badIds in @(@(),@($id,$id),@('short-id'),@('aion-mysql'))) {
	$ids=$badIds
	Expect-ProbeFailure { Read-ProbeContainer 'gameserver' }
}
$ids=@($id)
foreach ($defect in @('id','project','service','network','shared-network')) {
	$fixture=New-ProbeFixture
	switch ($defect) {
		'id' {$fixture.Id='b'*64}
		'project' {$fixture.Config.Labels.'com.docker.compose.project'='aion'}
		'service' {$fixture.Config.Labels.'com.docker.compose.service'='mysql'}
		'network' {$fixture.NetworkSettings.Networks=[pscustomobject]@{aion_default=[pscustomobject]@{}}}
		'shared-network' {$fixture.NetworkSettings.Networks | Add-Member -NotePropertyName aion_default -NotePropertyValue ([pscustomobject]@{})}
	}
	Expect-ProbeFailure { Read-ProbeContainer 'gameserver' }
}
foreach ($defect in @('id','image','start','stopped')) {
	$expected=New-ProbeFixture; $actual=New-ProbeFixture
	switch ($defect) {
		'id' {$actual.Id='b'*64}
		'image' {$actual.Image='sha256:changed'}
		'start' {$actual.State.StartedAt='2026-09-20T00:01:00Z'}
		'stopped' {$actual.State.Running=$false}
	}
	Expect-ProbeFailure { Assert-ProbeIdentity $expected $actual }
}
Write-Host "Hang probe ownership contract passed ($checks assertions); no Docker, servers or bots started."
