[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'run-live.ps1'), [ref]$tokens, [ref]$errors)
if ($errors.Count -ne 0) { throw 'LIVE runner did not parse.' }
$functions = @($ast.FindAll({ param($node)
	$node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Stop-Watcher'
}, $true))
if ($functions.Count -ne 1) { throw 'Expected one watcher shutdown helper.' }
# Exercise the actual helper with a process double. No processes or Docker stacks are started or killed.
. ([scriptblock]::Create($functions[0].Extent.Text))
$stopTestRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) ('aion-watcher-stop-test-' + [Guid]::NewGuid().ToString('N'))))
New-Item -ItemType Directory -Path $stopTestRoot | Out-Null
try {
	foreach ($case in @(
		@{ name = 'normal'; exited = $false; stops = $true; code = 0; throws = $false },
		@{ name = 'early-clean'; exited = $true; stops = $true; code = 0; throws = $true },
		@{ name = 'early-error'; exited = $true; stops = $true; code = 7; throws = $true },
		@{ name = 'forced-clean'; exited = $false; stops = $false; code = 0; throws = $true },
		@{ name = 'normal-error'; exited = $false; stops = $true; code = 7; throws = $false }
	)) {
		$script:stopFile = Join-Path $stopTestRoot ($case.name + '.stop')
		$script:watcherExitCode = $null
		$process = [pscustomobject]@{ HasExited = $case.exited; ExitCode = $case.code;
			Stops = $case.stops; Waits = 0; Kills = 0; Disposed = $false }
		$process | Add-Member ScriptMethod WaitForExit {
			param([int]$Milliseconds = -1)
			if (-not (Test-Path -LiteralPath $script:stopFile)) { throw 'Wait preceded owning stop request.' }
			$this.Waits++
			if ($Milliseconds -eq -1) { return }
			if ($Milliseconds -ne 30000) { throw 'Unexpected watcher shutdown deadline.' }
			if ($this.Stops) { $this.HasExited = $true }
			return $this.Stops
		}
		$process | Add-Member ScriptMethod Kill {
			param([bool]$EntireTree)
			if (-not $EntireTree) { throw 'Watcher tree was not terminated.' }
			$this.Kills++; $this.HasExited = $true
		}
		$process | Add-Member ScriptMethod Dispose { $this.Disposed = $true }
		$script:watcherProcess = $process
		$failure = $null
		try { Stop-Watcher } catch { $failure = $_ }
		if (($null -ne $failure) -ne $case.throws) { throw "Wrong watcher shutdown result for $($case.name): $failure" }
		if ($case.exited -and $failure.ToString() -notlike '*exited before*') { throw 'Early exit lost its cause.' }
		if (-not $case.stops -and $failure.ToString() -notlike '*did not stop*') { throw 'Forced stop lost its cause.' }
		if (-not $process.Disposed -or $null -ne $script:watcherProcess -or $script:watcherExitCode -ne $case.code) {
			throw 'Watcher handle cleanup or exit-code propagation failed.'
		}
		if ((Test-Path -LiteralPath $script:stopFile) -eq $case.exited -or
			$process.Kills -ne [int](-not $case.stops) -or
			$process.Waits -ne $(if ($case.exited) { 0 } elseif ($case.stops) { 1 } else { 2 })) {
			throw 'Watcher stop request, wait, or kill lifecycle was wrong.'
		}
		Stop-Watcher # The owning runner calls this again in finally; cleanup must be idempotent.
	}
	Write-Host 'Watcher shutdown contract passed: normal/nonzero exits, early clean exit, forced stop, and idempotent cleanup.'
}
finally {
	$parent = [IO.Path]::GetFullPath((Split-Path $stopTestRoot -Parent)).TrimEnd([IO.Path]::DirectorySeparatorChar)
	if ($parent -ne [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) -or
		(Split-Path $stopTestRoot -Leaf) -notmatch '^aion-watcher-stop-test-[a-f0-9]{32}$') { throw 'Unsafe watcher test cleanup path.' }
	Remove-Item -LiteralPath $stopTestRoot -Recurse -Force
}
