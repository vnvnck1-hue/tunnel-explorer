# -*- coding: utf-8 -*-
"""Render trait cards offline from trait-card-layout.json, for a quick look at the result.

    python tools/render_trait_card_preview.py [out.png] [--guides]

Draws in the layout editor's coordinate space (card-art %), which is what the game
lands on once tools/apply_trait_card_layout.py has compensated for the perspective —
so this is a faithful preview of the shipped card, not an approximation.
"""
from PIL import Image, ImageDraw, ImageFont
import io
import json
import math
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.join(HERE, '..', 'assets', 'menu', 'trait-resources')
LAYOUT = os.path.join(HERE, '..', 'trait-card-layout.json')
DATA = os.path.join(HERE, '..', 'trait-sample-data.json')

CARD_PNG = {1: 'trait-card-common-v1.1.1.png', 2: 'trait-card-rare-v1.1.1.png',
            3: 'trait-card-hero-v1.1.1.png', 4: 'trait-card-legendary-v1.1.1.png'}
TIER_NAME = {1: '일반', 2: '희귀', 3: '영웅', 4: '전설'}
FRAME_SCALE = 1.008
SS = 3
CARD_W, CARD_H = 250 * SS, 375 * SS

# The game resolves "Pretendard" (installed per-user); only the ExtraBold face exists,
# so every weight on the card renders from it. Fall back to Malgun if it is absent.
PRETENDARD = os.path.join(os.environ.get('LOCALAPPDATA', ''), r'Microsoft\Windows\Fonts\Pretendard-ExtraBold.otf')
FONT_BOLD = PRETENDARD if os.path.exists(PRETENDARD) else r'C:\Windows\Fonts\malgunbd.ttf'
FONT_REG = FONT_BOLD

# Chrome's line metrics for that face, per em, and the ink box of Hangul relative to the
# baseline - measured in-page. Used to place text exactly where the browser puts it.
F_ASC, F_DESC = 0.952, 0.241
LS_FIX = {'center': 0.5, 'right': 1.0, 'left': 0.0}

ICON_FIT = {}
for line in io.open(os.path.join(HERE, '..', 'tunnel-crew-infinite-mode-v7.1.5.html'), encoding='utf-8'):
    if "'trait-icon-" in line and '":[' not in line and "':[" in line:
        for part in line.strip().rstrip(',').split("],"):
            if "':[" not in part:
                continue
            name, nums = part.split("':[")
            ICON_FIT[name.strip().lstrip("'")] = [float(x) for x in nums.rstrip(']').split(',')]


def hexcolor(v):
    """Match the applier: a bare hex from the editor's free-text colour field is valid."""
    v = str(v).strip()
    if re.fullmatch(r'[0-9a-fA-F]{3}|[0-9a-fA-F]{4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8}', v):
        return '#' + v
    return v


def font(px, weight):
    return ImageFont.truetype(FONT_BOLD if weight >= 700 else FONT_REG, max(1, int(round(px * SS))))


def wrap(d, text, f, maxw, ls):
    """Break at spaces first, like the card's `word-break:keep-all`; split a word only
    when it cannot fit on a line of its own. Letter-spacing counts toward the width."""
    width = lambda t: d.textlength(t, font=f) + ls * len(t)
    lines, cur = [], ''
    for word in text.split(' '):
        cand = word if not cur else cur + ' ' + word
        if width(cand) <= maxw:
            cur = cand
        elif not cur:
            cur = ''
            for ch in word:
                if width(cur + ch) > maxw and cur:
                    lines.append(cur)
                    cur = ch
                else:
                    cur += ch
        else:
            lines.append(cur)
            cur = word
    if cur:
        lines.append(cur)
    return lines


