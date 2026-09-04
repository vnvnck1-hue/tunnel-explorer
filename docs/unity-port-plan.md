# 땅굴 크루 — Unity 포팅 계획 v0.1

> 작성: 2026-09-05 · 기준 빌드: `tunnel-crew-infinite-mode-v7.9.2.html` (17,371,356 B, 스크립트 블록 22개, 함수 약 1,050개)
> 근거: v7.9.2 코드 전수 분석 3편 — [코어 시뮬레이션](unity-port/analysis-01-core-sim.md) / [엔티티·전투·AI](unity-port/analysis-02-entities-combat-ai.md) / [셸·오디오·네트·자산](unity-port/analysis-03-shell-audio-net-assets.md) — 와 `docs/tunnel-crew-main-game-structure.md` v0.3, `docs/project-history-and-direction.md`
> 상태: **계획 초안 — §1 결정 사항 확정 후 M0 착수**

---

## 0. 한 줄 요약

브라우저 프로토타입이 검증한 골자 — **4직업 크루가 유한 지층을 채굴·전투하며 장악도를 올려 보스를 부르고, 특성 카드로 빌드를 만들고, 전리품을 들고 생환하는 원정 루프** — 를 그대로 가져가되, 17MB 단일 HTML에 뒤엉킨 시뮬레이션·연출·UI·개발툴을 **순수 C# 시뮬레이션 코어 + Unity 표현 계층 + ScriptableObject 데이터**의 3층으로 분리해 옮긴다. 밸런스 수치는 원본에서 기계적으로 추출해 데이터 에셋으로 넣고, 맵 생성은 같은 시드에서 원본과 동일한 결과가 나오는 것을 패리티 테스트로 보증한다.

---

## 1. 먼저 확정할 결정 (권장안 포함)

| # | 결정 | 권장 | 이유 |
|---|---|---|---|
| D1 | Unity 버전 / 렌더 파이프라인 | **Unity 6 LTS (6000.x) + URP 2D Renderer** | Light2D·노멀맵·풀스크린 셰이더 패스가 표준 제공. 원본의 WebGL 라이트맵/LX 4레이어 합성을 Renderer Feature로 옮기기 가장 수월 |
| D2 | 포팅 범위 | **본선 플레이 경로만**: 메인 메뉴 → 행성 원정(무한 모드) → 직업 선택(+AI) → 인게임 → 결과 → 정산·성장 지도 | 레거시 mine 모드(마을·수면·업그레이드 `UPG`), `#pTitle/#pUp/#pRep/#pSet/#pHow`, 솔로 미션(harvest/recover/purge)은 기획서 §14에서 "흡수·재정의"로 판정됨. 개발툴(Projectile Lab, Boss Lab, Test Hub, UILAB, LX 패널)은 Unity 에디터 인스펙터로 대체 |
| D3 | UI 프레임워크 | **UGUI + TextMeshPro** | 원본 UI가 DOM/CSS 오버레이 100%라 UGUI 캔버스 구조와 1:1 대응. 게임패드 내비게이션 필요(원본 미지원) |
| D4 | 입력 | **Input System (신형)** 액션맵 5종: Gameplay / UI / Chat / Craft / Ping | 원본은 G·V·C·Enter·Tab이 캡처 단계에서 본편 키를 가로채는 구조. 액션맵 전환으로 정리하면서 게임패드를 처음부터 지원 |
| D5 | 물리 | **Physics2D 미사용, 원본 원-AABB 충돌 유지** | 벽이 실시간으로 생기고 사라지는 게임. 원본 `collide()`의 3회 반복 해소 + 35% 보간 탈출은 이 게임 특유의 안전장치 |
| D6 | 시뮬레이션 틱 | **고정 60Hz 누산기** (원본은 가변 dt, 상한 0.05) | 코옵 호스트 권위·AI 결정 루프·리플레이 테스트를 위해 결정론 확보. 히트스톱은 원본대로 dt 배율(×0.055)로 |
| D7 | 코옵 스택 | **마지막 마일스톤(M8)로 미룸.** 코어는 처음부터 `SimCommand` 입력 큐로 설계 | 원본은 호스트 권위 + 시드 동기화. 그대로 옮길 수 있으나 알려진 불일치(coop/README "아직인 것")가 있어 재설계 여지. 스택은 Netcode for GameObjects 또는 기존 Node 릴레이 재사용 중 M7 시점에 결정 |
| D8 | 저장소 위치 | 같은 repo의 `unity/TunnelCrew/` 하위 폴더, `Library/` 등 gitignore | 문서·자산·원본 HTML과 한 곳. repo가 이미 자산으로 크므로 Unity 프로젝트는 원본 자산을 **복사하지 않고 임포트 스크립트로 참조·변환** |
| D9 | 1차 타깃 | Windows 데스크톱 단일 빌드 (기존 `.exe` 아이콘 규칙 `assets/app-icon-dragon.ico` 유지) | AGENTS.md 빌드 지침과 일치 |

