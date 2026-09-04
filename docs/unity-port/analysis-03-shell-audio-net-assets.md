# 땅굴 크루 v7.9.2 — 셸(화면·HUD·입력·오디오·네트·자산·저장·빌드) 분석 (Unity 이식용)

> 작성: 2026-09-05 · 대상: `tunnel-crew-infinite-mode-v7.9.2.html` (17,371,356 byte). 행 번호는 base64 제거 사본(22,418줄) 기준이며 원본과 동일
> 상위 문서: [`../unity-port-plan.md`](../unity-port-plan.md) · 자매 문서: [코어 시뮬레이션](analysis-01-core-sim.md), [엔티티·전투·AI](analysis-02-entities-combat-ai.md)

---

## 1. 화면(Screen)·플로우 구조

### 1.1 전체 골격

```
<body>                                                       (L702)
└ #shell > #stageWrap > #app                                 (L703)
   ├ <canvas id="stage">      메인 게임 렌더 (2D)            (L704)
   ├ <canvas id="fogGL">      WebGL FoW/어둠 레이어 z=2      (L705)
   ├ <canvas id="uiLayer">    월드 오버레이 UI z=5           (L706)
   │                          (핑 마커·채팅 말풍선·크래프팅 프리뷰)
   └ 이하 전부 DOM/CSS 오버레이
```

**핵심 결론: 셸 UI는 거의 100% DOM+CSS다.** 캔버스에 그려지는 UI는 다음 4가지뿐이다.

| 캔버스 UI | 위치 | 비고 |
|---|---|---|
| 미니맵 | `#tcMiniCanvas` 220×190 (L979) | `drawMini()` — tcHudPolishScript 내 |
| 핑 마커 / 화면밖 화살표 / 핑 휠 | `#uiLayer` | `paintUI()` (L8409) 래핑 |
| 채팅 말풍선 | `#uiLayer` | 동일 |
| 퀵크래프트 설치 프리뷰 | 월드 캔버스 | `drawObjects()` |

Unity 포팅 시 → 셸 전체를 UGUI로 재구성하고, 미니맵만 RenderTexture, 핑/말풍선은 월드-스페이스 캔버스로 옮기는 것이 자연스럽다.

### 1.2 화면 목록과 구현 방식

| 화면 | DOM id | 라인 | 방식 | 비고 |
|---|---|---|---|---|
| **메인 메뉴** | `#crewMenu` (`.crewPanel.on`) | L719–762 | DOM | 로고 `assets/menu/title-tunnel-crew-v2.png`, 키아트 `hero-tunnel-crew-keyart-v5.webp`, 6개 모드 버튼 |
| **게임 설정** | `#crewSettings` | L763–801 | DOM | 오디오 3슬라이더+음소거, FoW/알파라이팅/힌트/모션감소 토글, HUD 레이아웃 편집, 조작표 |
| 미션 선택(솔로) | `#crewMission` | L802–811 | DOM | harvest(활성) / recover·purge(잠금) |
| 바이옴 선택 | `#crewBiome` | L812–819 | DOM | purple(활성) / brine(잠금) |
| **직업 선택(솔로)** | `#crewRole` | L820–830 | DOM | 4직업 카드 + `+AI` 칩(AI 스크립트가 주입) |
| 결과(솔로) | `#crewResult` | L831–839 | DOM | 같은 역할 재시작 / 역할 변경 / 메인 메뉴 |
| (구) 타이틀 | `#pTitle` | L843–856 | DOM | **레거시**, 루프 데모 잔재 — "미사용" 주석 명시 |
| (구) 업그레이드 트리 | `#pUp` | L858–868 | DOM | 레거시 |
| (구) 보고서 | `#pRep` | L870–879 | DOM | 레거시 |
| (구) 설정 (개발 슬라이더) | `#pSet` | L881–962 | DOM | **개발용 튜닝 패널** (카메라/플레이어/조명/프리셋 JSON) |
| (구) 조작 | `#pHow` | L964–972 | DOM | 레거시 |
| **솔로 HUD** | `#tcHudPolish` | L974–~1000 | DOM (미니맵만 canvas) | `.tcMod` 8모듈 |
| **무한모드 HUD** | `#infHud` | L11374–11420 | DOM | 아래 §3 참조 |
| 무한모드 선택 | `#infModeChoiceModal` | L11442 | DOM 모달 | STANDARD / TEST SCENE(성장×3.0·특성×1.5) |
| 테스트 허브 | `#tcTestHubModal` | L11452 | DOM 모달 | 6개 개발툴 진입 |
| **직업 선택(무한)** | `#infRoleModal` | L11465 | DOM 모달 (실질 풀스크린, `role-select.css` inset:0) | 카드 런타임 생성 `infPaintRoleSelect()` L12236 |
| **특성 픽(레벨업)** | `#infLevelModal` | L11472 | DOM 모달 | 3장 카드, 키보드 1/2/3, 리롤 버튼 `#infReroll` |
| 심층 정복(보스 후 휴식) | `#infRestModal` | L11473 | DOM 모달 | 전설 전리품 선택 + 하강/탈출 |
| **결과 / 게임오버** | `#infResultModal` | L11474 | DOM 모달 | 다시 시작 / 메인 메뉴 / 귀환 정산 |
| **귀환 정산 + 성장 지도 + 유물 보관고** | `#infSettlementModal` | L11475–11597 | DOM (3-view, `data-view` 전환) | ①summary ②성장 노드 맵(SVG 링크) ③유물 보관고 |
| 행성 지도(원정) | `#tcStarCanvas`/`#tcStarWorld` | L13048–13051 | canvas 별하늘 + DOM/SVG 라우트 | 드래그 팬, 행성 클릭 |
| 투사체 연구소 | `<section id="projectileLab">` | L11598–11612 | DOM + 전용 canvas | **개발 전용** |
| 보스 라이브 인스펙터 | `<aside id="bossLabInspector">` | L11424–11441 | DOM | **개발 전용** |
| LAN 코옵 로비 | `#coopLobby` | `coop/client.js` L1532 런타임 생성 | DOM | 닉네임/방목록/코드참가/역할선택 |
| 코옵 안내 팝업 | `#coopHelp` | L10860 런타임 생성 | DOM | file:// 로 열었을 때 |

### 1.3 없는 것 (Unity에서 새로 만들어야 함)

- **로딩 화면이 없다.** `assets/loading/`에 industrial-drill 아트 4장(acid-lime / coral / electric-cyan / minimal)이 있지만 **v7.9.2 HTML은 이 경로를 전혀 참조하지 않는다.** 단일 HTML이라 모든 리소스가 인라인/즉시로드라 로딩 화면 자체가 불필요했던 구조.
- **일시정지(Pause) 화면이 없다.** `grep -i pause` 결과는 전부 오디오 `Audio.pause()`와 `BOSS_LAB.paused`(개발용 보스 정지)뿐이다. ESC는 설정 닫기/탈출 배치 취소/리스폰에만 매핑되어 있다 (L9477–9484).
- 컷씬(`assets/cutscenes/planet-launch.mp4`)은 **비활성**: `const INF_CUTSCENE={enabled:false,videoSrc:'',duration:2.8}` (L12986) — 빌드 용량 때문에 뺐다는 주석.

### 1.4 화면 전환 연출

`tcFxTransitionJs` (L15642)가 **클릭 캡처 위임** 방식으로 전 화면 전환을 가로챈다.

