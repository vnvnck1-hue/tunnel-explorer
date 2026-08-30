# Tunnel Crew UI 9-Slice · UI Layout Lab 인수인계

## 0. 문서 목적

이 문서는 다음 구현 에이전트가 **독립 실행형 UI 전용 데모 모드**를 제작하기 위한 작업 명세다.

이번 단계의 핵심 결과물은 다음 두 가지다.

1. 나중에 실제 PNG UI 리소스를 넣으면 즉시 교체할 수 있는 공용 9-Slice 렌더 시스템
2. 사용자가 게임의 모든 UI를 직접 배치하고 꾸민 뒤 JSON으로 저장할 수 있는 UI Layout Lab

이 문서 작성 단계에서는 구현하지 않는다. 다음 에이전트가 아래 명세에 따라 구현한다.

---

## 1. 작업 대상과 강제 범위

### 새로 만들 파일

- `demos/tunnel-crew-ui-layout-lab.html`

초기 데모는 위 HTML **단일 파일을 계속 수정**한다. 별도 버전 파일이나 백업 파일을 만들지 않는다.

### 참고할 파일

- 현재 게임: `tunnel-crew-infinite-mode-v7.3.0-merged.html`
- 기존 HUD DOM/CSS: 현재 게임의 `#tcHudPolish`, `.tcMod`, `.tcSurface`, `#tcHudPolishStyles`
- HUD 데이터 갱신 예제: 현재 게임의 `<script id="tcHudPolishScript">`
- 드래그/리사이즈/JSON UI 참고: `claude-work/trait-card-layout-editor.html`
- 현재 재화 아이콘: `assets/ui/currency/`

### 이번 단계에서 수정하지 말 것

- `tunnel-crew-infinite-mode-v7.3.0-merged.html`
- 게임 규칙, 전투, 채굴, 저장 데이터
- 기존 메뉴 및 HUD의 실제 런타임 코드
- 실제 최종 9-Slice 아트 리소스

원본 게임에 `UI LAB` 모드 버튼을 추가하는 작업은 **후속 단계**다. 이번에는 독립 실행 데모까지만 완성한다.

---

## 2. 사용자 목표

사용자는 코드를 직접 수정하지 않고 전용 화면에서 아래 항목을 조절할 수 있어야 한다.

- 화면별 UI 레이아웃
- 패널 위치와 크기
- 앵커와 정렬 기준
- 레이어 순서
- 표시/숨김과 잠금
- 패널 스킨과 9-Slice 인셋
- 배경색, 테두리색, 강조색, 투명도
- 패딩, 내부 간격, 모서리 크기
- 텍스트 내용
- 글꼴 크기, 굵기, 행간, 자간, 정렬, 색상
- 아이콘 크기, 위치, 투명도, 색조
- 게이지 크기와 색상
- 버튼 기본/호버/눌림/비활성 상태
- PC와 모바일 화면 프리셋
- 전체 결과 저장, 불러오기, 복사, 파일 다운로드

에디터에서 만든 결과는 나중에 원본 게임이 그대로 읽을 수 있는 안정적인 JSON이어야 한다.

---

## 3. 최종 데모 화면 구조

전체 화면을 아래 3열 구조로 구성한다.

```text
┌──────────────┬──────────────────────────────────────┬──────────────────┐
│ 화면/레이어  │          실제 비율 미리보기          │ 속성 인스펙터    │
│ 목록         │   선택·드래그·리사이즈·9-Slice      │ 레이아웃/스타일  │
│ 컴포넌트 트리│                                      │ 텍스트/리소스    │
├──────────────┴──────────────────────────────────────┴──────────────────┤
│ 상태바 · 뷰포트 · 줌 · 스냅 · 실행취소 · JSON · 저장 상태             │
└────────────────────────────────────────────────────────────────────────┘
```

### 좌측 패널

- 화면(Scene) 목록
- 현재 화면의 UI 모듈 트리
- 모듈 검색
- 모듈 표시/숨김
- 모듈 잠금/잠금 해제
- 레이어 위/아래 이동
- 복제와 삭제
- 새 모듈 추가

### 중앙 스테이지

- 선택된 화면을 실제 종횡비로 표시
- 선택 테두리
- 8방향 리사이즈 핸들
- 드래그 이동
- 가이드와 스냅선
- 안전 영역 표시
- 화면 중앙선과 3분할선
- 9-Slice 경계 미리보기 토글
- 실제 UI 보기와 편집 오버레이 보기 전환

