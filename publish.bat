@echo off
setlocal EnableExtensions

where dotnet >nul 2>&1
if errorlevel 1 (
    echo .NET SDK bulunamadi.
    pause
    exit /b 1
)

set "ROOT=%~dp0"
set "DIST=%ROOT%Dagitim\UNKCLUB-Tool"
set "UPDATE_ZIP=%ROOT%Dagitim\PreInstallTool.zip"
set "FULL_ZIP=%ROOT%Dagitim\UNKCLUB-Tool.zip"

echo.
echo [1/5] Defender istisnalari ekleniyor...
powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%defender-exclude.ps1"
if errorlevel 1 (
    echo Uyari: Defender istisna betigi hata verdi; derlemeye devam ediliyor.
)

echo.
echo [2/5] Release derlemesi yayinlaniyor...
if exist "%DIST%" rmdir /s /q "%DIST%"
mkdir "%DIST%" 2>nul

dotnet publish "%ROOT%PreInstallTool\PreInstallTool.csproj" -c Release -r win-x64 --self-contained false -o "%DIST%"
if errorlevel 1 (
    echo Derleme basarisiz.
    pause
    exit /b 1
)

echo.
echo [3/5] Kullanici adi icin UNKCLUB Tool.exe olusturuluyor...
copy /Y "%DIST%\PreInstallTool.exe" "%DIST%\UNKCLUB Tool.exe" >nul

echo.
echo [4/5] Dosyalarin engeli kaldiriliyor...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -LiteralPath '%DIST%' -Recurse -File | Unblock-File -ErrorAction SilentlyContinue"

echo.
echo [5/5] Dagitim arsivleri hazirlaniyor...
if exist "%UPDATE_ZIP%" del /f /q "%UPDATE_ZIP%"
if exist "%FULL_ZIP%" del /f /q "%FULL_ZIP%"

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$dist = '%DIST%';" ^
  "$updateRoot = Join-Path $env:TEMP ('unkclub-update-' + [guid]::NewGuid().ToString());" ^
  "New-Item -ItemType Directory -Path $updateRoot | Out-Null;" ^
  "robocopy $dist $updateRoot /E /XD Installers /XF install-config.json /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null;" ^
  "Compress-Archive -Path (Join-Path $updateRoot '*') -DestinationPath '%UPDATE_ZIP%' -Force;" ^
  "Remove-Item $updateRoot -Recurse -Force -ErrorAction SilentlyContinue;" ^
  "Compress-Archive -Path (Join-Path $dist '*') -DestinationPath '%FULL_ZIP%' -Force"

echo.
echo Dagitim hazir:
echo   Klasor : %DIST%
echo   Exe    : %DIST%\UNKCLUB Tool.exe
echo   Exe    : %DIST%\PreInstallTool.exe
echo   Guncelleme zip : %UPDATE_ZIP%
echo   Tam paket zip  : %FULL_ZIP%
echo.
echo Installers, install-config.json ve UNKCLUB.exe dagitim klasorundedir.
pause