---

## 2. 원본에서 가져가는 것 / 버리는 것

### 2.1 그대로 가져가는 골자 (규칙·수치 보존)

| 시스템 | 원본 근거 | 보존 항목 |
|---|---|---|
| 그리드 월드 | `G.cell/hp/dec/band`, 80×72, CELL=50 | 타일 7종(dirt/stone/ore/gem/crys/rock/core), HP 테이블 `HPT`, 산출 `YIELD`, 손상 4단계, 밴드 4구간 |
| 던전 생성 | `genTunnel()` 11단계 | 방 배치 → Prim MST + 우회 간선 → 지터 통로 → 연결성 보증 → BFS 거리장 → 암반 배치(봉인 검증 롤백) → 깊이 가중 광맥 → 출구(94백분위, 벽 속 1~2칸) → 유물/보급품/랜턴/소품. `DUNGEN` 상수 |
| 플레이어 | `TE`, `DEMO`, `collide()`, `tryDash()` | 이동 174.6px/s(3.5칸/s), 대시 55.6px·0.11s·쿨 0.91s, HP 181, i-frame 0.59, 카메라 데드존·룩어헤드·동적 줌·세로 42% 앵커 |
| 채굴 | `damage()`, 드릴 파이프라인 | 드릴 축 3점 샘플, 예열/과열 곡선, 암반 반동, 파괴 부수효과(인접 nudge, 드롭, 보급품 16%/4%), 대량 파괴 절감 규칙(PERF_BRK_BURST) |
| 시야 | `LOS` | 360레이 · 19타일 · 기억 11타일 · 30% 잔존 농도, 플레어/크루/보스 시야원 합산 |
| 4직업 | `INF_ROLES` (`CREW_ROLES`는 폐기) | dig/gunMul/dash 배율, Q/E 스킬 8종, 거너 파쇄탄(부착·신관 2s·쿨 12s), 드릴러 기반암 균열(압력 need 공식), 스카웃 그래플·정찰 펄스, 엔지니어 전력 노드·센트리 급전 규칙 |
| 투사체 | `updateProjectiles()` | 11종 카탈로그, 관통/도탄/폭발/레이저 규칙, `lastCell` 가드, 적 예고형 보스탄(물리 아님, 보간 연출 + 착탄 원 판정) |
| 적 | `updateEnemyAI()` | crawler/spitter/broodBeast + apex, 원뿔 시야 62° + 근접 감지, windup→strike→recover, 원거리 keep 3.0~5.6칸, 도약, 소프트 분리, 덩치=속도, **접촉 피해는 보스만** |
| 난이도 커브 | `infThreatValue/EnemyCap/SpawnInterval/SpawnBurst`, `spawnDebt` | "파면 팔수록 몰려온다" 루프 전체 |
| 런 구조 | `INF_PLANET`, 장악도, `infNextDepth` | 지층 3 + 이상지대, wallHp/enemyHp 배율, 장악도 목표 → 보스 소환, 휴식 → 하강 |
| 보스 | `INF_BOSS_TIER`, `infBossTick`, `bossLabBakedParams` | 수호자/포식자/변종 3티어, 장갑 링(66%/33% 페이즈, 피해 ×0.22), 돌진 FSM, 예고탄 2종, 벽 기믹 3종, 등장 4.65s·사망 3.5s 시네마틱 |
| 성장 | `INF_TRAITS`(~60) + `INF_LEGENDS`(6), 티어 가중치·피티, `INF_XP_TABLE/WEIGHT`, xpNeed 곡선 | 개인 XP 단일 관문(`infAwardXp`) 원칙 |
| 영구 성장 | `INF_NODE_SLOTS/CLUSTERS/DEFS`, `INF_PERM_CAPS` | 80노드/140랭크/코어 760, 누산 객체 P + 캡 클램프 |
| 유물 | `INF_RELICS`(33) + 원소 공명 | 훅 지점 12종 |
| 탈출 | `INF_ESCAPE` | 5단계 포트 프로세스, 심층별 대기, 착륙 지형 파괴, 생환 시에만 코어 보관 |
| AI 크루 | `ai/crew-ai.js` (1,790줄) | KIT 예산표, HUMANIZE 3겹 필터(성향·기분·의도 게이트, `G.t` 절대시각), decide 우선순위 14목표, 다익스트라 `pathDigCost`, AI 성장 분리 원칙 |
| 팀 도구 | `ping/tc-ping.js`, `chat/tc-chat.js`, `tcCraftingFeature` | 핑 9종·휠·도배 방지, 채팅 말풍선, 퀵크래프트 6레시피 |
| 저장 | `tc_infinite_meta_v1`, `tunnel_crew_settings_v1` | 스키마 그대로 JSON. 마이그레이션 체인은 v7.4.1 평면 구조부터만 |

