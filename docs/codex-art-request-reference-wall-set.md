# Codex 아트 요청서 — 레퍼런스 트랙 벽 타일러블 세트 (+ 채널맵 후속)

작성: 2026-09-09 · 요청자: Claude 구현 트랙 · 대상: Codex 아트 트랙
근거: [unity-port/reference-lab-migration-plan.md](unity-port/reference-lab-migration-plan.md) §2·§3(3단계)
계약: [../art-production/test-room-v01/process/production-spec.md](../art-production/test-room-v01/process/production-spec.md) ·
[../art-production/test-room-v01/process/reference-calibration-v1.md](../art-production/test-room-v01/process/reference-calibration-v1.md)

## 0. 한 줄 요약

> **레퍼런스 직결 스타일의 벽 타일러블 7장(cap 3 · front 3 · 접합 AO 1)이 없어서 환경 렌더러(4단계)가
> 멈춰 있다.** 이 7장이 오면 벽 격자·윤곽 그림자·파괴가 전부 열린다. 채널맵(normal/mask/ao/emission)은
> 그 다음(5단계) 요청이며 이 문서 §5 에 미리 적어 둔다.

## 1. 왜 필요한가 (구현 쪽 사정)

- `EnvironmentChunkRenderer` 는 `EnvironmentKit` 의 **`floorBase[]` · `wallTop[]`(cap) · `wallFront[]`(정면)** 세
  배열로 방을 그린다. 바닥은 레퍼런스 `floor a~f` 6장으로 채워지지만 **벽 두 배열이 비어 있다.**
- 레퍼런스 트랙의 벽은 `tr01_reference_wall_a` **384×384 세트피스 패널 한 장**(파이프 달린 벽)이라
  1셀 단위로 반복되는 cap/front 로 쓸 수 없다.
- 구 트랙(`TestRoomV01`)에는 `wall_top a~f` · `wall_front a~f` · `wall_top_rim a` · `contact_ao a` 가 전부 있으나,
  **레퍼런스 보드 스타일과 팔레트가 다르다.** 레퍼런스 트랙은 "보드에서 직접 추출한 픽셀만 쓴다"가 원칙이므로
  섞을 수 없다.

## 2. 스타일 기준 (반드시)

- 최상위 시각 기준: `art-production/test-room-v01/concept/tr01_reference_asset_calibration_board_v1.png`
- 팔레트·재질은 **`tr01_reference_floor_a~c` 와 `tr01_reference_wall_a`(패널) 에서 읽히는 것 그대로.**
  퍼플 주조색(앰비언트 `#9E80C2` 에서 읽히는 톤)을 유지한다. 별도 틴트·회전·반전으로 변형을 만들지 않는다
  (`reference-calibration-v1.md` "보드 승인 전에는 추가 변형·틴트·회전·반전 없음").
- **광원 방향 규약(신설)** — 이 세트부터 아트에 굽는 약한 자체 음영은 **화면 좌상단 35°** 에서 오는 빛으로
  통일한다. 이유: Ori 가 에셋마다 라이트 디렉션 맵을 손으로 칠해 룩을 만들었고, 우리는 그것을 "지층당
  고정 방향 1개"로 축약한다(리서치 결론). 방향이 자산마다 다르면 런타임 광원을 얹어도 요철이 서로 싸운다.
  Albedo 규칙(§5 계약: "방향광·강한 투사광을 굽지 않고 약한 자체 음영만")은 그대로 — **방향만 통일**한다.

## 3. 필수 납품 — 벽 타일러블 7장

캔버스는 전부 **128 PPU, 정수 픽셀, 안전 패딩 8px** (계약 §3). 파일명 규약 `tr01_<category>_<name>_<variant>_<channel>.png` (계약 §4).

| # | 파일 | 캔버스 | 피벗 | 반복 경계 | 역할 |
|---|---|---|---|---|---|
| 1 | `tr01_reference_wall_top_a_albedo.png` | 128×128 | 중앙 (64,64) | **4면** — 좌우·상하 4px 짝 | 벽 셀 윗면(cap). 조용한 기본 |
| 2 | `tr01_reference_wall_top_b_albedo.png` | 128×128 | 중앙 | 4면 | cap 변형 — 균열 |
| 3 | `tr01_reference_wall_top_c_albedo.png` | 128×128 | 중앙 | 4면 | cap 변형 — 광맥/보강 |
| 4 | `tr01_reference_wall_front_a_albedo.png` | **128×128** | **하단 중앙 (64,~8)** | 좌우 4px 짝 | 남쪽이 열린 셀의 정면. 조용한 석재 |
| 5 | `tr01_reference_wall_front_b_albedo.png` | 128×128 | 하단 중앙 | 좌우 | 정면 변형 — 대각 균열 |
| 6 | `tr01_reference_wall_front_c_albedo.png` | 128×128 | 하단 중앙 | 좌우 | 정면 변형 — 습윤/보수판 |
| 7 | `tr01_reference_contact_ao_a.png` | 128×128 RGBA | 중앙 | — | 바닥–벽 접합 AO. **북쪽 접점 알파 150, 54px 페이드**(승인 계약값) |

### 정면(front) 구조 — 구 트랙 승인 구조를 그대로 따른다
"**공통 상단 림 · 중앙 보강 · 하단 접촉 밴드**" 3단 구조. 높이는 프로파일 `wallLiftCells = 1.0`(현재값) 에
맞춰 **128px**. 계약이 허용하는 lift 범위는 0.75~1.5셀(96~192px)이지만, **한 지층 안에서 높이는 하나**여야
한다 — 셀마다 높이가 다르면 연결된 벽의 cap 이 어긋나 이음새가 생긴다(구현 기록 §2.2).

