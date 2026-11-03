# Ribaaṭ Transliteration System — The Eight Core Rules

Every title, tooltip, hover card, catalog entry, and transliteration across the
Ribaaṭ Initiative site (`index.html`, `quran-reader.html`), the Maktabat-Abee-Ṣaaliḥ
library app, and the Anṣaar-us-Sunnah waqf site follows one fixed spelling system,
so the Arabic is pronounced exactly the way it is written.

This system **replaces** the legacy Quran.com-style word-by-word transliteration
output everywhere: the Muṣḥaf Reader intercepts upstream WBW transliteration
strings at load time (`sanitizeGloss` in `quran-reader.html`) and renders every
Muṣḥaf word token through this ruleset instead, keyed by its immutable word
coordinate (`surah:ayah:word`). Audio sync tags, verse-level cards, and
Riwaayaat variant modules preserve those same coordinates; only the rendered
transliteration changes.

| Rule | Governs | Section |
|------|---------|---------|
| 1 | Alif Madd (آ) strictly 'Aa' | §10 |
| 2 | Radical soft Haa' vs Taa ul-Marbootah (`*llaah` / `*shaah` never soften) | §5 |
| 3 | Genuine Taa Marbootah + Ibn/bin junction → hard `-at` | §5 |
| 4 | Rhyming pairs & parallel conjunctions freeze the open `-ah` | §5 |
| 5 | Case-vowel glides · dynamic Abdullaah · nisbah · ABSOLUTE YAA' | §4, §6 |
| 6 | Diptotes take `-a` in Majroor | §4 |
| 7 | Author-name cover schemas | §7 |
| 8 | ص ض ط carry the dot below; ظ is the DH/dH digraph, never dotted | §1 |

---

## 1. The emphatic consonants — dot-below letters and the DH digraph (Rule 8)

