# Primary Match V2 비주얼 작업 인계 — 2026-09-15

## 최종 적용 상태

최상위 레퍼런스
[`tr01_primary_style_target.png`](../../art-production/test-room-v01/reference/tr01_primary_style_target.png)를
기준으로 기본 바닥·벽·연결 설비·캐릭터 그림자·조명 계층을 본편 Unity 런타임에 통합한 상태다.
마지막 Unity 작업은 지층 1의 중간톤을 레퍼런스 분포에 맞추는 조명 캘리브레이션이었다. 이후 사용자가
Editor에서 다른 작업을 계속하는 동안에는 Unity, MCP, Play Mode와 `Assets/`를 점유하지 않고
볼드 세트피스 리소스 우선 패스를 진행했다. 이후 사용자 요청에 따라 39개 자산·195개 채널을 임포트하고,
안전한 신규 38개를 기존 27개와 합친 본선 런타임 카탈로그 65개로 승격했다. 코드 생성 월드의 입구 연결
공간에 깊이별 광석 반입·환기 설비·수정 동력 구도를 배치하는 `PrimaryMatchRoomDecorator`가 연결됐다.

현재 검토 캡처:
[`qa-primary-match-v2-pass25-lighting.png`](../../art-production/test-room-v01/working/primary-match-v2/qa-primary-match-v2-pass25-lighting.png)

본선 Game View 최종 캡처:

- [`primary-match-runtime-depth1-ore-final.png`](../../unity/TunnelCrew/Captures/PrimaryMatch/primary-match-runtime-depth1-ore-final.png)
- [`primary-match-runtime-depth2-ventilation-final.png`](../../unity/TunnelCrew/Captures/PrimaryMatch/primary-match-runtime-depth2-ventilation-final.png)
- [`primary-match-runtime-depth3-crystal-final.png`](../../unity/TunnelCrew/Captures/PrimaryMatch/primary-match-runtime-depth3-crystal-final.png)

## 반드시 유지할 아트 방향

- 목표는 단순히 비슷한 분위기가 아니라 최상위 레퍼런스와 같은 구조 언어·명암 계층·재질 밀도다.
- 큰 광물, 레일, 장식물을 타일 배경 위에 독립 소품처럼 얹지 않는다. 모든 특수 요소는 바닥·벽
  토폴로지에 종속된 접점, 받침, 클램프, 끝단 또는 전환 타일을 가져야 한다.
- 아치는 아직 만들지 않는다. 사용자가 나중에 별도 단계로 진행하라고 지정했다.
- 기본 바닥은 작은 셀 반복보다 큰 직사각 석판, 긴 이음, 부분 파손과 가장자리 잔석을 우선한다.
- 캐릭터 그림자는 몸체 자식이 아니라 지면 앵커를 기준으로 접촉 AO와 짧은 방향성 투사층을 분리한다.

## 완료된 구현

### 1. 기본 환경 키트

- 작업 원본과 계약은
  [`primary-match-v2/README.md`](../../art-production/test-room-v01/working/primary-match-v2/README.md)에 있다.
- 바닥, 벽 상단, 벽 정면, 벽 림, 벽–바닥 접점 매크로를 제작했다.
- 바닥은 v5 석판형 원화를 18셀 월드 주기로 샘플링한다. 14셀 비교는 마젠타 띠 반복 때문에 폐기했다.
- 각 표면에 픽셀 정렬된 Normal/AO/Emission 12장을 생성하고 Unity 임포트 설정과 스타일 참조를 연결했다.
- `OrganicEnvironmentRenderer`는 셀마다 독립 스프라이트를 뿌리지 않고 월드 고정 UV로 큰 표면을 그린다.

### 2. 벽과 설비의 연결 구조

- 남향 벽의 실제 연속 길이에 따라 지지대가 생기고, 지지대 사이를 좌–중–우 수평 설비가 잇는다.
- 실제 암반 코너가 있을 때만 설비가 90도로 꺾이고 측벽 반복으로 이어진다.
- 시안·마젠타·앰버 서비스 기둥은 바닥 받침, 암반 접점, 세로 보강판, 상단 소켓을 가진다.
- Unity 전용 첫 방은 4차 superellipse 작업실이며, 연결 설비를 가리던 내부 Core 기둥을 비웠다.
- 아치, 독립 레일, 독립 대형 광물은 추가하지 않았다.

