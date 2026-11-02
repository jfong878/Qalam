$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ui-test-lib.ps1')
$traceFile = Join-Path $PSScriptRoot 'app-test-vscode.trace'
function Trace([string]$Message) {
    Add-Content -LiteralPath $traceFile -Value ((Get-Date).ToString('HH:mm:ss.fff') + ' ' + $Message) -Encoding UTF8
}
Set-Content -LiteralPath $traceFile -Value ((Get-Date).ToString('HH:mm:ss.fff') + ' start') -Encoding UTF8

$token = 'qalam-vscode-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$text = "Qalam VS Code selection capture test $token."
$file = Join-Path $env:TEMP ($token + '.txt')
$profile = Join-Path $env:TEMP ($token + '-profile')
$extensions = Join-Path $env:TEMP ($token + '-extensions')
$code = 'C:\Users\jfong\AppData\Local\Programs\Microsoft VS Code\Code.exe'
Set-Content -Path $file -Value $text -Encoding UTF8 -NoNewline

$status = 'failed'
$details = [ordered]@{ token = $token; expected = $text; expected_chars = $text.Length; window_found = $false; accepted = $false }
$process = $null

try {
    Trace 'launching vscode'
    $process = Start-Process -FilePath $code -ArgumentList @(
        "--user-data-dir=$profile",
        "--extensions-dir=$extensions",
        '--new-window',
        '--skip-welcome',
        '--skip-release-notes',
        '--disable-telemetry',
        '--disable-workspace-trust',
        $file
    ) -PassThru

    $window = Find-QalamWindow $token 30
    if ($null -eq $window) { throw "VS Code window for '$token' not found." }
    Trace 'window found'
    $details.window_found = $true
    $targetPid = Activate-QalamWindow $window 'Code'
    Trace ('activated pid ' + $targetPid)
    $details.window_pid = $targetPid
    Start-Sleep -Milliseconds 700
    Focus-QalamContent $window 0.65 0.45
    [System.Windows.Forms.SendKeys]::SendWait('^1')
    Start-Sleep -Milliseconds 300
    Trace 'editor focused'
    Assert-QalamForeground $window 'code' | Out-Null

    $before = Get-QalamLogCount
    Invoke-QalamDoubleCopy 'vscode' $window
    Trace 'copy sent'
    $newLines = Wait-QalamLog $before 15
    Trace 'log waited'
    $details.log = @($newLines)
    $details.clipboard = Get-QalamClipboardText

    $accepted = @($newLines | Where-Object { $_ -match 'trigger accepted' })
    if ($accepted.Count -gt 0) {
        $details.accepted = $true
        $details.method = if ($accepted[0] -match '\(uia\)') { 'uia' } else { 'clipboard' }
        $details.process = if ($accepted[0] -match 'trigger accepted: ([^ ]+)') { $Matches[1] } else { '' }
        $details.clipboard_matches = ($details.clipboard.Trim() -eq $text.Trim())
        if ($details.clipboard_matches -and $details.process -eq 'code') {
            $status = 'accepted'
        } else {
            $details.accepted = $false
            $details.error = "trigger came from '$($details.process)' and clipboard_matches=$($details.clipboard_matches)"
        }
    } else {
        $details.skip = @($newLines | Where-Object { $_ -match 'skip:' })
    }

    try { [System.Windows.Forms.SendKeys]::SendWait('{ESC}') } catch { }
    Trace 'popup dismissed'
} catch {
    $details.error = $_.Exception.Message
    Trace ('error: ' + $_.Exception.Message)
} finally {
    Trace 'cleanup start'
    if ($null -ne $process) { Stop-QalamProcessTree $process.Id }
    Stop-QalamProcessesByCommandLine $profile
    Trace 'cleanup processes killed'
    Start-Sleep -Milliseconds 500
    try { Remove-Item -LiteralPath $file -Force -ErrorAction SilentlyContinue } catch { }
    try { Remove-Item -LiteralPath $profile -Recurse -Force -ErrorAction SilentlyContinue } catch { }
    try { Remove-Item -LiteralPath $extensions -Recurse -Force -ErrorAction SilentlyContinue } catch { }
    Trace 'cleanup files removed'
    Write-QalamTestResult 'vscode' $status $details
    Trace 'result written'
}
