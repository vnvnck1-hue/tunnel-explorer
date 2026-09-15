"""모듈러 거너 Gungeon 비주얼 2차 배치 — 콘셉트 시트를 16 PPU 게임 자산으로 가공한다.

입력: art-production/test-room-v01/concept/modular-gunner-gungeon/
  mg-gunner-directions / mg-wall-mass / mg-vfx-sheets / mg-room-props / mg-enemy-language
출력: 같은 계약의 source/ 단계.

1차(condition_concept_art.py)와 같은 원칙이다. 그림을 새로 그리지 않고
배경 키잉 · 셀 분리 · 박스 다운샘플 · 알파 이진화 · 프레임 박스와 피벗 정렬만 한다.
"""
import os
from collections import deque

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
CONCEPT = os.path.join(ROOT, "art-production", "test-room-v01", "concept", "modular-gunner-gungeon")
OUT = HERE


def load(name):
    return np.array(Image.open(os.path.join(CONCEPT, name)).convert("RGBA"))


def save(rgba, name):
    Image.fromarray(rgba, "RGBA").save(os.path.join(OUT, name))
    return name


def box_resize(rgba, w, h, alpha_cut=110):
    a = rgba.astype(np.float32)
    al = a[..., 3:4] / 255.0
    pm = np.concatenate([a[..., :3] * al, a[..., 3:4]], axis=2)
    small = np.array(Image.fromarray(pm.astype(np.uint8), "RGBA").resize((w, h), Image.BOX)).astype(np.float32)
    outa = small[..., 3:4]
    rgb = np.clip(np.where(outa > 0, small[..., :3] / np.maximum(outa / 255.0, 1e-4), 0.0), 0, 255)
    hard = np.where(outa >= alpha_cut, 255.0, 0.0)
    return np.concatenate([rgb, hard], axis=2).astype(np.uint8)


def content_box(rgba, thr=40):
    ys, xs = np.where(rgba[..., 3] > thr)
    return int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())


def contact_sheet(files, name, zoom=6, pad=2, bg=(26, 26, 36, 255)):
    ims = [Image.open(os.path.join(OUT, f)) for f in files]
    w = sum(i.width * zoom + pad for i in ims) + pad
    h = max(i.height for i in ims) * zoom + pad * 2
    sheet = Image.new("RGBA", (w, h), bg)
    x = pad
    for i in ims:
        sheet.alpha_composite(i.resize((i.width * zoom, i.height * zoom), Image.NEAREST), (x, pad))
        x += i.width * zoom + pad
    sheet.save(os.path.join(OUT, name))


# ------------------------------------------------------------------ 배경 키잉
def _border_band(rgba, ring=20):
    rgb = rgba[..., :3].astype(np.int16)
    return np.concatenate([
        rgb[:ring].reshape(-1, 3), rgb[-ring:].reshape(-1, 3),
        rgb[:, :ring].reshape(-1, 3), rgb[:, -ring:].reshape(-1, 3),
    ])


def key_checkerboard(rgba, chroma=12, margin=8):
    """ImageGen 이 그림에 박아 온 체커보드 배경을 지운다.

    체커는 디더링이 섞여 있어 두 색만 찍어서는 다 걸리지 않는다. 테두리 띠에서
    무채색 픽셀의 밝기 범위를 실측해 그 구간만 후보로 삼고, 다시 테두리에서
    플러드 필로 '바깥쪽에 이어진' 픽셀만 지운다. 그래서 스파크 중심의 흰색이나
    적의 흰 눈처럼 안쪽에 고립된 같은 색은 살아남는다.
    """
    a = rgba.copy()
    rgb = a[..., :3].astype(np.int16)
    achromatic = (rgb.max(2) - rgb.min(2)) <= chroma

    band = _border_band(a)
    band_flat = band[(band.max(1) - band.min(1)) <= chroma]
    if len(band_flat) == 0:
        return a
    value = band_flat.mean(1)
    lo = float(np.percentile(value, 1)) - margin
    hi = float(np.percentile(value, 99)) + margin

    brightness = rgb.mean(2)
    candidate = achromatic & (brightness >= lo) & (brightness <= hi)

    h, w = candidate.shape
    visited = np.zeros((h, w), bool)
    queue = deque()

    def push(y, x):
        if candidate[y, x] and not visited[y, x]:
            visited[y, x] = True
            queue.append((y, x))

    for x in range(w):
        push(0, x)
        push(h - 1, x)
    for y in range(h):
        push(y, 0)
        push(y, w - 1)

    while queue:
        y, x = queue.popleft()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w:
                push(ny, nx)

    a[..., 3][visited] = 0
    return a


