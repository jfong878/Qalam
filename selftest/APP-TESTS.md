# Qalam application selection tests

The capture tests were rerun against the then-installed Qalam build on 2026-11-01,
using temporary profiles/documents so no user files or tabs were modified. The
2026-10-31 baseline is noted where a capture path changed.

> Build note: the package installer was fixed after the capture run, and the installed
> build is now `4bbecfbc…` rather than the `f9ce4c41…` build used for the table below.
> `--selftest` passed on the new build; the Chrome, VS Code, and Word capture tests
> were not rerun on it. See `.launch-check/RELEASE-TEST.md` for the clean-install
> status.

| Application | Capture path | Result | Evidence |
| --- | --- | --- | --- |
| Chrome (local app window) | Clipboard fallback | Pass | `trigger accepted: chrome (clipboard)`; clipboard matched the 58-character selection (2026-11-01) |
| VS Code (fresh profile) | Clipboard fallback | Pass | `trigger accepted: code (clipboard)`; clipboard matched the 59-character selection (2026-11-01) |
| Word (normal window, temporary RTF) | UI Automation | Pass | `uia selection chars=54`; `trigger accepted: winword (uia)`; clipboard matched the 54-character selection (2026-11-01) |
| Notepad | Clipboard fallback | Blocked | Notepad PID 18396 has an unsaved document open; the test refuses to close or alter it (2026-11-01) |

## Notes

- Chrome's first run logged `uia timeout` after 400 ms and correctly fell back to
  the clipboard. A later run exposed the selection through UI Automation. Both
  paths produced the exact text.
- VS Code is Electron and does not expose a `TextPattern` selection in this setup;
  the clipboard fallback is the working path.
- Word's COM automation hangs on this installation even though normal Word starts
  fine. The test therefore drives a real Word window with a temporary RTF instead
  of COM. That is the same selection/copy path a user exercises.
- No application-specific Qalam code change was needed: the UIA-first,
  clipboard-fallback reader covered all three.
- Notepad remains unverified because testing it would have required touching an
  existing user session (PID 18396, unsaved document). The native WinForms text-box
  trigger test in this folder exercises the same clipboard path.

## Final build verification (2026-11-01 17:05)

The installed binary matched `dist/Qalam.exe` (SHA-256 `f9ce4c41…`) and the headless
suites were rerun against it:

- `--selftest`: 8 concordance packs, 5,105,065 pairs, 75 target-menu items, and seven
  popup renders.
- `--grammartest`, `--charactertest` (27,321 characters), `--studytest`, and
  `--concordancetest` passed.
- `--mobiletest` passed every server/page/translation/study/package check; the
  generated Windows package was 8,626,937 bytes.
- DOCX self-test: 11 units, 0 warnings; PDF self-test: 3 blocks, 0 warnings.
- `/api/rewrite` returned a valid suggestion with `engine=local-edit` after the local
  `qwen2.5vl:3b` model was restored.
- App capture rerun on this build: Chrome (clipboard), VS Code (clipboard), and Word
  (UIA) all matched the selected text. Notepad remains blocked (see above).

The 2026-10-31 runs were the original baseline; the table above records the 2026-11-01
rerun. That rerun added a foreground check and a clipboard-match requirement, so a
capture from another app cannot be recorded as a pass.

## Added provider and grammar checks

- `Qalam.exe --grammartest --out selftest\grammar-cache.txt` parsed `hablar`, `comer`,
  `parler`, `student`, and `كتب` from Kaikki/Wiktionary. Spanish verbs returned eight
  tense groups, French six, and `student` returned noun definitions. Arabic `كتب`
  returned verb definitions and, after the form parser was extended, past, present,
  and imperative groups plus participles.
- The headless `--selftest` now checks the popup target event, the 75-item target menu
  (Auto, 73 languages, and a separator), the Arabic dialect phrasebook, and the
  English → Arabic reverse path. It does not perform a real pointer click on the menu.
- `Qalam.exe --compare --out selftest\compare` ran 17 sentences through the online and
  offline providers. The checked-in report is `selftest/compare/compare.md`.

