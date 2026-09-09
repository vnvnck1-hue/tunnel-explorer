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

### TR01-REFERENCE-CALIBRATION-V1-BOARD-DIRECT 교체

- 날짜: 2026-09-09
- 결정: 캘리브레이션 보드를 스타일 참고용으로만 두던 이원화 방식을 폐기한다.
- 최상위 승인 원본: `concept/tr01_reference_asset_calibration_board_v1.png`
- 처리: 보드의 바닥·벽·수정·드릴러 픽셀을 `tools/art/extract-reference-board-assets.py`로
  직접 crop/alpha 분리하고, 기존 `source/`, `working/`, Unity 임포트본을 해당 결과로 교체했다.
- 바닥 A~F alias는 승인 전 변형을 막기 위해 보드 바닥 A와 동일한 이미지로 통일했다.
- Unity 캘리브레이션 씬에서는 기존 바닥 회전·반전·틴트와 캐릭터/프랍 임의 확대를 제거했다.
- 씬 정리: `tools/art/normalize-reference-calibration-scene.py`
- 원칙: 보드 승인 전 별도 image_gen 재생성, 스타일 재해석, 색 틴트, 매크로 변형을 적용하지 않는다.

### TR01-REFERENCE-FLOOR-BC 및 검은 여백 수정

- 날짜: 2026-09-09
- 생성 방식: built-in `image_gen`, B/C 각각 1회 호출
- 입력 1: `working/reference_calibration_v1_direct/tr01_reference_floor_a_albedo.png`
- 입력 2: `concept/tr01_reference_asset_calibration_board_v1.png`
- B 생성 원본: `source/reference_calibration_v1/tr01_reference_floor_b_generated.png`
  - 두 개의 넓은 석판, 굵은 계단형 대각 이음, 제한된 마젠타 광물로 차별화.
- C 생성 원본: `source/reference_calibration_v1/tr01_reference_floor_c_generated.png`
  - 세 개의 넓은 판석, 조용한 중앙, 양쪽 가장자리의 제한된 마젠타 광물로 차별화.
- 공통 프롬프트 계약: 정사각 전체를 채우는 불투명 탑다운 직교 바닥, A와 같은 픽셀 클러스터·
  팔레트·재질 스케일, 검은 여백·투명 배경·프레임·비네트·독립 카드 표현 금지.
- 정규화: A는 원본 보드 사각 영역을 여백 없이 다시 추출하고 B/C와 함께 128×128로 변환했다.
  세 타일 모두 alpha 255이며 반대편 3px 경계를 동일화했다.
- 알파 수정: 수정·벽·드릴러는 저채도 보드 배경을 제외하는 마스크로 다시 추출해 검은 덩어리를 제거했다.
- Unity: `ReferenceCalibrationV1` 120셀을 A/B/C로 결정적 분배하고 회전·반전·틴트·임의 확대를 제거했다.
- 아트보드: 최종 Unity 임포트 PNG 자체를 사용해
  `concept/tr01_reference_asset_calibration_board_v1.png`를 재조립했다.

### TR01-REFERENCE-LAMP-A

- 날짜: 2026-09-09
- 생성 방식: built-in `image_gen`, 1회 호출
- 입력: 사용자가 제공한 보라색 광산 전등 크롭 — 형태·팔레트·발광 기준
- 생성 원본: `source/reference_calibration_v1/tr01_reference_lamp_a_generated.png`
- 최종 리소스: `unity/TunnelCrew/Assets/Art/Visual/ReferenceCalibrationV1/tr01_reference_lamp_a_albedo.png`
- 최종 프롬프트 요약: 레퍼런스의 상하 금속 하우징과 중앙 자홍색 발광 코어를 유지한 단일
  산업용 광산 전등. 정면 직교, 픽셀 아트, 실제 투명 배경, 환경·문자·UI·검은 사각 배경 금지.
- 정규화: 투명 원본을 192×256, 128 PPU, 하단 중앙 피벗용 캔버스로 변환했다.
- 승인 보드: 최종 Unity PNG 자체를 `concept/tr01_reference_asset_calibration_board_v1.png`에 삽입했다.
- Unity: `ReferenceCalibrationV1` 우측 하단에 전등 스프라이트를 배치하고 모든 검증 스프라이트에
  URP `Sprite-Lit-Default`를 적용했다. 전역 `Light2D`와 전등 중심의 마젠타 Point `Light2D`
  (inner 0.18, outer 2.4, intensity 2.35)를 씬 오브젝트로 저장한다. 전역광은 0.62로 낮춰
  전등 주변의 실제 조명 풀이 캡처에서 구분되게 했다.

