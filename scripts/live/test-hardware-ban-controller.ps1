[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'lifecycle-controller.ps1')
. (Join-Path $PSScriptRoot 'chat-fault-controller.ps1')
. (Join-Path $PSScriptRoot 'hardware-ban-controller.ps1')
$testRoot=Join-Path ([IO.Path]::GetTempPath()) ('aion-hardware-test-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
$project='aion-bots-contract'; $compose=@('compose','-f','fixture.yml','-p',$project)
$fixture=New-HardwareBanFixture 2027
$script:checks=0; $script:mode='passed'; $script:phase='running'; $script:polls=0
$script:path=''; $script:killAt=[DateTimeOffset]::UtcNow; $oldStart=[DateTimeOffset]::UtcNow.AddMinutes(-10).ToString('O')
$script:hardwareTestIds=@{mysql=('a'*64);loginserver=('b'*64);gameserver=('c'*64);chatserver=('d'*64)}
$script:commands=[Collections.Generic.List[string]]::new()
function Assert-HardwareTest([bool]$Value) { $script:checks++; if (-not $Value) { throw 'Hardware contract assertion failed.' } }
function Expect-HardwareFailure([scriptblock]$Action) { $failed=$false; try { & $Action | Out-Null } catch { $failed=$true }; Assert-HardwareTest $failed }
function Write-TestSnapshots([int]$Generation,[DateTimeOffset]$At) {
	$records=@()
	foreach ($kind in @('mac','hdd')) {
		if ($script:mode -eq 'missing-snapshot' -and $kind -eq 'hdd' -and $Generation -gt 1) { continue }
		$hash=Get-HardwareFixtureHash $fixture $kind
		if ($script:mode -eq 'wrong-hash' -and $Generation -gt 1) { $hash='0'*64 }
		$g=if ($script:mode -eq 'stale-generation' -and $Generation -gt 1) { 1 } else { $Generation }
		$records+=(@{srv='gs';run='contract';ts=$At.ToString('O');tpl='Applied Login hardware-ban snapshot: test';
			msg="Applied Login hardware-ban snapshot: kind=$kind, generation=$g, entries=2, distinctEntries=2, sha256=$hash"} | ConvertTo-Json -Compress)
	}
	[IO.File]::AppendAllLines((Join-Path $script:path 'logs/gs/gs.events.jsonl'),[string[]]$records)
}
# All Docker calls are intercepted; this script cannot launch a server, bot or database.
function docker {
	$a=[string[]]$args; $script:LASTEXITCODE=0; $script:commands.Add(($a -join '|'))
	if ($a[0] -eq 'compose') {
		Assert-HardwareTest (($a[0..4] -join '|') -ceq ($compose -join '|'))
		if ($a[5] -eq 'ps') { return $script:hardwareTestIds[$a[-1]] }
		if ($a[5] -eq 'logs') { throw 'Login restart must not depend on an optional connect-failure log.' }
		throw 'Unexpected compose operation.'
	}
	if ($a[0] -eq 'exec') {
		Assert-HardwareTest ($a[3] -ceq $script:hardwareTestIds.mysql -and $a[4] -ceq 'mysql')
		$state=[pscustomobject]@{zone='America/New_York';mac=$fixture.mac;hdd=$fixture.hdd}
		if ($script:mode -eq 'wrong-epoch' -and $script:phase -eq 'restarted') { $state.hdd=@([pscustomobject]@{key='E2E-B03';epoch=1},$fixture.hdd[1]) }
		return ($state | ConvertTo-Json -Depth 8 -Compress)
	}
	$service=@($script:hardwareTestIds.Keys | Where-Object { $script:hardwareTestIds[$_] -ceq $a[-1] })
	Assert-HardwareTest ($service.Count -eq 1)
	$service=$service[0]
	if ($a[0] -eq 'inspect') {
		$started=$oldStart
		if ($service -eq 'loginserver' -and $script:phase -eq 'restarted') { $started=$script:killAt.AddMilliseconds(1).ToString('O') }
		if ($service -eq 'gameserver' -and $script:mode -eq 'game-restarted' -and $script:phase -eq 'restarted') { $started=[DateTimeOffset]::UtcNow.ToString('O') }
		$networks=@{"${project}_default"=@{}}
		if ($script:mode -eq 'extra-network') { $networks['foreign']=@{} }
		return (@{Id=$(if ($script:mode -eq 'wrong-id') {'e'*64} else {$script:hardwareTestIds[$service]});Image='sha256:'+('f'*64);
			State=@{Running= -not ($service -eq 'loginserver' -and $script:phase -eq 'dead');Paused=$false;
				OOMKilled=($script:mode -eq 'oom' -and $script:phase -eq 'dead');ExitCode=$(if ($script:mode -eq 'wrong-exit') {1} else {137});StartedAt=$started};RestartCount=0;
			Config=@{Labels=@{'com.docker.compose.project'=$(if ($script:mode -eq 'wrong-project') {'aion'} else {$project});'com.docker.compose.service'=$service}};
			NetworkSettings=@{Networks=$networks}} | ConvertTo-Json -Depth 8 -Compress)
	}
	Assert-HardwareTest ($service -ceq 'loginserver')
	if ($a[0] -eq 'kill') {
		Assert-HardwareTest (($a[0..2] -join '|') -ceq 'kill|--signal|KILL')
		Assert-HardwareTest (Test-Path -LiteralPath (Join-Path $script:path 'login-server-crash-armed.json'))
		$script:phase='dead'; $script:killAt=[DateTimeOffset]::UtcNow; return
	}
	if ($a[0] -eq 'start') {
		$script:phase='restarted'; Start-Sleep -Milliseconds 1100
		Write-TestSnapshots 2 ([DateTimeOffset]::UtcNow)
		$beat=if ($script:mode -eq 'no-heartbeat') {$oldStart} else {[DateTimeOffset]::UtcNow.ToString('O')}
		[IO.File]::WriteAllText((Join-Path $script:path 'logs/ls/ls.events.jsonl'),(@{srv='ls';run='contract';ts=$beat;tpl='Server heartbeat fixture'} | ConvertTo-Json -Compress))
		return
	}
	throw 'Unexpected Docker action.'
}
function Test-HardwareOwner {
	$script:polls++
	if ($script:polls -gt 12) { throw 'Fixture observation exhausted without successful recovery.' }
	$plan=Join-Path $script:path 'login-server-crash-plan.json'; $receipt=Join-Path $script:path 'login-server-crash-armed.json'
	if ((Test-Path -LiteralPath $plan) -and -not (Test-Path -LiteralPath $receipt)) {
		$hash=[Convert]::ToHexStringLower([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes([IO.File]::ReadAllText($plan))))
		if ($script:mode -eq 'bad-arm-hash') { $hash='0'*64 }
		Write-LifecycleJson $receipt @{schemaVersion=1;run='contract';planSha256=$hash}
	}
	$outage=Join-Path $script:path 'login-outage-observed.json'
	if ($script:phase -eq 'dead' -and -not (Test-Path -LiteralPath $outage)) {
		Write-LifecycleJson $outage @{schemaVersion=1;run='contract';characterId=42;gameReplies=3;elapsedSeconds=3.1}
	}
}
try {
	Assert-HardwareTest ($fixture.mac[0].epoch -eq 1800032400000 -and $fixture.mac[1].epoch -eq 1815667200000)
	Assert-HardwareTest ((Get-HardwareFixtureHash $fixture 'mac') -ceq '2bf4448388d3209a635447f9f117fa4b6e1b74eb96d7582f12661dcf21fa3aec')
	Assert-HardwareTest ((Get-HardwareFixtureHash $fixture 'hdd') -ceq 'a5a2b7b5c1fd5b377e3cb590e650f1b95c9fa0190479995991f2bc00676fb3fc')
	Expect-HardwareFailure { New-HardwareBanFixture 2038 }; Expect-HardwareFailure { New-HardwareBanFixture 2026 }
	$sharedPath=Join-Path $testRoot 'writing.jsonl'
	$shared=[IO.File]::Open($sharedPath,[IO.FileMode]::CreateNew,[IO.FileAccess]::ReadWrite,[IO.FileShare]::ReadWrite)
	try {
		$bytes=[Text.Encoding]::UTF8.GetBytes("first`nsecond`n"); $shared.Write($bytes); $shared.Flush()
		Assert-HardwareTest (((Read-HardwareLogLines $sharedPath) -join ',') -ceq 'first,second')
	} finally { $shared.Dispose() }
	$request=[pscustomobject]@{schemaVersion=1;run='contract';characterId=42;refusedBots=@('b01','b02','b03','b04')}
	foreach ($mode in @('passed','wrong-id','wrong-project','extra-network','bad-arm-hash','wrong-exit','oom','game-restarted','stale-generation','wrong-epoch','wrong-hash','missing-snapshot','no-heartbeat')) {
		$script:mode=$mode; $script:phase='running'; $script:polls=0; $script:commands.Clear()
		$script:path=Join-Path $testRoot $mode
		New-Item -ItemType Directory -Path (Join-Path $script:path 'logs/gs'),(Join-Path $script:path 'logs/ls') | Out-Null
		Write-TestSnapshots 1 ([DateTimeOffset]::UtcNow)
		Write-LifecycleJson (Join-Path $script:path 'hardware-fixture.json') @{schemaVersion=1;run='contract';fixture=$fixture;seededUtc=[DateTimeOffset]::UtcNow.AddMinutes(-1).ToString('O')}
		$action={ Invoke-HardwareLoginFault $compose $project 'contract' $script:path $request { Test-HardwareOwner } }
		if ($mode -eq 'passed') { & $action } else { Expect-HardwareFailure $action }
		Assert-HardwareTest ((Test-Path -LiteralPath (Join-Path $script:path 'login-server-restarted.json')) -eq ($mode -eq 'passed'))
		if ($mode -in @('wrong-id','wrong-project','extra-network','bad-arm-hash')) { Assert-HardwareTest (@($script:commands | Where-Object { $_ -clike 'kill|*' }).Count -eq 0) }
	}
	# Missing, duplicate or shifted database rows and a different session zone must never pass.
	foreach ($defect in @('zone','missing','duplicate','details','epoch')) {
		$state=(@{zone='America/New_York';mac=$fixture.mac;hdd=$fixture.hdd} | ConvertTo-Json -Depth 6 | ConvertFrom-Json)
		switch ($defect) {'zone' {$state.zone='UTC'}; 'missing' {$state.mac=@($state.mac[0])}; 'duplicate' {$state.hdd=@($state.hdd[0],$state.hdd[0])}; 'details' {$state.mac[0].details='changed'}; 'epoch' {$state.hdd[1].epoch++}}
		Expect-HardwareFailure { Assert-HardwareDatabase $state $fixture }
	}
	foreach ($defect in @('run','id','subjects','count')) {
		$bad=($request | ConvertTo-Json | ConvertFrom-Json)
		switch ($defect) {'run' {$bad.run='other'}; 'id' {$bad.characterId=0}; 'subjects' {$bad.refusedBots=@('b01','b02','b03','b03')}; 'count' {$bad.refusedBots=@('b01')}}
		Expect-HardwareFailure { Assert-HardwareFaultRequest $bad 'contract' }
	}
	$tokens=$null; $errors=$null
	$runner=[Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'run-live.ps1'),[ref]$tokens,[ref]$errors)
	Assert-HardwareTest ($errors.Count -eq 0)
	$guards=@($runner.FindAll({param($node) $node -is [Management.Automation.Language.IfStatementAst] -and $node.Extent.Text.Contains("throw 'B4 must run alone")},$true))
	Assert-HardwareTest ($guards.Count -eq 1)
	$guard=[scriptblock]::Create($guards[0].Extent.Text)
	foreach ($defect in @('none','multiple','population','keep','record','short')) {
		$Scenario=@('B4'); $Bots=5; $Keep=$false; $WatcherMode='enforce'; $StepTimeoutSeconds=180
		switch ($defect) {'multiple' {$Scenario=@('B4','connect')}; 'population' {$Bots=6}; 'keep' {$Keep=$true}; 'record' {$WatcherMode='record'}; 'short' {$StepTimeoutSeconds=179}}
		if ($defect -eq 'none') { & $guard } else { Expect-HardwareFailure $guard }
	}
	Write-Host "Hardware controller contract passed ($script:checks assertions); Docker mocked, no bots/servers/databases started."
} finally {
	$resolved=[IO.Path]::GetFullPath($testRoot)
	if ((Split-Path $resolved -Parent) -cne [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') -or (Split-Path $resolved -Leaf) -cnotmatch '^aion-hardware-test-[a-f0-9]{32}$') { throw 'Unsafe hardware test cleanup.' }
	Remove-Item -LiteralPath $resolved -Recurse -Force
}
