[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'code-coverage.ps1')
function Assert-True([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$root = Join-Path $temporaryRoot ('aion-code-coverage-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
try {
	$source = Join-Path $root 'src/Aion.GameServer/Services'
	$binary = Join-Path $root 'tests/Aion.Simulation.Tests/bin/Debug/net10.0'
	$run = Join-Path $root 'run-a'
	New-Item -ItemType Directory -Path $source, $binary, $run | Out-Null
	[IO.File]::WriteAllText((Join-Path $source 'Sample.cs'), 'namespace Sample;')
	foreach ($name in @('Aion.GameServer.dll', 'Aion.GameServer.pdb')) { [IO.File]::WriteAllText((Join-Path $binary $name), 'original compiled input') }
	Initialize-AionSimCoverage -RepoRoot $root -RunDirectory $run -Run contract -GitSha abc -Seed 73 -Tier Full -Scenarios @('L0', 'M1')
	$request = Get-Content -Raw (Join-Path $run 'code-coverage-request.json') | ConvertFrom-Json
	Assert-True ($request.run -eq 'contract' -and $request.seed -eq 73 -and $request.tier -eq 'Full' -and ($request.scenarios -join ',') -eq 'L0,M1') 'Coverage request lost scope.'
	Assert-True ($request.sourceFiles.'Services/Sample.cs' -eq (Get-FileHash (Join-Path $source 'Sample.cs')).Hash.ToLowerInvariant()) 'Source snapshot does not describe input.'
	Assert-True ($null -eq $request.baselineSha256) 'Absent baseline was fabricated.'
	Complete-AionSimCoverage -RepoRoot $root -RunDirectory $run
	$result = Get-Content -Raw (Join-Path $run 'code-coverage-result.json') | ConvertFrom-Json
	Assert-True ($result.errors.Count -eq 2 -and $result.binariesRestored) 'Missing collector attachments must fail but not claim binary damage.'
	$attachment = Join-Path $run 'attachment'
	New-Item -ItemType Directory -Path $attachment | Out-Null
	[IO.File]::WriteAllText((Join-Path $attachment 'coverage.json'), '{"Aion.GameServer.dll":{}}')
	[IO.File]::WriteAllText((Join-Path $attachment 'coverage.cobertura.xml'), '<coverage/>')
	Complete-AionSimCoverage -RepoRoot $root -RunDirectory $run
	$result = Get-Content -Raw (Join-Path $run 'code-coverage-result.json') | ConvertFrom-Json
	Assert-True ($result.errors.Count -eq 0 -and $result.artifacts.'coverage.json'.path -eq 'attachment/coverage.json') 'Valid attachment discovery failed.'
	Assert-True ($result.requestSha256 -eq (Get-FileHash (Join-Path $run 'code-coverage-request.json')).Hash.ToLowerInvariant()) 'Request provenance was lost.'
	[IO.File]::WriteAllText((Join-Path $binary 'Aion.GameServer.dll'), 'instrumented residue')
	Complete-AionSimCoverage -RepoRoot $root -RunDirectory $run
	$result = Get-Content -Raw (Join-Path $run 'code-coverage-result.json') | ConvertFrom-Json
	Assert-True (-not $result.binariesRestored -and $result.errors[0] -like '*did not restore*') 'Unrestored instrumentation was accepted.'
	$duplicate = Join-Path $run 'duplicate'
	New-Item -ItemType Directory -Path $duplicate | Out-Null
	Copy-Item (Join-Path $attachment 'coverage.json') $duplicate
	Complete-AionSimCoverage -RepoRoot $root -RunDirectory $run
	$result = Get-Content -Raw (Join-Path $run 'code-coverage-result.json') | ConvertFrom-Json
	Assert-True ($result.artifacts.'coverage.json'.copies.Count -eq 2) 'Byte-identical TRX/collector copies were not retained.'
	[IO.File]::WriteAllText((Join-Path $duplicate 'coverage.json'), 'conflicting copy')
	Complete-AionSimCoverage -RepoRoot $root -RunDirectory $run
	$result = Get-Content -Raw (Join-Path $run 'code-coverage-result.json') | ConvertFrom-Json
	Assert-True (@($result.errors | Where-Object { $_ -like 'Conflicting*' }).Count -eq 1) 'Conflicting attachments were silently chosen.'

	# Evaluate the actual runner's coverage-selection expression without starting a test host or database.
	$tokens = $null; $parseErrors = $null
	$ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'run-sim-tier.ps1'), [ref]$tokens, [ref]$parseErrors)
	Assert-True ($parseErrors.Count -eq 0) 'SIM runner must parse.'
	$assignment = $ast.Find({ param($node) $node -is [Management.Automation.Language.AssignmentStatementAst] -and $node.Left.Extent.Text -eq '$collectCoverage' }, $true)
	$selection = [scriptblock]::Create($assignment.Extent.Text + "`n" + '$collectCoverage')
	foreach ($choice in @(@('Fast', $false, $false), @('Fast', $true, $true), @('Full', $false, $true), @('Full', $true, $true))) {
		$Tier = $choice[0]; $CodeCoverage = $choice[1]
		Assert-True ((& $selection) -eq $choice[2]) 'Full must collect by default; Fast must remain opt-in.'
	}
	Write-Host 'SIM coverage contract passed: scope, frozen inputs, attachment loss/duplication, restoration and Full/Fast selection. No bots or DB used.'
}
finally {
	$resolved = [IO.Path]::GetFullPath($root)
	if (-not $resolved.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolved) -notmatch '^aion-code-coverage-[a-f0-9]{32}$') { throw 'Refusing unsafe test cleanup.' }
	if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
