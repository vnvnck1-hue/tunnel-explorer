using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 분류가 정해진 광원 하나(기능명세서 §7.3). <see cref="LightSocketRenderer"/> 가
    /// 매 프레임 세기와 그림자를 정한다.
    /// </summary>
    /// <remarks><c>ExecuteAlways</c> — 등록만 하는 컴포넌트다. 자세한 이유는
    /// <see cref="VisualHeightAnchor"/> 의 같은 주석을 볼 것.</remarks>
    [ExecuteAlways]
    [RequireComponent(typeof(Light2D))]
    public sealed class LightSocket : MonoBehaviour
    {
        public LightClass lightClass = LightClass.Worklamp;

        [Tooltip("아트가 준 기준 세기. 깜빡임은 이 값에 곱한다.")]
        public float baseIntensity = 1f;

        [Tooltip("셀 단위 외곽 반지름. 그림자 우선순위 계산에 쓴다.")]
        public float rangeCells = 1f;

        [Tooltip("깜빡임 위상. 같은 종류의 램프가 한꺼번에 흔들리지 않게 한다.")]
        public float phase;

        Light2D _light;
        public Light2D Light => _light != null ? _light : _light = GetComponent<Light2D>();

        /// <summary>이번 프레임에 그림자를 켰는가. 디버그 오버레이가 읽는다.</summary>
        public bool ShadowOn { get; internal set; }

        void OnEnable() => LightSocketRenderer.Register(this);
        void OnDisable() => LightSocketRenderer.Unregister(this);
    }

    /// <summary>
    /// 기능명세서 §7.3 광원 분류 적용 + §13 그림자 광원 예산 집행.
    ///
    /// <b>왜 매니저인가</b> — 그림자 예산은 <b>전역</b> 결정이다. 광원 하나가 자기만 보고
    /// "나는 중요하니 그림자를 켜겠다" 고 하면 방에 램프가 다섯 개일 때 예산이 무너진다.
    /// 매 프레임 전체를 우선순위로 줄 세워 위에서 예산만큼 켠다.
    ///
    /// 예산을 넘은 광원은 <b>빛은 그대로 내고 그림자만 끈다.</b> 광원을 끄면 방이 어두워져
    /// 가독성이 무너지지만, 그림자는 없어도 형태가 읽힌다(§15.1).
    ///
    /// 깜빡임은 <see cref="FreezeFlicker"/> 로 고정할 수 있다 — 대기 원근의 그레인과 같은
    /// 이유다. 자동 캡처(§16.3)가 프레임마다 달라지면 회귀 비교가 불가능해진다.
    /// </summary>
    [DefaultExecutionOrder(120)]   // 실루엣(115) 다음. 정렬·페이드가 끝난 뒤 조명을 정한다
    public sealed class LightSocketRenderer : MonoBehaviour
    {
        static readonly List<LightSocket> Sockets = new List<LightSocket>(32);

        [SerializeField] VisualQualityTier _tier = VisualQualityTier.High;

        [Tooltip("광과민 옵션 — 깜빡임을 완전히 끈다(§10.3).")]
        [SerializeField] bool _reducePhotosensitivity;

        [Tooltip("깜빡임을 시간에 고정한다. 자동 캡처는 항상 고정 상태로 찍는다(§16.3).")]
        [SerializeField] bool _freezeFlicker;

        readonly List<LightSocket> _ordered = new List<LightSocket>(32);

        // Light2D.normalMapQuality 는 읽기 전용 프로퍼티다. 값을 넣으려면 백킹 필드를
        // 써야 한다 — ShadowCaster2D 형상과 같은 사정이다(구현 기록 §5). 실패하면 한 번만
        // 경고하고 노멀 없이 계속 그린다.
        static System.Reflection.FieldInfo _useNormalField;
        static System.Reflection.FieldInfo _normalQualityField;
        static bool _normalReflectionResolved;
        static bool _warnedNormalReflection;

        /// <summary>품질 단계(§13). 그림자 광원 예산이 여기서 나온다.</summary>
        public VisualQualityTier Tier
        {
            get => _tier;
            set { _tier = value; Apply(0f); }
        }

        /// <summary>광과민 옵션(§10.3). 켜면 깜빡임이 0 이 된다.</summary>
        public bool ReducePhotosensitivity
        {
            get => _reducePhotosensitivity;
            set { _reducePhotosensitivity = value; Apply(0f); }
        }

        public bool FreezeFlicker
        {
            get => _freezeFlicker;
            set { _freezeFlicker = value; Apply(0f); }
        }

        /// <summary>이번 프레임에 그림자를 켠 광원 수. 디버그 오버레이가 읽는다.</summary>
        public int ShadowCount { get; private set; }

        /// <summary>등록된 광원 수.</summary>
        public static int SocketCount => Sockets.Count;

        /// <summary>등록된 광원 전체. 디버그 오버레이와 테스트가 읽는다.</summary>
        public static IReadOnlyList<LightSocket> All => Sockets;

        public static void Register(LightSocket s)
        {
            if (s != null && !Sockets.Contains(s)) Sockets.Add(s);
        }

        public static void Unregister(LightSocket s)
        {
            if (s != null) Sockets.Remove(s);
        }

        static void ResolveNormalFields()
        {
            if (_normalReflectionResolved) return;
            _normalReflectionResolved = true;
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var t = typeof(Light2D);
            _useNormalField = t.GetField("m_UseNormalMap", flags);
            _normalQualityField = t.GetField("m_NormalMapQuality", flags);
        }

        /// <summary>
        /// 분류가 요구하는 노멀 품질을 광원에 넣는다.
        ///
        /// URP 기본값이 <c>Disabled</c> 라서 이걸 하지 않으면 아트의 노멀맵과
        /// <c>NormalsRendering</c> 패스가 전부 무시된다 —
        /// <see cref="LightClassRules.NormalQuality"/> 의 주석을 볼 것.
        /// </summary>
        void ApplyNormalQuality(Light2D light, LightClass c)
        {
            if (light.lightType == Light2D.LightType.Global) return;   // 전역광은 노멀을 쓰지 않는다

            ResolveNormalFields();
            if (_useNormalField == null || _normalQualityField == null)
            {
                if (!_warnedNormalReflection)
                {
                    _warnedNormalReflection = true;
                    Debug.LogWarning("[§7.3] Light2D 의 노멀맵 필드를 찾지 못했다 — " +
                                     "조명이 노멀맵을 읽지 않는다. URP 버전이 바뀌었는지 확인할 것.");
                }
                return;
            }

            var want = LightClassRules.NormalQuality(c, _tier);
            bool use = want != LightNormalQuality.Disabled;
            // 열거값 이름이 URP 쪽과 같다(Fast/Accurate/Disabled).
            object quality = System.Enum.Parse(_normalQualityField.FieldType, want.ToString());

            if (!Equals(_useNormalField.GetValue(light), use)) _useNormalField.SetValue(light, use);
            if (!Equals(_normalQualityField.GetValue(light), quality))
                _normalQualityField.SetValue(light, quality);
        }

        void LateUpdate() => Apply(_freezeFlicker ? 0f : Time.unscaledTime);

        /// <summary>
        /// 분류별 거동을 적용한다. Visual Lab 캡처는 플레이 모드 없이 이걸 직접 부른다.
        /// <paramref name="time"/> 이 0 이면 깜빡임이 멈춘다.
        /// </summary>
        public void Apply(float time)
        {
            // 1패스: 살아 있는 광원만 모으고 세기를 정한다.
            _ordered.Clear();
            for (int i = Sockets.Count - 1; i >= 0; i--)
            {
                var s = Sockets[i];
                if (s == null) { Sockets.RemoveAt(i); continue; }
                var light = s.Light;
                if (light == null) continue;

                float k = LightClassRules.FlickerAt(s.lightClass, time, s.phase,
                    VisualQualityRules.FlashScale(_reducePhotosensitivity));
                light.intensity = Mathf.Max(0f, s.baseIntensity * k);
                ApplyNormalQuality(light, s.lightClass);

                s.ShadowOn = false;
                _ordered.Add(s);
            }

            // 2패스: 그림자 우선순위로 줄 세운다. 같은 값이면 등록 순서를 지켜
            // 프레임마다 결과가 흔들리지 않게 한다(정렬 안정성).
            _ordered.Sort((a, b) =>
            {
                float pa = LightClassRules.ShadowPriority(a.lightClass, a.rangeCells, a.baseIntensity);
                float pb = LightClassRules.ShadowPriority(b.lightClass, b.rangeCells, b.baseIntensity);
                int cmp = pb.CompareTo(pa);
                return cmp != 0 ? cmp : Sockets.IndexOf(a).CompareTo(Sockets.IndexOf(b));
            });

            // 3패스: 예산만큼 그림자를 켠다. 나머지는 빛만 낸다.
            int budget = LightClassRules.ShadowBudget(_tier);
            int used = 0;
            for (int i = 0; i < _ordered.Count; i++)
            {
                var s = _ordered[i];
                bool on = used < budget && LightClassRules.WantsShadow(s.lightClass);
                if (on) used++;

                s.ShadowOn = on;
                s.Light.shadowIntensity = on ? LightClassRules.ShadowIntensity(s.lightClass) : 0f;
            }

            ShadowCount = used;
        }
    }
}
