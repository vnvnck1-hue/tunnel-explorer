# AI 행동 고도화 (AI_HUMANIZE_V1) — 기능 명세 · 머지 안내서

> 작업일 2026-09-03 · 베이스 `tunnel-crew-infinite-mode-v7.8.1.html`
> 결과물 `tunnel-crew-infinite-mode-v7.8.1-ai-behavior.html`
> 생성기 `ai/patch-ai-behavior.py` (앵커 35곳, 재실행 안전)
>
> **2026-09-03 머지 완료** — `v7.8.3-lighting-develop` 에 적용해 본선 `tunnel-crew-infinite-mode-v7.9.0.html` 생성. 앵커 35곳 전부 적용, 충돌 0건. 내부 버전 문자열 4곳(`menuBuild` · 크루 게임 설정 주석 · 무한 모드 직업 선택 주석 · `.tcSettingsEyebrow`) 도 v7.9.0 으로 올렸다. 이 머지에서 리더 스킬 게이트를 `lFire()` 로 고쳤다(아래 §09-6).

---

## 0. 30초 요약

| | |
|---|---|
| **무엇** | AI 크루 4직업이 "안 하던 자기 직업 행동"을 하게 만들고, 그 행동 전부에 확률·무작위성을 씌워 기계처럼 보이지 않게 했다. 관전 리더가 Q·E 를 쓴다. |
| **어디** | 주입된 **두 블록 안에서만**. ① AI 크루 블록 `<!-- AI_CREW_INJECTED_V1 -->` ② 관전 블록 `<!-- OBSERVER_MODE_INJECTED_V1 -->` |
| **본편 코드** | **한 줄도 안 건드렸다.** 게임 로직·UI·에셋·밸런스 수치 변경 0건 |
| **덩치** | AI 블록 1796 → 2372줄 (+576) · 관전 블록 743 → 884줄 (+141) |
| **머지 방법** | HTML diff 를 뜨지 말고 **패치 스크립트를 새 베이스에 재실행한다** (§1) |

---

## 1. 머지 전략

### A안 — 재생성 (권장)

이 작업은 전부 스크립트로 재생성된다. 상대 버전 파일을 베이스로 지정해 다시 돌리면 끝이다.

```bash
python ai/patch-ai-behavior.py tunnel-crew-infinite-mode-v7.8.3-lighting-develop.html --out tunnel-crew-infinite-mode-v7.8.4-merged.html
```

- 앵커를 정확히 1회 찾지 못하면 **조용히 넘어가지 않고 멈추고** 어느 패치가 깨졌는지 출력한다.
- 실패 메시지가 나오면 `ai/patch-ai-behavior.py` 의 **그 항목 앵커만** 새 코드에 맞춘다. §2 표에서 해당 행을 찾으면 무엇을 하려던 패치인지 바로 알 수 있다.
- 스크립트는 항상 베이스에서 새로 생성하므로, 몇 번 돌려도 중복 적용되지 않는다. 베이스에 이미 `AI_HUMANIZE_V1` 이 있으면 거부한다.

> **2026-09-03 확인**: 같은 시점에 작업 중인 `tunnel-crew-infinite-mode-v7.8.3-lighting-develop.html` 은 AI·관전 블록이 v7.8.1 과 **완전히 동일**하고(0줄 차이), 패치기 35곳이 전부 깨끗하게 적용된다(스모크 테스트 통과). 그 파일과의 머지는 A안 한 줄로 끝난다.

### B안 — 블록 통째 이식

상대 버전의 AI·관전 블록이 v7.8.1 과 **동일하다면** 두 블록을 그대로 덮어써도 된다. 먼저 동일한지 확인한다.

```bash
bash tools/ai-block-diff.sh tunnel-crew-infinite-mode-v7.8.1.html tunnel-crew-infinite-mode-v7.8.3-lighting-develop.html
```

차이가 0줄이면 안전하게 덮어쓸 수 있고, 차이가 있으면 A안으로 간다. (상대 쪽에도 AI 변경이 들어갔다는 뜻이므로 무조건 A안)

