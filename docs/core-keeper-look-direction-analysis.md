# 코어키퍼 룩으로 방향 전환 — 구조 분석과 목표 변경점

작성 2026-09-10 · 판단 근거: 코어키퍼 패치노트·위키 리서치 + 원본 HTML 본편(`v7.9.2`) 코드 실측 +
유니티 랩(`LightingPresetLab`) 현재 상태 실측

---

## 0. 결론 먼저

사용자 지적이 정확하다. **공간감이 없는 원인은 조명 수치가 아니라 "벽 너머가 보인다"는 것 하나다.**

지금 유니티 랩은 34×20 바닥 평면에 벽 블록을 섬처럼 올려놓은 구조다.
Global Light2D 가 `I=0.35` 로 전 화면을 균일하게 칠하므로, 플레이어가 어느 구석에 서 있어도
방 전체가 동시에 읽힌다. 이건 "폐쇄된 지하"가 아니라 **조명이 예쁘게 걸린 지형도**다.
벽에 면을 나눠 붙이고 AO 를 깔고 드롭섀도를 넣어도, 동시에 보이는 면적이 저만큼이면
부피는 절대 안 생긴다. 부피는 **보이지 않는 것과의 대비**에서 나온다.

그리고 이 방향 전환은 **새 방향이 아니라 기획서로의 복귀**다.
`docs/tunnel-crew-gdd.md:114` 에 이미 이렇게 적혀 있다:

> **타일 LOS + FoW**: 벽 너머 완전 어둠, 탐색 잔광, 횃불/플레어로 시야 확보.

즉 우리는 기획서를 이탈해 "Ori 식 열린 평면 라이팅"을 튜닝하고 있었다. 그게 헛수고였던 이유다.

**그리고 가장 중요한 발견: 이 시스템은 이미 만들어져 있다.**
원본 HTML 본편 `v7.9.2` 에 완성된 타일 LOS + WebGL 라이트마스크 파이프라인이 돌고 있고,
검증된 수치까지 박혀 있다. 유니티에서 새로 발명할 게 아니라 **이식**하면 된다.

---

## 1. 코어키퍼는 어떻게 만들어졌나

### 1-1. 월드 구조 — "장식된 방"이 아니라 "파낸 고체"

코어키퍼의 기본 상태는 **월드 전체가 고체 암반**이다. 바닥은 파낸 결과물이지 기본값이 아니다.
그래서 화면에서 플레이어에게 인접한 면적의 대부분은 항상 고체 덩어리다.

이 구조가 만드는 결과:

- **"벽 너머"라는 공간이 존재하지 않는다.** 벽 너머는 방이 아니라 빛이 닿을 수 없는 고체 부피다.
- 내가 파낸 통로의 **실루엣 자체가 화면 구성**이 된다. 구도를 조명이 만들지 않고 굴착 형태가 만든다.
- 굴착이 곧 시야 확장이므로 **채굴 행위에 즉각적인 시각 보상**이 붙는다.

우리 랩은 정반대다. 바닥이 기본값이고 벽이 예외(섬)다. 그래서 파낼 것도 없고 가릴 것도 없다.

### 1-2. 조명 — 타일 전파(간접광)가 주항, 오브젝트 섀도가 부항

패치 `0.5.2.0-b84a` 가 조명 엔진을 전면 교체했고, 노트가 구조를 그대로 알려준다:

