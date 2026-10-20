[CmdletBinding()]
param(
    [string]$SourceRoot = "src/Aion.GameServer",
    [string]$AllowlistPath = "scripts/ci/clock-read-allowlist.json"
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$project = Join-Path $repoRoot 'tools/Aion.ClockReadGuard/Aion.ClockReadGuard.csproj'

# No automatic approval/update mode. Infrastructure exceptions require a reviewed
# file/member/API/statement/count and reason; gameplay reads must use SystemClock.
& dotnet run --project $project --no-launch-profile -- `
    --repo $repoRoot --source $SourceRoot --allowlist $AllowlistPath
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
