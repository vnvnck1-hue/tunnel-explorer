# 땅굴 크루 황색 CRT UI 기능명세서

대상 프로젝트: `unity/TunnelCrew/`  
연관 기획: [`crt-ui-art-direction-plan.md`](crt-ui-art-direction-plan.md)  
기준 구현: Unity 6.3.15f1 / URP 17.3.0 / 2D Renderer

> 2026-09-12 구현 방향 갱신: 사용자 후속 요청에 따라 모니터 프리셋은 강도 단계가 아니라 Surveyor / Broadcast / Arcade / Relay / Abyss의 서로 다른 5종이다. Off와 Standard / Comfort / Photosensitive 접근성은 모니터와 별도 축으로 저장한다. 아래의 기존 강도 표는 초기 승인 강도를 해석하는 참고이며, 현재 선택 목록은 아니다. Surveyor가 Strong−의 기본 수치를 계승한다. §4와 §7의 잔상 초기값 충돌은 §7의 기본 Off를 우선하고 Abyss에서만 선택적 history RT를 사용한다. 진행 및 검증 근거는 [구현 작업 기록](implementation-worklog.md)을 따른다.

## 1. 범위와 비범위

### 범위

- 월드와 카메라 공간 UI를 함께 처리하는 최종 CRT 합성
- 런 HUD, 메뉴, 특성, 제작, 채팅, 관전, 결과 화면의 공통 UI 테마
- 효과별 강도/품질/접근성 설정
- 화면비 안전 영역과 자동 시각 회귀

### 비범위

- 게임 규칙, 시뮬레이션, 투영 프리셋 수치 변경
- 지층 Albedo/Normal/Mask 원본의 황색 재제작
- HTML 프로토타입 수정
- CRT 효과를 전제로 한 충돌 판정 또는 조준 좌표 변경

## 2. 런타임 구성

### 2.1 모듈

| 모듈 | 책임 |
|---|---|
| `CRTDisplayProfile` | 효과 파라미터와 프리셋 데이터 |
| `CRTDisplayController` | 프리셋 선택, 런타임 이벤트, 접근성 상한 적용 |
| `CRTDisplay.shader` | 곡률, 색수차, 스캔라인, 노이즈, 지터, 비네트, 인광 |
| `Full Screen Pass Renderer Feature` | 최종 컬러 버퍼에 머티리얼 1패스 실행 |
| `UiThemeProfile` | 색, 여백, 선 굵기, 폰트 크기, 애니메이션 시간 토큰 |
| `HudPresenter` 계층 | Sim 상태를 UGUI ViewModel에 복사 |
| `CrtSafeArea` | 곡률과 화면비를 반영한 UI 앵커 안전 영역 |

### 2.2 렌더 순서

1. URP 2D 월드 렌더
2. 기존 지층별 Volume: Color Adjustments, Bloom, Vignette, Film Grain
3. UGUI `Screen Space - Camera` HUD 렌더
4. `CRTDisplay.shader` 최종 합성
5. 효과 제외용 `Screen Space - Overlay` Canvas: OS/개발 도구처럼 게임 HUD가 아닌 요소가 꼭 필요한 경우에만 사용

Renderer Feature는 `After Rendering Post Processing` 계열 주입 시점을 우선 검증한다. 대상 Unity/URP 조합에서 카메라 UI가 소스 컬러에 포함되지 않으면, 월드+HUD를 동일 RenderTexture에 렌더한 뒤 프레젠테이션 카메라가 CRT 머티리얼로 Backbuffer에 출력하는 대체 경로를 사용한다.

일반 플레이 UI, 메뉴, 팝업, 채팅, 키 가이드는 모두 4번 CRT 합성 이전에 렌더한다. 게임 UI를 별도 Overlay Canvas로 빼서 왜곡을 회피하는 구성은 금지한다.

### 2.3 기존 코드 전환

- `RunBootstrap.OnGUI`의 HUD 블록을 기능 단위 Presenter로 분리한다.
- `MetaScreens.OnGUI`, `TeamOverlay.OnGUI`, `ObserverMode.OnGUI`도 같은 테마 시스템으로 옮긴다.
- Sim과 표시 데이터 계약은 변경하지 않는다.
- 전환 기간에는 `LegacyHud`와 `CrtHud`를 빌드 플래그 또는 개발 설정으로 A/B할 수 있게 한다.
- 한 화면에서 IMGUI와 UGUI가 중복 표시되지 않도록 단일 소유 플래그를 둔다.

