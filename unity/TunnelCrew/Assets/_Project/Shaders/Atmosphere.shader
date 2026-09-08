// 기능명세서 §7.5 — 대기 원근 패스.
//
// 왜 RendererFeature 가 아니라 화면 쿼드인가:
//   §7.5 는 "AtmosphereRendererFeature 또는 동등한 전체화면 패스" 라고 열어 두었고,
//   §6.4 는 이미 VisionAndGrade 레이어에 "LOS 어둠, 안개, 깊이 색보정" 을 배정해 두었다.
//   Renderer2D.asset 에 기능을 추가하면 본선 렌더 파이프라인이 바뀌는데, 지금 배치는
//   "실제 게임 씬의 최종 조명과 후처리 수치를 확정하지 말 것" 이 걸려 있다.
//   같은 레이어의 쿼드로 만들면 파이프라인 자산에 손대지 않고 Visual Lab 에서만 켤 수 있다.
//   DarknessOverlay 가 쓰는 방식과 같다.
//
// 합성은 프리멀티플라이드 알파(Blend One OneMinusSrcAlpha)다. 그래야 그레인을
// 알파와 무관한 순수 가산항으로 얹을 수 있다. 스트레이트 알파로는 "어둡게" 밖에 못 한다.
Shader "TunnelCrew/Atmosphere"
{
    Properties
    {
        _Enable        ("전체 세기", Range(0,1)) = 1

        _FogColor      ("안개 색", Color) = (0.18, 0.20, 0.30, 1)
        _FogDensity    ("안개 농도", Range(0,1)) = 0.22
        // x = 닿는 화면 높이(0 아래 ~ 1 위), y = 경계 부드러움
        _FogShape      ("안개 모양", Vector) = (0.42, 0.45, 0, 0)

        _NearTint      ("가까운 층 색", Color) = (0.24, 0.16, 0.26, 1)
        _FarTint       ("먼 층 색", Color) = (0.14, 0.20, 0.30, 1)
        _DepthSeparation ("깊이 분리", Range(0,0.5)) = 0.12

        _VignetteColor ("가장자리 색", Color) = (0.02, 0.02, 0.04, 1)
        // x = inner, y = outer, z = strength
        _VignetteShape ("가장자리 모양", Vector) = (0.55, 1.15, 0.35, 0)

        _StateTint     ("상태 색조 (a = 세기)", Color) = (0, 0, 0, 0)

        _GrainStrength ("그레인 세기", Range(0,0.2)) = 0.03
        _GrainDensity  ("그레인 밀도(기준 화면 높이당)", Float) = 540
        _GrainTime     ("그레인 시간(0 = 정지)", Float) = 0

        // 0 합성 / 1 안개 / 2 깊이 / 3 가장자리 / 4 상태 / 5 그레인
        _Isolate       ("디버그 격리", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend One OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "Atmosphere"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            CBUFFER_START(UnityPerMaterial)
                float  _Enable;
                float4 _FogColor;
                float  _FogDensity;
                float4 _FogShape;
                float4 _NearTint;
                float4 _FarTint;
                float  _DepthSeparation;
                float4 _VignetteColor;
                float4 _VignetteShape;
                float4 _StateTint;
                float  _GrainStrength;
                float  _GrainDensity;
                float  _GrainTime;
                float  _Isolate;
            CBUFFER_END

            Varyings vert (Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            // 스크린 픽셀 격자 기반 해시. 해상도가 바뀌어도 밀도를 화면 비율로 세므로
            // 그레인 점의 체감 크기가 유지된다(§13 예산: 추가 렌더 타깃 없음).
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // 격리 모드에서 해당 층만 남긴다. 0 이면 전부 통과.
            float LayerGate(float layer)
            {
                return (_Isolate < 0.5 || abs(_Isolate - layer) < 0.5) ? 1.0 : 0.0;
            }

            half4 frag (Varyings input) : SV_Target
            {
                // 쿼드는 카메라보다 크게 잡혀 있으므로 쿼드 UV 가 아니라 실제 스크린 UV 를 쓴다.
                float2 uv = input.positionCS.xy / _ScreenParams.xy;
                float aspect = _ScreenParams.x / max(1.0, _ScreenParams.y);

                float3 accum = 0;
                float  cover = 0;

                // ── 깊이 그룹 색 분리 — 화면 중앙은 건드리지 않고 위아래만 갈라 놓는다.
                {
                    float t = smoothstep(0.0, 1.0, uv.y);
                    float3 tint = lerp(_NearTint.rgb, _FarTint.rgb, t);
                    float a = _DepthSeparation * abs(uv.y - 0.5) * 2.0 * LayerGate(2);
                    accum = lerp(accum, tint, a);
                    cover = lerp(cover, 1.0, a);
                }

                // ── 저층 안개 — 화면 아래(카메라에 가까운 층)에서 가장 진하다.
                {
                    float half_ = max(0.005, _FogShape.y) * 0.5;
                    float mask = 1.0 - smoothstep(_FogShape.x - half_, _FogShape.x + half_, uv.y);
                    float a = saturate(_FogDensity * mask) * LayerGate(1);
                    accum = lerp(accum, _FogColor.rgb, a);
                    cover = lerp(cover, 1.0, a);
                }

                // ── 상태 색조 — 경보·과열 같은 전역 상태. 알파에 세기를 담는다.
                {
                    float a = saturate(_StateTint.a) * LayerGate(4);
                    accum = lerp(accum, _StateTint.rgb, a);
                    cover = lerp(cover, 1.0, a);
                }

                // ── 화면 가장자리 암부 — 종횡비를 보정해 원형으로 유지한다.
                float vig = 0;
                {
                    float2 d = (uv - 0.5) * 2.0;
                    d.x *= aspect;
                    float r = length(d) / max(1.0, aspect);
                    vig = smoothstep(_VignetteShape.x, _VignetteShape.y, r)
                        * saturate(_VignetteShape.z) * LayerGate(3);
                    accum = lerp(accum, _VignetteColor.rgb, vig);
                    cover = lerp(cover, 1.0, vig);
                }

                // 여기까지는 스트레이트 알파. 한 번에 프리멀티플라이드로 바꾼다.
                float3 rgb = accum * cover;

                // ── 필름 그레인 — 알파를 건드리지 않는 가산항.
                // 어두운 화면의 밴딩을 깨는 게 목적이라 양방향이어야 한다.
                {
                    float cell = max(1.0, _GrainDensity) / max(1.0, _ScreenParams.y);
                    float2 g = floor(uv * _ScreenParams.xy * cell);
                    float n = Hash21(g + _GrainTime * 37.0) - 0.5;
                    // 암부가 일부러 눌러 놓은 영역에는 그레인을 얹지 않는다. 색 버퍼를 읽을 수
                    // 없으니 실제 휘도는 모르지만, 검정으로 크러시한 곳에 디더를 뿌리면
                    // 프레임 테두리가 지글거린다 — 실제 캡처에서 그렇게 나왔다.
                    // 격리 모드에서는 vig 가 0 이라 그레인이 온전히 보인다.
                    rgb += n * _GrainStrength * (1.0 - saturate(vig)) * LayerGate(5);
                }

                float e = saturate(_Enable);
                return half4(rgb * e, cover * e);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
