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

## 14. 에디터 비점유 리소스 우선 패스 — 레일·결정·설비 전이

사용자가 Unity Editor에서 별도 작업을 계속할 수 있도록 Editor, MCP, Play Mode, `Assets/` 임포트를
전혀 사용하지 않고 작업용 원본만 생성했다. 공통 입력은 최상위 레퍼런스와 현재 채택된 바닥,
벽 설비 또는 벽–바닥 접점 시트다. 세 결과 모두 1254×1254 24-bit RGB 체크 배경으로 생성되어,
`extract-generated-checker-alpha.py --grid-cols 3 --grid-rows 3`으로 외곽 연결 저채도 배경만 제거하고
각 셀의 가장 큰 연결 성분을 보존했다. 최종본은 32-bit RGBA, 모서리 알파 0, 9개 셀 전경 존재를
확인했다. 아직 Unity 조립 화면을 보지 않았으므로 모두 `working candidate`이며 승인본이 아니다.

최종 프롬프트 세트:

1. `floor rail transitions`: 바닥에 얹힌 독립 소품이 아니라 깨진 직사각 석판, 매립 침목,
   볼트·강철 받침과 광물 균열에 결합된 3×3 레일 키트. 위 행은 좌–중–우 직선, 가운데는 두 회전과
   보강 교차, 아래는 파손 좌–간극–파손 우. 첫 결과가 발광 배관처럼 읽혀, 동일 배치와 접점을
   보존하면서 두 줄의 노출 강철 주행면·목재 침목·자갈 중심으로 한 번 수정해 최종 후보로 삼았다.
2. `crystal outcrops`: 균열·잔석 포켓·석판 모서리에서 자라는 지형 일체형 결정 3×3. 낮은 경계형,
   중형 벽발·중앙 군집, 채굴 후 흔적을 행별로 구성. 거대 독립 결정과 아이템 아이콘 형태 제외.
3. `equipment transitions`: 레일·배관·기둥이 암반에 들어가는 종료·코너·소켓·파손 램프 3×3.
   묻힌 강판, 끊어진 클램프, 캡이 씌워진 케이블과 석판 파손을 한 접점으로 묶고 독립 소품 제외.

- `tr01_primarymatch_floor_rail_transitions_3x3_source.png`
  SHA-256 `CD989E85CE44C86CB2FED4FA1C277DFD3E17227464194E2FBB2956837364EAA6`
- `tr01_primarymatch_crystal_outcrops_3x3_source.png`
  SHA-256 `1C9935D50C2AC582E84BE5BC276BEEF77D973B9C72742B23F1252E64219C11F8`
- `tr01_primarymatch_equipment_transitions_3x3_source.png`
  SHA-256 `8EAD9207EBDB0C8BCBF371CBA7A40CB02FA17B2ABB135881E3169791B77B1CC3`

다음 단계는 Unity 임포트가 아니라 418×418 셀별 오프라인 합성 보드다. 여기서 기존 바닥과의 색,
레일 게이지, 결정 높이, 파손 접점을 선별한 뒤에만 Unity 연결 계약을 확정한다.

## 15. 볼드 영웅 프랍 4종

사용자의 “크고 볼드하며 기능이 즉시 읽히는, 적당히 데포르메된 프랍” 지시를 반영했다. 특정 게임
자산은 복제하지 않고 스타일라이즈드 히어로 슈터의 큰 주형태·과장된 기능 부품·넓은 베벨 원칙만
적용했다. 2×2 각 셀에 암반과 결합된 광석 분쇄기, 원형 환기 터빈, 코일 발전기, 레일 적재소를
배치했다. 작은 디테일보다 롤러·원형 팬·중앙 코일·호퍼가 먼저 읽히도록 요청했다.

- 원본: `tr01_primarymatch_hero_machinery_2x2_source.png`
- SHA-256: `ADA3FA46A6A94D037A94C14D073FC91A905B9506B1D40F9FD03ADD02B204AAED`
- 계약 후보: 627×627 4셀, 실제 알파, 런타임 미연결

## 16. 전경·높이 레이어 4종

최상위 이미지의 디오라마 깊이를 재현하기 위해 좌우에서 들어오는 두꺼운 암반 선반, 하단 절벽 림,
한 셀 높이의 서비스 플랫폼을 생성했다. 어두운 하부 면, 넓은 정면, 굵은 강철 버팀대를 강조하고
작은 자유 배치 장식은 제외했다. 전경 가림을 실제로 시험할 수 있도록 각 조각을 큰 실루엣으로 유지했다.

- 원본: `tr01_primarymatch_foreground_depth_2x2_source.png`
- SHA-256: `FBBAFE7A65AF8764F7717A10BF3DFB0BD1B7C1738F3174586BE44AB2E12D93A8`
- 계약 후보: 627×627 4셀, 실제 알파, 런타임 미연결

## 17. 대형 벽 백드롭 4종

평평한 상단 벽면을 기능 형태로 나누기 위해 봉인 벌크헤드, 굵은 3연 파이프 매니폴드, 결정 가공기,
붕괴한 벽 설비를 제작했다. 원형·수직·대각·공동의 네 주형태가 서로 구분되며, 모두 공통 벽발과
암반 접점을 가진다. 벌크헤드는 열린 문이나 아치가 아닌 닫힌 기계 면으로 제한했다.

