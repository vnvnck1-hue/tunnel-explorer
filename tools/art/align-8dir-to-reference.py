"""Align eight separated pixel-art figures to an approved layout reference."""

from pathlib import Path
import argparse

import numpy as np
from PIL import Image

from importlib.machinery import SourceFileLoader


NORMALIZER = SourceFileLoader(
    "normalize_8dir_turnaround",
    str(Path(__file__).with_name("normalize-8dir-turnaround.py")),
).load_module()

DIRECTIONS = ("N", "NE", "E", "SE", "S", "SW", "W", "NW")
VECTORS = {
    "N": (0, -1), "NE": (1, -1), "E": (1, 0), "SE": (1, 1),
    "S": (0, 1), "SW": (-1, 1), "W": (-1, 0), "NW": (-1, -1),
}


def assign(regions, size):
    cx = cy = size / 2
    remaining = list(regions)
    result = {}
    for direction in DIRECTIONS:
        ex, ey = VECTORS[direction]
        length = (ex * ex + ey * ey) ** 0.5
        ex, ey = ex / length, ey / length

        def score(region):
            box, _ = region
            dx = (box[0] + box[2]) / 2 - cx
            dy = (box[1] + box[3]) / 2 - cy
            return (dx * ex + dy * ey) / max(1.0, (dx * dx + dy * dy) ** 0.5)

        chosen = max(remaining, key=score)
        result[direction] = chosen
        remaining.remove(chosen)
    return result


def align(source: Path, reference: Path, output: Path, grid: int = 4) -> None:
    src = Image.open(source).convert("RGBA")
    ref = Image.open(reference).convert("RGBA")
    if src.size != ref.size or src.width != src.height:
        raise ValueError("source and reference must use the same square canvas")
    src_regions = NORMALIZER.component_regions(np.asarray(src.getchannel("A")), threshold=0)
    ref_regions = NORMALIZER.component_regions(np.asarray(ref.getchannel("A")), threshold=0)
    if len(src_regions) != 8 or len(ref_regions) != 8:
        raise ValueError(f"expected 8 source and 8 reference figures, found {len(src_regions)} and {len(ref_regions)}")

    sources = assign(src_regions, src.width)
    targets = assign(ref_regions, ref.width)
    canvas = Image.new("RGBA", src.size, (0, 0, 0, 0))
    occupied = np.zeros((src.height, src.width), dtype=bool)

    for direction in DIRECTIONS:
        sbox, smask = sources[direction]
        tbox, _ = targets[direction]
        pixels = np.asarray(src.crop(sbox)).copy()
        pixels[~smask] = 0
        figure = Image.fromarray(pixels, mode="RGBA")

        source_pivot = ((sbox[0] + sbox[2]) / 2, sbox[3])
        target_pivot = ((tbox[0] + tbox[2]) / 2, tbox[3])
        dx = round((target_pivot[0] - source_pivot[0]) / grid) * grid
        dy = round((target_pivot[1] - source_pivot[1]) / grid) * grid
        x, y = sbox[0] + dx, sbox[1] + dy
        if x < 0 or y < 0 or x + figure.width > src.width or y + figure.height > src.height:
            raise ValueError(f"{direction} would leave the canvas")
        local = np.asarray(figure.getchannel("A")) > 0
        region = occupied[y:y + figure.height, x:x + figure.width]
        if np.any(region & local):
            raise ValueError(f"{direction} would overlap another figure")
        region |= local
        canvas.alpha_composite(figure, (x, y))

    output.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output, optimize=True)
    print(f"aligned {source} -> {output}; reference={reference}; grid={grid}px")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("reference", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--grid", type=int, default=4)
    args = parser.parse_args()
    align(args.source, args.reference, args.output, args.grid)


if __name__ == "__main__":
    main()
