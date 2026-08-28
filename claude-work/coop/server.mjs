/**
 * LAN co-op host v2: static files + WebSocket lobby/sync.
 * From repo:  cd coop && npm start
 *
 * v2 (트랙 F — 기획서 §19.1): 2인 host/guest 모델을 2~4인 좌석(p1~p4) 모델로
 * 확장하고, 방에 mode('harvest' 채취 미션 | 'infinite' 본편 무한 모드)를 더했다.
 * 서버는 여전히 권위 없는 릴레이다 — 게임 규칙 판정은 클라이언트가 한다.
 * 탈출 전원 탑승(§8.4-3·4) 같은 집계는 각 클라이언트가 board/end 이벤트를
 * 모아 판정한다.
 */
import http from 'http';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { networkInterfaces } from 'os';
import { WebSocketServer } from 'ws';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '..');
const PORT = Number(process.env.PORT || 5188);
const MAX_SEATS = 4;

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

const server = http.createServer((req, res) => {
  try {
    const u = new URL(req.url || '/', `http://${req.headers.host}`);
    let p = decodeURIComponent(u.pathname);
    if (p === '/') p = '/demos/tunnel-crew-loop-demo.html';
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

/* 좌석: p1(호스트) ~ p4. 나간 좌석 번호는 재사용한다. */
function freeSeat(room) {
  for (let i = 1; i <= MAX_SEATS; i++) {
    const seat = 'p' + i;
    if (![...room.players.values()].some((p) => p.seat === seat)) return seat;
  }
  return null;
}

function roomState(room) {
  const players = [...room.players.values()]
    .map((p) => ({ seat: p.seat, role: p.role, ready: !!p.ready }))
    .sort((a, b) => (a.seat < b.seat ? -1 : 1));
  return {
    t: 'room',
    code: room.code,
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

/* 게임 이벤트는 검사 없이 릴레이한다. 새 이벤트 타입은 여기만 추가하면 된다. */
const RELAY_TYPES = new Set([
  'state', 'dig', 'break', 'hp', 'loot', 'lamp', 'skill', 'end', 'ping',
  /* 트랙 F — 본편 무한 모드 */
  'escape',  /* 탈출 포트 요청/재현 {x,y,need,auto} */
  'board',   /* 탈출 포트 탑승 확정 */
  'level',   /* 개인 레벨업 알림 {level} — 명판 표시용, 성장은 개인(§5.2) */
  'boss',    /* 보스 이벤트 {ev:'spawn'|'down',tier} */
]);

const wss = new WebSocketServer({ server });

wss.on('connection', (ws) => {
  ws._meta = { room: null, seat: null };
  send(ws, { t: 'hello', v: 2, maxSeats: MAX_SEATS });

  ws.on('message', (raw) => {
    let msg;
    try {
      msg = JSON.parse(String(raw));
    } catch {
      return;
    }
    const type = msg.t;

    if (type === 'host') {
      let code = code4();
      while (rooms.has(code)) code = code4();
      const seed = msg.seed || 'tunnel-' + Math.floor(Math.random() * 1e9);
      const room = {
        code,
        seed,
        mode: msg.mode === 'infinite' ? 'infinite' : 'harvest',
        started: false,
        players: new Map([[ws, { seat: 'p1', role: null, ready: false }]]),
        sharedRes: 0,
      };
      rooms.set(code, room);
      ws._meta = { room: code, seat: 'p1' };
      send(ws, { t: 'hosted', code, seed, seat: 'p1' });
      broadcastRoom(room);
      return;
    }

    if (type === 'join') {
      const code = String(msg.code || '').trim().toUpperCase();
      const room = rooms.get(code);
      if (!room) {
        send(ws, { t: 'err', m: '방을 찾을 수 없습니다.' });
        return;
      }
      if (room.started) {
        send(ws, { t: 'err', m: '이미 시작된 방입니다.' });
        return;
      }
      const seat = freeSeat(room);
      if (!seat) {
        send(ws, { t: 'err', m: '방이 가득 찼습니다. (최대 ' + MAX_SEATS + '인)' });
        return;
      }
      room.players.set(ws, { seat, role: null, ready: false });
      ws._meta = { room: code, seat };
      send(ws, { t: 'joined', code, seed: room.seed, seat, mode: room.mode });
      broadcastRoom(room);
      return;
    }

    const roomCode = ws._meta.room;
    const room = roomCode && rooms.get(roomCode);
    if (!room) return;
    const me = room.players.get(ws);
    if (!me) return;
    const seat = me.seat;

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
      const payload = {
        t: 'start',
        seed: room.seed,
        mode: room.mode,
        players: players.map((p) => ({ seat: p.seat, role: p.role })),
      };
      for (const w of room.players.keys()) send(w, payload);
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

    if (RELAY_TYPES.has(type)) {
      broadcast(room, { ...msg, from: seat }, ws);
    }
  });

  ws.on('close', () => {
    const roomCode = ws._meta.room;
    if (!roomCode) return;
    const room = rooms.get(roomCode);
    if (!room) return;
    const me = room.players.get(ws);
    room.players.delete(ws);
    if (me) broadcast(room, { t: 'peer_left', seat: me.seat }, null);
    if (room.players.size === 0) rooms.delete(roomCode);
    else broadcastRoom(room);
  });
});

server.listen(PORT, '0.0.0.0', () => {
  const ips = lanIPs();
  const pathDemo = '/demos/tunnel-crew-loop-demo.html';
  console.log('');
  console.log('══════════════════════════════════════════════');
  console.log('  Tunnel Crew — LAN Co-op Host v2 (2~4인)');
  console.log('══════════════════════════════════════════════');
  console.log(`  Local:   http://127.0.0.1:${PORT}${pathDemo}`);
  if (ips.length) {
    for (const ip of ips) console.log(`  LAN:     http://${ip}:${PORT}${pathDemo}`);
  } else {
    console.log('  LAN:     (no IPv4 found — check Wi-Fi)');
  }
  console.log('');
  console.log('  본편(무한 모드) 코옵: 같은 포트에서 게임 HTML을 열고');
  console.log('  메인 메뉴 → LAN 코옵 → 모드 [무한 모드]로 시작');
  console.log('  1) 호스트: 방 만들기 → 코드 공유');
  console.log('  2) 참가자(최대 3명): 코드 입력 → 참가');
  console.log('  3) 전원 역할 선택 → 호스트가 [미션 시작]');
  console.log('══════════════════════════════════════════════');
  console.log('');
});
