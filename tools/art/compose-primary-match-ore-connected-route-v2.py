#!/usr/bin/env python3
"""Compose an ore-intake variant whose rail visibly originates under the crusher."""

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
OUT_IMAGE = WORK / "diorama-process-30-ore-intake-connected-route-v2.png"
OUT_COMPARE = WORK / "diorama-process-31-ore-intake-route-comparison.png"
OUT_CONTRACT = WORK / "ore-intake-connected-route-v2.json"


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
    ore = next(room for room in layout["rooms"] if room["id"] == "ore_intake")
    placements = []
    route_x = [500, 710, 920]
    route_index = 0
    removed = []
    for source in ore["placements"]:
        entry = dict(source)
        if entry["source"].startswith("tr01_primarymatch_equipment_transitions") and entry["layer"] == "ground":
            removed.append(entry)
            continue
        if entry.get("route") == "ore_dock":
            entry["screen"] = [route_x[route_index], 575, 250]
            route_index += 1
        placements.append(entry)
    if route_index != 3:
        raise ValueError("expected exactly three ore_dock rail pieces")

    layer_order = {"ground": 0, "background": 1, "midground": 2, "foreground": 3}
    canvas = composer.room_base(ore["accent"])
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

    rail_assets = [
        "TR01-PM-RAIL-HORIZONTAL-LEFT",
        "TR01-PM-RAIL-HORIZONTAL-MIDDLE",
        "TR01-PM-RAIL-HORIZONTAL-RIGHT",
    ]
    ports_by_delivery = {entry["deliveryAssetId"]: entry for entry in port_contract["assets"]}
    edges = [[port["edge"] for port in ports_by_delivery[asset_id]["connectionPorts"]] for asset_id in rail_assets]
    if edges != [["east"], ["west", "east"], ["west"]]:
        raise ValueError("connected route port sequence changed")
    hero = next(entry for entry in placements if entry["source"].startswith("tr01_primarymatch_hero_machinery"))
    hero_left, _, hero_width = hero["screen"]
    route_start, _, route_width = next(entry for entry in placements if entry.get("route") == "ore_dock")["screen"]
    overlap = max(0, min(hero_left + hero_width, route_start + route_width) - max(hero_left, route_start))
    contract = {
        "status": "offline_composition_candidate_not_runtime_integrated",
        "unityExecuted": False,
        "sourceRoom": "ore_intake",
        "sourceComposition": ore["compositionImage"],
        "compositionImage": OUT_IMAGE.name,
        "comparisonImage": OUT_COMPARE.name,
        "change": "rail route shifted under crusher base and redundant equipment scar removed",
        "route": {
            "id": "ore_dock",
            "assetIds": rail_assets,
            "screens": [entry["screen"] for entry in placements if entry.get("route") == "ore_dock"],
            "portEdges": edges,
            "internalConnectionsCompatible": True,
            "crusherScreenOverlapPixels": overlap,
            "groundOverlayNonBlocking": True,
        },
        "removedGroundOverlays": removed,
        "openFloorRectPixels": ore["openFloorRectPixels"],
        "nonGroundPlacementsChanged": False,
        "runtimeTopologyResolutionRequired": True,
    }
    OUT_CONTRACT.write_text(json.dumps(contract, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    board = Image.new("RGB", (1920, 1080), (12, 8, 18))
    draw = ImageDraw.Draw(board)
    draw.text((36, 20), "PROCESS 31  /  ORE-INTAKE ROUTE CONNECTION", font=font(24, True), fill=(244, 228, 247))
    draw.text((36, 52), "V2 starts the rail beneath the crusher base; all non-ground placement stays unchanged", font=font(14), fill=(164, 147, 175))
    original = cover(Image.open(WORK / ore["compositionImage"]).convert("RGB"), (900, 506))
    revised = cover(result, (900, 506))
    board.paste(original, (30, 105))
    board.paste(revised, (990, 105))
    draw.rectangle((30, 105, 930, 611), outline=(88, 66, 104), width=2)
    draw.rectangle((990, 105, 1890, 611), outline=(57, 226, 207), width=3)
    draw.text((30, 628), "V1  /  SEPARATE ORNAMENTAL ROUTE", font=font(18, True), fill=(205, 188, 214))
    draw.text((990, 628), "V2  /  CRUSHER-ORIGIN ROUTE", font=font(18, True), fill=(76, 232, 212))
    draw.text((30, 662), "rail begins after a detached equipment scar", font=font(14), fill=(154, 139, 166))
    draw.text((990, 662), f"rail overlaps crusher screen footprint by {overlap}px", font=font(14), fill=(154, 139, 166))
    draw.line((1250, 720, 1610, 720), fill=(249, 211, 74), width=8)
    draw.ellipse((1238, 708, 1262, 732), fill=(249, 211, 74))
    draw.ellipse((1598, 708, 1622, 732), fill=(249, 211, 74))
    draw.text((990, 748), "EAST  ->  WEST/EAST  ->  WEST", font=font(15, True), fill=(249, 211, 74))
    draw.text((990, 782), "3 connected ground overlays  /  non-blocking", font=font(14), fill=(202, 186, 212))
    draw.text((990, 812), "protected floor and all major footprints unchanged", font=font(14), fill=(202, 186, 212))
    draw.text((990, 842), "runtime anchor still requires current topology resolution", font=font(14), fill=(255, 157, 67))
    board.save(OUT_COMPARE, quality=95)
    print(f"Wrote {OUT_IMAGE.relative_to(ROOT)}")
    print(f"Wrote {OUT_CONTRACT.relative_to(ROOT)}")
    print(f"Wrote {OUT_COMPARE.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
