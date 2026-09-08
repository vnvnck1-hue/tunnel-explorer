# 비주얼 오버홀 구현 기록 (Claude 구현 트랙)

기준 문서: `docs/unity-visual-overhaul-functional-spec.md`
아트 계약: `art-production/test-room-v01/process/production-spec.md`
대상: `unity/TunnelCrew/Assets/_Project/Presentation/Visual/`

이 문서는 기능명세서의 어느 조항이 어떤 파일로 구현됐는지, 그리고 명세를 읽는 과정에서
확정하거나 조정한 판단을 남긴다.

## 1. 배치 1 — 기반 계층 (2026-09-08)

기능명세서 §19 의 착수 순서 중 아트에 의존하지 않는 3·4번을 먼저 만들었다. 승인 아트가
`art-production/test-room-v01/approved/` 에 들어오기 전에 기술 구조를 확정해 두어야
"기술 구조가 확정되지 않은 상태에서 대량의 아트를 다시 만드는 일"(§19)을 피할 수 있다.

| 조항 | 파일 |
|---|---|
| §6.4 렌더 레이어 12종 | `Visual/VisualLayers.cs`, `Editor/BuildVisualLayers.cs` |
| §6.5 발 위치 정렬 | `Visual/Depth/DepthSort.cs`, `VisualHeightAnchor.cs`, `FootpointSorter.cs` |
| §6.6 전경 오클루전·페이드 | `Visual/Depth/ForegroundOccluder.cs`, `ForegroundFadeController.cs` |
| §6.2 제작 기준 프로파일 | `Visual/Projection/WorldVisualProfile.cs` |
| §6.3 표면 생성 | `Visual/Environment/SurfaceTopology.cs`, `SurfaceTopologyBuilder.cs`, `SurfaceRules.cs`, `ISolidField.cs`, `WorldGridSolidField.cs` |
| §6.7 파괴 dirty 갱신 | `SurfaceTopologyBuilder.DirtyChunks`, `EnvironmentChunkRenderer.MarkCellDirty` |
| §6.4 다층 환경 렌더러 | `Visual/Environment/EnvironmentChunkRenderer.cs` |
| §8.3·§12.1 지층 키트·규칙 자산 | `EnvironmentKit.cs`, `SurfaceRuleSet.cs` |
| §12.3 Visual Lab | `Visual/Debug/VisualLabController.cs`, `Editor/BuildVisualLab.cs` |
| §12.4 디버그 오버레이 | `Visual/Debug/DepthDebugOverlay.cs` |
| §16.1 EditMode 테스트 | `Assets/Tests/EditMode/SurfaceTopologyTests.cs`, `DepthSortTests.cs` |
| §16.3 시각 회귀 캡처 seed | `Editor/CaptureVisualLab.cs` |

메뉴:

- `Tunnel Crew/비주얼 · 소팅 레이어 생성 (§6.4)`
- `Tunnel Crew/비주얼 · 임시 환경 아트 생성 (128px/셀)`
- `Tunnel Crew/비주얼 · Visual Lab 씬 생성 (§12.3)` — 위 둘을 포함해 한 번에 세운다
- `Tunnel Crew/비주얼 · Visual Lab 캡처 세트`

Visual Lab 조작: 이동 WASD · 파괴 X · 복구 C · 전경 페이드 토글 F · 디버그 오버레이 F1/F2.

## 2. 배치 1 에서 확정한 판단

### 2.1 벽은 cap 을 올리고 정면이 그 아래를 채운다

아트 규격 §6 의 "벽 정면은 점유 셀의 경계선에 발점을 둔다. 위로 솟은 픽셀은 footprint 에
포함하지 않는다"를 다음으로 구현했다.

- 고체 셀의 **cap** 은 화면 위로 `wallLiftCells` 만큼 올려 그린다.
- 남쪽이 열린 셀의 **정면** 은 셀의 남쪽 경계선(y = row)에 발점을 두고 lift 높이만큼 솟는다.
- 두 표면이 정확히 맞닿으므로 연결된 벽 덩어리에 이음새가 없다.

수직 벽 기둥의 가장 남쪽 셀에서 cap 이 올라가 생기는 빈칸은 항상 그 셀의 정면이 채운다
(남쪽이 비어 있으니 정면 조건이 성립한다). 즉 구조적으로 구멍이 날 수 없다.

### 2.2 cap lift 는 맵 전체에서 하나다

기능명세서 §8.6 은 정면 높이를 0.75~1.5셀로 준다. 이것을 **셀 단위 변화로 구현하지 않는다.**
셀마다 높이가 다르면 연결된 벽의 cap 이 어긋나 seam 이 생긴다. 0.75~1.5 는 지층·키트 단위
선택폭으로 해석하고, 3셀 이상 영웅 구조물은 footprint 를 예약하는 세트피스로 따로 처리한다
(§8.7). `SurfaceRules.WallLiftCells` 에 주석으로 남겼다.

### 2.3 §6.4 레이어 분리와 Y 정렬은 서로 모순되지 않는다

한 칸 두께 벽은 남쪽 방의 "북쪽 벽"이면서 북쪽 방의 "남쪽 벽"이다. 레이어를 하나만 줄 수
없어 보이지만, 두 표면이 서로 다르다.

- 남쪽 경계의 **정면** → `BackStructure`(4). 남쪽 방의 캐릭터(6)보다 뒤.
- 북쪽이 열린 셀의 **cap** → `FrontStructure`(7). 북쪽 방의 캐릭터(6)보다 앞.
- 북쪽이 막힌 셀의 cap → `WallTop`(5).

lift ≥ 0.75 이므로 각 표면이 실제로 겹치는 캐릭터와의 앞뒤가 모두 이 배치와 일치한다.
`SurfaceTopologyBuilder` 의 `ForegroundTop` 플래그가 이 분류를 담당한다.

### 2.4 레이어별 Tilemap 구성이 다른 이유

겹침 여부가 다르다.

| 레이어 | 구성 | 이유 |
|---|---|---|
| GroundBase/Detail/Decal | 맵당 하나, Chunk 모드 | 서로 겹치지 않는다 → 한 배치 |
| BackStructure | 맵당 하나, Individual + TopLeft | 정면 높이가 1셀을 넘으면 북쪽 정면과 겹친다 → 아래 행이 앞 |
| WallTop | 맵당 하나, Chunk 모드 | cap 은 전부 같은 lift 라 서로 겹치지 않는다 |
| FrontStructure | **청크당 하나**, Chunk 모드 | 페이드 그룹을 청크 단위로 만든다(§6.6 "조각조각 사라지지 않게") |

### 2.5 임시 아트

`BuildVisualPlaceholderArt` 가 만드는 단색 자산은 아트 트랙 산출물이 아니다. 다만 규격은
최종과 같게(128 PPU, 정수 픽셀 캔버스, 정면의 하단 중앙 피벗) 맞췄으므로 승인 아트로
교체할 때 배치가 흔들리지 않는다.

## 3. 배치 1 검증

### 3.1 EditMode

`TunnelCrew.Tests.EditMode` 176/176 통과 (기존 158 + 신규 18, 2026-09-08).

신규 fixture: 직선 통로, 섬, 좁은 통로, 한 칸 두께 벽, 볼록·오목 모서리, 파괴 전후 모듈
재선택, 호출 순서 무관 결정성, 매크로 해시 블록 경계(음수 좌표 포함), 청크 dirty 전파,
발 위치 sorting key 단조성.

### 3.2 캡처

`docs/unity-port/img/visual-lab/` — 플레이 모드 없이 에디터에서 렌더한다.

| 파일 | 확인 항목 |
|---|---|
| `visual-lab-room-overview.png` | 바닥 / 벽 정면 / 벽 상단 / 전경이 서로 다른 높이층으로 읽힌다 |
| `visual-lab-base-camera.png` | 기본 카메라 거리에서의 피사체 비율 |
| `visual-lab-behind-foreground.png` | 남쪽 벽 cap 이 캐릭터 하반신을 가린다 (§6.6) |
| `visual-lab-behind-pillar.png` | 중경 기둥과 캐릭터의 발 위치 정렬 |

## 4. 배치 1 에서 고친 실제 결함

1. **직교 이웃만 보고 벽 내부를 컬링했다.** 대각 하나가 빈칸이면 그 오목 모서리에서 cap
   귀퉁이가 드러나므로 한 조각짜리 구멍이 남는다. `Buried` 판정을 8이웃으로 바꿨다.
   EditMode 테스트가 잡았다.
2. **Sorting Layer `uniqueID` 가 0 으로 저장됐다.** FNV 해시를 `int` 로 캐스팅하면 음수가
   되고, `SerializedProperty.intValue` 는 음수를 0 으로 되돌린다. 0 은 "레이어 없음"과 같은
   값이라 `BackStructure` 등 4개 레이어를 지정한 렌더러가 경고 없이 `Default` 로 떨어졌다.
   31비트로 자르고, 이미 0 으로 저장된 항목을 복구하는 경로를 넣었다.
3. **`SortingLayer.NameToID` 로 레이어 존재를 판정했다.** 그 함수는 인덱스가 아니라
   uniqueID 를 돌려주므로 "없음"과 "ID 가 0 인 레이어"를 구분할 수 없다.
   `VisualLayers.Exists` 로 이름 조회하게 바꿨다.

## 5. 배치 2 — 그림자·채널·카메라·아트 인계 (2026-09-08)

배치 1 의 기반 계층 위에 §7.4 그림자, §7.1 재질 채널, §10 카메라 프로파일, §12.2 임포트
검사기를 올렸다. 본선 씬은 이 배치에서도 바꾸지 않는다 — `RunBootstrap` 은 여전히 기존
`WorldRenderer` 와 `WallShadowBuilder` 를 쓰고, 신규 시스템은 Visual Lab 에서만 돌아간다.

| 조항 | 파일 |
|---|---|
| §7.4-3 벽 footprint 윤곽 추적 | `Visual/Lighting/WallContourTracer.cs` |
| §7.4-3 윤곽 → ShadowCaster2D | `Visual/Lighting/ShadowGeometryBuilder.cs` |
| §7.4 세트피스 캐스터 확장 지점 | `IShadowContourSource` (같은 파일, 인터페이스만) |
| §7.4-1 접촉 AO | `Visual/Lighting/ContactShadowRenderer.cs` (`ContactShadow` 컴포넌트 포함) |
| §7.1 통합 Lit 재질 | `Shaders/WorldLit.shader`, `Shaders/CharacterLit.shader` |
| §7.1 채널 로직 공유 | `Shaders/TunnelCrewLit2D.hlsl`, `TunnelCrewLit2DNormal.hlsl`, `TunnelCrewLit2DProps.hlsl` |
| §7.1·§12.1 채널 자산 | `Visual/Materials/SurfaceMaterialSet.cs` |
| §12.3 채널 단독 보기 | `Visual/Materials/VisualChannelDebug.cs` |
| §10 카메라 프로파일 | `Visual/Projection/CameraViewMode.cs`, `WorldVisualProfile.ViewCellsFor` 외, `Presentation/Camera/CameraRig.cs`(가산) |
| §12.2 임포트 검사기 | `Editor/ArtPipeline/ApprovedArtValidator.cs`, `ApprovedArtContract.cs`, `PngInfo.cs`, `MiniJson.cs` |
| §12.2 승인 아트 임포트 | `Editor/ArtPipeline/ApprovedArtImporter.cs`, `ArtPackageLocator.cs` |
| §12.4 디버그 선(씬 지오메트리) | `Visual/Debug/VisualDebugLines.cs` |
| §12.3 Visual Lab 확장 | `Visual/Debug/VisualLabController.cs`, `DepthDebugOverlay.cs` |
| §16.1 신규 EditMode 테스트 | `Tests/EditMode/WallContourTests.cs`, `ApprovedArtValidatorTests.cs`, `VisualBatch2Tests.cs` |

신규 메뉴:

- `Tunnel Crew/비주얼 · 승인 패키지 경로 지정…`
- `Tunnel Crew/비주얼 · 승인 아트 검사 (§12.2)`
- `Tunnel Crew/비주얼 · 승인 아트 임포트 → EnvironmentKit`

Visual Lab 조작(갱신): 이동 WASD · 파괴 X · 복구 C · 전경 페이드 F · 접촉 AO G ·
탐색광 L(위치는 마우스) · 채널 단독 보기 Z · 카메라 프로파일 V · 디버그 오버레이 F1/F2.

## 6. 배치 2 에서 확정한 기술 판단

### 6.1 안쪽 경계 제거는 셀 사이 변을 만들지 않는다는 뜻이다

지시는 "안쪽 경계는 제거하고 외곽 경계만 사용" 이었다. 두 가지로 읽힐 수 있다.

1. 벽 셀과 벽 셀 사이의 변을 만들지 않는다.
2. 부호 면적이 음수인 고리(방·통로를 둘러싸는 경계)를 버린다.

**1번으로 구현했다.** 2번을 택하면 절차 맵에서 실내 벽 그림자가 전부 사라진다 — 파낸 방을
제외한 고체 영역이 대부분 하나로 이어져 있어서, 그 덩어리의 바깥 고리는 맵 테두리이고
방·통로의 경계는 모두 음수 고리가 된다. `WallContourTests` 의
`방_경계_고리를_버리면_실내_벽_그림자가_사라진다` 가 이 상황을 고정해 둔다
(테두리 1 + 기둥 1 + 방 경계 1 = 세 고리를 모두 캐스터로 만들어야 한다).

경계 변 추적은 애초에 벽-벽 사이 변을 만들지 않으므로 1번은 자동으로 만족된다. 기존
`WallShadowBuilder` 의 탐욕적 사각형 분할이 인접 사각형 사이에 남기던 이음새 변이 사라진다.

방향 규약은 모든 고리에서 "벽이 진행 방향의 왼쪽" 으로 통일했다. 부호 면적은 바깥 고리를
구분하는 정보로만 쓰고(디버그 색 구분), 캐스터 생성에서는 버리지 않는다.

### 6.2 형태 해시는 내용에서만 만든다

기존 `WallShadowBuilder.ApplyShape` 는 `m_ShapePathHash` 에
`path.GetHashCode() ^ Environment.TickCount` 를 넣었다. 같은 형태에서도 매번 값이 달라져
"바뀐 캐스터만 다시 만들기" 가 불가능하다. `WallContourTracer.HashOf` 는 정점 좌표만으로
FNV 해시를 만들고, `ShadowGeometryBuilder` 는 그 해시를 키로 살아 있는 캐스터를 추적해
**해시가 바뀐 것만** 파괴·생성한다.

### 6.3 그림자 추적은 프레임에 한 번으로 병합한다

