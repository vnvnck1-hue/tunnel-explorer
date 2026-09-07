using System;
using System.Collections.Generic;

namespace TunnelCrew.Sim
{
    /// <summary>
    /// 관전 모드 — 리더(플레이어) 오토파일럿. 원본 <c>ai/observer.js</c> 의 OBS.drive 를 옮긴 것.
    /// 사람 입력 대신 <see cref="PlayerInput"/> 을 만들어 낸다 — 사람 시스템·밸런스는 그대로, 입력만 가상.
    ///
    /// 판단(decide) 우선순위: 탈출 포트 탑승 → 다운 크루 구조 → 전투(보스 18칸 · 위협 13칸) → 재장전 → 채굴(6/11/17칸 · 경계벽) → 대기.
    /// 실행(act): 보스탄 예고 회피가 어떤 행동보다 먼저. 경로는 AI 크루와 같은 다익스트라(벽 = 뚫는 시간만큼 비싼 통로).
    /// 지형 게이트(AIGEO)는 <see cref="CrewGeo"/> 를 그대로 쓴다 — 도달성·접촉·봉인·진척 감시.
    /// </summary>
    public sealed class ObserverPilot
    {
        readonly TunnelSim _sim;
        readonly Random _rng = new Random(0x0B5E);
        /// <summary>CrewGeo.Progress 가 상태를 CrewMember 에 적기 때문에 리더용 빈 껍데기를 하나 둔다.</summary>
        readonly CrewMember _geoSelf = new CrewMember();

        CrewGoal _goal; double _react;
        readonly List<int> _path = new List<int>(); string _pathKey = ""; double _pathAge;
        (int c, int r, Vec2 at, double until)? _mine;
        (int c, int r)? _drillCell;
        Vec2 _last; double _stuckT, _jitter, _jitterA;
        bool _moveWanted;
        CrewKit _kit; RoleId _kitRole = (RoleId)255;

        /// <summary>리더의 경로 굴착 의지 — 원본 LEADER_KIT.digCost (AI 크루 KIT 와 같은 감각).</summary>
        static double LeaderDigCost(RoleId r) => r switch { RoleId.Driller => 5, RoleId.Gunner => 14, RoleId.Scout => 12, _ => 10 };
        /// <summary>교전 거리(칸) — 원본 LEADER_KIT.engage.</summary>
        static double Engage(RoleId r) => r switch { RoleId.Driller => 3.6, RoleId.Gunner => 5.2, RoleId.Scout => 4.6, _ => 4.4 };
        /// <summary>원본 teWorld(280)·1.2·.92 = 280px → 5.6칸 × 1.104.</summary>
        const double GunRange = 5.6 * 1.2 * .92;

        public ObserverPilot(TunnelSim sim) { _sim = sim; }

        public string GoalLabel => _goal?.Label ?? "대기";

        PlayerState P => _sim.Player;
        WorldGrid W => _sim.World;
        AiCrewSystem Crew => _sim.Crew;
        CrewGeo Geo => Crew.Geo;
        double Now => _sim.RunTime;
        double Rnd() => _rng.NextDouble();

        CrewKit Kit()
        {
            var role = _sim.Build.Role;
            if (_kit == null || _kitRole != role) { _kit = CrewKit.For(role); _kit.PathDigCost = LeaderDigCost(role); _kitRole = role; }
            return _kit;
        }

        /// <summary>층이 바뀌거나 관전을 켤 때 — 목표·경로·봉인 초기화 (원본 resetBrain).</summary>
        public void Reset()
        {
            _goal = null; _react = 0; _path.Clear(); _pathKey = ""; _pathAge = 0;
            _mine = null; _drillCell = null; _stuckT = 0; _jitter = 0; _moveWanted = false;
            Geo.ClearBans(); Geo.ProgressReset(_geoSelf);
            _last = P.Position;
        }

        /// <summary>한 프레임 분량의 가상 입력. 플레이 중이 아니면 빈 입력.</summary>
        public PlayerInput Build(double dt)
        {
            var input = new PlayerInput { AimWorld = P.Position + new Vec2(1, 0) };
            if (W == null || _sim.Phase != GamePhase.Playing) return input;
            _drillCell = null; _moveWanted = false;
            if (P.Downed) return input;                         // 기절 — AI 크루의 구조를 기다린다
            _react -= dt; _pathAge -= dt;
            if (_react <= 0 || _goal == null) { _goal = Decide(ref input); _react = .15 + Rnd() * .08; }
            Act(ref input, dt);
            WatchStuck(dt);
            return input;
        }

