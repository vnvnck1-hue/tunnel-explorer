# TR01 최상위 레퍼런스 기본 표면 키트 V2

기준일: 2026-09-15

기준 이미지: `../../reference/tr01_primary_style_target.png`

## 결과

최상위 레퍼런스의 기본 바닥과 벽 재질을 다시 관찰해 다음 Albedo 원본 5종을 새로 제작했다.
바닥·벽 상단·벽 정면·림에는 픽셀 정렬된 Normal/AO/Emission 채널 12종도 함께 둔다.

| 파일 | 역할 | 알파 | 구성 |
|---|---|---:|---|
| `tr01_primarymatch_floor_macro_3x3_source.png` | 석판형 기본 바닥 v5 | 없음 | 18셀 월드 고정 연속 매크로. 큰 직사각 판석, 중앙 저밀도, 가장자리 잔석 집중 |
| `tr01_primarymatch_wall_top_macro_3x3_source.png` | 벽 상단 암반 | 없음 | 연속 3×3 위치형 매크로 |
| `tr01_primarymatch_wall_front_macro_3x3_source.png` | 벽 정면 | 없음 | 연속 3×3 위치형 매크로 |
| `tr01_primarymatch_wall_top_rim_3x3_source.png` | 북쪽 노출 상단 림 | 없음 | 3행 변형 × 3열 연결 조각 |
| `tr01_primarymatch_floor_wall_contact_3x3_source.png` | 벽–바닥 접점 어댑터 | 실제 알파 | 8방향 + 중앙 3×3 |
| `tr01_primarymatch_wall_supports_3col_source.png` | 벽 일체형 세로 지지대 | 실제 알파 | 3열 변형, 바닥 중앙 피벗 |
| `tr01_primarymatch_wall_conduits_3x3_source.png` | 벽 일체형 수평 설비 | 실제 알파 | 3행 계열 × 좌·중·우 연결 조각 |
| `tr01_primarymatch_service_pylons_3col_source.png` | 구도용 3색 통합 조명 기둥 | 실제 알파 | 시안·마젠타·앰버 3열, 바닥 중앙 피벗 |
| `tr01_primarymatch_wall_junctions_3x3_source.png` | 벽 일체형 코너·측벽 설비 | 실제 알파 | 3행 계열 × 좌회전·세로 반복·우회전 |
| `tr01_primarymatch_floor_rail_transitions_3x3_source.png` | 바닥 매립형 레일·설비 연결 후보 | 실제 알파 | 좌·중·우, 회전·교차, 파손 종료 3×3 |
| `tr01_primarymatch_crystal_outcrops_3x3_source.png` | 지형 일체형 결정 군집 후보 | 실제 알파 | 낮은 경계·중형 군집·채굴 흔적 3×3 |
| `tr01_primarymatch_equipment_transitions_3x3_source.png` | 석판–설비 파손 전이 후보 | 실제 알파 | 종료·코너·소켓·파손 램프 3×3 |
| `tr01_primarymatch_hero_machinery_2x2_source.png` | 볼드 영웅 프랍 후보 | 실제 알파 | 분쇄기·터빈·발전기·적재소 2×2 |
| `tr01_primarymatch_foreground_depth_2x2_source.png` | 전경·높이 레이어 후보 | 실제 알파 | 좌우 선반·하단 절벽·서비스 플랫폼 2×2 |
| `tr01_primarymatch_monumental_wall_modules_2x2_source.png` | 대형 벽 백드롭 후보 | 실제 알파 | 벌크헤드·파이프·결정 가공기·붕괴 설비 2×2 |

모든 파일은 1254×1254px다. 벽 계열은 기존 3×3 위치형 계약을 유지한다. 바닥 v5는 더 이상 418px 조각을 임의 슬라이스하지 않고, 전체 이미지를 18셀 월드 매크로로 샘플링한다.

신규 볼드 프랍 3세트는 2×2, 셀당 627×627px 후보 계약이다. 작은 장식 반복보다 한눈에 읽히는
큰 원형·사각형 주형태, 넓은 받침, 과장된 기능 부품과 두꺼운 전경 면을 우선한다. 특정 게임의
기존 프랍을 복제하지 않고 스타일라이즈드 히어로 슈터의 명확한 실루엣·데포르메 원칙만 참고했다.
현재는 오프라인 합성용이며 Unity 슬라이스·피벗·배치 빈도는 승인 뒤 확정한다.

