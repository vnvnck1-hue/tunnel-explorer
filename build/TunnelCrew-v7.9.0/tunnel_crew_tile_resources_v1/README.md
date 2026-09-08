# Tunnel Crew Tile Resources

레퍼런스 이미지의 보라색 동굴 배경 스타일과 `BACKGROUND_TILE_RESOURCE_GUIDE.md`의 규격을 기준으로 제작한 타일 리소스다.

## 결과물

- `purple/`: 레퍼런스와 직접 대응하는 기본 퍼플 땅굴
- `brine/`: 동일한 원화를 가이드의 청록 염굴 팔레트로 변환한 바이옴
- `source_art/`: 이미지 생성 도구로 만든 4×4 기준 재질·광맥·오버레이 원화
- `tile_manifest.json`: 런타임용 전체 인덱스/좌표
- `tile_manifest.csv`: 스프레드시트 확인용 manifest

각 바이옴 폴더:

- `{biome}_strict_atlas_800x850.png`: 50×50 셀, 16열×17행, 268셀 사용
- `{biome}_strict_atlas_master_1600x1700.png`: 100×100 셀의 @2x 마스터
- `{biome}_floor_sheet_150x50.png`: dark/base/rim 3개 바닥 타일
- `individual_50/`: 268개 벽 타일 + 3개 바닥 타일
- `overlays/`: core 측면 4개, 밴드 seam 3개, core 하단 그림자 1개

## 엄격 atlas 인덱스

```text
typeIndex: dirt=0, stone=1, ore=2, gem=3, crys=4
index = typeIndex×48 + band×12 + surfaceVariant×4 + damageStage

rock     = 240 + band
core_top = 244 + band×6 + variant

atlasX = (index mod 16) × 50
atlasY = floor(index / 16) × 50
```

## 검증 결과

| 항목 | purple | brine |
|---|---:|---:|
| 엄격 atlas 타일 | 268 | 268 |
| 바닥 타일 | 3 | 3 |
| 개별 50×50 PNG | 271 | 271 |
| 오버레이 | 8 | 8 |
| 크기 오류 | 0 | 0 |
| 손상 타일 alpha 결손 | 확인 | 확인 |

전체 manifest 행은 536개이며 `(biome, index)` 조합은 모두 고유하다.

## 이미지 생성 방식

기준 원화는 Codex 내장 이미지 생성 도구로 만들었다. 레퍼런스는 스타일과 재질 참고용으로만 사용했고 UI, 캐릭터, 적, 횃불, 웅덩이와 텍스트는 제외했다.

사용한 프롬프트 세트의 핵심:

1. `wall materials`: 4열×4행, 열은 dirt/stone/rock/core, 행은 깊이 밴드 0–3. 정면 직교, 보라색 동굴, 둥근 암석 면, 좌상단 하이라이트, 글자와 거터 없음.
2. `resources and floor`: 4열×4행, 열은 ore/gem/crys/floor, 행은 깊이 밴드 0–3. 세 결정 군집 위치 고정, 바닥은 warm brown 변형.
3. `damage and decals`: 4열×4행 투명 오버레이. 행은 damage 1/2/3과 pebble/rune decal. 생성본의 체크무늬 배경은 후처리에서 실제 alpha로 변환.

후처리는 정수 그리드 crop, 100→50px 고품질 축소, 단계별 3/6/9개 가장자리 alpha 결손, d3 모서리 결손, atlas 배치와 manifest 생성을 수행한다.

## 재생성

`tools/build_tile_resources.ps1`을 실행하면 현재 저장된 AI 원화를 기준으로 동일한 파일 구조와 인덱스를 다시 만든다.

