# -*- coding: utf-8 -*-
"""Measure the trait-card artwork. Run this whenever the card frames or the icon art
are re-exported, then paste the output into the two places it names.

Prints two blocks:
  1. REF — the ring / plate / bar / panel windows of each card frame, in card-art %.
     Paste into the `REF` table in trait-card-layout-editor.html; it drives the
     editor's green guide lines and its "기본값으로 되돌리기" defaults.
  2. INF_TRAIT_ICON_FIT — per-icon [tx%, ty%, scale] that centres each icon PNG's
     opaque bounds in the ring. Paste into the table of the same name in the game
     HTML, and into the copy inside the editor.

These are raw artwork measurements. Turning a layout into game CSS — including the
perspective compensation for `.infCardDepth` — is tools/apply_trait_card_layout.py.

    python tools/measure_trait_card_geometry.py
"""
from PIL import Image
from collections import deque, defaultdict
import glob
import math
import os

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'assets', 'menu', 'trait-resources')
CARDS = [(1, 'common'), (2, 'rare'), (3, 'hero'), (4, 'legendary')]
CARD_VERSION = 'v1.1.1'

ICON_FIT_MAX = 78.0     # icon's longer side, as % of the ring diameter
ICON_FIT_DIAG = 99.0    # icon's diagonal cap, so near-square art keeps its corners inside


def lum(p):
    return 0.299 * p[0] + 0.587 * p[1] + 0.114 * p[2]


def flood(px, W, H, seed, test, maxw=0.9, maxh=0.55):
    """4-connected region on a 2px grid; returns None if it leaks past the size guard."""
    if not test(px[seed]):
        return None
    seen = {seed}
    q = deque([seed])
    minx = maxx = seed[0]
    miny = maxy = seed[1]
    while q:
        x, y = q.popleft()
        minx, maxx = min(minx, x), max(maxx, x)
        miny, maxy = min(miny, y), max(maxy, y)
        if maxy - miny > H * maxh or maxx - minx > W * maxw:
            return None
        for dx, dy in ((2, 0), (-2, 0), (0, 2), (0, -2)):
            nx, ny = x + dx, y + dy
            if 0 <= nx < W and 0 <= ny < H and (nx, ny) not in seen and test(px[nx, ny]):
                seen.add((nx, ny))
                q.append((nx, ny))
    return seen


def extents(region):
    """Widest row / tallest column — steadier than the bbox when the edge is shaded."""
    rows, cols = defaultdict(list), defaultdict(list)
    for x, y in region:
        rows[y].append(x)
        cols[x].append(y)
    ry = max(rows, key=lambda y: max(rows[y]) - min(rows[y]))
    cxs = min(rows[ry]), max(rows[ry])
    cx = max(cols, key=lambda x: max(cols[x]) - min(cols[x]))
    cys = min(cols[cx]), max(cols[cx])
    return cxs[0], cxs[1], cys[0], cys[1]


def measure_card(path):
    im = Image.open(path).convert('RGB')
    W, H = im.size
    px = im.load()
    pct = lambda x, y, w, h: (x / W * 100, y / H * 100, w / W * 100, h / H * 100)

    # ring interior: darkest contiguous blob around the art window
    best = None
    for T in range(60, 160, 3):
        r = flood(px, W, H, (512 * W // 1024, 540 * H // 1536), lambda p, T=T: lum(p) < T)
        if r and (best is None or len(r) > len(best)):
            best = r
    x0, x1, y0, y1 = extents(best)
    icon = ((x0 + x1) / 2 / W * 100, (y0 + y1) / 2 / H * 100, (x1 - x0) / W * 100)

    # beige copy panel
    beige = lambda p: p[0] > 175 and p[1] > 145 and p[2] > 105 and p[0] - p[2] > 28
    region = None
    for sy in range(int(H * 0.85), int(H * 0.76), -10):
        region = flood(px, W, H, (W // 2, sy), beige)
        if region and len(region) > 2000:
            break
    x0, x1, y0, y1 = extents(region)
    copy = pct(x0, y0, x1 - x0, y1 - y0)

    # dark meta bar, just above the copy panel
    region = flood(px, W, H, (W // 2, y0 - 60), lambda p: lum(p) < 70, maxh=0.25)
    a0, a1, b0, b1 = extents(region)
    meta = pct(a0, b0, a1 - a0, b1 - b0)

    # tier plate at the top: same-colour blob around the plate's centre
    best = None
    for sy in range(int(H * 0.05), int(H * 0.16), 10):
        c = px[W // 2, sy]
        for tol in (55, 70, 90):
            same = lambda p, c=c, tol=tol: abs(p[0] - c[0]) + abs(p[1] - c[1]) + abs(p[2] - c[2]) < tol
            r = flood(px, W, H, (W // 2, sy), same, maxh=0.35)
            if r and len(r) > 4000 and (best is None or len(r) > len(best)):
                best = r
    x0, x1, y0, y1 = extents(best)
    plate = pct(x0, y0, x1 - x0, y1 - y0)
    return icon, plate, meta, copy


def main():
    print('/* paste into REF in trait-card-layout-editor.html - card-art %, ring is [x,y,size] */')
    print('const REF={')
    rows = []
    for tier, name in CARDS:
        icon, plate, bar, panel = measure_card(os.path.join(ROOT, 'cards', 'trait-card-%s-%s.png' % (name, CARD_VERSION)))
        cx, cy, dw = icon
        # the ring layer is square in px, so its height as a % of the 2:3 card box is w/1.5
        ring = (cx - dw / 2, cy - (dw / 1.5) / 2, dw)
        rows.append(' %d:{ring:[%.2f,%.2f,%.2f],plate:[%.2f,%.2f,%.2f,%.2f],'
                    'bar:[%.2f,%.2f,%.2f,%.2f],panel:[%.2f,%.2f,%.2f,%.2f]}'
                    % ((tier,) + ring + plate + bar + panel))
    print(',\n'.join(rows))
    print('};')

    print('\nconst INF_TRAIT_ICON_FIT={')
    rows = []
    for f in sorted(glob.glob(os.path.join(ROOT, 'icons', '*.png'))):
        im = Image.open(f).convert('RGBA')
        W, H = im.size
        bb = im.split()[3].point(lambda v: 255 if v > 16 else 0).getbbox()
        cx, cy = (bb[0] + bb[2]) / 2 / W * 100, (bb[1] + bb[3]) / 2 / H * 100
        w, h = (bb[2] - bb[0]) / W * 100, (bb[3] - bb[1]) / H * 100
        s = min(ICON_FIT_MAX / max(w, h), ICON_FIT_DIAG / math.hypot(w, h))
        rows.append(" '%s':[%.2f,%.2f,%.4f]" % (os.path.basename(f), 50 - cx, 50 - cy, s))
    print(',\n'.join(rows))
    print('};')


if __name__ == '__main__':
    main()