### DRILLER-8DIR-IDENTITY-V2

- 날짜: 2026-09-09
- 단계: 애니메이션 제작 전 8방향 정체성·카메라 승인 시트
- 적용 프로세스: `docs/CHARACTER_8_DIRECTION_SHEET_GUIDE.md`
- 생성 방식: built-in `image_gen` 1회 + 배경 추출 1회
- 입력 1: 기존 `assets/characters/driller-8dir-transparent.png` — 원형 구도와 방향 순서만 참조
- 입력 2: `unity/TunnelCrew/Assets/Art/Visual/ReferenceCalibrationV1/tr01_reference_driller_a_albedo.png`
  — 캐릭터 정체성·팔레트·픽셀 표현의 최상위 기준
- 최종 파일: `assets/characters/driller-8dir-transparent.png` 직접 갱신
- 프롬프트 핵심: 정확히 8명, N/NE/E/SE/S/SW/W/NW가 중앙에서 바깥을 향함, 중앙 22%
  완전 비움, 고정 직교 3/4 카메라, 동일 피벗·크기, 아트보드의 주황 헬멧·갈색 수염·주황/남청
  작업복·시안 드릴·등 탱크 유지.
- 드릴 규격 수정: 손에서 비트 끝까지 캐릭터 키의 55% 이하, 전체 실루엣 폭은 몸 폭의 약 1.45배
  이하, 은색 비트는 3단으로 단축하고 N/S는 단축 투영한다. 향후 224×224 셀의 176×176
  안전영역 안에 정규화할 수 있도록 구성했다.
- 배경 처리: 최초 결과의 실제 체크무늬 픽셀을 별도 background-extraction 단계로 제거했다.
- 후처리: `tools/art/normalize-8dir-turnaround.py`로 2048×2048 RGBA, 외곽 5% 이상의 안전
  여백과 중앙 투명 반경 20.1%를 적용했다. 연결 성분 8개, 중앙 분리, 네 모서리 alpha 0을
  자동 검사했다.
- 판정: 8방향 승인 후보. 현재 Unity 런타임의 224×224 walk/fall 시트는 아직 교체하지 않았다.
  사용자 승인 후 방향별 기준 프레임 분리 단계로 진행한다.

### DRILLER-8DIR-WALK-4F-PREVIEW-V1

> **폐기 / 사용 금지 (2026-09-09):** 프레임마다 얼굴·장비·비율·선이 달라져 연속
> 애니메이션의 정체성 일관성을 충족하지 못했다. 이 결과는 승인 자산이나 후속 시트 입력으로
> 사용하지 않는다. 이후 ChatGPT/Codex 및 생성형 이미지 모델을 통한 애니메이션 프레임·GIF
> 제작은 `docs/CHARACTER_8_DIRECTION_SHEET_GUIDE.md` §14에 따라 금지한다.

- 날짜: 2026-09-09
- 단계: 최종 방향별 스프라이트 시트 제작 전, 8방향 동시 제자리 걷기 승인 GIF
- 생성 방식: built-in `image_gen`으로 걷기 위상별 4회 + 각 결과의 배경 추출 4회
- 입력: `assets/characters/driller-8dir-transparent.png` — 캐릭터 정체성, 8방향, 카메라,
  원형 배치와 드릴 길이의 고정 기준
- 걷기 위상:
  - F0: 왼발 접지, 오른발 후방, 몸이 낮은 접지 자세
  - F1: 첫 통과 자세, 두 발이 몸 아래로 모이고 몸이 소폭 상승
  - F2: 오른발 접지, 왼발 후방, F0와 같은 낮은 높이
  - F3: 두 번째 통과 자세, 다음 F0로 이어지는 소폭 상승
- 프롬프트 핵심: 정확히 8명과 기존 방향 유지, 제자리 보행, 짧고 무거운 보폭, 아주 작은
  상하 바운스와 장비의 제한된 후속 동작. 이동·회전·줌·카메라 변경·드릴 확대·VFX·바닥·
  그림자·문자·추가 오브젝트를 금지한다.
- 배경 처리: 생성된 RGB 체크무늬를 포즈 보존 background-extraction 단계로 제거해 실제
  32-bit RGBA로 변환했다.
