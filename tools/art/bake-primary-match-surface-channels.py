#!/usr/bin/env python3
"""Bake aligned normal, cavity AO, and mineral emission maps from primary-match albedo.

This is a deterministic authoring helper, not a generic Unity importer. It keeps every
pixel registered to the accepted 1254 px surface paintings and never resizes the art.
"""

from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter


SURFACES = {
    "floor": "tr01_primarymatch_floor_macro_3x3_source.png",
    "wall_top": "tr01_primarymatch_wall_top_macro_3x3_source.png",
    "wall_front": "tr01_primarymatch_wall_front_macro_3x3_source.png",
    "wall_rim": "tr01_primarymatch_wall_top_rim_3x3_source.png",
}


def smoothstep(edge0: float, edge1: float, value: np.ndarray) -> np.ndarray:
    t = np.clip((value - edge0) / (edge1 - edge0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def blurred_luma(luma: np.ndarray, radius: float) -> np.ndarray:
    image = Image.fromarray(np.uint8(np.clip(luma, 0.0, 1.0) * 255.0), mode="L")
    return np.asarray(image.filter(ImageFilter.GaussianBlur(radius)), dtype=np.float32) / 255.0


def bake_channels(source: Path, output_dir: Path, stem: str) -> None:
    albedo_image = Image.open(source).convert("RGB")
    rgb = np.asarray(albedo_image, dtype=np.float32) / 255.0
    luma = rgb[..., 0] * 0.2126 + rgb[..., 1] * 0.7152 + rgb[..., 2] * 0.0722

    # A two-scale height estimate suppresses single-pixel paint noise while retaining
    # broad slab planes and the dark seams that should turn away from the key light.
    height = blurred_luma(luma, 1.15) * 0.68 + blurred_luma(luma, 5.5) * 0.32
    grad_y, grad_x = np.gradient(height)
    nx = -grad_x * 18.0
    ny = -grad_y * 18.0
    nz = np.ones_like(nx)
    length = np.sqrt(nx * nx + ny * ny + nz * nz)
    normal = np.stack((nx / length, ny / length, nz / length), axis=-1)
    normal_rgb = np.uint8(np.clip(normal * 0.5 + 0.5, 0.0, 1.0) * 255.0 + 0.5)

    # AO is based on locally dark cavities, not absolute darkness. A naturally dark rock
    # plane stays readable while cracks and rubble pockets receive the strongest occlusion.
    near = blurred_luma(luma, 3.0)
    far = blurred_luma(luma, 13.0)
    cavity = np.maximum(near - luma, 0.0) * 1.55 + np.maximum(far - luma, 0.0) * 0.85
    ao = 1.0 - np.clip(cavity * 1.7, 0.0, 0.58)
    ao_rgb = np.uint8(np.repeat(ao[..., None], 3, axis=-1) * 255.0 + 0.5)

    # Only the authored purple-magenta mineral accents emit. Neutral stone highlights and
    # steel stay black, avoiding the old problem where albedo brightness glowed by itself.
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    value = np.maximum(np.maximum(r, g), b)
    magenta = smoothstep(0.045, 0.20, r - g) * smoothstep(0.015, 0.13, b - g)
    magenta *= smoothstep(0.17, 0.58, value)
    magenta_image = Image.fromarray(np.uint8(magenta * 255.0 + 0.5), mode="L")
    magenta = np.asarray(magenta_image.filter(ImageFilter.GaussianBlur(0.65)), dtype=np.float32) / 255.0
    emission_rgb = np.uint8(np.clip(rgb * (0.50 + magenta[..., None] * 0.75), 0.0, 1.0) * 255.0 + 0.5)
    emission_alpha = np.uint8(np.clip(magenta, 0.0, 1.0) * 255.0 + 0.5)
    emission = np.dstack((emission_rgb, emission_alpha))

    output_dir.mkdir(parents=True, exist_ok=True)
    Image.fromarray(normal_rgb, mode="RGB").save(output_dir / f"{stem}_normal.png", optimize=True)
    Image.fromarray(ao_rgb, mode="RGB").save(output_dir / f"{stem}_ao.png", optimize=True)
    Image.fromarray(emission, mode="RGBA").save(output_dir / f"{stem}_emission.png", optimize=True)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-dir", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    args = parser.parse_args()

    for stem, filename in SURFACES.items():
        source = args.source_dir / filename
        if not source.is_file():
            raise FileNotFoundError(source)
        bake_channels(source, args.output_dir, stem)


if __name__ == "__main__":
    main()