## Grammar and conjugation

Grammar lookup was exercised headlessly on 2026-11-01 against the installed build:

- Spanish `hablar`: 8 tenses, present through imperfect subjunctive.
- French `parler`: 6 tenses, including future and conditional.
- Arabic `كتب` and vocalized `كَتَبَ`: 3 tense groups (past, present, imperative),
  gender/number forms, verbal noun, and active/passive participles.

`--selftest` renders `final/popup-grammar-es-en.png` and
`final/popup-grammar-ar-en.png`. A real double-Ctrl+C selection of `hablar` logged
`grammar=ok`; the screen-capture harness is in `trigger-grammar-test.ps1`.

### Offline cache

On 2026-11-01, after caching `hablar`, Qalam was switched to `TranslationMode:
offline` and the real trigger was run again. The offline model was unavailable for
that selection, so the popup showed `Offline translation is unavailable`, but the
log continued:

```
grammar cache loaded: 5
grammar=cached
```

The popup still rendered `GRAMMAR · SPANISH · VERB · CACHED` with the full eight-tense
conjugation. `selftest/grammar-popup-installed-crop.png` captures that state. A fresh
`--grammartest` service also read `hablar` back with network disabled and reported
`cache_has=True from_disk=True`; the run is in `selftest/grammar-cache.txt`.

The configuration was restored to `auto` afterwards.

## Local concordance

On 2026-11-01 the installed build was checked against the seed packs copied to
`%LOCALAPPDATA%\Qalam\concordance`:

- `--concordancetest` built four fixture packs from TMX and passed exact phrase,
  `no mames`, vocalized `إِزِيك`, Arabic prefix (`كتاب` → `الكتاب`, `مدرسة` →
  `المدرسة`), reverse-direction, missing-query, and translation-hint checks
  (`selftest/concordance/concordancetest.log`).
- `--findconcordance --query "cabrón" --from es --to en --hint "that bastard"` returned
  eight real OpenSubtitles lines, with `No volverá, el cabrón. => The bastard's not
  coming back.` first. The Hindi/English console could not print the accent, but the
  UTF-8 report (`selftest/concordance/query-cabron-hint.txt`) is correct.
- `--findconcordance --query "كيفك" --from ar --to en` returned Levantine dialogue such
  as `كيفك يا جيف ؟ => " -How are you, Jeff?"` from the dialect pack.
- `--findconcordance --query "كتاب" --from ar --to en` matched `الكتاب` lines, and
  `--query "مدرسة"` matched `المدرسة` lines after Arabic prefix stemming.
- The installed `--selftest` returned four corpus examples for `student` (English →
  Arabic) and rendered them in `selftest/final-concordance/popup-word-en-ar.png` with
  `Examples: OpenSubtitles v2024 (sampled) (ODC-BY)` in the credit line.

The seed packs are samples: 50,000 general pairs per direction from OpenSubtitles
v2024, plus 427 Spanish/English slang pairs and 774 Arabic dialect pairs. They are
ODC-BY, attributed in the pack header and popup. No network is used for concordance
queries. Packs are loaded lazily by language pair: the installed log shows
`pair=es-en packs=2 loaded=20311 ms=683` and `pair=en-ar packs=4 loaded=20709 ms=633`
for the two directions exercised by `--selftest`, while app startup itself performs no
pack load.

## Reproduce

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\selftest\test-chrome.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\selftest\test-vscode.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\selftest\test-word.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\selftest\trigger-grammar-test.ps1
& "$env:LOCALAPPDATA\Qalam\Qalam.exe" --concordancetest --out .\concordance
& "$env:LOCALAPPDATA\Qalam\Qalam.exe" --findconcordance --query "cabrón" --from es --to en --out .\query.txt
```

The application tests write `app-test-<name>.json`; the trigger harnesses log through
Qalam's own log and save screenshots. `ui-test-lib.ps1` contains the window activation
and log readers. It refuses to send keys unless the target window is actually
foreground.
