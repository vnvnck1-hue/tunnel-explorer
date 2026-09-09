"""Build the active calibration assets from the approved board family.

Floor A and the three cutout subjects come from the original approved board;
floor B/C are the two generated extensions.  The outputs are the exact PNGs
consumed by Unity and subsequently placed back onto the approval board.
"""

from collections import deque
from pathlib import Path
import shutil

import numpy as np
from PIL import Image, ImageFilter


ROOT = Path(__file__).resolve().parents[2]
BOARD = ROOT / "art-production/test-room-v01/source/reference_calibration_v1/tr01_reference_asset_calibration_board_original_source.png"
GENERATED_B = ROOT / "art-production/test-room-v01/source/reference_calibration_v1/tr01_reference_floor_b_generated.png"
GENERATED_C = ROOT / "art-production/test-room-v01/source/reference_calibration_v1/tr01_reference_floor_c_generated.png"
GENERATED_LAMP = ROOT / "art-production/test-room-v01/source/reference_calibration_v1/tr01_reference_lamp_a_generated.png"
OUT = ROOT / "art-production/test-room-v01/working/reference_calibration_v1_direct"
SOURCE = ROOT / "art-production/test-room-v01/source/reference_calibration_v1"
WORKING = ROOT / "art-production/test-room-v01/working/reference_calibration_v1"
UNITY = ROOT / "unity/TunnelCrew/Assets/Art/Visual/ReferenceCalibrationV1"

# Rectangles are deliberately loose: the small outer margin is board backdrop,
# not painted asset content.  Coordinates are in the 1676x938 board pixel space.
CROPS = {
    "tr01_reference_wall_a_albedo.png": (420, 155, 765, 705),
    "tr01_reference_crystal_a_albedo.png": (810, 265, 1140, 705),
    "tr01_reference_driller_a_albedo.png": (1140, 230, 1676, 740),
}

TARGETS = {
    "tr01_reference_wall_a_albedo.png": (384, 384),
    "tr01_reference_crystal_a_albedo.png": (256, 256),
    "tr01_reference_driller_a_albedo.png": (384, 256),
    "tr01_reference_lamp_a_albedo.png": (192, 256),
}


def background_model(rgb: np.ndarray) -> np.ndarray:
    """Fit a smooth quadratic RGB backdrop using only crop border pixels."""
    h, w, _ = rgb.shape
    yy, xx = np.mgrid[0:h, 0:w]
    x = (xx / max(1, w - 1) * 2.0) - 1.0
    y = (yy / max(1, h - 1) * 2.0) - 1.0
    design = np.stack((np.ones_like(x), x, y, x * x, x * y, y * y), axis=-1)
    border = (xx < 10) | (yy < 10) | (xx >= w - 10) | (yy >= h - 10)
    a = design[border]
    model = np.empty_like(rgb, dtype=np.float32)
    for channel in range(3):
        coef, *_ = np.linalg.lstsq(a, rgb[..., channel][border], rcond=None)
        model[..., channel] = np.tensordot(design, coef, axes=([-1], [0]))
    return model


def extract(crop: Image.Image) -> Image.Image:
    rgba = np.asarray(crop.convert("RGBA"), dtype=np.uint8)
    rgb = rgba[..., :3].astype(np.float32)
    chroma = rgb.max(axis=-1) - rgb.min(axis=-1)
    brightness = rgb.max(axis=-1)

    # Keep connected regions that contain clearly painted pixels.  This avoids
    # treating the smooth vignette of the board as part of a sprite.
    strong = (chroma > 14.0) | (brightness > 52.0)
    candidate = (chroma > 5.0) | (brightness > 29.0)
    h, w = candidate.shape
    visited = np.zeros((h, w), dtype=bool)
    keep = np.zeros((h, w), dtype=bool)
    seeds = np.argwhere(strong)
    for sy, sx in seeds:
        if visited[sy, sx]:
            continue
        queue = deque([(int(sy), int(sx))])
        visited[sy, sx] = True
        component = []
        while queue:
            y, x = queue.popleft()
            component.append((y, x))
            for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
                if 0 <= ny < h and 0 <= nx < w and candidate[ny, nx] and not visited[ny, nx]:
                    visited[ny, nx] = True
                    queue.append((ny, nx))
        if len(component) >= 24:
            for y, x in component:
                keep[y, x] = True

    mask = Image.fromarray((keep.astype(np.uint8) * 255), "L")
    # Recover the dark outline immediately adjacent to the painted clusters,
    # without pulling the board backdrop into the sprite.
    mask = mask.filter(ImageFilter.MaxFilter(5))
    alpha = np.asarray(mask, dtype=np.uint8)
    out = rgba.copy()
    out[..., 3] = alpha
    return Image.fromarray(out, "RGBA")


