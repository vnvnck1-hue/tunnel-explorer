from collections import deque
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parent
CARD_DIR = ROOT / "assets" / "menu" / "trait-resources" / "cards"
CARDS = (
    ("trait-card-common-v1.1.png", "trait-card-common-v1.1.1.png"),
    ("trait-card-rare-v1.1.png", "trait-card-rare-v1.1.1.png"),
    ("trait-card-hero-v1.1.png", "trait-card-hero-v1.1.1.png"),
    ("trait-card-legendary-v1.1.png", "trait-card-legendary-v1.1.1.png"),
)


def is_outer_background(pixel: tuple[int, int, int, int]) -> bool:
    r, g, b, _ = pixel
    return min(r, g, b) >= 228 and max(r, g, b) - min(r, g, b) <= 18


def clear_connected_background(source: Image.Image) -> Image.Image:
    image = source.convert("RGBA")
    width, height = image.size
    pixels = image.load()
    visited = bytearray(width * height)
    queue: deque[tuple[int, int]] = deque()

    def enqueue(x: int, y: int) -> None:
        index = y * width + x
        if visited[index] or not is_outer_background(pixels[x, y]):
            return
        visited[index] = 1
        queue.append((x, y))

    for x in range(width):
        enqueue(x, 0)
        enqueue(x, height - 1)
    for y in range(height):
        enqueue(0, y)
        enqueue(width - 1, y)

    while queue:
        x, y = queue.popleft()
        r, g, b, _ = pixels[x, y]
        pixels[x, y] = (r, g, b, 0)
        if x > 0:
            enqueue(x - 1, y)
        if x + 1 < width:
            enqueue(x + 1, y)
        if y > 0:
            enqueue(x, y - 1)
        if y + 1 < height:
            enqueue(x, y + 1)

    return image


def main() -> None:
    for source_name, target_name in CARDS:
        source_path = CARD_DIR / source_name
        target_path = CARD_DIR / target_name
        if target_path.exists():
            raise FileExistsError(f"Refusing to overwrite {target_path}")
        with Image.open(source_path) as source:
            output = clear_connected_background(source)
            output.save(target_path, format="PNG", optimize=True)
        with Image.open(target_path) as check:
            alpha = check.getchannel("A")
            center = check.getpixel((check.width // 2, int(check.height * 0.38)))
            if check.size != (1024, 1536) or alpha.getpixel((0, 0)) != 0 or center[3] != 255:
                raise RuntimeError(f"Runtime card verification failed: {target_path}")
        print(f"{target_name}\t1024x1536\ttransparent outer background\topaque center")


if __name__ == "__main__":
    main()