## 왜 독립 타일 9장을 만들지 않았는가

독립 생성한 정사각 타일은 각 셀의 외곽이 스스로 닫히면서 화면에 격자 무늬를 만든다. 첫 바닥
생성에서도 이 문제가 그대로 발생해 폐기했다. 채택한 벽 상단·벽 정면과 바닥 v5는 한 장의 연속된 재질을
먼저 그리고, 보이지 않는 3×3 경계로 자르는 **위치형 매크로 타일**이다. 큰 석판, 균열, 잔석이 셀
경계를 가로지르므로 각 조각을 무작위로 섞어서는 안 된다.

런타임 선택 계약:

1. 월드 셀 좌표를 표면별 매크로 내부 좌표로 변환한다. 바닥 v5의 주기는 18셀이다.
2. 같은 매크로 블록 안에서는 원본의 공간 관계를 보존한다.
3. 현재 `EnvironmentKit.Pick()`의 임의 변형 선택에 그대로 넣지 않는다.
4. 매크로 블록 외곽에는 블렌드 또는 전이 조각이 필요하다. 생성기 재요청만으로 외곽을 완전한
   반복 경계로 만들지 못했으며, 이를 `seamless`로 승인하지 않는다.
5. 채굴 시 바뀐 셀과 연결된 매크로 조각·접점 어댑터를 함께 갱신한다.

## 3×3 슬라이스 계약

- 원본 좌표: 이미지 좌상단 원점, Y 아래 증가.
- 열 `0..2`, 행 `0..2`, 한 조각 `418×418px`.
- 이미지 좌표 사각형: `x = col × 418`, `y = row × 418`.
- Unity Sprite Editor 사각형은 좌하단 원점이므로 `unityY = (2 - row) × 418`로 변환한다.
- 불투명 매크로 세 종의 피벗: 각 조각 중앙.
- 림: 각 조각 중앙을 기본으로 하되 실제 SurfaceTopology 조립 캡처에서 높이 기준을 확정한다.
- 접점 어댑터 역할은 위에서부터 `NW/N/NE`, `W/C/E`, `SW/S/SE`다. 투명 여백을 유지한다.

Sprite Editor의 `ISpriteEditorDataProvider`로 잘라야 한다. `.meta` 파일을 손으로 편집하지 않는다.

## 시각 판단

새 세트가 이전 기본 타일보다 가까워진 점:

- 회색 석판보다 검보라·자주색 암반을 기본색으로 사용한다.
- 작은 돌·모르타르·균열이 굵은 픽셀 덩어리로 구성된다.
- 바닥보다 벽 상단이 더 거칠고, 벽 정면은 암반 층과 제한된 금속 보강재를 가진다.
- 벽–바닥 접점이 독립 검은 선이 아니라 같은 암반 조각과 자갈로 이어진다.
- 한 셀 안에서 그림이 완결되지 않고 큰 형태가 셀 경계를 넘는다.

아직 승인하지 않은 이유:

- 최상위 이미지만으로 가려진 원본 타일과 정확한 반복 패턴을 복원할 수는 없다.
- 외곽 반복 경계의 평균 RGB 차이가 내부 절단선보다 크다. 런타임 블렌드 전 검증이 필요하다.
- 생성 결과의 마젠타 광물 입자 밀도가 실제 게임 중앙 바닥에서 과한지 조립 화면으로 판단해야 한다.
- Normal/AO/Emission은 Albedo에 픽셀 정렬된 결정적 파생본이다. 조립 품질은 확인했지만 높이·재질 의미에 대한 아티스트 페인트오버와 Material Mask는 아직 필요하다.
- Unity 본편의 OrganicEnvironmentRenderer에 월드 고정 매크로 표면으로 연결했고 Play Mode/Game View를 반복 비교했다. 3셀 직접 반복, 고밀도 바닥 v3, 둥근 흙섬형 v4는 폐기했으며, 현재는 석판형 바닥 v5 18셀 미러 매크로와 벽 6~9셀 미러 매크로를 사용한다.

따라서 상태는 `working`이다. 기존 승인 리소스를 덮어쓰거나 `approved/`로 이동하지 않는다.

## 벽 일체형 지지대 계약