청크 단위 부분 추적을 하지 않는다. 청크 경계에서 벽이 이어지는데 청크 밖을 빈칸으로 보면
덩어리 내부에 가짜 경계 변이 생기고, 그 변이 그림자를 드리운다(기존 코드가 "빛이 타일
경계에서 직선으로 잘리는" 버그로 겪은 것과 같은 계열). 대신 셀 변경을 dirty 플래그로 모아
LateUpdate 에서 전체를 한 번만 추적한다. 추적 자체는 경계 변만 훑어 O(셀) 이고 사전 없이
(셀, 변) 인덱스로 O(1) 조회하므로 20×14 방에서 네 고리를 만드는 데 무시할 만한 비용이다.
캐스터 생성만 `_castersPerFrame` 예산으로 나눈다(§6.7 "2~3프레임 안에").

큰 절차 맵에서 이 방식이 부족해지면 연결 성분 단위 부분 추적으로 올려야 한다 — 절차 맵
연결 배치의 확인 항목으로 남긴다.

### 6.4 URP 2D 호환 방식은 손으로 쓴 HLSL 이다

Shader Graph 가 아니라 URP 17.3 의 2D 인클루드에 직접 얹었다. 근거:

- 이 프로젝트에 이미 같은 방식의 선례가 있다(`Shaders/Tilemap-Lit-Normal.shader`).
  Tilemap 청크 메시에 NORMAL/TANGENT 가 없어 TBN 이 0 이 되는 문제를 셰이더 안에서
  탄젠트를 강제해 해결한 코드이고, `WorldLit` 이 같은 처방을 이어받는다.
- Emission 은 조명 **뒤**에 더해야 하고 AO 는 조명 **앞**에 곱해야 한다.
  `CombinedShapeLightShared` 의 반환값 전후에 손을 넣는 것이 정확하다.
- 채널 단독 보기를 전역 유니폼 하나로 처리하려면 프래그먼트를 직접 쓰는 편이 단순하다.

채널이 들어가는 자리:

| 채널 | 자리 |
|---|---|
| AO | 조명 전에 Albedo 에 곱한다 — 들어오는 빛을 감쇠시키는 것이 물리적으로 맞다 |
| Material Mask | `surfaceData.mask` 로 넘긴다. URP 가 Light Blend Style 의 마스크 필터에 쓴다 |
| Normal | Lit 패스가 아니라 `NormalsRendering` 패스에서 쓴다(URP 가 노멀 버퍼를 따로 굽는다) |
| Emission | `CombinedShapeLightShared` 결과에 더한다. 광원에 곱해지면 안 된다 |
| MinLight | 조명 결과와 `max` 를 취한다. 밝은 곳을 더 밝게 만들지 않는다(§7.2) |

Albedo 에 최종 조명을 굽지 않는다. `_MinLight` 는 §7.2 의 표면별 최소광이며
`WorldVisualProfile.minLightBySurface`(xyzw = 벽상단/벽정면/바닥/캐릭터)에서
`SurfaceMaterialSet.minLightSlot` 으로 골라 온다.

**현재 Renderer2D 의 Light Blend Style 은 4종이고 마스크 필터는 R 채널만 쓴다**
(`Multiply with Mask`, `Additive with Mask`). 즉 아트 규격 §5 의 Mask R(금속)만 조명 훅이
있고 G(광택)·B(습윤·결정)·A(효과 강도)는 아직 셰이더 안에서만 읽힌다. 블렌드 스타일 추가는
프로젝트 전역 렌더 설정 변경이라 본선 조명 수치를 확정하는 배치로 미룬다.

### 6.5 디버그 선은 OnGUI 가 아니라 씬 지오메트리로 그린다

자동 캡처는 플레이 모드에 들어가지 않는다(§12.3·§16.3). 에디터에서는 `OnGUI` 가 불리지
않고, 불러도 화면 GUI 로 가서 렌더 텍스처에 남지 않는다. 그래서 그림자 윤곽·발 위치·페이드
그룹은 `VisualDebugLines` 가 `MeshTopology.Lines` 메시로 그려 `WorldOverlay` 레이어에 올린다.
`Camera.Render` 에 그대로 들어오므로 캡처와 플레이 모드가 같은 화면을 낸다. 텍스트 읽을거리는
`DepthDebugOverlay` 의 GUI 가 계속 담당한다.

### 6.6 카메라 프로파일은 가산이고 기본값은 기존 동작이다

`CameraRig._profile` 은 비어 있으면 기존 `SimTuning` 공식을 그대로 쓴다. 본선 씬을 이 배치에서
바꾸지 않기 위한 선택이다. 값 결정 로직은 `WorldVisualProfile.ViewCellsFor(CameraViewMode)`
하나로 모으고 `CameraRig` 와 Visual Lab 이 같은 함수를 부른다 — 한쪽만 바뀌어 "Lab 에서는
맞는데 게임에서는 다른" 상태가 되는 것을 막는다.

`CharacterRatioInRange` 는 §3.3 의 13~18% 목표를 판정한다. 현재 프로파일 값에서 캐릭터
1.35~1.5셀은 기본·근접 줌에서 범위 안이고 협동 줌아웃에서는 범위 아래로 내려간다 —
명세가 의도한 동작이며 테스트로 고정했다.

### 6.7 정션으로 열린 프로젝트에서는 저장소 경로를 자동으로 찾을 수 없다

이 프로젝트는 유니티 MCP 인식 문제(한글 경로) 때문에 정션 `C:\Users\Loadcomplete\TunnelCrew`
로 열도록 돼 있다. 그때 `Application.dataPath` 와 현재 작업 폴더가 모두 정션 경로가 되고,
위로 올라가도 저장소 루트에 닿지 않는다(`C:\Users\Loadcomplete` 에는 저장소가 없다).
.NET Standard 2.1 에는 재파스 포인트를 따라가는 API 가 없다.

`ArtPackageLocator` 가 이 순서로 찾는다: 커맨드라인 `-artPackageRoot` → EditorPrefs
(`tc.visual.artPackageRoot`) → `Application.dataPath` 상위 탐색 → 작업 폴더 상위 탐색.
어느 것도 못 찾으면 기대 위치를 돌려주고 검사기가 "manifest 없음" 오류로 보고한다 —
조용히 통과하지 않는다. **정션으로 여는 환경에서는 `비주얼 · 승인 패키지 경로 지정…` 메뉴로
1회 설정이 필요하다.** 캡처 출력 폴더도 같은 방식으로 찾는다.

### 6.8 sortingLayerHint 는 세 상태로 나눈다

manifest 는 `WorldStructure` 를 쓰는데 §6.4 표에는 없는 이름이다. 별칭까지 경고로 올리면
아트가 그 표현을 계속 쓰는 동안 리포트가 경고로 가득 찬다. 반대로 매핑 불가를 조용히
넘기면 렌더러가 소리 없이 기본 레이어로 떨어진다. 그래서 `HintMatch` 를 `Exact`(§6.4 이름
그대로) / `Alias`(결정적 매핑, 참고) / `Unknown`(경고) 로 나눴다.

### 6.9 등록만 하는 컴포넌트는 ExecuteAlways 다

`VisualHeightAnchor`·`ContactShadow`·`ForegroundOccluder` 에 `[ExecuteAlways]` 를 붙였다.
코드로 붙인 일반 MonoBehaviour 는 플레이 모드가 아니면 `OnEnable` 이 불리지 않아 정적
등록 목록에 들어가지 않는다. 그러면 자동 캡처에서 발 위치·접촉 AO·전경 페이드가 통째로
빠진다. 매니저(`FootpointSorter`·`ForegroundFadeController`·`ContactShadowRenderer`)에는
붙이지 않는다 — 캡처가 명시적으로 틱을 돌리는 것과 충돌한다.

## 7. 배치 2 검증

### 7.1 컴파일과 콘솔

Unity 6000.3.15f1 / URP 17.3.0 에서 컴파일 오류 0, 콘솔 오류 0, 경고 0.
`WorldLit`·`CharacterLit` 은 `BuildVisualPlaceholderArt` 가 머티리얼을 만들어
`Shader.isSupported` 까지 확인한다 — 셰이더 오류가 런타임까지 숨지 않게 한다.

### 7.2 EditMode

`TunnelCrew.Tests.EditMode` **233/233 통과** (배치 1 의 176 + 신규 57, 2026-09-08).

| 파일 | 내용 |
|---|---|
| `WallContourTests.cs` | 섬·직선·L자·떨어진 덩어리·대각 접점·좁은 통로·맵 꽉 찬 벽·벽 없음, 방 경계 음수 고리, 음수 고리를 버리면 실내 그림자가 사라짐, 결정성(고리 수·순서·정점·시작 변·해시), 내용 해시가 형태에만 의존, **파괴 전후 윤곽 갱신**(내부 셀 → 구멍 고리 / 변에 붙은 셀 → 홈 / 모서리 → 정점 증가), 일직선 정리 on/off |
| `ApprovedArtValidatorTests.cs` | **approved 0개 = 대기 상태**(오류 아님), 빈 assets 배열, manifest 없음, 깨진 JSON, 규격 통과, 파일 없음, **캔버스 < footprint**, **알파 없는 Albedo**, **채널 캔버스 불일치**, Albedo 누락, 파일명 규칙 위반, approved/ 밖 참조, 잘못된 PPU, 잘못된 투영, 캔버스 밖 피벗, 별칭 힌트 |
| `VisualBatch2Tests.cs` | 파일명 규칙 7종, 슬롯 매핑(타일/세트피스), **피벗 좌표계 변환**, 캔버스 정합성, Linear 채널 구분, 힌트 3상태, 카메라 줌 3모드·orthographicSize·캐릭터 비율·앵커 클램프, **접촉 그림자 발 위치 추종**·프로파일 기본값·공중 물체 축소, 채널 디버그 기본값·순환, PNG 헤더 읽기 3종, MiniJson 정상·오류 |

### 7.3 캡처

`docs/unity-port/img/visual-lab/visual-lab-b2-*.png` — 19장, 1920×1080, 플레이 모드 없이 렌더.
배치 1 의 `visual-lab-*.png` 는 그대로 남겨 비교할 수 있다.

| 파일 | 확인 항목 |
|---|---|
| `b2-room-overview` | 배치 1 구조가 채널 재질에서도 유지되는가 |
| `b2-shadow-contours` | 벽 footprint 윤곽 — 맵 테두리·기둥 2개(주황, 바깥 고리) + 방 경계(청록, 안쪽 고리) |
| `b2-shadow-torch` / `b2-shadow-torch-contours` | 탐색광의 노멀 반응과 윤곽 캐스터 |
| `b2-contact-shadow` | 발밑 접촉 AO 타원(실루엣 아님) |
| `b2-footpoint-and-height` | 발 위치 십자·페이드 판정 반경·시각 높이 |
| `b2-channel-albedo` / `normal` / `emission` / `mask` / `ao` / `lighting-only` | 채널 단독 보기 6종 |
| `b2-behind-foreground` / `b2-foreground-groups` | 전경 가림과 청크 단위 페이드 그룹 |
| `b2-camera-base` / `combat` / `coop` | 카메라 프로파일 3종 비교 |
| `b2-break-before` / `b2-break-pillar` / `b2-break-doorway` | **파괴 전후 윤곽 갱신** — 기둥이 U 자로, 방 경계에 문틀 홈이 생긴다 |

### 7.4 승인 아트 검사 실행 결과

현재 `art-production/test-room-v01/metadata/manifest.json` 에는 승인본이 없다
(concept 2 · rejected 2 · working 9). 검사기 출력:

```
■ 아트 대기 상태 — approved 자산 0개, 진행 중 13개.
  오류가 아니다. Codex 아트 트랙의 승인 패키지를 기다리는 정상 상태다.
```

`status` 가 `approved` 가 아닌 항목은 전부 "임포트 대상이 아니다" 참고로만 나오고,
`concept/`·`source/`·`working/` 파일은 복사도 임포트도 하지 않는다.

## 8. 배치 2 에서 고친 실제 결함

1. **Light2D 가 에디터 캡처에서 조명에 전혀 기여하지 않았다.** URP 17.3 의 `Light2D` 는
   메시를 `LateUpdate` 에서 만드는데(전역광 제외), 캡처는 `Camera.Render` 를 한 번 부르는
   경로라 그 LateUpdate 가 오지 않는다. 광원이 메시 없이 남아 캡처 세 장이 바이트까지 같았다
   (`camera-combat` == `contact-shadow` == `footpoint-and-height`). `UpdateMesh`·
   `UpdateBoundingSphere`·`CacheValues` 를 리플렉션으로 부르게 했다 —
   `ShadowGeometryBuilder` 가 `ShadowCaster2D` 를 다루는 것과 같은 방식이다.
   실패하면 경고 한 번만 남기고 최저 조도 화면으로 넘어간다.
2. **코드로 붙인 등록 컴포넌트가 에디터에서 등록되지 않았다.** `OnEnable` 이 불리지 않아
   발 위치·접촉 AO·전경 페이드가 캡처에서 통째로 빠졌다. §6.9 의 `ExecuteAlways` 로 고쳤다.
3. **`VisualLabController.Rebuild` 가 이전 구성을 지우지 않았다.** 캡처 도구와 메뉴가
   `Rebuild` 를 여러 번 부르면 `FootpointSorter`·`ContactShadowRenderer`·`ShadowGeometryBuilder`
   와 더미 캐릭터가 겹쳐 쌓였다(등록 앵커가 1 → 2 → 3). 진단 중 앵커 수 2 로 드러났고,
   자식을 먼저 지우도록 고쳤다. 세 번 연속 `Rebuild` 후에도 앵커 1 · 접촉 1 · 오클루더 2 ·
   윤곽 4 · 캐스터 4 로 유지된다.
4. **`sortingLayerHint` 별칭이 경고로 올라갔다.** `known` 을 초기화만 하고 별칭 분기에서
   내리지 않은 실수였다. 테스트가 잡았고, §6.8 의 3상태로 다시 설계했다.
5. **파괴 캡처 좌표가 이미 빈칸이었다.** `(11,5)` 는 방 정의상 바닥이라 `break-before` 와
   `break-after` 가 바이트까지 같았다. 실제 벽 칸(기둥 `(10,9)`, 북쪽 벽 `(10,12)`)으로 고쳤다.
6. **`WorldVisualProfile.SurfaceRules` 메서드가 동명 타입을 가렸다.**
   `[Range(SurfaceRules.MinLiftCells, …)]` 특성이 컴파일되지 않았다.
   `BuildSurfaceRules` 로 이름을 바꿨다.

## 9. Codex 승인 아트 인계 계약

검사기와 임포터가 실제로 요구하는 것을 여기에 고정한다. 아트 규격서와 어긋나면 이 문서가
구현 기준이다.

### 9.1 manifest 필수 필드 (자산당)

```json
{
  "assetId": "TR01-FLR-001",
  "revision": 1,
  "status": "approved",
  "footprintCells": [1, 1],
  "visualHeightCells": 1.0,
  "pivotPixels": [72, 144],
  "sortingLayerHint": "GroundBase",
  "localOrder": 0,
  "occluderGroup": null,
  "fadeTargetAlpha": null,
  "shadowCasterPath": null,
  "channels": {
    "albedo": "approved/tr01_floor_quiet_a_albedo.png",
    "normal": "approved/tr01_floor_quiet_a_normal.png",
    "emission": "approved/tr01_floor_quiet_a_emission.png",
    "mask": "approved/tr01_floor_quiet_a_mask.png",
    "ao": "approved/tr01_floor_quiet_a_ao.png"
  }
}
```

패키지 머리에는 `productionProjection: "ReferenceTopDown"`,
`deliveryPixelsPerCell: 128`, `sourcePixelsPerCell: 256` 이상이 있어야 한다.

- `channels.albedo` 는 필수다. 없으면 그 자산은 임포트되지 않는다.
- 모든 채널 경로는 `approved/` 로 시작해야 한다. `source/`·`working/`·`concept/` 를 가리키면 오류다.
- `channels` 키와 파일명 접미어가 같아야 한다(`"normal"` 이면 `_normal.png`).
- `visualHeightCells` 가 1.5 이상인데 `shadowCasterPath` 가 없으면 경고한다(§7.4 세트피스 캐스터).
- `sortingLayerHint` 가 `FrontStructure` 인데 `occluderGroup` 이 없으면 경고한다(§6.6 페이드 그룹).

### 9.2 피벗 좌표계 (아트 규격 §10 이 구현에 맡긴 부분)

`pivotPixels` 는 **이미지 좌상단 원점, Y 아래로 증가** 다. PNG 픽셀 순서와 같아 아트 도구에서
읽은 값을 그대로 적을 수 있다. 임포터가 Unity 의 **좌하단 원점 0~1 정규화** 로 뒤집는다
(`ApprovedArtContract.PivotToUnity`).

예: 144×144 캔버스(1셀 + 패딩 8px)의 벽 정면 발점이 아래 중앙이면 `[72, 144]`.
바닥처럼 중심 피벗이면 `[72, 72]`.

### 9.3 캔버스 규격

- 가로 ≥ `footprintCols × 128 + 패딩 × 2`
- 세로 ≥ `max(footprintRows, visualHeightCells) × 128 + 패딩 × 2`
- 패딩 기본 8px, Emission 은 16px 권장(현재 검사는 8px 기준)
- **모든 채널이 같은 캔버스 크기**여야 한다. Albedo 를 기준으로 대조한다.
- Albedo 에는 알파 채널이 있어야 한다(실루엣). Normal 은 팔레트 PNG 금지.

### 9.4 파일명

`tr01_<category>_<name>_<variant>_<channel>.png` — 소문자 영문·숫자·밑줄만.
채널 접미어는 `albedo`·`normal`·`emission`·`mask`·`ao`(보드는 `concept`·`layout`).

### 9.5 EnvironmentKit 슬롯 매핑 (이번 배치가 연결하는 범위)

| 파일명 카테고리 | 조건 | EnvironmentKit 슬롯 |
|---|---|---|
| `floor` / `flr` | `_edge` 포함 | `floorEdge` |
| `floor` / `flr` | `_contact` 또는 `_ao_` 포함 | `contactAo` |
| `floor` / `flr` | 그 밖 | `floorBase` |
| `walltop` / `wtp` | `_rim` 포함 | `wallTopRim` |
| `walltop` / `wtp` | `_inner` 포함 | `innerCorner` |
| `walltop` / `wtp` | `_outer` 또는 `_corner` 포함 | `outerCorner` |
| `walltop` / `wtp` | 그 밖 | `wallTop` |
| `wallfront` / `wfr` | `_west` / `_east` 포함 | `westSide` / `eastSide` |
| `wallfront` / `wfr` | 그 밖 | `wallFront` |
| 그 밖(`arch`·`pillar`·`prop`·`vfx` 등) | — | **연결하지 않음** |

슬롯 안의 순서는 `assetId` 오름차순이다. 모듈 번호가 배열 인덱스이므로 순서가 흔들리면
배치가 달라진다 — `assetId` 를 안정적으로 붙일 것.

**ARC·PIL·LIN·LGT·DEC·HERO·FGV·VFX 는 이 배치에서 연결하지 않는다.** footprint 예약,
LightSocket, 파괴 단계 대체 자산 같은 데이터가 더 필요하고, 그것은 `SetPieceCatalog` 배치의
범위다. 검사기는 이 자산들을 통과시키되 "타일 슬롯이 아니다" 참고를 남긴다.

### 9.6 채널 임포트 설정 (임포터가 강제한다)

| 채널 | textureType | sRGB | 비고 |
|---|---|---|---|
| albedo | `Sprite` | 켜짐 | PPU 128, 피벗 Custom, `alphaIsTransparency` |
| normal | `NormalMap` | 꺼짐 | `convertToNormalmap = false` (이미 탄젠트 공간) |
| emission / mask / ao | `Default` | **꺼짐** | 데이터 맵 |

PNG 파일은 색 공간 강제를 담을 수 없다. 그래서 검사기는 sRGB 청크 유무만 참고로 알리고,
실제 강제는 임포트 단계가 책임진다(§12.2 "Normal 이 sRGB 로 임포트됨" 항목).

### 9.7 채널 맵의 UV 배치

`SurfaceMaterialSet` 은 Normal·Emission·Mask·AO 를 **머티리얼 프로퍼티**로 넣는다(임포트
세컨더리 텍스처는 URP 17 타일맵·스프라이트 조명에 반영되지 않는 것을 2026-09-07 에 확인했다).
따라서 채널 맵은 Albedo 아틀라스와 **같은 UV 배치**여야 한다(아트 규격 §8.1). 자산이
개별 파일이면 각 파일의 채널이 UV 0~1 로 대응하므로 자산별 `SurfaceMaterialSet` 이 필요하고,
아틀라스로 묶으면 아틀라스당 하나로 충분하다. 어느 쪽으로 낼지 결정되면 그때
`SurfaceMaterialSet` 을 자산 단위로 늘릴지 아틀라스 단위로 둘지 확정한다.

## 10. 다음 배치와 보류할 일

### 10.1 다음 배치 후보

1. `AtmosphereRendererFeature` — 깊이 그룹 색·대비 분리, 저층 안개, 광원 글로우(§7.5).
2. 가려진 캐릭터의 실루엣·림 패스 — `WorldLit`/`CharacterLit` 에 자리만 두고 구현하지
   않았다(§6.6 "얇은 팀 색 실루엣 또는 림", §7.1 "가려진 실루엣 패스").
3. `SetPieceCatalog` + `SetPieceSpawner` — ARC·PIL·HERO·FGV 를 footprint 예약과 함께
   배치하고, `IShadowContourSource` 로 세트피스 캐스터를 붙인다(§8.7·§7.4).
4. `LightSocketRenderer` — manifest 의 `lightSockets` 를 읽어 램프·수정 광원을 세운다(§7.3).
5. Light Blend Style 확장 — Mask G·B·A 에 조명 훅을 주려면 Renderer2D 설정을 늘려야 한다.
   본선 조명 수치를 확정하는 배치와 함께 한다.
6. 임포트 후 재검사 — 임포터가 넣은 색 공간·PPU·피벗이 실제로 그렇게 들어갔는지
   `TextureImporter` 를 다시 읽어 확인하는 경로(§12.2 를 두 단계로 완결).

### 10.2 아직 보류하는 일

- **본선 렌더러 교체.** `RunBootstrap` 의 `WorldRenderer` → `EnvironmentChunkRenderer`,
  `WallShadowBuilder` → `ShadowGeometryBuilder` 는 고정 방이 **승인 아트로** 레퍼런스 수준에
  도달한 뒤에 한다(§19). 지금 바꾸면 임시 아트가 본선 화면에 들어간다.
- **절차 맵 연결.** `WorldGridSolidField` 와 `EnvironmentChunkRenderer.MarkCellDirty` 는
  준비됐지만 `WorldGrid.TileBroken`/`TileChanged` 에 잇지 않았다(§14 단계 C).
  이을 때 §6.3 의 청크 단위 부분 추적이 필요한지 함께 판단한다.
- **본선 조명·후처리 수치 확정.** Visual Lab 의 환경광 0.85 · 탐색광 2.2 는 구조 검증용
  값이고 프로파일 기본값과 별개다. 지층별 Volume 과 함께 정한다.
- **품질 단계·접근성 옵션**(§13·§14 F) — 접촉 AO 와 전경 가림은 품질 단계에서 제거하지
  않는다는 제약만 코드 주석에 남겨 두었다.

## 11. 배치 3 — 아트 계약 교정과 첫 승인 아트 임포트 (2026-09-08)

Codex 가 manifest revision 6 으로 **승인 13개(5채널 전부, 파일 61개)** 를 냈다. 검사기를
돌리자 **오류 31 · 경고 57** 이 나왔는데, 실제 파일을 열어 보니 **오류 31개 중 29개가 검사기
쪽 결함**이었다. 아트는 자기 규약에 일관되게 맞아 있었다.

| 항목 | 실제 납품 |
|---|---|
| 1셀 타일 | 정확히 **128×128** (floor 6종, wall_front, wall_top, wall_top_rim, contact_ao) |
| 아치 | **640×512** = 5셀 × 4셀 |
| 채널 캔버스 정합 | 완벽 — normal/emission/mask/ao 전부 albedo 와 동일 크기 |
| 알파 | floor·wall 계열 RGB(불투명), contact_ao·arch RGBA |

### 11.1 고친 검사기 결함

1. **패딩을 강제한 것이 거꾸로였다.** `footprint×128 + 패딩×2` 를 요구했지만,
   **타일링 자산에 패딩을 넣으면 인접 타일 사이에 이음새가 생긴다.** 128×128 정확 일치가
   옳다. 패딩은 알파 여백이 필요한 비타일 자산(세트피스·소품)의 규칙이다.
   → `IsTilingSlot(slot)` 으로 갈라 타일은 정확 일치, 비타일은 이상 + 패딩 없으면 경고.
2. **알파 채널을 모든 자산에 필수로 했다.** 셀을 꽉 채우는 불투명 바닥 타일은 알파 채널이
   없는 것이 정상이다. 막으면 아트에 무의미한 재수출만 강요한다.
   → `RequiresSilhouetteAlpha(slot)` — 데칼·세트피스만 필수, 타일은 참고.
3. **슬롯 매핑이 파일명의 두 번째 조각을 카테고리로 봤다.** 실제 파일명은
   `tr01_wall_front_a`·`tr01_wall_top_rim_a`·`tr01_contact_ao_a` 처럼 카테고리가 여러
   조각이라 `wall`·`contact` 로 읽혔고, 제 표(`wallfront`/`walltop`)에 걸리지 않았다.
   **결과적으로 WFR·WTP·WTP-RIM·AO-CONTACT 가 전부 `None` 으로 떨어져 EnvironmentKit 에
   연결되지 않았을 것이다** — 가장 심각한 결함이었다.
   → `SlotForAssetId` 를 1순위로 두고 아트 규격 §7 의 ID 범위 토큰(FLR/WTP/WFR/AO/ARC…)을
   읽는다. 파일명 매핑은 위치가 아니라 줄기 문자열 포함으로 바꿔 보조 수단으로 남긴다.
4. **캔버스 세로를 `max(footprint 깊이, 시각 높이)` 로 봤다.** 두 값은 다른 축이다.
   벽 정면은 지면 1셀 경계에 서 있어도(footprint 1) 그려지는 높이가 0.75셀(96px)일 수 있다.
   §8.6 의 0.75~1.5셀은 96px·192px 이고 **둘 다 128 의 배수가 아니다** — "셀 배수" 규칙도
   함께 틀렸다. → 솟는 슬롯은 선언한 시각 높이가 곧 스프라이트 높이, 바닥·데칼은 footprint
   깊이. `VisualHeightMatters(slot)` 로 유도한다.
5. `pivotPixels` 누락을 **오류 → 경고** 로 내렸다. 슬롯에서 유추한 피벗
   (`DefaultPivotPixels`: 바닥은 중심, 벽은 하단 발점)으로도 조립 검증은 진행할 수 있고,
   오류로 막으면 아트 트랙이 나머지 검사 결과를 못 본다. 최종 납품에는 여전히 필수다.
6. `sortingLayerHint` 처리 — `GroundAO` 를 별칭으로 받고(→ `GroundDecal`), 힌트가 없으면
   슬롯에서 기본값을 유추한다. **타일 슬롯에서는 이 값이 렌더링에 쓰이지 않는다** —
   `EnvironmentChunkRenderer` 가 표면 토폴로지로 레이어를 정하기 때문이다(같은 cap 이
   북쪽 개방 여부에 따라 `FrontStructure`/`WallTop` 로 갈린다). 그래서 참고로만 알린다.
7. sRGB 청크 경고를 **참고**로 내렸다(임포터가 확정적으로 끈다). 리포트에서 같은 메시지를
   묶어 대상 자산을 함께 보여 준다 — sRGB 48줄이 4줄이 됐다.

### 11.2 결과

```
승인 13개 · 진행 중 5개 · 오류 0 · 경고 19
  임포트 가능하다.
```

남은 경고 19개는 전부 진짜다: `pivotPixels` 누락 ×13, 아치 패딩 없음,
`shadowCasterPath` 누락 ×3(아치·기둥 2종), `visualHeightCells` 누락 ×2(WTP-BASE-A·WTP-RIM-A).

임포트 결과 — 수정 전이면 아래 네 슬롯이 전부 비어 있었을 것이다.

| 슬롯 | 연결 |
|---|---|
| `floorBase` | 6 (FLR-BASE-A~F) |
| `wallTop` / `wallTopRim` / `wallFront` / `contactAo` | 각 1 |
| 세트피스(ARC, PIL×2) | 연결 안 됨 — SetPieceCatalog 배치 |

스프라이트 검증: PPU 128, `wall_front` 피벗 정규화 `(0.50, 0.00)` = 하단 중앙,
`floor_base` 피벗 `(0.50, 0.50)` = 중심. **manifest 에 `pivotPixels` 가 없어도 슬롯 유추가
아트 규격 §6 과 같은 값을 냈다.**

EditMode **244/244 통과** (배치 2 의 233 + 신규 11).

### 11.3 캡처

`Tunnel Crew/비주얼 · Visual Lab 을 승인 아트로 전환` 메뉴를 추가했다. 캡처 태그가 키트에서
유도되므로 임시 아트 캡처(`visual-lab-b2-*`)를 덮어쓰지 않고 나란히 남는다.

- 임시 아트: `docs/unity-port/img/visual-lab/visual-lab-b2-*.png` (20장)
- 승인 아트: `docs/unity-port/img/visual-lab/visual-lab-b3-art-*.png` (20장)

첫 승인 아트 화면에서 확인한 것: **바닥·벽 정면·벽 상단·기둥의 깊이층 구조가 임시 아트에서
세운 그대로 유지된다.** 캐릭터 정렬과 파괴 후 윤곽 갱신도 정상이다.

### 11.4 발견한 구조 문제 — 자산별 채널 맵과 타일맵 머티리얼 (§12 에서 해결)

승인 아트를 연결했지만 **화면이 매우 어둡고 평평하다.** 원인은 조명 수치가 아니라 구조다.

승인 아트는 자산마다 자기 128×128 채널 맵을 갖는다(바닥 6종이면 노멀도 6장). 그런데
`EnvironmentChunkRenderer` 는 표면 분류마다 **머티리얼 하나**를 Tilemap 에 씌우고,
머티리얼의 `_NormalMap` 은 하나뿐이다. 즉 **바닥 6종의 서로 다른 노멀을 동시에 넣을 수 없다.**
그래서 승인 아트 전환 시 `SurfaceMaterialSet` 을 아예 연결하지 않았고, 그 결과
`_MinLight`(§7.2 최소광)도 걸리지 않아 화면이 어둡다.

가능한 길:

| 방안 | 판단 |
|---|---|
| (a) 임포트 때 **채널 정렬 아틀라스**를 만든다 | **채택.** 아트 규격 §8.1 이 "재질 채널별 동일 배치" 를 이미 요구한다. 머티리얼 하나로 끝나고 배칭도 유지된다 |
| (b) 자산마다 머티리얼을 만들고 Tilemap 대신 개별 SpriteRenderer | 배칭이 깨지고 §6.4 의 레이어 설계와 충돌 |
| (c) Unity Sprite Atlas 의 세컨더리 텍스처 | 2026-09-07 에 URP 17 타일맵·스프라이트 조명에 반영되지 않는 것을 확인했다 |

따라서 (a) 를 구현했다 — §12 를 볼 것.

### 11.5 Codex 에 전달할 것

1. **`pivotPixels` 를 13개 자산 전부에 추가** — 좌상단 원점, Y 아래로 증가.
   벽 정면(128×128) 발점은 `[64, 128]`, 바닥은 `[64, 64]`.
2. `visualHeightCells` 를 `TR01-WTP-BASE-A`·`TR01-WTP-RIM-A` 에 추가(솟는 자산이다).
3. 아치 `TR01-ARC-001` 에 알파 패딩 8px(Emission 16px) 권장 — Bloom 가장자리 잘림 방지.
4. `shadowCasterPath` 는 세트피스 배치에서 필요하다. 지금 급하지 않다.
5. **타일 자산에는 패딩을 넣지 말 것** — 128×128 정확 일치가 맞다. 지금 납품이 이미 옳다.

## 12. 배치 4 — 채널 아틀라스 (2026-09-08)

§11.4 가 남긴 구조 문제를 해결했다. 아트에 의존하지 않는 작업이라 Codex 가 제작하는 동안
진행했다.

| 조항 | 파일 |
|---|---|
| 아트 규격 §8.1 채널별 동일 배치 | `Editor/ArtPipeline/AtlasLayout.cs` (순수), `ChannelAtlasBuilder.cs` |
| §16.1 배치 규칙 테스트 | `Tests/EditMode/AtlasLayoutTests.cs` |

### 12.1 무엇을 하는가

`승인 아트 임포트 → EnvironmentKit` 메뉴가 개별 임포트 뒤에 아틀라스를 이어서 만든다.

1. 승인 자산을 **렌더러의 머티리얼 단위**로 묶는다 — `floor`(FloorBase·FloorEdge),
   `walltop`(WallTop·WallTopRim·코너), `wallfront`(WallFront·West·East).
   `EnvironmentChunkRenderer` 가 표면 분류마다 머티리얼 하나를 쓰므로 그 단위와 같아야
   아틀라스 하나가 머티리얼 하나에 대응한다.
2. 묶음마다 **채널 5종 아틀라스**를 같은 격자 배치로 만든다.
3. Albedo 아틀라스를 자산별 스프라이트로 자르고(피벗 보존), `EnvironmentKit` 슬롯에 넣는다.
4. 채널 아틀라스를 `SurfaceMaterialSet` 에 연결한다.

접촉 AO(`contactAo`)는 아틀라스에 넣지 않는다 — `GroundDecal` 타일맵은 채널 재질을 쓰지
않는 알파 곱셈 음영이라, 개별 스프라이트를 그대로 둔다.

### 12.2 확정한 판단

**원본 PNG 를 읽는다.** 임포트된 텍스처를 읽으면 `NormalMap` 타입이 적용한 재인코딩과
압축·색공간 변환이 섞인다. 패키지의 원본 PNG 를 `linear: true` Texture2D 로 읽어
바이트 그대로 쓰는 것이 유일하게 안전하다.

**격자 배치를 쓴다.** 타일 자산은 폭이 모두 1셀이고 높이만 0.75~1.5셀로 다르다(§8.6).
자유 패킹이 필요할 만큼 형태가 다양하지 않고, 격자는 채널 다섯 장이 **같은 배치**임을
보장하기 쉽다. 자산이 셀보다 낮으면 셀의 **아래쪽**에 붙인다 — 발점이 아래인 벽 정면의
기준이 흔들리지 않게 한다.

**가장자리를 늘려 채운다(4px).** 바이리니어 필터링은 스프라이트 사각형 바로 밖의 텍셀까지
닿는다. 그 자리가 기본값(투명·흰색)이면 타일 경계에 밝거나 어두운 선이 생긴다. 가장자리
픽셀을 복제해 두면 그 샘플이 자기 가장자리와 같아져 선이 사라진다.

**채널이 없으면 아틀라스를 만들지 않는다.** 셰이더의 기본값(`bump`/`black`/`white`)이
"그 채널 없음" 을 뜻하므로 빈 아틀라스를 만들 이유가 없다. 자산 일부만 채널을 냈으면
나머지 자리는 채널별 중립값(노멀 `(128,128,255)`, 발광 검정, 마스크·AO 흰색)으로 채운다.

**스프라이트 GUID 를 이름에서 만든다.** 재임포트마다 새 GUID 를 만들면 `EnvironmentKit` 이
잡고 있는 참조가 끊겨 슬롯이 비어 버린다. 이름의 MD5 로 고정 GUID 를 만들어
`ISpriteNameFileIdDataProvider` 에 함께 넣는다.

### 12.3 고친 결함

**`TextureImporter.spritesheet` 를 쓰지 않는다.** 처음에 그것으로 슬라이싱했더니 컴파일
경고가 났다 — *"Support for accessing sprite meta data through spritesheet has been removed.
Please use the UnityEditor.U2D.Sprites.ISpriteEditorDataProvider interface instead."*
동작은 했지만 "removed" 라고 명시된 경로는 조용히 멈출 수 있다.
`SpriteDataProviderFactories` + `ISpriteEditorDataProvider` 로 옮겼고,
`TunnelCrew.Editor` asmdef 에 `Unity.2D.Sprite.Editor` 참조를 넣었다.
(`SpriteRect`·`SpriteNameFileIdPair` 는 `UnityEditor.U2D.Sprites` 가 아니라 `UnityEditor`
네임스페이스다 — 처음에 잘못 적어 컴파일이 깨졌다.)

### 12.4 검증

실제 승인 아트로 만든 아틀라스:

| 묶음 | 아틀라스 | 격자 | 자산 | 스프라이트 |
|---|---|---|---|---|
| floor | 408×272 | 3×2 | 6 | 6 |
| walltop | 272×136 | 2×1 | 2 | 2 |
| wallfront | 136×136 | 1×1 | 1 | 1 |

- 세 묶음 모두 **채널 4종 크기가 Albedo 아틀라스와 일치** (UV 정합의 필요조건)
- 채널 아틀라스 `sRGBTexture = false` (Linear)
- 스프라이트가 아틀라스 부분 사각형: `tr01_flr_base_a` = `(4,4,128,128)` in 408×272,
  136px 간격으로 3×2 배치
- 피벗 보존: 바닥 `(0.50,0.50)` 중심, 벽 정면 `(0.50,0.00)` 하단 중앙, PPU 128
- **Normal 단독 보기 캡처에서 벽돌 요철이 벽 상단 타일 위치에만 나타난다** —
  배치가 어긋났으면 바닥으로 새어 즉시 드러난다
- 최소광(§7.2)이 걸려 승인 아트 화면이 §11.3 의 어두운 상태에서 벗어났다

EditMode **254/254 통과** (배치 3 의 244 + 신규 10). 컴파일 오류 0 · 경고 0 · 콘솔 오류 0.

### 12.5 아트 진행 상황 (이 배치 동안)

Codex 가 작업 중에도 계속 냈다: 승인 **13 → 17개**.

- 추가: `TR01-LGT-WORKLAMP-A`·`TR01-LGT-WARNING-A`·`TR01-LGT-CRYSTAL-A`, `TR01-HERO-DRILL-A`
- **`pivotPixels` 를 전부 채웠다** — 경고 19 → 10
- 남은 경고 10개는 전부 세트피스 관련이다: `shadowCasterPath` 누락 ×7(아치·기둥 2·조명 3·드릴),
  아치 알파 패딩, `visualHeightCells` 누락 ×2(WTP-BASE-A·WTP-RIM-A)

조명 소품(LGT)·드릴(HERO)·기둥(PIL)·아치(ARC)는 타일 슬롯이 아니므로 아직 연결되지 않는다.
이제 세트피스 자산이 7종 쌓였으므로 **`SetPieceCatalog` 배치의 우선순위가 올라갔다.**

## 13. 배치 5 — 세트피스 카탈로그 (2026-09-08)

세트피스 자산 7종이 타일 슬롯이 아니라 연결되지 않고 쌓여 있었다. 아트에 의존하지 않는
작업이라 Codex 가 제작하는 동안 진행했다.

| 조항 | 파일 |
|---|---|
| §8.7·§12.1 세트피스 카탈로그 | `Visual/Environment/SetPieceCatalog.cs` |
| §6.3·§8.7 배치 | `Visual/Environment/SetPieceSpawner.cs` (`SetPieceInstance` 포함) |
| §7.3 조명 소켓 | `LightSocketDef`, `SetPieceSpawner.AttachSocketLight` |
| §7.4 세트피스 캐스터 | `SetPieceInstance : IShadowContourSource` |
| §12.2 소켓·캐스터 파싱 | `ApprovedArtValidator.ParseLightSockets` |
| 카탈로그 생성 | `Editor/ArtPipeline/SetPieceCatalogBuilder.cs` |
| 좌표 변환 | `ApprovedArtContract.SocketOffsetCells`, `FootprintContourCells` |

### 13.1 확정한 판단

**세트피스는 아틀라스로 묶지 않는다.** 타일은 Tilemap 하나에 머티리얼 하나를 쓰므로
자산별 채널 맵을 넣을 수 없어 아틀라스가 필요했다(§12). 세트피스는 개별
<c>SpriteRenderer</c> 라 자산마다 머티리얼을 가질 수 있다. 방당 몇 개뿐이므로(§8.4)
배칭 손실도 문제가 아니고, 크기가 640×512·512×384·256×256 로 제각각이라 격자 패킹은
낭비가 크다.

**소켓 픽셀은 `pivotPixels` 와 같은 좌표계다.** 좌상단 원점·Y 아래로 증가. 런타임은 발점을
원점으로 하고 화면 위가 +Y 이므로 Y 를 뒤집는다:
`offsetCells = ((socketX - pivotX)/128, (pivotY - socketY)/128)`.
실제 값 검증 — 작업등(256×256, 피벗 [128,248], 소켓 [128,82]) → `(0.00, 1.30)`,
드릴 service(피벗 [256,376], 소켓 [310,230]) → `(0.42, 1.14)`,
경고등(피벗 [128,128], 소켓 [128,139]) → `(0.00, -0.09)` — 벽 부착이라 피벗보다 아래다.

**스프라이트 피벗이 발점이므로 본체를 올리지 않는다.** 아트가 이미 발점 위로 솟은 픽셀을
담고 있어서 앵커 원점에 그대로 두면 지면에 서 있는 것으로 읽힌다.
`VisualHeightAnchor.visualHeight` 는 0 으로 둔다 — 그 값은 공중에 뜬 물체(정렬은 지면,
본체만 위로)와 접촉 AO 축소를 위한 것이다.

**소켓 광원은 기본적으로 그림자를 만들지 않는다.** §7.3 이 생체·광물광을 "저비용 비그림자
Light2D 선택" 으로 두었고, 켜면 §13 의 그림자 광원 예산(일반 4개)을 소켓 5개로 이미 넘긴다.
`SetPieceSpawner._socketLightsCastShadows` 로 켤 수 있다.

**`shadowCasterPath` 가 없으면 footprint 사각형을 쓴다.** 검사기가 이미 경고를 남기므로
조용히 넘어가지 않는다. `ShadowGeometryBuilder` 가 윤곽 점을 정수로 스냅하므로 현재
세트피스 윤곽은 정수 셀 격자에 한정된다 — footprint 사각형은 정수라 문제가 없지만,
아트가 별도 윤곽을 내면 그때 소수점 지원이 필요하다.

### 13.2 고친 실제 결함 — SortingGroup 만으로는 레이어가 바뀌지 않는다

세트피스 7종이 **전부 화면에 나오지 않았다.** 순서대로 배제한 것:

1. 배치·정렬 수치는 정상 — 아치 order -176(y=11), 기둥 -144(y=9), 드릴 -56(footprint 깊이 2 반영)
2. `enabled=True`, 카메라 시야 안, 셰이더 `WorldLit`, 색 흰색
3. **`SortingGroup.sortingOrder` 를 5000 으로 올려도 안 나타났다** → 순서 문제 아님
4. 머티리얼을 `Sprites-Default`·`URP Sprite-Lit-Default` 로 바꿔도 동일 → 머티리얼 아님
5. 원본 PNG 알파 확인 — 기둥 불투명 픽셀 62,503개, 샘플 지점 `rgba(116,93,73,255)` → 아트 아님
6. **독립 GameObject 에 같은 스프라이트를 두고 렌더러 자신에게 `sortingLayerName` 을 넣으니
   렌더됐다** → 차이는 그 한 줄

원인: **URP 2D 렌더러는 렌더러 자신의 `sortingLayerID` 로 어느 광원 배치(batch)에 그릴지
정한다.** `SortingGroup` 은 그 뒤 "그룹 안에서의 상대 순서" 만 담당한다. 자식 렌더러가
`Default` 로 남으면 `Default` 배치에서 그려지고, `Default` 는 §6.4 순서의 최하위라 바닥 타일
아래로 내려간다 — 화면에서는 그냥 사라진 것처럼 보인다.

배치 1~4 에서 드러나지 않은 이유는 더미 캐릭터가 유일한 앵커였고, `VisualLabController`
가 그 렌더러에 `sortingLayerName` 을 직접 넣고 있었기 때문이다.

수정: `VisualHeightAnchor.Apply` 가 `SortingGroup` 과 **자식 렌더러 모두**에 레이어를 넣는다
(`ApplyLayerToRenderers`). 자식이 늘거나 줄면 `InvalidateRenderers()` 로 다시 모은다.

**이 규칙은 앞으로 모든 개체에 적용된다** — 캐릭터·적·드롭·설치물을 붙일 때 SortingGroup
하나로 끝났다고 생각하면 같은 증상을 다시 만난다.

### 13.3 함께 고친 것

**`EditorTick` 이 더미 앵커만 정렬했다.** `_dummy?.Apply(units)` 만 불러서 세트피스 앵커는
에디터 캡처에서 정렬이 적용되지 않았다(플레이 모드에서는 `FootpointSorter.LateUpdate` 가
전부 돌아 문제가 없었다). `FootpointSorter.ApplyAll(units)` 를 노출해 전부 적용한다.

### 13.4 검증

카탈로그 7종 — 아치·기둥 2종·조명 소품 3종·영웅 드릴. 전부 채널 묶음 연결됨.

| 자산 | footprint | 높이 | 레이어 | 소켓 |
|---|---|---|---|---|
| TR01-ARC-001 | 5×1 | 3.90 | BackStructure | 0 |
| TR01-PIL-INTACT-A / BROKEN-A | 1×1 | 3.88 | BackStructure | 0 |
| TR01-HERO-DRILL-A | 3×2 | 2.88 | WorldEntity | 2 |
| TR01-LGT-WORKLAMP-A | 1×1 | 1.88 | WorldEntity | 1 |
| TR01-LGT-CRYSTAL-A | 1×1 | 1.88 | WorldEntity | 1 |
| TR01-LGT-WARNING-A | 1×1 | 1.88 | BackStructure | 1 |

Visual Lab 에 §8.7 이 정한 자리로 배치(방 후면 중심 아치, 측면 기둥, 바닥 조명·설비):

- 세트피스 7 · 그림자 윤곽 **11**(벽 4 + 세트피스 7) · 캐스터 11
- 접촉 AO **8**(더미 + 세트피스 7) · 앵커 8
- Light2D **7**(전역 1 + 탐색광 1 + 소켓 5)
- 정렬 검증: 드릴(y=4, footprint 깊이 2 → 앞쪽 경계 3.5) order -56 이 작업등(y=4) -64 보다
  앞, 더미(y≈4.88) -78 보다 앞 — 발 위치 정렬이 여러 셀 구조물에서도 맞는다

캡처 `visual-lab-b3-art-*` 에서 7종이 전부 보이고 소켓 색이 manifest 와 일치한다
(마젠타 `#D42BD8` 경고등, 청록 `#24D8EF` 결정등, 앰버 `#FFAD32` 작업등).

### 13.5 남은 것

세트피스가 **여전히 어둡다.** 조명 수치와 알베도 대비 문제이고, §19 가 본선 조명 수치
확정을 보류시켰으므로 지금 만지지 않는다. 대신 다음 두 가지가 실제 해법이다.

1. `AtmosphereRendererFeature`(§7.5) — 깊이 그룹 색·대비 분리로 층을 갈라 준다
2. 지층별 Volume 과 함께 본선 조명 수치 확정(§14 단계 D 이후)

`TR01-PIL-INTACT-A` / `TR01-PIL-BROKEN-A` 쌍은 §9.4 의 파괴 상태(온전함·손상)로 쓰라는
신호다. manifest 에 `replacementAssetId` 가 아직 없어 카탈로그에 연결하지 않았다 —
파괴 연동은 절차 맵 배치에서 함께 한다.

---

## 14. 배치 6 — 대기 원근 패스 (2026-09-08)

기능명세서 §7.5. §14 단계 B 의 마지막 큰 조각이다.

### 14.1 구현한 파일과 조항 대응

| 파일 | 조항 | 역할 |
| --- | --- | --- |
| `Presentation/Visual/Lighting/AtmosphereProfile.cs` | §7.5, §12.1 | 안개·깊이 색 분리·가장자리 암부·그레인 수치의 단일 출처 |
| `Shaders/Atmosphere.shader` | §7.5 | 전체화면 합성 한 패스 |
| `Presentation/Visual/Lighting/AtmosphereDirector.cs` | §7.5, §12.3 | 카메라 추종 쿼드 · 프로파일 → 머티리얼 · 층 단독 보기 |
| `Presentation/Visual/Debug/VisualLabController.cs` | §12.3 | `BuildAtmosphere()` · 키 B/N |
| `Editor/BuildVisualLab.cs` | §12.1 | `AtmosphereProfile_Stratum1.asset` 생성·연결 |
| `Editor/CaptureVisualLab.cs` | §16.3 | 대기 캡처 6장 |
| `Tests/EditMode/AtmosphereTests.cs` | §16 | 프로파일 계약·결정성·격리 순환 |

### 14.2 확정한 기술 판단

**RendererFeature 를 쓰지 않고 `VisionAndGrade` 쿼드로 만들었다.**

URP 17.3 에는 쓸 수 있는 `FullScreenPassRendererFeature` 가 실제로 들어 있다
(`injectionPoint` · `fetchColorBuffer` · `passMaterial` 이 모두 public 이고,
`Blit.hlsl` 의 `Vert`/`_BlitTexture` 계약을 따르면 된다). 그런데도 쓰지 않은 이유:

1. §7.5 가 "`AtmosphereRendererFeature` **또는 동등한 전체화면 패스**" 로 열어 두었다.
2. §6.4 가 이미 `VisionAndGrade` 레이어에 "LOS 어둠, 안개, 깊이 색보정" 을 배정해 두었다 —
   대기 원근은 원래부터 그 레이어의 몫이다.
3. `Renderer2D.asset` 에 기능을 추가하면 **본선 렌더 파이프라인 자산이 바뀐다.** 이번
   작업 범위는 "실제 게임 씬의 최종 조명과 후처리 수치를 확정하지 말 것" 이 걸려 있고,
   파이프라인 자산 변경은 Visual Lab 뿐 아니라 본선 전 씬에 즉시 적용된다.
4. 같은 방식(`DarknessOverlay`)이 이미 프로젝트에 있어서 구조가 하나로 유지된다.

색 버퍼를 읽어야 하는 항목 — **과노출 클램프와 블룸 하이라이트 보존** — 은 이 쿼드에서
할 수 없으므로 그대로 URP Volume 에 남긴다(§7.5 후반). **광선 축(light shaft)** 은 광원별
형상이 필요해 §11.2 VFX 로 미룬다.

**합성은 프리멀티플라이드 알파다.** `Blend One OneMinusSrcAlpha`.
스트레이트 알파(`SrcAlpha OneMinusSrcAlpha`)로는 그레인이 "어둡게" 밖에 못 된다 —
알파와 무관한 양방향 가산항을 얹으려면 프리멀티플라이드여야 한다. 안개·깊이 색·상태
색조·암부는 셰이더 안에서 스트레이트로 차례로 얹고, 마지막에 한 번 `rgb = accum * cover`
로 바꾼 뒤 그레인을 더한다.

**화면 UV 는 쿼드 UV 가 아니라 `positionCS.xy / _ScreenParams.xy` 다.** 쿼드는 회전·흔들림에
빈틈이 없도록 1.15배로 잡혀 있어서 쿼드 UV 를 쓰면 안개 높이와 암부 반경이 15% 씩 틀린다.

**그레인은 기본적으로 시간 고정이다.** `AtmosphereDirector.GrainTime` 이 고정 플래그나
`animateGrain=false` 면 항상 0 을 돌려준다. 애니메이션 시에도 `floor(t*24)` 의 이산값이라
프레임률이 흔들려도 같은 1/24초 안에서는 값이 같다. 캡처 경로는 `FreezeGrain = true` 로
고정한다 — 그러지 않으면 §16.3 회귀 캡처가 매번 달라져 비교가 불가능해진다.

**기본값은 일부러 약하다.** §7.5 가 후처리를 "이미 존재하는 깊이층의 분리를 강화하는
용도로만" 쓰라고 못 박았다. 안개 농도 .22 · 깊이 분리 .12 · 암부 .35 · 그레인 .018.
어두운 화면을 여기서 밝게 만들려 하면 안 된다 — 그건 알베도·최소광·조명의 일이다.
테스트가 이 상한을 잠가 둔다(`기본값은_형태를_덮지_않을_만큼_약하다`).

### 14.3 검증

- 컴파일 오류 0 · 콘솔 오류 0 (`Account API did not become accessible` 는 Unity 계정
  서비스 경고로 이 작업과 무관하다)
- EditMode **277/277 통과** (배치 5 의 267 + 신규 10)
- `Shader.isSupported == true` — 테스트가 직접 확인한다
- 캡처 6장 신규, 전부 서로 다른 해시(배치 2 의 "캡처가 바이트 동일" 함정을 다시 확인)
  - `visual-lab-b3-art-atmosphere-off.png`
  - `visual-lab-b3-art-atmosphere-on.png`
  - `visual-lab-b3-art-atmosphere-fog.png`
  - `visual-lab-b3-art-atmosphere-depth.png`
  - `visual-lab-b3-art-atmosphere-vignette.png`
  - `visual-lab-b3-art-atmosphere-grain.png`
- 기존 20장은 대기 원근을 **끈 상태**로 찍는다(`Shot.Atmosphere` 기본 false) — 배치 3~5
  캡처와 회귀 비교가 계속 가능해야 한다

### 14.4 고친 실제 결함

**그레인이 크러시된 테두리에서 지글거렸다.** 첫 캡처(`atmosphere-on`)에서 방 밖 검정
영역에 눈에 띄는 디더가 깔렸다. 필름 그레인은 실제로는 중간톤에서 가장 강하고 완전한
검정에서는 사라지는데, 색 버퍼를 읽을 수 없어 휘도를 모른다. 대신 **암부 커버리지로
게이트**했다 — 일부러 검정으로 눌러 놓은 곳에는 디더를 얹지 않는다. 격리 모드에서는
암부 게이트가 0 이라 그레인이 온전히 보인다. 기본 세기도 .03 → .018 로 내렸다.

남은 것: Visual Lab 의 `FitWholeRoom` 캡처는 맵 밖까지 보여 주기 때문에 테두리 검정에
약한 그레인이 남는다. 이건 랩 구도 때문이고 실제 게임 화면은 프레임이 채워진다
(`camera-base`/`combat`/`coop` 캡처에서는 나타나지 않는다).

### 14.5 조작 (§12.3)

| 키 | 동작 |
| --- | --- |
| B | 대기 원근 켜기/끄기 |
| N | 층 단독 보기 순환 — 합성 → 안개 → 깊이 → 암부 → 상태 색조 → 그레인 |

### 14.6 다음

1. 가려진 캐릭터 실루엣·림 패스(§6.6, §7.1) — 셰이더 슬롯은 이미 있고 구현이 없다
2. `LightSocketRenderer`(§7.3) — 독립 램프·결정 소켓
3. 임포트 후 재검증(§12.2 2단계) — 임포터가 실제로 적용한 색공간·PPU·피벗을 되읽는다
4. Light Blend Style 확장 — Mask G/B/A 에 조명 훅을 주려면 `Renderer2D` 설정을 바꿔야
   하므로 본선 조명 수치 확정과 함께 묶는다

---

## 15. 배치 7 — 가려진 캐릭터 실루엣·림 패스 (2026-09-08)

기능명세서 §6.6 "가려진 캐릭터에는 얇은 팀 색 실루엣 또는 림을 표시한다",
"적은 완전 투명 처리하지 않고 위협 실루엣만 보장한다" · §7.1 "전경 페이드와 가려진 실루엣 패스".

### 15.1 구현한 파일과 조항 대응

| 파일 | 조항 | 역할 |
| --- | --- | --- |
| `Shaders/OccludedSilhouette.shader` | §6.6, §7.1 | 원본 알파에서 안쪽 림을 뽑는 Unlit 패스 |
| `Presentation/Visual/Depth/OccludedSilhouetteRenderer.cs` | §6.6 | `OccludedSilhouette` 컴포넌트 + 풀 매니저 |
| `Presentation/Visual/Depth/ForegroundOccluder.cs` | §6.6 | 셀 마스크 겹침 판정(`SetCellMask` · `capLiftCells`) |
| `Presentation/Visual/Depth/ForegroundFadeController.cs` | §6.6 | `CoveredAt` 조회 추가 |
| `Presentation/Visual/Environment/EnvironmentChunkRenderer.cs` | §6.6, §6.7 | 전경 cap 셀 마스크를 타일과 같은 자리에서 갱신 |
| `Presentation/Visual/Debug/VisualLabController.cs` | §12.3 | 적 더미 추가 · 키 H |
| `Editor/CaptureVisualLab.cs` | §16.3 | 실루엣 캡처 3장 |
| `Tests/EditMode/OccludedSilhouetteTests.cs` | §16 | 보간·모드·셀 마스크·레이어 계약 |

### 15.2 확정한 기술 판단

**실루엣은 개체의 자식이 아니라 풀 매니저가 그린다.** 위협 실루엣은 자기를 가린 전경
구조물보다 <i>위에</i> 있어야 한다. 그런데 `VisualHeightAnchor` 는 `SortingGroup` 을 붙이고,
그룹 안의 자식은 자기 Sorting Layer 로 배치돼도 최종 위치는 그룹 위치(`WorldEntity`)를
따른다 — 자식으로 두면 `FrontStructure` 위로 절대 올라가지 못한다. 접촉 AO(§7.4-1)가
같은 이유로 매니저인 것과 동일한 제약이다(배치 5 에서 세트피스 7종이 전부 보이지 않았던 바로 그 규칙).

**레이어는 `WorldFX` 다.** §6.4 순서에서 `FrontStructure` 보다 위, `VisionAndGrade` 보다
아래. 벽 위로는 올라오면서 §6.6 마지막 항 "시야 밖 구조물은 기존 LOS 어둠 규칙을
우선한다" 가 지켜진다 — 시야 밖 적의 실루엣이 LOS 어둠을 뚫고 보이면 정보 누설이다.
이 순서 관계를 테스트로 잠갔다(`실루엣_레이어는_전경보다_위_LOS_어둠보다_아래다`).

**관심/위협 두 모드로 갈린다.** 덮임 판정은 같지만 요구가 다르다.

| 모드 | 상황 | 처리 |
| --- | --- | --- |
| `Interest` | 로컬 관심 캐릭터. 앞의 벽이 이미 0.34 로 페이드했다 | 얇은 팀 색 림만, 최대 알파 0.85 |
| `Threat` | 적. 관심 대상이 아니므로 **벽이 페이드하지 않는다** | 림 + 낮은 내부 채움(0.22), 최대 알파는 프로파일 `enemySilhouetteMinAlpha`(0.55) |

`enemySilhouetteMinAlpha` 는 배치 1 에서 프로파일에 넣어 두고 쓰지 않던 값이다. 이제 여기가
그 값의 유일한 소비처다.

**림은 원본 알파의 안쪽 경계에서 뽑는다.** 자기 알파와 이웃 알파(4방향 + 대각선, `_RimWidth`
텍셀 거리)의 차이가 곧 경계다. 내부는 이웃이 모두 채워져 있어 0 이 되므로 두께 ≈ `_RimWidth`
텍셀의 테두리만 남는다. 대각선을 빼면 45도 경사에서 림이 끊긴다. UV 가 [0,1] 밖이면 0 으로
읽어 스프라이트 자기 가장자리에도 림이 생기게 하고, 동시에 아틀라스 이웃을 읽지 않는다.

머티리얼은 **모드별로 하나씩 공유**한다(`SilhouetteRim` · `SilhouetteThreat`). 색과 알파는
`SpriteRenderer.color`(정점 색)로 개체마다 주므로 머티리얼을 개체별로 만들지 않는다 — 그러면
배치가 깨진다(§13).

### 15.3 고친 실제 결함 — 전경 페이드가 방 전체에서 항상 켜져 있었다

실루엣의 덮임 판정을 붙이자마자 방 어디에 서 있어도 참이 나왔다. 원인을 재 보니
`ForegroundOccluder.footprintCells` 가 **청크 사각형(16×16)** 이었다. 전경 cap 타일맵은
페이드 그룹을 만들기 위해 청크마다 하나씩 두는데(배치 2), 오클루더 사각형도 그 청크
경계를 그대로 받았다. 실제 cap 타일은 그 안의 몇 셀뿐인데도 `Overlaps` 는 청크 안이면
무조건 참이었다.

즉 **배치 2 부터 §6.6 의 전경 페이드는 방 한가운데에서도 항상 켜져 있었다.** 캡처가
이걸 드러내지 못한 이유는 페이드 관련 장면이 `behind-foreground` 하나뿐이었고, 그 장면은
"켜져 있어야 맞는" 위치였기 때문이다.

고친 방법:

1. `ForegroundOccluder.SetCellMask(mask, originCol, originRow, cols, rows)` — 실제 cap
   타일이 놓인 셀만 표시한 마스크. 배열은 **복사하지 않고 참조로** 들고 있어서, 파괴·복구가
   제자리에서 고치면 같은 프레임에 반영된다(§6.7 의 같은 dirty 단위).
2. `EnvironmentChunkRenderer.DrawCell` 이 타일을 쓰는 바로 그 자리에서 마스크도 쓴다.
   갈라지면 부순 벽이 계속 캐릭터를 가린 것으로 판정된다.
3. 후보 셀마다 원-사각형 판정을 **정확히** 한다. 행 범위만 보고 통과시키면 `floor`/`ceil`
   여유 때문에 한 칸이 딸려 들어와, 가릴 수 없는 위치의 cap 이 걸린다(방 한가운데에서
   기둥이 걸리는 현상으로 나타났다).

**방향을 한 번 거꾸로 잡았다.** 처음에는 "정면이 남쪽으로 늘어져 그려진다" 고 보고 북쪽
행까지 찾게 했다. `MakeMap` 을 다시 읽어 보니 전경 cap 타일맵은 `localPosition.y = +lift`
로 **위(북쪽)** 로 올라가 있다. 그래서 셀 r 의 cap 은 화면 `[r + lift, r + 1 + lift]` 를
덮고, 그 앞(북쪽) 바닥에 선 캐릭터를 가린다 — 즉 캐릭터를 가리는 cap 셀은 캐릭터보다
**남쪽**에 있다. 필드 이름도 `southReachCells` → `capLiftCells` 로 바꿨다.
넉넉한 판정 반지름(0.45 + 0.9) 때문에 방향이 틀린 상태에서도 벽 앞 판정은 우연히 맞았고,
방 중앙의 기둥이 잘못 걸리는 것으로만 드러났다.

수정 전후 실측(더미 x=10, 판정 반지름 1.35):

| 더미 y | 수정 전 | 수정 후 | 실제 |
| --- | --- | --- | --- |
| 2.35 | 덮임 | 덮임 | 남쪽 벽 cap 앞 — 맞다 |
| 3.5 | 덮임 | 덮임 | cap 이 리프트만큼 올라와 여기까지 덮는다 |
| 4.9 | 덮임 | **안 덮임** | 방 한가운데 — 수정 전이 틀렸다 |
| 7.0 | 덮임 | **안 덮임** | 방 한가운데 |
| 10.0 | 덮임 | 덮임 | 대형 기둥(열 9~12, 행 8~9) cap 앞 — 맞다 |

오클루더 알파 실측으로도 확인했다.

- 더미 y=4.9 → `FrontStructure 0` alpha **1** · `FrontStructure 1` alpha **1** (페이드 없음)
- 더미 y=2.35 → `FrontStructure 0` alpha **0.34** · `FrontStructure 1` alpha **1** (겹친 청크만)

수정 전에는 두 위치 모두 0.34 였다.

### 15.4 검증

- 컴파일 오류 0 · 내 코드에서 온 콘솔 오류 0
  (`MCP-FOR-UNITY: Port 6401 …` 와 `Account API …` 는 도메인 리로드·계정 서비스 경고로 무관하다)
- EditMode **294/294 통과** (배치 6 의 277 + 신규 17)
- `Shader.isSupported == true` — 테스트가 직접 확인
- Visual Lab 실측: 관심 더미 `covered=false vis=0` / 적 더미 `covered=true vis=1` — 같은
  프레임에 두 모드가 서로 다르게 동작한다
- 캡처 3장 신규, 전부 다른 해시
  - `visual-lab-b3-art-silhouette-off.png`
  - `visual-lab-b3-art-silhouette-on.png` — 관심 캐릭터 청록 림 + 적 위협 실루엣
  - `visual-lab-b3-art-silhouette-threat.png` — 관심 캐릭터가 벽에서 벗어난 상태에서 적만 보정
- 기존 캡처는 실루엣을 **끈 상태**로 찍는다(`Shot.Silhouette` 기본 false)

### 15.5 Visual Lab 변경

- **적 더미 추가.** §6.6 의 "적은 완전 투명 처리하지 않는다" 를 보려면 관심 대상이 아닌
  개체가 전경 뒤에 하나 있어야 한다 — 그 앞의 벽은 페이드하지 않으므로, 실루엣이 벽 위로
  올라오는지가 유일한 확인 방법이다. 알파 모양이 있는 캡슐 스프라이트를 코드로 만든다.
- 관심 더미의 본체 스프라이트를 1×1 에서 64×128 판으로 바꿨다. 1×1 텍스처는 이웃 탭이
  전부 텍스처 밖이라 림이 전체 채움이 된다. **색과 모양은 이전과 같아서** 기존 캡처와
  비교는 계속 가능하다.
- 키 **H** — 실루엣·림 켜기/끄기

### 15.6 남은 것

1. 팀 색은 지금 Lab 이 상수로 준다. 직업별 팀 색은 §8.8 캐릭터 데이터가 오면 그쪽에서 온다.
2. 림 두께는 원본 스프라이트 텍셀 기준이라 캐릭터 아트의 해상도가 확정되면 한 번 다시 잡는다.
3. `LightSocketRenderer`(§7.3) — 독립 램프·결정 소켓
4. 임포트 후 재검증(§12.2 2단계)
5. Light Blend Style 확장(Mask G/B/A) — 본선 조명 수치 확정과 함께

---

## 16. 배치 8 — 전체 승인 패키지 수령 (revision 16, 2026-09-08)

Codex 가 테스트 방 v01 전량을 납품했다. 승인 51개 · PNG 239장.

### 16.1 패키지 완결성 (`art-production/test-room-v01/process/production-spec.md` §7 기준)

| ID 범위 | 요구 | 납품 | |
| --- | ---: | ---: | --- |
| FLR 바닥 | 6 | 6 | OK |
| WTP 벽 상단 | 6 | 7 (base 6 + rim 1) | OK |
| WFR 벽 정면 | 6 | 6 | OK |
| ARC 아치 | 1 | 1 | OK |
| PIL 기둥 | 2 | 2 | OK |
| LIN 레일·배관 | 6 | 6 | OK |
| LGT 조명 소품 | 3 | 3 | OK |
| DEC 장식 | 10 | 10 | OK |
| HERO 대형 설비 | 1 | 1 | OK |
| FGV 전경 | 4 | 4 | OK |
| VFX | 4 | 4 | OK |
| (추가) AO 접촉 데칼 | — | 1 | |

부족한 자산군은 없다. `rejected` 2개(ARC-001-SOURCE, WFR-A-C-SOURCE-SET)와 `working`
1개(WFR-BC-SOURCE-SET), `concept` 2개는 규칙대로 임포트에서 제외됐다.

### 16.2 검사 결과 — 오류 0 · 경고 1

첫 실행은 **경고 47개**였다. 배치 3 과 같은 패턴으로 대부분이 **검사기 쪽 잘못**이었다.
승인 패키지가 배치 3 때보다 풍부한 스키마를 쓰는데 검사기가 그걸 읽지 않았다.

| 첫 경고 | 실제 | 조치 |
| --- | --- | --- |
| `sortingLayerHint 'Ground'` 매핑 불가 ×6 | `Ground` 는 명백히 `GroundBase` 다 | 별칭 추가 → 참고로 내려감 |
| `visualHeightCells` 없음 ×26 | 패키지는 **피벗 위치로 자산 성격을 가른다**. 가운데 피벗 = 바닥에 눕는 평면(바닥·레일·cap·VFX), 아래 피벗 = 솟는 자산 | 되묻지 않고 발점 피벗에서 복원(`pivotY / 128`) |
| `shadowCasterPath` 없음 ×7 | 이미지 경로가 아니라 **셀 좌표 다각형**(`shadowCasterFootprintCells`)으로 왔다 — 8개 자산에 전부 들어 있다 | 그 필드를 읽어 카탈로그에 꽂음 |
| 전경 자산에 `occluderGroup` 없음 ×4 | `foregroundOccluder`(bool) + `fadeMaskPath` 로 왔다 | 자산마다 독립 그룹(assetId)으로 기본값 |
| 비타일 자산 패딩 없음 ×4 중 VFX 3개 | VFX 는 반복 재생 오버레이라 **패딩을 넣으면 이음새가 보인다** | VFX 를 패딩 요구에서 제외 |
| 캐스터 윤곽 없음 ×13 (2차) | 전부 1×1 소품이다. 한 셀 footprint 에서는 사각형이 곧 정확한 윤곽이라 아트가 줄 수 있는 것이 없다 | 여러 셀 자산만 경고 |

남은 경고는 **1개** — `TR01-ARC-001` 알베도 알파가 캔버스 경계에 닿는다(권장 8px 여백).
진짜지만 사소하고, Bloom·필터링에서 가장자리가 잘릴 수 있다는 주의다.

새로 읽는 manifest 필드: `shadowCasterFootprintCells` · `foregroundOccluder` · `fadeMaskPath`
· `pivotPixelsBottomOrigin`(교차 확인용). `MiniJson` 에 `GetBool` 과 `GetFloatPairs` 를 추가했다.

**캐스터 윤곽 좌표계**: 아트는 footprint **좌하단 원점**으로 준다(아치 5×1 →
`[0,0] [5,0] [5,1] [0,1]`). 런타임은 **발점(아래 변 중앙)** 기준이므로 X 에서 폭의 절반을
뺀다. 사각형을 준 자산은 내부 기본값(`FootprintContourCells`)과 결과가 완전히 같다 —
아트가 값을 넣고 빼도 화면이 바뀌지 않는다. 테스트로 잠갔다.

### 16.3 임포트 결과

`Assets/Art/Visual/TestRoomV01` 에 250장(자산 235 + 아틀라스 15).

**EnvironmentKit 타일 슬롯**

| 슬롯 | 배치 5 | 지금 |
| --- | ---: | ---: |
| floorBase | 6 | 6 |
| wallTop | 1 | 6 |
| wallTopRim | 1 | 1 |
| wallFront | 1 | 6 |
| contactAo | 1 | 1 |

**채널 아틀라스** (§8.1 정합)

- `floor` 408×272 · 3×2 셀 · 자산 6 · 채널 5종
- `walltop` 408×408 · 3×3 셀 · 자산 7 · 채널 5종
- `wallfront` 408×272 · 3×2 셀 · 자산 6 · 채널 5종

**SetPieceCatalog** 27개(세트피스·소품·전경·레일 — VFX 4개는 제외). 그림자 윤곽 8개 ·
조명 소켓 4개 · 오클루더 그룹 4개.

### 16.4 검증

- 컴파일 오류 0 · 내 코드에서 온 콘솔 오류 0
- EditMode **304/304 통과** (배치 7 의 294 + 신규 10 · `ApprovedArtSchemaTests`)
- 검사기 오류 0 · 경고 1
- 캡처 29장 갱신 — 바닥·벽 상단·벽 정면이 6종 변이로 깔리고 세트피스 7종과 발광이 모두 보인다

**아틀라스 이음새 없음.** 6종 변이를 한 아틀라스에 담는 것이 가장 큰 위험이라 바닥을
4배로 확대해 확인했다. 셀 경계(128px 배수)에 격자선이 없다 — 4px 가장자리 확장과
셀 배수 정확 일치가 제대로 동작한다. 만든 아틀라스를 픽셀로 직접 재서 6셀 모두
모든 채널에서 균일함도 확인했다(알베도 54/50/62 · AO 252~253 · Emission 0 · Mask 0/20/0).

**오독 하나를 기록해 둔다.** 확대 캡처에서 기둥 옆 바닥이 "평평한 밝은 사각 패치" 로
보여 결함으로 의심했다. 원본 PNG·아틀라스·렌더러를 차례로 재 보니 전부 정상이었고,
가로 스캔에서 x=300(=기둥의 셀 경계)에서 30 → 5 로 급락하는 것이 전부였다. **거의 검게
렌더되는 기둥 옆의 정상 바닥**이었다. 이미지만 보고 결함을 만들어 내지 말 것.

### 16.5 남은 것

**아트가 줬지만 아직 안 쓰는 데이터**

1. `fadeMaskPath` (FGV 4종) — 전경 페이드가 지금은 균일 알파다. 마스크를 쓰면 캐릭터를
   덮는 부분만 골라 사라지게 할 수 있다(§6.6 의 정신에 더 가깝다).
2. `connectionPorts` (LIN 6종) — 레일·배관 연결. 고정 방에는 필요 없고 절차 맵 배치에서 쓴다.
3. `emissionMode` 문자열 — 발광 성격 분류. 지금은 Emission 텍스처만 쓴다.

**아직 화면에 없는 것**

Visual Lab 은 세트피스 7종만 배치한다 — 카탈로그 27개 중 20개(장식 10 · 전경 4 · 레일 6)가
한 번도 렌더되지 않는다. 특히 **전경 4종은 §6.6 실루엣 패스를 실제 아트로 검증할 유일한
수단**이므로 다음 배치에서 배치한다.

**계약 항목**

§8.6 은 "코너는 단순 회전으로 처리하지 않고 별도 제작한다" 고 요구하지만, WTP 6종은
`wall_top_a`~`f` 로만 이름이 붙어 코너 역할이 선언돼 있지 않다. 키트의 `outerCorner` ·
`innerCorner` · `westSide` · `eastSide` · `floorEdge` 슬롯은 비어 있다. 테스트 방 v01
목록(§7)이 요구하지 않았으므로 이번 패키지의 결함은 아니다. 다음 패키지에서 코너 역할을
manifest 에 선언해 주면 슬롯을 연결한다.

---

## 17. 배치 9 — 소품 전량 배치와 배경 잔여물 검출 (2026-09-08)

카탈로그 27개 중 20개가 한 번도 렌더되지 않던 것을 배치했다. 그 결과 **아트 결함 9건**을
찾아냈고, 검사기가 그 결함군을 앞으로 스스로 잡도록 만들었다.

### 17.1 Visual Lab 배치 확장

세트피스 소켓 7 → **27**. 전경 4 · 레일 6 · 장식 10 을 추가했다.

- **전경 4종**은 남쪽 벽 바로 앞 행 2 에 세운다(열 3·7·11·15). footprint 2×1 이고 1.94셀
  솟으므로 행 2~4 를 덮는다 — 캐릭터가 그 위를 지나면 뒤로 들어간다.
- **레일 6종**은 행 6 열 13~18 에 한 줄로. 바닥에 눕는 평면 자산이라 피벗이 중앙이다.
- **장식 10종**은 기둥과 기존 세트피스를 피해 흩어 놓았다.

**씬에 직렬화된 배열이 코드 기본값을 덮는 문제**를 만났다. `_setPieceSockets` 는
`[SerializeField]` 라 씬이 옛 7개짜리 사본을 들고 있었고, 코드의 27개가 무시됐다.
기본값을 `DefaultSetPieceSockets`(static readonly)로 빼고
`EditorResetSetPieceSockets()` 재동기화 경로를 만들어 아트 전환 메뉴에서 부른다.

**오클루더의 남쪽 도달 거리를 시각 높이에서 받게 고쳤다.** `SetPieceSpawner` 가
`capLiftCells` 를 기본값 1셀로 두고 있었는데, 소품 스프라이트는 발점에서 위로 시각 높이만큼
솟으므로 실제 도달 거리는 `시각 높이 − footprint 깊이` 다. 전경 난간(1.94셀)이 캐릭터를
가리는데도 페이드가 걸리지 않을 값이었다. 실측 `capLift=0.9375` 로 정정됐다.

### 17.2 찾은 아트 결함 — 배경 키잉 실패 9건

배치 직후 캡처에서 소품 여러 개가 **창백한 사각형을 뒤에 달고** 나왔다. 원인을 순서대로 좁혔다.

1. 알베도 원본 PNG 측정 — 투명 28~82%, 테두리 알파 0. **정상이다.**
2. 아틀라스 측정 — 6셀 모두 모든 채널에서 균일. **정상이다.**
3. 임포트 설정 — `alphaSource=FromInput` · `alphaIsTransparency=true` · RGBA32. **정상이다.**
4. 셰이더 — `lit.a = ch.albedo.a` 이고 RGB 블렌드가 `SrcAlpha` 라 알파 0 은 기여가 0.
   **정상이다.**
5. 렌더러를 하나씩 끄는 대조 — 소품 본체가 범인이었다.
6. 소품을 `Sprites/Default` 로 바꿔 렌더 — **박스가 그대로 남았다.** 내 셰이더가 아니다.
7. 픽셀을 직접 읽자 답이 나왔다. 배경이 불투명이 아니라 **알파 17~159 의 밝은 회색
   반투명 막**이었다. 배경 제거가 덜 된 상태다.

전 자산의 알파 분포를 재니 두 무리로 깨끗하게 갈렸다.

| | 반투명 픽셀 비율 |
| --- | --- |
| 깨끗한 자산 31종 | 0.9 ~ 3.2% (부드러운 가장자리 한 겹) |
| 오염된 자산 9종 | 25.8 ~ 46.2% |

사이가 크게 비어 있어 판정에 애매함이 없다. 대상 9종:

| 자산 | 반투명 | 불투명 | 투명 |
| --- | ---: | ---: | ---: |
| TR01-DEC-BARREL-A | 46.2% | 25.3% | 28.5% |
| TR01-DEC-HARDWARE-A | 45.4% | 25.8% | 28.8% |
| TR01-DEC-BUCKET-A | 42.4% | 24.8% | 32.9% |
| TR01-DEC-CABLE-SPOOL-A | 37.5% | 33.1% | 29.4% |
| TR01-DEC-PAPERS-A | 37.3% | 29.3% | 33.4% |
| TR01-LIN-RAIL-BROKEN-A | 32.9% | 20.6% | 46.5% |
| TR01-DEC-TOOLBOX-A | 28.6% | 36.8% | 34.6% |
| TR01-LIN-PIPE-ELBOW-A | 26.7% | 45.4% | 27.9% |
| TR01-LIN-CABLE-JUNCTION-A | 25.8% | 40.7% | 33.5% |

### 17.3 검사기가 이 결함군을 잡게 만들었다

`PngInfo` 는 헤더만 본다 — "알파 채널이 있는가" 는 참이었으므로 이 결함은 원리적으로
잡히지 않았다. 그래서 픽셀을 센다.

- **`PngAlphaStats`** (신규) — PNG 를 직접 디코딩해 알파 분포를 센다. zlib 를
  `DeflateStream`(헤더 2바이트 건너뜀)으로 풀고 행 필터 0~4 를 되돌린다.
  `art-production/` 은 `Assets/` 밖이라 `Texture2D` 를 쓸 수 없기 때문이다.
  C# 결과가 파이썬 독립 측정과 소수점까지 일치한다.
- **`ApprovedArtContract.BackgroundResidueSuspect`** — 순수 판정.
  반투명 > 10% **이고** 불투명 ≥ 5% 일 때 잔여물로 본다.
  - 임계값 10% 는 실측 공백(3.2% ~ 25.8%) 안에서 골랐다. 테스트가 이 사실을 잠근다.
  - 불투명 하한이 필요한 이유: **접촉 AO 데칼**은 반투명 41.4% 인데 정상이다(불투명 0%).
    부드러운 그라디언트가 전부인 자산과 "불투명 본체 + 넓은 반투명 막"은 형태가 다르다.
- **자산 단위 제외** — 소품 하나가 깨졌다고 패키지 전체를 막으면 나머지 검증이 멈춘다.
  `ApprovedAsset.HasError` 와 `ArtValidationReport.Importable` 을 두어 깨진 것만 빼고
  임포트하고, 제외 사실을 리포트 머리에 크게 남긴다.

결과: **오류 9 · 경고 1 · 42개 임포트 · 9개 제외.**

### 17.4 검증 — §6.6 을 실제 아트로 확인

전경 4종이 들어오면서 배치 7 의 실루엣 패스를 처음으로 생산 아트로 검증했다.

`visual-lab-b3-art-behind-foreground.png`

- 더미가 선 **파이프 프레임만** 목표 알파로 페이드한다. 같은 행의 난간·매달린 케이블은
  불투명하게 남는다 — 셀 마스크 겹침 판정(배치 7)이 실제 아트에서도 그룹을 갈라낸다.
- 적 뒤의 암반은 **페이드하지 않는다**. 적은 관심 대상이 아니다(§6.6).

`visual-lab-b3-art-silhouette-on.png`

- 관심 캐릭터 — 얇은 청록 팀 림
- 적 — 불투명한 암반 **위로** 붉은 위협 실루엣이 올라온다. `WorldFX` 가
  `FrontStructure` 보다 위라는 계약이 화면에서 확인된다.

### 17.5 최종 상태

- EnvironmentKit: floorBase 6 · wallTop 6 · wallTopRim 1 · wallFront 6 · contactAo 1
- SetPieceCatalog: **18개**(27 − 제외 9) · 윤곽 8 · 소켓 4 · 오클루더 그룹 4
- 임포트된 PNG 235장
- EditMode **314/314 통과** (배치 8 의 304 + 신규 10 · `PngAlphaStatsTests`)
- 런타임 콘솔 오류 0

### 17.6 아트 트랙에 필요한 것

1. **위 9종 재키잉** — 배경을 완전히 지워 반투명을 가장자리 한 겹(3% 안쪽)으로. 지금은
   임포트에서 제외돼 화면에 없다.
2. `TR01-ARC-001` 알파 패딩 8px(Emission 16px).
3. (선택) 코너 역할 선언 — §8.6 은 코너를 별도 제작하라고 하지만 WTP 6종에 역할 표시가
   없어 `outerCorner`·`innerCorner` 슬롯을 연결할 수 없다.

### 17.7 도구 쪽 기록 — MCP 인스턴스 라우팅

작업 중 MCP 가 SlimeForge 로 여러 번 떠내려갔다(`dataPath` 로 확인). `set_active_instance`
는 세션 기본값일 뿐이고 다른 대화가 바꾸면 따라간다. **긴 작업에서는 중간중간
`Application.dataPath` 를 다시 찍어 확인해야 한다** — CLAUDE.md 의 지시가 한 번이 아니라
반복 확인이어야 하는 이유다. 잘못 라우팅된 호출은 "어셈블리를 찾을 수 없다" 나
빈 결과로 나타나 결함으로 오인하기 쉽다.

---

## 18. 배치 10 — 임포트 후 재검증과 아트 수정 반영 (2026-09-08)

### 18.1 §12.2 2단계 — 임포트 후 재검증

1단계(`ApprovedArtValidator`)는 `art-production/` 의 **원본 파일**을 본다. "납품물이
규격에 맞는가" 를 묻는다. 2단계는 다른 것을 묻는다 — **Unity 가 실제로 무엇을
적용했는가.** 원본이 완벽해도 임포터 설정이 어긋나면 화면이 틀린다.

특히 데이터 맵(Normal·Emission·Mask·AO)의 sRGB 가 켜지면 Unity 가 감마 변환을 걸어
노멀 방향과 AO 농도가 달라지는데 **콘솔에 아무 것도 남지 않는다.** 오늘 소품 결함을
쫓는 동안 임포터 설정을 손으로 확인했다 — 그 확인을 자동화한 것이다.

| 파일 | 역할 |
| --- | --- |
| `Editor/ArtPipeline/ImportSettingsContract.cs` | 채널별 임포트 설정의 **단일 출처**(`ImportExpectation`) |
| `Editor/ArtPipeline/PostImportValidator.cs` | 되읽기 검증 + 메뉴 `비주얼 · 임포트 후 재검증 (§12.2)` |
| `Tests/EditMode/ImportSettingsContractTests.cs` | 계약·왕복·경로 규칙 |

**설정을 한 곳에서만 정의하게 바꿨다.** 적용하는 코드와 검증하는 코드가 각자 규칙을
들고 있으면 둘이 조용히 갈라지고, 그러면 "검증은 통과하는데 화면이 틀린" 상태가 된다 —
검증이 없는 것보다 나쁘다. `ApprovedArtImporter.ConfigureImporter` 가 자기 설정 목록을
버리고 `ImportExpectation.For(channel).ApplyTo(ti)` 를 쓰게 했고, 검증기는 같은
`ImportExpectation` 으로 `DescribeMismatch(ti)` 를 부른다. 임포트 경로 규칙도
`ApprovedArtImporter.ImportedPathFor` 하나로 모았다.

검사 항목: 채널별 텍스처 타입·sRGB·alphaIsTransparency·밉맵·랩·필터·압축·PPU ·
스프라이트 피벗이 manifest `pivotPixels` 와 일치하는지 · 임포트된 텍스처 크기가
manifest 와 같은지(Max Size 축소 감지) · 실루엣 자산의 GPU 포맷에 알파가 살아 있는지 ·
파일이 실제로 존재하는지.

**검증기가 실제로 잡는지 확인했다.** "통과" 만 말하는 검증기는 신뢰할 수 없으므로
설정을 일부러 두 군데 틀어 놓고 돌렸다.

```
자산 51개 · 텍스처 235장 · 오류 2 · 경고 0
  [오류] TR01-DEC-BARREL-A · …_albedo.png — 스프라이트 피벗 (0.5, 0.5) 가
         manifest pivotPixels [128, 248] → (0.5, 0.0313) 와 다르다
  [오류] TR01-DEC-BARREL-A · …_normal.png — 'normal' 임포트 설정 불일치: sRGB True(기대 False)
```

둘 다 정확히 지목했고, 원복 후 다시 오류 0 이 됐다. 피벗 허용 오차는 1/512(정규화) 다 —
128px 셀의 1/4 픽셀로, 눈에 보이지 않는 수준에서만 통과시킨다.

### 18.2 아트 수정 반영 — 배경 잔여물 9종 해결

배치 9 에서 보고한 9종을 아트 트랙이 수정했다(파일 시각 18:20~18:21).

| | 수정 전 반투명 | 수정 후 |
| --- | ---: | ---: |
| TR01-DEC-BARREL-A | 46.2% | **0.9%** |
| TR01-DEC-HARDWARE-A | 45.4% | **0.6%** |
| TR01-DEC-BUCKET-A | 42.4% | **1.5%** |
| TR01-DEC-CABLE-SPOOL-A | 37.5% | **0.6%** |
| TR01-DEC-PAPERS-A | 37.3% | **0.5%** |
| TR01-LIN-RAIL-BROKEN-A | 32.9% | **0.2%** |
| TR01-DEC-TOOLBOX-A | 28.6% | **0.5%** |
| TR01-LIN-PIPE-ELBOW-A | 26.7% | **2.4%** |
| TR01-LIN-CABLE-JUNCTION-A | 25.8% | **0.8%** |

전부 목표(3% 이하)를 만족하고, 5개 채널의 알파 분포가 자산마다 완전히 일치한다
(불일치 0). 캔버스·피벗·footprint 도 그대로다.

**주의 — 검증 중 파일이 바뀌면 결과가 흔들린다.** 재검증을 처음 돌렸을 때
"자산 45개 · 텍스처 205장" 이 나와 계산(42/190)과 달라 결함으로 의심했는데, 그 순간
아트 트랙이 파일을 쓰고 있던 중간 상태였다. 잠시 뒤 51/235 로 안정됐다. **원본 파일을
읽는 검사는 아트 작업과 동시에 돌리면 중간 상태를 읽는다** — 숫자가 예상과 다르면
파일 수정 시각을 먼저 볼 것.

manifest 는 아직 revision 16 이고 해당 9개 항목의 `revision`·`alphaInspection` 이
갱신되지 않았다. 픽셀은 고쳐졌으므로 임포트·렌더에는 영향이 없는 기록상의 미완이다.

### 18.3 최종 상태

- 1단계 검사: 승인 51개 · **오류 0 · 경고 1**(아치 알파 패딩)
- **51개 전부 임포트**(제외 0) · 텍스처 235장
- 2단계 재검증: 자산 51 · 텍스처 235 · **오류 0 · 경고 0**
- `SetPieceCatalog` **27개**(제외 없음) · Visual Lab 에 27종 전부 배치
- 앵커 29(소품 27 + 더미 + 적) · 접촉 AO 29 · 그림자 캐스터 31 · Light2D 7
- EditMode **324/324 통과** (배치 9 의 314 + 신규 10)
- 캡처 29장 갱신 — 창백한 사각형이 모두 사라졌고 소품 27종의 실루엣이 정상이다

### 18.4 다음

1. `LightSocketRenderer`(§7.3) — 독립 램프·결정 소켓
2. `fadeMaskPath` 소비 — 전경 페이드를 균일 알파에서 마스크 기반으로(아트가 이미 납품함)
3. Light Blend Style 확장(Mask G/B/A) — 본선 조명 수치 확정과 함께(§19 가 보류)
4. 품질 단계·광과민·카메라 흔들림 옵션(§14 단계 F)

---

## 19. 배치 11 — 광원 분류와 그림자 예산 (2026-09-08)

기능명세서 §7.3(광원 분류) + §13(그림자 Light2D 예산).

### 19.1 먼저 확인한 것 — `fadeMaskPath` 는 만들지 않았다

다음 작업으로 §6.6 의 마스크 기반 전경 페이드를 하려고 아트가 납품한 페이드 마스크 4장을
열어 봤다. **마스크 알파가 알베도 알파와 바이트 단위로 동일했다**(일치율 100.00%,
평균 차 0.00, 4종 전부). 마스크가 실루엣 전체를 가리키므로 "전체가 페이드한다" 는 뜻이고,
이미 있는 균일 알파 페이드와 결과가 완전히 같다.

그래서 **소비 코드를 만들지 않았다.** 기능처럼 보이지만 화면이 하나도 바뀌지 않는 코드가
된다. 아트 트랙에 무엇을 담아야 하는지(뼈대는 알파 0, 캐릭터 높이에 걸리는 면은 255)를
`docs/codex-art-fix-request-r17.md` §7 에 적었다. 급하지 않은 충실도 항목이다.

### 19.2 구현한 파일과 조항 대응

| 파일 | 조항 | 역할 |
| --- | --- | --- |
| `Presentation/Visual/Lighting/LightClass.cs` | §7.3, §13 | 5분류 enum + 순수 규칙(`LightClassRules`) |
| `Presentation/Visual/Lighting/LightSocketRenderer.cs` | §7.3, §13 | `LightSocket` 컴포넌트 + 예산 집행 매니저 |
| `Environment/SetPieceCatalog.cs` | §7.3 | `LightSocketDef.lightClass` |
| `Environment/SetPieceSpawner.cs` | §7.3 | 소켓에 `LightSocket` 부착, 그림자 결정 위임 |
| `Editor/ArtPipeline/SetPieceCatalogBuilder.cs` | §12.2 | `emissionMode` 로 분류 유도 |
| `Editor/ArtPipeline/ApprovedArtValidator.cs` | §12.2 | `emissionMode` 읽기 |
| `Debug/VisualLabController.cs` | §12.3 | 매니저 배치 · 탐색광 등록 · 키 J |
| `Tests/EditMode/LightClassTests.cs` | §16 | 분류·자격·깜빡임·예산 |

### 19.3 확정한 기술 판단

**분류는 5개다.** §7.3 표의 5분류 중 `환경광선`(문틈·천장 균열)은 메시 또는 셰이더 콘이
필요해 §11.2 VFX 로 미뤘고, 대신 **`Indicator`(표시등)** 를 더했다. 실제 승인 아트의
드릴 소켓이 반지름 1.0~1.25셀 · 세기 0.25~0.35 로, 방을 밝히는 광원이 아니라 계기
표시등이다. 이를 작업등과 같이 취급하면 표시등이 그림자 예산을 먹어 정작 랜턴에
그림자가 없어진다.

**그림자 예산은 전역 결정이므로 매니저가 든다.** 광원 하나가 자기만 보고 "나는 중요하니
그림자를 켜겠다" 고 하면 방에 램프가 다섯 개일 때 예산이 무너진다. 매 프레임 전체를
우선순위(`ShadowPriority = 분류 보너스 + 반지름 × 세기`)로 줄 세워 위에서 예산만큼 켠다.
탐색광은 보너스 1000 으로 **항상 먼저** — 플레이어 손전등에 그림자가 없으면 벽이 읽히지 않는다.

**예산을 넘은 광원은 빛은 그대로 내고 그림자만 끈다.** 광원을 끄면 방이 어두워져 가독성이
무너지지만, 그림자는 없어도 형태가 읽힌다(§15.1). 테스트가 이 동작을 잠근다.

**정렬을 안정화했다.** 우선순위가 같을 때 등록 순서로 갈라 프레임마다 결과가 흔들리지
않게 했다 — 그림자가 두 광원 사이를 왕복하면 화면이 깜빡인다.

**깜빡임은 시간 0 에서 정확히 1 이다.** 대기 원근의 그레인과 같은 이유로
`FreezeFlicker` 를 두고 캡처 경로에서 켠다(§16.3). 사인 두 개를 겹쳐 단순 맥박처럼
보이지 않게 하고, 진폭은 작업등 ±6% · 광물광 ±4% 로 묶었다 — §7.3 이 요구하는 것은
"미세" 깜빡임이고, 크게 흔들면 광과민 옵션(§14 F)에 걸린다. 테스트가 2000 스텝을 돌려
0.9~1.1 범위를 벗어나지 않음을 확인한다. 위상은 assetId + 소켓 id 해시에서 유도해
같은 종류의 램프가 한꺼번에 흔들리지 않으면서 실행마다 같다.

**분류 유도는 임시 규칙임을 명시했다.** manifest 가 `lightClass` 를 직접 선언하는 것이
옳고, 그때까지 `emissionMode` 와 소켓 규모에서 유도한다. 규칙은
`LightClassRules.Classify` 한곳에 모여 있어 계약이 바뀌면 한 군데만 고친다.

**죽은 토글을 제거했다.** `SetPieceSpawner._socketLightsCastShadows` 는 이제 매니저가
결정을 갖기 때문에 아무 일도 하지 않는다. 동작하는 것처럼 보이는 토글을 남겨 두는 것이
없는 것보다 나쁘다.

### 19.4 검증

실제 승인 아트에서 유도된 분류:

| 자산 | 소켓 | 분류 | 반지름 | 세기 |
| --- | --- | --- | ---: | ---: |
| TR01-LGT-WORKLAMP-A | main | Worklamp | 3.0 | 0.75 |
| TR01-LGT-WARNING-A | main | Worklamp | 2.25 | 0.6 |
| TR01-LGT-CRYSTAL-A | main | MineralGlow | 3.5 | 0.8 |
| TR01-HERO-DRILL-A | service | Indicator | 1.25 | 0.35 |
| TR01-HERO-DRILL-A | status | Indicator | 1.0 | 0.25 |

Visual Lab 런타임 실측(광원 6개 등록):

```
그림자 켠 광원 3 / 등록 6
  Torch (Point)  Scout        shadowOn=True  shadowIntensity=0.85  intensity=2.2
  Light main     Worklamp     shadowOn=True  shadowIntensity=0.6   intensity=0.75
  Light main     Worklamp     shadowOn=True  shadowIntensity=0.6   intensity=0.6
  Light main     MineralGlow  shadowOn=False shadowIntensity=0     intensity=0.8
  Light service  Indicator    shadowOn=False shadowIntensity=0     intensity=0.35
  Light status   Indicator    shadowOn=False shadowIntensity=0     intensity=0.25
```

세기가 아트 값과 **정확히** 일치한다 — 시간 0 이라 깜빡임이 멈춘 상태이며, 캡처가
결정적이라는 뜻이다. 예산을 8 → 4 로 낮춰도 그림자 광원이 3개뿐이라 결과가 같다.
예산 초과 동작은 랩에서 재현되지 않으므로 단위 테스트로 확인했다 — 작업등 6개 ·
예산 4 에서 그림자 4개만 켜지고 나머지 둘은 빛을 유지하며, 표시등 8개를 더해도
예산을 먹지 않는다.

- 컴파일 오류 0 · 런타임 콘솔 오류 0
- EditMode **342/342 통과** (배치 10 의 324 + 신규 18)
- 캡처 29장 갱신

**막힌 곳 하나** — 테스트 어셈블리에 URP 참조가 없어 `Light2D` 를 쓰는 테스트가
컴파일되지 않았다(`UnityEngine.Rendering.Universal` 을 찾지 못함). 처음에는 테스트가
그냥 324개로 나와 통과한 것처럼 보였는데, 실제로는 <b>낡은 어셈블리가 돌고 있었다.</b>
`compileFailed` 를 확인해 알아냈다. `TunnelCrew.Tests.EditMode.asmdef` 에
`Unity.RenderPipelines.Universal.Runtime` 과 `...Universal.2D.Runtime` 을 더해 해결.
**테스트 수가 늘지 않았는데 통과했다면 컴파일 상태를 먼저 볼 것.**

### 19.5 다음

1. 전투광 펄스(§7.3 `Combat`) — 분류와 규칙은 있고 펄스 API 가 없다. 총구·폭발이
   붙는 §11.2 VFX 와 함께 하는 것이 맞다.
2. 환경광선(문틈·천장 균열) — §11.2 VFX
3. Light Blend Style 확장(Mask G/B/A) — 본선 조명 수치 확정과 함께(§19 가 보류)
4. 품질 단계·광과민·카메라 흔들림 옵션(§14 단계 F) — 이제 예산 전환(`HighQuality`)이
   있으므로 옵션 UI 에 붙일 첫 손잡이가 생겼다

---

## 20. 배치 12 — 품질 단계·접근성, 그리고 노멀맵이 조명에 닿지 않던 결함 (2026-09-08)

기능명세서 §13(품질 단계) + §10.3(접근성) + §14 단계 F. 작업 중 **아트 노멀맵 46장이
조명에 한 번도 쓰이지 않고 있던 결함**을 찾았다.

### 20.1 아트 revision 17 수령

| | 상태 |
| --- | --- |
| 배경 잔여물 9종 | 반투명 0.18~2.44% 로 재키잉 완료 · 5채널 알파 동일 · manifest `partialFraction` 기록 |
| 아치 알파 패딩 | 0px → **3px**(요청 8px). 권장 항목이라 경고로 남는다 |
| 페이드 마스크 | 그대로(§19.1 에서 급하지 않다고 표시한 항목) |

1단계 검사 **오류 0 · 경고 1**, 51개 전부 임포트(제외 0), 2단계 재검증 자산 51 ·
텍스처 235 · **오류 0 · 경고 0**.

### 20.2 구현 — 품질 단계와 접근성

| 파일 | 조항 | 역할 |
| --- | --- | --- |
| `Presentation/Visual/Projection/VisualQuality.cs` | §13, §10.3 | 4단계 enum + 순수 규칙(`VisualQualityRules`) |
| `Presentation/Visual/Projection/VisualOptionsController.cs` | §14 F | 단계·옵션을 시스템에 밀어 넣는 단일 지점 |
| `Depth/ForegroundFadeController.cs` | §10.3 | 전경 투명도 옵션(`AlphaOverride`) |
| `Lighting/AtmosphereDirector.cs` | §13, §10.3 | 안개 배율 · 그레인 배율 |
| `Shaders/TunnelCrewLit2D*.hlsl` | §13 | 전역 `_TCNormalReduce` |
| `Tests/EditMode/VisualQualityTests.cs` | §16 | 단계 값·가독성 불변·접근성 |

**`Low` 의 그림자 광원은 4가 아니라 2다.** §13 표의 "일반 4 / 스트레이스 8" 은
**성능 봉투**이고, 품질 단계 목록의 "Low: 그림자 광원 2개" 는 **단계별 값**이다.
배치 11 에서 이 둘을 섞어 `ShadowBudgetLow = 4` 로 두었던 것을 고쳤다. 예산 결정을
`VisualQualityRules.ShadowBudget(tier)` 로 옮기고 `LightClassRules` 는 위임만 한다.

**가독성 필수 기능은 끄는 코드를 아예 두지 않았다.** §13 —
"가독성에 필요한 벽 정면, 전경 가림, 접촉 AO는 품질 단계에서 제거하지 않는다".
`VisualQualityRules.MayDisable` 이 벽 정면·전경 가림·접촉 AO·가려진 실루엣에 대해
항상 false 를 돌려주고, 컨트롤러에는 그것들을 끄는 경로가 없다. 테스트가 잠근다.

**전경 투명도 옵션은 "더 잘 보이게" 하는 방향만 허용한다.** 프로파일 목표 알파보다
낮을 때만 적용된다 — 전경을 더 불투명하게 고정하는 것은 §13 의 취지와 어긋난다.

**노멀은 줄이고 끄지 않는다.** Low 배율 0.45. 0 으로 끄면 벽 정면이 평평해져
가독성 조항과 충돌한다. 전역은 <b>감소량</b>(`_TCNormalReduce`, 기본 0)으로 담았다 —
"배율" 로 담으면 이 전역을 설정하지 않는 씬에서 노멀이 사라진다.

**광과민은 줄이는 게 아니라 0 이다.** "조금 깜빡임" 은 광과민에 안전하지 않다.
깜빡임·그레인이 정확히 0 이 되고, 멀미 옵션은 그와 독립적으로 카메라 흔들림만 0 으로 만든다.

실측:

| 단계 | 그림자 광원 | 안개 배율 | 노멀 감소 |
| --- | ---: | ---: | ---: |
| Low | **2** (3개 중 1개 잘림) | 0.35 | 0.55 |
| Medium | 3 | 0.8 | 0.15 |
| High | 3 | 1.0 | 0 |
| Ultra | 3 | 1.15 | 0 |

광과민 ON → 그레인 배율 0 · 조명 깜빡임 차단 · 흔들림 배율 1(독립).
멀미 ON → 흔들림 배율 0.

### 20.3 찾은 결함 — 노멀맵이 조명에 닿지 않았다

품질 단계 캡처를 추가했더니 **`quality-low` 와 `quality-ultra` 가 바이트 동일**했다.
배치 2 에서 배운 함정이라 그냥 넘기지 않고 추적했다.

1. 같은 구도·해상도로 직접 렌더해 비교 → 32% 바이트가 다르다. 렌더는 바뀐다.
2. 그런데 캡처는 동일하다 → 캡처 경로에서 무엇이 다른가?
3. 캡처 루프의 <b>모든</b> 단계를 그대로 재현해 비교 → **0 바이트 차이.**
4. 성공했던 테스트와의 차이는 하나였다 — 그때는 **대기 원근이 켜져 있었다.**
   즉 32% 차이는 전부 안개 배율(0.35 vs 1.15)에서 왔고,
   **그림자 예산과 노멀 세기는 픽셀을 하나도 바꾸지 않았다.**
5. `Light2D` 상태를 덤프 → `shadowsEnabled=True` 는 정상인데
   **모든 광원이 `normalMapQuality = Disabled`** 였다.
6. 반사로 켜고 다시 렌더 → **화면 픽셀의 42%가 달라졌다.**

원인: URP `Light2D.normalMapQuality` 의 <b>기본값이 `Disabled`</b> 다. 그래서
아트가 납품한 노멀맵 46장, 임포터의 `NormalMap` 타입 설정, `WorldLit`/`CharacterLit` 의
`NormalsRendering` 패스, `TCNormalTS` 의 세기 조절이 <b>전부 존재하는데도</b> 조명이
노멀 버퍼를 한 번도 읽지 않았다. §7.1 의 "노멀 강도와 재질별 반응 조절" 과 §7.3 의
탐색광 "노멀 반응" 이 배치 2 이후 계속 무효였다.

고친 방법 — 분류별로 품질을 정한다(`LightClassRules.NormalQuality`).

| 분류 | 노멀 품질 | 근거 |
| --- | --- | --- |
| 탐색광 | `Accurate` | §7.3 이 "노멀 반응" 을 명시 |
| 작업등 | `Fast` | 벽 요철은 보여야 하되 저렴하게 |
| 생체·광물광 | `Disabled` | §7.3 "저비용 비그림자 Light2D" |
| 표시등·전투광 | `Disabled` | 너무 작거나 순간적이다 |

`normalMapQuality` 는 읽기 전용 프로퍼티라 백킹 필드(`m_UseNormalMap`,
`m_NormalMapQuality`)를 반사로 쓴다 — `ShadowCaster2D` 형상과 같은 사정이다(§5).
실패하면 한 번만 경고하고 노멀 없이 계속 그린다. 열거값 이름이 URP 쪽과 같아야
파싱이 되므로 그 사실을 테스트로 잠갔다.

§13 의 "Low: 노멀 단순화" 도 이제 실제 의미가 생겼다 — `Accurate` → `Fast` 한 단계
강등이고, 끄지는 않는다.

실측(High): 탐색광 `Accurate` · 작업등 2개 `Fast` · 결정·표시등 `Disabled`.
(Low): 탐색광이 `Fast` 로 강등되고 작업등 하나가 그림자를 잃는다.

### 20.4 검증

- 컴파일 오류 0 · 런타임 콘솔 오류 0 · 셰이더 4종 모두 `isSupported`
- EditMode **364/364 통과** (배치 11 의 342 + 신규 22)
- 캡처 34장 — 신규 `quality-low` / `quality-ultra` / `quality-photosensitive`,
  세 장 모두 서로 다른 해시(단계가 화면에 실제로 반영된다)

**테스트가 틀렸던 것 하나** — `컨트롤러가_조명과_대기에_단계를_전달한다` 에서 메시지는
"흔들림은 그대로다" 라고 쓰고 기대값을 0 으로 적었다. 그대로면 1 이다. 코드가 맞고
테스트가 틀렸으므로 테스트를 고쳤다.

**셰이더 이름 오독** — 진단 중 `Shader.Find("TunnelCrew/WorldLit")` 가 null 을 돌려주어
셰이더가 깨진 줄 알았다. 실제 이름은 `Tunnel Crew/WorldLit`(공백 포함)이다.
셰이더는 정상이었다.

### 20.5 남은 것

1. 아치 알파 패딩 3px → 8px(Emission 16px) — 아트 트랙, 경고 1건
2. 페이드 마스크에 "페이드 가능 영역" 담기 — 아트 트랙, 충실도 항목
3. 전투광 펄스(§7.3 `Combat`)와 환경광선 — §11.2 VFX 와 함께
4. Light Blend Style 확장(Mask G/B/A) — 본선 조명 수치 확정과 함께(§19 가 보류)
5. 카메라 흔들림 실제 배선 — `VisualOptionsController.ShakeScale` 은 준비됐고
   `CameraRig` 의 Impulse 쪽에서 곱해 쓰면 된다(§10.3)

---

## 21. 배치 13 — 아트 인계서 계약 이행 (2026-09-08)

기능명세서 §14 단계 B 가 `art-production/test-room-v01/process/unity-handoff.md` 를
"Claude 의 실제 Unity 조립 기준" 으로 지목한다. **그 문서를 이번에 처음 읽었고**,
만족하지 않던 조항 6건을 처리했다.

### 21.1 §2 스프라이트 메시 Full Rect

인계서: "스프라이트 메시: Full Rect. 알파 트리밍으로 피벗·채널 정렬을 바꾸지 않는다."

임포터가 `spriteMeshType` 을 **설정하지 않아 Unity 기본값 `Tight`** 였다. Tight 는 알파를
따라 메시를 깎아서 채널 맵과 알베도의 UV 범위가 어긋날 수 있다.
`ImportExpectation.MeshType` 을 추가해 적용·검증을 같은 값에서 가져오게 했다.
메시 종류는 `TextureImporter` 프로퍼티가 아니라 `TextureImporterSettings` 에만 있어
피벗과 같은 블록에서 다룬다.

### 21.2 §4-3 벽 상단·정면 매크로 변형

인계서: "벽 상단 A–F는 구역별 매크로 변형으로 배치해 한 화면에서 모든 패턴을 균등
반복하지 않는다."

바닥만 `MacroHash`(구역 4셀)를 쓰고 **벽 상단·정면은 셀별 `Hash`** 였다. 그래서 변형
6종이 화면 전체에 고르게 흩어져 매크로 패턴이 생기지 않았다.
`SurfaceRules.WallMacroCells`(기본 3셀)를 추가하고 상단·정면 모두 구역 해시로 뽑는다.
상단과 정면은 소금을 달리해 같은 구역에서 같은 인덱스가 겹치지 않게 했다.

바닥(4셀)보다 작게 둔 이유: 벽은 화면에서 띠처럼 보이므로 구역이 크면 벽 한 면이
통째로 한 변형이 된다.

### 21.3 §2 `pivotNormalized` 교차 확인

인계서: "manifest 의 `pivotNormalized` 를 사용하고 `pivotPixels` 와 대조한다."

`pivotPixels` 만 읽고 있었다. 이제 둘을 비교해 어긋나면 **오류**로 보고한다.
허용 오차는 1/1024(정규화) — 128px 셀에서 1/8 픽셀이다. 임포트에는 계속
`pivotPixels` 를 쓴다(정수라 반올림 오차가 없다).

좌표계 관계도 확인했다 — `pivotNormalized` 는 **하단 원점**(Unity 규약),
`pivotPixels` 는 좌상단 원점이다. 드릴로 검산: 384 − 376 = 8, 8/384 = 0.0208333.
51종 전부 정합한다.

### 21.4 §3 `connectionPorts` 검증

레일·배관 6종의 연결 포트를 읽고 계약을 확인한다 — 변 이름이 네 방향 중 하나인가,
좌표가 그 변에 실제로 붙어 있는가, 종류 문자열이 있는가.

**런타임에서 아직 쓰지 않지만 지금 검증해 두는 것이 맞다.** 절차 맵 배치에서 쓰기
시작할 때 값이 틀려 있으면 레일이 어긋나고, 그때는 원인이 아트인지 배치 코드인지
가리기 어렵다. 현재 6종 전부 경고 없이 통과한다.

### 21.5 §5 인계 검증 스크립트 — 스크립트 버그를 고쳤다

`tools/art/validate-test-room-package.ps1` 을 처음 실행하니 **실패**했다.

```
TR01-HERO-DRILL-A: pivot is not on integer pixels
```

데이터를 재 보니 정확했다. 원인은 스크립트의 **부동소수 직접 비교**였다.

- `pivotNormalized[1] × 384` = 0.020833333333333332 × 384 = **7.999999999999999**
- `-ne [Math]::Round(...)` 로 비교해 참이 됨 → 오류로 보고

같은 문제가 다음 검사에도 있었다 — `pivotPixels [256,376]` 을 기대값
`376.000000000000000512` 와 `-ne` 로 비교했다.

두 곳에 허용 오차(0.001)를 넣어 고쳤다. 승인 아트가 아니라 검증 스크립트이므로
직접 수정했다(인계서 §5 의 "승인본을 직접 수정하지 말 것" 은 자산에 대한 조항이다).
결과가 인계서 §5 의 기대값과 정확히 일치한다.

```
ManifestRevision      : 17
ApprovedAssets        : 51
ValidatedFiles        : 235
DeliveryPixelsPerCell : 128
Result                : PASS
```

### 21.6 Normal Y 방향 — 뒤집히지 않았다

인계서가 "Unity 에서 처음 확인할 항목" 으로 지목한 항목이다. 배치 12 에서 노멀이
실제로 조명에 들어가기 시작했으므로 이제 검증 가능하다.

**첫 시도는 오해를 줬다.** 북쪽 벽 정면(남쪽을 향한다)에 남쪽·북쪽에서 각각 빛을
비춰 밝기를 비교했다. 남쪽이 더 밝았지만(0.1524 vs 0.1364), 노멀을 끈 대조군을
재 보니 노멀이 오히려 그 비대칭을 **줄였다**(+0.0369 → +0.0247). "Y 뒤집힘" 으로
읽힐 수 있는 결과다. 하지만 이 검사는 타일의 <b>평균</b> 노멀이 남쪽을 향한다고
가정하고, 손으로 그린 요철에서는 그 가정이 성립하지 않는다.

**결정적 검사**는 R 채널을 기준으로 삼는 것이다. +X = 오른쪽은 OpenGL·DirectX 규약이
같아 논쟁이 없다. 노멀이 알베도에서 파생됐으므로(production-spec §248) 높이맵 공식
`n ∝ (−∂h/∂x, −∂h/∂y, 1)` 이 성립하고, 탄젠트 +Y 가 위쪽이면
`corr(G, 위쪽 밝기 기울기) ≈ −1` 이 정상이다.

| 자산 | corr(G, 위쪽 기울기) | corr(R, 오른쪽 기울기) |
| --- | ---: | ---: |
| 벽 정면 A | −0.999 | −0.996 |
| 벽 상단 A | −0.998 | −0.998 |
| 상자 | −0.983 | −0.978 |
| 기둥 | −0.981 | −0.980 |

**두 채널의 부호가 같다.** Y 만 뒤집혔다면 G 는 +0.99, R 은 −0.99 로 갈렸을 것이다.
갈리지 않았으므로 표준 탄젠트 공간 규약이고 **Y 는 정상**이다.

### 21.7 검증

- 컴파일 오류 0 · 런타임 콘솔 오류 0
- EditMode **372/372 통과** (배치 12 의 364 + 신규 8 · `HandoffContractTests`)
- 1단계 검사 오류 0 · 경고 1(아치 알파 패딩 3px, 요청 8px)
- 2단계 재검증 자산 51 · 텍스처 235 · 오류 0 (Full Rect 포함)
- 인계 스크립트 PASS
- 캡처 34장 갱신 — 벽 상단이 3셀 구역 단위 띠로 보인다

### 21.8 남은 것

**아트 트랙**
- 아치 알파 패딩 3px → 8px(Emission 16px) — 경고 1건
- 페이드 마스크에 "페이드 가능 영역" 담기(현재 실루엣과 바이트 동일)
- 벽 상단 코너 역할 선언 — `outerCorner`/`innerCorner` 슬롯이 비어 있다

**다음 게이트(§19 · 인계서 §6)**
- **실제 게임 카메라로 촬영한 고정 방 비교 캡처.** 지금까지 캡처는 전부 Visual Lab
  전용 카메라다. 이 게이트를 통과해야 절차 맵 확장과 본선 조명 확정이 열린다.

**제 트랙**
- Material Mask G/B/A 조명 훅(Light Blend Style 확장 — 본선 조명 확정과 묶임)
- 카메라 흔들림 배선(`VisualOptionsController.ShakeScale` 준비됨)
- 전투광 펄스 · 환경광선 — §11.2 VFX 와 함께

## 22. 배치 14 — 첫 시각 피드백 4건 (2026-09-09)

사용자가 Visual Lab 을 직접 보고 처음 지적한 4건을 고쳤다. 넷 다 원인이 코드·데이터에
있었고, "구현이 없어서" 가 아니라 **값이 조용히 빠져서** 생긴 문제였다.

### 22.1 그림자가 전부 네모였다 — 아트 캐스터 데이터가 사각형

지적: "각 요소는 자기 형태가 있는데 그걸 무시하고 네모 그림자를 만든다."

벽 그림자는 `WallContourTracer` 로 실제 외곽선을 딴다. 문제는 세트피스였다 —
`SetPieceDef.shadowContourCells` 가 manifest 의 `shadowCasterFootprintCells` 에서 오는데,
revision 17 의 실제 상태가 이렇다:

- 승인 51종 중 **43종은 캐스터 데이터가 없다** → `FootprintContourCells` 사각형으로 떨어졌다.
- 있는 8종도 **전부 축 정렬 사각형**이다(아치 5×1 → `[0,0][5,0][5,1][0,1]`).

즉 데이터에 실루엣이 처음부터 없었다. §7.4 는 별도 윤곽을 요구하고 검사기도 경고를
남기고 있었지만, 화면은 그동안 네모로 나가고 있었다.

**임시 경로를 넣었다** — `SpriteAlphaContour` 가 원본 PNG 알파에서 근사 윤곽을 뽑는다.
`SetPieceCatalogBuilder.ResolveShadowContour` 가 manifest 윤곽이 사각형이 아닐 때는
그것을 쓰고, 사각형이거나 없을 때만 알파 경로로 간다. 아트가 §7.4 윤곽을 납품하면
자동으로 아트 쪽이 이기고, 지울 코드는 `SpriteAlphaContour` 하나다.

윤곽 추적 방식 — **마칭 스퀘어로 전체 외곽선을 따지 않았다.** 그림자는 지면에 눕는
형상이라 위쪽(아치의 지붕, 드릴의 팔)까지 넣으면 그림자가 실제보다 훨씬 길어진다.
발점 위 footprint 깊이만큼만 띠로 스캔해 각 행의 좌우 끝을 잇는다. 결과는 사각형보다
정확하고(기둥의 좁은 밑동, 아치의 두 다리) 계산이 결정적이다.

한 번 잘못 든 길: 알파를 읽으려고 `TextureImporter.isReadable` 을 켜고 `SaveAndReimport`
했다. 임포트 진행 중에는 Unity 가 meta 를 쓰지 못해
`Cannot open file ….meta for write` 로 실패했고, 그 자산은 `GetPixels` 도 못 했다.
지금은 PNG 바이트를 직접 `LoadImage` 로 디코드한다 — 임포트 설정을 건드리지 않아
아트 패키지 규약(§2)과도 어긋나지 않는다.

검증: 세트피스 카탈로그 27종 **전부** 실루엣 윤곽(4점 사각형 0개). 드릴 13점 ·
배럴 12점 · 양동이 16점 · 기둥 7~9점 · 아치 5점. 상자·궤짝은 원래 형태가 상자라
사각형에 가까운 것이 맞다.

### 22.2 조명이 오브젝트를 무시했다 — 대상 레이어에서 빠져 있었다

지적: "바닥 조명은 좋은데, 벽·오브젝트와는 반응하지 않겠다는 듯이 어색하다."

광원 7개가 **전부** 대상 Sorting Layer 목록에서 `WorldEntity` 와 `FrontStructure` 를
빼고 있었다. 들어 있던 것은 `Default·WorldVoid·GroundBase·GroundDetail·GroundDecal·
BackStructure·WallTop` 이었다. 그래서 작업등·수정·히어로 드릴·상자·공구함·암석(13종)과
난간·암반립·케이블(4종)이 빛을 한 줄기도 받지 못했다. 바닥과 벽만 밝은 화면이 그 결과다.

URP 는 대상 레이어를 **광원에 직렬화**한다. 씬을 만든 뒤에 레이어를 추가하면 그 레이어는
어느 광원에도 들어가지 않는다 — 눈에 보이는 오류 없이 조용히 빠진다. 그래서 씬 데이터에
맡기지 않고 `VisualLayers.Lit` 을 코드가 소유하게 했다:

- `VisualLayers.Lit` / `LitLayerIds()` — 빛을 받아야 하는 7개 레이어.
  제외: `WorldVoid`(빛이 닿을 표면이 아니다), `WorldFX·VisionAndGrade·WorldOverlay·UI`
  (자체 발광·후처리·UI 라 2D 조명을 곱하면 안 된다).
- `LightSocketRenderer.ApplyLitLayers` — 값이 실제로 다를 때만 쓴다(전역광은 URP 가
  레이어별로 따로 관리한다).
- 호출 지점 3곳: 소켓 광원(`Apply` 1패스), Visual Lab 의 씬 광원(`PokeLights`),
  본선 랜턴(`RunBootstrap.UseNormalMaps`).

### 22.3 조명에서 먼 곳이 어둡지 않았다 — 안개가 광원 거리를 몰랐다

지적: "조명과 먼 곳의 어둠이 전장의 안개처럼 더 짙어야 한다."

두 가지가 겹쳐 있었다.

1. `Atmosphere` 패스의 안개는 **화면 세로 위치(`uv.y`)** 로만 작동한다 — 화면 아래에
   깔리는 띠와 가장자리 비네트다. 광원과의 거리라는 개념이 아예 없다.
2. Visual Lab 이 전역 환경광을 **0.85 로 하드코딩**하고 있었다. 방향이 없는 빛이
   화면을 균일하게 채우니 어디도 어두워지지 않는다.

광원 거리를 아는 자리는 프래그먼트뿐이다. 그래서 `TunnelCrewLit2D.hlsl` 에서 만든다 —
블렌드 스타일 0 의 광원 텍스처(`_ShapeLightTexture0`)를 샘플해 **알베도를 곱하기 전의
조명량**을 얻고, `_DarkKnee` 아래만 곡선으로 끌어내린다. 알베도 전의 값을 쓰는 것이
중요하다. 그러지 않으면 검은 암석이 "빛이 없는 곳" 으로 오해받는다.
발광은 어둠 **뒤에** 더한다 — 수정·표시등은 빛이 닿지 않는 곳에서도 스스로 보여야 한다.

환경광은 프로파일이 정하게 바꿨다(`WorldVisualProfile.ambientIntensity`). §15.1
"광원이 꺼져도 형태가 읽힌다" 는 이제 환경광이 아니라 셰이더의 `_MinLight` 가 담보한다.

**한 번 과하게 갔다.** 처음 환경광 0.18 · 어둠 강도 0.85 · `_MinLight` 0.05 로 넣으니
방 중앙이 거의 검게 나와 공간을 읽을 수 없었다. 0.26 / 0.62 / 0.08 로 내려 균형을 잡았다.
어둠은 취향이 갈리는 축이라 세 값 모두 머티리얼·프로파일에서 조절할 수 있게 뒀다.

### 22.4 노멀맵 반응이 약했다 — 대부분의 광원이 노멀을 읽지 않았다

지적: "노멀맵이 조명에 더 선명하고 강하게 반응해야 한다."

배치 12 에서 노멀이 조명에 닿게 만들었지만, **분류 정책이 대부분을 껐다** —
탐색광만 Accurate, 작업등 Fast, 나머지(수정광·전투광·표시등) Disabled 였다.
방을 실제로 물들이는 마젠타·시안 **수정광이 바로 그 "나머지"** 라서 요철을 전혀 세우지
않았다. §7.3 의 "저비용 비그림자" 는 그림자 예산 조항이고, 노멀은 광원당 추가 드로우가
아니라 노멀 버퍼 샘플이라 비용 성격이 다르다. 그래서 정책을 올렸다:

| 분류 | 전 | 후 |
|---|---|---|
| 탐색광 Scout | Accurate | Accurate |
| 작업등 Worklamp | Fast | **Accurate** |
| 광물광 MineralGlow | Disabled | **Fast** |
| 전투광 Combat | Disabled | **Fast** |
| 표시등 Indicator | Disabled | Disabled (계기판 크기라 요철이 안 읽힌다) |

같이 고친 두 가지:

- **광원 높이(`m_NormalMapDistance`)가 3 이었다.** 이 값이 노멀 반응의 세기를 정한다 —
  크면 빛이 정면에서 오는 것이 되어 요철이 사라진다. 본선(`RunBootstrap`)이 쓰는 0.8 로
  맞췄다(`LightSocketRenderer.NormalMapHeightCells`).
- `_NormalStrength` 기본값을 올렸다 — WorldLit 1 → 1.6, CharacterLit 1 → 1.4.

그리고 22.3 의 환경광 인하가 여기에도 직결된다. 방향 없는 전역광이 밝기를 지배하면
점광원이 만든 요철은 씻겨 나간다 — 네 지적이 서로 얽힌 지점이다.

### 22.5 최종 수치

| 값 | 전 | 후 | 자리 |
|---|---|---|---|
| 환경광 세기 | 0.85(하드코딩) | 0.26(프로파일) | `WorldVisualProfile_Stratum1` |
| WorldLit `_MinLight` | 0.16 | 0.08 | WorldLit.shader |
| CharacterLit `_MinLight` | 0.30 | 0.14 | CharacterLit.shader |
| `_NormalStrength` | 1 / 1 | 1.6 / 1.4 | WorldLit / CharacterLit |
| 노멀 광원 높이 | 3 | 0.8 | LightSocketRenderer |
| 어둠 강도 | — | 0.62 / 0.42 | WorldLit / CharacterLit |
| 어둠 knee · 곡선 | — | 0.65 · 2 | WorldLit |
| 어둠 색 | — | (0.16,0.18,0.28) | WorldLit |

### 22.6 검증

- 컴파일 오류 0 · 런타임 콘솔 오류 0
- EditMode 신규 9 + 갱신 6 통과(`LitLayersTests` · `LightClassTests`)
- 씬 광원 7개 전부 대상 레이어 7종(WorldEntity·FrontStructure 포함) · 노멀 읽는 광원 4개
- 세트피스 카탈로그 27종 전부 실루엣 윤곽(사각형 0)
- 캡처 32장 갱신 — 수정광이 벽·기계에 빛 웅덩이를 만들고, 벽돌 요철이 보이며,
  모서리로 갈수록 어두워진다

`LightClassTests` 의 노멀 정책 테스트 3개는 옛 정책을 단정하고 있어 새 의도로 고쳤다.
정책을 바꾼 것이 목적이므로 테스트가 따라오는 것이 맞다.

### 22.7 남은 것

- **아트 트랙 — §7.4 그림자 윤곽 납품.** 알파 추적은 근사다. 아트가 "그림자로 보일
  부분" 을 정하는 것(아치라면 지붕이 아니라 두 다리)이 최종안이다.
- 아치 알파 패딩 3px → 8px (검사 경고 1건, 배치 13 부터 남아 있다)
- 지층별 조명·후처리 수치 확정 — 지금 값은 지층 1 프로파일 하나뿐이다(§19 보류 항목).
- 상자·궤짝처럼 원래 형태가 상자인 소품은 그림자도 상자다. 이것은 결함이 아니다.
