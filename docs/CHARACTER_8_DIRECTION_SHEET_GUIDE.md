# 8방향 캐릭터 시트 제작 가이드

> 기준 결과물:
> [드릴러](../assets/characters/driller-8dir-transparent.png) ·
> [스카웃](../assets/characters/scout-8dir-transparent.png) ·
> [엔지니어](../assets/characters/engineer-8dir-transparent.png) ·
> [거너](../assets/characters/gunner-8dir-transparent.png)
>
> 스타일 기준 레퍼런스: 사용자가 제공한 12인 캐릭터 이미지. 큰 둥근 머리, 약 2등신, 짧은 팔다리, 짙은 손그림 선, 제한된 평면 색상과 인쇄 질감을 기준으로 한다.
>
> 목적: 어떤 캐릭터 컨셉을 입력하더라도 동일한 8방향 원형 구도, 동일한 탑다운 3/4 카메라, 동일한 비율과 그래픽 언어를 재현한다.

## 1. 결과물 한 줄 정의

**한 캐릭터의 8방향 모습을, 빈 중심점을 둘러싼 원형 구도에 배치한 투명 PNG 턴어라운드 시트.**

이 시트는 캐릭터 디자인 확정, 방향별 외형 확인, 애니메이션 생성용 참조 이미지로 사용한다. 최종 애니메이션 스프라이트 시트가 아니라 모든 방향의 정체성과 카메라를 잠그는 기준 시트다.

## 2. 고정값과 가변값

### 2.1 항상 고정하는 값

- 8방향 원형 배치
- 방향별 위치와 순서
- 중심에서 바깥쪽을 바라보는 방향성
- 직교 투영에 가까운 탑다운 3/4 카메라
- 모든 방향에서 동일한 카메라 높이·기울기·줌
- 약 2등신의 짧고 둥근 캐릭터 비율
- 굵은 짙은 갈색 외곽선
- 제한된 평면 색상과 한 단계 그림자
- 가벼운 인쇄 질감
- 중앙 여백
- 배경 완전 투명

### 2.2 캐릭터마다 바꾸는 값

- 역할과 성격
- 성별·연령대·체형의 세부 차이
- 얼굴, 머리카락, 수염
- 주색·보조색·강조색
- 의상과 보호 장비
- 주무기 또는 대표 도구
- 등에 멘 장비
- 역할을 읽게 하는 대표 실루엣 2~4개

가변값이 바뀌어도 고정값을 함께 바꾸지 않는다.

## 3. 캔버스와 원형 구도

### 3.1 마스터 캔버스

| 항목 | 권장값 |
|---|---|
| 비율 | `1:1` 정사각형 |
| 작업 해상도 | `2048 × 2048 px` 권장 |
| 최소 해상도 | `1536 × 1536 px` |
| 색상 모드 | RGBA |
| 배경 알파 | 캐릭터 외부 `0` |
| 캐릭터 수 | 정확히 8명 |
| 중앙 영역 | 완전히 비움 |

생성 도구가 해상도를 임의 변경하더라도 정사각형 구도와 정규화 좌표를 우선한다. 리사이즈가 필요하면 비율을 유지하고 캐릭터를 자르지 않는다.

### 3.2 방향별 중심 좌표

아래 좌표는 전체 캔버스를 `0.0~1.0`으로 본 정규화 좌표다. 각 캐릭터의 **발 중앙 또는 지면 피벗**을 기준으로 한다.

| 방향 | 화면상의 위치 | 권장 피벗 `(x, y)` | 보여야 하는 면 |
|---|---|---:|---|
| `N` | 위 | `(0.50, 0.22)` | 완전한 뒷모습 |
| `NE` | 오른쪽 위 | `(0.75, 0.30)` | 오른쪽 대각선 뒷모습 |
| `E` | 오른쪽 | `(0.86, 0.55)` | 오른쪽 옆모습 |
| `SE` | 오른쪽 아래 | `(0.75, 0.79)` | 오른쪽 대각선 앞모습 |
| `S` | 아래 | `(0.50, 0.88)` | 완전한 정면 |
| `SW` | 왼쪽 아래 | `(0.25, 0.79)` | 왼쪽 대각선 앞모습 |
| `W` | 왼쪽 | `(0.14, 0.55)` | 왼쪽 옆모습 |
| `NW` | 왼쪽 위 | `(0.25, 0.30)` | 왼쪽 대각선 뒷모습 |

