Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class SlangTestDpi {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
'@
[void][SlangTestDpi]::SetProcessDPIAware()

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class SlangTestWin32 {
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
try { [System.Windows.Forms.Clipboard]::SetText('qalam slang trigger placeholder') } catch { }

$form = New-Object System.Windows.Forms.Form
$form.Text = 'Qalam slang trigger test'
$form.Width = 720
$form.Height = 180
$form.StartPosition = 'CenterScreen'
$form.TopMost = $true

$box = New-Object System.Windows.Forms.TextBox
$box.Multiline = $true
$box.Dock = 'Fill'
$box.Font = New-Object System.Drawing.Font('Segoe UI', 13)
$box.Text = 'Calla chingada, no mames con mamar.'
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
    } elseif ($script:step -eq 8) {
        $form.Opacity = 0
    } elseif ($script:step -eq 32) {
        try {
            $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
            $bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
            $graphics = [System.Drawing.Graphics]::FromImage($bmp)
            $graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
            $bmp.Save($screenshotFile, [System.Drawing.Imaging.ImageFormat]::Png)
            $graphics.Dispose()
            $bmp.Dispose()
        } catch { }
    } elseif ($script:step -eq 52) {
        $timer.Stop()
        $form.Close()
    }
})

$clipboardFile = Join-Path $PSScriptRoot 'trigger-slang-clipboard.txt'
$screenshotFile = Join-Path $PSScriptRoot 'slang-popup-installed.png'
$form.Add_Shown({
    $foregroundPid = 0
    $foregroundThread = [SlangTestWin32]::GetWindowThreadProcessId([SlangTestWin32]::GetForegroundWindow(), [ref]$foregroundPid)
    $currentThread = [SlangTestWin32]::GetCurrentThreadId()
    [void][SlangTestWin32]::AttachThreadInput($currentThread, $foregroundThread, $true)
    [void][SlangTestWin32]::BringWindowToTop($form.Handle)
    [void][SlangTestWin32]::SetForegroundWindow($form.Handle)
    [void][SlangTestWin32]::SetFocus($form.Handle)
    [void][SlangTestWin32]::AttachThreadInput($currentThread, $foregroundThread, $false)
    $form.Activate()
    $box.Focus()
    $box.SelectAll()
    $timer.Start()
})

[void]$form.ShowDialog()

if ($null -ne $savedClipboard) {
    try { [System.Windows.Forms.Clipboard]::SetText($savedClipboard) } catch { }
}
