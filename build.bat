@echo off
rem Aura: double-click = build out\Aura.exe and start it.
rem Everything happens in build.ps1 next to this file. Log: build-log.txt
title Aura build
cd /d "%~dp0"
rem Windows PowerShell 5.1 reads a script without a UTF-8 BOM as ANSI and breaks the Cyrillic text.
rem Git hosting often drops the BOM, so we run a BOM copy (.build-run.ps1, git-ignored) next to build.ps1.
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$t=[IO.File]::ReadAllText('%~dp0build.ps1',[Text.Encoding]::UTF8); [IO.File]::WriteAllText('%~dp0.build-run.ps1',$t,(New-Object Text.UTF8Encoding $true))"
if errorlevel 1 (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" -Run
) else (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0.build-run.ps1" -Run
)
echo.
pause
