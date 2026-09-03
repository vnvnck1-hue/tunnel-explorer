#!/usr/bin/env node
/*
  땅굴 크루 — 스탠드얼론 배포 패키지 빌더 ("빌드")
  ──────────────────────────────────────────────────────────────────
  본선 HTML + 참조 자산 + LAN 코옵 서버 + 포터블 node.exe + START.bat + 실행안내.md 를
  build/TunnelCrew-vX.Y.Z/ 에 모으고 같은 이름의 zip 을 만든다. 설치·인터넷 없이
  폴더만 복사하면 어느 Windows PC 에서든 START.bat 으로 실행된다.

  사용:  node tools/build-package.mjs [원본.html] [--force] [--no-zip]
  기본:  원본 = 프로젝트 루트의 최신 tunnel-crew-infinite-mode-v*.html
         --force  : 같은 버전 폴더가 이미 있으면 지우고 다시 만든다
         --no-zip : 폴더만 만들고 zip 은 생략

  zip 은 반드시 bsdtar(C:\Windows\System32\tar.exe -a) 로 만든다.
  PowerShell Compress-Archive 는 한글 파일명(실행안내.md)과 경로 구분자를 깨뜨린다.
*/
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { execFileSync } from 'node:child_process';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PROJECT = path.resolve(__dirname, '..');
const args = process.argv.slice(2);
const FORCE = args.includes('--force');
const NO_ZIP = args.includes('--no-zip');
const ZIP_ONLY = args.includes('--zip-only'); // 이미 만든 패키지 폴더를 다시 zip 으로만 묶고 검사한다
const srcArg = args.find(a => !a.startsWith('--'));

function newestMainHtml() {
  const cands = fs.readdirSync(PROJECT)
    .map(f => ({ f, m: f.match(/^tunnel-crew-infinite-mode-v(\d+)\.(\d+)\.(\d+)\.html$/) }))
    .filter(x => x.m)
    .sort((a, b) => (+b.m[1] - +a.m[1]) || (+b.m[2] - +a.m[2]) || (+b.m[3] - +a.m[3]));
  if (!cands.length) throw new Error('루트에 tunnel-crew-infinite-mode-vX.Y.Z.html 이 없습니다 — 원본 경로를 인자로 주세요');
  return path.join(PROJECT, cands[0].f);
}

const SRC = path.resolve(srcArg || newestMainHtml());
const SRC_DIR = path.dirname(SRC);
const HTML_NAME = path.basename(SRC);
const verM = HTML_NAME.match(/v(\d+\.\d+\.\d+)/);
if (!verM) throw new Error('원본 파일명에서 버전(vX.Y.Z)을 읽지 못했습니다: ' + HTML_NAME);
const VERSION = verM[1];
const BUILD_DIR = path.join(PROJECT, 'build');
const PKG_NAME = `TunnelCrew-v${VERSION}`;
const OUT = path.join(BUILD_DIR, PKG_NAME);
const ZIP = path.join(BUILD_DIR, PKG_NAME + '.zip');
const TAR = 'C:\\Windows\\System32\\tar.exe';

const html = fs.readFileSync(SRC, 'utf8');
const fmtMB = n => (n / 1048576).toFixed(1) + ' MB';
const log = (...a) => console.log(...a);

/* ── 0. 출력 폴더 준비 ─────────────────────────────────────────────── */
if (ZIP_ONLY) {
  if (!fs.existsSync(OUT)) { console.error(`패키지 폴더가 없습니다: ${OUT}`); process.exit(2); }
} else {
  if (fs.existsSync(OUT)) {
    if (!FORCE) { console.error(`이미 있습니다: ${OUT}\n  다시 만들려면 --force 를 붙이세요.`); process.exit(2); }
    fs.rmSync(OUT, { recursive: true, force: true });
  }
  fs.mkdirSync(OUT, { recursive: true });
}

