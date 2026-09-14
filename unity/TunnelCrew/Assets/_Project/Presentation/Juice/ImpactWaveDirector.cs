using UnityEngine;

namespace TunnelCrew.Presentation.Juice
{
    /// <summary>
    /// 벽 파괴 충격파를 셰이더 전역값으로 올린다(<c>TunnelCrewImpact.hlsl</c>).
    ///
    /// <b>왜 트랜스폼이 아니라 셰이더인가</b> — 타일맵 청크는 메시 하나라 타일을 따로 흔들 수 없다.
    /// 청크 전체를 흔들면 이웃 청크와의 경계가 찢어지고, 스프라이트 단위로 흔들면 타일 사이가 벌어진다.
    /// 정점을 <b>월드 좌표만의 함수</b>로 밀면 맞닿은 정점끼리 같은 값을 받아 붙은 채로 출렁인다.
    ///
    /// 슬롯은 링 버퍼다. 연달아 부수면 오래된 충격이 밀려난다.
    /// 총구와 탄착은 전용 짧은 프리셋을 사용하고, 벽 파괴는 기본 지속을 유지한다.
    /// </summary>
    public sealed class ImpactWaveDirector : MonoBehaviour
    {
        public const int MaxImpacts = 8;
        public const float ShotDuration = 0.11f;
        public const float ProjectileImpactDuration = 0.15f;
        public const float ShotStrengthBoost = 1.45f;
        public const float ProjectileImpactStrengthBoost = 1.3f;

        static readonly int ImpactsId   = Shader.PropertyToID("_TCImpacts");
        static readonly int DurationsId = Shader.PropertyToID("_TCImpactDurations");
        static readonly int ParamsId    = Shader.PropertyToID("_TCImpactParams");
        static readonly int AmplitudeId = Shader.PropertyToID("_TCImpactAmplitude");

        [Header("파동")]
        [Tooltip("변위 진폭(셀). 0 이면 기능이 꺼진다.")]
        [SerializeField, Range(0f, 1.5f)] float _amplitude = 0.34f;

        [Tooltip("반경(셀). 이 거리에서 변위가 정확히 0 이 된다 — 경계 단차가 생기지 않는다.")]
        [SerializeField, Range(0.5f, 20f)] float _radiusCells = 4.25f;

        [Tooltip("지속(초). 끝에서 정확히 0.")]
        [SerializeField, Range(0.05f, 2.5f)] float _duration = 0.78f;

        [Tooltip("파동이 퍼지는 속도(셀/초).")]
        [SerializeField, Range(1f, 60f)] float _waveSpeed = 15f;

        [Tooltip("파수(rad/셀). 클수록 잔물결이 촘촘하다.")]
        [SerializeField, Range(0.5f, 12f)] float _waveNumber = 1.8f;

        readonly Vector4[] _impacts = new Vector4[MaxImpacts];
        readonly float[] _durations = new float[MaxImpacts];
        int _next;
        bool _dirty = true;

        void OnEnable() { Clear(); }
        void OnDisable() { Shader.SetGlobalFloat(AmplitudeId, 0f); }

        /// <summary>모든 충격을 지운다. 층 전환처럼 화면이 갈릴 때 부른다.</summary>
        public void Clear()
        {
            for (int i = 0; i < _impacts.Length; i++)
            {
                _impacts[i] = Vector4.zero;
                _durations[i] = 0f;
            }
            _next = 0;
            _dirty = true;
        }

        /// <summary>충격 하나를 넣는다.</summary>
        /// <param name="renderPos">렌더 월드 좌표(<c>IsometricProjection.ToRender</c> 를 거친 값).</param>
        /// <param name="strength">1 이 기준. 단단한 블록일수록 크게.</param>
        public void Add(Vector2 renderPos, float strength = 1f)
        {
            Add(renderPos, strength, _duration);
        }

        /// <summary>총구 압력용: 강한 첫 타만 보이고 곧바로 끊긴다.</summary>
        public void AddShot(Vector2 renderPos, float strength = 1f)
        {
            Add(renderPos, strength * ShotStrengthBoost, ShotDuration);
        }

        /// <summary>탄착용: 총구보다 한 박자만 더 남는 짧은 충격이다.</summary>
        public void AddProjectileImpact(Vector2 renderPos, float strength = 1f)
        {
            Add(renderPos, strength * ProjectileImpactStrengthBoost, ProjectileImpactDuration);
        }

        void Add(Vector2 renderPos, float strength, float duration)
        {
            if (_amplitude <= 0f || strength <= 0f) return;
            _impacts[_next] = new Vector4(renderPos.x, renderPos.y, Time.timeSinceLevelLoad, Mathf.Max(0f, strength));
            _durations[_next] = Mathf.Max(0.01f, duration);
            _next = (_next + 1) % MaxImpacts;
            _dirty = true;
        }

        void LateUpdate()
        {
            // 만료된 슬롯을 비운다 — 셰이더가 도는 루프를 짧게 유지한다.
            float now = Time.timeSinceLevelLoad;
            for (int i = 0; i < _impacts.Length; i++)
            {
                if (_impacts[i].w <= 0f) continue;
                float duration = _durations[i] > 0f ? _durations[i] : _duration;
                if (now - _impacts[i].z > duration)
                {
                    _impacts[i] = Vector4.zero;
                    _durations[i] = 0f;
                    _dirty = true;
                }
            }

            // 시간이 흐르는 동안은 매 프레임 올려야 한다(셰이더가 _Time 으로 나이를 센다).
            // 갱신이 없고 슬롯도 비었으면 굳이 올리지 않는다.
            if (!_dirty && !HasAny()) return;

            Shader.SetGlobalVectorArray(ImpactsId, _impacts);
            Shader.SetGlobalFloatArray(DurationsId, _durations);
            Shader.SetGlobalVector(ParamsId, new Vector4(_waveSpeed, _waveNumber, _radiusCells, _duration));
            Shader.SetGlobalFloat(AmplitudeId, _amplitude);
            _dirty = false;
        }

        bool HasAny()
        {
            for (int i = 0; i < _impacts.Length; i++) if (_impacts[i].w > 0f) return true;
            return false;
        }
    }
}
