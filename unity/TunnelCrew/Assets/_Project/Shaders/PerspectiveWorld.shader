// 3d-perspective-production-plan §4 3~4단계 — 원근 월드의 바닥·벽 표면 재질.
//
// Albedo 는 기존 EnvironmentKit 스프라이트 아틀라스에서 그대로 온다. Normal·Emission·
// AO·Mask 는 SurfaceMaterialSet 이 주던 것과 같은 텍스처를 같은 UV 로 읽는다.
// 조명만 URP 2D 경로 대신 PerspectiveLights.hlsl 의 하이브리드 버퍼를 쓴다.
Shader "Tunnel Crew/PerspectiveWorld"
{
    Properties
    {
        _MainTex("Albedo", 2D) = "white" {}
        _NormalMap("Normal", 2D) = "bump" {}
        _EmissionMap("Emission", 2D) = "black" {}
        _AOMap("AO (white = no occlusion)", 2D) = "white" {}

        _NormalStrength("Normal Strength", Range(0, 3)) = 1.0
        _AOStrength("AO Strength", Range(0, 1)) = 1
        _EmissionColor("Emission Tint", Color) = (1,1,1,1)
        _EmissionIntensity("Emission Intensity", Range(0, 8)) = 1
        _MinLight("Min Light", Range(0, 1)) = 0.14
        _SurfaceTint("Surface Tint", Color) = (1,1,1,1)

        _DarkTint("어둠 색(곱)", Color) = (0.28, 0.31, 0.42, 1)
        _DarkStrength("어둠 강도", Range(0, 1)) = 0.38
        _DarkKnee("어둠이 시작되는 조명량", Range(0.01, 1)) = 0.5
        _DarkCurve("어둠 곡선", Range(1, 6)) = 2

        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.35
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        [Toggle] _UseTopAmbient("벽 윗면 전역광을 쓴다", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "PerspectiveWorld"
            Tags { "LightMode" = "Universal2D" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "PerspectiveLights.hlsl"

            TEXTURE2D(_MainTex);       SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap);     SAMPLER(sampler_NormalMap);
            TEXTURE2D(_EmissionMap);   SAMPLER(sampler_EmissionMap);
            TEXTURE2D(_AOMap);         SAMPLER(sampler_AOMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _NormalStrength, _AOStrength, _EmissionIntensity, _MinLight;
                float4 _EmissionColor, _SurfaceTint;
                float4 _DarkTint;
                float _DarkStrength, _DarkKnee, _DarkCurve;
                float _Cutoff, _Cull, _UseTopAmbient;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                half4 color       : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float3 tangentWS  : TEXCOORD3;
                float3 bitangentWS: TEXCOORD4;
                half4 color       : COLOR;
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.tangentWS = TransformObjectToWorldDir(v.tangentOS.xyz);
                o.bitangentWS = cross(o.normalWS, o.tangentWS) * v.tangentOS.w;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color * _SurfaceTint;
                clip(albedo.a - _Cutoff);

                half3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv), _NormalStrength);
                float3x3 tbn = float3x3(normalize(i.tangentWS), normalize(i.bitangentWS), normalize(i.normalWS));
                half3 normalWS = normalize(mul(normalTS, tbn));

                half occ = SAMPLE_TEXTURE2D(_AOMap, sampler_AOMap, i.uv).r;
                half ao = lerp(1.0h, occ, _AOStrength);

                half3 ambient = _UseTopAmbient > 0.5 ? (half3)_TCAmbientTop.rgb : (half3)_TCAmbient.rgb;
                half3 light = TCEvaluateLights(i.positionWS, normalWS, ambient);
                light = max(light, (half3)_MinLight.xxx);

                // WorldLit 과 같은 거리 어둠 — 조명이 약한 곳은 채도를 낮추고 한기를 준다.
                half lum = dot(light, half3(0.299h, 0.587h, 0.114h));
                half dark = pow(saturate(1.0h - lum / max(0.01h, (half)_DarkKnee)), (half)_DarkCurve) * (half)_DarkStrength;
                half3 tinted = lerp((half3)1.0h, (half3)_DarkTint.rgb, dark);

                half4 em = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv);
                half3 emission = em.rgb * _EmissionColor.rgb * _EmissionIntensity * em.a;

                half3 rgb = albedo.rgb * ao * light * tinted + emission;
                return half4(rgb, 1.0h);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
