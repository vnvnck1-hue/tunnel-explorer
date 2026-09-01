# Kenney 사운드 후보 — 땅굴크루 v7.8.0

Kenney Game Assets All-in-1 3.7.0 / Audio 에서 현재 게임 사운드 이벤트에 대응될 만한
파일만 골라 복제했다. 전부 CC0 (`_license/` 에 팩별 License.txt 동봉).

- 총 178개 파일 / 6.2MB / 전부 `.ogg`
- 청음: `preview.html` 을 브라우저로 열면 폴더별로 전부 재생 가능

## 현재 오디오 구조 참고

v7.8.0 의 사운드는 `const SFX={...}` (본편 HTML 3319행~) 에서 **전부 WebAudio 절차 합성**이다.
예외는 드릴 루프 하나로, `DRILL_SMP` 에 WAV를 base64로 임베드해 두고
샘플 사용 불가 시 `drillHumProc()` 절차 합성으로 폴백한다.
→ 즉 샘플 교체는 이미 검증된 패턴이 있다. `drillHum` 과 동일하게
"샘플 우선 + 절차 폴백" 으로 이벤트를 하나씩 갈아끼우면 된다.

## 폴더 ↔ 게임 이벤트 매핑

| 폴더 | 대응 SFX | 추천 |
|---|---|---|
| `01-dig-drill` | `SFX.dig`, `SFX.brk` | **impactMining_000~004** 가 곡괭이/드릴 채굴음 그 자체. dig 랜덤 5종으로 바로 쓸 수 있다. brk 는 stonesHit / impactPlate_heavy |
| `02-drill-engine` | `SFX.drillHum`, `drillHeat`, `drillOverload` | engineCircular_00x 가 루프성 회전 엔진. `playbackRate` 로 heat 피치업 그대로 대응. 과부하는 phaserDown1 + lowFrequency_explosion_000 |
| `03-gun` | `SFX.tick`, `reload`, `reloadDone` | laserSmall_00x(짧음, 연사 적합) / laserRetro. 장전은 metalClick → metalLatch 2단 |
| `04-monster-combat` | `SFX.growl`, 피격 | creature1/3/5, slime_00x. rumble 은 보스 등장용 |
| `05-ore-resource` | `SFX.ore`, `SFX.res` | ore = secret1/3 또는 jingles-hit_03(임팩트 큼). res = coin/pluck (연타되므로 짧은 pluck 권장) |
| `06-cache-jackpot` | `SFX.cache`, 잭팟/피버 연출 | cards-pack-open-1 = 유물 개봉, chips-* = 잭팟 누적, dice-shake/throw = 도박 연출에 딱 맞음 |
| `07-ui` | `SFX.ui`, `back`, `pick`, `deploy` | click_00x / back_00x / confirmation. 현재 절차 UI음이 일부러 낮고 둔탁하니 밝은 click 계열은 톤 확인 필요 |
| `08-warn-fail` | `SFX.warn`, `timeout`, `fail` | error_00x = 경고, radar1/3 = 카운트다운 경보 루프, gameover/lose = 실패 |
| `09-start-dawn` | `SFX.ready`, `start`, `dawn` | powerUp/threeTone = 준비. dawn(생환 성공)은 jingles-hit_07/12, jingles-retro_04 |
| `10-exit-descend` | `SFX.exit`, `descend`, `dash` | doorOpen/Close, fall2/4 = 하강, woosh = 대시 |
| `11-shop-buy` | `SFX.buy` | handleCoins / chips-handle-1 |
| `12-crew-footstep` | (신규 제안) | 크루 이동 발소리. 인원수 많으면 스로틀 필수 |
| `13-ambient` | (신규 제안) | 물방울 drip, creak, computerNoise. 저빈도 랜덤 원샷으로 깔면 동굴감이 크게 오른다 |
| `14-bgm-loops` | (신규 제안) | **Infinite Descent** 가 무한모드 컨셉과 이름·분위기 모두 맞음. Sad Descent/Flowing Rocks 는 심층, Game Over 는 실패 화면 |

---

## 적용 현황 (v7.8.0)

아래 6개 카테고리가 본편에 적용됐다.

- 샘플 선택/게인: `tools/gen_kenney_smp_block.py` 의 BANK 표를 고친다
- 본편 반영: `python claude-work/audio/apply-kenney-sfx.py` (몇 번 돌려도 결과 동일)

다른 세션이 같은 HTML을 덮어써서 블록이 사라지면 apply 스크립트를 그냥 다시 돌리면 된다.

구조는 기존 `DRILL_SMP` 와 같은 **샘플 우선 · 절차 합성 폴백**이다.
`const SMP={...}` 이 base64 내장 ogg 를 디코드하고, 각 `SFX.*` 메서드를 래핑해
샘플이 있으면 샘플을, 없으면 원래 절차 합성을 그대로 부른다.

