# 땅굴 크루 v7.9.2 — 엔티티 / 전투 / AI 구조 맵 (Unity 이식용)

> 작성: 2026-09-05 · 대상: `tunnel-crew-infinite-mode-v7.9.2.html` (행 번호는 base64 제거 사본 기준이며 원본과 동일. 메인 스크립트 1259~15225, 이후 여러 개의 "런타임 주입 블록")
> 상위 문서: [`../unity-port-plan.md`](../unity-port-plan.md) · 자매 문서: [코어 시뮬레이션](analysis-01-core-sim.md), [셸·오디오·네트·자산](analysis-03-shell-audio-net-assets.md)

---

## 0. 전역 좌표계 · 단위 (먼저 알아야 포팅이 가능)

| 상수 | 값 | 줄 | 의미 |
|---|---|---|---|
| `CELL` | 50 (28~72 가변) | 2034, 2381 | 타일 픽셀 크기. **모든 거리 단위의 기준** |
| `TE_CELL_REF` | 9 | 2078 | 튜닝값 기준 셀 크기 |
| `teWorld(v)` | `v*(CELL/9)` | 2079 | **튜닝 수치 → 월드 픽셀 환산 함수.** CELL=50이면 ×5.556 |
| `teMovePx()` | `teWorld(67)*(1.15/√6.01)` ≈ 174.6 px/s | 2081 | 플레이어 기본 이동속도 |
| `R_SHELLY` / `R_MINION` | 25 / 19 | 2067 | 캐릭터 충돌 반지름 |
| `DRILL_TIP` | `R_SHELLY*2.55` = 63.75 | 2083 | 드릴 접촉 판정 팁 위치 |
| `HPT` | `{dirt:80,stone:140,ore:100,gem:100,crys:100,rock:1e9,core:1e9}` | 2060 | 타일 체력 |
| `SOLIDX(t)` | `rock`/`core` | 2061 | 파괴 불가(기반암) |
| `YIELD` | dirt→pulp1, stone→pulp2, ore→bloom1, gem→bloom3, crys→bloom2 | 2062 | 채굴 산출 |
| `TE` | zoom 6.01, moveSpeed 67, dashDist 10, dashDur 0.11, dashCd 0.91, aimFollow 0.18 | 2071 | 이동/대시/카메라 튜닝 |

> **숨은 결합 주의**: `teWorld()`가 `CELL`을 읽으므로 CELL을 바꾸면 이동속도·투사체 속도·적 속도가 전부 같이 변한다. Unity에서는 CELL=1 유닛으로 정규화하고 `teWorld`를 상수 `5.5556`(px) = `0.1111`(셀)로 굳히는 게 안전하다.

`G` (게임 상태 싱글톤) — **5132~5150**. `cell[]`(타일 타입 배열), `hp:Map<idx,number>`(타일 잔여 체력), `band[]`, `dec[]`, `enemies[]`, `projectiles[]`, `eshots[]`(적 탄), `lamps[]`, `sh`(플레이어), `php/phpMax`, `iframes`, `dash`, `knock`, `stunT`, `downed`, `drillHeat/drillHeatLock/drillWarm`, `comp`(연결성분).
인덱스 헬퍼: `ci(c,r)=r*COLS+c`, `inB`, `solid`, `cxw/cyw`, `toCell` — **5156~5161**.

---

## 1. 플레이어 클래스/역할 (드릴러 · 거너 · 스카웃 · 엔지니어)

### 1-1. 역할 테이블 (두 벌이 공존 — 중요)

**`CREW_ROLES`** — 10046~10055 (미션 모드/레거시)
```
driller  dig 2.00  gun false gunMul 0    dash 1.0  weapon drill  Q 돌파파기(7s)  E 없음
scout    dig 0.35  gun true  gunMul 1.0  dash 1.4  weapon gun    Q 플레어(5s)   E 없음
engineer dig 0.75  gun true  gunMul 0.9  dash 1.0  weapon gun    Q 발판(6s)     E 터렛(12s)
gunner   dig 0.12  gun true  gunMul 1.5  dash 0.9  weapon gun    Q 방어막(8s)   E 없음
```

**`INF_ROLES`** — 11818~11823 (무한/본편 모드. **실제 사용되는 최신 밸런스**)
```
driller  code EXCAVATION    dig 2.00 gunMul 0.65 dash 1.0  weapon drill  Q 돌파파기 7s   E 없음      #ffd36e
gunner   code FIRE SUPPORT  dig 0.00 gunMul 1.50 dash 0.9  weapon gun    Q 방어막 8s     E 조기기폭 0s #ff8d72
scout    code RECON         dig 0.35 gunMul 1.00 dash 1.4  weapon gun    Q 장거리플레어 5s E 그래플훅 5s #7febd0
engineer code FORTIFICATION dig 0.75 gunMul 0.90 dash 1.0  weapon gun    Q 전력노드 7s   E 센트리 10s  #c7a0ff
```
- 파생 함수: `crewDigMul()/crewGunMul()/crewDashMul()` — **10182~10184** (`CREW.role`을 읽음).
- 런타임 상태 컨테이너 `CREW` — **10061~10069** (phase, qCd, eCd, shieldT, breachT, turrets[], plats[], drone).
- 대형 런타임 상태 `INF` — **11824~11828** (역할별 파라미터 전부가 이 한 객체에 평면으로 들어있음. Unity에선 역할별 컴포넌트로 분해 필요).

### 1-2. 공통 스탯

- 플레이어 HP: `DEMO.playerHp = 181`, i-frame `DEMO.playerIFrame = 0.59` — **2342~2343**. 초기화 `G.phpMax=DEMO.playerHp` — **6053**.
- 사격 쿨: 기본 0.22초, 거너 0.14초, `/INF.fireRate` — **1966~1967**.
- 탄창/재장전: `magSize 12`, `reloadTime 1.35` — **11827**. `infStartReload/infUpdateReload` — **12232~12233**.
- 대시: `tryDash()` — **1904~1912**. 거리 `teWorld(10)*crewDashMul()`, 지속 0.11초, 쿨 0.91초.
- 드릴: `shelDps() = 88+12*SAVE.lv.mine` — **2463**. `DRILL_DMG()=DEMO.drillDmg(2)`, `DRILL_SPD()=4` — **2345~2346**.
- 드릴 예열/과열: `drillWarmMul()` **2347**, `drillSpinMul()` **2354**, `drillHitInterval()` **2355**, `drillCanUse()` **2361**. 튜닝은 `DEMO.drillWarm*/drillHeat*` — **2180~2189** (warmTime 1.44, heatBuild 0.13, heatCool 0.45, heatLock 2.43).

### 1-3. Q/E 스킬 구현

**진입점**: `crewSkillQ()` — **10562~10598**, `crewSkillE()` — **10599~10613**. 키 바인딩은 **9470** (`k==='q'`).
INF 모드에서는 각각 전용 함수로 위임됨:

| 역할 | Q | E |
|---|---|---|
| 드릴러 | `CREW.breachT=0.55` 세팅 → `updateCrew()` **10633~10643**이 매 프레임 전방 3칸(특성 시 5칸/3폭) 관통 굴착. 쿨 `7 × drillerQCdMul × drillerTraitQCdMul` | — |
| 스카웃 | 조준 방향 2.6칸에 `G.lamps` 플레어 push (rad `max(lampRadius*1.45, CELL*4.2)*scoutFlareRadMul`, ttl `22*scoutFlareLifeMul`, visionRange `5+scoutVisionBonus`) + `infScoutPulse()` — **10569~10578, 12385** | — |
| 엔지니어 | `infPlaceEngineerNode()` — **12412~12420**. 전력 노드(life 50, radius 4칸). 최대 `engineerMaxNodes(2)` | `infPlaceEngineerTurret()` — **12421~12427**. 센트리(life 45, mag 18, range 5.5칸, interval 0.34, power 0.72) |
| 거너 | `CREW.shieldT=2.8` + `G.iframes=2.8` — **10594~10597** | `infDetonateBreaker()` — **12329~12333**. 부착된 파쇄탄 조기 기폭 |

