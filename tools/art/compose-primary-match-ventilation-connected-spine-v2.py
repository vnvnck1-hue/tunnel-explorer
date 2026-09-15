#!/usr/bin/env python3
"""Compose a ventilation-service variant with one connected heavy-pipe spine."""

from __future__ import annotations

import importlib.util
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance, ImageFont


ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / "art-production/test-room-v01/working/primary-match-v2"
BASE_COMPOSER_PATH = ROOT / "tools/art/compose-primary-match-landscape-room-variants.py"
LAYOUT = WORK / "curated-room-landscape-layouts.json"
PORTS = WORK / "connection-port-candidates.json"
OUT_IMAGE = WORK / "diorama-process-34-ventilation-connected-spine-v2.png"
OUT_COMPARE = WORK / "diorama-process-35-ventilation-spine-comparison.png"
OUT_CONTRACT = WORK / "ventilation-connected-spine-v2.json"


def load_composer():
    spec = importlib.util.spec_from_file_location("primary_match_landscape_composer", BASE_COMPOSER_PATH)
    if spec is None or spec.loader is None:
        raise RuntimeError("cannot load base landscape composer")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


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


def horizontal_overlap(a: list[int], b: list[int]) -> int:
    return max(0, min(a[0] + a[2], b[0] + b[2]) - max(a[0], b[0]))


