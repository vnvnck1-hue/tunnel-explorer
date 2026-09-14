#!/usr/bin/env python3
"""Remove a baked neutral checkerboard that is connected to the image border.

This is intentionally conservative: only low-chroma, mid/high-value pixels reachable
from the canvas border become transparent. Interior metal highlights cannot be reached
through the dark painted outline and are therefore preserved.
"""

from __future__ import annotations

import argparse
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--grid-cols", type=int, default=1)
    parser.add_argument("--grid-rows", type=int, default=1)
    args = parser.parse_args()

    rgb = np.asarray(Image.open(args.source).convert("RGB"), dtype=np.uint8)
    height, width, _ = rgb.shape
    lo = rgb.min(axis=2).astype(np.int16)
    hi = rgb.max(axis=2).astype(np.int16)
    candidate = ((hi - lo) <= 42) & (lo >= 88)

    background = np.zeros((height, width), dtype=bool)
    queue: deque[tuple[int, int]] = deque()

    def seed(x: int, y: int) -> None:
        if candidate[y, x] and not background[y, x]:
            background[y, x] = True
            queue.append((x, y))

    for x in range(width):
        seed(x, 0)
        seed(x, height - 1)
    for y in range(height):
        seed(0, y)
        seed(width - 1, y)

    while queue:
        x, y = queue.popleft()
        for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if 0 <= nx < width and 0 <= ny < height and candidate[ny, nx] and not background[ny, nx]:
                background[ny, nx] = True
                queue.append((nx, ny))

    rgba = np.empty((height, width, 4), dtype=np.uint8)
    rgba[..., :3] = rgb
    rgba[..., 3] = np.where(background, 0, 255).astype(np.uint8)
    rgba[background, :3] = 0

    if width % args.grid_cols or height % args.grid_rows:
        raise ValueError("image dimensions must be divisible by the requested cleanup grid")

    cell_width = width // args.grid_cols
    cell_height = height // args.grid_rows
    for row in range(args.grid_rows):
        for col in range(args.grid_cols):
            x0, y0 = col * cell_width, row * cell_height
            cell = rgba[y0 : y0 + cell_height, x0 : x0 + cell_width, 3] > 0
            visited = np.zeros_like(cell)
            components: list[list[tuple[int, int]]] = []
            for cy, cx in np.argwhere(cell):
                if visited[cy, cx]:
                    continue
                component: list[tuple[int, int]] = []
                component_queue: deque[tuple[int, int]] = deque([(int(cx), int(cy))])
                visited[cy, cx] = True
                while component_queue:
                    px, py = component_queue.popleft()
                    component.append((px, py))
                    for nx, ny in ((px - 1, py), (px + 1, py), (px, py - 1), (px, py + 1)):
                        if (
                            0 <= nx < cell_width
                            and 0 <= ny < cell_height
                            and cell[ny, nx]
                            and not visited[ny, nx]
                        ):
                            visited[ny, nx] = True
                            component_queue.append((nx, ny))
                components.append(component)

            if not components:
                raise ValueError(f"grid cell {row},{col} has no foreground")
            largest = max(components, key=len)
            keep = np.zeros_like(cell)
            for px, py in largest:
                keep[py, px] = True
            rejected = cell & ~keep
            rgba[y0 : y0 + cell_height, x0 : x0 + cell_width, 3][rejected] = 0
            rgba[y0 : y0 + cell_height, x0 : x0 + cell_width, :3][rejected] = 0

    args.output.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(rgba, "RGBA").save(args.output)

    alpha = rgba[..., 3]
    print(
        f"{args.output}: {width}x{height}; transparent={int(np.count_nonzero(alpha == 0))}; "
        f"opaque={int(np.count_nonzero(alpha == 255))}; partial={int(np.count_nonzero((alpha > 0) & (alpha < 255)))}"
    )


if __name__ == "__main__":
    main()
