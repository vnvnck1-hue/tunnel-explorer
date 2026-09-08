#ifndef TUNNEL_CREW_LIT_2D_INCLUDED
#define TUNNEL_CREW_LIT_2D_INCLUDED

// 기능명세서 §7.1 — Albedo / Normal / Emission / Material Mask / AO 를 받는 통합 월드 재질.
//
// URP 17.3 의 2D 조명 경로에 얹는다. 조명 계산을 다시 쓰지 않고
// CombinedShapeLightShared 의 결과에 채널을 더한다 — URP 가 Light Blend Style,
// 마스크 필터, HDR 배율을 이미 처리하기 때문이다.
//
// 채널이 들어가는 자리:
//   AO       : 조명 전에 Albedo 에 곱한다. 들어오는 빛을 감쇠시키는 것이 물리적으로 맞는 자리다.
//   Mask     : surfaceData.mask 로 넘긴다. URP 가 Light Blend Style 의 마스크 필터에 쓴다.
//   Normal   : Lit 패스가 아니라 NormalsRendering 패스에서 쓴다(URP 가 노멀 버퍼를 따로 굽는다).
//   Emission : 조명 결과에 더한다. 광원에 곱해지면 안 된다.
//   MinLight : 광원이 꺼져도 형태가 읽히는 최저 조도(§7.2). 조명 결과와 max 를 취한다.
//
// Albedo 에 최종 조명을 굽지 않는다. Albedo 는 재질 고유색과 약한 자체 음영만 담는다
// (docs/test-room-art-production-spec.md §5).
//
// 이 파일은 Attributes/Varyings 구조체가 선언된 뒤, Universal2D 패스 안에서 포함한다.

#include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"
#include "TunnelCrewLit2DNormal.hlsl"

TEXTURE2D(_EmissionMap);
SAMPLER(sampler_EmissionMap);

TEXTURE2D(_AOMap);
SAMPLER(sampler_AOMap);

struct TCChannels
{
    half4 albedo;
    half4 mask;
    half3 normalTS;
    half3 emission;
    half ao;
};

TCChannels TCSampleChannels(float2 uv, half4 vertexColor)
{
    TCChannels ch;

    ch.albedo = vertexColor * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

    ch.mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, uv);
    ch.mask = lerp(ch.mask, half4(1, 1, 1, 1), saturate(_TCMuteMask));

    ch.normalTS = TCNormalTS(uv);

    half4 em = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv);
    // 발광은 고유 발광색을 유지한다 — 광원 중심을 흰색으로 고정하지 않는다(아트 규격 §5).
    ch.emission = em.rgb * _EmissionColor.rgb * _EmissionIntensity * em.a;
    ch.emission *= (1.0h - saturate(_TCMuteEmission));

    half occ = SAMPLE_TEXTURE2D(_AOMap, sampler_AOMap, uv).r;
    // 흰색 = 차폐 없음. 세기로 보간해 0 이면 AO 를 쓰지 않는다.
    ch.ao = lerp(1.0h, occ, _AOStrength * (1.0h - saturate(_TCMuteAO)));

    return ch;
}

/// 채널 하나만 보여 준다(§12.3 "Albedo/Normal/Emission/AO 단독 보기").
/// 합성 모드면 false 를 돌려준다.
bool TCIsolateChannel(in TCChannels ch, out half4 outColor)
{
    int mode = (int)round(_TCChannelIsolate);
    outColor = half4(0, 0, 0, ch.albedo.a);

    if (mode == 1)        // Albedo — 조명 없이 있는 그대로
    {
        outColor.rgb = ch.albedo.rgb;
        return true;
    }
    if (mode == 2)        // Normal — 탄젠트 공간을 그대로 색으로
    {
        outColor.rgb = ch.normalTS * 0.5h + 0.5h;
        return true;
    }
    if (mode == 3)        // Emission
    {
        outColor.rgb = ch.emission;
        return true;
    }
    if (mode == 4)        // Material Mask — R 금속 / G 광택 / B 습윤·결정
    {
        outColor.rgb = ch.mask.rgb;
        return true;
    }
    if (mode == 5)        // AO
    {
        outColor.rgb = half3(ch.ao, ch.ao, ch.ao);
        return true;
    }
    return false;
}

half4 TCLitFragment(Varyings input, half4 vertexColor)
{
    TCChannels ch = TCSampleChannels(input.uv, vertexColor);

    half4 isolated;
    if (TCIsolateChannel(ch, isolated))
    {
        if (isolated.a == 0.0h) discard;
        return isolated;
    }

    int mode = (int)round(_TCChannelIsolate);
    // 조명만 보기 — 알베도를 흰색으로 바꿔 조명 분포만 남긴다.
    half3 albedo = (mode == 6) ? half3(1, 1, 1) : ch.albedo.rgb;

    // AO 는 조명 전에 알베도에 곱한다.
    half3 occluded = albedo * ch.ao;

    SurfaceData2D surfaceData;
    InputData2D inputData;
    InitializeSurfaceData(occluded, ch.albedo.a, ch.mask, ch.normalTS, surfaceData);
    InitializeInputData(input.uv, input.lightingUV, inputData);

#if defined(DEBUG_DISPLAY)
    SETUP_DEBUG_TEXTURE_DATA_2D_NO_TS(inputData, input.positionWS, input.positionCS, _MainTex);
    surfaceData.normalWS = input.normalWS;
#endif

    half4 lit = CombinedShapeLightShared(surfaceData, inputData);

    // 최저 조도 — 광원이 하나도 없어도 형태가 읽혀야 한다(§7.2·§15.1).
    // 조명 결과와 max 를 취하므로 밝은 곳을 더 밝게 만들지는 않는다.
    lit.rgb = max(lit.rgb, occluded * _MinLight);

    // ── 조명 거리 어둠 (2026-09-09)
    //
    // "조명과 먼 곳이 전장의 안개처럼 더 짙게 어두워야 한다" 는 요청을 여기서 처리한다.
    // Atmosphere 패스의 안개는 화면 세로 위치(uv.y)로만 작동해서 광원과의 거리를 모른다.
    // 그래서 광원 거리를 아는 유일한 자리인 이 프래그먼트에서 만든다.
    //
    // 받은 빛의 양은 알베도를 곱하기 전의 값이어야 한다 — 그래야 검은 암석이
    // "빛이 없는 곳" 으로 오해받지 않는다. 그 값이 블렌드 스타일 0 의 광원 텍스처다
    // (우리 광원 전부가 Multiply = 블렌드 스타일 0 을 쓴다).
    #if USE_SHAPE_LIGHT_TYPE_0
    {
        half3 lightAmt = SAMPLE_TEXTURE2D(_ShapeLightTexture0, sampler_ShapeLightTexture0,
                                          inputData.lightingUV).rgb;
        half amt = max(lightAmt.r, max(lightAmt.g, lightAmt.b));
        // knee 위는 손대지 않고, 그 아래만 곡선으로 끌어내린다.
        half t = saturate(amt / max(1e-4h, _DarkKnee));
        half darkness = pow(saturate(1.0h - t), max(1.0h, _DarkCurve)) * saturate(_DarkStrength);
        lit.rgb = lerp(lit.rgb, lit.rgb * _DarkTint.rgb, darkness);
    }
    #endif

    // 발광은 어둠 뒤에 더한다 — 수정·표시등은 빛이 닿지 않는 곳에서도 스스로 보여야 한다.
    if (mode != 6) lit.rgb += ch.emission;

    lit.a = ch.albedo.a;
    return max(0, lit);
}

#endif // TUNNEL_CREW_LIT_2D_INCLUDED
