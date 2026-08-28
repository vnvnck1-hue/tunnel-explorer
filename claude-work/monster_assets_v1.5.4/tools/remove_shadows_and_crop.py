from pathlib import Path
from PIL import Image, ImageFilter

ROOT = Path(r"C:\Users\Loadcomplete\Documents\ChatGPT\땅굴 크루 만들기")
MONSTERS = ["crawler", "spitter", "brood-beast"]
FRAME = 256
COLS = 8


def clean_sheet(sheet):
    rgba = sheet.convert("RGBA")
    rgb = rgba.convert("RGB")
    core = Image.new("L", rgba.size, 0)
    core_px = core.load()
    rgb_px = rgb.load()
    alpha_px = rgba.getchannel("A").load()

    # Keep dark outlines and chromatic character pixels. Light neutral-gray
    # pixels are the generated grounding shadows and are removed.
    for y in range(rgba.height):
        for x in range(rgba.width):
            if alpha_px[x, y] == 0:
                continue
            r, g, b = rgb_px[x, y]
            brightness = (r + g + b) / 3
            chroma = max(r, g, b) - min(r, g, b)
            if brightness < 125 or chroma > 24:
                core_px[x, y] = 255

    # Preserve a narrow anti-aliased edge around the actual character.
    expanded = core.filter(ImageFilter.MaxFilter(7))
    keep = Image.new("L", rgba.size, 0)
    keep_px = keep.load()
    expanded_px = expanded.load()
    for y in range(rgba.height):
        for x in range(rgba.width):
            if expanded_px[x, y] == 0 or alpha_px[x, y] == 0:
                continue
            r, g, b = rgb_px[x, y]
            brightness = (r + g + b) / 3
            chroma = max(r, g, b) - min(r, g, b)
            # Neutral mid/light gray is the grounding shadow/halo.
            if chroma < 30 and brightness > 90:
                continue
            keep_px[x, y] = 255
    cleaned = rgba.copy()
    cleaned.putalpha(keep)
    return cleaned


for monster in MONSTERS:
    source_path = ROOT / f"monster-{monster}-anim-v1.5.2.png"
    sheet = clean_sheet(Image.open(source_path))
    sheet_path = ROOT / f"monster-{monster}-anim-v1.5.4.png"
    sheet.save(sheet_path)

    frames_dir = ROOT / f"monster-{monster}-frames-v1.5.4"
    frames_dir.mkdir(exist_ok=True)
    for index in range(16):
        col = index % COLS
        row = index // COLS
        box = (col * FRAME, row * FRAME, (col + 1) * FRAME, (row + 1) * FRAME)
        sheet.crop(box).save(frames_dir / f"frame_{index + 1:02d}.png")
    print(f"{monster}: cleaned sheet + 16 frames")
