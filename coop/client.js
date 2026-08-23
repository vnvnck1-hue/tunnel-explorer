/**
 * LAN co-op client for tunnel-crew-loop-demo.html
 * Loaded only when served from coop host. Solo file:// still works without this.
 */
(function () {
  'use strict';

  const COOP = {
    active: false,
    seat: null, // 'host' | 'guest'
    code: null,
    seed: null,
    ws: null,
    room: null,
    myRole: null,
    peer: null, // remote avatar state
    sharedRes: 0,
    _applying: false,
    _acc: 0,
    _started: false,
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

  function paintLobby() {
    const r = COOP.room;
    const codeEl = $('coopCodeShow');
    const peerEl = $('coopPeer');
    const startBtn = $('coopStart');
    if (codeEl) codeEl.textContent = COOP.code || '————';
    if (peerEl) {
      if (!r) peerEl.textContent = '대기 중…';
      else if (r.hasHost && r.hasGuest) peerEl.textContent = '연결됨 · 둘 다 역할 선택';
      else if (COOP.seat === 'host') peerEl.textContent = '게스트 접속 대기…';
      else peerEl.textContent = '호스트에 연결됨';
    }
    if (startBtn) {
      startBtn.style.display = COOP.seat === 'host' ? 'inline-block' : 'none';
      startBtn.disabled = !(r && r.hasGuest && r.hostRole && r.guestRole);
    }
    document.querySelectorAll('#coopRoles .roleBtn').forEach((btn) => {
      const id = btn.getAttribute('data-role');
      btn.classList.toggle('picked', COOP.myRole === id);
      const taken =
        r &&
        ((COOP.seat === 'host' && r.guestRole === id) ||
          (COOP.seat === 'guest' && r.hostRole === id));
      btn.classList.toggle('taken', !!taken && COOP.myRole !== id);
      btn.disabled = !!taken && COOP.myRole !== id;
    });
    const hr = $('coopHostRole');
    const gr = $('coopGuestRole');
    if (hr) hr.textContent = r && r.hostRole ? r.hostRole : '—';
    if (gr) gr.textContent = r && r.guestRole ? r.guestRole : '—';
  }

  function showLobby(on) {
    const el = $('coopLobby');
    if (el) el.classList.toggle('on', !!on);
    if (on) {
      const menu = $('crewMenu');
      const miss = $('crewMission');
      const crew = $('crewRole');
      const res = $('crewResult');
      if (menu) menu.classList.remove('on');
      if (miss) miss.classList.remove('on');
      const bio = $('crewBiome');
      if (bio) bio.classList.remove('on');
      if (crew) crew.classList.remove('on');
      if (res) res.classList.remove('on');
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
    ws.onclose = () => {
      status('연결 끊김 — 호스트 서버를 확인하세요', true);
      if (COOP.active && typeof endCrewMission === 'function' && CREW.phase === 'play') {
        toast && toast('상대 연결 끊김');
      }
    };
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
        status('코옵 서버 준비됨');
        break;
      case 'hosted':
        COOP.seat = 'host';
        COOP.code = msg.code;
        COOP.seed = msg.seed;
        status('방 생성 · 코드를 공유하세요');
        paintLobby();
        break;
      case 'joined':
        COOP.seat = 'guest';
        COOP.code = msg.code;
        COOP.seed = msg.seed;
        status('참가 성공');
        paintLobby();
        break;
      case 'room':
        COOP.room = msg;
        COOP.code = msg.code;
        COOP.seed = msg.seed;
        paintLobby();
        break;
      case 'err':
        status(msg.m || '오류', true);
        toast && toast(msg.m || '오류');
        break;
      case 'start':
        beginMission(msg);
        break;
      case 'state':
        applyPeerState(msg);
        break;
      case 'dig':
      case 'hp':
      case 'break':
        applyDig(msg);
        break;
      case 'lamp':
        applyLamp(msg);
        break;
      case 'res':
        COOP.sharedRes = msg.n | 0;
        if (typeof CREW !== 'undefined' && CREW.phase === 'play') {
          CREW.goalHave = Math.max(CREW.goalHave, COOP.sharedRes);
          if (CREW.goalHave >= CREW.goalNeed) CREW.canEscape = true;
          if (typeof paintCrewHud === 'function') paintCrewHud();
        }
        break;
      case 'end':
        if (typeof CREW !== 'undefined' && CREW.phase === 'play' && !COOP._ending) {
          COOP._ending = true;
          endCrewMission(msg.kind || 'fail');
          COOP._ending = false;
        }
        break;
      case 'peer_left':
        status('상대가 나갔습니다', true);
        toast && toast('상대가 나갔습니다');
        break;
      default:
        break;
    }
  }

  function applyPeerState(msg) {
    if (!COOP.peer) {
      COOP.peer = {
        x: 0,
        y: 0,
        vx: 0,
        vy: 0,
        aim: 0,
        face: 1,
        drill: 0,
        weapon: 'drill',
        roleId: null,
        php: 100,
        moving: false,
        name: 'PEER',
      };
    }
    const p = COOP.peer;
    p.x = msg.x;
    p.y = msg.y;
    p.aim = msg.aim;
    p.face = msg.face;
    p.drill = msg.drill;
    p.weapon = msg.weapon;
    p.roleId = msg.roleId;
    p.php = msg.php;
    p.moving = !!msg.moving;
    p.name = msg.name || p.name;
  }

  function applyDig(msg) {
    if (typeof damage !== 'function' || typeof G === 'undefined') return;
    COOP._applying = true;
    try {
      const c = msg.c | 0,
        r = msg.r | 0;
      if (typeof inB === 'function' && !inB(c, r)) return;
      const k = typeof ci === 'function' ? ci(c, r) : null;
      if (msg.t === 'break' || msg.hp === 0) {
        if (k != null && G.cell[k]) {
          G.cell[k] = null;
          G.hp.delete(k);
          G.compDirty = true;
          if (typeof LOS !== 'undefined' && LOS.markDirty) LOS.markDirty();
          if (typeof J !== 'undefined' && J.burst && typeof cxw === 'function') {
            J.burst(cxw(c), cyw(r), 8, ['#E8C89A', '#FFF'], 120);
          }
        }
      } else if (msg.t === 'hp' || msg.t === 'dig') {
        if (k != null && G.cell[k] && !SOLIDX(G.cell[k])) {
          if (typeof msg.hp === 'number') G.hp.set(k, msg.hp);
          else if (msg.d) damage(c, r, msg.d, msg.hx || 0, msg.hy || 0, true);
        }
      }
    } finally {
      COOP._applying = false;
    }
  }

  function applyLamp(msg) {
    if (!G || !G.lamps) return;
    G.lamps.push({
      c: 0,
      r: 0,
      x: msg.x,
      y: msg.y,
      ph: Math.random() * 6,
      rad: msg.rad || DEMO.lampRadius * 1.25,
      ttl: msg.ttl || 18,
      flare: 1,
    });
    if (typeof LOS !== 'undefined' && LOS.markDirty) LOS.markDirty();
  }

  function beginMission(msg) {
    COOP._started = true;
    COOP.active = true;
    COOP.sharedRes = 0;
    COOP.peer = null;
    showLobby(false);

    if (typeof DEMO !== 'undefined' && msg.seed) {
      DEMO.seed = msg.seed;
      COOP.seed = msg.seed;
    }
    // Softer for first LAN test
    if (typeof DEMO !== 'undefined') DEMO.enemyOn = false;

    const myRole = COOP.seat === 'host' ? msg.hostRole : msg.guestRole;
    const peerRole = COOP.seat === 'host' ? msg.guestRole : msg.hostRole;
    COOP.myRole = myRole;

    // Align dungeon seed before mission gen
    if (typeof applyDemoToDungen === 'function') applyDemoToDungen();

    if (typeof startCrewMission === 'function') {
      startCrewMission(myRole);
    }

    // Offset guest spawn so they don't overlap
    requestAnimationFrame(() => {
      if (COOP.seat === 'guest' && G && G.entry) {
        G.sh.x = G.entry.x + (typeof CELL !== 'undefined' ? CELL * 0.9 : 40);
        G.sh.y = G.entry.y;
        if (typeof collide === 'function') collide(G.sh, R_SHELLY);
      }
      if (COOP.peer == null) {
        COOP.peer = {
          x: G.entry.x,
          y: G.entry.y,
          aim: 0,
          face: 1,
          drill: 0,
          weapon: 'drill',
          roleId: peerRole,
          php: DEMO.playerHp || 100,
          moving: false,
          name: peerRole,
        };
      }
      toast && toast('코옵 시작 · 나: ' + myRole + ' / 상대: ' + peerRole);
    });
  }

  /* ── hooks into game ── */
  function installHooks() {
    // Wrap damage to sync
    if (typeof window.damage === 'function' || typeof damage === 'function') {
      const orig = damage;
      window.damage = function (c, r, d, hx, hy, quiet) {
        const before = G.cell[ci(c, r)];
        const k = ci(c, r);
        const hpBefore = G.hp.has(k) ? G.hp.get(k) : before && HPT[before];
        const ok = orig(c, r, d, hx, hy, quiet);
        if (COOP.active && !COOP._applying && before && !SOLIDX(before)) {
          const alive = G.cell[k];
          if (!alive) send({ t: 'break', c, r, hp: 0 });
          else if (G.hp.has(k) && G.hp.get(k) !== hpBefore)
            send({ t: 'hp', c, r, hp: G.hp.get(k) });
        }
        return ok;
      };
      // reassign in scope if damage is const-like — game uses function damage so global works if not const
    }

    // Patch via prototype on global - the game defines function damage() in script scope, NOT on window.
    // So we need to patch from inside the HTML. We'll inject wrapper in HTML instead.
  }

  // Called from patched HTML
  window.COOP_onDamage = function (c, r, hpOrBreak) {
    if (!COOP.active || COOP._applying) return;
    if (hpOrBreak === 0 || hpOrBreak === 'break') send({ t: 'break', c, r, hp: 0 });
    else send({ t: 'hp', c, r, hp: hpOrBreak });
  };

  window.COOP_onLoot = function () {
    if (!COOP.active) return;
    const n = (G.nRes | 0) + (COOP.sharedRes | 0); // local count; server takes max
    send({ t: 'res', n: G.nRes | 0 });
  };

  window.COOP_onLamp = function (x, y, rad, ttl) {
    if (!COOP.active || COOP._applying) return;
    send({ t: 'lamp', x, y, rad, ttl });
  };

  window.COOP_tick = function (dt) {
    if (!COOP.active || !COOP._started) return;
    COOP._acc += dt;
    if (COOP._acc >= 1 / 20) {
      COOP._acc = 0;
      if (G && G.sh) {
        send({
          t: 'state',
          x: G.sh.x,
          y: G.sh.y,
          aim: G.sh.aim,
          face: G.sh.face,
          drill: G.sh.drill | 0,
          weapon: G.weapon,
          roleId: COOP.myRole,
          php: G.php,
          moving: Math.hypot(G.sh.vx || 0, G.sh.vy || 0) > 1,
          name: COOP.myRole,
        });
      }
    }
    // Shared goal from picks
    if (typeof CREW !== 'undefined' && CREW.phase === 'play') {
      const local = G.nRes | 0;
      if (local > COOP.sharedRes) send({ t: 'res', n: local });
      CREW.goalHave = Math.max(local, COOP.sharedRes | 0);
      if (CREW.goalHave >= CREW.goalNeed) CREW.canEscape = true;
    }
  };

  window.COOP_drawPeer = function () {
    if (!COOP.active || !COOP.peer) return;
    const p = COOP.peer;
    if (!isFinite(p.x) || !isFinite(p.y)) return;
    const drilling = p.weapon === 'drill' && !!p.drill;
    const aim = typeof p.aim === 'number' ? p.aim : p.face < 0 ? Math.PI : 0;
    const face = p.face || 1;
    const moving = !!p.moving;

    cx.save();
    // Nameplate
    cx.fillStyle = 'rgba(10,6,20,.65)';
    cx.strokeStyle = 'rgba(200,160,255,.7)';
    cx.lineWidth = 2;
    const label = (p.roleId || 'PEER').toUpperCase();
    cx.font = '800 11px Pretendard, Malgun Gothic, sans-serif';
    cx.textAlign = 'center';
    const tw = cx.measureText(label).width + 14;
    const bx = p.x - tw / 2,
      by = p.y - R_SHELLY - 28;
    cx.fillRect(bx, by, tw, 16);
    cx.strokeRect(bx, by, tw, 16);
    cx.fillStyle = '#e8d6ff';
    cx.fillText(label, p.x, by + 12);

    // Peer sprite must use PEER role, not local CREW.roleId
    // (drawShelly only enables miner sheets when *local* is driller)
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
  };

  window.COOP_endBroadcast = function (kind) {
    if (!COOP.active || COOP._ending) return;
    COOP._ending = true;
    send({ t: 'end', kind });
    setTimeout(() => {
      COOP._ending = false;
    }, 500);
  };

  /* LOS: also reveal from peer */
  window.COOP_peerXY = function () {
    return COOP.active && COOP.peer ? [COOP.peer.x, COOP.peer.y] : null;
  };

  function buildUI() {
    if ($('coopLobby')) return;
    const css = document.createElement('style');
    css.textContent = `
#coopLobby{position:absolute;inset:0;z-index:60;display:none;align-items:center;justify-content:center;
  background:rgba(6,4,14,.88);backdrop-filter:blur(6px);padding:20px;pointer-events:auto}
#coopLobby.on{display:flex}
#coopCard{width:min(520px,94vw);background:#1a1228;border:1px solid #4a3568;border-radius:16px;
  padding:22px 20px 18px;color:#e4d4f5;font:13px/1.5 Pretendard,Malgun Gothic,sans-serif}
#coopCard h1{margin:0 0 6px;font:800 22px/1.2 Pretendard,sans-serif;color:#edd8ff;text-align:center}
#coopCard .sub{text-align:center;color:#9a86b5;margin-bottom:14px;font-size:12.5px}
#coopCard .row{display:flex;gap:8px;flex-wrap:wrap;justify-content:center;margin:10px 0}
#coopCard button,#coopCard .roleBtn{font:inherit;background:#2a1a42;color:#e4d4f5;border:1px solid #4a3568;
  border-radius:10px;padding:10px 14px;cursor:pointer}
#coopCard button.pri{background:rgba(180,120,255,.22);border-color:rgba(180,120,255,.5);color:#edd8ff;font-weight:700}
#coopCard button:disabled{opacity:.4;cursor:default}
#coopCard input{background:#0c0814;color:#e4d4f5;border:1px solid #3a2a55;border-radius:8px;
  padding:10px 12px;font:700 16px ui-monospace,Menlo,Consolas,monospace;letter-spacing:.12em;
  width:8em;text-align:center;text-transform:uppercase}
#coopCodeShow{font:800 28px/1 ui-monospace,Menlo,Consolas,monospace;letter-spacing:.2em;color:#d4a8ff;text-align:center;margin:8px 0}
#coopRoles{display:grid;grid-template-columns:1fr 1fr;gap:8px;margin-top:8px}
#coopRoles .roleBtn{text-align:left;padding:10px}
#coopRoles .roleBtn b{display:block;color:#d4a8ff;margin-bottom:2px}
#coopRoles .roleBtn span{font-size:11px;color:#9a86b5}
#coopRoles .roleBtn.picked{border-color:#a878e0;background:rgba(168,120,224,.18)}
#coopRoles .roleBtn.taken{opacity:.35}
#coopStatus{text-align:center;min-height:1.2em;margin-top:8px;font-size:12px;color:#9a86b5}
#coopMeta{display:flex;justify-content:space-between;gap:8px;font-size:11px;color:#9a86b5;margin-top:8px}
#crewRole.coop-hide{display:none!important}
`;
    document.head.appendChild(css);

    const div = document.createElement('div');
    div.id = 'coopLobby';
    div.innerHTML = `
      <div id="coopCard">
        <h1>땅굴 크루 · LAN 코옵</h1>
        <p class="sub">같은 Wi‑Fi에서 방 만들고 참가하세요</p>
        <div class="row">
          <button class="pri" id="coopHostBtn">방 만들기 (호스트)</button>
        </div>
        <div class="row">
          <input id="coopJoinCode" maxlength="4" placeholder="CODE" />
          <button id="coopJoinBtn">참가</button>
        </div>
        <div id="coopCodeShow">————</div>
        <p class="sub" id="coopPeer">대기 중…</p>
        <div id="coopMeta"><span>호스트 역할: <b id="coopHostRole">—</b></span>
          <span>게스트 역할: <b id="coopGuestRole">—</b></span></div>
        <div id="coopRoles">
          <button class="roleBtn" data-role="driller"><b>드릴러</b><span>채굴 · 돌파</span></button>
          <button class="roleBtn" data-role="scout"><b>스카웃</b><span>시야 · 플레어</span></button>
          <button class="roleBtn" data-role="engineer"><b>엔지니어</b><span>발판 · 터렛</span></button>
          <button class="roleBtn" data-role="gunner"><b>거너</b><span>사격 · 방어막</span></button>
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

  function boot() {
    // Prep UI/socket only — lobby opens from main menu (LAN 코옵)
    if (location.protocol === 'file:') return;
    buildUI();
    connect();
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
  else boot();
})();
