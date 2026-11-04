"""PDF geometry bridge for Qalam document translation.

Qalam translates the extracted text. This script only reads text block
geometry and writes the translated text back into the same rectangles, so
the page layout, images, and colors stay in place.

Commands:
    extract <input.pdf> <blocks.json>
    detect  <input.pdf> <source.json>
    apply   <input.pdf> <translations.json> <output.pdf>

Requires PyMuPDF (``python -m pip install pymupdf``).
"""

import io
import json
import os
import re
import statistics
import subprocess
import sys
import tempfile
import unicodedata

try:
    import pymupdf
except ImportError:
    try:
        import fitz as pymupdf  # PyMuPDF < 1.24 compatibility.
    except ImportError:
        sys.stderr.write(
            "PyMuPDF is required for PDF document translation.\n"
            "Install it with: python -m pip install pymupdf\n"
        )
        raise SystemExit(3)


RTL_TARGETS = {"ar", "fa", "ur", "he"}
BULLET_CHARS = "\u2022\u25cf\u25aa\u25e6\u25c6\u25a0"
OCR_LANGUAGES = ["en-US", "ar-SA", "es-ES", "fr-FR", "de-DE", "ru", "zh-Hans-CN"]
OCR_SOURCE = {"en-US": "en", "ar-SA": "ar", "es-ES": "es", "fr-FR": "fr",
              "de-DE": "de", "ru": "ru", "zh-Hans-CN": "zh-CN"}
OCR_DPI = 200
OCR_SCRIPT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "ocr_windows.ps1")
FONT_FAMILY_ALIASES = {
    "helvetica": "arial",
    "helveticaneue": "arial",
    "nimbussans": "arial",
    "nimbussansl": "arial",
    "liberationsans": "arial",
    "freesans": "arial",
    "times": "timesnewroman",
    "timesroman": "timesnewroman",
    "nimbusroman": "timesnewroman",
    "liberationserif": "timesnewroman",
    "courier": "couriernew",
    "nimbusmono": "couriernew",
    "liberationmono": "couriernew",
    "aptos": "calibri",
    "aptosdisplay": "calibri",
}

DEFAULT_FALLBACK_FONTS = {
    "sans": ["Calibri", "Segoe UI"],
    "serif": ["Times New Roman", "Georgia"],
    "mono": ["Consolas", "Courier New"],
}

SCRIPT_FALLBACK_FONTS = {
    "ar": {"sans": ["Calibri", "Segoe UI", "Tahoma"],
           "serif": ["Times New Roman", "Calibri"],
           "mono": ["Calibri", "Segoe UI", "Tahoma"]},
    "fa": {"sans": ["Calibri", "Segoe UI", "Tahoma"],
           "serif": ["Times New Roman", "Calibri"],
           "mono": ["Calibri", "Segoe UI", "Tahoma"]},
    "ur": {"sans": ["Calibri", "Segoe UI", "Tahoma"],
           "serif": ["Times New Roman", "Calibri"],
           "mono": ["Calibri", "Segoe UI", "Tahoma"]},
    "he": {"sans": ["Calibri", "Segoe UI", "Tahoma"],
           "serif": ["Times New Roman", "Calibri"],
           "mono": ["Calibri", "Segoe UI", "Tahoma"]},
    "zh-CN": {"sans": ["Microsoft YaHei"],
              "serif": ["SimSun", "Microsoft YaHei"],
              "mono": ["Microsoft YaHei"]},
    "zh-TW": {"sans": ["Microsoft JhengHei"],
              "serif": ["Microsoft JhengHei"],
              "mono": ["Microsoft JhengHei"]},
    "ja": {"sans": ["Yu Gothic", "Meiryo", "MS Gothic"],
           "serif": ["Yu Mincho", "MS Mincho", "Yu Gothic"],
           "mono": ["MS Gothic"]},
    "ko": {"sans": ["Malgun Gothic"],
           "serif": ["Batang", "Malgun Gothic"],
           "mono": ["Malgun Gothic"]},
    "hi": {"sans": ["Nirmala UI"], "serif": ["Nirmala UI"], "mono": ["Nirmala UI"]},
    "th": {"sans": ["Leelawadee UI", "Tahoma"],
           "serif": ["Leelawadee UI", "Tahoma"],
           "mono": ["Leelawadee UI", "Tahoma"]},
}

SCRIPT_FALLBACK_FILES = {
    "ar": {"sans": ["segoeui.ttf", "tahoma.ttf", "calibri.ttf"],
           "serif": ["times.ttf", "segoeui.ttf"],
           "mono": ["segoeui.ttf", "tahoma.ttf"]},
    "fa": {"sans": ["segoeui.ttf", "tahoma.ttf", "calibri.ttf"],
           "serif": ["times.ttf", "segoeui.ttf"],
           "mono": ["segoeui.ttf", "tahoma.ttf"]},
    "ur": {"sans": ["segoeui.ttf", "tahoma.ttf", "calibri.ttf"],
           "serif": ["times.ttf", "segoeui.ttf"],
           "mono": ["segoeui.ttf", "tahoma.ttf"]},
    "he": {"sans": ["segoeui.ttf", "tahoma.ttf", "calibri.ttf"],
           "serif": ["times.ttf", "segoeui.ttf"],
           "mono": ["segoeui.ttf", "tahoma.ttf"]},
    "zh-CN": {"sans": ["msyh.ttc"], "serif": ["simsun.ttc", "msyh.ttc"],
              "mono": ["msyh.ttc"]},
    "zh-TW": {"sans": ["msjh.ttc"], "serif": ["msjh.ttc"], "mono": ["msjh.ttc"]},
    "ja": {"sans": ["YuGothM.ttc", "msgothic.ttc"],
           "serif": ["msmincho.ttc", "YuGothM.ttc"],
           "mono": ["msgothic.ttc"]},
    "ko": {"sans": ["malgun.ttf"], "serif": ["batang.ttc", "malgun.ttf"],
           "mono": ["malgun.ttf"]},
    "hi": {"sans": ["Nirmala.ttc"], "serif": ["Nirmala.ttc"], "mono": ["Nirmala.ttc"]},
    "th": {"sans": ["leelawui.ttf"], "serif": ["leelawui.ttf"],
           "mono": ["leelawui.ttf"]},
}

SCRIPT_SAMPLES = {
    "ar": "مرحبا بالعالم",
    "fa": "سلام دنیا",
    "ur": "سلام دنیا ٹھیک",
    "he": "שלום עולם",
    "zh": "中文测试",
    "zh-CN": "中文测试",
    "zh-TW": "中文測試",
    "ja": "日本語のテスト",
    "ko": "한국어 테스트",
    "hi": "नमस्ते दुनिया",
    "th": "สวัสดีชาวโลก",
    "ru": "Привет мир",
    "el": "Γειά σου",
    "vi": "Tiếng Việt",
    "tr": "Türkçe ğüşiöç",
    "de": "Grüße schön",
    "fr": "Français ça va",
    "es": "Español ¿qué tal?",
    "pt": "Português ação",
    "it": "Italiano città",
    "pl": "Zażółć gęślą",
    "cs": "Příliš žluťoučký",
    "ro": "Română știință",
}

KNOWN_FONT_FILES = {
    "yugothic": "YuGothM.ttc",
    "yugothm": "YuGothM.ttc",
    "yugothr": "YuGothR.ttc",
    "yugothb": "YuGothB.ttc",
    "yugothicmedium": "YuGothM.ttc",
    "yugothicui": "YuGothM.ttc",
    "msgothic": "msgothic.ttc",
    "msmincho": "msmincho.ttc",
    "meiryo": "meiryo.ttc",
    "microsoftyahei": "msyh.ttc",
    "microsoftjhenghei": "msjh.ttc",
    "simsun": "simsun.ttc",
    "nsimsun": "simsun.ttc",
    "mingliu": "mingliu.ttc",
    "pmingliu": "mingliu.ttc",
    "malgungothic": "malgun.ttf",
    "nirmalaui": "Nirmala.ttc",
    "nirmalatext": "Nirmala.ttc",
    "leelawadeeui": "leelawui.ttf",
    "batang": "batang.ttc",
    "arialunicode": "arialuni.ttf",
}

FONT_FILE_VARIANTS = {
    "msyh.ttc": {"bold": "msyhbd.ttc"},
    "msjh.ttc": {"bold": "msjhbd.ttc"},
    "yugothm.ttc": {"bold": "YuGothB.ttc"},
    "yugothr.ttc": {"bold": "YuGothB.ttc"},
    "simsun.ttc": {"bold": "simsun.ttc"},
    "malgun.ttf": {"bold": "malgunbd.ttf"},
    "leelawui.ttf": {"bold": "leelauib.ttf"},
    "nirmala.ttc": {"bold": "Nirmala.ttc"},
    "segoeui.ttf": {"bold": "segoeuib.ttf", "italic": "segoeuii.ttf",
                     "bolditalic": "segoeuiz.ttf"},
    "tahoma.ttf": {"bold": "tahomabd.ttf"},
    "times.ttf": {"bold": "timesbd.ttf", "italic": "timesi.ttf",
                   "bolditalic": "timesbi.ttf"},
    "calibri.ttf": {"bold": "calibrib.ttf", "italic": "calibrii.ttf",
                     "bolditalic": "calibriz.ttf"},
    "arial.ttf": {"bold": "arialbd.ttf", "italic": "ariali.ttf",
                   "bolditalic": "arialbi.ttf"},
    "georgia.ttf": {"bold": "georgiab.ttf", "italic": "georgiai.ttf",
                     "bolditalic": "georgiaz.ttf"},
    "gara.ttf": {"bold": "garabd.ttf", "italic": "garait.ttf"},
}


def fail(message, code=2):
    sys.stderr.write(message.rstrip() + "\n")
    raise SystemExit(code)


