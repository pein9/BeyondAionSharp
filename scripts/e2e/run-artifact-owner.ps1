# Local process provenance for artifact retention. Loading this file has no side effects.
Set-StrictMode -Version Latest

function Register-AionRunArtifactOwner {
    param([Parameter(Mandatory)][string]$Directory)
    $resolved = [IO.Path]::GetFullPath($Directory)
    $folder = Get-Item -LiteralPath $resolved -ErrorAction Stop
    if (-not $folder.PSIsContainer -or ($folder.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Run ownership requires a real directory: $resolved"
    }
    $process = Get-Process -Id $PID -ErrorAction Stop
    try {
        $record = [ordered]@{ schemaVersion = 1; machine = [Environment]::MachineName; processId = $PID;
            processStartUtcTicks = $process.StartTime.ToUniversalTime().Ticks }
    }
    finally { $process.Dispose() }
    # Never overwrite another owner's record. Partial records after a hard kill are retained conservatively.
    $stream = [IO.File]::Open((Join-Path $resolved 'run-owner.json'), [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
    $writer = [IO.StreamWriter]::new($stream, [Text.UTF8Encoding]::new($false))
    try { $writer.Write(($record | ConvertTo-Json -Compress)) }
    finally { $writer.Dispose() }
}

function Test-AionRunArtifactOwnerExited {
    param([Parameter(Mandatory)][string]$Directory)
    try {
        $folder = Get-Item -LiteralPath $Directory -ErrorAction Stop
        if (-not $folder.PSIsContainer -or ($folder.Attributes -band [IO.FileAttributes]::ReparsePoint)) { return $false }
        $record = Get-Content -LiteralPath (Join-Path $Directory 'run-owner.json') -Raw -ErrorAction Stop | ConvertFrom-Json -AsHashtable
        if (($record.schemaVersion -isnot [long] -and $record.schemaVersion -isnot [int]) -or $record.schemaVersion -ne 1 -or
            $record.machine -cne [Environment]::MachineName -or
            ($record.processId -isnot [long] -and $record.processId -isnot [int]) -or $record.processId -le 0 -or $record.processId -gt [int]::MaxValue -or
            ($record.processStartUtcTicks -isnot [long] -and $record.processStartUtcTicks -isnot [int]) -or $record.processStartUtcTicks -le 0) { return $false }
        try { $process = Get-Process -Id $record.processId -ErrorAction Stop }
        catch {
            # Permission/inspection failures are not proof that an owner exited.
            return $_.FullyQualifiedErrorId -like 'NoProcessFoundForGivenId*'
        }
        try { return $process.StartTime.ToUniversalTime().Ticks -ne $record.processStartUtcTicks }
        finally { $process.Dispose() }
    }
    catch { return $false } # Unknown, foreign-host, malformed and legacy ownership stays protected.
}
