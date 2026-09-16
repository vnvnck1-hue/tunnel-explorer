using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 기존 <see cref="Light2D"/> 의 데이터를 원근 월드 셰이더의 광원 버퍼로 옮긴다
    /// (3d-perspective-production-plan §4 4단계 · "기존 광원 데이터를 버퍼로 전달하는 하이브리드").
    ///
    /// 광원의 생성·수명·색·세기·깜빡임·켜짐 여부는 여전히 <c>RunBootstrap</c>·<c>LightSocketRenderer</c>
    /// 가 단일 출처다. 여기서는 매 프레임 <b>읽기만</b> 한다 — 원근 월드를 꺼도 2D 조명은 그대로다.
    ///
    /// 좌표 계약: 본선 투영은 <see cref="ProjectionPreset.ReferenceTopDown"/> 이라 렌더 XY 가
    /// 시뮬 XY 와 1:1 이다. 그래도 프리셋이 바뀌어도 맞도록 <see cref="IsometricProjection.ToWorld"/>
    /// 로 되돌려 넣는다.
    /// </summary>
    public sealed class PerspectiveLightBuffer : MonoBehaviour
    {
        public const int MaxLights = 32;

        static readonly int IdPosRange = Shader.PropertyToID("_TCLightPosRange");
        static readonly int IdColor = Shader.PropertyToID("_TCLightColor");
        static readonly int IdCone = Shader.PropertyToID("_TCLightCone");
        static readonly int IdParams = Shader.PropertyToID("_TCLightParams");
        static readonly int IdCount = Shader.PropertyToID("_TCLightCount");
        static readonly int IdAmbient = Shader.PropertyToID("_TCAmbient");
        static readonly int IdAmbientTop = Shader.PropertyToID("_TCAmbientTop");
        static readonly int IdTuning = Shader.PropertyToID("_TCLightTuning");

        readonly Vector4[] _posRange = new Vector4[MaxLights];
        readonly Vector4[] _color = new Vector4[MaxLights];
        readonly Vector4[] _cone = new Vector4[MaxLights];
        readonly Vector4[] _params = new Vector4[MaxLights];
        readonly List<Light2D> _sources = new List<Light2D>(64);
        readonly List<ScoredLight> _scored = new List<ScoredLight>(64);

        struct ScoredLight { public Light2D Light; public float Score; }

        /// <summary>광원 목록을 다시 훑는 주기(초). 위치·색·세기는 매 프레임 읽는다.</summary>
        const float RescanInterval = 0.5f;
        float _nextScan;

        /// <summary>구도 중심(시뮬 좌표). 광원 예산을 넘으면 여기서 가까운 것부터 남긴다.</summary>
        public Vector2 Focus { get; set; }

        /// <summary>
        /// 광원 높이(셀). 2D 광원은 평면 위의 원이라 높이가 없다 — 3D 에서 바닥이 완전히
        /// 검어지지 않도록 분류별로 실제 설치 높이를 준다.
        /// </summary>
        public float handheldHeight = 0.62f;
        public float lampHeight = 1.15f;
        public float glowHeight = 0.35f;

        /// <summary>
        /// 램버트 랩(0 이면 순수 N·L). 2D 룩은 면이 평평해도 빛을 받으므로 기본을 높게 둔다.
        ///
        /// 아래 세 기본값은 눈대중이 아니라 실측으로 맞췄다(2026-09-16). 같은 런·같은 위치에서
        /// 2D 본선과 원근 화면을 연속 촬영해 월드 영역 평균 휘도를 비교했고,
        /// 2D 0.111 대비 원근 0.101(0.92배)까지 맞춘 값이다. 스모크: PerspectiveSmoke.
        /// </summary>
        [Range(0f, 1f)] public float lambertWrap = 0.5f;
        [Range(0f, 4f)] public float intensityGain = 1.1f;
        [Range(1f, 8f)] public float maxLight = 3f;

        /// <summary>벽 윗면 전역광 배율. 2D 의 전역광 2분할과 같은 뜻이다.</summary>
        [Range(0f, 2f)] public float topAmbientScale = 0.55f;

        /// <summary>
        /// 전역광 배율. URP 2D 는 전역광을 블렌드 스타일로 합성해 실제 체감이 raw intensity 보다 밝다.
        /// 원근 경로에서 2D 와 같은 "광원 밖 암부의 읽힘" 을 내려면 이 값으로 맞춘다.
        /// </summary>
        [Range(0f, 8f)] public float ambientGain = 3.6f;

        void LateUpdate() => Push();

        /// <summary>이번 프레임의 광원 상태를 전역 셰이더 프로퍼티로 올린다.</summary>
        public void Push()
        {
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + RescanInterval;
                Rescan();
            }

            Color ambient = Color.black;
            _scored.Clear();

            for (int i = 0; i < _sources.Count; i++)
            {
                var light = _sources[i];
                if (light == null || !light.isActiveAndEnabled || light.intensity <= 0.001f) continue;

                if (light.lightType == Light2D.LightType.Global)
                {
                    // 전역광은 개수만큼 더한다. 2D 는 레이어로 나누지만 여기서는 지면 대역을 기준으로 삼고
                    // 벽 윗면은 배율로 낮춘다(아래 _TCAmbientTop).
                    ambient += light.color * light.intensity;
                    continue;
                }
                if (light.lightType != Light2D.LightType.Point) continue;

                var sim = SimPositionOf(light);
                _scored.Add(new ScoredLight { Light = light, Score = (sim - Focus).sqrMagnitude });
            }

            _scored.Sort((a, b) => a.Score.CompareTo(b.Score));

            int count = Mathf.Min(_scored.Count, MaxLights);
            for (int i = 0; i < count; i++)
            {
                var light = _scored[i].Light;
                var sim = SimPositionOf(light);
                float height = HeightFor(light);

                _posRange[i] = new Vector4(sim.x, height, sim.y, Mathf.Max(0.05f, light.pointLightOuterRadius));
                var c = light.color;
                _color[i] = new Vector4(c.r, c.g, c.b, light.intensity);

                float outerAngle = light.pointLightOuterAngle;
                if (outerAngle >= 359f)
                {
                    _cone[i] = new Vector4(0f, 1f, -1f, 1f);
                }
                else
                {
                    // Light2D 스팟은 트랜스폼의 +Y(up)가 조사 방향이다. 렌더 방향을 시뮬 방향으로 되돌린다.
                    var renderUp = (Vector2)light.transform.up;
                    var dir = IsometricProjection.ToWorld(renderUp);
                    if (dir.sqrMagnitude < 1e-6f) dir = Vector2.up;
                    dir.Normalize();
                    float cosOuter = Mathf.Cos(outerAngle * 0.5f * Mathf.Deg2Rad);
                    float cosInner = Mathf.Cos(Mathf.Min(outerAngle, light.pointLightInnerAngle) * 0.5f * Mathf.Deg2Rad);
                    if (cosInner <= cosOuter + 1e-4f) cosInner = cosOuter + 1e-4f;
                    _cone[i] = new Vector4(dir.x, dir.y, cosOuter, cosInner);
                }

                _params[i] = new Vector4(Mathf.Max(0f, light.pointLightInnerRadius), height, 0f, 0f);
            }

            for (int i = count; i < MaxLights; i++)
            {
                _posRange[i] = Vector4.zero;
                _color[i] = Vector4.zero;
                _cone[i] = new Vector4(0f, 1f, -1f, 1f);
                _params[i] = Vector4.zero;
            }

            Shader.SetGlobalVectorArray(IdPosRange, _posRange);
            Shader.SetGlobalVectorArray(IdColor, _color);
            Shader.SetGlobalVectorArray(IdCone, _cone);
            Shader.SetGlobalVectorArray(IdParams, _params);
            Shader.SetGlobalVector(IdCount, new Vector4(count, 0f, 0f, 0f));
            ambient *= ambientGain;
            Shader.SetGlobalVector(IdAmbient, new Vector4(ambient.r, ambient.g, ambient.b, 1f));
            Shader.SetGlobalVector(IdAmbientTop,
                new Vector4(ambient.r * topAmbientScale, ambient.g * topAmbientScale, ambient.b * topAmbientScale, 1f));
            Shader.SetGlobalVector(IdTuning, new Vector4(lambertWrap, intensityGain, maxLight, 0f));
        }

        static Vector2 SimPositionOf(Light2D light)
        {
            var p = light.transform.position;
            return IsometricProjection.ToWorld(new Vector2(p.x, p.y));
        }

        /// <summary>
        /// 광원 설치 높이. <see cref="LightSocket"/> 이 분류를 들고 있으면 그것을 따르고,
        /// 없으면 반지름으로 손에 든 것과 고정 조명을 가른다.
        /// </summary>
        float HeightFor(Light2D light)
        {
            if (light.TryGetComponent<LightSocket>(out var socket))
            {
                if (socket.mountHeightCells > 0.01f) return socket.mountHeightCells;
                switch (socket.lightClass)
                {
                    case LightClass.Worklamp: return lampHeight;
                    case LightClass.MineralGlow: return glowHeight;
                    case LightClass.Indicator: return glowHeight;
                    default: return handheldHeight;
                }
            }
            return light.pointLightOuterRadius > 6f ? lampHeight : handheldHeight;
        }

        void Rescan()
        {
            _sources.Clear();
            var found = FindObjectsByType<Light2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++) _sources.Add(found[i]);
        }
    }
}