좌표는 캐릭터·무기 크기에 따라 `±0.03` 범위에서만 조정한다. 원 전체를 찌그러뜨리거나 한 방향만 중심에 붙이지 않는다.

### 3.3 중앙 여백

- 중앙점 기준 반경 `0.20~0.24`는 비운다.
- 캐릭터, 무기, 머리카락, 장비가 중앙 여백을 침범하지 않는다.
- 방향 안내 원, 십자선, 숫자, 방향 문자는 최종 결과에 넣지 않는다.
- 시트만 보아도 각 캐릭터가 중심에서 바깥쪽으로 향하고 있어야 한다.

### 3.4 안전 여백과 겹침

- 캔버스 바깥쪽 안전 여백: 최소 `4%`
- 인접 캐릭터 사이 투명 간격: 최소 캐릭터 폭의 `12%`
- 무기 끝, 머리카락, 배낭, 부츠를 자르지 않는다.
- 어떤 두 방향의 실루엣도 겹치지 않는다.

## 4. 방향 규칙

모든 캐릭터는 원 중심에서 바깥을 본다. 캐릭터가 놓인 위치와 바라보는 방향이 일치해야 한다.

```text
                 N
          NW           NE

       W       [EMPTY]      E

          SW           SE
                 S
```

### 4.1 뒷모습 판정

`N`, `NE`, `NW`는 얼굴을 정면으로 보여 주면 실패다.

- `N`: 뒤통수, 등, 배낭, 어깨 뒤가 중심적으로 보인다.
- `NE`·`NW`: 얼굴은 볼이나 코의 옆선만 조금 허용한다.
- 등에 멘 장비가 있다면 앞 방향보다 더 크게 읽혀야 한다.
- 머리 장식, 머리카락, 케이프, 배낭의 후면 구조가 일관되어야 한다.

### 4.2 정면 판정

- `S`: 얼굴과 몸통 앞면이 좌우 균형으로 보인다.
- `SE`·`SW`: 얼굴과 가슴 앞면이 보이는 대각선 정면이다.
- 주무기는 바라보는 방향으로 향한다.

### 4.3 좌우 재사용

최종 게임 애니메이션에서는 필요하면 다음 방향을 플립해 재사용할 수 있다.

- `NE ↔ NW`
- `E ↔ W`
- `SE ↔ SW`

하지만 턴어라운드 기준 시트는 장비 구조와 방향 검수를 위해 8방향을 모두 그린다.

## 5. 카메라와 투영

### 5.1 기준 카메라

- 투영: 직교 투영 또는 매우 약한 원근
- 시점: 탑다운 3/4 버드뷰
- 카메라 고도: 지면 기준 약 `35~45°` 위
- 카메라 롤: `0°`
- 렌즈 왜곡: 없음
- 방향별 카메라 회전: 금지
- 방향 표현 방식: 카메라는 고정하고 캐릭터만 회전

직각 탑다운, 정면 캐릭터 카드, 낮은 사이드뷰를 사용하지 않는다. 머리 윗면과 어깨·장비의 윗면이 보이면서도 얼굴 또는 등 방향이 읽혀야 한다.

### 5.2 카메라 잠금 검수

8명을 나란히 비교했을 때 다음 값이 같아야 한다.

- 머리 윗면이 보이는 비율
- 헬멧 챙 또는 머리카락의 타원 각도
- 어깨 윗면과 몸통 앞·뒤 면의 노출량
- 부츠가 지면에 닿는 각도
- 배낭 윗면이 보이는 정도
- 캐릭터 전체 크기

한 방향만 머리 윗면이 과도하게 보이면 카메라가 회전한 것이므로 재생성한다.

