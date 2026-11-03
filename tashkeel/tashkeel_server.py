#!/usr/bin/env python3
"""Resident tashkeel worker for Qalam.

Protocol: one JSON object per line on stdin, one JSON object per line on stdout.

  -> {"id": 1, "cmd": "tashkeel", "text": "كتب الطالب الدرس"}
  <- {"id": 1, "ok": true, "text": "كَتَبَ الطَّالِبُ الدَّرْسَ"}

  -> {"id": 2, "cmd": "pinyin", "text": "你好，世界"}
  <- {"id": 2, "ok": true, "text": "nǐ hǎo，shì jiè"}

  -> {"id": 3, "cmd": "ping"}
  <- {"id": 3, "ok": true, "text": ""}

Running resident matters: importing Mishkal and loading its lexicon costs about
1.6 s, while a warm diacritization call is about 0.1 s. The same process serves
Pinyin so the Chinese path does not pay a second interpreter startup.
"""

import json
import re
import sys
import traceback

MAX_CHARS = 5000
HAN = re.compile("[\u3400-\u4dbf\u4e00-\u9fff\uf900-\ufaff]+")
NO_SPACE_BEFORE = re.compile("\\s+([，。！？；：、,.!?;:)\\]】》」』”’])")
NO_SPACE_AFTER = re.compile("([(\\[【《「『“‘])\\s+")


def clean_output(text):
    # Mishkal appends a \x01 sentinel; strip C0 controls so none reach the UI
    # or the clipboard.
    return "".join(ch for ch in text if ord(ch) >= 32).strip()


# Dialect pronunciation for the tashkeel command.
#
# Every register has its own entries, and each entry names the lahjaat that
# actually use the word, so an Iraqi register never rewrites Moroccan words and a
# Sudanese one never rewrites Gulf words. Membership is exact: a word shared by
# three dialects carries all three codes.
NO_DIACRITICS = re.compile("[\u064B-\u0652\u0670\u0640]")
PUNCTUATION = "\u060C\u061B\u061F.,!?:;\"'()[]{}«»…"
CASE_ENDING = re.compile("[\u064F\u0650\u064B\u064C\u064D]$")


def _w(vocalized, *lahjaat):
    return (vocalized, frozenset(lahjaat))