- 피벗 후처리: `tools/art/build-driller-8dir-walk-gif.py`가 승인된 기준 이미지의 방향별
  슬롯을 읽고, 각 방향을 모든 프레임에서 해당 슬롯의 하단 중앙 지면 앵커에 고정한다.
  생성 결과의 프레임별 배치·크기 흔들림은 이 단계에서 제거한다.
- 최종 GIF: `assets/characters/driller-8dir-walk-4f.gif`
  - 2048×2048, 4프레임, 100ms/frame, 0.4초 무한 루프, 투명 배경
- 후속 시트용 RGBA 원본: `assets/characters/driller-8dir-walk-4f/frames/frame_000.png` ~
  `frame_003.png`
- 접촉판: `assets/characters/driller-8dir-walk-4f/driller-8dir-walk-4f-contact.png`
- 검증: 네 PNG 모두 2048×2048 RGBA, 연결 성분 8개, 모서리 alpha 0. GIF는 4프레임
  각각 100ms이며 모든 프레임의 투명 인덱스를 확인했다.
- 판정: 애니메이션 승인용 프리뷰. Unity의 기존 224×224 방향별 walk 시트는 아직 교체하지 않았다.

### DRILLER-8DIR-IDENTITY-V2 크기 검수 정정

- 날짜: 2026-09-09
- 판정 변경: 방향·투명도 검사는 통과했지만 기존 캐릭터 대비 크기 검사를 누락했으므로 승인
  후보 판정을 철회한다.
- 측정 결과: 드릴러 방향별 중간 높이는 `285px / 2048 = 13.9%`다.
- 기존 비교값: 스카웃·엔지니어·거너를 종횡비 유지 `contain` 방식으로 2048 정사각 좌표에
  환산하면 방향별 중간 높이는 각각 약 `485px`, `506px`, `453px`다.
- 결론: 드릴러는 기존 3캐릭터 기준 중간값 약 `481px`의 `59%` 크기이며 명백히 작다.
- 직접 원인: `tools/art/normalize-8dir-turnaround.py`의 `figure_scale=0.7`을 기존 캐릭터
  체급 비교 없이 적용해 이미 작은 방향별 실루엣을 추가 축소했다.
- 프로세스 원인: 기존 문서에는 `2048×2048` 캔버스 권장값만 있고, 기존 캐릭터를 동일 좌표로
  환산한 실루엣 점유율 및 허용 오차 검사가 없었다.
- 조치: 가이드에 `450~510px / 2048`, 기존 중간값 대비 `±10%` 크기 게이트를 추가했다.
  현재 드릴러 V2는 교정·재승인 전까지 크기 기준 및 애니메이션 입력으로 사용하지 않는다.
- 도구 교정: `normalize-8dir-turnaround.py`의 임의 기본 배율 `0.7`을 제거하고, 전체 방향에
  동일 배율을 적용해 격자 스냅 전 중간 높이를 `456px`로 맞춘 뒤 `450~510px` 범위,
  2% 외곽 여백, 8개 실루엣 분리를 자동 검증하도록 바꿨다. `480px` 시험에서는 인접 방향이
  붙어 6개 연결 성분으로 합쳐졌고, `450px`는 4px 픽셀 격자 변환 후 `448px`가 되었으므로
  겹치지 않으면서 최종 게이트를 통과하는 `456px`를 기본값으로 정했다.

### DRILLER-8DIR-IDENTITY-V3-SCALE-CORRECTED

- 날짜: 2026-09-09
- 목적: V2의 과도하게 작은 캐릭터 체급을 기존 스카웃·엔지니어·거너와 동일 범위로 교정.
- 생성 방식: built-in `image_gen` 재작화 1회, 좌우 잘림 수정 1회, 배경 추출 1회.
- 입력 1: `assets/characters/driller-8dir-transparent.png` V2 — 드릴러 정체성, 픽셀 스타일,
  팔레트, 방향, 카메라, 짧은 드릴 기준.
- 입력 2~4: 기존 스카웃·엔지니어·거너 8방향 시트 — 캐릭터 내용이 아닌 크기와 원형 배치
  밀도만 참조.
- 프롬프트 핵심: 정확히 8방향, 각 실루엣이 전체 캔버스 높이의 22~25%, 기존 드릴러의
  정체성과 짧은 3단 드릴 유지, 고정 직교 3/4 카메라, 중앙 여백, 무겹침·무잘림.
- 1차 판정: 크기는 개선됐으나 E/W 드릴 끝이 잘려 폐기.
- 2차 판정: 잘림은 해소됐으나 외곽 여백 부족. 배경 추출 후 결정적 정규화 적용.
- 정규화 도구 수정: 겹치는 사각 바운딩 박스가 다른 방향 픽셀을 복제하지 않도록 연결
  실루엣 마스크 자체만 crop하도록 교정했다.
