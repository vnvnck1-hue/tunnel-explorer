#ifndef TUNNEL_CREW_LIT_2D_PROPS_INCLUDED
#define TUNNEL_CREW_LIT_2D_PROPS_INCLUDED

// 머티리얼 프로퍼티와 디버그 전역값만 담는다. 텍스처를 참조하지 않으므로 어느 패스에서나
// 안전하게 포함할 수 있다.
//
// Universal2D · NormalsRendering · UniversalForward 세 패스가 <b>같은 UnityPerMaterial
// 레이아웃</b>을 가져야 SRP Batcher 가 묶는다. 그래서 CBUFFER 를 이 한 파일로 모았다.

// ─────────────────────────────────────────────────────────────
// 디버그 전역값. UnityPerMaterial 밖에 둬야 SRP Batcher 가 깨지지 않는다.
//
// "끄기(mute)" 의미로 만든 것이 중요하다. 값을 아무도 넣지 않으면 0 이 되고,
// 0 은 "모든 채널을 그대로 쓴다" 여야 한다. "켜기" 의미로 만들면 디버그 컴포넌트가
// 없는 씬에서 채널이 전부 빠진 화면이 나온다.
float _TCChannelIsolate;   // 0 합성 · 1 Albedo · 2 Normal · 3 Emission · 4 Mask · 5 AO · 6 조명만
float _TCMuteNormal;
float _TCMuteEmission;
float _TCMuteMask;
float _TCMuteAO;

// 품질 단계의 노멀 세기 배율(§13 "Low: 노멀 단순화").
// <b>0 이 기본이면 안 된다</b> — 이 전역을 설정하지 않는 씬에서 노멀이 사라진다.
// 그래서 "감소량" 으로 담는다: 0 = 그대로, 1 = 완전 평면.
float _TCNormalReduce;

// NOTE: SRP Batcher 는 레이아웃이 다르면 묶지 못한다 — 여기의 프로퍼티를 ifdef 하지 않는다.
CBUFFER_START(UnityPerMaterial)
    half4 _Color;
    half4 _EmissionColor;
    half _EmissionIntensity;
    half _NormalStrength;
    half _AOStrength;
    half _MinLight;
    // 조명 거리 어둠(§7.5 대기 원근의 "저층 안개" 와 다른 축이다 — 이건 광원 거리다).
    half4 _DarkTint;
    half _DarkStrength;
    half _DarkKnee;
    half _DarkCurve;
CBUFFER_END

#endif // TUNNEL_CREW_LIT_2D_PROPS_INCLUDED