| 항목 | 내용 |
|---|---|
| 렌더 파이프라인 | 신규 파이프라인으로 교체(조명·GPU 성능 동시 개선) |
| **간접광(bounce)** | 전면 재작업. **빛이 더 멀리 전파되고 표면 색을 반영** → 부드럽고 더 채도 높은 동광 |
| 오브젝트 섀도 | 신규 시스템. 대부분의 오브젝트·적이 **광원별 그림자**를 드리움. 설정 Off/Simple/Advanced |
| 이미시브 | 이미시브 표면이 **직접 빛을 생성**(용광로·제련로 등) |
| 광원 최적화 (`0.5.2.4`) | 광원이 꺼지는 대신 **저비용 대체 렌더링**으로 전환. `Light Quality` 설정이 "진짜" 광원 수를 결정 |
| 섀도 큐 | 렌더 준비가 안 된 광원은 **shadow update queue** 대기 중 저비용 대체본으로 그려 팝핑 제거 |
| 색 처리 (`0.5.2.2`) | 광원 색을 **선형 공간으로 변환**해 수집. 톤매핑 수정, 블룸 임계값 조정 |

읽어야 할 핵심은 두 줄이다:

1. **주항은 타일 격자 위의 빛 전파(flood)다.** 열린 타일을 따라 퍼지고 고체에서 멈춘다.
   "벽 너머 완전 검정"은 그림자 캐스팅의 결과가 아니라 **전파가 도달하지 못한 결과**다.
   비용이 광원 수에 비례하지 않아 횃불을 수백 개 박아도 버틴다.
2. **부항은 광원별 오브젝트 섀도다.** 캐릭터·적·펜스가 드리우는 디테일. 끌 수 있는 옵션인 이유가 이것 —
   룩의 본질이 아니라 장식이다.

우리는 지금 **부항만 구현하고 주항이 없다.** ShadowCaster2D 로 그림자를 만들고 있지만,
Global Light2D 는 애초에 오클루전 개념이 없어서 모든 타깃 레이어의 모든 픽셀을 무조건 칠한다.
`I=0.35` 인 동안은 무슨 짓을 해도 벽 너머가 보인다.

### 1-3. 어둠은 게임플레이 자원

코어키퍼는 자연광이 거의 없다. 광원은 횃불·램프·펫 글로우·발광 자원뿐이고,
램프 슬롯에 램프를 끼워 "빛 방울"을 들고 다닌다. 랜턴은 `+8 Glow` 까지 강화된다.
즉 **시야 반경이 육성 대상**이다. 스팀 토론에 "왜 이렇게 어둡냐"는 글이 계속 올라오는데,
답이 늘 "횃불은 싸다, 탐험한 곳은 다 밝혀라"인 것 — 어둠이 버그가 아니라 루프의 축이라는 뜻이다.

우리 기획서도 이미 같은 구조다: 스카웃이 "어둠을 연다"(플레어/횃불 투척), 드릴러는 조명 담당,
횃불·플레어는 공용 소모품(§72, §84, §127).

---

## 2. 이미 우리가 가진 것 — 원본 HTML 본편의 LOS 파이프라인

`build/TunnelCrew-v7.9.2/tunnel-crew-infinite-mode-v7.9.2.html` 에 완성본이 있다.
**유니티로 이식할 대상은 이것이고, 알고리즘과 수치를 새로 정할 필요가 없다.**

### 2-1. 타일 LOS (`const LOS`, L5164~)

```
explored : Uint8Array(COLS*ROWS)   탐색 기록(영구)
visible  : Uint8Array(COLS*ROWS)   현재 프레임 시야
pixels   : Uint8Array(n*4)         셰이더 업로드용 RGBA
```

- **레이 캐스트**: 2π 를 `losRays` 등분해 Bresenham 타일 워크.
  `see(x,y)` 로 표시 → **고체면 그 타일까지 표시하고 차단**(벽 자체는 보이고 그 너머는 안 보인다).
- 플레이어 주변 8칸은 항상 가시(구석 벽이 안 보이는 문제 방지).
- **시야원이 여러 개**: 플레이어 + 코옵 피어 전원 + AI 크루 전원(range 5) + 스카웃 플레어(원격, 독립)
  + 등장한 보스 주변(단 `visOnly=true` — 탐색 기록은 남기지 않음).
- 캐시: 셀 좌표·range·rays·mem·보스키가 그대로면 재계산 생략.

