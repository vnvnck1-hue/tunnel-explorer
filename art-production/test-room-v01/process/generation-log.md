# Test Room V01 Generation Log

생성 방식: built-in `image_gen`  
생성일: 2026-09-08  
공통 레퍼런스: `reference/reference-frame-0025.png`는 깊이층·조명 구조 참고 전용이다.

## TR01-CONCEPT-ROOM-001

- 상태: `concept`
- 결과: `concept/tr01_room_maintenance_a_concept.png`
- 선택 이유: 높은 후면 구조, 넓고 조용한 중앙 바닥, 전경 가림, 청록·앰버·마젠타의 역할 분리가
  명확하다. 테스트 방 전체의 시각 계약으로 사용한다.
- 보정 필요: 개별 게임 자산으로 직접 사용하지 않는다.

```text
Use case: stylized-concept
Asset type: final-quality game environment key art and visual target for a small 2D cooperative mining game test-room diorama
Input image 1: style and depth-structure reference only; use its layered 2.5D staging, rich material separation, strong foreground/midground/background, and colored-light logic. Do not copy its characters, UI, exact room, props, doorway design, composition, or identifiable details.
Primary request: Create an original compact abandoned mine maintenance chamber for “Tunnel Crew”. The room must feel buildable from modular 2D assets and read clearly from a screen-axis-aligned orthographic top-down 3/4 view, with no diamond tile rotation and no perspective convergence.
Scene/backdrop: a small underground maintenance room, dark void beyond the walls, tall rear wall, a massive original industrial arch in the back center opening toward a cyan-lit abyss, a broken heavy mining drill-generator occupying one side, a restrained purple crystal vein in raw rock on the other side, thick columns, pipes, rail fragments, work lamps, rubble and hanging cables. Keep the central walkable floor broad and visually quiet for combat.
Style/medium: polished painterly 2D game art, chunky readable silhouettes, hand-painted surfaces, stylized realism, final shipped-game quality, original design language.
Composition/framing: 16:9 gameplay framing, full room visible as a small diorama; foreground low railing, rocks and hanging cable partially occlude the bottom edge; clear seven-layer depth from black void to atmospheric VFX. No UI and no characters.
Lighting/mood: deep purple-blue ambient darkness; cyan backlight from the arch defining depth; warm amber work lights defining interactive props; subtle magenta mineral glow; strong contact shadows, ambient occlusion, rim light on wall tops, restrained bloom, dust and thin volumetric rays.
Color palette: charcoal, deep plum, cool blue-gray, cyan accents, amber highlights, limited magenta.
Materials/textures: quiet low-frequency worn stone floor, thick rock and riveted steel walls with visible front faces and top caps, oxidized metal, cables, glass lamps, crystalline mineral.
Constraints: original environment only; no characters, enemies, HUD, minimap, text, labels, logos, trademarks, watermark, border, diamond/isometric grid, fisheye, realistic 3D camera perspective, excessive tiny clutter, repeated tile stamps, or baked white studio light. The image is a visual contract for later modular asset production, so preserve clear separable silhouettes and believable cell-scale proportions.
```

## TR01-CONCEPT-KIT-001

- 상태: `concept`
- 결과: `concept/tr01_module_kit_a_layout.png`
- 선택 이유: 벽·아치·기둥·레일·배관·조명·소품·전경 구조를 같은 디자인 언어로 분해했다.
- 보정 필요: 체크무늬가 실제 알파가 아니라 이미지에 구워져 있다. 실루엣과 자산 목록 승인용으로만
  사용하고 슬라이스하지 않는다.

```text
Use case: stylized-concept
Asset type: modular 2D game environment asset-kit concept board for production breakdown
Input image 1: approved visual-target reference for design language, materials, palette, orthographic 3/4 top-down view, and proportions. Preserve its original abandoned mine maintenance-room identity.
Primary request: Extract and redesign the room into a clean modular asset kit board. Show separate, non-overlapping game-asset silhouettes arranged in spacious rows: quiet worn stone floor modules and crack decals; rock-and-riveted-steel wall top caps; matching tall wall front faces; inside and outside corners; one large cyan-backlit industrial arch; two pillar variants; straight, curved, branching and broken rail pieces; pipe and cable modules; three distinct lamps; ten small props such as crates, tools, stones, papers and mineral fragments; one broken heavy drill-generator hero prop; and four foreground occluders including railing, rock lip and hanging cable.
Scene/backdrop: genuinely transparent background, no room scene, no floor plane behind the assets, generous clear spacing between every object.
Style/medium: polished painterly 2D game sprites, chunky readable silhouettes, stylized realism, the same charcoal/plum/cool blue-gray materials with restrained cyan, amber and magenta accents.
Composition/framing: orthographic screen-axis-aligned top-down 3/4 view, no perspective convergence; consistent virtual camera and light direction; show each asset complete with no cropping. The board is a high-resolution source/concept sheet, not a UI.
Lighting/mood: neutralized soft ambient presentation so the Albedo can later be separated; keep only restrained material self-shading and subtle contact definition. Do not bake large cast shadows or dramatic room lighting. Emissive areas should be clearly bounded but not bloom across neighboring assets.
Constraints: actual transparent alpha; no labels, text, numbers, arrows, UI, border, characters, logos, watermark, checkerboard pattern, diamond grid, overlapping objects, cropped objects, repeated identical copies, white backdrop, cast-shadow floor, or perspective camera. Assets must look designed to snap to a 128-pixel-per-cell grid even though no grid is drawn.
```

## TR01-ARC-001

- 최초 생성 상태: `rejected`
- 최초 결과: `source/tr01_arch_gate_a_albedo_source.png`
- 재추출 상태: `working`
- 재추출 결과: `working/tr01_arch_gate_a_background_extraction_attempt.png`
- 선택 이유: 방 키아트의 구조 언어와 일치하고, 아치·기둥·케이블의 실루엣과 중앙 개구부가
  명확하다.
