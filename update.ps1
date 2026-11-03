param(
    [Parameter(Mandatory = $true)][string]$Setup,
    [Parameter(Mandatory = $true)][string]$Sha256,
    [Parameter(Mandatory = $true)][int]$WaitPid,
    [switch]$Restart
)

$ErrorActionPreference = 'Stop'
$dest = Join-Path $env:LOCALAPPDATA 'Qalam'
$logDir = Join-Path $dest 'logs'
$log = Join-Path $logDir 'update.log'
$installed = $false

New-Item -ItemType Directory -Force -Path $logDir | Out-Null

function Write-UpdateLog([string]$message) {
    try {
        $line = '[' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + '] ' + $message
        Add-Content -LiteralPath $log -Value $line -Encoding UTF8
    } catch { }
}

function Start-QalamIfStopped {
    if (Get-Process -Id $WaitPid -ErrorAction SilentlyContinue) { return }
    $exe = Join-Path $dest 'Qalam.exe'
    if (Test-Path -LiteralPath $exe) {
        try { Start-Process -FilePath $exe -WorkingDirectory $dest } catch { }
    }
}

try {
    Write-UpdateLog ('update start setup=' + $Setup)

    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Process -Id $WaitPid -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 250
    }
    if (Get-Process -Id $WaitPid -ErrorAction SilentlyContinue) {
        throw 'Qalam did not exit within 30 seconds.'
    }

    if (-not (Test-Path -LiteralPath $Setup)) { throw ('Update installer missing: ' + $Setup) }
    $actual = (Get-FileHash -LiteralPath $Setup -Algorithm SHA256).Hash
    if ($actual -ne $Sha256) { throw 'Update installer checksum mismatch.' }

    Write-UpdateLog 'running setup'
    $process = Start-Process -FilePath $Setup -ArgumentList '/S' -Wait -PassThru -WindowStyle Hidden
    if ($null -eq $process -or $process.ExitCode -ne 0) {
        $code = if ($null -eq $process) { 'unknown' } else { $process.ExitCode }
        throw ('The setup failed with exit code ' + $code + '.')
    }

    $installed = $true
    Write-UpdateLog 'update complete'
    if (Test-Path -LiteralPath $Setup) {
        try { Remove-Item -LiteralPath $Setup -Force } catch { }
    }

    if ($Restart) {
        $exe = Join-Path $dest 'Qalam.exe'
        if (-not (Test-Path -LiteralPath $exe)) { throw ('The updated Qalam executable is missing: ' + $exe) }
        Start-Process -FilePath $exe -WorkingDirectory $dest
    }
} catch {
    Write-UpdateLog ('update failed: ' + $_.Exception.Message)
    if (-not $installed) { Start-QalamIfStopped }
}
