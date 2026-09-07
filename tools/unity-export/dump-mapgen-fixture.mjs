#!/usr/bin/env node
/**
 * 원본 genTunnel() 을 격리 실행해 맵 생성 결과를 JSON 으로 덤프한다.
 * C# DungeonGenerator 의 패리티 테스트(계획 §6)가 이 픽스처와 완전 일치해야 한다.
 *
 *   node tools/unity-export/dump-mapgen-fixture.mjs [원본.html] [출력폴더] [--depths 1,2,3]
 *   기본: → unity/TunnelCrew/Assets/Tests/EditMode/Fixtures/
 *
 * 동작
 * - HTML 을 통째로 실행하지 않는다. genTunnel 과 그것이 쓰는 헬퍼의 **소스만** 잘라내
 *   최소 스텁과 함께 vm 에서 돌린다. canvas·WebGL·DOM 은 필요 없다.
 *
 * 원본과 의도적으로 다른 점 (계획 §4 에 명시된 결정)
 * - 원본은 진입 방 선택에 `Math.random()` 을 써서 같은 시드라도 맵이 달라진다.
 *   여기서는 그 호출을 시드 스트림 `R()` 로 바꾼다. **C# 포팅도 동일하게 한다.**
 *   패리티는 "원본과 똑같이 비결정적"이 아니라 "둘 다 같은 시드에서 같은 맵"을 뜻한다.
 *
 * 덤프 범위
 * - cell / entry / exit / relic / cache / lamps / props 까지.
 *   이들은 전부 buried·vegetation·fdec 보다 먼저 확정되므로, 뒤쪽을 생략해도 영향이 없다.
 */

import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { extractDeclaration, extractFunction } from './lib/js-scan.mjs';

const argv = process.argv.slice(2);
const flagIdx = argv.findIndex(a => a === '--depths');
const DEPTHS = flagIdx >= 0 ? argv[flagIdx + 1].split(',').map(Number) : [1, 2, 3];
const positional = argv.filter((_, i) => flagIdx < 0 || (i !== flagIdx && i !== flagIdx + 1));
const SRC = positional[0] ?? 'prototype-html/latest/tunnel-crew-infinite-mode-v7.9.2.html';
const OUT = positional[1] ?? 'unity/TunnelCrew/Assets/Tests/EditMode/Fixtures';

function need(x, what) {
  if (!x) { console.error(`원본에서 ${what} 를 찾지 못했다.`); process.exit(1); }
  return x;
}