**거너 주무기(LMB)** = 파쇄탄: `infFindBreakerTarget()` **12305**, `infTryFireBreaker()` **12310~12316** (벽에 부착, 신관 `breakerFuse=2`s, 쿨 `breakerMaxCd=12`s, 사거리 8칸), `infExplodeBreaker()` **12318~12328** (반경 `breakerRadius` 정사각, 중심 1.12배·직교 0.72·대각 0.55 감쇠, 적 반경 `CELL*(1.6+(rad-1)*0.65)`).

**드릴러 주무기(LMB)** = 기반암 균열: `infDrillerPressure(c,r,dt,hx,hy,boost)` — **12350~12359**. 크랙 맵 `INF.drillerCracks:Map<idx,{p,last,type,stage}>`, 필요압력 `need = (core?22*coreNeedMul*(mantle?0.65:1):14) * (1+(depth-1)*0.22)`. 완료 시 `infBreakFoundation()` **12338~12349** (충격파 `drillerShockRadius`, XP `crack 8 / crackCore 12`). 감쇠 `infUpdateDrillerCracks()` **12360** (hold 2.5s, decay 0.012/s).

**스카웃 E** = `infUseScoutGrapple()` — **12373~12384**. 조준 직선으로 최대 `scoutGrappleRange(5)`칸 레이캐스트 후 `G.dash`를 0.22초짜리로 강제 세팅. 자동 정찰 펄스는 `infUpdateRoleTools()` **12466~12470**에서 3×3 타일 섹터 신규 진입 시 발동.

**엔지니어 센트리 루프** = `infUpdateEngineer(dt)` — **12432~12456**. 전력 판정: 노드 반경 안 / 플레이어가 2.25칸 이내 / `engineerAutonomous`(0.5배 출력). 급전된 센트리만 사격.

**역할 툴 통합 틱**: `infUpdateRoleTools(dt)` — **12461~12477** (`updateCrew`에서 호출, 10624).

### 1-4. 스프라이트 (8방향)

- 레거시 벡터: `drawShellyVector()` — 2757.
- 8방향 데이터 URL 시트: `MINER_SPRITE_DATA` (S/SW/W/NW/N/NE/E/SE × idle/walk4/drill4) — **2796**. 방향 계산 `minerDir8(a)` — **2816**.
- **4개 역할 액션 시트 (실제 사용)** — 각각 5방향(`sw,w,nw,n,s`) 시트 + E/NE/SE는 X미러:
  - 거너 `GUNNER_SHEET_ROOT='assets/characters/reely-driller-actions/sheets/'`, `{walk:14, fall:11}` — **2824~2875**
  - 엔지니어 `reely-5279-actions`, `{walk:11, fall:16}`, 피벗 `{sw:163,w:179,nw:182,n:180,s:153}` — **2877~2926**
  - 스카웃 `reely-1530-actions`, `{walk:11, fall:16}`, 피벗 `{sw:161,w:158,nw:165,n:183,s:164}` — **2927~2974**
  - 드릴러 `reely-2851-actions`, `{walk:11, fall:16}`, 피벗 `{sw:164,w:176,nw:178,n:165,s:176}` — **2976~3020**
  - 셀 크기 224×224, 그리는 크기 `h=r*5.525`(= 138px @ r=25), `fall`은 다운 상태 애니메이션.

---

## 2. 투사체 시스템

### 2-1. 데이터 모델
`G.projectiles[]`의 원소 (생성: `tryFireGun()` **1946~1967**):
```js
{ x,y, vx,vy, life,
  pierce, bounces, explosive, laser,
  power,            // 동시사용(sync) 배율
  lastCell,         // 같은 타일 중복 데미지 방지
  ai, aiMul, aiOwner, srcTurret,   // AI 크루/터렛 소유권 태그
  visualId, visualAge, visualRot, trail[] }  // projectileWithVisual() 1943
```
- 속도: 일반 `teWorld(280)`, 레이저 `teWorld(480)`. 수명 1.2 / 0.72s.
- 다중샷: `INF.shots`(최대 5), 스프레드 0.13rad(레이저 0.045).
- 레이저 주기: `INF.laserEvery`, `shotCounter % laserEvery === 0` — **1954**.
- 비주얼 ID 결정: `projectileVisualId()` — **1935~1942** → `laser / rain / explosive / ricochet / pierce / multi / standard`.

### 2-2. 갱신 / 충돌
`updateProjectiles(dt)` — **1968~2015**. 순서가 그대로 우선순위:
1. 위치 적분 → `TUNNEL_PROJECTILE_FX.stepProjectile()`
2. **적 충돌**: `hypot < e.r+6` → `hurtEnemy(e, DEMO.enemyGunDmg(22) * crewGunMul * INF.gunMul * power * (laser?1.65:1), nx,ny, srcTurret?'turret':'weapon')`. pierce>0이면 12px 밀고 계속.
3. **식생 충돌**: `hitVegetationAt()`
4. **타일 충돌**: 파괴 가능 타일이면 `damage(c,r, shelDps()*0.28*INF.gunWallMul*power*(laser?1.8:1))`. `lastCell` 가드. pierce → 0.72칸 관통 / bounces → 축반사(`|dx|>|dy| ? vx*=-1 : vy*=-1`) / else 소멸. `SOLIDX`면 즉시 소멸.
5. 소멸 시 `explosive`면 `infProjectileBurst(x,y)` — **13482~13487** (3×3 벽 피해 + 반경 1.35칸 적 피해).

렌더: `drawProjectiles()` — **2016~2022** (`window.TUNNEL_PROJECTILE_FX` 있으면 위임).

### 2-3. 투사체 카탈로그 (11종)
`LAB_DEFS` — **15230~15242**: `standard, support, multi, pierce, ricochet, shard, explosive, rain, laser, bossScatter, bossBarrage`. 티어 1~4. `window.TUNNEL_PROJECTILE_LAB={profiles, definitions,...}` — **15455** (프로파일에 `telegraphRadius, telegraphColor, telegraphAlpha, arcHeight, haloSize, coreSize, glowColor, glowBlur, blendMode` 포함 — 보스탄 경고원 반경이 **여기서** 나옴, 13282~13284).

### 2-4. 적 투사체 (별도 배열)
`G.eshots[]` — 생성 `enemyFireShot()` **6588~6596** `{x,y,vx,vy,life:2.6,r:6.5,t,dmg}`. 갱신 `updateEnemyShots()` **6597~6619**: 타일 충돌 → 소멸, `AICREW.hitTest` → AI 크루 피격, 플레이어 `R_SHELLY+s.r` 원충돌. 렌더 **6620~6632**.

### 2-5. 보스 투사체 (예고형 — 3번째 계열)
`INF.bossShotQueue[]` → `INF.bossShots[]`. `infQueueBossShot()` **13277**, `infBossProjectileTick()` **13456~13459**, `infBossProjectileHit()` **13449~13455**, 렌더 `infDrawBossProjectiles()` **13460~13467**.
- 물리 시뮬레이션이 아니라 **`(sx,sy)→(tx,ty)`를 `flight`초에 걸쳐 보간하는 포물선 연출** + 착탄 시점에 `rad` 원 판정. Unity 포팅 시 이 점 매우 중요.

---

## 3. 몬스터 / 적

### 3-1. 종류 (3종 + 보스, 모두 같은 구조체)
`kind` 3가지 — **6272**:
```
crawler     근접, r = enemyRadius*0.86*sizeVar, 도약 능력 보유
spitter     원거리/근접 혼합 (ranged 플래그로 분화), r = enemyRadius*1.0*sizeVar
broodBeast  광란종(apex) 및 보스가 쓰는 시트, r = enemyRadius*1.28
```
`ranged`는 kind와 별개 플래그: `Math.random() < DEMO.enemyRangedRatio(0.2)` (apex 제외) — **6271**.

### 3-2. 스탯 (`DEMO`, 2296~2343)
```
enemyOn true, enemyInterval 7.5, enemyChance 0.64, enemyMax 16
enemySpeed 9.8 (→ teWorld 환산 = 54.4 px/s 기준값), enemyHp 81, enemyDmg 5.5
enemyAtkCd 1.5, enemyAggro 320(teWorld → 1778px), enemyRadius 22, enemyTouch 12
enemyKnock 70, enemyDrillMul 1.15, enemyGunDmg 22
분리: enemySepRatio .86 / enemySepRate 5.2 / enemySepMaxPush 240 / enemySepBossRate 24
배회·시야: enemyWanderRadius 5.2칸, enemyWanderSpeed .42, enemyVisionMul 1.0,
          enemyVisionFov 62°(반각), enemyNearSense 1.5칸, enemyLoseTime 5.0s
근접 모션: enemyWindup .42 / enemyStrike .12 / enemyRecover .34 / enemyReach 16px
원거리:   enemyShotSpeed 60, enemyShotDmg .72(배율), enemyShotCd 2.6,
          enemyKeepMin 3.0칸, enemyKeepMax 5.6칸, enemyFleeSpeed 1.18, enemyRangedWindupMul 1.2
도약:     enemyJumpChance .04
보스 물리: bossDashKnockMin/Max 2/4칸, bossDashStunChance 1.00, bossDashStunTime 1.0
```

