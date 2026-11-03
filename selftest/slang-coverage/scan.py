#!/usr/bin/env python3
"""Measure how much usable colloquial dialogue OpenSubtitles v2024 holds per pair.

For each language it reads the OPUS API pair statistics, then range-downloads a
bounded prefix of the pair XML, so the sample is a real slice of the alignment
rather than a guess. Two filters are applied:

  qalam  - the exact acceptance filter in src/Concordance.cs (Good())
  aware  - the same, but CJK word counts are character based so Japanese and
           Chinese are not rejected for lacking spaces

A line counts as colloquial when it contains one of a small set of high
confidence profanity/slang probes for that language. That is a lower bound on
slang, not a complete measure: it catches the register markers translators
reliably preserve, not idioms or new coinages.
"""

import argparse
import json
import re
import sys
import urllib.request
import zlib

# The xml/ files hold alignment pointers only (linkGrp/link). The segmented
# text, and therefore the dialogue, is in the TMX stream for the pair.
API = ("https://opus.nlpl.eu/opusapi/?corpus=OpenSubtitles"
       "&source=en&target={lang}&preprocessing=tmx")

# Target-side probes. Deliberately high confidence words that survive
# translation, so the measurement does not depend on my guessing idioms.
PROBES = {
    "es": ["mierda", "joder", "cabron", "puta", "coño", "pendejo", "verga",
           "hostia", "gilipollas", "chinga"],
    "fr": ["merde", "putain", "bordel", "foutre", "salope", "chier", "nique",
           "connard"],
    "de": ["scheiße", "scheisse", "verdammt", "arschloch", "ficken", "mist",
           "blöd", "kacke"],
    "it": ["cazzo", "merda", "stronzo", "porca", "minchia", "figa", "coglione"],
    "pt": ["merda", "porra", "caralho", "foda", "puta", "bosta", "cacete"],
    "ru": ["блядь", "хуй", "пиздец", "говно", "сука", "чёрт", "черт", "ебать"],
    "ar": ["خر", "كس", "شرموط", "زبالة", "لعنة", "حقير", "غبي"],
    "zh": ["操", "他妈的", "傻逼", "混蛋", "妈的", "靠", "屌"],
    "ja": ["くそ", "ちくしょう", "バカ", "畜生", "やばい", "マジ", "ふざけ"],
    "ko": ["씨발", "개새끼", "미친", "젠장", "좆", "빌어먹을", "지랄"],
    "tr": ["siktir", "amk", "orospu", "bok", "lan", "kahretsin", "piç"],
    "nl": ["kut", "godverdomme", "klootzak", "verdomme", "hoer", "tyfus"],
    "pl": ["kurwa", "chuj", "pierdol", "cholera", "skurwysyn", "jebać"],
    "uk": ["блядь", "хуй", "піздець", "лайно", "сука", "чорт", "їбати"],
    "ro": ["futu-i", "rahat", "dracu", "pula", "căcat", "prost", "nenorocit"],
    "he": ["לעזאזל", "חרא", "זין", "בן זונה", "דפוק", "מטומטם"],
    "hi": ["चूतिया", "गांड", "कमीना", "साला", "हरामी", "भाड़"],
    "vi": ["địt", "đéo", "khốn", "chó", "vãi", "mẹ kiếp"],
}

CJK = {"zh", "zh_cn", "zh_tw", "ja", "ko"}


def cjk(lang):
    return lang.lower() in CJK

TU = re.compile(r"<tu\b.*?</tu>", re.S)
TUV = re.compile(r'<tuv[^>]*xml:lang="([^"]+)"[^>]*>\s*<seg>(.*?)</seg>', re.S)
ENT = [("&lt;", "<"), ("&gt;", ">"), ("&amp;", "&"), ("&quot;", '"'),
       ("&apos;", "'")]


def unescape(text):
    for a, b in ENT:
        text = text.replace(a, b)
    return " ".join(text.split())


