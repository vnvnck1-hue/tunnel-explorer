#!/usr/bin/env node
/**
 * HTML 에 base64 로 박혀 있는 오디오를 파일로 꺼낸다.
 *
 *   node tools/unity-export/extract-embedded-audio.mjs [원본.html] [출력폴더]
 *   기본: prototype-html/latest/tunnel-crew-infinite-mode-v7.9.2.html → unity/TunnelCrew/Assets/Audio/
 *
 * 계획 §4·analysis-03 §10.1: 런타임 외부 오디오 파일은 5개뿐이고 나머지 SFX·BGM 은
 * 전부 HTML 내부 data URI 다. Unity 로 옮기려면 먼저 파일로 굳혀야 한다.
 *
 * 꺼내는 것
 *  - MENU_SFX.bank : 25종 × 변형 1~5개  → Audio/sfx/<id>_<n>.<ext> + sfx-bank.json
 *  - DRILL_SMP.wav : start / loop / rel → Audio/sfx/drill_*.wav
 *  - LOBBY_BGM_DATA / PURPLE_BGM_DATA   → Audio/bgm/*.mp3
 */

import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { extractDeclaration, extractProperty, decodeDataUri } from './lib/js-scan.mjs';

const SRC = process.argv[2] ?? 'prototype-html/latest/tunnel-crew-infinite-mode-v7.9.2.html';
const OUT = process.argv[3] ?? 'unity/TunnelCrew/Assets/Audio';

function write(dir, name, buffer) {
  fs.mkdirSync(dir, { recursive: true });
  const p = path.join(dir, name);
  fs.writeFileSync(p, buffer);
  return { file: name, bytes: buffer.length };
}

function main() {
  if (!fs.existsSync(SRC)) { console.error(`원본을 찾을 수 없다: ${SRC}`); process.exit(1); }
  const src = fs.readFileSync(SRC, 'utf8');

  const sfxDir = path.join(OUT, 'sfx');
  const bgmDir = path.join(OUT, 'bgm');
  const report = { source: path.basename(SRC), generated: new Date().toISOString(), bank: [], drill: [], bgm: [], skipped: [] };

  // ---------------------------------------------------------- 1. SFX 뱅크
  const bankDecl = extractProperty(src, 'bank', src.indexOf('const SMP='));
  if (!bankDecl) {
    report.skipped.push('MENU_SFX.bank 를 찾지 못함');
  } else {
    const ctx = vm.createContext({});
    vm.runInContext(`var __bank = ${bankDecl.expr};`, ctx, { timeout: 10000 });
    const bank = ctx.__bank;

    for (const [id, entry] of Object.entries(bank)) {
      const sources = Array.isArray(entry.src) ? entry.src : [];
      const files = [];
      sources.forEach((uri, i) => {
        const dec = decodeDataUri(uri);
        if (!dec) { report.skipped.push(`${id}[${i}] data URI 아님`); return; }
        const name = sources.length > 1 ? `${id}_${i + 1}.${dec.ext}` : `${id}.${dec.ext}`;
        files.push(write(sfxDir, name, dec.buffer).file);
      });
      report.bank.push({
        id,
        category: entry.cat ?? null,   // AU.vol 의 카테고리 게인 키
        gain: entry.g ?? null,
        pitchJitter: entry.jit ?? null,
        files,
      });
    }
  }

  // ---------------------------------------------------------- 2. 드릴 샘플
  const drillAssign = /DRILL_SMP\.wav\s*=\s*\{/.exec(src);
  if (!drillAssign) {
    report.skipped.push('DRILL_SMP.wav 할당을 찾지 못함');
  } else {
    const start = drillAssign.index + drillAssign[0].length - 1;
    const decl = extractProperty(`x:${src.slice(start)}`, 'x', 0);
    const ctx = vm.createContext({});
    vm.runInContext(`var __d = ${decl.expr};`, ctx, { timeout: 10000 });
    for (const [key, uri] of Object.entries(ctx.__d)) {
      const dec = decodeDataUri(uri);
      if (!dec) { report.skipped.push(`DRILL_SMP.${key} data URI 아님`); continue; }
      report.drill.push(write(sfxDir, `drill_${key}.${dec.ext}`, dec.buffer));
    }
  }

  // ---------------------------------------------------------- 3. BGM
  for (const [varName, outName] of [
    ['LOBBY_BGM_DATA', 'lobby'],
    ['PURPLE_BGM_DATA', 'purple'],
  ]) {
    const decl = extractDeclaration(src, varName);
    if (!decl) { report.skipped.push(`${varName} 선언 없음`); continue; }
    const ctx = vm.createContext({});
    try {
      vm.runInContext(`var __s = ${decl.expr};`, ctx, { timeout: 10000 });
    } catch (e) { report.skipped.push(`${varName} 평가 실패: ${e.message}`); continue; }
    const dec = decodeDataUri(ctx.__s);
    if (!dec) { report.skipped.push(`${varName} data URI 아님`); continue; }
    report.bgm.push(write(bgmDir, `${outName}.${dec.ext}`, dec.buffer));
  }

  // ---------------------------------------------------------- 리포트
  fs.mkdirSync(OUT, { recursive: true });
  fs.writeFileSync(path.join(OUT, 'sfx-bank.json'), JSON.stringify(report.bank, null, 2) + '\n');
  fs.writeFileSync(path.join(OUT, '_extract-report.json'), JSON.stringify(report, null, 2) + '\n');

  const totalFiles = report.bank.reduce((n, b) => n + b.files.length, 0) + report.drill.length + report.bgm.length;
  const totalBytes = [...report.drill, ...report.bgm].reduce((n, f) => n + f.bytes, 0);
  console.log(`원본     : ${SRC}`);
  console.log(`출력     : ${OUT}`);
  console.log(`SFX 뱅크 : ${report.bank.length}종 / 파일 ${report.bank.reduce((n, b) => n + b.files.length, 0)}개`);
  console.log(`드릴     : ${report.drill.map(d => d.file).join(', ') || '없음'}`);
  console.log(`BGM      : ${report.bgm.map(d => `${d.file} (${(d.bytes / 1048576).toFixed(1)}MB)`).join(', ') || '없음'}`);
  console.log(`총 파일  : ${totalFiles}개`);
  if (report.skipped.length) {
    console.log('건너뜀:');
    for (const s of report.skipped) console.log(`  - ${s}`);
  }
}

main();
