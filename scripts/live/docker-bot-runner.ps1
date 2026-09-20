# Optional load-generator transport environment. Never changes server settings or workload options.
function Get-LiveDockerEndpoints {
	param([string[]]$ComposeArguments, [string]$ProjectName, [string]$ExpectedRevision)
	$endpoints = @{}
	foreach ($service in @('loginserver', 'chatserver', 'gameserver', 'botrunner')) {
		$ids = @(& docker @ComposeArguments ps --all --quiet $service)
		if ($LASTEXITCODE -ne 0 -or $ids.Count -ne 1 -or [string]::IsNullOrWhiteSpace($ids[0])) {
			throw "Expected exactly one Docker $service container."
		}
		$inspection = & docker inspect $ids[0]
		if ($LASTEXITCODE -ne 0) { throw "Could not inspect Docker $service container." }
		$containers = @($inspection | ConvertFrom-Json)
		if ($containers.Count -ne 1) { throw "Ambiguous Docker $service inspection." }
		$container = $containers[0]
		if (-not $container.State.Running -or
			$container.Config.Labels.'com.docker.compose.project' -cne $ProjectName -or
			$container.Config.Labels.'com.docker.compose.service' -cne $service) {
			throw "Docker $service container is stopped or belongs to another stack."
		}
		$networks = @($container.NetworkSettings.Networks.PSObject.Properties)
		if ($networks.Count -ne 1 -or $networks[0].Name -cne "${ProjectName}_default") {
			throw "Docker $service is not exclusively on this run's default network."
		}
		$address = [Net.IPAddress]::Parse($networks[0].Value.IPAddress)
		if ($address.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork -or $address.Equals([Net.IPAddress]::Any)) {
			throw "Docker $service has no usable internal IPv4 address."
		}
		if ($service -eq 'botrunner' -and $container.Config.Labels.'org.opencontainers.image.revision' -cne $ExpectedRevision) {
			throw 'Docker bot image revision does not match the captured tool build.'
		}
		$endpoints[$service] = [pscustomobject]@{ address = $address.ToString(); containerId = $container.Id; imageId = $container.Image }
	}
	return $endpoints
}

function Get-LiveDockerBotArguments {
	param([string[]]$HostArguments, [hashtable]$Endpoints)
	if (($HostArguments[0..4] -join '|') -cne 'run|--project|tools/Aion.LiveBots|--no-build|--') {
		throw 'Unrecognized host bot command prefix.'
	}
	$options = @($HostArguments[5..($HostArguments.Length - 1)])
	foreach ($replacement in @{
		'--host' = $Endpoints.gameserver.address
		'--output' = '/artifacts'
		'--time-zone' = 'UTC'
	}.GetEnumerator()) {
		$index = [Array]::IndexOf($options, $replacement.Key)
		if ($index -lt 0 -or $index + 1 -ge $options.Length) { throw "Missing bot option $($replacement.Key)." }
		$options[$index + 1] = $replacement.Value
	}
	return @('exec', '-T', 'botrunner', 'dotnet', '/app/Aion.LiveBots.dll') + $options + @(
		'--login-host', $Endpoints.loginserver.address, '--chat-host', $Endpoints.chatserver.address,
		'--login-port', '2106', '--game-port', '7777', '--chat-port', '10241', '--admin-port', '7780'
	)
}
