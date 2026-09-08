#ifndef TUNNEL_CREW_LIT_2D_NORMAL_INCLUDED
#define TUNNEL_CREW_LIT_2D_NORMAL_INCLUDED

// _NormalMap 이 선언된 패스에서만 포함한다(Lit2DCommon.hlsl 또는 Normals2DCommon.hlsl 뒤).
// Universal2D 패스와 NormalsRendering 패스가 노멀을 같은 방식으로 읽어야
// 디버그 토글과 세기 조절이 실제 조명에 그대로 반영된다.

#include "TunnelCrewLit2DProps.hlsl"

half3 TCNormalTS(float2 uv)
{
    half3 n = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv));
    // xy 만 줄이고 다시 정규화한다 — z 를 건드리면 평면 중립값이 흔들린다.
    // 품질 단계 배율을 함께 적용한다. _TCNormalReduce 는 감소량이라 기본 0 이 곧 원본이다.
    n.xy *= _NormalStrength * saturate(1.0h - _TCNormalReduce);
    return normalize(lerp(n, half3(0, 0, 1), saturate(_TCMuteNormal)));
}

#endif // TUNNEL_CREW_LIT_2D_NORMAL_INCLUDED
