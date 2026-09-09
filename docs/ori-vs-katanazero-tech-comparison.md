# Ori vs Katana ZERO — 라이팅·환경 기술 스택 비교분석

작성: 2026-09-09 · 선행 문서: `docs/2d-lighting-reference-games-research.md`
목적: 땅굴 크루가 목표로 삼을 룩의 **원가 구조**를 확정한다. 두 게임은 같은 인상을 정반대 방법으로 만든다.

> `※` = 공개 자료로 확정되지 않은 추론. 근거를 함께 적었다.

---

## 0. 한 줄 결론

| | Ori | Katana ZERO |
|---|---|---|
| 라이팅을 만드는 주체 | **아티스트의 손** | **화면 후처리 셰이더** |
| 원가가 붙는 곳 | 에셋 수 × 라이트맵 장수 | 셰이더 1회 작성 (에셋 수와 무관) |
| 검증된 규모 | 80명급 · Microsoft 퍼블리싱 · 5년+ | **1인 + 교체되는 픽셀 아티스트 · $60,000 · 6년** |
| 땅굴 크루가 복제 가능한가 | ✕ 통째로는 불가 / △ 원리는 축약 가능 | **○ 거의 그대로 가능** |

**Ori의 그 빛은 Unity 설정이 아니다.** 에셋 7,000개에 대해 **손으로 칠한 라이트맵 30,000장**이 만든 결과다. 그리고 Moon Studios는 이걸 자동화(노멀맵 자동 생성)로 대체하려다 "싸고 플라스틱 같은 룩"이 나와서 **의도적으로 수작업을 선택**했다. 즉 Ori의 룩은 기술로 사는 게 아니라 아트 노동으로 사는 것이다.

**Katana ZERO의 그 무드는 광원이 아니다.** 제한된 네온 팔레트 + 알베도에 구워진 빛 + 전체화면 VHS/CRT 왜곡이다. 심지어 네온 팔레트를 고른 이유가 미학이 아니라 **"교체되는 아티스트들의 서로 다른 스타일을 뭉개서 하나로 보이게 하려고"** 였다. 소규모 팀을 위한 방법론 그 자체다.

---

## 1. 스택 항목별 비교

| 항목 | Ori (Blind Forest 2015 / Will of the Wisps 2020) | Katana ZERO (2019) |
|---|---|---|
| **엔진** | Unity. 프로토타입은 Construct → 1주만에 Unity 이식 → Unity 3 시작 → 레벨 스트리밍 때문에 Unity 5로 이주. WotW는 **Unity 소스 코드 접근 라이선스**로 자체 코드를 엔진에 직접 통합 | **GameMaker Studio 2** |
| **렌더 파이프라인** | 빌트인 + 대규모 자체 수정. **URP 아님** ※ (근거: Blind Forest 2015 출시는 URP 이전, WotW는 "modified Unity engine"으로 기술) | GameMaker 기본 렌더러 + GLSL ES 셰이더 |
| **셰이더 관리** | **"Ubershader" 프레임워크** 자체 구축 — 셰이더 순열(permutation) 자동 생성, 기능 모듈식 조합, 라이팅·애니메이션 확장 지점 | 개별 셰이더 소수. 핵심은 전체화면 후처리 1~2종 |
| **씬 구성** | **해상도가 서로 다른 다층 2D 평면 + 그 사이에 끼워 넣은 3D 오브젝트**. WotW에서 3D 캐릭터 도입. 2.5D "그림이 살아난다" 방식 | 단일 평면 2D 픽셀. 배경/전경 레이어 소수 |
| **카메라** | 원근(perspective) — 층 사이 실제 깊이가 있어야 패럴랙스가 성립 ※ | 정사영(orthographic) 픽셀 정합 |
| **광원 (동적)** | WotW의 최대 기술 진보 항목이 **"3D 캐릭터 · 동적 조명 · 물리"**. "동적 회화적 라이팅 엔진(dynamic painterly lighting engine)"을 대폭 개선 | 거의 없음. Wikipedia는 "게임플레이에 동기화된 동적 조명 효과"라고만 기술 — 방을 밝히는 광원이 아니라 이벤트 연출 |
| **그림자** | 라이트맵에 **칠해져 있다** ("에셋 그룹 전체를 잡아서 그 위에 빛과 그림자를 칠한다" — 리드 아티스트 Daniel van Leeuwen) + 동적 라이팅이 그 위에 얹힘 | 실질적으로 없음. 아트에 포함 |
| **표면 반응 (노멀)** | 에셋마다 **라이트 디렉션 맵(light direction map)** 을 손으로 칠함. 에셋 약 7,000개 → **라이트맵 약 30,000장**. 자동 스크립트·노멀맵 자동 생성은 *"very cheap [and] plasticky look"* 이라 **명시적으로 폐기** | 없음. 픽셀 명암이 스프라이트에 직접 그려짐 |
| **대기 연출** | 정교한 패럴랙스, **GPU 시뮬레이션 물**, 모션 블러, 파티클 밀도, 환경의 반응형 물리 애니메이션 | 파티클 시스템 + 화면 흔들림 |
| **후처리** | 블룸 · 모션 블러 중심. 화면을 "왜곡"하지 않음 — 회화적 사실감 방향 | **정체성 그 자체**. 커스텀 셰이더로 라인 스윕(주사선 훑기) + 색수차(chromatic shift) → VHS 테이프 고장 재현. CRT 왜곡을 주인공의 정신상태 서술 장치로 사용 |
| **에디터 툴링** | Unity 에디터를 "Ori 프로덕션 엔진"으로 개조: 커스텀 인스펙터, **멀티 씬 동시 편집**(끊김 없는 연속 월드용, 이후 시네마틱 툴로 확장), 전문 편집 소프트웨어급 시네마틱 툴, 아트를 에디터에서 직접 빌드·프리뷰·애니메이트 | GameMaker 기본. 특기 사항 없음 |
| **성능 목표** | 로딩 화면 없는 연속 월드 · 상시 60fps. Series X에서 네이티브 4K 120fps 달성 | Switch 이식이 "즉시" 가능할 만큼 가벼움 (GameMaker 이식성) |
| **팀·예산** | Moon Studios (2009년부터 Unity), 분산 대형 팀, Microsoft 퍼블리싱 | Justin Stander **1인**(디자인·프로그래밍 전담) + 교체되는 픽셀 아티스트. **$60,000**, 2013~2019 6년 |

