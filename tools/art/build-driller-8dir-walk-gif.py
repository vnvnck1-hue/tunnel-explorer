"""Build a fixed-pivot four-frame 8-direction walk preview.

The generated animation poses are treated as source artwork.  Each isolated
direction is scaled into its approved turnaround slot and anchored by the
bottom-center of that slot on every frame.  This removes generator layout
drift without redrawing the character.
"""

from __future__ import annotations

from collections import deque
from pathlib import Path
import argparse
import json

import numpy as np
from PIL import Image


DIRECTIONS = ("N", "NE", "E", "SE", "S", "SW", "W", "NW")


def component_boxes(alpha: np.ndarray) -> list[tuple[int, int, int, int]]:
    foreground = alpha > 16
    visited = np.zeros_like(foreground, dtype=bool)
    height, width = foreground.shape
    boxes: list[tuple[int, int, int, int]] = []
    for sy, sx in zip(*np.where(foreground)):
        if visited[sy, sx]:
            continue
        queue = deque([(int(sy), int(sx))])
        visited[sy, sx] = True
        points: list[tuple[int, int]] = []
        while queue:
            y, x = queue.popleft()
            points.append((y, x))
            for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
                if 0 <= ny < height and 0 <= nx < width and foreground[ny, nx] and not visited[ny, nx]:
                    visited[ny, nx] = True
                    queue.append((ny, nx))
        if len(points) >= 100:
            data = np.asarray(points, dtype=np.int32)
            boxes.append((
                int(data[:, 1].min()), int(data[:, 0].min()),
                int(data[:, 1].max()) + 1, int(data[:, 0].max()) + 1,
            ))
    return boxes


def directional_boxes(image: Image.Image) -> dict[str, tuple[int, int, int, int]]:
    boxes = component_boxes(np.asarray(image.getchannel("A"), dtype=np.uint8))
    if len(boxes) != 8:
        raise ValueError(f"expected exactly 8 separated figures, found {len(boxes)}")
    cx, cy = image.width / 2, image.height / 2
    expected = {
        "N": (0.0, -1.0), "NE": (1.0, -1.0), "E": (1.0, 0.0), "SE": (1.0, 1.0),
        "S": (0.0, 1.0), "SW": (-1.0, 1.0), "W": (-1.0, 0.0), "NW": (-1.0, -1.0),
    }
    remaining = set(range(len(boxes)))
    assigned: dict[str, tuple[int, int, int, int]] = {}
    for direction in DIRECTIONS:
        ex, ey = expected[direction]
        length = (ex * ex + ey * ey) ** 0.5
        ex, ey = ex / length, ey / length
        best = max(
            remaining,
            key=lambda i: (
                (((boxes[i][0] + boxes[i][2]) / 2 - cx) * ex + ((boxes[i][1] + boxes[i][3]) / 2 - cy) * ey)
                / max(1.0, (((boxes[i][0] + boxes[i][2]) / 2 - cx) ** 2 + ((boxes[i][1] + boxes[i][3]) / 2 - cy) ** 2) ** 0.5)
            ),
        )
        assigned[direction] = boxes[best]
        remaining.remove(best)
    return assigned


def align_frame(source: Path, reference: Image.Image, reference_boxes: dict[str, tuple[int, int, int, int]]) -> Image.Image:
    image = Image.open(source).convert("RGBA")
    source_boxes = directional_boxes(image)
    canvas = Image.new("RGBA", reference.size, (0, 0, 0, 0))
    for direction in DIRECTIONS:
        sx0, sy0, sx1, sy1 = source_boxes[direction]
        rx0, ry0, rx1, ry1 = reference_boxes[direction]
        figure = image.crop((sx0, sy0, sx1, sy1))
        scale = min((rx1 - rx0) / figure.width, (ry1 - ry0) / figure.height)
        size = (max(1, round(figure.width * scale)), max(1, round(figure.height * scale)))
        figure = figure.resize(size, Image.Resampling.LANCZOS)

        # The approved slot's bottom center is the immutable ground pivot.
        pivot_x = (rx0 + rx1) / 2
        pivot_y = ry1
        paste_x = round(pivot_x - figure.width / 2)
        paste_y = round(pivot_y - figure.height)
        canvas.alpha_composite(figure, (paste_x, paste_y))
    return canvas


def save_gif(frames: list[Image.Image], output: Path, duration: int) -> None:
    # RGBA adaptive palette preserves a transparent index for the preview GIF.
    paletted = []
    for frame in frames:
        alpha = frame.getchannel("A")
        rgb = Image.new("RGB", frame.size, (0, 0, 0))
        rgb.paste(frame.convert("RGB"), mask=alpha)
        p = rgb.quantize(colors=255, method=Image.Quantize.MEDIANCUT)
        palette = p.getpalette()
        mask = np.asarray(alpha) <= 16
        pixels = np.asarray(p).copy()
        pixels[mask] = 255
        p = Image.fromarray(pixels.astype(np.uint8), mode="P")
        p.putpalette(palette)
        p.info["transparency"] = 255
        paletted.append(p)
    paletted[0].save(
        output,
        save_all=True,
        append_images=paletted[1:],
        duration=duration,
        loop=0,
        transparency=255,
        disposal=2,
        optimize=False,
    )


def main() -> None:
    raise RuntimeError(
        "Disabled by the 2026-09-09 user decision: do not generate or assemble "
        "character animation frames or GIFs through ChatGPT/Codex."
    )
    parser = argparse.ArgumentParser()
    parser.add_argument("--reference", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--gif", type=Path, required=True)
    parser.add_argument("--duration", type=int, default=100)
    parser.add_argument("frames", nargs=4, type=Path)
    args = parser.parse_args()

    reference = Image.open(args.reference).convert("RGBA")
    reference_boxes = directional_boxes(reference)
    args.output_dir.mkdir(parents=True, exist_ok=True)
    args.gif.parent.mkdir(parents=True, exist_ok=True)

    aligned = [align_frame(path, reference, reference_boxes) for path in args.frames]
    frame_paths = []
    for index, frame in enumerate(aligned):
        path = args.output_dir / f"frame_{index:03d}.png"
        frame.save(path, optimize=True)
        frame_paths.append(str(path))
    save_gif(aligned, args.gif, args.duration)

    preview = Image.new("RGBA", reference.size, (0, 0, 0, 0))
    half = reference.width // 2
    for index, frame in enumerate(aligned):
        thumb = frame.resize((half, half), Image.Resampling.LANCZOS)
        preview.alpha_composite(thumb, ((index % 2) * half, (index // 2) * half))
    contact_path = args.output_dir.parent / "driller-8dir-walk-4f-contact.png"
    preview.save(contact_path, optimize=True)

    report = {
        "reference": str(args.reference),
        "canvas": list(reference.size),
        "directions": list(DIRECTIONS),
        "pivot_rule": "approved reference slot bottom-center, fixed across all frames",
        "frames": frame_paths,
        "gif": str(args.gif),
        "frame_duration_ms": args.duration,
        "loop_duration_ms": args.duration * len(aligned),
        "contact_sheet": str(contact_path),
        "reference_boxes": reference_boxes,
    }
    (args.output_dir / "build-report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
