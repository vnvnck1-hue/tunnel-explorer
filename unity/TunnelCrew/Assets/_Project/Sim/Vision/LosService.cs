using System;
using System.Collections.Generic;

namespace TunnelCrew.Sim
{
    /// <summary>시야를 만드는 광원 하나. 크루·플레어·보스가 모두 이 형태로 들어온다.</summary>
    public struct VisionSource
    {
        public Vec2 Position;
        public int Range;
        public int Rays;
        /// <summary>true 면 지금 보이기만 하고 탐색 기록(Explored)은 남기지 않는다. 보스 시야원이 그렇다.</summary>
        public bool VisibleOnly;

        public static VisionSource Crew(Vec2 p) => new VisionSource
        {
            Position = p,
            Range = SimTuning.CrewVisionRange,
            Rays = SimTuning.CrewVisionRays,
        };
    }

    /// <summary>
    /// 원본 <c>LOS</c> (5164~5353행) 를 옮긴 것. 타일 단위 레이캐스트로
    /// "지금 보이는 칸"과 "가 본 적 있는 칸"을 만든다.
    ///
    /// 원본은 결과를 RGBA 픽셀 버퍼에 바로 써서 WebGL 텍스처로 올렸다. 여기서는
    /// Sim 이 <see cref="Visible"/> · <see cref="Explored"/> 만 만들고, 텍스처로 굽는 일은
    /// Presentation 이 한다(analysis-04 §2.3). 코옵·AI 가 <see cref="IsSeen"/> 을 쓰므로
    /// 레이캐스트 자체는 Sim 에 남는다.
    /// </summary>
    public sealed class LosService
    {
        // 격자 접근은 델리게이트로 받는다 — 본편은 WorldGrid, 조명 랩은 손으로 만든 격자를 넣는다
        // (2026-09-10, 랩에 LOS 를 이식하면서). WorldGrid 를 직접 잡던 코드는 아래 생성자가 그대로 감싼다.
        readonly Func<int, int, bool> _isSolid;
        readonly Func<int> _version;
        readonly int _w, _h;

        /// <summary>이번 프레임에 보이는 칸. 0 또는 1.</summary>
        public byte[] Visible { get; }
        /// <summary>한 번이라도 본 칸. 0 또는 1. 영구 누적된다.</summary>
        public byte[] Explored { get; }

        /// <summary>마지막으로 계산한 시점의 플레이어 셀. 기억 감쇠의 중심이다.</summary>
        public int LastCol { get; private set; } = -999;
        public int LastRow { get; private set; } = -999;

        /// <summary>계산이 한 번이라도 끝났는지.</summary>
        public bool HasComputed { get; private set; }

        bool _dirty = true;
        int _lastWorldVersion = -1;
        string _lastSourceKey = "";

        /// <summary>지금 이 광원은 탐색 기록을 남기지 않는다는 플래그. 원본 visOnly.</summary>
        bool _visibleOnly;

        public LosService(WorldGrid world)
            : this(world.Cols, world.Rows, world.IsSolid, () => world.Version) { }

        /// <summary>
        /// 임의 격자용. <paramref name="isSolid"/> 는 범위 밖을 고체로 답해야 하고,
        /// <paramref name="version"/> 은 타일이 바뀔 때마다 커져야 캐시가 풀린다.
        /// </summary>
        public LosService(int cols, int rows, Func<int, int, bool> isSolid, Func<int> version)
        {
            _w = cols;
            _h = rows;
            _isSolid = isSolid ?? throw new ArgumentNullException(nameof(isSolid));
            _version = version ?? (() => 0);
            Visible = new byte[_w * _h];
            Explored = new byte[_w * _h];
        }

        bool InBounds(int c, int r) => c >= 0 && r >= 0 && c < _w && r < _h;
        int Index(int c, int r) => r * _w + c;

        /// <summary>타일이 바뀌면 호출. 원본 LOS.markDirty().</summary>
        public void MarkDirty() => _dirty = true;

        /// <summary>
        /// 시야를 다시 계산한다. 플레이어 셀·월드 버전·광원 구성이 모두 그대로면 건너뛴다.
        /// </summary>
        /// <param name="viewer">시야의 중심(플레이어). 기억 감쇠도 이 위치 기준이다.</param>
        /// <param name="extraSources">크루·플레어·보스 등 추가 시야원.</param>
        public void Compute(Vec2 viewer, IReadOnlyList<VisionSource> extraSources = null)
        {
            var (pc, pr) = WorldGrid.ToCell(viewer);
            string key = BuildSourceKey(extraSources);

            if (!_dirty && HasComputed && pc == LastCol && pr == LastRow
                && _version() == _lastWorldVersion && key == _lastSourceKey)
                return;

            Array.Clear(Visible, 0, Visible.Length);

            // 플레이어 본체
            See(pc, pr);
            // 주변 8칸은 항상 보인다 (구석 벽을 볼 수 있게)
            for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++)
                    if (dc != 0 || dr != 0) See(pc + dc, pr + dr);

            CastRadial(pc, pr, SimTuning.LosRange, SimTuning.LosRays);

