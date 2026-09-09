using TunnelCrew.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 무드 축 하나가 지층 팔레트와 위기 단계를 전부 구동한다 (확정 2026-09-09).
    ///
    /// <b>왜 축으로 묶는가</b> — URP 세션의 day/night 오케스트레이션은 "단일 ratio 하나가
    /// 색·각도·길이·셰이더 전역을 전부 구동" 하는 구조다. 땅굴 크루는 지하라 하루 주기가 없지만
    /// 그 <b>구조</b>는 그대로 쓸 값이 있다(리서치 §B-4). 우리 축은 두 개다:
    ///
    /// <list type="bullet">
    /// <item><b>깊이</b> — 지층이 깊어질수록 안개·비네트·그레인이 오르고 색이 빠진다.
    ///       대기 프로파일과 Volume 프로파일을 함께 갈아끼운다.</item>
    /// <item><b>위기</b> — 보스·경보 구간에서 0→1 로 올라가는 값. 프로파일을 바꾸지 않고
    ///       그 위에 얹는다(안개 배율·상태 틴트). 층을 되돌리지 않고도 무드만 조인다.</item>
    /// </list>
    ///
    /// <b>프로파일 자산을 런타임에 고치지 않는다</b> — 이주 계획 §4 규약. 위기 배율은
    /// <see cref="AtmosphereDirector.FogScale"/> · <see cref="AtmosphereDirector.StateTint"/>
    /// 같은 런타임 손잡이로만 넣는다.
    ///
    /// <b>에디터 스크럽</b> — 리서치가 "Test Time / Preview Time 같은 스크럽 필드는 반드시
    /// 넣는다" 고 못 박았다. <see cref="previewDepth"/>·<see cref="previewCrisis"/> 를 인스펙터에서
    /// 움직이면 플레이 없이도 결과를 볼 수 있다.
    /// </summary>
    public sealed class StratumMoodDirector : MonoBehaviour
    {
        [Header("축")]
        [Tooltip("지층 깊이(1..). 이상지대는 Planet.StratumCount 초과.")]
        [SerializeField, Min(1)] int _depth = 1;

        [Tooltip("위기 단계 0~1. 보스 등장·경보에서 올린다. 프로파일을 바꾸지 않고 위에 얹는다.")]
        [SerializeField, Range(0f, 1f)] float _crisis;

        [Header("에디터 스크럽 (플레이 없이 확인)")]
        [Tooltip("켜면 아래 두 값이 실제 축을 덮어쓴다. 자동 캡처는 꺼 둔다.")]
        public bool previewOverride;
        [Min(1)] public int previewDepth = 1;
        [Range(0f, 1f)] public float previewCrisis;

        [Header("지층 자산 (깊이 순서대로)")]
        [Tooltip("지층 1·2·3·이상지대. BuildAtmosphereProfiles 가 만든 4종.")]
        public AtmosphereProfile[] atmosphereByStratum = new AtmosphereProfile[4];

        [Tooltip("같은 순서의 Volume 프로파일. BuildVolumeProfiles 가 만든 4종.")]
        public VolumeProfile[] volumeByStratum = new VolumeProfile[4];

        [Header("위기 반응")]
        [Tooltip("위기 1 에서 안개 배율. 1 이면 반응 없음.")]
        [Range(1f, 3f)] public float crisisFogScale = 1.5f;

        [Tooltip("위기 1 에서 얹는 상태 틴트(알파가 세기).")]
        public Color crisisTint = new Color(1f, 0.18f, 0.28f, 0.22f);

        AtmosphereDirector _atmo;
        Volume _volume;
        int _appliedStratum = -1;
        float _appliedCrisis = -1f;

        /// <summary>현재 지층 인덱스(0=지층1 … 3=이상지대). 스크럽이 켜져 있으면 그 값.</summary>
        public int StratumIndex => IndexFor(previewOverride ? previewDepth : _depth);

        public int Depth
        {
            get => _depth;
            set { _depth = Mathf.Max(1, value); Apply(); }
        }

        /// <summary>위기 0~1. 보스 등장·경보에서 올린다.</summary>
        public float Crisis
        {
            get => _crisis;
            set { _crisis = Mathf.Clamp01(value); Apply(); }
        }

        public void Bind(AtmosphereDirector atmo, Volume volume)
        {
            _atmo = atmo;
            _volume = volume;
            _appliedStratum = -1;   // 강제로 다시 적용
            Apply();
        }

        /// <summary>
        /// 깊이 → 지층 인덱스. <see cref="Planet.IsAbyss"/> 를 그대로 따른다 —
        /// Sim 이 이상지대를 판정하는 규칙이 하나여야 화면과 규칙이 어긋나지 않는다.
        /// </summary>
        public static int IndexFor(int depth)
        {
            if (depth < 1) depth = 1;
            if (Planet.IsAbyss(depth)) return 3;
            return Mathf.Clamp(depth - 1, 0, 2);
        }

        void OnEnable() => Apply();

        void OnValidate()
        {
            _depth = Mathf.Max(1, _depth);
            previewDepth = Mathf.Max(1, previewDepth);
            if (isActiveAndEnabled) Apply();
        }

        void LateUpdate()
        {
            // 스크럽 중에는 인스펙터 조작을 매 프레임 반영한다.
            if (previewOverride) Apply();
        }

        public void Apply()
        {
            int idx = StratumIndex;
            float crisis = previewOverride ? previewCrisis : _crisis;

            // 지층이 바뀔 때만 프로파일을 갈아끼운다 — 매 프레임 교체하면 Volume 이 다시 굽는다.
            if (idx != _appliedStratum)
            {
                _appliedStratum = idx;

                if (_atmo != null && atmosphereByStratum != null &&
                    idx < atmosphereByStratum.Length && atmosphereByStratum[idx] != null)
                    _atmo.Profile = atmosphereByStratum[idx];

                if (_volume != null && volumeByStratum != null &&
                    idx < volumeByStratum.Length && volumeByStratum[idx] != null)
                    _volume.sharedProfile = volumeByStratum[idx];
            }

            if (!Mathf.Approximately(crisis, _appliedCrisis))
            {
                _appliedCrisis = crisis;
                if (_atmo != null)
                {
                    _atmo.FogScale = Mathf.Lerp(1f, crisisFogScale, crisis);
                    var t = crisisTint;
                    t.a *= crisis;
                    _atmo.StateTint = t;
                }
            }
        }

        /// <summary>디버그 오버레이·랩 UI 가 읽는 한 줄 요약.</summary>
        public string Summary()
        {
            int idx = StratumIndex;
            string name = idx == 3 ? "이상지대" : $"지층 {idx + 1}";
            float crisis = previewOverride ? previewCrisis : _crisis;
            var atmo = atmosphereByStratum != null && idx < atmosphereByStratum.Length
                ? atmosphereByStratum[idx] : null;
            return $"{name}{(previewOverride ? " (스크럽)" : "")} · 위기 {crisis:0.00} · " +
                   $"대기 {(atmo != null ? atmo.name : "없음")}";
        }
    }
}
