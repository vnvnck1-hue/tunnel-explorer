#ifndef TUNNEL_CREW_IMPACT_INCLUDED
#define TUNNEL_CREW_IMPACT_INCLUDED

// 벽 파괴 충격파 — 정점을 <b>월드 좌표의 연속 함수</b>로 민다.
//
// 왜 정점인가: 타일맵 청크는 메시 하나다. 타일을 각자 트랜스폼으로 흔들면 인접 타일 사이가
// 벌어져 이음새가 찢어진다. 반면 변위를 월드 좌표만의 함수로 두면, 맞닿은 두 타일의 정점은
// 같은 월드 좌표에 있으므로 <b>항상 같은 값</b>을 받는다 — 붙은 채로 함께 출렁인다.
//
// 공간 감쇠는 반경에서 정확히 0 이 되어야 한다. 0 이 아니면 그 경계에 눈에 보이는 단차가 생긴다.

#define TC_IMPACT_MAX 8

// xy = 충격 중심(월드) · z = 시작 시각(_Time.y 기준) · w = 세기. w <= 0 이면 빈 슬롯.
float4 _TCImpacts[TC_IMPACT_MAX];
// 슬롯별 지속 시간. 총구/탄착은 짧고 벽 파괴는 기존 지속을 유지한다.
float _TCImpactDurations[TC_IMPACT_MAX];
// x = 파동 속도(셀/초) · y = 파수(rad/셀) · z = 반경(셀) · w = 지속(초)
float4 _TCImpactParams;
// 전체 진폭(셀). 0 이면 기능 자체가 꺼진다 — 아무도 값을 넣지 않은 씬의 기본값이다.
float  _TCImpactAmplitude;

float2 TCImpactOffsetWS(float2 wp)
{
    float2 sum = float2(0.0, 0.0);
    if (_TCImpactAmplitude <= 0.0) return sum;

    float radius   = max(0.0001, _TCImpactParams.z);
    float defaultDuration = max(0.0001, _TCImpactParams.w);

    [unroll]
    for (int i = 0; i < TC_IMPACT_MAX; i++)
    {
        float4 im = _TCImpacts[i];
        if (im.w <= 0.0) continue;

        float duration = _TCImpactDurations[i] > 0.0 ? _TCImpactDurations[i] : defaultDuration;
        float age = _Time.y - im.z;
        if (age < 0.0 || age > duration) continue;

        float2 d = wp - im.xy;
        float dist = length(d);
        if (dist > radius) continue;

        // 공간 감쇠 — 반경에서 정확히 0.
        float sp = 1.0 - (dist / radius);
        sp = sp * sp;

        // 시간 감쇠 — 끝에서 정확히 0. 시작은 1 에서 출발해 바로 흔들린다.
        float tw = 1.0 - (age / duration);
        tw = tw * tw;

        // 중심에서는 방향이 정의되지 않으므로 살짝 램프를 걸어 특이점을 없앤다.
        float2 dir = dist > 1e-4 ? (d / dist) : float2(0.0, 1.0);
        float core = saturate(dist / 0.35);

        // 바깥으로 퍼지는 파동: 거리만큼 위상이 밀리고 시간만큼 앞선다.
        float phase = dist * _TCImpactParams.y - age * _TCImpactParams.x;

        sum += dir * (sin(phase) * sp * tw * core * im.w);
    }
    return sum * _TCImpactAmplitude;
}

// 오브젝트 공간 정점에 충격 변위를 얹는다. 월드를 거쳐 돌아오므로 어떤 트랜스폼에서도 맞다.
float3 TCApplyImpact(float3 positionOS, float wobble)
{
    if (wobble <= 0.0001 || _TCImpactAmplitude <= 0.0) return positionOS;

    float3 wp = TransformObjectToWorld(positionOS);
    float2 off = TCImpactOffsetWS(wp.xy) * wobble;
    positionOS = TransformWorldToObject(wp + float3(off, 0.0));
    return positionOS;
}

#endif
