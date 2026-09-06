#!/usr/bin/env node
/**
 * Unity 가 실제로 쓰는 아트만 골라 프로젝트로 복사한다.
 *
 *   node tools/unity-export/import-art.mjs [--all]
 *
 * 기본은 M1 에 필요한 것만: 퍼플 바이옴 타일 + 드릴러 8방향 시트.
 * `--all` 은 몬스터·드래곤·거너/스카웃/엔지니어까지 (M3 이후).
 *
 * 설계
 * - 저장소의 원본 자산을 **그대로 두고** 필요한 파일만 복사한다. 227MB 전체를
 *   Unity 로 가져오면 임포트 시간과 저장소가 낭비된다(계획 §7 리스크).
 * - 어떤 파일이 필요한지는 이 스크립트가 소유한다. 목록이 코드에 있어야
 *   원본이 바뀌었을 때 다시 돌리면 된다.
 * - 스프라이트 슬라이스 규격은 함께 나오는 index JSON 에 적는다.
 *   Unity 쪽 에디터 스크립트가 그걸 읽어 TextureImporter 를 설정한다.
 */

import fs from 'node:fs';
import path from 'node:path';

const ALL = process.argv.includes('--all');
const ART = 'unity/TunnelCrew/Assets/Art';

function copy(src, dstDir, dstName = null) {
  if (!fs.existsSync(src)) return null;
  fs.mkdirSync(dstDir, { recursive: true });
  const dst = path.join(dstDir, dstName ?? path.basename(src));
  fs.copyFileSync(src, dst);
  return dst;
}

function readJson(p) {
  return JSON.parse(fs.readFileSync(p, 'utf8').replace(/^﻿/, ''));
}

// ─────────────────────────────────────────────── 타일
function importTiles(report) {
  const manifest = readJson('tunnel_crew_tile_resources_v1/tile_manifest.json');
  const biome = 'purple';
  const entries = manifest.filter(e => e.biome === biome);
  const srcDir = `tunnel_crew_tile_resources_v1/${biome}/individual_50`;
  const dstDir = `${ART}/Tiles/${biome}`;

  let n = 0;
  const index = [];
  for (const e of entries) {
    if (!copy(path.join(srcDir, e.file), dstDir)) {
      report.missing.push(`${biome}/individual_50/${e.file}`);
      continue;
    }
    n++;
    index.push({
      file: e.file,
      type: e.type,
      band: e.band,
      surface: e.surface ?? 0,
      damage: e.damage ?? 0,
      variant: e.variant ?? 0,
      // 원본 tileAtlasIndex(type,damage,band,surface) 와 같은 슬롯 번호
      slot: atlasSlot(e),
    });
  }

  // 오버레이 (암반 측면·아래 그림자·밴드 시임)
  const ovDir = `tunnel_crew_tile_resources_v1/${biome}/overlays`;
  let ov = 0;
  if (fs.existsSync(ovDir)) {
    for (const f of fs.readdirSync(ovDir)) {
      if (copy(path.join(ovDir, f), `${ART}/Tiles/${biome}/overlays`)) ov++;
    }
  }

  // 바닥 시트 (3변형 가로 배치)
  const floor = copy(`tunnel_crew_tile_resources_v1/${biome}/${biome}_floor_sheet_150x50.png`,
                     `${ART}/Tiles/${biome}`);

  fs.writeFileSync(`${dstDir}/tile-index.json`, JSON.stringify({
    biome, cellPixels: 50, count: index.length,
    note: 'slot 은 원본 tileAtlasIndex() 와 같은 번호다. 런타임 조회는 이 번호로 한다.',
    floorSheet: floor ? path.basename(floor) : null,
    floorVariants: 3,
    tiles: index,
  }, null, 2) + '\n');

  report.tiles = { copied: n, overlays: ov, floorSheet: !!floor };
}