### cap 과 front 의 접합
cap 은 lift 만큼 화면 위로 올라가고, front 는 셀의 남쪽 경계선에 발점을 두고 lift 높이만큼 솟는다.
**두 면이 정확히 맞닿는다.** 따라서 front 의 **최상단 4px 은 cap 의 최하단 4px 과 색·명도가 이어져야** 한다.

### 변형 선택 방식 (아트가 알아야 할 것)
`EnvironmentKit.Pick(array, module)` = `module % 배열길이`. 셀 좌표에서 결정론적으로 뽑으므로
**a/b/c 는 서로 섞여 격자에 깔린다.** 변형 하나가 튀면 그 패턴이 규칙적으로 반복되어 보인다 —
세 장의 **평균 명도·채도 차이는 작게**, 디테일 차이로만 구분할 것 (바닥 A~C 검사와 같은 원칙).

## 4. 권장 납품 — 있으면 좋음 (없으면 cap 아트가 처리한다고 봄)

| 파일 | 캔버스 | 피벗 | 역할 |
|---|---|---|---|
| `tr01_reference_wall_top_rim_a_albedo.png` | 128×128 | 중앙 | 북쪽이 열려 상단 림이 보이는 cap |
| `tr01_reference_wall_outer_corner_a_albedo.png` | 128×128 | 중앙 | 볼록 모서리 — **회전 복제 금지**, §2 광원 방향으로 별도 제작 |
| `tr01_reference_wall_inner_corner_a_albedo.png` | 128×128 | 중앙 | 오목 모서리 |
| `tr01_reference_wall_side_west_a` / `_east_a` | 128×128 | 중앙 | 서/동 측면이 드러난 셀 덧조각 |
| `tr01_reference_floor_edge_a_albedo.png` | 128×128 | 중앙 | 벽 인접 바닥 경계 블렌드 |

## 5. 후속 요청 — 채널맵 (5단계, 이번 납품에 포함하지 않아도 됨)

albedo 와 **동일 캔버스·실루엣·피벗·패딩**으로 (계약 §5):

| 채널 | 접미사 | 색 공간 | 규칙 |
|---|---|---|---|
| Normal | `_normal` | **Linear** | 탄젠트 공간, 평면 중립 `(128,128,255)`. **§2 광원 방향과 일치하는 요철.** 생성형 가짜 노멀 납품 금지 |
| Material Mask | `_mask` | Linear | R 금속 · G 광택 · B 습윤/결정 · A 효과 강도 |
| AO | `_ao` | Linear | 흰색 = 차폐 없음. 접점·깊은 틈만 |
| Emission | `_emission` | sRGB | 비발광 검정, 광원 중심 고유 발광색 |

우선순위: ① 벽 cap/front 노멀 → ② 바닥 a~f 노멀 → ③ 수정 A emission(광물광이 실제로 픽셀을 물들이게)
→ ④ 마스크. 마스크 채널 배분(캐릭터 G / 프롭 R, `Renderer2D` 슬롯 채널 변경)은 5단계 착수 시 확정한다.

## 6. 납품 위치와 형식

```
art-production/test-room-v01/
  source/reference_wall_set_v1/        생성 원본(256px/셀 이상)
  working/reference_wall_set_v1/       클린업·반복 경계 검사 중간물
  approved/albedo/                     승인본 (128px/셀)
  metadata/manifest.json               revision 갱신 — 기존 승인본은 덮어쓰지 않는다
unity/TunnelCrew/Assets/Art/Visual/ReferenceCalibrationV1/   Unity 임포트본 (기존 10장과 같은 폴더)
```

manifest 항목(계약 §10): `pivotPixels`(이미지 좌상단 원점), `footprintWidth/Height`(전부 1×1),
`visualHeightCells`(front = 1.0), `repeatEdges`(cap: `nesw`, front: `ew`).

## 7. 검수 항목 (구현 트랙이 확인할 것)

1. **6×6 반복 검사** — cap a/b/c 혼합 격자와 front a/b/c 가로 띠에서 이음새·규칙 반복이 보이지 않는다
   (바닥 `tr01_reference_floor_variants_6x6.png` 와 같은 방식).
2. **cap–front 접합** — Visual Lab 에서 벽 한 줄을 세웠을 때 cap 하단과 front 상단 사이에 선이 없다.
3. **접합 AO** — 광원을 전부 꺼도(프리셋 ① 전역광만) 바닥–벽 경계가 읽힌다(§15.1).
4. **평균 명도** — a/b/c 평균 명도 차 ≤ 바닥 A~C 의 차이 수준. 튀는 변형이 격자 패턴을 만들지 않는다.
5. **레퍼런스 보드 대조** — 보드 옆에 나란히 놓아 팔레트가 어긋나지 않는다.
6. **광원 방향** — 세 cap·세 front 의 자체 음영이 전부 좌상단 35° 로 일관된다.

## 8. 도착 후 구현 트랙이 할 일 (참고)

`SurfaceMaterialSet_Reference_floor/walltop/wallfront` 생성 → `EnvironmentKit_ReferenceV1` 채움 →
`EnvironmentChunkRenderer` + `ShadowGeometryBuilder` + 파괴 갱신을 프리셋 랩에 배선(4단계) →
프리셋 8개 회귀 + 벽 윤곽 그림자 확인.