- 최종 파일: `assets/characters/driller-8dir-transparent.png` 직접 교체.
- 사용자 추가 피드백: 생성 결과가 픽셀풍 일러스트에 가까워 실제 픽셀 아트 느낌이 부족함.
- 픽셀 아트 확정: `tools/art/pixelize-8dir-turnaround.py`로 512×512 논리 픽셀, 4배
  nearest-neighbor 확대, 최대 64색, 디더링 없음, 이진 알파를 적용했다. 모든 RGBA 픽셀이
  동일한 4×4 블록에 정렬된다.
- 최종 검증: 2048×2048 RGBA, 방향 그룹 8개, 중간 높이 452px, 알파 점유율 24.11%,
  외곽 여백 좌/상/우/하 72/124/72/200px, 불투명 팔레트 64색, alpha 값 0/255,
  4×4 픽셀 격자 정렬, 모서리 alpha 0.
- 런타임 상태: 정지 8방향 승인용 파일만 교체하며 Unity walk/fall 애니메이션 시트는 변경하지 않는다.

### DRILLER-8DIR-IDENTITY-V3-EYE-CORRECTION

- 날짜: 2026-09-09
- 사용자 피드백: 64색 픽셀 변환 후 눈과 눈썹의 어두운 픽셀이 합쳐져 검은 안대처럼
  무섭게 보임.
- 원인: 전체 팔레트·격자 검사는 통과했지만 방향별 얼굴 확대 검수와 눈 내부 색 분리 검사를
  승인 게이트에 포함하지 않았다.
- 수정 방식: built-in `image_gen` 정밀 편집 1회로 눈과 눈썹만 교정하고 배경 추출 1회.
  아트보드 드릴러를 표정 기준으로 사용했으며 몸, 크기, 방향, 장비, 드릴, 배치는 잠갔다.
- 눈 규격: 밝은 웜 아이보리 눈 영역, 작은 암갈색 동공, 두 눈 사이 피부색 간격, 눈과 눈썹
  사이 밝은/피부색 간격. 검은 띠·안대·큰 검은 눈구멍 금지.
- 후처리: 중간 높이 456px 정규화 후 512 논리 픽셀, 4배 NEAREST, 64색, 이진 알파 적용.
- 프로세스 변경: `S/SE/SW/E/W` 얼굴 확대 검수와 팔레트 축소 후 눈 가독성 재검사를 필수화.
- 추가 검출: 정밀 편집 결과에서 `S` 방향이 중앙 쪽으로 이동해 중앙 반경 17%에 1,678픽셀이
  침범했다. 이 중간 결과는 폐기했다.
- 레이아웃 복원: `tools/art/align-8dir-to-reference.py`로 눈 수정 실루엣을 직전 승인본의
  방향별 하단 중앙 지면 앵커에 4px 격자로 재정렬했다.
- 최종 검증: 2048×2048 RGBA, 방향 8개, 중간 높이 452px, 64색, alpha 0/255,
  4×4 격자 정렬, 외곽 여백 좌/상/우/하 72/124/72/200px, 중앙 반경 17% 가시 픽셀 0.

### DRILLER-8DIR-IDENTITY-V4-ARTBOARD-PROPORTION

- 날짜: 2026-09-09
- 사용자 피드백: V3는 머리와 몸이 지나치게 작고 짧아 아기 같은 치비 인상이 강했다.
  승인 아트보드의 성인 드워프 체형과 그림 밀도를 직접 기준으로 다시 제작한다.
- 참조 우선순위: 사용자 제공 아트보드 드릴러를 얼굴·체형·의상·장비·색·묘사 밀도의
  최우선 기준으로 사용하고, 직전 8방향 시트는 방향 순서·원형 배치·점유율에만 사용했다.
- 생성 방식: built-in `image_gen` 신규 생성 1회, 체형 정밀 수정 1회, 배경 추출 1회.
- 1차 판정: 무기 축소와 방향 구성은 개선됐지만 머리 비중이 아직 커서 폐기했다.
- 체형 교정: 머리와 헬멧을 약 12~15% 줄이고 전체 높이는 유지한 채 몸통·허리·허벅지·
  정강이를 늘렸다. 최종 체형은 약 2.8~3등신의 건장한 성인 드워프이며 아트보드처럼
  몸통과 다리 관절이 명확히 읽힌다.
