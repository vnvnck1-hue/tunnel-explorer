namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 기능명세서 §6.3 — 셀을 한 장씩 그리지 않고 이웃 관계를 읽어 시각 표면을 만든다.
    ///
    /// <b>순수 함수만 둔다.</b> UnityEngine 타입도 시뮬레이션 타입도 참조하지 않으므로
    /// EditMode 에서 손으로 만든 격자로 직선·코너·섬·좁은 통로를 검증할 수 있다(§16.1).
    /// 결정성도 여기서 보장한다 — 입력이 같으면 모듈 번호까지 같다.
    ///
    /// 방위는 화면 기준이다: North = row+1(화면 위), South = row-1(화면 아래).
    /// </summary>
    public static class SurfaceTopologyBuilder
    {
        /// <summary>§6.3 — 청크 기본 크기. 파괴 시 이 단위와 인접 경계만 dirty 처리한다.</summary>
        public const int ChunkSize = 16;

        /// <summary>격자 전체의 표면을 만든다. <paramref name="into"/> 는 Cols*Rows 이상이어야 한다.</summary>
        public static void Build(ISolidField field, in SurfaceRules rules, CellSurface[] into)
        {
            BuildRegion(field, rules, 0, 0, field.Cols, field.Rows, into);
        }

        /// <summary>
        /// 사각 영역만 다시 만든다. 인덱스는 <c>row * field.Cols + col</c> 로 전체 격자 기준이다.
        /// 경계 셀의 표면은 영역 밖 이웃도 읽어 결정하므로, 파괴 시에는 dirty 청크를
        /// 1셀 넓혀 호출하면 이음새가 남지 않는다(§6.7).
        /// </summary>
        public static void BuildRegion(ISolidField field, in SurfaceRules rules,
            int col0, int row0, int width, int height, CellSurface[] into)
        {
            if (field == null || into == null) return;

            int cols = field.Cols, rows = field.Rows;
            if (col0 < 0) { width += col0; col0 = 0; }
            if (row0 < 0) { height += row0; row0 = 0; }
            if (col0 + width > cols) width = cols - col0;
            if (row0 + height > rows) height = rows - row0;
            if (width <= 0 || height <= 0) return;

            byte liftQ = LiftQ(rules);
            for (int r = row0; r < row0 + height; r++)
                for (int c = col0; c < col0 + width; c++)
                    into[r * cols + c] = Cell(field, rules, c, r, liftQ);
        }

        /// <summary>셀 하나의 표면. 이웃 8칸만 읽으므로 어느 순서로 불러도 결과가 같다.</summary>
        public static CellSurface Cell(ISolidField field, in SurfaceRules rules, int c, int r)
            => Cell(field, rules, c, r, LiftQ(rules));

        /// <summary>§8.6 범위로 자른 cap lift 를 1/16셀 단위로.</summary>
        static byte LiftQ(in SurfaceRules rules)
        {
            float lift = rules.WallLiftCells;
            if (lift < SurfaceRules.MinLiftCells) lift = SurfaceRules.MinLiftCells;
            if (lift > SurfaceRules.MaxLiftCells) lift = SurfaceRules.MaxLiftCells;
            return (byte)(int)(lift * 16f + 0.5f);
        }

        static CellSurface Cell(ISolidField field, in SurfaceRules rules, int c, int r, byte liftQ)
        {
            var s = new CellSurface
            {
                Band = field.BandAt(c, r),
                HeightQ = liftQ,
            };

            bool self = field.IsSolid(c, r);
            bool n = field.IsSolid(c, r + 1);
            bool so = field.IsSolid(c, r - 1);
            bool w = field.IsSolid(c - 1, r);
            bool e = field.IsSolid(c + 1, r);

            byte seed = field.SurfaceSeedAt(c, r);

            if (!self)
            {
                s.Surfaces |= SurfaceMask.FloorBase;
                if (n || so || w || e
                    || field.IsSolid(c - 1, r - 1) || field.IsSolid(c + 1, r - 1)
                    || field.IsSolid(c - 1, r + 1) || field.IsSolid(c + 1, r + 1))
                    s.Surfaces |= SurfaceMask.FloorEdge;

                // 벽에 붙은 바닥에 접촉 AO 를 깐다. 광원이 꺼져도 바닥과 벽의 경계가
                // 읽혀야 한다(§8.5 마지막 항, §15.1).
                //
                // 2026-09-10: 북쪽 한 방향만 깔던 것을 <b>네 방향</b>으로 넓혔다. 아트가 n/e/s/w
                // 네 장을 납품했는데(manifest r19) 북쪽만 소비하고 있었다. 한 셀이 두 방향에서
                // 벽을 만나면 층을 더 쌓지 않고 우선순위로 하나만 고른다 —
                // 북(가장 크게 보이는 벽면) > 남 > 동 > 서.
                if (n || so || e || w)
                {
                    s.Surfaces |= SurfaceMask.ContactAo;
                    s.AoDir = n ? (byte)0 : so ? (byte)2 : e ? (byte)1 : (byte)3;
                }

                s.FloorModule = Pick(rules.FloorVariants, MacroHash(c, r, rules.FloorMacroCells, rules.Salt), seed);
                s.HeightQ = 0;
                return s;
            }

            bool sw = field.IsSolid(c - 1, r - 1);
            bool se = field.IsSolid(c + 1, r - 1);
            bool nw = field.IsSolid(c - 1, r + 1);
            bool ne = field.IsSolid(c + 1, r + 1);

            // ── 고체 셀
            if (n && so && w && e && sw && se && nw && ne)
            {
                // 여덟 이웃이 모두 막힌 벽 내부 → 컬링.
                //
                // 직교 넷만 보고 자르면 안 된다. 대각 하나가 빈칸이면 그 오목 모서리에서
                // cap 의 귀퉁이가 실제로 드러나므로, 컬링했을 때 한 조각짜리 구멍이 남는다.
                s.Surfaces |= SurfaceMask.Buried;
                return s;
            }

            s.Surfaces |= SurfaceMask.WallTop;
            // 보스 소환 벽 — cap·정면 아트를 갈아 끼우는 표시. 이웃이 아니라 자기 셀만 본다.
            if (field.IsBossWallAt(c, r)) s.Surfaces |= SurfaceMask.BossWall;

            // 북쪽이 열려 있으면 올려 그린 cap 이 그 바닥의 캐릭터와 겹친다 →
            // 전경 오클루더. 남쪽이 열려 있으면 남쪽 방에서 보이는 벽 정면이다.
            if (!n) s.Surfaces |= SurfaceMask.ForegroundTop | SurfaceMask.TopRim;
            if (!so) s.Surfaces |= SurfaceMask.FrontFace;
            if (!w) s.Surfaces |= SurfaceMask.WestSide;
            if (!e) s.Surfaces |= SurfaceMask.EastSide;
            s.Surfaces |= SurfaceMask.ShadowEdge;

            // 볼록 모서리 — 두 직교 이웃이 모두 빈칸
            if (!so && !w) s.Corners |= CornerMask.OuterSW;
            if (!so && !e) s.Corners |= CornerMask.OuterSE;
            if (!n && !w) s.Corners |= CornerMask.OuterNW;
            if (!n && !e) s.Corners |= CornerMask.OuterNE;

            // 오목 모서리 — 두 직교 이웃은 고체인데 대각만 빈칸
            if (so && w && !sw) s.Corners |= CornerMask.InnerSW;
            if (so && e && !se) s.Corners |= CornerMask.InnerSE;
            if (n && w && !nw) s.Corners |= CornerMask.InnerNW;
            if (n && e && !ne) s.Corners |= CornerMask.InnerNE;

            // 벽 상단·정면도 구역 단위로 뽑는다(인계서 §4-3). 셀별 해시로 뽑으면
            // 변형 6종이 화면 전체에 고르게 흩어져 매크로 패턴이 생기지 않는다.
            // 상단과 정면은 서로 다른 소금을 써서 같은 구역에서 같은 인덱스가 겹치지 않게 한다.
            uint h = MacroHash(c, r, rules.WallMacroCells, rules.Salt);
            s.TopModule = Pick(rules.TopVariants, h, seed);
            s.FrontModule = Pick(rules.FrontVariants,
                MacroHash(c, r, rules.WallMacroCells, rules.Salt ^ 0x5F35) ^ 0x9E3779B9u, seed);
            s.CornerModule = Pick(rules.CornerVariants, h ^ 0x85EBCA6Bu, seed);
            return s;
        }

        // ───────────────────────────── 결정적 선택

        /// <summary>셀 고정 해시. 기존 타일 변형 해시와 같은 계열(Knuth 승수)로 맞춘다.</summary>
        public static uint Hash(int col, int row, int salt)
        {
            unchecked
            {
                uint x = (uint)(col * 73856093) ^ (uint)(row * 19349663) ^ (uint)(salt * 83492791);
                x ^= x >> 16; x *= 2654435761u;
                x ^= x >> 13; x *= 2246822519u;
                x ^= x >> 16;
                return x;
            }
        }

        /// <summary>
        /// 매크로 변형용 해시 — 셀이 아니라 <paramref name="macro"/>×<paramref name="macro"/>
        /// 블록 단위로 같은 값을 준다. 셀마다 무늬가 튀지 않고 큰 형태가 먼저 읽히게 한다(§8.5).
        /// </summary>
        public static uint MacroHash(int col, int row, int macro, int salt)
        {
            if (macro < 1) macro = 1;
            return Hash(FloorDiv(col, macro), FloorDiv(row, macro), salt);
        }

        /// <summary>음수에서도 블록 경계가 어긋나지 않는 내림 나눗셈.</summary>
        static int FloorDiv(int a, int b) => a >= 0 ? a / b : -(((-a) + b - 1) / b);

        static byte Pick(byte variants, uint hash, byte seed)
        {
            if (variants <= 1) return 0;
            unchecked { return (byte)((hash ^ ((uint)seed * 2654435761u)) % variants); }
        }

        // ───────────────────────────── 청크

        public static int ChunkCols(int cols) => (cols + ChunkSize - 1) / ChunkSize;
        public static int ChunkRows(int rows) => (rows + ChunkSize - 1) / ChunkSize;

        /// <summary>셀이 속한 청크 번호.</summary>
        public static int ChunkIndex(int col, int row, int cols)
            => (row / ChunkSize) * ChunkCols(cols) + (col / ChunkSize);

        /// <summary>
        /// 셀 하나가 바뀔 때 다시 만들어야 하는 청크들. 셀이 청크 경계에 있으면
        /// 이웃 청크의 표면 선택도 바뀌므로 함께 dirty 처리한다(§6.7).
        /// </summary>
        public static void DirtyChunks(int col, int row, int cols, int rows,
            System.Collections.Generic.ICollection<int> into)
        {
            int cc = ChunkCols(cols), cr = ChunkRows(rows);
            int c0 = System.Math.Max(0, col - 1) / ChunkSize;
            int c1 = System.Math.Min(cols - 1, col + 1) / ChunkSize;
            int r0 = System.Math.Max(0, row - 1) / ChunkSize;
            int r1 = System.Math.Min(rows - 1, row + 1) / ChunkSize;
            for (int r = r0; r <= r1 && r < cr; r++)
                for (int c = c0; c <= c1 && c < cc; c++)
                    into.Add(r * cc + c);
        }
    }
}
