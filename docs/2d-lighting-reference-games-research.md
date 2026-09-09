# 2D 라이팅 / 환경 연출 레퍼런스 게임 리서치

작성: 2026-09-09 · 목적: 땅굴 크루 Unity 포팅의 라이팅·환경 연출 목표 수준과 기술 스택 결정을 위한 벤치마크 조사

> 엔진 정보 중 `※` 표시는 공개 자료로 100% 확인되지 않은 항목(업계 통설/추정)이다. 최종 의사결정에 쓸 땐 재확인 필요.

---

## 0. 먼저 알아야 할 개념 (2D 라이팅은 5개 부품의 조합)

2D 게임의 "라이팅이 좋다"는 말은 대부분 아래 5개가 동시에 잘 되어 있다는 뜻이다.

| 부품 | 하는 일 | Unity에서의 이름 |
|---|---|---|
| 1. 광원(Light) | 빛의 위치·색·반경·감쇠 | `Light 2D` (Point / Spot / Freeform / Sprite / Global) |
| 2. 그림자(Shadow) | 벽·오브젝트가 빛을 막음 | `Shadow Caster 2D` |
| 3. 표면 반응(Normal/Specular) | 평평한 스프라이트가 입체로 보이게 | 스프라이트 노멀맵 + Secondary Texture |
| 4. 대기(Atmosphere) | 안개, 먼지, 신의광선, 원근 | 파티클 + 패럴랙스 레이어 + 반투명 스프라이트 |
| 5. 후처리(Post) | 블룸, 색보정, 비네트, 그레인 | URP `Volume` (Bloom / Color Adjustments / Vignette) |

**우리 프로젝트(땅굴·지하·어둠)에서 체감 기여도 순서**: ② 그림자 → ⑤ 후처리(블룸) → ① 광원 → ④ 대기 → ③ 노멀.
즉 노멀맵을 만지기 전에 "그림자 캐스터 + 블룸 + 어둠 밀도"를 먼저 잡는 게 투자 대비 효과가 크다.

---

## A군. 직접 참고 — 지하 · 어둠 · 채굴 · 코옵 (땅굴 크루와 장르 근접)