- 1254×1254 RGBA를 418×1254 세로 스프라이트 3개로 자른다.
- 피벗은 바닥 중앙이며, 런타임 높이는 1.75셀이다. 발판은 바닥 접점에 닿고 상단 클램프는 벽 림 위로 겹친다.
- 독립 소품처럼 무작위 살포하지 않는다. 남향 벽 정면의 연속 길이가 5셀 이상일 때 시작·끝 안쪽과 5셀 간격에만 배치한다.
- `Sprite-Lit-Default`와 `WorldEntity` 깊이 밴드를 사용하고 발판 Y로 정렬한다. 그래서 지면 색광을 받으며 캐릭터와의 앞뒤 관계도 접점 기준으로 유지한다.
- 아치가 아니며, 향후 파이프·케이블은 이 지지대의 상단/측면 소켓에 연결하는 별도 모듈로 구성한다.

## 벽 일체형 수평 설비 계약

- 1254×1254 RGBA를 418×418 스프라이트 9개로 자른다. 위에서부터 마젠타 이중 파이프,
  시안 서비스 패널, 앰버 케이블 트레이이며 각 행의 순서는 좌·중·우다.
- 좌·우 조각의 큰 암반·금속 소켓은 긴 남향 벽면의 안쪽 지지대 위치와 겹친다. 중간 셀은 같은
  행의 중앙 조각을 반복하고, 중간 지지대는 그 위에 그려 구조를 끊지 않는다.
- 셀별 난수로 계열을 바꾸지 않는다. 같은 벽 연속 구간은 좌표 해시로 한 행을 고른 뒤 끝까지
  동일한 계열을 유지한다. 짧은 벽·북/동/서 경계에는 배치하지 않는다.
- `Sprite-Lit-Default`, `WorldEntity`, 벽 구간 공통 높이 기준을 사용한다. 지질 림이 흔들려도
  설비 중심선은 직선으로 이어진다.
- 원본은 418 PPU, 중앙 피벗으로 Unity에 임포트했으며 Sprite Editor 편집 권한 검사 후
  `ISpriteEditorDataProvider`로 잘랐다. `.meta`는 직접 수정하지 않았다.

## 바닥·설비 일체형 조명 기둥 계약

- 1254×1254 RGBA를 418×1254 세로 스프라이트 3개로 자른다. 왼쪽부터 시안 방향등,
  마젠타 설비등, 앰버 작업등이다.
- 넓은 암반·강철 받침이 바닥 잔석에 묻히고, 상단 가로 소켓은 벽 수평 설비의 금속 단면을
  반복한다. 독립 토치나 자유 배치 장식물로 사용하지 않는다.
- Unity 전용 첫 방의 `PresentationLamps` 세 곳에만 순서대로 대응한다. 일반 맵 램프는 기존
  토치 경로를 유지한다.
- 418 PPU, 바닥 중앙 피벗, 런타임 높이 2.15셀이다. 광원 위치를 발광부 높이로 사용하고
  받침은 그보다 1.42셀 아래에 둔다. 깊이 정렬도 받침 Y를 기준으로 한다.
- `Sprite-Lit-Default`와 `WorldEntity`를 사용한다. Sprite Editor의 생성·이름·사각형·피벗
  capability를 모두 확인한 뒤 `ISpriteEditorDataProvider`로 잘랐으며 `.meta`는 직접 수정하지 않았다.

## 벽 일체형 코너·측벽 설비 계약

- 1254×1254 RGBA를 418×418 스프라이트 9개로 자른다. 위에서부터 마젠타·시안·앰버이며,
  각 행은 좌회전·세로 반복·우회전이다. 수평 설비와 동일한 행을 사용하므로 코너에서 색과
  금속 단면이 바뀌지 않는다.
- 임의의 벽 끝에 붙이지 않는다. 남향 정면이 수평 설비와 같은 5셀 이상 길이를 가지고,
  그 끝의 암반이 바깥 빈 셀을 끼고 위쪽으로 2셀 이상 실제 연속할 때만 회전 조각을 놓는다.
  따라서 맵의 암반 토폴로지가 연결부의 존재와 방향을 결정한다.
- 회전 뒤 세로 반복은 실제 측벽 길이만큼, 최대 4셀까지만 만든다. 반복 조각의 제작 여백은
  Y 1.15배로 닫되 중심 간격은 정확히 1셀을 유지한다.
- `Sprite-Lit-Default`, `WorldEntity`, 수평 구간 공통 높이 기준과 같은 색상 행을 사용한다.
  418 PPU·중앙 피벗이며 Sprite Editor capability 확인 후 `ISpriteEditorDataProvider`로 잘랐다.
