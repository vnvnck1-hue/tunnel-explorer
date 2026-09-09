# 레퍼런스 직결 최소 에셋 캘리브레이션 V1

기준일: 2026-09-09  
상태: `working` — 바닥 A~C, 알파 수정, 전등 A 및 실제 2D 조명 구성 완료

## 결정

- 최상위 시각 기준은 `../concept/tr01_reference_asset_calibration_board_v1.png`다.
- 보드 안의 바닥·벽·수정·드릴러 픽셀을 직접 추출한 파일만 Unity 리소스로 사용한다.
- 신규 바닥 B/C는 사용자 지시에 따라 생성했고, 최종 Unity PNG를 보드에 그대로 삽입했다.
- 보드 승인 전에는 추가 변형, 타일별 틴트·회전·반전을 적용하지 않는다.
- 원본 스타일·팔레트 참고용으로 `../reference/tr01_primary_style_target.png`를 유지한다.
- `concept/tr01_room_primary_style_concept_v2.png`와 `v3.png`는 비교 자료로만 보존한다.
- 기존 `approved/` 51종과 revision 17 manifest는 변경하지 않는다.
- 승인 대상은 보드의 바닥·벽·수정·대표 드릴러·전등 다섯 항목이며, Unity는 이 다섯 종류의 이미지를 직접 사용한다.

## 산출물

- 캘리브레이션 보드: `../concept/tr01_reference_asset_calibration_board_v1.png`
- 보드 직결 추출본: `../working/reference_calibration_v1_direct/`
- 생성 원본: `../source/reference_calibration_v1/`
- 정규화 작업본: `../working/reference_calibration_v1/`
- Unity 임포트본: `../../../unity/TunnelCrew/Assets/Art/Visual/ReferenceCalibrationV1/`
- Unity 검증 씬: `../../../unity/TunnelCrew/Assets/_Project/Scenes/ReferenceCalibrationV1.unity`
- Unity 자동 조립·캡처 도구:
  `../../../unity/TunnelCrew/Assets/_Project/Editor/BuildReferenceCalibrationPreview.cs`
- 최초 조립 캡처: `../qa/reference-calibration-v1-unity.png`
- 현재 캘리브레이션 Unity 조립 캡처: `../qa/reference-calibration-floor-variants-unity.png`
- 바닥 A 단독 6×6 반복 검사: `../qa/tr01_reference_floor_a_repeat_6x6.png`
- 바닥 A~F 혼합 6×6 반복 검사: `../qa/tr01_reference_floor_variants_6x6.png`
- 재현 스크립트: `../../../tools/art/finalize-reference-calibration-v1.py`
- 보드 직결 추출 스크립트: `../../../tools/art/extract-reference-board-assets.py`
- 최종 Unity PNG 기반 보드 조립 스크립트: `../../../tools/art/build-reference-calibration-board.py`
- 씬 직결 정리 스크립트: `../../../tools/art/normalize-reference-calibration-scene.py`

## 규격

| 후보 | Unity 캔버스 | PPU | 피벗 | 상태 |
|---|---:|---:|---|---|
| 바닥 A~C | 각 128×128 | 128 | 중앙 | working, 보드와 Unity 동일 파일 |
| 벽 A | 384×384 | 128 | 하단 중앙, y 0.02 | working set-piece |
| 수정 A | 256×256 | 128 | 하단 중앙, y 0.03 | working |
| 드릴러 A | 384×256 | 128 | 하단 중앙, y 0.03 | working scale reference |
| 전등 A | 192×256 | 128 | 하단 중앙, y 0.03 | working, 실제 Light2D 포함 |

벽·수정·드릴러는 보드의 검은 배경만 제거해 실제 RGBA 알파를 만들었다. 바닥 A는 보드의 사각
영역을 여백 없이 불투명 타일로 다시 추출했고, B/C는 동일 스타일로 새로 생성했다. 세 바닥은
모두 128×128, alpha 255이며 최종 Unity 파일을 아트보드에 그대로 삽입했다.

## Unity 검증 결과

- 1920×1080, 직교 카메라, Ground XY 화면 축 정렬로 캡처했다.
- 드릴러는 발점부터 머리까지 약 1.5셀로 읽히며, 얼굴·수염·거대 드릴이 환경보다 먼저 보인다.
- 벽·수정·드릴러의 발점과 투명 외곽이 정상이고 잘림이 없다.
- 기존 A~F 매크로 변형 검증은 이력으로만 보존한다. 현재 활성 바닥은 A/B/C 세 장이다.
- Unity 씬의 120셀은 A 30개, B 37개, C 53개로 결정적 분배되며 회전·반전·틴트는 없다.
- 바닥 세 장은 완전 불투명이라 셀 사이로 검은 카메라 배경이 보이지 않는다.
- 수정·벽·드릴러는 투명 외곽을 다시 정리해 보드 배경의 검은 덩어리를 제거했다.
- 전등 A가 우측 하단에 배치되었고 전역 `Light2D` 1개와 전등 포인트 `Light2D` 1개가
  저장된 씬과 최신 캡처를 확인했다.
- 현재 씬 파일은 바닥 타일의 회전·반전·틴트와 캐릭터/프랍의 임의 스케일을 제거해 보드 직결
  표시로 정리했다.
- 전등은 우측 하단에 배치하고 URP `Sprite-Lit-Default` 재질, 전역 2D 광원, 마젠타 포인트
  `Light2D`를 씬 오브젝트로 저장한다. 발광 중심과 실제 광원 위치는 동일하게 맞춘다.
- 전등 이외 프랍의 Normal/Emission/Mask/AO 확장은 보드 직결 Albedo 승인 뒤 진행한다.

## 현재 판정

아트보드와 Unity 씬의 승인 기준을 하나로 통합했다. 다음 승인 캡처부터는 보드 직결 Albedo를
그대로 보여주며, 그 이후에만 벽 모듈 분해, 수정 Emission, 드릴러 방향·동작 시트를 진행한다.
