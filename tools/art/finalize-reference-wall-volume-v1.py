from __future__ import annotations

import json
import hashlib
from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance, ImageStat


ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "art-production" / "test-room-v01"
SOURCE = ART / "source" / "reference_wall_volume_v1"
WORKING = ART / "working" / "reference_wall_volume_v1"
APPROVED_ALBEDO = ART / "approved" / "albedo"
APPROVED_AO = ART / "approved" / "ao"
QA = ART / "qa"
UNITY = ROOT / "unity" / "TunnelCrew" / "Assets" / "Art" / "Visual" / "ReferenceCalibrationV1"
META = ART / "metadata"

SIZE = 128
EDGE = 4

SOURCE_NAMES = {
    "tr01_reference_wall_side_west_a_albedo.png": "tr01_reference_wall_side_west_a_source.png",
    "tr01_reference_wall_side_east_a_albedo.png": "tr01_reference_wall_side_east_a_source.png",
    "tr01_reference_wall_outer_corner_a_albedo.png": "tr01_reference_wall_outer_corner_a_source.png",
    "tr01_reference_wall_inner_corner_a_albedo.png": "tr01_reference_wall_inner_corner_a_source.png",
    "tr01_reference_wall_top_rim_a_albedo.png": "tr01_reference_wall_top_rim_a_source.png",
}


def palette_reference() -> Image.Image:
    cap = Image.open(APPROVED_ALBEDO / "tr01_reference_wall_top_a_albedo.png").convert("RGB")
    front = Image.open(APPROVED_ALBEDO / "tr01_reference_wall_front_a_albedo.png").convert("RGB")
    joined = Image.new("RGB", (SIZE * 2, SIZE))
    joined.paste(cap, (0, 0))
    joined.paste(front, (SIZE, 0))
    return joined.quantize(colors=64, method=Image.Quantize.MEDIANCUT)


def quantize_to_reference(image: Image.Image, palette: Image.Image) -> Image.Image:
    return image.convert("RGB").quantize(palette=palette, dither=Image.Dither.NONE).convert("RGBA")


def lock_horizontal_edges(image: Image.Image) -> Image.Image:
    image = image.copy()
    left = image.crop((0, 0, EDGE, SIZE))
    right = image.crop((SIZE - EDGE, 0, SIZE, SIZE))
    merged = Image.blend(left, right, 0.5)
    image.paste(merged, (0, 0))
    image.paste(merged, (SIZE - EDGE, 0))
    return image


def rim_directional_grade(image: Image.Image) -> Image.Image:
    px = image.load()
    for y in range(SIZE):
        for x in range(SIZE):
            r, g, b, a = px[x, y]
            north = max(0.0, (12 - y) / 12)
            west = max(0.0, (12 - x) / 12)
            south = max(0.0, (y - (SIZE - 13)) / 12)
            east = max(0.0, (x - (SIZE - 13)) / 12)
            factor = 1.0 + 0.16 * max(north, west) - 0.12 * max(south, east)
            px[x, y] = (
                max(0, min(255, round(r * factor))),
                max(0, min(255, round(g * factor))),
                max(0, min(255, round(b * factor))),
                a,
            )
    return image


def normalize_albedo(name: str, source_name: str, palette: Image.Image) -> Image.Image:
    image = Image.open(SOURCE / source_name).convert("RGB")
    image = image.resize((SIZE, SIZE), Image.Resampling.LANCZOS)
    if "side_west" in name:
        image = ImageEnhance.Brightness(image).enhance(0.95)
    elif "side_east" in name:
        image = ImageEnhance.Brightness(image).enhance(0.84)
    elif "top_rim" in name:
        cap = Image.open(APPROVED_ALBEDO / "tr01_reference_wall_top_a_albedo.png").convert("RGB")
        image = Image.blend(cap, image, 0.42)
    image = quantize_to_reference(image, palette)
    if "side_" in name:
        image = lock_horizontal_edges(image)
    if "top_rim" in name:
        image = rim_directional_grade(image)
        image = lock_horizontal_edges(image)
    return quantize_to_reference(image, palette)


def save_everywhere(image: Image.Image, name: str) -> None:
    image.save(WORKING / name, optimize=True)
    image.save(APPROVED_ALBEDO / name, optimize=True)
    image.save(UNITY / name, optimize=True)


def make_ao_variants() -> dict[str, Image.Image]:
    north = Image.open(APPROVED_AO / "tr01_reference_contact_ao_a_ao.png").convert("RGBA")
    variants = {
        "e": north.transpose(Image.Transpose.ROTATE_270),
        "s": north.transpose(Image.Transpose.ROTATE_180),
        "w": north.transpose(Image.Transpose.ROTATE_90),
    }
    for direction, image in variants.items():
        plain = f"tr01_reference_contact_ao_{direction}.png"
        channel = f"tr01_reference_contact_ao_{direction}_ao.png"
        image.save(WORKING / plain, optimize=True)
        image.save(APPROVED_AO / plain, optimize=True)
        image.save(APPROVED_AO / channel, optimize=True)
        image.save(UNITY / plain, optimize=True)
    return variants


