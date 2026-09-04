# 땅굴 크루 v7.9.2 — 코어 시뮬레이션 아키텍처 맵 (Unity 이식용)

> 작성: 2026-09-05 · 대상: `tunnel-crew-infinite-mode-v7.9.2.html` (행 번호는 base64 데이터 URI를 제거한 사본 기준이며 원본과 동일)
> 상위 문서: [`../unity-port-plan.md`](../unity-port-plan.md) · 자매 문서: [엔티티·전투·AI](analysis-02-entities-combat-ai.md), [셸·오디오·네트·자산](analysis-03-shell-audio-net-assets.md)
> 메인 스크립트: 약 1259행 ~ 15200행. 이후는 실험실(Projectile Lab / Boss Lab / UI Lab)·AI 크루·관전 모드 등 부속 모듈.

---

## 1. 전역 상태 객체

### 1.1 `G` — 월드/런타임 상태 (5132–5150행)
단일 거대 객체. 모든 시뮬레이션이 여기에 붙어 있다.

| 필드 | 의미 |
|---|---|
| `t`, `rt` | 씬 시간 / 런 누적 시간(초). `rt`는 히트스톱과 무관 |
| `depth` | 현재 층수(1부터) |
| `cell[]` | **타일 배열** `COLS*ROWS`. 값은 `null`(빈칸) 또는 `'dirt'/'stone'/'ore'/'gem'/'crys'/'rock'/'core'` |
| `hp` | `Map<cellIndex, 남은HP>`. 손상된 타일만 지연 생성 |
| `dec[]`, `band[]` | 셀별 데칼 변형(0/1/2), 셀별 밴드 인덱스(0~3) |
| `exit`, `exitOpen`, `entry{x,y}` | 출구 셀 인덱스, 개방 여부, 진입점 월드좌표 |
| `sh{x,y,vx,vy,face,drill,aim}` | 플레이어("Shelly") |
| `camX,camY,offX,offY,Z,fixed` | 카메라 원점·오프셋·줌·고정챔버 여부 |
| `zDyn` | 카메라 줌 스무딩 값 (7382행에서 갱신) |
| `mins[],car[]` | 미니온/캐러밴 — **현재 버전에서는 매 프레임 비워짐**(7276행). 데드 코드 |
| `res[]` | 드롭된 재화 (pulp/bloom), z축 튀는 물리 포함 |
| `cache[]` | 바닥 보급품(`shard`/`pouch`) |
| `buried[]` | 매몰 동료 — 현재는 생성 직후 `got=true` 처리(7277행), 사실상 비활성 |
| `dash{active,vx,vy,t,cd}`, `dashDust[]`, `dustEmit` | 대시 상태 |
| `knock{vx,vy}`, `stunT` | 넉백/기절 |
| `drillBounce`, `rockBounceCd` | 암반 튕김 |
| `drillWarm`, `drillHeat`, `drillHeatLock` | 드릴 예열/과열 |
| `enemies[]`, `eshots[]`, `projectiles[]`, `enemyCd` | 전투 |
| `php`, `phpMax`, `iframes`, `downed`, `reviveT` | 플레이어 HP·무적·다운 |
| `vib`, `nudge` | `Map<cellIndex, {t,amp,dx,dy}>` 타격 진동/밀림 (렌더 전용) |
| `combo`, `comboT`, `pulse`, `tint`, `tintCol` | 연출 게이지 |
| `session`, `smax` | 세션 제한시간 (땅굴 모드에서는 99999로 무력화) |
| `gPulp`, `gBloom`, `nDep`, `nPick`, `nBlk`, `nRes`, `layers` | 런 집계 (`nBlk`=부순 블록, `nRes`=획득 자원 수) |
| `comp`, `compDirty` | `Int16Array` 빈칸 연결성분 ID (6186 `rebuildComp`) |
| `mode` | `'mine'`(원작 채굴) 또는 `'tunnel'`(땅굴 던전) — **월드 생성 분기의 핵심 스위치** |
| `relic` | `Set<cellIndex>` 벽 속 묻힌 유물 |
| `rub[]`, `fdec[]`, `dust[]`, `trail[]`, `bat[]`, `dfall[]`, `gore[]`, `mote[]` | 파편/데칼/먼지/자취/박쥐/낙진/혈흔/광입자 |
| `tunMeta`, `lamps[]`, `props[]`, `vegetation[]`, `healSeeds[]`, `hazards[]` | 던전 메타·랜턴·소품·식생·회복씨앗·환경위험 |

### 1.2 그리드 전역 (5131행 — G 바깥에 별도로 존재)
```js
let COLS=6, ROWS=12, WW=COLS*CELL, WH=ROWS*CELL, BANDROWS=3;
let CELL=50;                                    // 2034행
```
헬퍼 (5156–5161행):
```js
ci(c,r)=r*COLS+c;  inB(c,r);  solid(c,r)=inB&&!!G.cell[ci];
cxw(c)=c*CELL+CELL/2;  cyw(r)=r*CELL+CELL/2;
toCell(x,y)=[floor(x/CELL),floor(y/CELL)];
bandOf(r)=clamp(floor(r/BANDROWS),0,P.bands.length-1);
```

### 1.3 그 밖의 전역
| 이름 | 행 | 역할 |
|---|---|---|
| `SCENE` | 5130 | `'title'/'depths'/'village'/'sleep'/'intro'/'report'/'upgrade'/'record'/'projectileLab'` |
| `SAVE` | 2423 | 영구 저장 (localStorage `hio_save_v1`) |
| `CREW` | 10061 | 미션 런 상태 (phase / mission / biome / 목표 / 타이머 / 역할 스킬 쿨다운) |
| `INF` | 11824 | 무한(행성 원정) 모드 런 상태 — 필드 200개 이상 |
| `INF_META` | 11617 | 무한 모드 영구 메타 (localStorage `tc_infinite_meta_v1`) |
| `LOS` | 5164 | 타일 레이캐스트 시야 + 탐색 기록 |
| `FOW` | 1448 | WebGL 조명/포그 파이프라인 (IIFE) |
| `LIT` | 7872 | Canvas2D 알파 라이팅 (벽 그림자·스프라이트 림라이트) |
| `J` | 4878 | "주스" — 파티클·화면흔들림·히트스톱·데미지 텍스트 |
| `FEEL` | 4838 | 캐릭터 스쿼시/대시 트랜스폼·잔상 |
| `DEMO` | 2157–2344 | **런타임 튜닝 파라미터 190여 개** (드릴·LOS·루트·적) |
| `TE` | 2071 | 카메라·이동·대시·조명 시드루프 파라미터 |
| `P` / `PT` / `PT2` | 1267 / 1297 / 1321 | 팔레트 (기본 / 퍼플 땅굴 / 청록 염굴). `applyPal(mode)`(1345)이 `P`를 **제자리에서 덮어씀** |
| `OPT` | 2069 | `{timer,shake,fog,hint,lq,alphaLight}` |
| `VG` | 5153 | 마을 씬 상태 |
| `LIGHTS` | 7536 | 프레임마다 재구축되는 광원 배열 `[x,y,radius,intensity,kind]` |