- 원본: `tr01_primarymatch_monumental_wall_modules_2x2_source.png`
- SHA-256: `D10490A6DB7B365F04362E265ACB90B816B228F61DF09FC6879E0A8FB2602570`
- 계약 후보: 627×627 4셀, 실제 알파, 런타임 미연결

## 18. 디오라마 누적 합성 01~05

`tools/art/compose-primary-match-diorama.py`를 추가했다. 이는 Unity를 점유하지 않고 현재 Game View
또는 순수 바닥·벽 매크로 위에 후보 시트를 합성한다. 각 단계 이미지를 덮어쓰지 않고 별도 과정
파일로 남긴다.

- 과정 01: 레일·결정·파손 전이의 첫 공간 구획
- 과정 02: 영웅 프랍 4종을 넣은 볼드 형태 상한
- 과정 03: 전경 선반·절벽·플랫폼의 가림과 높이 가설
- 과정 04: 벽 백드롭까지 모두 넣은 의도적 밀도 상한
- 과정 05: UI 없는 새 바닥·벽 베이스에서 큰 프랍 수를 줄이고 중앙 여백을 남긴 권장 디오라마
- 과정 06: 과정 05의 배치를 보존하고 세 색상 기능부만 제한적으로 번지는 재질·조명 가설

과정 04는 과밀 비교본이며 채택 구도가 아니다. 과정 05는 벌크헤드·파이프·붕괴 설비를 배경층,
분쇄기·적재소·레일을 중경, 결정·전경 선반·플랫폼을 전경으로 나눴다. 기존 수호자 드론은 크기
비교용으로만 중앙에 합성했다. 오프라인 그림자와 색광은 공간 가설이며 Unity 검증을 대신하지 않는다.

## 19. 신규 6세트 기술 후보 채널

확정 마스크 규약 `R=금속`, `G=광택`, `B=습윤·결정`, `A=효과 강도`에 맞춰 6개 원본 각각의
Normal/AO/Emission/Mask 4채널, 총 24장을 생성했다. 투명 검정이 노멀 경사를 잡아당기지 않도록
알파 가중 블러로 높이장을 만들고, AO는 절대 암도가 아니라 주변 대비 공동만 사용한다.

첫 색상 분류는 보라 암반까지 B 채널로 넓게 잡아 폐기했다. 최종 후보는 높은 색 분리도, 높은 명도,
주변보다 밝은 국소 대비를 동시에 요구한다. 최종 B>32 점유율은 결정 45.4%, 설비 전이 7.1%,
레일 10.1%, 전경 23.5%, 영웅 프랍 11.4%, 벽 백드롭 9.1%다. 대표 영웅 프랍의 Normal/AO와
축소된 Emission/Mask를 직접 확인했다.

- 생성기: `tools/art/bake-primary-match-resource-channels.py`
- 결과: `channels/resource-first/` 24 PNG
- 해시 보고서: `channels/resource-first/report.json`
- 상태: `derived_working_candidate_requires_artist_paintover`
- Unity 임포트·Light2D·셰이더 반응: 미검증

과정 06은 최종 기술 후보의 좁아진 발광 범위를 오프라인으로 확인하기 위해 만들었다. 마젠타·시안·
앰버 고명도 픽셀만 두 단계 블러로 합성했으며, 프랍 위치·크기·중앙 여백은 과정 05와 동일하다.
이는 실제 URP Bloom이나 Light2D 결과를 주장하지 않는 비교 가설이다.

## 20. 최상위 이미지 대비 오프라인 분포 확인

서로 다른 구도이므로 픽셀 유사도 대신 각 이미지 중앙 80%의 명도·채도 분포를 비교했다.

| 이미지 | 명도 <0.08 | 명도 0.08–0.35 | 명도 중앙값 | 채도 중앙값 |
|---|---:|---:|---:|---:|
| 최상위 이미지 | 13.12% | 76.64% | 0.1725 | 0.6568 |
| 과정 05 | 15.37% | 76.37% | 0.1578 | 0.5571 |
| 과정 06 | 15.59% | 76.04% | 0.1559 | 0.6078 |

과정 06은 최상위 이미지보다 약간 어둡고 덜 채도가 높지만 중간톤 비중은 0.60%p 안에 있다.
오프라인 프리뷰 단계에서는 과노출보다 안전한 방향이며, Unity 연결 뒤에는 전체 노출을 다시 올리지
않고 큰 프랍의 국소 광원 반경과 표면별 최소광만 조정한다.

## 21. 런타임 발점·점유·가림 후보 계약

Unity Editor를 점유하지 않고 `SetPieceCatalog`, `SetPieceSpawner`, `ForegroundOccluder`의 실제 입력
계약만 읽어 신규 39셀의 런타임 후보 메타데이터를 만들었다. 각 셀의 알파 16 이상 실제 경계를 측정하고,
가장 낮은 불투명 띠의 X 중심과 알파 경계 하단을 비트림 스프라이트 피벗으로 기록했다. 따라서 중앙
피벗이 맞는 대형 기계와, 지지부가 좌우로 치우친 전경 선반을 같은 규칙으로 잘못 고정하지 않는다.