### 3. 캐릭터와 드론 그림자

- 플레이어·AI 크루는 지면 접촉 AO와 우하향 짧은 투사층을 사용한다.
- 적 그림자는 검은 원형 스티커 대신 암자주색 저고도 타원으로 통일했고, 도약 높이에 따라 몸과의 거리,
  크기와 불투명도가 달라진다.
- `GuardianDroneView`를 추가했다. 드론 본체는 지면에서 기본 0.78셀 떠 있고, 그림자는 지면 앵커에
  남아 bob과 날개 진동을 따라 흔들리지 않는다.

### 4. 조명 계층 — pass25 채택값

최종 캡처는 CRT를 꺼 순수 게임 렌더를 비교했다. 중앙 영역 통계는 아래와 같다.

| 지표 | 변경 전 | pass25 | 최상위 레퍼런스 |
|---|---:|---:|---:|
| 명도 0.08 미만 암부 | 73.65% | 18.25% | 18.71% |
| 명도 0.08–0.35 중간톤 | 24.21% | 76.51% | 72.37% |
| 채도 중앙값 | 0.795 | 0.658 | 0.661 |
| 명도 중앙값 | 0.0479 | 0.1372 | 0.1606 |

지층 1 채택값:

- 지면 전역광 하한 `0.22`, 벽 상단 전역광은 그 값의 `0.32`배다.
- Organic 최소광은 바닥 `0.42`, 벽 상단 `0.20`, 벽 정면 `0.28`, 림 `0.28`이다.
- 일반 SurfaceMaterialSet 최소광은 벽 상단 `0.20`, 벽 정면 `0.28`, 바닥 `0.42`, 캐릭터 `0.30`이다.
- Surface Volume은 노출 `1.45`, 대비 `2`, 채도 `-5`, Bloom `0.46/0.78`, Vignette `0.10`,
  Film Grain `0.040`이다.
- 지층 2·3·Abyss는 같은 계층을 유지하면서 노출을 `1.30/1.15/1.00`으로 단계적으로 낮췄다.

URP, HDR, 카메라 후처리, Volume 레이어 마스크, `sharedProfile` 연결과 모든 사용 파라미터의
`overrideState=true`를 QA Editor에서 확인했다.

### 5. 리소스 우선 볼드 세트피스 — Unity 미연결

최상위 레퍼런스의 디오라마 공간감과 큰 기능 형태를 기준으로 신규 원본 시트 6장·39개 변형을 만들었다.
특정 게임 자산을 복제하지 않고 스타일라이즈드 히어로 슈터의 굵은 실루엣, 넓은 베벨, 과장된 기능부,
큰 원형·사각 주형태 원칙을 적용했다.

- 바닥 레일·전이 9, 결정·채굴 흔적 9, 설비 접점 9
- 영웅 기계 4: 분쇄기, 환기 터빈, 전력 릴레이, 광차 적재소
- 전경·높이 4: 좌우 선반, 절벽 립, 서비스 플랫폼
- 대형 벽 백드롭 4: 봉인 벌크헤드, 매니폴드, 결정 가공기, 붕괴 설비
- 시트 기술 채널 24장, 128 PPU 개별 납품 채널 195장
- 국소 광원 소켓 후보 8자산 11개
- 비사각 하단 그림자 윤곽 후보 15자산, 5~12점
- 과정 이미지 01~35, 권장 배치 18개와 방 정체성 3종의 기계 판독 레이아웃

핵심 데이터:

