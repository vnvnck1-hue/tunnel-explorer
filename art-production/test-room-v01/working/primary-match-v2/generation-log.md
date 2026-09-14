# 생성 기록 — 최상위 레퍼런스 기본 표면 키트 V2

날짜: 2026-09-15

모드: built-in ImageGen

Use case: `stylized-concept`, 교정은 `precise-object-edit`, 알파 추출은 `background-extraction`

공통 최상위 입력: `../../reference/tr01_primary_style_target.png` — 스타일·재질·투영 기준

## 1. 바닥

첫 호출은 “4×4의 서로 교환 가능한 타일”을 요청했다. 결과가 각 셀을 어두운 사각 테두리로
완결해 격자 문제가 재현되었으므로 폐기했다.

채택본 최종 프롬프트:

```text
Use case: precise-object-edit
Asset type: production source macro-tile sheet for a Unity 2D game
Input images: Image 1 is the edit target floor sheet. Image 2 is the highest-priority visual reference.
Primary request: keep Image 1's deep plum-violet stone material, chunky pixel-painted rendering, scale, and overall detail, but remove the entire visible 4×4 square-cell framing. Eliminate every long straight horizontal and vertical grid seam that divides the image into sixteen framed squares. Repaint it as one continuous floor field matching the open central floor in Image 2.
Required structure: the image will still be sliced invisibly into a 4×4 grid by code, so allow natural irregular slab shapes, cracks, rubble seams, and color masses to cross those invisible quarter boundaries continuously. No feature should reveal the slicing grid. Make the outer left/right and top/bottom boundaries seamless and tileable as a complete macro texture.
Style/medium: match Image 2 as closely as possible: high-resolution pixel-painted game art, crisp clustered pixels, irregular worn mine-floor slabs, dark colored detail, quiet combat-floor density.
Constraints: change only the grid/framing structure while preserving the selected material language. No straight full-width or full-height seams, no repeated square panels, no tile borders, no walls, rails, crystals, props, characters, UI, text, logo, watermark, checkerboard, transparent holes, outer frame, bloom, vignette, or local colored light pools. Opaque square image.
```

실제 출력은 1254×1254px였다. 채택 이미지에는 내부 격자가 없으므로 3×3, 418px 단위로
재해석했다. 원본은 `tr01_primarymatch_floor_macro_3x3_source.png`다.

## 2. 벽 상단

최종 프롬프트:

```text
Use case: stylized-concept
Asset type: production source macro-tile sheet for Unity 2D, basic wall TOP surface only
Input images: Image 1 is the highest-priority complete visual target. Image 2 is the approved-in-this-task floor material reference that the wall must belong beside.
Primary request: create one square 4×4 macro texture representing the upper horizontal surfaces of solid mine walls, matching Image 1 as faithfully as possible. This is not a grid of sixteen framed tiles. It is one continuous field that code will invisibly slice into sixteen positional pieces. Use rough dark purple cap rock, broad broken stone masses, shallow fissures, embedded rubble and sparse muted magenta mineral traces. Wall tops must be darker, rougher, and more compact than Image 2's walkable floor while clearly sharing the same geology, pixel density, mortar color and rendering language. Natural forms must cross invisible quarter boundaries.
Style/medium: high-resolution pixel-painted game art matching Image 1, crisp clustered pixels, chunky readable forms, dark colored detail.
View: orthographic top surface, screen-axis aligned, no perspective convergence.
Lighting: neutral intrinsic form shading, faint consistent upper-left direction only; no local colored light pools, no bloom.
Constraints: full-bleed opaque square image; outer edges tileable as a macro texture; no visible 4×4 grid, no cell frames, no long straight seams, no vertical wall face, no floor path, no rails, arches, crystals, props, characters, UI, text, logos, watermark, checkerboard, transparency, border, or vignette.
```

실제 출력 크기에 맞춰 3×3 위치형 매크로로 채택했다.

## 3. 벽 정면

최종 프롬프트:

