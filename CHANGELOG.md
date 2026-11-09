# Changelog

All notable changes to Qalam. Versions follow Semantic Versioning; the
release feed at `latest.json` carries the same text as each entry here.

## Policy

- **MAJOR** for breaking changes to the config file, the worker protocol, or the
  install layout.
- **MINOR** for a completed, tested feature milestone.
- **PATCH** for a batch of backwards-compatible fixes, released together.
- Pre-release identifiers (`1.1.0-alpha.1`) for internal iterations; the core
  integers move only when the batch is finished.

## Unreleased

Single-word lookups now show a local **Senses** card. Lane's *Arabic-English Lexicon*
supplies classical Arabic root senses and CC-CEDICT supplies modern Mandarin word
entries, each labeled by era and source. When a translation is available, its words
rank the senses and the card credit names the ranking context; this is not full
sentence disambiguation. The packs are bundled, work offline, and are attributed under
their open licenses. The translation path is unchanged; Hans Wehr is not bundled.

## 1.0.0 — 2026-11-03

Initial public Windows build with automatic GitHub updates. The setup is unsigned; Windows SmartScreen may warn on first download.

## 1.0.1 — 2026-11-03

Qalam now keeps checking for updates while it stays open. New releases download in the background and install when you quit or choose Restart.

## 1.0.2 — 2026-11-03

Context examples now rank casual dialogue above flat prose. Each candidate line is scored on how spoken it sounds, using particles, address terms, interjections, profanity and dialect forms, with polite and official phrasing counted against it. A colloquial line now leads when it matches as well as a formal one; exact phrase matches still come first. This covers every language Qalam ships, including Chinese and Japanese.

## 1.0.3 — 2026-11-03

Arabic phrases now carry their Lahjah label: Egyptian, Levantine, Gulf, Sudanese, Iraqi, Yemeni, Hijazi, Moroccan, Tunisian, Algerian, Libyan, and MSA. A phrase shared across dialects carries every label, so Māshī is Sudanese, Gulf, and Egyptian at once, and the phrasebook grew from 108 to 144 entries. Tashkīl and the Listen button now follow a pronunciation register: Fuṣḥā keeps Mishkal's case endings and Tanwīn, while a Lahjah register uses the spoken vowels, drops the ʾIʿrāb, and sends that vocalized line to the speech service. Choose it from Tray > Pronunciation or with SpokenLahjah in config.json. If you are on 1.0.2 or earlier, run this installer once by hand: those builds still point at the retired Qalam-releases repo and cannot find updates on their own.

## 1.0.4 — 2026-11-03

Arabic phrases now carry their Lahjah label — Egyptian, Levantine, Gulf, Sudanese, Iraqi, Yemeni, Hijazi, Moroccan, Tunisian, Algerian, Libyan, and MSA — and a phrase shared across dialects carries every label, so Māshī is Sudanese, Gulf, and Egyptian at once. The phrasebook grew from 108 to 144 entries. Tashkīl and the Listen button now follow a pronunciation register: Fuṣḥā keeps Mishkal's case endings and Tanwīn, while a Lahjah register uses the spoken vowels, drops the ʾIʿrāb, and sends that vocalized line to the speech service. Each Lahjah has its own pronunciation lexicon, so an Iraqi register does not rewrite Moroccan words. Choose it from Tray > Pronunciation or with SpokenLahjah in config.json. If you are on 1.0.2 or earlier, run this installer once by hand: those builds still point at the retired Qalam-releases repo and cannot find updates on their own.

## 1.0.5 — 2026-11-03

Arabic phrases now carry their Lahjah label — Egyptian, Levantine, Gulf, Sudanese, Iraqi, Yemeni, Hijazi, Moroccan, Tunisian, Algerian, Libyan, and MSA — and a phrase shared across dialects carries every label, so Māshī is Sudanese, Gulf, and Egyptian at once. The phrasebook grew from 108 to 144 entries. Tashkīl and the Listen button now follow a pronunciation register: Fuṣḥā keeps Mishkal's case endings and Tanwīn, while a Lahjah register uses the spoken vowels, drops the ʾIʿrāb, and sends that vocalized line to the speech service. Each Lahjah has its own pronunciation lexicon, so an Iraqi register does not rewrite Moroccan words. Choose it from Tray > Pronunciation or with SpokenLahjah in config.json. This build also adds a Transliteration line in the Abū Ṣāliḥ scheme under the Tashkīl card, a Check for updates button beside Report a bug, and an automatic repair for installs still pointing at the retired Qalam-releases feed, which made Check for updates return 404. If you are on 1.0.2 or earlier, run this installer once by hand: those builds still point at the retired Qalam-releases repo and cannot find updates on their own.

## 1.0.6 — 2026-11-03

