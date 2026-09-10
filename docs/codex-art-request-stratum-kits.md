# Codex 아트 요청서 4차 — 지층 2·3·이상지대 환경 키트 + 보스 벽 + 광원 소품

작성: 2026-09-10 · 요청자: Claude 구현 트랙 · 대상: Codex 아트 트랙
근거: 기획서 [unity-visual-overhaul-functional-spec.md](unity-visual-overhaul-functional-spec.md) §8.3(지층별 구조 언어) · §8.6(R2 벽 구조) ·
이주 계획 [unity-port/reference-lab-migration-plan.md](unity-port/reference-lab-migration-plan.md) "본선 B 7단계"
계약: [../art-production/test-room-v01/process/production-spec.md](../art-production/test-room-v01/process/production-spec.md)
이전 요청서: [1차](codex-art-request-reference-wall-set.md) · [2차](codex-art-request-wall-volume.md) · [3차](codex-art-request-r2-darkness-boundary.md) — **전부 납품·소비 완료(manifest r20)**

상태: **기존 범위 납품 완료 (2026-09-10 · manifest r21)**  
범위 메모: 최초 요청 범위인 지층 알베도 36장 + 보스 벽 2장을 완료했다. 이후 추가된 §2-A 노멀맵 48장은 사용자 지시에 따라 이번 납품에서 제외하며 **미착수** 상태로 남긴다. §4 권장 소품도 미착수다.

납품 위치:
- 승인본: `art-production/test-room-v01/approved/albedo/`
- Unity 임포트본: `unity/TunnelCrew/Assets/Art/Visual/ReferenceCalibrationV1/`
- 생성 원본·로그: `art-production/test-room-v01/source/stratum_kits_v1/`
- 반복/비교 QA: `art-production/test-room-v01/qa/tr01_stratum2_*_6x6.png`, `tr01_stratum3_*_6x6.png`, `tr01_abyss_*_6x6.png`, `tr01_stratum_kits_comparison.png`, `tr01_reference_boss_wall_comparison.png`

## 0. 왜 4차인가

본선 `Run.unity` 가 레퍼런스 키트 환경 렌더러로 그려지기 시작했다(이주 B 7단계). 그런데 본선은 **지층 4개**
(지층 1 표층 · 지층 2 균열 · 지층 3 코어 · 이상지대)인데 키트는 **지층 1 하나**다. 지금은 층을 내려가도 같은 보라 암석이
나온다 — 기획서 §8.3 "각 지층은 같은 타일의 색상 변경이 아니라 서로 다른 구조 언어를 가져야 한다"에 어긋난다.

이번 요청은 **지층당 12장 × 3 = 36장**이 필수다. 1~3차에서 확정된 R2 벽 구조(상단 + 남향 정면 + 림, 측면·코너 없음)를
지층마다 반복한다. 접점 AO·드롭섀도·균열은 **순수 알파 마스크라 지층 공용**이다 — 다시 그리지 않는다.

## 1. 지층별 구조 언어 (기획서 §8.3 · 대기 프로파일 실측)

| 지층 | 접두어 | 구조 언어 | 주조명 | 주요 재질 | 대기 프로파일 색(참고) |
|---|---|---|---|---|---|
| 지층 2 균열 | `tr01_stratum2_` | 균열 지대 · 무너진 레일 · 불안정한 기계 | 보라·적색 균열광 | 갈라진 암석, 녹슨 금속, 먼지 | fog (0.16,0.15,0.28) · near (0.26,0.15,0.28) · far (0.12,0.17,0.31) |
| 지층 3 코어 | `tr01_stratum3_` | 코어 시설 · 거대 수정 · 고대 산업 구조 | 마젠타·청록 고휘도 | 결정, 검은 암반, 황동·강철 | fog (0.14,0.12,0.24) · near (0.27,0.14,0.27) · far (0.10,0.13,0.28) |
| 이상지대 | `tr01_abyss_` | 비정상 공간 · 뒤틀린 구조 · 생체 광물 | 팔레트 변형 + 불안정 펄스 | 변이 암석, 생체막, 발광 균열 | fog (0.11,0.10,0.22) · near (0.28,0.13,0.29) · far (0.08,0.10,0.26) |

**팔레트 원칙.** 세 지층 모두 퍼플 기조를 공유한다 — 퍼플은 어둠 색(라이트마스크 `dark`)이 만들고, 지층 차이는
**재질과 구조**로 낸다. 지층 2 는 지층 1 의 보라 암석에 균열·붉은 녹·먼지가 덮인 느낌, 지층 3 은 채도가 빠진 검은 암반에
결정·황동이 박힌 느낌, 이상지대는 형태가 뒤틀리고 생체막이 덮인 느낌. 색만 바꾼 리컬러는 반려한다(§8.3).

