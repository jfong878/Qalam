# How Reverso's double Ctrl+C popup works

Analysis target: Reverso Context for Windows, version **2.17.2.970**, installed at
`C:\Users\jfong\AppData\Local\Reverso\Reverso\`.

Method: black-box observation plus static inspection of the shipped files — no
disassembly of proprietary code was needed, and no Reverso code or binaries are used by
Qalam. Findings below are labelled **observed** (seen directly), **derived** (follows
from observations), or **unknown**.

## Observed

- The app auto-starts from
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\Reverso` with
  `"…\Reverso.exe" -minimized`. Its `settings.json` contains only window position,
  reminder state, `AppId`, `Token`, and UI language — no hotkey setting.
- `app.log` records:
  ```
  HotkeyService RegisterHotKey ctrl+c+double
  ```
  followed by `QuickSearchUI.QuickSearchWindow|JS Injected`.
- The install ships `QuickSearchCore.dll`, `QuickSearchRuntime.dll`,
  `QuickSearchUI.dll`, `Interop.UIAutomationClient.dll`, WebView2 assemblies, and WPF
  XAML resources `quicksearchwindow.xaml`, `quicksearchicon.xaml`, `edgealertwindow.xaml`.
- The same log repeatedly shows `QuickSearchRuntime.Services.AutomationService`
  subscribing/unsubscribing and failing with UI Automation COM errors
  (`0x80040201`, `0x8000FFFF`) when accessibility access fails.
- Reflection over `QuickSearchRuntime.dll` exposes:
  `QSHelper+<HotKeyPressed>`, `AutomationService`, `AutomationServiceThreadHelper`,
  `TextSelectionReader` (including `GetTextViaClipboard`),
  `TextSelectionHelper` (including `GetTextClipboard`, `Win32Point`,
  `WindowEnumProc`), and `Clipboard`.
- Strings in the assemblies include `QuickSearchAvailableApps - do not display nothing`,
  `QUICK_SEARCH_TOOLTIP_SHORT_SELECTION`, `QUICK_SEARCH_TOOLTIP_LONG_SELECTION`,
  `ApiDisableQuickSearch`, `disableQuickSearch`, `hotkey`, `reversoTranslate`, and
  messages about Microsoft Teams / Edge / WebView2.

## Derived

- The trigger is a global double-Ctrl+C chord. A single `RegisterHotKey` cannot express
  a key sequence, so the `HotkeyService` abstraction must implement the double-press
  detection itself (low-level hook or an equivalent), not a plain registration.
- On trigger, Reverso reads the current selection through UI Automation
  (`TextPattern`-style access), with a clipboard path as fallback. The type names,
  the repeated UIA failures, and the fallback names all point the same way.
- The result is shown in a WPF window that hosts WebView2 and receives injected
  JavaScript — i.e. the popup is a small web view, not native text controls.
- The app is restricted to an allowlist of host applications and has separate tooltips
  for short and long selections.

## Unknown

- The exact double-press window and the minimum/maximum selection lengths (constants
  live in IL method bodies that were not decompiled).
- The contents of the application allowlist.
- Whether authentication/telemetry calls are required for quick search.

## What Qalam does differently

| Aspect | Reverso | Qalam |
| --- | --- | --- |
| Selection source | UI Automation first, clipboard fallback | UI Automation first, clipboard fallback |
| Trigger detection | Custom double-Ctrl+C hotkey | `WH_KEYBOARD_LL` hook, 600 ms window |
| Popup | WPF + WebView2, injected JS | Native WPF |
| Translation | Reverso service | Google public endpoints, raced, MyMemory fallback |
| Context examples | Reverso's licensed corpus | Local OpenSubtitles concordance first; Tatoeba and MyMemory as fallback |
| Synonyms | Reverso dictionary | Google dictionary endpoint, host fallback |
| Grammar / conjugation | Reverso grammar pages | Wiktionary via Kaikki, open data |
| Pronunciation | Reverso audio | Google TTS |
| History / favorites | Reverso history and account favorites | Last 30 local entries plus a local favorites list; no account sync |
| Tashkeel | Not offered | Local Mishkal worker |
| Offline translation | Not offered | Optional local Ollama provider; phrasebook works without it |
| Document translation | Reverso document feature | Format-preserving DOCX/PDF output |
| Target languages | Reverso's set | 25 choices; Arabic ↔ English in auto mode; switchable inside the popup |
| Scope | Allowlisted apps | Any app where Ctrl+C copies or UIA exposes a selection |

Qalam's UIA reader is deliberately small: focused element, `TextPattern.GetSelection()`,
password fields skipped, 400 ms timeout, then the clipboard path. It covers apps that
never put selections on the clipboard, while the fallback covers apps whose UIA provider
does not implement `TextPattern`. Apps that do neither remain unsupported. The clipboard
fallback still treats a byte-identical first copy as "no selection" so stale text is not
shown; the UIA path does not have that limitation.

## Parity caveat

The popup workflow is covered, but the data is not identical. Reverso's example pairs
come from its own licensed corpus; Qalam now searches a local concordance built from
open OpenSubtitles dialogue before falling back to Tatoeba (CC BY 2.0 FR) and MyMemory.
The bundled packs are samples (50,000 general pairs per direction plus small slang and
dialect packs), not the full corpus, so coverage still differs. Grammar and conjugation
are read from Wiktionary via Kaikki instead of Reverso's curated pages, so coverage
differs there too. Reverso account features (favorites, sync) and Office/browser add-ins
have no Qalam equivalent. Qalam adds local favorites/history, format-preserving
DOCX/PDF translation, offline tashkeel, and an optional local Ollama translation
provider. Its offline mode is not a licensed offline model; it is a separately installed
local model, so quality and speed depend on that model.

## Removal note

Reverso 2.17.2.970 was uninstalled from this machine after Qalam passed the trigger, UIA,
clipboard, and tashkeel checks. The observations above come from the install, settings,
log, and assemblies inspected before removal; no Reverso files remain.