### 우측 인스펙터

탭을 아래처럼 나눈다.

1. `LAYOUT`
2. `PANEL`
3. `TEXT`
4. `COLOR`
5. `CONTENT`
6. `9-SLICE`
7. `STATE`

여러 요소를 선택했을 때는 공통 속성만 편집할 수 있어야 한다.

---

## 4. 화면(Scene) 프리셋

데모는 게임 전체 UI를 하나의 화면에 억지로 넣지 말고 화면 단위로 분리한다.

최소 화면 목록:

| scene id | 화면 |
|---|---|
| `mainMenu` | 메인 메뉴 |
| `missionSelect` | 미션 선택 |
| `biomeSelect` | 바이옴 선택 |
| `roleSelect` | 캐릭터/역할 선택 |
| `gameHud` | 일반 게임 HUD |
| `pauseSettings` | 일시정지·설정 |
| `missionResult` | 미션 정산 |
| `infiniteHud` | 무한 모드 HUD |
| `infiniteRest` | 층간 휴식/특성 선택 |
| `permanentGrowth` | 귀환 정산·영구 성장 지도 |
| `starmap` | 행성 선택 지도 |
| `labShell` | 각종 테스트 랩 공통 셸 |

처음부터 모든 화면을 완성된 아트로 만들 필요는 없다. 그러나 각 scene은 최소 2~5개의 대표 모듈을 가지고 있어 선택과 편집, 저장이 실제로 작동해야 한다.

현재 게임의 주요 HUD 모듈은 ID를 가급적 그대로 사용한다.

- `tc-objective`
- `tc-resources`
- `tc-status`
- `tc-skills`
- `tc-minimap`
- `tc-bag`
- `tc-interaction`
- `tc-briefing`
- `tc-settlement`

이 ID를 유지하면 후속 원본 게임 통합 시 매핑 비용이 줄어든다.

---

## 5. 편집 좌표계

### 저장 좌표

모듈의 기본 위치와 크기는 스테이지에 대한 퍼센트로 저장한다.

- `x`, `y`, `w`, `h`: 0~100 퍼센트
- 소수점 3자리까지 보존
- 편집 중 계산은 픽셀로 하되 저장 직전에 퍼센트로 정규화

### 앵커

각 모듈은 다음 앵커를 지원한다.

- `top-left`
- `top-center`
- `top-right`
- `center-left`
- `center`
- `center-right`
- `bottom-left`
- `bottom-center`
- `bottom-right`
- `stretch-x`
- `stretch-y`
- `stretch-both`

앵커 변경 시 현재 화면상의 위치가 갑자기 이동하지 않도록 현재 rect를 새 앵커 기준 값으로 재계산한다.

### 최소 크기

- 일반 패널: 42×30 px
- 텍스트: 24×18 px
- 아이콘: 16×16 px
- 9-Slice 패널: 좌우 인셋 합보다 넓고 상하 인셋 합보다 높아야 함

---

## 6. 9-Slice 시스템 요구사항

### 설계 원칙

지금은 실제 9-Slice PNG가 없지만 UI는 반드시 같은 API를 통해 렌더링한다. 임시 CSS 패널과 나중의 PNG 패널이 컴포넌트 코드를 바꾸지 않고 교체되어야 한다.

### 스킨 레지스트리

아래와 동등한 중앙 레지스트리를 둔다.

```js
const NINE_SLICE_SKINS = {
  simpleDark: {
    renderer: 'css',
    fill: 'rgba(15, 10, 26, .90)',
    borderColor: 'rgba(190, 145, 240, .46)',
    borderWidth: 2,
    radius: 12
  },
  panelMetal: {
    renderer: 'image',
    src: 'assets/ui/nine-slice/panel-metal.png',
    sourceSize: { w: 128, h: 128 },
    slice: { left: 24, top: 24, right: 24, bottom: 24 },
    borderScale: 1,
    centerMode: 'stretch',
    edgeMode: 'stretch'
  }
};
```

### DOM 렌더러

DOM 패널은 공용 클래스/함수로 적용한다.

권장 API:

```js
applyNineSlice(element, skinId, overrides)
```

이미지 스킨은 `border-image-source`, `border-image-slice`, `border-image-width`를 사용한다. 중앙 채움은 별도 배경 레이어 또는 스킨 메타데이터의 색상으로 처리한다.

