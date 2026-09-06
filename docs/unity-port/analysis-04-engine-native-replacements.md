# 땅굴 크루 Unity 포팅 — 엔진 네이티브 대체 분석 (HTML 임시 구현 → Unity 표준 제작법)

> 작성: 2026-09-06 · 기준 빌드: `tunnel-crew-infinite-mode-v7.9.2.html`
> 상위 문서: [`../unity-port-plan.md`](../unity-port-plan.md) · 자매 문서: [코어 시뮬레이션](analysis-01-core-sim.md) / [엔티티·전투·AI](analysis-02-entities-combat-ai.md) / [셸·오디오·네트·자산](analysis-03-shell-audio-net-assets.md)
> 상태: **분석 초안.** 이 문서의 결론은 상위 계획 §1 결정(D3·D5·D6)과 §5 마일스톤 M1~M7에 반영한다(§12).

---

## 0. 이 문서의 원칙

HTML 프로토타입은 "브라우저에서 당장 되는 것"으로 만족하며 빠르게 만들어 왔다. Unity로 옮기는 목적은 프로토타이핑을 끝내고 프로덕션 품질의 게임을 만드는 것이다. 따라서 이식의 기준을 다음처럼 둔다.

| 구분 | 뜻 | 예 |
|---|---|---|
| **보존** | 플레이 규칙, 밸런스 수치, 플레이어가 보고 듣는 결과 | 장악도 22~34%에 보스, 드릴 예열 곡선, 어둠 속 30% 기억 잔존, 손전등 원뿔 28° |
| **대체** | 결과는 같아야 하지만 방법은 Unity 표준으로 새로 만드는 것 | Canvas2D 확대·축소 차분 그림자 → Shadow Caster 2D, 보스 등장 DOM 오버레이 → Timeline |
| **폐기** | HTML·브라우저 제약 때문에만 존재했던 것 | base64 인라인 자산, 전역 `cx` 스왑, 런타임 함수 래핑, 자동 품질 강등, `<audio>` 자동재생 우회 |

판단 질문은 하나다. **"이 코드는 게임 규칙인가, 아니면 브라우저에서 그 규칙을 흉내 내기 위한 우회였는가?"** 우회였다면 Unity 방식으로 다시 만든다. 원본 코드를 한 줄씩 C#으로 번역하는 일은 이 문서가 다루는 영역(표현 계층)에서는 하지 않는다. 시뮬레이션 규칙(계획 §2.1)은 그대로 보존한다.

---

## 1. 한눈에 보는 대체표

