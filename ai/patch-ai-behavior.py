#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
AI_HUMANIZE_V1 — AI 플레이어 행동 고도화 패치기

  python ai/patch-ai-behavior.py [베이스.html] [--out 결과.html]

기본값: tunnel-crew-infinite-mode-v7.8.1.html → -ai-behavior.html

왜 패치 스크립트인가
  · 본편 HTML 은 17MB 라 손으로 못 고치고, 다른 에이전트가 같은 파일을 쓰면
    내 변경이 통째로 지워진다. 항상 베이스에서 새로 생성하므로 재실행이 안전하다.
  · ai/crew-ai.js 원본은 현재 주입본(HTML)보다 뒤처져 있다(센트리 수치·적탄 회피·
    빙의 조종 등이 HTML 에만 있다). 그래서 inject-ai-crew.py 로 재주입하면
    기능이 퇴행한다. 이 스크립트는 주입본을 직접 패치한다.

앵커를 정확히 1회 찾지 못하면 조용히 넘어가지 않고 멈춘다.

무엇을 바꾸는가 — 요약
  1) 성향(persona)·기분(mood)·의도(intent) 3겹 게이트: 모든 선택적 행위는
     "쿨다운이 돌면 즉시"가 아니라 망설임 시간 + 확률 판정을 통과해야 한다.
  2) 지금까지 아예 없던 직업 행동을 추가: 드릴러 기반암 균열·돌파 파기,
     거너 파쇄탄 공격·조기 기폭·통로 경계, 스카우트 정찰·펄스·그래플,
     엔지니어 센트리 자리 선정.
  3) 관전 리더 오토파일럿이 Q·E 를 쓴다(지금까지 한 번도 누르지 않았다).

