using System;
using System.Collections.Generic;
using System.Linq;

namespace TunnelCrew.Sim
{
    /// <summary>
    /// 원본 <c>genTunnel(d)</c> (v7.9.2, 5645~6004행) 의 11단계 파이프라인을 그대로 옮긴 것.
    ///
    /// **이 파일은 패리티 계약이다.** `Assets/Tests/EditMode/Fixtures/` 의 픽스처와
    /// 셀 배열이 한 칸도 어긋나면 안 된다. 난수 소비 순서까지 원본과 같아야 하므로,
    /// 겉보기에 불필요해 보이는 draw 나 순서도 건드리지 말 것.
    ///
    /// 원본과 의도적으로 다른 한 가지:
    ///   원본은 진입 방 선택에 `Math.random()` 을 써서 같은 시드라도 맵이 달라졌다.
    ///   여기서는 시드 스트림을 쓴다. 픽스처 덤퍼(dump-mapgen-fixture.mjs)도 같은 치환을
    ///   적용하므로 대조가 성립한다. 이렇게 해야 리플레이·코옵·회귀 테스트가 가능하다.
    /// </summary>
    public sealed class DungeonGenerator
    {
        static readonly int[][] D4 = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };

        readonly DungeonConfig _cfg;
        int W, H, N;
        Rng _rng;

        /// <summary>계산 중에는 원본처럼 숫자로 다룬다. 0 = 빈칸, 1 = 흙, 2 = 암반.</summary>
        byte[] _g;

        public DungeonGenerator(DungeonConfig cfg) => _cfg = cfg;

        int Idx(int x, int y) => y * W + x;
        bool Inb(int x, int y) => x >= 1 && y >= 1 && x < W - 1 && y < H - 1;
        void Set(int x, int y, byte v) { if (Inb(x, y)) _g[Idx(x, y)] = v; }
        double R() => _rng.NextDouble();

        public DungeonResult Generate(int depth)
        {
            W = _cfg.Cols;
            H = _cfg.Rows;
            N = W * H;
            _rng = Rng.ForTunnel(_cfg.Seed, depth, W, H);
            _g = new byte[N];
            for (int i = 0; i < N; i++) _g[i] = 1;

            int roomTarget = _cfg.RoomTarget;

            var rooms = PlaceRooms(roomTarget);        // 1
            CarveRooms(rooms);                          // 2
            var edges = BuildGraph(rooms);              // 3
            CarveCorridors(rooms, edges);               // 4
            CellularAutomata();                         // 5
            var (ex0, er0) = EnsureConnectivity(rooms); // 6
            var dist = DistanceField(ex0, er0);         // 7
            PlaceCore(ex0, er0);                        // 8

            var result = new DungeonResult
            {
                Depth = depth,
                Cols = W,
                Rows = H,
                RoomCount = rooms.Count,
                EntryCol = ex0,
                EntryRow = er0,
            };

            var tiles = FinalizeTiles(dist, result);    // 9
            int exit = PlaceExit(tiles, dist, ex0, er0, out var open); // 10
            PlacePoi(tiles, open, exit, result);        // 유물·보급품·랜턴·소품
            ApplyEnsurePath(tiles, ex0, er0);           // 11
            ApplyPresentationEntry(tiles, ex0, er0, result); // Unity 본편 전용 · RNG 이후

            result.Tiles = tiles;
            result.Exit = exit;
            result.ExitOpen = false;
            result.MaxDist = dist.Length == 0 ? 0 : dist.Max();
            result.Interior = (W - 2) * (H - 2);
            return result;
        }

        // ───────────────────────────────────────────────── 1) 방 배치
        sealed class Room
        {
            public int X, Y, W, H, Cx, Cy;
            public bool Blob;
        }

