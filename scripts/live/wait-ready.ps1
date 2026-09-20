[CmdletBinding()]
param(
	[Parameter(Mandatory)]
	[ValidatePattern('^[a-z0-9][a-z0-9_-]*$')]
	[string]$ProjectName,

	[Parameter(Mandatory)]
	[string]$RunDirectory,

	[ValidateRange(1, 3600)]
	[int]$TimeoutSeconds = 300,

	[string]$ComposeFile = (Join-Path $PSScriptRoot '../../docker/docker-compose.bots.yml'),

	[ValidateRange(0, 65535)]
	[int]$LoginPort = 0,

	[ValidateRange(0, 65535)]
	[int]$ChatPort = 0,

	[ValidateRange(0, 65535)]
	[int]$GamePort = 0,

	[ValidateRange(0, 65535)]
	[int]$AdminPort = 0,

	[switch]$SecondGameServer
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$composeFilePath = [IO.Path]::GetFullPath($ComposeFile)
$runPath = [IO.Path]::GetFullPath($RunDirectory)
if (-not (Test-Path -LiteralPath $composeFilePath -PathType Leaf)) {
	throw "Compose file does not exist: $composeFilePath"
}
if (-not (Test-Path -LiteralPath $runPath -PathType Container)) {
	throw "Run directory does not exist: $runPath"
}

function Resolve-Port([int]$specified, [string]$environmentName, [int]$fallback) {
	if ($specified -ne 0) { return $specified }
	$value = [Environment]::GetEnvironmentVariable($environmentName)
	$parsed = 0
	if (-not [string]::IsNullOrWhiteSpace($value) -and
		(-not [int]::TryParse($value, [ref]$parsed) -or $parsed -lt 1 -or $parsed -gt 65535)) {
		throw "$environmentName must be a TCP port (1-65535), got '$value'"
	}
	return $(if ($parsed -ne 0) { $parsed } else { $fallback })
}

function Test-TcpPort([int]$port) {
	$client = [Net.Sockets.TcpClient]::new()
	try {
		$connect = $client.ConnectAsync('127.0.0.1', $port)
		return $connect.Wait(250) -and $client.Connected
	}
	catch {
		return $false
	}
	finally {
		$client.Dispose()
	}
}

function Test-LogText([string]$path, [string]$text) {
	if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return $false }
	return [bool](Select-String -LiteralPath $path -SimpleMatch $text -Quiet)
}

$ports = [ordered]@{
	login = Resolve-Port $LoginPort 'AION_BOT_LOGIN_PORT' 12106
	chat = Resolve-Port $ChatPort 'AION_BOT_CHAT_PORT' 11241
	game = Resolve-Port $GamePort 'AION_BOT_GAME_PORT' 17777
	admin = Resolve-Port $AdminPort 'AION_BOT_ADMIN_PORT' 17780
}
if ($SecondGameServer) {
	$ports.game2 = Resolve-Port 0 'AION_BOT_GAME2_PORT' 17778
	$ports.admin2 = Resolve-Port 0 'AION_BOT_ADMIN2_PORT' 17781
	$ports.chat2 = Resolve-Port 0 'AION_BOT_CHAT2_PORT' 11242
}
$gameLog = Join-Path $runPath 'logs/gs/server_console.log'
$loginLog = Join-Path $runPath 'logs/ls/server_console.log'
$databasePassword = if ([string]::IsNullOrWhiteSpace($env:AION_BOT_DB_PASSWORD)) { 'aion-bots' } else { $env:AION_BOT_DB_PASSWORD }
$schemaQuery = @"
SELECT CASE WHEN
  (SELECT COUNT(*) FROM information_schema.tables
   WHERE (table_schema = 'aion_ls' AND table_name IN ('account_data', 'account_time', 'gameservers'))
      OR (table_schema = 'aion_gs' AND table_name IN ('inventory', 'player_effects', 'players'))
      OR (table_schema = 'aion_cs' AND table_name = 'chatlog')) = 7
  AND (SELECT COUNT(*) FROM information_schema.columns
       WHERE table_schema = 'aion_gs'
         AND ((table_name = 'inventory' AND column_name = 'rank_limit_expire_time')
           OR (table_name = 'player_effects' AND column_name = 'magical_criticals'))) = 2
  AND (SELECT COUNT(*) FROM information_schema.columns
       WHERE table_schema = 'aion_ls' AND table_name = 'account_data' AND column_name = 'toll') = 0
  AND (SELECT COUNT(*) FROM information_schema.tables
       WHERE table_schema = 'aion_ls' AND table_name = 'account_rewards') = 0
