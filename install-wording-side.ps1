$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dest = Join-Path $env:LOCALAPPDATA 'Qalam'
$installedExe = Join-Path $dest 'Qalam.exe'
Get-Process Qalam -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq $installedExe } |
    Stop-Process -Force
Start-Sleep -Milliseconds 400
Copy-Item -LiteralPath (Join-Path $root 'dist\Qalam-wording.exe') -Destination $installedExe -Force
Copy-Item -Path (Join-Path $root 'mobile\*') -Destination (Join-Path $dest 'mobile') -Force
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination $dest -Force
Start-Process -FilePath $installedExe -WorkingDirectory $dest
Start-Sleep -Seconds 2
Write-Output ('installed=' + (Get-Item $installedExe).LastWriteTime)
Write-Output ('mobile_theme=' + (Select-String -LiteralPath (Join-Path $dest 'mobile\app.css') -Pattern 'data-theme="dark"' -SimpleMatch).Count)
Write-Output ('mobile_wording=' + (Select-String -LiteralPath (Join-Path $dest 'mobile\index.html') -Pattern 'wordingBtn' -SimpleMatch).Count)
Get-Process Qalam -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq $installedExe } |
    Select-Object Id,Path | Format-Table -AutoSize
