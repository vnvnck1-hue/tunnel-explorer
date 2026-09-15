#!/usr/bin/env python3
"""Build candidate edge-port metadata for rail and equipment transition tiles."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / "art-production/test-room-v01/working/primary-match-v2"
RUNTIME = WORK / "runtime-catalog-candidates.json"
DELIVERY = WORK / "delivery-candidates/manifest.json"
OUT = WORK / "connection-port-candidates.json"
REVIEW = WORK / "diorama-process-29-connection-port-review.png"

SHEETS = [
    {
        "assetId": "TR01-PRIMARYMATCH-FLOOR-RAIL-TRANSITIONS-3X3",
        "title": "RAIL TRANSITIONS",
        "path": "tr01_primarymatch_floor_rail_transitions_3x3_source.png",
        "ports": {
            "horizontal_left": [("east", "rail_pair")],
            "horizontal_middle": [("west", "rail_pair"), ("east", "rail_pair")],
            "horizontal_right": [("west", "rail_pair")],
            "turn_up_left": [("north", "rail_pair"), ("east", "rail_pair")],
            "reinforced_cross": [("north", "rail_pair"), ("east", "rail_pair"), ("south", "rail_pair"), ("west", "rail_pair")],
            "turn_up_right": [("north", "rail_pair"), ("west", "rail_pair")],
            "damaged_left": [("east", "rail_pair")],
            "broken_gap": [("west", "rail_pair"), ("east", "rail_pair")],
            "damaged_right": [("west", "rail_pair")],
        },
        "continuity": {"broken_gap": "broken"},
    },
    {
        "assetId": "TR01-PRIMARYMATCH-EQUIPMENT-TRANSITIONS-3X3",
        "title": "EQUIPMENT TRANSITIONS",
        "path": "tr01_primarymatch_equipment_transitions_3x3_source.png",
        "ports": {
            "left_termination": [("east", "heavy_pipe")],
            "buried_threshold": [("west", "heavy_pipe"), ("east", "heavy_pipe")],
            "right_termination": [("west", "heavy_pipe")],
            "left_broken_corner": [("east", "equipment_socket")],
            "reinforced_socket": [("north", "dual_service")],
            "right_broken_corner": [("west", "equipment_socket")],
            "left_rubble_ramp": [("east", "power_bundle")],
            "capped_machine_scar": [("west", "power_bundle"), ("east", "power_bundle")],
            "right_rubble_ramp": [("west", "power_bundle")],
        },
        "continuity": {"capped_machine_scar": "broken"},
    },
]

EDGE_PIXEL = {
    "north": [64, 0],
    "east": [128, 64],
    "south": [64, 128],
    "west": [0, 64],
}
EDGE_NORMALIZED = {
    "north": [0.5, 0.0],
    "east": [1.0, 0.5],
    "south": [0.5, 1.0],
    "west": [0.0, 0.5],
}
TYPE_COLOR = {
    "rail_pair": (250, 211, 74),
    "heavy_pipe": (62, 219, 232),
    "power_bundle": (242, 74, 206),
    "equipment_socket": (255, 143, 48),
    "dual_service": (146, 108, 255),
}


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    face = "C:/Windows/Fonts/seguisb.ttf" if bold else "C:/Windows/Fonts/segoeui.ttf"
    try:
        return ImageFont.truetype(face, size)
    except OSError:
        return ImageFont.load_default()


def main() -> None:
    runtime = json.loads(RUNTIME.read_text(encoding="utf-8"))
    delivery = json.loads(DELIVERY.read_text(encoding="utf-8"))
    delivery_by_source = {asset["sourceVariantId"]: asset["assetId"] for asset in delivery["assets"]}
    sheet_by_id = {sheet["assetId"]: sheet for sheet in runtime["sheets"]}
    entries = []
    for definition in SHEETS:
        sheet = sheet_by_id[definition["assetId"]]
        source_path = WORK / definition["path"]
        for variant in sheet["variants"]:
            name = variant["assetId"].split("/")[-1]
            ports = [{
                "edge": edge,
                "pixel128": EDGE_PIXEL[edge],
                "normalizedTopLeft": EDGE_NORMALIZED[edge],
                "type": port_type,
            } for edge, port_type in definition["ports"][name]]
            entries.append({
                "assetId": variant["assetId"],
                "deliveryAssetId": delivery_by_source[variant["assetId"]],
                "sourceSheet": definition["path"],
                "sourceSheetSha256": sha256(source_path),
                "cell": variant["cell"],
                "connectionPorts": ports,
                "internalContinuity": definition["continuity"].get(name, "connected"),
                "topologyResolutionRequired": True,
                "status": "candidate_not_unity_verified",
            })

    contract = {
        "status": "offline_candidate_not_unity_integrated",
        "unityExecuted": False,
        "deliveryPixelsPerCell": 128,
        "allowedEdges": ["north", "east", "south", "west"],
        "allowedTypes": list(TYPE_COLOR),
        "rules": [
            "ports describe logical boundary connections, not visible alpha touching the canvas edge",
            "broken pieces may expose external ports while internalContinuity remains broken",
            "placement must resolve compatible opposite-edge ports against current room topology",
            "do not spawn isolated route fragments when no compatible neighbor or machine socket exists",
        ],
        "assets": entries,
        "summary": {
            "assetCount": len(entries),
            "portCount": sum(len(entry["connectionPorts"]) for entry in entries),
            "brokenContinuityAssets": sum(entry["internalContinuity"] == "broken" for entry in entries),
            "reviewImage": REVIEW.name,
        },
    }
    OUT.write_text(json.dumps(contract, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    board = Image.new("RGB", (1920, 1080), (12, 8, 18))
    draw = ImageDraw.Draw(board)
    draw.text((36, 20), "PROCESS 29  /  ROUTE + EQUIPMENT CONNECTION PORTS", font=font(24, True), fill=(244, 228, 247))
    draw.text((36, 52), "logical 128px cell-edge sockets  |  arrows point outward  |  topology resolution still required", font=font(14), fill=(164, 147, 175))
    for sheet_index, definition in enumerate(SHEETS):
        origin_x = 30 + sheet_index * 950
        origin_y = 100
        preview_size = 850
        source = Image.open(WORK / definition["path"]).convert("RGBA").resize((preview_size, preview_size), Image.Resampling.LANCZOS)
        board.paste(source.convert("RGB"), (origin_x, origin_y))
        draw.rectangle((origin_x, origin_y, origin_x + preview_size, origin_y + preview_size), outline=(91, 67, 109), width=2)
        cell_size = preview_size / 3
        sheet = sheet_by_id[definition["assetId"]]
        for variant in sheet["variants"]:
            col = variant["cell"]["column"]
            row = variant["cell"]["rowFromTop"]
            name = variant["assetId"].split("/")[-1]
            cx = origin_x + (col + 0.5) * cell_size
            cy = origin_y + (row + 0.5) * cell_size
            for edge, port_type in definition["ports"][name]:
                color = TYPE_COLOR[port_type]
                if edge == "north": start, end = (cx, origin_y + row * cell_size + 28), (cx, origin_y + row * cell_size - 4)
                elif edge == "south": start, end = (cx, origin_y + (row + 1) * cell_size - 28), (cx, origin_y + (row + 1) * cell_size + 4)
                elif edge == "west": start, end = (origin_x + col * cell_size + 28, cy), (origin_x + col * cell_size - 4, cy)
                else: start, end = (origin_x + (col + 1) * cell_size - 28, cy), (origin_x + (col + 1) * cell_size + 4, cy)
                draw.line((start, end), fill=color, width=7)
                ex, ey = end
                draw.ellipse((ex - 7, ey - 7, ex + 7, ey + 7), fill=color)
            draw.text((origin_x + col * cell_size + 10, origin_y + row * cell_size + 8), name.replace("_", " ").upper(), font=font(10, True), fill=(235, 221, 240))
            if definition["continuity"].get(name) == "broken":
                draw.text((cx - 33, cy - 8), "BROKEN", font=font(11, True), fill=(255, 93, 93))
        draw.text((origin_x, 970), definition["title"], font=font(18, True), fill=(236, 220, 242))
    legend_x = 650
    for index, (port_type, color) in enumerate(TYPE_COLOR.items()):
        x = legend_x + index * 235
        draw.rectangle((x, 1012, x + 14, 1026), fill=color)
        draw.text((x + 22, 1008), port_type.upper(), font=font(11, True), fill=(182, 166, 193))
    board.save(REVIEW, quality=95)
    print(f"Wrote {OUT.relative_to(ROOT)}: {contract['summary']}")
    print(f"Wrote {REVIEW.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
