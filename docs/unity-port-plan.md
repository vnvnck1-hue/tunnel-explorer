# 땅굴 크루 — Unity 포팅 계획 v0.1

> 작성: 2026-09-05 · 기준 빌드: `tunnel-crew-infinite-mode-v7.9.2.html` (17,371,356 B, 스크립트 블록 22개, 함수 약 1,050개)
> 근거: v7.9.2 코드 전수 분석 3편 — [코어 시뮬레이션](unity-port/analysis-01-core-sim.md) / [엔티티·전투·AI](unity-port/analysis-02-entities-combat-ai.md) / [셸·오디오·네트·자산](unity-port/analysis-03-shell-audio-net-assets.md) — 와 [엔진 네이티브 대체 분석](unity-port/analysis-04-engine-native-replacements.md)(2026-09-06), `docs/tunnel-crew-main-game-structure.md` v0.3, `docs/project-history-and-direction.md`
> 상태: **§1 결정 D1·D2·D8 확정(2026-09-06) · 기준 빌드 동결 완료 → M0 착수 가능**
> 기준 빌드 동결 커밋: `b55d39e` (`tunnel-crew-infinite-mode-v7.9.2.html`). 이후 원본 HTML 변경은 §9에 적재하고 포팅은 이 커밋 기준으로만 진행한다.

---

## 0. 한 줄 요약

브라우저 프로토타입이 검증한 골자 — **4직업 크루가 유한 지층을 채굴·전투하며 장악도를 올려 보스를 부르고, 특성 카드로 빌드를 만들고, 전리품을 들고 생환하는 원정 루프** — 를 그대로 가져가되, 17MB 단일 HTML에 뒤엉킨 시뮬레이션·연출·UI·개발툴을 **순수 C# 시뮬레이션 코어 + Unity 표현 계층 + ScriptableObject 데이터**의 3층으로 분리해 옮긴다. 밸런스 수치는 원본에서 기계적으로 추출해 데이터 에셋으로 넣고, 맵 생성은 같은 시드에서 원본과 동일한 결과가 나오는 것을 패리티 테스트로 보증한다.

---

## 1. 먼저 확정할 결정 (권장안 포함)

| # | 결정 | 권장 | 이유 |
|---|---|---|---|
| D1 | Unity 버전 / 렌더 파이프라인 | **확정: Unity 6.3 LTS `6000.3.15f1` + URP 2D Renderer** *(2026-09-06)* | 이 PC에 이미 설치됨(6000.0.69f1도 있으나 6.3이 최신 LTS, 2027-12까지 지원). Light2D·소프트 섀도·노멀맵·Full Screen Pass가 표준 제공 |
| D2 | 포팅 범위 | **확정: 본선 플레이 경로 + 이상지대 무한 하강** *(2026-09-06)* — 메인 메뉴 → 행성 원정 → 직업 선택(+AI) → 인게임(지층 1~3) → 중심부 보스 → **이상지대 N층 무한 하강(엔드게임)** → 탈출 → 결과 → 정산·성장 지도. `INF_PLANET.abyss`(wallHp ×1.6ⁿ, enemyHp ×1.55ⁿ, 장악도 .34)와 변종 보스 티어를 출시 기능으로 포함한다 | 레거시 mine 모드(마을·수면·업그레이드 `UPG`), `#pTitle/#pUp/#pRep/#pSet/#pHow`, 솔로 미션(harvest/recover/purge)은 기획서 §14에서 "흡수·재정의"로 판정됨. 개발툴(Projectile Lab, Boss Lab, Test Hub, UILAB, LX 패널)은 Unity 에디터 인스펙터로 대체 |
| D3 | UI 프레임워크 | **UGUI + TextMeshPro** | 원본 UI가 DOM/CSS 오버레이 100%라 UGUI 캔버스 구조와 1:1 대응. 게임패드 내비게이션 필요(원본 미지원) |
| D4 | 입력 | **Input System (신형)** 액션맵 5종: Gameplay / UI / Chat / Craft / Ping | 원본은 G·V·C·Enter·Tab이 캡처 단계에서 본편 키를 가로채는 구조. 액션맵 전환으로 정리하면서 게임패드를 처음부터 지원 |
| D5 | 물리 | **Physics2D 하이브리드** — 충돌·쿼리·분리는 엔진(Tilemap Collider + Composite, Kinematic Rigidbody2D + Cast/MovePosition, Continuous CD), 이동 적분·넉백·경로는 스크립트 | *(2026-09-06 개정)* 원본 `collide()`의 3회 반복·4px 서브스텝은 물리 엔진이 없어 만든 우회. 벽이 실시간 생성될 때의 탈출 처리는 OverlapCircle 기반으로 의도만 보존. 근거 [analysis-04 §6](unity-port/analysis-04-engine-native-replacements.md) |
| D6 | 시뮬레이션 틱 | **고정 60Hz 누산기** (원본은 가변 dt, 상한 0.05) | 코옵 호스트 권위·AI 결정 루프·리플레이 테스트를 위해 결정론 확보. 히트스톱은 `Time.timeScale=0.055`를 실시간 N ms 유지하는 방식으로 틱과 분리 — 원본의 12ms(보스)·20ms(적) 히트스톱이 16.7ms 틱보다 짧아도 보존된다 *(2026-09-06 개정, analysis-04 §3.2)* |
| D7 | 코옵 스택 | **마지막 마일스톤(M8)로 미룸.** 코어는 처음부터 `SimCommand` 입력 큐로 설계 | 원본은 호스트 권위 + 시드 동기화. 그대로 옮길 수 있으나 알려진 불일치(coop/README "아직인 것")가 있어 재설계 여지. 스택은 Netcode for GameObjects 또는 기존 Node 릴레이 재사용 중 M7 시점에 결정 |
| D8 | 저장소 위치 | **확정: 같은 repo 유지** *(2026-09-06)*. `unity/TunnelCrew/` 하위, `Library/ Temp/ Logs/ obj/ Build/` gitignore. 신규 바이너리(png·wav·ogg·psd·ttf)는 **Git LFS**로 추적 | 문서·자산·원본 HTML과 한 곳. 현재 `.git` 392MB + Unity 임포트 자산 약 160MB → 약 550MB로 GitHub 권장 한도(1GB 경고) 안. LFS는 반복 재수출되는 아트가 히스토리를 부풀리는 것을 막기 위한 것이며, GitHub Free/Pro 무료 한도 10GB 안에서 무료 |
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
- 개발툴 스크립트 블록: Projectile Lab, Boss Lab 인스펙터(값은 데이터로 이관), Test Hub/Relic Lab, UILAB(F8), LX 패널(F10), `#pSet`, `?tcTest` 패널. 관전(OBSERVER) 모드는 2026-09-07 포팅 완료(보류 해제)
- Canvas2D 폴백 어둠(`drawDarkness`), WebGL 미지원 분기, 자동 품질 강등 토글
- HTML·브라우저 제약의 산물 전체 목록은 [analysis-04 §10](unity-port/analysis-04-engine-native-replacements.md). 렌더 트릭·런타임 래핑·주입 파이프라인·자동재생 우회·가변 dt 루프 등은 어떤 형태로도 옮기지 않는다

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

> *(2026-09-06)* 아래 표는 1차 대응이다. 확정 구조는 [analysis-04 §2](unity-port/analysis-04-engine-native-replacements.md): **Light2D + 어둠 풀스크린 패스 1개(가시 폴리곤 RT + 탐색 누적 RT) + Volume 포스트프로세싱**. LX 4레이어는 어둠 패스 1개와 지층별 Volume 프로파일로 접힌다.


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
| ~~`export-ui-layout`~~ | — | — | **폐기** *(2026-09-06 결정)*. HUD 좌표를 원본에서 가져오지 않는다. UGUI 앵커·안전영역·게임패드 내비게이션 기준으로 **새로 배치**한다. 참고용으로 원본 스크린샷만 본다 |
| `bake-procedural-sfx.mjs` | HTML의 `SFX.*` 중 절차 합성 17종(buy, cache, dawn, descend, exit, fail, ore, ready, rescue, start, tick, timeout, voice, warn, zzz, 드릴 폴백 2) + 보스 럼블·사망음·brk 저역 | `Audio/baked/*.wav` 변형 3~5개씩 | `OfflineAudioContext`로 렌더. analysis-04 §8 |
| `gen-tile-normals` | 타일 536장 | `_NormalMap` Secondary Texture 아틀라스 | Laigter 등 높이 추정. analysis-04 §2.1 |
| `dump-mapgen-fixture.mjs` | `genTunnel(d)`를 시드 N개로 실행 | `fixtures/map-{seed}-{depth}.json` (cell 배열·entry·exit·lamps) | §6 패리티 테스트용. 원본의 `Math.random()` 사용 지점(진입점·출구 후보 5386·5744행)은 시드 RNG로 바꾼 사본에서 덤프 |

---

## 5. 마일스톤

각 마일스톤은 "플레이 가능한 상태"를 끝점으로 한다. 순서는 기획서 §15 수직 슬라이스 제안을 따른다.

### 현황 요약 (2026-09-07 기준)

