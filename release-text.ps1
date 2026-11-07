# Shared guards for release notes and update manifests.
# Keep this file ASCII-only: Windows PowerShell 5.1 reads a BOM-less script in
# the system code page, so literal non-ASCII source is itself a corruption risk.

function Get-QalamComparableText {
    param([string]$Text)
    if ($null -eq $Text) { return '' }
    return (($Text -replace "`r`n", "`n").Trim())
}

function Assert-QalamNoCjkMojibake {
    param(
        [string]$Text,
        [string]$Label = 'Release notes'
    )

    # A UTF-8 CJK or Hangul fragment decoded as Windows-1252 can be recovered by
    # encoding its non-ASCII run back to CP1252 and decoding the bytes as strict
    # UTF-8. Requiring at least two recovered CJK/Hangul characters avoids
    # treating valid accented Latin punctuation (for example eszett followed by
    # an ellipsis) as corruption. Scan runs so valid text elsewhere cannot mask it.
    $cp1252 = [Text.Encoding]::GetEncoding(1252, [Text.EncoderFallback]::ExceptionFallback, [Text.DecoderFallback]::ExceptionFallback)
    $cp1252Chars = New-Object 'System.Collections.Generic.HashSet[char]'
    for ($b = 0; $b -lt 256; $b++) {
        $one = New-Object byte[] 1
        $one[0] = [byte]$b
        try {
            $s = $cp1252.GetString($one)
            if ($s.Length -eq 1) { [void]$cp1252Chars.Add($s[0]) }
        }
        catch { }
    }

    $strict = New-Object System.Text.UTF8Encoding($false, $true)
    $i = 0
    while ($i -lt $Text.Length) {
        $start = $i
        while ($i -lt $Text.Length) {
            $c = $Text[$i]
            if ([int]$c -le 0x7F -or [char]::IsSurrogate($c) -or -not $cp1252Chars.Contains($c)) { break }
            $i++
        }
        if (($i - $start) -ge 3) {
            $run = $Text.Substring($start, $i - $start)
            $decoded = $null
            try {
                $decoded = $strict.GetString($cp1252.GetBytes($run))
            }
            catch { }
            if ($null -ne $decoded -and -not [string]::Equals($run, $decoded, [StringComparison]::Ordinal)) {
                $cjkCount = 0
                for ($j = 0; $j -lt $decoded.Length; $j++) {
                    $code = [int]$decoded[$j]
                    if (($code -ge 0x3040 -and $code -le 0x30FF) -or
                        ($code -ge 0x3400 -and $code -le 0x4DBF) -or
                        ($code -ge 0x4E00 -and $code -le 0x9FFF) -or
                        ($code -ge 0xAC00 -and $code -le 0xD7AF) -or
                        ($code -ge 0xF900 -and $code -le 0xFAFF)) {
                        $cjkCount++
                    }
                }
                if ($cjkCount -ge 2) {
                    $code = [int][char]$run[0]
                    throw ($Label + ' contains mangled non-ASCII text near U+' + $code.ToString('X4') + '. Pass the notes as UTF-8 with -NotesFile.')
                }
            }
        }
        if ($i -eq $start) { $i++ }
    }
}

