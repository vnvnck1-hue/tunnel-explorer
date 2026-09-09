# 05. 그림자 — 4가지 해법

원본 구간 13:02–16:45 (슬라이드 29~36)

세션의 그림자 철학: **URP 2D의 정식 섀도우 캐스터만으로는 톱다운 게임이 안 된다.**
그래서 샘플 게임은 상황별로 서로 다른 4개 기법을 섞어 쓴다.

| 상황 | 기법 | 슬라이드 |
| --- | --- | --- |
| 임의 형태의 어두운 영역 (건물 안쪽, 처마 밑) | **네거티브 라이팅** (Multiply freeform 라이트) | 29 |
| 오브젝트를 바닥에 "붙이기" (AO) | **블롭 섀도우** (Sprite 라이트) | 30 |
| 국지 광원(가로등) 주변에서 진짜 그림자 | **무한 투영** (`ShadowCaster2D`) | 31 |
| 태양광 아래 나무·수풀 | **유한 투영** (스크립트로 늘리는 블롭) | 32–33 |
| 형태가 뚜렷한 건물 | **freeform 라이트 프레임 보간** | 34–36 |

---

## 1. 네거티브 라이팅 (슬라이드 29, 챕터 13:02)

슬라이드 제목: **"Shadows = negative lighting"**

> *"In the demo, we use 2D lights to produce shadows. Using the multiply blend style darkens areas affected by the light to produce simulated shadows with the creative control of 2D lights."*

**아이디어: 그림자를 "빛이 없는 곳"으로 만들지 않고, "어둡게 만드는 빛"을 직접 배치한다.**

- `Freeform` 라이트로 그림자 모양을 **손으로 그린다** (Edit Shape).
- Blend Style을 **`Multiply`** 로 → 그 영역이 곱해져서 어두워진다.
- Overlap Operation을 **`Alpha Blend`** 로 → 그림자끼리 겹칠 때 더 검어지지 않고 자연스럽게 합쳐진다.

슬라이드에서 헛간(Barn) 예제로 Additive → Multiply 전환을 비교한다.

| 필드 | 값 |
| --- | --- |
| Light Type | **Freeform** |
| Intensity | 3.92 |
| Falloff | 0.26 |
| Falloff Strength | 0.494 |
| Target Sorting Layers | Default |
| **Blend Style** | Additive → **Multiply** |
| **Light Order** | **2** |
| **Overlap Operation** | **Alpha Blend** |

장점(슬라이드 원문의 표현): *"with the creative control of 2D lights"* — 아티스트가 그림자 모양·부드러움·색을 완전히 통제한다. 물리 정확도를 포기하고 연출을 얻는 교환.

> Light Order `2` 로 밀어두는 게 관례다. 일반 광원(0) 위에 그림자 레이어가 올라간다.

---

## 2. 블롭 섀도우 (슬라이드 30, 챕터 13:38)

슬라이드 제목: **"Keep objects on the ground: Blob shadow"**

> *"A quick and easy way to fake shadows is with a blob shadow, a blurred sprite that can be stretched to represent the ambient occlusion that an object produces on the ground."*

가장 싸고 가장 많이 쓰는 기법. **오브젝트가 공중에 떠 보이는 문제를 없앤다.**

| 필드 | 값 |
| --- | --- |
| Light Type | **Sprite** |
| Sprite | `shadow_circle` (가운데 흰색, 바깥으로 흐려지는 방사형 텍스처) |
| Intensity | 0 (슬라이드 표기) |
| Target Sorting Layers | Default |
| **Blend Style** | **Multiply** |
| **Light Order** | 2 |
| **Overlap Operation** | **Alpha Blend** |

- `Sprite` 타입 Light 2D + 흐린 원형 스프라이트 + Multiply = 바닥에 눌린 AO.
- 오브젝트마다 하나씩 자식으로 달고 스케일만 맞춘다.
- 발표자: 돼지 캐릭터 예제로 *"Shadow it's a blob and again this is great…"*
- 이게 다음 §4(유한 투영)의 재료가 된다.

---

