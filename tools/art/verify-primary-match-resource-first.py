#!/usr/bin/env python3
"""Verify resource-first Primary Match candidates without invoking Unity."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image, ImageChops


ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / "art-production/test-room-v01/working/primary-match-v2"
MANIFEST = WORK / "manifest.json"
PROCESS_IMAGES = [
    WORK / "diorama-process-01-rail-crystal-transitions.png",
    WORK / "diorama-process-02-bold-hero-machinery.png",
    WORK / "diorama-process-03-foreground-depth.png",
    WORK / "diorama-process-04-monumental-wall-backdrop.png",
    WORK / "diorama-process-05-curated-clean-room.png",
    WORK / "diorama-process-06-material-lighting-preview.png",
    WORK / "diorama-process-07-runtime-footpoint-review.png",
    WORK / "diorama-process-08-normalized-delivery-review.png",
    WORK / "diorama-process-09-light-socket-review.png",
    WORK / "diorama-process-10-shadow-contour-review.png",
    WORK / "diorama-process-11-room-identity-triptych.png",
    WORK / "diorama-process-12-ore-intake-landscape.png",
    WORK / "diorama-process-13-ventilation-service-landscape.png",
    WORK / "diorama-process-14-crystal-power-landscape.png",
    WORK / "diorama-process-15-landscape-depth-clearance-review.png",
    WORK / "diorama-process-16-map-fixture-placement-review.png",
    WORK / "diorama-process-17-major-footprint-fit-review.png",
    WORK / "diorama-process-18-full-blueprint-footprint-fit-review.png",
    WORK / "diorama-process-19-primary-reference-comparison.png",
    WORK / "diorama-process-20-ore-intake-lighting-calibration.png",
    WORK / "diorama-process-21-ventilation-service-lighting-calibration.png",
    WORK / "diorama-process-22-crystal-power-lighting-calibration.png",
    WORK / "diorama-process-23-lighting-calibration-comparison.png",
    WORK / "diorama-process-24-ore-intake-lighting-calibration-v2.png",
    WORK / "diorama-process-25-ventilation-service-lighting-calibration-v2.png",
    WORK / "diorama-process-26-crystal-power-lighting-calibration-v2.png",
    WORK / "diorama-process-27-lighting-calibration-v2-comparison.png",
    WORK / "diorama-process-28-foreground-fade-mask-review.png",
    WORK / "diorama-process-29-connection-port-review.png",
    WORK / "diorama-process-30-ore-intake-connected-route-v2.png",
    WORK / "diorama-process-31-ore-intake-route-comparison.png",
    WORK / "diorama-process-32-crystal-power-connected-bus-v2.png",
    WORK / "diorama-process-33-crystal-power-bus-comparison.png",
    WORK / "diorama-process-34-ventilation-connected-spine-v2.png",
    WORK / "diorama-process-35-ventilation-spine-comparison.png",
]
RUNTIME_CATALOG = WORK / "runtime-catalog-candidates.json"
DELIVERY_MANIFEST = WORK / "delivery-candidates/manifest.json"
CURATED_LAYOUT = WORK / "curated-diorama-layout.json"
LIGHT_SOCKETS = WORK / "light-socket-candidates.json"
SHADOW_CONTOURS = WORK / "shadow-contour-candidates.json"
ROOM_VARIANTS = WORK / "curated-room-variants.json"
LANDSCAPE_ROOM_LAYOUTS = WORK / "curated-room-landscape-layouts.json"
FIXTURE_SIMULATION = WORK / "fixture-placement-simulation.json"
FOOTPRINT_SIMULATION = WORK / "fixture-major-footprint-simulation.json"
FULL_BLUEPRINT_SIMULATION = WORK / "fixture-full-blueprint-footprint-simulation.json"
REFERENCE_COMPARISON = WORK / "reference-composition-comparison.json"
LIGHTING_CALIBRATION_V1 = WORK / "room-lighting-calibration-candidates.json"
LIGHTING_CALIBRATION_V2 = WORK / "room-lighting-calibration-candidates-v2.json"
FOREGROUND_FADE_MASKS = WORK / "foreground-fade-mask-candidates.json"
CONNECTION_PORTS = WORK / "connection-port-candidates.json"
ORE_CONNECTED_ROUTE = WORK / "ore-intake-connected-route-v2.json"
CRYSTAL_CONNECTED_BUS = WORK / "crystal-power-connected-bus-v2.json"
VENTILATION_CONNECTED_SPINE = WORK / "ventilation-connected-spine-v2.json"
VERIFICATION_REPORT = WORK / "offline-verification-report.json"
UNITY_IMPORT_PLAN = ROOT / "unity/TunnelCrew/AgentScripts/primary-match-bold-import-plan.json"
STAGING_SCRIPT = ROOT / "tools/art/stage-primary-match-bold-candidates.ps1"
UNITY_IMPORT_SCRIPT = ROOT / "unity/TunnelCrew/AgentScripts/ImportPrimaryMatchBoldCandidates.cs"
UNITY_VERIFY_SCRIPT = ROOT / "unity/TunnelCrew/AgentScripts/VerifyPrimaryMatchBoldCandidates.cs"
UNITY_TOPOLOGY_PREFLIGHT = ROOT / "unity/TunnelCrew/AgentScripts/InspectPrimaryMatchRoomTopology.cs"
UNITY_STAGE_RECEIPT = ROOT / "unity/TunnelCrew/AgentScripts/primary-match-bold-stage-receipt.json"
UNITY_TOPOLOGY_REPORT = ROOT / "unity/TunnelCrew/AgentScripts/primary-match-room-topology-preflight.json"
UNITY_IMPORT_REPORT = ROOT / "unity/TunnelCrew/primary-match-bold-import-verify.txt"
UNITY_PROMOTION_REPORT = ROOT / "unity/TunnelCrew/primary-match-bold-runtime-promotion.txt"
UNITY_RUNTIME_CATALOG = ROOT / "unity/TunnelCrew/Assets/_Project/Data/Resources/Visual/SetPieceCatalog_PrimaryMatchRuntime.asset"
UNITY_RUNTIME_DECORATOR = ROOT / "unity/TunnelCrew/Assets/_Project/Presentation/Visual/Environment/PrimaryMatchRoomDecorator.cs"
UNITY_RUNTIME_CAPTURES = [
    ROOT / "unity/TunnelCrew/Captures/PrimaryMatch/primary-match-runtime-depth1-ore-final.png",
    ROOT / "unity/TunnelCrew/Captures/PrimaryMatch/primary-match-runtime-depth2-ventilation-final.png",
    ROOT / "unity/TunnelCrew/Captures/PrimaryMatch/primary-match-runtime-depth3-crystal-final.png",
]


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def main() -> None:
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    candidates = [asset for asset in manifest["assets"] if asset.get("runtimeIntegrated") is False]
    if len(candidates) != 6:
        raise AssertionError(f"expected 6 resource-first candidates, got {len(candidates)}")

    verified_cells = 0
    for asset in candidates:
        path = WORK / asset["path"]
        if not path.is_file():
            raise AssertionError(f"missing candidate: {path}")
        if sha256(path) != asset["sha256"]:
            raise AssertionError(f"hash mismatch: {path.name}")

        with Image.open(path) as image:
            rgba = image.convert("RGBA")
            if rgba.size != (1254, 1254):
                raise AssertionError(f"unexpected dimensions: {path.name} {rgba.size}")
            if rgba.getchannel("A").getextrema() != (0, 255):
                raise AssertionError(f"candidate lacks real transparent and opaque pixels: {path.name}")

            cell_width, cell_height = asset["cellDimensionsPixels"]
            cols, rows = rgba.width // cell_width, rgba.height // cell_height
            if cols * cell_width != rgba.width or rows * cell_height != rgba.height:
                raise AssertionError(f"grid does not divide canvas: {path.name}")
            for row in range(rows):
                for col in range(cols):
                    alpha = rgba.crop((
                        col * cell_width,
                        row * cell_height,
                        (col + 1) * cell_width,
                        (row + 1) * cell_height,
                    )).getchannel("A")
                    if alpha.getbbox() is None:
                        raise AssertionError(f"empty cell {col},{row}: {path.name}")
                    verified_cells += 1

    expected_cells = manifest["validation"]["resourceFirstCandidateCellsWithForeground"]
    if verified_cells != expected_cells:
        raise AssertionError(f"expected {expected_cells} occupied cells, got {verified_cells}")

    for path in PROCESS_IMAGES:
        if not path.is_file():
            raise AssertionError(f"missing process image: {path.name}")
        with Image.open(path) as image:
            if image.size != (1920, 1080):
                raise AssertionError(f"unexpected process image size: {path.name} {image.size}")
    if manifest["validation"]["offlineDioramaProcessImagesCreated"] != len(PROCESS_IMAGES):
        raise AssertionError("manifest process image count mismatch")

    channel_report_path = WORK / manifest["validation"]["resourceFirstPreviewChannelReport"]
    channel_report = json.loads(channel_report_path.read_text(encoding="utf-8"))
    expected_contract = {"R": "metal", "G": "gloss", "B": "wet_or_crystal", "A": "effect_strength"}
    if channel_report["materialMaskContract"] != expected_contract:
        raise AssertionError("material mask contract mismatch")
    if channel_report["unityImported"] is not False:
        raise AssertionError("preview channels must remain Unity-unimported")
    if len(channel_report["assets"]) != len(candidates):
        raise AssertionError("preview channel asset count mismatch")
    channel_images = 0
    for entry in channel_report["assets"]:
        if set(entry["channels"]) != {"normal", "ao", "emission", "mask"}:
            raise AssertionError(f"incomplete channel set: {entry['assetId']}")
        for channel in entry["channels"].values():
            path = WORK / channel["path"]
            if not path.is_file() or sha256(path) != channel["sha256"]:
                raise AssertionError(f"missing or changed preview channel: {path}")
            with Image.open(path) as image:
                if image.size != (1254, 1254):
                    raise AssertionError(f"unexpected channel size: {path.name} {image.size}")
            channel_images += 1

    if channel_images != manifest["validation"]["resourceFirstPreviewChannelImagesCreated"]:
        raise AssertionError("preview channel image count mismatch")

    placement_path = WORK / manifest["validation"]["resourceFirstPlacementCandidateContract"]
    placement = json.loads(placement_path.read_text(encoding="utf-8"))
    composition = placement["compositionContract"]
    if composition["minimumOpenCenterWidthFraction"] < 0.45:
        raise AssertionError("center clearance contract regressed")
    if composition["archesAllowed"] is not False:
        raise AssertionError("arches must remain excluded")
    placement_ids = {entry["assetId"] for entry in placement["sets"]}
    candidate_ids = {entry["assetId"] for entry in candidates}
    if placement_ids != candidate_ids:
        raise AssertionError("placement contract does not cover every candidate set")

    runtime_catalog = json.loads(RUNTIME_CATALOG.read_text(encoding="utf-8"))
    if runtime_catalog["status"] != "offline_candidate_not_unity_integrated":
        raise AssertionError("runtime catalog must remain explicitly unintegrated")
    if runtime_catalog["summary"] != {"sheetCount": 6, "variantCount": verified_cells}:
        raise AssertionError("runtime catalog summary mismatch")
    runtime_sheet_ids = {entry["assetId"] for entry in runtime_catalog["sheets"]}
    if runtime_sheet_ids != candidate_ids:
        raise AssertionError("runtime catalog does not cover every candidate set")
    seen_variant_ids = set()
    for sheet in runtime_catalog["sheets"]:
        if sha256(WORK / sheet["path"]) != sheet["sha256"]:
            raise AssertionError(f"runtime catalog source hash mismatch: {sheet['path']}")
        for variant in sheet["variants"]:
            seen_variant_ids.add(variant["assetId"])
            measured = variant["measured"]
            candidate = variant["runtimeCandidate"]
            if not measured.get("occupied"):
                raise AssertionError(f"runtime catalog contains empty variant: {variant['assetId']}")
            pivot = measured["bottomContactPivotNormalized"]
            if len(pivot) != 2 or not all(0.0 <= value <= 1.0 for value in pivot):
                raise AssertionError(f"invalid pivot candidate: {variant['assetId']}")
            if candidate["integrationStatus"] != "candidate_not_unity_verified":
                raise AssertionError(f"variant lost candidate gate: {variant['assetId']}")
            if candidate["sortingLayer"] not in {
                "GroundDetail", "BackStructure", "WallTop", "WorldEntity", "FrontStructure"
            }:
                raise AssertionError(f"unsupported sorting layer: {variant['assetId']}")
            contour = candidate["shadowContourCells"]
            if contour and (len(contour) < 6 or len(contour) % 2 != 0):
                raise AssertionError(f"invalid shadow contour: {variant['assetId']}")
    if len(seen_variant_ids) != verified_cells:
        raise AssertionError("runtime catalog variant IDs are missing or duplicated")

    delivery = json.loads(DELIVERY_MANIFEST.read_text(encoding="utf-8"))
    if delivery["status"] != "working_candidate_not_approved" or delivery["unityImported"] is not False:
        raise AssertionError("delivery package must remain working and Unity-unimported")
    if delivery["deliveryPixelsPerCell"] != 128:
        raise AssertionError("delivery package PPU contract changed")
    if delivery["summary"]["assetCount"] != verified_cells:
        raise AssertionError("delivery asset count mismatch")
    if delivery["summary"]["channelImages"] != verified_cells * 5:
        raise AssertionError("delivery channel count mismatch")

    source_variant_ids = set()
    delivery_channel_images = 0
    for asset in delivery["assets"]:
        source_variant_ids.add(asset["sourceVariantId"])
        if asset["status"] != "working_candidate_not_approved":
            raise AssertionError(f"delivery asset lost working gate: {asset['assetId']}")
        if set(asset["channels"]) != {"albedo", "normal", "ao", "emission", "mask"}:
            raise AssertionError(f"delivery channels incomplete: {asset['assetId']}")
        expected_size = tuple(asset["dimensionsPixels"])
        alphas = {}
        for channel, relative in asset["channels"].items():
            path = DELIVERY_MANIFEST.parent / relative
            if not path.is_file() or sha256(path) != asset["channelHashes"][channel]:
                raise AssertionError(f"missing or changed delivery channel: {path}")
            with Image.open(path) as image:
                rgba = image.convert("RGBA")
                if rgba.size != expected_size:
                    raise AssertionError(f"delivery dimensions diverged: {path}")
                alphas[channel] = rgba.getchannel("A").copy()
            delivery_channel_images += 1
        for channel in ("normal", "ao"):
            if ImageChops.difference(alphas["albedo"], alphas[channel]).getbbox() is not None:
                raise AssertionError(f"delivery alpha alignment diverged: {asset['assetId']} {channel}")
        # Emission alpha is emission strength and material-mask alpha is effect
        # strength, so they must not equal silhouette alpha.  They may only live
        # inside it.
        for channel in ("emission", "mask"):
            if ImageChops.subtract(alphas[channel], alphas["albedo"]).getbbox() is not None:
                raise AssertionError(f"delivery data leaks outside silhouette: {asset['assetId']} {channel}")
        pivot = asset["pivotPixels"]
        if not (0 <= pivot[0] <= expected_size[0] and 0 <= pivot[1] <= expected_size[1]):
            raise AssertionError(f"delivery pivot outside canvas: {asset['assetId']}")

    if source_variant_ids != seen_variant_ids:
        raise AssertionError("delivery package does not cover every runtime candidate")
    if delivery_channel_images != verified_cells * 5:
        raise AssertionError("delivery channel file count mismatch")

    layout = json.loads(CURATED_LAYOUT.read_text(encoding="utf-8"))
    if layout["status"] != "offline_composition_candidate_not_runtime_integrated":
        raise AssertionError("curated layout must remain explicitly unintegrated")
    if layout["canvasPixels"] != [1920, 1080] or layout["minimumOpenCenterWidthFraction"] < 0.45:
        raise AssertionError("curated layout canvas or center-clearance contract changed")
    placements = layout["placements"]
    if len(placements) != 18 or len({entry["id"] for entry in placements}) != 18:
        raise AssertionError("curated layout placement IDs are missing or duplicated")
    allowed_existing = {
        "TR01-PRIMARYMATCH-SERVICE-PYLONS-3COL/cyan",
        "TR01-PRIMARYMATCH-SERVICE-PYLONS-3COL/amber",
        "existing/guardian_drone",
    }
    for entry in placements:
        if entry["runtimeVariantId"] not in seen_variant_ids | allowed_existing:
            raise AssertionError(f"curated layout references unknown asset: {entry['runtimeVariantId']}")
        source = ROOT / entry["source"] if entry["source"].startswith("unity/") else WORK / entry["source"]
        if not source.is_file():
            raise AssertionError(f"curated layout source missing: {source}")
    budgets = layout["budgets"]
    if budgets["monumentalBackdropCount"] > 3 or budgets["heroMachineCount"] > 2:
        raise AssertionError("curated layout exceeds bold-prop density budget")
    if budgets["railRouteCount"] != 1 or budgets["foregroundOccluderCount"] > 2:
        raise AssertionError("curated layout route/foreground budget changed")

    room_variants = json.loads(ROOM_VARIANTS.read_text(encoding="utf-8"))
    if room_variants["status"] != "offline_composition_candidate_not_runtime_integrated":
        raise AssertionError("room variants must remain explicitly unintegrated")
    if room_variants["compositionImage"] != "diorama-process-11-room-identity-triptych.png":
        raise AssertionError("room variant review image mismatch")
    rules = room_variants["sharedRules"]
    if rules != {
        "minimumOpenCenterWidthFraction": 0.45,
        "heroMachinesPerRoom": 1,
        "monumentalBackdropsPerRoom": 1,
        "foregroundOccludersPerRoom": 1,
        "archesAllowed": False,
    }:
        raise AssertionError("room identity composition rules changed")
    rooms = room_variants["rooms"]
    if len(rooms) != 3 or len({room["id"] for room in rooms}) != 3:
        raise AssertionError("room identity IDs are missing or duplicated")
    source_roles = {
        "tr01_primarymatch_hero_machinery_2x2_source.png": "hero",
        "tr01_primarymatch_monumental_wall_modules_2x2_source.png": "backdrop",
        "tr01_primarymatch_foreground_depth_2x2_source.png": "foreground",
    }
    for room in rooms:
        role_counts = {"hero": 0, "backdrop": 0, "foreground": 0}
        for entry in room["placements"]:
            source = WORK / entry["source"]
            if not source.is_file():
                raise AssertionError(f"room identity source missing: {source}")
            cols, rows = entry["grid"]
            col, row = entry["cell"]
            if cols <= 0 or rows <= 0 or not (0 <= col < cols and 0 <= row < rows):
                raise AssertionError(f"invalid room identity cell: {room['id']}")
            with Image.open(source) as image:
                if image.width % cols or image.height % rows:
                    raise AssertionError(f"room identity grid does not divide source: {source.name}")
            role = source_roles.get(entry["source"])
            if role:
                role_counts[role] += 1
        if role_counts != {"hero": 1, "backdrop": 1, "foreground": 1}:
            raise AssertionError(f"room identity density budget changed: {room['id']} {role_counts}")

    landscape_rooms = json.loads(LANDSCAPE_ROOM_LAYOUTS.read_text(encoding="utf-8"))
    if landscape_rooms["status"] != "offline_composition_candidate_not_runtime_integrated":
        raise AssertionError("landscape rooms must remain explicitly unintegrated")
    if landscape_rooms["canvasPixels"] != [1920, 1080]:
        raise AssertionError("landscape room canvas changed")
    landscape_rules = landscape_rooms["sharedRules"]
    if landscape_rules != {
        "heroMachinesPerRoom": 1,
        "monumentalBackdropsPerRoom": 1,
        "foregroundOccludersPerRoom": 1,
        "railRoutesMaximumPerRoom": 1,
        "openFloorBlockingCoverageMaximum": 0.08,
        "archesAllowed": False,
    }:
        raise AssertionError("landscape room rules changed")
    landscape_entries = landscape_rooms["rooms"]
    if len(landscape_entries) != 3 or len({room["id"] for room in landscape_entries}) != 3:
        raise AssertionError("landscape room IDs are missing or duplicated")
    if manifest["validation"]["resourceFirstLandscapeRoomCount"] != len(landscape_entries):
        raise AssertionError("manifest landscape room count mismatch")
    if set(manifest["validation"]["resourceFirstLandscapeRoomImages"]) != {
        room["compositionImage"] for room in landscape_entries
    }:
        raise AssertionError("manifest landscape room image set mismatch")
    landscape_coverage = {}
    for room in landscape_entries:
        if room["compositionImage"] not in {path.name for path in PROCESS_IMAGES}:
            raise AssertionError(f"landscape room review image is not tracked: {room['id']}")
        x0, y0, x1, y1 = room["openFloorRectPixels"]
        if not (0 <= x0 < x1 <= 1920 and 0 <= y0 < y1 <= 1080):
            raise AssertionError(f"landscape open-floor rect invalid: {room['id']}")
        if (x1 - x0) / 1920 < landscape_rooms["minimumOpenCenterWidthFraction"]:
            raise AssertionError(f"landscape open-floor width regressed: {room['id']}")
        anchors = room["runtimeAnchors"]
        if not {"backdrop", "hero", "foreground", "openFloor"}.issubset(anchors):
            raise AssertionError(f"landscape runtime anchors incomplete: {room['id']}")
        if any(not value or "screen" in value and key != "foreground" for key, value in anchors.items()):
            raise AssertionError(f"landscape runtime anchor is not semantic: {room['id']}")
        role_counts = {"hero": 0, "backdrop": 0, "foreground": 0}
        route_ids = set()
        blocking = Image.new("L", (1920, 1080), 0)
        for entry in room["placements"]:
            source = WORK / entry["source"]
            if not source.is_file():
                raise AssertionError(f"landscape room source missing: {source}")
            cols, rows = entry["grid"]
            col, row = entry["cell"]
            with Image.open(source) as sheet:
                rgba = sheet.convert("RGBA")
                if rgba.width % cols or rgba.height % rows or not (0 <= col < cols and 0 <= row < rows):
                    raise AssertionError(f"landscape room source grid invalid: {room['id']}")
                width, height = rgba.width // cols, rgba.height // rows
                item = rgba.crop((col * width, row * height, (col + 1) * width, (row + 1) * height))
                bounds = item.getchannel("A").getbbox()
                if bounds is None:
                    raise AssertionError(f"landscape room references empty cell: {room['id']}")
                item = item.crop(bounds)
            screen_x, screen_y, screen_width = entry["screen"]
            screen_height = max(1, round(item.height * screen_width / item.width))
            if entry["layer"] != "ground":
                alpha = item.getchannel("A").resize((screen_width, screen_height), Image.Resampling.LANCZOS)
                blocking.paste(alpha, (screen_x, screen_y), alpha)
            role = source_roles.get(entry["source"])
            if role:
                role_counts[role] += 1
            if "route" in entry:
                route_ids.add(entry["route"])
        if role_counts != {"hero": 1, "backdrop": 1, "foreground": 1}:
            raise AssertionError(f"landscape density budget changed: {room['id']} {role_counts}")
        if len(route_ids) > landscape_rules["railRoutesMaximumPerRoom"]:
            raise AssertionError(f"landscape route budget changed: {room['id']}")
        open_floor = blocking.crop((x0, y0, x1, y1)).point(lambda a: 255 if a >= 89 else 0)
        coverage = sum(open_floor.histogram()[1:]) / ((x1 - x0) * (y1 - y0))
        landscape_coverage[room["id"]] = round(coverage, 5)
        if coverage > landscape_rules["openFloorBlockingCoverageMaximum"]:
            raise AssertionError(f"landscape open floor is blocked: {room['id']} {coverage:.3f}")

    fixture_simulation = json.loads(FIXTURE_SIMULATION.read_text(encoding="utf-8"))
    if fixture_simulation["status"] != "verified_offline_against_mapgen_fixtures":
        raise AssertionError("map fixture placement simulation status mismatch")
    if fixture_simulation["unityExecuted"] is not False:
        raise AssertionError("map fixture simulation must remain offline")
    if fixture_simulation["viewportCells"] != [18, 12] or fixture_simulation["minimumOpenRectCells"] != [9, 3]:
        raise AssertionError("map fixture simulation viewport or clearance contract changed")
    expected_fixture_summary = {
        "fixtureCount": 3,
        "blueprintCount": 3,
        "evaluations": 9,
        "feasibleEvaluations": 9,
        "assignedRoomsFeasible": 3,
    }
    if fixture_simulation["summary"] != expected_fixture_summary:
        raise AssertionError("map fixture placement simulation summary mismatch")
    for fixture in fixture_simulation["fixtures"]:
        fixture_path = ROOT / fixture["fixture"]
        if not fixture_path.is_file() or sha256(fixture_path) != fixture["fixtureSha256"]:
            raise AssertionError(f"map fixture placement source changed: {fixture_path}")
        if fixture["assignedRoom"] not in fixture["blueprints"]:
            raise AssertionError(f"assigned room missing from fixture simulation: {fixture['depth']}")
        for room_id, candidate in fixture["blueprints"].items():
            if not candidate["feasible"]:
                raise AssertionError(f"room blueprint infeasible in fixture depth {fixture['depth']}: {room_id}")
            if candidate["openWidthFractionOfViewport"] < 0.5:
                raise AssertionError(f"fixture open width regressed at depth {fixture['depth']}: {room_id}")
            anchors = candidate["anchors"]
            if anchors["northWallFootLongestRun"] < 3 or anchors["southBoundary"] < 2:
                raise AssertionError(f"fixture anchor coverage regressed at depth {fixture['depth']}: {room_id}")

    footprint_simulation = json.loads(FOOTPRINT_SIMULATION.read_text(encoding="utf-8"))
    if footprint_simulation["status"] != "verified_offline_exact_major_footprints":
        raise AssertionError("major footprint simulation status mismatch")
    if footprint_simulation["unityExecuted"] is not False:
        raise AssertionError("major footprint simulation must remain offline")
    if footprint_simulation["viewportCells"] != [18, 12] or footprint_simulation["protectedOpenMinimumCells"] != [9, 3]:
        raise AssertionError("major footprint viewport or protected-open contract changed")
    if footprint_simulation["summary"] != {
        "fixtureCount": 3,
        "blueprintCount": 3,
        "evaluations": 9,
        "exactMajorFootprintFits": 9,
        "assignedRoomFits": 3,
    }:
        raise AssertionError("major footprint simulation summary mismatch")
    for fixture in footprint_simulation["fixtures"]:
        fixture_path = ROOT / fixture["fixture"]
        if sha256(fixture_path) != fixture["fixtureSha256"]:
            raise AssertionError(f"major footprint fixture source changed: {fixture_path}")
        for room_id, candidate in fixture["blueprints"].items():
            if not candidate["exactMajorFootprintsFeasible"]:
                raise AssertionError(f"major footprints infeasible at depth {fixture['depth']}: {room_id}")
            fit = candidate["majorFootprintFit"]
            placements = fit["placements"]
            if {entry["role"] for entry in placements} != {"backdrop", "hero", "foreground"}:
                raise AssertionError(f"major footprint roles incomplete at depth {fixture['depth']}: {room_id}")
            occupied = []
            for entry in placements:
                width, height = entry["footprintCells"]
                if len(entry["occupiedCells"]) != width * height:
                    raise AssertionError(f"major footprint area mismatch at depth {fixture['depth']}: {room_id}")
                occupied.extend(tuple(cell) for cell in entry["occupiedCells"])
            if len(occupied) != len(set(occupied)) or fit["occupiedCellCount"] != len(occupied):
                raise AssertionError(f"major footprints overlap at depth {fixture['depth']}: {room_id}")

    full_blueprint_simulation = json.loads(FULL_BLUEPRINT_SIMULATION.read_text(encoding="utf-8"))
    if full_blueprint_simulation["status"] != "verified_offline_full_blueprint_footprints":
        raise AssertionError("full blueprint footprint simulation status mismatch")
    if full_blueprint_simulation["unityExecuted"] is not False:
        raise AssertionError("full blueprint footprint simulation must remain offline")
    if full_blueprint_simulation["groundOverlaysAreNonBlocking"] is not True:
        raise AssertionError("full blueprint ground overlays must remain non-blocking")
    if full_blueprint_simulation["summary"] != {
        "fixtureCount": 3,
        "blueprintCount": 3,
        "evaluations": 9,
        "fullBlueprintFits": 9,
        "assignedRoomFits": 3,
    }:
        raise AssertionError("full blueprint footprint simulation summary mismatch")
    expected_full_counts = {
        "ore_intake": (6, 19, 4, 3),
        "ventilation_service": (6, 14, 3, 0),
        "crystal_power": (7, 17, 2, 0),
    }
    for fixture in full_blueprint_simulation["fixtures"]:
        fixture_path = ROOT / fixture["fixture"]
        if sha256(fixture_path) != fixture["fixtureSha256"]:
            raise AssertionError(f"full blueprint fixture source changed: {fixture_path}")
        for room_id, candidate in fixture["blueprints"].items():
            if not candidate["fullBlockingFootprintsFeasible"]:
                raise AssertionError(f"full blueprint infeasible at depth {fixture['depth']}: {room_id}")
            fit = candidate["fullBlueprintFit"]
            placements = fit["blockingPlacements"]
            expected_placements, expected_cells, expected_ground, expected_rail = expected_full_counts[room_id]
            if (fit["blockingPlacementCount"], fit["blockingOccupiedCells"],
                    fit["groundOverlayCount"], len(fit["railRouteCells"])) != (
                        expected_placements, expected_cells, expected_ground, expected_rail):
                raise AssertionError(f"full blueprint density changed at depth {fixture['depth']}: {room_id}")
            if not {"backdrop", "hero", "foreground"}.issubset({entry["role"] for entry in placements}):
                raise AssertionError(f"full blueprint major roles incomplete at depth {fixture['depth']}: {room_id}")
            occupied = [tuple(cell) for entry in placements for cell in entry["occupiedCells"]]
            if len(occupied) != expected_cells or len(occupied) != len(set(occupied)):
                raise AssertionError(f"full blueprint props overlap at depth {fixture['depth']}: {room_id}")
            open_rect = candidate["openRect"]
            open_cells = {
                (x, y)
                for x in range(open_rect["x"], open_rect["x"] + open_rect["width"])
                for y in range(open_rect["y"], open_rect["y"] + open_rect["height"])
            }
            if set(occupied) & open_cells:
                raise AssertionError(f"full blueprint blocks protected floor at depth {fixture['depth']}: {room_id}")
            rail_cells = [tuple(cell) for cell in fit["railRouteCells"]]
            if any(cell not in open_cells for cell in rail_cells):
                raise AssertionError(f"rail route escaped protected floor at depth {fixture['depth']}: {room_id}")
            if rail_cells and any(
                    rail_cells[index + 1] != (rail_cells[index][0] + 1, rail_cells[index][1])
                    for index in range(len(rail_cells) - 1)):
                raise AssertionError(f"rail route is not contiguous at depth {fixture['depth']}: {room_id}")

    reference_comparison = json.loads(REFERENCE_COMPARISON.read_text(encoding="utf-8"))
    if reference_comparison["status"] != "verified_offline_reference_comparison":
        raise AssertionError("primary reference comparison status mismatch")
    if reference_comparison["unityExecuted"] is not False:
        raise AssertionError("primary reference comparison must remain offline")
    if reference_comparison["comparisonImage"] != "diorama-process-19-primary-reference-comparison.png":
        raise AssertionError("primary reference comparison image mismatch")
    expected_comparison_ids = {
        "primary_reference", "ore_intake", "ventilation_service", "crystal_power"
    }
    comparison_entries = {entry["id"]: entry for entry in reference_comparison["entries"]}
    if set(comparison_entries) != expected_comparison_ids:
        raise AssertionError("primary reference comparison room coverage mismatch")
    metric_keys = {
        "darkUnder008Percent", "mid008To035Percent", "lumaMedian", "saturationMedian",
        "magentaAccentPercent", "cyanAccentPercent", "amberAccentPercent",
    }
    for entry_id, entry in comparison_entries.items():
        image_path = ROOT / entry["image"]
        if not image_path.is_file():
            raise AssertionError(f"primary reference comparison source missing: {image_path}")
        if entry["centralCropFraction"] != 0.8 or set(entry["metrics"]) != metric_keys:
            raise AssertionError(f"primary reference comparison metric contract changed: {entry_id}")
        if not (0.0 <= entry["metrics"]["lumaMedian"] <= 1.0
                and 0.0 <= entry["metrics"]["saturationMedian"] <= 1.0):
            raise AssertionError(f"primary reference comparison metric out of range: {entry_id}")
        if set(entry["deltaFromPrimaryReference"]) != metric_keys:
            raise AssertionError(f"primary reference comparison delta contract changed: {entry_id}")
    if any(comparison_entries["primary_reference"]["deltaFromPrimaryReference"].values()):
        raise AssertionError("primary reference self delta must remain zero")
    for room_id in expected_comparison_ids - {"primary_reference"}:
        metrics = comparison_entries[room_id]["metrics"]
        if not (0.12 <= metrics["lumaMedian"] <= 0.20
                and 0.50 <= metrics["saturationMedian"] <= 0.68
                and metrics["mid008To035Percent"] >= 75.0):
            raise AssertionError(f"resource-first room left the primary tonal envelope: {room_id}")

    lighting_v1 = json.loads(LIGHTING_CALIBRATION_V1.read_text(encoding="utf-8"))
    lighting_v2 = json.loads(LIGHTING_CALIBRATION_V2.read_text(encoding="utf-8"))
    if lighting_v1["status"] != "offline_lighting_calibration_candidate" or lighting_v1["unityExecuted"] is not False:
        raise AssertionError("lighting calibration V1 status mismatch")
    if lighting_v2["status"] != "offline_lighting_calibration_candidate_v2" or lighting_v2["unityExecuted"] is not False:
        raise AssertionError("lighting calibration V2 status mismatch")
    if lighting_v2["calibrationVersion"] != 2:
        raise AssertionError("lighting calibration V2 version mismatch")
    expected_calibration_rooms = {"ore_intake", "ventilation_service", "crystal_power"}
    for contract, version in ((lighting_v1, 1), (lighting_v2, 2)):
        rooms_by_id = {room["roomId"]: room for room in contract["rooms"]}
        if set(rooms_by_id) != expected_calibration_rooms:
            raise AssertionError(f"lighting calibration V{version} room coverage mismatch")
        for room_id, room in rooms_by_id.items():
            source_path = WORK / room["sourceImage"]
            calibrated_path = WORK / room["calibratedImage"]
            if not source_path.is_file() or not calibrated_path.is_file():
                raise AssertionError(f"lighting calibration V{version} image missing: {room_id}")
            if room["status"] != "offline_lighting_hypothesis_not_unity_verified":
                raise AssertionError(f"lighting calibration V{version} lost Unity gate: {room_id}")
            if len(room["lights"]) != 3 or {light["kind"] for light in room["lights"]} != {"radial", "beam"}:
                raise AssertionError(f"lighting calibration V{version} light hierarchy changed: {room_id}")
    if lighting_v1["comparisonImage"] != "diorama-process-23-lighting-calibration-comparison.png":
        raise AssertionError("lighting calibration V1 comparison image mismatch")
    if lighting_v2["comparisonImage"] != "diorama-process-27-lighting-calibration-v2-comparison.png":
        raise AssertionError("lighting calibration V2 comparison image mismatch")
    v2_rooms = {room["roomId"]: room for room in lighting_v2["rooms"]}
    for room_id, room in v2_rooms.items():
        metrics = room["metrics"]
        if not (0.17 <= metrics["lumaMedian"] <= 0.19
                and 0.52 <= metrics["saturationMedian"] <= 0.62
                and metrics["mid008To035Percent"] >= 80.0):
            raise AssertionError(f"lighting calibration V2 missed primary tonal envelope: {room_id}")
    if not (25.0 <= v2_rooms["ore_intake"]["metrics"]["magentaAccentPercent"] <= 36.0):
        raise AssertionError("ore intake V2 magenta balance regressed")
    if not (6.0 <= v2_rooms["ventilation_service"]["metrics"]["cyanAccentPercent"] <= 10.0):
        raise AssertionError("ventilation service V2 cyan balance regressed")
    if not (35.0 <= v2_rooms["crystal_power"]["metrics"]["magentaAccentPercent"] <= 45.0):
        raise AssertionError("crystal power V2 magenta balance regressed")

    light_sockets = json.loads(LIGHT_SOCKETS.read_text(encoding="utf-8"))
    if light_sockets["status"] != "offline_candidate_not_unity_integrated":
        raise AssertionError("light sockets must remain explicitly unintegrated")
    if light_sockets["summary"] != {
        "assetCount": 8,
        "socketCount": 11,
        "reviewImage": "diorama-process-09-light-socket-review.png",
    }:
        raise AssertionError("light socket summary mismatch")
    delivery_ids = {asset["assetId"] for asset in delivery["assets"]}
    socket_asset_ids = set()
    for asset in light_sockets["assets"]:
        socket_asset_ids.add(asset["assetId"])
        if asset["assetId"] not in delivery_ids:
            raise AssertionError(f"light socket asset missing from delivery: {asset['assetId']}")
        if not ("-HERO-" in asset["assetId"] or "-BACKDROP-" in asset["assetId"]):
            raise AssertionError(f"light socket scope expanded to noisy asset: {asset['assetId']}")
        emission_path = DELIVERY_MANIFEST.parent / asset["emissionPath"]
        if sha256(emission_path) != asset["emissionSha256"]:
            raise AssertionError(f"light socket emission hash mismatch: {asset['assetId']}")
        if not (1 <= len(asset["sockets"]) <= 2):
            raise AssertionError(f"light socket density out of range: {asset['assetId']}")
        for socket in asset["sockets"]:
            if socket["status"] != "candidate_not_unity_verified":
                raise AssertionError(f"light socket lost candidate gate: {asset['assetId']}")
            if socket["lightClass"] not in {"Worklamp", "MineralGlow", "Indicator"}:
                raise AssertionError(f"unsupported light class: {asset['assetId']}")
            if not (0.0 < socket["rangeCells"] <= 2.5 and 0.0 < socket["intensity"] <= 0.7):
                raise AssertionError(f"light socket exceeds restrained range/intensity: {asset['assetId']}")
            if socket["snapDistanceCells"] > 0.35:
                raise AssertionError(f"semantic light target missed emission: {asset['assetId']}")
    if len(socket_asset_ids) != 8:
        raise AssertionError("light socket asset IDs are duplicated")

    shadow_contours = json.loads(SHADOW_CONTOURS.read_text(encoding="utf-8"))
    if shadow_contours["status"] != "offline_candidate_not_unity_integrated":
        raise AssertionError("shadow contours must remain explicitly unintegrated")
    expected_shadow_ids = {
        asset["assetId"] for asset in delivery["assets"]
        if asset["runtimeCandidate"]["runtimeKind"] != "ground_overlay"
    }
    actual_shadow_ids = {asset["assetId"] for asset in shadow_contours["assets"]}
    if actual_shadow_ids != expected_shadow_ids or len(actual_shadow_ids) != 15:
        raise AssertionError("shadow contour coverage mismatch")
    for asset in shadow_contours["assets"]:
        albedo_path = DELIVERY_MANIFEST.parent / asset["albedoPath"]
        if sha256(albedo_path) != asset["albedoSha256"]:
            raise AssertionError(f"shadow contour albedo hash mismatch: {asset['assetId']}")
        if asset["status"] != "candidate_not_unity_verified":
            raise AssertionError(f"shadow contour lost candidate gate: {asset['assetId']}")
        contour = asset["shadowContourCells"]
        if len(contour) % 2 != 0 or asset["pointCount"] != len(contour) // 2:
            raise AssertionError(f"shadow contour coordinate mismatch: {asset['assetId']}")
        if not (5 <= asset["pointCount"] <= 24):
            raise AssertionError(f"shadow contour is rectangle-like or too complex: {asset['assetId']}")
        cols, rows = asset["footprintCells"]
        for index in range(0, len(contour), 2):
            x, y = contour[index], contour[index + 1]
            if not (-cols * 0.5 - 0.35 <= x <= cols * 0.5 + 0.35 and 0.0 <= y <= rows + 0.35):
                raise AssertionError(f"shadow contour exceeds footprint guard: {asset['assetId']}")

    fade_masks = json.loads(FOREGROUND_FADE_MASKS.read_text(encoding="utf-8"))
    if fade_masks["status"] != "offline_candidate_not_unity_integrated" or fade_masks["unityExecuted"] is not False:
        raise AssertionError("foreground fade masks must remain offline candidates")
    if fade_masks["currentRuntimeConsumption"] != "not_consumed_uniform_sprite_alpha_is_current":
        raise AssertionError("foreground fade-mask runtime gate changed")
    if fade_masks["summary"] != {
        "assetCount": 3,
        "alphaIdenticalMasks": 3,
        "reviewImage": "diorama-process-28-foreground-fade-mask-review.png",
    }:
        raise AssertionError("foreground fade-mask summary mismatch")
    expected_fade_ids = {
        asset["assetId"] for asset in delivery["assets"]
        if asset["runtimeCandidate"]["runtimeKind"] == "foreground_setpiece"
    }
    if {entry["assetId"] for entry in fade_masks["assets"]} != expected_fade_ids:
        raise AssertionError("foreground fade masks do not cover every foreground setpiece")
    fade_by_id = {}
    for entry in fade_masks["assets"]:
        fade_by_id[entry["assetId"]] = entry
        albedo_path = WORK / entry["sourceAlbedo"]
        fade_path = WORK / entry["fadeMaskPath"]
        if sha256(albedo_path) != entry["sourceAlbedoSha256"] or sha256(fade_path) != entry["fadeMaskSha256"]:
            raise AssertionError(f"foreground fade-mask hash mismatch: {entry['assetId']}")
        with Image.open(albedo_path) as albedo, Image.open(fade_path) as fade:
            albedo_rgba, fade_rgba = albedo.convert("RGBA"), fade.convert("RGBA")
            if list(fade_rgba.size) != entry["dimensionsPixels"] or fade_rgba.size != albedo_rgba.size:
                raise AssertionError(f"foreground fade-mask dimensions mismatch: {entry['assetId']}")
            if ImageChops.difference(albedo_rgba.getchannel("A"), fade_rgba.getchannel("A")).getbbox() is not None:
                raise AssertionError(f"foreground fade-mask alpha differs from albedo: {entry['assetId']}")
            if any(channel.getextrema() != (255, 255) for channel in fade_rgba.split()[:3]):
                raise AssertionError(f"foreground fade-mask RGB must remain white: {entry['assetId']}")
        if entry["status"] != "candidate_not_unity_verified" or entry["targetFadeAlpha"] != 0.34:
            raise AssertionError(f"foreground fade-mask candidate gate changed: {entry['assetId']}")

    connection_ports = json.loads(CONNECTION_PORTS.read_text(encoding="utf-8"))
    if connection_ports["status"] != "offline_candidate_not_unity_integrated" or connection_ports["unityExecuted"] is not False:
        raise AssertionError("connection ports must remain offline candidates")
    if connection_ports["deliveryPixelsPerCell"] != 128:
        raise AssertionError("connection-port cell pixel contract changed")
    if connection_ports["summary"] != {
        "assetCount": 18,
        "portCount": 27,
        "brokenContinuityAssets": 2,
        "reviewImage": "diorama-process-29-connection-port-review.png",
    }:
        raise AssertionError("connection-port summary mismatch")
    allowed_edges = {"north", "east", "south", "west"}
    allowed_types = {"rail_pair", "heavy_pipe", "power_bundle", "equipment_socket", "dual_service"}
    port_by_delivery_id = {}
    broken_ids = set()
    for entry in connection_ports["assets"]:
        if entry["deliveryAssetId"] in port_by_delivery_id:
            raise AssertionError(f"duplicate connection-port delivery asset: {entry['deliveryAssetId']}")
        port_by_delivery_id[entry["deliveryAssetId"]] = entry
        source_path = WORK / entry["sourceSheet"]
        if sha256(source_path) != entry["sourceSheetSha256"]:
            raise AssertionError(f"connection-port source sheet changed: {entry['assetId']}")
        if entry["status"] != "candidate_not_unity_verified" or entry["topologyResolutionRequired"] is not True:
            raise AssertionError(f"connection-port topology gate changed: {entry['assetId']}")
        if entry["internalContinuity"] == "broken":
            broken_ids.add(entry["assetId"].split("/")[-1])
        elif entry["internalContinuity"] != "connected":
            raise AssertionError(f"unsupported internal continuity: {entry['assetId']}")
        seen_edges = set()
        for port in entry["connectionPorts"]:
            edge = port["edge"]
            if edge not in allowed_edges or port["type"] not in allowed_types or edge in seen_edges:
                raise AssertionError(f"invalid or duplicate connection port: {entry['assetId']}")
            seen_edges.add(edge)
            x, y = port["pixel128"]
            nx, ny = port["normalizedTopLeft"]
            expected = {"north": (64, 0, 0.5, 0.0), "east": (128, 64, 1.0, 0.5),
                        "south": (64, 128, 0.5, 1.0), "west": (0, 64, 0.0, 0.5)}[edge]
            if (x, y, nx, ny) != expected:
                raise AssertionError(f"connection-port boundary coordinate mismatch: {entry['assetId']}")
    if len(port_by_delivery_id) != 18 or broken_ids != {"broken_gap", "capped_machine_scar"}:
        raise AssertionError("connection-port asset or broken-continuity coverage mismatch")
    rail_left = port_by_delivery_id["TR01-PM-RAIL-HORIZONTAL-LEFT"]
    rail_mid = port_by_delivery_id["TR01-PM-RAIL-HORIZONTAL-MIDDLE"]
    rail_right = port_by_delivery_id["TR01-PM-RAIL-HORIZONTAL-RIGHT"]
    if ([port["edge"] for port in rail_left["connectionPorts"]],
            [port["edge"] for port in rail_mid["connectionPorts"]],
            [port["edge"] for port in rail_right["connectionPorts"]]) != (["east"], ["west", "east"], ["west"]):
        raise AssertionError("primary horizontal rail route no longer joins left-middle-right")

    ore_connected_route = json.loads(ORE_CONNECTED_ROUTE.read_text(encoding="utf-8"))
    if (ore_connected_route["status"] != "offline_composition_candidate_not_runtime_integrated"
            or ore_connected_route["unityExecuted"] is not False):
        raise AssertionError("ore connected-route candidate status mismatch")
    if ore_connected_route["compositionImage"] != "diorama-process-30-ore-intake-connected-route-v2.png":
        raise AssertionError("ore connected-route composition image mismatch")
    if ore_connected_route["comparisonImage"] != "diorama-process-31-ore-intake-route-comparison.png":
        raise AssertionError("ore connected-route comparison image mismatch")
    route = ore_connected_route["route"]
    if (route["id"] != "ore_dock" or route["assetIds"] != [
            "TR01-PM-RAIL-HORIZONTAL-LEFT", "TR01-PM-RAIL-HORIZONTAL-MIDDLE",
            "TR01-PM-RAIL-HORIZONTAL-RIGHT"]):
        raise AssertionError("ore connected-route asset sequence changed")
    if route["portEdges"] != [["east"], ["west", "east"], ["west"]]:
        raise AssertionError("ore connected-route port sequence is incompatible")
    if (route["internalConnectionsCompatible"] is not True or route["groundOverlayNonBlocking"] is not True
            or route["crusherScreenOverlapPixels"] < 128):
        raise AssertionError("ore connected route no longer originates under crusher")
    if route["screens"] != [[500, 575, 250], [710, 575, 250], [920, 575, 250]]:
        raise AssertionError("ore connected-route preview screens changed")
    if (len(ore_connected_route["removedGroundOverlays"]) != 1
            or ore_connected_route["nonGroundPlacementsChanged"] is not False
            or ore_connected_route["runtimeTopologyResolutionRequired"] is not True):
        raise AssertionError("ore connected-route safety contract changed")

    crystal_connected_bus = json.loads(CRYSTAL_CONNECTED_BUS.read_text(encoding="utf-8"))
    if (crystal_connected_bus["status"] != "offline_composition_candidate_not_runtime_integrated"
            or crystal_connected_bus["unityExecuted"] is not False):
        raise AssertionError("crystal connected-bus candidate status mismatch")
    if crystal_connected_bus["compositionImage"] != "diorama-process-32-crystal-power-connected-bus-v2.png":
        raise AssertionError("crystal connected-bus composition image mismatch")
    if crystal_connected_bus["comparisonImage"] != "diorama-process-33-crystal-power-bus-comparison.png":
        raise AssertionError("crystal connected-bus comparison image mismatch")
    bus = crystal_connected_bus["route"]
    if (bus["id"] != "power_bus" or bus["portType"] != "heavy_pipe"
            or bus["runtimeAnchor"] != "west_relay_to_north_processor_connection"):
        raise AssertionError("crystal connected-bus semantic contract changed")
    if bus["assetIds"] != [
            "TR01-PM-EQUIPMENT-LEFT-TERMINATION", "TR01-PM-EQUIPMENT-BURIED-THRESHOLD",
            "TR01-PM-EQUIPMENT-RIGHT-TERMINATION"]:
        raise AssertionError("crystal connected-bus asset sequence changed")
    if bus["portEdges"] != [["east"], ["west", "east"], ["west"]]:
        raise AssertionError("crystal connected-bus port sequence is incompatible")
    if (bus["internalConnectionsCompatible"] is not True or bus["groundOverlayNonBlocking"] is not True
            or bus["heroScreenOverlapPixels"] < 64 or bus["backdropScreenOverlapPixels"] < 256):
        raise AssertionError("crystal power bus no longer connects both major machines")
    if bus["screens"] != [[520, 610, 240], [720, 610, 240], [920, 610, 240]]:
        raise AssertionError("crystal connected-bus preview screens changed")
    if (len(crystal_connected_bus["removedGroundOverlays"]) != 2
            or crystal_connected_bus["nonGroundPlacementsChanged"] is not False
            or crystal_connected_bus["runtimeTopologyResolutionRequired"] is not True):
        raise AssertionError("crystal connected-bus safety contract changed")

    ventilation_connected_spine = json.loads(VENTILATION_CONNECTED_SPINE.read_text(encoding="utf-8"))
    if (ventilation_connected_spine["status"] != "offline_composition_candidate_not_runtime_integrated"
            or ventilation_connected_spine["unityExecuted"] is not False):
        raise AssertionError("ventilation connected-spine candidate status mismatch")
    if ventilation_connected_spine["compositionImage"] != "diorama-process-34-ventilation-connected-spine-v2.png":
        raise AssertionError("ventilation connected-spine composition image mismatch")
    if ventilation_connected_spine["comparisonImage"] != "diorama-process-35-ventilation-spine-comparison.png":
        raise AssertionError("ventilation connected-spine comparison image mismatch")
    spine = ventilation_connected_spine["route"]
    expected_spine_assets = [
        "TR01-PM-EQUIPMENT-LEFT-TERMINATION",
        *(["TR01-PM-EQUIPMENT-BURIED-THRESHOLD"] * 4),
        "TR01-PM-EQUIPMENT-RIGHT-TERMINATION",
    ]
    expected_spine_edges = [["east"], *([["west", "east"]] * 4), ["west"]]
    if (spine["id"] != "ventilation_service_spine" or spine["portType"] != "heavy_pipe"
            or spine["runtimeAnchor"] != "west_service_through_turbine_to_east_service_connection"):
        raise AssertionError("ventilation connected-spine semantic contract changed")
    if spine["assetIds"] != expected_spine_assets or spine["portEdges"] != expected_spine_edges:
        raise AssertionError("ventilation connected-spine asset or port sequence changed")
    if spine["screens"] != [[355, 610, 240], [555, 610, 240], [755, 610, 240],
                             [955, 610, 240], [1155, 610, 240], [1355, 610, 240]]:
        raise AssertionError("ventilation connected-spine preview screens changed")
    if (spine["internalConnectionsCompatible"] is not True or spine["groundOverlayNonBlocking"] is not True
            or spine["heroScreenOverlapPixels"] < 480
            or min(spine["leftPylonScreenOverlapPixels"], spine["rightPylonScreenOverlapPixels"]) < 96
            or spine["protectedFloorClearancePixels"] < 32):
        raise AssertionError("ventilation spine no longer joins both pylons and turbine safely")
    if (len(ventilation_connected_spine["removedGroundOverlays"]) != 2
            or ventilation_connected_spine["nonGroundPlacementsChanged"] is not False
            or ventilation_connected_spine["runtimeTopologyResolutionRequired"] is not True):
        raise AssertionError("ventilation connected-spine safety contract changed")

    import_plan = json.loads(UNITY_IMPORT_PLAN.read_text(encoding="utf-8"))
    expected_plan_summary = {
        "assetCount": 39,
        "channelFilesToStage": 195,
        "curatedPassAssets": 15,
        "catalogEligibleAssets": 38,
        "groundOverlayAssets": 24,
        "gatedAssets": 1,
        "mergedLightSockets": 11,
        "mergedShadowContours": 15,
        "roomBlueprints": 3,
        "roomBlueprintPlacements": 32,
        "roomBlueprintImportedAssetRefs": 28,
        "roomBlueprintExistingAssetRefs": 4,
        "fixtureMaps": 3,
        "fixtureBlueprintEvaluations": 9,
        "fixtureFeasibleEvaluations": 9,
        "exactMajorFootprintEvaluations": 9,
        "exactMajorFootprintFits": 9,
        "fullBlueprintFootprintEvaluations": 9,
        "fullBlueprintFootprintFits": 9,
        "foregroundFadeMaskCandidates": 3,
        "connectionPortAssets": 18,
        "connectionPorts": 27,
        "brokenContinuityAssets": 2,
    }
    if import_plan["status"] != "dormant_candidate_plan_not_staged":
        raise AssertionError("Unity import plan must remain dormant")
    if import_plan["summary"] != expected_plan_summary:
        raise AssertionError("Unity import plan summary mismatch")
    if import_plan["targetRoot"] != "Assets/_Project/Art/Environment/PrimaryMatchBoldCandidates":
        raise AssertionError("Unity import plan target escaped candidate root")
    if import_plan["topologyPreflightScript"] != "AgentScripts/InspectPrimaryMatchRoomTopology.cs":
        raise AssertionError("Unity import plan topology preflight path changed")
    safety = import_plan["safety"]
    if not safety["generatedOutsideAssets"] or safety["changesSceneOrPrefab"] or safety["changesActiveRuntimeCatalog"]:
        raise AssertionError("Unity import plan safety contract regressed")
    if not safety["stagingRequiresExplicitLaterRun"] or not safety["importRequiresExplicitLaterRun"]:
        raise AssertionError("Unity import plan lost explicit-run gates")
    if not safety["roomBlueprintsRequireCurrentTopologyResolution"]:
        raise AssertionError("Unity import plan lost topology-resolution gate")
    if not safety["fixturePlacementEvidenceRequired"]:
        raise AssertionError("Unity import plan lost fixture-placement evidence gate")
    if not safety["exactMajorFootprintEvidenceRequired"]:
        raise AssertionError("Unity import plan lost exact-footprint evidence gate")
    if not safety["fullBlueprintFootprintEvidenceRequired"]:
        raise AssertionError("Unity import plan lost full-blueprint footprint evidence gate")
    if not safety["fadeMasksRemainDormantUntilRuntimeConsumerExists"]:
        raise AssertionError("Unity import plan lost dormant fade-mask gate")
    if not safety["connectionPortsRequireTopologyResolution"]:
        raise AssertionError("Unity import plan lost connection-port topology gate")
    for source in import_plan["sourceContracts"].values():
        path = ROOT / source["path"]
        if not path.is_file() or sha256(path) != source["sha256"]:
            raise AssertionError(f"Unity import plan source contract changed: {path}")
    plan_source_ids = set()
    for asset in import_plan["assets"]:
        plan_source_ids.add(asset["sourceVariantId"])
        if asset["status"] != "dormant_candidate_plan_not_staged":
            raise AssertionError(f"Unity import asset lost dormant gate: {asset['assetId']}")
        if set(asset["targetAssetPaths"]) != {"albedo", "normal", "ao", "emission", "mask"}:
            raise AssertionError(f"Unity import target channels incomplete: {asset['assetId']}")
        fade_candidate = asset["runtimeCandidate"].get("fadeMaskCandidate")
        if asset["assetId"] in expected_fade_ids:
            if fade_candidate is None or fade_candidate["sha256"] != fade_by_id[asset["assetId"]]["fadeMaskSha256"]:
                raise AssertionError(f"Unity import plan fade-mask candidate missing: {asset['assetId']}")
        elif fade_candidate is not None:
            raise AssertionError(f"Unity import plan attached fade mask to non-foreground asset: {asset['assetId']}")
        ports_candidate = asset["runtimeCandidate"].get("connectionPortsCandidate")
        if asset["assetId"] in port_by_delivery_id:
            source_ports = port_by_delivery_id[asset["assetId"]]
            if (ports_candidate is None or ports_candidate["ports"] != source_ports["connectionPorts"]
                    or ports_candidate["internalContinuity"] != source_ports["internalContinuity"]
                    or ports_candidate["topologyResolutionRequired"] is not True):
                raise AssertionError(f"Unity import plan connection ports missing: {asset['assetId']}")
        elif ports_candidate is not None:
            raise AssertionError(f"Unity import plan attached ports to unrelated asset: {asset['assetId']}")
        for target in asset["targetAssetPaths"].values():
            if not target.startswith(import_plan["targetRoot"] + "/"):
                raise AssertionError(f"Unity import target escaped candidate root: {target}")
    if plan_source_ids != seen_variant_ids:
        raise AssertionError("Unity import plan does not cover all runtime candidates")
    plan_asset_ids = {asset["assetId"] for asset in import_plan["assets"]}
    blueprint_rooms = import_plan["roomBlueprints"]
    if {room["roomId"] for room in blueprint_rooms} != {room["id"] for room in landscape_entries}:
        raise AssertionError("Unity import room blueprints do not cover landscape rooms")
    blueprint_placements = [blueprint_placement for room in blueprint_rooms
                            for blueprint_placement in room["placements"]]
    if len(blueprint_placements) != 32:
        raise AssertionError("Unity import room blueprint placement count mismatch")
    imported_refs = 0
    existing_refs = 0
    for room in blueprint_rooms:
        if room["status"] != "candidate_requires_current_topology_resolution":
            raise AssertionError(f"Unity import room blueprint lost topology gate: {room['roomId']}")
        if room["minimumOpenCenterWidthFraction"] < 0.45:
            raise AssertionError(f"Unity import room blueprint clearance regressed: {room['roomId']}")
        if not {"backdrop", "hero", "foreground", "openFloor"}.issubset(room["runtimeAnchors"]):
            raise AssertionError(f"Unity import room blueprint anchors incomplete: {room['roomId']}")
        if room["roomId"] == "ore_intake":
            if (room["compositionImage"] != ore_connected_route["compositionImage"]
                    or room["offlineCompositionRevision"] != "connected_route_v2"):
                raise AssertionError("Unity import plan did not adopt ore connected-route V2")
            route_screens = [placement["offlinePreviewScreen"] for placement in room["placements"]
                             if placement["runtimeAnchorRole"] == "route"]
            if route_screens != route["screens"]:
                raise AssertionError("Unity import plan ore route screens differ from V2 contract")
        elif room["roomId"] == "crystal_power":
            if (room["compositionImage"] != crystal_connected_bus["compositionImage"]
                    or room["offlineCompositionRevision"] != "connected_bus_v2"):
                raise AssertionError("Unity import plan did not adopt crystal connected-bus V2")
            bus_placements = [placement for placement in room["placements"]
                              if placement["runtimeAnchorRole"] == "route"]
            if ([placement["offlinePreviewScreen"] for placement in bus_placements] != bus["screens"]
                    or any(placement["runtimeAnchor"] != bus["runtimeAnchor"] for placement in bus_placements)):
                raise AssertionError("Unity import plan crystal bus differs from V2 contract")
        elif room["roomId"] == "ventilation_service":
            if (room["compositionImage"] != ventilation_connected_spine["compositionImage"]
                    or room["offlineCompositionRevision"] != "connected_spine_v2"):
                raise AssertionError("Unity import plan did not adopt ventilation connected-spine V2")
            spine_placements = [placement for placement in room["placements"]
                                if placement["runtimeAnchorRole"] == "route"]
            if ([placement["offlinePreviewScreen"] for placement in spine_placements] != spine["screens"]
                    or any(placement["runtimeAnchor"] != spine["runtimeAnchor"] for placement in spine_placements)):
                raise AssertionError("Unity import plan ventilation spine differs from V2 contract")
        elif room["offlineCompositionRevision"] != "v1":
            raise AssertionError(f"unexpected room composition revision: {room['roomId']}")
        for blueprint_placement in room["placements"]:
            if blueprint_placement["candidateImportedByThisPlan"]:
                imported_refs += 1
                if blueprint_placement["assetId"] not in plan_asset_ids:
                    raise AssertionError(f"room blueprint imported ref missing: {blueprint_placement['assetId']}")
            if blueprint_placement["existingAssetReference"]:
                existing_refs += 1
                if not blueprint_placement["assetId"].startswith("TR01-PRIMARYMATCH-SERVICE-PYLONS-3COL/"):
                    raise AssertionError(f"room blueprint unexpected existing ref: {blueprint_placement['assetId']}")
            if blueprint_placement["mustAvoidOpenFloor"] != (blueprint_placement["layer"] != "ground"):
                raise AssertionError(f"room blueprint clearance flag mismatch: {room['roomId']}")
            if (blueprint_placement["runtimeAnchorRole"] != "perimeterDetail"
                    and not blueprint_placement["runtimeAnchor"]):
                raise AssertionError(f"room blueprint runtime anchor missing: {room['roomId']}")
    if (imported_refs, existing_refs) != (28, 4):
        raise AssertionError("Unity import room blueprint ref counts mismatch")
    for script in (STAGING_SCRIPT, UNITY_IMPORT_SCRIPT, UNITY_VERIFY_SCRIPT, UNITY_TOPOLOGY_PREFLIGHT):
        if not script.is_file() or script.stat().st_size < 100:
            raise AssertionError(f"Unity dormant handoff script missing: {script}")
    staging_text = STAGING_SCRIPT.read_text(encoding="utf-8-sig")
    import_text = UNITY_IMPORT_SCRIPT.read_text(encoding="utf-8-sig")
    verify_text = UNITY_VERIFY_SCRIPT.read_text(encoding="utf-8-sig")
    topology_text = UNITY_TOPOLOGY_PREFLIGHT.read_text(encoding="utf-8-sig")
    if "[switch]$Apply" not in staging_text or "if (-not $Apply)" not in staging_text:
        raise AssertionError("Unity staging script lost dry-run-by-default gate")
    if "primary-match-bold-stage-receipt.json" not in import_text:
        raise AssertionError("Unity import script lost staging receipt gate")
    if "catalog.EditorSetEntries(defs)" not in import_text:
        raise AssertionError("Unity import script no longer builds isolated candidate catalog")
    forbidden_import_tokens = (
        ".Catalog = catalog", "EditorSceneManager.SaveScene", "PrefabUtility.SaveAsPrefabAsset"
    )
    if any(token in import_text for token in forbidden_import_tokens):
        raise AssertionError("Unity import script gained active scene/prefab/runtime mutation")
    if "activeAssignments != 0" not in verify_text or "verifyCatalog.Count != 38" not in verify_text:
        raise AssertionError("Unity import verifier lost isolation/count checks")
    if "inspected_read_only_no_runtime_mutation" not in topology_text or "runtimeMutationPerformed\\\": false" not in topology_text:
        raise AssertionError("Unity topology preflight lost read-only evidence fields")
    forbidden_topology_tokens = (
        ".Spawn(", ".Clear(", ".Damage(", ".ForceClear(", ".SetTile(",
        "EditorSceneManager.SaveScene", "PrefabUtility.SaveAsPrefabAsset", "AssetDatabase.CreateAsset",
    )
    if any(token in topology_text for token in forbidden_topology_tokens):
        raise AssertionError("Unity topology preflight gained a runtime or asset mutation")

    applied_evidence = (UNITY_STAGE_RECEIPT, UNITY_TOPOLOGY_REPORT, UNITY_IMPORT_REPORT,
                        UNITY_PROMOTION_REPORT, UNITY_RUNTIME_CATALOG, UNITY_RUNTIME_DECORATOR,
                        *UNITY_RUNTIME_CAPTURES)
    for path in applied_evidence:
        if not path.is_file() or path.stat().st_size < 100:
            raise AssertionError(f"Unity runtime application evidence missing: {path}")
    import_report = UNITY_IMPORT_REPORT.read_text(encoding="utf-8-sig")
    if not all(token in import_report for token in
               ("channels=195", "sprites=39", "catalogEntries=38", "activeRuntimeAssignments=0")):
        raise AssertionError("Unity candidate import evidence mismatch")
    promotion_report = UNITY_PROMOTION_REPORT.read_text(encoding="utf-8-sig")
    if not all(token in promotion_report for token in
               ("status=promoted_to_runtime_catalog", "candidateEntries=38",
                "legacyEntries=27", "runtimeEntries=65")):
        raise AssertionError("Unity runtime promotion evidence mismatch")
    topology_report = json.loads(UNITY_TOPOLOGY_REPORT.read_text(encoding="utf-8-sig"))
    if (topology_report["status"] != "inspected_read_only_no_runtime_mutation"
            or topology_report["runtimeMutationPerformed"] is not False
            or topology_report["world"]["connectedOpenCells"] < 1):
        raise AssertionError("Unity topology preflight evidence mismatch")
    for capture in UNITY_RUNTIME_CAPTURES:
        with Image.open(capture) as image:
            if image.width < 900 or image.height < 500 or image.getbbox() is None:
                raise AssertionError(f"Unity Game View capture invalid: {capture}")

    report = {
        "status": "verified_resource_first_and_unity_runtime_applied",
        "runtimeIntegrationVerified": True,
        "unityImportPerformed": True,
        "counts": {
            "candidateSheets": len(candidates),
            "occupiedVariants": verified_cells,
            "processImages": len(PROCESS_IMAGES),
            "sheetPreviewChannels": channel_images,
            "normalizedDeliveryAssets": len(delivery["assets"]),
            "normalizedDeliveryChannels": delivery_channel_images,
            "curatedPlacements": len(placements),
            "lightSocketAssets": len(socket_asset_ids),
            "lightSockets": light_sockets["summary"]["socketCount"],
            "shadowContourAssets": len(actual_shadow_ids),
            "roomIdentities": len(rooms),
            "landscapeRoomCompositions": len(landscape_entries),
            "dormantUnityImportAssets": import_plan["summary"]["assetCount"],
            "dormantUnityImportCatalogEntries": import_plan["summary"]["catalogEligibleAssets"],
            "runtimeCatalogEntries": 65,
            "runtimeGameViewCaptures": len(UNITY_RUNTIME_CAPTURES),
        },
        "processImages": {
            path.name: {"sha256": sha256(path), "dimensionsPixels": [1920, 1080]}
            for path in PROCESS_IMAGES
        },
        "contracts": {
            str(path.relative_to(ROOT)).replace("\\", "/"): {"sha256": sha256(path)}
            for path in (MANIFEST, placement_path, RUNTIME_CATALOG, DELIVERY_MANIFEST,
                         CURATED_LAYOUT, ROOM_VARIANTS, LANDSCAPE_ROOM_LAYOUTS, FIXTURE_SIMULATION,
                         FOOTPRINT_SIMULATION, FULL_BLUEPRINT_SIMULATION, REFERENCE_COMPARISON,
                         LIGHTING_CALIBRATION_V1, LIGHTING_CALIBRATION_V2, FOREGROUND_FADE_MASKS,
                         CONNECTION_PORTS, ORE_CONNECTED_ROUTE, CRYSTAL_CONNECTED_BUS,
                         VENTILATION_CONNECTED_SPINE, LIGHT_SOCKETS,
                         SHADOW_CONTOURS, channel_report_path,
                         UNITY_IMPORT_PLAN, STAGING_SCRIPT, UNITY_IMPORT_SCRIPT, UNITY_VERIFY_SCRIPT,
                         UNITY_TOPOLOGY_PREFLIGHT, UNITY_STAGE_RECEIPT, UNITY_TOPOLOGY_REPORT,
                         UNITY_IMPORT_REPORT, UNITY_PROMOTION_REPORT, UNITY_RUNTIME_CATALOG,
                         UNITY_RUNTIME_DECORATOR, *UNITY_RUNTIME_CAPTURES)
        },
        "remainingRuntimeGates": placement["runtimeGates"],
        "landscapeOpenFloorBlockingCoverage": landscape_coverage,
    }
    VERIFICATION_REPORT.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    print(f"OK: {len(candidates)} candidate sheets, {verified_cells} occupied cells, "
          f"{len(PROCESS_IMAGES)} diorama process images, {channel_images} preview channels, "
          f"{delivery_channel_images} normalized delivery channels, Unity runtime catalog 65, "
          "three Game View identities verified")
    print(f"Report: {VERIFICATION_REPORT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
