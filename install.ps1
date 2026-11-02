# Installs Qalam for the current user and takes over Reverso's autostart entry.
# Idempotent: re-running updates the app and keeps the original Reverso backup.

[CmdletBinding()]
param(
    [switch]$KeepReversoAutostart,
    [switch]$KeepReversoRunning,
    [switch]$NoLaunch,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dest = Join-Path $env:LOCALAPPDATA 'Qalam'
$tashkeelDest = Join-Path $dest 'tashkeel'
$documentsDest = Join-Path $dest 'documents'
$mobileDest = Join-Path $dest 'mobile'
$concordanceDest = Join-Path $dest 'concordance'

# Refuse before copying anything if a fresh install would be left half-finished.
# An existing virtual environment is enough on updates; Python is not required then.
$venvPython = Join-Path $tashkeelDest '.venv\Scripts\python.exe'
$systemPython = $null
if (-not (Test-Path -LiteralPath $venvPython)) {
    $systemPython = (Get-Command python.exe -ErrorAction SilentlyContinue).Source
    if ($systemPython) {
        $pythonInfo = Get-Item -LiteralPath $systemPython -ErrorAction SilentlyContinue
        if (-not $pythonInfo -or $pythonInfo.Length -eq 0) { $systemPython = $null }
    }
    if ($systemPython) {
        & $systemPython --version 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) { $systemPython = $null }
    }
    if (-not $systemPython) {
        throw 'Python 3 is required for offline tashkeel. Install Python and re-run this script.'
    }
}

function New-Shortcut([string]$Path, [string]$Target, [string]$WorkDir, [string]$Arguments = '') {
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($Path)
    $shortcut.TargetPath = $Target
    $shortcut.WorkingDirectory = $WorkDir
    $shortcut.Arguments = $Arguments
    $shortcut.IconLocation = "$Target,0"
    $shortcut.Description = 'Qalam - Highlight, then Ctrl + C + C to translate with Arabic Tashkeel'
    $shortcut.Save()
}

if (-not $SkipBuild) {
    & (Join-Path $root 'build.ps1')
    if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }
}

$exe = Join-Path $root 'dist\Qalam.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw "Build output missing: $exe" }

New-Item -ItemType Directory -Force -Path $tashkeelDest | Out-Null
New-Item -ItemType Directory -Force -Path $documentsDest | Out-Null
New-Item -ItemType Directory -Force -Path $mobileDest | Out-Null
New-Item -ItemType Directory -Force -Path $concordanceDest | Out-Null

# Updating a running executable is not possible on Windows. Stop Qalam
# cleanly, then the fresh build below becomes the only process after launch.
Get-Process -Name Qalam -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400

Copy-Item -LiteralPath $exe -Destination $dest -Force
Copy-Item -LiteralPath (Join-Path $root 'tashkeel\tashkeel_server.py') -Destination $tashkeelDest -Force
Copy-Item -LiteralPath (Join-Path $root 'tashkeel\requirements.txt') -Destination $tashkeelDest -Force
Copy-Item -LiteralPath (Join-Path $root 'documents\pdf_translate.py') -Destination $documentsDest -Force
Copy-Item -LiteralPath (Join-Path $root 'documents\requirements.txt') -Destination $documentsDest -Force
Copy-Item -Path (Join-Path $root 'mobile\*') -Destination $mobileDest -Force
# Keep the installer beside the installed app so the browser's Windows
# download endpoint can build a fresh package without the source checkout.
Copy-Item -LiteralPath (Join-Path $root 'install.ps1') -Destination $dest -Force
Copy-Item -LiteralPath (Join-Path $root 'uninstall.ps1') -Destination $dest -Force
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination $dest -Force

# Bundled character dictionary (Unihan). The stroke outlines are cached per
# character under %LOCALAPPDATA%\Qalam\strokes on first view.
$dataSource = Join-Path $root 'data'
if (Test-Path -LiteralPath $dataSource) {
    $dataDest = Join-Path $dest 'data'
    New-Item -ItemType Directory -Force -Path $dataDest | Out-Null
    Get-ChildItem -LiteralPath $dataSource -Filter '*.tsv' | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $dataDest $_.Name) -Force
    }
}

# Seed packs ship with the app. An existing pack with the same name is a
# user-built corpus and is deliberately left alone.
$concordanceSource = Join-Path $root 'concordance'
if (Test-Path -LiteralPath $concordanceSource) {
    Get-ChildItem -LiteralPath $concordanceSource -File |
        Where-Object { $_.Name -match '\.(tsv|qidx)$' } | ForEach-Object {
        $target = Join-Path $concordanceDest $_.Name
        if (-not (Test-Path -LiteralPath $target)) {
            Copy-Item -LiteralPath $_.FullName -Destination $target
        }
    }
}