---

## 2. 메인 루프

### 2.1 구조 (9658–9702행)
```js
let last=performance.now();
function loopStep(now){
 const rawDt=Math.min(.05,(now-last)/1000); last=now;
 const hitStopped=J.hs>0;
 if(hitStopped)J.hs=Math.max(0,J.hs-rawDt);
 const dt=rawDt*(hitStopped ? .055 : 1);   // 히트스톱 = 시간 5.5% 슬로우
 ...
}
function loop(now){ requestAnimationFrame(loop); try{loopStep(now);}catch(err){...} }
requestAnimationFrame(loop);               // 10886행에서 시작
```

**핵심 특성 — Unity 이식 시 주의:**
- **완전한 가변 타임스텝**. 고정 스텝·누산기 없음. `dt` 상한 0.05초(=20fps 하한).
- **히트스톱은 루프를 멈추지 않고 dt에 0.055를 곱함.** `G.rt`는 raw dt로 누적 (7097행).
- Unity로는 `Update()` + `Time.deltaTime`, 히트스톱은 `Time.timeScale` 또는 자체 dt 배율. 물리는 어차피 자체 구현(Rigidbody 미사용)이라 `FixedUpdate` 불필요.
- **자동 품질 강등** (9665–9668): 50프레임마다 fps 측정 → `fps<34`면 `OPT.lq=true`, `fps>52`면 해제. 최대 3회 토글(`FQ.flip<3`).

### 2.2 업데이트 디스패치 (9669–9693)
```
panelOpen() → 월드 정지
CREW.phase && !=='play' → J.step(dt)만 (월드 일시정지)
SCENE==='sleep'   → updateSleep(dt)      (9243)
SCENE==='depths'  → update(dt)           (7096)  ← 코어
SCENE==='village' → updateVillage(dt)    (8652)
그 외             → J.step(dt)
```
렌더는 그 뒤 `cx.setTransform(DPR,0,0,DPR,0,0)` 후 `renderDepths()`(8074) / `renderVillage()` / `renderSleep()` / `renderIntro()`.

### 2.3 `update(dt)` 실행 순서 (7096–7420행) — 이식 시 이 순서 유지 필수
1. 타이머 감쇠 (`G.t`, `flash`, `depCd`, `hintT`, `weaponLock`, `dash.cd`)
2. 입력 → 이동 벡터 `ax,ay` (WASD, `KEY` Set)
3. 조준 보간 `G.aimX/aimY` ← `TE.aimFollow*60*dt`
4. **이동**: 대시 / 드릴바운스 / 일반 중 택1 → `collide(sh, R_SHELLY)`
5. 넉백·기절 처리 (슬라이스 적분)
6. `updateDashDust`, `separatePlayers`
7. **채굴**: 드릴 예열/과열 → 드릴 축 3점 샘플 → `damage(c,r,dv,nx,ny,true)`
8. `tryFireGun()`, `updateProjectiles`, `updateVegetation`
9. 조준 각도 보간 `alerp(sh.aim, aimT, dt*turn)`
10. `updateEnemies`, `updateEnemyShots`
11. 자원(`G.res`) 물리 + 자석 + 획득
12. 보급품(`G.cache`) 물리 + 획득
13. 출구 하강 판정 (mine 모드 전용)
14. `updateCrew(dt)` (10614), `AICREW.update(dt)`
15. **카메라** (동적 줌 → 룩어헤드 → 데드존 → 클램프)
16. `vib`/`nudge` 만료, 콤보, 먼지, 자취
17. `stepRubble`, `stepBats`, `stepDustFall`, `J.step(dt)`

---

## 3. 월드 / 타일 시스템

### 3.1 타일 종류와 상수
```js
const HPT={dirt:80, stone:140, ore:100, gem:100, crys:100, rock:1e9, core:1e9};   // 2060
const SOLIDX=t=>t==='rock'||t==='core';                                          // 2061 파괴 불가
const YIELD={dirt:['pulp',1], stone:['pulp',2], ore:['bloom',1],
             gem:['bloom',3], crys:['bloom',2]};                                 // 2062
```
- `rock` = 맵 외곽 경계, `core` = 맵 내부의 파괴 불가 암반 덩어리(길을 "정리"하게 만드는 장치).
- 실제 HP = `HPT[t] * INF.wallHpMul` (6071–6072행). `wallHpMul`은 지층별 1 / 1.35 / 1.8, 이상지대는 `×1.6^n`.

### 3.2 맵 크기
```js
const LAYERS_ST=[...];               // 2037 — mine 모드 층 크기 (6×10 ~ 12×22)
const layerDef=d=>{...}              // 2043 — c<=6이면 fix=1 (화면 통째 고정 카메라)
const TUN_SIZE=[{c:24,r:20,n:'S'} ... {c:64,r:54,n:'XXL+'}];   // 2051 — 라벨용으로만 사용
```
**주의:** `setLayerSize(d)` (5356행)에서 `G.mode==='tunnel'`이면 `TUN_SIZE`를 무시하고
```js
COLS=DEMO.mapW|0;  // 80
ROWS=DEMO.mapH|0;  // 72
```
즉 **땅굴 던전은 심층과 무관하게 항상 80×72 (= 4000×3600 월드픽셀)**. `TUN_SIZE`/`tunDef`는 `G.tunMeta.label` 표시용 잔존물이다. 청킹·무한 지형은 없고, 층마다 유한 맵을 통째로 새로 생성한다.

`BANDROWS=Math.ceil(ROWS/P.bands.length)` → 72/4 = 18행마다 밴드 전환.

### 3.3 시드 RNG
```js
function mul(a){ ... }              // 2478 — mulberry32
function hashSeed(s){ ... }         // 2369 — FNV-1a
```
- mine 모드 시드: `mul(d*7919+1337+SAVE.runs*131+SAVE.day*77)` (5375행)
- tunnel 모드 시드: `hashSeed(DEMO.seed+'|'+d+'|'+COLS+'x'+ROWS) ^ (d*104729) ^ 0x9e3779b9` (5649–5650)
  - `DEMO.seed='tunnel-891730050'` (2158행)
  - **다만 진입점·출구 후보 선택 일부는 `Math.random()`을 씀** (5744–5746, 5386–5387) → 완전 결정론이 아님. Unity 이식 시 통일 권장.

### 3.4 `genDepth(d)` — mine 모드 생성 (5372–5492행)
1. 15% stone / 85% dirt로 채우고 외곽 `rock`
2. 랜덤 진입점 `(ec,er)` → `carve(ec,er,1.25)`
3. 미리 뚫린 통로 1~2가닥 (각도 랜덤워크, 길이 `ROWS*0.62`)
4. 광맥: `veins = max(3, COLS*ROWS/26)`, 종류 순환 `['ore','ore','ore','gem','crys','ore','gem','crys']`
5. **암반(`core`) 배치 + 봉인 검증**: 목표 `interior/13`칸. 배치 후 flood-fill로 도달 불가 칸이 2개 초과면 롤백 (5409–5439)
6. 출구: 진입점 거리 백분위 `pct`(스테이지별 0.46~0.72)에서 선택, 최소거리 `hypot(COLS,ROWS)*0.42`
7. 매몰 동료·식생·바닥 데칼·먼지 생성 → `LOS.reset()`