def words(text, lang):
    n = len(text.split())
    if cjk(lang):
        # CJK subtitles usually carry no spaces; count characters instead so the
        # shared filter does not read a full sentence as one word.
        chars = sum(1 for c in text if c.isalnum())
        return max(n, chars // 3)
    return n


def good(source, target, lang, aware, max_chars=220, min_words=2, max_words=24):
    if not source or not target:
        return False
    if not any(c.isalpha() for c in source) or not any(c.isalpha() for c in target):
        return False
    if len(source) < 4 or len(target) < 2 or len(source) > max_chars:
        return False
    if len(target) > max_chars + 60:
        return False
    tw = words(target, lang) if aware else len(target.split())
    sw = len(source.split())
    if sw < min_words or sw > max_words:
        return False
    if tw < 2 or tw > max_words + 8:
        return False
    for junk in ("http://", "https://", "www.", "<", ">", "-->", "00:"):
        if junk in source or junk in target:
            return False
    return sum(1 for c in source if c.isalpha()) >= 3


def pair_stats(lang):
    url = API.format(lang=lang)
    with urllib.request.urlopen(url, timeout=45) as response:
        data = json.loads(response.read().decode("utf-8"))
    rows = [c for c in data.get("corpora", []) if c.get("version") == "v2024"]
    if not rows:
        rows = [c for c in data.get("corpora", []) if c.get("latest") == "True"]
    return rows[0] if rows else None


def prefix(url, size):
    request = urllib.request.Request(url, headers={"Range": "bytes=0-%d" % (size - 1)})
    with urllib.request.urlopen(request, timeout=90) as response:
        return response.read()


def inflate(blob):
    try:
        d = zlib.decompressobj(16 + zlib.MAX_WBITS)
        return d.decompress(blob)
    except zlib.error as ex:
        return b""


def measure(lang, size):
    row = pair_stats(lang)
    if row is None:
        return {"lang": lang, "note": "no v2024 pair with English"}
    blob = prefix(row["url"], size)
    text = inflate(blob).decode("utf-8", "replace")
    units = TU.findall(text)
    base = lang.split("_")[0]
    probes = PROBES.get(lang) or PROBES.get(base) or []

    ok_qalam = ok_aware = hits = 0
    for unit in units:
        segs = {}
        for code, body in TUV.findall(unit):
            segs.setdefault(code, unescape(body))
        source = segs.get("en")
        target = segs.get(lang) or segs.get(base) or segs.get("zh")
        if not source or not target:
            continue
        if good(source, target, lang, False):
            ok_qalam += 1
        if good(source, target, lang, True):
            ok_aware += 1
            low = target.lower()
            if any(p in low for p in probes):
                hits += 1

    return {
        "lang": lang,
        "v2024_pairs": row["alignment_pairs"],
        "v2024_url": row["url"],
        "sampled": len(units),
        "accept_qalam": ok_qalam / len(units) if units else 0.0,
        "accept_aware": ok_aware / len(units) if units else 0.0,
        "colloquial_rate": hits / ok_aware if ok_aware else 0.0,
        "est_colloquial_pairs": int(row["alignment_pairs"] * ok_aware / len(units)
                                    * (hits / ok_aware)) if units and ok_aware else 0,
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--langs", default=",".join(PROBES))
    parser.add_argument("--bytes", type=int, default=12000000)
    parser.add_argument("--out")
    args = parser.parse_args()

    wanted = [x.strip() for x in args.langs.split(",") if x.strip()]
    results = []
    if args.out:
        # Append as each language finishes so a slow or missing pair, or a
        # killed run, never discards the measurements already taken.
        try:
            with open(args.out + ".jsonl", encoding="utf-8") as handle:
                for line in handle:
                    line = line.strip()
                    if line:
                        results.append(json.loads(line))
        except OSError:
            pass

    done = {row["lang"] for row in results}
    for lang in wanted:
        if lang in done:
            print("%-3s cached" % lang, flush=True)
            continue
        try:
            row = measure(lang, args.bytes)
        except Exception as ex:  # a missing pair is a finding, not a crash
            row = {"lang": lang, "note": "%s: %s" % (type(ex).__name__, ex)}
        results.append(row)
        if args.out:
            with open(args.out + ".jsonl", "a", encoding="utf-8") as handle:
                handle.write(json.dumps(row, ensure_ascii=False) + "\n")
        summary = row.get("note") or (
            "%8.1fM pairs  accept %4.1f%%  colloquial %4.1f%%  est %6.0fk"
            % (row["v2024_pairs"] / 1e6, row["accept_aware"] * 100,
               row["colloquial_rate"] * 100, row["est_colloquial_pairs"] / 1e3))
        print("%-3s %s" % (lang, summary), flush=True)

    if args.out:
        with open(args.out, "w", encoding="utf-8") as handle:
            json.dump(results, handle, ensure_ascii=False, indent=1)
    print("done", file=sys.stderr)


if __name__ == "__main__":
    main()
