using UnityEngine;
using UnityEngine.Rendering;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 기능명세서 §5.1·§6.5 — 한 오브젝트의 세 좌표를 분리한다.
    ///
    /// <list type="bullet">
    /// <item><c>Sim XY</c>: 이동·충돌·채굴·AI 가 쓰는 좌표. 이 컴포넌트는 읽기만 한다.</item>
    /// <item><c>Ground XY</c>: 화면에 투영된 발 위치. 정렬과 접촉 그림자의 기준이다.</item>
    /// <item><c>Visual Height</c>: 판정에 영향을 주지 않고 스프라이트를 지면 위로 솟게 하는 표현 높이.</item>
    /// </list>
    ///
    /// 정렬은 스프라이트 중심이 아니라 <b>지면 footprint 의 가장 아래 접점</b>으로 한다.
    /// 머리·무기·이펙트는 <see cref="SortingGroup"/> 내부 로컬 순서를 쓰므로 몸통과
    /// 절대 분리되지 않는다.
    /// </summary>
    /// <remarks>
    /// <c>ExecuteAlways</c> — 등록(OnEnable)만 하는 컴포넌트라 에디터에서도 켜 둔다.
    /// 그러지 않으면 코드로 붙였을 때 플레이 모드 밖에서 <see cref="FootpointSorter"/> 에
    /// 등록되지 않아, Visual Lab 자동 캡처(§12.3)에 발 위치가 나타나지 않는다.
    /// 이동·정렬 계산은 여전히 매니저가 부르는 <see cref="Apply"/> 에서만 일어난다.
    /// </remarks>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class VisualHeightAnchor : MonoBehaviour
    {
        [Header("지면 (§5.1)")]
        [Tooltip("발 위치 — 시뮬레이션 셀 좌표. 매 프레임 뷰가 갱신한다.")]
        public Vector2 groundPosition;

        [Tooltip("여러 셀을 차지하는 구조물의 footprint 크기(셀). 정렬은 가장 앞쪽 경계를 쓴다(§6.5).")]
        public Vector2 footprintCells = Vector2.one;

        [Tooltip("전경 페이드 겹침 판정에 쓰는 반지름(셀).")]
        [Range(0.1f, 3f)] public float footprintRadius = 0.45f;

        [Header("시각 높이")]
        [Tooltip("지면 위로 솟는 높이(월드 유닛). 공중 물체는 정렬 기준을 지면에 두고 본체만 올린다.")]
        public float visualHeight;

        [Tooltip("visualHeight 만큼 올릴 자식. 비어 있으면 아무것도 옮기지 않는다.")]
        public Transform body;

        [Header("정렬")]
        [Tooltip("소속 Sorting Layer. 비면 WorldEntity.")]
        public string sortingLayer = VisualLayers.WorldEntity;

        [Tooltip("같은 지면 Y 에서의 미세 우선순위. 셀 하나를 넘지 않는 작은 값만 쓴다.")]
        public int localOrder;

        [Header("전경 페이드 (§6.6)")]
        [Tooltip("로컬 관심 캐릭터인가. 전경 오클루더는 이 대상들의 footprint 합집합으로 페이드한다.")]
        public bool isLocalInterest;

        [Tooltip("가려졌을 때 실루엣 보정을 받는가. 적은 완전 투명 처리하지 않는다.")]
        public bool wantsSilhouette;

        SortingGroup _group;
        Renderer[] _renderers;
        int _appliedOrder = int.MinValue;
        string _appliedLayer;

        /// <summary>
        /// 자식 렌더러에도 같은 Sorting Layer 를 넣는다.
        ///
        /// <b>SortingGroup 만으로는 부족하다.</b> URP 2D 렌더러는 <b>렌더러 자신의</b>
        /// <c>sortingLayerID</c> 로 어느 광원 배치(batch)에 그릴지 정한다. 자식이
        /// <c>Default</c> 로 남으면 그 배치에서 그려지고, <c>Default</c> 는 §6.4 순서의
        /// 최하위라 바닥 타일 아래로 내려간다 — 화면에서는 그냥 사라진 것처럼 보인다.
        /// SortingGroup 은 그 뒤 "그룹 안에서의 상대 순서" 만 담당한다.
        ///
        /// 실제로 세트피스 7종이 이 이유로 전부 보이지 않았다(2026-09-08).
        /// </summary>
        void ApplyLayerToRenderers(string layer)
        {
            if (_renderers == null || _renderers.Length == 0)
                _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);

            int id = SortingLayer.NameToID(layer);
            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null) continue;
                r.sortingLayerID = id;
            }
        }

        /// <summary>자식 렌더러가 늘거나 줄었을 때 다시 모으게 한다.</summary>
        public void InvalidateRenderers()
        {
            _renderers = null;
            _appliedLayer = null;
        }

        /// <summary>정렬 기준이 되는 지면 Y — footprint 의 가장 앞쪽(작은 Y) 경계.</summary>
        public float SortGroundY => groundPosition.y - Mathf.Max(0f, footprintCells.y - 1f) * 0.5f;

        /// <summary>
        /// SortingGroup 을 지연 해석한다. 에디터에서 코드로 붙인 컴포넌트는 Awake 가
        /// 불리지 않으므로(플레이 모드가 아닐 때), Visual Lab 캡처가 Apply 를 직접
        /// 부르는 경로에서도 정렬이 적용되게 한다.
        /// </summary>
        SortingGroup Group()
        {
            if (_group != null) return _group;
            if (!TryGetComponent(out _group)) _group = gameObject.AddComponent<SortingGroup>();
            return _group;
        }

        void Awake() => Group();

        void OnEnable() => FootpointSorter.Register(this);
        void OnDisable() => FootpointSorter.Unregister(this);

        /// <summary>
        /// 발 위치를 화면 좌표로 옮기고 정렬값을 적용한다.
        /// <see cref="FootpointSorter"/> 가 LateUpdate 에서 한 번에 부른다.
        /// </summary>
        public void Apply(int unitsPerCell)
        {
            var ground = IsometricProjection.ToRender(groundPosition);
            var t = transform;
            var p = t.position;
            t.position = new Vector3(ground.x, ground.y, p.z);

            if (body != null)
            {
                var lp = body.localPosition;
                if (!Mathf.Approximately(lp.y, visualHeight))
                    body.localPosition = new Vector3(lp.x, visualHeight, lp.z);
            }

            var group = Group();
            if (group == null) return;

            int order = DepthSort.OrderFor(SortGroundY, unitsPerCell) + localOrder;
            if (order != _appliedOrder)
            {
                group.sortingOrder = order;
                _appliedOrder = order;
            }

            string layer = string.IsNullOrEmpty(sortingLayer) ? VisualLayers.WorldEntity : sortingLayer;
            if (layer != _appliedLayer)
            {
                // 없는 레이어 이름을 넣으면 Unity 가 조용히 Default 로 떨어뜨린다.
                // 소팅 레이어는 Tunnel Crew/비주얼 · 소팅 레이어 생성 이 만든다.
                if (VisualLayers.Exists(layer))
                {
                    group.sortingLayerName = layer;
                    ApplyLayerToRenderers(layer);
                }
                _appliedLayer = layer;
            }
        }
    }
}