### 2.2 버리는 것 (Unity 버전에 넣지 않음)

- 레거시 mine 모드 전체: `genDepth`, 마을·수면·인트로 씬, `UPG`(bond/pen/mine/hole), `hio_save_v1`, 세션 타이머, `G.mins/car/buried`(이미 데드 코드)
- 솔로 미션 모드(`CREW_MISSIONS`, 바이옴 선택 화면). 브라인 바이옴 장판 규칙은 행성 콘텐츠로 재활용 후보
- 개발툴 스크립트 블록: Projectile Lab, Boss Lab 인스펙터(값은 데이터로 이관), Test Hub/Relic Lab, UILAB(F8), LX 패널(F10), `#pSet`, `?tcTest` 패널, 관전(OBSERVER) 모드는 보류
- Canvas2D 폴백 어둠(`drawDarkness`), WebGL 미지원 분기, 자동 품질 강등 토글

### 2.3 새로 만들어야 하는 것 (원본에 없음)

- **일시정지 화면**, **로딩 화면**(assets/loading 아트 4장은 존재하지만 미참조)
- 게임패드 입력 + UI 내비게이션
- 9-slice UI 스프라이트(원본은 CSS로 대체 중, PNG 없음)
- 해상도·화면비 대응 규칙(원본은 브라우저 리사이즈 의존)

---

## 3. 목표 아키텍처

```
unity/TunnelCrew/Assets/
├ _Project/
│  ├ Sim/                ← 순수 C# (UnityEngine 참조 금지, asmdef 분리)
│  │  ├ World/           WorldGrid, TileType, DungeonGenerator, Connectivity(comp), Rng(mulberry32+FNV1a)
│  │  ├ Vision/          LosService (레이캐스트, explored/visible 버퍼 → byte[])
│  │  ├ Actors/          PlayerState, EnemyState, BossState, Projectile, EnemyShot, BossShot
│  │  ├ Systems/         Movement, Collision, Mining, Loot, Combat, EnemyAI, BossAI, Spawner, Threat
│  │  ├ Run/             RunState(INF), Dominance, XpGate, TraitDeck, Escape, Strata
│  │  ├ Roles/           Driller/Gunner/Scout/Engineer 스킬 + 설치물(Turret, PowerNode, Breaker, Flare)
│  │  ├ Crew/            AiCrewMember, Persona, IntentGate, Decide, Act, PathDigCost
│  │  ├ Meta/            PermanentNodes, Relics, SaveData, Migration
│  │  ├ Craft/           Recipes, Placement
│  │  └ Events/          ISimEvent (TileBroken, EnemyHurt, BossSpawned…) — 연출/오디오/HUD가 구독
│  ├ Presentation/       ← MonoBehaviour
│  │  ├ World/           TileChunkRenderer(또는 Tilemap 어댑터), PropRenderer, LampRenderer
│  │  ├ Actors/          8방향 스프라이트 애니메이터, 몬스터 16프레임 스트립, 드래곤 4애니
│  │  ├ Lighting/        Light2D 브리지, LosTexture 업로더, LX 4레이어 Renderer Feature
│  │  ├ Camera/          CameraRig(데드존·룩어헤드·동적 줌·클램프), Impulse(히트스톱·킥)
│  │  ├ Juice/           J/FEEL 대체 — 파티클·데미지 텍스트·스쿼시
│  │  ├ Audio/           SfxBank(25종×변형), Bgm 라우터(lobby/purple/boss), 앰비언스 2겹
│  │  └ UI/              MainMenu, RoleSelect, Hud(솔로/무한), LevelUpCards, Rest, Result, Settlement(노드맵 SVG→UGUI), Starmap, Ping, Chat, CraftWheel, Pause, Loading
│  ├ Data/               ← ScriptableObject
│  │  TuningTE, TuningDEMO, LitTune, LxPreset, RoleDefs, TraitDefs, LegendDefs, NodeDefs, RelicDefs, BossParams, PlanetDef, EscapeParams, Recipes, XpTable, EnemyDefs, AiKits
│  └ Bootstrap/          GameFlow 상태 머신(단일 — CREW.phase/SCENE/INF.active 3중 상태 통합)
├ Art/ (임포트 스크립트가 생성)  Tiles(SpriteAtlas), Characters, Monsters, Dragon, UI
├ Audio/ (추출 스크립트가 생성)
└ Tests/  EditMode: 생성 패리티, 충돌, XP 곡선, 노드 캡 / PlayMode: 스모크
tools/unity-export/      ← 원본 HTML에서 데이터·자산을 뽑는 스크립트 (Node/Python)
```

