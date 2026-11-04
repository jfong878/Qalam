#!/usr/bin/env python3
"""Run the 13-block, 15-language PDF formatting regression suite.

This is deliberately usable from the release script, not only by hand. It
checks the generated report, page count, source-font preservation, copy-layer
contaminants, and the complex-script copy text that exercises the ActualText
wrapper in documents/pdf_translate.py.
"""

import argparse
import json
import re
import subprocess
import sys
from pathlib import Path

import pymupdf

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT / "documents") not in sys.path:
    sys.path.insert(0, str(ROOT / "documents"))
import pdf_translate

TARGETS = [
    "es", "fr", "de", "pt", "it", "ru", "ar", "he",
    "fa", "ur", "zh-CN", "ja", "ko", "hi", "th",
]
EXPECTED_FONTS = {
    "es": "Calibri", "fr": "Calibri", "de": "Calibri", "pt": "Calibri",
    "it": "Calibri", "ru": "Calibri", "ar": "Calibri", "he": "Calibri",
    "fa": "Calibri", "ur": "Calibri", "zh-CN": "Microsoft YaHei",
    "ja": "Yu Gothic", "ko": "Malgun Gothic", "hi": "Nirmala UI",
    "th": "Leelawadee UI",
}
# These are the words whose glyphs exposed the shared Arabic base and complex
# Indic/Thai copy-layer defects. ActualText makes them exact again.
EXPECTED_COPY = {
    "ar": ["ملخص", "احترافي", "منسق العمليات"],
    "hi": ["व्यावसायिक", "वरिष्ठ", "यूनिवर्सिटी", "प्रतिक्रिया", "रिकॉर्ड", "शिक्षा"],
    "th": ["ดำเนินงาน", "ประสานงาน", "ประสบการณ์"],
}
PUA_OR_FALLBACK = re.compile(r"[\ue000-\uf8ff\u019f]")
ALLOWED_LATIN = (
    "jordan.lee@example.com",
    "gordan.lee@example.com",
    "+1 555 0100",
)


def parse_report(path):
    fields = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        if "=" in line:
            key, value = line.split("=", 1)
            fields[key] = value
    return fields


def remove_allowed_latin(text):
    for token in ALLOWED_LATIN:
        text = text.replace(token, "")
    return text


def actual_text_markers(doc):
    """Return the logical text stored in PDF ActualText marked content."""
    markers = []
    for page in doc:
        for xref in page.get_contents():
            try:
                stream = doc.xref_stream(xref).decode("latin1")
            except Exception:
                continue
            for encoded in re.findall(r"/ActualText\s*<([0-9A-Fa-f]+)>", stream):
                try:
                    value = bytes.fromhex(encoded).decode("utf-16-be")
                except Exception:
                    continue
                markers.append(value.lstrip("\ufeff"))
    return markers