### 3-3. 스폰 규칙
- 위치: `pickEnemySpawn()` — **6242~6259**. 빈 타일 중 플레이어로부터 `aggro*0.55 ~ aggro*1.85` 링. 실패 시 4칸 밖 아무 곳.
- 타이밍: `updateEnemies()` **6954~6960**. `G.enemyCd` → 무한모드는 `infSpawnInterval()` = `max(0.62, 5.2/(1+(threat-1)*0.55))` **12228**, 버스트 `infSpawnBurst()` = `1+floor((threat-1)/1.75)`, 최대 5 **12229**. 추가로 `INF.spawnDebt`(블록 파괴마다 +0.30~0.72, **12552**)를 소비해 한 번에 최대 +4.
- 상한: `infEnemyCap()` = `min(64, 18+floor(0.5*(floorBroken/3 + floorTime/15) + threat*1.5))` — **12227**.
- **위협도(핵심 난이도 커브)**: `infThreatValue()` = `min(9, 1 + 0.5*((depth-1)*0.35 + floorTime/90 + floorBroken/28))` — **12226**.
- 생성: `spawnEnemy(forceApex)` — **6260~6293**.
  - apex 확률 `min(0.18, 0.025 + (threat-1)*0.022)`
  - HP = `DEMO.enemyHp * INF.enemyHpMul * (1+(threat-1)*0.55) * (apex?8:1)`
  - **런타임 HP 리스케일**: 이미 살아있는 적도 threat이 오르면 HP/HPMax를 비례 상향 — **6967** (`threatHpMul`)
  - 속도 = `enemySpeedFromSize(r,apex,ranged)` **6410~6416**: `(baseR/r)^1.35`, apex ×1.18, ranged ×1.08, clamp 0.55~1.7 → **덩치가 곧 속도**
  - 피해배율 `damageMul = apex?2.35:1`

### 3-4. 행동 상태 기계
`updateEnemyAI(e,dt,spd,timeSpd,dist,dx,dy)` — **6719~6815**.

```
stunT>0 ─────────────► 완전 정지 (넉백만 적용)
        │
        ▼
  시야 갱신 (0.1~0.15초 주기, e.sightCd)
  enemySeesPlayer(e) 6431~6441:
    dist<=aggro && (dist<=nearSense*CELL || 정면 62° 원뿔) && sightClear()
        │ 보임                              │ 안 보임 lostT += dt
        ▼                                   ▼ lostT >= 5s → wander로 복귀(home 재설정)
   ai='chase' (+ enemyAlertFx '!')
        │
        ▼
  atkState 있으면 이동 불가:
    windup(0.42s, ranged ×1.2) → strike(0.12s, 여기서 판정) → recover(0.34s) → atkCd
        · 근접: enemyMeleeStrike() 6577 — reach = r+R_SHELLY+enemyReach*1.7,
                방향 내적 >= 0.32 이어야 명중 (옆으로 빠지면 헛스윙)
        · 원거리: enemyFireShot() 6588
        │
        ▼
  chase 분기
    ranged: d<keepMin(3칸) → enemyFleeDir()로 후퇴(×1.18)
            d>keepMax(5.6칸) → 접근
            사이 → 좌우 스트레이프(×0.5), 시선은 플레이어 고정
            쿨 되면 정지 후 발사
    근접:   d<=reach && atkCd<=0 → enemyBeginAttack()
            d<=reach*0.9 → 감속하며 대기
            미시야 → lastSeen 지점으로 이동(×0.9)
    → wander: idle(주변 스캔, faceA 회전) ↔ walk(enemyPickWander로 home 반경 5.2칸 내 목표, ×0.42)
```
- **도약**(crawler 전용): `updateEnemies()` **6980~6986**. chase 중 거리 2.4칸~aggro, 확률 `dt*0.04`, 지속 0.30s, 속도 `teWorld(42~52)`, 쿨 4.8~10.3s.
- **접촉 피해는 보스만** — 잡몹은 공격 모션으로만 타격 (**7011~7015**의 주석 참조).
- **분리(soft separation)**: `separateEnemies(dt)` — **6635~6667**. 반지름 합 ×0.86까지는 겹침 허용, 초과분을 `sepRate`만큼씩 해소, 질량 = `1/max(4,r)`, 보스는 질량 0(안 밀림) + `bossStep`으로 상대만 즉시 밀어냄. 플레이어/AI 크루용은 `separatePlayers(dt)` **6671~6701** (플레이어 질량 0.35).
- 프레임 드래그: `e.vx*=0.86`, 넉백 `kbx *= exp(-7.2*dt)` (`KNOCK_DRAG=7.2`, **6832**) — 이동거리 = 초기속도/7.2.
- **AI 크루 타겟팅 훅**: `AI_TGT(e)` — **18621~18640**. 0.6초 히스테리시스로 가장 가까운 크루 선택. 적 AI 전체가 `G.sh` 대신 이 함수를 경유함(6432, 6578, 6589, 6720, 6976).
- 메인 루프: `updateEnemies(dt)` — **6947~7034**. 상태이상 틱(`infRelicEnemyTick`), 빙결(`frozenT`), 슬로우(`slowT/slowMul`), 드릴 접촉 피해(**7003~7010**), 시체 필터(`hp>0 || bossDying`), 주변 울음소리.

### 3-5. 스프라이트 / 애니메이션
`ENEMY_SPRITE_BASE = 'monster_assets_v1.5.4/frames'` — **2098**.
`ENEMY_SPRITES = {crawler:[], spitter:[], broodBeast:[]}`, 폴더 `crawler / spitter / brood-beast`, 파일 `frame_01.png ~ frame_16.png` (각 16장) — **2099~2107**.

프레임 인덱싱 `enemySpriteFrame(e,moving)` — **2144~2154**:
```
blinkT>0 → set[8 + clamp((0.24-blinkT)/0.06,0,3)]   (idx 8~11  = 눈 깜빡임)
!moving  → set[6 + (floor(animT*2)&1)]              (idx 6~7   = 대기 2프레임)
moving   → set[base + floor(animT*10)%6]            (idx 0~5   = 보행 6프레임)
           base = 12 if (apex && speed>8)           (idx 12~17 = 광란종 질주)
```
> **중요**: 몬스터 시트는 **8방향이 아님**. 단일 방향 16프레임 스트립이고, 좌우 반전조차 잡몹에는 적용하지 않음(보스만 `facing`으로 X스케일 반전, 7072~7074). 8방향 시트는 **플레이어 캐릭터 전용**(§1-4).

렌더: `drawEnemies()` — **7047~7093**. 카메라 컬링 → LOS 컬링 → `lightAt()` 감쇠 → bob/jump lift/lunge 오프셋 → 그림자 타원 → `LIT.spr()` (조명 적용 스프라이트) → 체력바(보스/apex 제외). 크기 `e.r*3.15` (apex 3.55, 보스 3.3).

### 3-6. 사망 연출
`spawnGore(e)` **6897**, `spawnGoreBurst()`, `GORE_MAX=120` — **6896**. 보스 ×2.7, apex ×1.5 스케일. 색: 보스 `#4A0C10`, apex `#4E0B22`, 일반 `#340D2E`.

---

## 4. 보스 (레드 파이어 드래곤 / 암반 포식자)

### 4-1. 등급 (tier) — `INF_BOSS_TIER` **13229~13237**
```
finalDepth = INF_PLANET.strata.length (=3)
guardian (심층 수호자, depth < 3): hp×.38 size×.62 dmg×.65 speed×1.45 proj×.5 summon×.5 dashBonus+.25 core 2
                                   → 장갑 없음, 페이즈 없음
apex     (암반 포식자, depth == 3): 전부 ×1, core 6
variant  (변종, depth > 3=이상지대): hp×1.12 dmg×1.15 speed×1.05 dashBonus+.1 core 6
```
`infBossTierForDepth(d)` **13239**, `infBossTierDef(e)` **13243**.