| 영역 | HTML 임시 구현 (원본 위치) | Unity 대체 | 얻는 것 |
|---|---|---|---|
| 광원 | 광원별 쿼드 + 프래그먼트 감쇠, 예산 6+8개 (`FOW.stamp` 1714) | **URP 2D Renderer · Light2D** (Global / Point / Spot / Freeform) | 광원 수 제한 없음, 노멀맵 표준 지원, 블렌드 스타일 4종 |
| 벽 그림자·림 | 벽 이미지 방사 확대·축소 차분 (`LIT.beginWalls` 7872) | **Shadow Caster 2D** + 스프라이트 림 셰이더 | 광원 수만큼 자동, 소프트 섀도 옵션, 타일 수 무관 |
| 시야(LOS) 어둠 | 360 레이 → RGBA 픽셀 버퍼 → WebGL 텍스처 (`LOS` 5164) | 레이캐스트 → **가시 폴리곤 메시**를 RenderTexture에 렌더, 탐색 기억은 누적 RT, 풀스크린 패스 1개 | 픽셀 버퍼 CPU 작성 제거, 해상도 무관, 부드러운 경계 |
| LX 4레이어 합성 | 풀스크린 셰이더 4패스 + Canvas 블렌드 모드 (`FOW.composite` 1752) | **Volume 포스트프로세싱** (Color Adjustments · Tonemapping · Bloom · Vignette · Film Grain) + 어둠 패스 1개 | 지층별 Volume 블렌딩, 인스펙터 튜닝, 디더는 Film Grain으로 |
| 타일 렌더 | 셀 캐시 `_BS` + 매 프레임 drawImage, vib/nudge 오프셋 (8158~8251) | **Tilemap** 2~3장 + 커스텀 `TileBase`, SpriteAtlas | 컬링·배칭 자동, 손상 단계는 `RefreshTile` |
| 스프라이트 조명 | 실루엣 multiply/screen 합성 (`LIT.spr`) | **Sprite-Lit** 셰이더 + 노멀맵(Secondary Texture) | 광원 방향 자동 반응 |
| 히트 플래시·상태 틴트 | 드로우 시 색 덧칠 (`e.hurt`, frozen) | **Shader Graph** 스프라이트 셰이더 프로퍼티 (`_Flash`, `_Tint`, `_Dissolve`) | 스프라이트당 MaterialPropertyBlock, 배칭 유지 |
| 파티클 12종 `J` | 배열 직접 적분 + 원·사각 드로우 (4878) | **Particle System(Shuriken)** 프리셋 + 대량 잔해는 **VFX Graph** | 서브이미터·트레일·충돌 기본 제공 |
| 화면 흔들림·킥 | `J.shake/sdx/sph` 수식 (8074) | **Cinemachine Impulse** | 방향 임펄스, 감쇠 프로파일, 노이즈 |
| 히트스톱 | 메인 루프 dt×0.055 (9661) | `Time.timeScale` 기반 히트스톱 (UI는 unscaled) | 60Hz 틱과 독립, 12ms 짧은 멈춤도 보존 |
| 스쿼시·잔상 | `FEEL.transform/drawGhosts` (4838) | **DOTween** 펀치 스케일 + 잔상 스프라이트 풀 (자체 Feedback 컴포넌트에 포함) | 코드 몇 줄, 이징 커브 선택 |
| 카메라 | 데드존·룩어헤드·동적 줌·클램프 수작업 (7364~7402) | **Cinemachine 3** Position Composer + Target Group + Confiner 2D + Impulse | 원본 4단계가 컴포넌트 4개에 1:1 대응 |
| 보스 등장·사망 시네마틱 | DOM 오버레이 + 수동 tick + `CREW.phase` 정지 (15854, 16089) | **Timeline** + Cinemachine 카메라 블렌드 + Signal(포효 프레임) | 편집기에서 타이밍 조정, 애니 프레임 동기 자동 |
| 화면 전환 와이프 | DOM 시트 3장 + 클릭 캡처 위임 (15642) | 풀스크린 와이프 셰이더(Shader Graph) + Timeline 또는 UI 애니 | 씬 로드와 결합 |
| 8방향 캐릭터 애니 | 시트 슬라이스·피벗 표·좌우 반전 수작업 (2824~3020) | **Animator** + 2D Freeform Directional 블렌드 트리, 임포트 시 피벗 확정 | 방향·속도 파라미터 2개, Animation Event |
| 몬스터·보스 애니 | `animT` 수동 프레임 인덱싱 (2134~2154) | Animator 클립 + Animation Event (포효 SFX 등) | `animT>=1.4` 같은 프레임 조건 제거 |
| 충돌·이동 | 원-AABB 3회 반복 + 4px 서브스텝 + 35% 탈출 (`collide` 6166) | **Physics2D**: Kinematic Rigidbody2D + `Cast/MovePosition`, Tilemap Collider + Composite, Continuous CD | 터널링·분리·쿼리 엔진 위임 (§6 상세) |
| 소프트 분리 | `separateEnemies/Players` 수식 (6635) | 콜라이더 반경 ×0.86 + Physics2D 레이어 매트릭스 | 질량·보스 불가침 규칙은 Rigidbody 타입으로 |
| 전리품 물리 | z축 포물선·바운스 수식 | 가짜 z는 스크립트 유지, 획득·자석은 **Trigger Collider** | 거리 스캔 루프 제거 |
| 잔해·혈흔·자취 데칼 | 배열 `rub/gore/fdec/trail` + 수명 (5498) | 잔해는 Rigidbody2D 소량, 얼룩은 **바닥 RenderTexture에 페인팅** | 영구 흔적이 비용 0 |
| 텔레그래프(보스탄·돌진) | 원·캡슐 그라디언트 드로우 (7035, 13460) | Shader Graph 방사 채움 쿼드 + 캡슐 스프라이트 | 색·두께·펄스가 머티리얼 프로퍼티 |
| 투사체 트레일·레이저 | `trail[]` 점 배열, 선 드로우 | **TrailRenderer** / **LineRenderer** + 스크롤 셰이더 | |
| HUD·메뉴 | DOM/CSS (box-shadow 112, filter 111, backdrop-filter 12, transition 86) | **UGUI + TextMeshPro** + 9-slice 스프라이트 + DOTween. 유리(backdrop 블러) 효과는 **폐기**, 모달 뒤는 반투명 딤 | 게임패드 내비게이션, 해상도 독립, 월드 스페이스 캔버스 |
| 미니맵 | 220×190 캔버스 drawMini | `Texture2D.SetPixels32` 1장 (그리드가 데이터라 카메라 불필요) | |
| 정산 노드 지도 | SVG 링크 + DOM 노드 (14028) | UGUI 노드 프리팹 + UI 라인 (Unity UI Extensions `UILineRenderer`) | 팬·줌은 ScrollRect |
| 폰트 | Pretendard → 시스템 폴백 | TMP **SDF 폰트 에셋**(Pretendard, 한글 2,350자 + 동적 폴백) | 외곽선·언더레이·그라디언트 머티리얼 프리셋 |
| 오디오 버스 | WebAudio 게인 트리 + WaveShaper 리미터 (`AU` 3327) | **Audio Mixer** 그룹(dig/brk/combat/ui/loot/alert/music) + Compressor/Limiter + **Snapshot**(calm/combat/boss) | 덕킹·전환 곡선 인스펙터 |
| SFX 뱅크 | base64 25종 + 절차 합성 17종 (`SFX`, `MENU_SFX.bank`) | `SfxBank` ScriptableObject(변형 배열·게인·피치 지터) + 합성음은 **WAV로 베이크** | 파일 하나 바꾸면 소리 교체 |
| BGM 라우터 | `<audio>` data URI + revision 카운터 (4352) | AudioSource 2개 크로스페이드 + Mixer Snapshot | 자동재생 정책 우회 코드 전부 삭제 |
| 입력 | `KEY Set` + 캡처 단계 가로채기 | **Input System** 액션맵 5종 + 리바인딩 UI | 게임패드 기본 |
| 자산 로딩 | 단일 HTML 인라인 / 즉시 로드 | **Addressables** + 로딩 화면(`assets/loading` 4장) | 메모리·시작 시간 관리 |
| 튜닝 패널 | F8 UILAB · F10 LX · Boss Lab · `#pSet` · `DEMO` 190필드 | **ScriptableObject** + 인스펙터(Play 모드 라이브 수정) + 빌드 플래그 뒤 디버그 메뉴 1개 | 저장·핀·되돌리기가 에셋 버전관리로 |
| 저장 | localStorage 키 12개 | JSON → `persistentDataPath`, 스키마 `tc_infinite_meta_v1` 유지 | |
| 코옵 | Node 릴레이 + HTML 서빙 + 방 코드 | **Netcode for GameObjects** + Unity Transport(+Relay/Lobby) | 서버 배포·포트 탐색·file:// 분기 삭제 (M8) |

---

## 2. 시각 — 조명 · 어둠 · 시야

원본 화면 인상의 절반은 어둠과 빛이다. 세 시스템(LOS · FOW · LIT)이 겹쳐 있던 것을 Unity에서는 **Light2D + 어둠 패스 1개 + Volume** 세 층으로 재구성한다. 보존해야 하는 것은 수치와 감각이고, 방법은 전부 바꾼다.

### 2.1 광원 → URP 2D Light2D