- 보정 필요: 실제 알파는 확보했으나 본체 알파 정규화, 조명 분리, 규격 캔버스, 피벗,
  Albedo/Normal/Emission/Mask/AO 분리가 남아 있다.

최초 생성 프롬프트:

```text
Use case: stylized-concept
Asset type: isolated high-resolution 2D game environment sprite source, asset ID TR01-ARC-001
Input image 1: approved room visual target; preserve the original industrial arch design language and material palette.
Input image 2: modular breakdown reference; use only the large arch as the subject and refine it.
Primary request: Produce one complete original heavy industrial mine arch/gateway sprite. It consists of two thick riveted steel-and-stone vertical supports and one broad segmented curved header, with a clear empty opening in the middle. Include two small inset cyan light housings as separate-looking bounded emissive surfaces and a few restrained hanging cable loops under the header. It must stand convincingly on a ground line and be usable as the rear-wall centerpiece of a 2D room.
Scene/backdrop: genuinely transparent background including inside the doorway opening; no checkerboard, floor, wall, cavern, scenery, glow field, or cast shadow.
Style/medium: polished painterly 2D game sprite, chunky readable silhouette, stylized realism, charcoal/plum/cool blue-gray rock and oxidized dark steel, restrained brass fasteners.
Composition/framing: centered isolated asset, orthographic screen-axis-aligned top-down 3/4 view with no perspective convergence; full object visible; symmetrical overall construction with small hand-painted wear asymmetry; generous transparent padding. Designed footprint 3 cells wide by 1 cell deep, visual height about 3.25 cells, source density 256 pixels per cell.
Lighting/mood: neutral soft ambient presentation with restrained material self-shading and thin cool rim on upward planes. Cyan panels are crisp bounded color regions without bloom. No dramatic directional room light baked into the Albedo source.
Constraints: exactly one arch asset; actual transparent alpha; transparent center opening; no text, labels, UI, characters, rocks blocking the opening, separate loose props, logos, watermark, border, checkerboard, white backdrop, shadow floor, bloom haze, cropped edges, diamond grid, or perspective camera. Preserve clean silhouette and clear ground-contact feet for pivoting.
```

배경 재추출 프롬프트:

```text
Use case: background-extraction
Asset type: production 2D game sprite with true alpha transparency
Input image 1: edit target.
Primary request: Remove only the entire pale checkerboard background and make it genuinely transparent, including the full empty opening inside the arch and all spaces between hanging chains and cables.
Constraints: preserve the arch design, exact silhouette, proportions, colors, painted texture, cyan lamp shapes, cable positions and framing unchanged; do not redraw, restyle, relight, crop, resize, add shadows, add glow, add scenery, add text, or add objects. Keep crisp antialiased edges with no white fringe, gray halo, checkerboard pixels, or background residue. Output a single RGBA sprite on actual transparent alpha.
```

## TR01-FLR-BASE-A~F

- 상태: 5개 채널 `approved`
- 결과: `approved/albedo/floor/tr01_floor_base_[a-f]_albedo.png`
- 원본: `source/floor/tr01_floor_base_[a-f]_albedo_source.png`
- 작업본: `working/floor/tr01_floor_base_[a-f]_albedo_normalized.png`
- QA: `working/floor/tr01_floor_base_a_repeat_qa.png`,
  `working/floor/tr01_floor_base_variants_normalized_qa.png`
- 선택 이유: 저대비·저빈도 표면이며 3×3 반복에서 경계선이 두드러지지 않는다. 6종 평균색을
  공통 목표값으로 정규화해 혼합 배치의 사각 패치를 줄였다.
- 채널 완료: Normal, 비발광 Emission, Material Mask, AO.

공통 생성 프롬프트:

```text
Use case: stylized-concept
Asset type: one seamless square tileable 2D game floor Albedo texture
Input image 1: visual style and palette reference only; match the quiet central maintenance-room floor without copying structures or props.
Style/medium: polished painterly 2D game texture, stylized realism, low-frequency broad forms, production Albedo source.
Composition/framing: pure 90-degree top-down material view filling the entire square canvas; seamless wrapping on left/right and top/bottom edges; no border, frame, bevel, gutter or perspective.
Lighting/mood: neutral diffuse ambient only, no directional shadow, colored lighting, glow, bloom or vignette.
Color palette: charcoal, deep plum, muted cool blue-gray; restrained contrast and similar average value.
Constraints: exactly one continuous seamless material tile; no walls, rails, props, crystals, lamps, characters, symbols, text, UI, logo, watermark, checkerboard, diamond grid, large focal crack, edge darkening or obvious center composition.
```

변형 지시:

- A: 가장 조용한 압축 암분과 넓은 패치
- B: 마모된 넓은 석판과 약한 이음
- C: 이동으로 닳은 중심부와 옅은 자주 얼룩
- D: 압축된 흙과 넓은 쓸림
- E: 중간 크기 슬레이트 판과 얕은 접합
- F: 매끈한 혼합 암분과 제한된 보라 광물 반점

## TR01-WFR-A~C 첫 시도

- 상태: `rejected`
- 결과: `source/wall/tr01_wall_front_[a-c]_rejected_source.png`
- 폐기 이유: 한 셀 정면 모듈 대신 여러 셀을 포함한 큰 벽 패널로 생성됐다. 불투명 배경과
  복수 구조를 잘라 쓰면 좌우 이음새와 스케일 계약을 보장할 수 없다.
- 재사용 범위: 암석 크기, 철골 보강 방식과 재질 색상의 디자인 참고만 허용한다.

공통 생성 프롬프트:

