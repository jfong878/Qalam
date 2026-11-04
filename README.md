# Qalam

![Qalam Qaaf mark](assets/qalam-mark-64.png)

[![Latest release](https://img.shields.io/github/v/release/jfong878/Qalam?label=download)](https://github.com/jfong878/Qalam/releases/latest)

A local, independent replacement for Reverso Context's **Ctrl + C + C** popup, with
offline **Arabic Tashkeel** and **Chinese Pinyin** for text that has none. The desktop
app also serves a responsive browser client, so the same translation, study, and
concordance workspace works in a desktop browser or on a phone on the same network.

Qalam is not affiliated with Reverso and does not use Reverso code or binaries. It was
built after inspecting how the installed Reverso 2.17.2.970 responds to a double copy;
see [ANALYSIS.md](ANALYSIS.md).

## What it does

1. Highlight text in any application.
2. Press **Ctrl + C + C** anywhere (within 600 ms by default).
3. Qalam asks Windows UI Automation for the focused control's selected text and falls
   back to the clipboard when the app does not expose a text selection.
4. A popup appears next to the cursor with:
   - the detected → target language and the translation,
   - **Tashkeel** for the Arabic side or **Pinyin** for Chinese text, generated
     offline with Mishkal and pypinyin,
   - a **character dictionary** for Chinese selections with pinyin, Kangxi
     radical, stroke count, definition, traditional/simplified variants, and an
     animated stroke-order view,
   - **synonyms** for short queries when Google's dictionary has them,
   - **in-context examples** from a local open-corpus concordance first, then
     Tatoeba and MyMemory only when the local packs have no match, with attribution,
   - **grammar and conjugation** for single words from Wiktionary via Kaikki, cached
     locally so recently viewed cards work in offline mode,
   - a **target-language selector** in the popup itself,
   - **Listen** buttons for pronunciation and copy buttons for the text.

Arabic source text is vocalized directly. English (or other) source translated to Arabic
gets tashkeel on the Arabic translation. If you force **Arabic → Arabic**, Qalam skips
translation and only adds tashkeel.

`Esc` or clicking elsewhere closes the popup. The target button in the popup header
opens the same language menu (73 targets) as the tray; choosing a language persists in the config
and immediately re-runs the current selection. The tray icon keeps the last result, a
**Favorites** menu for saved lookups, a **History** submenu with the last 30 lookups, the
target-language menu, the translation-source menu, config/log shortcuts, and Quit. Double-clicking the tray icon, or
choosing **Open Qalam**, opens the full window.

The full window includes a **Report a bug** action. It creates a local Markdown report
with the app build, Windows version, theme, provider mode, target language, and editable
notes. **Copy report** and **Save report** never send anything automatically; the same
action is available in the mobile footer.

Qalam also has a normal window for text you want to type or paste. Launching the pinned
taskbar icon or the Start Menu shortcut opens it; the Startup shortcut starts Qalam in
the background so login does not put a window on screen. If Qalam is already running, a
second launch wakes the existing process and shows the window instead of creating a
duplicate tray icon. Closing the window hides it and leaves the Ctrl + C + C hook
running. History and favorites are stored
locally in `%LOCALAPPDATA%\Qalam\history.json` and `favorites.json`; set `SyncFolder` in
the config to keep those two files in a cloud-synced folder instead. That is file sync,
not a Qalam account.

## Install

Download and run `Qalam-Setup-<version>.exe`. The current public, unsigned build is
published at <https://github.com/jfong878/Qalam/releases/latest>, which always redirects
to the newest release.

This is a one-click, per-user installer (no UAC) modeled on the OmniPDF release: it installs to
`%LOCALAPPDATA%\Qalam`, creates Start Menu, Startup, and desktop shortcuts,
backs up and disables Reverso's autostart entry, and installs the Python
environment for Tashkeel and PDF translation. It never uninstalls Reverso or
edits Reverso's files.

The setup runs the same idempotent `install.ps1` used by the developer path, so
re-running it upgrades the executable and preserves `config.json`, history,
favorites, study data, and concordance packs. Uninstall from
**Settings > Apps > Installed apps > Qalam** (or run `Uninstall Qalam.exe`).

For development, the old source-tree path still works:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File install.ps1
```

### Build the installer

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build-installer.ps1
```

This builds `dist/Qalam.exe`, stages the same payload used by the portable
installer, downloads the pinned NSIS 3.11 compiler into `.tools/` on first use,
and produces:

- `dist/Qalam-Setup-<version>.exe` — one-click per-user setup
- `dist/Qalam-Windows.zip` — portable/source-tree package
- `dist/latest.json` — update manifest used by Qalam
- `dist/latest.yml` — equivalent generic-feed metadata

The setup is unsigned by default, so Windows SmartScreen will warn until a
code-signing certificate is configured.

For each release, use the release script. It updates `src/AssemblyInfo.cs`,
builds with the same version, creates a draft, verifies every asset, publishes
it as the latest release, then downloads the public feed and installer again to
verify their hashes:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File release.ps1 `
  -Version 1.0.1 `
  -Notes "What changed in this release."
```

The release script also runs the 13-block, 15-language PDF formatting suite
from `selftest/format_suite.py` against the built `dist/Qalam.exe`; any layout,
font, report, or copy-text failure stops the release before a draft is created.
Run it directly when iterating on the PDF path:

```powershell
python selftest/format_suite.py --exe dist/Qalam.exe
```

Use `-BuildOnly` to prepare and inspect a build without publishing. If the
publish step is interrupted after the draft is created, rerun with
`-PublishOnly -Resume` using the same version; the incomplete draft is replaced
and rebuilt before publishing. `-Notes` is required. The build phase refuses to
reuse or lower the current assembly version; `-PublishOnly` requires artifacts
already built at the requested version.

## Updates

Qalam checks a generic HTTPS feed shortly after startup, every six hours while
it remains running, and from **tray → Check for updates…**. The feed moved from the
old `qalam-releases` repository to the main repository; installs made before 1.0.3
still carry the old URL and need the installer run once from
<https://github.com/jfong878/Qalam/releases/latest>, which rewrites it in place.
Fresh installs default to the public GitHub release feed:
`https://github.com/jfong878/Qalam/releases/latest/download/latest.json`.
Override `UpdateUrl` in `config.json` to use another HTTPS `latest.json`
manifest, or set it to an empty string to disable automatic checks:

```json
{
  "version": "1.0.1",
  "url": "Qalam-Setup-1.0.1.exe",
  "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
  "size": 8863398,
  "notes": "What changed in this release.",
  "mandatory": false
}
```

The URL may be relative to the manifest. Qalam downloads the setup into
`%LOCALAPPDATA%\Qalam\updates`, verifies its SHA-256, and reports
**"Restart to update"** in the tray. The setup installs silently when Qalam
quits, or immediately if you choose **Restart now**. `update.ps1` waits for the
running app, verifies the installer again, runs it with `/S`, and restarts only
when requested. Existing user data is preserved by the installer.

Upload `Qalam-Setup-<version>.exe` and `latest.json` to the same HTTPS
directory. `latest.yml` is generated for feed parity with the OmniPDF-style
release layout; Qalam itself reads `latest.json`.

The manifest must be served over HTTPS. HTTP is accepted only for loopback
testing. The hash protects against a corrupted or mismatched download; it does
not replace a code-signing certificate because the manifest is served by the
update host. Use a protected host and sign the setup before public distribution.

## Requirements

- Windows 10/11 with .NET Framework 4.8 (included in current Windows)
- Python 3 on `PATH` for the installer (to create the tashkeel and PDF environment)
- Internet access for translation and for the installer's Python packages; tashkeel,
  the phrasebook, and concordance search are fully offline
- Optional: a running [Ollama](https://ollama.com) server for colloquial translation
  and the explicit offline provider
- The seed and topical packs are small; the 100k core-language packs add roughly 240 MB,
  and each 2M depth pair with its `.qidx` sidecar uses about 400 MB. User-built packs use
  more disk, and larger packs should be built with `--index` so the text stays on disk

## Configuration

`%LOCALAPPDATA%\Qalam\config.json` is created on first run. Open it from the tray menu.

| Field | Default | Meaning |
| --- | --- | --- |
| `AutoTarget` | `true` | Arabic source → English, everything else → Arabic |
| `Target` | `"ar"` | Target used when `AutoTarget` is false |
| `DoubleCopyWindowMs` | `600` | Maximum gap between the two Ctrl+C presses |
| `MaxChars` | `600` | Longer selections are trimmed |
| `PythonPath` | `""` | Optional explicit interpreter for the tashkeel worker |
| `PdfPythonPath` | `""` | Optional explicit interpreter for the PDF bridge; blank uses `PythonPath` or the installed virtual environment |
| `SyncFolder` | `""` | Optional folder for `history.json` and `favorites.json`, for example a OneDrive folder; blank means `%LOCALAPPDATA%\Qalam` |
| `MobileEnabled` | `false` | Start the browser/mobile server with the app |
| `MobilePort` | `8787` | LAN port; Qalam tries the next free port if this one is taken |
| `MobileToken` | `""` | Generated random token required by the browser client; treat it as a local password |
| `UpdateUrl` | GitHub release feed | HTTPS URL of the update manifest; empty disables automatic checks |
| `AutoUpdate` | `true` | Check shortly after startup and every six hours while running |
| `LocalModelEnabled` | `true` | Enable local Ollama for slang and `offline` mode |
| `LocalModelEndpoint` | `"http://127.0.0.1:11434"` | Ollama API base URL |
| `LocalModelName` | `""` | Optional exact model name; blank auto-selects an installed chat model |
| `TranslationMode` | `"auto"` | `auto` (local slang + online), `online`, or `offline` (Ollama only) |
| `Theme` | `"system"` | `system`, `light`, `sepia`, or `dark`; System follows the Windows app theme |
| `ExcludedProcesses` | password managers | Foreground processes that never trigger |

Supported targets: **73 languages** — Arabic, English, French, Spanish, German, Russian, Chinese (Simplified and Traditional), Turkish, Urdu, Persian, Hindi, Portuguese, Italian, Japanese, Korean, Indonesian, Dutch, Polish, Ukrainian, Greek, Hebrew, Vietnamese, Thai, Romanian, Czech, Swedish, Danish, Norwegian, Finnish, Hungarian, Bulgarian, Croatian, Serbian, Slovak, Slovenian, Catalan, Filipino, Malay, Bengali, Tamil, Telugu, Marathi, Swahili, Azerbaijani, Kazakh, Uzbek, Afrikaans, Albanian, Armenian, Georgian, Khmer, Lao, Burmese, Nepali, Sinhala, Amharic, Yoruba, Zulu, Icelandic, Irish, Welsh, Maltese, Macedonian, Bosnian, Belarusian, Lithuanian, Latvian, Estonian, Basque, Galician, Esperanto, and Latin. The single table lives in `src/LanguageCatalog.cs`, so the popup, main window, tray, and browser client cannot drift. Auto mode is Arabic ↔ English; pick any other target from the popup header, the main window, the tray, or the web client. Tashkeel is applied to Arabic only.

## Colloquial phrases

Machine translation is good at standard prose and poor at slang. Qalam has a curated
Spanish ↔ English table for Mexican and Peninsular colloquialisms and profanity,
including `calla chingada`, `no mames`, `no manches`, `güey`, `cabrón`, `pendejo`,
`pinche`, `chingón`, `joder`, `coño`, `mierda`, `hostia`, `a huevo`, `me vale madre`,
`vale verga`, `ni madres`, `gilipollas`, `capullo`, `me cago en la leche`, `ni de coña`,
`de puta madre`, and a second pass of Mexican and Peninsular everyday lines such as
`no mames güey`, `vete a la verga`, `me lleva la chingada`, `está cabrón`, `a poco`,
`de volada`, `en chinga`, `qué oso`, `vaya tela`, `qué morro`, and `a tope`. It also
has an Arabic-dialect table covering Egyptian, Levantine, Gulf, Sudanese, Iraqi, Yemeni, Hijazi,
Moroccan, Tunisian, Algerian, Libyan, and common MSA expressions such as `إيه ده`, `معلش`, `شو الأخبار`, `وين رايح`, `وش أخبارك`,
`لاباس`, `بزاف`, `خلاص`, and `يا سلام`, plus newer everyday lines including `إزيك`,
`إيه يا عم`, `يا باشا`, `كده كده`, `على طول`, `مش فارقة`, `خد بالك`, `بجد`, `جامد`,
`دلوقتي`, `شو رايك`, `على راسي`, `بلا مؤاخذة`, `ما تزعل`, `تكرم`, `شو صار`,
`ما في مشكلة`, `وش رايك`, `أبشر`, `شنو الأخبار`, `كيداير`, `الله يخليك`, `بالصحة`,
`لو سمحت`, `بالتوفيق`, `ماشي`, `بعد بخلص من الشغل`, `قريب`, `هسي`,
`زول`, and `كيفنك`, with an English → Arabic reverse path for common lines.
When the selection is fully covered, the popup uses the curated rendering, labels it
**Colloquial meaning**, and works without a network. For example:

> **Calla chingada, no mames con mamar.** → *Shut the fuck up, don't come at me with that bullshit.*

Partial phrases and other language pairs can use a local model. If
[Ollama](https://ollama.com) is running on this machine, Qalam asks it for a
translation that keeps the original register whenever the selection contains slang or
profanity. The model stays on this machine and Qalam never installs or downloads one.
It prefers a small Qwen/Llama-family model; set `LocalModelName` to choose a specific
model, `LocalModelEndpoint` to use another server, or `LocalModelEnabled` to `false` to
skip the local path. When Ollama is not running, the normal online services are used.

The phrasebook is deliberately reviewable. The current table has 96 Spanish/English
entries, 144 Arabic → English entries, and 28 English → Arabic entries. It covers
common Mexican, Peninsular, Egyptian, Levantine, Gulf, Sudanese, Iraqi, Yemeni, Hijazi,
Moroccan, Tunisian, Algerian, and Libyan expressions, not
every dialect; the local model handles the rest. Each Arabic entry carries the lahjah
(dialect) that uses it, and several when a phrase is shared: `ماشي` is tagged
Sudanese, Gulf, and Egyptian, and the label travels with the hint the local model
receives. Entries are matched as whole phrases, and a partial match is never returned as a half-translated sentence.

## Grammar and conjugation

Highlight a single word, then press Ctrl + C + C. When the selection is one word, Qalam
looks it up in the open [Kaikki](https://kaikki.org/dictionary/) dictionary, which is
derived from English Wiktionary under CC BY-SA. The popup shows the part of speech,
IPA when available, short definitions, and a compact conjugation table for verbs.
Spanish entries can show present, preterite, imperfect, future, conditional, present
and imperfect subjunctive, and imperative forms; French and the other Latin-script
languages show the tenses their Wiktionary entry provides. Arabic verbs get past,
present, and imperative tables with gender and number distinctions, plus verbal noun
and active/passive participles. Vocalized Arabic selections are looked up by their
undiacritized page key, so `كَتَبَ` finds the same entry as `كتب`. The full window
shows the same card after a typed lookup.

Sources are attributed in the card. No Reverso grammar page, account, or proprietary
data is used. Coverage depends on the Wiktionary entry: not every word has an IPA or
an inflection table, and Arabic dialect words often have definitions without a full
paradigm. In that case Qalam shows what the open entry contains instead of inventing
the missing forms.

Successful lookups are saved to `%LOCALAPPDATA%\Qalam\grammar-cache.json` (up to 400
entries). A card served from that file is marked **CACHED**. In `offline` mode Qalam
reads only this cache for grammar and never contacts `kaikki.org`; a word that has
never been looked up online shows no grammar card until it has been cached once.

## Study

**Study** turns saved results into a local spaced-repetition deck. Use **Study** on
the main result card, or tray → **Study last result** / **Study…**. The first time,
choose **Import favorites & history**; a result added from the main window becomes a
card immediately. Each card shows the source, hides the translation, and grades with
**Again / Hard / Good / Easy** (keyboard 1–4). Again returns in ten minutes; the other
grades schedule one, three, or more days with an ease factor. **Review ahead** lets
you work through the deck before cards are due.

The deck is capped at 1,000 cards and stored in `study.json` beside history and
favorites, so `SyncFolder` syncs it with those files. The same deck is available in
the desktop app and the local browser/mobile client; there is no account or cloud
deck. `--studytest --out FILE` checks the scheduler and renders `study-window.png`.

## Mobile & web

Qalam can serve its own browser client on the LAN. Use tray → **Mobile & web app**.
The server binds an available port (8787 by default), creates a random access token,
copies the tokenized URL to the clipboard, and shows the LAN address. The same page
works in a desktop browser at `http://127.0.0.1:8787/?token=…` and on a phone at
`http://<LAN-IP>:8787/?token=…`. Open the exact link; the token is the only credential.
Allow Qalam through Windows Firewall the first time. Use the same tray item to stop
the server, or set `MobileEnabled` to `false` in the config.

The page has a **System / Light / Sepia / Dark** theme selector; System follows the device
`prefers-color-scheme`, and the choice is remembered locally. The page uses a search-first
**Translate**, **Study**, and **Context** layout. Translation
uses the same provider path as the desktop popup and appends aligned examples from the
local qidx packs under the result. Study reads and grades the same `study.json` deck,
and Context searches the corpus directly with provenance. The page is installable
where the browser permits a home-screen web app; otherwise bookmark it. There is no
Qalam account, cloud relay, or externally hosted copy. The link contains a local
secret, so do not post it publicly, and turn the server off when you do not need it.

The **Download for Windows** card and header button fetch
`/download/windows?token=…`. Qalam builds a ZIP on demand from the running app: the
installer scripts, browser assets, character data, Tashkeel/PDF workers, and the 50k
seed/topical concordance packs. The depth packs (500,000 and 2,000,000 pairs) are deliberately left out so the
download stays around 9 MB; a recipient can build larger packs later. Extract the ZIP
and run `Install Qalam.cmd`. The package does not include the local config, history,
study deck, or access token.

The HTTP API is intentionally small: `/api/health`, `/api/languages`,
`/api/translate`, `/api/rewrite`, `/api/report`, `/api/corpus`, `/api/study`,
`/api/study/add`, `/api/study/grade`, and `/api/study/import`, plus the
authenticated `/download/windows` package route. Every request requires the
`X-Qalam-Token` header (or the `token` query parameter). `--mobiletest --out FILE`
exercises the server, token rejection, page assets, theme/wording/report controls,
translation, study round trip, and the Windows package without showing a window. `--mobileserve --port 8787 --token TOKEN --out URL.txt`
runs the server for development and browser checks.

## Local concordance

Context examples come from an on-disk sentence-pair index before any web service is
asked. Packs live in `%LOCALAPPDATA%\Qalam\concordance\` as plain TSV files. The app
ships two tiers of sampled [OpenSubtitles v2024](https://opus.nlpl.eu/OpenSubtitles/)
packs (ODC-BY): 50,000-pair seed packs plus 500,000-pair and 2,000,000-pair depth packs for English ↔
Spanish and Arabic ↔ English, plus smaller topical packs for Spanish/English slang and
Arabic dialect lines. That is real dialogue, not generated examples, and it works
with no network connection.

A pack is directional. The header names the pair and its source; each following line
is one aligned source/target pair:

```tsv
#qalam-concordance	1
#source	es
#target	en
#corpus	OpenSubtitles v2024 (sampled)
#license	ODC-BY
#url	https://opus.nlpl.eu/OpenSubtitles/
#pairs	20000
No volverá, el cabrón.	The bastard's not coming back.
```

Queries are normalized for case, accents, punctuation, Arabic diacritics, and
tatweel. Arabic conjunction and article prefixes are stemmed, so `كتاب` finds
`الكتاب` and `مدرسة` finds `المدرسة`. Ranking favours the exact phrase, then token
coverage, a readable sentence length, and target words from the selected translation. The translation hint is only
a ranking signal, so a noisy alignment can still appear; the section is labelled with
its corpus and license.

Within that order, lines that read as spoken dialogue rank above flat prose. A
per-language lexicon of colloquial markers (particles, address terms, interjections,
profanity, dialect forms) and of formal markers (polite pronouns, honorifics,
official phrasing) scores each candidate, together with structural cues such as
hesitations. The bonus is bounded: it reorders lines with the same match, and an
exact phrase match still wins outright. `Qalam.exe --registertest --pack` prints the
scores and checks every language the app ships.

### Build a larger pack

OPUS TMX files are the supported input. The builder streams them, accepts gzip, and
keeps complete pairs when a bounded download ends in the middle of the XML:

```powershell
Qalam.exe --buildconcordance --tmx en-es.tmx --from en --to es `
  --both --limit 2000000 --sample --index --out "$env:LOCALAPPDATA\Qalam\concordance"
```

Useful options:

| Option | Effect |
| --- | --- |
| `--both` | Write `en-es.tsv` and `es-en.tsv` from the same aligned pairs |
| `--limit N` | Maximum pairs written per direction (default 20000) |
| `--sample` | Reservoir-sample across the whole input instead of taking the first N |
| `--contains "cabron,verga"` | Keep only lines containing one of these normalized terms |
| `--suffix slang` | Name the output `es-en-slang.tsv` so it sits beside the general pack |
| `--index` | Write the binary `.qidx` sidecar used for large disk-backed packs |
| `--corpus`, `--license`, `--url` | Attribution written into the pack header and popup |

Large packs also get a binary `.qidx` sidecar. It stores sorted token postings, row
offsets, and the aligned text; the loader keeps only the token directory and row
offsets in memory and reads candidate rows from disk. That keeps a 2,000,000-pair pack
searchable without materializing every pair and every posting list in RAM. The TSV
remains the readable interchange format; delete a `.qidx` to force a rebuild, or pass
`--index` to create it. The installed build can search a pack without starting the UI:

```powershell
Qalam.exe --findconcordance --query "cabrón" --from es --to en `
  --hint "that bastard" --out query.txt
```

A full OpenSubtitles pair is several gigabytes; the bundled depth tier is a
2,000,000-pair sample per direction for English ↔ Spanish and Arabic ↔ English, not the
whole corpus, and Reverso's licensed corpus is larger. The 12 core-language pairs are
100,000-pair samples. Packs are scanned on first use and only the selected language pair is
loaded. Seed packs use the in-memory token index; depth packs use `.qidx` and read
candidate rows from disk instead of loading all text. Startup stays immediate, and a
lookup pays only for its own pair. Restart Qalam after adding a pack so the scan sees
it. The builder does not download anything itself. Provenance and rebuild details are
in [concordance/README.md](concordance/README.md).

## Online versus offline

`selftest/compare/compare.md` is a checked-in run of 17 fixed sentences (standard prose,
Spanish/English slang, and Egyptian/Levantine/Gulf/Moroccan dialect) through both modes
on this machine. The run used the auto-selected `qwen2.5vl:3b` model on CPU:

| Path | Mean token F1 | Script-correct | Usable | Mean latency |
| --- | ---: | ---: | ---: | ---: |
| Online | 0.917 | 17/17 | 17/17 | 91 ms |
| Offline | 0.887 | 17/17 | 17/17 | 2,994 ms |

Online is stronger on standard prose. Offline keeps the curated phrasebook's register,
is deterministic once the model is warm, and does not send the selection out; its
weakness is vocabulary below the phrasebook and much higher CPU latency. The report
contains every output, so the F1 numbers are a starting point rather than a claim of
human-equivalent quality. The model and all outputs can differ on another machine.

Offline grammar is independent of the model: Qalam serves a previously cached
definition and conjugation card even when the local translation model is unavailable,
and marks it **CACHED**. `auto` and `online` also use a cached entry when one exists;
`offline` is the only mode that refuses network access on a cache miss. Context
examples follow the same rule: the phrasebook and local concordance are always
available, while Tatoeba, MyMemory, and Google are never contacted in `offline` mode.

## Documents

**Translate document…** in the main window, or in the tray menu, translates a `.docx`
or a text-based `.pdf` and writes a separate output file. The original is never
overwritten, and a failed or cancelled run does not leave a partial document behind.

- **DOCX** is translated through its WordprocessingML parts. Paragraph and run styles,
  colors, fonts, sizes, bold/italic runs, tables, headers, footers, footnotes,
  hyperlinks, page breaks, and section layout stay in the file. RTL targets get the
  corresponding paragraph and run direction markers.
- **PDF** text is extracted with its box, font size, color, weight, slant, and alignment.
  The translation is written back into the same box, with the original page images,
  backgrounds, borders, and links left in place. Text that cannot fit is reduced within
  the box; if it still cannot fit, the original passage is kept and the result dialog
  reports a layout warning rather than leaving a blank hole.
- Scanned PDFs need OCR, which Qalam does not bundle. Password-protected and encrypted
  PDFs are refused with an explanation.

The document path uses the same translation service and target selection as the popup.
Long documents report progress and can be cancelled from the progress window.

## Translation and privacy

In the default `auto` mode, selected text is sent to Google's public endpoints
(`translate.googleapis.com`, `translate.google.com`, and `clients5.google.com`) for
translation, dictionary synonyms, and pronunciation audio. Context examples are
looked up in the local phrasebook and concordance first; Tatoeba and MyMemory are
contacted only when those local sources do not provide enough lines. Grammar lookups
go to `kaikki.org`. Qalam races Google's translation shapes and falls back to
MyMemory using the detected source language if both fail. The curated phrasebook and
concordance are local. When a local Ollama model is running, colloquial text is sent
to it over the loopback interface and does not leave this machine; otherwise the
online services above are used.

Set `TranslationMode` to `offline` (tray → **Translation source**, or the button in
the main window) to use only the local Ollama model for translation. Offline mode does
not contact Google, MyMemory, Tatoeba, or Kaikki; online examples are skipped, while
curated phrasebook and concordance examples still appear. Grammar comes from
`grammar-cache.json` and is marked **CACHED**; if the word has not been cached, no
grammar card is shown. Set it to `online` to skip Ollama entirely. Tashkeel and
concordance search stay on this machine in every mode.

The log (`%LOCALAPPDATA%\Qalam\logs\qalam.log`) records lifecycle, endpoint failures,
language pairs, character counts, and skip reasons. It does **not** record selected or
translated text. The grammar cache does store the looked-up word, its definitions,
forms, and source URL locally; delete `%LOCALAPPDATA%\Qalam\grammar-cache.json` to
clear it. History and favorites, however, store the source and translation in plain
text; clear them from tray → **History** → **Clear history** and tray → **Favorites**
→ **Clear favorites**. If `SyncFolder` points at a cloud folder, those files leave
this machine through that provider.

Document translation sends each text block to the same translation services. The
original file is only read; the translated copy is written where you choose it. The PDF
bridge's temporary extraction files live under `%TEMP%\qalam-doc-*` and are removed when
the run finishes or is cancelled.

## Tashkeel quality

Mishkal is a rule-based diacritizer, not a human. It is usually right on common MSA but
can misplace shadda or case endings, for example `السلام عليكم` can come out as
`السِّلَامُ عَلَيْكُمْ` instead of `السَّلَامُ عَلَيْكُمْ`. Treat the output as a
reading aid and review anything that will be published or taught.

Already-vocalized text is passed through without doubling the marks.

## Pronunciation and Lahjāt

Tashkeel and the **Listen** button follow a pronunciation register, chosen from tray ->
**Pronunciation** or with `SpokenLahja` in `config.json`. The default is **Fuṣḥā**;
the others are Egyptian, Levantine, Gulf, Sudanese, Iraqi, Yemeni, Hijazi, Moroccan,
Tunisian, Algerian, and Libyan.

Fuṣḥā keeps Mishkal's case endings and Tanwīn. A lahjah register runs a second pass in
the offline worker: the dialect forms it knows are vocalized the way they are spoken
(`هسي` -> `هَسِّي`, `بخلص` -> `بَخَلِّص`, `ماشي` -> `مَاشِي`) and the ʾIʿrāb is
dropped, because nobody says `بَخَلُّصُ` in the street. The Listen button sends that
same vocalized line to the speech service, so the audio follows the register instead of
reading the text as MSA.

The speech service exposes one Arabic voice: every regional tag returns the same audio,
so the difference you hear comes from the vowels and endings in the vocalized text, not
from a different speaker. Words outside the dialect lexicon still get the MSA reading.


## Pinyin

Chinese source text, or any source translated into Chinese, gets a **PINYIN** card in
the same slot as **TASHKEEL**. The transcription is produced on this machine by
[pypinyin](https://github.com/mozillazg/python-pinyin) with tone marks, so `你好，世界。`
becomes `nǐ hǎo，shì jiè。`. Latin words, numbers, and punctuation pass through
unchanged. **Listen** speaks the original Chinese text; **Copy** copies the romanization.

pypinyin applies a default reading to polyphonic characters such as `行`, `重`, and
`长`, so a character pronounced differently in context can get the wrong tone or
syllable. Treat the card as a reading aid and check anything that will be published or
taught.

## Chinese characters

**Characters** opens the character window from the main window, from the Pinyin card's
**Characters** button, or from tray → **Chinese Characters…**. Type or paste text, or
open it directly from the current Chinese selection. Use the window's **← Back** button
or Esc to return to the main window, compact result, or tray-opened window you came
from.

For every distinct Han character in the text the window shows:

- the character and its Mandarin reading,
- its Kangxi radical number and radical, with a short gloss for the radical,
- the radical's additional strokes and the total stroke count,
- the Unihan definition, and
- the traditional and simplified forms when they differ from the selected character.

The left column lists the characters in order of first appearance and summarises the
radicals they use. Selecting a character shows it large on the right with **Play**,
**Step**, and **Restart** controls for its stroke order: each stroke appears in gold and
then settles into the finished character. Stroke outlines come from
[Hanzi Writer](https://hanziwriter.org/) character data (derived from Make Me a Hanzi);
attribution and the Arphic Public License apply. The first view of a character downloads
its outline (about 1–3 KB) and caches it under `%LOCALAPPDATA%\Qalam\strokes`, so later
views work without a connection. Characters that were never viewed online cannot show
stroke order in offline mode.

The dictionary itself is bundled and offline. `data/characters.tsv` is generated by
[tools/build_characters.py](tools/build_characters.py) from **Unihan 18.0** and the
Unicode **CJKRadicals** table: 27,321 BMP characters with readings, radicals, stroke
counts, variants, and concise glosses. It is a character dictionary, not a word
dictionary: looking up `学生` shows `学` and `生` separately, not a compound entry.
Rare characters outside the BMP (CJK Extension B and later) are not included. Unihan
glosses are terse, and some simplified forms inherit their radical from the traditional
form because Unihan does not repeat `kRSUnicode` for every variant.

## Reverso parity

Covered by Qalam: the Ctrl + C + C trigger, UI Automation and clipboard selection, a
pinned taskbar/Start Menu window, translation, colloquial phrasebook and optional
on-device slang/profanity translation, a local open-corpus concordance with real
bilingual examples, synonyms, pronunciation, copy, local favorites, history, **73
target languages**, format-preserving DOCX/PDF document translation, open-data grammar
and conjugation lookup, an optional offline provider, offline Tashkeel and Pinyin, a
bundled character/radical dictionary with stroke-order animation, and a responsive
browser/mobile client served by the desktop app — none of which Reverso offers as a
local, account-free workspace.

Not covered: Reverso's licensed example corpus itself. Qalam's concordance uses open
OpenSubtitles data, so the examples are different and the bundled 2,000,000-pair samples
are not the full multi-gigabyte corpus. A Reverso account backend is also absent (the
optional `SyncFolder` can sync the JSON files through OneDrive or Dropbox, but Qalam
has no account or server), as are Office/browser add-ins and Reverso's curated
grammar/conjugation pages (Qalam reads Wiktionary via Kaikki instead). Qalam is a
local replacement, not a byte-for-byte clone. The global shortcut already works inside
Office and browsers, so no add-in is needed for the lookup workflow. The browser
client is served from the local desktop app; there is no standalone native iOS or
Android binary.

## Limitations

- **Character data is a character dictionary.** The bundled Unihan table covers BMP
  characters with concise glosses; it does not include word entries, example sentences,
  etymology, or characters outside the BMP. Stroke outlines are downloaded per character
  and need one connection unless already cached.
- **Selection acquisition.** Qalam asks UI Automation for the focused control's
  selected text first, then falls back to the clipboard. An app that exposes neither a
  UIA text selection nor Ctrl+C copying cannot be supported.
- The clipboard fallback treats an unchanged first copy as "nothing was selected" to
  avoid stale text. This only affects apps without UIA; use the tray's **Show last
  result** if you hit it.
- **Examples are not Reverso's.** Qalam searches whole phrases as well as words, and
  adds curated usage lines for known Spanish/English slang. The local concordance is
  sampled OpenSubtitles dialogue; the web fallback is Tatoeba and MyMemory, which are
  open, volunteer corpora. Rare phrases, dialects, and some language pairs still return
  fewer examples or none; the popup omits the section when it is empty.
- **The bundled corpus is a large open sample, not Reverso's dump.** English ↔ Spanish
  and Arabic ↔ English have 2,000,000 aligned pairs per direction; 12 additional pairs
  have 100,000, plus the 50,000-pair seed and small topical packs.
  A full OpenSubtitles TMX is several gigabytes; build an even larger pack with
  `--buildconcordance --index` if you need more coverage. Corpus alignments can be
  noisy, and the translation hint only ranks matches; it cannot verify that a subtitle
  pair is semantically perfect. Seed packs are held in memory; depth packs keep their
  text and postings on disk and read candidate rows through `.qidx`.
- **The index is token-based.** Arabic conjunction/article prefixes are normalized, but
  scripts without word spaces (Chinese, Japanese, Thai) need a different tokenizer
  before a user-built pack becomes broadly searchable. The bundled packs are
  English/Spanish/Arabic.
- Synonyms come from Google's dictionary and are only requested for short queries; not
  every word or language pair has an entry.
- Google's endpoints are unofficial and can return HTTP 429. The parallel endpoints and
  MyMemory fallback reduce, but cannot remove, that dependency.
- PDF translation works on selectable text, not scanned images. It reuses the original
  text boxes, so unusually complex multi-column layouts can produce a layout warning;
  Qalam keeps the original passage rather than deleting it.
- The document translator does not add tashkeel automatically. Use the popup or the main
  window on the Arabic text you want vocalized.
- Qalam does not bundle an offline translation model. `offline` mode uses a separately
  installed Ollama server and model; the curated phrasebook works without either.
- The offline provider is limited to selections of 1,400 characters or fewer. In `auto`
  mode the slang-specific local path is limited to 400 characters; when Ollama is
  unreachable, `auto` falls back to the online services.
- Grammar and conjugation coverage depends on Wiktionary. A word with no open entry, or
  a dialect form not described there, shows no grammar card rather than guessed forms.
  Arabic dialect verbs often have no inflection table; MSA verbs such as `كتب` do.
- The grammar cache is capped at 400 entries and has no expiry. A cached card can
  therefore be older than the current Wiktionary page; delete `grammar-cache.json` to
  force fresh network lookups.
- The desktop app is Windows-only. The browser client works in desktop browsers and
  phones on the same LAN, but there is no standalone native iOS/Android app. The
  tashkeel worker adds roughly 80–100 MB of RAM while idle.
- **Study stays local.** The deck is a JSON file shared by the desktop and browser
  clients and synced only if `SyncFolder` points at a shared folder. There is no Qalam
  account or cloud deck. The browser URL contains a local access token; treat it as a
  password and turn the server off when not needed.

## Files

| Path | Purpose |
| --- | --- |
| `src/Qalam.cs` | Application controller, hook, popup, translation, and tray |
| `src/Colloquial.cs` | Curated Spanish and Arabic-dialect phrasebook, composition, and language detection |
| `src/LocalModel.cs` | Optional local Ollama translation for slang and the offline provider |
| `src/Grammar.cs` | Wiktionary/Kaikki grammar and conjugation lookup |
| `src/Concordance.cs` | Local sentence-pair index, TMX/TSV pack builder, and query/test tools |
| `src/DiskConcordance.cs` | Binary `.qidx` writer and disk-backed pack reader |
| `src/LanguageCatalog.cs` | Single 73-language table for popup, tray, documents, and web |
| `src/Mobile.cs` | LAN HTTP server, token auth, JSON API, Windows package builder, and browser test/serve tools |
| `mobile/` | Responsive Context UI, manifest, and icon |
| `src/TranslationCompare.cs` | Headless online-versus-offline comparison |
| `src/MainWindow.cs` | Taskbar/Start Menu window and full translation workspace |
| `src/SingleInstance.cs` | Single-instance activation for pinned launches |
| `src/Documents.cs` / `src/PdfDocuments.cs` | DOCX translation and PDF bridge client |
| `documents/pdf_translate.py` | PyMuPDF geometry extract/apply bridge |
| `tashkeel/tashkeel_server.py` | Resident JSON-lines Mishkal and pypinyin worker |
| `src/Characters.cs` | Unihan character table loader and Hanzi Writer stroke data |
| `src/CharacterWindow.cs` | Character/radical dictionary window and stroke-order animation |
| `src/Study.cs` | Local study deck, scheduling, and JSON storage |
| `src/StudyWindow.cs` | Study review window and deck self-test |
| `data/characters.tsv` | Bundled Unihan character table (27,321 characters) |
| `tools/build_characters.py` | Regenerates `data/characters.tsv` from Unihan and CJKRadicals |
| `%LOCALAPPDATA%\Qalam\strokes\*.json` | Cached stroke outlines, one file per character |
| `%LOCALAPPDATA%\Qalam\data\characters.tsv` | Installed copy of the character table |
| `assets/` | Qaaf mark (PNG sizes and `qalam.ico`) and `make_logo.py` |
| `build.ps1` | Compiles with the in-box C# 5 compiler |
| `install.ps1` / `uninstall.ps1` | Per-user install and takeover / restore |
| `update.ps1` | Out-of-process updater: waits for Qalam to exit, verifies the setup hash, installs, and restarts |
| `selftest/` | Rendered popup PNGs, trigger harness, and [real-app tests](selftest/APP-TESTS.md) |
| `concordance/*.tsv` | Seed, depth, slang, and dialect sentence-pair packs copied by the installer |
| `concordance/*.tsv.qidx` | Disk-backed indexes for the depth packs |
| `%LOCALAPPDATA%\Qalam\concordance\*.tsv` | Installed concordance packs, loaded on first use per language pair |
| `%LOCALAPPDATA%\Qalam\mobile\` | Installed browser/mobile client served by the LAN server |
| `%LOCALAPPDATA%\Qalam\grammar-cache.json` | Cached definitions/conjugations, up to 400 entries |
| `%LOCALAPPDATA%\Qalam\study.json` | Local spaced-repetition deck, up to 1,000 cards |
| `dist/Qalam.exe` | Build output |

## Development

```powershell
./build.ps1
"%LOCALAPPDATA%\Qalam\Qalam.exe" --selftest --out "C:\path\to\output"
```

`--selftest` runs the worker, performs live translation, dictionary, grammar, dialect,
and context lookups, checks the Spanish and newer Arabic colloquial phrases end to end,
and renders seven popups to PNG without showing a window — sentence pairs in both
directions, pinyin, the slang result with its context, a real word result with
synonyms and examples, and Spanish and Arabic grammar cards. This is useful for checking Arabic shaping and harakat
placement.

`--grammartest --out FILE` checks the open-data parser without starting the WPF app or
the tashkeel worker, then creates a fresh service and reads the result back with
network access disabled to verify `grammar-cache.json`.

`--charactertest --out FILE` loads the bundled Unihan table, checks pinyin, radicals,
stroke counts, and traditional/simplified variants for sample characters, downloads or
reads the cached stroke outline for `好`, and renders `character-stroke-hao.png` plus a
full `character-window.png` next to the report.

`--studytest --out FILE` checks the interval/ease scheduling for Good, Easy, and Again,
then renders `study-window.png` next to the report.

`--mobiletest --out FILE` starts the real LAN server on loopback, rejects a request
without the token, fetches the page/CSS/JS/manifest, translates a phrasebook line,
adds a study card, grades it, and verifies the persisted interval. It exits nonzero on
any failed assertion. `--mobileserve --port 8787 --token TOKEN --out URL.txt` keeps the
same server running for browser checks; open the URL it writes.

`--concordancetest --out DIR` builds small TMX fixtures in both directions with
`--index`, loads the resulting qidx packs with no network, and checks exact-phrase
ranking, slang, vocalized Arabic, reverse packs, a missing query, and translation-hint
ordering.
`--buildconcordance --tmx FILE --from LANG --to LANG` writes a pack (or both directions);
`--findconcordance --query TEXT --from LANG --to LANG` prints ranked matches from an
installed pack. See [Local concordance](#local-concordance).

`--compare --out DIR` runs the
fixed online-versus-offline set and
writes `compare.md`, `compare.txt`, and `compare.json`. The checked-in run is in
`selftest/compare/`; it reports the model, latency, script checks, outputs, and token-F1
scores. The score is a diagnostic, not a claim of human-equivalent quality.

`--docselftest --input FILE --output FILE --target ar` runs the document pipeline
headlessly and writes a report next to the output. The fixtures in `selftest/docs` use
`make_layout_samples.py`; see `selftest/docs/layout-en-report.txt` and
`selftest/docs/layout-ar-report.txt` for the last runs.

## Uninstall

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File uninstall.ps1
```

This stops Qalam, removes both shortcuts, restores Reverso's autostart entry if it is
still absent **and its executable still exists**, and deletes `%LOCALAPPDATA%\Qalam`.
After Reverso has been uninstalled, no dead startup entry is created. Use `-KeepFiles`
to keep settings and logs.
