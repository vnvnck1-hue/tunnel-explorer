/**
 * LAN co-op host: static files + WebSocket lobby/sync.
 * From repo:  cd coop && npm start
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

function other(room, ws) {
  if (room.host === ws) return room.guest;
  if (room.guest === ws) return room.host;
  return null;
}

function roomState(room) {
  return {
    t: 'room',
    code: room.code,
    hasHost: !!room.host,
    hasGuest: !!room.guest,
    started: !!room.started,
    seed: room.seed,
    hostRole: room.hostRole || null,
    guestRole: room.guestRole || null,
    hostReady: !!room.hostReady,
    guestReady: !!room.guestReady,
  };
}

function broadcastRoom(room) {
  const msg = roomState(room);
  send(room.host, msg);
  send(room.guest, msg);
}

const wss = new WebSocketServer({ server });

wss.on('connection', (ws) => {
  ws._meta = { room: null, seat: null };
  send(ws, { t: 'hello', v: 1 });

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
        host: ws,
        guest: null,
        seed,
        started: false,
        hostRole: null,
        guestRole: null,
        hostReady: false,
        guestReady: false,
        sharedRes: 0,
      };
      rooms.set(code, room);
      ws._meta = { room: code, seat: 'host' };
      send(ws, { t: 'hosted', code, seed });
      broadcastRoom(room);
      return;
    }

    if (type === 'join') {
      const code = String(msg.code || '')
        .trim()
        .toUpperCase();
      const room = rooms.get(code);
      if (!room) {
        send(ws, { t: 'err', m: '방을 찾을 수 없습니다.' });
        return;
      }
      if (room.guest) {
        send(ws, { t: 'err', m: '방이 가득 찼습니다.' });
        return;
      }
      room.guest = ws;
      ws._meta = { room: code, seat: 'guest' };
      send(ws, { t: 'joined', code, seed: room.seed });
      broadcastRoom(room);
      return;
    }

    const roomCode = ws._meta.room;
    const room = roomCode && rooms.get(roomCode);
    if (!room) return;
    const seat = ws._meta.seat;

    if (type === 'role') {
      if (seat === 'host') {
        room.hostRole = msg.role || null;
        room.hostReady = !!msg.ready;
      } else {
        room.guestRole = msg.role || null;
        room.guestReady = !!msg.ready;
      }
      broadcastRoom(room);
      return;
    }

    if (type === 'start') {
      if (seat !== 'host') return;
      if (!room.guest) {
        send(ws, { t: 'err', m: '게스트가 아직 없습니다.' });
        return;
      }
      if (!room.hostRole || !room.guestRole) {
        send(ws, { t: 'err', m: '둘 다 역할을 선택하세요.' });
        return;
      }
      room.started = true;
      room.sharedRes = 0;
      const payload = {
        t: 'start',
        seed: room.seed,
        hostRole: room.hostRole,
        guestRole: room.guestRole,
      };
      send(room.host, payload);
      send(room.guest, payload);
      return;
    }

    if (
      type === 'state' ||
      type === 'dig' ||
      type === 'break' ||
      type === 'hp' ||
      type === 'loot' ||
      type === 'lamp' ||
      type === 'skill' ||
      type === 'res' ||
      type === 'end' ||
      type === 'ping'
    ) {
      const peer = other(room, ws);
      if (type === 'res' && typeof msg.n === 'number') {
        room.sharedRes = Math.max(room.sharedRes, msg.n | 0);
        const out = { t: 'res', n: room.sharedRes, from: seat };
        send(room.host, out);
        send(room.guest, out);
        return;
      }
      if (peer) send(peer, { ...msg, from: seat });
    }
  });

  ws.on('close', () => {
    const roomCode = ws._meta.room;
    if (!roomCode) return;
    const room = rooms.get(roomCode);
    if (!room) return;
    const peer = other(room, ws);
    if (room.host === ws) room.host = null;
    if (room.guest === ws) room.guest = null;
    send(peer, { t: 'peer_left' });
    if (!room.host && !room.guest) rooms.delete(roomCode);
    else broadcastRoom(room);
  });
});

server.listen(PORT, '0.0.0.0', () => {
  const ips = lanIPs();
  const pathDemo = '/demos/tunnel-crew-loop-demo.html';
  console.log('');
  console.log('══════════════════════════════════════════════');
  console.log('  Tunnel Explorer — LAN Co-op Host');
  console.log('══════════════════════════════════════════════');
  console.log(`  Local:   http://127.0.0.1:${PORT}${pathDemo}`);
  if (ips.length) {
    for (const ip of ips) {
      console.log(`  LAN:     http://${ip}:${PORT}${pathDemo}`);
    }
  } else {
    console.log('  LAN:     (no IPv4 found — check Wi-Fi)');
  }
  console.log('');
  console.log('  1) 이 PC(윈도우)에서 Local 주소 열기 → 방 만들기');
  console.log('  2) 맥에서 LAN 주소 열기 → 방 코드로 참가');
  console.log('  3) 각자 역할 선택 후 호스트가 [미션 시작]');
  console.log('  방화벽이 물으면 Node.js 개인 네트워크 허용');
  console.log('══════════════════════════════════════════════');
  console.log('');
});
