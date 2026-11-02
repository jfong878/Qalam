$ErrorActionPreference = 'Stop'

function Patch-File([string]$relative, [hashtable]$replacements) {
    $path = Join-Path $PSScriptRoot $relative
    $text = [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
    foreach ($old in $replacements.Keys) {
        $count = ([regex]::Matches($text, [regex]::Escape($old))).Count
        if ($count -ne 1) { throw "$relative expected one replacement, found $count" }
        $text = $text.Replace($old, $replacements[$old])
    }
    [IO.File]::WriteAllText($path, $text, (New-Object Text.UTF8Encoding($false)))
    Write-Host "patched $relative"
}

$char = @{}
$char['            Background = Ui.MakeBrush(0xFF, 0x0E, 0x12, 0x18);'] = '            Background = Ui.Window;'
$char['            searchBorder.Background = Ui.MakeBrush(0xFF, 0x15, 0x1A, 0x22);'] = '            searchBorder.Background = Ui.Input;'
$char['            searchBorder.BorderBrush = Ui.MakeBrush(0xFF, 0x2A, 0x34, 0x42);'] = '            searchBorder.BorderBrush = Ui.InputBorder;'
$char['            charBox.Background = Ui.MakeBrush(0xFF, 0x0B, 0x0F, 0x16);'] = '            charBox.Background = Ui.Deep;'
$char['            if (row != null) row.Background = Ui.MakeBrush(0x1F, 0xD9, 0xB9, 0x6A);'] = '            if (row != null) row.Background = Ui.SelectedRow;'
$char['            canvas.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x0E, 0x12, 0x1A));'] = '            canvas.Background = Ui.Canvas;'
Patch-File 'src\CharacterWindow.cs' $char

$study = @{}
$study['            Background = Ui.MakeBrush(0xFF, 0x0E, 0x12, 0x18);'] = '            Background = Ui.Window;'
Patch-File 'src\StudyWindow.cs' $study

$doc = @{}
$doc['            root.Background = Ui.MakeBrush(0xFF, 0x14, 0x18, 0x20);'] = '            root.Background = Ui.Menu;'
$doc['            root.BorderBrush = Ui.MakeBrush(0x33, 0xD9, 0xB9, 0x6A);'] = '            root.BorderBrush = Ui.Outline;'
Patch-File 'src\DocumentWindow.cs' $doc

$chars = @{}
$chars['                else path.Fill = Ui.MakeBrush(0x1B, 0xF2, 0xF4, 0xF8);'] = '                else path.Fill = Ui.StrokeHint;'
Patch-File 'src\Characters.cs' $chars

$build = @{}
$build["    (Join-Path `$root 'src\Qalam.cs')"] = "    (Join-Path `$root 'src\Qalam.cs')`r`n    (Join-Path `$root 'src\Theme.cs')"
Patch-File 'build.ps1' $build