필수 주의사항:

- 코너는 늘어나지 않아야 한다.
- 패널 크기가 작아져도 코너가 서로 겹치지 않아야 한다.
- `overflow`, 포커스 링, 선택 핸들이 잘리지 않아야 한다.
- 9-Slice 장식 레이어와 실제 콘텐츠 레이어를 분리한다.
- 선택 핸들은 장식 레이어 밖에 있어야 한다.

### Canvas 렌더러

후속 게임 통합을 위해 Canvas용 순수 함수도 데모 안에 구현한다.

권장 API:

```js
drawNineSlice(ctx, image, destRect, slice, options)
```

`drawNineSlice`는 원본을 9영역으로 나누어 `drawImage()` 9회로 그린다.

```text
┌──────┬──────────────┬──────┐
│ LT   │ TOP          │ RT   │
├──────┼──────────────┼──────┤
│ LEFT │ CENTER       │ RIGHT│
├──────┼──────────────┼──────┤
│ LB   │ BOTTOM       │ RB   │
└──────┴──────────────┴──────┘
```

지원 옵션:

- `centerMode: 'stretch' | 'tile' | 'none'`
- `edgeMode: 'stretch' | 'tile'`
- `alpha`
- `tint`는 가능하면 별도 오프스크린 캐시 사용
- 고정 코너 크기 또는 `borderScale`
- DPR 대응

### 캐시

같은 스킨과 같은 출력 크기는 반복해서 9번 그리지 않도록 오프스크린 캔버스 캐시를 둘 수 있다.

캐시 키 예시:

```text
skinId|width|height|borderScale|tint|dpr
```

리사이즈 중에는 즉시 렌더하되, 포인터를 놓은 뒤 최종 결과를 캐시에 넣는다.

---

## 7. 임시 심플 스킨

실제 PNG가 없는 현재 단계에서는 아래 CSS 스킨을 제공한다.

- `simpleDark`: 어두운 반투명 패널 + 보라 테두리
- `simplePaper`: 베이지 패널 + 갈색 테두리
- `simpleDanger`: 어두운 적색 패널
- `simpleButton`: 기본 버튼
- `simpleSlot`: 아이템 슬롯
- `none`: 배경 없는 컨테이너

중요: 임시 스킨도 각 모듈이 직접 CSS 클래스를 붙이는 방식이 아니라 `skinId`와 공용 `applyNineSlice()`를 통해 적용해야 한다.

나중에 `simpleDark.renderer`만 `image`로 바꾸더라도 모든 패널이 즉시 PNG 9-Slice로 교체되어야 한다.

---

## 8. 9-Slice 리소스 테스트 도구

우측 `9-SLICE` 탭에는 추후 PNG를 받아 바로 시험할 수 있는 미니 도구를 포함한다.

필수 기능:

- PNG 파일 선택
- 이미지 원본 크기 표시
- `left`, `top`, `right`, `bottom` 인셋 숫자 입력
- 인셋 가이드 시각화
- 3가지 크기 동시 미리보기
  - 작은 버튼
  - 중간 HUD 패널
  - 큰 팝업
- center stretch/tile 전환
- edge stretch/tile 전환
- `skinId`, 경로, slice metadata JSON 내보내기

파일 선택으로 만든 Object URL은 미리보기 전용이다. 기본 JSON에는 Blob URL이나 로컬 절대 경로를 저장하지 않는다.

경로는 프로젝트 상대 경로만 허용한다.

예:

```text
assets/ui/nine-slice/panel-metal.png
```

---

## 9. 편집기 핵심 조작

### 선택

- 클릭: 단일 선택
- `Shift + 클릭`: 다중 선택
- 빈 공간 클릭: 선택 해제
- 레이어 트리 클릭: 해당 요소 선택
- 더블 클릭: 텍스트 직접 편집 또는 그룹 안으로 진입

### 이동

- 드래그 이동
- 방향키 1 px 이동
- `Shift + 방향키` 10 px 이동
- 스냅 켜기/끄기
- 안전 영역, 스테이지 중앙, 다른 요소의 변과 중심에 스냅

### 리사이즈

- 8방향 핸들
- `Shift`: 종횡비 고정
- `Alt`: 중심 기준 리사이즈
- 최소 크기 보장
- 9-Slice 인셋보다 작아지는 크기 방지