Four emphatic consonants have no English equivalent and carry a **dot below**
the letter. All other consonants (including kh, gh, th, dh, sh, f, q, k) are
written plain; 'Ayn and Hamzah stay as straight ASCII apostrophes (`'`).

| Arabic | Latin | Examples |
|--------|-------|----------|
| ص | `ṣ` | Ṣaaliḥ · Ṣalaah · Uṣool |
| ض | `ḍ` | Nawaaqiḍ · Maḍmoon · Ramaḍaan · Ḍuhaa |
| ط | `ṭ` | Ṭaaifah · Ṭareeq · Khuṭbah |
| ح | `ḥ` | Ḥadeeth · Tawḥeed · Raḥmaan |
| ظ | `dH` / `DH` | MandHoomat · ḤifdH · DHaahiree |

Notes:

- **ظ is never a dot-below letter.** It maps strictly to the DH/dH digraph;
  the dotted glyph (z-with-dot) never appears anywhere in the system, and bare
  `z`, `dh`, or `Dh` spellings are drift.
- **Digraph casing (Rule 8):** use `DH` when the digraph opens the token
  string or carries Title Case — `DHaahiree`, `DHaalimoona`; use `dH` for
  lowercase and mid-word occurrences — `MandHoomat`, `ḤifdH`,
  `tandHuroona`, `yadHunnoona`. The second letter of the digraph is always a
  capital H.
- Under Shaddah the digraph doubles (`dHdH` / `DHDH`); under the article fold
  it assimilates like every other sun letter (`AdH-` standalone, `udH-` after
  a Muḍaaf head).
- ʿAin (ع) and Hamzah (ء) are straight apostrophes: `'Aqeedah`, `'Aaṣim`,
  `Qur'aan`, `Shu'bah`. Never the curly glyphs.
- Ḥaa (ح) is `ḥ`, not `h` — so `Faatiḥah`, `Aṣ-Ṣalaah`, `Al-Ḥadeeth`.

## 2. Alif-Laam with the Ḥuroof ush-Shamseeyah

Before a **sun letter** the Laam of the article is assimilated — written
doubled — so "Al-Shams" becomes Ash-Shams and "Al-Sabt" becomes As-Sabt. The
one exception is ل itself, where the Laam doubles into the following Laam and
"Al-Lughat" stands as written.

| Letter | Article | Example |
|--------|---------|---------|
| ت | At- | At-Tajweed |
| ث | Ath- | Ath-Thalaathah |
| د | Ad- | Ad-Durar |
| ذ | Adh- | Adh-Dhayl |
| ر | Ar- | Ar-Raḥmaan |
| ز | Az- | Az-Zakaah |
| س | As- | As-Sunnah |
| ش | Ash- | Ash-Shams |
| ص | Aṣ- | Aṣ-Ṣalaah |
| ض | Aḍ- | Aḍ-Ḍaḥaa |
| ط | Aṭ- | Aṭ-Ṭareeqat |
| ظ | AdH- | the dH letter — doubles like the others |
| ل | Al- | Al-Lughat (the Laam doubles into itself) |
| ن | An- | An-Naḥw |

The Laam is only doubled for the letter it immediately precedes: As-Sabt (س),
Adh-Dhayl (ذ), Ad-Durar (د), Ash-Shams (ش), Ar-Raḥmaan (ر).

## 3. The moon letters keep the Laam

Before a **moon letter** the Laam of the article is fully written and unchanged.

| Letter | Article | Example |
|--------|---------|---------|
| ا | Al- | Al-Imlaa |
| ب | Al- | Al-Bayaan |
| ج | Al- | Al-Jaami' us-Saḥeeḥ |
| ح | Al- | Al-Ḥadeeth |
| خ | Al- | Al-Khaṭeeb |
| ع | Al- | Al-'Aqeedah |
| غ | Al- | Al-Ghunyah |
| ف | Al- | Al-Fiqh |
| ق | Al- | Al-Qiraa'ah |
| ك | Al- | Al-Kitaab |
| م | Al- | Al-Maktabah |
| ه | Al- | Al-Hidaayah |
| و | Al- | Al-Waasiṭeeyah |
| ي | Al- | Al-Yaqeen |

## 4. Waṣl in Muḍaaf constructions and case-vowel glides (Rule 5)

When the article follows a Muḍaaf noun — an Iḍaafah, "X of the Y" — the Alif
drops in Waṣl and the Laam joins with the **active case vowel of the preceding
word**, replacing the default `a`:

> 'Uloom ul-Ḥadeeth · Qawaa'id il-Fiqheeyah · ḤifdH ul-Mutoon · Jawaab uṣ-Ṣaḥeeḥ

The sun letters still assimilate here, and under Rule 5 the case vowel merges
directly into the assimilated letter:

> Kitaab it-Tawḥeed · Uṣool ud-Deen

Practical rule: after a construct noun, the article is written lowercase and
joined with a hyphen (`ul-`, `il-`, `ut-`, `ush-`, `uṣ-`, …), never capital
`Al-`.

### 4.1 The case-vowel glide computation

Programmatically calculate each Mudaaf / Mudaaf-Ilayh pair: the default `a`
of `al-` is replaced by the case vowel of the preceding word.

| Grammatical state | Glide | Example |
|-------------------|-------|---------|
| Rafa' — Nominative | `u` | Jawaab uṣ-Ṣaḥeeḥ · Fasl ul-Kalaam |
| Nasb — Accusative | `a` | Naṣr al-Islaam · Dars al-Manṭiq |
| Jarr — Genitive | `i` | 'Ilm il-Bayaan · Ibn il-Qayyim il-Jawzeeyah |

Sun-letter glides merge into the assimilated letter itself — `Jawaab uṣ-Ṣaḥeeḥ`
(never `Jawaab aṣ-Ṣaḥeeḥ`); moon letters simply take the glide on the Laam.

### 4.2 Continuous-speech Waṣl transitions (lowercase particle format)

When a Muḍaaf noun transitions into a following definite noun at continuous
speech speed, the case vowel glides directly into a lowercase hyphenated
particle that joins the main noun. The three productive transitions in this
scheme:

**1. Dammah (Nominative) + Sun Letter Transition — `ur-`**

Rule: The final nominative `u` of a Muḍaaf noun glides directly into a
lowercase transition particle hyphenated to the capitalized main Sun Letter
noun.

| Arabic | Transliteration |
|--------|---------------|
| الْمِنْحَةُ الرَّبَّانِيَّةُ | `Al-Minḥat ur-Rabbaneeyah` |

Note: Under Rule 4 (Rhyming Title Schemes), `Rabbaneeyah` freezes its open
`-ah` ending instead of switching to `-at`.

**2. Kasrah (Genitive) + Moon Letter Transition — `il-`**

Rule: The final genitive `i` sound glides directly into the pronounced Laam of
the Moon Letter using the lowercase `il-` prefix.

| Arabic | Transliteration |
|--------|---------------|
| شَرْحِ الْأَرْبَعِينَ | `Sharḥ il-Arba'een` |

