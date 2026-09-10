# Reference Wall Volume V1 — generation log

Date: 2026-09-10  
Request: `docs/codex-art-request-wall-volume.md`  
Mode: built-in `image_gen`  
Use case: `stylized-concept` (west/east/corners/rim), `precise-object-edit` (west correction)

## Shared input roles

- `concept/tr01_reference_asset_calibration_board_v1.png`: highest-level style and palette reference.
- `approved/albedo/tr01_reference_wall_front_a_albedo.png`: front masonry, reinforcement, and contact-band reference.
- `approved/albedo/tr01_reference_wall_top_a_albedo.png`: cap material and scale reference.
- Generated west draft: edit target only for the correction pass.
- Normalized east draft: geometry guidance only for the corrected west side.

## Selected outputs

| Asset | Generated source | Decision / cleanup |
|---|---|---|
| west side A | `tr01_reference_wall_side_west_a_source.png` | selected after one correction pass; downsample, approved palette quantization, west-light grade, 4px EW seam lock |
| east side A | `tr01_reference_wall_side_east_a_source.png` | selected; downsample, approved palette quantization, darker east-plane grade, 4px EW seam lock |
| outer corner A | `tr01_reference_wall_outer_corner_a_source.png` | selected; downsample and approved palette quantization |
| inner corner A | `tr01_reference_wall_inner_corner_a_source.png` | selected; downsample and approved palette quantization |
| top rim A | `tr01_reference_wall_top_rim_a_source.png` | selected; blended with approved cap A, directional rim grade, approved palette quantization, 4px EW seam lock |

`tr01_reference_wall_side_west_a_rejected_source.png` was rejected because the exposed west plane did not read clearly enough at game scale.

## Final prompt set

### West side correction

> Correct the selected draft so it reads unmistakably as the WEST-facing exposed 45-degree side plane of a one-cell wall, not as another straight front wall. Use the east-side draft only as geometric guidance for how wall thickness reads, while drawing the opposite west orientation with its own fixed upper-left 35-degree lighting. Preserve the crisp purple-gray pixel-art palette, masonry scale, reinforcement language, and quiet average value. One square full-bleed tile; no text, UI, characters, cast shadow, scene, frame, logo, or watermark; do not simply mirror the east side.

### East side

> Create the EAST-facing exposed side of a one-cell mine wall, reinterpreting the approved front masonry as a visibly angled 45-degree top-down side plane. It must be a distinct east-facing drawing, not a simple mirror. Include the common upper rim, stone/reinforcement rhythm, lower contact band, and readable thickness. Use crisp clustered pixel art, cool purple-gray masonry, deep violet crevices, sparse magenta flecks, and an east/south plane darker under fixed upper-left 35-degree light. One seamless horizontal tile; no text, UI, characters, cast shadow, scene, frame, logo, or watermark.

### Outer corner

> Create a convex outer corner tile where two exposed wall faces meet around a solid wall cell, with a readable L-shaped angled boundary and wall thickness. Combine cap, rim, vertical face, and contact band in one coherent square tile. Match the approved purple-gray pixel art and fixed upper-left 35-degree light; north/west bevels brighter and south/east recesses darker. Unique direction-aware drawing, not a rotated copy; no unrelated elements or text.

### Inner corner

> Create a concave inner corner tile where two wall runs meet around a carved floor recess, with a readable inward L-shaped notch and contact depth. Combine cap, rim, vertical face, and contact band. Match the approved purple-gray pixel art and fixed upper-left 35-degree light; upper-left rims brighter and the inward southeast recess darkest. Unique direction-aware drawing, not a rotated copy; no unrelated elements or text.

### Top rim

> Create one directional wall-top rim tile that makes the cap read as a raised solid wall under fixed upper-left 35-degree lighting. The approved cap defines material and palette. Keep a clear bright north/west rim, restrained darker south/east bevel, and quiet stone center. Crisp clustered pixel art, square full-bleed tile, no strong cast light, text, UI, characters, props, scene, frame, logo, or watermark.

## Deterministic post-process

`tools/art/finalize-reference-wall-volume-v1.py` produces the 128×128 working, approved, and Unity copies, locks requested seams, generates the 6×6 QA sheet, and records numeric QA. Contact AO E/S/W is rotated from the approved north AO without generative processing so alpha maximum 150, fade shape, and total opacity remain identical.

Status: `approved`
