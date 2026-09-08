/**
 * 사내 코옵 클라이언트 v3 — 트랙 F (기획서 §19.1, §17.4-4)
 *
 * v1: tunnel-crew-loop-demo(채취 미션) 2인 전용.
 * v2: 2~4인 좌석(p1~p4) 모델과 본편 무한 모드(mode:'infinite') 지원.
 * v3 (사내 인트라넷):
 * - 닉네임(localStorage 저장)·방 이름·방 목록. 서버가 방 밖 접속자에게
 *   rooms 이벤트를 푸시하므로 목록은 실시간 갱신된다. 코드 참가는 폴백으로 유지.
 * - 방 나가기(leave)와 호스트 승계(promoted).
 * - 피어 위치 보간: state 는 30Hz 로 받고 렌더는 매 프레임 지수 보간으로
 *   따라가서 스텝 현상 없이 부드럽다. 순간이동(리스폰 등)은 거리 임계로 스냅.
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

  /* 프로토콜 버전 — server.mjs 의 PROTO_VER 와 함께 올린다.
     어제 열어둔 낡은 탭이 새 서버에 붙는 것을 방 생성/참가 시점에 차단. */
  const COOP_VER = 4;

  const COOP = {
    active: false,
    seat: null, // 'p1' ~ 'p4' (p1 = 호스트)
    code: null,
    seed: null,
    mode: 'harvest', // 'harvest' | 'infinite'
    ws: null,
    room: null,
    myRole: null,
    nick: '',
    roomsCache: [],
    peers: new Map(), // seat -> {x,y,tx,ty,aim,face,drill,weapon,roleId,php,moving,level,status,name}
    sharedRes: 0,
    _applying: false,
    _acc: 0,
    _started: false,
    _infLaunch: false, // infStartRun 이 코옵을 끄지 않게 하는 신호
    _localStatus: 'play', // 'play' | 'boarded' | 'down' | 'escaped'
  };
  window.COOP = COOP;

  try {
    COOP.nick = localStorage.getItem('tc_coop_nick') || '';
  } catch (e) {}

  /* 재접속용 플레이어 식별자 — 브라우저에 고정. 진행 중 런에서 연결이 끊기면
     서버가 이 pid 의 좌석을 60초 보존하고, 같은 pid 의 resume 을 기다린다. */
  try {
    COOP.pid = localStorage.getItem('tc_coop_pid');
    if (!COOP.pid) {
      COOP.pid = 'pid-' + Math.random().toString(36).slice(2) + Date.now().toString(36);
      localStorage.setItem('tc_coop_pid', COOP.pid);
    }
  } catch (e) {
    COOP.pid = 'pid-' + Math.random().toString(36).slice(2);
  }

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
        x: NaN, y: NaN, tx: NaN, ty: NaN, aim: 0, face: 1, drill: 0, weapon: 'drill',
        roleId: null, php: 100, moving: false, level: 1, status: 'play', name: '',
      });
    }
    return COOP.peers.get(seat);
  }

  function myNick() {
    const el = $('coopNick');
    const v = ((el && el.value) || COOP.nick || '').trim().slice(0, 12);
    if (v && v !== COOP.nick) {
      COOP.nick = v;
      try {
        localStorage.setItem('tc_coop_nick', v);
      } catch (e) {}
    }
    return COOP.nick;
  }

  const ROLE_KO = { driller: '드릴러', gunner: '거너', scout: '스카우트', engineer: '엔지니어' };
  const esc = (s) => String(s || '').replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

  /* ── 로비 페인트 ── */

  function paintLobby() {
    const inRoom = !!COOP.code;
    const browse = $('coopBrowse');
    const roomEl = $('coopRoomPane');
    if (browse) browse.style.display = inRoom ? 'none' : 'block';
    if (roomEl) roomEl.style.display = inRoom ? 'block' : 'none';
    if (!inRoom) {
      paintRooms();
      return;
    }
    const r = COOP.room;
    const codeEl = $('coopCodeShow');
    const nameEl = $('coopRoomTitle');
    const peerEl = $('coopPeer');
    const startBtn = $('coopStart');
    const list = $('coopPlayers');
    if (codeEl) codeEl.textContent = COOP.code || '————';
    if (nameEl) nameEl.textContent = (r && r.name) || '';
    if (peerEl) {
      if (!r) peerEl.textContent = '대기 중…';
      else peerEl.textContent = '접속 ' + r.players.length + '/4 · 모드: ' + (r.mode === 'infinite' ? '무한 모드 (본편)' : '채취 미션');
    }
    if (list) {
      list.innerHTML = (r ? r.players : [])
        .map((p) => {
          const me = p.seat === COOP.seat ? ' (나)' : '';
          const host = p.seat === 'p1' ? '👑 ' : '';
          const who = p.name ? esc(p.name) : p.seat.toUpperCase();
          return '<span class="coopSeat' + (p.role ? ' roled' : '') + '">' + host + who + me + ' · ' + (p.role ? ROLE_KO[p.role] || p.role : '역할 미선택') + '</span>';
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

  function paintRooms() {
    const el = $('coopRoomList');
    if (!el) return;
    const list = COOP.roomsCache || [];
    if (!list.length) {
      el.innerHTML = '<p class="coopEmpty">아직 열린 방이 없습니다 — 첫 방을 만들어보세요!</p>';
      return;
    }
    el.innerHTML = list
      .map((r) => {
        const full = r.n >= r.max;
        const dead = r.started || full;
        const tag = r.started ? '진행 중' : full ? '만석' : '참가';
        return (
          '<div class="coopRoomRow' + (dead ? ' dead' : '') + '">' +
          '<span class="rn">' + (r.lock ? '🔒 ' : '') + esc(r.name) + '</span>' +
          '<span class="rh">👑 ' + esc(r.host) + '</span>' +
          '<span class="rm">' + (r.mode === 'infinite' ? '무한 모드' : '채취 미션') + '</span>' +
          '<span class="rp">' + r.n + '/' + r.max + '</span>' +
          '<button data-code="' + r.code + '" data-lock="' + (r.lock ? 1 : 0) + '"' + (dead ? ' disabled' : '') + '>' + tag + '</button>' +
          '</div>'
        );
      })
      .join('');
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
      toast && toast('코옵은 서버로 열어야 합니다 (cd claude-work/coop && npm start)');
      return;
    }
    buildUI();
    if (!COOP.ws || COOP.ws.readyState > 1) connect();
    showLobby(true);
    paintLobby();
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
    ws.onopen = () => {
      status('서버 연결됨');
      /* 진행 중 런에서의 순단 복구 — 좌석 되찾기 */
      if (COOP._reconT && COOP._started) send({ t: 'resume', pid: COOP.pid });
    };
    ws.onclose = () => {
      if (COOP.active && COOP._started) scheduleReconnect();
      else status('연결 끊김 — 코옵 서버를 확인하세요', true);
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

  /* ── 자동 재접속 — 네트워크 순단·절전 복귀 (탭이 살아있는 경우) ──
     탭이 새로고침/종료되면 런 상태가 사라지므로 재접속하지 않는다.
     60초(서버 유예와 동일) 안에 복구되면 좌석·역할 그대로 복귀한다. */
  function scheduleReconnect() {
    if (COOP._reconT) return;
    COOP._reconUntil = performance.now() + 62000;
    status('연결 끊김 — 재접속 시도 중…', true);
    toast && toast('서버 연결 끊김 — 자동 재접속 시도 중');
    COOP._reconT = setInterval(() => {
      if (performance.now() > COOP._reconUntil) {
        stopReconnect(false);
        return;
      }
      if (COOP.ws && COOP.ws.readyState <= 1) return; // 연결 중이거나 이미 연결됨
      connect();
    }, 2000);
  }

  function stopReconnect(ok) {
    if (COOP._reconT) {
      clearInterval(COOP._reconT);
      COOP._reconT = null;
    }
    if (ok) return;
    /* 복구 실패 — 방은 만료됐다. 런은 내 화면 기준(솔로)으로 계속한다. */
    COOP.peers.clear();
    if (COOP.CBT && COOP.CBT.auth === 'guest') cbtHostLost();
    toast && toast('재접속 실패 — 이번 런은 솔로로 이어집니다');
  }

  function leaveToBrowse() {
    send({ t: 'leave' });
    COOP.seat = null;
    COOP.code = null;
    COOP.room = null;
    COOP.active = false;
    COOP._started = false;
    COOP.peers.clear();
    paintLobby();
    status('방에서 나왔습니다');
  }

  function onMsg(msg) {
    if (cbtOnMsg(msg)) return; // 전투 동기화 메시지 (트랙 F-2)
    switch (msg.t) {
      case 'hello':
        break;
      case 'rooms':
        COOP.roomsCache = msg.rooms || [];
        if (!COOP.code) paintRooms();
        break;
      case 'hosted':
        COOP.seat = msg.seat;
        COOP.code = msg.code;
        COOP.seed = msg.seed;
        status('방 생성됨 — 크루를 기다리세요 (목록에 자동 공개)');
        paintLobby();
        metaPull('room'); // 닉네임 확정 시점 — 서버 성장 데이터 동기화
        break;
      case 'joined':
        COOP.seat = msg.seat;
        COOP.code = msg.code;
        COOP.seed = msg.seed;
        COOP.mode = msg.mode || 'harvest';
        status('참가 완료 · 좌석 ' + msg.seat.toUpperCase());
        paintLobby();
        metaPull('room');
        break;
      case 'promoted':
        COOP.seat = msg.seat;
        toast && toast('호스트가 되었습니다 (👑 P1)');
        paintLobby();
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
      case 'pdown': { /* v7.7.2c — 크루 기절/부활 통지 */
        const pd = peerOf(msg.from);
        pd.status = msg.on ? 'down' : 'play';
        pd._seen = performance.now();
        toast && toast(msg.on
          ? (pd.name || msg.from.toUpperCase()) + ' 기절! 접근해 5초간 치료하세요'
          : (pd.name || msg.from.toUpperCase()) + ' 구조 완료');
        break;
      }
      case 'state': {
        const p = peerOf(msg.from);
        p._seen = performance.now(); // 응답성 판정 (탈출 게이트·적 타깃 제외용)
        /* 위치는 보간 목표(tx,ty)로만 받는다 — 실제 x,y 는 렌더 루프가 따라간다.
           속도(tvx,tvy)로 수신 사이 구간을 외삽해 체감 지연을 줄인다 */
        p.tx = msg.x;
        p.ty = msg.y;
        p.tvx = msg.vx || 0;
        p.tvy = msg.vy || 0;
        if (!isFinite(p.x)) {
          p.x = msg.x;
          p.y = msg.y;
        }
        Object.assign(p, {
          aim: msg.aim, face: msg.face, drill: msg.drill,
          weapon: msg.weapon, roleId: msg.roleId, php: msg.php, moving: msg.moving,
        });
        if (typeof msg.level === 'number') p.level = msg.level;
        if (msg.fromName) p.name = msg.fromName;
        p.rvp = msg.rvp || 0; /* v7.7.2c — 기절 피어의 구조 진행률 (렌더 전용) */
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
        const p = peerOf(msg.from);
        p.status = 'boarded';
        toast && toast((p.name || msg.from.toUpperCase()) + ' 탈출 포트 탑승');
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
        toast && toast((p.name || msg.from.toUpperCase()) + (msg.kind === 'escaped' ? ' 생환' : ' 쓰러짐'));
        if (msg.from === 'p1') cbtHostLost(); // 호스트 런 종료 — 전투 로컬 폴백
        break;
      }
      case 'afk': {
        const p = peerOf(msg.from);
        p.afk = !!msg.on;
        if (!p.afk) p._seen = performance.now();
        break;
      }
      case 'resumed': {
        stopReconnect(true);
        COOP.seat = msg.seat;
        COOP.code = msg.code;
        COOP.seed = msg.seed;
        COOP.mode = msg.mode || COOP.mode;
        if (COOP.room) COOP.room.players = msg.players || COOP.room.players;
        status('재접속 완료 · 좌석 ' + msg.seat.toUpperCase());
        toast && toast('재접속 완료 — 좌석 유지 (' + msg.seat.toUpperCase() + ')');
        break;
      }
      case 'resume_fail': {
        stopReconnect(false);
        break;
      }
      case 'peer_drop': {
        const p = peerOf(msg.seat);
        p.dropped = true;
        toast && toast((msg.name || msg.seat.toUpperCase()) + ' 연결 끊김 — 60초 대기');
        if (msg.seat === 'p1') cbtHostLost(); // 호스트 순단 — 일단 로컬 폴백
        break;
      }
      case 'peer_back': {
        const p = peerOf(msg.seat);
        p.dropped = false;
        p.afk = false;
        p._seen = performance.now();
        if (msg.name) p.name = msg.name;
        toast && toast((msg.name || msg.seat.toUpperCase()) + ' 재접속');
        /* 재접속 크루는 끊긴 동안의 지형 변화를 놓쳤다 — 호스트가 전체 지형을
           재전송해 드리프트를 치유한다 (다른 크루에게는 사실상 no-op) */
        if (cbtHost() && typeof G !== 'undefined' && G.cell) {
          const v = [];
          for (let k = 0; k < G.cell.length; k++) v.push([k, G.cell[k] || 0]);
          for (let o = 0; o < v.length; o += 2000) send({ t: 'cells', v: v.slice(o, o + 2000) });
        }
        /* 호스트가 돌아왔다 — 로컬 폴백을 걷고 다시 호스트 권위로 */
        if (msg.seat === 'p1' && COOP.CBT && COOP.CBT.auth === 'local' && COOP.seat !== 'p1') {
          COOP.CBT.auth = 'guest';
          G.enemies.length = 0;
          COOP.CBT.puppets.clear();
          INF.boss = null;
          INF.bossActive = false;
          if (G.eshots) G.eshots.length = 0;
          INF.bossShots = [];
          INF.bossShotQueue = [];
          toast && toast('전투를 호스트 기준으로 재동기화');
        }
        break;
      }
      case 'peer_left': {
        const p = COOP.peers.get(msg.seat);
        COOP.peers.delete(msg.seat);
        if (msg.seat === 'p1') cbtHostLost(); // 호스트 이탈 — 전투 로컬 폴백
        const who = msg.name || (p && p.name) || (msg.seat ? msg.seat.toUpperCase() : '상대');
        toast && toast(who + ' 퇴장');
        paintLobby();
        break;
      }
    }
  }

  /* ══════════ 성장 데이터 서버 동기화 (v3.1) ══════════
   *
   * localStorage 는 origin(접속 주소)별로 분리된다 — 서버 IP 가 바뀌거나
   * localhost/IP 를 오가면 노드 트리·보관 코어가 갈라진다. 코옵 서버가
   * 닉네임 키로 INF_META 를 보관하고, 저장 시각(savedAt)이 최신인 쪽을 쓴다.
   * 닉네임이 곧 계정이다 — 팀 내에서 닉네임을 겹치지 않게 쓸 것 (README).
   */
  const META_SYNC = { pushT: null, pulledFor: null };

  function metaKey() {
    const n = (COOP.nick || '').trim();
    return n && /^[\w가-힣.-]{1,24}$/u.test(n) ? n : null;
  }

  function metaReplace(remote) {
    for (const k of Object.keys(INF_META)) delete INF_META[k];
    Object.assign(INF_META, remote);
  }

  function metaPush() {
    const key = metaKey();
    if (!key || typeof INF_META === 'undefined') return;
    clearTimeout(META_SYNC.pushT);
    META_SYNC.pushT = setTimeout(() => {
      fetch('/meta/' + encodeURIComponent(key), {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(INF_META),
      }).catch(() => {});
    }, 2500);
  }

  function metaPull(reason) {
    const key = metaKey();
    if (!key || typeof INF_META === 'undefined') return;
    if (typeof INF !== 'undefined' && INF.active) return; // 런 중에는 건드리지 않는다
    if (META_SYNC.pulledFor === key && reason === 'boot') return;
    META_SYNC.pulledFor = key;
    fetch('/meta/' + encodeURIComponent(key), { cache: 'no-store' })
      .then((r) => (r.ok ? r.json() : null))
      .then((remote) => {
        if (!remote) { metaPush(); return; } // 서버에 없음 — 내 것 업로드
        const localAt = INF_META.savedAt || 0;
        const remoteAt = remote.savedAt || 0;
        if (remoteAt > localAt) {
          if (typeof INF !== 'undefined' && INF.active) return;
          metaReplace(remote);
          if (CBT.orig.infSaveMeta) CBT.orig.infSaveMeta();
          else if (typeof infSaveMeta === 'function') infSaveMeta();
          if (typeof infUpdateMenuRecord === 'function') infUpdateMenuRecord();
          toast && toast('서버에서 성장 데이터 불러옴 — ' + key + ' (코어 ' + (INF_META.bankedCores || 0) + ')');
        } else if (localAt > remoteAt) {
          metaPush();
        }
      })
      .catch(() => {});
  }

  /* ══════════ 전투 동기화 — 호스트 권위 (트랙 F-2) ══════════
   *
   * 무한 모드 코옵에서 적·보스는 호스트(p1)만 시뮬레이션한다.
   * - 호스트: 기존 코드 그대로. AI_TGT/AI_TGT_HURT 확장으로 원격 크루도
   *   타깃/피격 대상이 되고(피해는 phit 통지), 15Hz esnap 으로 적·투사체를,
   *   5Hz cells diff 로 지형 변화(보스 벽·장갑·돌진 붕괴)를 전파한다.
   * - 게스트: updateEnemies/spawnEnemy/infSpawnBoss 를 차단하고 퍼펫을 보간
   *   렌더한다. 자기 공격은 ehit 로 호스트에 위임, 처치 시 호스트가 ekill 로
   *   기여 좌석에 통지 → 경험치는 각자 로컬(§5.2).
   * - 호스트가 나가거나 먼저 끝나면 로컬 시뮬레이션으로 폴백한다.
   */
  const CBT = {
    auth: null, // 'host' | 'guest' | 'local'(폴백) | null(비활성)
    eidSeq: 0,
    snapAcc: 0,
    cellAcc: 0,
    drillAcc: 0,
    cellShadow: null,     // 호스트: 지형 diff 기준
    puppets: new Map(),   // 게스트: eid -> 퍼펫 (G.enemies 안의 객체)
    proxies: new Map(),   // 호스트: seat -> {x,y,vx,vy,down,seat} — AI_TGT 용
    drillHits: new Map(), // 게스트: eid -> 누적 드릴 피해 (ehit 폭주 방지)
    bwallBuf: [],         // 호스트: 이번 플러시에 전파할 보스 소환 벽 [k, type, hp] (v7.7.2b)
    orig: {},
  };
  COOP.CBT = CBT;

  function cbtActive() {
    return COOP.active && COOP._started && isInfinite() && !!CBT.auth;
  }
  function cbtHost() {
    return cbtActive() && CBT.auth === 'host';
  }
  function cbtGuest() {
    return cbtActive() && CBT.auth === 'guest';
  }
  function cbtPeerAlive(p) {
    return p && p.status === 'play' && isFinite(p.x) && !p.afk && !p.dropped &&
      performance.now() - (p._seen || 0) < 3000;
  }
  /* 탈출 게이트용 응답성 — 연결 끊김·자리 비움·10초 이상 무소식(탭 정지)이면
     "생존자 전원 탑승" 판정에서 제외한다. 돌아오면 자동 복귀. (교착 방지) */
  function peerGateActive(p) {
    return !p.afk && !p.dropped && performance.now() - (p._seen || 0) < 10000;
  }

  function cbtStart() {
    CBT.auth = COOP.seat === 'p1' ? 'host' : 'guest';
    CBT.eidSeq = 0;
    CBT.snapAcc = CBT.cellAcc = CBT.drillAcc = 0;
    CBT.puppets.clear();
    CBT.proxies.clear();
    CBT.drillHits.clear();
    CBT.bwallBuf = [];
    CBT.cellShadow = null; // 첫 호스트 틱에서 G.cell 로 초기화 (infInitFloor 이후)
  }

  /* 호스트 이탈/종료 — 남은 게스트는 자기 화면 기준 로컬 시뮬레이션으로 전환 */
  function cbtHostLost() {
    if (!cbtGuest()) return;
    CBT.auth = 'local';
    G.enemies = G.enemies.filter((e) => !e._puppet);
    CBT.puppets.clear();
    if (INF.boss && INF.boss._puppet) {
      INF.boss = null;
      INF.bossActive = false;
      INF.bossSpawned = false;
    }
    if (G.eshots) G.eshots.length = 0;
    INF.bossShots = [];
    INF.bossShotQueue = [];
    toast && toast('호스트 이탈 — 전투를 내 화면 기준으로 전환합니다');
  }

  function cbtOnFloorChange() {
    CBT.puppets.clear();
    CBT.drillHits.clear();
    if (CBT.auth === 'guest') G.enemies = G.enemies.filter((e) => !e._puppet);
    if (CBT.auth === 'host' && typeof G !== 'undefined' && G.cell) CBT.cellShadow = G.cell.slice();
    if (G.eshots) G.eshots.length = 0;
  }

  /* 보스탄 피해 공식 — infBossProjectileHit(11394) 사본 */
  function cbtBossShotDmg() {
    const spec = typeof bossSpec === 'function' ? bossSpec() : null;
    return spec
      ? Math.max(0, Math.round(spec.projectileDamage * bossSpecTierMul(INF.boss, 'dmgMul')))
      : Math.max(9, Math.round(DEMO.enemyDmg * (3.55 + Math.max(0, INF.depth - 1) * .35 * INF_GROWTH_SCALE)));
  }

  /* ── 게스트: 퍼펫 생성/틱 ── */

  function cbtMakePuppet(d) {
    const e = {
      _puppet: 1, _eid: d.i,
      x: d.x, y: d.y, tx: d.x, ty: d.y, vx: 0, vy: 0,
      hp: d.hp, hpMax: d.hm, threatHpMul: 1, speedMul: 1, damageMul: 1,
      apex: !!d.a, elite: d.el || 0, kbx: 0, kby: 0,
      jumpT: 0, jumpDur: 0, jumpCd: Infinity, jumpVx: 0, jumpVy: 0,
      kind: d.k || 'crawler', animT: Math.random() * 6.28,
      blinkCd: 2.2 + Math.random() * 3.6, blinkT: 0,
      cd: 0, hurt: 0, bob: Math.random() * 6.28, r: d.r,
      ranged: d.k === 'spitter', ai: 'chase', homeX: d.x, homeY: d.y,
      wanderX: d.x, wanderY: d.y, wanderT: 1, wanderIdle: false,
      faceA: d.fa || 0, scanDir: 1, strafe: 1,
      seePlayer: true, lostT: 0, sightCd: 0, lastSeenX: d.x, lastSeenY: d.y,
      atkState: null, atkT: 0, atkWindupTotal: 0, atkCd: 1, atkDirX: 1, atkDirY: 0,
    };
    if (d.b) {
      Object.assign(e, {
        boss: 1, bossTier: d.bt || 'apex', kind: 'broodBeast',
        armorCells: d.ac || [], armorStage: 0, facing: d.fc || -1,
        bossAnimKey: d.ak || 'idle', bossAnimT: 0, bossAnimRate: d.ar || 1,
        bossDashState: null, patternCd: 99, attackCd: 99,
      });
    }
    return e;
  }

  function cbtApplySnap(msg) {
    if (!cbtGuest()) return;
    /* 층이 다른 스냅샷은 버린다 — 하강 레이스 중 이전 층 적의 유령 생성 방지 */
    if (typeof msg.dep === 'number' && msg.dep !== INF.depth) return;
    const p1 = peerOf(msg.from);
    p1._seen = performance.now();
    const seen = new Set();
    for (const d of msg.en || []) {
      if (d.hp <= 0) continue;
      seen.add(d.i);
      let e = CBT.puppets.get(d.i);
      if (!e) {
        e = cbtMakePuppet(d);
        CBT.puppets.set(d.i, e);
        G.enemies.push(e);
        if (d.b) {
          INF.boss = e;
          INF.bossSpawned = true;
          INF.bossActive = true;
        }
      }
      e.tx = d.x; e.ty = d.y;
      e.tvx = d.vx || 0; e.tvy = d.vy || 0;
      if (!isFinite(e.x)) { e.x = d.x; e.y = d.y; }
      e.hp = d.hp; e.hpMax = d.hm; e.r = d.r;
      e.faceA = d.fa || 0;
      e.atkState = d.st || null; e.atkT = d.at || 0; e.atkWindupTotal = d.aw || 0;
      e.atkDirX = d.adx || 0; e.atkDirY = d.ady || 0;
      if (d.b) {
        e.facing = d.fc || -1;
        e.bossAnimKey = d.ak || 'idle';
        e.bossAnimRate = d.ar || 1;
        e.bossDashState = d.ds || null;
        e.bossDashDirX = d.ddx || 0; e.bossDashDirY = d.ddy || 0;
        e.bossDashT = d.dT || 0; e.bossDashLeadTotal = d.dl || 0;
        e.bossFireBreathT = d.fb || 0;
        if (d.ac) e.armorCells = d.ac;
      }
    }
    /* 스냅샷에 없는 퍼펫은 제거 — 사망 연출은 ekill 이 담당 */
    for (const [i, e] of [...CBT.puppets]) {
      if (seen.has(i)) continue;
      CBT.puppets.delete(i);
      G.enemies = G.enemies.filter((o) => o !== e);
      if (INF.boss === e) INF.boss = null;
    }
    /* 적 투사체(침)·보스탄 — 시각 재현 전용, 판정은 호스트 phit */
    if (G.eshots) {
      G.eshots.length = 0;
      for (const s of msg.es || []) G.eshots.push({ x: s.x, y: s.y, vx: s.vx, vy: s.vy, r: s.r || 6.5, t: 0, life: 9, dmg: 0 });
    }
    INF.bossShotQueue = (msg.bq || []).map((q) => ({ tx: q.tx, ty: q.ty, delay: q.d, flight: q.f, rad: q.r, visualId: q.v, power: q.p || 1 }));
    INF.bossShots = (msg.bs || []).map((s) => ({ sx: s.sx, sy: s.sy, tx: s.tx, ty: s.ty, t: s.t, flight: s.f, rad: s.r, visualId: s.v, power: s.p || 1 }));
    CBT.snapAt = performance.now(); // 적 외삽 한도 기준
  }

  /* 게스트의 updateEnemies 대체 — 보간·애니메이션·내 드릴 타격만 */
  function cbtPuppetTick(dt) {
    G.iframes = Math.max(0, G.iframes - dt);
    if (typeof INF !== 'undefined' && INF.active) INF.floorTime = (INF.floorTime || 0) + dt;
    const drillOn = G.weapon === 'drill' && G.mouse.down && G.sh.drill;
    const ca = Math.cos(G.sh.aim), sa = Math.sin(G.sh.aim);
    const tipX = drillOn ? G.sh.x + ca * DRILL_TIP : 0, tipY = drillOn ? G.sh.y + sa * DRILL_TIP : 0;
    const extrapolate = performance.now() - (CBT.snapAt || 0) < 250; // 외삽 한도
    for (const e of G.enemies) {
      if (!e._puppet) continue;
      if (isFinite(e.tx)) {
        if (extrapolate) { e.tx += (e.tvx || 0) * dt; e.ty += (e.tvy || 0) * dt; }
        const dx0 = e.tx - e.x, dy0 = e.ty - e.y;
        if (Math.hypot(dx0, dy0) > CELL * 8) { e.x = e.tx; e.y = e.ty; }
        else { const f = Math.min(1, dt * 12); e.x += dx0 * f; e.y += dy0 * f; }
      }
      e.hurt = Math.max(0, e.hurt - dt);
      e.bob += dt * 6;
      e.animT += dt;
      if (e.boss) e.bossAnimT = (e.bossAnimT || 0) + dt * Math.max(.05, e.bossAnimRate || 1);
      if (e.blinkT > 0) e.blinkT = Math.max(0, e.blinkT - dt);
      else if ((e.blinkCd -= dt) <= 0) { e.blinkT = .24; e.blinkCd = 2.4 + Math.random() * 4.2; }
      /* 내 드릴 타격 (updateEnemies 5633 사본) — 피해는 누적해 15Hz 로 위임 */
      if (drillOn && e.hp > 0 && Math.hypot(tipX - e.x, tipY - e.y) < e.r + 8) {
        const im = INF.active ? INF.drillMul * ((G.mouse.drillDown && G.mouse.gunDown) ? (INF.syncMul || 1) : 1) : 1;
        const dmg = shelDps() * DEMO.enemyDrillMul * DRILL_DMG() * drillWarmMul() * im * dt;
        const acc = CBT.drillHits.get(e._eid) || { d: 0, nx: ca, ny: sa };
        acc.d += dmg; acc.nx = ca; acc.ny = sa;
        CBT.drillHits.set(e._eid, acc);
        e.hurt = .18;
      }
    }
    /* 드릴 피해 플러시 */
    CBT.drillAcc += dt;
    if (CBT.drillAcc >= 1 / 15 && CBT.drillHits.size) {
      CBT.drillAcc = 0;
      for (const [i, acc] of CBT.drillHits) {
        if (acc.d > 0) send({ t: 'ehit', i, d: +acc.d.toFixed(2), nx: +acc.nx.toFixed(3), ny: +acc.ny.toFixed(3), src: 0 });
      }
      CBT.drillHits.clear();
    }
    /* 보스탄 시각 진행 (판정 없음) */
    for (const q of INF.bossShotQueue || []) q.delay -= dt;
    for (const s of INF.bossShots || []) s.t = Math.min(s.flight, s.t + dt);
  }

  /* 게스트의 updateEnemyShots 대체 — 이동·벽 충돌 연출만 */
  function cbtPuppetShots(dt) {
    if (!G.eshots) return;
    for (let i = G.eshots.length - 1; i >= 0; i--) {
      const s = G.eshots[i];
      s.t += dt; s.life -= dt; s.x += s.vx * dt; s.y += s.vy * dt;
      let dead = s.life <= 0 || s.x < 0 || s.y < 0 || s.x > WW || s.y > WH;
      if (!dead) {
        const c = Math.floor(s.x / CELL), r = Math.floor(s.y / CELL);
        if (!inB(c, r) || G.cell[ci(c, r)]) { dead = true; J.burst && J.burst(s.x, s.y, 5, ['#8BE38F', '#DFF7D0'], 90); }
      }
      if (dead) G.eshots.splice(i, 1);
    }
  }

  /* ── 호스트: 스냅샷·지형 diff·피어 피격 판정 ── */

  function cbtHostTick(dt) {
    /* 피어 프록시 — AI_TGT 가 원격 크루를 타깃으로 삼도록 */
    for (const [seat, p] of COOP.peers) {
      let pr = CBT.proxies.get(seat);
      if (!pr) { pr = { seat, x: 0, y: 0, vx: 0, vy: 0, down: true }; CBT.proxies.set(seat, pr); }
      pr.x = p.x; pr.y = p.y;
      pr.down = !cbtPeerAlive(p);
    }
    for (const seat of [...CBT.proxies.keys()]) if (!COOP.peers.has(seat)) CBT.proxies.delete(seat);

    /* 적 침(스피터 투사체) vs 원격 크루 — 호스트가 판정해 phit 통지 */
    if (G.eshots) {
      for (let i = G.eshots.length - 1; i >= 0; i--) {
        const s = G.eshots[i];
        for (const pr of CBT.proxies.values()) {
          if (pr.down) continue;
          if (Math.hypot(s.x - pr.x, s.y - pr.y) < R_SHELLY + (s.r || 6)) {
            const d = Math.hypot(s.vx, s.vy) || 1;
            send({ t: 'phit', seat: pr.seat, d: s.dmg, nx: s.vx / d, ny: s.vy / d });
            J.burst && J.burst(s.x, s.y, 8, ['#8BE38F', '#FFF'], 140);
            G.eshots.splice(i, 1);
            break;
          }
        }
      }
    }

    /* 적·투사체 스냅샷 15Hz */
    CBT.snapAcc += dt;
    if (CBT.snapAcc >= 1 / 15) {
      CBT.snapAcc = 0;
      const en = [];
      for (const e of G.enemies) {
        if (e.hp <= 0) continue; // 사체는 스냅샷 제외 (ekill 이 처리)
        if (!e._eid) e._eid = ++CBT.eidSeq;
        const d = {
          i: e._eid, x: Math.round(e.x), y: Math.round(e.y),
          vx: Math.round((e.vx || 0) + (e.kbx || 0)), vy: Math.round((e.vy || 0) + (e.kby || 0)),
          hp: Math.ceil(e.hp), hm: Math.ceil(e.hpMax), r: +e.r.toFixed(1),
          k: e.kind, fa: +(e.faceA || 0).toFixed(2),
        };
        if (e.apex) d.a = 1;
        if (e.elite) d.el = e.elite;
        if (e.atkState) { d.st = e.atkState; d.at = +(e.atkT || 0).toFixed(2); d.aw = +(e.atkWindupTotal || 0).toFixed(2); d.adx = +(e.atkDirX || 0).toFixed(2); d.ady = +(e.atkDirY || 0).toFixed(2); }
        if (e.boss) {
          d.b = 1; d.bt = e.bossTier; d.fc = e.facing || -1;
          d.ak = e.bossAnimKey || 'idle'; d.ar = +(e.bossAnimRate || 1).toFixed(2);
          if (e.bossDashState) { d.ds = e.bossDashState; d.ddx = +(e.bossDashDirX || 0).toFixed(2); d.ddy = +(e.bossDashDirY || 0).toFixed(2); d.dT = +(e.bossDashT || 0).toFixed(2); d.dl = +(e.bossDashLeadTotal || 0).toFixed(2); }
          if (e.bossFireBreathT > 0) d.fb = +e.bossFireBreathT.toFixed(2);
          if (e.armorCells && e.armorCells.length) d.ac = e.armorCells;
        }
        en.push(d);
      }
      const es = (G.eshots || []).map((s) => ({ x: Math.round(s.x), y: Math.round(s.y), vx: Math.round(s.vx), vy: Math.round(s.vy), r: s.r }));
      const bq = (INF.bossShotQueue || []).map((q) => ({ tx: Math.round(q.tx), ty: Math.round(q.ty), d: +q.delay.toFixed(2), f: q.flight, r: Math.round(q.rad), v: q.visualId, p: +(q.power || 1).toFixed(2) }));
      const bs = (INF.bossShots || []).map((s) => ({ sx: Math.round(s.sx), sy: Math.round(s.sy), tx: Math.round(s.tx), ty: Math.round(s.ty), t: +s.t.toFixed(2), f: s.flight, r: Math.round(s.rad), v: s.visualId, p: +(s.power || 1).toFixed(2) }));
      /* dep: 층 태그 — 하강 타이밍이 어긋난 동안 이전 층 적이 유령으로 생기는 것 방지 */
      send({ t: 'esnap', dep: INF.depth, en, es, bq, bs });
    }

    /* 지형 diff 5Hz — 보스 벽 소환·암반 장갑·돌진 붕괴를 전부 커버 */
    CBT.cellAcc += dt;
    if (CBT.cellAcc >= .2) {
      CBT.cellAcc = 0;
      if (!CBT.cellShadow) CBT.cellShadow = G.cell.slice();
      else {
        const v = [];
        const cell = G.cell, shadow = CBT.cellShadow;
        for (let k = 0; k < cell.length; k++) {
          if (shadow[k] !== cell[k]) { shadow[k] = cell[k]; v.push([k, cell[k] || 0]); }
        }
        for (let o = 0; o < v.length; o += 400) send({ t: 'cells', v: v.slice(o, o + 400) });
      }
      /* v7.7.2b — 보스 소환 벽 상세(경도·강조) 플러시: cells diff 뒤에 보내
         게스트에서 셀 타입 → 상세 순으로 적용되게 한다 */
      if (CBT.bwallBuf.length) {
        for (let o = 0; o < CBT.bwallBuf.length; o += 400) send({ t: 'bwall', v: CBT.bwallBuf.slice(o, o + 400) });
        CBT.bwallBuf = [];
      }
    }
  }

  /* ── 수신 핸들러 (onMsg 에서 위임) ── */

  function cbtOnMsg(msg) {
    switch (msg.t) {
      case 'esnap':
        cbtApplySnap(msg);
        return true;
      case 'ehit': {
        if (!cbtHost()) return true;
        const e = G.enemies.find((x) => x._eid === msg.i);
        if (!e || e.hp <= 0) return true;
        COOP._ehitFrom = msg.from;
        COOP._ehitSrc = msg.src || 0;
        try {
          hurtEnemy(e, msg.d, msg.nx, msg.ny, msg.src === 'turret' ? 'turret' : undefined);
        } finally {
          COOP._ehitFrom = null;
          COOP._ehitSrc = 0;
        }
        return true;
      }
      case 'ekill': {
        if (!cbtGuest()) return true;
        const e = CBT.puppets.get(msg.i);
        const x = e ? e.x : msg.x, y = e ? e.y : msg.y;
        if (e) {
          e.hp = 0;
          try {
            if (typeof spawnGoreBurst === 'function') spawnGoreBurst(e, 0, 0);
            if (typeof spawnGore === 'function') spawnGore(e);
          } catch (err) {}
          G.enemies = G.enemies.filter((o) => o !== e);
          CBT.puppets.delete(msg.i);
          if (INF.boss === e) INF.boss = null;
        }
        J.burst && J.burst(x, y, 16, ['#C7A0FF', '#FFE9C9', '#6FE3D3'], 220);
        J.ring && J.ring(x, y, '#C7A0FF', 8, CELL * 1.2, 3);
        if (SFX.ore) SFX.ore();
        if (msg.by === COOP.seat && CBT.orig.infAwardEnemyKillXp) {
          CBT.orig.infAwardEnemyKillXp(
            { x, y, apex: !!msg.apex, elite: msg.el || 0, boss: !!msg.boss },
            msg.src === 'turret' ? 'turret' : undefined
          );
        }
        return true;
      }
      case 'phit': {
        if (msg.seat === COOP.seat && COOP.active && COOP._started && typeof applyPlayerDamage === 'function') {
          applyPlayerDamage(msg.d, msg.nx || 0, msg.ny || 0);
        }
        return true;
      }
      case 'cells': {
        if (!cbtGuest()) return true;
        COOP._applying = true;
        try {
          let dirty = false;
          for (const [k, tp] of msg.v || []) {
            const cur = G.cell[k] || 0, want = tp || 0;
            if (cur === want) continue;
            if (want) {
              G.cell[k] = want;
              G.band[k] = bandOf(Math.floor(k / COLS));
              if (G.dec) G.dec[k] = 0;
              G.hp.delete(k);
            } else {
              G.cell[k] = null;
              G.hp.delete(k);
              if (G.dec) G.dec[k] = 0;
            }
            dirty = true;
          }
          if (dirty) {
            G.compDirty = true;
            if (typeof LOS !== 'undefined') LOS.markDirty();
          }
        } finally {
          COOP._applying = false;
        }
        return true;
      }
      case 'bwall': {
        /* v7.7.2b — 보스 소환 벽 상세: 셀 타입·5배 경도 HP·붉은 강조·연출 (cells diff 보완) */
        if (!cbtGuest()) return true;
        COOP._applying = true;
        try {
          const set = typeof INF !== 'undefined' ? (INF.bossWallCells = INF.bossWallCells || new Set()) : null;
          let n = 0;
          for (const [k, tp, hp] of msg.v || []) {
            if (tp) {
              /* 호스트가 확정한 보스 벽 — 타입까지 그대로 따른다 (dirt→stone 전환 포함, cells diff 유실 대비) */
              if (G.cell[k] !== tp) {
                G.cell[k] = tp;
                G.band[k] = bandOf(Math.floor(k / COLS));
                if (G.dec) G.dec[k] = 0;
              }
              if (hp > 0) G.hp.set(k, hp); else G.hp.delete(k);
              if (set) set.add(k);
              n++;
              /* 소환 먼지 — 본편과 동일하게 2~4초 체류 (샘플링해 폭주 방지) */
              if (typeof J !== 'undefined' && J.sm && n <= 40 && typeof cxw === 'function') {
                const px = cxw(k % COLS), py = cyw(Math.floor(k / COLS));
                for (let i = 0; i < 2; i++) {
                  const a = Math.random() * 6.283, s = 55 * (.3 + Math.random() * .8), dustLife = 2 + Math.random() * 2;
                  J.sm.push({ x: px + (Math.random() - .5) * CELL * .4, y: py + CELL * .15, vx: Math.cos(a) * s, vy: Math.sin(a) * s - 9, r: 9 + Math.random() * 13, col: G.cell[k] === 'rock' ? '#3A4462' : '#4A4034', alpha: .38, life: 1, grow: 7, decay: 1 / dustLife });
                }
              }
            }
          }
          if (n) {
            G.compDirty = true;
            if (typeof LOS !== 'undefined') LOS.markDirty();
            if (typeof J !== 'undefined' && J.kick) J.kick(Math.min(8.4, 4 + n * .4)); /* v7.7.2c — 소환 흔들림 2배 */
          }
        } finally {
          COOP._applying = false;
        }
        return true;
      }
      case 'bfx': {
        if (!cbtGuest()) return true;
        if (window.TUNNEL_PROJECTILE_FX) window.TUNNEL_PROJECTILE_FX.impact(msg.x, msg.y, 0, msg.v || 'bossScatter');
        else { J.ring && J.ring(msg.x, msg.y, '#FF557D', 10, (msg.rad || CELL) * 1.35, 4); J.burst && J.burst(msg.x, msg.y, 18, ['#FF557D', '#FFB0B8', '#3A0D1A'], 230); J.kick && J.kick(5); }
        return true;
      }
      case 'depth': {
        if (!cbtActive() || INF.depth >= msg.d) return true;
        COOP._applying = true;
        try {
          const modal = document.getElementById('infRestModal');
          if (modal) modal.classList.remove('on', 'highTier');
          let guard = 0;
          while (INF.depth < msg.d && guard++ < 5) {
            INF.restChosen = true;
            window.infNextDepth();
          }
        } finally {
          COOP._applying = false;
        }
        /* 동시 하강(double descend) 가드 — 이미 크루와 함께 내려왔으므로
           내 하강 입력은 소화된 것: restChosen 을 접어 원본 관문이 막게 하고,
           모달이 닫히기 직전의 클릭 레이스는 시간 가드(1.5초)가 커버한다 */
        INF.restChosen = false;
        COOP._remoteDescendAt = performance.now();
        toast && toast('크루와 함께 하강 — ' + (typeof infDepthLabel === 'function' ? infDepthLabel(INF.depth) : '심층 ' + INF.depth));
        return true;
      }
      case 'boss': {
        if (!cbtGuest()) return true;
        if (msg.ev === 'spawn') {
          INF.bossSpawned = true;
          toast && toast(msg.tier === 'guardian' ? '심층 수호자 출현 — 중심부의 전조' : msg.tier === 'apex' ? '중심부 보스 · 암반 포식자 침공' : '변종 포식자 침공');
          J.kick && J.kick(9);
        } else if (msg.ev === 'down') {
          const b = INF.boss;
          if (b && b._puppet) {
            G.enemies = G.enemies.filter((o) => o !== b);
            CBT.puppets.delete(b._eid);
          }
          INF.boss = null;
          INF.bossActive = true; // infBossDefeated 관문 통과용
          const fake = { x: msg.x || G.sh.x, y: msg.y || G.sh.y, bossTier: msg.tier || 'apex', hp: 0, hpMax: 1, r: 100, boss: 1 };
          COOP._applying = true; // 자동 탈출 요청의 중복 브로드캐스트 억제 (지점은 전원 동일)
          try {
            if (CBT.orig.infBossDefeated) CBT.orig.infBossDefeated(fake);
          } finally {
            COOP._applying = false;
          }
        }
        return true;
      }
    }
    return false;
  }

  /* infSaveMeta 래핑 — 저장 시각을 찍고 서버로 디바운스 업로드 */
  function metaInstall() {
    if (CBT.orig.infSaveMeta || typeof window.infSaveMeta !== 'function') return;
    CBT.orig.infSaveMeta = window.infSaveMeta;
    window.infSaveMeta = function () {
      if (typeof INF_META !== 'undefined') INF_META.savedAt = Date.now();
      const ok = CBT.orig.infSaveMeta();
      if (ok) metaPush();
      return ok;
    };
  }

  /* ── 몽키패치 설치 — boot 시 1회. 모든 분기는 런타임 플래그로 게이트 ── */

  function cbtInstall() {
    if (CBT.orig.updateEnemies || typeof window.updateEnemies !== 'function') return;
    const O = CBT.orig;
    ['updateEnemies', 'updateEnemyShots', 'spawnEnemy', 'hurtEnemy', 'infSpawnBoss',
     'infBossDefeated', 'infAwardEnemyKillXp', 'infBossProjectileHit', 'infNextDepth',
     'AI_TGT', 'AI_TGT_HURT',
     'infBossWallWave', 'infBossWallPrison', 'infBossWallMaterialize', 'infBossStartPattern',
    ].forEach((n) => { O[n] = window[n]; });

    /* ── 보스 패턴 타깃 — 모든 크루가 대상 ──
       원본(석화 감옥·지각 파동·원거리 포격·돌진 조준)은 G.sh 를 하드코딩한다.
       호출은 전부 동기이고 타깃 좌표를 호출 시점에 읽으므로, 호출 동안만
       G.sh 를 추첨된 크루(프록시 {x,y,vx,vy})로 바꿔치기하면 안전하다. */
    function cbtWithCrewTarget(fn, args) {
      const alive = [G.sh];
      for (const pr of CBT.proxies.values()) if (!pr.down) alive.push(pr);
      const t = alive[(Math.random() * alive.length) | 0];
      if (t === G.sh) return fn.apply(null, args);
      const real = G.sh;
      G.sh = t;
      try {
        return fn.apply(null, args);
      } finally {
        G.sh = real;
      }
    }
    window.infBossWallWave = function (e) {
      if (!cbtHost()) return O.infBossWallWave(e);
      return cbtWithCrewTarget(O.infBossWallWave, [e]);
    };
    window.infBossWallPrison = function (e) {
      if (!cbtHost()) return O.infBossWallPrison(e);
      return cbtWithCrewTarget(O.infBossWallPrison, [e]);
    };
    window.infBossStartPattern = function (e, forcedType) {
      if (!cbtHost()) return O.infBossStartPattern(e, forcedType);
      return cbtWithCrewTarget(O.infBossStartPattern, [e, forcedType]);
    };
    /* 벽 소환의 "플레이어를 가두지 않음" 보호(원본은 G.sh 만)를 원격 크루로 확장 */
    window.infBossWallMaterialize = function (w) {
      if (cbtHost()) {
        const px = cxw(w.c), py = cyw(w.r);
        for (const pr of CBT.proxies.values()) {
          if (!pr.down && Math.hypot(pr.x - px, pr.y - py) < CELL * 1.05) return;
        }
        /* v7.7.2b — 보스 소환 벽 전용 동기화: 경도(5배 HP)·붉은 강조·연출은
           cells diff 로 전달되지 않으므로, 실제로 생성/전환된 칸을 bwall 로 전파한다 */
        const k = typeof ci === 'function' ? ci(w.c, w.r) : null;
        const set = typeof INF !== 'undefined' && INF.bossWallCells;
        const had = !!(set && k != null && set.has(k));
        const r = O.infBossWallMaterialize(w);
        if (set && k != null && set.has(k) && !had) {
          CBT.bwallBuf.push([k, G.cell[k] || 0, G.hp.has(k) ? G.hp.get(k) : 0]);
        }
        return r;
      }
      return O.infBossWallMaterialize(w);
    };

    window.updateEnemies = function (dt) {
      if (cbtGuest()) { cbtPuppetTick(dt); return; }
      O.updateEnemies(dt);
    };
    window.updateEnemyShots = function (dt) {
      if (cbtGuest()) { cbtPuppetShots(dt); return; }
      O.updateEnemyShots(dt);
    };
    window.spawnEnemy = function (forceApex) {
      if (cbtGuest()) return;
      O.spawnEnemy(forceApex);
    };
    window.hurtEnemy = function (e, dmg, nx, ny, src) {
      if (cbtGuest()) {
        if (!e || e.hp <= 0 || !e._eid) return;
        if (typeof infRelicOnHit === 'function') dmg = infRelicOnHit(e, dmg, src);
        send({ t: 'ehit', i: e._eid, d: +(+dmg).toFixed(2), nx: +(nx || 0).toFixed(3), ny: +(ny || 0).toFixed(3), src: src === 'turret' ? 'turret' : 0 });
        e.hurt = .18;
        J.dmg && J.dmg(e.x, e.y - e.r, dmg, dmg > 20);
        return;
      }
      if (cbtHost() && e) e._hitSeat = COOP._ehitFrom || null;
      O.hurtEnemy(e, dmg, nx, ny, src);
      if (cbtHost() && e && e.hp <= 0 && !e._killSent && e._eid) {
        e._killSent = 1;
        send({ t: 'ekill', i: e._eid, x: Math.round(e.x), y: Math.round(e.y), by: e._hitSeat || COOP.seat, apex: e.apex ? 1 : 0, el: e.elite ? 1 : 0, boss: e.boss ? 1 : 0, src: COOP._ehitSrc || 0 });
      }
    };
    window.infAwardEnemyKillXp = function (e, src) {
      if (cbtHost() && e && e._hitSeat) return; // 게스트 기여 — ekill 로 그쪽에서 획득
      O.infAwardEnemyKillXp(e, src);
    };
    window.infSpawnBoss = function () {
      if (cbtGuest()) return; // 보스는 스냅샷으로 도착
      O.infSpawnBoss();
      if (cbtHost() && INF.boss) send({ t: 'boss', ev: 'spawn', tier: INF.boss.bossTier });
    };
    window.infBossDefeated = function (e) {
      const tier = e && e.bossTier, bx = e && e.x, by = e && e.y;
      O.infBossDefeated(e);
      if (cbtHost()) send({ t: 'boss', ev: 'down', tier, x: Math.round(bx || 0), y: Math.round(by || 0) });
    };
    window.infBossProjectileHit = function (s) {
      O.infBossProjectileHit(s);
      if (!cbtHost()) return;
      send({ t: 'bfx', x: Math.round(s.tx), y: Math.round(s.ty), rad: Math.round(s.rad), v: s.visualId || 'bossScatter' });
      const dmg = Math.round(cbtBossShotDmg() * (s.power || 1)); /* v7.7.2b — 기 모으기 배율을 원격 크루 피해에도 적용 */
      for (const [seat, p] of COOP.peers) {
        if (!cbtPeerAlive(p)) continue;
        if (Math.hypot(p.x - s.tx, p.y - s.ty) <= s.rad) send({ t: 'phit', seat, d: dmg, nx: 0, ny: 0 });
      }
    };
    window.infNextDepth = function () {
      /* 동시 하강 가드 — 크루의 하강을 방금 따라 내려왔다면(1.5초) 내 클릭은
         이미 소화된 것이다. 무시하지 않으면 한 층을 건너뛴다 (double descend) */
      if (cbtActive() && !COOP._applying &&
          performance.now() - (COOP._remoteDescendAt || 0) < 1500) return;
      const willRun = INF.restChosen;
      O.infNextDepth();
      if (!willRun) return;
      cbtOnFloorChange();
      if (cbtActive() && !COOP._applying) send({ t: 'depth', d: INF.depth });
    };
    /* 원격 크루를 적 타깃 후보에 포함 — AI 크루의 AI_TGT 계약 그대로 확장 */
    window.AI_TGT = function (e) {
      const t0 = O.AI_TGT(e);
      if (!cbtHost() || !e) return t0;
      const now = G.rt != null ? G.rt : G.t || 0;
      e._cTgtCd = (e._cTgtCd || 0) - (now - (e._cTgtAt != null ? e._cTgtAt : now));
      e._cTgtAt = now;
      if (e._cTgtCd > 0 && e._coopTgt && !e._coopTgt.down) return e._coopTgt;
      e._cTgtCd = .6;
      let best = t0, bd = Math.hypot(t0.x - e.x, t0.y - e.y), bp = null;
      for (const pr of CBT.proxies.values()) {
        if (pr.down) continue;
        const d = Math.hypot(pr.x - e.x, pr.y - e.y);
        if (d < bd) { bd = d; best = pr; bp = pr; }
      }
      e._coopTgt = bp;
      return best;
    };
    window.AI_TGT_HURT = function (e, dmg, nx, ny) {
      const r = O.AI_TGT_HURT(e, dmg, nx, ny);
      if (r !== null) return r;
      if (cbtHost() && e && e._coopTgt && !e._coopTgt.down) {
        send({ t: 'phit', seat: e._coopTgt.seat, d: +(+dmg).toFixed(1), nx: +(nx || 0).toFixed(3), ny: +(ny || 0).toFixed(3) });
        return false; // 처리됨 (원격이라 즉사 여부는 그쪽 판정)
      }
      return null;
    };
  }

  /* ── 수신 이벤트 적용 (v1 방식 유지) ── */

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
        if (p.name) peer.name = p.name;
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
      cbtStart(); // 전투 호스트 권위 동기화 시작 (트랙 F-2)
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
    /* d(심층) — 서버가 같은 심층의 동시 요청을 중재한다 (먼저 온 요청 승리) */
    send({ t: 'escape', x, y, need, auto: !!auto, d: typeof INF !== 'undefined' ? INF.depth | 0 : 0 });
  };
  window.COOP_onBoard = function () {
    if (!COOP.active || !isInfinite()) return;
    COOP._localStatus = 'boarded';
    send({ t: 'board' });
  };
  /* v7.7.2c — 상호 부활: 내 기절/부활 상태를 크루에게 알린다.
     status 'down' 은 적 타깃·탈출 게이트·피어 렌더가 이미 알고 있는 상태라 그대로 재사용한다. */
  window.COOP_onDownState = function (on) {
    if (!COOP.active || !COOP._started) return;
    COOP._localStatus = on ? 'down' : 'play';
    send({ t: 'pdown', on: on ? 1 : 0 });
  };
  /* null = 솔로(코옵 아님) → 게임은 기존 솔로 판정을 쓴다.
     true/false = 코옵 판정: 나 포함 생존자 전원이 탑승했는가 (§8.4-4 — 다운된 크루는 제외) */
  window.COOP_escapeAllAboard = function () {
    if (!COOP.active || !COOP._started || !isInfinite()) return null;
    if (COOP._localStatus !== 'boarded') return false;
    for (const p of COOP.peers.values()) {
      /* 무응답(탭 정지 등)·자리 비움 크루는 게이트에서 제외 — 교착 방지 */
      if (p.status === 'play' && peerGateActive(p)) return false;
    }
    return true;
  };
  window.COOP_escapeCount = function () {
    if (!COOP.active || !isInfinite()) return null;
    let boarded = COOP._localStatus === 'boarded' ? 1 : 0,
      total = 1;
    for (const p of COOP.peers.values()) {
      if (p.status === 'down' || p.status === 'escaped') continue;
      if (p.status === 'play' && !peerGateActive(p)) continue; // 무응답 제외
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

  /* ── 주기 송신 — 30Hz (사내 유선망 기준, 대역폭 여유 큼) ── */

  window.COOP_tick = function (dt) {
    if (!COOP.active || !COOP._started) return;
    COOP._acc += dt;
    if (COOP._acc >= 1 / 30) {
      COOP._acc = 0;
      if (G && G.sh) {
        send({
          t: 'state',
          x: G.sh.x, y: G.sh.y, aim: G.sh.aim, face: G.sh.face,
          vx: Math.round(G.sh.vx || 0), vy: Math.round(G.sh.vy || 0), /* 외삽용 속도 */
          drill: G.sh.drill | 0, weapon: G.weapon, roleId: COOP.myRole,
          php: G.php, moving: Math.hypot(G.sh.vx || 0, G.sh.vy || 0) > 1,
          level: typeof INF !== 'undefined' && INF.active ? INF.level : 1,
          rvp: G.downed ? +Math.min(1, (G.reviveT || 0) / 5).toFixed(2) : 0, /* v7.7.2c */
        });
      }
    }
    if (cbtHost()) cbtHostTick(dt); // 전투 스냅샷·지형 diff·피어 피격 판정
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
    /* 프레임 dt — 외삽용 (rAF 재개 직후 폭주 방지 위해 50ms 상한) */
    const nowMs = performance.now();
    const fdt = Math.min(.05, Math.max(0, (nowMs - (COOP._drawT || nowMs)) / 1000));
    COOP._drawT = nowMs;
    for (const [seat, p] of COOP.peers) {
      /* 위치 보간 — 30Hz 수신 + 속도 외삽(마지막 수신 후 0.25초 한도)을
         매 프레임 지수 추적. 큰 점프(리스폰 등)는 스냅 */
      if (isFinite(p.tx) && nowMs - (p._seen || 0) < 250) {
        p.tx += (p.tvx || 0) * fdt;
        p.ty += (p.tvy || 0) * fdt;
      }
      if (isFinite(p.tx)) {
        if (!isFinite(p.x) || Math.hypot(p.tx - p.x, p.ty - p.y) > 260) {
          p.x = p.tx;
          p.y = p.ty;
        } else {
          p.x += (p.tx - p.x) * 0.38;
          p.y += (p.ty - p.y) * 0.38;
        }
      }
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
      const roleTxt = (ROLE_KO[p.roleId] || p.roleId || seat).toUpperCase();
      const idle = p.status === 'play' && !peerGateActive(p);
      const label =
        (p.name ? p.name + ' · ' : '') + roleTxt +
        (isInfinite() ? ' Lv' + (p.level || 1) : '') +
        (p.status === 'boarded' ? ' · 탑승' : down ? ' · 다운' : p.dropped ? ' · 연결 끊김' : p.afk ? ' · 자리 비움' : idle ? ' · 무응답' : '');
      if (idle && !down) cx.globalAlpha = .55;
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
      /* v7.7.2c — 기절 피어: 붉은 링 + 구조 진행 게이지 (내가 옆에 서면 차오른다) */
      if (down) {
        const prog = Math.max(0, Math.min(1, p.rvp || 0));
        cx.globalAlpha = .55 + .2 * Math.sin(G.t * 5);
        cx.strokeStyle = '#FF6B7A'; cx.lineWidth = 3.2;
        cx.beginPath(); cx.arc(p.x, p.y, R_SHELLY + 9, 0, Math.PI * 2); cx.stroke();
        if (prog > 0) {
          cx.globalAlpha = .95; cx.strokeStyle = '#7FEBD0';
          cx.beginPath(); cx.arc(p.x, p.y, R_SHELLY + 9, -Math.PI / 2, -Math.PI / 2 + Math.PI * 2 * prog); cx.stroke();
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
#coopCard{width:min(600px,94vw);max-height:92vh;overflow-y:auto;background:#1a1228;border:1px solid #4a3568;border-radius:16px;
  padding:22px 20px 18px;color:#e4d4f5;font:13px/1.5 Pretendard,Malgun Gothic,sans-serif}
#coopCard h1{margin:0 0 6px;font:800 22px/1.2 Pretendard,sans-serif;color:#edd8ff;text-align:center}
#coopCard .sub{text-align:center;color:#9a86b5;margin-bottom:14px;font-size:12.5px}
#coopCard .row{display:flex;gap:8px;flex-wrap:wrap;justify-content:center;align-items:center;margin:10px 0}
#coopCard button,#coopCard .roleBtn{font:inherit;background:#2a1a42;color:#e4d4f5;border:1px solid #4a3568;
  border-radius:10px;padding:10px 14px;cursor:pointer}
#coopCard button.pri{background:rgba(180,120,255,.22);border-color:rgba(180,120,255,.5);color:#edd8ff;font-weight:700}
#coopCard button.picked{border-color:#a878e0;background:rgba(168,120,224,.18)}
#coopCard button:disabled{opacity:.4;cursor:default}
#coopCard input{background:#0c0814;color:#e4d4f5;border:1px solid #3a2a55;border-radius:8px;
  padding:10px 12px;font:600 14px Pretendard,Malgun Gothic,sans-serif}
#coopCard input#coopJoinCode{font:700 16px ui-monospace,Menlo,Consolas,monospace;letter-spacing:.12em;
  width:7em;text-align:center;text-transform:uppercase}
#coopCard label{font-size:12px;color:#9a86b5}
#coopCard .secTitle{display:flex;align-items:center;justify-content:space-between;gap:8px;
  margin:14px 0 6px;font:700 13px Pretendard,sans-serif;color:#c9b2e8}
#coopCard .secTitle button{padding:5px 10px;font-size:11px;border-radius:8px}
#coopRoomList{display:flex;flex-direction:column;gap:6px;max-height:200px;overflow-y:auto;
  border:1px solid #2e2145;border-radius:10px;padding:8px;background:rgba(12,8,20,.5)}
#coopRoomList .coopEmpty{margin:8px 0;text-align:center;color:#7d6a99;font-size:12px}
#coopRoomList .coopRoomRow{display:flex;align-items:center;gap:10px;padding:7px 10px;
  border:1px solid #3a2a55;border-radius:9px;background:#211636}
#coopRoomList .coopRoomRow.dead{opacity:.55}
#coopRoomList .rn{font-weight:700;color:#edd8ff;flex:1;min-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
#coopRoomList .rh,#coopRoomList .rm,#coopRoomList .rp{font-size:11px;color:#9a86b5;white-space:nowrap}
#coopRoomList button{padding:5px 12px;font-size:12px;border-radius:8px}
#coopCodeShow{font:800 28px/1 ui-monospace,Menlo,Consolas,monospace;letter-spacing:.2em;color:#d4a8ff;text-align:center;margin:8px 0}
#coopRoomTitle{text-align:center;font:700 15px Pretendard,sans-serif;color:#edd8ff;margin:2px 0}
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
        <h1>땅굴 크루 · 사내 코옵</h1>
        <p class="sub">회사 네트워크에서 방을 만들고 참가하세요 · 최대 4인</p>

        <div id="coopBrowse">
          <div class="row">
            <label for="coopNick">닉네임</label>
            <input id="coopNick" maxlength="12" placeholder="닉네임" style="width:10em" />
          </div>
          <div class="secTitle">방 목록<button id="coopRefresh">새로고침</button></div>
          <div id="coopRoomList"></div>
          <div class="row" style="margin-top:14px">
            <input id="coopRoomName" maxlength="20" placeholder="방 이름 (선택)" style="width:11em" />
            <input id="coopRoomPw" maxlength="16" placeholder="비밀번호 (선택)" style="width:8em" />
            <button class="pri" id="coopHostBtn">방 만들기</button>
          </div>
          <div class="row" style="opacity:.85">
            <input id="coopJoinCode" maxlength="4" placeholder="CODE" />
            <button id="coopJoinBtn">코드로 참가</button>
          </div>
          <div class="row">
            <button id="coopBackMenu">메인 메뉴</button>
          </div>
        </div>

        <div id="coopRoomPane" style="display:none">
          <div id="coopRoomTitle"></div>
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
            <button id="coopLeaveRoom">방 나가기</button>
          </div>
        </div>

        <div id="coopStatus"></div>
      </div>`;
    const app = $('app') || document.body;
    app.appendChild(div);

    const nickEl = $('coopNick');
    if (nickEl) nickEl.value = COOP.nick || '';

    $('coopHostBtn').onclick = () => {
      const seed =
        typeof DEMO !== 'undefined' && DEMO.seed
          ? DEMO.seed
          : 'tunnel-' + Math.floor(Math.random() * 1e9);
      send({ t: 'host', v: COOP_VER, seed, nick: myNick(), name: ($('coopRoomName').value || '').trim(), pw: ($('coopRoomPw').value || '').trim(), pid: COOP.pid });
    };
    $('coopJoinBtn').onclick = () => {
      const code = ($('coopJoinCode').value || '').trim().toUpperCase();
      if (code.length < 4) {
        status('방 코드 4자를 입력하세요', true);
        return;
      }
      send({ t: 'join', v: COOP_VER, code, nick: myNick(), pid: COOP.pid });
    };
    $('coopJoinCode').addEventListener('keydown', (e) => {
      if (e.key === 'Enter') $('coopJoinBtn').click();
    });
    $('coopRefresh').onclick = () => send({ t: 'list' });
    $('coopRoomList').addEventListener('click', (e) => {
      const btn = e.target.closest('button[data-code]');
      if (!btn || btn.disabled) return;
      let pw = '';
      if (btn.getAttribute('data-lock') === '1') {
        pw = (prompt('이 방은 비밀번호가 있습니다. 입력하세요:') || '').trim();
        if (!pw) return;
      }
      send({ t: 'join', v: COOP_VER, code: btn.getAttribute('data-code'), nick: myNick(), pw, pid: COOP.pid });
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
    $('coopLeaveRoom').onclick = () => leaveToBrowse();
    $('coopBackMenu').onclick = () => {
      if (COOP.code) leaveToBrowse();
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
    paintRooms();
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
    cbtInstall(); // 전투 동기화 몽키패치 (본편 함수가 있을 때만)
    metaInstall(); // 성장 데이터 서버 동기화
    connect();
    metaPull('boot');
  }

  /* 탭 백그라운드 통지 — rAF 정지로 얼어붙기 전에 크루에게 알린다.
     받은 쪽은 탈출 게이트·적 타깃에서 즉시 제외한다. (교착 방지) */
  document.addEventListener('visibilitychange', () => {
    if (!COOP.active || !COOP._started) return;
    send({ t: 'afk', on: document.hidden });
  });

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
  else boot();
})();