- 무기 교정: 아트보드 드릴의 정체성·청록/남색 팔레트는 유지하되 전체 길이를 약 65~70%로
  줄이고 짧은 3단 비트로 정리해 방향별 슬롯과 인접 실루엣을 침범하지 않게 했다.
- 최종 파일: `assets/characters/driller-8dir-transparent.png` 직접 교체.
- 픽셀 밀도: 아트보드의 촘촘한 픽셀 묘사를 보존하기 위해 고밀도 프로필인 1024×1024
  논리 픽셀, 2배 NEAREST, 최대 96색, 디더링 없음, 이진 알파를 적용했다.
- 최종 검증: 2048×2048 RGBA, 방향 그룹 8개, 방향별 높이 441~479px·중간값 455px,
  외곽 여백 좌/상/우/하 128/142/106/186px, 중앙 반경 17% 가시 픽셀 0, 불투명 팔레트
  96색, alpha 값 0/255, 2×2 픽셀 격자 정렬.
- 프로세스 변경: 모든 캐릭터에 2등신·512/64색을 강제하지 않는다. 승인 아트보드의 실제
  체형을 캐릭터별 오버라이드로 기록하고 표준 또는 고밀도 픽셀 프로필을 먼저 고정한다.
- 런타임 상태: 정지 8방향 정체성 기준 파일만 교체하며 Unity walk/fall 애니메이션 시트는
  변경하지 않는다.

### TR01-CHARACTER-ARTBOARD-V1 — 최상위 키아트 직접 복원

- 날짜: 2026-09-09
- 목적: 8방향 제작 전에 네 역할을 단일 3/4 전신 원화로 먼저 승인한다.
- 최상위 기준: `reference/tr01_primary_style_target.png` 안의 실제 캐릭터 디자인을 새로
  해석하지 않고, 작은 인게임 표현을 고해상도로 복원한다.
- 1차 오류: 기존 `scout/engineer/gunner-sw-reference-guide-500.png`를 정체성 입력으로
  사용해 파란 고글 스카웃·드론 엔지니어·노란 헬멧 거너가 생성됐다. 사용자가 최상위
  키아트와 다른 룩이라고 판정했으며 세 결과 모두 폐기했다.
- 교정: 기존 캐릭터 가이드를 입력에서 완전히 제외하고 최상위 키아트에서 인물별 원본 크롭을
  직접 만들었다. 크롭은 복원 대상, 승인된 단일 드릴러는 출력 밀도·외곽선·카메라 기준,
  전체 키아트는 가려진 문맥 확인용으로만 사용했다.
- 역할 매핑:
  - 드릴러: 주황 헬멧, 갈색 수염, 청록 대형 드릴.
  - 스카웃: 금발, 주황 통신 후드, 남청 작업복, 위로 든 시안 손전등.
  - 거너: 녹색 후드, 짧은 회색 머리, 중갈색 피부, 주황·남청 중화기.
  - 엔지니어: 반사 바이저가 달린 주황 밀폐 헬멧, 청색 작업복, 유선 수리 공구.
- 제외 인물: 중앙의 붉은 머리 총기 캐릭터는 네 역할 밖의 중복 후보로 보고 V1에서 제외했다.
- 생성 방식: built-in `image_gen`; 드릴러 1회, 키아트 직결 스카웃·거너·엔지니어 각 1회,
  각 결과의 실제 RGBA 배경 추출 1회.
- 결과 폴더: `concept/character-artboard-v1/`.
- 승인 보드: `concept/character-artboard-v1/tr01_character_lineup_artboard_v1.png`.
- 상태: 4종 단일 원화 승인 대기. Unity 캐릭터 및 8방향/동작 시트는 변경하지 않았다.

### TR01-CHARACTER-ARTBOARD-V1-DEFAULT-CORRECTION — 공통 기본 자세

- 날짜: 2026-09-09
- 입력: V1의 드릴러·스카웃·거너·엔지니어 단일 원화. 각 이미지는 캐릭터 디자인과
  그림 스타일의 절대 기준이며, 드릴러는 공통 포즈·카메라 기준이다.
- 목적: 네 캐릭터를 모두 앞면이 주로 보이면서 화면 오른쪽을 향하는 고정 직교 3/4 시점의
  기본 대기 자세로 통일한다.
- 포즈 규칙: 양발 접지, 안정된 어깨너비 스탠스, 도약·달리기·사격·수리·팔을 든 동작 없음.
  장비는 몸 앞에서 화면 오른쪽을 향해 편안하게 든다.
