# Codex 아트 요청서 3차 — 개정 R2 · 굴착 경계와 그림자

작성: 2026-09-10 · 요청자: Claude 구현 트랙 · 대상: Codex 아트 트랙
근거: [core-keeper-look-direction-analysis.md](core-keeper-look-direction-analysis.md) ·
기획서 [unity-visual-overhaul-functional-spec.md](unity-visual-overhaul-functional-spec.md) §0.2 · §8.6 · §19.0
계약: [../art-production/test-room-v01/process/production-spec.md](../art-production/test-room-v01/process/production-spec.md)
이전 요청서: [1차](codex-art-request-reference-wall-set.md) · [2차](codex-art-request-wall-volume.md) (2차의 측면·코너는 R2 로 소비 중단)

상태: **필수 납품 완료 (2026-09-10)** — 9장, manifest revision 20 및 Unity 임포트 검증 완료.

## 0. 무엇이 바뀌었나 — 이 요청서가 2차와 다른 이유

환경 목표가 **코어키퍼 배경 룩**으로 확정됐다. 이제 방은 "바닥 위의 벽 섬"이 아니라 **"고체 암반을
파낸 통로"**고, 벽 너머는 타일 LOS 로 완전 암흑이다(기획서 §0.2). 이 구조에서 화면에 남는 벽면은
**굴착 경계 한 줄**뿐이다. 그래서 아트의 우선순위가 뒤집힌다:

- 미탐색 고체용 벽 상단 변형 → **불필요**(검정으로 덮인다)
- 서·동 측면, 코너 4방위 → **불필요**(코어키퍼 실측: 해당 레이어 자체가 없다, 기획서 §8.6.1)
- **굴착 경계 한 줄의 완성도** → 룩의 전부

그리고 채굴이 들어왔다(마우스로 벽을 판다). **파는 순간의 피드백**(균열·광맥)이 아트 항목이 된다.

## 1. 지금 화면에서 확인된 문제 (요청 근거)

| 증상 | 원인 | 아트로 해결되는 부분 |
|---|---|---|
| **벽 그림자가 딱딱하다** | 상시 드롭섀도는 절차 생성 **단색 셀 사각형**(`SolidCellSprite`)을 오프셋해 깐 것. 가장자리가 0px 페이드 | ② 부드러운 드롭섀도 타일셋 |
| 벽 상단 림이 한 장 | `wall_top_rim_a` 1장이 북쪽 열린 116~120칸 전부에 반복 | ① 림 변형 b/c |
| 채굴 피드백 없음 | 벽이 한 번에 사라진다. 코어키퍼는 `wallCrackFront` 로 정면에 균열이 쌓인다 | ③ 균열 3단계 |

동적 그림자의 딱딱함·캐릭터를 덮는 문제는 **구현 트랙이 처리한다**(§5). 아트 대기 아님.

## 2. 필수 납품 — 3개 항목 · 실제 파일 9장

규격은 1·2차와 동일: **128 PPU · 정수 픽셀 · 안전 패딩 8px · 파일명 `tr01_reference_<name>_<variant>_<channel>.png`**
광원 방향 **지층1 좌상단 35°** 고정(계약 §5-1).

### ① 벽 상단 림 변형 — 2장

| 파일 | 캔버스 | 피벗 | 역할 |
|---|---|---|---|
| `tr01_reference_wall_top_rim_b_albedo.png` | 128×128 | 중앙 | `_a` 와 같은 규칙(북·서 밝고 남·동 어둡다), 균열·이끼 위치만 다르게 |
| `tr01_reference_wall_top_rim_c_albedo.png` | 128×128 | 중앙 | 같음. 한 장은 살짝 깨진 가장자리 |

**왜** — R2 에서 북쪽 열린 cap 은 `CapFor()` 가 `wallTopRim` 배열에서 `TopModule` 로 고른다. 배열이 3장이면
`wall_top` a/b/c 와 같은 구역 해시로 흩어져 반복이 깨진다. **코드 변경 없이 파일만 넣으면 된다.**

### ② 부드러운 벽 드롭섀도 타일셋 — 4장 (9-slice 대체)

| 파일 | 캔버스 | 피벗 | 역할 |
|---|---|---|---|
| `tr01_reference_wall_shadow_center.png` | 128×128 RGBA | 중앙 | 벽 덩어리 안쪽. **알파 단색 (0,0,0, 215)** |
| `tr01_reference_wall_shadow_edge.png` | 128×128 RGBA | 중앙 | 남쪽 가장자리. 위쪽 절반 215 → 아래쪽 0 으로 **부드러운 페이드** (약 40px) |
| `tr01_reference_wall_shadow_corner_outer.png` | 128×128 RGBA | 중앙 | 볼록 모서리(남동). 두 방향 페이드 |
| `tr01_reference_wall_shadow_corner_inner.png` | 128×128 RGBA | 중앙 | 오목 모서리 |