- 타이밍: `D={cover:260, hold:40, reveal:340}` ms (L15646)
- 시트 위치: `A_=0.78, B_=-0.55, C_=-2.12, LEAD=0.22` (뷰포트 폭 배수)
- 라우트 테이블 `ROUTES` (L15720–15751): `fwd`(전진 와이프) / `back`(후진) 지정
  - fwd: `#menuSolo`, `#menuInfinite`, `#infModeStandard/Test`, `#tcStarLaunch`, `.infRoleStartCard`, `#infRetry`, `#menuObserver` …
  - back: `#infRoleBack`, `#missionBackMenu`, `#crewToMenu`, `#tcStarBack`, `#infMenu`, `#crewSettingsBack`, `#tcSetDone` …
- 함수 래핑 진입점: `startCrewMission`, `infCloseStarmap`, `infCloseSettlement` (L15790–15810)
- `prefers-reduced-motion` 또는 `.tcReducedMotion` 이면 즉시 실행 (L15673)

---

## 2. 스크립트 블록 인벤토리 (22개)

| # | 라인 | id | 역할 | 출시/개발 |
|---|---|---|---|---|
| 1 | L1259 | (익명) | **본체 엔진** ~9,600줄. 렌더링·조명/FoW·월드 생성·오디오(AU/SFX/MUS/BGM_ROUTE/AMBI)·입력·SAVE·CREW 솔로미션 | 출시 |
| 2 | L10888 | `src="/coop/client.js?v=7.9.2-character-art"` | 코옵 클라이언트 (외부 파일, 서버에서 서빙될 때만 로드) | 출시(옵션) |
| 3 | L10890 | **tcHudPolishScript** | **솔로 미션 HUD 갱신 루프**. `fitAll()` 스케일, `updateObjective/Status/Resources/Bag/Hint`, `drawMini()`, 브리핑·정산 패널 | 출시 |
| 4 | L11613 | `bossLabBakedParams` (application/json) | 보스 튜닝값 63개를 HTML에 "구워둔" 데이터 블록 | 데이터(출시) |
| 5 | L11614 | **tcInfiniteModeScript** | **무한모드 전체** ~3,600줄. INF 상태·직업·특성·보스·탈출·영구 노드 트리·유물·행성 지도·정산 + BOSS_LAB | 출시(+BOSS_LAB 개발) |
| 6 | L15227 | `projectileLabScriptLegacy` | `type="text/plain"` → **실행 안 됨(죽은 코드)** | 사장됨 |
| 7 | L15315 | **projectileLabInspectorScript** | 투사체 연구소 — 13종 투사체 연사 테스트 | **개발 전용** |
| 8 | L15462 | **tcMainMenuImageMotion** | 메인 메뉴 마우스 패럴랙스(`--menu-x/y`, lerp 0.105) | 출시 |
| 9 | L15642 | **tcFxTransitionJs** | 화면 전환 종이시트 와이프 (FX7.3.1) | 출시 |
| 10 | L15854 | **tcBossFxJs** | 보스 등장 시네마틱. 구간 `dim .55/pan 1.05/hold .35/roar 1.75/back .95`초, 레터박스·비네트·이름 플레이트·포효 임팩트(animT 1.4) | 출시 |
| 11 | L16089 | **tcBossDeathFxJs** | 보스 사망 연출 v2. `bars .34 / death 2.4 / fadeAt 2.75 / outAt 2.95 / TOTAL 3.5`초. death 24프레임을 인월드 렌더 경로로 재생 | 출시 |
| 12 | L16210 | **tcBossBgmJs** | 보스 BGM (아래 §5) | 출시 |
| 13 | L16485 | (익명) AIGEO | AI 채굴 목표 지형 게이트(도달성/연결성분 검증) | 출시(AI 하위) |
| 14 | L16605 | (익명) AI 크루 | **로컬 AI 동료** — 직업 선택 화면 `+AI` 칩(L18879–18901). `AICREW={roster,members,max:3,enabled}` | 출시 |
| 15 | L19014 | **tcTestHubScript** | 테스트 허브 + 유물 연구소(RELIC LAB, 32종 라이브 토글) | **개발 전용**(메뉴 05번으로 노출) |
| 16 | L19153 | **tcHudV77** | 무한 HUD 추가 동작 — 깊이 게이지(`tcPaintDepthRail`), 평시 페이드(`tcPaintCalm`), **Tab 홀드 키가이드** + 런 시작 12초 자동 노출 | 출시 |
| 17 | L19267 | **uiLayoutLabScript** | **F8 인게임 UI 레이아웃 에디터**(UILAB). 드래그·리사이즈·스케일·그룹 진입·JSON 내보내기. `localStorage['tc.uiLayout.v1']` 자동 저장 + **부팅 시 블라인드 재적용** | **개발 툴이지만 설정 화면에 노출되고 저장값이 실런타임에 적용됨** |
| 18 | L19901 | (익명) OBSERVER | 관전 모드 — 4직업 완전 자동. 리더는 가상 입력(KEY/G.mouse 덮어쓰기), Tab 시점 전환, Esc 해제. 메뉴 버튼 `#menuObserver` 런타임 주입(L20734) | 준-출시(데모용) |
| 19 | L20793 | (익명) **TEAM_PING_V1** | 팀 핑 시스템 | 출시 |
| 20 | L21552 | (익명) **TEAM_CHAT_V1** | 크루 채팅 | 출시 |
| 21 | L22036 | **tcCraftingFeature** | C홀드 현장 퀵크래프트 (6레시피 라디얼 휠) | 출시 |
| 22 | L22200 | (익명) LX 패널 | **F10 조명 실시간 튜닝 패널**. `localStorage['tc_lx_v791c']` | **개발 전용** |

> **개발 전용으로 잘라내도 되는 것**: #6(죽은 코드), #7 투사체 연구소, #15 테스트 허브/유물 랩, #17 UILAB(단, 저장된 레이아웃 JSON은 최종 UI 좌표의 **정답지**이므로 포팅 전에 export 해둘 것), #22 LX 패널, #5 안의 `BOSS_LAB_*` 계열(L11831–12035), `#infTestPanel`(`?tcTest` 쿼리 게이트, L15183), `#pSet` 슬라이더 패널.
> 단, **BOSS_LAB의 `bossLabBakedParams` JSON(L11613)은 실제 보스 밸런스 값**이므로 Unity ScriptableObject로 이관 필요.

---

## 3. HUD 요소와 참조하는 게임 상태

### 3.1 솔로 미션 HUD — `#tcHudPolish` (8모듈, `tcHudPolishScript` L10890)

| 모듈 id | 표시 | 읽는 상태 |
|---|---|---|
| `tc-objective` | 미션명·바이옴·목표 카운트·게이지·% | `CREW.missionId`, `CREW.biomeId`, `CREW.goalHave/goalNeed` |
| `tc-resources` | PULP / BLOOM | `G.gPulp`, `G.gBloom` |
| `tc-status` | 역할명·HP·무기·드릴 열 | `CREW.role.name`, `G.php/G.phpMax`, `G.weapon`, `G.drillHeat` |
| `tc-skills` | Q스킬·SPACE·F·무기 | `CREW.qCd`, `G.weapon` |
| `tc-minimap` | 220×190 canvas, `L{depth} · {COLS}×{ROWS}` | `G.cell[]`, `COLS/ROWS`, `G.depth`, `G.sh.x/y` |
| `tc-bag` | 유물 슬롯·가방 4칸 | `CREW.carrying`, `CREW.relic`, `CREW.missionId` |
| `tc-interaction` | 상호작용 키 힌트 | 근접 오브젝트 |
| `tc-briefing` | 런 시작 시 브리핑(`TC.briefUntil`) | `CREW.goalNeed` |

