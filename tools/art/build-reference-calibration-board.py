"""Build the approval art board from the exact Unity-imported PNG assets."""

from pathlib import Path

import numpy as np
from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "unity/TunnelCrew/Assets/Art/Visual/ReferenceCalibrationV1"
OUTPUT = ROOT / "art-production/test-room-v01/concept/tr01_reference_asset_calibration_board_v1.png"


def backdrop(size: tuple[int, int]) -> Image.Image:
    width, height = size
    yy, xx = np.mgrid[0:height, 0:width]
    radial = np.sqrt(((xx - width * 0.50) / width) ** 2 + ((yy - height * 0.48) / height) ** 2)
    lift = np.clip(1.0 - radial * 1.5, 0.0, 1.0)
    rgb = np.empty((height, width, 3), dtype=np.uint8)
    rgb[..., 0] = 14 + (lift * 8).astype(np.uint8)
    rgb[..., 1] = 14 + (lift * 7).astype(np.uint8)
    rgb[..., 2] = 19 + (lift * 11).astype(np.uint8)
    return Image.fromarray(rgb, "RGB").convert("RGBA")


def place(board: Image.Image, filename: str, position: tuple[int, int], scale: int = 1) -> None:
    image = Image.open(ART / filename).convert("RGBA")
    if scale != 1:
        image = image.resize((image.width * scale, image.height * scale), Image.Resampling.NEAREST)
    board.alpha_composite(image, position)


def main() -> None:
    board = backdrop((1676, 938))
    place(board, "tr01_reference_floor_a_albedo.png", (70, 150), 2)
    place(board, "tr01_reference_floor_b_albedo.png", (360, 150), 2)
    place(board, "tr01_reference_floor_c_albedo.png", (650, 150), 2)
    place(board, "tr01_reference_wall_a_albedo.png", (1040, 70), 1)
    place(board, "tr01_reference_crystal_a_albedo.png", (370, 570), 1)
    place(board, "tr01_reference_driller_a_albedo.png", (810, 570), 1)
    place(board, "tr01_reference_lamp_a_albedo.png", (1400, 610), 1)
    board.convert("RGB").save(OUTPUT, optimize=True)
    print(f"Built board from Unity assets: {OUTPUT}")


if __name__ == "__main__":
    main()
