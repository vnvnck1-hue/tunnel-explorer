#!/usr/bin/env python3
"""Normalize generated crafting art into runtime-ready PNG/WebP assets."""

from __future__ import annotations

import argparse
import json
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont


ASSETS = {
    "icon-auto-turret": ("icons", (256, 256), 0.84),
    "icon-coolant-capsule": ("icons", (256, 256), 0.82),
    "icon-med-injector": ("icons", (256, 256), 0.84),
    "icon-flare-bundle": ("icons", (256, 256), 0.82),
    "icon-folding-barricade": ("icons", (256, 256), 0.88),
    "icon-shaped-charge": ("icons", (256, 256), 0.82),
    "ui-wheel-slot": ("ui", (256, 256), 0.96),
    "ui-wheel-selection": ("ui", (256, 256), 0.98),
    "ui-wheel-connector": ("ui", (640, 640), 0.92),
    "ui-detail-panel": ("ui", (384, 512), 0.96),
    "ui-craft-button": ("ui", (512, 176), 0.96),
    "ui-craft-action-glyph": ("ui", (128, 128), 0.82),
}


def remove_baked_checker(image: Image.Image) -> Image.Image:
    """Remove a light gray/white checkerboard baked into RGB generations."""
    rgba = image.convert("RGBA")
    if image.mode in ("RGBA", "LA") and rgba.getchannel("A").getextrema()[0] < 255:
        return rgba

    rgb = np.asarray(rgba)[..., :3].astype(np.int16)
    hi = rgb.max(axis=2)
    lo = rgb.min(axis=2)
    candidate = (lo >= 205) & ((hi - lo) <= 20)
    h, w = candidate.shape
    visited = np.zeros((h, w), dtype=np.uint8)
    queue: deque[tuple[int, int]] = deque()

    def seed(x: int, y: int) -> None:
        if candidate[y, x] and not visited[y, x]:
            visited[y, x] = 1
            queue.append((x, y))

    for x in range(w):
        seed(x, 0)
        seed(x, h - 1)
    for y in range(h):
        seed(0, y)
        seed(w - 1, y)

    # Ring-like UI assets enclose another checkerboard island in the center.
    cx, cy = w // 2, h // 2
    if candidate[max(0, cy - 4) : cy + 5, max(0, cx - 4) : cx + 5].mean() > 0.8:
        seed(cx, cy)

    while queue:
        x, y = queue.popleft()
        for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if 0 <= nx < w and 0 <= ny < h and candidate[ny, nx] and not visited[ny, nx]:
                visited[ny, nx] = 1
                queue.append((nx, ny))

    alpha = np.full((h, w), 255, dtype=np.uint8)
    alpha[visited.astype(bool)] = 0

    # Feather only retained pixels touching the removed background.
    bg = visited.astype(bool)
    adjacent = np.zeros_like(bg)
    adjacent[1:] |= bg[:-1]
    adjacent[:-1] |= bg[1:]
    adjacent[:, 1:] |= bg[:, :-1]
    adjacent[:, :-1] |= bg[:, 1:]
    edge = adjacent & ~bg
    neutral = np.clip(1.0 - (hi - lo) / 45.0, 0.0, 1.0)
    bright = np.clip((hi - 145) / 110.0, 0.0, 1.0)
    edge_alpha = np.clip(255 * (1.0 - neutral * bright * 0.82), 24, 255).astype(np.uint8)
    alpha[edge] = np.minimum(alpha[edge], edge_alpha[edge])

    out = np.dstack((rgb.astype(np.uint8), alpha))
    return Image.fromarray(out, "RGBA")


