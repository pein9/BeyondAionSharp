@rem Manual launcher for RestartAll.ps1; keeps the window open if the run reports a problem.
@rem The command stays on one line: cmd reads a .bat as it runs it, and the script may git-pull a new copy of this file.
@powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0RestartAll.ps1" %* || pause