- 현재 고정 QA 맵에서 회전 4개와 측벽 반복 10개가 생성되며, 아치나 레일은 추가하지 않았다.

## 정렬 표면 채널 계약

- 바닥·벽 상단·벽 정면·림마다 Albedo와 같은 1254×1254 해상도의 Normal, AO, Emission을 둔다.
  셋 모두 같은 UV와 미러 매크로 주기를 사용하므로 타일 경계에서 채널이 따로 미끄러지지 않는다.
- Normal은 넓은 석판 면과 균열을 보존하는 2단계 명도 높이장에서 굽고, AO는 절대 명도가 아니라
  주변보다 어두운 국소 공동에만 들어간다. 어두운 평면 전체를 검게 누르지 않는다.
- Emission은 밝은 중성 석재를 제외하고 자주·마젠타가 녹색 채널보다 명확히 높은 광물 픽셀만
  알파로 선택한다. 런타임의 광범위한 색상 추정 발광은 채널이 있을 때 꺼진다.
- Unity 임포트는 Normal=`NormalMap/sRGB off`, AO=`Default/sRGB off`, Emission=`Default/sRGB on + input alpha`,
  공통 Bilinear·Uncompressed·mipmap off다. 현재 스타일 참조는 12/12다.

## 이번 범위에서 제외한 것

- 아치, 캐릭터, 자유 배치 장식물
- 신규 레일·결정·파손 전이 시트의 Unity 임포트, 런타임 배치, 충돌·채굴 종료 규칙 확정
- 신규 영웅 프랍·전경·백드롭 시트의 Unity 임포트, 소팅·가림·충돌·광원 소켓 확정
- 지층 2·3·이상지대 팔레트 변형
- 신규 6세트의 기술 후보 Normal/AO/Emission/Material Mask는 생성했다. 의미 기반 아티스트
  페인트오버와 Unity 조명 반응 승인은 제외한다.
- EnvironmentKit의 기존 타일 배열 직접 교체와 씬 직렬화 변경. 현재 연결은 Resources의 OrganicEnvironmentStyle을 통한 옵트인 매크로 메시다.

## 다음 승인 게이트

1. 신규 3개 후보 시트를 셀별로 잘라 기존 바닥·벽과 합성한 오프라인 접점 보드를 먼저 만든다.
2. 레일 게이지, 회전 소켓, 결정 높이, 채굴 후 흔적 중 실제 런타임에 넣을 조각만 선별한다.
3. F8/F9 투영 프리셋에서 18셀 바닥 매크로의 외곽 대칭과 벽 높이 기준을 다시 비교한다.
4. 코너·측벽 연결부가 다른 시드와 F8/F9 투영에서도 같은 접점 높이를 유지하는지 확장 검증한다.
5. 파생 Normal/AO/Emission을 아티스트 페인트오버하고 금속·광택·결정 Material Mask를 제작한다.
6. 중앙 셀·경계 셀·모서리 셀을 채굴해 남은 면·접점·설비 종료가 자연스러운지 확인한다.
7. 최상위 이미지와 같은 화면 크기·구도로 나란히 비교한 뒤 기본 Albedo와 신규 세트피스를 승인한다.

## 오프라인 디오라마 과정 이미지

모든 이미지는 Unity 렌더 검증이 아니라 리소스의 스케일·실루엣·밀도·레이어 가설을 보는 합성본이다.

1. `diorama-process-01-rail-crystal-transitions.png` — 연결형 레일·결정·파손 접점
2. `diorama-process-02-bold-hero-machinery.png` — 대형 분쇄기·터빈·발전기·적재소 누적
3. `diorama-process-03-foreground-depth.png` — 좌우·하단 전경 가림과 높이 레이어 누적
4. `diorama-process-04-monumental-wall-backdrop.png` — 대형 벽면 기능 형태까지 넣은 밀도 상한
5. `diorama-process-05-curated-clean-room.png` — UI 없는 정돈된 기준 디오라마. 큰 프랍 3~5개와
   중앙 플레이 여백을 유지한 현재 권장 조합
6. `diorama-process-06-material-lighting-preview.png` — 05의 배치를 유지하고 마젠타 결정, 시안 설비,
   앰버 작업등에만 제한된 오프라인 발광을 더한 재질·조명 가설
7. `diorama-process-07-runtime-footpoint-review.png` — 39개 셀의 실제 알파 경계와 하단 접점 피벗
   후보를 한 화면에서 검토하는 런타임 인계 보드