스케일링: `fitModule(id, baseW, baseH)` → `--fit` CSS 변수 (0.55~1.45 클램프). 기준 크기 `objective 520×92 / resources 250×64 / status 330×100 / skills 340×72 / minimap 190×190 / bag 250×120 / interaction 190×58`.
갱신: `tick()` — `updateVisibility()`는 `CREW.phase==='play'` 일 때만 모듈 표시.

또한 캔버스 밖 별도 DOM: `#resHud`(L710, `paintResHud()` L10232), `#crewHud`(L715, `paintCrewHud()` L10382), `#toast`, `#act`, `#gear`, `#crewMuteFab`, `#crewSettingsFab`.

### 3.2 무한모드 HUD — `#infHud` (L11374, 갱신 `infPaint()` L15166, 120ms 인터벌 L15224)

| 요소 | 표시 | 읽는 상태 |
|---|---|---|
| `#infDepth` | "심층 N" | `INF.depth` |
| `#infDomRail` (장악도 레일) | 크루 아이콘이 보스 아이콘으로 접근 | `INF.floorBroken / INF.totalBreakable`, 목표 `infDominanceTarget()` |
| `#infThreatText` | "위협 잠잠/경계/위험/폭주/붕괴 · N" | `infThreatValue()`, `G.enemies.filter(hp>0 && !boss).length` |
| `#infLevelText` / `#infCoreText` | Lv.N / 코어 N | `INF.level`, `INF.core` |
| `#infDepthRailFill` / `#infDepthMarks` | 좌측 세로 깊이 게이지 | `INF.depth` (tcHudV77 `tcPaintDepthRail`) |
| `#infBossBox` / `#infBossFill` / `#infBossText` | 보스 HP% + 장갑 | `INF.boss.hp/hpMax`, `infBossArmorAlive()` |
| `#infBossIndicator` / `#infBossDistance` | 화면밖 보스 방향 화살표 + 거리(칸) | `infPaintBossIndicator()` L13257 |
| `#infXpFill` / `#infXpText` | 하단 엣지 XP 바 | `INF.xp / INF.xpNeed` |
| `#infVitals` (좌하단) | 역할 뱃지 + HP바 + **탄창 세그먼트** + 재장전 바 | `G.php`, `INF.ammo/INF.magSize`, `INF.reloadLeft/reloadDuration` · `infPaintVitals()` L14936 |
| `#infBuildText` | 역할별 지형 상태 + 총기 배율 | 드릴러: `INF.drillMul*role.dig`, `INF.drillerBroken`, 균열% / 거너: `INF.breakerCharges/breakerCd` / 스카웃: `CREW.eCd`, `INF.scoutSectorCount` / 엔지니어: `INF.engineerNodes`, `CREW.turrets` |
| `#infSkills` (우하단) | 역할별 스킬 슬롯 (런타임 생성) | `infPaintSkills()` L15046 |
| `#infManual` | 캐릭터 사용설명서 툴팁(v7.9.2 신규) | 역할별 정적 텍스트 |
| `#infEscapeRow` / `#infEscapeBtn` | "탈출 요청 · X" | `INF.escape` · `infPaintEscapeHud()` L13710 |
| `#infKeyGuide` | LMB/RMB/WASD/SPACE (+ C 퀵크래프트) | Tab 홀드 또는 런 시작 12초 |

역할별 뱃지 이미지: `assets/ui/role-badges/role-badge-{driller|gunner|scout|engineer}.png`.

---

## 4. 입력(Input)

### 4.1 키보드 — `KEY = new Set()` (L9464), keydown 리스너 L9465–9491

| 키 | 동작 | 조건 |
|---|---|---|
| W/A/S/D | 이동 | 항상 (`KEY.add`) |
| Space | 대시 `tryDash()` / 대화 진행 | |
| Q | 역할 스킬 `crewSkillQ()` | SCENE==='depths' |
| E | 역할 보조 스킬 `crewSkillE()` / 마을 상호작용 | |
| R | 재장전 `infStartReload(true)` | INF.active |
| F | 손전등 토글 `TE.flashlight` | |
| X | 탈출 포트 배치 토글 `infToggleEscapePlacement()` | INF.active |
| 1 / 2 / 3 | 특성 카드 선택 | `#infLevelModal.on` |
| ESC | 행성지도 닫기 → 탈출배치 취소 → 설정 닫기 → 리스폰 (우선순위 순) | |
| `[` / `]` | 줌 -0.15 / +0.15 (1~7) | 개발용 |
| **G** (탭) | 위치 핑 · (홀드) 8방향 핑 휠 | TEAM_PING |
| **V** | 빠른 위험 핑 | TEAM_PING |
| **Enter** | 채팅 열기/전송 (빈칸이면 닫기), Esc=취소 | TEAM_CHAT |
| **C** (홀드) | 퀵크래프트 휠, 1~6 슬롯, Space=확정, Esc=취소 | tcCraftingFeature |
| **Tab** (홀드) | 키 가이드 표시 / 관전모드 시점 전환 | |
| F8 | UI 레이아웃 편집 | 개발 |
| F10 | LX 조명 패널 | 개발 |
| Shift+F9 / Shift+F10 | AI 크루 ON/OFF, AI 경로 표시 | 개발 |
| F6 | 크래프트 QA 세팅 | `?crafttest` + localhost 게이트 |

### 4.2 마우스 — `cv.addEventListener('pointerdown'...)` L9494–9530

| 입력 | 무한모드 | 솔로/기타 |
|---|---|---|
| LMB 홀드 | 드릴 (`G.mouse.drillDown`) / 거너는 파쇄탄 발사 | `G.mouse.down` (드릴 or 사격) |
| RMB 홀드 (또는 Ctrl+LMB) | 총 (`G.mouse.gunDown`) | — |
| 이동 | 조준 `G.mouse.sx/sy` | 동일 |
| **휠** | (무한모드에선 무시) | `switchWeapon(±1)` |
| 우클릭 메뉴 | `contextmenu` 캡처 차단 (L9524–9527) | |
| blur | 모든 마우스 상태 리셋 (L9528) | |

`lpos(e)` (L9492)로 클라이언트 좌표 → 논리 캔버스 좌표(`LW`,`LH`) 변환. `setPointerCapture` 사용.

### 4.3 게임패드 / 터치

- **게임패드 지원 없음.** `navigator.getGamepads` / `gamepadconnected` 호출 0건.
- **터치 지원 없음.** `touchstart`는 오디오 언락 제스처 목록(L4570 `evs:['pointerdown','keydown','touchstart','wheel']`)에만 등장.
- coop README "아직인 것" 표에 "컨트롤러 최적화"가 미해결 항목으로 명시되어 있음.

---

## 5. 오디오

### 5.1 아키텍처 — 3계층 하이브리드

