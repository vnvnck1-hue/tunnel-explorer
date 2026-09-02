// 팀 핑 시스템 주입기 — ping/tc-ping.js 를 게임 HTML에 심는다.
//
//   node ping/inject-ping.mjs <대상.html> [출력.html]
//
// - 출력을 생략하면 대상을 제자리에서 갱신한다.
// - 이미 주입된 파일이면 TEAM_PING_INJECTED_V1 <script> 본문만 교체한다 (재주입).
// - 본편 함수 본문은 건드리지 않는다 (paintUI / AICREW.update / crewPaintSettings 를 런타임에 감싼다).
//   바이트 패치는 메뉴 빌드 라벨(PROTOTYPE / x.y.z) 하나만, 있으면 바꾸고 없으면 넘어간다.
// - 파일은 CRLF · 초장문 라인이므로 텍스트 정규화 없이 문자열 치환만 한다.
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const MARK = 'TEAM_PING_INJECTED_V1';
const target = process.argv[2];
const outPath = process.argv[3] || target;
if (!target) { console.error('사용법: node ping/inject-ping.mjs <대상.html> [출력.html]'); process.exit(1); }

let s = fs.readFileSync(target, 'utf8');
const js = fs.readFileSync(path.join(HERE, 'tc-ping.js'), 'utf8');

function patch(text, old, neu, label) {
  const n = text.split(old).length - 1;
  if (n !== 1) { console.error(`[${label}] 앵커를 ${n}번 찾음 (기대 1)\n---\n${old.slice(0, 200)}\n---`); process.exit(1); }
  return text.replace(old, neu);
}

if (s.includes(MARK)) {
  const mark = s.indexOf(MARK);
  const open = s.indexOf('<script>', mark);
  const close = s.indexOf('</script>', open);
  if (open < 0 || close < 0) { console.error('script 블록을 찾지 못했습니다'); process.exit(1); }
  s = s.slice(0, open) + '<script>\n' + js + '\n' + s.slice(close);
  console.log('재주입: tc-ping.js 본문 교체');
} else {
  /* 빌드 라벨 — "PROTOTYPE / 7.8.1-ui" 처럼 한 곳. 없으면 경고만 */
  const m = s.match(/PROTOTYPE \/ ([0-9][0-9.]*)(-[a-z0-9-]+)?</);
  if (m && s.split(m[0]).length - 1 === 1) {
    s = s.replace(m[0], `PROTOTYPE / ${m[1]}-ping<`);
    console.log('빌드 라벨:', m[0].slice(0, -1), '→', `PROTOTYPE / ${m[1]}-ping`);
  } else console.warn('빌드 라벨 앵커 없음 — 건너뜀');

  const block = '\n<!-- ' + MARK + ' — ping/tc-ping.js 에서 생성. 수정은 그 파일을 고치고\n'
    + '     node ping/inject-ping.mjs 를 다시 실행하세요. 기획: docs/tunnel-crew-ping-system.md -->\n'
    + '<script>\n' + js + '\n</script>\n';
  s = patch(s, '</body>', block + '</body>', '본체 삽입');
  console.log('신규 주입: 본체 (훅은 런타임 래핑)');
}

fs.writeFileSync(outPath, s);
console.log('대상 :', target);
console.log('출력 :', outPath);
console.log('크기 :', (Buffer.byteLength(s, 'utf8') / 1048576).toFixed(1), 'MB');
