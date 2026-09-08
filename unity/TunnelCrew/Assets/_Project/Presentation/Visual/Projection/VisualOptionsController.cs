using UnityEngine;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 기능명세서 §13 품질 단계 + §10.3 접근성 옵션을 한곳에서 적용한다(§14 단계 F).
    ///
    /// <b>왜 컨트롤러가 필요한가</b> — 지금까지 각 시스템이 자기 스위치를 들고 있었다
    /// (그림자 예산, 안개 배율, 그레인, 깜빡임, 노멀 세기). 옵션 화면에서 "품질: 낮음" 을
    /// 고르면 그 다섯 곳이 <b>일관되게</b> 움직여야 하고, 사람이 하나씩 챙기면 반드시
    /// 하나를 빠뜨린다. 여기가 그 단일 지점이다.
    ///
    /// <b>가독성 필수 기능은 건드리지 않는다.</b> §13 — "가독성에 필요한 벽 정면,
    /// 전경 가림, 접촉 AO는 품질 단계에서 제거하지 않는다". 이 컨트롤러는 그 세 가지와
    /// 가려진 캐릭터 실루엣을 <b>끄는 코드를 아예 갖고 있지 않다.</b> 끄고 싶어도 못 끈다.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class VisualOptionsController : MonoBehaviour
    {
        [Header("품질 단계 (§13)")]
        [SerializeField] VisualQualityTier _tier = VisualQualityTier.High;

        [Header("접근성 (§10.3)")]
        [Tooltip("멀미 옵션 — 카메라 흔들림을 끈다.")]
        [SerializeField] bool _reduceMotion;

        [Tooltip("광과민 옵션 — 깜빡임·플래시·그레인을 끈다.")]
        [SerializeField] bool _reducePhotosensitivity;

        [Tooltip("전경 투명도 보정. 0 이면 프로파일 값을 그대로 쓴다(§10.3 전경 투명도 옵션).")]
        [SerializeField, Range(0f, 1f)] float _foregroundAlphaOverride;

        [Header("연결")]
        [SerializeField] LightSocketRenderer _lights;
        [SerializeField] AtmosphereDirector _atmosphere;

        static readonly int NormalReduceId = Shader.PropertyToID("_TCNormalReduce");

        public VisualQualityTier Tier
        {
            get => _tier;
            set { _tier = value; Apply(); }
        }

        public bool ReduceMotion
        {
            get => _reduceMotion;
            set { _reduceMotion = value; Apply(); }
        }

        public bool ReducePhotosensitivity
        {
            get => _reducePhotosensitivity;
            set { _reducePhotosensitivity = value; Apply(); }
        }

        /// <summary>0 이면 프로파일 값을 쓴다. 값이 있으면 전경 목표 알파를 덮는다.</summary>
        public float ForegroundAlphaOverride
        {
            get => _foregroundAlphaOverride;
            set { _foregroundAlphaOverride = Mathf.Clamp01(value); Apply(); }
        }

        /// <summary>카메라 흔들림 배율(§10.3). 카메라 코드가 곱해 쓴다.</summary>
        public float ShakeScale => VisualQualityRules.ShakeScale(_reduceMotion);

        /// <summary>다음 단계로 돌린다(디버그 키 하나로 순환).</summary>
        public VisualQualityTier CycleTier()
        {
            _tier = (VisualQualityTier)(((int)_tier + 1) % 4);
            Apply();
            return _tier;
        }

        public void Bind(LightSocketRenderer lights, AtmosphereDirector atmosphere)
        {
            _lights = lights;
            _atmosphere = atmosphere;
            Apply();
        }

        void OnEnable() => Apply();

        /// <summary>단계와 옵션을 연결된 시스템에 밀어 넣는다.</summary>
        public void Apply()
        {
            float flash = VisualQualityRules.FlashScale(_reducePhotosensitivity);

            // 노멀 세기 — 전역 감소량으로 넣는다. 이 전역을 설정하지 않는 씬에서
            // 노멀이 사라지지 않도록 셰이더는 "감소량"(기본 0)으로 받는다.
            Shader.SetGlobalFloat(NormalReduceId,
                1f - VisualQualityRules.NormalStrengthScale(_tier));

            if (_lights != null)
            {
                _lights.Tier = _tier;
                _lights.ReducePhotosensitivity = _reducePhotosensitivity;
            }

            if (_atmosphere != null)
            {
                _atmosphere.FogScale = VisualQualityRules.FogScale(_tier);
                _atmosphere.FlashScale = flash;
            }

            // 전경 투명도는 목표 알파의 하한만 올린다 — 페이드 자체를 끄지 않는다.
            // §13 이 전경 가림을 가독성 필수로 두었으므로 "전경을 불투명하게 고정" 은
            // 이 컨트롤러가 제공하지 않는다. 더 잘 보이게 하는 방향만 허용한다.
            if (_foregroundAlphaOverride > 0f)
                ForegroundFadeController.AlphaOverride = _foregroundAlphaOverride;
            else
                ForegroundFadeController.AlphaOverride = 0f;
        }

        /// <summary>Visual Lab 캡처가 부른다. 시간이 흐르지 않는 경로용.</summary>
        public void EditorTick() => Apply();
    }
}