### 4-2. 스폰
`infSpawnBoss()` — **13245~13256**. 트리거는 **장악도**: `INF.floorBroken / INF.totalBreakable >= infDominanceTarget()` (`INF_DOMINANCE_TARGET=.40` 기본값, 11813; 지층별 실제 목표는 `INF_PLANET.strata[].dominance` .22/.26/.30, 이상지대 .34) — 호출부 **12570** (`infOnBlockBroken`) 및 AI 크루 경로 **17694~17696**.
```
hp   = DEMO.enemyHp(81) * (11.2 + (depth-1)*2.2*0.5) * INF.enemyHpMul * INF_BOSS_POWER(10) * tier.hpMul
r    = DEMO.enemyRadius(22) * 3.65 * tier.sizeMul * INF_BOSS_SIZE_MUL(2.5)   ≈ 200px
speedMul   = 1.35 * tier.speedMul
damageMul  = 1.55 * 10 * tier.dmgMul
kind = 'broodBeast', armorCells:[], armorStage:0, patternCd 4.2, attackCd = rand(4.2~6.2)
guardian 이 아니면 infBossSpawnArmor(e, 6)
```

### 4-3. 튜닝 상수
`BOSS_TUNE_FALLBACK` — **2114~2125** (조명/공격간격/돌진/먼지/천장붕괴/입 위치/그림자/흔들림 전부).
`BOSS_LAB_DEFAULTS` — **11835**: `maxHp 9072, moveSpeed 1.35, size 3.65, contactDamage 72, projectileDamage 20, projectileCount 6, projectileRadius 1.12, projectileFlight 1.9, summonInterval 5.96, summonCount 2, initialArmor 6, phaseArmor 7, armorDamageTaken .22`.
접근자: `bossSpec()` **2127**, `bossSpecTierMul()` **2129**, `bossTune(key)` **2130**, `bossRandomRange()` **2131**.
주요 값: `attackInterval 4.2~6.2`, `attackLead 1.1~1.8`, `dashChance .22`, `dashSpeedMul 4.4`, `dashDuration .55`, `dashDamageMul 1.6`, `dashCeilingChance .2`.

### 4-4. 메인 틱 & 페이즈
`infBossTick(e,dt)` — **13468~13481**:
```
fireBreathT 감쇠
infBossProjectileTick(dt)   // 예고탄 큐 소화
infBossWallTick(dt)         // 벽 소환 큐 소화
patternCd / attackCd / wallGimmickCd 감소

[장갑 페이즈] (guardian 제외)
  hp/hpMax <= .66 && armorStage<1 → armorStage=1, infBossSpawnArmor(e,7), spawnEnemy()×2
  hp/hpMax <= .33 && armorStage<2 → armorStage=2, infBossSpawnArmor(e,8), spawnEnemy()×2, kick(7)

[소환] patternCd<=0 → summonInterval 리셋, spawnEnemy() × summonCount
[벽 기믹] wallGimmickCd<=0 && 비전투중 → infBossWallGimmickStart(e), 쿨 9~15s
[공격]   attackCd<=0 → bossForcedRanged>0 이면 강제 원거리, 아니면 infBossStartPattern(e)
```

`infBossStartPattern(e, forcedType)` — **13288~13306**:
- `attackLead = rand(1.1~1.8)` 예고시간, `dash = rand < dashChance + tier.dashBonus`
- **돌진**이면 `bossBeginDash(e, lead)` — **6373~6380**
- **원거리**면 fireBreath 애니 재생 (`baseDuration 2.6 / animRate`), `chargePow = clamp(lead/attackLeadMin, 0.6, 1.25)` — 예고가 길수록 탄 크기·반경·피해 증가
  - 48% 확률 **산발 붕괴탄** (`bossScatter`): 타깃 주변 0.45~2.9칸 랜덤 산포
  - 52% **연속 포격** (`bossBarrage`): 0.32칸 간격 일렬, 0.30초 시차
  - 타깃은 플레이어 속도 0.45초 리드 예측

### 4-5. 돌진 (Dash) — 3단계 FSM
`updateBossWander(e,dt,spd,timeSpd)` — **6381~6403**:
```
bossDashState: null → 'windup'(lead초, 정지 + 텔레그래프) → 'charge'(dashDuration 0.55s, spd×4.4)
  charge 중: bossMovementShake / bossDashVfxTick / bossDashImpact 매 프레임
  종료: kick(dashImpactShake=6), bossBeginWanderIdle
비돌진 시: idle(2.2~5.4s) ↔ walk(목표점 2.5~7.5칸, 2.6~6.4s), 속도 min(1.65, 0.76*speedMul*timeSpd)
```
- 텔레그래프 렌더: `drawBossDashTelegraph(e)` — **7035~7042** (붉은 그라디언트 캡슐 + 대시 라인 + 화살표).
- 충격: `bossDashImpact(e)` **6858~6883** → `bossDashLaunch(target,nx,ny)` **6834~6855**. 넉백 거리 2~4칸(`v = CELL*tiles*KNOCK_DRAG`), 100% 확률 1초 기절. 잡몹과 플레이어 모두 날림(중복 방지 `bossDashHits:Set`).
- 부가: `bossSpawnCeilingCollapse(e)` **6356~6364** — 20% 확률로 화면 전체 천장 붕괴 파동(`J.ceilingArea`, `BD_MAX=360` 낙하물, 1362).
- 체력 30% 이하: `infBossDashPrison(e,lead)` **13426~13448** — 돌진 진로 양옆에 협곡 벽 생성.
- **보스는 이동만으로 벽을 부숨**: `bossCrushWalls(e,dt)` — **6335~6338** (반경 `max(CELL*0.8, r*0.92)`, 장갑 셀 제외).

### 4-6. 벽 소환 기믹 (3종)
`infBossWallGimmickStart(e)` — **13419~13424**: 40% field / 35% wave / 25% prison.
- `infBossWallField(e)` **13361~13378** — 주변 2~4개 덩어리, 후속 원거리 3연발
- `infBossWallWave(e)` **13379~13400** — 플레이어 방향 파도 장벽(길이 22~48, 두께 2, 1.0초에 훑음), 기존 벽은 붉은 보스 벽으로 전환
- `infBossWallPrison(e)` **13401~13418** — 플레이어 주위 반경 2.8칸 원형 감옥
- 공통 재질화: `infBossWallMaterialize(w)` **13316~13344**. `stone` 기본, 확률 `0.05+(1-hpRatio)*0.20`로 `rock`(경화). 소환 벽 HP = `HPT.stone * wallHpMul * INF_BOSS_WALL_HP_MUL(5)` — **11816, 13332**. 플레이어 1.05칸 내/보스 몸통 위에는 생성 금지.

### 4-7. 장갑 시스템
`infBossSpawnArmor(e,want)` **13272~13276** — 보스 몸통 밖 링(`r/CELL+0.7 ~ +1.55`)에 `stone` 타일 생성, `e.armorCells[]`에 인덱스 저장.
`infBossArmorAlive(e)` **13271** — 살아있는 장갑 셀 수.
`hurtEnemy()` **6299~6304** — 장갑이 남아 있으면 받는 피해 `× armorDamageTaken(0.22)`. 유물 `r_ram`이면 ×4 완화.
**보스는 피격 넉백 면역** — **6316~6317**.

