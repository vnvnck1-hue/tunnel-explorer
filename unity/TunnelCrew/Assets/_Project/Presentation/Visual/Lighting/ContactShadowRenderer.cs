using System.Collections.Generic;
using UnityEngine;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 접촉 그림자를 원하는 개체. 발 위치는 <see cref="VisualHeightAnchor"/> 에서 읽는다.
    ///
    /// 컴포넌트 값이 0 이하면 프로파일 값을 쓴다 — 아트별 조정이 필요한 것만 덮어쓰게 한다.
    /// </summary>
    /// <remarks><c>ExecuteAlways</c> — 등록만 하는 컴포넌트다. 자세한 이유는
    /// <see cref="VisualHeightAnchor"/> 의 같은 주석을 볼 것.</remarks>
    [ExecuteAlways]
    [RequireComponent(typeof(VisualHeightAnchor))]
    public sealed class ContactShadow : MonoBehaviour
    {
        [Tooltip("타원의 가로 반지름(셀). 0 이하면 프로파일의 contactShadowRadius.")]
        public float radius;

        [Tooltip("세로 눌림. 0 이하면 0.45 — 바닥에 누운 타원으로 보이게 한다.")]
        public float squash;

        [Tooltip("불투명도. 0 이하면 프로파일의 contactShadowOpacity.")]
        public float opacity;

        [Tooltip("발 위치에서의 오프셋(셀). 바퀴·다리 위치가 실루엣 중심과 다를 때 쓴다.")]
        public Vector2 offset;

        [Tooltip("전용 스프라이트. 비면 렌더러가 만든 부드러운 타원을 쓴다.")]
        public Sprite sprite;

        [Tooltip("공중에 뜬 물체는 높이에 따라 그림자가 작고 옅어진다.")]
        public bool scaleWithVisualHeight = true;

        [Tooltip("발밑 AO 뒤로 뻗는 짧은 투사 그림자 길이(셀). 0이면 접촉 AO만 그린다.")]
        public float castLength;

        [Tooltip("화면 좌표 기준 투사 방향. 최상위 레퍼런스의 공통 키라이트는 우하향 그림자를 만든다.")]
        public Vector2 castDirection = new Vector2(0.72f, -0.38f);

        [Tooltip("접촉 AO 대비 투사 그림자 불투명도 배율.")]
        [Range(0f, 1f)] public float castOpacity = 0.58f;

        VisualHeightAnchor _anchor;
        public VisualHeightAnchor Anchor => _anchor != null ? _anchor : _anchor = GetComponent<VisualHeightAnchor>();

        void OnEnable() => ContactShadowRenderer.Register(this);
        void OnDisable() => ContactShadowRenderer.Unregister(this);
    }

    /// <summary>
    /// 기능명세서 §7.4-1 — 접촉 AO. 발·바퀴·기둥·상자가 바닥에 닿는 짧고 부드러운 음영이다.
    ///
    /// <b>실루엣을 그림자로 쓰지 않는다.</b> SpriteRenderer 알파를 그대로 눌러 그리면 개체의
    /// 그림이 바닥에 한 장 더 생긴다. 여기서는 발 위치 중심의 단순한 타원(또는 지정 스프라이트)
    /// 하나만 둔다. 방향성 투사 그림자를 대신하는 검은 타원이 아니라, "바닥에 붙어 있다"를
    /// 보여 주는 작고 부드러운 음영이다.
    ///
    /// <b>왜 매니저인가</b> — <see cref="VisualHeightAnchor"/> 는 <c>SortingGroup</c> 을 붙이고,
    /// SortingGroup 은 자식 렌더러의 Sorting Layer 를 통째로 덮어쓴다. 그림자를 개체의 자식으로
    /// 두면 §6.4 의 <c>GroundDecal</c> 로 보낼 수 없다. 그래서 평평한 풀을 매니저가 들고 있다.
    /// 스프라이트와 머티리얼이 하나뿐이라 한 배치로 묶이는 이점도 있다.
    /// </summary>
    [DefaultExecutionOrder(105)]   // FootpointSorter(100) 다음 — 같은 프레임의 발 위치를 쓴다
    public sealed class ContactShadowRenderer : MonoBehaviour
    {
        static readonly List<ContactShadow> Wanted = new List<ContactShadow>(64);

        [SerializeField] WorldVisualProfile _profile;

        [Tooltip("§6.4 의 접촉 AO 레이어. 기능명세서는 GroundDecal 에 '접촉 AO' 를 둔다.")]
        [SerializeField] string _sortingLayer = VisualLayers.GroundDecal;

        [Tooltip("타원을 만들 때의 텍스처 한 변. 접촉 AO 는 부드러운 그라디언트라 작아도 된다.")]
        [SerializeField, Range(16, 256)] int _generatedSize = 64;

        [Tooltip("Neutral black flattens the characters against the purple mine. A very dark plum keeps the shadow inside the environment palette.")]
        [SerializeField] Color _shadowColor = new Color(0.055f, 0.012f, 0.075f, 0.45f);

        readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>(64);
        Sprite _defaultSprite;
        Texture2D _defaultTexture;

        public WorldVisualProfile Profile { get => _profile; set => _profile = value; }

        /// <summary>접근성·품질 단계에서 끌 수 있다. 다만 §13 은 접촉 AO 를 가독성 필수로 둔다.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>이번 프레임에 실제로 그린 그림자 수. 디버그 오버레이가 읽는다.</summary>
        public int ActiveCount { get; private set; }

        public static void Register(ContactShadow s)
        {
            if (s != null && !Wanted.Contains(s)) Wanted.Add(s);
        }

        public static void Unregister(ContactShadow s)
        {
            if (s != null) Wanted.Remove(s);
        }

        void OnDestroy()
        {
            if (_defaultSprite != null) Destroy(_defaultSprite);
            if (_defaultTexture != null) Destroy(_defaultTexture);
        }

        void LateUpdate() => Apply();

        /// <summary>
        /// 발 위치를 따라 그림자를 옮긴다. Visual Lab 캡처는 플레이 모드 없이 이걸 직접 부른다.
        /// </summary>
        public void Apply()
        {
            float defRadius = _profile != null ? _profile.contactShadowRadius : 0.34f;
            float defOpacity = _profile != null ? _profile.contactShadowOpacity : 0.45f;

            int used = 0;
            for (int i = Wanted.Count - 1; i >= 0; i--)
            {
                var want = Wanted[i];
                if (want == null) { Wanted.RemoveAt(i); continue; }
                if (!Enabled) continue;

                var anchor = want.Anchor;
                if (anchor == null) continue;

                var sr = Take(used++);
                if (sr == null) { used--; continue; }

                sr.gameObject.name = $"Contact AO {i}";

                sr.sprite = want.sprite != null ? want.sprite : DefaultSprite();

                float radius = want.radius > 0f ? want.radius : defRadius;
                float squash = want.squash > 0f ? want.squash : 0.45f;
                float opacity = want.opacity > 0f ? want.opacity : defOpacity;

                // 공중에 뜬 물체 — 정렬 기준은 지면에 두고 그림자만 작고 옅게 한다(§6.5).
                if (want.scaleWithVisualHeight && anchor.visualHeight > 0f)
                {
                    float k = 1f / (1f + anchor.visualHeight * 0.5f);
                    radius *= Mathf.Lerp(1f, 0.7f, 1f - k);
                    opacity *= k;
                }

                var ground = anchor.groundPosition + want.offset;
                var p = IsometricProjection.ToRender(ground);
                var t = sr.transform;
                t.position = new Vector3(p.x, p.y, 0f);
                t.rotation = Quaternion.identity;

                // 스프라이트는 1유닛 정사각형으로 만든다. 반지름 → 지름으로 스케일.
                // 세로는 투영의 납작함을 함께 반영한다(마름모 프리셋 비교용).
                float squashY = squash * Mathf.Max(0.05f, IsometricProjection.ShadowSquash * 2f);
                t.localScale = new Vector3(radius * 2f, radius * 2f * squashY, 1f);

                var c = _shadowColor;
                c.a = Mathf.Clamp01(opacity);
                sr.color = c;

                // 발 위치 Y 로 정렬한다. 바닥 데칼끼리도 앞뒤가 있어야 겹칠 때 튀지 않는다.
                int units = _profile != null ? _profile.depthUnitsPerCell : DepthSort.DefaultUnitsPerCell;
                sr.sortingOrder = DepthSort.OrderFor(ground.y, units);

                // 레퍼런스의 캐릭터는 발밑 AO 하나로 끝나지 않는다. 공통 키라이트의 반대편으로
                // 짧고 납작한 그림자가 한 겹 더 밀려 있어 발 방향과 공간 깊이를 동시에 읽게 한다.
                if (want.castLength > 0f)
                {
                    var cast = Take(used++);
                    if (cast == null) { used--; continue; }
                    cast.gameObject.name = $"Directional cast {i}";
                    cast.sprite = DefaultSprite();

                    Vector2 direction = want.castDirection.sqrMagnitude > .001f
                        ? want.castDirection.normalized : new Vector2(.88f, -.47f);
                    float heightSeparation = want.scaleWithVisualHeight
                        ? Mathf.Max(0f, anchor.visualHeight) * .18f : 0f;
                    Vector2 castPosition = p + direction * (want.castLength * .52f + heightSeparation);
                    var castTransform = cast.transform;
                    castTransform.position = new Vector3(castPosition.x, castPosition.y, 0f);
                    castTransform.rotation = Quaternion.Euler(0f, 0f,
                        Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                    castTransform.localScale = new Vector3(
                        radius * 1.65f + want.castLength,
                        radius * 2f * squashY * .72f, 1f);

                    var castColor = _shadowColor;
                    castColor.a = Mathf.Clamp01(opacity * want.castOpacity);
                    cast.color = castColor;
                    cast.sortingOrder = DepthSort.OrderFor(ground.y, units) - 1;
                }
            }

            // 남는 풀은 끈다. 파괴하지 않고 재사용해 GC 를 만들지 않는다(§13).
            for (int i = used; i < _pool.Count; i++)
                if (_pool[i] != null && _pool[i].enabled) _pool[i].enabled = false;

            ActiveCount = used;
        }

        SpriteRenderer Take(int index)
        {
            while (_pool.Count <= index)
            {
                var go = new GameObject($"ContactShadow {_pool.Count}");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                if (VisualLayers.Exists(_sortingLayer)) sr.sortingLayerName = _sortingLayer;
                sr.color = _shadowColor;
                _pool.Add(sr);
            }

            var r = _pool[index];
            if (r == null) return null;
            if (!r.enabled) r.enabled = true;
            return r;
        }

        /// <summary>
        /// 부드러운 타원을 코드로 만든다. 임시 아트 파일명이나 색에 의존하지 않기 위한 선택이며,
        /// 아트가 전용 스프라이트를 내놓으면 <see cref="ContactShadow.sprite"/> 로 덮어쓴다.
        /// </summary>
        Sprite DefaultSprite()
        {
            if (_defaultSprite != null) return _defaultSprite;

            int n = Mathf.Max(16, _generatedSize);
            _defaultTexture = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "ContactShadow (generated)",
            };

            var px = new Color32[n * n];
            float half = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    // 중심이 진하고 가장자리로 부드럽게 사라진다. 경계가 또렷하면
                    // "검은 타원 스티커" 로 보인다(§7.4 가 명시적으로 배제한 모습).
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);                  // smoothstep
                    px[y * n + x] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(a * 255f));
                }

            _defaultTexture.SetPixels32(px);
            _defaultTexture.Apply();

            // 1유닛 정사각형 — 크기는 transform 스케일로 준다.
            _defaultSprite = Sprite.Create(_defaultTexture, new Rect(0, 0, n, n),
                new Vector2(0.5f, 0.5f), n);
            _defaultSprite.name = "ContactShadow (generated)";
            return _defaultSprite;
        }
    }
}
