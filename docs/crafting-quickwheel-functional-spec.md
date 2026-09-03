# 현장 퀵크래프트 기능명세서

기준 게임: `tunnel-crew-infinite-mode-v7.9.0.html`  
선택 콘셉트: 원형 현장 퀵크래프트  
문서 상태: 구현 전 기능·자산 명세

## 1. 목표

채굴한 PULP/BLOOM을 런 도중 즉시 소비하여 생존·탐색·전투·굴착 수단으로 바꾼다. 메뉴를 열어도 월드는 멈추지 않으며, 플레이 화면과 플레이어 주변을 가능한 한 많이 유지한다.

핵심 원칙:

- 현재 런 자원 `G.gPulp`, `G.gBloom`만 소비한다.
- 채취 목표는 누적 채취량 `G.nRes`를 사용하므로 제작 후에도 감소하지 않는다.
- 제작품은 직업 고유 장비보다 약하게 만들어 역할 정체성을 보호한다.
- 텍스트와 수치는 DOM으로 렌더링하고, 이미지에는 굽지 않는다.
- 레시피/수치는 데이터 테이블로 분리하여 UI와 효과 코드가 같은 정의를 사용한다.

## 2. 최종 리소스

### 2.1 제작물 아이콘

모든 아이콘은 256×256 RGBA, 중앙 정렬, 투명 배경이다. 휠에서는 92×92px, 상세 패널에서는 68×68px, 월드 배치 미리보기에서는 40~56px로 축소한다.

| 기능 ID | 최종 PNG | 용도 |
|---|---|---|
| `auto-turret` | `assets/ui/crafting/icons/icon-auto-turret.png` | 휠·상세·설치 미리보기·월드 포탑 |
| `coolant-capsule` | `assets/ui/crafting/icons/icon-coolant-capsule.png` | 휠·상세·사용 피드백 |
| `med-injector` | `assets/ui/crafting/icons/icon-med-injector.png` | 휠·상세·사용 피드백 |
| `flare-bundle` | `assets/ui/crafting/icons/icon-flare-bundle.png` | 휠·상세·월드 조명탄 표식 |
| `folding-barricade` | `assets/ui/crafting/icons/icon-folding-barricade.png` | 휠·상세·설치 미리보기·월드 방벽 |
| `shaped-charge` | `assets/ui/crafting/icons/icon-shaped-charge.png` | 휠·상세·설치 미리보기·월드 폭약 |

동일 경로의 `.webp`는 무손실 경량본이다. 본선 HTML은 PNG를 기준으로 참조한다. 단일 HTML 빌더가 PNG와 WebP 중 작은 쪽을 자동 선택하므로 별도 분기 코드는 넣지 않는다.

### 2.2 퀵크래프트 UI 파츠

| 리소스 | 규격 | 적용 방식 |
|---|---:|---|
| `assets/ui/crafting/ui/ui-wheel-slot.png` | 256×256 | 각 레시피 슬롯의 기본 배경 `<img>` |
| `assets/ui/crafting/ui/ui-wheel-selection.png` | 256×256 | 선택 슬롯 위에 겹치는 투명 앰버 오버레이 |
| `assets/ui/crafting/ui/ui-wheel-connector.png` | 640×640 | 슬롯 뒤, 플레이어 앞에 배치하는 6방향 연결 링 |
| `assets/ui/crafting/ui/ui-detail-panel.png` | 384×512 | 우측 상세 카드의 9-slice 판재 |
| `assets/ui/crafting/ui/ui-craft-button.png` | 512×176 | 제작 버튼의 9-slice 판재 |
| `assets/ui/crafting/ui/ui-craft-action-glyph.png` | 128×128 | 제작 버튼 왼쪽 망치·렌치 아이콘 |

PNG/WebP 쌍과 치수는 `assets/ui/crafting/crafting-assets-manifest.json`에 기록되어 있다. 전체 확인용 시트는 `assets/ui/crafting/crafting-assets-preview.png`다.

### 2.3 기존 자산 재사용

다음 자산은 이미 같은 UI 언어로 완성되어 있으므로 중복 제작하지 않는다.