if (-not (Test-Path -LiteralPath $venvPython)) {
    Write-Host "Creating tashkeel virtual environment in $tashkeelDest\.venv ..."
    & $systemPython -m venv (Join-Path $tashkeelDest '.venv')
    if ($LASTEXITCODE -ne 0) { throw "python -m venv failed with exit code $LASTEXITCODE" }
}

$mishkalOk = Test-Path -LiteralPath (Join-Path $tashkeelDest '.venv\Lib\site-packages\mishkal')
if (-not $mishkalOk) {
    Write-Host 'Installing Mishkal into the tashkeel environment ...'
    & $venvPython -m pip install --quiet --disable-pip-version-check -r (Join-Path $tashkeelDest 'requirements.txt')
    if ($LASTEXITCODE -ne 0) { throw "pip install failed with exit code $LASTEXITCODE" }
}

$pypinyinOk = Test-Path -LiteralPath (Join-Path $tashkeelDest '.venv\Lib\site-packages\pypinyin')
if (-not $pypinyinOk) {
    Write-Host 'Installing pypinyin for Chinese transcription ...'
    & $venvPython -m pip install --quiet --disable-pip-version-check -r (Join-Path $tashkeelDest 'requirements.txt')
    if ($LASTEXITCODE -ne 0) { throw "pip install failed with exit code $LASTEXITCODE" }
}

$pymupdfOk = $false
try {
    & $venvPython -c "import pymupdf" 2>$null
    $pymupdfOk = ($LASTEXITCODE -eq 0)
} catch { }
if (-not $pymupdfOk) {
    Write-Host 'Installing PyMuPDF for PDF document translation ...'
    & $venvPython -m pip install --quiet --disable-pip-version-check -r (Join-Path $documentsDest 'requirements.txt')
    if ($LASTEXITCODE -ne 0) { throw "pip install failed with exit code $LASTEXITCODE" }
}

$startupShortcut = Join-Path ([Environment]::GetFolderPath('Startup')) 'Qalam.lnk'
$programsShortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'Qalam.lnk'
New-Shortcut -Path $startupShortcut -Target (Join-Path $dest 'Qalam.exe') -WorkDir $dest -Arguments '--background'
New-Shortcut -Path $programsShortcut -Target (Join-Path $dest 'Qalam.exe') -WorkDir $dest
Write-Host "Autostart shortcut: $startupShortcut"

$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$backupPath = Join-Path $dest 'reverso-autostart-backup.json'
$reversoValue = $null
try {
    $reversoValue = (Get-ItemProperty -Path $runKey -Name 'Reverso' -ErrorAction Stop).Reverso
} catch { }

if ($reversoValue -and -not (Test-Path -LiteralPath $backupPath)) {
    @{ name = 'Reverso'; value = $reversoValue } | ConvertTo-Json | Set-Content -LiteralPath $backupPath -Encoding UTF8
    Write-Host "Backed up Reverso autostart entry to $backupPath"
}
if ($reversoValue -and -not $KeepReversoAutostart) {
    Remove-ItemProperty -Path $runKey -Name 'Reverso'
    Write-Host 'Disabled Reverso autostart (backup kept).'
}

if (-not $KeepReversoRunning) {
    $running = Get-Process -Name Reverso -ErrorAction SilentlyContinue
    if ($running) {
        foreach ($process in $running) { [void]$process.CloseMainWindow() }
        Start-Sleep -Seconds 2
        $still = Get-Process -Name Reverso -ErrorAction SilentlyContinue
        if ($still) {
            $still | Stop-Process -Force
            Write-Host 'Stopped the running Reverso process so both popups do not compete.'
        } else {
            Write-Host 'Closed the running Reverso process.'
        }
    }
}

if (-not $NoLaunch) {
    # Normal launch opens the main window, matching the pinned taskbar icon.
    # The Startup shortcut passes --background and stays in the tray.
    Start-Process -FilePath (Join-Path $dest 'Qalam.exe') -WorkingDirectory $dest
}

Write-Host ''
Write-Host 'Qalam installed.'
Write-Host "  App:      $dest\Qalam.exe"
Write-Host "  Config:   $dest\config.json"
Write-Host "  Log:      $dest\logs\qalam.log"
Write-Host '  Trigger:  Highlight, then Ctrl + C + C anywhere for the Compact Pop-up.'
Write-Host '  Undo:     uninstall.ps1 (restores Reverso autostart)'
