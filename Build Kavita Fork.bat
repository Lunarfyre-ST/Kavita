@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Build-Windows-Fork.ps1"
if errorlevel 1 echo Build failed. See the messages above.
pause