8. `diorama-process-08-normalized-delivery-review.png` — 39개 개별 128 PPU 납품 후보의 캔버스,
   상대 크기와 피벗을 범주별로 검토하는 보드
9. `diorama-process-09-light-socket-review.png` — 영웅 기계·백드롭 기능부에 제한한 국소 광원
   소켓 11개의 발점 기준 위치 검토 보드
10. `diorama-process-10-shadow-contour-review.png` — 중형 결정·영웅 기계·전경·백드롭 15종의
    하단 실루엣 기반 비사각 그림자 윤곽 보드
11. `diorama-process-11-room-identity-triptych.png` — 같은 모듈 키트로 만든 광석 반입·환기 설비·
    수정 동력 3개 방. 각 방은 영웅 기계 1, 대형 백드롭 1, 전경 오클루더 1과 중앙 45% 여백을 지킨다.
12. `diorama-process-12-ore-intake-landscape.png` — 붕괴 벽·분쇄기·단일 레일이 좌측에서 우측으로
    읽히는 16:9 광석 반입실
13. `diorama-process-13-ventilation-service-landscape.png` — 대형 원형 벌크헤드와 환기 팬을 상단
    중심에 묶고 하단 플레이 공간을 비운 16:9 환기 설비실
14. `diorama-process-14-crystal-power-landscape.png` — 결정 가공기와 전력 릴레이를 양쪽 기능축으로
    분리한 16:9 수정 동력실
15. `diorama-process-15-landscape-depth-clearance-review.png` — 세 16:9 방의 보호 바닥과 Unity
    토폴로지 앵커를 함께 표시한 런타임 인계 보드
16. `diorama-process-16-map-fixture-placement-review.png` — 깊이 1~3 실제 맵 생성 픽스처에서
    18×12 화면과 9×3 이상 개방 바닥을 찾은 배치 가능성 보드
17. `diorama-process-17-major-footprint-fit-review.png` — 후보 카탈로그의 실제 점유 크기로
    백드롭·영웅 기계·전경을 보호 바닥 밖에 배치한 비중첩 검토 보드
18. `diorama-process-18-full-blueprint-footprint-fit-review.png` — 서비스 기둥·중형 결정·세부 소품까지
    모든 비바닥 배치를 포함해 보호 바닥과 상호 중첩을 검사한 전체 청사진 보드
19. `diorama-process-19-primary-reference-comparison.png` — 최상위 이미지와 세 16:9 방을 같은
    검토 크기로 놓고 중앙 80%의 명도·채도·암부·중간톤을 직접 비교한 승인 보드
20~22. 방별 광원 캘리브레이션 V1 — 넓은 시안 분리광과 앰버 보조광의 첫 범위 가설
23. `diorama-process-23-lighting-calibration-comparison.png` — 원본과 V1을 비교해 과한 명도 상승을
    확인한 보드
24~26. 방별 광원 캘리브레이션 V2 — 광원 범위를 줄이고 색 농도와 중간톤을 회복한 채택 후보
27. `diorama-process-27-lighting-calibration-v2-comparison.png` — 원본과 V2를 비교한 최종 오프라인
    조명 가설 보드
28. `diorama-process-28-foreground-fade-mask-review.png` — 전경 선반 좌·우와 하단 절벽 립의
    Albedo, 흰색 실루엣 마스크, 현재 균일 0.34 알파 가설을 비교한 보드
29. `diorama-process-29-connection-port-review.png` — 레일·설비 전환 18종의 논리적 셀 경계
    포트 27개와 파손 내부 연속성 2종을 표시한 연결 검토 보드
30. `diorama-process-30-ore-intake-connected-route-v2.png` — 레일 시작부를 분쇄기 받침 아래로
    옮기고 분리된 설비 흉터를 제거한 광석 반입실 V2
31. `diorama-process-31-ore-intake-route-comparison.png` — 장식처럼 떨어진 V1 경로와 분쇄기에서
    이어지는 V2 경로를 나란히 비교한 채택 보드
32. `diorama-process-32-crystal-power-connected-bus-v2.png` — 전력 릴레이와 결정 가공기를
    3조각 중량 배관으로 연결한 수정 동력실 V2
33. `diorama-process-33-crystal-power-bus-comparison.png` — 분리된 작은 바닥 장식 V1과 하나의
    굵은 기능축으로 정리한 V2를 나란히 비교한 채택 보드
