@echo off
setlocal

where dotnet >nul 2>&1
if errorlevel 1 (
    echo .NET SDK bulunamadi.
    pause
    exit /b 1
)

set "ROOT=%~dp0"
set "OUT=%ROOT%PreInstallTool\bin\Release\net8.0-windows"
set "DIST=%ROOT%Dagitim\PreInstallTool"

echo Derleniyor...
dotnet publish "%ROOT%PreInstallTool\PreInstallTool.csproj" -c Release -o "%OUT%" --self-contained false
if errorlevel 1 (
    pause
    exit /b 1
)

echo.
echo Dagitim paketi hazirlaniyor...
if exist "%DIST%" rmdir /s /q "%DIST%"
mkdir "%DIST%"
xcopy "%OUT%\*" "%DIST%\" /E /I /Y >nul

echo.
echo Dagitim klasoru hazir:
echo %DIST%
echo.
echo Bu klasoru kullanicilariniza verin. Kurulum dosyalari icindedir.
pause
