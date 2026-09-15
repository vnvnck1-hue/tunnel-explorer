#!/usr/bin/env python3
"""Build a deterministic primary-reference vs room-diorama comparison board."""

from __future__ import annotations

import colorsys
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / "art-production/test-room-v01/working/primary-match-v2"
REFERENCE = ROOT / "art-production/test-room-v01/reference/tr01_primary_style_target.png"
LAYOUT = WORK / "curated-room-landscape-layouts.json"
OUT_IMAGE = WORK / "diorama-process-19-primary-reference-comparison.png"
OUT_DATA = WORK / "reference-composition-comparison.json"


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    face = "C:/Windows/Fonts/seguisb.ttf" if bold else "C:/Windows/Fonts/segoeui.ttf"
    try:
        return ImageFont.truetype(face, size)
    except OSError:
        return ImageFont.load_default()


def cover(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    scale = max(size[0] / image.width, size[1] / image.height)
    resized = image.resize((round(image.width * scale), round(image.height * scale)), Image.Resampling.LANCZOS)
    left = (resized.width - size[0]) // 2
    top = (resized.height - size[1]) // 2
    return resized.crop((left, top, left + size[0], top + size[1]))


def central_crop(image: Image.Image, fraction: float = 0.8) -> Image.Image:
    width, height = image.size
    crop_width, crop_height = round(width * fraction), round(height * fraction)
    left, top = (width - crop_width) // 2, (height - crop_height) // 2
    return image.crop((left, top, left + crop_width, top + crop_height))


def metrics(image: Image.Image) -> dict[str, float]:
    sample = central_crop(image.convert("RGB")).resize((320, 180), Image.Resampling.BILINEAR)
    lumas: list[float] = []
    saturations: list[float] = []
    accent = {"magenta": 0, "cyan": 0, "amber": 0}
    for red, green, blue in sample.get_flattened_data():
        r, g, b = red / 255.0, green / 255.0, blue / 255.0
        lumas.append(0.2126 * r + 0.7152 * g + 0.0722 * b)
        hue, saturation, value = colorsys.rgb_to_hsv(r, g, b)
        saturations.append(saturation)
        degrees = hue * 360.0
        if saturation >= 0.45 and value >= 0.15:
            if 285 <= degrees <= 345:
                accent["magenta"] += 1
            elif 165 <= degrees <= 205:
                accent["cyan"] += 1
            elif 20 <= degrees <= 55:
                accent["amber"] += 1
    lumas.sort()
    saturations.sort()
    count = len(lumas)
    return {
        "darkUnder008Percent": round(sum(value < 0.08 for value in lumas) * 100 / count, 2),
        "mid008To035Percent": round(sum(0.08 <= value < 0.35 for value in lumas) * 100 / count, 2),
        "lumaMedian": round(lumas[count // 2], 4),
        "saturationMedian": round(saturations[count // 2], 4),
        "magentaAccentPercent": round(accent["magenta"] * 100 / count, 2),
        "cyanAccentPercent": round(accent["cyan"] * 100 / count, 2),
        "amberAccentPercent": round(accent["amber"] * 100 / count, 2),
    }


def main() -> None:
    layout = json.loads(LAYOUT.read_text(encoding="utf-8"))
    entries = [{
        "id": "primary_reference",
        "title": "PRIMARY STYLE TARGET",
        "path": str(REFERENCE.relative_to(ROOT)).replace("\\", "/"),
        "image": REFERENCE,
        "note": "target: layered rock + machinery + combat readability",
    }]
    notes = {
        "ore_intake": "bold crusher + connected rail; open combat center",
        "ventilation_service": "single circular landmark; strong vertical depth",
        "crystal_power": "split power axis; magenta/cyan focal hierarchy",
    }
    for room in layout["rooms"]:
        entries.append({
            "id": room["id"],
            "title": room["title"].upper(),
            "path": str((WORK / room["compositionImage"]).relative_to(ROOT)).replace("\\", "/"),
            "image": WORK / room["compositionImage"],
            "note": notes[room["id"]],
        })

    for entry in entries:
        entry["metrics"] = metrics(Image.open(entry["image"]))
    target_metrics = entries[0]["metrics"]
    data_entries = []
    for entry in entries:
        delta = {
            key: round(entry["metrics"][key] - target_metrics[key], 4)
            for key in target_metrics
        }
        data_entries.append({
            "id": entry["id"],
            "title": entry["title"],
            "image": entry["path"],
            "centralCropFraction": 0.8,
            "metrics": entry["metrics"],
            "deltaFromPrimaryReference": delta,
        })
    report = {
        "status": "verified_offline_reference_comparison",
        "unityExecuted": False,
        "comparisonImage": OUT_IMAGE.name,
        "entries": data_entries,
        "interpretationGate": "runtime actors, VFX, camera projection, and final lighting still require Unity Game View",
    }
    OUT_DATA.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    board = Image.new("RGB", (1920, 1080), (12, 8, 18))
    draw = ImageDraw.Draw(board)
    draw.text((36, 18), "PROCESS 19  /  PRIMARY REFERENCE VS RESOURCE-FIRST ROOMS", font=font(24, True), fill=(244, 228, 247))
    draw.text((36, 49), "same 16:9 review scale  |  metrics use central 80%  |  Unity remains untouched", font=font(14), fill=(164, 147, 175))
    panel_width, panel_height = 916, 476
    image_width, image_height = 884, 391
    origins = [(30, 80), (974, 80), (30, 570), (974, 570)]
    for entry, (origin_x, origin_y) in zip(entries, origins):
        draw.rounded_rectangle(
            (origin_x, origin_y, origin_x + panel_width, origin_y + panel_height),
            radius=8, fill=(24, 16, 31), outline=(84, 61, 101), width=2,
        )
        preview = cover(Image.open(entry["image"]).convert("RGB"), (image_width, image_height))
        board.paste(preview, (origin_x + 16, origin_y + 42))
        draw.text((origin_x + 16, origin_y + 10), entry["title"], font=font(18, True), fill=(241, 225, 246))
        metric = entry["metrics"]
        metric_line = (
            f"LUMA {metric['lumaMedian']:.3f}   SAT {metric['saturationMedian']:.3f}   "
            f"DARK {metric['darkUnder008Percent']:.1f}%   MID {metric['mid008To035Percent']:.1f}%"
        )
        draw.rectangle((origin_x + 16, origin_y + 408, origin_x + 900, origin_y + 461), fill=(12, 9, 17, 230))
        draw.text((origin_x + 28, origin_y + 414), metric_line, font=font(13, True), fill=(79, 231, 213))
        draw.text((origin_x + 28, origin_y + 438), entry["note"], font=font(13), fill=(205, 187, 214))
    board.save(OUT_IMAGE, quality=95)
    print(f"Wrote {OUT_IMAGE.relative_to(ROOT)}")
    print(f"Wrote {OUT_DATA.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