34. `diorama-process-34-ventilation-connected-spine-v2.png` — 좌우 서비스 기둥과 중앙 환기 터빈을
    6조각 중량 배관으로 관통 연결한 환기 설비실 V2
35. `diorama-process-35-ventilation-spine-comparison.png` — 떨어져 있던 서비스 프랍 V1과 하나의
    굵은 설비 시스템으로 읽히는 V2를 나란히 비교한 채택 보드

재생성은 `tools/art/compose-primary-match-diorama.py --stage 1..6`으로 한다. 이 스크립트는
`art-production/`과 기존 PNG만 읽으며 Unity Editor, Scene, Prefab, `Assets/` 임포트를 사용하지 않는다.
과정 11은 `tools/art/compose-primary-match-room-variants.py`가 `curated-room-variants.json`을 읽어
재생성하며 같은 격리 규칙을 따른다.
과정 12~14는 `tools/art/compose-primary-match-landscape-room-variants.py`가
`curated-room-landscape-layouts.json`을 읽어 한 번에 재생성한다.
과정 16과 `fixture-placement-simulation.json`은
`tools/art/simulate-primary-match-room-blueprints-on-fixtures.py`가 회귀 픽스처 3개를 읽어 생성한다.
과정 17과 `fixture-major-footprint-simulation.json`은
`tools/art/simulate-primary-match-major-footprints-on-fixtures.py`가 실제 `footprintCells`를 적용한다.
과정 18과 `fixture-full-blueprint-footprint-simulation.json`은
`tools/art/simulate-primary-match-full-blueprint-footprints-on-fixtures.py`가 모든 비바닥 프랍을
동시에 배치하고, 레일 같은 지면 오버레이는 보호 바닥 안의 비차단 경로로 별도 검증한다.
과정 19와 `reference-composition-comparison.json`은
`tools/art/compose-primary-match-reference-comparison.py`가 최상위 이미지와 과정 12~14를 원본 픽셀에서
읽어 생성한다. 런타임 캐릭터·VFX·카메라·최종 조명 차이는 Unity 검증 전까지 명시적으로 제외한다.
과정 24~27과 `room-lighting-calibration-candidates-v2.json`은
`tools/art/compose-primary-match-lighting-calibration.py`가 재생성한다. 과정 20~23의 V1은 과한 광원
범위를 판단한 비교 근거로 보존하며, 기존 디오라마를 덮어쓰지 않는다.
과정 28과 `foreground-fade-mask-candidates.json`은
`tools/art/build-primary-match-foreground-fade-masks.py`가 전경 3종의 Albedo 알파를 그대로 사용해
생성한다. 현재 런타임은 마스크를 소비하지 않으므로 임포트 계획에서도 휴면 후보로만 병합한다.
과정 29와 `connection-port-candidates.json`은
`tools/art/build-primary-match-connection-ports.py`가 128px 셀의 북·동·남·서 논리 포트를 기록한다.
현재 토폴로지와 호환되는 반대편 포트를 찾기 전에는 자동 배치하지 않는다.
과정 30~31과 `ore-intake-connected-route-v2.json`은
`tools/art/compose-primary-match-ore-connected-route-v2.py`가 만든다. V2 화면 위치는 오프라인
프리뷰지만 휴면 방 청사진이 채택하며 실제 월드 좌표는 계속 토폴로지 프리플라이트 뒤에 결정한다.
과정 32~33과 `crystal-power-connected-bus-v2.json`은
`tools/art/compose-primary-match-crystal-connected-bus-v2.py`가 만든다. 세 중량 배관의 호환 포트와
양쪽 대형 설비 화면 점유부 연결을 확인하고, 실제 월드 연결은 전용 의미 앵커로 잠근다.

런타임 배치 후보의 화면 예산, 셀 의미, 앵커·소팅·충돌 가정과 검증 게이트는
`placement-candidates.json`에 기계 판독 가능하게 기록했다. 이 파일 역시 런타임 설정이 아니라
과정 04의 과밀을 방지하고 과정 05의 중앙 여백을 재현하기 위한 작업 계약이다.