function main() {
  const html = fs.readFileSync(SRC, 'utf8');
  const src = html.replace(/data:[a-zA-Z0-9/+.-]+;base64,[A-Za-z0-9+/=]+/g, 'DATA');

  // ---- 필요한 원본 조각만 잘라낸다 -------------------------------------
  const fnGenTunnel = need(extractFunction(src, 'genTunnel'), 'genTunnel');
  const fnSetLayerSize = need(extractFunction(src, 'setLayerSize'), 'setLayerSize');
  const fnApplyDemo = need(extractFunction(src, 'applyDemoToDungen'), 'applyDemoToDungen');
  const cDemo = need(extractDeclaration(src, 'DEMO'), 'DEMO');
  const cDungen = need(extractDeclaration(src, 'DUNGEN'), 'DUNGEN');
  const cD4 = need(extractDeclaration(src, 'D4'), 'D4');
  const cTunSize = need(extractDeclaration(src, 'TUN_SIZE'), 'TUN_SIZE');

  // 진입 방 선택의 Math.random() → R() (위 주석의 결정)
  let genTunnelSrc = fnGenTunnel.source;
  const before = (genTunnelSrc.match(/Math\.random\(\)/g) ?? []).length;
  genTunnelSrc = genTunnelSrc.replace(/Math\.random\(\)/g, 'R()');
  const after = (genTunnelSrc.match(/Math\.random\(\)/g) ?? []).length;

  // ---- 스텁 + 조각을 조립 ---------------------------------------------
  const harness = `
    let CELL = 50;
    let COLS = 6, ROWS = 12, WW = COLS*CELL, WH = ROWS*CELL, BANDROWS = 3;
    let LW = 1920, LH = 1080;
    const P = { bands: [0,1,2,3] };                 // bandOf 는 길이만 쓴다
    const SAVE = { stranded: 0, lv: { mine: 0 } };
    const clamp = (v,a,b) => v<a ? a : v>b ? b : v;
    const teZoomZ = () => 1;
    const stage = () => 3;
    const SOLIDX = t => t === 'rock' || t === 'core';
    const ci = (c,r) => r*COLS + c;
    const cxw = c => c*CELL + CELL/2;
    const cyw = r => r*CELL + CELL/2;
    const bandOf = r => clamp(Math.floor(r/BANDROWS), 0, P.bands.length-1);
    function mul(a){return function(){a|=0;a=a+0x6D2B79F5|0;let t=Math.imul(a^a>>>15,1|a);
      t=t+Math.imul(t^t>>>7,61|t)^t;return((t^t>>>14)>>>0)/4294967296;};}
    function hashSeed(s){let h=2166136261>>>0;
      for(let i=0;i<s.length;i++){h^=s.charCodeAt(i);h=Math.imul(h,16777619);}return h>>>0;}
    function generateVegetation(){ /* 셀 배열을 바꾸지 않는다. 덤프 대상 뒤에 온다 */ }
    const LOS = { reset(){} };

    ${cTunSize.expr ? `const TUN_SIZE = ${cTunSize.expr};` : ''}
    const tunDef = d => TUN_SIZE[Math.min(TUN_SIZE.length-1, Math.max(0, d-1))];
    const tunRooms = (c,r) => Math.max(5, Math.round((c-2)*(r-2)/68));
    const D4 = ${cD4.expr};
    const DEMO = ${cDemo.expr};
    const DUNGEN = ${cDungen.expr};

    const G = {
      mode: 'tunnel', cell: null, dec: null, band: null, hp: null,
      relic: null, cache: [], lamps: [], props: [], buried: [], dust: [],
      fdec: [], gore: [], vegetation: [], healSeeds: [],
      entry: null, exit: 0, exitOpen: false, compDirty: false, tunMeta: null,
      fixed: false, Z: 1, offX: 0, offY: 0, camX: 0, camY: 0,
    };

    ${fnApplyDemo.source}
    ${fnSetLayerSize.source}
    ${genTunnelSrc}

    function __dump(depth){
      genTunnel(depth);
      return {
        depth,
        seed: DEMO.seed,
        cols: COLS, rows: ROWS, cell: CELL,
        entryCell: { c: Math.floor(G.entry.x/CELL), r: Math.floor(G.entry.y/CELL) },
        exit: G.exit,
        exitOpen: G.exitOpen,
        cells: Array.from(G.cell, v => v === null ? '' : v),
        band: G.band.slice(),
        dec: G.dec.slice(),
        relic: [...G.relic].sort((a,b)=>a-b),
        cache: G.cache.map(c => ({ c: Math.floor(c.x/CELL), r: Math.floor(c.y/CELL), kind: c.kind, val: c.val })),
        lamps: G.lamps.map(l => ({ c: l.c, r: l.r })),
        props: G.props.map(p => ({ kind: p.kind, c: Math.floor(p.x/CELL), r: Math.floor(p.y/CELL) })),
        tunMeta: { rooms: G.tunMeta.rooms, exitD: G.tunMeta.exitD, maxD: G.tunMeta.maxD, interior: G.tunMeta.interior },
      };
    }
  `;

  const ctx = vm.createContext({ Math, JSON, Number, String, Array, Object, Set, Map, Uint8Array, Int16Array, Int32Array, console });
  vm.runInContext(harness, ctx, { timeout: 60000 });

  fs.mkdirSync(OUT, { recursive: true });
  const index = [];
  for (const d of DEPTHS) {
    const fx = vm.runInContext(`__dump(${d})`, ctx, { timeout: 60000 });
    const name = `map-${fx.seed}-d${d}.json`;
    fs.writeFileSync(path.join(OUT, name), JSON.stringify(fx));

    const counts = {};
    for (const t of fx.cells) counts[t || '(빈칸)'] = (counts[t || '(빈칸)'] ?? 0) + 1;
    index.push({ file: name, depth: d, rooms: fx.tunMeta.rooms, counts });
    console.log(`depth ${d}: ${fx.cols}x${fx.rows} · 방 ${fx.tunMeta.rooms} · 출구 ${fx.exit} · ` +
      `유물 ${fx.relic.length} · 보급 ${fx.cache.length} · 랜턴 ${fx.lamps.length} · 소품 ${fx.props.length}`);
    console.log(`         타일: ${Object.entries(counts).map(([k, v]) => `${k} ${v}`).join(' / ')}`);
  }
  fs.writeFileSync(path.join(OUT, '_index.json'), JSON.stringify({
    source: path.basename(SRC),
    generated: new Date().toISOString(),
    note: 'genTunnel 의 Math.random() 을 시드 RNG 로 바꾼 사본에서 덤프했다. C# 포팅도 동일하게 한다.',
    mathRandomReplaced: before - after,
    fixtures: index,
  }, null, 2) + '\n');

  console.log(`\nMath.random() → R() 치환: ${before - after}곳`);
  console.log(`출력: ${OUT}`);
}

main();