        List<Room> PlaceRooms(int roomTarget)
        {
            var rooms = new List<Room>();
            int tries = roomTarget * 40;
            for (int a = 0; a < tries && rooms.Count < roomTarget; a++)
            {
                int w = _cfg.RoomMin + (int)(R() * (_cfg.RoomMax - _cfg.RoomMin + 1));
                int h = _cfg.RoomMin + (int)(R() * (_cfg.RoomMax - _cfg.RoomMin + 1));
                int x = 1 + (int)(R() * (W - 2 - w));
                int y = 1 + (int)(R() * (H - 2 - h));
                if (x < 1 || y < 1 || x + w > W - 1 || y + h > H - 1) continue;

                bool clash = false;
                foreach (var r in rooms)
                {
                    if (x - 2 < r.X + r.W && x + w + 2 > r.X && y - 2 < r.Y + r.H && y + h + 2 > r.Y)
                    { clash = true; break; }
                }
                if (clash) continue;

                rooms.Add(new Room
                {
                    X = x, Y = y, W = w, H = h,
                    Cx = (int)Math.Floor(x + w / 2.0),
                    Cy = (int)Math.Floor(y + h / 2.0),
                    Blob = R() * 100 < _cfg.BlobPercent,
                });
            }
            return rooms;
        }

        // ───────────────────────────────────────────────── 2) 방 파내기
        void CarveRooms(List<Room> rooms)
        {
            foreach (var r in rooms)
            {
                if (!r.Blob)
                {
                    for (int y = r.Y; y < r.Y + r.H; y++)
                        for (int x = r.X; x < r.X + r.W; x++)
                            Set(x, y, 0);
                }
                else
                {
                    double rx = r.W / 2.0, ry = r.H / 2.0;
                    for (int y = r.Y; y < r.Y + r.H; y++)
                        for (int x = r.X; x < r.X + r.W; x++)
                        {
                            double u = (x + .5 - (r.X + rx)) / rx;
                            double v = (y + .5 - (r.Y + ry)) / ry;
                            if (u * u + v * v <= 1.0 + (R() - .5) * .45) Set(x, y, 0);
                        }
                    int wx = r.Cx, wy = r.Cy;
                    int steps = JsMath.Round(r.W * r.H * .35);
                    for (int t = 0; t < steps; t++)
                    {
                        Set(wx, wy, 0);
                        if (R() < .5) wx += R() < .5 ? 1 : -1;
                        else wy += R() < .5 ? 1 : -1;
                        wx = Math.Max(r.X - 1, Math.Min(r.X + r.W, wx));
                        wy = Math.Max(r.Y - 1, Math.Min(r.Y + r.H, wy));
                    }
                }
            }
        }

        // ───────────────────────────────────────────────── 3) Prim MST + 우회 간선
        List<(int a, int b)> BuildGraph(List<Room> rooms)
        {
            var edges = new List<(int a, int b)>();
            if (rooms.Count <= 1) return edges;

            double D2(int a, int b)
            {
                double dx = rooms[a].Cx - rooms[b].Cx, dy = rooms[a].Cy - rooms[b].Cy;
                return dx * dx + dy * dy;
            }

            var inT = new List<int> { 0 };
            var outList = Enumerable.Range(1, rooms.Count - 1).ToList();
            while (outList.Count > 0)
            {
                int bi = 0, bj = 0;
                double bd = double.PositiveInfinity;
                foreach (int i in inT)
                    for (int k = 0; k < outList.Count; k++)
                    {
                        double dd = D2(i, outList[k]);
                        if (dd < bd) { bd = dd; bi = i; bj = k; }
                    }
                edges.Add((bi, outList[bj]));
                inT.Add(outList[bj]);
                outList.RemoveAt(bj);
            }

            var cand = new List<(int i, int j, double d)>();
            for (int i = 0; i < rooms.Count; i++)
                for (int j = i + 1; j < rooms.Count; j++)
                {
                    bool exists = false;
                    foreach (var e in edges)
                        if ((e.a == i && e.b == j) || (e.a == j && e.b == i)) { exists = true; break; }
                    if (exists) continue;
                    cand.Add((i, j, D2(i, j)));
                }

            // JS Array.sort 는 안정 정렬이다. LINQ OrderBy 도 안정 정렬이라 순서가 같다.
            // List.Sort 는 불안정하므로 쓰면 안 된다 (같은 거리에서 순서가 갈린다).
            var sorted = cand.OrderBy(c => c.d).ToList();
            int nLoop = JsMath.Round(rooms.Count * _cfg.LoopPercent / 100.0);
            for (int k = 0; k < Math.Min(nLoop, sorted.Count); k++)
                edges.Add((sorted[k].i, sorted[k].j));

            return edges;
        }

