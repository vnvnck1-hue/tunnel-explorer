"""Convert the generated Gunner source sheet into Unity-ready pixel sprites.

The source is deliberately generated on a flat #ff00ff background.  This tool
keys that colour to alpha, crops the fixed 3x2 layout, downsamples to the
logical pixel grid with nearest-neighbour sampling, and leaves one transparent
pixel of padding around every part.
"""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image


BASE_PARTS = {
    "gunner_head": (0.00, 0.00, 1 / 3, 0.50),
    "gunner_body": (1 / 3, 0.00, 2 / 3, 0.50),
    "gunner_hand_left": (2 / 3, 0.00, 1.00, 0.50),
    "gunner_hand_right": (0.00, 0.50, 1 / 3, 1.00),
    "gunner_weapon": (1 / 3, 0.50, 2 / 3, 1.00),
    "gunner_muzzle_flash": (2 / 3, 0.50, 0.90, 1.00),
    "gunner_bullet": (0.90, 0.50, 1.00, 1.00),
}

DIRECTIONAL_PARTS = {
    "gunner_hand_left": (2 / 3, 0.00, 1.00, 0.50),
    "gunner_head_back": (0.00, 0.50, 1 / 3, 1.00),
    "gunner_body_back": (1 / 3, 0.50, 2 / 3, 1.00),
    "gunner_hand_right": (2 / 3, 0.50, 1.00, 1.00),
}

DIAGONAL_PARTS = {
    "gunner_head_down_left": (0.00, 0.00, 0.50, 0.50),
    "gunner_body_down_left": (0.50, 0.00, 1.00, 0.50),
    "gunner_head_up_left": (0.00, 0.50, 0.50, 1.00),
    "gunner_body_up_left": (0.50, 0.50, 1.00, 1.00),
}


def key_magenta(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    pixels = rgba.load()
    for y in range(rgba.height):
        for x in range(rgba.width):
            r, g, b, a = pixels[x, y]
            # The generator's background is almost uniform but can vary by a
            # few values.  The art palette contains no hot magenta.
            if r >= 210 and b >= 190 and g <= 105:
                pixels[x, y] = (0, 0, 0, 0)
            elif a:
                pixels[x, y] = (r, g, b, 255)
    return rgba


def extract(image: Image.Image, normalized_box: tuple[float, float, float, float], scale: int) -> Image.Image:
    width, height = image.size
    x0, y0, x1, y1 = normalized_box
    cell = image.crop((round(x0 * width), round(y0 * height), round(x1 * width), round(y1 * height)))
    alpha_box = cell.getchannel("A").getbbox()
    if alpha_box is None:
        raise RuntimeError(f"No opaque pixels found in cell {normalized_box}")
    cropped = cell.crop(alpha_box)
    logical_size = (
        max(1, round(cropped.width / scale)),
        max(1, round(cropped.height / scale)),
    )
    logical = cropped.resize(logical_size, Image.Resampling.NEAREST)
    padded = Image.new("RGBA", (logical.width + 2, logical.height + 2), (0, 0, 0, 0))
    padded.alpha_composite(logical, (1, 1))
    return padded


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--scale", type=int, default=16)
    parser.add_argument("--layout", choices=("base", "directional", "diagonal"), default="base")
    args = parser.parse_args()

    args.output.mkdir(parents=True, exist_ok=True)
    keyed = key_magenta(Image.open(args.source))

    parts = {
        "base": BASE_PARTS,
        "directional": DIRECTIONAL_PARTS,
        "diagonal": DIAGONAL_PARTS,
    }[args.layout]
    for name, box in parts.items():
        sprite = extract(keyed, box, args.scale)
        destination = args.output / f"{name}.png"
        sprite.save(destination, optimize=True)
        print(f"{name}: {sprite.width}x{sprite.height} -> {destination}")


if __name__ == "__main__":
    main()
