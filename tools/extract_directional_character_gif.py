from __future__ import annotations

import argparse
import json
import math
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image


DEFAULT_CROP = (28, 276, 220, 468)
DEFAULT_SEED = (96, 96)


def flood_from_border(candidate: np.ndarray) -> np.ndarray:
    """Return candidate pixels connected to the crop border (8-connectivity)."""
    height, width = candidate.shape
    seen = np.zeros_like(candidate, dtype=bool)
    queue: deque[tuple[int, int]] = deque()

    for x in range(width):
        for y in (0, height - 1):
            if candidate[y, x] and not seen[y, x]:
                seen[y, x] = True
                queue.append((y, x))
    for y in range(height):
        for x in (0, width - 1):
            if candidate[y, x] and not seen[y, x]:
                seen[y, x] = True
                queue.append((y, x))

    while queue:
        y, x = queue.popleft()
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                if dx == 0 and dy == 0:
                    continue
                ny, nx = y + dy, x + dx
                if (
                    0 <= ny < height
                    and 0 <= nx < width
                    and candidate[ny, nx]
                    and not seen[ny, nx]
                ):
                    seen[ny, nx] = True
                    queue.append((ny, nx))
    return seen


def label_components(mask: np.ndarray) -> tuple[np.ndarray, list[dict[str, float | int]]]:
    """Label 8-connected foreground components and collect their geometry."""
    height, width = mask.shape
    labels = np.zeros((height, width), dtype=np.int32)
    components: list[dict[str, float | int]] = []
    next_label = 0

    for start_y, start_x in zip(*np.nonzero(mask)):
        if labels[start_y, start_x] != 0:
            continue
        next_label += 1
        queue: deque[tuple[int, int]] = deque([(int(start_y), int(start_x))])
        labels[start_y, start_x] = next_label
        points: list[tuple[int, int]] = []

        while queue:
            y, x = queue.popleft()
            points.append((y, x))
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    if dx == 0 and dy == 0:
                        continue
                    ny, nx = y + dy, x + dx
                    if (
                        0 <= ny < height
                        and 0 <= nx < width
                        and mask[ny, nx]
                        and labels[ny, nx] == 0
                    ):
                        labels[ny, nx] = next_label
                        queue.append((ny, nx))

        ys = np.fromiter((p[0] for p in points), dtype=np.int32)
        xs = np.fromiter((p[1] for p in points), dtype=np.int32)
        components.append(
            {
                "label": next_label,
                "area": len(points),
                "cx": float(xs.mean()),
                "cy": float(ys.mean()),
                "left": int(xs.min()),
                "top": int(ys.min()),
                "right": int(xs.max()) + 1,
                "bottom": int(ys.max()) + 1,
            }
        )
    return labels, components


def dilate(mask: np.ndarray, radius: int) -> np.ndarray:
    """Small square dilation without requiring an image-processing dependency."""
    result = np.zeros_like(mask, dtype=bool)
    height, width = mask.shape
    for dy in range(-radius, radius + 1):
        for dx in range(-radius, radius + 1):
            y0, y1 = max(0, dy), min(height, height + dy)
            x0, x1 = max(0, dx), min(width, width + dx)
            result[y0:y1, x0:x1] |= mask[y0 - dy : y1 - dy, x0 - dx : x1 - dx]
    return result


def remove_external_neutral_specks(rgb: np.ndarray, keep: np.ndarray) -> tuple[np.ndarray, int]:
    """Remove isolated pale matte specks while preserving enclosed highlights."""
    rgb16 = rgb.astype(np.int16)
    mean = rgb16.mean(axis=2)
    spread = rgb16.max(axis=2) - rgb16.min(axis=2)
    pale = keep & (mean >= 130) & (spread <= 40)
    labels, components = label_components(pale)
    core = keep & ~pale
    cleaned = keep.copy()
    removed = 0

    for component in components:
        component_mask = labels == int(component["label"])
        area = int(component["area"])
        ys, xs = np.nonzero(component_mask)
        y0, y1 = int(ys.min()), int(ys.max()) + 1
        x0, x1 = int(xs.min()), int(xs.max()) + 1
        cardinal_contacts = 0
        if y0 > 0 and bool(np.any(core[y0 - 1, x0:x1])):
            cardinal_contacts += 1
        if y1 < core.shape[0] and bool(np.any(core[y1, x0:x1])):
            cardinal_contacts += 1
        if x0 > 0 and bool(np.any(core[y0:y1, x0 - 1])):
            cardinal_contacts += 1
        if x1 < core.shape[1] and bool(np.any(core[y0:y1, x1])):
            cardinal_contacts += 1
        component_mean = float(mean[component_mask].mean())
        # Matte specks are small, neutral, mid-gray components that touch the
        # character core from only one side. Enclosed highlights have dark or
        # colored core on multiple sides and are retained.
        if area <= 64 and component_mean < 195 and cardinal_contacts < 3:
            cleaned[component_mask] = False
            removed += area
    return cleaned, removed


