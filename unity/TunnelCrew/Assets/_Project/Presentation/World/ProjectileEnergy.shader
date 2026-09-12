Shader "Tunnel Crew/Projectile-Energy"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Glow ("Glow", Range(0,4)) = 1.7
        _Tail ("Tail", Range(0,1)) = .72
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
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
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _Glow;
                float _Tail;
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
                // Projectile points to +X. One quad contains a tapered tail, luminous body and hot head.
                float2 p = i.uv * 2.0 - 1.0;
                float x01 = saturate(i.uv.x);
                float width = lerp(.08, .56, smoothstep(0.0, .78, x01));
                float body = 1.0 - smoothstep(width * .42, width, abs(p.y));
                float tailGate = smoothstep(1.0 - _Tail, .52, x01);
                float head = 1.0 - smoothstep(.12, .54, length(float2((p.x - .62) * 1.35, p.y)));
                float endFade = smoothstep(0.0, .08, x01) * (1.0 - smoothstep(.94, 1.0, x01));
                float alpha = saturate(max(body * tailGate, head) * endFade);
                float core = 1.0 - smoothstep(width * .10, width * .34, abs(p.y));
                core *= smoothstep(.18, .72, x01);
                float texA = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
                float3 rgb = i.color.rgb * (alpha * _Glow) + core.xxx * alpha * 1.45;
                return half4(rgb, alpha * i.color.a * texA);
            }
            ENDHLSL
        }
    }
}
