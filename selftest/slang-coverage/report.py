#!/usr/bin/env python3
"""Turn scan.py output into the ranked coverage report.

Ranks by usable dialogue, not by raw alignment size, because the shipped app
only ever holds a 100k or 2M sample per direction. A pair can be huge and still
be useless if the alignment is junk.
"""

import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))

NAMES = {
    "es": "Spanish", "fr": "French", "de": "German", "it": "Italian",
    "pt": "Portuguese", "ru": "Russian", "ar": "Arabic",
    "zh": "Chinese", "zh_cn": "Chinese (Simplified)",
    "zh_tw": "Chinese (Traditional)", "zh_CN": "Chinese (Simplified)",
    "zh_TW": "Chinese (Traditional)", "pt_BR": "Portuguese (Brazil)",
    "ja": "Japanese", "ko": "Korean",
    "tr": "Turkish", "nl": "Dutch", "pl": "Polish", "uk": "Ukrainian",
    "ro": "Romanian", "he": "Hebrew", "hi": "Hindi", "vi": "Vietnamese",
}

# What the curated phrasebook in src/Colloquial.cs already covers.
CURATED = {
    "es": "96 es/en", "ar": "101 ar>en, 28 en>ar",
}


def usable(row):
    return row["v2024_pairs"] * row["accept_aware"]


def tier(row):
    """Tied to the pack sizes the app actually ships."""
    n = usable(row)
    if n >= 2000000:
        return "fills a 2M pack"
    if n >= 100000:
        return "fills a 100k pack"
    return "below 100k"


def main():
    with open(os.path.join(HERE, "raw.json"), encoding="utf-8") as handle:
        everything = json.load(handle)
    rows = [r for r in everything if not r.get("note")]
    missing = [r for r in everything if r.get("note")]
    # v2024's Portuguese side is pt_BR; the generic pt pair is a smaller subset
    # of the same dialogue and would otherwise appear as a second Portuguese row.
    if any(r["lang"] == "pt_BR" for r in rows):
        rows = [r for r in rows if r["lang"] != "pt"]
    # Superseded probes: before the pair codes were known, Chinese was tried as
    # zh/zh_cn and Portuguese as pt. Those misses are not findings.
    superseded = {"zh", "zh_cn", "zh_tw"}
    if any(r["lang"] in ("zh_CN", "zh_TW") for r in rows):
        missing = [r for r in missing if r["lang"] not in superseded]
    rows.sort(key=lambda r: usable(r), reverse=True)

    out = []
    out.append("# OpenSubtitles v2024 coverage by language, English pair")
    out.append("")
    out.append("Measured with `scan.py`: for each language, a 20 MB prefix of the")
    out.append("English-<lang> TMX stream is filtered with the same acceptance")
    out.append("rules as `src/Concordance.cs`, then the surviving share is applied")
    out.append("to the full v2024 alignment size reported by the OPUS API.")
    out.append("\"In a 100k pack\" is how many usable pairs a 100,000-pair pack")
    out.append("would actually hold; a 2M pack holds at most 2,000,000 and scales")
    out.append("the same way. Rank is by usable pairs, not by raw size.")
    out.append("")
    out.append("| # | Language | aligned pairs | accepted | usable pairs | in a 100k pack | in a 2M pack | tier |")
    out.append("| ---: | --- | ---: | ---: | ---: | ---: | ---: | --- |")
    for i, r in enumerate(rows, 1):
        pairs = r["v2024_pairs"]
        acc = r["accept_aware"]
        n = usable(r)
        at100 = int(min(100000, pairs) * acc)
        at2m = int(min(2000000, pairs) * acc)
        out.append("| %d | %s | %s | %.1f%% | %s | %s | %s | %s |"
                   % (i, NAMES.get(r["lang"], r["lang"]),
                      "{:,}".format(pairs), acc * 100,
                      "{:,}".format(int(n)), "{:,}".format(at100),
                      "{:,}".format(at2m), tier(r)))

    if missing:
        out.append("")
        out.append("Not available as a direct English pair:")
        for r in missing:
            out.append("- %s (%s)" % (NAMES.get(r["lang"], r["lang"]), r["note"]))

    out.append("")
    out.append("## What the numbers mean")
    out.append("")
    out.append("- Everything above is *general* dialogue. It contains slang because")
    out.append("  subtitles contain slang, not because anyone tagged it. The app")
    out.append("  currently has no way to rank a colloquial line above a flat one.")
    out.append("- A probe-based slang density was measured and then discarded: with")
    out.append("  hand-written profanity lists it is dominated by artefacts (Arabic")
    out.append("  clitics and substrings inflate it by an order of magnitude, and")
    out.append("  Italian film clusters can sample near zero). Volume and")
    out.append("  acceptance are the two figures that survive scrutiny.")
    out.append("- Acceptance is a junk filter, not an alignment-quality score. OPUS")
    out.append("  alignment confidence is not evaluated here.")
    out.append("- The sample is a prefix of the stream, so it reflects the first")
    out.append("  documents in the file. Counts good to roughly a percentage point.")
    out.append("- Nearly every language here can fill a 2M pack. Dialogue supply is")
    out.append("  not what separates them; shipping size, alignment quality, and the")
    out.append("  absence of any register ranking are what decide whether the")
    out.append("  examples feel like Reverso's.")
    out.append("")

    path = os.path.join(HERE, "ranking.md")
    with open(path, "w", encoding="utf-8") as handle:
        handle.write("\n".join(out))
    print(path)
    print("%d languages ranked" % len(rows))


if __name__ == "__main__":
    main()