```text
Use case: stylized-concept
Asset type: isolated high-resolution 2D game wall-front Albedo sprite source for TR01-WFR
Input image 1: visual style and material reference only; match its rock-and-riveted-steel maintenance-room structure.
Primary request: Create exactly one modular wall FRONT FACE segment, one cell wide and one cell visually tall. This is the vertical south-facing face beneath a separate wall-top cap, not a complete wall and not a floor tile. The top edge must be straight and connect to neighboring identical-width modules; the bottom edge meets the ground with a restrained dark contact band.
Scene/backdrop: genuine transparent alpha around only the outer silhouette; no checkerboard, floor, room, scenery or cast shadow.
Style/medium: polished painterly 2D game sprite, stylized realism, chunky low-frequency detail, charcoal stone reinforced with oxidized dark steel.
Composition/framing: centered straight-on front face with a slight orthographic top-down 3/4 reading only in surface planes; no perspective convergence; full rectangular module visible.
Lighting/mood: neutral diffuse Albedo, restrained self-shading from lighter top to darker bottom, no cyan/magenta/amber scene light, no bloom.
Constraints: exactly one rectangular wall-front module; left and right edges must tile and align; no wall-top cap, corner turn, pillar, arch, doorway, pipe, lamp, crystal, rubble, characters, text, UI, logos, watermark, border, backdrop, diamond projection or dramatic shadow.
```

## TR01-WFR-BASE-A 재시도

- 상태: 5개 채널 `approved`
- 결과: `approved/albedo/wall/tr01_wall_front_a_albedo.png`
- 원본: `source/wall/tr01_wall_front_a_albedo_source.png`
- QA: `working/wall_front_a_repeat_qa.png`
- 승인 이유: 캔버스를 채우는 1셀 정면으로 생성했으며 상단 철골, 중앙 수평 보강, 하단 접촉부가
  반복 배치에서 연속된다. 최초 좌우 경계 RGB 평균 오차 2.50이었던 승인본을 최종 공정에서
  반대 경계 픽셀 완전 일치로 다시 고정했다.
- 채널 완료: 반복 변형 B–F, Normal, 비발광 Emission, Mask, AO.

```text
Use case: stylized-concept
Asset type: one square tileable 2D game wall-front Albedo texture, one-cell module for TR01-WFR
Input image 1: visual style and material reference only; use the dark rock and riveted steel language of the maintenance-room walls.
Primary request: Create exactly one vertical south-facing wall FRONT FACE material tile filling the entire square canvas. It represents one cell wide by one cell tall beneath a separate wall-top cap. The upper 8 percent is a consistent thin cool steel-and-stone joining rim; the lower 12 percent is a consistent restrained dark contact-AO band. The middle is dark mine rock masonry with sparse industrial reinforcement.
Style/medium: polished painterly 2D game texture, stylized realism, chunky low-frequency shapes, production Albedo.
Composition/framing: straight-on orthographic vertical face; canvas fully filled edge to edge; left and right edges tile seamlessly; no transparent padding, object cutout, external background, frame around the canvas, perspective convergence or camera tilt.
Lighting/mood: neutral diffuse Albedo; lighter upper plane transition and darker lower contact band only; no colored scene light, bloom or cast shadow.
Color palette: charcoal stone, deep plum shadow, cool blue-gray steel, extremely restrained oxidized brown.
Constraints: exactly one one-cell square material tile; consistent top and bottom band heights; no wall-top cap surface, floor, corner turn, pillar, arch, door, pipe, lamp, crystal, rubble, characters, symbols, text, UI, logos, watermark, checkerboard, diamond grid or central hero object.
Variant A: quiet base with two broad horizontal rock courses and minimal reinforcement, lowest detail density.
```

## TR01-WTP-BASE-A / TR01-WTP-RIM-A

- 상태: `approved`
- 결과: `approved/*/wall_top/tr01_wall_top_a_*`,
  `approved/*/wall_top_rim/tr01_wall_top_rim_a_*`
- 승인 이유: 상단 A는 3×3 반복에서 끊김 없이 이어지며 바닥보다 무겁고 밝은 구조 면으로
  읽힌다. 림 A는 북쪽 경계에 연속적인 얇은 강조선을 제공한다.
- B~F는 구조 방향과 철골 밀도가 서로 달라 매크로 구역 변형으로 승인하고 4면 경계를
  픽셀 단위로 일치시켰다.

벽 상단 공통 생성 프롬프트:

```text
Use case: stylized-concept
Asset type: one seamless square tileable 2D game wall-top Albedo texture, one-cell module for TR01-WTP
Input image 1: visual style and palette reference only. Match the heavy mine wall caps, not the walkable floor.
Primary request: Create exactly one horizontal TOP CAP surface for a thick rock-and-steel mine wall, filling the whole square canvas. It is viewed from pure orthographic top-down and sits one cell above a separate wall-front sprite. Use broad armored rock slabs embedded in dark oxidized steel so it reads heavier, rougher and slightly lighter than the floor.
Style/medium: polished painterly 2D game texture, stylized realism, chunky low-frequency shapes, production Albedo.
Composition/framing: pure 90-degree top-down material view; canvas fully filled edge to edge; seamless wrapping on all four edges; no border, transparent padding, external backdrop, perspective or camera tilt.
Lighting/mood: neutral diffuse Albedo with restrained self-shading only; no scene-colored light, glow, bloom, cast shadow or vignette.
Constraints: exactly one continuous wall-top material tile; no front-facing wall, floor, rim highlight strip, corner turn, pillar, arch, door, pipe, lamp, crystal, rubble, characters, symbols, text, UI, logo, watermark, checkerboard, diamond grid or central focal object.
```

림 편집 프롬프트:

```text
Use case: precise-object-edit
Asset type: one square tileable 2D game wall-top rim Albedo texture, TR01-WTP-RIM-A
Input image 1: edit target, approved wall-top A structure.
Primary request: Change only the north/topmost 4 percent strip of the tile by adding a very thin, restrained cool blue-gray raised rim catching a faint desaturated amber edge highlight. The rim must run continuously from the left edge to the right edge so adjacent tiles connect. Keep it subtle and structural, not emissive.
Constraints: preserve the wall-top material and maintain seamless left/right edges; no new objects, no border on the other three sides, no glow, bloom, light pool, text, symbols, transparency, checkerboard, watermark, perspective or crop.
```

## 파생 채널 및 접촉 AO