```text
Use case: stylized-concept
Asset type: production source macro-tile sheet for Unity 2D, exposed WALL FRONT only
Input images: Image 1 is the highest-priority complete visual target. Image 2 is the approved-in-this-task floor material reference that the wall must belong beside.
Primary request: create one square 4×4 macro texture representing a continuous front-facing mine wall facade, matching the purple rock wall faces around the play space in Image 1 as faithfully as possible. It is one continuous facade that code will invisibly slice into sixteen positional pieces, not sixteen framed tiles. Compose irregular horizontal stone courses, chunky broken rock blocks, deep near-black purple crevices, sparse muted magenta mineral flecks, and occasional very restrained dark metal reinforcement fragments integrated into the masonry. Natural block courses and cracks must cross invisible quarter boundaries. Keep the top and bottom edge contracts horizontally continuous.
Style/medium: high-resolution pixel-painted game art matching Image 1, crisp clustered pixels, readable chunky planes, dense dark colored shadows.
View: straight orthographic front face, no perspective convergence, no side face.
Lighting: weak intrinsic upper-left form shading only, no cyan/magenta/orange light pools and no bloom.
Constraints: full-bleed opaque square image; horizontally seamless as a complete macro facade; no visible 4×4 grid, no cell frames, no repeated square panels, no floor top surface, no rails, arches, pipes, large crystals, props, characters, UI, text, logo, watermark, checkerboard, transparency, outer border or vignette.
```

실제 출력 크기에 맞춰 3×3 위치형 매크로로 채택했다.

## 4. 벽 상단 림

첫 호출은 4행을 요청했으나 1254px에서 정수 슬라이스가 되지 않아 폐기했다. 3행으로 다시
생성한 결과를 채택했다.

최종 프롬프트:

```text
Use case: stylized-concept
Asset type: production source sprite sheet for Unity 2D, 3×3 exposed wall-top rim family
Input images: Image 1 is the highest-priority visual target. Image 2 defines the new matching floor material. Image 3 defines the new matching wall-top material.
Primary request: create an exactly square sheet organized into precisely three equal horizontal bands and three equal conceptual columns. Each row is one continuous horizontal wall-cap edge variant. Within each row, a narrow irregular rocky lip runs continuously from left edge to right edge, with a near-black recessed seam directly beneath it and the matching rough wall-top material filling the rest of that row. The lip height, recess depth and horizontal endpoint profile must match across all three rows, while small breakups vary. The two horizontal separators between rows may be clean hard boundaries because rows are separate sprites; do not add empty gutters. There must be no vertical separators or visible column grid because each row will be invisibly sliced into three connected positional pieces.
Style: faithful to Image 1's high-resolution pixel-painted purple mine environment; same pixel density and geology as Images 2 and 3, crisp clustered pixels, dark colored detail.
View: orthographic 3/4 cap edge, screen-axis aligned, no perspective convergence.
Lighting: faint neutral upper-left form shading only, no local light pools and no bloom.
Constraints: full-bleed opaque square image; no fourth row, no 4×4 structure, no boxed cells, no decorative rail, pipe, arch, crystal cluster, prop, character, UI, text, logo, watermark, checkerboard, transparent hole, outer frame or vignette.
```

## 5. 벽–바닥 접점 어댑터

첫 생성은 체크무늬를 실제 픽셀로 포함한 불투명 RGB여서 폐기했다. 같은 형태의 배경만 다시
추출해 `Format32bppArgb`, 모서리 알파 0인 결과를 채택했다.

최종 알파 추출 프롬프트:

```text
Use case: background-extraction
Asset type: Unity 2D production sprite sheet with genuine alpha transparency
Input image: Image 1 is the edit target.
Primary request: remove every gray-and-white checkerboard background pixel and replace it with genuine transparent alpha. Preserve only the nine purple pixel-painted rock/rubble contact overlay shapes, including all their small detached rubble fragments, exact positions, proportions, colors, crisp pixel edges, and the 3×3 layout. Do not repaint, resize, move, merge, crop, relight or restyle the purple artwork.
Constraints: transparent background must be actual alpha, not white, gray, black, or checkerboard. Keep the square canvas and all nine shapes exactly where they are. No new shadows, borders, labels, text, logos, watermarks, or extra objects.
```

## 6. 외곽 반복 경계 교정

바닥·벽 상단·벽 정면 각각에 대해 내부를 유지하고 바깥 경계만 반복 연결하라는 단일 변경 편집을
시도했다. 결과의 평균 외곽 RGB 차이는 각각 `8.9/10.3`, `10.9/10.1`, `20.9/12.9`로 기존보다
개선되지 않았거나 악화됐다. 세 결과 모두 폐기하고 프로젝트에는 복사하지 않았다.

비교 기준은 좌우/상하 외곽 픽셀 쌍의 평균 채널 절대차다. 채택본의 외곽값은 다음과 같다.

| 자산 | 외곽 좌우 | 내부 X 절단 | 외곽 상하 | 내부 Y 절단 |
|---|---:|---:|---:|---:|
| 바닥 | 7.6 | 4.1 | 9.3 | 4.8 |
| 벽 상단 | 5.8 | 5.9 | 9.5 | 7.4 |
| 벽 정면 | 9.1 | 5.5 | 10.8 | 5.4 |