def draw_text_layer(card, d, c, text):
    """Lay the text out the way Chrome does: shrink to fit, centre the line box in the
    layer, then place each line on its baseline at halfLeading + fontAscent."""
    x, y = CARD_W * c['x'] / 100, CARD_H * c['y'] / 100
    w, h = CARD_W * c['w'] / 100, CARD_H * c['h'] / 100
    px = c['size']
    while True:
        fs = px * SS
        f = font(px, c['weight'])
        ls = c['ls'] * fs
        lines = wrap(d, text, f, w, ls)
        block = len(lines) * c['lh'] * fs
        if block <= h or px <= c['min']:
            break
        px = max(c['min'], px - 0.4)

    lh = c['lh'] * fs
    va = c.get('valign', 'center')
    top = y if va == 'top' else (y + h - block) if va == 'bottom' else y + (h - block) / 2
    half = (lh - (F_ASC + F_DESC) * fs) / 2
    fix = ls * LS_FIX.get(c['align'], 0.5)              # trailing letter-space compensation
    fill = hexcolor(c['color'])
    for i, ln in enumerate(lines):
        baseline = top + i * lh + half + F_ASC * fs
        wide = d.textlength(ln, font=f) + ls * len(ln)
        lx = {'center': x + (w - wide) / 2, 'left': x, 'right': x + w - wide}[c['align']] + fix
        for ch in ln:
            d.text((lx, baseline), ch, font=f, fill=fill, anchor='ls')
            lx += d.textlength(ch, font=f) + ls
    return px


def render(trait, layout, guides=False):
    tier = trait['tier']
    L = layout[str(tier)]
    card = Image.new('RGBA', (CARD_W, CARD_H), (0, 0, 0, 0))
    frame = Image.open(os.path.join(ROOT, 'cards', CARD_PNG[tier])).convert('RGBA')
    fw, fh = int(CARD_W * FRAME_SCALE), int(CARD_H * FRAME_SCALE)
    card.alpha_composite(frame.resize((fw, fh), Image.LANCZOS), ((CARD_W - fw) // 2, (CARD_H - fh) // 2))

    ic = L['icon']
    box = CARD_W * ic['size'] / 100
    bx, by = CARD_W * ic['x'] / 100, CARD_H * ic['y'] / 100
    tx, ty, s = ICON_FIT.get(trait['icon'], [0, 0, .78])
    s *= ic.get('fill', .78) / .78
    art = Image.open(os.path.join(ROOT, 'icons', trait['icon'])).convert('RGBA')
    n = max(1, int(round(box * s)))
    art = art.resize((n, n), Image.LANCZOS)
    card.alpha_composite(art, (int(round(bx + box / 2 + (tx / 100 * box - box / 2) * s)),
                               int(round(by + box / 2 + (ty / 100 * box - box / 2) * s))))

    d = ImageDraw.Draw(card)
    if guides:
        d.ellipse([bx, by, bx + box, by + box], outline=(255, 80, 80, 190), width=2 * SS)
        for key, col in (('tier', (0, 255, 0, 170)), ('name', (255, 210, 0, 190)),
                         ('kind', (0, 200, 255, 170)), ('desc', (255, 0, 255, 170))):
            c = L[key]
            d.rectangle([CARD_W * c['x'] / 100, CARD_H * c['y'] / 100,
                         CARD_W * (c['x'] + c['w']) / 100, CARD_H * (c['y'] + c['h']) / 100],
                        outline=col, width=2 * SS)

    draw_text_layer(card, d, L['tier'], TIER_NAME[tier])
    draw_text_layer(card, d, L['name'], trait['name'])
    draw_text_layer(card, d, L['kind'], trait['kind'])
    draw_text_layer(card, d, L['desc'], trait['desc'])
    return card


def main():
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    guides = '--guides' in sys.argv
    out = args[0] if args else os.path.join(HERE, '..', 'trait-card-preview.png')

    layout = json.load(io.open(LAYOUT, encoding='utf-8'))['tiers']
    traits = json.load(io.open(DATA, encoding='utf-8'))
    picks = []
    for tier in (1, 2, 3, 4):
        pool = [t for t in traits if t['tier'] == tier]
        picks.append(max(pool, key=lambda t: len(t['name']) * 2 + len(t['desc']) + len(t['kind'])))

    rows = 2 if guides else 1
    sheet = Image.new('RGB', (CARD_W * 4, CARD_H * rows + (10 * SS if guides else 0)), (18, 14, 26))
    for i, t in enumerate(picks):
        a = render(t, layout, False)
        sheet.paste(a, (i * CARD_W, 0), a)
        if guides:
            b = render(t, layout, True)
            sheet.paste(b, (i * CARD_W, CARD_H + 10 * SS), b)
    sheet = sheet.resize((sheet.width * 2 // SS, sheet.height * 2 // SS), Image.LANCZOS)
    sheet.save(out)
    print('rendered %s' % out)
    print('sampled (longest text per tier): %s' % ', '.join(t['name'] for t in picks))


if __name__ == '__main__':
    main()
