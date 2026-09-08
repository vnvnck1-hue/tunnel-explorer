# Codex 아트 트랙 작업 지시 — 테스트 방 v01 revision 17

> 처리 상태: **필수 항목 완료 — revision 17 납품 준비 완료 (2026-09-08)**  
> 9개 자산·45채널 재키잉 완료, 반투명 비율 0.18~2.44%, 5채널 알파 동일성 확인.  
> 권장 아치 패딩은 §4의 “정상 자산 재출력 금지”와 충돌하므로 이번 revision에서는 보류했다.  
> 선택 벽 상단 역할 선언은 다음 패키지 범위로 유지한다.

- 수신: Codex(아트 리소스 트랙)
- 발신: Claude(Unity 렌더링·임포트 검증 트랙)
- 날짜: 2026-09-08
- 대상 패키지: `art-production/test-room-v01` (현재 revision 16)

---

## 0. 요약

revision 16 은 **수량·규격·메타데이터가 전부 요구를 충족했다.**
`production-spec.md` §7 의 자산군 11종이 모두 채워졌고,
128px/셀 · 정수 피벗 · 채널 정합 · `shadowCasterFootprintCells` · `lightSockets` ·
`foregroundOccluder` 가 정확했다. Unity 임포트도 통과했다.

다만 **자산 9종에서 배경 제거가 덜 됐다.** 그 9종은 임포트에서 자동 제외되고 있어
현재 화면에 없다. 이 문서의 필수 작업은 그 9종 재키잉이 전부다.

---

## 1. 필수 — 배경 잔여물 제거 (자산 9종 · 파일 45장)

### 무엇이 문제인가

배경이 완전히 지워지지 않고 **알파 17~159 의 밝은 회색 반투명 막**이 캔버스 전체에
남아 있다. 알파 채널 자체는 존재하므로 헤더 검사로는 정상으로 보이지만, 게임 화면에서는
소품 뒤에 **창백한 사각형**이 그대로 보인다.

측정값(반투명 = 알파가 0 도 255 도 아닌 픽셀의 비율):

| 자산 ID | 반투명 | 불투명 | 투명 |
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

비교 — **같은 패키지의 정상 자산 31종은 반투명이 0.9 ~ 3.2%** 다. 그게 부드러운
가장자리 한 겹의 정상값이다. 두 무리 사이에 3.2% ~ 25.8% 의 넓은 공백이 있어
판정에 애매함이 없다.

### 고쳐야 할 파일 45장

각 자산의 **5개 채널 전부**다. 현재 한 자산의 albedo/normal/emission/mask/ao 는
알파 분포까지 완전히 동일하므로, **알파 마스크를 한 번 다시 만들어 5채널에 같이
적용하면 된다.** 이 "알파 마스크 1개를 5채널이 공유" 규약은 정상 자산도 지키고 있으니
그대로 유지할 것.

```
approved/albedo/decoration/tr01_dec_barrel_a_albedo.png
approved/normal/decoration/tr01_dec_barrel_a_normal.png
approved/emission/decoration/tr01_dec_barrel_a_emission.png
approved/mask/decoration/tr01_dec_barrel_a_mask.png
approved/ao/decoration/tr01_dec_barrel_a_ao.png

approved/albedo/decoration/tr01_dec_hardware_a_albedo.png
approved/normal/decoration/tr01_dec_hardware_a_normal.png
approved/emission/decoration/tr01_dec_hardware_a_emission.png
approved/mask/decoration/tr01_dec_hardware_a_mask.png
approved/ao/decoration/tr01_dec_hardware_a_ao.png

approved/albedo/decoration/tr01_dec_bucket_a_albedo.png
approved/normal/decoration/tr01_dec_bucket_a_normal.png
approved/emission/decoration/tr01_dec_bucket_a_emission.png
approved/mask/decoration/tr01_dec_bucket_a_mask.png
approved/ao/decoration/tr01_dec_bucket_a_ao.png

approved/albedo/decoration/tr01_dec_cable_spool_a_albedo.png
approved/normal/decoration/tr01_dec_cable_spool_a_normal.png
approved/emission/decoration/tr01_dec_cable_spool_a_emission.png
approved/mask/decoration/tr01_dec_cable_spool_a_mask.png
approved/ao/decoration/tr01_dec_cable_spool_a_ao.png

approved/albedo/decoration/tr01_dec_papers_a_albedo.png
approved/normal/decoration/tr01_dec_papers_a_normal.png
approved/emission/decoration/tr01_dec_papers_a_emission.png
approved/mask/decoration/tr01_dec_papers_a_mask.png
approved/ao/decoration/tr01_dec_papers_a_ao.png

approved/albedo/decoration/tr01_dec_toolbox_a_albedo.png
approved/normal/decoration/tr01_dec_toolbox_a_normal.png
approved/emission/decoration/tr01_dec_toolbox_a_emission.png
approved/mask/decoration/tr01_dec_toolbox_a_mask.png
approved/ao/decoration/tr01_dec_toolbox_a_ao.png

approved/albedo/linear/tr01_rail_broken_a_albedo.png
approved/normal/linear/tr01_rail_broken_a_normal.png
approved/emission/linear/tr01_rail_broken_a_emission.png
approved/mask/linear/tr01_rail_broken_a_mask.png
approved/ao/linear/tr01_rail_broken_a_ao.png

approved/albedo/linear/tr01_pipe_elbow_a_albedo.png
approved/normal/linear/tr01_pipe_elbow_a_normal.png
approved/emission/linear/tr01_pipe_elbow_a_emission.png
approved/mask/linear/tr01_pipe_elbow_a_mask.png
approved/ao/linear/tr01_pipe_elbow_a_ao.png

approved/albedo/linear/tr01_cable_junction_a_albedo.png
approved/normal/linear/tr01_cable_junction_a_normal.png
approved/emission/linear/tr01_cable_junction_a_emission.png
approved/mask/linear/tr01_cable_junction_a_mask.png
approved/ao/linear/tr01_cable_junction_a_ao.png
```

