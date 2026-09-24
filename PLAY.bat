@echo off
REM Double-click this to play. It is one line: PowerShell refuses a .ps1 on a double-click by
REM default, and -ExecutionPolicy Bypass answers that for this one command without changing
REM anything on the machine. See play.ps1 for what it does, and docs\README.md for the rest.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0play.ps1" %*
if errorlevel 1 pause