## 3. 무한 투영 — ShadowCaster2D (슬라이드 31, 챕터 14:13)

슬라이드 제목: **"Infinite projection shadows: 2D shadow caster"**

> *"2D objects can project infinite shadows easily with the shadow caster component. They can achieve a nice effect when the area of light is limited, for example, the street lamps in the demo."*

URP의 **정식** 2D 섀도우 기능.

`Shadow Caster 2D` 컴포넌트 (슬라이드의 `barrel` 오브젝트):

| 필드 | 값 |
| --- | --- |
| Use Renderer Silhouette | ✔ |
| Casts Shadows | ✔ |
| Self Shadows | ✔ |
| Target Sorting Layers | Default |
| Edit Shape | (수동 폴리곤 편집 가능) |

- 그림자가 **라이트에서 멀어질수록 무한히 뻗는다.** 발표자: *"I shined the light at you, the shadow would go infinitely."*
- 그래서 **광원의 범위가 좁을 때만** 자연스럽다 → 가로등, 랜턴, 모닥불.
- 반대로 태양광처럼 화면 전체를 덮는 라이트에 붙이면 그림자가 화면 밖까지 뻗어 이상해진다 → §4의 문제 정의.
- `Use Renderer Silhouette` 를 켜면 스프라이트 알파를 그대로 실루엣으로 쓴다(폴리곤을 직접 안 그려도 됨).

---

## 4. 유한 투영 — 스크립트로 늘리는 블롭 (슬라이드 32~33, 챕터 14:48)

슬라이드 32 제목: **"Finite projection shadows: Controlled blob shadow"**

> *"In a top-down game, an endless shadow projection coming from 'sunlight' could look strange. On the left, we use the infinite shadow projection (2D shadow caster) and on the right our solution."*

문제: 나무 숲에 ShadowCaster2D를 쓰면 그림자가 끝없이 뻗어 화면이 검게 덮인다(슬라이드 좌측).
해법: **블롭 섀도우를 스프라이트로 두고, 스크립트로 각도와 길이를 태양 시간에 맞춰 늘린다**(슬라이드 우측).

슬라이드 33 제목: **"Finite projection shadows: Blob shadows transformed by script"**

> *"We use a stretched blob shadow for the trees and bushes that rotates based on the time of the day. The function UpdateShadow in the script rotates this shadow."*

슬라이드에 노출된 `UpdateShadow` 구현 (슬라이드 캡처를 옮긴 것 — 변수명/수치는 근사):

```csharp
void UpdateShadow(float ratio)
{
    var currentShadowAngle  = ShadowAngle.Evaluate(ratio);   // AnimationCurve
    var currentShadowLength = ShadowLength.Evaluate(ratio);  // AnimationCurve

    var opposedAngle = currentShadowAngle + 0.5f;            // 태양 반대편
    while (currentShadowAngle > 1.0f)
        currentShadowAngle -= 1.0f;

    // 태양 방향 벡터를 만들어 셰이더 전역으로 넘긴다
    Vector3 lightDirection = Quaternion.Euler(0, SunHeight * 90.0f, 0) * Vector3.up;
    lightDirection = Quaternion.Euler(0, 0, opposedAngle * 360.0f) * lightDirection;
    Shader.SetGlobalVector("_LightDirection", lightDirection.normalized);

    foreach (var shadow in m_Shadows)
    {
        var t = shadow.transform;
        t.eulerAngles = new Vector3(0, 0, currentShadowAngle * 360.0f);
        t.localScale  = new Vector3(1, 0.5f * shadow.BaseLength * currentShadowLength, 1);
    }
}
```

읽어낼 설계 포인트:

1. **입력은 `ratio` 하나** — 하루 진행도 0~1. 모든 그림자가 같은 시간축을 공유한다.
2. **각도/길이를 `AnimationCurve` 로 authoring** — 물리 계산이 아니라 아티스트가 그린 커브. 새벽엔 길고 정오엔 짧게, 마음대로 튜닝 가능.
3. **회전은 Z축, 길이는 localScale.y** — 블롭 스프라이트를 늘리는 것뿐이므로 매우 싸다.
4. **`Shader.SetGlobalVector("_LightDirection", …)`** — 셰이더에도 태양 방향을 전역으로 뿌린다. 커스텀 셰이더/노멀맵 연출이 같은 방향을 공유한다.
5. `shadow.BaseLength` — 오브젝트별 기본 그림자 길이(나무는 길게, 수풀은 짧게).