### C안 — 손으로 이식

§2 표 순서대로 적용한다. 표의 "충돌 위험" 이 `높음` 인 항목만 신경 쓰면 된다 — 나머지는 삽입(insert)이라 충돌하지 않는다.

---

## 2. 변경 지점 35곳

`대상` 은 AI 크루 블록 내부의 함수다. `삽입` 은 기존 코드를 지우지 않고 끼워 넣기만 한 것.

### 2.1 코어 (1)

| 패치 이름 | 대상 | 무엇 | 종류 | 충돌 위험 |
|---|---|---|---|---|
| `humanize-core` | 편성 절 앞 | 성향·기분·의도 게이트 + 새 직업 행동 함수 21개를 한 블록으로 삽입 (약 430줄) | 삽입 | 낮음 |

### 2.2 KIT · 스폰 (4)

| 패치 이름 | 대상 | 무엇 | 종류 | 충돌 위험 |
|---|---|---|---|---|
| `kit-driller` | `KIT.driller` | `crack: true`, `breachCd: 8` | 삽입 | 낮음 |
| `kit-gunner` | `KIT.gunner` | `breakerAtk: true` | 삽입 | 낮음 |
| `kit-scout` | `KIT.scout` | `pulseCd: 9`, `grappleCd: 6`, `exploreCd: 11` | 삽입 | 낮음 |
| `spawn-persona` | `spawnMember()` | 멤버 객체에 성향·새 쿨다운·새 상태 필드 추가 (§3.4) | 삽입 | 낮음 |

### 2.3 프레임 갱신 (6)

| 패치 이름 | 대상 | 무엇 | 종류 | 충돌 위험 |
|---|---|---|---|---|
| `update-cooldowns` | `AI.update()` | `m._dt` 기록 · `updateMood()` · 새 쿨다운 4개 감산 | 삽입 | 낮음 |
| `update-react` | `AI.update()` | 판단 주기를 `0.12+r*0.1` → **성향값** `m.pers.react*rnd(0.75,1.4)` · `updateBreach()` 호출 | 교체 | **높음** |
| `update-dash-noise` | `AI.update()` | 무작위 대시 확률에 적극성·기분 배율 | 교체 | 중간 |
| `update-potshot` | `AI.update()` | 비전투 목표에서 기회사격 `potshot()` | 삽입 | 중간 |
| `update-installations-tick` | `updateInstallations()` | `updateCracks()` · `updateMarks()` 매 프레임 호출 | 삽입 | 낮음 |
| `floor-reset` | `AI.onFloorInit()` | 지층 전환 때 균열·표식·대기값 초기화 (**성향은 유지** — 같은 사람이다) | 삽입 | 중간 |

### 2.4 판단 `decide()` (5)

| 패치 이름 | 무엇 | 종류 | 충돌 위험 |
|---|---|---|---|
| `decide-fight` | 요격 거리에 적극성 반영 · 집중력 낮으면 2순위 표적 오판(22%) · 채굴 중 먼 적 무시 · `m.lastFoeDir` 기록 | 교체 | **높음** |
| `decide-duties` | 직업 고유 임무 전체를 의도 게이트로 감싸고, **스카우트 정찰·펄스**와 **드릴러 기반암 균열** 목표를 신설 | 교체 | **높음** |
| `decide-loot` | 회수 확률 고정 40% → `28% × 탐욕 × 기분` | 교체 | 낮음 |
| `decide-reload` | 재장전 임계 고정 45% → 개인값 26~62% + 의도 게이트(가끔 잊는다) | 교체 | 중간 |
| `decide-watch` | 거너 `guard`(제자리 조준) → **`watch`(통로 입구 선점)** | 교체 | 중간 |

### 2.5 실행 `act()` (6)

