#!/usr/bin/env node
/**
 * 원본 HTML 에서 밸런스·튜닝 상수를 뽑아 JSON 으로 굳힌다.
 *
 *   node tools/unity-export/dump-tuning.mjs [원본.html] [출력폴더]
 *   기본값: prototype-html/latest/tunnel-crew-infinite-mode-v7.9.2.html  →  unity/TunnelCrew/Assets/_Project/Data/raw/
 *
 * 설계 메모
 * - HTML 전체를 실행하지 않는다. 원본은 canvas·WebGL·AudioContext·DOM 에 의존해
 *   jsdom 으로도 통째로 돌리기 어렵고, 돌릴 이유도 없다.
 * - 대신 `const NAME=...` 선언의 소스 범위만 잘라내 vm 샌드박스에서 순서대로 평가한다.
 *   상수끼리의 참조(예: INF_NODE_SLOTS → INF_PERM_CAPS)는 같은 컨텍스트를 공유해 해결된다.
 * - 함수 프로퍼티(INF_TRAITS 의 ok/a 등)는 호출하지 않고 "[fn]" 으로 직렬화한다.
 *   특성 효과는 자동 추출이 불가능하므로 계획 §5 M4 의 수작업 표에서 다룬다.
 */

import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';

const SRC = process.argv[2] ?? 'prototype-html/latest/tunnel-crew-infinite-mode-v7.9.2.html';
const OUT = process.argv[3] ?? 'unity/TunnelCrew/Assets/_Project/Data/raw';

/**
 * 상수가 참조하는 헬퍼. 값으로 저장하지 않고 컨텍스트에만 올린다.
 * 예: INF_NODE_OFFSETS 는 infNodeRadialOffsets() 를 호출해 만들어진다(11643행).
 */
const PRELUDE = ['infNodeRadialOffsets'];

/** 뽑을 상수. 순서가 곧 평가 순서다(뒤엣것이 앞엣것을 참조할 수 있음). */
const WANTED = [
  // 월드·타일
  'HPT', 'YIELD', 'DUNGEN', 'LAYERS_ST', 'TUN_SIZE',
  // 이동·카메라·조명
  'TE', 'TE_CELL_REF', 'R_SHELLY', 'R_MINION', 'LIT_TUNE', 'OPT',
  // 런타임 튜닝 190필드
  'DEMO',
  // 보스
  'BOSS_TUNE_FALLBACK', 'BOSS_LAB_DEFAULTS', 'BOSS_DRAGON_ANIMS',
  // 무한/행성 원정
  'INF_ROLES', 'INF_PLANET', 'INF_CARDS', 'INF_ESCAPE', 'INF_BOSS_TIER',
  'INF_XP_TABLE', 'INF_XP_WEIGHT', 'INF_XP_FLOOR_CAP',
  'INF_GROWTH_SCALE', 'INF_DOMINANCE_TARGET',
  'INF_BOSS_POWER', 'INF_BOSS_SIZE_MUL', 'INF_BOSS_WALL_HP_MUL',
  // 성장
  'INF_PERM_CAPS', 'INF_NODE_SLOTS', 'INF_NODE_CLUSTERS', 'INF_NODE_GRID', 'INF_NODE_OFFSETS',
  'INF_TRAITS', 'INF_LEGENDS',
  // 유물
  'INF_RELIC_ELEM', 'INF_RELIC_TIER', 'INF_RELICS',
  // 상한
  'RES_MAX', 'CACHE_MAX', 'RUB_MAX', 'RUB_LIFE', 'GORE_MAX',
  // 적
  'KNOCK_DRAG',
];

// ---------------------------------------------------------------- 소스 스캐너

/**
 * `from` 위치부터 균형 잡힌 표현식의 끝(세미콜론 직전)을 찾는다.
 * 문자열·템플릿·주석 안의 괄호는 세지 않는다.
 */
function findExpressionEnd(src, from) {
  let depth = 0;
  let i = from;
  while (i < src.length) {
    const c = src[i];
    const next = src[i + 1];

    // 주석
    if (c === '/' && next === '/') { i = src.indexOf('\n', i); if (i < 0) return -1; continue; }
    if (c === '/' && next === '*') { i = src.indexOf('*/', i + 2); if (i < 0) return -1; i += 2; continue; }

    // 문자열 / 템플릿
    if (c === '"' || c === "'" || c === '`') {
      const quote = c;
      i++;
      while (i < src.length) {
        if (src[i] === '\\') { i += 2; continue; }
        if (src[i] === quote) { i++; break; }
        i++;
      }
      continue;
    }

    if (c === '{' || c === '[' || c === '(') depth++;
    else if (c === '}' || c === ']' || c === ')') depth--;
    else if (c === ';' && depth === 0) return i;
    else if (c === '\n' && depth === 0) {
      // 세미콜론 없이 줄이 끝나는 선언도 있다. 다음 비공백이 새 선언이면 종료.
      const rest = src.slice(i + 1, i + 40).trimStart();
      if (/^(const|let|var|function|\/\*|\/\/)/.test(rest)) return i;
    }
    i++;
  }
  return -1;
}