- **색은 없다.** 순수 알파 마스크. 색은 코드가 `LabWallDropShadow._color`(퍼플 검정)로 곱한다.
- **방향은 남쪽·동쪽 가장자리만** — 광원이 좌상단이라 그림자는 우하단으로 떨어진다(오프셋 0.50, −0.46 셀).
  북·서 가장자리는 벽 자신이 덮으므로 페이드가 필요 없다. 회전·반전으로 4방위를 만들지 **않는다**.
- 코어키퍼는 `wallTopShadowCaster` 를 1셀 지오메트리로 압출하고 광원별로 부드럽게 계산한다.
  우리는 광원과 무관한 상시 그림자를 타일로 깔고 있으므로 **페이드가 아트에 들어가야 한다.**

### ③ 벽 정면 균열 3단계 — 3장 (채굴 피드백)

| 파일 | 캔버스 | 피벗 | 역할 |
|---|---|---|---|
| `tr01_reference_wall_crack_1_albedo.png` | 128×128 RGBA | 하단 중앙 | 첫 타격. 가는 균열 2~3줄 |
| `tr01_reference_wall_crack_2_albedo.png` | 128×128 RGBA | 하단 중앙 | 균열이 번지고 조각이 살짝 벌어짐 |
| `tr01_reference_wall_crack_3_albedo.png` | 128×128 RGBA | 하단 중앙 | 부서지기 직전. 파편·어두운 틈 |

- **정면(`wallFront`) 위에 얹는 오버레이**다. 정면 a/b/c 어느 위에 올려도 맞아야 하므로 배경은 투명,
  균열 자체만 그린다. 코어키퍼 `wallCrackFront` 와 같은 자리다(기획서 §8.6.1 ⑦ — 정보는 정면에 얹는다).
- 상단면(cap)에는 균열을 그리지 않는다. 위에서 보이는 균열은 굴착 경계에서 읽히지 않는다.

## 3. 선택 — 여유가 있으면

| 파일 | 역할 |
|---|---|
| `tr01_reference_ore_front_{a,b}_albedo.png` + `_emission` | 정면에 얹는 광맥. 채굴 대상 표시. 코어키퍼 `oreFront` |
| `tr01_reference_torch_a_albedo.png` + `_emission` | 설치형 횃불 소품(96×128, 하단 중앙). 코어키퍼 루프의 "횃불 박기"용 |
| `tr01_reference_floor_edge_a_albedo.png` | 벽에 인접한 바닥 경계 블렌드(`floorEdge` 슬롯, 2차에서 이월) |

## 4. 소비 중단 — 만들지 말 것

| 항목 | 이유 |
|---|---|
| 벽 상단(cap) 변형 d/e/f | 미탐색 고체는 검정. 화면에 남는 cap 은 경계 한 줄이고 그건 림이 맡는다 |
| 서·동 측면 b, 코너 4방위 | 코어키퍼에 레이어 없음. 2차 납품물은 보존만 |
| 미탐색 구역용 어떤 아트든 | 완전 암흑 |

## 5. 구현 트랙이 병행하는 것 (아트 대기 아님)

- **동적 그림자가 캐릭터를 덮는 문제** — `ShadowCaster2D` 의 대상 소팅 레이어를 지면 레이어로 제한
  (WorldEntity 제외). 벽 그림자는 바닥·벽 정면에만 떨어진다.
- **동적 그림자 딱딱함** — 광원 분류별 `shadowSoftness` 상향, `ShadowIntensity` 하향.
- **드롭섀도 임시 완화** — ② 도착 전까지 opacity 0.85 → 0.55.
- ② 도착 시 `LabWallDropShadow` 가 이웃 마스크로 center/edge/corner 를 고르는 경로 신설.
- ③ 도착 시 `WorldGrid.DamageStage`(본편에 이미 있음, 0~3) → 정면 오버레이 타일맵 연결.

## 6. 검수 항목

1. 림 a/b/c 를 6×6 반복 검사 — 이음새 0, 광원 방향 일관
2. 드롭섀도 edge 의 페이드가 **한 방향**(남쪽)만이고 center 와 알파가 이어진다
3. 균열 1→2→3 이 정면 a/b/c 어느 위에서도 읽힌다(투명 배경 확인)
4. 모든 파일 128 PPU · 8px 패딩 · 파일명 규약
5. 레퍼런스 보드 옆에 놓고 팔레트 대조