| 패치 이름 | 무엇 | 종류 | 충돌 위험 |
|---|---|---|---|
| `act-branches` | 새 목표 분기 4개 삽입: `crack` · `pulse` · `scout` · `watch` | 삽입 | 낮음 |
| `act-flare` | 인라인 플레어 코드 → `throwFlare()` 호출 (투척 각도·거리에 흔들림) | 교체 | 중간 |
| `act-turret-spot` | `placeTurret(m)` → `placeTurret(m, pickTurretSpot(m, null))` | 교체 | 낮음 |
| `act-mine` | 채굴 중 **돌파 파기** 의도 판정 · 잠깐 손 멈추는 숨 돌리기 | 삽입 | 중간 |
| `act-fight-kite` | 유지 거리·회피 방향에 개인차·흔들림 · 후퇴 대시를 조심성 기반 의도로 | 교체 | **높음** |
| `act-guard-sweep` | 등속 조준 회전 → 멈췄다 훑는 시선 + 딴짓 | 교체 | 중간 |

### 2.6 전투 지원 · 파쇄탄 (2, 함수 전면 교체)

| 패치 이름 | 대상 | 무엇 | 충돌 위험 |
|---|---|---|---|
| `combat-support` | `combatSupport()` **전면 교체** | 세 직업 전부 의도 게이트 · 센트리 커버리지 판정 복원 · **거너 파쇄탄 공격 신설** | **높음** |
| `breakers` | `updateBreakers()` **전면 교체** | `detonateBreaker(m, b, early)` 분리 + **조기 기폭** 신설 · 폭발 피해에 거리 감쇠 | **높음** |

### 2.7 사격 · 목표 선택 · 렌더 (4)

| 패치 이름 | 대상 | 무엇 | 종류 | 충돌 위험 |
|---|---|---|---|---|
| `fire-aim-error` | `fire()` | 조준 오차 고정 ±0.045 → **개인값 ±0.022~0.085** (굴착 중 1.6배) | 교체 | 중간 |
| `fire-range-role` | `act()` 교전 | `FIRE_RANGE()` → `min(FIRE_RANGE(), kit.range)` — **선언만 되고 안 쓰이던 직업별 사거리를 적용** | 교체 | 중간 |
| `pickmine-noise` | `pickMine()` | 광맥 점수에 개인 편애도 + `rnd(-4,4)` 흔들림 | 교체 | 중간 |
| `darkspot-unseen` | `darkSpotAhead()` | 조명 유무만 보던 점수에 **미탐색(`LOS.seenAt`)** 가중 | 교체 | 낮음 |
| `place-turret` | `placeTurret()` | 시그니처 `(m)` → `(m, spot)` · 회수 대상을 배열 순서 → **수명 최소** | 교체 | 중간 |
| `draw-marks` | `drawInstallations()` | 기반암 균열 게이지 · 펄스 표식 렌더 삽입 | 삽입 | 낮음 |

### 2.8 관전 블록 (5)

| 패치 이름 | 대상 | 무엇 | 종류 | 충돌 위험 |
|---|---|---|---|---|
| `obs-leader-skills` | `OBS.drive` 앞 | 리더 성향 `LPERS` · `lIntent()` · `leaderSkills()` · `pickLeaderBedrock()` 삽입 (약 140줄) | 삽입 | 낮음 |
| `obs-leader-call` | `OBS.drive()` | 매 프레임 `leaderSkills(dt)` 호출 (try/catch 로 감싸 오토파일럿을 멈추지 않는다) | 삽입 | 중간 |
| `obs-bedrock-goal` | 관전 `decide()` | 리더 드릴러의 **기반암 목표 `rock`** 신설 | 삽입 | 중간 |
| `obs-bedrock-act` | 관전 `act()` | `rock` 분기 — 붙어서 드릴 입력을 물린다(본편 `infDrillerPressure` 가 균열을 쌓는다) | 삽입 | 중간 |
| `obs-reset-brain` | `resetBrain()` | 층 전환 때 `rockTarget` 초기화 · **리더 손버릇 재추첨** | 삽입 | 낮음 |

---

## 3. 새로 생긴 심볼 (이름 충돌 확인용)

상대 버전에 같은 이름이 있으면 충돌한다. 전부 IIFE 내부 지역 심볼이라 전역 오염은 없다.

### 3.1 AI 크루 블록 — 함수 21개

