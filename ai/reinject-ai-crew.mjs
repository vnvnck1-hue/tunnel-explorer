// v7.8.0-dopamine.html 안의 AI_CREW_INJECTED_V1 <script> 본문을 ai/crew-ai.js 최신본으로 교체
import fs from 'fs';
const html = 'tunnel-crew-infinite-mode-v7.8.0-dopamine.html';
const s = fs.readFileSync(html, 'utf8');
const js = fs.readFileSync('ai/crew-ai.js', 'utf8');
const mark = s.indexOf('AI_CREW_INJECTED_V1');
if (mark < 0) { console.error('marker not found'); process.exit(1); }
const open = s.indexOf('<script>', mark);
const close = s.indexOf('</script>', open);
if (open < 0 || close < 0) { console.error('script block not found'); process.exit(1); }
const out = s.slice(0, open) + '<script>\n' + js + '\n' + s.slice(close);
fs.writeFileSync(html, out);
console.log('reinjected: old', close - open, '→ new', js.length + 10, 'bytes');
