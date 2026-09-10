# R2 Darkness Boundary V1 — generation log

Date: 2026-09-10  
Request: `docs/codex-art-request-r2-darkness-boundary.md`  
Mode: built-in `image_gen` plus deterministic Pillow/NumPy post-process  
Use cases: `precise-object-edit`, `background-extraction`

## Input roles

- `approved/albedo/tr01_reference_wall_top_rim_a_albedo.png`: direct rim geometry, palette, and lighting reference.
- `approved/albedo/tr01_reference_wall_top_a_albedo.png`: cap material reference.
- `approved/albedo/tr01_reference_wall_front_{a,b,c}_albedo.png`: crack scale, palette, and overlay readability references.
- `concept/tr01_reference_asset_calibration_board_v1.png`: highest-level visual reference.
- Generated crack stage 1/2: continuity anchors for the following damage stage.

## Final prompt set

### Rim B

> Derive a new seamless wall-top rim variant from approved rim A while preserving its exact one-cell geometry, stone scale, cool purple-gray palette, average value, and upper-left 35-degree lighting. Change only the placement of restrained hairline cracks and tiny desaturated violet moss traces. Keep every outer rim intact. No new structures, crystals, text, UI, logo, watermark, cast shadow, or scene.

### Rim C

> Derive a second seamless wall-top rim variant from approved rim A. Preserve geometry, palette, scale, average value, and upper-left 35-degree lighting. Add one subtly chipped section along the bright upper rim and a different sparse crack pattern; keep damage modest. Do not rotate or mirror A. No unrelated objects or text.

### Crack 1

> Create only first-hit crack marks for use over any approved wall-front tile: two or three thin branching stone cracks localized near the center-lower area, with dark-violet cores and tiny pale upper-left lips. Genuinely transparent square background with clear padding. Crack pixels only; no wall texture, panel, checkerboard, text, UI, logo, watermark, cast shadow, rubble pile, crystals, or border.

### Crack 2

> Advance the exact stage-1 crack network by extending the same branches, adding secondary forks, slightly widening the central split, and opening two or three tiny dark gaps. Keep stage 1 recognizable. Preserve transparent background, placement, palette, pixel style, padding, and fixed upper-left lighting. Damage pixels only.

### Crack 3

> Advance the exact stage-2 network to the final pre-break stage. Keep all branches, widen only major central splits, add several dark open fissures, a few detached stone chips, and restrained tiny fragments near the lower center. Clearly more damaged than stage 2 without becoming a rubble pile. Transparent background and damage pixels only.

## Deterministic production

`tools/art/finalize-r2-darkness-boundary-v1.py` performs all final delivery work:

- blends generated rim details back into approved rim A, matches mean luminance, quantizes to the approved palette, and locks the east/west 4px seam;
- removes generated checkerboard pixels from crack sources, creates true RGBA transparency, enforces 8px clear padding, and composites each previous crack stage into the next;
- creates the four shadow masks numerically as pure black RGB with alpha 215 and a 40px south/east fade;
- writes working, approved, Unity, QA, metadata, and `.meta` outputs.

Status: `approved`
