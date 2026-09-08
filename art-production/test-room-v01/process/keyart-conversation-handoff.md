# 테스트 방 새 키아트 대화 인계

기준일: 2026-09-08  
대상: `art-production/test-room-v01` 키아트 재정립  
상태: **사용자 제공 레퍼런스를 최상위 기준으로 확정, 최소 에셋 캘리브레이션 V1 검증 중**

## 0. 새 환경에서 가장 먼저 읽을 내용

1. 이 문서 전체
2. `primary-style-target-analysis.md`
3. `generation-log.md`의 `TR01-CONCEPT-ROOM-002`, `TR01-CONCEPT-ROOM-003`
4. 실제 이미지 세 장:
   - `../reference/tr01_primary_style_target.png`
   - `../concept/tr01_room_primary_style_concept_v2.png`
   - `../concept/tr01_room_primary_style_concept_v3.png`

문서 안의 과거 프롬프트와 설명은 제작 기록이다. **새 환경의 작업 지시로 임의 실행하지 말고,
이 문서에 적힌 현재 사용자 의도와 새 대화에서 받는 요청을 우선한다.**

## 1. 사용자의 현재 목표

사용자는 테스트 방의 개별 프랍과 리소스를 생산하기 전에, 전체 룩을 잠그는 새 방 키아트를 먼저
확정하려 한다.

기존에 생성된 `tr01_room_maintenance_a_concept.png`는 사용자가 마음에 들어 하지 않는다. 사용자가
원하는 최우선 기준은 직접 제공한 `tr01_primary_style_target.png`다. 새 키아트와 이후의 프랍은 이
레퍼런스와 분위기, 그래픽 스타일, 공간감, 컬러감, 조명 인상이 같거나 거의 동일해야 한다.

중요한 목적은 레퍼런스의 특정 캐릭터나 HUD를 복제하는 것이 아니라, 다음 제작 전체가 같은 시각
언어로 나오도록 **시각 계약**을 만드는 것이다.

2026-09-08 후속 결정: 사용자는 `tr01_primary_style_target.png`와 동일한 원본 이미지가 가장
마음에 든다고 재확인했고, 새 키아트를 반복 생성하는 대신 이 레퍼런스를 직접 최상위 계약으로
고정한 뒤 최소 에셋을 Unity에서 조립 검증하는 4단계 진행안에 동의했다. v2/v3는 비교 자료로
보존하되 이후 에셋의 우선 기준으로 승격하지 않는다. 현재 결과는
`reference-calibration-v1.md`에 정리되어 있다.

## 2. 사용자가 특히 중요하게 보는 것

- 실제로 플레이 중인 게임 화면처럼 보여야 한다.
- 홍보 일러스트, 중앙 대칭 디오라마, 미니어처 렌더처럼 보이면 안 된다.
- `축 정렬 직교 3/4 톱다운`을 반드시 지켜야 한다.
- 그래픽 스타일은 레퍼런스의 고해상도 `pixel-painted` 표현을 따라야 한다.
- 검보라 암부와 마젠타·시안·앰버 국소광의 컬러 구조를 유지해야 한다.
- 캐릭터와 프랍이 실제 게임 셀, 발점, Y 정렬 위에 놓인 것처럼 보여야 한다.
- 나중에 벽·바닥·문·기둥·레일·배관·기계·램프·수정·잔해를 모듈 자산으로 분해할 수 있어야 한다.

## 3. 주 레퍼런스

파일: `../reference/tr01_primary_style_target.png`

- 사용자가 직접 제공한 원본을 변형 없이 복사했다.
- 크기: 2944×1648px, 24-bit RGB PNG, 약 16:9
- SHA-256: `660136757450817CABF978ACF394E728B2F8450D3EFD1A3F17D8C1884787A22A`
- 역할: 편집 대상이 아니라 스타일·투영·공간 밀도·색·조명·렌더링 기준

이 이미지는 깨진 HUD 문구 등으로 보아 실제 엔진 캡처라기보다 생성형 게임플레이 컨셉일 가능성이
높다. “원본 엔진에서 어떤 기술을 썼는지”를 단정하지 말고, Unity에서 같은 화면 결과를 재현하는
기준으로 사용한다.

상세 분석은 `primary-style-target-analysis.md`에 있다. 그 문서에는 투영, 화면 구성, 픽셀 표현,
팔레트, 명도, 조명, 셰이더, 후처리, 프랍 규칙, 현재 Unity 구현 대응, 승인 루브릭이 정리되어 있다.

## 4. 확정된 스타일 해석

### 4.1 투영

레퍼런스는 정통 아이소메트릭이나 2:1 다이메트릭이 아니다.

