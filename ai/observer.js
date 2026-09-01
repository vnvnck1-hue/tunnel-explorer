/* ══════════════════════════════════════════════════════════════════════
   관전 모드 (OBSERVER) — v1
   ──────────────────────────────────────────────────────────────────────
   목적: 직업 4개가 완전 자동으로 스테이지를 계속 진행하고, 사람은
   관찰만 한다. 리더(사람 캐릭터)는 가상 입력으로 움직이고, 나머지
   3직업은 기존 AI 크루가 리더를 따라 자기 일을 한다.

   설계 원칙
   1) 리더 오토파일럿은 "가상 입력"으로만 조작한다 — KEY 셋과 G.mouse 를
      프레임마다 덮어쓴다. 사람 시스템(특성·경험치·코어 회수·탑승 판정)을
      전혀 건드리지 않으므로 밸런스가 원본 그대로다.
   2) 관전 중 실제 마우스·키보드는 게임에 닿지 않는다 — 캡처 단계에서
      끊는다. 관전 조작(Tab 시점 전환 · Esc 해제 · [ ] 줌)만 통과한다.
   3) 진행 결정(특성 카드·심층 보상·하강·재출격)은 DOM 버튼을 지연 클릭
      한다 — 관찰자가 "무엇이 선택됐는지" 볼 시간을 준다.
   4) 관전 모드가 꺼져 있으면 게임은 원본과 완전히 같은 코드 경로다.

   주입 훅 (ai/inject-observer.mjs):
     - update(dt) 의 입력 읽기 직전: OBSERVER.drive(dt)
     - 카메라 확정 직후:            OBSERVER.camera(dt)
   ══════════════════════════════════════════════════════════════════════ */