### 복사와 삭제

- `Ctrl/Cmd + D`: 복제
- `Delete`: 삭제
- 잠긴 요소는 이동/삭제 불가
- 필수 루트 요소는 삭제 불가

### 실행 취소

- `Ctrl/Cmd + Z`: Undo
- `Ctrl/Cmd + Shift + Z` 또는 `Ctrl/Cmd + Y`: Redo
- 최소 50단계 기록
- 드래그 중 매 프레임 기록하지 말고 pointerup 시 한 단계로 기록

---

## 10. 인스펙터 속성

### LAYOUT

- x, y, width, height
- anchor
- z-index
- rotation
- opacity
- visible
- locked
- keep aspect
- min/max size

### PANEL

- skinId
- background opacity
- panel tint
- border scale
- padding 4방향
- gap
- content alignment
- overflow
- drop shadow 강도

### TEXT

- 텍스트 내용
- font family token
- font size
- font weight
- line height
- letter spacing
- horizontal alignment
- vertical alignment
- color
- text shadow
- wrap / no-wrap / ellipsis
- uppercase 변환

### COLOR

- 전역 테마 토큰
- 모듈별 override
- 배경색
- 테두리색
- 본문색
- 보조 텍스트색
- 강조색
- 성공/경고/위험색
- 게이지 시작/끝 색

색 입력은 color input과 RGBA/HEX 텍스트 입력을 함께 제공한다.

### CONTENT

- 샘플 숫자
- 제목/설명
- 아이콘 경로
- 아이콘 크기
- 게이지 값
- 리스트/슬롯 개수
- 빈 상태/최대 상태/긴 텍스트 상태

### STATE

- default
- hover
- pressed
- disabled
- selected
- warning
- complete
- failed

버튼과 슬롯은 상태별 색상과 스킨을 따로 편집할 수 있어야 한다.

---

## 11. 반응형 미리보기

상단 툴바에서 뷰포트를 전환한다.

- 1920×1080
- 1600×900
- 1366×768
- 1280×720
- 1024×768
- 844×390 모바일 가로
- 390×844 모바일 세로
- 사용자 지정

스테이지는 화면 안에 맞춰 축소 표시하되 내부 좌표는 선택한 논리 해상도를 유지한다.

안전 영역 프리셋:

- none
- desktop 16 px
- mobile notch landscape
- mobile portrait

프리셋 변경 후 요소가 화면 밖으로 나가면 경고만 표시한다. 사용자의 값을 자동으로 변경하지 않는다.

---

## 12. 테마 토큰

공통 디자인 값은 개별 요소에 흩뿌리지 말고 토큰으로 관리한다.

최소 토큰:

```js
{
  color: {
    bg: '#0a0612',
    panel: '#1d122b',
    panelAlt: '#120b20',
    border: '#8f5fd0',
    text: '#f3ebff',
    textMuted: '#9c88b4',
    accent: '#7febd0',
    accentAlt: '#f06ab8',
    warning: '#ffb048',
    danger: '#ff6b76'
  },
  font: {
    ui: 'Pretendard, Malgun Gothic, sans-serif',
    mono: 'ui-monospace, Consolas, monospace'
  },
  space: { xs: 4, sm: 7, md: 11, lg: 16, xl: 24 },
  radius: { sm: 7, md: 12, lg: 16 },
  motion: { fast: 100, normal: 180 }
}
```

전역 토큰을 바꾸면 override가 없는 모든 모듈이 즉시 갱신되어야 한다.

---

## 13. JSON 저장 스키마

스키마 이름과 버전을 고정한다.

```json
{
  "schema": "tunnel-crew-ui-layout-v1",
  "exportedAt": "2026-08-29T00:00:00.000Z",
  "viewport": {
    "preset": "1920x1080",
    "width": 1920,
    "height": 1080,
    "safeArea": "desktop"
  },
  "theme": {
    "tokens": {}
  },
  "skins": {
    "simpleDark": {
      "renderer": "css"
    },
    "panelMetal": {
      "renderer": "image",
      "src": "assets/ui/nine-slice/panel-metal.png",
      "sourceSize": { "w": 128, "h": 128 },
      "slice": { "left": 24, "top": 24, "right": 24, "bottom": 24 },
      "centerMode": "stretch",
      "edgeMode": "stretch"
    }
  },
  "scenes": {
    "gameHud": {
      "modules": [
        {
          "id": "tc-resources",
          "type": "resourceHud",
          "parentId": null,
          "rect": { "x": 39.808, "y": 5.428, "w": 20.384, "h": 5.697 },
          "anchor": "top-center",
          "z": 18,
          "visible": true,
          "locked": false,
          "skinId": "simpleDark",
          "layout": { "padding": [7, 7, 7, 7], "gap": 7 },
          "style": { "opacity": 1, "tint": null },
          "typography": {},
          "content": {},
          "states": {}
        }
      ]
    }
  }
}
```