        // ───────────────────────────── 판단
        CrewGoal Decide(ref PlayerInput input)
        {
            var esc = _sim.Escape;
            if (esc.Phase == EscapePhase.Placing) esc.Cancel();
            // 0) 탈출 포트가 있으면 탑승한다 — 크루 전원이 같은 규칙
            if (esc.Phase == EscapePhase.Incoming || esc.Phase == EscapePhase.Ready)
                return new CrewGoal { Kind = CrewGoalKind.Escape, At = esc.Position, Label = "탈출 포트" };
            // 1) 쓰러진 AI 크루 구조 — 리더가 유일한 구조자일 수 있다
            foreach (var m in Crew.Members) if (m.Down) return new CrewGoal { Kind = CrewGoalKind.Revive, ReviveTarget = m, At = m.Position, Label = "크루 구조" };
            // 2) 전투 — 보스가 가까우면 보스, 아니면 가장 가까운 위협
            if (_sim.Bosses.Active)
            {
                var b = _sim.Bosses.Boss.Body;
                if (Vec2.Distance(b.Position, P.Position) < 18) return new CrewGoal { Kind = CrewGoalKind.Fight, Enemy = b, Boss = true, Label = "보스 교전" };
            }
            var e = NearestThreat();
            if (e != null) return new CrewGoal { Kind = CrewGoalKind.Fight, Enemy = e, Label = "교전" };
            // 3) 전투가 끊긴 사이 재장전
            var bd = _sim.Build;
            if (bd.RoleHasGun && bd.ReloadLeft <= 0 && bd.Ammo < bd.MagSize * .5) input.ReloadPressed = true;
            // 4) 채굴 — 한 번 고른 벽은 부술 때까지 붙잡는다
            if (_mine.HasValue)
            {
                var mt = _mine.Value; var t = W.At(mt.c, mt.r);
                if (t == TileType.Empty || TileTypes.IsBedrock(t) || mt.until < Now || !Geo.CanMine(P.Position, mt.c, mt.r, Now)) _mine = null;
                else return new CrewGoal { Kind = CrewGoalKind.Mine, C = mt.c, R = mt.r, At = mt.at, Label = "채굴" };
            }
            var pick = PickMine(6) ?? PickMine(11) ?? PickMine(17) ?? PickFrontier();
            if (pick != null)
            {
                _mine = (pick.C, pick.R, pick.At, Now + 16);
                return new CrewGoal { Kind = CrewGoalKind.Mine, C = pick.C, R = pick.R, At = pick.At, Label = "채굴" };
            }
            return new CrewGoal { Kind = CrewGoalKind.Guard, At = P.Position, Label = "대기" };
        }

        EnemyState NearestThreat()
        {
            EnemyState best = null; double bd = 1e9;
            foreach (var e in _sim.Enemies.Enemies)
            {
                if (e.Hp <= 0 || e.IsBoss) continue;
                double d = Vec2.Distance(e.Position, P.Position);
                if (d > 13) continue;
                if (d > 2.4 && !SightUtil.IsClear(W, P.Position, e.Position)) continue;
                if (d < bd) { bd = d; best = e; }
            }
            return best;
        }

        static double OreScore(TileType t) => t == TileType.Gem ? 28 : t == TileType.Crys ? 22 : t == TileType.Ore ? 16 : t == TileType.Stone ? 3 : 2;

        /// <summary>광맥 가중 · 가까운 벽 우선. AI 크루가 잡은 벽 주변 1칸은 피한다 — 리더까지 같은 벽에 줄 서지 않게.</summary>
        CrewGoal PickMine(int radius)
        {
            var (oc, orr) = WorldGrid.ToCell(P.Position);
            var claims = new List<(int c, int r)>();
            foreach (var o in Crew.Members) if (o.Goal != null && o.Goal.Kind == CrewGoalKind.Mine) claims.Add((o.Goal.C, o.Goal.R));
            CrewGoal best = null; double bs = -1e9;
            for (int dr = -radius; dr <= radius; dr++) for (int dc = -radius; dc <= radius; dc++)
            {
                int c = oc + dc, r = orr + dr;
                if (!W.InInterior(c, r)) continue;
                var t = W.At(c, r);
                if (t == TileType.Empty || TileTypes.IsBedrock(t)) continue;
                if (!Geo.CanMine(P.Position, c, r, Now)) continue;
                bool claimed = false;
                foreach (var q in claims) if (Math.Abs(q.c - c) <= 1 && Math.Abs(q.r - r) <= 1) { claimed = true; break; }
                if (claimed) continue;
                double s = OreScore(t) - Math.Sqrt(dc * dc + dr * dr) * 2.2;
                if (s > bs) { bs = s; best = new CrewGoal { Kind = CrewGoalKind.Mine, C = c, R = r, At = WorldGrid.CellCenter(c, r), Label = "채굴" }; }
            }
            return best;
        }