(function () {
  'use strict';

  const OBS = {
    active: false,
    focus: 0,               /* 0 = 리더 · 1..n = AI 크루 */
    /* 리더 오토파일럿 상태 */
    goal: null, react: 0,
    path: [], pathKey: '', pathAge: 0,
    mineTarget: null,
    stuckT: 0, lastX: 0, lastY: 0, jitter: 0, jitterA: 0,
    _dist: null, _prev: null, _size: 0,
    /* 진행 자동화 타이머 (autoUI 0.3초 틱) */
    tLevel: 0, tRest: 0, tResult: 0, idleT: 0,
    /* 관전 카메라 */
    camX: null, camY: null,
  };
  window.OBSERVER = OBS;

  /* ══════════ 빙의 — 관전 중 Esc 로 현재 시점의 크루를 직접 조종한다 (F9 관전 복귀) ══════════ */
  OBS.possess = null;                    /* 조종 중인 AI 크루 (null = 빙의 없음) */
  window.OBS_MANUAL = null;              /* AICREW.manualAct 가 읽는 실입력 채널 */
  function enterPossess(focusIdx) {
    if (typeof AICREW === 'undefined') return;
    const m = AICREW.members[focusIdx - 1];
    if (!m) return;
    releasePossess();
    OBS.possess = m; m.manual = true; m.goal = null; m.path = [];
    window.OBS_MANUAL = { keys: new Set(), sx: null, sy: null, aimX: null, aimY: null, fire: false, dash: false, q: false, e: false };
    say('빙의 — ' + (ROLE_KO[m.roleId] || m.roleId) + ' 직접 조종 · Tab/F9 관전 복귀');
    paintBadge();
  }
  function releasePossess() {
    if (!OBS.possess) return;
    OBS.possess.manual = false; OBS.possess.goal = null;
    OBS.possess = null;
    if (window.OBS_MANUAL) { OBS_MANUAL.keys.clear(); OBS_MANUAL.fire = false; OBS_MANUAL.dash = false; OBS_MANUAL.q = false; }
  }

  const ROLE_KO = { driller: '드릴러', gunner: '거너', scout: '스카우트', engineer: '엔지니어' };
  const ROLES = ['driller', 'gunner', 'scout', 'engineer'];
  /* 리더의 경로 굴착 의지·교전 거리 — AI 크루 KIT 와 같은 감각 */
  const LEADER_KIT = {
    driller: { digCost: 5, engage: 3.6 },
    gunner: { digCost: 14, engage: 5.2 },   /* 파쇄탄이 벽을 열어 주므로 중간값 */
    scout: { digCost: 12, engage: 4.6 },
    engineer: { digCost: 10, engage: 4.4 },
  };

  const ready = () => typeof G !== 'undefined' && typeof CELL !== 'undefined'
    && typeof CREW !== 'undefined' && typeof INF !== 'undefined';
  const playing = () => ready() && INF.active && CREW.phase === 'play' && SCENE === 'depths';
  const idx = (c, r) => r * COLS + c;
  const cellAt = (c, r) => (c >= 0 && c < COLS && r >= 0 && r < ROWS ? G.cell[idx(c, r)] : 'rock');
  const unbreak = (t) => (typeof SOLIDX === 'function' ? SOLIDX(t) : t === 'rock' || t === 'core');
  const kit = () => LEADER_KIT[INF.roleId] || LEADER_KIT.driller;
  const say = (msg) => { if (typeof toast === 'function') toast(msg); };

  /* ══════════ 경로 — AI 크루와 같은 다익스트라 (벽 = 뚫는 시간만큼 비싼 통로) ══════════ */

  function digCost(t) {
    if (!t) return 1;
    if (unbreak(t)) return Infinity;
    const hp = ((typeof HPT !== 'undefined' && HPT[t]) || 100) * (INF.wallHpMul || 1);
    return 1 + (hp / 100) * kit().digCost;
  }
  function findPath(sc, sr, gc, gr) {
    const N = COLS * ROWS;
    if (OBS._size !== N) { OBS._dist = new Float64Array(N); OBS._prev = new Int32Array(N); OBS._size = N; }
    const dist = OBS._dist, prev = OBS._prev;
    dist.fill(Infinity); prev.fill(-1);
    if (sc < 0 || sr < 0 || gc < 0 || gr < 0 || sc >= COLS || gc >= COLS || sr >= ROWS || gr >= ROWS) return null;
    const start = idx(sc, sr), goal = idx(gc, gr);
    if (start === goal) return [];
    dist[start] = 0;
    const heap = [[0, start]];
    const pop = () => {
      let bi = 0;
      for (let i = 1; i < heap.length; i++) if (heap[i][0] < heap[bi][0]) bi = i;
      const v = heap[bi]; heap[bi] = heap[heap.length - 1]; heap.pop(); return v;
    };
    let guard = 0;
    while (heap.length && guard++ < 24000) {
      const [d, k] = pop();
      if (d > dist[k]) continue;
      if (k === goal) break;
      const c = k % COLS, r = (k - c) / COLS;
      for (let i = 0; i < 4; i++) {
        const dc = i === 0 ? 1 : i === 1 ? -1 : 0, dr = i === 2 ? 1 : i === 3 ? -1 : 0;
        const nc = c + dc, nr = r + dr;
        if (nc < 1 || nr < 1 || nc >= COLS - 1 || nr >= ROWS - 1) continue;
        const nk = idx(nc, nr), w = digCost(G.cell[nk]);
        if (!isFinite(w)) continue;
        const nd = d + w;
        if (nd < dist[nk]) { dist[nk] = nd; prev[nk] = k; heap.push([nd, nk]); }
      }
    }
    if (!isFinite(dist[goal])) return null;
    const out = [];
    for (let k = goal; k !== -1 && k !== start; k = prev[k]) out.push(k);
    return out.reverse();
  }
  function ensurePath(gx, gy) {
    const sh = G.sh;
    const c0 = Math.floor(sh.x / CELL), r0 = Math.floor(sh.y / CELL);
    const gc = Math.floor(gx / CELL), gr = Math.floor(gy / CELL);
    const key = gc + ',' + gr;
    if (key !== OBS.pathKey || OBS.pathAge <= 0 || !OBS.path.length) {
      OBS.path = findPath(c0, r0, gc, gr) || [];
      OBS.pathKey = key; OBS.pathAge = 0.5;
    }
    while (OBS.path.length) {
      const k = OBS.path[0], c = k % COLS, r = (k - c) / COLS;
      if (!G.cell[k] && Math.hypot(cxw(c) - sh.x, cyw(r) - sh.y) < CELL * 0.55) OBS.path.shift();
      else break;
    }
    return OBS.path.length ? OBS.path[0] : -1;
  }

  /* ══════════ 가상 입력 ══════════ */

  /* 이동 — WASD 를 방향으로 환산해 KEY 셋에 넣는다 */
  function keysToward(wx, wy) {
    const dx = wx - G.sh.x, dy = wy - G.sh.y, d = Math.hypot(dx, dy);
    if (d < 4) return;
    let nx = dx / d, ny = dy / d;
    if (OBS.jitter > 0) { nx = Math.cos(OBS.jitterA); ny = Math.sin(OBS.jitterA); }
    if (nx < -0.38) KEY.add('a'); else if (nx > 0.38) KEY.add('d');
    if (ny < -0.38) KEY.add('w'); else if (ny > 0.38) KEY.add('s');
  }
  /* 조준 — w2s 로 화면 좌표를 만들어 마우스 위치를 대신한다.
     screenToWorld 가 같은 카메라 값으로 역변환하므로 오차가 없다. */
  function aimAt(wx, wy) {
    if (typeof w2s !== 'function') return;
    const s = w2s(wx, wy);
    G.mouse.sx = s[0]; G.mouse.sy = s[1];
  }
  function fleeDash(nx, ny) {
    G.dirx = nx; G.diry = ny;
    if (typeof tryDash === 'function') tryDash();
  }

  /* ══════════ 위협 · 회피 ══════════ */

  function nearestThreat() {
    if (!G.enemies) return null;
    let best = null, bd = 1e9;
    for (const e of G.enemies) {
      if (e.hp <= 0 || e.boss) continue;
      const d = Math.hypot(e.x - G.sh.x, e.y - G.sh.y);
      if (d > CELL * 13) continue;
      if (d > CELL * 2.4 && typeof sightClear === 'function' && !sightClear(G.sh.x, G.sh.y, e.x, e.y)) continue;
      if (d < bd) { bd = d; best = e; }
    }
    return best;
  }

  /* 보스탄 예고 원 안이면 하던 일과 무관하게 빠진다 — AI 크루와 같은 규칙 */
  function dodgeBossShot(dt) {
    if (!INF.active) return false;
    let danger = null, bestEta = 1e9;
    const scan = (s, eta) => {
      if (eta > 1.8) return;
      const d = Math.hypot(G.sh.x - s.tx, G.sh.y - s.ty);
      if (d > s.rad * 1.15) return;
      if (eta < bestEta) { bestEta = eta; danger = { s, d, eta }; }
    };
    for (const s of INF.bossShots || []) scan(s, Math.max(0, s.flight - s.t));
    for (const q of INF.bossShotQueue || []) scan(q, Math.max(0, q.delay || 0) + (q.flight || 0));
    if (!danger) return false;
    let nx = G.sh.x - danger.s.tx, ny = G.sh.y - danger.s.ty;
    const n = Math.hypot(nx, ny);
    if (n < 1) { const a = Math.random() * Math.PI * 2; nx = Math.cos(a); ny = Math.sin(a); }
    else { nx /= n; ny /= n; }
    const need = danger.s.rad * 1.3 - danger.d;
    keysToward(G.sh.x + nx * (need + CELL), G.sh.y + ny * (need + CELL));
    if (danger.eta < 0.6 && need > CELL * 0.5) fleeDash(nx, ny);
    return true;
  }

  /* ══════════ 채굴 대상 — 광맥 가중, 열린 면 우선 ══════════ */

  function pickMine(radius) {
    const oc = Math.floor(G.sh.x / CELL), or_ = Math.floor(G.sh.y / CELL);
    /* AI 크루가 잡은 벽 주변 1칸은 피한다 — 리더까지 같은 벽에 줄 서지 않게 */
    const claims = [];
    if (typeof AICREW !== 'undefined' && AICREW.members)
      for (const o of AICREW.members) if (o.mineTarget) claims.push(o.mineTarget);
    let best = null, bs = -1e9;
    for (let dr = -radius; dr <= radius; dr++) for (let dc = -radius; dc <= radius; dc++) {
      const c = oc + dc, r = or_ + dr;
      if (c < 1 || r < 1 || c >= COLS - 1 || r >= ROWS - 1) continue;
      const t = G.cell[idx(c, r)];
      if (!t || unbreak(t)) continue;
      let open = false;
      for (let i = 0; i < 4; i++) {
        const nc = c + (i === 0 ? 1 : i === 1 ? -1 : 0), nr = r + (i === 2 ? 1 : i === 3 ? -1 : 0);
        if (!cellAt(nc, nr)) { open = true; break; }
      }
      if (!open) continue;
      let claimed = false;
      for (const q of claims) if (Math.abs(q.c - c) <= 1 && Math.abs(q.r - r) <= 1) { claimed = true; break; }
      if (claimed) continue;
      const ore = t === 'gem' ? 28 : t === 'crys' ? 22 : t === 'ore' ? 16 : t === 'stone' ? 3 : 2;
      const s = ore - Math.hypot(dc, dr) * 2.2;
      if (s > bs) { bs = s; best = { c, r, x: cxw(c), y: cyw(r) }; }
    }
    return best;
  }
  /* 주변이 다 파였으면 무작위 표본으로 멀리 있는 벽을 찾는다 — 장악도를 계속 올린다 */
  function pickFrontier() {
    for (let i = 0; i < 60; i++) {
      const c = 1 + ((Math.random() * (COLS - 2)) | 0), r = 1 + ((Math.random() * (ROWS - 2)) | 0);
      const t = G.cell[idx(c, r)];
      if (t && !unbreak(t)) return { c, r, x: cxw(c), y: cyw(r) };
    }
    return null;
  }

  /* ══════════ 판단 ══════════ */

  function decide() {
    const esc = INF.escape;
    if (esc && esc.state === 'placing' && typeof infCancelEscapePlacement === 'function') infCancelEscapePlacement();
    /* 0) 탈출 포트가 있으면 탑승한다 — 크루 전원이 같은 규칙 */
    if (esc && (esc.state === 'incoming' || esc.state === 'ready')) {
      return { kind: 'escape', x: esc.x, y: esc.y, label: '탈출 포트' };
    }
    /* 1) 쓰러진 AI 크루 구조 — 리더가 유일한 구조자일 수 있다 */
    if (typeof AICREW !== 'undefined' && AICREW.members.length) {
      const dm = AICREW.members.find((m) => m.down);
      if (dm) return { kind: 'revive', m: dm, x: dm.x, y: dm.y, label: '크루 구조' };
    }
    /* 2) 전투 — 보스가 가까우면 보스, 아니면 가장 가까운 위협 */
    const boss = INF.boss && INF.boss.hp > 0 ? INF.boss : null;
    if (boss && Math.hypot(boss.x - G.sh.x, boss.y - G.sh.y) < CELL * 18) {
      return { kind: 'fight', e: boss, boss: true, label: '보스 교전' };
    }
    const e = nearestThreat();
    if (e) return { kind: 'fight', e, label: '교전' };
    /* 3) 전투가 끊긴 사이 재장전 */
    if (typeof infStartReload === 'function' && INF.reloadLeft <= 0 && INF.ammo < INF.magSize * 0.5) infStartReload(true);
    /* 4) 채굴 — 한 번 고른 벽은 부술 때까지 붙잡는다 */
    if (OBS.mineTarget) {
      const t = cellAt(OBS.mineTarget.c, OBS.mineTarget.r);
      if (!t || unbreak(t) || OBS.mineTarget.until < G.t) OBS.mineTarget = null;
      else return { kind: 'mine', x: OBS.mineTarget.x, y: OBS.mineTarget.y, label: '채굴' };
    }
    const pick = pickMine(6) || pickMine(11) || pickMine(17) || pickFrontier();
    if (pick) {
      OBS.mineTarget = { c: pick.c, r: pick.r, x: pick.x, y: pick.y, until: G.t + 16 };
      return { kind: 'mine', x: pick.x, y: pick.y, label: '채굴' };
    }
    return { kind: 'idle', label: '대기' };
  }

  /* ══════════ 실행 ══════════ */

  function followPath(gx, gy) {
    const step = ensurePath(gx, gy);
    if (step < 0) { keysToward(gx, gy); aimAt(gx, gy); return; }
    const c = step % COLS, r = (step - c) / COLS, wx = cxw(c), wy = cyw(r);
    if (G.cell[step]) {
      /* 앞이 벽 — 조준하고 드릴(거너는 같은 입력으로 파쇄탄이 나간다) */
      aimAt(wx, wy);
      const d = Math.hypot(wx - G.sh.x, wy - G.sh.y);
      if (d < CELL * 1.35) G.mouse.drillDown = true;
      else keysToward(wx, wy);
      return;
    }
    keysToward(wx, wy); aimAt(wx, wy);
  }

  function shootNear(cells) {
    if (!G.enemies) return;
    let best = null, bd = CELL * cells;
    for (const e of G.enemies) {
      if (e.hp <= 0) continue;
      const d = Math.hypot(e.x - G.sh.x, e.y - G.sh.y);
      if (d < bd && (typeof sightClear !== 'function' || sightClear(G.sh.x, G.sh.y, e.x, e.y))) { bd = d; best = e; }
    }
    if (best) { aimAt(best.x, best.y); G.mouse.gunDown = true; }
  }

  function act(goal, dt) {
    /* 보스탄 예고는 어떤 행동보다 먼저다 */
    const dodging = dodgeBossShot(dt);

    if (goal.kind === 'fight') {
      const e = goal.e;
      if (e.hp <= 0) { OBS.goal = null; return; }
      const d = Math.hypot(e.x - G.sh.x, e.y - G.sh.y);
      const want = CELL * (goal.boss ? kit().engage + 1.8 : kit().engage);
      aimAt(e.x, e.y);
      const los = typeof sightClear !== 'function' || sightClear(G.sh.x, G.sh.y, e.x, e.y);
      const range = ((typeof teWorld === 'function' ? teWorld(280) : 280) * 1.2) * 0.92;
      if (los && d <= range) G.mouse.gunDown = true;
      if (dodging) return;                      /* 사격은 유지한 채 원 밖으로 */
      if (!los && d > CELL * 2) { followPath(e.x, e.y); aimAt(e.x, e.y); return; }
      if (d < want * 0.6) keysToward(G.sh.x * 2 - e.x, G.sh.y * 2 - e.y);
      else if (d > want * 1.35) keysToward(e.x, e.y);
      else {
        /* 사거리 유지 — 옆으로 돌며 쏜다 */
        const a = Math.atan2(e.y - G.sh.y, e.x - G.sh.x) + Math.PI / 2;
        keysToward(G.sh.x + Math.cos(a) * CELL, G.sh.y + Math.sin(a) * CELL);
      }
      /* 드릴러는 붙은 적을 드릴로 간다 */
      if (INF.roleId === 'driller' && d < CELL * 1.6) G.mouse.drillDown = true;
      return;
    }
    if (dodging) return;

    if (goal.kind === 'escape') {
      const d = Math.hypot(goal.x - G.sh.x, goal.y - G.sh.y);
      if (d > CELL * 0.8) followPath(goal.x, goal.y);
      shootNear(8);
      return;
    }
    if (goal.kind === 'revive') {
      const m = goal.m;
      if (!m || !m.down) { OBS.goal = null; return; }
      goal.x = m.x; goal.y = m.y;
      const d = Math.hypot(m.x - G.sh.x, m.y - G.sh.y);
      if (d > CELL * 1.3) followPath(m.x, m.y);
      shootNear(8);
      return;
    }
    if (goal.kind === 'mine') { followPath(goal.x, goal.y); return; }
    /* idle — 주변 경계 */
    shootNear(9);
  }

  function watchStuck(dt) {
    const moved = Math.hypot(G.sh.x - OBS.lastX, G.sh.y - OBS.lastY);
    OBS.lastX = G.sh.x; OBS.lastY = G.sh.y;
    const wants = KEY.size > 0;
    if (wants && moved < dt * 9) OBS.stuckT += dt; else OBS.stuckT = Math.max(0, OBS.stuckT - dt * 2);
    if (OBS.jitter > 0) OBS.jitter -= dt;
    if (OBS.stuckT > 1.2) {
      OBS.stuckT = 0; OBS.path = []; OBS.pathKey = ''; OBS.pathAge = 0;
      OBS.jitter = 0.5; OBS.jitterA = Math.random() * Math.PI * 2;
      if (OBS.goal && OBS.goal.kind === 'mine') OBS.mineTarget = null;
    }
  }

  function resetBrain() {
    OBS.goal = null; OBS.react = 0;
    OBS.path = []; OBS.pathKey = ''; OBS.pathAge = 0;
    OBS.mineTarget = null; OBS.stuckT = 0; OBS.jitter = 0;
    OBS.tLevel = OBS.tRest = OBS.tResult = OBS.idleT = 0;
  }

  /* ══════════ 프레임 훅 1 — 리더 오토파일럿 (입력 읽기 직전) ══════════ */

  OBS.drive = function (dt) {
    if (!OBS.active || !playing()) return;
    /* 빙의 입력 — 마우스를 월드 좌표로 환산해 크루에게 전달. 리더 오토파일럿은 계속 돈다 */
    if (OBS.possess) {
      if (typeof AICREW === 'undefined' || !AICREW.members.includes(OBS.possess)) releasePossess();
      else {
        const M = window.OBS_MANUAL;
        if (M && M.sx != null && typeof screenToWorld === 'function') {
          const mw = screenToWorld(M.sx, M.sy);
          M.aimX = mw.x; M.aimY = mw.y;
        }
      }
    }
    /* 가상 입력으로 완전히 대체한다 — 실제 입력은 캡처 단계에서 이미 끊겨 있다 */
    KEY.clear();
    G.mouse.drillDown = false; G.mouse.gunDown = false;
    if (G.downed) { G.mouse.down = false; return; }   /* 기절 — AI 크루의 구조를 기다린다 */
    OBS.react -= dt; OBS.pathAge -= dt;
    if (OBS.react <= 0 || !OBS.goal) { OBS.goal = decide(); OBS.react = 0.15 + Math.random() * 0.08; }
    try { act(OBS.goal, dt); }
    catch (e) {
      if (!OBS._errAt || performance.now() - OBS._errAt > 3000) {
        OBS._errAt = performance.now();
        console.error('[OBSERVER]', OBS.goal && OBS.goal.kind, e);
      }
      OBS.goal = null;
    }
    G.mouse.down = G.mouse.drillDown;                 /* 본편 입력 규약과 동일 */
    watchStuck(dt);
  };

  /* ══════════ 프레임 훅 2 — 관전 카메라 (본편 카메라 확정 직후) ══════════ */

  function camTargets() {
    const role = (typeof INF_ROLES !== 'undefined' && INF_ROLES[INF.roleId]) || null;
    const list = [{ x: G.sh.x, y: G.sh.y, label: (role ? role.name : '리더') + ' (리더)' }];
    if (typeof AICREW !== 'undefined') {
      for (const m of AICREW.members) list.push({ x: m.x, y: m.y, label: 'AI ' + (ROLE_KO[m.roleId] || m.roleId) + ' Lv' + m.level });
    }
    return list;
  }

  OBS.camera = function (dt) {
    if (!OBS.active || !ready() || G.fixed || !INF.active) return;
    /* 빙의 중 — 시점을 빙의 크루에 고정한다 */
    if (OBS.possess) {
      const i = (typeof AICREW !== 'undefined') ? AICREW.members.indexOf(OBS.possess) : -1;
      if (i < 0) releasePossess(); else OBS.focus = i + 1;
    }
    const list = camTargets();
    if (OBS.focus >= list.length) OBS.focus = 0;
    if (OBS.focus === 0) { OBS.camX = null; return; }   /* 리더 — 본편 카메라 그대로 */
    const t = list[OBS.focus];
    const vw = LW / G.Z, vh = LH / G.Z;
    if (OBS.camX == null) { OBS.camX = G.camX + vw * 0.5; OBS.camY = G.camY + vh * 0.42; }
    const k = Math.min(1, dt * 5);
    OBS.camX += (t.x - OBS.camX) * k; OBS.camY += (t.y - OBS.camY) * k;
    const b = (typeof tcClampCamera === 'function')
      ? tcClampCamera(OBS.camX - vw * 0.5, OBS.camY - vh * 0.42, vw, vh)
      : { x: OBS.camX - vw * 0.5, y: OBS.camY - vh * 0.42 };
    G.camX = b.x; G.camY = b.y;
  };

  function cycleFocus() {
    if (!ready()) return;
    const list = camTargets();
    OBS.focus = (OBS.focus + 1) % list.length;
    if (OBS.focus === 0) OBS.camX = null;
    say('관전 시점 · ' + list[OBS.focus].label);
    paintBadge();
  }

  /* ══════════ 진행 자동화 — 카드·하강·재출격 (0.3초 틱) ══════════ */

  function modalOn(id) { const el = document.getElementById(id); return !!(el && el.classList.contains('on')); }

  function autoUI() {
    paintBadge();
    if (!OBS.active || !ready()) return;
    /* 메인 메뉴로 나가면 관전을 스스로 푼다 — 다음 수동 런을 오염시키지 않는다 */
    if (!INF.active) {
      OBS.idleT += 0.3;
      if (OBS.idleT > 1.5) OBS.exit('메뉴 복귀');
      return;
    }
    OBS.idleT = 0;

    /* 빙의 중 — 진행 선택(특성 카드·심층 보상·하강·재출격)은 전부 사람이 한다 */
    if (OBS.possess) { OBS.tLevel = OBS.tRest = OBS.tResult = 0; return; }

    /* 레벨업 특성 카드 — 잠깐 보여주고 자동 선택 (다시 뽑기는 쓰지 않는다) */
    if (modalOn('infLevelModal')) {
      OBS.tLevel += 0.3;
      if (OBS.tLevel >= 1.5) {
        OBS.tLevel = -1.2;
        const btns = [...document.querySelectorAll('#infLevelCards button')].filter((b) => !b.disabled);
        if (btns.length) { btns[(Math.random() * btns.length) | 0].click(); say('관전 · 특성 자동 선택'); }
      }
    } else OBS.tLevel = 0;

    /* 보스 격파 휴식 — 심층 보상을 고르고 다음 지층으로 내려간다 */
    if (modalOn('infRestModal')) {
      OBS.tRest += 0.3;
      if (!INF.restChosen && OBS.tRest >= 2.1) {
        const btns = [...document.querySelectorAll('#infLegendCards button')].filter((b) => !b.disabled);
        if (btns.length) { btns[(Math.random() * btns.length) | 0].click(); say('관전 · 심층 보상 자동 선택'); }
      }
      if (INF.restChosen && OBS.tRest >= 3.9) {
        const d = document.getElementById('infDescend');
        if (d && !d.disabled) { OBS.tRest = 0; resetBrain(); d.click(); say('관전 · 다음 지층으로'); }
      }
    } else OBS.tRest = 0;

    /* 런 종료(전멸·생환) — 결과를 보여주고 자동 재출격. 편성은 유지된다. */
    if (modalOn('infResultModal')) {
      OBS.tResult += 0.3;
      if (OBS.tResult >= 4.5) {
        OBS.tResult = 0; resetBrain();
        const r = document.getElementById('infRetry');
        if (r) { say('관전 · 자동 재출격'); r.click(); }
      }
    } else OBS.tResult = 0;
  }

  /* ══════════ 실제 입력 차단 — 관전 중 내 마우스·키보드는 자유다 ══════════ */

  /* 게임이 듣는 키만 막는다. 브라우저 단축키(F5·F12 등)는 그대로 둔다. */
  const BLOCK_KEYS = new Set(['w', 'a', 's', 'd', 'q', 'e', 'r', 'x', 'f', '1', '2', '3', ' ', 'f9']);

  window.addEventListener('keydown', (e) => {
    /* F8 — 런 도중 관전 토글 (편성은 그대로) */
    if (e.key === 'F8' && ready() && INF.active) {
      if (OBS.active) OBS.exit();
      else if (CREW.phase === 'play') OBS.resume();
      e.preventDefault(); e.stopImmediatePropagation(); return;
    }
    /* F9 또는 Tab — 플레이(빙의·직접 조종) 중이면 관전 모드로 복귀한다 (전원 AI 대체) */
    if ((e.key === 'F9' || e.key === 'Tab') && ready() && INF.active) {
      if (OBS.possess) { releasePossess(); say('관전 모드 복귀 — 전원 AI'); paintBadge(); e.preventDefault(); e.stopImmediatePropagation(); return; }
      if (!OBS.active && OBS.everUsed && CREW.phase === 'play') { OBS.resume(); e.preventDefault(); e.stopImmediatePropagation(); return; }
      if (e.key === 'F9') { e.preventDefault(); e.stopImmediatePropagation(); return; }
      /* 관전 중(비빙의) Tab 은 아래 시점 순환으로 흘러간다 */
    }
    if (!OBS.active || !ready() || !INF.active) return;
    /* 빙의 중 — 실제 키 입력을 빙의 크루에게 전달한다 */
    if (OBS.possess) {
      const k = e.key.toLowerCase(), M = window.OBS_MANUAL;
      if (M) {
        if (k === 'w' || k === 'a' || k === 's' || k === 'd') { M.keys.add(k); e.preventDefault(); e.stopImmediatePropagation(); return; }
        if (e.code === 'Space') { M.dash = true; e.preventDefault(); e.stopImmediatePropagation(); return; }
        if (k === 'q') { M.q = true; e.preventDefault(); e.stopImmediatePropagation(); return; }
        if (k === 'e') { M.e = true; e.preventDefault(); e.stopImmediatePropagation(); return; }
      }
      if (e.code === 'BracketLeft' || e.code === 'BracketRight') return;   /* 줌 통과 */
      if (e.key === 'Tab' || e.key === 'Escape' || BLOCK_KEYS.has(k)) { e.preventDefault(); e.stopImmediatePropagation(); }
      return;
    }
    if (e.key === 'Tab') { cycleFocus(); e.preventDefault(); e.stopImmediatePropagation(); return; }
    if (e.key === 'Escape') {
      /* Esc — 지금 보고 있는 캐릭터를 직접 조종한다. 리더면 관전 해제, AI 크루면 빙의 */
      if (OBS.focus === 0) OBS.exit('리더 직접 조종 · Tab/F9 관전 복귀');
      else enterPossess(OBS.focus);
      e.preventDefault(); e.stopImmediatePropagation(); return;
    }
    if (e.code === 'BracketLeft' || e.code === 'BracketRight') return;   /* 줌은 관전에 유용 — 통과 */
    if (BLOCK_KEYS.has(e.key.toLowerCase()) || e.code === 'Space') { e.preventDefault(); e.stopImmediatePropagation(); }
  }, true);
  window.addEventListener('keyup', (e) => {
    if (!OBS.possess || !window.OBS_MANUAL) return;
    const k = e.key.toLowerCase();
    if (k === 'w' || k === 'a' || k === 's' || k === 'd') { OBS_MANUAL.keys.delete(k); e.preventDefault(); e.stopImmediatePropagation(); }
  }, true);

  const blockPtr = (e) => {
    if (!OBS.active || !ready() || !INF.active) return;
    if (typeof cv !== 'undefined' && e.target === cv) {
      /* 빙의 중 — 좌클릭을 빙의 크루의 공격/채굴로 전달한다 */
      if (OBS.possess && window.OBS_MANUAL) {
        if (e.type === 'pointerdown' && e.button === 0) OBS_MANUAL.fire = true;
        if (e.type === 'pointerup') OBS_MANUAL.fire = false;
      }
      e.preventDefault(); e.stopImmediatePropagation();
    }
  };
  window.addEventListener('pointerdown', blockPtr, true);
  window.addEventListener('pointerup', blockPtr, true);
  window.addEventListener('pointermove', (e) => {
    if (!OBS.active || !ready() || !INF.active) return;
    if (typeof cv !== 'undefined' && e.target === cv) {
      /* 빙의 중 — 마우스 위치를 기록해 조준에 쓴다 (월드 환산은 drive 에서) */
      if (OBS.possess && window.OBS_MANUAL && typeof lpos === 'function') {
        const p = lpos(e); OBS_MANUAL.sx = p[0]; OBS_MANUAL.sy = p[1];
      }
      e.stopImmediatePropagation();
    }
  }, true);

  /* ══════════ 시작 / 해제 ══════════ */

  OBS.enter = function () {
    if (!ready() || typeof AICREW === 'undefined' || typeof infLaunchFromRoleSelect !== 'function') {
      say('관전 모드를 시작할 수 없습니다'); return;
    }
    const leader = (typeof INF_ROLES !== 'undefined' && INF_ROLES[INF.selectedRoleId]) ? INF.selectedRoleId : 'driller';
    /* 리더 직업을 뺀 나머지 3직업을 AI 크루로 — 4직업 전원 출격 */
    AICREW.clear();
    for (const r of ROLES) if (r !== leader) AICREW.add(r);
    AICREW.enabled = true;
    OBS.active = true; OBS.everUsed = true; OBS.focus = 0; OBS.camX = null;
    resetBrain();
    document.body.classList.add('obsActive');
    say('관전 모드 — Tab 시점 전환 · Esc 해제 · F8 재개');
    infLaunchFromRoleSelect(leader);
    paintBadge();
  };

  /* 런 도중 재개 — 편성을 건드리지 않고 오토파일럿만 켠다 (F8) */
  OBS.resume = function () {
    OBS.active = true; OBS.everUsed = true; OBS.focus = 0; OBS.camX = null;
    resetBrain();
    document.body.classList.add('obsActive');
    say('관전 모드 재개 — Tab 시점 전환 · Esc 해제');
    paintBadge();
  };

  OBS.exit = function (why) {
    if (!OBS.active) return;
    releasePossess();
    OBS.active = false; OBS.camX = null; OBS.focus = 0;
    if (ready()) {
      KEY.clear();
      G.mouse.drillDown = G.mouse.gunDown = G.mouse.down = false;
    }
    document.body.classList.remove('obsActive');
    say('관전 모드 해제' + (why ? ' (' + why + ')' : '') + ' — 직접 조작 · Tab/F9 관전 복귀');
    paintBadge();
  };

  /* ══════════ UI — 진입 버튼 · 관전 배지 ══════════ */

  const CSS = `
.modeBtn.observer{border-color:rgba(127,235,208,.55)}
.modeBtn.observer b{color:#7febd0}
#obsStartBtn{display:block;margin:14px auto 0;padding:11px 26px;border:1px solid rgba(127,235,208,.5);
 border-radius:12px;background:rgba(18,43,40,.65);color:#bfffef;cursor:pointer;
 font:800 13px/1 Pretendard,Malgun Gothic,sans-serif;letter-spacing:.03em}
#obsStartBtn:hover{background:rgba(127,235,208,.16)}
#obsStartBtn small{display:block;margin-top:5px;color:#7ea99f;font-weight:600;font-size:10px}
#obsBadge{position:absolute;left:50%;top:8px;transform:translateX(-50%);z-index:60;display:none;
 align-items:center;gap:10px;padding:7px 16px;border:1px solid rgba(127,235,208,.4);border-radius:999px;
 background:rgba(8,14,13,.82);backdrop-filter:blur(4px);color:#bfffef;pointer-events:none;
 font:800 11.5px/1 Pretendard,Malgun Gothic,sans-serif;letter-spacing:.03em}
#obsBadge .dot{width:8px;height:8px;border-radius:50%;background:#7febd0;animation:obsPulse 1.6s infinite}
#obsBadge .keys{color:#7ea99f;font-weight:600}
@keyframes obsPulse{0%,100%{opacity:1}50%{opacity:.35}}
body.obsActive #obsBadge{display:flex}
`;
  function ensureCSS() {
    if (document.getElementById('obsCSS')) return;
    const s = document.createElement('style');
    s.id = 'obsCSS'; s.textContent = CSS;
    document.head.appendChild(s);
  }

  /* 메인 메뉴 진입 — 다른 모드 버튼과 같은 문법(modeBtn)으로 노출한다.
     클릭하면 본편과 같은 진입 흐름(무한 모드 → 표준 → 직업 확정)을 그대로 타고
     곧장 관전 출격한다. 리더는 마지막으로 고른 직업(기본 드릴러). */
  function enterFromMenu() {
    if (!ready() || typeof AICREW === 'undefined') { say('관전 모드를 시작할 수 없습니다'); return; }
    if (typeof SFX !== 'undefined' && SFX.ui) SFX.ui();
    if (typeof tcLaunchInfScene === 'function') tcLaunchInfScene(false);   /* 상태 초기화 + 모드 선택 */
    if (typeof infChooseMode === 'function') infChooseMode(false);         /* 표준 모드 → 직업 선택 */
    OBS.enter();                                                           /* 출격 — infStartRun 이 모달을 닫는다 */
  }
  function ensureMenuButton() {
    if (document.getElementById('menuObserver')) return;
    const grid = document.querySelector('#crewRoot .modeGrid') || document.querySelector('.modeGrid');
    if (!grid) return;
    const n = grid.querySelectorAll('.modeBtn').length + 1;
    const b = document.createElement('button');
    b.className = 'modeBtn observer'; b.id = 'menuObserver'; b.type = 'button';
    b.innerHTML = '<i>' + String(n).padStart(2, '0') + '</i><b>관전 모드</b>'
      + '<span>OBSERVER / 4직업 완전 자동 · Tab 시점 · Esc 해제</span><u aria-hidden="true">↗</u>';
    b.addEventListener('click', (e) => { e.preventDefault(); enterFromMenu(); });
    grid.appendChild(b);
  }

  function ensureButton() {
    const panel = document.querySelector('#infRoleModal .infRolePanel');
    if (!panel || document.getElementById('obsStartBtn')) return;
    const b = document.createElement('button');
    b.id = 'obsStartBtn'; b.type = 'button';
    b.innerHTML = '👁 관전 모드 — 4직업 완전 자동 진행'
      + '<small>선택한 직업이 AI 리더 · 나머지 3직업 AI 크루 · Tab 시점 전환 · Esc 해제</small>';
    b.addEventListener('click', (e) => { e.preventDefault(); e.stopPropagation(); OBS.enter(); });
    const note = panel.querySelector('.infRoleNote');
    if (note) note.insertAdjacentElement('beforebegin', b);
    else panel.appendChild(b);
  }

  function paintBadge() {
    let el = document.getElementById('obsBadge');
    if (!el) {
      el = document.createElement('div');
      el.id = 'obsBadge';
      (document.getElementById('app') || document.body).appendChild(el);
    }
    if (!OBS.active || !ready() || !INF.active) return;
    const list = camTargets();
    const cur = list[Math.min(OBS.focus, list.length - 1)];
    if (OBS.possess) {
      el.innerHTML = '<span class="dot"></span>빙의 조종 · ' + (cur ? cur.label : '—')
        + '<span class="keys">WASD 이동 · 클릭 공격/채굴 · Space 대시 · Q/E 스킬 · Tab/F9 관전 복귀</span>';
      return;
    }
    el.innerHTML = '<span class="dot"></span>관전 모드 · 시점: ' + (cur ? cur.label : '—')
      + '<span class="keys">Tab 전환 · Esc 이 캐릭터 조종 · F9 관전 · [ ] 줌</span>';
  }

  function boot() {
    ensureCSS();
    ensureMenuButton();
    ensureButton();
    setInterval(() => { try { ensureMenuButton(); ensureButton(); autoUI(); } catch (e) {} }, 300);
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
  else boot();
})();