/** 원본 tileAtlasIndex(type,damage,band,surface) (2531행) 와 동일한 슬롯 번호. */
function atlasSlot(e) {
  if (e.type === 'rock') return 240 + e.band;
  if (e.type === 'core_top') return 244 + e.band * 6 + ((e.variant ?? 0) % 6);
  const ti = { dirt: 0, stone: 1, ore: 2, gem: 3, crys: 4 }[e.type];
  if (ti === undefined) return -1;
  const damage = Math.min(3, Math.max(0, e.damage ?? 0));
  return ti * 48 + e.band * 12 + ((e.surface ?? 0) % 3) * 4 + damage;
}

// ─────────────────────────────────────────────── 캐릭터
const ROLE_SHEETS = {
  driller:  { dir: 'reely-2851-actions',   prefix: 'reely-2851-',   walk: 11, fall: 16 },
  gunner:   { dir: 'reely-driller-actions', prefix: 'reely-driller-', walk: 14, fall: 11 },
  scout:    { dir: 'reely-1530-actions',   prefix: 'reely-1530-',   walk: 11, fall: 16 },
  engineer: { dir: 'reely-5279-actions',   prefix: 'reely-5279-',   walk: 11, fall: 16 },
};
const DIRS = ['sw', 'w', 'nw', 'n', 's'];   // 좌측 5방향만 원본. se/e/ne 는 런타임 X 반전

function importRole(role, report) {
  const spec = ROLE_SHEETS[role];
  const srcDir = `assets/characters/${spec.dir}/sheets`;
  const repDir = `assets/characters/${spec.dir}/reports`;
  const dstDir = `${ART}/Characters/${role}`;

  const sheets = [];
  for (const dir of DIRS) {
    for (const action of ['walk', 'fall']) {
      const file = `${spec.prefix}${dir}-${action}.png`;
      if (!copy(path.join(srcDir, file), dstDir)) { report.missing.push(`${srcDir}/${file}`); continue; }

      // report JSON 이 셀 크기·피벗·프레임 수의 정답지다
      const repPath = path.join(repDir, `${spec.prefix}${dir}-${action}.report.json`);
      let meta = null;
      if (fs.existsSync(repPath)) {
        const r = readJson(repPath);
        meta = {
          cellWidth: r.output_size?.[0] ?? 224,
          cellHeight: r.output_size?.[1] ?? 224,
          columns: r.sheet_columns ?? 8,
          rows: r.sheet_rows ?? 2,
          pivotInCell: r.pivot_in_cell ?? null,
          frames: (action === 'walk' ? spec.walk : spec.fall),
        };
      }
      sheets.push({ file, direction: dir, action, meta });
    }
  }

  fs.mkdirSync(dstDir, { recursive: true });
  fs.writeFileSync(`${dstDir}/sheet-index.json`, JSON.stringify({
    role,
    note: '좌측 5방향만 원본이다. se/e/ne 는 런타임에 X 반전해서 쓴다(원본 2822행 주석).',
    mirroredDirections: { se: 'sw', e: 'w', ne: 'nw' },
    sheets,
  }, null, 2) + '\n');

  report.characters[role] = sheets.length;
}

// ─────────────────────────────────────────────── 실행
function main() {
  const report = { generated: new Date().toISOString(), tiles: null, characters: {}, missing: [] };

  importTiles(report);
  importRole('driller', report);
  if (ALL) for (const r of ['gunner', 'scout', 'engineer']) importRole(r, report);

  fs.mkdirSync(ART, { recursive: true });
  fs.writeFileSync(`${ART}/_import-report.json`, JSON.stringify(report, null, 2) + '\n');

  console.log(`타일     : ${report.tiles.copied}개 + 오버레이 ${report.tiles.overlays}개` +
              `${report.tiles.floorSheet ? ' + 바닥 시트' : ''}`);
  for (const [role, n] of Object.entries(report.characters))
    console.log(`캐릭터   : ${role} 시트 ${n}장`);
  if (report.missing.length) {
    console.log(`누락 ${report.missing.length}개:`);
    for (const m of report.missing.slice(0, 10)) console.log('  -', m);
  }
  console.log(`출력     : ${ART}`);
}

main();
