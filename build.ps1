<#
  Aura build script. Easiest way: double-click build.bat next to this file.

  1. finds the .NET 8 SDK (or newer); if there is none, downloads it into .dotnet\ next to the project
     (no admin rights, nothing is installed system-wide, delete the folder any time);
  2. closes a running Aura from out\ (and brings the stock taskbar back);
  3. builds ONE self-contained out\Aura.exe (any Windows 10/11 x64, no .NET needed to run it);
  4. full log -> build-log.txt, errors only -> build-errors.txt (opened in Notepad);
  5. with -Run starts Aura.
#>
param(
    [switch]$Run,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Continue"
$ProgressPreference = "SilentlyContinue"   # Invoke-WebRequest is much faster without the progress bar
Set-Location -LiteralPath $PSScriptRoot

$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_UI_LANGUAGE = "en"          # English errors: easier to search for and to send

$log         = Join-Path $PSScriptRoot "build-log.txt"
$errFile     = Join-Path $PSScriptRoot "build-errors.txt"
$out         = Join-Path $PSScriptRoot "out"
$exe         = Join-Path $out "Aura.exe"
$localDotnet = Join-Path $PSScriptRoot ".dotnet"
$localExe    = Join-Path $localDotnet "dotnet.exe"

function Say([string]$text, [string]$color = "Gray") { Write-Host $text -ForegroundColor $color }
function Step([string]$text) { Write-Host ""; Write-Host "==> $text" -ForegroundColor Cyan }
function Fail([string]$text) {
    Write-Host ""
    Write-Host "  $text" -ForegroundColor Red
    Write-Host "  Полный лог: $log" -ForegroundColor Yellow
    Write-Host "  Пришли мне build-errors.txt или build-log.txt — разберёмся." -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "  AURA — сборка" -ForegroundColor Magenta
Write-Host "  Ничего нажимать не нужно, всё делается само." -ForegroundColor DarkGray

if (-not [Environment]::Is64BitOperatingSystem) { Fail "Нужна 64-битная Windows 10/11." }

# ---------------------------------------------------------------- 1. files from a downloaded zip are "blocked"
Step "Подготовка файлов"
Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File -ErrorAction SilentlyContinue |
    Unblock-File -ErrorAction SilentlyContinue

# ---------------------------------------------------------------- 2. .NET SDK: find or download
function Get-SdkMajor([string]$dotnetExe) {
    try {
        $majors = @(& $dotnetExe --list-sdks 2>$null | ForEach-Object { if ("$_" -match '^(\d+)\.') { [int]$Matches[1] } })
        if ($majors.Count -eq 0) { return 0 }
        return ($majors | Measure-Object -Maximum).Maximum
    } catch { return 0 }
}

Step "Ищу .NET SDK"
$dotnet = $null
if ((Test-Path -LiteralPath $localExe) -and ((Get-SdkMajor $localExe) -ge 8)) { $dotnet = $localExe }
if (-not $dotnet) {
    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($cmd -and ((Get-SdkMajor $cmd.Source) -ge 8)) { $dotnet = $cmd.Source }
}
if (-not $dotnet) {
    Say "  .NET SDK не найден — скачиваю (один раз, ~250 МБ, без прав администратора)..." "Yellow"
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
        $installer = Join-Path $env:TEMP "dotnet-install.ps1"
        Invoke-WebRequest -UseBasicParsing -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $installer
        & $installer -Channel 8.0 -InstallDir $localDotnet -NoPath
    } catch {
        Fail "Не получилось скачать .NET SDK: $($_.Exception.Message)`n  Проверь интернет или поставь вручную .NET 8 SDK x64: https://dotnet.microsoft.com/download/dotnet/8.0`n  и запусти build.bat ещё раз."
    }
    if (-not (Test-Path -LiteralPath $localExe)) { Fail "Установщик .NET отработал, но dotnet.exe не появился." }
    $dotnet = $localExe
}
if ($dotnet -eq $localExe) {
    $env:DOTNET_ROOT = $localDotnet
    $env:DOTNET_MULTILEVEL_LOOKUP = "0"
    $env:PATH = "$localDotnet;$env:PATH"
}
$sdkVersion = "$(& $dotnet --version 2>$null)"
Say "  SDK $sdkVersion  ($dotnet)" "DarkGray"

# ---------------------------------------------------------------- 3. close our running Aura (it locks out\Aura.exe)
$running = @(Get-Process -Name "Aura" -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and ($_.Path -eq $exe) })
if ($running.Count -gt 0) {
    Step "Закрываю запущенную Aura"
    $running | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 800
    # A killed Aura cannot show the stock taskbar again by itself.
    if (Test-Path -LiteralPath $exe) { Start-Process -FilePath $exe -ArgumentList "--show-taskbar" -Wait -ErrorAction SilentlyContinue }
}

