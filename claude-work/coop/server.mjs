/**
 * 사내 코옵 서버 v3: static files + WebSocket lobby/sync.
 * From repo:  cd claude-work/coop && npm start
 *
 * v2 (트랙 F — 기획서 §19.1): 2인 host/guest 모델을 2~4인 좌석(p1~p4) 모델로
 * 확장하고, 방에 mode('harvest' 채취 미션 | 'infinite' 본편 무한 모드)를 더했다.
 * v3 (사내 인트라넷): 방 목록·방 이름·닉네임·방 나가기를 더했다.
 *   - 서버는 사내망의 아무 PC 한 대에서 돌리고, 전원이 브라우저로 접속한다.
 *   - "호스트가 된다" = 게임 안에서 방을 만든다(p1 좌석). 서버 실행과 무관.
 *   - 정적 루트는 저장소 루트다 — 본편 HTML·assets·demos·coop 을 그대로 서빙한다.
 *     주의: 이 폴더의 실체는 <루트>/coop 이고 claude-work/coop 은 정션(junction)이다.
 *     ESM __dirname 은 실제 경로로 풀리므로 루트는 '..' 한 단계다.
 * 서버는 여전히 권위 없는 릴레이다 — 게임 규칙 판정은 클라이언트가 한다.
 */
import http from 'http';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { networkInterfaces } from 'os';
import { WebSocketServer } from 'ws';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '..'); // 저장소 루트 (실제 위치: <루트>/coop)
const PORT = Number(process.env.PORT || process.argv[2] || 5188);
const MAX_SEATS = 4;
/* 프로토콜 버전 — client.js 의 COOP_VER 와 함께 올린다.
   오래 열어둔 어제 탭이 새 서버에 붙어 조용히 어긋나는 것을 막는다. */
const PROTO_VER = 4;
/* '/' 로 접속하면 여는 본편 파일. 새 버전이 나오면 여기만 바꾼다. */
const GAME_HTML = process.env.GAME || '/tunnel-crew-infinite-mode-v7.8.1.html';

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.svg': 'image/svg+xml',
  '.ico': 'image/x-icon',
  '.md': 'text/markdown; charset=utf-8',
  '.woff2': 'font/woff2',
  '.wav': 'audio/wav',
  '.mp3': 'audio/mpeg',
  '.ogg': 'audio/ogg',
  '.webm': 'audio/webm',
  '.webp': 'image/webp',
  '.gif': 'image/gif',
};

function lanIPs() {
  const out = [];
  const nets = networkInterfaces();
  for (const name of Object.keys(nets)) {
    for (const net of nets[name] || []) {
      if (net.family === 'IPv4' && !net.internal) out.push(net.address);
    }
  }
  return out;
}

function sendFile(res, filePath) {
  fs.readFile(filePath, (err, data) => {
    if (err) {
      res.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' });
      res.end('Not found');
      return;
    }
    const ext = path.extname(filePath).toLowerCase();
    res.writeHead(200, {
      'Content-Type': MIME[ext] || 'application/octet-stream',
      'Cache-Control': 'no-cache',
    });
    res.end(data);
  });
}

/* ── 성장 데이터 보관소 (v3.1) ──
 * localStorage 는 접속 주소(origin)별로 분리되므로, 서버 IP/주소가 바뀌면
 * 노드 트리·보관 코어가 갈라진다. 서버가 닉네임 키로 INF_META 를 보관해
 * 어떤 주소로 접속해도 같은 성장을 이어가게 한다. (신뢰 모델: 사내망)
 *   GET /meta/<key>  → JSON | 404
 *   PUT /meta/<key>  → 저장 (본문 JSON, 512KB 제한)
 */
const SAVE_DIR = path.join(__dirname, 'saves');
try { fs.mkdirSync(SAVE_DIR, { recursive: true }); } catch (e) {}
const META_KEY_RE = /^[\w가-힣.-]{1,24}$/u;