| 마일스톤 | 상태 | 비고 |
|---|---|---|
| M0 준비 · M1 코어 루프 · M2 시야/조명 · M3 전투/4직업 · M4 런 구조 | 완료 | M3·M4 사용자 체크포인트 통과. 어둠 톤은 사용자가 직접 조정(고치지 않음) |
| M5 메타 — 정산·영구 노드 80·유물 33·저장·메뉴·와이프·로딩·설정 | 완료 | UGUI 정식 배치는 보류(IMGUI 1080p 배율 유지, Pretendard 도착 후 M7 잔여로) |
| M6 AI 크루·핑·채팅·퀵크래프트 | 완료 | 관전 모드(OBSERVER)는 2026-09-07 사용자 요청으로 포팅 완료(ObserverPilot · ObserverMode) |
| M7 오디오·연출·게임패드·빌드 | 구현 완료 · **체크포인트 대기** | 완료 기준 "원본 v7.9.2 와 나란히 10분 비교 리뷰"는 사용자 확인 필요 |
| M8 코옵 (선택) | 미착수 | 착수 여부 결정 대기 |

- 테스트: EditMode 134/134 (Dungeon/World/Combat/RunState/Boss/Escape/Trait/TraitSubsystem/Meta/Relic/Crew/Team).
- 빌드: `unity/Build/Windows/TunnelCrew.exe` (Windows 64, 392MB, 에러 0, 메인 메뉴 진입 확인). 빌드 산출물은 git 제외.
- 검증 캡처: `unity/TunnelCrew/m3_*.png · m4_*.png · m5_*.png · m6_*.png · m7_*.png` (git 제외, 로컬 참고용).

**2026-09-07 오후 처리분**