`tools/art/analyze-primary-match-runtime-candidates.py`는 6개 시트 39셀의 실제 알파 경계를 읽고,
각 셀의 가장 낮은 불투명 띠에서 Unity 비트림 사각형 기준 하단 접점 피벗을 산출한다. 결과
`runtime-catalog-candidates.json`에는 기존 `SetPieceDef`가 받는 `footprintCells`,
`visualHeightCells`, Sorting Layer, 접촉 그림자 반지름·윤곽, 전경 페이드 그룹·알파와 대체 자산 ID
후보가 들어 있다. 수치는 모두 `candidate_not_unity_verified`이며 `Assets/`, Scene, Prefab 또는
Editor 상태를 변경하지 않는다. 특히 서비스 플랫폼은 보행·높이 규칙이 정해질 때까지 런타임 사용을
막아 두었다.

`tools/art/build-primary-match-working-delivery.py`는 위 계약을 사용해 6개 시트를 39개 개별 자산으로
나누고 Albedo/Normal/AO/Emission/Material Mask 총 195장을 `delivery-candidates/`에 만든다.
바닥 오버레이는 128×128 한 셀을 보존하고, 영웅 프랍·전경·백드롭은 128 PPU 점유 폭과 시각 높이에
맞는 비트림 캔버스에 발점을 정렬한다. 다섯 채널은 같은 변환을 공유한다. Normal/AO 알파는 Albedo
실루엣과 같고, Emission/Mask 강도 알파는 그 실루엣 밖으로 나가지 않도록 제한했다. 이 묶음 역시
`working_candidate_not_approved`이며 승인 폴더나 Unity에 복사하지 않는다.

과정 05의 권장 구도는 `curated-diorama-layout.json`에 18개 배치로 분리했다. 배경 3, 영웅 기계 2,
단일 레일 경로, 결정 3, 전경 오클루더 2와 보류 플랫폼 1의 화면 좌표·층·앵커 의미·그림자 프리뷰
수치를 기록한다. `compose-primary-match-diorama.py`가 이 파일을 직접 읽으며, 리팩터링 전후 과정 05와
06의 SHA-256이 각각 동일해 픽셀 결과가 바뀌지 않았음을 확인했다. 화면 좌표를 실제 월드 셀로 바꾸는
작업은 Unity에서 현재 방 토폴로지를 읽은 뒤 수행한다.

`tools/art/analyze-primary-match-light-sockets.py`는 개별 Emission 채널에서 실제 밝은 군집을 찾되,
주변 광석이 기계 기능등보다 먼저 선택되지 않도록 자산별 의미 앵커에 스냅한다. 분쇄기 호퍼, 터빈
허브, 릴레이 코어·표시관, 적재소 앰버 작업등, 벌크헤드 중앙 패널, 매니폴드 관, 결정 가공기 코어·
패널과 붕괴부의 약한 광물광만 남겨 8개 자산 11개 소켓이다. `light-socket-candidates.json`에
`Worklamp`·`MineralGlow`·`Indicator`, 색, 반경, 세기와 발점 오프셋을 기록했으며, 지면 오버레이와
분산 결정 시트에는 Light2D 소켓을 만들지 않는다.

`tools/art/build-primary-match-shadow-contours.py`는 Unity의 `SpriteAlphaContour`와 같은 의도로
Albedo 전체 외곽이 아니라 발점 위의 점유 깊이 띠만 스캔한다. 0.35 알파 컷, 최대 24점 규칙으로
중형 결정 3, 영웅 기계 4, 전경·플랫폼 4, 백드롭 4의 총 15개 윤곽을 만들었다. 실제 결과는 5~12점이며
점유 폭·깊이 +0.35셀 가드 안에 있다. `shadow-contour-candidates.json`과 과정 10 보드에서 좌우 결정의
기울기, 전경 선반의 파인 내부와 기계 밑동을 확인할 수 있다. 실제 투사 방향·길이는 Unity 광원 검증 전
후보 상태다.

`curated-room-variants.json`은 단일 쇼케이스 구도를 세 가지 방 정체성으로 확장한다. 광석 반입은
붕괴 설비·분쇄기·단일 레일, 환기 설비는 봉인 벌크헤드·대형 팬·시안 작업등, 수정 동력은 결정
가공기·전력 릴레이·제어 소켓을 중심으로 한다. 과정 11은 같은 자산군이 반복 배치처럼 보이지 않고
기능과 실루엣이 다른 공간으로 읽히는지 확인하는 오프라인 보드다.