### 2-2. 픽셀 채널 계약 (L5303~)

```
R = 현재 시야        vis ? 255 : 0
G = 탐색 잔상        explored 이면 exp * (0.30 + 0.70 * fade²),  fade = 1 - dist/mem
B = max(R, G)        호환 합성값
```

멀어진 탐색 지역도 **30% 기억 농도를 유지**해 갑자기 완전 검정으로 잘리지 않게 한다.
업로드 전 `losVisualValues` 로 **시간 이징**을 걸어 밝아짐/어두워짐이 부드럽게 흐른다.

### 2-3. 검증된 수치

| 파라미터 | 값 | 의미 |
|---|---|---|
| `losRange` | **19 타일** | 지형이 *드러나는* 반경 |
| `losRays` | **360** | 1° 간격 |
| `losMemory` | **11 타일** | 탐색 잔상이 밝게 유지되는 반경 |
| `losExplored` | **0.29** | 잔상 최대 농도 |
| `lampRadius` | **94px / CELL 50 ≈ 1.9 타일** | 실제로 *밝은* 반경 |
| `LIGHTMAP_SCALE` | 0.65 | 마스크 해상도(성능) |

**여기가 룩의 핵심 비율이다.** 드러나는 반경 19 타일 vs 밝은 반경 2 타일 —
거의 10배다. 그래서 "윤곽은 보이지만 어둡고, 내 주변만 환하다"는 지하 감각이 나온다.
지금 우리 랩은 이 비율이 1:1 이다(전부 드러나고 전부 밝다).

### 2-4. 퍼플이 사는 자리 — 이게 사용자 요구의 해답

```js
lightmap:{on:true, mode:'multiply', opacity:1, dark:'#44248F',
          bright:1.03, saturation:.9, litColor:1, litClear:.07}
```

`dark: #44248F` — **퍼플은 앰비언트 광원이 아니라 라이트마스크의 "어둠 색"이다.**
Multiply 로 전 화면에 곱하고, 밝은 곳만 `litClear:.07` 로 빼준다.

이게 중요한 이유: 앰비언트를 0 으로 내려도 **퍼플이 사라지지 않는다.**
오히려 어두운 영역이 넓어질수록 퍼플이 화면을 더 지배한다.
사용자가 지킨다고 한 "퍼플 컬러톤"과 "벽 너머 완전 암흑"은 **충돌하지 않고 서로를 강화한다.**
(현재 유니티 랩은 퍼플을 `WorldVisualProfile.ambientColor (0.62,0.50,0.76)` 에 넣어 뒀다 —
이 방식은 앰비언트를 못 내린다. 옮겨야 한다.)

---

## 3. 유니티 랩 현재 상태 실측 (전환 전 기준선)

```
Global 'Global (ambient)'  I=0.35  blend=0  tgt=8 layers   ← 전 화면 균일 조명
Point  Worklamp ×3         I=2.35  blend=0
Point  MineralGlow ×5      I=1.60  blend=3
Point  Indicator ×3        I=0.38  blend=1
Freeform Negative ×5       I=0.45  blend=0                 ← 어둠을 손으로 칠하던 우회책
ShadowCaster2D  = 0 (에디트) / 런타임 Bind 시 생성
바닥 438셀 전부 가시 · 전부 조명 · LOS 없음 · explored 없음
```

`Negative Freeform` 5개가 지금 하고 있는 일이 바로 "어둠을 수동으로 그리기"다.
전파 기반 LOS 가 들어오면 **이 우회책은 통째로 불필요해진다.** 구조가 단순해진다.

---

## 4. 목표에서 달라지는 것

### 4-1. 죽는 것 / 축소되는 것

