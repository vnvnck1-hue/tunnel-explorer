# Material Mask 채널 규약 — 확정 (2026-09-09)

이 문서가 **마스크 채널의 정답지**다. 아트 authoring · 셰이더 · `Renderer2D` 블렌드 스타일이
모두 여기를 따른다. 바꾸려면 이 문서를 먼저 고치고, 그 뒤 세 곳을 함께 옮긴다.

## 0. 한 줄

| 채널 | 담는 것 | 소비하는 Blend Style | 용도 |
|---|---|---|---|
| **R** | 금속 | (예약 — 아직 라이트 없음) | 금속 전용 반응(후속) |
| **G** | 광택 | **`Multiply with Mask`** (슬롯 2) | 크루 실루엣 림 · 금속 모서리 광택 |
| **B** | 습윤 · 결정 | **`Additive with Mask`** (슬롯 3) | 광맥·수정 발광 |
| **A** | 효과 강도 | (셰이더 `surfaceData.mask.a`) | 위 반응의 세기 |

`Renderer2D.asset` 실제 값 (`TextureChannel`: None 0 · R 1 · G 2 · B 3 · A 4):

```
- name: Multiply         maskTextureChannel: 0  blendMode: 1   (마스크 없음 — 일반 광원 전부)
- name: Additive         maskTextureChannel: 0  blendMode: 0
- name: Multiply with Mask  maskTextureChannel: 2  blendMode: 1   ← G
- name: Additive with Mask  maskTextureChannel: 3  blendMode: 0   ← B
```

## 1. 왜 이 배치인가

**세션 기본안(캐릭터 G / 프롭 R)을 그대로 쓰지 않았다.** URP 2D 라이팅 세션(GDC 2023)은
`Multiply with Mask`=G, `Additive with Mask`=R 을 예로 들지만, 우리에게는 이미 두 개의
선행 계약이 있다.

1. **아트 계약이 채널 의미를 이미 정했다** — `R 금속 · G 광택 · B 습윤/결정 · A 효과 강도`
   (`docs/codex-art-request-reference-wall-set.md` §5). 승인 마스크 **46장**이 이 규약으로
   그려져 있고, 실측하면 R·G·B 세 채널에 모두 실데이터가 있다(0~255).
   세션 기본안을 따르면 이 46장을 다시 그려야 한다.
2. **우리 스타일 분석이 이미 B→Additive 를 지목했다** —
   `art-production/test-room-v01/process/primary-style-target-analysis.md`:
   "수정·젖은 광물은 별도 Material Mask 로 Additive 반응을 선택한다."

그래서 **의미를 유지하고 슬롯을 의미에 맞춰 옮겼다**. 결과가 세션의 의도(하나는 Multiply,
하나는 Additive)와 같고, 기존 아트를 한 장도 버리지 않는다.

**고친 것은 결함이었다** — 두 마스크 슬롯이 **둘 다 R(1)** 이었다. 채널이 같으면
"금속에만 곱하는 빛"과 "금속에만 더하는 빛"이 되어 광택·결정 용도가 아예 성립하지 않는다.
게다가 코드에서 `blendStyleIndex` 를 쓰는 곳이 0건이라, 슬롯 두 개를 만들어 놓고
**아무 라이트도 쓰지 않는 상태**였다(리서치 §B-1 "가장 큰 격차").

## 2. 아트가 지켜야 하는 것

- 마스크는 **Linear**(sRGB 끔) · 비압축. 임포트 규약은 `ImportSettingsContract` 가 강제한다.
- **G(광택)**: 빛을 받았을 때 반짝여야 하는 곳만. 크루는 실루엣 가장자리(림), 프롭은 금속 모서리.
  전면을 채우면 캐릭터가 통째로 번쩍인다 — **가장자리 2~4px 대역**이 기준이다.
- **B(습윤·결정)**: 스스로 빛나야 하는 광맥·수정·젖은 표면. Emission 채널과 역할이 다르다 —
  Emission 은 "항상 빛난다", B 는 "광물광(`MineralGlow`) 라이트가 닿을 때 빛난다".
  라이트 하나로 화면 전체 광맥을 켜고 끌 수 있는 것이 이 채널의 값이다.
- **R(금속)**: 계속 그려 둔다. 소비하는 라이트는 아직 없다.
- **A(효과 강도)**: 위 반응의 세기. 비우면 1 로 본다.

## 3. 코드가 지켜야 하는 것

- 마스크 반응을 원하는 `Light2D` 는 `blendStyleIndex` 를 **2(광택) 또는 3(결정)** 으로 둔다.
  일반 광원은 0(Multiply) 을 쓴다 — 지금 전 광원이 그렇다.
- 스프라이트가 마스크를 라이트에 넘기는 경로는 **머티리얼 `_MaskTex` → `surfaceData.mask`** 다
  (`TunnelCrewLit2D.hlsl` 의 `TCSampleChannels`). URP 의 Secondary Texture 슬롯은 쓰지 않는다 —
  우리는 채널을 셰이더가 직접 샘플해 `InitializeSurfaceData` 로 넘긴다.
- `LightClass` 와의 대응: `MineralGlow` → 슬롯 3, 크루 림 라이트(신설 예정) → 슬롯 2.
  `Scout`/`Worklamp`/`Combat`/`Indicator` 는 슬롯 0 을 유지한다.

## 4. 회귀 고정

`Tests/EditMode/MaskChannelConventionTests.cs` 가 `Renderer2D.asset` 의 네 슬롯을 직접 읽어
위 표와 같은지 확인한다. 슬롯이 바뀌면 테스트가 먼저 깨진다.
