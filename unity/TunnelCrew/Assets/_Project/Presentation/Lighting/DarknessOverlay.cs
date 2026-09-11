using TunnelCrew.Sim;
using UnityEngine;

namespace TunnelCrew.Presentation
{
    /// <summary>
    /// 시야 밖을 덮는 어둠. <see cref="LosService"/> 의 결과를 셀 해상도 텍스처로 올리고
    /// 카메라 앞 쿼드에 셰이더로 그린다.
    ///
    /// 원본과 같은 방식이다. 원본도 80×72 픽셀 버퍼를 WebGL 텍스처로 올리고 바이리니어로
    /// 늘려 썼기 때문에 어둠 경계가 부드러웠다(analysis-01 §4.1). 가시 다각형 메시로 가는
    /// 안(analysis-04 §2.3)은 이 방식이 부족할 때 검토한다.
    ///
    /// 시간 보간(rise 0.16s / fall 0.38s)은 원본 <c>updateLosVisual()</c> 을 그대로 옮겼다.
    /// 이게 없으면 모퉁이를 돌 때 시야가 딱딱 끊긴다.
    /// </summary>
    [ExecuteAlways]
    public sealed class DarknessOverlay : MonoBehaviour
    {
        [Header("보간 — 원본 updateLosVisual")]
        [Tooltip("보이기 시작할 때의 시상수(초).")]
        [SerializeField] float _riseTime = 0.16f;
        [Tooltip("안 보이게 될 때의 시상수(초). 사라지는 쪽이 느려야 자연스럽다.")]
        [SerializeField] float _fallTime = 0.38f;

        [Header("색")]
        [SerializeField] Color _darkColor = new Color(0.03f, 0.025f, 0.06f, 1f);
        [SerializeField] Color _memoryColor = new Color(0.10f, 0.09f, 0.16f, 1f);
        [SerializeField, Range(0f, 1f)] float _maxDarkness = 1f;
        [Tooltip("시야 smoothstep 상한(원본 fogProg 0.88). 낮추면 경계가 날카로워진다. 경계 폭 자체는 셰이더의 1.35칸 9탭 필터가 만든다.")]
        [SerializeField, Range(0.05f, 1f)] float _edgeSoftness = 0.88f;

        [Header("경계 해상도")]
        [Tooltip("셀당 텍셀 수. 1 = 셀 해상도(경계가 한 칸 폭으로 번진다). 4 면 경계가 1/4칸 폭으로 죄어져 " +
                 "코어키퍼처럼 벽 가장자리에서 딱 끊긴다(2026-09-10). 값은 Bind 에서 확정된다.")]
        [SerializeField, Range(1, 8)] int _supersample = 1;

        [Tooltip("보이는 <b>벽 칸</b>의 가시도. 1 = 바닥과 같게(기본). 0.5 면 LOS 가 벽 타일까지 표시해도 그 칸은 절반만 " +
                 "드러나 암반이 어둡게 읽힌다 — 코어키퍼의 '윗면은 빛을 못 받는다' 를 소팅 레이어 없는 본선에서 흉내내는 값(2026-09-10). " +
                 "랩은 정면·윗면을 레이어로 나누므로 1 을 쓴다.")]
        [SerializeField, Range(0f, 1f)] float _solidVisibility = 1f;

        /// <summary>
        /// 굴착 경계에 <b>맞닿은</b> 고체 칸의 가시도 배율. 벽면과 이어지는 윗면이 여기에 해당한다.
        /// 1 보다 크면 경계 쪽 윗면이 안쪽보다 밝아져, 평평한 검정이 아니라 <b>면이 꺾이는 그라데이션</b>
        /// 으로 읽힌다. 남쪽(우리가 정면을 보는 방향)이 열린 칸은 더 크게 받는다.
        /// </summary>
        [SerializeField, Range(1f, 6f)] float _solidEdgeBoost = 2.6f;

        /// <summary>경계에 닿지 않은 안쪽 고체 칸의 배율. 깊을수록 검게 잠긴다.</summary>
        [SerializeField, Range(0f, 1f)] float _solidInteriorScale = 0.35f;

        /// <summary>보이는 벽 칸의 가시도(위 필드 주석).</summary>
        public void SetSolidVisibility(float v) => _solidVisibility = Mathf.Clamp01(v);

        LosService _los;
        Camera _camera;

        Texture2D _losTex;
        Color32[] _pixels;
        /// <summary>보간 중인 실수 값. 텍스처에 넣기 전 단계.</summary>
        float[] _visSmooth;
        float[] _memSmooth;

        MeshRenderer _renderer;
        Material _material;
        int _cols, _rows;

        static readonly int LosTexId = Shader.PropertyToID("_LosTex");
        static readonly int DarkColorId = Shader.PropertyToID("_DarkColor");
        static readonly int MemoryColorId = Shader.PropertyToID("_MemoryColor");
        static readonly int WorldSizeId = Shader.PropertyToID("_WorldSize");
        static readonly int MaxDarknessId = Shader.PropertyToID("_MaxDarkness");
        static readonly int EdgeSoftnessId = Shader.PropertyToID("_EdgeSoftness");
        static readonly int InvProjId = Shader.PropertyToID("_InvProj");

