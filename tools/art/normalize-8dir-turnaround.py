"""Normalize an approved eight-direction turnaround without redrawing it."""

from collections import deque
from pathlib import Path
import argparse
import statistics

import numpy as np
from PIL import Image


def component_regions(
    alpha: np.ndarray,
    threshold: int = 16,
) -> list[tuple[tuple[int, int, int, int], np.ndarray]]:
    foreground = alpha > threshold
    visited = np.zeros_like(foreground, dtype=bool)
    height, width = foreground.shape
    regions = []
    for sy, sx in zip(*np.where(foreground)):
        if visited[sy, sx]:
            continue
        queue = deque([(int(sy), int(sx))])
        visited[sy, sx] = True
        area = 0
        points = []
        while queue:
            y, x = queue.popleft()
            area += 1
            points.append((y, x))
            for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
                if 0 <= ny < height and 0 <= nx < width and foreground[ny, nx] and not visited[ny, nx]:
                    visited[ny, nx] = True
                    queue.append((ny, nx))
        if area >= 100:
            points = np.asarray(points, dtype=np.int32)
            box = (
                int(points[:, 1].min()), int(points[:, 0].min()),
                int(points[:, 1].max()) + 1, int(points[:, 0].max()) + 1,
            )
            mask = np.zeros((box[3] - box[1], box[2] - box[0]), dtype=bool)
            mask[points[:, 0] - box[1], points[:, 1] - box[0]] = True
            regions.append((box, mask))
    return regions


def component_boxes(alpha: np.ndarray, threshold: int = 16) -> list[tuple[int, int, int, int]]:
    return [box for box, _ in component_regions(alpha, threshold)]


def normalize(
    source: Path,
    output: Path,
    size: int = 2048,
    margin_ratio: float = 0.02,
    target_median_height: int = 456,
) -> None:
    image = Image.open(source).convert("RGBA")
    alpha = image.getchannel("A")
    box = alpha.getbbox()
    if box is None:
        raise ValueError("input contains no visible pixels")
    image = image.crop(box)
    margin = round(size * margin_ratio)
    available = size - margin * 2
    scale = min(available / image.width, available / image.height)
    resized = image.resize(
        (round(image.width * scale), round(image.height * scale)),
        Image.Resampling.LANCZOS,
    )
    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    canvas.alpha_composite(resized, ((size - resized.width) // 2, (size - resized.height) // 2))

    # Scale every separated view by one shared factor so the median silhouette
    # height matches the established characters on a 2048px comparison canvas.
    # Never shrink figures merely to create animation padding; cell fitting is
    # a later, direction-specific sprite-sheet operation.
    regions = component_regions(np.asarray(canvas.getchannel("A"), dtype=np.uint8))
    boxes = [box for box, _ in regions]
    if len(regions) != 8:
        raise ValueError(f"expected 8 separated figures before layout normalization, found {len(regions)}")
    median_height = statistics.median(box[3] - box[1] for box in boxes)
    figure_scale = target_median_height / median_height
    separated = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    occupied = np.zeros((size, size), dtype=bool)
    placed_boxes: list[tuple[int, int, int, int]] = []
    for box, region_mask in regions:
        figure_array = np.asarray(canvas.crop(box)).copy()
        figure_array[~region_mask] = 0
        figure = Image.fromarray(figure_array, mode="RGBA")
        scaled = figure.resize(
            (max(1, round(figure.width * figure_scale)), max(1, round(figure.height * figure_scale))),
            Image.Resampling.LANCZOS,
        )
        center_x = (box[0] + box[2]) / 2
        center_y = (box[1] + box[3]) / 2
        paste_x = round(center_x - scaled.width / 2)
        paste_y = round(center_y - scaled.height / 2)
        paste_x = min(max(paste_x, margin), size - margin - scaled.width)
        paste_y = min(max(paste_y, margin), size - margin - scaled.height)
        local_mask = np.asarray(scaled.getchannel("A"), dtype=np.uint8) > 16
        occupied_region = occupied[paste_y:paste_y + scaled.height, paste_x:paste_x + scaled.width]
        if np.any(occupied_region & local_mask):
            raise ValueError("scaled direction silhouettes overlap; reduce target height or adjust ring layout")
        occupied_region |= local_mask
        separated.alpha_composite(scaled, (paste_x, paste_y))
        placed_boxes.append((paste_x, paste_y, paste_x + scaled.width, paste_y + scaled.height))
    canvas = separated

    alpha_array = np.asarray(canvas.getchannel("A"), dtype=np.uint8)
    count = len(placed_boxes)
    corners = [alpha_array[0, 0], alpha_array[0, -1], alpha_array[-1, 0], alpha_array[-1, -1]]
    if count != 8:
        raise ValueError(f"expected 8 separated figures, found {count}")
    if any(corners):
        raise ValueError(f"canvas corners are not transparent: {corners}")
    union_box = canvas.getchannel("A").getbbox()
    if union_box is None or min(union_box[0], union_box[1], size - union_box[2], size - union_box[3]) < margin:
        raise ValueError(f"silhouette violates the required {margin}px outer safety margin: {union_box}")

    output.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output, optimize=True)
    final_median_height = statistics.median(box[3] - box[1] for box in placed_boxes)
    if not 450 <= final_median_height <= 510:
        raise ValueError(f"median figure height {final_median_height}px is outside the required 450..510px range")
    print(
        f"normalized {source} -> {output}; size={canvas.size}; figures={count}; "
        f"median_height={final_median_height}px; bbox={canvas.getchannel('A').getbbox()}"
    )


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--size", type=int, default=2048)
    parser.add_argument("--margin", type=float, default=0.02)
    parser.add_argument("--target-median-height", type=int, default=456)
    args = parser.parse_args()
    normalize(args.source, args.output, args.size, args.margin, args.target_median_height)


if __name__ == "__main__":
    main()