def check_target(target, exe, fixture, out_dir):
    out_pdf = out_dir / (target + ".pdf")
    report_path = out_dir / (target + ".report.txt")
    stdout_path = out_dir / (target + ".stdout.txt")
    for path in (out_pdf, report_path, stdout_path):
        try:
            path.unlink()
        except FileNotFoundError:
            pass

    command = [
        str(exe), "--docselftest",
        "--input", str(fixture),
        "--output", str(out_pdf),
        "--target", target,
        "--report", str(report_path),
    ]
    try:
        proc = subprocess.run(
            command,
            cwd=str(ROOT),
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=180,
        )
    except Exception as exc:
        return ["%s: could not run docselftest: %s" % (target, exc)]
    stdout_path.write_text(
        (proc.stdout or "") + (proc.stderr or ""), encoding="utf-8"
    )
    problems = []
    if proc.returncode != 0:
        problems.append("%s: docselftest exited %d" % (target, proc.returncode))
    if not report_path.exists():
        problems.append("%s: report missing" % target)
        return problems
    fields = parse_report(report_path)
    expected_fields = {
        "translated": "13", "units": "13", "pages": "1",
        "skipped": "0", "warnings": "0", "exists": "True",
    }
    for key, expected in expected_fields.items():
        if fields.get(key) != expected:
            problems.append(
                "%s: %s=%r, expected %r" % (target, key, fields.get(key), expected)
            )
    if not out_pdf.exists():
        problems.append("%s: output PDF missing" % target)
        return problems

    doc = pymupdf.open(str(out_pdf))
    try:
        if doc.page_count != 1:
            problems.append("%s: pages=%d, expected 1" % (target, doc.page_count))
        fonts = sorted({info[3] for page in doc for info in page.get_fonts(full=True)})
        if not any(EXPECTED_FONTS[target].lower() in font.lower() for font in fonts):
            problems.append(
                "%s: expected font %r, got %r"
                % (target, EXPECTED_FONTS[target], fonts)
            )
        raw = "\n".join(page.get_text() for page in doc)
        normalized = pdf_translate.normalize_text(raw)
        if PUA_OR_FALLBACK.search(raw):
            problems.append("%s: PUA or U+019F in copy text" % target)
        markers = actual_text_markers(doc)
        if len(markers) < 13:
            problems.append(
                "%s: only %d ActualText block(s), expected 13"
                % (target, len(markers))
            )
        if target in EXPECTED_COPY:
            for phrase in EXPECTED_COPY[target]:
                if not any(phrase in marker for marker in markers):
                    problems.append(
                        "%s: ActualText is missing %r" % (target, phrase)
                    )
            if target in {"hi", "th"}:
                for marker in markers:
                    remainder = remove_allowed_latin(marker)
                    if re.search(r"[A-Za-z]", remainder):
                        problems.append(
                            "%s: unexpected Latin/IPA characters in ActualText" % target
                        )
                        break
        if fields.get("warnings") not in (None, "0"):
            problems.append("%s: report contains warnings" % target)
    finally:
        doc.close()
    return problems


def main(argv=None):
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8", errors="replace")
        except Exception:
            pass
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--exe", default=str(ROOT / "dist" / "Qalam.exe"),
        help="Qalam executable to test",
    )
    parser.add_argument(
        "--fixture", default=str(ROOT / "selftest" / "format30" / "source.pdf"),
        help="13-block source PDF fixture",
    )
    parser.add_argument(
        "--out", default=str(ROOT / "selftest" / "format30" / "run"),
        help="directory for generated PDFs and reports",
    )
    parser.add_argument(
        "--languages", default=",".join(TARGETS),
        help="comma-separated targets; defaults to the full release set",
    )
    args = parser.parse_args(argv)

    exe = Path(args.exe).resolve()
    fixture = Path(args.fixture).resolve()
    out_dir = Path(args.out).resolve()
    targets = [item.strip() for item in args.languages.split(",") if item.strip()]
    if not exe.exists():
        print("ERROR: executable not found: %s" % exe, file=sys.stderr)
        return 2
    if not fixture.exists():
        print("ERROR: fixture not found: %s" % fixture, file=sys.stderr)
        return 2
    out_dir.mkdir(parents=True, exist_ok=True)

    failures = []
    summary = []
    for target in targets:
        problems = check_target(target, exe, fixture, out_dir)
        summary.append({"target": target, "ok": not problems, "problems": problems})
        if problems:
            failures.extend(problems)
            print("FAIL %-5s %s" % (target, "; ".join(problems)))
        else:
            print("PASS %-5s 13/13 blocks, 1 page, 0 skipped, 0 warnings" % target)

    (out_dir / "summary.json").write_text(
        json.dumps({"ok": not failures, "results": summary}, ensure_ascii=False, indent=1),
        encoding="utf-8",
    )
    if failures:
        print("\n%d formatting/copy failure(s):" % len(failures), file=sys.stderr)
        for failure in failures:
            print("  " + failure, file=sys.stderr)
        return 1
    print("\nAll %d language targets passed." % len(targets))
    return 0


if __name__ == "__main__":
    sys.exit(main())
