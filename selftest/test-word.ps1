$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ui-test-lib.ps1')
$traceFile = Join-Path $PSScriptRoot 'app-test-word.trace'
function Trace([string]$Message) {
    Add-Content -LiteralPath $traceFile -Value ((Get-Date).ToString('HH:mm:ss.fff') + ' ' + $Message) -Encoding UTF8
}
Set-Content -LiteralPath $traceFile -Value ((Get-Date).ToString('HH:mm:ss.fff') + ' start') -Encoding UTF8

$token = 'qalam-word-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$text = "Qalam Word selection capture test $token."
$file = Join-Path $env:TEMP ($token + '.rtf')
$wordExe = 'C:\Program Files\Microsoft Office\Root\Office16\WINWORD.EXE'
$rtf = '{\rtf1\ansi\deff0{\fonttbl{\f0\fswiss\fcharset0 Segoe UI;}}\f0\fs40 ' + $text + '\par}'
Set-Content -Path $file -Value $rtf -Encoding ASCII

$status = 'failed'
$details = [ordered]@{ token = $token; expected = $text; expected_chars = $text.Length; window_found = $false; accepted = $false }
$beforeWordPids = @(Get-Process WINWORD -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
$process = $null

try {
    Trace 'launching Word with RTF'
    $process = Start-Process -FilePath $wordExe -ArgumentList @($file) -PassThru

    $window = Find-QalamWindow $token 30
    if ($null -eq $window) { throw "Word window for '$token' not found." }
    Trace 'window found'
    $details.window_found = $true
    $targetPid = Activate-QalamWindow $window 'WINWORD'
    Trace ('activated pid ' + $targetPid)
    $details.window_pid = $targetPid
    Start-Sleep -Milliseconds 700
    Focus-QalamContent $window 0.5 0.55
    Trace 'content focused'
    Assert-QalamForeground $window 'winword' | Out-Null

    $before = Get-QalamLogCount
    Invoke-QalamDoubleCopy 'word' $window
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
        if ($details.clipboard_matches -and $details.process -eq 'winword') {
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
    if ($null -ne $process -and -not $process.HasExited) {
        try { [void]$process.CloseMainWindow() } catch { }
        Start-Sleep -Milliseconds 800
    }
    Get-Process WINWORD -ErrorAction SilentlyContinue |
        Where-Object { $beforeWordPids -notcontains $_.Id } |
        Stop-Process -Force -ErrorAction SilentlyContinue
    try { Remove-Item -LiteralPath $file -Force -ErrorAction SilentlyContinue } catch { }
    Trace 'cleanup files removed'
    Write-QalamTestResult 'word' $status $details
    Trace 'result written'
}
