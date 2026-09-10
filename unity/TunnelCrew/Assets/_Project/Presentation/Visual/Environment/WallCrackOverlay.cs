using UnityEngine;
using UnityEngine.Tilemaps;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 벽 정면 위에 얹는 균열 오버레이(3차 아트 ③ · 코어키퍼 <c>wallCrackFront</c>).
    ///
    /// 정면 타일맵과 같은 소팅 레이어에서 order +1, <b>같은 재질</b>을 빌려 같은 빛을 받는다 —
    /// 재질을 빌리지 않고 Sprite-Lit-Default 로 두면 <c>_MaskTex</c> 기본이 white 라 마스크 광원에 날아간다
    /// (SurfaceMaterialSet 주석). 본선은 <c>WorldGrid.DamageStage</c>(0~3)를, 랩은 타격 카운트를 단계로 넣는다.
    /// </summary>
    public sealed class WallCrackOverlay : MonoBehaviour
    {
        Tilemap _map;
        Tile[] _tiles;

        public bool Ready => _map != null && _tiles != null && _tiles.Length > 0;

        /// <summary>
        /// <paramref name="host"/> 는 정면(BackStructure) 타일맵 렌더러. 이 오브젝트는 그 부모(그리드) 아래에 있어야 한다.
        /// 키트에 균열이 없으면 아무것도 만들지 않고 <see cref="Ready"/> 가 false 다.
        /// </summary>
        public void Bind(EnvironmentKit kit, TilemapRenderer host)
        {
            if (kit == null || kit.wallCrack == null || kit.wallCrack.Length == 0) return;
            // 정면 타일맵의 그리드 아래로 옮기지 않는다 — 렌더러가 Bind 마다 그 그리드를 파괴한다. 자기 그리드를 쓴다(셀 1유닛, 원점 같음).
            if (GetComponentInParent<Grid>() == null)
            {
                var grid = gameObject.AddComponent<Grid>();
                grid.cellSize = new Vector3(1f, 1f, 0f);
            }

            if (_map == null)
            {
                _map = gameObject.AddComponent<Tilemap>();
                var tr = gameObject.AddComponent<TilemapRenderer>();
                tr.mode = TilemapRenderer.Mode.Individual;
                if (host != null)
                {
                    tr.sortingLayerName = host.sortingLayerName;
                    tr.sortingOrder = host.sortingOrder + 1;
                    tr.sharedMaterial = host.sharedMaterial;
                }
                else if (VisualLayers.Exists(VisualLayers.BackStructure))
                    tr.sortingLayerName = VisualLayers.BackStructure;
            }
            else _map.ClearAllTiles();

            _tiles = new Tile[kit.wallCrack.Length];
            for (int i = 0; i < _tiles.Length; i++)
            {
                var t = ScriptableObject.CreateInstance<Tile>();
                t.sprite = kit.wallCrack[i];
                t.colliderType = Tile.ColliderType.None;
                _tiles[i] = t;
            }
        }

        /// <summary>단계 0 = 지움, 1.. = 균열 스프라이트(마지막 단계로 클램프).</summary>
        public void SetStage(int col, int row, int stage)
        {
            if (!Ready) return;
            var pos = new Vector3Int(col, row, 0);
            if (stage <= 0) { _map.SetTile(pos, null); return; }
            _map.SetTile(pos, _tiles[Mathf.Clamp(stage - 1, 0, _tiles.Length - 1)]);
        }

        public void Clear() { if (_map != null) _map.ClearAllTiles(); }
    }
}
