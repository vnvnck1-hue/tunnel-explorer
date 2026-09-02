#!/usr/bin/env node
/*
  땅굴 크루 — 단일 HTML 빌더
  ──────────────────────────────────────────────────────────────────
  본선 HTML 이 참조하는 외부 자산(이미지·오디오·CSS)을 전부 data URI 로
  인라인하고, 런타임 shim 으로 Image.src / fetch / innerHTML / CSS url()
  등을 가로채서 자산 맵에서 찾아준다. 본문 게임 코드는 수정하지 않는다.

  사용:  node tools/build-single-html.mjs [원본.html] [출력.html]
  기본:  build/TunnelCrew-v7.8.1/tunnel-crew-infinite-mode-v7.8.1.html
         → build/tunnel-crew-infinite-mode-v7.8.1-single.html

  단일 파일에서 빠지는 것: LAN 코옵(ws 서버)·서버 저장(coop/saves).
*/
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PROJECT = path.resolve(__dirname, '..');
const SRC = path.resolve(process.argv[2] || path.join(PROJECT, 'build/TunnelCrew-v7.8.1/tunnel-crew-infinite-mode-v7.8.1.html'));
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
if (html.includes('monster_assets_v1.5.4/frames')) dynDirs.add('monster_assets_v1.5.4/frames');
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

/* ── 5. 인코딩 ─────────────────────────────────────────────────────── */
const map = {};
let rawBytes = 0;
const byReason = {};
for (const [rel, reason] of [...included].sort()) {
  const buf = fs.readFileSync(path.join(ROOT, rel));
  const ext = rel.slice(rel.lastIndexOf('.') + 1).toLowerCase();
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

// 6-b. 마크업/문자열 속의 src="assets/…" · href="assets/…" (파서가 직접 세팅하는 속성은 setter 를 타지 않으므로 빌드 시 치환)
out = out.replace(new RegExp(`(\\s(?:src|href|poster)=)(["'])(${ROOT_RE_SRC}[^"']+?)\\2`, 'g'), (m, a, q, p) => {
  const d = lookup(p); if (!d) { missing.add(p); return m; }
  replaced++; return a + q + d + q;
});
// 6-c. CSS url(assets/…)
out = out.replace(new RegExp(`url\\((['"]?)(${ROOT_RE_SRC}[^'")]+?)\\1\\)`, 'g'), (m, q, p) => {
  const d = lookup(p); if (!d) { missing.add(p); return m; }
  replaced++; return `url("${d}")`;
});

// 6-d. 코옵 클라이언트 (서버가 내려주는 /coop/client.js) 제거 — 단일 파일에선 서버가 없다
out = out.replace(/<script\b[^>]*\bsrc=["']\/coop\/client\.js["'][^>]*><\/script>/g,
  '<!-- single-file build: /coop/client.js 제거 (LAN 코옵은 START.bat 패키지에서만) -->');

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
  return h.replace(/(\s(?:src|href|poster)=)(["'])((?:\.\/)?(?:assets|monster_assets_v1\.5\.4|tunnel_crew_tile_resources_v1)\/[^"']+?)\2/g,
      function(mm,a,q,p){var r=resolve(p);return r?a+q+r+q:mm;})
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
window.__TC_ASSET_RESOLVE=resolve;
window.__TC_ASSET_REPORT=function(){var m=M(),n=0,b=0;for(var k in m){n++;b+=m[k].length;}return {files:n,base64Chars:b,approxMB:+(b*0.75/1048576).toFixed(1)};};
})();
</script>
`;
const mapScript = `<script id="tcSingleFileAssets">window.__TC_ASSETS=${JSON.stringify(map)};</script>\n`;

const metaRe = /<meta\s+charset=["']utf-8["']\s*\/?>/i;
if (!metaRe.test(out)) throw new Error('<meta charset="utf-8"> 를 찾지 못했습니다 — 삽입 지점 확인 필요');
out = out.replace(metaRe, m => m + '\n<meta name="tc-build" content="single-file">\n' + mapScript + shim);

// 6-f. 제목에 표기
out = out.replace(/<title>([^<]*)<\/title>/, (m, t) => `<title>${t} · 단일 파일</title>`);

fs.mkdirSync(path.dirname(OUT), { recursive: true });
fs.writeFileSync(OUT, out, 'utf8');

/* ── 7. 리포트 ─────────────────────────────────────────────────────── */
console.log('원본  :', SRC, fmtMB(Buffer.byteLength(html)));
console.log('출력  :', OUT, fmtMB(Buffer.byteLength(out)));
console.log('자산  :', included.size, '개 /', fmtMB(rawBytes), '(raw) → base64 ≈', fmtMB(rawBytes * 4 / 3));
console.log('치환  :', replaced, '건 (마크업 src/href · CSS url · link→style)');
console.log('\n[포함 내역 — 상위 폴더별]');
for (const [k, v] of Object.entries(byReason).sort((a, b) => b[1] - a[1])) console.log('  ', fmtMB(v).padStart(10), k);
console.log('\n[동적 폴더]'); for (const d of dynDirs) console.log('  ', d); for (const d of suffixDirs) console.log('  ', d, '(접미사 매칭)');
if (missing.size) { console.log('\n[치환 실패 — 맵에 없는 경로]'); for (const m of missing) console.log('  ', m); }
if (unmatched.length) { console.log('\n[HTML 에 등장하지만 파일이 없는 토큰] (동적 조립·라벨·주석일 수 있음)'); for (const t of unmatched) console.log('  ', t); }
const notIncluded = allFiles.filter(f => !included.has(f));
const niBytes = notIncluded.reduce((s, f) => s + fs.statSync(path.join(ROOT, f)).size, 0);
console.log('\n[제외된 자산]', notIncluded.length, '개 /', fmtMB(niBytes));