### 3.1 설계 원칙 (원본의 숨은 결합을 푸는 규칙)

1. **좌표 정규화**: 1셀 = 1 Unity 유닛. 원본 `teWorld(v)=v*CELL/9`는 상수 5.5556/50 = **0.1111 셀**로 굳힌다. 원본의 px 수치는 ÷50으로 셀 단위 변환해 데이터에 넣는다.
2. **전역 상태 해체**: `G`·`INF`·`CREW` 세 거대 객체를 도메인별 상태 구조체로 분해. `infResetBuild()`(12234행)가 특성이 건드리는 INF 필드 전체 목록이므로 `PlayerBuild` 구조체 설계의 출발점으로 쓴다.
3. **부수효과는 이벤트로**: 원본 `damage()`·`hurtEnemy()`는 파티클·사운드·XP·장악도·유물 훅을 한 함수에 담고 있다. Sim은 `TileBroken`, `EnemyHurt` 같은 이벤트만 발행하고, XP·장악도·유물은 Sim 내부 구독자, 파티클·사운드·HUD는 Presentation 구독자로 나눈다.
4. **데미지 소유권 명시**: 원본은 `AICREW.dmgSrc/breakSrc` 전역 플래그 + try/finally로 "누가 부쉈나"를 전달한다. Unity에서는 `DamageSource{ActorId, Kind}`를 인자로 넘긴다.
5. **타겟 조회 추상화**: 적 AI가 `G.sh` 대신 `AI_TGT(e)`를 경유하는 구조는 좋다. `ITargetProvider`로 승격해 플레이어·AI 크루·코옵 피어를 같은 목록으로 다룬다.
6. **상태 머신 단일화**: `SCENE`, `CREW.phase`, `INF.active`가 교차 검사되는 구조를 `GameFlow` 하나로. 시네마틱이 `CREW.phase='bossIntro'`로 월드를 멈추는 방식은 `Sim.Paused` 플래그로.
7. **렌더 레이어 명시**: 원본은 전역 `cx`를 오프스크린으로 스왑하며 100여 개 draw 함수가 암묵적으로 레이어를 정한다. Unity에서는 Sorting Layer + Order로 §8.3 드로우 순서(18단계)를 표로 고정한다.
8. **튜닝값은 코드에 없다**: `DEMO`(190개), `TE`, `LIT_TUNE`, `LX_DEFAULT`, `BOSS_TUNE_FALLBACK`+`bossLabBakedParams`(63개), `INF_*` 상수 전부 ScriptableObject. 원본 F8/F10/Boss Lab 패널의 역할은 인스펙터 + 런타임 디버그 오버레이 하나로.
9. **런타임 함수 래핑 금지**: 원본은 `infSpawnBoss`, `infBossDefeated`, `AICREW.update`, `updateCrew`, `paintUI`가 5개 블록에서 중첩 래핑된다. 이식할 때는 **래핑 후 최종 동작**을 기준으로 옮기고, 확장점은 이벤트로 연다.

### 3.2 그리드 렌더링 선택

- 1차: **Unity Tilemap** 2장(바닥/벽) + 타일 240슬롯 아틀라스(`tile_manifest.json`에 atlasX/Y 포함 → SpriteAtlas 직행). 손상 단계 변경은 `SetTile`, 타격 진동/밀림(`vib/nudge`)은 `SetTransformMatrix`로 셀 단위 오프셋.
- 성능 미달 시: 청크 메시 렌더러(16×16 셀, dirty 청크만 재빌드). 5,760셀이라 Tilemap으로 충분할 가능성이 높다.
- 암반(core) 3패스(아래 그림자/측면 H=0.26셀/윗면)는 별도 Tilemap 레이어 + 오버레이 스프라이트.