**3. Fixed Plural Fathah + Sun Letter Transition — `an-`**

Rule: Sound masculine plurals carry a mandatory, unalterable Fathah (`a`) on
their final Noon. Before a Sun Letter like Noon, this forms a clean `an-`
glide.

| Arabic | Transliteration |
|--------|---------------|
| الْأَرْبَعُونَ النَّوَوِيَّةُ | `Al-Arba'oon an-Nawaweeyah` |

### 4.2 Dynamic Abdullaah inflections

The name of the servant of Allaah shifts its inflection with its grammatical
state:

| State | Spelling |
|-------|----------|
| Rafa' — Nominative | `'Abdullaah` |
| Nasb — Accusative | `'Abdallaah` |
| Jarr — Genitive (e.g. directly after Ibn) | `'Abdillaah` |

So: `'Abdullaah bin Yoosuf` (nominative) but `Ibn 'Abdillaah` (genitive after
Ibn). The shift is total — never a mixed form.

### 4.3 Nisbah suffixes

Enforce `-eeyah` over `-iyyah` for the relative (nisbah) ending — `Arabeeyah`,
`Al-Waasiṭeeyah` — and map the Arabic suffix ييين explicitly to `-iyyeen` —
`Al-Manṭiqiyyeen`.

### 4.4 Diptotes (Al-Mamnoo' min aṣ-Ṣarf) — Rule 6

Diptote names reject tanween and take fathah instead of kasrah in the genitive.
Under this system the trailing solar/lunar connectors absorb the vowel change,
so a diptote in Majroor state takes an **`-a`-based article prefix** (`as-`,
`an-`, `at-`), never the kasrah base (`is-`, `in-`, `it-`). The name itself is
never altered.

Common diptotes: Daawood, Ibraaheem, 'Uthmaan, Ayyoob, Yoonus, Moosaa, Haaroon,
Jibreel, Ismaa'eel, Zakariyyaa, Yahyaa, 'Eesaa.

| Phrase | Correct | Reject |
|--------|---------|--------|
| Ibn Abee Daawood as-Sijistaanee | `as-` (fathah absorbed from Daawood) | Ibn Abee Daawood is-Sijistaanee |
| Ibn Abee Daawood an-Nasfee | `an-` (fathah base) | Ibn Abee Daawood in-Nasfee |
| Ibn Ismaa'eel at-Tukmeetee | `at-` (fathah base) | Ibn Ismaa'eel it-Tukmeetee |

A construct head that closes into the article merges with it in Waṣl —
"Dhu al-Ḥijjah" closes into "Dhul-Ḥijjah" (the CONSTRUCT_CLOSE data in
`tools/scheme-data.js`, used by `foldAlifLaam`).

## 5. Taa ul-Marbootah — Rules 2, 3, and 4

### 5.1 The -t ending

A word whose last letter is a genuine Taa ul-Marbootah (ة) keeps the **-t**
ending whenever a definite article or construct connection follows it in
Waṣl — and, by Rule 3, directly before Ibn/bin. The `-t` is the sound actually
pronounced in Waṣl; the `-ah` ending is the pause (Waqf) form.

