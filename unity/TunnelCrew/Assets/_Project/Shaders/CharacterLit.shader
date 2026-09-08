// 기능명세서 §7.1 — 캐릭터·적용 Lit 재질. 채널 파이프라인은 Tunnel Crew/WorldLit 과 같다.
//
// 월드 재질과 다른 점은 둘뿐이다.
//   1) 탄젠트를 강제하지 않는다. 스프라이트 메시는 제 NORMAL/TANGENT 를 갖고 있고,
//      2D Animation 의 스키닝 경로도 그것을 쓴다. 강제하면 스키닝이 깨진다.
//   2) 최저 조도 기본값이 높다 — 얼굴·헬멧·무기가 환경광 속에서도 읽혀야 한다(§7.2).
//
// 과도한 자체 발광으로 LOS 와 어둠 규칙을 무력화하지 않도록 Emission 기본 세기는
// 월드와 같게 둔다(아트 규격 §9.1).
Shader "Tunnel Crew/CharacterLit"
{
    Properties
    {
        [Header(Channels)]
        _MainTex("Albedo", 2D) = "white" {}
        _MaskTex("Material Mask (R metal G gloss B wet)", 2D) = "white" {}
        _NormalMap("Normal", 2D) = "bump" {}
        _EmissionMap("Emission", 2D) = "black" {}
        _AOMap("AO (white = no occlusion)", 2D) = "white" {}

        [Header(Response)]
        _NormalStrength("Normal Strength", Range(0, 3)) = 1
        _AOStrength("AO Strength", Range(0, 1)) = 1
        _EmissionColor("Emission Tint", Color) = (1,1,1,1)
        _EmissionIntensity("Emission Intensity", Range(0, 8)) = 1
        _MinLight("Min Light", Range(0, 1)) = 0.30

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
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonLitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 TCLitFragmentEntry(Varyings input) : SV_Target
            {
                return TCLitFragment(input, input.color);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "NormalsRendering"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

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
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                // 여기서는 탄젠트를 강제하지 않는다 — 스프라이트 메시의 값을 그대로 쓴다.
                Varyings o = CommonNormalsVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 TCNormalsFragment(Varyings input) : SV_Target
            {
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
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 TCUnlitFragment(Varyings input) : SV_Target
            {
                return CommonUnlitFragment(input, input.color);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
