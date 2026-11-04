@echo off
setlocal
cd /d "%~dp0"
if not exist "dotnet\dotnet.exe" (
    echo The bundled .NET runtime is missing. Keep this file inside the Kavita Fork folder.
    pause
    exit /b 1
)
if not exist "Kavita.Server.dll" (
    echo The Kavita server files are missing. Keep this file inside the Kavita Fork folder.
    pause
    exit /b 1
)
set "DOTNET_ROOT=%~dp0dotnet"
set "DOTNET_MULTILEVEL_LOOKUP=0"
echo Starting Kavita Fork. Keep this window open; press Ctrl+C to stop.
echo New installations use http://localhost:5051
echo Existing installations use the port in config\appsettings.json.
echo.
"%~dp0dotnet\dotnet.exe" "%~dp0Kavita.Server.dll"
echo.
echo Kavita has stopped. Check the messages above if it stopped unexpectedly.
pause