반드시 해야 하는 것에는 확률을 넣지 않는다 — 탈출 포트 탑승, 쓰러진 동료 구조,
보스탄 회피, 파묻힘 탈출. 여기에 무작위성을 넣으면 크루가 죽는다.
"""

import sys
from pathlib import Path

try:                                    # 윈도 콘솔(cp949)에서 한글·em dash 출력이 깨지지 않게
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
except Exception:
    pass

ROOT = Path(__file__).resolve().parent.parent
DEFAULT_BASE = 'tunnel-crew-infinite-mode-v7.8.1.html'
DEFAULT_OUT = 'tunnel-crew-infinite-mode-v7.8.1-ai-behavior.html'


class Fail(Exception):
    pass


LOG = []


def patch(src, name, anchor, repl, count=1):
    n = src.count(anchor)
    if n != count:
        raise Fail(
            '[%s] 앵커가 %d회여야 하는데 %d회 발견됐다 — 게임 코드가 바뀌었다.\n'
            '  앵커 머리: %s' % (name, count, n, anchor.strip().splitlines()[0][:160])
        )
    LOG.append(name)
    return src.replace(anchor, repl, count)


def replace_fn(src, name, header, new_text):
    """header(`  function foo(...) {`)로 시작하는 함수를 중괄호 짝을 세어 통째로 교체."""
    n = src.count(header)
    if n != 1:
        raise Fail('[%s] 함수 헤더가 1회여야 하는데 %d회 — 코드가 바뀌었다.\n  %s' % (name, n, header))
    i = src.index(header)
    j = src.index('{', i + len(header) - 1)
    depth = 0
    k = j
    while k < len(src):
        if src[k] == '{':
            depth += 1
        elif src[k] == '}':
            depth -= 1
            if depth == 0:
                break
        k += 1
    if depth != 0:
        raise Fail('[%s] 중괄호 짝을 못 찾았다' % name)
    LOG.append(name)
    return src[:i] + new_text + src[k + 1:]


# ══════════════════════════════════════════════════════════════════════
#  1) 휴먼화 코어 + 새 직업 행동 (AI 크루 블록 안에 삽입)
# ══════════════════════════════════════════════════════════════════════

HUMANIZE = r'''
  /* ══════════════════════════════════════════════════════════════════
     AI_HUMANIZE_V1 — 사람처럼 보이게 하는 층
     ──────────────────────────────────────────────────────────────────
     문제: 쿨다운이 도는 순간 정확히 스킬을 쓰고, 항상 최적의 벽을 고르고,
     항상 최적의 표적을 쏘는 크루는 동료가 아니라 포탑으로 보인다. 행동을
     추가할수록 이 문제는 더 심해진다 — 완벽한 행동이 늘어날 뿐이다.

     해법: 모든 "선택적 행위"를 세 겹으로 흘려보낸다.
       1) 성향(persona) — 크루마다 다른 성실성·적극성·조준 흔들림·판단 주기·
          광맥 편애도. 같은 직업이어도 판마다 다른 사람이 앉는다.
       2) 기분(mood)    — 8~20초마다 재추첨되는 배율. 한 판 안에서도 리듬이 변한다.
       3) 의도(intent)  — 준비가 끝나도 곧바로 쓰지 않는다. 망설임 시간을 굴리고,
          결심의 순간에 확률 판정을 한 번 더 한다. 실패하면 더 길게 뜸을 들인다.
          그래서 같은 상황에서 할 때도 있고 안 할 때도 있다.

     반드시 해야 하는 것은 흘리지 않는다 — 탈출 포트 탑승, 쓰러진 동료 구조,
     보스탄 회피, 파묻힘 탈출. 여기에 확률을 넣으면 크루가 죽는다.
     ══════════════════════════════════════════════════════════════════ */

  const roll = (p) => Math.random() < p;

  function rollPersona(roleId) {
    const p = {
      eager: rnd(0.60, 1.45),      /* 준비된 스킬을 얼마나 빨리 쓰는가 */
      discipline: rnd(0.55, 1.00), /* 자기 직업 일을 얼마나 성실히 챙기는가 */
      aggression: rnd(0.60, 1.35), /* 얼마나 멀리 나가 싸우는가 */
      caution: rnd(0.55, 1.35),    /* 방어막·후퇴·대시를 얼마나 일찍 쓰는가 */
      greed: rnd(0.35, 1.35),      /* 바닥의 재화에 얼마나 반응하는가 */
      curiosity: rnd(0.50, 1.45),  /* 정찰·딴짓을 얼마나 하는가 */
      focus: rnd(0.55, 1.00),      /* 낮으면 하던 일을 자주 놓는다 */
      aimErr: rnd(0.022, 0.085),   /* 조준 흔들림(rad) */
      reloadAt: rnd(0.26, 0.62),   /* 이 비율 밑으로 떨어지면 채운다 */
      react: rnd(0.11, 0.26),      /* 재판단 주기(초) */
      oreBias: rnd(0.55, 1.50),    /* 광맥을 얼마나 편애하는가 */
      strafe: Math.random() < 0.5 ? 1 : -1,
    };
    /* 직업마다 성향의 중심이 조금 다르다 — 거너는 더 적극적, 스카우트는 더 호기심 많다 */
    if (roleId === 'gunner') { p.aggression *= 1.15; p.curiosity *= 0.85; }
    if (roleId === 'scout') { p.curiosity *= 1.35; p.caution *= 1.10; }
    if (roleId === 'driller') { p.discipline *= 1.10; p.curiosity *= 0.90; }
    if (roleId === 'engineer') { p.discipline *= 1.15; p.aggression *= 0.90; }
    return p;
  }

  function updateMood(m, dt) {
    m.moodT -= dt;
    if (m.moodT > 0) return;
    m.moodT = rnd(8, 20);
    m.mood = rnd(0.72, 1.28);
  }

  /* ── 의도 게이트 ──────────────────────────────────────────────────
     cfg = { min, max, p, eager }
       · ready 가 거짓이면 대기값을 초기화한다 (쿨다운 중)
       · 준비 직후엔 망설임 시간을 굴린다 (성향·기분으로 나눈다)
       · 시간이 끝나면 확률 판정. 실패하면 더 긴 망설임 — 이번 판은 그냥 넘긴다

     대기는 게임 시간(G.t) 기준 절대 시각으로 잡는다. 프레임마다 깎으면
     decide() 가 0.12~0.26초에 한 번만 도는 탓에 실제 대기가 십수 배로
     늘어난다(1차 구현에서 엔지니어가 20초 동안 센트리를 안 세웠다).
     ────────────────────────────────────────────────────────────── */
  function intent(m, tag, ready, cfg) {
    const W = m.wait || (m.wait = {});
    const c = cfg || {};
    if (!ready) { W[tag] = -1; return false; }
    const lo = c.min == null ? 0.4 : c.min, hi = c.max == null ? 2.6 : c.max;
    const eager = Math.max(0.35, m.pers.eager * (m.mood || 1) * (c.eager == null ? 1 : c.eager));
    if (W[tag] == null || W[tag] < 0) { W[tag] = G.t + rnd(lo, hi) / eager; return false; }
    if (G.t < W[tag]) return false;
    const p = Math.min(0.97, (c.p == null ? 0.7 : c.p) * (0.62 + m.pers.discipline * 0.48));
    if (Math.random() < p) { W[tag] = -1; return true; }
    W[tag] = G.t + rnd(lo, hi) * rnd(1.3, 2.8);
    return false;
  }

  const litAt = (x, y) => {
    if (!G.lamps) return false;
    for (const l of G.lamps) if (Math.hypot(l.x - x, l.y - y) < (l.rad || 120) * 0.8) return true;
    return false;
  };
  const seenAt = (x, y) => ((typeof LOS !== 'undefined' && LOS.seenAt) ? !!LOS.seenAt(x, y) : true);

  /* 딴짓 — 잠깐 멈춰 주위를 둘러본다. 목적 없는 행동이 사람처럼 보이게 한다 */
  function idleBeat(m, dt, spin) {
    if (m.idleT > 0) {
      m.idleT -= dt;
      m.vx *= 0.7; m.vy *= 0.7;
      if (spin !== false) { m.aim += dt * m.idleDir * rnd(0.6, 1.4); m.face = Math.cos(m.aim) < 0 ? -1 : 1; }
      return true;
    }
    if (roll(dt * 0.07 * (1.45 - m.pers.focus) * (m.mood || 1))) {
      m.idleT = rnd(0.35, 1.5); m.idleDir = Math.random() < 0.5 ? 1 : -1;
      if (roll(0.22)) say(m, ['음...', '이쪽인가', '조용하네', '...'][(Math.random() * 4) | 0]);
      return true;
    }
    return false;
  }

  /* 하던 일을 하면서도 눈앞의 적에겐 한두 발 쏜다 — 항상은 아니다 */
  function potshot(m, dt) {
    if (m.reloadLeft > 0 || m.gunCd > 0 || m.digging) return;
    if (!roll(dt * 1.5 * m.pers.aggression * (m.mood || 1))) return;
    const near = enemiesNear(m, Math.min(m.kit.range, 7.5), true);
    if (!near.length) return;
    aimTo(m, near[0].e.x, near[0].e.y);
    fire(m, near[0].e.x, near[0].e.y);
  }

  /* ══════════════════════════════════════════════════════════════
     드릴러 — 기반암 균열 (§0.5-1 "단단한 암반 돌파")
     사람의 infDrillerPressure 는 INF.roleId 게이트가 걸려 있고 사람 경험치로
     정산되므로 그대로 쓸 수 없다(§9.6.7 밸런스 분리). 같은 감각의 AI 전용
     크랙 맵을 따로 굴린다. 외곽 지지층은 사람과 같이 굴착 금지.
     ══════════════════════════════════════════════════════════════ */
  AI.cracks = new Map();
  function crackNeed(type) {
    const depth = (typeof INF !== 'undefined' && INF.depth) || 1;
    return (type === 'core' ? 24 : 15) * (1 + Math.max(0, depth - 1) * 0.22);
  }
  function bedrockAt(c, r) {
    if (c < 1 || r < 1 || c >= COLS - 1 || r >= ROWS - 1) return false;
    const t = G.cell[idx(c, r)];
    return !!t && unbreakable(t);
  }
  function breakBedrock(m, c, r, type) {
    const k = idx(c, r);
    if (!G.cell[k]) return;
    G.cell[k] = null; G.hp.delete(k); if (G.vib) G.vib.delete(k);
    G.nBlk++; G.compDirty = true;
    if (inf()) INF.totalBlocks++;        /* 장악도(floorBroken)에는 넣지 않는다 — 사람 규칙과 같다 */
    AI.cracks.delete(k);
    if (typeof LOS !== 'undefined' && LOS.markDirty) LOS.markDirty();
    J.ring(cxw(c), cyw(r), '#FFF3D6', 10, CELL * 1.5, 3.4);
    J.burst(cxw(c), cyw(r), 18, ['#C8B8E8', '#FFD36E', '#FFF3D6'], 240);
    SFX.brk && SFX.brk();
    const T = XP();
    awardXp(m, type === 'core' ? (T.crackCore || 12) : (T.crack || 8), 'dig', { x: cxw(c), y: cyw(r) });
    say(m, type === 'core' ? '코어 관통' : '기반암 관통');
  }
  function crackPressure(m, c, r, dt, boost) {
    if (!bedrockAt(c, r)) return false;
    const k = idx(c, r), type = G.cell[k];
    const cur = AI.cracks.get(k) || { p: 0, last: G.t, type: type, stage: 0, owner: m.id };
    cur.p = Math.min(1, cur.p + dt * m.kit.digMul * 1.15 * (boost ? 2.3 : 1) / crackNeed(type));
    cur.last = G.t; cur.owner = m.id;
    AI.cracks.set(k, cur);
    m.digging = true; m.drill = 1;
    const stage = Math.min(4, Math.floor(cur.p * 4));
    if (stage > cur.stage) {
      cur.stage = stage;
      J.text(cxw(c), cyw(r) - 26, '균열 ' + Math.floor(cur.p * 100) + '%', '#FFD36E', 13);
      J.ring(cxw(c), cyw(r), '#FFD36E', 4 + stage * 2, CELL * (0.45 + stage * 0.16), 2.2);
    }
    if (roll(dt * 6)) {
      J.chunks(cxw(c), cyw(r), 2, ['#C8B8E8', '#FFD36E', '#FFF3D6'], 105, Math.cos(m.aim), Math.sin(m.aim));
      SFX.dig && SFX.dig();
    }
    if (cur.p >= 1) breakBedrock(m, c, r, type);
    return true;
  }
  /* 손을 뗀 균열은 서서히 닫힌다 — 사람의 drillerCrackDecay 와 같은 취지 */
  function updateCracks(dt) {
    if (!AI.cracks.size) return;
    for (const [k, v] of AI.cracks) {
      if (!G.cell[k] || !unbreakable(G.cell[k])) { AI.cracks.delete(k); continue; }
      if (G.t - v.last < 0.25) continue;
      v.p -= dt * 0.016;
      if (v.p <= 0) AI.cracks.delete(k);
    }
  }
  /* 기반암 목표 — "뚫으면 새 공간이 열리는" 벽만 고른다. 아무 기반암이나
     비비면 시간만 버린다. 이미 파둔 균열과 미탐색 방향을 선호하되 편차를 준다. */
  function pickBedrock(m, radius) {
    if (typeof compOf !== 'function' || !G.comp) return null;
    const comp = compOf(m.x, m.y);
    if (comp < 0) return null;
    const oc = Math.floor(m.x / CELL), or_ = Math.floor(m.y / CELL), R = radius || 7;
    let best = null, bs = -1e9;
    for (let dr = -R; dr <= R; dr++) for (let dc = -R; dc <= R; dc++) {
      const c = oc + dc, r = or_ + dr;
      if (!bedrockAt(c, r)) continue;
      let touch = false;
      for (let i = 0; i < 4; i++) {
        const nc = c + (i === 0 ? 1 : i === 1 ? -1 : 0), nr = r + (i === 2 ? 1 : i === 3 ? -1 : 0);
        if (nc < 1 || nr < 1 || nc >= COLS - 1 || nr >= ROWS - 1) continue;
        const nk = idx(nc, nr);
        if (!G.cell[nk] && G.comp[nk] === comp) { touch = true; break; }
      }
      if (!touch) continue;
      const cur = AI.cracks.get(idx(c, r));
      const s = (cur ? cur.p * 26 : 0)
        + (seenAt(cxw(c), cyw(r)) ? 0 : 12)
        + (G.cell[idx(c, r)] === 'core' ? -9 : 0)
        - Math.hypot(dc, dr) * 2.2 + rnd(-5, 5);
      if (s > bs) { bs = s; best = { c: c, r: r, x: cxw(c), y: cyw(r), type: G.cell[idx(c, r)] }; }
    }
    return best;
  }

  /* ══ 드릴러 — 돌파 파기 (사람 Q 와 같은 감각: 0.55초 창, 전방 3칸) ══ */
  function startBreach(m) {
    m.breachT = 0.55;
    m.breachCd = (m.kit.breachCd || 8) * rnd(0.85, 1.35);
    J.kick(6, Math.cos(m.aim), Math.sin(m.aim));
    J.ring(m.x, m.y, '#FFD36E', 8, CELL * 1.25, 2.6);
    SFX.brk && SFX.brk();
    say(m, '돌파!');
  }
  function updateBreach(m, dt) {
    if (!(m.breachT > 0)) return;
    m.breachT = Math.max(0, m.breachT - dt);
    const ca = Math.cos(m.aim), sa = Math.sin(m.aim), seen = {};
    m.digging = true; m.drill = 1;
    for (let len = CELL * 0.5; len <= CELL * 3.1; len += CELL * 0.5) {
      const cr = toCell(m.x + ca * len, m.y + sa * len), c = cr[0], r = cr[1];
      if (c < 1 || r < 1 || c >= COLS - 1 || r >= ROWS - 1) continue;
      const k = idx(c, r);
      if (seen[k]) continue;
      seen[k] = 1;
      const t = G.cell[k];
      if (!t) continue;
      if (unbreakable(t)) { crackPressure(m, c, r, dt * 2.2, true); continue; }
      AI.breakSrc = m;
      try { damage(c, r, shelDps() * DRILL_DMG() * m.kit.digMul * 2.8 * dt, ca, sa, true); }
      finally { AI.breakSrc = null; }
    }
  }

  /* ══ 거너 — 파쇄탄 공격 표적 (사람 거너의 주무기다) ══ */
  function breakerCluster(m, foes) {
    if (!foes || !foes.length) return null;
    let best = null, bs = 4;
    for (const f of foes) {
      const e = f.e, d = Math.hypot(e.x - m.x, e.y - m.y);
      if (d > CELL * 6.4 || d < CELL * 2.0) continue;      /* 너무 가까우면 자기도 맞는다 */
      let n = 0;
      for (const o of G.enemies) if (o.hp > 0 && Math.hypot(o.x - e.x, o.y - e.y) < CELL * 1.7) n++;
      const s = n * 10 + (e.apex ? 16 : e.elite ? 9 : 0) - d / CELL + rnd(-3, 3);
      if (s > bs) { bs = s; best = e; }
    }
    if (!best) return null;
    const cr = toCell(best.x, best.y);
    return { c: cr[0], r: cr[1], e: best };
  }

  /* ══ 거너 — 통로 경계 자리 (전에는 리더 옆에서 조준만 돌렸다) ══ */
  function pickWatchPost(m) {
    const from = m.lastFoeDir == null ? rnd(0, Math.PI * 2) : m.lastFoeDir;
    let best = null, bs = -1e9;
    for (let i = 0; i < 10; i++) {
      const a = from + rnd(-1.1, 1.1) + (i > 6 ? rnd(-Math.PI, Math.PI) : 0);
      const d = CELL * rnd(1.8, 4.6);
      const x = G.sh.x + Math.cos(a) * d, y = G.sh.y + Math.sin(a) * d;
      if (typeof solidAt === 'function' && solidAt(x, y)) continue;
      let open = 0;
      for (let j = 0; j < 8; j++) {
        const aa = (j / 8) * Math.PI * 2;
        if (!(typeof solidAt === 'function' && solidAt(x + Math.cos(aa) * CELL * 1.2, y + Math.sin(aa) * CELL * 1.2))) open++;
      }
      const s = open * 2.2 + (canSee(x, y, G.sh.x, G.sh.y) ? 4 : 0)
        - Math.hypot(x - m.x, y - m.y) / CELL + rnd(-3, 3);
      if (s > bs) { bs = s; best = { x: x, y: y }; }
    }
    return best;
  }

  /* ══ 스카우트 — 플레어 투척 (던지는 손도 정확하지 않다) ══ */
  function throwFlare(m, tx, ty, label) {
    const a = Math.atan2(ty - m.y, tx - m.x) + rnd(-0.18, 0.18);
    const reach = CELL * rnd(2.2, 3.0);
    const fx = m.x + Math.cos(a) * reach, fy = m.y + Math.sin(a) * reach;
    G.lamps.push({ c: 0, r: 0, x: fx, y: fy, ph: Math.random() * 6,
      rad: Math.max(DEMO.lampRadius * 1.45, CELL * 4.2), ttl: 22, flare: 1, visionRange: 5 });
    if (typeof LOS !== 'undefined' && LOS.markDirty) LOS.markDirty();
    J.flash(fx, fy, 42, 'rgba(255,220,160,.9)');
    J.ring(fx, fy, '#FFD080', 6, CELL * 1.6, 3);
    m.flareCd = m.kit.flareCd * rnd(0.85, 1.35);
    aimTo(m, fx, fy);
    reconAward(m, fx, fy);               /* 정찰 — 새 구역을 밝힐 때만 (자기가속 방지) */
    say(m, label || '플레어');
  }

  /* ══ 스카우트 — 정찰 펄스: 벽 너머 광맥·위협 표시 (§9.1) ══ */
  AI.marks = [];
  function scoutPulse(m) {
    const R = 5, cr = toCell(m.x, m.y), cc = cr[0], rr = cr[1];
    let veins = 0, foes = 0;
    for (let dr = -R; dr <= R; dr++) for (let dc = -R; dc <= R; dc++) {
      const c = cc + dc, r = rr + dr;
      if (c < 0 || r < 0 || c >= COLS || r >= ROWS || Math.hypot(dc, dr) > R + 0.15) continue;
      const t = G.cell[idx(c, r)];
      if (t === 'ore' || t === 'gem' || t === 'crys') {
        AI.marks.push({ x: cxw(c), y: cyw(r), kind: 'vein', ttl: rnd(6, 9) });
        veins++;
      }
    }
    for (const e of G.enemies) {
      if (e.hp <= 0 || Math.hypot(e.x - m.x, e.y - m.y) > CELL * R) continue;
      AI.marks.push({ x: e.x, y: e.y, kind: 'threat', ttl: rnd(4, 6), e: e });
      foes++;
    }
    if (AI.marks.length > 90) AI.marks.splice(0, AI.marks.length - 90);
    m.pulseCd = (m.kit.pulseCd || 9) * rnd(0.85, 1.4);
    J.ring(m.x, m.y, '#7FEBD0', 10, CELL * R, 3.2);
    SFX.cache && SFX.cache();
    reconAward(m, m.x, m.y);
    say(m, veins ? '광맥 ' + veins + '개' : (foes ? '적 ' + foes + '기' : '펄스'));
    return true;
  }
  function updateMarks(dt) {
    for (let i = AI.marks.length - 1; i >= 0; i--) {
      const k = AI.marks[i];
      k.ttl -= dt;
      if (k.kind === 'threat' && k.e) {
        if (k.e.hp <= 0) { AI.marks.splice(i, 1); continue; }
        k.x = k.e.x; k.y = k.e.y;
      }
      if (k.ttl <= 0) AI.marks.splice(i, 1);
    }
  }

  /* ══ 스카우트 — 정찰 목표: 아직 안 밝혀진 쪽을 직접 보러 간다 ══ */
  function pickScoutSpot(m) {
    let best = null, bs = -1e9;
    for (let i = 0; i < 18; i++) {
      const a = rnd(0, Math.PI * 2), d = CELL * rnd(5, 14);
      const x = G.sh.x + Math.cos(a) * d, y = G.sh.y + Math.sin(a) * d;
      if (x < CELL * 1.5 || y < CELL * 1.5 || x > WW - CELL * 1.5 || y > WH - CELL * 1.5) continue;
      if (typeof solidAt === 'function' && solidAt(x, y)) continue;
      const s = (seenAt(x, y) ? 0 : 14) + d / CELL * 0.8 + rnd(-4, 4);
      if (s > bs) { bs = s; best = { x: x, y: y }; }
    }
    return (best && bs > 7) ? best : null;      /* 볼 게 없으면 안 나간다 */
  }

  /* ══ 스카우트 — 그래플 훅 (사람 E): 열린 직선이 길면 당겨 간다 ══ */
  function tryGrapple(m, tx, ty) {
    if (m.dash || m.grappleCd > 0) return false;
    const a = Math.atan2(ty - m.y, tx - m.x), dx = Math.cos(a), dy = Math.sin(a);
    let reach = 0;
    for (let d = 12; d <= CELL * 5; d += 8) {
      if (typeof solidAt === 'function' && solidAt(m.x + dx * d, m.y + dy * d)) break;
      reach = d;
    }
    if (reach < CELL * 2.2) return false;
    const dur = 0.22;
    m.dash = { vx: dx * reach / dur, vy: dy * reach / dur, t: dur };
    m.dashCd = Math.max(m.dashCd, 0.4);
    m.grappleCd = (m.kit.grappleCd || 6) * rnd(0.8, 1.5);
    J.ring(m.x + dx * reach, m.y + dy * reach, '#7FEBD0', 5, CELL * 0.5, 2.2);
    SFX.dash && SFX.dash();
    say(m, '그래플');
    return true;
  }

  /* ══ 엔지니어 — 센트리 자리: 사격선이 트인 곳. 다만 항상 최적을 고르진 않는다 ══ */
  function pickTurretSpot(m, fx, fy) {
    const cands = [];
    for (let i = 0; i < 14; i++) {
      const a = rnd(0, Math.PI * 2), d = CELL * rnd(0.7, 2.8);
      const x = m.x + Math.cos(a) * d, y = m.y + Math.sin(a) * d;
      if (typeof solidAt === 'function' && solidAt(x, y)) continue;
      let open = 0;
      for (let j = 0; j < 10; j++) {
        const aa = (j / 10) * Math.PI * 2;
        if (canSee(x, y, x + Math.cos(aa) * CELL * 4, y + Math.sin(aa) * CELL * 4)) open++;
      }
      let s = open * 1.8 + rnd(-4, 4);
      if (AI.nodes.some((n) => n.owner === m.id && Math.hypot(n.x - x, n.y - y) <= CELL * n.radius)) s += 8;
      if (fx != null) s += canSee(x, y, fx, fy) ? 12 : -6;
      for (const t of AI.turrets) if (Math.hypot(t.x - x, t.y - y) < CELL * 1.6) s -= 7;
      cands.push({ x: x, y: y, s: s });
    }
    if (!cands.length) return null;
    cands.sort((a, b) => b.s - a.s);
    /* 상위 후보 중에서 흔들어 뽑는다 — 가끔은 두 번째·세 번째 자리에 세운다 */
    const i = Math.random() < 0.62 ? 0 : (Math.random() < 0.6 ? 1 : 2);
    return cands[Math.min(i, cands.length - 1)];
  }

'''

# ══════════════════════════════════════════════════════════════════════
#  2) combatSupport — 확률 게이트 + 거너 파쇄탄 공격
# ══════════════════════════════════════════════════════════════════════

COMBAT_SUPPORT = r'''  function combatSupport(m, goal, dt) {
    const kit = m.kit, e = goal.enemy;
    const fx = e ? e.x : goal.x, fy = e ? e.y : goal.y;

    if (m.roleId === 'engineer') {
      const mine = AI.turrets.filter((t) => t.owner === m.id);
      /* 교전 중 센트리 — 쿨이 돌면 무조건 세우는 게 아니라, 현장을 못 덮고 있을 때
         더 적극적으로 세운다. 이미 잘 덮고 있으면 그냥 같이 쏜다. */
      const covers = mine.some((t) => Math.hypot(t.x - fx, t.y - fy) < CELL * (kit.turretRange + 1.5));
      const want = mine.length < kit.maxTurrets ? 0.8 : (covers ? 0.12 : 0.6);
      if (intent(m, 'turretFight', m.turretCd <= 0, { min: 0.35, max: 2.4, p: want })) {
        placeTurret(m, pickTurretSpot(m, fx, fy));
        return;
      }
      const dead = mine.find((t) => !t.powered);
      const canFeed = !!dead && m.nodeCd <= 0 && Math.hypot(dead.x - m.x, dead.y - m.y) < CELL * kit.nodeRadius * 1.2;
      if (intent(m, 'nodeFight', canFeed, { min: 0.3, max: 1.8, p: 0.82 })) { placeNode(m); return; }
    }

    if (m.roleId === 'scout') {
      /* 교전 구역 조명 — 어두우면 밝힌다. 다만 즉시 던지지는 않는다 */
      if (intent(m, 'flareFight', m.flareCd <= 0 && !litAt(fx, fy), { min: 0.25, max: 1.9, p: 0.8 })) {
        throwFlare(m, fx, fy, '조명 지원');
        return;
      }
      /* 교전 중 펄스 — 여럿이 몰려오면 위치를 훑어 준다 */
      if (intent(m, 'pulseFight', m.pulseCd <= 0, { min: 0.6, max: 3.5, p: 0.4 })) { scoutPulse(m); return; }
    }

    if (m.roleId === 'gunner') {
      const d = Math.hypot(fx - m.x, fy - m.y);
      const hurt = m.hp < m.hpMax * (0.42 + 0.2 * m.pers.caution);
      if (intent(m, 'shield', m.qCd <= 0 && (d < CELL * 3.2 || hurt), { min: 0.1, max: 1.2, p: hurt ? 0.9 : 0.5 })) {
        m.shieldT = 2.8; m.qCd = 8 * rnd(0.9, 1.25);
        J.ring(m.x, m.y, '#7FEBD0', 8, CELL * 1.4, 3);
        say(m, '방어막');
        return;
      }
      /* 벽 뒤의 적 — 차폐 제거 */
      if (m.breakerCd <= 0 && !canSee(m.x, m.y, fx, fy)
          && intent(m, 'breakerCover', true, { min: 0.2, max: 1.6, p: 0.72 })) {
        const cr = toCell((m.x + fx) / 2, (m.y + fy) / 2);
        const t = cellOf(cr[0], cr[1]);
        if (t && !unbreakable(t)) { fireBreaker(m, cr[0], cr[1]); say(m, '차폐 제거'); return; }
      }
      /* 파쇄탄 공격 — 뭉친 적·정예에 붙인다. 지금까지 AI 거너는 파쇄탄을
         벽에만 썼다(사람 거너에게는 이게 주무기다) */
      if (m.breakerCd <= 0 && kit.breakerAtk) {
        const tgt = breakerCluster(m, threats(m));
        if (tgt && intent(m, 'breakerAtk', true, { min: 0.25, max: 2.0, p: 0.7 })) {
          if (fireBreaker(m, tgt.c, tgt.r)) { say(m, '파쇄탄'); return; }
        }
      }
    }
  }'''

# ══════════════════════════════════════════════════════════════════════
#  3) updateBreakers — 조기 기폭 분리
# ══════════════════════════════════════════════════════════════════════

BREAKERS = r'''  function detonateBreaker(m, b, early) {
    J.ring(b.x, b.y, '#ff8d72', 12, CELL * 1.8, 4);
    J.burst(b.x, b.y, 24, ['#ff6f45', '#ffd36e', '#fff3d6'], 300);
    SFX.brk && SFX.brk();
    if (early) J.text(b.x, b.y - 30, '조기 기폭', '#ffd36e', 15);
    const rad = m.kit.breakerRadius;
    AI.breakSrc = m;
    try {
      for (let dr = -rad; dr <= rad; dr++) for (let dc = -rad; dc <= rad; dc++) {
        const c = b.c + dc, rr = b.r + dr;
        const t = cellOf(c, rr);
        if (!t || unbreakable(t)) continue;
        damage(c, rr, ((HPT[t] || 100) * (inf() ? INF.wallHpMul || 1 : 1)) * 1.15, dc || 1, dr || 0, true);
      }
    } finally { AI.breakSrc = null; }
    const eRad = CELL * (1.8 + (rad - 1) * 0.65);
    for (const e of G.enemies) {
      if (e.hp <= 0) continue;
      const d = Math.hypot(e.x - b.x, e.y - b.y);
      if (d >= eRad) continue;
      const fall = 1 - d / eRad;
      AI.dmgSrc = m;
      try {
        hurtEnemy(e, DEMO.enemyGunDmg * 2.2 * m.kit.gunMul * (0.55 + 0.75 * fall) * (early ? 1.25 : 1),
          (e.x - b.x) / (d || 1), (e.y - b.y) / (d || 1));
      } finally { AI.dmgSrc = null; }
    }
  }

  function updateBreakers(m, dt) {
    m.breakerCd = Math.max(0, m.breakerCd - dt);
    for (let i = m.breaker.length - 1; i >= 0; i--) {
      const b = m.breaker[i];
      b.t -= dt;
      if (b.t > 0) {
        /* 조기 기폭(사람 E) — 신관이 남았어도 폭심에 적이 들어오면 지금 터뜨릴 수 있다.
           매번 완벽하게 맞추지는 않는다 — 늦게 누르거나, 이번 발은 그냥 기다린다 */
        if (b.t < 1.7 && !b.noEarly) {
          let hot = 0;
          for (const e of G.enemies) {
            if (e.hp > 0 && Math.hypot(e.x - b.x, e.y - b.y) < CELL * (1.5 + m.kit.breakerRadius * 0.6)) hot++;
          }
          if (hot > 0 && intent(m, 'earlyDet', true, { min: 0.05, max: 0.55, p: 0.5 + 0.12 * hot })) {
            m.breaker.splice(i, 1);
            detonateBreaker(m, b, true);
            say(m, '조기 기폭');
            continue;
          }
          if (hot === 0 && roll(dt * 0.4)) b.noEarly = true;
        }
        continue;
      }
      m.breaker.splice(i, 1);
      detonateBreaker(m, b, false);
    }
  }'''

# ══════════════════════════════════════════════════════════════════════
#  4) act() 새 목표 분기
# ══════════════════════════════════════════════════════════════════════

ACT_BRANCHES = r'''    if (goal.kind === 'crack') {
      /* 기반암 균열 — 붙어서 압력을 넣는다. 오래 걸리면 지겨워져 손을 뗀다 */
      if (!GEO.inReach(m.x, m.y, goal.c, goal.r)) { followStep(m, goal, dt); return; }
      steer(m, goal.x, goal.y, dt, 0.35);
      aimTo(m, goal.x, goal.y);
      crackPressure(m, goal.c, goal.r, dt, false);
      if (m.breachCd <= 0 && intent(m, 'breachRock', true, { min: 0.8, max: 4, p: 0.62 })) startBreach(m);
      m.crackT = (m.crackT || 0) + dt;
      if (m.crackT > m.crackPatience || roll(dt * 0.02)) {
        m.crackT = 0; m.crackPatience = rnd(9, 20);
        m.crackTarget = null; m.goal = null;
        if (roll(0.35)) say(m, '이건 나중에');
      }
      return;
    }

    if (goal.kind === 'pulse') { scoutPulse(m); m.goal = null; return; }

    if (goal.kind === 'scout') {
      const d = Math.hypot(goal.x - m.x, goal.y - m.y);
      /* 먼 거리는 그래플로 당겨 간다 — 사람 스카우트의 E */
      if (d > CELL * 3 && m.kit.grappleCd
          && intent(m, 'grapple', m.grappleCd <= 0, { min: 0.3, max: 2.4, p: 0.55 })) tryGrapple(m, goal.x, goal.y);
      if (d > CELL * 1.3) { followStep(m, goal, dt); potshot(m, dt); return; }
      /* 도착 — 둘러보고, 가끔 펄스까지 돌린다 */
      reconAward(m, m.x, m.y);
      if (m.pulseCd <= 0 && roll(0.45)) scoutPulse(m);
      else if (roll(0.3)) say(m, ['여긴 비었어', '길 있다', '기록해 둔다'][(Math.random() * 3) | 0]);
      m.goal = null; m.vx *= 0.7; m.vy *= 0.7;
      return;
    }

    if (goal.kind === 'watch') {
      const d = Math.hypot(goal.x - m.x, goal.y - m.y);
      m.watchT -= dt;
      if (d > CELL * 0.9) { followStep(m, goal, dt); potshot(m, dt); return; }
      m.vx *= 0.86; m.vy *= 0.86;
      const near = enemiesNear(m, Math.min(m.kit.range, 9), true);
      if (near.length) { aimTo(m, near[0].e.x, near[0].e.y); fire(m, near[0].e.x, near[0].e.y); return; }
      if (idleBeat(m, dt, false)) return;
      /* 시선을 훑는다 — 일정 속도로 회전하지 않고 멈췄다 돌린다 */
      if (m.sweepT == null || m.sweepT <= 0) { m.sweepT = rnd(0.5, 2.2); m.sweepA = m.aim + rnd(-1.5, 1.5); }
      m.sweepT -= dt;
      aimTo(m, m.x + Math.cos(m.sweepA) * CELL * 3, m.y + Math.sin(m.sweepA) * CELL * 3);
      return;
    }

'''

# ══════════════════════════════════════════════════════════════════════
#  5) 관전 리더 — 역할 스킬
# ══════════════════════════════════════════════════════════════════════

LEADER_SKILLS = r'''  /* ══════════════════════════════════════════════════════════════
     AI_HUMANIZE_V1 — 관전 리더의 역할 스킬
     리더 오토파일럿은 지금까지 Q·E 를 한 번도 누르지 않았다. 역할이 영향을
     주는 값은 경로 굴착 비용과 교전 거리 둘뿐이어서, 어느 직업이든 "이동 +
     드릴 + 사격"만 하는 사람으로 보였다.

     리더는 사람 캐릭터를 그대로 조종하므로 본편 crewSkillQ/E 를 부르면 된다
     (기반암 균열도 드릴 입력만 물리면 본편 로직이 알아서 돌아간다).
     쿨마다 정확히 누르지 않도록 크루와 같은 의도 게이트를 쓴다.
     ══════════════════════════════════════════════════════════════ */
  const LPERS = { eager: 1, hesitate: 1 };
  function rollLeaderPersona() {
    LPERS.eager = 0.7 + Math.random() * 0.75;
    LPERS.hesitate = 0.7 + Math.random() * 0.9;
    OBS.wait = {};
  }
  /* 크루의 intent 와 같은 규칙 — 대기는 게임 시간(G.t) 절대 시각으로 잡는다 */
  function lIntent(tag, ready, min, max, p) {
    const W = OBS.wait || (OBS.wait = {});
    if (!ready) { W[tag] = -1; return false; }
    const wait = () => (min + Math.random() * (max - min)) * LPERS.hesitate / LPERS.eager;
    if (W[tag] == null || W[tag] < 0) { W[tag] = G.t + wait(); return false; }
    if (G.t < W[tag]) return false;
    if (Math.random() < p) { W[tag] = -1; return true; }
    W[tag] = G.t + wait() * (1.4 + Math.random() * 1.6);
    return false;
  }
  /* 준비 조건이 프레임마다 뒤집히는 스킬(드릴 접촉 여부·근접 여부)은 쿨다운으로만
     장전하고, 실제 조건이 아직 아니면 짧게 다시 뜸을 들인다. 조건을 lIntent 의
     ready 에 그대로 넣으면 한 프레임 꺼질 때마다 망설임이 처음부터 다시 시작돼
     영원히 안 쓴다 — `OBS.drillCell` 은 매 프레임 초기화되므로 실제로 그랬다. */
  function lFire(tag, cdReady, when, min, max, p, fn) {
    if (!lIntent(tag, cdReady, min, max, p)) return false;
    if (!when) { OBS.wait[tag] = G.t + 0.25; return false; }   /* 장전 유지 — 조건이 서면 곧 쓴다 */
    fn();
    return true;
  }
  function foesWithin(cells) {
    const out = [];
    if (!G.enemies) return out;
    for (const e of G.enemies) if (e.hp > 0 && Math.hypot(e.x - G.sh.x, e.y - G.sh.y) < CELL * cells) out.push(e);
    return out;
  }
  function litNear(x, y) {
    if (!G.lamps) return false;
    for (const l of G.lamps) if (Math.hypot(l.x - x, l.y - y) < (l.rad || 120) * 0.8) return true;
    return false;
  }
  function leaderSkills(dt) {
    OBS._dt = dt;
    if (typeof CREW === 'undefined' || CREW.qCd == null) return;
    const role = INF.roleId;
    const hurt = (G.phpMax ? G.php / G.phpMax : 1) < 0.55;

    const hasQ = typeof crewSkillQ === 'function', hasE = typeof crewSkillE === 'function';

    if (role === 'driller') {
      /* 돌파 파기 — 벽에 드릴을 물린 상태에서만 의미가 있다 */
      lFire('q', hasQ && CREW.qCd <= 0, !!OBS.drillCell, 0.5, 3.4, 0.6, crewSkillQ);
      return;
    }
    if (role === 'scout') {
      const ahead = { x: G.sh.x + Math.cos(G.sh.aim) * CELL * 3, y: G.sh.y + Math.sin(G.sh.aim) * CELL * 3 };
      const dark = !litNear(ahead.x, ahead.y);
      lFire('q', hasQ && CREW.qCd <= 0, dark || foesWithin(7).length > 0, 0.6, 3.6, 0.62, crewSkillQ);
      const g = OBS.goal;
      const far = !!(g && g.x != null && Math.hypot(g.x - G.sh.x, g.y - G.sh.y) > CELL * 4);
      lFire('e', hasE && CREW.eCd <= 0, far && !(G.dash && G.dash.active), 1.2, 5.5, 0.45, crewSkillE);
      return;
    }
    if (role === 'engineer') {
      /* 노드·센트리는 어디서든 세울 수 있다 — 쿨다운만 조건이다 */
      const hot = foesWithin(8).length > 0;
      if (lIntent('e', hasE && CREW.eCd <= 0, 0.8, 5.0, hot ? 0.75 : 0.5)) crewSkillE();
      if (lIntent('q', hasQ && CREW.qCd <= 0, 1.0, 5.5, hot ? 0.7 : 0.5)) crewSkillQ();
      return;
    }
    if (role === 'gunner') {
      const close = foesWithin(3.2).length > 0;
      lFire('q', hasQ && CREW.qCd <= 0, close || hurt, 0.15, 1.4, hurt ? 0.85 : 0.52, crewSkillQ);
      /* 조기 기폭 — 붙여둔 파쇄탄 폭심에 적이 있을 때만 */
      let armed = false;
      for (const ch of (INF.breakerCharges || [])) {
        if (!ch.stuck) continue;
        for (const e of G.enemies) {
          if (e.hp > 0 && Math.hypot(e.x - ch.x, e.y - ch.y) < CELL * 2.2) { armed = true; break; }
        }
        if (armed) break;
      }
      lFire('e', hasE && CREW.eCd <= 0, armed, 0.1, 0.9, 0.7, crewSkillE);
    }
  }

  /* 리더 드릴러의 기반암 — 사람은 드릴을 물리면 균열이 쌓인다. 오토파일럿은
     기반암을 아예 후보에서 뺐기 때문에 사람 드릴러의 본업을 못 했다. */
  function pickLeaderBedrock() {
    if (INF.roleId !== 'driller') return null;
    if (typeof compOf !== 'function' || !G.comp) return null;
    const comp = compOf(G.sh.x, G.sh.y);
    if (comp < 0) return null;
    const oc = Math.floor(G.sh.x / CELL), or_ = Math.floor(G.sh.y / CELL), R = 6;
    let best = null, bs = -1e9;
    for (let dr = -R; dr <= R; dr++) for (let dc = -R; dc <= R; dc++) {
      const c = oc + dc, r = or_ + dr;
      if (c < 1 || r < 1 || c >= COLS - 1 || r >= ROWS - 1) continue;
      const t = G.cell[c + r * COLS];
      if (!t || !(typeof SOLIDX === 'function' ? SOLIDX(t) : (t === 'rock' || t === 'core'))) continue;
      let touch = false;
      for (let i = 0; i < 4; i++) {
        const nc = c + (i === 0 ? 1 : i === 1 ? -1 : 0), nr = r + (i === 2 ? 1 : i === 3 ? -1 : 0);
        const nk = nc + nr * COLS;
        if (!G.cell[nk] && G.comp[nk] === comp) { touch = true; break; }
      }
      if (!touch) continue;
      const crack = (INF.drillerCracks && INF.drillerCracks.get) ? INF.drillerCracks.get(c + r * COLS) : null;
      const s = (crack ? crack.p * 26 : 0) + (t === 'core' ? -9 : 0)
        - Math.hypot(dc, dr) * 2.4 + (Math.random() * 10 - 5);
      if (s > bs) { bs = s; best = { c: c, r: r, x: cxw(c), y: cyw(r) }; }
    }
    return best;
  }

'''


def main(argv):
    args = [a for a in argv[1:] if not a.startswith('--')]
    out_name = DEFAULT_OUT
    for i, a in enumerate(argv[1:]):
        if a == '--out' and i + 2 <= len(argv) - 1:
            out_name = argv[i + 2]
    base_name = args[0] if args and args[0] != out_name else DEFAULT_BASE
    base = ROOT / base_name
    out = ROOT / out_name
    if not base.exists():
        raise Fail('베이스 파일이 없다: %s' % base)

    s = base.read_text(encoding='utf-8')
    if 'AI_HUMANIZE_V1' in s:
        raise Fail('베이스에 이미 AI_HUMANIZE_V1 이 들어 있다 — 원본 베이스를 지정하라')

    # ── 1) 휴먼화 코어 삽입 ─────────────────────────────────────────
    s = patch(s, 'humanize-core',
              '  /* ══════════ 편성 ══════════ */',
              HUMANIZE + '  /* ══════════ 편성 ══════════ */')

    # ── 2) KIT — 새 직업 행동 예산 ──────────────────────────────────
    s = patch(s, 'kit-driller',
              "      drillMelee: true,      /* 드릴 팁 접촉 피해 */",
              "      drillMelee: true,      /* 드릴 팁 접촉 피해 */\n"
              "      crack: true,           /* 기반암 균열 — 사람 드릴러의 본업 (§0.5-1) */\n"
              "      breachCd: 8,           /* 돌파 파기 — 전방 3칸 관통 (사람 Q) */")
    s = patch(s, 'kit-gunner',
              "      breakerCd: 9, breakerRadius: 1,",
              "      breakerCd: 9, breakerRadius: 1,\n"
              "      breakerAtk: true,      /* 파쇄탄을 적에게도 쓴다 (사람 거너의 주무기) */")
    s = patch(s, 'kit-scout',
              "      flareCd: 7,            /* 어두운 전방에 플레어 */",
              "      flareCd: 7,            /* 어두운 전방에 플레어 */\n"
              "      pulseCd: 9,            /* 정찰 펄스 — 벽 너머 광맥·위협 (§9.1) */\n"
              "      grappleCd: 6,          /* 그래플 훅 (사람 E) */\n"
              "      exploreCd: 11,         /* 선행 정찰 — 본업 */")

    # ── 3) spawnMember — 성향 부여 ─────────────────────────────────
    s = patch(s, 'spawn-persona',
              "      lootCd: 0, lootGot: 0, dodging: false,",
              "      lootCd: 0, lootGot: 0, dodging: false,\n"
              "      /* AI_HUMANIZE_V1 — 이 크루의 성향. 같은 직업이어도 판마다 다른 사람이 앉는다 */\n"
              "      pers: rollPersona(roleId), wait: {}, mood: 1, moodT: rnd(2, 9),\n"
              "      breachT: 0, breachCd: rnd(1, 5), pulseCd: rnd(1, 6), grappleCd: rnd(0, 3), exploreCd: rnd(2, 9),\n"
              "      crackTarget: null, crackT: 0, crackPatience: rnd(9, 20),\n"
              "      watch: null, watchT: 0, sweepT: 0, sweepA: 0, farJit: 0,\n"
              "      idleT: 0, idleDir: 1, strafeT: 0, lastFoeDir: null, _dt: 0.016,")

    # ── 4) 프레임 갱신 ─────────────────────────────────────────────
    s = patch(s, 'update-cooldowns',
              "      m.lootCd = Math.max(0, m.lootCd - dt);",
              "      m.lootCd = Math.max(0, m.lootCd - dt);\n"
              "      /* AI_HUMANIZE_V1 — 성향·기분·새 직업 행동 쿨다운 */\n"
              "      m._dt = dt;\n"
              "      updateMood(m, dt);\n"
              "      m.breachCd = Math.max(0, (m.breachCd || 0) - dt);\n"
              "      m.pulseCd = Math.max(0, (m.pulseCd || 0) - dt);\n"
              "      m.grappleCd = Math.max(0, (m.grappleCd || 0) - dt);\n"
              "      m.exploreCd = Math.max(0, (m.exploreCd || 0) - dt);")
    s = patch(s, 'update-react',
              "      if (m.react <= 0 || !m.goal) { m.goal = decide(m); m.react = 0.12 + Math.random() * 0.1; }",
              "      /* 판단 주기도 사람마다 다르다 — 반응이 빠른 크루와 느린 크루가 섞인다 */\n"
              "      if (m.react <= 0 || !m.goal) { m.goal = decide(m); m.react = m.pers.react * rnd(0.75, 1.4); }\n"
              "      updateBreach(m, dt);                             /* 돌파 파기 창이 열려 있으면 전방을 뚫는다 */")
    s = patch(s, 'update-dash-noise',
              "Math.random() < dt * 0.03)",
              "Math.random() < dt * 0.03 * m.pers.aggression * (m.mood || 1))")
    s = patch(s, 'update-potshot',
              "      applyMotion(m, dt);\n      watchStuck(m, dt);\n    }\n  };",
              "      /* 하던 일 중에도 눈앞의 적에겐 가끔 쏜다 — 교전 목표일 때는 act 가 이미 쏘고 있다 */\n"
              "      if (m.goal && m.goal.kind !== 'fight' && m.goal.kind !== 'escape') potshot(m, dt);\n"
              "      applyMotion(m, dt);\n      watchStuck(m, dt);\n    }\n  };")
    s = patch(s, 'update-installations-tick',
              "  function updateInstallations(dt) {\n    /* 전력 노드 — 반경 안의 센트리에 급전 */",
              "  function updateInstallations(dt) {\n"
              "    updateCracks(dt);                 /* 손을 뗀 기반암 균열은 닫힌다 */\n"
              "    updateMarks(dt);                  /* 정찰 펄스 표식 수명 */\n"
              "    /* 전력 노드 — 반경 안의 센트리에 급전 */")

    # ── 5) 지층 전환 — 균열·표식 초기화 (성향은 유지: 같은 사람이다) ──
    s = patch(s, 'floor-reset',
              "      m.lootCd = 0; m.dodging = false;\n"
              "    }\n"
              "    AI.turrets.length = 0; AI.nodes.length = 0;",
              "      m.lootCd = 0; m.dodging = false;\n"
              "      /* 성향(pers)은 유지한다 — 지층이 바뀌어도 같은 사람이다. 기분만 다시 굴린다 */\n"
              "      m.wait = {}; m.crackTarget = null; m.crackT = 0; m.breachT = 0;\n"
              "      m.watch = null; m.watchT = 0; m.idleT = 0; m.moodT = rnd(1, 6);\n"
              "    }\n"
              "    AI.turrets.length = 0; AI.nodes.length = 0;\n"
              "    AI.cracks.clear(); AI.marks.length = 0;")

    # ── 6) 사격 — 조준 흔들림·직업별 사거리 ─────────────────────────
    s = patch(s, 'fire-aim-error',
              "    const a = Math.atan2(ty - m.y, tx - m.x) + rnd(-0.045, 0.045);",
              "    /* 조준 흔들림은 크루마다 다르다 — 모두가 같은 정확도로 쏘면 기계로 보인다 */\n"
              "    const err = (m.pers ? m.pers.aimErr : 0.045) * (m.digging ? 1.6 : 1);\n"
              "    const a = Math.atan2(ty - m.y, tx - m.x) + rnd(-err, err);")
    s = patch(s, 'fire-range-role',
              "      const inRange = d <= CELL * FIRE_RANGE() && canSee(m.x, m.y, e.x, e.y);",
              "      /* 직업별 사거리를 실제로 쓴다 — KIT.range 는 선언만 되고 안 쓰이고 있었다 */\n"
              "      const inRange = d <= CELL * Math.min(FIRE_RANGE(), m.kit.range) && canSee(m.x, m.y, e.x, e.y);")

    # ── 7) 채굴 목표 — 개인 편차 ───────────────────────────────────
    s = patch(s, 'pickmine-noise',
              "      const ore = t === 'gem' ? 28 : t === 'crys' ? 22 : t === 'ore' ? 16 : t === 'stone' ? 3 : 2;\n"
              "      const s = ore - Math.hypot(c - oc, r - or_) * 2.4;",
              "      /* 광맥 편애도와 흔들림 — 네 명이 같은 벽을 같은 순서로 고르지 않는다 */\n"
              "      const ore = (t === 'gem' ? 28 : t === 'crys' ? 22 : t === 'ore' ? 16 : t === 'stone' ? 3 : 2)\n"
              "        * (m.pers ? m.pers.oreBias : 1);\n"
              "      const s = ore - Math.hypot(c - oc, r - or_) * 2.4 + rnd(-4, 4);")

    # ── 8) 플레어 목표 — 미탐색 우선 ───────────────────────────────
    s = patch(s, 'darkspot-unseen',
              "        const s = d * (lit ? 0.2 : 1);",
              "        /* 조명 유무만 보면 이미 훤히 본 통로에도 던진다 — 미탐색 쪽을 우선한다 */\n"
              "        const s = d * (lit ? 0.2 : 1) * (seenAt(x, y) ? 1 : 1.8) + rnd(-0.6, 0.6);")

    # ── 9) 센트리 — 자리 선정·회수 규칙 ────────────────────────────
    s = patch(s, 'place-turret',
              "  function placeTurret(m) {\n"
              "    const kit = m.kit;\n"
              "    const mine = AI.turrets.filter((t) => t.owner === m.id);\n"
              "    if (mine.length >= kit.maxTurrets) {\n"
              "      const old = mine[0];\n"
              "      AI.turrets.splice(AI.turrets.indexOf(old), 1);\n"
              "      J.text(old.x, old.y - 20, '회수', '#C7A0FF', 12);\n"
              "    }\n"
              "    const p = freeSpotNear(m.x, m.y, 1.2);",
              "  function placeTurret(m, spot) {\n"
              "    const kit = m.kit;\n"
              "    const mine = AI.turrets.filter((t) => t.owner === m.id);\n"
              "    if (mine.length >= kit.maxTurrets) {\n"
              "      /* 슬롯이 찼으면 수명이 가장 적게 남은 것부터 회수한다 (예전엔 배열 순서) */\n"
              "      let old = mine[0];\n"
              "      for (const t of mine) if (t.life < old.life) old = t;\n"
              "      AI.turrets.splice(AI.turrets.indexOf(old), 1);\n"
              "      J.text(old.x, old.y - 20, '회수', '#C7A0FF', 12);\n"
              "    }\n"
              "    const p = spot || pickTurretSpot(m, null) || freeSpotNear(m.x, m.y, 1.2);")

    # ── 10) combatSupport / updateBreakers 교체 ────────────────────
    s = replace_fn(s, 'combat-support', '  function combatSupport(m, goal, dt) {', COMBAT_SUPPORT)
    s = replace_fn(s, 'breakers', '  function updateBreakers(m, dt) {', BREAKERS)

    # ── 11) decide() — 전투 판단 ───────────────────────────────────
    s = patch(s, 'decide-fight',
              "    const foes = threats(m);\n"
              "    if (foes.length) {\n"
              "      const t = foes[0];\n"
              "      /* 파티에서 너무 멀리 떨어진 적까지 쫓아가면 대열이 무너진다 */\n"
              "      const leashOk = t.dp <= CELL * (m.kit.intercept + 4) || t.d <= CELL * m.kit.intercept;\n"
              "      if (leashOk) return { kind: 'fight', x: t.e.x, y: t.e.y, enemy: t.e, label: '교전' };\n"
              "    }",
              "    const foes = threats(m);\n"
              "    if (foes.length) {\n"
              "      m.lastFoeDir = Math.atan2(foes[0].e.y - G.sh.y, foes[0].e.x - G.sh.x);\n"
              "      /* 표적 선택도 완벽하지 않다 — 산만한 크루는 가끔 두 번째 위협을 먼저 잡는다 */\n"
              "      const t = (foes.length > 1 && roll(0.22 * (1.45 - m.pers.focus))) ? foes[1] : foes[0];\n"
              "      /* 요격 거리는 성향을 탄다 — 소극적인 크루는 파티 곁을 지킨다 */\n"
              "      const reach = m.kit.intercept * (0.7 + m.pers.aggression * 0.45) * (m.mood || 1);\n"
              "      const leashOk = t.dp <= CELL * (reach + 4) || t.d <= CELL * reach;\n"
              "      /* 벽을 파는 중이면 멀리 있는 적은 집중력만큼 안 돌아본다 */\n"
              "      const busy = m.goal && (m.goal.kind === 'mine' || m.goal.kind === 'crack')\n"
              "        && t.dp > CELL * 5 && t.d > CELL * 4.5;\n"
              "      if (leashOk && !(busy && roll(m.pers.focus * 0.55))) {\n"
              "        return { kind: 'fight', x: t.e.x, y: t.e.y, enemy: t.e, label: '교전' };\n"
              "      }\n"
              "    }")

    # ── 12) decide() — 직업 고유 임무 ──────────────────────────────
    s = patch(s, 'decide-duties',
              "    if (m.roleId === 'engineer') {\n"
              "      const mine = AI.turrets.filter((t) => t.owner === m.id);\n"
              "      if (m.turretCd <= 0 && mine.length < kit.maxTurrets) return { kind: 'turret', x: m.x, y: m.y, label: '센트리 설치' };\n"
              "      /* 파티가 센트리 라인을 벗어나 전진했으면 진지를 앞으로 옮긴다 —\n"
              "         한 번 세우고 끝내는 엔지니어는 엔지니어가 아니다 */\n"
              "      if (m.turretCd <= 0 && mine.length && mine.every((t) => Math.hypot(t.x - G.sh.x, t.y - G.sh.y) > CELL * 8)) {\n"
              "        return { kind: 'turret', x: m.x, y: m.y, label: '센트리 전진 배치' };\n"
              "      }\n"
              "      const nodes = AI.nodes.filter((n) => n.owner === m.id);\n"
              "      /* 센트리가 급전을 못 받고 있으면 노드부터 세운다 */\n"
              "      const unpowered = mine.find((t) => !t.powered);\n"
              "      if (m.nodeCd <= 0 && (nodes.length < kit.maxNodes || unpowered)) {\n"
              "        const at = unpowered || m;\n"
              "        return { kind: 'node', x: at.x, y: at.y, label: unpowered ? '센트리 급전' : '전력 노드' };\n"
              "      }\n"
              "    }\n"
              "    if (m.roleId === 'scout' && m.flareCd <= 0) {\n"
              "      const dark = darkSpotAhead(m);\n"
              "      if (dark) return { kind: 'flare', x: dark.x, y: dark.y, label: '플레어' };\n"
              "    }",
              "    if (m.roleId === 'engineer') {\n"
              "      const mine = AI.turrets.filter((t) => t.owner === m.id);\n"
              "      if (intent(m, 'turret', m.turretCd <= 0 && mine.length < kit.maxTurrets,\n"
              "          { min: 0.6, max: 4.5, p: 0.72 })) {\n"
              "        return { kind: 'turret', x: m.x, y: m.y, label: '센트리 설치' };\n"
              "      }\n"
              "      /* 파티가 센트리 라인을 벗어났으면 진지를 앞으로 — 다만 곧바로 뽑진 않는다 */\n"
              "      const stale = mine.length && mine.every((t) => Math.hypot(t.x - G.sh.x, t.y - G.sh.y) > CELL * 8);\n"
              "      if (intent(m, 'turretFwd', m.turretCd <= 0 && stale, { min: 1.2, max: 6, p: 0.55 })) {\n"
              "        return { kind: 'turret', x: m.x, y: m.y, label: '센트리 전진 배치' };\n"
              "      }\n"
              "      const nodes = AI.nodes.filter((n) => n.owner === m.id);\n"
              "      const unpowered = mine.find((t) => !t.powered);\n"
              "      if (intent(m, 'node', m.nodeCd <= 0 && (nodes.length < kit.maxNodes || unpowered),\n"
              "          { min: 0.5, max: 4, p: unpowered ? 0.85 : 0.6 })) {\n"
              "        const at = unpowered || m;\n"
              "        return { kind: 'node', x: at.x, y: at.y, label: unpowered ? '센트리 급전' : '전력 노드' };\n"
              "      }\n"
              "    }\n"
              "    if (m.roleId === 'scout') {\n"
              "      /* 정찰 — 스카우트의 본업인데 목표 목록에 아예 없었다.\n"
              "         아직 안 밝혀진 쪽을 직접 보러 갔다가 돌아온다 */\n"
              "      if (intent(m, 'explore', m.exploreCd <= 0, { min: 1.5, max: 7, p: 0.5 * m.pers.curiosity })) {\n"
              "        const spot = pickScoutSpot(m);\n"
              "        if (spot) {\n"
              "          m.exploreCd = (kit.exploreCd || 11) * rnd(0.8, 1.6);\n"
              "          return { kind: 'scout', x: spot.x, y: spot.y, label: '정찰' };\n"
              "        }\n"
              "      }\n"
              "      if (intent(m, 'pulse', m.pulseCd <= 0, { min: 1, max: 6, p: 0.45 * m.pers.curiosity })) {\n"
              "        return { kind: 'pulse', x: m.x, y: m.y, label: '정찰 펄스' };\n"
              "      }\n"
              "      if (m.flareCd <= 0) {\n"
              "        const dark = darkSpotAhead(m);\n"
              "        if (dark && intent(m, 'flare', true, { min: 0.5, max: 3.5, p: 0.65 })) {\n"
              "          return { kind: 'flare', x: dark.x, y: dark.y, label: '플레어' };\n"
              "        }\n"
              "      }\n"
              "    }\n"
              "    if (m.roleId === 'driller' && m.kit.crack) {\n"
              "      /* 기반암 — 사람 드릴러의 본업(§0.5-1). AI 는 지금까지 한 번도 안 팠다.\n"
              "         뚫으면 새 길이 열리는 벽만 고르고, 고르는 것 자체도 확률이다 */\n"
              "      if (!m.crackTarget && intent(m, 'crack', true, { min: 2.5, max: 9, p: 0.5 })) {\n"
              "        const b = pickBedrock(m, 7);\n"
              "        if (b) m.crackTarget = { c: b.c, r: b.r, x: b.x, y: b.y, until: G.t + 24 };\n"
              "      }\n"
              "      if (m.crackTarget) {\n"
              "        const t = cellOf(m.crackTarget.c, m.crackTarget.r);\n"
              "        if (!t || !unbreakable(t) || m.crackTarget.until < G.t) m.crackTarget = null;\n"
              "        else return { kind: 'crack', x: m.crackTarget.x, y: m.crackTarget.y,\n"
              "          c: m.crackTarget.c, r: m.crackTarget.r, label: '기반암 균열' };\n"
              "      }\n"
              "    }")

    # ── 13) decide() — 재화·재장전 ─────────────────────────────────
    s = patch(s, 'decide-loot',
              "      if (q && Math.random() < 0.4) {",
              "      if (q && roll(0.28 * m.pers.greed * (m.mood || 1))) {")
    s = patch(s, 'decide-reload',
              "    if (m.reloadLeft <= 0 && m.ammo < m.mag * 0.45) {\n"
              "      m.reloadLeft = m.reloadTime;\n"
              "      say(m, '재장전');\n"
              "    }",
              "    /* 재장전 — 채우는 시점이 사람마다 다르고, 가끔 그냥 잊는다 */\n"
              "    if (m.reloadLeft <= 0 && m.ammo < m.mag * m.pers.reloadAt\n"
              "        && intent(m, 'reload', true, { min: 0.15, max: 1.6, p: 0.8 })) {\n"
              "      m.reloadLeft = m.reloadTime;\n"
              "      say(m, '재장전');\n"
              "    }")

    # ── 14) decide() — 거너 경계 ───────────────────────────────────
    s = patch(s, 'decide-watch',
              "    if (m.roleId === 'gunner' && m.breakerCd > 2) return { kind: 'guard', x: G.sh.x, y: G.sh.y, label: '구역 경계' };",
              "    /* 거너 — 파쇄탄이 식기 전엔 벽을 잡지 않는다. 예전에는 리더 옆에 서서\n"
              "       조준만 돌렸다(쿨 9초 중 7초가 빈 시간이었다). 이제 통로 입구를 잡는다 */\n"
              "    if (m.roleId === 'gunner' && m.breakerCd > 2) {\n"
              "      const badPost = m.watch && typeof solidAt === 'function' && solidAt(m.watch.x, m.watch.y);\n"
              "      if (!m.watch || m.watchT <= 0 || badPost) {\n"
              "        m.watch = pickWatchPost(m); m.watchT = rnd(3.5, 9.5);\n"
              "      }\n"
              "      if (m.watch) return { kind: 'watch', x: m.watch.x, y: m.watch.y, label: '구역 경계' };\n"
              "      return { kind: 'guard', x: G.sh.x, y: G.sh.y, label: '대기' };\n"
              "    }")

    # ── 15) act() — 새 분기 + 채굴/교전 손질 ───────────────────────
    s = patch(s, 'act-branches',
              "    if (goal.kind === 'loot') {",
              ACT_BRANCHES + "    if (goal.kind === 'loot') {")
    s = patch(s, 'act-flare',
              "    if (goal.kind === 'flare') {\n"
              "      const kit = m.kit;\n"
              "      aimTo(m, goal.x, goal.y);\n"
              "      const a = Math.atan2(goal.y - m.y, goal.x - m.x);\n"
              "      const fx = m.x + Math.cos(a) * CELL * 2.6, fy = m.y + Math.sin(a) * CELL * 2.6;\n"
              "      G.lamps.push({ c: 0, r: 0, x: fx, y: fy, ph: Math.random() * 6, rad: Math.max(DEMO.lampRadius * 1.45, CELL * 4.2), ttl: 22, flare: 1, visionRange: 5 });\n"
              "      if (typeof LOS !== 'undefined' && LOS.markDirty) LOS.markDirty();\n"
              "      J.flash(fx, fy, 42, 'rgba(255,220,160,.9)');\n"
              "      J.ring(fx, fy, '#FFD080', 6, CELL * 1.6, 3);\n"
              "      m.flareCd = kit.flareCd;\n"
              "      reconAward(m, fx, fy);                            /* 정찰 — 새 구역을 밝힐 때만 */\n"
              "      say(m, '플레어');\n"
              "      return;\n"
              "    }",
              "    if (goal.kind === 'flare') { throwFlare(m, goal.x, goal.y, '플레어'); m.goal = null; return; }")
    s = patch(s, 'act-turret-spot',
              "    if (goal.kind === 'turret') { placeTurret(m); return; }",
              "    if (goal.kind === 'turret') { placeTurret(m, pickTurretSpot(m, null)); return; }")
    s = patch(s, 'act-mine',
              "      steer(m, goal.x, goal.y, dt, 0.4);\n"
              "      if (gunner) { fireBreaker(m, goal.c, goal.r); return; }",
              "      steer(m, goal.x, goal.y, dt, 0.4);\n"
              "      if (gunner) { fireBreaker(m, goal.c, goal.r); return; }\n"
              "      /* 돌파 파기 — 쿨이 돌 때마다 쓰지 않고, 두꺼운 벽일 때 더 자주 쓴다 */\n"
              "      if (m.kit.breachCd && m.breachT <= 0 && m.breachCd <= 0) {\n"
              "        const wh = GEO.wallHp ? GEO.wallHp(goal.c, goal.r) : 0;\n"
              "        if (intent(m, 'breach', true, { min: 0.5, max: 3.2, p: wh > 120 ? 0.75 : 0.4 })) startBreach(m);\n"
              "      }\n"
              "      /* 잠깐 손을 멈추고 숨을 돌린다 — 벽에 딱 붙어 끝까지 갈지는 않는다 */\n"
              "      if (idleBeat(m, dt, false)) return;")
    s = patch(s, 'act-fight-kite',
              "      if (d < want * 0.65) {\n"
              "        moveAwayFrom(m, e.x, e.y, dt, 0.95);\n"
              "        if (d < CELL * 1.5 && m.dashCd <= 0 && m.hp < m.hpMax * 0.5) tryDashAI(m, m.x - e.x, m.y - e.y);\n"
              "      } else if (d > want * 1.35) followStep(m, goal, dt);\n"
              "      else {\n"
              "        /* 사거리 유지 — 옆으로 돌며 쏜다 */\n"
              "        const a = Math.atan2(e.y - m.y, e.x - m.x) + Math.PI / 2 * (m.id % 2 ? 1 : -1);\n"
              "        steer(m, m.x + Math.cos(a) * CELL, m.y + Math.sin(a) * CELL, dt, 0.55);\n"
              "      }",
              "      /* 유지 거리와 회피 방향에 개인차·흔들림을 준다. 좌우 방향은 가끔 바뀐다 */\n"
              "      if (m.strafeT == null || m.strafeT <= 0) {\n"
              "        m.strafeT = rnd(0.8, 3.2);\n"
              "        m.farJit = rnd(-0.08, 0.16);\n"
              "        if (roll(0.35)) m.pers.strafe *= -1;\n"
              "      }\n"
              "      m.strafeT -= dt;\n"
              "      const near = want * (0.55 + 0.16 * m.pers.caution), far = want * (1.3 + m.farJit);\n"
              "      if (d < near) {\n"
              "        moveAwayFrom(m, e.x, e.y, dt, 0.95);\n"
              "        const panic = m.hp < m.hpMax * (0.32 + 0.24 * m.pers.caution);\n"
              "        if (d < CELL * 1.5 && m.dashCd <= 0 && panic\n"
              "            && intent(m, 'kite', true, { min: 0.05, max: 0.6, p: 0.72 })) tryDashAI(m, m.x - e.x, m.y - e.y);\n"
              "      } else if (d > far) followStep(m, goal, dt);\n"
              "      else {\n"
              "        const a = Math.atan2(e.y - m.y, e.x - m.x) + Math.PI / 2 * m.pers.strafe;\n"
              "        steer(m, m.x + Math.cos(a) * CELL, m.y + Math.sin(a) * CELL, dt, 0.55);\n"
              "      }")
    s = patch(s, 'act-guard-sweep',
              "    else {\n"
              "      m.vx *= 0.85; m.vy *= 0.85;\n"
              "      m.aim += dt * 0.7;\n"
              "      m.face = Math.cos(m.aim) < 0 ? -1 : 1;\n"
              "    }",
              "    else if (!idleBeat(m, dt)) {\n"
              "      /* 일정 속도로 조준을 돌리는 건 기계처럼 보인다 — 멈췄다 훑는다 */\n"
              "      m.vx *= 0.85; m.vy *= 0.85;\n"
              "      if (m.sweepT == null || m.sweepT <= 0) { m.sweepT = rnd(0.6, 2.4); m.sweepA = m.aim + rnd(-2, 2); }\n"
              "      m.sweepT -= dt;\n"
              "      aimTo(m, m.x + Math.cos(m.sweepA) * CELL * 3, m.y + Math.sin(m.sweepA) * CELL * 3);\n"
              "    }")

    # ── 16) 렌더 — 균열·펄스 표식 ──────────────────────────────────
    s = patch(s, 'draw-marks',
              "  function drawInstallations() {\n    for (const n of AI.nodes) {",
              "  function drawInstallations() {\n"
              "    /* AI 기반암 균열 — 사람의 균열 게이지와 같은 감각 */\n"
              "    for (const [k, v] of AI.cracks) {\n"
              "      if (!G.cell[k]) continue;\n"
              "      const c = k % COLS, r = (k - c) / COLS, x = cxw(c), y = cyw(r);\n"
              "      if (!seenAt(x, y)) continue;\n"
              "      cx.save(); cx.translate(x, y);\n"
              "      cx.strokeStyle = v.p > 0.72 ? '#FFF3D6' : '#FFD36E';\n"
              "      cx.globalAlpha = 0.32 + v.p * 0.62; cx.lineWidth = 1.7;\n"
              "      cx.beginPath(); cx.arc(0, 0, CELL * 0.34, -Math.PI * 0.5, -Math.PI * 0.5 + Math.PI * 2 * v.p); cx.stroke();\n"
              "      cx.restore();\n"
              "    }\n"
              "    /* 스카우트 펄스 표식 — 광맥/위협 */\n"
              "    for (const k of AI.marks) {\n"
              "      const a = Math.min(1, k.ttl / 1.5);\n"
              "      cx.save(); cx.globalAlpha = 0.7 * a; cx.lineWidth = 2;\n"
              "      if (k.kind === 'vein') {\n"
              "        cx.strokeStyle = '#FFD36E';\n"
              "        cx.beginPath(); cx.arc(k.x, k.y, CELL * 0.3, 0, 7); cx.stroke();\n"
              "        cx.beginPath(); cx.moveTo(k.x - 4, k.y); cx.lineTo(k.x + 4, k.y);\n"
              "        cx.moveTo(k.x, k.y - 4); cx.lineTo(k.x, k.y + 4); cx.stroke();\n"
              "      } else {\n"
              "        cx.strokeStyle = '#FF718A';\n"
              "        cx.beginPath(); cx.moveTo(k.x - 6, k.y - 6); cx.lineTo(k.x + 6, k.y + 6);\n"
              "        cx.moveTo(k.x + 6, k.y - 6); cx.lineTo(k.x - 6, k.y + 6); cx.stroke();\n"
              "      }\n"
              "      cx.restore();\n"
              "    }\n"
              "    for (const n of AI.nodes) {")

    # ══════════════════════════════════════════════════════════════
    #  관전 리더 (observer 블록)
    # ══════════════════════════════════════════════════════════════
    s = patch(s, 'obs-leader-skills',
              "  OBS.drive = function (dt) {",
              LEADER_SKILLS + "  OBS.drive = function (dt) {")
    s = patch(s, 'obs-leader-call',
              "    G.mouse.down = G.mouse.drillDown;                 /* 본편 입력 규약과 동일 */\n"
              "    watchStuck(dt);",
              "    try { leaderSkills(dt); } catch (e) { /* 스킬 실패가 오토파일럿을 멈추게 하지 않는다 */ }\n"
              "    G.mouse.down = G.mouse.drillDown;                 /* 본편 입력 규약과 동일 */\n"
              "    watchStuck(dt);")
    s = patch(s, 'obs-bedrock-goal',
              "    const pick = pickMine(6) || pickMine(11) || pickMine(17) || pickFrontier();",
              "    /* 리더 드릴러 — 기반암도 후보다. 사람이 드릴을 물리면 균열이 쌓인다 */\n"
              "    if (INF.roleId === 'driller' && lIntent('rock', true, 3, 11, 0.45)) {\n"
              "      const b = pickLeaderBedrock();\n"
              "      if (b) { OBS.rockTarget = { c: b.c, r: b.r, x: b.x, y: b.y, until: G.t + 22 }; }\n"
              "    }\n"
              "    if (OBS.rockTarget) {\n"
              "      const t = G.cell[OBS.rockTarget.c + OBS.rockTarget.r * COLS];\n"
              "      const solidRock = t && (typeof SOLIDX === 'function' ? SOLIDX(t) : (t === 'rock' || t === 'core'));\n"
              "      if (!solidRock || OBS.rockTarget.until < G.t) OBS.rockTarget = null;\n"
              "      else return { kind: 'rock', x: OBS.rockTarget.x, y: OBS.rockTarget.y,\n"
              "        c: OBS.rockTarget.c, r: OBS.rockTarget.r, label: '기반암 균열' };\n"
              "    }\n"
              "    const pick = pickMine(6) || pickMine(11) || pickMine(17) || pickFrontier();")
    s = patch(s, 'obs-bedrock-act',
              "    if (goal.kind === 'mine') {\n      followPath(goal.x, goal.y);",
              "    if (goal.kind === 'rock') {\n"
              "      /* 기반암 — 붙어서 드릴을 물린다. 본편 infDrillerPressure 가 균열을 쌓는다 */\n"
              "      const d = Math.hypot(goal.x - G.sh.x, goal.y - G.sh.y);\n"
              "      aimAt(goal.x, goal.y);\n"
              "      if (d > CELL * 0.95) keysToward(goal.x, goal.y);\n"
              "      if (d < CELL * 1.3) { G.mouse.drillDown = true; OBS.drillCell = { c: goal.c, r: goal.r }; }\n"
              "      OBS.rockT = (OBS.rockT || 0) + dt;\n"
              "      if (OBS.rockT > 12 + Math.random() * 10) { OBS.rockT = 0; OBS.rockTarget = null; OBS.goal = null; }\n"
              "      return;\n"
              "    }\n"
              "    if (goal.kind === 'mine') {\n      followPath(goal.x, goal.y);")
    s = patch(s, 'obs-reset-brain',
              "    OBS.drillCell = null;\n",
              "    OBS.drillCell = null;\n    OBS.rockTarget = null; OBS.rockT = 0;\n    rollLeaderPersona();          /* 판마다 리더의 손버릇이 달라진다 */\n",
              count=1)

    out.write_text(s, encoding='utf-8')
    print('OK  %s → %s' % (base.name, out.name))
    print('    패치 %d곳: %s' % (len(LOG), ', '.join(LOG)))


if __name__ == '__main__':
    try:
        main(sys.argv)
    except Fail as e:
        print('FAIL %s' % e)
        sys.exit(1)