| 용도 | 기존 자산 |
|---|---|
| PULP 비용 | `assets/ui/currency/currency-pulp.png` |
| BLOOM 비용 | `assets/ui/currency/currency-bloom.png` |
| 성공 체크 | CSS 원형 배지 + 문자 `✓` |
| 단축키 캡 | 기존 `kbd` 스타일과 동일한 CSS |
| 부족/잠금 표현 | 별도 이미지 없이 opacity, grayscale, 색상 변수로 표현 |
| 설치 가능/불가 원 | 기존 캔버스 `J.ring` 및 반투명 도형 |
| 제작·설치 VFX | 기존 `J.burst`, `J.flash`, `J.ring`, `J.text` 재사용 |

## 3. 화면 배치

### 3.1 데스크톱 기준

기준 좌표는 뷰포트 비율이며, HUD 레이아웃 편집값과 무관하게 화면 중앙 플레이어를 기준으로 한다.

- 오버레이 루트: `position:fixed; inset:0; z-index:58`.
- 어두운 배경막: 전체 화면 `rgba(5,3,10,.26)`. HUD는 가리지 않고 월드만 살짝 낮춘다.
- 휠 중심: `left:50%; top:42%`.
- 휠 크기: `--craft-wheel-size:clamp(390px,56vh,560px)`.
- 연결 링: 휠 중심에 `width/height:100%`, `z-index:0`.
- 슬롯: `clamp(108px,14vh,132px)`, 중심 반경은 휠 크기의 34%.
- 6개 슬롯 각도: 위부터 시계방향으로 `-90°, -30°, 30°, 90°, 150°, 210°`.
- 플레이어 안전 공간: 휠 중앙 지름 150px에는 불투명 UI를 배치하지 않는다.
- 상세 패널: 휠 오른쪽 24px, 크기 280×374px, 화면 우측 HUD와 최소 18px 간격.
- 상세 패널이 미니맵과 겹치면 휠과 패널 전체를 왼쪽으로 최대 8vw 이동한다.

권장 슬롯 순서:

1. 위: 성형 폭약
2. 오른쪽 위: 자동 포탑
3. 오른쪽 아래: 냉각 캡슐
4. 아래: 접이식 방벽
5. 왼쪽 아래: 응급 주사
6. 왼쪽 위: 휴대 조명탄

이 순서는 공격→지원→생존→탐색 흐름이며 선택 콘셉트 이미지와 같다.

### 3.2 반응형

- 너비 1180px 미만: 상세 패널을 240×332px로 줄이고 휠을 `min(50vh,440px)`로 제한한다.
- 너비 900px 미만 또는 세로 620px 미만: 상세 패널을 숨기고 선택 슬롯 아래에 이름·비용·가능 여부만 표시하는 1줄 요약 바를 사용한다.
- 터치 입력은 1차 구현 범위 밖이다. 터치 기기에서는 퀵크래프트 버튼을 HUD 우하단 스킬 모듈 왼쪽에 노출하는 후속 대응이 필요하다.
- HUD 레이아웃 편집 모드 `F8`이 열려 있으면 퀵크래프트 입력을 무시한다.

## 4. DOM 구조

`#tcHudPolish` 다음에 아래 구조를 추가한다. 슬롯과 텍스트는 JS에서 레시피 테이블을 기준으로 생성한다.

```html
<div id="tcCraftingRoot" aria-hidden="true">
  <div class="tcCraftDim"></div>
  <div class="tcCraftWheel" role="menu" aria-label="빠른 제작">
    <img class="tcCraftConnector" src="assets/ui/crafting/ui/ui-wheel-connector.png" alt="">
    <div class="tcCraftSlots"></div>
  </div>
  <section class="tcCraftDetail" aria-live="polite"></section>
</div>
```

슬롯 한 개의 내부 구조:

```html
<button class="tcCraftSlot" role="menuitem" data-recipe="auto-turret">
  <img class="tcCraftSlotPlate" src="assets/ui/crafting/ui/ui-wheel-slot.png" alt="">
  <img class="tcCraftItemIcon" src="assets/ui/crafting/icons/icon-auto-turret.png" alt="자동 포탑">
  <img class="tcCraftSelected" src="assets/ui/crafting/ui/ui-wheel-selection.png" alt="">
  <span class="tcCraftCostMini"></span>
</button>
```

상세 패널과 버튼은 `border-image`를 사용한다.

```css
.tcCraftDetail {
  border: 28px solid transparent;
  border-image: url("assets/ui/crafting/ui/ui-detail-panel.png") 64 fill / 28px stretch;
}
.tcCraftConfirm {
  border: 14px solid transparent;
  border-image: url("assets/ui/crafting/ui/ui-craft-button.png") 42 fill / 14px stretch;
}
```

