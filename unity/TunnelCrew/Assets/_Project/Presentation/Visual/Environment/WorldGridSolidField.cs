using TunnelCrew.Sim;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 시뮬레이션 <see cref="WorldGrid"/> 를 표면 생성기 입력으로 노출한다.
    /// 생성기가 시뮬레이션 타입을 직접 참조하지 않게 하는 유일한 접점이다.
    /// </summary>
    public sealed class WorldGridSolidField : ISolidField
    {
        readonly WorldGrid _world;
        readonly System.Func<int, bool> _isBossWall;

        /// <param name="isBossWall">셀 인덱스(<c>WorldGrid.Index</c>) → 보스 소환 벽 여부. 보통 <c>BossSystem.WallCells.Contains</c>. null 이면 보스 벽 없음.</param>
        public WorldGridSolidField(WorldGrid world, System.Func<int, bool> isBossWall = null)
        {
            _world = world;
            _isBossWall = isBossWall;
        }

        public int Cols => _world.Cols;
        public int Rows => _world.Rows;

        /// <summary>범위 밖은 고체 — <see cref="WorldGrid.IsSolid"/> 규약을 그대로 쓴다.</summary>
        public bool IsSolid(int col, int row) => _world.IsSolid(col, row);

        public byte BandAt(int col, int row)
            => _world.InBounds(col, row) ? _world.BandAt(_world.Index(col, row)) : (byte)0;

        public byte SurfaceSeedAt(int col, int row)
            => _world.InBounds(col, row) ? _world.DecAt(_world.Index(col, row)) : (byte)0;

        public bool IsBossWallAt(int col, int row)
            => _isBossWall != null && _world.InBounds(col, row) && _isBossWall(_world.Index(col, row));
    }

    /// <summary>
    /// 고정 방(§12.3 Visual Lab)과 EditMode fixture 를 위한 손으로 만든 격자.
    /// 문자열 한 줄이 한 행이고, 위쪽 줄이 큰 row(화면 위)다.
    /// </summary>
    public sealed class ArraySolidField : ISolidField
    {
        readonly bool[] _solid;
        readonly byte[] _band;
        readonly byte[] _seed;
        readonly bool[] _boss;

        public int Cols { get; }
        public int Rows { get; }

        public ArraySolidField(int cols, int rows)
        {
            Cols = cols; Rows = rows;
            _solid = new bool[cols * rows];
            _band = new byte[cols * rows];
            _seed = new byte[cols * rows];
            _boss = new bool[cols * rows];
        }

        /// <summary>
        /// 문자로 격자를 만든다. <c>'#'</c> 는 고체, 그 밖은 빈칸이다.
        /// <paramref name="rowsTopFirst"/> 의 첫 줄이 가장 위(row = Rows-1)다.
        /// </summary>
        public static ArraySolidField Parse(params string[] rowsTopFirst)
        {
            int rows = rowsTopFirst.Length;
            int cols = 0;
            foreach (var s in rowsTopFirst) if (s.Length > cols) cols = s.Length;

            var f = new ArraySolidField(cols, rows);
            for (int i = 0; i < rows; i++)
            {
                int r = rows - 1 - i;
                var line = rowsTopFirst[i];
                for (int c = 0; c < cols; c++)
                    f.SetSolid(c, r, c < line.Length && line[c] == '#');
            }
            return f;
        }

        public void SetSolid(int col, int row, bool solid)
        {
            if (!In(col, row)) return;
            _solid[row * Cols + col] = solid;
        }

        public void SetBand(int col, int row, byte band)
        {
            if (In(col, row)) _band[row * Cols + col] = band;
        }

        public void SetSeed(int col, int row, byte seed)
        {
            if (In(col, row)) _seed[row * Cols + col] = seed;
        }

        /// <summary>보스 소환 벽 표시. 고체가 아니면 표면 생성기가 무시한다.</summary>
        public void SetBossWall(int col, int row, bool boss)
        {
            if (In(col, row)) _boss[row * Cols + col] = boss;
        }

        bool In(int c, int r) => c >= 0 && r >= 0 && c < Cols && r < Rows;

        public bool IsSolid(int col, int row) => !In(col, row) || _solid[row * Cols + col];
        public byte BandAt(int col, int row) => In(col, row) ? _band[row * Cols + col] : (byte)0;
        public byte SurfaceSeedAt(int col, int row) => In(col, row) ? _seed[row * Cols + col] : (byte)0;
        public bool IsBossWallAt(int col, int row) => In(col, row) && _boss[row * Cols + col];
    }
}