| 이벤트 | 샘플 | 게인 |
|---|---|---|
| `SFX.dig` | impactMining_000~004 (5종 랜덤) | .075 |
| `SFX.brk` | stonesHit1/2 + impactPlate_heavy_000 (+기존 저역 sine 보강) | .28 |
| `SFX.shot` (사격 전용, 신규) | laserSmall_000/002/004 | .11 |
| `SFX.shard` (룬조각·보급품 드롭, 신규) | pluck_002 + 낮은 sine 배음 | .07 |
| `SFX.reload` | metalClick (수동 재장전은 2단) | .14 |
| `SFX.reloadDone` | metalLatch | .09 |
| `SFX.growl` | creature1/3/5, vol 로 게인·피치 변조 + lp 1400 | .15 |
| `SFX.oreBreak` (분홍 광석 벽돌 파쇄, 신규) | impactGlass_light_000/002/004 + 저역 sine | .16 |
| `SFX.kill` (적 처치, 신규) | impactSoft_medium ×4 + slime_000 꼬리 + 저역 sine | .11/.055 |
| `SFX.res` | pluck_001/002 | .06 |
| `SFX.ui` / `back` / `pick` / `deploy` | click_003 / back_001 / select_002 / confirmation_001 | .09/.075/.055/.055 |
| 로비 메뉴 호버 (신규) | click_003 | .05 |
| 로비 메뉴 클릭 (신규) | click_001 | .09 |
| `SFX.cardFlip` (특성 카드 등장, 신규) | bookFlip2 — 카드 한 장마다 240ms 간격 | .16 |
| `SFX.cardPick` (특성 카드 선택, 신규) | maximize_003 | .07 |
| `SFX.step` (플레이어 발소리, 신규) | footstep_concrete_000~003 + 18% 확률로 cloth | .06 / .033 |
| `SFX.stepCrew` (AI 크루 발소리, 신규) | footstep00/03/07 | .032 |
| `SFX.dash` (대시·그래플, 신규) | Helmet pickup1/setDown1 + cloth | .13 |
| `SFX.drillOverload` | phaserDown1 + lowFrequency_explosion_000 (60ms 딜레이 2겹) | .20/.24 |

전부 `AU.catMul(cat)` 을 타므로 설정창 볼륨 슬라이더가 그대로 먹는다.
표의 게인은 브라우저에서 마스터 피크를 재서 기존 절차 합성 대비 ±0.07 이내로 맞춘 값이다.

### 효과음 전체 -50% (`AU.sfxMix = 1.5`)

블록 안에서 `AU.sfxMix` 를 3.0 → **1.5** 로 낮춘다. `catMul()` 을 타는 모든 소리
(절차 합성 · Kenney 샘플 · 드릴 루프)에 한 번에 걸린다.
설정창 효과음 슬라이더가 쓰는 `AU.vol.sfx` 는 건드리지 않으므로 슬라이더 동작은 그대로다.
되돌리려면 런타임에 `AU.sfxMix=3` 또는 생성 블록의 값을 고친다.

### `secret3` 사용 금지

`05-ore-resource/secret3.ogg` 는 파일까지 삭제했다. 이 소리를 쓰던
`SFX.ore`(유물 발견 · 빙결 · 레벨업 카드)는 **기존 절차 합성으로 되돌아갔다.**
샘플로 다시 채우려면 `impactBell_heavy_001` / `jingles-hit_03` / `glass_006` 정도가 후보다.

### 발소리 — 시간이 아니라 이동 거리로 보폭을 만든다

프레임레이트나 속도 버프에 흔들리지 않게, 누적 이동 거리가 `STEPS.stride`(60px)를 넘을 때마다
한 걸음 울린다. 기본 이동속도에서 **초당 2.7보**로 실측했다. 대시 중에는 울리지 않는다.

- 플레이어: `playerMoveDustTick(dt,vx,vy)` 를 감싸서 거리를 누적
- AI 크루: `crew-ai` 쪽은 건드리지 않고 공용 `spawnMoveDust()` 만 감싼다.
  인원수만큼 발소리가 쏟아지면 시끄러우니 215~345ms 전체 스로틀로 묶어
  "무리의 발소리"로 들리게 했다. 플레이어 본인의 먼지 호출은 거리로 걸러낸다.

`SFX.dash` 는 원래 **정의가 없어서 무음**이었다(스카우트 그래플에서 호출만 하고 있었다).
헬멧/천 소리로 채웠다.

### 로비 메뉴 호버/클릭

메인 로비 메뉴(`#crewMenu .modeBtn`, `.menuTools button`)만 별도 처리한다.

- 호버 → `click_003` (45ms 스로틀, 버튼이 바뀔 때만)
- 클릭 → `click_001`

메뉴 버튼의 기존 핸들러가 `SFX.ui()` 를 부르기 때문에, 캡처 단계에서 먼저 "예약"해 두고
뒤이어 오는 `SFX.ui()` 가 그 예약을 소비해 `click_001` 로 바뀐다 — 소리가 두 번 나지 않는다.
`SFX.ui()` 를 부르지 않는 버튼은 90ms 뒤 타이머가 대신 울린다.
로비 메뉴 밖의 `SFX.ui()` 는 예전대로 `click_003` 이다 (예약은 최대 90ms 만 살아 있다).

### 한 사운드를 여러 이벤트가 공유하던 것을 분리했다

