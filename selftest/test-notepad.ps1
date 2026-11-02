$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ui-test-lib.ps1')
$traceFile = Join-Path $PSScriptRoot 'app-test-notepad.trace'
function Trace([string]$Message) {
    Add-Content -LiteralPath $traceFile -Value ((Get-Date).ToString('HH:mm:ss.fff') + ' ' + $Message) -Encoding UTF8
}
Set-Content -LiteralPath $traceFile -Value ((Get-Date).ToString('HH:mm:ss.fff') + ' start') -Encoding UTF8

$token = 'qalam-notepad-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$text = "Qalam Notepad selection capture test $token."
$file = Join-Path $env:TEMP ($token + '.txt')
Set-Content -Path $file -Value $text -Encoding UTF8 -NoNewline

$status = 'failed'
$details = [ordered]@{ token = $token; expected = $text; expected_chars = $text.Length; window_found = $false; accepted = $false }
$beforePids = @(Get-Process notepad -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
$process = $null

try {
    if ($beforePids.Count -gt 0) { throw 'Notepad is already running; skipped so existing user tabs are not touched.' }

    Trace 'launching Notepad'
    $process = Start-Process -FilePath 'notepad.exe' -ArgumentList @($file) -PassThru

    $window = Find-QalamWindow $token 20
    if ($null -eq $window) { throw "Notepad window for '$token' not found." }
    Trace 'window found'
    $details.window_found = $true
    $targetPid = Activate-QalamWindow $window 'notepad'
    Trace ('activated pid ' + $targetPid)
    $details.window_pid = $targetPid
    Start-Sleep -Milliseconds 500
    Focus-QalamContent $window 0.5 0.5
    Trace 'content focused'

    $before = Get-QalamLogCount
    Invoke-QalamDoubleCopy 'notepad'
    Trace 'copy sent'
    $newLines = Wait-QalamLog $before 15
    Trace 'log waited'
    $details.log = @($newLines)
    $details.clipboard = Get-QalamClipboardText

    $accepted = @($newLines | Where-Object { $_ -match 'trigger accepted' })
    if ($accepted.Count -gt 0) {
        $status = 'accepted'
        $details.accepted = $true
        $details.method = if ($accepted[0] -match '\(uia\)') { 'uia' } else { 'clipboard' }
        $details.process = if ($accepted[0] -match 'trigger accepted: ([^ ]+)') { $Matches[1] } else { '' }
        $details.clipboard_matches = ($details.clipboard.Trim() -eq $text.Trim())
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
    if ($null -ne $process -and -not $process.HasExited) {
        try { [void]$process.CloseMainWindow() } catch { }
        Start-Sleep -Milliseconds 600
    }
    Get-Process notepad -ErrorAction SilentlyContinue |
        Where-Object { $beforePids -notcontains $_.Id } |
        Stop-Process -Force -ErrorAction SilentlyContinue
    try { Remove-Item -LiteralPath $file -Force -ErrorAction SilentlyContinue } catch { }
    Trace 'cleanup files removed'
    Write-QalamTestResult 'notepad' $status $details
    Trace 'result written'
}
