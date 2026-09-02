/* ══════════════════════════════════════════════════════════════════
   AI_GEO_V1 (v7.8.1) — AI 크루·관전 리더 공용 지형 판정.
   본편 HTML 에도 같은 블록이 들어가 있다. 먼저 정의된 쪽이 이긴다.
   ══════════════════════════════════════════════════════════════════ */
(function () {
  if (window.AIGEO) return;   /* 이미 주입본이 정의했다 */
  /* ══════════════════════════════════════════════════════════════════
     AIGEO — AI 가 벽을 고르기 전에 반드시 통과해야 하는 지형 게이트
     ──────────────────────────────────────────────────────────────────
     문제: 기존 pickMine 은 "네 면 중 하나가 뚫려 있으면" 캘 수 있다고 봤다.
     그 열린 면이 기반암(rock/core) 건너편이면 AI 는 영원히 그 벽에 닿지
     못한 채 모서리에 몸을 비빈다. 목표 유효시간이 끝나도 같은 벽이 다시
     최고점을 받아 재선택되므로 루프가 끊기지 않는다.

     해법 세 겹:
       1) 도달성  — 열린 면이 "내가 서 있는 열린 공간 성분(G.comp)"에
                    속할 때만 목표가 된다. 엔진이 이미 유지하는 연결 성분을
                    그대로 쓴다 (targetsR 과 같은 기준).
       2) 접촉    — 기반암 모서리를 대각으로 관통해 파는 것을 막는다.
       3) 봉인    — 진척이 없는 벽은 일정 시간 후보에서 제외한다.
                    같은 벽을 즉시 다시 잡는 재발을 끊는 안전망.
     ══════════════════════════════════════════════════════════════════ */
  const A = {};
  const kOf = (c, r) => r * COLS + c;
  const inb = (c, r) => c >= 1 && r >= 1 && c < COLS - 1 && r < ROWS - 1;
  const hardAt = (c, r) => {
    if (c < 0 || r < 0 || c >= COLS || r >= ROWS) return true;
    const t = G.cell[kOf(c, r)];
    return typeof SOLIDX === 'function' ? !!SOLIDX(t) : (t === 'rock' || t === 'core');
  };
  const bad = (t) => !t || (typeof SOLIDX === 'function' ? SOLIDX(t) : (t === 'rock' || t === 'core'));

  /* ── 봉인 목록 ── */
  const BAN = new Map();
  A.ban = function (c, r, sec) { BAN.set(kOf(c, r), (G.t || 0) + (sec || 14)); };
  A.banned = function (c, r) {
    const k = kOf(c, r), until = BAN.get(k);
    if (until == null) return false;
    if (until <= (G.t || 0)) { BAN.delete(k); return false; }
    return true;
  };
  A.clearBans = function () { BAN.clear(); };

  /* ── 도달성 ── (x,y) 에 선 액터가 걸어가서 실제로 붙을 수 있는 벽인가 */
  A.canMine = function (x, y, c, r) {
    if (typeof G === 'undefined' || !G.cell || !inb(c, r)) return false;
    if (bad(G.cell[kOf(c, r)])) return false;
    if (A.banned(c, r)) return false;
    if (typeof compOf !== 'function' || !G.comp) return true;   /* 성분 정보가 없으면 막지 않는다 */
    const comp = compOf(x, y);
    if (comp < 0) return false;
    for (let i = 0; i < 4; i++) {
      const nc = c + (i === 0 ? 1 : i === 1 ? -1 : 0), nr = r + (i === 2 ? 1 : i === 3 ? -1 : 0);
      if (!inb(nc, nr)) continue;
      const nk = kOf(nc, nr);
      if (!G.cell[nk] && G.comp[nk] === comp) return true;
    }
    return false;
  };

  /* ── 접촉 ── 드릴이 실제로 닿는 칸인가.
       같은 칸·상하좌우는 허용, 대각은 기반암 모서리를 관통하지 않을 때만 허용. */
  A.inReach = function (x, y, c, r) {
    const mc = Math.floor(x / CELL), mr = Math.floor(y / CELL);
    const dc = Math.abs(mc - c), dr = Math.abs(mr - r);
    if (dc + dr <= 1) return true;
    if (dc === 1 && dr === 1) return !(hardAt(c, mr) && hardAt(mc, r));
    return false;
  };

  /* ── 경계벽 ── 내 열린 공간에 닿아 있는 벽 전체에서 최고점을 고른다.
       무작위 표본과 달리 "닿을 수 없는 벽"이 절대 나오지 않는다. */
  A.frontier = function (x, y, score) {
    if (typeof G === 'undefined' || !G.cell) return null;
    if (typeof compOf !== 'function' || !G.comp) return null;
    const comp = compOf(x, y);
    if (comp < 0) return null;
    let best = null, bs = -1e9;
    for (let r = 1; r < ROWS - 1; r++) for (let c = 1; c < COLS - 1; c++) {
      const t = G.cell[kOf(c, r)];
      if (bad(t) || A.banned(c, r)) continue;
      let touch = false;
      for (let i = 0; i < 4; i++) {
        const nc = c + (i === 0 ? 1 : i === 1 ? -1 : 0), nr = r + (i === 2 ? 1 : i === 3 ? -1 : 0);
        if (!inb(nc, nr)) continue;
        const nk = kOf(nc, nr);
        if (!G.cell[nk] && G.comp[nk] === comp) { touch = true; break; }
      }
      if (!touch) continue;
      const s = score(c, r, t);
      if (s > bs) { bs = s; best = { c, r, x: cxw(c), y: cyw(r), type: t }; }
    }
    return best;
  };

  /* ── 진척 감시 ── 목표 벽 체력이 줄고 있는가.
       state 는 아무 객체나 (크루 멤버 / OBS). 멈춰 있으면 false 를 돌려준다. */
  A.wallHp = function (c, r) {
    if (!inb(c, r)) return -1;
    const k = kOf(c, r), t = G.cell[k];
    if (bad(t)) return -1;
    if (G.hp && G.hp.has(k)) return G.hp.get(k);
    const mul = (typeof INF !== 'undefined' && typeof infTraitCardsEnabled === 'function' && infTraitCardsEnabled()) ? (INF.wallHpMul || 1) : 1;
    return ((typeof HPT !== 'undefined' && HPT[t]) || 100) * mul;
  };
  A.progressReset = function (s) { s._geoC = -1; s._geoR = -1; s._geoT = 0; };
  A.progress = function (s, c, r, dt, limit) {
    const hp = A.wallHp(c, r);
    if (hp < 0) { A.progressReset(s); return true; }          /* 이미 부숴짐 */
    if (s._geoC !== c || s._geoR !== r) { s._geoC = c; s._geoR = r; s._geoHp = hp; s._geoT = 0; return true; }
    if (hp < (s._geoHp == null ? hp : s._geoHp) - 0.01) { s._geoHp = hp; s._geoT = 0; return true; }
    s._geoT = (s._geoT || 0) + dt;
    if (s._geoT > (limit || 1.8)) { A.progressReset(s); return false; }
    return true;
  };

  window.AIGEO = A;
})();

/* ══════════════════════════════════════════════════════════════════════
   AI 크루 (로컬 플레이어 AI) — v1
   ──────────────────────────────────────────────────────────────────────
   목적: 어떤 모드든 직업 선택 화면에서 카드 옆 [+AI] 를 눌러 AI 동료를
   즉시 편성한다. LAN 코옵·서버·별도 창이 필요 없다. 같은 게임 인스턴스
   안에서 도는 진짜 로컬 동료다.

   설계 원칙
   1) AI 크루는 "그 직업이 실제로 할 만한 행위"를 한다. 드릴러는 길을
      파고, 거너는 적을 지우고, 스카우트는 어둠을 열고, 엔지니어는
      센트리를 세운다. 역할별 행동 예산(§0.5-5)을 코드로 명시한다.
   2) 성장(경험치·특성 카드)은 사람만 갖는다. AI가 부순 블록은 장악도에
      기여하되 사람 경험치를 올리지 않는다 (기획서 §5.2 개인 성장).
   3) AI 설치물(센트리·전력 노드)은 사람 엔지니어의 INF 시스템과 섞지
      않고 AI 소유로 따로 굴린다. 서로의 상한·전력망을 오염시키지 않는다.

   주입 지점은 ai/inject-ai-crew.py 가 관리한다.
   ══════════════════════════════════════════════════════════════════════ */