        /// <param name="supersample">셀당 텍셀 수. 1 이면 본편 그대로(셀 해상도 + 바이리니어 = 한 칸 폭 번짐).
        /// 조명 랩은 4 — 경계가 벽 가장자리에서 1/4칸 안에 끊긴다.</param>
        public void Bind(LosService los, int cols, int rows, Camera cam, int supersample = 1)
        {
            _los = los;
            _cols = cols;
            _rows = rows;
            _camera = cam;
            _supersample = Mathf.Clamp(supersample, 1, 8);

            BuildTexture();
            BuildQuad();
        }

        /// <summary>시야 경계 smoothstep 폭. 작을수록 날카롭다.</summary>
        public void SetEdgeSoftness(float softness)
        {
            _edgeSoftness = Mathf.Clamp(softness, 0.001f, 1f);
            ApplyColors();
        }

        void BuildTexture()
        {
            if (_losTex != null) DestroyImmediate(_losTex);

            int tw = _cols * _supersample, th = _rows * _supersample;
            _losTex = new Texture2D(tw, th, TextureFormat.RGBA32, false)
            {
                name = "LosTexture",
                filterMode = FilterMode.Bilinear,   // 셀 경계를 부드럽게 — 원본과 같은 이유
                wrapMode = TextureWrapMode.Clamp,
            };
            _pixels = new Color32[tw * th];
            // 시간 보간은 셀 단위로 한다 — 텍셀은 셀 값을 복제해 채운다.
            _visSmooth = new float[_cols * _rows];
            _memSmooth = new float[_cols * _rows];

            // LOS 가 아직 한 번도 계산되지 않은 동안(메뉴 배경·층 전환 직후)은 <b>전부 어둠</b>이어야 한다.
            // new Texture2D 의 내용은 미정의(실측 0.8 회색)라 그대로 두면 R=0.8 → 맵 전체가 드러난다(2026-09-10, Run 직접 실행).
            for (int i = 0; i < _pixels.Length; i++) _pixels[i] = new Color32(0, 0, 0, 255);
            _losTex.SetPixels32(_pixels);
            _losTex.Apply(false);
        }

        void BuildQuad()
        {
            if (_renderer == null)
            {
                var mf = gameObject.GetComponent<MeshFilter>();
                if (mf == null) mf = gameObject.AddComponent<MeshFilter>();
                mf.sharedMesh = BuildUnitQuad();

                _renderer = gameObject.GetComponent<MeshRenderer>();
                if (_renderer == null) _renderer = gameObject.AddComponent<MeshRenderer>();
                _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _renderer.receiveShadows = false;

                // 월드의 모든 스프라이트·타일맵보다 뒤에 그려야 가릴 수 있다.
                // 이걸 빠뜨리면 정렬 순서가 0 이라 타일맵(10, 20)이 어둠 위에 올라온다.
                _renderer.sortingLayerName = "Default";
                _renderer.sortingOrder = 1000;
            }

            var shader = Shader.Find("TunnelCrew/Darkness");
            if (shader == null)
            {
                Debug.LogError("[M2] TunnelCrew/Darkness 셰이더를 찾지 못했다.");
                return;
            }

            _material = new Material(shader) { name = "DarknessMat" };
            _renderer.sharedMaterial = _material;
            _material.SetTexture(LosTexId, _losTex);
            _material.SetVector(WorldSizeId, new Vector4(_cols, _rows, 0, 0));
            ApplyColors();
        }

        /// <summary>
        /// 정렬 레이어를 바꾼다. 본편은 <c>Default:1000</c> 로 충분하지만, 12종 소팅 레이어를 쓰는
        /// 씬(조명 랩)에서는 <c>Default</c> 가 가장 뒤라 어둠이 타일 밑에 깔린다 — 그쪽은
        /// <c>VisionAndGrade</c> 로 올려야 한다(2026-09-10).
        /// </summary>
        public void SetSorting(string layerName, int order)
        {
            if (_renderer == null) return;
            _renderer.sortingLayerName = layerName;
            _renderer.sortingOrder = order;
        }

        /// <summary>랩 프리셋이 어둠·기억 색을 즉시 바꿀 수 있게.</summary>
        public void SetColors(Color dark, Color memory, float maxDarkness = -1f)
        {
            _darkColor = dark;
            _memoryColor = memory;
            if (maxDarkness >= 0f) _maxDarkness = maxDarkness;
            ApplyColors();
        }

        void ApplyColors()
        {
            if (_material == null) return;
            _material.SetColor(DarkColorId, _darkColor);
            _material.SetColor(MemoryColorId, _memoryColor);
            _material.SetFloat(MaxDarknessId, _maxDarkness);
            _material.SetFloat(EdgeSoftnessId, _edgeSoftness);
            // 프리셋마다 렌더→시뮬 역변환이 달라진다. 매 프레임 넘겨 두면 전환 시 따로 갱신할 게 없다.
            _material.SetVector(InvProjId, IsometricProjection.InverseRow());
        }