외곽 수치가 내부보다 큰 항목이 있으므로 `macroEdgeWrapApproved=false`다.

## 7. 바닥 v3 · Unity 반복 비교

최상위 레퍼런스를 재입력하고 기존 바닥을 실패 기준으로 함께 제공했다. 요청의 핵심은 기존
3×3/만화경 흔적 제거, 여러 셀을 가로지르는 큰 암반 석판, 구조 안에 묻힌 드문 마젠타 광맥,
사방 연결이었다. 출력은 기존 연결 파일
`tr01_primarymatch_floor_macro_3x3_source.png`에 덮어썼으며 별도 버전 파일은 만들지 않았다.

Unity 비교 결과:

- 12셀 미러 반복: 직접 3×3 격자는 감소했으나 화면 좌측에 대칭 경계가 보였다.
- 일반 반복: 대칭은 없어졌지만 월드 UV 왜곡과 큰 붓결이 합쳐져 표면이 늘어난 것처럼 보여 폐기했다.
- 18셀 미러 반복: 중앙 진입 방 안의 반복 경계를 거의 화면 밖으로 밀어 현재 작업 기준으로 채택했다.
- 바닥 마젠타 발광은 0.42에서 0.22로 낮췄다.
- 외곽에서 드문 대칭 흔적이 남으므로 `macroEdgeWrapApproved=false`는 계속 유지한다.

전용 Normal 맵 전에는 Albedo 명도 경사 기반 임시 노멀(바닥 강도 0.65)을 사용한다. 이 기술
패스는 원화 승인이나 최종 채널 납품을 의미하지 않는다.

## 8. 벽 일체형 지지대 3종

최상위 레퍼런스의 산업 구조가 암반과 분리된 소품이 아니라 벽 림과 바닥 접점을 동시에 물고 있다는
관찰을 바탕으로 세로 보강재 3종을 생성했다. 아치는 사용자 지시에 따라 포함하지 않았다.

- 1차 생성은 형태와 화풍은 유효했지만 체크무늬가 실제 RGB 픽셀이어서 폐기했다.
- 2차 배경 추출도 모서리 알파가 255라 폐기했다.
- 3차 배경 추출은 `Format32bppArgb`, 네 모서리와 지지대 사이가 `A=0`이어서 채택했다.
- SHA-256: `946C0FDE351D1D922DF5EA09AEBA9374DB5EAF7E116FC2E26D333B1DE512A421`.
- Sprite Editor 데이터 프로바이더로 418×1254 3열, 바닥 중앙 피벗, 418 PPU로 임포트했다.
- 커스텀 `WorldLit` 공용 재질은 아틀라스 알파가 검은 사각형으로 보이는 문제가 있어 폐기했다. Unity
  표준 `Universal Render Pipeline/2D/Sprite-Lit-Default`에서 실제 알파와 2D 색광을 확인해 채택했다.
- 런타임은 5셀 이상 남향 연속 벽면에만 배치하고 발판 Y로 깊이 정렬한다. 테스트 맵에서 12개 생성,
  활성 12개, 스프라이트 변형 3개 연결을 확인했다.

## 9. 벽 일체형 수평 설비 3계열

최상위 레퍼런스와 채택한 세로 지지대 시트를 함께 입력해, 지지대 상단 소켓 사이를 닫는 3×3
연결 키트를 생성했다. 세 행은 각각 마젠타 이중 파이프, 시안 서비스 패널, 앰버 케이블 트레이이며
각 행이 좌–중–우로 한 구조를 이룬다. 아치·바닥 레일·독립 소품은 요청에서 제외했다.

첫 결과는 형태와 화풍이 유효했지만 체크무늬가 RGB 픽셀이어서 폐기했다. 같은 캔버스와 모든
픽셀을 유지한 채 배경만 실제 알파로 추출하는 편집을 한 번 수행했고, `Format32bppArgb`, 모서리
알파 0, 행 사이 알파 0을 확인해 채택했다.

- 원본: `tr01_primarymatch_wall_conduits_3x3_source.png`
- SHA-256: `FEE9F641C5F7CE47474F934CB54A7140867539F47FE84021465F3B1A5DDF044E`
- Unity 슬라이스: 418×418, 위에서부터 r1/r2/r3, 각 행 left/mid/right, 중앙 피벗, 418 PPU
- 런타임: 5셀 이상 남향 벽면에서 안쪽 좌 지지대부터 안쪽 우 지지대까지 한 계열로 연속 배치
- 검증: 실제 런타임 25개 설비/12개 지지대, 9개 스프라이트 전부 사용, `Sprite-Lit-Default`,
  `WorldEntity`, 구조 테스트 11/11과 맵 패리티 16/16 통과