| Ending | When | Examples |
|--------|------|----------|
| `-at` | article / construct follows — including directly before **Ibn/bin** (Rule 3) | Soorat ut-Tawbah · Al-Muqaddimat ul-Aajurroomeeyah · Riwaayat Qaaloon · Al-Muqaddimat Ibn Khaldoon · Laameeyat Ibn Taymeeyah |
| `-ah` | pause — end of phrase, punctuation, standalone title — **and** before parallel wa-conjunctions (Rule 4) | Soorat ul-Faatiḥah (paused) · Aṣ-Ṣalaah · As-Sunnah · Al-Bidaayah wan-Nihaayah · Al-Ma'rifah wal-Iqtiṣaad wan-Naqḍ · 'Aqeedah wa Manhaj |

Examples of the rules in action:

- سُورَة + name → **Soorat** ul-Baqarah, Soorat ut-Tawbah, Soorat Aal-'Imraan.
- رِوَايَة + narrator → **Riwaayat** Qaaloon, li-**Riwaayatay** Shu'bah wa
  Ḥafṣ (dual).
- مَقْدِمَة + ul- → Al-**Muqaddimat** ul-Aajurroomeeyah; مَقْدِمَة + Ibn →
  **Al-Muqaddimat Ibn Khaldoon** (Rule 3 hard connector).
- المَعْرِفَة + wal- → Al-**Ma'rifah** wal-Iqtiṣaad wan-Naqḍ (Rule 4 freeze).
- العَقِيدَة + wa- → **'Aqeedah** wa Manhaj (Rule 4 freeze).

**Exceptions — proper names** ending in Taa ul-Marbootah keep the `-ah`
spelling as their fixed name regardless of what follows: Shu'bah wa Ḥafṣ ·
Faaṭimah Yaaqoot ul-Libyaaree · Majza'ah 'Abduh · Ibn Taymeeyah · Madeenah ·
and the surah names (Al-Baqarah, At-Tawbah, As-Sajdah, …).

### 5.2 Radical soft Haa' — the *llaah / *shaah protection (Rule 2)

Words matching the `*llaah` or `*shaah` endings carry a **radical soft Haa'**
(ه), not a Taa ul-Marbootah: `Allaah`, `Billaah`, `Sabeelillaah`,
`'Abdullaah`, and the whole `*shaah` class. They are an independent consonant
class and **never** terminate in `-at` (never `-llaat`, never `-shaat`) under
any circumstances during Waṣl — including directly before `bin` or `Ibn`.
Rule 3 applies only to genuine ة words; these words are exempt forever.

## 6. Long vowels, the ABSOLUTE YAA' rule, and technical terms

