from __future__ import annotations

import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageEnhance, ImageStat


ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "art-production" / "test-room-v01"
SOURCE = ART / "source" / "r2_darkness_boundary_v1"
WORKING = ART / "working" / "r2_darkness_boundary_v1"
APPROVED_ALBEDO = ART / "approved" / "albedo"
APPROVED_AO = ART / "approved" / "ao"
QA = ART / "qa"
METADATA = ART / "metadata"
UNITY = ROOT / "unity" / "TunnelCrew" / "Assets" / "Art" / "Visual" / "ReferenceCalibrationV1"

SIZE = 128
PAD = 8
EDGE = 4


def palette_from(images: list[Path], colors: int = 64) -> Image.Image:
    canvas = Image.new("RGB", (SIZE * len(images), SIZE))
    for index, path in enumerate(images):
        image = Image.open(path).convert("RGB").resize((SIZE, SIZE), Image.Resampling.LANCZOS)
        canvas.paste(image, (index * SIZE, 0))
    return canvas.quantize(colors=colors, method=Image.Quantize.MEDIANCUT)


def quantize(image: Image.Image, palette: Image.Image) -> Image.Image:
    alpha = image.getchannel("A") if image.mode == "RGBA" else None
    result = image.convert("RGB").quantize(palette=palette, dither=Image.Dither.NONE).convert("RGBA")
    if alpha is not None:
        result.putalpha(alpha)
    return result


def luma(image: Image.Image) -> float:
    mean = ImageStat.Stat(image.convert("RGB")).mean
    return sum(value * weight for value, weight in zip(mean, (0.2126, 0.7152, 0.0722)))


def lock_ew_to_reference(image: Image.Image, reference: Image.Image) -> Image.Image:
    result = image.copy()
    band = reference.crop((0, 0, EDGE, SIZE))
    result.paste(band, (0, 0))
    result.paste(band, (SIZE - EDGE, 0))
    return result


def make_rims() -> dict[str, Image.Image]:
    rim_a = Image.open(APPROVED_ALBEDO / "tr01_reference_wall_top_rim_a_albedo.png").convert("RGBA")
    palette = palette_from([
        APPROVED_ALBEDO / "tr01_reference_wall_top_rim_a_albedo.png",
        APPROVED_ALBEDO / "tr01_reference_wall_top_a_albedo.png",
    ])
    result = {}
    for variant in ("b", "c"):
        source = Image.open(SOURCE / f"tr01_reference_wall_top_rim_{variant}_source.png").convert("RGBA")
        source = source.resize((SIZE, SIZE), Image.Resampling.LANCZOS)
        mixed = Image.blend(rim_a, source, 0.32 if variant == "b" else 0.38)
        current_luma = max(1.0, luma(mixed))
        mixed = ImageEnhance.Brightness(mixed).enhance(luma(rim_a) / current_luma)
        mixed = quantize(mixed, palette)
        mixed = lock_ew_to_reference(mixed, rim_a)
        result[variant] = quantize(mixed, palette)
    return result


def extract_crack(path: Path, palette: Image.Image) -> Image.Image:
    source = np.asarray(Image.open(path).convert("RGB"), dtype=np.float32)
    maximum = source.max(axis=2)
    minimum = source.min(axis=2)
    saturation = maximum - minimum
    luma_src = source[:, :, 0] * 0.2126 + source[:, :, 1] * 0.7152 + source[:, :, 2] * 0.0722
    purple_bias = source[:, :, 2] - source[:, :, 1]
    strong = ((saturation >= 18) & (purple_bias >= 5) & (luma_src < 205)) | (luma_src < 115)
    soft = ((saturation >= 10) & (purple_bias >= 3) & (luma_src < 220)) | (luma_src < 145)
    alpha = np.zeros(strong.shape, dtype=np.uint8)
    alpha[soft] = 150
    alpha[strong] = 255

    rgb = np.clip(source, 0, 255).astype(np.uint8)
    rgb[alpha == 0] = 0
    rgb_image = Image.fromarray(rgb, "RGB").resize((SIZE, SIZE), Image.Resampling.LANCZOS)
    alpha_image = Image.fromarray(alpha, "L").resize((SIZE, SIZE), Image.Resampling.LANCZOS)
    alpha_array = np.asarray(alpha_image, dtype=np.uint8).copy()
    alpha_array = np.where(alpha_array < 24, 0, np.where(alpha_array < 112, 96, np.where(alpha_array < 210, 180, 255))).astype(np.uint8)
    alpha_array[:PAD, :] = 0
    alpha_array[-PAD:, :] = 0
    alpha_array[:, :PAD] = 0
    alpha_array[:, -PAD:] = 0
    result = rgb_image.convert("RGBA")
    result.putalpha(Image.fromarray(alpha_array, "L"))
    return quantize(result, palette)


