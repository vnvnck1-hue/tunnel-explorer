# -*- coding: utf-8 -*-
"""
Kenney 샘플 SFX 블록을 본편 HTML에 적용한다. 몇 번 돌려도 결과가 같다(idempotent).

  python claude-work/audio/apply-kenney-sfx.py [target.html]

블록 자체는 tools/gen_kenney_smp_block.py 가 만든다.
다른 세션이 같은 HTML을 덮어써서 블록이 사라지면 이 스크립트를 그냥 다시 돌리면 된다.
"""
import os, re, sys, subprocess

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
TARGET = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "tunnel-crew-infinite-mode-v7.8.0.html")
GEN = os.path.join(ROOT, "tools", "gen_kenney_smp_block.py")
BLOCK = os.path.join(os.path.dirname(GEN), "smp_block.js")

BEGIN = "/* KENNEY-SFX-BLOCK:BEGIN */"
END = "/* KENNEY-SFX-BLOCK:END */"
AMBI_SRC = os.path.join(os.path.dirname(os.path.abspath(__file__)), "ambience_block.js")
AMBI_BEGIN = "/* AMBIENCE-BLOCK:BEGIN */"
AMBI_END = "/* AMBIENCE-BLOCK:END */"
# 앰비언스 블록은 BGM_ROUTE 정의가 끝난 직후에 넣는다
AMBI_ANCHOR = "window.addEventListener('focus',()=>BGM_ROUTE.ensure());"
BGM = "/* ── 절차적 BGM ── */"

# SFX 객체가 닫히는 지점 - voice(who) 다음의 '};'
ANCHOR = re.compile(r"(\n\s*voice\(who\)\{.*?\n\};)", re.S)

# 호출부 교체: (설명, 찾을 문자열, 바꿀 문자열). 이미 바뀌어 있으면 건너뛴다.
CALLSITES = [
    ("gunfire",
     "_gunCd=cd/(inf?inf.fireRate:1);SFX.tick&&SFX.tick();",
     "_gunCd=cd/(inf?inf.fireRate:1);(SFX.shot?SFX.shot():SFX.tick&&SFX.tick());"),
    ("breaker-shot",
     "J.kick(2.6,-ca,-sa);SFX.tick&&SFX.tick();toast('파쇄탄 발사",
     "J.kick(2.6,-ca,-sa);(SFX.shot?SFX.shot():SFX.tick&&SFX.tick());toast('파쇄탄 발사"),
    ("pink-ore-break",
     "if(oreCol)SFX.ore();else SFX.brk();",
     "if(oreCol)(SFX.oreBreak?SFX.oreBreak():SFX.ore());else SFX.brk();"),
    # hurtEnemy() 안 처치 분기. 이 형태의 호출은 파일 전체에서 여기 한 곳뿐이다.
    ("enemy-death",
     "if(SFX.ore)SFX.ore();",
     "if(SFX.kill)SFX.kill();else if(SFX.ore)SFX.ore();"),
    ("trait-card-appear",
     "SFX.tick&&SFX.tick();if(t.tier>=3)",
     "(SFX.cardFlip?SFX.cardFlip():SFX.tick&&SFX.tick());if(t.tier>=3)"),
    ("trait-card-pick",
     "SFX.buy&&SFX.buy();setTimeout(infCheckLevel,80);",
     "(SFX.cardPick?SFX.cardPick():SFX.buy&&SFX.buy());setTimeout(infCheckLevel,80);"),
    ("rune-shard-drop",
     "const shard=Math.random()<(oreCol?.42:.16);SFX.tick();",
     "const shard=Math.random()<(oreCol?.42:.16);(SFX.shard?SFX.shard():SFX.tick());"),
]


def main():
    if not os.path.exists(BLOCK) or os.path.getmtime(GEN) > os.path.getmtime(BLOCK):
        print("[gen] regenerating smp_block.js")
        subprocess.run([sys.executable, GEN], check=True)
    block = open(BLOCK, encoding="utf-8").read().rstrip("\n")

    src = open(TARGET, "rb").read().decode("utf-8")
    nl = "\r\n" if "\r\n" in src[:20000] else "\n"
    if nl == "\r\n":
        block = block.replace("\n", "\r\n")

    # 1) 마커가 있는 기존 블록 제거
    i, j = src.find(BEGIN), src.find(END)
    if i >= 0 and j > i:
        src = src[:i] + src[j + len(END):].lstrip("\r\n")
        print("[block] removed previous block")

    m = ANCHOR.search(src)
    if not m:
        sys.exit("[block] SFX object end not found - check anchor")

    # 2) 마커가 없던 초기 버전 블록 정리 (SFX 객체 ~ 절차적 BGM 주석 사이)
    b = src.find(BGM, m.end())
    if b > 0 and "Kenney" in src[m.end():b]:
        src = src[:m.end()] + nl + src[b:]
        print("[block] removed legacy (unmarked) block")
        m = ANCHOR.search(src)

    src = src[:m.end()] + nl + block + src[m.end():]
    print("[block] inserted (%d bytes)" % len(block))

    # 3) 호출부
    for name, old, new in CALLSITES:
        old_n, new_n = old.replace("\n", nl), new.replace("\n", nl)
        if new_n in src:
            print("[call] %s - already applied" % name)
        elif old_n in src:
            src = src.replace(old_n, new_n, 1)
            print("[call] %s - patched" % name)
        else:
            sys.exit("[call] %s - source string not found" % name)

    # 4) 앰비언스 블록 (로비/땅굴 환경음)
    ai, aj = src.find(AMBI_BEGIN), src.find(AMBI_END)
    if ai >= 0 and aj > ai:
        src = src[:ai] + src[aj + len(AMBI_END):].lstrip(chr(13) + chr(10))
        print("[ambi] removed previous block")
    ablock = open(AMBI_SRC, encoding="utf-8").read().strip()
    if nl != chr(10):
        ablock = ablock.replace(chr(10), nl)
    a = src.find(AMBI_ANCHOR)
    if a < 0:
        sys.exit("[ambi] BGM_ROUTE anchor not found")
    a += len(AMBI_ANCHOR)
    src = src[:a] + nl + ablock + src[a:]
    print("[ambi] inserted (%d bytes)" % len(ablock))

    tmp = TARGET + ".tmp"
    open(tmp, "wb").write(src.encode("utf-8"))
    os.replace(tmp, TARGET)
    print("[done] %s (%.2f MB)" % (os.path.basename(TARGET), os.path.getsize(TARGET) / 1048576))


main()
