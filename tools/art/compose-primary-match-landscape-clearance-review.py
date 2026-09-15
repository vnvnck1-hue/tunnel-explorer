#!/usr/bin/env python3
"""Create a review board for landscape room depth, anchors, and open-floor clearance."""

from __future__ import annotations

import json
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / "art-production/test-room-v01/working/primary-match-v2"
LAYOUT = WORK / "curated-room-landscape-layouts.json"
OUT = WORK / "diorama-process-15-landscape-depth-clearance-review.png"

ROLE_COLORS = {
    "backdrop": (78, 184, 255),
    "hero": (255, 70, 205),
    "foreground": (255, 155, 46),
    "route": (248, 221, 93),
    "openFloor": (65, 235, 204),
}


def main() -> None:
    contract = json.loads(LAYOUT.read_text(encoding="utf-8"))
    board = Image.new("RGB", (1920, 1080), (13, 10, 19))
    draw = ImageDraw.Draw(board)
    draw.text((38, 24), "PROCESS 15  /  LANDSCAPE DEPTH + RUNTIME ANCHOR CLEARANCE", fill=(239, 225, 245))
    draw.text((38, 47), "cyan box = protected open floor; screen coordinates remain offline hypotheses", fill=(151, 138, 166))

    panel_x, image_width, image_height = 30, 560, 315
    for index, room in enumerate(contract["rooms"]):
        y = 82 + index * 330
        preview = Image.open(WORK / room["compositionImage"]).convert("RGB")
        preview = preview.resize((image_width, image_height), Image.Resampling.LANCZOS)
        board.paste(preview, (panel_x, y))
        x0, y0, x1, y1 = room["openFloorRectPixels"]
        sx0 = panel_x + round(x0 * image_width / 1920)
        sy0 = y + round(y0 * image_height / 1080)
        sx1 = panel_x + round(x1 * image_width / 1920)
        sy1 = y + round(y1 * image_height / 1080)
        draw.rectangle((sx0, sy0, sx1, sy1), outline=ROLE_COLORS["openFloor"], width=3)
        draw.rectangle((panel_x, y, panel_x + image_width, y + image_height), outline=(83, 68, 96), width=2)

        tx = 620
        draw.text((tx, y + 6), room["title"].upper(), fill=(232, 217, 240))
        draw.text((tx, y + 30), f"OPEN FLOOR  {(x1 - x0) / 19.2:.1f}% WIDTH  /  BLOCKING 0.0%", fill=ROLE_COLORS["openFloor"])
        for line, (role, anchor) in enumerate(room["runtimeAnchors"].items()):
            color = ROLE_COLORS.get(role, (180, 170, 191))
            draw.rectangle((tx, y + 68 + line * 42, tx + 12, y + 80 + line * 42), fill=color)
            draw.text((tx + 24, y + 64 + line * 42), role.upper(), fill=(195, 183, 206))
            draw.text((tx + 160, y + 64 + line * 42), anchor, fill=(142, 130, 155))
        draw.text((1330, y + 66), "UNITY MAPPING GATE", fill=(255, 184, 79))
        draw.text((1330, y + 94), "resolve anchors against current room topology", fill=(154, 141, 167))
        draw.text((1330, y + 116), "then verify F8/F9 sorting and collision", fill=(154, 141, 167))
        draw.text((1330, y + 138), "do not copy these screen coordinates", fill=(154, 141, 167))

    board.save(OUT, quality=95)
    print(OUT)


if __name__ == "__main__":
    main()