(function () {
  'use strict';

  const AI = {
    /* 편성 — 직업 선택 화면에서 만든다. 런이 끝나도 유지되어 다음 런에 재사용 */
    roster: [],
    members: [],
    turrets: [],
    nodes: [],
    max: 3,
    enabled: true,
    debug: false,
    seq: 0,
    _dist: null, _prev: null, _size: 0,
    _uiT: 0,
  };
  window.AICREW = AI;

  const ROLE_KO = { driller: '드릴러', gunner: '거너', scout: '스카우트', engineer: '엔지니어' };
  const ROLE_COL = { driller: '#ffd36e', gunner: '#ff8d72', scout: '#7febd0', engineer: '#c7a0ff' };
  const ORDER = ['driller', 'gunner', 'scout', 'engineer'];

  /* ── 역할 행동 예산 ───────────────────────────────────────────────
     "이 직업이 한 판에서 실제로 하는 일"을 수치로 적는다. AI 는 이 표를
     그대로 실행한다. 사람 플레이어의 INF 배율과 분리해 두어야 AI 를 넣어도
     사람 밸런스가 흔들리지 않는다. */
  const KIT = {
    driller: {
      digMul: 1.35,          /* 굴착 주력 — 통로를 만든다 */
      gunMul: 0.55, fireCd: 0.30, mag: 10, reload: 1.7, range: 6.5,
      drillMelee: true,      /* 드릴 팁 접촉 피해 */
      alert: 12,             /* 적을 인지하는 반경 (칸) */
      intercept: 5,          /* 파티에서 이만큼까지만 적을 쫓는다 — 본업은 굴착 */
      pathDigCost: 5,        /* 경로 탐색에서 벽을 뚫을 의지 (낮을수록 잘 판다) */
      engage: 3.6,           /* 유지하려는 교전 거리 (칸) */
      hp: 210,
      job: '굴착',
    },
    gunner: {
      digMul: 0.10,          /* 벽은 파쇄탄으로만 — 드릴 굴착은 거의 못 한다 */
      gunMul: 1.55, fireCd: 0.15, mag: 22, reload: 1.9, range: 9.5,
      pathDigCost: 30,
      alert: 20,             /* 크루에서 가장 넓게 본다 — 화력 지원의 눈 */
      intercept: 13,         /* 적극적으로 요격 나간다 */
      engage: 5.2,           /* 사거리를 벌려 쏜다 */
      hp: 240,
      breakerCd: 9, breakerRadius: 1,
      job: '화력 지원',
    },
    scout: {
      digMul: 0.42,
      gunMul: 1.0, fireCd: 0.22, mag: 14, reload: 1.5, range: 8,
      pathDigCost: 12,
      alert: 17,
      intercept: 9,
      engage: 4.6,
      hp: 175,
      flareCd: 7,            /* 어두운 전방에 플레어 */
      dashMul: 1.4,
      job: '정찰',
    },
    engineer: {
      digMul: 0.75,
      gunMul: 0.9, fireCd: 0.26, mag: 16, reload: 1.7, range: 7.5,
      pathDigCost: 10,
      alert: 14,
      intercept: 7,          /* 센트리 라인을 벗어나지 않는다 */
      engage: 4.4,
      hp: 200,
      maxTurrets: 2, turretCd: 12, turretLife: 55,
      maxNodes: 2, nodeCd: 16, nodeLife: 70, nodeRadius: 4.2,
      job: '진지 구축',
    },
  };

  /* ── 게임 접근 안전판 ──────────────────────────────────────────── */
  const ready = () => typeof G !== 'undefined' && typeof CELL !== 'undefined' && typeof CREW !== 'undefined';
  const playing = () => ready() && CREW.phase === 'play' && AI.members.length > 0;
  const inf = () => (typeof INF !== 'undefined' && INF.active ? INF : null);
  const idx = (c, r) => r * COLS + c;
  const cellOf = (c, r) => (c >= 0 && c < COLS && r >= 0 && r < ROWS ? G.cell[idx(c, r)] : 'rock');
  const unbreakable = (t) => (typeof SOLIDX === 'function' ? SOLIDX(t) : t === 'rock' || t === 'core');
  const rnd = (a, b) => a + Math.random() * (b - a);
  /* v7.8.1 — 지형 게이트 (AI_GEO_V1). 모듈이 없으면 예전 동작으로 흘려보낸다 */
  const GEO = (typeof AIGEO !== 'undefined') ? AIGEO : {
    canMine: () => true, inReach: () => true, banned: () => false,
    ban: () => {}, frontier: () => null, progress: () => true, progressReset: () => {},
  };

  /* ══════════ 편성 ══════════ */

  AI.count = function (roleId) {
    return AI.roster.filter((r) => r === roleId).length;
  };
  AI.add = function (roleId) {
    if (!KIT[roleId]) return false;
    if (AI.roster.length >= AI.max) return false;
    AI.roster.push(roleId);
    AI.paintRosterUI();
    if (playing()) spawnMember(roleId, true);   /* 런 도중에도 즉시 합류 */
    return true;
  };
  AI.remove = function (roleId) {
    const i = AI.roster.lastIndexOf(roleId);
    if (i < 0) return false;
    AI.roster.splice(i, 1);
    const m = [...AI.members].reverse().find((x) => x.roleId === roleId);
    if (m) AI.members.splice(AI.members.indexOf(m), 1);
    AI.paintRosterUI();
    return true;
  };
  AI.clear = function () {
    AI.roster.length = 0; AI.members.length = 0;
    AI.turrets.length = 0; AI.nodes.length = 0;
    AI.paintRosterUI();
  };

  /* ══════════ 런 시작 / 종료 ══════════ */

  AI.onRunStart = function () {
    AI.members.length = 0; AI.turrets.length = 0; AI.nodes.length = 0;
    if (!AI.enabled || !AI.roster.length || !ready()) return;
    for (const roleId of AI.roster) spawnMember(roleId, false);
    if (AI.members.length && typeof toast === 'function') {
      toast('AI 크루 ' + AI.members.length + '명 합류 · ' +
        AI.members.map((m) => ROLE_KO[m.roleId]).join(', '));
    }
  };
  AI.onRunEnd = function () {
    AI.members.length = 0; AI.turrets.length = 0; AI.nodes.length = 0;
  };
  /* 지층이 바뀌면 지형이 새로 생긴다 — 크루를 사람 옆으로 다시 모은다 */
  AI.onFloorInit = function () {
    for (const m of AI.members) {
      const p = freeSpotNear(G.sh.x, G.sh.y, 1.4);
      m.x = p.x; m.y = p.y; m.vx = m.vy = 0;
      m.path = []; m.goal = null; m.mineTarget = null;
      m.hp = Math.max(m.hp, m.hpMax * 0.6); m.down = false; m.downT = 0;
      m.ammo = m.mag; m.reloadLeft = 0;
      m.xpCapped = 0;                       /* 트리클 상한은 지층마다 회복 */
      m.sectors = null;                     /* 정찰 구역 기록도 지층마다 새로 */
      m.lootCd = 0; m.dodging = false;
    }
    AI.turrets.length = 0; AI.nodes.length = 0;
  };

  function freeSpotNear(x, y, cells) {
    const R = cells * CELL;
    for (let i = 0; i < 40; i++) {
      const a = Math.random() * Math.PI * 2, d = R * (0.35 + Math.random() * 0.9);
      const nx = x + Math.cos(a) * d, ny = y + Math.sin(a) * d;
      if (typeof solidAt === 'function' && !solidAt(nx, ny)) return { x: nx, y: ny };
    }
    return { x: x + rnd(-20, 20), y: y + rnd(-20, 20) };
  }

  function spawnMember(roleId, live) {
    const base = KIT[roleId];
    if (!base) return null;
    /* KIT 사본 — 특성은 이 사본을 바꾼다. 크루끼리도 서로 다른 빌드가 된다. */
    const kit = Object.assign({}, base);
    const p = freeSpotNear(G.sh.x, G.sh.y, 1.5);
    const m = {
      id: ++AI.seq, roleId, kit,
      x: p.x, y: p.y, vx: 0, vy: 0, aim: 0, face: 1,
      hp: kit.hp, hpMax: kit.hp, iframes: 0, down: false, downT: 0, reviveT: 0,
      drill: 0, drillWarm: 0, digging: false,
      gunCd: 0, ammo: kit.mag, mag: kit.mag, reloadLeft: 0, reloadTime: kit.reload,
      qCd: 0, eCd: 0, shieldT: 0, dashCd: 0, dash: null,
      flareCd: 2, turretCd: 1.5, nodeCd: 3, breakerCd: 0, breaker: [],
      lootCd: 0, lootGot: 0, dodging: false,
      goal: null, path: [], pathAge: 0, pathKey: '', react: 0,
      mineTarget: null, stuckT: 0, lastX: p.x, lastY: p.y, jitter: 0, jitterA: 0,
      say: '', sayT: 0,
      /* 개인 성장 (§5.2) — 사람과 같은 곡선, 자기 행동으로만 오른다 */
      level: 1, xp: 0,
      xpNeed: (typeof infXpNeedFor === 'function') ? infXpNeedFor(1) : 42,
      xpCapped: 0, xpTrickle: 0, traits: [], traitIds: {},
    };
    AI.members.push(m);
    if (live && typeof toast === 'function') toast('AI ' + ROLE_KO[roleId] + ' 합류');
    return m;
  }

  /* ══════════ 지형·경로 ══════════ */

  function digCost(t, kit) {
    if (!t) return 1;
    if (unbreakable(t)) return Infinity;
    const hp = ((typeof HPT !== 'undefined' && HPT[t]) || 100) * (inf() ? INF.wallHpMul || 1 : 1);
    return 1 + (hp / 100) * kit.pathDigCost;
  }

  /* 다익스트라 — 벽은 "뚫는 데 드는 시간"만큼 비싼 통로다.
     비용 계수가 직업마다 달라서 드릴러는 지름길을 파고 거너는 돌아간다. */
  function findPath(sc, sr, gc, gr, kit) {
    const N = COLS * ROWS;
    if (AI._size !== N) { AI._dist = new Float64Array(N); AI._prev = new Int32Array(N); AI._size = N; }
    const dist = AI._dist, prev = AI._prev;
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
        const nk = idx(nc, nr), w = digCost(G.cell[nk], kit);
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

  function ensurePath(m, gx, gy) {
    const c0 = Math.floor(m.x / CELL), r0 = Math.floor(m.y / CELL);
    const gc = Math.floor(gx / CELL), gr = Math.floor(gy / CELL);
    const key = gc + ',' + gr;
    if (key !== m.pathKey || m.pathAge <= 0 || !m.path.length) {
      m.path = findPath(c0, r0, gc, gr, m.kit) || [];
      m.pathKey = key; m.pathAge = 0.4;
    }
    while (m.path.length) {
      const k = m.path[0], c = k % COLS, r = (k - c) / COLS;
      if (!G.cell[k] && Math.hypot(cxw(c) - m.x, cyw(r) - m.y) < CELL * 0.5) m.path.shift();
      else break;
    }
    return m.path.length ? m.path[0] : -1;
  }

  /* ══════════ 이동 ══════════ */

  function steer(m, tx, ty, dt, speedMul) {
    const dx = tx - m.x, dy = ty - m.y, d = Math.hypot(dx, dy);
    if (d < 3) { m.vx *= 0.7; m.vy *= 0.7; return; }
    let nx = dx / d, ny = dy / d;
    if (m.jitter > 0) { nx = Math.cos(m.jitterA); ny = Math.sin(m.jitterA); }
    /* 동료끼리 겹치지 않게 밀어낸다 */
    let px = 0, py = 0;
    for (const o of AI.members) {
      if (o === m) continue;
      const ox = m.x - o.x, oy = m.y - o.y, od = Math.hypot(ox, oy);
      if (od > 1 && od < R_SHELLY * 1.9) { px += (ox / od) * (1 - od / (R_SHELLY * 1.9)); py += (oy / od) * (1 - od / (R_SHELLY * 1.9)); }
    }
    {
      const ox = m.x - G.sh.x, oy = m.y - G.sh.y, od = Math.hypot(ox, oy);
      if (od > 1 && od < R_SHELLY * 1.9) { px += (ox / od) * 0.8; py += (oy / od) * 0.8; }
    }
    nx += px * 0.9; ny += py * 0.9;
    const n = Math.hypot(nx, ny) || 1;
    const spd = teMovePx() * (speedMul == null ? 1 : speedMul) * (m.kit.moveMul || 1);
    m.vx = (nx / n) * spd; m.vy = (ny / n) * spd;
  }

  function moveAwayFrom(m, tx, ty, dt, speedMul) {
    steer(m, m.x * 2 - tx, m.y * 2 - ty, dt, speedMul);
  }

  function applyMotion(m, dt) {
    if (m.dash) {
      m.x += m.dash.vx * dt; m.y += m.dash.vy * dt;
      collide(m, R_SHELLY);
      m.dash.t -= dt;
      if (m.dash.t <= 0) m.dash = null;
      return;
    }
    m.x += m.vx * dt; m.y += m.vy * dt;
    collide(m, R_SHELLY);
    m.vx *= 0.82; m.vy *= 0.82;
  }

  function tryDashAI(m, dirX, dirY) {
    if (m.dash || m.dashCd > 0) return;
    const d = Math.hypot(dirX, dirY) || 1;
    const dist = teWorld(TE.dashDist) * (m.kit.dashMul || 1), dur = TE.dashDur;
    m.dash = { vx: (dirX / d) * (dist / dur), vy: (dirY / d) * (dist / dur), t: dur };
    m.dashCd = TE.dashCd;
  }

  /* ══════════ 전투 ══════════ */

  /* 총알이 실제로 닿는 거리 — 사람 총과 같은 제원에서 계산한다 */
  const FIRE_RANGE = () => (teWorld(280) * 1.2) / CELL * 0.92;

  function canSee(ax, ay, bx, by) {
    return typeof sightClear !== 'function' || sightClear(ax, ay, bx, by);
  }

  /* 위협 목록 — "동료라면 반응했을 적"을 고른다.
     1) 내가 직접 보는 적
     2) 사람이 보고 있는 적 (팀은 서로 알린다)
     3) 사람에게 달라붙은 적은 거리와 무관하게 최우선
     이 셋을 합치지 않으면 AI 는 좁은 시야 때문에 전투를 통째로 놓친다. */
  function threats(m) {
    const out = [];
    if (!G.enemies) return out;
    const alertR = CELL * (m.kit.alert || 12);
    const partyR = CELL * 9;
    for (const e of G.enemies) {
      if (e.hp <= 0) continue;
      const dm = Math.hypot(e.x - m.x, e.y - m.y);
      const dp = Math.hypot(e.x - G.sh.x, e.y - G.sh.y);
      let known = false, prio = 0;
      if (dp < partyR && canSee(G.sh.x, G.sh.y, e.x, e.y)) { known = true; prio = 100 - dp / CELL; }
      else if (dm < alertR && canSee(m.x, m.y, e.x, e.y)) { known = true; prio = 60 - dm / CELL; }
      else if (dp < alertR && canSee(G.sh.x, G.sh.y, e.x, e.y)) { known = true; prio = 40 - dp / CELL; }
      if (!known) continue;
      if (e.atkState) prio += 12;            /* 지금 때리는 중인 적을 먼저 */
      if (e.elite || e.apex) prio += 8;
      out.push({ e, d: dm, dp, prio });
    }
    out.sort((a, b) => b.prio - a.prio);
    return out;
  }
  function enemiesNear(m, cells, needSight) {
    const out = [];
    if (!G.enemies) return out;
    const max = CELL * cells;
    for (const e of G.enemies) {
      if (e.hp <= 0) continue;
      const d = Math.hypot(e.x - m.x, e.y - m.y);
      if (d > max) continue;
      if (needSight && !canSee(m.x, m.y, e.x, e.y)) continue;
      out.push({ e, d });
    }
    out.sort((a, b) => a.d - b.d);
    return out;
  }

  /* AI 사격 — 사람의 특성 배율을 타지 않도록 p.ai 로 표시해서 쏜다 */
  function fire(m, tx, ty) {
    if (m.gunCd > 0 || m.reloadLeft > 0) return false;
    if (m.ammo <= 0) { m.reloadLeft = m.reloadTime; return false; }
    m.ammo--;
    const a = Math.atan2(ty - m.y, tx - m.x) + rnd(-0.045, 0.045);
    const ca = Math.cos(a), sa = Math.sin(a);
    const sp = teWorld(280);
    G.projectiles.push({
      x: m.x + ca * R_SHELLY * 0.9, y: m.y + sa * R_SHELLY * 0.9,
      vx: ca * sp, vy: sa * sp, life: 1.2,
      pierce: 0, bounces: 0, explosive: false, laser: false,
      power: 1, lastCell: -1, ai: 1, aiMul: m.kit.gunMul, aiOwner: m.id,
      visualId: 'standard', visualAge: 0, visualRot: 0, trail: [],
    });
    if (window.TUNNEL_PROJECTILE_FX) {
      window.TUNNEL_PROJECTILE_FX.muzzle(m.x + ca * R_SHELLY * 0.9, m.y + sa * R_SHELLY * 0.9, a, 'standard');
    }
    m.gunCd = m.kit.fireCd;
    if (m.ammo <= 0) m.reloadLeft = m.reloadTime;
    return true;
  }

  /* 드릴 접촉 피해 — 드릴러가 벽을 파는 김에 붙은 적을 갈아버린다 */
  function drillMelee(m, dt) {
    if (!m.kit.drillMelee || !m.digging) return;
    const ca = Math.cos(m.aim), sa = Math.sin(m.aim);
    const tx = m.x + ca * DRILL_TIP, ty = m.y + sa * DRILL_TIP;
    for (const e of G.enemies) {
      if (e.hp <= 0) continue;
      if (Math.hypot(tx - e.x, ty - e.y) < e.r + 8) {
        AI.dmgSrc = m;
        try { hurtEnemy(e, shelDps() * DEMO.enemyDrillMul * DRILL_DMG() * m.kit.digMul * dt, ca, sa); }
        finally { AI.dmgSrc = null; }
      }
    }
  }


  /* ══════════════════════════════════════════════════════════════
     개인 성장 (§5.2) — AI 크루도 자기 행동으로 경험치를 얻고, 자기
     레벨업 시점에 자기 특성을 고른다. 사람과 경험치 곡선·역할 가중치를
     공유하므로 "네 명이 각자 다른 속도로 다른 빌드가 된다"가 성립한다.

     본편의 INF_TRAITS 는 전역 INF 를 직접 바꾸는 함수라 그대로 쓸 수 없다.
     그래서 같은 계열·같은 감각의 AI 전용 풀을 두고, 효과는 각 크루의
     KIT 사본에만 적용한다. 사람 빌드는 절대 건드리지 않는다.
     ══════════════════════════════════════════════════════════════ */

  const XP = () => (typeof INF_XP_TABLE !== 'undefined' ? INF_XP_TABLE
    : { dirt: 1, stone: 2, rare: 4, kill: 4, eliteMul: 1.7, apexMul: 2.4, killBoss: 45, turretKill: 5 });
  const XP_FLOOR_CAP = 60;

  function xpWeight(m, kind) {
    const row = (typeof INF_XP_WEIGHT !== 'undefined' && INF_XP_WEIGHT[kind]) || null;
    const w = row ? row[m.roleId] : null;
    return w == null ? 1 : w;
  }
  function xpNeedFor(L) {
    return (typeof infXpNeedFor === 'function') ? infXpNeedFor(L) : Math.round(30 + 10 * L + 2.4 * L * L);
  }
  function awardXp(m, base, kind, opts) {
    if (!(base > 0) || m.down) return 0;
    const o = opts || {};
    if (o.capped && (m.xpCapped || 0) >= XP_FLOOR_CAP) return 0;
    const growth = (typeof INF !== 'undefined' && INF.growthMultiplier) || 1;
    const gain = Math.max(1, Math.round(base * xpWeight(m, kind) * growth));
    m.xp += gain;
    if (o.capped) m.xpCapped = (m.xpCapped || 0) + gain;
    if (o.x != null && typeof J !== 'undefined' && J.text && Math.random() < 0.45) {
      J.text(o.x, o.y - 24, '+' + gain, ROLE_COL[m.roleId], 10);
    }
    checkLevel(m);
    return gain;
  }
  function checkLevel(m) {
    let guard = 0;
    while (m.xp >= m.xpNeed && guard++ < 4) {
      m.xp -= m.xpNeed;
      m.level++;
      m.xpNeed = xpNeedFor(m.level);
      pickTrait(m);
    }
  }

  /* 레벨이 오를수록 상위 티어가 열린다 — 사람의 카드 곡선과 같은 감각 */
  function maxTier(level) { return level < 3 ? 1 : level < 6 ? 2 : level < 9 ? 3 : 4; }

  /* AI 전용 특성 풀. a(m) 은 그 크루의 KIT 사본만 바꾼다. */
  const AI_TRAITS = [
    /* ── 드릴러: 길을 연다 ── */
    { role: 'driller', id: 'd_motor', tier: 1, n: '토크 증폭 모터', d: '굴착 +18%', a: (m) => m.kit.digMul *= 1.18 },
    { role: 'driller', id: 'd_rivet', tier: 1, n: '리벳 강화 약실', d: '사격 +22%', a: (m) => m.kit.gunMul *= 1.22 },
    { role: 'driller', id: 'd_frame', tier: 1, n: '경량 프레임', d: '이동 +8% · 체력 +30', a: (m) => { m.kit.moveMul *= 1.08; m.hpMax += 30; m.hp += 30; } },
    { role: 'driller', id: 'd_fault', tier: 2, n: '단층 추적 비트', d: '굴착 +25% · 통로 개척 적극', a: (m) => { m.kit.digMul *= 1.25; m.kit.pathDigCost *= 0.8; } },
    { role: 'driller', id: 'd_guard', tier: 2, n: '작업 구역 방호', d: '체력 +55 · 근접 교전 허용', a: (m) => { m.hpMax += 55; m.hp += 55; m.kit.engage = Math.max(2.4, m.kit.engage - 0.4); } },
    { role: 'driller', id: 'd_seismic', tier: 3, n: '지진 공진축', d: '굴착 +32% · 드릴 접촉 피해 증가', a: (m) => m.kit.digMul *= 1.32 },
    { role: 'driller', id: 'd_mantle', tier: 4, n: '맨틀 천공 키', d: '굴착 +45% · 이동 +10%', a: (m) => { m.kit.digMul *= 1.45; m.kit.moveMul *= 1.10; } },

    /* ── 거너: 적을 지운다 ── */
    { role: 'gunner', id: 'g_cycler', tier: 1, n: '고속 약실 순환기', d: '연사 +18%', a: (m) => m.kit.fireCd *= 0.85 },
    { role: 'gunner', id: 'g_belt', tier: 1, n: '확장 급탄 벨트', d: '탄창 +6', ok: (m) => m.kit.mag < 40, a: (m) => m.kit.mag += 6 },
    { role: 'gunner', id: 'g_liner', tier: 1, n: '텅스텐 라이너', d: '화력 +20%', a: (m) => m.kit.gunMul *= 1.20 },
    { role: 'gunner', id: 'g_optic', tier: 2, n: '전술 조준경', d: '인지 +4칸 · 요격 +3칸', a: (m) => { m.kit.alert += 4; m.kit.intercept += 3; } },
    { role: 'gunner', id: 'g_radius', tier: 2, n: '파쇄 확장 슬리브', d: '파쇄 반경 +1칸', ok: (m) => m.kit.breakerRadius < 2, a: (m) => m.kit.breakerRadius = 2 },
    { role: 'gunner', id: 'g_quick', tier: 2, n: '속사 재장전', d: '재장전 -25%', a: (m) => m.kit.reload *= 0.75 },
    { role: 'gunner', id: 'g_fuse', tier: 3, n: '고속 신관', d: '파쇄탄 재사용 -30%', a: (m) => m.kit.breakerCd *= 0.7 },
    { role: 'gunner', id: 'g_storm', tier: 4, n: '지속 사격 교리', d: '연사 +20% · 화력 +25%', a: (m) => { m.kit.fireCd *= 0.8; m.kit.gunMul *= 1.25; } },

    /* ── 스카우트: 어둠을 연다 ── */
    { role: 'scout', id: 's_boots', tier: 1, n: '경량 부츠', d: '이동 +14%', a: (m) => m.kit.moveMul *= 1.14 },
    { role: 'scout', id: 's_flare', tier: 1, n: '플레어 증설', d: '플레어 주기 -30%', a: (m) => m.kit.flareCd *= 0.7 },
    { role: 'scout', id: 's_carbine', tier: 1, n: '카빈 총열 개조', d: '사격 +20%', a: (m) => m.kit.gunMul *= 1.20 },
    { role: 'scout', id: 's_optic', tier: 2, n: '야간 광학', d: '인지 +5칸', a: (m) => m.kit.alert += 5 },
    { role: 'scout', id: 's_cutter', tier: 2, n: '절삭기 출력 증폭', d: '굴착 +35%', a: (m) => { m.kit.digMul *= 1.35; m.kit.pathDigCost *= 0.85; } },
    { role: 'scout', id: 's_evade', tier: 3, n: '회피 기동', d: '대시 +20% · 이동 +10%', a: (m) => { m.kit.dashMul *= 1.2; m.kit.moveMul *= 1.10; } },
    { role: 'scout', id: 's_beacon', tier: 4, n: '지속 조명탄', d: '플레어 주기 -40% · 인지 +5칸', a: (m) => { m.kit.flareCd *= 0.6; m.kit.alert += 5; } },

    /* ── 엔지니어: 공간을 만든다 ── */
    { role: 'engineer', id: 'e_mag', tier: 1, n: '확장 탄약 호퍼', d: '센트리 탄창 +8', a: (m) => { m.kit.turretMag += 8; for (const t of AI.turrets) if (t.owner === m.id) { t.mag += 8; t.ammo += 8; } } },
    { role: 'engineer', id: 'e_cutter', tier: 1, n: '공학 커터 증폭', d: '굴착 +28%', a: (m) => m.kit.digMul *= 1.28 },
    { role: 'engineer', id: 'e_grid', tier: 1, n: '전력망 확장', d: '노드 반경 +1.2칸', a: (m) => m.kit.nodeRadius += 1.2 },
    { role: 'engineer', id: 'e_rate', tier: 2, n: '센트리 사격 통제', d: '센트리 연사 +25% · 위력 +20%', a: (m) => { m.kit.turretRate *= 0.8; m.kit.turretPower *= 1.2; } },
    { role: 'engineer', id: 'e_fast', tier: 2, n: '신속 설치 키트', d: '설치 대기 -30%', a: (m) => { m.kit.turretCd *= 0.7; m.kit.nodeCd *= 0.7; } },
    { role: 'engineer', id: 'e_third', tier: 3, n: '3번 슬롯 개방', d: '센트리 최대 +1기', ok: (m) => m.kit.maxTurrets < 3, a: (m) => m.kit.maxTurrets = 3 },
    { role: 'engineer', id: 'e_range', tier: 3, n: '장거리 사통 장치', d: '센트리 사거리 +2칸 · 수명 +25', a: (m) => { m.kit.turretRange += 2; m.kit.turretLife += 25; } },
    { role: 'engineer', id: 'e_fortress', tier: 4, n: '이동 요새 교리', d: '센트리 위력 +35% · 노드 반경 +1.5칸', a: (m) => { m.kit.turretPower *= 1.35; m.kit.nodeRadius += 1.5; } },
  ];

  function pickTrait(m) {
    const cap = maxTier(m.level);
    const pool = AI_TRAITS.filter((t) => t.role === m.roleId && t.tier <= cap
      && !m.traitIds[t.id] && (!t.ok || t.ok(m)));
    if (!pool.length) {
      /* 풀이 마르면 성장이 멈추지 않게 기본 숙련으로 대체한다 (§17.4-6 과 같은 취지) */
      applyTrait(m, {
        id: 'basic', n: '숙련',
        a: (mm) => { mm.kit.digMul *= 1.06; mm.kit.gunMul *= 1.06; mm.hpMax += 12; mm.hp += 12; },
      }, true);
      return;
    }
    /* 높은 티어를 선호하되 결정적이지 않게 — 크루마다 빌드가 갈린다 */
    pool.sort((a, b) => (b.tier + Math.random() * 1.4) - (a.tier + Math.random() * 1.4));
    applyTrait(m, pool[0], false);
  }
  function applyTrait(m, t, repeatable) {
    try { t.a(m); } catch (e) { console.error('[AICREW] trait', t.id, e); }
    if (!repeatable) m.traitIds[t.id] = true;
    m.traits.push(t.n);
    /* KIT 사본이 바뀌면 파생 수치도 따라간다 */
    m.reloadTime = m.kit.reload;
    m.mag = m.kit.mag;
    if (typeof J !== 'undefined' && J.text) {
      J.text(m.x, m.y - 52, 'Lv' + m.level + ' · ' + t.n, ROLE_COL[m.roleId], 14);
      J.ring(m.x, m.y, ROLE_COL[m.roleId], 6, CELL * 1.1, 2.4);
    }
    say(m, 'Lv' + m.level + ' ' + t.n);
    if (typeof toast === 'function') toast('AI ' + ROLE_KO[m.roleId] + ' Lv' + m.level + ' · ' + t.n);
  }

  /* ── 경험치 획득처 ── */
  /* 처치 — 어떤 크루의 피해로 죽었는지는 AI.dmgSrc 가 들고 있다 */
  AI.dmgSrc = null;
  AI.ownerOf = function (p) {
    if (!p || p.aiOwner == null) return null;
    return AI.members.find((m) => m.id === p.aiOwner) || null;
  };
  AI.awardKill = function (e, src) {
    const m = AI.dmgSrc;
    if (!m || !AI.members.includes(m)) return;
    const T = XP();
    if (e.boss) { awardXp(m, T.killBoss || 45, 'combat', { x: e.x, y: e.y }); return; }
    const tier = e.apex ? (T.apexMul || 2.4) : (e.elite ? (T.eliteMul || 1.7) : 1);
    if (src === 'turret') awardXp(m, (T.turretKill || 5) * tier, 'support', { x: e.x, y: e.y });
    else awardXp(m, (T.kill || 4) * tier, 'combat', { x: e.x, y: e.y });
  };
  /* 정찰 경험치 — 사람의 scoutExploreXp 와 같은 규칙으로 '새 구역'에만 준다.
     플레어를 던지는 행위 자체에 주면 쿨다운 단축 특성과 맞물려 자기가속하고,
     스카우트만 혼자 레벨이 폭주한다(1차 구현에서 실제로 그랬다). */
  function reconAward(m, x, y) {
    const sw = Math.ceil(COLS / 3);
    const key = Math.floor(Math.floor(x / CELL) / 3) + Math.floor(Math.floor(y / CELL) / 3) * sw;
    if (!m.sectors) m.sectors = new Set();
    if (m.sectors.has(key)) { awardXp(m, 1, 'recon', { capped: true }); return; }
    m.sectors.add(key);
    const base = (typeof INF !== 'undefined' && INF.scoutExploreXp) || 2;
    awardXp(m, base, 'recon', { x: x, y: y });
  }

  /* 시간 트리클 — 사람의 지층당 상한과 같은 취지. 조용한 역할도 뒤처지지 않게. */
  function xpTrickle(m, dt) {
    m.xpTrickle = (m.xpTrickle || 0) + dt;
    if (m.xpTrickle < 6) return;
    m.xpTrickle = 0;
    awardXp(m, 2, 'objective', { capped: true });
  }

  /* ══════════ 굴착 ══════════ */

  /* AI 가 부순 블록: 장악도·코어는 팀에 기여하고, 경험치는 판 크루 개인에게 간다.
     사람의 경험치와 특성 발동은 건드리지 않는다 (§5.2). */
  AI.creditBreak = function (type, x, y) {
    const raw = AI.breakSrc;
    const src = raw && raw.roleId ? raw : AI.ownerOf(raw);
    const I = inf();
    if (I) {
      INF.floorBroken++; INF.totalBlocks++;
      INF.spawnDebt += 0.3 + Math.min(0.42, INF.floorBroken * 0.006);
      G.enemyCd = Math.min(G.enemyCd, Math.max(0.35, 2.1 - INF.floorBroken * 0.018));
      if (type === 'ore' || type === 'gem' || type === 'crys') INF.core += 1;
      if (!INF.bossSpawned && typeof infDominanceTarget === 'function'
        && INF.floorBroken / Math.max(1, INF.totalBreakable) >= infDominanceTarget()
        && typeof infSpawnBoss === 'function') infSpawnBoss();
    }
    if (src) {
      const T = XP();
      const base = type === 'stone' ? (T.stone || 2)
        : (type === 'ore' || type === 'gem' || type === 'crys') ? (T.rare || 4) : (T.dirt || 1);
      awardXp(src, base, 'dig', { x: x, y: y });
    }
  };

  function digAt(m, c, r, dt) {
    const t = cellOf(c, r);
    if (!t || unbreakable(t)) return false;
    const wx = cxw(c), wy = cyw(r);
    m.aim = Math.atan2(wy - m.y, wx - m.x);
    if (Math.hypot(wx - m.x, wy - m.y) > CELL * 1.35) return false;   /* 너무 멀면 못 판다 */
    if (!GEO.inReach(m.x, m.y, c, r)) return false;   /* v7.8.1 — 기반암 모서리 대각 관통 금지 */
    m.digging = true; m.drill = 1;
    const nx = Math.cos(m.aim), ny = Math.sin(m.aim);
    const dv = shelDps() * DRILL_DMG() * m.kit.digMul * dt;
    AI.breakSrc = m;
    try { damage(c, r, dv, nx, ny, true); } finally { AI.breakSrc = null; }
    if (Math.random() < dt * 5) J.burst(wx - nx * CELL * 0.3, wy - ny * CELL * 0.3, 2, ['#E8CBA6', '#FFF3D6'], 70);
    return true;
  }

  /* 거너의 파쇄탄 — 드릴이 없으니 벽은 이걸로 뚫는다 */
  function fireBreaker(m, c, r) {
    if (m.breakerCd > 0) return false;
    const wx = cxw(c), wy = cyw(r);
    if (Math.hypot(wx - m.x, wy - m.y) > CELL * 7) return false;
    m.breaker.push({ x: wx, y: wy, c, r, t: 2.0 });
    m.breakerCd = m.kit.breakerCd;
    m.aim = Math.atan2(wy - m.y, wx - m.x);
    J.burst(m.x + Math.cos(m.aim) * 20, m.y + Math.sin(m.aim) * 20, 8, ['#ff8d72', '#ffd36e'], 170);
    return true;
  }
  function updateBreakers(m, dt) {
    m.breakerCd = Math.max(0, m.breakerCd - dt);
    for (let i = m.breaker.length - 1; i >= 0; i--) {
      const b = m.breaker[i];
      b.t -= dt;
      if (b.t > 0) continue;
      m.breaker.splice(i, 1);
      J.ring(b.x, b.y, '#ff8d72', 12, CELL * 1.8, 4);
      J.burst(b.x, b.y, 24, ['#ff6f45', '#ffd36e', '#fff3d6'], 300);
      SFX.brk && SFX.brk();
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
      for (const e of G.enemies) {
        if (e.hp <= 0) continue;
        const d = Math.hypot(e.x - b.x, e.y - b.y);
        if (d < CELL * 1.8) {
          AI.dmgSrc = m;
          try { hurtEnemy(e, DEMO.enemyGunDmg * 2.2 * m.kit.gunMul, (e.x - b.x) / (d || 1), (e.y - b.y) / (d || 1)); }
          finally { AI.dmgSrc = null; }
        }
      }
    }
  }

  /* ══════════ 설치물 — 엔지니어 ══════════ */

  function placeTurret(m) {
    const kit = m.kit;
    const mine = AI.turrets.filter((t) => t.owner === m.id);
    if (mine.length >= kit.maxTurrets) {
      const old = mine[0];
      AI.turrets.splice(AI.turrets.indexOf(old), 1);
      J.text(old.x, old.y - 20, '회수', '#C7A0FF', 12);
    }
    const p = freeSpotNear(m.x, m.y, 1.2);
    AI.turrets.push({
      owner: m.id, x: p.x, y: p.y, aim: m.aim, cd: 0.2,
      life: kit.turretLife, maxLife: kit.turretLife,
      ammo: kit.turretMag, mag: kit.turretMag, reload: 0,
      range: kit.turretRange, rate: kit.turretRate, power: kit.turretPower,
    });
    m.turretCd = kit.turretCd;
    awardXp(m, 2, 'support', { x: p.x, y: p.y, capped: true });   /* 설치 자체는 반복 생산 — 상한 적용 */
    J.burst(p.x, p.y, 12, ['#C7A0FF', '#FFF'], 140);
    J.ring(p.x, p.y, '#C7A0FF', 6, CELL * 1.1, 2.4);
    SFX.cache && SFX.cache();
    say(m, '센트리 설치');
    return true;
  }

  function placeNode(m) {
    const kit = m.kit;
    const mine = AI.nodes.filter((n) => n.owner === m.id);
    if (mine.length >= kit.maxNodes) {
      const old = mine[0];
      AI.nodes.splice(AI.nodes.indexOf(old), 1);
    }
    AI.nodes.push({ owner: m.id, x: m.x, y: m.y, life: kit.nodeLife, maxLife: kit.nodeLife, radius: kit.nodeRadius });
    m.nodeCd = kit.nodeCd;
    awardXp(m, 2, 'support', { x: m.x, y: m.y, capped: true });
    /* 전력 노드는 주변을 밝힌다 — 팀 시야에 실제로 기여한다 */
    G.lamps.push({ c: 0, r: 0, x: m.x, y: m.y, ph: Math.random() * 6, rad: DEMO.lampRadius * 0.9, ttl: kit.nodeLife, flare: 1, visionRange: 3 });
    if (typeof LOS !== 'undefined' && LOS.markDirty) LOS.markDirty();
    J.ring(m.x, m.y, '#7FEBD0', 6, CELL * 1.3, 2.6);
    say(m, '전력 노드');
    return true;
  }

  function updateInstallations(dt) {
    /* 전력 노드 — 반경 안의 센트리에 급전 */
    for (let i = AI.nodes.length - 1; i >= 0; i--) {
      const n = AI.nodes[i];
      n.life -= dt;
      if (n.life <= 0) AI.nodes.splice(i, 1);
    }
    for (let i = AI.turrets.length - 1; i >= 0; i--) {
      const t = AI.turrets[i];
      t.life -= dt;
      if (t.life <= 0) { AI.turrets.splice(i, 1); J.burst(t.x, t.y, 8, ['#685674', '#C7A0FF'], 90); continue; }
      t.powered = AI.nodes.some((n) => Math.hypot(n.x - t.x, n.y - t.y) <= CELL * n.radius);
      t.cd = Math.max(0, t.cd - dt);
      if (t.reload > 0) { t.reload = Math.max(0, t.reload - dt); if (t.reload <= 0) t.ammo = t.mag; continue; }
      if (!t.powered) continue;                     /* 급전 없으면 침묵 — 노드의 의미 */
      if (t.ammo <= 0) { t.reload = 2.2; continue; }
      if (t.cd > 0) continue;
      let best = null, bd = 1e9;
      for (const e of G.enemies) {
        if (e.hp <= 0) continue;
        const d = Math.hypot(e.x - t.x, e.y - t.y);
        if (d < bd && d <= CELL * t.range && (typeof sightClear !== 'function' || sightClear(t.x, t.y, e.x, e.y))) { bd = d; best = e; }
      }
      if (!best) continue;
      t.aim = Math.atan2(best.y - t.y, best.x - t.x);
      t.ammo--; t.cd = t.rate || 0.34;
      const ca = Math.cos(t.aim), sa = Math.sin(t.aim), sp = teWorld(320);
      G.projectiles.push({
        x: t.x + ca * 16, y: t.y + sa * 16, vx: ca * sp, vy: sa * sp, life: 1.0,
        pierce: 0, bounces: 0, explosive: false, laser: false,
        power: 1, lastCell: -1, ai: 1, aiMul: t.power || 0.72, srcTurret: 1, aiOwner: t.owner,
        visualId: 'standard', visualAge: 0, visualRot: 0, trail: [],
      });
    }
  }

  /* ══════════ 피해 / 다운 / 구조 ══════════ */

  AI.hurt = function (m, dmg, nx, ny) {
    if (!m || m.down || m.iframes > 0) return false;
    if (m.shieldT > 0) dmg *= 0.35;
    m.hp = Math.max(0, m.hp - dmg);
    m.iframes = DEMO.playerIFrame;
    m.x -= nx * DEMO.enemyKnock * 0.3; m.y -= ny * DEMO.enemyKnock * 0.3;
    collide(m, R_SHELLY);
    J.text(m.x, m.y - 30, '-' + Math.round(dmg), '#FF6B6B', 14);
    if (m.hp <= 0) {
      m.down = true; m.downT = 0; m.reviveT = 0;
      m.vx = m.vy = 0; m.dash = null; m.digging = false;
      J.ring(m.x, m.y, '#FF557D', 8, CELL * 1.4, 3);
      toast && toast('AI ' + ROLE_KO[m.roleId] + ' 다운 — 접근해 구조');
    }
    return true;
  };
  /* 좌표로 맞는 판정 — 적 투사체가 쓴다 */
  AI.hitTest = function (x, y, r) {
    for (const m of AI.members) {
      if (m.down) continue;
      if (Math.hypot(m.x - x, m.y - y) < R_SHELLY + (r || 0)) return m;
    }
    return null;
  };
  /* 적이 노릴 수 있는 크루 — 사람 + 살아있는 AI */
  AI.targets = function () {
    const out = [{ x: G.sh.x, y: G.sh.y, player: true }];
    for (const m of AI.members) if (!m.down) out.push({ x: m.x, y: m.y, member: m });
    return out;
  };

  function updateDown(m, dt) {
    m.downT += dt;
    /* 구조 — 사람이든 다른 AI든 옆에 있으면 일으킨다 (§9.2) */
    let helper = Math.hypot(G.sh.x - m.x, G.sh.y - m.y) < CELL * 1.6;
    if (!helper) helper = AI.members.some((o) => o !== m && !o.down && Math.hypot(o.x - m.x, o.y - m.y) < CELL * 1.6);
    if (helper) {
      m.reviveT += dt;
      if (Math.random() < dt * 8) J.burst(m.x, m.y - 10, 2, ['#7FEBD0', '#FFF'], 60);
      if (m.reviveT >= 3) {
        m.down = false; m.hp = m.hpMax * 0.5; m.iframes = 1.4; m.reviveT = 0;
        J.flash(m.x, m.y, 40, 'rgba(127,235,208,.8)');
        toast && toast('AI ' + ROLE_KO[m.roleId] + ' 구조 완료');
      }
    } else m.reviveT = Math.max(0, m.reviveT - dt * 0.5);
  }

  function say(m, text) { m.say = text; m.sayT = 1.8; }


  /* ══════════════════════════════════════════════════════════════
     보스 투사체 회피
     보스탄은 (tx,ty) 에 rad 반경으로 예고 후 착탄한다. 즉 "피할 수 있게"
     설계된 공격이다. 사람이 피하는 걸 AI 가 못 피하면 동료가 아니라 짐이다.
     예고 원 안에 있으면 사격은 유지한 채 원 밖으로만 빠진다.
     ══════════════════════════════════════════════════════════════ */
  function dodgeBossShot(m, dt) {
    const I = inf();
    if (!I) return false;
    let danger = null, bestEta = 1e9;
    const scan = (s, eta) => {
      if (eta > 1.8) return;                       /* 아직 먼 예고는 무시 */
      const d = Math.hypot(m.x - s.tx, m.y - s.ty);
      if (d > s.rad * 1.15) return;                /* 이미 원 밖 */
      if (eta < bestEta) { bestEta = eta; danger = { s: s, d: d, eta: eta }; }
    };
    for (const s of I.bossShots || []) scan(s, Math.max(0, s.flight - s.t));
    for (const q of I.bossShotQueue || []) scan(q, Math.max(0, q.delay || 0) + (q.flight || 0));
    if (!danger) { m.dodging = false; return false; }

    m.dodging = true;
    let nx = m.x - danger.s.tx, ny = m.y - danger.s.ty;
    const n = Math.hypot(nx, ny);
    if (n < 1) { const a = Math.random() * Math.PI * 2; nx = Math.cos(a); ny = Math.sin(a); }
    else { nx /= n; ny /= n; }
    const need = danger.s.rad * 1.3 - danger.d;
    steer(m, m.x + nx * (need + CELL), m.y + ny * (need + CELL), dt, 1.2);
    /* 코앞이면 대시로 확실히 뺀다 */
    if (danger.eta < 0.6 && m.dashCd <= 0 && need > CELL * 0.5) tryDashAI(m, nx, ny);
    say(m, '회피');
    return true;
  }

  /* 보스탄 착탄 — 사람만 맞고 AI 는 안 맞으면 회피가 의미 없다 */
  AI.bossShotHit = function (s) {
    if (!AI.members.length) return;
    const spec = (typeof bossSpec === 'function') ? bossSpec() : null;
    let dmg = spec ? Math.max(0, Math.round(spec.projectileDamage)) : 0;
    if (!dmg) dmg = Math.max(9, Math.round(DEMO.enemyDmg * 3.55));
    for (const m of AI.members) {
      if (m.down) continue;
      if (Math.hypot(m.x - s.tx, m.y - s.ty) > s.rad) continue;
      const dx = m.x - s.tx, dy = m.y - s.ty, d = Math.hypot(dx, dy) || 1;
      AI.hurt(m, dmg, dx / d, dy / d);
    }
  };

  /* ══════════════════════════════════════════════════════════════
     전투 중 지원 행동
     "적을 쏜다"가 모든 것을 밀어내면 안 된다. 엔지니어는 교전 중에도
     센트리를 세워야 하고, 스카우트는 교전 구역을 밝혀야 한다. 이 함수는
     사격·이동과 경쟁하지 않는다 — 같은 프레임에 같이 일어난다.
     ══════════════════════════════════════════════════════════════ */
  function combatSupport(m, goal, dt) {
    const kit = m.kit, e = goal.enemy;
    const fx = e ? e.x : goal.x, fy = e ? e.y : goal.y;

    if (m.roleId === 'engineer') {
      const mine = AI.turrets.filter((t) => t.owner === m.id);
      /* 슬롯이 비었으면 무조건 세운다. 슬롯이 찼어도 교전 지점에서 멀면
         현장으로 옮겨 세운다 — 뒤에 남은 센트리는 전투에 기여하지 않는다. */
      const useless = mine.length && mine.every((t) => Math.hypot(t.x - fx, t.y - fy) > CELL * (kit.turretRange + 2));
      if (m.turretCd <= 0 && (mine.length < kit.maxTurrets || useless)) { placeTurret(m); return; }
      /* 급전을 못 받는 센트리가 있으면 노드를 세워 살린다 */
      const dead = mine.find((t) => !t.powered);
      if (dead && m.nodeCd <= 0 && Math.hypot(dead.x - m.x, dead.y - m.y) < CELL * kit.nodeRadius * 1.2) {
        placeNode(m); return;
      }
    }

    if (m.roleId === 'scout') {
      /* 교전 구역 조명 — 어두운 곳에서 싸우는 팀에게 실질적인 지원이다 */
      if (m.flareCd <= 0) {
        let lit = false;
        for (const l of G.lamps) if (Math.hypot(l.x - fx, l.y - fy) < (l.rad || 120) * 0.8) { lit = true; break; }
        if (!lit) {
          const a = Math.atan2(fy - m.y, fx - m.x);
          const lx = m.x + Math.cos(a) * CELL * 2.4, ly = m.y + Math.sin(a) * CELL * 2.4;
          G.lamps.push({ c: 0, r: 0, x: lx, y: ly, ph: Math.random() * 6,
            rad: Math.max(DEMO.lampRadius * 1.45, CELL * 4.2), ttl: 22, flare: 1, visionRange: 5 });
          if (typeof LOS !== 'undefined' && LOS.markDirty) LOS.markDirty();
          J.flash(lx, ly, 42, 'rgba(255,220,160,.9)');
          m.flareCd = kit.flareCd;
          reconAward(m, lx, ly);
          say(m, '조명 지원');
          return;
        }
      }
    }

    if (m.roleId === 'gunner') {
      const d = Math.hypot(fx - m.x, fy - m.y);
      if (m.qCd <= 0 && (d < CELL * 3 || m.hp < m.hpMax * 0.6)) {
        m.shieldT = 2.8; m.qCd = 8;
        J.ring(m.x, m.y, '#7FEBD0', 8, CELL * 1.4, 3);
        say(m, '방어막');
        return;
      }
      /* 벽 뒤의 적을 파쇄탄으로 열어젖힌다 — 거너다운 전투 기여 */
      if (m.breakerCd <= 0 && !canSee(m.x, m.y, fx, fy)) {
        const [c, r] = toCell((m.x + fx) / 2, (m.y + fy) / 2);
        const t = cellOf(c, r);
        if (t && !unbreakable(t)) { fireBreaker(m, c, r); say(m, '차폐 제거'); return; }
      }
    }
  }

  /* ══════════════════════════════════════════════════════════════
     재화 습득
     크루가 광물을 아예 못 줍던 문제. 다만 재화만 쫓아다니면 전투도 굴착도
     안 하는 청소부가 된다. 그래서 두 층으로 나눈다.
       ① 접촉 습득 — 지나가다 밟으면 줍는다 (항상)
       ② 회수 행동 — 근처에 떨어진 게 있으면 '확률적으로' 주우러 간다
     ②는 전투보다 아래, 채굴보다 위에 둔다.
     ══════════════════════════════════════════════════════════════ */
  function lootPickupRange() {
    return (typeof DEMO !== 'undefined' ? DEMO.lootPickup : 26) * 1.0;
  }
  /* 접촉 습득 — 사람의 픽업과 같은 곳으로 정산된다(팀 재화) */
  function collectLoot(m, dt) {
    if (!G.res || !G.res.length) return;
    const pick = lootPickupRange(), mag = (DEMO.lootMagnet || 72) * 0.55;
    for (const q of G.res) {
      if (q.gone || !q.land) continue;
      const d = Math.hypot(q.x - m.x, q.y - m.y);
      if (d > mag) continue;
      if (d > pick) {
        /* 약한 자석 — 사람보다 약하게 둬서 사람 몫을 빨아가지 않는다 */
        if ((q.landAge || 0) >= (DEMO.lootMagnetDelay || 0.8)) {
          q.x += (m.x - q.x) * Math.min(1, dt * (DEMO.lootMagnetSpd || 7.5) * 0.5);
          q.y += (m.y - q.y) * Math.min(1, dt * (DEMO.lootMagnetSpd || 7.5) * 0.5);
        }
        continue;
      }
      q.gone = true;
      const v = Math.max(1, Math.round(q.val * (typeof depthMul === 'function' ? depthMul() : 1)));
      if (q.kind === 'pulp') G.gPulp += v; else G.gBloom += v;
      G.nRes += v;
      const col = q.kind === 'bloom' ? P.bloom : P.pulp;
      J.ring(m.x, m.y, col, 10, 26 + v * 3, 2);
      J.burst(m.x, m.y, 3, col, 90);
      if (typeof paintResHud === 'function') paintResHud();
      SFX.res && SFX.res();
      awardXp(m, Math.max(1, v), 'loot', { x: m.x, y: m.y });   /* 회수도 개인 경험치 (§5.3) */
      m.lootGot = (m.lootGot || 0) + v;
    }
  }
  /* 회수 목표 — 근처의 가장 값나가는 재화. 확률·쿨다운으로 절제한다. */
  function pickLoot(m) {
    if (!G.res || !G.res.length) return null;
    if (m.lootCd > 0) return null;
    const maxD = CELL * 7;
    let best = null, bs = -1e9;
    for (const q of G.res) {
      if (q.gone || !q.land) continue;
      const d = Math.hypot(q.x - m.x, q.y - m.y);
      if (d > maxD) continue;
      /* 사람이 코앞에 둔 재화는 사람 몫으로 남긴다 */
      if (Math.hypot(q.x - G.sh.x, q.y - G.sh.y) < CELL * 2.2) continue;
      const s = (q.kind === 'bloom' ? 6 : 3) + (q.val || 1) * 2 - d / CELL * 1.6;
      if (s > bs) { bs = s; best = q; }
    }
    return best;
  }

  /* ══════════ 판단 — 역할별 행동 예산 ══════════ */

  /* 리더 = 사람 플레이어. AI 는 사람 주변에서 자기 일을 한다. */
  function leaderDist(m) { return Math.hypot(G.sh.x - m.x, G.sh.y - m.y); }

  /* 캘 만한 벽 — 광맥에 가중치. anchor 주변만 본다.
     제한을 걸지 않는다: 크루가 능동적으로 계속 파는 편이 플레이 감각에 낫다.
     (광맥만 캐게 막아 봤더니 할 일이 없어 서 있는 시간이 크게 늘었다.) */
  function pickMine(m, ax, ay, radius) {
    const oc = Math.floor(ax / CELL), or_ = Math.floor(ay / CELL);
    let best = null, bs = -1e9;
    for (let dr = -radius; dr <= radius; dr++) for (let dc = -radius; dc <= radius; dc++) {
      const c = oc + dc, r = or_ + dr;
      if (c < 1 || r < 1 || c >= COLS - 1 || r >= ROWS - 1) continue;
      const t = G.cell[idx(c, r)];
      if (!t || unbreakable(t)) continue;
      /* v7.8.1 — "열린 면이 있다"가 아니라 "내가 걸어가서 붙을 수 있다"로 판정한다.
         열린 면이 기반암 건너편이면 영원히 닿지 못하고 모서리만 비빈다 */
      if (!GEO.canMine(m.x, m.y, c, r)) continue;
      const ore = t === 'gem' ? 28 : t === 'crys' ? 22 : t === 'ore' ? 16 : t === 'stone' ? 3 : 2;
      const s = ore - Math.hypot(c - oc, r - or_) * 2.4;
      if (s > bs) { bs = s; best = { c, r, x: cxw(c), y: cyw(r), type: t }; }
    }
    return best;
  }

  /* 스카우트: 아직 어두운 방향을 찾는다 — 플레어를 던질 곳 */
  function darkSpotAhead(m) {
    const best = { x: 0, y: 0, score: -1 };
    for (let i = 0; i < 12; i++) {
      const a = (i / 12) * Math.PI * 2;
      for (let d = 3; d <= 7; d++) {
        const x = m.x + Math.cos(a) * CELL * d, y = m.y + Math.sin(a) * CELL * d;
        if (x < CELL || y < CELL || x > WW - CELL || y > WH - CELL) break;
        if (typeof solidAt === 'function' && solidAt(x, y)) break;
        let lit = false;
        for (const l of G.lamps) if (Math.hypot(l.x - x, l.y - y) < (l.rad || 120)) { lit = true; break; }
        const s = d * (lit ? 0.2 : 1);
        if (s > best.score) { best.score = s; best.x = x; best.y = y; }
      }
    }
    return best.score > 0 ? best : null;
  }

  function decide(m) {
    const kit = m.kit, I = inf();
    const ld = leaderDist(m);

    /* 0) 탈출 포트 — 요청되면 전원이 간다 (§8.4) */
    const esc = I && I.escape;
    if (esc && (esc.state === 'incoming' || esc.state === 'ready')) {
      return { kind: 'escape', x: esc.x, y: esc.y, label: '탈출 포트' };
    }

    /* 1) 쓰러진 동료 구조가 최우선 (§9.2) */
    const downed = AI.members.find((o) => o !== m && o.down && Math.hypot(o.x - m.x, o.y - m.y) < CELL * 14);
    if (downed) return { kind: 'revive', x: downed.x, y: downed.y, target: downed, label: '구조' };

    /* 2) 리더가 너무 멀면 붙는다 — 혼자 돌아다니는 AI 는 동료가 아니다 */
    const leash = m.roleId === 'scout' ? 13 : 9;
    if (ld > CELL * leash) return { kind: 'follow', x: G.sh.x, y: G.sh.y, label: '합류' };

    /* 3) 전투 — 인지는 팀 단위, 요격 거리는 직업별.
          거너는 멀리까지 쫓아가 지우고, 드릴러는 파티에 붙은 적만 처리한다. */
    if (I && I.boss && I.boss.hp > 0 && Math.hypot(I.boss.x - m.x, I.boss.y - m.y) < CELL * 16) {
      return { kind: 'fight', x: I.boss.x, y: I.boss.y, enemy: I.boss, boss: true, label: '보스 교전' };
    }
    const foes = threats(m);
    if (foes.length) {
      const t = foes[0];
      /* 파티에서 너무 멀리 떨어진 적까지 쫓아가면 대열이 무너진다 */
      const leashOk = t.dp <= CELL * (m.kit.intercept + 4) || t.d <= CELL * m.kit.intercept;
      if (leashOk) return { kind: 'fight', x: t.e.x, y: t.e.y, enemy: t.e, label: '교전' };
    }

    /* 4) 직업 고유 임무 — 전투가 없을 때 그 직업이 하는 일 */
    if (m.roleId === 'engineer') {
      const mine = AI.turrets.filter((t) => t.owner === m.id);
      if (m.turretCd <= 0 && mine.length < kit.maxTurrets) return { kind: 'turret', x: m.x, y: m.y, label: '센트리 설치' };
      /* 파티가 센트리 라인을 벗어나 전진했으면 진지를 앞으로 옮긴다 —
         한 번 세우고 끝내는 엔지니어는 엔지니어가 아니다 */
      if (m.turretCd <= 0 && mine.length && mine.every((t) => Math.hypot(t.x - G.sh.x, t.y - G.sh.y) > CELL * 8)) {
        return { kind: 'turret', x: m.x, y: m.y, label: '센트리 전진 배치' };
      }
      const nodes = AI.nodes.filter((n) => n.owner === m.id);
      /* 센트리가 급전을 못 받고 있으면 노드부터 세운다 */
      const unpowered = mine.find((t) => !t.powered);
      if (m.nodeCd <= 0 && (nodes.length < kit.maxNodes || unpowered)) {
        const at = unpowered || m;
        return { kind: 'node', x: at.x, y: at.y, label: unpowered ? '센트리 급전' : '전력 노드' };
      }
    }
    if (m.roleId === 'scout' && m.flareCd <= 0) {
      const dark = darkSpotAhead(m);
      if (dark) return { kind: 'flare', x: dark.x, y: dark.y, label: '플레어' };
    }

    /* 4.4) 재화 회수 — 근처에 떨어진 게 있으면 '가끔' 주우러 간다.
            항상 쫓아가면 청소부가 되고, 아예 안 가면 광물이 바닥에 쌓인다. */
    if (m.lootCd <= 0) {
      const q = pickLoot(m);
      if (q && Math.random() < 0.4) {
        m.lootCd = 2.5 + Math.random() * 2;
        return { kind: 'loot', x: q.x, y: q.y, res: q, label: '재화 회수' };
      }
      if (!q) m.lootCd = 1.2;          /* 주울 게 없으면 잠깐 쉬었다 다시 본다 */
    }

    /* 4.5) 전투가 끊긴 사이 재장전 — 다음 교전을 빈 탄창으로 맞지 않는다 */
    if (m.reloadLeft <= 0 && m.ammo < m.mag * 0.45) {
      m.reloadLeft = m.reloadTime;
      say(m, '재장전');
    }

    /* 5) 채굴 — 한 번 고른 벽은 부술 때까지 붙잡는다 */
    const ax = (m.x + G.sh.x) / 2, ay = (m.y + G.sh.y) / 2;
    if (m.mineTarget) {
      const t = cellOf(m.mineTarget.c, m.mineTarget.r);
      const far = Math.hypot(m.mineTarget.x - ax, m.mineTarget.y - ay) > CELL * (leash + 4);
      if (!t || unbreakable(t) || far || m.mineTarget.until < G.t
          || !GEO.canMine(m.x, m.y, m.mineTarget.c, m.mineTarget.r)) m.mineTarget = null;
      else return { kind: 'mine', x: m.mineTarget.x, y: m.mineTarget.y, c: m.mineTarget.c, r: m.mineTarget.r, label: '채굴' };
    }
    /* 거너는 파쇄탄이 식기 전엔 벽을 붙잡지 않는다 — 대신 구역을 지킨다 */
    if (m.roleId === 'gunner' && m.breakerCd > 2) return { kind: 'guard', x: G.sh.x, y: G.sh.y, label: '구역 경계' };
    let pick = pickMine(m, ax, ay, Math.max(4, leash - 2));
    if (!pick) pick = pickMine(m, ax, ay, leash + 3);        /* 주변이 이미 다 파였으면 더 멀리 본다 */
    if (!pick) pick = pickMine(m, G.sh.x, G.sh.y, leash + 6);
    /* v7.8.1 — 반경 안이 다 파였으면 "내 열린 공간의 경계벽"에서 고른다.
       반경 확장만으로는 기반암 너머 벽이 계속 후보로 올라온다 */
    if (!pick) pick = GEO.frontier(m.x, m.y, (c, r, t) => {
      const ore = t === 'gem' ? 28 : t === 'crys' ? 22 : t === 'ore' ? 16 : t === 'stone' ? 3 : 2;
      return ore - Math.hypot(cxw(c) - m.x, cyw(r) - m.y) / CELL * 1.4;
    });
    if (pick) {
      m.mineTarget = { c: pick.c, r: pick.r, x: pick.x, y: pick.y, until: G.t + 14 };
      return { kind: 'mine', x: pick.x, y: pick.y, c: pick.c, r: pick.r, label: '채굴' };
    }
    return { kind: 'guard', x: G.sh.x, y: G.sh.y, label: '대기' };
  }

  /* ══════════ 실행 ══════════ */

  function followStep(m, goal, dt) {
    const step = ensurePath(m, goal.x, goal.y);
    if (step < 0) { steer(m, goal.x, goal.y, dt); aimTo(m, goal.x, goal.y); return; }
    const c = step % COLS, r = (step - c) / COLS;
    if (G.cell[step]) {
      /* 앞이 벽 — 직업의 방식으로 뚫는다 */
      const wx = cxw(c), wy = cyw(r);
      if (m.roleId === 'gunner') {
        if (!fireBreaker(m, c, r)) { steer(m, wx, wy, dt, 0.4); aimTo(m, wx, wy); }
        else moveAwayFrom(m, wx, wy, dt, 0.8);
        return;
      }
      steer(m, wx, wy, dt, 0.55);
      if (!digAt(m, c, r, dt)) aimTo(m, wx, wy);
      return;
    }
    steer(m, cxw(c), cyw(r), dt);
    aimTo(m, cxw(c), cyw(r));
  }

  function aimTo(m, x, y) {
    const want = Math.atan2(y - m.y, x - m.x);
    let d = want - m.aim;
    while (d > Math.PI) d -= Math.PI * 2;
    while (d < -Math.PI) d += Math.PI * 2;
    m.aim += d * 0.25;
    m.face = Math.cos(m.aim) < 0 ? -1 : 1;
  }

  function act(m, goal, dt) {
    /* 보스탄 예고는 어떤 행동보다 먼저다 — 굴착 중이든 이동 중이든 */
    if (goal.kind !== 'fight' && dodgeBossShot(m, dt)) return;

    if (goal.kind === 'fight') {
      const e = goal.enemy;
      const d = Math.hypot(e.x - m.x, e.y - m.y);
      const want = CELL * (goal.boss ? m.kit.engage + 1.6 : m.kit.engage);
      aimTo(m, e.x, e.y);
      /* 지원 행동을 먼저 흘린다 — "쏘느라 아무것도 안 하는" 문제를 막는다 */
      combatSupport(m, goal, dt);
      /* 보스탄 예고 안이면 사격은 유지한 채 원 밖으로 뺀다 */
      if (dodgeBossShot(m, dt)) { if (canSee(m.x, m.y, e.x, e.y)) fire(m, e.x, e.y); return; }
      /* 사격 — 총알이 닿고 시야가 통할 때만. 안 닿으면 붙는다. */
      const inRange = d <= CELL * FIRE_RANGE() && canSee(m.x, m.y, e.x, e.y);
      if (inRange) fire(m, e.x, e.y);
      if (!inRange) { followStep(m, goal, dt); return; }
      if (d < want * 0.65) {
        moveAwayFrom(m, e.x, e.y, dt, 0.95);
        if (d < CELL * 1.5 && m.dashCd <= 0 && m.hp < m.hpMax * 0.5) tryDashAI(m, m.x - e.x, m.y - e.y);
      } else if (d > want * 1.35) followStep(m, goal, dt);
      else {
        /* 사거리 유지 — 옆으로 돌며 쏜다 */
        const a = Math.atan2(e.y - m.y, e.x - m.x) + Math.PI / 2 * (m.id % 2 ? 1 : -1);
        steer(m, m.x + Math.cos(a) * CELL, m.y + Math.sin(a) * CELL, dt, 0.55);
      }
      /* 드릴러는 붙은 적을 드릴로 간다 */
      if (m.kit.drillMelee && d < CELL * 1.5) { m.digging = true; m.drill = 1; drillMelee(m, dt); }
      return;
    }

    if (goal.kind === 'escape') {
      const d = Math.hypot(goal.x - m.x, goal.y - m.y);
      if (d > CELL * 1.0) followStep(m, goal, dt);
      else { m.vx *= 0.7; m.vy *= 0.7; aimTo(m, goal.x, goal.y); }
      const foes = enemiesNear(m, 7, true);
      if (foes.length) { aimTo(m, foes[0].e.x, foes[0].e.y); fire(m, foes[0].e.x, foes[0].e.y); }
      return;
    }

    if (goal.kind === 'revive') {
      const d = Math.hypot(goal.x - m.x, goal.y - m.y);
      if (d > CELL * 1.2) followStep(m, goal, dt);
      else { m.vx *= 0.7; m.vy *= 0.7; aimTo(m, goal.x, goal.y); }
      return;
    }

    if (goal.kind === 'turret') { placeTurret(m); return; }
    if (goal.kind === 'node') {
      const d = Math.hypot(goal.x - m.x, goal.y - m.y);
      if (d > CELL * 1.2) { followStep(m, goal, dt); return; }
      placeNode(m); return;
    }
    if (goal.kind === 'flare') {
      const kit = m.kit;
      aimTo(m, goal.x, goal.y);
      const a = Math.atan2(goal.y - m.y, goal.x - m.x);
      const fx = m.x + Math.cos(a) * CELL * 2.6, fy = m.y + Math.sin(a) * CELL * 2.6;
      G.lamps.push({ c: 0, r: 0, x: fx, y: fy, ph: Math.random() * 6, rad: Math.max(DEMO.lampRadius * 1.45, CELL * 4.2), ttl: 22, flare: 1, visionRange: 5 });
      if (typeof LOS !== 'undefined' && LOS.markDirty) LOS.markDirty();
      J.flash(fx, fy, 42, 'rgba(255,220,160,.9)');
      J.ring(fx, fy, '#FFD080', 6, CELL * 1.6, 3);
      m.flareCd = kit.flareCd;
      reconAward(m, fx, fy);                            /* 정찰 — 새 구역을 밝힐 때만 */
      say(m, '플레어');
      return;
    }

    if (goal.kind === 'mine') {
      const d = Math.hypot(goal.x - m.x, goal.y - m.y);
      const gunner = m.roleId === 'gunner';
      /* v7.8.1 — 거너는 파쇄탄으로 원거리 처리, 나머지는 드릴이 실제로 닿을 때만 굴착.
         유클리드 거리만 보면 기반암 모서리 너머 벽을 붙잡고 제자리에서 비빈다 */
      const touch = gunner ? d <= CELL * 1.25 : GEO.inReach(m.x, m.y, goal.c, goal.r);
      if (!touch) { followStep(m, goal, dt); GEO.progressReset(m); return; }
      steer(m, goal.x, goal.y, dt, 0.4);
      if (gunner) { fireBreaker(m, goal.c, goal.r); return; }
      /* 굴착이 성립하지 않거나 벽 체력이 줄지 않으면 그 벽을 봉인하고 다른 목표로 —
         같은 벽이 즉시 재선택되는 무한 루프를 끊는 마지막 안전망 */
      if (!digAt(m, goal.c, goal.r, dt) || !GEO.progress(m, goal.c, goal.r, dt, 1.8)) {
        GEO.ban(goal.c, goal.r, 14);
        m.mineTarget = null; m.goal = null;
        m.path = []; m.pathKey = ''; m.pathAge = 0;
      }
      return;
    }

    if (goal.kind === 'loot') {
      if (goal.res && goal.res.gone) { m.goal = null; return; }
      const d = Math.hypot(goal.x - m.x, goal.y - m.y);
      if (goal.res) { goal.x = goal.res.x; goal.y = goal.res.y; }
      if (d > lootPickupRange() * 0.9) followStep(m, goal, dt);
      else { m.vx *= 0.7; m.vy *= 0.7; m.goal = null; }
      return;
    }

    if (goal.kind === 'follow') { followStep(m, goal, dt); return; }

    /* guard — 리더 근처를 지키며 주변을 살핀다 */
    const d = Math.hypot(G.sh.x - m.x, G.sh.y - m.y);
    if (d > CELL * 3.2) followStep(m, goal, dt);
    else {
      m.vx *= 0.85; m.vy *= 0.85;
      m.aim += dt * 0.7;
      m.face = Math.cos(m.aim) < 0 ? -1 : 1;
    }
  }

  /* ══════════ 복구 ══════════
     지형은 런 도중에도 바뀐다 — 보스의 벽 붕괴, 탈출 포트 착륙, 붕괴 계열
     특성. 크루가 흙에 파묻히거나 길이 막히면 스스로 빠져나와야 한다.
     그래도 안 되면 파티 옆으로 복귀시킨다(대부분의 코옵 게임과 같은 처리). */
  function warpToParty(m) {
    J.burst(m.x, m.y, 10, ['#C7A0FF', '#FFF'], 120);
    const p = freeSpotNear(G.sh.x, G.sh.y, 1.6);
    m.x = p.x; m.y = p.y; m.vx = m.vy = 0;
    m.path = []; m.pathKey = ''; m.pathAge = 0;
    m.buriedT = 0; m.lostT = 0; m.stuckT = 0;
    J.burst(m.x, m.y, 10, ['#C7A0FF', '#FFF'], 120);
    say(m, '재합류');
  }
  function recover(m, dt) {
    if (typeof solidAt === 'function' && solidAt(m.x, m.y)) {
      const c = Math.floor(m.x / CELL), r = Math.floor(m.y / CELL);
      const t = cellOf(c, r);
      if (t && !unbreakable(t)) {
        m.digging = true; m.drill = 1;
        AI.breakSrc = m;
        try { damage(c, r, shelDps() * DRILL_DMG() * Math.max(1, m.kit.digMul) * 3 * dt, 0, -1, true); }
        finally { AI.breakSrc = null; }
      }
      m.buriedT = (m.buriedT || 0) + dt;
      if (m.buriedT > 4) warpToParty(m);
      m.vx *= 0.6; m.vy *= 0.6;
      return true;
    }
    m.buriedT = 0;
    m.lostT = m.stuckT > 0.8 ? (m.lostT || 0) + dt : Math.max(0, (m.lostT || 0) - dt * 0.5);
    if (m.lostT > 6) warpToParty(m);
    return false;
  }

  function watchStuck(m, dt) {
    const moved = Math.hypot(m.x - m.lastX, m.y - m.lastY);
    m.lastX = m.x; m.lastY = m.y;
    const wants = Math.hypot(m.vx, m.vy) > 8;
    if (wants && moved < dt * 10) m.stuckT += dt; else m.stuckT = Math.max(0, m.stuckT - dt * 2);
    if (m.jitter > 0) m.jitter -= dt;
    if (m.stuckT > 1.0) {
      m.stuckT = 0; m.pathAge = 0; m.path = []; m.pathKey = '';
      m.jitter = 0.5; m.jitterA = Math.random() * Math.PI * 2;
      if (m.goal && m.goal.kind === 'mine') {
        if (m.mineTarget) GEO.ban(m.mineTarget.c, m.mineTarget.r, 10);   /* v7.8.1 — 재선택 차단 */
        m.mineTarget = null; m.goal = null;
      }
    }
  }

  /* ══════════ 프레임 ══════════ */

  AI.update = function (dt) {
    if (!AI.enabled || !ready() || !AI.members.length) return;
    if (CREW.phase !== 'play') return;
    updateInstallations(dt);
    for (const m of AI.members) {
      m.iframes = Math.max(0, m.iframes - dt);
      m.gunCd = Math.max(0, m.gunCd - dt);
      m.qCd = Math.max(0, m.qCd - dt);
      m.eCd = Math.max(0, m.eCd - dt);
      m.dashCd = Math.max(0, m.dashCd - dt);
      m.shieldT = Math.max(0, m.shieldT - dt);
      m.flareCd = Math.max(0, m.flareCd - dt);
      m.turretCd = Math.max(0, m.turretCd - dt);
      m.nodeCd = Math.max(0, m.nodeCd - dt);
      m.lootCd = Math.max(0, m.lootCd - dt);
      m.pathAge -= dt;
      m.sayT = Math.max(0, m.sayT - dt);
      if (m.reloadLeft > 0) { m.reloadLeft = Math.max(0, m.reloadLeft - dt); if (m.reloadLeft <= 0) m.ammo = m.mag; }
      updateBreakers(m, dt);
      updateBoarding(m, dt);
      xpTrickle(m, dt);
      collectLoot(m, dt);            /* 접촉 습득 — 어떤 행동 중이든 밟으면 줍는다 */

      if (m.down) { updateDown(m, dt); continue; }

      m.digging = false; m.drill = 0;
      if (recover(m, dt)) { applyMotion(m, dt); continue; }   /* 파묻힘 탈출이 최우선 */

      m.react -= dt;
      if (m.react <= 0 || !m.goal) { m.goal = decide(m); m.react = 0.12 + Math.random() * 0.1; }
      try { act(m, m.goal, dt); }
      catch (e) {
        m.vx = m.vy = 0;
        if (!m._errAt || performance.now() - m._errAt > 3000) {
          m._errAt = performance.now();
          console.error('[AICREW]', m.roleId, m.goal && m.goal.kind, e);
        }
      }
      applyMotion(m, dt);
      watchStuck(m, dt);
    }
  };

  /* ══════════ 적의 타깃 선정 ══════════
     AI 크루가 없으면 항상 사람을 반환한다 — 즉 기존 동작과 완전히 같다.
     크루가 있으면 가장 가까운 크루를 노리되, 0.6초 이력(hysteresis)을 두어
     적이 두 사람 사이에서 떨리지 않게 한다. */
  window.AI_TGT = function (e) {
    if (!AI.enabled || !AI.members.length || !ready()) { if (e) e._aiTgt = null; return G.sh; }
    if (!e) return G.sh;
    e._aiTgtCd = (e._aiTgtCd || 0) - (G.rt - (e._aiTgtAt || G.rt));
    e._aiTgtAt = G.rt;
    if (e._aiTgtCd > 0) {
      const cur = e._aiTgt;
      if (!cur) return G.sh;
      if (!cur.down && AI.members.includes(cur)) return cur;
    }
    e._aiTgtCd = 0.6;
    let best = null, bd = Math.hypot(G.sh.x - e.x, G.sh.y - e.y);
    for (const m of AI.members) {
      if (m.down) continue;
      const d = Math.hypot(m.x - e.x, m.y - e.y);
      if (d < bd) { bd = d; best = m; }
    }
    e._aiTgt = best;
    return best || G.sh;
  };
  /* 적의 타격이 AI 크루에게 갔는지 — enemyContactDamage 가 쓴다 */
  window.AI_TGT_HURT = function (e, dmg, nx, ny) {
    const m = e && e._aiTgt;
    if (!m || m.down || !AI.members.includes(m)) return null;
    return AI.hurt(m, dmg, nx, ny);
  };

  /* ══════════ 탈출 — 생존자 전원 탑승 (§8.4-3·4) ══════════
     AI 크루가 있으면 로켓은 그들도 태워야 뜬다. 이게 성립해야 코옵 설계의
     핵심인 "혼자 못 나간다"를 솔로에서도 검증할 수 있다. */
  function updateBoarding(m, dt) {
    const I = inf(), esc = I && I.escape;
    if (!esc || esc.state !== 'ready') { m.boarded = false; m.boardT = 0; return; }
    if (m.down) { m.boarded = false; m.boardT = 0; return; }
    const d = Math.hypot(m.x - esc.x, m.y - esc.y);
    if (d <= CELL * (typeof INF_ESCAPE !== 'undefined' ? INF_ESCAPE.boardRange : 1.25)) {
      m.boardT = (m.boardT || 0) + dt;
      if (m.boardT >= (typeof INF_ESCAPE !== 'undefined' ? INF_ESCAPE.boardTime : 1.2) && !m.boarded) {
        m.boarded = true;
        say(m, '탑승');
        toast && toast('AI ' + ROLE_KO[m.roleId] + ' 탑승');
      }
    } else { m.boardT = Math.max(0, (m.boardT || 0) - dt * 2); m.boarded = false; }
  }
  /* null = AI 크루 없음(기존 솔로 판정 그대로) · false = 아직 대기 · true = 전원 탑승 */
  AI.escapeAllAboard = function () {
    if (!AI.enabled || !AI.members.length) return null;
    for (const m of AI.members) {
      if (m.down) continue;              /* 다운된 크루는 제외 — 생존자 전원 */
      if (!m.boarded) return false;
    }
    return true;
  };
  AI.escapeCount = function () {
    if (!AI.members.length) return null;
    let total = 1, on = 0;
    for (const m of AI.members) { if (m.down) continue; total++; if (m.boarded) on++; }
    return { boarded: on, total };
  };

  /* ══════════ 이벤트 리액션 (Phase 4.5 — 도파민 콘텐츠 대응) ══════════
     게임 훅(잭팟·피버·탈출 배당)이 AICREW.reactEvent(kind)로 호출한다.
     살아있는 크루 하나가 직업 개성이 묻은 한마디를 말풍선으로 뱉는다 — "동료와 함께" 감각. */
  const REACT_LINES = {
    jackpot: {
      driller: ['노다지다!! 내가 판다!', '이 맥이야, 이 맥!'],
      gunner: ['소리 들었지? 온다. 엄호한다', '캐, 내가 막는다'],
      scout: ['신호가 미쳤어, 진짜배기다', '이런 건 처음 봐'],
      engineer: ['수익률 계산 완료. 캐자', '센트리 돌려놨어, 마음껏 캐'],
    },
    fever: {
      driller: ['드릴이 노래한다!!', '멈추지 마!'],
      gunner: ['그 기세 좋다!', '길이 뻥뻥 뚫리네'],
      scout: ['속도 봐라, 따라간다!', '앞은 내가 본다, 파!'],
      engineer: ['출력 안정적. 계속!', '과열 수치가 예술이다'],
    },
    escapeWait: {
      driller: ['더 캘 수 있어... 조금만 더', '배당이 아깝잖아'],
      gunner: ['버티는 만큼 번다 이거지', '탄약은 충분해. 더 버텨?'],
      scout: ['위험 수당 챙기자고', '적 몰려온다, 판단은 빨리'],
      engineer: ['초당 2%p, 나쁘지 않은 이율이야', '리스크 대비 수익 양호'],
    },
  };
  AI.reactEvent = function (kind) {
    if (!AI.enabled || !AI.members.length) return;
    const table = REACT_LINES[kind];
    if (!table) return;
    const alive = AI.members.filter((m) => !m.down);
    if (!alive.length) return;
    const m = alive[(Math.random() * alive.length) | 0];
    const lines = table[m.roleId] || table.driller;
    say(m, lines[(Math.random() * lines.length) | 0]);
  };

  /* AI 크루 위치 — 시야 합산(FoW)과 적 타깃 선정에 쓴다 */
  AI.visionXY = function () {
    if (!AI.members.length) return null;
    const out = [];
    for (const m of AI.members) if (!m.down) out.push([m.x, m.y]);
    return out.length ? out : null;
  };

  /* ══════════ 렌더 ══════════ */

  AI.draw = function () {
    if (!AI.members.length || !ready() || typeof cx === 'undefined') return;
    drawInstallations();
    for (const m of AI.members) {
      const col = ROLE_COL[m.roleId] || '#c7a0ff';
      cx.save();
      if (m.down) cx.globalAlpha = 0.45;
      else if (m.iframes > 0) cx.globalAlpha = 0.5 + 0.5 * Math.sin(G.t * 28);

      /* 몸 — 사람 캐릭터와 같은 그리기 함수를 쓴다 */
      if (typeof drawShellyVector === 'function') {
        drawShellyVector(m.x, m.y, R_SHELLY, G.t, m.face, m.digging ? 1 : 0, m.aim, m.roleId !== 'driller');
      }
      cx.globalAlpha = 1;

      /* 보호막처럼 실제 상태를 나타내는 효과만 캐릭터 둘레에 표시한다. */
      if (m.shieldT > 0) {
        cx.strokeStyle = '#7FEBD0'; cx.globalAlpha = 0.5 + 0.3 * Math.sin(G.t * 9);
        cx.beginPath(); cx.arc(m.x, m.y, R_SHELLY * 1.35, 0, Math.PI * 2); cx.stroke();
      }
      cx.globalAlpha = 1;

      /* 배경 패널 없는 미니멀 플로팅 라벨 */
      const label = 'AI ' + (ROLE_KO[m.roleId] || m.roleId) + ' · ' + m.level;
      cx.font = '700 9.5px ' + (typeof FNT !== 'undefined' ? FNT : 'sans-serif');
      cx.textAlign = 'center';
      const by = m.y - R_SHELLY - 34;
      cx.lineJoin = 'round';
      cx.lineWidth = 2.5;
      cx.strokeStyle = 'rgba(10,6,20,.82)';
      cx.strokeText(label, m.x, by + 11);
      cx.fillStyle = m.down ? '#ffc9d6' : '#F0E6FF';
      cx.fillText(label, m.x, by + 11);

      /* 체력 바 */
      const bw = 34, hp = Math.max(0, m.hp / m.hpMax);
      cx.fillStyle = 'rgba(0,0,0,.55)'; cx.fillRect(m.x - bw / 2, by + 17, bw, 4);
      cx.fillStyle = m.down ? '#FF557D' : hp > 0.5 ? '#7FEBD0' : hp > 0.25 ? '#FFD36E' : '#FF8DA8';
      cx.fillRect(m.x - bw / 2, by + 17, bw * hp, 4);

      /* 지금 뭘 하는지 — 플레이테스트에서 이게 제일 중요하다 */
      const state = m.down ? '다운 ' + (3 - m.reviveT > 0 ? Math.ceil(3 - m.reviveT) + 's' : '') : (m.sayT > 0 ? m.say : (m.goal ? m.goal.label : ''));
      if (state) {
        cx.font = '700 9.5px ' + (typeof FNT !== 'undefined' ? FNT : 'sans-serif');
        cx.fillStyle = m.down ? '#FF8DA8' : 'rgba(230,214,255,.78)';
        cx.fillText(state, m.x, by - 4);
      }

      if (AI.debug && m.path.length) {
        cx.strokeStyle = col; cx.globalAlpha = 0.35; cx.lineWidth = 2;
        cx.beginPath(); cx.moveTo(m.x, m.y);
        for (const k of m.path) { const c = k % COLS, r = (k - c) / COLS; cx.lineTo(cxw(c), cyw(r)); }
        cx.stroke(); cx.globalAlpha = 1;
      }
      cx.restore();
    }
  };

  function drawInstallations() {
    for (const n of AI.nodes) {
      const a = Math.min(1, n.life / 4);
      cx.save(); cx.globalAlpha = 0.28 * a;
      cx.strokeStyle = '#7FEBD0'; cx.lineWidth = 1.5; cx.setLineDash([5, 6]);
      cx.beginPath(); cx.arc(n.x, n.y, CELL * n.radius, 0, 7); cx.stroke(); cx.setLineDash([]);
      cx.globalAlpha = a; cx.fillStyle = '#122b28'; cx.strokeStyle = '#7FEBD0'; cx.lineWidth = 2;
      cx.beginPath(); cx.arc(n.x, n.y, 9, 0, 7); cx.fill(); cx.stroke();
      cx.fillStyle = '#BFFFEF'; cx.beginPath(); cx.arc(n.x, n.y, 3.4, 0, 7); cx.fill();
      cx.restore();
    }
    for (const t of AI.turrets) {
      const col = t.powered ? '#C7A0FF' : '#685674';
      cx.save();
      const src = AI.nodes.find((n) => Math.hypot(n.x - t.x, n.y - t.y) <= CELL * n.radius);
      if (src) {
        cx.globalAlpha = 0.3 + 0.15 * Math.sin(G.t * 8); cx.strokeStyle = '#7FEBD0'; cx.lineWidth = 1.4;
        cx.setLineDash([6, 5]); cx.beginPath(); cx.moveTo(src.x, src.y); cx.lineTo(t.x, t.y); cx.stroke(); cx.setLineDash([]);
      }
      cx.globalAlpha = 1; cx.translate(t.x, t.y);
      cx.fillStyle = '#21152F'; cx.strokeStyle = col; cx.lineWidth = 2.2;
      cx.beginPath(); cx.arc(0, 0, 12, 0, 7); cx.fill(); cx.stroke();
      cx.rotate(t.aim || 0);
      cx.fillStyle = col; cx.beginPath(); cx.roundRect(0, -3.6, 17, 7.2, 3); cx.fill();
      cx.rotate(-(t.aim || 0));
      cx.fillStyle = '#F3E9FF'; cx.beginPath(); cx.arc(0, 0, 4, 0, 7); cx.fill();
      cx.strokeStyle = t.powered ? '#7FEBD0' : '#FF718A'; cx.lineWidth = 1.8;
      cx.beginPath(); cx.arc(0, 0, 16, -Math.PI * 0.5, -Math.PI * 0.5 + Math.PI * 2 * (t.ammo / Math.max(1, t.mag))); cx.stroke();
      if (!t.powered) {
        cx.fillStyle = '#FF8DA8'; cx.font = '800 8px ' + (typeof FNT !== 'undefined' ? FNT : 'sans-serif');
        cx.textAlign = 'center'; cx.fillText('NO POWER', 0, -20);
      }
      cx.restore();
    }
  }

  /* ══════════ 편성 UI — 직업 카드 옆의 작은 +AI ══════════ */

  const CSS = `
.aiCrewChip{position:absolute;right:42px;top:9px;z-index:6;display:flex;align-items:center;gap:0;
 border:1px solid color-mix(in srgb,var(--role,#c7a0ff) 55%,#3b2f49);border-radius:999px;
 background:rgba(12,8,20,.86);backdrop-filter:blur(3px);overflow:hidden;
 font:800 10px/1 Pretendard,Malgun Gothic,sans-serif;color:#e9def9}
.aiCrewChip button{all:unset;padding:5px 8px;cursor:pointer;color:inherit;line-height:1}
.aiCrewChip button:hover{background:color-mix(in srgb,var(--role,#c7a0ff) 26%,transparent)}
.aiCrewChip button:disabled{opacity:.3;cursor:default}
.aiCrewChip .n{padding:5px 7px;min-width:14px;text-align:center;color:var(--role,#c7a0ff);
 background:rgba(255,255,255,.05)}
.aiCrewChip.zero .n{color:#6f6280}
.aiCrewBar{display:flex;align-items:center;justify-content:center;gap:8px;flex-wrap:wrap;
 margin:12px auto 0;padding:9px 14px;max-width:900px;
 border:1px solid rgba(199,160,255,.22);border-radius:12px;background:rgba(199,160,255,.05);
 font:600 11.5px/1.5 Pretendard,Malgun Gothic,sans-serif;color:#a294b8}
.aiCrewBar b{color:#d9c6f5}
.aiCrewBar .slot{padding:3px 9px;border:1px solid currentColor;border-radius:999px;font-weight:800;font-size:10.5px}
.aiCrewBar .empty{color:#5f5473;border-style:dashed}
.aiCrewBar button{all:unset;padding:4px 10px;border:1px solid rgba(199,160,255,.3);border-radius:8px;
 cursor:pointer;color:#c9b6e6;font-weight:700;font-size:10.5px}
.aiCrewBar button:hover{background:rgba(199,160,255,.14)}
.aiHud{position:absolute;right:12px;top:112px;z-index:8;align-items:flex-end;display:flex;flex-direction:column;gap:4px;
 pointer-events:none;font:700 10.5px/1.35 Pretendard,Malgun Gothic,sans-serif}
.aiHud .row{display:flex;align-items:center;gap:6px;padding:4px 8px;border-radius:8px;
 border:1px solid rgba(255,255,255,.09);background:rgba(10,6,18,.62);color:#ded2f0}
.aiHud .dot{width:7px;height:7px;border-radius:50%}
.aiHud .st{color:#9d8fb4;font-weight:600}
.aiHud .hp{width:38px;height:4px;border-radius:2px;background:rgba(255,255,255,.14);overflow:hidden}
.aiHud .hp i{display:block;height:100%}
.aiHud .lv{padding:1px 5px;border-radius:5px;background:rgba(255,255,255,.09);color:#ffd36e;font-size:9.5px}
.aiHud .xp{width:26px;height:3px;border-radius:2px;background:rgba(255,255,255,.12);overflow:hidden}
.aiHud .xp i{display:block;height:100%;background:#C7A0FF}
.aiHud .down{color:#ff8da8}

`;

  function ensureCSS() {
    if (document.getElementById('aiCrewCSS')) return;
    const s = document.createElement('style');
    s.id = 'aiCrewCSS'; s.textContent = CSS;
    document.head.appendChild(s);
  }

  /* 어떤 직업 선택 화면이든 .tcRoleChoice[data-role] 카드를 쓴다.
     그래서 미션 모드·무한 모드·앞으로 생길 화면까지 한 번에 붙는다. */
  function attachChips() {
    const cards = document.querySelectorAll('.tcRoleChoice[data-role]');
    for (const card of cards) {
      const roleId = card.getAttribute('data-role');
      if (!KIT[roleId]) continue;
      if (getComputedStyle(card).position === 'static') card.style.position = 'relative';
      let chip = card.querySelector(':scope > .aiCrewChip');
      if (!chip) {
        chip = document.createElement('div');
        chip.className = 'aiCrewChip';
        chip.innerHTML = '<button type="button" data-ai="-" title="AI 크루 빼기">−</button>'
          + '<span class="n">0</span>'
          + '<button type="button" data-ai="+" title="이 직업의 AI 크루 추가">+ AI</button>';
        /* 카드 선택 클릭과 섞이지 않게 이벤트를 여기서 끊는다 */
        chip.addEventListener('click', (e) => { e.preventDefault(); e.stopPropagation(); });
        chip.addEventListener('pointerdown', (e) => e.stopPropagation());
        chip.querySelector('[data-ai="+"]').addEventListener('click', (e) => {
          e.preventDefault(); e.stopPropagation();
          if (AI.add(roleId)) { SFX && SFX.tick && SFX.tick(); }
          else toast && toast('AI 크루는 최대 ' + AI.max + '명입니다');
        });
        chip.querySelector('[data-ai="-"]').addEventListener('click', (e) => {
          e.preventDefault(); e.stopPropagation();
          if (AI.remove(roleId)) SFX && SFX.back && SFX.back();
        });
        card.appendChild(chip);
      }
      const n = AI.count(roleId);
      chip.classList.toggle('zero', n === 0);
      chip.querySelector('.n').textContent = String(n);
      chip.querySelector('[data-ai="+"]').disabled = AI.roster.length >= AI.max;
      chip.querySelector('[data-ai="-"]').disabled = n === 0;
    }
    /* 카드 그리드 아래의 편성 요약 */
    const grids = new Set();
    for (const card of cards) if (card.parentElement) grids.add(card.parentElement);
    for (const grid of grids) {
      let bar = grid.parentElement && grid.parentElement.querySelector(':scope > .aiCrewBar');
      if (!bar) {
        bar = document.createElement('div');
        bar.className = 'aiCrewBar';
        grid.insertAdjacentElement('afterend', bar);
      }
      const slots = [];
      slots.push('<span class="slot" style="color:#ffe6a8">나</span>');
      for (const r of AI.roster) slots.push('<span class="slot" style="color:' + ROLE_COL[r] + '">AI ' + ROLE_KO[r] + '</span>');
      for (let i = AI.roster.length; i < AI.max; i++) slots.push('<span class="slot empty">빈 자리</span>');
      bar.innerHTML = '<b>크루 편성</b>' + slots.join('')
        + (AI.roster.length ? '<button type="button" data-ai-clear>모두 비우기</button>' : '')
        + '<span style="flex-basis:100%;text-align:center;color:#7b6f8f;font-size:10.5px">'
        + '카드 우측 상단 <b>+ AI</b> 로 동료를 넣는다 · 런 중에도 <b>F9</b> 로 켜고 끌 수 있다</span>';
      const cb = bar.querySelector('[data-ai-clear]');
      if (cb) cb.addEventListener('click', (e) => { e.preventDefault(); e.stopPropagation(); AI.clear(); });
    }
  }
  AI.paintRosterUI = attachChips;

  /* 플레이 중 HUD — 각 AI 가 지금 뭘 하는지 */
  function paintHud() {
    let el = document.getElementById('aiCrewHud');
    if (!AI.members.length || !ready() || CREW.phase !== 'play') { if (el) el.remove(); return; }
    if (!el) {
      el = document.createElement('div');
      el.id = 'aiCrewHud'; el.className = 'aiHud';
      (document.getElementById('app') || document.body).appendChild(el);
    }
    el.innerHTML = AI.members.map((m) => {
      const col = ROLE_COL[m.roleId];
      const hp = Math.max(0, m.hp / m.hpMax);
      const st = m.down ? '<span class="down">다운 · 구조 필요</span>'
        : '<span class="st">' + (m.sayT > 0 ? m.say : (m.goal ? m.goal.label : '—')) + '</span>';
      const xpp = Math.max(0, Math.min(1, m.xp / Math.max(1, m.xpNeed)));
      return '<div class="row" title="' + (m.traits.join(' · ') || '특성 없음') + '">'
        + '<span class="dot" style="background:' + col + '"></span>'
        + '<b style="color:' + col + '">' + ROLE_KO[m.roleId] + '</b>'
        + '<span class="lv">Lv' + m.level + '</span>'
        + '<span class="hp"><i style="width:' + (hp * 100) + '%;background:' + (hp > .5 ? '#7FEBD0' : hp > .25 ? '#FFD36E' : '#FF8DA8') + '"></i></span>'
        + '<span class="xp"><i style="width:' + (xpp * 100) + '%"></i></span>'
        + st + '</div>';
    }).join('');
  }

  /* ══════════ 부팅 ══════════ */

  function boot() {
    ensureCSS();
    attachChips();
    /* 직업 선택 화면은 동적으로 다시 그려지므로(무한 모드 그리드) 계속 붙여준다 */
    const mo = new MutationObserver(() => {
      clearTimeout(AI._moT);
      AI._moT = setTimeout(attachChips, 40);
    });
    mo.observe(document.body, { childList: true, subtree: true });
    setInterval(() => { try { paintHud(); } catch (e) {} }, 220);
  }

  addEventListener('keydown', (e) => {
    if (e.key === 'F9') { AI.enabled = !AI.enabled; toast && toast('AI 크루 ' + (AI.enabled ? 'ON' : 'OFF')); e.preventDefault(); }
    if (e.key === 'F10') { AI.debug = !AI.debug; toast && toast('AI 경로 표시 ' + (AI.debug ? 'ON' : 'OFF')); e.preventDefault(); }
  });

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
  else boot();
})();
