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

TRANSLIT_MARKS = set("ًٌٍَُِّْٰـ")

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


FIXUPS = {
    "السلام": "السَلَام",
    "عليكم": "عَلَيْكُم",
    "الصلاة": "الصَلَاة",
    "الله": "اللَّه",
    "لله": "لِلَّه",
    "الحمد": "الْحَمْد",
    "الرحمن": "الرَّحْمَن",
    "الرحيم": "الرَّحِيم",
    "محمد": "مُحَمَّد",
    "الاسلام": "الْإِسْلَام",
    "مسلم": "مُسْلِم",
    "ماذا": "مَاذَا",
    "كيف": "كَيْفَ",
    "اليوم": "الْيَوْم",
    "الشغل": "الشُّغْل",
    "شكرا": "شُكْرًا",
    "اسمك": "اسْمُكَ",
    "انا": "أَنَا",
    "تريد": "تُرِيدُ",
}
SHORT_TAIL = {"َ", "ُ", "ِ", "ً", "ٌ", "ٍ"}


def apply_fixups(text):
    """Repair the words Mishkal reliably mis-vocalizes.

    It returns السُّلَّام and الصَلَاَة for the two most
    common words in the app, which then read sullaam and salaaah.
    """
    pieces = []
    for token in text.split(" "):
        if not token:
            pieces.append(token)
            continue
        prefix, core, suffix = _split_punct(token)
        bare = NO_DIACRITICS.sub("", core)
        fixed = FIXUPS.get(bare)
        core_prefix = ""
        if fixed is None:
            # A conjunction or preposition glued to a repaired word:
            # وَالسَّلَامُ must reach the same fixup as السَّلَامُ.
            for key in FIXUPS:
                lead = len(bare) - len(key)
                if lead in (1, 2) and bare.endswith(key) and bare[0] in "وفبكل":
                    seen = 0
                    cut = 0
                    for index, ch in enumerate(core):
                        if NO_DIACRITICS.match(ch):
                            seen += 1
                        if seen == lead:
                            cut = index + 1
                            break
                    core_prefix = core[:cut]
                    fixed = FIXUPS[key]
                    break
        if fixed is None:
            pieces.append(token)
            continue
        tail = core[-1] if core and core[-1] in SHORT_TAIL else ""
        if tail and fixed[-1] not in SHORT_TAIL:
            fixed += tail
        pieces.append(prefix + core_prefix + fixed + suffix)
    return " ".join(pieces)


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
    return _vowel_repair(" ".join(pieces))


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
VOWELS = {"a", "u", "i", "an", "un", "in", "aa", "ee", "oo", "Aa"}


SHORT_VOWELS = {
    "َ": "a", "ُ": "u", "ِ": "i",
    "ً": "an", "ٌ": "un", "ٍ": "in",
}


def _next_is_vowel(chars, index):
    for later in chars[index + 1:]:
        if later in TRANSLIT_MARKS:
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


def _pause(text):
    if not text.endswith(("aa", "ee", "oo")) and text[-1:] in ("u", "i", "a"):
        return text[:-1]
    return text


def _cap(text):
    for index, ch in enumerate(text):
        if ch.isalpha():
            return text[:index] + ch.upper() + text[index + 1:]
    return text


