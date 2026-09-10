# 레퍼런스 랩 이주 계획 — VisualLab 스택 → 레퍼런스 아트 기반

작성: 2026-09-09 · 대상 씬: `Assets/_Project/Scenes/LightingPresetLab.unity`
연계: [../urp-2d-lighting/09-tunnel-crew-application.md](../urp-2d-lighting/09-tunnel-crew-application.md) · [visual-overhaul-implementation.md](visual-overhaul-implementation.md) · [../unity-visual-overhaul-functional-spec.md](../unity-visual-overhaul-functional-spec.md)

## 0-0. 개정 R2 (2026-09-10) — 목표를 코어키퍼 룩으로 전환

**이 절이 문서 전체에서 최우선한다.** 아래와 충돌하는 기존 단계·결정은 모두 이 절로 대체된다.
근거: [../core-keeper-look-direction-analysis.md](../core-keeper-look-direction-analysis.md) ·
개정 기획서: [../unity-visual-overhaul-functional-spec.md](../unity-visual-overhaul-functional-spec.md) §0.2 · §5.4 · §7.6 · §8.6

1~5단계로 VisualLab 스택 이주는 끝났으나 **공간감이 생기지 않았다.** 원인은 이주 대상 목록에
없던 것 하나다 — **벽 너머가 보인다.** 환경 목표를 코어키퍼 배경 룩으로 확정하고,
아래 6단계를 **다른 모든 잔여 작업보다 앞에 둔다.**

### 6단계 — 어둠 골격 (최우선, 신설)

| # | 작업 | 아트 의존 |
|---|---|---|
| 1 | 랩 방을 **고체 채움 + 굴착 통로** 구조로 재작성. 34×20 열린 평면 폐기. 연결성은 콜라이더 flood fill 로 검증(고립 0) | 없음 |
| 2 | Global 환경광 `0.35 → 0.03`. `Negative Freeform` 광원 5개 **제거** | 없음 |
| 3 | **타일 LOS 계산기 이식** — 원본 HTML `v7.9.2` 의 `LOS`(L5164~)를 C# 순수 함수로. EditMode 검증 | 없음 |
| 4 | **라이트마스크 합성**. 퍼플을 `WorldVisualProfile.ambientColor` → 마스크 `dark: #44248F` 로 이전 | 없음 |
| 5 | **`explored` 잔상**(농도 0.29 · 페이드 11셀) + 시간 이징 | 없음 |
| 6 | 굴착 → `MarkDirty()` LOS 갱신 연동 | 없음 |
| 7 | 프리셋 랩 A/B 축 교체 — `dark`·`litClear`·드러남/밝음 비율 | 없음 |

**6단계 전체가 아트 의존 없음이다.** 아트를 기다릴 필요가 없다.

#### 6단계 구현 기록 (2026-09-10, 1~6 완료 · 7 부분)

