# 작업지시서 — 재현도 수정 배치 1~7 (2026-09-07)

전수 감사(`docs/unity-port/fidelity-audit-2026-09-07.md`)의 상위 7개 항목 구현 지시서.
기준(정답)은 `prototype-html/latest/tunnel-crew-infinite-mode-v7.9.2.html` — 아래 줄번호는 이 파일 기준.
작업 착수 직전까지 조사한 프로토타입 실측 값·코드 근거를 전부 담았으므로, 구현 시 프로토를 다시 뒤질 필요를 최소화했다.

## 작업 방식 (사용자 지시)

- **워크트리 하나에서 작업**해 다른 작업과 나중에 한 번에 머지한다.
- 주의 1: `EnterWorktree` 기본 baseRef 가 `origin/main`(구버전)이므로, 생성 직후 `git reset --hard <로컬 main HEAD>` 로 맞출 것 (2026-09-07 기준 4873afc).
- 주의 2: 메인 트리에 **미커밋 보스 재현도 수정**(9개 파일 + `Assets/Art/Tiles/boss-walls/` 에셋)이 있다. 워크트리에 같은 내용을 먼저 복사·커밋해 기반으로 삼으면, 이후 머지 시 동일 내용이라 충돌 없이 합쳐진다. 대상 파일: import-art.mjs, TileSet_purple.asset, TileSetAsset.cs, BuildArtAssets.cs, RunBootstrap.cs, BossIntroCinematic.cs, CombatView.cs, WorldRenderer.cs, BossSystem.cs, boss-walls/(+.meta), fidelity-audit 문서.
- 검증: 순수 Sim 항목은 EditMode 테스트(기존 BossTests 방식 — Unity MCP RunCommand로 직접 호출), 연출 항목은 플레이 모드 스크린샷.

---

## 1. 스카우트 정찰 펄스 (감사 S1) — 체감 상

플레이어 스카우트의 핵심 성장 루프가 통째로 빠져 있다. AI 크루용 구현(`AiCrewSystem.cs:298 ScoutPulse`, `:761 ReconAward`)이 참고 원형.