- 분석기: `tools/art/analyze-primary-match-runtime-candidates.py`
- 기계 판독 결과: `runtime-catalog-candidates.json` — 6시트, 39변형
- 시각 검토: `diorama-process-07-runtime-footpoint-review.png` — 청록 알파 경계, 앰버 발점
- 런타임 입력 후보: 점유 셀, 시각 높이, Sorting Layer, 접촉 그림자 반지름·윤곽, 전경 페이드 그룹,
  파괴 후 대체 ID
- 보류: 실제 PPU·투영별 피벗, 충돌, 전경 겹침, Light2D, 서비스 플랫폼 보행 규칙
- 상태: 모든 변형 `candidate_not_unity_verified`, Unity `Assets/` 미복사·미임포트

`verify-primary-match-resource-first.py`가 이제 원본·24개 채널·35개 과정 이미지뿐 아니라 런타임
카탈로그의 소스 해시, 39개 고유 ID, 피벗 범위, 지원 Sorting Layer, 그림자 윤곽 짝수 좌표까지 검사한다.

## 22. 128 PPU 개별 납품 후보

시트 상태로는 기존 승인 임포터가 자산별 `Sprite`와 채널 묶음을 직접 만들기 어렵기 때문에, Unity를
열기 전에 39개 셀을 실제 임포트 단위로 정규화했다. 바닥 오버레이는 셀 전체를 128×128로 보존하고,
솟은 자산은 측정 발점과 후보 점유 폭·시각 높이를 사용해 비트림 캔버스에 등비 축소했다. 큰 형태의
화면 비중을 보존하기 위해 영웅 프랍은 2셀, 전경 선반 3셀, 절벽 립 4셀, 백드롭 3셀 폭을 유지한다.

- 생성기: `tools/art/build-primary-match-working-delivery.py`
- 결과: `delivery-candidates/` — Albedo 39 + Normal 39 + AO 39 + Emission 39 + Mask 39
- 총 파일: PNG 195장, 약 9.55 MB + `manifest.json`
- 과정 08: 개별 캔버스·상대 크기·발점 일괄 검토
- 채널 정렬: 다섯 채널 동일 크기·동일 변환, Normal/AO는 Albedo 실루엣 알파와 동일
- 강도 채널: Emission/Mask 알파는 Albedo 실루엣 안으로 클램프
- 상태: `working_candidate_not_approved`, Unity 미복사·미임포트

최종 오프라인 검증은 원본 6장, 점유 셀 39개, 과정 이미지 35장, 시트 기술 채널 24장, 개별 납품
채널 195장과 배치·런타임 계약을 모두 통과했다.

## 23. 권장 디오라마 배치의 데이터화

과정 05의 배치가 합성 스크립트의 하드코딩으로만 남으면 Unity 연결 때 구도가 다시 흐트러질 수 있어
`curated-diorama-layout.json`으로 분리했다. 총 18개 항목이 원본 시트·셀, 런타임 후보 ID, 배경/중경/
지면/전경 층, 화면 위치·폭, 토폴로지 앵커 의미와 오프라인 그림자 값을 가진다. 배경 3·영웅 기계 2·
레일 경로 1·결정 3·전경 오클루더 2라는 화면 예산도 같은 파일에 고정했다.

`compose-primary-match-diorama.py`의 과정 05/06은 이제 이 JSON을 직접 읽는다. 전환 전후 재생성 결과:

- 과정 05 SHA-256: `42F11243F7A58CE56B361F7B6224702BA6023879EEEC4E260317BBD2C7A8B813` — 동일
- 과정 06 SHA-256: `078C5BC6440579F08384BA596E8E285E7F5945DFDA7A71657B18C229905C36F2` — 동일

화면 좌표를 임의의 월드 좌표로 단정하지 않았다. Unity 연결 시 현재 방의 실제 북쪽 벽발, 동서 서비스
앵커, 남쪽 전경 경계와 레일 연결 가능 셀을 읽은 뒤 대응해야 한다. 검증기는 18개 고유 ID, 소스 존재,
39개 런타임 후보 참조, 예외인 기존 조명 기둥·수호자 드론, 중앙 45% 여백과 밀도 예산을 검사한다.

## 24. 기능 의미 기반 국소 광원 소켓

개별 납품 Emission에서 알파 72 이상의 연결 군집을 찾고 발점 기준 셀 좌표로 변환했다. 최초 자동
선택은 분쇄기·벌크헤드·적재소의 기능등보다 주변 마젠타 광석을 에너지 순으로 먼저 골라 폐기했다.
최종 분석기는 자산별 의미 앵커를 먼저 선언하고 같은 색의 실제 Emission 군집 중 가까운 위치로
스냅한다. 모든 스냅 거리는 0.211셀 이하라 눈으로 지정한 기능부와 픽셀 군집이 일치한다.

- 분쇄기: 호퍼 광물광 1
- 환기 터빈: 중앙 시안 허브 작업등 1
- 전력 릴레이: 중앙 마젠타 코어 + 시안 표시관 2
- 적재소: 우측 앰버 작업등 1
- 벌크헤드: 중앙 시안 패널 1
- 파이프 매니폴드: 마젠타 관 2
- 결정 가공기: 대형 결정 코어 + 제어 패널 2
- 붕괴 설비: 낮은 잔류 광물광 1