```
rollPersona  updateMood  intent  idleBeat  potshot
crackNeed  bedrockAt  breakBedrock  crackPressure  updateCracks  pickBedrock
startBreach  updateBreach
breakerCluster  detonateBreaker  pickWatchPost
throwFlare  scoutPulse  updateMarks  pickScoutSpot  tryGrapple
pickTurretSpot
```
상수: `roll`, `litAt`, `seenAt`

### 3.2 관전 블록 — 함수 6개

```
rollLeaderPersona  lIntent  foesWithin  litNear  leaderSkills  pickLeaderBedrock
```
상수: `LPERS`

### 3.3 새 상태 컨테이너

| 심볼 | 무엇 | 수명 |
|---|---|---|
| `AICREW.cracks` | `Map<cellIndex, {p, last, type, stage, owner}>` — AI 소유 기반암 균열 | 지층 전환 시 `clear()` |
| `AICREW.marks` | `[{x, y, kind:'vein'|'threat', ttl, e?}]` — 스카우트 펄스 표식 | ttl 만료 · 지층 전환 시 비움 |
| `OBSERVER.wait` | 리더 의도 게이트 대기표 | `resetBrain()` |
| `OBSERVER.rockTarget` / `rockT` | 리더 드릴러 기반암 목표 | `resetBrain()` |

### 3.4 멤버 객체 새 필드

```js
pers  wait  mood  moodT                       // 성향·기분·의도
breachT  breachCd  pulseCd  grappleCd  exploreCd
crackTarget  crackT  crackPatience
watch  watchT  sweepT  sweepA  farJit
idleT  idleDir  strafeT  lastFoeDir  _dt
```

### 3.5 새 `goal.kind`

| kind | 라벨 | 누가 |
|---|---|---|
| `crack` | 기반암 균열 | 드릴러 |
| `breach` *(별도 목표 아님)* | 돌파! | 드릴러 — `mine`/`crack` 안에서 `m.breachT` 창으로 발동 |
| `pulse` | 정찰 펄스 | 스카우트 |
| `scout` | 정찰 | 스카우트 |
| `watch` | 구역 경계 | 거너 |
| `rock` *(관전 블록)* | 기반암 균열 | 관전 리더 드릴러 |

HUD·머리 위 상태 표시는 `goal.label` 을 그대로 쓰므로 추가 작업이 필요 없다.

### 3.6 시그니처가 바뀐 기존 함수

| 함수 | 전 | 후 | 기존 호출부 |
|---|---|---|---|
| `placeTurret` | `(m)` | `(m, spot)` | **인자 생략 시 자동 선정** — 기존 호출 그대로 동작 (빙의 `manualAct` 포함) |
| `updateBreakers` | 폭발 로직 내장 | `detonateBreaker()` 분리 호출 | 호출부 변경 없음 |
| `combatSupport` | — | 본문 전면 교체 | 호출부 변경 없음 (`act()` 교전 분기) |

---

## 4. 본편 API 의존 (새로 생긴 것만)

상대 버전에서 이 이름들이 바뀌었으면 여기만 확인하면 된다.

| 심볼 | 쓰는 곳 | 없으면 |
|---|---|---|
| `LOS.seenAt(x,y)` | 미탐색 판정 — 정찰 목표·플레어 위치·기반암 선호·균열 렌더 | `typeof` 가드 → 전부 "이미 봤다"로 취급 (동작은 유지) |
| `G.nBlk` · `G.compDirty` · `G.vib` · `G.hp.delete` | `breakBedrock()` — 기반암 제거 | **가드 없음.** 이름이 바뀌면 기반암 파괴가 깨진다 |
| `INF.totalBlocks` | `breakBedrock()` 집계 | `inf()` 가드 |
| `INF_XP_TABLE.crack` / `.crackCore` | 기반암 경험치 (8 / 12) | 폴백 상수 내장 |
| `compOf()` · `G.comp` | 기반암·경계벽 도달성 | 없으면 후보 없음 처리 (안전) |
| `crewSkillQ()` / `crewSkillE()` | **관전 리더 스킬** | `typeof` 가드 |
| `CREW.qCd` / `CREW.eCd` | 리더 스킬 쿨다운 조회 | `null` 체크 후 반환 |
| `INF.breakerCharges[].stuck` | 리더 거너 조기 기폭 판정 | 빈 배열 폴백 |
| `G.php` / `G.phpMax` | 리더 피격 상태 | 없으면 "안 다침" 취급 |
| `G.dash.active` | 리더 그래플 중복 방지 | 옵셔널 체크 |
| `INF.drillerCracks` | 리더 기반암 목표 점수 | 옵셔널 체크 |
| `SOLIDX()` | 관전 블록 기반암 판정 | `rock`/`core` 문자열 폴백 |

