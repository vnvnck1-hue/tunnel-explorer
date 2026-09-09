// 기능명세서 §7.1 — 바닥·벽·소품용 통합 Lit 재질.
//
// URP 17.3 의 Sprite-Lit 경로를 따르면서 Emission / AO 채널과 최저 조도를 더한다.
// Tilemap 청크 메시에는 NORMAL/TANGENT 가 없어 TBN 이 0 이 되므로(모든 타일이 어두워지고
// 노멀맵이 무시됨) NormalsRendering 패스에서 탄젠트를 강제한다 — 기존
// Tunnel Crew/Tilemap-Lit-Normal 과 같은 처방이다.
//
// 캐릭터는 Tunnel Crew/CharacterLit 을 쓴다. 스프라이트는 제 탄젠트를 갖고 있고
// 최저 조도 기본값이 더 높다(§7.2 "얼굴·헬멧·무기가 환경광 속에서도 읽히도록").
Shader "Tunnel Crew/WorldLit"
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

                // Tilemap 청크 메시에는 NORMAL/TANGENT 가 없어 TBN 이 0 이 된다.
                // 스프라이트와 같은 기준으로 강제한다: 카메라를 향한 노멀 · +X 탄젠트 ·
                // 바이탄젠트 +Y (w = -1).
                input.normal  = float3(0, 0, -1);
                input.tangent = float4(1, 0, 0, -1);

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