**프로토 스펙**
- 기본값 (INF 초기 객체 11827행): `scoutPulseRadius:5`, `scoutExploreXp:2`, `scoutPulseMax:2.4`(하이라이트 지속), `scoutPulseCd` 쿨 2.8초.
- 신규 구역 자동 펄스 (12466~12469): 스카우트일 때 매 틱, 플레이어 셀의 **3×3 셀 섹터** 키 `floor(c/3) + floor(r/3) * ceil(COLS/3)` 가 미방문이고 쿨이 0이면 → 섹터 등록, 쿨 2.8, `infScoutPulse(x, y, reward=true)`.
- `infScoutPulse(x,y,reward)` (12385~12396):
  - 반경 5칸(+.15 여유) 내 `ore/gem/crys` 타일 → vein 마크, 반경 내 생존 적 → threat 마크.
  - `scoutHighlights = marks; scoutPulseT = scoutPulseMax(2.4)`.
  - 연출: 링 #7FEBD0 반경 5칸, 버스트 14개(#7FEBD0/#D8FFF5), 텍스트 '정찰 펄스'.
  - reward=true면: XP +2 (recon 채널, 라벨 '탐사 XP' #7FEBD0) + 토스트 `신규 구역 정찰 · 광맥 N · 위협 N` + 레벨 체크.
- Q 플레어 착탄 시에도 펄스 (10577): `infScoutPulse(x, y, false)` — XP 없음.
- 하이라이트 렌더 (`infDrawScoutOverlay` 12485~12497):
  - `scoutPulseT>0` 동안. fade = `min(1, t/.45)`, pulse = `.72+.28*sin(G.t*10)`.
  - 플레이어 중심 링: 반경 `5칸 * (1 - t/max*.22)`, alpha fade*.32, #7FEBD0.
  - 마크: threat는 적 위치 실시간 추적(죽으면 스킵) 붉은 삼각형(#FF557D), vein은 45° 회전 사각형 — 색: crys #7FFFF0 / gem #FF7AA8 / ore #C87AFF. 화면 밖이면 가장자리로 클램프(x 28~LW-28, y 188~LH-36)하고 alpha ×.72.
- 층 초기화(12505)에서 `scoutSectors/scoutPulseT/scoutPulseCd/scoutHighlights` 리셋.
- 연동 특성: `s_watch`(12048) 가 `scoutPulseMax += 1.2` — Unity `RoleTuning.ScoutPulseMax` 를 쓰는지 확인.

**Unity 구현 지점**
- `PlayerBuild.cs:34-35` 기본값 수정: `ScoutPulseRadius 4.0→5.0`, `ScoutExploreXp 1→2`.
- Sim: `RoleSystem`(또는 전용 소형 시스템)에 섹터 Set·쿨·`PulseT`·하이라이트 목록 + 틱 로직, `UseQ` Scout 분기(119~128)에서 플레어 생성 후 착탄점 펄스 호출. XP는 `XpGate` recon 채널(가중치 스카우트 1.0)로 지급.
- 이벤트로 (위치, 광맥 수, 위협 수) 를 Presentation에 넘겨 링/버스트/텍스트/토스트, 하이라이트는 별도 View(또는 CombatView)에서 위 렌더 규칙으로.

## 2. 엔지니어 센트리 자동 재장전 (감사 S2) — 체감 상

**프로토 스펙** (12439): 급전 중(`powered`)에만 진행. `ammo<=0`이면 `reload`를 2.2초로 세팅 후 감소, 0이 되면 `ammo=mag` + 텍스트 '자동 장전'(#7FEBD0) 후 그 틱은 사격 안 함. `INF.engineerTurretReload = 2.2` (11827). 재장전 중 터렛 위 'RELOAD' 라벨 (12459).

**Unity 구현 지점**
- `RoleTuning.EngineerTurretReload` (PlayerBuild.cs:25) `1.0 → 2.2`.
- `Turret` 클래스(RoleSystem.cs:31~39)에 `Reload` 필드 추가.
- `TickEngineer`(RoleSystem.cs:420~464)의 `if (!t.Powered || t.Cooldown > 0 || t.Ammo <= 0) continue;`(440행)를 프로토 순서로 분해: powered 검사 → ammo<=0 이면 reload 진행/완충 → cd 검사 → 사격.
- '자율 전력' 특성(TraitDeck.cs:216, `EngineerTurretReload ×0.85`)이 이 값으로 실효화되는지 확인.
- 뷰(CraftView/설치물 렌더 쪽)에 RELOAD 표시, 재장전 완료 텍스트는 CombatView.Text.

## 3. 채굴 중 조준 회전 저항 (감사 S3) — 체감 상

**프로토 스펙** (7271~7273, 2084~2085, 2477):
```js
const turn = digging ? DRILL_TURN_DIG            // 2.5  — 실제로 벽을 갈고 있는 프레임
           : (sh.drill ? DRILL_TURN_IDLE*.85     // 10.2 — 드릴 가동(버튼 홀드) 중
                       : DRILL_TURN_IDLE);       // 12   — 평시
sh.aim = alerp(sh.aim, aimT, Math.min(1, dt*turn));
// alerp(2477): 최단호 각도 보간 — d=((b-a+3π)%2π)-π; return a+d*k
```
`sh.drill` 은 드릴이 가동된 프레임에 1(7205), 매 틱 초기화(7172) — Unity에선 `input.DrillHeld` 로 근사. `digging` = 실제 타격 중 = `PlayerState.IsDigging`.

**Unity 구현 지점**
- `MovementSystem.cs:34-35` 의 `p.Aim = toAim.Angle` 즉시 대입을 위 각도 보간으로 교체. `LerpAngle` 헬퍼 추가.
- `SimTuning` 에 `DrillTurnDig = 2.5`, `DrillTurnIdle = 12` 상수 추가.
- 주의: `p.Aim` 은 대시 폴백 방향·스킬 조준에도 쓰이므로 회전 저항이 곧 손맛 — 의도된 동작.

## 4. 적 근접 피해 위협 시간 배율 (감사 S4) — 체감 상

**프로토 스펙** (6565~6569): 접촉/근접 피해에 `timeDmg = 1 + max(0, threat-1) * .38` 곱, **보스 제외** (`e.boss?1:timeDmg`). 위협 9에서 약 ×4.

**Unity 구현 지점**: `EnemySystem.cs:327-328`(근접 타격) — `SimTuning.EnemyDamage * e.DamageMul` 에 비보스일 때 `* (1 + Math.Max(0, Threat - 1) * .38)`. `Threat` 프로퍼티는 EnemySystem에 이미 있음. 같은 계열의 접촉 피해 경로가 더 있는지(투사체는 별도 스케일 있음 — 건드리지 말 것) 확인.

## 5. 식생 시스템 + 바닥 보급품 캐시 (감사 S5·S6) — 체감 상, 볼륨 큼

### 5a. 식생 (7728~7823)
- 생성: 열린 칸의 5.5%, 최대 96개, 군집 확률 42%. 종류 4종 + 힐러(치유초). HP 일반 32 / 힐러 115.
- 드릴 선단이 식생을 타격 (7209~7210 `damageVegetation…`), 폭발류는 `damageVegetationRadius` (파쇄탄·보스탄 폭발에서 호출 — infProjectileBurst 13485 참조).
- 파괴 드랍: PULP. 힐러 파괴 시 힐 시드 3~5개 낙하 — 습득 시 최대 HP 6% 회복.
- 일부 종은 생체 발광 광원(라이트 목록에 등재).
- 층 전환 시 재생성.
- Unity: Sim에 VegetationSystem(생성·HP·피해 훅·시드 드랍) + LootSystem에 힐 시드 픽업 + Presentation 식생 View(절차 스프라이트로 시작 가능) + 광원 등록. `MiningSystem`(드릴 샘플)과 폭발 경로에 피해 훅.

### 5b. 보급품 캐시 (5928~5935, 6120~6143, 7322~7345, 12668~12679)
- 생성기: 층당 6개 (`DungeonGenerator.cs:602~611` 에 `CachePlacement` 가 이미 생성됨 — **소비 코드만 없음**). shard 45% / pouch, val 7~13(pouch) / 6~11(shard).
- 습득 (7322~7345): 접근 시 자원 지급 + XP — pouch 4 / shard 9 (loot 채널). 큰 캐시 연출: 킥 2.4 + 플래시 + 링 2개 + 버스트 20 + 텍스트 24px.
- 벽 파괴 드롭 (6120~6143): 일반 벽 파괴 시 4% 보급품 / 16% 룬 조각 계열 드롭, 묻힌 유물 타일 파괴 시 shard(val 14~24) 낙하. (유물 발굴 확률 판정은 Unity에 이미 있음 — 드롭만 추가)
- 자원 습득 XP (INF_LOOT_XP, 12668~12679): pulp .6 / bloom 1.8 / core 2.5 — 소수 누적 후 지급. Unity는 현재 희귀 블록 고정 2 (`TunnelSim.cs:572`) — 교체.
- 룬 조각 SFX: `AudioDirector.Shard()` 정의만 있고 미배선 (프로토 6136 `SFX.shard`) — 함께 배선.

## 6. 데미지 숫자 튜닝 + 히트스톱 출처 게이트 (감사 S7·S8) — 손맛 직결

### 6a. 데미지 숫자 (DEMO 2230~2256 → CombatView.cs:32-33 교체)
| 키 | 프로토 값 | 현 Unity |
|---|---|---|
| dmgSize / Big | **21 / 64** | 25 / 34 |
| dmgPop / PopDec | **1.41 / 2.1** | 1.45 / 3.4 |
| dmgLife (감쇠) | **0.4 (수명↔감쇠 환산 주의)** | 감쇠 2.32/s |
| dmgRise / RiseDamp | **265 / 0.97** | 120 / — |
| dmgGravity | **671** | 980 |
| dmgJitterX/Y | **30 / 27** | 12 / 9 |
| dmgDrift | **80** | 40 |
| dmgFade | **1.1** | 2.2 |
| dmgSquash | **0.5** | 0.22 |
| dmgArcSpread | **169°** | 90° |
| dmgSpin | **2.4** | 1.2 |
| dmgMax | **20** | 44 |
| dmgLaunchRand | **0.45** | — |
| dmgCritTilt | **18** | — |
| dmgZoomCap / ScaleZoom | **1.85 / true** | — |
| dmgOutline / A | **0.18 / 0.55** | — |
| 색 | **#FFF8E8 / big #FFE27A / tint #FF9A3A** | — |
| dmgGhost | **true (잔상 트레일)** | 필드만 있고 미구현 (CombatView.cs:28) |

CombatView의 px→월드 환산 상수(Px)를 유지한 채 값만 이식하고, ghost 트레일(이동 궤적 잔상)을 구현. 프로토의 J.dmg 스텝 공식은 4878~5127 (J 객체) 참조.

### 6b. 히트스톱 출처 게이트 (FEEL.enemyHit 4853)
```js
enemyHit(e,dmg,nx,ny,src){ if(src!=='weapon'||AICREW.dmgSrc) return;  // 내 무기 타격만
  ... this.stop(dead?(apex?58:42):(boss?12:20)); if(!dead) J.kick(boss?.45:(big?1.35:.75), ...); }
```
- Unity `RunBootstrap.cs:171-178` 이 모든 `Sim.EnemyHurt` 에 히트스톱을 걸고 있음 → `EnemySystem.DamageSource`(EnemySystem.cs:444, 이미 존재하나 미사용)를 피해 경로마다 세팅하고, 핸들러에서 **플레이어 무기 출처일 때만** `Feedback.EnemyHit`. AI 크루·터렛·설치물 피해는 제외.
- 함께 대조: 피격 킥(보스 .45 / big 1.35 / 일반 .75), e.hurt 상승치(.25/.19).

## 7. 기타 4건 (감사 S10·S11·M1·M8·M9)

### 7a. 보스 방향 인디케이터 (S10)
프로토 `#infBossIndicator`(11419) + `infPaintBossIndicator`(13257~13270):
- 보스 생존 + 플레이 중일 때만. 화면 안(x 52~LW-52, y 180~LH-92)이면 보스 머리 위(보스 반경*줌+34px 위)에 표시, 밖이면 화면 중심→보스 방향 직선이 경계와 만나는 점에 클램프하고 화살표 각도 `atan2(dy,dx)`.
- 거리 텍스트: `round(hypot(보스-플레이어)/CELL)` + '칸'.
- 스폰 후 1800ms 동안 alert 점멸 (`INF.indicatorFlashUntil`, infSpawnBoss 13251).
- Unity: `RunBootstrap.OnGUI` 에 ➤ 글리프 + 거리 라벨로 이식. Sim 이벤트는 기존 BossSpawned 활용.

### 7b. 관전 리더 Q/E 스킬 + 페르소나 + 기반암 목표 (S11)
프로토 20346~20456:
- `rollLeaderPersona()` — 판마다 리더 손버릇(스킬 사용 성향) 랜덤.
- `lIntent`/`lFire`/`leaderSkills` — 의도 게이트로 직업 스킬 사용: 드릴러 Q 돌파 파기(단단한 벽 앞), 스카우트 Q 플레어(어두울 때)·E 그래플(이동 단축), 엔지니어 E 노드·Q 센트리(+무급전 센트리에 다가가 급전), 거너 Q 방어막(피격 위기)·E 파쇄탄 조기 기폭.
- `pickLeaderBedrock`(20430~20456) — 리더가 드릴러면 기반암을 골라 압력 굴착하는 `rock` goal (drive 20485에서 호출).
- Unity: `ObserverPilot.cs` — `Decide`(78~112)에 rock goal 추가, `Act`(157~214)에서 `input.SkillQPressed/SkillEPressed`(PlayerState.cs:82-83) 설정. v7.8.1 merge-spec(`docs/ai-behavior-v7.8.1-merge-spec.md`)도 참조.

### 7c. 다운 시 부활 자원 소모 순서 (M1)
프로토 `infEndRun`(14900~14906): HP 0 → ① **구조 가능한 동료가 있으면 기절**(다운) 우선 → ② 없을 때만 불사조 유물 → ③ 긴급 재기동 → ④ 런 종료.
Unity `TunnelSim.cs:711-717` 은 ②③을 먼저 소모 — 순서 재배열.

### 7d. 유물 2종 실효화 (M8·M9)
- **r_executioner 처형인의 인장** (6306~6309): `hurtEnemy`에서 피해 적용 후 `hp>0 && !boss && !apex && hp/hpMax<=.12` 면 즉사. 연출: 텍스트 '처형'(#FF557D 18px) + 스파이크 6 + 링. → `EnemySystem.HurtEnemy`(379~417 부근)에 분기. 유물 보유 조회는 RelicSystem 훅(`infRelicHas` 대응) 경유.
- **r_ram 파성퇴 코어** (6302, 13329~13333):
  1) 보스 장갑 생존 시 피해 감쇠 `armorTaken(.22)` 를 `min(1, .22*4)` 로 — '파성퇴 관통'(#FF8D5C) 텍스트 0.8s 스로틀. Unity의 보스 장갑 피해 감쇠 지점(EnemySystem.HurtEnemy의 armor 분기)에 적용.
  2) 보스 소환 벽: 경화(rock) 무효 + HP 1 → `BossSystem.Materialize`(hard 판정 직후) 에 분기.

---

## 검증 체크리스트

- [ ] EditMode: timeDmg(위협 9 근접 피해 ≈ ×4.04), 부활 순서(크루 생존 시 다운·불사조 미소모), 센트리 재장전(탄 소진 2.2초 후 재개), r_executioner/r_ram 분기
- [ ] 시뮬 스크립트: 스카우트로 이동 시 신규 섹터마다 recon XP 적립·쿨 2.8 준수
- [ ] 플레이 모드: 펄스 하이라이트/링, 데미지 숫자 궤적(발사 속도·산포·잔상), 크루 사격 중 히트스톱 없음, 보스 인디케이터 화면 밖 화살표+거리, 관전 리더가 스킬 쓰는지
- [ ] 회귀: 기존 BossTests 9종