| # | 결과 |
|---|---|
| 1 | 방 재작성 완료. 223/680 = 33% 바닥(예전 64%). 콜라이더 flood fill: 자유 223 · 도달 223 · 고립 0. 스폰 (16,9) 중앙 챔버 |
| 2 | 환경광 **0.14**(아래 정정 참고). `Negative Freeform` 5개 제거 — 씬에서 `NegativeLightVolume` 0개 확인 |
| 3 | **새로 짜지 않았다.** 본편에 이미 `Sim/Vision/LosService.cs`(원본 LOS 이식본)가 있어 격자 접근을 델리게이트로 일반화해(`LosService(cols, rows, isSolid, version)`) 랩의 `ArraySolidField` 에 물렸다. 본편 `WorldGrid` 생성자는 그대로 |
| 4 | **새로 짜지 않았다.** 본편 `Presentation/Lighting/DarknessOverlay.cs` + `TunnelCrew/Darkness` 셰이더를 그대로 쓴다. 랩은 12종 소팅 레이어라 `SetSorting("VisionAndGrade", 0)` 만 추가(`Default` 에 두면 타일 밑에 깔린다) |
| 5 | `DarknessOverlay` 가 이미 R/G(시야/기억) + rise 0.16s / fall 0.38s 이징을 갖고 있다. `SimTuning.Los*` = 19 · 360 · 11 · 0.29 · 바닥 0.30 승계 |
| 6 | `LabEnvironment.SetCell` 이 표면·그림자·드롭섀도·콜라이더·LOS 를 같은 프레임에 갱신. 채굴은 **마우스 왼쪽**(손 닿는 1.6셀), 메우기 오른쪽. 검증: (21,9) 채굴 → 콜라이더 457→456, 가시 셀 90→91, edits 1 |
| 7 | 프리셋 **⑬ 코어키퍼 기준 (R2)** 추가(시작 프리셋). **O** 키로 LOS 켜고 끄며 R1↔R2 비교. `LightingPreset` 에 어둠 축 신설: `losDarkColor` · `losMemoryColor` · `losMaxDarkness` · `losEdgeSoftness` → 스위처 `ApplyLosDarkness` 가 `DarknessOverlay` 에 민다. 기존 자산은 기본값(=⑬ 값)을 받는다 |

#### 6단계 후속 (2026-09-10 오후) — 경계 날카로움 · 조명 소팅 · 비네트

| 항목 | 조치 | 실측 |
|---|---|---|
| **어둠 경계 뭉개짐** | `DarknessOverlay.Bind(..., supersample: 4)` — 셀 값을 4×4 텍셀로 복제해 바이리니어 번짐 폭을 한 칸 → 1/4칸으로. 본편은 기본값 1 그대로 | LOS 텍스처 34×20 → **136×80** |
| **조명 소팅 정밀화** | `LightSocket.mountHeightCells` 신설. `≥ SurfaceRules.MinLiftCells(0.75)` 이면 `Lit`(윗면 포함), 아니면 `LitGroundLevel`. 기둥 램프 1.0 · 바닥 결정 0.35 · 표시등 0 | 3단 분리 확인 — **윗면까지**: Worklamp×3 + Global / **지면만**: MineralGlow×5 · Indicator×3 · Flashlight · Halo |
| **비네트 이중 적용** | URP Volume 에 활성 `Vignette` 가 있으면 스위처가 대기 프로파일 복제본의 `vignetteStrength` 를 0 으로 | atmo vignette **0.00**, URP vignette active |

미결 → 결정: 라이트마스크 합성 위치는 **카메라 추적 오버레이 쿼드**(본편 `DarknessOverlay` 방식). Renderer Feature 는 쓰지 않는다.

#### 6단계 후속 2 (2026-09-10) — 벽 그림자 소팅·부드러움 ("그림자가 딱딱하고 캐릭터를 덮는다")

| 항목 | 조치 | 실측 |
|---|---|---|
| **벽 그림자가 캐릭터를 덮음** | `ShadowCaster2D.m_ApplyToSortingLayers`(공개 API 없음 → 리플렉션)를 `VisualLayers.ShadowReceivers` = Default·GroundBase·GroundDetail·GroundDecal·BackStructure 로. **WorldEntity·WallTop·FrontStructure 제외** | 벽 캐스터 10개 전부 5레이어 수신. 남은 1개(ALL)는 캐릭터 자신의 발밑 캐스터(본편 PlayerView) |
| **동적 그림자 딱딱함** | `LightClassRules.ShadowIntensity` 0.85/0.6 → **0.70/0.45**, `ShadowSoftness` 신설 Scout 0.55 · Worklamp 0.75. `LightSocketRenderer` 가 매 프레임 적용, 랩 손전등도 같은 규칙 | Flashlight 0.70/0.55 · Worklamp×3 0.45/0.75 |
| **드롭섀도 딱딱함** | 임시 완화 opacity 0.85 → **0.55**. 근본 해결은 3차 아트 요청 ②(페이드 있는 타일셋) | Shadow Map alpha 0.55, GroundDecal:-50 |

