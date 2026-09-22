@echo off
setlocal
cd /d "%~dp0"

if "%~1"=="" (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
) else (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" -GameDir "%~1"
)

if errorlevel 1 (
  echo.
  echo [ExcuseMeDrone] BUILD FAILED
  pause
  exit /b 1
)

echo.
echo [ExcuseMeDrone] BUILD OK
pause