## 6. 캐릭터 비율

스타일 기준은 귀엽고 단순한 약 `2등신` 캐릭터다.

| 부위 | 전체 키 대비 권장 비율 |
|---|---:|
| 머리와 머리 장식 | `45~52%` |
| 목·몸통 | `25~30%` |
| 다리와 발 | `18~24%` |
| 팔 길이 | 몸통 높이와 비슷하거나 더 짧게 |
| 손 | 작고 둥근 덩어리 |
| 발 | 작고 넓은 타원 또는 둥근 부츠 |

### 6.1 형태 언어

- 머리: 크고 둥근 한 덩어리
- 몸통: 작고 짧은 사다리꼴·콩 모양
- 팔·다리: 짧고 굵은 원통
- 손·발: 손가락이나 신발 구조를 세밀하게 나누지 않음
- 장비: 실제 구조보다 역할을 설명하는 큰 형태 우선
- 무기: 배럴, 드릴, 컨트롤러 등 대표 형태 1~3개만 강조

### 6.2 역할별 체형 차이

모두 같은 2등신 체계를 사용하되 실루엣으로 역할을 구분한다.

| 역할 | 체형 방향 |
|---|---|
| 드릴러 | 둥글고 낮은 무게 중심, 큰 드릴과 탱크 |
| 스카웃 | 머리카락과 스카프가 가볍게 뻗는 실루엣 |
| 엔지니어 | 둥근 헬멧, 안경, 네모난 배낭과 컨트롤러 |
| 거너 | 어깨와 무기가 넓고 안정적인 실루엣 |

## 7. 그래픽 스타일

### 7.1 선

- 외곽선: 짙은 갈색 또는 흑갈색
- 순수 검정은 최소화
- 외곽선 굵기: 머리 폭의 약 `2.0~3.5%`
- 내부선: 외곽선의 `55~75%`
- 선은 완벽한 벡터보다 약간 불규칙한 손그림 느낌
- 작은 재봉선·볼트·패널선을 반복하지 않는다.

권장 외곽선 범위:

- 기본: `#2F261F`
- 따뜻한 변형: `#3A281E`
- 차가운 장비 내부선: `#2B2D2C`

### 7.2 색상

- 캐릭터별 핵심 색상 `3~5개`
- 피부·머리카락을 포함해 전체 팔레트 `8~12개` 이내 권장
- 색상은 따뜻하고 약간 탁한 방향
- 순백과 순검정의 큰 면 사용 금지
- 작은 부분마다 다른 색을 쓰지 않는다.

팔레트 구조:

1. 역할 주색
2. 역할 보조색
3. 장비 금속색
4. 피부색
5. 머리카락색
6. 작은 발광 강조색

### 7.3 명암

- 기본색 + 그림자색의 `2단계`가 원칙
- 필요한 경우 작은 하이라이트 1단계만 추가
- 광원은 화면 왼쪽 위에서 오는 부드러운 공통광
- 그림자는 캐릭터 내부에만 사용
- 캐릭터 아래 투영 그림자는 넣지 않는다.
- 금속도 사진처럼 반사시키지 않고 평면 색상으로 단순화한다.

### 7.4 질감

- 얇은 종이 인쇄 또는 건식 브러시 질감
- 넓은 색면에 아주 적은 입자와 얼룩만 사용
- 질감은 축소했을 때 보이지 않을 정도로 약하게
- 하프톤 망점, 촘촘한 스티플, 사실적인 재질 노이즈 금지
- 모든 방향에 비슷한 질감 밀도를 사용

### 7.5 얼굴

- 눈: 작은 점 또는 짧은 타원
- 코: 점이나 한 획
- 입: 한 줄 또는 작은 곡선
- 표정은 한눈에 읽히되 과장된 치아·주름·콧구멍을 피한다.
- 같은 캐릭터의 눈 간격, 얼굴 폭, 머리카락 윤곽이 8방향에서 일관되어야 한다.

### 7.6 디테일 예산

