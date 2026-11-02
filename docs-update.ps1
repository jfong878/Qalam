$ErrorActionPreference = 'Stop'

function Update-Doc([string]$relative, [hashtable]$pairs) {
    $path = Join-Path $PSScriptRoot $relative
    $text = [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
    foreach ($old in $pairs.Keys) {
        $count = ([regex]::Matches($text, [regex]::Escape($old))).Count
        if ($count -eq 0) { Write-Warning "not found in ${relative}: $($old.Substring(0, [Math]::Min(70, $old.Length)))"; continue }
        $text = $text.Replace($old, $pairs[$old])
    }
    [IO.File]::WriteAllText($path, $text, (New-Object Text.UTF8Encoding($false)))
    Write-Host "updated $relative"
}

$readme = @{}
$readme['- copies the seeded and 500k depth concordance packs, plus their `.qidx` indexes, to'] = '- copies the seeded, 100k core-language, and 2M depth concordance packs, plus their `.qidx` indexes, to'
$readme['- The seed concordance packs use about 18 MB; the bundled 500k depth TSVs and their
  `.qidx` sidecars use several hundred MB. User-built packs use more disk, and larger
  packs should be built with `--index` so the text stays on disk'] = '- The seed and topical packs are small; the 100k core-language packs add roughly 240 MB,
  and each 2M depth pair with its `.qidx` sidecar uses about 400 MB. User-built packs use
  more disk, and larger packs should be built with `--index` so the text stays on disk'
$readme['| `TranslationMode` | `"auto"` | `auto` (local slang + online), `online`, or `offline` (Ollama only) |'] = '| `TranslationMode` | `"auto"` | `auto` (local slang + online), `online`, or `offline` (Ollama only) |
| `Theme` | `"system"` | `system`, `light`, `sepia`, or `dark`; System follows the Windows app theme |'
$readme['The page uses a search-first **Translate**, **Study**, and **Context** layout. Translation'] = 'The page has a **System / Light / Sepia / Dark** theme selector; System follows the device
`prefers-color-scheme`, and the choice is remembered locally. The page uses a search-first
**Translate**, **Study**, and **Context** layout. Translation'
$readme['The main window and browser client offer a theme menu: System, Light, Sepia, and Dark.'] = 'The main window and browser client offer a theme menu: System, Light, Sepia, and Dark.'
$readme['The app
ships two tiers of sampled [OpenSubtitles v2024](https://opus.nlpl.eu/OpenSubtitles/)
packs (ODC-BY): 50,000-pair seed packs and 500,000-pair depth packs for English ↔
Spanish and Arabic ↔ English, plus smaller topical packs for Spanish/English slang and
Arabic dialect lines.'] = 'The app
ships sampled [OpenSubtitles v2024](https://opus.nlpl.eu/OpenSubtitles/) packs (ODC-BY):
50,000-pair seed packs, 100,000-pair core packs for 12 additional language pairs, and
2,000,000-pair depth packs for English ↔ Spanish and Arabic ↔ English, plus smaller
topical packs for Spanish/English slang and Arabic dialect lines.'
$readme['Qalam.exe --buildconcordance --tmx en-es.tmx --from en --to es `
  --both --limit 500000 --sample --index --out "$env:LOCALAPPDATA\Qalam\concordance"'] = 'Qalam.exe --buildconcordance --tmx en-es.tmx --from en --to es `
  --both --limit 2000000 --sample --index --out "$env:LOCALAPPDATA\Qalam\concordance"'
$readme['A full OpenSubtitles pair is several gigabytes; the bundled depth tier is a
500,000-pair sample per direction, not the whole corpus, and Reverso''s licensed corpus
is larger.'] = 'A full OpenSubtitles pair is several gigabytes; the bundled depth tier is a
2,000,000-pair sample per direction for English ↔ Spanish and Arabic ↔ English, not the
whole corpus, and Reverso''s licensed corpus is larger. The 12 core-language pairs are
100,000-pair samples.'
$readme['- **The bundled corpus is a large open sample, not Reverso''s dump.** Each depth
  direction has 500,000 aligned pairs plus the 50,000-pair seed and small topical packs.'] = '- **The bundled corpus is a large open sample, not Reverso''s dump.** English ↔ Spanish
  and Arabic ↔ English have 2,000,000 aligned pairs per direction; 12 additional pairs
  have 100,000, plus the 50,000-pair seed and small topical packs.'
$readme['The **Download for Windows** card and header button fetch
`/download/windows?token=…`. Qalam builds a ZIP on demand from the running app: the
installer scripts, browser assets, character data, Tashkeel/PDF workers, and the 50k
seed/topical concordance packs. The 500k depth packs are deliberately left out so the
download stays around 9 MB; a recipient can build larger packs later.'] = 'The **Download for Windows** card and header button fetch
`/download/windows?token=…`. Qalam builds a ZIP on demand from the running app: the
installer scripts, browser assets, character data, Tashkeel/PDF workers, and the small
seed/topical concordance packs. The multi-million-pair depth packs are deliberately left
out so the download stays around 9 MB; a recipient can build larger packs later.'
Update-Doc 'README.md' $readme

$corpus = @{}
$corpus['| `en-es-depth.tsv`, `es-en-depth.tsv` | English ↔ Spanish | 500,000 | OpenSubtitles v2024, 600 MB prefix, reservoir sample |
| `ar-en.tsv`, `en-ar.tsv` | Arabic ↔ English | 50,000 | OpenSubtitles v2024, reservoir sample |
| `ar-en-depth.tsv`, `en-ar-depth.tsv` | Arabic ↔ English | 500,000 | OpenSubtitles v2024, 600 MB prefix, reservoir sample |'] = '| `en-es-deep2m.tsv`, `es-en-deep2m.tsv` | English ↔ Spanish | 2,000,000 | OpenSubtitles v2024, 600 MB prefix, reservoir sample |
| `ar-en-deep2m.tsv`, `en-ar-deep2m.tsv` | Arabic ↔ English | 2,000,000 | OpenSubtitles v2024, 600 MB prefix, reservoir sample |
| `en-fr.tsv` … `zh-en.tsv` | 12 additional pairs | 100,000 | OpenSubtitles v2024, reservoir sample |'
$corpus['This keeps a 500,000-pair pack usable without materializing every pair and
posting list in RAM.'] = 'This keeps a 2,000,000-pair pack usable without materializing every pair and
posting list in RAM.'
$corpus['The 500,000-pair depth packs read a
600 MB compressed prefix and sampled across every parsed row, not just the first N.'] = 'The 2,000,000-pair depth packs read a
600 MB compressed prefix and sampled across every parsed row, not just the first N.
The 12 core-language packs use 100,000 pairs each.'
$corpus['# 500k depth packs, both directions, with the disk index
Qalam.exe --buildconcordance --tmx en-es.tmx.gz --from en --to es `
  --both --limit 500000 --sample --index --suffix depth `'] = '# 2M depth packs, both directions, with the disk index
Qalam.exe --buildconcordance --tmx en-es.tmx.gz --from en --to es `
  --both --limit 2000000 --sample --index --suffix deep2m `'
Update-Doc 'concordance\README.md' $corpus
