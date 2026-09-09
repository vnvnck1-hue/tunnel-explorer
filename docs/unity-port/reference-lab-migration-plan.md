# 레퍼런스 랩 이주 계획 — VisualLab 스택 → 레퍼런스 아트 기반

작성: 2026-09-09 · 대상 씬: `Assets/_Project/Scenes/LightingPresetLab.unity`
연계: [../urp-2d-lighting/09-tunnel-crew-application.md](../urp-2d-lighting/09-tunnel-crew-application.md) · [visual-overhaul-implementation.md](visual-overhaul-implementation.md) · [../unity-visual-overhaul-functional-spec.md](../unity-visual-overhaul-functional-spec.md)

## 0. 무엇을 하는가

`VisualLab`(승인 아트 `TestRoomV01` 기반)이 들고 있는 비주얼 스택을 **레퍼런스 직결 아트
(`ReferenceCalibrationV1`) 기반으로 옮긴다.** 본편(`Run.unity`) 이식은 아트 오버홀 완료까지 보류.

### 확정된 결정 (2026-09-09)

| 결정 | 내용 | 이유 |
|---|---|---|
| 목적지 씬 | **`LightingPresetLab` 확장** | 레퍼런스 아트 10장 + 광원 리그 + 대기 후처리 + 프리셋 8개가 이미 들어있다. 이주의 0단계가 끝나 있는 셈 |
| `ReferenceCalibrationV1` | **손대지 않는다** | `BuildReferenceCalibrationPreview` 가 매번 `NewScene(Empty, Single)` → 같은 경로 저장으로 덮어쓴다. "아트가 규격에 맞는가"를 결정론적으로 뽑는 QA 보드로 보존 |
| 착수 순서 | **1·2단계 먼저** | 아트를 기다리지 않는 시스템부터. 3단계에서 아트 요청서를 뽑는다 |

---

## 1. 이주 대상 목록 (VisualLab 스택 13종)

| 분류 | 시스템 | 아트 의존 | 단계 |
|---|---|---|---|
| 깊이 | `FootpointSorter` | 없음 | **1** |
| 그림자 | `ContactShadowRenderer` + `ContactShadow` | 없음 (런타임 생성 스프라이트) | **1** |
| 깊이 | `OccludedSilhouetteRenderer` + `OccludedSilhouette` | 없음 | **1** |
| 깊이 | `ForegroundFadeController` | 없음 | **1** |
| 후처리 | `VisualOptionsController` | 없음 | **1** |
| 조명 | `LightSocketRenderer` + `LightSocket` | 없음 | **2** |
| 후처리 | `AtmosphereDirector` | 없음 | 0 (완료) |
| 환경 | `SurfaceTopologyBuilder` · `ArraySolidField` | 없음 (규칙 코드) | 4 |
| 환경 | `EnvironmentChunkRenderer` | **`EnvironmentKit` 필요** | 4 |
| 그림자 | `ShadowGeometryBuilder` | **벽 격자 필요** | 4 |
| 환경 | `SetPieceSpawner` | **세트피스 자산 필요** | 4 |
| 디버그 | `VisualDebugLines` · `DepthDebugOverlay` | 없음 | 1 (곁들여) |

---

## 2. 아트 격차 — 사실 확인

| | 파일 | 채널 |
|---|---|---|
| `Art/Visual/TestRoomV01` (VisualLab) | 51 albedo · 46 normal · 46 mask · ao · emission | 완비 |
| `Art/Visual/ReferenceCalibrationV1` (이주 대상) | **10장, 전부 albedo** | albedo만 |

- `SurfaceMaterialSet` 의 `normal` / `emission` / `materialMask` / `ao` 는 전부 nullable →
  **albedo만으로도 돌아간다.** 노멀·마스크 반응만 없다.
- `EnvironmentKit.floorBase[]` ← `floor a/b/c` **3장으로 지금 채울 수 있다.**
  (d/e/f 는 바이트 단위로 동일한 이력 파일이고 가장자리 4px 이 완전한 검정 — 타일맵에 깔면 격자선이
  된다. 2026-09-09 실측. 승인 바닥은 A/B/C 뿐이다.)