def normalize_text(text):
    # PDF text often contains Arabic presentation forms (U+FE70..U+FEFF).
    # Translation services expect the normal letters, and Word/PDF readers
    # render both, so normalize once at extraction.
    text = unicodedata.normalize("NFKC", text)
    text = text.replace("\u00ad", "")
    # Rebuilt Arabic ToUnicode maps the separate dot glyphs to a zero-width
    # space so copied text is not forced to carry PUA placeholders. Remove the
    # marker but keep real word spaces; extract_blocks suppresses MuPDF's
    # synthetic spaces when it sees these markers.
    text = text.replace("\u200b", "")
    text = text.replace("\r\n", "\n").replace("\r", "\n")
    lines = [line.strip() for line in text.split("\n") if line.strip()]
    joined = ""
    for line in lines:
        if joined.endswith("-") and line[:1].islower():
            joined = joined[:-1] + line
        else:
            joined = (joined + " " + line).strip() if joined else line
    return " ".join(joined.split())


def has_letters(text):
    return any(ch.isalpha() for ch in text)


def hex_color(value):
    if value is None:
        return "000000"
    return "%06x" % (int(value) & 0xFFFFFF)


def block_alignment(bbox, page_width, source_rtl=False):
    """Infer paragraph alignment from its position, not from its glyph direction.

    A PDF line of Arabic is right-aligned, but a full-width Arabic paragraph
    reads as a full-width block. Short centered headings must remain centered.
    """
    x0, _, x1, _ = bbox
    width = max(1.0, x1 - x0)
    center = (x0 + x1) / 2.0
    if width < page_width * 0.70 and abs(center - page_width / 2.0) < page_width * 0.06:
        return "center"
    if source_rtl:
        return "right"
    right_gap = page_width - x1
    if right_gap < page_width * 0.15 and x0 > page_width * 0.25:
        return "right"
    return "left"


def rtl_text(text):
    rtl = 0
    ltr = 0
    for ch in text:
        code = ord(ch)
        if 0x0590 <= code <= 0x08FF or 0xFB1D <= code <= 0xFDFF or 0xFE70 <= code <= 0xFEFF:
            rtl += 1
        elif ("A" <= ch <= "Z") or ("a" <= ch <= "z"):
            ltr += 1
    return rtl > ltr


def line_direction(lines, text):
    horizontal = 0
    for line in lines:
        direction = line.get("dir", (1, 0))
        if direction and direction[0] < 0:
            horizontal += 1
    return horizontal * 2 > len(lines) or rtl_text(text)


def starts_bullet_text(text):
    return bool(text) and text.lstrip()[:1] in BULLET_CHARS


def dominant_run(line):
    runs = [run for run in line.get("Runs", []) if str(run.get("Text", "")).strip()]
    if not runs:
        return None
    return max(runs, key=lambda run: len(str(run.get("Text", ""))))


def line_style(line):
    run = dominant_run(line)
    if run is None:
        return None
    return (
        round(float(run.get("Size", 0) or 0), 1),
        bool(run.get("Bold")),
        bool(run.get("Italic")),
        str(run.get("Font", "")),
        str(run.get("Color", "")),
    )


def style_matches(a, b):
    if a is None or b is None:
        return a is b
    return (abs(a[0] - b[0]) <= 0.5 and a[1] == b[1] and a[2] == b[2]
            and a[3] == b[3] and a[4] == b[4])


def merge_runs(runs):
    merged = []
    for run in runs:
        value = str(run.get("Text", ""))
        if not value:
            continue
        if (merged and merged[-1]["Bold"] == run["Bold"]
                and merged[-1]["Italic"] == run["Italic"]
                and merged[-1]["Color"] == run["Color"]
                and abs(merged[-1]["Size"] - run["Size"]) <= 0.01
                and merged[-1]["Font"] == run["Font"]):
            merged[-1]["Text"] += " " + value
        else:
            merged.append(dict(run))
    for run in merged:
        run["Words"] = len(str(run.get("Text", "")).split())
    return merged


def split_styled_blocks(blocks):
    """Split a PDF text block wherever its style or a list item changes.

    PDF producers often put a bold job title and the regular employer line in
    one block, or pack several bullet items together. Translating that as one
    paragraph loses the resume hierarchy and the list boundaries, so split at
    each style change and at each new bullet before paragraphs are merged.
    """
    result = []
    for block in blocks:
        lines = block.get("Lines") or []
        if len(lines) <= 1:
            result.append(block)
            continue

        groups = []
        current = []
        current_style = None
        for line in lines:
            style = line_style(line)
            bullet = bool(line.get("Bullet")) or starts_bullet_text(str(line.get("Text", "")))
            if current and (bullet or (style is not None and current_style is not None
                                       and not style_matches(style, current_style))):
                groups.append(current)
                current = []
            current.append(line)
            if style is not None:
                current_style = style
        if current:
            groups.append(current)

        if len(groups) <= 1:
            result.append(block)
            continue

        for index, group in enumerate(groups):
            segment = dict(block)
            if index:
                segment["Id"] = "%s_%d" % (block["Id"], index + 1)
            segment["Lines"] = group
            segment["Text"] = normalize_text(
                " ".join(str(line.get("Text", "")) for line in group)
            )
            segment["Bbox"] = [
                min(float(line["Bbox"][0]) for line in group),
                min(float(line["Bbox"][1]) for line in group),
                max(float(line["Bbox"][2]) for line in group),
                max(float(line["Bbox"][3]) for line in group),
            ]
            style = line_style(group[0]) or (0.0, False, False, "", "")
            segment["Size"] = round(style[0] or float(block.get("Size", 11)), 2)
            segment["Bold"] = style[1]
            segment["Italic"] = style[2]
            segment["Font"] = style[3] or block.get("Font", "")
            segment["Color"] = style[4] or block.get("Color", "000000")
            segment["LineHeight"] = round(max(
                segment["Size"] * 1.15,
                (segment["Bbox"][3] - segment["Bbox"][1]) / max(1, len(group)),
            ), 2)
            segment["Rtl"] = rtl_text(segment["Text"])
            segment["Align"] = block_alignment(
                segment["Bbox"], float(segment.get("PageWidth", 595)), segment["Rtl"]
            )
            segment["SourceBboxes"] = [list(line["Bbox"]) for line in group]
            segment["Runs"] = merge_runs(
                [run for line in group for run in line.get("Runs", [])]
            )
            result.append(segment)
    return result


def merge_blocks(blocks):
    """Join adjacent line boxes that belong to the same paragraph.

    Some PDF producers expose every visual line as its own text block. Keeping
    those lines separate would translate half-sentences and lose the space
    needed to reflow a longer target language. Merge only boxes with the same
    style, direction, and horizontal edge, with a line-sized vertical gap.
    """
    merged = []
    for block in blocks:
        if not merged:
            merged.append(block)
            continue

        previous = merged[-1]
        # A new bullet always begins its own translation unit; merging it into
        # the previous item would renumber the list in the target language.
        if starts_bullet_text(str(block.get("Text", ""))):
            merged.append(block)
            continue
        size = min(float(previous.get("Size", 11)), float(block.get("Size", 11)))
        gap = block["Bbox"][1] - previous["Bbox"][3]
        size_tolerance = 1.2 if previous.get("Ocr") and block.get("Ocr") else 0.75
        same_style = (
            previous.get("Page") == block.get("Page")
            and abs(float(previous.get("Size", 0)) - float(block.get("Size", 0))) <= size_tolerance
            and previous.get("Color") == block.get("Color")
            and previous.get("Font") == block.get("Font")
            and bool(previous.get("Bold")) == bool(block.get("Bold"))
            and bool(previous.get("Italic")) == bool(block.get("Italic"))
            and bool(previous.get("Rtl")) == bool(block.get("Rtl"))
            and bool(previous.get("Ocr")) == bool(block.get("Ocr"))
        )
        a = previous["Bbox"]
        b = block["Bbox"]
        edge_tolerance = 18.0 if previous.get("Ocr") else 3.0
        # OCR frequently splits one visual line into several boxes. Those
        # boxes have overlapping vertical extents, so the paragraph gap test
        # rejects them before the edge test can run. Join them explicitly.
        same_line_ocr = False
        if same_style and previous.get("Ocr") and block.get("Ocr"):
            vertical_overlap = min(a[3], b[3]) - max(a[1], b[1])
            min_height = min(a[3] - a[1], b[3] - b[1])
            horizontal_gap = b[0] - a[2]
            same_line_ocr = (
                vertical_overlap >= min_height * 0.50
                and -1.5 <= horizontal_gap <= edge_tolerance
            )
        if not same_style or (not same_line_ocr and (gap < -1.5 or gap > max(6.0, size * 0.80))):
            merged.append(block)
            continue

        if not same_line_ocr:
            width_a = max(1.0, a[2] - a[0])
            width_b = max(1.0, b[2] - b[0])
            overlap = min(a[2], b[2]) - max(a[0], b[0])
            overlap_ratio = 0.40 if previous.get("Ocr") else 0.45
            if overlap < min(width_a, width_b) * overlap_ratio:
                merged.append(block)
                continue

            left_edge = abs(a[0] - b[0]) <= edge_tolerance
            right_edge = abs(a[2] - b[2]) <= edge_tolerance
            centered = abs((a[0] + a[2]) - (b[0] + b[2])) <= 4.0
            if not (left_edge or right_edge or centered):
                merged.append(block)
                continue

        source_boxes = previous.setdefault("SourceBboxes", [list(previous["Bbox"])])
        source_boxes.extend(block.get("SourceBboxes", [block["Bbox"]]))
        previous.setdefault("Lines", []).extend(block.get("Lines", []))
        previous.setdefault("Runs", []).extend(block.get("Runs", []))
        previous["Ocr"] = bool(previous.get("Ocr") or block.get("Ocr"))
        if block.get("OcrLanguage"):
            previous["OcrLanguage"] = block["OcrLanguage"]
        previous["Text"] = normalize_text(previous["Text"] + " " + block["Text"])
        previous["Bbox"] = [
            min(a[0], b[0]),
            min(a[1], b[1]),
            max(a[2], b[2]),
            max(a[3], b[3]),
        ]
        previous["Align"] = block_alignment(
            previous["Bbox"], float(previous.get("PageWidth", 595)), bool(previous.get("Rtl"))
        )

    return merged


