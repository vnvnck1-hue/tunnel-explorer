using System;
using System.Collections.Generic;

namespace TunnelCrew.Sim
{
    /// <summary>
    /// AIGEO (v7.8.1) — AI 가 벽을 고르기 전에 반드시 통과해야 하는 지형 게이트.
    /// "기반암 건너편 벽을 목표로 잡고 구석만 비비는" 무한 루프를 세 겹으로 끊는다:
    /// ① 도달성(내가 서 있는 열린 공간 성분에 닿은 벽만) ② 접촉(기반암 모서리 대각 관통 금지) ③ 봉인(진척 없는 벽은 한동안 후보 제외).
    /// 원본은 엔진이 유지하는 G.comp 를 썼다. 여기서는 월드 버전이 바뀌면 열린 칸의 4방향 연결 성분을 다시 칠한다.
    /// </summary>
    public sealed class CrewGeo
    {
        WorldGrid _world;
        int[] _comp; int _compVersion = -1;
        readonly Dictionary<int, double> _ban = new Dictionary<int, double>();
        readonly Queue<int> _q = new Queue<int>();

        public void Bind(WorldGrid world) { _world = world; _comp = null; _compVersion = -1; _ban.Clear(); }

        public void Ban(int c, int r, double now, double sec = 14) { if (_world == null) return; _ban[_world.Index(c, r)] = now + sec; }
        public bool Banned(int c, int r, double now)
        {
            if (_world == null) return false;
            int k = _world.Index(c, r);
            if (!_ban.TryGetValue(k, out double until)) return false;
            if (until <= now) { _ban.Remove(k); return false; }
            return true;
        }
        public void ClearBans() => _ban.Clear();

        void EnsureComp()
        {
            if (_comp != null && _compVersion == _world.Version) return;
            int n = _world.CellCount;
            if (_comp == null || _comp.Length != n) _comp = new int[n];
            Array.Fill(_comp, -1);
            _compVersion = _world.Version;
            int next = 0;
            for (int k = 0; k < n; k++)
            {
                if (_comp[k] >= 0 || _world.AtIndex(k) != TileType.Empty) continue;
                _comp[k] = next; _q.Enqueue(k);
                while (_q.Count > 0)
                {
                    int cur = _q.Dequeue();
                    int c = cur % _world.Cols, r = cur / _world.Cols;
                    for (int i = 0; i < 4; i++)
                    {
                        int nc = c + (i == 0 ? 1 : i == 1 ? -1 : 0), nr = r + (i == 2 ? 1 : i == 3 ? -1 : 0);
                        if (!_world.InBounds(nc, nr)) continue;
                        int nk = _world.Index(nc, nr);
                        if (_comp[nk] >= 0 || _world.AtIndex(nk) != TileType.Empty) continue;
                        _comp[nk] = next; _q.Enqueue(nk);
                    }
                }
                next++;
            }
        }

        /// <summary>(x,y) 에 선 액터의 열린 공간 성분. 벽 속이면 -1.</summary>
        public int CompOf(Vec2 p)
        {
            EnsureComp();
            var (c, r) = WorldGrid.ToCell(p);
            if (!_world.InBounds(c, r)) return -1;
            return _comp[_world.Index(c, r)];
        }

        bool Bad(TileType t) => t == TileType.Empty || TileTypes.IsBedrock(t);

        /// <summary>도달성 — (x,y) 에 선 액터가 걸어가서 실제로 붙을 수 있는 벽인가.</summary>
        public bool CanMine(Vec2 at, int c, int r, double now)
        {
            if (_world == null || !_world.InInterior(c, r)) return false;
            if (Bad(_world.At(c, r)) || Banned(c, r, now)) return false;
            int comp = CompOf(at);
            if (comp < 0) return false;
            for (int i = 0; i < 4; i++)
            {
                int nc = c + (i == 0 ? 1 : i == 1 ? -1 : 0), nr = r + (i == 2 ? 1 : i == 3 ? -1 : 0);
                if (!_world.InInterior(nc, nr)) continue;
                int nk = _world.Index(nc, nr);
                if (_world.AtIndex(nk) == TileType.Empty && _comp[nk] == comp) return true;
            }
            return false;
        }

        /// <summary>접촉 — 같은 칸·상하좌우는 허용, 대각은 기반암 모서리를 관통하지 않을 때만.</summary>
        public bool InReach(Vec2 at, int c, int r)
        {
            var (mc, mr) = WorldGrid.ToCell(at);
            int dc = Math.Abs(mc - c), dr = Math.Abs(mr - r);
            if (dc + dr <= 1) return true;
            if (dc == 1 && dr == 1) return !(HardAt(c, mr) && HardAt(mc, r));
            return false;
        }
        bool HardAt(int c, int r) => !_world.InBounds(c, r) || TileTypes.IsBedrock(_world.At(c, r));

        /// <summary>경계벽 — 내 열린 공간에 닿아 있는 벽 전체에서 최고점을 고른다.</summary>
        public CrewGoal Frontier(Vec2 at, double now, Func<int, int, TileType, double> score)
        {
            int comp = CompOf(at);
            if (comp < 0) return null;
            CrewGoal best = null; double bs = -1e9;
            for (int r = 1; r < _world.Rows - 1; r++) for (int c = 1; c < _world.Cols - 1; c++)
            {
                var t = _world.At(c, r);
                if (Bad(t) || Banned(c, r, now)) continue;
                bool touch = false;
                for (int i = 0; i < 4 && !touch; i++)
                {
                    int nc = c + (i == 0 ? 1 : i == 1 ? -1 : 0), nr = r + (i == 2 ? 1 : i == 3 ? -1 : 0);
                    if (!_world.InInterior(nc, nr)) continue;
                    int nk = _world.Index(nc, nr);
                    if (_world.AtIndex(nk) == TileType.Empty && _comp[nk] == comp) touch = true;
                }
                if (!touch) continue;
                double s = score(c, r, t);
                if (s > bs) { bs = s; best = new CrewGoal { Kind = CrewGoalKind.Mine, C = c, R = r, At = WorldGrid.CellCenter(c, r), Label = "채굴" }; }
            }
            return best;
        }

        public double WallHp(int c, int r)
        {
            if (!_world.InInterior(c, r)) return -1;
            var t = _world.At(c, r);
            if (Bad(t)) return -1;
            return _world.HpAt(_world.Index(c, r));
        }

        public void ProgressReset(CrewMember m) { m.GeoC = -1; m.GeoR = -1; m.GeoT = 0; }
        /// <summary>진척 감시 — 목표 벽 체력이 줄고 있는가. 멈춰 있으면 false.</summary>
        public bool Progress(CrewMember m, int c, int r, double dt, double limit = 1.8)
        {
            double hp = WallHp(c, r);
            if (hp < 0) { ProgressReset(m); return true; }
            if (m.GeoC != c || m.GeoR != r) { m.GeoC = c; m.GeoR = r; m.GeoHp = hp; m.GeoT = 0; return true; }
            if (hp < m.GeoHp - .01) { m.GeoHp = hp; m.GeoT = 0; return true; }
            m.GeoT += dt;
            if (m.GeoT > limit) { ProgressReset(m); return false; }
            return true;
        }
    }
}
