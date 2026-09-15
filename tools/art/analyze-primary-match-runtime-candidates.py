#!/usr/bin/env python3
"""Build an offline, non-Unity runtime handoff for primary-match candidate sheets.

The output deliberately remains a candidate contract.  It measures each cell's
real alpha bounds and derives a bottom contact pivot, then combines those facts
with conservative placement metadata understood by SetPieceCatalog.cs.
"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / "art-production/test-room-v01/working/primary-match-v2"
OUT = BASE / "runtime-catalog-candidates.json"
BOARD = BASE / "diorama-process-07-runtime-footpoint-review.png"


SHEETS = [
    {
        "id": "TR01-PRIMARYMATCH-FLOOR-RAIL-TRANSITIONS-3X3",
        "path": "tr01_primarymatch_floor_rail_transitions_3x3_source.png",
        "grid": (3, 3),
        "kind": "ground_overlay",
        "names": [
            "horizontal_left", "horizontal_middle", "horizontal_right",
            "turn_up_left", "reinforced_cross", "turn_up_right",
            "damaged_left", "broken_gap", "damaged_right",
        ],
    },
    {
        "id": "TR01-PRIMARYMATCH-CRYSTAL-OUTCROPS-3X3",
        "path": "tr01_primarymatch_crystal_outcrops_3x3_source.png",
        "grid": (3, 3),
        "kind": "mixed_ground_and_setpiece",
        "names": [
            "low_left_seam", "low_center_cluster", "low_right_seam",
            "medium_left_outcrop", "medium_center_outcrop", "medium_right_outcrop",
            "depleted_left_scar", "depleted_center_scar", "depleted_right_scar",
        ],
    },
    {
        "id": "TR01-PRIMARYMATCH-EQUIPMENT-TRANSITIONS-3X3",
        "path": "tr01_primarymatch_equipment_transitions_3x3_source.png",
        "grid": (3, 3),
        "kind": "ground_overlay",
        "names": [
            "left_termination", "buried_threshold", "right_termination",
            "left_broken_corner", "reinforced_socket", "right_broken_corner",
            "left_rubble_ramp", "capped_machine_scar", "right_rubble_ramp",
        ],
    },
    {
        "id": "TR01-PRIMARYMATCH-HERO-MACHINERY-2X2",
        "path": "tr01_primarymatch_hero_machinery_2x2_source.png",
        "grid": (2, 2),
        "kind": "setpiece",
        "names": ["ore_crusher", "ventilation_turbine", "power_relay", "minecart_loading_dock"],
    },
    {
        "id": "TR01-PRIMARYMATCH-FOREGROUND-DEPTH-2X2",
        "path": "tr01_primarymatch_foreground_depth_2x2_source.png",
        "grid": (2, 2),
        "kind": "foreground_or_elevated",
        "names": ["left_foreground_shelf", "right_foreground_shelf", "bottom_cliff_lip", "raised_service_platform"],
    },
    {
        "id": "TR01-PRIMARYMATCH-MONUMENTAL-WALL-MODULES-2X2",
        "path": "tr01_primarymatch_monumental_wall_modules_2x2_source.png",
        "grid": (2, 2),
        "kind": "backdrop_setpiece",
        "names": ["sealed_bulkhead", "pipe_manifold", "crystal_processor", "collapsed_wall_machine"],
    },
]


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def alpha_measure(cell: Image.Image) -> dict:
    alpha = cell.getchannel("A")
    bbox = alpha.point(lambda a: 255 if a >= 16 else 0).getbbox()
    if bbox is None:
        return {"occupied": False}

    left, top, right, bottom = bbox
    # Use the lowest 5% of occupied pixels to estimate the visual foot contact.
    band_top = max(top, bottom - max(2, round((bottom - top) * 0.05)))
    xs = []
    px = alpha.load()
    for y in range(band_top, bottom):
        for x in range(left, right):
            if px[x, y] >= 32:
                xs.append(x)
    foot_x = (sum(xs) / len(xs)) if xs else (left + right - 1) * 0.5
    w, h = cell.size
    return {
        "occupied": True,
        "alphaBoundsTopLeftPixels": [left, top, right, bottom],
        "alphaCoverageFraction": round(sum(alpha.histogram()[16:]) / (w * h), 4),
        "bottomContactPivotNormalized": [
            round((foot_x + 0.5) / w, 4),
            round((h - bottom) / h, 4),
        ],
        "occupiedWidthFraction": round((right - left) / w, 4),
        "occupiedHeightFraction": round((bottom - top) / h, 4),
    }


def runtime_candidate(sheet_id: str, name: str) -> dict:
    base = {
        "integrationStatus": "candidate_not_unity_verified",
        "localOrder": 0,
        "lightSockets": [],
        "replacementAssetId": "",
    }

    if "FLOOR-RAIL" in sheet_id or "EQUIPMENT-TRANSITIONS" in sheet_id:
        return base | {
            "runtimeKind": "ground_overlay",
            "sortingLayer": "GroundDetail",
            "footprintCells": [1, 1],
            "visualHeightCells": 0.0,
            "collision": "none",
            "contactShadowRadius": 0.0,
            "shadowContourCells": [],
            "occluderGroup": "",
            "fadeTargetAlpha": 0.0,
        }

    if "CRYSTAL-OUTCROPS" in sheet_id:
        is_medium = name.startswith("medium_")
        result = base | {
            "runtimeKind": "setpiece" if is_medium else "ground_overlay",
            "sortingLayer": "WorldEntity" if is_medium else "GroundDetail",
            "footprintCells": [1, 1],
            "visualHeightCells": 1.05 if is_medium else 0.18,
            "collision": "soft_block_candidate" if is_medium else "none",
            "contactShadowRadius": 0.34 if is_medium else 0.0,
            "shadowContourCells": [-0.44, 0.0, 0.44, 0.0, 0.38, 0.72, -0.38, 0.72] if is_medium else [],
            "occluderGroup": "",
            "fadeTargetAlpha": 0.0,
        }
        if is_medium:
            side = name.removeprefix("medium_").removesuffix("_outcrop")
            result["replacementAssetId"] = f"TR01-PRIMARYMATCH-CRYSTAL-OUTCROPS-3X3/{'depleted_' + side + '_scar'}"
        return result

    if "HERO-MACHINERY" in sheet_id:
        specs = {
            "ore_crusher": ([2, 2], 2.2, 0.76, "WorldEntity", "broad_machine_core"),
            "ventilation_turbine": ([2, 1], 2.05, 0.68, "BackStructure", "wall_plane_core"),
            "power_relay": ([2, 2], 1.95, 0.72, "WorldEntity", "broad_machine_core"),
            "minecart_loading_dock": ([2, 2], 1.45, 0.72, "WorldEntity", "dock_core_keep_rail_lane_open"),
        }
        footprint, height, radius, layer, collision = specs[name]
        half = footprint[0] * 0.5
        depth = footprint[1] * 0.78
        return base | {
            "runtimeKind": "setpiece",
            "sortingLayer": layer,
            "footprintCells": footprint,
            "visualHeightCells": height,
            "collision": collision,
            "contactShadowRadius": radius,
            "shadowContourCells": [-half, 0.0, half, 0.0, half * 0.86, depth, -half * 0.86, depth],
            "occluderGroup": "",
            "fadeTargetAlpha": 0.0,
        }

    if "FOREGROUND-DEPTH" in sheet_id:
        if name == "raised_service_platform":
            return base | {
                "runtimeKind": "elevated_surface_gated",
                "sortingLayer": "WallTop",
                "footprintCells": [3, 2],
                "visualHeightCells": 0.8,
                "collision": "requires_walkability_and_height_rules",
                "contactShadowRadius": 0.0,
                "shadowContourCells": [],
                "occluderGroup": "",
                "fadeTargetAlpha": 0.0,
            }
        specs = {
            "left_foreground_shelf": ([3, 2], 2.55, "primarymatch_foreground_left"),
            "right_foreground_shelf": ([3, 2], 2.55, "primarymatch_foreground_right"),
            "bottom_cliff_lip": ([4, 1], 1.42, "primarymatch_foreground_lip"),
        }
        footprint, height, group = specs[name]
        half = footprint[0] * 0.5
        return base | {
            "runtimeKind": "foreground_setpiece",
            "sortingLayer": "FrontStructure",
            "footprintCells": footprint,
            "visualHeightCells": height,
            "collision": "boundary_only",
            "contactShadowRadius": max(0.7, round(footprint[0] * 0.28, 2)),
            "shadowContourCells": [-half, 0.0, half, 0.0, half * 0.9, float(footprint[1]), -half * 0.9, float(footprint[1])],
            "occluderGroup": group,
            "fadeTargetAlpha": 0.34,
        }

    if "MONUMENTAL-WALL" in sheet_id:
        specs = {
            "sealed_bulkhead": ([3, 1], 2.6, "solid_wall_topology"),
            "pipe_manifold": ([3, 1], 2.25, "wall_plane_only"),
            "crystal_processor": ([3, 1], 2.7, "solid_wall_topology"),
            "collapsed_wall_machine": ([3, 2], 2.35, "solid_wall_and_rubble"),
        }
        footprint, height, collision = specs[name]
        half = footprint[0] * 0.5
        return base | {
            "runtimeKind": "backdrop_setpiece",
            "sortingLayer": "BackStructure",
            "footprintCells": footprint,
            "visualHeightCells": height,
            "collision": collision,
            "contactShadowRadius": round(footprint[0] * 0.25, 2),
            "shadowContourCells": [-half, 0.0, half, 0.0, half, float(footprint[1]), -half, float(footprint[1])],
            "occluderGroup": "",
            "fadeTargetAlpha": 0.0,
        }

    raise ValueError(f"No runtime candidate rule for {sheet_id}/{name}")


def main() -> None:
    result = {
        "status": "offline_candidate_not_unity_integrated",
        "sourceOfRuntimeContract": [
            "SetPieceCatalog.cs",
            "SetPieceSpawner.cs",
            "ForegroundOccluder.cs",
        ],
        "coordinateNotes": {
            "alphaBounds": "cell-local pixels [left, top, rightExclusive, bottomExclusive]",
            "pivot": "normalized Unity sprite pivot in the untrimmed cell rect",
            "shadowContour": "ground-foot-relative cell coordinates used by SetPieceDef.shadowContourCells",
        },
        "approvalGates": [
            "pivot candidates must be checked in ReferenceTopDown plus F8/F9 projection presets",
            "collision candidates must preserve the central 45 percent open-width contract",
            "foreground candidates require live overlap/fade verification",
            "raised_service_platform remains disabled until walkability and visual-height rules are explicit",
            "all values remain candidates until Game View comparison and regression tests pass",
        ],
        "sheets": [],
    }

    total = 0
    board = Image.new("RGB", (1920, 1080), (19, 15, 27))
    draw = ImageDraw.Draw(board)
    draw.text((48, 24), "PROCESS 07  /  ALPHA BOUNDS + FOOTPOINT CANDIDATES  /  OFFLINE ONLY", fill=(235, 220, 244))
    draw.text((48, 48), "cyan box = occupied alpha bounds   amber cross = measured bottom-contact pivot   values remain Unity-unverified", fill=(157, 143, 174))
    for sheet_index, spec in enumerate(SHEETS):
        path = BASE / spec["path"]
        image = Image.open(path).convert("RGBA")
        cols, rows = spec["grid"]
        cell_w, cell_h = image.width // cols, image.height // rows
        variants = []
        for index, name in enumerate(spec["names"]):
            col, row = index % cols, index // cols
            cell = image.crop((col * cell_w, row * cell_h, (col + 1) * cell_w, (row + 1) * cell_h))
            variants.append({
                "assetId": f"{spec['id']}/{name}",
                "cell": {"column": col, "rowFromTop": row, "rectPixels": [col * cell_w, row * cell_h, cell_w, cell_h]},
                "measured": alpha_measure(cell),
                "runtimeCandidate": runtime_candidate(spec["id"], name),
            })
            total += 1
        result["sheets"].append({
            "assetId": spec["id"],
            "path": spec["path"],
            "sha256": sha256(path),
            "grid": [cols, rows],
            "cellDimensionsPixels": [cell_w, cell_h],
            "integrationKind": spec["kind"],
            "variants": variants,
        })

        panel_x = 48 + (sheet_index % 3) * 624
        panel_y = 88 + (sheet_index // 3) * 490
        preview_size = 410
        preview = Image.new("RGBA", image.size, (30, 24, 40, 255))
        preview.alpha_composite(image)
        preview = preview.resize((preview_size, preview_size), Image.Resampling.LANCZOS)
        board.paste(preview.convert("RGB"), (panel_x, panel_y))
        sx, sy = preview_size / image.width, preview_size / image.height
        for variant in variants:
            measured = variant["measured"]
            rect_x, rect_y, rect_w, rect_h = variant["cell"]["rectPixels"]
            left, top, right, bottom = measured["alphaBoundsTopLeftPixels"]
            bx0 = panel_x + (rect_x + left) * sx
            by0 = panel_y + (rect_y + top) * sy
            bx1 = panel_x + (rect_x + right) * sx
            by1 = panel_y + (rect_y + bottom) * sy
            draw.rectangle((bx0, by0, bx1, by1), outline=(64, 211, 226), width=1)
            pivot_x, pivot_y = measured["bottomContactPivotNormalized"]
            px = panel_x + (rect_x + pivot_x * rect_w) * sx
            py = panel_y + (rect_y + (1.0 - pivot_y) * rect_h) * sy
            draw.line((px - 5, py, px + 5, py), fill=(255, 190, 74), width=2)
            draw.line((px, py - 5, px, py + 5), fill=(255, 190, 74), width=2)
        draw.text((panel_x, panel_y + preview_size + 8), spec["id"].replace("TR01-PRIMARYMATCH-", ""), fill=(221, 210, 230))
        draw.text((panel_x, panel_y + preview_size + 28), f"{cols}x{rows} / {len(variants)} occupied variants / source alpha measured", fill=(138, 126, 151))

    result["summary"] = {"sheetCount": len(SHEETS), "variantCount": total}
    OUT.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    board.save(BOARD)
    print(f"Wrote {OUT.relative_to(ROOT)}: {len(SHEETS)} sheets, {total} variants")
    print(f"Wrote {BOARD.relative_to(ROOT)}: {board.width}x{board.height}")


if __name__ == "__main__":
    main()