# ---------------------------------------------------------------- 4. build
Step "Собираю Aura (первый раз 2–5 минут: докачиваются пакеты)"
if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Recurse -Force -ErrorAction SilentlyContinue }
Remove-Item -LiteralPath $errFile -ErrorAction SilentlyContinue

$publishArgs = @(
    "publish", "src/Aura.Shell/Aura.Shell.csproj",
    "-c", $Configuration, "-r", "win-x64",
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:EnableCompressionInSingleFile=true",
    "-o", $out, "-nologo"
)

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("Aura build $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), SDK $sdkVersion, $([Environment]::OSVersion.VersionString)")
& $dotnet @publishArgs 2>&1 | ForEach-Object {
    $line = "$_"
    $lines.Add($line)
    if ($line -match '\berror [A-Z]+\d+') { Write-Host $line -ForegroundColor Red }
    elseif ($line -match '\bwarning [A-Z]+\d+') { Write-Host $line -ForegroundColor DarkYellow }
    else { Write-Host $line -ForegroundColor DarkGray }
}
$code = $LASTEXITCODE
$lines | Set-Content -LiteralPath $log -Encoding UTF8

if ($code -ne 0 -or -not (Test-Path -LiteralPath $exe)) {
    $errors = @($lines | Where-Object { $_ -match '\berror [A-Z]+\d+' } | Select-Object -Unique)
    if ($errors.Count -gt 0) {
        $errors | Set-Content -LiteralPath $errFile -Encoding UTF8
        Start-Process notepad.exe -ArgumentList "`"$errFile`""
        Fail "Сборка не удалась: ошибок $($errors.Count). Список открыл в блокноте (build-errors.txt)."
    }
    Fail "Сборка не удалась (код $code)."
}

# ---------------------------------------------------------------- 5. emergency "put Windows back" script next to Aura.exe
$restoreBat = Join-Path $PSScriptRoot "restore-windows.bat"
if (Test-Path -LiteralPath $restoreBat) { Copy-Item -LiteralPath $restoreBat -Destination $out -Force }

$sizeMb = [math]::Round((Get-Item -LiteralPath $exe).Length / 1MB)
Step "Готово!"
Say "  $exe  ($sizeMb МБ)" "Green"
Say "  Этот Aura.exe работает на любой Windows 10/11 x64, ставить .NET не нужно." "DarkGray"
Say "  Если что-то пойдёт не так: out\restore-windows.bat вернёт Windows как было." "DarkGray"

# ---------------------------------------------------------------- 6. desktop shortcut (asked once)
$lnk = Join-Path ([Environment]::GetFolderPath("Desktop")) "Aura.lnk"
if (-not (Test-Path -LiteralPath $lnk)) {
    Write-Host ""
    $answer = Read-Host "  Создать ярлык Aura на рабочем столе? Enter = да, n = нет"
    if ("$answer".Trim() -notmatch '^(n|no|н|нет|т)$') {
        try {
            $shell = New-Object -ComObject WScript.Shell
            $shortcut = $shell.CreateShortcut($lnk)
            $shortcut.TargetPath = $exe
            $shortcut.WorkingDirectory = $out
            $shortcut.Description = "Aura — кастомизация Windows"
            $shortcut.Save()
            Say "  Ярлык создан." "Green"
        } catch { Say "  Ярлык создать не вышло: $($_.Exception.Message)" "Yellow" }
    }
}

# ---------------------------------------------------------------- 7. run
if ($Run) {
    Step "Запускаю Aura"
    Start-Process -FilePath $exe -WorkingDirectory $out
    Say "  При первом запуске сам откроется хаб. Потом — через кнопку «Пуск» на панели Aura." "DarkGray"
}
exit 0