- 생성 도구: `tools/art/generate-test-room-material-maps.ps1`
- 검수 도구: `tools/art/validate-test-room-package.ps1`
- 규칙: Albedo 명암에서 저강도 탄젠트 노멀과 국소 틈새 AO를 계산한다. 비발광 자산의
  Emission은 검정이며, 금속 밴드는 자산군 규칙에 따라 Material Mask R/G에 기록한다.
- 접촉 AO: 128×128 RGBA, 벽 접점 알파 150, 54px 안에서 0으로 페이드.

## TR01-ARC-001 승인 정규화

- 입력: `working/tr01_arch_gate_a_background_extraction_attempt.png`
- 재현 도구: `tools/art/finalize-test-room-arch.ps1`
- 결과: 640×512px RGBA, 5×1셀 footprint, 하단 기준 피벗 `(320, 8)`.
- 처리: 알파를 0/255 범위로 정규화하고 청록 발광부를 Emission으로 분리했다. Albedo의 해당
  영역은 중립 구조색으로 교체했으며, 저강도 Normal·Material Mask·AO를 같은 실루엣으로 생성했다.
- 검수: 다섯 채널 모두 640×512px, 알파 범위 0~255, 파일명·경로·정수 피벗 규칙 통과.

## TR01-PIL-INTACT-A / TR01-PIL-BROKEN-A

- 생성 방식: built-in ImageGen. 방 키아트는 스타일·재질·카메라 참고로만 사용했다.
- 정상형: 석재 코어와 리벳 철골 보강을 가진 1셀 산업 지지 기둥.
- 파손형: 상부 1/3 파손, 굽은 보강판과 노출 암석을 가진 대응 변형. 최초 결과의 불투명
  체크무늬는 승인하지 않고 background-extraction 편집으로 실제 투명 알파를 다시 확보했다.
- 재현 도구: `tools/art/finalize-test-room-pillars.ps1`.
- 결과: 각각 256×512px RGBA, 1×1셀 footprint, 하단 기준 피벗 `(128, 8)`, 5개 채널.
- 공통 생성 프롬프트 핵심: 3/4 top-down orthographic, neutral diffuse Albedo, one isolated
  pillar, transparent background, no floor/contact shadow/colored light/text/logo/watermark.

## TR01-LGT-WORKLAMP-A / WARNING-A / CRYSTAL-A

- 생성 방식: built-in ImageGen. 방 키아트는 스타일·재질·3/4 직교 카메라 참고로만 사용했다.
- 세 자산 모두 실제 투명 알파 원본을 얻었고 `tools/art/finalize-test-room-lights.ps1`로
  256×256px Albedo·Normal·Emission·Mask·AO를 제작했다.
- 작업등: 앰버 유리와 철제 삼각 받침, 발점 `[128,248]`.
- 경고등: 벽 부착형 마젠타 유리 하우징, 중심 피벗 `[128,128]`.
- 결정등: 시안 결정 3개와 저상 철제 소켓, 발점 `[128,248]`.
- 공통 제약: isolated one-cell prop, neutral diffuse Albedo, genuine transparency, no baked bloom,
  light spill, floor, contact shadow, text, logo, checkerboard or watermark.

## TR01-LIN 모듈 6종

- 생성 방식: built-in ImageGen, 자산별 개별 프롬프트. 방 키아트는 재질과 팔레트 참고로만 사용했다.
- 구성: 레일 직선·90도 곡선·T분기·단절, 중량 파이프 90도 엘보, 전력 케이블 Y분기.
- 재현 도구: `tools/art/finalize-test-room-linear.ps1`.
- 결과: 각 256×256px, 중심 피벗 `[128,128]`, 비발광 5채널, 경계 연결 포트 metadata.
- 공통 프롬프트 핵심: pure orthographic top-down ground-plane, exactly one one-cell module,
  endpoints reach stated canvas edges, neutral Albedo, genuine transparency, no floor/text/watermark.

## TR01-FGV 전경 오클루더 4종

- 생성 방식: built-in ImageGen, 자산별 개별 프롬프트.
- 구성: 산업 난간, 암반 립, U자 매달린 케이블, 저상 배관 프레임.
- 결과: 512×256px, 2×1셀, 발점 `[256,248]`, 비발광 5채널과 별도 페이드 마스크.
- 공통 프롬프트 핵심: foreground occluder, landscape, 3/4 orthographic, neutral Albedo,
  genuine transparency, no baked fade/contact shadow/text/watermark.

## TR01-DEC 장식 소품 10종

- 생성 방식: built-in ImageGen, 자산별 개별 프롬프트.
- 구성: 보급 상자, 열린 공구함, 암석 3개 묶음, 마젠타 광물, 밀폐 배럴, 정비 종이 묶음,
  케이블 스풀, 파손 목재·철골 빔, 광석 바구니, 볼트·너트·체인 하드웨어 묶음.
- 재현 도구: `tools/art/finalize-test-room-decorations.ps1`.
- 결과: 각 256×256px, 1×1셀, 발점 `[128,248]`, 5개 채널. 광물만 Emission 활성.
- 고해상도 원화 전체를 픽셀 단위로 처리하면 Windows 메모리 압박이 발생해, 256px 선축소 후
  알파 정리와 채널 파생을 수행하는 저메모리 공정으로 고정했다.

## TR01-HERO-DRILL-A

- 생성 방식: built-in ImageGen. 키아트의 역할·팔레트·카메라만 참고해 별도 구조로 생성했다.
- 최초 원본과 생성형 배경 분리본 모두 체크무늬가 불투명 픽셀로 남아 그대로는 폐기 판정했다.
- `tools/art/finalize-test-room-hero.ps1`의 밝은 무채색 배경 제거 공정으로 실제 알파를 만들고,
  512×384px·3×2셀·발점 `[256,376]`로 정규화했다.
- Albedo에서 앰버·시안 표시등을 Emission으로 분리하고 Normal·Mask·AO를 생성했다.
- 승인 사유: 드릴 헤드, 원통 구동부, 개방 정비 패널, 발전기, 지지 발이 한 실루엣으로 명료하며
  테스트 방의 보조 초점과 수리 가능한 비활성 설비라는 역할을 동시에 전달한다.