> 발표자 코멘트: *"for each of the blob shadows and linked it…"* — 씬의 모든 블롭 섀도우를 하나의 핸들러에 등록해서 일괄 갱신한다.

---

## 5. 건물 — freeform 라이트 프레임 보간 (슬라이드 34~36, 챕터 15:42)

슬라이드 34 제목: **"Building projection: Interpolating freeform lights"**

> *"For a building with a very defined shape, we needed a shadow that represents the building's changing silhouette at different times of day and rotates based on the sun's orientation."*

블롭으로는 헛간 같은 **뚜렷한 실루엣**을 표현할 수 없다. 그래서:

1. 태양 위치별로 **freeform 라이트(=네거티브 라이팅 그림자)의 정점 배치를 여러 프레임으로 미리 만든다.** 슬라이드 34에 헛간 그림자 4가지 변형이 나란히 보인다.
2. 그 프레임들 사이를 **보간(tween)** 한다.

슬라이드 36 원문:
> *"We created an interpolator that tweens the position of the vectors of a freeform light. The script uses different freeform lights (when the sun is up, right, down, to the left of the building) to create the smooth interpolation."*

씬 구성 (슬라이드 36의 Hierarchy / Inspector):

```
Light 2D_R                 ← 실제 렌더되는 그림자 라이트
├── Frame1  (Light 2D)     ← 태양이 위
├── Frame2  (Light 2D)     ← 태양이 오른쪽
├── Frame3  (Light 2D)     ← 태양이 아래
└── Frame4  (Light 2D)     ← 태양이 왼쪽

Light Interpolator (Script)
  Element 0 : Reference Light = Frame1,  Normalized Time = 0.10
  Element 1 : Reference Light = Frame1,  Normalized Time = 0.16
  Element 2 : Reference Light = Frame2,  Normalized Time = 0.50
  Element 3 : Reference Light = Frame3,  Normalized Time = 0.70
  Element 4 : Reference Light = Frame4,  Normalized Time = 0.80
  Preview Time : 00:00                   ← 에디터에서 스크럽해서 확인
```

(수치는 슬라이드 캡처에서 읽은 값 — 정확한 값보다 **"참조 라이트 + normalized time" 쌍의 리스트**라는 구조가 요점이다.)

렌더되는 `Light 2D_R` 설정:

| 필드 | 값 |
| --- | --- |
| Light Type | **Freeform** |
| Intensity | 3.92 |
| Falloff | 0.26 |
| Target Sorting Layers | Default |
| Blend Style | **Multiply** |
| Light Order | 2 |
| Overlap Operation | **Alpha Blend** |
| Shadows → Strength | 0.75 |
| Normal Maps → Quality | **Disabled** (그림자 라이트는 노멀맵 불필요) |

핵심 교훈: 발표자의 마무리 코멘트 *"technique that you have enough transitions"* — **프레임을 충분히(최소 4개, 태양의 4방위) 만들어두면 보간만으로 자연스럽다.**

슬라이드 35 = 결과 화면. 헛간 그림자가 태양 방향에 따라 실루엣째로 회전한다.

---

## 요약 결정표

| 오브젝트 성격 | 추천 |
| --- | --- |
| 바닥에 붙은 작은 소품, 캐릭터, 몹 | 블롭 섀도우 (§2) |
| 좁은 광원 근처의 물체 (가로등/랜턴 밑 통, 상자) | ShadowCaster2D (§3) |
| 태양광 아래 다수 자연물 (나무, 수풀) | 스크립트로 늘린 블롭 (§4) |
| 실루엣이 중요한 큰 구조물 (건물, 벽) | freeform 프레임 보간 (§5) |
| 실내/처마/구조적 암부 | 네거티브 라이팅 freeform (§1) |
