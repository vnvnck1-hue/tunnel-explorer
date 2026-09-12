Shader "Tunnel Crew/Projectile-Energy"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _SecondaryColor ("Hot Core", Color) = (1,1,1,1)
        _Glow ("Glow", Range(0,5)) = 1.8
        _Shape ("Shape", Range(0,8)) = 0
        _AccentMode ("Accent", Range(0,8)) = 0
        _Seed ("Seed", Float) = 0
        _Pulse ("Pulse Rate", Float) = 40
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
                float4 _SecondaryColor;
                float _Glow;
                float _Shape;
                float _AccentMode;
                float _Seed;
                float _Pulse;
            CBUFFER_END

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                o.color = i.color * _Color;
                return o;
            }

            float Ellipse(float2 p, float2 radius)
            {
                float d = length(p / radius);
                return 1.0 - smoothstep(.78, 1.0, d);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;
                float t = _Time.y;
                float flicker = .90 + .10 * sin(t * _Pulse + _Seed * 13.71);
                float edgeNoise = sin(p.x * 19.0 - t * 24.0 + _Seed * 9.0)
                                * sin(p.y * 13.0 + t * 17.0 + _Seed * 5.0);

                float mask;
                float core;
                if (_Shape < .5)
                {
                    float w = lerp(.12, .68, smoothstep(-1.0, .52, p.x));
                    mask = (1.0 - smoothstep(w * .64, w, abs(p.y))) * smoothstep(-1.0, -.66, p.x);
                    mask *= 1.0 - smoothstep(.72, 1.0, p.x);
                    core = Ellipse(p - float2(.42, 0), float2(.43, .29));
                }
                else if (_Shape < 1.5)
                {
                    mask = Ellipse(p, float2(.82, .78));
                    mask *= saturate(1.0 + edgeNoise * .20);
                    core = Ellipse(p - float2(.12, .04), float2(.40, .34));
                }
                else if (_Shape < 2.5)
                {
                    float taper = saturate((p.x + 1.0) * 1.35) * saturate((1.0 - p.x) * 2.8);
                    mask = 1.0 - smoothstep(.08 + taper * .22, .18 + taper * .34, abs(p.y));
                    core = (1.0 - smoothstep(.025, .105, abs(p.y))) * smoothstep(-.68, -.15, p.x);
                }
                else if (_Shape < 3.5)
                {
                    float d = abs(p.x) + abs(p.y) * 1.18;
                    mask = 1.0 - smoothstep(.72, 1.0, d);
                    core = 1.0 - smoothstep(.18, .48, d);
                }
                else if (_Shape < 4.5)
                {
                    float d = length(p);
                    float rim = .06 * sin(15.0 * atan2(p.y, p.x) + t * 9.0 + _Seed);
                    mask = 1.0 - smoothstep(.68 + rim, .96 + rim, d);
                    core = 1.0 - smoothstep(.16, .48, d);
                    core += (1.0 - smoothstep(.035, .10, abs(d - .61))) * .65;
                }
                else if (_Shape < 5.5)
                {
                    float w = lerp(.06, .70, smoothstep(-.95, .62, p.x));
                    mask = (1.0 - smoothstep(w * .58, w, abs(p.y))) * (1.0 - smoothstep(.62, 1.0, p.x));
                    core = Ellipse(p - float2(.34, 0), float2(.48, .31));
                }
                else if (_Shape < 6.5)
                {
                    mask = (1.0 - smoothstep(.24, .46, abs(p.y))) * smoothstep(-1.0, -.78, p.x) * (1.0 - smoothstep(.82, 1.0, p.x));
                    core = (1.0 - smoothstep(.025, .09, abs(p.y))) * smoothstep(-.88, -.35, p.x);
                    core += Ellipse(p - float2(.66, 0), float2(.24, .24));
                }
                else if (_Shape < 7.5)
                {
                    float2 a = abs(p);
                    float hex = max(a.y, a.x * .866 + a.y * .50);
                    mask = 1.0 - smoothstep(.68, .91, hex);
                    core = 1.0 - smoothstep(.21, .43, hex);
                    core += (1.0 - smoothstep(.025, .085, abs(hex - .58))) * .55;
                }
                else
                {
                    float d = abs(p.x) * .46 + abs(p.y) * 1.65;
                    mask = 1.0 - smoothstep(.66, 1.0, d);
                    core = (1.0 - smoothstep(.05, .20, abs(p.y + p.x * .10))) * mask;
                }

                float scan = .82 + .18 * sin(p.x * (13.0 + _AccentMode) - t * (8.0 + _AccentMode) + _Seed * 4.0);
                mask = saturate(mask * lerp(1.0, scan, saturate(_AccentMode * .08)) * flicker);
                core = saturate(core * (1.05 + .15 * edgeNoise));
                float texA = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
                float aura = saturate(mask - core * .20);
                float3 rgb = i.color.rgb * aura * _Glow + _SecondaryColor.rgb * core * 2.15;
                float alpha = saturate(mask * i.color.a * texA);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