원래 코드에서 서로 다른 사건이 같은 SFX 를 부르고 있었다. 샘플을 얹으면 이 공유가
바로 티가 나서(총소리가 메뉴에서 나거나, 적이 죽을 때 광맥 발견음이 나거나) 분리했다.

| 원래 | 분리 후 |
|---|---|
| `SFX.tick` = 총격 + 룬조각 드롭 + 카드 클릭 | `SFX.shot`(총격) / `SFX.shard`(드롭) / `tick`(나머지, 절차 합성 유지) |
| `SFX.ore` = 분홍 광석 파쇄 + 적 처치 + 유물 발견 + 빙결 | `SFX.oreBreak`(파쇄) / `SFX.kill`(처치) / `ore`(나머지) |

새 메서드는 전부 `SFX.xxx?SFX.xxx():<기존 호출>` 형태로 호출하므로,
블록이 없으면 원래 소리로 그대로 돌아간다.

### `SFX.tick` 은 건드리지 않는다 (중요)

`SFX.tick` 은 총격 전용이 아니라 **룬조각/보급품 드롭 · 특성 카드 클릭 · 역할 선택**
등에도 쓰이는 범용 블립이다. 여기에 레이저 샘플을 얹으면 벽돌 부술 때와 메뉴 클릭에서
총소리가 난다. 그래서 tick 은 원래 절차 합성 그대로 두고 두 이벤트를 새로 팠다:

- `SFX.shot()` — 총격/파쇄탄 발사 전용. 폴백은 기존 tick 톤.
- `SFX.shard()` — 룬조각·보급품 드롭. 조용한 플럭 + 낮은 sine 배음.
  벽 파괴음(`brk`) 위에 얹히므로 앞에 나서지 않는 레벨로 잡았다.

호출부 3곳을 `SFX.shot?SFX.shot():...` / `SFX.shard?SFX.shard():...` 형태로 바꿨다
(총격, 파쇄탄 발사, 룬조각 드롭). 블록이 없어도 기존 tick 으로 동작한다.

### 드릴 엔진 루프는 기본값 그대로 (옵트인)

`drillHum` 은 **기존 폴리싱 3-파트 DRILL_SMP 를 유지**했다. 그쪽은 start/loop/release
3파트에 열 피치·떨림까지 전용으로 튜닝된 자산이라, 범용 `engineCircular` 로 덮으면
품질이 떨어진다. 대신 언제든 비교할 수 있게 옵트인으로 붙여뒀다:

```js
DEMO.drillKenneyLoop = true;   // Kenney engineCircular_002 루프로 전환
DEMO.drillKenneyGain = 0.30;   // 게인
DEMO.drillKenneyPitch = 0.55;  // 열에 따른 회전수 상승폭
```

이 루프만 base64 내장이 아니라 이 폴더의 ogg 를 `fetch` 한다(HTML 용량 0).
그래서 **로컬 파일 직접 열기(file://)로는 동작하지 않고** 서버로 띄워야 한다.
로드 실패 시 자동으로 기존 드릴로 되돌아간다.

### 되돌리기 / 부분 끄기

```js
DEMO.kenneySfx = false;                    // 전체 OFF → 전부 기존 절차 합성
DEMO.kenneySfxOff = ['tick','ui'];         // 특정 이벤트만 OFF
DEMO.kenneySfxGain = { dig: 0.05 };        // 이벤트별 게인 조정
```

### 용량

HTML 이 16.34MB → 16.72MB (+약 370KB). 채택한 25개 파일만 내장했고,
나머지 후보는 이 폴더에 파일로만 남아 있다.

## 남은 후보 우선순위 (미적용)

1. `cards-pack-open-1` + `chips-stack-2` → 유물 캐시/잭팟 연출 (`SFX.cache`)
2. `Infinite Descent.ogg` → BGM (현재 BGM 슬롯 존재 여부 확인 필요)
3. `drip1/2/4` → 동굴 앰비언트 (저빈도 랜덤 원샷)
4. `error_00x` / `radar1` / `gameover1` → 경고·타임아웃·실패 (`SFX.warn/timeout/fail`)
5. `jingles-hit_07` → 생환 성공 (`SFX.dawn`), `handleCoins` → 상점 (`SFX.buy`)

## 주의

- 게임은 단일 HTML 배포이므로, 추가 채택 시에도 base64 임베드가 필요하다.
  ogg 6.2MB 전체는 과하니 **채택분만** 임베드하고 나머지는 이 폴더에 후보로 남긴다.
- 현재 절차 합성 사운드는 의도적으로 저역·둔탁하게 튜닝되어 있다
  (주석: "밝은 스퀘어 제거", "밝은 메이저 화음 대신 낮은 불협화").
  Kenney 의 밝은 UI/레트로 계열을 그대로 넣으면 톤이 깨질 수 있으니
  lowpass 를 물리거나 어두운 쪽 파일을 고르는 편이 안전하다.
- 볼륨 카테고리(`cat:'ui'|'dig'|'brk'`)가 있으므로 샘플 재생 경로도 동일 게인 노드를 타야 설정 슬라이더가 먹는다.