## 5. 입력 명세

### 5.1 기본 키

최종 기본 키는 콘셉트 이미지의 `TAB`이 아니라 `C`로 한다.

이유:

- 무한 모드에서 `TAB`은 키 가이드 홀드에 사용 중이다.
- 관전 모드에서 `TAB`은 시점 전환·직접 조종 복귀에 사용 중이다.
- 같은 키를 유지하면 모드별 우선순위가 불명확해지고 기존 기능이 깨진다.

입력 흐름:

- `C keydown`: 퀵크래프트 열기. 키 반복 이벤트는 무시한다.
- 마우스 이동: 휠 중심에서 마우스 방향을 계산해 가장 가까운 60° 슬롯 선택.
- 데드존: 중심 반경 54px 안에서는 기존 선택을 유지한다.
- 좌클릭 또는 `Space`: 선택 레시피 제작/배치 단계 진입.
- 숫자 `1`~`6`: 해당 슬롯을 직접 선택하고 한 번 더 누르면 제작한다.
- C를 0.18초 이상 홀드한 경우 `C keyup`에 닫힌다.
- C를 짧게 탭하면 휠이 열린 상태로 유지되며, C를 한 번 더 누르거나 `Escape`로 닫는다. 키를 계속 누르기 어려운 사용자와 키보드 테스트를 위한 동등 입력이다.
- 설치 단계에서 좌클릭: 설치 확정, 우클릭/Escape: 설치 취소.

퀵크래프트가 열릴 때 `G.mouse.down`, `G.mouse.drillDown`, `G.mouse.gunDown`을 false로 초기화하고, 캡처 단계에서 클릭 이벤트를 소비하여 뒤의 드릴/사격이 발동하지 않게 한다. WASD 이동과 월드 업데이트는 계속 허용한다.

## 6. 상태 모델

```js
const CRAFTING = {
  phase: 'closed',       // closed | wheel | placing
  selected: 'auto-turret',
  hoverAngle: 0,
  placement: null,
  cooldowns: new Map(),
  active: { turret: null, barricades: [], flares: [], charges: [] }
};
```

상태 전이:

- `closed → wheel`: 플레이 중 C 입력, 설정/모달/다운 상태가 아님.
- `wheel → closed`: C 해제, Escape, 피격으로 다운, 미션 종료.
- `wheel → placing`: 설치형 레시피 제작 확인.
- `wheel → closed`: 즉시 사용형 레시피 성공.
- `placing → closed`: 설치 성공 또는 취소.
- 어떤 상태든 미션 결과·휴식·레벨업 카드·성장 지도·채팅이 열리면 강제 종료한다.

## 7. 레시피 데이터

```js
const CRAFT_RECIPES = [
  {id:'shaped-charge', name:'성형 폭약', kind:'place', pulp:7, bloom:2, maxActive:2},
  {id:'auto-turret', name:'자동 포탑', kind:'place', pulp:8, bloom:2, maxActive:1},
  {id:'coolant-capsule', name:'냉각 캡슐', kind:'instant', pulp:4, bloom:0, cooldown:12},
  {id:'folding-barricade', name:'접이식 방벽', kind:'place', pulp:6, bloom:0, maxActive:2},
  {id:'med-injector', name:'응급 주사', kind:'instant', pulp:5, bloom:1, cooldown:18},
  {id:'flare-bundle', name:'휴대 조명탄', kind:'place', pulp:3, bloom:0, maxActive:2}
];
```

공통 제작 가능 조건:

- `CREW.phase === 'play'`.
- 플레이어가 다운 상태가 아님.
- `G.gPulp >= recipe.pulp && G.gBloom >= recipe.bloom`.
- 해당 레시피의 재사용 대기시간이 0.
- 활성 설치물 수가 `maxActive` 미만.

재료는 즉시 사용형은 효과 적용 직전에, 설치형은 유효한 지점에 설치가 확정되는 순간 차감한다. 취소·충돌·설치 불가 시 차감하지 않는다. 차감 뒤 `paintResHud()`와 `updateResources()`를 호출한다.

## 8. 제작물 기능

### 8.1 자동 포탑

