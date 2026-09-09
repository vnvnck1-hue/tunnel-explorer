# 02. 2D 노멀맵

원본 구간 4:10–6:58 (슬라이드 9~18) · 챕터명 *"Different ways to create Normal Maps for 2D"*

세션의 주장: **톱다운 2D에서 "고급스러움"을 만드는 건 라이트 개수가 아니라 노멀맵이다.**
라이트가 움직일 때 표면이 반응하지 않으면 아무리 라이트를 깔아도 스티커처럼 보인다.

## 1. 노멀맵은 Secondary Texture 로 붙인다

슬라이드 원문: *"Secondary textures: Where the magic happens — Look for this tab inside the Sprite Editor. Any 2D object can use them."*

- 붙이는 곳: **Sprite Editor → Secondary Textures** 탭.
- 슬롯 이름 규칙: `_NormalMap`, `_MaskTex` (샘플 슬라이드에서 두 슬롯을 함께 등록한다).
- 적용 대상에 제한이 없다는 점을 명시적으로 강조한다.
  - 2D animated characters (스프라이트 리깅/스켈레탈 애니메이션)
  - 2D tilemaps (예: 울타리)
  - 그 외 임의의 2D 스프라이트
- 즉 **타일맵도 노멀맵/마스크맵을 받는다.** 톱다운 배경 전체를 입체적으로 만들 수 있다는 뜻.

## 2. 노멀맵 만드는 5가지 방법 (슬라이드 12 원문 요약)

슬라이드 제목: *"Normal maps for 2D: Different authoring methods"*

1. **3D 모델링 소프트웨어에서 굽기** — 스프라이트를 Blender / 3ds Max 등에서 만들었다면 노멀맵 텍스처를 그대로 뽑는 게 가장 쉽다.
2. **기존 노멀맵을 변형해서 재사용(morph)** — 원형·링·젬 같은 형태는 같은 노멀맵을 이미지 변형만 해서 돌려쓴다.
3. **노멀맵 생성 툴 사용** — 발표에서 이름을 든 예: **Sprite Illuminator** ("there are special generators like uh Sprite Illuminator").
4. **컬러 샘플링 (color sampling method)** — 슬라이드 13. 레퍼런스 노멀맵(구/큐브/실린더/원뿔 형태의 노멀맵 팔레트)에서 **색을 스포이드로 찍어** 스프라이트 위에 직접 칠한다. 면의 방향이 곧 색이므로, 형태별 레퍼런스만 있으면 손으로 칠할 수 있다.
5. **3각도 라이팅 합성 (hand-painted, 3 channels)** — 슬라이드 15. 아래 별항 참조.

> 샘플 게임의 실제 선택: 슬라이드 16 — *"In our demos, we use hand-painted normal maps for most assets, color sampling from textures."*
> 즉 **4번 + 5번(수작업)이 주력**이다. 이전 데모 *Dragon Crashers* 의 기둥/램프 프롭도 같은 방식으로 칠했다.

## 3. 3각도 핸드페인팅 (슬라이드 15, *"Combining three different light angles"*)

노멀맵의 RGB가 각각 X/Y/Z 방향인 성질을 그대로 이용한다.

| 칠하는 라이트 | 결과 채널 | 슬라이드 표기 |
| --- | --- | --- |
| **오른쪽에서 빛** | R (X축) | "Light from Right → Tinted Red (X Axis)" |
| **위에서 빛** | G (Y축) | "Light from Top → Tinted Green (Y Axis)" |
| **정면에서 빛** | B (Z축) | "Light from Front (can be 100% Blue)" |

절차:
1. 오브젝트를 세 방향(위/오른쪽/정면)에서 라이팅한 그림을 각각 만든다.
2. 위 = 초록 틴트, 오른쪽 = 빨강 틴트, 정면 = 파랑(그냥 100% 블루로 채워도 됨).
3. 세 채널을 합성 → 노멀맵 완성.
4. `Albedo Sprite Texture` + `Normal Map` → Unity에서 최종 결과.

슬라이드의 최종 코멘트: *"…together to create the final result. This can lead to really good results but"* — 즉 **품질은 좋지만 수작업 비용이 크다.** 팀 사정에 맞춰 위 5가지 중 고르라는 게 결론이다("what works best for your team").

## 4. 임포트 설정 — 여기서 제일 많이 틀린다

슬라이드 17 원문: *"Normal map textures should be imported as Texture Type: Normal Map. This asset should be a Normal Map texture type for optimal sprite atlasing."*

Inspector (예: `Bush_n`):

| 필드 | 값 |
| --- | --- |
| Texture Type | **Normal map** |
| Texture Shape | 2D |
| Create from Grayscale | off |
| Flip Green Channel | off (필요 시에만) |

- 이유는 렌더 품질만이 아니라 **스프라이트 아틀라스 최적화**다. 타입이 맞아야 아틀라싱이 제대로 묶인다 ("this will help with your Atlas … for your Sprites").
- 마스크맵은 반대로 `Default` 로 넣는다 → [03-mask-maps.md](03-mask-maps.md)

## 5. 라이트 쪽 스위치를 잊지 말 것

노멀맵을 붙여도 **라이트의 `Normal Maps → Quality` 가 `Disabled` 면 아무 일도 일어나지 않는다.**

- Quality: `Disabled` / `Fast` / `Accurate`
- Distance: 라이트가 표면에서 떠 있는 거리감. 샘플 값 예: 캐릭터용 라이트 `20.31`, 소품 입체용 스팟 `1.28`
- 슬라이드 28에서 명시: *"You can use normal maps with spot, point, and freeform lights."* → 글로벌 라이트에는 안 통한다. 앰비언트만 깔고 노멀맵 반응을 기대하면 안 된다.

## 관련
- 입체감 연출로서의 사용법 → [04-lights.md](04-lights.md) §3
