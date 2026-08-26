from pathlib import Path
from PIL import Image
import numpy as np

ROOT = Path(r"C:\Users\Loadcomplete\Documents\ChatGPT\땅굴 크루 만들기")
RAW = Path(r"C:\Users\Loadcomplete\.codex\generated_images\01a03c50-a2e6-71f1-8910-9c64f94015b2")

SOURCES = {
    "crawler": RAW / "exec-43becab6-7095-4a8f-a9ab-22715d5d220c.png",
    "spitter": RAW / "exec-a4facb92-6f51-4f22-a005-2d4914feba67.png",
    "brood-beast": RAW / "exec-6a42ce07-944a-4fc9-b27b-e65ccc219d34.png",
}

FRAME = 256
COLS = 8
ROWS = 2
TARGET = 200


def is_background(pixel):
    r, g, b = pixel[:3]
    return min(r, g, b) > 205 and max(pixel[:3]) - min(pixel[:3]) < 24


def content_runs(source, row):
    image = np.array(source.convert("RGB"))
    height = image.shape[0]
    top = round(row * height / 2)
    bottom = round((row + 1) * height / 2)
    mask = (image[top:bottom].min(axis=2) < 205) | ((image[top:bottom].max(axis=2) - image[top:bottom].min(axis=2)) > 24)
    projection = mask.sum(axis=0) > 8
    runs = []
    start = None
    for index, active in enumerate(projection):
        if active and start is None:
            start = index
        if start is not None and (not active or index == len(projection) - 1):
            end = index if not active else index + 1
            if end - start > 20:
                runs.append((start, end))
            start = None
    return runs


def extract_frame(source, bbox):
    tile = source.crop(bbox).convert("RGBA")

    alpha = Image.new("L", tile.size, 255)
    px = alpha.load()
    rgb = tile.convert("RGB")
    for y in range(tile.height):
        for x in range(tile.width):
            if is_background(rgb.getpixel((x, y))):
                px[x, y] = 0

    bbox = alpha.getbbox()
    canvas = Image.new("RGBA", (FRAME, FRAME), (0, 0, 0, 0))
    if bbox:
        content = tile.crop(bbox)
        content_alpha = alpha.crop(bbox)
        scale = min(TARGET / content.width, TARGET / content.height, 1.0)
        size = (max(1, round(content.width * scale)), max(1, round(content.height * scale)))
        content = content.resize(size, Image.Resampling.LANCZOS)
        content_alpha = content_alpha.resize(size, Image.Resampling.LANCZOS)
        content.putalpha(content_alpha)
        x = (FRAME - size[0]) // 2
        y = (FRAME - size[1]) // 2
        canvas.alpha_composite(content, (x, y))
    return canvas


for name, path in SOURCES.items():
    source = Image.open(path).convert("RGB")
    sheet = Image.new("RGBA", (FRAME * COLS, FRAME * ROWS), (0, 0, 0, 0))
    for row in range(ROWS):
        runs = content_runs(source, row)
        while len(runs) < COLS:
            runs.append(runs[-1])
        runs = runs[:COLS]
        source_height = source.height
        row_top = round(row * source_height / 2)
        row_bottom = round((row + 1) * source_height / 2)
        for col, (left, right) in enumerate(runs):
            crop_box = (left, row_top, right, row_bottom)
            frame = extract_frame(source, crop_box)
            sheet.alpha_composite(frame, (col * FRAME, row * FRAME))
    out = ROOT / f"monster-{name}-anim-v1.5.2.png"
    sheet.save(out)
    print(f"{out.name}: {sheet.size}, {sheet.mode}")
