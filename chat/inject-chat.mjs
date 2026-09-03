// 크루 채팅 주입기 — chat/tc-chat.js 를 게임 HTML에 심는다.
//
//   node chat/inject-chat.mjs <대상.html> [출력.html]
//
// - 출력을 생략하면 대상을 제자리에서 갱신한다.
// - 이미 주입된 파일이면 TEAM_CHAT_INJECTED_V1 <script> 본문만 교체한다 (재주입).
// - 본편 함수 본문은 건드리지 않는다 (paintUI 를 런타임에 감싼다). 빌드 라벨도 바꾸지 않는다.
// - 파일은 CRLF · 초장문 라인이므로 텍스트 정규화 없이 문자열 치환만 한다.
// - 코옵에서 쓰려면 coop/server.mjs RELAY_TYPES 에 'chat' 이 있어야 한다.
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const MARK = 'TEAM_CHAT_INJECTED_V1';
const target = process.argv[2];
const outPath = process.argv[3] || target;
if (!target) { console.error('사용법: node chat/inject-chat.mjs <대상.html> [출력.html]'); process.exit(1); }

let s = fs.readFileSync(target, 'utf8');
const js = fs.readFileSync(path.join(HERE, 'tc-chat.js'), 'utf8');
if (/<\/script/i.test(js)) { console.error('tc-chat.js 안에 </script> 가 있어 인라인할 수 없습니다'); process.exit(1); }

function patch(text, old, neu, label) {
  const n = text.split(old).length - 1;
  if (n !== 1) { console.error(`[${label}] 앵커를 ${n}번 찾음 (기대 1)\n---\n${old.slice(0, 200)}\n---`); process.exit(1); }
  return text.replace(old, () => neu);
}

if (s.includes(MARK)) {
  const mark = s.indexOf(MARK);
  const open = s.indexOf('<script>', mark);
  const close = s.indexOf('</script>', open);
  if (open < 0 || close < 0) { console.error('script 블록을 찾지 못했습니다'); process.exit(1); }
  s = s.slice(0, open) + '<script>\n' + js + '\n' + s.slice(close);
  console.log('재주입: tc-chat.js 본문 교체');
} else {
  const block = '\n<!-- ' + MARK + ' — chat/tc-chat.js 에서 생성. 수정은 그 파일을 고치고\n'
    + '     node chat/inject-chat.mjs 를 다시 실행하세요. -->\n'
    + '<script>\n' + js + '\n</script>\n';
  s = patch(s, '</body>', block + '</body>', '본체 삽입');
  console.log('신규 주입: 본체 (훅은 런타임 래핑)');
}

fs.writeFileSync(outPath, s);
console.log('대상 :', target);
console.log('출력 :', outPath);
console.log('크기 :', (Buffer.byteLength(s, 'utf8') / 1048576).toFixed(1), 'MB');