### 3.3 조명·시야 대응

| 원본 | Unity |
|---|---|
| `LOS` 레이캐스트 → RGBA 픽셀 버퍼 | `LosService`(Sim, byte[] visible/explored) → `Texture2D R8G8` 업로드 (Job 병렬화 선택) |
| `FOW` WebGL 라이트맵(노멀맵 fBm, Bayer 디더, 광원 예산 6+8) | URP Light2D(Point/Spot=손전등 원뿔 28°) + 노멀맵 텍스처. 광원 예산은 Light2D 컬링으로 자연 처리 |
| LX 4레이어 합성(lightmap/contrast/zone/core) | Full Screen Pass Renderer Feature 1개, LxPreset SO에서 uniform 공급. 시야 rise 0.16s/fall 0.38s 보간 포함 |
| `LIT` 벽 그림자·림(광원 방사 차분) | Shadow Caster 2D를 벽 Tilemap Collider에서 자동 생성, 림은 셰이더 |
| `drawDarkness` 폴백 | 없음 |

---

## 4. 데이터·자산 추출 파이프라인 (`tools/unity-export/`)

HTML을 Unity에서 파싱하지 않는다. 원본에서 한 번 뽑아 파일로 굳힌다.

| 스크립트 | 입력 | 출력 | 비고 |
|---|---|---|---|
| `extract-embedded-audio.py` | HTML의 `MENU_SFX.bank`(25종·50+변형), `DRILL_SMP` 3종, `LOBBY/PURPLE_BGM_DATA` | `unity/…/Audio/*.wav|ogg|mp3` + `sfx-bank.json`(cat/g/jit) | 런타임 외부 오디오 파일은 5개뿐, 나머지 전부 base64 |
| `dump-tuning.mjs` | HTML을 jsdom/vm으로 로드해 `DEMO, TE, LIT_TUNE, LX_DEFAULT, DUNGEN, HPT, YIELD, INF_ROLES, INF_TRAITS(ok/a 함수는 id·설명만), INF_LEGENDS, INF_NODE_*, INF_PERM_CAPS, INF_RELICS, INF_PLANET, INF_ESCAPE, INF_CARDS, INF_XP_TABLE/WEIGHT, INF_BOSS_TIER, BOSS_TUNE_FALLBACK, #bossLabBakedParams, RECIPES, KIT, PING CFG, CHAT CFG` 직렬화 | `data/*.json` → Unity 에디터 임포터가 SO 생성 | 특성의 `a()` 효과 함수는 자동 추출 불가 → **수작업 표(§5 M4)** |
| `slice-character-sheets.py` | `assets/characters/reely-*-actions/sheets/*.png` + `reports/*.report.json` | Unity `.spriteatlas` + 슬라이스 메타(224×224, 8열×2행, 방향별 피벗 Y) | 좌측 5방향 원본, se/e/ne는 X 반전 규칙 유지 |
| `import-monster-frames` | `monster_assets_v1.5.4/frames/{crawler,spitter,brood-beast}/frame_01..16.png` | 애니 클립 4종(보행 0~5, 대기 6~7, 깜빡임 8~11, 질주 12~17) | 단일 방향, 반전 없음(보스만 facing 반전) |
| `import-dragon-frames` | `assets/red-fire-dragon/{idle 37, walking 37, fire-breath-a 26, death 24}` | 10fps 클립 4종 | 포효 SFX 트리거 = fireBreath 15프레임(animT 1.4) |
| `import-tiles` | `tunnel_crew_tile_resources_v1/tile_manifest.json` (536엔트리, 50×50) | SpriteAtlas 2바이옴 + `TileAtlasIndex` 룰 (`ti*48 + band*12 + surface*4 + damage`) | overlays 8종 포함 |
| `export-ui-layout` | 실행 중 브라우저 `localStorage['tc.uiLayout.v1']` | `ui-layout.json` | **HUD 좌표의 정답지**. HTML 초기 좌표는 UILAB 오버라이드로 덮여 있을 수 있음 |
| `dump-mapgen-fixture.mjs` | `genTunnel(d)`를 시드 N개로 실행 | `fixtures/map-{seed}-{depth}.json` (cell 배열·entry·exit·lamps) | §6 패리티 테스트용. 원본의 `Math.random()` 사용 지점(진입점·출구 후보 5386·5744행)은 시드 RNG로 바꾼 사본에서 덤프 |

