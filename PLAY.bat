@echo off
REM Double-click this to play the game from source. It is one line: PowerShell refuses to run
REM .ps1 files on a double-click by default, and -ExecutionPolicy Bypass is how that is answered
REM for this one command without changing anything on the machine.
REM See tools\play.ps1 for what it actually does, and docs\README.md for the rest.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\play.ps1" %*
if errorlevel 1 pause
