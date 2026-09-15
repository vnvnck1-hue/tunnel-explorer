#!/usr/bin/env python3
"""Compose full 16:9 room-identity dioramas without touching Unity or Assets/."""

from __future__ import annotations

import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter


ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / "art-production/test-room-v01/working/primary-match-v2"
LAYOUT = WORK / "curated-room-landscape-layouts.json"


def cell(sheet: Image.Image, col: int, row: int, cols: int, rows: int) -> Image.Image:
    width, height = sheet.width // cols, sheet.height // rows
    item = sheet.crop((col * width, row * height, (col + 1) * width, (row + 1) * height))
    bounds = item.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError(f"empty cell {col},{row}")
    return item.crop(bounds)


def cover(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    scale = max(size[0] / image.width, size[1] / image.height)
    resized = image.resize((round(image.width * scale), round(image.height * scale)), Image.Resampling.LANCZOS)
    left, top = (resized.width - size[0]) // 2, (resized.height - size[1]) // 2
    return resized.crop((left, top, left + size[0], top + size[1]))


def glow(canvas: Image.Image, box: tuple[int, int, int, int], color: tuple[int, int, int], alpha: int) -> None:
    layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    ImageDraw.Draw(layer).ellipse(box, fill=(*color, alpha))
    canvas.alpha_composite(layer.filter(ImageFilter.GaussianBlur(max(28, (box[2] - box[0]) // 6))))


def room_base(accent: str) -> Image.Image:
    floor = Image.open(WORK / "tr01_primarymatch_floor_macro_3x3_source.png").convert("RGB")
    wall_front = Image.open(WORK / "tr01_primarymatch_wall_front_macro_3x3_source.png").convert("RGB")
    wall_top = Image.open(WORK / "tr01_primarymatch_wall_top_macro_3x3_source.png").convert("RGB")
    canvas = cover(floor, (1920, 1080)).convert("RGBA")
    canvas.alpha_composite(cover(wall_front, (1920, 260)).convert("RGBA"), (0, 0))
    canvas.alpha_composite(cover(wall_top, (1920, 135)).convert("RGBA"), (0, 0))

    boundary = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    ImageDraw.Draw(boundary).rectangle((0, 205, 1920, 325), fill=(18, 3, 29, 145))
    canvas.alpha_composite(boundary.filter(ImageFilter.GaussianBlur(36)))

    accents = {
        "amber_magenta": ((255, 125, 28), (224, 36, 190)),
        "cyan": ((32, 215, 235), (64, 125, 224)),
        "magenta_cyan": ((237, 34, 211), (31, 205, 230)),
    }[accent]
    glow(canvas, (90, 180, 920, 980), accents[0], 38)
    glow(canvas, (900, 130, 1860, 930), accents[1], 30)

    vignette = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    ImageDraw.Draw(vignette).rectangle((0, 0, 1920, 1080), outline=(9, 2, 16, 155), width=100)
    canvas.alpha_composite(vignette.filter(ImageFilter.GaussianBlur(70)))
    return canvas


def place(canvas: Image.Image, item: Image.Image, x: int, y: int, width: int, layer: str) -> None:
    height = max(1, round(item.height * width / item.width))
    item = item.resize((width, height), Image.Resampling.LANCZOS)
    blur = {"ground": 9, "background": 18, "midground": 15, "foreground": 23}[layer]
    strength = {"ground": 105, "background": 155, "midground": 150, "foreground": 185}[layer]
    alpha = item.getchannel("A")
    shadow = Image.new("RGBA", item.size, (24, 4, 35, 0))
    shadow.putalpha(alpha.point(lambda a: a * strength // 255).filter(ImageFilter.GaussianBlur(blur)))
    canvas.alpha_composite(shadow, (x + 8, y + 13))
    canvas.alpha_composite(item, (x, y))


def main() -> None:
    contract = json.loads(LAYOUT.read_text(encoding="utf-8"))
    cache: dict[str, Image.Image] = {}
    for room in contract["rooms"]:
        canvas = room_base(room["accent"])
        for entry in room["placements"]:
            source_name = entry["source"]
            if source_name not in cache:
                cache[source_name] = Image.open(WORK / source_name).convert("RGBA")
            cols, rows = entry["grid"]
            col, row = entry["cell"]
            place(canvas, cell(cache[source_name], col, row, cols, rows), *entry["screen"], entry["layer"])
        canvas = ImageEnhance.Color(canvas.convert("RGB")).enhance(1.035)
        canvas = ImageEnhance.Contrast(canvas).enhance(1.025)
        output = WORK / room["compositionImage"]
        canvas.save(output, quality=95)
        print(output)


if __name__ == "__main__":
    main()
