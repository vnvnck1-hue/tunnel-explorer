using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 랩용 어두운 광원 하나 — 접촉 블롭 또는 네거티브 라이팅.
    ///
    /// <b>원리</b> — Sprite 타입 <see cref="Light2D"/> 에 흐린 원 쿠키를 넣고 <b>Multiply</b>
    /// 블렌드 스타일에 어두운 색으로 넣으면, 그 영역의 라이트 버퍼가 어두워진다. 즉 "빛을
    /// 빼는 광원"이다(<c>docs/urp-2d-lighting/05-shadows.md</c> §1·§2). 겹칠 때 색이 쌓이지
    /// 않도록 <see cref="Light2D.overlapOperation"/> 을 <c>AlphaBlend</c> 로 둔다.
    ///
    /// <b>왜 ShadowCaster2D 가 아닌가</b> — 형태 주입 API 가 internal 이라 리플렉션이 필요하고,
    /// <c>docs/urp-2d-lighting/09</c> §D 가 그것을 늘리지 말라고 못 박았다. 여기 쓰는 것은
    /// 전부 public API 다. 정확한 오클루전이 필요한 벽은 본선의 <c>WallShadowBuilder</c> 몫이고,
    /// 이 컴포넌트는 <b>랩에서 룩을 고르는 용도</b>다.
    /// </summary>
    [RequireComponent(typeof(Light2D))]
    public sealed class LabShadowBlob : MonoBehaviour
    {
        public enum Kind
        {
            /// <summary>프롭 발밑의 접촉 그림자.</summary>
            Contact = 0,

            /// <summary>아티스트가 지정한 넓은 암부(네거티브 라이팅).</summary>
            Negative = 1,
        }

        [SerializeField] Kind _kind = Kind.Contact;

        [Tooltip("월드 단위 반지름. Sprite 라이트는 쿠키 스프라이트 크기를 스케일로 따른다.")]
        [SerializeField] Vector2 _size = new Vector2(2.3f, 0.5f);

        [Tooltip("이 블롭이 얼마나 어두워질 수 있는가. 프리셋 세기에 곱한다.")]
        [SerializeField, Range(0f, 1f)] float _weight = 1f;

        Light2D _light;

        public Kind BlobKind => _kind;

        /// <summary>
        /// 프리셋이 부른다. <paramref name="strength"/> 가 0 이면 광원을 끈다 — 세기 0 인 광원도
        /// 라이트 버퍼를 차지하므로(<c>07-performance.md</c>) 아예 비활성으로 둔다.
        ///
        /// <b>왜 intensity 와 color.a 를 같이 미는가</b> — <c>05-shadows.md</c> §2 의 블롭 섀도우
        /// 인스펙터 표는 <c>Intensity 0</c> 으로 적혀 있다. Multiply 블렌드 + AlphaBlend 겹침에서
        /// 어둠을 실제로 모는 값이 intensity 인지 색 알파인지가 그 표만으로는 확정되지 않는다.
        /// 둘 다 세기에 대해 단조증가하게 밀어 두면 어느 쪽이 유효하든 버튼이 눈에 보이게
        /// 달라진다 — 어느 쪽인지 눈으로 확인하는 것이 이 랩의 목적이다.
        /// </summary>
        public void ApplyStrength(float strength)
        {
            if (_light == null) _light = GetComponent<Light2D>();

            float s = Mathf.Clamp01(strength) * _weight;
            if (s <= 0.001f)
            {
                _light.enabled = false;
                return;
            }

            _light.enabled = true;
            _light.intensity = Mathf.Lerp(0.2f, 1.5f, s);

            var c = BaseColor(_kind);
            c.a = Mathf.Lerp(0.35f, 1f, s);
            _light.color = c;
        }

        void Awake()
        {
            _light = GetComponent<Light2D>();
            Configure(_light, _kind, _size);
        }

        /// <summary>
        /// 광원을 어두운 Sprite 라이트로 세운다. 에디터 빌더도 같은 함수를 쓰므로 씬에 직렬화된
        /// 값과 런타임 값이 어긋나지 않는다.
        /// </summary>
        public static void Configure(Light2D light, Kind kind, Vector2 size)
        {
            if (light == null) return;

            light.lightType = Light2D.LightType.Sprite;
            light.lightCookieSprite = SoftEllipse();

            // Renderer2D.asset 의 블렌드 스타일 0 번이 Multiply 다. 어두운 색을 곱해 빛을 뺀다.
            light.blendStyleIndex = 0;
            light.overlapOperation = Light2D.OverlapOperation.AlphaBlend;

            light.color = BaseColor(kind);

            // 일반 광원(Light Order 0) 위에 그림자 레이어가 올라가야 어둠이 이긴다.
            // 05-shadows.md §1·§2 가 관례로 제시한 값이 2 다. 접촉을 암부보다 위에 둔다.
            light.lightOrder = kind == Kind.Contact ? 3 : 2;

            // 어두운 광원이 그림자를 또 만들 이유가 없다.
            light.shadowsEnabled = false;
            light.shadowIntensity = 0f;

            light.transform.localScale = new Vector3(Mathf.Max(0.01f, size.x),
                                                     Mathf.Max(0.01f, size.y), 1f);
        }

        /// <summary>
        /// 어둠의 색. 접촉 그림자는 더 검게, 네거티브는 살짝 색이 남게 둔다 — 완전한 검정으로
        /// 넓은 면적을 덮으면 지층 색이 죽는다(§7.5 "후처리는 형태를 대체하지 않는다").
        /// </summary>
        static Color BaseColor(Kind kind) => kind == Kind.Contact
            ? new Color(0.05f, 0.03f, 0.07f, 1f)
            : new Color(0.12f, 0.09f, 0.18f, 1f);

        // ───────────────────────────── 쿠키 스프라이트

        static Sprite s_softEllipse;

        /// <summary>
        /// 가장자리가 흐린 원 스프라이트. 런타임 생성이라 에셋을 추가하지 않는다.
        /// 1 월드유닛 크기로 만들어 두고 스케일로 늘린다.
        /// </summary>
        public static Sprite SoftEllipse()
        {
            if (s_softEllipse != null) return s_softEllipse;

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "LabSoftEllipse",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size * 2f - 1f;
                float ny = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(nx * nx + ny * ny);
                // smoothstep 로 가장자리를 부드럽게. 하드 엣지면 블롭이 접시처럼 보인다.
                float a = 1f - Mathf.SmoothStep(0.15f, 1f, r);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            s_softEllipse = Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), size);
            s_softEllipse.name = "LabSoftEllipse";
            s_softEllipse.hideFlags = HideFlags.HideAndDontSave;
            return s_softEllipse;
        }
    }
}
