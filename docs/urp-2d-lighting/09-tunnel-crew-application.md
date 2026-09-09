# 09. 땅굴 크루 적용 — 현재 상태 진단과 적용 후보

작성 기준: 2026-09-09 · `unity/TunnelCrew` (Unity 6000.3.15f1 / URP 17.3.0)
연계 문서: [../tunnel-crew-lighting-lx.md](../tunnel-crew-lighting-lx.md), [../unity-visual-overhaul-functional-spec.md](../unity-visual-overhaul-functional-spec.md), [../unity-port/visual-overhaul-implementation.md](../unity-port/visual-overhaul-implementation.md), [../2d-lighting-reference-games-research.md](../2d-lighting-reference-games-research.md)

## A. 이미 갖춰진 것 (세션 내용과 일치)

| 항목 | 현재 상태 |
| --- | --- |
| 2D Renderer | `Assets/Settings/Renderer2D.asset` 존재. 2D (URP) 구성 |
| Blend Style 4슬롯 | `Multiply` / `Additive` / `Multiply with Mask` / `Additive with Mask` — 세션의 샘플 구성과 동일한 골격 |
| Light Render Texture Scale | `0.5` — 세션 권장값과 동일 |
| Max Light Render Textures | `16` — 세션 슬라이드와 동일 |
| Max Shadow Render Textures | `1` — 세션 슬라이드와 동일 |
| 노멀맵 | `Assets/Art/**/*_n.png` 269장. 스프라이트 메타에 `_NormalMap` **Secondary Texture 로 이미 연결됨** (`purple_walls_atlas`, 캐릭터 시트 등) |
| 라이트 분류 체계 | `LightClass`(Scout/Worklamp/MineralGlow/Combat/Indicator) + `LightNormalQuality` + 그림자 예산 규칙 — 세션에 없는 우리 고유 자산이고, 세션의 "성능/아트/게임플레이 균형" 원칙을 이미 코드로 강제하고 있다 |
| ShadowCaster2D | `WallShadowBuilder` 가 벽/액터 캐스터 + `CompositeShadowCaster2D` 를 구성 (URP 17 internal API 를 리플렉션으로 우회) |
| Soft shadow | `shadowSoftness = 0.35f` 사용 중 — 세션이 "신기능"으로 예고한 것을 이미 쓰고 있다 |

→ **세션의 1~3장(세팅·노멀맵)은 대체로 이행된 상태다.** 격차는 마스크맵과 그림자 연출 쪽에 있다.

## B. 격차 — 세션에 있고 우리에겐 아직 없는 것

### B-1. 마스크맵 채널 라이팅 (가장 큰 격차)

- 마스크 PNG는 있다 (`tr01_atlas_floor_mask.png`, `tr01_arch_gate_a_mask.png`, `tc_ph_channel_mask.png` 등).
- 그런데 **머티리얼 프로퍼티(`_MaskTex`, `SurfaceMaterialSet`)로만 쓰고 있고, 스프라이트 Secondary Texture 로는 붙어 있지 않다.** 코드에 `blendStyleIndex` 사용이 전혀 없다 = **"with Mask" blend style 을 아무 라이트도 안 쓴다.**
- 즉 Renderer2D에 마스크 슬롯 2개를 만들어 놓고 **비어 있는 상태**다.
- 또 현재 두 마스크 슬롯이 **둘 다 `maskTextureChannel: 1`(= R)** 이다. 세션 방식(캐릭터 G / 소품 R)으로 쓰려면 `Multiply with Mask` 를 **G(=2)** 로 바꿔야 채널 분리가 성립한다.

적용 후보:
- **크루 실루엣 림 라이트** — 어두운 땅굴에서 크루 4인의 가독성. 세션이 명시한 1번 용도 그대로. `Multiply with Mask (G)` + 캐릭터 마스크맵.
- **광물·수정 발광** — `MineralGlow` 분류가 이미 "Emission 중심, 저비용 비그림자"로 정의되어 있다. `Additive with Mask (R)` 로 광맥만 발광시키면 라이트 하나로 화면 전체 광맥을 처리할 수 있다(픽셀 단위 마스킹이므로).
- **기계 표시등** — `Indicator` 분류도 마스크 채널로 처리하면 라이트 수를 줄일 수 있다.

### B-2. 네거티브 라이팅 (Multiply freeform)

