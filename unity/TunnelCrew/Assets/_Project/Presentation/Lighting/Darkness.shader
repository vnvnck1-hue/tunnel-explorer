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

                half2 los = SAMPLE_TEXTURE2D(_LosTex, sampler_LosTex, uv).rg;
                half visible = los.r;
                half memory  = los.g;   // 이미 0~0.29 범위의 "농도" 다

                // 밝기는 두 값 중 큰 쪽. 원본은 memory 를 그대로 밝기로 썼으므로
                // 여기에 smoothstep 을 걸면 안 된다. 걸면 29% 기억이 거의 최대 밝기가 된다.
                // 부드러운 경계는 셀 해상도 텍스처의 바이리니어 보간이 이미 만든다.
                // _EdgeSoftness 는 "보이는" 채널의 가장자리만 살짝 다듬는 데 쓴다.
                visible = smoothstep(0.0, _EdgeSoftness, visible);
                half light = max(visible, memory);

                half alpha = saturate((1.0 - light) * _MaxDarkness);

                // 기억으로만 보이는 구역은 완전한 검정이 아니라 살짝 색이 남는다.
                half memoryWeight = saturate(memory - visible);
                half3 tint = lerp(_DarkColor.rgb, _MemoryColor.rgb, memoryWeight);

                return half4(tint, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
