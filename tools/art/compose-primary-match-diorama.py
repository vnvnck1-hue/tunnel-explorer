#!/usr/bin/env python3
"""Build review-only diorama composites without touching Unity or Assets/.

The result is deliberately labelled by filename as a process image. It previews scale,
silhouette and density; it is not evidence of runtime sorting, lighting or placement.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter


ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / "art-production/test-room-v01/working/primary-match-v2"
BASE = WORK / "qa-primary-match-v2-pass25-lighting.png"
CURATED_LAYOUT = WORK / "curated-diorama-layout.json"


def cell(sheet: Image.Image, col: int, row: int, cols: int = 3, rows: int = 3) -> Image.Image:
    cell_width = sheet.width // cols
    cell_height = sheet.height // rows
    item = sheet.crop((col * cell_width, row * cell_height,
                       (col + 1) * cell_width, (row + 1) * cell_height))
    alpha = item.getchannel("A")
    bounds = alpha.getbbox()
    if bounds is None:
        raise ValueError(f"empty cell {col},{row}")
    return item.crop(bounds)


def place(canvas: Image.Image, item: Image.Image, x: int, y: int, width: int,
          shadow_offset: tuple[int, int] = (8, 12), shadow_blur: int = 10,
          shadow_alpha: int = 125) -> None:
    height = max(1, round(item.height * width / item.width))
    item = item.resize((width, height), Image.Resampling.LANCZOS)
    alpha = item.getchannel("A")
    shadow = Image.new("RGBA", item.size, (28, 7, 38, 0))
    shadow.putalpha(alpha.point(lambda a: a * shadow_alpha // 255).filter(ImageFilter.GaussianBlur(shadow_blur)))
    canvas.alpha_composite(shadow, (x + shadow_offset[0], y + shadow_offset[1]))
    canvas.alpha_composite(item, (x, y))


def cover(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    scale = max(size[0] / image.width, size[1] / image.height)
    resized = image.resize((round(image.width * scale), round(image.height * scale)), Image.Resampling.LANCZOS)
    left = (resized.width - size[0]) // 2
    top = (resized.height - size[1]) // 2
    return resized.crop((left, top, left + size[0], top + size[1]))


def glow(canvas: Image.Image, box: tuple[int, int, int, int], color: tuple[int, int, int], alpha: int) -> None:
    layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)
    draw.ellipse(box, fill=(*color, alpha))
    radius = max(20, (box[2] - box[0]) // 5)
    layer = layer.filter(ImageFilter.GaussianBlur(radius))
    canvas.alpha_composite(layer)


def clean_base() -> Image.Image:
    floor = Image.open(WORK / "tr01_primarymatch_floor_macro_3x3_source.png").convert("RGB")
    canvas = cover(floor, (1920, 1080)).convert("RGBA")

    wall_front = Image.open(WORK / "tr01_primarymatch_wall_front_macro_3x3_source.png").convert("RGB")
    wall_top = Image.open(WORK / "tr01_primarymatch_wall_top_macro_3x3_source.png").convert("RGB")
    canvas.alpha_composite(cover(wall_front, (1920, 260)).convert("RGBA"), (0, 0))
    canvas.alpha_composite(cover(wall_top, (1920, 135)).convert("RGBA"), (0, 0))

    # A soft boundary shadow separates the raised back wall from the playable floor.
    boundary = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(boundary)
    draw.rectangle((0, 210, 1920, 320), fill=(20, 4, 31, 135))
    boundary = boundary.filter(ImageFilter.GaussianBlur(34))
    canvas.alpha_composite(boundary)

    # Restrained light pools establish three depth zones without baking lights into assets.
    glow(canvas, (190, 210, 890, 900), (209, 37, 183), 44)
    glow(canvas, (850, 150, 1590, 800), (29, 203, 226), 34)
    glow(canvas, (1050, 520, 1900, 1120), (255, 119, 29), 26)

    vignette = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    vd = ImageDraw.Draw(vignette)
    vd.rectangle((0, 0, 1920, 1080), outline=(10, 2, 17, 160), width=100)
    vignette = vignette.filter(ImageFilter.GaussianBlur(70))
    canvas.alpha_composite(vignette)
    return canvas


def stage_one(canvas: Image.Image) -> None:
    rail = Image.open(WORK / "tr01_primarymatch_floor_rail_transitions_3x3_source.png").convert("RGBA")
    crystal = Image.open(WORK / "tr01_primarymatch_crystal_outcrops_3x3_source.png").convert("RGBA")
    transition = Image.open(WORK / "tr01_primarymatch_equipment_transitions_3x3_source.png").convert("RGBA")

    # One readable rail route across the open right-hand work floor.
    place(canvas, cell(rail, 0, 0), 965, 462, 235)
    place(canvas, cell(rail, 1, 0), 1165, 462, 235)
    place(canvas, cell(rail, 2, 0), 1365, 462, 235)
    place(canvas, cell(rail, 2, 1), 1510, 515, 245)

    # Large anchors stay near boundaries, preserving the central combat lane.
    place(canvas, cell(crystal, 0, 1), 70, 335, 300, shadow_blur=14)
    place(canvas, cell(crystal, 1, 1), 1530, 650, 300, shadow_blur=15)
    place(canvas, cell(crystal, 1, 2), 770, 770, 250, shadow_blur=12)

    # Transition sockets explain where old machinery disappears into the geology.
    place(canvas, cell(transition, 1, 0), 390, 180, 300)
    place(canvas, cell(transition, 0, 2), 80, 690, 270)
    place(canvas, cell(transition, 2, 2), 1640, 310, 245)


def stage_two(canvas: Image.Image) -> None:
    stage_one(canvas)
    hero = Image.open(WORK / "tr01_primarymatch_hero_machinery_2x2_source.png").convert("RGBA")

    # Oversized functional silhouettes create the diorama's four visual anchors.
    place(canvas, cell(hero, 0, 0, 2, 2), 20, 105, 430, shadow_blur=17, shadow_alpha=155)
    place(canvas, cell(hero, 1, 0, 2, 2), 1510, 125, 380, shadow_blur=17, shadow_alpha=150)
    place(canvas, cell(hero, 0, 1, 2, 2), 1430, 650, 400, shadow_blur=18, shadow_alpha=160)
    place(canvas, cell(hero, 1, 1, 2, 2), 1030, 555, 390, shadow_blur=16, shadow_alpha=150)


def stage_three(canvas: Image.Image) -> None:
    stage_two(canvas)
    depth = Image.open(WORK / "tr01_primarymatch_foreground_depth_2x2_source.png").convert("RGBA")

    # These pieces intentionally render last: their overlap is the depth hypothesis.
    place(canvas, cell(depth, 0, 0, 2, 2), -45, 470, 560, shadow_blur=20, shadow_alpha=175)
    place(canvas, cell(depth, 1, 0, 2, 2), 1435, 430, 535, shadow_blur=20, shadow_alpha=175)
    place(canvas, cell(depth, 0, 1, 2, 2), 455, 760, 690, shadow_blur=22, shadow_alpha=185)
    place(canvas, cell(depth, 1, 1, 2, 2), 1055, 695, 460, shadow_blur=18, shadow_alpha=165)


def backdrop_layer(canvas: Image.Image) -> None:
    backdrop = Image.open(WORK / "tr01_primarymatch_monumental_wall_modules_2x2_source.png").convert("RGBA")
    place(canvas, cell(backdrop, 0, 1, 2, 2), 15, 95, 385, shadow_blur=16, shadow_alpha=145)
    place(canvas, cell(backdrop, 0, 0, 2, 2), 500, 75, 410, shadow_blur=18, shadow_alpha=155)
    place(canvas, cell(backdrop, 1, 0, 2, 2), 945, 75, 400, shadow_blur=18, shadow_alpha=155)
    place(canvas, cell(backdrop, 1, 1, 2, 2), 1450, 90, 405, shadow_blur=18, shadow_alpha=160)


def stage_four(canvas: Image.Image) -> None:
    backdrop_layer(canvas)
    stage_three(canvas)


def stage_five() -> Image.Image:
    canvas = clean_base()
    layout = json.loads(CURATED_LAYOUT.read_text(encoding="utf-8"))
    cache: dict[str, Image.Image] = {}
    for entry in layout["placements"]:
        source_name = entry["source"]
        if source_name not in cache:
            source_path = ROOT / source_name if source_name.startswith("unity/") else WORK / source_name
            cache[source_name] = Image.open(source_path).convert("RGBA")
        source = cache[source_name]
        cols, rows = entry["grid"]
        col, row = entry["cell"]
        item = cell(source, col, row, cols, rows)
        x, y, width = entry["screen"]
        place(canvas, item, x, y, width,
              shadow_blur=entry["shadowBlur"], shadow_alpha=entry["shadowAlpha"])
    return canvas


def accent_bloom(canvas: Image.Image, color: tuple[int, int, int], selector: str) -> None:
    rgb = canvas.convert("RGB")
    pixels = rgb.load()
    mask = Image.new("L", rgb.size, 0)
    selected = mask.load()
    for y in range(rgb.height):
        for x in range(rgb.width):
            r, g, b = pixels[x, y]
            value = max(r, g, b)
            if selector == "magenta":
                amount = min(r - g, b - g) if value >= 135 else 0
            elif selector == "cyan":
                amount = min(g - r, b - r) if value >= 135 else 0
            else:
                amount = min(r - b, g - b) if value >= 145 else 0
            selected[x, y] = max(0, min(255, (amount - 35) * 3))

    for radius, strength in ((16, 92), (48, 45)):
        blurred = mask.filter(ImageFilter.GaussianBlur(radius))
        blurred = blurred.point(lambda a: a * strength // 255)
        layer = Image.new("RGBA", canvas.size, (*color, 0))
        layer.putalpha(blurred)
        canvas.alpha_composite(layer)


def stage_six() -> Image.Image:
    canvas = stage_five()
    accent_bloom(canvas, (235, 35, 211), "magenta")
    accent_bloom(canvas, (35, 210, 235), "cyan")
    accent_bloom(canvas, (255, 132, 34), "amber")
    rgb = ImageEnhance.Color(canvas.convert("RGB")).enhance(1.04)
    rgb = ImageEnhance.Contrast(rgb).enhance(1.035)
    return rgb.convert("RGBA")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--stage", type=int, default=1, choices=(1, 2, 3, 4, 5, 6))
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()

    if args.stage == 5:
        canvas = stage_five()
    elif args.stage == 6:
        canvas = stage_six()
    else:
        canvas = Image.open(BASE).convert("RGBA")
        # Pull back the captured frame slightly so candidate art can be judged without
        # pretending it already receives the exact runtime light stack.
        rgb = ImageEnhance.Contrast(canvas.convert("RGB")).enhance(0.94)
        canvas = Image.merge("RGBA", (*rgb.split(), canvas.getchannel("A")))
        if args.stage == 1:
            stage_one(canvas)
        elif args.stage == 2:
            stage_two(canvas)
        elif args.stage == 3:
            stage_three(canvas)
        else:
            stage_four(canvas)

    default_outputs = {
        1: "diorama-process-01-rail-crystal-transitions.png",
        2: "diorama-process-02-bold-hero-machinery.png",
        3: "diorama-process-03-foreground-depth.png",
        4: "diorama-process-04-monumental-wall-backdrop.png",
        5: "diorama-process-05-curated-clean-room.png",
        6: "diorama-process-06-material-lighting-preview.png",
    }
    output = args.output or WORK / default_outputs[args.stage]
    output.parent.mkdir(parents=True, exist_ok=True)
    canvas.convert("RGB").save(output, quality=95)
    print(output)


if __name__ == "__main__":
    main()
