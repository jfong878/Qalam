$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
Set-Location $root
$builder = Get-Process -Id 25812 -ErrorAction SilentlyContinue
if ($builder) {
    Write-Output 'waiting for corpus builder'
    [void]$builder.WaitForExit(220000)
}
if (Get-Process -Id 25812 -ErrorAction SilentlyContinue) {
    Write-Output 'builder_still_running'
    exit 2
}
Write-Output 'builder_exited'
Get-ChildItem (Join-Path $root 'concordance') -Filter '*deep2m*' |
    Select-Object Name,Length,LastWriteTime | Format-Table -AutoSize
$build = Start-Process -FilePath 'powershell.exe' -WorkingDirectory $root -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-File','build.ps1') -Wait -PassThru -NoNewWindow
Write-Output ('normal_build_exit=' + $build.ExitCode)
if ($build.ExitCode -ne 0) { exit $build.ExitCode }
$install = Start-Process -FilePath 'powershell.exe' -WorkingDirectory $root -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-File','install.ps1','-SkipBuild') -Wait -PassThru -NoNewWindow
Write-Output ('install_exit=' + $install.ExitCode)
if ($install.ExitCode -ne 0) { exit $install.ExitCode }
$destExe = Join-Path $env:LOCALAPPDATA 'Qalam\Qalam.exe'
if (-not (Get-Process Qalam -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $destExe })) {
    Start-Process -FilePath $destExe -WorkingDirectory (Split-Path $destExe)
}
Get-Process Qalam -ErrorAction SilentlyContinue |
    Select-Object Id,Path | Format-Table -AutoSize
