# 레퍼런스 직결 최소 에셋 캘리브레이션 V1

기준일: 2026-09-08  
상태: `working` — Unity 최소 조립 및 바닥 매크로 변형 6종 검증 완료

## 결정

- 최상위 시각 기준은 `../reference/tr01_primary_style_target.png`다.
- `concept/tr01_room_primary_style_concept_v2.png`와 `v3.png`는 비교 자료로만 보존한다.
- 기존 `approved/` 51종과 revision 17 manifest는 변경하지 않는다.
- 새 후보는 먼저 바닥·벽·수정·대표 드릴러 네 항목만 제작해 128 PPU Unity 조립에서 검증한다.

## 산출물

- 캘리브레이션 보드: `../concept/tr01_reference_asset_calibration_board_v1.png`
- 생성 원본: `../source/reference_calibration_v1/`
- 정규화 작업본: `../working/reference_calibration_v1/`
- Unity 임포트본: `../../../unity/TunnelCrew/Assets/Art/Visual/ReferenceCalibrationV1/`
- Unity 검증 씬: `../../../unity/TunnelCrew/Assets/_Project/Scenes/ReferenceCalibrationV1.unity`
- Unity 자동 조립·캡처 도구:
  `../../../unity/TunnelCrew/Assets/_Project/Editor/BuildReferenceCalibrationPreview.cs`
- 최초 조립 캡처: `../qa/reference-calibration-v1-unity.png`
- 바닥 6종 Unity 조립 캡처: `../qa/reference-calibration-floor-variants-unity.png`
- 바닥 A 단독 6×6 반복 검사: `../qa/tr01_reference_floor_a_repeat_6x6.png`
- 바닥 A~F 혼합 6×6 반복 검사: `../qa/tr01_reference_floor_variants_6x6.png`
- 재현 스크립트: `../../../tools/art/finalize-reference-calibration-v1.py`

## 규격

| 후보 | Unity 캔버스 | PPU | 피벗 | 상태 |
|---|---:|---:|---|---|
| 바닥 A~F | 각 128×128 | 128 | 중앙 | working set |
| 벽 A | 384×384 | 128 | 하단 중앙, y 0.02 | working set-piece |
| 수정 A | 256×256 | 128 | 하단 중앙, y 0.03 | working |
| 드릴러 A | 384×256 | 128 | 하단 중앙, y 0.03 | working scale reference |

벽의 최초 생성본에는 체크무늬가 RGB로 구워져 있어 `rejected` 처리했고, 동일 디자인을
`background-extraction`으로 다시 추출해 실제 RGBA 알파를 확보했다. 수정과 드릴러도 실제 알파
최솟값 0을 확인했다. 바닥은 불투명 RGB 소스가 정상이다.

## Unity 검증 결과

- 1920×1080, 직교 카메라, Ground XY 화면 축 정렬로 캡처했다.
- 드릴러는 발점부터 머리까지 약 1.5셀로 읽히며, 얼굴·수염·거대 드릴이 환경보다 먼저 보인다.
- 벽·수정·드릴러의 발점과 투명 외곽이 정상이고 잘림이 없다.
- 첫 바닥 후보는 작은 암석과 마젠타 점이 지나치게 많아 폐기했다.
- 두 번째 바닥 후보 A를 기준으로 B~F를 추가했다. 대각 2분할, 중앙 3분할, 수평 지층,
  거의 온전한 대형 석판, 불규칙 3분할로 큰 균열 구조를 나눴다.
- A~F를 결정적 분배·회전·반전으로 섞은 6×6 반복판과 Unity 120셀 조립에서 단일 원본의
  반복 고리가 크게 줄었다. 각 파일은 서로 다른 픽셀이며 반대편 경계 4px가 완전히 동일하다.
- 현재 캡처는 비율·형태·기본 팔레트를 보는 Albedo 단계다. Normal/Emission/Mask/AO와 실시간
  마젠타·시안·앰버 조명은 이 스타일 방향이 승인된 뒤 제작한다.

## 현재 판정

레퍼런스 직결 방식은 v3보다 캐릭터 애착점과 직업 실루엣을 분명히 회복했다. 바닥 매크로 변형
6종은 넓은 방에서도 사용할 수 있는 첫 세트가 됐다. 다음 순서는 벽을 실제 벽 정면/상단 모듈로
재분해하고, 이어서 수정의 Emission 분리, 드릴러의 방향·동작 시트를 만든다.