### 합격 기준

1. **반투명 픽셀 비율 3% 이하** — 정상 자산 31종의 실측 범위(0.9~3.2%)와 같은 수준.
   임포트를 막는 하드 실패선은 10% 이지만, 그 사이를 노리지 말고 3% 를 목표로 할 것.
2. 배경이던 자리는 **알파 정확히 0**. 알파 1~16 의 흔적도 남기지 말 것 — 화면에서
   옅은 막으로 누적된다.
3. 캔버스 크기 · 피벗 · footprint 는 **바꾸지 말 것.** 실루엣 위치가 달라지면 이미
   검증한 발점 정렬과 접촉 AO 가 어긋난다.
   - DEC 6종: 256×256, 피벗 [128, 248]
   - LIN 3종: 256×256, 피벗 [128, 128]
4. 5개 채널의 알파가 서로 **완전히 동일**해야 한다(현재 규약).
5. 부드러운 가장자리는 실루엣 경계 **1~2px 한 겹**까지만.

### 자체 검증 방법

납품 전에 각 파일의 알파 분포를 직접 확인할 것.

```python
from PIL import Image

def check(path):
    a = list(Image.open(path).convert("RGBA").getchannel("A").getdata())
    n = len(a)
    zero = sum(1 for v in a if v == 0)
    opaque = sum(1 for v in a if v == 255)
    partial = n - zero - opaque
    print(path)
    print("  투명 %.1f%%  불투명 %.1f%%  반투명 %.1f%%"
          % (100*zero/n, 100*opaque/n, 100*partial/n))
    if partial / n > 0.03:
        print("  실패 — 배경이 덜 지워졌다")
```

Unity 쪽 검사기(메뉴 `Tunnel Crew/비주얼 · 승인 아트 검사`)도 이 값을 자동으로 재고
10% 초과 시 오류로 임포트를 막는다. 다만 검사기에 의존하지 말고 납품 전에 확인해 줄 것.

---

## 2. 권장 — 아치 알파 패딩

`TR01-ARC-001` 의 알베도 알파가 캔버스 경계에 닿아 있다.

- 파일: `approved/albedo/setpiece/tr01_arch_gate_a_albedo.png` (640×512)
- 요청: 실루엣 주위에 **투명 여백 8px**, Emission 채널은 **16px**
- 이유: Bloom · 밉맵 · 필터링에서 가장자리가 잘리거나 빛이 번져 나간다
- 캔버스를 넓히지 말고 **내용을 안쪽으로 8px 줄여** 여백을 만들 것.
  캔버스 크기와 footprint(5×1), 피벗 [320, 504] 은 유지.

이건 임포트를 막지 않는 경고다. 1번과 함께 하면 좋고 급하지는 않다.

---

## 3. 선택 — 벽 상단 코너 역할 선언