- `EnvironmentKit.wallTop[]`(cap) / `wallFront[]`(1셀 폭 × lift 높이, 하단 중앙 피벗) ←
  레퍼런스에는 `wall_a` **한 장뿐**이고 파이프가 달린 벽 패널이라 **타일러블이 아니다.**
  → 4단계를 막는 유일한 항목.

---

## 3. 단계

### 0단계 — 완료 (2026-09-09)
레퍼런스 아트 보드(floor a~c 격자 + 벽/수정/램프/드릴러) · 광원 리그(Global · Worklamp ·
Scout · MineralGlow · Indicator) · `AtmosphereDirector` · `LabShadowBlob` 임시 블롭 ·
`LightingPresetSwitcher` + 프리셋 8종 · WASD 이동 폰.

### 1단계 — 아트 의존 없는 시스템 이주

#### 선행 제약 3개 (배선 조사에서 확인, 2026-09-09)

**1-A. 정렬 레이어를 `Default` → `VisualLayers` 로 옮겨야 한다.**
프리셋 랩은 지금 전부 `Default` 레이어에 있고 광원도 `targetSortingLayers = {0}` 이다.
그런데 `ContactShadowRenderer` 는 그림자를 `VisualLayers.GroundDecal` 에,
`OccludedSilhouetteRenderer` 는 실루엣을 `WorldFX` 에 놓는다. 레이어 체계 없이는 이 둘이
자기 자리를 찾지 못한다. 배치:

| 대상 | 레이어 |
|---|---|
| 바닥 격자 | `GroundBase` |
| 벽 · 수정 · 램프 | `BackStructure` (깊이 밴드 안) |
| 드릴러 | `WorldEntity` |
| 접촉 그림자 | `GroundDecal` (렌더러가 넣는다) |
| 실루엣 · 림 | `WorldFX` (렌더러가 넣는다) |

광원은 `VisualLayers.LitLayerIds()` 로 바꾼다 — 이 목록은 `Default` 를 **포함**하므로
(본선 Run 이 아직 Default 에 살아서 일부러 넣어둔 것) 이주 중간 상태에서도 안전하다.

**1-B. `VisualHeightAnchor` 가 트랜스폼 위치를 지배한다.**
`Apply(unitsPerCell)` 이 `IsometricProjection.ToRender(groundPosition)` 으로 위치를 다시
쓴다. 즉 손으로 놓은 `transform.position` 은 무시된다. 따라서
**`LightingLabPawn` 은 `transform.position` 대신 `groundPosition`(셀 좌표)을 움직여야 한다.**
`WorldVisualProfile_Stratum1` 의 투영이 항등(`basisX (1,0)` · `basisY (0,1)`)이라
셀 좌표 == 월드 좌표이므로 기존 좌표값은 그대로 쓸 수 있다.

**1-C. `ShadowGeometryBuilder` 는 1단계에 못 온다.**
`Bind(_env.IsWallCell, cols, rows)` 로 **`EnvironmentChunkRenderer` 에 의존**한다.
벽 격자가 없으면 성립하지 않으므로 4단계로 미룬다. 그때까지 벽 그림자는
`LabShadowBlob`(Negative)이 대신한다.

#### 작업 순서
1. **레이어 이주** (1-A) + 광원 `targetSortingLayers = LitLayerIds()`
2. `FootpointSorter` · `ForegroundFadeController` · `OccludedSilhouetteRenderer`
   — 셋 다 `.Profile = WorldVisualProfile_Stratum1` 배선
3. 드릴러·프롭에 `VisualHeightAnchor` 부여 (`groundPosition` = 현재 좌표) + 폰을
   `groundPosition` 구동으로 변경 (1-B)
4. `ContactShadowRenderer` + 각 개체에 `ContactShadow(radius=0, opacity=0)`
   — **0 은 "프로파일 값을 쓴다"는 뜻.** 임시 `LabShadowBlob`(Contact)을 여기서 제거한다.
   프리셋의 `blobStrength` 축이 `contactShadowOpacity` 정식 경로로 바뀐다
5. 드릴러에 `OccludedSilhouette(mode = Interest)` + 관심 대상이 아닌 더미 하나 추가
   — §6.6 "적은 완전 투명 처리하지 않고 위협 실루엣만 보장" 을 볼 대조군이 필요하다
6. `VisualOptionsController` · `VisualDebugLines` · `DepthDebugOverlay`

