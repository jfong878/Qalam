Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class TestWin32 {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr SetFocus(IntPtr hWnd);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
}
'@

$savedClipboard = $null
try {
    if ([System.Windows.Forms.Clipboard]::ContainsText()) {
        $savedClipboard = [System.Windows.Forms.Clipboard]::GetText()
    }
} catch { }

try { [System.Windows.Forms.Clipboard]::SetText('qalam trigger placeholder') } catch { }

$form = New-Object System.Windows.Forms.Form
$form.Text = 'Qalam trigger test'
$form.Width = 560
$form.Height = 170
$form.StartPosition = 'CenterScreen'
$form.TopMost = $true

$box = New-Object System.Windows.Forms.TextBox
$box.Multiline = $true
$box.Dock = 'Fill'
$box.Font = New-Object System.Drawing.Font('Segoe UI', 12)
$box.Text = 'The student wrote the lesson at school this morning.'
$form.Controls.Add($box)

$script:step = 0
$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 250
$timer.Add_Tick({
    $script:step++
    if ($script:step -eq 2) {
        $box.Focus()
        $box.SelectAll()
        [System.Windows.Forms.SendKeys]::SendWait('^c')
    } elseif ($script:step -eq 3) {
        [System.Windows.Forms.SendKeys]::SendWait('^c')
    } elseif ($script:step -eq 6) {
        try {
            [System.IO.File]::WriteAllText($clipboardFile, [System.Windows.Forms.Clipboard]::GetText(), (New-Object System.Text.UTF8Encoding($false)))
        } catch { }
    } elseif ($script:step -eq 22) {
        [System.Windows.Forms.SendKeys]::SendWait('{ESC}')
        $timer.Stop()
        $form.Close()
    }
})

$clipboardFile = Join-Path $PSScriptRoot 'trigger-test-clipboard.txt'
$form.Add_Shown({
    $foregroundPid = 0
    $foregroundThread = [TestWin32]::GetWindowThreadProcessId([TestWin32]::GetForegroundWindow(), [ref]$foregroundPid)
    $currentThread = [TestWin32]::GetCurrentThreadId()
    [void][TestWin32]::AttachThreadInput($currentThread, $foregroundThread, $true)
    [void][TestWin32]::BringWindowToTop($form.Handle)
    [void][TestWin32]::SetForegroundWindow($form.Handle)
    [void][TestWin32]::SetFocus($form.Handle)
    [void][TestWin32]::AttachThreadInput($currentThread, $foregroundThread, $false)
    $form.Activate()
    $box.Focus()
    $box.SelectAll()
    $timer.Start()
})

[void]$form.ShowDialog()

if ($null -ne $savedClipboard) {
    try { [System.Windows.Forms.Clipboard]::SetText($savedClipboard) } catch { }
}
