@echo off
setlocal EnableExtensions

where dotnet >nul 2>&1
if errorlevel 1 (
    echo .NET SDK bulunamadi.
    pause
    exit /b 1
)

set "ROOT=%~dp0"
set "DIST=%ROOT%Dagitim"
set "STAGING=%DIST%\_publish"
set "EXE=%DIST%\UNKCLUB Tool.exe"
set "UPDATE_ZIP=%DIST%\UNKCLUB-Tool.zip"

echo.
echo [1/4] Defender istisnalari ekleniyor...
powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%defender-exclude.ps1"
if errorlevel 1 (
    echo Uyari: Defender istisna betigi hata verdi; derlemeye devam ediliyor.
)

echo.
echo [2/4] Tek dosya (single-file) Release yayinlaniyor...
echo Calisan UNKCLUB/PreInstallTool kapatiliyor...
taskkill /F /IM "UNKCLUB Tool.exe" /T >nul 2>&1
taskkill /F /IM PreInstallTool.exe /T >nul 2>&1
timeout /t 2 /nobreak >nul

if exist "%STAGING%" rmdir /s /q "%STAGING%"
mkdir "%STAGING%" 2>nul
mkdir "%DIST%" 2>nul

dotnet publish "%ROOT%PreInstallTool\PreInstallTool.csproj" -c Release -r win-x64 -o "%STAGING%"
if errorlevel 1 (
    echo Derleme basarisiz.
    pause
    exit /b 1
)

echo.
echo [3/4] UNKCLUB Tool.exe olusturuluyor...
if exist "%EXE%" del /f /q "%EXE%"
move /Y "%STAGING%\PreInstallTool.exe" "%EXE%" >nul
if exist "%STAGING%" rmdir /s /q "%STAGING%"

powershell -NoProfile -ExecutionPolicy Bypass -Command "Unblock-File -LiteralPath '%EXE%' -ErrorAction SilentlyContinue"

echo.
echo [4/4] Guncelleme zip hazirlaniyor...
if exist "%UPDATE_ZIP%" del /f /q "%UPDATE_ZIP%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%EXE%' -DestinationPath '%UPDATE_ZIP%' -Force"

echo.
echo Dagitim hazir:
echo   Exe : %EXE%
echo   Zip : %UPDATE_ZIP%
echo.
echo Tek EXE dagitimi: Installers ve config exe icine gomuludur.
echo Ilk calistirmada %%LocalAppData%%\UNKCLUB-Tool\ altina cikarilir.
pause
