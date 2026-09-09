using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 네거티브 라이팅 — 어두운 영역을 <b>다각형으로 직접 그린다</b> (정식화 2026-09-09).
    ///
    /// <b>왜 정식인가</b> — 지금까지 암부는 <see cref="LabShadowBlob"/> 의 Sprite 타입 타원
    /// 쿠키였다(임시). 타원으로는 갱도 입구·천장 낮은 구간처럼 <b>형태가 있는 어둠</b>을 만들 수 없다.
    /// 리서치 §B-2 가 지목한 격차가 정확히 이것이다 — "아티스트가 형태를 직접 그리는 어두운 영역이 없다".
    ///
    /// <b>리플렉션을 늘리지 않았다</b> — 이주 계획 §4 규약. <c>Light2D.SetShapePath</c> 와
    /// <c>lightType</c>·<c>blendStyleIndex</c>·<c>overlapOperation</c>·<c>lightOrder</c> 가 전부
    /// public API 다. <c>ShadowCaster2D</c> 형태 주입처럼 internal 필드를 건드리는 경로가 아니다.
    ///
    /// <b>설정</b>(리서치 §B-2 권장 그대로) — <c>Freeform</c> + 블렌드 스타일 0(<c>Multiply</c>) +
    /// Overlap <c>AlphaBlend</c> + Light Order 를 일반 광원 위로. 어두운 색을 곱해 빛을 뺀다.
    ///
    /// <b>비용</b> — 그림자 캐스팅 라이트를 늘리지 않고 대비를 만드는 수단이다(성능 §4).
    /// 그림자는 켜지 않는다 — 어둠이 또 그림자를 만들 이유가 없다.
    /// </summary>
    [RequireComponent(typeof(Light2D))]
    public sealed class NegativeLightVolume : MonoBehaviour
    {
        /// <summary>일반 광원(Order 0) 위. 05-shadows.md §1 이 관례로 제시한 값.</summary>
        public const int NegativeLightOrder = 2;

        /// <summary>어둠의 색. 완전한 검정으로 넓은 면적을 덮으면 지층 색이 죽는다(§7.5).</summary>
        public static readonly Color DefaultDarkness = new Color(0.12f, 0.09f, 0.18f, 1f);

        [Tooltip("어두워질 영역의 다각형(로컬 좌표, 셀 단위). 시계·반시계 무관. 3점 이상.")]
        [SerializeField] Vector2[] _path =
        {
            new Vector2(-2f, -1f), new Vector2(2f, -1f), new Vector2(2f, 1f), new Vector2(-2f, 1f),
        };

        [Tooltip("어둠 세기. 프리셋의 negativeStrength 가 이 값을 민다.")]
        [SerializeField, Range(0f, 1f)] float _strength = 0.45f;

        [Tooltip("가장자리 페더(칸). 0 이면 딱 떨어지는 경계 — 보통 부드럽게 둔다.")]
        [SerializeField, Range(0f, 3f)] float _falloff = 0.9f;

        Light2D _light;

        public Light2D Light => _light != null ? _light : _light = GetComponent<Light2D>();

        public float Strength
        {
            get => _strength;
            set { _strength = Mathf.Clamp01(value); ApplyStrength(); }
        }

        /// <summary>다각형을 갈아끼운다. 점이 3개 미만이면 무시한다.</summary>
        public void SetPath(IReadOnlyList<Vector2> path)
        {
            if (path == null || path.Count < 3) return;
            var copy = new Vector2[path.Count];
            for (int i = 0; i < path.Count; i++) copy[i] = path[i];
            _path = copy;
            Apply();
        }

        void OnEnable() => Apply();

        void OnValidate()
        {
            if (isActiveAndEnabled) Apply();
        }

        public void Apply()
        {
            var light = Light;
            if (light == null) return;

            Configure(light, _path, _falloff);
            ApplyStrength();
        }

        void ApplyStrength()
        {
            var light = Light;
            if (light != null) light.intensity = _strength;
        }

        /// <summary>
        /// 하나의 <see cref="Light2D"/> 를 네거티브 freeform 으로 세운다.
        ///
        /// 빌더(에디터)와 런타임이 같은 함수를 쓴다 — 씬에 저장된 상태와 플레이 중 상태가
        /// 어긋나면 "고쳤는데 차이가 없다" 가 반복된다(구현 기록 §4-4 의 교훈).
        /// </summary>
        public static void Configure(Light2D light, Vector2[] path, float falloffCells)
        {
            if (light == null) return;

            light.lightType = Light2D.LightType.Freeform;
            if (path != null && path.Length >= 3)
            {
                var p = new Vector3[path.Length];
                for (int i = 0; i < path.Length; i++) p[i] = new Vector3(path[i].x, path[i].y, 0f);
                light.SetShapePath(p);
            }

            light.shapeLightFalloffSize = Mathf.Max(0f, falloffCells);

            // 블렌드 스타일 0 = Multiply. 어두운 색을 곱해 빛을 뺀다.
            light.blendStyleIndex = 0;
            light.overlapOperation = Light2D.OverlapOperation.AlphaBlend;
            light.lightOrder = NegativeLightOrder;
            light.color = DefaultDarkness;

            // 어둠이 또 그림자를 만들 이유가 없다.
            light.shadowsEnabled = false;
            light.shadowIntensity = 0f;

            // 월드 레이어 전부를 덮는다 — 오브젝트만 밝게 남으면 어둠이 뜬다.
            light.targetSortingLayers = VisualLayers.LitLayerIds();
        }

        /// <summary>축 정렬 사각형 다각형. 빌더가 간단한 암부를 놓을 때 쓴다.</summary>
        public static Vector2[] Rect(Vector2 size)
        {
            float hx = Mathf.Max(0.05f, size.x) * 0.5f, hy = Mathf.Max(0.05f, size.y) * 0.5f;
            return new[]
            {
                new Vector2(-hx, -hy), new Vector2(hx, -hy), new Vector2(hx, hy), new Vector2(-hx, hy),
            };
        }

        /// <summary>
        /// 갱도 입구처럼 <b>한쪽이 좁아지는</b> 사다리꼴. 타원으로는 만들 수 없던 형태 —
        /// 이 컴포넌트가 존재하는 이유를 화면에서 보여 주는 기본 도형이다.
        /// </summary>
        public static Vector2[] Mouth(float width, float depth, float narrow = 0.45f)
        {
            float hx = Mathf.Max(0.05f, width) * 0.5f;
            float hy = Mathf.Max(0.05f, depth) * 0.5f;
            float nx = hx * Mathf.Clamp01(narrow);
            return new[]
            {
                new Vector2(-hx, -hy), new Vector2(hx, -hy),
                new Vector2(nx, hy), new Vector2(-nx, hy),
            };
        }
    }
}
