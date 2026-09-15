#!/usr/bin/env python3
"""Derive conservative light-socket candidates from normalized emission maps."""

from __future__ import annotations

import hashlib
import json
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / "art-production/test-room-v01/working/primary-match-v2"
DELIVERY_DIR = BASE / "delivery-candidates"
DELIVERY_MANIFEST = DELIVERY_DIR / "manifest.json"
OUT = BASE / "light-socket-candidates.json"
BOARD = BASE / "diorama-process-09-light-socket-review.png"
PPU = 128


# Approximate semantic anchors are authored from the process-08 visual review.
# Detection snaps each anchor to a real same-hue emission component.
SEMANTIC_TARGETS = {
    "TR01-PM-HERO-ORE-CRUSHER": [
        {"hue": "magenta", "offset": [0.0, 1.35], "lightClass": "MineralGlow", "range": 1.8, "intensity": 0.48},
    ],
    "TR01-PM-HERO-VENTILATION-TURBINE": [
        {"hue": "cyan", "offset": [0.0, 1.15], "lightClass": "Worklamp", "range": 2.15, "intensity": 0.62},
    ],
    "TR01-PM-HERO-POWER-RELAY": [
        {"hue": "magenta", "offset": [0.0, 1.2], "lightClass": "MineralGlow", "range": 2.2, "intensity": 0.64},
        {"hue": "cyan", "offset": [0.65, 1.05], "lightClass": "Indicator", "range": 1.25, "intensity": 0.32},
    ],
    "TR01-PM-HERO-MINECART-LOADING-DOCK": [
        {"hue": "amber", "offset": [0.75, 0.82], "lightClass": "Worklamp", "range": 1.85, "intensity": 0.56},
    ],
    "TR01-PM-BACKDROP-SEALED-BULKHEAD": [
        {"hue": "cyan", "offset": [0.0, 1.3], "lightClass": "Worklamp", "range": 1.85, "intensity": 0.52},
    ],
    "TR01-PM-BACKDROP-PIPE-MANIFOLD": [
        {"hue": "magenta", "offset": [-0.45, 1.15], "lightClass": "MineralGlow", "range": 1.75, "intensity": 0.48},
        {"hue": "magenta", "offset": [0.45, 1.15], "lightClass": "MineralGlow", "range": 1.75, "intensity": 0.48},
    ],
    "TR01-PM-BACKDROP-CRYSTAL-PROCESSOR": [
        {"hue": "magenta", "offset": [0.0, 1.25], "lightClass": "MineralGlow", "range": 2.3, "intensity": 0.66},
        {"hue": "magenta", "offset": [0.85, 0.55], "lightClass": "Indicator", "range": 1.15, "intensity": 0.3},
    ],
    "TR01-PM-BACKDROP-COLLAPSED-WALL-MACHINE": [
        {"hue": "magenta", "offset": [-0.65, 0.45], "lightClass": "MineralGlow", "range": 1.55, "intensity": 0.4},
    ],
}


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def components(mask: np.ndarray) -> list[list[tuple[int, int]]]:
    height, width = mask.shape
    seen = np.zeros_like(mask, dtype=np.bool_)
    found = []
    for y, x in zip(*np.nonzero(mask)):
        if seen[y, x]:
            continue
        queue = deque([(int(y), int(x))])
        seen[y, x] = True
        group = []
        while queue:
            cy, cx = queue.popleft()
            group.append((cy, cx))
            for ny in range(max(0, cy - 1), min(height, cy + 2)):
                for nx in range(max(0, cx - 1), min(width, cx + 2)):
                    if mask[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        queue.append((ny, nx))
        if len(group) >= 3:
            found.append(group)
    return found


def hue_name(rgb: np.ndarray) -> tuple[str, list[float]]:
    r, g, b = [float(value) for value in rgb]
    scores = {
        "magenta": r + b - g * 0.8,
        "cyan": g + b - r * 0.8,
        "amber": r + g - b * 0.8,
    }
    name = max(scores, key=scores.get)
    palette = {
        "magenta": [0.92, 0.18, 0.82, 1.0],
        "cyan": [0.16, 0.82, 0.92, 1.0],
        "amber": [1.0, 0.48, 0.12, 1.0],
    }
    return name, palette[name]


def analyze_asset(asset: dict) -> list[dict]:
    emission_path = DELIVERY_DIR / asset["channels"]["emission"]
    rgba = np.asarray(Image.open(emission_path).convert("RGBA"), dtype=np.float32)
    alpha = rgba[..., 3]
    # High threshold rejects broad, low-energy mineral tint and keeps readable lamps/panels.
    mask = alpha >= 72
    groups = components(mask)
    pivot_x, pivot_y = asset["pivotPixels"]
    candidates = []
    for group in groups:
        ys = np.fromiter((p[0] for p in group), dtype=np.int32)
        xs = np.fromiter((p[1] for p in group), dtype=np.int32)
        weights = np.maximum(1.0, alpha[ys, xs])
        energy = float(weights.sum())
        if energy < 900:
            continue
        cx = float(np.average(xs + 0.5, weights=weights))
        cy = float(np.average(ys + 0.5, weights=weights))
        color_sample = np.average(rgba[ys, xs, :3], axis=0, weights=weights)
        hue, color = hue_name(color_sample)
        candidates.append({
            "energy": energy,
            "pixel": [cx, cy],
            "offsetCells": [round((cx - pivot_x) / PPU, 3), round((pivot_y - cy) / PPU, 3)],
            "hue": hue,
            "color": color,
            "componentPixels": len(group),
        })

    selected = []
    remaining = list(candidates)
    for target in SEMANTIC_TARGETS[asset["assetId"]]:
        same_hue = [candidate for candidate in remaining if candidate["hue"] == target["hue"]]
        pool = same_hue or remaining
        if not pool:
            raise AssertionError(f"no emission component near semantic target: {asset['assetId']}")
        tx, ty = target["offset"]
        candidate = min(pool, key=lambda item: (item["offsetCells"][0] - tx) ** 2 + (item["offsetCells"][1] - ty) ** 2)
        remaining.remove(candidate)
        distance = ((candidate["offsetCells"][0] - tx) ** 2 + (candidate["offsetCells"][1] - ty) ** 2) ** 0.5
        candidate["semanticTargetOffsetCells"] = target["offset"]
        candidate["snapDistanceCells"] = round(distance, 3)
        candidate["lightClass"] = target["lightClass"]
        candidate["rangeCells"] = target["range"]
        candidate["intensity"] = target["intensity"]
        selected.append(candidate)

    for index, candidate in enumerate(selected):
        candidate.update({
            "id": f"emission_{candidate['hue']}_{index + 1}",
            "status": "candidate_not_unity_verified",
        })
        del candidate["energy"]
    return selected


def main() -> None:
    delivery = json.loads(DELIVERY_MANIFEST.read_text(encoding="utf-8"))
    targets = [asset for asset in delivery["assets"] if "-HERO-" in asset["assetId"] or "-BACKDROP-" in asset["assetId"]]
    result = {
        "status": "offline_candidate_not_unity_integrated",
        "source": "delivery-candidates/manifest.json",
        "scope": "hero machinery and monumental backdrop only",
        "threshold": {"emissionAlphaMinimum": 72, "minimumWeightedEnergy": 900, "maximumSocketsPerAsset": 2},
        "rules": [
            "do not generate Light2D sockets for ground overlays or distributed crystal sheets",
            "magenta candidates default to MineralGlow; cyan and amber candidates default to Worklamp",
            "all socket positions are measured from the normalized delivery footpoint at 128 PPU",
            "Unity review may demote small panels to Indicator or remove redundant sockets",
        ],
        "assets": [],
    }

    board = Image.new("RGB", (1920, 1080), (18, 14, 25))
    draw = ImageDraw.Draw(board)
    draw.text((44, 24), "PROCESS 09  /  CONSERVATIVE LIGHT-SOCKET CANDIDATES  /  HERO + BACKDROP ONLY", fill=(237, 220, 245))
    draw.text((44, 49), "ring = measured emission cluster   line = footpoint-to-socket offset   Unity Light2D is not simulated", fill=(154, 140, 169))

    for index, asset in enumerate(targets):
        sockets = analyze_asset(asset)
        result["assets"].append({
            "assetId": asset["assetId"],
            "sourceVariantId": asset["sourceVariantId"],
            "emissionPath": asset["channels"]["emission"],
            "emissionSha256": sha256(DELIVERY_DIR / asset["channels"]["emission"]),
            "sockets": sockets,
        })

        col, row = index % 4, index // 4
        x0, y0 = 55 + col * 465, 105 + row * 475
        sprite = Image.open(DELIVERY_DIR / asset["channels"]["albedo"]).convert("RGBA")
        max_size = 335
        scale = min(max_size / sprite.width, max_size / sprite.height)
        size = (round(sprite.width * scale), round(sprite.height * scale))
        shown = sprite.resize(size, Image.Resampling.LANCZOS)
        panel = Image.new("RGB", (350, 350), (31, 25, 41))
        px0, py0 = (350 - size[0]) // 2, (350 - size[1]) // 2
        panel.paste(shown, (px0, py0), shown.getchannel("A"))
        board.paste(panel, (x0, y0))
        pivot_x = x0 + px0 + asset["pivotPixels"][0] * scale
        pivot_y = y0 + py0 + asset["pivotPixels"][1] * scale
        draw.line((pivot_x - 6, pivot_y, pivot_x + 6, pivot_y), fill=(255, 191, 67), width=2)
        draw.line((pivot_x, pivot_y - 6, pivot_x, pivot_y + 6), fill=(255, 191, 67), width=2)
        colors = {"magenta": (239, 61, 211), "cyan": (49, 212, 232), "amber": (255, 147, 42)}
        for socket in sockets:
            sx = pivot_x + socket["offsetCells"][0] * PPU * scale
            sy = pivot_y - socket["offsetCells"][1] * PPU * scale
            color = colors[socket["hue"]]
            draw.line((pivot_x, pivot_y, sx, sy), fill=(*color, 130), width=2)
            draw.ellipse((sx - 11, sy - 11, sx + 11, sy + 11), outline=color, width=3)
        draw.text((x0, y0 + 360), asset["assetId"].replace("TR01-PM-", ""), fill=(209, 194, 220))
        draw.text((x0, y0 + 379), f"sockets {len(sockets)} / candidate only", fill=(126, 114, 140))

    result["summary"] = {
        "assetCount": len(result["assets"]),
        "socketCount": sum(len(asset["sockets"]) for asset in result["assets"]),
        "reviewImage": "diorama-process-09-light-socket-review.png",
    }
    OUT.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    board.save(BOARD)
    print(f"Wrote {OUT.relative_to(ROOT)}: {result['summary']['assetCount']} assets, {result['summary']['socketCount']} sockets")
    print(f"Wrote {BOARD.relative_to(ROOT)}: {board.width}x{board.height}")


if __name__ == "__main__":
    main()
