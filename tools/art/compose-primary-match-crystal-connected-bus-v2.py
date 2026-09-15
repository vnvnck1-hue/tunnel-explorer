#!/usr/bin/env python3
"""Compose a crystal-power variant with a connected three-piece heavy-pipe bus."""

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
OUT_IMAGE = WORK / "diorama-process-32-crystal-power-connected-bus-v2.png"
OUT_COMPARE = WORK / "diorama-process-33-crystal-power-bus-comparison.png"
OUT_CONTRACT = WORK / "crystal-power-connected-bus-v2.json"


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


def main() -> None:
    composer = load_composer()
    layout = json.loads(LAYOUT.read_text(encoding="utf-8"))
    port_contract = json.loads(PORTS.read_text(encoding="utf-8"))
    room = next(entry for entry in layout["rooms"] if entry["id"] == "crystal_power")
    removed = [
        dict(entry) for entry in room["placements"]
        if entry["source"].startswith("tr01_primarymatch_equipment_transitions") and entry["layer"] == "ground"
    ]
    placements = [
        dict(entry) for entry in room["placements"]
        if not (entry["source"].startswith("tr01_primarymatch_equipment_transitions") and entry["layer"] == "ground")
    ]
    bus_source = "tr01_primarymatch_equipment_transitions_3x3_source.png"
    bus_screens = [[520, 610, 240], [720, 610, 240], [920, 610, 240]]
    for col, screen in enumerate(bus_screens):
        placements.append({
            "source": bus_source,
            "grid": [3, 3],
            "cell": [col, 0],
            "screen": screen,
            "layer": "ground",
            "route": "power_bus",
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

    bus_assets = [
        "TR01-PM-EQUIPMENT-LEFT-TERMINATION",
        "TR01-PM-EQUIPMENT-BURIED-THRESHOLD",
        "TR01-PM-EQUIPMENT-RIGHT-TERMINATION",
    ]
    ports_by_delivery = {entry["deliveryAssetId"]: entry for entry in port_contract["assets"]}
    edges = [[port["edge"] for port in ports_by_delivery[asset_id]["connectionPorts"]] for asset_id in bus_assets]
    if edges != [["east"], ["west", "east"], ["west"]]:
        raise ValueError("power-bus port sequence changed")
    hero = next(entry for entry in placements if entry["source"].startswith("tr01_primarymatch_hero_machinery"))
    backdrop = next(entry for entry in placements if entry["source"].startswith("tr01_primarymatch_monumental_wall_modules"))
    bus_left, _, bus_width = bus_screens[0]
    bus_right = bus_screens[-1][0] + bus_screens[-1][2]
    hero_left, _, hero_width = hero["screen"]
    backdrop_left, _, backdrop_width = backdrop["screen"]
    hero_overlap = max(0, min(hero_left + hero_width, bus_left + bus_width) - max(hero_left, bus_left))
    backdrop_overlap = max(0, min(backdrop_left + backdrop_width, bus_right) - max(backdrop_left, bus_left))
    contract = {
        "status": "offline_composition_candidate_not_runtime_integrated",
        "unityExecuted": False,
        "sourceRoom": "crystal_power",
        "sourceComposition": room["compositionImage"],
        "compositionImage": OUT_IMAGE.name,
        "comparisonImage": OUT_COMPARE.name,
        "change": "two detached equipment details replaced by a connected heavy-pipe bus",
        "route": {
            "id": "power_bus",
            "runtimeAnchor": "west_relay_to_north_processor_connection",
            "assetIds": bus_assets,
            "screens": bus_screens,
            "portEdges": edges,
            "portType": "heavy_pipe",
            "internalConnectionsCompatible": True,
            "heroScreenOverlapPixels": hero_overlap,
            "backdropScreenOverlapPixels": backdrop_overlap,
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
    draw.text((36, 20), "PROCESS 33  /  CRYSTAL-POWER BUS CONNECTION", font=font(24, True), fill=(244, 228, 247))
    draw.text((36, 52), "V2 replaces detached floor details with one readable heavy-pipe axis", font=font(14), fill=(164, 147, 175))
    original = cover(Image.open(WORK / room["compositionImage"]).convert("RGB"), (900, 506))
    revised = cover(result, (900, 506))
    board.paste(original, (30, 105))
    board.paste(revised, (990, 105))
    draw.rectangle((30, 105, 930, 611), outline=(88, 66, 104), width=2)
    draw.rectangle((990, 105, 1890, 611), outline=(57, 226, 207), width=3)
    draw.text((30, 628), "V1  /  DETACHED FLOOR DETAILS", font=font(18, True), fill=(205, 188, 214))
    draw.text((990, 628), "V2  /  RELAY-TO-PROCESSOR BUS", font=font(18, True), fill=(76, 232, 212))
    draw.text((30, 662), "two small props compete without a shared direction", font=font(14), fill=(154, 139, 166))
    draw.text((990, 662), f"hero overlap {hero_overlap}px  /  backdrop overlap {backdrop_overlap}px", font=font(14), fill=(154, 139, 166))
    draw.line((1250, 720, 1610, 720), fill=(62, 219, 232), width=8)
    draw.ellipse((1238, 708, 1262, 732), fill=(62, 219, 232))
    draw.ellipse((1598, 708, 1622, 732), fill=(62, 219, 232))
    draw.text((990, 748), "EAST  ->  WEST/EAST  ->  WEST", font=font(15, True), fill=(62, 219, 232))
    draw.text((990, 782), "3 connected heavy-pipe overlays  /  non-blocking", font=font(14), fill=(202, 186, 212))
    draw.text((990, 812), "all major silhouettes and protected floor unchanged", font=font(14), fill=(202, 186, 212))
    draw.text((990, 842), "runtime anchor still requires current topology resolution", font=font(14), fill=(255, 157, 67))
    board.save(OUT_COMPARE, quality=95)
    print(f"Wrote {OUT_IMAGE.relative_to(ROOT)}")
    print(f"Wrote {OUT_CONTRACT.relative_to(ROOT)}")
    print(f"Wrote {OUT_COMPARE.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
