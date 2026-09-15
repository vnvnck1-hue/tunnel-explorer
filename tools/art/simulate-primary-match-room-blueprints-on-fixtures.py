#!/usr/bin/env python3
"""Offline placement feasibility pass using authoritative map-generation fixtures."""

from __future__ import annotations

import hashlib
import json
from collections import deque
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / "art-production/test-room-v01/working/primary-match-v2"
FIXTURES = ROOT / "unity/TunnelCrew/Assets/Tests/EditMode/Fixtures"
BLUEPRINTS = WORK / "curated-room-landscape-layouts.json"
REPORT = WORK / "fixture-placement-simulation.json"
BOARD = WORK / "diorama-process-16-map-fixture-placement-review.png"
VIEW_W, VIEW_H = 18, 12
MIN_OPEN_W, MIN_OPEN_H = 9, 3


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def connected_open(data: dict) -> list[bool]:
    cols, rows = data["cols"], data["rows"]
    cells = data["cells"]
    start = data["entryCell"]["r"] * cols + data["entryCell"]["c"]
    if cells[start] != "":
        raise AssertionError(f"fixture entry is not open: depth {data['depth']}")
    seen = [False] * len(cells)
    seen[start] = True
    queue = deque([start])
    while queue:
        index = queue.popleft()
        c, r = index % cols, index // cols
        for dc, dr in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nc, nr = c + dc, r + dr
            if not (0 <= nc < cols and 0 <= nr < rows):
                continue
            nxt = nr * cols + nc
            if not seen[nxt] and cells[nxt] == "":
                seen[nxt] = True
                queue.append(nxt)
    return seen


def largest_rect(mask: list[bool], cols: int, x0: int, y0: int, width: int, height: int) -> dict:
    heights = [0] * width
    best = {"x": x0, "y": y0, "width": 0, "height": 0, "area": 0}
    for local_r in range(height):
        r = y0 + local_r
        for local_c in range(width):
            c = x0 + local_c
            heights[local_c] = heights[local_c] + 1 if mask[r * cols + c] else 0
        for left in range(width):
            minimum = 10**9
            for right in range(left, width):
                minimum = min(minimum, heights[right])
                if minimum == 0:
                    break
                rect_width = right - left + 1
                area = rect_width * minimum
                if area > best["area"]:
                    best = {
                        "x": x0 + left,
                        "y": r - minimum + 1,
                        "width": rect_width,
                        "height": minimum,
                        "area": area,
                    }
    return best


def longest_horizontal(values: set[tuple[int, int]]) -> int:
    best = 0
    by_row: dict[int, list[int]] = {}
    for c, r in values:
        by_row.setdefault(r, []).append(c)
    for cols in by_row.values():
        run = 0
        previous = None
        for c in sorted(cols):
            run = run + 1 if previous is not None and c == previous + 1 else 1
            best = max(best, run)
            previous = c
    return best