- 디자인 잠금: 얼굴·머리·피부·헬멧·의상·팔레트·배낭·장비·픽셀 클러스터·외곽선·명암
  밀도를 V1에서 재해석하거나 단순화하지 않는다.
- 거너 보호캡 교정: 녹색 후드와 회색 머리가 내부로 비쳐 보이는 얇은 투명 폴리카보네이트
  쉘, 좁고 선명한 테두리, 작은 청백색 반사 픽셀로 다시 그렸다. 흰색 불투명 얼룩은 금지한다.
- 엔지니어 교정: 1차 기본 자세가 측면에 가까워 폐기하고, 머리·몸통·골반을 관찰자 쪽으로
  더 돌려 앞면과 양쪽 어깨가 읽히는 우측 3/4 각도로 다시 생성했다.
- 배경 처리: 스카웃·거너는 built-in `image_gen` 배경 추출 후 이진 알파로 확정했다.
  엔지니어는 배경 추출이 두 번 연속 체크무늬를 남겨, 캐릭터를 재생성하지 않고 캔버스
  외곽과 연결된 무채색 체크 픽셀만 결정적으로 제거했다.
- 최종 폴더: 기존 단일 기준인 `concept/character-artboard-v1/`을 직접 갱신했다.
- 승인 보드: `concept/character-artboard-v1/tr01_character_lineup_artboard_v1.png`.
- 런타임 상태: 승인용 단일 원화만 추가했다. Unity 리소스와 8방향·애니메이션 시트는
  변경하지 않았다.

### GUNNER-8DIR-ARTBOARD-DIRECT-V1

- 날짜: 2026-09-09
- 입력 1: `concept/character-artboard-v1/tr01_character_artboard_v1_gunner.png` — 디자인,
  성인 체형, 투명 보호캡, 장비 비율, 색, 픽셀 밀도와 렌더링의 절대 기준.
- 입력 2: 교체 전 `assets/characters/gunner-8dir-transparent.png` — 캐릭터 디자인은 무시하고
  8방향 순서·원형 배치·빈 중앙·고정 카메라만 참조.
- 생성 방식: built-in `image_gen` 8방향 생성 1회, 체크무늬 RGB 배경 추출 1회.
- 사용자 우선 지시: 이전처럼 캐릭터를 작은 규격으로 축소하거나 후가공하지 않는다. 현재
  단일 원화의 머리–몸 비율, 팔다리, 중화기 크기, 보호캡과 묘사 밀도를 그대로 회전한다.
- 후처리: 없음. 정규화, 리사이즈, 크롭, 픽셀화, 팔레트 축소, 이진 알파 변환을 실행하지 않았다.
- 최종 파일: `assets/characters/gunner-8dir-transparent.png` 직접 교체.
- 검증: 원본 생성 크기 `1254×1254` RGBA, 연결 실루엣 8개, 중앙 반경 17% 가시 픽셀 0,
  모서리 실질 투명. 방향 순서는 N·NE·E·SE·S·SW·W·NW이며 각 무기는 바깥쪽을 향한다.
- 런타임 상태: 8방향 정체성 기준 이미지만 교체했다. Unity 방향별 런타임 시트와 애니메이션은
  변경하지 않았다.

## TR01-REFERENCE-WALL-SET-V1

- 날짜: `2026-09-09`
- 목적: `EnvironmentKit_ReferenceV1`의 빈 `wallTop`, `wallFront`, `contactAo` 슬롯을 채우는
  레퍼런스 트랙 벽 타일러블 세트 제작.
- 생성 방식: built-in `image_gen` 신규 생성 1회(cap A), 정밀 편집 3회(cap A edge correction,
  cap B, cap C), 신규 생성 3회(front A/B/C). 접합 AO는 생성형 이미지가 아닌 계약값을 따르는
  결정적 기술 자산으로 제작했다.
- 시각 레퍼런스: `concept/tr01_reference_asset_calibration_board_v1.png`를 최상위 보드로,
  `working/reference_calibration_v1_direct/tr01_reference_wall_a_albedo.png`를 벽 표면
  모티프 기준으로 사용했다. 기존 `TestRoomV01` 벽은 3단 구조와 반복 검수 선례만 참고했다.
- 결과: cap A/B/C 및 front A/B/C albedo 6종을 128×128로 nearest 정규화하고, cap은 4면,
  front는 좌우 4px 반복 경계를 픽셀 평균으로 잠갔다. front C는 생성 원본의 낮은 평균 명도를
  변형 간 허용 범위로 맞추기 위해 작업본에서만 1.24배 명도 보정을 적용했다.
