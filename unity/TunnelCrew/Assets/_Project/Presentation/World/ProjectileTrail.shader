Shader "Tunnel Crew/Projectile-Trail"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _Flow ("Flow", Float) = 18
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Flow;
            CBUFFER_END

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                o.color = i.color * _Color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float edge = 1.0 - smoothstep(.08, .50, abs(i.uv.y - .5));
                float broken = .76 + .24 * sin(i.uv.x * 24.0 - _Time.y * _Flow);
                float hot = pow(edge, 4.0);
                float a = edge * broken * i.color.a;
                float3 rgb = i.color.rgb * (edge * 1.45 + hot * 1.85);
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
}