## 2. 필수 납품 — 지층당 12장 × 3 지층 = 36장

규격은 1~3차와 동일: **128 PPU · 정수 픽셀 · 안전 패딩 8px · `<접두어><name>_<variant>_<channel>.png`**
광원 방향 **좌상단 35°** 고정(계약 §5-1). 아래 표의 `<pfx>` 에 지층 접두어를 넣는다.

| # | 파일 | 캔버스 | 피벗 | 역할 · 1차 대응 |
|---|---|---|---|---|
| 1~3 | `<pfx>floor_{a,b,c}_albedo.png` | 128×128 | 중앙 | 바닥 큰 면 3종. 이음새 없는 채움. 지층 1 `floor_a/b/c` 와 같은 규칙 |
| 4~6 | `<pfx>wall_top_{a,b,c}_albedo.png` | 128×128 | 중앙 | 벽 상단 cap 3종. **R2 에서 cap 은 미탐색 검정에 대부분 덮인다** — 굴착 경계 한 줄만 보이므로 무늬보다 재질 읽힘이 우선 |
| 7~9 | `<pfx>wall_top_rim_{a,b,c}_albedo.png` | 128×128 | 중앙 | 북쪽 열린 cap(림). **굴착 경계의 얼굴** — 북·서 밝고 남·동 어둡다(좌상단 광원). 3차 림 b/c 와 같은 규칙 |
| 10~12 | `<pfx>wall_front_{a,b,c}_albedo.png` | 128×128 | 하단 중앙 | 남향 정면 3종. 1셀 높이 띠 — 높이는 코드가 압출한다(§8.6.1). 동·서로만 이어지면 된다 |

**지층 공용(재요청 없음)**: 접점 AO 4방향 · 드롭섀도 타일셋 4장 · 균열 3단계 — 알파 마스크라 그대로 쓴다.
**만들지 않는 것**: 서·동 측면, 코너, cap 변형 d/e/f (기획서 §8.6.2, 3차 §4).

우선순위는 **지층 2 → 지층 3 → 이상지대**. 지층 2 12장이 먼저 도착하면 즉시 본선 2층에 걸 수 있다(§5).

## 2-A. 필수 — 노멀맵 (`_normal`) · 지층 1 레퍼런스 세트부터 12장

**왜 지금 추가하는가(2026-09-10 저녁 실측).** 랩에 노멀 강도 0 / 1.6 / 3.0 프리셋을 만들어 비교했는데 **세 장면이 픽셀 단위로 같았다.**
`SurfaceMaterialSet_Reference_{floor,walltop,wallfront}` 의 `normal` 슬롯이 전부 비어 있고(`{fileID: 0}`), 1~3차 납품은 전부
albedo 였다. 즉 노멀맵 다이나믹 라이팅(손전등을 돌리면 벽 베벨이 살아나는 그 기술)은 **배선은 돼 있는데 데이터가 없다.**
TestRoomV01 세트에는 노멀 46장이 있어 VisualLab 에서는 동작한다 — 레퍼런스 세트로 갈아타면서 채널이 빠졌다.

| 파일 | 대응 albedo | 규격 |
|---|---|---|
| `tr01_reference_floor_{a,b,c}_normal.png` | 바닥 3 | **Linear** · 평면 중립 `(128,128,255)` · 좌상단 35° 요철 |
| `tr01_reference_wall_top_{a,b,c}_normal.png` | cap 3 | 같은 규격 |
| `tr01_reference_wall_top_rim_{a,b,c}_normal.png` | 림 3 | 같은 규격. **림의 북·서 베벨이 노멀에서 살아야 한다** — 손전등이 마주보면 여기서 림이 선다 |
| `tr01_reference_wall_front_{a,b,c}_normal.png` | 정면 3 | 같은 규격, 피벗 하단 중앙 |

- albedo 와 **동일 캔버스·실루엣·피벗·패딩**(계약 §5).
- **생성형 가짜 노멀 납품 금지** — 2차 요청서 §4 와 같은 조건. Ori 팀이 "cheap and plasticky" 로 폐기한 길이다. 랩 프리셋 ⑯(강도 3.0)에서 비닐 느낌이 나면 그걸로 판별한다.
- 지층 2·3·이상지대 키트(§2)도 **albedo 와 함께 `_normal` 을 짝으로** 납품한다 — 지층당 12장 추가. 채널 없이 오면 그 지층은 노멀 라이팅이 없는 채로 들어간다.
- `_ao` · `_mask` · `_emission` 은 그 다음(2차 §4 우선순위 그대로).