function handleMeta(req, res, key) {
  if (!META_KEY_RE.test(key)) {
    res.writeHead(400, { 'Content-Type': 'text/plain; charset=utf-8' });
    res.end('bad key');
    return;
  }
  const file = path.join(SAVE_DIR, key + '.json');
  if (req.method === 'GET') {
    fs.readFile(file, (err, data) => {
      if (err) { res.writeHead(404); res.end('no save'); return; }
      res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
      res.end(data);
    });
    return;
  }
  if (req.method === 'PUT') {
    let body = '';
    let over = false;
    req.on('data', (c) => {
      body += c;
      if (body.length > 512 * 1024) { over = true; req.destroy(); }
    });
    req.on('end', () => {
      if (over) return;
      try { JSON.parse(body); } catch (e) {
        res.writeHead(400); res.end('bad json'); return;
      }
      fs.writeFile(file, body, (err) => {
        if (err) { res.writeHead(500); res.end('write fail'); return; }
        res.writeHead(200, { 'Content-Type': 'application/json' });
        res.end('{"ok":true}');
      });
    });
    return;
  }
  res.writeHead(405);
  res.end();
}

const server = http.createServer((req, res) => {
  try {
    const u = new URL(req.url || '/', `http://${req.headers.host}`);
    let p = decodeURIComponent(u.pathname);
    if (p.startsWith('/meta/')) {
      handleMeta(req, res, p.slice('/meta/'.length));
      return;
    }
    if (p === '/') p = GAME_HTML;
    if (p.includes('..')) {
      res.writeHead(400);
      res.end('bad path');
      return;
    }
    const filePath = path.join(ROOT, p.replace(/^\//, ''));
    if (!filePath.startsWith(ROOT)) {
      res.writeHead(403);
      res.end('forbidden');
      return;
    }
    fs.stat(filePath, (err, st) => {
      if (err || !st.isFile()) {
        res.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' });
        res.end('Not found: ' + p);
        return;
      }
      sendFile(res, filePath);
    });
  } catch (e) {
    res.writeHead(500);
    res.end(String(e));
  }
});

/* ── rooms ─────────────────────────────────────────────────── */

const rooms = new Map();

function code4() {
  const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
  let s = '';
  for (let i = 0; i < 4; i++) s += chars[(Math.random() * chars.length) | 0];
  return s;
}

function send(ws, obj) {
  if (ws && ws.readyState === 1) ws.send(JSON.stringify(obj));
}

function cleanName(v, max) {
  return String(v || '').replace(/[<>]/g, '').trim().slice(0, max);
}

/* 좌석: p1(호스트) ~ p4. 나간 좌석 번호는 재사용한다.
   재접속 유예(ghost) 중인 좌석은 점유로 간주해 자리를 보전한다. */
function freeSeat(room) {
  const taken = new Set([...room.players.values()].map((p) => p.seat));
  if (room.ghosts) for (const g of room.ghosts.values()) taken.add(g.seat);
  for (let i = 1; i <= MAX_SEATS; i++) {
    const seat = 'p' + i;
    if (!taken.has(seat)) return seat;
  }
  return null;
}

function clearGhosts(room) {
  if (!room.ghosts) return;
  for (const g of room.ghosts.values()) clearTimeout(g.timer);
  room.ghosts.clear();
}

function maybeDeleteRoom(room) {
  if (room.players.size === 0 && (!room.ghosts || room.ghosts.size === 0)) {
    clearGhosts(room);
    rooms.delete(room.code);
    return true;
  }
  return false;
}

function roomState(room) {
  const players = [...room.players.values()]
    .map((p) => ({ seat: p.seat, role: p.role, ready: !!p.ready, name: p.name }))
    .sort((a, b) => (a.seat < b.seat ? -1 : 1));
  return {
    t: 'room',
    code: room.code,
    name: room.name,
    mode: room.mode,
    started: !!room.started,
    seed: room.seed,
    hostSeat: 'p1',
    players,
  };
}

function broadcast(room, msg, exceptWs) {
  for (const ws of room.players.keys()) if (ws !== exceptWs) send(ws, msg);
}

function broadcastRoom(room) {
  const msg = roomState(room);
  for (const ws of room.players.keys()) send(ws, msg);
}

/* ── 방 목록 (v3) — 방 밖의 모든 접속자에게 푸시한다 ── */

function roomsSummary() {
  return {
    t: 'rooms',
    rooms: [...rooms.values()].map((r) => {
      const host = [...r.players.values()].find((p) => p.seat === 'p1');
      return {
        code: r.code,
        name: r.name,
        mode: r.mode,
        started: !!r.started,
        n: r.players.size,
        max: MAX_SEATS,
        host: (host && host.name) || 'P1',
        lock: !!r.pw,
      };
    }),
  };
}

function broadcastRooms() {
  const msg = roomsSummary();
  for (const ws of wss.clients) {
    if (ws.readyState === 1 && !(ws._meta && ws._meta.room)) send(ws, msg);
  }
}

/* 방에서 빼기 — close 와 'leave' 가 공유한다.
   asGhost: 진행 중(started) 런에서 연결이 끊긴 경우 좌석을 60초 보존해
   같은 pid 의 'resume' 재접속을 기다린다. 명시적 '나가기'는 즉시 처리. */
const GHOST_GRACE_MS = 60000;

function leaveRoom(ws, asGhost) {
  const roomCode = ws._meta && ws._meta.room;
  if (!roomCode) return;
  const room = rooms.get(roomCode);
  ws._meta.room = null;
  ws._meta.seat = null;
  if (!room) return;
  const me = room.players.get(ws);
  room.players.delete(ws);
  if (!me) return;

  if (asGhost && room.started && me.pid) {
    room.ghosts = room.ghosts || new Map();
    /* 같은 pid 의 기존 ghost(같은 브라우저 두 탭 등)는 타이머를 정리하고 대체 —
       정리 없이 덮어쓰면 만료 타이머가 이중 발화해 peer_left 가 중복 방송된다 */
    const dup = room.ghosts.get(me.pid);
    if (dup) clearTimeout(dup.timer);
    const g = { seat: me.seat, role: me.role, name: me.name, pid: me.pid };
    g.timer = setTimeout(() => {
      room.ghosts.delete(g.pid);
      broadcast(room, { t: 'peer_left', seat: g.seat, name: g.name }, null);
      broadcastRoom(room);
      if (!maybeDeleteRoom(room)) broadcastRooms();
      else broadcastRooms();
    }, GHOST_GRACE_MS);
    room.ghosts.set(me.pid, g);
    broadcast(room, { t: 'peer_drop', seat: me.seat, name: me.name }, null);
    broadcastRoom(room);
    broadcastRooms();
    return;
  }

  broadcast(room, { t: 'peer_left', seat: me.seat, name: me.name }, null);
  if (maybeDeleteRoom(room)) {
    broadcastRooms();
    return;
  }
  if (room.players.size === 0) {
    /* 활성 0명 + ghost 만 남음 — 방은 유예 만료까지 유지 */
    broadcastRooms();
    return;
  }
  {
    /* 호스트(p1)가 나가면 남은 좌석 중 가장 빠른 사람이 p1 을 승계한다 */
    if (me && me.seat === 'p1' && !room.started) {
      const next = [...room.players.values()].sort((a, b) => (a.seat < b.seat ? -1 : 1))[0];
      if (next) {
        for (const [w, p] of room.players) {
          if (p === next) {
            p.seat = 'p1';
            w._meta.seat = 'p1';
            send(w, { t: 'promoted', seat: 'p1' });
            break;
          }
        }
      }
    }
    broadcastRoom(room);
  }
  broadcastRooms();
}

/* 게임 이벤트는 검사 없이 릴레이한다. 새 이벤트 타입은 여기만 추가하면 된다. */
const RELAY_TYPES = new Set([
  'state', 'dig', 'break', 'hp', 'loot', 'lamp', 'skill', 'end', 'ping',
  'chat',    /* 크루 채팅 {text} — chat/tc-chat.js (from/fromName 은 서버가 붙인다) */
  /* 트랙 F — 본편 무한 모드 */
  'escape',  /* 탈출 포트 요청/재현 {x,y,need,auto} */
  'board',   /* 탈출 포트 탑승 확정 */
  'level',   /* 개인 레벨업 알림 {level} — 명판 표시용, 성장은 개인(§5.2) */
  'boss',    /* 보스 이벤트 {ev:'spawn'|'down',tier} */
  /* 트랙 F-2 — 전투 호스트 권위 동기화 */
  'esnap',   /* 호스트→전원: 적·투사체 스냅샷 (15Hz) */
  'ehit',    /* 게스트→전원(호스트가 처리): 적 피해 {i,d,nx,ny,src} */
  'ekill',   /* 호스트→전원: 적 처치 {i,x,y,by,apex,el,boss,src} — by 좌석이 경험치 획득 */
  'phit',    /* 호스트→전원: 플레이어 피격 {seat,d,nx,ny} — seat 본인만 적용 */
  'cells',   /* 호스트→전원: 지형 diff {v:[[k,type|0],...]} — 보스 벽/장갑/붕괴 */
  'bwall',   /* 호스트→전원: 보스 소환 벽 상세 {v:[[k,type,hp],...]} — 5배 경도·붉은 강조 (v7.7.2b) */
  'pdown',   /* 전원: 크루 기절/부활 통지 {on:1|0} — 상호 부활 (v7.7.2c) */
  'bfx',     /* 호스트→전원: 보스탄 착탄 연출 {x,y,rad,v} */
  'depth',   /* 하강 동기화 {d} */
  'afk',     /* 탭 백그라운드 통지 {on} — 탈출 게이트·적 타깃에서 제외 */
]);

const wss = new WebSocketServer({ server });

wss.on('connection', (ws) => {
  ws._meta = { room: null, seat: null, name: '' };
  send(ws, { t: 'hello', v: 3, maxSeats: MAX_SEATS });
  send(ws, roomsSummary());

  ws.on('message', (raw) => {
    let msg;
    try {
      msg = JSON.parse(String(raw));
    } catch {
      return;
    }
    const type = msg.t;

    if (type === 'list') {
      send(ws, roomsSummary());
      return;
    }

    /* 재접속 — 진행 중 런에서 끊긴 좌석(ghost)을 같은 pid 로 되찾는다 */
    if (type === 'resume') {
      const pid = String(msg.pid || '');
      let found = null;
      if (pid) {
        for (const room of rooms.values()) {
          const g = room.ghosts && room.ghosts.get(pid);
          if (g) { found = { room, g }; break; }
        }
      }
      if (!found) {
        send(ws, { t: 'resume_fail' });
        send(ws, roomsSummary());
        return;
      }
      const { room, g } = found;
      clearTimeout(g.timer);
      room.ghosts.delete(pid);
      room.players.set(ws, { seat: g.seat, role: g.role, ready: true, name: g.name, pid });
      ws._meta = { room: room.code, seat: g.seat, name: g.name };
      send(ws, {
        t: 'resumed',
        code: room.code,
        seat: g.seat,
        seed: room.seed,
        mode: room.mode,
        players: roomState(room).players,
      });
      broadcast(room, { t: 'peer_back', seat: g.seat, name: g.name }, ws);
      broadcastRoom(room);
      broadcastRooms();
      return;
    }

    /* 버전 체크 — 방 생성/참가 시점에만 검사한다 (관전성 메시지는 무해) */
    if ((type === 'host' || type === 'join') && (msg.v | 0) !== PROTO_VER) {
      send(ws, { t: 'err', m: '클라이언트 버전이 다릅니다 — 페이지를 새로고침(F5) 하세요.' });
      return;
    }

    if (type === 'host') {
      if (ws._meta.room) leaveRoom(ws);
      let code = code4();
      while (rooms.has(code)) code = code4();
      const seed = msg.seed || 'tunnel-' + Math.floor(Math.random() * 1e9);
      const nick = cleanName(msg.nick, 12) || 'P1';
      const room = {
        code,
        seed,
        name: cleanName(msg.name, 20) || nick + '의 방',
        mode: msg.mode === 'harvest' ? 'harvest' : 'infinite',
        started: false,
        pw: String(msg.pw || '').slice(0, 16),
        players: new Map([[ws, { seat: 'p1', role: null, ready: false, name: nick, pid: String(msg.pid || '') }]]),
        ghosts: new Map(),
        escapes: new Map(), // depth -> {msg,at} — 탈출 포트 요청 경합 중재
        sharedRes: 0,
      };
      rooms.set(code, room);
      ws._meta = { room: code, seat: 'p1', name: nick };
      send(ws, { t: 'hosted', code, seed, seat: 'p1' });
      broadcastRoom(room);
      broadcastRooms();
      return;
    }

    if (type === 'join') {
      if (ws._meta.room) leaveRoom(ws);
      const code = String(msg.code || '').trim().toUpperCase();
      const room = rooms.get(code);
      if (!room) {
        send(ws, { t: 'err', m: '방을 찾을 수 없습니다.' });
        send(ws, roomsSummary());
        return;
      }
      if (room.started) {
        send(ws, { t: 'err', m: '이미 시작된 방입니다.' });
        return;
      }
      if (room.pw && String(msg.pw || '') !== room.pw) {
        send(ws, { t: 'err', m: '비밀번호가 다릅니다.' });
        return;
      }
      const seat = freeSeat(room);
      if (!seat) {
        send(ws, { t: 'err', m: '방이 가득 찼습니다. (최대 ' + MAX_SEATS + '인)' });
        return;
      }
      const nick = cleanName(msg.nick, 12) || seat.toUpperCase();
      room.players.set(ws, { seat, role: null, ready: false, name: nick, pid: String(msg.pid || '') });
      ws._meta = { room: code, seat, name: nick };
      send(ws, { t: 'joined', code, seed: room.seed, seat, mode: room.mode });
      broadcastRoom(room);
      broadcastRooms();
      return;
    }

    const roomCode = ws._meta.room;
    const room = roomCode && rooms.get(roomCode);
    if (!room) return;
    const me = room.players.get(ws);
    if (!me) return;
    const seat = me.seat;

    if (type === 'leave') {
      leaveRoom(ws);
      send(ws, roomsSummary());
      return;
    }

    if (type === 'role') {
      /* 같은 역할 중복 — §17.2-3 미결정. 결정 전까지는 자유 선택을 허용한다. */
      me.role = msg.role || null;
      me.ready = !!msg.ready;
      broadcastRoom(room);
      return;
    }

    if (type === 'mode') {
      if (seat !== 'p1' || room.started) return;
      room.mode = msg.mode === 'infinite' ? 'infinite' : 'harvest';
      broadcastRoom(room);
      broadcastRooms();
      return;
    }

    if (type === 'start') {
      if (seat !== 'p1') return;
      const players = [...room.players.values()];
      if (players.length < 2) {
        send(ws, { t: 'err', m: '참가자가 아직 없습니다.' });
        return;
      }
      if (players.some((p) => !p.role)) {
        send(ws, { t: 'err', m: '모두 역할을 선택해야 합니다.' });
        return;
      }
      room.started = true;
      room.sharedRes = 0;
      if (room.escapes) room.escapes.clear();
      const payload = {
        t: 'start',
        seed: room.seed,
        mode: room.mode,
        players: players.map((p) => ({ seat: p.seat, role: p.role, name: p.name })),
      };
      for (const w of room.players.keys()) send(w, payload);
      broadcastRooms();
      return;
    }

    if (type === 'res') {
      /* 채취 미션의 공유 자원 — 최대값 동기화 (기존 방식 유지) */
      if (typeof msg.n === 'number') {
        room.sharedRes = Math.max(room.sharedRes, msg.n | 0);
        const out = { t: 'res', n: room.sharedRes, from: seat };
        for (const w of room.players.keys()) send(w, out);
      }
      return;
    }

    if (type === 'escape') {
      /* 탈출 포트 요청 경합 중재 — 같은 심층(d)에서 5초 안에 겹치면
         먼저 온 요청이 이기고, 늦은 요청자에게는 기존 포트를 회신해 수렴시킨다 */
      const d = msg.d | 0;
      room.escapes = room.escapes || new Map();
      const prev = room.escapes.get(d);
      if (prev && Date.now() - prev.at < 5000) {
        send(ws, prev.msg);
        return;
      }
      room.escapes.set(d, { msg: { ...msg, from: seat }, at: Date.now() });
      broadcast(room, { ...msg, from: seat }, ws);
      return;
    }

    if (RELAY_TYPES.has(type)) {
      broadcast(room, { ...msg, from: seat, fromName: me.name }, ws);
    }
  });

  ws.on('close', () => {
    leaveRoom(ws, true); // 진행 중 런이면 좌석을 60초 보존 (재접속 유예)
  });
});

server.listen(PORT, '0.0.0.0', () => {
  const ips = lanIPs();
  console.log('');
  console.log('══════════════════════════════════════════════');
  console.log('  Tunnel Crew — 사내 코옵 서버 v4 (2~4인)');
  console.log('══════════════════════════════════════════════');
  console.log(`  본인:    http://127.0.0.1:${PORT}/`);
  if (ips.length) {
    for (const ip of ips) console.log(`  사내망:  http://${ip}:${PORT}/`);
  } else {
    console.log('  사내망:  (IPv4 없음 — 네트워크 연결을 확인하세요)');
  }
  console.log('');
  console.log(`  '/' 는 ${GAME_HTML} 을 엽니다.`);
  console.log('  1) 메인 메뉴 → LAN 코옵 → 닉네임 입력 (닉네임 = 성장 데이터 계정)');
  console.log('  2) 방 만들기 또는 방 목록에서 참가 (최대 4인)');
  console.log('  3) 전원 역할 선택 → 호스트가 [미션 시작]');
  console.log('');
  console.log('  ⚠ 서버 PC는 절전 해제 + 고정 IP 권장 (주소가 바뀌면 접속 불편)');
  console.log('══════════════════════════════════════════════');
  console.log('');
});