def inspect_window(data: dict, connected: list[bool], x: int, y: int, room_id: str) -> dict:
    cols, rows = data["cols"], data["rows"]
    cells = data["cells"]

    def solid(c: int, r: int) -> bool:
        return not (0 <= c < cols and 0 <= r < rows) or cells[r * cols + c] != ""

    open_cells: set[tuple[int, int]] = set()
    north: set[tuple[int, int]] = set()
    west: set[tuple[int, int]] = set()
    east: set[tuple[int, int]] = set()
    south: set[tuple[int, int]] = set()
    for r in range(y, y + VIEW_H):
        for c in range(x, x + VIEW_W):
            if not connected[r * cols + c]:
                continue
            open_cells.add((c, r))
            if solid(c, r + 1):
                north.add((c, r))
            if solid(c - 1, r):
                west.add((c, r))
            if solid(c + 1, r):
                east.add((c, r))
            if solid(c, r - 1):
                south.add((c, r))

    rect = largest_rect(connected, cols, x, y, VIEW_W, VIEW_H)
    north_run = longest_horizontal(north)
    open_run = longest_horizontal(open_cells)
    common = rect["width"] >= MIN_OPEN_W and rect["height"] >= MIN_OPEN_H and north_run >= 3 and len(south) >= 2
    if room_id == "ore_intake":
        specific = len(west) >= 1 and open_run >= 5
    elif room_id == "ventilation_service":
        specific = north_run >= 4 and len(west) >= 1 and len(east) >= 1
    else:
        specific = len(west) >= 1 and len(east) >= 1
    feasible = common and specific
    score = rect["area"] * 10 + north_run * 4 + min(len(west), 8) + min(len(east), 8) + min(len(south), 8)
    return {
        "viewport": [x, y, VIEW_W, VIEW_H],
        "openRect": rect,
        "openWidthFractionOfViewport": round(rect["width"] / VIEW_W, 4),
        "anchors": {
            "northWallFoot": len(north),
            "northWallFootLongestRun": north_run,
            "westWallFoot": len(west),
            "eastWallFoot": len(east),
            "southBoundary": len(south),
        },
        "feasible": feasible,
        "score": score,
    }


def best_window(data: dict, connected: list[bool], room_id: str) -> dict:
    candidates = [
        inspect_window(data, connected, x, y, room_id)
        for y in range(0, data["rows"] - VIEW_H + 1)
        for x in range(0, data["cols"] - VIEW_W + 1)
    ]
    candidates.sort(key=lambda item: (item["feasible"], item["score"]), reverse=True)
    return candidates[0]


def draw_board(results: list[dict], fixtures: list[dict]) -> None:
    board = Image.new("RGB", (1920, 1080), (13, 10, 19))
    draw = ImageDraw.Draw(board)
    draw.text((38, 24), "PROCESS 16  /  ROOM BLUEPRINTS AGAINST MAPGEN FIXTURES", fill=(239, 225, 245))
    draw.text((38, 48), "18x12 local viewport; cyan = largest connected open rectangle; dots = wall-foot anchors", fill=(151, 138, 166))
    palette = {"solid": (44, 24, 54), "open": (86, 63, 98), "cyan": (59, 236, 207), "north": (80, 184, 255),
               "west": (255, 70, 205), "east": (255, 155, 46), "south": (248, 221, 93)}
    for index, (result, data) in enumerate(zip(results, fixtures)):
        panel_x = 30 + index * 630
        panel_y = 90
        panel_w, panel_h = 600, 540
        cols, rows = data["cols"], data["rows"]
        cell_w, cell_h = panel_w / cols, panel_h / rows
        cells = data["cells"]
        connected = connected_open(data)
        for r in range(rows):
            for c in range(cols):
                color = palette["open"] if connected[r * cols + c] else palette["solid"]
                x0 = panel_x + round(c * cell_w)
                y0 = panel_y + round((rows - r - 1) * cell_h)
                x1 = panel_x + round((c + 1) * cell_w)
                y1 = panel_y + round((rows - r) * cell_h)
                draw.rectangle((x0, y0, x1, y1), fill=color)
        chosen = result["blueprints"][result["assignedRoom"]]
        vx, vy, vw, vh = chosen["viewport"]
        px0 = panel_x + round(vx * cell_w)
        py0 = panel_y + round((rows - (vy + vh)) * cell_h)
        px1 = panel_x + round((vx + vw) * cell_w)
        py1 = panel_y + round((rows - vy) * cell_h)
        draw.rectangle((px0, py0, px1, py1), outline=(225, 212, 236), width=2)
        rect = chosen["openRect"]
        rx0 = panel_x + round(rect["x"] * cell_w)
        ry0 = panel_y + round((rows - (rect["y"] + rect["height"])) * cell_h)
        rx1 = panel_x + round((rect["x"] + rect["width"]) * cell_w)
        ry1 = panel_y + round((rows - rect["y"]) * cell_h)
        draw.rectangle((rx0, ry0, rx1, ry1), outline=palette["cyan"], width=3)
        draw.rectangle((panel_x, panel_y, panel_x + panel_w, panel_y + panel_h), outline=(95, 76, 108), width=2)

        text_y = 655
        draw.text((panel_x, text_y), f"DEPTH {data['depth']}  /  {result['assignedRoom'].replace('_', ' ').upper()}", fill=(232, 217, 240))
        draw.text((panel_x, text_y + 26), f"open rect {rect['width']}x{rect['height']} cells  /  {chosen['openWidthFractionOfViewport'] * 100:.1f}% width", fill=palette["cyan"])
        draw.text((panel_x, text_y + 50), f"north run {chosen['anchors']['northWallFootLongestRun']}  west {chosen['anchors']['westWallFoot']}  east {chosen['anchors']['eastWallFoot']}  south {chosen['anchors']['southBoundary']}", fill=(165, 151, 178))
        draw.text((panel_x, text_y + 76), "PASS" if chosen["feasible"] else "NEEDS FALLBACK", fill=(87, 234, 175) if chosen["feasible"] else (255, 144, 80))
        for line, room_id in enumerate(("ore_intake", "ventilation_service", "crystal_power")):
            candidate = result["blueprints"][room_id]
            draw.text((panel_x, text_y + 118 + line * 32), room_id.replace("_", " ").upper(), fill=(184, 170, 195))
            draw.text((panel_x + 250, text_y + 118 + line * 32),
                      f"{'PASS' if candidate['feasible'] else 'FAIL'}  {candidate['openRect']['width']}x{candidate['openRect']['height']}",
                      fill=(87, 234, 175) if candidate["feasible"] else (255, 144, 80))
    board.save(BOARD, quality=95)