- [`runtime-catalog-candidates.json`](../../art-production/test-room-v01/working/primary-match-v2/runtime-catalog-candidates.json)
- [`delivery-candidates/manifest.json`](../../art-production/test-room-v01/working/primary-match-v2/delivery-candidates/manifest.json)
- [`curated-diorama-layout.json`](../../art-production/test-room-v01/working/primary-match-v2/curated-diorama-layout.json)
- [`curated-room-variants.json`](../../art-production/test-room-v01/working/primary-match-v2/curated-room-variants.json)
- [`curated-room-landscape-layouts.json`](../../art-production/test-room-v01/working/primary-match-v2/curated-room-landscape-layouts.json)
- [`fixture-placement-simulation.json`](../../art-production/test-room-v01/working/primary-match-v2/fixture-placement-simulation.json)
- [`fixture-major-footprint-simulation.json`](../../art-production/test-room-v01/working/primary-match-v2/fixture-major-footprint-simulation.json)
- [`fixture-full-blueprint-footprint-simulation.json`](../../art-production/test-room-v01/working/primary-match-v2/fixture-full-blueprint-footprint-simulation.json)
- [`reference-composition-comparison.json`](../../art-production/test-room-v01/working/primary-match-v2/reference-composition-comparison.json)
- [`room-lighting-calibration-candidates-v2.json`](../../art-production/test-room-v01/working/primary-match-v2/room-lighting-calibration-candidates-v2.json)
- [`foreground-fade-mask-candidates.json`](../../art-production/test-room-v01/working/primary-match-v2/foreground-fade-mask-candidates.json)
- [`connection-port-candidates.json`](../../art-production/test-room-v01/working/primary-match-v2/connection-port-candidates.json)
- [`ore-intake-connected-route-v2.json`](../../art-production/test-room-v01/working/primary-match-v2/ore-intake-connected-route-v2.json)
- [`crystal-power-connected-bus-v2.json`](../../art-production/test-room-v01/working/primary-match-v2/crystal-power-connected-bus-v2.json)
- [`light-socket-candidates.json`](../../art-production/test-room-v01/working/primary-match-v2/light-socket-candidates.json)
- [`shadow-contour-candidates.json`](../../art-production/test-room-v01/working/primary-match-v2/shadow-contour-candidates.json)
- [`offline-verification-report.json`](../../art-production/test-room-v01/working/primary-match-v2/offline-verification-report.json)
- [`primary-match-bold-import-plan.json`](../../unity/TunnelCrew/AgentScripts/primary-match-bold-import-plan.json)
- [`ImportPrimaryMatchBoldCandidates.cs`](../../unity/TunnelCrew/AgentScripts/ImportPrimaryMatchBoldCandidates.cs)
- [`VerifyPrimaryMatchBoldCandidates.cs`](../../unity/TunnelCrew/AgentScripts/VerifyPrimaryMatchBoldCandidates.cs)
- [`InspectPrimaryMatchRoomTopology.cs`](../../unity/TunnelCrew/AgentScripts/InspectPrimaryMatchRoomTopology.cs)

