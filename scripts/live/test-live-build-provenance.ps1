[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'run-live.ps1'), [ref]$tokens, [ref]$errors)
if ($errors.Count -ne 0) { throw 'LIVE runner did not parse.' }
$captures = @($ast.FindAll({ param($node)
	$node -is [Management.Automation.Language.AssignmentStatementAst] -and $node.Left.Extent.Text -eq '$gitSha'
}, $true))
$builds = @($ast.FindAll({ param($node)
	$node -is [Management.Automation.Language.CommandAst] -and $node.GetCommandName() -eq 'Invoke-CheckedNative' -and
	$node.Extent.Text -match "'dotnet'.*'build'"
}, $true))
if ($captures.Count -ne 1 -or $builds.Count -ne 2 -or $captures[0].Extent.EndOffset -ge $builds[0].Extent.StartOffset) {
	throw 'LIVE source revision must be captured once, before either tool build, not after preflight.'
}
$reader = @($ast.FindAll({ param($node)
	$node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Get-LiveGitRevision'
}, $true))
$guards = @($ast.FindAll({ param($node)
	$node -is [Management.Automation.Language.IfStatementAst] -and $node.Clauses[0].Item1.Extent.Text -eq '(Get-LiveGitRevision) -cne $gitSha'
}, $true))
$startup = @($ast.FindAll({ param($node)
	$node -is [Management.Automation.Language.CommandAst] -and $node.GetCommandName() -eq 'Invoke-CheckedNative' -and
	$node.Extent.Text.Contains("'Bot stack startup'")
}, $true))
if ($reader.Count -ne 1 -or $guards.Count -ne 1 -or $startup.Count -ne 1 -or
	$guards[0].Extent.StartOffset -le $builds[1].Extent.EndOffset -or $guards[0].Extent.EndOffset -ge $startup[0].Extent.StartOffset) {
	throw 'Build revision guard must follow both builds and precede stack startup.'
}
. ([scriptblock]::Create($reader[0].Extent.Text))
$capture = [scriptblock]::Create($captures[0].Extent.Text)
$guard = [scriptblock]::Create($guards[0].Extent.Text)
$arguments = @($ast.FindAll({ param($node)
	$node -is [Management.Automation.Language.AssignmentStatementAst] -and $node.Left.Extent.Text -eq '$botArguments'
}, $true) | Where-Object { $_.Operator -eq [Management.Automation.Language.TokenKind]::Equals })
if ($arguments.Count -ne 1 -or $arguments[0].Extent.StartOffset -le $guards[0].Extent.EndOffset) { throw 'Bot argument construction moved before revision capture.' }
$makeArguments = [scriptblock]::Create($arguments[0].Extent.Text)
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$script:fixtureHead = 'a' * 40; $script:gitExit = 0
function git {
	param([Parameter(ValueFromRemainingArguments = $true)][object[]]$Arguments)
	if (($Arguments -join '|') -cne "-C|$repoRoot|rev-parse|HEAD") { throw 'Unexpected Git revision request.' }
	$script:LASTEXITCODE = $script:gitExit
	return $script:fixtureHead
}
. $capture
. $guard
$script:fixtureHead = 'b' * 40 # A commit during readiness must not relabel the already-built tools.
$Run = 'fixture'; $runPath = $repoRoot; $Bots = 50; $Scenario = @('SOAK')
$ConnectTimeoutSeconds = 10; $StepTimeoutSeconds = 1800; $Seed = 73
$configProfile = 'docker-bots-soak'; $timeZone = 'UTC'
. $makeArguments
$index = [Array]::IndexOf($botArguments, '--git-sha')
if ($index -lt 0 -or $botArguments[$index+1] -cne ('a' * 40)) { throw 'Post-build HEAD replaced the built revision.' }
$caught = $false
try { . $guard } catch { $caught = $_.ToString() -like '*Git HEAD changed during LIVE tool builds*' }
if (-not $caught) { throw 'A commit during tool builds was accepted.' }
foreach ($case in @(@{ head = 'not-a-sha'; code = 0 }, @{ head = ('a' * 40); code = 1 })) {
	$script:fixtureHead = $case.head; $script:gitExit = $case.code; $caught = $false
	try { Get-LiveGitRevision | Out-Null } catch { $caught = $_.ToString() -like '*Could not resolve*' }
	if (-not $caught) { throw 'Invalid or failed Git revision lookup was accepted.' }
}
Write-Host 'LIVE build provenance passed: capture/build/start ordering, build-time drift rejection, post-build commit preservation and invalid Git results.'
