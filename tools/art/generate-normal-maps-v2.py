"""승인 albedo 에서 노멀맵을 만든다 — 다중 스케일 + 알파 실루엣 베벨.

왜 v2 인가
----------
v1(`generate-stratum-normal-maps.ps1`)은 휘도를 높이로 보고 **1픽셀 중앙차분** 하나만 썼다.
그래서 만들어지는 기울기가 거의 전부 픽셀 단위 잡음이었고(저주파 비중 바닥 9% · 벽 정면 19%),
128 PPU 화면에서 한 칸 떨어진 광원이 보면 이웃 픽셀끼리 상쇄돼 아무것도 남지 않는다.
실제로 랩에서 노멀 강도 0 과 3.0 의 평균 차가 3.2 에 그쳤다(2026-09-10 실측).

v2 는 두 가지를 더한다.

1. **다중 스케일** — 높이를 반지름 0·1·2·4·8·16 으로 흐린 뒤 각각의 기울기를 가중 합한다.
   큰 반지름일수록 진폭이 줄어드는 것을 `(r+1)**LEVEL_GAIN` 으로 보상한다. 잡음이 아니라
   **형태**가 남아야 멀리 있는 광원이 면의 방향을 읽는다.
2. **알파 실루엣 베벨** — 잘라낸 스프라이트(벽 정면·림)는 가장자리에서 안쪽으로 높이를
   떨어뜨려 바깥을 향한 노멀을 만든다. 2D 스프라이트가 부피로 읽히는 건 대부분 이 효과다.
   불투명 타일(바닥·cap)은 마스크가 전부 1 이라 이 항이 저절로 0 이 된다.

albedo 휘도를 높이로 쓰는 것 자체는 v1 과 같다(요청서 §2-A 가 금지한 조건). 손으로 그린
타일은 화가가 이미 음영을 그려 넣어서 휘도가 형태 단서이긴 하지만, **칠이 어두우면 파인 것으로
읽는 한계는 그대로 남는다.** 정식 노멀이 오면 이 도구는 버린다.

출력은 v1 과 같은 경로·파일명이라 Unity 배선을 건드리지 않는다. OpenGL +Y · Linear.
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "art-production" / "test-room-v01"
APPROVED_ALBEDO = ART / "approved" / "albedo"
APPROVED_NORMAL = ART / "approved" / "normal"
UNITY_DIR = ROOT / "unity" / "TunnelCrew" / "Assets" / "Art" / "Visual" / "ReferenceCalibrationV1"

FAMILIES = ("tr01_reference_", "tr01_stratum2_", "tr01_stratum3_", "tr01_abyss_")
VARIANTS = ("a", "b", "c")

# 스케일 층과 가중치. 큰 스케일을 살려야 형태가 남는다.
RADII = (0, 1, 2, 4, 8, 16)
WEIGHTS = (0.22, 0.35, 0.60, 1.00, 1.15, 1.00)
LEVEL_GAIN = 1.0           # 흐릴수록 줄어드는 진폭을 (r+1)**GAIN 으로 보상

# 종류별 설정. tile = 양축 반복(바닥·cap), strip = 가로만 반복(정면·림).
KINDS = {
    "floor":        dict(token="floor",        wrap=(True, True),  strength=0.72, bevel=0.0),
    "wall_top":     dict(token="wall_top",     wrap=(True, True),  strength=0.80, bevel=0.0),
    "wall_top_rim": dict(token="wall_top_rim", wrap=(True, False), strength=0.82, bevel=0.9),
    "wall_front":   dict(token="wall_front",   wrap=(True, False), strength=0.80, bevel=0.7),
}
BEVEL_RADIUS = 10          # 실루엣에서 안쪽으로 몇 px 을 깎을 것인가


def box_blur(a: np.ndarray, r: int, wrap: tuple[bool, bool]) -> np.ndarray:
    """분리형 박스 블러. 축마다 반복(wrap) 또는 가장자리 복제를 고른다."""
    if r <= 0:
        return a.astype(np.float64, copy=True)
    out = a.astype(np.float64)
    for axis, w in enumerate(wrap):
        n = out.shape[axis]
        pad = min(r, n - 1)
        mode = "wrap" if w else "edge"
        padded = np.pad(out, [(pad, pad) if i == axis else (0, 0) for i in range(2)], mode=mode)
        c = np.cumsum(padded, axis=axis)
        c = np.concatenate(
            [np.zeros([1 if i == axis else s for i, s in enumerate(c.shape)]), c], axis=axis
        )
        hi = np.take(c, np.arange(2 * pad + 1, n + 2 * pad + 1), axis=axis)
        lo = np.take(c, np.arange(0, n), axis=axis)
        out = (hi - lo) / (2 * pad + 1)
    return out


def gradient(h: np.ndarray, wrap: tuple[bool, bool]) -> tuple[np.ndarray, np.ndarray]:
    """간격 1 중앙차분. x 는 오른쪽이 +, y 는 아래가 +(이미지 좌표)."""
    wy, wx = wrap[0], wrap[1]
    mx = "wrap" if wx else "edge"
    my = "wrap" if wy else "edge"
    px = np.pad(h, ((0, 0), (1, 1)), mode=mx)
    py = np.pad(h, ((1, 1), (0, 0)), mode=my)
    gx = (px[:, 2:] - px[:, :-2]) * 0.5
    gy = (py[2:, :] - py[:-2, :]) * 0.5
    return gx, gy


def build_normal(path: Path, cfg: dict) -> Image.Image:
    im = Image.open(path).convert("RGBA")
    rgba = np.asarray(im).astype(np.float64) / 255.0
    rgb, alpha = rgba[..., :3], rgba[..., 3]
    wrap = cfg["wrap"]

    # 높이 = 휘도. 타일마다 평균 밝기가 달라도 기울기만 쓰므로 정규화는 대비만 맞춘다.
    luma = 0.2126 * rgb[..., 0] + 0.7152 * rgb[..., 1] + 0.0722 * rgb[..., 2]
    spread = float(luma.max() - luma.min())
    if spread > 1e-6:
        luma = (luma - luma.min()) / spread

    # 투명 픽셀은 높이에 관여하지 않는다 — 바깥의 0 이 가장자리에 가짜 절벽을 만든다.
    mask = (alpha > 0.5).astype(np.float64)
    if mask.min() < 1.0:
        inside_mean = float((luma * mask).sum() / max(1.0, mask.sum()))
        luma = luma * mask + inside_mean * (1.0 - mask)

    gx = np.zeros_like(luma)
    gy = np.zeros_like(luma)
    for r, w in zip(RADII, WEIGHTS):
        bx, by = gradient(box_blur(luma, r, wrap), wrap)
        gain = w * ((r + 1) ** LEVEL_GAIN)
        gx += bx * gain
        gy += by * gain

    # 실루엣 베벨 — 가장자리에서 안쪽으로 높이를 떨군다.
    if cfg["bevel"] > 0.0 and mask.min() < 1.0:
        shell = box_blur(mask, BEVEL_RADIUS, wrap)
        sx, sy = gradient(shell, wrap)
        scale = cfg["bevel"] * BEVEL_RADIUS
        gx += sx * scale
        gy += sy * scale

    # 목표 세기로 정규화 — 종류마다 기울기 크기를 예측 가능하게 만든다.
    mag = np.sqrt(gx * gx + gy * gy)
    ref = float(np.percentile(mag, 90))
    if ref > 1e-9:
        k = cfg["strength"] / ref
        gx *= k
        gy *= k

    # OpenGL +Y: 화면 위쪽이 +Y 이므로 이미지 y 증가(아래)와 부호가 반대다.
    nx, ny, nz = -gx, gy, np.ones_like(gx)
    inv = 1.0 / np.sqrt(nx * nx + ny * ny + nz * nz)
    out = np.zeros(rgba.shape, dtype=np.uint8)
    out[..., 0] = np.clip(np.rint((nx * inv * 0.5 + 0.5) * 255.0), 0, 255)
    out[..., 1] = np.clip(np.rint((ny * inv * 0.5 + 0.5) * 255.0), 0, 255)
    out[..., 2] = np.clip(np.rint((nz * inv * 0.5 + 0.5) * 255.0), 0, 255)
    out[..., 3] = np.clip(np.rint(alpha * 255.0), 0, 255)
    return Image.fromarray(out, mode="RGBA")


def low_freq_share(img: Image.Image, wrap: tuple[bool, bool]) -> tuple[float, float]:
    a = np.asarray(img.convert("RGB")).astype(np.float64)
    nx = a[..., 0] / 127.5 - 1.0
    ny = a[..., 1] / 127.5 - 1.0
    mag = np.sqrt(nx * nx + ny * ny)
    lx, ly = box_blur(nx, 8, wrap), box_blur(ny, 8, wrap)
    low = np.sqrt(lx * lx + ly * ly)
    return float(mag.mean()), float(low.mean() / max(1e-9, mag.mean()))


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true", help="파일을 쓰지 않고 수치만 본다")
    args = ap.parse_args()

    APPROVED_NORMAL.mkdir(parents=True, exist_ok=True)
    UNITY_DIR.mkdir(parents=True, exist_ok=True)

    written = 0
    missing: list[str] = []
    stats: dict[str, list[tuple[float, float]]] = {}
    for family in FAMILIES:
        for kind, cfg in KINDS.items():
            for v in VARIANTS:
                stem = f"{family}{cfg['token']}_{v}"
                src = APPROVED_ALBEDO / f"{stem}_albedo.png"
                if not src.exists():
                    missing.append(src.name)
                    continue
                img = build_normal(src, cfg)
                mean, share = low_freq_share(img, cfg["wrap"])
                stats.setdefault(kind, []).append((mean, share))
                if not args.dry_run:
                    dst = APPROVED_NORMAL / f"{stem}_normal.png"
                    img.save(dst)
                    img.save(UNITY_DIR / f"{stem}_normal.png")
                written += 1

    print(f"{'종류':14s} {'|xy| 평균':>9s} {'저주파 비중':>11s}")
    for kind, rows in stats.items():
        m = sum(r[0] for r in rows) / len(rows)
        sh = sum(r[1] for r in rows) / len(rows)
        print(f"{kind:14s} {m:9.3f} {sh:10.1%}")

    if missing:
        print("\n없는 albedo:", len(missing))
        for m in missing[:10]:
            print("   ", m)
    print(f"\n{'(모의) ' if args.dry_run else ''}노멀 {written} 장")
    return 0


if __name__ == "__main__":
    sys.exit(main())