THEN 1 ELSE 0 END;
"@
if ($SecondGameServer) {
	$schemaQuery += @"
SELECT CASE WHEN
  (SELECT COUNT(*) FROM information_schema.tables WHERE
    (table_schema='aion_gs2' AND table_name IN ('inventory','player_effects','players'))
    OR (table_schema='aion_cs2' AND table_name='chatlog')) = 4
  AND (SELECT COUNT(*) FROM aion_ls.gameservers WHERE id IN (1,2)) = 2
  AND (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema='aion_gs2'
    AND ((table_name='inventory' AND column_name='rank_limit_expire_time')
      OR (table_name='player_effects' AND column_name='magical_criticals'))) = 2
THEN 1 ELSE 0 END;
"@
}

$previousRunDirectory = $env:AION_E2E_RUN_DIR
$env:AION_E2E_RUN_DIR = $runPath
$composeArgs = @('compose', '-f', $composeFilePath, '-p', $ProjectName)
$schemaReady = $false
$stopwatch = [Diagnostics.Stopwatch]::StartNew()
$lastMissing = @()

try {
	do {
		$missing = [Collections.Generic.List[string]]::new()
		foreach ($entry in $ports.GetEnumerator()) {
			if (-not (Test-TcpPort $entry.Value)) {
				$missing.Add("$($entry.Key) port $($entry.Value)")
			}
		}

		if (-not (Test-LogText $gameLog 'Game server started in ')) {
			$missing.Add('game-server startup log')
		}
		if (-not (Test-LogText $loginLog 'Gameserver #1 is now online')) {
			$missing.Add('login-server game-server registration log')
		}
		if ($SecondGameServer) {
			if (-not (Test-LogText (Join-Path $runPath 'logs/gs2/server_console.log') 'Game server started in ')) { $missing.Add('second game-server startup') }
			if (-not (Test-LogText $loginLog 'Gameserver #2 is now online')) { $missing.Add('second Login registration') }
			foreach ($pair in @(@('cs',1),@('cs2',2))) {
				if (-not (Test-LogText (Join-Path $runPath "logs/$($pair[0])/server_console.log") "Gameserver #$($pair[1]) is now online")) { $missing.Add("$($pair[0]) registration") }
			}
		}

		if (-not $schemaReady) {
			try {
				$schemaResult = (& docker @composeArgs exec -T mysql mysql -uroot "-p$databasePassword" -Nse $schemaQuery 2>$null | Out-String).Trim()
				$expectedSchema = if ($SecondGameServer) { "1`n1" } else { '1' }
				$schemaReady = $LASTEXITCODE -eq 0 -and $schemaResult.Replace("`r",'') -ceq $expectedSchema
			}
			catch {
				$schemaReady = $false
			}
		}
		if (-not $schemaReady) {
			$missing.Add('required database schema')
		}

		if ($missing.Count -eq 0) {
			Write-Host ("Bot stack ready after {0:n1}s: ports, startup logs, registration and schema are healthy." -f $stopwatch.Elapsed.TotalSeconds)
			return
		}

		$lastMissing = $missing.ToArray()
		if ($stopwatch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
			Start-Sleep -Seconds 1
		}
	} while ($stopwatch.Elapsed.TotalSeconds -lt $TimeoutSeconds)

	Write-Host 'Compose status at readiness timeout:'
	& docker @composeArgs ps
	foreach ($logPath in $loginLog, $gameLog) {
		if (Test-Path -LiteralPath $logPath -PathType Leaf) {
			Write-Host "Tail of $logPath`:"
			Get-Content -LiteralPath $logPath -Tail 20
		}
	}
	throw "Bot stack was not ready after $TimeoutSeconds seconds; missing: $($lastMissing -join ', ')"
}
finally {
	if ($null -eq $previousRunDirectory) { Remove-Item Env:AION_E2E_RUN_DIR -ErrorAction SilentlyContinue }
	else { $env:AION_E2E_RUN_DIR = $previousRunDirectory }
}
