using System.Collections.Generic;
using UnityEngine;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 세트피스가 들고 있는 광원 소켓(기능명세서 §7.3, §8.2 "조명 소켓 목록").
    /// 오프셋은 <b>발점 기준 셀 좌표</b>이고 화면 위가 +Y 다.
    /// </summary>
    [System.Serializable]
    public struct LightSocketDef
    {
        public string id;
        [Tooltip("발점에서의 오프셋(셀). 화면 위가 +Y.")]
        public Vector2 offsetCells;
        public Color color;
        [Tooltip("Light2D 외곽 반지름(셀).")]
        public float rangeCells;
        [Range(0f, 8f)] public float intensity;

        [Tooltip("§7.3 광원 분류. 그림자·깜빡임·예산 취급이 이 값으로 갈린다.")]
        public LightClass lightClass;
    }

    /// <summary>
    /// 세트피스 하나의 정의(기능명세서 §8.2 필수 출력물 + §12.1 `SetPieceCatalog`).
    ///
    /// 아트가 manifest 로 넘긴 값을 그대로 담는다 — 수치를 런타임 코드에 흩어놓지 않기
    /// 위한 경계다.
    /// </summary>
    [System.Serializable]
    public sealed class SetPieceDef
    {
        public string assetId;

        [Tooltip("Albedo 스프라이트. 피벗이 지면 접점(발점)이다.")]
        public Sprite sprite;

        [Tooltip("이 세트피스 전용 채널 묶음. 세트피스는 개별 렌더러라 자산마다 머티리얼을 가질 수 있다.")]
        public SurfaceMaterialSet materials;

        [Tooltip("점유 셀. 정렬은 가장 앞쪽 경계를 쓴다(§6.5).")]
        public Vector2Int footprintCells = Vector2Int.one;

        [Tooltip("지면 위로 솟는 높이(셀). 스프라이트에 이미 담겨 있으므로 판정·디버그용이다.")]
        public float visualHeightCells = 1f;

        [Tooltip("§6.4 의 Sorting Layer 이름.")]
        public string sortingLayer = VisualLayers.WorldEntity;

        public int localOrder;

        [Tooltip("전경 페이드 그룹. 비면 페이드하지 않는다(§6.6).")]
        public string occluderGroup;

        [Tooltip("가려졌을 때의 목표 알파. 0 이하면 프로파일 값을 쓴다.")]
        [Range(0f, 1f)] public float fadeTargetAlpha;

        public LightSocketDef[] lightSockets;

        [Tooltip("접촉 AO 반지름(셀). 0 이하면 footprint 폭에서 유추한다.")]
        public float contactShadowRadius;

        /// <summary>
        /// 그림자 캐스터 윤곽. 발점 기준 셀 좌표를 x,y 로 이어 붙인 배열이다
        /// (Unity 가 <c>Vector2[]</c> 를 중첩 직렬화하지 못해 평탄화했다).
        /// 비면 footprint 사각형을 쓴다.
        /// </summary>
        public float[] shadowContourCells;

        [Tooltip("파괴 후 대체 자산 ID(§8.2). 비면 대체하지 않는다.")]
        public string replacementAssetId;

        /// <summary>윤곽을 점 목록으로 돌려준다. 없으면 footprint 사각형.</summary>
        public void AppendContour(Vector2 groundCell, List<Vector2> into)
        {
            into.Clear();

            if (shadowContourCells != null && shadowContourCells.Length >= 6)
            {
                for (int i = 0; i + 1 < shadowContourCells.Length; i += 2)
                    into.Add(groundCell + new Vector2(shadowContourCells[i], shadowContourCells[i + 1]));
                return;
            }

            // footprint 사각형 — 발점은 아래 변 중앙이다(아트 규격 §6).
            float halfW = Mathf.Max(1, footprintCells.x) * 0.5f;
            float depth = Mathf.Max(1, footprintCells.y);
            into.Add(groundCell + new Vector2(-halfW, 0f));
            into.Add(groundCell + new Vector2(halfW, 0f));
            into.Add(groundCell + new Vector2(halfW, depth));
            into.Add(groundCell + new Vector2(-halfW, depth));
        }

        /// <summary>전경 페이드 대상인가.</summary>
        public bool IsForeground =>
            sortingLayer == VisualLayers.FrontStructure || !string.IsNullOrEmpty(occluderGroup);

        /// <summary>접촉 AO 반지름. 지정이 없으면 footprint 폭에서 유추한다.</summary>
        public float ResolvedContactRadius =>
            contactShadowRadius > 0f
                ? contactShadowRadius
                : Mathf.Max(1, footprintCells.x) * 0.34f;
    }

    /// <summary>
    /// 기능명세서 §12.1 — 세트피스 카탈로그. 아치·기둥·조명 소품·영웅 설비처럼
    /// 타일이 아닌 자산을 담는다.
    ///
    /// <b>왜 세트피스는 아틀라스가 필요 없는가</b> — 타일은 Tilemap 하나에 머티리얼 하나를
    /// 쓰므로 자산별 채널 맵을 넣을 수 없어 아틀라스로 묶어야 했다(구현 기록 §12).
    /// 세트피스는 개별 <c>SpriteRenderer</c> 라 자산마다 머티리얼을 가질 수 있다.
    /// 방당 몇 개뿐이므로(§8.4) 배칭 손실도 문제가 되지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "Tunnel Crew/Set Piece Catalog", fileName = "SetPieceCatalog")]
    public sealed class SetPieceCatalog : ScriptableObject
    {
        [SerializeField] List<SetPieceDef> _entries = new List<SetPieceDef>();

        public IReadOnlyList<SetPieceDef> Entries => _entries;
        public int Count => _entries.Count;

        public SetPieceDef Find(string assetId)
        {
            if (string.IsNullOrEmpty(assetId)) return null;
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i] != null && _entries[i].assetId == assetId) return _entries[i];
            return null;
        }

        /// <summary>ID 접두어로 찾는다. <c>TR01-LGT</c> 처럼 분류 단위로 고를 때 쓴다.</summary>
        public void FindByPrefix(string prefix, List<SetPieceDef> into)
        {
            into.Clear();
            if (string.IsNullOrEmpty(prefix)) return;
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i] != null && _entries[i].assetId != null
                    && _entries[i].assetId.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                    into.Add(_entries[i]);
        }

#if UNITY_EDITOR
        /// <summary>임포터가 카탈로그를 다시 채운다.</summary>
        public void EditorSetEntries(List<SetPieceDef> entries)
        {
            _entries = entries ?? new List<SetPieceDef>();
        }
#endif
    }
}