## 3. 셰이더 기능 명세

### 3.1 UV 곡률

- 입력 UV를 화면 중심 기준 `[-1, 1]`로 변환한다.
- `uv += uv * abs(uv.yx) * curvature` 형태의 배럴 왜곡을 기본으로 한다.
- 화면비를 보정해 원형 왜곡이 21:9에서 타원으로 늘어나지 않게 한다.
- 왜곡 후 범위 밖 샘플은 `Screen black`으로 채운다.
- `Strong−` 기본값 `0.052`, 허용 범위 `0.000~0.120`.

### 3.2 색수차

- R/B 채널을 중심에서 방사 방향으로 반대 오프셋하고 G는 기준 UV를 사용한다.
- 강도는 중심 20% 구간에서 0에 가깝고 외곽으로 갈수록 증가한다.
- 기본 최대 오프셋은 1080p 기준 `0.95px`, Event Heavy 피크 `1.80px`.
- 접근성 프리셋에서는 0.

### 3.3 스캔라인

- 출력 픽셀 Y와 시간 위상을 사용해 화면 해상도에 고정된 선을 만든다.
- 카메라 줌에 따라 줄 간격이 변하면 안 된다.
- 1080p 기준 2~3px 주기, 명암 진폭 기본 `0.095`.
- UI 텍스트가 1px 선에 의해 끊기지 않도록 최소 밝기 하한을 적용한다.

### 3.4 노이즈와 지터

- 프레임별 해시/블루 노이즈를 혼합하며 RGB 독립 노이즈는 약하게 제한한다.
- 평상시 전체 화면 UV 이동은 금지한다.
- 1~3개의 좁은 수평 밴드만 X 방향으로 이동시키며 밴드 위치와 발생 간격은 결정 가능한 시드로 생성한다.
- 기본 노이즈 진폭 `0.018`, 밴드 폭 `2~8px`, 평상시 오프셋 `0~0.35px`.
- 상시 상태에서는 강한 동기선을 고정 표시하지 않는다. 평균 3~8초 간격으로 낮은 알파의 단일 밴드만 짧게 통과시킨다.
- `SignalHit(amount)` 이벤트에서 최대 120ms 동안만 피크 오프셋을 허용한다.

### 3.5 비네트와 베젤

- CRT 비네트는 곡률 이후 UV에서 계산한다.
- `Strong−` 기본 강도는 `0.36`으로 한다.
- 기존 지층 Volume의 비네트와 곱해 과도하게 어두워지지 않도록 최종 최소 휘도를 둔다.
- HUD 안전 영역 안쪽에서는 비네트가 핵심 텍스트 대비를 WCAG식 단순 명도비 4.5:1 아래로 내리지 않게 한다.
- 베젤은 셰이더의 검은 외곽과 UGUI 장식 프레임을 분리한다. 실제 입력/클리핑 영역은 셰이더 곡률과 동일한 프로파일을 쓴다.

### 3.6 황색 등급과 인광

- 월드 컬러 버퍼에는 황색/세피아 등급을 적용하지 않는다. CRT 패스 전후의 평균 hue 이동이 육안으로 발생하지 않아야 한다.
- 황색 인광은 `UiThemeProfile`을 사용하는 HUD 그래픽의 원본 색으로만 만든다.
- 최종 CRT 패스는 월드와 HUD 모두에 공간 왜곡·주사선·색수차·노이즈를 적용하지만 색상 매핑은 수행하지 않는다.
- 밝은 픽셀의 이전 프레임을 약하게 혼합하는 persistence는 선택 기능이다. 별도 history RT가 필요하므로 Comfort에서는 끈다.

## 4. `CRTDisplayProfile` 데이터

필수 필드:

```text
enabled
scanlineDensity, scanlineStrength, scanlineSpeed
aberrationPixels
curvature
vignetteStrength, vignetteRoundness, edgeFeather
noiseStrength, noiseSpeed
jitterStrengthPixels, jitterBandCount, jitterFrequency
phosphorBloom, persistence
brightness, contrast, blackFloor
safeAreaInset
allowEventGlitch
```

`Strong−` 초기값은 `curvature 0.052`, `aberrationPixels 0.95`, `scanlineStrength 0.095`, `noiseStrength 0.018`, `jitterStrengthPixels 0.35`, `vignetteStrength 0.36`, `persistence 0.11`이다.

모든 시간 변화는 `Time.unscaledTime`을 사용한다. 일시정지 메뉴에서도 화면 장치는 살아 있어야 하지만 Photosensitive에서는 시간 변화 파라미터를 0으로 고정한다.

