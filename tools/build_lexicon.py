#!/usr/bin/env python3
"""Build Qalam's local lexicon packs.

Outputs:
  data/lexicon/ar-lane.tsv.gz
  data/lexicon/zh-cedict.tsv.gz

Lane's Arabic-English Lexicon is public-domain text digitized by the Perseus
Digital Library (CC BY-SA 3.0 for the Perseus XML). CC-CEDICT is CC BY-SA 4.0.
The generated packs keep the source and license in every row's source column.

Lane's XML uses a Buckwalter-like transliteration rather than Arabic script.
The converter below is intentionally conservative: an entry is skipped when a
character cannot be mapped, instead of shipping a wrong Arabic headword.

Usage:
  python tools/build_lexicon.py
  python tools/build_lexicon.py --no-download   # use .launch-check/lexicon-src
"""

import argparse
import gzip
import html
import json
import re
import sys
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CACHE = ROOT / ".launch-check" / "lexicon-src"
OUT = ROOT / "data" / "lexicon"

LANE_REPO = "laneslexicon/lexicon_xml"
LANE_RAW = "https://raw.githubusercontent.com/" + LANE_REPO + "/master/"
LANE_TREE = "https://api.github.com/repos/" + LANE_REPO + "/git/trees/master?recursive=1"
CEDICT_URL = "https://www.mdbg.net/chinese/export/cedict/cedict_1_0_ts_utf-8_mdbg.txt.gz"

LANE_SOURCE = "Lane's Arabic-English Lexicon (1863-93), Perseus"
CEDICT_SOURCE = "CC-CEDICT (CC BY-SA 4.0)"

# Lane/Perseus transliteration. Vowels are removed for an undiacritized
# lookup form; the original transliteration is kept in the pronunciation column.
BW = {
    "'": "\u0621",  # hamza
    "|": "\u0622",  # alef with madda
    ">": "\u0623",  # alef with hamza above
    "<": "\u0625",  # alef with hamza below
    "&": "\u0624",  # waw with hamza
    "}": "\u0626",  # yeh with hamza
    "A": "\u0627",  # alef
    "b": "\u0628",
    "p": "\u0629",  # teh marbuta
    "t": "\u062a",
    "v": "\u062b",
    "j": "\u062c",
    "H": "\u062d",
    "x": "\u062e",
    "d": "\u062f",
    "*": "\u0630",
    "r": "\u0631",
    "z": "\u0632",
    "s": "\u0633",
    "$": "\u0634",
    "S": "\u0635",
    "D": "\u0636",
    "T": "\u0637",
    "Z": "\u0638",
    "E": "\u0639",
    "g": "\u063a",
    "f": "\u0641",
    "q": "\u0642",
    "k": "\u0643",
    "l": "\u0644",
    "m": "\u0645",
    "n": "\u0646",
    "h": "\u0647",
    "w": "\u0648",
    "Y": "\u0649",
    "y": "\u064a",
    "3": "\u0639",  # occasional ASCII ayin in the digitization
    "2": "\u0621",
    "9": "\u0635",
    "^": "\u0621",
}
DROP = set("auioFNK~_#-")


def fetch(url, path):
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists() and path.stat().st_size > 0:
        return
    print("fetch " + url)
    request = urllib.request.Request(url, headers={"User-Agent": "Qalam-lexicon-builder/1"})
    with urllib.request.urlopen(request, timeout=120) as response:
        data = response.read()
    path.write_bytes(data)


def lane_xml_files(download):
    CACHE.mkdir(parents=True, exist_ok=True)
    tree_path = CACHE / "lane-tree.json"
    if download and not tree_path.exists():
        fetch(LANE_TREE, tree_path)
    if tree_path.exists():
        tree = json.loads(tree_path.read_text(encoding="utf-8"))
        files = [item["path"] for item in tree.get("tree", [])
                 if item.get("type") == "blob" and item["path"].endswith(".xml")]
    else:
        files = sorted(p.name for p in (CACHE / "lane").glob("*.xml"))
    if not files:
        raise SystemExit("No Lane XML files found. Run once without --no-download.")
    lane_dir = CACHE / "lane"
    lane_dir.mkdir(parents=True, exist_ok=True)
    result = []
    for name in files:
        target = lane_dir / name
        if download:
            fetch(LANE_RAW + urllib.parse.quote(name), target)
        if target.exists():
            result.append(target)
    return result


ROOT_CHARS = set("bptvjHxdzs$SDTZEgfqklmnhwYy")


def convert_root(value):
    """Convert a Lane root to Arabic script.

    Lane writes the hamza on waw/yeh with a following ``^`` (for example
    ``$uw^obuwbN`` is shu'buwb). Treating ``^`` as a standalone hamza produces
    wrong words, so this pack deliberately indexes Lane by root. Roots are
    consonant-only and convert reliably; entry keys stay as transliteration
    metadata instead of claiming a form we cannot reconstruct safely.
    """
    if not value or any(char not in ROOT_CHARS for char in value.strip()):
        return None
    text = "".join(BW[char] for char in value.strip())
    if len(text) < 2 or len(text) > 5:
        return None
    return text