def segments(mask, min_run, max_gap):
    idx = np.where(mask)[0]
    if len(idx) == 0:
        return []
    out = []
    s = last = idx[0]
    for v in idx[1:]:
        if v - last > max_gap:
            if last - s >= min_run:
                out.append((int(s), int(last)))
            s = v
        last = v
    if last - s >= min_run:
        out.append((int(s), int(last)))
    return out


def rows_of(rgba, min_run=12, max_gap=24, thr=40):
    return segments((rgba[..., 3] > thr).sum(axis=1) > 1, min_run, max_gap)


def cols_of(band, min_run=12, max_gap=24, thr=40):
    return segments((band[..., 3] > thr).sum(axis=0) > 1, min_run, max_gap)


def split_columns(band, count, thr=15):
    """개수를 아는 행을 가장 넓은 빈 구간 (count-1) 개에서 자른다.

    소품 행은 물체마다 폭과 간격이 달라 고정 임계로는 램프 브래킷 같은 부품이
    따로 떨어져 나간다. 얇은 세로 조각을 임계로 걸러 낸 뒤 가장 큰 빈 구간만 쓴다.
    """
    mask = (band[..., 3] > 40).sum(axis=0) > thr
    idx = np.where(mask)[0]
    if len(idx) == 0:
        return []
    gaps = []
    prev = idx[0]
    for v in idx[1:]:
        if v - prev > 1:
            gaps.append((int(prev), int(v), int(v - prev)))
        prev = v
    cuts = sorted(g[0] + (g[1] - g[0]) // 2 for g in sorted(gaps, key=lambda g: -g[2])[:count - 1])
    bounds = [int(idx[0])] + cuts + [int(idx[-1])]
    return [(bounds[i], bounds[i + 1]) for i in range(len(bounds) - 1)]


def cells(rgba, row, min_run=12, max_gap=24):
    band = rgba[row[0]:row[1] + 1]
    return [(band, c) for c in cols_of(band, min_run, max_gap)]


# ------------------------------------------------------- 방향별 몸통·머리
GUNNER = "mg-gunner-directions-concept-v01.png"
DIRECTIONS = ["s", "se", "e", "ne", "n"]
HEAD_W, HEAD_H = 18, 17
BODY_W, BODY_H = 20, 22


def build_gunner_directions():
    src = load(GUNNER)
    rs = rows_of(src, min_run=40, max_gap=40)
    if len(rs) != 2:
        raise SystemExit(f"expected 2 rows in gunner sheet, got {rs}")

    made = []
    for row_index, (kind, fw, fh, pivot_note) in enumerate(
            [("head", HEAD_W, HEAD_H, "neck"), ("body", BODY_W, BODY_H, "feet")]):
        band = src[rs[row_index][0]:rs[row_index][1] + 1]
        cs = cols_of(band, min_run=30, max_gap=40)
        if len(cs) != 5:
            raise SystemExit(f"expected 5 {kind} cells, got {len(cs)}")

        # 모든 방향이 같은 박스·같은 접지선을 쓰도록 공통 스케일을 잡는다.
        boxes = []
        for x0, x1 in cs:
            cell = band[:, x0:x1 + 1]
            boxes.append(content_box(cell))
        unit = max(b[2] - b[0] + 1 for b in boxes) / float(fw - 2)

        frames = []
        for i, (x0, x1) in enumerate(cs):
            cell = band[:, x0:x1 + 1]
            bx0, by0, bx1, by1 = boxes[i]
            cw, ch = int(round(fw * unit)), int(round(fh * unit))
            canvas = np.zeros((ch, cw, 4), np.uint8)
            # 가로는 가운데, 세로는 바닥(머리는 목 끝)을 프레임 하단에 맞춘다.
            ox = int(round(cw / 2.0 - (bx0 + bx1 + 1) / 2.0))
            oy = int(round(ch - (by1 + 1) - unit))
            sx0, sy0 = max(0, -ox), max(0, -oy)
            dx0, dy0 = max(0, ox), max(0, oy)
            pw = min(cell.shape[1] - sx0, cw - dx0)
            ph = min(cell.shape[0] - sy0, ch - dy0)
            if pw > 0 and ph > 0:
                canvas[dy0:dy0 + ph, dx0:dx0 + pw] = cell[sy0:sy0 + ph, sx0:sx0 + pw]
            frames.append(box_resize(canvas, fw, fh, alpha_cut=96))

        sheet = np.concatenate(frames, axis=1)
        made.append(save(sheet, f"gunner_{kind}_dirs.png"))
    contact_sheet(made, "_preview_gunner_dirs.png", zoom=6)
    return made, (HEAD_W, HEAD_H, BODY_W, BODY_H, len(DIRECTIONS))


# --------------------------------------------------------- 벽 외곽 타일 13종
WALL_MASS = "mg-wall-mass-concept-v02.png"
WALL_GRID = 16          # 암반 덩어리를 가로 16칸으로 쪼개 한 칸을 16x16 타일로 본다.

# 필요한 타일과 그 8이웃 점유 조건.
# 값: (필수 점유 이웃, 필수 빈 이웃). 이웃 키는 n/s/e/w/ne/nw/se/sw.
WALL_PATTERNS = {
    "n":        ("sew", "n"),
    "s":        ("new", "s"),
    "w":        ("nse", "w"),
    "e":        ("nsw", "e"),
    "nw":       ("se", "nw"),
    "ne":       ("sw", "ne"),
    "sw":       ("ne", "sw"),
    "se":       ("nw", "se"),
    "inner_nw": ("nsew", "NW"),
    "inner_ne": ("nsew", "NE"),
    "inner_sw": ("nsew", "SW"),
    "inner_se": ("nsew", "SE"),
    "fill":     ("nsew", ""),
}
NEIGHBOUR_OFFSET = {
    "n": (0, -1), "s": (0, 1), "w": (-1, 0), "e": (1, 0),
    "NW": (-1, -1), "NE": (1, -1), "SW": (-1, 1), "SE": (1, 1),
}


def _expand(spec):
    """'nse' -> ['n','s','e'], 'NW' -> ['NW'] 처럼 대각 키를 살려서 푼다."""
    out, i = [], 0
    while i < len(spec):
        if spec[i].isupper():
            out.append(spec[i:i + 2])
            i += 2
        else:
            out.append(spec[i])
            i += 1
    return out


def build_wall_edges():
    """암반 덩어리 한 장에서 8이웃 점유 패턴으로 외곽·코너 타일을 자동으로 잘라 낸다.

    좌표를 손으로 찍으면 덩어리 모양이 조금만 달라져도 엉뚱한 곳이 잘린다.
    덩어리를 격자로 나눠 점유 맵을 만들고, 원하는 이웃 조건에 맞는 칸을 찾아 쓴다.
    """
    src = load(WALL_MASS)
    bx0, by0, bx1, by1 = content_box(src, 120)
    w, h = bx1 - bx0 + 1, by1 - by0 + 1
    cell = w / float(WALL_GRID)
    rows = int(round(h / cell))

    solid = src[..., 3] > 120
    occ = np.zeros((rows, WALL_GRID), bool)
    for gy in range(rows):
        for gx in range(WALL_GRID):
            x0 = bx0 + int(gx * cell)
            y0 = by0 + int(gy * cell)
            patch = solid[y0:y0 + int(cell), x0:x0 + int(cell)]
            occ[gy, gx] = patch.mean() > 0.55 if patch.size else False

    def neighbour(gy, gx, key):
        dx, dy = NEIGHBOUR_OFFSET[key]
        ny, nx = gy + dy, gx + dx
        if ny < 0 or ny >= rows or nx < 0 or nx >= WALL_GRID:
            return False
        return bool(occ[ny, nx])

    made = []
    for name, (need_solid, need_empty) in WALL_PATTERNS.items():
        best = None
        for gy in range(rows):
            for gx in range(WALL_GRID):
                if not occ[gy, gx]:
                    continue
                if any(not neighbour(gy, gx, k) for k in _expand(need_solid)):
                    continue
                if any(neighbour(gy, gx, k) for k in _expand(need_empty)):
                    continue
                # 덩어리 중심에서 먼 칸일수록 외곽선이 또렷하다.
                score = abs(gx - WALL_GRID / 2) + abs(gy - rows / 2)
                if best is None or score > best[0]:
                    best = (score, gy, gx)
        if best is None:
            print(f"  wall edge '{name}': 패턴에 맞는 칸을 찾지 못해 건너뜀")
            continue
        _, gy, gx = best
        x0 = bx0 + int(gx * cell)
        y0 = by0 + int(gy * cell)
        crop = src[y0:y0 + int(cell), x0:x0 + int(cell)]
        alpha_cut = 110 if name != "fill" else 0
        tile = box_resize(crop, 16, 16, alpha_cut=alpha_cut)
        if name == "fill":
            tile[..., 3] = 255
        made.append(save(tile, f"env_wall_edge_{name}.png"))

    contact_sheet(made, "_preview_wall_edges.png", zoom=8)
    return made


# ------------------------------------------------------------------- VFX 시트
VFX = "mg-vfx-sheets-concept-v01.png"
VFX_ROWS = [("shockwave", 32, 5), ("smoke", 32, 5), ("spark", 32, 5)]
BULLET_NAMES = ["player", "enemy", "pierce", "explosive"]


def build_vfx():
    src = key_checkerboard(load(VFX))
    rs = rows_of(src, min_run=30, max_gap=12)
    if len(rs) < 4:
        raise SystemExit(f"expected 4 vfx rows, got {rs}")

    made = []
    for row_index, (kind, size, count) in enumerate(VFX_ROWS):
        band = src[rs[row_index][0]:rs[row_index][1] + 1]
        cs = cols_of(band, min_run=20, max_gap=30)
        if len(cs) != count:
            raise SystemExit(f"{kind}: expected {count} cells, got {len(cs)}")
        frames = []
        for x0, x1 in cs:
            cell = band[:, x0:x1 + 1]
            bx0, by0, bx1, by1 = content_box(cell)
            cw, ch = bx1 - bx0 + 1, by1 - by0 + 1
            side = max(cw, ch)
            pad = np.zeros((side, side, 4), np.uint8)
            pad[(side - ch) // 2:(side - ch) // 2 + ch, (side - cw) // 2:(side - cw) // 2 + cw] = \
                cell[by0:by1 + 1, bx0:bx1 + 1]
            frames.append(box_resize(pad, size, size, alpha_cut=80))
        made.append(save(np.concatenate(frames, axis=1), f"vfx_{kind}.png"))

    # 4번째 행 — 탄종별 탄두. 오른쪽을 향한 채로 길이 방향만 맞춘다.
    band = src[rs[3][0]:rs[3][1] + 1]
    cs = cols_of(band, min_run=20, max_gap=30)
    heads = []
    for i, (x0, x1) in enumerate(cs[:len(BULLET_NAMES)]):
        cell = band[:, x0:x1 + 1]
        bx0, by0, bx1, by1 = content_box(cell)
        crop = cell[by0:by1 + 1, bx0:bx1 + 1]
        scale = 10.0 / max(1, crop.shape[0])
        bw = max(4, int(round(crop.shape[1] * scale)))
        heads.append(save(box_resize(crop, bw, 6, alpha_cut=80), f"bullet_{BULLET_NAMES[i]}.png"))
    made += heads

    contact_sheet(made[:3], "_preview_vfx_rows.png", zoom=4)
    contact_sheet(heads, "_preview_bullets.png", zoom=10)
    return made


# --------------------------------------------------------------------- 소품
PROPS = "mg-room-props-concept-v01.png"
PROP_NAMES = [
    ["door_closed", "door_open", "pillar", "lamp_wall", "lamp_floor"],
    ["crystal_magenta", "crystal_cyan", "ore_boulder", "support_beam", "rubble_pile"],
]
# 셀당 목표 폭(16 PPU 기준 픽셀). 기둥은 정확히 한 셀 폭이어야 한다.
PROP_WIDTHS = {
    "door_closed": 32, "door_open": 32, "pillar": 16, "lamp_wall": 12, "lamp_floor": 12,
    "crystal_magenta": 16, "crystal_cyan": 16, "ore_boulder": 20, "support_beam": 16, "rubble_pile": 22,
}


def build_props():
    src = key_checkerboard(load(PROPS))
    # 상단에 얇은 잔여 스트립이 남는 경우가 있어 충분히 긴 행만 소품 행으로 본다.
    rs = [r for r in rows_of(src, min_run=40, max_gap=12) if r[1] - r[0] >= 120]
    if len(rs) != 2:
        raise SystemExit(f"expected 2 prop rows, got {rs}")

    made = []
    for row_index, names in enumerate(PROP_NAMES):
        band = src[rs[row_index][0]:rs[row_index][1] + 1]
        cs = split_columns(band, len(names))
        if len(cs) != len(names):
            raise SystemExit(f"row {row_index}: expected {len(names)} props, got {len(cs)}")
        for i, (x0, x1) in enumerate(cs):
            cell = band[:, x0:x1 + 1]
            bx0, by0, bx1, by1 = content_box(cell)
            crop = cell[by0:by1 + 1, bx0:bx1 + 1]
            target_w = PROP_WIDTHS[names[i]]
            target_h = max(4, int(round(crop.shape[0] * target_w / crop.shape[1])))
            made.append(save(box_resize(crop, target_w, target_h, alpha_cut=96), f"prop_{names[i]}.png"))
    contact_sheet(made, "_preview_props.png", zoom=6)
    return made


# --------------------------------------------------------------- 적 시각 언어
ENEMY = "mg-enemy-language-concept-v01.png"
ENEMY_FRAME = 32
ENEMY_ROWS = [
    ("telegraph", ["charge", "ranged", "slam"]),
    ("stage", ["0", "1", "2", "3"]),
    ("elite", ["idle", "charge", "slam"]),
]


def build_enemy_language():
    src = key_checkerboard(load(ENEMY))
    rs = rows_of(src, min_run=40, max_gap=12)
    if len(rs) < 4:
        raise SystemExit(f"expected 4 enemy rows, got {rs}")

    # 체력 단계 0번(무손상)을 기준 크기로 잡아 모든 셀을 같은 축척에 둔다.
    stage_band = src[rs[1][0]:rs[1][1] + 1]
    stage_cols = cols_of(stage_band, min_run=30, max_gap=40)
    ref = stage_band[:, stage_cols[0][0]:stage_cols[0][1] + 1]
    rbx0, rby0, rbx1, rby1 = content_box(ref)
    unit = (rbx1 - rbx0 + 1) / 16.0

    made = []
    for row_index, (kind, names) in enumerate(ENEMY_ROWS):
        band = src[rs[row_index][0]:rs[row_index][1] + 1]
        cs = cols_of(band, min_run=30, max_gap=40)
        frames = []
        for x0, x1 in cs[:len(names)]:
            cell = band[:, x0:x1 + 1]
            bx0, by0, bx1, by1 = content_box(cell)
            cw = ch = int(round(ENEMY_FRAME * unit))
            canvas = np.zeros((ch, cw, 4), np.uint8)
            ox = int(round(cw / 2.0 - (bx0 + bx1 + 1) / 2.0))
            oy = int(round(ch - (by1 + 1) - 6 * unit))
            sx0, sy0 = max(0, -ox), max(0, -oy)
            dx0, dy0 = max(0, ox), max(0, oy)
            pw = min(cell.shape[1] - sx0, cw - dx0)
            ph = min(cell.shape[0] - sy0, ch - dy0)
            if pw > 0 and ph > 0:
                canvas[dy0:dy0 + ph, dx0:dx0 + pw] = cell[sy0:sy0 + ph, sx0:sx0 + pw]
            frames.append(box_resize(canvas, ENEMY_FRAME, ENEMY_FRAME, alpha_cut=96))
        made.append(save(np.concatenate(frames, axis=1), f"enemy_{kind}.png"))

    # 4번째 행 — 재질별 파편.
    band = src[rs[3][0]:rs[3][1] + 1]
    cs = cols_of(band, min_run=6, max_gap=18)
    shards = []
    for i, (x0, x1) in enumerate(cs):
        cell = band[:, x0:x1 + 1]
        bx0, by0, bx1, by1 = content_box(cell)
        crop = cell[by0:by1 + 1, bx0:bx1 + 1]
        w = max(2, int(round(crop.shape[1] / unit)))
        h = max(2, int(round(crop.shape[0] / unit)))
        shards.append(save(box_resize(crop, w, h, alpha_cut=110), f"enemy_shard_{i:02d}.png"))
    made += shards

    contact_sheet(made[:3], "_preview_enemy_language.png", zoom=4)
    if shards:
        contact_sheet(shards, "_preview_enemy_shards.png", zoom=8)
    return made


# ------------------------------------------------------------------ HUD 아이콘
HUD = "mg-hud-icons-concept-v01.png"
HUD_ROWS = [
    ("ammo", ["standard", "rapid", "pierce", "explosive"]),
    ("marker", ["danger", "elite", "reload", "low_ammo", "kill"]),
]
HUD_SIZE = 12          # 12px 높이에서 읽히게 그려 달라고 요청한 크기다.


def build_hud_icons():
    src = key_checkerboard(load(HUD))
    rs = rows_of(src, min_run=30, max_gap=20)
    if len(rs) != 2:
        raise SystemExit(f"expected 2 hud rows, got {rs}")

    made = []
    for row_index, (kind, names) in enumerate(HUD_ROWS):
        band = src[rs[row_index][0]:rs[row_index][1] + 1]
        cs = split_columns(band, len(names), thr=4)
        if len(cs) != len(names):
            raise SystemExit(f"hud row {row_index}: expected {len(names)}, got {len(cs)}")
        for i, (x0, x1) in enumerate(cs):
            cell = band[:, x0:x1 + 1]
            bx0, by0, bx1, by1 = content_box(cell)
            crop = cell[by0:by1 + 1, bx0:bx1 + 1]
            side = max(crop.shape[0], crop.shape[1])
            pad = np.zeros((side, side, 4), np.uint8)
            pad[(side - crop.shape[0]) // 2:(side - crop.shape[0]) // 2 + crop.shape[0],
                (side - crop.shape[1]) // 2:(side - crop.shape[1]) // 2 + crop.shape[1]] = crop
            made.append(save(box_resize(pad, HUD_SIZE, HUD_SIZE, alpha_cut=90), f"hud_{kind}_{names[i]}.png"))
    contact_sheet(made, "_preview_hud.png", zoom=10)
    return made


if __name__ == "__main__":
    gunner, gmeta = build_gunner_directions()
    print("gunner dirs : %s head=%dx%d body=%dx%d x%d" % (gunner, gmeta[0], gmeta[1], gmeta[2], gmeta[3], gmeta[4]))
    print("wall edges  : %d" % len(build_wall_edges()))
    print("vfx         : %d" % len(build_vfx()))
    print("props       : %d" % len(build_props()))
    print("enemy lang  : %d" % len(build_enemy_language()))
    print("hud icons   : %d" % len(build_hud_icons()))
