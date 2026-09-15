#!/usr/bin/env python3
"""Create offline room-lighting calibration previews without touching Unity."""

from __future__ import annotations

import colorsys
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont


ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / "art-production/test-room-v01/working/primary-match-v2"
REFERENCE = ROOT / "art-production/test-room-v01/reference/tr01_primary_style_target.png"
OUT_CONTRACT = WORK / "room-lighting-calibration-candidates-v2.json"
OUT_BOARD = WORK / "diorama-process-27-lighting-calibration-v2-comparison.png"

ROOMS = [
    {
        "id": "ore_intake",
        "title": "ORE INTAKE",
        "source": "diorama-process-12-ore-intake-landscape.png",
        "output": "diorama-process-24-ore-intake-lighting-calibration-v2.png",
        "brightness": 1.02,
        "saturation": 0.98,
        "finalSaturation": 1.18,
        "lights": [
            {"kind": "radial", "box": [620, 210, 1660, 1040], "color": [24, 218, 232], "alpha": 46, "blur": 155},
            {"kind": "radial", "box": [20, 280, 930, 1030], "color": [255, 131, 34], "alpha": 28, "blur": 145},
            {"kind": "beam", "polygon": [[1680, 0], [1900, 0], [1190, 820], [910, 780]], "color": [35, 221, 235], "alpha": 18, "blur": 52},
        ],
        "intent": "reduce magenta dominance; add cyan route separation and amber machinery warmth",
    },
    {
        "id": "ventilation_service",
        "title": "VENTILATION SERVICE",
        "source": "diorama-process-13-ventilation-service-landscape.png",
        "output": "diorama-process-25-ventilation-service-lighting-calibration-v2.png",
        "brightness": 1.01,
        "saturation": 1.08,
        "finalSaturation": 1.20,
        "lights": [
            {"kind": "radial", "box": [430, 40, 1510, 990], "color": [22, 221, 235], "alpha": 44, "blur": 160},
            {"kind": "radial", "box": [650, 650, 1320, 1120], "color": [255, 143, 42], "alpha": 20, "blur": 125},
            {"kind": "beam", "polygon": [[80, 0], [300, 0], [1040, 850], [760, 900]], "color": [34, 215, 231], "alpha": 16, "blur": 58},
        ],
        "intent": "strengthen cyan landmark pool while preserving a quiet southern combat floor",
    },
    {
        "id": "crystal_power",
        "title": "CRYSTAL POWER",
        "source": "diorama-process-14-crystal-power-landscape.png",
        "output": "diorama-process-26-crystal-power-lighting-calibration-v2.png",
        "brightness": 1.03,
        "saturation": 1.0,
        "finalSaturation": 1.12,
        "lights": [
            {"kind": "radial", "box": [870, 210, 1860, 1040], "color": [24, 216, 232], "alpha": 48, "blur": 155},
            {"kind": "radial", "box": [260, 560, 1160, 1130], "color": [255, 135, 38], "alpha": 18, "blur": 135},
            {"kind": "beam", "polygon": [[1700, 0], [1900, 0], [1280, 810], [1010, 760]], "color": [38, 216, 232], "alpha": 16, "blur": 55},
        ],
        "intent": "separate cyan service light from the magenta core and add a restrained warm counterpoint",
    },
]


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    face = "C:/Windows/Fonts/seguisb.ttf" if bold else "C:/Windows/Fonts/segoeui.ttf"
    try:
        return ImageFont.truetype(face, size)
    except OSError:
        return ImageFont.load_default()


def cover(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    scale = max(size[0] / image.width, size[1] / image.height)
    resized = image.resize((round(image.width * scale), round(image.height * scale)), Image.Resampling.LANCZOS)
    left, top = (resized.width - size[0]) // 2, (resized.height - size[1]) // 2
    return resized.crop((left, top, left + size[0], top + size[1]))


def add_light(canvas: Image.Image, definition: dict) -> None:
    layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)
    fill = (*definition["color"], definition["alpha"])
    if definition["kind"] == "radial":
        draw.ellipse(definition["box"], fill=fill)
    else:
        draw.polygon([tuple(point) for point in definition["polygon"]], fill=fill)
    layer = layer.filter(ImageFilter.GaussianBlur(definition["blur"]))
    canvas.alpha_composite(layer)