| 항목 | 지금 | 전환 후 |
|---|---|---|
| **Global Light2D 앰비언트 0.35** | 룩의 기반 | **≈0.02~0.05 로 내림.** 기반이 라이트마스크로 이동 |
| **Negative Freeform 광원 5개** | 어둠 수동 조형 | **삭제.** LOS 전파가 정확히 대신함 |
| **`ambientColor` 퍼플** | 앰비언트 색 | **라이트마스크 `dark` 색으로 이전**(#44248F 계열) |
| **벽 상단면 변형 3종의 값** | 넓은 면적에 무늬 분산 | **하락.** 벽 윗면 대부분이 미탐색 검정에 묻힘 |
| **`AtmosphereDirector` 포그·깊이분리** | 거리감 담당 | **중복.** 어둠이 거리감을 직접 만든다. 비네트도 이중 적용 정리 |
| **프리셋 랩의 "무드 축" 중 앰비언트 축** | A/B 대상 | **거의 고정값.** 대신 `dark`·`litClear`·`losRange/lampRadius` 비율이 새 A/B 축 |
| **34×20 열린 평면 테스트 방** | 표준 테스트 씬 | **폐기.** 고체 채움 + 굴착 통로 구조로 재작성 |

### 4-2. 살아나는 것 / 값이 올라가는 것

| 항목 | 이유 |
|---|---|
| **`SurfaceMask.Buried` 컬링** | 미탐색 고체는 통째로 평면 검정 → 컬링 이득이 지금보다 훨씬 큼 |
| **경계면 3종 (FrontFace / TopRim / ContactAo)** | **보이는 벽면이 굴착 경계에만 남는다.** 이 3종이 룩 전체를 짊어짐. 이번에 넣은 서/동 측면·코너도 여기서 값을 함 |
| **블룸 + 톤매핑** | 검정 배경 위 밝은 광원이 룩의 전부. 코어키퍼도 블룸 임계값을 따로 튜닝했다 |
| **광원 색 채도** | 앰비언트가 없으면 광원 색이 곧 화면 색. `saturation` 이 실질 파라미터가 됨 |
| **굴착(채굴) 연출** | 시야 확장이 곧 보상. 게임 루프와 비주얼이 같은 축에 올라감 |
| **코옵 감각** | LOS 를 크루 전원이 합산 → 동료가 비춘 곳이 보인다. 원본에 이미 구현돼 있고 **코옵 게임에서 이건 큰 자산** |

### 4-3. 새로 필요한 것

| 항목 | 규모 | 비고 |
|---|---|---|
| **타일 LOS 계산기(C# 포팅)** | 중 | 원본 `LOS` 객체를 그대로 이식. 순수 함수라 EditMode 테스트 가능 |
| **LOS → 텍스처 업로드 + 시간 이징** | 소 | `Texture2D` R/G/B 채널 계약 그대로 |
| **라이트마스크 합성** | 중 | 두 갈래 — §5 참조 |
| **고체 채움 + 굴착 월드 생성** | 중 | 랩 방을 "파낸 구조"로 재작성. `LabEnvironment` 룸 정의 교체 |
| **굴착 시 LOS/표면 부분 갱신** | 소 | `DirtyChunks` 가 이미 있음. LOS 는 `markDirty()` 만 |
| **읽기 하한선 튜닝** | 소 | 너무 어두우면 코어키퍼처럼 "왜 안 보이냐" 민원. `losExplored 0.29` 가 그 하한선 역할 |

### 4-4. 아트 요청서에 생기는 변화

- **미탐색 고체용 아트는 필요 없다.** 검정으로 덮인다 → 벽 상단 변형 추가 발주 중단.
- **경계면 아트의 우선순위가 최상위로 올라간다.** 특히 `TopRim`(굴착 경계의 밝은 테두리)이
  룩의 얼굴이 된다. 지금 절차적 림은 못 쓰는 상태이므로 **이게 1순위 발주**.
- **코너 4방위 문제는 그대로 유효**하고, 오히려 더 중요해진다 —
  굴착 실루엣이 구도를 만드는 구조에서 코너는 항상 시선이 가는 자리다.
- 광원 아트(횃불·플레어·램프)의 **이미시브/글로우 스프라이트**가 새로 필요해질 수 있다.

---

## 5. 구현 갈래 — 어느 쪽으로 갈지 결정 필요

### 갈래 A. URP 2D 네이티브 (앰비언트 죽이기 + 섀도 캐스터 전면 적용)

앰비언트를 0.03 으로 내리고 모든 벽에 `ShadowCaster2D` 를 붙여 광원별로 가린다.

- 장점: 새 셰이더 없음. 기존 `ShadowGeometryBuilder` 재사용.
- 단점: **비용이 광원×캐스터로 증가**(코어키퍼가 굳이 전파를 쓰는 이유). 횃불 수십 개를 못 버틴다.
  그리고 **`explored` 잔상을 표현할 방법이 없다** — 2D 섀도는 "기억"을 모른다.
  탐색한 곳이 지나가면 완전 검정으로 잘려서 코어키퍼 룩이 안 된다.

### 갈래 B. 라이트마스크 이식 (원본 HTML 구조를 그대로)

LOS 를 CPU 로 계산해 `Texture2D` 에 싣고, **전체 화면에 Multiply 로 합성**한다.
`dark` 퍼플 + `litClear` + `saturation` 까지 원본 파라미터를 그대로 가져온다.
URP 2D 의 광원은 "밝은 곳"만 담당하고, "얼마나 드러나는가"는 마스크가 담당한다.

- 장점: **원본과 룩이 동일해진다**(검증된 수치·검증된 셰이더 로직).
  비용이 광원 수와 무관. `explored` 잔상이 자연스럽게 나온다. 퍼플 유지가 구조적으로 보장됨.
  코옵/AI/플레어 시야 합산이 공짜로 따라온다.
- 단점: 전체 화면 합성 단계를 하나 추가해야 한다(URP 2D Renderer Feature 또는 카메라 위 오버레이 쿼드).
  Blend Style 4개와 정렬 레이어 12개 위에 얹히는 순서를 정해야 함.

### 갈래 C. 둘 다 (= 코어키퍼가 실제로 하는 것)

B 를 기반(주항)으로 깔고, 히어로 광원 몇 개에만 A 의 `ShadowCaster2D`(부항)를 남긴다.
코어키퍼의 `Object Shadows: Advanced` 가 정확히 이 부항이고, **끌 수 있는 옵션**이라는 게
우선순위를 알려준다.

**권고: C 로 가되 B 를 먼저 완성한다.**
A 만으로는 코어키퍼 룩에 도달하지 못한다(잔상 불가 + 비용). 그리고 B 는 이미 검증된 코드가 있다.

---

## 6. 체감 변화가 가장 큰 순서 (사용자 요구 반영)

1. **`losRange 19` vs `lampRadius ~2` 비율 도입 + 앰비언트 0.35 → 0.03.**
   이 하나로 화면의 90% 가 바뀐다. 다른 건 안 건드려도 된다.
2. **퍼플을 `dark: #44248F` 로 이전.** 어두워지면서 퍼플이 더 강해진다.
3. **랩 방을 고체 채움 + 굴착 통로로 재작성.** 벽 너머가 "없어야" 어둠이 의미를 가진다.
4. **`explored` 잔상 0.29 + 11타일 페이드.** 여기서 "탐험한 지하"라는 기억감이 붙는다.
5. 경계면 림 아트 발주 → 굴착 경계가 화면의 주인공이 된다.

---

## 7. 남는 결정 사항

- **갈래 B 의 합성 위치**: URP 2D Renderer Feature(`AfterRenderingPostProcessing`) vs
  카메라 자식 오버레이 쿼드. 정렬 레이어 12개·블렌드 스타일 4개와의 순서 계약을 정해야 한다.
- **LOS 해상도**: 원본은 타일 1:1 + 마스크 0.65 스케일. 유니티에서도 같이 갈지.
- **읽기 하한선**: `losExplored 0.29` 를 그대로 쓸지, 유니티 톤매핑 차이를 감안해 재튜닝할지.
- **코너 4방위 아트**: 발주할지(§4-4).
- **`AtmosphereDirector`**: 포그·비네트를 어디까지 걷어낼지.

---

## 8. 부록 — 코어키퍼의 벽 리소스 구조 (타일셋 청사진 실측)

모딩 툴체인이 `PugMapTileset` 에셋을 덤프해 만든 청사진 JSON(`tileset_main_*.json`)을
직접 읽어 확인했다. 타일 셀은 **16px**.

### 8-1. 벽 하나를 구성하는 레이어 전체

| 레이어 | `mesh_fill_type` | `lookup_kind` | 이웃 비트 | 아틀라스 | 실제 셀 |
|---|---|---|---|---|---|
| `wall` (상단면) | **AdaptativeFill** | GeneratedTexture | `0xFF` 8방위 전체 | 16×20 | **315** |
| `wallFront` (남향 정면) | **AdaptativeExtrude** | NineWay | `0x11` **동·서만** | 6×2 | **6** |
| `wallTopShadowCaster` | AdaptativeExtrude | NineWay | `0x00` 없음 | 1×1 | **1** |
| `wallCrackFront` | AdaptativeExtrude | — | — | — | 파괴 균열(정면) |
| `oreFront` | AdaptativeExtrude | — | — | — | 광맥(정면) |
| `ground` | AdaptativeFill | GeneratedTexture | `0xFF` | 16×20 | 315 |
| `groundFront` | AdaptativeExtrude | NineWay | `0x00` | 6×1 | 6 |

`tileset_main` 전체 레이어: `ground` `groundFront` `dugupGround` `groundSlime` `wall`
`wallFront` `wallCrackFront` `wallTopShadowCaster` `oreFront` `water` `waterFront`
`wateredGround` `bigRoot` `bigRootIndirectLight` `bigRootShadow` `smallGrass`
`smallGrassStraws` `sunbeam` `roofhole` `ancientCrystalFront` `chrysalis`.
`tileset_base_building` 에는 `wallColorShadow` `thinWall` `thinWallFront`
`thinWallColorShadow` `litFloor` `litFloorEmissive` `fenceIndirectLight` `fenceShadow` 등이 더 있다.

### 8-2. 읽어야 할 것

1. **`AdaptativeFill` = 평면 내 타일링 / `AdaptativeExtrude` = 평면 밖으로 압출.**
   상단면은 격자에 깔리고, 정면은 셀의 남쪽 경계에서 아래로 압출된 수직 띠다.
   **높이는 아트가 아니라 압출 파라미터다.**
2. **아트 노동의 95% 가 상단면에 있다** — 315셀 대 6셀. 형태·경계·코너 문제는 전부 상단면
   오토타일 안에서 해결된다. 각 셀은 `borders`(top/right/bottom/left, 16조합)와
   `inner_corners`(ne/se/sw/nw)로 기술된다.
3. **정면은 거의 무료다(6셀).** 동·서 이웃만 알면 되기 때문 — 수직 띠는 가로로 이어지기만 하면
   되고 남북 이웃은 정면 모양에 영향을 주지 않는다.
4. **서·동 측면 레이어가 없다.** 엄격한 탑다운에서 남향 정면 하나만 보이므로 그릴 필요가 없다.
5. **코너 전용 레이어가 없다.** 상단면 오토타일의 메타데이터로 처리된다.
6. **섀도캐스터는 아트가 아니라 1셀 지오메트리다.** 벽 높이가 바닥에 드리우는 그림자를
   실루엣 하나를 압출해 처리한다.
7. **17타일 블롭은 저작 포맷, 런타임은 315셀.** 아티스트가 ~17장을 그리면 툴이 확장한다.
   사람이 315장을 그리지 않는다. (커뮤니티도 "adaptive texture 생성 방식은 개발사만 안다"고 명시)
8. **광맥·균열이 정면에만 붙는다** — 플레이어가 마주 보는 수직면에 정보를 얹는 가독성 결정.
9. `bigRootIndirectLight` · `fenceIndirectLight` 처럼 **간접광 기여를 별도 레이어로 저작**한다.
   §1-2 의 "간접광이 주항"이라는 패치노트와 일치한다.

### 8-3. 우리 작업에 대한 판정

우리의 면 분해 방향 자체는 맞았다. 다만 **과투자했다.**

| 우리가 만든 것 | 코어키퍼 | 판정 |
|---|---|---|
| cap 3종 (`WallTop`) | `wall` 315셀 | 방향 일치. 변형 수는 오토타일 확장으로 해결할 문제 |
| 정면 3종 (`FrontFace`) | `wallFront` 6셀 | **일치** |
| 상단 림 (`TopRim`) | `wall` 아틀라스의 `borders` 에 내장 | 별 레이어로 둔 것은 우리 선택. 유효 |
| **서·동 측면 2종** | **없음** | **불필요** |
| **코너 외곽·내곽 2종** | **없음** (상단면 메타데이터) | **불필요. 4방위 발주 취소** |
| 접촉 AO 4방위 | 상단면 `borders` + 간접광 레이어 | 유효 |
| `ShadowGeometryBuilder` 윤곽 | `wallTopShadowCaster` 1셀 압출 | 방향 일치, 우리가 더 복잡함 |

## 참고 출처

- [Core Keeper 0.5.2.0-b84a 패치노트 (위키)](https://core-keeper.fandom.com/wiki/0.5.2.0-b84a) — 신규 렌더 파이프라인, 간접광 재작업, 광원별 오브젝트 섀도, Object Shadows 설정
- [Core Keeper 0.5.2.4 패치노트 (Steam)](https://store.steampowered.com/news/app/1621690/view/3677788723132770926) — 섀도 업데이트 큐, Light Quality, 톤매핑·블룸 임계값
- [Core Keeper 0.5.2.4-9120 (위키)](https://core-keeper.fandom.com/wiki/0.5.2.4-9120)
- [Light sources (Core Keeper 위키)](https://core-keeper.fandom.com/wiki/Light_sources) — 광원 종류, 글로우 강화
- [Core Keeper 모딩 위키 — 기술 스택](https://core-keeper-modding.gitbook.io/modding-wiki/concepts/technologies-and-tools) — Unity DOTS/ECS/Burst
- [Pugstorm/CoreKeeperModSDK](https://github.com/Pugstorm/CoreKeeperModSDK)
- [always dark? (Steam 토론)](https://steamcommunity.com/app/1621690/discussions/0/7098294290806458126/) — 어둠이 설계 의도라는 커뮤니티 합의
- [germanoeich/CoreKeeperTilesetGenerator](https://github.com/germanoeich/CoreKeeperTilesetGenerator) — `PugMapTileset` 덤프 기반 청사진. §8 의 레이어·마스크·아틀라스 수치는 `Editor/Templates/Blueprints/tileset_main_*.json` 실측
- [Core Keeper 모딩 문서 — 타일](https://github.com/CoreKeeperMods/Core-Keeper-Docs/blob/main/creating-mods/modding-examples/placeables/tiles.md) — 타일셋·Adaptive Textures dict, `tileset_main`/`base_building`/`extras` 구분
- [CoreLib.Tilesets](https://mod.io/g/corekeeper/m/corelibtilesets)
- 사내: `docs/tunnel-crew-gdd.md:114`, `build/TunnelCrew-v7.9.2/tunnel-crew-infinite-mode-v7.9.2.html` (`LOS` L5164~, `LX.lightmap` L1377, `FOW` L1448~)
