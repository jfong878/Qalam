[CmdletBinding()]
param(
    [string]$AppPath = '',
    [string]$ResultPath = '',
    [string]$TracePath = '',
    [string]$ExpectedPair = 'ar->en',
    [string]$Token = ''
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ui-test-lib.ps1')
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms

if ([string]::IsNullOrWhiteSpace($AppPath)) {
    $AppPath = Join-Path $env:LOCALAPPDATA 'Qalam\Qalam.exe'
}
if ([string]::IsNullOrWhiteSpace($ResultPath)) {
    $ResultPath = Join-Path $PSScriptRoot 'view-in-app-smoke.json'
}
if ([string]::IsNullOrWhiteSpace($TracePath)) {
    $TracePath = [IO.Path]::ChangeExtension($ResultPath, '.trace')
}
if ([string]::IsNullOrWhiteSpace($Token)) {
    $Token = 'QalamHandoff' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
}

$AppPath = [IO.Path]::GetFullPath($AppPath)
$ResultPath = [IO.Path]::GetFullPath($ResultPath)
$TracePath = [IO.Path]::GetFullPath($TracePath)
$sourceScript = Join-Path $PSScriptRoot 'view-in-app-source.ps1'
$statusText = 'Opened from the compact pop-up.'
$arabic = [string]::Join('', @(
    [char]0x0645, [char]0x0631, [char]0x062D, [char]0x0628, [char]0x0627,
    [char]0x0020,
    [char]0x0628, [char]0x0627, [char]0x0644, [char]0x0639, [char]0x0627, [char]0x0644, [char]0x0645
))
$sourceText = $arabic + ' ' + $Token

$status = 'failed'
$qalam = $null
$source = $null
$historyExisted = $false
$historyBackup = Join-Path ([IO.Path]::GetTempPath()) ('qalam-view-in-app-history-' + [Guid]::NewGuid().ToString('N') + '.json')
$savedClipboard = $null
$clipboardSaved = $false
$details = [ordered]@{
    token = $Token
    source = $sourceText
    expected_pair = $ExpectedPair
    app = $AppPath
    app_version = ''
    app_sha256 = ''
    config_target = ''
    config_auto_target = $null
    store_dir = ''
    started = (Get-Date).ToString('o')
    trigger_line = ''
    trigger_method = ''
    result_line = ''
    popup_button_seen = $false
    popup_button_enabled = $false
    main_visible = $false
    status_found = $false
    source_match = $false
    translation_visible = $false
    popup_gone = $false
    history_unchanged = $false
    no_retranslate_after_click = $false
    history_before = $null
    history_after = $null
    input_value = ''
    expected_translation = ''
    translation_words = @()
    hyperlink_names = @()
    text_names = @()
    missing_translation_words = @()
    log_after_click = @()
    checks = [ordered]@{}
    cleanup = [ordered]@{}
    error = ''
}

$resultDir = Split-Path -Parent $ResultPath
if ($resultDir) { New-Item -ItemType Directory -Force -Path $resultDir | Out-Null }
$traceDir = Split-Path -Parent $TracePath
if ($traceDir) { New-Item -ItemType Directory -Force -Path $traceDir | Out-Null }
Set-Content -LiteralPath $TracePath -Value ((Get-Date).ToString('HH:mm:ss.fff') + ' start') -Encoding UTF8
if (Test-Path -LiteralPath $ResultPath) {
    Remove-Item -LiteralPath $ResultPath -Force -ErrorAction SilentlyContinue
}

function Trace([string]$Message) {
    Add-Content -LiteralPath $TracePath -Value ((Get-Date).ToString('HH:mm:ss.fff') + ' ' + $Message) -Encoding UTF8
}

function Get-QalamStoreDir {
    $dataDir = Join-Path $env:LOCALAPPDATA 'Qalam'
    try {
        $configPath = Join-Path $dataDir 'config.json'
        if (Test-Path -LiteralPath $configPath) {
            $config = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
            $syncFolder = [string]$config.SyncFolder
            if ($syncFolder -and (Test-Path -LiteralPath $syncFolder -PathType Container)) {
                return $syncFolder
            }
        }
    }
    catch { }
    return $dataDir
}

function Read-QalamConfig {
    $configPath = Join-Path $env:LOCALAPPDATA 'Qalam\config.json'
    if (-not (Test-Path -LiteralPath $configPath)) { return $null }
    try { return Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json }
    catch { return $null }
}

function Get-HistorySnapshot([string]$HistoryPath, [string]$Tag) {
    $snapshot = [ordered]@{
        path = $HistoryPath
        exists = $false
        hash = 'MISSING'
        entries = 0
        token_entries = 0
        translation = ''
        error = ''
    }
    if (-not (Test-Path -LiteralPath $HistoryPath)) { return $snapshot }
    $snapshot.exists = $true
    try {
        $snapshot.hash = (Get-FileHash -LiteralPath $HistoryPath -Algorithm SHA256).Hash
        $raw = Get-Content -LiteralPath $HistoryPath -Raw -Encoding UTF8
        $entries = $raw | ConvertFrom-Json
        $snapshot.entries = @($entries).Count
        $tokenEntries = @($entries | Where-Object {
            $_.source -and ([string]$_.source).IndexOf($Tag, [StringComparison]::Ordinal) -ge 0
        })
        $snapshot.token_entries = $tokenEntries.Count
        if ($tokenEntries.Count -gt 0) {
            $snapshot.translation = [string]$tokenEntries[0].translation
        }
    }
    catch {
        $snapshot.error = $_.Exception.Message
    }
    return $snapshot
}

function Find-ViewInAppButton([int]$ProcessId) {
    # Search only Qalam's top-level windows. A root Descendants search makes
    # Windows UI Automation walk every open application and can hang while the
    # compact popup is still laying itself out.
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, 'View in App')
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $windows = $root.FindAll(
        [System.Windows.Automation.TreeScope]::Children,
        [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($window in $windows) {
        try {
            if ($window.Current.ProcessId -ne $ProcessId) { continue }
            if ($window.Current.IsOffscreen) { continue }
            $button = $window.FindFirst(
                [System.Windows.Automation.TreeScope]::Descendants, $condition)
            if ($null -ne $button) { return $button }
        }
        catch { }
    }
    return $null
}

function Find-MainWindow([int]$ProcessId) {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $windows = $root.FindAll(
        [System.Windows.Automation.TreeScope]::Children,
        [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($window in $windows) {
        try {
            if ($window.Current.ProcessId -eq $ProcessId -and
                $window.Current.Name -like 'Qalam*Translation*' -and
                -not $window.Current.IsOffscreen) {
                return $window
            }
        }
        catch { }
    }
    return $null
}

function Find-StatusElement($MainWindow) {
    if ($null -eq $MainWindow) { return $null }
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $statusText)
    return $MainWindow.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

function Get-InputValue($MainWindow) {
    if ($null -eq $MainWindow) { return '' }
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Edit)
    $edit = $MainWindow.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
    if ($null -eq $edit) { return '' }
    try {
        $pattern = $edit.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
        return [string]$pattern.Current.Value
    }
    catch {
        try {
            $pattern = $edit.GetCurrentPattern([System.Windows.Automation.TextPattern]::Pattern)
            return [string]$pattern.DocumentRange.GetText(-1)
        }
        catch { return '' }
    }
}

function Get-HyperlinkNames($MainWindow) {
    $names = @()
    if ($null -eq $MainWindow) { return $names }
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Hyperlink)
    $links = $MainWindow.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
    foreach ($link in $links) {
        try {
            $name = [string]$link.Current.Name
            if (-not [string]::IsNullOrWhiteSpace($name)) { $names += $name }
        }
        catch { }
    }
    return $names
}

function Get-TextNames($MainWindow) {
    $names = @()
    if ($null -eq $MainWindow) { return $names }
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Text)
    $texts = $MainWindow.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
    foreach ($text in $texts) {
        try {
            $name = [string]$text.Current.Name
            if (-not [string]::IsNullOrWhiteSpace($name)) { $names += $name }
        }
        catch { }
    }
    return $names
}

function Normalize-SmokeText([string]$Text) {
    return (($Text -replace '\s+', ' ').Trim())
}

try {
    Trace 'validate inputs'
    if (-not (Test-Path -LiteralPath $AppPath -PathType Leaf)) {
        throw "Qalam executable was not found: $AppPath"
    }
    if (-not (Test-Path -LiteralPath $sourceScript -PathType Leaf)) {
        throw "Smoke source script was not found: $sourceScript"
    }
    if (@(Get-Process Qalam -ErrorAction SilentlyContinue).Count -gt 0) {
        throw 'Qalam is already running; close it before running this test so an existing user session is not touched.'
    }

    $config = Read-QalamConfig
    if ($null -ne $config) {
        $details.config_target = [string]$config.Target
        $details.config_auto_target = [bool]$config.AutoTarget
        if ($ExpectedPair -eq 'ar->en' -and -not [bool]$config.AutoTarget -and [string]$config.Target -ne 'en') {
            throw ("Qalam target is '$($config.Target)'; set Target to 'en' or enable AutoTarget before this Arabic-to-English smoke test.")
        }
    }

    $storeDir = Get-QalamStoreDir
    $historyPath = Join-Path $storeDir 'history.json'
    $details.store_dir = $storeDir
    $historyExisted = Test-Path -LiteralPath $historyPath
    if ($historyExisted) {
        Copy-Item -LiteralPath $historyPath -Destination $historyBackup -Force -ErrorAction Stop
    }
    Trace ('history backed up: ' + $historyExisted)

    try {
        $savedClipboard = [System.Windows.Forms.Clipboard]::GetDataObject()
        $clipboardSaved = $true
    }
    catch { }
    if ($clipboardSaved) {
        try { [System.Windows.Forms.Clipboard]::SetText('qalam-view-in-app-placeholder-' + $Token) } catch { }
    }
    Trace ('clipboard saved: ' + $clipboardSaved)

    $details.app_version = [Reflection.AssemblyName]::GetAssemblyName($AppPath).Version.ToString()
    $details.app_sha256 = (Get-FileHash -LiteralPath $AppPath -Algorithm SHA256).Hash
    $appDir = Split-Path -Parent $AppPath
    $logStart = Get-QalamLogCount

    Trace 'start qalam'
    $qalam = Start-Process -FilePath $AppPath -ArgumentList '--background' -WorkingDirectory $appDir -PassThru
    $deadline = (Get-Date).AddSeconds(20)
    $ready = $false
    while ((Get-Date) -lt $deadline) {
        if ($qalam.HasExited) { throw ('Qalam exited during startup with code ' + $qalam.ExitCode + '.') }
        $newLines = Get-QalamNewLog $logStart
        if (@($newLines | Where-Object { $_ -match 'Qalam started; hook=True' }).Count -gt 0) {
            $ready = $true
            break
        }
        Start-Sleep -Milliseconds 250
    }
    if (-not $ready) { throw 'Qalam did not report a ready keyboard hook during startup.' }
    Trace 'qalam ready'

    Trace 'start source window'
    $source = Start-Process -FilePath 'powershell.exe' -ArgumentList @(
        '-NoProfile', '-STA', '-ExecutionPolicy', 'Bypass', '-WindowStyle', 'Hidden',
        '-File', $sourceScript, '-Token', $Token
    ) -PassThru

    $deadline = (Get-Date).AddSeconds(60)
    $triggerLine = ''
    $resultLine = ''
    $resultPattern = 'result ' + [Regex]::Escape($ExpectedPair) + ' ok'
    while ((Get-Date) -lt $deadline) {
        $newLines = Get-QalamNewLog $logStart
        $triggerMatches = @($newLines | Where-Object { $_ -match 'trigger accepted' })
        $resultMatches = @($newLines | Where-Object { $_ -match $resultPattern })
        if ($triggerMatches.Count -gt 0) { $triggerLine = [string]$triggerMatches[0] }
        if ($resultMatches.Count -gt 0) { $resultLine = [string]$resultMatches[0] }
        if ($triggerLine -and $resultLine) { break }
        if ($source.HasExited -and -not $triggerLine) {
            throw 'The source window exited before Qalam accepted a selection.'
        }
        Start-Sleep -Milliseconds 250
    }
    $details.trigger_line = $triggerLine
    $details.result_line = $resultLine
    if (-not $triggerLine) { throw 'Qalam did not accept the smoke selection.' }
    if ($triggerLine -match '\((uia|clipboard)\)') { $details.trigger_method = $Matches[1] }
    Trace ('trigger accepted; method=' + $details.trigger_method)
    if (-not $resultLine) { throw ('Qalam did not complete the expected ' + $ExpectedPair + ' translation.') }
    Trace 'translation complete'

    Trace 'wait popup button'
    $deadline = (Get-Date).AddSeconds(75)
    $popupButton = $null
    while ((Get-Date) -lt $deadline) {
        $candidate = Find-ViewInAppButton $qalam.Id
        if ($null -ne $candidate) {
            $details.popup_button_seen = $true
            try { $details.popup_button_enabled = [bool]$candidate.Current.IsEnabled } catch { }
            if ($details.popup_button_enabled) {
                try {
                    if (-not $candidate.Current.IsOffscreen) { $popupButton = $candidate; break }
                }
                catch { }
            }
        }
        if ($source.HasExited -and -not $details.popup_button_seen) {
            throw 'The source window exited before the View in App button appeared.'
        }
        Start-Sleep -Milliseconds 500
    }
    if ($null -eq $popupButton) {
        $tail = @(Get-QalamNewLog $logStart | Select-Object -Last 15)
        throw ('View in App did not become enabled. Recent log: ' + ($tail -join ' | '))
    }
    Trace 'popup button enabled'

    Trace 'wait history entry'
    $deadline = (Get-Date).AddSeconds(10)
    $historyBefore = Get-HistorySnapshot $historyPath $Token
    while ((Get-Date) -lt $deadline -and $historyBefore.token_entries -eq 0) {
        Start-Sleep -Milliseconds 250
        $historyBefore = Get-HistorySnapshot $historyPath $Token
    }
    $details.history_before = $historyBefore
    if ($historyBefore.token_entries -eq 0) {
        throw 'The popup result was not added to history before the handoff.'
    }
    $details.expected_translation = [string]$historyBefore.translation
    $details.translation_words = @($details.expected_translation -split '\s+' | Where-Object { $_.Length -gt 0 })
    if (-not $details.expected_translation) {
        throw 'The history entry had no translation text to verify.'
    }

    $logBeforeClick = Get-QalamLogCount
    Trace 'click view in app'
    $invoke = $popupButton.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $invoke.Invoke()
    Trace 'click returned'

    Trace 'wait main window'
    $main = $null
    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $deadline) {
        $main = Find-MainWindow $qalam.Id
        if ($null -ne $main) { break }
        Start-Sleep -Milliseconds 250
    }
    if ($null -eq $main) { throw 'The main Qalam window did not appear after View in App.' }
    $details.main_visible = $true
    Trace 'main window visible'

    Trace 'read main state'
    $deadline = (Get-Date).AddSeconds(10)
    do {
        $statusElement = Find-StatusElement $main
        $inputValue = Get-InputValue $main
        $hyperlinkNames = @(Get-HyperlinkNames $main)
        $textNames = @(Get-TextNames $main)
        $missingWords = @($details.translation_words | Where-Object { $hyperlinkNames -notcontains $_ })
        $normalizedExpected = Normalize-SmokeText $details.expected_translation
        $joinedLinks = Normalize-SmokeText ($hyperlinkNames -join ' ')
        $joinedText = Normalize-SmokeText ($textNames -join ' ')
        $details.status_found = $null -ne $statusElement
        $details.input_value = $inputValue
        $details.source_match = ($inputValue.Trim() -eq $sourceText.Trim())
        $details.hyperlink_names = $hyperlinkNames
        $details.text_names = @($textNames | Select-Object -First 60)
        $details.missing_translation_words = $missingWords
        $details.translation_visible = $details.translation_words.Count -gt 0 -and (
            $missingWords.Count -eq 0 -or
            $joinedLinks.IndexOf($normalizedExpected, [StringComparison]::Ordinal) -ge 0 -or
            $joinedText.IndexOf($normalizedExpected, [StringComparison]::Ordinal) -ge 0)
        if ($details.status_found -and $details.source_match -and $details.translation_visible) { break }
        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)

    Trace ('main state status=' + $details.status_found + ' source=' + $details.source_match + ' translation=' + $details.translation_visible)
    Trace ('expected=[' + $details.expected_translation + '] links=[' + ($details.hyperlink_names -join '|') + '] missing=[' + ($details.missing_translation_words -join '|') + ']')

    Trace 'read history after'
    $historyAfter = Get-HistorySnapshot $historyPath $Token
    $details.history_after = $historyAfter
    $details.history_unchanged = ($historyBefore.hash -eq $historyAfter.hash) -and
        ($historyBefore.entries -eq $historyAfter.entries) -and
        ($historyBefore.token_entries -eq $historyAfter.token_entries)

    Trace 'check popup gone'
    $buttonAfter = Find-ViewInAppButton $qalam.Id
    if ($null -eq $buttonAfter) {
        $details.popup_gone = $true
    }
    else {
        try { $details.popup_gone = [bool]$buttonAfter.Current.IsOffscreen } catch { $details.popup_gone = $true }
    }

    $logAfterClick = @(Get-QalamNewLog $logBeforeClick)
    $details.log_after_click = @($logAfterClick | Select-Object -Last 20 | ForEach-Object { [string]$_ })
    $details.no_retranslate_after_click = @($logAfterClick | Where-Object {
        $_ -match 'translate:|result '
    }).Count -eq 0

    $details.checks = [ordered]@{
        trigger_accepted = [bool]$details.trigger_line
        result_ok = [bool]$details.result_line
        popup_button_seen = $details.popup_button_seen
        popup_button_enabled = $details.popup_button_enabled
        main_visible = $details.main_visible
        status_found = $details.status_found
        source_match = $details.source_match
        translation_visible = $details.translation_visible
        popup_gone = $details.popup_gone
        history_unchanged = $details.history_unchanged
        no_retranslate_after_click = $details.no_retranslate_after_click
    }
    $failed = @($details.checks.Keys | Where-Object { -not $details.checks[$_] })
    if ($failed.Count -gt 0) {
        throw ('Smoke checks failed: ' + ($failed -join ', '))
    }
    $status = 'pass'
    Trace 'pass'
}
catch {
    $details.error = $_.Exception.Message
    Trace ('fail: ' + $details.error)
    try { $details.log_tail = @(Get-QalamNewLog ([int]$logStart) | Select-Object -Last 30 | ForEach-Object { [string]$_ }) } catch { }
}
finally {
    Trace 'cleanup start'
    $cleanupErrors = @()
    if ($null -ne $source -and -not $source.HasExited) {
        try { Stop-QalamProcessTree $source.Id } catch { $cleanupErrors += ('source: ' + $_.Exception.Message) }
    }
    if ($null -ne $qalam -and -not $qalam.HasExited) {
        try { Stop-QalamProcessTree $qalam.Id } catch { $cleanupErrors += ('qalam: ' + $_.Exception.Message) }
    }
    Start-Sleep -Milliseconds 800

    if ($historyExisted) {
        try { Copy-Item -LiteralPath $historyBackup -Destination $historyPath -Force -ErrorAction Stop }
        catch { $cleanupErrors += ('history restore: ' + $_.Exception.Message) }
    }
    elseif (Test-Path -LiteralPath $historyPath) {
        try { Remove-Item -LiteralPath $historyPath -Force -ErrorAction Stop }
        catch { $cleanupErrors += ('history removal: ' + $_.Exception.Message) }
    }
    Remove-Item -LiteralPath $historyBackup -Force -ErrorAction SilentlyContinue

    if ($clipboardSaved) {
        if ($null -ne $savedClipboard) {
            try { [System.Windows.Forms.Clipboard]::SetDataObject($savedClipboard, $true) }
            catch { $cleanupErrors += ('clipboard restore: ' + $_.Exception.Message) }
        }
        else {
            try { [System.Windows.Forms.Clipboard]::Clear() }
            catch { $cleanupErrors += ('clipboard clear: ' + $_.Exception.Message) }
        }
    }

    $details.cleanup = [ordered]@{
        source_stopped = ($null -eq $source -or $source.HasExited)
        qalam_stopped = ($null -eq $qalam -or $qalam.HasExited)
        history_backup_created = $historyExisted
        clipboard_saved = $clipboardSaved
        errors = $cleanupErrors
    }
    Trace 'cleanup complete'

    try {
        Trace 'result serialize start'
        $resultDetails = [ordered]@{
            token = $details.token
            source = $details.source
            expected_pair = $details.expected_pair
            app = $details.app
            app_version = $details.app_version
            app_sha256 = $details.app_sha256
            config_target = $details.config_target
            config_auto_target = $details.config_auto_target
            store_dir = $details.store_dir
            started = $details.started
            trigger_line = $details.trigger_line
            trigger_method = $details.trigger_method
            result_line = $details.result_line
            popup_button_seen = $details.popup_button_seen
            popup_button_enabled = $details.popup_button_enabled
            main_visible = $details.main_visible
            status_found = $details.status_found
            source_match = $details.source_match
            translation_visible = $details.translation_visible
            popup_gone = $details.popup_gone
            history_unchanged = $details.history_unchanged
            no_retranslate_after_click = $details.no_retranslate_after_click
            history_before_hash = [string]$details.history_before.hash
            history_before_entries = $details.history_before.entries
            history_before_token_entries = $details.history_before.token_entries
            history_after_hash = [string]$details.history_after.hash
            history_after_entries = $details.history_after.entries
            history_after_token_entries = $details.history_after.token_entries
            input_value = $details.input_value
            expected_translation = $details.expected_translation
            translation_words = @($details.translation_words)
            hyperlink_names = @($details.hyperlink_names)
            text_names = @($details.text_names)
            missing_translation_words = @($details.missing_translation_words)
            log_after_click = @($details.log_after_click)
            checks = $details.checks
            cleanup = $details.cleanup
            error = $details.error
            log_tail = @($details.log_tail)
        }
        $payload = [ordered]@{
            app = 'view-in-app'
            status = $status
            timestamp = (Get-Date).ToString('o')
            details = $resultDetails
        }
        $json = $payload | ConvertTo-Json -Depth 6
        Trace 'result serialize done'
        [IO.File]::WriteAllText($ResultPath, $json, (New-Object System.Text.UTF8Encoding($false)))
        Trace ('result written: ' + $status)
    }
    catch {
        Trace ('result write failed: ' + $_.Exception.Message)
        Write-Host ('RESULT WRITE FAILED: ' + $_.Exception.Message)
    }
}

if ($status -eq 'pass') {
    Write-Host ('PASS view-in-app ' + $AppPath)
    Write-Host ('RESULT ' + $ResultPath)
    exit 0
}
Write-Host ('FAIL view-in-app ' + $AppPath)
Write-Host ('ERROR ' + $details.error)
Write-Host ('RESULT ' + $ResultPath)
exit 1