## 10. 구도용 통합 서비스 조명 기둥 3종

최상위 레퍼런스의 작은 작업등이 벽·바닥의 금속 골조와 암반 접점에 붙어 있다는 관찰을 바탕으로,
기존의 독립 토치를 대체할 3색 서비스 기둥을 생성했다. 채택한 세로 지지대와 수평 설비도 입력해
받침·몸통·상단 소켓의 재질 언어를 맞췄다. 아치·레일·캐릭터는 포함하지 않았다.

첫 결과의 구조와 색 역할은 채택했지만 회색 체크무늬가 24-bit RGB 배경으로 들어 있어 그대로는
사용하지 않았다. 같은 캔버스·형태·색을 유지하고 배경만 실제 알파로 추출하는 편집을 수행했다.
최종본은 `Format32bppArgb`, 네 모서리와 세 기둥 사이가 `A=0`이다.

- 원본: `tr01_primarymatch_service_pylons_3col_source.png`
- SHA-256: `8AB9B0B9A6057669E867A56590F95E7C5FA4B060D1EEC11973D8F8595756B47E`
- Unity 슬라이스: 418×1254, cyan/magenta/amber, 바닥 중앙 피벗, 418 PPU
- 런타임: `PresentationLamps` 세 곳에만 높이 2.15셀·발광부 대비 받침 -1.42셀로 배치
- 검증: 원본/Unity SHA 일치, 실제 알파, 런타임 3/3, `Sprite-Lit-Default`, `WorldEntity`,
  당시 환경 11/11·맵 16/16·비주얼 35/35 통과, pass18 Game View 확인.
  코너·표면 채널까지 합친 pass21 최종 회귀는 환경 17/17·맵 16/16·비주얼 35/35.

## 11. 벽 일체형 코너·측벽 설비 3계열

최상위 레퍼런스, 채택한 벽 지지대, 채택한 수평 설비를 함께 입력했다. 목표는 새 독립 소품을
추가하는 것이 아니라 수평 설비의 금속 단면과 암반 클램프를 그대로 코너와 측벽까지 연장하는
것이었다. 아치와 레일은 명시적으로 제외했다.

채택본 생성 프롬프트의 핵심 규격:

```text
Use case: stylized-concept
Asset type: transparent production sprite sheet for a Unity 2D top-down mine wall junction kit
Inputs: Image 1 is the highest-priority complete visual target. Image 2 is the adopted wall-support kit.
Image 3 is the adopted horizontal conduit kit whose material, cross-section, colour families and sockets must continue.
Create one clean 3x3 sheet. Rows: magenta, cyan, amber. Columns in every row: left 90-degree turn,
straight vertical repeat, right 90-degree turn. Each turn must visibly join the matching horizontal conduit
to a side-wall run through a rock-and-steel clamp; the repeat must tile vertically with the same cross-section.
Match the reference's high-resolution pixel-painted purple mine, crisp clustered pixels, chunky rock planes,
dark steel, restrained emissive accents and integrated rubble seams. Keep every piece centred in its equal cell.
No isolated prop silhouette, no floating connector, no arch, no rail, no character, no UI, no text, no logo,
no outer frame, no cast background, no baked checkerboard. Transparent background.
```

첫 생성본의 구조가 가장 명확해 이를 채택했다. 이후 두 번의 배경 추출 편집은 실제 알파 대신
24-bit RGB 체크무늬를 다시 그려 넣어 폐기했다. 채택본에는 캔버스 외곽에서 이어지는 저채도
체크 영역만 flood-fill로 제거하고, 각 418×418 셀에서 가장 큰 연결 성분만 남기는 결정적 기술
정리를 적용했다. 형태·색·해상도는 재생성하거나 리사이즈하지 않았다.

- 원본: `tr01_primarymatch_wall_junctions_3x3_source.png`
- SHA-256: `BC51E50654324229A54C04372A7659251F2A58049574CE193B929EEF14EABF52`
- Unity 슬라이스: 418×418, magenta/cyan/amber × left/vertical/right, 중앙 피벗, 418 PPU
- 런타임: 5셀 이상 남향 설비 정면의 끝에서 암반 측벽이 위로 2셀 이상 실제 이어질 때만 회전;
  실제 길이만큼 최대 4개 세로 반복
