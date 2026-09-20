[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'lifecycle-controller.ps1')
$testRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) ('aion-lifecycle-test-' + [Guid]::NewGuid().ToString('N'))))
New-Item -ItemType Directory -Path $testRoot | Out-Null
$project = 'aion-bots-contract'
$compose = @('compose', '-f', 'fixture.yml', '-p', $project)
$containerId = 'a' * 64
$script:commands = [Collections.Generic.List[string]]::new()
$script:mode = 'passed'; $script:stage = 'running'; $script:inspections = 0
$script:started = [DateTimeOffset]::UtcNow.AddMinutes(-20).ToString('O')
$script:casePath = ''; $script:ownerChecks = 0; $script:armReceipt = $true
$script:assertions = 0
$script:sqlReads = 0

function Assert-True([bool]$Value, [string]$Message) {
	$script:assertions++
	if (-not $Value) { throw $Message }
}
function Assert-Fails([scriptblock]$Action, [string]$Message) {
	$failure = $null
	try { & $Action | Out-Null } catch { $failure = $_ }
	Assert-True ($null -ne $failure -and $failure.ToString().Contains($Message)) "Expected '$Message', got '$failure'."
}

# Every Docker operation is intercepted. These tests never contact a Docker daemon.
function docker {
	$Arguments = [string[]]$args
	$script:LASTEXITCODE = 0
	$script:commands.Add(($Arguments -join '|'))
	if ($Arguments[0] -eq 'compose') {
		Assert-True (($Arguments[0..4] -join '|') -ceq ($compose -join '|')) 'Wrong compose scope.'
		switch ($Arguments[5]) {
			'ps' {
				Assert-True (($Arguments[5..8] -join '|') -ceq 'ps|--all|--quiet|gameserver') 'Wrong container selection.'
				if ($script:mode -eq 'list-failed') { $script:LASTEXITCODE = 1; return }
				if ($script:mode -eq 'missing') { return }
				if ($script:mode -eq 'ambiguous') { return @($containerId, ('b' * 64)) }
				if ($script:mode -eq 'short-id') { return 'aion-mysql' }
				return $containerId
			}
			'logs' {
				Assert-True ($Arguments.Count -eq 11 -and $Arguments[6] -ceq '--since' -and
					($Arguments[8..9] -join '|') -ceq '--no-color|--no-log-prefix') 'Restart log query must exclude earlier boots.'
				Assert-True ([DateTimeOffset]::Parse($Arguments[7]) -gt [DateTimeOffset]::UtcNow.AddMinutes(-1)) 'Stale log cutoff.'
				if ($script:mode -eq 'log-failed') { $script:LASTEXITCODE = 1; return }
				if ($script:mode -eq 'old-logs-only') { return 'Nothing new since the kill.' }
				if ($Arguments[-1] -eq 'gameserver') { return 'Game server started in 10 seconds' }
				Assert-True ($Arguments[-1] -ceq 'loginserver') 'Wrong registration source.'
				if ($script:mode -eq 'missing-registration') { return 'Unrelated login log' }
				return 'Gameserver #1 is now online'
			}
			'exec' {
				$script:sqlReads++
				Assert-True (($Arguments[6..11] -join '|') -ceq '-T|-e|MYSQL_PWD=aion-bots|mysql|mysql|-uroot') 'SQL must use only Docker MySQL.'
				Assert-True ($Arguments[12] -ceq '-Nse' -and $Arguments[13] -match '^SELECT JSON_OBJECT\(.+ FROM aion_gs.players WHERE id=42$') 'SQL assertion changed scope.'
				if ($script:mode -eq 'sql-failed') { $script:LASTEXITCODE = 1; return }
				if ($script:mode -eq 'sql-empty') { return }
				if ($script:mode -eq 'sql-ambiguous') { return @('{}', '{}') }
				if ($script:mode.StartsWith('bot-')) {
					$row = @{ id=42; name='Aslifecycle'; worldId=220010000; x=1; y=2; z=3; online=1 }
					if ($script:sqlReads -gt 1 -or $script:mode -eq 'bot-no-delayed-save') { $row.x=4; $row.y=5; $row.z=6 }
					if ($script:mode -eq 'bot-wrong-subject') { $row.id=43 }
					if ($script:mode -eq 'bot-offline') { $row.online=0 }
					if ($script:mode -eq 'bot-already-saved' -and $script:sqlReads -gt 2) { $row.x=7; $row.y=8; $row.z=9 }
					return ($row | ConvertTo-Json -Compress)
				}
				return '{"id":42,"name":"Aslifecycle","worldId":220010000,"x":1,"y":2,"z":3,"online":1}'
			}
			default { throw 'Unexpected compose command.' }
		}
	}
	Assert-True ($Arguments[-1] -ceq $containerId) 'Docker mutation/inspection used a non-exact id.'
	switch ($Arguments[0]) {
		'inspect' {
			$script:inspections++
			if ($script:mode -eq 'inspect-failed') { $script:LASTEXITCODE = 1; return }
			$networks = @{ "${project}_default" = @{} }
			if ($script:mode -eq 'wrong-network') { $networks = @{ other = @{} } }
			if ($script:mode -eq 'extra-network') { $networks['other'] = @{} }
			@{
				Id = $(if ($script:mode -eq 'wrong-id' -or ($script:mode -eq 'changed-id' -and $script:inspections -gt 1)) { 'b' * 64 } else { $containerId })
				Image = $(if ($script:mode -eq 'changed-image' -and $script:inspections -gt 1) { 'sha256:changed' } else { 'sha256:fixture' })
				State = @{ Running = $script:stage -ne 'dead' -and $script:mode -ne 'stopped'; StartedAt = $script:started;
					ExitCode = $(if ($script:mode -eq 'wrong-exit') { 1 } else { 137 }); OOMKilled = $script:mode -eq 'oom' }
				Config = @{ Labels = @{
					'com.docker.compose.project' = $(if ($script:mode -eq 'wrong-project') { 'aion' } else { $project })
					'com.docker.compose.service' = $(if ($script:mode -eq 'wrong-service') { 'mysql' } else { 'gameserver' })
				} }
				NetworkSettings = @{ Networks = $networks }
			} | ConvertTo-Json -Depth 8 -Compress
		}
		'kill' {
			Assert-True (($Arguments[0..2] -join '|') -ceq 'kill|--signal|KILL') 'Not a hard server crash.'
			Assert-True (Test-Path -LiteralPath (Join-Path $script:casePath 'game-server-crash-armed.json')) 'Kill preceded watcher arming.'
			if ($script:mode -eq 'kill-failed') { $script:LASTEXITCODE = 1; return }
			$script:stage = 'dead'
		}
		'start' {
			Assert-True ($script:stage -eq 'dead') 'Restart preceded verified death.'
			if ($script:mode -eq 'start-failed') { $script:LASTEXITCODE = 1; return }
			$script:stage = 'restarted'
			if ($script:mode -ne 'stale-process') { $script:started = [DateTimeOffset]::UtcNow.AddMilliseconds(100).ToString('O') }
		}
		default { throw 'Unexpected Docker command.' }
	}
}

