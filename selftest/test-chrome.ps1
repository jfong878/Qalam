$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ui-test-lib.ps1')
$traceFile = Join-Path $PSScriptRoot 'app-test-chrome.trace'
function Trace([string]$Message) {
    Add-Content -LiteralPath $traceFile -Value ((Get-Date).ToString('HH:mm:ss.fff') + ' ' + $Message) -Encoding UTF8
}
Set-Content -LiteralPath $traceFile -Value ((Get-Date).ToString('HH:mm:ss.fff') + ' start') -Encoding UTF8

$token = 'qalam-chrome-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$text = "Qalam Chrome selection capture test $token."
$title = "Qalam Chrome $token"
$html = Join-Path $env:TEMP ($token + '.html')
$profile = Join-Path $env:TEMP ($token + '-profile')
$chrome = 'C:\Program Files\Google\Chrome\Application\chrome.exe'

@"
<!doctype html>
<html>
<head><meta charset="utf-8"><title>$title</title></head>
<body style="margin:0;font:28px 'Segoe UI',sans-serif;padding:60px;background:#fff;color:#111">$text</body>
</html>
"@ | Set-Content -Path $html -Encoding UTF8

$status = 'failed'
$details = [ordered]@{ token = $token; expected = $text; expected_chars = $text.Length; window_found = $false; accepted = $false }
$process = $null

try {
    Trace 'launching chrome'
    $uri = ([Uri]$html).AbsoluteUri
    $process = Start-Process -FilePath $chrome -ArgumentList @(
        "--user-data-dir=$profile",
        '--no-first-run',
        '--no-default-browser-check',
        '--disable-session-crashed-bubble',
        "--app=$uri",
        '--window-size=1000,600'
    ) -PassThru

    $window = Find-QalamWindow $title 25
    if ($null -eq $window) { throw "Chrome window '$title' not found." }
    Trace 'window found'
    $details.window_found = $true
    $targetPid = Activate-QalamWindow $window 'chrome'
    Trace ('activated pid ' + $targetPid)
    $details.window_pid = $targetPid
    Start-Sleep -Milliseconds 500
    Focus-QalamContent $window 0.5 0.45
    Trace 'focused content'
    Assert-QalamForeground $window 'chrome' | Out-Null

    $before = Get-QalamLogCount
    Invoke-QalamDoubleCopy 'chrome' $window
    Trace 'copy sent'
    $newLines = Wait-QalamLog $before 15
    Trace 'log waited'
    $details.log = @($newLines)
    $details.clipboard = Get-QalamClipboardText
    $foreground = [QalamTestWin32]::GetForegroundWindow()
    $foregroundPid = 0
    [void][QalamTestWin32]::GetWindowThreadProcessId($foreground, [ref]$foregroundPid)
    $details.foreground_after = (Get-Process -Id $foregroundPid -ErrorAction SilentlyContinue).ProcessName

    $accepted = @($newLines | Where-Object { $_ -match 'trigger accepted' })
    if ($accepted.Count -gt 0) {
        $details.accepted = $true
        $details.method = if ($accepted[0] -match '\(uia\)') { 'uia' } else { 'clipboard' }
        $details.process = if ($accepted[0] -match 'trigger accepted: ([^ ]+)') { $Matches[1] } else { '' }
        $details.clipboard_matches = ($details.clipboard.Trim() -eq $text.Trim())
        if ($details.clipboard_matches -and $details.process -eq 'chrome') {
            $status = 'accepted'
        } else {
            $details.accepted = $false
            $details.error = "trigger came from '$($details.process)' and clipboard_matches=$($details.clipboard_matches)"
        }
    } else {
        $skip = @($newLines | Where-Object { $_ -match 'skip:' })
        if ($skip.Count -gt 0) { $details.skip = $skip }
    }

    try { [System.Windows.Forms.SendKeys]::SendWait('{ESC}') } catch { }
    Trace 'popup dismissed'
} catch {
    $details.error = $_.Exception.Message
    Trace ('error: ' + $_.Exception.Message)
} finally {
    Trace 'cleanup start'
    if ($null -ne $process) { Stop-QalamProcessTree $process.Id }
    Trace 'cleanup parent killed'
    Stop-QalamProcessesByCommandLine $profile
    Trace 'cleanup profile processes killed'
    Start-Sleep -Milliseconds 500
    try { Remove-Item -LiteralPath $html -Force -ErrorAction SilentlyContinue } catch { }
    try { Remove-Item -LiteralPath $profile -Recurse -Force -ErrorAction SilentlyContinue } catch { }
    Trace 'cleanup files removed'
    Write-QalamTestResult 'chrome' $status $details
    Trace 'result written'
}
