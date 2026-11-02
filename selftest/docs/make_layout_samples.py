"""Builds the two PDF layout fixtures used by the document self-test.

This is a test fixture generator, not part of the installed application.
It exercises a tinted band, a heading, wrapping body text, colors, a
right-aligned note, a hyperlink, an image, a second page with Arabic
source text split across two lines, and a footer.
"""

import os
import sys

import pymupdf


ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DOCS = os.path.join(ROOT, "selftest", "docs")
ASSETS = os.path.join(ROOT, "assets")
WINDOWS = os.environ.get("WINDIR", r"C:\Windows")
ARIAL = os.path.join(WINDOWS, "Fonts", "arial.ttf")


def html_archive():
    archive = pymupdf.Archive()
    with open(ARIAL, "rb") as handle:
        archive.add((handle.read(), "qalam-font.ttf"))
    return archive


def add_arabic(page, rect, text):
    css = (
        "@font-face{font-family:Arabic;src:url(qalam-font.ttf);}"
        "*{font-family:Arabic;font-size:15px;color:#203040;"
        "line-height:17.25px;margin:0;padding:0;}"
        "        div{direction:rtl;}"
    )
    spare, scale = page.insert_htmlbox(
        rect, '<div dir="rtl">%s</div>' % text,
        css=css, archive=html_archive(), scale_low=0.8,
    )
    if spare < 0:
        raise RuntimeError("Arabic fixture text did not fit: %.1f" % spare)


def make_english(path):
    doc = pymupdf.open()
    page = doc.new_page(width=595, height=842)

    page.draw_rect(pymupdf.Rect(40, 40, 555, 105),
                   color=None, fill=(0.965, 0.945, 0.875), overlay=False)
    page.insert_image(pymupdf.Rect(470, 48, 525, 103),
                      filename=os.path.join(ASSETS, "qalam-mark-256.png"))

    page.insert_textbox(
        pymupdf.Rect(60, 48, 450, 126),
        "A longer document heading for layout testing",
        fontsize=20, fontname="hebo", color=(0x1f / 255, 0x33 / 255, 0x4d / 255),
    )
    page.insert_textbox(
        pymupdf.Rect(60, 136, 535, 194),
        "This paragraph is deliberately long so the translated text has to wrap "
        "inside the same block without moving the surrounding page elements.",
        fontsize=11, fontname="helv", color=(0x40 / 255, 0x40 / 255, 0x40 / 255),
    )
    page.insert_textbox(
        pymupdf.Rect(60, 208, 320, 229),
        "Short line in red.",
        fontsize=11, fontname="helv", color=(0x73 / 255, 0x1a / 255, 0x1a / 255),
    )
    page.insert_textbox(
        pymupdf.Rect(315, 236, 535, 257),
        "Right-aligned note",
        fontsize=11, fontname="helv", color=(0x1a / 255, 0x4d / 255, 0x80 / 255),
        align=pymupdf.TEXT_ALIGN_RIGHT,
    )

    link_text = "Open the Qalam project page"
    page.insert_text((60, 294), link_text, fontsize=11, fontname="helv",
                     color=(0x1a / 255, 0x4d / 255, 0x99 / 255))
    width = pymupdf.get_text_length(link_text, fontname="helv", fontsize=11)
    page.insert_link({
        "kind": pymupdf.LINK_URI,
        "from": pymupdf.Rect(60, 281, 60 + width, 297),
        "uri": "https://example.com/qalam",
    })
    page.draw_line(pymupdf.Point(60, 295), pymupdf.Point(60 + width, 295),
                   color=(0x1a / 255, 0x4d / 255, 0x99 / 255), width=0.6)

    page.draw_rect(pymupdf.Rect(48, 211, 56, 229),
                   color=None, fill=(0.9, 0.78, 0.45), overlay=False)
    page.draw_line(pymupdf.Point(60, 775), pymupdf.Point(535, 775),
                   color=(0.75, 0.75, 0.75), width=0.8)
    page.insert_textbox(
        pymupdf.Rect(60, 790, 535, 810),
        "Qalam layout sample - page 1",
        fontsize=9, fontname="helv", color=(0x66 / 255, 0x66 / 255, 0x66 / 255),
    )
    doc.save(path, garbage=4, deflate=True)
    doc.close()


def make_arabic(path):
    doc = pymupdf.open()
    page = doc.new_page(width=595, height=842)

    # Two visual lines, one right-aligned line box each. The bridge must
    # merge them into one paragraph before translating.
    add_arabic(page, pymupdf.Rect(72, 82, 535, 102),
               "\u0643\u062a\u0628 \u0627\u0644\u0637\u0627\u0644\u0628 \u0627\u0644\u062f\u0631\u0633 \u0641\u064a \u0627\u0644\u0645\u062f\u0631\u0633\u0629 \u0647\u0630\u0627 \u0627\u0644\u0635\u0628\u0627\u062d.")
    add_arabic(page, pymupdf.Rect(330, 107, 535, 127),
               "\u0647\u0630\u0647 \u0641\u0642\u0631\u0629 \u0639\u0631\u0628\u064a\u0629 \u0637\u0648\u064a\u0644\u0629 \u0644\u0627\u062e\u062a\u0628\u0627\u0631 \u0627\u0644\u062a\u0631\u062c\u0645\u0629.")

    page.draw_rect(pymupdf.Rect(48, 155, 120, 235),
                   color=None, fill=(0.90, 0.93, 0.96), overlay=False)
    page.draw_rect(pymupdf.Rect(48, 155, 120, 235),
                   color=(0.35, 0.48, 0.62), width=0.8, overlay=True)
    page.insert_textbox(
        pymupdf.Rect(60, 165, 115, 225),
        "COLOR",
        fontsize=9, fontname="helv", color=(0.2, 0.3, 0.4),
    )
    page.insert_textbox(
        pymupdf.Rect(60, 790, 535, 810),
        "Qalam layout sample - page 2",
        fontsize=9, fontname="helv", color=(0x66 / 255, 0x66 / 255, 0x66 / 255),
    )
    doc.save(path, garbage=4, deflate=True)
    doc.close()


def main():
    if not os.path.isfile(ARIAL):
        raise SystemExit("Arial is required to build the Arabic fixture: " + ARIAL)
    os.makedirs(DOCS, exist_ok=True)
    make_english(os.path.join(DOCS, "layout-en.pdf"))
    make_arabic(os.path.join(DOCS, "layout-ar.pdf"))
    print("wrote layout-en.pdf and layout-ar.pdf")


if __name__ == "__main__":
    main()
