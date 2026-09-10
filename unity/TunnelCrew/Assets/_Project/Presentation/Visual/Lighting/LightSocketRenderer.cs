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

        /// <summary>
        /// 광원의 설치 높이(셀). 0 = 바닥·낮은 소품. 벽 lift(<see cref="SurfaceRules.MinLiftCells"/>,
        /// 0.75셀) 이상이면 벽 윗면을 비춘다 — 천장·기둥 상단·높이 매단 램프.
        /// 조명 소팅 정밀화(2026-09-10): 이 값 하나가 "어느 레이어를 비추는가"를 정한다.
        /// </summary>
        [Tooltip("설치 높이(셀). 0.75 이상이면 벽 윗면·전경 cap 까지 비춘다.")]
        public float mountHeightCells = 0f;

        /// <summary>
        /// 노멀맵 조명이 보는 광원 높이(셀) 오버라이드. 0 이하 = 공용값(<see cref="LightSocketRenderer.NormalMapHeightCells"/>).
        /// 사용자 결정(2026-09-10): 손전등·헤드램프만 1.0 — 플레이어가 벽 베벨을 마주볼 때 요철이 선다. 램프·크루·보스는 공용값.
        /// </summary>
        [Tooltip("노멀맵 광원 높이(셀) 오버라이드. 0 이하 = 공용값 2.")]
        public float normalMapHeightCells = -1f;

        /// <summary>벽 윗면(WallTop)·전경 cap(FrontStructure)을 비추는가.</summary>
        public bool LightsWallTops => mountHeightCells >= SurfaceRules.MinLiftCells;

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
        static System.Reflection.FieldInfo _normalDistanceField;
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
            _normalDistanceField = t.GetField("m_NormalMapDistance", flags);
        }

        /// <summary>
        /// 광원이 §7.1 의 월드 레이어 전부를 비추게 한다.
        ///
        /// URP 는 대상 레이어를 광원에 직렬화한다. 그래서 씬을 만든 뒤에 Sorting Layer 를
        /// 추가하면 그 레이어는 어느 광원에도 들어가지 않는다 — 실제로 <c>WorldEntity</c> 와
        /// <c>FrontStructure</c> 가 빠져 있어 오브젝트·전경이 빛을 받지 못했다(2026-09-09).
        /// 씬 데이터에 의존하지 않도록 코드에서 매번 채운다.
        ///
        /// 전역광은 URP 가 레이어별로 따로 관리하므로(주석: "If we need to update this at
        /// runtime make sure we add code to update global lights") 값이 실제로 달라질 때만 쓴다.
        /// </summary>
        internal static void ApplyLitLayers(Light2D light, bool lightsWallTops = true)
        {
            if (light == null) return;
            // 지면 높이 광원은 벽 윗면(WallTop)·전경 cap(FrontStructure)을 비추지 않는다 —
            // 비추면 벽에 높이가 없다는 뜻이 된다(2026-09-10, 조명 소팅 정밀화). 윗면은 전역광과
            // 높이 있는 광원(LightSocket.mountHeightCells ≥ 벽 lift)의 몫이다.
            var want = lightsWallTops ? VisualLayers.LitLayerIds() : VisualLayers.LitGroundLevelLayerIds();
            if (want.Length == 0) return;

            var have = light.targetSortingLayers;
            if (have != null && have.Length == want.Length)
            {
                bool same = true;
                for (int i = 0; i < want.Length; i++)
                {
                    bool found = false;
                    for (int j = 0; j < have.Length; j++) if (have[j] == want[i]) { found = true; break; }
                    if (!found) { same = false; break; }
                }
                if (same) return;
            }
            light.targetSortingLayers = want;
        }

        /// <summary>
        /// 분류가 요구하는 노멀 품질을 광원에 넣는다.
        ///
        /// URP 기본값이 <c>Disabled</c> 라서 이걸 하지 않으면 아트의 노멀맵과
        /// <c>NormalsRendering</c> 패스가 전부 무시된다 —
        /// <see cref="LightClassRules.NormalQuality"/> 의 주석을 볼 것.
        /// </summary>
        void ApplyNormalQuality(Light2D light, LightClass c, float heightOverride = -1f)
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

            // 광원 높이(칸). 이 값이 노멀 반응의 세기를 정한다 — 크면 빛이 거의 정면에서
            // 오는 것이 되어 요철이 사라진다. 씬 직렬화 기본값 3 이 "노멀이 안 보인다" 의
            // 원인이었다. 본선(RunBootstrap.UseNormalMaps)이 쓰는 0.8 과 같게 맞춘다.
            float height = heightOverride > 0f ? heightOverride : NormalMapHeightCells;
            if (use && _normalDistanceField != null &&
                !Equals(_normalDistanceField.GetValue(light), height))
                _normalDistanceField.SetValue(light, height);
        }

        /// <summary>노멀 조명이 보는 광원 높이(칸). 낮을수록 요철이 강하게 선다.</summary>
        /// <summary>
        /// 노멀맵 조명이 쓰는 광원 높이(셀). URP 2D 는 이 값으로 빛의 입사각을 만든다 —
        /// 평평한 면의 밝기가 <c>높이 / 광원까지의 거리</c> 에 비례한다.
        ///
        /// <b>낮추면 바닥이 죽는다.</b> 2026-09-09 에 요철을 세우려고 0.8 로 내렸더니, 바닥처럼
        /// 노멀이 평평한 면은 2 셀만 떨어져도 밝기가 0.37 배로 주저앉았다. 반면 옆면 노멀을 가진
        /// 기둥·상자는 그대로 밝아서 "오브젝트만 빛을 받고 바닥은 안 받는" 화면이 됐다.
        /// 같은 공간에 있는 것들이 같은 빛을 받아야 하므로 다시 올린다. 요철은 광원 높이가 아니라
        /// 머티리얼의 <c>_NormalStrength</c>(현재 1.6) 로 세운다.
        /// </summary>
        internal const float NormalMapHeightCells = 2f;

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
                ApplyNormalQuality(light, s.lightClass, s.normalMapHeightCells);
                ApplyLitLayers(light, s.LightsWallTops);

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
                // 반그림자 폭도 분류가 정한다 — 랜턴은 넓고 손전등은 좁다(2026-09-10, "그림자가 너무 딱딱하다").
                s.Light.shadowSoftness = LightClassRules.ShadowSoftness(s.lightClass);
            }

            ShadowCount = used;
        }
    }
}
