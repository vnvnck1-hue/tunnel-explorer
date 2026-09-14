using System.Collections.Generic;
using UnityEngine;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>§6.6 — 가림 보정의 두 성격. 관심 캐릭터와 적은 요구가 다르다.</summary>
    public enum SilhouetteMode
    {
        /// <summary>로컬 관심 캐릭터. 불투명한 벽 위에 얇은 팀 색 외곽 림만 얹는다.</summary>
        Interest = 0,

        /// <summary>적. 벽은 페이드하지 않으므로 위협 실루엣이 벽 위로 올라와야 한다.</summary>
        Threat = 1,
    }

    /// <summary>
    /// 가림 보정을 받을 개체(기능명세서 §6.6).
    ///
    /// 값이 0 이하면 프로파일 값을 쓴다 — 아트별 조정이 필요한 것만 덮어쓰게 한다.
    /// </summary>
    /// <remarks><c>ExecuteAlways</c> — 등록만 하는 컴포넌트다. 자세한 이유는
    /// <see cref="VisualHeightAnchor"/> 의 같은 주석을 볼 것.</remarks>
    [ExecuteAlways]
    [RequireComponent(typeof(VisualHeightAnchor))]
    public sealed class OccludedSilhouette : MonoBehaviour
    {
        [Tooltip("실루엣을 뜰 원본 스프라이트. 비면 자식에서 첫 SpriteRenderer 를 찾는다.")]
        public SpriteRenderer body;

        [Tooltip("팀 색 또는 위협 색. §6.6 은 '얇은 팀 색' 을 요구한다.")]
        public Color color = new Color(0.42f, 0.78f, 1f);

        public SilhouetteMode mode = SilhouetteMode.Interest;

        [Tooltip("최대 불투명도. 0 이하면 관심은 0.85, 위협은 프로파일의 enemySilhouetteMinAlpha.")]
        [Range(0f, 1f)] public float maxAlpha;

        /// <summary>현재 표시 세기(0~1). 매니저가 시간 기반으로 보간한다.</summary>
        public float Visibility { get; internal set; }

        /// <summary>이번 프레임에 오클루더에 덮여 있었는가. 디버그 오버레이가 읽는다.</summary>
        public bool Covered { get; internal set; }

        VisualHeightAnchor _anchor;
        public VisualHeightAnchor Anchor => _anchor != null ? _anchor : _anchor = GetComponent<VisualHeightAnchor>();

        /// <summary>원본 스프라이트 렌더러. 지정이 없으면 자식에서 찾는다.</summary>
        public SpriteRenderer Body()
        {
            if (body != null) return body;
            body = GetComponentInChildren<SpriteRenderer>(includeInactive: true);
            return body;
        }

        void OnEnable() => OccludedSilhouetteRenderer.Register(this);
        void OnDisable() => OccludedSilhouetteRenderer.Unregister(this);
    }

    /// <summary>
    /// 기능명세서 §6.6·§7.1 — 가려진 캐릭터 실루엣·림 패스.
    ///
    /// <b>왜 개체의 자식이 아니라 매니저인가.</b> 위협 실루엣은 자기를 가린 전경 구조물보다
    /// <i>위에</i> 그려져야 한다. 그런데 <see cref="VisualHeightAnchor"/> 는
    /// <c>SortingGroup</c> 을 붙이고, SortingGroup 안의 자식은 자기 Sorting Layer 로
    /// 배치되더라도 최종 위치는 그룹 위치(=<c>WorldEntity</c>)를 따른다. 즉 자식으로 두면
    /// 절대 <c>FrontStructure</c> 위로 올라가지 못한다. 접촉 AO 가 같은 이유로 매니저다.
    ///
    /// <b>레이어는 <c>WorldFX</c> 다.</b> §6.4 순서에서 <c>FrontStructure</c> 보다 위,
    /// <c>VisionAndGrade</c> 보다 아래다. 그래야 벽 위로는 올라오면서 §6.6 마지막 항의
    /// "시야 밖 구조물은 기존 LOS 어둠 규칙을 우선한다" 가 지켜진다 — 시야 밖 적의
    /// 실루엣이 어둠을 뚫고 보이면 그건 정보 누설이다.
    ///
    /// 관심 캐릭터와 적의 차이는 <see cref="SilhouetteMode"/> 하나로 갈린다.
    /// 관심 캐릭터는 불투명한 벽 위에 얇은 외곽 림만 표시하고, 적은 위협의 형태까지
    /// 읽혀야 하므로 낮은 내부 채움을 더한다.
    /// </summary>
    [DefaultExecutionOrder(115)]   // ForegroundFadeController(110) 다음 — 같은 프레임의 오클루더 알파를 쓴다
    public sealed class OccludedSilhouetteRenderer : MonoBehaviour
    {
        static readonly List<OccludedSilhouette> Wanted = new List<OccludedSilhouette>(32);

        [SerializeField] WorldVisualProfile _profile;

        [Tooltip("§6.4 의 레이어. FrontStructure 보다 위, VisionAndGrade 보다 아래여야 한다.")]
        [SerializeField] string _sortingLayer = VisualLayers.WorldFX;

        [Tooltip("림 두께(원본 스프라이트 텍셀). §6.6 은 '얇은' 림을 요구한다.")]
        [SerializeField, Range(0.5f, 8f)] float _rimWidthTexels = 2f;

        [Tooltip("위협 실루엣의 내부 채움. 형태만 읽히면 되므로 낮게 둔다.")]
        [SerializeField, Range(0f, 1f)] float _threatFill = 0.22f;

        readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>(32);
        Material _rimMaterial;
        Material _threatMaterial;
        bool _warnedNoShader;

        public WorldVisualProfile Profile { get => _profile; set => _profile = value; }

        /// <summary>접근성·품질 단계에서 끌 수 있다(§14 단계 F).</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>이번 프레임에 실제로 그린 실루엣 수. 디버그 오버레이가 읽는다.</summary>
        public int ActiveCount { get; private set; }

        public static void Register(OccludedSilhouette s)
        {
            if (s != null && !Wanted.Contains(s)) Wanted.Add(s);
        }

        public static void Unregister(OccludedSilhouette s)
        {
            if (s != null) Wanted.Remove(s);
        }

        /// <summary>등록된 대상 전체. 디버그 오버레이와 테스트가 읽는다.</summary>
        public static IReadOnlyList<OccludedSilhouette> All => Wanted;

        /// <summary>
        /// 표시 세기 한 스텝. 전경 페이드와 <b>같은 시간 규약</b>을 쓴다(§6.6:
        /// 가려질 때 0.18~0.28초, 복원 0.25~0.4초). 남은 거리에 비례한 Lerp 를 쓰면
        /// 그 시간이 지켜지지 않으므로 <c>MoveTowards</c> 로 등속 이동한다.
        /// </summary>
        public static float StepVisibility(float current, bool covered,
            float outTime, float inTime, float dt)
        {
            float goal = covered ? 1f : 0f;
            float time = covered ? outTime : inTime;
            float step = time > 0.0001f ? dt / time : 1f;
            return Mathf.Clamp01(Mathf.MoveTowards(current, goal, step));
        }

        /// <summary>
        /// 모드별 최대 불투명도. 적은 완전 투명 처리하지 않는다(§6.6) — 프로파일의
        /// <c>enemySilhouetteMinAlpha</c> 가 그 하한이다.
        /// </summary>
        public static float MaxAlphaFor(SilhouetteMode mode, float declared, float enemyMinAlpha)
        {
            if (declared > 0f) return Mathf.Clamp01(declared);
            return mode == SilhouetteMode.Threat ? Mathf.Clamp01(enemyMinAlpha) : 0.85f;
        }

        void LateUpdate() => Apply(Time.unscaledDeltaTime);

        /// <summary>
        /// 덮임 여부를 다시 재고 실루엣을 그린다. Visual Lab 캡처는 플레이 모드 없이
        /// 이걸 직접 부른다.
        /// </summary>
        public void Apply(float dt)
        {
            float outTime = _profile != null ? _profile.foregroundFadeOut : 0.22f;
            float inTime = _profile != null ? _profile.foregroundFadeIn : 0.32f;
            float extra = _profile != null ? _profile.foregroundFadeRadius : 0.9f;
            float enemyMin = _profile != null ? _profile.enemySilhouetteMinAlpha : 0.55f;
            int units = _profile != null ? _profile.depthUnitsPerCell : DepthSort.DefaultUnitsPerCell;

            int used = 0;
            for (int i = Wanted.Count - 1; i >= 0; i--)
            {
                var want = Wanted[i];
                if (want == null) { Wanted.RemoveAt(i); continue; }

                var anchor = want.Anchor;
                var src = want.Body();
                if (anchor == null || src == null || src.sprite == null) continue;

                // 덮임 판정은 오클루더 알파와 무관하다. 관심 캐릭터는 이미 페이드한
                // 벽 뒤에 있고, 적은 불투명한 벽 뒤에 있지만 둘 다 "가려졌다" 다.
                bool covered = Enabled &&
                    ForegroundFadeController.CoveredAt(anchor.groundPosition,
                        anchor.footprintRadius + extra, out _);

                want.Covered = covered;
                want.Visibility = StepVisibility(want.Visibility, covered, outTime, inTime, dt);
                if (want.Visibility <= 0.001f) continue;

                var sr = Take(used++, want.mode);
                if (sr == null) { used--; continue; }

                // 원본 스프라이트를 그대로 덮어 놓는다 — 형태가 어긋나면 실루엣이 아니다.
                sr.sprite = src.sprite;
                sr.flipX = src.flipX;
                sr.flipY = src.flipY;
                var st = sr.transform;
                var bt = src.transform;
                st.SetPositionAndRotation(bt.position, bt.rotation);
                st.localScale = bt.lossyScale;

                float max = MaxAlphaFor(want.mode, want.maxAlpha, enemyMin);
                var c = want.color;
                c.a = max * want.Visibility;
                sr.color = c;

                // 실루엣끼리도 발 위치로 앞뒤를 지킨다.
                sr.sortingOrder = DepthSort.OrderFor(anchor.SortGroundY, units) + anchor.localOrder;
            }

            // 남는 풀은 끈다. 파괴하지 않고 재사용해 GC 를 만들지 않는다(§13).
            for (int i = used; i < _pool.Count; i++)
                if (_pool[i] != null && _pool[i].enabled) _pool[i].enabled = false;

            ActiveCount = used;
        }

        SpriteRenderer Take(int index, SilhouetteMode mode)
        {
            var mat = MaterialFor(mode);
            if (mat == null) return null;

            while (_pool.Count <= index)
            {
                var go = new GameObject($"Silhouette {_pool.Count}");
                go.transform.SetParent(transform, false);
                var created = go.AddComponent<SpriteRenderer>();
                // 렌더러 자신의 Sorting Layer 를 직접 넣는다 — URP 2D 는 이 값으로
                // 광원 배치를 고르고, 비어 있으면 Default(최하위)로 내려간다.
                if (VisualLayers.Exists(_sortingLayer)) created.sortingLayerName = _sortingLayer;
                _pool.Add(created);
            }

            var r = _pool[index];
            if (r == null) return null;
            if (r.sharedMaterial != mat) r.sharedMaterial = mat;
            if (!r.enabled) r.enabled = true;
            return r;
        }

        /// <summary>
        /// 모드별 공유 머티리얼. 림 두께·내부 채움은 인스턴스마다 다를 필요가 없어서
        /// 머티리얼에 둔다 — 개체마다 머티리얼을 만들면 배치가 깨진다(§13).
        /// </summary>
        Material MaterialFor(SilhouetteMode mode)
        {
            bool threat = mode == SilhouetteMode.Threat;
            ref Material slot = ref threat ? ref _threatMaterial : ref _rimMaterial;
            if (slot != null) return slot;

            var shader = Shader.Find("TunnelCrew/OccludedSilhouette");
            if (shader == null)
            {
                if (!_warnedNoShader)
                {
                    _warnedNoShader = true;
                    Debug.LogError("[§6.6] TunnelCrew/OccludedSilhouette 셰이더를 찾지 못했다 — 실루엣 보정이 없다.");
                }
                return null;
            }

            slot = new Material(shader) { name = threat ? "SilhouetteThreat" : "SilhouetteRim" };
            slot.SetFloat("_RimWidth", _rimWidthTexels);
            slot.SetFloat("_Fill", threat ? _threatFill : 0f);
            return slot;
        }

        void OnDestroy()
        {
            if (_rimMaterial != null) DestroyImmediate(_rimMaterial);
            if (_threatMaterial != null) DestroyImmediate(_threatMaterial);
        }

        /// <summary>Visual Lab 과 회귀 캡처가 상태를 초기화할 때 쓴다.</summary>
        public void ResetSilhouettes()
        {
            for (int i = 0; i < Wanted.Count; i++)
                if (Wanted[i] != null) { Wanted[i].Visibility = 0f; Wanted[i].Covered = false; }
            for (int i = 0; i < _pool.Count; i++)
                if (_pool[i] != null) _pool[i].enabled = false;
            ActiveCount = 0;
        }
    }
}