### 3.5 `genTunnel(d)` — 땅굴 던전 생성 (5645–6004행) ★ 핵심
`DUNGEN` 설정 (2049행, `applyDemoToDungen()`이 `DEMO`에서 덮어씀):
```js
{rmin:2, rmax:5, blob:55, cw:1, jit:20, loop:30, ca:0, core:14, ore:8, bur:5, cache:13}
```
`tunRooms(c,r)=max(5, round((c-2)*(r-2)/68))` → 80×72이면 약 **80개 방**.

11단계 파이프라인:
| # | 행 | 내용 |
|---|---|---|
| 1 | 5659 | 방 배치 — 2칸 여유 두고 비충돌 랜덤 배치, `rmin~rmax` 크기, `blob%` 확률로 blob형 |
| 2 | 5673 | 방 파내기 — 사각형 또는 타원+랜덤워크(`steps=w*h*0.35`) |
| 3 | 5691 | 연결 그래프 — **Prim MST** + 최근접 우회 간선 `rooms*loop/100`개 |
| 4 | 5710 | 통로 파내기 — 지터 워크(`jit%` 확률로 랜덤 방향), 폭 `cw` |
| 5 | 5729 | 셀룰러 오토마타 `ca`회 (기본 0 = 비활성. `DEMO.birth=4, survive=4`) |
| 6 | 5742 | 연결성 보증 — flood fill, 5칸 미만 고립방은 메움, 나머지는 최근접점끼리 터널 연결 (최대 24패스) |
| 7 | 5791 | 진입점 기준 **BFS 거리장** `dist[]` |
| 8 | 5803 | 암반(`core`) 배치 — 목표 `interior*14%`. `risky()` 판정 후 `reach()`로 봉인 여부 검증, 위험하면 롤백 |
| 9 | 5846 | 타일 확정 + **깊이 가중 광맥** — `depth=nd/maxD`; `u<depth*.34 ? 'crys' : u<depth*.62 ? 'gem' : 'ore'`. 목표 `interior*8%`. 이후 `dirt` 중 `softPct=55%`만 dirt로 남기고 나머지 중 `medPct=22%`를 `stone`으로 |
| 10 | 5885 | 출구 — 거리장 94 백분위 통로 끝에서 **진입점 반대 방향 벽 속 1~2칸**(반드시 파야 열림) |
| — | 5916 | 묻힌 유물 `bur=5`개 → `G.relic` Set (통로 인접 벽 셀) |
| — | 5927 | 바닥 보급품 `cache=13`개 |
| — | 5937 | 랜턴 `lampCount=6` (최소 간격 `min(W,H)*0.28`), 소품 `min(28, max(8, open*0.04))`개 |
| 11 | 5981 | 매몰 동료 / 진입점 반경 2.1칸 강제 개방(`ensurePath`) / 데칼 / `LOS.reset()` |

### 3.6 바이옴 / 깊이 밴드
- **밴드**(`G.band[k]`): 순수 시각 요소. `bandOf(r)` = 세로 4구간, `P.bands[0..3]` 색상 + 밴드 경계 시임(8254행).
- **바이옴**: `CREW_BIOMES` (10187행) — `purple`(위험 없음) / `brine`(염수 장판, `setupCrewBiome` 10261에서 10개 배치, 접촉 시 0.42초마다 3 데미지).
- **지층(strata)**: 무한 모드의 실질 난이도 밴드 → §6 참조.

### 3.7 채굴 — `damage(c,r,d,hx,hy,quiet)` (6068–6162행) ★
```js
if(!solid(c,r) || SOLIDX(G.cell[ci(c,r)])) return false;
let h = G.hp.has(k) ? G.hp.get(k) : HPT[t]*hpMul;  h -= d;
if(h<=0){ G.cell[k]=null; G.hp.delete(k); G.nBlk++; G.compDirty=true; LOS.markDirty(); ... }
else G.hp.set(k,h);
```
파괴 시 부수효과 (전부 이 한 함수 안에서):
- 콤보 갱신, 파티클 7종(`J.after/flash/ring/chunks/burst/smoke/spikes/square/star`), 파편 `spawnRubble`
- 4방향 인접 타일 `G.nudge` 밀림 (5.6px, 0.19s)
- **PERF_BRK_BURST** (6066–6067, 6092): 180ms 창 안 파괴 개수를 세어 4개 초과 시 사운드 생략, 3개 초과 시 히트스톱 생략, 파티클 35%로 절감. 폭발 연쇄 프레임 드랍 방지책.
- 출구 셀이면 `G.exitOpen=true` (6115)
- `G.relic` 셀이면 유물 드롭 (6120)
- 광석 16% / 일반 4% 확률로 보급품 드롭 (6134)
- `YIELD[t]` → `spawnLootBurst` (6144)
- `infOnBlockBroken(t,x,y)` 훅 (6079) — 무한 모드 XP·장악도·특성 연쇄

**드릴 파이프라인** (7203–7266행):
```js
const DRILL_TIP = R_SHELLY*2.55;         // 63.75px (2083)
samples = [[DRILL_TIP*reach,1],[×0.72,.85],[×0.45,.55]];
// 각 샘플점에서 targets(px,py,1)로 최근접 파괴가능 타일 → 거리<CELL*0.62 인 것 중 최근접 선택
dv = shelDps() * DRILL_DMG() * digM * drillWarmMul() * im * focusMul * dt;
damage(c, r, dv, nx, ny, true);
```
- `shelDps() = 88 + 12*SAVE.lv.mine` (2463행)
- `DRILL_DMG() = DEMO.drillDmg = 2` (2170, 2345) → 기본 DPS ≈ 176
- `drillWarmMul()` (2347): `min + (1-min)*t^curve`, `warmMin=0.2`, `warmTime=1.44s`, `curve=1.35`, `decay=3.49/s`
- 과열: `heatBuild=0.13/s`, `heatCool=0.45/s`, 100% 도달 시 `heatLock=2.43s` 잠금 + warm×0.35
- 타격 간격 `drillHitInterval() = 0.06 / max(0.08, warmMul)` (2355)
- 방향 전환 저항: 채굴 중 `DRILL_TURN_DIG=2.5`, 평상시 `DRILL_TURN_IDLE=12` (2084–2085)
- 파괴 불가(`rock`/`core`) 조준 시 **반동**: `drillRockBounce=340px/s`, `Dur=0.16s`, `Cd=0.38s` (7254–7264)