def ocr_image(page, language):
    """Run Windows OCR for one page and return line/word geometry."""
    if not os.path.isfile(OCR_SCRIPT):
        return None
    temp = tempfile.NamedTemporaryFile(suffix=".png", delete=False)
    temp.close()
    try:
        pixmap = page.get_pixmap(dpi=OCR_DPI)
        pixmap.save(temp.name)
        completed = subprocess.run(
            ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass",
             "-File", OCR_SCRIPT, "-Path", temp.name, "-Language", language],
            stdout=subprocess.PIPE, stderr=subprocess.PIPE,
            timeout=60, text=True, encoding="utf-8", errors="replace",
        )
        if completed.returncode != 0:
            return None
        text = (completed.stdout or "").strip()
        if not text:
            return None
        if not text.startswith("{"):
            start = text.find("{")
            if start < 0:
                return None
            text = text[start:]
        return json.loads(text)
    except Exception:
        return None
    finally:
        try:
            os.unlink(temp.name)
        except Exception:
            pass


def choose_ocr_language(page):
    """Pick the installed OCR language with the most readable letters."""
    best = None
    best_score = -1
    for language in OCR_LANGUAGES:
        data = ocr_image(page, language)
        if not data:
            continue
        text = " ".join(str(line.get("text", "")) for line in data.get("lines", []))
        score = sum(1 for ch in text if ch.isalpha())
        # Prefer a language that actually produced words, and prefer the
        # first candidate on a tie (English before the other Latin engines).
        if score > best_score:
            best = language
            best_score = score
    return best if best_score > 0 else None


def ocr_font_size(text, height):
    """Estimate the source font size from the ink height of an OCR line.

    OCR word boxes measure ink, not the em square: a line with both ascenders
    and descenders is close to one em, while an all-caps line only covers the
    cap height. Dividing by a fixed number made titles far too small.
    """
    letters = [ch for ch in text if ch.isalpha()]
    if not letters:
        factor = 1.15
    else:
        descender = any(ch.lower() in "gjpqy" for ch in letters)
        ascender = any(ch.isupper() or ch.lower() in "bdfhklt" for ch in letters)
        if descender and ascender:
            factor = 1.05
        elif descender:
            factor = 1.32
        elif ascender:
            factor = 1.24
        else:
            factor = 1.55
    return max(6.0, round(height * factor, 2))