기존에 이미 쓰던 것(`damage` `hurtEnemy` `shelDps` `DRILL_DMG` `solidAt` `toCell` `cxw/cyw` `J.*` `SFX.*` `AIGEO` `teMovePx` 등)은 그대로다.

---

## 5. 밸런스 분리 — 머지 리뷰 체크리스트

§9.6.7 "사람 밸런스와의 분리" 가 깨지면 이 도구는 쓸모가 없다. 새 코드가 지키는 것:

- [x] AI 기반암 균열은 **AI 전용 `AICREW.cracks`** 를 쓴다. 사람의 `INF.drillerCracks` 를 읽지도 쓰지도 않는다 (관전 리더는 사람 캐릭터 본인이므로 예외적으로 조회만 한다).
- [x] 기반암 파괴는 `INF.floorBroken` 을 **올리지 않는다** — 사람 `infBreakFoundation` 과 같은 규칙(장악도·보스 출현 조건에 기반암을 넣지 않는다).
- [x] 기반암·파쇄탄 경험치는 전부 `awardXp(m, ...)` 로 **그 크루 개인**에게 간다. `infAwardXp`(사람 관문)를 부르지 않는다.
- [x] 파쇄탄 공격 피해는 `m.kit.gunMul` 만 탄다. 사람의 `INF.gunMul`·`breakerDamageMul` 계열을 쓰지 않는다.
- [x] 센트리·노드는 여전히 `AICREW.turrets` / `AICREW.nodes` 로 따로 돈다.
- [x] 새 특성·카드 없음 — `AI_TRAITS` 풀 무변경.
- [x] 관전 리더만 본편 `crewSkillQ/E` 를 부른다(리더 = 사람 캐릭터이므로 원래 경로 그대로).

---

## 6. 튜닝 지점 — 확률표

수치는 전부 `intent(m, tag, ready, {min, max, p})` 한 형태로 모여 있다.
`min~max` = 준비 후 망설이는 초, `p` = 결심 확률. 실패하면 `rnd(min,max) × 1.3~2.8` 만큼 더 뜸을 들인다.

| 태그 | 행동 | 망설임(초) | 확률 |
|---|---|---|---|
| `turret` | 센트리 설치 | 0.6~4.5 | 0.72 |
| `turretFwd` | 센트리 전진 배치 | 1.2~6 | 0.55 |
| `turretFight` | 교전 중 센트리 | 0.35~2.4 | 슬롯 여유 0.8 / 현장 미커버 0.6 / 이미 커버 **0.12** |
| `node` `nodeFight` | 전력 노드 | 0.3~4 | 0.6 (급전 필요 시 0.82~0.85) |
| `explore` | 선행 정찰 | 1.5~7 | 0.5 × 호기심 |
| `pulse` `pulseFight` | 정찰 펄스 | 0.6~6 | 0.45 × 호기심 / 교전 중 0.4 |
| `flare` `flareFight` | 플레어 | 0.25~3.5 | 0.65 / 교전 중 0.8 |
| `grapple` | 그래플 훅 | 0.3~2.4 | 0.55 |
| `crack` | 기반암 목표 선택 | 2.5~9 | 0.5 |
| `breach` | 돌파 파기 | 0.5~3.2 | 두꺼운 벽 0.75 / 얇은 벽 0.4 |
| `breachRock` | 기반암에 돌파 파기 | 0.8~4 | 0.62 |
| `breakerAtk` | 파쇄탄 공격 | 0.25~2.0 | 0.7 |
| `breakerCover` | 차폐 제거 | 0.2~1.6 | 0.72 |
| `earlyDet` | 조기 기폭 | 0.05~0.55 | 0.5 + 0.12 × 적 수 |
| `shield` | 방어막 | 0.1~1.2 | 다치면 0.9 / 아니면 0.5 |
| `reload` | 재장전 | 0.15~1.6 | 0.8 |
| `kite` | 후퇴 대시 | 0.05~0.6 | 0.72 |