def normalize(image: Image.Image, size: tuple[int, int], fill_ratio: float) -> Image.Image:
    rgba = remove_baked_checker(image)
    bbox = rgba.getchannel("A").getbbox()
    if not bbox:
        raise ValueError("asset has no visible pixels")
    crop = rgba.crop(bbox)
    target_w = max(1, int(size[0] * fill_ratio))
    target_h = max(1, int(size[1] * fill_ratio))
    scale = min(target_w / crop.width, target_h / crop.height)
    resized = crop.resize(
        (max(1, round(crop.width * scale)), max(1, round(crop.height * scale))),
        Image.Resampling.LANCZOS,
    )
    canvas = Image.new("RGBA", size, (0, 0, 0, 0))
    canvas.alpha_composite(resized, ((size[0] - resized.width) // 2, (size[1] - resized.height) // 2))
    return canvas


def make_preview(outputs: dict[str, Path], destination: Path) -> None:
    canvas = Image.new("RGB", (1600, 1000), "#09070f")
    draw = ImageDraw.Draw(canvas)
    font = ImageFont.load_default()
    draw.text((48, 32), "TUNNEL CREW / CRAFTING ASSET KIT", fill="#f4d08a", font=font)

    icon_ids = [key for key in ASSETS if key.startswith("icon-")]
    for i, key in enumerate(icon_ids):
        x = 48 + i * 250
        y = 92
        tile = Image.new("RGBA", (220, 260), "#171120")
        tile.alpha_composite(Image.open(outputs[key]).convert("RGBA").resize((200, 200)), (10, 8))
        canvas.paste(tile, (x, y), tile)
        draw.text((x + 8, y + 230), key.removeprefix("icon-"), fill="#d8cadf", font=font)

    slot = Image.open(outputs["ui-wheel-slot"]).convert("RGBA").resize((210, 210))
    selected = Image.open(outputs["ui-wheel-selection"]).convert("RGBA").resize((210, 210))
    connector = Image.open(outputs["ui-wheel-connector"]).convert("RGBA").resize((460, 460))
    canvas.paste(connector, (58, 445), connector)
    canvas.paste(slot, (183, 570), slot)
    canvas.paste(selected, (183, 570), selected)
    draw.text((58, 920), "connector + slot + selected overlay", fill="#d8cadf", font=font)

    panel = Image.open(outputs["ui-detail-panel"]).convert("RGBA")
    button = Image.open(outputs["ui-craft-button"]).convert("RGBA").resize((410, 141))
    glyph = Image.open(outputs["ui-craft-action-glyph"]).convert("RGBA").resize((62, 62))
    canvas.paste(panel, (620, 430), panel)
    canvas.paste(button, (607, 820), button)
    canvas.paste(glyph, (780, 858), glyph)
    draw.text((620, 920), "detail panel / craft button", fill="#d8cadf", font=font)
    canvas.save(destination, optimize=True)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-dir", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    args = parser.parse_args()

    args.output_dir.mkdir(parents=True, exist_ok=True)
    outputs: dict[str, Path] = {}
    manifest = {"format": 1, "assets": {}}

    for asset_id, (group, size, fill_ratio) in ASSETS.items():
        src = args.source_dir / f"{asset_id}.raw.png"
        if not src.exists():
            raise FileNotFoundError(src)
        group_dir = args.output_dir / group
        group_dir.mkdir(parents=True, exist_ok=True)
        final = normalize(Image.open(src), size, fill_ratio)
        png = group_dir / f"{asset_id}.png"
        webp = group_dir / f"{asset_id}.webp"
        final.save(png, optimize=True)
        final.save(webp, format="WEBP", lossless=True, quality=100, method=6)
        outputs[asset_id] = png
        manifest["assets"][asset_id] = {
            "group": group,
            "png": png.relative_to(args.output_dir).as_posix(),
            "webp": webp.relative_to(args.output_dir).as_posix(),
            "width": size[0],
            "height": size[1],
            "alpha": True,
        }

    preview = args.output_dir / "crafting-assets-preview.png"
    make_preview(outputs, preview)
    (args.output_dir / "crafting-assets-manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    print(f"processed {len(outputs)} assets")
    print(f"preview: {preview}")


if __name__ == "__main__":
    main()