function Test-Owner {
	$script:ownerChecks++
	if ($script:mode -eq 'owner-dead' -or ($script:mode -eq 'owner-lost-before-kill' -and $script:ownerChecks -eq 4)) { throw 'Owner is no longer live.' }
	$planPath = Join-Path $script:casePath 'game-server-crash-plan.json'
	$receiptPath = Join-Path $script:casePath 'game-server-crash-armed.json'
	if ((Test-Path -LiteralPath $planPath) -and -not (Test-Path -LiteralPath $receiptPath)) {
		if (-not $script:armReceipt) { return } # Exercise the real arming timeout, not a substituted error.
		$hash = [Convert]::ToHexStringLower([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes([IO.File]::ReadAllText($planPath))))
		Write-LifecycleJson $receiptPath @{ schemaVersion = 1; run = $(if ($script:mode -eq 'receipt-run') { 'other' } else { 'contract' });
			planSha256 = $(if ($script:mode -eq 'receipt-hash') { 'b' * 64 } else { $hash }) }
	}
	# Stop missing-log cases promptly without weakening the actual recovery deadline.
	if ($script:stage -eq 'restarted' -and $script:mode -in @('old-logs-only', 'missing-registration') -and $script:ownerChecks -gt 5) {
		throw 'No fresh recovery evidence.'
	}
}