**계층 1: WebAudio 절차적 합성 + 내장 샘플** — `const AU` (L3327–)
```
AU.ctx (AudioContext)
 └ AU.mas (master, vol.master=.9)
    ├ AU.sg (sfx) → AU.pre(1/3) → AU.lim(WaveShaper 소프트리미터, 2x oversample) → mas
    └ AU.mg (music, vol.mus=.66)
AU.vol = {master:.9, mus:.66, sfx:1, dig:2.2, brk:1.2, combat:1.45, ui:1.85, loot:1.2, alert:1.35}
AU.sfxMix = 3.0
```
- `MENU_SFX.bank` (L3763–3790): **base64 data URI로 HTML에 인라인된 25종 샘플 뱅크**. 각 항목 `{cat, g(게인), jit(피치 지터), src:[변형 1~5개]}`
  키: `dig(5) brk(3) shot(3) shard reload reloadDone growl(3) orebrk(3) kill(4) killwet res(2) ui hover menuclick back pick deploy cardflip cardpick step(4) stepcrew(3) cloth gear(2) ovl ovl2`
- 샘플 디코드 실패 시 **절차적 합성으로 자동 폴백** (L3606 주석)
- `DRILL_SMP` (L3427/L3596): 드릴 start/loop/rel 3종 WAV 내장, 별도 루프 재생기
- 실제 호출 빈도 상위: `SFX.ui`(110) `SFX.tick`(32) `SFX.brk`(24) `SFX.cache`(18) `SFX.ore`(15) `SFX.drillHum`(14) `SFX.back`(11) `SFX.dawn`(10) `SFX.buy`(10) `SFX.ready`(9)

**계층 2: `<audio>` 엘리먼트 BGM (data URI 인라인)**
- `LOBBY_MUSIC` (L4276): "眠れる霊殿" / Heitaro Ashibe · DOVA-SYNDROME — `LOBBY_BGM_DATA` data URI, `loop=true`
- `PURPLE_MUSIC` (L4316): "Ancient memories" / DOVA-SYNDROME — `PURPLE_BGM_DATA` data URI
- 볼륨: `AU.vol.master * AU.vol.mus`

**계층 3: 외부 파일 앰비언스/보스BGM (fetch + WebAudio, `<audio>` 폴백)**
- `AMBI` (L4393): `sets.lobby=[lobby-cave.webm g:1.00]`, `sets.tunnel=[tunnel-dungeon.ogg g:1.19, tunnel-cave-stereo.ogg g:4.48]` — 두 겹 동시 재생(길이가 달라 루프 주기가 겹치지 않음). LOBBY_MUSIC/PURPLE_MUSIC의 play/pause를 이쪽으로 위임(L4606–4619)해 호출부 무수정.

### 5.2 라우팅 — `BGM_ROUTE` (L4352)

```
BGM_ROUTE = { current:'none'|'procedural'|'lobby'|'purple'|'boss'|'cutscene', revision }
  .claimProcedural()  .useLobby()  .usePurple(reset)  .ensure()
  .useBoss()  .endBoss({fade, resumeAfter})     ← tcBossBgmJs 가 런타임으로 추가
```
`revision` 카운터로 stale play-promise가 옛 BGM을 되살리는 것을 방지. `visibilitychange`/`pageshow`/`focus`에 `ensure()` 바인딩 (L4380–4382).
자동재생 정책 대응: `AU.init()`/`AU.resume()`을 keydown·pointerdown 최초 이벤트에서 호출 (L4564–4575).

### 5.3 보스 BGM — `tcBossBgmJs` (L16210)