def seam_lock(image: Image.Image, band: int = 3) -> Image.Image:
    """Make opposite edge bands identical while keeping the tile opaque."""
    image = image.convert("RGBA")
    px = image.load()
    w, h = image.size
    for offset in range(band):
        for y in range(h):
            a, b = px[offset, y], px[w - band + offset, y]
            mixed = tuple((a[i] + b[i]) // 2 for i in range(3)) + (255,)
            px[offset, y] = mixed
            px[w - band + offset, y] = mixed
    for offset in range(band):
        for x in range(w):
            a, b = px[x, offset], px[x, h - band + offset]
            mixed = tuple((a[i] + b[i]) // 2 for i in range(3)) + (255,)
            px[x, offset] = mixed
            px[x, h - band + offset] = mixed
    return image


def floor_from_board(board: Image.Image) -> Image.Image:
    # Exact opaque square inside the original board's presentation margin.
    return seam_lock(board.crop((51, 365, 367, 681)).resize((128, 128), Image.Resampling.LANCZOS))


def floor_from_generated(path: Path) -> Image.Image:
    image = Image.open(path).convert("RGB")
    # Image generation leaves a narrow presentation rim; remove it before the
    # full-canvas opaque tile is resized.
    image = image.crop((12, 12, image.width - 12, image.height - 12))
    return seam_lock(image.resize((128, 128), Image.Resampling.LANCZOS))


def normalize(image: Image.Image, target: tuple[int, int]) -> Image.Image:
    """Resize only after extraction, preserving the board's painted pixels."""
    alpha = image.getchannel("A")
    box = alpha.getbbox()
    if box is None:
        raise ValueError("board crop contains no visible pixels")
    image = image.crop(box)
    if target[0] == target[1]:
        side = max(image.width, image.height)
        canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
        canvas.alpha_composite(image, ((side - image.width) // 2, side - image.height))
        image = canvas
    available = (target[0] - 8, target[1] - 8)
    scale = min(available[0] / image.width, available[1] / image.height)
    resized = image.resize(
        (max(1, round(image.width * scale)), max(1, round(image.height * scale))),
        Image.Resampling.LANCZOS,
    )
    canvas = Image.new("RGBA", target, (0, 0, 0, 0))
    canvas.alpha_composite(resized, ((target[0] - resized.width) // 2, target[1] - 8 - resized.height))
    return canvas


def main() -> None:
    board = Image.open(BOARD).convert("RGB")
    for directory in (OUT, SOURCE, WORKING, UNITY):
        directory.mkdir(parents=True, exist_ok=True)
    assets = {
        "tr01_reference_floor_a_albedo.png": floor_from_board(board),
        "tr01_reference_floor_b_albedo.png": floor_from_generated(GENERATED_B),
        "tr01_reference_floor_c_albedo.png": floor_from_generated(GENERATED_C),
        "tr01_reference_lamp_a_albedo.png": normalize(
            Image.open(GENERATED_LAMP).convert("RGBA"), TARGETS["tr01_reference_lamp_a_albedo.png"]
        ),
    }
    for filename, box in CROPS.items():
        assets[filename] = normalize(extract(board.crop(box)), TARGETS[filename])

    for filename, extracted in assets.items():
        direct_path = OUT / filename
        extracted.save(direct_path, optimize=True)
        shutil.copy2(direct_path, WORKING / filename)
        shutil.copy2(direct_path, UNITY / filename)
        source_names = {
            "tr01_reference_floor_a_albedo.png": [
                "tr01_reference_floor_a_source.png",
                "tr01_reference_floor_a_v2_source.png",
            ],
            "tr01_reference_floor_b_albedo.png": ["tr01_reference_floor_b_source.png"],
            "tr01_reference_floor_c_albedo.png": ["tr01_reference_floor_c_source.png"],
            "tr01_reference_wall_a_albedo.png": ["tr01_reference_wall_a_source.png"],
            "tr01_reference_crystal_a_albedo.png": ["tr01_reference_crystal_a_source.png"],
            "tr01_reference_driller_a_albedo.png": ["tr01_reference_driller_a_source.png"],
            "tr01_reference_lamp_a_albedo.png": ["tr01_reference_lamp_a_source.png"],
        }[filename]
        for source_name in source_names:
            shutil.copy2(direct_path, SOURCE / source_name)
        print(filename, extracted.size, direct_path)


if __name__ == "__main__":
    main()