**남기는 것**: `LabShadowBlob`(Negative)은 유지한다. 네거티브 라이팅은 본편에도 없는
격차(§B-2)이고 정식 시스템이 아직 없다. 5단계에서 freeform 으로 승격한다.

**검증**: 프리셋 8개를 눌러 회귀 확인 + 접촉 그림자가 프로파일 값을 따라 움직이는지
+ 드릴러가 벽 뒤로 갔을 때 실루엣이 올라오는지.

### 2단계 — 광원 파이프라인 정식화
손으로 놓은 `Light2D` 5개 → `LightSocketRenderer` + `LightSocket` 로 교체.
`LightClass` 5분류(Scout/Worklamp/MineralGlow/Combat/Indicator) · 미세 깜빡임 ·
**그림자 예산**(§13) 이 실제로 작동하게 한다.

**검증**: 광원을 예산 초과까지 늘려도 그림자 개수가 지켜지는지. 표시등이 예산을 먹지 않는지.

### 3단계 — 표면 자산 authoring + 아트 요청서
- `SurfaceMaterialSet_Reference_floor` 생성 (albedo만, 나머지 채널 비움)
- `EnvironmentKit_ReferenceV1.floorBase = floor a~f`
- `wallTop` / `wallFront` 에서 **의도적으로 멈춘다**
- **산출물**: Codex 아트 요청서 — cap 변형 N장 · front(1셀 폭 × lift) · 볼록/오목 모서리 ·
  바닥-벽 접합 AO · 채널맵(normal/mask/ao/emission) 목록

### 4단계 — 환경 렌더러 이주 (아트 도착 후)
`ArraySolidField` → `SurfaceTopologyBuilder` → `EnvironmentChunkRenderer` →
`ShadowGeometryBuilder`(벽 윤곽 `ShadowCaster2D`) → 파괴 dirty 갱신 → `SetPieceSpawner`.

**검증**: seam 없음 · cap/front 정합 · 파괴 후 윤곽 재추적.

### 5단계 — 채널 라이팅 (normal/mask 도착 후, "Ori 방향")
- 마스크 채널 규약 확정 — §B-1: 캐릭터 G / 프롭 R. `Renderer2D.asset` 의
  `Multiply with Mask` 채널을 R(1)→G(2) 로 바꿀지 **먼저 결정**해야 그 뒤 authoring 규약이 고정된다
- 크루 실루엣 림 라이트 (`Multiply with Mask (G)`)
- 광맥 발광 (`Additive with Mask (R)`) — 라이트 하나로 화면 전체 광맥 처리
- `LabShadowBlob`(Negative) → 정식 `Freeform` + `Multiply` + `AlphaBlend` 로 승격
- **지층별 고정 광원 방향을 아트 계약에 넣는다** (Ori 의 라이트 디렉션 맵을 우리 규모로 축약한 것)

---

## 4. 규약

- **리플렉션을 늘리지 않는다.** `09` §D. `ShadowCaster2D` 형태 주입은 기존
  `ShadowGeometryBuilder` 의 것만 쓰고, 새 그림자는 freeform/블롭 등 public API 로 한다
- **디스크 프로파일 자산을 런타임에 수정하지 않는다.** 배율은 메모리 복제본에만
- **퍼플 톤은 축이 아니다.** 앰비언트 색 `#9E80C2` 고정. 세기·필터만 움직인다
- **매 단계 끝에 프리셋 8개로 회귀 확인.** 랩이 이미 그 도구다
- 씬은 빌더로 생성한다(`BuildLightingPresetLab`). 손편집은 다음 생성에서 날아간다.
  단, **프리셋 자산 값은 덮지 않는다** — 사용자가 인스펙터에서 조정한 결과를 보존한다

## 4-1. 1단계 구현 기록 (2026-09-09)

| 조항 | 파일 |
|---|---|
| 씬 조립 · 레이어 이주 · 정식 시스템 배선 | `Editor/BuildLightingPresetLab.cs` |
| 프리셋 → 정식 접촉 그림자(`contactShadowOpacity`) · 프로파일 복제본 4시스템 공유 · 투영 고정 · F1 디버그 | `Visual/Lighting/LightingPresetSwitcher.cs` (`EditorAssign` 추가) |
| 앵커 `groundPosition` 구동 이동 | `Visual/Lighting/LightingLabPawn.cs` |
| 접촉 그림자·실루엣 런타임 부착 | `Visual/Lighting/LabAnchoredEntity.cs` (신규) |
| 위협 더미 스프라이트 | `Art/Visual/Placeholder/lab_threat_capsule.png` (빌더가 굽는다) |