- 비용: PULP 8, BLOOM 2.
- 설치 거리: 플레이어 중심에서 최대 `CELL * 2.8`.
- 사거리: 190 world px.
- 발사 간격: 0.48초.
- 지속시간: 45초.
- 동시 활성: 1개. 기존 포탑이 있으면 새 제작을 막고 `설치 한도 1/1` 표시.
- 엔지니어 전용 `CREW.turrets`와 데이터 충돌을 피하기 위해 `CRAFTING.active.turret`에 별도로 보관한다.
- 월드 표현: `icon-auto-turret.png`를 52×52px로 그리되, 기존 LOS 판정과 화면 좌표 변환을 적용한다.
- 엔지니어 센트리보다 연사·탄약·네트워크 효과가 약하며 전력 노드 보너스를 받지 않는다.

### 8.2 냉각 캡슐

- 비용: PULP 4.
- 즉시 `G.drillHeat`를 현재 값의 25%로 감소.
- `G.drillHeatLock`을 0으로 만든다.
- 재사용 대기시간: 12초.
- 드릴 열 시스템이 꺼져 있거나 열이 10% 미만이면 제작 불가 상태 `지금은 필요 없음`.
- 사용 VFX: 플레이어 위치에 청록색 `J.ring` 2회와 작은 증기 입자. 아이콘은 월드에 남기지 않는다.

### 8.3 응급 주사

- 비용: PULP 5, BLOOM 1.
- 최대 체력의 35% 회복, 최대치 초과 없음.
- 0.7초 사용 시간. 이 시간 동안 드릴/사격/대시만 막고 이동은 45% 속도로 허용한다.
- 사용 중 직접 피해를 받으면 취소하고 재료는 반환한다.
- 체력이 90% 이상이면 제작 불가 상태 `체력이 충분함`.
- 재사용 대기시간: 18초.

### 8.4 휴대 조명탄

- 비용: PULP 3.
- 조준 방향 `CELL * 2.6` 위치에 투척.
- 반경: `DEMO.lampRadius * 1.0`.
- 지속시간: 30초.
- 동시 활성: 2개.
- 기존 `G.lamps` 객체를 사용하되 `craft:1` 플래그를 추가한다.
- 스카웃 플레어의 정찰 펄스와 시야 보너스는 발동하지 않는다.
- 월드 표식: `icon-flare-bundle.png`를 34×34px로 표시하고 중심에 기존 조명 VFX를 합성한다.

### 8.5 접이식 방벽

- 비용: PULP 6.
- 설치 거리: 최대 `CELL * 2.3`.
- 플레이어 조준 방향에 수직으로 배치.
- 크기: `CELL * 1.7 × CELL * 0.38`.
- 내구도: 플레이어 최대 체력의 1.6배.
- 지속시간: 35초.
- 동시 활성: 2개.
- 플레이어와 아군 투사체는 통과하고 적 이동·적 투사체만 차단한다.
- 월드 표현: `icon-folding-barricade.png`를 방향에 맞춰 회전하고 78×48px로 렌더링한다.

### 8.6 성형 폭약

- 비용: PULP 7, BLOOM 2.
- 설치 거리: 최대 `CELL * 2.2`, 벽 또는 바닥 인접 지점.
- 기폭 지연: 2초.
- 벽 피해: 일반 벽 체력의 180%, 기반암에는 20% 압력만 적용.
- 적 피해: 기본 총기 피해 기준 2.2배, 반경 `CELL * 1.6`.
- 플레이어·아군 피해 없음.
- 동시 활성: 2개.
- 기존 `J.flash`, `J.ring`, `J.burst`와 `TUNNEL_PROJECTILE_FX.impact(...,'explosive')`를 재사용한다.
- 월드 표현: `icon-shaped-charge.png`를 38×38px로 그리며 남은 2초 동안 붉은 상태등을 점멸한다.

## 9. UI 상태 표현

| 상태 | 슬롯 | 상세 카드 | 버튼 |
|---|---|---|---|
| 기본 | 100% 채도, 금속 프레임 | 설명·비용 | `제작하기` |
| 선택 | 앰버 선택 오버레이, 1.04배 | 선택 아이콘·효과·비용 | 밝기 100% |
| 재료 부족 | 아이콘 45% 채도, 62% opacity | 부족한 수량만 `#ff718a` | `재료 부족`, 비활성 |
| 활성 한도 | 프레임 보라색, 숫자 배지 | `설치 한도 N/N` | 비활성 |
| 쿨다운 | 원형 CSS 마스크로 남은 비율 | 남은 초 표시 | 비활성 |
| 제작 성공 | 아이콘이 중심으로 0.16초 수축 | 체크 표시 0.45초 | 짧은 밝기 펄스 |

