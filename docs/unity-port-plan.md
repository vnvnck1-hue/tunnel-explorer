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
- 개발툴 스크립트 블록: Projectile Lab, Boss Lab 인스펙터(값은 데이터로 이관), Test Hub/Relic Lab, UILAB(F8), LX 패널(F10), `#pSet`, `?tcTest` 패널, 관전(OBSERVER) 모드는 보류
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
