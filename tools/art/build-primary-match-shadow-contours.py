#!/usr/bin/env python3
"""Trace lower-silhouette shadow contours for normalized working assets.

This mirrors the intent and constants of Unity's SpriteAlphaContour without
opening the Editor.  Outputs remain candidates until projection/light review.
"""

from __future__ import annotations

import hashlib
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / "art-production/test-room-v01/working/primary-match-v2"
DELIVERY_DIR = BASE / "delivery-candidates"
DELIVERY_MANIFEST = DELIVERY_DIR / "manifest.json"
OUT = BASE / "shadow-contour-candidates.json"
BOARD = BASE / "diorama-process-10-shadow-contour-review.png"
PPU = 128.0
ALPHA_CUT = 0.35
MAX_POINTS = 24


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def point_line_distance(a: tuple[float, float], b: tuple[float, float], c: tuple[float, float]) -> float:
    ac = (c[0] - a[0], c[1] - a[1])
    length = math.hypot(*ac)
    if length < 1e-5:
        return math.dist(a, b)
    return abs(ac[0] * (a[1] - b[1]) - (a[0] - b[0]) * ac[1]) / length


def simplify(points: list[tuple[float, float]], tolerance: float = 0.05) -> list[tuple[float, float]]:
    if len(points) <= 4:
        return points
    out = [points[0]]
    for index in range(1, len(points) - 1):
        if point_line_distance(out[-1], points[index], points[index + 1]) > tolerance:
            out.append(points[index])
    out.append(points[-1])
    return out