## TR01-VFX 4종

- 생성 방식: built-in ImageGen, 먼지·저층 안개·광선·낙진을 개별 생성했다.
- 재현 도구: `tools/art/finalize-test-room-vfx.ps1`.
- 먼지·광선·낙진은 512×512, 안개는 1024×256으로 정규화했다.
- 안개는 좌우 미러 반복으로 경계 픽셀을 일치시켰으며 manifest에 각 재생 방식을 기록했다.
- 공통 제약: genuine transparency, isolated VFX texture, no room/objects/text/watermark/background.

## TR01-WFR-BASE-B~F 승인 변형

- 날짜: 2026-09-08
- 생성 방식: built-in ImageGen, `precise-object-edit`
- 입력 이미지: `approved/albedo/wall/tr01_wall_front_a_albedo.png`; 구조·카메라·밴드 위치를
  잠그는 편집 기준 이미지로 사용했다.
- 생성 원본: `source/wall/tr01_wall_front_[b-f]_albedo_source.png`
- 승인 결과: `approved/{albedo,normal,emission,mask,ao}/wall/tr01_wall_front_[b-f]_*`
- 선택 이유: A의 상단 연결 림, 중앙 수평 보강, 하단 접촉 밴드를 유지하면서 중앙 재질부만
  조용한 석재(B), 대각 균열(C), 보수판(D), 습윤 흔적(E), 수직 보강(F)으로 구분된다.
- 후처리: `tools/art/finalize-test-room-wall-variants.ps1`로 128×128px 축소, 불투명 Albedo,
  4px 반대 경계 일치, 비발광 Emission, Normal·Mask·AO를 생성했다.

최종 공통 프롬프트:

```text
Use case: precise-object-edit
Asset type: seamless one-cell 2D game wall-front Albedo variant
Input image 1: edit target and topology lock.
Primary request: Create the requested restrained material variant only in the middle field.
Constraints: preserve the exact square canvas, straight-on orthographic wall-front, left/right seamless
continuity, upper joining-rim height, central horizontal-brace height, lower contact-band height, palette,
scale and neutral diffuse Albedo. Keep the full canvas opaque. No wall-top, floor, perspective, colored
light, glow, cast shadow, pillar, arch, door, pipe, lamp, crystal, rubble, character, text, symbol, logo,
watermark, checkerboard, border or crop.
```

변형 지시:

- B: quieter stone courses, small slate chips, two restrained rivets on the existing central brace
- C: one broad shallow diagonal crack only in the stone field
- D: one worn rectangular repair plate in the lower stone field below the brace
- E: restrained dark moisture discoloration and faint mineral streaking in the lower field
- F: two narrow vertical reinforcement straps inside the middle field

## TR01-WTP-BASE-B~F 최종 승인

- 날짜: 2026-09-08
- 생성 방식: built-in ImageGen, `stylized-concept`; 공통 프롬프트는 위
  `TR01-WTP-BASE-A / TR01-WTP-RIM-A` 항목에 전문을 보존했다.
- 생성 원본: `source/wall_top/tr01_wall_top_[b-f]_albedo_source.png`
- 승인 결과: `approved/{albedo,normal,emission,mask,ao}/wall_top/tr01_wall_top_[b-f]_*`
- 승인 이유: 서로 다른 암석·보강판 밀도를 매크로 구역 변형으로 활용할 수 있고, 후처리로
  모든 결과의 4면 반복 경계를 동일하게 고정했다.
- 후처리: `tools/art/finalize-test-room-wall-variants.ps1`, 128×128px, 중심 피벗 `[64,64]`,
  `WallTop`, 비발광 5채널.

## Revision 17 — 장식·선형 모듈 알파 재키잉

- 날짜: 2026-09-08
- 대상: `TR01-DEC-BARREL-A`, `HARDWARE-A`, `BUCKET-A`, `CABLE-SPOOL-A`, `PAPERS-A`,
  `TOOLBOX-A`, `TR01-LIN-RAIL-BROKEN-A`, `PIPE-ELBOW-A`, `CABLE-JUNCTION-A`
- 생성 방식: built-in ImageGen, `background-extraction`, 자산별 1회 호출
- 입력 이미지 역할: 각 revision 16 Albedo를 정확한 편집 대상으로 사용
- 생성 후보 경로:
  - Barrel: `C:/Users/Loadcomplete/.codex/generated_images/01a07ec9-d7ab-7600-8fd4-8e63f920b3aa/exec-12f31261-4dd4-4aa6-9d9d-af34cfd9fd72.png`
  - Hardware: `C:/Users/Loadcomplete/.codex/generated_images/01a07ec9-d7ab-7600-8fd4-8e63f920b3aa/exec-9d611ad2-99fe-47b0-b665-e828cd1ba381.png`
  - Bucket: `C:/Users/Loadcomplete/.codex/generated_images/01a07ec9-d7ab-7600-8fd4-8e63f920b3aa/exec-f75a09e0-fc31-458b-bce2-d23f88af7bcb.png`
  - Cable spool: `C:/Users/Loadcomplete/.codex/generated_images/01a07ec9-d7ab-7600-8fd4-8e63f920b3aa/exec-ee9d9710-ebc0-4a45-8c60-880f7067dddd.png`
  - Papers: `C:/Users/Loadcomplete/.codex/generated_images/01a07ec9-d7ab-7600-8fd4-8e63f920b3aa/exec-0dd3b1df-a4fa-43df-bd03-ce42211d7dc4.png`
  - Toolbox: `C:/Users/Loadcomplete/.codex/generated_images/01a07ec9-d7ab-7600-8fd4-8e63f920b3aa/exec-31295a03-bf6e-4205-b4e3-e74c5d9b6274.png`
  - Broken rail: `C:/Users/Loadcomplete/.codex/generated_images/01a07ec9-d7ab-7600-8fd4-8e63f920b3aa/exec-fad5fad2-0a40-4588-8ad7-4d742ace5e06.png`
  - Pipe elbow: `C:/Users/Loadcomplete/.codex/generated_images/01a07ec9-d7ab-7600-8fd4-8e63f920b3aa/exec-098ac2a6-797a-4dc2-9dfb-73f6a2430008.png`
  - Cable junction: `C:/Users/Loadcomplete/.codex/generated_images/01a07ec9-d7ab-7600-8fd4-8e63f920b3aa/exec-ccb0da28-9422-48f8-9886-8ea30864ca55.png`