- **캐릭터 발 위치 수정** (사용자 피드백: 드릴러가 중심보다 떠 보임) — 원본 `drawDrillerSprite` 는 `translate(x,y)` 뒤 `LIT.spr(…, r - pivot*scale, …)` 로 그려 발(피벗)이 충돌원 바닥 y+r 에 놓인다. Unity 는 피벗을 시뮬 위치에 두고 있어 정확히 r=0.5 셀 떠 있었다. `PlayerView.FootDrop`(=SimTuning.PlayerRadius) 만큼 내리고 원본 걷기 들썩임 `sin(t·16)·r·.035` 도 함께 이식. 크루의 라벨·HP바·실드 자식 오프셋은 FootDrop 을 더해 세계 좌표 유지. 시트 피벗 자체(원본 `*_PIVOTS` → `1 - pivot/224`)는 이미 정확했음.
- **로비 BGM 원본 복원 완료** — ffmpeg 없이 PyAV(`pip install av`, ffmpeg 내장 `vorbis` 인코더, `strict experimental`) 로 `lobby-cave.webm` → `Resources/Audio/music/lobby-cave.ogg`(76.8s, 48kHz 스테레오, 128kbps). 주의: 프레임 pts 를 직접 채워야 Ogg 헤더 길이가 맞다(pts=None 이면 4.8s 로 기록돼 Unity 가 4.8s 클립으로 읽음). `AudioDirector.BuildMusic` 로비 트랙 = lobby-cave g 1.00(원본 BGM_ROUTE 값), 파일 없으면 이전 대체로 폴백.
- **게임패드 리매핑 UI 완료** — `Presentation/Input/GamepadMap.cs`(액션 10종 → GamepadButton, PlayerPrefs `tc.gp.<action>`, 기본값 = M7 표). 설정 화면 우측 패널: 줄 클릭 → 캡처(6초) → 패드 버튼 누르면 배정, 중복 버튼은 상대 액션을 기본값으로 되돌림, "기본값 복원". RunBootstrap 런 입력과 MetaScreens 일시정지가 GamepadMap 을 읽는다. 스틱·메뉴 A/B 는 고정.
- **UI 리소스를 원본 최신본으로 교체** (사용자 지적: 로비·직업 선택이 레거시 이미지) — 원본 v7.9.2 참조 기준으로 키아트 `hero-tunnel-crew-keyart-v5.webp`(→ `UI/keyart-main.png` 2048×1147, Pillow 변환), 로고 `title-tunnel-crew-v2.png`(→ `UI/title-logo.png`, 메인 메뉴 텍스트 타이틀 대체), 직업 카드·초상화 `assets/characters/*-playable.png`(작업 트리 최신본 → `UI/select-*` 원본 크기 · `UI/portrait-*` 500²). 레거시 `hero-main-v1`·`char-*-select-v4`·`bg-tunnel-cavern-casual-v3-dark`(`UI/bg-cavern`) 폐기, 런 밖 화면 배경은 키아트 공용. 규칙: `assets/menu/` 파일명 버전이 아니라 최신 HTML 의 실제 참조를 기준으로 고른다.
- **"무한 모드" 범위 확인** — 원본 메뉴의 02 무한 모드(`tcLaunchInfScene` → 직업 선택 → 바로 런)와 03 행성 원정(행성 지도 → 직업 선택 → 런)은 같은 INF 엔진·같은 `INF_PLANET`(지층 3 + 이상지대)을 쓴다. 코드상 차이는 `INF.planetRun` 플래그 하나로, 행성별 기록(`INF_META.planets` runs/clears/escapes/bestDepth)을 남기는지만 다르다. D2 가 행성 원정 경로로 통합했으므로 게임플레이는 이미 포팅되어 있고, **"행성 지도 없이 바로 출격" 진입 버튼만 없다.** 필요하면 메인 메뉴에 02 버튼(기본 행성 즉시 출격, planetRun=false) 추가로 끝난다 — 사용자 결정 대기.
- **관전 모드 포팅** (사용자 요청 — 계획 §2.2 "보류" 를 해제) — 원본 `ai/observer.js` v7.8.1 규약 그대로. `Sim/Crew/ObserverPilot.cs`: 리더 오토파일럿(순수 C#, 가상 `PlayerInput`) — 판단 우선순위 탈출 포트 → 다운 크루 구조 → 전투(보스 18칸·위협 13칸, 교전 거리 LEADER_KIT) → 재장전 → 채굴(6/11/17칸·경계벽, AI 크루 목표 주변 1칸 회피, 16초 유지) → 대기, 보스탄 예고 회피 최우선, 경로는 `AiCrewSystem.FindPath`(digCost 5/14/12/10), 지형 게이트 `CrewGeo`(진척 없으면 14초 봉인, 거너 6s·그 외 2s), WASD 8방향 양자화(.38)·1.2초 스턱 지터. `Presentation/Observer/ObserverMode.cs`: Tab 시점 순환 · Esc = 리더면 해제 / 크루면 위치·직업 교대 후 해제(`TunnelSim.SwitchRoleMidRun` + `AiCrewSystem.SetRole` + `RunBootstrap.ApplyRoleSwap`) · F9/Tab 관전 복귀(EverUsed 런만) · F8 토글 · 진행 자동화 0.3s 틱(카드 1.5s·전설 2.1s 뒤 무작위 선택, 휴식 3.9s 뒤 `RunBootstrap.DoDescend`, 결과 4.5s 뒤 같은 편성 재출격, 메뉴 복귀 1.5s 뒤 자동 해제) · 상단 배지 · 관전 중 카드/직업 단축키 차단. 카메라는 `CameraRig.FollowOverride`(k=dt·5). 진입: 메인 메뉴 05 관전 모드(리더 = 마지막 선택 직업), 직업 선택 하단 버튼. 미이식: OBS_MANUAL 수동 빙의(원본에서도 데드 코드), `[ ]` 줌 통과(포팅본에 줌 키 없음).
- **리소스 전수조사 (2026-09-07, 사용자 요청)** — 포팅본 479개 파일을 md5 로 원본 `assets/`·`monster_assets_v1.5.4`·`tunnel_crew_tile_resources_v1` 와 대조하고, v7.9.2 HTML 이 실제 참조하는 30개 경로 + 동적 루트(시트 4종·몬스터·타일·특성 아이콘·크래프트 아이콘·red-fire-dragon)와 비교. 결과: 캐릭터 시트 40·몬스터 48·타일 278·특성 아이콘 20·크래프트 14·SFX 49·배지 4·BGM 3·로고 — 전부 원본 참조본과 바이트 동일. **레거시로 판명·교체**: ① 장악도 레일 보스 아이콘 `boss-icon.png`(1254², 미참조) → 원본 `infDomRailIcons` 의 `dom-boss-icon.png`, 크루 아이콘(초상화 사용 중) → `dom-crew-icon.png`(없으면 배지 폴백) ② 좌하단 바이탈 초상화 → 원본 `#infVitalsBadge` 와 같은 역할 배지 ③ 앞서 교체한 키아트·로고·직업 카드. **누락 발견·이식**: 보스 드래곤 스프라이트 — 원본은 `assets/red-fire-dragon/{idle 37, walking 37, fire-breath-a 26, death 24}` 를 10fps 로 재생하는데 포팅본은 몬스터 시트에 붉은 틴트만 얹고 있었다(M0 표 "자산 임포트 드래곤 미완"). `Art/Dragon/` 로 복사, `BuildArtAssets.RunDragon`(메뉴 "M7 · 보스 드래곤 시트 생성") 이 MonsterSheets 에 `dragon_*` 로 넣고, `EnemyView.DrawDragon` 이 원본 `bossDragonAnimation` 규칙(fireBreath > walking/idle · 키 변경 시 t=0 · 배속 `BossState.AnimRate` = 예고 baseLead/lead, 돌진 1.8) 으로 재생, 격파 시 몸을 남겨 death 24프레임(2.4s)·페이드. 틴트는 드래곤 프레임일 때 원색. **남은 참고**: 로딩 화면 아트 `loading-drill`(industrial-drill-electric-cyan) 은 원본 미참조지만 §2.3 신설 화면용으로 고른 것이라 유지. 드릴 WAV 3종·lobby-cave.ogg·keyart(webp→png)·portrait(축소) 는 변환본이라 md5 불일치가 정상.
- **폰트 적용 완료** — Pretendard 1.3.9(`Downloads/Pretendard-1.3.9/public/static` Regular·Bold OTF, OFL 라이선스 동봉) 와 ARCO(**`ARCO for OSX.otf` → `Fonts/ARCO.otf`**. 같은 zip 의 `ARCO.ttf` 는 손상 파일이라 Unity 가 대체 글꼴로 조용히 그린다 — 이름만 보면 ARCO 로 나와 속기 쉽다. 폰트 검증은 두 글꼴을 나란히 띄운 스크린샷으로) 를 `Data/Resources/Fonts/` 에 넣고 `Presentation/UI/Fonts.cs` 로 통일. IMGUI 는 각 OnGUI 첫 줄 `Fonts.ApplySkin()`(GUI.skin.font 교체 → 라벨·버튼·텍스트필드 전부, GUIStyle.font 가 비어 있으면 스킨 폰트를 쓰는 규칙) — RunBootstrap·MetaScreens·TeamOverlay·BossIntroCinematic·ObserverMode. TextMesh 는 크루 라벨·채팅 말풍선 = Pretendard Bold, **피해 숫자 = ARCO**(사용자 지정 · 원본 `@font-face 'ARCO'`) — 벽 피해·적 피해(`Damage`)뿐 아니라 `Text` 로 나가는 숫자 팝업(피격 -N · 재화 +N · 크루 +N, `CombatView.IsNumeric`)도 ARCO. 한글·문장 라벨만 Pretendard(ARCO 에 한글 없음). TextMesh 는 폰트를 바꾸면 MeshRenderer 머티리얼도 그 폰트의 것으로 바꿔야 한다(`CombatView.SetFont`).
- **메인 메뉴 02 무한 모드 직행** 추가(사용자 결정) — 행성 지도를 건너뛰고 직업 선택 → 출격(원본 `#menuInfinite` 흐름). 메뉴 번호 01 출격(행성 지도) · 02 무한 모드 · 03 성장 지도 · 04 유물 · 05 설정 · 06 관전 · 07 종료, 숫자키 1~5.
- **캐릭터 그림자** (사용자 요청) — 플레이어·AI 크루 발밑에 Light2D 캐스터만. (처음 함께 넣었던 고정 접지 타원 `DropShadow` 는 "조명에 따라 각도가 변하는 그림자만 필요" 라는 사용자 결정으로 삭제.) `WallShadowBuilder.AttachActorCaster`(발 둘레 12각형 ShadowCaster2D, selfShadows 끔) — 강한 점광원 앞에서 벽처럼 빛을 가린다. 둘 다 `PlayerView.Awake` 에서 붙어 크루도 자동 적용. **2차 조정(사용자: 서로의 손전등에 반응하는 그림자)** — ② 가 안 보이던 원인은 손전등 `shadowIntensity` 기본 .75 가 앰비언트에 묻힌 것(ON/OFF 픽셀 차 ≈10/255). 손전등·헤일로·크루 손전등 `shadowIntensity=1, softness .2`, 캐스터를 몸 폭(r×.9, 세로 .55, selfShadows on)으로 키움. **AI 크루 손전등 신설**(`_crewLights`, 사람 손전등과 같은 스팟 · 조준 방향 · 다운 시 소등) — 원본에는 없음. 결과: 크루가 플레이어를 비추면 플레이어 발에서 반대편으로 벽과 같은 그림자 줄기가 뻗는다(Captures/crew-flash-shadow-7-final.png). 검증 요령: 캐스터 ON/OFF 스크린샷의 픽셀 차 이미지(×6 증폭)로 그림자 형상을 확인.
- **조명·타일 버그 수정** (사용자 보고: 빛이 타일 경계에서 직선으로 잘림) — 원인 2가지, 전수 점검 결과 포함.
  ① **층 전환 시 벽 그림자 캐스터 누적**: `WallShadowBuilder.Bind` 가 층마다 새 `WallShadows` 루트를 만들면서 이전 층 것을 지우지 않아, 지층 3 에서 루트 5개·캐스터 2,145개(정상 ~365)가 겹쳐 있었고 그중 4,575칸분이 **새 층의 바닥 위**를 덮어 빛을 잘랐다. Bind 첫머리에서 이전 루트 파괴·더티 큐 초기화·옛 월드 이벤트 해제. 검증: 3층 하강 후 다음 프레임 루트 1 · 바닥 오덮음 0 · 벽 미덮음 0.
  ② **던전 랜턴도 첫 층 것만 생성**되어 층이 바뀌어도 옛 자리를 비추던 누락 — `RebuildLamps()` 로 층마다 재생성(랜턴 GO 30 → 6).
  ③ 벽 캐스터 `selfShadows=true` → 벽 타일 앞면이 자기 그림자에 잠겨 빛이 타일 앞에서 끊김. false 로(원본 wRim 처럼 광원 쪽 벽면이 밝다). 손전등 그림자 세기 1.0 → .9(할로 .85), 부드러움 .35 — 1.0 은 경계가 완전 검정 직선.
  점검했으나 문제 없던 것: 캐스터 사각형 좌표(셀 [c,c+1) 정확), 타일맵 3층 모두 Sprite-Lit, 광원 sorting layer, 어둠 오버레이 재바인드, 플레어 라이트 재사용, 보스 라이트. 남은 참고: 타일 노멀맵이 없어 원본의 벽 림(wRim)·음영(wShade)은 아직 없음(계획 §2.2 의 "노멀맵 + Sprite-Lit" 미착수).
- **타일 노멀맵 + 벽 림 조명** (사용자 요청 · 계획 §2.2 착수) — 최종 구성과 그 과정에서 확인한 URP 17 제약:
  - 노멀 생성: `Art/Tiles/purple/normals/<tile>_n.png` (Pillow · 둥근 베벨 폭 14px · 기울기 ×12 · 밝기 요철 .25, OpenGL +Y). 1차(×2.2, 광원 높이 3)·2차(×5.5, 1.5)는 "티가 안 난다".
  - **제약 1: 타일맵 청크 메시에는 NORMAL/TANGENT 가 없다.** URP `Sprite-Lit-Default` 의 NormalsRendering 패스가 TBN=0 을 만들어, 노멀맵 조명을 켜면 바닥·벽 전부 어두워지고 노멀맵은 무시된다. → `Assets/_Project/Shaders/Tilemap-Lit-Normal.shader`(Sprite-Lit-Default 사본, 노멀 패스에서 normal=(0,0,-1)·tangent=(1,0,0,-1) 강제). 세 타일맵(Floor/Walls/CoreTop) 모두 이 셰이더의 머티리얼(`RunBootstrap.BuildWorld`).
  - **제약 2: 렌더러가 `_NormalMap` 을 스프라이트 세컨더리 텍스처로 프레임마다 덮어쓴다.** 세컨더리가 없으면 평면 노멀이 들어가 머티리얼의 `_NormalMap` 은 무시된다(타일맵). → 벽 268타일을 **한 아틀라스**(`Art/Tiles/purple/atlas/purple_walls_atlas.png`, 54px 셀·2px 가장자리 복제, 918×864)로 묶고, 같은 배치의 노멀 아틀라스를 그 텍스처의 세컨더리 `_NormalMap` 으로 등록(`BuildArtAssets.RunWallAtlas`, 메뉴 "M7 · 벽 아틀라스 + 노멀맵"). TileSetAsset 슬롯은 아틀라스 서브 스프라이트를 우선 사용(`wallNormalAtlas` 필드 추가). Chunk 모드 유지 = 벽 전체 1배치. 타일별 세컨더리(`RunTileNormals`)는 남겨두되 효과 없음.
  - Light2D: `normalMapQuality=Accurate · normalMapDistance=0.8`(URP 17 은 읽기 전용 → `m_NormalMapQuality/m_NormalMapDistance` 리플렉션, `RunBootstrap.UseNormalMaps`). 노멀맵 모드는 평면의 N·L 을 ~0.3 으로 떨어뜨려 전체가 어두워지므로 손전등 1.35→2.6, 크루 손전등 1.1→2.0, 헤일로 0.9→1.4 로 보정.
  - 검증 요령(중요): 스크린샷 파일은 **선형** 값이라 미리보기(sRGB)보다 훨씬 어둡게 읽힌다 — 절대값이 아니라 A/B 차이로만 판단. A/B 는 `RunBootstrap.enabled=false` 로 루프를 멈춰야 한다(마우스가 조준을 계속 바꿔 손전등이 움직임 → 프레임이 어긋남). 합성 노멀(왼쪽 절반 광원 쪽/오른쪽 반대)을 씌운 흰 사각 스프라이트로 파이프라인 자체를 먼저 확인할 것.
  - 남은 튠: 림의 세기는 베벨 기울기·`normalMapDistance`·손전등 세기 세 값으로 조절. 바닥 시트·오버레이는 평면.
- **고정 마름모 그림자 최종 제거** — 접지 타원을 지운 뒤에도 남아 있던 것은 플레이어 헤일로(발 위 0.5칸 중심 광원)가 발밑 캐스터를 늘 위에서 비춰 아래로만 드리운 그림자였다. 헤일로 `shadowIntensity=0`. 이제 캐릭터 그림자는 손전등·랜턴·크루 손전등 방향에만 반응한다.
- 정리 — `.gitignore` 에 `unity/TunnelCrew/Captures/`·`unity/TunnelCrew/*.png`, ProjectSettings 의 `2D_URP` 흔적(metroPackageName/Description) 제거. MCP for Unity 패키지를 개발용으로 매니페스트에 추가(10.1.2 커밋 고정, 배포 전 제거 가능). 이 PC 환경 메모: 에디터 6000.3.15f1 · MCP 조작은 정션 `C:\Users\Loadcomplete\TunnelCrew` 로 열어야 함(서버가 한글 경로 상태 파일을 cp949 로 읽어 실패).

**남은 작업**

1. **M7 체크포인트** — 사용자가 원본과 나란히 10분 플레이: 드릴 루프/벽 파괴/사격/보스 BGM 전환 소리, AI 크루 동행감, 핑(G/V)·채팅(Enter)·제작(C) 조작감, 보스 등장·격파 연출 타이밍. 게임패드 리매핑은 실기 패드로 캡처 흐름 확인 필요(이 PC 에 패드 미연결).
2. **UGUI 정식 배치** — 현재 IMGUI(1080p 배율)로 전 화면이 동작한다. IMGUI 확정 vs UGUI 이전은 사용자 결정 대기(2026-09-07 차이 설명 전달).
3. **M8 코옵** — SimCommand 큐를 네트워크 입력으로 확장, 호스트 권위·시드 동기화. 착수 시 원본 알려진 불일치(게스트 유물 미적용, 보스탄 경감 이중 적용) 재설계. **착수 여부는 사용자 결정 대기.**
4. 소소한 정리 — 게임 뷰 라벨(CrewView TextMesh) 크기 튠(주관 항목, 체크포인트에서 함께 판단), persistentDataPath 변경(`Tunnel Crew Team/Tunnel Crew`)에 따른 M5 이전 세이브 마이그레이션은 필요 시.


### M0 — 준비 (추출·골격)
- Unity 6 LTS 선택 — 이 PC에 Unity Hub와 6000.0.69f1 · 6000.3.15f1, .NET SDK 10이 이미 설치되어 있음(2026-09-06 확인). **6000.3.15f1(Unity 6.3 LTS)** 사용. URP 2D 템플릿으로 `unity/TunnelCrew` 생성, asmdef 3개(Sim / Presentation / Tests)
- §4 추출 스크립트 전부 실행, `data/*.json` + 자산 임포트 완료
- `GameFlow` 빈 상태 머신, 빈 씬 3개(Boot / Menu / Run)
- **완료 기준**: 에디터에서 타일 아틀라스·캐릭터 시트·오디오가 임포트 오류 0으로 보이고, SO 데이터가 원본 값과 일치(스팟체크 20개)

**진행 상황 (2026-09-06)** — 커밋 `f704570`

| 항목 | 상태 |
|---|---|
| Unity 6.3.15f1 + URP 2D 프로젝트 `unity/TunnelCrew` | 완료. 불필요한 서비스 패키지 18종 제거(39개 남음) |
| asmdef | 완료 (계획의 3개 → **6개**: Sim / Data / Presentation / Editor / Tests.EditMode / Tests.PlayMode). Data 를 분리한 이유는 Sim 을 `noEngineReferences=true` 순수 C# 으로 유지하기 위함 |
| `dump-tuning.mjs` | 완료. **44 / 44** 추출 |
| `extract-embedded-audio.mjs` | 완료. **53 파일** (SFX 25종/48, 드릴 3, BGM 2) |
| 자산 임포트 (타일·캐릭터·몬스터·드래곤) | **미완** |
| 원본 JSON → ScriptableObject 변환 | **미완** |
| 빈 씬 3개 (Boot / Menu / Run) + 빌드 설정 | 완료 |
| `GameFlow` 상태 머신 골격 | 완료 (`GamePhase` 12상태, 3중 상태 통합) |
| 검증 | 배치 모드 컴파일 에러 0 · 경고 0 · EditMode 테스트 **11/11 통과** |

부수 성과: `Sim/Math/Rng.cs` 가 원본 mulberry32 + FNV-1a 를 비트 단위로 재현하는 것을
JS 기준값과 대조해 확인했다(`RngParityTests`). §6 맵 생성 패리티의 전제가 성립한다.

**계획과 달라진 점**
- 특성 수는 기획 문서의 "약 60종" 이 아니라 **72종**이다. M4 수작업 표의 작업량을 이 숫자로 잡는다.
- `Sim` 은 `noEngineReferences=true` 로 UnityEngine 을 전혀 참조하지 않는다.
  `System.Numerics` · `System.MathF` 를 쓰고, ScriptableObject ↔ Sim 구조체 변환은 `Data` 가 맡는다.
- 바이너리 자산은 `unity/TunnelCrew/.gitattributes` 로 **Git LFS** 추적한다(범위는 이 폴더 한정).

### M1 — 코어 루프 그레이박스 (파고, 걷고, 줍는다)
- `WorldGrid`, `Rng`, `DungeonGenerator`(genTunnel 11단계), `Connectivity`
- `Movement`+`Collision`(원-AABB 3회), 대시(4px 서브스텝→0.08셀), 넉백·기절
- `Mining`: 드릴 3점 샘플, 예열/과열, 암반 반동, `damage()` 규칙, 손상 4단계 타일 스왑, nudge
- `Loot`: z축 포물선·바운스·자석·획득, `RES_MAX` 상한
- `CameraRig`: 데드존 111px→2.22셀, 룩어헤드 2.78셀, 동적 줌, 세로 42% 앵커, 클램프
- 렌더: Tilemap 2장 + 암반 3패스, 드릴러 8방향 시트 1종, 임시 HUD(HP·pulp·bloom)
- **완료 기준**: 시드 고정 맵에서 WASD·LMB·Space로 3분 플레이. §6 맵 생성 패리티 테스트 통과

**진행 상황 (2026-09-06)**

| 항목 | 상태 |
|---|---|
| `Rng` (mulberry32 + FNV-1a) | 완료. 원본과 **비트 단위 일치** |
| `DungeonGenerator` (genTunnel 11단계) | 완료. **패리티 통과** — 심층 1~3 에서 5,760칸 전부 일치 |
| `WorldGrid` · 타일 체력 · 손상 4단계 | 완료 |
| `CollisionSystem` (원-AABB 3회 + 벽 탈출) | 완료 |
| `MovementSystem` (대시 서브스텝 · 넉백 · 암반 반동) | 완료 |
| `MiningSystem` (3점 샘플 · 예열/과열) | 완료 |
| `LootSystem` (z 포물선 · 바운스 · 자석) | 완료 |
| `CameraRig` (데드존·룩어헤드·동적줌·클램프) | 완료 (원본 공식 직접 이식 — 아래 참조) |
| 렌더: Tilemap + 타일셋 268슬롯 | 완료. 배치 모드 렌더 캡처로 확인 |
| 드릴러 8방향 스프라이트 | 완료 (좌측 5방향 + X반전) |
| 임시 HUD | 완료 (IMGUI) |
| **사람의 3분 플레이 확인** | **대기 — 사장님 확인 필요** |

검증: EditMode **42/42 통과**, 컴파일 에러·경고 0.
클래스별 — 맵 패리티 14 · 난수 패리티 11 · 시뮬레이션 스모크 9 · 튜닝 패리티 8.

스모크 테스트가 자동으로 확인하는 것: 진입점이 벽 속이 아님 · 무입력 시 정지 ·
이동 속도 상한 · **20초 무작위 이동에도 벽에 갇히지 않음** · 드릴로 블록이 부서지고
재화가 자석으로 들어옴 · 과열 잠금과 해제 · 대시가 걷기보다 멂 ·
**60fps 와 30fps 결과가 동일**(고정 틱) · 60초 연속 주행 무예외.

**계획과 달라진 점**
- 카메라를 Cinemachine 이 아니라 원본 공식 그대로 이식했다. M1 의 목표가 "원본과 같은
  감각인가" 확인이라 먼저 같은 수치를 재현하고, Impulse·Target Group 이 실제로 필요해지는
  **M3 에서 Cinemachine 으로 이전**한다. analysis-04 §5 의 대응표가 이전 기준이 된다.
- 씬을 손으로 꾸미지 않고 `RunBootstrap` 이 코드로 세운다. 재현 가능해야 배치 모드에서
  검증할 수 있기 때문이다. 실제 씬 구성은 조명·HUD 가 붙는 M2 이후에 잡는다.
- 아트는 M1 에 필요한 것만 임포트했다(퍼플 타일 268 + 드릴러 시트 10, 4.3MB).
  나머지는 `import-art.mjs --all` 로 M3 에서 가져온다.

### M2 — 시야·조명
- `LosService` + LOS 텍스처, 시야 전환 보간
- Light2D 앰비언트/손전등(F 토글)/랜턴 6개/플레어, 노멀맵
- LX Renderer Feature(4레이어), `LxPreset` SO에 v7.9.1-lx 확정값
- 벽 그림자(Shadow Caster 2D)
- **완료 기준**: 원본 스크린샷과 나란히 놓고 어둠 농도·기억 잔존·손전등 원뿔이 시각적으로 대응. 렌더 컬링(카메라 ±1셀, softSeen pad 2) 적용

**진행 상황 (2026-09-06)** — 커밋 `af87db4`

| 항목 | 상태 |
|---|---|
| `LosService` (360레이 · 19타일 · 기억 11타일) | 완료. 농도 수치 원본과 일치 |
| 어둠 셰이더 + 시간 보간(rise .16 / fall .38) | 완료 |
| Light2D 전역광 · 손전등(F) · 랜턴 6개 | 완료 |
| Volume 포스트프로세싱 | 완료. 지층 3 + 이상지대 프로파일 4개 |
| Shadow Caster 2D (벽 그림자) | 완료. 캐스터 365개, 청크 단위 재생성 |
| 렌더 컬링 | **불필요로 판정.** 아래 실측 참조 |

**성능 실측 (2026-09-06, RTX 3050 Ti)**

| 항목 | 시간 |
|---|---|
| Sim 1틱 | 0.0011 ms |
| 시야 전체 재계산 | 0.0266 ms |
| 어둠 텍스처 5,760셀 | 0.0373 ms |
| 합계 | 프레임의 약 1.3% |

Tilemap 이 이미 청크 단위로 컬링하고 CPU 비용이 무시할 수준이라
원본의 컬링 규칙(카메라 ±1셀, softSeen pad 2)을 옮기지 않는다.
필요해지면 그때 넣는다. 그림자 캐스터 365개의 비용도 측정 노이즈 범위였다.

**사장님 피드백 (2026-09-06)**
- **완전 암흑 구역의 톤이 마음에 들지 않는다.** 사장님이 직접 조정하기로 했다.
  조정 지점은 세 곳이다:
  `RunBootstrap._ambientIntensity`(전역광 0.16) ·
  `DarknessOverlay._darkColor` / `_memoryColor` / `_maxDarkness`.
  이 값들을 바꾸는 것으로 해결되지 않으면 어둠 셰이더의 `light` 합성식을 손봐야 한다.
  **이 항목은 대신 고치지 말 것.**

### M3 — 전투와 4직업
- `Projectile` 11종 규칙, `EnemyShot`, 예고형 `BossShot`(보간 + 착탄 원)
- 적 3종 + apex: 스폰 링, 원뿔 시야, FSM(windup/strike/recover), 원거리 keep, 도약, 소프트 분리, 덩치=속도, 상태이상(스턴·빙결·슬로우)
- `Combat`: `hurtEnemy` 규칙(즉시 각성, 넉백 공식 hitPower^0.72×rangePower, apex 저항 0.52, 보스 면역), `applyPlayerDamage`(플레이 중 ×0.85, 방어막 ×0.35), i-frame, 상호 부활(5초·1.6셀·50%)
- 4직업 스킬 8종 + 거너 파쇄탄 + 드릴러 균열 + 엔지니어 급전 규칙 + 설치물(센트리·전력노드)
- 히트스톱·킥·스쿼시(FEEL), 파티클(J 대체), 데미지 텍스트
- 몬스터 16프레임 클립, 4직업 시트
- **완료 기준**: 4직업 각각 30초 전투 스모크. 적 HP/속도/공격 간격이 데이터 값과 일치

**진행 상황 (2026-09-06)** — 사장님 확인 대기 (M3 체크포인트)

| 항목 | 상태 |
|---|---|
| `ProjectileSystem` — 탄 규칙(관통·도탄·폭발·레이저), 벽·적 피해, 탄창·재장전 | 완료 |
| `EnemySystem` — 스폰 링, 원뿔 시야, FSM, 원거리 keep, 도약, 소프트 분리, 덩치=속도, 상태이상 | 완료 |
| `hurtEnemy` / `applyPlayerDamage` 규칙 (넉백 공식, apex 저항, ×0.85, 방어막 ×0.35, i-frame) | 완료 |
| `RoleSystem` — 4직업 Q/E 8종, 거너 파쇄탄(좌클릭·E 기폭), 드릴러 균열·돌파, 엔지니어 노드·센트리 급전, 스카우트 플레어·그래플 | 완료 |
| 드릴러 기반암 처리 | 원본 7252행대로 **튕기지 않고 물어서 균열을 쌓는다** (`MiningSystem.onBedrock` 훅) |
| `Feedback` 컴포넌트 — 히트스톱(`Time.timeScale`, D6), 카메라 킥/흔들림, 스쿼시·리코일, 3단계 피격 | 완료. Feel 대체(analysis-04 §3.2) |
| `EnemyView` — 16프레임 클립(walk/idle/blink/sprint), 크기 r×3.15, 체력바, 선딜 텔레그래프, 피격 플래시 | 완료 |
| `CombatView` — 투사체·적탄·설치물·데미지 텍스트 | 완료 (절차 생성 스프라이트 그레이박스) |
| 몬스터 3종 × 16프레임, 4직업 시트 임포트 + `MonsterSheetAsset` | 완료 |
| 플레이어 뷰 — 직업별 시트, 무적 깜빡임, 기절·다운 색 | 완료 |
| 조작 — Q/E 스킬, R 재장전, 우클릭 사격, 1~4 직업 교체, F5 층 재생성 | 완료 |
| 예고형 `BossShot`, 상호 부활 | **M4 로 이월** (보스·크루가 있어야 의미가 있다) |
| 파티클(Shuriken 프리셋), 스프라이트 셰이더 피격 플래시 | **M7 연출 단계로 이월**. 지금은 색 틴트로 대체 |
| Cinemachine 이전 | 보류. `CameraRig` + `Feedback.CameraOffset` 으로 충분해 이전 이유가 아직 없다 |
| 테스트 | `CombatTests` 14개 (사격·재장전·스킬 8종·파쇄탄·균열·층 전환 정리) |

**사장님 확인 요청** — 4직업을 각 30초씩 플레이하고 손맛을 판정:
플레이 모드 진입 → 숫자키 1(드릴러) 2(거너) 3(스카우트) 4(엔지니어). 시작 시 적 4마리가 근처에 깔린다.
확인 포인트: 히트스톱 길이, 킥 세기, 적 텔레그래프의 읽힘, 거너 파쇄탄 부착감, 드릴러 균열 속도, 센트리 급전 반경.
튜닝 지점: `Feedback` 인스펙터(`_hitstopScale` .055 · `_hitstopCapMs` 95 · `_kickCellsPerUnit` .045 · `_kickDecay` 9),
`RunBootstrap._prespawnEnemies`.

### M4 — 런 구조: 장악도 → 보스 → 하강 → 탈출
- `RunState`: 지층 3 + 이상지대 배율, `Threat`/`EnemyCap`/`SpawnInterval`/`SpawnBurst`/`spawnDebt`
- `Dominance`: 층 파괴 가능 블록 집계, 목표 도달 → 보스 소환
- `XpGate` 단일 관문 + 역할 가중치 + 층당 트리클 상한 60, xpNeed 곡선, `TraitDeck`(티어 가중치·피티·리롤), **특성 ~60종 효과를 수작업으로 `PlayerBuild` 필드 변경 표로 옮김**(원본 `a()` 함수를 한 줄씩 읽어 표 작성 — 가장 손이 많이 가는 항목)
- 보스: 3티어, 장갑 링·페이즈, 소환, 돌진 FSM(텔레그래프·충격·기절·천장 붕괴), 예고탄 2종, 벽 기믹 3종, 감옥, 이동 파쇄
- 등장/사망 시네마틱(레터박스·카메라 보간·이름 플레이트·포효 프레임 동기), 전설 카드 3택 휴식 화면
- `Escape` 5단계, 착륙 지형 파괴, 전원 탑승, 생환/사망 분기
- 무한 HUD 전체(장악도 레일·위협·보스 HP·XP 바·탄창·역할별 빌드 텍스트·스킬 슬롯·키가이드)
- **완료 기준**: 1층 진입 → 장악도 → 수호자 → 하강 → 2층 → 탈출 성공/사망 → 결과 화면까지 한 사이클 (기획서 §19.3 스모크 기준)

**진행 상황 (2026-09-06)** — 자율 진행 중

| 항목 | 상태 |
|---|---|
| `Planet` / `RunState` — 지층 3 + 이상지대 배율, 장악도, 위협·캡·간격·버스트, spawnDebt | 완료. 수치 원본과 일치 (테스트) |
| `XpGate` — 단일 관문, 역할 가중치 표, 층당 트리클 상한 60, xpNeed = 30+10L+2.4L², 보스 중 적립만 | 완료 |
| `BossSystem` — 3티어(수호자/포식자/변종), 장갑 링·페이즈(66%·33%), 소환, 돌진 FSM(예고→돌진, 밀치기·기절), 예고탄 2종(산발·연속, 기 모으기), 벽 기믹 3종(융기·파동·감옥) + 협곡 돌진, 벽 뭉개기(장악도 미집계), 격파 보상·회복 | 완료 |
| 천장 붕괴 연출(dashCeiling) | 이월 — 순수 연출. M7 |
| `EscapeSystem` — X 지정(6칸) → 도착 대기(20+7·심층, ≤60) → 착륙 파괴 1.45칸 → 탑승 1.25칸·1.2초, 보스(수호자 제외) 처치 시 자동 요청 | 완료 |
| 런 흐름 — `GamePhase` Playing/Rest/Result, 보스 격파 → 2.6초 → 휴식(전설 3택 필수) → 하강 / 귀환, 다운 → 결과, 탑승 → 결과 | 완료 |
| `TraitDeck` — 카드 75장 표(역할 32·공용 27·해금 16) + 전설 6, 등급 가중치(목표 레벨 대비), 피티, 리롤(지층 +1, 상한 2), 풀 소진 예비 보급 | 완료. **원본 `a()` 를 한 줄씩 옮긴 표** |
| 직업 튠 값 → `PlayerBuild.Roles`(런 단위) 이동 | 완료. 특성이 층을 넘어 유지되도록 `RoleSystem` 이 매 층 새로 만들어져도 값이 산다 |
| 공용 특성 서브시스템 8종 (보조 드릴·잔상·위성·소용돌이·군단·행성 파쇄기·대붕괴·폭풍) | 완료 — 원본 infUpdateAuxDrills(범위 30% 축소, 쿨 max(.12,.48-n·.035)) · afterHits(.32초 뒤 1.25/.25 폭발) · vortex(3블록마다 5칸 끌기 + 2.25/.36) · infPlanetBreaker(10×5 띠 .375, HP 1.5%) · 대붕괴(3.5/.3375, HP 2.5%) · infTraitRiskDamage(비치명 HP 1 잔존). 궤도 드릴 비트 8개 그리기 |
| 공용 특성 값은 있으나 **소비처 미연결** | DrillWidth/Penetration/FocusDrill(MiningSystem) · BreakShock/ShardBurst/BreakShield/ChainCollapse/AutoDigEvery(파괴 훅) · LootMagnet/Pickup(LootSystem) · DrillMoveMul(Movement) · HeatBuildMul/EndlessOverdrive(과열). M4 후속 |
| 시네마틱(등장·사망 레터박스, 카메라 보간, 이름 플레이트) | 이월 — M7 연출 |
| 무한 HUD 전체 | IMGUI 임시 (장악도·위협·보스 HP·XP·탄창·스킬·탈출·카드·휴식·결과). UGUI 배치는 M5 |
| 테스트 | RunStateTests 10 · BossTests 9 · EscapeTests 8 · TraitTests 9 |

| 공용 특성 소비처 연결 | 완료 — 드릴 폭·관통·집중(MiningSystem), 굴진 가속(Movement), 자원 흡입(Loot), 보호막·충격파·파편·자동 굴착탄(파괴 훅), 무정지 과급(과열 무시·HP 소모) |
| 보스 조명 | 완료 — Light2D 반경 r×2.4, 세기 .5, 맥동 4% (원본 BOSS_TUNE) |
| 한 사이클 스모크 (플레이 모드, 2026-09-06) | **통과** — 장악도 22.1% → 수호자 소환(HP 3447, r 2.49) → 원거리·벽 소환 패턴 → 격파(코어 +3, HP 30% 회복) → 2.6초 → 휴식: 레벨업 카드 → 전설 3택 → 하강 → 지층 2(벽 ×1.35, 적 ×1.4), 레벨·특성·코어 유지, 스포너 재초기화 |

**M4 남은 것**: 없음 — 시네마틱·천장 붕괴는 M7, HUD 정식 배치는 M5.

**사장님 피드백 (2026-09-06, M4 1차)** — "재미 재검증 말고 프로토타입 재현도를 볼 것." 즉각 체감 차이 3건:
| 지적 | 원인 | 조치 |
|---|---|---|
| 카메라 흔들림 과함 | 킥 단위를 0.045셀/단위로 잡고 스프링 감쇠(9/s)를 써서 원본(px 단위, `shake*=.035^dt`, 약 0.3초)보다 4배 크고 3배 오래 흔들렸다 | `Feedback` 를 원본 J.kick/J.step/8082행 공식으로 교체: 1단위=1px(0.02셀), `pow(.035,dt)` 감쇠, `cos(sph)·exp(-sph·.16)` 방향성 사인 + 16% 노이즈, 진행 중 흔들림의 72% 미만 킥은 무시 |
| 드릴 타격감 없음 | 드릴 비트에 숫자만 있고 파티클 0, 숫자도 작고 정적 | `FxSystem`(Shuriken 3 + 절차 링/플래시/사각/별) — 원본 DEMO 수치 그대로: 비트마다 스파이크 13·돌덩이 10(412px/s)·불꽃 14(327), 파괴 시 돌덩이 22·불꽃 26·연기·링·플래시·별. 데미지 숫자는 원본 J.dmg 그대로(25/34px, 팝 1.45, 스쿼시 .22, 상승 120px/s + 중력 980, 회전, 그림자) |
| UI 작고 이미지 없음 | IMGUI 14px 고정 | 1080p 기준 k 배율(폰트도 k 배로 굽어 선명), 상단 장악도 레일(크루 초상화 → 보스 아이콘) + 위협, 좌하단 초상화·HP·탄창·드릴 열, 우하단 스킬 슬롯(쿨다운 가림), 하단 XP 바, 카드 400×250 3장(등급색·초상화), 휴식/결과 오버레이 |

**사장님 확인 요청 (M4 2차)** — 위 3건이 프로토타입 느낌에 가까워졌는지. 남은 차이: 폰트(Pretendard 미도입, 유니티 기본), 사운드(M7), 카드 아이콘(원본 SVG → M5).

**사장님 확인 요청 (M4)** — 한 사이클을 직접 돌려보기:
플레이 모드 → 벽을 부숴 장악도(HUD 2줄째)를 22% 까지 → 수호자 출현 → 처치 → 휴식 화면에서 카드(1·2·3) → Enter 하강.
X 로 탈출 포트 지정 → 좌클릭 확정 → 20초 후 도착 → 포트 위 1.2초 탑승 → 결과 화면.
확인 포인트(재현도): 보스 예고탄·벽 기믹이 원본처럼 읽히는지, 카드 등급 분포, 휴식 화면 정보량.

### M5 — 메타: 정산·영구 노드·유물·저장·메뉴
- `SaveData`(`tc_infinite_meta_v1` 스키마) JSON → `persistentDataPath`, 설정 저장
- 정산 3뷰(요약·성장 지도·유물 보관고), 영구 노드 80개 지도(팬·줌·발견 연출·랭크·캡 클램프·구매 실패 롤백)
- 유물 33종 + 원소 공명 + 소켓 5
- 메인 메뉴(패럴랙스 키아트), 직업 선택, 행성 지도(11행성, 잠금 10), 화면 전환 와이프(260/40/340ms)
- **새로 설계**: 일시정지, 로딩(assets/loading 아트 사용), 해상도 옵션
- **완료 기준**: 3런 연속 플레이 후 코어·노드·기록이 재시작 후 유지. 메뉴→런→정산→메뉴 왕복

**진행 상황 (2026-09-06)** — 자율 진행 중

| 항목 | 상태 |
|---|---|
| `MetaState` — `tc_infinite_meta_v1` 스키마(기록·보관 코어·해금 3종·영구 랭크·유물 소켓 5) + `Sanitize` | 완료. 레거시 마이그레이션(v7.2/7.3)은 넣지 않는다 — 유니티 세이브는 v1 부터 |
| `MetaStore` — `persistentDataPath` JSON(원자적 저장), 구매 실패 롤백 | 완료 |
| `PermanentNodes` — 80노드·140랭크·코어 760 (슬롯 골격 i→m1·m2→m3·m4→m5·m6→cap, 방사형 좌표), 선행·공개(reveal)·구매·누산·상한 클램프·런 반영 | 완료. **원본 INF_NODE_DEFS 를 한 줄씩 옮긴 표**, 테스트로 합계 검증 |
| 런 반영 — 영구 노드 → `PlayerBuild`/`RoleTuning`, 카드 계열 해금, 리롤 재고, 출격 프리셋 카드, 긴급 재기동(HP 35%·런 1회), 원격 전송(코어 N개마다 1개 보존) | 완료 |
| 정산 — 생환 시 전부 보관, 다운 시 keepRate/keepMin/원격 전송 중 큰 쪽 보존 (§8.2), 기록·해금(도탄·발파·돌파) | 완료. 결과 화면에 표시 |
| 희귀 광물 → 코어 (+추가 코어 확률), 광석 회복 | 완료 (원본 infOnBlockBroken rare 분기) |
| `Relics` — 33종 표, 소켓 4+왕관, 자동 장착(가장 오래된 칸 교체), 원소 공명(2→1단·3→2단·융합로 +1) | 완료 |
| `RelicSystem` — 효과 33종: 피해 배율(송곳니·배수진·지층의 기억·심연·용암·영구동토·뇌전 공명), 상태이상(맹독·화상·서리·감전·급속 냉동·들불·냉기 폭발), 방어(젤·암반 피부·회중시계·피뢰침·모래시계 시간 정지), 처치(흡혈·피의 계약·유폭), 월드(공명석·사태 유발자·자철석·도시락), 부활(불사조), 태엽 수호자 드론, 발굴(광맥 1.5%+·피티 260·보스 35/60%·묻힌 유물 50%·탐지기), 밀수꾼 정산 | 완료 — 원본 infRelic* 수치 그대로. 훅은 EnemySystem 의 대리자(EnemyDamageMod/KnockMul/WallSlam/PlayerDamageMod/AfterPlayerHurt/TimeStopped) |
| `MetaScreens` — 메인 메뉴(키아트·기록 한 줄) · 행성 지도(11행성, 잠금 10 실루엣) · 직업 선택(4카드: 코드·일러스트·태그·장비·배율·영구 랭크) · 귀환 정산 3뷰(요약 / 성장 지도 팬·줌·툴팁·클릭 구매·발견 연출·랭크 핍 / 유물 보관고 소켓·자동 장착·공명) · 일시정지(Esc, 계속·포기) | 완료 (IMGUI 임시, 1080p 배율). 흐름: 메뉴→행성→직업→런→결과(Enter)→정산→메뉴 |
| `RunBootstrap` 게이트 — `RunActive`/`Paused`, `LaunchRun`/`SuspendRun`, 결과 Enter → 정산 이벤트 | 완료 |
| 화면 전환 와이프 — 원본 TCFX.wipe 규칙(cover 260 · hold 40 · reveal 340 ms, 사다리꼴 시트, 뒤로 가기 반전, 감속 모드·전환 중이면 생략), 모든 화면 전환이 hold 시점에 상태를 바꿈 | 완료 (m5_wipe.png) |
| 로딩 화면 — 출격 와이프 hold 구간에 `assets/loading/industrial-drill-electric-cyan` 회전 + "직업 출격 — 지층 생성 중" | 완료 |
| 설정 화면(메인 메뉴 04) — 해상도(`Screen.resolutions`) · 전체 화면 · 감속 모드(Feedback.ReducedMotion 연동) · BGM/SFX 볼륨(M7 오디오가 `MetaScreens.BgmVolume/SfxVolume` 로 읽음), PlayerPrefs `tc.*` 저장 | 완료 (m5_settings.png) |
| UGUI 정식 배치 | 보류 — IMGUI 1080p 배율로 유지, Pretendard 폰트 도착 후 M7 에서 함께 교체 |
| 테스트 | MetaTests 8 · RelicTests 11 |

### M6 — AI 크루·핑·채팅·퀵크래프트

#### 진행 (2026-09-06)

| 항목 | 상태 |
|---|---|
| `CrewKit` — 역할 행동 예산 4종(원본 KIT 표 그대로: 드릴러 dig 1.35·pathDigCost 5 / 거너 gun 1.55·intercept 13·breakerCd 9 / 스카우트 alert 17·flare 7 / 엔지니어 turret 2·node 2), `CrewPersona`(성향 12항목·직업별 중심 이동) | 완료 |
| `CrewGeo` — AIGEO 지형 게이트: 열린 공간 성분(월드 버전 기준 재계산)·도달성·접촉(대각 기반암 금지)·봉인·경계벽·진척 감시 | 완료 |
| `AiCrewSystem` — 편성(최대 3, 런 사이 유지)·런 시작/층 재배치·다익스트라 `FindPath`(벽 = 뚫는 시간 비용, 동료 점유 +3)·이동/분리/대시·의도 게이트(절대시각)·기분·딴짓·potshot·decide 14목표(리더 구조 → 탈출 → 동료 구조 → 합류 → 보스/교전(팀 인지) → 직업 임무(센트리/전진 배치/급전 · 정찰/펄스/플레어 · 기반암 균열) → 재화 회수 → 재장전 → 채굴(claims 회피·frontier) → 경계/대기)·act·전투 지원(센트리/급전·조명/펄스·방어막/차폐 제거/파쇄탄 공격)·보스탄 회피(예고 1.8s·원 밖 1.3배·0.6s 전 대시 80%)·적탄 옆대시·파묻힘 탈출/워프·stuck 감시 | 완료 |
| 개인 성장 — 사람과 같은 XP 곡선·역할 가중치, 정찰 XP 는 새 구역만, 트리클/설치 상한 60, `CrewTraits` 30장(티어 = 레벨) + 숙련 대체 | 완료 |
| 밸런스 분리 — AI 굴착은 `TunnelSim.BreakSource` 로 크레딧(장악도·코어 기여, 사람 XP·특성 발동 없음), AI 탄은 `Projectile.Owner/AiMul`, 처치 XP 는 `EnemySystem.DamageSource`, AI 설치물은 별도 목록 | 완료 |
| 적 표적 — `EnemySystem.TargetOf`(가장 가까운 크루, 0.6s 이력, 기절한 사람 제외) · 근접/원거리 타격이 크루에게 감 · 보스탄 크루 착탄 | 완료 |
| 상호 구조 — 크루 다운(5초 치료 → 50%) · **사람 기절**(원본 v7.7.2c: 구조자가 있으면 런이 끝나지 않고 5초 치료 → HP 50%, 없으면 종료) | 완료 |
| 탈출 — 생존자 전원 탑승(`EscapeSystem.CrewAllAboard`), HUD 에 탑승 인원 | 완료 |
| 시야 — 크루 위치·플레어/노드 visionRange 를 LOS 광원으로 합산 (원본 AI.visionXY · lamps.visionRange) | 완료 |
| 표현 — `CrewView`(사람 시트 재사용, 라벨/HP/상태, 센트리·노드·급전선·균열·정찰 표식), HUD 우상단 크루 패널, 기절 오버레이, 직업 선택 카드 [− n + AI] 칩 + 편성 바 | 완료 (m6_roleselect.png · m6_crew.png) |
| `PingSystem` — 핑 9종(색·글리프·우선순위·명령형), 휠 8방향(↑가자 ↗공격 →발견 ↘채굴 ↓후퇴 ↙방어 ←도움 ↖위험), 컨텍스트 판정(적/기절 크루/탈출 포트/미탐사=알 수 없는 위치/광맥·벽), 스택·동의·자기 마커 취소, 도배 제한(충전 4 · 2.5s 회복 · 10s 6회 → 5s 잠금 · 기절 도움 6s), 3줄 로그 병합, AI 명령 주입(가자/공격/채굴/후퇴/방어/발견/도움 · 위험 구역 3칸 5초 + Geo 봉인 · 같은 좌석 명령 교체 · 우선순위 가로채기 · ✓ 수락 표시), AI 자발 핑(전역 4s · 멤버 10~22s · 초당 35%) | 완료 |
| `TeamOverlay` 핑 표현 — G 탭/홀드 휠(120ms·18px·데드존 24px·반경 96px)·V 위험, 월드 마커(팝·바닥 링·기둥·글리프·×스택·+동의·1.2s 라벨), 화면 밖 화살표(중요도순 3, 위험·도움·후퇴는 거리 무관), 로그·거절 노트, 휠 중앙 충전/잠금 표시 | 완료 (m6_ping_chat.png) |
| `CrewChat` — Enter 열기/전송/Esc 초안 보존, 80자·제어문자 정리·250ms 간격, 좌하단 로그(평소 3줄·10s 페이드 / 열면 8줄), 머리 위 말풍선(5s·0.5s 페이드·안개 속 동료 위치 비노출), AI 멘트 10문장(전역 15~30s·긴급 6s·문장당 2회·크루당 20s·한산 20s/60s) | 완료 |
| `QuickCraftSystem` — 6레시피(성형 폭약·자동 포탑·냉각 캡슐·접이식 방벽·응급 주사·휴대 조명탄) 비용/한도/쿨다운/불필요 사유, 즉시(냉각 75%·잠금 해제)·주입(0.7s·35%·피격 취소 환불·이동 45%·장비 차단)·배치(타일 스냅·사거리·발밑 금지·발자국·겹침), 설치물 갱신(포탑 3.8칸 .48s 위력 .62 · 방벽 적 밀어냄/적탄 삼킴 · 조명탄 30s 시야 3 · 성형 폭약 2s 벽 180%·적 ×2.2) | 완료 |
| `TeamOverlay` 크래프트 표현 — C 홀드(0.18s)/탭 토글, 마우스 60° 슬롯(데드존 54px)·1~6·Space/클릭, 원형 휠(connector·slot·selection 아트)·상세 카드(9-slice detail-panel·craft-button·글리프)·배치 미리보기(타일 사각·아이콘·라벨), `CraftView` 월드 아이콘 + 수명/내구 링 | 완료 (m6_craft.png) |
| 입력 우선순위 — 채팅 중 모든 키는 글자(F 손전등 포함), 핑 휠·크래프트 휠·배치 중 좌우클릭 차단, 휠에서 Space 는 확인, 카드 숫자키보다 크래프트 숫자키 우선, Esc 는 오버레이가 먼저 먹는다 | 완료 |
| 미포팅 | 관전 모드(OBSERVER — 계획 범위 밖), 코옵 송수신(M8), 핑 음형·크래프트 SFX(M7 오디오에서 `Ping.Sound`/`Craft.Sfx` 이벤트로 연결) |
| 테스트 | CrewTests 9 · TeamTests 7 (핑 충전/잠금 · 컨텍스트/스택/취소/미탐사 · AI 명령/위험 구역 · 채팅 정리/말풍선/AI 멘트 규칙 · 레시피/사유/즉시/주입 · 배치 스냅/사거리/한도/포탑 사격 · 성형 폭약/방벽) — 전체 134/134 |
- `AiCrewMember`: KIT 4종, Persona 롤, 기분, IntentGate(절대시각), decide 14목표 우선순위, act, 다익스트라 `pathDigCost`, 파묻힘 탈출·워프, 다운/부활, AI 성장 분리(`AI_TRAITS`), 소유권 기반 XP 귀속
- 보스탄 회피, 적탄 옆대시, 스트레이프, 잠담(idleBeat/potshot)
- 핑 9종·8방향 휠·컨텍스트 판정·도배 방지·AI 명령 주입, 채팅 말풍선·로그·AI 잡담
- 퀵크래프트 6레시피, 배치 유효성, 줌 잠금
- **완료 기준**: 사람 1 + AI 3 편성으로 M4 사이클 완주. AI가 벽에 갇혀 정지하는 사례 0(10분 관찰)

### M7 — 오디오·연출·빌드

#### 진행 (2026-09-07)

| 항목 | 상태 |
|---|---|
| `AudioSynth` — 원본 AU.tone/AU.hit 를 오프라인 렌더(파형 4종·지수 슬라이드/엔벌로프·RBJ 바이쿼드 LP/BP/HP 스윕·노이즈 버퍼 (1-i/n)^.5·소프트 리미터 .62/tanh) → AudioClip 캐시 | 완료 |
| `AudioDirector` — SFX 뱅크 25종+(ui/back/dig/brk/ore/oreBreak/res/shard/deploy/pick/rescue/cache/exit/descend/tick/shot/reload(수동 2단)/reloadDone/warn/ready/fail/start/timeout/buy/dawn/kill/growl/cardFlip/cardPick/step/stepCrew/dash/drillOverload/rumble/roar) 원본 수치 그대로, Kenney 샘플 우선·절차 폴백(bank.json 25항목 · 게인·피치 지터·카테고리 게인 dig 2.2/brk 1.2/combat 1.45/ui 1.85/loot 1.2/alert 1.35), 스로틀(dig 70 · res 60 · growl 420 · reload 120 · crewStep 215~345ms), 팀 핑 음형 9종 | 완료 |
| 드릴 — HTML 내장 폴리싱 WAV(start/loop/release) 추출, start→loop 조인 141ms·release 249ms 크로스페이드, 열 → 디튠 1200·log2(1+.45h^1.25) + 떨림(12Hz·28+137h²cent) + 드리프트 | 완료 |
| BGM 라우터 — 로비 / 땅굴 2겹(dungeon 1.19 · cave-stereo ×4.48 프리게인) / 보스(.78, in .55 · out 2.8) · 처치 3.0/2.6 복귀 · 런 종료 1.4 · 층 전환 1.0 · 앰비언스 in 1.4/out .9 · 레이어 시작 위치 랜덤. **로비 lobby-cave.webm 은 Unity 가 못 읽어 tunnel-dungeon 한 겹(.8)으로 대체** | 완료 (webm 대체는 ffmpeg 도착 후 교체) |
| 훅 — 벽 파괴/광석/드릴 비트/사격(사람만)/처치/재화/재장전/대시/카드 등장·선택/레벨업/탈출 도착·요청/기절/런 종료(생환 dawn·다운 fail)/보스 등장·처치, 메뉴 클릭·호버·뒤로·선택·출격, 채팅 전송, 크래프트, 적 각성 growl(거리 감쇠) | 완료 |
| 보스 등장 시네마틱 `BossIntroCinematic` — 월드 정지(렌더만) · 레터박스 11.5% · 비네트 · 카메라 팬(dim .55 · pan 1.05 · hold .35 · roar 1.75 · back .95, 줌 ×1.15, 저주파 흔들림) · 이름 플레이트(티어 카피) · 포효(킥 13 · 링 2 · 버스트 42 · 플래시 · 천장 먼지 85ms) · 포효 음성 = fireBreath 15프레임 시점 · 클릭/Esc 스킵 · 감속 모드 생략. `CameraRig.CineOverride` · **보스 시야원**(LOS bossSources: r×2.4+2, 탐색 기록 없음) 추가 — 원본에 있었으나 M2 에서 빠졌던 것 | 완료 (m7_bossintro.png) |
| 보스 격파 연출 — 다단 폭발(.2s 간격, 몸통 .8r 안 버스트·링·연기) + .15s 킥 2.2, 2.6s 뒤 휴식 | 완료 |
| 천장 붕괴 `CeilingFx` — 돌/먼지 z 낙하(55~120px/s)·착지 후 소멸, 보스 돌진 착지 20% 확률로 화면 전체 파동(clusters 11 · rocks 2 · dust 4 · alpha .22 · height r×2.6 · wave .55 · shake 3.4) | 완료 |
| 특성 카드 아이콘 — `TraitIcons`(원본 INF_TRAIT_ICON_ASSET 89항목) → HUD 카드 우상단 | 완료 |
| 게임패드 — 왼스틱 이동 · 오른스틱 조준 · RT 사격 · LT/RB 드릴 · A 대시 · X 재장전 · LB Q · Y E · D↓ 탈출 · D↑ 손전등 · Start 일시정지 / 메뉴: A 확인 · B 뒤로 · D패드/스틱 좌우 선택 (리매핑 UI 는 미착수) | 완료 |
| Windows 빌드 `BuildWindows` — 제품명 Tunnel Crew · 아이콘 app-icon-dragon · Boot/Menu/Run · unity/Build/Windows/TunnelCrew.exe (392MB, 에러 0) · Boot 씬이 Run 씬을 자동 로드(GameFlow.Start) · 실행 시 메인 메뉴 진입 확인, Player.log 예외 0 | 완료 (m7_build.png) |
| 미착수 | Pretendard 폰트(파일 미도착), 원본 v7.9.2 나란히 10분 비교 리뷰(사용자 체크포인트) |
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
| 원본이 가변 dt라 고정 60Hz로 바꾸면 손맛(드릴 타격 간격 0.06s=3.6틱, 히트스톱 12~68ms)이 달라짐. 특히 보스 피격 12ms·적 피격 20ms는 16.7ms 틱보다 짧아 사라지거나 두 배가 됨 | 히트스톱은 `Time.timeScale` 실시간 유지로 틱과 분리(D6 개정). 드릴 간격은 서브틱 누산으로 보존. M3에서 원본과 A/B |
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

## 10. 엔진 네이티브 대체 원칙 (2026-09-06)

HTML 프로토타입은 브라우저에서 당장 되는 방법으로 만족하며 빠르게 만들었다. Unity 포팅은 프로토타이핑을 끝내고 프로덕션 게임을 만드는 일이므로, **규칙·수치·보이고 들리는 결과는 보존하되 HTML 제약 때문에 택한 구현 방법은 가져가지 않는다.** 표현 계층(조명·어둠·VFX·셰이더·포스트·애니메이션·물리·카메라·UI·오디오)은 Unity 표준 제작법으로 새로 만든다.

전체 대체표·폐기 목록·추천 패키지는 [analysis-04](unity-port/analysis-04-engine-native-replacements.md). 핵심만 요약한다.

| 영역 | Unity 표준 | 대체하는 원본 |
|---|---|---|
| 조명·어둠 | URP 2D Light2D + Shadow Caster 2D + 어둠 풀스크린 패스 1개 + Volume(Color Adjustments·Tonemapping·Bloom·Vignette·Film Grain) | FOW WebGL 라이트맵, LIT 방사 차분 그림자, LX 4레이어, LOS 픽셀 버퍼 |
| 타일 | Tilemap 3장 + 커스텀 TileBase + 노멀맵 Secondary Texture | `_BS` 캐시 drawImage, 암반 3패스 |
| 스프라이트 효과 | Shader Graph 프로퍼티(`_Flash/_Tint/_Dissolve/_Emissive/_Rim`) | 드로우 함수 안 색 덧칠 |
| VFX | Particle System 프리셋 + VFX Graph(대량) + 바닥 RT 데칼 페인팅 | `J` 파티클 12종 배열, `rub/gore/fdec` 수명 배열 |
| 피드백 | `Time.timeScale` 히트스톱 + Cinemachine Impulse + DOTween을 묶은 자체 `Feedback` 컴포넌트 + `FeedbackProfile` SO (유료 Feel 미도입) | `J.hs` dt 배율, `J.kick`, `FEEL.transform` |
| 시네마틱·전환 | Timeline + Cinemachine 블렌드 + Signal, 풀스크린 와이프 셰이더 | DOM 오버레이 tick, `CREW.phase` 정지, 시트 3장 |
| 애니메이션 | Animator + 2D Freeform Directional 블렌드 트리, Animation Event, 임포트 시 피벗 확정 | `animT` 수식 인덱싱, 런타임 피벗 표 |
| 카메라 | Cinemachine 3 Position Composer + Target Group + Confiner 2D | 데드존·룩어헤드·동적 줌·클램프 수작업 |
| 물리 | Physics2D 하이브리드 (D5 개정) | 원-AABB 3회 반복, 4px 서브스텝 |
| UI | UGUI + TMP(한글 SDF) + 9-slice + DOTween + 테마 SO. 유리 블러는 폐기, 모달 뒤는 반투명 딤 | DOM/CSS: box-shadow 112·filter 111·backdrop-filter 12·transition 86 |
| 오디오 | Audio Mixer 그룹·Snapshot·Limiter + SfxBank SO + 절차 합성음 WAV 베이크 | WebAudio 게인 트리, `MENU_SFX.bank`, 합성 SFX 17종 |
| 툴링 | ScriptableObject + 인스펙터 + 디버그 메뉴 1개, Addressables, Localization | DEMO 190필드, F8/F10/Boss Lab, localStorage 12키 |
| 코옵 | Netcode for GameObjects + Unity Transport + Relay/Lobby | Node 릴레이·HTML 서빙·START.bat |

**유료 에셋 결정 (2026-09-06)**: Feel·Translucent Image·Odin 모두 **미도입**. 피드백은 자체 컴포넌트, 유리 효과는 딤 처리로 대체, SO 대량 데이터는 CSV/JSON 임포터로 관리. 무료로 쓰는 것은 DOTween(무료판, 저작권 고지 동봉)·Unity UI Extensions·Laigter. 부족이 확인되면 그 시점에 재검토한다.

마일스톤 반영: M1 Cinemachine·DOTween·Physics2D, M2 Volume 프로파일·Shadow Caster 청크·타일 노멀맵, M3 Shuriken 프리셋·스프라이트 셰이더·피드백 컴포넌트, M4 Timeline 2개·텔레그래프 셰이더, M5 9-slice·테마 SO·TMP 폰트·모달 딤·Localization, M7 Mixer 스냅샷·SfxBank·베이크 WAV 검수.

---

*이 문서는 누적 갱신한다. 결정이 바뀌면 결론과 이유, 남은 위험을 함께 적는다. 마일스톤 완료 시 §5 해당 항목에 완료일과 검증 결과를 기록한다.*
