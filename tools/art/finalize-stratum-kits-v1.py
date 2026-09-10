from __future__ import annotations

import hashlib
import json
import shutil
from pathlib import Path

import numpy as np
from PIL import Image, ImageEnhance, ImageStat

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "art-production" / "test-room-v01"
GEN = Path(r"C:\Users\Loadcomplete\.codex\generated_images\01a08584-6c17-78d2-9ad7-fb25c93a0e0d")
SOURCE = ART / "source" / "stratum_kits_v1"
WORKING = ART / "working" / "stratum_kits_v1"
ALBEDO = ART / "approved" / "albedo"
QA = ART / "qa"
META = ART / "metadata"
UNITY = ROOT / "unity" / "TunnelCrew" / "Assets" / "Art" / "Visual" / "ReferenceCalibrationV1"
SIZE = 128
EDGE = 4

NAMES = []
for prefix in ("tr01_stratum2_", "tr01_stratum3_", "tr01_abyss_"):
    for kind in ("floor", "wall_top", "wall_top_rim", "wall_front"):
        NAMES.extend(f"{prefix}{kind}_{variant}_albedo.png" for variant in "abc")
NAMES.extend(("tr01_reference_boss_wall_top_a_albedo.png", "tr01_reference_boss_wall_front_a_albedo.png"))


def generated_sources():
    files = sorted(
        (p for p in GEN.glob("exec-*.png") if p.stat().st_mtime >= 1789032600),
        key=lambda p: p.stat().st_mtime,
    )
    if len(files) != len(NAMES):
        raise RuntimeError(f"Expected {len(NAMES)} generated sources, found {len(files)}")
    return files


def repeat_mode(name):
    return "nesw" if ("floor_" in name or ("wall_top_" in name and "rim" not in name)) else "ew"


