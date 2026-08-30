/**
 * LAN co-op client v2 — 트랙 F (기획서 §19.1, §17.4-4)
 *
 * v1은 tunnel-crew-loop-demo(채취 미션) 2인 전용이었다.
 * v2는 2~4인 좌석(p1~p4) 모델과 본편 무한 모드(mode:'infinite')를 지원한다.
 *
 * 동기화 경계 (§17.4-4 점검 결과):
 * - 맵: 무한 모드 생성기는 DEMO.seed 기반(hashSeed(seed|depth|size))이라
 *   시드만 맞추면 전 클라이언트 동일 맵이다. 시드는 방 생성 시 서버가 배포한다.
 * - 개인 성장(§5.2): 경험치·레벨·카드 선택은 동기화하지 않는다. 레벨 숫자만
 *   명판 표시용으로 알린다(level 이벤트).
 * - 탈출(§8.4): 요청은 escape 이벤트로 전 클라이언트가 같은 지점·대기시간을
 *   재현하고, 탑승은 board 이벤트로 집계해 "생존자 전원 탑승"을 각자 판정한다.
 * - 벽 파괴·플레어·위치는 v1 방식 유지(이벤트 릴레이).
 * - 적 AI·보스 HP는 아직 로컬(비동기) — 다음 단계 항목.
 *
 * Loaded only when served from coop host. Solo file:// still works without this.
 */
