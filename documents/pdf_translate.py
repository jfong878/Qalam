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

import json
import os
import statistics
import sys
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
FONT_CANDIDATES = {
    "ar": ["arial.ttf", "segoeui.ttf", "tahoma.ttf"],
    "fa": ["arial.ttf", "segoeui.ttf", "tahoma.ttf"],
    "ur": ["arial.ttf", "segoeui.ttf", "tahoma.ttf"],
    "he": ["arial.ttf", "segoeui.ttf", "tahoma.ttf"],
    "zh-CN": ["msyh.ttc", "msyh.ttf", "simsun.ttc", "arialuni.ttf"],
    "zh-TW": ["msjh.ttc", "mingliu.ttc", "arialuni.ttf"],
    "ja": ["YuGothM.ttc", "msgothic.ttc", "meiryo.ttc", "arialuni.ttf"],
    "ko": ["malgun.ttf", "arialuni.ttf"],
    "hi": ["Nirmala.ttf", "mangal.ttf", "arialuni.ttf"],
    "th": ["leelawui.ttf", "tahoma.ttf", "arialuni.ttf"],
}
DEFAULT_FONT = "arial.ttf"


def fail(message, code=2):
    sys.stderr.write(message.rstrip() + "\n")
    raise SystemExit(code)


def normalize_text(text):
    # PDF text often contains Arabic presentation forms (U+FE70..U+FEFF).
    # Translation services expect the normal letters, and Word/PDF readers
    # render both, so normalize once at extraction.
    text = unicodedata.normalize("NFKC", text)
    text = text.replace("\u00ad", "")
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
        size = min(float(previous.get("Size", 11)), float(block.get("Size", 11)))
        gap = block["Bbox"][1] - previous["Bbox"][3]
        same_style = (
            previous.get("Page") == block.get("Page")
            and abs(float(previous.get("Size", 0)) - float(block.get("Size", 0))) <= 0.75
            and previous.get("Color") == block.get("Color")
            and previous.get("Font") == block.get("Font")
            and bool(previous.get("Bold")) == bool(block.get("Bold"))
            and bool(previous.get("Italic")) == bool(block.get("Italic"))
            and bool(previous.get("Rtl")) == bool(block.get("Rtl"))
        )
        if not same_style or gap < -1.5 or gap > max(6.0, size * 0.80):
            merged.append(block)
            continue

        a = previous["Bbox"]
        b = block["Bbox"]
        width_a = max(1.0, a[2] - a[0])
        width_b = max(1.0, b[2] - b[0])
        overlap = min(a[2], b[2]) - max(a[0], b[0])
        if overlap < min(width_a, width_b) * 0.45:
            merged.append(block)
            continue

        left_edge = abs(a[0] - b[0]) <= 3.0
        right_edge = abs(a[2] - b[2]) <= 3.0
        centered = abs((a[0] + a[2]) - (b[0] + b[2])) <= 4.0
        if not (left_edge or right_edge or centered):
            merged.append(block)
            continue

        source_boxes = previous.setdefault("SourceBboxes", [list(previous["Bbox"])])
        source_boxes.extend(block.get("SourceBboxes", [block["Bbox"]]))
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


def extract_blocks(doc):
    blocks = []
    warnings = []
    for page_index, page in enumerate(doc):
        page_width = page.rect.width
        page_blocks = page.get_text("dict").get("blocks", [])
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
            span_sizes = []
            span_weights = []
            span_flags = []
            span_colors = []
            for line in lines:
                text = "".join(span.get("text", "") for span in line.get("spans", []))
                if text.strip():
                    line_texts.append(text.strip())
                for span in line.get("spans", []):
                    value = span.get("text", "")
                    if not value:
                        continue
                    span_sizes.append(float(span.get("size", 0)))
                    span_weights.append(max(1, len(value)))
                    span_flags.append(int(span.get("flags", 0)))
                    span_colors.append(span.get("color", 0))
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

            blocks.append({
                "Id": "p%db%d" % (page_index, block_index),
                "Page": page_index,
                "Bbox": [round(float(v), 3) for v in block["bbox"]],
                "Text": text,
                "Size": round(size, 2),
                "Color": hex_color(color),
                "Font": (lines[0].get("spans") or [{}])[0].get("font", ""),
                "Bold": bool(flags & 16),
                "Italic": bool(flags & 2),
                "Serif": bool(flags & 4),
                "Monospace": bool(flags & 8),
                "Rtl": source_rtl,
                "PageWidth": round(page_width, 2),
                "Align": block_alignment(block["bbox"], page_width, source_rtl),
                "LineHeight": round(line_height, 2),
                "SourceBboxes": [[round(float(v), 3) for v in block["bbox"]]],
            })
        if text_on_page == 0:
            warnings.append("page %d: no selectable text" % (page_index + 1))
    return merge_blocks(blocks), warnings


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
    # A single Arabic quotation or name does not make an English document
    # Arabic; compare the two scripts instead of using the first match.
    return ("ar" if arabic > latin else "en"), text[:2000]