캐릭터당 반드시 읽혀야 하는 대표 요소를 최대 4개로 제한한다.

예:

```text
드릴러 = 주황 헬멧 + 붉은 수염 + 파란 드릴 + 등 탱크
스카웃 = 파란 고글 모자 + 포니테일 + 붉은 스카프 + 소형 총
```

대표 요소가 아닌 작은 포켓, 볼트, 호스, 패널, 흠집은 생략한다.

## 8. 장비와 무기

- 장비는 캐릭터보다 사실적으로 그리지 않는다.
- 큰 원통, 상자, 구, 손잡이 등 단순한 덩어리로 환원한다.
- 무기는 바라보는 방향과 일치해야 한다.
- 무기 끝이 인접 캐릭터나 캔버스를 침범하면 크기를 줄인다.
- 손은 무기에 자연스럽게 붙어 있어야 하며 손가락 개수 묘사는 생략할 수 있다.
- 등에 멘 장비는 `N`, `NE`, `NW`에서 구조가 동일해야 한다.
- 독립 드론·소환물·VFX는 캐릭터 시트와 분리한다.

## 9. 투명 배경 규격

- 최종 포맷: `PNG`, `32-bit RGBA`
- 캐릭터 외부 알파: 정확히 `0`
- 캐릭터 내부 알파: 원칙적으로 `255`
- 체크무늬는 투명 배경 표현일 뿐 최종 픽셀로 포함하면 안 된다.
- 흰 배경, 검은 배경, 방향 원, 중앙점, 눈금, 텍스트를 제거한다.
- 캐릭터 가장자리의 흰색·회색 프린지와 검은 헤일로를 허용하지 않는다.

모서리 네 픽셀의 알파값은 모두 `0`이어야 한다.

## 10. 기본 생성 워크플로

1. 캐릭터 컨셉 이미지와 스타일 레퍼런스를 입력한다.
2. 컨셉에서 유지할 정체성 요소를 3~6개로 요약한다.
3. 아래 마스터 프롬프트로 8방향 시트를 생성한다.
4. 방향 수, 순서, 뒷모습, 카메라, 비율을 검수한다.
5. 체크무늬가 실제 픽셀이면 배경 추출 프롬프트를 별도로 실행한다.
6. 알파 채널과 모서리 픽셀을 검사한다.
7. 프로젝트의 기존 대상 파일을 직접 갱신한다.
8. 이 시트를 방향별 애니메이션의 정체성 참조로 사용한다.

한 번에 스타일·배경·애니메이션까지 모두 해결하려 하지 않는다. 먼저 턴어라운드의 정체성과 카메라를 고정한다.

## 11. 마스터 생성 프롬프트

아래 프롬프트에서 대괄호 항목만 교체한다. 영문 사용을 권장한다.

```text
Use case: style-transfer
Asset type: transparent 8-direction [ROLE] character sheet

Image 1 is the character concept and identity reference.
Image 2 is the sole reference for proportions, line style, shape language, colors, and rendering simplicity.

Create exactly eight full-body views of the same [ROLE] character, evenly arranged around an empty center. Every character faces directly outward from the center.

Preserve these identity elements from Image 1: [3 TO 6 ICONIC FEATURES].

Match Image 2 closely:
- extremely compact chibi proportions, about 2 heads tall
- one large rounded head, tiny torso, very short arms and legs
- small simple hands and feet
- rounded toy-like silhouette and simplified equipment
- thick dark-brown hand-drawn outlines with organic irregular edges
- flat muted colors with only one simple shadow tone
- subtle dry-print grain and sparse speckles
- dot-like eyes and minimal facial features
- no gradients, glossy rendering, realistic anatomy, or mechanical micro-detail

Compass layout:
top N full back view;
top-right NE rear three-quarter;
right E right profile;
bottom-right SE front three-quarter;
bottom S full front view;
bottom-left SW front three-quarter;
left W left profile;
top-left NW rear three-quarter.

Absolute composition lock:
fixed square canvas, circular eight-character layout, equal spacing, identical scale and ground pivots, empty center, and one fixed orthographic top-down three-quarter camera. Rotate only the character, never the camera. Do not crop, overlap, add, remove, or duplicate a direction.

Genuine transparent RGBA background. No checkerboard, white background, compass guide, floor, shadow, scenery, effects, labels, text, or watermark.
```

