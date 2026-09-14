Shader "Tunnel Crew/Projectile-Smoke-Trail"
{
    // 비행 중에는 가는 리본, 탄 소멸 뒤에는 _Fade 곡선으로 남는 저대비 난류 연기.
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _Fade ("Fade", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; float2 world : TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Fade;
            CBUFFER_END

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(i.positionOS.xyz);
                float age = saturate(i.uv.x);
                float curl = sin(ws.x * 3.1 + ws.y * 2.3 + _Time.y * 1.4 + age * 7.0);
                ws.xy += float2(-.35, 1.0) * curl * age * .018;
                ws.y += age * age * .045;
                o.positionHCS = TransformWorldToHClip(ws);
                o.uv = i.uv;
                o.color = i.color * _Color;
                o.world = ws.xy;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float edge = 1.0 - smoothstep(.13, .50, abs(i.uv.y - .5));
                float cloud = .72 + .28 * sin(i.uv.x * 31.0 + i.world.x * 5.7 - _Time.y * 1.2);
                cloud *= .78 + .22 * hash21(floor(i.world * 17.0) + floor(_Time.y * 2.0));
                float a = edge * cloud * i.color.a * _Fade;
                return half4(i.color.rgb, a);
            }
            ENDHLSL
        }
    }
}