- 후보 판정: 일부 결과는 투명 배경을 얻었지만 원본보다 형태·표면 디테일이 재해석됐고,
  Hardware 결과는 체크무늬가 다시 불투명 픽셀로 생성됐다. 따라서 생성 후보는 최종 RGB로
  채택하지 않았다.
- 최종 처리: 원본 RGB·위치·크기·피벗을 유지하고 `tools/art/fix-test-room-alpha-r17.ps1`로
  기존 알파 1~159를 0, 240~255를 255, 중간 구간을 좁은 램프로 변환했다. 같은 마스크를
  Albedo·Normal·Emission·Mask·AO에 적용했다.
- 승인 결과: `approved/`의 기존 45개 경로를 교체, 대상 자산 revision 2, manifest revision 17.
  반투명 비율 0.18~2.44%, 5채널 알파 완전 동일, 256×256 캔버스 유지.

최종 공통 프롬프트:

```text
Use case: background-extraction
Asset type: production 2D game sprite with genuine RGBA transparency
Input image 1: exact edit target.
Primary request: Remove only the entire pale gray and checkerboard-pattern background surrounding the
subject. The checker pattern is unwanted image content, not a transparency preview. Replace every
background square and translucent gray film with alpha zero.
Constraints: preserve the object or module geometry, edge connection points, exact silhouette,
proportions, position, scale, colors, painted texture, internal openings and framing unchanged. Do not
redraw, restyle, relight, crop, resize, rotate, move, add shadows, glow, scenery, text, or objects. Keep
only a one-to-two-pixel soft antialiased contour with no white fringe, gray halo, checkerboard pixels,
or translucent background film. Output one isolated RGBA sprite on actual transparent alpha.
```

## TR01-CONCEPT-ROOM-002

- 날짜: 2026-09-08
- 생성 방식: built-in `image_gen`
- use case: `stylized-concept`
- 상태: `concept` 후보, 사용자 승인 대기
- 입력 이미지: `reference/tr01_primary_style_target.png`
- 입력 역할: 편집 대상이 아닌 최우선 스타일·카메라·공간 밀도·팔레트·조명·렌더링·분위기 레퍼런스
- 결과: `concept/tr01_room_primary_style_concept_v2.png`
- 캔버스: 1672×941px RGB, 약 16:9
- 1차 내부 판정: 축 정렬 직교 3/4 시점, 높은 후면 구조, 보라 암부, 시안·마젠타·앰버
  삼색 조명, 굵은 pixel-painted 덩어리, 큰 프랍과 전경 오클루전이 레퍼런스에 근접했다.
  HUD 없이도 실제 협동 전투 화면의 밀도와 캐릭터 스케일이 읽힌다.
- 확인 필요: 사용자가 주 키아트로 승인하기 전까지 기존 키아트나 프랍 생성 기준을 교체하지 않는다.
  후보의 수정이 필요하면 승인 루브릭에서 가장 큰 차이 한 항목만 지정해 반복한다.

```text
Use case: stylized-concept
Asset type: final-quality 16:9 game environment key art and gameplay visual target for Tunnel Crew, intended to guide later modular 2D prop production.
Input images: Image 1 is the primary and dominant style, camera, spatial-density, palette, lighting, rendering, and mood reference. Generate a new original Tunnel Crew room; do not edit Image 1 and do not copy its exact characters, HUD, room layout, doorway shape, or individual props.

Primary request: Create an original compact underground mine maintenance-and-extraction chamber that feels like a genuine polished cooperative action-game screenshot. Match Image 1 as closely as possible in overall visual character: the same dense screen-filling room, chunky proportions, colored darkness, layered 2.5D depth, rich hand-painted pixel clusters, strong local lighting, and readable gameplay space. The environment is the main subject and must provide a clear visual contract for later wall, floor, arch, pillar, rail, pipe, machine, lamp, crystal, debris, and foreground-occluder assets.

Scene/backdrop: A dark abandoned industrial mine chamber enclosed by tall rock-and-riveted-metal walls. A large original reinforced mine gateway in the upper center opens into a cyan-lit tunnel. One side contains a broad magenta crystal seam embedded in broken rock; another side contains a heavy damaged mining compressor-drill with thick pipes and an amber maintenance light. Curved rail segments, chunky pipes, cables, work lamps, crates, rock piles, and restrained debris frame a broad central combat floor. Dark foreground rock and low industrial structures partially occlude the bottom and side edges.

Subjects and scale: Include four original short, stocky Tunnel Crew miners in distinct practical colored mining suits, plus one small utility drone, engaged in a readable cooperative combat/mining moment. They are scale references, approximately 13–18 percent of image height, and must not dominate the environment. Use original designs, not the characters from Image 1.

Style/medium: High-end pixel-painted 2D game art. Combine chunky stepped pixel clusters and limited-value silhouette blocks with rich hand-painted material variation, colored rim light, broad bevel highlights, and selective soft emissive glow. Preserve visible pixel character at gameplay scale. This must not become smooth digital concept painting, vector art, low-detail retro pixel art, 3D render, miniature diorama, or photorealism.

Composition/framing: 16:9 landscape, screen-axis-aligned orthographic top-down three-quarter view. Ground X/Y axes remain horizontal and vertical. No vanishing point, perspective convergence, fisheye, camera roll, diamond/isometric tile rotation, or compressed dimetric grid. One dense room fills the frame. Tall rear wall occupies roughly the upper third; readable combat floor occupies the middle; foreground occluders close the lower edge. Distribute the brightest accents as a triangle: magenta crystal light, cyan gateway light, and warm amber combat/work light. Maintain a clear walkable center and separable modular silhouettes.

Lighting/mood: Very dark but strongly colored purple-black and navy ambient shadows, not gray. Cyan tunnel backlight establishes depth; saturated magenta mineral glow defines the mine identity; compact amber-orange lamps and combat flashes define interaction. Use painted self-shading, short contact AO, directional cast shadows, normal-map-like bevel response, restrained HDR bloom, thin low fog, dust, sparks, and narrow volumetric light shafts. Keep glow local and preserve crisp sprite silhouettes.

Color palette: Near-black plum and navy void; dark violet, blue-gray, and charcoal structure; luminous magenta and violet minerals; sharp cyan/teal backlight; small amber-orange and warm-white cores. Preserve colored information inside the shadows.

Materials/textures: Chunky fractured mine rock, riveted oxidized steel, broad beveled panels, thick rails and pipe couplers, worn stone floor with broad quiet value patches, glass lamp cores, sharp faceted crystals, heavy painted machinery. Prioritize 3–7 large readable forms per prop over uniform micro-detail.

Constraints: original Tunnel Crew environment and characters; no HUD, interface panels, health bars, inventory, minimap, text, letters, numbers, labels, logos, trademarks, watermark, border, or caption. No checkerboard. No wide empty showroom floor. No tiny characters. No thin generic props. No uniform purple wash, white studio lighting, excessive fog, excessive bloom, depth of field, motion blur, chromatic aberration, glossy realistic PBR, smooth painterly brushwork, or obvious repeated tile stamps. The final image must look like a production-ready gameplay key art from which modular sprites can be designed.
```

