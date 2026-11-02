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
            respond({"id": req.get("id"), "ok": True, "text": clean_output(vocalizer.tashkeel(text))})
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