총 8개 자산 11개이며 `light-socket-candidates.json`과 과정 09 보드에 기록했다. Worklamp 반경은
최대 2.15셀·세기 0.62, MineralGlow는 최대 2.3셀·0.66, Indicator는 최대 1.25셀·0.32로 제한했다.
지면 오버레이와 분산 결정 시트는 Emission만 사용하고 Light2D 소켓을 만들지 않는다. 실제 조명 반응,
그림자 예산과 깜빡임은 Unity 미검증이다.

## 25. 하단 실루엣 그림자 윤곽

기존 사각형 `shadowContourCells` 후보를 그대로 쓰면 새 프랍도 네모 그림자로 돌아갈 수 있어,
`SpriteAlphaContour`와 같은 하단 띠 스캔을 오프라인으로 재현했다. 스프라이트 전체 외곽이 아니라
발점 아래 0.15셀부터 점유 깊이만 보며, 알파 0.35 이상인 각 높이 띠의 좌우 끝을 잇고 직선상 중복점을
0.05셀 허용치로 줄인다. 점유 폭·깊이에는 +0.35셀 가드를 적용한다.

- 생성기: `tools/art/build-primary-match-shadow-contours.py`
- 결과: `shadow-contour-candidates.json`
- 범위: 중형 결정 3 + 영웅 기계 4 + 전경·플랫폼 4 + 백드롭 4 = 15종
- 복잡도: 최소 5점, 최대 12점 — 단순 4점 사각형 없음
- 과정 10: 원본 위에 반투명 청록 윤곽과 발점 표시
- 상태: `candidate_not_unity_verified`

과정 10에서 좌우 결정의 비대칭, 전경 선반의 안쪽 파임, 기계의 좁아지는 밑동이 보존된다. 백드롭은
벽발이 넓어 상대적으로 사각형에 가깝지만 하단 폭 변화가 남는다. 최종 투사 모양은 Light2D 방향과
투영 프리셋에 따라 달라지므로 Unity 검증 전 승인하지 않는다.

## 26. 재현 가능한 오프라인 검증 보고서

`verify-primary-match-resource-first.py`가 성공 시 `offline-verification-report.json`을 만든다. 과정 이미지
35장과 manifest·배치·런타임·납품·레이아웃·방 정체성·광원·그림자·채널 보고서의 SHA-256, 개수, 남은 Unity
게이트를 기록한다. 연속 두 번 실행한 보고서 해시는
`B14822A76E2CF10BE16F15949AB1213ED7072D9A8C20C2CEFFE9A0C8ED1A80D6`로 동일했다.

검증 상태는 `verified_offline_resource_first`이며 `runtimeIntegrationVerified=false`,
`unityImportPerformed=false`다. 이는 리소스 준비 완료를 증명하지만 본편 화면 완료를 주장하지 않는다.

## 27. 휴면 Unity 임포트 계획

여러 오프라인 계약을 Unity 연결 시 다시 손으로 합치지 않도록
`build-primary-match-unity-import-plan.py`를 추가했다. 결과는 `AgentScripts`의 JSON이며 `Assets` 밖이라
Editor 임포트를 일으키지 않는다.

- 전체 후보 39개·채널 파일 195장
- 과정 05 사용 자산 15개
- `SetPieceCatalog` 후보 38개(비차단 지면 오버레이 포함)
- 지면 오버레이 24개
- 보행·높이 규칙 보류 플랫폼 1개
- 병합된 광원 11개·그림자 윤곽 15개

`stage-primary-match-bold-candidates.ps1`는 모든 소스 해시와 대상 경로가 후보 전용 폴더 안인지 검사한다.
기본 실행은 dry-run이고 실제 복사는 명시적 `-Apply`가 있어야 한다. dry-run에서 39개·195파일을 확인했고
복사는 0건이었다. `ImportPrimaryMatchBoldCandidates.cs`는 스테이지 영수증이 없으면 즉시 중단하며,
실행되더라도 후보 전용 `SetPieceCatalog_PrimaryMatchBoldCandidate`만 만들고 활성 Spawner에는 연결하지 않는다.
`VerifyPrimaryMatchBoldCandidates.cs`는 195개 임포터, 39개 피벗, 카탈로그 38개, 광원 11개, 윤곽 14개와
활성 런타임 할당 0개를 검사하도록 준비했다. 두 Unity 스크립트는 아직 실행하지 않았다.

## 28. 동일 키트의 방 정체성 3종

단일 권장 디오라마만으로는 자산이 실제 맵에서 반복 장식처럼 보일 가능성을 판단하기 어려워,
`curated-room-variants.json`에 세 가지 절제된 조합을 별도 정의했다.

- 광석 반입: 붕괴 설비 + 대형 분쇄기 + 단일 레일 경로
- 환기 설비: 봉인 벌크헤드 + 대형 환기 팬 + 시안 서비스 기둥
- 수정 동력: 결정 가공기 + 전력 릴레이 + 제한된 결정 군집