오프라인 최종 검증 결과는 `offline-verification-report.json`에 저장한다. 원본·과정 이미지·채널·
개별 납품·레이아웃·방 정체성·광원·그림자 계약의 개수와 SHA-256, 남은 런타임 게이트를 포함한다.
보고서는 오프라인 계약뿐 아니라 Unity 스테이징 영수증, 임포트·승격 보고서, 읽기 전용 토폴로지
프리플라이트와 깊이 1~3 Game View 캡처를 함께 검사한다. 현재는
`runtimeIntegrationVerified=true`, `unityImportPerformed=true`이며 본선 런타임 카탈로그 65개를 확인한다.
같은 적용 상태에서 두 번 생성한 최종 보고서 SHA-256은
`9235082BDD9BA702F446DFF2EE5B34971F0E0646F959C04E1179E075ACB5DA96`로 동일했다.

Unity 재개 준비는 `tools/art/build-primary-match-unity-import-plan.py`가
`unity/TunnelCrew/AgentScripts/primary-match-bold-import-plan.json`에 통합한다. 39개·195파일 전체,
권장 구도 사용 15개, 런타임 카탈로그 대상 38개(지면 오버레이 24개 포함), 보류 플랫폼 1개와 광원 11개·
그림자 15개를 구분한다. V2 계획에는 방 블루프린트 3개·배치 32개도 포함하며, 신규 후보 참조 28개와
기존 서비스 기둥 참조 4개를 의미 앵커에 연결한다. `stage-primary-match-bold-candidates.ps1`는 기본이
dry-run이며 `-Apply` 없이는
복사하지 않는다. 이후 `ImportPrimaryMatchBoldCandidates.cs`와 `VerifyPrimaryMatchBoldCandidates.cs`가
후보 전용 폴더와 격리 카탈로그를 먼저 만들고 검증한 뒤, 기존 27개와 신규 38개를 합친
`SetPieceCatalog_PrimaryMatchRuntime` 65개 항목으로 승격했다. Scene·Prefab은 변경하지 않았고,
`RunBootstrap`의 코드 생성 환경에 `PrimaryMatchRoomDecorator`를 연결했다.
계획 생성기는 깊이 1~3 픽스처의 방 블루프린트 평가 9/9 통과 보고서도 필수 소스 계약으로 검사한다.
대형 백드롭·영웅 기계·전경의 정확한 점유 크기 비중첩 평가 9/9도 별도 필수 계약이다.

`InspectPrimaryMatchRoomTopology.cs`는 이후 Editor가 비었을 때 실행할 읽기 전용 프리플라이트다. 현재
`WorldGrid`의 연결된 빈 공간, 북·서·동 벽발, 남측 전경 경계와 최대 축정렬 개방 사각형을 계산해
`AgentScripts/primary-match-room-topology-preflight.json`만 쓴다. Spawn·Clear·Damage·SetTile·Scene/
Prefab 저장·Asset 생성 호출이 없는지 오프라인 검증기가 검사한다. 깊이 1에서 실행해 80×72 월드,
입구 연결 빈 셀 1,834개, 최대 개방 사각형 25×7을 확인했으며 월드 버전은 `[0, 0]`으로 변하지 않았다.

본선 Play Mode에서는 입구 연결 영역의 16×7 구간에 깊이별 구도를 배치한다. 깊이 1 광석 반입실
9개, 깊이 2 환기 설비실 12개, 깊이 3 수정 동력실 9개가 생성됐고 각각 Game View 캡처로 확인했다.
전경 오클루더 페이드, 확대 비율과 같은 비율의 접촉 그림자·캐스터 윤곽도 활성화했다. 전체 EditMode
회귀 테스트는 508/508 통과, 최종 Console 오류는 0건이다.

## 신규 리소스 기술 후보 채널

`tools/art/bake-primary-match-resource-channels.py`가 신규 6세트에 픽셀 정렬된 Normal, AO,
Emission, Material Mask를 각각 생성한다. 결과 24장은 `channels/resource-first/`에 있으며
`report.json`이 파일별 SHA-256을 기록한다. Material Mask는 확정 규약 `R=금속`, `G=광택`,
`B=습윤·결정`, `A=효과 강도`를 따른다.

이 채널은 자동 생성된 기술 채널이므로 의미 기반 페인트오버의 여지는 남아 있다. 일반 설비의 B 채널
점유는 약 7~11%, 전경 23.5%, 결정 전용 시트 45.4%다. 195개 채널은 Unity에 임포트했고, 본선
Game View에서 2D Light 소켓·재질 반응을 확인했다.

생성 호출과 폐기 사유는 `generation-log.md`, 기계 판독 규격은 `manifest.json`에 기록했다.