---

## 5. 마일스톤

각 마일스톤은 "플레이 가능한 상태"를 끝점으로 한다. 순서는 기획서 §15 수직 슬라이스 제안을 따른다.

### M0 — 준비 (추출·골격)
- Unity Hub + Unity 6 LTS 설치(현재 이 PC에 Unity·.NET SDK 없음), URP 2D 템플릿으로 `unity/TunnelCrew` 생성, asmdef 3개(Sim / Presentation / Tests)
- §4 추출 스크립트 전부 실행, `data/*.json` + 자산 임포트 완료
- `GameFlow` 빈 상태 머신, 빈 씬 3개(Boot / Menu / Run)
- **완료 기준**: 에디터에서 타일 아틀라스·캐릭터 시트·오디오가 임포트 오류 0으로 보이고, SO 데이터가 원본 값과 일치(스팟체크 20개)

### M1 — 코어 루프 그레이박스 (파고, 걷고, 줍는다)
- `WorldGrid`, `Rng`, `DungeonGenerator`(genTunnel 11단계), `Connectivity`
- `Movement`+`Collision`(원-AABB 3회), 대시(4px 서브스텝→0.08셀), 넉백·기절
- `Mining`: 드릴 3점 샘플, 예열/과열, 암반 반동, `damage()` 규칙, 손상 4단계 타일 스왑, nudge
- `Loot`: z축 포물선·바운스·자석·획득, `RES_MAX` 상한
- `CameraRig`: 데드존 111px→2.22셀, 룩어헤드 2.78셀, 동적 줌, 세로 42% 앵커, 클램프
- 렌더: Tilemap 2장 + 암반 3패스, 드릴러 8방향 시트 1종, 임시 HUD(HP·pulp·bloom)
- **완료 기준**: 시드 고정 맵에서 WASD·LMB·Space로 3분 플레이. §6 맵 생성 패리티 테스트 통과

### M2 — 시야·조명
- `LosService` + LOS 텍스처, 시야 전환 보간
- Light2D 앰비언트/손전등(F 토글)/랜턴 6개/플레어, 노멀맵
- LX Renderer Feature(4레이어), `LxPreset` SO에 v7.9.1-lx 확정값
- 벽 그림자(Shadow Caster 2D)
- **완료 기준**: 원본 스크린샷과 나란히 놓고 어둠 농도·기억 잔존·손전등 원뿔이 시각적으로 대응. 렌더 컬링(카메라 ±1셀, softSeen pad 2) 적용

### M3 — 전투와 4직업
- `Projectile` 11종 규칙, `EnemyShot`, 예고형 `BossShot`(보간 + 착탄 원)
- 적 3종 + apex: 스폰 링, 원뿔 시야, FSM(windup/strike/recover), 원거리 keep, 도약, 소프트 분리, 덩치=속도, 상태이상(스턴·빙결·슬로우)
- `Combat`: `hurtEnemy` 규칙(즉시 각성, 넉백 공식 hitPower^0.72×rangePower, apex 저항 0.52, 보스 면역), `applyPlayerDamage`(플레이 중 ×0.85, 방어막 ×0.35), i-frame, 상호 부활(5초·1.6셀·50%)
- 4직업 스킬 8종 + 거너 파쇄탄 + 드릴러 균열 + 엔지니어 급전 규칙 + 설치물(센트리·전력노드)
- 히트스톱·킥·스쿼시(FEEL), 파티클(J 대체), 데미지 텍스트
- 몬스터 16프레임 클립, 4직업 시트
- **완료 기준**: 4직업 각각 30초 전투 스모크. 적 HP/속도/공격 간격이 데이터 값과 일치

