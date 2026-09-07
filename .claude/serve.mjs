import http from 'node:http';
import { createReadStream, statSync } from 'node:fs';
import { extname, join, normalize } from 'node:path';

const root = process.cwd();
const types = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.css': 'text/css', '.png': 'image/png', '.jpg': 'image/jpeg', '.gif': 'image/gif', '.svg': 'image/svg+xml', '.json': 'application/json', '.mp3': 'audio/mpeg', '.ogg': 'audio/ogg', '.wav': 'audio/wav' };

http.createServer((req, res) => {
  try {
    const url = decodeURIComponent(req.url.split('?')[0]);
    let p = normalize(join(root, url));
    if (!p.startsWith(root)) { res.writeHead(403); res.end(); return; }
    // prototype-html/<하위>/ 의 HTML 은 자산을 저장소 루트 기준으로 참조한다.
    const sub = url.match(/^\/prototype-html\/[^/]+\/(.+)$/);
    if (sub) { try { statSync(p); } catch { p = normalize(join(root, sub[1])); } }
    if (statSync(p).isDirectory()) p = join(p, 'index.html');
    const st = statSync(p);
    res.writeHead(200, { 'Content-Type': types[extname(p).toLowerCase()] || 'application/octet-stream', 'Content-Length': st.size });
    createReadStream(p).pipe(res);
  } catch {
    res.writeHead(404); res.end('not found');
  }
}).listen(8791, () => console.log('serving on 8791'));
