/* ══════════════════════════════════════════════════════════════════
   TEAM_PING_V1 — 팀 핑 시스템 (docs/tunnel-crew-ping-system.md 1차 MVP)
   ──────────────────────────────────────────────────────────────────
   조작   G 탭 = 여기(위치) 핑 · G 홀드 + 방향 + 릴리즈 = 8방향 핑 휠 · V = 빠른 위험 핑
   표현   월드 마커(UI 캔버스, 안개 위) + 화면 밖 화살표(최대 3) + 3줄 로그 + 의미별 음형
   대상   적·보스 / 광맥·암반 / 기절 크루 / 탈출 포트 — 시야(LOS.seenAt) 안만 구체화
   코옵   COOP.ws 로 {t:'ping'} 전송 — 서버 RELAY_TYPES 에 'ping' 이 이미 있다.
          수신자마다 같은 규칙(좌표·타입·도배)으로 검증한다 (호스트 권위 판정은 2차).
   AI     AICREW.update 를 감싸 명령형 핑을 "단기 goal" 로 주입한다. 리더 구조·탈출·
          동료 구조·근접 위협은 핑보다 우선 (decide 로 되돌린다).
   훅     paintUI / AICREW.update / crewPaintSettings 를 감싼다. 본편 함수 본문은 건드리지 않는다.
   ══════════════════════════════════════════════════════════════════ */
