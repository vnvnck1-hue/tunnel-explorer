"""Legacy normalizer for the pre-board-direct calibration set.

Do not use this script for V1 approval: it creates derived floor variants and
would break the single-source board workflow.  Use
``extract-reference-board-assets.py`` instead.
"""

from pathlib import Path
from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "art-production/test-room-v01/source/reference_calibration_v1"
WORKING = ROOT / "art-production/test-room-v01/working/reference_calibration_v1"
UNITY = ROOT / "unity/TunnelCrew/Assets/Art/Visual/ReferenceCalibrationV1"
QA = ROOT / "art-production/test-room-v01/qa"


def rgba(path: Path) -> Image.Image:
    return Image.open(path).convert("RGBA")


def alpha_crop(image: Image.Image) -> Image.Image:
    box = image.getchannel("A").getbbox()
    if box is None:
        raise ValueError("source contains no visible pixels")
    return image.crop(box)


def fit_on_canvas(image: Image.Image, size: tuple[int, int], margin: int) -> Image.Image:
    image = alpha_crop(image)
    available = (size[0] - margin * 2, size[1] - margin * 2)
    scale = min(available[0] / image.width, available[1] / image.height)
    resized = image.resize(
        (max(1, round(image.width * scale)), max(1, round(image.height * scale))),
        Image.Resampling.LANCZOS,
    )
    canvas = Image.new("RGBA", size, (0, 0, 0, 0))
    x = (size[0] - resized.width) // 2
    y = size[1] - margin - resized.height
    canvas.alpha_composite(resized, (x, y))
    return canvas


def seam_lock(image: Image.Image, band: int = 4) -> Image.Image:
    """Make opposite edge bands identical without altering the quiet tile centre."""
    px = image.load()
    w, h = image.size
    for offset in range(band):
        for y in range(h):
            a = px[offset, y]
            b = px[w - band + offset, y]
            mixed = tuple((a[i] + b[i]) // 2 for i in range(4))
            px[offset, y] = mixed
            px[w - band + offset, y] = mixed
    for offset in range(band):
        for x in range(w):
            a = px[x, offset]
            b = px[x, h - band + offset]
            mixed = tuple((a[i] + b[i]) // 2 for i in range(4))
            px[x, offset] = mixed
            px[x, h - band + offset] = mixed
    return image


def save(image: Image.Image, filename: str) -> None:
    for directory in (WORKING, UNITY):
        directory.mkdir(parents=True, exist_ok=True)
        image.save(directory / filename, optimize=True)


floor_sources = {
    "a": "tr01_reference_floor_a_v2_source.png",
    "b": "tr01_reference_floor_b_source.png",
    "c": "tr01_reference_floor_c_source.png",
    "d": "tr01_reference_floor_d_source.png",
    "e": "tr01_reference_floor_e_source.png",
    "f": "tr01_reference_floor_f_source.png",
}
floors: dict[str, Image.Image] = {}
for key, filename in floor_sources.items():
    floor = rgba(SOURCE / filename).resize((128, 128), Image.Resampling.LANCZOS)
    floor = seam_lock(floor)
    save(floor, f"tr01_reference_floor_{key}_albedo.png")
    floors[key] = floor

wall = fit_on_canvas(rgba(SOURCE / "tr01_reference_wall_a_source.png"), (384, 384), 8)
save(wall, "tr01_reference_wall_a_albedo.png")

crystal = fit_on_canvas(rgba(SOURCE / "tr01_reference_crystal_a_source.png"), (256, 256), 8)
save(crystal, "tr01_reference_crystal_a_albedo.png")

driller = fit_on_canvas(rgba(SOURCE / "tr01_reference_driller_a_source.png"), (384, 256), 8)
save(driller, "tr01_reference_driller_a_albedo.png")

QA.mkdir(parents=True, exist_ok=True)
repeat = Image.new("RGBA", (128 * 6, 128 * 6))
for row in range(6):
    for col in range(6):
        repeat.alpha_composite(floors["a"], (col * 128, row * 128))
repeat.save(QA / "tr01_reference_floor_a_repeat_6x6.png", optimize=True)

mixed = Image.new("RGBA", (128 * 6, 128 * 6))
floor_keys = tuple(floors)
for row in range(6):
    for col in range(6):
        key = floor_keys[(col * 5 + row * 3 + col * row) % len(floor_keys)]
        tile = floors[key]
        turn = (col * 3 + row * 5) % 4
        if turn:
            tile = tile.rotate(turn * 90)
        if (col + row) % 2:
            tile = tile.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        mixed.alpha_composite(tile, (col * 128, row * 128))
mixed.save(QA / "tr01_reference_floor_variants_6x6.png", optimize=True)

fingerprints = set()
for key, floor in floors.items():
    assert floor.crop((0, 0, 4, 128)).tobytes() == floor.crop((124, 0, 128, 128)).tobytes(), key
    assert floor.crop((0, 0, 128, 4)).tobytes() == floor.crop((0, 124, 128, 128)).tobytes(), key
    fingerprints.add(hash(floor.tobytes()))
assert len(fingerprints) == len(floors), "floor variants must be pixel-distinct"

print("Reference calibration V1 normalized")
print(f"working: {WORKING}")
print(f"unity:   {UNITY}")
print(f"qa:      {QA / 'tr01_reference_floor_variants_6x6.png'}")
print("checks:  6 distinct floors, opposite 4 px edge bands identical")
