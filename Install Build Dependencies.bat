@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Install-Windows-Dependencies.ps1"
if errorlevel 1 echo Setup failed. See the messages above.
pause