        /// <summary>주변이 다 파였으면 내 열린 공간에 닿은 경계벽에서 고른다 — 장악도를 계속 올린다.</summary>
        CrewGoal PickFrontier() => Geo.Frontier(P.Position, Now, (c, r, t) => OreScore(t) - Vec2.Distance(WorldGrid.CellCenter(c, r), P.Position) * 1.2);

        // ───────────────────────────── 실행
        void Act(ref PlayerInput input, double dt)
        {
            bool dodging = DodgeBossShot(ref input);
            var g = _goal;
            if (g.Kind == CrewGoalKind.Fight)
            {
                var e = g.Enemy;
                if (e == null || e.Hp <= 0) { _goal = null; return; }
                double d = Vec2.Distance(e.Position, P.Position);
                double want = g.Boss ? Engage(_sim.Build.Role) + 1.8 : Engage(_sim.Build.Role);
                input.AimWorld = e.Position;
                bool los = SightUtil.IsClear(W, P.Position, e.Position);
                if (los && d <= GunRange) input.FireHeld = true;
                if (dodging) return;                                // 사격은 유지한 채 원 밖으로
                if (!los && d > 2) { FollowPath(ref input, e.Position); input.AimWorld = e.Position; return; }
                if (d < want * .6) MoveToward(ref input, P.Position * 2 - e.Position);
                else if (d > want * 1.35) MoveToward(ref input, e.Position);
                else
                {   // 사거리 유지 — 옆으로 돌며 쏜다
                    double a = Math.Atan2(e.Position.Y - P.Position.Y, e.Position.X - P.Position.X) + Math.PI / 2;
                    MoveToward(ref input, P.Position + new Vec2(Math.Cos(a), Math.Sin(a)));
                }
                if (_sim.Build.Role == RoleId.Driller && d < 1.6) input.DrillHeld = true;   // 드릴러는 붙은 적을 드릴로 간다
                return;
            }
            if (dodging) return;

            if (g.Kind == CrewGoalKind.Escape)
            {
                if (Vec2.Distance(g.At, P.Position) > .8) FollowPath(ref input, g.At);
                ShootNear(ref input, 8);
                return;
            }
            if (g.Kind == CrewGoalKind.Revive)
            {
                var m = g.ReviveTarget;
                if (m == null || !m.Down) { _goal = null; return; }
                g.At = m.Position;
                if (Vec2.Distance(m.Position, P.Position) > 1.3) FollowPath(ref input, m.Position);
                ShootNear(ref input, 8);
                return;
            }
            if (g.Kind == CrewGoalKind.Mine)
            {
                FollowPath(ref input, g.At);
                // 드릴을 넣고 있는데 벽 체력이 줄지 않으면 그 벽을 봉인하고 다른 목표로 (거너는 파쇄탄 재장전을 기다린다)
                if (!_drillCell.HasValue) { Geo.ProgressReset(_geoSelf); return; }
                var dc = _drillCell.Value;
                double lim = _sim.Build.Role == RoleId.Gunner ? 6.0 : 2.0;
                if (!Geo.Progress(_geoSelf, dc.c, dc.r, dt, lim))
                {
                    Geo.Ban(dc.c, dc.r, Now, 14);
                    _mine = null; _goal = null; _path.Clear(); _pathKey = ""; _pathAge = 0;
                }
                return;
            }
            ShootNear(ref input, 9);   // 대기 — 주변 경계
        }