- AO 결과: `tr01_reference_contact_ao_a.png`, 북쪽 alpha 150, y=54에서 0, 54px 선형 페이드.
- 검수: cap 평균 명도 `97.61/98.22/97.10`, front 평균 명도 `73.42/80.50/73.02`,
  모든 지정 반복 경계 평균 RGB 오차 `0`. 6×6 cap 혼합과 front 6연속 띠 QA 이미지를 생성했다.
- 상태: albedo 6종과 접합 AO 1종 `approved`; normal/mask/emission은 요청서대로 후속 5단계에서
  제작한다.

### Cap A initial generation

```text
Use case: stylized-concept
Asset type: production game environment texture, seamless tileable wall cap sprite
Primary request: Create one square reference-track wall top cap tile, variant A, for Tunnel Crew. It must read as quiet purple underground masonry with subtle magenta mineral seams and only a restrained hint of cyan industrial mineral influence; no pipes, machinery, doors, characters, props, or UI.
Style/medium: crisp high-quality pixel art, hand-authored game sprite look, hard pixel clusters, clean readable silhouette, no painterly blur, no anti-aliased edges, no text.
Composition/framing: orthographic top-facing 1x1 cell tile, centered, square 128x128 pixel target, full tile visible, transparent outside the tile silhouette. Make all four edges genuinely seamless: left/right and top/bottom edge content must match exactly in motif, tone, and value. Keep an 8px safe transparent/quiet padding inside the canvas boundary.
Lighting/mood: very weak baked self-shading only, unified light from screen upper-left at 35 degrees; no strong directional cast light, no bloom.
Color palette: match the viewed Tunnel Crew ReferenceCalibrationV1 calibration board: muted violet-purple stone, charcoal-violet crevices, small restrained magenta mineral flecks, cool cyan accents only as tiny reflected notes. Preserve the purple ambient cast around #9E80C2 without a flat tint.
Materials/textures: large quiet stone slabs with sparse hairline cracks and small edge chips; low contrast variation so A/B/C can mix in a grid without visible value stepping.
Constraints: production-ready albedo only; opaque tile interior with no black background; exact square footprint; no rotation, mirroring, or perspective; do not imitate a complete wall panel; do not bake strong scene lighting into the albedo.
Avoid: pipes, metal frames, machinery, doors, glowing neon crystals, heavy cracks, checkerboard patterns, borders, labels, lettering, watermark, vignette, drop shadow, black rectangle background.
```

### Cap A edge correction

```text
Use case: precise-object-edit
Asset type: production game environment texture, seamless wall cap sprite
Primary request: Edit the supplied wall cap texture into a true full-bleed tile. Preserve its purple stone slab layout, restrained magenta mineral flecks, tiny cyan reflected notes, pixel-art cluster style, and upper-left 35-degree weak self-shading. Remove the large transparent outer margin by extending the texture naturally to all four canvas edges.
Constraints: final image must be an opaque square tile interior, seamless on left/right and top/bottom with matching 4px edge pairs, quiet low-contrast albedo, no strong cast light, no blur, no anti-aliasing, no text, no machinery, no pipes, no watermark, no black background, no transparency at the tile edges. Keep it suitable for reduction to 128x128 pixels.
```

### Cap B / Cap C edits

```text
Use case: precise-object-edit
Asset type: production game environment texture, seamless wall cap sprite
Primary request: Using the supplied full-bleed purple masonry cap as the style anchor, create variant B with only a restrained hairline crack network and a few chipped slab edges added. Keep the same overall palette, material, density, average brightness, and upper-left 35-degree weak self-shading.
Constraints: opaque square full-bleed tile; left/right and top/bottom 4px edge pairs must be seamless; quiet violet stone with sparse magenta mineral flecks and tiny cyan reflected notes; no pipes, machinery, doors, characters, text, watermark, bloom, black background, transparency, rotation, mirroring, or strong scene lighting. Suitable for reduction to 128x128 albedo.
```

```text
Use case: precise-object-edit
Asset type: production game environment texture, seamless wall cap sprite
Primary request: Using the supplied full-bleed purple masonry cap as the style anchor, create variant C with a few subtle mineral-vein and structural-reinforcement accents integrated into the stone. Keep these accents sparse and quiet, not glowing and not brighter overall than the base.
Constraints: opaque square full-bleed tile; left/right and top/bottom 4px edge pairs must be seamless; keep the same muted purple palette, average value, slab density, and upper-left 35-degree weak self-shading; no pipes, machinery, doors, characters, text, watermark, bloom, black background, transparency, rotation, mirroring, or strong scene lighting. Suitable for reduction to 128x128 albedo.
```

