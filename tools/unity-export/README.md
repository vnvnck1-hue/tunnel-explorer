# tools/unity-export — 원본 HTML → Unity 자산·데이터 추출

기준 빌드 `tunnel-crew-infinite-mode-v7.9.2.html` (동결 커밋 `b55d39e`) 에서
Unity 프로젝트가 쓸 데이터와 자산을 뽑아내는 스크립트 모음이다.

**원칙**: Unity 런타임이 HTML 을 파싱하지 않는다. 여기서 한 번 뽑아 파일로 굳히고,
이후 Unity 는 그 파일만 읽는다. 원본이 바뀌면 스크립트를 다시 돌린다.

## 실행

프로젝트 루트에서 실행한다. Node 18+ 필요.

```bash
node tools/unity-export/dump-tuning.mjs
node tools/unity-export/extract-embedded-audio.mjs
```

## 스크립트

| 스크립트 | 입력 | 출력 | 상태 |
|---|---|---|---|
| `dump-tuning.mjs` | HTML 의 `const` 선언 44개 | `unity/.../Data/raw/*.json` | 완료 |
| `extract-embedded-audio.mjs` | `MENU_SFX.bank`, `DRILL_SMP.wav`, `*_BGM_DATA` | `unity/.../Assets/Audio/` + `sfx-bank.json` | 완료 |
| `bake-procedural-sfx.mjs` | `SFX.*` 중 절차 합성 17종 | `Audio/baked/*.wav` | 미작성 (M7) |
| `slice-character-sheets.py` | `assets/characters/reely-*-actions/` | 스프라이트 슬라이스 메타 | 미작성 (M1) |
| `import-tiles.mjs` | `tunnel_crew_tile_resources_v1/tile_manifest.json` | SpriteAtlas + 인덱스 룰 | 미작성 (M1) |
| `gen-tile-normals.py` | 타일 536장 | `_NormalMap` 아틀라스 | 미작성 (M2) |
| `dump-mapgen-fixture.mjs` | `genTunnel(d)` 시드 N개 | `fixtures/map-{seed}-{depth}.json` | 미작성 (M1, 패리티 게이트) |

## 동작 방식 — 왜 HTML 을 실행하지 않는가

원본은 canvas · WebGL · AudioContext · DOM 에 강하게 묶여 있어 jsdom 으로도 통째로
실행하기 어렵다. 대신 `lib/js-scan.mjs` 가 소스에서 선언의 **범위만** 잘라내고,
잘라낸 조각을 `node:vm` 샌드박스에서 평가한다. 문자열·템플릿·주석 안의 괄호를
세지 않는 스캐너라 중첩 객체도 정확히 잘린다.

## 알려진 제약

- **특성 효과는 자동 추출이 불가능하다.** `INF_TRAITS` 의 `ok()` / `a()` 는 `INF` 전역을
  직접 변형하는 클로저다. JSON 에는 `"[fn]"` 로 남고, 실제 효과는 계획 §5 M4 의
  수작업 표에서 다룬다. **원본 특성 수는 72종**이다 (기획 문서의 "약 60종",
  `traits.json` 의 56종과 다르므로 M4 작업량 산정에 이 숫자를 쓸 것).
- `INF_NODE_OFFSETS` 는 `infNodeRadialOffsets()` 를 호출해 만들어지므로,
  `dump-tuning.mjs` 의 `PRELUDE` 에 그 헬퍼를 먼저 올린다.
- `const A=1, B=2;` 형태의 다중 선언은 앞 이름을 평가할 때 뒤 이름도 컨텍스트에
  올라온다. 스크립트가 이를 회수한다 (`R_MINION` / `R_SHELLY`).

## 추출 결과 요약 (2026-09-06)

- 튜닝 상수 **44 / 44**. `DEMO` 189필드, `INF_TRAITS` 72종, `INF_RELICS` 33종,
  `INF_PLANET` 지층 3 + 이상지대.
- 오디오 **53 파일**: SFX 뱅크 25종 / 48개(ogg), 드릴 3개(wav), BGM 2개(mp3, 8.1MB).
  런타임 외부 파일 5개(앰비언스 3 · 보스 BGM 1 · 포효 1)는 원래 `assets/` 에 있으므로
  추출 대상이 아니다.