        static Mesh BuildUnitQuad()
        {
            var m = new Mesh { name = "DarknessQuad" };
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

        void LateUpdate()
        {
            if (_los == null || _camera == null || _material == null) return;

            FollowCamera();
            UpdateTexture(Time.deltaTime);
            ApplyColors();
        }

        /// <summary>카메라 시야를 가리도록 쿼드를 맞춘다.</summary>
        void FollowCamera()
        {
            float h = _camera.orthographicSize * 2f;
            float w = h * _camera.aspect;
            // 회전·흔들림이 있어도 빈틈이 없도록 살짝 크게 잡는다
            transform.position = new Vector3(_camera.transform.position.x,
                                             _camera.transform.position.y, 0f);
            transform.localScale = new Vector3(w * 1.15f, h * 1.15f, 1f);
        }

        /// <summary>
        /// 고체 칸이 굴착 경계에 얼마나 가까운가. 경계에 닿으면 &gt;1(밝게), 아니면 안쪽 배율.
        ///
        /// 남쪽이 열린 칸을 가장 크게 본다 — 탑다운에서 우리가 마주 보는 면이 그쪽이고,
        /// 그 위의 윗면이 정면과 이어지는 부분이라 여기서 밝기가 풀려야 입체로 읽힌다.
        /// </summary>
        float SolidEdgeFactor(int c, int r)
        {
            if (_solidEdgeBoost <= 1f) return 1f;
            bool south = !SolidAt(c, r - 1);
            if (south) return _solidEdgeBoost;
            if (!SolidAt(c, r + 1) || !SolidAt(c - 1, r) || !SolidAt(c + 1, r))
                return 1f + (_solidEdgeBoost - 1f) * 0.55f;
            return _solidInteriorScale;
        }

        bool SolidAt(int c, int r)
        {
            if (c < 0 || r < 0 || c >= _cols || r >= _rows) return true;   // 맵 밖은 고체로 본다
            return _los.IsSolid(c, r);
        }

        /// <summary>
        /// 원본 updateLosVisual() — 지수 보간. 나타나는 쪽이 빠르고 사라지는 쪽이 느리다.
        /// </summary>
        void UpdateTexture(float dt)
        {
            if (!_los.HasComputed) return;

            float riseK = 1f - Mathf.Exp(-dt / Mathf.Max(0.0001f, _riseTime));
            float fallK = 1f - Mathf.Exp(-dt / Mathf.Max(0.0001f, _fallTime));

            for (int r = 0; r < _rows; r++)
                for (int c = 0; c < _cols; c++)
                {
                    int k = r * _cols + c;

                    // 고체 칸(벽)은 시야·기억 <b>양쪽</b>에서 감쇠한다. 기억에만 감쇠가 없으면
                    // 한 번 지나간 벽의 윗면이 탐색 잔상으로 계속 또렷하게 읽혀, 파낸 통로가
                    // 아니라 지형도처럼 보인다(2026-09-11 사용자 지적).
                    bool solid = _solidVisibility < 1f && _los.IsSolid(c, r);
                    // 경계에 닿은 윗면은 밝게, 안쪽은 검게 — 셀 단위 두 단계를 만들고
                    // 셰이더의 1.35칸 9탭 필터가 이를 부드러운 그라데이션으로 푼다.
                    float solidVis = solid ? _solidVisibility * SolidEdgeFactor(c, r) : 0f;
                    float visTarget = _los.Visible[k] != 0 ? (solid ? solidVis : 1f) : 0f;
                    float memTarget = _los.MemoryValue(c, r) / 255f;
                    if (solid) memTarget *= solidVis;

                    _visSmooth[k] = Mathf.Lerp(_visSmooth[k], visTarget,
                        visTarget > _visSmooth[k] ? riseK : fallK);
                    _memSmooth[k] = Mathf.Lerp(_memSmooth[k], memTarget,
                        memTarget > _memSmooth[k] ? riseK : fallK);

                    var px = new Color32(
                        (byte)(Mathf.Clamp01(_visSmooth[k]) * 255f),
                        (byte)(Mathf.Clamp01(_memSmooth[k]) * 255f),
                        0, 255);

                    // 셀 값을 supersample×supersample 텍셀에 복제한다. 바이리니어는 텍셀 경계에서만
                    // 섞이므로 경계 폭이 1/supersample 칸으로 죄어진다 — 셀 안쪽은 딱 떨어진다.
                    int ss = _supersample, tw = _cols * ss;
                    int x0 = c * ss, y0 = r * ss;
                    for (int dy = 0; dy < ss; dy++)
                    {
                        int row = (y0 + dy) * tw + x0;
                        for (int dx = 0; dx < ss; dx++) _pixels[row + dx] = px;
                    }
                }

            _losTex.SetPixels32(_pixels);
            _losTex.Apply(false);
        }

        void OnDestroy()
        {
            if (_losTex != null) DestroyImmediate(_losTex);
            if (_material != null) DestroyImmediate(_material);
        }
    }
}
