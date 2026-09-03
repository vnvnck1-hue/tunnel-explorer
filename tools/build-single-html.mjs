#!/usr/bin/env node
/*
  땅굴 크루 — 단일 HTML 빌더
  ──────────────────────────────────────────────────────────────────
  본선 HTML 이 참조하는 외부 자산(이미지·오디오·CSS)을 전부 data URI 로
  인라인하고, 런타임 shim 으로 Image.src / fetch / innerHTML / CSS url()
  등을 가로채서 자산 맵에서 찾아준다. 본문 게임 코드는 수정하지 않는다.

  사용:  node tools/build-single-html.mjs [원본.html] [출력.html]
  기본:  원본 = 프로젝트 루트의 최신 tunnel-crew-infinite-mode-v*.html
         출력 = build/<원본 이름>-single.html
  자산은 원본 HTML 이 있는 폴더 기준(assets/ · monster_assets_v1.5.4/ · tunnel_crew_tile_resources_v1/)으로 찾는다.

  단일 파일에서 빠지는 것: LAN 코옵(ws 서버)·서버 저장(coop/saves).
  → 메인 메뉴의 LAN 코옵 버튼은 잠금(locked·disabled) 상태로 남겨 둔다.
*/
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PROJECT = path.resolve(__dirname, '..');

/* 루트에서 가장 높은 버전의 본선 HTML 을 고른다 (v7.9.0 > v7.8.1) */
function newestMainHtml() {
  const cands = fs.readdirSync(PROJECT)
    .map(f => ({ f, m: f.match(/^tunnel-crew-infinite-mode-v(\d+)\.(\d+)\.(\d+)\.html$/) }))
    .filter(x => x.m)
    .sort((a, b) => (+b.m[1] - +a.m[1]) || (+b.m[2] - +a.m[2]) || (+b.m[3] - +a.m[3]));
  if (!cands.length) throw new Error('루트에 tunnel-crew-infinite-mode-vX.Y.Z.html 이 없습니다 — 원본 경로를 인자로 주세요');
  return path.join(PROJECT, cands[0].f);
}
const SRC = path.resolve(process.argv[2] || newestMainHtml());
const OUT = path.resolve(process.argv[3] || path.join(PROJECT, 'build', path.basename(SRC, '.html') + '-single.html'));
const ROOT = path.dirname(SRC);

const ASSET_ROOTS = ['assets', 'monster_assets_v1.5.4', 'tunnel_crew_tile_resources_v1'];
const EXT_RE = /\.(png|gif|webp|jpe?g|svg|ogg|mp3|wav|webm|css|json|woff2?|ttf)$/i;
const MIME = {
  png: 'image/png', gif: 'image/gif', webp: 'image/webp', jpg: 'image/jpeg', jpeg: 'image/jpeg', svg: 'image/svg+xml',
  ogg: 'audio/ogg', mp3: 'audio/mpeg', wav: 'audio/wav', webm: 'audio/webm',
  css: 'text/css', json: 'application/json', woff: 'font/woff', woff2: 'font/woff2', ttf: 'font/ttf',
};
const ROOT_RE_SRC = '(?:\\./)?(?:assets|monster_assets_v1\\.5\\.4|tunnel_crew_tile_resources_v1)/';

const html = fs.readFileSync(SRC, 'utf8');
const fmtMB = n => (n / 1048576).toFixed(2) + ' MB';

/* ── 1. 자산 파일 전체 목록 ───────────────────────────────────────── */
function walk(dir, out = []) {
  if (!fs.existsSync(dir)) return out;
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) walk(p, out);
    else out.push(p);
  }
  return out;
}
const allFiles = ASSET_ROOTS.flatMap(r => walk(path.join(ROOT, r)))
  .map(p => path.relative(ROOT, p).split(path.sep).join('/'))
  .filter(p => EXT_RE.test(p));

