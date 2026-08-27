from __future__ import annotations

import argparse
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image


def flood_background(rgb: np.ndarray) -> np.ndarray:
    """Return the checkerboard-like background connected to the image border."""
    maxc = rgb.max(axis=2)
    minc = rgb.min(axis=2)
    mean = rgb.mean(axis=2)
    # The generator sometimes bakes its transparency preview into RGB pixels.
    # Neutral, bright pixels are the checkerboard; flood-fill only from borders
    # so cream highlights inside the dragon are retained.
    candidate = ((maxc - minc) <= 18) & (mean >= 200)
    h, w = candidate.shape
    seen = np.zeros((h, w), dtype=bool)
    q: deque[tuple[int, int]] = deque()

    for x in range(w):
        if candidate[0, x] and not seen[0, x]:
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


def load_isolated(path: Path, size: int) -> Image.Image:
    im = Image.open(path).convert("RGBA")
    rgba = np.array(im)

    if np.all(rgba[:, :, 3] == 255):
        bg = flood_background(rgba[:, :, :3])
        rgba[bg, 3] = 0
    else:
        # Any RGB pixels under zero alpha are irrelevant; normalize them so
        # they cannot reappear during palette conversion.
        rgba[rgba[:, :, 3] == 0, :3] = 0

    # Keep the complete generated canvas: no content crop is used.
    out = Image.fromarray(rgba, "RGBA").resize((size, size), Image.Resampling.LANCZOS)

    # GIF has one transparent palette index, so make the cutout deterministic.
    alpha = out.getchannel("A").point(lambda a: 255 if a >= 128 else 0)
    out.putalpha(alpha)
    return out


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input-dir", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--size", type=int, default=512)
    args = parser.parse_args()

    paths = [args.input_dir / f"frame_{i:02d}.png" for i in range(1, 9)]
    missing = [str(p) for p in paths if not p.exists()]
    if missing:
        raise SystemExit(f"Missing frames: {', '.join(missing)}")

    frames = []
    for path in paths:
        frame = load_isolated(path, args.size)
        frame.save(path, "PNG", optimize=True)
        frames.append(frame)

    args.output.parent.mkdir(parents=True, exist_ok=True)
    durations = [80, 80, 80, 80, 80, 80, 80, 160]
    frames[0].save(
        args.output,
        "GIF",
        save_all=True,
        append_images=frames[1:],
        duration=durations,
        loop=0,
        disposal=2,
        transparency=0,
        optimize=False,
    )

    print(f"wrote {args.output} ({len(frames)} frames, {args.size}x{args.size})")
    for path, frame in zip(paths, frames):
        alpha = frame.getchannel("A")
        print(path.name, frame.mode, frame.size, "alpha_bbox=", alpha.getbbox())


if __name__ == "__main__":
    main()
