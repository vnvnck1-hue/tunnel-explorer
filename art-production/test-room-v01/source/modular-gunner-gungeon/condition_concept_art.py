"""모듈러 거너 Gungeon 비주얼 배치 — 콘셉트 시트를 16 PPU 게임 자산으로 가공한다.

입력: art-production/test-room-v01/concept/modular-gunner-gungeon/*.png (Codex 제작)
출력: 같은 아트 계약의 source/ 단계. Unity 로 복사하는 일은 별도 단계에서 한다.

이 스크립트는 그림을 새로 그리지 않는다. 셀 분리, 다운샘플, 알파 임계,
프레임 박스·피벗 정렬만 수행한다.
"""
import os
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
CONCEPT = os.path.join(ROOT, "art-production", "test-room-v01", "concept", "modular-gunner-gungeon")
OUT = HERE

ENV = "mg-environment-modules-concept-v01.png"
GUN = "mg-gunner-recoil-frames-concept-v01.png"
ENEMY = "mg-target-hit-death-frames-concept-v01.png"
DECAL = "mg-impact-decals-concept-v01.png"


def load(name):
    return np.array(Image.open(os.path.join(CONCEPT, name)).convert("RGBA"))


def box_resize(rgba, w, h, alpha_cut=110):
    """프리멀티플라이드 박스 축소 후 알파를 이진화해 픽셀 경계를 다시 세운다."""
    a = rgba.astype(np.float32)
    al = a[..., 3:4] / 255.0
    pm = np.concatenate([a[..., :3] * al, a[..., 3:4]], axis=2)
    small = np.array(Image.fromarray(pm.astype(np.uint8), "RGBA").resize((w, h), Image.BOX)).astype(np.float32)
    outa = small[..., 3:4]
    rgb = np.where(outa > 0, small[..., :3] / np.maximum(outa / 255.0, 1e-4), 0.0)
    rgb = np.clip(rgb, 0, 255)
    hard = np.where(outa >= alpha_cut, 255.0, 0.0)
    return np.concatenate([rgb, hard], axis=2).astype(np.uint8)


def save(rgba, name):
    Image.fromarray(rgba, "RGBA").save(os.path.join(OUT, name))
    return name


def content_box(rgba, thr=40):
    m = rgba[..., 3] > thr
    ys, xs = np.where(m)
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


# ---------------------------------------------------------------- 환경 타일
FLOOR_ROW = (53, 188)
FLOOR_CELLS = [(37, 171), (199, 332), (360, 494), (522, 655), (684, 817),
               (845, 979), (1007, 1154), (1183, 1331), (1360, 1495)]
WALL_ROW = (234, 432)
# 휘도 프로파일로 측정한 모듈 내부 경계. 상단 캡은 밝고(≈70) 정면 기둥은 어둡다(≈38).
WALL_CAP_BAND = (8, 62)
WALL_FACE_BAND = (80, 198)
WALL_CELL_PX = 135
# (모듈 x0, x1, 크리스털 유무). 정면·캡 기본은 무결정 모듈에서 뽑는다.
WALL_MODULES = [(37, 227, False), (257, 425, False), (615, 751, True), (777, 926, True)]
RUBBLE_ROW = (770, 936)
RUBBLE_CELLS = [(46, 141), (182, 265), (308, 411), (457, 580), (604, 780),
                (808, 912), (955, 1036), (1071, 1159), (1203, 1277)]


