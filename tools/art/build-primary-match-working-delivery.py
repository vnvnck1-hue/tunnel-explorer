#!/usr/bin/env python3
"""Split and normalize Primary Match sheets into Unity-ready *working* files.

Nothing is copied into Unity and nothing is marked approved.  The output keeps
all five channels pixel-aligned and records the exact transform used per asset.
"""

from __future__ import annotations

import hashlib
import json
import math
import re
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw


ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / "art-production/test-room-v01/working/primary-match-v2"
CATALOG = BASE / "runtime-catalog-candidates.json"
CHANNEL_REPORT = BASE / "channels/resource-first/report.json"
OUT = BASE / "delivery-candidates"
MANIFEST = OUT / "manifest.json"
BOARD = BASE / "diorama-process-08-normalized-delivery-review.png"
PPU = 128
EDGE_PAD = 8


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def slug(text: str) -> str:
    text = text.lower().replace("3x3", "").replace("2x2", "")
    text = re.sub(r"[^a-z0-9]+", "_", text).strip("_")
    return text


def category_for(sheet_id: str) -> str:
    mapping = {
        "FLOOR-RAIL": "rail",
        "CRYSTAL-OUTCROPS": "crystal",
        "EQUIPMENT-TRANSITIONS": "equipment",
        "HERO-MACHINERY": "hero",
        "FOREGROUND-DEPTH": "foreground",
        "MONUMENTAL-WALL": "backdrop",
    }
    return next(value for key, value in mapping.items() if key in sheet_id)


def neutral(channel: str) -> tuple[int, int, int, int]:
    if channel == "normal":
        return 128, 128, 255, 0
    if channel == "ao":
        return 255, 255, 255, 0
    return 0, 0, 0, 0