def isolate_main_character(
    rgb: np.ndarray,
    seed: tuple[int, int],
    ownership: tuple[int, int, int, int] | None = None,
) -> tuple[np.ndarray, dict]:
    """Remove checkerboard and retain only the component nearest the target seed."""
    rgb16 = rgb.astype(np.int16)
    maxc = rgb16.max(axis=2)
    minc = rgb16.min(axis=2)
    mean = rgb16.mean(axis=2)
    spread = maxc - minc

    # The source checkerboard is pale and nearly neutral. The broader threshold
    # also absorbs its antialiased/compression fringe, but only when connected
    # to the crop border, protecting enclosed helmet/goggle highlights.
    pale_neutral = (mean >= 172) & (spread <= 64)
    background = flood_from_border(pale_neutral)
    foreground = ~background

    ownership_excluded_pixels = 0
    if ownership is not None:
        left, top, right, bottom = ownership
        owned = np.zeros_like(foreground, dtype=bool)
        owned[max(0, top) : min(foreground.shape[0], bottom), max(0, left) : min(foreground.shape[1], right)] = True
        ownership_excluded_pixels = int(np.count_nonzero(foreground & ~owned))
        foreground &= owned

    labels, components = label_components(foreground)
    if not components:
        raise RuntimeError("No foreground component was found in the selected crop.")

    seed_x, seed_y = seed
    viable = [component for component in components if int(component["area"]) >= 64]
    if not viable:
        viable = components

    def component_score(component: dict[str, float | int]) -> tuple[float, int]:
        distance_sq = (float(component["cx"]) - seed_x) ** 2 + (
            float(component["cy"]) - seed_y
        ) ** 2
        return (distance_sq, -int(component["area"]))

    selected = min(viable, key=component_score)
    keep = labels == int(selected["label"])

    diagnostics = {
        "component_count_before_filter": len(components),
        "selected_component": selected,
        "discarded_component_areas": [
            int(component["area"])
            for component in components
            if int(component["label"]) != int(selected["label"])
        ],
        "ownership_excluded_foreground_pixels": ownership_excluded_pixels,
    }
    return keep, diagnostics


def exact_palette_frame(rgb: np.ndarray, alpha_mask: np.ndarray) -> tuple[Image.Image, int]:
    """Create a GIF P frame with index 0 reserved for transparency."""
    opaque_colors = np.unique(rgb[alpha_mask].reshape(-1, 3), axis=0)
    height, width = alpha_mask.shape

    if len(opaque_colors) <= 255:
        indices = np.zeros((height, width), dtype=np.uint8)
        color_to_index = {
            tuple(int(channel) for channel in color): index + 1
            for index, color in enumerate(opaque_colors)
        }
        ys, xs = np.nonzero(alpha_mask)
        for y, x in zip(ys, xs):
            indices[y, x] = color_to_index[tuple(int(channel) for channel in rgb[y, x])]
        palette = [0, 0, 0] + opaque_colors.astype(np.uint8).reshape(-1).tolist()
        palette.extend([0] * (768 - len(palette)))
        frame = Image.fromarray(indices, "P")
        frame.putpalette(palette)
        frame.info["transparency"] = 0
        frame.info["disposal"] = 2
        return frame, len(opaque_colors)

    # This branch is not expected for the current GIF, but keeps the tool safe
    # for later inputs with more than 255 retained colors.
    rgba = np.dstack((rgb, np.where(alpha_mask, 255, 0).astype(np.uint8)))
    quantized = Image.fromarray(rgba, "RGBA").convert("RGB").quantize(
        colors=255,
        method=Image.Quantize.MEDIANCUT,
        dither=Image.Dither.NONE,
    )
    quantized_indices = np.asarray(quantized, dtype=np.uint16)
    shifted = np.where(alpha_mask, quantized_indices + 1, 0).astype(np.uint8)
    palette = [0, 0, 0] + (quantized.getpalette() or [])[: 255 * 3]
    palette.extend([0] * (768 - len(palette)))
    frame = Image.fromarray(shifted, "P")
    frame.putpalette(palette)
    frame.info["transparency"] = 0
    frame.info["disposal"] = 2
    return frame, len(opaque_colors)


