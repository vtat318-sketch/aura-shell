@echo off
rem Aura: double-click = build out\Aura.exe and start it.
rem Everything happens in build.ps1 next to this file. Log: build-log.txt
title Aura build
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" -Run
echo.
pause
