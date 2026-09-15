#!/usr/bin/env python3
"""Compose three restrained room identities from the Primary Match resource kit."""

from __future__ import annotations

import json
import textwrap
from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter


ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / "art-production/test-room-v01/working/primary-match-v2"
LAYOUT = WORK / "curated-room-variants.json"
OUT = WORK / "diorama-process-11-room-identity-triptych.png"


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


def place(canvas: Image.Image, item: Image.Image, x: int, y: int, width: int, layer: str) -> None:
    height = max(1, round(item.height * width / item.width))
    item = item.resize((width, height), Image.Resampling.LANCZOS)
    alpha = item.getchannel("A")
    blur = 15 if layer in {"background", "midground"} else 20 if layer == "foreground" else 8
    strength = 145 if layer in {"background", "midground"} else 175 if layer == "foreground" else 105
    shadow = Image.new("RGBA", item.size, (25, 5, 34, 0))
    shadow.putalpha(alpha.point(lambda a: a * strength // 255).filter(ImageFilter.GaussianBlur(blur)))
    canvas.alpha_composite(shadow, (x + 6, y + 10))
    canvas.alpha_composite(item, (x, y))


def room_base(size: tuple[int, int], accent: str) -> Image.Image:
    floor = Image.open(WORK / "tr01_primarymatch_floor_macro_3x3_source.png").convert("RGB")
    front = Image.open(WORK / "tr01_primarymatch_wall_front_macro_3x3_source.png").convert("RGB")
    top = Image.open(WORK / "tr01_primarymatch_wall_top_macro_3x3_source.png").convert("RGB")
    canvas = cover(floor, size).convert("RGBA")
    canvas.alpha_composite(cover(front, (size[0], 205)).convert("RGBA"), (0, 0))
    canvas.alpha_composite(cover(top, (size[0], 105)).convert("RGBA"), (0, 0))

    colors = {
        "amber_magenta": ((255, 122, 28), (221, 35, 185)),
        "cyan": ((33, 210, 230), (71, 125, 220)),
        "magenta_cyan": ((233, 34, 205), (35, 202, 228)),
    }[accent]
    for color, box, alpha in ((colors[0], (35, 160, 500, 760), 48), (colors[1], (260, 240, 650, 850), 32)):
        glow = Image.new("RGBA", size, (0, 0, 0, 0))
        gd = ImageDraw.Draw(glow)
        gd.ellipse(box, fill=(*color, alpha))
        canvas.alpha_composite(glow.filter(ImageFilter.GaussianBlur(65)))
    return canvas


def main() -> None:
    layout = json.loads(LAYOUT.read_text(encoding="utf-8"))
    canvas = Image.new("RGB", (1920, 1080), (13, 10, 19))
    draw = ImageDraw.Draw(canvas)
    draw.text((42, 22), "PROCESS 11  /  THREE ROOM IDENTITIES FROM ONE BOLD MODULAR KIT", fill=(237, 221, 245))
    draw.text((42, 46), "one hero machine + one monumental backdrop + one foreground edge per room", fill=(153, 139, 169))
    cache: dict[str, Image.Image] = {}
    panel_size = (600, 880)
    for index, room in enumerate(layout["rooms"]):
        panel = room_base(panel_size, room["accent"])
        for entry in room["placements"]:
            source = entry["source"]
            if source not in cache:
                cache[source] = Image.open(WORK / source).convert("RGBA")
            cols, rows = entry["grid"]
            col, row = entry["cell"]
            x, y, width = entry["screen"]
            place(panel, cell(cache[source], col, row, cols, rows), x, y, width, entry["layer"])
        panel = ImageEnhance.Contrast(panel.convert("RGB")).enhance(1.025)
        panel = ImageEnhance.Color(panel).enhance(1.035)
        x0 = 30 + index * 630
        canvas.paste(panel, (x0, 92))
        draw.rectangle((x0, 92, x0 + panel_size[0], 92 + panel_size[1]), outline=(85, 69, 100), width=2)
        draw.text((x0 + 18, 990), room["title"], fill=(230, 214, 239))
        story_lines = textwrap.wrap(room["story"], width=52)[:2]
        for line_index, line in enumerate(story_lines):
            draw.text((x0 + 18, 1012 + line_index * 18), line, fill=(139, 126, 153))
    canvas.save(OUT, quality=95)
    print(OUT)


if __name__ == "__main__":
    main()