## 12. 기존 시트의 스타일만 바꾸는 프롬프트

기존 8방향 시트가 이미 있을 때 사용한다.

```text
Use case: style-transfer
Asset type: redesigned transparent 8-direction character sheet

Image 1 is the edit target and absolute composition reference.
Image 2 is the sole style and proportion reference.

Redraw only the eight characters from Image 1 in the proportions and graphic style of Image 2. Preserve the character's role, colors, costume, weapon, and largest iconic equipment shapes.

Match Image 2: about 2 heads tall, large rounded head, tiny torso, very short limbs, small hands and feet, thick dark-brown organic outlines, flat muted colors, one shadow tone, subtle dry-print texture, dot-like eyes, minimal facial features, and strongly simplified equipment.

Strict invariants: preserve Image 1's canvas, exact eight positions, radial spacing, outward-facing directions, character scale, ground pivots, empty center, and fixed orthographic top-down three-quarter camera. Rotate only the character. Do not move, zoom, tilt, crop, overlap, add, remove, or duplicate a direction.

Transparent RGBA background. No checkerboard, guide, floor, shadow, effects, text, or watermark.
```

## 13. 배경 추출 프롬프트

체크무늬나 흰 배경이 실제 이미지에 포함됐을 때만 별도로 실행한다.

```text
Use case: background-extraction
Asset type: transparent 8-direction character sheet

Remove the entire background from Image 1 and keep only the eight characters.

Preserve every character exactly: same identity, compact proportions, graphic style, radial composition, positions, directions, camera, scale, pose, spacing, colors, outlines, face, costume, weapon, equipment, and texture. Do not redraw, restyle, move, crop, resize, sharpen, or add detail.

Output a clean 32-bit RGBA PNG with alpha exactly zero everywhere outside the eight silhouettes. Preserve intentional light colors inside the characters. No halos, checkerboard, white or black background, guide, center dot, floor, shadow, text, or watermark.
```

## 14. 애니메이션 참조 규칙

8방향 턴어라운드 시트를 이미지-투-비디오 또는 애니메이션 생성의 참조로 사용할 때:

- 캐릭터별 위치와 지면 피벗을 고정한다.
- 방향을 바꾸지 않는다.
- 카메라를 움직이지 않는다.
- 원형 배치와 중앙 여백을 유지한다.
- 각 캐릭터가 제자리에서 같은 동작을 수행하게 한다.
- 루프의 마지막 프레임은 첫 프레임과 연결한다.
- 무기와 배낭의 관성만 작은 보조 움직임으로 허용한다.

걷기 예시:

```text
Use the attached 8-direction sheet as the exact first frame and identity reference.

Animate all eight characters walking in place at the same time, each continuing to face its original compass direction. Use short alternating steps, subtle body bounce, and small secondary motion in hair, clothing, weapon, and backpack.

Keep every character locked to the exact position and ground pivot. No travel, turning, sliding, rotation, scaling, camera movement, layout change, or direction change. Preserve the transparent background and empty center.

Create a clean 2-second seamless loop. The final frame must connect perfectly to the first frame.
```

## 15. 검수 체크리스트

### 15.1 구성

- [ ] 캐릭터가 정확히 8명이다.
- [ ] 중앙이 비어 있다.
- [ ] 8명이 같은 반지름과 간격으로 배치됐다.
- [ ] 캐릭터나 무기가 겹치지 않는다.
- [ ] 캔버스 밖으로 잘린 부분이 없다.

### 15.2 방향

