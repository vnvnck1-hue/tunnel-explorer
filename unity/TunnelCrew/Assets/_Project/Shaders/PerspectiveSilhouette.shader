// 3d-perspective-production-plan §4 6단계 — 벽에 가린 액터의 실루엣.
//
// 원근 카메라와 액터 사이에 벽이 들어와도 "지금 어디에 무엇이 있는가" 를 잃지 않게 한다.
// 벽 자체를 지우거나 투명하게 만들지 않는 것은 아트의 덩어리감을 지키기 위해서다 —
// 2D 본선의 OccludedSilhouetteRenderer 와 같은 선택이다.
//
// 가림 판정은 깊이 버퍼가 아니라 C# 이 격자로 한다(PerspectiveWorldView.IsOccluded).
// URP 2D 렌더러에서는 이 경로의 깊이 쓰기를 믿을 수 없어 ZTest Greater 가 액터 전체를 덮었다
// (2026-09-16 실측). 그래서 여기서는 항상 그리고, 켜고 끄는 것은 C# 이 정한다.
Shader "Tunnel Crew/PerspectiveSilhouette"
{
    Properties
    {
        _MainTex("Albedo", 2D) = "white" {}
        _SilhouetteColor("실루엣 색", Color) = (0.42, 0.82, 1, 0.40)
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "PerspectiveSilhouette"
            Tags { "LightMode" = "Universal2D" }

            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _SilhouetteColor;
                float _Cutoff;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
                clip(alpha - _Cutoff);
                return half4(_SilhouetteColor.rgb, _SilhouetteColor.a);
            }
            ENDHLSL
        }
    }
}