권장 화면은 `diorama-process-05-curated-clean-room.png`, 재질·조명 가설은 과정 06이다. 합성기가
레이아웃 JSON을 읽도록 바꾼 뒤 두 이미지를 재생성했으며 기존 SHA-256과 픽셀 단위로 동일했다.
과정 07~10은 발점, 개별 납품 크기, 광원 소켓, 그림자 윤곽 검토 보드다. 과정 11은 같은 키트로
광석 반입·환기 설비·수정 동력의 서로 다른 방 정체성이 성립하는지 확인하는 삼분할 보드다.
과정 12~14는 이를 실제 게임 화면 비율 1920×1080으로 각각 확장한 깨끗한 디오라마다. 각 화면은
폭 45% 이상의 개방 바닥을 가지며 비지면 프랍의 알파 차단율은 오프라인 측정상 0%다.
과정 15는 보호 바닥과 `north_wall_center`, `west_wall_foot`, `southeast_screen_boundary` 등의
의미 앵커를 표시해 화면 좌표를 런타임 좌표로 오용하지 않게 하는 인계 보드다.
과정 16은 깊이 1~3 맵 생성 회귀 픽스처에서 18×12 로컬 화면을 전수 탐색한 결과다. 세 방을 세
픽스처에 교차 적용한 9개 평가가 모두 통과했고 개방 폭은 50.0~66.7%였다.
과정 17은 실제 후보 `footprintCells`로 백드롭·영웅 기계·전경 3개를 동시에 놓아 보호 바닥 침범과
상호 중첩이 없는지 확인한다. 동일한 9개 평가가 모두 통과했다.
과정 18은 여기에 서비스 기둥·중형 결정·세부 소품을 더해 각 방의 모든 비바닥 배치를 동시에 검사한다.
광석 반입 6개·19셀, 환기 설비 6개·14셀, 수정 동력 7개·17셀이 보호 바닥과 서로를 침범하지 않았고,
깊이 1~3 × 방 3종의 전체 교차 평가도 9/9 통과했다. 지면 오버레이는 비차단으로 별도 집계했다.
과정 19는 최상위 이미지와 세 방을 같은 검토 크기로 직접 비교한다. 중앙 80% 기준 세 방의 명도
중앙값은 0.1455~0.1597, 채도 중앙값은 0.5432~0.6000으로 톤 계약 안에 있으며, 오프라인 조합에서
부족한 시안·앰버 면적은 Unity Light2D·VFX 연결 시 확인할 우선 항목으로 기록했다.
과정 20~23의 첫 조명 가설은 넓은 시안 워시로 명도 중앙값이 0.192~0.209까지 올라 폐기 근거로
보존했다. 범위를 줄이고 색 농도를 되살린 과정 24~27의 V2는 세 방의 명도 중앙값을 0.176~0.184로
맞췄다. 환기실 시안은 8.10%, 광석 반입실 마젠타는 30.39%로 최상위 이미지의 강조색 계층에 가까워졌다.
과정 28은 전경 후보 3종의 흰색 실루엣 페이드 마스크다. Albedo 알파와 픽셀 단위로 동일하며 현재
런타임은 아직 이 마스크를 소비하지 않으므로 기본 195채널과 분리된 휴면 후보로 유지한다.
과정 29는 레일·설비 전환 18종의 논리 연결 포트 27개를 고정한다. 기본 수평 레일은 좌·중·우가
연속되고 파손 2종은 외부 포트와 별개로 내부 연속성을 `broken`으로 유지한다. 실제 방 토폴로지의
호환 반대편 포트가 확인되기 전에는 자동 배치하지 않는다.
과정 30~31에서 광석 반입 레일을 분쇄기 받침 아래로 155px 겹쳐 시작하도록 개선했다. 떨어져 있던
설비 흉터는 제거했고 비바닥 배치·보호 바닥은 그대로다. 휴면 방 청사진도 이 V2 프리뷰를 채택했다.
과정 32~33은 수정 동력실의 분리된 바닥 장식 두 개를 중량 배관 좌·중·우 3조각으로 바꾼다. 전력
릴레이와 80px, 결정 가공기와 495px 겹쳐 하나의 기능축으로 읽히며 휴면 청사진은
`west_relay_to_north_processor_connection` 의미 앵커로 이 V2를 채택한다.

과정 34~35는 환기 설비실의 좌우 서비스 기둥과 중앙 터빈을 6조각 `heavy_pipe` 스파인으로 연결한다.
좌우 기둥과 120px·135px, 터빈과 510px 겹치며 보호 전투 바닥 위 40px 여유를 유지한다.

V2 임포트 계획은 위 방 블루프린트 3개·배치 32개를 포함한다. 신규 참조 28개와 기존 서비스 기둥
참조 4개를 구분해 적용했다. 읽기 전용 `InspectPrimaryMatchRoomTopology.cs`로 연결 빈 공간·벽발·남측
경계·최대 개방 사각형을 먼저 확인했고, 월드 판정을 바꾸지 않는 프레젠테이션 데코레이터로 배치했다.

## 주요 파일

- 계획과 전체 캡처 판정:
  [`connected-environment-visual-upgrade-plan.md`](connected-environment-visual-upgrade-plan.md)
- 환경 렌더러:
  [`OrganicEnvironmentRenderer.cs`](../../unity/TunnelCrew/Assets/_Project/Presentation/Visual/Environment/OrganicEnvironmentRenderer.cs),
  [`OrganicEnvironmentStyle.cs`](../../unity/TunnelCrew/Assets/_Project/Presentation/Visual/Environment/OrganicEnvironmentStyle.cs)