- [ ] `N`은 완전한 뒷모습이다.
- [ ] `NE`, `NW`는 대각선 뒷모습이다.
- [ ] `E`, `W`는 옆모습이다.
- [ ] `SE`, `SW`는 대각선 앞모습이다.
- [ ] `S`는 완전한 정면이다.
- [ ] 모든 캐릭터가 중심에서 바깥을 본다.
- [ ] 같은 방향이 복제되거나 누락되지 않았다.

### 15.3 카메라

- [ ] 모든 방향에서 머리 윗면 노출량이 비슷하다.
- [ ] 직각 탑다운이나 낮은 사이드뷰가 섞이지 않았다.
- [ ] 방향마다 줌과 캐릭터 크기가 같다.
- [ ] 카메라가 아니라 캐릭터가 회전했다.

### 15.4 비율과 스타일

- [ ] 전체 키가 약 2등신이다.
- [ ] 머리가 전체 키의 절반 안팎이다.
- [ ] 팔다리와 손발이 짧고 단순하다.
- [ ] 대표 장비 외의 미세 디테일이 억제됐다.
- [ ] 외곽선이 짙은 갈색이며 굵기가 일관된다.
- [ ] 명암이 기본색과 그림자색 중심이다.
- [ ] 인쇄 질감이 약하고 균일하다.
- [ ] 8방향 모두 같은 인물로 보인다.

### 15.5 파일

- [ ] PNG가 `32-bit RGBA`다.
- [ ] 네 모서리 알파가 모두 `0`이다.
- [ ] 체크무늬가 실제 픽셀로 남지 않았다.
- [ ] 흰색·검은색 헤일로가 없다.
- [ ] 캐릭터 내부의 눈·램프·하이라이트는 지워지지 않았다.

## 16. 실패 유형과 수정 문장

| 실패 | 수정 프롬프트에 추가할 문장 |
|---|---|
| 뒷모습이 정면으로 나옴 | `N must be a full back view. NE and NW must clearly show the back of the head, shoulders, and backpack.` |
| 방향 중복 | `All eight compass directions must be distinct. Do not mirror, duplicate, or omit a direction.` |
| 카메라가 방향마다 바뀜 | `Use one locked camera. Rotate only the character, never the camera.` |
| 비율이 길어짐 | `The character must remain about two heads tall with an oversized round head and extremely short limbs.` |
| 디테일이 많아짐 | `Remove small seams, bolts, cables, scratches, panels, and surface rendering. Keep only the four largest iconic features.` |
| 너무 매끈한 벡터가 됨 | `Use slightly irregular hand-drawn dark-brown outlines and subtle dry-print grain.` |
| 3D 광택이 생김 | `Use flat colors and one simple shadow tone. No gradients, specular highlights, glossy materials, or 3D rendering.` |
| 중앙을 침범함 | `Keep the center completely empty and move all eight figures outward on one consistent radius.` |
| 체크무늬가 남음 | 배경 추출 프롬프트를 별도 실행 |
| 장비가 방향마다 달라짐 | `The same equipment count, shapes, attachment points, and colors must remain consistent in all eight views.` |

수정은 한 번에 하나의 실패만 겨냥한다. 전체 프롬프트를 매번 바꾸면 정체성과 구도가 함께 흔들릴 수 있다.

## 17. 파일명과 프로젝트 배치

권장 파일명:

```text
assets/characters/[character-id]-8dir-transparent.png
```

예:

```text
assets/characters/driller-8dir-transparent.png
assets/characters/scout-8dir-transparent.png
```

동일 캐릭터를 수정할 때는 별도 버전 파일을 만들지 않고 기존 기준 파일을 갱신한다. 백업은 명시적으로 요청받은 경우에만 만든다.

## 18. 최종 승인 기준

다음 세 문장을 모두 만족해야 승인한다.

1. **어떤 방향에서도 같은 캐릭터로 보인다.**
2. **8명을 가린 뒤 한 명씩 보아도 카메라 높이와 비율이 같다.**
3. **작게 축소해도 역할의 주색과 대표 장비만으로 직업을 구분할 수 있다.**

하나라도 만족하지 않으면 애니메이션 제작으로 넘어가지 않고 턴어라운드 시트부터 수정한다.
