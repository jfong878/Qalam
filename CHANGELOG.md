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

## 1.0.11 — 2026-11-03

Transliteration correctness batch. The nisbah is written -eeyah (us-Soodaaneeyah, 'Arabeeyah), فِي gives fee, a word-initial hamza is silent (akthar), a tāʾ marbūṭah closes in -ah before a plain word, a noun governed by a preposition carries its i onto the following article (al-Lughat il-'Arabeeyah), and بخلص has a vocalized form in Fuṣḥā as well as the Sudanese bakhliṣ.

## 1.0.10 — 2026-11-03

Lahjah readings are now chosen per register where the dialects differ: Sudanese says bakhliṣ for بخلص, while the Egyptian and Gulf registers keep bakhalliṣ. The raw sentence ان شاء الله بعد بخلص من الشغل اليوم reads In Shaa' Allaah ba'd bakhliṣ min ash-Shughl il-Yawm in the Sudanese register.