| 원본 광원 | Unity | 보존 수치 |
|---|---|---|
| 앰비언트 `ambient:298` | **Global Light 2D** (타일·캐릭터 소팅 레이어별 세기 분리) | 어둠 바닥 밝기 |
| 손전등 본체 + 원뿔 `halfAngle 28°`, `flashRange 468px` | **Spot Light 2D** inner/outer angle, falloff, radius 9.36셀 | 원뿔각·사거리 |
| 랜턴 6개, 플레어, 조명탄 | **Point Light 2D** + 깜빡임은 intensity 애니 커브 | 반경·수명 |
| 보스 글로우, 폭발 플래시 | Point Light 2D 짧은 수명 프리팹 (풀링) | |
| 시점 크루 100% / 나머지 85% | Light2D intensity | |

- 원본의 광원 예산(6+8)은 WebGL 드로우콜 절약용이었다. Light2D는 카메라 컬링이 기본이라 **예산 코드를 옮기지 않는다.**
- 광원 색이 겹칠 때 "RGB 가산, A는 최대"(`EXT_blend_minmax`)로 만든 느낌은 Light2D **Blend Style**(Additive 기본, 필요시 Multiply 스타일 1개 추가)로 맞춘다.
- 노멀맵: 원본은 64×64 fBm 절차 노멀을 전 화면에 깔았다. Unity에서는 타일 아틀라스에 **Secondary Texture `_NormalMap`**을 붙인다. 50px 타일 536장의 노멀은 높이 추정 도구(Laigter, SpriteIlluminator 등)로 일괄 생성하고 `import-tiles` 스크립트가 함께 임포트한다. 캐릭터·몬스터는 1차에서 노멀 없이 시작한다.

### 2.2 벽 그림자·림 → Shadow Caster 2D + 셰이더

원본 `LIT.beginWalls`는 벽 전체를 오프스크린에 그린 뒤 광원 중심으로 확대·축소해 차분을 냈다. 이는 "타일 수와 무관한 비용"을 얻기 위한 Canvas2D 우회였다.

- **Shadow Caster 2D**를 벽 Tilemap의 Composite Collider 2D 경로에서 청크 단위(16×16셀)로 생성한다. 벽이 부서지면 그 청크만 재생성한다. Light2D가 광원마다 그림자를 자동으로 드리운다.
- 원본의 부드럽고 번지는 그림자 인상은 Unity 6 URP 2D 라이트의 **소프트 섀도** 옵션과 Shadow Intensity로 맞춘다. 원본 값 `wShadow 1.20`은 강도의 출발점일 뿐이며 화면 비교로 재튠한다.
- 벽 광원쪽 림(`wRim 0.36`)과 반대쪽 음영(`wShade 1.20`)은 그림자 시스템이 아니라 **타일 노멀맵 + Sprite-Lit 셰이더**가 자연히 만든다. 별도 코드 없음.
- 스프라이트 림라이트(`spRim 0.31, 4.2px`)는 Shader Graph 스프라이트 셰이더에 **가장 가까운 광원 방향 기준 림** 항을 넣는다(2D 라이트 텍스처 샘플로 근사). 전용 합성 패스는 두지 않는다.

### 2.3 시야(LOS) → 가시 폴리곤 + 누적 RenderTexture

원본은 매 프레임 CPU에서 360 레이를 쏘고 셀마다 RGBA 바이트를 채워 텍스처로 올렸다. 규칙(19타일, 벽은 보이되 차단, 기억 11타일, 잔존 30%)은 보존하고 방법을 바꾼다.

1. **레이캐스트는 Sim에 남긴다**(코옵·AI가 `seenAt`을 쓰므로). 결과를 셀 바이트 배열이 아니라 **가시 다각형 정점 배열**로도 낸다.
2. Presentation은 그 다각형을 **팬 메시**로 만들어 `Visible` RT(카메라 해상도 ÷2)에 렌더한다. 크루·플레어·보스 시야원은 같은 RT에 추가 메시로 그린다.
3. `Explored` RT는 월드 크기(80×72셀 × 8px)로 하나 두고, 매 프레임 `max(Explored, Visible)`로 누적한다. 원본의 거리 감쇠(`fade = 1 - d/11`, 바닥 30%)는 이 RT를 샘플할 때 셰이더에서 계산한다.
4. **어둠 풀스크린 패스 1개**(Full Screen Pass Renderer Feature, Shader Graph Fullscreen 타깃)가 `Visible`·`Explored`·2D 라이트 텍스처를 합쳐 최종 어둠을 낸다. 시야 전환 보간(rise 0.16s / fall 0.38s)은 `Visible` RT를 이전 프레임과 지수 혼합하는 것으로 처리한다.

이 구조에서 원본 LX의 lightmap·contrast·zone·core 4패스는 **어둠 패스 1개 + Volume**으로 접힌다.

### 2.4 LX 합성 → Volume 포스트프로세싱

| LX 레이어 | Volume 오버라이드 | 비고 |
|---|---|---|
| 0 lightmap (어둠색 → 광원색) | 어둠 패스(§2.3) | 어둠색은 지층별 프리셋 |
| 1 contrast (어둠 압축, 빛 채도·대비) | **Color Adjustments** (Contrast, Saturation) + **Tonemapping ACES** | |
| 2 zone (구역 앰비언스 틴트) | 지층별 **Volume 프로파일** + Color Filter, 보스 소환 시 Volume weight 트윈 | 지층 진입 = 프로파일 교체 |
| 3 core (핫코어 smoothstep) | **Bloom** threshold/scatter | 광원 중심 과노출 |
| Bayer 4×4 디더 | **Film Grain** (약하게) 또는 어둠 패스에서 블루노이즈 | 원본의 거친 질감 보존 |
| 피격 붉은 틴트 `G.tint` | Vignette color + intensity 트윈 | `FEEL.drawScreen` 대체 |
| `LIGHTMAP_SCALE 0.65` 부드러움 | 어둠 RT를 ½ 해상도 + 바이리니어 | |

