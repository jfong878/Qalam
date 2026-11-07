param(
    [string]$Token = 'QalamHandoff',
    [int]$CloseAfterSeconds = 180
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase
Add-Type -AssemblyName System.Windows.Forms

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class ViewInAppSourceWin32 {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr SetFocus(IntPtr hWnd);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
}
'@

# Keep the source text ASCII-safe here; the Arabic is assembled from code points
# so Windows PowerShell 5.1 does not depend on the file encoding.
$arabic = [string]::Join('', @(
    [char]0x0645, [char]0x0631, [char]0x062D, [char]0x0628, [char]0x0627,
    [char]0x0020,
    [char]0x0628, [char]0x0627, [char]0x0644, [char]0x0639, [char]0x0627, [char]0x0644, [char]0x0645
))
$text = $arabic + ' ' + $Token

$window = New-Object System.Windows.Window
$window.Title = 'Qalam View in App smoke source ' + $Token
$window.Width = 680
$window.Height = 190
$window.Topmost = $true
$window.WindowStartupLocation = 'CenterScreen'
$window.ResizeMode = 'NoResize'

$box = New-Object System.Windows.Controls.TextBox
$box.Text = $text
$box.FontSize = 22
$box.TextWrapping = 'Wrap'
$box.AcceptsReturn = $true
$window.Content = $box

$script:step = 0
$script:closeTick = [Math]::Max(8, [int]($CloseAfterSeconds * 4))
$timer = New-Object System.Windows.Threading.DispatcherTimer
$timer.Interval = [TimeSpan]::FromMilliseconds(250)
$timer.Add_Tick({
    $script:step++
    if ($script:step -eq 2) {
        [System.Windows.Forms.SendKeys]::SendWait('^c')
    } elseif ($script:step -eq 3) {
        [System.Windows.Forms.SendKeys]::SendWait('^c')
    } elseif ($script:step -eq 5) {
        $window.Topmost = $false
    } elseif ($script:step -eq $script:closeTick) {
        $timer.Stop()
        $window.Close()
    }
})

$window.Add_ContentRendered({
    $helper = New-Object System.Windows.Interop.WindowInteropHelper($window)
    $handle = $helper.Handle
    $foregroundPid = 0
    $foregroundThread = [ViewInAppSourceWin32]::GetWindowThreadProcessId([ViewInAppSourceWin32]::GetForegroundWindow(), [ref]$foregroundPid)
    $currentThread = [ViewInAppSourceWin32]::GetCurrentThreadId()
    [void][ViewInAppSourceWin32]::AttachThreadInput($currentThread, $foregroundThread, $true)
    [void][ViewInAppSourceWin32]::BringWindowToTop($handle)
    [void][ViewInAppSourceWin32]::SetForegroundWindow($handle)
    [void][ViewInAppSourceWin32]::SetFocus($handle)
    [void][ViewInAppSourceWin32]::AttachThreadInput($currentThread, $foregroundThread, $false)
    $window.Activate()
    $box.Focus()
    $box.SelectAll()
    $timer.Start()
})

[void]$window.ShowDialog()