## TR01-CONCEPT-ROOM-003

- 날짜: 2026-09-08
- 생성 방식: built-in `image_gen`
- use case: `precise-object-edit`
- 상태: `concept` 후보, 사용자 승인 대기
- 편집 대상: `concept/tr01_room_primary_style_concept_v2.png`
- 보조 레퍼런스: `reference/tr01_primary_style_target.png`
- 결과: `concept/tr01_room_primary_style_concept_v3.png`
- 캔버스: 1672×941px RGB, 약 16:9
- 사용자 피드백: v2가 실제 인게임처럼 보이지 않고 `축 정렬 직교 3/4 톱다운` 계약을 충분히
  지키지 못했다.
- 변경 범위: 그래픽 스타일·팔레트·주요 피사체는 고정하고 투영, 방 배치, 오브젝트 축 정렬,
  카메라 프레이밍만 교정했다.
- 1차 내부 판정: 바닥 사각 그리드, 문턱, 벽 기초선, 기계 footprint가 화면 X/Y에 정렬되고
  평행선 수렴이 제거됐다. 방 일부가 화면 밖으로 이어져 중앙 대칭 디오라마보다 실제 플레이 중인
  레벨 구간에 가깝게 읽힌다.

```text
Use case: precise-object-edit
Asset type: revised 16:9 Tunnel Crew gameplay key art, projection and in-game staging correction.
Input images: Image 1 is the edit target. Image 2 is the authoritative reference for camera projection, gameplay framing, ground-axis alignment, spatial scale, and in-game feeling.

Primary request: Redraw and recompose Image 1 only as needed to correct the camera projection and gameplay staging. Make the result unmistakably a real in-game 2D room viewed with screen-axis-aligned orthographic top-down three-quarter projection, matching Image 2. Preserve Image 1's successful pixel-painted graphic style, purple-black palette, cyan/magenta/amber lighting, miners, drone, mine materials, gateway, machinery, rails, crystals, and overall polish. Change the spatial projection, room layout, object alignment, and framing rather than changing the art style.

Projection lock:
- Orthographic camera with zero perspective convergence and zero focal-length distortion.
- Ground/world X axis runs exactly left-to-right on screen.
- Ground/world Y axis runs exactly bottom-to-top on screen.
- The walkable floor is an axis-aligned rectangular grid plane, never a diamond and never a trapezoid.
- Tile seams, wall foundations, rail straights, pipe runs, doorway thresholds, crates, and machine footprints visibly confirm horizontal/vertical screen alignment.
- Parallel edges stay parallel everywhere and objects do not shrink with distance.
- The three-quarter depth comes only from consistently painted vertical wall-front faces, visible top caps, character side planes, bottom-center ground pivots, Y sorting, contact shadows, and foreground occlusion.
- All wall-front faces share one consistent visual height and face toward the bottom of the screen.
- Do not aim architecture toward a central vanishing point. Do not use diagonal isometric axes.

In-game staging correction:
- Frame the scene like a live gameplay camera looking at one section of a larger map, not a self-contained symmetrical illustration or miniature diorama.
- Let room structures continue or crop naturally beyond the left, right, and bottom viewport edges.
- Use an asymmetrical practical level layout with a broad readable central combat zone.
- Place the four miners and drone at consistent gameplay sprite scale with clearly grounded feet and short contact shadows.
- Space characters as active player units, not as a posed promotional group.
- Keep enemies and weapon effects readable but secondary to navigation.
- Use foreground occluders sparingly at the bottom edge; do not create a heavy cinematic black frame.
- Retain the upper-center cyan passage as a navigation cue, but make its threshold a horizontal screen-aligned line and avoid perspective tunnel convergence.
- Reduce decorative crystal repetition in the open floor; concentrate mineral growth along wall and terrain boundaries as in Image 2.

Style invariants: keep the same high-end pixel-painted 2D treatment, chunky stepped pixel clusters, broad beveled metal, hand-painted rock variation, colored shadow information, local emissive glow, crisp gameplay silhouettes, dark violet/navy ambience, magenta mineral light, cyan backlight, and compact amber work/combat light. Preserve the general identities and colors of the four miners and drone without copying Image 2's characters.

Constraints: no HUD, interface, text, letters, numbers, labels, logos, watermark, border, caption, grid overlay, checkerboard, perspective camera, vanishing point, fisheye, camera roll, diamond tiles, 2:1 dimetric projection, true isometric projection, diagonal room footprint, converging architecture, miniature-diorama presentation, centered poster composition, smooth digital painting, 3D render, photorealism, depth of field, motion blur, or excessive bloom.
```

