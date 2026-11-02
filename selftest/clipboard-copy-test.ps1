Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class CopyTestWin32 {
    [DllImport("user32.dll")] public static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr SetFocus(IntPtr hWnd);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
}
'@

$form = New-Object System.Windows.Forms.Form
$form.Text = 'Clipboard copy test'
$form.Width = 500
$form.Height = 150
$form.StartPosition = 'CenterScreen'
$form.TopMost = $true

$box = New-Object System.Windows.Forms.TextBox
$box.Multiline = $true
$box.Dock = 'Fill'
$box.Font = New-Object System.Drawing.Font('Segoe UI', 12)
$box.Text = 'The student wrote the lesson at school this morning.'
$form.Controls.Add($box)

$outFile = Join-Path $PSScriptRoot 'clipboard-copy-test.txt'
$script:lines = New-Object System.Collections.Generic.List[string]
$script:step = 0
$script:watch = [System.Diagnostics.Stopwatch]::StartNew()

$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 250
$timer.Add_Tick({
    $script:step++
    if ($script:step -eq 2) {
        $script:lines.Add(('before first  t={0} seq={1}' -f $script:watch.ElapsedMilliseconds, [CopyTestWin32]::GetClipboardSequenceNumber()))
        [System.Windows.Forms.SendKeys]::SendWait('^c')
        $script:lines.Add(('after first   t={0} seq={1}' -f $script:watch.ElapsedMilliseconds, [CopyTestWin32]::GetClipboardSequenceNumber()))
        $script:lines.Add(('clipboard     {0}' -f [System.Windows.Forms.Clipboard]::GetText()))
    } elseif ($script:step -eq 3) {
        $script:lines.Add(('before second t={0} seq={1}' -f $script:watch.ElapsedMilliseconds, [CopyTestWin32]::GetClipboardSequenceNumber()))
        [System.Windows.Forms.SendKeys]::SendWait('^c')
        $script:lines.Add(('after second  t={0} seq={1}' -f $script:watch.ElapsedMilliseconds, [CopyTestWin32]::GetClipboardSequenceNumber()))
        $script:lines.Add(('clipboard     {0}' -f [System.Windows.Forms.Clipboard]::GetText()))
    } elseif ($script:step -eq 4) {
        $script:lines.Add(('after settle  t={0} seq={1}' -f $script:watch.ElapsedMilliseconds, [CopyTestWin32]::GetClipboardSequenceNumber()))
        [System.IO.File]::WriteAllLines($outFile, $script:lines)
        $timer.Stop()
        $form.Close()
    }
})

$form.Add_Shown({
    $foregroundPid = 0
    $foregroundThread = [CopyTestWin32]::GetWindowThreadProcessId([CopyTestWin32]::GetForegroundWindow(), [ref]$foregroundPid)
    $currentThread = [CopyTestWin32]::GetCurrentThreadId()
    [void][CopyTestWin32]::AttachThreadInput($currentThread, $foregroundThread, $true)
    [void][CopyTestWin32]::BringWindowToTop($form.Handle)
    [void][CopyTestWin32]::SetForegroundWindow($form.Handle)
    [void][CopyTestWin32]::SetFocus($form.Handle)
    [void][CopyTestWin32]::AttachThreadInput($currentThread, $foregroundThread, $false)
    $form.Activate()
    $box.Focus()
    $box.SelectAll()
    $timer.Start()
})

[void]$form.ShowDialog()
