"""Convert a normalized 8-direction turnaround to a strict pixel grid."""

from pathlib import Path
import argparse

import numpy as np
from PIL import Image


def pixelize(source: Path, output: Path, logical_size: int = 512, colors: int = 64) -> None:
    image = Image.open(source).convert("RGBA")
    if image.width != image.height or image.width % logical_size:
        raise ValueError("input must be a square canvas evenly divisible by logical size")

    logical = image.resize((logical_size, logical_size), Image.Resampling.NEAREST)
    rgba = np.asarray(logical).copy()
    alpha = np.where(rgba[:, :, 3] >= 128, 255, 0).astype(np.uint8)

    rgb = Image.fromarray(rgba[:, :, :3], mode="RGB")
    palette = rgb.quantize(
        colors=colors,
        method=Image.Quantize.MEDIANCUT,
        dither=Image.Dither.NONE,
    ).convert("RGB")
    result = np.dstack((np.asarray(palette), alpha)).astype(np.uint8)
    result[alpha == 0, :3] = 0
    logical_rgba = Image.fromarray(result, mode="RGBA")

    scale = image.width // logical_size
    final = logical_rgba.resize(image.size, Image.Resampling.NEAREST)
    final_array = np.asarray(final)
    if set(np.unique(final_array[:, :, 3])) - {0, 255}:
        raise ValueError("output contains non-binary alpha")

    # Every scale x scale output block must be a single RGBA value.
    blocks = final_array.reshape(logical_size, scale, logical_size, scale, 4)
    if not np.all(blocks == blocks[:, :1, :, :1, :]):
        raise ValueError("output is not aligned to the logical pixel grid")

    output.parent.mkdir(parents=True, exist_ok=True)
    final.save(output, optimize=True)
    opaque_colors = np.unique(result[alpha == 255, :3], axis=0).shape[0]
    print(
        f"pixelized {source} -> {output}; logical={logical_size}; scale={scale}x; "
        f"opaque_colors={opaque_colors}; alpha=0/255; dither=none"
    )


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--logical-size", type=int, default=512)
    parser.add_argument("--colors", type=int, default=64)
    args = parser.parse_args()
    pixelize(args.source, args.output, args.logical_size, args.colors)


if __name__ == "__main__":
    main()
