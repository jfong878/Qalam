$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$process = Get-Process Qalam -ErrorAction Stop | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if ($null -eq $process) { throw 'Qalam main window not found' }
$main = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
$buttonCondition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty, 'Report a bug')
$button = $main.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $buttonCondition)
if ($null -eq $button) { throw 'Report a bug button not found in the main window' }
$invoke = $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
$invoke.Invoke()
Start-Sleep -Milliseconds 900

$windowCondition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $process.Id)
$windows = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
    [System.Windows.Automation.TreeScope]::Children, $windowCondition)
$reportWindow = $null
foreach ($window in $windows) {
    if ($window.Current.Name -like 'Report a bug*') { $reportWindow = $window; break }
}
if ($null -eq $reportWindow) {
    $names = @($windows | ForEach-Object { $_.Current.Name })
    throw ('Report dialog did not open. Windows: ' + ($names -join ' | '))
}
$copyCondition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty, 'Copy report')
$copy = $reportWindow.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $copyCondition)
if ($null -eq $copy) { throw 'Copy report control not found in dialog' }
Write-Output ('dialog=' + $reportWindow.Current.Name)
Write-Output 'desktop_report_dialog_ok'
$close = $reportWindow.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
$close.Close()
Start-Sleep -Milliseconds 300
Write-Output 'dialog_closed'