def clean_text(value):
    text = html.unescape(value or "")
    text = text.replace("\u2014", "-").replace("\u2013", "-")
    text = re.sub(r"\s+", " ", text).strip()
    text = text.strip(" ,;:")
    if len(text) > 280:
        text = text[:277].rstrip(" ,;:") + "..."
    return text


def lane_rows(files):
    rows = []
    seen = set()
    per_headword = {}
    errors = 0
    for path in files:
        try:
            root = ET.parse(path).getroot()
        except Exception as exc:
            print("skip %s: %s" % (path.name, exc))
            errors += 1
            continue
        for div in root.iter("div2"):
            if div.get("type") != "root":
                continue
            root_key = div.get("n", "")
            if not root_key or " " in root_key:
                continue
            root_ar = convert_root(root_key)
            if root_ar is None:
                continue
            for entry in div.iter("entryFree"):
                key = entry.get("key") or ""
                if not key:
                    for form in entry.iter("form"):
                        for orth in form.iter("orth"):
                            if orth.get("type") == "arrow":
                                continue
                            candidate = "".join(orth.itertext()).strip()
                            if candidate:
                                key = candidate
                                break
                        if key:
                            break
                if not key or " " in key or "\t" in key:
                    continue
                headword = root_ar
                glosses = []
                for hi in entry.iter("hi"):
                    if hi.get("rend") != "ital":
                        continue
                    gloss = clean_text("".join(hi.itertext()))
                    if len(gloss) < 3:
                        continue
                    if not glosses:
                        glosses.append(gloss)
                        continue
                    previous = glosses[-1]
                    if previous.endswith((".", "!", "?")):
                        break
                    # Lane often splits one rendering across adjacent italic
                    # runs ("He asked" + "to dictate it"). Follow a colon,
                    # comma, or lowercase continuation, then stop at a period.
                    if gloss[0].islower() or previous.endswith((":", ";", ",")):
                        glosses.append(gloss)
                    else:
                        break
                    if len(glosses) >= 3:
                        break
                if not glosses:
                    continue
                parts = [item.rstrip(":;, ") for item in glosses if item.rstrip(":;, ")]
                gloss = clean_text("; ".join(parts))
                if len(gloss.split()) < 2:
                    continue
                if per_headword.get(headword, 0) >= 12:
                    continue
                identity = (headword, gloss)
                if identity in seen:
                    continue
                seen.add(identity)
                rows.append(("ar", headword, key, key, root_ar, "", "classical",
                             "classical", gloss, LANE_SOURCE))
                per_headword[headword] = per_headword.get(headword, 0) + 1
    print("lane: %d rows, %d headwords, %d file errors" %
          (len(rows), len(per_headword), errors))
    return rows


CEDICT_LINE = re.compile(r"^(\S+)\s+(\S+)\s+\[([^\]]+)\]\s+/(.*)/$")


def cedict_rows(path):
    rows = []
    seen = set()
    with gzip.open(path, "rt", encoding="utf-8", errors="replace") as handle:
        for line in handle:
            line = line.rstrip("\r\n")
            if not line or line.startswith("#"):
                continue
            match = CEDICT_LINE.match(line)
            if not match:
                continue
            traditional, simplified, pinyin, gloss = match.groups()
            if not re.search(r"[\u4e00-\u9fff]", simplified):
                continue
            glosses = [clean_text(item) for item in gloss.split("/") if item.strip()]
            if not glosses:
                continue
            glosses = [item for item in glosses if item]
            if not glosses:
                continue
            combined = "; ".join(glosses)
            if len(combined) > 500:
                combined = combined[:497].rstrip(" ;") + "..."
            identity = (simplified, combined)
            if identity in seen:
                continue
            seen.add(identity)
            alternate = traditional if traditional != simplified else ""
            rows.append(("zh", simplified, alternate, pinyin, "", "", "modern",
                         "modern", combined, CEDICT_SOURCE))
    print("cedict: %d rows" % len(rows))
    return rows


def write_pack(path, rows):
    path.parent.mkdir(parents=True, exist_ok=True)
    header = "lang\theadword\talt\tpron\troot\tpos\tera\tregister\tgloss\tsource\n"
    with gzip.open(path, "wt", encoding="utf-8", newline="\n") as handle:
        handle.write(header)
        for row in rows:
            handle.write("\t".join(value.replace("\t", " ") for value in row) + "\n")
    print("wrote %s (%d bytes)" % (path, path.stat().st_size))


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--no-download", action="store_true",
                        help="use cached Lane/CC-CEDICT files only")
    args = parser.parse_args(argv)

    download = not args.no_download
    cedict_path = CACHE / "cedict.txt.gz"
    if download:
        fetch(CEDICT_URL, cedict_path)
    if not cedict_path.exists():
        raise SystemExit("CC-CEDICT missing: " + str(cedict_path))

    lane_files = lane_xml_files(download)
    print("lane files: %d" % len(lane_files))
    lane = lane_rows(lane_files)
    cedict = cedict_rows(cedict_path)
    if not lane:
        raise SystemExit("Lane extraction produced no rows.")
    if not cedict:
        raise SystemExit("CC-CEDICT extraction produced no rows.")
    write_pack(OUT / "ar-lane.tsv.gz", lane)
    write_pack(OUT / "zh-cedict.tsv.gz", cedict)
    return 0


if __name__ == "__main__":
    sys.exit(main())