### 3.8 타일 스프라이트 / 아틀라스 (2502–2600행)
```js
TILE_RESOURCE_ROOT='tunnel_crew_tile_resources_v1';
// 바이옴별: {biome}_strict_atlas_master_1600x1700.png (16열 아틀라스, 타일 100px)
//           {biome}_floor_sheet_150x50.png (바닥 3변형)
//           overlays: core_bottom_shadow_50x11, core_side_b0..b3_50x13, seam_0..2_50x6
function tileAtlasIndex(type,damage,band,surface){    // 2531
 if(type==='rock') return 240+band;
 if(type==='core') return 244 + band*6 + surface%6;
 ti = {dirt:0,stone:1,ore:2,gem:3,crys:4}[type];
 return ti*48 + band*12 + (surface%3)*4 + clamp(damage,0,3);
}
```
→ **타일 = 종류(5) × 밴드(4) × 표면변형(3) × 손상단계(4) = 240 슬롯 + rock 4 + core 24.**
손상단계 `db = min(3, round((1-hp/full)*3))` (8170행).
`blockSprite()` (2552)는 `_BS` 캐시에 셀 크기(`CELL*DPR`)로 굽고, 아틀라스 미로드 시 절차적 폴백 드로잉.

Unity로는 그대로 스프라이트 아틀라스 + Tilemap 또는 커스텀 메시로 매핑 가능. `_BS` 캐시는 `CELL` 변경(`setDemoCell`, 2381) 시 전체 무효화된다.

---

## 4. 포그 오브 워 / 조명 / 시야

세 개의 독립 시스템이 겹쳐 있다.

### 4.1 `LOS` — 타일 레이캐스트 (5164–5353행)
**데이터 구조:**
```js
explored : Uint8Array(COLS*ROWS)   // 영구 탐색 기록
visible  : Uint8Array(COLS*ROWS)   // 이번 프레임 가시
pixels   : Uint8Array(COLS*ROWS*4) // R=가시, G=기억 감쇠, B=합성, A=255 → WebGL 텍스처
```
**알고리즘** — `compute(wx,wy)` (5221):
1. 캐시 체크: `(dirty, lastC, lastR, range, rays, mem, bossKey)` 전부 동일하면 스킵
2. `visible.fill(0)`, 플레이어 셀 + 8이웃 강제 가시
3. `rays=360`개 (`DEMO.losRays`) 각도로 `range=19`타일 (`DEMO.losRange`) Bresenham 레이캐스트 — `cast()` (5206). **벽 타일은 표시 후 차단** (`solid(x,y)` → return)
4. 추가 시야원 합산:
   - 코옵 피어 (`COOP_peersXY`)
   - AI 크루 (`AICREW.visionXY`)
   - **스카웃 플레어** (`G.lamps` 중 `flare`) — 벽 안에 있으면 인접 빈칸으로 스냅, 반경 `L.visionRange`, 레이 `clamp(range*16, 56, 112)`
   - 크루 전원 근접 시야 — 반경 5칸, 72 레이
   - **보스 시야원** — `visOnly=true`로 `explored`는 남기지 않음 (5289–5302)
5. 픽셀 버퍼 작성 (5303–5330):
   ```js
   R = visible ? 255 : 0
   G = explored ? round(exp * (0.30 + 0.70*fade²)) : 0    // exp = losExplored*255 = 74
   fade = 1 - sqrt(d²)/mem                                 // mem = losMemory = 11 타일
   ```
   즉 **멀어진 탐색 지역도 30% 기억 농도 유지** — 완전 검정 절단 방지.

**조회 API:** `seenTile(c,r)`, `softSeenTile(c,r,pad)` (반경 pad 안에 하나라도 보이면 true — 렌더 컬링용), `visibleAt(x,y)`, `seenAt(x,y)`.

관련 튜닝 (2285–2291행):
```js
losOn:true, losRange:19, losRays:360, losExplored:0.29, losMemory:11
```

### 4.2 `FOW` — WebGL 조명 파이프라인 (1448–1894행)
`#fogGL` 캔버스 + WebGL1. 두 단계:

**A. 라이트맵 마스크 (오프스크린 FBO)** — `stamp()` (1714)
- 해상도 `LIGHTMAP_SCALE = 0.65` × DPR
- 각 광원마다 쿼드 1장. 프래그먼트 셰이더가 감쇠 + **노멀맵(64×64 fBm 절차 생성, 1478–1491)** + **Bayer 4×4 디더** + 원뿔(손전등) 처리
- 블렌드: `EXT_blend_minmax` 있으면 **RGB는 가산, A는 MAX** (겹치는 광원 색은 섞이고 세기는 최대값)
- 광원 예산: `MAX_FX_LIGHTS=6`, `MAX_LAMP_LIGHTS=8` (1493–1494). 우선순위+거리 정렬 후 상위 N개만.

**B. LX 합성 레이어** — `composite(shx,shy)` (1752–1894)
`u_layer` 0~3의 4개 풀스크린 패스를 Canvas2D 블렌드 모드로 스테이지에 합성:
| layer | 이름 | 역할 |
|---|---|---|
| 0 | lightmap | 어둠색 → 광원색, 알파 = 불투명도 |
| 1 | contrast | 회색 0.5 중립, 어둠 압축 / 빛 채도·대비 상승 |
| 2 | zone | 구역 앰비언스 틴트 |
| 3 | core | 핫코어 — `smoothstep(threshold, +softness, lit)` 중심만 |

LOS는 여기서 `u_losTex`로 들어가고, **`updateLosVisual()`(1689)이 rise 0.16s / fall 0.38s 지수 보간으로 시야 전환을 부드럽게** 한다.

플레이어 조명 (1783–1792): 캐릭터마다 앰비언트 + 손전등(본체 + 원뿔). 시점 캐릭터 100%, 나머지 크루 85%.
```js
ambient:298, flashRange:468, halfAngle:28°, heightRatio:0.25, nStrength:0.04,
fogDensity:0.65, lightSteps:16, softMask:true, breathe:true    // TE, 2075-2076
```

LX 설정은 localStorage `tc_lx_v791c`에 저장 (22254행).

### 4.3 `LIT` — Canvas2D 알파 라이팅 (7872–8073행)
셰이더 없이 합성만으로:
- **`spr()`** — 스프라이트 실루엣에 방향성 그늘(multiply) + 하이라이트(screen) + 림라이트. `destination-in`으로 원본 실루엣 재클리핑.
- **`beginWalls(cx)` / `endWalls()`** — 벽을 오프스크린 캔버스에 그린 뒤, 광원 중심 방사 확대/축소 차분으로:
  - 확대본 − 원본 = **캐스트 그림자**
  - 원본 − 확대본 = **광원 쪽 모서리 림**
  - 원본 − 축소본 = **광원 반대쪽 벽면 음영**
  → 비용이 타일 수와 무관 (광원당 풀스크린 drawImage 몇 장).

```js
const LIT_TUNE={ spRim:0.31, spRimPx:4.2, spLite:0.67, spShade:1.00, spAmb:0.80,
  wRim:0.36, wRimE:0.038, wShade:1.20, wShadeE:0.038,
  wShadow:1.20, wShadowE:0.133, wSteps:3, wRes:0.25, wLights:2 };   // 7865
```
`OPT.lq`면 광원 1개·스텝 1회로 강등.

