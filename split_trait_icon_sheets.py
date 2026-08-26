from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parent
SOURCE_DIR = Path(r"C:\Users\Loadcomplete\.codex\generated_images\01a03c10-a05d-74b0-bc3f-8c6a572e5c66")
OUTPUT_DIR = ROOT / "assets" / "menu" / "trait-resources" / "icons"


SHEETS = [
    (
        SOURCE_DIR / "exec-afa7c7bf-9797-466b-bf7c-8b273b25dbf8.png",
        [
            "trait-icon-crack-fissure-v1.png",
            "trait-icon-core-bedrock-v1.png",
            "trait-icon-seismic-shockwave-v1.png",
            "trait-icon-explosive-charge-v1.png",
        ],
    ),
    (
        SOURCE_DIR / "exec-4b2de8c0-ba9f-4873-9aa1-c74d1694fcd7.png",
        [
            "trait-icon-fuse-detonator-v1.png",
            "trait-icon-shrapnel-v1.png",
            "trait-icon-grapple-winch-v1.png",
            "trait-icon-scanner-radar-v1.png",
        ],
    ),
    (
        SOURCE_DIR / "exec-78d13c1c-ce53-4628-9bbd-b0364e3a05f2.png",
        [
            "trait-icon-sentry-turret-v1.png",
            "trait-icon-dual-power-v1.png",
            "trait-icon-ricochet-v1.png",
            "trait-icon-survival-bulkhead-v1.png",
        ],
    ),
    (
        SOURCE_DIR / "exec-95200849-8919-4056-807a-f6a16f1c29b4.png",
        [
            "trait-icon-piercing-round-v1.png",
            "trait-icon-ore-refinery-v1.png",
            "trait-icon-rapid-fire-turbo-v1.png",
            "trait-icon-control-network-v1.png",
        ],
    ),
]


def make_transparent(image: Image.Image) -> Image.Image:
    """Remove the near-white baked checkerboard while retaining icon colors."""
    image = image.convert("RGBA")
    pixels = image.load()
    for y in range(image.height):
        for x in range(image.width):
            r, g, b, a = pixels[x, y]
            near_white = min(r, g, b) >= 242 and (max(r, g, b) - min(r, g, b)) <= 14
            if near_white:
                pixels[x, y] = (r, g, b, 0)
    return image


def main() -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    written = []

    for sheet_path, names in SHEETS:
        with Image.open(sheet_path) as source:
            source = source.convert("RGBA")
            half_w = source.width // 2
            half_h = source.height // 2
            boxes = [
                (0, 0, half_w, half_h),
                (half_w, 0, source.width, half_h),
                (0, half_h, half_w, source.height),
                (half_w, half_h, source.width, source.height),
            ]

            for box, name in zip(boxes, names):
                icon = source.crop(box)
                icon = make_transparent(icon)
                icon = icon.resize((1254, 1254), Image.Resampling.LANCZOS)
                output_path = OUTPUT_DIR / name
                icon.save(output_path, format="PNG", optimize=True)
                written.append(output_path)

    for path in written:
        with Image.open(path) as image:
            alpha = image.getchannel("A")
            corner_alpha = alpha.getpixel((0, 0))
            nonzero = alpha.getbbox() is not None
            if image.size != (1254, 1254) or corner_alpha != 0 or not nonzero:
                raise RuntimeError(f"Transparency/size verification failed: {path}")
            print(f"{path.name}\t{image.size[0]}x{image.size[1]}\tcorner_alpha={corner_alpha}\tbytes={path.stat().st_size}")

    print(f"Wrote {len(written)} icons to {OUTPUT_DIR}")


if __name__ == "__main__":
    main()