def _translit_word(word, next_word, glide, at_start):
    bare = "".join(ch for ch in word if ch not in TRANSLIT_MARKS)
    if not bare:
        return word

    particle = ""
    if len(bare) > 3 and bare[0] in ("و", "ف") and bare.startswith("ال", 1):
        particle = "wa" if bare[0] == "و" else "fa"
        word = word[1:]
        while word and word[0] in TRANSLIT_MARKS:
            word = word[1:]
        bare = bare[1:]
        glide = None

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
        body_word = "".join(kept)
        if bare[2] in SUN_LETTERS:
            body_word = body_word.replace("\u0651", "", 1)

    rendered = _word_letters(body_word)

    # Taa ul-Marbootah: -ah in pause, -at before a word, -atu before a particle.
    taa = ""
    if bare.endswith("ة"):
        vowel = ""
        for candidate in ("an", "un", "in", "u", "i", "a"):
            if rendered.endswith(candidate):
                vowel = rendered[-len(candidate):]
                rendered = rendered[:-len(candidate)]
                break
        # The long alif already supplies the a of -ah / -at.
        long_a = rendered.endswith("a")
        if next_word is None:
            taa = "h" if long_a else "ah"
        else:
            bare_next = "".join(ch for ch in next_word if ch not in TRANSLIT_MARKS)
            base = "t" if long_a else "at"
            taa = base + vowel if bare_next[:1] in ("و", "ف") else base

    if article:
        if not rendered:
            return particle + bare + taa
        first = bare[2]
        tail = _cap(rendered) + taa
        letter = SUN_LETTERS[first] if first in SUN_LETTERS else "l"
        if particle:
            if next_word is None and tail[-1:] in ("u", "i", "a"):
                # Pause form: the particle phrase drops its case vowel.
                tail = tail[:-1]
            return particle + letter + "-" + tail
        if next_word is None:
            tail = _pause(tail)
        if glide in ("u", "i", "a"):
            # Idaafah glide: Aṣ-Ṣalaat ul-Jamaa'ah, Manhaj Rajul id-Dawlah.
            return glide + letter + "-" + tail
        return ("A" if at_start else "a") + letter + "-" + tail

    if next_word is None:
        # Pause form: a final short case vowel is not pronounced.
        rendered = _pause(rendered)
    return particle + rendered + taa


VOWEL_MARKS = set(SHORT_VOWELS) | {"ْ", "ّ", "ٰ"}


def _vowel_repair(text):
    """Give an unvocalized word enough vowels to be pronounceable.

    Colloquial words Mishkal cannot vocalize arrive bare, so بخلص would
    otherwise read bkhlṣ. Insert a short a between consonant clusters; the
    first cluster of a word takes i, as in bikhalliṣ.
    """
    pieces = []
    for token in text.split(" "):
        if not token or any(ch in VOWEL_MARKS for ch in token):
            pieces.append(token)
            continue
        letters = [ch for ch in token if ch not in PUNCTUATION]
        if len(letters) < 3:
            pieces.append(token)
            continue
        out = []
        run = 0
        for ch in token:
            if ch in PUNCTUATION:
                out.append(ch)
                continue
            run += 1
            out.append(ch)
            if run >= 2:
                out.append("َ")
                run = 0
        pieces.append("".join(out))
    return " ".join(pieces)


def transliterate(text):
    words = text.split(" ")
    out = []
    glide = None
    for index, word in enumerate(words):
        if not word.strip():
            out.append(word)
            continue
        nxt = None
        for later in words[index + 1:]:
            if later.strip():
                nxt = later
                break
        rendered = _translit_word(word, nxt, glide, glide is None)
        out.append(rendered)
        if rendered.endswith("at"):
            glide = "u"
        elif rendered[-1:] in ("u", "i", "a"):
            glide = rendered[-1]
        else:
            # A construct chain with no visible ending reads as majroor.
            glide = "i"
    return " ".join(out).replace("  ", " ").strip()


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

            lahja = req.get("lahja") or "fusha"
            if not isinstance(lahja, str):
                lahja = "fusha"
            lahja = lahja.strip().lower()

            if cmd == "translit":
                if vocalizer is None:
                    raise RuntimeError("mishkal import failed: %s" % mishkal_error)
                spoken = apply_lahja(apply_fixups(vocalizer.tashkeel(text)), lahja)
                respond({"id": req.get("id"), "ok": True, "text": transliterate(spoken)})
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
            vocalized = apply_lahja(apply_fixups(vocalizer.tashkeel(text)), lahja)
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