LAHJA_WORDS = {
    # Egyptian
    "دلوقتي": _w("دِلْوَقْتِي", "egyptian"),
    "عايز": _w("عَايِز", "egyptian"),
    "عاوز": _w("عَاوِز", "egyptian"),
    "أوي": _w("أَوِي", "egyptian"),
    "كمان": _w("كَمَان", "egyptian", "levantine"),
    "بص": _w("بُصّ", "egyptian"),
    "معلش": _w("مَعْلِش", "egyptian"),
    "إيه": _w("إِيه", "egyptian"),
    "ازيك": _w("إِزَيِّك", "egyptian"),
    "إزيك": _w("إِزَيِّك", "egyptian"),
    "خالص": _w("خَالِص", "egyptian"),
    "بخلص": _w("بَخَلِّص", "egyptian", "sudanese", "gulf"),
    "أخلص": _w("أَخَلِّص", "egyptian", "sudanese", "gulf"),
    # Levantine
    "هلق": _w("هَلَّق", "levantine"),
    "هلأ": _w("هَلَّأ", "levantine"),
    "بدي": _w("بِدِّي", "levantine"),
    "بدك": _w("بِدِّك", "levantine"),
    "منيح": _w("مْنِيح", "levantine"),
    "كتير": _w("كْتِير", "levantine"),
    "عنجد": _w("عَنْجَد", "levantine"),
    "شوي": _w("شْوَي", "levantine", "gulf"),
    "شو": _w("شُو", "levantine"),
    "خلص": _w("خَلِّص", "levantine"),
    "يلا": _w("يَلَّا", "levantine", "egyptian", "sudanese"),
    # Gulf
    "الحين": _w("الحِين", "gulf"),
    "وش": _w("وَشْ", "gulf", "hijazi"),
    "أبا": _w("أَبَا", "gulf"),
    "ابا": _w("أَبَا", "gulf"),
    "تبي": _w("تِبِي", "gulf"),
    "زين": _w("زَيْن", "gulf"),
    "وايد": _w("وَايِد", "gulf"),
    "شلونك": _w("شْلُونَك", "gulf", "iraqi"),
    "عساك": _w("عَسَاك", "gulf", "yemeni"),
    "أبشر": _w("أَبْشِر", "gulf"),
    "خلاص": _w("خَلَاص", "gulf", "sudanese", "levantine"),
    "ماشي": _w("مَاشِي", "sudanese", "gulf", "egyptian"),
    "قريب": _w("قَرِيب", "sudanese", "gulf"),
    # Sudanese
    "هسي": _w("هَسِّي", "sudanese"),
    "زول": _w("زَوْل", "sudanese"),
    "كيفنك": _w("كَيْفِنْك", "sudanese"),
    # Iraqi
    "هسة": _w("هَسَّة", "iraqi"),
    "هسا": _w("هَسَا", "iraqi"),
    "شكو": _w("شَكُو", "iraqi"),
    "ماكو": _w("مَاكُو", "iraqi"),
    "خوش": _w("خُوش", "iraqi"),
    "هواية": _w("هَوَايَة", "iraqi"),
    "شنو": _w("شِنُو", "iraqi", "sudanese", "moroccan"),
    # Yemeni
    "شي": _w("شِي", "yemeni"),
    "حياك": _w("حَيَّاك", "yemeni", "gulf", "hijazi", "sudanese"),
    # Hijazi
    "دحين": _w("دَحِين", "hijazi"),
    "إيش": _w("إِيش", "hijazi", "gulf"),
    "ايش": _w("إِيش", "hijazi", "gulf"),
    "أبغى": _w("أَبْغَى", "hijazi"),
    "ابغى": _w("أَبْغَى", "hijazi"),
    "كذا": _w("كِذَا", "hijazi", "gulf"),
    "زحمة": _w("زَحْمَة", "hijazi", "gulf", "egyptian"),
    # Moroccan
    "دابا": _w("دَابَا", "moroccan"),
    "بزاف": _w("بْزَاف", "moroccan", "algerian"),
    "لاباس": _w("لَابَاس", "moroccan"),
    "واخا": _w("وَاخَا", "moroccan"),
    "صافي": _w("صَافِي", "moroccan"),
    "مزيان": _w("مْزِيَان", "moroccan"),
    "خويا": _w("خُويَا", "moroccan"),
    "كيداير": _w("كِيدَايِر", "moroccan"),
    "شكون": _w("شْكُون", "moroccan", "tunisian"),
    "علاش": _w("عْلَاش", "moroccan", "tunisian"),
    "شحال": _w("شْحَال", "moroccan"),
    "فين": _w("فِين", "moroccan"),
    "واش": _w("وَاش", "moroccan", "algerian"),
    # Tunisian
    "برشا": _w("بَرْشَا", "tunisian"),
    "باهي": _w("بَاهِي", "tunisian", "libyan"),
    # Libyan
    "هلبا": _w("هَلْبَا", "libyan"),
    "نبي": _w("نِبِي", "libyan"),
}


def _split_punct(token):
    start = 0
    end = len(token)
    while start < end and token[start] in PUNCTUATION:
        start += 1
    while end > start and token[end - 1] in PUNCTUATION:
        end -= 1
    return token[:start], token[start:end], token[end:]


def apply_lahja(text, lahja):
    """Rewrite a vocalized line toward the spoken form of one lahja."""
    if not lahja or lahja == "fusha":
        return text

    pieces = []
    for token in text.split(" "):
        if not token:
            pieces.append(token)
            continue
        prefix, core, suffix = _split_punct(token)
        bare = NO_DIACRITICS.sub("", core)
        entry = LAHJA_WORDS.get(bare)
        if entry is not None and lahja in entry[1]:
            core = entry[0]
        else:
            # Every lahja drops the case ending; Fuṣḥā keeps it.
            core = CASE_ENDING.sub("", core)
        pieces.append(prefix + core + suffix)
    return " ".join(pieces)


