$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName Microsoft.VisualBasic

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class QalamTestWin32 {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr SetFocus(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
}
'@

$script:QalamLog = Join-Path $env:LOCALAPPDATA 'Qalam\logs\qalam.log'

function Find-QalamWindow([string]$Needle, [int]$TimeoutSeconds = 25) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $root = [System.Windows.Automation.AutomationElement]::RootElement
        $windows = $root.FindAll(
            [System.Windows.Automation.TreeScope]::Children,
            [System.Windows.Automation.Condition]::TrueCondition)
        foreach ($window in $windows) {
            try {
                $name = $window.Current.Name
            } catch {
                continue
            }
            if ($name -and $name.IndexOf($Needle, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                return $window
            }
        }
        Start-Sleep -Milliseconds 300
    }
    return $null
}

function Get-QalamWindowHandle($window) {
    if ($null -eq $window) { return [IntPtr]::Zero }
    return [IntPtr]$window.Current.NativeWindowHandle
}

function Activate-QalamWindow($window, [string]$ExpectedProcess, [int]$TimeoutSeconds = 8) {
    $handle = Get-QalamWindowHandle $window
    if ($handle -eq [IntPtr]::Zero) { throw "Window has no native handle." }

    $windowPid = 0
    [void][QalamTestWin32]::GetWindowThreadProcessId($handle, [ref]$windowPid)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $deadline) {
        try { [Microsoft.VisualBasic.Interaction]::AppActivate($windowPid) } catch { }

        $foreground = [QalamTestWin32]::GetForegroundWindow()
        if ($foreground -eq $handle) { return $windowPid }

        $foregroundPid = 0
        $foregroundThread = [QalamTestWin32]::GetWindowThreadProcessId($foreground, [ref]$foregroundPid)
        $currentThread = [QalamTestWin32]::GetCurrentThreadId()
        [void][QalamTestWin32]::AttachThreadInput($currentThread, $foregroundThread, $true)
        [void][QalamTestWin32]::ShowWindow($handle, 9)
        [void][QalamTestWin32]::BringWindowToTop($handle)
        [void][QalamTestWin32]::SetForegroundWindow($handle)
        [void][QalamTestWin32]::SetFocus($handle)
        [void][QalamTestWin32]::AttachThreadInput($currentThread, $foregroundThread, $false)
        Start-Sleep -Milliseconds 300

        if ([QalamTestWin32]::GetForegroundWindow() -eq $handle) { return $windowPid }
    }

    $foreground = [QalamTestWin32]::GetForegroundWindow()
    $foregroundPid = 0
    [void][QalamTestWin32]::GetWindowThreadProcessId($foreground, [ref]$foregroundPid)
    $foregroundName = ''
    try { $foregroundName = (Get-Process -Id $foregroundPid -ErrorAction Stop).ProcessName } catch { }
    throw "Could not bring '$ExpectedProcess' to the foreground (foreground PID $foregroundPid, $foregroundName); refusing to send keys."
}

function Assert-QalamForeground($window, [string]$ExpectedProcess, [int]$TimeoutSeconds = 5) {
    $handle = Get-QalamWindowHandle $window
    if ($handle -eq [IntPtr]::Zero) { throw "Window has no native handle." }
    $windowPid = 0
    [void][QalamTestWin32]::GetWindowThreadProcessId($handle, [ref]$windowPid)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try { [Microsoft.VisualBasic.Interaction]::AppActivate($windowPid) } catch { }
        [void][QalamTestWin32]::ShowWindow($handle, 9)
        [void][QalamTestWin32]::SetWindowPos($handle, [IntPtr]::Zero, 0, 0, 0, 0, 0x0001 -bor 0x0002 -bor 0x0040)
        [void][QalamTestWin32]::SetForegroundWindow($handle)
        Start-Sleep -Milliseconds 200
        if ([QalamTestWin32]::GetForegroundWindow() -eq $handle) { return $windowPid }
    }
    throw "Could not keep '$ExpectedProcess' in the foreground; refusing to send keys."
}

function Focus-QalamContent($window, [double]$XFraction = 0.5, [double]$YFraction = 0.45) {
    $handle = Get-QalamWindowHandle $window
    $rect = New-Object QalamTestWin32+RECT
    if (-not [QalamTestWin32]::GetWindowRect($handle, [ref]$rect)) { throw "GetWindowRect failed." }
    $x = [int]($rect.Left + ($rect.Right - $rect.Left) * $XFraction)
    $y = [int]($rect.Top + ($rect.Bottom - $rect.Top) * $YFraction)
    [void][QalamTestWin32]::SetCursorPos($x, $y)
    [QalamTestWin32]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    [QalamTestWin32]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 250
}

function Get-QalamLogCount {
    if (-not (Test-Path $script:QalamLog)) { return 0 }
    return @(Get-Content $script:QalamLog -ErrorAction SilentlyContinue).Count
}

