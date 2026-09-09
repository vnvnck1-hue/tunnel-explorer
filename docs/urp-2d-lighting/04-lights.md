# 04. 라이트 구성 — 앰비언트 / 태양광 / 입체감

원본 구간 11:10–13:02 (슬라이드 26~28)

샘플 게임의 라이팅은 계층이 명확하다.

```
[1] Global 앰비언트 라이트   — 씬 전체 기본 밝기 + 무드 틴트 (완전 암부 방지)
[2] 큰 Spot "태양광"         — 카메라에 부착, 스크립트로 회전. 방향성/시간대 담당
[3] 오브젝트 단위 Spot/Point — 노멀맵 반응용. 입체감 담당
[4] 마스크 채널 라이트       — 캐릭터 림 / 소품 강조 (03 문서)
[5] Multiply 라이트          — 그림자 (05 문서)
```

## 1. 앰비언트 라이팅 (슬라이드 26, 챕터 11:10)

> *"Global lights affect the whole scene, making it easy to change the mood of the world. It's used in the demo to avoid complete dark areas and apply a general tint."*

Light 2D 실제 값:

| 필드 | 값 |
| --- | --- |
| Light Type | **Global** |
| Intensity | **0.25** |
| Target Sorting Layers | **Everything** |
| Blend Style | **Multiply** |
| Light Order | 0 |
| Overlap Operation | Additive |

포인트:
- **낮은 intensity(0.25)** 로 깔아 "완전한 검정"을 없앤다. 발표자: *"they've put in an ambient light with a sort of low intensity"*.
- Blend Style이 `Multiply` 라는 게 중요하다. Additive로 깔면 전체가 하얗게 들뜨고, Multiply면 **원래 아트 색을 유지하면서 어둡게/틴트**된다.
- 밤/낮 무드 전환의 1차 손잡이가 이 라이트의 Color다. → [06-day-night-cycle.md](06-day-night-cycle.md)
- Global 라이트는 노멀맵에 반응하지 않는다. 입체감은 [3]이 담당한다.

## 2. 2D의 "태양광" = 거대한 스팟 라이트 (슬라이드 27, 챕터 12:00)

슬라이드 제목: *"Sunlight in 2D, a.k.a. 'directional light'"*

> *"A large spot light is used in the demo as the key light. This light is attached to the camera and rotates with a script simulating the movement of the sun."*

URP 2D에는 **directional light가 없다.** 그래서:

1. **아주 큰 반경의 Spot(Point) 라이트**를 하나 만든다 (슬라이드의 씬 뷰에 씬을 다 덮는 거대한 원이 보인다).
2. 그 라이트를 **Main Camera의 자식으로 붙인다** → 카메라가 어디로 가도 "태양"이 항상 화면을 덮는다. 씬 크기와 무관하게 라이트 1개로 해결.
3. 스크립트가 이 라이트를 **회전/이동**시켜 태양의 이동을 모방한다.
4. 이 라이트가 **키 라이트(key light)** 다. 나머지는 보조.

발표자 표현: *"had to be a little more creative, so what they did was they created…"* — 즉 2D에 directional이 없다는 제약을 우회한 트릭이다.

> 실무 메모: 카메라 자식으로 두면 라이트가 씬 좌표에 고정되지 않으므로 "특정 장소만 밝다" 같은 표현은 안 된다. 장소 단위 표현은 별도 Spot/Freeform 라이트로 배치한다.

## 3. 노멀맵으로 입체감 만들기 (슬라이드 28, 챕터 12:29)

슬라이드 제목: *"Simulating depth with 2D lights"*

> *"We use lights and normal maps everywhere to create the illusion of volume and give the demo a unique look and feel. You can use normal maps with spot, point, and freeform lights."*

슬라이드의 before/after: 같은 덤불 스프라이트가 노멀맵 반응 없이는 평평하고, 켜면 위쪽 면이 밝고 아래가 어두운 **볼륨**이 생긴다.

노출된 Light 2D 값:

| 필드 | 값 |
| --- | --- |
| Light Type | Spot |
| Intensity | **3.86** |
| Radius Inner / Outer | 0 / **11.79** |
| Inner / Outer Spot Angle | 240 / 360 |
| Falloff Strength | 0.229 |
| Target Sorting Layers | **Objects** |
| Blend Style | Multiply |
| Overlap Operation | Additive |
| **Normal Maps → Quality** | **Accurate** |
| **Normal Maps → Distance** | **1.28** |

포인트:
- Intensity가 3.86 — **1을 훌쩍 넘겨 쓴다.** 노멀맵 반응을 눈에 보이게 하려면 세게 때린다.
- `Target Sorting Layers = Objects` — 바닥/배경이 아니라 **오브젝트 레이어만** 이 라이트를 받는다. 레이어 분리로 "지면은 평평, 오브젝트만 입체"를 만든다.
- `Normal Maps Distance = 1.28` (작다) vs 캐릭터 마스크 라이트의 `20.31` (크다). Distance는 라이트가 표면에서 떠 있는 느낌 → 작으면 그레이징(스치는) 광, 크면 정면광에 가깝다.
- 발표자: *"was to make sure to set uh normal maps on your lights"* — **라이트 쪽 스위치를 켜는 걸 잊지 말라**는 게 이 슬라이드의 실질적 교훈.

## 4. 부가 효과 아이디어 (슬라이드 37, 챕터 16:45 *"More light and shadow possibilities"*)

슬라이드 제목: *"Add effects for greater immersion — There are a few easy effects yet to be implemented, some that we plan to include in the demo:"*

- **Clouds shadows** (구름 그림자)
- **Shaft of lights** (광선/빛기둥)
- **Dust particles** (먼지 입자)
- **A fireplace** (모닥불)
- **Water refraction** (물 굴절)

발표자 결론: *"always be thinking about adding more visual effects to your…"* — 라이팅 시스템이 깔리면 이런 것들이 싸게 붙는다.
