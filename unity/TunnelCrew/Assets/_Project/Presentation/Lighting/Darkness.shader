// 시야 밖을 덮는 어둠. 원본 FOW 의 LOS 합성(analysis-01 §4.1~4.2)을 대신한다.
//
// 원본은 셀 해상도(80x72) RGBA 버퍼를 WebGL 텍스처로 올리고 바이리니어로 늘려 썼다.
// 그래서 어둠 경계가 부드러웠다. 같은 방식을 그대로 쓴다.
//   R 채널 = 지금 보이는가 (0 또는 1, 시간 보간됨)
//   G 채널 = 탐색 기억 농도 (0~1)
//
// 카메라 앞의 쿼드 하나에 붙여 월드 좌표로 샘플한다.
Shader "TunnelCrew/Darkness"
{
    Properties
    {
        _LosTex        ("LOS (R=visible, G=memory)", 2D) = "black" {}
        _DarkColor     ("어둠 색", Color) = (0.03, 0.025, 0.06, 1)
        _MemoryColor   ("기억 구역 색", Color) = (0.10, 0.09, 0.16, 1)
        _WorldSize     ("월드 크기(셀)", Vector) = (80, 72, 0, 0)
        _MaxDarkness   ("최대 어둠", Range(0,1)) = 1.0
        _EdgeSoftness  ("경계 부드러움", Range(0.001,1)) = 0.35
        // 렌더→시뮬 역변환 (a,b,c,d): sim = (a·rx + b·ry, c·rx + d·ry). 기본값은 2:1 마름모.
        _InvProj       ("역투영 행", Vector) = (1, 2, -1, 2)
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector"= "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "Darkness"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 worldXY    : TEXCOORD0;
            };

            TEXTURE2D(_LosTex);
            SAMPLER(sampler_LosTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _DarkColor;
                float4 _MemoryColor;
                float4 _WorldSize;
                float  _MaxDarkness;
                float  _EdgeSoftness;
                float4 _InvProj;
            CBUFFER_END

            Varyings vert (Attributes input)
            {
                Varyings o;
                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(worldPos);
                o.worldXY = worldPos.xy;
                return o;
            }

            half4 frag (Varyings input) : SV_Target
            {
                // 렌더 좌표를 시뮬레이션 XY 로 되돌린 뒤 LOS 텍스처를 읽는다.
                // 역변환은 투영 프리셋마다 달라서 IsometricProjection.InverseRow() 가 넘겨준다.
                float2 simXY = float2(dot(_InvProj.xy, input.worldXY),
                                      dot(_InvProj.zw, input.worldXY));
                float2 uv = simXY / _WorldSize.xy;

                // 맵 밖은 완전한 어둠
                if (uv.x < 0 || uv.y < 0 || uv.x > 1 || uv.y > 1)
                    return half4(_DarkColor.rgb, _MaxDarkness);

                // ── 원본 HTML(v7.9.2 fogProg) 의 LOS 합성을 그대로 옮긴다(2026-09-10).
                // 바이리니어 한 번으로는 셀 경계가 네모로 딱딱 끊기고, 시야가 바뀌는 칸이 통째로 튀어 화면이 정신없다.
                // 원본은 ① 1.35칸 간격 9탭 필터로 약 1.5칸 폭의 반그림자를 만들고 ② 경계에만 월드 고정 노이즈를 섞어
                // 완벽한 원·타일 윤곽을 깨고 ③ smoothstep 으로 시야/기억을 정리한 뒤 ④ "알려진 정도"로 어둠 알파를 냈다.
                // 오프셋은 월드 셀 단위라 텍스처 해상도(supersample)와 무관하다.
                float2 d = 1.35 / _WorldSize.xy;
                half2 s = SAMPLE_TEXTURE2D(_LosTex, sampler_LosTex, uv).rg * 0.24;
                s += (SAMPLE_TEXTURE2D(_LosTex, sampler_LosTex, uv + float2(d.x, 0)).rg
                    + SAMPLE_TEXTURE2D(_LosTex, sampler_LosTex, uv - float2(d.x, 0)).rg
                    + SAMPLE_TEXTURE2D(_LosTex, sampler_LosTex, uv + float2(0, d.y)).rg
                    + SAMPLE_TEXTURE2D(_LosTex, sampler_LosTex, uv - float2(0, d.y)).rg) * 0.12;
                s += (SAMPLE_TEXTURE2D(_LosTex, sampler_LosTex, uv + d).rg
                    + SAMPLE_TEXTURE2D(_LosTex, sampler_LosTex, uv - d).rg
                    + SAMPLE_TEXTURE2D(_LosTex, sampler_LosTex, uv + float2(d.x, -d.y)).rg
                    + SAMPLE_TEXTURE2D(_LosTex, sampler_LosTex, uv + float2(-d.x, d.y)).rg) * 0.07;
                s = saturate(s);
                half visible = s.r;
                half memory  = s.g;   // 0~0.29 범위의 "농도"(LosService.MemoryValue)

                // 경계에만 약한 월드 고정 노이즈 — 카메라가 움직여도 무늬가 크롤링하지 않는다.
                float2 np = floor(simXY * 0.72);
                half noise = frac(sin(dot(np, float2(127.1, 311.7))) * 43758.5453);
                half edge = 4.0 * visible * (1.0 - visible);
                visible = saturate(visible + (noise - 0.5) * 0.16 * edge);

                // _EdgeSoftness = 시야 smoothstep 의 상한(원본 0.88). 낮추면 경계가 날카로워진다.
                visible = smoothstep(0.035, max(_EdgeSoftness, 0.05), visible);
                memory  = smoothstep(0.004, 0.24, memory) * 0.22;

                // 알려진 정도 → 어둠. 기억은 시야의 1.45배 가중으로 "본 적 있는 곳"을 보랏빛 공간감으로 남긴다.
                half known  = max(visible, memory * 1.45);
                half unseen = 1.0 - smoothstep(0.015, 0.46, known);
                half alpha  = saturate(unseen * _MaxDarkness);

                half memoryMix = (1.0 - visible) * smoothstep(0.01, 0.20, memory);
                half3 tint = lerp(_DarkColor.rgb, _MemoryColor.rgb, memoryMix);

                return half4(tint, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
