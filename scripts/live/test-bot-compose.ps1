[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$composeFile = Join-Path $repoRoot 'docker/docker-compose.bots.yml'
$contractRun = Join-Path ([IO.Path]::GetTempPath()) ("aion-bots-compose-contract-{0}" -f [Guid]::NewGuid().ToString('N'))
$previousRunDirectory = $env:AION_E2E_RUN_DIR
$previousOverlayDirectory = $env:AION_BOT_OVERLAY_DIR

function Assert-Contract([bool]$condition, [string]$message) {
	if (-not $condition) {
		throw "Bot compose contract failed: $message"
	}
}

try {
	foreach ($server in 'ls', 'cs', 'gs') {
		New-Item -ItemType Directory -Force -Path (Join-Path $contractRun "logs/$server") | Out-Null
	}
	$env:AION_E2E_RUN_DIR = $contractRun
	Remove-Item Env:AION_BOT_OVERLAY_DIR -ErrorAction SilentlyContinue

	$json = & docker compose -f $composeFile -p aion-bots-contract config --format json
	if ($LASTEXITCODE -ne 0) {
		throw "docker compose config exited with $LASTEXITCODE"
	}
	$config = ($json -join "`n") | ConvertFrom-Json
	$services = $config.services
	Assert-Contract (($services.PSObject.Properties.Name | Sort-Object) -join ',' -eq 'chatserver,gameserver,loginserver,mysql') 'unexpected service set'

	foreach ($serviceName in $services.PSObject.Properties.Name) {
		$service = $services.$serviceName
		Assert-Contract (-not ($service.PSObject.Properties.Name -contains 'container_name')) "$serviceName pins container_name"
	}

	Assert-Contract ($services.loginserver.image -eq 'aion-bots-loginserver:local') 'login image is not bot-scoped'
	Assert-Contract ($services.chatserver.image -eq 'aion-bots-chatserver:local') 'chat image is not bot-scoped'
	Assert-Contract ($services.gameserver.image -eq 'aion-bots-gameserver:local') 'game image is not bot-scoped'
	Assert-Contract ($services.mysql.tmpfs -contains '/var/lib/mysql:size=1g,mode=1777') 'MySQL data is not tmpfs-backed'
	$initScript = $services.mysql.volumes | Where-Object { $_.target -eq '/docker-entrypoint-initdb.d/00-init.sh' }
	Assert-Contract ($initScript.read_only -and [IO.Path]::GetFullPath($initScript.source) -eq [IO.Path]::GetFullPath((Join-Path $repoRoot 'docker/mysql/init/00-init.sh'))) 'shared database initializer mount is wrong'
	$botSeed = $services.mysql.volumes | Where-Object { $_.target -eq '/docker-entrypoint-initdb.d/10-bot-seed.sql' }
	Assert-Contract ($botSeed.read_only -and [IO.Path]::GetFullPath($botSeed.source) -eq [IO.Path]::GetFullPath((Join-Path $repoRoot 'docker/bots/seed/10-bot-seed.sql'))) 'bot seed mount is wrong'

	$publishedPorts = @($services.loginserver.ports.published) + @($services.chatserver.ports.published) + @($services.gameserver.ports.published)
	Assert-Contract ($publishedPorts -contains '12106') 'login port 12106 is not published'
	Assert-Contract ($publishedPorts -contains '11241') 'chat port 11241 is not published'
	Assert-Contract ($publishedPorts -contains '17777') 'game port 17777 is not published'
	Assert-Contract ($publishedPorts -contains '17780') 'admin port 17780 is not published'
	Assert-Contract (-not ($publishedPorts | Where-Object { $_ -in '2106', '10241', '7777', '7780' })) 'a production-default host port is published'
	$adminPort = $services.gameserver.ports | Where-Object { $_.target -eq 7780 }
	Assert-Contract ($adminPort.host_ip -eq '127.0.0.1') 'admin API is not loopback-only'

	$expectedOverlay = [IO.Path]::GetFullPath((Join-Path $repoRoot 'docker/bots/overlay'))
	foreach ($entry in @(@('loginserver', 'ls'), @('chatserver', 'cs'), @('gameserver', 'gs'))) {
		$serviceName, $server = $entry
		$service = $services.$serviceName
		$overlay = $service.volumes | Where-Object { $_.target -eq '/app/config-overlay' }
		Assert-Contract ($overlay.read_only -and [IO.Path]::GetFullPath($overlay.source) -eq $expectedOverlay) "$serviceName overlay mount is wrong"
		$logMount = $service.volumes | Where-Object { $_.target -like '/app/*-server/log' }
		Assert-Contract ([IO.Path]::GetFullPath($logMount.source) -eq [IO.Path]::GetFullPath((Join-Path $contractRun "logs/$server"))) "$serviceName log mount is not per-run"
		Assert-Contract ($service.environment.AION_LOG_JSONL_DIR -eq $logMount.target) "$serviceName JSONL directory is outside its run log mount"
	}

	$deterministicRates = Get-Content -Raw (Join-Path $repoRoot 'docker/bots/overlay/20-deterministic-rates.properties')
	Assert-Contract ($deterministicRates -match 'gameserver\.craft\.fail\.chance\s*=\s*0') 'deterministic craft failure chance is not zero'
	Assert-Contract ($deterministicRates -match 'gameserver\.gather\.fail\.chance\s*=\s*0') 'deterministic gather failure chance is not zero'
	$deterministicProfile = (Get-ChildItem (Join-Path $repoRoot 'docker/bots/overlay') -Filter '*.properties' | Get-Content -Raw) -join "`n"
	Assert-Contract ($deterministicProfile -match 'loginserver\.accounts\.autocreate\s*=\s*true') 'deterministic profile does not enable account auto-creation'
	$soakProfile = (Get-ChildItem (Join-Path $repoRoot 'docker/bots/overlay-soak') -Filter '*.properties' | Get-Content -Raw) -join "`n"
	Assert-Contract ($soakProfile -notmatch 'gameserver\.(craft|gather)\.fail\.chance') 'soak profile overrides production failure rates'
	Assert-Contract ($soakProfile -match 'loginserver\.accounts\.autocreate\s*=\s*true') 'soak profile does not enable account auto-creation'

	$seedSql = Get-Content -Raw (Join-Path $repoRoot 'docker/bots/seed/10-bot-seed.sql')
	Assert-Contract ($seedSql -match "'director'\s*,\s*'Zd2bHPtGKgR\+5Xk\+9H2ugwisL08='\s*,\s*TRUE\s*,\s*9") 'director credentials or access level changed'

	Write-Host 'Bot compose contract passed: isolated names/images, tmpfs DB, seed, ports, overlays and per-run logs.'
}
finally {
	if ($null -eq $previousRunDirectory) { Remove-Item Env:AION_E2E_RUN_DIR -ErrorAction SilentlyContinue }
	else { $env:AION_E2E_RUN_DIR = $previousRunDirectory }
	if ($null -eq $previousOverlayDirectory) { Remove-Item Env:AION_BOT_OVERLAY_DIR -ErrorAction SilentlyContinue }
	else { $env:AION_BOT_OVERLAY_DIR = $previousOverlayDirectory }
	if (Test-Path -LiteralPath $contractRun) {
		$resolvedContractRun = [IO.Path]::GetFullPath($contractRun)
		$resolvedTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
		if (-not $resolvedContractRun.StartsWith($resolvedTemp, [StringComparison]::OrdinalIgnoreCase)) {
			throw "Refusing to remove unexpected contract path: $resolvedContractRun"
		}
		Remove-Item -LiteralPath $resolvedContractRun -Recurse -Force
	}
}
