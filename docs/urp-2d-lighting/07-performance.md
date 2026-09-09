# 07. 성능 노트

원본 구간 18:41–19:20 (슬라이드 42) · 챕터 *"Notes on performance"*

슬라이드 6개 항목 전문(번역 + 해설):

## 1. Fill rate를 최대한 낮게 유지한다
> *"Keep fill rate as low as possible. **One large light can have worse performance than several small lights.**"*

- 큰 라이트 1개 > 작은 라이트 여러 개 보다 **더 느릴 수 있다.** 픽셀을 얼마나 칠하느냐가 비용이다.
- ⚠️ 단, [04-lights.md](04-lights.md) §2의 "카메라에 붙인 거대 태양광"은 예외적으로 채택한 트릭이다. 화면 크기만큼만 칠하므로 감당 가능하다. **씬에 흩뿌린 거대 라이트**가 문제다.

## 2. 배치(batch)되게 만든다
> *"Lights perform best when batchable. **Lights with the same lighting setup across contiguous layers can all be drawn together.**"*

- **동일한 라이팅 셋업 + 연속된 Sorting Layer** 는 한 번에 그려진다.
- 실무 규칙: Target Sorting Layers / Blend Style / Light Order 조합의 **종류를 줄이고**, 레이어 순서를 라이팅 그룹 단위로 **연속되게 배치**한다.
- 레이어를 라이팅 무관하게 뒤섞으면 배치가 깨진다.

## 3. Render Scale을 낮게
> *"Keep your render scale as low as possible. Render scale adjusts the texture size used when rendering lighting, and a lower texture size means fewer pixels to be rendered."*

- Renderer 2D Data → **Render Scale**. 샘플 값 **0.5**.
- 라이팅만 절반 해상도로 그린다. 스프라이트 아트 자체 해상도는 그대로다 → 화질 손실이 거의 눈에 안 띈다. **가장 가성비 높은 손잡이.**

## 4. 그림자 캐스팅 라이트 수를 줄인다
> *"Minimize the number of shadow casting lights on screen. There is a performance cost when switching to draw shadows that is non-trivial."*

- 섀도우를 그리려면 별도 패스로 전환해야 하고 그 전환 비용이 크다.
- → 그래서 샘플이 진짜 섀도우 캐스터를 최소화하고 **네거티브 라이팅/블롭**으로 대체하는 것이다 ([05-shadows.md](05-shadows.md)).

## 5. 화면에 동시에 보이는 Blend Style 종류를 줄인다
> *"Minimize the number of different blend styles onscreen. There is a cost when switching to draw the blend styles that is non-trivial."*

- Blend Style 슬롯이 4개인데 **4개를 다 한 화면에서 쓰면** 전환 비용이 4번 난다.
- → [01-project-setup.md](01-project-setup.md) 에서 "슬롯 설계는 아키텍처"라고 한 이유.

## 6. Max Light / Shadow Render Textures 튜닝
> *"Adjust the number of Max Light Render Textures and Max Shadow Render to fit your project's needs. Higher numbers will increase performance (up to a limit), but they will also increase the memory needed. You will need to find the right number to fit your memory and rendering needs."*

Renderer 2D Data 슬라이드 실측:

| 필드 | 값 |
| --- | --- |
| Render Scale | **0.5** |
| Max Light Render Textures | **16** |
| Max Shadow Render Textures | **1** |

- 늘리면 **속도는 오르지만(한계까지) 메모리를 먹는다.** 플랫폼별로 찾아야 하는 값.

---

## 체크리스트 (환경 세팅 시 그대로 훑기)

- [ ] Render Scale을 0.5부터 시작해서 화질이 무너지는 지점까지만 올린다
- [ ] 한 화면에 쓰는 blend style을 2~3종으로 제한한다
- [ ] Sorting Layer 순서를 라이팅 그룹 단위로 연속 배치했는지 확인
- [ ] `ShadowCaster2D` 는 좁은 광원 근처에만, 화면당 개수를 센다
- [ ] 씬에 흩뿌린 거대 반경 라이트가 없는지 확인 (카메라 부착 키 라이트는 예외)
- [ ] Max Light Render Textures / Max Shadow Render Textures 를 타깃 플랫폼 메모리에 맞춰 확정
