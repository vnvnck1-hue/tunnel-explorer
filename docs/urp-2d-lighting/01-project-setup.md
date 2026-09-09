# 01. 프로젝트 / 렌더러 세팅

원본 구간 2:05–4:10 (슬라이드 6~10)

## 1. 처음부터 2D (URP) 템플릿으로 시작한다

슬라이드 원문: *"Start new 2D projects with the 2D (URP) Template — This template has everything you need preconfigured for 2D dev."*

- New Project 창의 템플릿 목록에서 **2D (URP)** 를 고른다. Universal Render Pipeline + **2D Renderer** 가 미리 연결된 빈 프로젝트다.
- 발표자가 강조한 이유: 나중에 갈아끼우면 **머티리얼/셰이더/라이트 설정을 전부 다시 잡아야 한다.** ("the same value or they will have to go back in and change stuff later")
- 즉 라이팅을 쓸 계획이 조금이라도 있으면 프로젝트 생성 시점에 결정한다.

## 2. Renderer 2D Data (2D Renderer Asset)

2D 라이팅의 전역 설정은 전부 `Renderer2D` 에셋에 있다. 세션에서 직접 언급/노출된 항목:

| 항목 | 역할 | 샘플에서 본 값 |
| --- | --- | --- |
| **Light Blend Styles** (최대 4슬롯) | 라이트가 스프라이트에 합성되는 방식. Light 2D의 `Blend Style` 드롭다운이 이 목록을 그대로 참조한다 | Multiply / Additive / Multiply with Mask / Additive with Mask |
| **Render Scale (Light Render Texture Scale)** | 라이팅을 그릴 텍스처 해상도 배율. 낮추면 픽셀이 줄어 성능이 좋아진다 | `0.5` |
| **Max Light Render Textures** | 동시에 쓸 수 있는 라이트 렌더 텍스처 수 | `16` |
| **Max Shadow Render Textures** | 섀도우용 렌더 텍스처 수 | `1` |
| HDR Emulation Scale | 밝기 오버플로 표현 범위 | (기본 1) |

### Blend Style 슬롯은 "설계"다

- 슬롯이 **4개뿐**이므로, 프로젝트 초반에 "우리 게임은 이 4개를 이렇게 쓴다"를 정해두는 것이 사실상 라이팅 아키텍처다.
- 슬롯마다 두 가지를 정한다.
  - **Blend Mode** — `Additive`(빛 더하기) / `Multiply`(어둡게, 곱하기) / `Subtractive`(빼기)
  - **Mask Texture Channel** — `None / R / G / B / A` (+ `OneMinus*` 반전 계열). 여기서 지정한 채널이 스프라이트 마스크맵에서 읽히는 채널이다. → [03-mask-maps.md](03-mask-maps.md)
- 샘플 게임 구성이 사실상 표준 조합이다.
  - `Multiply` — 그림자/어둠 담당 (네거티브 라이팅)
  - `Additive` — 일반 광원 담당
  - `Multiply with Mask (G)` — 캐릭터 전용
  - `Additive with Mask (R)` — 환경·소품 전용

> 성능 노트: **화면에 동시에 보이는 blend style 종류를 줄여라.** blend style을 바꿔 그릴 때 드로우 전환 비용이 있다. → [07-performance.md](07-performance.md)

## 3. Light 2D의 공통 파라미터 (세션 내내 반복 노출)

| 파라미터 | 메모 |
| --- | --- |
| Light Type | `Freeform` / `Sprite` / `Point`(=Spot) / `Global` (구 `Parametric`은 deprecated) |
| Color / Intensity | Intensity는 1을 넘길 수 있다 (샘플에 1.57, 3.86, 3.92 등) |
| Radius Inner / Outer | Point 라이트의 감쇠 범위 |
| Inner / Outer Spot Angle | 360/360 이면 원형, 좁히면 스포트 |
| Falloff / Falloff Strength | 경계 부드러움 |
| **Target Sorting Layers** | 이 라이트가 영향을 줄 Sorting Layer. `Everything` / `Mixed…` / 개별 선택. **라이팅 분리의 1차 수단** |
| **Blend Style** | 위 Renderer 2D 슬롯 중 하나 |
| **Light Order** | 같은 blend style 안에서의 합성 순서 |
| **Overlap Operation** | `Additive` / `Alpha Blend`. 그림자 라이트는 대체로 `Alpha Blend` |
| Shadows → Strength | 이 라이트가 만드는 섀도우 강도 (샘플 0.75) |
| Volumetric | 광선/볼륨 느낌 |
| **Normal Maps → Quality / Distance** | `Disabled` / `Fast` / `Accurate`. 켜야 노멀맵이 반응한다. Distance는 "라이트가 표면에서 얼마나 떠 있나" |

## 관련
- 우리 프로젝트의 현재 Renderer2D 설정 진단 → [09-tunnel-crew-application.md](09-tunnel-crew-application.md)