def find_font(target, serif=False, monospace=False):
    windows = os.environ.get("WINDIR", r"C:\Windows")
    fonts = os.path.join(windows, "Fonts")
    candidates = list(FONT_CANDIDATES.get(target, [DEFAULT_FONT]))
    # Keep a serif or monospaced source looking like itself on the Latin
    # side. RTL and CJK targets keep their script font first.
    if target not in RTL_TARGETS and target not in ("zh-CN", "zh-TW", "ja", "ko", "hi", "th"):
        if monospace:
            candidates = ["consola.ttf", "cour.ttf"] + candidates
        elif serif:
            candidates = ["times.ttf", "georgia.ttf"] + candidates
    candidates.append(DEFAULT_FONT)
    for name in candidates:
        path = os.path.join(fonts, name)
        if os.path.isfile(path):
            return path
    return None


def make_archive(font_path):
    archive = pymupdf.Archive()
    if font_path:
        with open(font_path, "rb") as handle:
            archive.add((handle.read(), "qalam-font.ttf"))
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
    return (
        "@font-face {font-family:%s; src:url(qalam-font.ttf);}"
        "* {font-family:%s, Arial, sans-serif; font-size:%.2fpx;"
        " color:#%s; line-height:%.2fpx; margin:0; padding:0;"
        " font-weight:%s; font-style:%s;}"
        "div {direction:%s;%s}"
    ) % (
        font_name, font_name, size, color, line_height,
        "bold" if bold else "normal", "italic" if italic else "normal",
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


def apply_translations(input_path, translations_path, output_path):
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

    merged_blocks = extract_blocks(doc)[0]
    by_page = {}
    for block in merged_blocks:
        by_page.setdefault(int(block.get("Page", 0)), []).append(block)

    for page_index, page in enumerate(doc):
        page_blocks = by_page.get(page_index, [])
        pending = []
        for block in page_blocks:
            block_id = block["Id"]
            translation = translations.get(block_id, "")
            if not translation:
                continue

            font_path = find_font(
                target, bool(block.get("Serif")), bool(block.get("Monospace"))
            )
            if font_path not in archives:
                archives[font_path] = make_archive(font_path)
            archive = archives[font_path]

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

        for _, _, sources, _, _, _, _, _, _ in pending:
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
        for block_id, bbox, sources, translation, size, color, align, line_height, flags in pending:
            escaped = (
                translation.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
            )
            html = '<div dir="%s">%s</div>' % (direction, escaped)

            fitted = False
            current_size = size
            for attempt in range(6):
                # Shrink the line box with the glyphs. Keeping the original
                # line-height while reducing the font size buys no extra
                # lines and used to make every retry fail the same way.
                current_line_height = max(
                    current_size * 1.05, line_height * (current_size / max(size, 0.01))
                )
                css = build_css(
                    font_name, current_size, color, current_line_height, align,
                    bool(flags & 16), bool(flags & 2), direction,
                )
                try:
                    spare, scale = page.insert_htmlbox(
                        bbox, html, css=css, archive=archive, scale_low=0.75
                    )
                except Exception as exc:
                    failed.append("%s: %s" % (block_id, exc))
                    break
                if spare >= 0:
                    fitted = True
                    inserted += 1
                    break
                current_size = max(5.0, current_size * 0.85)
            if not fitted:
                # Never punch a hole in the page. Put the original passage
                # back when a much longer translation cannot fit its box.
                source_html = '<div dir="%s">%s</div>' % (
                    "rtl" if block.get("Rtl") else "ltr",
                    str(block.get("Text", "")).replace("&", "&amp;")
                        .replace("<", "&lt;").replace(">", "&gt;"),
                )
                source_css = build_css(
                    font_name, size, color, line_height, block.get("Align", "left"),
                    bool(flags & 16), bool(flags & 2), direction,
                )
                try:
                    spare, _ = page.insert_htmlbox(
                        bbox, source_html, css=source_css, archive=archive, scale_low=0.45
                    )
                except Exception:
                    spare = -1
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
        if len(argv) != 5:
            fail("usage: pdf_translate.py apply <input.pdf> <translations.json> <output.pdf>")
        inserted, failed = apply_translations(argv[2], argv[3], argv[4])
        print(json.dumps({"Inserted": inserted, "Failed": failed}, ensure_ascii=False))
        return 0
    fail("unknown command: " + command)


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