- 검증: 실제 RGBA, 원본/Unity SHA 일치, 9개 이름·사각형·피벗, 고정 QA 맵 회전 4개/반복 10개,
  Organic 17/17·MapGen 16/16·VisualBatch2 35/35, pass19 전체·상세 Game View 확인

## 12. 저밀도 바닥 v4와 정렬 표면 채널

pass20에서 전용 채널을 처음 적용해 최상위 레퍼런스와 다시 비교했다. 벽의 재질 깊이는 좋아졌지만
기존 바닥 v3는 작은 잔석과 밝은 결정이 전체에 균등해 중앙 전투 공간까지 벽처럼 시끄러웠다.
레퍼런스의 중앙 65%가 넓고 저대비인 점을 기준으로 현재 바닥을 `precise-object-edit`했다.

채택본 프롬프트:

```text
Use case: precise-object-edit
Asset type: production source macro texture for a Unity 2D top-down game floor
Input images: Image 1 is the highest-priority complete visual target. Image 2 is the current floor macro that must be improved.
Primary request: repaint Image 2 so it matches the QUIET OPEN CENTRAL WALKABLE FLOOR of Image 1 much more faithfully.
Preserve the deep plum-violet geology, high-resolution pixel-painted rendering, crisp clustered pixels,
orthographic top view, full-bleed square canvas, and continuous macro-texture structure.
Greatly reduce visual noise: remove most small rubble, pebble carpets, repeated rosettes, and most bright
magenta crystal clusters from the interior. Use broad low-contrast worn stone/packed-earth planes, a few large
irregular slab boundaries, sparse shallow cracks and occasional dark gravel pockets. Keep the middle 65 percent
especially calm. Concentrate remaining rubble and muted magenta mineral traces in irregular peripheral bands.
No visible 3x3 grid, no long straight seams, no baked colored light pools, no walls, rails, arches, pipes,
supports, large crystals, props, characters, UI, text, checkerboard, transparency, border or vignette.
```

- 생성 보존본: Codex ImageGen 출력 `exec-4357ebb8-3266-44f0-abae-794c4632ed8f.png`
- 현재 단일 원본: `tr01_primarymatch_floor_macro_3x3_source.png`
- SHA-256: `95C71B350FE90997FBCE8CB5D0B1890B15AE2F04EB1931994B3B37FD666966B9`
- 판정: 중앙 저대비 면적 증가, 결정은 가장자리와 일부 이음으로 이동, 캐릭터·그림자·색광 여백 개선

이후 `tools/art/bake-primary-match-surface-channels.py`로 바닥·벽 상단·벽 정면·림 각각의
Normal/AO/Emission을 같은 1254×1254 좌표에 결정적으로 생성했다. Normal은 두 스케일의 명도
높이장을 사용하고, AO는 국소 공동만 감쇠하며, Emission은 마젠타 광물 픽셀만 알파로 선택한다.
Unity에서 12/12 참조, 실제 URP, NormalMap/sRGB와 AO·Emission 임포트 설정을 확인했다.
pass21 Game View에서 고밀도 바닥보다 중앙 동선이 조용해지고 조명 아래 큰 면이 분리되는 것을 채택했다.

## 13. 석판형 기본 바닥 v5 · 18셀 재확정

pass21을 최상위 레퍼런스와 다시 직접 비교해, 남아 있던 둥근 흙섬·세포형 덩어리를 레퍼런스의
큰 직사각 판석, 직선형이되 깨진 이음, 부분 파손 모서리로 교체했다. 중앙 65%는 저대비 판석 면,
마젠타 광물과 잔석은 외곽 및 일부 파손 이음에 집중했다.

- 생성 보존본: Codex ImageGen 출력 `exec-66c87859-13ac-4b9d-9c2a-80b21f94f073.png`
- 현재 단일 원본: `tr01_primarymatch_floor_macro_3x3_source.png`
- SHA-256: `82112FA2625F8E5BA5DE173170ECCB03EEAB55C57581687ED7580E9B14DEE8B2`
- 정렬 채널: Normal `6041792062A511D170A0CBD4DFA02A43C1F85CE5BDC8B6F10AB096DDD78292D3`,
  AO `3BF2A078D1610FFA0C490A8CD646A9D3EC5E81313C7956F1B9DCE3EAD96A7735`,
  Emission `D7A00E78F9B4AB8E41F33B7DAC13C1074B493E169106005DFD26ECB48C00E0EC`
- pass22: 18셀 미러 주기 채택. pass23의 14셀 비교는 주기적 마젠타 띠가 드러나 폐기.
- 제외: 아치, 레일, 독립 대형 광물, 캐릭터, UI.
