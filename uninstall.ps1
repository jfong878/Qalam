# Removes Qalam and restores the Reverso autostart entry it backed up.

[CmdletBinding()]
param(
    [switch]$KeepFiles
)

$ErrorActionPreference = 'Stop'
$dest = Join-Path $env:LOCALAPPDATA 'Qalam'

Get-Process -Name Qalam -ErrorAction SilentlyContinue | Stop-Process -Force

$startupShortcut = Join-Path ([Environment]::GetFolderPath('Startup')) 'Qalam.lnk'
$programsShortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'Qalam.lnk'
foreach ($shortcut in @($startupShortcut, $programsShortcut)) {
    if (Test-Path -LiteralPath $shortcut) {
        Remove-Item -LiteralPath $shortcut -Force
        Write-Host "Removed $shortcut"
    }
}

$backupPath = Join-Path $dest 'reverso-autostart-backup.json'
if (Test-Path -LiteralPath $backupPath) {
    $backup = Get-Content -LiteralPath $backupPath -Raw | ConvertFrom-Json
    $runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    $reversoExe = $null
    if ($backup.value -match '^\s*"([^"]+)"') { $reversoExe = $matches[1] }
    if (-not $reversoExe -or -not (Test-Path -LiteralPath $reversoExe)) {
        Write-Host 'Reverso is no longer installed; skipped restoring its autostart entry.'
    } else {
        $existing = $null
        try {
            $existing = (Get-ItemProperty -Path $runKey -Name $backup.name -ErrorAction Stop).($backup.name)
        } catch { }
        if (-not $existing) {
            Set-ItemProperty -Path $runKey -Name $backup.name -Value $backup.value
            Write-Host "Restored Reverso autostart: $($backup.value)"
        } else {
            Write-Host 'Reverso autostart already present; left it untouched.'
        }
    }
}

if ($KeepFiles) {
    Write-Host "Files kept at $dest"
} elseif (Test-Path -LiteralPath $dest) {
    Remove-Item -LiteralPath $dest -Recurse -Force
    Write-Host "Removed $dest"
}

Write-Host 'Qalam uninstalled. Reverso itself was not modified or removed.'