# Aboo Saalih transliteration (Maktabat-Abee-Saaliḥ scheme), used as the
# pronunciation aid under the tashkeel card. Only this scheme is implemented:
# emphatics are dotted (ṣ ḍ ṭ ḥ), DHaa is the dH digraph, long vowels double
# (aa/ee/oo), and the article folds into sun letters (Ash-Shams, Aṣ-Ṣalaah).
TRANS_LETTERS = {
    "ب": "b", "ت": "t", "ث": "th", "ج": "j", "ح": "ḥ", "خ": "kh", "د": "d",
    "ذ": "dh", "ر": "r", "ز": "z", "س": "s", "ش": "sh", "ص": "ṣ", "ض": "ḍ",
    "ط": "ṭ", "ظ": "dH", "ع": "'", "غ": "gh", "ف": "f", "ق": "q", "ك": "k",
    "ل": "l", "م": "m", "ن": "n", "ه": "h",
}
SUN_LETTERS = {
    "ت": "t", "ث": "th", "د": "d", "ذ": "dh", "ر": "r", "ز": "z", "س": "s",
    "ش": "sh", "ص": "ṣ", "ض": "ḍ", "ط": "ṭ", "ظ": "dH", "ل": "l", "ن": "n",
}
SHORT_VOWELS = {"a", "u", "i", "an", "un", "in", "aa", "ee", "oo", "Aa"}
MARKS = set(SHORT_VOWELS) | {"\u0651", "\u0652", "\u0670", "\u0640"}


def _next_is_vowel(chars, index):
    for later in chars[index + 1:]:
        if later in MARKS:
            return later in SHORT_VOWELS
        return False
    return False


def _word_letters(word):
    chars = [c for c in word if c != "\u0640"]
    out = []
    for index, ch in enumerate(chars):
        if ch == "\u0651":
            # Shaddah doubles the consonant itself, never the vowel after it.
            spot = len(out) - 1
            while spot >= 0 and out[spot] in VOWELS:
                spot -= 1
            if spot >= 0:
                out.insert(spot, out[spot])
            continue
        if ch in SHORT_VOWELS:
            out.append(SHORT_VOWELS[ch])
            continue
        if ch == "\u0652":
            continue
        if ch == "\u0670":
            if out and out[-1] == "a":
                out[-1] = "aa"
            else:
                out.append("aa")
            continue
        if ch == "ا":
            if out and out[-1] == "a":
                out[-1] = "aa"
            else:
                out.append("a")
            continue
        if ch == "ى":
            out.append("aa")
            continue
        if ch == "و":
            if out and out[-1] == "u":
                out[-1] = "oo"
            else:
                out.append("w")
            continue
        if ch == "ي":
            if out and out[-1] == "i":
                out[-1] = "ee"
            else:
                out.append("y")
            continue
        if ch == "ة":
            out.append("at" if _next_is_vowel(chars, index) else "ah")
            continue
        if ch == "ء":
            out.append("'")
            continue
        if ch == "أ":
            out.append("a")
            continue
        if ch == "إ":
            out.append("i")
            continue
        if ch == "آ":
            out.append("Aa")
            continue
        if ch == "ؤ":
            out.append("u")
            continue
        if ch == "ئ":
            out.append("i")
            continue
        out.append(TRANS_LETTERS.get(ch, ""))
    return "".join(out)