3차 아트 요청서: [../codex-art-request-r2-darkness-boundary.md](../codex-art-request-r2-darkness-boundary.md) — 림 b/c · 부드러운 드롭섀도 4장 · 정면 균열 3단계.

#### 6단계 후속 3 (2026-09-10) — 벽 윗면이 통째로 보인다 ("폐쇄감이 덜하다")

**원인은 아트도 림도 아니다.** 전역광 하나가 `Lit` 전체를 같은 세기로 칠해서, LOS 가 "보인다"고 표시한
벽 셀의 윗면이 바닥과 같은 밝기로 드러났다. 타일 LOS 는 벽 타일까지 표시하고 차단하므로 방에 붙은 벽 한 줄은
항상 가시다 — 그 한 줄의 윗면이 바닥과 같은 밝기면 "벽"이 아니라 "밝은 띠"로 읽힌다. 코어키퍼에서 윗면이
어두운 이유는 빛이 옆(방)에서 오기 때문이다 — 정면은 받고 윗면은 거의 못 받는다.

| 조치 | 실측 |
|---|---|
| 전역광을 둘로 분리. `Global (ambient)` → `LitGroundLevel`, 신설 `Global (wall tops)` → `LitElevated`(WallTop·FrontStructure) | ambient 0.140 / wall tops **0.035** |
| `LightingPreset.wallTopAmbientScale` 신설(기본 0.25). 스위처가 두 전역광을 함께 민다 | ⑬ 기준 0.14 × 0.25 |
| 윗면을 밝히는 나머지 경로는 설치 높이 ≥0.75 인 램프만(후속 1) | 램프 옆 윗면만 살아남 |

두 전역광의 대상 레이어가 겹치지 않아 URP 의 "같은 레이어에 전역광 둘" 경고는 없다.

**그런데 진범은 전역광이 아니었다.** 픽셀 실측으로 추적한 결과, "통째로 보이는 벽 윗면"은 벽 윗면이 아니라
`BackStructure` 의 **벽 정면 타일**이었고, 앰비언트 0 · 이미션 0 · Sprite-Lit 로 바꿔도 (240,204,241) 로 남았다.
원인은 **수정광(`MineralGlow`, 블렌드 슬롯 3 "Additive with Mask")** — `WorldLit` 의 `_MaskTex` 기본값이 `"white"` 라서
마스크 맵을 납품받지 못한 벽 정면 재질은 마스크 채널이 전부 1 이고, 반경 3.2 셀 안의 정면이 통째로 가산됐다.
첫 R2 캡처의 좌상단 밝은 직사각형이 그것이다(수정 소품 옆).

| 조치 | 실측 |
|---|---|
| `SurfaceMaterialSet.Apply` — `materialMask` 가 없으면 `_MaskTex = Texture2D.blackTexture` 명시. "마스크 없음 = 마스크 광원에 반응하지 않음" | 같은 픽셀 (246,213,240) → **(37,26,42)** |
| 서·동 측면 소비 중단(`EnvironmentChunkRenderer`, 기획서 §8.6.2) | BackStructure 에 side 스프라이트 0 |

#### 3차 아트(manifest r20) 투입 (2026-09-10)

| 납품 | 소비 경로 | 실측 |
|---|---|---|
| 림 b/c | 기존 `CapFor()` — 파일만 넣으면 됨 | FrontStructure 에 rim a 9 · b 19 · c 19 |
| **드롭섀도 타일셋 4장** | `EnvironmentKit.wallShadow`(인덱스 계약 center/edge/outer/inner) + `LabWallDropShadow.EnsureRoleTiles` — 남쪽 열림 edge · 동쪽 열림 edge 90° 회전 · 둘 다 corner_outer · 남동 대각만 corner_inner · 그 밖 center. 4장이 다 없으면 단색 셀로 복귀 | center 120 · edge 155 · outer 16 · inner 9 · opacity 0.75 |
| 균열 3단계 | **아직 미소비** — 랩 채굴이 1클릭 즉시 파괴라 단계가 없다. 다음: 3타 채굴 + 정면 오버레이 | — |

