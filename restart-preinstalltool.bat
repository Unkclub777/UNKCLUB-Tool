@echo off
set ROOT=C:\Users\nomer\OneDrive\Desktop\EMULAT~1
set DST=%ROOT%\PreInstallTool\bin\Release\net8.0-windows
set REB=%ROOT%\PreInstallTool\bin\Release-rebuild\net8.0-windows
set EXE=%DST%\PreInstallTool.exe

taskkill /F /IM PreInstallTool.exe >nul 2>&1
timeout /t 2 /nobreak >nul

copy /Y "%REB%\PreInstallTool.exe" "%EXE%" >nul 2>&1
if errorlevel 1 (
  start "" "%REB%\PreInstallTool.exe"
) else (
  start "" "%EXE%"
)
