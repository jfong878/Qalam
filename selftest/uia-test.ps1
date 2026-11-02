Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase
Add-Type -AssemblyName System.Windows.Forms

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class UiaTestWin32 {
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
try { [System.Windows.Forms.Clipboard]::SetText('qalam uia placeholder') } catch { }

$window = New-Object System.Windows.Window
$window.Title = 'Qalam UIA test (Ctrl+C suppressed)'
$window.Width = 560
$window.Height = 180
$window.Topmost = $true
$window.WindowStartupLocation = 'CenterScreen'

$box = New-Object System.Windows.Controls.TextBox
$box.Text = 'The student wrote the lesson at school this morning.'
$box.FontSize = 18
$box.TextWrapping = 'Wrap'
$box.AcceptsReturn = $true
$window.Content = $box

$window.Add_PreviewKeyDown({
    $mods = [System.Windows.Input.Keyboard]::Modifiers
    if ($_.Key -eq [System.Windows.Input.Key]::C -and
        (($mods -band [System.Windows.Input.ModifierKeys]::Control) -eq [System.Windows.Input.ModifierKeys]::Control)) {
        $_.Handled = $true
    }
})

$script:step = 0
$timer = New-Object System.Windows.Threading.DispatcherTimer
$timer.Interval = [TimeSpan]::FromMilliseconds(250)
$timer.Add_Tick({
    $script:step++
    if ($script:step -eq 2) {
        [System.Windows.Forms.SendKeys]::SendWait('^c')
    } elseif ($script:step -eq 3) {
        [System.Windows.Forms.SendKeys]::SendWait('^c')
    } elseif ($script:step -eq 24) {
        [System.Windows.Forms.SendKeys]::SendWait('{ESC}')
        $timer.Stop()
        $window.Close()
    }
})

$window.Add_ContentRendered({
    $helper = New-Object System.Windows.Interop.WindowInteropHelper($window)
    $handle = $helper.Handle
    $foregroundPid = 0
    $foregroundThread = [UiaTestWin32]::GetWindowThreadProcessId([UiaTestWin32]::GetForegroundWindow(), [ref]$foregroundPid)
    $currentThread = [UiaTestWin32]::GetCurrentThreadId()
    [void][UiaTestWin32]::AttachThreadInput($currentThread, $foregroundThread, $true)
    [void][UiaTestWin32]::BringWindowToTop($handle)
    [void][UiaTestWin32]::SetForegroundWindow($handle)
    [void][UiaTestWin32]::SetFocus($handle)
    [void][UiaTestWin32]::AttachThreadInput($currentThread, $foregroundThread, $false)
    $window.Activate()
    $box.Focus()
    $box.SelectAll()
    $timer.Start()
})

[void]$window.ShowDialog()

if ($null -ne $savedClipboard) {
    try { [System.Windows.Forms.Clipboard]::SetText($savedClipboard) } catch { }
}