`LX_DEFAULT`의 확정값은 Volume 프로파일의 출발점으로 1회 옮기고, 이후 튜닝은 인스펙터에서 한다. F10 패널과 `tc_lx_v791c` 저장은 폐기한다.

### 2.5 타일 → Tilemap

- **Tilemap 3장**: 바닥 / 벽 / 암반(core) 윗면. 암반 측면(H=0.26셀)과 아래 그림자는 벽 Tilemap 아래 소팅의 오버레이 타일로 넣는다. 원본 3패스 수작업은 소팅 레이어로 대체.
- 타일은 **커스텀 `TileBase`** 하나가 `(type, band, surface, damage)` → 아틀라스 스프라이트를 고른다. `tile_manifest.json`의 `atlasX/Y`를 그대로 쓴다. 손상 단계가 바뀌면 `Tilemap.RefreshTile(cell)`.
- 타격 진동·밀림(`vib/nudge` 5.6px, 0.19s)은 `Tilemap.SetTransformMatrix`로 셀 오프셋. 렌더 전용 상태를 Sim에서 분리한다.
- 밴드 시임·천장 요철·출구 마커는 별도 오버레이 Tilemap 또는 스프라이트.
- 5,760셀은 Tilemap 청크 렌더로 충분하다. 성능 문제가 나올 때만 청크 메시로 간다(상위 계획 §3.2 그대로).

### 2.6 스프라이트 셰이더 (Shader Graph, Sprite Lit 타깃)

하나의 캐릭터·몬스터 셰이더에 프로퍼티로 넣는다. 원본은 이 효과들을 드로우 함수 안에서 색을 덧칠하며 만들었다.

| 원본 | 프로퍼티 |
|---|---|
| `e.hurt` 히트 플래시 0.18~0.25s | `_FlashAmount` (흰색 lerp) |
| `frozenT` / `slowT` | `_Tint` 색·강도 |
| 보스 `bossFade` 사망 | `_Dissolve` (노이즈 임계, 가장자리 발광) |
| 보스 장갑 셀 강조, 붉은 보스 벽 | 벽 타일용 `_Emissive` 색 |
| 다운 상태 회색 | `_Desaturate` |
| `LIT.spr` 림 | `_RimStrength/_RimWidth` |

MaterialPropertyBlock으로 인스턴스별 값을 넣어 배칭을 유지한다.

---

## 3. 시각 — VFX · 연출(주스)

원본 `J` 객체는 파티클 12종 배열·화면흔들림·히트스톱·데미지 텍스트를 한 객체에 담고, 시뮬레이션 dt까지 좌우했다. Unity에서는 **Sim 이벤트(`TileBroken`, `EnemyHurt`, `PlayerHurt`, `BossPhase`…)를 Presentation이 구독**해 아래 도구로 표현한다. 연출이 Sim에 영향을 주는 경로는 히트스톱 하나만 남기고, 그것도 `Time.timeScale`로 처리한다.

### 3.1 파티클

| `J` 배열 | 용도 | Unity |
|---|---|---|
| `ch` chunks | 블록 파괴 파편 | **Shuriken** 프리셋 + 타일 종류별 색 그라디언트, 서브이미터로 먼지 |
| `rg` ring | 충격 링 | 링 스프라이트 + 스케일/알파 커브(파티클 1개 또는 DOTween) |
| `bd` burst | 방사 점 | Shuriken Burst |
| `sm` smoke | 연기 | Shuriken, 텍스처 시트 애니 |
| `sp` spikes | 피격 스파이크 | Shuriken, Stretched Billboard |
| `sq/sr` square/star | 잭팟·보급 반짝 | Shuriken |
| `fl` flash | 화면 플래시 | Point Light 2D 펄스 + Vignette 짧은 트윈 |
| `af` after (잔상) | 대시 잔상 | 스프라이트 풀 7개, 알파 감쇠 |
| `dm` damage text | 데미지 숫자 | **TMP 월드 스페이스** 풀 + DOTween 상승·페이드 |
| `dust/mote/dfall/bat` | 환경 먼지·광입자·낙진·박쥐 | 카메라 자식 **VFX Graph** (수천 개, GPU) |
| `ceilingArea` 천장 붕괴 `BD_MAX 360` | 보스 돌진 낙석 | VFX Graph 또는 Shuriken 1발 360개 |
| `spawnRubble` RUB_MAX 300 | 굴러다니는 잔해 | Rigidbody2D 소량(≤40) + 초과분은 Shuriken 충돌 모듈 |
| `gore/fdec/trail` 데칼 | 혈흔·바닥 얼룩·자취 | **바닥 RT 페인팅**(월드 크기 RT에 스탬프 1회). 수명·개수 제한 코드 삭제 |

원본 `PERF_BRK_BURST`(180ms 창에서 4개 초과 시 소리·파티클 절감)는 Canvas2D 성능 우회였다. Unity에서는 파티클 시스템 **Max Particles**와 SFX 폴리포니 제한(§8)이 같은 일을 한다. 규칙을 옮기지 않는다.

### 3.2 히트스톱 · 킥 · 스쿼시 (FEEL)