        // ───────────────────────────────────────────────── 4) 통로 파내기
        void Carve(int x, int y, int w)
        {
            int rr2 = w / 2;
            for (int dy = -rr2; dy <= rr2; dy++)
                for (int dx = -rr2; dx <= rr2; dx++)
                {
                    if (w == 2 && (dx == -1 || dy == -1)) continue;
                    Set(x + dx, y + dy, 0);
                }
        }

        void CarveCorridors(List<Room> rooms, List<(int a, int b)> edges)
        {
            foreach (var (a, b) in edges)
            {
                int x = rooms[a].Cx, y = rooms[a].Cy;
                int tx = rooms[b].Cx, ty = rooms[b].Cy;
                int guard = 0;
                while ((x != tx || y != ty) && guard++ < N)
                {
                    Carve(x, y, _cfg.CorridorWidth);
                    if (R() < _cfg.JitterPercent / 100.0)
                    {
                        if (R() < .5) x += R() < .5 ? 1 : -1;
                        else y += R() < .5 ? 1 : -1;
                        x = Math.Max(1, Math.Min(W - 2, x));
                        y = Math.Max(1, Math.Min(H - 2, y));
                        continue;
                    }
                    int dx = Math.Sign(tx - x), dy = Math.Sign(ty - y);
                    if (dx != 0 && dy != 0) { if (R() < .5) x += dx; else y += dy; }
                    else if (dx != 0) x += dx;
                    else if (dy != 0) y += dy;
                }
                Carve(tx, ty, _cfg.CorridorWidth);
            }
        }

