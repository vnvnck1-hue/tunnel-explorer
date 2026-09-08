using UnityEngine;
using UnityEngine.Rendering;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>§7.5 디버그 격리 — 한 층만 남겨 보고 값을 잡는다.</summary>
    public enum AtmosphereChannel
    {
        Composite = 0,
        FogOnly = 1,
        DepthOnly = 2,
        VignetteOnly = 3,
        StateTintOnly = 4,
        GrainOnly = 5,
    }

    /// <summary>
    /// 기능명세서 §7.5 — 대기 원근 패스를 카메라 앞 화면 쿼드로 그린다.
    ///
    /// <b>Renderer2D.asset 에 RendererFeature 를 추가하지 않는다.</b> §7.5 는
    /// "AtmosphereRendererFeature 또는 동등한 전체화면 패스" 라고 열어 두었고, §6.4 는
    /// 이미 <c>VisionAndGrade</c> 레이어에 "LOS 어둠, 안개, 깊이 색보정" 을 배정해 두었다.
    /// 같은 레이어의 쿼드로 만들면 본선 렌더 파이프라인 자산이 그대로 남아, 이번 배치의
    /// "실제 게임 씬의 최종 조명·후처리 수치를 확정하지 말 것" 제약을 지킬 수 있다.
    /// 구조는 <see cref="TunnelCrew.Presentation.DarknessOverlay"/> 와 같다.
    ///
    /// 과노출 클램프와 블룸은 색 버퍼를 읽어야 하므로 여기서 하지 않는다 — 이미 붙어 있는
    /// URP Volume 이 담당한다(§7.5 후반). 광선 축(light shaft)은 광원별 형상이 필요해
    /// §11.2 VFX 쪽으로 미룬다.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class AtmosphereDirector : MonoBehaviour
    {
        [Header("프로파일 (§12.1)")]
        [Tooltip("비어 있으면 아무것도 그리지 않는다 — 프로파일이 유일한 수치 출처다.")]
        [SerializeField] AtmosphereProfile _profile;

        [Header("스위치")]
        [SerializeField] bool _enabled = true;

        [Tooltip("전체 세기. 0 이면 완전 통과 — 켜고 끈 화면을 나란히 비교할 때 쓴다.")]
        [SerializeField, Range(0f, 1f)] float _intensity = 1f;

        [SerializeField] AtmosphereChannel _isolate = AtmosphereChannel.Composite;

        [Header("전역 상태 색조 (§7.5)")]
        [Tooltip("경보·과열 같은 전역 상태색. 알파가 세기다.")]
        [SerializeField] Color _stateTint = new Color(0.6f, 0.1f, 0.1f, 0f);

        [Header("품질 단계·접근성 (§13·§10.3)")]
        [Tooltip("저층 안개 배율. 품질 단계가 정한다(§13 'Low: 저층 안개 축소').")]
        [SerializeField, Range(0f, 2f)] float _fogScale = 1f;

        [Tooltip("그레인·플래시 배율. 광과민 옵션에서 0 이 된다(§10.3).")]
        [SerializeField, Range(0f, 1f)] float _flashScale = 1f;

        [Header("결정성 (§16.3)")]
        [Tooltip("그레인을 시간에 고정한다. 자동 캡처는 항상 고정 상태로 찍는다.")]
        [SerializeField] bool _freezeGrain;

        Camera _camera;
        MeshRenderer _renderer;
        Material _material;

        static readonly int EnableId = Shader.PropertyToID("_Enable");
        static readonly int FogColorId = Shader.PropertyToID("_FogColor");
        static readonly int FogDensityId = Shader.PropertyToID("_FogDensity");
        static readonly int FogShapeId = Shader.PropertyToID("_FogShape");
        static readonly int NearTintId = Shader.PropertyToID("_NearTint");
        static readonly int FarTintId = Shader.PropertyToID("_FarTint");
        static readonly int DepthSeparationId = Shader.PropertyToID("_DepthSeparation");
        static readonly int VignetteColorId = Shader.PropertyToID("_VignetteColor");
        static readonly int VignetteShapeId = Shader.PropertyToID("_VignetteShape");
        static readonly int StateTintId = Shader.PropertyToID("_StateTint");
        static readonly int GrainStrengthId = Shader.PropertyToID("_GrainStrength");
        static readonly int GrainDensityId = Shader.PropertyToID("_GrainDensity");
        static readonly int GrainTimeId = Shader.PropertyToID("_GrainTime");
        static readonly int IsolateId = Shader.PropertyToID("_Isolate");

        public AtmosphereProfile Profile
        {
            get => _profile;
            set { _profile = value; Apply(); }
        }

        public bool Active
        {
            get => _enabled;
            set { _enabled = value; Apply(); }
        }

        public AtmosphereChannel Isolate
        {
            get => _isolate;
            set { _isolate = value; Apply(); }
        }

        public Color StateTint
        {
            get => _stateTint;
            set { _stateTint = value; Apply(); }
        }

        public bool FreezeGrain
        {
            get => _freezeGrain;
            set { _freezeGrain = value; Apply(); }
        }

        /// <summary>저층 안개 배율(§13). 품질 단계가 넣는다.</summary>
        public float FogScale
        {
            get => _fogScale;
            set { _fogScale = Mathf.Max(0f, value); Apply(); }
        }

        /// <summary>그레인 배율(§10.3). 광과민 옵션에서 0 이다.</summary>
        public float FlashScale
        {
            get => _flashScale;
            set { _flashScale = Mathf.Clamp01(value); Apply(); }
        }

        /// <summary>다음 층으로 격리를 돌린다(디버그 키 한 개로 순환).</summary>
        public AtmosphereChannel CycleIsolate()
        {
            _isolate = (AtmosphereChannel)(((int)_isolate + 1) % 6);
            Apply();
            return _isolate;
        }

        public void Bind(Camera cam, AtmosphereProfile profile)
        {
            _camera = cam;
            _profile = profile;
            BuildQuad();
            FollowCamera();
            Apply();
        }

        void OnEnable()
        {
            if (_camera == null) _camera = GetComponentInParent<Camera>();
            BuildQuad();
            Apply();
        }

        void BuildQuad()
        {
            if (_renderer == null)
            {
                if (!TryGetComponent(out MeshFilter mf)) mf = gameObject.AddComponent<MeshFilter>();
                if (mf.sharedMesh == null) mf.sharedMesh = BuildUnitQuad();

                if (!TryGetComponent(out _renderer)) _renderer = gameObject.AddComponent<MeshRenderer>();
                _renderer.shadowCastingMode = ShadowCastingMode.Off;
                _renderer.receiveShadows = false;
                _renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            }

            // §6.4 — 깊이 색보정은 VisionAndGrade 레이어의 몫이다. 어둠 오버레이보다
            // 앞(더 큰 order)에 두어 안개가 어둠 위에 얹히게 한다.
            if (VisualLayers.Exists(VisualLayers.VisionAndGrade))
                _renderer.sortingLayerName = VisualLayers.VisionAndGrade;
            _renderer.sortingOrder = 100;

            if (_material == null)
            {
                var shader = Shader.Find("TunnelCrew/Atmosphere");
                if (shader == null)
                {
                    Debug.LogError("[§7.5] TunnelCrew/Atmosphere 셰이더를 찾지 못했다.");
                    return;
                }
                _material = new Material(shader) { name = "AtmosphereMat" };
            }
            _renderer.sharedMaterial = _material;
        }

        static Mesh BuildUnitQuad()
        {
            var m = new Mesh { name = "AtmosphereQuad" };
            m.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0),
                new Vector3(-0.5f,  0.5f, 0), new Vector3(0.5f,  0.5f, 0),
            };
            m.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>카메라 시야를 덮도록 쿼드를 맞춘다. 셰이더는 실제 스크린 UV 를 쓰므로 여유분은 무해하다.</summary>
        void FollowCamera()
        {
            if (_camera == null) return;
            float h = _camera.orthographicSize * 2f;
            float w = h * _camera.aspect;
            var cp = _camera.transform.position;
            transform.position = new Vector3(cp.x, cp.y, 0f);
            transform.localScale = new Vector3(w * 1.15f, h * 1.15f, 1f);
        }

        /// <summary>프로파일 값을 머티리얼에 밀어 넣는다.</summary>
        public void Apply()
        {
            if (_material == null) return;

            float e = (_enabled && _profile != null) ? Mathf.Clamp01(_intensity) : 0f;
            _material.SetFloat(EnableId, e);
            _material.SetFloat(IsolateId, (int)_isolate);
            if (_profile == null) return;

            _material.SetColor(FogColorId, _profile.fogColor);
            _material.SetFloat(FogDensityId, _profile.fogDensity * Mathf.Max(0f, _fogScale));
            _material.SetVector(FogShapeId, _profile.FogShape);

            _material.SetColor(NearTintId, _profile.nearTint);
            _material.SetColor(FarTintId, _profile.farTint);
            _material.SetFloat(DepthSeparationId, _profile.depthSeparation);

            _material.SetColor(VignetteColorId, _profile.vignetteColor);
            _material.SetVector(VignetteShapeId, _profile.VignetteShape);

            _material.SetColor(StateTintId, _stateTint);

            _material.SetFloat(GrainStrengthId, _profile.grainStrength * Mathf.Clamp01(_flashScale));
            _material.SetFloat(GrainDensityId, _profile.grainDensity);
            _material.SetFloat(GrainTimeId, GrainTime(_profile, _freezeGrain, Time.unscaledTime));
        }

        /// <summary>
        /// 그레인 시간. 고정이거나 프로파일이 애니메이션을 껐으면 항상 0 이다 —
        /// §16.3 회귀 캡처가 프레임마다 달라지면 비교가 불가능해진다.
        /// </summary>
        public static float GrainTime(AtmosphereProfile profile, bool freeze, float time)
        {
            if (freeze || profile == null || !profile.animateGrain) return 0f;
            // 초당 24 스텝의 이산 시간 — 부동소수 정밀도 손실 없이 필름 느낌이 난다.
            return Mathf.Floor(time * 24f);
        }

        void LateUpdate()
        {
            if (_camera == null) _camera = GetComponentInParent<Camera>();
            FollowCamera();
            Apply();
        }

        /// <summary>에디터 캡처 경로 — LateUpdate 가 돌지 않으므로 직접 부른다.</summary>
        public void EditorTick()
        {
            if (_camera == null) _camera = GetComponentInParent<Camera>();
            BuildQuad();
            FollowCamera();
            Apply();
        }

        void OnDestroy()
        {
            if (_material != null) DestroyImmediate(_material);
        }
    }
}
