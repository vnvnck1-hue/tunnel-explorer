from __future__ import annotations

import argparse
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image


def border_connected_white(rgb: np.ndarray) -> np.ndarray:
    """Find pale, neutral pixels connected to the outer border."""
    maxc = rgb.max(axis=2).astype(np.int16)
    minc = rgb.min(axis=2).astype(np.int16)
    mean = rgb.mean(axis=2)
    # Include the pale gray/pink pixels produced by antialiasing against the
    # white source matte, not only pure white pixels.
    candidate = ((maxc - minc) <= 60) & (mean >= 170)
    h, w = candidate.shape
    seen = np.zeros((h, w), dtype=bool)
    q: deque[tuple[int, int]] = deque()

    for x in range(w):
        if candidate[0, x]:
            seen[0, x] = True
            q.append((0, x))
        if candidate[h - 1, x] and not seen[h - 1, x]:
            seen[h - 1, x] = True
            q.append((h - 1, x))
    for y in range(h):
        if candidate[y, 0] and not seen[y, 0]:
            seen[y, 0] = True
            q.append((y, 0))
        if candidate[y, w - 1] and not seen[y, w - 1]:
            seen[y, w - 1] = True
            q.append((y, w - 1))

    while q:
        y, x = q.popleft()
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                if not (dx or dy):
                    continue
                ny, nx = y + dy, x + dx
                if 0 <= ny < h and 0 <= nx < w and candidate[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    q.append((ny, nx))
    return seen


def clean_frame(frame: Image.Image) -> Image.Image:
    rgba = np.array(frame.convert("RGBA"))
    rgb = rgba[:, :, :3]
    alpha = rgba[:, :, 3]

    # GIF compositing presents the white background as opaque even when a
    # transparency index exists in the source metadata, so inspect the pixels.
    background = border_connected_white(rgb)

    # Remove a one-pixel pale antialias fringe around the detected background,
    # while leaving saturated dragon colors and the cream belly untouched.
    near_white = ((rgb.max(axis=2).astype(np.int16) - rgb.min(axis=2).astype(np.int16)) <= 90) & (rgb.mean(axis=2) >= 120)
    expanded = background.copy()
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            if not (dx or dy):
                continue
            shifted = np.zeros_like(background)
            y0, y1 = max(0, dy), min(background.shape[0], background.shape[0] + dy)
            x0, x1 = max(0, dx), min(background.shape[1], background.shape[1] + dx)
            shifted[y0:y1, x0:x1] = background[y0 - dy:y1 - dy, x0 - dx:x1 - dx]
            expanded |= shifted & near_white

    alpha[expanded] = 0
    rgba[:, :, 3] = alpha
    rgba[alpha == 0, :3] = 0
    return Image.fromarray(rgba, "RGBA")


def main() -> None:
    parser = argparse.ArgumentParser(description="Remove border-connected white background from every GIF frame.")
    parser.add_argument("--input", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    source = Image.open(args.input)
    frames = []
    durations = []
    for index in range(getattr(source, "n_frames", 1)):
        source.seek(index)
        frames.append(clean_frame(source.copy()))
        durations.append(source.info.get("duration", 100))

    args.output.parent.mkdir(parents=True, exist_ok=True)
    frames[0].save(
        args.output,
        "GIF",
        save_all=True,
        append_images=frames[1:],
        duration=durations,
        loop=source.info.get("loop", 0),
        disposal=2,
        transparency=0,
        optimize=False,
    )

    print(f"wrote {args.output} ({len(frames)} frames, {frames[0].size[0]}x{frames[0].size[1]})")
    for index, (frame, duration) in enumerate(zip(frames, durations), start=1):
        print(f"frame_{index:02d}: alpha_bbox={frame.getchannel('A').getbbox()} duration={duration}")


if __name__ == "__main__":
    main()