**검증(플레이 모드, 수치)** — 투영 `ReferenceTopDown` · 앵커 5개 좌표 유지(항등) · `ContactShadowRenderer.ActiveCount = 5`
· 프리셋 ⑦에서 `contactShadowOpacity 0.50 → 0.60` (정식 경로) · 드릴러를 벽 뒤로 보내면
벽 `alpha = 0.34`(= `foregroundFadeAlpha`) · 실루엣 2개(Interest + Threat) 활성.
캡처: `img/lighting-sweep/lab-step1-preset7.png`, `lab-step1-behind-wall.png`.

**여기서 확정한 판단**
- `ContactShadow` / `OccludedSilhouette` 는 파일명과 다른 클래스라 씬에 직렬화하면 `m_Script` 가
  로컬 fileID 로 박혀 로드 시 missing script 가 된다(실제로 발생). VisualLab 처럼 **런타임 부착**으로
  해결했고(`LabAnchoredEntity`), 프로덕션 파일 분리는 하지 않았다.
- 스위처의 ScriptableObject 참조는 `SerializedObject` 경로에서 null 로 직렬화된 일이 있어 다른
  시스템과 같은 **직접 할당(`EditorAssign`)** 로 통일하고, 빌더가 저장 전에 전부 non-null 인지 검증한다.
- `ShadowGeometryBuilder` 는 `EnvironmentChunkRenderer` 에 묶여 4단계로. `DepthDebugOverlay` 는
  `VisualLabController` 에 묶여 가져오지 않음(`VisualDebugLines` 만, F1).
- **MCP 운용 주의**: 도메인 리로드 뒤 활성 인스턴스가 SlimeForge 로 떨어진다. 변경을 일으키는
  호출 전에 `set_active_instance` 재고정 + 코드 안에서 `Application.dataPath` 에 `TunnelCrew` 가
  들어있는지 확인한다.

## 4-2. 2단계 구현 기록 (2026-09-09)

| 조항 | 파일 |
|---|---|
| `LightSocketRenderer` 배선 · 소켓 4개(Scout/Worklamp/MineralGlow/Indicator) · `VisualOptionsController.Bind(lights, atmo)` | `Editor/BuildLightingPresetLab.cs` (`AddSocket`) |
| 소켓 런타임 부착 래퍼 | `Visual/Lighting/LabLightSocket.cs` (신규) |
| 프리셋 `lightScale` → `LightSocket.baseIntensity` · 예산 검증 손잡이 L/K · 품질 단계 T · UI 소켓/그림자/예산 표시 | `Visual/Lighting/LightingPresetSwitcher.cs` |

**검증(플레이 모드, 수치)** — 소켓 4 · 그림자 2/4(Scout 0.85 · Worklamp 0.60 · 광물광/표시등 0)
· ⑧에서 `baseIntensity` ×1.7 (2.35→4.00, 1.45→2.47) · 작업등 4개 추가 시 그림자 희망 6개 중
**정확히 4개**만 켜지고 우선순위 Scout(1007.6) → 작업등(9.6) → 테스트 1·2(7.5, 등록순 안정) →
3·4는 빛만 · 깜빡임으로 `Light2D.intensity` 가 base 주변에서 흔들림. 캡처: `img/lighting-sweep/lab-step2-budget.png`.

**여기서 확정한 판단**
- `LightSocketRenderer.Apply` 가 매 프레임 `light.intensity = baseIntensity × 깜빡임` 으로 덮어쓴다.
  그래서 프리셋의 광원 축은 `Light2D` 가 아니라 **`LightSocket.baseIntensity`** 를 민다(원값 보관).
- `LightSocket` 도 `LightSocketRenderer.cs` 의 두 번째 클래스 → 씬 직렬화 시 깨진다(실제 4개).
  `LabAnchoredEntity` 와 같은 **런타임 부착**(`LabLightSocket`)으로 해결.