def make_cracks() -> dict[int, Image.Image]:
    palette = palette_from([
        APPROVED_ALBEDO / "tr01_reference_wall_front_a_albedo.png",
        APPROVED_ALBEDO / "tr01_reference_wall_front_b_albedo.png",
        APPROVED_ALBEDO / "tr01_reference_wall_front_c_albedo.png",
    ], colors=48)
    result = {}
    previous = None
    for stage in (1, 2, 3):
        current = extract_crack(SOURCE / f"tr01_reference_wall_crack_{stage}_source.png", palette)
        if previous is not None:
            current = Image.alpha_composite(current, previous)
        result[stage] = current
        previous = current
    return result


def fade(value: int) -> float:
    if value < 64:
        return 1.0
    if value >= 104:
        return 0.0
    return (104 - value) / 40.0


def make_shadows() -> dict[str, Image.Image]:
    center = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 215))
    edge = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    outer = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    inner = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    ep, op, ip = edge.load(), outer.load(), inner.load()
    for y in range(SIZE):
        fy = fade(y)
        for x in range(SIZE):
            fx = fade(x)
            ep[x, y] = (0, 0, 0, round(215 * fy))
            op[x, y] = (0, 0, 0, round(215 * fx * fy))
            ip[x, y] = (0, 0, 0, round(215 * max(fx, fy)))
    return {"center": center, "edge": edge, "corner_outer": outer, "corner_inner": inner}


def save_group(images: dict, kind: str) -> None:
    for key, image in images.items():
        if kind == "rim":
            name = f"tr01_reference_wall_top_rim_{key}_albedo.png"
            approved = APPROVED_ALBEDO
        elif kind == "crack":
            name = f"tr01_reference_wall_crack_{key}_albedo.png"
            approved = APPROVED_ALBEDO
        else:
            name = f"tr01_reference_wall_shadow_{key}.png"
            approved = APPROVED_AO
        image.save(WORKING / name, optimize=True)
        image.save(approved / name, optimize=True)
        if kind == "shadow":
            image.save(approved / name.replace(".png", "_ao.png"), optimize=True)
        image.save(UNITY / name, optimize=True)