$previousPassword = $env:AION_BOT_DB_PASSWORD
$env:AION_BOT_DB_PASSWORD = 'aion-bots'
try {
	$request = '{"schemaVersion":1,"run":"contract","characterId":42,"characterName":"Aslifecycle","worldId":220010000,"initial":{"x":1,"y":2,"z":3},"saved":{"x":4,"y":5,"z":6}}' | ConvertFrom-Json
	Assert-LifecyclePositionRequest $request 'contract'
	foreach ($field in @('schemaVersion', 'run', 'characterId', 'characterName', 'worldId')) {
		$bad = $request | ConvertTo-Json -Depth 5 | ConvertFrom-Json
		$bad.$field = 'invalid!'
		Assert-Fails { Assert-LifecyclePositionRequest $bad 'contract' } 'Invalid lifecycle character identity'
	}
	foreach ($badId in @(0, -1, 2147483648L, 42.0, $null)) {
		$bad = $request | ConvertTo-Json -Depth 5 | ConvertFrom-Json; $bad.characterId = $badId
		Assert-Fails { Assert-LifecyclePositionRequest $bad 'contract' } 'Invalid lifecycle character identity'
	}
	foreach ($point in @('initial', 'saved')) {
		foreach ($axis in @('x', 'y', 'z')) {
			foreach ($badCoordinate in @('4', $true, $null, [double]::NaN, [double]::PositiveInfinity, 10001)) {
				$bad = $request | ConvertTo-Json -Depth 5 | ConvertFrom-Json; $bad.$point.$axis = $badCoordinate
				Assert-Fails { Assert-LifecyclePositionRequest $bad 'contract' } 'Invalid lifecycle position'
			}
		}
	}
	$bad = $request | ConvertTo-Json -Depth 5 | ConvertFrom-Json; $bad.saved = $bad.initial
	Assert-Fails { Assert-LifecyclePositionRequest $bad 'contract' } 'did not change position'
	Assert-True (Test-LifecyclePosition @{ x=1.09; y=2.09; z=3.09 } $request.initial) 'Position rounding tolerance was lost.'
	Assert-True (-not (Test-LifecyclePosition @{ x=1.11; y=2; z=3 } $request.initial)) 'Position tolerance is too broad.'
	Assert-Fails { Get-LifecycleContainer $compose 'aion' } 'isolated bot project'
	foreach ($arguments in @(@('compose','-f','fixture.yml','-p','aion'), @('compose','-f','fixture.yml','-p',$project,'down'), @('compose','-f','fixture.yml','-p',$project,'--profile','other'))) {
		Assert-Fails { Get-LifecycleContainer $arguments $project } 'Docker arguments'
	}
	Assert-True ($script:commands.Count -eq 0) 'Unsafe selection contacted Docker.'
	Assert-LifecycleComposeArguments ($compose + @('--profile','bot-runner')) $project
	foreach ($script:mode in @('list-failed','missing','ambiguous','short-id','inspect-failed','stopped','wrong-id','wrong-project','wrong-service','wrong-network','extra-network')) {
		Assert-Fails { Get-LifecycleContainer $compose $project } $(if ($script:mode -in @('list-failed','missing','ambiguous','short-id')) { 'exact game-server container id' } elseif ($script:mode -eq 'inspect-failed') { 'inspection failed' } else { 'not running exclusively' })
	}
	$script:mode = 'passed'
	Assert-True ((Read-LifecyclePlayerRow $compose 42).id -eq 42) 'SQL row not returned.'
	Assert-Fails { Read-LifecyclePlayerRow $compose 0 } 'positive character id'
	foreach ($script:mode in @('sql-failed','sql-empty','sql-ambiguous')) { Assert-Fails { Read-LifecyclePlayerRow $compose 42 } 'exactly one Docker player row' }

	foreach ($case in @(
		@{ mode='passed'; error=''; kills=1; starts=1 },
		@{ mode='owner-dead'; error='Owner is no longer live'; kills=0; starts=0 },
		@{ mode='owner-lost-before-kill'; error='Owner is no longer live'; kills=0; starts=0 },
		@{ mode='no-receipt'; error='Watcher did not arm'; kills=0; starts=0 },
		@{ mode='receipt-hash'; error='receipt mismatch'; kills=0; starts=0 },
		@{ mode='receipt-run'; error='receipt mismatch'; kills=0; starts=0 },
		@{ mode='changed-id'; error='not running exclusively'; kills=0; starts=0 },
		@{ mode='changed-image'; error='identity changed'; kills=0; starts=0 },
		@{ mode='kill-failed'; error='SIGKILL failed'; kills=1; starts=0 },
		@{ mode='wrong-exit'; error='did not exit'; kills=1; starts=0 },
		@{ mode='oom'; error='did not exit'; kills=1; starts=0 },
		@{ mode='start-failed'; error='restart failed'; kills=1; starts=1 },
		@{ mode='stale-process'; error='has not restarted'; kills=1; starts=1 },
		@{ mode='log-failed'; error='fresh game-server startup logs'; kills=1; starts=1 },
		@{ mode='old-logs-only'; error='No fresh recovery evidence'; kills=1; starts=1 },
		@{ mode='missing-registration'; error='No fresh recovery evidence'; kills=1; starts=1 }
	)) {
		$script:mode = $case.mode; $script:stage = 'running'; $script:inspections = 0; $script:ownerChecks = 0
		$script:started = [DateTimeOffset]::UtcNow.AddMinutes(-20).ToString('O')
		$script:armReceipt = $case.mode -ne 'no-receipt'; $script:commands.Clear()
		$script:casePath = Join-Path $testRoot $case.mode
		New-Item -ItemType Directory -Path $script:casePath | Out-Null
		$action = { Invoke-LifecycleCrash $compose $project 'contract' $script:casePath { Test-Owner } }
		if ($case.error -eq '') { & $action } else { Assert-Fails $action $case.error }
		Assert-True (@($script:commands.Where({ $_.StartsWith('kill|') })).Count -eq $case.kills) "Wrong kill count: $($case.mode)"
		Assert-True (@($script:commands.Where({ $_.StartsWith('start|') })).Count -eq $case.starts) "Wrong restart count: $($case.mode)"
		Assert-True ((Test-Path -LiteralPath (Join-Path $script:casePath 'game-server-restarted.json')) -eq ($case.error -eq '')) "False recovery receipt: $($case.mode)"
		if ($case.error -eq '') {
			$plan = Get-Content -Raw -LiteralPath (Join-Path $script:casePath 'game-server-crash-plan.json') | ConvertFrom-Json
			Assert-True (([DateTimeOffset]$plan.killDeadlineUtc - [DateTimeOffset]$plan.armedUtc).TotalSeconds -eq 30 -and
				([DateTimeOffset]$plan.recoveryDeadlineUtc - [DateTimeOffset]$plan.armedUtc).TotalSeconds -eq 180) 'Crash deadlines changed.'
			Assert-Fails $action 'already exists'
		}
	}
	Assert-Fails { Wait-LifecycleRestart $compose $project @{} ([DateTimeOffset]::UtcNow) ([DateTimeOffset]::UtcNow.AddSeconds(-1)) {} } 'recovery deadline'

	# Real tiny child processes exercise launch/exit cleanup; they are not bot or server processes.
	$watcher = [Diagnostics.Process]::GetCurrentProcess()
	try {
		foreach ($case in @(@{ name='launch-failed'; file='aion-test-nonexistent-executable'; args=@() },
			@{ name='early-zero'; file=(Get-Process -Id $PID).Path; args=@('-NoProfile','-Command','[Console]::Out.WriteLine("child output"); exit 0') },
			@{ name='early-error'; file=(Get-Process -Id $PID).Path; args=@('-NoProfile','-Command','[Console]::Error.WriteLine("child error"); exit 7') })) {
			$path = Join-Path $testRoot $case.name; New-Item -ItemType Directory -Path $path | Out-Null
			$failure = $null
			try { Invoke-LifecycleBot $case.file $case.args $compose $project 'contract' $path $watcher | Out-Null } catch { $failure = $_ }
			Assert-True ($null -ne $failure) 'Incomplete lifecycle child reported success.'
			$evidence = Get-Content -Raw -LiteralPath (Join-Path $path 'lifecycle-controller.json') | ConvertFrom-Json
			Assert-True ($evidence.phase -eq 'waiting-for-position' -and -not [string]::IsNullOrWhiteSpace($evidence.failure)) 'Failure evidence was not retained.'
			if ($case.name -eq 'launch-failed') { Assert-True (-not $failure.ToString().Contains('outputCopy')) 'Cleanup masked launch failure.' }
			if ($case.name -eq 'early-zero') { Assert-True ((Get-Content -Raw -LiteralPath (Join-Path $path 'lifecycle-bot.stdout.log')).Contains('child output')) 'Child stdout was lost.' }
			if ($case.name -eq 'early-error') { Assert-True ((Get-Content -Raw -LiteralPath (Join-Path $path 'lifecycle-bot.stderr.log')).Contains('child error')) 'Child stderr was lost.' }
		}
		foreach ($script:mode in @('bot-passed', 'bot-wrong-subject', 'bot-offline', 'bot-no-delayed-save', 'bot-already-saved')) {
			$script:casePath = Join-Path $testRoot $script:mode
			New-Item -ItemType Directory -Path $script:casePath | Out-Null
			$script:stage = 'running'; $script:sqlReads = 0; $script:inspections = 0; $script:commands.Clear()
			$script:started = [DateTimeOffset]::UtcNow.AddMinutes(-20).ToString('O')
			# This artifact producer stands in for BOTH the bot and watcher. It tests the owner's
			# real process/poll/SQL/receipt state machine, not a gameplay or watcher integration run.
			$fixture = {
				$ErrorActionPreference = 'Stop'
				$path = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('__PATH__'))
				function Publish($name, $value) {
					$final = Join-Path $path $name
					[IO.File]::WriteAllText($final + '.tmp', ($value | ConvertTo-Json -Depth 6))
					[IO.File]::Move($final + '.tmp', $final)
				}
				function Await($name) {
					$deadline = [DateTimeOffset]::UtcNow.AddSeconds(30)
					while (-not (Test-Path -LiteralPath (Join-Path $path $name))) {
						if ([DateTimeOffset]::UtcNow -gt $deadline) { throw "Fixture timed out waiting for $name" }
						Start-Sleep -Milliseconds 25
					}
				}
				[Console]::Out.WriteLine($PID)
				Publish 'lifecycle-position.json' @{ schemaVersion=1; run='contract'; characterId=42; characterName='Aslifecycle'; worldId=220010000;
					initial=@{x=1;y=2;z=3}; saved=@{x=4;y=5;z=6} }
				Await 'lifecycle-saved.json'
				Publish 'game-server-crash-request.json' @{ schemaVersion=1; run='contract'; characterId=42; characterName='Aslifecycle'; worldId=220010000;
					initial=@{x=4;y=5;z=6}; saved=@{x=7;y=8;z=9} }
				Await 'game-server-crash-plan.json'
				$hash = [Convert]::ToHexStringLower([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes([IO.File]::ReadAllText((Join-Path $path 'game-server-crash-plan.json')))))
				Publish 'game-server-crash-armed.json' @{schemaVersion=1;run='contract';planSha256=$hash}
				Await 'game-server-restarted.json'
				exit 0
			}.ToString().Replace('__PATH__', [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($script:casePath)))
			$arguments = @('-NoProfile','-EncodedCommand', [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($fixture)))
			$action = { Invoke-LifecycleBot (Get-Process -Id $PID).Path $arguments $compose $project 'contract' $script:casePath $watcher }
			if ($script:mode -eq 'bot-passed') { Assert-True ((& $action) -eq 0) 'Completed controller returned failure.' }
			else { Assert-Fails $action $(switch ($script:mode) {
				'bot-no-delayed-save' { 'pre-movement position' }
				'bot-already-saved' { 'Unsaved movement was already persisted' }
				default { 'identity/online state changed' }
			}) }
			$evidence = Get-Content -Raw -LiteralPath (Join-Path $script:casePath 'lifecycle-controller.json') | ConvertFrom-Json
			Assert-True (($evidence.phase -eq 'restarted') -eq ($script:mode -eq 'bot-passed')) 'Controller reported the wrong final phase.'
			Assert-True (@($script:commands.Where({ $_.StartsWith('kill|') })).Count -eq [int]($script:mode -eq 'bot-passed')) 'SQL failure did not prevent fault injection.'
			$fixturePid = [int](Get-Content -LiteralPath (Join-Path $script:casePath 'lifecycle-bot.stdout.log') -First 1)
			Assert-True ($null -eq (Get-Process -Id $fixturePid -ErrorAction SilentlyContinue)) 'Controller left its child process alive.'
			if ($script:mode -eq 'bot-passed') { Assert-True ($evidence.samples.Count -eq 2 -and $evidence.samples[0].row.x -eq 1 -and $evidence.samples[1].row.x -eq 4) 'Delayed save evidence was lost.' }
		}
	} finally { $watcher.Dispose() }
	# Execute the actual public runner's O1 guards, allowance scope and backend dispatch.
	$tokens = $null; $errors = $null
	$runner = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'run-live.ps1'), [ref]$tokens, [ref]$errors)
	Assert-True ($errors.Count -eq 0) 'LIVE runner did not parse.'
	$guards = @($runner.FindAll({ param($node)
		$node -is [Management.Automation.Language.IfStatementAst] -and $node.Extent.Text.Contains("throw 'O1 must run alone")
	}, $true))
	Assert-True ($guards.Count -eq 1) 'Missing O1 admission guard.'
	$guard = [scriptblock]::Create($guards[0].Extent.Text)
	foreach ($invalid in @('none','multiple','population','keep','record','short')) {
		$Scenario=@('O1'); $Bots=1; $Keep=$false; $WatcherMode='enforce'; $StepTimeoutSeconds=1200
		switch ($invalid) {
			'multiple' { $Scenario=@('O1','connect') }
			'population' { $Bots=2 }
			'keep' { $Keep=$true }
			'record' { $WatcherMode='record' }
			'short' { $StepTimeoutSeconds=1049 }
		}
		if ($invalid -eq 'none') { & $guard } else { Assert-Fails $guard 'O1 must run alone' }
	}
	$allowances = @($runner.FindAll({ param($node)
		$node -is [Management.Automation.Language.IfStatementAst] -and $node.Extent.Text.Contains('$bootEntries =')
	}, $true))
	Assert-True ($allowances.Count -eq 1) 'Missing scoped O1 boot allowance.'
	$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
	$runPath = Join-Path $testRoot 'runner-allowance'; New-Item -ItemType Directory -Path $runPath | Out-Null
	$Scenario=@('O1'); $watcherArguments=@('original'); $Run='contract'
	$originalAllowlist = Get-Content -Raw -LiteralPath (Join-Path $repoRoot 'parity-artifacts/e2e/log-allowlist.json')
	. ([scriptblock]::Create($allowances[0].Extent.Text))
	$scoped = @(Get-Content -Raw -LiteralPath (Join-Path $runPath 'lifecycle-log-allowlist.json') | ConvertFrom-Json)
	$original = @($originalAllowlist | ConvertFrom-Json)
	Assert-True ($scoped.Count -eq $original.Count) 'O1 created a new fingerprint allowance.'
	foreach ($entry in $original) {
		$copy = @($scoped | Where-Object fp -CEQ $entry.fp)
		Assert-True ($copy.Count -eq 1) 'Allowance fingerprint changed.'
		if ($entry.fp -eq '231c488f') {
			Assert-True ($copy[0].maxCount -eq 2 -and $copy[0].owner -ceq $entry.owner -and $copy[0].expires -eq $entry.expires) 'Two-boot scope lost count/owner/expiry.'
		} else { Assert-True (($copy[0] | ConvertTo-Json -Depth 8) -ceq ($entry | ConvertTo-Json -Depth 8)) 'Unrelated allowance was broadened.' }
	}
	Assert-True ((Get-Content -Raw -LiteralPath (Join-Path $repoRoot 'parity-artifacts/e2e/log-allowlist.json')) -ceq $originalAllowlist) 'Global allowances were changed.'
	Assert-True (($watcherArguments -join '|').Contains('--expect-game-server-crash|true|--allowlist|')) 'O1 watcher did not arm fault checking.'
	$dispatches = @($runner.FindAll({ param($node)
		$node -is [Management.Automation.Language.IfStatementAst] -and
			$node.Clauses[0].Item1.Extent.Text -ceq '$Scenario -contains ''O1'' -or $Scenario -contains ''B2F'''
	}, $true))
	Assert-True ($dispatches.Count -eq 1) 'O1 bypassed the owning controller.'
	function Get-LiveDockerBotArguments { return @('exec','-T','botrunner','dotnet','/app/Aion.LiveBots.dll') }
	function Invoke-LifecycleBot {
		param($FileName, $Arguments, $ComposeArguments, $ProjectName, $Run, $RunDirectory, $Watcher)
		Assert-True ($Scenario -contains 'O1') 'Chat fault was routed to the Game controller.'
		Assert-FaultDispatch @PSBoundParameters
	}
	function Invoke-ChatFaultBot {
		param($FileName, $Arguments, $ComposeArguments, $ProjectName, $Run, $RunDirectory, $Watcher)
		Assert-True ($Scenario -contains 'B2F') 'Game fault was routed to the Chat controller.'
		Assert-FaultDispatch @PSBoundParameters
	}
	function Assert-FaultDispatch {
		param($FileName, $Arguments, $ComposeArguments, $ProjectName, $Run, $RunDirectory, $Watcher)
		Assert-True ($FileName -ceq $(if ($BotExecution -eq 'Host') { 'dotnet' } else { 'docker' })) 'Wrong lifecycle execution host.'
		Assert-True ($ProjectName -ceq $project -and $Run -ceq 'contract' -and $RunDirectory -ceq $runPath -and $Watcher -ceq 'fixture-watcher') 'Lifecycle owner settings lost.'
		Assert-True (($Arguments -join '|') -ceq $(if ($BotExecution -eq 'Host') { 'host-arguments' } else { ($compose -join '|') + '|exec|-T|botrunner|dotnet|/app/Aion.LiveBots.dll' })) 'Lifecycle child arguments lost.'
		return $expectedExit
	}
	$botArguments=@('host-arguments'); $composeArgs=$compose; $projectName=$project; $watcherProcess='fixture-watcher'; $dockerEndpoints=@{}
	foreach ($BotExecution in @('Host','Docker')) {
		foreach ($Scenario in @(@('O1'), @('B2F'))) {
			foreach ($expectedExit in @(0, 7)) {
				$botExitCode=-1
				. ([scriptblock]::Create($dispatches[0].Extent.Text))
				Assert-True ($botExitCode -eq $expectedExit) 'Fault controller exit code was lost.'
			}
		}
	}
	Write-Host "Lifecycle controller contract passed ($script:assertions assertions); Docker was mocked, no bots or servers started."
}
finally {
	if ($null -eq $previousPassword) { Remove-Item Env:AION_BOT_DB_PASSWORD -ErrorAction SilentlyContinue } else { $env:AION_BOT_DB_PASSWORD = $previousPassword }
	$parent = [IO.Path]::GetFullPath((Split-Path $testRoot -Parent)).TrimEnd([IO.Path]::DirectorySeparatorChar)
	if ($parent -ne [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) -or
		(Split-Path $testRoot -Leaf) -notmatch '^aion-lifecycle-test-[a-f0-9]{32}$') { throw 'Unsafe lifecycle test cleanup path.' }
	Remove-Item -LiteralPath $testRoot -Recurse -Force
}
