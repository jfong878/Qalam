param(
    [Parameter(Mandatory = $true)][string]$Zip
)

$ErrorActionPreference = 'Stop'
$logDir = Join-Path $env:LOCALAPPDATA 'Qalam\logs'
$log = Join-Path $logDir 'install.log'
$work = Join-Path $env:TEMP ('qalam-setup-' + [Guid]::NewGuid().ToString('N'))

New-Item -ItemType Directory -Force -Path $logDir | Out-Null

function Write-SetupLog([string]$message) {
    try {
        $line = '[' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + '] ' + $message
        Add-Content -LiteralPath $log -Value $line -Encoding UTF8
    } catch { }
}

try {
    Write-SetupLog ('setup start zip=' + $Zip)
    if (-not (Test-Path -LiteralPath $Zip)) { throw ('Payload missing: ' + $Zip) }

    Expand-Archive -LiteralPath $Zip -DestinationPath $work -Force
    $installer = Join-Path $work 'install.ps1'
    if (-not (Test-Path -LiteralPath $installer)) { throw 'The payload does not contain install.ps1.' }

    $powershell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = & $powershell -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File $installer -SkipBuild -NoLaunch 2>&1
        $code = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previous
    }
    foreach ($line in @($output)) { Write-SetupLog ('  ' + [string]$line) }
    if ($code -ne 0) { throw ('The installer failed with exit code ' + $code + '.') }

    Write-SetupLog 'setup complete'
    exit 0
} catch {
    Write-SetupLog ('setup failed: ' + $_.Exception.Message)
    exit 1
} finally {
    if (Test-Path -LiteralPath $work) {
        try { Remove-Item -LiteralPath $work -Recurse -Force } catch { }
    }
}