def crop_with_background_fill(
    image: Image.Image,
    box: tuple[int, int, int, int],
    fill: tuple[int, int, int] = (248, 247, 248),
) -> Image.Image:
    """Crop a fixed cell, padding out-of-source areas with removable background."""
    left, top, right, bottom = box
    width, height = right - left, bottom - top
    if width <= 0 or height <= 0:
        raise ValueError(f"Invalid crop box: {box}")

    result = Image.new("RGB", (width, height), fill)
    source_left = max(0, left)
    source_top = max(0, top)
    source_right = min(image.width, right)
    source_bottom = min(image.height, bottom)
    if source_left < source_right and source_top < source_bottom:
        region = image.crop((source_left, source_top, source_right, source_bottom))
        result.paste(region, (source_left - left, source_top - top))
    return result


def parse_quad(value: str) -> tuple[int, int, int, int]:
    parts = tuple(int(part.strip()) for part in value.split(","))
    if len(parts) != 4:
        raise argparse.ArgumentTypeError("Expected left,top,right,bottom")
    return parts


def parse_pair(value: str) -> tuple[int, int]:
    parts = tuple(int(part.strip()) for part in value.split(","))
    if len(parts) != 2:
        raise argparse.ArgumentTypeError("Expected x,y")
    return parts


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Extract one fixed-pivot character cell from an 8-direction GIF."
    )
    parser.add_argument("--input", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--report", type=Path)
    parser.add_argument("--frames-dir", type=Path)
    parser.add_argument("--sheet", type=Path)
    parser.add_argument("--direction", default="unknown")
    parser.add_argument("--crop", type=parse_quad, default=DEFAULT_CROP)
    parser.add_argument("--seed", type=parse_pair, default=DEFAULT_SEED)
    parser.add_argument("--pivot", type=parse_pair, default=(96, 150))
    parser.add_argument("--ownership", type=parse_quad)
    parser.add_argument("--sheet-columns", type=int, default=8)
    parser.add_argument("--frame-start", type=int, default=0)
    parser.add_argument("--frame-end", type=int)
    args = parser.parse_args()

    source = Image.open(args.input)
    source_total_frame_count = getattr(source, "n_frames", 1)
    frame_start = args.frame_start
    frame_end = source_total_frame_count - 1 if args.frame_end is None else args.frame_end
    if not (0 <= frame_start <= frame_end < source_total_frame_count):
        raise ValueError(
            f"Frame range must be within 0..{source_total_frame_count - 1}: "
            f"{frame_start}..{frame_end}"
        )
    source_frame_indices = list(range(frame_start, frame_end + 1))
    frame_count = len(source_frame_indices)
    gif_frames: list[Image.Image] = []
    expected_rgba: list[np.ndarray] = []
    durations: list[int] = []
    frame_reports: list[dict] = []

    if args.frames_dir:
        args.frames_dir.mkdir(parents=True, exist_ok=True)

    for output_frame_index, source_frame_index in enumerate(source_frame_indices):
        source.seek(source_frame_index)
        crop = crop_with_background_fill(source.convert("RGB"), args.crop)
        rgb = np.asarray(crop, dtype=np.uint8)
        keep, diagnostics = isolate_main_character(rgb, args.seed, args.ownership)
        keep, removed_specks = remove_external_neutral_specks(rgb, keep)
        diagnostics["removed_external_neutral_speck_pixels"] = removed_specks
        gif_frame, source_color_count = exact_palette_frame(rgb, keep)
        rgba = np.dstack((rgb, np.where(keep, 255, 0).astype(np.uint8)))
        rgba[~keep, :3] = 0

        gif_frames.append(gif_frame)
        expected_rgba.append(rgba)
        if args.frames_dir:
            Image.fromarray(rgba, "RGBA").save(
                args.frames_dir / f"frame_{output_frame_index:03d}.png",
                "PNG",
                optimize=False,
            )
        duration = int(source.info.get("duration", 100))
        durations.append(duration)

        ys, xs = np.nonzero(keep)
        bbox = [int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1]
        touches_border = bool(
            keep[0, :].any()
            or keep[-1, :].any()
            or keep[:, 0].any()
            or keep[:, -1].any()
        )
        frame_reports.append(
            {
                "output_frame": output_frame_index,
                "source_frame": source_frame_index,
                "duration_ms": duration,
                "alpha_bbox": bbox,
                "opaque_pixels": int(keep.sum()),
                "source_opaque_colors": source_color_count,
                "touches_canvas_border": touches_border,
                **diagnostics,
            }
        )

    args.output.parent.mkdir(parents=True, exist_ok=True)
    gif_frames[0].save(
        args.output,
        "GIF",
        save_all=True,
        append_images=gif_frames[1:],
        duration=durations,
        loop=int(source.info.get("loop", 0)),
        disposal=2,
        transparency=0,
        optimize=False,
        include_color_table=True,
    )

    sheet_pixel_channel_mismatches: int | None = None
    if args.sheet:
        if args.sheet_columns <= 0:
            raise ValueError("--sheet-columns must be positive")
        cell_width, cell_height = gif_frames[0].size
        sheet_rows = math.ceil(frame_count / args.sheet_columns)
        sheet = Image.new(
            "RGBA",
            (cell_width * args.sheet_columns, cell_height * sheet_rows),
            (0, 0, 0, 0),
        )
        for frame_index, rgba in enumerate(expected_rgba):
            column = frame_index % args.sheet_columns
            row = frame_index // args.sheet_columns
            sheet.alpha_composite(
                Image.fromarray(rgba, "RGBA"),
                (column * cell_width, row * cell_height),
            )
        args.sheet.parent.mkdir(parents=True, exist_ok=True)
        sheet.save(args.sheet, "PNG", optimize=False)

        encoded_sheet = Image.open(args.sheet).convert("RGBA")
        sheet_pixel_channel_mismatches = 0
        for frame_index, expected in enumerate(expected_rgba):
            column = frame_index % args.sheet_columns
            row = frame_index // args.sheet_columns
            actual = np.asarray(
                encoded_sheet.crop(
                    (
                        column * cell_width,
                        row * cell_height,
                        (column + 1) * cell_width,
                        (row + 1) * cell_height,
                    )
                ),
                dtype=np.uint8,
            )
            sheet_pixel_channel_mismatches += int(np.count_nonzero(actual != expected))

    # Reopen the artifact: validation must test the encoded file, not only the
    # in-memory masks used before saving.
    encoded = Image.open(args.output)
    encoded_frame_count = getattr(encoded, "n_frames", 1)
    alpha_mismatch_pixels = 0
    opaque_rgb_mismatch_pixels = 0
    encoded_durations: list[int] = []
    for frame_index, expected in enumerate(expected_rgba):
        encoded.seek(frame_index)
        actual = np.asarray(encoded.convert("RGBA"), dtype=np.uint8)
        expected_alpha = expected[:, :, 3] > 0
        actual_alpha = actual[:, :, 3] > 0
        alpha_mismatch_pixels += int(np.count_nonzero(expected_alpha != actual_alpha))
        shared_opaque = expected_alpha & actual_alpha
        opaque_rgb_mismatch_pixels += int(
            np.count_nonzero(np.any(actual[:, :, :3] != expected[:, :, :3], axis=2) & shared_opaque)
        )
        encoded_durations.append(int(encoded.info.get("duration", 0)))

    report = {
        "input": str(args.input.resolve()),
        "output": str(args.output.resolve()),
        "direction": args.direction,
        "source_size": list(source.size),
        "crop_box": list(args.crop),
        "output_size": list(gif_frames[0].size),
        "seed_in_crop": list(args.seed),
        "pivot_in_cell": list(args.pivot),
        "ownership_box_in_cell": list(args.ownership) if args.ownership else None,
        "pivot_policy": "fixed crop; no per-frame trim or recenter",
        "frames_directory": str(args.frames_dir.resolve()) if args.frames_dir else None,
        "sprite_sheet": str(args.sheet.resolve()) if args.sheet else None,
        "sheet_columns": args.sheet_columns if args.sheet else None,
        "sheet_rows": math.ceil(frame_count / args.sheet_columns) if args.sheet else None,
        "sheet_pixel_channel_mismatches_after_encode": sheet_pixel_channel_mismatches,
        "source_total_frame_count": source_total_frame_count,
        "selected_source_frames": source_frame_indices,
        "output_frame_count": frame_count,
        "encoded_frame_count": encoded_frame_count,
        "source_loop": int(source.info.get("loop", 0)),
        "durations_ms": encoded_durations,
        "alpha_mismatch_pixels_after_encode": alpha_mismatch_pixels,
        "opaque_rgb_mismatch_pixels_after_encode": opaque_rgb_mismatch_pixels,
        "all_frames_clear_of_canvas_border": not any(
            frame["touches_canvas_border"] for frame in frame_reports
        ),
        "frames": frame_reports,
    }

    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")

    print(json.dumps({key: value for key, value in report.items() if key != "frames"}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