def checker(size: tuple[int, int]) -> Image.Image:
    image = Image.new("RGBA", size)
    draw = ImageDraw.Draw(image)
    cell = 8
    for y in range(0, size[1], cell):
        for x in range(0, size[0], cell):
            color = (52, 52, 58, 255) if (x // cell + y // cell) % 2 else (88, 88, 96, 255)
            draw.rectangle((x, y, x + cell - 1, y + cell - 1), fill=color)
    return image


def edge_mse(a: Image.Image, b: Image.Image) -> float:
    aa = np.asarray(a.convert("RGB"), dtype=np.float32)
    bb = np.asarray(b.convert("RGB"), dtype=np.float32)
    return float(np.mean((aa - bb) ** 2))


def make_qa(rims: dict[str, Image.Image], shadows: dict[str, Image.Image], cracks: dict[int, Image.Image]) -> dict:
    rim_a = Image.open(APPROVED_ALBEDO / "tr01_reference_wall_top_rim_a_albedo.png").convert("RGBA")
    rim_all = [rim_a, rims["b"], rims["c"]]
    rim_sheet = Image.new("RGBA", (SIZE * 6, SIZE * 6))
    for y in range(6):
        for x in range(6):
            rim_sheet.paste(rim_all[(x + y) % 3], (x * SIZE, y * SIZE))
    rim_sheet.convert("RGB").save(QA / "tr01_reference_wall_top_rim_abc_6x6.png", optimize=True)

    shadow_sheet = checker((SIZE * 4, SIZE))
    for index, key in enumerate(("center", "edge", "corner_outer", "corner_inner")):
        shadow_sheet.alpha_composite(shadows[key], (index * SIZE, 0))
    shadow_sheet.convert("RGB").save(QA / "tr01_reference_wall_shadow_tileset.png", optimize=True)

    fronts = [Image.open(APPROVED_ALBEDO / f"tr01_reference_wall_front_{v}_albedo.png").convert("RGBA") for v in "abc"]
    crack_sheet = Image.new("RGBA", (SIZE * 3, SIZE * 3))
    for y, front in enumerate(fronts):
        for x, stage in enumerate((1, 2, 3)):
            tile = Image.alpha_composite(front, cracks[stage])
            crack_sheet.paste(tile, (x * SIZE, y * SIZE))
    crack_sheet.convert("RGB").save(QA / "tr01_reference_wall_crack_stages_front_abc.png", optimize=True)

    report = {
        "deliveryPixelsPerCell": SIZE,
        "paddingPixels": PAD,
        "rims": {},
        "shadows": {},
        "cracks": {},
    }
    for variant, image in zip("abc", rim_all):
        report["rims"][variant] = {
            "meanLuma": round(luma(image), 2),
            "leftRightEdgeMse": round(edge_mse(image.crop((0, 0, EDGE, SIZE)), image.crop((SIZE - EDGE, 0, SIZE, SIZE))), 4),
        }
    for key, image in shadows.items():
        alpha = np.asarray(image.getchannel("A"), dtype=np.uint8)
        report["shadows"][key] = {
            "alphaMax": int(alpha.max()),
            "alphaMin": int(alpha.min()),
            "rgbExtrema": image.convert("RGB").getextrema(),
            "alphaAtRows": {str(row): int(alpha[row, SIZE // 2]) for row in (0, 63, 64, 84, 103, 104, 127)},
        }
    for stage, image in cracks.items():
        alpha = np.asarray(image.getchannel("A"), dtype=np.uint8)
        ys, xs = np.nonzero(alpha)
        report["cracks"][str(stage)] = {
            "opaquePixelCount": int(np.count_nonzero(alpha)),
            "alphaSum": int(alpha.sum()),
            "bbox": [int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1],
            "transparentBorderPixels": PAD,
        }
    (METADATA / "r2-darkness-boundary-v1-qa.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    return report


def copy_meta(template_name: str, target_name: str, pivot: tuple[float, float]) -> None:
    template = (UNITY / f"{template_name}.meta").read_text(encoding="utf-8")
    lines = [line for line in template.splitlines() if not line.startswith("guid: ")]
    guid = hashlib.md5(f"TunnelCrew/ReferenceCalibrationV1/{target_name}".encode("utf-8")).hexdigest()
    lines.insert(1, f"guid: {guid}")
    text = "\n".join(lines) + "\n"
    text = text.replace(f"second: {Path(template_name).stem}_0", f"second: {Path(target_name).stem}_0")
    text = text.replace(f"{Path(template_name).stem}_0:", f"{Path(target_name).stem}_0:")
    text = text.replace("spritePivot: {x: 0.5, y: 0.5}", f"spritePivot: {{x: {pivot[0]}, y: {pivot[1]}}}")
    text = text.replace("spritePivot: {x: 0.5, y: 0}", f"spritePivot: {{x: {pivot[0]}, y: {pivot[1]}}}")
    (UNITY / f"{target_name}.meta").write_text(text, encoding="utf-8")


def main() -> None:
    for directory in (WORKING, APPROVED_ALBEDO, APPROVED_AO, QA, METADATA, UNITY):
        directory.mkdir(parents=True, exist_ok=True)
    rims = make_rims()
    cracks = make_cracks()
    shadows = make_shadows()
    save_group(rims, "rim")
    save_group(cracks, "crack")
    save_group(shadows, "shadow")
    report = make_qa(rims, shadows, cracks)

    for variant in "bc":
        name = f"tr01_reference_wall_top_rim_{variant}_albedo.png"
        copy_meta("tr01_reference_wall_top_rim_a_albedo.png", name, (0.5, 0.5))
    for stage in (1, 2, 3):
        name = f"tr01_reference_wall_crack_{stage}_albedo.png"
        copy_meta("tr01_reference_wall_front_a_albedo.png", name, (0.5, 0.0))
    for key in shadows:
        name = f"tr01_reference_wall_shadow_{key}.png"
        copy_meta("tr01_reference_contact_ao_a.png", name, (0.5, 0.5))

    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