def _translit_word(word, next_word):
    bare = "".join(ch for ch in word if ch not in TRANSLIT_MARKS)
    if not bare:
        return word

    body_word = word
    article = bare.startswith("ال") and len(bare) > 2
    if article:
        removed = 0
        kept = []
        for ch in word:
            if removed < 2 and ch in ("ا", "ل"):
                removed += 1
                continue
            kept.append(ch)
        body_word = "".join(kept).replace("\u0651", "", 1) if bare[2] in SUN_LETTERS else "".join(kept)

    # Case vowels stay: the -u of As-Salaamu is pronounced, so the aid keeps it.
    rendered = _word_letters(body_word)
    if not rendered:
        return bare

    if article:
        tail = rendered[0].upper() + rendered[1:]
        if bare[2] in SUN_LETTERS:
            return "A" + SUN_LETTERS[bare[2]] + "-" + tail
        return "Al-" + tail

    if bare.endswith("ة") and next_word is not None:
        if "".join(ch for ch in next_word if ch not in TRANSLIT_MARKS).startswith("ال") and rendered.endswith("ah"):
            rendered = rendered[:-2] + "at"
    return rendered


def transliterate(text):
    words = text.split(" ")
    out = []
    for index, word in enumerate(words):
        if not word.strip():
            out.append(word)
            continue
        nxt = None
        for later in words[index + 1:]:
            if later.strip():
                nxt = later
                break
        out.append(_translit_word(word, nxt))
    result = " ".join(out)
    return result.replace("  ", " ").strip()


def respond(payload):
    sys.stdout.write(json.dumps(payload, ensure_ascii=False) + "\n")
    sys.stdout.flush()


def to_pinyin(text, lazy_pinyin, style):
    """Transcribe Han runs only, leaving Latin text and punctuation intact."""
    pieces = []
    last = 0
    for match in HAN.finditer(text):
        pieces.append(text[last:match.start()])
        pieces.append(" ".join(lazy_pinyin(match.group(0), style=style)))
        last = match.end()
    pieces.append(text[last:])
    result = "".join(pieces)
    result = NO_SPACE_BEFORE.sub(r"\1", result)
    result = NO_SPACE_AFTER.sub(r"\1", result)
    result = re.sub(r"[ \t]+", " ", result)
    return result.strip()


def main():
    # The C# side launches Python with PYTHONIOENCODING/PYTHONUTF8 and writes
    # UTF-8 bytes; reconfigure is a belt-and-braces guard for older launches.
    try:
        sys.stdin.reconfigure(encoding="utf-8")
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass

    # Both engines are optional at startup: a missing package disables only its
    # own command, so a Pinyin request still works when Mishkal is absent.
    vocalizer = None
    mishkal_error = None
    try:
        from mishkal.tashkeel import TashkeelClass

        vocalizer = TashkeelClass()
    except Exception as exc:
        mishkal_error = exc

    pinyin_engine = None
    pinyin_error = None
    try:
        from pypinyin import Style, lazy_pinyin

        pinyin_engine = (lazy_pinyin, Style)
    except Exception as exc:
        pinyin_error = exc

    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue
        req = {}
        try:
            req = json.loads(line)
            cmd = req.get("cmd", "tashkeel")
            if cmd == "ping":
                respond({"id": req.get("id"), "ok": True, "text": ""})
                continue

            text = req.get("text", "")
            if not isinstance(text, str):
                raise ValueError("text must be a string")
            if len(text) > MAX_CHARS:
                text = text[:MAX_CHARS]

            if cmd == "translit":
                respond({"id": req.get("id"), "ok": True, "text": transliterate(text)})
                continue

            if cmd == "pinyin":
                if pinyin_engine is None:
                    raise RuntimeError("pypinyin import failed: %s" % pinyin_error)
                lazy_pinyin, style = pinyin_engine
                respond({"id": req.get("id"), "ok": True,
                         "text": clean_output(to_pinyin(text, lazy_pinyin, style))})
                continue

            if vocalizer is None:
                raise RuntimeError("mishkal import failed: %s" % mishkal_error)
            lahja = req.get("lahja") or "fusha"
            if not isinstance(lahja, str):
                lahja = "fusha"
            vocalized = apply_lahja(vocalizer.tashkeel(text), lahja.strip().lower())
            respond({"id": req.get("id"), "ok": True, "text": clean_output(vocalized)})
        except Exception:
            respond(
                {
                    "id": req.get("id"),
                    "ok": False,
                    "error": traceback.format_exc(limit=2),
                }
            )


if __name__ == "__main__":
    main()
