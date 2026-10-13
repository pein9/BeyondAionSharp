# Registers (or updates) the Windows scheduled task that runs RestartAll.ps1 every night.
#   powershell -ExecutionPolicy Bypass -File docker\Register-RestartAllTask.ps1
# It runs as the current user and only while that user is logged on, because Docker Desktop
# needs the user's session. The task calls PowerShell directly rather than RestartAll.bat: the
# .bat pauses on failure, which would leave the task "Running" and block the next night's run.
param(
    [string]$TaskName = 'Aion RestartAll',
    [string]$At = '3:00AM'
)
$ErrorActionPreference = 'Stop'

$script = Join-Path $PSScriptRoot 'RestartAll.ps1'
$action = New-ScheduledTaskAction -Execute 'powershell.exe' `
    -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$script`"" `
    -WorkingDirectory $PSScriptRoot
$trigger = New-ScheduledTaskTrigger -Daily -At $At
# StartWhenAvailable: a run missed while the PC was off or logged out starts at the next chance.
# The 2-hour limit only stops a hung run; a normal run with a rebuild takes a few minutes.
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -MultipleInstances IgnoreNew `
    -ExecutionTimeLimit (New-TimeSpan -Hours 2) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Limited

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal `
    -Description 'Nightly restart of the live Aion Docker stack; deploys new commits on main. See docker\README.md.' -Force | Out-Null

$task = Get-ScheduledTask -TaskName $TaskName
$info = $task | Get-ScheduledTaskInfo
Write-Host "Registered '$TaskName': daily at $At as $($task.Principal.UserId); next run $($info.NextRunTime)."