각 방은 영웅 기계 1, 대형 백드롭 1, 전경 오클루더 1만 사용하고 중앙 가로 폭 45% 이상을 비운다.
`compose-primary-match-room-variants.py`가 과정 11의 1920×1080 삼분할 보드를 만들며, 검증기는 3개
고유 방 ID, 원본 시트·셀 범위, 역할별 정확한 밀도와 아치 제외 규칙을 검사한다. Unity·`Assets/`·
Scene·Prefab에는 접근하지 않았다.

## 29. 16:9 방별 디오라마 3종

삼분할 세로 보드는 실루엣 비교에는 유효하지만 실제 플레이 화면의 수평 공간감을 증명하지 못한다.
그래서 `curated-room-landscape-layouts.json`에 1920×1080 전용 배치를 별도로 만들고 과정 12~14를
생성했다. 세 화면 모두 대형 백드롭과 영웅 기계는 한 개씩만 쓰며, 전경 오클루더 한 개를 화면
가장자리로 밀고 단일 기능축을 유지한다.

- 과정 12 광석 반입: 좌측 분쇄기에서 우측 레일로 흐르는 수평 동선
- 과정 13 환기 설비: 상단 중앙의 원형 기계군과 하단의 넓은 무장애 바닥
- 과정 14 수정 동력: 좌측 릴레이·상단 가공기·우측 결정 군집의 삼각 기능축

각 방은 폭 45% 이상의 `openFloorRectPixels`를 계약에 포함한다. 검증기는 지면 장식은 허용하되
중경·백드롭·전경 알파가 이 구역을 가리는 비율을 계산하며, 세 방 모두 0%로 통과했다. 합성기와
검증기는 `art-production/`만 사용했고 Unity Editor와 `Assets/`는 사용하지 않았다.

## 30. 토폴로지 앵커·보호 바닥 인계 보드

16:9 화면 좌표가 실수로 런타임 좌표처럼 사용되지 않도록 방마다 `runtimeAnchors`를 추가했다.
백드롭은 북쪽 벽면, 영웅 기계는 벽발 또는 서비스 앵커, 전경은 화면 남측 경계, 레일·서비스 장치는
기능 연결점으로 해석한다. 과정 15는 세 원본 화면을 축소해 청록 보호 바닥과 앵커 이름을 함께 표시한다.

검증기는 모든 방에 backdrop·hero·foreground·openFloor 앵커가 있는지, 화면 좌표가 아닌 의미 이름인지,
보호 바닥 폭과 실제 알파 차단율이 계약을 지키는지 확인한다. 이 보드는 향후 현재 방 토폴로지를 읽은
뒤 배치할 때의 인계 자료이며, Unity에서 자동 적용된 상태는 아니다.

## 31. 휴면 임포트 계획 V2의 방 블루프린트 병합

과정 12~15의 방 계약이 별도 문서로만 남아 임포트 뒤 누락되지 않도록
`build-primary-match-unity-import-plan.py`가 런타임 카탈로그와 16:9 방 계약을 함께 읽도록 확장했다.
V2 계획은 각 시트·셀을 실제 개별 납품 ID로 해석하고, 서비스 기둥처럼 이미 존재하는 참조는 별도로
표시한다.

- 방 블루프린트 3개, 총 배치 참조 28개
- 이번 후보 묶음에서 임포트될 참조 24개
- 기존 서비스 기둥 참조 4개
- 모든 비지면 프랍에 `mustAvoidOpenFloor=true`
- 모든 방 상태 `candidate_requires_current_topology_resolution`

갱신된 계획으로 스테이징 스크립트를 다시 dry-run해 39자산·195파일을 확인했고 복사는 0건이었다.
stage receipt와 후보 `Assets` 폴더가 모두 없는 것도 재확인했다. 따라서 이 단계는 Editor 임포트나
활성 런타임 배치를 수행하지 않았다.

## 32. 읽기 전용 Unity 토폴로지 프리플라이트 준비

실제 방 배치 직전에 현재 월드 구조를 근거로 앵커를 선택할 수 있도록
`AgentScripts/InspectPrimaryMatchRoomTopology.cs`를 준비했다. 실행 시 `RunBootstrap.Sim.World`를 읽어
입구와 연결된 빈 셀만 BFS로 추리고 다음 후보를 보고한다.

- 북쪽 벽발, 서쪽 벽발, 동쪽 벽발, 남측 전경 경계 후보와 각 개수
- 연결 영역 안의 최대 축정렬 개방 사각형과 월드 폭 대비 비율
- 현재 깊이·격자 크기·월드 버전 전후값
- 휴면 방 블루프린트 3개의 존재 여부

출력은 `AgentScripts/primary-match-room-topology-preflight.json` 하나이며 `Assets/` 밖이다. 정적 검증은
Spawn·Clear·Damage·ForceClear·SetTile·Scene/Prefab 저장·Asset 생성 토큰을 금지한다. 현재는 사용자가
Editor를 쓰는 중이므로 실행하지 않았고, 출력 파일이 없는 것을 확인했다.

## 33. 맵 생성 회귀 픽스처 배치 시뮬레이션