- **히트스톱**: `Time.timeScale = 0.055`를 실시간 N ms 동안(unscaled 코루틴). Sim의 `FixedUpdate`는 timeScale을 따르므로 원본과 같은 "시간 5.5%" 효과가 나오고, 60Hz 틱 양자화와 무관하게 12ms 보스 히트스톱도 살아난다(상위 계획 §7 위험 해소). UI·오디오 페이드는 unscaled.
- **카메라 킥·흔들림**: **Cinemachine Impulse Source**. 원본 `J.kick(strength, dirX, dirY)`는 방향 있는 임펄스 1발이다. 보스 돌진 `dashImpactShake 6`, 피격 3단계는 Impulse 프로파일 3종.
- **스쿼시·스트레치·리코일**: DOTween `DOPunchScale` / `DOPunchPosition`. 대시 `sx .86→1.16 / sy 1.12→.94` 곡선은 AnimationCurve 에셋으로.
- 이 셋을 **자체 제작 `Feedback` 컴포넌트** 하나로 묶는다 *(2026-09-06 결정: Feel 미구매, 직접 구현)*. 구조는 `FeedbackProfile` ScriptableObject(히트스톱 ms · 임펄스 세기/방향/프로파일 · 펀치 스케일 커브 · 플래시 색/시간 · SFX id · 파티클 프리팹 · 지연)의 배열이고, 이벤트 구독자가 `Feedback.Play(profile, pos, dir)`을 부른다. 원본 `FEEL/J` 조합 60여 곳은 프로파일 에셋 20~30개로 정리한다. 부족하면 그때 Feel을 사서 프로파일을 MMF Player로 옮긴다.
- `prefers-reduced-motion`은 설정 토글 "모션 감소"로 유지하되 DOTween/Impulse 강도 배율 1개로 구현.

### 3.3 시네마틱 → Timeline

보스 등장(4.65s)·사망(3.5s)은 DOM 오버레이와 수동 `tick`, `CREW.phase` 정지로 만들었다. Unity에서는 **Timeline 에셋 2개**로 만든다.

- 트랙: Cinemachine(플레이어 vcam → 보스 vcam 블렌드 1.05s, 줌 1.15배), Activation(레터박스 UI), Animation(이름 플레이트), Audio(럼블·포효), **Signal**(포효 프레임 15 → 카메라 킥·링·버스트·SFX).
- 월드 정지는 `Sim.Paused` 플래그. Timeline은 unscaled로 진행.
- 사망 연출의 "보스를 월드에 남겨 조명·안개를 받게 한다"는 판단은 보존한다. `_Dissolve` 셰이더 프로퍼티를 Timeline 애니 트랙이 구동.
- 원본에서 5개 블록이 `infSpawnBoss`를 중첩 래핑해 만든 순서(연출·BGM·HUD 숨김)는 **`BossSpawned` 이벤트 구독자 3개**로 평면화한다.

### 3.4 화면 전환

종이 시트 와이프(260/40/340ms)는 풀스크린 셰이더 1개(진행도·방향 프로퍼티) + 짧은 Timeline 또는 DOTween. 씬 전환(Menu ↔ Run)은 Addressables 로드와 같은 커버 구간에 넣어 **로딩 화면**이 와이프 안에 숨는다.

### 3.5 텔레그래프 · 투사체 비주얼

- 보스 예고탄 착탄 원(`telegraphRadius`, 색, 알파): 방사 채움 Shader Graph 쿼드. 예고 진행도는 `_Fill`.
- 돌진 캡슐: 9-slice 캡슐 스프라이트 + 화살표, 스케일은 lead 시간.
- 투사체 11종: 프리팹 11개, 트레일은 **TrailRenderer**, 레이저는 **LineRenderer** + UV 스크롤 셰이더, 글로우는 Bloom이 대신한다(`glowBlur` 프로퍼티 폐기).
- `TUNNEL_PROJECTILE_FX.stepProjectile`의 비주얼 계산은 전부 파티클·트레일로 흡수.

---

## 4. 애니메이션

원본은 프레임 인덱스를 수식으로 계산하고(`floor(animT*10)%6`), 방향별 피벗 표를 런타임에 들고 다니며, 좌측 5방향만 그리고 3방향은 X 반전했다. 시트 자산은 그대로 쓰되 런타임 규칙은 전부 임포트 시점으로 옮긴다.

- **임포트 스크립트가 클립을 생성**한다: 역할 4 × 방향 5 × 액션(idle/walk/drill/fall). 피벗은 `report.json`의 `pivot_in_cell`을 스프라이트 메타에 굽는다. 런타임 피벗 표 삭제.
- **Animator** 하나에 **2D Freeform Directional 블렌드 트리**(파라미터 `DirX, DirY`)로 8방향을 잇는다. E/NE/SE는 클립을 미러 플래그로 생성하거나 `flipX`. `minerDir8` 함수 삭제.
- 속도 파라미터로 idle↔walk, 트리거로 drill/fall/dash. 재장전·스킬은 1차는 전신 클립(상체 레이어 없음).
- 몬스터 16프레임 스트립: 클립 4종(walk 0~5, idle 6~7, blink 8~11, sprint 12~17), 깜빡임은 `blinkCd` 대신 Animator 랜덤 트리거.
- 보스 드래곤 4애니(10fps): Animator, `fireBreath` 클립 15프레임에 **Animation Event → 포효 SFX·카메라 킥**. 원본 `animT>=1.4` 체크 삭제.
- 장기 확장: 새 보스·NPC를 프레임 시트 대신 **2D Animation 패키지(스프라이트 리깅)**로 만들면 프레임 162장 같은 자산 폭발을 피할 수 있다. 기존 드래곤은 프레임 그대로.
- 픽셀 정합: 원본은 CSS 픽셀에 DPR≤2로 그렸다. Unity는 PPU=50(1셀=1유닛), 정수 줌 강제 없음(원본도 동적 줌이라 정합 요구가 없었다).

---

## 5. 카메라 → Cinemachine 3

원본 카메라 4단계는 Cinemachine 컴포넌트에 거의 1:1로 대응한다. 직접 짜지 않는다.

| 원본 (7364~7402) | Cinemachine |
|---|---|
| 데드존 `teWorld(20)=111px` | **Position Composer** Dead Zone 2.22셀, Damping = `followSpeed .07` 환산 |
| 룩어헤드 조준 방향 139px | Look-ahead Time/Smoothing, 또는 조준점 타깃 오프셋 |
| 세로 42% 앵커 | Screen Position Y 오프셋 |
| 동적 줌 (AI 크루가 80% 안에) | **Target Group**(플레이어 + AI 크루) + Group Framing, Ortho Size 범위 = `baseZ~zIn` |
| 클램프 `tcClampCamera` padX 611 | **Confiner 2D** (맵 경계 폴리곤, 여유 포함) |
| 화면 흔들림 | **Impulse Listener** |
| 보스 시네마틱 카메라 | 두 번째 vcam, 우선순위 전환 + 블렌드 |
| 퀵크래프트 줌 잠금 `TC_CRAFT.zoomLock` | 우선순위 vcam 하나 더 (Ortho 고정). 렌더 루프가 크래프트 상태를 읽던 결합 제거 |
| 고정 챔버 `G.fixed` | 폐기 (mine 모드 전용) |