/** `const NAME=` 선언의 소스(우변)를 찾아 반환. */
function extractDeclaration(src, name) {
  const re = new RegExp(String.raw`(?:^|[\n;])\s*(?:const|let|var)\s+${name}\s*=`, 'm');
  const m = re.exec(src);
  if (!m) return null;
  const valueStart = m.index + m[0].length;
  const end = findExpressionEnd(src, valueStart);
  if (end < 0) return null;
  return {
    line: src.slice(0, m.index).split('\n').length,
    expr: src.slice(valueStart, end).trim(),
  };
}

// ---------------------------------------------------------------- 직렬화

function serialize(value, seen = new WeakSet()) {
  if (value === null || value === undefined) return null;
  const t = typeof value;
  if (t === 'number') return Number.isFinite(value) ? value : String(value); // 1e9, Infinity 보존
  if (t === 'string' || t === 'boolean') return value;
  if (t === 'function') return '[fn]';
  if (t === 'symbol' || t === 'bigint') return String(value);
  if (value instanceof Set) return [...value].map(v => serialize(v, seen));
  if (value instanceof Map) return Object.fromEntries([...value].map(([k, v]) => [String(k), serialize(v, seen)]));
  if (seen.has(value)) return '[circular]';
  seen.add(value);
  if (Array.isArray(value)) return value.map(v => serialize(v, seen));
  const out = {};
  for (const k of Object.keys(value)) {
    try { out[k] = serialize(value[k], seen); }
    catch (e) { out[k] = `[error: ${e.message}]`; }
  }
  return out;
}

// ---------------------------------------------------------------- 실행

function main() {
  if (!fs.existsSync(SRC)) {
    console.error(`원본을 찾을 수 없다: ${SRC}`);
    process.exit(1);
  }
  const html = fs.readFileSync(SRC, 'utf8');
  // base64 데이터 URI 는 스캔 대상이 아니므로 미리 지워 속도를 올린다.
  const src = html.replace(/data:[a-zA-Z0-9/+.-]+;base64,[A-Za-z0-9+/=]+/g, 'DATA');

  // 원본 상수들이 참조하는 최소한의 전역만 채운 샌드박스.
  const sandbox = {
    Math, JSON, Number, String, Boolean, Array, Object, Set, Map, Date,
    parseInt, parseFloat, isNaN, isFinite,
    console: { log() {}, warn() {}, error() {} },
    window: {}, document: undefined, navigator: undefined,
  };
  const ctx = vm.createContext(sandbox);

  const results = {};
  const meta = { source: path.basename(SRC), generated: new Date().toISOString(), found: [], missing: [], failed: [] };

  // 헬퍼 먼저 올린다.
  for (const name of PRELUDE) {
    const decl = extractDeclaration(src, name);
    if (!decl) { meta.failed.push({ name, line: 0, error: 'prelude 선언을 찾지 못함' }); continue; }
    try { vm.runInContext(`var ${name} = ${decl.expr};`, ctx, { timeout: 5000 }); }
    catch (e) { meta.failed.push({ name, line: decl.line, error: `prelude: ${e.message}` }); }
  }

  for (const name of WANTED) {
    const decl = extractDeclaration(src, name);
    if (!decl) { meta.missing.push(name); continue; }
    try {
      vm.runInContext(`var ${name} = ${decl.expr};`, ctx, { timeout: 5000 });
      results[name] = { line: decl.line, value: serialize(ctx[name]) };
      meta.found.push(name);
    } catch (e) {
      meta.failed.push({ name, line: decl.line, error: e.message });
    }
  }

  // `const A=1, B=2;` 처럼 한 선언에 여러 이름이 붙은 경우, 앞 이름을 평가할 때
  // 뒤 이름도 컨텍스트에 함께 올라온다. (예: `const R_MINION=19, R_SHELLY=25;`)
  for (const name of [...meta.missing]) {
    if (ctx[name] === undefined) continue;
    results[name] = { line: 0, value: serialize(ctx[name]) };
    meta.found.push(name);
    meta.missing.splice(meta.missing.indexOf(name), 1);
  }

  fs.mkdirSync(OUT, { recursive: true });
  for (const [name, data] of Object.entries(results)) {
    fs.writeFileSync(path.join(OUT, `${name}.json`), JSON.stringify(data.value, null, 2) + '\n');
  }
  fs.writeFileSync(path.join(OUT, '_manifest.json'), JSON.stringify(meta, null, 2) + '\n');

  console.log(`원본     : ${SRC}`);
  console.log(`출력     : ${OUT}`);
  console.log(`추출 성공: ${meta.found.length} / ${WANTED.length}`);
  if (meta.missing.length) console.log(`선언 없음: ${meta.missing.join(', ')}`);
  if (meta.failed.length) {
    console.log('평가 실패:');
    for (const f of meta.failed) console.log(`  ${f.name} (line ${f.line}) — ${f.error}`);
  }
}

main();
