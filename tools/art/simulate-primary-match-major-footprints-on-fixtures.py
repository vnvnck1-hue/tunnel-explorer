#!/usr/bin/env python3
"""Fit exact major-prop footprints into map fixture viewports without Unity."""

from __future__ import annotations

import importlib.util
import json
from itertools import product
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / "art-production/test-room-v01/working/primary-match-v2"
FIXTURES = ROOT / "unity/TunnelCrew/Assets/Tests/EditMode/Fixtures"
LAYOUT = WORK / "curated-room-landscape-layouts.json"
RUNTIME = WORK / "runtime-catalog-candidates.json"
REPORT = WORK / "fixture-major-footprint-simulation.json"
BOARD = WORK / "diorama-process-17-major-footprint-fit-review.png"

base_path = ROOT / "tools/art/simulate-primary-match-room-blueprints-on-fixtures.py"
spec = importlib.util.spec_from_file_location("primary_match_fixture_base", base_path)
base = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(base)


def major_specs() -> dict[str, list[dict]]:
    layout = json.loads(LAYOUT.read_text(encoding="utf-8"))
    runtime = json.loads(RUNTIME.read_text(encoding="utf-8"))
    by_cell = {
        (sheet["path"], variant["cell"]["column"], variant["cell"]["rowFromTop"]): variant
        for sheet in runtime["sheets"] for variant in sheet["variants"]
    }
    result = {}
    for room in layout["rooms"]:
        majors = []
        for placement in room["placements"]:
            source = placement["source"]
            if "monumental_wall" in source:
                role = "backdrop"
            elif "hero_machinery" in source:
                role = "hero"
            elif "foreground_depth" in source:
                role = "foreground"
            else:
                continue
            col, row = placement["cell"]
            variant = by_cell[(source, col, row)]
            screen_center = placement["screen"][0] + placement["screen"][2] * 0.5
            edge = "west" if screen_center < 640 else "east" if screen_center > 1280 else "center"
            majors.append({
                "role": role,
                "sourceVariantId": variant["assetId"],
                "footprintCells": variant["runtimeCandidate"]["footprintCells"],
                "edgePreference": edge,
            })
        if {entry["role"] for entry in majors} != {"backdrop", "hero", "foreground"}:
            raise AssertionError(f"room major-role contract incomplete: {room['id']}")
        result[room["id"]] = majors
    return result


def footprint_cells(anchor: tuple[int, int], width: int, height: int, role: str, edge: str) -> set[tuple[int, int]]:
    c, r = anchor
    if role == "backdrop":
        x0, y0 = c - width // 2, r - height + 1
    elif role == "foreground":
        x0, y0 = c - width // 2, r
    elif edge == "east":
        x0, y0 = c - width + 1, r - height // 2
    else:
        x0, y0 = c, r - height // 2
    return {(x, y) for y in range(y0, y0 + height) for x in range(x0, x0 + width)}