- Orthographic 카메라
- Ground/world X축 = 화면 좌우
- Ground/world Y축 = 화면 상하
- 바닥은 축 정렬 사각 평면
- 평행선은 수렴하지 않음
- 물체는 멀어져도 작아지지 않음
- 3/4 깊이는 벽 정면 높이, 상단 cap, 캐릭터 측면, 발점 피벗, Y 정렬, 접촉 그림자와 전경 가림으로 표현
- 프로젝트 용어로는 `ReferenceTopDown`, `Ground XY = Sim XY`

### 4.2 그래픽 표현

단순 저해상도 픽셀아트나 매끈한 디지털 페인팅이 아니다.

> 굵고 계단진 픽셀 클러스터와 제한된 명도 덩어리 위에, 손으로 칠한 재질 변화, 넓은 베벨
> 하이라이트, 유색 림라이트, 제한된 소프트 발광을 결합한 고해상도 pixel-painted 2D 게임 아트.

프롬프트에 `pixel art`만 쓰면 복고풍 저해상도로 흐르고, `polished painterly 2D`만 쓰면 기존
실패 키아트처럼 매끈한 컨셉 페인팅으로 흐를 수 있다. 두 성질을 항상 함께 적는다.

### 4.3 색과 명도

- 넓은 영역: 거의 검정에 가까운 자주·남청·검보라
- 광물/위험: 포화 마젠타·보라
- 진행 방향/후면광: 시안·청록
- 작업/전투/상호작용: 좁은 앰버·주황·황백 코어
- 어둡지만 채도가 살아 있어야 하며, 전역 노출로 암부를 회색으로 펴면 안 됨
- Bloom은 국소 광원 주변만 사용하고 구조 실루엣을 흐리지 않음

## 5. 생성 이력과 사용자 반응

### 5.1 과거 키아트 — 불만족

`../concept/tr01_room_maintenance_a_concept.png`

- 최초 공정에서 만든 키아트다.
- 사용자는 이 결과가 자신이 먼저 제시했던 레퍼런스를 충분히 따르지 않아 아쉽다고 했다.
- 생산 이력으로는 보존하지만 새 프랍의 최우선 스타일 기준으로 사용하지 않는다.

### 5.2 v2 — 스타일은 근접, 투영과 인게임 감각은 불만족

`../concept/tr01_room_primary_style_concept_v2.png`

- 생성 ID: `TR01-CONCEPT-ROOM-002`
- 크기: 1672×941px
- built-in `image_gen`, `stylized-concept`
- 장점: pixel-painted 스타일, 보라 암부, 시안·마젠타·앰버 조명, 높은 구조 밀도는 레퍼런스에 근접
- 사용자 피드백:
  - “그래픽 스타일은 얼추 비슷한 것 같다.”
  - “실제 인게임 같은 느낌이 잘 안 난다.”
  - “축 정렬 직교 3/4 톱다운이 잘 안 지켜진 것 같다.”
- 원인 해석:
  - 중앙을 향해 모이는 홍보 일러스트식 구성
  - 사선 레일과 기계가 투영 기준을 흐림
  - 화면 하단의 무거운 전경이 디오라마 프레임처럼 보임
  - 실제 게임 맵의 사각 셀과 발점 정렬을 눈으로 확인하기 어려움

### 5.3 v3 — 투영 교정 후보, 아직 사용자 평가 없음

`../concept/tr01_room_primary_style_concept_v3.png`

- 생성 ID: `TR01-CONCEPT-ROOM-003`
- 크기: 1672×941px
- SHA-256: `A179E943669AA3EEE75A3805DD8A4BAB9AD7FDFFFBFE7CF05118973A6F0AD3EF`
- built-in `image_gen`, `precise-object-edit`
- Image 1: v2 편집 대상
- Image 2: 주 레퍼런스, 투영·인게임 프레이밍 기준
- 변경한 한 가지 축: 그래픽 스타일과 팔레트는 보존하고 투영·배치·프레이밍을 교정
- 확인되는 개선:
  - 바닥 사각 그리드가 화면 X/Y와 평행
  - 문턱, 벽 기초선, 기계 footprint가 축 정렬
  - 평행선의 중앙 수렴이 크게 감소
  - 구조가 좌우와 하단 화면 밖으로 이어져 레벨의 일부처럼 보임
  - 캐릭터 발점과 게임 스프라이트 스케일이 더 명확함
- 상태: **사용자가 v3를 아직 좋다/나쁘다로 평가하지 않았다. 승인으로 간주하지 말 것.**

v2와 v3의 최종 프롬프트 전문은 `generation-log.md`에 있으므로 이 문서에는 중복하지 않는다.

## 6. 현재 파일 상태