**Unity 대응:** LIT는 2D 라이트(URP Light2D) + 노멀맵으로 대체 가능. FOW의 LOS 텍스처는 R8/RG8 텍스처 + 커스텀 셰이더가 자연스럽다.

### 4.4 폴백 — `drawDarkness()` (7478행)
WebGL 불가 시 저해상도(0.36~0.46배) 캔버스에 라디얼 그라디언트 스프라이트(`LS_PX=192`)를 3개 로브(`LOBE`, 7468)로 스탬프.

---

## 5. 플레이어 이동 & 물리

### 5.1 상수
```js
const R_SHELLY=25, R_MINION=19;                                    // 2067
const TE={ zoom:6.01, followSpeed:0.07, lookAhead:25, deadzone:20,
           moveSpeed:67, aimFollow:0.18,
           dashDist:10, dashDur:0.11, dashCd:0.91, ... };          // 2071
const TE_CELL_REF=9;
const teWorld = v => v*(CELL/TE_CELL_REF);                         // 2079 → ×5.556 (CELL=50)
const teMovePx = () => teWorld(TE.moveSpeed)*(1.15/max(0.85,sqrt(TE.zoom)));   // 2081
const teZoomZ  = () => TE.zoom*(TE_CELL_REF/CELL);                 // 2082 → 1.082
```
**실제 이동 속도** ≈ `67×5.556 × (1.15/2.4515)` ≈ **174.6 px/s** ≈ 3.5 타일/초.
**대시**: 거리 `10×5.556 = 55.6px`, 시간 0.11s → **505 px/s**, 쿨다운 0.91s.

`DEMO.playerHp = 181`, `playerIFrame = 0.59` (2342–2343).

### 5.2 그리드 충돌 — `collide(o, rad)` (6166–6183행)
```js
for(let it=0; it<3; it++){                    // 3회 반복 해소
  3×3 이웃 셀 순회 → solid이면
  가장 가까운 점 nx,ny = clamp(o.x, bx, bx+CELL)
  d = hypot(o.x-nx, o.y-ny)
  if(d<rad) o.x += dx/d*(rad-d), o.y += dy/d*(rad-d)
}
o.x = clamp(o.x, rad+2, WW-rad-2);            // 월드 경계
// 그래도 벽 속이면 7×7 이웃 중 최근접 빈칸으로 35% 보간 탈출
```
원-AABB 밀어내기. Unity에서는 CircleCollider2D + Tilemap Collider로 대체 가능하지만, **35% 보간 탈출 로직**은 벽이 실시간으로 생겨나는 이 게임 특유의 안전장치라 유지 권장.

### 5.3 대시 (1904–1912, 7121–7134행)
```js
function tryDash(){
 if(SCENE!=='depths'||G.dash.active||G.dash.cd>0) return;
 let dx=G.dirx, dy=G.diry;                    // 마지막 이동 방향
 if(hypot(dx,dy)<.2){ dx=cos(sh.aim); dy=sin(sh.aim); }   // 정지 중이면 조준 방향
 const dist=teWorld(TE.dashDist)*crewDashMul(), dur=TE.dashDur;
 G.dash={active:true, vx:dx*dist/max(.04,dur), vy:..., t:dur};
 G.dash.cd = TE.dashCd/(mul>1.2?1.15:1);
}
```
업데이트 시 **4px마다 서브스텝 분할**(터널링 방지, 7123), 축별로 막히면 그 축 속도만 0.
역할별 배율: driller 1.0 / scout 1.4 / engineer 1.0 / gunner 0.9 (`CREW_ROLES`, 10046).

### 5.4 넉백 (7153–7167) — 최대 14 슬라이스, `exp(-KNOCK_DRAG*dt)` 감쇠, 8px/s 미만 시 0.

### 5.5 카메라 (7364–7402행) ★
```js
// ① 동적 줌
const baseZ = teZoomZ();          // 1.082
const zIn   = baseZ*1.35;         // 1.461 — 기본은 35% 더 줌인
let zNeed = zIn;
for(AI 크루 m) zNeed = min(zNeed, min(LW*.40/|dx|, LH*.40/|dy|));   // 화면 80% 안에 들어오는 최대 줌
G.zDyn += (clamp(zNeed, baseZ, zIn) - G.zDyn) * min(1, dt*2.4);
G.Z = G.zDyn;

// ② 룩어헤드 — 조준 방향으로 teWorld(25)=139px
const tx = sh.x + cos(sh.aim)*look,  ty = sh.y + sin(sh.aim)*look;

// ③ 데드존 + 추종
const dead = teWorld(20) = 111px;
if(dist > dead){
  pull = dist - dead;
  camC += (camC + n*pull - camC) * min(1, TE.followSpeed*60*dt);    // followSpeed=0.07
}

// ④ 클램프 — tcClampCamera (9628)
padX = min(vw*.18, teWorld(110)=611);   // 월드 가장자리 여유
G.camX = clamp(x, -padX, max(-padX, WW-vw+padX));
```
카메라 중심은 화면 **세로 42% 지점**(`camY + vh*.42`) — 상하 비대칭.
월드→스크린: `w2s(x,y) = [(x-camX)*Z+offX, (y-camY)*Z+offY]` (7423행).

**고정 챔버 모드** (`G.fixed`, mine 모드 6열 이하 층): 맵 전체가 화면에 들어가도록 `Z=clamp(min((LW-22)/WW,(LH-140)/WH), .5, 1.6)`, 카메라 정지 (5368행).

---

## 6. 런 구조

세 가지 런 모드가 같은 `G`/`update()`를 공유한다.

### 6.1 `startIncursion(mode)` (6039–6060행) — 공통 진입
```js
G.mode = (mode==='tunnel') ? 'tunnel' : 'mine';
applyPal(G.mode);  SP_L=null; GSPR=null;        // 팔레트·조명 스프라이트 무효화
if(tunnel){ SAVE.tunRuns++; G.smax=99999; G.session=G.smax; }   // 세션 타이머 무력화
// 모든 런타임 배열 초기화 → enterDepth(1) → SAVE.runs++ → SCENE='depths'
```

### 6.2 `enterDepth(d)` (6005–6029행)
`genDepth(d)` → 음악 전환 → 최고기록 갱신 → 플레이어를 `G.entry`로 이동 → `ensureSpawnSafe` → 카메라 초기화 → `LOS.reset(); LOS.compute()` → 연출.

### 6.3 미션 모드 (`CREW`) — 10046–10730행
**흐름:** `menu → mission → biome → role → play → result`

`startCrewMission(roleId)` (10493):
```
startIncursion('tunnel') → setupCrewMissionGoals() → setupCrewBiome()
→ spawnCrewDrone(roleId) → crewShow('play') → startCrewTutorial()
```