(function () {
  'use strict';
  if (window.TCPING) return;

  /* ── 튠 상수 (기획서 §2.1 · §5 · §7) ─────────────────────────── */
  const CFG = {
    tapMs: 120, tapMovePx: 18, deadPx: 24, radiusPx: 96,
    dur: 4, durAlert: 5, trackSec: 2.5, labelSec: 1.2, popSec: 0.16, ringSec: 0.45,
    stackTiles: 1.5, agreeExtend: 2, offTiles: 30, offMax: 3,
    logSec: 3, logMax: 3, logMergeSec: 2,
    charges: 4, regenSec: 2.5, burstN: 6, burstSec: 10, lockSec: 5, downHelpSec: 6,
    aiGoSec: 8, aiAttackSec: 10, aiMineSec: 6, aiRetreatSec: 5, aiHelpSec: 10, aiDangerSec: 5, aiDangerTiles: 3,
  };

  /* 휠 방향(dir) — 0=↑ 부터 시계방향 45° 씩. 위=전진, 아래=후퇴, 왼쪽=도움, 오른쪽=발견 */
  const TYPES = {
    here:    { ko: '여기', col: '#5FB8FF', glyph: '●', dir: -1, pri: 0, cmd: false },
    go:      { ko: '가자', col: '#5EE08A', glyph: '▲', dir: 0,  pri: 3, cmd: true },
    attack:  { ko: '공격', col: '#FF5A5A', glyph: '✖', dir: 1,  pri: 5, cmd: true },
    find:    { ko: '발견', col: '#FFD36E', glyph: '◆', dir: 2,  pri: 1, cmd: false },
    mine:    { ko: '채굴', col: '#FF9A3C', glyph: '⛏', dir: 3,  pri: 2, cmd: true },
    retreat: { ko: '후퇴', col: '#FF4D6D', glyph: '▼', dir: 4,  pri: 6, cmd: false },
    defend:  { ko: '방어', col: '#B98CFF', glyph: '⬢', dir: 5,  pri: 4, cmd: true },
    help:    { ko: '도움', col: '#5FF5E0', glyph: '✚', dir: 6,  pri: 8, cmd: false },
    danger:  { ko: '위험', col: '#E8194B', glyph: '⚠', dir: 7,  pri: 7, cmd: false },
  };
  const DIRS = ['go', 'attack', 'find', 'mine', 'retreat', 'defend', 'help', 'danger'];
  const SEAT_COL = { p1: '#FFD36E', p2: '#7FEBD0', p3: '#FF8D72', p4: '#C7A0FF' };
  const ROLE_KO = { driller: '드릴러', gunner: '거너', scout: '스카우트', engineer: '엔지니어' };
  const CELL_KO = { ore: '광맥', gem: '보석 광맥', crys: '수정 광맥', stone: '단단한 암반', dirt: '흙벽', rock: '기반암', core: '기반암' };

  const P = {
    ver: 1, debug: false,
    markers: [], log: [], events: [],
    hold: null,        /* {t0, sx, sy, cx, cy, moved, open} */
    cur: { sx: 0, sy: 0 },
    charge: CFG.charges, chargeT: 0, attempts: [], lockUntil: 0, lastDownHelp: -1e9, lastT: 0,
    orders: [],        /* AI 명령 {type, x, y, c, r, target, seat, until, ids:Set, said:Set} */
    zones: [],         /* 위험 회피 구역 {x,y,r,until} */
    seq: 0, _ws: null, _tut: false,
    cfg: CFG, types: TYPES,
  };
  window.TCPING = P;

  const now = () => performance.now() / 1000;
  const $ = (id) => document.getElementById(id);
  const ready = () => (typeof G !== 'undefined') && (typeof CELL !== 'undefined') && (typeof CREW !== 'undefined') && (typeof SCENE !== 'undefined');
  const playing = () => ready() && SCENE === 'depths' && CREW.phase === 'play';
  const inf = () => ((typeof INF !== 'undefined') && INF.active ? INF : null);
  const clamp = (v, a, b) => Math.max(a, Math.min(b, v));
  const mapClamp = (x, y) => ({ x: clamp(x, CELL * 0.5, WW - CELL * 0.5), y: clamp(y, CELL * 0.5, WH - CELL * 0.5) });
  const viewer = () => (typeof tcViewChar === 'function' ? tcViewChar() : G.sh);
  const mySeat = () => ((typeof COOP !== 'undefined') && COOP.active && COOP.seat) || 'p1';
  const myName = () => ((typeof COOP !== 'undefined') && COOP.active && COOP.nick) || '나';
  const seatName = (seat, name) => name || (seat ? seat.toUpperCase() : 'P?');
  const setting = (k, d) => ((typeof CREW_SETTINGS !== 'undefined') && CREW_SETTINGS[k] != null ? CREW_SETTINGS[k] : d);
  const seen = (x, y) => ((typeof LOS !== 'undefined') && typeof LOS.seenAt === 'function' ? !!LOS.seenAt(x, y) : true);
  const emit = (name, data) => { P.events.push({ t: Date.now(), name, data }); if (P.events.length > 200) P.events.shift(); if (P.debug) console.debug('[TCPING]', name, data); };
  const toastSafe = (m) => { try { if (typeof toast === 'function') toast(m); } catch (e) {} };

  /* ══════════ 컨텍스트 판정 (§4) ══════════ */
  function enemyName(e) {
    if (e.boss) { const T = (typeof INF_BOSS_TIER !== 'undefined') && INF_BOSS_TIER[e.bossTier]; return (T && T.name) || '보스'; }
    return (e.apex ? '정예 ' : '') + (e.ranged ? '독침벌레' : '굴벌레');
  }
  function resolveContext(wx, wy) {
    /* 1) 적·보스 — 시야에 들어온 적만 */
    let best = null, bd = 1e9;
    if (G.enemies) for (const e of G.enemies) {
      if (!(e.hp > 0)) continue;
      const d = Math.hypot(e.x - wx, e.y - wy);
      if (d < Math.max((e.r || 16) + 10, CELL * 0.9) && d < bd && seen(e.x, e.y)) { best = e; bd = d; }
    }
    if (best) return { kind: 'enemy', id: best._eid != null ? best._eid : null, ref: best, name: enemyName(best), x: best.x, y: best.y };
    /* 2) 기절한 크루 — 리더 / AI / 코옵 피어 */
    if (G.downed && Math.hypot(G.sh.x - wx, G.sh.y - wy) < CELL * 1.3) return { kind: 'crew', who: 'leader', name: '리더 구조', x: G.sh.x, y: G.sh.y };
    if ((typeof AICREW !== 'undefined') && AICREW.members) for (const m of AICREW.members) {
      if (m.down && Math.hypot(m.x - wx, m.y - wy) < CELL * 1.3) return { kind: 'crew', who: 'ai', id: m.id, ref: m, name: 'AI ' + (ROLE_KO[m.roleId] || '') + ' 구조', x: m.x, y: m.y };
    }
    if ((typeof COOP !== 'undefined') && COOP.active) for (const [seat, p] of COOP.peers) {
      if (p.status === 'down' && isFinite(p.x) && Math.hypot(p.x - wx, p.y - wy) < CELL * 1.3) return { kind: 'crew', who: 'peer', seat, name: seatName(seat, p.name) + ' 구조', x: p.x, y: p.y };
    }
    /* 3) 탈출 포트 */
    const I = inf();
    if (I && I.escape && I.escape.state !== 'placing' && Math.hypot(I.escape.x - wx, I.escape.y - wy) < CELL * 1.7) return { kind: 'escape', name: '탈출 포트', x: I.escape.x, y: I.escape.y };
    /* 4) 지형 — 미탐사는 '알 수 없는 위치' (§4.1) */
    if (!seen(wx, wy)) return { kind: 'unknown', name: '알 수 없는 위치', x: wx, y: wy };
    const c = Math.floor(wx / CELL), r = Math.floor(wy / CELL);
    if (c >= 0 && r >= 0 && c < COLS && r < ROWS) {
      const t = G.cell[r * COLS + c];
      if (t && CELL_KO[t]) return { kind: (t === 'ore' || t === 'gem' || t === 'crys') ? 'ore' : 'wall', cellType: t, c, r, name: CELL_KO[t], x: c * CELL + CELL / 2, y: r * CELL + CELL / 2 };
    }
    return null;
  }
  const ctxWire = (ctx) => ctx ? { kind: ctx.kind, id: ctx.id != null ? ctx.id : undefined, seat: ctx.seat, who: ctx.who, name: ctx.name, c: ctx.c, r: ctx.r } : undefined;
  /* 수신한 컨텍스트를 로컬 오브젝트에 다시 연결한다 (적 추적·기절 종료 판정용) */
  function ctxBind(w, x, y) {
    if (!w) return null;
    const ctx = Object.assign({ x, y }, w);
    if (w.kind === 'enemy' && G.enemies) ctx.ref = G.enemies.find((e) => e.hp > 0 && ((w.id != null && e._eid === w.id) || Math.hypot(e.x - x, e.y - y) < CELL * 1.2)) || null;
    if (w.kind === 'crew' && w.who === 'ai' && (typeof AICREW !== 'undefined')) ctx.ref = AICREW.members.find((m) => m.id === w.id) || null;
    return ctx;
  }

  /* ══════════ 도배 제한 (§7.1) ══════════ */
  function tickCharges(dt) {
    if (P.charge < CFG.charges) { P.chargeT += dt; while (P.chargeT >= CFG.regenSec && P.charge < CFG.charges) { P.chargeT -= CFG.regenSec; P.charge++; } }
    else P.chargeT = 0;
  }
  /* 송신 가능 여부 — 기절 상태의 도움 핑은 별도 6초 규칙 */
  function gate(type) {
    const t = now();
    if (G.downed) {
      if (type !== 'help') return { ok: false, reason: 'down' };
      if (t - P.lastDownHelp < CFG.downHelpSec) return { ok: false, reason: 'down_cd', wait: CFG.downHelpSec - (t - P.lastDownHelp) };
      return { ok: true, downHelp: true };
    }
    if (t < P.lockUntil) return { ok: false, reason: 'locked', wait: P.lockUntil - t };
    P.attempts = P.attempts.filter((a) => t - a < CFG.burstSec);
    P.attempts.push(t);
    if (P.attempts.length >= CFG.burstN) { P.lockUntil = t + CFG.lockSec; P.attempts = []; return { ok: false, reason: 'burst', wait: CFG.lockSec }; }
    if (P.charge < 1) return { ok: false, reason: 'charge', wait: CFG.regenSec - P.chargeT };
    return { ok: true };
  }
  /* 원격 송신자별 검증 — 같은 규칙을 수신자 전원이 적용한다 */
  const remoteRate = new Map();
  function remoteGate(seat) {
    const t = now(); let s = remoteRate.get(seat);
    if (!s) { s = { charge: CFG.charges, last: t, attempts: [], lock: 0 }; remoteRate.set(seat, s); }
    s.charge = Math.min(CFG.charges, s.charge + (t - s.last) / CFG.regenSec); s.last = t;
    if (t < s.lock) return false;
    s.attempts = s.attempts.filter((a) => t - a < CFG.burstSec); s.attempts.push(t);
    if (s.attempts.length >= CFG.burstN) { s.lock = t + CFG.lockSec; s.attempts = []; return false; }
    if (s.charge < 1) return false;
    s.charge -= 1; return true;
  }

  /* ══════════ 마커 (§5.1 · §6.1) ══════════ */
  function findStack(type, x, y) {
    return P.markers.find((m) => m.type === type && Math.hypot(m.x - x, m.y - y) < CELL * CFG.stackTiles);
  }
  function findAny(x, y, notSeat) {
    let best = null, bd = 1e9;
    for (const m of P.markers) { const d = Math.hypot(m.x - x, m.y - y); if (d < CELL * CFG.stackTiles && d < bd && m.seat !== notSeat) { best = m; bd = d; } }
    return best;
  }
  function addMarker(o) {
    const T = TYPES[o.type]; const t = now();
    const stack = findStack(o.type, o.x, o.y);
    if (stack) { stack.level = Math.min(3, stack.level + 1); stack.until = Math.max(stack.until, t + Math.min(CFG.dur, (stack.until - t) + 1)); stack.pulse = t; pushLog(o.seat, o.name, o.type, o.ctx); sound(o.type, o.x); return stack; }
    const dur = (o.type === 'danger' || o.type === 'retreat') ? CFG.durAlert : CFG.dur;
    const m = { id: o.id, type: o.type, x: o.x, y: o.y, seat: o.seat, name: o.name, ctx: o.ctx || null, born: t, until: t + dur, level: 1, agree: new Set(), pulse: 0, col: T.col };
    P.markers.push(m);
    pushLog(o.seat, o.name, o.type, o.ctx);
    sound(o.type, o.x);
    emit('ping_show', { type: o.type, seat: o.seat, ctx: o.ctx && o.ctx.kind });
    return m;
  }
  function agreeMarker(id, seat) {
    const m = P.markers.find((k) => k.id === id); if (!m) return false;
    if (!m.agree.has(seat)) { m.agree.add(seat); m.until = Math.min(m.until + CFG.agreeExtend, m.born + CFG.durAlert + CFG.agreeExtend); m.pulse = now(); }
    sound('here', m.x); return true;
  }
  function removeMarker(id) { const i = P.markers.findIndex((m) => m.id === id); if (i >= 0) P.markers.splice(i, 1); }
  function tickMarkers() {
    const t = now();
    for (const m of P.markers) {
      const c = m.ctx;
      if (c && c.kind === 'enemy') {
        if (c.ref && c.ref.hp > 0 && t - m.born < CFG.trackSec) { m.x = c.ref.x; m.y = c.ref.y; }
        else if (c.ref && !(c.ref.hp > 0)) m.until = Math.min(m.until, t + 0.6);   /* 대상 사망 — 조기 종료 */
      }
      if (c && c.kind === 'crew') {   /* 구조되면 종료 */
        let up = false;
        if (c.who === 'leader') up = !G.downed;
        else if (c.who === 'ai') up = c.ref ? !c.ref.down : false;
        else if (c.who === 'peer' && (typeof COOP !== 'undefined')) { const p = COOP.peers.get(c.seat); up = !p || p.status !== 'down'; }
        if (up) m.until = Math.min(m.until, t + 0.6);
      }
    }
    P.markers = P.markers.filter((m) => m.until > t);
  }

  /* ══════════ 로그 (§5.3) ══════════ */
  function logText(name, type, ctx) {
    const ko = TYPES[type].ko;
    return name + ': ' + (ctx && ctx.name && ctx.kind !== 'unknown' ? ctx.name + ' ' + ko : ctx && ctx.kind === 'unknown' ? '알 수 없는 위치 ' + ko : ko);
  }
  function pushLog(seat, name, type, ctx) {
    const t = now(), key = seat + '|' + type;
    const last = P.log[P.log.length - 1];
    if (last && last.key === key && t - last.t < CFG.logMergeSec) { last.n++; last.t = t; return; }
    P.log.push({ key, text: logText(seatName(seat, name), type, ctx), t, n: 1, col: TYPES[type].col });
    while (P.log.length > CFG.logMax) P.log.shift();
  }

  /* ══════════ 사운드 (§5.4) — 의미별 음형, 거리와 무관한 2D UI 음 ══════════ */
  function sound(type, wx) {
    if (!(typeof AU !== 'undefined') || !AU.ready) return;
    if (setting('pingMute', false)) return;
    if (!(setting('ping', 1) > 0.001)) return;
    const g = 0.14;   /* cat:'ping' → AU.vol.ping(설정 슬라이더) 이 곱해진다 */
    const o = (f, d, at, ex) => AU.tone(f, d, Object.assign({ type: 'sine', g, at, cat: 'ping', lp: 2400 }, ex || {}));
    const n = (d, at, ex) => AU.hit(d, Object.assign({ f0: 500, f1: 180, g: g * 0.5, q: 0.7, ft: 'lowpass', at, cat: 'ping' }, ex || {}));
    switch (type) {
      case 'here':    o(520, 0.14, 0); break;
      case 'go':      o(440, 0.12, 0); o(660, 0.18, 0.11); break;
      case 'attack':  o(330, 0.1, 0, { type: 'square', lp: 1400 }); n(0.08, 0); o(330, 0.14, 0.12, { type: 'square', lp: 1400 }); n(0.08, 0.12); break;
      case 'find':    o(880, 0.3, 0, { slide: 1240 }); break;
      case 'mine':    n(0.12, 0, { f0: 320, f1: 90, g: g * 0.9 }); o(240, 0.16, 0.02, { type: 'triangle' }); break;
      case 'retreat': o(220, 0.14, 0, { type: 'triangle', lp: 900 }); o(175, 0.26, 0.15, { type: 'triangle', lp: 900 }); break;
      case 'defend':  o(392, 0.2, 0, { type: 'triangle' }); o(392, 0.3, 0.16, { type: 'triangle' }); break;
      case 'help':    o(523, 0.12, 0); o(784, 0.3, 0.12); break;
      case 'danger':  o(196, 0.16, 0, { type: 'sawtooth', lp: 1000, g: g * 0.8 }); n(0.1, 0); o(196, 0.3, 0.18, { type: 'sawtooth', lp: 1000, g: g * 0.8 }); n(0.1, 0.18); break;
    }
  }

  /* ══════════ 송신 ══════════ */
  function wsSend(obj) { try { if ((typeof COOP !== 'undefined') && COOP.active && COOP.ws && COOP.ws.readyState === 1) COOP.ws.send(JSON.stringify(obj)); } catch (e) {} }
  function rejectNote(reason, wait) {
    const w = wait != null ? Math.max(0.1, wait).toFixed(1) + '초' : '';
    P.note = { text: reason === 'locked' || reason === 'burst' ? '핑 잠금 ' + w : reason === 'charge' ? '핑 충전 ' + w : reason === 'down' ? '기절 중 — 도움 핑만' : reason === 'down_cd' ? '도움 핑 ' + w : '', until: now() + 1.4 };
    emit('ping_rejected', { reason });
  }
  function sendPing(type, sx, sy, viaQuick) {
    if (!playing() || !TYPES[type]) return false;
    const w0 = screenToWorld(sx, sy), w = mapClamp(w0.x, w0.y);
    if (!isFinite(w.x) || !isFinite(w.y)) return false;
    const seat = mySeat(), name = myName();
    /* 자기 마커 위 G 탭 = 취소 (§6.1) */
    if (type === 'here' && !viaQuick) {
      /* 카메라 룩어헤드로 같은 화면 지점도 월드 좌표가 조금 흔들린다 — 스택 반경(1.5칸)과 같은 기준으로 잡는다 */
      const own = P.markers.find((m) => m.seat === seat && Math.hypot(m.x - w.x, m.y - w.y) < CELL * CFG.stackTiles);
      if (own) { removeMarker(own.id); wsSend({ t: 'ping', v: P.ver, op: 'cancel', id: own.id }); emit('ping_cancel', { reason: 'marker' }); return true; }
      /* 다른 크루 마커 위 기본 핑 = 동의 — 충전을 쓰지 않는다 */
      const other = findAny(w.x, w.y, seat);
      if (other) { agreeMarker(other.id, seat); wsSend({ t: 'ping', v: P.ver, op: 'agree', id: other.id }); emit('ping_agree', { type: other.type }); return true; }
    }
    const gt = gate(type);
    if (!gt.ok) { rejectNote(gt.reason, gt.wait); return false; }
    if (gt.downHelp) P.lastDownHelp = now(); else P.charge -= 1;
    const ctx = resolveContext(w.x, w.y);
    const x = ctx && ctx.x != null ? ctx.x : w.x, y = ctx && ctx.y != null ? ctx.y : w.y;
    const id = seat + '-' + (++P.seq) + '-' + Date.now().toString(36);
    addMarker({ id, type, x, y, seat, name, ctx });
    aiOnPing({ type, x, y, seat, ctx });
    wsSend({ t: 'ping', v: P.ver, id, type, x: Math.round(x), y: Math.round(y), ctx: ctxWire(ctx), at: Date.now() });
    const me = viewer();
    emit('ping_send', { type, targetKind: ctx ? ctx.kind : 'floor', distanceFromPlayer: +(Math.hypot(x - me.x, y - me.y) / CELL).toFixed(1), viaQuickKey: !!viaQuick });
    return true;
  }
  /* 수신 — 좌석·좌표·타입·도배 검증 후 표시 (§8.2) */
  function onRemote(msg) {
    if (!ready() || !msg || msg.v !== P.ver || !msg.from) return;
    if (msg.op === 'cancel') { removeMarker(msg.id); return; }
    if (msg.op === 'agree') { agreeMarker(msg.id, msg.from); return; }
    if (!TYPES[msg.type] || !isFinite(msg.x) || !isFinite(msg.y)) return;
    if (msg.x < 0 || msg.y < 0 || msg.x > WW || msg.y > WH) return;
    if (!remoteGate(msg.from)) { emit('ping_rejected', { reason: 'remote_rate', from: msg.from }); return; }
    if (setting('pingHide', false) && msg.type !== 'help') return;   /* 완전 숨김 — 구조 요청은 유지 (§7.2) */
    const ctx = ctxBind(msg.ctx, msg.x, msg.y);
    addMarker({ id: String(msg.id || (msg.from + '-' + Date.now())), type: msg.type, x: msg.x, y: msg.y, seat: msg.from, name: msg.fromName || '', ctx });
    aiOnPing({ type: msg.type, x: msg.x, y: msg.y, seat: msg.from, ctx });
    emit('ping_acknowledge', { type: msg.type, latencyMs: msg.at ? Math.max(0, Date.now() - msg.at) : null });
  }
  function watchSocket() {
    if (!(typeof COOP !== 'undefined') || !COOP.ws || COOP.ws === P._ws) return;
    P._ws = COOP.ws;
    COOP.ws.addEventListener('message', (ev) => { let m; try { m = JSON.parse(ev.data); } catch (e) { return; } if (m && m.t === 'ping') onRemote(m); });
  }

  /* ══════════ AI 반응 (§6.2) ══════════
     명령형 핑은 orders 에 쌓이고, AICREW.update 앞에서 각 멤버의 goal 로 주입된다.
     같은 좌석의 새 명령형 핑은 그 좌석의 이전 가자/공격/채굴/방어 명령을 교체한다. */
  function aiOnPing(p) {
    if (!(typeof AICREW !== 'undefined') || !AICREW.members || !AICREW.members.length) return;
    const t = now();
    if (TYPES[p.type].cmd) P.orders = P.orders.filter((o) => !(o.seat === p.seat && TYPES[o.type].cmd));
    const base = { type: p.type, x: p.x, y: p.y, seat: p.seat, ids: new Set(), said: new Set(), t0: t, ctx: p.ctx };
    switch (p.type) {
      case 'go':      P.orders.push(Object.assign(base, { until: t + CFG.aiGoSec })); break;
      case 'attack':  if (p.ctx && p.ctx.kind === 'enemy' && p.ctx.ref) P.orders.push(Object.assign(base, { target: p.ctx.ref, until: t + CFG.aiAttackSec })); else aiSayAll('대상 없음'); break;
      case 'mine': {
        let c, r;
        if (p.ctx && p.ctx.c != null) { c = p.ctx.c; r = p.ctx.r; } else { c = Math.floor(p.x / CELL); r = Math.floor(p.y / CELL); }
        const tp = (c >= 0 && r >= 0 && c < COLS && r < ROWS) ? G.cell[r * COLS + c] : null;
        if (tp && !(typeof SOLIDX === 'function' ? SOLIDX(tp) : tp === 'rock' || tp === 'core')) P.orders.push(Object.assign(base, { c, r, x: c * CELL + CELL / 2, y: r * CELL + CELL / 2, until: t + CFG.aiMineSec }));
        else aiSayAll('대상 없음');
        break; }
      case 'retreat': P.orders.push(Object.assign(base, { until: t + CFG.aiRetreatSec })); break;
      case 'defend':  P.orders.push(Object.assign(base, { until: t + CFG.aiGoSec })); break;
      case 'find':    P.orders.push(Object.assign(base, { until: t + CFG.aiMineSec })); break;
      case 'help':    P.orders.push(Object.assign(base, { until: t + CFG.aiHelpSec })); break;
      case 'danger':  P.zones.push({ x: p.x, y: p.y, r: CELL * CFG.aiDangerTiles, until: t + CFG.aiDangerSec }); aiBanZone(p.x, p.y); break;
    }
  }
  function aiSayAll(text) { for (const m of AICREW.members) if (!m.down) { m.say = text; m.sayT = 1.6; } }
  function aiBanZone(x, y) {
    if (!(typeof AIGEO !== 'undefined') || typeof AIGEO.ban !== 'function') return;
    const c0 = Math.floor(x / CELL), r0 = Math.floor(y / CELL), R = CFG.aiDangerTiles;
    for (let dr = -R; dr <= R; dr++) for (let dc = -R; dc <= R; dc++) if (Math.hypot(dc, dr) <= R) { try { AIGEO.ban(c0 + dc, r0 + dr, CFG.aiDangerSec); } catch (e) {} }
  }
  function inZone(x, y) { const t = now(); return P.zones.find((z) => z.until > t && Math.hypot(z.x - x, z.y - y) < z.r) || null; }
  /* 핑보다 우선하는 기존 최상위 판단 — 이 조건이면 decide 에 맡긴다 */
  function aiBusy(m, order) {
    if (m.down || m.manual) return true;
    const I = inf();
    if (G.downed && order.type !== 'help') return true;
    if (I && I.escape && (I.escape.state === 'incoming' || I.escape.state === 'ready')) return true;
    if (AICREW.members.some((o) => o !== m && o.down && Math.hypot(o.x - m.x, o.y - m.y) < CELL * 14) && order.type !== 'help') return true;
    if (order.type !== 'attack' && order.type !== 'retreat' && G.enemies) {
      for (const e of G.enemies) if (e.hp > 0 && Math.hypot(e.x - m.x, e.y - m.y) < CELL * 4) return true;
    }
    return false;
  }
  function pickMembers(order) {
    const alive = AICREW.members.filter((m) => !m.down && !m.manual);
    if (!alive.length) return [];
    const near = (x, y) => alive.slice().sort((a, b) => Math.hypot(a.x - x, a.y - y) - Math.hypot(b.x - x, b.y - y));
    switch (order.type) {
      case 'go': case 'retreat': return alive;
      case 'attack': return alive;
      case 'mine': { const d = alive.find((m) => m.roleId === 'driller'); return d ? [d] : [near(order.x, order.y).filter((m) => m.roleId !== 'gunner')[0] || near(order.x, order.y)[0]]; }
      case 'defend': { const pref = alive.filter((m) => m.roleId === 'gunner' || m.roleId === 'engineer'); return pref.length ? pref : [near(order.x, order.y)[0]]; }
      case 'find': return [near(order.x, order.y)[0]];
      case 'help': { const n = near(order.x, order.y); return n.slice(0, alive.length >= 3 ? 2 : 1); }
    }
    return [];
  }
  function orderGoal(m, o) {
    const label = '핑 · ' + TYPES[o.type].ko;
    switch (o.type) {
      case 'go': case 'defend': case 'find':
        if (Math.hypot(o.x - m.x, o.y - m.y) < CELL * 1.2) return null;   /* 도착 — 해제 */
        return { kind: 'follow', x: o.x, y: o.y, label };
      case 'attack':
        if (!o.target || !(o.target.hp > 0) || !G.enemies.includes(o.target)) { o.until = 0; return null; }
        return { kind: 'fight', x: o.target.x, y: o.target.y, enemy: o.target, boss: !!o.target.boss, label };
      case 'mine': {
        const tp = G.cell[o.r * COLS + o.c];
        if (!tp || (typeof SOLIDX === 'function' ? SOLIDX(tp) : false)) { o.until = 0; return null; }
        m.mineTarget = { c: o.c, r: o.r, x: o.x, y: o.y, until: G.t + 6 };
        return { kind: 'mine', x: o.x, y: o.y, c: o.c, r: o.r, label }; }
      case 'retreat': {
        /* 핑 반대편 — 리더 쪽으로 3칸 이탈 */
        let dx = G.sh.x - o.x, dy = G.sh.y - o.y; const L = Math.hypot(dx, dy) || 1; dx /= L; dy /= L;
        const tx = clamp(G.sh.x + dx * CELL * 3, CELL, WW - CELL), ty = clamp(G.sh.y + dy * CELL * 3, CELL, WH - CELL);
        if (Math.hypot(o.x - m.x, o.y - m.y) > CELL * 6) return null;
        return { kind: 'follow', x: tx, y: ty, label }; }
      case 'help': {
        const c = o.ctx;
        let tx = o.x, ty = o.y, down = true;
        if (c && c.kind === 'crew') {
          if (c.who === 'leader') { tx = G.sh.x; ty = G.sh.y; down = !!G.downed; }
          else if (c.who === 'ai' && c.ref) { tx = c.ref.x; ty = c.ref.y; down = !!c.ref.down; }
          else if (c.who === 'peer' && (typeof COOP !== 'undefined')) { const p = COOP.peers.get(c.seat); if (p) { tx = p.x; ty = p.y; down = p.status === 'down'; } }
        } else if (G.downed) { tx = G.sh.x; ty = G.sh.y; }
        if (!down) { o.until = 0; return null; }
        return { kind: 'revive', x: tx, y: ty, target: c && c.ref ? c.ref : 'ping', label }; }
    }
    return null;
  }
  function aiApply() {
    if (!(typeof AICREW !== 'undefined') || !AICREW.members || !AICREW.enabled) return;
    const t = now();
    P.orders = P.orders.filter((o) => o.until > t);
    P.zones = P.zones.filter((z) => z.until > t);
    for (const o of P.orders) {
      const who = pickMembers(o);
      for (const m of who) {
        if (aiBusy(m, o)) continue;
        /* 이미 다른 명령을 수행 중인 멤버는 더 중요한 핑(도움·공격 등)만 가로챈다 */
        if (P.orders.some((q) => q !== o && q.ids.has(m.id) && TYPES[q.type].pri >= TYPES[o.type].pri)) continue;
        /* 이전 프레임에 act 가 goal 을 비웠다면(채굴 실패 등) 이 멤버는 수행 불가 */
        if (o.ids.has(m.id) && !m.goal && o.type === 'mine') { o.until = 0; m.say = '대상 없음'; m.sayT = 1.6; emit('ai_ping_response', { type: o.type, accepted: false, endReason: 'fail' }); break; }
        const g = orderGoal(m, o);
        if (!g) { if (o.ids.has(m.id)) { o.ids.delete(m.id); emit('ai_ping_response', { type: o.type, accepted: true, endReason: 'done' }); } continue; }
        if (!o.said.has(m.id)) { o.said.add(m.id); m.say = '✓ ' + TYPES[o.type].ko; m.sayT = 1.8; emit('ai_ping_response', { type: o.type, accepted: true, startLatencyMs: Math.round((t - o.t0) * 1000) }); }
        o.ids.add(m.id);
        m.goal = g; m.react = 0.3;   /* decide 를 건너뛰고 act 가 이 goal 을 수행한다 */
      }
    }
    /* 위험 구역 안의 멤버는 밖으로 — 전투 중이 아닐 때만 */
    for (const m of AICREW.members) {
      if (m.down || m.manual) continue;
      const z = inZone(m.x, m.y);
      if (!z || (m.goal && m.goal.kind === 'fight')) continue;
      let dx = m.x - z.x, dy = m.y - z.y; const L = Math.hypot(dx, dy) || 1; dx /= L; dy /= L;
      m.goal = { kind: 'follow', x: clamp(z.x + dx * (z.r + CELL), CELL, WW - CELL), y: clamp(z.y + dy * (z.r + CELL), CELL, WH - CELL), label: '핑 · 위험 회피' };
      m.react = 0.3; m.mineTarget = null;
    }
    /* AI 의 자발적 핑 — 관전 연출 */
    const adt = P._aiT ? Math.min(0.1, t - P._aiT) : 0; P._aiT = t;
    try { aiAutoPing(t, adt); } catch (e) { if (P.debug) console.error('[TCPING aiping]', e); }
  }

  /* ══════════ AI 크루의 자발적 핑 (관전 연출) ══════════
     AI 도 자기 상황에 맞는 핑을 가끔 보낸다. 표시·로그·소리만 낸다 —
     사람의 충전을 쓰지 않고, 다른 AI 에게 명령으로 작동하지 않으며,
     코옵으로 전송하지 않는다(AI 크루는 리더 로컬 전용이라 피어 화면에 없다). */
  const AIP = { globalNext: 0, gap: 4, cdMin: 10, cdMax: 22, perSec: 0.35 };
  function aiCtxForCell(c, r) {
    if (c < 0 || r < 0 || c >= COLS || r >= ROWS) return null;
    const t = G.cell[r * COLS + c]; if (!t || !CELL_KO[t]) return null;
    return { kind: (t === 'ore' || t === 'gem' || t === 'crys') ? 'ore' : 'wall', cellType: t, c, r, name: CELL_KO[t], x: c * CELL + CELL / 2, y: r * CELL + CELL / 2 };
  }
  function aiSend(m, type, x, y, ctx, t) {
    if (t == null) t = now();
    m._pingCd = t + AIP.cdMin + Math.random() * (AIP.cdMax - AIP.cdMin);
    AIP.globalNext = t + AIP.gap;
    if (setting('pingHide', false) && type !== 'help') return;   /* 완전 숨김 — AI 핑도 다른 크루의 핑이다 */
    const seat = 'ai' + m.id, name = 'AI ' + (ROLE_KO[m.roleId] || ''), w = mapClamp(x, y);
    addMarker({ id: seat + '-' + (++P.seq) + '-' + Date.now().toString(36), type, x: w.x, y: w.y, seat, name, ctx: ctx || null });
    emit('ai_ping', { type, role: m.roleId, ctx: ctx ? ctx.kind : 'floor' });
  }
  function aiAutoPing(t, dt) {
    if (!(typeof AICREW !== 'undefined') || !AICREW.members || t < AIP.globalNext || !playing()) return;
    /* 순번을 돌려 첫 멤버가 기회를 독점하지 않게 한다 */
    const N = AICREW.members.length; AIP.rot = ((AIP.rot || 0) + 1) % Math.max(1, N);
    for (let k = 0; k < N; k++) {
      const m = AICREW.members[(k + AIP.rot) % N];
      if (m.manual) continue;
      if (m._pingCd == null) m._pingCd = t + 6 + Math.random() * 8;   /* 합류 직후엔 잠시 조용히 */
      if (t < m._pingCd) continue;
      /* 기절 — 도움 요청은 확정, 6초마다 (§7.1 기절 규칙과 동일) */
      if (m.down) { aiSend(m, 'help', m.x, m.y, { kind: 'crew', who: 'ai', id: m.id, ref: m, name: 'AI ' + (ROLE_KO[m.roleId] || '') + ' 구조', x: m.x, y: m.y }, t); m._pingCd = t + 6; return; }
      const g = m.goal; if (!g || (g.label && g.label.indexOf('핑 ·') === 0)) continue;   /* 사람 핑을 따르는 중엔 되받아 핑하지 않는다 */
      let pick = null;
      if (g.kind === 'fight' && g.enemy && g.enemy.hp > 0) {
        const e = g.enemy;
        pick = { type: (e.boss || e.apex) ? 'danger' : 'attack', x: e.x, y: e.y, ctx: { kind: 'enemy', id: e._eid != null ? e._eid : null, ref: e, name: enemyName(e), x: e.x, y: e.y } };
      } else if (g.kind === 'mine' && g.c != null) {
        const ctx = aiCtxForCell(g.c, g.r), key = g.c + ',' + g.r;
        if (ctx && ctx.kind === 'ore') pick = { type: m._pingOre === key ? 'mine' : 'find', x: ctx.x, y: ctx.y, ctx };
        else if (ctx && Math.random() < 0.3) pick = { type: 'mine', x: ctx.x, y: ctx.y, ctx };
        if (pick) m._pingOre = key;
      } else if (g.kind === 'flare') pick = { type: 'here', x: g.x, y: g.y, ctx: null };
      else if (g.kind === 'turret' || g.kind === 'node') pick = { type: 'defend', x: m.x, y: m.y, ctx: null };
      else if (g.kind === 'revive') pick = { type: 'help', x: g.x, y: g.y, ctx: resolveContext(g.x, g.y) };
      else if (g.kind === 'escape') pick = { type: 'go', x: g.x, y: g.y, ctx: { kind: 'escape', name: '탈출 포트', x: g.x, y: g.y } };
      else if (g.kind === 'follow' && Math.hypot(g.x - m.x, g.y - m.y) > CELL * 6) pick = { type: 'go', x: g.x, y: g.y, ctx: resolveContext(g.x, g.y) };
      if (!pick) continue;
      /* "가끔" — 상황이 이어지는 동안 초당 35% 확률로 한 번 */
      if (Math.random() > AIP.perSec * dt) continue;
      aiSend(m, pick.type, pick.x, pick.y, pick.ctx, t);
      return;   /* 프레임당 한 명 — 전역 간격이 나머지를 조절한다 */
    }
  }

  /* ══════════ 입력 (§2) ══════════ */
  function canvasPos(e) {
    const cv = $('stage'); if (!cv) return null;
    const b = cv.getBoundingClientRect(); if (b.width < 2) return null;
    return { sx: (e.clientX - b.left) / b.width * LW, sy: (e.clientY - b.top) / b.height * LH };
  }
  function modalOpen() {
    const lv = $('infLevelModal'); if (lv && lv.classList.contains('on')) return true;
    const st = $('infSettlementModal'); if (st && st.classList.contains('on')) return true;
    return (typeof STARMAP !== 'undefined') && STARMAP.open;
  }
  function inputOk() {
    if (!playing() || modalOpen()) return false;
    if (window.TCCHAT && TCCHAT.open) return false;   /* 채팅 입력 중엔 G·V 를 글자로 쓴다 */
    const I = inf(); if (I && I.escape && I.escape.state === 'placing') return false;
    if ((typeof DLG !== 'undefined') && DLG.on) return false;
    return true;
  }
  function killHolds() {   /* 홀드 중인 장비 사용은 G 를 누르는 순간 끝낸다 (§2.1) */
    if (!G.mouse) return;
    G.mouse.down = false; G.mouse.drillDown = false; G.mouse.gunDown = false; G.mouse.ctrlGun = false;
  }
  function beginHold() {
    if (P.hold) return;
    const sx = P.cur.sx || (G.mouse && G.mouse.sx) || LW / 2, sy = P.cur.sy || (G.mouse && G.mouse.sy) || LH / 2;
    P.hold = { t0: now(), sx, sy, cx: sx, cy: sy, moved: 0, open: false, openAt: 0, hovered: null };
    killHolds();
  }
  function hoverIndex(h) {
    const dx = h.cx - h.sx, dy = h.cy - h.sy, d = Math.hypot(dx, dy);
    if (d < CFG.deadPx) return null;
    const a = Math.atan2(dy, dx);
    return ((Math.round((a + Math.PI / 2) / (Math.PI / 4)) % 8) + 8) % 8;
  }
  function endHold(commit) {
    const h = P.hold; if (!h) return; P.hold = null;
    if (!commit) { emit('ping_cancel', { reason: 'cancel' }); return; }
    const dt = (now() - h.t0) * 1000;
    if (!h.open && dt < CFG.tapMs && h.moved < CFG.tapMovePx) { sendPing('here', h.cx, h.cy, false); return; }
    const i = hoverIndex(h);
    if (i == null) { emit('ping_cancel', { reason: 'center' }); return; }
    sendPing(DIRS[i], h.cx, h.cy, false);
  }
  addEventListener('keydown', (e) => {
    if (e.repeat) return;
    const k = e.key.toLowerCase();
    if (e.code === 'Escape' && P.hold) { endHold(false); e.preventDefault(); e.stopImmediatePropagation(); return; }
    if (k === 'g' && !e.ctrlKey && !e.altKey && !e.metaKey) {
      if (!inputOk()) return;
      beginHold(); e.preventDefault();
    } else if (k === 'v' && !e.ctrlKey && !e.altKey && !e.metaKey) {
      if (!inputOk()) return;
      const sx = P.cur.sx || G.mouse.sx, sy = P.cur.sy || G.mouse.sy;
      sendPing('danger', sx, sy, true); e.preventDefault();
    }
  }, { capture: true });
  addEventListener('keyup', (e) => { if (e.key.toLowerCase() === 'g' && P.hold) { endHold(true); e.preventDefault(); } }, { capture: true });
  addEventListener('pointermove', (e) => {
    const p = canvasPos(e); if (!p) return;
    P.cur = p;
    const h = P.hold; if (!h) return;
    h.cx = p.sx; h.cy = p.sy; h.moved = Math.max(h.moved, Math.hypot(p.sx - h.sx, p.sy - h.sy));
    if (!h.open && (h.moved >= CFG.tapMovePx || (now() - h.t0) * 1000 >= CFG.tapMs)) { h.open = true; h.openAt = now(); emit('ping_wheel_open', {}); wheelTut(); }
    h.hovered = hoverIndex(h);
  }, { capture: true, passive: true });
  /* 휠이 열린 동안 좌·우클릭은 장비 입력으로 새기지 않는다. 우클릭은 취소 */
  addEventListener('pointerdown', (e) => {
    if (!P.hold) return;
    if (e.button === 2) endHold(false);
    e.preventDefault(); e.stopImmediatePropagation();
  }, { capture: true });
  addEventListener('pointerup', (e) => { if (P.hold) { e.preventDefault(); e.stopImmediatePropagation(); } }, { capture: true });
  addEventListener('blur', () => endHold(false));
  document.addEventListener('visibilitychange', () => { if (document.hidden) endHold(false); });
  addEventListener('pointercancel', () => endHold(false), { capture: true });

  /* ══════════ 그리기 (§5) — paintUI 뒤, UI 캔버스(안개 위) ══════════ */
  function drawGlyph(c, x, y, T, r, alpha) {
    c.globalAlpha = alpha;
    c.fillStyle = 'rgba(10,7,18,.88)'; c.beginPath(); c.arc(x, y, r, 0, 7); c.fill();
    c.lineWidth = 2; c.strokeStyle = T.col; c.stroke();
    c.fillStyle = T.col; c.font = '900 ' + Math.round(r * 1.05) + 'px "Segoe UI Symbol",Pretendard,"Malgun Gothic",sans-serif';
    c.textAlign = 'center'; c.textBaseline = 'middle'; c.fillText(T.glyph, x, y + 1);
  }
  function drawMarkers(c) {
    const t = now(), me = viewer(), reduced = setting('reducedMotion', false);
    const left = 52, right = LW - 52, top = 120, bottom = LH - 92;
    const off = [];
    for (const m of P.markers) {
      const T = TYPES[m.type], age = t - m.born, left_ = m.until - t;
      const s = w2s(m.x, m.y), sx = s[0], sy = s[1];
      const onScreen = sx >= left && sx <= right && sy >= top && sy <= bottom;
      if (!onScreen) { off.push(m); continue; }
      let alpha = left_ < 0.6 ? Math.max(0, left_ / 0.6) : 1;
      const pop = reduced ? 1 : age < CFG.popSec ? 0.4 + 0.6 * Math.sin((age / CFG.popSec) * Math.PI / 2) : 1;
      const base = 13 + (m.level - 1) * 2;
      c.save();
      /* 바닥 링 — 0.45초 퍼짐 (재강조 시 다시) */
      const ringAge = m.pulse ? Math.min(age, t - m.pulse) : age;
      if (!reduced && ringAge < CFG.ringSec) {
        const k = ringAge / CFG.ringSec;
        c.globalAlpha = alpha * (1 - k) * 0.8; c.strokeStyle = T.col; c.lineWidth = 2.5;
        c.beginPath(); c.ellipse(sx, sy, CELL * G.Z * (0.25 + 1.3 * k), CELL * G.Z * (0.25 + 1.3 * k) * 0.55, 0, 0, 7); c.stroke();
      }
      /* 위치 점 + 기둥 */
      c.globalAlpha = alpha * 0.9; c.fillStyle = T.col;
      c.beginPath(); c.ellipse(sx, sy, 4, 2.2, 0, 0, 7); c.fill();
      c.strokeStyle = T.col; c.lineWidth = 1.5; c.beginPath(); c.moveTo(sx, sy - 3); c.lineTo(sx, sy - 18 * pop); c.stroke();
      const iy = sy - 18 * pop - base * pop;
      drawGlyph(c, sx, iy, T, base * pop, alpha * 0.95);
      /* 스택 단계 표시 */
      if (m.level > 1) { c.globalAlpha = alpha; c.fillStyle = '#fff'; c.font = '900 10px Pretendard,"Malgun Gothic",sans-serif'; c.textAlign = 'left'; c.fillText('×' + m.level, sx + base + 3, iy - base * 0.6); }
      /* 동의 수 */
      if (m.agree.size) { c.globalAlpha = alpha; c.fillStyle = '#5FF5E0'; c.font = '900 10px Pretendard,"Malgun Gothic",sans-serif'; c.textAlign = 'left'; c.fillText('+' + m.agree.size, sx + base + 3, iy + base * 0.7); }
      /* 보낸 사람 · 핑 이름 — 1.2초 */
      const la = age < CFG.labelSec ? 1 : Math.max(0, 1 - (age - CFG.labelSec) / 0.25);
      if (la > 0) {
        const txt = seatName(m.seat, m.name) + ' · ' + T.ko + (m.ctx && m.ctx.name && m.ctx.kind !== 'unknown' ? ' · ' + m.ctx.name : m.ctx && m.ctx.kind === 'unknown' ? ' · 알 수 없는 위치' : '');
        c.font = '800 12px Pretendard,"Malgun Gothic",sans-serif'; c.textAlign = 'center'; c.textBaseline = 'middle';
        const tw = c.measureText(txt).width + 14, ly = iy - base - 14;
        c.globalAlpha = alpha * la * 0.85; c.fillStyle = 'rgba(10,7,18,.9)'; c.fillRect(sx - tw / 2, ly - 9, tw, 18);
        c.strokeStyle = T.col; c.lineWidth = 1; c.strokeRect(sx - tw / 2, ly - 9, tw, 18);
        c.globalAlpha = alpha * la; c.fillStyle = '#f6efff'; c.fillText(txt, sx, ly + 0.5);
      }
      c.restore();
    }
    /* 화면 밖 — 중요도순 최대 3개. 위험·도움·후퇴는 거리 무관, 나머지는 30칸 이내 (§5.2) */
    const alwaysOff = { danger: 1, help: 1, retreat: 1 };
    off.sort((a, b) => TYPES[b.type].pri - TYPES[a.type].pri);
    let n = 0;
    for (const m of off) {
      const dist = Math.hypot(m.x - me.x, m.y - me.y) / CELL;
      if (!alwaysOff[m.type] && dist > CFG.offTiles) continue;
      if (n++ >= CFG.offMax) break;
      const T = TYPES[m.type], s = w2s(m.x, m.y);
      const mx = LW * 0.5, my = LH * 0.5, dx = s[0] - mx, dy = s[1] - my;
      let tx = Infinity, ty = Infinity;
      if (Math.abs(dx) > 0.001) tx = (dx > 0 ? right - mx : left - mx) / dx;
      if (Math.abs(dy) > 0.001) ty = (dy > 0 ? bottom - my : top - my) / dy;
      const k = Math.max(0, Math.min(tx > 0 ? tx : Infinity, ty > 0 ? ty : Infinity));
      const px = mx + dx * k, py = my + dy * k, a = Math.atan2(dy, dx);
      const alpha = Math.max(0, Math.min(1, (m.until - now()) / 0.6));
      c.save();
      c.translate(px, py); c.rotate(a); c.globalAlpha = alpha; c.fillStyle = T.col;
      c.beginPath(); c.moveTo(24, 0); c.lineTo(14, -7); c.lineTo(14, 7); c.closePath(); c.fill();
      c.rotate(-a);
      drawGlyph(c, 0, 0, T, 13, alpha);
      c.globalAlpha = alpha; c.fillStyle = '#f6efff'; c.font = '800 10px ui-monospace,monospace'; c.textAlign = 'center'; c.textBaseline = 'middle';
      c.fillText(Math.max(1, Math.round(dist)) + '칸', 0, 22);
      c.restore();
    }
  }
  function drawLog(c) {
    const t = now();
    P.log = P.log.filter((l) => t - l.t < CFG.logSec);
    if (!P.log.length && !P.note) return;
    const x = 62, y0 = 150;   /* 좌상단 진행 모듈·심층 눈금을 피해 그 아래 */
    c.save(); c.font = '800 12px Pretendard,"Malgun Gothic",sans-serif'; c.textAlign = 'left'; c.textBaseline = 'middle';
    let i = 0;
    for (const l of P.log) {
      const a = Math.min(1, (CFG.logSec - (t - l.t)) / 0.4);
      const txt = l.text + (l.n > 1 ? ' ×' + l.n : ''), tw = c.measureText(txt).width + 22, y = y0 + i * 22;
      c.globalAlpha = a * 0.85; c.fillStyle = 'rgba(12,8,22,.86)'; c.fillRect(x, y - 10, tw, 20);
      c.fillStyle = l.col; c.fillRect(x, y - 10, 3, 20);
      c.globalAlpha = a; c.fillStyle = '#f6efff'; c.fillText(txt, x + 11, y + 0.5);
      i++;
    }
    if (P.note && P.note.until > t && P.note.text) {
      const y = y0 + i * 22, tw = c.measureText(P.note.text).width + 22;
      c.globalAlpha = 0.8; c.fillStyle = 'rgba(40,10,20,.86)'; c.fillRect(x, y - 10, tw, 20);
      c.fillStyle = '#ffb3c2'; c.fillText(P.note.text, x + 11, y + 0.5);
    } else if (P.note && P.note.until <= t) P.note = null;
    c.restore();
  }
  function drawWheel(c) {
    const h = P.hold; if (!h || !h.open) return;
    const R = CFG.radiusPx, cx0 = h.sx, cy0 = h.sy, t = now();
    const k = setting('reducedMotion', false) ? 1 : Math.min(1, (t - h.openAt) / 0.08);
    c.save();
    c.translate(cx0, cy0); c.scale(k, k);
    /* 배경 링 */
    c.globalAlpha = 0.82; c.fillStyle = 'rgba(12,8,22,.78)';
    c.beginPath(); c.arc(0, 0, R + 24, 0, 7); c.arc(0, 0, CFG.deadPx + 4, 0, 7, true); c.fill();
    c.globalAlpha = 0.5; c.strokeStyle = 'rgba(255,255,255,.18)'; c.lineWidth = 1;
    c.beginPath(); c.arc(0, 0, R + 24, 0, 7); c.stroke(); c.beginPath(); c.arc(0, 0, CFG.deadPx + 4, 0, 7); c.stroke();
    for (let i = 0; i < 8; i++) {
      const T = TYPES[DIRS[i]], a = -Math.PI / 2 + i * Math.PI / 4, hov = h.hovered === i;
      if (hov) {
        c.globalAlpha = 0.28; c.fillStyle = T.col;
        c.beginPath(); c.arc(0, 0, R + 24, a - Math.PI / 8, a + Math.PI / 8); c.arc(0, 0, CFG.deadPx + 4, a + Math.PI / 8, a - Math.PI / 8, true); c.closePath(); c.fill();
      }
      const ix = Math.cos(a) * (R - 8), iy = Math.sin(a) * (R - 8);
      drawGlyph(c, ix, iy - 8, T, hov ? 16 : 13, hov ? 1 : 0.85);
      c.globalAlpha = hov ? 1 : 0.8; c.fillStyle = hov ? '#fff' : '#d9cbe8';
      c.font = (hov ? '900 13px' : '800 12px') + ' Pretendard,"Malgun Gothic",sans-serif'; c.textAlign = 'center'; c.textBaseline = 'middle';
      c.fillText(T.ko, ix, iy + 14);
    }
    /* 중앙 — 여기(기본) / 충전·잠금 안내 */
    const locked = t < P.lockUntil, empty = P.charge < 1 && !G.downed;
    c.globalAlpha = 0.95; c.fillStyle = h.hovered == null ? 'rgba(95,184,255,.25)' : 'rgba(12,8,22,.6)';
    c.beginPath(); c.arc(0, 0, CFG.deadPx + 2, 0, 7); c.fill();
    c.fillStyle = locked || empty ? '#ffb3c2' : '#f6efff'; c.font = '900 10px Pretendard,"Malgun Gothic",sans-serif'; c.textAlign = 'center'; c.textBaseline = 'middle';
    c.fillText(locked ? (P.lockUntil - t).toFixed(1) + 's' : empty ? (CFG.regenSec - P.chargeT).toFixed(1) + 's' : '취소', 0, 0);
    /* 충전 점 4개 */
    for (let i = 0; i < CFG.charges; i++) { c.globalAlpha = 0.9; c.fillStyle = i < P.charge ? '#5FB8FF' : 'rgba(255,255,255,.18)'; c.beginPath(); c.arc(-15 + i * 10, R + 36, 3, 0, 7); c.fill(); }
    c.restore();
    /* 커서 십자 — 실제 핑 위치 */
    c.save(); c.globalAlpha = 0.9; c.strokeStyle = h.hovered != null ? TYPES[DIRS[h.hovered]].col : TYPES.here.col; c.lineWidth = 1.5;
    c.beginPath(); c.moveTo(h.cx - 8, h.cy); c.lineTo(h.cx + 8, h.cy); c.moveTo(h.cx, h.cy - 8); c.lineTo(h.cx, h.cy + 8); c.stroke(); c.restore();
  }
  function drawAll() {
    if (!ready() || SCENE !== 'depths') return;
    const c = (typeof ux !== 'undefined' && ux) ? ux : cx;
    if (!c) return;
    const t = now(), dt = P.lastT ? Math.min(0.1, t - P.lastT) : 0; P.lastT = t;
    tickCharges(dt); tickMarkers(); watchSocket(); keyGuide(); firstTut();
    /* 마우스를 안 움직여도 120ms 가 지나면 휠이 열린다 — 중앙에서 떼면 취소 */
    if (P.hold && !P.hold.open && (t - P.hold.t0) * 1000 >= CFG.tapMs) { P.hold.open = true; P.hold.openAt = t; emit('ping_wheel_open', {}); wheelTut(); }
    if (setting('pingHide', false)) P.markers = P.markers.filter((m) => m.type === 'help' || m.seat === mySeat());
    c.save(); c.setTransform(DPR, 0, 0, DPR, 0, 0);
    drawMarkers(c); drawLog(c); drawWheel(c);
    c.restore();
  }

  /* ══════════ 학습 (§10) · 설정 (§9) ══════════ */
  function keyGuide() {
    const el = $('infKeyGuide'); if (!el || el.querySelector('.tcPingKeys')) return;
    el.insertAdjacentHTML('beforeend', '<br><span class="tcPingKeys"><b>G</b> 탭 위치 핑 · 홀드 핑 휠　<b>V</b> 빠른 위험 핑</span>');
  }
  function firstTut() {
    if (P._tut || !playing()) return;
    P._tut = true;
    try { if (localStorage.getItem('tc_ping_tut_v1')) return; } catch (e) {}
    setTimeout(() => { if (playing()) { toastSafe('G 탭: 위치 알리기 · G 홀드: 팀 핑 선택 · V: 빠른 위험'); try { localStorage.setItem('tc_ping_tut_v1', '1'); } catch (e) {} } }, 6000);
  }
  function wheelTut() {
    try { if (localStorage.getItem('tc_ping_wheel_v1')) return; localStorage.setItem('tc_ping_wheel_v1', '1'); } catch (e) { return; }
    toastSafe('핑 휠: ↑가자 ↗공격 →발견 ↘채굴 ↓후퇴 ↙방어 ←도움 ↖위험');
  }
  function ensureSettings() {
    const sfx = $('tcSetSfx'); if (!sfx || $('tcSetPing')) return;
    const row = sfx.closest('.tcSettingsRow'); if (!row) return;
    row.insertAdjacentHTML('afterend',
      '<div class="tcSettingsRow"><div class="tcSettingsLabel">팀 핑 음량<span class="tcSettingsHint">크루가 보내는 핑 신호음</span></div><div class="tcSettingsControl"><input id="tcSetPing" type="range" min="0" max="100" value="100"><output id="tcSetPingOut">100%</output></div></div>'
      + '<div class="tcSettingsRow"><div class="tcSettingsLabel">핑 음소거<span class="tcSettingsHint">핑 신호음만 끈다</span></div><div class="tcSettingsControl"><button type="button" class="tcSettingsToggle" id="tcSetPingMute" aria-label="핑 음소거" aria-pressed="false"></button></div></div>'
      + '<div class="tcSettingsRow"><div class="tcSettingsLabel">핑 완전 숨김<span class="tcSettingsHint">다른 크루의 핑을 표시하지 않는다 · 구조 요청은 유지</span></div><div class="tcSettingsControl"><button type="button" class="tcSettingsToggle" id="tcSetPingHide" aria-label="핑 완전 숨김" aria-pressed="false"></button></div></div>');
    $('tcSetPing').addEventListener('input', (e) => { const v = Math.max(0, Math.min(1, (+e.target.value || 0) / 100)); CREW_SETTINGS.ping = v; if ((typeof AU !== 'undefined')) AU.vol.ping = v; save(); paintSettings(); });
    $('tcSetPingMute').addEventListener('click', () => { CREW_SETTINGS.pingMute = !CREW_SETTINGS.pingMute; save(); paintSettings(); });
    $('tcSetPingHide').addEventListener('click', () => { CREW_SETTINGS.pingHide = !CREW_SETTINGS.pingHide; save(); paintSettings(); });
    paintSettings();
  }
  function save() { try { if (typeof crewSaveSettings === 'function') crewSaveSettings(); } catch (e) {} }
  function paintSettings() {
    if (!(typeof CREW_SETTINGS !== 'undefined')) return;
    if (CREW_SETTINGS.ping == null) CREW_SETTINGS.ping = 1;
    const s = $('tcSetPing'), o = $('tcSetPingOut');
    if (s) s.value = Math.round(CREW_SETTINGS.ping * 100); if (o) o.textContent = Math.round(CREW_SETTINGS.ping * 100) + '%';
    for (const [id, on] of [['tcSetPingMute', !!CREW_SETTINGS.pingMute], ['tcSetPingHide', !!CREW_SETTINGS.pingHide]]) { const b = $(id); if (b) { b.classList.toggle('on', on); b.setAttribute('aria-pressed', String(on)); } }
    if ((typeof AU !== 'undefined')) AU.vol.ping = CREW_SETTINGS.ping;
  }

  /* ══════════ 훅 ══════════ */
  function hook() {
    if (typeof paintUI === 'function' && !paintUI._tcPing) {
      const prev = paintUI;
      paintUI = function () { prev.apply(this, arguments); try { drawAll(); } catch (e) { if (P.debug) console.error('[TCPING]', e); } };
      paintUI._tcPing = true;
    }
    if ((typeof AICREW !== 'undefined') && typeof AICREW.update === 'function' && !AICREW.update._tcPing) {
      const prev = AICREW.update;
      AICREW.update = function (dt) { try { aiApply(); } catch (e) { if (P.debug) console.error('[TCPING ai]', e); } return prev.apply(this, arguments); };
      AICREW.update._tcPing = true;
    }
    if (typeof crewPaintSettings === 'function' && !crewPaintSettings._tcPing) {
      const prev = crewPaintSettings;
      crewPaintSettings = function () { const r = prev.apply(this, arguments); try { ensureSettings(); paintSettings(); } catch (e) {} return r; };
      crewPaintSettings._tcPing = true;
    }
    ensureSettings();
    watchSocket();   /* 프레임이 멈춘 탭(백그라운드)에서도 코옵 소켓 리스너는 붙어 있어야 한다 */
    if ((typeof AU !== 'undefined')) AU.vol.ping = setting('ping', 1);
  }
  function boot() { hook(); setInterval(hook, 1000); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot); else boot();

  /* 디버그/테스트용 API */
  P.send = (type, wx, wy, quick) => { const s = w2s(wx, wy); return sendPing(type, s[0], s[1], !!quick); };   /* quick=true 면 취소·동의 판정을 건너뛴다(V 키와 동일) */
  P.simulateRemote = (msg) => onRemote(Object.assign({ v: P.ver, from: 'p2', fromName: '테스트', at: Date.now() }, msg));
  P.reset = () => { P.markers = []; P.log = []; P.orders = []; P.zones = []; P.charge = CFG.charges; P.lockUntil = 0; P.attempts = []; };
  P.aiPing = AIP;   /* 튠: gap·cdMin·cdMax·perSec */
  P.aiTick = aiAutoPing;   /* 테스트용 — (t초, dt초) */
})();
