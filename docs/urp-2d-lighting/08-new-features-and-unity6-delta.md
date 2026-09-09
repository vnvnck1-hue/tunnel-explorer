# 08. 발표 당시 신기능 + Unity 6 / URP 17.3 기준 차이

## A. 발표에서 소개한 신기능 (슬라이드 43, 챕터 19:20)

슬라이드 제목: **"New 2D features in Unity (2022 LTS and 2023.1 Tech Stream)"** — 4가지

| 기능 | 내용 |
| --- | --- |
| **Soft infinite shadows** | ShadowCaster2D 투영 그림자의 경계를 부드럽게. 슬라이드 이미지: 광원에서 뻗은 그림자 줄기가 흐릿하게 퍼진다 |
| **2D Light texture Shader Graph node** | Shader Graph에 `2D Light Texture` 노드. `LightTex0` ~ `LightTex3` 중 선택 → **커스텀 셰이더가 2D 라이팅 결과 텍스처를 직접 샘플링**할 수 있다 (blend style 슬롯 4개에 1:1 대응) |
| **2D Lights SRP batching** | URP 에셋의 `SRP Batcher` 체크박스가 2D 라이트에도 적용. [07-performance.md](07-performance.md) §2의 배치 이야기와 직결 |
| **Light Batching Debugger** | 라이트가 왜 배치되지 않는지 보여주는 디버거 창. 발표자: *"…and then like a debugger"* |

> `2D Light Texture` 노드는 우리 프로젝트에서 특히 쓸 만하다. 라이팅 결과를 셰이더에서 받아 후처리(예: 어둠 속 실루엣 강조, 광원 색 기반 틴트)를 직접 만들 수 있다.

## B. Unity 6 / URP 17.3 기준으로 바뀐 것 (우리 프로젝트 기준 검증)

우리 포팅 프로젝트는 **Unity 6000.3.15f1 + `com.unity.render-pipelines.universal` 17.3.0** 이다.
패키지 소스(`Library/PackageCache/com.unity.render-pipelines.universal@…`)를 직접 확인한 결과:

### Light2D.LightType
```
Parametric = 0  (deprecated)
Freeform   = 1
Sprite     = 2
Point      = 3     ← 인스펙터 표기는 "Spot"
Global     = 4
```
발표의 "Spot light"는 이 `Point` 타입이다. 별도의 spot 타입은 없다.

### Light2DBlendStyle
```
TextureChannel : None=0, R=1, G=2, B=3, A=4, OneMinusR=5, OneMinusG=6, OneMinusB=7, OneMinusA=8
BlendMode      : Additive=0, Multiply=1, Subtractive=2
```
- **`Subtractive` 블렌드 모드가 있다.** 발표는 Multiply만 다뤘지만, 그림자용으로 Subtractive도 후보다. (Multiply = 색을 곱해 어둡게 / Subtractive = 값을 빼서 어둡게 — 채도 유지 성향이 다르다)
- `OneMinus*` 반전 채널이 있어 "마스크가 검은 곳에만 영향" 같은 반전 마스킹도 가능하다.

### ShadowCaster2D — 발표 슬라이드와 인스펙터가 다르다
발표(2023) 슬라이드는 `Use Renderer Silhouette` / `Casts Shadows` / `Self Shadows` **체크박스 3개**였다.
URP 17.3의 `ShadowCaster2D` 는 이렇게 바뀌었다:

```csharp
public enum ShadowCastingOptions {
    SelfShadow,          // 스프라이트 자체 음영만
    CastShadow,          // 투영 그림자만
    CastAndSelfShadow,   // 둘 다
    NoShadow             // 그림자 없이, 다른 캐스터 위에 올바르게 그려짐
}
```
추가 프로퍼티: `castingOption`, `alphaCutoff`, `trimEdge`, `useRendererSilhouette`, `selfShadows`, `castsShadows`
- **`alphaCutoff`** — 스프라이트 알파 임계값. 반투명 픽셀을 실루엣에 포함할지 결정.
- **`trimEdge`** — 실루엣 외곽을 안쪽으로 깎는다. 그림자가 스프라이트 밖으로 새는 걸 막는 데 유용.
- Shape source: `ShapeEditor` / `ShapeProvider` (콜라이더 등 외부 제공자에서 형태를 받을 수 있음)

### Light 2D의 그림자 관련 프로퍼티 (발표 이후 확장)
```
shadowIntensity                 // Shadows → Strength (0~1)
shadowSoftness                  // 부드러운 그림자  ← 슬라이드 43 "Soft infinite shadows"의 결과물
shadowSoftnessFalloffIntensity  // 부드러움 감쇠 (0~1)
volumetricEnabled               // 라이트 볼륨
volumetricShadowsEnabled / shadowVolumeIntensity  // 볼륨 그림자
```

### 그 외
- `CompositeShadowCaster2D` 존재 (여러 ShadowCaster2D를 하나의 실루엣으로 합침 — 건물처럼 여러 스프라이트로 구성된 구조물에 필수)
- Renderer 2D Data에 `Use Camera Sorting Layers Texture`, `Camera Sorting Layer Downsampling Method` 등 카메라 소팅 레이어 텍스처 옵션이 있다 (발표에서 미언급)

## C. 발표 내용 중 "그대로 유효한 것" / "다시 확인할 것"

| 항목 | 상태 |
| --- | --- |
| 2D (URP) 템플릿으로 시작 | 그대로 유효 |
| Secondary Textures로 노멀맵/마스크맵 연결 | 그대로 유효 |
| 노멀맵 = Normal map / 마스크맵 = Default 임포트 | 그대로 유효 |
| Blend Style 4슬롯 + 채널 분리 | 그대로 유효 |
| 네거티브 라이팅 / 블롭 / 프레임 보간 | 전부 순수 기법 — 버전 무관 |
| 성능 6대 규칙 | 그대로 유효 (수치는 재측정 필요) |
| ShadowCaster2D 인스펙터 필드명 | **변경됨** — 위 B 참조 |
| Soft shadow | **정식 프로퍼티로 들어왔음** (`shadowSoftness`) — 발표 때는 "신기능 예고" |