- 표시등 세기를 0.45→0.38 로 내려 `LightClassRules` 의 표시등 조건(≤1.5칸·≤0.4)에 맞췄다.
- 전역광은 소켓 밖(VisualLab 과 동일). 네거티브 블롭도 소켓 밖 — 예산과 무관한 어두운 광원.

## 4-3. 3단계 구현 기록 (2026-09-09)

| 조항 | 파일 |
|---|---|
| 아트 요청서 (벽 타일러블 7장 + 채널맵 후속) | [../codex-art-request-reference-wall-set.md](../codex-art-request-reference-wall-set.md) |
| 키트·머티리얼 셋 빌더 (재실행 가능, 기존 값 보존, 빈 배열만 채움) | `Editor/BuildReferenceEnvironmentKit.cs` — 메뉴 `Tunnel Crew/비주얼 · 레퍼런스 환경 키트 생성 (3단계)` |
| 생성 자산 | `Data/Visual/EnvironmentKit_ReferenceV1.asset` · `SurfaceMaterialSet_Reference_floor/walltop/wallfront.asset` |

**결과** — `floorBase` **3장(a/b/c)** 채움(처음 6장을 넣었다가 d/e/f 의 검정 테두리가 격자선을 만들어
3장으로 정정, 도구가 바닥 배열은 항상 다시 쓴다) · `wallTop`/`wallFront`/`contactAo` **0장 = 의도된 대기** ·
머티리얼 셋 3종은 채널 없음(albedo 전용, `normalStrength 1.6` 구 트랙 승인값과 동일).
`EnvironmentKit.IsEmpty = false` 이지만 환경 렌더러가 방을 그리려면 `wallTop`+`wallFront` 가 필요하다.

**아트 도착 시 절차** — 요청서 §3 파일명(`tr01_reference_wall_top_{a..f}` · `wall_front_{a..f}` ·
`contact_ao_{a}`)으로 `Art/Visual/ReferenceCalibrationV1/` 에 넣고 **같은 메뉴를 다시 실행**하면
빈 배열만 채워진다(자산 파일이 이미 있어 씬 참조가 끊기지 않는다). 그 뒤 4단계.

## 4-4. 4단계 선행 배선 (2026-09-09, 벽 아트 대기 중)

| 조항 | 파일 |
|---|---|
| 방 정의·런타임 Bind·X/C 파괴/복구 | `Visual/Lighting/LabEnvironment.cs` (신규) |
| 랩을 양의 셀 공간으로 이동(원점 오프셋 (8,5)) · 손배치 바닥 격자 제거 · `EnvironmentChunkRenderer` + `ShadowGeometryBuilder` 배선 · 카메라 (8.5, 5) | `Editor/BuildLightingPresetLab.cs` |
| UI: 환경 셀 수·벽 윤곽 캐스터·파괴 편집 수 | `Visual/Lighting/LightingPresetSwitcher.cs` |

**방** — 17×10, 테두리 1칸 `#`, 내부 15×8 `.`(기존 바닥 영역과 동일). 테두리는 카메라 밖이라 벽 아트가
없어도 화면이 깨지지 않고, `ShadowGeometryBuilder` 가 그 윤곽으로 캐스터를 만든다.

**검증(플레이 모드, 수치)** — 필드 17×10 · `GroundBase` 15×8 타일맵 · 벽 타일맵 0(예상) · 캐스터 2(테두리 윤곽)
· 앵커 오프셋 정확(폰 7.25,4.25) · 접촉 그림자 5 · 프리셋 ⑦ 정상 · **X 파괴**: (7,5) 바닥 타일 제거,
캐스터 2→3(윤곽 재추적), 필드 solid=true · **C 복구**: 타일 복귀, 캐스터 2.
캡처: `img/lighting-sweep/lab-step4pre-env.png`, `lab-step4pre-destroy.png`.

**여기서 확정한 판단**
- 렌더러 Bind 는 **런타임(Awake)**. 에디터에서 바인드하면 타일맵이 씬에 직렬화되어 키트가 바뀔 때마다
  씬을 다시 만들어야 한다. 런타임이면 아트 도착 후 키트만 채우고 플레이하면 벽이 나온다.
