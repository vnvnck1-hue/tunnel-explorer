# 땅굴 크루 블렌드 조명 (LX) — v7.9.1-lighting-develop

포토샵 블렌드 모드(multiply·screen·overlay·soft light·color dodge·hue·color…)로 조명이 씬과 유기적으로 반응하도록 만든 조명 개발 분기.

| 항목 | 위치 |
|---|---|
| 분기 파일 | `tunnel-crew-infinite-mode-v7.9.1-lighting-develop.html` |
| 생성 스크립트 | `node tools/inject-lighting-lx.mjs tunnel-crew-infinite-mode-v7.9.0.html <dst>` (바이트 패치 35개) |
| 패널 코드 | `tools/lx-panel.js` (`</body>` 앞에 주입) |
| 실행 | 정적 서버(`.claude/launch.json` → `tunnel-crew-static`, 8321)에서 열기 |
| 패널 열기 | **F10** 또는 URL 에 `?lx` |

## 레이어 (합성 순서)

| # | 레이어 | 기본 블렌드 | 역할 |
|---|---|---|---|
| ① | 컬러 라이트맵 | multiply | 씬 × (어둠색 → 광원색). 광원마다 색이 붙는다 |
| ② | 대비 | overlay | 회색 0.5 중립. 어둠은 눌리고 빛은 대비·채도 상승 |
| ④ | 구역 앰비언스 | color | 시점 캐릭터 발밑 지층 밴드 색으로 색조 한 겹 (어두운 곳 위주) |
| ③ | 핫코어 | color-dodge | 광원 세기 ≥ threshold 인 중심만 태운다 |
| ⑤ | 스프라이트 명암 | 그늘 multiply · 빛면 screen · 림 lighter | LIT 모듈, Canvas2D 블렌드 |
| — | 벽 그림자·음영·림 | multiply · multiply · screen | LIT 모듈 |

광원 종류: `hero`(램프) `flash`(손전등) `torch`(횃불·플레어) `cold`(결정 등불) `bio`(식생) `boss` `fx`(투사체) `exit`. 각각 색 + 세기 배율.

"LX ON/OFF" 버튼이 구 안개 셰이더와의 A/B. 프리셋: 기본(LX) / 기존 조명 재현 / 따뜻한 갱도 / 네온 결정.

## JSON 전달

패널 하단 "JSON 복사" → 그대로 전달. 형식:

```json
{ "version": "7.9.1-lx",
  "lx":  { "lights": {...}, "lightmap": {...}, "contrast": {...}, "core": {...}, "zone": {...}, "sprite": {...}, "wall": {...} },
  "lit": { ...LIT_TUNE (스프라이트·벽 재질 조명 수치) },
  "te":  { "ambient", "flashRange", "halfAngle", "heightRatio", "nStrength", "fogDensity", "lightSteps", "softMask", "flashlight", "breathe" } }
```

"붙여넣은 JSON 적용"으로 되돌려 넣을 수 있고, 값은 localStorage(`tc_lx_v791b`)에 자동 저장된다. 본편 이식은 `LX_DEFAULT` 를 전달받은 `lx` 로, `LIT_TUNE`/`TE` 는 각 필드로 교체.

## 구현 메모

- 씬 캔버스를 매 프레임 WebGL 텍스처로 올려(`texImage2D(cv)`) 안개 셰이더 `lxProg` 안에서 블렌드 수식을 직접 계산한다. 출력이 불투명하므로 `fogGL` 은 씬 해상도(OW/OH), 라이트 마스크 FBO 는 `LIGHTMAP_SCALE` 유지.
- 마스크: RGB = 광원색×세기 가산, A = 세기 MAX (`blendEquationSeparate`). 셰이더에서 `tint = rgb / max(rgb)`, `lit = a`.
- 스프라이트: 블렌드 모드는 투명 영역에도 칠하므로 `destination-in` 으로 원본 실루엣을 복원한다.
- 식생 글리머는 `paintUI`(UI 캔버스) 안으로 이동 — 스테이지 위에 그리면 불투명 출력에 가려진다.
- 런 시작 직후 베이지색으로 씻긴 화면은 `G.flash` 시작 플래시(UI 캔버스)이며 조명과 무관.
- 비용: FoW 프레임 약 1~2 ms (1280×720, 스탬프 9개).