        /// <summary>보스탄 예고 원 안이면 하던 일과 무관하게 빠진다 — AI 크루와 같은 규칙.</summary>
        bool DodgeBossShot(ref PlayerInput input)
        {
            if (!_sim.Bosses.Active) return false;
            BossShot danger = null; double bestEta = 1e9, dd = 0;
            foreach (var s in _sim.Bosses.Shots)
            {
                double eta = Math.Max(0, s.Flight - s.T);
                if (eta > 1.8) continue;
                double d = Vec2.Distance(P.Position, s.Target);
                if (d > s.Radius * 1.15) continue;
                if (eta < bestEta) { bestEta = eta; danger = s; dd = d; }
            }
            if (danger == null) return false;
            var n = P.Position - danger.Target;
            if (n.Length < .02) { double a = Rnd() * Math.PI * 2; n = new Vec2(Math.Cos(a), Math.Sin(a)); } else n = n.Normalized;
            double need = danger.Radius * 1.3 - dd;
            MoveToward(ref input, P.Position + n * (need + 1));
            if (bestEta < .6 && need > .5) input.DashPressed = true;
            return true;
        }

        void ShootNear(ref PlayerInput input, double cells)
        {
            EnemyState best = null; double bd = cells;
            foreach (var e in _sim.Enemies.Enemies)
            {
                if (e.Hp <= 0) continue;
                double d = Vec2.Distance(e.Position, P.Position);
                if (d < bd && SightUtil.IsClear(W, P.Position, e.Position)) { bd = d; best = e; }
            }
            if (best != null) { input.AimWorld = best.Position; input.FireHeld = true; }
        }

        // ───────────────────────────── 경로 · 이동
        int EnsurePath(Vec2 goal)
        {
            var (c0, r0) = WorldGrid.ToCell(P.Position);
            var (gc, gr) = WorldGrid.ToCell(goal);
            string key = gc + "," + gr;
            if (key != _pathKey || _pathAge <= 0 || _path.Count == 0)
            {
                _path.Clear();
                var found = Crew.FindPath(c0, r0, gc, gr, Kit(), null);
                if (found != null) _path.AddRange(found);
                _pathKey = key; _pathAge = .5;
            }
            while (_path.Count > 0)
            {
                int k = _path[0]; int c = k % W.Cols, r = k / W.Cols;
                if (W.AtIndex(k) == TileType.Empty && Vec2.Distance(WorldGrid.CellCenter(c, r), P.Position) < .55) _path.RemoveAt(0);
                else break;
            }
            return _path.Count > 0 ? _path[0] : -1;
        }

        void FollowPath(ref PlayerInput input, Vec2 goal)
        {
            int step = EnsurePath(goal);
            if (step < 0) { MoveToward(ref input, goal); input.AimWorld = goal; return; }
            int c = step % W.Cols, r = step / W.Cols; var w = WorldGrid.CellCenter(c, r);
            if (W.AtIndex(step) != TileType.Empty)
            {   // 앞이 벽 — 조준하고 드릴(거너는 같은 입력으로 파쇄탄이 나간다)
                input.AimWorld = w;
                double d = Vec2.Distance(w, P.Position);
                // 드릴이 실제로 닿는 칸일 때만 입력을 넣고, 닿을 때까지 계속 붙는다
                if (d < 1.35 && Geo.InReach(P.Position, c, r))
                {
                    input.DrillHeld = true; _drillCell = (c, r);
                    if (d > .95) MoveToward(ref input, w);
                }
                else MoveToward(ref input, w);
                return;
            }
            MoveToward(ref input, w); input.AimWorld = w;
        }

        /// <summary>WASD 로 환산 — 원본 keysToward 의 8방향 양자화(임계 .38)를 그대로.</summary>
        void MoveToward(ref PlayerInput input, Vec2 to)
        {
            var d = to - P.Position; double len = d.Length;
            if (len < .08) return;
            double nx = d.X / len, ny = d.Y / len;
            if (_jitter > 0) { nx = Math.Cos(_jitterA); ny = Math.Sin(_jitterA); }
            double mx = nx < -.38 ? -1 : nx > .38 ? 1 : 0, my = ny < -.38 ? -1 : ny > .38 ? 1 : 0;
            if (mx == 0 && my == 0) return;
            var mv = new Vec2(mx, my); input.Move = mv.Normalized; _moveWanted = true;
        }

        void WatchStuck(double dt)
        {
            double moved = Vec2.Distance(P.Position, _last); _last = P.Position;
            if (_moveWanted && moved < dt * .18) _stuckT += dt; else _stuckT = Math.Max(0, _stuckT - dt * 2);
            if (_jitter > 0) _jitter -= dt;
            if (_stuckT > 1.2)
            {
                _stuckT = 0; _path.Clear(); _pathKey = ""; _pathAge = 0;
                _jitter = .5; _jitterA = Rnd() * Math.PI * 2;
                if (_goal != null && _goal.Kind == CrewGoalKind.Mine) _mine = null;
            }
        }
    }
}