- **Long vowels are written doubled** — `aa`, `ee`, `oo` (Ṣaaliḥ, Raḥmaan,
  Qaaloon, Ad-Dooree, 'Ishaa', 'Aṣr). No macrons, no circumflexes.
- **ABSOLUTE YAA' RULE:** a final long-vowel Yaa' (ي) closing an isolated
  word, name, or nisbah suffix maps strictly to the double vowel `-ee` —
  `as-Sijistaanee`, `il-Albaanee`, `Deen`, `Saheeh`. Never truncate to a
  single `-i` or `-y`. (Consonantal yaa' forms — the dual `-ay`, the genitive
  `-yi`, the doubled `-iyyi` — are not long-vowel finals and keep their own
  spellings.)
- **Every Soorah-name reference uses the Soorat construct** with the article
  folded for Iḍaafah and sun letters: Soorat ul-Kahf, Soorat ush-Shu'araa',
  Soorat Yoosuf.
- **Technical rule terms are capitalized**: Imaalah, Naql, Taqleel, Sakt,
  Ishmaam, Ikhtilaas, Madd Munfaṣṣil, Madd Laazim, Yaa' Maftooḥah Waṣlan,
  Saakinah Waqfan, Haa' al-Kinaayah, Yaa'aat ul-Iḍaafah, Yaa' Zaa'idah,
  Hamzat ul-Waṣl.
- **Straight apostrophes only** — hamzah/'ain marks are `'`, never curly.

## 7. Author-name schemas (Rule 7)

Author names enforce exact case inflections based on the cover-text schema:

- `Muhammad Naasir ud-Deen il-Albaanee` (mixed glides: Naasir apposes
  Muhammad, never Majroor from it — Rafa' glide gives `ud-Deen`; Deen stays
  Majroor as Muḍaaf-Ilayh of Naasir in every state, feeding `il-Albaanee`.
  Only inside a full Jarr frame — li-Muhammad…, min Muhammad… — does the whole
  chain drop to `id-Deen il-Albaanee`)
- `Muhammad bin 'Alee ish-Shawkaanee`
- `Ibn 'Abdillaah` (dynamic genitive of 'Abdullaah — Rule 5)
- `Ibn Abee Daawood as-Sijistaanee` (diptote genitive — Rule 6)

Catalog entries, citation footnotes, and title pages must reproduce these
schemas verbatim; recompute any inflection that differs.

### 7.1 Maktabah-facing compliance targets

The following titles are the canonical forms enforced across the Maktabah (the
`server.ts` display fields, `SCHEME_TITLES`, `SCHEME_AUTHORS`, pdf-filename
keys, and the `curriculum.json` entries). Everywhere these titles appear in
the catalogue, they must match these strings verbatim:

- **Base Matn:** `Al-Arba'een an-Nawaweeyah` (owner's ruling 2026-10-21: النَّوَوِيَّة opens
  on a sun letter (noon), so the article assimilates to `an-`; `un-` was a slip on this line
  alone - the SHARH title below, and every generator fed from this section, now agree)
- **Sharḥ (full title):** `Al-Minḥat ur-Rabbaneeyah fee Sharḥ il-Arba'een an-Nawaweeyah`
- **Author (Nawawee):** `Yaḥyaa bin Sharaf in-Nawawee` (author field) /
  `Yaḥyaa bin Sharaf [in-Nawawee]` (display bracket form in `SCHEME_AUTHORS`)
- **Sharḥ author:** `Ṣaaliḥ bin 'Abdillaah il-'Uṣaymee`

## 8. Ḥuroof ul-Muqaṭṭa'aat — coordinate-keyed interception

The disjoint letters opening certain surahs are recited letter-by-letter, so
they are never phonetically transliterated. Every Muqaṭṭa'aat token resolves
by its word-coordinate key (`surah:ayah:word`) **straight to its literal
disjoined text layout**, bypassing the standard vowel loops completely:

| Coordinate | Layout |
|------------|--------|
| 2:1:0 | Alif Laam Meem |
| 7:1:0 | Alif Laam Meem Ṣaad |
| 13:1:0 | Alif Laam Meem Raa' |
| 19:1:0 | Kaaf Haa' Yaa' 'Ayn Ṣaad |
| 20:1:0 | Ṭaa' Haa' |
| 42:2:0 | 'Ayn Seen Qaaf |
| 50:1:0 | Qaaf |
| 68:1:0 | Noon |

Strict hamzah apostrophes and long-vowel letter names throughout — `'Ayn`,
`Raa'`, `Yaa'` — never truncated vowel loops. The full 30-entry coordinate
table lives in `tools/scheme-data.js` (MUQATTAAT_COORDS) with a generated
inline copy in the reader; a shape-based fallback covers Riwaayaat spellings
of the same tokens (Qaaloon's disjoint-letter orthography), while Riwaayaat
variant rows keep the same word-coordinate keys.

---

## 9. Quran.com WBW override and word-coordinate integrity

- Upstream word-by-word **translation** glosses remain credited to Quran.com
  (Quran Foundation); upstream WBW **transliteration** strings are intercepted
  and discarded (`sanitizeGloss`) — they are never displayed anywhere.
- Every hover card, click tooltip, verse-level card, audio sync tag, and
  Riwaayaat variant row is keyed by `surah:ayah:word`; variants and definitions
  align through that key, so replacing the rendering layer cannot desynchronize
  the data layers.
- The reader's footer states the split: translation courtesy of Quran.com;
  all transliteration rendered in-house by this system.

---

## 10. Alif Madd — strictly 'Aa' (Rule 1)

Alif Madd (آ) transliterates strictly as the long vowel `Aa` — a pure long-a
layout with no hamzah seat and no vowel-loop detour: `Aadam`, `Aaminah`,
`Al-Aajurroomeeyah`, `Al-Aakhar`. Word-initially the layout capitalizes as
`Aa`; mid-token the same strictness applies. (The KFGQPC corpus writes maddah
with dagger/madda marks, so verse tokens route through the long-vowel path —
this rule binds headings, titles, and any direct input.)

---

## 11. Legacy corrections — the `-ee` restoration directive

Digital editions, Arabic text datasets, index layouts, and PDF metadata may
still carry legacy spellings that violate the ABSOLUTE YAA' rule and the
diptote schemas. The standing directive:

- Legacy single `-i` endings on final long-vowel Yaa' restore to `-ee`
  (`Sijistaani` → `Sijistaanee`, `Albani` → `Albaanee`, `Deeni` → `Deenee`
  where the yaa' is the long vowel).
- Kasrah-base article prefixes after diptotes restore to the fathah base
  (`is-Sijistaanee` → `as-Sijistaanee`, `in-Nasfee` → `an-Nasfee`).
- `tools/mutoon-scan.py` runs the optical scan over the Mutoon library via
  PyMuPDF, reports every legacy form, and mirrors corrected text plus verified
  metadata into the Maktabah catalog layers.

---

## 12. IANA timezone display — the `formatTz` convention

The Ṣalaah-times widget humanizes the IANA timezone returned by the Aladhan
API (`formatTz` in `index.html`, unit-tested in `tools/test-names.js`):

- Split on `/`, rejoin as **Continent / City** — `America/Los_Angeles` →
  America / Los Angeles.
- Underscores become spaces — `Asia/Kuala_Lumpur` → Asia / Kuala Lumpur.
- A zone with no `/` (UTC) is left unchanged; deeper segments preserved as-is
  (`America/Argentina/Buenos_Aires` → America / Argentina/Buenos Aires).

Full subtitle shape shared by every branch:

> Selected City · America / Los Angeles · Sunday, 16 Aug 2026
> Your Location · America / New York · Sunday, 16 Aug 2026

---

*This document is the source of truth for all three projects
(Ribaaṭ-Initiative, Maktabat-Abee-Ṣaaliḥ, Anṣaar-us-Sunnah-International-Waqf);
each carries a synchronized copy. Enforcement is codified in
`tools/scheme-data.js` (MARBOOTAH_T, SUN_FOLD, CONSTRUCT_CLOSE, U_FORM,
SOORAT_CONSTRUCT, MUQATTAAT_COORDS, CASE_VOWELS, ABDULLAAH_INFL, NISBAH,
ABSOLUTE_YAA, DIPTOTES_GEN_A, AUTHOR_SCHEMA, DH_DIGRAPH — U_FORM derives from
SUN_FOLD and SOORAT_CONSTRUCT.head from MARBOOTAH_T, so neither can silently
disagree). The pages carry generated inline copies; `node tools/scheme-data.js
--sync` regenerates them and `tools/test-names.js` verifies the sync.
`tools/check-alif-laam.js` enforces article folding, canonical name spellings,
the Soorat construct, the Taa ul-Marbootah -t rule, and the Rule 4 conjunction
freeze; `tools/check-transliteration.js` pins the engine outputs (including the
Muqaṭṭa'aat coordinate interceptions and the DH/dH digraph) over a 77,430-word
corpus scan; `tools/check-doc.js` runs every example in this document through
those same rules (deliberate wrong-form illustrations — "Al-Shams", "Al-Sabt",
"Soorah Yoosuf" — are allowlisted counter-examples). Run `bash .freebuff/check.sh`
(or `powershell -File .freebuff/check.ps1`) before publishing any transliterated
content.*





