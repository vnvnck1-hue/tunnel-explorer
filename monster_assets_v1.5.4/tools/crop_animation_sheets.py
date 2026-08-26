from pathlib import Path
from PIL import Image

ROOT = Path(r"C:\Users\Loadcomplete\Documents\ChatGPT\땅굴 크루 만들기")
MONSTERS = ["crawler", "spitter", "brood-beast"]
FRAME_SIZE = 256
COLS = 8

for monster in MONSTERS:
    sheet_path = ROOT / f"monster-{monster}-anim-v1.5.2.png"
    output_dir = ROOT / f"monster-{monster}-frames-v1.5.2"
    output_dir.mkdir(exist_ok=True)
    sheet = Image.open(sheet_path).convert("RGBA")

    for index in range(16):
        col = index % COLS
        row = index // COLS
        box = (
            col * FRAME_SIZE,
            row * FRAME_SIZE,
            (col + 1) * FRAME_SIZE,
            (row + 1) * FRAME_SIZE,
        )
        frame = sheet.crop(box)
        frame.save(output_dir / f"frame_{index + 1:02d}.png")

    print(f"{monster}: 16 frames -> {output_dir}")
