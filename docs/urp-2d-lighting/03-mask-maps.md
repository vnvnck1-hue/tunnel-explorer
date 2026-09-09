# 03. 마스크맵 (Mask Maps)

원본 구간 6:58–10:30 (슬라이드 19~24) · 챕터명 *"What are Mask maps in 2D"*

## 1. 원리 (슬라이드 19 원문)

> *"Mask maps are used by the 2D lights blending styles. The light blending style takes a light's value at a given pixel and multiplies that value by the mask at the same pixel. The resulting masked light value is then added, subtracted, or multiplied by the color at that pixel, based on which blending style is chosen."*

계산 순서:

```
masked_light = light_value(pixel) * mask(pixel, 지정채널)
final_color  = blend( sprite_color(pixel), masked_light )     // blend = Additive | Multiply | Subtractive
```

슬라이드의 그림 4단:
`2D Light`(부드러운 광점) → `Mask (Red channel)`(스프라이트 형태의 마스크) → `Masked Light Value`(마스크로 잘린 빛) → 최종 결과(검을 든 캐릭터에 검만 청색으로 빛남).

**핵심: 마스크맵은 "빛의 밝기"가 아니라 "빛이 통과할 자격"이다.** 라이트를 화면 전체에 깔아도 마스크가 0인 픽셀에는 아무 영향이 없다.

## 2. 채널을 나눠 용도를 분리한다

Renderer 2D Data의 blend style 슬롯마다 mask channel을 다르게 잡아서, **하나의 마스크 텍스처에 여러 용도를 채널로 패킹**한다. 샘플 게임 구성:

| 용도 | Blend Style | 마스크 채널 | 슬라이드 |
| --- | --- | --- | --- |
| 플레이어 캐릭터 | `Multiply with Mask (G)` | **G** | 20 |
| 환경 · 소품 (프롭) | `Additive with Mask (R)` | **R** | 21 |

슬라이드 20 원문: *"We set up some lights that only target the mask maps and the channel G used for the character."*
슬라이드 21 원문: *"We also set up some lights that only target the mask maps and the channel R used for props."*

→ 즉 **"캐릭터만 건드리는 라이트"와 "소품만 건드리는 라이트"를 채널로 완전히 격리**한다. Target Sorting Layers(레이어 단위)보다 훨씬 세밀하다.

### 슬라이드에 노출된 Light 2D 실제 값 (양쪽 공통 골격)

| 필드 | 캐릭터용 (슬라이드 20) | 소품용 (슬라이드 21) |
| --- | --- | --- |
| Light Type | Spot | Spot |
| Intensity | 1.57 | 1.57 |
| Radius Inner / Outer | 31.32 / 43.59 | 31.32 / 43.59 |
| Inner / Outer Spot Angle | 360 / 360 | 360 / 360 |
| Falloff Strength | 0 | 0 |
| Target Sorting Layers | Mixed… | Mixed… |
| **Blend Style** | **Multiply with Mask (G)** | **Additive with Mask (R)** |
| Light Order | 0 | 0 |
| Overlap Operation | Additive | Additive |
| Shadows → Strength | 0.75 | 0.75 |
| Normal Maps Quality / Distance | Accurate / 20.31 | Accurate / 20.31 |

## 3. 마스크맵으로 실제로 뭘 하나 (슬라이드 19 하단 3줄)

> - *"2D lights have the option to only affect mask maps. For example, they can be used to only affect the light information in the mask map texture."*
> - *"For readability purposes, characters often have a rim light around the silhouette as they move."*
> - *"We use mask maps to create immersion and polish, making the game look more distinctive."*

정리하면 세 가지 목적:

1. **가독성(readability)** — 캐릭터 실루엣 테두리에 **림 라이트**. 슬라이드의 형광 초록 실루엣 이미지가 바로 마스크맵(캐릭터 외곽선만 흰색)이다. 배경이 복잡해도 캐릭터가 뜬다.
2. **강조/연출** — 검·랜턴·크리스탈처럼 "이 부분만 발광" 처리.
3. **폴리시** — 게임 룩의 개성. 같은 아트여도 마스크 라이팅 유무로 인상이 크게 달라진다.

## 4. 임포트 설정 (슬라이드 22)

> *"Mask maps should be imported as Texture Type: Default. This asset should be a Default texture type for optimal sprite atlasing."*

Inspector (예: `Hero_mask 1`):

| 필드 | 값 |
| --- | --- |
| Texture Type | **Default** |
| Texture Shape | 2D |
| sRGB (Color Texture) | ✔ |
| Alpha Source | Input Texture Alpha |
| Alpha Is Transparency | ✔ |

노멀맵과 **정반대**라는 걸 기억한다. (노멀맵 = Normal map, 마스크맵 = Default)
이유도 동일하게 아틀라싱 최적화. 발표자 표현: 잘못 넣으면 *"chugging"*(버벅임)이 생긴다.

## 5. 결과 (슬라이드 23 *"Normal maps and mask maps in action"*)

두 스크린샷 비교로 마무리한다. 발표자 코멘트: *"it's got depth to it and again it makes it seem that much more…"* — 노멀맵(입체감) + 마스크맵(선택적 발광/림)이 함께 걸릴 때 톱다운 2D가 3D처럼 읽힌다.

## 6. 균형 (슬라이드 25 *"A balancing act: Performance / art / gameplay"*)

챕터 10:30 *"Performance/art/gameplay considerations"*.
- 라이팅은 **성능 · 아트 · 게임플레이 3자 균형** 문제다. 예쁘지만 10fps면 의미가 없다("the most beautiful thing if it runs at 10 frames a second").
- 참고로 제시된 다른 2D 라이팅 데모(둘 다 Unity Asset Store에서 받을 수 있음):
  - **Lost Crypt**
  - **Dragon Crashers**
