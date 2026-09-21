# Terminal runner evidence and report generation; dot-sourcing has no side effects.
function Write-AionRunReport {
	[CmdletBinding()]
	param(
		[Parameter(Mandatory)][string]$RunDirectory,
		[Parameter(Mandatory)][string]$Run,
		[Parameter(Mandatory)][ValidateSet('SIM', 'LIVE', 'FULL', 'LIVE_RETRY')][string]$Mode,
		[AllowEmptyCollection()][string[]]$Scenarios = @(),
		[Parameter(Mandatory)][ValidateSet('passed', 'failed')][string]$Status,
		[Parameter(Mandatory)][DateTimeOffset]$StartedUtc,
		[Parameter(Mandatory)][double]$DurationSeconds,
		[AllowNull()][string]$Failure,
		[string]$GitSha = '',
		[int]$Seed = 1
	)
	$root = [IO.Path]::GetFullPath($RunDirectory)
	if (-not (Test-Path -LiteralPath $root -PathType Container)) { throw 'Cannot report a run directory that was never created.' }
	[ordered]@{
		schemaVersion = 1; run = $Run; mode = $Mode; scenarios = @($Scenarios); status = $Status;
		startedUtc = $StartedUtc.ToString('O'); finishedUtc = [DateTimeOffset]::UtcNow.ToString('O');
		durationSeconds = $DurationSeconds; error = $Failure; gitSha = $GitSha; seed = $Seed
	} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $root 'runner-result.json') -Encoding utf8NoBOM
	& python (Join-Path $PSScriptRoot 'report-run.py') $root
	$reportCode = $LASTEXITCODE
	if ($reportCode -notin @(0, 1)) { throw "Run reporter failed with exit $reportCode." }
	if (-not (Test-Path -LiteralPath (Join-Path $root 'report.md')) -or -not (Test-Path -LiteralPath (Join-Path $root 'report.json'))) {
		throw 'Run reporter did not write both reports.'
	}
	$reportEvidence = Get-Content -Raw -LiteralPath (Join-Path $root 'report.json') | ConvertFrom-Json
	$sourceHash = (Get-FileHash -LiteralPath (Join-Path $root 'runner-result.json') -Algorithm SHA256).Hash.ToLowerInvariant()
	if ($reportEvidence.runnerSourceSha256 -cne $sourceHash) { throw 'Run report does not describe the current runner evidence.' }
	if ($reportCode -ne 0 -and $Status -eq 'passed') {
		throw "Run report rejected incomplete or failed evidence. See $root/report.md (reporter exit $reportCode)."
	}
}
