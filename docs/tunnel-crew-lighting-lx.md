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

"LX ON/OFF" 버튼이 구 안개 셰이더와의 A/B. 프리셋: 기본(LX) / 기존 조명 재현 / 따뜻한 갱도 / 화려함 A(오버레이) / 화려함 C(글로우·색대비, 사용자 방향 ③④) / 화려함 B(하드라이트) / 네온 결정. 빛 밝기 1.0 초과 + 세기 1.5 초과는 광원 반경이 단색으로 날아가므로 화려함은 채도·어둠색·닷지·글로우로 만든다.

## JSON 전달

패널 하단 "JSON 복사" → 그대로 전달. 형식:

```json
{ "version": "7.9.1-lx",
  "lx":  { "lights": {...}, "lightmap": {...}, "contrast": {...}, "core": {...}, "zone": {...}, "sprite": {...}, "wall": {...} },
  "lit": { ...LIT_TUNE (스프라이트·벽 재질 조명 수치) },
  "te":  { "ambient", "flashRange", "halfAngle", "heightRatio", "nStrength", "fogDensity", "lightSteps", "softMask", "flashlight", "breathe" } }
```

"붙여넣은 JSON 적용"으로 되돌려 넣을 수 있고, 값은 localStorage(`tc_lx_v791c`)에 자동 저장된다. 본편 이식은 `LX_DEFAULT` 를 전달받은 `lx` 로, `LIT_TUNE`/`TE` 는 각 필드로 교체.

## 구현 메모

- 라이트 마스크(WebGL FBO): RGB = 광원색×세기 가산, A = 세기 MAX (`blendEquationSeparate`). 셰이더에서 `tint = rgb / max(rgb)`, `lit = a`.
- 합성은 **레이어 → Canvas2D 블렌드** 방식이다. `lxProg` 가 레이어 하나(①라이트맵/②대비/④구역/③핫코어)의 색+알파만 `fogGL` 에 그리고, 그 캔버스를 스테이지 `cx` 에 `globalCompositeOperation = 모드` 로 `drawImage` 한다(레이어당 1회, 4패스). `fogGL` 은 LX 켜짐 상태에서는 숨김(오프스크린 버퍼).
- 씬 캔버스를 **읽지 않는다**. 초기 구현은 스테이지를 `texImage2D` 로 올려 셰이더 안에서 블렌드했는데, `file://` 로 열면 상대경로 이미지(monster frames 등)가 교차 출처로 취급돼 스테이지가 오염(tainted)되고 업로드가 `SecurityError` 로 실패 → 한 프레임 반짝 후 구 안개로 떨어지는 증상이 났다(2026-09-03 사용자 보고). 지금 방식은 오염된 캔버스에서도 동작함을 127.0.0.1 교차 출처 이미지로 검증했다.
- 모드 매핑: `normal→source-over`, `linear-dodge→lighter`, 나머지는 Canvas2D 이름과 동일(`LX_canvasOp`).
- 소팅: LX 모드에서는 조명 레이어를 히어로 스프라이트 직전에 합성한다(`lxEarly`). 바닥·벽·적은 빛 아래, 히어로·AI 크루·코옵 피어·박쥐·파티클은 빛 위라 손전등 콘·핫코어가 캐릭터를 덮지 않는다. 패널 "크루 위 소팅" 버튼(`LX.sortUnderCrew`)으로 끄면 예전처럼 전체 위 합성. 구 안개 모드는 항상 전체 위.
- 스프라이트: 블렌드 모드는 투명 영역에도 칠하므로 `destination-in` 으로 원본 실루엣을 복원한다.
- 식생 글리머는 `paintUI`(UI 캔버스) 안으로 이동.
- 런 시작 직후 베이지색으로 씻긴 화면은 `G.flash` 시작 플래시(UI 캔버스)이며 조명과 무관. 2~3초 뒤 판단.
- 비용: FoW 프레임 약 1 ms (1280×720, 스탬프 9~11개, 4패스 drawImage 포함).