def main() -> None:
    blueprint_contract = json.loads(BLUEPRINTS.read_text(encoding="utf-8"))
    room_ids = [room["id"] for room in blueprint_contract["rooms"]]
    fixture_paths = sorted(FIXTURES.glob("map-*.json"))
    fixtures = [json.loads(path.read_text(encoding="utf-8")) for path in fixture_paths]
    assignments = {1: "ore_intake", 2: "ventilation_service", 3: "crystal_power"}
    results = []
    for path, data in zip(fixture_paths, fixtures):
        connected = connected_open(data)
        evaluations = {room_id: best_window(data, connected, room_id) for room_id in room_ids}
        results.append({
            "fixture": str(path.relative_to(ROOT)).replace("\\", "/"),
            "fixtureSha256": sha256(path),
            "depth": data["depth"],
            "assignedRoom": assignments[data["depth"]],
            "connectedOpenCells": sum(connected),
            "blueprints": evaluations,
        })
    report = {
        "status": "verified_offline_against_mapgen_fixtures",
        "unityExecuted": False,
        "viewportCells": [VIEW_W, VIEW_H],
        "minimumOpenRectCells": [MIN_OPEN_W, MIN_OPEN_H],
        "blueprintContract": {
            "path": str(BLUEPRINTS.relative_to(ROOT)).replace("\\", "/"),
            "sha256": sha256(BLUEPRINTS),
        },
        "summary": {
            "fixtureCount": len(results),
            "blueprintCount": len(room_ids),
            "evaluations": len(results) * len(room_ids),
            "feasibleEvaluations": sum(
                candidate["feasible"] for result in results for candidate in result["blueprints"].values()
            ),
            "assignedRoomsFeasible": sum(
                result["blueprints"][result["assignedRoom"]]["feasible"] for result in results
            ),
        },
        "fixtures": results,
    }
    REPORT.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    draw_board(results, fixtures)
    print(f"Wrote {REPORT.relative_to(ROOT)}")
    print(f"Wrote {BOARD.relative_to(ROOT)}")
    print(report["summary"])


if __name__ == "__main__":
    main()