최종 확률은 `p × (0.62 + 성실성 × 0.48)` 이고, 망설임은 `÷ (적극성 × 기분)` 이다.

성향 난수 범위(스폰 시 1회, 지층이 바뀌어도 유지):

| 항목 | 범위 | 쓰이는 곳 |
|---|---|---|
| `eager` 적극성 | 0.60~1.45 | 망설임 시간 |
| `discipline` 성실성 | 0.55~1.00 | 결심 확률 |
| `aggression` | 0.60~1.35 | 요격 거리 · 기회사격 · 무작위 대시 |
| `caution` 조심성 | 0.55~1.35 | 유지 거리 · 방어막·대시 임계 |
| `greed` 탐욕 | 0.35~1.35 | 재화 회수 확률 |
| `curiosity` 호기심 | 0.50~1.45 | 정찰·펄스 확률 |
| `focus` 집중력 | 0.55~1.00 | 딴짓 빈도 · 표적 오판 · 채굴 중 적 무시 |
| `aimErr` | 0.022~0.085 rad | 사격 오차 |
| `reloadAt` | 0.26~0.62 | 재장전 임계 |
| `react` | 0.11~0.26초 | 판단 주기 |
| `oreBias` | 0.55~1.50 | 광맥 편애 |

직업별 중심 보정: 거너 `aggression ×1.15` `curiosity ×0.85` · 스카우트 `curiosity ×1.35` `caution ×1.10` · 드릴러 `discipline ×1.10` `curiosity ×0.90` · 엔지니어 `discipline ×1.15` `aggression ×0.90`.

기분 `mood` 는 8~20초마다 0.72~1.28 재추첨.

---

## 7. 확률을 넣지 않은 것 (건드리면 크루가 죽는다)

- 탈출 포트 탑승 (`escape`)
- 쓰러진 동료·리더 구조 (`revive`, `G.downed` 최우선)
- 보스탄 예고 회피 (`dodgeBossShot`)
- 파묻힘 탈출 (`recover`)
- 접촉 재화 습득 (`collectLoot`)

머지 중 이 다섯 경로에 의도 게이트가 끼어들면 잘못된 머지다.

---

## 8. 검증 절차

창이 숨으면 `requestAnimationFrame` 이 멈추므로 `update()` 를 직접 스테핑한다.
`OBSERVER.enter()` 를 부르면 4직업이 전부 AI 로 돌아 한 번에 검증된다.

```js
INF.selectedRoleId = 'gunner';          // 리더 직업을 바꿔 AI 드릴러도 검증
OBSERVER.enter();
const labels = {};
for (let i = 0; i < 3600; i++) {        // 60초
  update(1/60);
  for (const m of AICREW.members) {
    const l = (m.goal && m.goal.label) || 'none';
    labels[m.roleId + '|' + l] = (labels[m.roleId + '|' + l] || 0) + 1;
  }
}
console.log(labels, AICREW.cracks.size, AICREW.marks.length, AICREW.turrets.length);
renderDepths();                          // 렌더 경로 예외 확인
```

전투는 적이 근처에 없으면 검증되지 않는다. 강제로 붙인다.

```js
for (let i = 0; i < 8; i++) spawnEnemy(false);
const g = AICREW.members.find(m => m.roleId === 'gunner'); g.hp = g.hpMax * 0.3;
for (const e of G.enemies) { const a = Math.random()*6.283, d = CELL*1.6;
  const x = g.x + Math.cos(a)*d, y = g.y + Math.sin(a)*d;
  if (!solidAt(x, y)) { e.x = x; e.y = y; e.seePlayer = true; } }
for (let i = 0; i < 420; i++) update(1/60);
```

