#ifndef TUNNEL_CREW_LIT_2D_NORMAL_INCLUDED
#define TUNNEL_CREW_LIT_2D_NORMAL_INCLUDED

// _NormalMap 이 선언된 패스에서만 포함한다(Lit2DCommon.hlsl 또는 Normals2DCommon.hlsl 뒤).
// Universal2D 패스와 NormalsRendering 패스가 노멀을 같은 방식으로 읽어야
// 디버그 토글과 세기 조절이 실제 조명에 그대로 반영된다.

#include "TunnelCrewLit2DProps.hlsl"

float4 _MainTex_TexelSize;

half TCLuminanceAt(float2 uv)
{
    half3 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;
    return dot(c, half3(0.299h, 0.587h, 0.114h));
}

half3 TCNormalTS(float2 uv)
{
    half3 n = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv));

    // 신규 매크로는 아직 albedo-only다. 그 경우 인접 명도 경사로 낮은 주파수 요철을
    // 유도해 Light2D 방향이 갈라진 바위 면과 균열을 읽게 한다. 미러 반복 경계는
    // 양쪽 기울기가 0으로 만나므로 매크로 블록 이음새를 새로 만들지 않는다.
    if (_LabDerivedNormal > 0.001h)
    {
        float2 texel = _MainTex_TexelSize.xy * 2.0;
        half l = TCLuminanceAt(uv - float2(texel.x, 0));
        half r = TCLuminanceAt(uv + float2(texel.x, 0));
        half d = TCLuminanceAt(uv - float2(0, texel.y));
        half u = TCLuminanceAt(uv + float2(0, texel.y));
        half2 slope = half2(l - r, d - u) * (_NormalStrength * 3.0h);
        half3 derived = normalize(half3(slope, 1.0h));
        n = normalize(lerp(n, derived, saturate(_LabDerivedNormal)));
    }
    else
    {
        // xy 만 줄이고 다시 정규화한다 — z 를 건드리면 평면 중립값이 흔들린다.
        n.xy *= _NormalStrength;
    }

    // 품질 단계 배율을 함께 적용한다. _TCNormalReduce 는 감소량이라 기본 0 이 곧 원본이다.
    n.xy *= saturate(1.0h - _TCNormalReduce);
    return normalize(lerp(n, half3(0, 0, 1), saturate(_TCMuteNormal)));
}

#endif // TUNNEL_CREW_LIT_2D_NORMAL_INCLUDED