/* ── 1. 복사 유틸 ──────────────────────────────────────────────────── */
let copied = 0, copiedBytes = 0;
function copyFile(src, dst) {
  fs.mkdirSync(path.dirname(dst), { recursive: true });
  fs.copyFileSync(src, dst);
  copied++; copiedBytes += fs.statSync(src).size;
}
function copyDir(src, dst, filter = () => true) {
  if (!fs.existsSync(src)) { console.warn('경고: 폴더 없음 —', src); return; }
  for (const e of fs.readdirSync(src, { withFileTypes: true })) {
    const s = path.join(src, e.name), d = path.join(dst, e.name);
    const rel = path.relative(PROJECT, s).split(path.sep).join('/');
    if (!filter(rel, e)) continue;
    if (e.isDirectory()) copyDir(s, d, filter); else copyFile(s, d);
  }
}

// HTML 이 참조하는 파일 토큰 (루트 파일 복사·누락 검사에 쓴다)
const tokenRe = /[A-Za-z0-9_\-./]+\.(?:png|gif|webp|jpe?g|svg|ogg|mp3|wav|webm|mp4|css|json)\b/g;
const tokens = new Set([...html.matchAll(tokenRe)].map(m => m[0].replace(/^\.?\//, '')));

if (!ZIP_ONLY) {
/* ── 2. 게임 HTML ──────────────────────────────────────────────────── */
copyFile(SRC, path.join(OUT, HTML_NAME));

/* ── 3. 자산 폴더 ──────────────────────────────────────────────────── */
// assets/ 전체 — 원본 psd 는 제외, 컷씬 영상은 HTML 이 참조할 때만
const cutsceneUsed = /assets\/cutscenes\//.test(html) && !/INF_CUTSCENE=\{enabled:false/.test(html);
copyDir(path.join(SRC_DIR, 'assets'), path.join(OUT, 'assets'), (rel, e) => {
  if (!e.isDirectory() && /\.psd$/i.test(rel)) return false;
  if (rel === 'assets/cutscenes' && !cutsceneUsed) return false;
  return true;
});
// 타일 리소스 전체 (purple·brine 바이옴 + 매니페스트)
copyDir(path.join(SRC_DIR, 'tunnel_crew_tile_resources_v1'), path.join(OUT, 'tunnel_crew_tile_resources_v1'));
// 몬스터는 frames 만 (concepts·sheets·tools 는 제작 원본, frames/archive 는 보관용) — 빼먹으면 적 스프라이트 404
copyDir(path.join(SRC_DIR, 'monster_assets_v1.5.4', 'frames'), path.join(OUT, 'monster_assets_v1.5.4', 'frames'),
  (rel, e) => !(e.isDirectory() && /\/(archive|_old|old|backup)$/i.test(rel)));

// HTML 옆 루트 파일 참조(예: 예전 dragon_boss_death_transparent.gif) — 토큰이 루트에 실존하면 복사
for (const t of tokens) {
  if (t.includes('/')) continue;
  const p = path.join(SRC_DIR, t);
  if (fs.existsSync(p) && fs.statSync(p).isFile() && !fs.existsSync(path.join(OUT, t))) copyFile(p, path.join(OUT, t));
}

/* ── 4. 코옵 서버 ──────────────────────────────────────────────────── */
const COOP_SRC = path.join(PROJECT, 'coop');
for (const f of ['server.mjs', 'client.js', 'package.json', 'package-lock.json', 'README.md', 'CHECKLIST.md']) {
  const p = path.join(COOP_SRC, f);
  if (fs.existsSync(p)) copyFile(p, path.join(OUT, 'coop', f));
}
copyDir(path.join(COOP_SRC, 'node_modules'), path.join(OUT, 'coop', 'node_modules'));
fs.mkdirSync(path.join(OUT, 'coop', 'saves'), { recursive: true }); // 성장 데이터는 비워서 출고
{
  const sp = path.join(OUT, 'coop', 'server.mjs');
  let s = fs.readFileSync(sp, 'utf8');
  const re = /const GAME_HTML = process\.env\.GAME \|\| '\/[^']+';/;
  if (!re.test(s)) throw new Error('coop/server.mjs 에서 GAME_HTML 줄을 찾지 못했습니다');
  s = s.replace(re, `const GAME_HTML = process.env.GAME || '/${HTML_NAME}';`);
  fs.writeFileSync(sp, s);
}

/* ── 5. 포터블 node.exe ────────────────────────────────────────────── */
{
  const prior = fs.readdirSync(BUILD_DIR)
    .filter(d => /^TunnelCrew-v\d+\.\d+\.\d+$/.test(d) && d !== PKG_NAME)
    .map(d => path.join(BUILD_DIR, d, 'node', 'node.exe'))
    .filter(p => fs.existsSync(p))
    .sort((a, b) => fs.statSync(b).mtimeMs - fs.statSync(a).mtimeMs);
  let nodeSrc = prior[0];
  if (!nodeSrc) {
    if (process.platform !== 'win32' || process.arch !== 'x64') throw new Error('포터블 node.exe 를 찾지 못했습니다 (이전 패키지 없음, 현재 node 가 win32/x64 아님)');
    nodeSrc = process.execPath;
  }
  copyFile(nodeSrc, path.join(OUT, 'node', 'node.exe'));
  log('node.exe ←', nodeSrc);
}

/* ── 6. START.bat · 실행안내.md ────────────────────────────────────── */
const START_BAT = `@echo off
setlocal EnableDelayedExpansion
cd /d "%~dp0"

rem Usage: START.bat          -> port 5188 (or the next free port if 5188 is taken)
rem        START.bat 5189     -> force port 5189
set FIXED=%~1
if "%FIXED%"=="" (set PORT=5188) else (set PORT=%FIXED%)
set TRIES=0

:check
netstat -ano -p TCP | findstr /R /C:":%PORT% .*LISTENING" >nul
if not errorlevel 1 (
  echo  [*] Port %PORT% is already used by another program on this PC.
  if not "%FIXED%"=="" goto :busy
  set /a PORT+=1
  set /a TRIES+=1
  if !TRIES! GEQ 12 goto :busy
  echo      Trying port !PORT! instead ...
  goto :check
)

echo.
echo  ==========================================
echo   TUNNEL CREW v${VERSION}  (LAN co-op server)
echo  ==========================================
echo.
echo  Starting server on port %PORT% ...
echo  If Windows Firewall asks, click ALLOW.
echo  Close this window to stop the server.
echo.

start "" cmd /c "timeout /t 2 >nul & start http://localhost:%PORT%/"
"%~dp0node\\node.exe" "%~dp0coop\\server.mjs" %PORT%

echo.
echo  Server stopped. (If it exited immediately, the port may be in use.)
echo  Try another port:  START.bat 5189
pause
exit /b

:busy
echo.
echo  Could not find a free port. Another server (maybe an older Tunnel Crew
echo  or a dev server) is running on this PC. Close it, or run:
echo      START.bat 5200
echo.
pause
exit /b 1
`.replace(/\r?\n/g, '\r\n');
fs.writeFileSync(path.join(OUT, 'START.bat'), START_BAT);

const GUIDE = `# 땅굴 크루 v${VERSION} — 배포 패키지 실행 안내

Node.js 설치가 **필요 없습니다**. 이 폴더 하나로 어떤 Windows PC에서든 그대로 동작합니다.
(포터블 node.exe 동봉 · 인터넷 연결 불필요 · 완전 오프라인 동작)

## 1. 서버 켜기 (한 대만)

1. 이 폴더를 PC 아무 곳에나 복사합니다. (예: \`C:\\TunnelCrew\`)
2. **\`START.bat\` 더블클릭** — 서버가 켜지고 브라우저가 자동으로 열립니다.
3. 처음 켤 때 **Windows 방화벽 창이 뜨면 "액세스 허용"** 을 눌러 주세요.
   (허용하지 않으면 다른 PC가 접속할 수 없습니다 — 혼자 할 때는 상관없음)
4. 서버 창에 표시되는 \`사내망: http://<IP>:5188/\` 주소를 팀에 공유합니다.

> 포트 5188이 이미 사용 중이면 자동으로 다음 빈 포트를 찾습니다. 고정하려면 \`START.bat 5189\` 처럼 인자로 실행

## 2. 다른 PC에서 접속 (설치 불필요)

브라우저(크롬/엣지)로 서버 창에 표시된 \`http://<서버IP>:5188/\` 접속 — 그게 전부입니다.

## 3. LAN 코옵 (2~4인)

1. 메인 메뉴 → **LAN 코옵**
2. **닉네임** 입력 — 닉네임이 곧 계정입니다 (성장 데이터가 서버에 닉네임별로 저장됨)
3. **방 만들기** 또는 방 목록에서 **[참가]**
4. 전원 역할 선택 → 호스트(👑 P1)가 **미션 시작**

## 4. 팀 핑

- **G 탭**: 현재 조준 위치에 "여기" 핑 (적·광맥·기절 크루·탈출 포트 위에서는 자동으로 의미가 구체화)
- **G 홀드 + 방향 + 릴리즈**: 8방향 핑 휠 — 가자·공격·발견·채굴·후퇴·방어·도움·위험
- **V**: 빠른 위험 핑
- 코옵에서는 전 크루에게 전달되고, AI 크루는 명령형 핑(가자·공격·채굴·방어)에 따라 움직입니다.

## 5. AI 크루 · 관전 모드

- **AI 크루**: 직업 선택 화면에서 카드 우측 상단 \`+ AI\` 를 눌러 AI 동료 추가 (최대 3명)
- **관전 모드**: 무한 모드 진행 중 **F9** — 4직업이 완전 자동으로 진행되고 사람은 관찰만
  - \`Tab\` 시점 전환 · \`[\` \`]\` 줌 · \`Esc\` 현재 크루 빙의(직접 조종) · \`F9\` 관전 복귀
- **F10**: AI 경로·목표 오버레이

## 6. 데이터 저장 위치

- 성장 데이터(노드 트리·코어·기록)는 서버 PC의 \`coop\\saves\\<닉네임>.json\` 에 저장됩니다.
- 패키지를 새 버전으로 교체할 때 \`coop\\saves\` 폴더만 복사하면 성장이 이어집니다.

## 7. 문제 해결

| 증상 | 조치 |
|---|---|
| 다른 PC에서 접속 안 됨 | 서버 PC 방화벽에서 node.exe 허용 확인 · 같은 네트워크(사내망)인지 확인 |
| 서버 창이 바로 꺼짐 | 포트 충돌 — \`START.bat 5189\` 로 재시도 |
| 접속 주소가 자꾸 바뀜 | 서버 PC에 고정 IP 권장 (성장 데이터는 닉네임 키라 주소가 바뀌어도 유지) |
| 게임이 이상하게 어긋남 | 모든 접속자가 페이지 새로고침(F5) — 오래 열어둔 탭은 버전 체크에 걸립니다 |

문제가 계속되면 서버 창 로그와 브라우저 F12 콘솔을 확인하세요.

## 8. 설치 없는 단일 파일 버전

혼자 플레이(솔로 · 무한 · AI 크루 · 관전)만 필요하면 \`tunnel-crew-infinite-mode-v${VERSION}-single.html\` 한 파일을
더블클릭해서 바로 실행할 수 있습니다. 이 버전은 서버가 없어 **LAN 코옵은 비활성**입니다.
`;
fs.writeFileSync(path.join(OUT, '실행안내.md'), GUIDE);
} // !ZIP_ONLY

/* ── 7. 참조 자산 존재 검사 ────────────────────────────────────────── */
const missing = [];
for (const t of tokens) {
  if (!/^(assets|monster_assets_v1\.5\.4|tunnel_crew_tile_resources_v1)\//.test(t)) continue;
  if (!fs.existsSync(path.join(OUT, t))) missing.push(t);
}
const mustHave = ['assets/audio/ambience', 'assets/audio/bgm', 'monster_assets_v1.5.4/frames/crawler',
  'monster_assets_v1.5.4/frames/spitter', 'monster_assets_v1.5.4/frames/brood-beast',
  'tunnel_crew_tile_resources_v1/purple', 'tunnel_crew_tile_resources_v1/brine', 'coop/client.js', 'node/node.exe'];
for (const m of mustHave) if (!fs.existsSync(path.join(OUT, m))) missing.push(m + ' (필수)');

/* zip 중앙 디렉터리에서 항목 이름을 읽는다 (zip64 아닌 일반 zip 기준) */
function zipEntryNames(zipPath) {
  const fd = fs.openSync(zipPath, 'r');
  try {
    const size = fs.statSync(zipPath).size;
    const tailLen = Math.min(size, 65557);
    const tail = Buffer.alloc(tailLen);
    fs.readSync(fd, tail, 0, tailLen, size - tailLen);
    let eocd = -1;
    for (let i = tailLen - 22; i >= 0; i--) if (tail.readUInt32LE(i) === 0x06054b50) { eocd = i; break; }
    if (eocd < 0) throw new Error('zip EOCD 를 찾지 못했습니다');
    const cdSize = tail.readUInt32LE(eocd + 12), cdOff = tail.readUInt32LE(eocd + 16);
    const cd = Buffer.alloc(cdSize);
    fs.readSync(fd, cd, 0, cdSize, cdOff);
    const names = [];
    let p = 0;
    while (p + 46 <= cdSize && cd.readUInt32LE(p) === 0x02014b50) {
      const flags = cd.readUInt16LE(p + 8), n = cd.readUInt16LE(p + 28), x = cd.readUInt16LE(p + 30), c = cd.readUInt16LE(p + 32);
      const raw = cd.subarray(p + 46, p + 46 + n);
      // bsdtar 는 한글 이름을 UTF-8 플래그 없이 시스템 코드페이지(CP949)로 쓴다 — Windows 탐색기 압축 해제와 같은 규약
      names.push((flags & 0x800) ? raw.toString('utf8') : new TextDecoder('euc-kr').decode(raw));
      p += 46 + n + x + c;
    }
    return names;
  } finally { fs.closeSync(fd); }
}

/* ── 8. zip (bsdtar) ───────────────────────────────────────────────── */
let zipInfo = '(생략)';
if (!NO_ZIP) {
  if (!fs.existsSync(TAR)) throw new Error('bsdtar 를 찾지 못했습니다: ' + TAR);
  if (fs.existsSync(ZIP)) fs.rmSync(ZIP);
  execFileSync(TAR, ['-a', '-cf', ZIP, '-C', BUILD_DIR, PKG_NAME], { stdio: 'inherit' });
  // 항목 이름은 zip 중앙 디렉터리를 직접 읽어 UTF-8 로 해석한다 (tar -tf 콘솔 출력은 코드페이지에 따라 한글이 깨져 보인다)
  const entries = zipEntryNames(ZIP);
  const badSep = entries.filter(e => e.includes('\\'));
  const guideOk = entries.some(e => e.endsWith('/실행안내.md'));
  zipInfo = `${ZIP} ${fmtMB(fs.statSync(ZIP).size)} · ${entries.length} 항목 · 한글 파일명 ${guideOk ? '정상' : '깨짐!'}${badSep.length ? ' · 백슬래시 경로 ' + badSep.length + '건!' : ''}`;
  if (!guideOk || badSep.length) missing.push('zip 무결성 (실행안내.md 또는 경로 구분자)');
}

/* ── 9. 리포트 ─────────────────────────────────────────────────────── */
function dirSize(p) { let s = 0; for (const e of fs.readdirSync(p, { withFileTypes: true })) { const q = path.join(p, e.name); s += e.isDirectory() ? dirSize(q) : fs.statSync(q).size; } return s; }
log('\n원본   :', SRC);
log('패키지 :', OUT, fmtMB(dirSize(OUT)), `(${copied} 파일)`);
for (const d of fs.readdirSync(OUT)) { const p = path.join(OUT, d); log('  ', fmtMB(fs.statSync(p).isDirectory() ? dirSize(p) : fs.statSync(p).size).padStart(9), d); }
log('zip    :', zipInfo);
log('GAME_HTML → /' + HTML_NAME);
if (missing.length) { log('\n[누락]'); for (const m of missing) log('  ', m); process.exitCode = 1; }
else log('\n참조 자산 누락 없음 · 필수 폴더 확인 완료');
