@echo off
cd /d "%~dp0\.."
echo.
echo === WorkshopOS client build (script v5) ===
echo Working directory: %CD%
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-client.ps1" -Configuration Release -Version 1.2.4 -SkipInstaller
echo.
if errorlevel 1 (
  echo BUILD FAILED - no zip was created from a failed publish.
  echo Make sure you see "WorkshopOS client build script v5" above.
  pause
  exit /b 1
)
echo.
echo Done. Output: packaging\dist\WorkshopOS-Client-win-x64-v1.2.4.zip
explorer "%CD%\packaging\dist"
pause
