using System.Collections.Generic;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 벽 덩어리의 <b>외곽선</b>을 셀 경계 격자에서 추적한다(기능명세서 §7.4
    /// "벽 그림자 캐스터는 셀 사각형이 아니라 생성된 벽·기둥·문 footprint 윤곽에서 만든다").
    ///
    /// <b>순수 함수만 둔다.</b> UnityEngine 타입을 참조하지 않으므로 EditMode 에서 손으로 만든
    /// 격자로 검증할 수 있고, 파괴 전후 윤곽 변화도 테스트로 고정할 수 있다.
    ///
    /// <b>알고리즘</b> — 경계 변 추적. 벽 셀의 네 변 중 이웃이 벽이 아닌 변만 유향 변으로
    /// 만들고, 머리-꼬리를 이어 닫힌 고리를 얻는다. 벽 셀과 벽 셀 사이의 <b>안쪽 경계는
    /// 애초에 만들어지지 않는다</b> — 기존 <c>WallShadowBuilder</c> 의 탐욕적 사각형 분할이
    /// 인접 사각형 사이에 남기던 이음새 변이 여기서는 존재하지 않는다.
    ///
    /// 방향은 모두 "벽이 진행 방향의 왼쪽"으로 맞춘다. 변의 방향 번호가 셀의 변 번호와
    /// 같아지도록 골랐다: S=+X, E=+Y, N=-X, W=-Y.
    ///
    /// <b>정점에서 두 갈래가 나올 때</b>(대각으로만 닿은 두 벽이 한 점을 공유할 때)는
    /// 왼쪽 회전을 먼저 고른다. 벽이 왼쪽에 있는 방향 규약에서 왼쪽 회전이 같은 고리에
    /// 머무는 선택이다.
    /// </summary>
    public static class WallContourTracer
    {
        /// <summary>셀의 남쪽 변. 진행 방향 +X.</summary>
        public const int SideSouth = 0;
        /// <summary>셀의 동쪽 변. 진행 방향 +Y.</summary>
        public const int SideEast = 1;
        /// <summary>셀의 북쪽 변. 진행 방향 -X.</summary>
        public const int SideNorth = 2;
        /// <summary>셀의 서쪽 변. 진행 방향 -Y.</summary>
        public const int SideWest = 3;

        /// <summary>정수 셀 모서리 좌표. 실수 오차 없이 고리를 닫기 위해 정수로 다룬다.</summary>
        public readonly struct Corner
        {
            public readonly int X, Y;
            public Corner(int x, int y) { X = x; Y = y; }
            public bool Equals(Corner o) => X == o.X && Y == o.Y;
            public override string ToString() => $"({X},{Y})";
        }

        /// <summary>
        /// 벽 여부를 돌려주는 판정자. 범위 밖은 <b>벽이 아니다</b> —
        /// 그래야 맵 가장자리에서도 고리가 닫힌다.
        /// </summary>
        public delegate bool WallAt(int col, int row);

        /// <summary>
        /// 닫힌 외곽선 하나. 좌표는 시뮬레이션 셀 단위의 정수 모서리다.
        /// </summary>
        public sealed class Contour
        {
            public readonly List<Corner> Points = new List<Corner>();

            /// <summary>이 고리를 시작한 셀과 변. 추적 순서를 재현 가능하게 만드는 값이다.</summary>
            public int StartCol, StartRow, StartSide;

            /// <summary>
            /// 슈레이스 부호 면적 × 2. 벽이 왼쪽인 규약에서 양수면 벽 덩어리의 바깥 고리,
            /// 음수면 벽에 둘러싸인 빈 공간(방·통로)의 고리다.
            /// </summary>
            public long DoubleSignedArea;

            public bool IsOuter => DoubleSignedArea > 0;

            /// <summary>같은 윤곽인지 값으로 비교하는 해시. 캐스터 재생성 여부를 판단한다.</summary>
            public int ContentHash;
        }

        // ───────────────────────────── 추적

        /// <summary>
        /// <paramref name="isWall"/> 가 참인 셀들의 모든 외곽선을 만든다.
        ///
        /// 결과 순서는 셀을 row-major 로, 변을 S→E→N→W 로 훑는 순서에 고정된다.
        /// 같은 입력이면 고리의 개수·순서·정점 순서·시작 정점까지 모두 같다.
        /// </summary>
        public static void Trace(WallAt isWall, int cols, int rows, List<Contour> into,
            bool simplifyCollinear = true)
        {
            into.Clear();
            if (isWall == null || cols <= 0 || rows <= 0) return;

            // (셀, 변) 하나가 변 하나다. 소비 여부만 기억하면 되므로 사전이 필요 없다.
            var consumed = new bool[cols * rows * 4];

            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    if (!isWall(c, r)) continue;
                    for (int side = 0; side < 4; side++)
                    {
                        int e = (r * cols + c) * 4 + side;
                        if (consumed[e] || !EdgeExists(isWall, c, r, side)) continue;

                        var contour = TraceLoop(isWall, cols, rows, consumed, c, r, side);
                        if (contour == null) continue;
                        if (simplifyCollinear) SimplifyCollinear(contour.Points);
                        contour.DoubleSignedArea = DoubleSignedArea(contour.Points);
                        contour.ContentHash = HashOf(contour);
                        if (contour.Points.Count >= 3) into.Add(contour);
                    }
                }
        }

        static Contour TraceLoop(WallAt isWall, int cols, int rows, bool[] consumed,
            int startCol, int startRow, int startSide)
        {
            var contour = new Contour
            {
                StartCol = startCol,
                StartRow = startRow,
                StartSide = startSide,
            };

            int c = startCol, r = startRow, side = startSide;
            // 고리 하나가 격자의 모든 변을 다 쓸 수는 없지만, 잘못된 입력에서 무한 루프에
            // 빠지지 않도록 상한을 둔다.
            int guard = cols * rows * 4 + 4;

            while (guard-- > 0)
            {
                consumed[(r * cols + c) * 4 + side] = true;
                contour.Points.Add(StartCorner(c, r, side));

                var head = EndCorner(c, r, side);

                // 왼쪽 → 직진 → 오른쪽 → 되돌기 순으로 이어 붙일 변을 찾는다.
                // 왼쪽 우선이 대각 접점에서 같은 고리에 머무는 선택이다.
                bool advanced = false;
                for (int t = 0; t < 4 && !advanced; t++)
                {
                    int next = t switch
                    {
                        0 => (side + 1) & 3,   // 왼쪽
                        1 => side,             // 직진
                        2 => (side + 3) & 3,   // 오른쪽
                        _ => (side + 2) & 3,   // 되돌기
                    };

                    if (!CandidateAt(head, next, cols, rows, out int nc, out int nr)) continue;
                    if (!EdgeExists(isWall, nc, nr, next)) continue;
                    if (consumed[(nr * cols + nc) * 4 + next]) continue;

                    c = nc; r = nr; side = next;
                    advanced = true;
                }

                if (advanced) continue;

                // 더 이을 변이 없다. 머리가 시작 정점으로 돌아왔으면 닫힌 고리다.
                var first = StartCorner(startCol, startRow, startSide);
                return head.Equals(first) ? contour : null;
            }

            return null;
        }

        /// <summary>이 (셀, 변) 이 경계 변인가 — 셀이 벽이고 그 방향 이웃이 벽이 아니어야 한다.</summary>
        public static bool EdgeExists(WallAt isWall, int c, int r, int side)
        {
            if (!isWall(c, r)) return false;
            return side switch
            {
                SideSouth => !isWall(c, r - 1),
                SideEast => !isWall(c + 1, r),
                SideNorth => !isWall(c, r + 1),
                _ => !isWall(c - 1, r),
            };
        }

        /// <summary>변의 꼬리 정점.</summary>
        public static Corner StartCorner(int c, int r, int side) => side switch
        {
            SideSouth => new Corner(c, r),
            SideEast => new Corner(c + 1, r),
            SideNorth => new Corner(c + 1, r + 1),
            _ => new Corner(c, r + 1),
        };

        /// <summary>변의 머리 정점.</summary>
        public static Corner EndCorner(int c, int r, int side) => side switch
        {
            SideSouth => new Corner(c + 1, r),
            SideEast => new Corner(c + 1, r + 1),
            SideNorth => new Corner(c, r + 1),
            _ => new Corner(c, r),
        };

        /// <summary>
        /// 이 정점에서 이 방향으로 나가는 변은 어느 (셀, 변) 인가. 정점과 방향이 정해지면
        /// 후보 셀이 하나로 정해지므로 탐색이 필요 없다.
        /// </summary>
        static bool CandidateAt(Corner v, int side, int cols, int rows, out int c, out int r)
        {
            switch (side)
            {
                case SideSouth: c = v.X; r = v.Y; break;
                case SideEast: c = v.X - 1; r = v.Y; break;
                case SideNorth: c = v.X - 1; r = v.Y - 1; break;
                default: c = v.X; r = v.Y - 1; break;
            }
            return c >= 0 && r >= 0 && c < cols && r < rows;
        }

        // ───────────────────────────── 정리

        /// <summary>일직선으로 이어지는 정점을 없앤다. 긴 벽 한 면이 정점 2개가 된다.</summary>
        public static void SimplifyCollinear(List<Corner> pts)
        {
            if (pts.Count < 3) return;

            // 고리이므로 마지막-처음-두번째도 함께 본다. 뒤에서 앞으로 지운다.
            for (int i = pts.Count - 1; i >= 0; i--)
            {
                if (pts.Count < 3) break;
                var prev = pts[(i - 1 + pts.Count) % pts.Count];
                var cur = pts[i];
                var next = pts[(i + 1) % pts.Count];

                long ax = cur.X - prev.X, ay = cur.Y - prev.Y;
                long bx = next.X - cur.X, by = next.Y - cur.Y;
                if (ax * by - ay * bx == 0) pts.RemoveAt(i);
            }
        }

        /// <summary>슈레이스 부호 면적 × 2. 정수 좌표라 오차가 없다.</summary>
        public static long DoubleSignedArea(List<Corner> pts)
        {
            long acc = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % pts.Count];
                acc += (long)a.X * b.Y - (long)b.X * a.Y;
            }
            return acc;
        }

        /// <summary>
        /// 윤곽의 내용 해시. 정점 좌표만으로 만들며 시각·주소·프레임에 의존하지 않는다.
        /// 기존 <c>WallShadowBuilder.ApplyShape</c> 는 <c>Environment.TickCount</c> 를 섞어
        /// 같은 형태에서도 매번 다른 값이 나왔다 — 그러면 바뀐 캐스터만 다시 만들 수 없다.
        /// </summary>
        public static int HashOf(Contour contour)
        {
            unchecked
            {
                uint h = 2166136261u;
                var pts = contour.Points;
                for (int i = 0; i < pts.Count; i++)
                {
                    h = (h ^ (uint)pts[i].X) * 16777619u;
                    h = (h ^ (uint)pts[i].Y) * 16777619u;
                }
                h = (h ^ (uint)pts.Count) * 16777619u;
                return (int)(h & 0x7FFFFFFF);
            }
        }
    }
}