### Front A / Front B / Front C generation

```text
Use case: stylized-concept
Asset type: production game environment texture, 1x1 cell wall front sprite
Primary request: Create variant A of a south-facing wall front tile for Tunnel Crew, a quiet purple stone wall face with a common upper rim, a subtle central reinforcement band, and a lower contact band. The wall face is exactly 128x128 target pixels and fills the square canvas edge-to-edge.
Style/medium: crisp high-quality pixel art, hard pixel clusters, clean sprite edges, no painterly blur, no anti-aliased edges, no text.
Composition/framing: orthographic front-facing 1x1 cell wall face, vertical stone slabs, bottom-center ground footpoint at approximately (64,128). Left/right edges must tile seamlessly with matching 4px pairs. The top 4px must visually connect to a cap underside.
Lighting/mood: weak baked self-shading only from screen upper-left at 35 degrees; no strong cast light or bloom.
Color palette: muted violet-purple stone and charcoal-violet seams from the Tunnel Crew ReferenceCalibrationV1 board, sparse restrained magenta mineral flecks, tiny cyan reflected notes, purple ambient cast around #9E80C2.
Materials/textures: calm masonry, common top rim, central vertical reinforcement, lower contact band, sparse quiet cracks and chips; low contrast.
Constraints: opaque full-bleed albedo, no transparent border, no black background, no pipes, no machinery, no door, no crystals dominating, no characters, no rotation, no mirroring, no watermark, no UI.
```

```text
Use case: stylized-concept
Asset type: production game environment texture, 1x1 cell wall front sprite
Primary request: Create variant B of a south-facing wall front tile for Tunnel Crew. Match the same overall design, palette, brightness, and three-part structure as a quiet purple masonry wall: common upper rim, subtle central reinforcement, lower contact band. Add one restrained diagonal crack across the stone slabs as the only notable variation.
Style/medium: crisp high-quality pixel art, hard pixel clusters, clean sprite edges, no painterly blur, no anti-aliased edges, no text.
Composition/framing: orthographic front-facing square 1x1 cell, exactly 128x128 target pixels, fills canvas edge-to-edge, bottom-center ground footpoint (64,128). Left/right edges tile seamlessly with matching 4px pairs. Top 4px must match a cap underside.
Lighting/mood: weak baked self-shading only from screen upper-left at 35 degrees; no strong cast light or bloom.
Color palette: muted violet-purple stone, charcoal-violet seams, sparse restrained magenta flecks, tiny cyan reflected notes, purple ambient cast around #9E80C2.
Constraints: opaque full-bleed albedo, no transparent border, no black background, no pipes, machinery, door, large glowing crystals, characters, rotation, mirroring, watermark, UI. Keep average value close to variants A and C.
```

```text
Use case: stylized-concept
Asset type: production game environment texture, 1x1 cell wall front sprite
Primary request: Create variant C of a south-facing wall front tile for Tunnel Crew. Match the same overall design, palette, brightness, and three-part structure as a quiet purple masonry wall: common upper rim, subtle central reinforcement, lower contact band. Add a restrained repaired metal plate and a few damp mineral marks integrated into the face, without making it brighter overall.
Style/medium: crisp high-quality pixel art, hard pixel clusters, clean sprite edges, no painterly blur, no anti-aliased edges, no text.
Composition/framing: orthographic front-facing square 1x1 cell, exactly 128x128 target pixels, fills canvas edge-to-edge, bottom-center ground footpoint (64,128). Left/right edges tile seamlessly with matching 4px pairs. Top 4px must match a cap underside.
Lighting/mood: weak baked self-shading only from screen upper-left at 35 degrees; no strong cast light or bloom.
Color palette: muted violet-purple stone, charcoal-violet seams, sparse restrained magenta flecks, tiny cyan reflected notes, purple ambient cast around #9E80C2.
Materials/textures: common upper rim, central reinforcement, lower contact band, small repaired plate with cool cyan-violet metal, subtle damp darkening; low contrast.
Constraints: opaque full-bleed albedo, no transparent border, no black background, no pipes, machinery, door, large glowing crystals, characters, rotation, mirroring, watermark, UI. Keep average value close to variants A and B.
```
