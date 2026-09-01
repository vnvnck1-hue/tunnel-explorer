// 관전 모드 주입기 — ai/observer.js 를 게임 HTML에 심는다.
//
//   node ai/inject-observer.mjs <대상.html> [출력.html]
//
// - 출력을 생략하면 대상을 제자리에서 갱신한다.
// - 이미 주입된 파일이면 OBSERVER_MODE_INJECTED_V1 <script> 본문만 교체한다 (재주입).
// - 처음 주입할 때는 훅 2개(드라이브·카메라)를 바이트 패치한다.
//   앵커를 정확히 1회 찾지 못하면 멈추고 어떤 훅이 깨졌는지 출력한다.
import fs from 'fs';

const MARK = 'OBSERVER_MODE_INJECTED_V1';
const target = process.argv[2];
const outPath = process.argv[3] || target;
if (!target) { console.error('사용법: node ai/inject-observer.mjs <대상.html> [출력.html]'); process.exit(1); }

let s = fs.readFileSync(target, 'utf8');
const js = fs.readFileSync('ai/observer.js', 'utf8');

function patch(text, old, neu, label) {
  const n = text.split(old).length - 1;
  if (n !== 1) { console.error(`[${label}] 앵커를 ${n}번 찾음 (기대 1)\n---\n${old.slice(0, 200)}\n---`); process.exit(1); }
  return text.replace(old, neu);
}

if (s.includes(MARK)) {
  // 재주입 — 스크립트 본문만 교체
  const mark = s.indexOf(MARK);
  const open = s.indexOf('<script>', mark);
  const close = s.indexOf('</script>', open);
  if (open < 0 || close < 0) { console.error('script 블록을 찾지 못했습니다'); process.exit(1); }
  s = s.slice(0, open) + '<script>\n' + js + '\n' + s.slice(close);
  console.log('재주입: observer.js 본문 교체');
} else {
  // 훅 1 — 리더 오토파일럿: update(dt)의 입력 읽기 직전
  s = patch(s,
    " if(G.downed&&typeof infDownedTick==='function')infDownedTick(dt);   /* v7.7.2c — 기절 중 구조 진행 (stunT 로 입력 차단) */",
    " if(typeof OBSERVER!=='undefined')OBSERVER.drive(dt);   /* 관전 모드 — 리더 오토파일럿 가상 입력 */\n"
    + " if(G.downed&&typeof infDownedTick==='function')infDownedTick(dt);   /* v7.7.2c — 기절 중 구조 진행 (stunT 로 입력 차단) */",
    'update: 오토파일럿 드라이브');

  // 훅 2 — 관전 카메라: 본편 카메라 확정 직후 (파일이 CRLF 라 앵커는 한 줄로 잡는다)
  s = patch(s,
    "  G.camX=bounded.x;G.camY=bounded.y;}",
    "  G.camX=bounded.x;G.camY=bounded.y;\n"
    + "  if(typeof OBSERVER!=='undefined')OBSERVER.camera(dt);   /* 관전 모드 — 시점 오버라이드 */}",
    'update: 관전 카메라');

  // 본체 — AI 크루 스크립트 뒤, </body> 앞
  const block = '\n<!-- ' + MARK + ' — ai/observer.js 에서 생성. 수정은 그 파일을 고치고\n'
    + '     node ai/inject-observer.mjs 를 다시 실행하세요. -->\n'
    + '<script>\n' + js + '\n</script>\n';
  s = patch(s, '</body>', block + '</body>', '본체 삽입');
  console.log('신규 주입: 훅 2개 + 본체');
}

fs.writeFileSync(outPath, s);
console.log('대상 :', target);
console.log('출력 :', outPath);
console.log('크기 :', (Buffer.byteLength(s, 'utf8') / 1048576).toFixed(1), 'MB');