            // 추가 광원
            if (extraSources != null)
            {
                foreach (var s in extraSources)
                {
                    var (sc, sr) = WorldGrid.ToCell(s.Position);
                    if (!InBounds(sc, sr)) continue;

                    _visibleOnly = s.VisibleOnly;
                    See(sc, sr);
                    CastRadial(sc, sr, Math.Max(1, s.Range), Math.Max(8, s.Rays));
                    _visibleOnly = false;
                }
            }

            LastCol = pc;
            LastRow = pr;
            _lastWorldVersion = _version();
            _lastSourceKey = key;
            _dirty = false;
            HasComputed = true;
        }

        void CastRadial(int c0, int r0, int range, int rays)
        {
            for (int i = 0; i < rays; i++)
            {
                double a = (i / (double)rays) * Math.PI * 2.0;
                int tc = c0 + JsMath.Round(Math.Cos(a) * range);
                int tr = r0 + JsMath.Round(Math.Sin(a) * range);
                Cast(c0, r0, tc, tr);
            }
        }

        /// <summary>
        /// 원본 <c>cast()</c> — Bresenham. **벽 타일은 표시한 뒤 차단**한다.
        /// 그래야 벽면이 보이면서도 그 너머는 가려진다.
        /// </summary>
        void Cast(int c0, int r0, int c1, int r1)
        {
            int dx = Math.Abs(c1 - c0), dy = Math.Abs(r1 - r0);
            int sx = c0 < c1 ? 1 : -1, sy = r0 < r1 ? 1 : -1;
            int err = dx - dy, x = c0, y = r0;
            int maxStep = dx + dy + 2;

            for (int s = 0; s < maxStep; s++)
            {
                See(x, y);
                if (_isSolid(x, y) && (x != c0 || y != r0)) return;
                if (x == c1 && y == r1) return;

                int e2 = err * 2;
                if (e2 > -dy) { err -= dy; x += sx; }
                if (e2 < dx) { err += dx; y += sy; }
            }
        }

        void See(int c, int r)
        {
            if (!InBounds(c, r)) return;
            int k = Index(c, r);
            Visible[k] = 1;
            if (!_visibleOnly) Explored[k] = 1;
        }

        string BuildSourceKey(IReadOnlyList<VisionSource> sources)
        {
            if (sources == null || sources.Count == 0) return "";
            var sb = new System.Text.StringBuilder(sources.Count * 12);
            foreach (var s in sources)
            {
                var (c, r) = WorldGrid.ToCell(s.Position);
                sb.Append(c).Append(',').Append(r).Append(',').Append(s.Range).Append('|');
            }
            return sb.ToString();
        }

        // ───────────────────────────── 조회
        public bool IsVisible(int c, int r)
            => InBounds(c, r) && Visible[Index(c, r)] != 0;

        public bool IsExplored(int c, int r)
            => InBounds(c, r) && Explored[Index(c, r)] != 0;

        /// <summary>원본 inMemory() — 기억 반경 안인가.</summary>
        public bool InMemory(int c, int r)
        {
            int dc = c - LastCol, dr = r - LastRow;
            return dc * dc + dr * dr <= SimTuning.LosMemory * SimTuning.LosMemory;
        }

        /// <summary>원본 seenTile() — 지금 보이거나, 기억 반경 안의 탐색 지역.</summary>
        public bool IsSeen(int c, int r)
        {
            if (!InBounds(c, r)) return false;
            int k = Index(c, r);
            if (Visible[k] != 0) return true;
            return Explored[k] != 0 && InMemory(c, r);
        }

        /// <summary>원본 softSeenTile() — 반경 pad 안에 하나라도 보이면 true. 렌더 컬링용.</summary>
        public bool IsSoftSeen(int c, int r, int pad)
        {
            for (int dr = -pad; dr <= pad; dr++)
                for (int dc = -pad; dc <= pad; dc++)
                    if (IsSeen(c + dc, r + dr)) return true;
            return false;
        }

        /// <summary>
        /// 원본 픽셀 버퍼의 G 채널 — 탐색 지역의 농도(0~255).
        /// <c>exp * (0.30 + 0.70 * fade²)</c>, fade = 1 - 거리/기억반경.
        /// 기억 반경 밖이어도 0 이 아니라 30% 를 유지해 급격히 검게 끊기지 않는다.
        /// </summary>
        public int MemoryValue(int c, int r)
        {
            if (!IsExplored(c, r)) return 0;
            int exp = JsMath.Clamp(JsMath.Round(SimTuning.LosExplored * 255), 0, 255);
            int mem = SimTuning.LosMemory;
            int dc = c - LastCol, dr = r - LastRow;
            double d2 = dc * dc + dr * dr;
            double fade = 0;
            if (d2 <= (double)mem * mem) fade = 1.0 - Math.Sqrt(d2) / Math.Max(1, mem);
            double floor = SimTuning.LosMemoryFloor;
            return Math.Max(0, JsMath.Round(exp * (floor + (1 - floor) * fade * fade)));
        }

        /// <summary>층을 새로 만들 때. 원본 LOS.reset().</summary>
        public void Reset()
        {
            Array.Clear(Visible, 0, Visible.Length);
            Array.Clear(Explored, 0, Explored.Length);
            LastCol = -999; LastRow = -999;
            _dirty = true;
            _lastSourceKey = "";
            _lastWorldVersion = -1;
            HasComputed = false;
        }
    }
}
