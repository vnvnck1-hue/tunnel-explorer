// 3d-perspective-production-plan §4 5단계 — 선·궤적·파티클처럼 스스로 빛나는 연출용 재질.
//
// 조명을 받지 않고 정점 색을 그대로 곱한다. LineRenderer/TrailRenderer 의 그라디언트와
// ParticleSystem 의 색·알파가 정점 색으로 들어오므로, 이것을 존중해야 2D 본선과 같은 색이 나온다.
// URP 2D 렌더러는 Universal2D LightMode 패스만 그린다.
Shader "Tunnel Crew/PerspectiveOverlay"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _Tint("Tint", Color) = (1,1,1,1)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 5   // SrcAlpha
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 10  // OneMinusSrcAlpha
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "PerspectiveOverlay"
            Tags { "LightMode" = "Universal2D" }

            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend [_SrcBlend] [_DstBlend]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Tint;
                float _SrcBlend, _DstBlend;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4 color       : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4 color       : COLOR;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color * (half4)_Tint;
                clip(c.a - 0.004h);
                return c;
            }
            ENDHLSL
        }
    }
}