### JSON 기능

- localStorage 자동 저장
- 수동 저장
- 초기값 복원
- JSON 텍스트 복사
- JSON 붙여넣기 불러오기
- `.json` 다운로드
- `.json` 파일 선택 불러오기
- 스키마와 버전 검증
- 잘못된 값은 전체를 파괴하지 말고 오류 위치 표시
- 알 수 없는 필드는 가능한 한 보존

권장 localStorage 키:

```text
tunnelCrew.uiLayoutLab.v1
```

---

## 14. 원본 게임과의 향후 통합 계약

이번 데모는 나중에 다음 방식으로 원본 게임에 들어갈 수 있어야 한다.

### 메뉴 진입

후속 단계에서 메인 메뉴에 `UI LAB` 또는 `UI 조정 모드` 버튼을 추가한다.

### 런타임 API

데모 구현 시 아래와 동등한 공개 API를 제공한다.

```js
window.TUNNEL_UI_LAB = {
  open(),
  close(),
  exportConfig(),
  importConfig(config),
  applyConfig(root, sceneId),
  reset(),
  getState(),
  skins,
  drawNineSlice
};
```

독립 데모에서는 `open()` 상태로 시작한다. 원본 게임에 합칠 때는 루트 오버레이를 숨긴 상태로 두고 메뉴 버튼에서 연다.

### 게임 DOM 적용

`applyConfig(root, sceneId)`는 모듈 ID를 이용해 기존 요소에 다음을 적용한다.

- 위치와 크기
- CSS custom properties
- skinId
- typography
- 색상 토큰
- 표시 상태

게임 상태 데이터 갱신 로직과 편집기 스타일 적용 로직을 분리한다. 예를 들어 `updateResources()`는 숫자만 바꾸고 패널의 위치와 색은 `applyConfig()`가 담당해야 한다.

---

## 15. 구현 권장 구조

단일 HTML 내부 코드는 역할별로 명확히 나눈다.

```text
1. Constants / schema / defaults
2. NineSliceRegistry
3. DOM nine-slice renderer
4. Canvas drawNineSlice renderer
5. Scene and module templates
6. Editor state store
7. History manager
8. Stage renderer
9. Selection / drag / resize controller
10. Inspector builders
11. Resource slice tester
12. Import / export / persistence
13. Keyboard shortcuts
14. Bootstrap and public API
```

프레임워크나 빌드 도구 없이 순수 HTML/CSS/JS로 작성한다. 외부 CDN과 네트워크 의존성을 추가하지 않는다.

---

## 16. 성능과 안정성

- pointermove에서는 필요한 rect만 갱신한다.
- 인스펙터 전체를 매 프레임 다시 만들지 않는다.
- 드래그 중 localStorage 저장을 반복하지 않는다.
- 저장은 pointerup 또는 250 ms debounce를 사용한다.
- DOM 측정과 DOM 쓰기를 한 루프에서 마구 섞지 않는다.
- 미리보기 이미지는 `Image.decode()` 또는 load 완료 후 적용한다.
- 없는 이미지 경로는 심플 CSS 스킨으로 폴백한다.
- 편집기 오류가 나도 JSON 내보내기와 초기화 버튼은 사용할 수 있어야 한다.
- 모바일에서 스테이지 터치 드래그와 패널 스크롤이 충돌하지 않도록 한다.
- `prefers-reduced-motion`을 존중한다.

---

## 17. 접근성

- 모든 버튼에 명확한 텍스트 또는 aria-label 제공
- 키보드만으로 레이어 선택과 숫자 입력 가능
- 선택 요소를 색상만으로 구분하지 않음
- 포커스 링 제거 금지
- 색 대비 경고 제공
- 텍스트 크기가 10 px 미만이면 경고
- 터치 핸들 최소 24 px 히트 영역 확보

---

## 18. 완료 조건

