// 기능명세서 §6.6 — "가려진 캐릭터에는 얇은 팀 색 실루엣 또는 림을 표시한다",
// "적은 완전 투명 처리하지 않고 위협 실루엣만 보장한다".
//
// 원본 스프라이트의 알파에서 <b>안쪽 림</b>을 뽑는다. 자기 알파와 이웃 알파의 차이가
// 곧 경계이므로, 실루엣 안쪽으로 _RimWidth 텍셀 두께의 테두리만 남는다.
// 위협 실루엣은 여기에 낮은 내부 채움(_Fill)을 더해 형태 전체가 읽히게 한다.
//
// 조명을 받지 않는다(Unlit). 가림 보정은 가독성 장치이므로 벽 뒤의 어둠에 함께
// 묻혀서는 안 된다. 대신 §6.4 의 VisionAndGrade 보다 아래인 WorldFX 에 그려서
// LOS 어둠 규칙은 계속 우선한다(§6.6 마지막 항).
Shader "TunnelCrew/OccludedSilhouette"
{
    Properties
    {
        [PerRendererData] _MainTex ("스프라이트", 2D) = "white" {}
        _RimWidth ("림 두께(텍셀)", Range(0.5, 8)) = 2
        _Fill     ("내부 채움", Range(0, 1)) = 0
        _AlphaCut ("실루엣으로 볼 최소 알파", Range(0.01, 0.9)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType"     = "Plane"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            Name "OccludedSilhouette"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            // 텍셀 크기는 텍스처에서 유도되는 내장값이라 UnityPerMaterial 밖에 있어야 한다.
            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float _RimWidth;
                float _Fill;
                float _AlphaCut;
            CBUFFER_END

            Varyings vert (Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.color = input.color;
                o.uv = input.uv;
                return o;
            }

            half A(float2 uv)
            {
                // 스프라이트 밖(아틀라스 이웃)을 읽지 않도록 UV 를 잘라낸다. 잘린 방향은
                // "비어 있음" 으로 취급해야 실루엣 가장자리에 림이 생긴다.
                if (uv.x < 0 || uv.y < 0 || uv.x > 1 || uv.y > 1) return 0;
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
                return step(_AlphaCut, a);
            }

            half4 frag (Varyings input) : SV_Target
            {
                float2 ts = _MainTex_TexelSize.xy * max(0.5, _RimWidth);
                half a = A(input.uv);

                // 4방향 + 대각선. 대각선을 빼면 45도 경사에서 림이 끊긴다.
                half n = min(min(A(input.uv + float2(ts.x, 0)), A(input.uv - float2(ts.x, 0))),
                             min(A(input.uv + float2(0, ts.y)), A(input.uv - float2(0, ts.y))));
                half d = min(min(A(input.uv + ts), A(input.uv - ts)),
                             min(A(input.uv + float2(ts.x, -ts.y)), A(input.uv - float2(ts.x, -ts.y))));

                // 안쪽 림 — 내부는 이웃이 모두 채워져 있어 0 이 된다.
                half rim = saturate(a - min(n, d));
                half cov = max(rim, a * _Fill);

                return half4(input.color.rgb, cov * input.color.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