선택 변화마다 SFX의 가벼운 `tick`, 제작 성공은 `cache` 또는 전용 합성음을 사용한다. 마우스가 같은 슬롯 안에서 움직이는 동안 소리를 반복하지 않는다.

## 10. 본선 통합 위치

대상은 새 버전 파일을 만들지 않고 사용자가 지정한 본선 HTML 하나를 직접 수정한다.

1. 마크업: `#tcHudPolish` 블록 직후 `#tcCraftingRoot` 추가.
2. CSS: 기존 HUD polish 스타일 뒤에 `tcCraft*` 네임스페이스로 추가.
3. 상태: `const CREW` 선언 직후 `CRAFTING`, `CRAFT_RECIPES` 추가.
4. 시작 초기화: `startCrewMission()`에서 제작 상태·쿨다운·설치물 초기화.
5. 런 업데이트: `updateCrew(dt)`에서 `updateCrafting(dt)`와 설치물 업데이트 호출.
6. 월드 렌더: `drawCrewExtras()`에서 `drawCraftedObjects()` 호출.
7. 자원 UI: 제작 성공 후 `paintResHud()` 및 `updateResources()` 호출.
8. 입력: 전역 keydown/keyup/pointer 처리 앞쪽 캡처 단계에 제작 우선순위 추가.
9. 종료: `endCrewMission()`, 설정 열기, 레벨업/휴식/결과 모달에서 `closeCrafting(true)` 호출.
10. 코옵: 1차 솔로 검증 후 설치 결과 이벤트만 전송하고, 재료 차감은 플레이어별 로컬 소유로 유지.

## 11. 성능·접근성

- 12개 PNG 전체 런타임 원본은 약 1MB 수준이며, lossless WebP 세트는 약 0.6MB다.
- 휠을 처음 열 때 끊기지 않도록 미션 시작 시 `Image`로 12개 자산을 프리로드한다.
- 닫힌 상태의 DOM은 `display:none` 또는 `visibility:hidden; pointer-events:none` 처리한다.
- 애니메이션은 transform/opacity만 사용한다.
- `prefers-reduced-motion`에서는 회전·수축 애니메이션을 제거한다.
- 슬롯 버튼에는 레시피 이름, 비용, 가능 여부를 포함한 `aria-label`을 제공한다.
- 색만으로 부족 상태를 전달하지 않고 텍스트와 숫자를 함께 사용한다.

## 12. 완료 기준

로컬 QA에서는 `http://127.0.0.1/...html?crafttest=1`로 실행한 뒤 `F6`을 누르면 펄프·공명꽃 99개, 드릴 열 78%, 체력 48%가 세팅된다. 이 입력은 localhost/127.0.0.1과 `crafttest` 쿼리가 동시에 있을 때만 활성화된다.

- 6종 아이콘과 6종 UI 파츠가 모두 404 없이 로드된다.
- 모든 PNG가 RGBA이고 외곽 배경이 실제 투명이다.
- C 홀드→마우스 선택→좌클릭 제작→C 해제 흐름이 동작한다.
- 휠을 여는 동안 월드·적·미션 타이머가 계속 진행된다.
- 제작 중 드릴/사격이 오작동하지 않는다.
- 부족 재료·활성 한도·쿨다운·불필요 상태가 각각 구분된다.
- 설치 취소 시 자원이 차감되지 않는다.
- 제작에 자원을 사용해도 채취 목표 `G.nRes`는 감소하지 않는다.
- 엔지니어 포탑과 제작 포탑이 서로의 활성 한도를 침범하지 않는다.
- 900px 이하에서는 요약 UI로 전환되어 HUD와 겹치지 않는다.
- 단일 빌드에서 모든 크래프팅 자산이 인라인되고 외부 요청이 0건이다.

## 13. 자산 재가공

원본 생성 이미지를 다시 교체할 때는 다음 명령으로 동일 규격을 재생성한다.

```powershell
python tools/process-crafting-assets.py `
  --source-dir tmp/crafting-assets-source `
  --output-dir assets/ui/crafting
```

스크립트는 체크무늬 배경 제거, 알파 정리, 중앙 정렬, 규격 리사이즈, PNG 최적화, lossless WebP 생성, 매니페스트 및 미리보기 시트 생성을 수행한다.