function Assert-QalamReleaseText {
    param(
        [string]$Text,
        [string]$Label = 'Release notes',
        [switch]$AllowEmpty
    )

    if ([string]::IsNullOrWhiteSpace($Text)) {
        if ($AllowEmpty) { return }
        throw ($Label + ' is empty.')
    }

    if ($Text.IndexOf([char]0xFFFD) -ge 0) {
        throw ($Label + ' contains U+FFFD, the replacement character written when text was decoded lossily. Pass the notes as UTF-8 with -NotesFile.')
    }

    $strict = New-Object System.Text.UTF8Encoding($false, $true)
    try {
        $roundTrip = $strict.GetString($strict.GetBytes($Text))
    }
    catch {
        throw ($Label + ' contains an invalid Unicode sequence. Pass the notes as UTF-8 with -NotesFile.')
    }
    if (-not [string]::Equals($Text, $roundTrip, [StringComparison]::Ordinal)) {
        throw ($Label + ' does not survive a UTF-8 round trip. Pass the notes as UTF-8 with -NotesFile.')
    }

    for ($i = 0; $i -lt $Text.Length; $i++) {
        $c = $Text[$i]
        $code = [int]$c
        if ([char]::IsHighSurrogate($c)) {
            if ($i + 1 -ge $Text.Length -or -not [char]::IsLowSurrogate($Text[$i + 1])) {
                throw ($Label + ' contains an unpaired surrogate. Pass the notes as UTF-8 with -NotesFile.')
            }
            $i++
            continue
        }
        if ([char]::IsLowSurrogate($c)) {
            throw ($Label + ' contains an unpaired surrogate. Pass the notes as UTF-8 with -NotesFile.')
        }
        if (($code -lt 0x20 -and $code -ne 9 -and $code -ne 10 -and $code -ne 13) -or
            $code -eq 0x7F -or ($code -ge 0x80 -and $code -le 0x9F)) {
            throw ($Label + ' contains control character U+' + $code.ToString('X4') + '.')
        }
    }

    # These are the high-confidence signatures of UTF-8 bytes being decoded as
    # Latin-1/Windows-1252: Arabic, accented Latin, smart punctuation, emoji and
    # CJK text all produce recognizable leading-byte pairs.
    $patterns = @(
        '\u00EF\u00BF\u00BD',
        '[\u00C3\u00C2\u00D8\u00D9\u00D0\u00D1][\u0080-\u00BF]',
        '[\u0637\u0638][\u0080-\u00BF]',
        '\u00E2\u20AC[\u0080-\u00BF\u2018-\u201D]?',
        '\u00F0\u0178',
        '[\u00E4\u00E5\u00E6\u00E7][\u0080-\u00BF]'
    )
    foreach ($pattern in $patterns) {
        $match = [regex]::Match($Text, $pattern)
        if ($match.Success) {
            $code = [int][char]$match.Value[0]
            throw ($Label + ' contains mangled non-ASCII text near U+' + $code.ToString('X4') + '. Pass the notes as UTF-8 with -NotesFile.')
        }
    }

    Assert-QalamNoCjkMojibake -Text $Text -Label $Label
}

function Read-QalamUtf8File {
    param(
        [string]$Path,
        [string]$Label = 'Text file'
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw ($Label + ' path is empty.')
    }
    $full = [IO.Path]::GetFullPath($Path)
    if (-not [IO.File]::Exists($full)) {
        throw ($Label + ' file was not found: ' + $full)
    }

    $strict = New-Object System.Text.UTF8Encoding($false, $true)
    try {
        $text = $strict.GetString([IO.File]::ReadAllBytes($full))
    }
    catch {
        throw ($Label + ' is not valid UTF-8: ' + $full)
    }
    if ($text.Length -gt 0 -and [int]$text[0] -eq 0xFEFF) {
        $text = $text.Substring(1)
    }
    Assert-QalamReleaseText -Text $text -Label $Label
    return $text
}

function Assert-QalamManifestNotes {
    param(
        [string]$Path,
        [string]$Expected,
        [string]$Label = 'Update manifest'
    )

    $text = Read-QalamUtf8File -Path $Path -Label $Label
    try {
        $manifest = $text | ConvertFrom-Json
    }
    catch {
        throw ($Label + ' is not valid JSON: ' + $Path)
    }
    if ($null -eq $manifest) {
        throw ($Label + ' is empty: ' + $Path)
    }

    $notes = [string]$manifest.notes
    Assert-QalamReleaseText -Text $notes -Label ($Label + ' notes') -AllowEmpty
    if ((Get-QalamComparableText $notes) -cne (Get-QalamComparableText $Expected)) {
        throw ($Label + ' notes do not match the supplied release notes. Refusing to publish mangled or stale text.')
    }
    return $manifest
}