- 파일: `assets/audio/bgm/boss-blood-ascendant.mp3` (**HTML 내장 아님**, 유일한 외부 mp3)
- 기본: `fetch` + WebAudio `AudioBufferSourceNode(loop)` + 앞뒤 무음 트림 / 폴백: `<audio loop>` (file:// CORS 회피, L16315)
- 훅(전부 런타임 함수 래핑, 바이트 패치 없음):
  - `infSpawnBoss` → `BGM_ROUTE.useBoss()` (앰비언스 페이드아웃) — L16439
  - `infBossDefeated` → `endBoss({fade:3.0, resumeAfter:2.6})` — L16447
  - `CREW.phase==='infiniteResult'` → `endBoss({fade:1.4})` — L16453
  - `infEndRun` / `infInitFloor` → `endBoss({fade:1.0})` — L16458
- 튜닝: `DEMO.bossBgm=false`로 끄기, `DEMO.bossBgmGain`, 콘솔 `BOSS_BGM.report()`
- 보스 포효 SFX: `roarEl = new Audio('assets/audio/sfx/dragon-boss-roar.wav')` (L15934, tcBossFxJs)

### 5.4 오디오 자산 인벤토리

| 경로 | 개수 | 내용 |
|---|---|---|
| `assets/audio/bgm/` | 1 mp3 | boss-blood-ascendant.mp3 |
| `assets/audio/ambience/` | 1 webm + 2 ogg | lobby-cave.webm, tunnel-dungeon.ogg, tunnel-cave-stereo.ogg |
| `assets/audio/sfx/` | 1 wav | dragon-boss-roar.wav |
| `assets/sfx/` | 3 wav | drill_start / drill_loop / drill_release |
| `assets/sfx/kenney-candidates/` | **203 ogg / 19 폴더** | **후보 라이브러리(개발용, 런타임 미참조)**. 01-dig-drill(16) 02-drill-engine(14) 03-gun(13) 04-monster-combat(14) 05-ore-resource(12) 06-cache-jackpot(14) 07-ui(18) 08-warn-fail(15) 09-start-dawn(12) 10-exit-descend(11) 11-shop-buy(10) 12-crew-footstep(11) 13-ambient(8) 14-bgm-loops(9) 15-ore-crystal(12) 16-enemy-death(14) |

**실제 런타임 오디오 파일은 단 5개**다. 나머지 SFX/BGM은 전부 HTML 내부 base64. Unity 포팅 시 base64 페이로드를 추출해 AudioClip으로 변환해야 한다.

---

## 6. 네트워킹 / 코옵

### 6.1 구성

```
coop/server.mjs   579줄   Node ≥18, ESM, 의존성 ws@^8.21.3, PORT 기본 5188
coop/client.js   1684줄   서버에서 서빙될 때만 로드 (<script src="/coop/client.js">)
PROTO_VER = 4 (server L28) ↔ COOP_VER = 4 (client L30)
MAX_SEATS = 4 (p1~p4, p1 = 호스트)
```

서버는 **HTTP 정적 서버 + WebSocket 릴레이 + 세이브 저장소** 3역할. `/` 요청 시 `GAME_HTML` 환경변수(빌드시 패키지 HTML명으로 치환)를 서빙.

### 6.2 방(Room) / 방 코드

- `code4()` (server L167): `'ABCDEFGHJKLMNPQRSTUVWXYZ23456789'` (32자, I/O/0/1 제외)에서 4글자 → **약 100만 조합**
- 방 옵션: 이름(≤20자), 비밀번호(≤16자, 선택), 모드(`harvest`|`infinite`)
- **닉네임이 곧 계정** (≤12자). `localStorage['tc_coop_nick']`
- 방 목록 실시간 푸시(`rooms` 이벤트) + 코드 참가 폴백
- 좌석 배정 `freeSeat()` (L184), 호스트 승계 `promoted` (L311–320)

### 6.3 프로토콜 — 제어 메시지 (서버가 검증/처리)

| 타입 | 방향 | 내용 |
|---|---|---|
| `hello` | S→C | `{v:3, maxSeats:4}` 접속 즉시 |
| `rooms` | S→C | 방 목록 요약, 변경 시마다 푸시 |
| `host` / `join` | C→S | `{v, seed, nick, name, pw, pid}` — **`v!==PROTO_VER`면 거부** (L407) |
| `joined` | S→C | `{code, seed, seat, mode}` |
| `list` | C→S | 방 목록 요청 |
| `mode` | C→S | p1만, 미시작 상태만 |
| `role` | C→S | `{role, ready}` |
| `start` | C→S(p1) → S→전원 | `{seed, mode, players}` |
| `resume` | C→S | `{pid}` 재접속 (좌석 60초 보존) |
| `leave` / `promoted` | | 방 나가기 / 호스트 승계 |
| `res` | C→S→전원 | 공유 재화 — **서버가 `Math.max`로 중재** (L526) |
| `escape` | C→S→전원 | **서버 중재**: 심층별 먼저 온 요청 승리, 5초 창 (L537–543) |

### 6.4 프로토콜 — 릴레이 메시지 `RELAY_TYPES` (server L330–349, 검증 없이 전파)

```
state  dig  break  hp  loot  lamp  skill  end  ping  chat
escape board level boss
esnap  ehit  ekill  phit  cells  bwall  pdown  bfx  depth  afk
```

| 타입 | 페이로드 | 주기/방향 |
|---|---|---|
| `state` | `{x,y,aim,face,vx,vy,drill,weapon,roleId,php,moving,level,rvp}` | **30Hz** 전원→전원 (client L1362–1375) |
| `esnap` | `{dep, en(적), es(적탄), bq, bs}` | **15Hz** 호스트→전원 (client L816) |
| `cells` | `{v:[[k,type|0],...]}` 400개씩 청크 | **5Hz** 호스트→전원 지형 diff (L830) |
| `bwall` | `{v:[[k,type,hp],...]}` | 보스 소환 벽 (5배 경도) |
| `ehit` | `{i,d,nx,ny,src}` | 게스트→호스트 적 피해 위임 (드릴 피해는 누적해 15Hz로) |
| `ekill` | `{i,x,y,by,apex,el,boss,src}` | 호스트→전원, **`by` 좌석에 경험치 귀속** |
| `phit` | `{seat,d,nx,ny}` | 호스트→전원, **seat 본인만 적용** |
| `break`/`hp` | `{c,r,hp}` | 벽 파괴/체력 |
| `pdown` | `{on:1|0}` | 기절/부활 (상호 부활) |
| `afk` | `{on}` | `document.hidden` 통지 → 탈출 게이트·적 타깃 제외 |

### 6.5 권위 모델 — **호스트 권위(Host-Authoritative), 락스텝 아님**

- **맵**: 결정론적 시드 기반 (`hashSeed(seed|depth|size)`) — **시드만 동기화하면 전 클라이언트 동일 맵**. 시드는 방 생성 시 서버 배포.
- **적/보스**: p1(호스트)이 단독 시뮬레이션 → 15Hz 스냅샷 전파. 게스트 공격은 `ehit` 위임.
- **호스트 이탈 시**: 남은 크루가 자동 로컬 시뮬레이션 폴백, 복귀하면 재동기화.
- **개인 성장은 동기화하지 않음** (기획서 §5.2): 경험치·레벨업 카드·코어는 각자. `level` 이벤트는 명판 표시용 숫자만.
- **보간**: `state`는 30Hz 수신, 렌더는 매 프레임 지수 보간 + 속도 외삽(마지막 수신 후 0.25초 한도), 큰 점프는 스냅 (client L1395–1400).
- 자동 재접속: 2초 간격 재시도, 좌석 60초 보존 (`_reconT`, client L250).

### 6.6 서버 사이드 세이브

`handleMeta()` (server L90–128) — `GET/PUT /meta/:key`
- 저장 경로 `coop/saves/<닉네임>.json`, 키 정규식 `/^[\w가-힣.-]{1,24}$/u`, 최대 512KB
- `INF_META`(노드 트리·보관 코어·기록)를 닉네임 키로 보관. **저장 시각이 최신인 쪽 채택**.
- localhost ↔ IP 주소 변경에도 성장 이어짐. file:// 솔로는 로컬 저장.

### 6.7 팀 핑 시스템 — `TEAM_PING_V1` (L20793, 원본 `ping/tc-ping.js` 753줄)

**조작**: `G` 탭 = 위치 핑 · `G` 홀드+방향+릴리즈 = 8방향 휠 · `V` = 빠른 위험 핑

**9종 타입** (L20822–20832): `here(여기 #5FB8FF ●) go(가자 ▲) attack(공격 ✖) find(발견 ◆) mine(채굴 ⛏) retreat(후퇴 ▼) defend(방어 ⬢) help(도움 ✚) danger(위험 ⚠)`
- 휠 방향 순서 `DIRS=['go','attack','find','mine','retreat','defend','help','danger']` (0=↑부터 시계 45°씩)
- 우선순위 `pri`: help 8 > danger 7 > retreat 6 > attack 5 > defend 4 > go 3 > mine 2 > find 1 > here 0
- `cmd:true`(AI에게 명령이 되는 것): go, attack, mine, defend

**튠 상수 `CFG`** (L20812–20818):
```
tapMs:120, tapMovePx:18, deadPx:24, radiusPx:96
dur:4, durAlert:5, trackSec:2.5, labelSec:1.2, popSec:0.16, ringSec:0.45
stackTiles:1.5, agreeExtend:2, offTiles:30, offMax:3      (화면밖 화살표 최대 3)
logSec:3, logMax:3, logMergeSec:2                          (로그 3줄)
charges:4, regenSec:2.5, burstN:6, burstSec:10, lockSec:5  (도배 방지)
aiGoSec:8, aiAttackSec:10, aiMineSec:6, aiRetreatSec:5, aiHelpSec:10, aiDangerSec:5, aiDangerTiles:3
```
**컨텍스트 판정** `resolveContext()` (L20873): 적/보스 → 기절 크루 → 광맥/암반(`CELL_KO`) → 탈출 포트 순, **`LOS.seenAt()` 시야 안만 구체화**.
**코옵**: `{t:'ping'}` 전송, 수신자마다 동일 규칙으로 검증 (호스트 권위 판정은 2차 예정).
**AI 연동**: `AICREW.update`를 감싸 명령형 핑을 단기 goal로 주입. 리더 구조·탈출·동료 구조·근접 위협이 핑보다 우선.
좌석 색: `p1 #FFD36E / p2 #7FEBD0 / p3 #FF8D72 / p4 #C7A0FF`

### 6.8 크루 채팅 — `TEAM_CHAT_V1` (L21552, 원본 `chat/tc-chat.js` 430줄)

**조작**: Enter = 열기 / 전송(빈칸이면 닫기) · Esc = 취소(입력 보관)
**튠 `CFG`** (L21567–21575):
```
maxLen:80, sendGapMs:250
bubbleSec:5, bubbleFade:0.5, bubblePop:0.14, bubbleMaxW:210, bubbleLines:3, bubbleLift:38
idleLines:3, openLines:8, idleFadeSec:10, histMax:80
aiChat:'always', aiGapMin:15, aiGapMax:30, aiGapUrgent:6, aiPerLine:2, aiMemberCd:20, aiIdleSec:20, aiIdleCd:60
```
- 좌하단 로그: 평소 3줄(10초 페이드), 입력 중 8줄
- 말풍선: 캐릭터 머리 위 `#uiLayer`에 5초, `LOS.seenAt` 시야 체크
- **AI 크루도 채팅**(`aiChat:'always'`) — AI 좌석은 `ai:` 프리픽스, 색 `#C9C9D6`
- 코옵: `{t:'chat', text}` — `from`/`fromName`은 서버가 부착
- 키 입력은 window **캡처 단계**에서 가로채 본편 단축키(WASD·E·Q·R·G) 차단

---

## 7. 에셋 인벤토리

### 7.1 `assets/` — 총 **1,984 파일**

| 확장자 | 개수 |
|---|---|
| png | 1,612 |
| ogg | 205 (203 = kenney 후보 라이브러리) |
| gif | 65 |
| json | 62 (모두 스프라이트 추출 `*.report.json`) |
| webp | 13 |
| txt | 12 |
| wav / md | 4 / 4 |
| webm / psd / mp4 / mp3 / ico / html / css | 각 1 |

하위 분포: `characters/` 1,476 · `red-fire-dragon/` 162 · `ui/` 48 · `menu/` 22+ (trait-resources: cards 17, icons 20) · `boss-walls/` 2 · `loading/` 4 · `cutscenes/` 1.

### 7.2 캐릭터 스프라이트 — 8방향 시트 컨벤션

**A. 컨셉/턴어라운드 시트** (`docs/CHARACTER_8_DIRECTION_SHEET_GUIDE.md`, 489줄)
- 마스터 캔버스 **2048×2048 권장 / 1536 최소**, 1:1, RGBA, 배경 알파 0
- 8캐릭터를 **원형 배치**, 중심에서 바깥을 향함, 중앙 반경 0.20~0.24 비움
- 정규화 피벗(발 중앙/지면): `N(.50,.22) NE(.75,.30) E(.86,.55) SE(.75,.79) S(.50,.88) SW(.25,.79) W(.14,.55) NW(.25,.30)` — ±0.03 조정 허용
- 안전 여백 ≥4%, 인접 캐릭터 간 투명 간격 ≥ 캐릭터 폭의 12%
- 파일: `assets/characters/{driller|gunner|scout|engineer}-8dir-transparent.png`

**B. 런타임 애니메이션 시트** — 실제 게임이 읽는 것 (L2824–3010)

| 역할 | 루트 | 파일 프리픽스 | walk 프레임 | fall 프레임 | 방향별 피벗 Y |
|---|---|---|---|---|---|
| 드릴러 | `assets/characters/reely-2851-actions/sheets/` | `reely-2851-` | 11 | 16 | sw:164 w:176 nw:178 n:165 s:176 |
| 거너 | `assets/characters/reely-driller-actions/sheets/` | `reely-driller-` | 14 | 11 | 고정 (112,166) |
| 스카웃 | `assets/characters/reely-1530-actions/sheets/` | `reely-1530-` | 11 | 16 | sw:161 w:158 nw:165 n:183 s:164 |
| 엔지니어 | `assets/characters/reely-5279-actions/sheets/` | `reely-5279-` | 11 | 16 | sw:163 w:179 nw:182 n:180 s:153 |

- 파일명 규칙: `{prefix}{dir}-{action}.png`, `dir ∈ {sw, w, nw, n, s}`, `action ∈ {walk, fall}` → 역할당 10장
- **셀 크기 224×224, 시트 8열 × 2행** (`report.json`: `sheet_columns:8, sheet_rows:2`, `output_size:[224,224]`)
- 프레임 추출: `g.drawImage(sheet, (fi%8)*224, floor(fi/8)*224, 224,224, 0,0,224,224)` (L2999)
- **좌측 5방향만 원본, 우측 3방향(se/e/ne)은 런타임 수평 반전** (L2822 주석)
- 8방향 인덱싱: `minerDir8(a)` (L2816) → `['E','SE','S','SW','W','NW','N','NE'][round(a/(π/4))&7]`
- 피벗 정책: `"fixed crop; no per-frame trim or recenter"` — 프레임마다 재중심화하지 않고, 각 방향의 최대 하단 픽셀을 게임 바닥선에 매핑
- **메타데이터 JSON**: `{role}-actions/reports/*.report.json` (62개) — `crop_box`, `output_size`, `pivot_in_cell`, `sheet_columns/rows`, `source_total_frame_count(37)`, `selected_source_frames[]`

### 7.3 몬스터 — `monster_assets_v1.5.4/`

- 총 164 png + 3 py + 2 md
- `frames/crawler/` 16장, `frames/spitter/` 16장, `frames/brood-beast/` 16장, `frames/archive/` 6장
- 파일명: `frame_01.png` ~ `frame_16.png` (`String(i).padStart(2,'0')`, L2103)
- 런타임 로드: `ENEMY_SPRITE_BASE='monster_assets_v1.5.4/frames'` (L2098), 폴더명 매핑 `broodBeast → brood-beast`
- `sheets/` 15장 = 3종 × 5버전(v1.5, v1.5.1~v1.5.4) — **제작 원본, 런타임 미참조** (build-package가 `frames`만 복사)
- `docs/monster-animation-crop-guide-v1.5.md`

### 7.4 보스 드래곤 — `assets/red-fire-dragon/` (162 파일)

`BOSS_DRAGON_ANIMS` (L2108–2113), 전부 **10fps**:

| 키 | 프레임 수 | 폴더 | GIF 폴백 | 루프 |
|---|---|---|---|---|
| idle | 37 | `idle-frames/` | `idle-transparent.gif` | O |
| walking | 37 | `walking-frames/` | `walking-transparent.gif` | O |
| fireBreath | 26 | `fire-breath-a-frames/` | `Fire_Breath_a.gif` | X |
| death | 24 | `death-frames/` | `death-transparent.gif` | X |

기타: `arrival/`(2) `gif-move-sw/`(8) `gif-spawn/`(8) `fire.psd`
보스 벽: `assets/boss-walls/boss-wall-block.png`, `boss-wall-crystal.png`

### 7.5 타일 — `tunnel_crew_tile_resources_v1/` (= `tiles/` 미러, 567 png)

`TILE_RESOURCE_ROOT='tunnel_crew_tile_resources_v1'` (L2503)

**`tile_manifest.json` — 536 엔트리, 전부 50×50 px**

| 필드 | 값 |
|---|---|
| biome | brine 268 / purple 268 |
| type | dirt 96, stone 96, ore 96, gem 96, crys 96, core_top 48, rock 8 |
| 기타 | `index, file, band, surface, damage, width, height, atlasX, atlasY` |

파일명 규칙: `{biome}_{type}_b{band}_s{surface}_d{damage}.png` (예: `brine_dirt_b0_s0_d3.png`)
- `band` = 깊이 밴드, `surface` = 표면 변형, `damage` = 0~3 (4단계 손상)
- 아틀라스 좌표(`atlasX/Y`)가 매니페스트에 이미 포함 → Unity SpriteAtlas로 직행 가능
- 아틀라스 파일: `purple_strict_atlas_800x850.png`, `purple_strict_atlas_master_1600x1700.png`, `purple_floor_sheet_150x50.png`
- 개별 타일: `{biome}/individual_50/` 271장 × 2, `{biome}/overlays/` 8장 × 2
- 또 `tile_manifest.csv` 동일 데이터

**`BACKGROUND_TILE_RESOURCE_GUIDE.md`(366줄) 런타임 규격**

| 항목 | 값 |
|---|---|
| 논리 타일 크기 | **50 × 50 world px** (`let CELL=50`, L2034) |
| 최종 PNG | 50×50 RGBA |
| AI 제작 마스터 | 100×100 (@2x) |
| 피벗 | 좌상단 (0,0) |
| 월드 위치 | `(column×50, row×50)` |
| 내부 캔버스 | `round(50 × devicePixelRatio)` — DPR은 렌더용, 논리 크기 불변 |
| 실제 draw | 틈 방지로 `50.6 × 50.6`까지 덮음 |

합성 순서(8단계): 암흑 배경 → 바닥 타일 → 바닥 데칼/잔해 → 벽 타일 → core 노출 측면·윗면 → 깊이 밴드 경계선 → 출구/소품/랜턴/캐릭터 → 조명·FoW

바닥 변형 해시: `((col×17 + row×31) & 7) / 7` → `floor_dark 25% / floor_base 25% / floor_rim 50%`

벽 스펙:

| ID | HP | 보상 | 밴드색 | 손상 4단계 |
|---|---:|---|---|---|
| dirt | 80 | pulp 1 | O | O |
| stone | 140 | pulp 2 | X | O |
| ore | 100 | bloom 1 | O | O |
| gem | 100 | bloom 3 | O | O |
| crys | 100 | bloom 2 | O | O |
| rock / core_top | ∞ | — | — | 없음 |

광맥 결정 3군집 좌표(50px 기준): 주 `(26,29) h17 w7 rot-0.08` / 좌하 `(15,35) h10 w4 rot0.35` / 우상 `(36,19) h11 w4.5 rot-0.40` — **손상 단계가 바뀌어도 중심 이동 금지**.

### 7.6 `traits.json` — 56개 특성 (제작용 사본, 런타임은 HTML 인라인 `INF_TRAITS`를 읽음)

| 분류 | 개수 |
|---|---|
| role: all | 23 |
| role: driller | 9 |
| role: gunner | 8 |
| role: scout | 8 |
| role: engineer | 8 |
| tier 1 / 2 / 3 / 4 | 14 / 14 / 14 / 14 |

스키마: `{role, req(선행 특성 id|null), kind(계열명), id, tier, n(이름), d(설명)}`
특성 카드 아트: `assets/menu/trait-resources/cards/` 17장 (베이스 프레임 v1~v1.1.1), `icons/` 20장 (`trait-icon-{slug}-v1.png`)

### 7.7 UI 에셋 — `assets/ui/` 48 파일

| 폴더 | 내용 |
|---|---|
| `currency/` | `currency-{pulp,bloom,core,relic}.png` (4) |
| `role-badges/` | `role-badge-{driller,engineer,gunner,scout}.png` (4) |
| `crafting/icons/` | 6종 × png+webp = 12 |
| `crafting/ui/` | `ui-craft-button`, `ui-craft-action-glyph`, `ui-detail-panel`, `ui-wheel-connector`, `ui-wheel-selection`, `ui-wheel-slot` × png+webp = 12 |
| `crafting-concepts/` | 컨셉안 3장 + README |
| 루트 | `boss-icon.png`, `dom-boss-icon.png`, `dom-crew-icon.png`, all-crew 후보 4장, boss-skull 후보 3장 |

**9-Slice 규격**: `docs/UI_NINE_SLICE_EDITOR_HANDOFF.md`(823줄)는 **작업 명세서이지 구현 문서가 아니다.** 산출물은 `demos/tunnel-crew-ui-layout-lab.html`(독립 데모)이고, 본편에는 `uiLayoutLabScript`(F8)로 축약 반영되었다. 실제 9-slice PNG 리소스는 아직 없고 CSS `.tcSurface/.tcGlass`가 대체 중이다 → **Unity에서는 Sprite 9-slice로 새로 만들어야 한다.**

### 7.8 퀵크래프트 6레시피 (`tcCraftingFeature` L22040–22047)

| id | 이름 | kind | pulp | bloom | max/cd | 효과 |
|---|---|---|---:|---:|---|---|
| shaped-charge | 성형 폭약 | place | 7 | 2 | max 2 | 2초 뒤 벽 180% / 적 ×2.2, range 2.2 |
| auto-turret | 자동 포탑 | place | 8 | 2 | max 1 | 45초 지속, range 2.8 |
| coolant-capsule | 냉각 캡슐 | instant | 4 | 0 | cd 12 | 열 75% 제거 |
| folding-barricade | 접이식 방벽 | place | 6 | 0 | max 2 | 35초, range 2.3, foot 2×1 |
| med-injector | 응급 주사 | channel | 5 | 1 | cd 18 | 0.7초 채널, 최대HP 35% |
| flare-bundle | 휴대 조명탄 | place | 3 | 0 | max 2 | 30초, range 2.6 |

---

## 8. 세이브 데이터 — localStorage

| 키 | 정의 위치 | 스키마 / 용도 | 성격 |
|---|---|---|---|
| `hio_save_v1` | L2418 `SKEY` | `FRESH()` (L2420): `{pulp, bloom, day:1, best:1, runs, rescued, crew, caches, missed, stranded, lostTotal, maxStage, tunBest, tunRuns, relics, lv:{bond,pen,mine,hole}, seen:{bond,pen,mine,hole}, flag:{}}` | **레거시** (구 마을/캠프 루프). 로드 시 키별 머지(L2429–2437) |
| `tunnel_crew_settings_v1` | L10407 | `CREW_SETTINGS` (L10408): `{master:.9, mus:.93, sfx:1, muted:false, fog:true, alphaLight:true, hints:true, reducedMotion:false}` | **현역 · 설정** |
| `tunnel_crew_tut_v1` | L10406 | `'1'` 플래그 — 튜토리얼 완료 | 현역 |
| **`tc_infinite_meta_v1`** | L11616 `INF_META_KEY` | **핵심 영구 저장** — 아래 상세 | **현역 · 메인** |
| `tc.uiLayout.v1` | L19286 `LSKEY` | UILAB HUD 좌표/스케일 오버라이드. **부팅 시 블라인드 재적용** | 준-현역 |
| `tc_ping_tut_v1` | L21487 | 핑 튜토리얼 토스트 1회 표시 | 현역 |
| `tc_ping_wheel_v1` | L21491 | 핑 휠 튜토리얼 1회 | 현역 |
| `tc_coop_nick` | client.js L54 | 코옵 닉네임 (≤12자) | 현역 |
| `tc_coop_pid` | client.js L59 | 재접속용 브라우저 고정 식별자 | 현역 |
| `tc_boss_lab_params_v2` | L11831 | 보스 랩 현재 작업본 (자동 저장) | 개발 |
| `tc_boss_lab_params_pinned` | L11832 | 수동 체크포인트 | 개발 |
| `tc_boss_lab_params_prev` | L11833 | 직전 자동 저장본(1단계 되돌리기) | 개발 |
| `tc_lx_v791c` | L22206 | LX 조명 수치 (기본값 변경 시 키 버전 상승) | 개발 |

### `tc_infinite_meta_v1` 스키마 상세

```jsonc
{
  "bestDepth": 0,            // 최고 심층
  "bestBlocks": 0,           // 최고 파괴 블록 수
  "totalBosses": 0,          // 누적 보스 처치
  "escapes": 0,              // 생환 횟수
  "bankedCores": 0,          // 기지 보관 코어 (영구 통화)
  "unlocks": {               // 해금 플래그
    "ricochet": true,        //  ← bestDepth ≥ 2       (L14919)
    "explosive": true,       //  ← totalBosses ≥ 3     (L14920)
    "breach": true           //  ← bestDepth ≥ 3       (L14921)
  },
  "permanentRanks": { "<nodeId>": rank },   // 영구 노드 (평면 구조, L13757)
  "permanentLog": [ {role,id,rank,from} ],  // 마이그레이션 시 미확인 노드 보존
  "relics": {                               // 유물 (L14282)
    "owned": [],
    "sockets": [null,null,null,null,null],  // 5칸
    "age": [0,0,0,0,0],
    "seq": 0
  }
}
```

**마이그레이션 체인** (`infInitPermanentMetaOnly()` L13756–13790):
1. v7.2.0 이전 — `permanentNodes[role] = [id,...]`
2. v7.3.0~v7.4.0 — `permanentRanks[role][id] = rank` (2단)
3. → **v7.4.1 통합 평면** `permanentRanks[id] = rank`
`infResolveLegacyId()`로 구 ID 매핑, 실패 시 `permanentLog`에 보존.

**저장 정책**: 코어 획득은 **생환해야만** `bankedCores`에 반영 (L14916–14917, 기획서 §8.2). 노드 구매는 `infSaveMeta()` 실패 시 **롤백** (L14118).
**코옵 동기화**: 서버 `PUT /meta/<닉네임>` — 저장 시각 최신 우선 (§6.6).

---

## 9. 빌드 툴링

### 9.1 `tools/build-package.mjs` (322줄) — **스탠드얼론 Windows 배포 패키지**

```
node tools/build-package.mjs [원본.html] [--force] [--no-zip] [--zip-only]
```
원본 기본값: 루트에서 `tunnel-crew-infinite-mode-vX.Y.Z.html` 중 최고 버전 자동 선택 (`newestMainHtml()` L32).

**산출물** `build/TunnelCrew-v{VERSION}/` + 동명 `.zip`:

```
TunnelCrew-v7.9.2/
├ tunnel-crew-infinite-mode-v7.9.2.html
├ assets/                        (psd 제외, cutscenes는 INF_CUTSCENE 활성일 때만)
├ tunnel_crew_tile_resources_v1/ (전체)
├ monster_assets_v1.5.4/frames/  (frames만 — concepts/sheets/tools 제외, archive 제외)
├ coop/  server.mjs·client.js·package.json·package-lock.json·README.md·CHECKLIST.md
│        + node_modules/ + saves/ (빈 폴더로 출고)
├ node/node.exe                  (포터블 — 이전 패키지에서 재사용 or process.execPath)
├ START.bat                      (포트 5188, 사용 중이면 +1씩 최대 12회 탐색, 브라우저 자동 오픈)
└ 실행안내.md
```
- `coop/server.mjs`의 `GAME_HTML` 상수를 패키지 HTML 파일명으로 **문자열 치환** (L124–130) — 실패 시 throw
- HTML 본문의 루트 상대 경로 토큰을 정규식으로 수집해 루트 실존 파일 추가 복사 (L105–110)
- **zip은 반드시 `C:\Windows\System32\tar.exe -a`(bsdtar)** — PowerShell `Compress-Archive`는 한글 파일명(`실행안내.md`)과 경로 구분자를 깨뜨림
- 실적: `build/TunnelCrew-v7.8.0`, `build/TunnelCrew-v7.8.1` 존재

### 9.2 `tools/build-single-html.mjs` (371줄) — **완전 단일 HTML**

```
node tools/build-single-html.mjs [원본.html] [출력.html]
기본 출력: build/<원본이름>-single.html
```
- 자산 루트 3곳(`assets`, `monster_assets_v1.5.4`, `tunnel_crew_tile_resources_v1`)을 walk → **전부 data URI로 인라인**
- 대상 확장자: `png gif webp jpg jpeg svg ogg mp3 wav webm css json woff woff2 ttf`
- **런타임 shim** 주입: `Image.src`, `fetch`, `innerHTML`, CSS `url()`을 가로채 자산 맵에서 해결 → **본문 게임 코드 무수정**
- **빠지는 기능**: LAN 코옵(ws 서버), 서버 세이브(`coop/saves`). 메인 메뉴 LAN 코옵 버튼은 `locked·disabled` 처리.

### 9.3 기타 툴

| 파일 | 용도 |
|---|---|
| `tools/local-preview-server.mjs` | 로컬 정적 서버 |
| `tools/inject-lighting-lx.mjs` / `lx-panel.js` | LX 조명 패널 주입 |
| `tools/build-red-fire-dragon-arrival.mjs`, `build_red_dragon_spawn_gif.py`, `extract_red_dragon_frames.py` | 보스 프레임 추출 |
| `tools/extract_directional_character_gif.py` | GIF → 8방향 시트/프레임 (`*.report.json` 생성) |
| `tools/build_tile_resources.ps1`, `gen_kenney_smp_block.py`, `smp_block.js` | 타일/사운드 뱅크 생성 |
| `tools/process-crafting-assets.py`, `webp-lossless.py`, `remove_white_background_gif.py` | 에셋 후처리 |
| `ping/inject-ping.mjs`, `chat/inject-chat.mjs`, `ai/inject-ai-crew.py`, `ai/reinject-ai-crew.mjs`, `ai/inject-observer.mjs` | **기능 모듈을 본편 HTML에 주입** — 각 스크립트 블록의 원본이 별도 파일로 유지됨 |

> **포팅 관점 중요**: 기능 모듈(`ping/tc-ping.js` 753줄, `chat/tc-chat.js` 430줄, `ai/crew-ai.js` 1,790줄, `ai/observer.js` 858줄, `coop/client.js` 1,684줄)은 **HTML 밖에 원본 소스가 별도로 존재**한다. HTML을 파싱하는 대신 이 파일들을 직접 읽는 편이 훨씬 낫다.

---

## 10. Unity 포팅 시 셸 관련 리스크 정리

1. **base64 인라인 자산 추출이 최우선 작업**이다. BGM 2곡(LOBBY/PURPLE), SFX 뱅크 25종(총 50+ 변형), 드릴 WAV 3종이 전부 HTML 내부 data URI다. 외부 파일은 5개뿐.
2. **UI 좌표의 정답지는 `localStorage['tc.uiLayout.v1']`**이다. HTML/CSS의 초기 좌표는 UILAB 오버라이드로 덮여 있을 수 있으니, 실제 플레이 브라우저에서 이 JSON을 export 받아야 최종 레이아웃이 재현된다.
3. **일시정지·로딩 화면은 새로 설계**해야 한다 (원본에 없음).
4. **레거시 DOM 5개**(`#pTitle #pUp #pRep #pSet #pHow`)와 `#crewMission/#crewBiome`(솔로 전용, 대부분 잠금)은 포팅 대상에서 제외 검토 대상이다. 실질 플레이 경로는 **메인 메뉴 → 무한모드 → 직업 선택 → 인게임 → 결과 → 정산/성장 지도**다.
5. **코옵은 호스트 권위 + 결정론적 시드 맵**이므로 Unity에서 이식할 때 구조를 그대로 가져갈 수 있다. 다만 "게스트 유물이 호스트 적에 안 걸림", "게스트가 보스탄 피해 경감 이중 적용" 같은 알려진 불일치가 `coop/README.md` 표에 명시되어 있으니 재설계 기회로 삼는 것이 좋다.
6. **입력 재매핑 필요**: 게임패드/터치가 전혀 없고, `G`(핑)·`V`(위험)·`C`(크래프트)·`Enter`(채팅)·`Tab`(가이드)가 캡처 단계에서 본편 키를 가로채는 구조라 Unity Input System으로 옮길 때 우선순위/Action Map 분리 설계가 필요하다.
