using UnityEngine;
using UnityEngine.Tilemaps;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 벽 셀에 물리 콜라이더를 세운다.
    ///
    /// <b>본편에는 이런 것이 없다.</b> 본편은 <c>TunnelSim</c> 이 <c>World.IsSolid</c> 로 충돌을
    /// 직접 계산하고 Unity 물리를 쓰지 않는다(프로젝트 전체에 Collider2D 사용처가 0건이었다).
    /// 랩에는 Sim 이 없으므로 걸어서 벽에 막히는 감각을 보려면 물리가 필요하다 —
    /// <b>랩 전용</b>이고 본편 이동 규칙을 대체하지 않는다.
    ///
    /// 셀마다 BoxCollider2D 를 놓지 않는다. 34×20 방이면 수백 개가 되고 모서리마다 걸린다.
    /// 타일맵 콜라이더를 <see cref="CompositeCollider2D"/> 로 합쳐 벽 덩어리 하나당 외곽선
    /// 하나로 만든다 — 캐릭터가 벽면을 따라 미끄러진다.
    /// </summary>
    public sealed class LabWallCollision : MonoBehaviour
    {
        [SerializeField] bool _enabled = true;

        Tilemap _map;
        TilemapCollider2D _tc;
        CompositeCollider2D _composite;
        Tile _tile;
        ISolidField _field;

        public int Cells { get; private set; }

        public bool Active
        {
            get => _enabled;
            set
            {
                _enabled = value;
                if (_tc != null) _tc.enabled = value;
                if (_composite != null) _composite.enabled = value;
            }
        }

        /// <summary>벽이 바뀌면 <see cref="LabEnvironment"/> 가 다시 부른다.</summary>
        public void Resync(ISolidField field)
        {
            if (field != null) _field = field;
            if (_field == null) return;

            EnsureMap();
            _map.ClearAllTiles();

            int n = 0;
            for (int r = 0; r < _field.Rows; r++)
            for (int c = 0; c < _field.Cols; c++)
            {
                if (!_field.IsSolid(c, r)) continue;
                _map.SetTile(new Vector3Int(c, r, 0), _tile);
                n++;
            }
            Cells = n;

            // 외곽선 재생성은 Synchronous 가 알아서 한다(위 EnsureMap 주석 참고).
        }

        void EnsureMap()
        {
            if (_map != null) return;

            var grid = GetComponent<Grid>();
            if (grid == null) grid = gameObject.AddComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f);

            var go = new GameObject("Collision Map");
            go.transform.SetParent(transform, false);

            _map = go.AddComponent<Tilemap>();
            // 렌더러를 붙이지 않는다 — 보이지 않는 충돌 전용 층이다.

            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;

            _tc = go.AddComponent<TilemapCollider2D>();

            // 합성기를 먼저 만든 뒤에 Merge 를 건다. 순서를 뒤집으면 붙을 합성기가 없어
            // 설정이 무시되고 외곽선이 0개가 된다(2026-09-10 실측: compositePaths=0).
            _composite = go.AddComponent<CompositeCollider2D>();
            _composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            // Manual 로 두고 Resync 안에서 GenerateGeometry 를 부르면 아무것도 안 나온다 —
            // TilemapCollider2D 는 SetTile 을 프레임 끝에 처리하므로 Awake 시점에는 도형이 0개다
            // (2026-09-10 실측: 그 자리에서 0, 한 프레임 뒤 수동 호출은 경로 17·도형 31).
            // 자동 생성에 맡기면 파괴로 타일이 바뀔 때도 알아서 따라온다.
            _composite.generationType = CompositeCollider2D.GenerationType.Synchronous;
            _tc.compositeOperation = Collider2D.CompositeOperation.Merge;

            _tile = ScriptableObject.CreateInstance<Tile>();
            _tile.colliderType = Tile.ColliderType.Grid;
            // 스프라이트를 준다 — 렌더러가 없어 보이지 않지만, 빈 타일은 콜라이더 생성에서
            // 건너뛰어질 수 있다. 안전한 쪽을 택한다.
            _tile.sprite = LabWallDropShadow.SolidCellSprite();

            Active = _enabled;
        }
    }
}