/* ── 2. HTML 에 등장하는 리터럴 토큰 ───────────────────────────────── */
// data: URI 본문(base64)에는 '.' 이 없으므로 확장자 토큰이 섞여 나오지 않는다.
const tokenRe = /[A-Za-z0-9_\-./]+\.(?:png|gif|webp|jpe?g|svg|ogg|mp3|wav|webm|css|json)\b/g;
const tokens = new Set();
for (const m of html.matchAll(tokenRe)) {
  const t = m[0].replace(/^\.?\//, '');
  if (t.length < 6) continue;
  tokens.add(t);
}

/* ── 3. 동적 폴더 규칙 ─────────────────────────────────────────────── */
const dynDirs = new Set();
// 몬스터 프레임: ENEMY_SPRITE_BASE='monster_assets_v1.5.4/frames' + dir + frame_NN.png
// dir 목록은 Object.entries({crawler:'crawler',spitter:'spitter',broodBeast:'brood-beast'}) 에서 읽는다 (frames/archive 같은 보관 폴더 제외)
{
  const baseM = html.match(/ENEMY_SPRITE_BASE='([^']+)'/);
  const dirsM = html.match(/for\(const \[kind,dir\] of Object\.entries\(\{([^}]+)\}\)\)/);
  if (baseM && dirsM) for (const m of dirsM[1].matchAll(/'([^']+)'/g)) dynDirs.add(baseM[1] + '/' + m[1]);
  else if (html.includes('monster_assets_v1.5.4/frames')) dynDirs.add('monster_assets_v1.5.4/frames');
}
// 캐릭터 액션 시트: *_SHEET_ROOT='assets/characters/reely-xxx-actions/sheets/'
for (const m of html.matchAll(/['"`](assets\/[A-Za-z0-9_\-./]+\/sheets\/)['"`]/g)) dynDirs.add(m[1].replace(/\/$/, ''));
// 보스 드래곤: BOSS_DRAGON_ANIMS 의 folder:'…' + frame_NN.png / fallback:'…gif'
if (html.includes("'assets/red-fire-dragon/'")) {
  for (const m of html.matchAll(/folder:'([^']+)'/g)) dynDirs.add('assets/red-fire-dragon/' + m[1]);
  for (const m of html.matchAll(/fallback:'([^']+\.(?:gif|png|webp))'/g)) tokens.add('assets/red-fire-dragon/' + m[1]);
}

// 타일 리소스: TILE_RESOURCE_ROOT='tunnel_crew_tile_resources_v1' + '/' + biome + '/…'
// (확장자 없는 루트 상수라 토큰 스캔에 안 걸림 → 바이옴 폴더 아래에서 접미사 토큰으로 매칭)
const suffixDirs = new Set();
{
  const rootM = html.match(/TILE_RESOURCE_ROOT='([^']+)'/);
  const biomeM = html.match(/for\(const biome of \[([^\]]+)\]\)/);
  if (rootM && biomeM) for (const m of biomeM[1].matchAll(/'([^']+)'/g)) suffixDirs.add(rootM[1] + '/' + m[1] + '/');
}

/* ── 4. 포함 대상 결정 ─────────────────────────────────────────────── */
const included = new Map(); // relPath → reason
const tokenHit = new Set();
for (const f of allFiles) {
  for (const d of dynDirs) {
    if (f.startsWith(d + '/')) { included.set(f, 'dir:' + d); break; }
  }
  if (included.has(f)) continue;
  for (const t of tokens) {
    if (t.startsWith('-') || t.startsWith('_')) continue; // 템플릿 조각(`${role}-playable.png`)은 아래에서 별도 처리
    if (f === t || f.endsWith('/' + t)) { included.set(f, 'token:' + t); tokenHit.add(t); break; }
  }
}
// 템플릿 조각 보완: `${x}-playable.png` 류는 같은 접미사를 가진 파일이 참조된 폴더 안에 있으면 포함
for (const t of tokens) {
  if (!(t.startsWith('-') || t.startsWith('_'))) continue;
  for (const f of allFiles) {
    if (!f.endsWith(t) || included.has(f)) continue;
    const dir = f.slice(0, f.lastIndexOf('/') + 1);
    const inSuffixDir = [...suffixDirs].some(d => dir.startsWith(d) && !dir.slice(d.length).includes('individual'));
    if (html.includes(dir) || inSuffixDir) { included.set(f, 'suffix:' + t); tokenHit.add(t); }
  }
}

const unmatched = [...tokens].filter(t => !tokenHit.has(t))
  .filter(t => !allFiles.some(f => f === t || f.endsWith('/' + t) || f.endsWith(t)));

/* ── 5-a. PNG → 무손실 WebP (용량 다이어트 2번) ─────────────────────────
   원본 자산은 그대로 두고, 인라인할 바이트만 WebP 로 바꾼다. 픽셀 동일(lossless).
   파일마다 PNG 와 WebP 중 작은 쪽을 고른다(타일처럼 WebP 가 더 커지는 파일은 PNG 유지).
   변환 결과는 build/.single-cache/ 에 캐시해 재빌드를 빠르게 한다.  --no-webp 로 끌 수 있다. */
import { spawnSync } from 'node:child_process';
const NO_WEBP = process.argv.includes('--no-webp');
const CACHE = path.join(PROJECT, 'build', '.single-cache', 'webp');
const webpFor = new Map(); // rel → cached webp path
if (!NO_WEBP) {
  const jobs = [];
  for (const rel of included.keys()) {
    if (!/\.png$/i.test(rel)) continue;
    const abs = path.join(ROOT, rel);
    const st = fs.statSync(abs);
    const key = rel.replace(/[^A-Za-z0-9._-]/g, '_') + `__${st.size}_${Math.floor(st.mtimeMs)}.webp`;
    const dst = path.join(CACHE, key);
    webpFor.set(rel, dst);
    if (!fs.existsSync(dst)) jobs.push([abs, dst]);
  }
  if (jobs.length) {
    console.log(`WebP 변환 ${jobs.length}개 (무손실, 캐시 ${CACHE}) …`);
    // -X utf8 + PYTHONUTF8: 한글 경로가 stdin/stdout 을 UTF-8 로 오가게 한다 (기본 cp949 면 경로가 깨져 파일을 못 찾는다)
    const py = spawnSync('python', ['-X', 'utf8', path.join(__dirname, 'webp-lossless.py')],
      { input: JSON.stringify(jobs), encoding: 'utf8', maxBuffer: 64 * 1048576, env: { ...process.env, PYTHONUTF8: '1', PYTHONIOENCODING: 'utf-8' } });
    let res = null; try { res = JSON.parse(py.stdout); } catch {}
    if (py.status !== 0 || !Array.isArray(res)) {
      console.warn('경고: WebP 변환 실패 — PNG 그대로 인라인합니다.', (res && res.error) || py.stderr || py.error || '');
      webpFor.clear();
    } else {
      const failed = res.filter(r => !r.ok);
      if (failed.length) console.warn(`경고: WebP 변환 실패 ${failed.length}개 (PNG 유지):`, failed.slice(0, 3).map(f => path.relative(ROOT, f.src) + ' — ' + f.error).join('; '), failed.length > 3 ? '…' : '');
    }
  }
}

/* ── 5-b. 인코딩 ───────────────────────────────────────────────────── */
const map = {};
let rawBytes = 0, pngBytes = 0, webpBytes = 0, webpUsed = 0, webpKept = 0;
const byReason = {};
for (const [rel, reason] of [...included].sort()) {
  let buf = fs.readFileSync(path.join(ROOT, rel));
  let ext = rel.slice(rel.lastIndexOf('.') + 1).toLowerCase();
  const wp = webpFor.get(rel);
  if (wp && fs.existsSync(wp)) {
    const wbuf = fs.readFileSync(wp);
    if (wbuf.length < buf.length) { pngBytes += buf.length; webpBytes += wbuf.length; buf = wbuf; ext = 'webp'; webpUsed++; }
    else webpKept++;
  }
  const mime = MIME[ext] || 'application/octet-stream';
  map[rel] = `data:${mime};base64,${buf.toString('base64')}`;
  rawBytes += buf.length;
  const k = reason.split(':')[0] + ':' + rel.split('/').slice(0, 3).join('/');
  byReason[k] = (byReason[k] || 0) + buf.length;
}

/* ── 6. HTML 변환 ──────────────────────────────────────────────────── */
let out = html;
const norm = p => p.replace(/^\.?\//, '').split(/[?#]/)[0];
const lookup = p => map[norm(p)] || null;
let replaced = 0;
const missing = new Set();

// 6-a. 외부 CSS <link> → <style> 인라인
out = out.replace(/<link\b[^>]*\bhref=["']((?:\.\/)?assets\/[^"']+\.css)["'][^>]*>/g, (m, href) => {
  const abs = path.join(ROOT, norm(href));
  if (!fs.existsSync(abs)) { missing.add(href); return m; }
  let css = fs.readFileSync(abs, 'utf8');
  const cssDir = path.posix.dirname(norm(href));
  css = css.replace(/url\((['"]?)([^'")]+)\1\)/g, (mm, q, p) => {
    if (/^(data|blob|https?):/.test(p)) return mm;
    const rel = /^(assets|monster_)/.test(p) ? norm(p) : path.posix.normalize(path.posix.join(cssDir, p));
    const d = map[rel]; if (d) { replaced++; return `url("${d}")`; }
    missing.add(p); return mm;
  });
  replaced++;
  return `<style data-inlined-from="${norm(href)}">\n${css}\n</style>`;
});

// 6-b. 마크업/문자열 속의 src="assets/…" · href="assets/…" (용량 다이어트 1번 — 중복 제거)
//      파서가 직접 세팅하는 속성은 setter 를 타지 않는다. 예전엔 여기서 data URI 로 바꿔 맵과 이중으로 들어갔다(키아트 5MB 등).
//      이제는 data-tc-src 로 바꿔 두고 shim 의 MutationObserver 가 요소가 생기는 즉시 맵에서 채운다 → 바이트는 맵에 한 번만.
let deferredAttrs = 0;
out = out.replace(new RegExp(`(\\s)(src|href|poster)=(["'])(${ROOT_RE_SRC}[^"']+?)\\3`, 'g'), (m, ws, attr, q, p) => {
  if (!lookup(p)) { missing.add(p); return m; }
  deferredAttrs++; return `${ws}data-tc-${attr}=${q}${p}${q}`;
});
// 6-c. CSS url(assets/…)
out = out.replace(new RegExp(`url\\((['"]?)(${ROOT_RE_SRC}[^'")]+?)\\1\\)`, 'g'), (m, q, p) => {
  const d = lookup(p); if (!d) { missing.add(p); return m; }
  replaced++; return `url("${d}")`;
});

// 6-d. 코옵 클라이언트 (서버가 내려주는 /coop/client.js) 제거 — 단일 파일에선 서버가 없다
out = out.replace(/<script\b[^>]*\bsrc=["']\/coop\/client\.js["'][^>]*><\/script>/g,
  '<!-- single-file build: /coop/client.js 제거 (LAN 코옵은 START.bat 패키지에서만) -->');

// 6-d1. 메인 메뉴 LAN 코옵 버튼 잠금 — 서버가 없는 단일 파일에서는 눌러도 동작하지 않으므로 잠금 표시로 남긴다
{
  const before = out;
  out = out.replace(
    /<button class="modeBtn coop" id="menuCoop">(<i>[^<]*<\/i>)<b>LAN 코옵<\/b><span>[^<]*<\/span>/,
    '<button class="modeBtn coop locked" id="menuCoop" disabled aria-disabled="true" title="단일 파일 버전에서는 LAN 코옵을 사용할 수 없습니다">$1<b>LAN 코옵 <span class="lockedTag">단일 파일 미지원</span></b><span>서버 포함 패키지(START.bat)에서만 가능</span>');
  if (before === out) console.warn('경고: 메인 메뉴 LAN 코옵 버튼을 찾지 못해 잠금 처리 못 함');
}

// 6-d2. 코옵 안내 카드 문구 — 단일 파일 버전용으로 교체 (없으면 그대로 둔다)
{
  const before = out;
  out = out.replace(
    /<h2>LAN 코옵은 서버 접속이 필요합니다<\/h2>'\s*\+'<p>[\s\S]*?<\/p>'\s*\+'<div class="chAddr">[\s\S]*?<\/div>'/,
    `<h2>단일 파일 버전은 LAN 코옵을 지원하지 않습니다</h2>'
   +'<p>이 파일은 혼자 플레이(솔로 · 무한 · AI 크루 · 관전) 전용입니다.<br>'
   +'LAN 코옵은 서버가 포함된 배포 패키지(<code>START.bat</code>)로 실행하세요.</p>'
   +'<div class="chAddr">코옵 서버가 이미 켜져 있다면 → <b>http://&lt;서버IP&gt;:5188/</b> 로 접속</div>'`);
  if (before === out) console.warn('경고: 코옵 안내 카드 문구를 찾지 못해 원문 유지');
}

// 6-e. shim + 자산 맵 삽입 (첫 <meta charset> 직후, 어떤 스크립트보다 먼저)
const shim = String.raw`
<script id="tcSingleFileShim">
/* ══════════ 단일 파일 빌드 shim ══════════
   assets/… 경로로 들어오는 모든 로드를 window.__TC_ASSETS 맵의 data URI 로 바꿔 준다.
   맵에 없는 경로는 원래 값으로 통과시킨다(서버로 서빙될 때도 그대로 동작). */
(function(){
'use strict';
window.__TC_SINGLE_FILE=true;
function M(){return window.__TC_ASSETS||{};}
function resolve(v){
  if(typeof v!=='string'||v.length>4096)return null;
  if(v.startsWith('data:')||v.startsWith('blob:'))return null;
  var p=v;
  if(/^[a-z][a-z0-9+.-]*:/i.test(p)){try{var u=new URL(p);p=decodeURIComponent(u.pathname);}catch(e){return null;}}
  p=p.replace(/^\.?\/+/,'').split(/[?#]/)[0];
  var m=M();
  return m[p]||null;
}
var ROOT_RE=/(?:\.\/)?(?:assets|monster_assets_v1\.5\.4|tunnel_crew_tile_resources_v1)\//;
function cssResolve(v){
  if(typeof v!=='string'||v.indexOf('url(')<0||!ROOT_RE.test(v))return v;
  return v.replace(/url\((['"]?)([^'")]+)\1\)/g,function(mm,q,p){var r=resolve(p);return r?'url("'+r+'")':mm;});
}
function htmlResolve(h){
  if(typeof h!=='string'||!ROOT_RE.test(h))return h;
  return h.replace(/(\s)(?:data-tc-)?(src|href|poster)=(["'])((?:\.\/)?(?:assets|monster_assets_v1\.5\.4|tunnel_crew_tile_resources_v1)\/[^"']+?)\3/g,
      function(mm,ws,a,q,p){var r=resolve(p);return r?ws+a+'='+q+r+q:mm;})
    .replace(/url\((['"]?)((?:\.\/)?(?:assets|monster_assets_v1\.5\.4|tunnel_crew_tile_resources_v1)\/[^'")]+?)\1\)/g,
      function(mm,q,p){var r=resolve(p);return r?'url("'+r+'")':mm;});
}
function patch(proto,name,fn){
  var d=Object.getOwnPropertyDescriptor(proto,name); if(!d||!d.set)return;
  Object.defineProperty(proto,name,{configurable:true,enumerable:d.enumerable,
    get:function(){return d.get.call(this);},
    set:function(v){d.set.call(this,fn(v));}});
}
var attrFix=function(v){return resolve(v)||v;};
patch(HTMLImageElement.prototype,'src',attrFix);
patch(HTMLMediaElement.prototype,'src',attrFix);
patch(HTMLSourceElement.prototype,'src',attrFix);
patch(HTMLVideoElement.prototype,'poster',attrFix);
patch(HTMLLinkElement.prototype,'href',attrFix);
patch(Element.prototype,'innerHTML',htmlResolve);
patch(Element.prototype,'outerHTML',htmlResolve);
var iah=Element.prototype.insertAdjacentHTML;
Element.prototype.insertAdjacentHTML=function(pos,h){return iah.call(this,pos,htmlResolve(h));};
var sa=Element.prototype.setAttribute;
Element.prototype.setAttribute=function(n,v){
  if(typeof n==='string'&&typeof v==='string'){var k=n.toLowerCase();
    if(k==='src'||k==='href'||k==='poster'||k==='xlink:href'){v=resolve(v)||v;}
    else if(k==='style'){v=cssResolve(v);}}
  return sa.call(this,n,v);
};
var sans=Element.prototype.setAttributeNS;
Element.prototype.setAttributeNS=function(ns,n,v){
  if(typeof v==='string'&&/href$/i.test(String(n)))v=resolve(v)||v;
  return sans.call(this,ns,n,v);
};
var CSD=CSSStyleDeclaration.prototype;
['backgroundImage','background','cssText','maskImage','webkitMaskImage','borderImage','borderImageSource','listStyleImage','content'].forEach(function(k){patch(CSD,k,cssResolve);});
var sp=CSD.setProperty; CSD.setProperty=function(n,v,pr){return sp.call(this,n,cssResolve(v),pr);};
if(window.fetch){var of=window.fetch;
  window.fetch=function(input,init){
    try{var u=(typeof input==='string')?input:(input&&typeof input.url==='string'?input.url:String(input));
      var r=resolve(u); if(r)return of.call(window,r,init);}catch(e){}
    return of.call(window,input,init);
  };}
var oo=XMLHttpRequest.prototype.open;
XMLHttpRequest.prototype.open=function(m,u){var a=Array.prototype.slice.call(arguments);a[1]=resolve(u)||u;return oo.apply(this,a);};
var OA=window.Audio;
function Audio(src){var a=new OA();if(arguments.length&&src!=null)a.src=src;return a;}
Audio.prototype=OA.prototype; window.Audio=Audio;
/* 빌드 시 data-tc-src/href/poster 로 바꿔 둔 마크업 속성을, 요소가 파싱되는 즉시 맵에서 채운다 (맵과 마크업의 이중 인라인 방지) */
var DEF_ATTRS=['src','href','poster'];
function fixEl(el){
  if(!el||el.nodeType!==1||!el.hasAttribute)return;
  for(var i=0;i<DEF_ATTRS.length;i++){var a=DEF_ATTRS[i],d='data-tc-'+a;
    if(el.hasAttribute(d)){var v=el.getAttribute(d);el.removeAttribute(d);el.setAttribute(a,resolve(v)||v);}}
}
function fixTree(root){
  fixEl(root);
  if(root&&root.querySelectorAll){var q=root.querySelectorAll('[data-tc-src],[data-tc-href],[data-tc-poster]');for(var i=0;i<q.length;i++)fixEl(q[i]);}
}
try{
  new MutationObserver(function(recs){for(var r=0;r<recs.length;r++){var ad=recs[r].addedNodes;for(var i=0;i<ad.length;i++)fixTree(ad[i]);}})
    .observe(document.documentElement,{childList:true,subtree:true});
}catch(e){}
document.addEventListener('DOMContentLoaded',function(){fixTree(document.documentElement);});
window.__TC_ASSET_RESOLVE=resolve;
window.__TC_ASSET_REPORT=function(){var m=M(),n=0,b=0;for(var k in m){n++;b+=m[k].length;}return {files:n,base64Chars:b,approxMB:+(b*0.75/1048576).toFixed(1)};};
})();
</script>
`;
const mapScript = `<script id="tcSingleFileAssets">window.__TC_ASSETS=${JSON.stringify(map)};</script>\n`;
/* 메인 메뉴의 .modeBtn.locked 흐림(opacity .28)이 메뉴 전용 규칙에 눌려 코옵 버튼에 안 먹는다 — 단일 파일용으로 확정 */
const singleCss = `<style id="tcSingleFileCss">
#menuCoop.locked,#crewMenu #menuCoop.locked{opacity:.34!important;cursor:not-allowed!important;filter:saturate(.4)}
#menuCoop.locked u{opacity:0!important}
</style>
`;

const metaRe = /<meta\s+charset=["']utf-8["']\s*\/?>/i;
if (!metaRe.test(out)) throw new Error('<meta charset="utf-8"> 를 찾지 못했습니다 — 삽입 지점 확인 필요');
out = out.replace(metaRe, m => m + '\n<meta name="tc-build" content="single-file">\n' + mapScript + shim + singleCss);

// 6-f. 제목에 표기
out = out.replace(/<title>([^<]*)<\/title>/, (m, t) => `<title>${t} · 단일 파일</title>`);

fs.mkdirSync(path.dirname(OUT), { recursive: true });
fs.writeFileSync(OUT, out, 'utf8');

/* ── 7. 리포트 ─────────────────────────────────────────────────────── */
console.log('원본  :', SRC, fmtMB(Buffer.byteLength(html)));
console.log('출력  :', OUT, fmtMB(Buffer.byteLength(out)));
console.log('자산  :', included.size, '개 /', fmtMB(rawBytes), '(인라인 바이트) → base64 ≈', fmtMB(rawBytes * 4 / 3));
console.log('WebP  :', NO_WEBP ? '끔 (--no-webp)' : `${webpUsed}개 무손실 변환 (PNG ${fmtMB(pngBytes)} → WebP ${fmtMB(webpBytes)}) · ${webpKept}개는 PNG 가 더 작아 유지`);
console.log('치환  :', replaced, '건 (CSS url · link→style) · 마크업 src/href 지연 해석', deferredAttrs, '건 (맵 중복 없음)');
const outMB = Buffer.byteLength(out) / 1048576;
console.log(outMB <= 100 ? `용량  : ${outMB.toFixed(1)} MB ≤ 100 MB ✓` : `용량  : ${outMB.toFixed(1)} MB — 100 MB 초과! 다이어트 필요`);
console.log('\n[포함 내역 — 상위 폴더별]');
for (const [k, v] of Object.entries(byReason).sort((a, b) => b[1] - a[1])) console.log('  ', fmtMB(v).padStart(10), k);
console.log('\n[동적 폴더]'); for (const d of dynDirs) console.log('  ', d); for (const d of suffixDirs) console.log('  ', d, '(접미사 매칭)');
if (missing.size) { console.log('\n[치환 실패 — 맵에 없는 경로]'); for (const m of missing) console.log('  ', m); }
if (unmatched.length) { console.log('\n[HTML 에 등장하지만 파일이 없는 토큰] (동적 조립·라벨·주석일 수 있음)'); for (const t of unmatched) console.log('  ', t); }
const notIncluded = allFiles.filter(f => !included.has(f));
const niBytes = notIncluded.reduce((s, f) => s + fs.statSync(path.join(ROOT, f)).size, 0);
console.log('\n[제외된 자산]', notIncluded.length, '개 /', fmtMB(niBytes));