def edge_error(image: Image.Image, horizontal: bool) -> float:
    rgb = image.convert("RGB")
    if horizontal:
        a = rgb.crop((0, 0, EDGE, SIZE))
        b = rgb.crop((SIZE - EDGE, 0, SIZE, SIZE))
    else:
        a = rgb.crop((0, 0, SIZE, EDGE))
        b = rgb.crop((0, SIZE - EDGE, SIZE, SIZE))
    ah = list(a.get_flattened_data())
    bh = list(b.get_flattened_data())
    total = sum((x - y) ** 2 for pa, pb in zip(ah, bh) for x, y in zip(pa, pb))
    return total / (len(ah) * 3)


def checker_under(image: Image.Image) -> Image.Image:
    bg = Image.new("RGBA", image.size)
    draw = ImageDraw.Draw(bg)
    cell = 8
    for y in range(0, image.height, cell):
        for x in range(0, image.width, cell):
            c = (52, 52, 58, 255) if (x // cell + y // cell) % 2 else (88, 88, 96, 255)
            draw.rectangle((x, y, x + cell - 1, y + cell - 1), fill=c)
    bg.alpha_composite(image)
    return bg.convert("RGB")


def make_qa(albedos: dict[str, Image.Image], aos: dict[str, Image.Image]) -> dict:
    order = list(albedos)
    repeat = Image.new("RGB", (SIZE * 6, SIZE * 6))
    for y in range(6):
        for x in range(6):
            name = order[(x + y * 2) % len(order)]
            repeat.paste(albedos[name].convert("RGB"), (x * SIZE, y * SIZE))
    repeat.save(QA / "tr01_reference_wall_volume_variants_6x6.png", optimize=True)

    ao_sheet = Image.new("RGB", (SIZE * 4, SIZE))
    north = Image.open(APPROVED_AO / "tr01_reference_contact_ao_a_ao.png").convert("RGBA")
    for i, image in enumerate([north, aos["e"], aos["s"], aos["w"]]):
        ao_sheet.paste(checker_under(image), (i * SIZE, 0))
    ao_sheet.save(QA / "tr01_reference_contact_ao_nesw.png", optimize=True)

    report = {"deliveryPixelsPerCell": SIZE, "edgeLockPixels": EDGE, "assets": {}}
    for name, image in albedos.items():
        stat = ImageStat.Stat(image.convert("RGB"))
        report["assets"][name] = {
            "dimensions": list(image.size),
            "meanRgb": [round(v, 2) for v in stat.mean],
            "meanLuma": round(sum(v * w for v, w in zip(stat.mean, (0.2126, 0.7152, 0.0722))), 2),
            "horizontalEdgeMse": round(edge_error(image, True), 4),
            "verticalEdgeMse": round(edge_error(image, False), 4),
        }
    for direction, image in {"n": north, **aos}.items():
        alpha = image.getchannel("A")
        report["assets"][f"contact_ao_{direction}"] = {
            "dimensions": list(image.size),
            "alphaMax": alpha.getextrema()[1],
            "alphaSum": sum(alpha.get_flattened_data()),
        }
    (META / "reference-wall-volume-v1-qa.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    return report


def copy_meta(template_name: str, target_name: str, pivot: tuple[float, float]) -> None:
    template = (UNITY / f"{template_name}.meta").read_text(encoding="utf-8")
    # Unity regenerates a unique GUID on import. Avoid cloning a live asset GUID.
    lines = [line for line in template.splitlines() if not line.startswith("guid: ")]
    guid = hashlib.md5(f"TunnelCrew/ReferenceCalibrationV1/{target_name}".encode("utf-8")).hexdigest()
    lines.insert(1, f"guid: {guid}")
    text = "\n".join(lines) + "\n"
    text = text.replace(f"second: {Path(template_name).stem}_0", f"second: {Path(target_name).stem}_0")
    text = text.replace(f"{Path(template_name).stem}_0:", f"{Path(target_name).stem}_0:")
    text = text.replace("spritePivot: {x: 0.5, y: 0.5}", f"spritePivot: {{x: {pivot[0]}, y: {pivot[1]}}}")
    (UNITY / f"{target_name}.meta").write_text(text, encoding="utf-8")


def main() -> None:
    for directory in (WORKING, APPROVED_ALBEDO, APPROVED_AO, QA, UNITY, META):
        directory.mkdir(parents=True, exist_ok=True)

    palette = palette_reference()
    albedos = {}
    for name, source_name in SOURCE_NAMES.items():
        image = normalize_albedo(name, source_name, palette)
        save_everywhere(image, name)
        albedos[name] = image

    aos = make_ao_variants()
    report = make_qa(albedos, aos)

    cap_template = "tr01_reference_wall_top_a_albedo.png"
    front_template = "tr01_reference_wall_front_a_albedo.png"
    ao_template = "tr01_reference_contact_ao_a.png"
    for name in albedos:
        template = front_template if "side_" in name else cap_template
        pivot = (0.5, 0.0) if "side_" in name else (0.5, 0.5)
        copy_meta(template, name, pivot)
    for direction in "esw":
        copy_meta(ao_template, f"tr01_reference_contact_ao_{direction}.png", (0.5, 0.5))

    print(f"Wrote {len(albedos)} albedo assets and {len(aos)} AO assets")
    for name, values in report["assets"].items():
        print(name, values)


if __name__ == "__main__":
    main()