- 런타임 조명:
  [`RunBootstrap.cs`](../../unity/TunnelCrew/Assets/_Project/Presentation/Bootstrap/RunBootstrap.cs),
  [`BuildVolumeProfiles.cs`](../../unity/TunnelCrew/Assets/_Project/Editor/BuildVolumeProfiles.cs)
- 그림자:
  [`ContactShadowRenderer.cs`](../../unity/TunnelCrew/Assets/_Project/Presentation/Visual/Lighting/ContactShadowRenderer.cs),
  [`EnemyView.cs`](../../unity/TunnelCrew/Assets/_Project/Presentation/Actors/EnemyView.cs),
  [`GuardianDroneView.cs`](../../unity/TunnelCrew/Assets/_Project/Presentation/Actors/GuardianDroneView.cs)
- 생성·임포트 보조:
  [`tools/art`](../../tools/art), [`AgentScripts`](../../unity/TunnelCrew/AgentScripts)

## 검증 상태

조명 캘리브레이션 직전 전체 회귀 결과:

- `OrganicWallSamplingTests` 17/17
- `MapGenParityTests` 16/16
- `VisualBatch2Tests` 36/36
- Unity Console 오류·경고 0

pass25 영구 반영 후 확인한 결과:

- Unity 6000.3.15f1 컴파일 성공, 컴파일 오류 0
- URP/HDR/카메라 후처리/Volume 연결 및 저장값 확인
- Play Mode 3840×2160 Game View 캡처 확인
- `OrganicWallSamplingTests` 17/17

리소스 우선 패스의 오프라인 재현 검증:

- 원본 시트 6장, 점유 셀 39개
- 과정 이미지 35장, 시트 기술 채널 24장
- 전경 실루엣 페이드 마스크 3/3 Albedo 알파 일치
- 레일·설비 전환 18자산·연결 포트 27개 계약 검증
- 광석 반입 레일 V2 분쇄기 점유부 155px 연결, 비바닥 배치 불변
- 수정 동력 중량 배관 V2, 릴레이 80px·가공기 495px 연결
- 조명 캘리브레이션 V2 명도·채도·중간톤 계약 3/3 통과
- 개별 납품 자산 39개·채널 PNG 195장
- 권장 배치 18개, 기능 광원 11개, 그림자 윤곽 15개
- 방 정체성 후보 3종, 각 영웅 기계·대형 백드롭·전경 오클루더 1개
- 맵 생성 픽스처 3개 × 방 블루프린트 3종 배치 평가 9/9 통과
- 대형 프랍 실제 점유 크기 비중첩 평가 9/9 통과
- 전체 비바닥 청사진 점유·보호 바닥 평가 9/9 통과
- `verify-primary-match-resource-first.py` 통과

본선 적용 후 최종 검증:

- 후보 임포트: 스프라이트 39개, 정렬 채널 195개, 격리 카탈로그 38개
- 본선 승격: 기존 27 + 신규 38 = 런타임 카탈로그 65개
- 깊이 1/2/3 런타임 배치: 9 / 12 / 9개
- 읽기 전용 토폴로지 프리플라이트: 연결 빈 셀 1,834개, 최대 개방 사각형 25×7, 월드 버전 불변
- 전체 EditMode 테스트 508/508 통과
- Unity Console 오류 0건

## 남은 선택 작업

1. 다른 랜덤 시드와 F8/F9 투영별로 방 위치·가림을 추가 눈검수한다.
2. 자동 생성 Material Mask를 의미 기반으로 페인트오버해 금속·결정 분리를 더 정교하게 다듬는다.
3. 보행·높이 판정 계약이 정해지면 현재 게이트된 서비스 플랫폼 1개를 별도 승격한다.
4. 아치 작업은 사용자 지시대로 계속 보류한다.

QA 임시 프로젝트 `C:\Users\vnvnc\AppData\Local\Temp\TunnelCrewQaLighting`은 정상 종료 후 삭제했다.
씬·프리팹 YAML은 직접 수정하지 않았다.