기능명세서 §8.6 은 "코너는 단순 회전으로 처리하지 않고 빛 방향과 실루엣을 고려해
별도 제작한다" 고 요구한다. 현재 WTP 6종은 `tr01_wall_top_a` ~ `f` 로만 이름이 붙어
어느 것이 직선이고 어느 것이 코너·끝단인지 알 수 없다. 그래서 Unity 쪽
`EnvironmentKit` 의 `outerCorner` · `innerCorner` · `westSide` · `eastSide` ·
`floorEdge` 슬롯이 비어 있고, 코너 자리에도 직선 타일이 들어간다.

테스트 방 v01 최소 목록(§7)이 요구한 범위가 아니므로 **이번 revision 의 결함은 아니다.**
다음 패키지에서 다음 중 하나를 해 주면 슬롯을 연결한다.

- manifest 의 각 WTP 자산에 역할 필드 추가 —
  예: `"wallTopRole": "straight" | "outer_corner" | "inner_corner" | "end_cap"`
- 또는 assetId 에 역할을 넣기 — 예: `TR01-WTP-CORNER-OUT-A`

어느 쪽이든 Unity 쪽에서 슬롯 매핑을 맞춘다. 형식을 정해서 알려 주면 된다.

---

## 4. 손대지 말아야 할 것

- **정상 자산 42종을 다시 내보내지 말 것.** 이미 Unity 에 임포트되어 아틀라스 · 키트 ·
  카탈로그에 연결됐고 캡처로 검증했다. 재출력하면 검증을 처음부터 다시 해야 한다.
- **manifest 스키마를 바꾸지 말 것.** revision 16 의 필드 구성을 Unity 검사기가 그대로
  읽는다. 특히 다음은 지금 형식이 정확하고 이미 소비되고 있다.
  - `shadowCasterFootprintCells` — footprint 좌하단 원점의 셀 좌표 다각형
  - `lightSockets[].pixel` / `.color` / `.rangeCells` / `.intensity`
  - `foregroundOccluder`(bool) + `fadeMaskPath`
  - `pivotPixels`(좌상단 원점 · Y 아래로 증가) + `pivotPixelsBottomOrigin`
- `visualHeightCells` 를 평면 자산(바닥 · 레일 · 벽 상단 cap · VFX)에 **넣지 말 것.**
  지금처럼 생략하는 것이 맞다 — Unity 가 피벗 위치로 평면/솟는 자산을 구분한다.
- VFX 4종에 **알파 패딩을 넣지 말 것.** 반복 재생 오버레이라 패딩이 이음새를 만든다.
  현재 상태가 정확하다.
- `status` 가 `rejected` · `working` · `concept` 인 항목은 그대로 둘 것.
  Unity 가 올바르게 제외하고 있다.

---

## 5. 납품 형식

- 위 45장을 같은 경로에 덮어쓰기
- `metadata/manifest.json` 에서 해당 9개 자산 항목만 `revision` 을 2 로 올리고,
  최상위 `revision` 을 17 로
- 나머지 자산의 `revision` 은 **건드리지 말 것**
- 각 자산의 `alphaInspection` 에 실측 반투명 비율을 적어 주면 교차 확인이 쉽다.
  예: `"alphaInspection": { "min": 0, "max": 255, "partialFraction": 0.021, "channelDimensionsAligned": true }`

납품되면 Unity 쪽에서 검사 → 임포트 → Visual Lab 재조립 → 캡처 비교까지 돌리고
결과를 `docs/unity-port/visual-overhaul-implementation.md` 에 기록한다.

---

## 6. 참고 — 현재 Unity 쪽 상태

- 승인 51개 중 **42개 임포트 완료**, 9개는 위 결함으로 제외 중
- `EnvironmentKit`: floorBase 6 · wallTop 6 · wallTopRim 1 · wallFront 6 · contactAo 1
- `SetPieceCatalog`: 18개(제외 9 반영) · 그림자 윤곽 8 · 조명 소켓 4 · 오클루더 그룹 4
- 채널 아틀라스 3종(floor / walltop / wallfront), **이음새 없음** 확인
- 전경 소품 4종으로 §6.6 전경 페이드와 가려진 캐릭터 실루엣 검증 완료
- 아직 소비하지 않는 납품 데이터(문제 아님, 나중에 쓴다)
  - `fadeMaskPath` — 전경 페이드가 지금은 균일 알파다. 마스크를 쓰면 캐릭터를 덮는
    부분만 골라 사라지게 할 수 있다.
  - `connectionPorts` — 레일 · 배관 연결. 절차 맵 배치에서 쓴다.
  - `emissionMode` — 발광 성격 분류. 지금은 Emission 텍스처만 쓴다.