#### 6단계 후속 4 (2026-09-10) — 램프 옆 벽 기둥 윗면 ("이 부분도 어두워야")

동쪽 벽 기둥(셀 21, 8~10) 윗면이 밝았다. 원인은 기둥 램프의 설치 높이 1.0 ≥ lift 0.75 → `Lit`(윗면 포함)을 비춤.
→ **램프 설치 높이 0.5** 로 내려 지면 광원으로. 이제 점광원 중 윗면을 비추는 것은 없고, 윗면은 윗면 전역광 0.035 만 받는다.
실측: (21,8) (18,13,28) · (21,9) (46,36,63) · (21,10) (13,6,19).
코어키퍼도 횃불 옆 윗면을 조금 밝히지만, 우리 목표(폐쇄감)에서는 윗면을 전역광 축 하나로만 다루는 쪽이 맞다 —
윗면 밝기를 올리고 싶으면 `wallTopAmbientScale` 을 올린다(프리셋 축).

#### 본선(Run) 조명 설정 대조 — "본선 필드 조명이 랩에 다 들어와 있나?" (2026-09-10)

**아니다. 일부만 같고, 다른 것은 의도된 진화이거나 드리프트다.** 근거 `RunBootstrap.BuildLighting` / `BuildWorld` / `RebuildLamps`.

| 항목 | 본선 (RunBootstrap) | 랩 (⑬ 기준) | 판정 |
|---|---|---|---|
| 전역광 세기 | `_ambientIntensity` **0.16** | 바닥 **0.14** · 윗면 0.035 | 유사. 랩은 윗면 분리(R2) |
| 전역광 색 | (0.62, **0.58**, 0.80) | (0.62, **0.50**, 0.76) | **드리프트** — 랩이 더 퍼플. 어느 쪽이 정답인지 결정 필요 |
| 손전등 | 40/56° · r 9.36 · I 2.6 · (1,.94,.80) · 그림자 0.9/0.35 | 같음 · 그림자 **0.70/0.55** | 그림자만 의도적으로 부드럽게(후속 2) |
| 후광 | 360° · r 2.6 · I 1.4 · 그림자 0 | 같음 | 동일 |
| **램프** | 360° · **r 5.2 · I 1.1 · 주황 (1,.69,.28)** · 그림자 기본 | Worklamp **r 2.4 · I 2.35 · 마젠타 (0.92,0.16,1)** · 소켓 파이프라인 | **다르다.** 본선은 넓고 약한 주황(§7.3 "따뜻한 작업광"), 랩은 승인 아트의 마젠타 유리 |
| 크루 손전등 | 멤버 수만큼 r 8.4 · I 2.0 | 없음 | 랩 미포함 — 코옵 룩에 영향 큼 |
| 보스·플레어 광 | 있음 | 없음 | 무관 |
| 어둠 오버레이 | `DarknessOverlay` 기본색 (0.03,0.025,0.06)/(0.10,0.09,0.16) · 셀 해상도 | 퍼플 (0.02,0.01,0.045)/(0.16,0.09,0.30) · **4× 초해상** | 랩이 진화형. 본선으로 역이식 대상 |
| 벽 그림자 | `WallShadowBuilder`(청크 캐스터) | `ShadowGeometryBuilder`(윤곽) + 수신 레이어 제한 | 다른 시스템. 랩이 진화형 |
| 타일 재질 | `Tunnel Crew/Tilemap-Lit-Normal` + 벽 노멀 아틀라스 | `Tunnel Crew/WorldLit` (SurfaceMaterialSet, 채널 4종) | 다른 셰이더. 랩이 진화형 |
| Volume | `Resources/Volume_Stratum1_Surface` | 같은 자산 + 프리셋 Bloom 오버라이드 | 동일 자산 |
| 카메라 | ortho · bg (0.04,0.03,0.07) · post on · AA none | ortho 4 · bg (0.018,0.008,0.028) · post on | 배경색 소폭 다름 |
| 노멀맵 | `UseNormalMaps` Accurate + height | 같은 함수 | 동일 |
| 소팅 레이어 | `Default` 단일(Floor 0 / Walls 10 / CoreTop 20) | 12종 | 다름 — 본선 이식 시 최대 작업 |
| 대기 패스 | 없음 | `AtmosphereDirector` | 랩만 |

