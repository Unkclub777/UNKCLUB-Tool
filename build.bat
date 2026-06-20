@echo off
setlocal

where dotnet >nul 2>&1
if errorlevel 1 (
    echo .NET SDK bulunamadi.
    echo Lutfen su adresten .NET 8 SDK kurun: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

dotnet build "%~dp0PreInstallTool.sln" -c Release
if errorlevel 1 (
    pause
    exit /b 1
)

echo.
echo Derleme tamamlandi:
echo %~dp0PreInstallTool\bin\Release\net8.0-windows\PreInstallTool.exe
pause
