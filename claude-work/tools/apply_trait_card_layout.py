# -*- coding: utf-8 -*-
"""Write a layout exported from trait-card-layout-editor.html into the game HTML.

    python tools/apply_trait_card_layout.py trait-card-layout.json

The editor works in **card-art %** — what you see sitting on the frame artwork.
The game draws those layers inside `.infCardDepth`, which applies
`perspective(850px)` with its origin at 50%/54%, so every layer is magnified by
850/(850-z) on screen while the frame image itself only gets `scale(1.008)`.
This script divides that magnification back out, for positions *and* for type
size, so the shipped card matches the editor pixel for pixel.

It only rewrites the block between the /*<<trait-card-layout>>*/ markers.
"""
import io
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
GAME = os.path.join(HERE, '..', 'tunnel-crew-infinite-mode-v7.1.5.html')
BEGIN = '/*<<trait-card-layout>>*/'
END = '/*<</trait-card-layout>>*/'

PERSPECTIVE = 850.0        # .infCardDepth
FRAME_SCALE = 1.008        # .infCardFrame
ORIGIN = (50.0, 54.0)      # .infCardDepth transform-origin
Z_ICON, Z_TEXT = 38.0, 28.0
Z_OVER_ICON = 44.0         # a text layer drawn on top of the icon must sit in front of it
CARD_RATIO = 1.5

# editor key -> css var prefix
PREFIX = {'tier': 'tl', 'name': 'nm', 'kind': 'kd', 'desc': 'ds'}
VALIGN = {'top': 'flex-start', 'center': 'center', 'bottom': 'flex-end'}
# letter-spacing appends a space after the LAST glyph too, so a centred line's visible
# ink sits half a space left of centre (and a right-aligned one a full space left).
LS_FIX = {'center': 0.5, 'right': 1.0, 'left': 0.0}


def hexcolor(v):
    """The editor's colour field is free text; accept a bare hex and make it valid CSS."""
    v = str(v).strip()
    if re.fullmatch(r'[0-9a-fA-F]{3}|[0-9a-fA-F]{4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8}', v):
        return '#' + v
    return v


def layer_z(c, icon, notes, key):
    """`.infCardDepth` is preserve-3d, so siblings are painted by depth, not z-index —
    the icon sits at 38px and would cover any text laid over it. The editor previews
    flat (text above icon), so lift such a layer in front of the icon to match."""
    if 'z' in c:
        return float(c['z'])
    ring_h = icon['size'] / CARD_RATIO
    overlaps = not (c['y'] + c['h'] <= icon['y'] or c['y'] >= icon['y'] + ring_h)
    if overlaps and not (c['x'] + c['w'] <= icon['x'] or c['x'] >= icon['x'] + icon['size']):
        notes.append('%s: over the icon, lifted to translateZ(%gpx)' % (key, Z_OVER_ICON))
        return Z_OVER_ICON
    return Z_TEXT


def k(z):
    return PERSPECTIVE / (PERSPECTIVE - z)


def to_screen(u, v):
    return 50 + FRAME_SCALE * (u - 50), 50 + FRAME_SCALE * (v - 50)


def to_css(sx, sy, z):
    return ORIGIN[0] + (sx - ORIGIN[0]) / k(z), ORIGIN[1] + (sy - ORIGIN[1]) / k(z)


def css_rect(x, y, w, h, z):
    sl, st = to_screen(x, y)
    sr, sb = to_screen(x + w, y + h)
    cl, ct = to_css(sl, st, z)
    cr, cb = to_css(sr, sb, z)
    return cl, ct, cr - cl, cb - ct


def build(layout, notes):
    """layout: {"1": {...}, ...} in card-art % -> the per-tier CSS var block."""
    out = []
    for tier in ('1', '2', '3', '4'):
        t = layout[tier]
        sel = ('#infLevelCards .infTraitCard,#infLegendCards .infTraitCard' if tier == '1' else
               '#infLevelCards .infTraitCard[data-tier="%s"],#infLegendCards .infTraitCard[data-tier="%s"]' % (tier, tier))
        v = []

        ic = t['icon']
        scx, scy = to_screen(ic['x'] + ic['size'] / 2, ic['y'] + ic['size'] / CARD_RATIO / 2)
        ax, ay = to_css(scx, scy, Z_ICON)
        w = FRAME_SCALE * ic['size'] / k(Z_ICON)
        # the layer is square in px, so its height as a % of the 2:3 card box is w/1.5
        v.append('--ic-l:%.2f%%;--ic-t:%.2f%%;--ic-w:%.2f%%;--ic-fill:%.4f'
                 % (ax - w / 2, ay - w / CARD_RATIO / 2, w, ic.get('fill', .78) / .78))

        for key, p in PREFIX.items():
            c = t[key]
            z = layer_z(c, ic, notes, 'tier %s / %s' % (tier, key))
            l, tp, ww, hh = css_rect(c['x'], c['y'], c['w'], c['h'], z)
            font_k = FRAME_SCALE / k(z)   # type shares its own layer's magnification
            col = hexcolor(c['color'])
            if col != c['color']:
                notes.append('tier %s / %s: colour %r -> %r' % (tier, key, c['color'], col))
            v.append(
                '--{p}-l:{l:.2f}%;--{p}-t:{t:.2f}%;--{p}-w:{w:.2f}%;--{p}-h:{h:.2f}%;--{p}-z:{z:g}px;'
                '--{p}-fs:{fs:.2f};--{p}-min:{mn:.2f};--{p}-lh:{lh};--{p}-ls:{ls}em;'
                '--{p}-fw:{fw};--{p}-col:{col};--{p}-sh:{sh};--{p}-ta:{ta};--{p}-ai:{ai};--{p}-lsfix:{lsfix}em'.format(
                    p=p, l=l, t=tp, w=ww, h=hh, z=z,
                    fs=c['size'] * font_k, mn=c['min'] * font_k,
                    lh=c['lh'], ls=c['ls'], fw=c['weight'], col=col,
                    sh=c.get('shadow') or 'none', ta=c['align'],
                    ai=VALIGN.get(c.get('valign', 'center'), 'center'),
                    lsfix=round(c['ls'] * LS_FIX.get(c['align'], 0.5), 4)))
        out.append('%s{%s}' % (sel, ';'.join(v)))
    return '\n'.join(out)


def main():
    src = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, '..', 'trait-card-layout.json')
    data = json.load(io.open(src, encoding='utf-8'))
    layout = data['tiers'] if 'tiers' in data else data
    for tier in ('1', '2', '3', '4'):
        assert tier in layout, 'layout is missing tier %s' % tier
        for key in ['icon'] + list(PREFIX):
            assert key in layout[tier], 'tier %s is missing layer %r' % (tier, key)

    notes = []
    block = build(layout, notes)
    s = io.open(GAME, encoding='utf-8').read()
    a, b = s.index(BEGIN), s.index(END)
    s = s[:a + len(BEGIN)] + '\n' + block + '\n' + s[b:]
    io.open(GAME, 'w', encoding='utf-8', newline='').write(s)
    print('applied %s -> %s' % (os.path.basename(src), os.path.basename(GAME)))
    for note in notes:
        print('  note: %s' % note)


if __name__ == '__main__':
    main()
