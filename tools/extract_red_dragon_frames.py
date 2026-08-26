from __future__ import annotations

import argparse
import os
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter


CELL = 512
GRID = 4


def components(mask: np.ndarray):
    h, w = mask.shape
    labels = np.zeros((h, w), dtype=np.int32)
    found = []
    label = 0
    for y in range(h):
        for x in range(w):
            if not mask[y, x] or labels[y, x]:
                continue
            label += 1
            q = deque([(x, y)])
            labels[y, x] = label
            pixels = []
            min_x = max_x = x
            min_y = max_y = y
            while q:
                px, py = q.popleft()
                pixels.append((px, py))
                min_x = min(min_x, px)
                max_x = max(max_x, px)
                min_y = min(min_y, py)
                max_y = max(max_y, py)
                for oy in (-1, 0, 1):
                    for ox in (-1, 0, 1):
                        if ox == 0 and oy == 0:
                            continue
                        nx, ny = px + ox, py + oy
                        if 0 <= nx < w and 0 <= ny < h and mask[ny, nx] and not labels[ny, nx]:
                            labels[ny, nx] = label
                            q.append((nx, ny))
            found.append(
                {
                    "label": label,
                    "area": len(pixels),
                    "bbox": (min_x, min_y, max_x, max_y),
                    "pixels": pixels,
                }
            )
    return found


def segment_character(cell: Image.Image) -> Image.Image:
    rgb = np.asarray(cell.convert("RGB"), dtype=np.int16)
    r, g, b = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    sat = np.maximum.reduce([r, g, b]) - np.minimum.reduce([r, g, b])
    value = np.maximum.reduce([r, g, b])
    yy = np.arange(CELL)[:, None]

    # Saturated red/orange/plum pixels form the reliable character core. Pale cave
    # ground, dust, and shadow are intentionally excluded at this stage.
    core = (sat >= 40) | ((value <= 140) & (sat >= 18))
    core[:64, :] = False  # frame numbers and top border
    core[:, :20] = False
    core[:, -20:] = False
    core[-20:, :] = False

    ground_like = (
        (yy > 352)
        & (sat < 46)
        & (r > 120)
        & (g > 110)
        & (b > 95)
        & ((r - g) < 54)
        & ((g - b) < 54)
    )
    core[ground_like] = False

    found = components(core)
    eligible = [item for item in found if item["area"] >= 180 or (item["bbox"][3] - item["bbox"][1]) >= 52]
    if not eligible:
        return Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))

    def score(item):
        x0, y0, x1, y1 = item["bbox"]
        cx = (x0 + x1) / 2
        cy = (y0 + y1) / 2
        centrality = max(0.15, 1.0 - (abs(cx - 256) / 300 + abs(cy - 290) / 360) * 0.35)
        height_bonus = min(2.0, max(0.3, (y1 - y0 + 1) / 160))
        ground_penalty = 0.12 if y0 > 300 and (x1 - x0) > 140 and (y1 - y0) < 170 else 1.0
        return item["area"] * centrality * height_bonus * ground_penalty

    main = max(eligible, key=score)
    mx0, my0, mx1, my1 = main["bbox"]
    selected = np.zeros((CELL, CELL), dtype=bool)
    main_mask = np.zeros((CELL, CELL), dtype=bool)
    for px, py in main["pixels"]:
        main_mask[py, px] = True
    main_dilated = np.asarray(
        Image.fromarray((main_mask.astype(np.uint8) * 255), mode="L").filter(ImageFilter.MaxFilter(5))
    ) > 0

    # Keep the main body plus nearby saturated islands such as wing membranes, tail
    # flame, eyes, and claws. Detached embers remain excluded.
    for item in found:
        x0, y0, x1, y1 = item["bbox"]
        sat_mean = float(np.mean([sat[py, px] for px, py in item["pixels"]]))
        touches_main = any(main_dilated[py, px] for px, py in item["pixels"])
        if item is main or (touches_main and sat_mean >= 34 and item["area"] >= 40):
            for px, py in item["pixels"]:
                selected[py, px] = True

    # Restore anti-aliased contour and cream chest edges only immediately around the
    # saturated character core.
    selected_img = Image.fromarray((selected.astype(np.uint8) * 255), mode="L")
    dilated = np.asarray(selected_img.filter(ImageFilter.MaxFilter(5))) > 0
    pale_character = (
        dilated
        & (r > 150)
        & (g > 130)
        & (b > 95)
        & ((r - b) > 18)
    )
    selected |= pale_character

    # Remove any residual low, flat ground strip while preserving saturated feet and claws.
    residual_ground = (
        (yy > 328)
        & (sat < 58)
        & (r > 120)
        & (g > 110)
        & (b > 95)
    )
    selected[residual_ground] = False
    residual_dust = (
        (yy > 260)
        & (sat < 70)
        & (r > 120)
        & (g > 110)
        & (b > 95)
    )
    selected[residual_dust] = False

    # One final connectedness pass removes isolated sparks and dust fragments that
    # survived the color test. Keep only the component touching the character core.
    final_parts = components(selected)
    if final_parts:
        primary = max(final_parts, key=lambda item: item["area"])
        px0, py0, px1, py1 = primary["bbox"]
    else:
        primary = None
        px0 = py0 = px1 = py1 = 0
    keep = np.zeros((CELL, CELL), dtype=bool)
    for item in final_parts:
        x0, y0, x1, y1 = item["bbox"]
        dx = max(px0 - x1, x0 - px1, 0)
        dy = max(py0 - y1, y0 - py1, 0)
        near_main = max(dx, dy) <= 8
        if near_main and item["area"] >= 250:
            for px, py in item["pixels"]:
                keep[py, px] = True
    selected = keep

    alpha = Image.fromarray((selected.astype(np.uint8) * 255), mode="L")
    alpha = alpha.filter(ImageFilter.GaussianBlur(0.65))
    out = Image.fromarray(rgb.astype(np.uint8), mode="RGB").convert("RGBA")
    out.putalpha(alpha)
    return out


def extract(sheet_path: Path, kind: str, frame_count: int, output_root: Path):
    sheet = Image.open(sheet_path).convert("RGB")
    if sheet.size != (2048, 2048):
        raise ValueError(f"Expected 2048x2048 sheet, got {sheet.size}: {sheet_path}")
    out_dir = output_root / kind
    out_dir.mkdir(parents=True, exist_ok=True)
    for idx in range(frame_count):
        col = idx % GRID
        row = idx // GRID
        cell = sheet.crop((col * CELL, row * CELL, (col + 1) * CELL, (row + 1) * CELL))
        if kind == "spawn" and idx == 0:
            result = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
        else:
            result = segment_character(cell)
        bbox = result.getchannel("A").getbbox()
        if bbox:
            # Normalize every usable frame to the shared ground pivot y=432.
            shift_y = 432 - bbox[3]
            aligned = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
            aligned.alpha_composite(result, (0, shift_y))
            result = aligned
        result.save(out_dir / f"frame_{idx + 1:02d}.png", "PNG")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--spawn-sheet", type=Path, required=True)
    parser.add_argument("--idle-sheet", type=Path, required=True)
    parser.add_argument("--output-root", type=Path, required=True)
    args = parser.parse_args()
    extract(args.spawn_sheet, "spawn", 8, args.output_root)
    extract(args.idle_sheet, "idle", 4, args.output_root)


if __name__ == "__main__":
    main()