def trace(asset: dict, image: Image.Image) -> list[tuple[float, float]]:
    alpha = np.asarray(image.getchannel("A"), dtype=np.float32) / 255.0
    height, width = alpha.shape
    pivot_x, pivot_top_y = [float(v) for v in asset["pivotPixels"]]
    cols, rows = asset["footprintCells"]

    # Equivalent top-left coordinates for Unity's lower footprint band scan.
    band_low_top = min(height - 1, round(pivot_top_y + PPU * 0.15))
    band_high_top = max(0, round(pivot_top_y - rows * PPU + PPU * 0.15))
    steps = max(2, MAX_POINTS // 2)
    row_span = max(1, (band_low_top - band_high_top) // steps)
    left = []
    right = []
    for step in range(steps):
        t = step / (steps - 1)
        y_top = round(band_low_top + (band_high_top - band_low_top) * t)
        y0 = max(0, y_top - row_span + 1)
        y1 = min(height, y_top + 1)
        ys, xs = np.nonzero(alpha[y0:y1, :] >= ALPHA_CUT)
        if xs.size == 0:
            continue
        # Cell Y grows screen-up from the delivery footpoint.
        cell_y = max(0.0, (pivot_top_y - y_top) / PPU)
        left.append(((float(xs.min()) - pivot_x) / PPU, cell_y))
        right.append(((float(xs.max()) + 1.0 - pivot_x) / PPU, cell_y))

    if len(left) < 2:
        return []
    polygon = [(left[0][0], 0.0), (right[0][0], 0.0)]
    polygon.extend(right[1:])
    polygon.extend(reversed(left[1:]))

    half_width = max(1, cols) * 0.5 + 0.35
    max_depth = max(1, rows) + 0.35
    polygon = [
        (max(-half_width, min(half_width, x)), max(0.0, min(max_depth, y)))
        for x, y in polygon
    ]
    return simplify(polygon)


def main() -> None:
    delivery = json.loads(DELIVERY_MANIFEST.read_text(encoding="utf-8"))
    targets = [asset for asset in delivery["assets"] if asset["runtimeCandidate"]["runtimeKind"] != "ground_overlay"]
    result = {
        "status": "offline_candidate_not_unity_integrated",
        "source": "delivery-candidates/manifest.json",
        "algorithmContract": {
            "matchesIntentOf": "SpriteAlphaContour.cs",
            "alphaCut": ALPHA_CUT,
            "maximumPoints": MAX_POINTS,
            "scan": "lower footprint-depth band only",
            "coordinates": "delivery footpoint-relative cells",
        },
        "assets": [],
    }

    board = Image.new("RGBA", (1920, 1080), (18, 14, 25, 255))
    draw = ImageDraw.Draw(board)
    draw.text((42, 20), "PROCESS 10  /  LOWER-SILHOUETTE SHADOW CONTOURS  /  NON-RECTANGULAR CANDIDATES", fill=(237, 220, 245))
    draw.text((42, 44), "cyan polygon = caster footprint inferred from the sprite's lower band   amber cross = footpoint", fill=(154, 140, 169))

    for index, asset in enumerate(targets):
        albedo_path = DELIVERY_DIR / asset["channels"]["albedo"]
        image = Image.open(albedo_path).convert("RGBA")
        contour = trace(asset, image)
        if len(contour) < 3:
            raise AssertionError(f"could not trace {asset['assetId']}")
        flat = [round(value, 3) for point in contour for value in point]
        result["assets"].append({
            "assetId": asset["assetId"],
            "sourceVariantId": asset["sourceVariantId"],
            "albedoPath": asset["channels"]["albedo"],
            "albedoSha256": sha256(albedo_path),
            "footprintCells": asset["footprintCells"],
            "shadowContourCells": flat,
            "pointCount": len(contour),
            "status": "candidate_not_unity_verified",
        })

        col, row = index % 5, index // 5
        x0, y0 = 35 + col * 378, 82 + row * 327
        panel_w, panel_h = 330, 255
        scale = min((panel_w - 16) / image.width, (panel_h - 34) / image.height)
        size = (max(1, round(image.width * scale)), max(1, round(image.height * scale)))
        shown = image.resize(size, Image.Resampling.LANCZOS)
        px0 = x0 + (panel_w - size[0]) // 2
        py0 = y0 + panel_h - size[1]
        panel = Image.new("RGB", (panel_w, panel_h), (31, 25, 41))
        panel.paste(shown, ((panel_w - size[0]) // 2, panel_h - size[1]), shown.getchannel("A"))
        board.paste(panel, (x0, y0))
        pivot_x = px0 + asset["pivotPixels"][0] * scale
        pivot_y = py0 + asset["pivotPixels"][1] * scale
        screen_points = [(pivot_x + x * PPU * scale, pivot_y - y * PPU * scale) for x, y in contour]
        overlay = Image.new("RGBA", board.size, (0, 0, 0, 0))
        overlay_draw = ImageDraw.Draw(overlay)
        overlay_draw.polygon(screen_points, fill=(40, 207, 220, 62))
        overlay_draw.line(screen_points + [screen_points[0]], fill=(54, 224, 235, 235), width=2)
        board.alpha_composite(overlay)
        draw.line((pivot_x - 5, pivot_y, pivot_x + 5, pivot_y), fill=(255, 190, 67), width=2)
        draw.line((pivot_x, pivot_y - 5, pivot_x, pivot_y + 5), fill=(255, 190, 67), width=2)
        draw.text((x0, y0 + panel_h + 7), asset["assetId"].replace("TR01-PM-", "")[:43], fill=(204, 190, 215))
        draw.text((x0, y0 + panel_h + 25), f"points {len(contour)} / fp {asset['footprintCells']}", fill=(125, 113, 139))

    result["summary"] = {
        "assetCount": len(result["assets"]),
        "minimumPointCount": min(asset["pointCount"] for asset in result["assets"]),
        "maximumPointCount": max(asset["pointCount"] for asset in result["assets"]),
        "reviewImage": "diorama-process-10-shadow-contour-review.png",
    }
    OUT.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    board.convert("RGB").save(BOARD)
    print(f"Wrote {OUT.relative_to(ROOT)}: {result['summary']}")
    print(f"Wrote {BOARD.relative_to(ROOT)}: {board.width}x{board.height}")


if __name__ == "__main__":
    main()
