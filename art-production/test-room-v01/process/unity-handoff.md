# 테스트 방 V01 아트 → Unity 구현 인계서

기준일: 2026-09-08  
아트 담당: Codex  
구현 담당: Claude  
패키지 상태: revision 17 알파 수정 완료, Claude 재검사 대기

## 1. 구현 입력

Unity에는 다음 두 위치만 입력한다.

- `art-production/test-room-v01/approved/`
- `art-production/test-room-v01/metadata/manifest.json`

`concept/`, `source/`, `working/`은 제작 근거와 재생산용이다. 특히 `rejected`가 붙은 생성
원본과 체크무늬가 픽셀에 구워진 파일은 Unity에 임포트하지 않는다.

## 2. 고정 규격

- 프로덕션 투영: `ReferenceTopDown`
- 납품 해상도: 128 pixels per cell
- 피벗 좌표계: 이미지 좌상단 원점, X 오른쪽 증가, Y 아래 증가
- Unity 정규화 피벗: manifest의 `pivotNormalized`를 사용하고 `pivotPixels`와 대조한다.
- Albedo: sRGB, RGBA 8-bit
- Normal·Emission·Mask·AO: Linear, RGBA 8-bit
- 필터와 압축은 픽셀 검증 중 Bilinear/Compression None으로 시작하고 최종 플랫폼 설정은
  Visual Lab 비교 후 결정한다.
- 스프라이트 메시: Full Rect. 알파 트리밍으로 피벗·채널 정렬을 바꾸지 않는다.

Material Mask 의미는 R=금속, G=광택, B=습윤·결정 반응, A=효과 강도다. Emission이 검정인
자산은 비발광이다.

## 3. 패키지 구성

승인 자산은 51종이며 manifest가 요구하는 파일 235개가 존재한다.

- 바닥 6, 벽 정면 6, 벽 상단 6, 상단 림 1, 접촉 AO 1
- 아치 1, 기둥 2, 조명 3, 대형 드릴 설비 1
- 레일·파이프·케이블 6, 장식 10, 전경 오클루더 4, VFX 4

조명 자산은 `lightSockets`, 선형 자산은 `connectionPorts`, 전경 자산은 `fadeMaskPath`와
`shadowCasterFootprintCells`를 manifest에서 읽는다. 정렬은 `sortingLayerHint`와 `localOrder`를
사용하며 파일명이나 폴더명으로 추론하지 않는다.

## 4. 조립 순서

1. manifest를 읽어 채널, 캔버스, 피벗, footprint를 검증한다.
2. 바닥 → 벽 상단 → 벽 정면 → 접촉 AO → 후면 구조 → 월드 엔티티 → 전경 → VFX 순서로
   고정 방을 조립한다.
3. 벽 정면 A–F는 같은 구조 슬롯 안에서 변형으로 사용한다. 벽 상단 A–F는 구역별 매크로
   변형으로 배치해 한 화면에서 모든 패턴을 균등 반복하지 않는다.
4. `lightSockets`를 2D Light 위치의 초기값으로 쓰되, 발광 텍스처 자체가 주변광을 대체하지
   않게 한다.
5. 전경 페이드는 `fadeMaskPath`로 제한하고 전체 스프라이트를 무조건 균일 투명화하지 않는다.
6. Visual Lab에서 레퍼런스와 동일 프레이밍으로 기본광, 소등, 전경 페이드, 파괴 전후를 캡처한다.

## 5. 인계 검증

저장소 루트에서 다음 명령을 실행한다.

```powershell
& tools/art/validate-test-room-package.ps1
```

기준 결과:

```text
ManifestRevision      : 17
ApprovedAssets        : 51
ValidatedFiles        : 235
DeliveryPixelsPerCell : 128
Result                : PASS
```

Unity에서 처음 확인할 항목은 벽 상단/정면 접합선, 접촉 AO 위치, 발점 정렬, 전경 페이드,
Normal Y 방향, Linear 채널의 sRGB 비활성화다. 문제가 생기면 승인본을 직접 수정하지 말고
자산 ID, 캡처, 기대값, 실제값을 Codex 아트 트랙에 전달해 manifest revision으로 교체한다.

## 6. 책임 경계와 다음 게이트

Codex의 최초 패키지 생산은 revision 16에서 완료됐고, Claude의 Unity 실측으로 확인된
9개 자산의 알파 수정은 revision 17에서 처리했다. Claude는 Unity 임포트, 슬라이스,
머티리얼 연결, 방 조립, 조명·오클루전·정렬 및 자동 검사를 담당한다. 다음 아트 작업 게이트는
Claude가 실제 게임 카메라로 촬영한 고정 방 비교 캡처다. 그 전에는 임의로 대량 변형을 늘리지
않고, 캡처에서 드러난 크기·명도·접합·실루엣 문제만 수정한다.