def fit_majors(data: dict, connected: list[bool], basic: dict, specs: list[dict]) -> dict | None:
    cols, rows = data["cols"], data["rows"]
    cells = data["cells"]
    vx, vy, vw, vh = basic["viewport"]
    rect = basic["openRect"]
    protected = {
        (c, r)
        for r in range(rect["y"], rect["y"] + rect["height"])
        for c in range(rect["x"], rect["x"] + rect["width"])
    }

    def solid(c: int, r: int) -> bool:
        return not (0 <= c < cols and 0 <= r < rows) or cells[r * cols + c] != ""

    anchors = {"backdrop": [], "hero_west": [], "hero_east": [], "foreground": []}
    for r in range(vy, vy + vh):
        for c in range(vx, vx + vw):
            if not connected[r * cols + c]:
                continue
            if solid(c, r + 1):
                anchors["backdrop"].append((c, r))
            if solid(c - 1, r):
                anchors["hero_west"].append((c, r))
            if solid(c + 1, r):
                anchors["hero_east"].append((c, r))
            if solid(c, r - 1):
                anchors["foreground"].append((c, r))

    options = []
    for item in specs:
        width, height = item["footprintCells"]
        if item["role"] == "backdrop":
            source = anchors["backdrop"]
        elif item["role"] == "foreground":
            source = anchors["foreground"]
        else:
            source = anchors["hero_east"] if item["edgePreference"] == "east" else anchors["hero_west"]
        item_options = []
        for anchor in source:
            occupied = footprint_cells(anchor, width, height, item["role"], item["edgePreference"])
            if occupied & protected:
                continue
            if any(not (vx <= c < vx + vw and vy <= r < vy + vh) for c, r in occupied):
                continue
            if any(not connected[r * cols + c] for c, r in occupied):
                continue
            item_options.append({**item, "anchorCell": list(anchor), "occupiedCells": [list(cell) for cell in sorted(occupied)]})
        if not item_options:
            return None
        options.append(item_options)

    for combination in product(*options):
        used: set[tuple[int, int]] = set()
        valid = True
        for placement in combination:
            occupied = {tuple(cell) for cell in placement["occupiedCells"]}
            if used & occupied:
                valid = False
                break
            used |= occupied
        if valid:
            return {"placements": list(combination), "occupiedCellCount": len(used)}
    return None


def best_fit(data: dict, connected: list[bool], room_id: str, specs: list[dict]) -> dict:
    candidates = []
    for y in range(0, data["rows"] - base.VIEW_H + 1):
        for x in range(0, data["cols"] - base.VIEW_W + 1):
            basic = base.inspect_window(data, connected, x, y, room_id)
            if not basic["feasible"]:
                continue
            fit = fit_majors(data, connected, basic, specs)
            if fit is not None:
                candidates.append({**basic, "majorFootprintFit": fit, "exactMajorFootprintsFeasible": True})
    if not candidates:
        return {"exactMajorFootprintsFeasible": False}
    candidates.sort(key=lambda item: item["score"], reverse=True)
    return candidates[0]


