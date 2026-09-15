#!/usr/bin/env python3
"""Bake aligned preview channels for Primary Match resource-first candidate sprites.

These maps accelerate Unity A/B integration but remain working candidates. They derive
material meaning from authored colour and must receive an artist paintover before approval.
Material mask contract: R metal, G gloss, B wet/crystal, A effect strength.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter


ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / "art-production/test-room-v01/working/primary-match-v2"


def smoothstep(edge0: float, edge1: float, value: np.ndarray) -> np.ndarray:
    t = np.clip((value - edge0) / (edge1 - edge0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def blur(values: np.ndarray, radius: float) -> np.ndarray:
    image = Image.fromarray(np.uint8(np.clip(values, 0.0, 1.0) * 255.0 + 0.5), "L")
    return np.asarray(image.filter(ImageFilter.GaussianBlur(radius)), dtype=np.float32) / 255.0


def bake(source: Path, output_dir: Path) -> dict[str, str]:
    source_image = Image.open(source).convert("RGBA")
    rgba = np.asarray(source_image, dtype=np.float32) / 255.0
    rgb, alpha = rgba[..., :3], rgba[..., 3]
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    value = rgb.max(axis=2)
    minimum = rgb.min(axis=2)
    chroma = value - minimum
    saturation = chroma / np.maximum(value, 1e-4)
    luma = r * 0.2126 + g * 0.7152 + b * 0.0722

    # Avoid normals being pulled toward transparent black at sprite silhouettes.
    weight_near = blur(alpha, 1.4)
    weight_far = blur(alpha, 5.5)
    near = blur(luma * alpha, 1.4) / np.maximum(weight_near, 0.025)
    far = blur(luma * alpha, 5.5) / np.maximum(weight_far, 0.025)
    height = near * 0.72 + far * 0.28
    grad_y, grad_x = np.gradient(height)
    nx = -grad_x * 15.0 * alpha
    ny = -grad_y * 15.0 * alpha
    nz = np.ones_like(nx)
    length = np.sqrt(nx * nx + ny * ny + nz * nz)
    normal = np.stack((nx / length, ny / length, nz / length), axis=-1)
    normal_rgb = np.clip(normal * 0.5 + 0.5, 0.0, 1.0)
    normal_rgb[alpha <= 0.0] = (0.5, 0.5, 1.0)
    normal_rgba = np.dstack((normal_rgb, alpha))

    local = blur(luma * alpha, 4.0) / np.maximum(blur(alpha, 4.0), 0.025)
    broad = blur(luma * alpha, 14.0) / np.maximum(blur(alpha, 14.0), 0.025)
    cavity = np.maximum(local - luma, 0.0) * 1.65 + np.maximum(broad - luma, 0.0) * 0.85
    ao_value = 1.0 - np.clip(cavity * 1.75, 0.0, 0.62)
    ao_rgb = np.repeat(ao_value[..., None], 3, axis=-1)
    ao_rgba = np.dstack((ao_rgb, alpha))
    ao_rgba[alpha <= 0.0] = (1.0, 1.0, 1.0, 0.0)

    local_value = blur(value * alpha, 9.0) / np.maximum(blur(alpha, 9.0), 0.025)
    bright_accent = smoothstep(0.075, 0.27, value - local_value)
    magenta = smoothstep(0.25, 0.56, np.minimum(r - g, b - g))
    magenta *= smoothstep(0.48, 0.82, value) * bright_accent
    cyan = smoothstep(0.20, 0.48, np.minimum(g - r, b - r))
    cyan *= smoothstep(0.50, 0.82, value) * bright_accent
    amber = smoothstep(0.25, 0.54, np.minimum(r - b, g - b))
    amber *= smoothstep(0.52, 0.84, value) * bright_accent
    emissive = np.maximum.reduce((magenta, cyan * 0.92, amber * 0.78))
    emissive *= smoothstep(0.24, 0.67, value) * alpha
    emissive = blur(emissive, 0.55) * alpha
    emission_rgb = np.clip(rgb * (0.35 + emissive[..., None] * 1.10), 0.0, 1.0)
    emission_rgba = np.dstack((emission_rgb, emissive))
    emission_rgba[alpha <= 0.0] = 0.0

    # Preview material classification. Broad low-chroma painted planes become metal;
    # vivid minerals/lights become crystal. Gloss stays at highlights and hard metal edges.
    neutral = 1.0 - smoothstep(0.16, 0.43, saturation)
    metal = neutral * smoothstep(0.12, 0.40, value) * alpha
    crystal = np.maximum.reduce((magenta, cyan * 0.88, amber * 0.62)) * alpha
    highlight = smoothstep(0.08, 0.30, luma - blur(luma * alpha, 3.0))
    gloss = np.clip(metal * (0.28 + highlight * 0.72) + crystal * 0.44, 0.0, 1.0)
    strength = np.maximum.reduce((metal * 0.72, gloss, crystal)) * alpha
    material = np.dstack((metal, gloss, crystal, strength))
    material[alpha <= 0.0] = 0.0

    stem = source.stem.removesuffix("_source")
    outputs = {
        "normal": output_dir / f"{stem}_normal.png",
        "ao": output_dir / f"{stem}_ao.png",
        "emission": output_dir / f"{stem}_emission.png",
        "mask": output_dir / f"{stem}_mask.png",
    }
    output_dir.mkdir(parents=True, exist_ok=True)
    Image.fromarray(np.uint8(normal_rgba * 255.0 + 0.5), "RGBA").save(outputs["normal"], optimize=True)
    Image.fromarray(np.uint8(ao_rgba * 255.0 + 0.5), "RGBA").save(outputs["ao"], optimize=True)
    Image.fromarray(np.uint8(emission_rgba * 255.0 + 0.5), "RGBA").save(outputs["emission"], optimize=True)
    Image.fromarray(np.uint8(material * 255.0 + 0.5), "RGBA").save(outputs["mask"], optimize=True)
    return {name: str(path.relative_to(WORK)).replace("\\", "/") for name, path in outputs.items()}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--work-dir", type=Path, default=WORK)
    args = parser.parse_args()
    work_dir = args.work_dir.resolve()
    manifest_path = work_dir / "manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    output_dir = work_dir / "channels/resource-first"

    candidates = [asset for asset in manifest["assets"] if asset.get("runtimeIntegrated") is False]
    report = {
        "status": "derived_working_candidate_requires_artist_paintover",
        "materialMaskContract": {"R": "metal", "G": "gloss", "B": "wet_or_crystal", "A": "effect_strength"},
        "unityImported": False,
        "assets": [],
    }
    for asset in candidates:
        channels = bake(work_dir / asset["path"], output_dir)
        report["assets"].append({
            "assetId": asset["assetId"],
            "source": asset["path"],
            "channels": {
                name: {"path": path, "sha256": sha256(work_dir / path)}
                for name, path in channels.items()
            },
        })

    report_path = output_dir / "report.json"
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"OK: baked {len(candidates)} candidate channel sets ({len(candidates) * 4} images)")


if __name__ == "__main__":
    main()
