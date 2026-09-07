using System.Collections.Generic;
using TunnelCrew.Data;
using TunnelCrew.Sim;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TunnelCrew.Presentation
{
    /// <summary>
    /// <see cref="WorldGrid"/> 를 Tilemap 으로 그린다.
    ///
    /// 원본은 매 프레임 5,760칸을 순회하며 컬링·드로우했다(analysis-01 §8.3).
    /// Tilemap 은 청크 단위로 배칭·컬링하므로 바뀐 칸만 갱신하면 된다.
    /// </summary>
    public sealed class WorldRenderer : MonoBehaviour
    {
        [SerializeField] TileSetAsset _tileSet;
        [SerializeField] Tilemap _floor;
        [SerializeField] Tilemap _walls;
        [SerializeField] Tilemap _coreTop;

        WorldGrid _world;

        /// <summary>이 칸이 보스 소환 벽인가 — RunBootstrap 이 BossSystem.WallCells 로 연결한다 (원본 INF.bossWallCells).</summary>
        public System.Func<int, bool> IsBossWall;

        /// <summary>슬롯 번호 → 런타임 Tile. 268개를 에셋으로 굽지 않고 한 번만 만든다.</summary>
        readonly Dictionary<int, Tile> _tileCache = new Dictionary<int, Tile>();
        readonly Dictionary<Sprite, Tile> _spriteTiles = new Dictionary<Sprite, Tile>();
        static readonly Matrix4x4 FlipX = Matrix4x4.Scale(new Vector3(-1, 1, 1));

        public void Bind(WorldGrid world)
        {
            _world = world;
            _world.TileBroken += e => RefreshCell(e.Col, e.Row);
            _world.TileDamaged += e => RefreshCell(e.Col, e.Row);
            _world.TileChanged += k => RefreshCell(k % _world.Cols, k / _world.Cols);
            RebuildAll();
        }

        public void RebuildAll()
        {
            if (_world == null || _tileSet == null) return;

            _floor.ClearAllTiles();
            _walls.ClearAllTiles();
            if (_coreTop != null) _coreTop.ClearAllTiles();

            // 바닥은 한 번만 깔면 된다. 벽이 부서져도 바뀌지 않는다.
            for (int r = 0; r < _world.Rows; r++)
                for (int c = 0; c < _world.Cols; c++)
                {
                    var sp = _tileSet.FloorAt(c, r);
                    if (sp != null) _floor.SetTile(new Vector3Int(c, r, 0), TileFor(sp));
                }

            for (int r = 0; r < _world.Rows; r++)
                for (int c = 0; c < _world.Cols; c++)
                    RefreshCell(c, r);
        }

        void RefreshCell(int c, int r)
        {
            if (_world == null || _tileSet == null) return;
            var pos = new Vector3Int(c, r, 0);
            var type = _world.At(c, r);

            if (type == TileType.Empty)
            {
                _walls.SetTile(pos, null);
                _walls.SetTransformMatrix(pos, Matrix4x4.identity);
                if (_coreTop != null) _coreTop.SetTile(pos, null);
                return;
            }

            int k = _world.Index(c, r);

            // 보스 소환 벽 — 전용 타일 2종을 셀 고정 해시로 선택·좌우 플립 (원본 8172~8184, 피격 단계 없이 그대로 유지)
            if (IsBossWall != null && IsBossWall(k))
            {
                var bw = _tileSet.BossWall(k, out bool flip);
                if (bw != null)
                {
                    _walls.SetTile(pos, TileFor(bw));
                    _walls.SetTransformMatrix(pos, flip ? FlipX : Matrix4x4.identity);
                    if (_coreTop != null) _coreTop.SetTile(pos, null);
                    return;
                }
            }

            int band = _world.BandAt(k);
            int surface = _world.DecAt(k);
            int damage = _world.DamageStage(k);

            var sprite = _tileSet.Get(type, damage, band, surface);
            _walls.SetTile(pos, sprite != null ? TileFor(sprite) : null);
            _walls.SetTransformMatrix(pos, Matrix4x4.identity);
        }

        Tile TileFor(Sprite sprite)
        {
            if (_spriteTiles.TryGetValue(sprite, out var t)) return t;
            t = ScriptableObject.CreateInstance<Tile>();
            t.sprite = sprite;
            t.colliderType = Tile.ColliderType.None;   // 충돌은 Sim 이 그리드로 직접 판정한다
            _spriteTiles[sprite] = t;
            return t;
        }

        void OnDestroy()
        {
            foreach (var t in _spriteTiles.Values) if (t != null) Destroy(t);
            _spriteTiles.Clear();
            _tileCache.Clear();
        }

#if UNITY_EDITOR
        /// <summary>에디터에서 참조를 꽂아 주기 위한 헬퍼.</summary>
        public void EditorAssign(TileSetAsset set, Tilemap floor, Tilemap walls, Tilemap coreTop)
        {
            _tileSet = set; _floor = floor; _walls = walls; _coreTop = coreTop;
        }
#endif
    }
}