def draw_board(results: list[dict], fixtures: list[dict]) -> None:
    board = Image.new("RGB", (1920, 1080), (13, 10, 19))
    draw = ImageDraw.Draw(board)
    draw.text((38, 24), "PROCESS 17  /  EXACT MAJOR-PROP FOOTPRINT FIT", fill=(239, 225, 245))
    draw.text((38, 48), "blue backdrop + magenta hero machine + amber foreground; cyan remains protected open floor", fill=(151, 138, 166))
    colors = {"solid": (44, 24, 54), "open": (86, 63, 98), "openRect": (59, 236, 207),
              "backdrop": (78, 184, 255), "hero": (255, 70, 205), "foreground": (255, 155, 46)}
    for index, (result, data) in enumerate(zip(results, fixtures)):
        panel_x, panel_y = 30 + index * 630, 90
        panel_w, panel_h = 600, 540
        cols, rows = data["cols"], data["rows"]
        cw, ch = panel_w / cols, panel_h / rows
        connected = base.connected_open(data)
        for r in range(rows):
            for c in range(cols):
                color = colors["open"] if connected[r * cols + c] else colors["solid"]
                draw.rectangle((panel_x + round(c * cw), panel_y + round((rows-r-1) * ch),
                                panel_x + round((c+1) * cw), panel_y + round((rows-r) * ch)), fill=color)
        chosen = result["blueprints"][result["assignedRoom"]]
        vx, vy, vw, vh = chosen["viewport"]
        draw.rectangle((panel_x + round(vx*cw), panel_y + round((rows-vy-vh)*ch),
                        panel_x + round((vx+vw)*cw), panel_y + round((rows-vy)*ch)), outline=(225, 212, 236), width=2)
        rect = chosen["openRect"]
        draw.rectangle((panel_x + round(rect["x"]*cw), panel_y + round((rows-rect["y"]-rect["height"])*ch),
                        panel_x + round((rect["x"]+rect["width"])*cw), panel_y + round((rows-rect["y"])*ch)),
                       outline=colors["openRect"], width=3)
        for placement in chosen["majorFootprintFit"]["placements"]:
            for c, r in placement["occupiedCells"]:
                draw.rectangle((panel_x + round(c*cw), panel_y + round((rows-r-1)*ch),
                                panel_x + round((c+1)*cw), panel_y + round((rows-r)*ch)),
                               fill=colors[placement["role"]], outline=(15, 10, 20))
        draw.rectangle((panel_x, panel_y, panel_x+panel_w, panel_y+panel_h), outline=(95, 76, 108), width=2)
        y = 655
        draw.text((panel_x, y), f"DEPTH {data['depth']}  /  {result['assignedRoom'].replace('_',' ').upper()}", fill=(232,217,240))
        draw.text((panel_x, y+28), f"OPEN {rect['width']}x{rect['height']}  /  MAJOR FOOTPRINTS {chosen['majorFootprintFit']['occupiedCellCount']} CELLS", fill=colors["openRect"])
        draw.text((panel_x, y+56), "PASS — 3/3 EXACT FOOTPRINTS FIT WITHOUT OVERLAP", fill=(87,234,175))
        for line, placement in enumerate(chosen["majorFootprintFit"]["placements"]):
            draw.text((panel_x, y+102+line*54), placement["role"].upper(), fill=colors[placement["role"]])
            draw.text((panel_x+120, y+102+line*54), placement["sourceVariantId"].split("/")[-1], fill=(173,159,186))
            draw.text((panel_x+120, y+122+line*54), f"{placement['footprintCells'][0]}x{placement['footprintCells'][1]} at {placement['anchorCell']}", fill=(137,125,150))
    board.save(BOARD, quality=95)


def main() -> None:
    specs_by_room = major_specs()
    fixture_paths = sorted(FIXTURES.glob("map-*.json"))
    fixtures = [json.loads(path.read_text(encoding="utf-8")) for path in fixture_paths]
    assignments = {1: "ore_intake", 2: "ventilation_service", 3: "crystal_power"}
    results = []
    for path, data in zip(fixture_paths, fixtures):
        connected = base.connected_open(data)
        evaluations = {room_id: best_fit(data, connected, room_id, specs) for room_id, specs in specs_by_room.items()}
        results.append({
            "fixture": str(path.relative_to(ROOT)).replace("\\", "/"),
            "fixtureSha256": base.sha256(path),
            "depth": data["depth"],
            "assignedRoom": assignments[data["depth"]],
            "blueprints": evaluations,
        })
    feasible = sum(candidate["exactMajorFootprintsFeasible"] for result in results for candidate in result["blueprints"].values())
    assigned = sum(result["blueprints"][result["assignedRoom"]]["exactMajorFootprintsFeasible"] for result in results)
    report = {
        "status": "verified_offline_exact_major_footprints",
        "unityExecuted": False,
        "viewportCells": [base.VIEW_W, base.VIEW_H],
        "protectedOpenMinimumCells": [base.MIN_OPEN_W, base.MIN_OPEN_H],
        "summary": {
            "fixtureCount": len(results),
            "blueprintCount": len(specs_by_room),
            "evaluations": len(results) * len(specs_by_room),
            "exactMajorFootprintFits": feasible,
            "assignedRoomFits": assigned,
        },
        "fixtures": results,
    }
    if feasible != 9 or assigned != 3:
        raise AssertionError(f"major footprint feasibility regressed: {report['summary']}")
    REPORT.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    draw_board(results, fixtures)
    print(f"Wrote {REPORT.relative_to(ROOT)}")
    print(f"Wrote {BOARD.relative_to(ROOT)}")
    print(report["summary"])


if __name__ == "__main__":
    main()
