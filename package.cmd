@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0package.ps1" -GameDir "%~1"
if errorlevel 1 (
  echo [ExcuseMeDrone] PACKAGE FAILED
  pause
  exit /b 1
)
echo Candidate package: ExcuseMeDrone-1.0.4.zip. In-game validation required before publication.
pause
