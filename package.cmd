@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0package.ps1" -GameDir "%~1"
if errorlevel 1 (
  echo [ExcuseMeDrone] PACKAGE FAILED
  pause
  exit /b 1
)
echo Package: ExcuseMeDrone-1.0.7.zip. Publication is a separate step.
pause