def transform_cell(cell: Image.Image, channel: str, measured: dict, runtime: dict) -> tuple[Image.Image, dict]:
    kind = runtime["runtimeKind"]
    if kind == "ground_overlay":
        image = cell.resize((PPU, PPU), Image.Resampling.LANCZOS)
        return image, {
            "mode": "full_cell_ground_overlay",
            "sourceCropPixels": [0, 0, cell.width, cell.height],
            "scale": round(PPU / cell.width, 6),
            "pivotPixelsTopLeft": [PPU // 2, PPU // 2],
            "pivotNormalizedBottomOrigin": [0.5, 0.5],
        }

    left, top, right, bottom = measured["alphaBoundsTopLeftPixels"]
    left = max(0, left - 2)
    top = max(0, top - 2)
    right = min(cell.width, right + 2)
    bottom = min(cell.height, bottom + 2)

    footprint = runtime["footprintCells"]
    visual_height = max(0.5, float(runtime["visualHeightCells"]))
    canvas_w = int(footprint[0] * PPU + EDGE_PAD * 4)
    canvas_h = int(math.ceil(visual_height * PPU) + EDGE_PAD * 2)
    pivot_target_x = canvas_w * 0.5
    pivot_target_y = canvas_h - EDGE_PAD

    pivot_norm_x, pivot_norm_y = measured["bottomContactPivotNormalized"]
    pivot_src_x = pivot_norm_x * cell.width
    pivot_src_y = (1.0 - pivot_norm_y) * cell.height

    extents = {
        "left": max(1.0, pivot_src_x - left),
        "right": max(1.0, right - pivot_src_x),
        "top": max(1.0, pivot_src_y - top),
        "bottom": max(1.0, bottom - pivot_src_y),
    }
    capacities = {
        "left": pivot_target_x - EDGE_PAD,
        "right": canvas_w - pivot_target_x - EDGE_PAD,
        "top": pivot_target_y - EDGE_PAD,
        "bottom": canvas_h - pivot_target_y,
    }
    scales = [capacities[k] / extents[k] for k in ("left", "right", "top")]
    if extents["bottom"] > 1.5:
        scales.append(max(1.0, capacities["bottom"]) / extents["bottom"])
    scale = min(scales)

    crop = cell.crop((left, top, right, bottom))
    out_w = max(1, round(crop.width * scale))
    out_h = max(1, round(crop.height * scale))
    crop = crop.resize((out_w, out_h), Image.Resampling.LANCZOS)
    paste_x = round(pivot_target_x - (pivot_src_x - left) * scale)
    paste_y = round(pivot_target_y - (pivot_src_y - top) * scale)

    canvas = Image.new("RGBA", (canvas_w, canvas_h), neutral(channel))
    canvas.alpha_composite(crop, (paste_x, paste_y))
    return canvas, {
        "mode": "alpha_trimmed_footpoint_canvas",
        "sourceCropPixels": [left, top, right, bottom],
        "scale": round(scale, 6),
        "pastePixelsTopLeft": [paste_x, paste_y],
        "pivotPixelsTopLeft": [round(pivot_target_x), round(pivot_target_y)],
        "pivotNormalizedBottomOrigin": [round(pivot_target_x / canvas_w, 6), round((canvas_h - pivot_target_y) / canvas_h, 6)],
    }


def main() -> None:
    catalog = json.loads(CATALOG.read_text(encoding="utf-8"))
    channel_report = json.loads(CHANNEL_REPORT.read_text(encoding="utf-8"))
    channel_by_sheet = {entry["assetId"]: entry["channels"] for entry in channel_report["assets"]}

    for channel in ("albedo", "normal", "ao", "emission", "mask"):
        (OUT / channel).mkdir(parents=True, exist_ok=True)

    delivery = {
        "packageId": "tr01-primary-match-bold-working-delivery-v1",
        "revision": 1,
        "status": "working_candidate_not_approved",
        "productionProjection": "ReferenceTopDown",
        "deliveryPixelsPerCell": PPU,
        "pivotPixelsCoordinateSystem": "image_top_left_y_down",
        "sourceRuntimeCatalog": "../runtime-catalog-candidates.json",
        "unityImported": False,
        "assets": [],
    }

    board = Image.new("RGB", (1920, 1080), (18, 14, 25))
    draw = ImageDraw.Draw(board)
    draw.text((42, 22), "PROCESS 08  /  39 NORMALIZED WORKING DELIVERY ASSETS  /  128 PPU  /  UNITY NOT TOUCHED", fill=(236, 220, 244))
    draw.text((42, 46), "amber cross = delivery pivot   each albedo has pixel-aligned normal / ao / emission / material mask", fill=(154, 140, 169))

    thumb_slots = []
    for sheet in catalog["sheets"]:
        source_paths = {"albedo": BASE / sheet["path"]}
        source_paths.update({channel: BASE / data["path"] for channel, data in channel_by_sheet[sheet["assetId"]].items()})
        sources = {channel: Image.open(path).convert("RGBA") for channel, path in source_paths.items()}
        cols, rows = sheet["grid"]
        cell_w, cell_h = sheet["cellDimensionsPixels"]
        category = category_for(sheet["assetId"])

        for variant in sheet["variants"]:
            rect_x, rect_y, rect_w, rect_h = variant["cell"]["rectPixels"]
            outputs = {}
            common_transform = None
            silhouette_alpha = None
            name = variant["assetId"].split("/", 1)[1]
            stem = f"tr01_primarymatch_{category}_{slug(name)}_a"
            for channel, source in sources.items():
                cell = source.crop((rect_x, rect_y, rect_x + rect_w, rect_y + rect_h))
                normalized, transform = transform_cell(
                    cell, channel, variant["measured"], variant["runtimeCandidate"]
                )
                if channel == "albedo":
                    silhouette_alpha = normalized.getchannel("A").copy()
                elif channel in ("normal", "ao"):
                    normalized.putalpha(silhouette_alpha)
                else:
                    # Emission/mask alpha encodes strength, but it must never
                    # extend beyond the shared sprite silhouette after resize.
                    normalized.putalpha(ImageChops.darker(normalized.getchannel("A"), silhouette_alpha))
                if common_transform is None:
                    common_transform = transform
                elif normalized.size != tuple(outputs["albedo"]["dimensionsPixels"]):
                    raise AssertionError(f"channel dimensions diverged for {variant['assetId']}")
                filename = f"{stem}_{channel}.png"
                path = OUT / channel / filename
                normalized.save(path, optimize=True)
                outputs[channel] = {
                    "path": f"{channel}/{filename}",
                    "dimensionsPixels": list(normalized.size),
                    "sha256": sha256(path),
                }

            delivery_id = f"TR01-PM-{category.upper()}-{name.upper().replace('_', '-')}"
            entry = {
                "assetId": delivery_id,
                "sourceVariantId": variant["assetId"],
                "status": "working_candidate_not_approved",
                "channelStatus": {channel: "derived_working_candidate" for channel in outputs},
                "footprintCells": variant["runtimeCandidate"]["footprintCells"],
                "visualHeightCells": variant["runtimeCandidate"]["visualHeightCells"],
                "sortingLayerHint": variant["runtimeCandidate"]["sortingLayer"],
                "localOrder": variant["runtimeCandidate"]["localOrder"],
                "pivotPixels": common_transform["pivotPixelsTopLeft"],
                "pivotNormalized": common_transform["pivotNormalizedBottomOrigin"],
                "dimensionsPixels": outputs["albedo"]["dimensionsPixels"],
                "channels": {channel: data["path"] for channel, data in outputs.items()},
                "channelHashes": {channel: data["sha256"] for channel, data in outputs.items()},
                "normalization": common_transform,
                "runtimeCandidate": variant["runtimeCandidate"],
            }
            delivery["assets"].append(entry)
            thumb_slots.append((category, name, OUT / outputs["albedo"]["path"], entry))

    # Compact visual audit: six category columns, assets stacked into their column.
    categories = ["rail", "crystal", "equipment", "hero", "foreground", "backdrop"]
    per_category = {category: [] for category in categories}
    for item in thumb_slots:
        per_category[item[0]].append(item)
    column_w = 304
    for ci, category in enumerate(categories):
        x0 = 38 + ci * 313
        draw.text((x0, 78), category.upper(), fill=(226, 210, 236))
        items = per_category[category]
        rows = len(items)
        slot_h = min(102, 930 // max(1, rows))
        for ri, (_, name, path, entry) in enumerate(items):
            y0 = 104 + ri * slot_h
            sprite = Image.open(path).convert("RGBA")
            max_h = slot_h - 18
            max_w = 112
            scale = min(max_w / sprite.width, max_h / sprite.height)
            size = (max(1, round(sprite.width * scale)), max(1, round(sprite.height * scale)))
            sprite = sprite.resize(size, Image.Resampling.LANCZOS)
            checker = Image.new("RGB", size, (31, 25, 41))
            checker.paste(sprite, mask=sprite.getchannel("A"))
            board.paste(checker, (x0, y0))
            pivot_x, pivot_y_top = entry["pivotPixels"]
            px = x0 + pivot_x * scale
            py = y0 + pivot_y_top * scale
            draw.line((px - 4, py, px + 4, py), fill=(255, 187, 68), width=2)
            draw.line((px, py - 4, px, py + 4), fill=(255, 187, 68), width=2)
            draw.text((x0 + 120, y0 + 4), name[:27], fill=(192, 180, 202))
            dims = entry["dimensionsPixels"]
            draw.text((x0 + 120, y0 + 22), f"{dims[0]}x{dims[1]}  fp {entry['footprintCells']}", fill=(120, 110, 133))

    delivery["summary"] = {
        "assetCount": len(delivery["assets"]),
        "channelImages": len(delivery["assets"]) * 5,
        "reviewImage": "../diorama-process-08-normalized-delivery-review.png",
    }
    MANIFEST.write_text(json.dumps(delivery, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    board.save(BOARD)
    print(f"Wrote {MANIFEST.relative_to(ROOT)}: {len(delivery['assets'])} assets, {len(delivery['assets']) * 5} channel files")
    print(f"Wrote {BOARD.relative_to(ROOT)}: {board.width}x{board.height}")


if __name__ == "__main__":
    main()