### 머지 후 통과해야 하는 것 (실측 기준값)

| 확인 | 기대 |
|---|---|
| 목표 라벨에 `기반암 균열` `정찰` `구역 경계` 가 나타난다 | 드릴러·스카우트·거너 신규 행동 |
| `AICREW.cracks.size` > 0 (드릴러가 크루에 있을 때) | 균열 누적 |
| `AICREW.marks.length` > 0 | 펄스 발동 |
| 센트리 설치 간격이 쿨다운(12초)보다 훨씬 길다 | 실측 26~31초 — 기회를 흘린다 |
| 펄스 간격이 쿨다운(9초)보다 길다 | 실측 16~21초 |
| 관전 리더 Q 사용이 기회의 절반 내외 | 60초에 4회 (기회 8.5회) |
| 전투에서 `방어막 → 파쇄탄 → 조기 기폭 → 회피` 가 순서대로 | say 로그로 확인 |
| 센트리 탄창이 줄어든다 | 22 → 2 |
| 콘솔 예외 0건 | `[AICREW]` `[OBSERVER]` 에러 없음 |

---

## 9. 함정 (머지하다 걸릴 것들)

1. **의도 게이트 대기는 게임 시간 `G.t` 절대 시각이다.** 프레임마다 `dt` 를 깎는 방식으로 "정리"하면 안 된다 — `decide()` 가 0.12~0.26초에 한 번만 돌아서 실제 대기가 십수 배로 늘어난다. 1차 구현에서 엔지니어가 20초 동안 센트리를 한 기도 안 세웠다.
2. **`ai/crew-ai.js` 는 주입본보다 뒤처져 있다.** `inject-ai-crew.py` 로 재주입하면 센트리 수치(`turretRange` 등 — 없으면 센트리가 한 발도 안 쏜다)·적탄 회피·빙의 조종·채굴 목표 중복 방지가 사라진다. 머지 후에도 재주입하지 말고 `patch-ai-behavior.py` 를 쓴다.
3. **v7.8.1 계열은 `F9`/`F10` 이 `Shift` 조합이다** (관전 복귀 키와 충돌해서). 상대 버전이 v7.8.0 계열이면 이 부분이 다르다.
4. **`placeTurret` 두 번째 인자**를 추가했다. 상대 버전에 새 호출부가 생겼다면 인자 없이 불러도 동작하지만, 자리 선정이 자동으로 바뀐다는 점만 알고 있으면 된다.
6. **준비 조건이 프레임마다 뒤집히는 스킬은 `lFire()` 로 장전한다.** 관전 리더의 Q 는 `OBS.drillCell`(매 프레임 초기화)을 `lIntent` 의 ready 에 그대로 넣었더니, 조건이 한 프레임 꺼질 때마다 망설임이 처음부터 다시 시작돼 60초에 0회가 됐다. 쿨다운으로만 장전하고 실제 조건이 아직이면 0.25초만 재대기하도록 바꿨다(수정 후 60초에 5회, 간격 7.8~20.4초).
5. **`throwFlare()` 로 통합**했다. 상대 버전이 플레어 인라인 코드를 손봤다면 그 변경을 `throwFlare()` 안으로 옮겨야 한다.

---

## 10. 이번 작업이 건드리지 않은 것

머지 시 그대로 상대 버전 것을 쓰면 된다.

편성 UI · 경험치·특성 풀(`AI_TRAITS`) · 경로탐색(`findPath`/`digCost`) · `AIGEO` 지형 게이트 · 적 타깃 선정(`AI_TGT`) · 피해·다운·구조(`AI.hurt`/`updateDown`) · 탈출 전원 탑승(`updateBoarding`) · 캐릭터 스프라이트 렌더 · 관전 카메라·UI·입력 차단 · 본편 게임 코드 전체.