- 현재 그림자는 `ShadowCaster2D` 기반(실제 오클루전)뿐이다. **아티스트가 형태를 직접 그리는 어두운 영역이 없다.**
- 땅굴 크루에 잘 맞는 자리: 천장 낮은 구간, 갱도 입구 안쪽, 구조물 뒤, 파괴되지 않은 벽 그림자 — 성능 비용이 거의 없고 분위기가 크게 오른다.
- 설정: `Freeform` + `Multiply` + Overlap `Alpha Blend` + Light Order 를 일반 광원 위로.
- 세션의 성능 §4와도 맞는다 — **섀도우 캐스팅 라이트 수를 줄이는 대체 수단**.

### B-3. 블롭 섀도우 (Sprite 라이트)

- 현재 액터 그림자는 `WallShadowBuilder.AttachActorCaster` 로 캐스터를 붙인다(정확하지만 비싸다).
- 세션 방식의 `Sprite` 타입 Light 2D + 흐린 원 + Multiply 는 **몹·아이템·잔해처럼 개수가 많은 것들**에 훨씬 싸다.
- 후보: 몹 다수, 드롭 아이템, 파편, 설치물. 크루/보스만 진짜 캐스터를 유지.

### B-4. 하루 주기 / 시간축 오케스트레이션

- 땅굴 크루는 지하라 낮/밤이 없다. **그대로 옮길 필요는 없다.**
- 대신 세션의 **"단일 ratio 하나가 색·각도·길이·셰이더 전역을 전부 구동"** 구조는 그대로 쓸 값이 있다.
  - 후보 축: **층 깊이(depth)** — 깊어질수록 앰비언트 색/세기, 안개, 그림자 대비를 Gradient/AnimationCurve로 authoring.
  - 후보 축: **위기 단계(경보/보스 페이즈)** — 색 그라디언트 하나로 전체 무드 전환.
- `Test Time` / `Preview Time` 같은 **에디터 스크럽 필드는 반드시 넣는다.** 우리는 이미 F10 LX 패널이 있으니 거기에 붙이는 게 자연스럽다.

### B-5. 2D Light Texture Shader Graph 노드

- 라이팅 결과 텍스처(`LightTex0`~`3`)를 셰이더에서 직접 읽는다.
- 후보: 어둠 속 실루엣 강조, 광원 색 기반 표면 틴트, 라이팅 값에 반응하는 안개.

## C. 환경 세팅 시 순서 (권장)

1. **Blend Style 슬롯 확정** — `Multiply with Mask` 의 채널을 R→G 로 변경할지 결정. 결정하면 그 뒤 마스크맵 authoring 규약이 고정된다. ([01](01-project-setup.md))
2. **마스크맵 채널 규약 문서화** — 어떤 채널에 무엇을 넣을지(G=캐릭터 림, R=발광 프롭). 아트 파이프라인(`art-production/`)에 반영. ([03](03-mask-maps.md))
3. **크루 림 라이트 1개 프로토타입** — VisualLab 씬에서 마스크 라이팅 실증. ([03](03-mask-maps.md))
4. **네거티브 라이팅 freeform 그림자** — 테스트 방에 1~2개 배치해 룩 확인. ([05](05-shadows.md) §1)
5. **블롭 섀도우로 몹/아이템 그림자 교체** 후 프레임 비교. ([05](05-shadows.md) §2)
6. **깊이/위기 축 오케스트레이터** 설계 — Gradient + AnimationCurve + 스크럽 필드. ([06](06-day-night-cycle.md))
7. **성능 체크리스트 통과 확인.** ([07](07-performance.md))

## D. 주의 — 우리 프로젝트 고유 제약과 충돌하는 지점

- **씬 텍스처 읽기 금지 규약**(LX 분기: `file://` 오염 문제)이 있다. 2D Light Texture 노드를 쓸 때 같은 문제를 다시 만들지 않는지 먼저 확인한다.
- URP 17.3의 `ShadowCaster2D` 형태 주입 API가 internal 이라 리플렉션으로 우회 중이다. 새 그림자 기법을 넣을 때 **리플렉션을 더 늘리지 말고** freeform 라이트/블롭처럼 public API 로 되는 쪽을 우선한다.
- `Light2D.normalMapQuality` / `normalMapDistance` 는 URP 17에서 읽기 전용이라 직렬화 필드에 직접 쓰고 있다(`RunBootstrap` 주석). 마스크 라이트를 추가할 때 `blendStyleIndex` 도 동일한 제약이 있는지 먼저 확인한다.
