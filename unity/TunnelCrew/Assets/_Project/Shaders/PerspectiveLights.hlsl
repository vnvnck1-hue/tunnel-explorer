#ifndef TUNNEL_CREW_PERSPECTIVE_LIGHTS_INCLUDED
#define TUNNEL_CREW_PERSPECTIVE_LIGHTS_INCLUDED

// 3d-perspective-production-plan §4 4단계 — 하이브리드 월드 조명.
//
// 원근 월드는 URP 2D 렌더러의 화면공간 라이트 텍스처를 쓸 수 없다(광원이 2D 카메라
// 기준으로 구워지기 때문). 그래서 기존 Light2D 의 *데이터* 만 버퍼로 받아
// 3D 월드 공간에서 같은 감쇠 공식을 다시 계산한다. 광원의 생성·수명·색·세기·깜빡임은
// 여전히 기존 게임 코드가 단일 출처다 — 여기서는 읽기만 한다.
//
// 좌표 계약: 본선 투영은 ReferenceTopDown 이라 렌더 XY 가 시뮬 XY 와 1:1 이다.
// 버퍼에 들어오는 좌표는 (sim.x, 높이, sim.y) 의 3D 월드 좌표다.

#define TC_MAX_LIGHTS 32

// 전역 셰이더 프로퍼티(Shader.SetGlobalVectorArray)는 이름 있는 CBUFFER 안에 두면
// 바인딩되지 않는다 — 그 경우 SetGlobalConstantBuffer 로 직접 채워야 한다.
// 여기서는 매 프레임 C# 이 SetGlobal* 로 올리므로 전역 스코프에 그대로 선언한다.
float4 _TCLightPosRange[TC_MAX_LIGHTS];   // xyz 월드 위치 · w 외곽 반지름
float4 _TCLightColor[TC_MAX_LIGHTS];      // rgb 색 · a 세기
float4 _TCLightCone[TC_MAX_LIGHTS];       // xy 바닥 평면 방향 · z cos(외곽) · w cos(내곽)
float4 _TCLightParams[TC_MAX_LIGHTS];     // x 내곽 반지름 · y 광원 높이 · z 예약 · w 예약
float4 _TCLightCount;                     // x 실제 광원 수
float4 _TCAmbient;                        // rgb 지면 대역 전역광 · a 사용 안 함
float4 _TCAmbientTop;                     // rgb 벽 윗면 전역광
float4 _TCLightTuning;                    // x 램버트 랩 · y 세기 배율 · z 최대 조도 · w 사용 안 함

/// URP 2D 포인트 라이트의 거리 감쇠를 흉내낸다 — 내곽 반지름 안은 평평, 외곽에서 0.
half TCDistanceFalloff(float distance, float innerRadius, float outerRadius)
{
    if (distance >= outerRadius) return 0.0h;
    float inner = min(innerRadius, outerRadius * 0.98);
    float t = saturate((distance - inner) / max(1e-4, outerRadius - inner));
    // URP 2D 의 기본 폴오프와 비슷한 곡선(가장자리가 부드럽게 죽는다).
    return (half)(1.0 - t * t * (3.0 - 2.0 * t));
}

/// 스팟 각도 감쇠. cosOuter 가 -1 이면 전방위다.
half TCConeFalloff(float2 toFragmentDir, float2 coneDir, float cosOuter, float cosInner)
{
    if (cosOuter <= -0.999) return 1.0h;
    float c = dot(normalize(toFragmentDir), coneDir);
    return (half)saturate((c - cosOuter) / max(1e-4, cosInner - cosOuter));
}

/// 월드 한 점의 조명량. <paramref name="normal"/> 은 월드 공간 단위 노멀.
half3 TCEvaluateLights(float3 worldPos, half3 normal, half3 ambient)
{
    half3 sum = ambient;
    int count = (int)_TCLightCount.x;
    half wrap = (half)_TCLightTuning.x;
    half gain = (half)_TCLightTuning.y;

    [loop]
    for (int i = 0; i < count; i++)
    {
        float3 lightPos = _TCLightPosRange[i].xyz;
        float outerRadius = _TCLightPosRange[i].w;
        float3 delta = lightPos - worldPos;

        // 거리는 바닥 평면 기준으로 잰다. 원본 Light2D 가 평면 위 원이므로
        // 높이 차이까지 거리에 넣으면 같은 반지름이 훨씬 좁아 보인다.
        float planar = length(delta.xz);
        half atten = TCDistanceFalloff(planar, _TCLightParams[i].x, outerRadius);
        if (atten <= 0.0h) continue;

        float2 cone = _TCLightCone[i].xy;
        atten *= TCConeFalloff(-delta.xz, cone, _TCLightCone[i].z, _TCLightCone[i].w);
        if (atten <= 0.0h) continue;

        // 높이를 가진 광원이라 벽 정면과 바닥이 서로 다른 각도를 받는다.
        half3 l = (half3)normalize(delta + float3(0, 1e-4, 0));
        half ndl = saturate(dot(normal, l) * (1.0h - wrap) + wrap);

        sum += _TCLightColor[i].rgb * (_TCLightColor[i].a * atten * ndl * gain);
    }

    return min(sum, (half3)_TCLightTuning.z);
}

#endif