**미션 3종** (`CREW_MISSIONS`, 10056; 목표 판정은 `updateCrewMission` 10356):
| id | 목표 | 판정 |
|---|---|---|
| `harvest` | `goalNeed=12` 광물 채취 | `CREW.goalHave = G.nRes` |
| `recover` | 유물 1개 운반 | `pickFarOpenSpot(CELL*16)`에 배치, 32px 이내 접근 시 획득. 쓰러지면 드롭(`dropCrewRelic`) |
| `purge` | 엘리트 1기 처치 | HP `enemyHp*4.2`, 반경 `×1.55`인 엘리트 스폰 |

**탈출:** `CREW.canEscape && hypot(sh - G.entry) < 48` → `endCrewMission('win')` (10664행).
**시간 제한:** `CREW.timeMax=420초`, `timeLeft<=0` → `'timeout'` (10632).
**실패:** `G.php<=0` → `endCrewMission('fail')`. 단 구조 가능한 동료가 있으면 `playerEnterDowned()`로 전환 (10530).
**정산 점수** (10931행): `pulp*5 + bloom*25 + relic*500 + round(timeLeft*3)`; 랭크 A>1200, B>650, C.

**역할 스킬** (`crewSkillQ` 10562 / `crewSkillE` 10599):
- driller Q: `CREW.breachT=0.55` → `updateCrew`(10633)에서 조준 축 따라 3칸까지 `shelDps()*2*3.0*dt` 파괴
- scout Q: 플레어 램프 push (`rad=lampRadius*1.25`, `ttl=18`, `visionRange=3`)
- engineer Q: 조준 축 4칸 발판 + 벽 제거 / E: 터렛 (`life=22`, `cd=0.42`, 사거리 190)
- gunner Q: `shieldT=2.8` + `iframes=2.8`

### 6.4 무한 / 행성 원정 모드 (`INF`) — 11614–15200행
**행성 정의** (`INF_PLANET`, 12908행):
```js
strata:[
 {name:'표층 지대', wallHp:1,    enemyHp:1,   dominance:.22},
 {name:'균열 지대', wallHp:1.35, enemyHp:1.4, dominance:.26},
 {name:'중심부',    wallHp:1.8,  enemyHp:1.9, dominance:.30}
],
abyss:{ wallHpGrowth:1.6, enemyHpGrowth:1.55, dominance:.34 }   // 이상지대(4층~) 심층당 배율
```
```js
infWallHpMulFor(d)  = d<=3 ? strata[d-1].wallHp  : strata[2].wallHp  * 1.6^(d-3);
infEnemyHpMulFor(d) = d<=3 ? strata[d-1].enemyHp : strata[2].enemyHp * 1.55^((d-3)*0.5);
const INF_GROWTH_SCALE=.5;     // 11817
```

**층 진행 = "장악도(dominance)"**:
```js
INF.totalBreakable = G.cell.filter(파괴가능).length;              // 12507
if(INF.floorBroken / INF.totalBreakable >= infDominanceTarget()) infSpawnBoss();   // 12570
```
즉 **해당 층의 파괴 가능 블록을 22~34% 부수면 보스가 등장**. 보스 처치 → 휴식 모달 → `infNextDepth()` (13543) → `INF.depth++` → `enterDepth` → `infInitFloor()`.

**동적 위협 스케일** (12226–12229행) — 시간·진행도 기반:
```js
infThreatValue() = min(9, 1 + 0.5*( (depth-1)*.35 + floorTime/90 + floorBroken/28 ));
infEnemyCap()    = min(64, 18 + floor(0.5*(floorBroken/3 + floorTime/15) + threat*1.5));
infSpawnInterval()= max(.62, 5.2/(1 + (threat-1)*.55));
infSpawnBurst()  = min(5, 1 + floor((threat-1)/1.75));
```
추가로 블록을 부술 때마다 (12552행):
```js
INF.spawnDebt += 0.30 + min(0.42, floorBroken*0.006);
G.enemyCd = min(G.enemyCd, max(0.35, 2.1 - floorBroken*0.018));
```
→ **파면 팔수록 적이 빨리 몰려온다.** 이식 시 핵심 긴장 루프.

**레벨업 / 특성 카드** (`INF_CARDS`, 12937):
```js
targetLevel:10,  need:{base:30, linear:10, quad:2.4},   // xpNeed = 30 + 10L + 2.4L²
rerollStart:1, rerollPerStratum:1, rerollCap:2
```
XP 테이블 (12585): `dirt:1, stone:2, rare:4, crack:8, crackCore:12, kill:4, killBoss:45, turretKill:5, gridUptime:2`
역할별 가중치 (12594) — 드릴러는 dig 1.00 / combat 0.65, 거너는 반대. 시간 트리클 XP는 층당 `INF_XP_FLOOR_CAP=60` 상한.

**탈출** (`INF_ESCAPE`, 13554행):
```js
placeRange:6칸, summonBase:20초, summonPerDepth:7초, summonMax:60초,
clearRadius:1.45칸, boardRange:1.25칸, boardTime:1.2초
```
X키 → 지점 지정 → 도착 대기 → 착륙 시 반경 1.45칸 지형 파괴(13600) → 전원 탑승 → `infEndRun(reason, escaped=true)` (14900).
**코어는 생환해야 보관된다.** 실패 시 영구 노드 `keepRate`만큼만 보존 (14911–14917).

### 6.5 mine 모드 (원작 채굴 루프)
- `sessionFor()` (2466): 스테이지 0/1/2는 `[34,46,62] + 8*lv.hole`초, 이후 `timeOf(lv)=110+22*lv`
- 층 하강: 출구 접촉 0.8초 → `G.session -= DESCEND_COST(4)` → `enterDepth(depth+1)` (7352–7360)
- 하루 종료 → `SCENE='sleep'` → `updateSleep` (9243)이 5초 페이드 후 `SAVE.day++`, `saveGame()`, `SCENE='village'`
- `stage()` (2441): 0=혼자파기 / 1=첫구조 / 2=배치회수 / 3=정상. `SAVE.maxStage`에 래치되어 되돌아가지 않음.

---

## 7. 경제 / 저장

### 7.1 재화
- **필드**: `pulp`(녹색) / `bloom`(분홍) — `YIELD` 테이블로 타일에서 드롭. `G.gPulp`, `G.gBloom`.
- `depthMul() = 1 + 0.25*(G.depth-1)` (6164행) — 획득량 심층 배율.
- **획득 파라미터** (2272–2284행):
```js
lootCount:1, lootGemBonus:1, lootScatter:150, lootScatterRand:90, lootSpread:3.3,
lootPopZ:21, lootPopVz:170, lootGravity:895,
lootPickup:28, lootMagnet:72, lootMagnetDelay:0.8, lootMagnetSpd:7.5
```
자원은 z축 포물선 → 최대 2회 바운스 → 착지 0.8초 후 자석 반경 72px 안이면 끌려옴 → 28px 이내 획득.
상한: `RES_MAX=480`, `CACHE_MAX=160`, `RUB_MAX=300`(수명 `RUB_LIFE=34초`) (5498–5501).
- **코어**(`INF.core`): 무한 모드 전용. 희귀 타일 파괴 시 획득, 생환해야 `INF_META.bankedCores`로 이월.

