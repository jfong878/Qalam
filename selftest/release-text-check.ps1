$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
. (Join-Path $root 'release-text.ps1')
$utf8 = New-Object System.Text.UTF8Encoding($false)
$cp1252 = [Text.Encoding]::GetEncoding(1252)

$failures = @()
function Expect-Success([string]$Name, [scriptblock]$Action) {
    try {
        & $Action
        Write-Host ('PASS ' + $Name)
    }
    catch {
        $script:failures += ($Name + ': ' + $_.Exception.Message)
        Write-Host ('FAIL ' + $Name)
    }
}
function Expect-Failure([string]$Name, [scriptblock]$Action) {
    try {
        & $Action
        $script:failures += ($Name + ': expected the guard to reject this text')
        Write-Host ('FAIL ' + $Name)
    }
    catch {
        Write-Host ('PASS ' + $Name)
    }
}

$arabic = [string]::Join('', @([char]0x0637, [char]0x064A, [char]0x0628))
$chinese = [string]::Join('', @([char]0x4E2D, [char]0x6587))
$korean = [string]::Join('', @([char]0xC548, [char]0xB155))
$accented = 'Caf' + [char]0x00E9
$validPunctuation = 'Caf' + [char]0x00E9 + ' ' + [char]0x2014 + ' ' + [char]0x201C + 'quoted' + [char]0x201D
$validLatinPunctuation = @(
    ('gro' + [char]0x00DF + [char]0x2026),
    ([char]0x00D8 + [char]0x2026),
    ([char]0x00C5 + [char]0x2026),
    ([char]0x00D1 + [char]0x2026),
    ('Caf' + [char]0x00E9 + [char]0x2014 + [char]0x2026),
    ([char]0x00DE + [char]0x2026)
) -join ' '
$emoji = [string]::Join('', @([char]0xD83D, [char]0xDE80))
$valid = 'Release ' + $arabic + ' ' + $chinese + ' ' + $korean + ' ' + $accented + ' ' + $emoji + "`r`nSecond line"
$mojibakeArabic = 'bad ' + [char]0x00D8 + [char]0x00A3
$mojibakeArabic1256 = 'bad ' + [string]::Join('', @([char]0x0637, [char]0x00B7, [char]0x0638, [char]0x0679, [char]0x0637, [char]0x00A8))
$mojibakeLatin = 'bad ' + [char]0x00C3 + [char]0x00A9
$mojibakeKorean = 'bad ' + $cp1252.GetString($utf8.GetBytes($korean))
$mojibakeCjk = 'bad ' + $cp1252.GetString($utf8.GetBytes([string]::Join('', @([char]0x6587, [char]0x6587))))
$mojibakeEmoji = 'bad ' + $cp1252.GetString($utf8.GetBytes($emoji))
$replacement = 'bad ' + [char]0xFFFD
$control = 'bad ' + [char]0x0001
$loneSurrogate = 'bad ' + [char]0xD800

Expect-Success 'valid non-ASCII release notes' { Assert-QalamReleaseText -Text $valid -Label 'Test notes' }
Expect-Success 'valid accented text with punctuation' { Assert-QalamReleaseText -Text $validPunctuation -Label 'Test notes' }
Expect-Success 'valid Latin letters before punctuation' { Assert-QalamReleaseText -Text $validLatinPunctuation -Label 'Test notes' }
Expect-Failure 'replacement character' { Assert-QalamReleaseText -Text $replacement -Label 'Test notes' }
Expect-Failure 'Arabic mojibake' { Assert-QalamReleaseText -Text $mojibakeArabic -Label 'Test notes' }
Expect-Failure 'Arabic Windows-1256 mojibake' { Assert-QalamReleaseText -Text $mojibakeArabic1256 -Label 'Test notes' }
Expect-Failure 'Latin mojibake' { Assert-QalamReleaseText -Text $mojibakeLatin -Label 'Test notes' }
Expect-Failure 'Korean mojibake' { Assert-QalamReleaseText -Text $mojibakeKorean -Label 'Test notes' }
Expect-Failure 'CJK mojibake without control byte' { Assert-QalamReleaseText -Text $mojibakeCjk -Label 'Test notes' }
Expect-Failure 'emoji mojibake' { Assert-QalamReleaseText -Text $mojibakeEmoji -Label 'Test notes' }
Expect-Failure 'control character' { Assert-QalamReleaseText -Text $control -Label 'Test notes' }
Expect-Failure 'unpaired surrogate' { Assert-QalamReleaseText -Text $loneSurrogate -Label 'Test notes' }

$temp = Join-Path ([IO.Path]::GetTempPath()) ('qalam-release-text-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $temp | Out-Null
try {
    $goodJson = Join-Path $temp 'good.json'
    $badJson = Join-Path $temp 'bad.json'
    $staleJson = Join-Path $temp 'stale.json'
    $goodText = Join-Path $temp 'good.txt'
    $badBytes = Join-Path $temp 'bad-utf8.txt'
    [IO.File]::WriteAllText($goodJson, ([ordered]@{ version = '1.0.0'; notes = $valid } | ConvertTo-Json), $utf8)
    [IO.File]::WriteAllText($badJson, ([ordered]@{ version = '1.0.0'; notes = $mojibakeArabic } | ConvertTo-Json), $utf8)
    [IO.File]::WriteAllText($staleJson, ([ordered]@{ version = '1.0.0'; notes = ($valid + ' (stale)') } | ConvertTo-Json), $utf8)
    [IO.File]::WriteAllText($goodText, $valid, $utf8)
    [IO.File]::WriteAllBytes($badBytes, [byte[]](0xC3, 0x28))

    Expect-Success 'valid UTF-8 notes file' {
        $null = Read-QalamUtf8File -Path $goodText -Label 'Test notes'
    }
    Expect-Success 'valid update manifest' {
        $null = Assert-QalamManifestNotes -Path $goodJson -Expected $valid -Label 'Test manifest'
    }
    Expect-Failure 'mangled update manifest' {
        $null = Assert-QalamManifestNotes -Path $badJson -Expected $valid -Label 'Test manifest'
    }
    Expect-Failure 'stale update manifest' {
        $null = Assert-QalamManifestNotes -Path $staleJson -Expected $valid -Label 'Test manifest'
    }
    Expect-Failure 'invalid UTF-8 file' {
        $null = Read-QalamUtf8File -Path $badBytes -Label 'Test notes'
    }
}
finally {
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Host ('ERROR ' + $_) }
    exit 1
}
Write-Host 'release-text-check=PASS'
