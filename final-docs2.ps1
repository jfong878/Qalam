$ErrorActionPreference = 'Stop'

$path = Join-Path $PSScriptRoot 'README.md'
$text = [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)

$listen = '- **Listen** buttons for pronunciation and copy buttons for the text.'
if ($text.IndexOf($listen, [StringComparison]::Ordinal) -ge 0 -and $text.IndexOf('Improve wording', [StringComparison]::OrdinalIgnoreCase) -lt 0) {
    $replacement = $listen + "`n   - **Improve wording** in the full window and browser client, using the optional`n     local Ollama model and the current translation as context for a more natural rewrite."
    $text = $text.Replace($listen, $replacement)
}

$windowMarker = 'choosing **Open Qalam**, opens the full window.'
if ($text.IndexOf($windowMarker, [StringComparison]::Ordinal) -ge 0 -and $text.IndexOf('theme selector', [StringComparison]::OrdinalIgnoreCase) -lt 0) {
    $text = $text.Replace($windowMarker, $windowMarker + "`n`nThe full window has a **System / Light / Sepia / Dark** theme menu. System follows`nthe Windows app theme; the browser client follows the device preference.")
}

$apiMarker = '`/api/translate`, `/api/corpus`, `/api/study`, `/api/study/add`,'
if ($text.IndexOf($apiMarker, [StringComparison]::Ordinal) -ge 0) {
    $text = $text.Replace($apiMarker, '`/api/translate`, `/api/rewrite`, `/api/corpus`, `/api/study`, `/api/study/add`,')
}

$mobileMarker = 'The page uses a search-first **Translate**, **Study**, and **Context** layout.'
if ($text.IndexOf($mobileMarker, [StringComparison]::Ordinal) -ge 0 -and $text.IndexOf('theme selector', [StringComparison]::OrdinalIgnoreCase) -lt 0) {
    $text = $text.Replace($mobileMarker,
        'The page has a **System / Light / Sepia / Dark** selector; System follows the device`nprefers-color-scheme and the choice is remembered locally. It also has **Improve wording**,`nwhich asks the local Ollama model for a more natural input and can apply the suggestion.`nThe page uses a search-first **Translate**, **Study**, and **Context** layout.')
}

$ollamaMarker = '- Optional: a running [Ollama](https://ollama.com) server for colloquial translation'
if ($text.IndexOf($ollamaMarker, [StringComparison]::Ordinal) -ge 0 -and $text.IndexOf('Improve wording**', [StringComparison]::OrdinalIgnoreCase) -lt 0) {
    $text = $text.Replace($ollamaMarker, $ollamaMarker + "`n  and **Improve wording**")
}
[IO.File]::WriteAllText($path, $text, (New-Object Text.UTF8Encoding($false)))

$corpusPath = Join-Path $PSScriptRoot 'concordance\README.md'
$corpus = [IO.File]::ReadAllText($corpusPath, [Text.Encoding]::UTF8)
$corpus = [regex]::Replace($corpus, '(?m)^\| `en-es-depth\.tsv`.*$', '| `en-es-deep2m.tsv`, `es-en-deep2m.tsv` | English ↔ Spanish | 2,000,000 | OpenSubtitles v2024, 600 MB prefix, reservoir sample |')
$corpus = [regex]::Replace($corpus, '(?m)^\| `ar-en-depth\.tsv`.*$', '| `ar-en-deep2m.tsv`, `en-ar-deep2m.tsv` | Arabic ↔ English | 2,000,000 | OpenSubtitles v2024, 600 MB prefix, reservoir sample |')
if ($corpus.IndexOf('12 additional pairs', [StringComparison]::Ordinal) -lt 0) {
    $corpus = $corpus.Replace('| `en-es.tsv`, `es-en.tsv` | English ↔ Spanish | 50,000 | OpenSubtitles v2024, reservoir sample |',
        "| `en-es.tsv`, `es-en.tsv` | English ↔ Spanish | 50,000 | OpenSubtitles v2024, reservoir sample |`n| `fr-en.tsv` … `zh-en.tsv` | 12 additional pairs | 100,000 | OpenSubtitles v2024, reservoir sample |")
}
$corpus = $corpus.Replace('The depth packs also have `.qidx` sidecars.', 'The depth packs also have `.qidx` sidecars; the 2M packs are the large-install tier and the 100k packs are the core-language tier.')
[IO.File]::WriteAllText($corpusPath, $corpus, (New-Object Text.UTF8Encoding($false)))
Write-Host 'final docs updated'