---

## 6. 물리 · 충돌 — 상위 계획 D5 재검토

D5는 "Physics2D 미사용, 원본 원-AABB 유지"였다. 원본 `collide()`의 3회 반복·4px 서브스텝·35% 보간 탈출은 **Canvas 게임에 물리 엔진이 없어서** 만든 것이다. 이 문서의 원칙에 따라 다음으로 바꾼다.

**권장: Physics2D를 충돌·쿼리·분리에 쓰고, 이동 적분은 스크립트가 한다(Kinematic).**

| 항목 | 방식 |
|---|---|
| 벽 | Tilemap Collider 2D + **Composite Collider 2D**(청크별 Tilemap으로 재생성 비용 분산) |
| 플레이어·AI·적 | `Rigidbody2D`(Kinematic, `useFullKinematicContacts`) + CircleCollider2D. 이동은 `Rigidbody2D.Cast` → `MovePosition`. 대시·보스 돌진은 **Continuous** 충돌 감지로 터널링 방지 → 4px 서브스텝 삭제 |
| 벽 속 갇힘(벽이 실시간 생성) | `Physics2D.OverlapCircle`로 감지 후 가장 가까운 빈 셀로 이동. 원본 35% 탈출의 **의도**만 보존(계획 D5의 안전장치 항목) |
| 소프트 분리 (`sepRatio .86`) | 콜라이더 반경을 `0.86r`로 두어 겹침 허용, 밀어내기는 `OverlapCircleAll` 기반 스크립트 유지(Box2D 강제 분리는 원본보다 딱딱함) |
| 보스 질량 0 | 보스만 상대를 밀고 자신은 안 밀림 → 보스 콜라이더는 다른 레이어, 밀어내기 스크립트에서 질량 ∞ |
| 넉백 | 속도 필드 + `exp(-7.2·dt)` 감쇠는 스크립트(규칙이라 보존) |
| 전리품 | 가짜 z 포물선은 스크립트, 획득 28px·자석 72px은 **Trigger** 2개 |
| 투사체 | `Physics2D.CircleCast` 매 스텝. 도탄 축 반사는 hit normal로 |
| 적 시야 `sightClear` | `Physics2D.Linecast` 벽 레이어 |
| AI 경로 다익스트라 `pathDigCost` | 그리드 기반 규칙이라 **보존**(콜라이더와 무관) |
| 잔해 | Dynamic Rigidbody2D ≤ 40개 + 수명 |
| 결정론 | Box2D는 같은 빌드·같은 순서면 결정론적. 호스트 권위 모델(D7)에 충분. 락스텝은 현 단계 불필요 |

얻는 것: 터널링·겹침·쿼리 코드 삭제, 새 오브젝트(방벽·포탑·보스 벽)가 콜라이더만 붙이면 충돌에 참여, 디버그 기즈모. 잃는 것: 프레임당 Tilemap Collider 재생성 비용 → 청크 분할과 `RefreshTile` 지연 배치로 관리. **D5는 "Physics2D 하이브리드"로 개정한다.**

---

## 7. UI

셸 UI는 100% DOM/CSS였다. 이것이 프로토타입 속도의 핵심이었지만, 결과물은 CSS 전용 효과에 깊이 의존한다(box-shadow 112 · filter 111 · backdrop-filter 12 · transition 86 · @keyframes 52 · linear/radial-gradient 98 · text-shadow 30 · CSS 변수 131). Unity에서는 **UGUI + TextMeshPro + DOTween**으로 새로 만들고, "같은 인상"을 아래 방식으로 낸다.

| CSS 의존 | UGUI 대체 |
|---|---|
| 패널 `box-shadow`, 유리 `backdrop-filter`, `.tcSurface/.tcGlass` | **9-slice 스프라이트**(패널 3종 × 상태) + 그림자 9-slice 1장. **유리 블러는 폐기** *(2026-09-06 결정)*: 모달·카드 뒤는 전체화면 반투명 딤(검정 α 0.55~0.7) + 패널 자체는 불투명도 높은 9-slice. 블러 RT·Translucent Image 모두 쓰지 않는다 |
| `linear/radial-gradient` | 그라디언트 스프라이트 또는 UI 그라디언트 셰이더(Shader Graph Canvas 타깃) |
| `text-shadow`, 글로우 | TMP 머티리얼 프리셋(Underlay, Outline, Glow) |
| `transition`/`@keyframes`/`animation` | DOTween 시퀀스 프리셋 또는 Animator(UI 전용 클립) |
| CSS 변수 131개 (`--menu-x` 등) | **테마 ScriptableObject**(색·간격·폰트 크기) + TMP Style Sheet |
| `filter: blur/brightness` 호버 | UI 머티리얼 프로퍼티 트윈 |
| SVG 노드맵 | UGUI 노드 프리팹 + `UILineRenderer`(Unity UI Extensions), ScrollRect 팬·줌 |
| 메뉴 마우스 패럴랙스 | 레이어별 RectTransform 오프셋 스크립트 |
| 미니맵 캔버스 | `Texture2D` 80×72(셀=픽셀) → RawImage, 확대는 Point 필터 |
| 핑 마커·말풍선·크래프트 프리뷰 (`#uiLayer`) | **월드 스페이스 Canvas** 또는 스프라이트. 화면밖 화살표는 Screen Space 캔버스 |
| 폰트 Pretendard → 시스템 폴백 | Pretendard **SDF 폰트 에셋**(KS X 1001 한글 2,350 + 영문·기호 정적, 나머지 동적 폴백 폰트 1개) |
| `fitModule --fit` 스케일 | **Canvas Scaler** Scale With Screen Size (1920×1080, match 0.5) + 안전영역 |
| HUD 좌표 `tc.uiLayout.v1` | 1회 임포트해 RectTransform 앵커·오프셋으로 굳힘. UILAB 폐기 |
| 키보드 1/2/3 카드 선택, Tab 홀드 가이드 | Input System UI 액션맵. **게임패드 내비게이션**은 `Navigation` 명시 + 첫 선택 자동 포커스 |

