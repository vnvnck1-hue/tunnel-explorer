using System.Collections.Generic;
using TunnelCrew.Sim;
using UnityEngine;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// Primary Match의 굵은 프랍 언어를 절차 생성 월드에 얹는다.
    /// 월드 타일·충돌·채굴 가능 여부는 절대 바꾸지 않고, 연결된 빈 공간 안에 비주얼만 배치한다.
    /// </summary>
    public sealed class PrimaryMatchRoomDecorator : MonoBehaviour
    {
        SetPieceSpawner _spawner;
        ShadowGeometryBuilder _shadows;
        RectInt _room;
        string _identity;

        public string Identity => _identity;
        public RectInt Room => _room;
        public int SpawnedCount => _spawner != null ? _spawner.SpawnedCount : 0;

        public void Bind(WorldGrid world, int depth, SetPieceCatalog catalog,
            WorldVisualProfile profile, ShadowGeometryBuilder shadows)
        {
            if (_spawner == null) _spawner = gameObject.GetComponent<SetPieceSpawner>()
                                           ?? gameObject.AddComponent<SetPieceSpawner>();
            _spawner.Clear(_shadows);
            _shadows = shadows;
            _spawner.Catalog = catalog;
            _spawner.Profile = profile;
            _identity = string.Empty;
            _room = default;

            if (world == null || catalog == null || catalog.Count == 0) return;
            var open = ConnectedOpenCells(world);
            _room = FindLargestOpenRectangle(world, open);
            if (_room.width < 12 || _room.height < 5)
            {
                Debug.LogWarning($"[Primary Match] 연결 공간이 작아 방 프랍을 건너뛴다: {_room.width}x{_room.height}");
                return;
            }

            // 큰 방 한가운데에 최대 16×7짜리 디오라마 구역을 잡는다. 남는 공간은 전투 여백이다.
            int width = Mathf.Min(16, _room.width);
            int height = Mathf.Min(7, _room.height);
            int x = _room.x + (_room.width - width) / 2;
            int y = _room.y + (_room.height - height) / 2;
            _room = new RectInt(x, y, width, height);

            int variant = Mathf.Abs(depth - 1) % 3;
            if (variant == 0) BuildOreIntake();
            else if (variant == 1) BuildVentilationService();
            else BuildCrystalPower();

            if (_shadows != null) _shadows.FlushPending();
            Debug.Log($"[Primary Match] {_identity} 배치 완료: {_spawner.SpawnedCount}개, room={_room}");
        }

        HashSet<int> ConnectedOpenCells(WorldGrid world)
        {
            int startC = world.EntryCol;
            int startR = world.EntryRow;
            if (world.IsSolid(startC, startR))
            {
                bool found = false;
                for (int radius = 1; radius < Mathf.Max(world.Cols, world.Rows) && !found; radius++)
                    for (int r = Mathf.Max(0, startR - radius); r <= Mathf.Min(world.Rows - 1, startR + radius) && !found; r++)
                        for (int c = Mathf.Max(0, startC - radius); c <= Mathf.Min(world.Cols - 1, startC + radius); c++)
                            if (!world.IsSolid(c, r)) { startC = c; startR = r; found = true; break; }
                if (!found) return new HashSet<int>();
            }

            var result = new HashSet<int>();
            var queue = new Queue<int>();
            int start = world.Index(startC, startR);
            result.Add(start);
            queue.Enqueue(start);
            var directions = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                int c = index % world.Cols;
                int r = index / world.Cols;
                for (int i = 0; i < directions.Length; i++)
                {
                    int nc = c + directions[i].x;
                    int nr = r + directions[i].y;
                    if (!world.InBounds(nc, nr) || world.IsSolid(nc, nr)) continue;
                    int next = world.Index(nc, nr);
                    if (result.Add(next)) queue.Enqueue(next);
                }
            }
            return result;
        }

        static RectInt FindLargestOpenRectangle(WorldGrid world, HashSet<int> open)
        {
            int bestArea = 0;
            var best = new RectInt();
            var heights = new int[world.Cols];
            for (int r = 0; r < world.Rows; r++)
            {
                for (int c = 0; c < world.Cols; c++)
                    heights[c] = open.Contains(world.Index(c, r)) ? heights[c] + 1 : 0;
                for (int left = 0; left < world.Cols; left++)
                {
                    int minimumHeight = int.MaxValue;
                    for (int right = left; right < world.Cols; right++)
                    {
                        minimumHeight = Mathf.Min(minimumHeight, heights[right]);
                        if (minimumHeight == 0) break;
                        int width = right - left + 1;
                        int area = width * minimumHeight;
                        if (area <= bestArea) continue;
                        bestArea = area;
                        best = new RectInt(left, r - minimumHeight + 1, width, minimumHeight);
                    }
                }
            }
            return best;
        }

        Vector2 Cell(int x, int y) => new Vector2(_room.x + x + 0.5f, _room.y + y + 0.5f);

        void Spawn(string assetId, int x, int y, float scale = 1f)
        {
            if (_spawner.Spawn(assetId, Cell(x, y), _shadows, scale) == null)
                Debug.LogWarning($"[Primary Match] 배치 실패: {assetId}");
        }

        void BuildOreIntake()
        {
            _identity = "ore_intake";
            int north = _room.height - 1;
            Spawn("TR01-PM-BACKDROP-COLLAPSED-WALL-MACHINE", 6, north, 1.5f);
            Spawn("TR01-PM-HERO-ORE-CRUSHER", 4, north - 2, 1.55f);
            Spawn("TR01-PIL-INTACT-A", 12, north - 2, 1.1f);
            Spawn("TR01-PM-RAIL-HORIZONTAL-LEFT", 6, north - 3, 1.2f);
            Spawn("TR01-PM-RAIL-HORIZONTAL-MIDDLE", 7, north - 3, 1.2f);
            Spawn("TR01-PM-RAIL-HORIZONTAL-RIGHT", 8, north - 3, 1.2f);
            Spawn("TR01-PM-CRYSTAL-MEDIUM-LEFT-OUTCROP", 2, 2, 1.25f);
            Spawn("TR01-PM-CRYSTAL-MEDIUM-RIGHT-OUTCROP", 13, 2, 1.25f);
            Spawn("TR01-PM-FOREGROUND-RIGHT-FOREGROUND-SHELF", 13, 1, 1.35f);
        }

        void BuildVentilationService()
        {
            _identity = "ventilation_service";
            int north = _room.height - 1;
            Spawn("TR01-PM-BACKDROP-SEALED-BULKHEAD", 7, north, 1.5f);
            Spawn("TR01-PM-HERO-VENTILATION-TURBINE", 7, north - 2, 1.55f);
            Spawn("TR01-PIL-BROKEN-A", 3, north - 3, 1.05f);
            Spawn("TR01-PIL-INTACT-A", 11, north - 3, 1.05f);
            Spawn("TR01-PM-EQUIPMENT-LEFT-TERMINATION", 4, north - 3, 1.2f);
            for (int x = 5; x <= 8; x++) Spawn("TR01-PM-EQUIPMENT-BURIED-THRESHOLD", x, north - 3, 1.2f);
            Spawn("TR01-PM-EQUIPMENT-RIGHT-TERMINATION", 9, north - 3, 1.2f);
            Spawn("TR01-PM-CRYSTAL-MEDIUM-LEFT-OUTCROP", 2, 2, 1.2f);
            Spawn("TR01-PM-FOREGROUND-LEFT-FOREGROUND-SHELF", 2, 1, 1.35f);
        }

        void BuildCrystalPower()
        {
            _identity = "crystal_power";
            int north = _room.height - 1;
            Spawn("TR01-PM-BACKDROP-CRYSTAL-PROCESSOR", 7, north, 1.5f);
            Spawn("TR01-PM-HERO-POWER-RELAY", 4, north - 2, 1.55f);
            Spawn("TR01-PIL-INTACT-A", 12, north - 2, 1.05f);
            Spawn("TR01-PM-EQUIPMENT-LEFT-TERMINATION", 5, north - 3, 1.2f);
            Spawn("TR01-PM-EQUIPMENT-BURIED-THRESHOLD", 6, north - 3, 1.2f);
            Spawn("TR01-PM-EQUIPMENT-RIGHT-TERMINATION", 7, north - 3, 1.2f);
            Spawn("TR01-PM-CRYSTAL-MEDIUM-CENTER-OUTCROP", 12, 3, 1.3f);
            Spawn("TR01-PM-CRYSTAL-MEDIUM-RIGHT-OUTCROP", 13, 2, 1.25f);
            Spawn("TR01-PM-FOREGROUND-RIGHT-FOREGROUND-SHELF", 13, 1, 1.35f);
        }
    }
}