| 게임 | 무엇을 배울 것인가 | 엔진 / 기술 스택 | Steam | YouTube |
|---|---|---|---|---|
| **Darkwood** (2017) | 2D 탑다운 라이팅의 교과서. 시야(FOV) 콘 + 그림자 오클루전 + 시야 밖 무채색화. 어둠을 게임 규칙으로 만든 사례 | Unity (커스텀 2D 라이트/섀도 + 시야 마스크 셰이더) | [274520](https://store.steampowered.com/app/274520/) · [Darkwood 2](https://store.steampowered.com/app/3708010/) | [게임플레이](https://www.youtube.com/results?search_query=Darkwood+gameplay+lighting) · [재현 튜토리얼](https://www.youtube.com/watch?v=XWMPEE8O05c) · [Unity 구현](https://www.youtube.com/watch?v=UbgVdrQ1zPE) |
| **Barotrauma** (2019) | 2D 측면 폐쇄공간 라이팅 최고 수준. 손전등·비상등·화재·물 굴절, 시야 차단. 멀티 동기화까지 해결 | 자체 C# 엔진 (MonoGame/XNA 계열, Farseer 물리) ※ | [602960](https://store.steampowered.com/app/602960/) | [게임플레이](https://www.youtube.com/results?search_query=Barotrauma+lighting+darkness+gameplay) |
| **Core Keeper** (2022) | 우리와 가장 유사한 포맷(지하 탑다운 채굴 + 8인 코옵). 캐릭터 착용 광원, 설치 횃불, 미탐험 구역의 완전한 검정 | Unity (DOTS/ECS 기반) ※ | [1621690](https://store.steampowered.com/app/1621690/) | [게임플레이](https://www.youtube.com/results?search_query=Core+Keeper+gameplay+lighting+cave) |
| **Dome Keeper** (2022) | 채굴 루프 + 지상/지하 대비 연출. 미니멀 광원으로 분위기 내는 절제 | **Godot 3.5.1** + GodotSteam | [1637320](https://store.steampowered.com/app/1637320/) | [게임플레이](https://www.youtube.com/results?search_query=Dome+Keeper+gameplay) |
| **Noita** (2020) | 픽셀 단위 물리 + 라이팅. 발광 물질, 실시간 그림자, 액체 반사. 기술적으로 가장 극단 | 자체 엔진 "Falling Everything" (C++) | [881100](https://store.steampowered.com/app/881100/) | [기술 발표](https://www.youtube.com/results?search_query=Noita+Falling+Everything+engine+GDC) |
| **Terraria** (2011) | 타일 기반 라이팅의 표준. 타일 단위 조명 전파(flood fill), 낮/밤, 바이옴별 색조 | XNA → **FNA** (C#) | [105600](https://store.steampowered.com/app/105600/) | [게임플레이](https://www.youtube.com/results?search_query=Terraria+lighting+cave+atmosphere) |
| **Starbound** (2016) | 셀룰러 라이팅 + 컬러 블리딩. 타일 게임인데도 부드러운 색번짐 | 자체 C++ 엔진 | [211820](https://store.steampowered.com/app/211820/) | [게임플레이](https://www.youtube.com/results?search_query=Starbound+lighting+cave) |
| **Don't Starve Together** (2016) | 어둠 = 죽음. 광원 반경을 자원으로 만든 설계 + 종이인형 스타일 조명 | 자체 엔진 (Lua 스크립팅, 3D 렌더 위 2D) | [322330](https://store.steampowered.com/app/322330/) | [게임플레이](https://www.youtube.com/results?search_query=Dont+Starve+Together+darkness+light) |
| **SteamWorld Dig 2** (2017) | 채굴 진행에 따른 조명 변화, 광원 아이템 | Unity ※ | [571310](https://store.steampowered.com/app/571310/) | [게임플레이](https://www.youtube.com/results?search_query=SteamWorld+Dig+2+gameplay) |
| **Drill Core** (2024) | 지하 굴착 + 어둠 관리 + 관리 시뮬. 최신 사례 | Unity ※ | [2821800](https://store.steampowered.com/app/2821800/) | [게임플레이](https://www.youtube.com/results?search_query=Drill+Core+game+gameplay) |
| **Necesse** (2024) | 탑다운 픽셀 지하 + 코옵. 저비용 라이팅으로 충분한 분위기 | 자체 Java 엔진 (LWJGL) ※ | [1169040](https://store.steampowered.com/app/1169040/) | [게임플레이](https://www.youtube.com/results?search_query=Necesse+gameplay+lighting) |
| **Duskers** (2016) | 정보 결핍 연출. CRT 후처리 + 극단적 비네트로 "안 보이는 공포" | Unity ※ | [254320](https://store.steampowered.com/app/254320/) | [게임플레이](https://www.youtube.com/results?search_query=Duskers+gameplay+atmosphere) |

---

## B군. 라이팅·환경 연출 예술성 최상위 (기법 훔칠 대상)

| 게임 | 배울 기법 | 엔진 / 기술 스택 | Steam | YouTube |
|---|---|---|---|---|
| **Sea of Stars** (2023) | **픽셀아트 100% 동적 라이팅**. 낮/밤·날씨·일식이 실시간으로 월드를 리컬러하고 그림자를 다시 그림. 픽셀아트 + 동적 조명의 최고 답안 | Unity + **자체 렌더 파이프라인** (공식 프레스킷 명시) | [1244090](https://store.steampowered.com/app/1244090/) | [Time of Day](https://www.youtube.com/watch?v=O0nKgv9By_g) · [동적 라이팅 프리뷰](https://www.youtube.com/watch?v=NAX_ilYEMXc) |
| **Ori and the Will of the Wisps** (2020) | 2D 환경 연출의 최고봉. 다층 패럴랙스 + 볼류메트릭 라이트 + 파티클 밀도 + 블룸 조율 | Unity (커스텀 셰이더/툴체인) | [1057090](https://store.steampowered.com/app/1057090/) | [비주얼](https://www.youtube.com/results?search_query=Ori+Will+of+the+Wisps+lighting+visuals) |
| **Hollow Knight** (2017) | 어두운 지하를 "예쁘게" 만드는 법. 안개 레이어, 실루엣 전경, 극소량 광원 + 강한 대비 | Unity | [367520](https://store.steampowered.com/app/367520/) | [비주얼](https://www.youtube.com/results?search_query=Hollow+Knight+atmosphere+lighting) |
| **Rain World** (2017) | **팔레트 기반 라이팅**. 광원 계산 대신 룸별 팔레트 이미지(R=음영/G=중간/B=하이라이트)로 색을 결정. 저비용으로 통일감 있는 분위기 | Unity + **Futile** (2D 프레임워크) + Level Color 셰이더 | [312520](https://store.steampowered.com/app/312520/) | [비주얼](https://www.youtube.com/results?search_query=Rain+World+visuals+atmosphere) |
| **GRIS** (2018) | 색채 자체가 조명. 수채화 레이어 + 그라디언트 | Unity | [683320](https://store.steampowered.com/app/683320/) | [비주얼](https://www.youtube.com/results?search_query=GRIS+game+visuals) |
| **Nine Sols** (2024) | 핸드드로운 + 동적 광원 결합. 실내 조명 배치의 정석 | Unity | [1809540](https://store.steampowered.com/app/1809540/) | [비주얼](https://www.youtube.com/results?search_query=Nine+Sols+visuals+lighting) |
| **Blasphemous 2** (2023) | 픽셀아트 + 라이팅/블룸. 픽셀 해상도와 광원 해상도 분리 문제의 해법 | Unity | [2114740](https://store.steampowered.com/app/2114740/) · [1편](https://store.steampowered.com/app/774361/) | [비주얼](https://www.youtube.com/results?search_query=Blasphemous+2+visuals+lighting) |
| **Dead Cells** (2018) | 픽셀아트에 동적 광원·발광·화면흔들림을 얹은 "게임필" 연출. 조명이 타격감의 일부 | **Haxe + Heaps** (자체 엔진) | [588650](https://store.steampowered.com/app/588650/) | [VFX 분석](https://www.youtube.com/results?search_query=Dead+Cells+visual+effects+juice+breakdown) |
| **Children of Morta** (2019) | 픽셀 탑다운 던전 + 따뜻한 광원. **우리 카메라 앵글과 가장 유사한 조명 배치** | Unity ※ | [330020](https://store.steampowered.com/app/330020/) | [비주얼](https://www.youtube.com/results?search_query=Children+of+Morta+lighting+visuals) |
| **INSIDE** (2016) | 2.5D 라이팅·안개·색보정의 결정판. 화면당 광원 몇 개로 이 퀄리티를 냄 | Unity (커스텀 렌더링/후처리) | [304430](https://store.steampowered.com/app/304430/) | [비주얼](https://www.youtube.com/results?search_query=INSIDE+Playdead+lighting+visuals) |
| **Little Nightmares** (2017) | 손전등 광원 하나로 씬을 지배. 그림자 연출 | **Unreal Engine 4** | [424840](https://store.steampowered.com/app/424840/) · [2편](https://store.steampowered.com/app/860510/) | [비주얼](https://www.youtube.com/results?search_query=Little+Nightmares+lighting) |
| **Planet of Lana** (2023) | 2.5D 회화적 환경 + 대기 원근 | Unity ※ | [1608230](https://store.steampowered.com/app/1608230/) | [비주얼](https://www.youtube.com/results?search_query=Planet+of+Lana+visuals) |
| **SIGNALIS** (2022) | 저해상도 + 강한 후처리(디더링·스캔라인·비네트)로 만든 분위기. **적은 에셋으로 최대 무드** | Unity | [1262350](https://store.steampowered.com/app/1262350/) | [비주얼](https://www.youtube.com/results?search_query=SIGNALIS+visuals+atmosphere) |
| **Ultros** (2024) | 형광·사이키델릭 팔레트. 어두운 배경 위 강한 발광 | Unity ※ | [2386310](https://store.steampowered.com/app/2386310/) | [비주얼](https://www.youtube.com/results?search_query=Ultros+game+visuals) |
| **ENDER LILIES** (2021) | 물·안개·역광. 습한 지하 표현 | Unreal Engine 4 ※ | [1369630](https://store.steampowered.com/app/1369630/) | [비주얼](https://www.youtube.com/results?search_query=Ender+Lilies+visuals) |
| **Aeterna Noctis** (2021) | 픽셀아트 + 노멀맵 동적 라이팅을 정면으로 시도한 사례 | Unity ※ | [1517970](https://store.steampowered.com/app/1517970/) | [비주얼](https://www.youtube.com/results?search_query=Aeterna+Noctis+lighting) |
| **Salt and Sanctuary** (2016) | 어두운 2D + 횃불 연출 | 자체 엔진 (MonoGame/XNA 계열) ※ | [283640](https://store.steampowered.com/app/283640/) | [비주얼](https://www.youtube.com/results?search_query=Salt+and+Sanctuary+visuals) |
| **Sundered** (2017) | 손그림 2D + 동적 광원/파티클 과포화 | Unity ※ | [535480](https://store.steampowered.com/app/535480/) | [비주얼](https://www.youtube.com/results?search_query=Sundered+visuals+lighting) |
| **Teslagrad** (2013) | 초기 Unity 2D 라이팅 레퍼런스. 실루엣 + 단색 광원 | Unity | [249590](https://store.steampowered.com/app/249590/) · [2편](https://store.steampowered.com/app/1698220/) | [비주얼](https://www.youtube.com/results?search_query=Teslagrad+visuals) |
| **Katana ZERO** (2019) | 네온·CRT·색보정으로 만든 무드 (광원은 최소) | **GameMaker Studio** | [460950](https://store.steampowered.com/app/460950/) | [비주얼](https://www.youtube.com/results?search_query=Katana+Zero+visuals+neon) |
| **Hyper Light Drifter** (2016) | 팔레트 제한 + 블룸만으로 만든 탑다운 무드 | **GameMaker Studio** | [257850](https://store.steampowered.com/app/257850/) | [비주얼](https://www.youtube.com/results?search_query=Hyper+Light+Drifter+visuals) |
| **Return of the Obra Dinn** (2018) | 1비트 디더링 — "라이팅을 포기하고 스타일로 이기는" 극단 사례 | Unity (커스텀 디더 셰이더) | [653530](https://store.steampowered.com/app/653530/) | [기술 해설](https://www.youtube.com/results?search_query=Obra+Dinn+1-bit+dithering+technique) |
| **BELOW** (2018) | 어둠·랜턴·미니멀 UI (Steam 앱ID 조회 실패 → 검색 링크) | Unity ※ | [Steam 검색](https://store.steampowered.com/search/?term=BELOW+Capybara) | [비주얼](https://www.youtube.com/results?search_query=BELOW+Capybara+game+lantern+darkness) |
| **Cuphead** (2017) | 라이팅이 아니라 **프레임 애니메이션**으로 승부한 대조 사례 | Unity | [268910](https://store.steampowered.com/app/268910/) | [비주얼](https://www.youtube.com/results?search_query=Cuphead+animation+technique) |

---

## C. 기술 스택 총정리 — 무엇을 쓰고 있나

| 스택 | 대표작 | 특징 | 땅굴 크루 적용성 |
|---|---|---|---|
| **Unity URP 2D Renderer** (Light 2D + Shadow Caster 2D + Volume) | Sea of Stars, Nine Sols, Blasphemous 2, Hollow Knight, Darkwood | 공식 지원, 문서·샘플 풍부. 노멀맵/Secondary Texture 지원 | **○ 현재 우리 선택. 정답에 가깝다** |
| Unity + 자체 렌더 파이프라인 | Sea of Stars, INSIDE, Obra Dinn | 픽셀 정합·특수 룩을 위해 직접 작성 | △ URP 한계에 부딪힐 때 부분 커스텀 셰이더로 |
| Unity + Futile / 팔레트 셰이더 | Rain World | 광원 계산 없이 팔레트로 무드 통일 | **○ 저비용 대안. 층·구역별 팔레트 전략은 즉시 차용 가능** |
| Godot 2D Light + Occluder | Dome Keeper | 가볍고 셋업 빠름 | ✕ (엔진 이전 불필요) |
| 자체 C++ 엔진 | Noita, Starbound | 픽셀 단위 시뮬 가능, 개발비 폭증 | ✕ |
| Haxe + Heaps | Dead Cells | 픽셀 게임필 특화 | ✕ |
| GameMaker + 후처리 | Katana Zero, Hyper Light Drifter | 광원 없이 팔레트/블룸만 | ✕ (기법만 차용) |
| XNA/FNA 타일 라이팅 | Terraria | 타일 flood-fill 조명 전파 | **○ 알고리즘 차용 가치 있음 (땅굴 타일 기반 광량 전파)** |

---

## D. 결론 — 우리가 취할 3가지

1. **URP 2D Renderer 유지가 맞다.** Sea of Stars·Nine Sols·Darkwood가 전부 같은 계열이며, 목표 룩은 이 스택 안에서 도달 가능하다. 엔진 교체 검토는 불필요.
2. **먼저 잡을 것은 "어둠의 밀도"와 그림자다.** 노멀맵/스페큘러는 3순위. Global Light 강도 + Shadow Caster 2D + Bloom 세 값만으로 체감 품질의 대부분이 결정된다 (Hollow Knight·SIGNALIS가 증거: 광원 수는 적다).
3. **Rain World식 "구역 팔레트"를 층 시스템에 붙일 것.** 깊이(층)별로 팔레트를 갈아끼우면 광원을 늘리지 않고도 "다른 세계에 왔다"는 느낌을 만들 수 있다.

---

## E. 실습용 학습 자료 (기초부터)

- Unity 공식 — [URP 2D 라이팅 소개](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/Lights-2D-intro.html) / [2D 라이팅 전체 문서](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/2d-index.html)
- Unity How-to — [2D 라이트로 무드 만들기](https://unity.com/how-to/use-2d-lights-unity-set-mood) / [URP 2D 라이트·섀도 기법](https://unity.com/how-to/2d-light-shadow-techniques-in-the-universal-render-pipeline)
- Unity at GDC 2023 — [탑다운 게임에 URP 2D 라이트 적용](https://www.youtube.com/watch?v=YhrwKF_i-BI) ← **우리 카메라 앵글과 동일, 최우선 시청**
- 노멀맵 실습 — [스프라이트 노멀맵 + 2D 라이트](https://www.youtube.com/watch?v=kpt7Ft5y8v4) / [픽셀아트 동적 라이팅](https://www.youtube.com/watch?v=vOXrrEvYUVg) / [픽셀 퍼펙트 라이트·파티클](https://www.youtube.com/watch?v=2qeNu2QApAM)
- 이론 — [2D Lighting Techniques (Slembcke)](https://www.slembcke.net/blog/2DLightingTechniques/) / [GameMaker 노멀맵 라이팅 해설](https://gamemaker.io/en/blog/using-normal-maps-to-light-your-2d-game)
- Unity 샘플 프로젝트 — **Happy Harvest** (탑다운 2D, URP 라이트/섀도 전면 사용). Asset Store에서 무료로 받아 씬을 직접 열어보는 게 가장 빠른 학습
