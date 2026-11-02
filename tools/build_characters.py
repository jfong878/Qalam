#!/usr/bin/env python3
"""Build Qalam's bundled Chinese character table from Unicode data.

Inputs (downloaded separately, not committed here):
  * Unihan.zip from https://www.unicode.org/Public/UCD/latest/ucd/Unihan.zip
    (Unihan_IRGSources.txt, Unihan_Readings.txt, Unihan_Variants.txt)
  * CJKRadicals.txt from https://www.unicode.org/Public/UCD/latest/ucd/CJKRadicals.txt

Output:
  data/characters.tsv, one BMP character per line:

    char \t pinyin \t radicalNumber \t radical \t extraStrokes \t totalStrokes
         \t traditionalVariant \t simplifiedVariant \t definition

The table is intentionally a character dictionary, not a word dictionary: it
answers "what is this character, which radical is it filed under, how many
strokes, and what does it mean". Definitions are Unihan's concise glosses.
"""

import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
UNIHAN = os.path.join(ROOT, "selftest", "chars", "unihan")
RADICALS = os.path.join(ROOT, "selftest", "chars", "CJKRadicals.txt")
OUT = os.path.join(ROOT, "data", "characters.tsv")

FIELDS = (
    "kDefinition",
    "kMandarin",
    "kRSUnicode",
    "kTotalStrokes",
    "kTraditionalVariant",
    "kSimplifiedVariant",
)


def read_table(path):
    rows = {}
    if not os.path.exists(path):
        raise SystemExit("missing input: " + path)
    with open(path, encoding="utf-8") as handle:
        for line in handle:
            if not line or line[0] == "#":
                continue
            parts = line.rstrip("\n").split("\t")
            if len(parts) < 3:
                continue
            code, field, value = parts[0], parts[1], parts[2]
            if field in FIELDS:
                rows.setdefault(code, {})[field] = value
    return rows


def read_radicals(path):
    radicals = {}
    if not os.path.exists(path):
        raise SystemExit("missing input: " + path)
    with open(path, encoding="utf-8") as handle:
        for line in handle:
            if not line or line[0] == "#" or ";" not in line:
                continue
            parts = [p.strip() for p in line.split(";")]
            if len(parts) < 3 or not parts[0].isdigit():
                continue
            try:
                number = int(parts[0])
                # Prefer the unified ideograph (last column); fall back to the
                # compatibility radical glyph.
                glyph = chr(int(parts[2], 16))
                if not glyph or ord(glyph) > 0xFFFF:
                    glyph = chr(int(parts[1], 16))
                radicals[number] = glyph
            except ValueError:
                continue
    return radicals


def first_code_point(value):
    if not value:
        return ""
    match = re.search(r"U\+([0-9A-Fa-f]{4,6})", value)
    if not match:
        return ""
    code = int(match.group(1), 16)
    if code > 0xFFFF:
        return ""
    return chr(code)


def clean(text, limit=180):
    if not text:
        return ""
    text = text.replace("\t", " ").replace("\r", " ").replace("\n", " ")
    text = re.sub(r"\s+", " ", text).strip()
    if len(text) > limit:
        cut = text[:limit]
        space = cut.rfind(" ")
        if space > limit // 2:
            cut = cut[:space]
        text = cut.rstrip(" ,;") + "\u2026"
    return text


def split_radical(value):
    match = re.match(r"(\d+)\.(-?\d+)", value or "")
    if not match:
        return 0, 0
    return int(match.group(1)), int(match.group(2))


def main():
    irg = read_table(os.path.join(UNIHAN, "Unihan_IRGSources.txt"))
    readings = read_table(os.path.join(UNIHAN, "Unihan_Readings.txt"))
    variants = read_table(os.path.join(UNIHAN, "Unihan_Variants.txt"))
    radicals = read_radicals(RADICALS)

    codes = set(irg) | set(readings) | set(variants)
    records = []
    by_char = {}
    skipped = 0
    for code in codes:
        match = re.fullmatch(r"U\+([0-9A-Fa-f]{4,6})", code)
        if not match:
            continue
        value = int(match.group(1), 16)
        if value > 0xFFFF:
            skipped += 1
            continue
        char = chr(value)
        data = {}
        data.update(irg.get(code, {}))
        data.update(readings.get(code, {}))
        data.update(variants.get(code, {}))
        definition = clean(data.get("kDefinition", ""))
        pinyin = clean(data.get("kMandarin", ""), 80)
        if not definition and not pinyin:
            continue

        number, extra = split_radical(data.get("kRSUnicode", ""))
        total_match = re.search(r"\d+", data.get("kTotalStrokes", ""))
        record = {
            "char": char,
            "pinyin": pinyin,
            "number": number,
            "radical": radicals.get(number, ""),
            "extra": extra,
            "total": total_match.group(0) if total_match else "",
            "traditional": first_code_point(data.get("kTraditionalVariant", "")),
            "simplified": first_code_point(data.get("kSimplifiedVariant", "")),
            "definition": definition,
        }
        records.append(record)
        by_char[char] = record

    # Simplified characters occasionally lack kRSUnicode; the traditional form
    # still carries the radical classification, so inherit it when possible.
    for record in records:
        if record["number"]:
            continue
        for candidate in (record["traditional"], record["simplified"]):
            source = by_char.get(candidate) if candidate else None
            if source and source["number"]:
                record["number"] = source["number"]
                record["radical"] = source["radical"]
                record["extra"] = ""
                break

    records.sort(key=lambda row: ord(row["char"]))
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="\n") as handle:
        handle.write("#qalam-characters\t1\n")
        handle.write("#source\tUnihan 18.0 (Unicode Character Database)\n")
        handle.write("#license\tUnicode License v3\n")
        handle.write("#url\thttps://www.unicode.org/Public/UCD/latest/ucd/Unihan.zip\n")
        handle.write("#radicals\thttps://www.unicode.org/Public/UCD/latest/ucd/CJKRadicals.txt\n")
        handle.write("#columns\tchar\tpinyin\tradicalNumber\tradical\textraStrokes\ttotalStrokes\t"
                     "traditional\tsimplified\tdefinition\n")
        for row in records:
            handle.write("\t".join([
                row["char"],
                row["pinyin"],
                str(row["number"]) if row["number"] else "",
                row["radical"],
                str(row["extra"]) if row["number"] else "",
                row["total"],
                row["traditional"],
                row["simplified"],
                row["definition"],
            ]) + "\n")

    size = os.path.getsize(OUT)
    print("wrote %s: %d characters, %.1f KB (skipped %d non-BMP)" %
          (OUT, len(records), size / 1024.0, skipped))


if __name__ == "__main__":
    main()