---

## 2. "그 느낌"의 실제 부품 — 해체

### Ori의 인상을 만드는 5개
1. **해상도가 다른 다층 평면 + 사이에 끼운 3D** — 깊이가 물리적으로 존재한다. 후처리로 흉내낼 수 없는 부분.
2. **손칠 라이트 디렉션 맵** — 빛이 "계산된" 게 아니라 "그려진" 것. Ori 룩의 90%.
3. **볼류메트릭 광선 / 신의광선** — 빛 자체를 형태 있는 오브젝트로 둠.
4. **끊임없는 파티클** — 화면에 항상 먼지·포자·불티가 떠 있어 공기가 있다고 느끼게 함.
5. **색온도 대비** — 차가운 배경 / 따뜻한 초점. 블룸은 그 대비를 증폭하는 데만 씀.

### Katana ZERO의 인상을 만드는 5개
1. **극단적 제한 팔레트 + 네온** — 씬당 색 3~4개. *채택 이유가 "아티스트 스타일 차이를 뭉개기"* 였다는 게 핵심.
2. **알베도에 구워진 빛** — 스프라이트가 이미 조명된 상태로 그려짐. 런타임 광원 불필요.
3. **애디티브 글로우 스프라이트** — 간판·무기 궤적·핏방울. 저비용 고효과.
4. **전체화면 왜곡 셰이더** — 주사선 스윕 + 색수차 + 글리치. **에셋 수와 무관하게 전체 화면의 룩을 한 번에 결정.**
5. **히트스톱·화면 흔들림** — 조명이 아니라 타이밍인데, 결과적으로 "번쩍인다"는 인상을 만든다.

---

## 3. 땅굴 크루 현황 대조

현재 `Assets/_Project/Presentation/` 에 이미 들어있는 것:

| 우리 자산 | 대응하는 레퍼런스 |
|---|---|
| `Settings/Renderer2D.asset` — Multiply / Additive / (+Mask) 4종 블렌드 스타일 | 두 게임 모두의 기반. 애디티브가 Katana ZERO 글로우의 부품 |
| `Lighting/Darkness.shader` · `DarknessOverlay.cs` | **어둠 밀도** = 결론 2번의 1번 손잡이 |
| `Visual/Lighting/ShadowGeometryBuilder.cs` · `WallShadowBuilder.cs` · `ContactShadowRenderer.cs` | **그림자** = 결론 2번의 2번 손잡이 |
| `Visual/Lighting/AtmosphereProfile.cs` — fogDensity / depthSeparation / vignette / grain | Ori의 대기 + Katana ZERO의 후처리, **둘 다** 여기서 나온다 |
| `Visual/Lighting/LightClass.cs` — 광원 5분류 + `LightNormalQuality` + 그림자 예산 | Ori식 "광원 종류마다 다른 취급" 구조. 이미 있음 |
| `Visual/Depth/` + `WorldVisualProfile` | Ori의 다층 평면 개념의 2D 축약판 |

**판정: 구조는 이미 Ori 쪽에 가깝게 짜여 있다. 비어 있는 건 기술이 아니라 "빛이 칠해진 아트"다.**

---

## 4. 그래서 무엇을 가져올 것인가

### Ori에서 가져올 것 — 단, 30,000장이 아니라 지층 수만큼
Ori의 원리는 "에셋마다 라이트 방향을 안다"이다. 이걸 우리 규모로 축약하면:

> **지층(구역)마다 고정 광원 방향 1개를 정하고, 그 방향의 빛과 그림자를 아트에 칠해서 들여온다.**

