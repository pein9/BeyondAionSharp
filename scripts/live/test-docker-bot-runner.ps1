[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'docker-bot-runner.ps1')
$project = 'aion-bots-contract'
$revision = 'a' * 40
$compose = @('compose', '-f', 'fixture.yml', '-p', $project, '--profile', 'bot-runner')
$script:mode = 'passed'
function docker {
	param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)
	$script:LASTEXITCODE = 0
	if ($Arguments[0] -eq 'compose') {
		if (($Arguments[0..($compose.Length - 1)] -join '|') -cne ($compose -join '|') -or
			($Arguments[$compose.Length..($compose.Length + 2)] -join '|') -cne 'ps|--all|--quiet') { throw 'Unexpected Docker list command.' }
		if ($script:mode -eq 'list-failed') { $script:LASTEXITCODE = 1; return }
		if ($script:mode -eq 'missing') { return }
		if ($script:mode -eq 'ambiguous') { return @('one', 'two') }
		return 'id-' + $Arguments[-1]
	}
	if ($Arguments[0] -ne 'inspect' -or $Arguments.Count -ne 2) { throw 'Unexpected Docker command.' }
	if ($script:mode -eq 'inspect-failed') { $script:LASTEXITCODE = 1; return }
	$service = $Arguments[1].Substring(3)
	$network = @{ "${project}_default" = @{ IPAddress = '172.23.0.4' } }
	if ($script:mode -eq 'wrong-network') { $network = @{ unrelated = @{ IPAddress = '172.23.0.4' } } }
	if ($script:mode -eq 'extra-network') { $network['unrelated'] = @{ IPAddress = '172.24.0.4' } }
	if ($script:mode -eq 'no-address') { $network["${project}_default"].IPAddress = '0.0.0.0' }
	@{
		Id = $Arguments[1]; Image = 'sha256:fixture'
		State = @{ Running = $script:mode -ne 'stopped' }
		Config = @{ Labels = @{
			'com.docker.compose.project' = $(if ($script:mode -eq 'wrong-project') { 'other' } else { $project })
			'com.docker.compose.service' = $(if ($script:mode -eq 'wrong-service') { 'other' } else { $service })
			'org.opencontainers.image.revision' = $(if ($script:mode -eq 'wrong-revision') { 'b' * 40 } else { $revision })
		} }
		NetworkSettings = @{ Networks = $network }
	} | ConvertTo-Json -Depth 8 -Compress
}
$endpoints = Get-LiveDockerEndpoints -ComposeArguments $compose -ProjectName $project -ExpectedRevision $revision
if ($endpoints.Count -ne 4 -or $endpoints.botrunner.imageId -cne 'sha256:fixture') { throw 'Missing Docker provenance.' }
foreach ($script:mode in @('list-failed', 'missing', 'ambiguous', 'inspect-failed', 'stopped', 'wrong-project', 'wrong-service', 'wrong-network', 'extra-network', 'no-address', 'wrong-revision')) {
	$failed = $false
	try { Get-LiveDockerEndpoints -ComposeArguments $compose -ProjectName $project -ExpectedRevision $revision | Out-Null } catch { $failed = $true }
	if (-not $failed) { throw "Unsafe Docker endpoint case accepted: $script:mode" }
}
$endpoints.loginserver.address = '172.23.0.2'; $endpoints.chatserver.address = '172.23.0.3'
$hostArguments = @('run', '--project', 'tools/Aion.LiveBots', '--no-build', '--',
	'--run', 'contract', '--output', 'C:\aion run\artifacts', '--host', '127.0.0.1',
	'--bots', '500', '--scenario', 'SOAK', '--seed', '73', '--git-sha', $revision,
	'--profile', 'docker-bots-soak', '--time-zone', 'Eastern Standard Time',
	'--connect-timeout-seconds', '10', '--step-timeout-seconds', '1800', '--soak-seconds', '7200',
	'--soak-activities', 'Quest,Gather,Craft,Vendor,Trade,Group,Duel,Pvp,Relog,CrashDisconnect')
$converted = @(Get-LiveDockerBotArguments -HostArguments $hostArguments -Endpoints $endpoints)
if (($converted[0..4] -join '|') -cne 'exec|-T|botrunner|dotnet|/app/Aion.LiveBots.dll') { throw 'Container runner command changed.' }
foreach ($name in @('--run', '--bots', '--scenario', '--seed', '--git-sha', '--profile', '--connect-timeout-seconds', '--step-timeout-seconds', '--soak-seconds', '--soak-activities')) {
	if ($converted[[Array]::IndexOf($converted, $name) + 1] -cne $hostArguments[[Array]::IndexOf($hostArguments, $name) + 1]) { throw "Lost workload argument: $name" }
}
foreach ($expected in @{
	'--host'='172.23.0.4'; '--login-host'='172.23.0.2'; '--chat-host'='172.23.0.3'; '--output'='/artifacts'; '--time-zone'='UTC'
	'--login-port'='2106'; '--game-port'='7777'; '--chat-port'='10241'; '--admin-port'='7780'
}.GetEnumerator()) {
	if ($converted[[Array]::IndexOf($converted, $expected.Key) + 1] -cne $expected.Value) { throw "Wrong internal option: $($expected.Key)" }
}
# Execute the real dispatch branch: both backends must retain the child process exit status.
$tokens=$null; $errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'run-live.ps1'),[ref]$tokens,[ref]$errors)
if ($errors.Count -ne 0) { throw 'LIVE runner failed to parse.' }
$startup = @($ast.FindAll({ param($node)
	$node -is [Management.Automation.Language.IfStatementAst] -and $node.Extent.Text.Contains("$" + "upArguments.Add('--build')")
}, $true))
if ($startup.Count -ne 1) { throw 'Missing image startup build policy.' }
$startupBlock = [scriptblock]::Create($startup[0].Extent.Text)
foreach ($BotExecution in @('Host', 'Docker')) {
	foreach ($SkipImageBuild in @($false, $true)) {
		$upArguments = [Collections.Generic.List[string]]::new()
		. $startupBlock
		$expectedBuild = $BotExecution -eq 'Host' -and -not $SkipImageBuild
		if ($upArguments.Contains('--build') -ne $expectedBuild) { throw 'Startup would rebuild the revision-labelled client or skip the host server build.' }
	}
}
$dispatch=@($ast.FindAll({ param($node)
	$node -is [Management.Automation.Language.IfStatementAst] -and $node.Extent.Text.Contains('& docker @composeArgs @containerArguments')
},$true))
if ($dispatch.Count -ne 1) { throw 'Missing backend dispatch.' }
$block=[scriptblock]::Create($dispatch[0].Extent.Text)
$botArguments=$hostArguments; $dockerEndpoints=$endpoints; $composeArgs=$compose
function dotnet { param([Parameter(ValueFromRemainingArguments=$true)][object[]]$Arguments) $script:called='Host'; $script:LASTEXITCODE=$script:expectedExit }
function docker { param([Parameter(ValueFromRemainingArguments=$true)][object[]]$Arguments) $script:called='Docker'; $script:LASTEXITCODE=$script:expectedExit }
foreach ($BotExecution in @('Host','Docker')) {
	foreach ($script:expectedExit in @(0,1,137)) {
		$script:called=''; . $block
		if ($script:called -cne $BotExecution -or $LASTEXITCODE -ne $script:expectedExit) { throw 'Backend or exit status was lost.' }
	}
}
Write-Host 'Docker bot runner contract passed: isolated endpoint identity, revision, workload forwarding, backend selection and failure propagation.'