새로 만드는 화면: 일시정지, 로딩(`assets/loading` 4장 랜덤 + 팁), 해상도·전체화면·V-Sync 옵션, 리바인딩.

대안 메모: UI Toolkit(USS)은 CSS→USS 변환이 기계적이고 transition·변수를 지원하지만, Unity 6 런타임에서 월드 스페이스 UI·블러·커스텀 셰이더가 약하다. HUD가 월드와 강하게 묶인 이 게임에서는 **스택 하나(UGUI)**가 낫다. 메뉴만 UI Toolkit으로 나누는 것은 관리 비용이 더 크다. 상위 계획 D3 유지.

---

## 8. 오디오

| 원본 | Unity |
|---|---|
| `AU.vol` 카테고리 게인 × `sfxMix 3.0`, WaveShaper 소프트 리미터 | **Audio Mixer**: Master ▸ Music / SFX ▸ (dig, brk, combat, ui, loot, alert). SFX 그룹에 Compressor + Limiter |
| BGM 라우터 `current/revision`, 보스 페이드 3.0s/2.6s, 앰비언스 2겹 | AudioSource 크로스페이드 컴포넌트 + **Mixer Snapshot**(lobby / tunnel / boss) `TransitionTo(fade)` |
| `MENU_SFX.bank` 25종 변형 1~5개, `jit` 피치 지터 | **`SfxBank` ScriptableObject**: `{id, clips[], volume, pitchJitter, category, polyphony}`. 재생은 풀링된 AudioSource, `pitch = 1 ± jit` |
| 절차 합성 SFX 17종 (buy, cache, dawn, descend, exit, fail, ore, ready, rescue, start, tick, timeout, voice, warn, zzz, 드릴 폴백 2) + 보스 럼블 41/63Hz, 사망 sawtooth, brk 저역 사인 | **베이크**: `tools/unity-export/bake-procedural-sfx.mjs`가 `OfflineAudioContext`로 각 함수를 렌더해 WAV 저장(변형 3~5개씩 시드 다르게). 이후 사운드 디자이너가 교체 가능한 파일이 된다 |
| `DRILL_SMP` start/loop/release | AudioSource 3개, loop는 `PlayScheduled` 이음새 없이, 과열은 `pitch`·LowPass 필터 |
| `thr('dig',70)` 스로틀, PERF_BRK_BURST 소리 생략 | `SfxBank.polyphony` + 최소 재생 간격 |
| 자동재생 정책 `AU.init/resume` 제스처 훅 | 폐기 |
| `<audio>` 폴백, file:// CORS 분기 | 폐기 |
| 보스 포효 = 애니 프레임 15 | Animation Event |

선택지: 적응형 음악(지층·위협도에 따라 레이어가 붙는 BGM)을 나중에 하려면 **FMOD Studio**(인디 무료 티어) 도입이 표준이다. 1차는 Audio Mixer로 충분하고, 위협도 `infThreatValue`에 따라 앰비언스 2겹의 볼륨을 섞는 정도는 Mixer 파라미터 노출로 된다.

---

## 9. 데이터 · 저장 · 툴링 · 네트

- **튠 값**: `DEMO` 190필드, `TE`, `LIT_TUNE`, `LX_DEFAULT`, `BOSS_TUNE_FALLBACK` + `bossLabBakedParams`, `INF_*` 전부 ScriptableObject. Play 모드에서 인스펙터 수정이 곧 Boss Lab·F10이다. 핀·되돌리기는 git.
- **디버그**: 빌드 플래그 뒤 인게임 디버그 메뉴 1개(적 스폰, 층 이동, 특성 강제, 무적, 시드 입력). `?tcTest`, Test Hub, Relic Lab, Projectile Lab, Observer는 이 메뉴의 항목으로 흡수하거나 폐기.
- **자산**: Addressables 그룹(타일 바이옴별, 캐릭터별, 보스, 오디오). 로딩 화면이 그룹 로드 진행도를 표시.
- **저장**: `tc_infinite_meta_v1` JSON 스키마 유지 → `persistentDataPath`. 설정은 별도 파일. Steam 클라우드는 파일 단위라 그대로 된다.
- **로컬라이즈**: 텍스트가 HTML에 한국어 하드코딩. **Localization 패키지** String Table로 빼고 TMP와 연결. 포팅 시점이 가장 싼 때다.
- **코옵(M8)**: Node 릴레이 서버는 "브라우저가 소켓 서버가 못 되어서" 있었다. **Netcode for GameObjects + Unity Transport**, 방 코드는 **Relay + Lobby** 서비스로 대체. 호스트 권위·시드 동기 모델은 보존, `SimCommand` 큐가 NetworkVariable/RPC로.
- **빌드**: Windows 빌드 + `app-icon-dragon.ico`. `build-package.mjs`(node.exe 동봉, START.bat), `build-single-html.mjs`(100MB 인라인)는 전부 폐기.

---

## 10. 폐기 목록 — HTML·브라우저 제약의 산물

Unity 버전에 어떤 형태로도 옮기지 않는다.