에셋 7,000개 × 4~5장이 아니라, 지층 N개 × 방향 1개다. 아트 계약(`art-production/.../production-spec.md`)에 "이 지층의 광원 방향은 좌상단 35°" 같은 한 줄을 넣는 것으로 끝난다. 런타임 광원은 그 위에 **강조로만** 얹는다 — 현재 `AtmosphereProfile` 주석에 이미 같은 원칙("후처리는 형태를 대체하지 않는다")이 적혀 있으니 사상은 일치한다.

### Katana ZERO에서 가져올 것 — 즉시 가능
1. **지층별 제한 팔레트.** 색 3~4개로 구역을 통일. Rain World식 팔레트 전략과 같은 결론이고, 아트 인력이 여러 명일 때 스타일 차이를 가려주는 실무 효과까지 동일하다.
2. **`AtmosphereProfile`의 grain / vignette / depthSeparation을 지금보다 과감하게.** 현재 기본값(grain 0.018, vignette 0.35)은 매우 보수적이다. Katana ZERO·SIGNALIS는 이 수치대에서 정체성을 만든다.
3. **`MineralGlow` 분류 = 애디티브 글로우 스프라이트.** 그림자 없이 발광만. 가장 값싸게 "빛나 보이는" 부품이고 분류가 이미 있다.

### 하지 말 것
- **자동 생성 노멀맵에 기대기.** Ori 팀이 직접 걸어보고 폐기한 길이다("plasticky"). `LightNormalQuality.Accurate`를 넓게 켜는 것도 같은 함정 — 비용은 들고 결과는 싸 보인다. 노멀은 `Scout`(손전등) 같은 소수 광원에만.
- **광원 개수로 승부하기.** Katana ZERO는 광원이 거의 없고, Ori조차 "동적 조명"은 손칠 아트 **위에** 얹는 보조였다.

---

## 5. 다음 단계 — 결론 2번 실습 설계

`CaptureVisualLab` 이 이미 있으니, 3개 손잡이를 격자로 스윕해 컨택트 시트를 만든다.

| 손잡이 | 위치 | 스윕 값 |
|---|---|---|
| 어둠 밀도 | `DarknessOverlay` / Global Light 세기 | 최소광 3단 (짙음 / 중간 / 옅음) |
| 그림자 | `ShadowGeometryBuilder` on/off + 그림자 예산 | 없음 / 접촉만 / 접촉+벽 |
| 블룸·후처리 | `AtmosphereProfile` (grain·vignette·depthSeparation) | 보수(현재값) / 중간 / 과감(Katana ZERO급) |

3×3×3 = 27장이면 감을 잡는 데 충분하고, 그중 마음에 드는 3장을 골라 값을 확정한다. **이게 라이팅 지식 없이 시작할 때 가장 빠른 경로다** — 수치를 이해하고 고르는 게 아니라, 결과를 보고 고른 뒤 값을 읽는다.

---

## 출처

- [MCV — Unity Focus: Making Ori and the Blind Forest](https://mcvuk.com/development-news/unity-focus-making-ori-and-the-blind-forest/) (Ubershader, 멀티 씬 편집, Unity 3→5, GPU 물, 2.5D)
- [TheGamer — Ori and the Will of the Wisps Artists Hand-Painted Over 30,000 Light Maps](https://www.thegamer.com/ori-and-the-will-of-the-wisps-artists-hand-painted-30000-light-maps/) (7,000 에셋 / 30,000 라이트맵 / "plasticky" 인용)
- [Unity — How Ori: Will of the Wisps scaled for multiple platforms](https://create.unity.com/moon-studios-case-study) (소스 접근, 4K 120fps)
- [Wikipedia — Ori and the Will of the Wisps](https://en.wikipedia.org/wiki/Ori_and_the_Will_of_the_Wisps) / [Ori and the Blind Forest](https://en.wikipedia.org/wiki/Ori_and_the_Blind_Forest) / [Moon Studios](https://en.wikipedia.org/wiki/Moon_Studios)
- [Wikipedia — Katana Zero](https://en.wikipedia.org/wiki/Katana_Zero) (GMS2, $60,000, 6년, 네온 채택 이유 인용)
- [GameMaker — Katana ZERO showcase](https://gamemaker.io/en/showcase/katana-zero)
- [Red Bull — Justin Stander 인터뷰](https://www.redbull.com/us-en/katana-zero-developer-justin-stander-interview) / [MCV — protracted development](https://mcvuk.com/business-news/askiisoft-katana-zero/)
- [GDC 2015 — Animating Ori and the Blind Forest (James Benson) 정리](https://zyzyz.github.io/en/2018/01/GDC2015-Animating-Ori/)
- [DualShockers — 10 Games With A Built-In CRT Filter](https://www.dualshockers.com/10-games-built-in-crt-filter/) (Katana ZERO의 CRT 왜곡을 서술 장치로 쓰는 방식)