### 7.2 런 간 업그레이드 — `UPG` (2450–2472행) — 레거시 mine 모드 전용
| key | 이름 | base | mul | max | 통화 | 효과 |
|---|---|---|---|---|---|---|
| `bond` | 동료애 | [7,3] | 1.72 | 10 | bloom | `kOf(lv)=min(1, .35+.065lv)` |
| `pen` | 선봉대 | [5,1] | 1.55 | 12 | pulp | `capOf(lv)=1+lv` |
| `mine` | 곡괭이 | [5,1] | 1.48 | 14 | pulp | `shelDps()=88+12lv` |
| `hole` | 구덩이 목 | [9,4] | 1.76 | 10 | pulp | `timeOf(lv)=110+22lv` |
```js
costOf(key,lv) = { pulp: round(base[0]*mul^lv * (cur==='pulp'?1:.5)),
                   bloom: round(base[1]*mul^lv * (cur==='bloom'?1:.6)) };
```

### 7.3 무한 모드 영구 노드 (11628–11812행)
단일 트리, 직업 잠금 없음. 10개 군집 × 8슬롯 = **80노드 / 140랭크 / 총 코어 760**.
슬롯 골격 `INF_NODE_SLOTS` (11632): `i(3랭크) → m1·m2(2) → m3·m4(2) → m5·m6(1) → cap(1)`, 비용 `[1,2,3]/[3,5]/[4,7]/[9]/[14]`.
한 런 적용 = 공용 16 + 직업 16 = 32노드.

### 7.4 localStorage 키 전체 목록
| 키 | 행 | 내용 |
|---|---|---|
| `hio_save_v1` (`SKEY`) | 2418 | 메인 세이브 — `saveGame/loadGame/wipeGame` (2424–2439) |
| `tc_infinite_meta_v1` | 11616 | 무한 모드 메타 (bestDepth, bankedCores, unlocks, planets) |
| `tunnel_crew_settings_v1` | 10407 | 오디오·포그·힌트·모션 설정 |
| `tunnel_crew_tut_v1` | 10406 | 튜토리얼 완료 플래그 |
| `tc_boss_lab_params_v2` / `_pinned` / `_prev` | 11831–11833 | 보스 파라미터 (+ HTML 내장 `#bossLabBakedParams` JSON, 11613행) |
| `tc.uiLayout.v1` | 19199 | UI 레이아웃 오버라이드 |
| `tc_lx_v791c` | 22203 | LX 조명 수치 |
| `tc_ping_tut_v1`, `tc_ping_wheel_v1` | 21487, 21491 | 핑 튜토리얼 |

`FRESH()` (2420):
```js
{pulp,bloom,day:1,best:1,runs,rescued,crew,caches,missed,stranded,lostTotal,maxStage,
 tunBest,tunRuns,relics, lv:{bond,pen,mine,hole}, seen:{...}, flag:{}}
```

---

## 8. 렌더링 파이프라인

### 8.1 캔버스 레이어 (3장 + 오프스크린)
| 캔버스 | 컨텍스트 | 해상도 | 용도 |
|---|---|---|---|
| `#stage` | `cx` (2D) | `LW*DPR × LH*DPR` | 월드 |
| `#uiLayer` | `ux` (2D) | 동일 | HUD·데미지 텍스트·틴트 |
| `#fogGL` | WebGL | `OW=LW*DPR` (마스크는 `×0.65`) | 조명·포그 |
| `wallCv`/`tmpCv`/`scA`/`scB` | 2D | 오프스크린 | LIT 벽·스프라이트 라이팅 |
| `_lc` | 2D | `LW*0.36~0.46` | WebGL 폴백 어둠 |

### 8.2 해상도 처리 — `resize()` (9636–9652행)
```js
DPR = Math.min(2, devicePixelRatio||1);        // 상한 2
LW = round(rect.width); LH = round(rect.height);
cv.width = round(LW*DPR); cx.setTransform(DPR,0,0,DPR,0,0);   // CSS픽셀 좌표계로 그림
FOW.resize(LW,LH);
```
`ResizeObserver` + `visualViewport` 이벤트 → `tcQueueResize()`가 rAF로 디바운스 (9653–9657).

### 8.3 `renderDepths()` 드로우 순서 (8074–8370행) ★
```
1  배경 void 색 + 화면 먼지
2  화면흔들림 계산 shx,shy = -J.sdx*J.shake*1.05*cos(J.sph)*exp(-J.sph*.16) + 노이즈
3  collectLights()                                    // 7537 — LIGHTS 배열 재구축
4  cx.save(); translate(offX+shx, offY+shy); scale(Z,Z); translate(-camX,-camY)
   ─── 이하 월드 좌표계 ───
5  챔버 외곽 물결 테두리
6  바닥 — 컬링 범위 [c0..c1, r0..r1] = 카메라 뷰 ±1셀, LOS.softSeenTile(c,r,2)로 필터
7  바닥 데칼(G.fdec) → 혈흔(drawGore) → 자취(drawTrailDirt) → 파편(drawRubble) → J.drawUnder()
8  cx = LIT.beginWalls(cx);          ← ★ 전역 cx를 오프스크린으로 스왑
     블록 타일 (vib/nudge 오프셋 적용, blockSprite로 드로우)
     보스 소환 벽 강조
     천장 요철
     암반(core) — 3패스: 아래 그림자 → 측면(H=CELL*0.26) → 윗면(H만큼 위로)
   cx = _litMainCx;  LIT.endWalls();  ← 그림자·음영·림 3패스 합성
9  밴드 시임 → 출구 X 마커
10 소품 → 랜턴 → 식생 → 회복씨앗 → 매몰동료
11 자원(G.res) → 보급품(G.cache) → 미니온/캐러밴(비활성)
12 조작 힌트 → 광입자 → 낙진 → 대시먼지 → 투사체 → 적 → 적탄
13 drawCrewExtras() → FEEL.drawGhosts()
14 [lxEarly] FOW.composite(shx,shy)    ← LX.sortUnderCrew면 캐릭터보다 먼저 합성
15 Shelly (FEEL.transform() 스쿼시 적용) → 기절/다운 표시
16 AICREW.draw() → COOP_drawPeer() → 박쥐 → J.draw()
   cx.restore();
   ─── 스크린 좌표계 ───
17 [!lxEarly] FOW.composite() 또는 drawDarkness()
18 paintUI(shx,shy)                    // 8409 — ux로 스왑해 HUD 그림
```

### 8.4 컬링
```js
c0 = max(0, floor(camX/CELL)-1);  c1 = min(COLS-1, ceil((camX+LW/Z)/CELL)+1);
r0 = max(0, floor(camY/CELL)-1);  r1 = min(ROWS-1, ceil((camY+LH/Z)/CELL)+1);
```
개별 오브젝트는 `x < camX-CELL*2 || x > camX+LW/Z+CELL*2` AABB 체크 + `LOS.seenAt()`.

