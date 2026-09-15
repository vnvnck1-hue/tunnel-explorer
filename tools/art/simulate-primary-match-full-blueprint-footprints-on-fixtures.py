#!/usr/bin/env python3
"""Fit every non-ground room-blueprint prop into authoritative map fixtures."""

from __future__ import annotations

import importlib.util
import json
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / "art-production/test-room-v01/working/primary-match-v2"
FIXTURES = ROOT / "unity/TunnelCrew/Assets/Tests/EditMode/Fixtures"
LAYOUT = WORK / "curated-room-landscape-layouts.json"
RUNTIME = WORK / "runtime-catalog-candidates.json"
REPORT = WORK / "fixture-full-blueprint-footprint-simulation.json"
BOARD = WORK / "diorama-process-18-full-blueprint-footprint-fit-review.png"


def load_module(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(module)
    return module


base = load_module("primary_match_fixture_base_full", ROOT / "tools/art/simulate-primary-match-room-blueprints-on-fixtures.py")


def build_specs() -> dict[str, dict]:
    layout = json.loads(LAYOUT.read_text(encoding="utf-8"))
    runtime = json.loads(RUNTIME.read_text(encoding="utf-8"))
    by_cell = {
        (sheet["path"], variant["cell"]["column"], variant["cell"]["rowFromTop"]): variant
        for sheet in runtime["sheets"] for variant in sheet["variants"]
    }
    result = {}
    for room in layout["rooms"]:
        blocking, ground = [], []
        for index, placement in enumerate(room["placements"]):
            if placement["layer"] == "ground":
                ground.append(placement)
                continue
            source = placement["source"]
            if "monumental_wall" in source:
                role = "backdrop"
            elif "hero_machinery" in source:
                role = "hero"
            elif "foreground_depth" in source:
                role = "foreground"
            elif "service_pylons" in source:
                role = "service"
            else:
                role = "detail"
            col, row = placement["cell"]
            variant = by_cell.get((source, col, row))
            footprint = variant["runtimeCandidate"]["footprintCells"] if variant else [1, 1]
            variant_id = variant["assetId"] if variant else f"existing/{Path(source).stem}/{col}_{row}"
            center_x = placement["screen"][0] + placement["screen"][2] * 0.5
            edge = "west" if center_x < 640 else "east" if center_x > 1280 else "center"
            blocking.append({
                "instanceId": f"{room['id']}_{index:02d}",
                "role": role,
                "sourceVariantId": variant_id,
                "footprintCells": footprint,
                "edgePreference": edge,
            })
        result[room["id"]] = {
            "blocking": blocking,
            "groundOverlayCount": len(ground),
            "railPieces": sum("route" in placement for placement in ground),
        }
    return result


def occupied_for(anchor: tuple[int, int], side: str, width: int, height: int) -> set[tuple[int, int]]:
    c, r = anchor
    if side == "north":
        x0, y0 = c - width // 2, r - height + 1
    elif side == "south":
        x0, y0 = c - width // 2, r
    elif side == "east":
        x0, y0 = c - width + 1, r - height // 2
    else:
        x0, y0 = c, r - height // 2
    return {(x, y) for y in range(y0, y0 + height) for x in range(x0, x0 + width)}


def fit_all(data: dict, connected: list[bool], basic: dict, room: dict) -> dict | None:
    cols, rows = data["cols"], data["rows"]
    cells = data["cells"]
    vx, vy, vw, vh = basic["viewport"]
    rect = basic["openRect"]
    protected = {(c, r) for r in range(rect["y"], rect["y"] + rect["height"])
                 for c in range(rect["x"], rect["x"] + rect["width"])}

    def solid(c: int, r: int) -> bool:
        return not (0 <= c < cols and 0 <= r < rows) or cells[r * cols + c] != ""

    boundary: dict[str, list[tuple[int, int]]] = {side: [] for side in ("north", "south", "west", "east")}
    for r in range(vy, vy + vh):
        for c in range(vx, vx + vw):
            if not connected[r * cols + c]:
                continue
            if solid(c, r + 1): boundary["north"].append((c, r))
            if solid(c, r - 1): boundary["south"].append((c, r))
            if solid(c - 1, r): boundary["west"].append((c, r))
            if solid(c + 1, r): boundary["east"].append((c, r))

    candidates = []
    for spec in room["blocking"]:
        if spec["role"] == "backdrop":
            sides = ["north"]
        elif spec["role"] == "foreground":
            sides = ["south"]
        elif spec["edgePreference"] in {"west", "east"}:
            sides = [spec["edgePreference"]]
        else:
            sides = ["north", "west", "east", "south"]
        width, height = spec["footprintCells"]
        options = []
        for side in sides:
            for anchor in boundary[side]:
                occupied = occupied_for(anchor, side, width, height)
                if occupied & protected:
                    continue
                if any(not (vx <= c < vx + vw and vy <= r < vy + vh) for c, r in occupied):
                    continue
                if any(not connected[r * cols + c] for c, r in occupied):
                    continue
                options.append({**spec, "boundarySide": side, "anchorCell": list(anchor),
                                "occupiedCells": [list(cell) for cell in sorted(occupied)]})
        if not options:
            return None
        candidates.append(options[:48])

    order = sorted(range(len(candidates)), key=lambda i: (len(candidates[i]),
                                                           -room["blocking"][i]["footprintCells"][0]
                                                           * room["blocking"][i]["footprintCells"][1]))
    selected: list[dict | None] = [None] * len(candidates)

    def search(position: int, used: set[tuple[int, int]]) -> bool:
        if position == len(order):
            return True
        index = order[position]
        for option in candidates[index]:
            occupied = {tuple(cell) for cell in option["occupiedCells"]}
            if used & occupied:
                continue
            selected[index] = option
            if search(position + 1, used | occupied):
                return True
        selected[index] = None
        return False

    if not search(0, set()):
        return None

    route_cells = []
    if room["railPieces"]:
        for r in range(rect["y"], rect["y"] + rect["height"]):
            for c in range(rect["x"], rect["x"] + rect["width"] - room["railPieces"] + 1):
                candidate = [(c + offset, r) for offset in range(room["railPieces"])]
                if all(connected[rr * cols + cc] for cc, rr in candidate):
                    route_cells = [list(cell) for cell in candidate]
                    break
            if route_cells:
                break
        if not route_cells:
            return None
    placements = [item for item in selected if item is not None]
    return {
        "blockingPlacements": placements,
        "blockingPlacementCount": len(placements),
        "blockingOccupiedCells": sum(len(item["occupiedCells"]) for item in placements),
        "groundOverlayCount": room["groundOverlayCount"],
        "railRouteCells": route_cells,
    }


def best_fit(data: dict, connected: list[bool], room_id: str, room: dict) -> dict:
    basic_candidates = []
    for y in range(data["rows"] - base.VIEW_H + 1):
        for x in range(data["cols"] - base.VIEW_W + 1):
            basic = base.inspect_window(data, connected, x, y, room_id)
            if basic["feasible"]:
                basic_candidates.append(basic)
    basic_candidates.sort(key=lambda item: item["score"], reverse=True)
    for basic in basic_candidates:
        fit = fit_all(data, connected, basic, room)
        if fit is not None:
            return {**basic, "fullBlueprintFit": fit, "fullBlockingFootprintsFeasible": True}
    return {"fullBlockingFootprintsFeasible": False}


def draw_board(results: list[dict], fixtures: list[dict]) -> None:
    board = Image.new("RGB", (1920, 1080), (13, 10, 19))
    draw = ImageDraw.Draw(board)
    draw.text((38, 24), "PROCESS 18  /  FULL NON-GROUND BLUEPRINT FOOTPRINT FIT", fill=(239, 225, 245))
    draw.text((38, 48), "major props + service pylons + medium crystals; yellow rail stays a non-blocking ground route", fill=(151, 138, 166))
    colors = {"solid": (44, 24, 54), "open": (86, 63, 98), "openRect": (59, 236, 207),
              "backdrop": (78, 184, 255), "hero": (255, 70, 205), "foreground": (255, 155, 46),
              "service": (120, 224, 255), "detail": (204, 119, 238), "route": (248, 221, 93)}
    for index, (result, data) in enumerate(zip(results, fixtures)):
        panel_x, panel_y = 30 + index * 630, 90
        panel_w, panel_h = 600, 540
        cols, rows = data["cols"], data["rows"]
        cw, ch = panel_w / cols, panel_h / rows
        connected = base.connected_open(data)
        for r in range(rows):
            for c in range(cols):
                color = colors["open"] if connected[r * cols + c] else colors["solid"]
                draw.rectangle((panel_x+round(c*cw), panel_y+round((rows-r-1)*ch),
                                panel_x+round((c+1)*cw), panel_y+round((rows-r)*ch)), fill=color)
        chosen = result["blueprints"][result["assignedRoom"]]
        vx, vy, vw, vh = chosen["viewport"]
        draw.rectangle((panel_x+round(vx*cw), panel_y+round((rows-vy-vh)*ch),
                        panel_x+round((vx+vw)*cw), panel_y+round((rows-vy)*ch)), outline=(225,212,236), width=2)
        rect = chosen["openRect"]
        draw.rectangle((panel_x+round(rect["x"]*cw), panel_y+round((rows-rect["y"]-rect["height"])*ch),
                        panel_x+round((rect["x"]+rect["width"])*cw), panel_y+round((rows-rect["y"])*ch)),
                       outline=colors["openRect"], width=3)
        fit = chosen["fullBlueprintFit"]
        for placement in fit["blockingPlacements"]:
            for c, r in placement["occupiedCells"]:
                draw.rectangle((panel_x+round(c*cw), panel_y+round((rows-r-1)*ch),
                                panel_x+round((c+1)*cw), panel_y+round((rows-r)*ch)),
                               fill=colors[placement["role"]], outline=(15,10,20))
        for c, r in fit["railRouteCells"]:
            draw.rectangle((panel_x+round(c*cw), panel_y+round((rows-r-1)*ch),
                            panel_x+round((c+1)*cw), panel_y+round((rows-r)*ch)), fill=colors["route"])
        draw.rectangle((panel_x, panel_y, panel_x+panel_w, panel_y+panel_h), outline=(95,76,108), width=2)
        y = 655
        draw.text((panel_x, y), f"DEPTH {data['depth']}  /  {result['assignedRoom'].replace('_',' ').upper()}", fill=(232,217,240))
        draw.text((panel_x, y+28), f"{fit['blockingPlacementCount']} BLOCKING PROPS / {fit['blockingOccupiedCells']} CELLS / {fit['groundOverlayCount']} GROUND OVERLAYS", fill=colors["openRect"])
        draw.text((panel_x, y+56), "PASS — FULL NON-GROUND SET FITS WITHOUT OVERLAP", fill=(87,234,175))
        role_counts = {}
        for placement in fit["blockingPlacements"]:
            role_counts[placement["role"]] = role_counts.get(placement["role"], 0) + 1
        for line, role in enumerate(("backdrop", "hero", "foreground", "service", "detail")):
            draw.text((panel_x, y+104+line*34), role.upper(), fill=colors[role])
            draw.text((panel_x+140, y+104+line*34), str(role_counts.get(role, 0)), fill=(178,164,190))
    board.save(BOARD, quality=95)


def main() -> None:
    specs = build_specs()
    fixture_paths = sorted(FIXTURES.glob("map-*.json"))
    fixtures = [json.loads(path.read_text(encoding="utf-8")) for path in fixture_paths]
    assignments = {1: "ore_intake", 2: "ventilation_service", 3: "crystal_power"}
    results = []
    for path, data in zip(fixture_paths, fixtures):
        connected = base.connected_open(data)
        evaluations = {room_id: best_fit(data, connected, room_id, room) for room_id, room in specs.items()}
        results.append({
            "fixture": str(path.relative_to(ROOT)).replace("\\", "/"),
            "fixtureSha256": base.sha256(path),
            "depth": data["depth"],
            "assignedRoom": assignments[data["depth"]],
            "blueprints": evaluations,
        })
    fits = sum(candidate["fullBlockingFootprintsFeasible"] for result in results for candidate in result["blueprints"].values())
    assigned = sum(result["blueprints"][result["assignedRoom"]]["fullBlockingFootprintsFeasible"] for result in results)
    report = {
        "status": "verified_offline_full_blueprint_footprints",
        "unityExecuted": False,
        "groundOverlaysAreNonBlocking": True,
        "summary": {
            "fixtureCount": 3,
            "blueprintCount": 3,
            "evaluations": 9,
            "fullBlueprintFits": fits,
            "assignedRoomFits": assigned,
        },
        "fixtures": results,
    }
    if fits != 9 or assigned != 3:
        raise AssertionError(f"full blueprint footprint feasibility regressed: {report['summary']}")
    REPORT.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    draw_board(results, fixtures)
    print(f"Wrote {REPORT.relative_to(ROOT)}")
    print(f"Wrote {BOARD.relative_to(ROOT)}")
    print(report["summary"])


if __name__ == "__main__":
    main()