오프라인 디오라마의 직사각형 방에서만 구도가 성립하는 편향을 확인하기 위해, 본편
`Assets/Tests/EditMode/Fixtures`의 깊이 1~3 맵 생성 JSON을 읽어 모든 18×12 로컬 화면을 전수
탐색했다. 빈 셀은 입구와 연결된 성분으로 제한하고 각 방에 9×3 이상 개방 사각형, 북쪽 벽발 연속
3칸 이상, 남측 경계 2개 이상과 방별 동서 앵커 조건을 적용했다.

- 깊이 1 광석 반입: 최대 개방 9×5, 화면 폭 50.0%
- 깊이 2 환기 설비: 최대 개방 12×5, 화면 폭 66.7%
- 깊이 3 수정 동력: 최대 개방 10×3, 화면 폭 55.6%
- 전체 교차 평가: 방 3종 × 픽스처 3개 = 9/9 통과

결과는 `fixture-placement-simulation.json`, 시각 검토는 과정 16에 남겼다. 휴면 임포트 계획도 이
보고서 해시와 9/9 통과를 요구하므로 실패한 지형 계약을 무시하고 임포트 계획을 다시 만들 수 없다.
이 검사는 고정 회귀 픽스처의 구조적 가능성을 보이는 것이며 실제 현재 시드의 Unity 배치를 대신하지 않는다.

## 34. 대형 프랍 실제 점유 크기 비중첩 검사

과정 16은 앵커와 개방 폭만 확인했기 때문에 큰 프랍 세 개가 동시에 들어갈 때의 충돌을 증명하지 못했다.
`simulate-primary-match-major-footprints-on-fixtures.py`는 런타임 후보의 실제 `footprintCells`를 읽고,
각 18×12 화면 안에서 백드롭·영웅 기계·전경 프랍을 방향별 벽발에 이산 배치한다.

- 광석 반입: 붕괴 설비 3×2 + 분쇄기 2×2 + 우측 전경 선반 3×2
- 환기 설비: 봉인 벌크헤드 3×1 + 환기 터빈 2×1 + 좌측 전경 선반 3×2
- 수정 동력: 결정 가공기 3×1 + 전력 릴레이 2×2 + 우측 전경 선반 3×2

각 점유 셀은 연결된 빈 공간 안에 있고, 청록 보호 바닥과 겹치지 않으며, 세 프랍끼리도 중복되지 않아야
한다. 깊이 1~3 × 방 3종의 9개 평가가 모두 통과했다. `fixture-major-footprint-simulation.json`과 과정
17이 근거이며 휴면 임포트 계획도 이 9/9 결과를 필수로 요구한다.

## 35. 전체 비바닥 청사진 점유 검사

대형 프랍 세 개만 통과해도 서비스 기둥이나 중형 결정이 같은 벽발을 차지할 수 있으므로,
`simulate-primary-match-full-blueprint-footprints-on-fixtures.py`가 각 방의 모든 비바닥 배치를 함께
해결하도록 확장했다. 지면 레일·전이·흔적은 충돌을 만들지 않는 오버레이로 분리하고, 광석 반입실의
레일 3셀은 보호 바닥 안에서 연속 경로를 이루게 했다.

- 광석 반입: 비바닥 6개·19셀, 지면 오버레이 4개
- 환기 설비: 비바닥 6개·14셀, 지면 오버레이 3개
- 수정 동력: 비바닥 7개·17셀, 지면 오버레이 2개
- 전체 교차 평가: 깊이 1~3 × 방 3종 = 9/9 통과

모든 비바닥 셀은 서로 겹치지 않고 보호 개방 사각형을 침범하지 않는다. 결과 계약은
`fixture-full-blueprint-footprint-simulation.json`, 시각 근거는 과정 18이다. 휴면 임포트 계획은
이 계약의 해시와 9/9 결과도 요구하며, 검사는 계속 Unity 미실행 상태다.

## 36. 최상위 이미지 직접 비교 보드

리소스 조합이 독립적으로 보기 좋은 데서 멈추지 않도록 최상위 이미지와 과정 12~14를 동일한 16:9
패널로 묶었다. `compose-primary-match-reference-comparison.py`는 각 이미지 중앙 80%에서 명도 중앙값,
채도 중앙값, 0.08 미만 암부, 0.08~0.35 중간톤과 마젠타·시안·앰버 강조색 비율을 측정한다.

세 방의 명도 중앙값은 0.1455~0.1597, 채도 중앙값은 0.5432~0.6000, 중간톤 비율은
84.81~86.48%다. 최상위 이미지의 0.1759·0.6528·79.84%보다 약간 어둡고 절제되어 있지만,
검증 계약의 톤 범위 안에 있다. 광석 반입실의 마젠타 비중이 높고 세 방 모두 시안·앰버 면적은
레퍼런스보다 작다는 점도 수치로 남겼다. 이는 캐릭터 공격광·런타임 Light2D·후처리가 빠진 오프라인
조합의 의도된 미검증 차이이며, Unity 연결 시 우선 확인할 항목이다.

과정 19가 시각 보드, `reference-composition-comparison.json`이 기계 판독 결과다. 원본과 기존
디오라마는 수정하지 않았고 Unity Editor·`Assets/`도 사용하지 않았다.