def background_colour(pix, rect):
    """Median colour of the pixels just outside a box, so patches blend in."""
    pad = max(2, min(6, int(max(1, rect.y1 - rect.y0) * 0.35)))
    samples = []
    xs = range(max(0, rect.x0 - pad), min(pix.width, rect.x1 + pad), 3)
    ys = range(max(0, rect.y0 - pad), min(pix.height, rect.y1 + pad), 3)
    for x in xs:
        for y in (rect.y0 - pad, rect.y1 + pad - 1):
            if 0 <= y < pix.height:
                samples.append(pix.pixel(x, y))
    for y in ys:
        for x in (rect.x0 - pad, rect.x1 + pad - 1):
            if 0 <= x < pix.width:
                samples.append(pix.pixel(x, y))
    if not samples:
        return 255 if pix.n == 1 else (255,) * pix.n
    if isinstance(samples[0], int):
        ordered = sorted(samples)
        return ordered[len(ordered) // 2]
    channels = []
    for index in range(len(samples[0])):
        ordered = sorted(item[index] for item in samples)
        channels.append(ordered[len(ordered) // 2])
    return tuple(channels)


def erase_ocr_sources(page, boxes):
    """Remove scanned glyphs by painting over them in a rendered bitmap.

    A scan is one big image, so a PDF redaction cannot delete its glyphs: it
    either leaves the pixels or deletes the whole image. Rendering the text
    area, painting the boxes in their local background colour, and putting
    that bitmap back is lossless for the rest of the page.
    """
    if not boxes:
        return True
    doc = page.parent
    dpi = OCR_DPI
    longest = max(page.rect.width, page.rect.height)
    if longest * dpi / 72.0 > 6000:
        dpi = max(72, int(6000 * 72 / longest))
    colorspace = pymupdf.csRGB
    try:
        images = page.get_images(full=True)
        if images and all(int(image[4]) <= 1 for image in images):
            colorspace = pymupdf.csGRAY
    except Exception:
        pass
    try:
        pix = page.get_pixmap(dpi=dpi, alpha=False, colorspace=colorspace)
    except Exception:
        return False
    scale = dpi / 72.0
    bounds = pymupdf.IRect(0, 0, pix.width, pix.height)
    for box in boxes:
        target = pymupdf.IRect(
            int(round(box.x0 * scale)) - 1,
            int(round(box.y0 * scale)) - 1,
            int(round(box.x1 * scale)) + 2,
            int(round(box.y1 * scale)) + 2,
        ) & bounds
        if target.is_empty:
            continue
        pix.set_rect(target, background_colour(pix, target))
    old_streams = list(page.get_contents())
    try:
        page.insert_image(page.rect, pixmap=pix)
    except Exception:
        return False
    # The scan is drawn by the old content stream, which sits above anything
    # newly inserted. Drop it now that the patched bitmap carries the page.
    remaining = page.get_contents()
    if len(remaining) <= len(old_streams):
        return False
    try:
        for xref in old_streams:
            doc.update_stream(xref, b"")
    except Exception:
        return False
    return True


def ocr_page_blocks(page, page_index, language):
    data = ocr_image(page, language)
    if not data:
        return []
    scale = 72.0 / OCR_DPI
    page_width = page.rect.width
    blocks = []
    for line_index, line in enumerate(data.get("lines", [])):
        words = line.get("words") or []
        if not words:
            continue
        text = normalize_text(str(line.get("text", "")))
        # OCR often reads a bullet as a leading period or zero. Restore the
        # marker before block merging so it stays attached to its sentence.
        text = re.sub(r"^[.\u00b7\u2022\u25cf]\s+", "\u25cf ", text)
        text = re.sub(r"^0\s+", "\u25cf ", text)
        if not text or not has_letters(text):
            continue
        x0 = min(float(word["x"]) for word in words)
        y0 = min(float(word["y"]) for word in words)
        x1 = max(float(word["x"]) + float(word["w"]) for word in words)
        y1 = max(float(word["y"]) + float(word["h"]) for word in words)
        bbox = [round(x0 * scale, 3), round(y0 * scale, 3),
                round(x1 * scale, 3), round(y1 * scale, 3)]
        height = max(1.0, (y1 - y0) * scale)
        size = ocr_font_size(text, height)
        run = {
            "Text": text,
            "Bold": False,
            "Italic": False,
            "Color": "000000",
            "Size": size,
            "Font": "OCR",
            "Words": len(text.split()),
        }
        source_rtl = rtl_text(text)
        line_entry = {
            "Text": text,
            "Bbox": bbox,
            "Bullet": bool(text[:1] in BULLET_CHARS),
            "Runs": [run],
        }
        blocks.append({
            "Id": "p%do%d" % (page_index, line_index),
            "Page": page_index,
            "Bbox": bbox,
            "Text": text,
            "Size": size,
            "Color": "000000",
            "Font": "OCR",
            "Bold": False,
            "Italic": False,
            "Serif": False,
            "Monospace": False,
            "Rtl": source_rtl,
            "PageWidth": round(page_width, 2),
            "Align": block_alignment(bbox, page_width, source_rtl),
            "LineHeight": round(max(size * 1.15, height), 2),
            "SourceBboxes": [bbox],
            "Runs": [run],
            "Lines": [line_entry],
            "Ocr": True,
            "OcrLanguage": language,
        })

    # OCR reports geometry, not weight. A short line noticeably larger than the
    # page's body text is a heading or job title; giving it bold keeps the same
    # hierarchy the digital-text path gets from font flags.
    sizes = [float(block.get("Size", 0)) for block in blocks]
    if sizes:
        body_size = statistics.median(sizes)
        if body_size > 0:
            for block in blocks:
                width = float(block["Bbox"][2]) - float(block["Bbox"][0])
                if (float(block.get("Size", 0)) >= body_size * 1.08
                        and width < page_width * 0.60):
                    block["Bold"] = True
                    for run in block.get("Runs", []):
                        run["Bold"] = True
    # OCR line boxes do not carry font weight, and its size estimate can be
    # smaller for a heading than for body text. A short all-caps line is a
    # reliable second signal for the headings resumes actually use.
    for block in blocks:
        letters = [ch for ch in str(block.get("Text", "")) if ch.isalpha()]
        if (letters and len(letters) <= 40
                and sum(1 for ch in letters if ch.isupper()) / float(len(letters)) >= 0.85):
            block["Bold"] = True
            for run in block.get("Runs", []):
                run["Bold"] = True
    return blocks


def extract_blocks(doc, ocr_language=None):
    blocks = []
    warnings = []
    for page_index, page in enumerate(doc):
        page_width = page.rect.width
        page_text_dict = page.get_text("dict")
        if "\u200b" in page.get_text():
            inhibit_spaces = getattr(pymupdf, "TEXT_INHIBIT_SPACES", 0)
            if inhibit_spaces:
                page_text_dict = page.get_text("dict", flags=inhibit_spaces)
        page_blocks = page_text_dict.get("blocks", [])
        text_on_page = 0
        for block_index, block in enumerate(page_blocks):
            if block.get("type") != 0:
                continue
            lines = block.get("lines", [])
            if not lines:
                continue

            rotated = any(abs(line.get("dir", (1, 0))[1]) > 0.01 for line in lines)
            if rotated:
                warnings.append("page %d: rotated text left unchanged" % (page_index + 1))
                continue

            line_texts = []
            line_entries = []
            span_sizes = []
            span_weights = []
            span_flags = []
            span_colors = []
            for line in lines:
                spans = line.get("spans", [])
                text = "".join(span.get("text", "") for span in spans)
                if not text.strip():
                    continue
                line_texts.append(text.strip())

                runs = []
                for span in spans:
                    value = span.get("text", "")
                    if not value:
                        continue
                    flags = int(span.get("flags", 0))
                    size_value = round(float(span.get("size", 0) or 0), 2)
                    color_value = hex_color(span.get("color", 0))
                    font = str(span.get("font", ""))
                    bold = bool(flags & 16) or bool(re.search(
                        r"(?i)(bold|black|heavy|semibold|demibold)", font))
                    italic = bool(flags & 2) or bool(re.search(
                        r"(?i)(italic|oblique)", font))
                    if (runs and runs[-1]["Bold"] == bold and runs[-1]["Italic"] == italic
                            and runs[-1]["Color"] == color_value
                            and abs(runs[-1]["Size"] - size_value) <= 0.01
                            and runs[-1]["Font"] == font):
                        runs[-1]["Text"] += value
                    else:
                        runs.append({
                            "Text": value,
                            "Bold": bold,
                            "Italic": italic,
                            "Color": color_value,
                            "Size": size_value,
                            "Font": font,
                        })
                    if value.strip():
                        span_sizes.append(float(span.get("size", 0) or 0))
                        span_weights.append(max(1, len(value)))
                        span_flags.append(flags)
                        span_colors.append(span.get("color", 0))

                for run in runs:
                    run["Words"] = len(run["Text"].split())
                line_entries.append({
                    "Text": normalize_text(text),
                    "Bbox": [round(float(v), 3) for v in line.get("bbox", block["bbox"])],
                    "Bullet": bool(text.lstrip()[:1] in BULLET_CHARS),
                    "Runs": runs,
                })

            if not line_texts:
                continue

            text = normalize_text(" ".join(line_texts))
            if not text or not has_letters(text):
                continue
            text_on_page += len(text)

            size = 0.0
            if span_sizes:
                ordered = sorted(zip(span_sizes, span_weights), key=lambda pair: pair[0])
                total = sum(weight for _, weight in ordered)
                seen = 0
                for value, weight in ordered:
                    seen += weight
                    if seen * 2 >= total:
                        size = value
                        break
            if size <= 0:
                size = 11.0

            flags = span_flags[0] if span_flags else 0
            color = span_colors[0] if span_colors else 0
            line_height = max(size * 1.15, (block["bbox"][3] - block["bbox"][1]) / max(1, len(lines)))
            source_rtl = line_direction(lines, text)

            block_runs = []
            for entry in line_entries:
                for run in entry.get("Runs", []):
                    if (block_runs and block_runs[-1]["Bold"] == run["Bold"]
                            and block_runs[-1]["Italic"] == run["Italic"]
                            and block_runs[-1]["Color"] == run["Color"]
                            and abs(block_runs[-1]["Size"] - run["Size"]) <= 0.01
                            and block_runs[-1]["Font"] == run["Font"]):
                        block_runs[-1]["Text"] += " " + run["Text"]
                    else:
                        block_runs.append(dict(run))
            for run in block_runs:
                run["Words"] = len(run["Text"].split())
            block_bold = any(bool(run.get("Bold")) for run in block_runs)
            block_italic = any(bool(run.get("Italic")) for run in block_runs)

            blocks.append({
                "Id": "p%db%d" % (page_index, block_index),
                "Page": page_index,
                "Bbox": [round(float(v), 3) for v in block["bbox"]],
                "Text": text,
                "Size": round(size, 2),
                "Color": hex_color(color),
                "Font": (lines[0].get("spans") or [{}])[0].get("font", ""),
                "Bold": block_bold,
                "Italic": block_italic,
                "Serif": bool(flags & 4),
                "Monospace": bool(flags & 8),
                "Rtl": source_rtl,
                "PageWidth": round(page_width, 2),
                "Align": block_alignment(block["bbox"], page_width, source_rtl),
                "LineHeight": round(line_height, 2),
                "SourceBboxes": [[round(float(v), 3) for v in block["bbox"]]],
                "Runs": block_runs,
                "Lines": line_entries,
            })
        if text_on_page == 0:
            if ocr_language is None:
                ocr_language = choose_ocr_language(page)
            if ocr_language:
                ocr_blocks = ocr_page_blocks(page, page_index, ocr_language)
                if ocr_blocks:
                    blocks.extend(ocr_blocks)
                    text_on_page = sum(len(block.get("Text", "")) for block in ocr_blocks)
            if text_on_page == 0:
                warnings.append("page %d: no selectable text and OCR found nothing" % (page_index + 1))

    # A mixed document keeps its font on OCR pages too. Only a pure scan has
    # no font name at all, and then find_font uses the script-appropriate face.
    digital_fonts = {}
    for block in blocks:
        if block.get("Ocr"):
            continue
        font = str(block.get("Font", ""))
        if font:
            digital_fonts[font] = digital_fonts.get(font, 0) + len(block.get("Text", ""))
    if digital_fonts:
        dominant = max(digital_fonts, key=digital_fonts.get)
        for block in blocks:
            if not block.get("Ocr"):
                continue
            block["Font"] = dominant
            for run in block.get("Runs", []):
                run["Font"] = dominant
    return merge_blocks(split_styled_blocks(blocks)), warnings


def detect_source(doc):
    sample = []
    for page in doc:
        sample.append(page.get_text("text"))
        if sum(len(part) for part in sample) > 60000:
            break
    text = "\n".join(sample)
    arabic = 0
    latin = 0
    for ch in text:
        code = ord(ch)
        if (0x0600 <= code <= 0x06FF or 0x0750 <= code <= 0x077F
                or 0xFB50 <= code <= 0xFDFF or 0xFE70 <= code <= 0xFEFF):
            arabic += 1
        elif ("A" <= ch <= "Z") or ("a" <= ch <= "z"):
            latin += 1
    if arabic == 0 and latin < 20 and doc.page_count:
        language = choose_ocr_language(doc[0])
        if language:
            return OCR_SOURCE.get(language, "en"), text[:2000]
    # A single Arabic quotation or name does not make an English document
    # Arabic; compare the two scripts instead of using the first match.
    return ("ar" if arabic > latin else "en"), text[:2000]


_FONT_MAP = None
_FONT_FAMILIES = None
_FONT_PATH_FAMILY = {}
_FONT_COVERAGE = {}


def font_directory():
    return os.path.join(os.environ.get("WINDIR", r"C:\Windows"), "Fonts")


def normalize_family(name):
    value = re.sub(r"^[A-Z]{6}\+", "", str(name or ""))
    value = value.split(",")[0]
    value = re.sub(r"\((?:TrueType|OpenType|All res)\)", "", value, flags=re.I)
    value = re.sub(
        r"\b(bold|black|heavy|semibold|demibold|extrabold|italic|oblique|regular)\b",
        "", value, flags=re.I,
    )
    value = re.sub(r"[^A-Za-z0-9]", "", value).lower()
    for suffix in ("psmt", "mt", "ps"):
        if value.endswith(suffix) and len(value) > len(suffix) + 2:
            value = value[:-len(suffix)]
    return value


def installed_fonts():
    global _FONT_MAP, _FONT_FAMILIES
    if _FONT_MAP is not None:
        return _FONT_MAP, _FONT_FAMILIES
    mapping = {}
    families = {}
    fonts = font_directory()
    try:
        import winreg
        keys = (
            (winreg.HKEY_LOCAL_MACHINE,
             r"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts"),
            (winreg.HKEY_CURRENT_USER,
             r"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts"),
        )
        for hive, subkey in keys:
            try:
                key = winreg.OpenKey(hive, subkey)
            except OSError:
                continue
            index = 0
            while True:
                try:
                    name, value, _ = winreg.EnumValue(key, index)
                except OSError:
                    break
                index += 1
                if not isinstance(value, str):
                    continue
                value = os.path.expandvars(value.strip().strip('"'))
                path = value if os.path.isabs(value) else os.path.join(fonts, value)
                if not os.path.isfile(path):
                    continue
                if os.path.splitext(path)[1].lower() not in (".ttf", ".ttc", ".otf"):
                    continue
                clean = re.sub(r"\s*\((?:TrueType|OpenType|All res)\)\s*$", "",
                               name, flags=re.I)
                for part in re.split(r"\s*&\s*", clean):
                    if not part.strip():
                        continue
                    bold = bool(re.search(
                        r"\b(bold|black|heavy|semibold|demibold|extrabold)\b",
                        part, flags=re.I))
                    italic = bool(re.search(r"\b(italic|oblique)\b", part, flags=re.I))
                    family = normalize_family(part)
                    if not family:
                        continue
                    mapping[(family, bold, italic)] = path
                    families.setdefault(family, path)
                    _FONT_PATH_FAMILY[os.path.normcase(os.path.abspath(path))] = family
    except Exception:
        pass
    if not mapping and os.path.isdir(fonts):
        for filename in os.listdir(fonts):
            root, extension = os.path.splitext(filename)
            if extension.lower() not in (".ttf", ".ttc", ".otf"):
                continue
            family = normalize_family(root)
            if not family:
                continue
            path = os.path.join(fonts, filename)
            mapping[(family, False, False)] = path
            families.setdefault(family, path)
            _FONT_PATH_FAMILY[os.path.normcase(os.path.abspath(path))] = family
    _FONT_MAP = mapping
    _FONT_FAMILIES = families
    return mapping, families


def family_path(family, bold=False, italic=False):
    mapping, families = installed_fonts()
    key = normalize_family(family)
    if not key:
        return None
    candidates = [key]
    alias = FONT_FAMILY_ALIASES.get(key)
    if alias:
        candidates.append(alias)
    for candidate in candidates:
        for bold_flag, italic_flag in (
            (bool(bold), bool(italic)), (bool(bold), False),
            (False, bool(italic)), (False, False),
        ):
            path = mapping.get((candidate, bold_flag, italic_flag))
            if path and os.path.isfile(path):
                return path
        if len(candidate) >= 5:
            for available in families:
                if available.startswith(candidate) or candidate.startswith(available):
                    for bold_flag, italic_flag in (
                        (bool(bold), bool(italic)), (bool(bold), False),
                        (False, bool(italic)), (False, False),
                    ):
                        path = mapping.get((available, bold_flag, italic_flag))
                        if path and os.path.isfile(path):
                            return path
    filename = KNOWN_FONT_FILES.get(key)
    if filename:
        path = os.path.join(font_directory(), filename)
        if os.path.isfile(path):
            return path
    return None


def source_category(source_font, serif, monospace):
    name = str(source_font or "").lower()
    if monospace or re.search(r"mono|courier|consol|menlo|monaco", name):
        return "mono"
    if re.search(r"times|serif|georgia|garamond|cambria|roman|book|minion|palatino|"
                 r"baskerville|didot|merriweather|playfair|lora", name):
        return "serif"
    if re.search(r"sans|helvetica|arial|calibri|segoe|tahoma|verdana|roboto|"
                 r"opensans|lato|montserrat|aptos|inter|poppins|raleway|nunito|"
                 r"fira|noto", name):
        return "sans"
    return "serif" if serif else "sans"


def font_covers(font_path, target):
    if not font_path:
        return False
    key = (os.path.normcase(os.path.abspath(font_path)), target)
    if key in _FONT_COVERAGE:
        return _FONT_COVERAGE[key]
    sample = SCRIPT_SAMPLES.get(target, "The quick brown fox")
    letters = [ch for ch in sample if ch.isalpha()]
    covered = True
    try:
        font = pymupdf.Font(fontfile=font_path)
        if hasattr(font, "has_glyph"):
            covered = all(font.has_glyph(ord(ch)) for ch in letters)
        else:
            raise AttributeError("has_glyph unavailable")
    except Exception:
        try:
            from fontTools.ttLib import TTFont
            face = TTFont(font_path, fontNumber=0, lazy=True)
            cmap = face.getBestCmap()
            covered = all(ord(ch) in cmap for ch in letters)
        except Exception:
            covered = True
    _FONT_COVERAGE[key] = covered
    return covered


def fallback_font(target, category):
    names = SCRIPT_FALLBACK_FONTS.get(target, {}).get(category)
    if not names:
        names = DEFAULT_FALLBACK_FONTS.get(category, [])
    for name in names:
        path = family_path(name)
        if path and font_covers(path, target):
            return path
    files = SCRIPT_FALLBACK_FILES.get(target, {}).get(category, [])
    for filename in files:
        path = os.path.join(font_directory(), filename)
        if os.path.isfile(path) and font_covers(path, target):
            return path
    for name in DEFAULT_FALLBACK_FONTS.get(category, []):
        path = family_path(name)
        if path and font_covers(path, target):
            return path
    for filename in ("calibri.ttf", "segoeui.ttf", "times.ttf"):
        path = os.path.join(font_directory(), filename)
        if os.path.isfile(path):
            return path
    return None


def find_font(target, serif=False, monospace=False, source_font=""):
    family = normalize_family(source_font)
    if family and family != "ocr":
        family = FONT_FAMILY_ALIASES.get(family, family)
        path = family_path(family)
        if path and font_covers(path, target):
            return path
    path = fallback_font(target, source_category(source_font, serif, monospace))
    if path:
        return path
    path = family_path("Calibri")
    if path:
        return path
    return os.path.join(font_directory(), "segoeui.ttf")


_TTC_FACE_CACHE = {}


def ttc_style_face(font_path, bold, italic):
    """Extract a styled face from a TTC when Windows ships no separate file.

    Nirmala UI is the important case: its Bold face lives only inside
    Nirmala.ttc, and MuPDF always uses face zero from a collection.
    """
    if not font_path or (not bold and not italic):
        return None
    if os.path.splitext(font_path)[1].lower() not in (".ttc", ".otc"):
        return None
    normalized = os.path.normcase(os.path.abspath(font_path))
    key = (normalized, bool(bold), bool(italic))
    if key in _TTC_FACE_CACHE:
        return _TTC_FACE_CACHE[key]
    try:
        from fontTools.ttLib import TTCollection
        collection = TTCollection(font_path, lazy=False)
    except Exception:
        return None
    for index, face in enumerate(collection.fonts):
        name = face["name"]
        subfamily = (name.getDebugName(2) or "").lower()
        full = (name.getDebugName(4) or "").lower()
        if "semilight" in subfamily:
            continue
        if bold and "bold" not in subfamily and "bold" not in full:
            continue
        if italic and "italic" not in subfamily and "oblique" not in subfamily:
            continue
        if not bold and ("bold" in subfamily or "bold" in full):
            continue
        if not italic and ("italic" in subfamily or "oblique" in subfamily):
            continue
        cache_dir = os.path.join(tempfile.gettempdir(), "qalam-font-cache")
        try:
            os.makedirs(cache_dir, exist_ok=True)
            stem = re.sub(r"[^A-Za-z0-9]+", "-",
                          os.path.splitext(os.path.basename(font_path))[0]).strip("-")
            style = ("bold" if bold else "") + ("italic" if italic else "")
            target = os.path.join(cache_dir, "%s-%d-%s.ttf" % (stem, index, style))
            if not os.path.isfile(target):
                face.save(target)
        except Exception:
            return None
        _TTC_FACE_CACHE[key] = target
        return target
    return None


def style_font(font_path, bold, italic):
    if not font_path:
        return None
    if not bold and not italic:
        return font_path
    ttc_face = ttc_style_face(font_path, bold, italic)
    if ttc_face:
        return ttc_face
    normalized_path = os.path.normcase(os.path.abspath(font_path))
    family = _FONT_PATH_FAMILY.get(normalized_path)
    if family:
        mapping, _ = installed_fonts()
        path = mapping.get((family, bool(bold), bool(italic)))
        if path and os.path.isfile(path):
            return path
    variants = FONT_FILE_VARIANTS.get(os.path.basename(font_path).lower(), {})
    key = "bolditalic" if bold and italic else ("bold" if bold else "italic")
    candidate = variants.get(key)
    if candidate:
        path = os.path.join(os.path.dirname(font_path), candidate)
        if os.path.isfile(path):
            return path
    if family:
        mapping, _ = installed_fonts()
        for bold_flag, italic_flag in ((bool(bold), False), (False, bool(italic)), (False, False)):
            path = mapping.get((family, bold_flag, italic_flag))
            if path and os.path.isfile(path):
                return path
    return font_path


def make_archive(font_path, bold_path=None, italic_path=None, bold_italic_path=None):
    archive = pymupdf.Archive()
    files = (
        (font_path, "qalam-font.ttf"),
        (bold_path, "qalam-bold.ttf"),
        (italic_path, "qalam-italic.ttf"),
        (bold_italic_path, "qalam-bold-italic.ttf"),
    )
    for path, name in files:
        if not path:
            continue
        with open(path, "rb") as handle:
            archive.add((handle.read(), name))
    return archive


def build_css(font_name, size, color, line_height, align, bold, italic, direction):
    # MuPDF's HTML engine reverses physical left/right under direction:rtl:
    # "text-align:right" lays the line out on the left. The RTL default is
    # already right, so omit the property for that case and only override
    # centered or left-aligned lines explicitly.
    if direction == "rtl" and align == "right":
        text_align = ""
    elif direction == "rtl" and align == "left":
        text_align = "text-align:right;"
    else:
        text_align = "text-align:%s;" % align

    if bold and italic:
        family = "QalamBoldItalic"
    elif bold:
        family = "QalamBold"
    elif italic:
        family = "QalamItalic"
    else:
        family = "QalamFont"

    faces = (
        "@font-face {font-family:QalamFont; src:url(qalam-font.ttf);}"
        "@font-face {font-family:QalamBold; src:url(qalam-bold.ttf);}"
        "@font-face {font-family:QalamItalic; src:url(qalam-italic.ttf);}"
        "@font-face {font-family:QalamBoldItalic; src:url(qalam-bold-italic.ttf);}"
    )
    return faces + (
        "* {font-family:%s, sans-serif; font-size:%.2fpx;"
        " color:#%s; line-height:%.2fpx; margin:0; padding:0;"
        " font-weight:normal; font-style:normal;}"
        "div {direction:%s;%s white-space:pre-wrap;}"
        "div.b {padding-left:1.15em; text-indent:-1.15em;}"
        "div.c {padding-left:1.15em;}"
        "span {font-size:inherit;}"
    ) % (
        family, size, color, line_height,
        direction, text_align,
    )


def link_key(link):
    rect = link.get("from")
    box = (0, 0, 0, 0)
    if rect is not None:
        box = (round(float(rect.x0), 1), round(float(rect.y0), 1),
               round(float(rect.x1), 1), round(float(rect.y1), 1))
    target = link.get("to")
    destination = None
    if target is not None:
        destination = (round(float(target.x), 1), round(float(target.y), 1))
    return (box, link.get("kind"), link.get("uri"), link.get("page"), destination)


def find_underline(drawings, link_rect):
    for drawing in drawings:
        rect = drawing.get("rect")
        if rect is None or rect.height > 2.0 or rect.width < 8.0:
            continue
        if (rect.x0 >= link_rect.x0 - 2.0 and rect.x1 <= link_rect.x1 + 2.0
                and rect.y0 >= link_rect.y0 - 2.0 and rect.y1 <= link_rect.y1 + 2.0):
            if any(item[0] == "l" for item in drawing.get("items", [])):
                return drawing
    return None


def insert_relocated_links(page, link_plan, blocks, line_boxes):
    """Reattach links to the translated text, not the vacated source box."""
    current = set(link_key(link) for link in page.get_links())
    for link, block_id, underline in link_plan:
        target = dict(link)
        source_rect = pymupdf.Rect(link.get("from"))
        chosen = None
        if block_id in blocks:
            block_rect = blocks[block_id]
            candidates = []
            for line_rect, line_text in line_boxes:
                intersection = pymupdf.Rect(line_rect) & block_rect
                if not line_text.strip() or intersection.is_empty:
                    continue
                covered = intersection.get_area() / max(1.0, line_rect.get_area())
                if covered >= 0.45:
                    candidates.append((line_rect, line_text))
            if candidates:
                # Preserve the link's vertical position within its paragraph
                # as closely as the translated line count allows.
                link_center = (source_rect.y0 + source_rect.y1) / 2.0
                chosen = min(
                    candidates,
                    key=lambda item: abs((item[0].y0 + item[0].y1) / 2.0 - link_center),
                )[0]
                target["from"] = pymupdf.Rect(chosen)
            else:
                target["from"] = pymupdf.Rect(block_rect)
        key = link_key(target)
        if key in current:
            continue
        try:
            page.insert_link(target)
            current.add(key)
        except Exception:
            pass

        if underline is not None and chosen is not None:
            try:
                y = chosen.y1 - 0.5
                page.draw_line(
                    pymupdf.Point(chosen.x0, y), pymupdf.Point(chosen.x1, y),
                    color=underline.get("color"), width=underline.get("width", 0.6),
                    overlay=True,
                )
            except Exception:
                pass


def escape_html(text):
    return (text or "").replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def starts_bullet(text):
    return bool(text) and text.lstrip()[:1] in BULLET_CHARS


def split_bullets(text):
    value = " ".join((text or "").replace("\r", " ").replace("\n", " ").split())
    if not value:
        return ""
    pattern = re.compile(r"\s*([" + re.escape(BULLET_CHARS) + r"])\s*")

    def repl(match):
        if match.start() == 0:
            return match.group(1) + " "
        return "\n" + match.group(1) + " "

    return pattern.sub(repl, value).strip()


def run_html(text, run, base_size, base_color):
    body = escape_html(text)
    if not run:
        return body
    styles = []
    bold = bool(run.get("Bold"))
    italic = bool(run.get("Italic"))
    if bold and italic:
        styles.append("font-family:QalamBoldItalic")
    elif bold:
        styles.append("font-family:QalamBold")
    elif italic:
        styles.append("font-family:QalamItalic")
    color = str(run.get("Color") or base_color or "000000").lower()
    if base_color and color != str(base_color).lower():
        styles.append("color:#" + color)
    try:
        size = float(run.get("Size", 0) or 0)
        if size > 0 and abs(size - base_size) > 0.4:
            styles.append("font-size:%.2fpx" % size)
    except Exception:
        pass
    if not styles:
        return body
    return '<span style="%s">%s</span>' % (";".join(styles), body)


def render_words(words, runs, base_size, base_color):
    if not words:
        return ""
    if not runs:
        return escape_html(" ".join(words))
    if len(runs) == 1:
        return run_html(" ".join(words), runs[0], base_size, base_color)

    source_total = sum(max(1, int(run.get("Words", 0) or 0)) for run in runs)
    counts = []
    remaining = len(words)
    for index, run in enumerate(runs):
        if index == len(runs) - 1:
            count = remaining
        else:
            count = int(round(len(words) * max(1, int(run.get("Words", 0) or 0)) / float(source_total)))
            count = max(0, min(count, remaining))
        counts.append(count)
        remaining -= count

    parts = []
    used = 0
    for run, count in zip(runs, counts):
        if count <= 0:
            continue
        chunk = " ".join(words[used:used + count])
        parts.append(run_html(chunk, run, base_size, base_color))
        used += count
    if used < len(words):
        parts.append(escape_html(" ".join(words[used:])))
    return " ".join(part for part in parts if part)


def render_line_html(line, runs, base_size, base_color, bullet, continuation=False):
    content = render_words(line.split(), runs, base_size, base_color)
    if not content:
        return ""
    if bullet:
        css_class = ' class="b"'
    elif continuation:
        css_class = ' class="c"'
    else:
        css_class = ""
    return '<div%s>%s</div>' % (css_class, content)


def render_bullet_group(line, group, base_size, base_color):
    words = line.split()
    if not words:
        return ""
    # A bullet whose source lines share one style should wrap naturally in the
    # target language. Splitting it at the source line's word count used to
    # break an Arabic phrase across two lines just because the English source
    # happened to wrap there.
    styles = [line_style(source_line) for source_line in group]
    if len(group) == 1 or all(style_matches(styles[0], style) for style in styles[1:]):
        runs = merge_runs(
            [run for source_line in group for run in source_line.get("Runs", [])]
        )
        return render_line_html(line, runs, base_size, base_color, bullet=True)
    weights = []
    for source_line in group:
        weight = sum(max(1, int(run.get("Words", 0) or 0))
                     for run in source_line.get("Runs", []))
        weights.append(max(1, weight))
    total = sum(weights)
    result = []
    used = 0
    for index, source_line in enumerate(group):
        if index == len(group) - 1:
            count = len(words) - used
        else:
            # Keep the source line's word count as the boundary. Proportional
            # rounding used to pull the first word of the employer line into
            # the bold job title.
            count = max(0, min(int(weights[index]), len(words) - used))
        if count <= 0:
            continue
        chunk = " ".join(words[used:used + count])
        used += count
        runs = source_line.get("Runs", [])
        result.append(render_line_html(
            chunk, runs, base_size, base_color,
            bullet=(index == 0), continuation=(index > 0),
        ))
    if used < len(words):
        result.append(render_line_html(
            " ".join(words[used:]), group[-1].get("Runs", []),
            base_size, base_color, bullet=False, continuation=True,
        ))
    return "".join(result)


def render_translation_html(block, translation, base_size, base_color):
    # Translation models sometimes emit a non-ASCII hyphen. Leelawadee and
    # Malgun do not contain U+2010/U+2011, which would pull a serif fallback
    # into an otherwise clean resume. Normalize it to the plain hyphen.
    translation = translation.replace("\u2010", "-").replace("\u2011", "-")
    lines = [line.strip() for line in split_bullets(translation).split("\n") if line.strip()]
    if not lines:
        return ""
    source_lines = block.get("Lines") or []
    source_bullets = [line for line in source_lines if line.get("Bullet")]
    translated_bullets = [line for line in lines if starts_bullet(line)]

    groups = []
    current = None
    for source_line in source_lines:
        if source_line.get("Bullet"):
            current = [source_line]
            groups.append(current)
        elif current is not None:
            current.append(source_line)

    result = []
    if groups and len(groups) == len(translated_bullets):
        bullet_index = 0
        for line in lines:
            if starts_bullet(line):
                result.append(render_bullet_group(
                    line, groups[bullet_index], base_size, base_color
                ))
                bullet_index += 1
            else:
                result.append(render_line_html(
                    line, block.get("Runs", []), base_size, base_color, False
                ))
    else:
        for line in lines:
            result.append(render_line_html(
                line, block.get("Runs", []), base_size, base_color, starts_bullet(line)
            ))
    return "".join(result)


def page_text_edges(page_blocks):
    boxes = [item.get("Bbox") for item in page_blocks
             if item.get("Bbox") and len(item["Bbox"]) == 4]
    if not boxes:
        return 0.0, 0.0
    return (min(float(box[0]) for box in boxes),
            max(float(box[2]) for box in boxes))


def mirror_short_box(block, bbox, page_blocks, direction, page_width):
    """Mirror a short left/right-edge block to the opposite margin for RTL/LTR.

    A left-aligned heading in an English PDF is a right-aligned heading after
    translation to Arabic. Keeping its narrow source box at the left edge makes
    the Arabic sit in the middle of the page, so move the box to the target
    margin when it is short and the destination space is empty.
    """
    source_align = block.get("Align", "left")
    if direction == "rtl" and source_align != "left":
        return
    if direction == "ltr" and source_align != "right":
        return

    left_edge, right_edge = page_text_edges(page_blocks)
    text_width = right_edge - left_edge
    box_width = bbox.x1 - bbox.x0
    if text_width <= 0 or box_width >= text_width * 0.72:
        return

    if direction == "rtl":
        new_x0 = right_edge - box_width
    else:
        new_x0 = left_edge
    new_x0 = max(0.0, min(new_x0, page_width - box_width))
    new_x1 = new_x0 + box_width
    if new_x1 > page_width:
        return

    for other in page_blocks:
        if other is block:
            continue
        other_box = other.get("Bbox")
        if not other_box or len(other_box) != 4:
            continue
        vertical = min(bbox.y1, float(other_box[3])) - max(bbox.y0, float(other_box[1]))
        horizontal = min(new_x1, float(other_box[2])) - max(new_x0, float(other_box[0]))
        if vertical > 2.5 and horizontal > 2.0:
            return

    bbox.x0 = new_x0
    bbox.x1 = new_x1


def expand_box(block, bbox, page, page_blocks, direction, align):
    # Use empty space to the right (or left for RTL) before shrinking type.
    left_edge, right_edge = page_text_edges(page_blocks)
    if right_edge <= left_edge:
        left_edge, right_edge = 36, page.rect.width - 36
    if direction == "ltr" and align == "left":
        right = right_edge
        for other in page_blocks:
            if other is block:
                continue
            other_box = other.get("Bbox")
            if other_box[3] > bbox.y0 and other_box[1] < bbox.y1 and other_box[0] > bbox.x1 - 1:
                right = min(right, other_box[0] - 3)
        if right > bbox.x1:
            bbox.x1 = right
    elif direction == "rtl" and align == "right":
        left = left_edge
        for other in page_blocks:
            if other is block:
                continue
            other_box = other.get("Bbox")
            if other_box[3] > bbox.y0 and other_box[1] < bbox.y1 and other_box[2] < bbox.x0 + 1:
                left = max(left, other_box[2] + 3)
        if left < bbox.x0:
            bbox.x0 = left

    # Grow downward to the next overlapping block, but never across a column.
    bottom = page.rect.height - 36
    for other in page_blocks:
        if other is block:
            continue
        other_box = other.get("Bbox")
        if other_box[1] <= bbox.y1 - 1:
            continue
        overlap = min(bbox.x1, other_box[2]) - max(bbox.x0, other_box[0])
        if overlap > 12:
            bottom = min(bottom, other_box[1] - 2)
            break
    if bottom > bbox.y1:
        bbox.y1 = bottom


def _target_script_char(char, target):
    code = ord(char)
    if target == "he":
        return 0x0590 <= code <= 0x05FF
    return (
        0x0600 <= code <= 0x06FF
        or 0x0750 <= code <= 0x077F
        or 0x08A0 <= code <= 0x08FF
        or 0xFB50 <= code <= 0xFDFF
        or 0xFE70 <= code <= 0xFEFF
    )


def _latin_char(char):
    code = ord(char)
    return (
        0x0041 <= code <= 0x005A
        or 0x0061 <= code <= 0x007A
        or 0x00C0 <= code <= 0x024F
        or 0x1E00 <= code <= 0x1EFF
    )


def _decode_unicode(hex_text):
    try:
        return bytes.fromhex(hex_text).decode("utf-16-be")
    except Exception:
        return ""


def _parse_tounicode(stream):
    """Read bfchar/bfrange entries so unrelated glyph mappings are preserved."""
    mapping = {}
    for block in re.findall(r"beginbfchar(.*?)endbfchar", stream, re.S):
        for source, dest in re.findall(
            r"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]*)>", block
        ):
            try:
                mapping[int(source, 16)] = _decode_unicode(dest)
            except Exception:
                pass
    for block in re.findall(r"beginbfrange(.*?)endbfrange", stream, re.S):
        for match in re.finditer(
            r"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>\s*(?:<([0-9A-Fa-f]*)>|\[(.*?)\])",
            block,
            re.S,
        ):
            start = int(match.group(1), 16)
            end = int(match.group(2), 16)
            if end < start or end - start > 0xFFFF:
                continue
            if match.group(3) is not None:
                base = _decode_unicode(match.group(3))
                if not base:
                    continue
                for offset in range(end - start + 1):
                    mapping[start + offset] = base[:-1] + chr(ord(base[-1]) + offset)
            else:
                destinations = re.findall(r"<([0-9A-Fa-f]*)>", match.group(4))
                for offset, dest in enumerate(destinations):
                    if start + offset > end:
                        break
                    mapping[start + offset] = _decode_unicode(dest)
    return mapping


def _cmap_unicode_by_gid(font):
    """Choose one useful Unicode spelling for every glyph in an embedded font."""
    chosen = {}
    try:
        cmap = font.getBestCmap() or {}
    except Exception:
        return {}
    for codepoint, glyph_name in cmap.items():
        if codepoint <= 0 or 0xE000 <= codepoint <= 0xF8FF:
            continue
        try:
            gid = font.getGlyphID(glyph_name)
        except Exception:
            continue
        value = unicodedata.normalize("NFKC", chr(codepoint))
        if not value or value == "\ufffd":
            continue
        if 0xFB50 <= codepoint <= 0xFDFF or 0xFE70 <= codepoint <= 0xFEFF:
            rank = 2
        elif (
            0x0600 <= codepoint <= 0x06FF
            or 0x0750 <= codepoint <= 0x077F
            or 0x08A0 <= codepoint <= 0x08FF
            or 0x0590 <= codepoint <= 0x05FF
        ):
            rank = 0
        elif codepoint < 0x80 or 0x00A0 <= codepoint <= 0x024F:
            rank = 1
        else:
            rank = 3
        preference = (rank, codepoint)
        if gid not in chosen or preference < chosen[gid][0]:
            chosen[gid] = (preference, value)
    return {gid: item[1] for gid, item in chosen.items()}


def _shaped_glyph_text(face, shaper, texts, target):
    """Map shaped glyph IDs to the logical text represented by their cluster."""
    import uharfbuzz as hb

    if target in {"ar", "fa", "ur", "he"}:
        direction = "rtl"
        script, language = {
            "ar": ("Arab", "ar"),
            "fa": ("Arab", "fa"),
            "ur": ("Arab", "ur"),
            "he": ("Hebr", "he"),
        }[target]

        def wanted(piece):
            return any(_target_script_char(char, target) for char in piece)
    else:
        direction = "ltr"
        script = "Latn"
        language = "en"

        def wanted(piece):
            return any(_latin_char(char) for char in piece)

    votes = {}

    def vote(gid, value):
        if gid <= 0 or not value:
            return
        bucket = votes.setdefault(gid, {})
        bucket[value] = bucket.get(value, 0) + 1

    for text in texts:
        if not text or not wanted(text):
            continue
        try:
            buffer = hb.Buffer()
            # MONOTONE_CHARACTERS keeps the dot components in the same cluster
            # as their base letter; the advancing glyph owns the cluster text.
            buffer.cluster_level = 1
            buffer.add_str(text)
            buffer.direction = direction
            buffer.script = script
            buffer.language = language
            hb.shape(shaper, buffer)
        except Exception:
            continue
        infos = list(buffer.glyph_infos)
        positions = list(buffer.glyph_positions)
        starts = sorted(set(info.cluster for info in infos))
        for index, start in enumerate(starts):
            end = starts[index + 1] if index + 1 < len(starts) else len(text)
            piece = text[start:end]
            if not wanted(piece):
                continue
            entries = [
                (info, position)
                for info, position in zip(infos, positions)
                if info.cluster == start
            ]
            if not entries:
                continue
            winner, _ = max(entries, key=lambda item: item[1].x_advance)
            if winner.codepoint <= 0:
                continue
            vote(winner.codepoint, piece)
            for info, _ in entries:
                if info.codepoint != winner.codepoint:
                    vote(info.codepoint, "\u200b")

    return {
        gid: max(counts.items(), key=lambda item: (item[1], len(item[0])))[0]
        for gid, counts in votes.items()
    }


def _replace_tounicode(doc, font_xref, mapping):
    entries = [
        (gid, value)
        for gid, value in sorted(mapping.items())
        if 0 < gid <= 0xFFFF and value
    ]
    lines = [
        "/CIDInit /ProcSet findresource begin",
        "12 dict begin",
        "begincmap",
        "/CIDSystemInfo <</Registry(Adobe)/Ordering(UCS)/Supplement 0>> def",
        "/CMapName /Adobe-Identity-UCS def",
        "/CMapType 2 def",
        "1 begincodespacerange",
        "<0000> <FFFF>",
        "endcodespacerange",
        "%d beginbfchar" % len(entries),
    ]
    for gid, value in entries:
        lines.append("<%04X> <%s>" % (gid, value.encode("utf-16-be").hex().upper()))
    lines.extend([
        "endbfchar",
        "endcmap",
        "CMapName currentdict /CMap defineresource pop",
        "end",
        "end",
    ])
    key = doc.xref_get_key(font_xref, "ToUnicode")
    if key[0] == "xref":
        tounicode_xref = int(key[1].split()[0])
    else:
        tounicode_xref = doc.get_new_xref()
        doc.update_object(tounicode_xref, "<<>>")
        doc.xref_set_key(font_xref, "ToUnicode", "%d 0 R" % tounicode_xref)
    doc.update_stream(tounicode_xref, "\r\n".join(lines).encode("latin1"))


def _wrap_actual_text(doc, page, before_contents, text):
    """Mark one inserted HTML block with the exact text it represents.

    A font's ToUnicode map cannot distinguish, for example, Calibri's shared
    Arabic base glyph used with different dot components, or an Indic
    contextual form. PDF ActualText operates at the page-content level and
    gives readers the logical text without changing what is drawn.
    """
    value = str(text or "")
    if not value:
        return 0
    new_streams = [
        xref for xref in page.get_contents()
        if xref not in set(before_contents)
    ]
    if not new_streams:
        return 0
    encoded = ("\ufeff" + value).encode("utf-16-be").hex().upper()
    prefix = ("/Span << /ActualText <%s> >> BDC\n" % encoded).encode("latin1")
    suffix = b"\nEMC\n"
    wrapped = 0
    for xref in new_streams:
        try:
            content = doc.xref_stream(xref)
            if not content:
                continue
            doc.update_stream(xref, prefix + content + suffix)
            wrapped += 1
        except Exception:
            continue
    return wrapped


def repair_copy_layer(doc, texts, target):
    """Rebuild ToUnicode for glyphs whose real text differs from the font cmap.

    Windows fonts such as Calibri store Arabic ligatures and their dot marks in
    glyphs with no Unicode cmap entry. MuPDF then falls back to the glyph ID
    when the PDF is copied, producing PUA or unrelated characters. A ToUnicode
    map built from HarfBuzz clusters gives each advancing glyph its real Arabic
    text and turns the zero-advance dot components into a removable marker.
    Latin-script targets are repaired too because Calibri's "ti" ligature is
    named uni019F and otherwise extracts as the unrelated character U+019F.
    """
    if not texts:
        return 0
    try:
        import uharfbuzz as hb
        from fontTools.ttLib import TTFont
    except ImportError as exc:
        raise RuntimeError(
            "PDF copy-layer repair needs uharfbuzz and fonttools"
        ) from exc

    xrefs = []
    seen = set()
    for page in doc:
        for font_info in page.get_fonts(full=True):
            try:
                xref = int(font_info[0])
            except Exception:
                continue
            if xref > 0 and xref not in seen:
                seen.add(xref)
                xrefs.append(xref)

    patched = 0
    for xref in xrefs:
        try:
            _, _, _, content = doc.extract_font(xref)
        except Exception:
            continue
        if not content:
            continue
        try:
            font = TTFont(io.BytesIO(content))
            face = hb.Face(content)
            shaper = hb.Font(face)
        except Exception:
            continue
        used = _shaped_glyph_text(face, shaper, texts, target)
        if not used:
            continue
        mapping = {}
        try:
            key = doc.xref_get_key(xref, "ToUnicode")
            if key[0] == "xref":
                stream = doc.xref_stream(int(key[1].split()[0])).decode("latin1")
                mapping = _parse_tounicode(stream)
        except Exception:
            mapping = {}
        if not mapping:
            mapping = _cmap_unicode_by_gid(font)
        mapping.update(used)
        if not mapping:
            continue
        _replace_tounicode(doc, xref, mapping)
        patched += 1
    return patched


def apply_translations(input_path, translations_path, output_path, blocks_path=None):
    with open(translations_path, "r", encoding="utf-8") as handle:
        payload = json.load(handle)
    target = str(payload.get("Target", "ar"))
    translations = {
        str(item.get("Id")): str(item.get("Translation", "")).strip()
        for item in payload.get("Blocks", [])
        if item.get("Id") is not None and str(item.get("Translation", "")).strip()
    }

    doc = pymupdf.open(input_path)
    if doc.needs_pass:
        fail("This PDF is password protected. Unlock a copy before translating it.")

    font_name = "QalamFont"
    archives = {}
    direction = "rtl" if target in RTL_TARGETS else "ltr"
    inserted = 0
    failed = []

    merged_blocks = None
    if blocks_path:
        try:
            with open(blocks_path, "r", encoding="utf-8") as handle:
                merged_blocks = json.load(handle).get("Blocks", [])
        except Exception:
            merged_blocks = None
    if merged_blocks is None:
        merged_blocks = extract_blocks(doc)[0]
    by_page = {}
    for block in merged_blocks:
        by_page.setdefault(int(block.get("Page", 0)), []).append(block)
    for page_blocks in by_page.values():
        page_blocks.sort(key=lambda item: item.get("Bbox", [0, 0, 0, 0])[1])

    for page_index, page in enumerate(doc):
        page_blocks = by_page.get(page_index, [])
        pending = []
        for block in page_blocks:
            block_id = block["Id"]
            translation = translations.get(block_id, "")
            if not translation:
                continue

            flags = 0
            if block.get("Bold"): flags |= 16
            if block.get("Italic"): flags |= 2
            if block.get("Serif"): flags |= 4
            if block.get("Monospace"): flags |= 8
            color = block.get("Color", "000000")
            size = float(block.get("Size", 11.0))
            align = block.get("Align", "left")
            line_height = float(block.get("LineHeight", max(size * 1.15, size * 1.3)))
            if direction == "rtl" and align == "left":
                align = "right"
            elif direction == "ltr" and align == "right":
                align = "left"

            bbox = pymupdf.Rect(block["Bbox"])
            bbox.x0 = max(0, bbox.x0 - 1.0)
            bbox.y0 = max(0, bbox.y0 - 1.0)
            bbox.x1 = min(page.rect.width, bbox.x1 + 1.0)
            bbox.y1 = min(page.rect.height, bbox.y1 + 1.0)
            sources = []
            for source in block.get("SourceBboxes", [block["Bbox"]]):
                source_box = pymupdf.Rect(source)
                source_box.x0 = max(0, source_box.x0 - 1.0)
                source_box.y0 = max(0, source_box.y0 - 1.0)
                source_box.x1 = min(page.rect.width, source_box.x1 + 1.0)
                source_box.y1 = min(page.rect.height, source_box.y1 + 1.0)
                sources.append(source_box)
            if block.get("Ocr"):
                # OCR can split one visual line into pieces. The merged block
                # box bridges the punctuation and bullet in the source image.
                ocr_cover = pymupdf.Rect(block["Bbox"])
                ocr_cover.x0 = max(0, ocr_cover.x0 - 2.0)
                ocr_cover.y0 = max(0, ocr_cover.y0 - 2.0)
                ocr_cover.x1 = min(page.rect.width, ocr_cover.x1 + 2.0)
                ocr_cover.y1 = min(page.rect.height, ocr_cover.y1 + 2.0)
                sources.append(ocr_cover)
            mirror_short_box(block, bbox, page_blocks, direction, page.rect.width)
            pending.append((block_id, bbox, sources, translation, size, color, align, line_height, flags))

        if not pending:
            continue

        original_links = page.get_links()
        original_drawings = page.get_drawings()
        link_plan = []
        for link in original_links:
            link_rect = pymupdf.Rect(link.get("from"))
            best_id = None
            best_overlap = 0.0
            for candidate in pending:
                for source_box in candidate[2]:
                    intersection = pymupdf.Rect(source_box) & link_rect
                    if not intersection.is_empty and intersection.get_area() > best_overlap:
                        best_overlap = intersection.get_area()
                        best_id = candidate[0]
            link_plan.append((link, best_id, find_underline(original_drawings, link_rect)))

        ocr_ids = set(item["Id"] for item in page_blocks if item.get("Ocr"))
        for block_id, _, sources, _, _, _, _, _, _ in pending:
            if block_id in ocr_ids:
                continue
            for source_box in sources:
                page.add_redact_annot(source_box, fill=None, cross_out=False)
        try:
            page.apply_redactions(
                images=pymupdf.PDF_REDACT_IMAGE_NONE,
                graphics=getattr(pymupdf, "PDF_REDACT_LINE_ART_NONE", 0),
            )
        except TypeError:
            page.apply_redactions(images=pymupdf.PDF_REDACT_IMAGE_NONE)
        except Exception:
            page.apply_redactions()

        # A PDF link underline is line art, not part of the glyph outline. Keep
        # the page's backgrounds and borders, but clear the old underline inside
        # the link rectangle so it does not sit under an empty spot after the
        # translated link moves within its block.
        if any(underline is not None for _, _, underline in link_plan):
            for _, block_id, underline in link_plan:
                if block_id is None or underline is None:
                    continue
                line_rect = pymupdf.Rect(underline["rect"])
                line_rect.x0 = max(0, line_rect.x0 - 1.0)
                line_rect.y0 = max(0, line_rect.y0 - 1.0)
                line_rect.x1 = min(page.rect.width, line_rect.x1 + 1.0)
                line_rect.y1 = min(page.rect.height, line_rect.y1 + 1.0)
                page.add_redact_annot(line_rect, fill=None, cross_out=False)
            try:
                page.apply_redactions(
                    images=pymupdf.PDF_REDACT_IMAGE_NONE,
                    graphics=pymupdf.PDF_REDACT_LINE_ART_REMOVE_IF_TOUCHED,
                    text=pymupdf.PDF_REDACT_TEXT_NONE,
                )
            except TypeError:
                pass

        blocks_by_id = {item[0]: item[1] for item in pending}
        block_by_id = {item["Id"]: item for item in page_blocks}
        ocr_boxes = []
        for block_id, _, sources, _, _, _, _, _, _ in pending:
            block = block_by_id.get(block_id)
            if block is None or not block.get("Ocr"):
                continue
            ocr_boxes.extend(sources)
        if ocr_boxes and not erase_ocr_sources(page, ocr_boxes):
            # Fallback for pages whose scan cannot be replaced as a bitmap.
            for source_box in ocr_boxes:
                try:
                    page.draw_rect(source_box, color=None, fill=(1, 1, 1), overlay=True)
                except Exception:
                    pass
        for block_id, bbox, sources, translation, size, color, align, line_height, flags in pending:
            block = block_by_id.get(block_id)
            if block is None:
                continue
            font_path = find_font(
                target, bool(block.get("Serif")), bool(block.get("Monospace")), block.get("Font", "")
            )
            bold_path = style_font(font_path, True, False)
            italic_path = style_font(font_path, False, True)
            bold_italic_path = style_font(font_path, True, True)
            archive_key = (font_path, bold_path, italic_path, bold_italic_path)
            if archive_key not in archives:
                archives[archive_key] = make_archive(font_path, bold_path, italic_path, bold_italic_path)
            archive = archives[archive_key]

            expand_box(block, bbox, page, page_blocks, direction, align)
            blocks_by_id[block_id] = bbox
            html = render_translation_html(block, translation, size, color)

            fitted = False
            # Shrink less aggressively than before. The old loop went all the
            # way to 5 pt; a resume ended up technically fitted but unreadable.
            for scale in (1.0, 0.94, 0.88, 0.82, 0.78):
                current_size = max(5.0, size * scale)
                current_line_height = max(
                    current_size * 1.10, line_height * (current_size / max(size, 0.01))
                )
                css = build_css(
                    font_name, current_size, color, current_line_height, align,
                    bool(flags & 16), bool(flags & 2), direction,
                )
                before_contents = page.get_contents()
                try:
                    spare, _ = page.insert_htmlbox(
                        bbox, html, css=css, archive=archive, scale_low=1.0
                    )
                except Exception as exc:
                    failed.append("%s: %s" % (block_id, exc))
                    break
                if spare >= 0:
                    fitted = True
                    inserted += 1
                    if not _wrap_actual_text(doc, page, before_contents, translation):
                        failed.append("%s: copy text was not marked" % block_id)
                    break

            if not fitted:
                # Last resort: put the source passage back rather than leave
                # a hole. This keeps the page complete if a translation is
                # wildly longer than its box.
                source_html = render_translation_html(block, block.get("Text", ""), size, color)
                source_css = build_css(
                    font_name, size, color, line_height, block.get("Align", "left"),
                    bool(flags & 16), bool(flags & 2), direction,
                )
                before_contents = page.get_contents()
                try:
                    spare, _ = page.insert_htmlbox(
                        bbox, source_html, css=source_css, archive=archive, scale_low=0.80
                    )
                except Exception:
                    spare = -1
                if spare < 0:
                    failed.append("%s: translation did not fit; no text inserted" % block_id)
                else:
                    if not _wrap_actual_text(doc, page, before_contents, block.get("Text", "")):
                        failed.append("%s: copy text was not marked" % block_id)
                    failed.append("%s: translation did not fit; original passage kept" % block_id)

        line_boxes = []
        for text_block in page.get_text("dict").get("blocks", []):
            if text_block.get("type") != 0:
                continue
            for line in text_block.get("lines", []):
                line_text = "".join(span.get("text", "") for span in line.get("spans", []))
                if line_text.strip():
                    line_boxes.append((pymupdf.Rect(line["bbox"]), line_text))
        insert_relocated_links(page, link_plan, blocks_by_id, line_boxes)

    if translations:
        try:
            repair_copy_layer(doc, list(translations.values()), target)
        except Exception as exc:
            failed.append("copy layer: %s" % exc)

    tmp_path = output_path + ".part"
    doc.save(tmp_path, garbage=4, deflate=True)
    doc.close()
    os.replace(tmp_path, output_path)
    return inserted, failed


def main(argv):
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8", errors="replace")
        except Exception:
            pass
    if len(argv) < 3:
        fail("usage: pdf_translate.py extract|detect|apply ...")
    command = argv[1].lower()
    if command == "extract":
        if len(argv) != 4:
            fail("usage: pdf_translate.py extract <input.pdf> <blocks.json>")
        input_path, output_path = argv[2], argv[3]
        doc = pymupdf.open(input_path)
        if doc.needs_pass:
            fail("This PDF is password protected. Unlock a copy before translating it.")
        blocks, warnings = extract_blocks(doc)
        payload = {"Pages": doc.page_count, "Blocks": blocks, "Warnings": warnings}
        doc.close()
        with open(output_path, "w", encoding="utf-8") as handle:
            json.dump(payload, handle, ensure_ascii=False, indent=1)
        return 0
    if command == "detect":
        if len(argv) != 4:
            fail("usage: pdf_translate.py detect <input.pdf> <source.json>")
        input_path, output_path = argv[2], argv[3]
        doc = pymupdf.open(input_path)
        source, sample = detect_source(doc)
        doc.close()
        with open(output_path, "w", encoding="utf-8") as handle:
            json.dump({"Source": source, "Sample": sample}, handle, ensure_ascii=False)
        return 0
    if command == "apply":
        if len(argv) not in (5, 6):
            fail("usage: pdf_translate.py apply <input.pdf> <translations.json> <output.pdf> [blocks.json]")
        blocks_path = argv[5] if len(argv) == 6 else None
        inserted, failed = apply_translations(argv[2], argv[3], argv[4], blocks_path)
        print(json.dumps({"Inserted": inserted, "Failed": failed}, ensure_ascii=False))
        return 0
    fail("unknown command: " + command)


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