        // ───────────────────────────────────────────────── 5) 세포 자동자
        void CellularAutomata()
        {
            for (int p = 0; p < _cfg.CaPasses; p++)
            {
                var nx = (byte[])_g.Clone();
                int birth = _cfg.CaBirth, survive = _cfg.CaSurvive;
                for (int y = 1; y < H - 1; y++)
                    for (int x = 1; x < W - 1; x++)
                    {
                        int e = 0;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dy == 0) continue;
                                if (_g[Idx(x + dx, y + dy)] == 0) e++;
                            }
                        int k = Idx(x, y);
                        if (_g[k] == 0) { if (e <= Math.Max(1, survive - 3)) nx[k] = 1; }
                        else if (e >= birth + 2) nx[k] = 0;
                    }
                _g = nx;
            }
        }

        // ───────────────────────────────────────────────── 6) 연결성 보증
        Int16[] _region;

        List<int> Flood()
        {
            _region ??= new short[N];
            for (int i = 0; i < N; i++) _region[i] = -1;
            short id = 0;
            var sizes = new List<int>();
            for (int y = 1; y < H - 1; y++)
                for (int x = 1; x < W - 1; x++)
                {
                    int k = Idx(x, y);
                    if (_g[k] != 0 || _region[k] >= 0) continue;
                    // 원본은 q.pop() 을 쓰므로 스택(DFS)이다. 결과는 같지만 그대로 옮긴다.
                    var q = new List<int> { k };
                    _region[k] = id;
                    int n = 0;
                    while (q.Count > 0)
                    {
                        int c = q[q.Count - 1];
                        q.RemoveAt(q.Count - 1);
                        int cx0 = c % W, cy0 = (c - cx0) / W;
                        n++;
                        for (int dd = 0; dd < 4; dd++)
                        {
                            int x1 = cx0 + D4[dd][0], y1 = cy0 + D4[dd][1];
                            if (x1 < 1 || y1 < 1 || x1 >= W - 1 || y1 >= H - 1) continue;
                            int k1 = y1 * W + x1;
                            if (_g[k1] != 0 || _region[k1] >= 0) continue;
                            _region[k1] = id;
                            q.Add(k1);
                        }
                    }
                    sizes.Add(n);
                    id++;
                }
            return sizes;
        }

        (int ex0, int er0) EnsureConnectivity(List<Room> rooms)
        {
            // 원본은 여기서 Math.random() 을 썼다. 시드 스트림으로 바꾼다(클래스 주석 참조).
            Room entryRoom = rooms.Count > 0 ? rooms[(int)(R() * rooms.Count)] : null;
            int ex0 = Math.Max(2, Math.Min(W - 3,
                entryRoom != null ? entryRoom.Cx : 2 + (int)(R() * Math.Max(1, W - 4))));
            int er0 = Math.Max(2, Math.Min(H - 3,
                entryRoom != null ? entryRoom.Cy : 2 + (int)(R() * Math.Max(1, H - 4))));
            Set(ex0, er0, 0);

            var sizes = Flood();
            short mainId = _region[Idx(ex0, er0)];

            for (int pass = 0; pass < 24; pass++)
            {
                int target = -1, tk = -1;
                double bd = double.PositiveInfinity;
                for (int y = 1; y < H - 1; y++)
                    for (int x = 1; x < W - 1; x++)
                    {
                        int k = Idx(x, y);
                        if (_g[k] != 0 || _region[k] == mainId || _region[k] < 0) continue;
                        if (sizes[_region[k]] < 5) { _g[k] = 1; continue; }
                        double dd = (x - ex0) * (x - ex0) + (y - er0) * (y - er0);
                        if (dd < bd) { bd = dd; target = _region[k]; tk = k; }
                    }
                if (target < 0) break;

                int txx = tk % W, tyy = (tk - txx) / W;
                int bx = ex0, by = er0;
                double bdd = double.PositiveInfinity;
                for (int y = 1; y < H - 1; y++)
                    for (int x = 1; x < W - 1; x++)
                    {
                        int k = Idx(x, y);
                        if (_g[k] != 0 || _region[k] != mainId) continue;
                        double dd = (x - txx) * (x - txx) + (y - tyy) * (y - tyy);
                        if (dd < bdd) { bdd = dd; bx = x; by = y; }
                    }

                int cxp = bx, cyp = by, guard = 0;
                while ((cxp != txx || cyp != tyy) && guard++ < N)
                {
                    Carve(cxp, cyp, 1);
                    if (cxp != txx) cxp += Math.Sign(txx - cxp);
                    else cyp += Math.Sign(tyy - cyp);
                }
                sizes = Flood();
                mainId = _region[Idx(ex0, er0)];
            }
            Flood();
            return (ex0, er0);
        }

        // ───────────────────────────────────────────────── 7) 거리장 (BFS)
        short[] DistanceField(int ex0, int er0)
        {
            var dist = new short[N];
            for (int i = 0; i < N; i++) dist[i] = -1;
            var q = new List<int> { Idx(ex0, er0) };
            dist[q[0]] = 0;
            for (int h = 0; h < q.Count; h++)
            {
                int c = q[h];
                int cx0 = c % W, cy0 = (c - cx0) / W;
                for (int dd = 0; dd < 4; dd++)
                {
                    int x1 = cx0 + D4[dd][0], y1 = cy0 + D4[dd][1];
                    if (x1 < 1 || y1 < 1 || x1 >= W - 1 || y1 >= H - 1) continue;
                    int k1 = y1 * W + x1;
                    if (_g[k1] != 0 || dist[k1] >= 0) continue;
                    dist[k1] = (short)(dist[c] + 1);
                    q.Add(k1);
                }
            }
            return dist;
        }

        // ───────────────────────────────────────────────── 8) 암반 (봉인되면 롤백)
        void PlaceCore(int ex0, int er0)
        {
            int interior = (W - 2) * (H - 2);
            int wantCore = JsMath.Round(interior * _cfg.CorePercent / 100.0);
            var seen = new byte[N];
            var qArr = new int[N];

            int Reach()
            {
                Array.Clear(seen, 0, N);
                int head = 0, tail = 0, n = 1;
                int s0 = Idx(ex0, er0);
                seen[s0] = 1; qArr[tail++] = s0;
                while (head < tail)
                {
                    int c = qArr[head++];
                    int cx0 = c % W, cy0 = (c - cx0) / W;
                    for (int dd = 0; dd < 4; dd++)
                    {
                        int x1 = cx0 + D4[dd][0], y1 = cy0 + D4[dd][1];
                        if (x1 < 1 || y1 < 1 || x1 >= W - 1 || y1 >= H - 1) continue;
                        int k1 = y1 * W + x1;
                        if (seen[k1] != 0 || _g[k1] == 2) continue;
                        seen[k1] = 1; n++; qArr[tail++] = k1;
                    }
                }
                return n;
            }

            int openish = 0;
            for (int y = 1; y < H - 1; y++)
                for (int x = 1; x < W - 1; x++)
                    if (_g[Idx(x, y)] != 2) openish++;

            bool Risky(int x, int y)
            {
                if (x <= 2 || y <= 2 || x >= W - 3 || y >= H - 3) return true;
                for (int dd = 0; dd < 4; dd++)
                    if (_g[Idx(x + D4[dd][0], y + D4[dd][1])] == 2) return true;
                return false;
            }

            int placed = 0;
            for (int a = 0; a < wantCore * 6 && placed < wantCore; a++)
            {
                int x = 1 + (int)(R() * (W - 2));
                int y = 1 + (int)(R() * (H - 2));
                int len = 2 + (int)(R() * 4);
                var cells = new List<int>();
                var prev = new List<byte>();
                bool risk = false;

                for (int t = 0; t < len; t++)
                {
                    int k = Idx(x, y);
                    if (Inb(x, y) && _g[k] == 1 && !(Math.Abs(x - ex0) < 3 && Math.Abs(y - er0) < 3))
                    {
                        if (Risky(x, y)) risk = true;
                        cells.Add(k); prev.Add(_g[k]); _g[k] = 2;
                    }
                    if (R() < .5) x = Math.Max(1, Math.Min(W - 2, x + (R() < .5 ? 1 : -1)));
                    else y = Math.Max(1, Math.Min(H - 2, y + (R() < .5 ? 1 : -1)));
                }

                if (cells.Count == 0) continue;
                if (!risk) { openish -= cells.Count; placed += cells.Count; continue; }
                if (Reach() < openish - cells.Count - 2)
                {
                    for (int i = 0; i < cells.Count; i++) _g[cells[i]] = prev[i];
                }
                else { openish -= cells.Count; placed += cells.Count; }
            }
        }

        // ───────────────────────────────────────────────── 9) 타일 확정 + 광맥
        TileType[] FinalizeTiles(short[] dist, DungeonResult result)
        {
            int maxD = 0;
            for (int i = 0; i < N; i++) if (dist[i] > maxD) maxD = dist[i];

            var A = new TileType[N];
            result.Band = new byte[N];
            result.Dec = new byte[N];
            int bandRows = _cfg.BandRows;

            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int k = Idx(x, y);
                    result.Band[k] = (byte)JsMath.Clamp(y / bandRows, 0, _cfg.BandCount - 1);
                    double u0 = R();
                    result.Dec[k] = (byte)(u0 < .14 ? 1 : (u0 < .20 ? 2 : 0));
                    int eg = Math.Max(1, _cfg.EdgeThickness);
                    if (x < eg || y < eg || x >= W - eg || y >= H - eg) { A[k] = TileType.Rock; continue; }
                    A[k] = _g[k] == 0 ? TileType.Empty : (_g[k] == 2 ? TileType.Core : TileType.Dirt);
                }

            int interior = (W - 2) * (H - 2);
            int wantOre = JsMath.Round(interior * _cfg.OrePercent / 100.0);
            int placedOre = 0;
            for (int a = 0; a < wantOre * 10 && placedOre < wantOre; a++)
            {
                int x = 1 + (int)(R() * (W - 2));
                int y = 1 + (int)(R() * (H - 2));
                if (A[Idx(x, y)] != TileType.Dirt) continue;

                int nd = -1;
                for (int dd = 0; dd < 4; dd++)
                {
                    int x1 = x + D4[dd][0], y1 = y + D4[dd][1];
                    if (Inb(x1, y1) && dist[Idx(x1, y1)] > nd) nd = dist[Idx(x1, y1)];
                }
                double depth = maxD != 0 ? Math.Max(0, nd) / (double)maxD : 0;
                double u = R();
                TileType kind = u < depth * .34 ? TileType.Crys : (u < depth * .62 ? TileType.Gem : TileType.Ore);
                int len = 2 + (int)(R() * 3);
                for (int t = 0; t < len; t++)
                {
                    int k = Idx(x, y);
                    if (Inb(x, y) && A[k] == TileType.Dirt) { A[k] = kind; placedOre++; }
                    if (R() < .5) x = Math.Max(1, Math.Min(W - 2, x + (R() < .5 ? 1 : -1)));
                    else y = Math.Max(1, Math.Min(H - 2, y + (R() < .5 ? 1 : -1)));
                }
            }

            // 단단한 벽 — 파는 리듬에 변주
            double soft = _cfg.SoftPercent / 100.0, med = _cfg.MedPercent / 100.0;
            for (int y = 1; y < H - 1; y++)
                for (int x = 1; x < W - 1; x++)
                {
                    int k = Idx(x, y);
                    if (A[k] == TileType.Dirt)
                        if (R() > soft) A[k] = (R() < med) ? TileType.Stone : TileType.Dirt;
                }

            result.MaxDist = maxD;
            return A;
        }

        // ───────────────────────────────────────────────── 10) 출구
        int PlaceExit(TileType[] A, short[] dist, int ex0, int er0, out List<(int x, int y, int d)> open)
        {
            var raw = new List<(int x, int y, int d)>();
            for (int y = 1; y < H - 1; y++)
                for (int x = 1; x < W - 1; x++)
                {
                    int k = Idx(x, y);
                    if (A[k] == TileType.Empty && dist[k] >= 0) raw.Add((x, y, dist[k]));
                }
            open = raw.OrderBy(o => o.d).ToList();   // JS sort 와 같은 안정 정렬

            (int x, int y, int d) exq = open.Count > 0
                ? open[Math.Max(0, (int)(open.Count * .94))]
                : (x: ex0, y: er0, d: 0);
            int best = Idx(exq.x, exq.y);

            int ux = JsMath.Sign(exq.x - ex0); if (ux == 0) ux = 1;
            int uy = JsMath.Sign(exq.y - er0); if (uy == 0) uy = 1;
            var order = Math.Abs(exq.x - ex0) >= Math.Abs(exq.y - er0)
                ? new[] { new[] { ux, 0 }, new[] { 0, uy } }
                : new[] { new[] { 0, uy }, new[] { ux, 0 } };

            int px = exq.x, py = exq.y, found = -1;
            int steps = 1 + (int)(R() * 2);
            bool broke = false;
            foreach (var dir in order)
            {
                int cx0 = px, cy0 = py;
                for (int t = 0; t < steps; t++)
                {
                    int nx2 = cx0 + dir[0], ny2 = cy0 + dir[1];
                    if (!Inb(nx2, ny2)) break;
                    int kk = Idx(nx2, ny2);
                    if (A[kk] == TileType.Core || A[kk] == TileType.Rock) break;
                    cx0 = nx2; cy0 = ny2; found = kk;
                    if (A[kk] != TileType.Empty) { broke = true; break; }   // 원본의 `break outer`
                }
                if (broke) break;
            }
            if (found >= 0 && A[found] != TileType.Core && A[found] != TileType.Rock) best = found;
            if (A[best] == TileType.Empty) A[best] = TileType.Dirt;
            return best;
        }

        // ───────────────────────────────────────────────── 유물 · 보급품 · 랜턴 · 소품
        void PlacePoi(TileType[] A, List<(int x, int y, int d)> open, int best, DungeonResult result)
        {
            var pool = open.Where(o => o.d > 4 && Idx(o.x, o.y) != best).ToList();

            (int x, int y, int d)? Pick()
            {
                if (pool.Count == 0) return null;
                int i = (int)(R() * pool.Count);
                var v = pool[i];
                pool.RemoveAt(i);
                return v;
            }

            // 묻힌 유물 — 통로에 인접한 벽 한 칸
            result.Relics = new HashSet<int>();
            for (int i = 0; i < _cfg.BuriedRelics; i++)
            {
                var p = Pick();
                if (p == null) break;
                var cands = new List<int>();
                for (int dd = 0; dd < 4; dd++)
                {
                    int x1 = p.Value.x + D4[dd][0], y1 = p.Value.y + D4[dd][1];
                    if (Inb(x1, y1) && (A[Idx(x1, y1)] == TileType.Dirt || A[Idx(x1, y1)] == TileType.Stone))
                        cands.Add(Idx(x1, y1));
                }
                // 후보가 없으면 난수를 뽑지 않는다. 스트림이 어긋나지 않도록 원본과 같게.
                if (cands.Count > 0) result.Relics.Add(cands[(int)(R() * cands.Count)]);
            }

            // 바닥 보급품
            result.Caches = new List<CachePlacement>();
            for (int i = 0; i < _cfg.Caches; i++)
            {
                var p = Pick();
                if (p == null) break;
                bool shard = R() < .45;
                R();                                  // 원본의 `t: R()*6` — 값은 안 쓰지만 draw 는 소비한다
                int val = shard ? 7 + (int)(R() * 7) : 6 + (int)(R() * 6);
                result.Caches.Add(new CachePlacement { Col = p.Value.x, Row = p.Value.y, IsShard = shard, Value = val });
            }

            // 랜턴
            result.Lamps = new List<(int col, int row)>();
            int want = _cfg.LampCount;
            int minGap = Math.Max(4, JsMath.Round(Math.Min(W, H) * .28));
            for (int a = 0; a < Math.Max(1, want) * 40 && result.Lamps.Count < want; a++)
            {
                if (open.Count == 0) break;
                var p = open[(int)(R() * open.Count)];
                if (p.d < 3) continue;
                bool ok = true;
                foreach (var L in result.Lamps)
                    if (JsMath.Hypot(L.col - p.x, L.row - p.y) < minGap) { ok = false; break; }
                if (!ok) continue;
                R();   // ph: R()*6.283
                result.Lamps.Add((p.x, p.y));
            }

            // 배경 소품
            result.Props = new List<PropPlacement>();
            string[] kinds = { "crystal", "crystal", "scaffold", "crate", "cart", "web" };
            int nProp = Math.Min(28, Math.Max(8, JsMath.Round(open.Count * .04)));
            for (int i = 0; i < nProp; i++)
            {
                if (open.Count == 0) break;
                var p = open[(int)(R() * open.Count)];
                if (p.d < 2) continue;
                string kind = kinds[(int)(R() * kinds.Length)];   // clash 검사 전에 뽑는다 (원본과 동일)
                bool clash = false;
                foreach (var L in result.Lamps)
                    if (JsMath.Hypot(L.col - p.x, L.row - p.y) < 2.2) { clash = true; break; }
                if (clash) continue;
                double ox = (R() - .5) * .3;   // x
                double oy = (R() - .5) * .3;   // y
                R();                            // rot
                R();                            // seed
                R();                            // ph
                result.Props.Add(new PropPlacement
                {
                    Kind = kind,
                    Col = (int)Math.Floor(p.x + 0.5 + ox),
                    Row = (int)Math.Floor(p.y + 0.5 + oy),
                });
            }
        }

        // ───────────────────────────────────────────────── 11) 진입점 강제 개방
        void ApplyEnsurePath(TileType[] A, int ex0, int er0)
        {
            if (!_cfg.EnsurePath) return;
            for (int dr = -2; dr <= 2; dr++)
                for (int dc = -2; dc <= 2; dc++)
                {
                    int x = ex0 + dc, y = er0 + dr;
                    if (!Inb(x, y)) continue;
                    int k = Idx(x, y);
                    if (A[k] != TileType.Empty && !TileTypes.IsBedrock(A[k]) && JsMath.Hypot(dc, dr) <= 2.1)
                        A[k] = TileType.Empty;
                }
        }

        /// <summary>
        /// 원본 생성이 완전히 끝난 뒤 적용하는 Unity 전용 구도 패스.
        /// Runtime 설정에서는 꺼져 있으므로 패리티 픽스처와 난수 소비 순서는 변하지 않는다.
        /// </summary>
        void ApplyPresentationEntry(TileType[] A, int ex0, int er0, DungeonResult result)
        {
            int halfW = _cfg.PresentationEntryHalfWidth;
            int halfH = _cfg.PresentationEntryHalfHeight;
            if (halfW <= 0 || halfH <= 0) return;

            for (int dr = -halfH; dr <= halfH; dr++)
                for (int dc = -halfW; dc <= halfW; dc++)
                {
                    double nx = System.Math.Abs(dc / (halfW + .15));
                    double ny = System.Math.Abs(dr / (halfH + .15));
                    // 타원은 위·아래에서 한두 셀로 급격히 좁아져, 레퍼런스의 긴 설비 벽 대신
                    // 계단형 동굴 입구를 만들었다. 4차 superellipse는 중앙 직선 구간을 길게
                    // 유지하면서 모서리만 둥글려 산업 작업실과 자연 암반의 중간 실루엣을 만든다.
                    if (System.Math.Pow(nx, 4) + System.Math.Pow(ny, 4) > 1.0) continue;

                    int x = ex0 + dc, y = er0 + dr;
                    if (!Inb(x, y)) continue;
                    int k = Idx(x, y);
                    // Unity 전용 첫 방 안의 무작위 Core는 검은 정사각 독립 기둥으로 남아
                    // 연결 설비와 열린 작업 동선을 끊는다. 외곽 Rock은 보존하되 내부 Core까지
                    // 비워, 특수 구조물이 아니라 기본 방 바닥으로 읽히게 한다.
                    if (A[k] != TileType.Rock) A[k] = TileType.Empty;
                }

            if (!_cfg.PresentationEntryLamps) return;

            result.PresentationLamps = new List<(int col, int row)>
            {
                (ex0 - Math.Max(2, halfW - 2), er0 + Math.Max(1, halfH - 2)), // 시안 방향등
                (ex0 + Math.Max(2, halfW - 2), er0 + Math.Max(1, halfH - 2)), // 마젠타 설비등
                (ex0, er0 - Math.Max(2, halfH - 1)),                         // 앰버 작업등
            };

            foreach (var lamp in result.PresentationLamps)
            {
                if (!Inb(lamp.col, lamp.row)) continue;
                int k = Idx(lamp.col, lamp.row);
                if (A[k] != TileType.Empty) continue;
                if (!result.Lamps.Contains(lamp)) result.Lamps.Add(lamp);
            }
        }
    }

    public sealed class DungeonResult
    {
        public int Depth, Cols, Rows, RoomCount;
        public int EntryCol, EntryRow;
        public int Exit;
        public bool ExitOpen;
        public int MaxDist, Interior;
        public TileType[] Tiles;
        public byte[] Band, Dec;
        public HashSet<int> Relics;
        public List<CachePlacement> Caches;
        public List<(int col, int row)> Lamps;
        /// <summary>Unity 비주얼 진입부의 색 역할 고정 램프. 순서: 시안, 마젠타, 앰버.</summary>
        public List<(int col, int row)> PresentationLamps;
        public List<PropPlacement> Props;
    }

    public struct CachePlacement { public int Col, Row, Value; public bool IsShard; }
    public struct PropPlacement { public string Kind; public int Col, Row; }
}