- 프롭 벽 패널의 `ForegroundOccluder.fadeGroup` 은 1000 — 렌더러가 청크마다 만드는 그룹(0..N)과 겹치지 않게.
- **격자선 진단**: 재질을 `Sprite-Lit-Default` 로 바꿔도 남았다 → 셰이더가 아니라 아트. 바닥 d/e/f 의
  4px 검정 테두리가 원인(§2). 키트를 a/b/c 로 정정.

**아트 도착 후 남는 일** — 키트 메뉴 재실행(벽 슬롯 채움) → 플레이 → cap/front 접합·윤곽 그림자 확인 → 회귀.

## 4-5. 리서치 지렛대 당기기 — URP Bloom · 지층 팔레트 (2026-09-09, 아트 무관)

리서치 결론 ②("어둠+그림자+**블룸**")의 블룸과 ③("Rain World 식 지층 팔레트")가 미착수였다.
본편 `RunBootstrap.BuildVolume` 이 쓰는 `Resources/Volume_Stratum1_Surface … Abyss` 4종
(`BuildVolumeProfiles.cs`: ColorAdjustments·Tonemapping·Bloom·Vignette·FilmGrain)을 **랩이 그대로 소비**한다 —
본편과 같은 배관이라 랩에서 고른 값이 그대로 이식된다.

| 조항 | 파일 |
|---|---|
| 프리셋에 `volumeProfile` · `atmosphereProfile` · `bloomIntensity/Threshold`(음수 = 프로파일 값) | `Visual/Lighting/LightingPreset.cs` |
| 전역 `Volume` + 프로파일 **컴포넌트까지 복제**(디스크 자산 dirty 방지) · Bloom 오버라이드 · 대기 프로파일 팔레트 전환 | `Visual/Lighting/LightingPresetSwitcher.cs` (`ApplyVolume`) |
| 카메라 `UniversalAdditionalCameraData.renderPostProcessing = true` · `Global Volume` · 프리셋 ⑨⑩ | `Editor/BuildLightingPresetLab.cs` |

**프리셋** — ⑨ 블룸 강조 = ⑦ + Bloom 1.6 / thr 0.55(본편 지층1 0.60 / 0.70) · ⑩ 지층2 팔레트 = ⑦ +
`Volume_Stratum2_Fracture`(대비 8 · 채도 2 · 블룸 0.70/0.68 · 필터 (0.98,0.94,1.00)).

**검증(플레이 모드)** — ⑨: Volume 복제본 bloom 1.60/0.55, **디스크 자산은 0.60/0.70 · dirty=false** 유지 ·
후처리 플래그 true · ⑩: 프로파일 `Volume_Stratum2_Fracture (runtime)` 로 교체, 대비/채도/필터 값 확인.
캡처: `img/lighting-sweep/lab-bloom-7-default.png`(⑦, 기본 0.60) · `lab-bloom-9.png` · `lab-palette-10.png`.

**여기서 확정한 판단**
- `Instantiate(VolumeProfile)` 은 하위 `VolumeComponent` 참조를 공유한다 → 값을 쓰면 디스크 자산이 바뀐다.
  프로파일마다 컴포넌트를 `Instantiate` 해 새 프로파일에 담는다(원본별 복제본 캐시).
- 블룸 오버라이드가 없는 프리셋은 **원본 값으로 되돌린다** — 다른 프리셋이 남긴 값이 새지 않게.
- `AtmosphereDirector`(자체 쿼드: 안개·틴트·비네트·그레인)와 URP Volume(비네트·그레인)이 **겹친다.**
  지금은 둘 다 두고 눈으로 판단한다. 확정 후 한쪽으로 정리해야 한다(본편도 같은 중복 후보).

## 5. 진행 기록

| 단계 | 상태 | 날짜 |
|---|---|---|
| 0 | 완료 | 2026-09-09 |
| 1 | **완료** — 아래 "1단계 구현 기록" | 2026-09-09 |
| 2 | **완료** — §4-2 | 2026-09-09 |
| 3 | **완료(아트 대기 상태로)** — 요청서 + 키트·머티리얼 셋 authoring, §4-3 | 2026-09-09 |
| 4 | **선행 배선 완료** — 렌더러·윤곽 그림자·파괴 검증됨, 벽 슬롯만 아트 대기 (§4-4) | 2026-09-09 |
| 5 | 아트 대기 | |