## 5. UI 컴포넌트 명세

### 5.1 공통 `CrtPanel`

- 9-slice 배경, 1~2px 테두리, 최대 2개의 절단 모서리 장식
- 선택/경고 상태에서 테두리 인광만 변화하며 패널 전체 점멸은 금지
- 최소 내부 여백: 16px@1080p
- 표시/숨김 전환: 100~160ms의 밝기 상승/감쇠. 위치 튀김은 사용하지 않는다.

### 5.2 바이탈 패널

- 위치: 좌하단 안전 영역, 기준 크기 약 560×174@1080p
- 필수 표시: 직업/초상 아이콘, 하트+HP 막대/현재값, 탄환+탄약 막대/현재값, 열 아이콘+열 막대
- 평상시에는 직업명, `HP`, `탄`, `드릴 열`, `예열`, `코어/PULP/BLOOM` 문자열과 최대값을 표시하지 않는다.
- 보조 자원은 획득/소비 순간의 아이콘 토스트 또는 관련 제작 화면에서만 표시한다.
- HP 22% 미만은 Critical 색과 고정 경고 아이콘을 함께 사용한다. 색만으로 상태를 전달하지 않는다.
- 초상화는 원본 컬러 35% + 황색 마스크 또는 단색 아이콘 중 가독성 테스트로 결정한다.

### 5.3 장악도/보스 레일

- 위치: 상단 중앙 안전 영역
- 크루/보스 아이콘, 진행 레일, 지층 아이콘+`2/3`, 위협 아이콘만 표시한다.
- `장악도`, `목표`, `위협`, `지층` 문자열은 표시하지 않는다.
- 보스 활성 시 레일 아래에 HP를 확장한다.
- 보스 아이콘의 빨강은 Critical 보조색으로 유지할 수 있다.

### 5.4 스킬 슬롯

- 위치: 우하단, Q/E/Space 또는 현재 입력 장치에 대응한 3슬롯
- 준비, 쿨다운, 사용할 수 없음의 실루엣이 서로 달라야 한다.
- 쿨다운은 방사형보다 숫자+수직 셔터 마스크를 우선한다.
- 평상시 스킬명과 상태 문구는 표시하지 않는다. 픽토그램, 키캡, 쿨다운 마스크만 사용한다.
- 슬롯 최소 크기 96×96@1080p, 키 라벨 22px 이상.

### 5.5 로그/AI/채팅

- 평상시 로그는 숨긴다. 획득, 위험, 구조 요청처럼 행동이 필요한 이벤트만 아이콘 토스트로 잠시 표시한다.
- AI 크루는 역할색을 작은 식별 마커에만 남기고 나머지는 황색 토큰을 쓴다.
- 채팅 입력 중에도 입력창은 동일한 곡률·스캔라인 패스를 통과한다. 단, 시간 변화형 지터와 노이즈만 `Comfort` 상한으로 완화할 수 있다.

## 6. 설정과 이벤트 API

개발/설정 UI에서 제공할 항목:

- CRT 프리셋: Off / Comfort / Strong− / Strong / Event Heavy / Photosensitive
- 스캔라인 강도
- 화면 곡률
- 노이즈/지터
- 색수차
- 외곽 비네트

런타임 이벤트:

```text
SetProfile(profileId)
SetAccessibility(isPhotosensitive)
SignalHit(amount, duration)
SignalDropout(amount, duration)
SetUiFocus(isTextInput)
SetCaptureMode(enabled)
```

- 피격/보스 등장/텔레포트는 `SignalHit`을 호출할 수 있다.
- 일반 이동, 채굴 연타, 자동사격은 지터를 호출하지 않는다.
- `SetCaptureMode(true)`는 비교 캡처를 위해 시간 시드와 노이즈 위상을 고정한다.

## 7. 품질 단계와 성능

| 기능 | Comfort | Strong− | Strong | Event Heavy |
|---|---|---|---|---|
| 곡률/스캔라인 | 1패스 | 1패스 | 1패스 | 1패스 |
| 색수차 샘플 | 3 | 3 | 3 | 5 선택 |
| 노이즈 | 해시 | 해시 | 블루 노이즈 | 블루 노이즈 |
| Persistence RT | Off | Off | 선택 | On |
| 내부 해상도 | 원본 | 원본 | 원본 | 원본 |