다음 항목이 모두 작동해야 완료다.

### 9-Slice

- 임시 CSS 스킨이 공용 skin API를 통해 적용됨
- 이미지 기반 스킨 정의를 등록할 수 있음
- DOM 패널이 크기 변경 시 코너를 유지함
- Canvas `drawNineSlice()`가 3가지 크기에서 정상 작동함
- PNG 업로드 후 4방향 slice 값을 조절하고 즉시 미리보기 가능
- 이미지 로드 실패 시 CSS 스킨 폴백

### 레이아웃 편집

- scene 전환
- 모듈 선택
- 드래그 이동
- 8방향 리사이즈
- 스냅
- 앵커 변경
- 레이어 순서 변경
- 표시와 잠금
- 복제와 삭제
- Undo/Redo

### 스타일 편집

- 패널 크기/위치
- 패딩과 간격
- 텍스트 크기/색상/정렬/내용
- 패널과 테두리 색상
- 아이콘 크기와 위치
- 게이지 값과 색상
- 상태별 버튼 미리보기
- 전역 테마 토큰 반영

### 저장

- 자동 저장 후 새로고침 복구
- JSON 복사/붙여넣기
- 파일 다운로드/불러오기
- 초기화
- `tunnel-crew-ui-layout-v1` 스키마 출력

### 반응형

- 1920×1080
- 1366×768
- 844×390
- 390×844

위 네 해상도에서 에디터 조작과 미리보기가 깨지지 않아야 한다.

---

## 19. 검증 절차

구현 후 가능하면 로컬 서버로 연다.

```powershell
npx --yes serve . -p 5188
```

접속 경로:

```text
http://127.0.0.1:5188/demos/tunnel-crew-ui-layout-lab.html
```

검증 순서:

1. `gameHud` 화면을 연다.
2. `tc-resources`를 이동하고 크기를 바꾼다.
3. 패널 색과 텍스트 크기를 변경한다.
4. 다른 뷰포트 프리셋으로 전환한다.
5. Undo/Redo를 확인한다.
6. JSON을 내보낸다.
7. 초기화한다.
8. 방금 JSON을 다시 불러와 동일한 화면인지 확인한다.
9. 임시 PNG를 9-Slice 탭에 넣고 인셋을 조절한다.
10. 작은 버튼, HUD, 큰 팝업에서 코너가 늘어나지 않는지 확인한다.
11. 콘솔 오류와 404 리소스가 없는지 확인한다.

---

## 20. 구현 에이전트가 최종 보고할 내용

- 새로 만든 파일
- 구현한 주요 기능
- 공개 API
- JSON 스키마 예시
- localStorage 키
- 테스트한 뷰포트
- 9-Slice 이미지 교체 방법
- 로컬 브라우저 검증 결과
- 남은 제한 또는 후속 통합 작업

---

## 21. 절대 피해야 할 구현

- 각 패널마다 별도 하드코딩된 9-Slice 코드 작성
- 패널마다 서로 다른 좌표 체계 사용
- 편집 결과를 CSS 문자열 하나로만 저장
- Blob URL 또는 `C:\...` 절대 경로를 JSON에 저장
- 드래그 중 매 프레임 history/localStorage 기록
- 사용자 JSON import 시 검증 없이 전역 상태를 교체
- 원본 게임의 전투·채굴 코드까지 데모에 복사
- 이번 단계에서 원본 게임 HTML에 모드를 억지로 합치기
- 최종 리소스가 없다는 이유로 9-Slice API를 생략하기

---

## 22. 권장 구현 순서

1. 기본 에디터 셸과 중앙 스테이지
2. scene/module 데이터 모델
3. 임시 CSS 스킨 + `applyNineSlice()`
4. Canvas `drawNineSlice()`와 테스트 패널
5. 선택·이동·리사이즈
6. 레이어 트리와 인스펙터
7. 텍스트·색상·콘텐츠 편집
8. 테마 토큰
9. Undo/Redo
10. localStorage와 JSON import/export
11. PNG 9-Slice 리소스 테스트 도구
12. 반응형/키보드/접근성 검증
13. 공개 API와 향후 통합 주석 정리

이 순서를 지키면 실제 UI 아트가 도착하기 전에도 편집 시스템 전체를 검증할 수 있고, 나중에는 스킨 레지스트리의 이미지 경로와 slice 값만 추가하여 원본 게임에 적용할 수 있다.