The transliteration no longer writes a vowel after a closing hamza — shaa', not shaa'a — and keeps the strict and the spoken line distinct. Tashkīl repairs the words Mishkal mis-vocalizes, so al-Salām no longer comes back as al-Sullām and al-Ṣalāh no longer carries a spurious vowel. The spoken line drops the ʾIʿrāb and gives unvocalized Lahjah words their vowels, and the Listen button speaks that same vocalized line.

## 1.0.7 — 2026-11-03

The spoken line now reads as you wrote it: In Shaa' Allaah ba'id bakhalliṣ mina ash-Shughl il-Yawm. The genitive i of Shughl carries onto the article as il-, and bakhalliṣ keeps its kasra rather than collapsing to bakhallṣ.

## 1.0.8 — 2026-11-03

The Transliteration line now follows the pronunciation register you chose, so a Lahjah setting changes the romanization as well as the Tashkīl and the voice; Fuṣḥā still shows the case endings. In Shaa' Allaah ba'id bakhalliṣ mina ash-Shughl il-Yawm is what the Sudanese register gives for your example.

## 1.0.9 — 2026-11-03

Unvocalized input is read by context: a bare ان with no shadda is the conditional In, not anna, so ان شاء الله بعد بخلص من الشغل اليوم becomes In Shaa' Allaah ba'd bakhalliṣ min ash-Shughl il-Yawm. Formulae such as In Shaa' Allaah, Bismillaah, Al-Ḥamdu lillaah, As-Salaamu 'alaykum, Maa Shaa' Allaah, Subḥaan Allaah, Allaahu Akbar and Laa ilaaha illaa Allaah keep their Fuṣḥā shape in every register; a preposition keeps the plain article after it (min ash-shughul) while the genitive i still carries onto a following article (ash-Shughl il-Yawm).

## 1.0.34 — 2026-11-07

Running `uninstall.ps1` directly now removes the desktop shortcut, the Add/Remove Programs entry, and `HKCU\Software\Qalam`, so a manual uninstall no longer leaves links or a registration behind. `-KeepFiles` still removes those shortcuts and the registration while keeping settings and logs.

## 1.0.32 — 2026-11-04

The compact pop-up now has a View in App action that opens the completed source and translation in the main window without re-running the translation or adding a duplicate history entry. Formal Arabic that happens to contain a dialect word such as طيب is no longer sent to the local slang model, and the normal translation endpoint is now the primary online path; the reported passage now keeps the righteous man and honoring his parents instead of Saleh and from his parents.

## 1.0.31 — 2026-11-04

PDF translations now preserve exact copy text for Arabic, Hebrew, Persian, Urdu, Hindi, and Thai. Every inserted translation carries PDF ActualText, so readers that support marked content copy the translation instead of the font's shared glyphs or contextual forms. The 13-block, 15-language formatting suite now runs against the built executable before any release draft is created and refuses the release on a layout, font, report, or copy-text regression; the fixture and runner ship with the test suite.

## 1.0.30 — 2026-11-04

Arabic PDFs now copy as standard Arabic instead of PUA placeholders. Qalam rebuilds each translated font's ToUnicode map from its actual HarfBuzz glyph clusters, so ligatures copy as their real letters and the separate dot glyphs are dropped on read-back. The page rendering and the source font stay unchanged, and English-to-Arabic followed by Arabic-to-English now round-trips with clean text. The PDF helper now installs `uharfbuzz` and `fonttools` alongside PyMuPDF for this repair.

## 1.0.29 — 2026-11-04

The translated sentence is now a reading surface: click any word and the voice starts from that word, continuing to the end of the passage, while the Listen button switches to Pause. The Slow toggle now changes speed immediately instead of letting the buffered normal-speed audio finish first. Works in the main window and the compact pop-up.

## 1.0.28 — 2026-11-04

Document translation now pauses for review before it writes a file. Every translated passage appears beside its source so you can correct the wording, and clearing a passage leaves it in the original language. Qalam writes the PDF or DOCX only when you choose Save; Cancel leaves the original untouched. The progress window steps aside while you edit.

## 1.0.27 — 2026-11-04

Scanned PDFs now translate. Qalam reads pages that have no text layer with the Windows OCR engine, picks the installed OCR language that reads the page best, and writes the translation into the same layout. The scanned glyphs are erased in place and the translation sits on the cleaned page, so a scan no longer shows two languages stacked on top of each other. Digital PDFs keep the 1.0.26 formatting work, and the PDF helper now receives the same measured geometry for both reading and writing. Add more OCR languages in Windows Settings > Time & language > Language & region.

## 1.0.26 — 2026-11-04

PDF translation now preserves bullet lines, inline bold and italic labels, the source font family, and readable type sizes. Each block gets room to grow into nearby whitespace before text is shrunk, so translated resumes keep their hierarchy instead of collapsing into tiny paragraphs. DOCX translation is unchanged; a PDF still reflows long translations within its original blocks because the format has no text-flow model.

