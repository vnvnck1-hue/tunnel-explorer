#!/usr/bin/env python3
"""Consolidate offline Primary Match contracts into a dormant Unity import plan.

The plan lives in AgentScripts (outside Assets), so generating it cannot trigger
Unity import or touch the currently open scene/play state.
"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / "art-production/test-room-v01/working/primary-match-v2"
DELIVERY = BASE / "delivery-candidates/manifest.json"
LAYOUT = BASE / "curated-diorama-layout.json"
RUNTIME_CATALOG = BASE / "runtime-catalog-candidates.json"
LANDSCAPE_ROOMS = BASE / "curated-room-landscape-layouts.json"
FIXTURE_SIMULATION = BASE / "fixture-placement-simulation.json"
FOOTPRINT_SIMULATION = BASE / "fixture-major-footprint-simulation.json"
FULL_BLUEPRINT_SIMULATION = BASE / "fixture-full-blueprint-footprint-simulation.json"
LIGHTS = BASE / "light-socket-candidates.json"
SHADOWS = BASE / "shadow-contour-candidates.json"
FADE_MASKS = BASE / "foreground-fade-mask-candidates.json"
CONNECTION_PORTS = BASE / "connection-port-candidates.json"
ORE_CONNECTED_ROUTE = BASE / "ore-intake-connected-route-v2.json"
CRYSTAL_CONNECTED_BUS = BASE / "crystal-power-connected-bus-v2.json"
VENTILATION_CONNECTED_SPINE = BASE / "ventilation-connected-spine-v2.json"
OUT = ROOT / "unity/TunnelCrew/AgentScripts/primary-match-bold-import-plan.json"
TARGET_ROOT = "Assets/_Project/Art/Environment/PrimaryMatchBoldCandidates"


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def main() -> None:
    delivery = json.loads(DELIVERY.read_text(encoding="utf-8"))
    layout = json.loads(LAYOUT.read_text(encoding="utf-8"))
    runtime_catalog = json.loads(RUNTIME_CATALOG.read_text(encoding="utf-8"))
    landscape_rooms = json.loads(LANDSCAPE_ROOMS.read_text(encoding="utf-8"))
    fixture_simulation = json.loads(FIXTURE_SIMULATION.read_text(encoding="utf-8"))
    if (fixture_simulation["status"] != "verified_offline_against_mapgen_fixtures"
            or fixture_simulation["summary"]["feasibleEvaluations"] != fixture_simulation["summary"]["evaluations"]):
        raise ValueError("map fixture placement simulation is incomplete or has failed evaluations")
    footprint_simulation = json.loads(FOOTPRINT_SIMULATION.read_text(encoding="utf-8"))
    if (footprint_simulation["status"] != "verified_offline_exact_major_footprints"
            or footprint_simulation["summary"]["exactMajorFootprintFits"] != footprint_simulation["summary"]["evaluations"]):
        raise ValueError("major footprint simulation is incomplete or has failed evaluations")
    full_blueprint_simulation = json.loads(FULL_BLUEPRINT_SIMULATION.read_text(encoding="utf-8"))
    if (full_blueprint_simulation["status"] != "verified_offline_full_blueprint_footprints"
            or full_blueprint_simulation["summary"]["fullBlueprintFits"] != full_blueprint_simulation["summary"]["evaluations"]):
        raise ValueError("full blueprint footprint simulation is incomplete or has failed evaluations")
    lights = json.loads(LIGHTS.read_text(encoding="utf-8"))
    shadows = json.loads(SHADOWS.read_text(encoding="utf-8"))
    fade_masks = json.loads(FADE_MASKS.read_text(encoding="utf-8"))
    connection_ports = json.loads(CONNECTION_PORTS.read_text(encoding="utf-8"))
    ore_connected_route = json.loads(ORE_CONNECTED_ROUTE.read_text(encoding="utf-8"))
    crystal_connected_bus = json.loads(CRYSTAL_CONNECTED_BUS.read_text(encoding="utf-8"))
    ventilation_connected_spine = json.loads(VENTILATION_CONNECTED_SPINE.read_text(encoding="utf-8"))

    curated = {
        entry["runtimeVariantId"]
        for entry in layout["placements"]
        if entry["runtimeVariantId"].startswith("TR01-PRIMARYMATCH-")
    }
    light_by_id = {entry["assetId"]: entry["sockets"] for entry in lights["assets"]}
    shadow_by_id = {entry["assetId"]: entry["shadowContourCells"] for entry in shadows["assets"]}
    fade_by_id = {entry["assetId"]: entry for entry in fade_masks["assets"]}
    ports_by_delivery_id = {entry["deliveryAssetId"]: entry for entry in connection_ports["assets"]}
    delivery_id_by_source = {entry["sourceVariantId"]: entry["assetId"] for entry in delivery["assets"]}
    source_variant_by_cell = {
        (sheet["path"], variant["cell"]["column"], variant["cell"]["rowFromTop"]): variant["assetId"]
        for sheet in runtime_catalog["sheets"]
        for variant in sheet["variants"]
    }
    existing_by_cell = {
        ("tr01_primarymatch_service_pylons_3col_source.png", 0, 0):
            "TR01-PRIMARYMATCH-SERVICE-PYLONS-3COL/cyan",
        ("tr01_primarymatch_service_pylons_3col_source.png", 2, 0):
            "TR01-PRIMARYMATCH-SERVICE-PYLONS-3COL/amber",
    }

    assets = []
    for entry in delivery["assets"]:
        runtime = dict(entry["runtimeCandidate"])
        replacement = runtime.get("replacementAssetId", "")
        if replacement:
            runtime["replacementAssetId"] = delivery_id_by_source.get(replacement, replacement)
        kind = runtime["runtimeKind"]
        # All non-gated visual definitions belong in the runtime-ready catalog. Ground overlays
        # are non-blocking SpriteRenderer decorations spawned by the same footpoint system.
        catalog_eligible = kind != "elevated_surface_gated"
        if entry["assetId"] in shadow_by_id:
            runtime["shadowContourCells"] = shadow_by_id[entry["assetId"]]
        runtime["lightSockets"] = light_by_id.get(entry["assetId"], [])
        if entry["assetId"] in fade_by_id:
            fade = fade_by_id[entry["assetId"]]
            runtime["fadeMaskCandidate"] = {
                "path": fade["fadeMaskPath"],
                "sha256": fade["fadeMaskSha256"],
                "alphaIdenticalToAlbedo": fade["alphaIdenticalToAlbedo"],
                "currentRuntimeConsumption": "not_consumed_uniform_sprite_alpha_is_current",
            }
        if entry["assetId"] in ports_by_delivery_id:
            port_entry = ports_by_delivery_id[entry["assetId"]]
            runtime["connectionPortsCandidate"] = {
                "ports": port_entry["connectionPorts"],
                "internalContinuity": port_entry["internalContinuity"],
                "topologyResolutionRequired": port_entry["topologyResolutionRequired"],
                "currentRuntimeConsumption": "not_consumed_by_candidate_importer",
            }

        targets = {}
        for channel, relative in entry["channels"].items():
            filename = Path(relative).name
            targets[channel] = f"{TARGET_ROOT}/{channel}/{filename}"

        assets.append({
            "assetId": entry["assetId"],
            "sourceVariantId": entry["sourceVariantId"],
            "status": "dormant_candidate_plan_not_staged",
            "selectedForCuratedPass": entry["sourceVariantId"] in curated,
            "catalogEligible": catalog_eligible,
            "spawnEnabledCandidate": catalog_eligible,
            "groundOverlayCandidate": kind == "ground_overlay",
            "gated": kind == "elevated_surface_gated",
            "gatedReason": "walkability_and_visual_height_rules_required" if kind == "elevated_surface_gated" else "",
            "dimensionsPixels": entry["dimensionsPixels"],
            "pivotNormalizedBottomOrigin": entry["pivotNormalized"],
            "sourceChannels": entry["channels"],
            "sourceChannelHashes": entry["channelHashes"],
            "targetAssetPaths": targets,
            "runtimeCandidate": runtime,
        })

    room_blueprints = []
    for room in landscape_rooms["rooms"]:
        resolved = []
        room_placements = list(room["placements"])
        if room["id"] == "ventilation_service":
            removed_signatures = {
                (entry["source"], tuple(entry["cell"]), entry["layer"])
                for entry in ventilation_connected_spine["removedGroundOverlays"]
            }
            room_placements = [
                entry for entry in room_placements
                if (entry["source"], tuple(entry["cell"]), entry["layer"]) not in removed_signatures
            ]
            for index, screen in enumerate(ventilation_connected_spine["route"]["screens"]):
                column = 0 if index == 0 else 2 if index == len(ventilation_connected_spine["route"]["screens"]) - 1 else 1
                room_placements.append({
                    "source": "tr01_primarymatch_equipment_transitions_3x3_source.png",
                    "grid": [3, 3], "cell": [column, 0], "screen": screen,
                    "layer": "ground", "route": ventilation_connected_spine["route"]["id"],
                })
        if room["id"] == "crystal_power":
            removed_signatures = {
                (entry["source"], tuple(entry["cell"]), entry["layer"])
                for entry in crystal_connected_bus["removedGroundOverlays"]
            }
            room_placements = [
                entry for entry in room_placements
                if (entry["source"], tuple(entry["cell"]), entry["layer"]) not in removed_signatures
            ]
            for column, screen in enumerate(crystal_connected_bus["route"]["screens"]):
                room_placements.append({
                    "source": "tr01_primarymatch_equipment_transitions_3x3_source.png",
                    "grid": [3, 3], "cell": [column, 0], "screen": screen,
                    "layer": "ground", "route": crystal_connected_bus["route"]["id"],
                })
        route_screens = iter(ore_connected_route["route"]["screens"]) if room["id"] == "ore_intake" else None
        removed_overlay = ore_connected_route["removedGroundOverlays"][0] if room["id"] == "ore_intake" else None
        for original_placement in room_placements:
            if (removed_overlay is not None
                    and original_placement["source"] == removed_overlay["source"]
                    and original_placement["cell"] == removed_overlay["cell"]
                    and original_placement["layer"] == removed_overlay["layer"]):
                continue
            placement = dict(original_placement)
            if route_screens is not None and placement.get("route") == ore_connected_route["route"]["id"]:
                placement["screen"] = next(route_screens)
            col, row = placement["cell"]
            source_variant_id = source_variant_by_cell.get((placement["source"], col, row))
            existing_reference = False
            if source_variant_id is None:
                source_variant_id = existing_by_cell.get((placement["source"], col, row))
                existing_reference = source_variant_id is not None
            if source_variant_id is None:
                raise ValueError(f"room {room['id']} cannot resolve {placement['source']} cell {col},{row}")

            if placement["source"].startswith("tr01_primarymatch_monumental_wall_modules"):
                anchor_role = "backdrop"
            elif placement["source"].startswith("tr01_primarymatch_hero_machinery"):
                anchor_role = "hero"
            elif placement["source"].startswith("tr01_primarymatch_foreground_depth"):
                anchor_role = "foreground"
            elif "route" in placement:
                anchor_role = "route"
            elif placement["source"].startswith("tr01_primarymatch_service_pylons"):
                anchor_role = "servicePair" if "servicePair" in room["runtimeAnchors"] else "controlSocket"
            else:
                anchor_role = "perimeterDetail"

            center_x = placement["screen"][0] + placement["screen"][2] * 0.5
            edge_preference = "west" if center_x < 640 else "east" if center_x > 1280 else "center"
            runtime_anchor = room["runtimeAnchors"].get(anchor_role, "open_floor_perimeter")
            if room["id"] == "crystal_power" and anchor_role == "route":
                runtime_anchor = crystal_connected_bus["route"]["runtimeAnchor"]
            if room["id"] == "ventilation_service" and anchor_role == "route":
                runtime_anchor = ventilation_connected_spine["route"]["runtimeAnchor"]
            resolved.append({
                "sourceVariantId": source_variant_id,
                "assetId": delivery_id_by_source.get(source_variant_id, source_variant_id),
                "candidateImportedByThisPlan": source_variant_id in delivery_id_by_source,
                "existingAssetReference": existing_reference,
                "layer": placement["layer"],
                "runtimeAnchorRole": anchor_role,
                "runtimeAnchor": runtime_anchor,
                "edgePreference": edge_preference,
                "mustAvoidOpenFloor": placement["layer"] != "ground",
                "offlinePreviewScreen": placement["screen"],
            })
        room_blueprints.append({
            "roomId": room["id"],
            "status": "candidate_requires_current_topology_resolution",
            "compositionImage": (ore_connected_route["compositionImage"] if room["id"] == "ore_intake"
                                 else crystal_connected_bus["compositionImage"] if room["id"] == "crystal_power"
                                 else ventilation_connected_spine["compositionImage"] if room["id"] == "ventilation_service"
                                 else room["compositionImage"]),
            "offlineCompositionRevision": ("connected_route_v2" if room["id"] == "ore_intake"
                                           else "connected_bus_v2" if room["id"] == "crystal_power"
                                           else "connected_spine_v2" if room["id"] == "ventilation_service" else "v1"),
            "openFloorRectPixels": room["openFloorRectPixels"],
            "minimumOpenCenterWidthFraction": landscape_rooms["minimumOpenCenterWidthFraction"],
            "runtimeAnchors": room["runtimeAnchors"],
            "placements": resolved,
        })

    result = {
        "planId": "TR01-PRIMARYMATCH-BOLD-CANDIDATE-IMPORT-V2",
        "status": "dormant_candidate_plan_not_staged",
        "unityProject": "unity/TunnelCrew",
        "requiredUnityVersion": "6000.3.15f1",
        "targetRoot": TARGET_ROOT,
        "candidateCatalogPath": "Assets/_Project/Data/Visual/SetPieceCatalog_PrimaryMatchBoldCandidate.asset",
        "candidateMaterialRoot": "Assets/_Project/Data/Visual/PrimaryMatchBoldCandidateMaterials",
        "topologyPreflightScript": "AgentScripts/InspectPrimaryMatchRoomTopology.cs",
        "topologyPreflightReport": "AgentScripts/primary-match-room-topology-preflight.json",
        "safety": {
            "generatedOutsideAssets": True,
            "changesSceneOrPrefab": False,
            "changesActiveRuntimeCatalog": False,
            "stagingRequiresExplicitLaterRun": True,
            "importRequiresExplicitLaterRun": True,
            "roomBlueprintsRequireCurrentTopologyResolution": True,
            "fixturePlacementEvidenceRequired": True,
            "exactMajorFootprintEvidenceRequired": True,
            "fullBlueprintFootprintEvidenceRequired": True,
            "fadeMasksRemainDormantUntilRuntimeConsumerExists": True,
            "connectionPortsRequireTopologyResolution": True,
        },
        "sourceContracts": {
            "delivery": {"path": str(DELIVERY.relative_to(ROOT)).replace("\\", "/"), "sha256": sha256(DELIVERY)},
            "layout": {"path": str(LAYOUT.relative_to(ROOT)).replace("\\", "/"), "sha256": sha256(LAYOUT)},
            "runtimeCatalog": {"path": str(RUNTIME_CATALOG.relative_to(ROOT)).replace("\\", "/"), "sha256": sha256(RUNTIME_CATALOG)},
            "landscapeRooms": {"path": str(LANDSCAPE_ROOMS.relative_to(ROOT)).replace("\\", "/"), "sha256": sha256(LANDSCAPE_ROOMS)},
            "fixturePlacementSimulation": {"path": str(FIXTURE_SIMULATION.relative_to(ROOT)).replace("\\", "/"), "sha256": sha256(FIXTURE_SIMULATION)},
            "majorFootprintSimulation": {"path": str(FOOTPRINT_SIMULATION.relative_to(ROOT)).replace("\\", "/"), "sha256": sha256(FOOTPRINT_SIMULATION)},
            "fullBlueprintFootprintSimulation": {"path": str(FULL_BLUEPRINT_SIMULATION.relative_to(ROOT)).replace("\\", "/"), "sha256": sha256(FULL_BLUEPRINT_SIMULATION)},
            "lights": {"path": str(LIGHTS.relative_to(ROOT)).replace("\\", "/"), "sha256": sha256(LIGHTS)},
            "shadows": {"path": str(SHADOWS.relative_to(ROOT)).replace("\\", "/"), "sha256": sha256(SHADOWS)},
            "foregroundFadeMasks": {"path": str(FADE_MASKS.relative_to(ROOT)).replace("\\", "/"), "sha256": sha256(FADE_MASKS)},
            "connectionPorts": {"path": str(CONNECTION_PORTS.relative_to(ROOT)).replace("\\", "/"), "sha256": sha256(CONNECTION_PORTS)},
            "oreConnectedRoute": {"path": str(ORE_CONNECTED_ROUTE.relative_to(ROOT)).replace("\\", "/"), "sha256": sha256(ORE_CONNECTED_ROUTE)},
            "crystalConnectedBus": {"path": str(CRYSTAL_CONNECTED_BUS.relative_to(ROOT)).replace("\\", "/"), "sha256": sha256(CRYSTAL_CONNECTED_BUS)},
            "ventilationConnectedSpine": {"path": str(VENTILATION_CONNECTED_SPINE.relative_to(ROOT)).replace("\\", "/"), "sha256": sha256(VENTILATION_CONNECTED_SPINE)},
        },
        "roomBlueprints": room_blueprints,
        "assets": assets,
        "summary": {
            "assetCount": len(assets),
            "channelFilesToStage": len(assets) * 5,
            "curatedPassAssets": sum(1 for asset in assets if asset["selectedForCuratedPass"]),
            "catalogEligibleAssets": sum(1 for asset in assets if asset["catalogEligible"]),
            "groundOverlayAssets": sum(1 for asset in assets if asset["groundOverlayCandidate"]),
            "gatedAssets": sum(1 for asset in assets if asset["gated"]),
            "mergedLightSockets": sum(len(asset["runtimeCandidate"]["lightSockets"]) for asset in assets),
            "mergedShadowContours": sum(bool(asset["runtimeCandidate"]["shadowContourCells"]) for asset in assets),
            "roomBlueprints": len(room_blueprints),
            "roomBlueprintPlacements": sum(len(room["placements"]) for room in room_blueprints),
            "roomBlueprintImportedAssetRefs": sum(
                placement["candidateImportedByThisPlan"]
                for room in room_blueprints for placement in room["placements"]
            ),
            "roomBlueprintExistingAssetRefs": sum(
                placement["existingAssetReference"]
                for room in room_blueprints for placement in room["placements"]
            ),
            "fixtureMaps": fixture_simulation["summary"]["fixtureCount"],
            "fixtureBlueprintEvaluations": fixture_simulation["summary"]["evaluations"],
            "fixtureFeasibleEvaluations": fixture_simulation["summary"]["feasibleEvaluations"],
            "exactMajorFootprintEvaluations": footprint_simulation["summary"]["evaluations"],
            "exactMajorFootprintFits": footprint_simulation["summary"]["exactMajorFootprintFits"],
            "fullBlueprintFootprintEvaluations": full_blueprint_simulation["summary"]["evaluations"],
            "fullBlueprintFootprintFits": full_blueprint_simulation["summary"]["fullBlueprintFits"],
            "foregroundFadeMaskCandidates": fade_masks["summary"]["assetCount"],
            "connectionPortAssets": connection_ports["summary"]["assetCount"],
            "connectionPorts": connection_ports["summary"]["portCount"],
            "brokenContinuityAssets": connection_ports["summary"]["brokenContinuityAssets"],
        },
    }
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {OUT.relative_to(ROOT)}: {result['summary']}")


if __name__ == "__main__":
    main()