(function () {
  'use strict';

  const COOP = {
    active: false,
    seat: null, // 'p1' ~ 'p4' (p1 = 호스트)
    code: null,
    seed: null,
    mode: 'harvest', // 'harvest' | 'infinite'
    ws: null,
    room: null,
    myRole: null,
    peers: new Map(), // seat -> {x,y,aim,face,drill,weapon,roleId,php,moving,level,status}
    sharedRes: 0,
    _applying: false,
    _acc: 0,
    _started: false,
    _infLaunch: false, // infStartRun 이 코옵을 끄지 않게 하는 신호
    _localStatus: 'play', // 'play' | 'boarded' | 'down' | 'escaped'
  };
  window.COOP = COOP;

  function $(id) {
    return document.getElementById(id);
  }

  function send(obj) {
    if (COOP.ws && COOP.ws.readyState === 1) COOP.ws.send(JSON.stringify(obj));
  }

  function status(msg, isErr) {
    const el = $('coopStatus');
    if (!el) return;
    el.textContent = msg || '';
    el.style.color = isErr ? '#ff8a8a' : '#9a86b5';
  }

  function isInfinite() {
    return COOP.mode === 'infinite';
  }

  function peerOf(seat) {
    if (!COOP.peers.has(seat)) {
      COOP.peers.set(seat, {
        x: NaN, y: NaN, aim: 0, face: 1, drill: 0, weapon: 'drill',
        roleId: null, php: 100, moving: false, level: 1, status: 'play',
      });
    }
    return COOP.peers.get(seat);
  }

  const ROLE_KO = { driller: '드릴러', gunner: '거너', scout: '스카우트', engineer: '엔지니어' };

  function paintLobby() {
    const r = COOP.room;
    const codeEl = $('coopCodeShow');
    const peerEl = $('coopPeer');
    const startBtn = $('coopStart');
    const list = $('coopPlayers');
    if (codeEl) codeEl.textContent = COOP.code || '————';
    if (peerEl) {
      if (!r) peerEl.textContent = '대기 중…';
      else peerEl.textContent = '접속 ' + r.players.length + '/4 · 모드: ' + (r.mode === 'infinite' ? '무한 모드 (본편)' : '채취 미션');
    }
    if (list) {
      list.innerHTML = (r ? r.players : [])
        .map((p) => {
          const me = p.seat === COOP.seat ? ' (나)' : '';
          const host = p.seat === 'p1' ? '👑 ' : '';
          return '<span class="coopSeat' + (p.role ? ' roled' : '') + '">' + host + p.seat.toUpperCase() + me + ' · ' + (p.role ? ROLE_KO[p.role] || p.role : '역할 미선택') + '</span>';
        })
        .join('');
    }
    document.querySelectorAll('#coopModeRow button').forEach((b) => {
      b.classList.toggle('picked', r && r.mode === b.getAttribute('data-mode'));
      b.disabled = COOP.seat !== 'p1';
    });
    if (startBtn) {
      startBtn.style.display = COOP.seat === 'p1' ? 'inline-block' : 'none';
      startBtn.disabled = !(r && r.players.length >= 2 && r.players.every((p) => p.role));
    }
    document.querySelectorAll('#coopRoles .roleBtn').forEach((btn) => {
      const id = btn.getAttribute('data-role');
      btn.classList.toggle('picked', COOP.myRole === id);
      btn.setAttribute('aria-pressed', COOP.myRole === id ? 'true' : 'false');
    });
  }

  function showLobby(on) {
    const el = $('coopLobby');
    if (el) el.classList.toggle('on', !!on);
    if (on) {
      ['crewMenu', 'crewMission', 'crewBiome', 'crewRole', 'crewResult'].forEach((id) => {
        const n = $(id);
        if (n) n.classList.remove('on');
      });
      if (typeof CREW !== 'undefined') CREW.phase = 'coop';
    }
  }

  window.COOP_openLobby = function () {
    if (location.protocol === 'file:') {
      toast && toast('코옵은 서버로 열어야 합니다 (cd coop && npm start)');
      return;
    }
    buildUI();
    if (!COOP.ws || COOP.ws.readyState > 1) connect();
    showLobby(true);
    status(COOP.ws && COOP.ws.readyState === 1 ? '서버 연결됨' : '서버 연결 중…');
  };

  window.COOP_closeLobby = function () {
    showLobby(false);
  };

  function connect() {
    const proto = location.protocol === 'https:' ? 'wss' : 'ws';
    const url = proto + '://' + location.host;
    const ws = new WebSocket(url);
    COOP.ws = ws;
    ws.onopen = () => status('서버 연결됨');
    ws.onclose = () => status('연결 끊김 — 호스트 서버를 확인하세요', true);
    ws.onerror = () => status('연결 오류', true);
    ws.onmessage = (ev) => {
      let msg;
      try {
        msg = JSON.parse(ev.data);
      } catch {
        return;
      }
      onMsg(msg);
    };
  }

  function onMsg(msg) {
    switch (msg.t) {
      case 'hello':
        break;
      case 'hosted':
        COOP.seat = msg.seat;
        COOP.code = msg.code;
        COOP.seed = msg.seed;
        status('방 생성됨 — 코드를 공유하세요');
        break;
      case 'joined':
        COOP.seat = msg.seat;
        COOP.code = msg.code;
        COOP.seed = msg.seed;
        COOP.mode = msg.mode || 'harvest';
        status('참가 완료 · 좌석 ' + msg.seat.toUpperCase());
        break;
      case 'room':
        COOP.room = msg;
        COOP.mode = msg.mode || 'harvest';
        paintLobby();
        break;
      case 'err':
        status(msg.m || '오류', true);
        break;
      case 'start':
        beginMission(msg);
        break;
      case 'state': {
        const p = peerOf(msg.from);
        Object.assign(p, {
          x: msg.x, y: msg.y, aim: msg.aim, face: msg.face, drill: msg.drill,
          weapon: msg.weapon, roleId: msg.roleId, php: msg.php, moving: msg.moving,
        });
        if (typeof msg.level === 'number') p.level = msg.level;
        break;
      }
      case 'dig':
      case 'hp':
      case 'break':
        applyDig(msg);
        break;
      case 'lamp':
        applyLamp(msg);
        break;
      case 'res':
        COOP.sharedRes = Math.max(COOP.sharedRes | 0, msg.n | 0);
        if (typeof paintCrewHud === 'function') paintCrewHud();
        break;
      case 'escape':
        /* 다른 크루가 탈출 포트를 요청했다 — 같은 지점·대기시간으로 재현 */
        COOP._applying = true;
        try {
          if (typeof infCoopApplyEscape === 'function') infCoopApplyEscape(msg);
        } finally {
          COOP._applying = false;
        }
        break;
      case 'board': {
        peerOf(msg.from).status = 'boarded';
        toast && toast(msg.from.toUpperCase() + ' 탈출 포트 탑승');
        break;
      }
      case 'level': {
        const p = peerOf(msg.from);
        p.level = msg.level | 0;
        break;
      }
      case 'end': {
        const p = peerOf(msg.from);
        p.status = msg.kind === 'escaped' ? 'escaped' : 'down';
        toast && toast(msg.from.toUpperCase() + (msg.kind === 'escaped' ? ' 생환' : ' 쓰러짐'));
        break;
      }
      case 'peer_left':
        COOP.peers.delete(msg.seat);
        toast && toast(msg.seat ? msg.seat.toUpperCase() + ' 퇴장' : '상대 퇴장');
        paintLobby();
        break;
    }
  }

  /* ── 수신 이벤트 적용 (v1 방식 유지) ── */

  function applyPeerNoop() {}

  function applyDig(msg) {
    if (typeof damage !== 'function' || typeof G === 'undefined') return;
    COOP._applying = true;
    try {
      const c = msg.c | 0,
        r = msg.r | 0;
      if (typeof inB === 'function' && !inB(c, r)) return;
      const k = typeof ci === 'function' ? ci(c, r) : null;
      if (msg.t === 'break' || msg.hp === 0) {
        if (G.cell[k]) {
          if (typeof infOnBlockBroken === 'function' && typeof INF !== 'undefined' && INF.active) {
            /* 코옵 피어의 파괴도 장악도에 기여한다. 경험치는 개인별(§5.2)이라
               infOnBlockBroken 을 그대로 부르면 내 경험치가 오르므로 직접 지운다. */
            INF.floorBroken++;
            INF.totalBlocks++;
          }
          G.cell[k] = null;
          G.hp.delete(k);
          if (G.dec) G.dec[k] = 0;
          G.compDirty = true;
          if (typeof LOS !== 'undefined') LOS.markDirty();
          if (typeof J !== 'undefined' && J.burst && typeof cxw === 'function') {
            J.burst(cxw(c), cyw(r), 6, ['#C8B8E8', '#FFF'], 120);
          }
        }
      } else if (msg.t === 'hp' || msg.t === 'dig') {
        if (G.cell[k]) G.hp.set(k, msg.hp);
      }
    } finally {
      COOP._applying = false;
    }
  }

  function applyLamp(msg) {
    if (typeof G === 'undefined' || !G.lamps) return;
    G.lamps.push({
      c: 0, r: 0, x: msg.x, y: msg.y, ph: Math.random() * 6,
      rad: msg.rad || 120, ttl: msg.ttl || 18, flare: true,
    });
  }

  /* ── 미션 시작 ── */

  function beginMission(msg) {
    COOP._started = true;
    COOP.active = true;
    COOP.sharedRes = 0;
    COOP.peers.clear();
    COOP.mode = msg.mode || 'harvest';
    COOP._localStatus = 'play';
    showLobby(false);

    if (typeof DEMO !== 'undefined' && msg.seed) {
      DEMO.seed = msg.seed;
      COOP.seed = msg.seed;
    }

    const mine = (msg.players || []).find((p) => p.seat === COOP.seat);
    COOP.myRole = (mine && mine.role) || COOP.myRole || 'driller';
    for (const p of msg.players || []) {
      if (p.seat !== COOP.seat) {
        const peer = peerOf(p.seat);
        peer.roleId = p.role;
        peer.status = 'play';
      }
    }

    if (isInfinite()) {
      /* 본편 무한 모드 — infStartRun 이 COOP 를 끄지 않도록 신호를 든다 */
      COOP._infLaunch = true;
      try {
        if (typeof infStartRun === 'function') infStartRun(COOP.myRole);
      } finally {
        COOP._infLaunch = false;
      }
    } else {
      if (typeof DEMO !== 'undefined') DEMO.enemyOn = false; // 채취 미션 첫 테스트용 완화 (v1 유지)
      if (typeof applyDemoToDungen === 'function') applyDemoToDungen();
      if (typeof startCrewMission === 'function') startCrewMission(COOP.myRole);
    }

    /* 좌석별 스폰 오프셋 — 겹침 방지 */
    requestAnimationFrame(() => {
      const idx = Math.max(0, parseInt(String(COOP.seat).slice(1), 10) - 1);
      if (idx > 0 && typeof G !== 'undefined' && G.sh) {
        const off = (typeof CELL !== 'undefined' ? CELL * 0.9 : 40) * idx;
        G.sh.x += off;
        if (typeof collide === 'function' && typeof R_SHELLY !== 'undefined') collide(G.sh, R_SHELLY);
      }
      toast && toast('코옵 시작 (' + (isInfinite() ? '무한 모드' : '채취') + ') · 나: ' + (ROLE_KO[COOP.myRole] || COOP.myRole) + ' · ' + (COOP.room ? COOP.room.players.length : 2) + '인');
    });
  }

  /* ── 게임 → 네트워크 훅 ── */

  window.COOP_onDamage = function (c, r, hpOrBreak) {
    if (!COOP.active || COOP._applying) return;
    if (hpOrBreak === 0 || hpOrBreak === 'break') send({ t: 'break', c, r, hp: 0 });
    else send({ t: 'hp', c, r, hp: hpOrBreak });
  };

  window.COOP_onLoot = function () {
    if (!COOP.active || isInfinite()) return;
    send({ t: 'res', n: G.nRes | 0 });
  };

  window.COOP_onLamp = function (x, y, rad, ttl) {
    if (!COOP.active || COOP._applying) return;
    send({ t: 'lamp', x, y, rad, ttl });
  };

  /* 탈출 포트 (§8.4) — 요청 브로드캐스트와 탑승 집계 */
  window.COOP_onEscape = function (x, y, need, auto) {
    if (!COOP.active || COOP._applying || !isInfinite()) return;
    send({ t: 'escape', x, y, need, auto: !!auto });
  };
  window.COOP_onBoard = function () {
    if (!COOP.active || !isInfinite()) return;
    COOP._localStatus = 'boarded';
    send({ t: 'board' });
  };
  /* null = 솔로(코옵 아님) → 게임은 기존 솔로 판정을 쓴다.
     true/false = 코옵 판정: 나 포함 생존자 전원이 탑승했는가 (§8.4-4 — 다운된 크루는 제외) */
  window.COOP_escapeAllAboard = function () {
    if (!COOP.active || !COOP._started || !isInfinite()) return null;
    if (COOP._localStatus !== 'boarded') return false;
    for (const p of COOP.peers.values()) {
      if (p.status === 'play') return false;
    }
    return true;
  };
  window.COOP_escapeCount = function () {
    if (!COOP.active || !isInfinite()) return null;
    let boarded = COOP._localStatus === 'boarded' ? 1 : 0,
      total = 1;
    for (const p of COOP.peers.values()) {
      if (p.status === 'down' || p.status === 'escaped') continue;
      total++;
      if (p.status === 'boarded') boarded++;
    }
    return { boarded, total };
  };
  window.COOP_onLevel = function (level) {
    if (!COOP.active || !isInfinite()) return;
    send({ t: 'level', level: level | 0 });
  };
  window.COOP_onInfEnd = function (reason, escaped) {
    if (!COOP.active || !isInfinite()) return;
    COOP._localStatus = escaped ? 'escaped' : 'down';
    send({ t: 'end', kind: escaped ? 'escaped' : 'down' });
    COOP._started = false;
  };

  window.COOP_endBroadcast = function (kind) {
    if (!COOP.active || COOP._ending) return;
    COOP._ending = true;
    send({ t: 'end', kind });
    setTimeout(() => {
      COOP._ending = false;
    }, 500);
  };

  /* ── 주기 송신 ── */

  window.COOP_tick = function (dt) {
    if (!COOP.active || !COOP._started) return;
    COOP._acc += dt;
    if (COOP._acc >= 1 / 20) {
      COOP._acc = 0;
      if (G && G.sh) {
        send({
          t: 'state',
          x: G.sh.x, y: G.sh.y, aim: G.sh.aim, face: G.sh.face,
          drill: G.sh.drill | 0, weapon: G.weapon, roleId: COOP.myRole,
          php: G.php, moving: Math.hypot(G.sh.vx || 0, G.sh.vy || 0) > 1,
          level: typeof INF !== 'undefined' && INF.active ? INF.level : 1,
        });
      }
    }
    /* 채취 미션의 공유 목표 (v1 유지) — 무한 모드는 개인 성장·개인 가방 */
    if (!isInfinite() && typeof CREW !== 'undefined' && CREW.phase === 'play') {
      const local = G.nRes | 0;
      if (local > COOP.sharedRes) send({ t: 'res', n: local });
      CREW.goalHave = Math.max(local, COOP.sharedRes | 0);
      if (CREW.goalHave >= CREW.goalNeed) CREW.canEscape = true;
    }
  };

  /* ── 피어 렌더 ── */

  window.COOP_drawPeer = function () {
    if (!COOP.active || !COOP.peers.size) return;
    for (const [seat, p] of COOP.peers) {
      if (!isFinite(p.x) || !isFinite(p.y)) continue;
      if (p.status === 'escaped') continue; // 생환한 크루는 현장에 없다
      const drilling = p.weapon === 'drill' && !!p.drill;
      const aim = typeof p.aim === 'number' ? p.aim : p.face < 0 ? Math.PI : 0;
      const face = p.face || 1;
      const moving = !!p.moving;

      cx.save();
      const down = p.status === 'down';
      if (down) cx.globalAlpha = 0.45;
      cx.fillStyle = 'rgba(10,6,20,.65)';
      cx.strokeStyle = down ? 'rgba(255,110,140,.7)' : 'rgba(200,160,255,.7)';
      cx.lineWidth = 2;
      const label =
        (ROLE_KO[p.roleId] || p.roleId || seat).toUpperCase() +
        (isInfinite() ? ' Lv' + (p.level || 1) : '') +
        (p.status === 'boarded' ? ' · 탑승' : down ? ' · 다운' : '');
      cx.font = '800 11px Pretendard, Malgun Gothic, sans-serif';
      cx.textAlign = 'center';
      const tw = cx.measureText(label).width + 14;
      const bx = p.x - tw / 2,
        by = p.y - (typeof R_SHELLY !== 'undefined' ? R_SHELLY : 16) - 28;
      cx.fillRect(bx, by, tw, 16);
      cx.strokeRect(bx, by, tw, 16);
      cx.fillStyle = down ? '#ffc9d6' : '#e8d6ff';
      cx.fillText(label, p.x, by + 12);

      let drawn = false;
      if (p.roleId === 'driller' && typeof drawMinerSprite === 'function') {
        drawn = !!drawMinerSprite(p.x, p.y, R_SHELLY, G.t, aim, drilling, moving);
      }
      if (!drawn) {
        if (typeof drawShellyVector === 'function') {
          drawShellyVector(p.x, p.y, R_SHELLY, G.t, face, drilling, aim, p.weapon !== 'drill');
        } else if (typeof drawShelly === 'function') {
          drawShelly(p.x, p.y, R_SHELLY, G.t, face, drilling, aim, p.weapon !== 'drill', moving);
        }
      }
      cx.restore();
    }
  };

  /* LOS 시야 합산 — 다중 피어 */
  window.COOP_peersXY = function () {
    if (!COOP.active || !COOP.peers.size) return null;
    const out = [];
    for (const p of COOP.peers.values()) {
      if (isFinite(p.x) && isFinite(p.y) && p.status !== 'escaped') out.push([p.x, p.y]);
    }
    return out.length ? out : null;
  };
  /* v1 호환 — 첫 피어 */
  window.COOP_peerXY = function () {
    const all = window.COOP_peersXY();
    return all ? all[0] : null;
  };

  /* ── 로비 UI ── */

  function buildUI() {
    if ($('coopLobby')) return;
    const css = document.createElement('style');
    css.textContent = `
#coopLobby{position:absolute;inset:0;z-index:60;display:none;align-items:center;justify-content:center;
  background:rgba(6,4,14,.88);backdrop-filter:blur(6px);padding:20px;pointer-events:auto}
#coopLobby.on{display:flex}
#coopCard{width:min(560px,94vw);background:#1a1228;border:1px solid #4a3568;border-radius:16px;
  padding:22px 20px 18px;color:#e4d4f5;font:13px/1.5 Pretendard,Malgun Gothic,sans-serif}
#coopCard h1{margin:0 0 6px;font:800 22px/1.2 Pretendard,sans-serif;color:#edd8ff;text-align:center}
#coopCard .sub{text-align:center;color:#9a86b5;margin-bottom:14px;font-size:12.5px}
#coopCard .row{display:flex;gap:8px;flex-wrap:wrap;justify-content:center;margin:10px 0}
#coopCard button,#coopCard .roleBtn{font:inherit;background:#2a1a42;color:#e4d4f5;border:1px solid #4a3568;
  border-radius:10px;padding:10px 14px;cursor:pointer}
#coopCard button.pri{background:rgba(180,120,255,.22);border-color:rgba(180,120,255,.5);color:#edd8ff;font-weight:700}
#coopCard button.picked{border-color:#a878e0;background:rgba(168,120,224,.18)}
#coopCard button:disabled{opacity:.4;cursor:default}
#coopCard input{background:#0c0814;color:#e4d4f5;border:1px solid #3a2a55;border-radius:8px;
  padding:10px 12px;font:700 16px ui-monospace,Menlo,Consolas,monospace;letter-spacing:.12em;
  width:8em;text-align:center;text-transform:uppercase}
#coopCodeShow{font:800 28px/1 ui-monospace,Menlo,Consolas,monospace;letter-spacing:.2em;color:#d4a8ff;text-align:center;margin:8px 0}
#coopPlayers{display:flex;gap:6px;flex-wrap:wrap;justify-content:center;min-height:1.6em;margin:6px 0}
#coopPlayers .coopSeat{padding:4px 10px;border:1px solid #3a2a55;border-radius:999px;font-size:11px;color:#9a86b5}
#coopPlayers .coopSeat.roled{border-color:#7febd0;color:#c9f5e8}
#coopRoles{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:5px;margin-top:8px}
#coopRoles .roleBtn{text-align:left;padding:10px}
#coopRoles .roleBtn b{display:block;color:#d4a8ff;margin-bottom:2px}
#coopRoles .roleBtn span{font-size:11px;color:#9a86b5}
#coopRoles .roleBtn.picked{border-color:#a878e0;background:rgba(168,120,224,.18)}
#coopStatus{text-align:center;min-height:1.2em;margin-top:8px;font-size:12px;color:#9a86b5}
`;
    document.head.appendChild(css);

    const div = document.createElement('div');
    div.id = 'coopLobby';
    div.innerHTML = `
      <div id="coopCard">
        <h1>땅굴 크루 · LAN 코옵</h1>
        <p class="sub">같은 Wi‑Fi에서 방 만들고 참가하세요 · 최대 4인</p>
        <div class="row">
          <button class="pri" id="coopHostBtn">방 만들기 (호스트)</button>
          <input id="coopJoinCode" maxlength="4" placeholder="CODE" />
          <button id="coopJoinBtn">참가</button>
        </div>
        <div id="coopCodeShow">————</div>
        <p class="sub" id="coopPeer">대기 중…</p>
        <div id="coopPlayers"></div>
        <div class="row" id="coopModeRow">
          <button data-mode="harvest">채취 미션</button>
          <button data-mode="infinite">무한 모드 (본편)</button>
        </div>
        <div id="coopRoles" class="tcRoleSelectGrid" aria-label="코옵 직업 선택">
          <button class="roleBtn tcRoleChoice" data-role="driller" style="--role:#ffd36e"><span class="tcRoleCode">EXCAVATION</span><img class="tcRoleArt" src="/assets/menu/char-driller-select-v4.png" alt="드릴러"><span class="tcRoleCopy"><b class="tcRoleName">드릴러</b><span class="tcRoleTag">채굴 · 돌파</span></span><i class="tcRoleCheck" aria-hidden="true">✓</i></button>
          <button class="roleBtn tcRoleChoice" data-role="gunner" style="--role:#ff8d72"><span class="tcRoleCode">FIRE SUPPORT</span><img class="tcRoleArt" src="/assets/menu/char-gunner-select-v4.png" alt="거너"><span class="tcRoleCopy"><b class="tcRoleName">거너</b><span class="tcRoleTag">사격 · 발파</span></span><i class="tcRoleCheck" aria-hidden="true">✓</i></button>
          <button class="roleBtn tcRoleChoice" data-role="scout" style="--role:#7febd0"><span class="tcRoleCode">RECON</span><img class="tcRoleArt" src="/assets/menu/char-scout-select-v4.png" alt="스카우트"><span class="tcRoleCopy"><b class="tcRoleName">스카우트</b><span class="tcRoleTag">시야 · 기동</span></span><i class="tcRoleCheck" aria-hidden="true">✓</i></button>
          <button class="roleBtn tcRoleChoice" data-role="engineer" style="--role:#c7a0ff"><span class="tcRoleCode">FORTIFICATION</span><img class="tcRoleArt" src="/assets/menu/char-engineer-select-v4.png" alt="엔지니어"><span class="tcRoleCopy"><b class="tcRoleName">엔지니어</b><span class="tcRoleTag">전력망 · 센트리</span></span><i class="tcRoleCheck" aria-hidden="true">✓</i></button>
        </div>
        <div class="row" style="margin-top:14px">
          <button class="pri" id="coopStart" disabled>미션 시작 (호스트)</button>
          <button id="coopBackMenu">메인 메뉴</button>
        </div>
        <div id="coopStatus"></div>
      </div>`;
    const app = $('app') || document.body;
    app.appendChild(div);

    $('coopHostBtn').onclick = () => {
      const seed =
        typeof DEMO !== 'undefined' && DEMO.seed
          ? DEMO.seed
          : 'tunnel-' + Math.floor(Math.random() * 1e9);
      send({ t: 'host', seed });
    };
    $('coopJoinBtn').onclick = () => {
      const code = ($('coopJoinCode').value || '').trim().toUpperCase();
      if (code.length < 4) {
        status('방 코드 4자를 입력하세요', true);
        return;
      }
      send({ t: 'join', code });
    };
    $('coopJoinCode').addEventListener('keydown', (e) => {
      if (e.key === 'Enter') $('coopJoinBtn').click();
    });
    document.querySelectorAll('#coopModeRow button').forEach((btn) => {
      btn.addEventListener('click', () => {
        if (COOP.seat !== 'p1') return;
        send({ t: 'mode', mode: btn.getAttribute('data-mode') });
      });
    });
    document.querySelectorAll('#coopRoles .roleBtn').forEach((btn) => {
      btn.addEventListener('click', () => {
        if (!COOP.seat) {
          status('먼저 방을 만들거나 참가하세요', true);
          return;
        }
        COOP.myRole = btn.getAttribute('data-role');
        send({ t: 'role', role: COOP.myRole, ready: true });
        paintLobby();
      });
    });
    $('coopStart').onclick = () => send({ t: 'start' });
    $('coopBackMenu').onclick = () => {
      showLobby(false);
      COOP.active = false;
      COOP._started = false;
      status('');
      if (typeof crewShow === 'function') crewShow('menu');
      else {
        const menu = $('crewMenu');
        if (menu) menu.classList.add('on');
      }
    };
  }

  /* 메인 메뉴의 LAN 코옵 버튼은 마크업에서 '임시 잠금' 상태로 배포된다
     (서버 없이 열면 눌러도 아무 일이 없으므로). 코옵 호스트로 서빙되는
     경우에만 잠금을 푼다 — file:// 로 연 빌드는 잠긴 채로 남는다. */
  function unlockMenuButton() {
    const btn = $('menuCoop');
    if (!btn) return;
    btn.disabled = false;
    btn.classList.remove('locked');
    btn.removeAttribute('aria-disabled');
    btn.removeAttribute('title');
    const tag = btn.querySelector('.lockedTag');
    if (tag) tag.remove();
  }

  function boot() {
    if (location.protocol === 'file:') return;
    unlockMenuButton();
    buildUI();
    connect();
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
  else boot();
})();