## 1.0.25 — 2026-11-04

Audio now starts after the first 180-character chunk instead of waiting for the whole passage. Chunks are cached in %LOCALAPPDATA%\Qalam\audio-cache, so replaying the same text or resuming after pause/slow opens instantly, and the Listen button shows Loading while the first chunk is fetched. Remaining chunks download one at a time in the background; parallel requests are throttled by the speech service.

## 1.0.24 — 2026-11-04

Every Listen button now cycles through play, pause and slow (0.65×) playback. The label shows the next action — Listen, Pause, Slow or Play — and paused audio resumes from its position instead of restarting.

## 1.0.23 — 2026-11-04

The main window keeps a 10,000-character ceiling, twice Reverso Premium's 5,000-character limit. Installs still carrying the popup-era 600 default move to 10,000 on load, and a selection over 600 characters still opens in the main window instead of the popup.

## 1.0.22 — 2026-11-04

Detection corrections after testing the resolver against real sentences: German and Portuguese no longer read as Spanish, Italian is recognized instead of falling back to English, kana-only Japanese is spoken as Japanese, and a French sentence tagged Spanish (or the reverse) is re-read locally instead of trusting the wrong tag. Each language now carries one Listen button, placed beside the text it speaks: the popup keeps the source and translation buttons and drops the footer duplicate, while the main window keeps the original-text button and drops the one in the action row.

## 1.0.21 — 2026-11-04

Audio language detection now follows the text: script evidence decides first, the detected or target tag is normalized against the catalog, and Latin fallbacks distinguish French from Spanish by function words and accents, with lists for Catalan, Galician, the Nordic languages, Czech, Slovak, Croatian, Serbian, Bosnian, Slovenian, Estonian, Lithuanian, Latvian, Icelandic, Afrikaans, Swahili, Filipino, Malay, Esperanto and Latin. A French selection now speaks French instead of Spanish in the popup, the main window, and the input Listen button.

## 1.0.20 — 2026-11-04

Listen buttons now sit beside the source and the translation in the Ctrl + C + C popup and in the main window, and the main window adds one for the input box. A selection longer than 600 characters opens in the main window instead of the popup, and the main window accepts up to 10,000 characters. Speech requests now carry the parameters Google requires, so audio plays again instead of reporting "Audio unavailable."

## 1.0.17 — 2026-11-03

Fuṣḥā repairs only the words Mishkal could not read and leaves every word carrying a short vowel exactly as it stands, so btabda' no longer collapses to Btbd' and وبتخلص reads wabatakhalliṣ.

## 1.0.16 — 2026-11-03

Kashida and zero-width marks are dropped before reading, so text copied out of a justified page no longer turns بـتـبـدأ into Batabadaa or الـيـوم into al-Ayawam.

## 1.0.15 — 2026-11-03

The installer rejected the Windows Store Python aliases because they are zero-byte stubs, so every install aborted with "Python 3 is required" before copying a single file. Candidates are now run instead of measured, with a `py -3` fallback.

## 1.0.14 — 2026-11-03

The updater no longer depends on GitHub's /releases/latest alias, which lags a fresh publish and served 404 for the installer; the manifest now carries an absolute versioned URL and a 404 on the feed falls back to the tag reported by the API.

## 1.0.13 — 2026-11-03

Text that already carries harakaat is no longer re-vocalized, so Fuṣḥā stops inventing i'raab the writer never wrote (bitakhalluṣi ish-Shughli → bitakhalluṣ ash-Shughl).

## 1.0.12 — 2026-11-03

Fixes the alif maqṣūrah (mataa, not mataaa), a word-final hamza (btabda'), and the article after a consonant-final word (ash-Shughl, not ish-Shughl). Sudanese وبتخلص reads wa bitkhalliṣ, and the per-register tables cover Egyptian, Levantine, Gulf, Iraqi and Moroccan.

## 1.0.11 — 2026-11-03

Transliteration correctness batch. The nisbah is written -eeyah (us-Soodaaneeyah, 'Arabeeyah), فِي gives fee, a word-initial hamza is silent (akthar), a tāʾ marbūṭah closes in -ah before a plain word, a noun governed by a preposition carries its i onto the following article (al-Lughat il-'Arabeeyah), and بخلص has a vocalized form in Fuṣḥā as well as the Sudanese bakhliṣ.

## 1.0.10 — 2026-11-03

Lahjah readings are now chosen per register where the dialects differ: Sudanese says bakhliṣ for بخلص, while the Egyptian and Gulf registers keep bakhalliṣ. The raw sentence ان شاء الله بعد بخلص من الشغل اليوم reads In Shaa' Allaah ba'd bakhliṣ min ash-Shughl il-Yawm in the Sudanese register.