## TR01-REFERENCE-CALIBRATION-V1

- 날짜: 2026-09-08
- 생성 방식: built-in `image_gen`
- 최상위 입력: `reference/tr01_primary_style_target.png` — 스타일·비율·재질·가독성 레퍼런스
- 보조 입력: `concept/tr01_reference_asset_calibration_board_v1.png` — 네 후보의 디자인 일관성 기준
- 상태: `working`; 기존 승인 패키지와 manifest는 변경하지 않음
- 결과:
  - 캘리브레이션 보드 `concept/tr01_reference_asset_calibration_board_v1.png`
  - 바닥 v1 `source/reference_calibration_v1/tr01_reference_floor_a_source.png` — 반복 밀도 과다로 rejected
  - 바닥 v2 `source/reference_calibration_v1/tr01_reference_floor_a_v2_source.png` — working
  - 벽 최초 RGB 체크무늬본 — rejected, 저장하지 않음
  - 벽 알파 추출본 `source/reference_calibration_v1/tr01_reference_wall_a_source.png` — working
  - 수정 `source/reference_calibration_v1/tr01_reference_crystal_a_source.png` — working
  - 드릴러 `source/reference_calibration_v1/tr01_reference_driller_a_source.png` — working
- 공통 프롬프트 계약: 레퍼런스의 굵은 계단형 픽셀 클러스터, 넓은 제한 명암 면, 짧고 굵은
  비율, 검보라·남청 기반과 제한된 마젠타·시안·앰버를 따른다. 특정 캐릭터나 프랍은 복제하지
  않고 땅굴 크루 고유 디자인으로 만든다. 중립 baked form light만 포함하고 방 조명·긴 그림자·
  넓은 글로우는 제외한다. UI·문자·로고·워터마크·미니어처·PBR 광택을 금지한다.
- 바닥 v2 추가 프롬프트: 한 셀 전체를 1~3개의 큰 석판 면으로 제한하고 자갈밭, 조약돌 무늬,
  고빈도 균열, 반복되는 마젠타 점을 제거한다. 네 변은 seamless, 중앙은 조용하게 유지한다.
- 벽 추가 프롬프트: 굵은 상단 cap, 높은 정면, 청록 철제 보강/배관, 소량의 마젠타 광물로
  3~7개의 큰 형태를 구성하고 하단 발점을 명확히 한다. 최초 결과의 체크무늬 배경만 정밀 추출해
  실제 투명 알파로 교체하며 디자인과 픽셀 경계는 보존한다.
- 수정 추가 프롬프트: 3~5개의 지배적인 결정 덩어리와 넓은 암석 받침, 작은 내부 발광 코어만
  사용하고 과도한 미세 면과 주변 글로우를 금지한다.
- 드릴러 추가 프롬프트: 큰 얼굴과 갈색 수염, 주황 작업복, 남청 장갑·부츠, 양손으로 든 거대한
  시안·남청 드릴을 사용한다. 3/4 우향, 전신과 발점·드릴 끝을 보존하고 투명 배경으로 만든다.
- Unity 검증: `qa/reference-calibration-v1-unity.png`, 상세 판정은
  `process/reference-calibration-v1.md` 참조.

### 바닥 매크로 변형 B~F 확장

- 날짜: 2026-09-08
- 생성 방식: built-in `image_gen`, 자산별 1회 호출
- 입력 1: `reference/tr01_primary_style_target.png` — 최상위 픽셀 표현·재질·팔레트 기준
- 입력 2: `source/reference_calibration_v1/tr01_reference_floor_a_v2_source.png` — 스케일·명도 기준
- 생성 원본:
  - B: `source/reference_calibration_v1/tr01_reference_floor_b_source.png`
  - C: `source/reference_calibration_v1/tr01_reference_floor_c_source.png`
  - D: `source/reference_calibration_v1/tr01_reference_floor_d_source.png`
  - E: `source/reference_calibration_v1/tr01_reference_floor_e_source.png`
  - F: `source/reference_calibration_v1/tr01_reference_floor_f_source.png`
- 공통 최종 프롬프트: 정사각형 1셀, 정확한 탑다운 직교, A와 같은 굵은 수제 픽셀 덩어리·
  스케일·검보라 명도·재질. 네 변 seamless, 캐릭터 아래 중앙은 조용하게 유지한다. 큰 석판과
  소수의 굵은 이음만 사용한다. 자갈 노이즈, 잦은 미세 균열, 넓은 글로우, 방향성 조명,
  그림자, 비네트, 오브젝트, 캐릭터, 글자, UI, 로고, 워터마크, 프레임, 투명 배경,
  체크무늬를 금지한다.
- 변형별 최종 지시:
  - B: 넓은 대각 이음 하나가 큰 슬레이트 면 두 개를 분리하고 짧은 보조 균열만 둔다.
  - C: 중앙 대형 슬레이트를 넓은 세 조각으로 나누고 작은 파편은 거의 두지 않는다.
  - D: 두 덩어리를 긴 계단형 수평 지층 이음으로 나누고 짧은 오프셋 이음 하나만 둔다.
  - E: 거의 온전한 거대 석판 하나, 모서리 파손 하나, 가장자리의 짧은 얕은 균열 하나만 둔다.
  - F: 불규칙한 대형 판석 세 덩어리와 드문 분기 이음, 이음 옆 마젠타 흔적 하나만 둔다.
- 후처리: `tools/art/finalize-reference-calibration-v1.py`로 각 128×128 축소, 반대편 4px
  경계 동일화, working·Unity 경로 동시 저장, A 단독 및 A~F 혼합 6×6 반복판 생성.
- 검증: 6파일 픽셀 고유성, 네 변 4px 일치, Unity 128 PPU 120셀 참조 합계 120,
  `qa/reference-calibration-floor-variants-unity.png` 캡처 완료.
