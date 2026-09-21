# Coverage receipts are scoped to one test process; loading this file starts nothing.
function Initialize-AionSimCoverage {
	param([string]$RepoRoot, [string]$RunDirectory, [string]$Run, [string]$GitSha,
		[int]$Seed, [string]$Tier, [string[]]$Scenarios)
	$sourceRoot = Join-Path $RepoRoot 'src/Aion.GameServer'
	$files = [ordered]@{}
	foreach ($path in (Get-ChildItem -LiteralPath $sourceRoot -Recurse -File -Filter '*.cs' | Sort-Object FullName)) {
		$relative = [IO.Path]::GetRelativePath($sourceRoot, $path.FullName).Replace('\', '/')
		if ($relative -match '(^|/)(bin|obj)/') { continue }
		$files[$relative] = (Get-FileHash -LiteralPath $path.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
	}
	$binaryRoot = Join-Path $RepoRoot 'tests/Aion.Simulation.Tests/bin/Debug/net10.0'
	$binaries = [ordered]@{}
	foreach ($name in @('Aion.GameServer.dll', 'Aion.GameServer.pdb')) {
		$binaries[$name] = (Get-FileHash -LiteralPath (Join-Path $binaryRoot $name) -Algorithm SHA256).Hash.ToLowerInvariant()
	}
	$settings = Join-Path $RunDirectory 'coverage.runsettings'
	Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'coverage.runsettings') -Destination $settings
	$baseline = Join-Path $RepoRoot 'parity-artifacts/e2e/code-coverage-baseline.json'
	$baselineHash = $null
	if (Test-Path -LiteralPath $baseline) {
		$baselineCopy = Join-Path $RunDirectory 'code-coverage-baseline.json'
		Copy-Item -LiteralPath $baseline -Destination $baselineCopy
		$baselineHash = (Get-FileHash -LiteralPath $baselineCopy -Algorithm SHA256).Hash.ToLowerInvariant()
	}
	[ordered]@{
		schemaVersion = 1; run = $Run; mode = 'SIM'; gitSha = $GitSha; seed = $Seed; tier = $Tier; scenarios = @($Scenarios)
		collector = 'coverlet.collector/6.0.4'; testFilter = 'FullyQualifiedName~SimulationFastScenarioTests'
		sourceRoot = [IO.Path]::GetFullPath($sourceRoot); sourceFiles = $files
		binaries = $binaries; baselineSha256 = $baselineHash
		settingsSha256 = (Get-FileHash -LiteralPath $settings -Algorithm SHA256).Hash.ToLowerInvariant()
	} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $RunDirectory 'code-coverage-request.json') -Encoding utf8NoBOM
}

function Complete-AionSimCoverage {
	param([string]$RepoRoot, [string]$RunDirectory)
	$requestPath = Join-Path $RunDirectory 'code-coverage-request.json'
	$request = Get-Content -Raw -LiteralPath $requestPath | ConvertFrom-Json
	$errors = [Collections.Generic.List[string]]::new()
	$artifacts = [ordered]@{}
	foreach ($name in @('coverage.json', 'coverage.cobertura.xml')) {
		$paths = @(Get-ChildItem -LiteralPath $RunDirectory -Recurse -File -Filter $name | Sort-Object FullName)
		if ($paths.Count -eq 0) { $errors.Add("Missing $name attachment."); continue }
		# The TRX logger copies collector attachments into its deployment tree. Equivalent copies are
		# evidence, not a second measurement; retain every path and reject any conflicting contents.
		$hashes = @($paths | ForEach-Object { (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() } | Select-Object -Unique)
		if ($hashes.Count -ne 1) { $errors.Add("Conflicting $name attachments; found $($hashes.Count) distinct hashes."); continue }
		$artifacts[$name] = @{ path = [IO.Path]::GetRelativePath($RunDirectory, $paths[0].FullName).Replace('\', '/')
			sha256 = $hashes[0]; copies = @($paths | ForEach-Object { [IO.Path]::GetRelativePath($RunDirectory, $_.FullName).Replace('\', '/') }) }
	}
	foreach ($binary in $request.binaries.PSObject.Properties) {
		$path = Join-Path $RepoRoot "tests/Aion.Simulation.Tests/bin/Debug/net10.0/$($binary.Name)"
		if (-not (Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $binary.Value) {
			$errors.Add("Collector did not restore the original $($binary.Name).")
		}
	}
	[ordered]@{ schemaVersion = 1; run = $request.run; requestSha256 = (Get-FileHash -LiteralPath $requestPath -Algorithm SHA256).Hash.ToLowerInvariant()
		artifacts = $artifacts; errors = @($errors); binariesRestored = @($errors | Where-Object { $_ -like 'Collector did not restore*' }).Count -eq 0
	} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $RunDirectory 'code-coverage-result.json') -Encoding utf8NoBOM
}