function Get-QalamNewLog([int]$Before) {
    if (-not (Test-Path $script:QalamLog)) { return @() }
    $lines = @(Get-Content $script:QalamLog -ErrorAction SilentlyContinue)
    if ($lines.Count -le $Before) { return @() }
    return @($lines | Select-Object -Skip $Before)
}

function Wait-QalamLog([int]$Before, [int]$TimeoutSeconds = 15) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lines = @()
    while ((Get-Date) -lt $deadline) {
        $lines = Get-QalamNewLog $Before
        $trigger = @($lines | Where-Object { $_ -match 'trigger accepted|skip:|trigger:' })
        if ($trigger.Count -gt 0) { break }
        Start-Sleep -Milliseconds 250
    }

    $accepted = @($lines | Where-Object { $_ -match 'trigger accepted' })
    if ($accepted.Count -gt 0) {
        $resultDeadline = (Get-Date).AddSeconds(10)
        while ((Get-Date) -lt $resultDeadline) {
            $lines = Get-QalamNewLog $Before
            if (@($lines | Where-Object { $_ -match '\bresult [^ ]+->|translate:|tashkeel:' }).Count -gt 0) { break }
            Start-Sleep -Milliseconds 250
        }
    }
    return $lines
}

function Invoke-QalamDoubleCopy([string]$Label, $Window = $null) {
    $placeholder = 'qalam-test-placeholder-' + $Label + '-' + [Guid]::NewGuid().ToString('N')
    try { [System.Windows.Forms.Clipboard]::SetText($placeholder) } catch { }

    if ($null -ne $Window) {
        $handle = Get-QalamWindowHandle $Window
        if ([QalamTestWin32]::GetForegroundWindow() -ne $handle) {
            throw "The target window lost the foreground before copy; refusing to send keys."
        }
    }

    [System.Windows.Forms.SendKeys]::SendWait('^a')
    Start-Sleep -Milliseconds 300
    [System.Windows.Forms.SendKeys]::SendWait('^c')
    Start-Sleep -Milliseconds 180
    [System.Windows.Forms.SendKeys]::SendWait('^c')
    Start-Sleep -Milliseconds 350
}

function Get-QalamClipboardText {
    try {
        if ([System.Windows.Forms.Clipboard]::ContainsText()) {
            return [System.Windows.Forms.Clipboard]::GetText()
        }
    } catch { }
    return ''
}

function Stop-QalamProcessTree([int]$ProcessId) {
    if ($ProcessId -le 0) { return }
    try { & taskkill.exe /PID $ProcessId /T /F 2>$null | Out-Null } catch { }
}

function Stop-QalamProcessesByCommandLine([string]$Needle) {
    try {
        $matches = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
            Where-Object { $_.CommandLine -and $_.CommandLine.IndexOf($Needle, [StringComparison]::OrdinalIgnoreCase) -ge 0 }
        foreach ($match in $matches) {
            Stop-QalamProcessTree ([int]$match.ProcessId)
        }
    } catch { }
}

function Write-QalamTestResult([string]$App, [string]$Status, $Details) {
    $trace = Join-Path $PSScriptRoot 'app-test-lib.trace'
    Add-Content -LiteralPath $trace -Value ((Get-Date).ToString('HH:mm:ss.fff') + ' result start ' + $App) -Encoding UTF8
    $out = Join-Path $PSScriptRoot ("app-test-" + $App + ".json")
    $payload = [ordered]@{
        app = $App
        status = $Status
        timestamp = (Get-Date).ToString('o')
        details = $Details
    }
    Add-Content -LiteralPath $trace -Value ((Get-Date).ToString('HH:mm:ss.fff') + ' json start') -Encoding UTF8
    $safeDetails = [ordered]@{}
    foreach ($key in $Details.Keys) {
        $value = $Details[$key]
        Add-Content -LiteralPath $trace -Value ((Get-Date).ToString('HH:mm:ss.fff') + ' field ' + $key + ' type ' + $(if ($null -eq $value) { 'null' } else { $value.GetType().FullName })) -Encoding UTF8
        if ($null -eq $value) {
            $safeDetails[$key] = $null
        } elseif ($value -is [string] -or $value -is [bool] -or $value -is [int] -or $value -is [long] -or $value -is [double]) {
            $safeDetails[$key] = $value
        } elseif ($value -is [array]) {
            $safeDetails[$key] = @($value | ForEach-Object { [string]$_ })
        } else {
            $safeDetails[$key] = [string]$value
        }
    }
    $payload.details = $safeDetails
    $json = $payload | ConvertTo-Json -Depth 8
    Add-Content -LiteralPath $trace -Value ((Get-Date).ToString('HH:mm:ss.fff') + ' json done') -Encoding UTF8
    Set-Content -Path $out -Value $json -Encoding UTF8
    Add-Content -LiteralPath $trace -Value ((Get-Date).ToString('HH:mm:ss.fff') + ' json written') -Encoding UTF8
    Write-Output ("RESULT " + $App + " " + $Status)
    Write-Output ("FILE " + $out)
}