**구현 트랙**: 타일맵 하나에 재질 하나라 cap 3장이 노멀 1장을 공유할 수 없다 — 스프라이트별 세컨더리 텍스처는 URP 17 타일맵 조명에
반영되지 않는다(RunBootstrap 주석 2026-09-07). 변형 a/b/c 를 한 아틀라스로 굽고 노멀도 같은 배치로 굽는 경로(`ChannelAtlasBuilder`)를
레퍼런스 키트에 연결한다. 이건 아트 도착 전에 준비한다.

## 3. 필수 — 보스 소환 벽 (지층 공용 · 2장)

| 파일 | 캔버스 | 피벗 | 역할 |
|---|---|---|---|
| `tr01_reference_boss_wall_top_a_albedo.png` | 128×128 | 중앙 | 보스 소환 벽 cap. 붉은 기운·맥동하는 균열. 원본 BOSS_WALL_TILES 계승 |
| `tr01_reference_boss_wall_front_a_albedo.png` | 128×128 | 하단 중앙 | 같은 벽의 정면 |

**왜** — 구 `WorldRenderer` 는 전용 붉은 타일 2종으로 그렸는데 키트에 없어서 지금 본선은 cap 위에 **붉은 반투명 셀**을 얹어
때우고 있다(`Boss Wall Tint`). "이 벽은 다르다"는 게임플레이 정보라 임시로 둘 수 없다. 지층 공용 1세트면 된다 — 보스 벽은
어느 층에서도 같은 것으로 읽혀야 한다.

## 4. 권장 — 광원·채굴 소품 (여유가 있으면, 지층 공용)

| 파일 | 캔버스 | 피벗 | 역할 |
|---|---|---|---|
| `tr01_reference_ore_front_{a,b}_albedo.png` + `_emission.png` | 128×128 RGBA | 하단 중앙 | 정면에 얹는 광맥 오버레이 2종(코어키퍼 `oreFront`). 채굴 대상 표시. 이미션은 광맥만 |
| `tr01_reference_torch_a_albedo.png` + `_emission.png` | 96×128 RGBA | 하단 중앙 | 설치형 횃불 소품. 코어키퍼 루프의 "횃불 박기" — 본선 `Lamp_*` 광원 자리에 놓을 실체 |
| `tr01_reference_floor_edge_a_albedo.png` | 128×128 | 중앙 | 벽에 인접한 바닥 경계 블렌드(`floorEdge` 슬롯, 2·3차에서 이월) |

## 5. 구현 트랙이 병행하는 것 (아트 대기 아님)

- **키트 빌더 지층화** — `BuildReferenceEnvironmentKit` 의 접두어 `tr01_reference_` 를 매개변수로 빼고
  `EnvironmentKit_Stratum{2,3}` · `EnvironmentKit_Abyss` 를 만든다. 공용 AO·드롭섀도·균열은 지층 1 키트에서 참조 복사.
- **본선 층별 키트 선택** — `RunBootstrap.BindEnvironment` 가 `_depth` 에 따라 키트·프로파일·재질 세트를 고른다
  (`StratumMoodDirector` 가 대기·Volume 을 고르는 것과 같은 축). 지층 2 키트가 오면 2층부터 바뀐다.
- **보스 벽** — 키트 슬롯 `bossWallTop/Front` 신설, `Boss Wall Tint` 제거, `SurfaceMask` 에 보스 벽 플래그 → 렌더러가 cap/정면을 갈아 끼움.
- **광맥** — `WorldGrid` 의 광맥 타일 타입 → 정면 오버레이(균열과 같은 `WallCrackOverlay` 경로).

## 6. 검수 항목

1. 지층 3종을 **나란히 놓았을 때 색만 다른 게 아니라 재질·구조가 다르게** 읽힌다(§8.3) — 4차의 합격선
2. 각 지층 6×6 반복 검사 — floor/cap/rim/front 이음새 0
3. 림 a/b/c 의 광원 방향 일관(북·서 밝음)
4. 지층 공용 알파 마스크(AO·드롭섀도·균열)를 세 지층 정면·바닥 위에 올려 봐도 어긋나지 않는다
5. 보스 벽이 어느 지층 키트 옆에서도 "다른 벽"으로 즉시 읽힌다
6. 모든 파일 128 PPU · 8px 패딩 · 파일명 규약 · 좌상단 35°