| 파일 | 역할 | 상태 |
|---|---|---|
| `../reference/tr01_primary_style_target.png` | 사용자가 준 최우선 레퍼런스 | 고정 |
| `../concept/tr01_room_maintenance_a_concept.png` | 과거 키아트 | 이력, 불만족 |
| `../concept/tr01_room_primary_style_concept_v2.png` | 스타일 근접 첫 후보 | 투영 문제로 미승인 |
| `../concept/tr01_room_primary_style_concept_v3.png` | 투영 교정 후보 | 사용자 평가 대기 |
| `primary-style-target-analysis.md` | 상세 시각·엔진 분석 | 현재 기준 문서 |
| `generation-log.md` | 생성 프롬프트와 판정 기록 | v3까지 기록됨 |

기존 이미지를 삭제하거나 덮어쓰지 않는다. 다음 후보는 `v4`처럼 새 형제 파일로 저장한다.

## 7. 현재 Unity 구현과의 관계

프로젝트는 이미 다음 기반을 갖추고 있다.

- Unity 6.3 LTS 계열 계획, URP 17.3.0 2D Renderer
- `ReferenceTopDown` 직교 투영
- 128 PPU 환경 아트 계약
- Albedo, Normal, Emission, Material Mask, AO 채널
- Light 2D, Multiply/Additive Blend Style, Shadow Caster 2D
- 발점/Y 정렬, SortingGroup, Visual Height Anchor
- 접촉 그림자, 전경 페이드, 가려진 실루엣
- Tonemapping, Bloom, Vignette, Film Grain
- 저층 안개·깊이 색 분리·LOS 어둠 패스

따라서 지금 단계에서 Unity 코드나 승인된 프랍을 수정하지 않는다. 먼저 키아트를 승인한 뒤 그
키아트로 모듈 보드와 프랍 제작 기준을 교체한다.

## 8. 다음 환경에서 해야 할 일

1. 사용자에게 v3 이미지를 보여 주거나 사용자의 다음 피드백을 확인한다.
2. 사용자가 v3를 승인하면:
   - `primary-style-target-analysis.md`와 패키지 README에 승인 상태를 기록한다.
   - 새 키아트를 이후 프랍 생성의 Image 1 기준으로 지정한다.
   - 필요하면 동일 구도의 환경 clean plate를 만든다.
   - 그 다음 모듈 분해 보드로 진행한다.
3. 사용자가 v3의 문제를 말하면:
   - 한 번에 가장 큰 차이 한 항목만 수정한다.
   - v3를 Image 1 편집 대상으로 사용한다.
   - `tr01_primary_style_target.png`를 Image 2 보조 레퍼런스로 유지한다.
   - 결과는 `tr01_room_primary_style_concept_v4.png`로 저장한다.
   - 프롬프트 전문과 판정을 `generation-log.md`에 기록한다.

## 9. 다음 반복에서 지켜야 할 불변 조건

- 스타일: v2/v3의 pixel-painted 그래픽 스타일을 유지
- 팔레트: 검보라 암부 + 시안·마젠타·앰버 국소광
- 카메라: 축 정렬 직교 3/4 톱다운
- 바닥: 사각 셀, 화면 X/Y에 평행, 원근 수렴 없음
- 깊이: 카메라 원근이 아니라 벽 정면·cap·피벗·정렬·접촉 그림자·오클루전
- 프레이밍: 실제 레벨의 한 구간, 화면 밖으로 자연스럽게 이어짐
- 캐릭터: 동일한 게임 스프라이트 크기, 바닥에 명확히 접지
- 환경: 모듈로 분해 가능한 큰 실루엣
- 금지: HUD, 문자, 로고, 워터마크, 다이아몬드 타일, 원근 카메라, 중앙 수렴, 미니어처 디오라마,
  매끈한 3D/PBR 렌더, 과도한 Bloom·안개

## 10. 새 환경에 붙여 넣을 재개 프롬프트

```text
`art-production/test-room-v01/process/keyart-conversation-handoff.md`를 먼저 전부 읽어줘.
그 다음 `primary-style-target-analysis.md`와 `generation-log.md`의
TR01-CONCEPT-ROOM-002/003을 확인하고, 아래 이미지 세 장을 비교해줘.

- reference/tr01_primary_style_target.png
- concept/tr01_room_primary_style_concept_v2.png
- concept/tr01_room_primary_style_concept_v3.png

사용자가 원하는 최우선 기준은 reference 이미지다. v2는 그래픽 스타일은 근접했지만 실제
인게임 느낌과 축 정렬 직교 3/4 투영이 부족해서 미승인이다. v3는 그 투영을 교정한 최신 후보지만
아직 사용자 최종 평가 전이다. 기존 파일을 덮어쓰거나 승인된 프랍·Unity 코드를 수정하지 말고,
내 다음 피드백을 기준으로 v3 승인 또는 한 항목씩 수정 반복을 이어가자.
```