def metrics(image: Image.Image) -> dict[str, float]:
    width, height = image.size
    sample = image.crop((round(width * 0.1), round(height * 0.1), round(width * 0.9), round(height * 0.9)))
    sample = sample.convert("RGB").resize((320, 180), Image.Resampling.BILINEAR)
    lumas, saturations = [], []
    accents = {"magenta": 0, "cyan": 0, "amber": 0}
    for red, green, blue in sample.get_flattened_data():
        r, g, b = red / 255.0, green / 255.0, blue / 255.0
        lumas.append(0.2126 * r + 0.7152 * g + 0.0722 * b)
        hue, saturation, value = colorsys.rgb_to_hsv(r, g, b)
        saturations.append(saturation)
        degrees = hue * 360.0
        if saturation >= 0.45 and value >= 0.15:
            if 285 <= degrees <= 345:
                accents["magenta"] += 1
            elif 165 <= degrees <= 205:
                accents["cyan"] += 1
            elif 20 <= degrees <= 55:
                accents["amber"] += 1
    lumas.sort()
    saturations.sort()
    count = len(lumas)
    return {
        "darkUnder008Percent": round(sum(value < 0.08 for value in lumas) * 100 / count, 2),
        "mid008To035Percent": round(sum(0.08 <= value < 0.35 for value in lumas) * 100 / count, 2),
        "lumaMedian": round(lumas[count // 2], 4),
        "saturationMedian": round(saturations[count // 2], 4),
        "magentaAccentPercent": round(accents["magenta"] * 100 / count, 2),
        "cyanAccentPercent": round(accents["cyan"] * 100 / count, 2),
        "amberAccentPercent": round(accents["amber"] * 100 / count, 2),
    }


def main() -> None:
    reference_metrics = metrics(Image.open(REFERENCE).convert("RGB"))
    results = []
    for room in ROOMS:
        source = Image.open(WORK / room["source"]).convert("RGB")
        calibrated = ImageEnhance.Brightness(source).enhance(room["brightness"])
        calibrated = ImageEnhance.Color(calibrated).enhance(room["saturation"]).convert("RGBA")
        for light in room["lights"]:
            add_light(calibrated, light)
        calibrated = ImageEnhance.Contrast(calibrated.convert("RGB")).enhance(1.015)
        calibrated = ImageEnhance.Color(calibrated).enhance(room["finalSaturation"])
        calibrated.save(WORK / room["output"], quality=95)
        measured = metrics(calibrated)
        results.append({
            "roomId": room["id"],
            "sourceImage": room["source"],
            "calibratedImage": room["output"],
            "brightnessFactor": room["brightness"],
            "saturationFactor": room["saturation"],
            "finalSaturationFactor": room["finalSaturation"],
            "lights": room["lights"],
            "intent": room["intent"],
            "metrics": measured,
            "deltaFromPrimaryReference": {
                key: round(measured[key] - reference_metrics[key], 4) for key in reference_metrics
            },
            "status": "offline_lighting_hypothesis_not_unity_verified",
        })

    contract = {
        "status": "offline_lighting_calibration_candidate_v2",
        "calibrationVersion": 2,
        "unityExecuted": False,
        "primaryReference": str(REFERENCE.relative_to(ROOT)).replace("\\", "/"),
        "primaryReferenceMetrics": reference_metrics,
        "rules": [
            "preserve source diorama pixels and save calibrated siblings",
            "use broad soft pools and one restrained directional beam per room",
            "do not treat preview alpha or blur as final Unity Light2D values",
            "preserve protected open-floor readability",
        ],
        "rooms": results,
        "comparisonImage": OUT_BOARD.name,
    }
    OUT_CONTRACT.write_text(json.dumps(contract, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    board = Image.new("RGB", (1920, 1080), (12, 8, 18))
    draw = ImageDraw.Draw(board)
    draw.text((36, 18), "PROCESS 27  /  PRIMARY-MATCH LIGHTING CALIBRATION V2", font=font(24, True), fill=(244, 228, 247))
    draw.text((36, 49), "narrower cyan pools + recovered color density  |  offline hypothesis", font=font(14), fill=(164, 147, 175))
    preview_width, preview_height = 590, 332
    for index, (room, result) in enumerate(zip(ROOMS, results)):
        x = 30 + index * 630
        y = 88
        source = cover(Image.open(WORK / room["source"]).convert("RGB"), (preview_width, preview_height))
        calibrated = cover(Image.open(WORK / room["output"]).convert("RGB"), (preview_width, preview_height))
        board.paste(source, (x, y))
        board.paste(calibrated, (x, y + 446))
        draw.rectangle((x, y, x + preview_width, y + preview_height), outline=(85, 63, 101), width=2)
        draw.rectangle((x, y + 446, x + preview_width, y + 446 + preview_height), outline=(55, 222, 206), width=2)
        draw.text((x, y + 342), f"{room['title']}  /  SOURCE", font=font(16, True), fill=(218, 201, 226))
        draw.text((x, y + 370), "unlit resource-first composition", font=font(13), fill=(147, 132, 159))
        draw.text((x, y + 788), f"{room['title']}  /  CALIBRATED", font=font(16, True), fill=(80, 234, 214))
        metric = result["metrics"]
        draw.text(
            (x, y + 816),
            f"LUMA {metric['lumaMedian']:.3f}  SAT {metric['saturationMedian']:.3f}  "
            f"M {metric['magentaAccentPercent']:.1f}%  C {metric['cyanAccentPercent']:.1f}%  A {metric['amberAccentPercent']:.1f}%",
            font=font(12, True), fill=(205, 190, 215),
        )
        draw.text((x, y + 842), room["intent"], font=font(12), fill=(153, 138, 166))
    board.save(OUT_BOARD, quality=95)
    print(f"Wrote {OUT_CONTRACT.relative_to(ROOT)}")
    for room in ROOMS:
        print(f"Wrote {(WORK / room['output']).relative_to(ROOT)}")
    print(f"Wrote {OUT_BOARD.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