def build_environment():
    env = load(ENV)

    # 바닥: 카드형 외곽 비네트를 잘라내고 안쪽만 반복 타일로 쓴다.
    y0, y1 = FLOOR_ROW
    inset = 13
    floors = []
    for i, (x0, x1) in enumerate(FLOOR_CELLS):
        cell = env[y0 + inset:y1 - inset + 1, x0 + inset:x1 - inset + 1].copy()
        cell[..., 3] = 255
        floors.append(save(box_resize(cell, 16, 16), "env_floor_%02d.png" % i))

    # 벽: 상단 캡(16x16)과 정면(16x24)을 분리한다.
    # 원본 모듈은 한 셀 폭에 캡 8px·정면 14px 비율이라 타일맵 격자보다 납작하다.
    # 캡은 세로로 2배, 정면은 1.7배 늘려 맞춘다. 캡은 자갈, 정면은 수직 기둥이라
    # 세로 확대가 실루엣을 무너뜨리지 않는다.
    wy0 = WALL_ROW[0]
    caps, faces = [], []
    for i, (x0, x1, crystal) in enumerate(WALL_MODULES):
        cx = (x0 + x1) // 2
        sx = max(0, cx - WALL_CELL_PX // 2)
        cap = env[wy0 + WALL_CAP_BAND[0]:wy0 + WALL_CAP_BAND[1], sx:sx + WALL_CELL_PX].copy()
        cap[..., 3] = 255
        caps.append(save(box_resize(cap, 16, 16), "env_wall_cap_%02d.png" % i))

        face = env[wy0 + WALL_FACE_BAND[0]:wy0 + WALL_FACE_BAND[1], sx:sx + WALL_CELL_PX].copy()
        face[..., 3] = 255
        faces.append(save(box_resize(face, 16, 24), "env_wall_face_%02d.png" % i))

    # 잔석 소품: 실루엣 그대로 셀 폭 비율에 맞춰 축소만 한다.
    ry0, ry1 = RUBBLE_ROW
    props = []
    for i, (x0, x1) in enumerate(RUBBLE_CELLS):
        cell = env[ry0:ry1 + 1, x0:x1 + 1].copy()
        cell[..., 3] = np.where(cell[..., 3] > 200, 255, 0).astype(np.uint8)
        if (cell[..., 3] > 128).sum() < 60:
            continue
        bx0, by0, bx1, by1 = content_box(cell, 128)
        cell = cell[by0:by1 + 1, bx0:bx1 + 1]
        ppc = 135.0 / 16.0
        w = max(3, int(round(cell.shape[1] / ppc)))
        h = max(3, int(round(cell.shape[0] / ppc)))
        props.append(save(box_resize(cell, w, h, alpha_cut=128), "env_rubble_%02d.png" % i))

    contact_sheet(floors, "_preview_env_floor.png", zoom=8)
    contact_sheet(caps + faces, "_preview_env_wall.png", zoom=8)
    contact_sheet(props, "_preview_env_rubble.png", zoom=8)
    return floors, caps, faces, props


# ------------------------------------------------------------- 거너 무기 반동
GUN_FRAMES = [58, 590, 1142, 1686]
GUN_TOP = 194
GUN_CROP_W = 250
GUN_BAND = (168, 292)
GUN_FRAME_W, GUN_FRAME_H = 32, 12


def build_weapon():
    src = load(GUN)
    masked = []
    for x0 in GUN_FRAMES:
        a = src[GUN_TOP:GUN_TOP + 344, x0:x0 + GUN_CROP_W].astype(np.float32).copy()
        rgb = a[..., :3] / 255.0
        mx = rgb.max(2)
        mn = rgb.min(2)
        sat = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
        green = (rgb[..., 1] > rgb[..., 0] * 1.05) & (rgb[..., 1] > rgb[..., 2] * 1.12) & (sat > 0.22)
        skin = (rgb[..., 0] > 0.72) & (rgb[..., 1] > 0.55) & (rgb[..., 2] > 0.42) & (rgb[..., 0] > rgb[..., 2] * 1.25)
        a[..., 3][green | skin] = 0
        a[:GUN_BAND[0], :, 3] = 0
        a[GUN_BAND[1]:, :, 3] = 0
        masked.append(a.astype(np.uint8))

    boxes = [content_box(m) for m in masked]
    x0 = min(b[0] for b in boxes)
    y0 = min(b[1] for b in boxes)
    x1 = max(b[2] for b in boxes)
    y1 = max(b[3] for b in boxes)
    fw, fh = GUN_FRAME_W, GUN_FRAME_H
    sheet = np.zeros((fh, fw * len(masked), 4), np.uint8)
    for i, m in enumerate(masked):
        frame = box_resize(m[y0:y1 + 1, x0:x1 + 1], fw, fh, alpha_cut=96)
        # 원본은 좌향이다. 조준 리그가 +X 를 총구 방향으로 쓰므로 좌우를 뒤집는다.
        sheet[:, i * fw:(i + 1) * fw] = frame[:, ::-1]
    save(sheet, "gunner_weapon_recoil.png")
    Image.fromarray(sheet, "RGBA").resize((fw * len(masked) * 8, fh * 8), Image.NEAREST).save(
        os.path.join(OUT, "_preview_gunner_weapon.png"))
    return "gunner_weapon_recoil.png", (fw, fh, len(masked), x1 - x0 + 1, y1 - y0 + 1)


# ------------------------------------------------------- 적 피격·사망 프레임
ENEMY_FRAMES = [(54, 325), (390, 728), (773, 1058), (1114, 1426), (1482, 1810), (1883, 2117)]
ENEMY_TOP, ENEMY_BOT = 233, 529
ENEMY_FRAME = 32


def build_enemy():
    src = load(ENEMY)
    frames = [src[ENEMY_TOP:ENEMY_BOT + 1, x0:x1 + 1] for x0, x1 in ENEMY_FRAMES]
    bx0, by0, bx1, by1 = content_box(frames[0])
    cx = (bx0 + bx1) / 2.0
    unit = (bx1 - bx0 + 1) / 16.0          # 무손상 표적 폭을 16px 로 맞춘다.
    fw = fh = ENEMY_FRAME
    cw = int(round(fw * unit))
    ch = int(round(fh * unit))
    bottom = by1 + 6 * unit                 # 접지선을 프레임 하단에서 6px 위에 둔다.
    sheet = np.zeros((fh, fw * len(frames), 4), np.uint8)
    for i, f in enumerate(frames):
        canvas = np.zeros((ch, cw, 4), np.uint8)
        ox = int(round(cw / 2.0 - cx))
        oy = int(round(ch - bottom))
        h, w = f.shape[:2]
        sx0, sy0 = max(0, -ox), max(0, -oy)
        dx0, dy0 = max(0, ox), max(0, oy)
        pw = min(w - sx0, cw - dx0)
        ph = min(h - sy0, ch - dy0)
        if pw > 0 and ph > 0:
            canvas[dy0:dy0 + ph, dx0:dx0 + pw] = f[sy0:sy0 + ph, sx0:sx0 + pw]
        sheet[:, i * fw:(i + 1) * fw] = box_resize(canvas, fw, fh, alpha_cut=96)
    save(sheet, "enemy_rock_frames.png")
    Image.fromarray(sheet, "RGBA").resize((fw * len(frames) * 6, fh * 6), Image.NEAREST).save(
        os.path.join(OUT, "_preview_enemy.png"))
    return "enemy_rock_frames.png", (fw, fh, len(frames))


# ------------------------------------------------------------------- 데칼
DECAL_ROWS = [("wall", (295, 560)), ("floor", (620, 865)), ("splat", (920, 1200))]


def column_cells(mask, min_run=20, max_gap=14):
    """행 안에서 서로 떨어진 데칼 덩어리의 x 구간을 찾는다."""
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


def build_decals():
    src = load(DECAL)
    made = {}
    for kind, (y0, y1) in DECAL_ROWS:
        band = src[y0:y1 + 1, :, 3] > 40
        cells = column_cells(band.sum(axis=0) > 1)
        names = []
        for i, (x0, x1) in enumerate(cells):
            cell = src[y0:y1 + 1, max(0, x0):min(src.shape[1], x1 + 1)].copy()
            if (cell[..., 3] > 40).sum() < 60:
                continue
            bx0, by0, bx1, by1 = content_box(cell)
            cell = cell[by0:by1 + 1, bx0:bx1 + 1]
            side = max(cell.shape[0], cell.shape[1])
            pad = np.zeros((side, side, 4), np.uint8)
            pad[(side - cell.shape[0]) // 2:(side - cell.shape[0]) // 2 + cell.shape[0],
                (side - cell.shape[1]) // 2:(side - cell.shape[1]) // 2 + cell.shape[1]] = cell
            names.append(save(box_resize(pad, 16, 16, alpha_cut=70), "decal_%s_%02d.png" % (kind, i)))
        made[kind] = names
        if names:
            contact_sheet(names, "_preview_decal_%s.png" % kind, zoom=8)
    return made


if __name__ == "__main__":
    floors, caps, faces, props = build_environment()
    weapon, wmeta = build_weapon()
    enemy, emeta = build_enemy()
    decals = build_decals()
    print("floors      : %d" % len(floors))
    print("wall caps   : %d, faces: %d" % (len(caps), len(faces)))
    print("rubble      : %d" % len(props))
    print("weapon      : %s frame=%dx%d x%d (src %dx%d)" % (weapon, wmeta[0], wmeta[1], wmeta[2], wmeta[3], wmeta[4]))
    print("enemy       : %s frame=%dx%d x%d" % (enemy, emeta[0], emeta[1], emeta[2]))
    for k, v in decals.items():
        print("decal %-6s: %d" % (k, len(v)))
