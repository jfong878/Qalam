#!/usr/bin/env python3
"""Generate Qalam's Qaaf mark and Windows icon.

The mark is a rounded tile with the Arabic letter Qaaf (ق). Variants are
rendered first so the small-size legibility can be judged before choosing one.
"""

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
PREVIEW = ROOT / "preview"
INK = (14, 18, 24, 255)
INK_LIGHT = (27, 34, 46, 255)
GOLD = (231, 194, 125, 255)
GOLD_LIGHT = (244, 214, 158, 255)
GOLD_DARK = (185, 146, 82, 255)

FONT_PATHS = {
    "tahoma": Path(r"C:\Windows\Fonts\tahomabd.ttf"),
    "segoe": Path(r"C:\Windows\Fonts\segoeuib.ttf"),
    "arial": Path(r"C:\Windows\Fonts\arialbd.ttf"),
}


def gradient(size, top, bottom):
    image = Image.new("RGBA", (size, size), top)
    draw = ImageDraw.Draw(image)
    for y in range(size):
        t = y / max(1, size - 1)
        color = tuple(int(top[i] * (1 - t) + bottom[i] * t) for i in range(4))
        draw.line([(0, y), (size, y)], fill=color)
    return image


def tile(size, fill, radius, border=None, border_width=0):
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).rounded_rectangle(
        [0, 0, size - 1, size - 1], radius=radius, fill=255
    )
    if isinstance(fill, Image.Image):
        image.paste(fill, (0, 0), mask)
    else:
        ImageDraw.Draw(image).rounded_rectangle(
            [0, 0, size - 1, size - 1], radius=radius, fill=fill
        )
    if border is not None and border_width > 0:
        ImageDraw.Draw(image).rounded_rectangle(
            [0, 0, size - 1, size - 1],
            radius=radius,
            outline=border,
            width=border_width,
        )
    return image


def draw_letter(image, text, font_path, font_size, color, y_offset=0):
    draw = ImageDraw.Draw(image)
    font = ImageFont.truetype(str(font_path), font_size)
    draw.text(
        (image.width // 2, image.height // 2 + y_offset),
        text,
        font=font,
        fill=color,
        anchor="mm",
    )
    return image


def compose(name, background, letter_color, border, border_width, font_key, font_size, circle=False):
    size = 1024
    if circle:
        canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        mask = Image.new("L", (size, size), 0)
        ImageDraw.Draw(mask).ellipse([0, 0, size - 1, size - 1], fill=255)
        if isinstance(background, Image.Image):
            canvas.paste(background, (0, 0), mask)
        else:
            ImageDraw.Draw(canvas).ellipse([0, 0, size - 1, size - 1], fill=background)
        if border is not None and border_width > 0:
            ImageDraw.Draw(canvas).ellipse(
                [0, 0, size - 1, size - 1], outline=border, width=border_width
            )
    else:
        canvas = tile(size, background, 220, border, border_width)
    draw_letter(canvas, "\u0642", FONT_PATHS[font_key], font_size, letter_color)
    canvas.save(PREVIEW / f"{name}.png")
    for small in (64, 32, 24, 16):
        canvas.resize((small, small), Image.Resampling.LANCZOS).save(PREVIEW / f"{name}-{small}.png")


def main():
    PREVIEW.mkdir(parents=True, exist_ok=True)

    ink_gradient = gradient(1024, INK_LIGHT, INK)
    gold_gradient = gradient(1024, GOLD_LIGHT, GOLD_DARK)

    # Ink tile, gold letter.
    compose("ink-tile", ink_gradient, GOLD_LIGHT, GOLD, 38, "tahoma", 610)
    compose("ink-tile-segoe", ink_gradient, GOLD_LIGHT, GOLD, 38, "segoe", 650)
    compose("ink-tile-noborder", ink_gradient, GOLD_LIGHT, None, 0, "tahoma", 630)
    compose("ink-circle", ink_gradient, GOLD_LIGHT, GOLD, 38, "tahoma", 560, circle=True)

    # Gold tile, ink letter.
    compose("gold-tile", gold_gradient, INK, INK, 38, "tahoma", 610)
    compose("gold-tile-segoe", gold_gradient, INK, INK, 38, "segoe", 650)
    compose("gold-circle", gold_gradient, INK, INK, 38, "tahoma", 560, circle=True)

    # Contact sheet: full mark plus actual-size enlargements for inspection.
    names = [
        "ink-tile",
        "ink-tile-segoe",
        "ink-tile-noborder",
        "ink-circle",
        "gold-tile",
        "gold-tile-segoe",
        "gold-circle",
    ]
    sheet = Image.new("RGBA", (7 * 300, 420), (246, 246, 246, 255))
    draw = ImageDraw.Draw(sheet)
    label_font = ImageFont.truetype(str(FONT_PATHS["segoe"]), 18)
    for index, name in enumerate(names):
        x = index * 300 + 20
        full = Image.open(PREVIEW / f"{name}.png").resize((260, 260), Image.Resampling.LANCZOS)
        sheet.alpha_composite(full, (x, 20))
        draw.text((x, 290), name, font=label_font, fill=(20, 20, 20, 255))
        for j, small in enumerate((32, 24, 16)):
            icon = Image.open(PREVIEW / f"{name}-{small}.png").resize((small * 2, small * 2), Image.Resampling.NEAREST)
            sheet.alpha_composite(icon, (x + j * 80, 330))

    sheet.save(PREVIEW / "contact-sheet.png")
    print("Wrote", PREVIEW / "contact-sheet.png")


if __name__ == "__main__":
    main()
