@echo off
chcp 65001 >nul
rem Aura emergency switch: puts Windows back exactly as it was before Aura (same as the hub button).
title Aura - vernut Windows
cd /d "%~dp0"
set "EXE=%~dp0Aura.exe"
if not exist "%EXE%" set "EXE=%~dp0out\Aura.exe"
if not exist "%EXE%" goto noexe

echo.
echo   Верну Windows как было до Aura:
echo     - стандартная панель задач Windows
echo     - прежние обои
echo     - без пункта Aura в меню проводника и без автозапуска
echo   Настройки Aura уйдут в резервную папку (их можно вернуть).
echo.
echo   Нажми любую клавишу, чтобы продолжить, или просто закрой окно.
pause >nul

powershell.exe -NoProfile -Command "Get-Process -Name Aura -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $env:EXE } | Stop-Process -Force"
timeout /t 1 /nobreak >nul
start "" /wait "%EXE%" --reset
echo.
echo   Готово. Если панель задач Windows не появилась, перезагрузи компьютер.
echo.
pause
exit /b 0

:noexe
echo.
echo   Не нашёл Aura.exe рядом с этим файлом и в папке out.
echo   Сначала собери Aura: build.bat
echo.
pause
exit /b 1
