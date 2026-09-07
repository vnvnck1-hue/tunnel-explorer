import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const port = Number(process.env.PREVIEW_PORT || 4173);
const mime = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.md': 'text/markdown; charset=utf-8',
};

http.createServer((req, res) => {
  const pathname = decodeURIComponent(new URL(req.url, `http://${req.headers.host}`).pathname);
  const relative = pathname === '/' ? '/prototype-html/latest/tunnel-crew-infinite-mode-v7.9.2.html' : pathname;
  const ok = p => p.startsWith(root) && fs.existsSync(p) && fs.statSync(p).isFile();
  /* 프로토타입 HTML 은 prototype-html/<하위>/ 에 있고 자산은 저장소 루트 기준으로
     참조하므로, 하위 경로에서 못 찾으면 루트에서 한 번 더 찾는다. */
  const sub = relative.match(/^\/prototype-html\/[^/]+\/(.+)$/);
  let file = path.resolve(root, `.${relative}`);
  if (!ok(file) && sub) file = path.resolve(root, sub[1]);
  if (!ok(file)) {
    res.writeHead(404); res.end('Not found'); return;
  }
  res.writeHead(200, {'Content-Type': mime[path.extname(file).toLowerCase()] || 'application/octet-stream', 'Cache-Control': 'no-store'});
  fs.createReadStream(file).pipe(res);
}).listen(port, '127.0.0.1', () => console.log(`preview server listening on http://127.0.0.1:${port}/`));
