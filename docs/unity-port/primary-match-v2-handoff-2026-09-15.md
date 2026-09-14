# Primary Match V2 비주얼 작업 인계 — 2026-09-15

## 중단 시점

최상위 레퍼런스
[`tr01_primary_style_target.png`](../../art-production/test-room-v01/reference/tr01_primary_style_target.png)를
기준으로 기본 바닥·벽·연결 설비·캐릭터 그림자·조명 계층을 본편 Unity 런타임에 통합한 상태다.
마지막 작업은 지층 1의 중간톤을 레퍼런스 분포에 맞추는 조명 캘리브레이션이었고, 사용자의 요청으로
회귀 테스트 도중 작업을 저장하고 중단했다.

현재 검토 캡처:
[`qa-primary-match-v2-pass25-lighting.png`](../../art-production/test-room-v01/working/primary-match-v2/qa-primary-match-v2-pass25-lighting.png)

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

사용자의 중단 요청이 `MapGenParityTests`와 `VisualBatch2Tests` 재실행 사이에 들어왔다. 따라서 다음 작업자는
코드를 더 바꾸기 전에 이 두 묶음과 Console 오류·경고를 다시 확인해야 한다.

## 다음 작업 순서

1. Unity Editor를 TunnelCrew 전용 정션 `C:\Users\Loadcomplete\TunnelCrew`로 다시 연다. 기존 원본 Editor는
   이전 동기식 명령 이후 응답이 멎어 있었으므로 재사용하지 말고, 프로젝트 경로와 Unity `6000.3.15f1`을 확인한다.
2. `MapGenParityTests`, `VisualBatch2Tests`, Console 0건을 먼저 확인한다.
3. pass25를 최상위 레퍼런스와 나란히 두고 상위 중간톤을 조금 더 올릴지 판단한다. 암부 비율과 채도는 이미
   맞았으므로, 다시 전체 노출을 올려 검정을 잃지 말고 광원 반경·표면별 최소광을 국소 조정한다.
4. 다른 시드와 F9/F8 투영에서 코너, 미러 경계, 측벽 설비가 끊기지 않는지 확장 검증한다.
5. 바닥·벽 채널을 아티스트 페인트오버하고 금속/결정 전용 Material Mask를 만든다.
6. 포즈 실루엣을 반영한 유한 그림자는 현재 2층 저비용 그림자와 A/B한 뒤에만 채택한다.
7. 아치 작업은 계속 보류한다.

QA 임시 프로젝트 `C:\Users\vnvnc\AppData\Local\Temp\TunnelCrewQaLighting`은 정상 종료 후 삭제했다.
씬·프리팹 YAML은 직접 수정하지 않았다.