## 37. 방별 조명·강조색 캘리브레이션 V1 → V2

과정 19에서 확인한 낮은 시안·앰버 비중을 런타임 연결 전에 시각화하기 위해 방마다 넓은 시안 분리광,
작은 앰버 보조광과 한 줄의 약한 방향광을 합성했다. 과정 20~22의 V1은 공간 분리는 좋아졌지만 명도
중앙값이 0.192~0.209로 최상위 이미지 0.1759를 넘어 배경이 씻겨 보였다. 과정 23에 원본과 함께 남겨
폐기 근거로 보존했다.

V2는 광원 알파와 범위를 줄이고 마지막 색 농도를 회복했다.

- 광석 반입: 명도 0.179, 채도 0.537, 마젠타 30.39%
- 환기 설비: 명도 0.184, 채도 0.576, 시안 8.10%
- 수정 동력: 명도 0.176, 채도 0.579, 마젠타 39.44%

세 방 모두 명도 중앙값 0.17~0.19, 채도 0.52~0.62, 중간톤 80% 이상 계약을 통과했다. V2 방별
이미지는 과정 24~26, 비교 보드는 과정 27, 기계 판독 값과 광원 형상은
`room-lighting-calibration-candidates-v2.json`에 있다. 이는 Light2D 최종값이 아닌 오프라인 가설이며,
원본 과정 12~14와 V1은 덮어쓰지 않았다. Unity·`Assets/`·Play Mode는 사용하지 않았다.

## 38. 전경 실루엣 페이드 마스크 3종

기존 승인 패키지와 `ApprovedArtValidator`를 확인한 결과, 현재 `fadeMaskPath` 계약은 선택적 상부
그라데이션이 아니라 RGB 흰색과 Albedo와 동일한 알파를 가진 실루엣 마스크다. 또한 현 런타임
`ForegroundOccluder`는 마스크를 아직 소비하지 않고 스프라이트 전체 알파를 0.34까지 내린다.

이 계약에 맞춰 좌측 전경 선반·우측 전경 선반·하단 절벽 립의 마스크 3장을
`delivery-candidates/fade/`에 만들었다. 크기는 각각 416×343, 416×343, 544×198이며 모든 픽셀에서
Albedo 알파와 정확히 일치하고 RGB는 255다. `foreground-fade-mask-candidates.json`에 원본·마스크
해시, 오클루더 그룹과 목표 알파를 기록했고 과정 28에서 Albedo·마스크·균일 페이드 결과를 비교한다.

휴면 임포트 계획은 세 마스크를 각 전경 후보의 `fadeMaskCandidate`로 병합하지만, 현 런타임 소비자가
없으므로 195개 기본 채널 스테이징에는 넣지 않는다. 선택 페이드 셰이더 또는 런타임 계약이 추가되기
전에는 활성화하지 않는 안전 게이트를 유지한다. Unity Editor와 `Assets/`는 사용하지 않았다.

## 39. 레일·설비 전환 연결 포트 27개

큰 프랍과 레일이 시각적으로 가까워도 논리 연결점이 없으면 절차 배치에서 허공에 끊길 수 있다.
`build-primary-match-connection-ports.py`는 레일 전환 9종과 설비 전환 9종에 128px 셀 경계 포트를
부여했다.

- 레일: `rail_pair` 16개
- 설비 전환: `heavy_pipe`, `power_bundle`, `equipment_socket`, `dual_service` 합계 11개
- 전체: 18자산·27포트
- `broken_gap`, `capped_machine_scar`: 외부 포트는 있으나 내부 연속성은 `broken`

과정 29는 두 3×3 시트 위에 포트 방향과 매체 색을 표시한다. 기본 수평 레일 좌·중·우는
`east → west/east → west`로 정확히 이어지고, 코너·십자·종단은 이름과 실루엣에 맞는 방향만 가진다.
`connection-port-candidates.json`에는 픽셀·정규화 좌표와 토폴로지 확인 게이트가 있다. 휴면 임포트
계획은 각 납품 자산에 포트를 병합하지만 현재 후보 임포터는 소비하지 않으며, 실제 방의 호환 반대편
포트가 확인되기 전 자동 배치하지 않는다. Unity와 `Assets/`는 사용하지 않았다.

## 40. 광석 반입실 연결 레일 V2 채택

포트 자체는 맞았지만 과정 12의 레일은 분쇄기와 떨어진 설비 흉터 뒤에서 시작해 장식물처럼 읽혔다.
`compose-primary-match-ore-connected-route-v2.py`는 비바닥 배치를 전혀 바꾸지 않고 레일 좌·중·우를
X 500·710·920으로 옮겨 첫 조각이 분쇄기 화면 점유부 아래 155px 들어가게 했다. 분리되어 있던
`capped_machine_scar` 지면 오버레이는 제거했다.

과정 30은 개선된 전체 방이며 과정 31은 V1/V2 직접 비교다. 레일 포트 순서는
`east → west/east → west`, 세 조각 모두 비차단 지면 오버레이이며 보호 바닥과 대형 프랍 계약은
변하지 않는다. 휴면 Unity 방 청사진은 광석 반입실의 프리뷰를 V2로 채택해 총 배치가 28→27,
신규 후보 참조가 24→23으로 정리됐다. 실제 월드 좌표는 여전히 현재 토폴로지 프리플라이트 전까지
결정하지 않는다. Unity Editor와 `Assets/`는 사용하지 않았다.

