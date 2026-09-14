Shader "Tunnel Crew/Organic Rock"
{
    Properties
    {
        _LabUVRect("Atlas interior UV", Vector) = (0,0,1,1)
        _LabMacro("World-space variation", Range(0,1)) = 1
        [Header(Channels)]
        _MainTex("Albedo", 2D) = "white" {}
        _MaskTex("Material Mask (R metal G gloss B wet)", 2D) = "white" {}
        _NormalMap("Normal", 2D) = "bump" {}
        _EmissionMap("Emission", 2D) = "black" {}
        _AOMap("AO (white = no occlusion)", 2D) = "white" {}

        [Header(Response)]
        _NormalStrength("Normal Strength", Range(0, 3)) = 1.6
        _AOStrength("AO Strength", Range(0, 1)) = 1
        _EmissionColor("Emission Tint", Color) = (1,1,1,1)
        _EmissionIntensity("Emission Intensity", Range(0, 8)) = 1
        _MinLight("Min Light", Range(0, 1)) = 0.14

        [Header(Distance Darkness)]
        _DarkTint("어둠 색(곱)", Color) = (0.28, 0.31, 0.42, 1)
        _DarkStrength("어둠 강도", Range(0, 1)) = 0.38
        _DarkKnee("어둠이 시작되는 조명량", Range(0.01, 1)) = 0.5
        _DarkCurve("어둠 곡선", Range(1, 6)) = 2
        _ImpactWobble("파괴 충격 흔들림", Range(0, 2)) = 1

        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

        // 레거시 프로퍼티. 이 셰이더를 쓰는 머티리얼이 기본 스프라이트 셰이더로
        // 되돌아갈 때 조용히 동작하도록 남긴다.
        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "TunnelCrewImpact.hlsl"
            float4 _LabUVRect;
            float _LabMacro;
            float2 LabUV(float2 w) { float2 p = w * 0.53 + 0.045 * sin(w.yx * 1.71); return _LabUVRect.xy + frac(p) * _LabUVRect.zw; }
            half LabMacro(float2 w) { return lerp(1.0, 0.82 + 0.16 * sin(w.x * .71 + sin(w.y * .43)) + .08 * sin(w.y * 1.12), _LabMacro); }

            #pragma vertex TCLitVertex
            #pragma fragment TCLitFragmentEntry

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color        : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_LIT_OUTPUTS
                half4 color        : COLOR;
            };

            #include "TunnelCrewLit2D.hlsl"

            Varyings TCLitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                // Mesh vertices use the same impact deformation as the tile walls.
                input.positionOS = TCApplyImpact(input.positionOS, _ImpactWobble);


                Varyings o = CommonLitVertex(input);
                o.color = input.color * _Color;
                return o;
            }

            half4 TCLitFragmentEntry(Varyings input) : SV_Target
            {
                input.color.rgb *= LabMacro(input.uv);
                input.uv = LabUV(input.uv);
                return TCLitFragment(input, input.color);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "NormalsRendering"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "TunnelCrewImpact.hlsl"
            float4 _LabUVRect;
            float _LabMacro;
            float2 LabUV(float2 w) { float2 p = w * 0.53 + 0.045 * sin(w.yx * 1.71); return _LabUVRect.xy + frac(p) * _LabUVRect.zw; }
            half LabMacro(float2 w) { return lerp(1.0, 0.82 + 0.16 * sin(w.x * .71 + sin(w.y * .43)) + .08 * sin(w.y * 1.12), _LabMacro); }

            #pragma vertex TCNormalsVertex
            #pragma fragment TCNormalsFragment

            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_NORMALS_INPUTS
                float4 color        : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_NORMALS_OUTPUTS
                half4   color           : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Normals2DCommon.hlsl"
            #include "TunnelCrewLit2DNormal.hlsl"

            Varyings TCNormalsVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                // Mesh vertices use the same impact deformation as the tile walls.
                input.positionOS = TCApplyImpact(input.positionOS, _ImpactWobble);


                // Tilemap 청크 메시에는 NORMAL/TANGENT 가 없어 TBN 이 0 이 된다.
                // 스프라이트와 같은 기준으로 강제한다: 카메라를 향한 노멀 · +X 탄젠트 ·
                // 바이탄젠트 +Y (w = -1).
                input.normal  = float3(0, 0, -1);
                input.tangent = float4(1, 0, 0, -1);

                Varyings o = CommonNormalsVertex(input);
                o.color = input.color * _Color;
                return o;
            }

            half4 TCNormalsFragment(Varyings input) : SV_Target
            {
                input.uv = LabUV(input.uv);
                const half4 mainTex = input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                const half3 normalTS = TCNormalTS(input.uv);
                return NormalsRenderingShared(mainTex, normalTS,
                    input.tangentWS.xyz, input.bitangentWS.xyz, input.normalWS.xyz);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" "Queue"="Transparent" "RenderType"="Transparent"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "TunnelCrewImpact.hlsl"
            float4 _LabUVRect;
            float _LabMacro;
            float2 LabUV(float2 w) { float2 p = w * 0.53 + 0.045 * sin(w.yx * 1.71); return _LabUVRect.xy + frac(p) * _LabUVRect.zw; }
            half LabMacro(float2 w) { return lerp(1.0, 0.82 + 0.16 * sin(w.x * .71 + sin(w.y * .43)) + .08 * sin(w.y * 1.12), _LabMacro); }

            #pragma vertex TCUnlitVertex
            #pragma fragment TCUnlitFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            #include "TunnelCrewLit2DProps.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            Varyings TCUnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                // Mesh vertices use the same impact deformation as the tile walls.
                input.positionOS = TCApplyImpact(input.positionOS, _ImpactWobble);


                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color;
                return o;
            }

            half4 TCUnlitFragment(Varyings input) : SV_Target
            {
                input.color.rgb *= LabMacro(input.uv);
                input.uv = LabUV(input.uv);
                return CommonUnlitFragment(input, input.color);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
