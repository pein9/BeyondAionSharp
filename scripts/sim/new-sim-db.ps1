param(
	[ValidateSet('Create', 'Open', 'Drop')]
	[string]$Action = 'Create',
	[string]$RunId = $env:AION_SIM_RUN_ID,
	[string]$Shard = '0',
	[string]$DatabaseName,
	[string]$ContainerName = 'aion-mysql',
	[string]$RootPassword = 'aion',
	[int]$HostPort = 3306
)

$ErrorActionPreference = 'Stop'

function Invoke-Docker {
	param([string[]]$Arguments)
	& docker @Arguments
	if ($LASTEXITCODE -ne 0) {
		throw "docker $($Arguments -join ' ') exited with $LASTEXITCODE"
	}
}

function Normalize-NamePart([string]$Value, [string]$Fallback) {
	if ([string]::IsNullOrWhiteSpace($Value)) { $Value = $Fallback }
	$normalized = ($Value.ToLowerInvariant() -replace '[^a-z0-9_]', '_').Trim('_')
	if ([string]::IsNullOrWhiteSpace($normalized)) { return $Fallback }
	return $normalized
}

if ([string]::IsNullOrWhiteSpace($DatabaseName)) {
	$runPart = Normalize-NamePart $RunId (Get-Date -Format 'yyyyMMddHHmmss')
	$shardPart = Normalize-NamePart $Shard '0'
	$DatabaseName = "aion_gs_sim_${runPart}_${shardPart}"
}
if ($DatabaseName -notmatch '^aion_gs_sim_[a-z0-9_]+$') {
	throw ('Simulation database name must match ^aion_gs_sim_[a-z0-9_]+$: {0}' -f $DatabaseName)
}
if ($DatabaseName.Length -gt 64) {
	throw "Simulation database name exceeds MySQL's 64-character limit: $DatabaseName"
}

Invoke-Docker -Arguments @('info') *> $null
$existing = ((& docker ps -a --filter "name=^/$ContainerName$" --format '{{.Names}}' | Out-String).Trim())
if ($Action -eq 'Drop') {
	if ($existing -eq $ContainerName) {
		$running = ((& docker ps --filter "name=^/$ContainerName$" --format '{{.Names}}' | Out-String).Trim())
		if ($running -ne $ContainerName) { Invoke-Docker -Arguments @('start', $ContainerName) *> $null }
		Invoke-Docker -Arguments @('exec', '-e', "MYSQL_PWD=$RootPassword", $ContainerName, 'mysql', '-uroot', '-e', "DROP DATABASE IF EXISTS ``$DatabaseName``;") *> $null
	}
	return
}

if ($existing -eq $ContainerName) {
	$running = ((& docker ps --filter "name=^/$ContainerName$" --format '{{.Names}}' | Out-String).Trim())
	if ($running -ne $ContainerName) { Invoke-Docker -Arguments @('start', $ContainerName) *> $null }
} else {
	Invoke-Docker -Arguments @('run', '--name', $ContainerName, '-e', "MYSQL_ROOT_PASSWORD=$RootPassword", '-e', 'MYSQL_ROOT_HOST=%', '-p', "${HostPort}:3306", '-d', 'mysql:8.4') *> $null
}

$deadline = (Get-Date).AddSeconds(90)
do {
	& docker exec -e "MYSQL_PWD=$RootPassword" $ContainerName mysqladmin ping -h 127.0.0.1 -P 3306 --protocol=tcp -uroot --silent *> $null
	if ($LASTEXITCODE -eq 0) { break }
	Start-Sleep -Seconds 1
} while ((Get-Date) -lt $deadline)
if ($LASTEXITCODE -ne 0) {
	throw "Timed out waiting for Docker MySQL container $ContainerName."
}

# Open never resets data. The NI-08 orchestrator owns creation and final cleanup.
if ($Action -eq 'Open') {
	$count = (& docker exec -e "MYSQL_PWD=$RootPassword" $ContainerName mysql -uroot -N -e "SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name='$DatabaseName';" | Out-String).Trim()
	if ($LASTEXITCODE -ne 0 -or $count -ne '1') { throw "Retained simulation database does not exist: $DatabaseName" }
	[ordered]@{ database=$DatabaseName; host='127.0.0.1'; port=$HostPort; user='root'; password=$RootPassword; container=$ContainerName } | ConvertTo-Json -Compress
	return
}

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$schemaPath = Join-Path $repoRoot 'game-server\sql\aion_gs.sql'
if (-not (Test-Path -LiteralPath $schemaPath -PathType Leaf)) {
	throw "Game-server schema not found: $schemaPath"
}

Invoke-Docker -Arguments @('exec', '-e', "MYSQL_PWD=$RootPassword", $ContainerName, 'mysql', '-uroot', '-e', "DROP DATABASE IF EXISTS ``$DatabaseName``; CREATE DATABASE ``$DatabaseName`` CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;") *> $null
$containerSchemaPath = "/tmp/$DatabaseName.sql"
Invoke-Docker -Arguments @('cp', $schemaPath, "${ContainerName}:$containerSchemaPath") *> $null

# No --force: the first schema error stops the import and fails this script.
Invoke-Docker -Arguments @('exec', '-e', "MYSQL_PWD=$RootPassword", $ContainerName, 'sh', '-c', "mysql --protocol=socket -uroot '$DatabaseName' < '$containerSchemaPath'") *> $null

[ordered]@{
	database = $DatabaseName
	host = '127.0.0.1'
	port = $HostPort
	user = 'root'
	password = $RootPassword
	container = $ContainerName
} | ConvertTo-Json -Compress