---

## 9. 숨은 결합 (Hidden Coupling) — 이식 난이도 목록

**심각도 순:**

### ★★★ 1. `cx` 전역 컨텍스트 스왑
```js
let cx = cv.getContext('2d');            // 1358 — let, 재할당 가능
cx = LIT.beginWalls(cx);  ... cx = _litMainCx;    // 8158, 8251
const prev=cx; cx=ux; ... cx=prev;                // 8413, 8426 (paintUI)
```
100개 이상의 `drawXxx()` 함수가 인자 없이 전역 `cx`를 쓴다. 어떤 함수가 어느 레이어에 그려지는지 호출 컨텍스트로만 결정된다. Unity 이식 시 각 드로우 함수의 **목표 레이어를 일일이 판정**해야 한다.

### ★★★ 2. 그리드 전역 (`COLS/ROWS/WW/WH/CELL/BANDROWS`)
`setLayerSize()`가 이 6개를 동시에 바꾸고, `G.cell/dec/band`, `LOS.explored/visible/pixels`, `G.comp`가 모두 이 크기에 의존한다. `LOS.ensure()`(5174)와 `rebuildComp()`(6188)가 크기 불일치를 감지해 재할당하지만, `_BS` 스프라이트 캐시는 `setDemoCell()`에서만 무효화된다.

### ★★★ 3. `P` 팔레트 in-place 변형
```js
function applyPal(mode){ ... }   // 1345 — PBASE/PT/PT2 중 하나로 P의 키를 덮어씀
```
`P`를 참조하는 모든 코드가 모드 전환에 암묵적으로 반응한다. 또한 `applyPal` 직후 `SP_L=null; GSPR=null`로 구운 조명 스프라이트를 무효화해야 한다 (6042행).

### ★★ 4. `DEMO` 객체 — 개발 패널의 라이브 튜닝 타깃
190여 개 필드가 시뮬레이션 곳곳에서 직접 읽힌다 (`DEMO.drillDmg`, `DEMO.losRange`, `DEMO.enemyHp`, `DEMO.lootMagnet`…). `applyDemoToDungen()`(2374)이 `DUNGEN`으로도 흘려보낸다. `DEMO.mapW/mapH`는 `TUN_SIZE`를 무력화한다. **Unity에서는 ScriptableObject로 승격 권장.**

### ★★ 5. `G.cell` / `G.hp`를 변형하는 지점이 산재
| 위치 | 행 |
|---|---|
| `damage()` | 6078 |
| 엔지니어 Q (발판) | 10587 |
| 드릴러 Q (breachT) | 10640 |
| `infEscapeArrive()` (포트 착륙 반경 파괴) | 13605 |
| `genDepth`/`genTunnel` (전체 교체) | 5477, 5988 |
| `ensureSpawnSafe` | 5584 |
| 보스 벽 소환 `infBossWallMaterialize` | 13316 |

각 지점이 `G.compDirty=true` + `LOS.markDirty()`를 **수동으로** 호출해야 한다. 하나라도 빠지면 시야/길찾기가 어긋난다.

### ★★ 6. 옵셔널 모듈 훅 — `typeof X!=='undefined'` 가드
`update()`와 `damage()` 안에서만도 `OBSERVER`, `INF`, `AICREW`, `COOP_*`, `CREW`, `TC_CRAFT`, `BOSS_LAB`, `LOS`, `FOW`를 매 프레임 존재 여부 검사한다. 실제 정의 위치:
- `window.AICREW = AI` — 16640행
- `window.OBSERVER = OBS` — 19940행
- `COOP_*` 는 자유 함수 (`COOP_onDamage`, `COOP_peersXY`, `COOP_escapeAllAboard` 등)
- `window.TC_CRAFT` — 22050행 (카메라 줌을 잠금)

이들은 **스크립트 로드 순서에 의존**하고, `loopStep`이 try/catch로 감싸져 있어(9697) 예외가 조용히 삼켜진다.

### ★ 7. `CREW.phase` 와 `SCENE` 의 이중 상태 머신
루프 디스패치(9671–9680)가 두 값을 교차 검사한다. `CREW.phase==='play'`면 `DLG.on`을 강제로 false로 만드는 등(9676) 부작용도 있다. `INF.active`가 세 번째 축으로 끼어들어 `SCENE==='depths' && CREW.phase==='play' && INF.active` 같은 조건이 곳곳에 반복된다.

### ★ 8. `J`(주스) 객체
파티클 12종 배열(`p,t,ch,rg,fl,af,sp,sm,bd,sq,sr,dm`) + 화면흔들림 + 히트스톱이 한 객체에 있고, **`J.hs`(히트스톱)가 메인 루프의 dt를 좌우**한다(9661–9663). 시뮬레이션과 연출이 분리되어 있지 않다.

### ★ 9. 죽은 코드
- `G.mins` / `G.car` (미니온) — `update()` 7276행에서 매 프레임 `length=0`
- `G.buried` — 7277행에서 매 프레임 `got=true`
- `TUN_SIZE` / `tunDef()` — 라벨 문자열로만 사용
- `G.session` / `G.smax` — 땅굴 모드에서 99999로 무력화

이식 시 제거 대상이지만, 생성 코드(`genDepth`/`genTunnel`)는 여전히 이들을 채우므로 함께 정리해야 한다.

---

## 10. Unity 이식 권장 분해

| JS | Unity |
|---|---|
| `G.cell/dec/band/hp` | `WorldGrid` (NativeArray<byte> + Dictionary<int,float>) |
| `genDepth`/`genTunnel` | `IMapGenerator` 구현 2종 + `System.Random(seed)` |
| `LOS` | `FogOfWarService` — Job으로 레이캐스트, 결과를 R8G8 Texture2D로 업로드 |
| `FOW` + `LIT` | URP 2D Renderer + Light2D + 커스텀 FullScreen 셰이더 (LX 4레이어) |
| `update()` | `PlayerController` / `MiningSystem` / `LootSystem` / `CameraRig` 로 분해 |
| `collide()` | 유지 (커스텀 원-AABB, Physics2D 미사용) |
| `damage()` | `MiningSystem.Damage()` + `ITileBreakListener` 이벤트로 부수효과 분리 |
| `DEMO`/`TE`/`LIT_TUNE`/`LX` | ScriptableObject 4종 |
| `SAVE`/`INF_META`/`CREW_SETTINGS` | JSON + `Application.persistentDataPath` |
| `J`/`FEEL` | `JuiceService` (VFX Graph + Cinemachine Impulse) |
| `CREW`/`INF` | `RunContext` 상태 머신 2종 |

**가장 먼저 손대야 할 것:** `cx` 전역 스왑 제거(→ 명시적 레이어 인자)와 `COLS/ROWS/CELL` 전역의 `WorldGrid` 캡슐화. 이 둘만 정리해도 나머지는 기계적으로 옮겨진다.