### 4-8. 등장 시네마틱 (`TCBOSSFX`, FX7.3.1)
DOM+CSS 오버레이 `#tcBossFx` — **15818~15853**, 로직 **15854~16090**.
```
SEQ = { dim .55, pan 1.05, hold .35, roar 1.75, back .95 }   총 4.65초
T1=.55  T2=1.60  T3=1.95  T4=3.70  T5=4.65
FX.start(b) 15902: CREW.phase='bossIntro' (월드 정지, 렌더만) → 레터박스·비네트 인
FX.tick()   15951:
  0~T1     레터박스 11.5% + 비네트 + 저주파 rumble (AU.tone 41Hz/63Hz)
  T1~T2    카메라를 플레이어→보스로 ease 보간, G.Z를 1.15배 줌인
  T2~T3    이름 플레이트 인 (tier 문구: DEEP GUARDIAN / APEX PREDATOR / VARIANT)
  T3       포효: bossFireBreathT=99 (fireBreath 26프레임 재생), J.kick(13), 링×2, 버스트 42
           animT >= 1.4 (15프레임) 도달 시 dragon-boss-roar.wav 재생 (실전 발사 프레임과 동일)
  T3~T4    천장 먼지 0.085초 간격, 랜덤 kick
  T4~T5    카메라 복귀 + 페이드아웃
FX.stop()   16025: CREW.phase 복구, G.Z 복구
훅: window.infSpawnBoss 를 런타임 래핑 (16038~16045)
HUD: body.tcBossCine 클래스로 DOM UI opacity 0, 캔버스 UI는 FX.uiAlpha()로 paintUI 래핑(16048~16055)
```
`tcClampCamera()`를 쓰는 `camFor(x,y)` — **15889~15892**.

### 4-9. 사망 시네마틱 (`TCBOSSDEATHFX`, FX7.5.2)
**16090~16209**. `SEQ={bars .34, death 2.4, fadeAt 2.75, fadeLen .5, outAt 2.95}`, `TOTAL=3.5`.
```
FX.start(b,after) 16124: CREW.phase='bossDeath', b.bossDying=true, b.bossFade=1
  → bossDragonAnimation()이 'death' 키(24프레임 @10fps) 강제 (2137)
  → 보스를 월드에 남겨둬서 조명·안개·LOS가 그대로 적용됨 (v1의 DOM GIF 방식 폐기)
deathSfx() 16121: sawtooth 116Hz + triangle 58Hz + AU.hit
tick 16139: 레터박스 → 카메라 푸시인(1.13배) → 2.75s부터 bossFade 0으로 → 이름 타이틀
finish 16154: removeBossFromWorld(), 콜백 실행
```
후속 정산 체인: `infBossDefeated(e)` **13493~13502** → `infBossStartOutro()` **13503** → `infBossOutroTick()` **13509~13524** → `infBossShowRestUI(tier)` **13525~13535** (전설 특성 3장 선택 + 하강 버튼).
- 사망 시네마틱이 붙으면 아웃트로 2.6초는 0.32초 게이트로 축소됨 — **16167~16169**.
- 보상: `INF.core += tier.coreBase + depth`, HP 30% 회복, `infRelicBossDrop()`, apex/variant면 `infAutoSummonEscape()`.

