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