### M4 — 런 구조: 장악도 → 보스 → 하강 → 탈출
- `RunState`: 지층 3 + 이상지대 배율, `Threat`/`EnemyCap`/`SpawnInterval`/`SpawnBurst`/`spawnDebt`
- `Dominance`: 층 파괴 가능 블록 집계, 목표 도달 → 보스 소환
- `XpGate` 단일 관문 + 역할 가중치 + 층당 트리클 상한 60, xpNeed 곡선, `TraitDeck`(티어 가중치·피티·리롤), **특성 ~60종 효과를 수작업으로 `PlayerBuild` 필드 변경 표로 옮김**(원본 `a()` 함수를 한 줄씩 읽어 표 작성 — 가장 손이 많이 가는 항목)
- 보스: 3티어, 장갑 링·페이즈, 소환, 돌진 FSM(텔레그래프·충격·기절·천장 붕괴), 예고탄 2종, 벽 기믹 3종, 감옥, 이동 파쇄
- 등장/사망 시네마틱(레터박스·카메라 보간·이름 플레이트·포효 프레임 동기), 전설 카드 3택 휴식 화면
- `Escape` 5단계, 착륙 지형 파괴, 전원 탑승, 생환/사망 분기
- 무한 HUD 전체(장악도 레일·위협·보스 HP·XP 바·탄창·역할별 빌드 텍스트·스킬 슬롯·키가이드)
- **완료 기준**: 1층 진입 → 장악도 → 수호자 → 하강 → 2층 → 탈출 성공/사망 → 결과 화면까지 한 사이클 (기획서 §19.3 스모크 기준)

### M5 — 메타: 정산·영구 노드·유물·저장·메뉴
- `SaveData`(`tc_infinite_meta_v1` 스키마) JSON → `persistentDataPath`, 설정 저장
- 정산 3뷰(요약·성장 지도·유물 보관고), 영구 노드 80개 지도(팬·줌·발견 연출·랭크·캡 클램프·구매 실패 롤백)
- 유물 33종 + 원소 공명 + 소켓 5
- 메인 메뉴(패럴랙스 키아트), 직업 선택, 행성 지도(11행성, 잠금 10), 화면 전환 와이프(260/40/340ms)
- **새로 설계**: 일시정지, 로딩(assets/loading 아트 사용), 해상도 옵션
- **완료 기준**: 3런 연속 플레이 후 코어·노드·기록이 재시작 후 유지. 메뉴→런→정산→메뉴 왕복

### M6 — AI 크루·핑·채팅·퀵크래프트
- `AiCrewMember`: KIT 4종, Persona 롤, 기분, IntentGate(절대시각), decide 14목표 우선순위, act, 다익스트라 `pathDigCost`, 파묻힘 탈출·워프, 다운/부활, AI 성장 분리(`AI_TRAITS`), 소유권 기반 XP 귀속
- 보스탄 회피, 적탄 옆대시, 스트레이프, 잠담(idleBeat/potshot)
- 핑 9종·8방향 휠·컨텍스트 판정·도배 방지·AI 명령 주입, 채팅 말풍선·로그·AI 잡담
- 퀵크래프트 6레시피, 배치 유효성, 줌 잠금
- **완료 기준**: 사람 1 + AI 3 편성으로 M4 사이클 완주. AI가 벽에 갇혀 정지하는 사례 0(10분 관찰)

### M7 — 오디오·연출·빌드
- SFX 뱅크 25종(카테고리 게인·피치 지터·소프트 리미터 대체 = Audio Mixer), 드릴 start/loop/release, BGM 라우터(lobby/purple/boss·revision 가드), 앰비언스 2겹, 보스 BGM 페이드 규칙(spawn→useBoss, defeated→3.0s/2.6s)
- 게임패드 전 화면 내비게이션, 리매핑
- Windows 빌드 + `app-icon-dragon.ico`, 빌드 검증 체크리스트(AGENTS.md 규칙 준용)
- **완료 기준**: 원본 v7.9.2와 나란히 10분 플레이 비교 리뷰

### M8 — 코옵 (선택)
- `SimCommand` 큐를 네트워크 입력으로 확장, 호스트 권위 + 시드 동기화(원본 프로토콜 v4 구조 유지: state 30Hz / esnap 15Hz / cells 5Hz / ehit 위임 / ekill 귀속)
- 스택 결정(D7). 서버 세이브(`PUT /meta/닉`)는 필요 시
- 원본 알려진 불일치(게스트 유물 미적용, 보스탄 경감 이중 적용) 재설계

---

## 6. 검증 전략