### 4-10. 보스 BGM
`BOSS_BGM` — **16222~16365**. 파일 `assets/audio/bgm/boss-blood-ascendant.mp3`, level .78, fadeIn .55, fadeOut 2.8. Web Audio(무음 트림 루프) + `<audio>` 폴백(file:// 대응).
`BGM_ROUTE.useBoss()` **16384~16398**, `endBoss({fade,resumeAfter})` **16400~16415**.
훅 (**16429~16460**, 전부 런타임 래핑):
```
infSpawnBoss   → BGM_ROUTE.useBoss()        // 등장 연출과 같은 프레임
infBossDefeated→ endBoss({fade:3.0, resumeAfter:2.6})
infEndRun      → endBoss({fade:1.4})
infInitFloor   → endBoss({fade:1.0})
```
기본 경로: `LOBBY_MUSIC` / `PURPLE_MUSIC` / `procedural(MUS)` — **4272~4380**, 실제로는 `AMBI` 앰비언스로 스왑됨 **4606~4622**.

### 4-11. 보스 드래곤 애니메이션
`BOSS_DRAGON_ANIMS` — **2108~2113**:
```
idle       37프레임 10fps loop   assets/red-fire-dragon/idle-frames/frame_01.png ...
walking    37프레임 10fps loop   walking-frames/
fireBreath 26프레임 10fps once   fire-breath-a-frames/
death      24프레임 10fps once   death-frames/
```
선택 로직: `bossDragonAnimation(e,moving)` — **2134~2143**. 우선순위 `bossDying > fireBreath > (moving ? walking : idle)`. 속도 배율 `e.bossAnimRate`.

---

## 5. AI 크루 (`AICREW` / AI_HUMANIZE_V1)

주입 블록: **16603~18820** (`<!-- AI_CREW_INJECTED_V1 -->`, 원본은 `ai/crew-ai.js`, 주입기 `ai/inject-ai-crew.py`).
전역: `window.AICREW = AI` — **16640**. 최대 3명(`AI.max=3`, 16633).

### 5-1. 역할 행동 예산 `KIT` — **16650~16704**
```
driller  digMul 1.35 gunMul .55 fireCd .30 mag 10 reload 1.7 range 6.5
         drillMelee true, crack true, breachCd 8, alert 12, intercept 5,
         pathDigCost 5, engage 3.6, hp 210
gunner   digMul .10  gunMul 1.55 fireCd .15 mag 22 reload 1.9 range 9.5
         pathDigCost 30, alert 20, intercept 13, engage 5.2, hp 240,
         breakerCd 9, breakerRadius 1, breakerAtk true
scout    digMul .42  gunMul 1.0  fireCd .22 mag 14 reload 1.5 range 8
         pathDigCost 12, alert 17, intercept 9, engage 4.6, hp 175,
         flareCd 7, pulseCd 9, grappleCd 6, exploreCd 11, dashMul 1.4
engineer digMul .75  gunMul .9   fireCd .26 mag 16 reload 1.7 range 7.5
         pathDigCost 10, alert 14, intercept 7, engage 4.4, hp 200,
         maxTurrets 2, turretCd 12, turretLife 55, turretMag 14,
         turretRange 6.5, turretRate .34, turretPower .72,
         maxNodes 2, nodeCd 16, nodeLife 70, nodeRadius 4.2
```
> 각 멤버는 `Object.assign({}, KIT[role])` **사본**을 들고 다니며(17190), AI 특성은 이 사본만 수정 → 크루마다 다른 빌드가 된다. 사람 `INF` 배율과 완전히 분리.

### 5-2. AI_HUMANIZE_V1 — 3겹 필터 (**16721~16827**)
1. **성향 `pers`** — `rollPersona(roleId)` **16742~16763**:
   `eager .60~1.45, discipline .55~1.00, aggression .60~1.35, caution .55~1.35, greed .35~1.35, curiosity .50~1.45, focus .55~1.00, aimErr .022~.085 rad, reloadAt .26~.62, react .11~.26s, oreBias .55~1.50, strafe ±1`
   직업 보정: gunner aggression×1.15/curiosity×0.85, scout curiosity×1.35/caution×1.10, driller discipline×1.10, engineer discipline×1.15/aggression×0.90.
2. **기분 `mood`** — `updateMood(m,dt)` **16765~16770**: 8~20초마다 0.72~1.28 재추첨.
3. **의도 게이트 `intent(m,tag,ready,cfg)`** — **16782~16794**:
   준비되면 `G.t + rand(min,max)/eager` 절대시각으로 대기 → 시각 도달 시 `p*(0.62+discipline*0.48)` 확률 판정 → 실패하면 대기 `×1.3~2.8` 재추첨.
   **`G.t` 절대시각을 쓰는 이유가 코드 주석에 명시**(decide가 0.12~0.26초에 한 번만 돌기 때문). Unity 포팅 시 그대로 유지 필요.
   > 반드시 해야 하는 것(탈출 탑승·동료 구조·보스탄 회피·파묻힘 탈출)은 게이트를 **통과시키지 않음**.
4. 부가: `idleBeat(m,dt,spin)` **16804** (딴짓 0.35~1.5초), `potshot(m,dt)` **16820** (일하다 가끔 한두 발).

### 5-3. 멤버 데이터 모델 — `spawnMember()` **17186~17218**
```js
{ id, roleId, kit,
  x,y,vx,vy,aim,face,
  hp,hpMax,iframes,down,downT,reviveT,
  drill,drillWarm,digging, gunCd,ammo,mag,reloadLeft,reloadTime,
  qCd,eCd,shieldT,dashCd,dash,
  flareCd,turretCd,nodeCd,breakerCd,breaker[],
  pers,wait{},mood,moodT,                      // HUMANIZE
  breachT,breachCd,pulseCd,grappleCd,exploreCd,
  crackTarget,crackT,crackPatience,
  watch,watchT,sweepT,sweepA,
  idleT,idleDir,strafeT,lastFoeDir,
  goal,path[],pathAge,pathKey,react,
  mineTarget,stuckT,lastX,lastY,jitter,jitterA,
  say,sayT,
  level,xp,xpNeed,xpCapped,traits[],traitIds{} }
```

### 5-4. 결정 루프 `decide(m)` — **18152~18304** (우선순위 순서)
```
0.  G.downed          → goal 'revive' (리더 구조, 무조건 최우선)
0.  INF.escape 요청됨 → goal 'escape'
1.  다운된 AI 동료 14칸 내 → 'revive'
2.  리더 leash 초과(scout 13칸 / 기타 9칸) → 'follow'
3.  전투:
      보스 16칸 내 → 'fight'(boss)
      threats(m) → 표적 선택 (22%*(1.45-focus) 확률로 2순위를 먼저 잡음)
      reach = intercept * (0.7 + aggression*0.45) * mood
      채굴 중이면 focus*0.55 확률로 먼 적 무시
4.  직업 고유:
      engineer → 'turret'(intent p.72) / 'turretFwd' / 'node'(무급전 센트리 우선 p.85)
      scout    → 'scout'(정찰 p .5*curiosity) / 'pulse'(p .45*curiosity) / 'flare'(어두운 전방 p .65)
      driller  → 'crack'(기반암 균열, 목표 24초 유지, intent p .5)
4.4 'loot'  — 확률 0.28 * greed * mood
4.5 재장전  — ammo < mag*reloadAt 이고 intent(p .8) 통과 시
5.  채굴:
      mineTarget 유지 → 'mine'
      gunner && breakerCd>2 → 'watch'(통로 경계 자리) / 'guard'
      pickMine(반경 leash-2 → leash+3 → leash+6) → 실패 시 GEO.frontier()
      전부 실패 → 'guard'
```
목표 종류: `revive / escape / follow / fight / turret / node / flare / mine / crack / pulse / scout / watch / loot / guard` (14종).

### 5-5. 실행 `act(m,goal,dt)` — **18337~18496**
- 최우선 `dodgeBossShot(m,dt)` **17935~** (예고원 밖으로만 이동, 사격은 유지).
- `fight`: `combatSupport()` → 사거리 판정 `min(FIRE_RANGE(), kit.range)` + `canSee` → 사격.
  거리 유지: `near = engage*(0.55+0.16*caution)`, `far = engage*(1.3+farJit)`. 사이면 좌우 스트레이프(0.8~3.2초마다 방향 재추첨, 35% 확률 반전). 드릴러는 1.5칸 내 `drillMelee()`.
- `mine`: `GEO.inReach()` 게이트(v7.8.1 지형 게이트 `AIGEO`, 16714~16718) → `digAt()` → 진척 없으면 `GEO.ban(c,r,14)`로 그 벽 봉인(무한루프 방지).
- `crack`: `crackPressure()` 지속, `crackPatience(9~20초)` 넘으면 포기("이건 나중에").
- 나머지는 각각 해당 스킬 함수 호출.

### 5-6. 프레임 진입점 `AI.update(dt)` — **18552~18615**
호출부: `update(dt)` 내부 **7362** (`AICREW.update(dt)`), 렌더 **8362** (`AICREW.draw()`).
```
updateInstallations(dt)   // 센트리·노드·크랙·마크
멤버 루프:
  쿨다운 일괄 감소 → updateMood → updateBreakers → updateBoarding → xpTrickle → collectLoot
  down이면 updateDown(dt) 후 continue
  recover(m,dt)  // 파묻힘 탈출 최우선. 4초 초과 시 warpToParty()
  manual이면 manualAct()  // 관전 빙의 (window.OBS_MANUAL)
  react<=0 || !goal → goal = decide(m); react = pers.react * rand(0.75,1.4)
  updateBreach → dodgeEnemyShot(80% 확률 옆대시) → 낮은 확률 진행방향 대시
  try{ act(m,goal,dt) } catch → 3초 스로틀 콘솔 에러
  potshot (fight/escape 외)
  applyMotion → watchStuck
```

### 5-7. 경로 탐색
`findPath(sc,sr,gc,gr,kit,self)` — **17231~17272**. **다익스트라 (선형탐색 힙, guard 24000)**. 4방향.
```
digCost(t,kit) 17222:  빈 칸 = 1
                       기반암 = Infinity
                       벽 = 1 + (HPT[t]*wallHpMul/100) * kit.pathDigCost
동료가 서 있는 칸 +3
```
> `pathDigCost`가 드릴러 5 / 거너 30이라 **드릴러는 벽을 뚫는 지름길, 거너는 우회**를 자연히 선택한다. 이게 이 시스템의 핵심 설계.
`ensurePath()` **17274~17288** (0.4초 캐시), `followStep()` **18308~18326** (앞이 벽이면 거너는 파쇄탄, 나머지는 드릴).

### 5-8. 이동
`steer(m,tx,ty,dt,speedMul)` **17292~17312** — 목표 방향 + 동료/플레이어 반발(`R_SHELLY*1.9` 내). 속도 `teMovePx()*speedMul*kit.moveMul`.
`applyMotion()` **17325~17338** (드래그 0.82), `tryDashAI()` **17340~17346**, `dodgeEnemyShot()` **17349~17369** (최근접시각 0~0.55초 예측).
`recover()` **17511~17530**, `watchStuck()` **17532~17548** (1초 정체 시 경로 폐기 + 지터).

### 5-9. 전투/스킬 구현
- `fire(m,tx,ty)` **17483~17505** — `p.ai=1, p.aiMul=kit.gunMul, p.aiOwner=m.id` 태그 부착. 조준 오차 `pers.aimErr * (digging?1.6:1)`.
- `drillMelee(m,dt)` **17508~17520**
- `threats(m)` **17446~17466** — **팀 정보 공유**: 내가 본 적 + 사람이 본 적 + 사람에게 붙은 적. 우선도 100/60/40 기반 + 공격중 +12, 엘리트 +8.
- `FIRE_RANGE()` **17435** = `teWorld(280)*1.2/CELL*0.92` ≈ 5.7칸
- 드릴러: `AI.cracks:Map` **16835**, `crackPressure()` **16860~16880**, `breakBedrock()` **16845**, `pickBedrock()` **16893~16918** (뚫으면 새 공간이 열리는 벽만 — `G.comp` 연결성분 사용), `startBreach()/updateBreach()` **16921~16947**
- 거너: `fireBreaker()` **17723~17732**, `detonateBreaker()` **17733~17760**, `updateBreakers()` **17762~17788** (조기 기폭 intent p `0.5+0.12*hot`), `breakerCluster()` **16950~16964**, `pickWatchPost()` **16967~16985**
- 스카웃: `throwFlare()` **16988~17001**, `scoutPulse()` **17005~17029** (+`AI.marks[]`), `pickScoutSpot()` **17043~17054**, `tryGrapple()` **17057~17074**
- 엔지니어: `pickTurretSpot()` **17077~17099** (상위 후보 중 62%/1위, 나머지 2·3위 — 일부러 비최적), `placeTurret()` **17792~17816**, `placeNode()` **17818~17834**, `updateInstallations()` **17836~17872** (급전 안 되면 센트리 침묵)

### 5-10. 성장 (사람과 분리)
`AI_TRAITS` — **17573~17611** (역할별 7~8종, tier 1~4). `maxTier(level)` = `<3:1, <6:2, <9:3, else 4` **17570**.
`awardXp/checkLevel/pickTrait/applyTrait` — **17545~17642**. `INF_XP_TABLE`/`INF_XP_WEIGHT` 공유, `XP_FLOOR_CAP=60`.
`AI.awardKill()` **17651**, `AI.creditBreak()` **17685** — **AI가 부순 블록은 장악도(floorBroken)만 올리고 사람 XP는 안 올림** (§5.2 원칙). 소유권 추적은 `AI.dmgSrc` / `AI.breakSrc` 전역 플래그(`hurtEnemy` 6329, `damage` 6081에서 분기).

### 5-11. 라이프사이클 / 렌더
`AI.add/remove/setRole` **17103~17140**, `AI.onRunStart/onRunEnd/onFloorInit` **17145~17174**.
`AI.hurt(m,dmg,nx,ny)` **17876~17891** (shieldT면 ×0.35, hp<=0이면 down), `AI.hitTest` **17893**, `AI.targets` **17901**, `updateDown()` **17909~17924** (1.6칸 내 조력자 있으면 5초 → hp 50% 부활).
`AI.draw()` **18691~** — 역할별 시트 우선, 실패 시 `drawShellyVector`. 라벨 = `AI {역할} · Lv`, 체력바, **현재 goal.label 실시간 표시**(플레이테스트용).
UI: `.aiCrewChip/.aiCrewBar/#aiCrewHud` **18825~18940**.

### 5-12. 인접 시스템
- `OBSERVER` (관전 리더 오토파일럿) — **20156(decide) / 20237(act) / 20336~20420**. 사람 캐릭터를 `crewSkillQ/E` 호출로 조종. goal 종류 `fight/escape/revive/rock/mine`.
- 핑/명령 시스템 — **21078~21530**. `AICREW.update`를 래핑해 명령 핑을 단기 goal로 주입(21523~21526).

---

## 6. 피해 / 체력 / 상태 모델

### 6-1. 플레이어
```
G.php / G.phpMax (기본 181), G.iframes (피격 시 0.59s)
applyPlayerDamage(raw,nx,ny)  6535~6562
  iframes>0 → 무효
  CREW.phase==='play' → dmg = max(4, round(dmg*0.85)); shieldT>0이면 추가 ×0.35
  infRelicPlayerDamageMod() → 유물 경감
  넉백: sh.x -= nx*DEMO.enemyKnock(70)*0.35 → collide()
  FEEL.hurt(dmg,nx,ny)  4854~4860   // 3단계 강도, 히트스톱 28/46/68ms, 화면 틴트
  php<=0 → infEndRun / crewSoftRespawn / endCrewMission('fail')
```
- 접촉 피해: `enemyContactDamage(e,nx,ny,mul)` **6563~6574**. `AI_TGT_HURT()` 훅이 먼저 AI 크루 여부를 가려냄.
- 넉백 물리: `G.knock{vx,vy}` + `KNOCK_DRAG 7.2`, 기절 `G.stunT` (**7111**에서 이동 입력 차단).
- 대시 무적 없음 (i-frame만).

### 6-2. 상호 부활 (v7.7.2c)
**6487~6534**. `REVIVE_NEED_SEC=5, REVIVE_RANGE=1.6칸, REVIVE_HP_RATIO=0.5`.
```
playerEnterDowned()  6503  → G.downed=true, 입력 차단, 조작 잠금
infDownedTick(dt)    6518  → stunT/iframes 유지
   reviveHelpersNear()>0 → reviveT += dt, 5초 도달 시 playerRevive()
   아니면 reviveT -= dt*0.5
   reviveRescuersAlive()<=0 → 즉시 런 종료 (구조자가 아무도 없음)
playerRevive()       6512  → hp 50%, iframes 2.2s
```
AI 크루 쪽 동일 로직: `updateDown()` **17909**. 부활 1회 제공 특성/유물 별도: `P.revive` (11721 등), `infRelic` 불사조.

### 6-3. 적
```
hurtEnemy(e,dmg,nx,ny,src)  6294~6334
  1. 맞으면 즉시 각성 (ai='chase', lastSeen 갱신) — 보스 제외
  2. infRelicOnHit() → 치명타/조건부/상태이상 프록
  3. 보스 장갑 생존 시 dmg *= armorDamageTaken(0.22)
  4. e.hp -= dmg; e.hurt = 0.18 (히트 플래시)
  5. r_executioner: hp/hpMax<=0.12 일반 적 즉사
  6. FEEL.enemyHit() 4853 → 히트스톱 (사망 42/58ms, 보스 12ms, 일반 20ms) + 카메라 킥
  7. 넉백 impulse = clamp(teWorld(92) * hitPower^0.72 * rangePower * resist * relicKnock,
                          teWorld(3), teWorld(290))
     hitPower = (dmg/enemyGunDmg)^0.72
     rangePower = clamp(1.42 - playerDist/(CELL*7.4), 0.12, 1.36)   // 근접 사격이 더 밈
     resist = apex ? 0.52 : 1        보스 = 넉백 완전 면역
  8. hp<=0 → gore, 킥, SFX, XP 정산(AICREW.awardKill 우선), infRelicOnKill, infBossDefeated
```
상태이상 필드: `e.stunT`(6725), `e.frozenT`+`freezeImmuneT`(6988~6990), `e.slowT/slowMul`(6998), `e.hurt`, `e.blinkT/blinkCd`. 유물 틱 `infRelicEnemyTick(e,dt)` **6969** (화상·독·감전·슬로우).

### 6-4. 타일 피해
`damage(c,r,d,hx,hy,quiet)` — **6068~6162**. `G.hp:Map`에 잔여 체력 저장(없으면 `HPT[t]*INF.wallHpMul`). 0 이하면 파괴 → `infOnBlockBroken()`(장악도·XP·특성 프록) + 대량 VFX + 전리품(`YIELD`) + 묻힌 유물(`G.relic`) + 보급품 확률(광물 16% / 일반 4%).

---

## 7. 특성 / 노드 / 퍽 시스템

**데이터는 전부 HTML 인라인.** 루트의 `traits.json`(56개)은 제작용 사본이며 런타임은 읽지 않는다. 아이콘 경로만 외부(`assets/menu/trait-resources/icons/`).

### 7-1. 런 특성 카드 (레벨업 시 3장 중 1택)
`INF_TRAITS` — **12025~12105** (약 60종).
스키마:
```js
{ role: 'driller'|'gunner'|'scout'|'engineer'|'all',
  req:  'drill'?,          // 드릴 보유 직업만
  lock: 'deep'|'bedrock'|'shrapnel'|'barrage'|'recon'|'pathfind'|'grid'|'fortress'?,  // 영구노드 해금
  kind: '고압 드릴' 등 계열명,
  id, tier: 1..4, n: 이름, d: 설명,
  ok:   () => boolean,     // 등장 조건 (이미 최대치면 false)
  a:    () => void }       // 효과 — INF 전역을 직접 변형 (숨은 결합!)
```
- 롤 필터 `infTraitRoleOk(t)` **12109~12114**, 거너 대체 문구 `INF_TRAIT_ROLE_TEXT` **12116~12128**.
- 뽑기: `infPickTraits()` **12221~12225** — `infRollTier()` **12214~12220**로 티어 가중치 `{1: max(24,62-34p), 2: 28, 3: 8+16p, 4: 2+8p}` (p = `level/INF_CARDS.targetLevel(10)`). 피티 `INF.pity>=4` 또는 `level%5===0`이면 티어 3+ 강제.
- 적용 `infApplyTraitChoice(trait)` **12855**, UI `infOpenLevel()` **~12895**.
- 레벨 곡선 `infXpNeedFor(L) = round(30 + 10L + 2.4L²)` — **12942**, `INF_CARDS` **12937~**.
- **전설 카드** `INF_LEGENDS` — **12158~12165** (6종, 보스 격파 보상 3장 중 1택, 13533).
- 상태 초기화 `infResetBuild()` **12234~12235** (특성이 건드리는 모든 INF 필드 목록이 여기 다 있음 — Unity 스탯 구조체 설계 시 이 줄이 최고의 참고자료).

### 7-2. 영구 노드 (계정 단위 메타 진행 — 80노드 / 140랭크 / 코어 760)
- 골격 `INF_NODE_SLOTS` — **11632~11641**:
  ```
  i   기초강화 maxRank 3 cost[1,2,3]           reveal m1,m2
  m1  전문화   maxRank 2 cost[3,5]  req i      reveal m3   ties m2
  m2  전문화   maxRank 2 cost[3,5]  req i      reveal m4
  m3  전문화   maxRank 2 cost[4,7]  req m1     reveal m5   ties m4
  m4  전문화   maxRank 2 cost[4,7]  req m2     reveal m6
  m5  행동변형 maxRank 1 cost[9]    req m3     reveal cap  ties m6
  m6  행동변형 maxRank 1 cost[9]    req m4     reveal cap
  cap 캡스톤   maxRank 1 cost[14]   req m5+m6 (requiresCount 2)
  ```
- 군집 `INF_NODE_CLUSTERS` — **11652~11663** (10개: crew_haul, crew_cmd + 4직업 × {gear, surv}).
- 배치 `INF_NODE_GRID` **11629**, `infNodeRadialOffsets()` **11643**, `INF_NODE_OFFSETS` **11644**, 역할 허브 **11646~11651**.
- 내용 `INF_NODE_DEFS` — **11679~11790**. 헬퍼 `N(name, icon, effectByRank[], apply(P,rank), extra)` **11666**.
  `apply`는 **INF가 아니라 누적 객체 `P`를 변형** → 합산 후 캡 적용 (`INF_PERM_CAPS` **11669~11677**: dmgDrill .35, dmgGun .35, move .12, xp .30, hp 72, mag 8, shield .45, foundation .45, breakerRadius 2, vision 2.5, turrets 2, nodes 2, reroll 3, startCards 2, revive 1 등).
- 조립 `INF_PERMANENT_NODES` (IIFE) — **11792~11811**. 조회 `INF_NODE_BY_ID` **13725**, `infRunNodes(roleId)` **13730** (crew + 자기 직업만 = 한 런 32노드/56랭크).
- 클램프 `infPermClamp(P)` **13819**, 커밋 `infPermCommit`, 카드 해금 `infPermCardUnlocked(lock)` / `infPermAddKey(P,key)`.
- UI: `infPaintSettlement()` **14028~14104** (SVG 스킬트리 렌더).

### 7-3. 예약/폐기 데이터 (Unity 이식 시 무시 가능)
`INF_DRILLER_TREE` **12135~12157**, `INF_BRANCHES` **12167~12188** — 런 중 구매 트리는 기획서 §6.5로 폐기됨. 단 `infApplyTraitEffect(effectId)` **12192~**의 `driller_*` 플래그들은 **실제 런 특성이 참조**하므로 살아있음.

### 7-4. 유물 (Relic) — 특성과 별개의 3번째 축
`INF_RELIC_ELEM` **14220~14225** (fire/frost/volt/earth), `INF_RELIC_TIER` **14226**, `INF_RELICS` **14227~** (일반/희귀/전설).
훅 지점(전부 `typeof ... === 'function'` 가드로 느슨 결합):
`infRelicOnHit` 6298, `infRelicHas` 6302/6307, `infRelicKnockMul` 6314, `infRelicOnKill` 6331, `infRelicPlayerDamageMod` 6544, `infRelicAfterPlayerHurt` 6547, `infRelicEnemyTick` 6969, `infRelicWallSlam` 7001, `infRelicTimeStopActive` 6598/6950, `infRelicOnBlock/OnRare/OnDescend/BossDrop/BuriedFind`.

---

## 8. 현장 퀵크래프트 (원형 제작 휠)

블록: **21986~22200**. 전역 `window.TC_CRAFT`. 입력 = **C 홀드**.

### 8-1. 레시피 `RECIPES` — **22041~22048**
| id | 이름 | kind | pulp | bloom | 제한 | 효과 | range |
|---|---|---|---|---|---|---|---|
| `shaped-charge` | 성형 폭약 | place | 7 | 2 | max 2 | 2초 후 폭발. 벽 피해 `HPT*1.8`(기반암 0.2), 적 `enemyGunDmg*2.2`, 반경 1.6칸 | 2.2칸 |
| `auto-turret` | 자동 포탑 | place | 8 | 2 | max 1 | 45초, 사거리 190px, `support` 탄, 쿨 0.48s, power 0.62 | 2.8칸 |
| `coolant-capsule` | 냉각 캡슐 | instant | 4 | 0 | cd 12s | `G.drillHeat *= 0.25`, `drillHeatLock = 0` | — |
| `folding-barricade` | 접이식 방벽 | place | 6 | 0 | max 2 | 35초, HP `phpMax*1.6`, 적 밀어냄 + 적 탄 소멸. 2×1 풋프린트, 90° 스냅 | 2.3칸 |
| `med-injector` | 응급 주사 | channel | 5 | 1 | cd 18s | 0.7초 채널링 → `phpMax*0.35` 회복. **피격 시 취소 + 재료 환불** | — |
| `flare-bundle` | 휴대 조명탄 | place | 3 | 0 | max 2 | 30초 `G.lamps` 램프(visionRange 3) | 2.6칸 |

자원: `G.gPulp` / `G.gBloom` (`currencies()` **22062**). 소모 `spend()` **22076**, 환불 `refund()` **22077**.

### 8-2. 흐름
```
open() 22107      phase 'closed' → 'wheel'. clearAttack()으로 드릴/사격 강제 해제
selectFromPointer 22102   휠 중심 기준 각도 → 6분할(60° 간격, -90° 시작) 스냅
confirmSelection 22134    kind==='place' → beginPlacement()
                          instant/channel → 즉시 실행
beginPlacement 22110      phase 'placing', P.zoomLock = G.Z  (카메라 줌 고정)
                          ※ 렌더 루프 7366~7369 가 craftZoomLock 을 읽어 G.Z 고정 — 숨은 결합
updatePlacement 22111~22126
   화면좌표 → screenToWorld → toCell → 타일 스냅
   유효성: 거리 0.55칸 ~ r.range 이내, 풋프린트 전 칸이 !solid, 기존 설치물 겹침 없음
confirmPlacement 22127~22133   해당 배열에 push
updateObjects(dt) 22142~22151  쿨다운/채널링/포탑/방벽/조명탄/폭약 전부 여기서 갱신
drawObjects()     22155~22161  아이콘 + 잔여수명 원호 게이지
reset()           22162        런 시작 시 초기화 (G.lamps에서 craft 램프 제거)
```
`reason(r)` **22066~22074**가 제작 가능 여부의 단일 관문(재료/쿨/설치한도/상황 조건).

### 8-3. 훅 (런타임 래핑)
**22166~22167**: `updateCrew` → `update(dt)` 추가, `drawCrewExtras` → `drawObjects()` 추가.

---

## 9. Unity 포팅 시 주의할 "숨은 전역 결합" 목록

| 결합 | 위치 | 내용 |
|---|---|---|
| `CELL` → `teWorld()` | 2034, 2079 | 이동/투사체/적 속도/사거리 전부가 CELL에 종속 |
| `INF` 평면 객체 | 11824~11828 | 4개 역할의 모든 파라미터 + 60개 특성 효과가 한 객체에 섞임. **`infResetBuild()` 12234가 전체 필드 목록** |
| `AICREW.dmgSrc` / `AICREW.breakSrc` | 6081, 6329, 17515 | 데미지 소유권을 전역 플래그 + try/finally로 전달 (스레드 불가 패턴) |
| `AI_TGT()` / `AI_TGT_HURT()` | 18621, 18642 | 적 AI 전체가 이 전역 함수로 타겟을 받음. AI 크루 없으면 `G.sh` 반환 |
| `CREW.phase` | 전역 | `play / bossIntro / bossDeath / infiniteRest / infiniteLevel / infiniteResult / menu`. **시네마틱이 이 값을 바꿔 `update()`를 멈춤** (월드 정지, 렌더만) |
| `bossTune(key)` | 2130 | `BOSS_LAB.values` → `BOSS_TUNE_FALLBACK` 폴백. 인스펙터가 없으면 하드코딩 값 |
| `TC_CRAFT.zoomLock` | 7366~7369 | 렌더 루프가 크래프트 상태를 직접 읽음 |
| 런타임 함수 래핑 | 16038, 16161, 16437, 21523, 22166 | `infSpawnBoss`, `infBossDefeated`, `AICREW.update`, `updateCrew`, `paintUI`가 여러 블록에서 중첩 래핑됨. **원본 함수만 읽으면 실제 동작을 놓침** |
| `J` (주스/파티클 시스템) | 전역 | `J.kick/ring/burst/text/chunks/spikes/flash/smoke/stop/after/sm/ceilingArea`. 전투 로직과 VFX가 완전히 섞여 있음 → Unity에서는 이벤트로 분리 권장 |
| `FEEL` | 4840~4864 | 히트스톱·화면 흔들림·틴트. `FEEL.enemyHit`가 `src!=='weapon'`이면 조기 리턴하는 점 주의 |
| `LOS.markDirty()` | 전역 | 타일/램프가 바뀔 때마다 호출 필요. 빠뜨리면 시야가 갱신 안 됨 |
| `G.comp` / `compDirty` | 5148 | 연결성분. AI 드릴러의 `pickBedrock`이 "뚫으면 새 공간이 열리는지" 판정에 사용 |
