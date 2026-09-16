// 3d-perspective-production-plan §4 4단계 — 액터·연출 빌보드 재질.
//
// 월드 표면과 같은 광원 버퍼를 쓰되, 캐릭터는 최저 조도가 높고(§7.2) 노멀을 세우지 않는다.
// 빌보드는 카메라를 마주보므로 면 노멀을 그대로 쓰면 바닥 광원에 거의 반응하지 않는다.
// 그래서 면 노멀과 "위" 를 섞은 부드러운 노멀로 램버트를 계산한다.
Shader "Tunnel Crew/PerspectiveBillboard"
{
    Properties
    {
        _MainTex("Albedo", 2D) = "white" {}
        _BaseMap("Albedo (URP alias)", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1,1,1,1)
        _Color("Tint (legacy)", Color) = (1,1,1,1)
        _MinLight("Min Light", Range(0, 1)) = 0.32
        _NormalLift("면 노멀을 위로 세우는 정도", Range(0, 1)) = 0.65
        _EmissionBoost("자체 발광(투사체·이펙트)", Range(0, 4)) = 0
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.35
        [Toggle] _Unlit("조명을 받지 않는다", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "PerspectiveBillboard"
            Tags { "LightMode" = "Universal2D" }

            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "PerspectiveLights.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BaseColor, _Color;
                float _MinLight, _NormalLift, _EmissionBoost, _Cutoff, _Unlit;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceTint)
            UNITY_INSTANCING_BUFFER_END(Props)

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 tint = (half4)_BaseColor * (half4)_Color;
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * tint;
                clip(albedo.a - _Cutoff);

                if (_Unlit > 0.5)
                    return half4(albedo.rgb * (1.0h + (half)_EmissionBoost), 1.0h);

                // 빌보드 면 노멀은 카메라를 향한다. 그대로 쓰면 바닥 광원이 거의 닿지 않으므로
                // "위" 쪽으로 세워 캐릭터가 방 조명에 자연스럽게 반응하게 한다.
                half3 n = normalize(lerp(normalize(i.normalWS), half3(0, 1, 0), (half)_NormalLift));
                half3 light = TCEvaluateLights(i.positionWS, n, (half3)_TCAmbient.rgb);
                light = max(light, (half3)_MinLight.xxx);

                half3 rgb = albedo.rgb * light + albedo.rgb * (half)_EmissionBoost;
                return half4(rgb, 1.0h);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