def lock_edges(image, mode):
    arr = np.asarray(image.convert("RGBA"), dtype=np.uint16).copy()
    if mode == "nesw":
        band = ((arr[:EDGE] + arr[-EDGE:]) // 2).astype(np.uint8)
        arr[:EDGE] = band
        arr[-EDGE:] = band
    band = ((arr[:, :EDGE] + arr[:, -EDGE:]) // 2).astype(np.uint8)
    arr[:, :EDGE] = band
    arr[:, -EDGE:] = band
    return Image.fromarray(arr.astype(np.uint8), "RGBA")


def finish_source(path, name):
    image = Image.open(path).convert("RGB").resize((SIZE, SIZE), Image.Resampling.LANCZOS)
    image = ImageEnhance.Contrast(image).enhance(1.04)
    image = image.quantize(colors=48, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).convert("RGBA")
    image.putalpha(255)
    return lock_edges(image, repeat_mode(name))


def copy_meta(template, target_name, pivot):
    text = template.read_text(encoding="utf-8")
    lines = [line for line in text.splitlines() if not line.startswith("guid: ")]
    seed = f"TunnelCrew/ReferenceCalibrationV1/{target_name}".encode()
    lines.insert(1, "guid: " + hashlib.md5(seed).hexdigest())
    text = "\n".join(lines) + "\n"
    text = text.replace("spritePivot: {x: 0.5, y: 0.5}", f"spritePivot: {{x: {pivot[0]}, y: {pivot[1]}}}")
    text = text.replace("spritePivot: {x: 0.5, y: 0}", f"spritePivot: {{x: {pivot[0]}, y: {pivot[1]}}}")
    (UNITY / f"{target_name}.meta").write_text(text, encoding="utf-8")


def edge_mse(image, mode):
    arr = np.asarray(image.convert("RGB"), dtype=np.float32)
    result = {"ew": float(np.mean((arr[:, :EDGE] - arr[:, -EDGE:]) ** 2))}
    if mode == "nesw":
        result["ns"] = float(np.mean((arr[:EDGE] - arr[-EDGE:]) ** 2))
    return result


def make_repeat_sheet(names, output):
    tiles = [Image.open(ALBEDO / name).convert("RGB") for name in names]
    sheet = Image.new("RGB", (SIZE * 6, SIZE * 6))
    for y in range(6):
        for x in range(6):
            sheet.paste(tiles[(x + y) % len(tiles)], (x * SIZE, y * SIZE))
    sheet.save(output, optimize=True)


def update_manifest():
    path = META / "manifest.json"
    data = json.loads(path.read_text(encoding="utf-8"))
    existing = {asset.get("assetId") for asset in data["assets"]}
    for name in NAMES:
        stem = name.removesuffix("_albedo.png")
        is_front = "wall_front" in stem
        mode = repeat_mode(name)
        asset_id = "TR01-" + stem.removeprefix("tr01_").upper().replace("_", "-")
        if asset_id in existing:
            continue
        data["assets"].append({
            "assetId": asset_id,
            "revision": 1,
            "status": "approved",
            "requiredChannels": ["albedo"],
            "footprintCells": [1, 1],
            "footprintWidth": 1,
            "footprintHeight": 1,
            "pivotNormalized": [0.5, 0.0 if is_front else 0.5],
            "pivotPixels": [64, 128 if is_front else 64],
            "dimensionsPixels": [128, 128],
            "visualHeightCells": 1 if is_front else 0,
            "sortingLayerHint": "BackStructure" if is_front else ("Ground" if "floor_" in name else "WallTop"),
            "localOrder": 0,
            "repeatEdges": mode,
            "channels": {"albedo": f"approved/albedo/{name}"},
            "tileability": {"seamLockedPixels": EDGE, "oppositeEdgesIdentical": True},
        })
    data["revision"] = 21
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def write_generation_log(sources):
    lines = [
        "# Stratum Kits v1 generation log", "", "- Date: 2026-09-10",
        "- Generator: Codex built-in ImageGen",
        "- References: calibration board and approved floor/wall top/rim/front assets",
        "- Finishing: 128x128 resize, 48-color no-dither quantization, 4px opposite-edge lock",
        "", "## Source mapping", "",
    ]
    lines.extend(f"- `{name}` <- `{source}`" for name, source in zip(NAMES, sources))
    lines.extend(("", "Normals and optional lighting/mining props are excluded from this original-scope delivery.", ""))
    (SOURCE / "generation-log.md").write_text("\n".join(lines), encoding="utf-8")


def main():
    for directory in (SOURCE, WORKING, ALBEDO, QA, META, UNITY):
        directory.mkdir(parents=True, exist_ok=True)
    sources = generated_sources()
    template = UNITY / "tr01_reference_floor_a_albedo.png.meta"
    report = {"deliveryPixelsPerCell": SIZE, "seamLockedPixels": EDGE, "assets": {}}
    for source, name in zip(sources, NAMES):
        shutil.copy2(source, SOURCE / name.replace("_albedo.png", "_source.png"))
        image = finish_source(source, name)
        for target in (WORKING / name, ALBEDO / name, UNITY / name):
            image.save(target, optimize=True)
        pivot = (0.5, 0.0) if "wall_front" in name else (0.5, 0.5)
        copy_meta(template, name, pivot)
        report["assets"][name] = {
            "dimensions": list(image.size),
            "mode": image.mode,
            "edgeMse": edge_mse(image, repeat_mode(name)),
            "meanLuma": round(sum(ImageStat.Stat(image.convert("RGB")).mean) / 3, 2),
        }

    for prefix in ("tr01_stratum2_", "tr01_stratum3_", "tr01_abyss_"):
        for kind in ("floor", "wall_top", "wall_top_rim", "wall_front"):
            names = [f"{prefix}{kind}_{variant}_albedo.png" for variant in "abc"]
            make_repeat_sheet(names, QA / f"{prefix}{kind}_abc_6x6.png")

    overview = Image.new("RGB", (SIZE * 9, SIZE * 4), (18, 16, 24))
    for col, prefix in enumerate(("tr01_stratum2_", "tr01_stratum3_", "tr01_abyss_")):
        for row, kind in enumerate(("floor", "wall_top", "wall_top_rim", "wall_front")):
            for variant_index, variant in enumerate("abc"):
                name = f"{prefix}{kind}_{variant}_albedo.png"
                overview.paste(Image.open(ALBEDO / name).convert("RGB"), ((col * 3 + variant_index) * SIZE, row * SIZE))
    overview.save(QA / "tr01_stratum_kits_comparison.png", optimize=True)

    boss = Image.new("RGB", (SIZE * 5, SIZE), (18, 16, 24))
    compare = [
        "tr01_stratum2_wall_top_a_albedo.png", "tr01_stratum3_wall_top_a_albedo.png",
        "tr01_abyss_wall_top_a_albedo.png", "tr01_reference_boss_wall_top_a_albedo.png",
        "tr01_reference_boss_wall_front_a_albedo.png",
    ]
    for index, name in enumerate(compare):
        boss.paste(Image.open(ALBEDO / name).convert("RGB"), (index * SIZE, 0))
    boss.save(QA / "tr01_reference_boss_wall_comparison.png", optimize=True)

    write_generation_log(sources)
    (META / "stratum-kits-v1-qa.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    update_manifest()
    print(json.dumps({"albedo": len(NAMES), "qaSheets": 14, "manifestRevision": 21}))


if __name__ == "__main__":
    main()