- 기본 목표는 색수차 포함 3~5회 텍스처 샘플의 단일 패스다.
- 매 프레임 머티리얼 인스턴스 생성과 GC 할당은 금지한다.
- 프로파일 변경은 `MaterialPropertyBlock` 또는 영구 런타임 머티리얼을 사용한다.
- Render Scale이 변해도 스캔라인은 출력 픽셀 기준 밀도를 유지한다.

## 8. 안전 영역

- `safeAreaInset` 기본값은 화면 짧은 변의 2.8%.
- 곡률 Strong에서 좌우/상하 실제 잘림을 캡처해 프로파일별 인셋을 보정한다.
- OS safe area와 CRT safe area 중 더 안쪽 값을 사용한다.
- 21:9에서는 HUD를 16:9 중앙에 고정하지 않고 양쪽 여백을 활용하되, 곡률 외곽 5%에는 핵심 수치를 두지 않는다.

## 9. 테스트

### EditMode

- 프로파일 직렬화와 범위 clamp
- Photosensitive가 aberration/jitter/persistence/event glitch를 0으로 만드는지
- 해상도별 scanline 주기가 출력 픽셀에 고정되는지
- 화면비별 safe area 계산
- 기존 Sim 값과 HUD ViewModel 값의 패리티

### PlayMode

- 1280×720, 1920×1080, 2560×1440, 3840×2160
- 16:9, 16:10, 21:9
- 타이틀 → 출격 → 전투 → 특성 → 제작 → 보스 → 결과
- 키보드/마우스와 패드 전환
- 카메라 흔들림, Projection F8/F9 전환과 CRT 왜곡의 독립성
- 채팅 입력과 접근성 프리셋

### 시각 회귀

- 고정 노이즈 시드로 Off/Comfort/Strong−/Strong/Event Heavy/Photosensitive 자동 캡처
- 밝은 캐릭터, 어두운 통로, 붉은 텔레그래프, 작은 한글 본문을 포함한 장면 사용
- 월드 원색이 유지되는지 확인할 수 있도록 보라색 지층, 주황색 캐릭터, 붉은 텔레그래프를 포함한다.
- 합격 기준: 텍스트 클리핑 0, 안전 영역 침범 0, 지형 seam 추가 0, 핵심 색 구분 유지

### 성능

- 기준 PC 1080p 그래픽 품질 High에서 CRT GPU 0.7ms 목표, 1.0ms 상한
- 4K 그래픽 품질 High에서 1.8ms 목표
- 런타임 GC 0B/frame
- Persistence 사용 시 RT 메모리와 대역폭을 별도 기록

## 10. 수용 기준

- 월드와 HUD에 스캔라인, 곡률, 비네트가 연속적으로 보이며 경계에서 끊기지 않는다.
- UI 프레임, 아이콘, 막대, 키캡도 필드 타일과 같은 곡률로 휘고 같은 동기선에 함께 변위된다.
- `Strong−`는 승인 레퍼런스보다 화면 효과가 약하지만 무효과 화면과 비교하면 즉시 CRT로 인식되어야 한다.
- 색수차는 외곽 고대비 경계에서 확인되지만 중앙 텍스트를 흐리지 않는다.
- Strong에서 노이즈/지터가 시각적으로 인지되고 Photosensitive에서는 완전히 정지한다.
- HUD의 모든 기존 값과 상태가 새 UI에서도 누락 없이 표시된다.
- 기존 `IsometricProjection` 값이나 게임 좌표 변환을 CRT 기능이 변경하지 않는다.
- 모든 기능은 설정에서 즉시 Off로 전환 가능하다.
- Renderer Feature가 없는 빌드나 셰이더 오류 시 원본 화면을 그대로 출력하는 fail-open 경로가 있다.

## 11. 권장 파일 배치

```text
Assets/_Project/Presentation/UI/CRT/
  Runtime/CRTDisplayController.cs
  Runtime/CRTDisplayProfile.cs
  Runtime/CrtSafeArea.cs
  Runtime/UiThemeProfile.cs
  Shaders/CRTDisplay.shader
  Materials/M_CRTDisplay.mat
  Prefabs/HUD_CRT.prefab
  Prefabs/Widgets/*.prefab
  Textures/UI_CRT_Atlas.png
  Profiles/CRT_*.asset
Assets/Tests/EditMode/CRT/
Assets/Tests/PlayMode/CRT/
```

구현 시 기존 `RunBootstrap.cs`에 셰이더 수치나 UI 좌표를 추가 하드코딩하지 않는다. 새 시스템은 데이터 프로파일과 전용 Presenter로 분리한다.