## 41. 수정 동력실 중량 배관 버스 V2 채택

과정 14의 수정 동력실은 전력 릴레이와 결정 가공기 사이에 작은 바닥 장식 두 개가 떨어져 있어 기능
관계가 약했다. `compose-primary-match-crystal-connected-bus-v2.py`는 이 두 장식을 제거하고 설비 전환
상단 좌·중·우 3조각을 X 520·720·920에 배치해 하나의 굵은 중량 배관 축을 만들었다.

배관은 전력 릴레이 화면 점유부와 80px, 결정 가공기 점유부와 495px 겹친다. 포트 순서는
`east → west/east → west`, 매체는 모두 `heavy_pipe`이며 비차단 지면 오버레이다. 비바닥 프랍과
보호 바닥은 변경하지 않았다. 과정 32가 V2 전체 방, 과정 33이 V1/V2 비교이며
`crystal-power-connected-bus-v2.json`이 기계 판독 계약이다.

휴면 방 청사진도 `west_relay_to_north_processor_connection` 의미 앵커와 함께 V2를 채택했다. 광석
반입실에서 1개를 줄이고 수정 동력실에서 1개를 늘려 전체 배치는 다시 28개, 신규 후보 참조는 24개다.
실제 월드 좌표는 현재 토폴로지와 호환 포트를 읽기 전까지 결정하지 않는다. Unity와 `Assets/`는
사용하지 않았다.

## 42. 환기 설비실 서비스 스파인 V2 채택

과정 13의 환기 설비실은 중앙 벌크헤드·터빈의 크기는 충분했지만 좌우 서비스 기둥 아래의 작은 장치가
터빈에 닿지 않아 세 개의 독립 장식처럼 읽혔다. `compose-primary-match-ventilation-connected-spine-v2.py`는
두 장치를 제거하고 설비 전환 상단 좌·중×4·우 6조각을 X 355~1355에 배치해 방 전체를 가로지르는
하나의 중량 배관 스파인을 만들었다.

스파인은 좌우 서비스 기둥과 각각 120px·135px, 중앙 터빈과 510px 겹치며 보호 전투 바닥보다 40px
위에서 끝난다. 포트 순서는 `east → west/east ×4 → west`, 매체는 모두 `heavy_pipe`이고 비차단 지면
오버레이다. 비바닥 프랍과 보호 바닥은 변경하지 않았다. 과정 34가 V2 전체 방, 과정 35가 V1/V2 비교,
`ventilation-connected-spine-v2.json`이 기계 판독 계약이다.

휴면 방 청사진은 `west_service_through_turbine_to_east_service_connection` 의미 앵커와 함께 V2를
채택했다. 전체 배치는 32개, 신규 후보 참조는 28개, 기존 서비스 기둥 참조는 4개다. 이 기록 시점에는
Unity Editor·`Assets/`를 사용하지 않았으며, 이후 명시적 본선 적용 단계에서 현재 토폴로지를 해석한다.

## 43. 본선 Unity 적용과 최종 검증

사용자 요청에 따라 리소스 우선 단계를 종료하고 V2 전체를 TunnelCrew 본선에 적용했다. 스테이징은
39개 자산·195개 정렬 채널을 후보 전용 폴더로 복사했고, Unity 임포터가 스프라이트 39개·안전한
카탈로그 항목 38개·광원 소켓 11개·비사각 그림자 윤곽 14개를 검증했다. 보행 판정이 없는 서비스
플랫폼 1개는 파일만 임포트하고 런타임 카탈로그에서는 계속 게이트했다.

읽기 전용 토폴로지 프리플라이트는 깊이 1의 80×72 월드에서 입구 연결 빈 셀 1,834개와 최대 개방
사각형 25×7을 찾았고 월드 버전은 `[0, 0]`으로 유지됐다. 기존 카탈로그 27개와 신규 38개를 합친
`SetPieceCatalog_PrimaryMatchRuntime` 65개 항목을 만들고, `PrimaryMatchRoomDecorator`가 연결 영역의
16×7 구간에 깊이별 구도를 재현하도록 `RunBootstrap`에 연결했다. 광석 반입 9개, 환기 설비 12개,
수정 동력 9개가 Play Mode에서 생성됐다.

큰 형태가 기존 타일 속에서 작게 읽히지 않도록 백드롭 1.5배, 영웅 기계 1.55배, 전경 1.35배를
적용했고 접촉 그림자·투사 윤곽도 같은 비율로 확대했다. 전경 오클루더 페이드를 활성화했다. 깊이
1~3 Game View를 각각 저장했으며 전체 EditMode 테스트 508/508, Unity Console 오류 0건으로 종료했다.
적용 증거까지 포함한 검증 보고서는 연속 두 번 생성해 SHA-256
`9235082BDD9BA702F446DFF2EE5B34971F0E0646F959C04E1179E075ACB5DA96`로 동일함을 확인했다.