def main() -> None:
    composer = load_composer()
    layout = json.loads(LAYOUT.read_text(encoding="utf-8"))
    port_contract = json.loads(PORTS.read_text(encoding="utf-8"))
    room = next(entry for entry in layout["rooms"] if entry["id"] == "ventilation_service")
    removed = [
        dict(entry) for entry in room["placements"]
        if entry["source"].startswith("tr01_primarymatch_equipment_transitions") and entry["layer"] == "ground"
    ]
    placements = [
        dict(entry) for entry in room["placements"]
        if not (entry["source"].startswith("tr01_primarymatch_equipment_transitions") and entry["layer"] == "ground")
    ]

    spine_source = "tr01_primarymatch_equipment_transitions_3x3_source.png"
    spine_screens = [[355 + index * 200, 610, 240] for index in range(6)]
    spine_cells = [[0, 0], [1, 0], [1, 0], [1, 0], [1, 0], [2, 0]]
    for cell, screen in zip(spine_cells, spine_screens):
        placements.append({
            "source": spine_source,
            "grid": [3, 3],
            "cell": cell,
            "screen": screen,
            "layer": "ground",
            "route": "ventilation_service_spine",
        })

    layer_order = {"ground": 0, "background": 1, "midground": 2, "foreground": 3}
    canvas = composer.room_base(room["accent"])
    cache = {}
    for entry in sorted(placements, key=lambda item: layer_order[item["layer"]]):
        source_name = entry["source"]
        if source_name not in cache:
            cache[source_name] = Image.open(WORK / source_name).convert("RGBA")
        cols, rows = entry["grid"]
        col, row = entry["cell"]
        composer.place(canvas, composer.cell(cache[source_name], col, row, cols, rows), *entry["screen"], entry["layer"])
    result = ImageEnhance.Color(canvas.convert("RGB")).enhance(1.035)
    result = ImageEnhance.Contrast(result).enhance(1.025)
    result.save(OUT_IMAGE, quality=95)

    spine_assets = [
        "TR01-PM-EQUIPMENT-LEFT-TERMINATION",
        *(["TR01-PM-EQUIPMENT-BURIED-THRESHOLD"] * 4),
        "TR01-PM-EQUIPMENT-RIGHT-TERMINATION",
    ]
    ports_by_delivery = {entry["deliveryAssetId"]: entry for entry in port_contract["assets"]}
    edges = [[port["edge"] for port in ports_by_delivery[asset_id]["connectionPorts"]] for asset_id in spine_assets]
    expected_edges = [["east"], *([["west", "east"]] * 4), ["west"]]
    if edges != expected_edges:
        raise ValueError("ventilation spine port sequence changed")

    hero = next(entry for entry in placements if entry["source"].startswith("tr01_primarymatch_hero_machinery"))
    pylons = [entry for entry in placements if entry["source"].startswith("tr01_primarymatch_service_pylons")]
    if len(pylons) != 2:
        raise ValueError("expected exactly two ventilation service pylons")
    route_screen = [spine_screens[0][0], spine_screens[0][1], spine_screens[-1][0] + spine_screens[-1][2] - spine_screens[0][0]]
    hero_overlap = horizontal_overlap(hero["screen"], route_screen)
    pylon_overlaps = [horizontal_overlap(pylon["screen"], route_screen) for pylon in pylons]
    route_bottom = spine_screens[0][1] + spine_screens[0][2]
    protected_floor_top = room["openFloorRectPixels"][1]
    protected_floor_clearance = protected_floor_top - route_bottom
    if min(hero_overlap, *pylon_overlaps, protected_floor_clearance) <= 0:
        raise ValueError("spine must overlap hero and both pylons while clearing protected floor")

    contract = {
        "status": "offline_composition_candidate_not_runtime_integrated",
        "unityExecuted": False,
        "sourceRoom": "ventilation_service",
        "sourceComposition": room["compositionImage"],
        "compositionImage": OUT_IMAGE.name,
        "comparisonImage": OUT_COMPARE.name,
        "change": "two detached equipment details replaced by one six-piece pylon-to-turbine service spine",
        "route": {
            "id": "ventilation_service_spine",
            "runtimeAnchor": "west_service_through_turbine_to_east_service_connection",
            "assetIds": spine_assets,
            "screens": spine_screens,
            "portEdges": edges,
            "portType": "heavy_pipe",
            "internalConnectionsCompatible": True,
            "heroScreenOverlapPixels": hero_overlap,
            "leftPylonScreenOverlapPixels": pylon_overlaps[0],
            "rightPylonScreenOverlapPixels": pylon_overlaps[1],
            "protectedFloorClearancePixels": protected_floor_clearance,
            "groundOverlayNonBlocking": True,
        },
        "removedGroundOverlays": removed,
        "openFloorRectPixels": room["openFloorRectPixels"],
        "nonGroundPlacementsChanged": False,
        "runtimeTopologyResolutionRequired": True,
    }
    OUT_CONTRACT.write_text(json.dumps(contract, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    board = Image.new("RGB", (1920, 1080), (12, 8, 18))
    draw = ImageDraw.Draw(board)
    draw.text((36, 20), "PROCESS 35  /  VENTILATION SERVICE-SPINE CONNECTION", font=font(24, True), fill=(244, 228, 247))
    draw.text((36, 52), "V2 turns three detached silhouettes into one readable service system", font=font(14), fill=(164, 147, 175))
    original = cover(Image.open(WORK / room["compositionImage"]).convert("RGB"), (900, 506))
    revised = cover(result, (900, 506))
    board.paste(original, (30, 105))
    board.paste(revised, (990, 105))
    draw.rectangle((30, 105, 930, 611), outline=(88, 66, 104), width=2)
    draw.rectangle((990, 105, 1890, 611), outline=(57, 226, 207), width=3)
    draw.text((30, 628), "V1  /  DETACHED SERVICE PROPS", font=font(18, True), fill=(205, 188, 214))
    draw.text((990, 628), "V2  /  PYLON-TO-TURBINE SPINE", font=font(18, True), fill=(76, 232, 212))
    draw.text((30, 662), "two small floor sockets stop before reaching the turbine", font=font(14), fill=(154, 139, 166))
    draw.text((990, 662), f"pylon overlaps {pylon_overlaps[0]}px / {pylon_overlaps[1]}px  ·  turbine overlap {hero_overlap}px", font=font(14), fill=(154, 139, 166))
    draw.line((1185, 720, 1690, 720), fill=(62, 219, 232), width=8)
    draw.ellipse((1173, 708, 1197, 732), fill=(62, 219, 232))
    draw.ellipse((1678, 708, 1702, 732), fill=(62, 219, 232))
    draw.text((990, 748), "EAST  ->  4 × WEST/EAST  ->  WEST", font=font(15, True), fill=(62, 219, 232))
    draw.text((990, 782), "6 connected heavy-pipe overlays  /  non-blocking", font=font(14), fill=(202, 186, 212))
    draw.text((990, 812), f"protected combat floor starts {protected_floor_clearance}px below the route", font=font(14), fill=(202, 186, 212))
    draw.text((990, 842), "runtime anchor still requires current topology resolution", font=font(14), fill=(255, 157, 67))
    board.save(OUT_COMPARE, quality=95)
    print(f"Wrote {OUT_IMAGE.relative_to(ROOT)}")
    print(f"Wrote {OUT_CONTRACT.relative_to(ROOT)}")
    print(f"Wrote {OUT_COMPARE.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