#### 6단계 후속 5 (2026-09-10) — 크루 손전등 · 3타 채굴 + 균열

| 항목 | 조치 | 실측 |
|---|---|---|
| **크루 손전등** | `LabCrewLights` 신설 — 서 있는 동료 2명(챔버 (15,8)·(19,10)), 본선 수치(40/56° · r 8.4 · I 2.0 · 지면 광원 · Scout 그림자). `VisionSource.Crew` 로 LOS 에 합산 | 2개 생성 · I 2.6(프리셋 lightScale 1.3 반영) · wallTops=False · 가시 셀 90→92 |
| **3타 채굴 + 균열** | `LabEnvironment` 타격 카운트(`_hitsToBreak` 3). 1·2타에 `EnvironmentKit.wallCrack[stage-1]` 을 "Crack Map"(정면 레이어 +1, 정면 재질 공유)에 얹고 3타에 파괴. 메우기·X/C 는 기록 무효화. 키트 슬롯 `wallCrack` + 빌더 `WallCrackByStage` | 키트 wallCrack=3 · stage2 타일 1 · 스프라이트 `wall_crack_2` |

**결론.** 랩은 본선의 복제가 아니라 본선의 <b>다음 버전</b>이다(계획 §0: 본편 이식은 오버홀 완료 후). 그래도 본선에서
가져와야 할 두 가지가 빠져 있다 — ① **크루 손전등**(코옵에서 화면 빛의 절반) ② **램프의 본선 정의**(넓고 약한 주황)
와 랩 마젠타 램프의 관계 정리. 그리고 전역광 색 드리프트는 결정 사항이다.

**정정 — 환경광 0.03 은 틀렸다.** 계획서와 기획서 §7.2 에 "0.35 → 0.03"으로 적었는데, `DarknessOverlay` 는
알파 블렌드 오버레이라 **드러난 영역의 밝기는 환경광이 결정**한다. 0.03 이면 LOS 가 드러낸 19칸 반경이
그냥 검게 보여 "드러남 대 밝음" 비율(§7.6.4)이 사라진다. 원본 HTML 도 같은 구조였다 — 씬을 밝게
렌더한 뒤 마스크로 곱했다. 그래서 **0.14** 로 잡았다: 드러났지만 조명이 닿지 않는 영역 = 어두운 퍼플,
손전등·후광 ~2칸 = 밝음. 퍼플의 두 번째 거처는 `DarknessOverlay` 의 `memoryColor`(0.16, 0.09, 0.30).
기획서 §7.2·§14 B0 의 "0.03"은 이 정정으로 읽을 것.

플레이 실측(⑬ 프리셋, 스폰 직후): 가시 셀 90(벽면 포함) · 탐색 90 · 어둠 쿼드 `VisionAndGrade:0`
`TunnelCrew/Darkness` · 전역광 0.14 · 벽 상단 188 · 정면(BackStructure) 220 · 코너 53 · 림 cap 112.

### 개정으로 폐기·축소되는 기존 항목

