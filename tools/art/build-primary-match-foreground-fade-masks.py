#!/usr/bin/env python3
"""Build white silhouette fade-mask candidates for Primary Match foreground props."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / "art-production/test-room-v01/working/primary-match-v2"
DELIVERY = WORK / "delivery-candidates/manifest.json"
OUT_DIR = WORK / "delivery-candidates/fade"
OUT_CONTRACT = WORK / "foreground-fade-mask-candidates.json"
OUT_REVIEW = WORK / "diorama-process-28-foreground-fade-mask-review.png"


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    face = "C:/Windows/Fonts/seguisb.ttf" if bold else "C:/Windows/Fonts/segoeui.ttf"
    try:
        return ImageFont.truetype(face, size)
    except OSError:
        return ImageFont.load_default()


def contain(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    scale = min(size[0] / image.width, size[1] / image.height)
    return image.resize((max(1, round(image.width * scale)), max(1, round(image.height * scale))), Image.Resampling.LANCZOS)


def checker(size: tuple[int, int], cell: int = 18) -> Image.Image:
    image = Image.new("RGBA", size, (38, 29, 46, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], cell):
        for x in range(0, size[0], cell):
            if (x // cell + y // cell) % 2:
                draw.rectangle((x, y, x + cell - 1, y + cell - 1), fill=(57, 44, 68, 255))
    return image


def main() -> None:
    delivery = json.loads(DELIVERY.read_text(encoding="utf-8"))
    foreground = [
        asset for asset in delivery["assets"]
        if asset["runtimeCandidate"]["runtimeKind"] == "foreground_setpiece"
    ]
    if len(foreground) != 3:
        raise ValueError(f"expected 3 foreground setpieces, got {len(foreground)}")
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    entries = []
    for asset in foreground:
        albedo_path = DELIVERY.parent / asset["channels"]["albedo"]
        albedo = Image.open(albedo_path).convert("RGBA")
        alpha = albedo.getchannel("A")
        output_name = Path(asset["channels"]["albedo"]).name.replace("_albedo.png", "_fade.png")
        output_path = OUT_DIR / output_name
        fade = Image.new("RGBA", albedo.size, (255, 255, 255, 0))
        fade.putalpha(alpha)
        fade.save(output_path)
        entries.append({
            "assetId": asset["assetId"],
            "sourceAlbedo": str(albedo_path.relative_to(WORK)).replace("\\", "/"),
            "sourceAlbedoSha256": sha256(albedo_path),
            "fadeMaskPath": str(output_path.relative_to(WORK)).replace("\\", "/"),
            "fadeMaskSha256": sha256(output_path),
            "dimensionsPixels": list(albedo.size),
            "rgbaContract": "RGB=white_255; A=source_albedo_alpha_exact",
            "alphaIdenticalToAlbedo": True,
            "targetFadeAlpha": asset["runtimeCandidate"]["fadeTargetAlpha"],
            "occluderGroup": asset["runtimeCandidate"]["occluderGroup"],
            "status": "candidate_not_unity_verified",
        })

    contract = {
        "status": "offline_candidate_not_unity_integrated",
        "unityExecuted": False,
        "currentRuntimeConsumption": "not_consumed_uniform_sprite_alpha_is_current",
        "futureUse": "fadeMaskPath contract for selective foreground fade",
        "assets": entries,
        "summary": {
            "assetCount": len(entries),
            "alphaIdenticalMasks": sum(entry["alphaIdenticalToAlbedo"] for entry in entries),
            "reviewImage": OUT_REVIEW.name,
        },
    }
    OUT_CONTRACT.write_text(json.dumps(contract, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    board = Image.new("RGB", (1920, 1080), (12, 8, 18))
    draw = ImageDraw.Draw(board)
    draw.text((36, 20), "PROCESS 28  /  FOREGROUND FADE-MASK CANDIDATES", font=font(24, True), fill=(244, 228, 247))
    draw.text((36, 52), "white RGB + exact albedo alpha  |  current runtime still uses uniform sprite alpha", font=font(14), fill=(164, 147, 175))
    panel_width = 590
    for index, (asset, entry) in enumerate(zip(foreground, entries)):
        x = 30 + index * 630
        y = 92
        draw.rounded_rectangle((x, y, x + panel_width, 1035), radius=8, fill=(24, 16, 31), outline=(84, 61, 101), width=2)
        title = asset["sourceVariantId"].split("/")[-1].replace("_", " ").upper()
        draw.text((x + 18, y + 14), title, font=font(17, True), fill=(237, 219, 243))
        albedo = Image.open(DELIVERY.parent / asset["channels"]["albedo"]).convert("RGBA")
        fade = Image.open(WORK / entry["fadeMaskPath"]).convert("RGBA")
        albedo_preview = contain(albedo, (520, 260))
        fade_preview = contain(fade, (520, 260))
        for row, (preview, label) in enumerate(((albedo_preview, "ALBEDO"), (fade_preview, "WHITE SILHOUETTE MASK"))):
            top = y + 62 + row * 318
            plate = checker((550, 278))
            px, py = (550 - preview.width) // 2, (278 - preview.height) // 2
            plate.alpha_composite(preview, (px, py))
            board.paste(plate.convert("RGB"), (x + 20, top))
            draw.text((x + 24, top + 284), label, font=font(13, True), fill=(77, 231, 212) if row else (195, 181, 205))
        faded = albedo.copy()
        faded.putalpha(faded.getchannel("A").point(lambda value: round(value * entry["targetFadeAlpha"])))
        faded_preview = contain(faded, (520, 220))
        plate = checker((550, 238))
        plate.alpha_composite(faded_preview, ((550 - faded_preview.width) // 2, (238 - faded_preview.height) // 2))
        board.paste(plate.convert("RGB"), (x + 20, y + 705))
        draw.text((x + 24, y + 949), f"UNIFORM FADE PREVIEW  /  ALPHA {entry['targetFadeAlpha']:.2f}", font=font(13, True), fill=(255, 170, 76))
        draw.text((x + 24, y + 976), f"{asset['dimensionsPixels'][0]}x{asset['dimensionsPixels'][1]}  |  {entry['occluderGroup']}", font=font(12), fill=(149, 135, 161))
    board.save(OUT_REVIEW, quality=95)
    print(f"Wrote {OUT_CONTRACT.relative_to(ROOT)}")
    print(f"Wrote {len(entries)} fade masks under {OUT_DIR.relative_to(ROOT)}")
    print(f"Wrote {OUT_REVIEW.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