| 검증 | 방법 |
|---|---|
| **맵 생성 패리티** | 원본 `genTunnel`(Math.random 제거 사본)로 덤프한 fixture 10개와 C# 생성기 출력의 `cell[]`·entry·exit 완전 일치. 실패 시 어느 단계(11단계 중)에서 갈라졌는지 단계별 스냅샷 비교 |
| 수치 패리티 | SO 데이터 vs `dump-tuning` JSON 자동 diff (에디터 테스트) |
| 규칙 단위 테스트 | `collide` 밀어내기, 드릴 예열 곡선, xpNeed(L)=30+10L+2.4L², 노드 캡 클램프, 티어 가중치 합, threat/cap/interval 함수 |
| 스모크(PlayMode) | 헤드리스 Sim을 60Hz로 N틱 돌려 예외 0, 적 수 ≤ cap, 장악도 단조 증가 |
| 감각 비교 | 원본 HTML을 `.claude/launch.json` 로컬 서버로 띄워 같은 시드·같은 직업으로 나란히 플레이(기존 [[tunnel-crew-browser-verify]] 방식) |

---

## 7. 리스크와 대응

| 리스크 | 대응 |
|---|---|
| 특성 ~60종·유물 33종·노드 80종의 효과가 `INF` 전역을 직접 변형하는 클로저라 자동 추출 불가 | M4에서 효과 표를 수작업 작성. 표 자체를 `docs/unity-port-trait-table.md`로 남겨 리뷰 가능하게 |
| 원본이 가변 dt라 고정 60Hz로 바꾸면 손맛(드릴 타격 간격 0.06s, 대시 0.11s, 히트스톱 28~68ms)이 미세하게 달라짐 | 60Hz 틱(16.7ms)이 모든 타이머보다 짧아 체감 차이는 작을 것. M3에서 원본과 A/B |
| 렌더 순서·레이어가 원본에서 암묵적 | §8.3의 18단계 드로우 순서를 Sorting Layer 표로 먼저 고정하고 각 프리팹에 배정 |
| 자산 227MB, 캐릭터 시트 1,476장 | 런타임 참조 파일만 임포트(sheets 원본·kenney 후보 203 ogg·concepts 제외). 임포트 스크립트가 목록을 소유 |
| 원본이 계속 진화(v7.9.x) | 포팅 기준은 v7.9.2로 동결. 이후 원본 변경은 `docs/unity-port-plan.md` §9에 "이관 대기" 항목으로 적재 |
| Unity 미설치, 프로젝트 초기 세팅 리스크 | M0에 하루 배정. Unity 6 LTS + URP 2D 템플릿 그대로 사용 |

---

## 8. 원본 코드 참조 지도 (포팅 작업자용)

행 번호는 base64 제거본 기준(원본과 동일한 행). 원본 소스가 별도 파일로 있는 모듈은 파일을 우선한다.

| 영역 | 위치 |
|---|---|
| 상수·팔레트·튜닝 | 1267~2345 (`P/PT/PT2`, `HPT/YIELD`, `TE`, `DEMO`, `LIT_TUNE` 7865) |
| 캐릭터·몬스터·드래곤 시트 | 2098~3020 |
| 오디오(AU/SFX/MENU_SFX/BGM_ROUTE/AMBI) | 3327~4622 |
| 주스·필(J/FEEL) | 4838~5130 |
| 전역 상태 G, LOS | 5130~5353 |
| 맵 생성 genTunnel | 5645~6004 · enterDepth 6005 · startIncursion 6039 |
| 채굴 damage / 충돌 collide | 6068~6183 |
| 적 스폰·AI·보스 이동 | 6242~7093 |
| update(dt) 본체 | 7096~7420 (카메라 7364) |
| LIT / renderDepths / paintUI | 7872~8426 |
| 메인 루프·리사이즈·입력 | 9464~9702 |
| 무한 모드 INF 전체 | 11614~15225 (역할 11818, 특성 12025, 노드 11632~11811, 보스 13229~13535, 탈출 13554, 정산 14028, 엔드런 14900, 유물 14220) |
| 화면 전환 / 보스 등장·사망 FX / 보스 BGM | 15642 / 15854 / 16089 / 16210 |
| AI 크루 | `ai/crew-ai.js` (HTML 16603~18820) |
| 핑 / 채팅 / 크래프트 | `ping/tc-ping.js` / `chat/tc-chat.js` / HTML 21986~22200 |
| 코옵 | `coop/server.mjs`, `coop/client.js` |
| LX 조명 | HTML 22200~ (`LX_DEFAULT` 확정값) |

---

## 9. 이관 대기 (v7.9.2 이후 원본 변경)

*(비어 있음 — 원본에 새 기능이 들어오면 여기에 적는다)*

---

*이 문서는 누적 갱신한다. 결정이 바뀌면 결론과 이유, 남은 위험을 함께 적는다. 마일스톤 완료 시 §5 해당 항목에 완료일과 검증 결과를 기록한다.*