| 항목 | 상태 |
|---|---|
| `Negative Freeform` 광원 5개 | **제거.** LOS 전파가 정확히 대신한다 |
| 34×20 열린 평면 랩 방 | **폐기.** 굴착 구조로 재작성 |
| 벽 서/동 측면(`westSide`/`eastSide`) 스프라이트 | **소비 중단.** 코어키퍼에 해당 레이어 없음(기획서 §8.6.1) |
| 코너 4방위 아트 발주 | **취소.** 상단면 오토타일의 경계·내부코너로 해결 |
| 미탐색 고체용 벽 상단 변형 추가 발주 | **중단.** 검정으로 덮인다 |
| `AtmosphereDirector` 포그·깊이분리·비네트 이중 적용 | **정리 대상** |
| 5단계 "Ori 방향" 채널 라이팅 | **격하.** 드러난 영역 안에서만 유효. 6단계 후로 순서 이동 |
| 프리셋 랩의 앰비언트 무드 축 | **거의 고정값화** |

### 값이 올라가는 기존 항목

- `SurfaceMask.Buried` 컬링 — 미탐색 고체가 통째로 평면 검정이 되므로 이득이 급증
- 경계면 3종(`FrontFace` · `TopRim` · `ContactAo`) — **보이는 벽면이 굴착 경계에만 남으므로
  이 3종이 룩 전체를 짊어진다.** 특히 `TopRim` 이 1순위 발주(현재 절차적 림은 사용 불가 상태)
- URP Bloom · 톤매핑 — 검정 배경 위 밝은 광원이 룩의 전부
- 코옵 시야 합산 — 원본에 이미 구현. 동료가 비춘 곳을 팀 전체가 본다

---

## 0. 무엇을 하는가

`VisualLab`(승인 아트 `TestRoomV01` 기반)이 들고 있는 비주얼 스택을 **레퍼런스 직결 아트
(`ReferenceCalibrationV1`) 기반으로 옮긴다.** 본편(`Run.unity`) 이식은 아트 오버홀 완료까지 보류.

> 개정 R2: 이주 자체는 1~5단계로 완료됐다. 잔여 목표는 §0-0 의 6단계다.

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
| 4 | **완료** — 벽 아트 7장 도착 → 키트 채움 → cap/front 접합·윤곽 그림자·전경 오클루전 검증 (구현 기록 §24) | 2026-09-09 |
| 5 | **부분 완료** - 마스크 채널 확정 - 광맥 발광(Additive with Mask) - 네거티브 freeform 승격 완료. 크루 림 라이트는 캐릭터 마스크 대기 | 2026-09-09 |

### 5단계 진행 기록 (2026-09-09)

구현 기록 [visual-overhaul-implementation.md](visual-overhaul-implementation.md) §23 에 상세.

| 항목 | 상태 |
|---|---|
| 마스크 blend style 채널 확정 (Multiply->G - Additive->B) | **완료** - [mask-channel-convention.md](mask-channel-convention.md) + 테스트 6종 |
| 마스크 채널 규약 문서화 - 아트 계약 반영 | **완료** |
| 광맥 발광 `Additive with Mask` | **완료** - 임시 마스크(`BakePlaceholderMasks`)로 실증 |
| 네거티브 -> 정식 `Freeform` + `Multiply` + `AlphaBlend` | **완료** - `NegativeLightVolume`, 리플렉션 없음 |
| 크루 실루엣 림 라이트 (`Multiply with Mask` G) | **아트 대기** - 캐릭터 전용 마스크 없음 |
| 지층별 고정 광원 방향을 아트 계약에 | **완료** - `production-spec.md` §5-1 |
| 지층별 제한 팔레트 (대기 프로파일 4종) | **완료** - `BuildAtmosphereProfiles`, 프리셋 ⑩⑪⑫ |
| 무드 축(깊이/위기) + 에디터 스크럽 | **완료** - `StratumMoodDirector` |