- 17MB 단일 HTML 구조: base64 인라인 오디오·이미지, 자산 shim(Image.src/fetch 가로채기), `build-single-html`
- 스크립트 주입 파이프라인(`inject-ping/chat/ai-crew/observer`, `reinject`)과 **런타임 함수 래핑**(`infSpawnBoss`·`AICREW.update`·`paintUI` 중첩) → 이벤트 구독으로
- 전역 `cx` 컨텍스트 스왑, `_BS` 스프라이트 캐시, DPR 처리, `ResizeObserver`/`visualViewport` 리사이즈, CSS 픽셀 좌표계
- Canvas2D 합성 트릭: 방사 확대·축소 그림자, `destination-in` 실루엣 재클리핑, `shadowBlur` 글로우 24곳, `screen` 잔상
- WebGL1 라이트맵(광원 예산, `EXT_blend_minmax`, Bayer 디더 셰이더), `drawDarkness` 폴백, 자동 품질 강등(`OPT.lq`, `FQ.flip`)
- 가변 dt 루프와 dt 상한 0.05, `J.hs`가 dt를 곱하는 히트스톱
- WebAudio 절차 합성(베이크 후), `<audio>` 폴백, 자동재생 제스처 훅, `revision` 카운터
- DOM 오버레이 시네마틱, 클릭 캡처 위임 화면 전환, `CREW.phase`로 월드 정지
- localStorage 12키(스키마만 JSON으로 이관), UILAB `tc.uiLayout.v1`(좌표만 1회 임포트), LX `tc_lx_v791c`, Boss Lab 3키
- `Math.random()` 424곳 → 단일 시드 RNG(Sim) + 연출용 RNG 분리
- 레거시 mine 모드·솔로 미션·`#pTitle/#pUp/#pRep/#pSet/#pHow`(상위 계획 §2.2)
- 코옵 Node 서버·HTML 서빙·포트 탐색 START.bat

---

## 11. 추천 패키지 · 에셋

가격·라이선스는 도입 시점에 확인한다. 1차는 Unity 제공 패키지만으로 시작할 수 있다.

| 패키지 / 에셋 | 용도 | 비용 | 우선 |
|---|---|---|---|
| URP 2D Renderer, Light2D, Shadow Caster 2D, Shader Graph, Full Screen Pass | 조명·어둠·셰이더 (§2) | Unity 내장 | M2 |
| Volume 포스트프로세싱 (URP) | LX 대체 (§2.4) | 내장 | M2 |
| Tilemap + 2D Tilemap Extras | 타일 (§2.5) | 내장 | M1 |
| Particle System, VFX Graph | VFX (§3.1) | 내장 | M3 |
| **Cinemachine 3** | 카메라·임펄스 (§5) | 내장 | M1 |
| Timeline | 시네마틱 (§3.3) | 내장 | M4 |
| Animator, 2D Animation (선택) | 애니 (§4) | 내장 | M1 |
| Input System | 입력·게임패드 | 내장 | M0 |
| TextMeshPro, UGUI | UI (§7) | 내장 | M1 |
| Addressables | 로딩 (§9) | 내장 | M5 |
| Localization | 텍스트 외부화 (§9) | 내장 | M5 |
| Netcode for GameObjects, Unity Transport, Relay/Lobby | 코옵 (§9) | 내장 / 서비스 무료 티어 | M8 |
| **DOTween** (Pro 선택) | 트윈 전반 | 무료 / Pro 유료 | M1 |
| ~~Feel~~ (More Mountains) | 히트스톱·임펄스·플래시 조합 (§3.2) | 유료 $50 | **미도입** — 자체 `Feedback` 컴포넌트로. 부족할 때 재검토 |
| Unity UI Extensions | `UILineRenderer` 노드맵, 라디얼 레이아웃 | 무료(OSS) | M5 |
| ~~Translucent Image~~ | UI 유리 효과 (§7) | 유료 $24.99 | **미도입** — 유리 효과 자체를 폐기, 딤 처리 |
| Laigter / SpriteIlluminator | 타일 노멀맵 생성 (§2.1) | 무료(OSS) / 유료 | M2 |
| FMOD Studio (선택) | 적응형 음악 (§8) | 인디 무료 티어 | 보류 |
| ~~Odin Inspector~~ | SO 대량 편집 편의 | 유료 $55, 연매출 $200K 제한 | **미도입** — 특성·노드·유물 데이터는 CSV/JSON → SO 임포터로 관리(§4 `dump-tuning` 경로). 필요 시 PropertyDrawer 몇 개 직접 작성 |

---

## 12. 상위 계획에 미치는 변경

| 항목 | 변경 |
|---|---|
| §1 D5 물리 | "Physics2D 미사용" → **"Physics2D 하이브리드(충돌·쿼리·분리는 엔진, 적분은 Kinematic 스크립트)"**. 근거 §6 |
| §1 D6 틱 | 고정 60Hz 유지. 히트스톱은 `Time.timeScale`로 틱과 분리(§3.2). §7 "체감 차이 작음" 위험 문장을 이 방식으로 해소 |
| §3.3 조명 표 | §2 구조(Light2D + 어둠 패스 1개 + Volume)로 교체. LX 4레이어 Renderer Feature 1개 → 어둠 패스 1개 + Volume 프로파일 |
| §4 추출 표 | `bake-procedural-sfx.mjs`(절차 합성 17종 + 럼블·사망음 WAV), 타일 노멀맵 생성 단계 추가 |
| §5 M1 | Cinemachine·DOTween·Physics2D 도입. 카메라 수작업 항목 삭제 |
| §5 M2 | Volume 프로파일 지층별 3+1, Shadow Caster 청크 재생성, 타일 노멀맵 |
| §5 M3 | Shuriken 프리셋표, Shader Graph 스프라이트 셰이더 프로퍼티 6종, 자체 `Feedback` 컴포넌트 + `FeedbackProfile` SO |
| §5 M4 | 보스 시네마틱 Timeline 2개, 텔레그래프 셰이더 |
| §5 M5 | 9-slice 세트, 테마 SO, TMP 한글 폰트 에셋, 모달 딤 규격, Localization 도입 |
| §5 M7 | Audio Mixer 스냅샷 3종, SfxBank SO, 베이크 WAV 검수 |
| §2.2 버리는 것 | §10 폐기 목록 참조 추가 |
