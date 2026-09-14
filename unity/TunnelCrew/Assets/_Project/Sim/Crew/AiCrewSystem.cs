using System;
using System.Collections.Generic;

namespace TunnelCrew.Sim
{
    public enum CrewFxKind : byte { Ring, Burst, Flash, Text, Kick, Chunks }
    public struct CrewFxEvent
    {
        public CrewFxKind Kind; public Vec2 At; public Vec2 Dir; public double Radius; public string Color; public string Label; public double Size; public int Count;
    }

    /// <summary>
    /// AI 크루 — 원본 <c>AICREW</c> (crew-ai.js · AI_CREW_INJECTED_V1). 어떤 모드든 직업 선택 화면에서 [+AI] 를 눌러
    /// 즉시 편성하는 **같은 인스턴스 안의 로컬 동료**. 같은 세계·같은 적·같은 지형을 공유한다.
    ///
    /// 설계 원칙 (§9.6):
    /// 1) 크루는 "그 직업이 실제로 할 만한 행위"를 한다 — 역할 행동 예산 <see cref="CrewKit"/>.
    /// 2) 성장은 분리된다 — AI 가 부순 블록은 장악도에 기여하되 사람 경험치를 올리지 않고, AI 는 자기 XP·자기 특성으로 큰다.
    /// 3) AI 설치물은 사람 엔지니어의 전력망과 섞지 않는다.
    /// 4) AI_HUMANIZE_V1 — 성향·기분·의도 게이트 세 겹으로 "선택적 행위"를 흘려보낸다. 반드시 해야 하는 것(탑승·구조·보스탄 회피·파묻힘 탈출)은 흘리지 않는다.
    ///
    /// 게임 접근은 <see cref="TunnelSim"/> 을 통해서만 한다. 층이 바뀌어 World 가 새로 만들어져도 참조가 어긋나지 않는다.
    /// </summary>
    public sealed class AiCrewSystem
    {
        public const int Max = 3;
        readonly TunnelSim _sim;
        readonly Rng _rng;
        public readonly CrewGeo Geo = new CrewGeo();

        /// <summary>편성 — 직업 선택 화면에서 만든다. 런이 끝나도 유지되어 다음 런에 재사용.</summary>
        public readonly List<RoleId> Roster = new List<RoleId>();
        public readonly List<CrewMember> Members = new List<CrewMember>();
        public readonly List<CrewTurret> Turrets = new List<CrewTurret>();
        public readonly List<CrewNode> Nodes = new List<CrewNode>();
        public readonly Dictionary<int, CrewCrack> Cracks = new Dictionary<int, CrewCrack>();
        public readonly List<CrewMark> Marks = new List<CrewMark>();
        public bool Enabled = true;
        public bool Debug;
        int _seq;

        public event Action<CrewFxEvent> Fx;
        public event Action<string> Toast;
        public event Action<CrewMember> LeveledUp;

        public AiCrewSystem(TunnelSim sim, uint seed = 0xA1C3E5) { _sim = sim; _rng = new Rng(seed); }

        // ───────────────────────────── 접근 축약
        WorldGrid W => _sim.World;
        PlayerState P => _sim.Player;
        double Now => _sim.RunTime;
        double Rnd(double a, double b) => _rng.Range(a, b);
        bool Roll(double p) => _rng.NextDouble() < p;
        bool Unbreakable(TileType t) => TileTypes.IsBedrock(t);
        TileType CellOf(int c, int r) => W.InBounds(c, r) ? W.At(c, r) : TileType.Rock;
        bool SolidAt(Vec2 p) { var (c, r) = WorldGrid.ToCell(p); return W.IsSolid(c, r); }
        bool CanSee(Vec2 a, Vec2 b) => SightUtil.IsClear(W, a, b);
        bool SeenAt(Vec2 p) { var (c, r) = WorldGrid.ToCell(p); return _sim.Los == null || _sim.Los.IsSeen(c, r); }
        bool Playing => Enabled && W != null && Members.Count > 0 && _sim.Phase == GamePhase.Playing;
        static readonly string[] RoleKo = { "드릴러", "거너", "스카우트", "엔지니어" };
        static readonly string[] RoleCol = { "#ffd36e", "#ff8d72", "#7febd0", "#c7a0ff" };
        public static string NameOf(RoleId r) => RoleKo[(int)r];
        public static string ColorOf(RoleId r) => RoleCol[(int)r];

        void Ring(Vec2 at, string col, double r1, double size = 6) => Fx?.Invoke(new CrewFxEvent { Kind = CrewFxKind.Ring, At = at, Color = col, Radius = r1, Size = size });
        void Burst(Vec2 at, int n, string col, double speedPx) => Fx?.Invoke(new CrewFxEvent { Kind = CrewFxKind.Burst, At = at, Count = n, Color = col, Size = speedPx });
        void Flash(Vec2 at, double r, string col) => Fx?.Invoke(new CrewFxEvent { Kind = CrewFxKind.Flash, At = at, Radius = r, Color = col });
        void Text(Vec2 at, string s, string col, double px) => Fx?.Invoke(new CrewFxEvent { Kind = CrewFxKind.Text, At = at, Label = s, Color = col, Size = px });
        void Say(CrewMember m, string s) { m.Say = s; m.SayT = 1.8; }

        // ───────────────────────────── AI_HUMANIZE_V1
        void UpdateMood(CrewMember m, double dt)
        {
            m.MoodT -= dt;
            if (m.MoodT > 0) return;
            m.MoodT = Rnd(8, 20); m.Mood = Rnd(.72, 1.28);
        }

        /// <summary>
        /// 의도 게이트 — 준비가 끝나도 곧바로 쓰지 않는다. 망설임 시간을 굴리고 결심의 순간에 확률 판정을 한 번 더 한다.
        /// 대기는 게임 시간 절대 시각으로 잡는다 (decide 가 0.12~0.26초에 한 번만 돌기 때문).
        /// </summary>
        bool Intent(CrewMember m, string tag, bool ready, double min = .4, double max = 2.6, double p = .7, double eagerMul = 1)
        {
            if (!ready) { m.Wait[tag] = -1; return false; }
            double eager = Math.Max(.35, m.Pers.Eager * m.Mood * eagerMul);
            if (!m.Wait.TryGetValue(tag, out double w) || w < 0) { m.Wait[tag] = Now + Rnd(min, max) / eager; return false; }
            if (Now < w) return false;
            double pp = Math.Min(.97, p * (.62 + m.Pers.Discipline * .48));
            if (Roll(pp)) { m.Wait[tag] = -1; return true; }
            m.Wait[tag] = Now + Rnd(min, max) * Rnd(1.3, 2.8);
            return false;
        }

        bool LitAt(Vec2 p)
        {
            foreach (var f in _sim.Roles.Flares) if (Vec2.Distance(f.Position, p) < f.LightRadius * .8) return true;
            if (_sim.Generation?.Lamps != null)
                foreach (var (c, r) in _sim.Generation.Lamps) if (Vec2.Distance(WorldGrid.CellCenter(c, r), p) < SimTuning.PxCells(94) * .8) return true;
            return false;
        }

        /// <summary>딴짓 — 잠깐 멈춰 주위를 둘러본다.</summary>
        bool IdleBeat(CrewMember m, double dt, bool spin = true)
        {
            if (m.IdleT > 0)
            {
                m.IdleT -= dt; m.Velocity *= .7;
                if (spin) { m.Aim += dt * m.IdleDir * Rnd(.6, 1.4); m.Face = Math.Cos(m.Aim) < 0 ? -1 : 1; }
                return true;
            }
            if (Roll(dt * .07 * (1.45 - m.Pers.Focus) * m.Mood))
            {
                m.IdleT = Rnd(.35, 1.5); m.IdleDir = Roll(.5) ? 1 : -1;
                if (Roll(.22)) Say(m, new[] { "음...", "이쪽인가", "조용하네", "..." }[_rng.NextInt(4)]);
                return true;
            }
            return false;
        }

        /// <summary>하던 일을 하면서도 눈앞의 적에겐 한두 발 쏜다 — 항상은 아니다.</summary>
        void Potshot(CrewMember m, double dt)
        {
            if (m.ReloadLeft > 0 || m.GunCd > 0 || m.Digging) return;
            if (!Roll(dt * 1.5 * m.Pers.Aggression * m.Mood)) return;
            var near = EnemiesNear(m, Math.Min(m.Kit.Range, 7.5), true);
            if (near.Count == 0) return;
            AimTo(m, near[0].Position); Fire(m, near[0].Position);
        }

        // ───────────────────────────── 드릴러 — 기반암 균열 (AI 전용 크랙 맵)
        double CrackNeed(TileType t) => (t == TileType.Core ? 24 : 15) * (1 + Math.Max(0, _sim.Depth - 1) * .22);
        bool BedrockAt(int c, int r) => W.InInterior(c, r) && Unbreakable(W.At(c, r));

        void BreakBedrock(CrewMember m, int c, int r, TileType type)
        {
            int k = W.Index(c, r);
            if (W.AtIndex(k) == TileType.Empty) return;
            W.ClearSilent(c, r);          // 장악도(floorBroken)에는 넣지 않는다 — 사람 규칙과 같다
            Cracks.Remove(k);
            _sim.Los?.MarkDirty();
            var at = WorldGrid.CellCenter(c, r);
            Ring(at, "#FFF3D6", 1.5, 10); Burst(at, 18, "#FFD36E", 240);
            AwardXp(m, type == TileType.Core ? XpGate.XpCrackCore : XpGate.XpCrack, XpKind.Dig, at: at);
            Say(m, type == TileType.Core ? "코어 관통" : "기반암 관통");
        }

        bool CrackPressure(CrewMember m, int c, int r, double dt, bool boost)
        {
            if (!BedrockAt(c, r)) return false;
            int k = W.Index(c, r); var type = W.At(c, r);
            if (!Cracks.TryGetValue(k, out var cur)) Cracks[k] = cur = new CrewCrack { Type = type, Last = Now, Owner = m };
            cur.P = Math.Min(1, cur.P + dt * m.Kit.DigMul * 1.15 * (boost ? 2.3 : 1) / CrackNeed(type));
            cur.Last = Now; cur.Owner = m;
            m.Digging = true; m.Drill = 1;
            int stage = Math.Min(4, (int)Math.Floor(cur.P * 4));
            var at = WorldGrid.CellCenter(c, r);
            if (stage > cur.Stage)
            {
                cur.Stage = stage;
                Text(at + new Vec2(0, .52), $"균열 {(int)(cur.P * 100)}%", "#FFD36E", 13);
                Ring(at, "#FFD36E", .45 + stage * .16, 4 + stage * 2);
            }
            if (Roll(dt * 6)) Fx?.Invoke(new CrewFxEvent { Kind = CrewFxKind.Chunks, At = at, Count = 2, Color = "#C8B8E8", Size = 105, Dir = Vec2.FromAngle(m.Aim) });
            if (cur.P >= 1) BreakBedrock(m, c, r, type);
            return true;
        }

        void UpdateCracks(double dt)
        {
            if (Cracks.Count == 0) return;
            var dead = new List<int>();
            foreach (var kv in Cracks)
            {
                if (!Unbreakable(W.AtIndex(kv.Key))) { dead.Add(kv.Key); continue; }
                if (Now - kv.Value.Last < .25) continue;
                kv.Value.P -= dt * .016;
                if (kv.Value.P <= 0) dead.Add(kv.Key);
            }
            foreach (var k in dead) Cracks.Remove(k);
        }

        /// <summary>기반암 목표 — "뚫으면 새 공간이 열리는" 벽만 고른다.</summary>
        CrewGoal PickBedrock(CrewMember m, int radius)
        {
            int comp = Geo.CompOf(m.Position);
            if (comp < 0) return null;
            var (oc, orr) = WorldGrid.ToCell(m.Position);
            CrewGoal best = null; double bs = -1e9;
            for (int dr = -radius; dr <= radius; dr++) for (int dc = -radius; dc <= radius; dc++)
            {
                int c = oc + dc, r = orr + dr;
                if (!BedrockAt(c, r)) continue;
                bool touch = false;
                for (int i = 0; i < 4 && !touch; i++)
                {
                    int nc = c + (i == 0 ? 1 : i == 1 ? -1 : 0), nr = r + (i == 2 ? 1 : i == 3 ? -1 : 0);
                    if (!W.InInterior(nc, nr)) continue;
                    if (W.At(nc, nr) == TileType.Empty && Geo.CompOf(WorldGrid.CellCenter(nc, nr)) == comp) touch = true;
                }
                if (!touch) continue;
                Cracks.TryGetValue(W.Index(c, r), out var cur);
                var at = WorldGrid.CellCenter(c, r);
                double s = (cur != null ? cur.P * 26 : 0) + (SeenAt(at) ? 0 : 12) + (W.At(c, r) == TileType.Core ? -9 : 0)
                         - Math.Sqrt(dc * dc + dr * dr) * 2.2 + Rnd(-5, 5);
                if (s > bs) { bs = s; best = new CrewGoal { Kind = CrewGoalKind.Crack, C = c, R = r, At = at, Label = "기반암 균열" }; }
            }
            return best;
        }

        // ── 드릴러 — 돌파 파기 (사람 Q: 0.55초 창, 전방 3칸)
        void StartBreach(CrewMember m)
        {
            m.BreachT = .55; m.BreachCd = (m.Kit.BreachCd > 0 ? m.Kit.BreachCd : 8) * Rnd(.85, 1.35);
            Fx?.Invoke(new CrewFxEvent { Kind = CrewFxKind.Kick, Size = 6, Dir = Vec2.FromAngle(m.Aim) });
            Ring(m.Position, "#FFD36E", 1.25, 8);
            Say(m, "돌파!");
        }
        void UpdateBreach(CrewMember m, double dt)
        {
            if (m.BreachT <= 0) return;
            m.BreachT = Math.Max(0, m.BreachT - dt);
            var dir = Vec2.FromAngle(m.Aim); var seen = new HashSet<int>();
            m.Digging = true; m.Drill = 1;
            for (double len = .5; len <= 3.1; len += .5)
            {
                var (c, r) = WorldGrid.ToCell(m.Position + dir * len);
                if (!W.InInterior(c, r)) continue;
                int k = W.Index(c, r);
                if (!seen.Add(k)) continue;
                var t = W.At(c, r);
                if (t == TileType.Empty) continue;
                if (Unbreakable(t)) { CrackPressure(m, c, r, dt * 2.2, true); continue; }
                DamageWall(m, c, r, SimTuning.DrillDps * SimTuning.DrillDamageMul * m.Kit.DigMul * 2.8 * dt, dir);
            }
        }

        /// <summary>AI 가 벽을 깎는다 — 크레딧은 BreakSource 로 TunnelSim 에 전달된다 (사람 XP 를 올리지 않는다).</summary>
        void DamageWall(CrewMember m, int c, int r, double dv, Vec2 dir)
        {
            _sim.BreakSource = m;
            try { W.Damage(c, r, dv, dir); } finally { _sim.BreakSource = null; }
        }
        void HurtEnemyBy(CrewMember m, EnemyState e, double dmg, Vec2 dir, bool byTurret = false)
        {
            _sim.Enemies.DamageSource = m;
            try { _sim.Enemies.HurtEnemy(e, dmg, dir, m.Position, byTurret); } finally { _sim.Enemies.DamageSource = null; }
        }

        // ── 거너 — 파쇄탄 표적 (뭉친 적·정예)
        EnemyState BreakerCluster(CrewMember m, List<(EnemyState e, double d, double dp, double prio)> foes)
        {
            EnemyState best = null; double bs = 4;
            foreach (var f in foes)
            {
                var e = f.e; double d = Vec2.Distance(e.Position, m.Position);
                if (d > 6.4 || d < 2.0) continue;
                int n = 0; foreach (var o in _sim.Enemies.Enemies) if (o.Alive && Vec2.Distance(o.Position, e.Position) < 1.7) n++;
                double s = n * 10 + (e.IsApex ? 16 : 0) - d + Rnd(-3, 3);
                if (s > bs) { bs = s; best = e; }
            }
            return best;
        }

        // ── 거너 — 통로 경계 자리
        Vec2? PickWatchPost(CrewMember m)
        {
            double from = m.LastFoeDir ?? Rnd(0, Math.PI * 2);
            Vec2? best = null; double bs = -1e9;
            for (int i = 0; i < 10; i++)
            {
                double a = from + Rnd(-1.1, 1.1) + (i > 6 ? Rnd(-Math.PI, Math.PI) : 0), d = Rnd(1.8, 4.6);
                var p = P.Position + Vec2.FromAngle(a) * d;
                if (SolidAt(p)) continue;
                int open = 0;
                for (int j = 0; j < 8; j++) if (!SolidAt(p + Vec2.FromAngle(j / 8.0 * Math.PI * 2) * 1.2)) open++;
                double s = open * 2.2 + (CanSee(p, P.Position) ? 4 : 0) - Vec2.Distance(p, m.Position) + Rnd(-3, 3);
                if (s > bs) { bs = s; best = p; }
            }
            return best;
        }

        // ── 스카우트 — 플레어 투척 (던지는 손도 정확하지 않다)
        void ThrowFlare(CrewMember m, Vec2 target, string label)
        {
            double a = (target - m.Position).Angle + Rnd(-.18, .18);
            var f = m.Position + Vec2.FromAngle(a) * Rnd(2.2, 3.0);
            _sim.Roles.Flares.Add(new Flare { Position = f, Ttl = 22, MaxTtl = 22, LightRadius = Math.Max(SimTuning.PxCells(94) * 1.45, 4.2), VisionRange = 5 });
            _sim.Los?.MarkDirty();
            Flash(f, .84, "#FFDCA0"); Ring(f, "#FFD080", 1.6, 6);
            m.FlareCd = m.Kit.FlareCd * Rnd(.85, 1.35);
            AimTo(m, f);
            ReconAward(m, f);
            Say(m, label ?? "플레어");
        }

        // ── 스카우트 — 정찰 펄스: 벽 너머 광맥·위협 표시 (§9.1)
        bool ScoutPulse(CrewMember m)
        {
            const int R = 5; var (cc, rr) = WorldGrid.ToCell(m.Position);
            int veins = 0, foes = 0;
            for (int dr = -R; dr <= R; dr++) for (int dc = -R; dc <= R; dc++)
            {
                int c = cc + dc, r = rr + dr;
                if (!W.InBounds(c, r) || Math.Sqrt(dc * dc + dr * dr) > R + .15) continue;
                var t = W.At(c, r);
                if (t == TileType.Ore || t == TileType.Gem || t == TileType.Crys) { Marks.Add(new CrewMark { At = WorldGrid.CellCenter(c, r), Ttl = Rnd(6, 9) }); veins++; }
            }
            foreach (var e in _sim.Enemies.Enemies)
            {
                if (!e.Alive || Vec2.Distance(e.Position, m.Position) > R) continue;
                Marks.Add(new CrewMark { At = e.Position, Threat = true, Ttl = Rnd(4, 6), Enemy = e }); foes++;
            }
            if (Marks.Count > 90) Marks.RemoveRange(0, Marks.Count - 90);
            m.PulseCd = (m.Kit.PulseCd > 0 ? m.Kit.PulseCd : 9) * Rnd(.85, 1.4);
            Ring(m.Position, "#7FEBD0", R, 10);
            ReconAward(m, m.Position);
            Say(m, veins > 0 ? $"광맥 {veins}개" : foes > 0 ? $"적 {foes}기" : "펄스");
            return true;
        }
        void UpdateMarks(double dt)
        {
            for (int i = Marks.Count - 1; i >= 0; i--)
            {
                var k = Marks[i]; k.Ttl -= dt;
                if (k.Threat && k.Enemy != null) { if (!k.Enemy.Alive) { Marks.RemoveAt(i); continue; } k.At = k.Enemy.Position; }
                if (k.Ttl <= 0) Marks.RemoveAt(i);
            }
        }

        // ── 스카우트 — 정찰 목표
        Vec2? PickScoutSpot(CrewMember m)
        {
            Vec2? best = null; double bs = -1e9;
            for (int i = 0; i < 18; i++)
            {
                double a = Rnd(0, Math.PI * 2), d = Rnd(5, 14);
                var p = P.Position + Vec2.FromAngle(a) * d;
                if (p.X < 1.5 || p.Y < 1.5 || p.X > W.Cols - 1.5 || p.Y > W.Rows - 1.5) continue;
                if (SolidAt(p)) continue;
                double s = (SeenAt(p) ? 0 : 14) + d * .8 + Rnd(-4, 4);
                if (s > bs) { bs = s; best = p; }
            }
            return best != null && bs > 7 ? best : null;   // 볼 게 없으면 안 나간다
        }

        // ── 스카우트 — 그래플 훅
        bool TryGrapple(CrewMember m, Vec2 target)
        {
            if (m.Dashing || m.GrappleCd > 0) return false;
            var dir = (target - m.Position).Normalized; double reach = 0;
            for (double d = .24; d <= 5; d += .16) { if (SolidAt(m.Position + dir * d)) break; reach = d; }
            if (reach < 2.2) return false;
            const double dur = .22;
            m.DashVel = dir * (reach / dur); m.DashT = dur;
            m.DashCd = Math.Max(m.DashCd, .4);
            m.GrappleCd = (m.Kit.GrappleCd > 0 ? m.Kit.GrappleCd : 6) * Rnd(.8, 1.5);
            Ring(m.Position + dir * reach, "#7FEBD0", .5, 5);
            Say(m, "그래플");
            return true;
        }

        // ── 엔지니어 — 센트리 자리: 사격선이 트인 곳. 다만 항상 최적을 고르진 않는다
        Vec2? PickTurretSpot(CrewMember m, Vec2? focus)
        {
            var cands = new List<(Vec2 p, double s)>();
            for (int i = 0; i < 14; i++)
            {
                var p = m.Position + Vec2.FromAngle(Rnd(0, Math.PI * 2)) * Rnd(.7, 2.8);
                if (SolidAt(p)) continue;
                int open = 0;
                for (int j = 0; j < 10; j++) if (CanSee(p, p + Vec2.FromAngle(j / 10.0 * Math.PI * 2) * 4)) open++;
                double s = open * 1.8 + Rnd(-4, 4);
                foreach (var n in Nodes) if (n.Owner == m && Vec2.Distance(n.Position, p) <= n.Radius) { s += 8; break; }
                if (focus.HasValue) s += CanSee(p, focus.Value) ? 12 : -6;
                foreach (var t in Turrets) if (Vec2.Distance(t.Position, p) < 1.6) s -= 7;
                cands.Add((p, s));
            }
            if (cands.Count == 0) return null;
            cands.Sort((a, b) => b.s.CompareTo(a.s));
            int idx = Roll(.62) ? 0 : (Roll(.6) ? 1 : 2);
            return cands[Math.Min(idx, cands.Count - 1)].p;
        }

        // ───────────────────────────── 편성
        public int Count(RoleId role) { int n = 0; foreach (var r in Roster) if (r == role) n++; return n; }
        public bool Add(RoleId role)
        {
            if (Roster.Count >= Max) return false;
            Roster.Add(role);
            if (Playing || (Enabled && W != null && _sim.Phase == GamePhase.Playing)) SpawnMember(role, live: true);   // 런 도중에도 즉시 합류
            return true;
        }
        public bool Remove(RoleId role)
        {
            int i = Roster.LastIndexOf(role);
            if (i < 0) return false;
            Roster.RemoveAt(i);
            for (int j = Members.Count - 1; j >= 0; j--) if (Members[j].Role == role) { Members.RemoveAt(j); break; }
            return true;
        }
        public void Clear() { Roster.Clear(); Members.Clear(); Turrets.Clear(); Nodes.Clear(); }

        /// <summary>런 도중 직업 교체 (관전 Esc 교대 — 원본 AICREW.setRole). 키트·탄창·체력 비율을 새 직업으로, 성향·레벨·위치는 유지.</summary>
        public void SetRole(CrewMember m, RoleId role)
        {
            int ri = Roster.IndexOf(m.Role); if (ri >= 0) Roster[ri] = role;
            var kit = CrewKit.For(role);
            double hpRatio = m.HpMax > 0 ? m.Hp / m.HpMax : 1;
            m.Role = role; m.Kit = kit;
            m.HpMax = kit.Hp; m.Hp = Math.Max(1, kit.Hp * hpRatio);
            m.Ammo = kit.Mag; m.Mag = kit.Mag; m.ReloadTime = kit.Reload; m.ReloadLeft = 0;
            m.Goal = null; m.Path.Clear(); m.PathKey = ""; m.PathAge = 0; m.Digging = false; m.Drill = 0;
            m.QCd = m.ECd = 0;
            Geo.ProgressReset(m);
        }

        // ───────────────────────────── 런 시작 / 종료 / 층
        public void OnRunStart()
        {
            Members.Clear(); Turrets.Clear(); Nodes.Clear(); Cracks.Clear(); Marks.Clear(); _tgt.Clear();
            Geo.Bind(W);
            if (!Enabled || Roster.Count == 0 || W == null) return;
            foreach (var role in Roster) SpawnMember(role, live: false);
            if (Members.Count > 0)
            {
                var names = new List<string>(); foreach (var m in Members) names.Add(RoleKo[(int)m.Role]);
                Toast?.Invoke($"AI 크루 {Members.Count}명 합류 · {string.Join(", ", names)}");
            }
        }
        public void OnRunEnd() { Members.Clear(); Turrets.Clear(); Nodes.Clear(); }

        /// <summary>지층이 바뀌면 지형이 새로 생긴다 — 크루를 사람 옆으로 다시 모은다. 성향은 유지, 기분만 다시 굴린다.</summary>
        public void OnFloorInit()
        {
            Geo.Bind(W); _tgt.Clear();
            foreach (var m in Members)
            {
                m.Position = FreeSpotNear(P.Position, 1.4); m.Velocity = Vec2.Zero;
                m.Path.Clear(); m.Goal = null; m.MineTarget = null;
                m.Hp = Math.Max(m.Hp, m.HpMax * .6); m.Down = false; m.DownT = 0;
                m.Ammo = m.Mag; m.ReloadLeft = 0;
                m.XpCapped = 0; m.Sectors = null; m.LootCd = 0; m.Dodging = false;
                m.Wait.Clear(); m.CrackTarget = null; m.CrackT = 0; m.BreachT = 0;
                m.Watch = null; m.WatchT = 0; m.IdleT = 0; m.MoodT = Rnd(1, 6);
                m.Boarded = false; m.BoardT = 0; m.DashT = 0;
            }
            Turrets.Clear(); Nodes.Clear(); Cracks.Clear(); Marks.Clear();
        }

        Vec2 FreeSpotNear(Vec2 at, double cells)
        {
            for (int i = 0; i < 40; i++)
            {
                var p = at + Vec2.FromAngle(Rnd(0, Math.PI * 2)) * (cells * (.35 + Rnd(0, .9)));
                if (!SolidAt(p)) return p;
            }
            return at + new Vec2(Rnd(-.4, .4), Rnd(-.4, .4));
        }

        CrewMember SpawnMember(RoleId role, bool live)
        {
            var kit = CrewKit.For(role);   // 사본 — 특성은 이 사본을 바꾼다
            var p = FreeSpotNear(P.Position, 1.5);
            var m = new CrewMember
            {
                Id = ++_seq, Role = role, Kit = kit, Position = p, Last = p,
                Hp = kit.Hp, HpMax = kit.Hp, Ammo = kit.Mag, Mag = kit.Mag, ReloadTime = kit.Reload,
                FlareCd = 2, TurretCd = 1.5, NodeCd = 3,
                Pers = CrewPersona.Roll(role, _rng), MoodT = Rnd(2, 9),
                BreachCd = Rnd(1, 5), PulseCd = Rnd(1, 6), GrappleCd = Rnd(0, 3), ExploreCd = Rnd(2, 9), CrackPatience = Rnd(9, 20),
            };
            Members.Add(m);
            if (live) Toast?.Invoke($"AI {RoleKo[(int)role]} 합류");
            return m;
        }

        // ───────────────────────────── 지형·경로 (다익스트라 — 벽은 "뚫는 데 드는 시간"만큼 비싼 통로)
        double DigCost(TileType t, CrewKit kit)
        {
            if (t == TileType.Empty) return 1;
            if (Unbreakable(t)) return double.PositiveInfinity;
            return 1 + (W.MaxHp(t) / 100.0) * kit.PathDigCost;
        }

        double[] _dist; int[] _prev; readonly List<(double d, int k)> _heap = new List<(double, int)>();
        void HeapPush((double d, int k) v)
        {
            _heap.Add(v); int i = _heap.Count - 1;
            while (i > 0) { int p = (i - 1) / 2; if (_heap[p].d <= _heap[i].d) break; (_heap[p], _heap[i]) = (_heap[i], _heap[p]); i = p; }
        }
        (double d, int k) HeapPop()
        {
            var top = _heap[0]; int last = _heap.Count - 1; _heap[0] = _heap[last]; _heap.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int l = i * 2 + 1, r = l + 1, s = i;
                if (l < _heap.Count && _heap[l].d < _heap[s].d) s = l;
                if (r < _heap.Count && _heap[r].d < _heap[s].d) s = r;
                if (s == i) break;
                (_heap[s], _heap[i]) = (_heap[i], _heap[s]); i = s;
            }
            return top;
        }

        public List<int> FindPath(int sc, int sr, int gc, int gr, CrewKit kit, CrewMember self)
        {
            int N = W.CellCount;
            if (_dist == null || _dist.Length != N) { _dist = new double[N]; _prev = new int[N]; }
            Array.Fill(_dist, double.PositiveInfinity); Array.Fill(_prev, -1);
            if (!W.InBounds(sc, sr) || !W.InBounds(gc, gr)) return null;
            int start = W.Index(sc, sr), goal = W.Index(gc, gr);
            if (start == goal) return new List<int>();
            // 동료가 서 있는 칸은 통행 비용을 올린다 — 우회로가 있으면 줄 서지 않고 돌아간다
            var occupied = new HashSet<int>();
            foreach (var o in Members) if (o != self && !o.Down) { var (oc, orr) = WorldGrid.ToCell(o.Position); if (W.InBounds(oc, orr)) occupied.Add(W.Index(oc, orr)); }
            if (!P.Downed) { var (pc, pr) = WorldGrid.ToCell(P.Position); if (W.InBounds(pc, pr)) occupied.Add(W.Index(pc, pr)); }
            _dist[start] = 0; _heap.Clear(); HeapPush((0, start));
            int guard = 0;
            while (_heap.Count > 0 && guard++ < 24000)
            {
                var (d, k) = HeapPop();
                if (d > _dist[k]) continue;
                if (k == goal) break;
                int c = k % W.Cols, r = k / W.Cols;
                for (int i = 0; i < 4; i++)
                {
                    int nc = c + (i == 0 ? 1 : i == 1 ? -1 : 0), nr = r + (i == 2 ? 1 : i == 3 ? -1 : 0);
                    if (!W.InInterior(nc, nr)) continue;
                    int nk = W.Index(nc, nr);
                    double w = DigCost(W.AtIndex(nk), kit);
                    if (double.IsInfinity(w)) continue;
                    if (occupied.Contains(nk)) w += 3;
                    double nd = d + w;
                    if (nd < _dist[nk]) { _dist[nk] = nd; _prev[nk] = k; HeapPush((nd, nk)); }
                }
            }
            if (double.IsInfinity(_dist[goal])) return null;
            var out_ = new List<int>();
            for (int k = goal; k != -1 && k != start; k = _prev[k]) out_.Add(k);
            out_.Reverse();
            return out_;
        }

        int EnsurePath(CrewMember m, Vec2 goal)
        {
            var (c0, r0) = WorldGrid.ToCell(m.Position); var (gc, gr) = WorldGrid.ToCell(goal);
            string key = gc + "," + gr;
            if (key != m.PathKey || m.PathAge <= 0 || m.Path.Count == 0)
            {
                m.Path.Clear();
                var p = FindPath(c0, r0, gc, gr, m.Kit, m);
                if (p != null) m.Path.AddRange(p);
                m.PathKey = key; m.PathAge = .4;
            }
            while (m.Path.Count > 0)
            {
                int k = m.Path[0]; int c = k % W.Cols, r = k / W.Cols;
                if (W.AtIndex(k) == TileType.Empty && Vec2.Distance(WorldGrid.CellCenter(c, r), m.Position) < .5) m.Path.RemoveAt(0);
                else break;
            }
            return m.Path.Count > 0 ? m.Path[0] : -1;
        }

        // ───────────────────────────── 이동
        void Steer(CrewMember m, Vec2 target, double speedMul = 1)
        {
            var delta = target - m.Position; double d = delta.Length;
            if (d < .06) { m.Velocity *= .7; return; }
            var n = delta / d;
            if (m.Jitter > 0) n = Vec2.FromAngle(m.JitterA);
            // 동료끼리 겹치지 않게 밀어낸다
            Vec2 push = Vec2.Zero; double sep = SimTuning.PlayerRadius * 1.9;
            foreach (var o in Members)
            {
                if (o == m) continue;
                var off = m.Position - o.Position; double od = off.Length;
                if (od > .02 && od < sep) push += (off / od) * (1 - od / sep);
            }
            { var off = m.Position - P.Position; double od = off.Length; if (od > .02 && od < sep) push += (off / od) * .8; }
            n += push * .9;
            double len = Math.Max(1e-9, n.Length);
            double spd = SimTuning.MoveSpeed * speedMul * m.Kit.MoveMul;
            m.Velocity = (n / len) * spd;
        }
        void MoveAwayFrom(CrewMember m, Vec2 t, double speedMul = 1) => Steer(m, m.Position * 2 - t, speedMul);

        void ApplyMotion(CrewMember m, double dt)
        {
            if (m.Dashing)
            {
                m.Position += m.DashVel * dt;
                CollisionSystem.Resolve(W, ref m.Position, SimTuning.PlayerRadius);
                m.DashT -= dt;
                return;
            }
            m.Position += m.Velocity * dt;
            CollisionSystem.Resolve(W, ref m.Position, SimTuning.PlayerRadius);
            m.Velocity *= .82;
        }

        void TryDash(CrewMember m, Vec2 dir)
        {
            if (m.Dashing || m.DashCd > 0) return;
            double d = Math.Max(1e-9, dir.Length);
            double dist = SimTuning.DashDistance * m.Kit.DashMul, dur = SimTuning.DashDuration;
            m.DashVel = (dir / d) * (dist / dur); m.DashT = dur; m.DashCd = SimTuning.DashCooldown;
        }

        /// <summary>적 투사체 회피 — 나에게 명중 코스인 탄을 감지하면 80% 확률로 옆으로 대시.</summary>
        readonly HashSet<EnemyShot> _dodged = new HashSet<EnemyShot>();
        void DodgeEnemyShot(CrewMember m)
        {
            if (m.Dashing || m.DashCd > 0 || _sim.Enemies.Shots.Count == 0) return;
            foreach (var s in _sim.Enemies.Shots)
            {
                var rel = m.Position - s.Position; double sv2 = s.Velocity.X * s.Velocity.X + s.Velocity.Y * s.Velocity.Y;
                if (sv2 < 1e-6) continue;
                double t = (rel.X * s.Velocity.X + rel.Y * s.Velocity.Y) / sv2;
                if (t < 0 || t > .55) continue;
                var a = s.Position + s.Velocity * t - m.Position;
                if (a.Length > SimTuning.PlayerRadius + s.Radius + .3) continue;
                if (!_dodged.Add(s)) continue;
                if (Roll(.8))
                {
                    var n = a * -1;
                    if (n.Length < .04) { double sv = Math.Sqrt(sv2); n = new Vec2(-s.Velocity.Y / sv, s.Velocity.X / sv); if (Roll(.5)) n *= -1; }
                    TryDash(m, n); Say(m, "회피");
                }
                break;
            }
            if (_dodged.Count > 64) _dodged.RemoveWhere(x => !_sim.Enemies.Shots.Contains(x));
        }

        // ───────────────────────────── 전투
        static double FireRange => SimTuning.TeCells(280) * 1.2 * .92;

        /// <summary>위협 목록 — ① 내가 보는 적 ② 사람이 보는 적 ③ 사람에게 붙은 적 (팀 단위 인지).</summary>
        List<(EnemyState e, double d, double dp, double prio)> Threats(CrewMember m)
        {
            var out_ = new List<(EnemyState, double, double, double)>();
            double alertR = m.Kit.Alert, partyR = 9;
            foreach (var e in _sim.Enemies.Enemies)
            {
                if (!e.Alive) continue;
                double dm = Vec2.Distance(e.Position, m.Position), dp = Vec2.Distance(e.Position, P.Position);
                bool known = false; double prio = 0;
                if (dp < partyR && CanSee(P.Position, e.Position)) { known = true; prio = 100 - dp; }
                else if (dm < alertR && CanSee(m.Position, e.Position)) { known = true; prio = 60 - dm; }
                else if (dp < alertR && CanSee(P.Position, e.Position)) { known = true; prio = 40 - dp; }
                if (!known) continue;
                if (e.Attack != AttackPhase.None) prio += 12;
                if (e.IsApex) prio += 8;
                out_.Add((e, dm, dp, prio));
            }
            out_.Sort((a, b) => b.Item4.CompareTo(a.Item4));
            return out_;
        }
        List<EnemyState> EnemiesNear(CrewMember m, double cells, bool needSight)
        {
            var out_ = new List<(EnemyState e, double d)>();
            foreach (var e in _sim.Enemies.Enemies)
            {
                if (!e.Alive) continue;
                double d = Vec2.Distance(e.Position, m.Position);
                if (d > cells) continue;
                if (needSight && !CanSee(m.Position, e.Position)) continue;
                out_.Add((e, d));
            }
            out_.Sort((a, b) => a.d.CompareTo(b.d));
            var r = new List<EnemyState>(out_.Count); foreach (var x in out_) r.Add(x.e);
            return r;
        }

        /// <summary>AI 사격 — 사람의 특성 배율을 타지 않도록 Owner 를 달아 쏜다. 조준 흔들림은 크루마다 다르다.</summary>
        bool Fire(CrewMember m, Vec2 target)
        {
            if (m.GunCd > 0 || m.ReloadLeft > 0) return false;
            if (m.Ammo <= 0) { m.ReloadLeft = m.ReloadTime; return false; }
            m.Ammo--;
            double err = m.Pers.AimErr * (m.Digging ? 1.6 : 1);
            double aimOffset = m.Kit.Accuracy < 1.0
                ? ProjectileSystem.RollInaccuracy(_rng, m.Kit.Accuracy) * (m.Digging ? 1.15 : 1.0)
                : Rnd(-err, err);
            double a = (target - m.Position).Angle + aimOffset;
            var dir = Vec2.FromAngle(a);
            _sim.Projectiles.Emit(new Projectile
            {
                Position = m.Position + dir * ProjectileSystem.CharacterMuzzleOffset, Velocity = dir * SimTuning.TeCells(ProjectileSystem.BaseSpeedPx("standard")), Life = ProjectileSystem.BaseLife("standard"),
                Power = 1, Owner = m, AiMul = m.Kit.GunMul, VisualId = "standard",
            }, a, m.Position);
            m.GunCd = m.Kit.FireCd;
            if (m.Ammo <= 0) m.ReloadLeft = m.ReloadTime;
            return true;
        }

        /// <summary>드릴 접촉 피해 — 드릴러가 벽을 파는 김에 붙은 적을 갈아버린다.</summary>
        void DrillMelee(CrewMember m, double dt)
        {
            if (!m.Kit.DrillMelee || !m.Digging) return;
            var dir = Vec2.FromAngle(m.Aim); var tip = m.Position + dir * SimTuning.DrillTip;
            foreach (var e in _sim.Enemies.Enemies)
            {
                if (!e.Alive || Vec2.Distance(tip, e.Position) >= e.Radius + SimTuning.PxCells(8)) continue;
                HurtEnemyBy(m, e, SimTuning.DrillDps * SimTuning.EnemyDrillMul * SimTuning.DrillDamageMul * m.Kit.DigMul * dt, dir);
            }
        }

        // ───────────────────────────── 개인 성장 (§5.2)
        int AwardXp(CrewMember m, double baseAmt, XpKind kind, bool capped = false, Vec2? at = null)
        {
            if (baseAmt <= 0 || m.Down) return 0;
            if (capped && m.XpCapped >= XpGate.FloorCap) return 0;
            int gain = Math.Max(1, JsMath.Round(baseAmt * XpGate.WeightFor(kind, m.Role)));
            m.Xp += gain;
            if (capped) m.XpCapped += gain;
            if (at.HasValue && Roll(.45)) Text(at.Value + new Vec2(0, .48), "+" + gain, RoleCol[(int)m.Role], 10);
            CheckLevel(m);
            return gain;
        }
        void CheckLevel(CrewMember m)
        {
            int guard = 0;
            while (m.Xp >= m.XpNeed && guard++ < 4)
            {
                m.Xp -= m.XpNeed; m.Level++; m.XpNeed = XpGate.NeedFor(m.Level);
                PickTrait(m);
                LeveledUp?.Invoke(m);
            }
        }
        void PickTrait(CrewMember m)
        {
            int cap = CrewTraits.MaxTier(m.Level);
            var pool = new List<(CrewTraitDef t, double key)>();
            foreach (var t in CrewTraits.All)
                if (t.Role == m.Role && t.Tier <= cap && !m.TraitIds.Contains(t.Id) && (t.Ok == null || t.Ok(m))) pool.Add((t, t.Tier + Rnd(0, 1.4)));
            if (pool.Count == 0) { ApplyTrait(m, CrewTraits.Basic, repeatable: true); return; }
            pool.Sort((a, b) => b.key.CompareTo(a.key));   // 높은 티어를 선호하되 결정적이지 않게
            ApplyTrait(m, pool[0].t, repeatable: false);
        }
        void ApplyTrait(CrewMember m, CrewTraitDef t, bool repeatable)
        {
            t.Apply(m, this);
            if (!repeatable) m.TraitIds.Add(t.Id);
            m.Traits.Add(t.Name);
            m.ReloadTime = m.Kit.Reload; m.Mag = m.Kit.Mag;
            Text(m.Position + new Vec2(0, 1.04), $"Lv{m.Level} · {t.Name}", RoleCol[(int)m.Role], 14);
            Ring(m.Position, RoleCol[(int)m.Role], 1.1, 6);
            Say(m, $"Lv{m.Level} {t.Name}");
            Toast?.Invoke($"AI {RoleKo[(int)m.Role]} Lv{m.Level} · {t.Name}");
        }

        /// <summary>처치 — 어떤 크루의 피해로 죽었는지는 EnemySystem.DamageSource 가 들고 있다.</summary>
        public void AwardKill(CrewMember m, EnemyState e, bool byTurret)
        {
            if (m == null || !Members.Contains(m)) return;
            if (e.IsBoss) { AwardXp(m, XpGate.XpKillBoss, XpKind.Combat, at: e.Position); return; }
            double tier = e.IsApex ? XpGate.ApexMul : 1;
            if (byTurret) AwardXp(m, XpGate.XpTurretKill * tier, XpKind.Support, at: e.Position);
            else AwardXp(m, XpGate.XpKill * tier, XpKind.Combat, at: e.Position);
        }
        /// <summary>정찰 경험치 — 사람의 scoutExploreXp 와 같은 규칙으로 '새 구역'에만 준다 (자기가속 방지).</summary>
        void ReconAward(CrewMember m, Vec2 at)
        {
            int sw = (int)Math.Ceiling(W.Cols / 3.0);
            var (c, r) = WorldGrid.ToCell(at);
            int key = c / 3 + (r / 3) * sw;
            m.Sectors ??= new HashSet<int>();
            if (!m.Sectors.Add(key)) { AwardXp(m, 1, XpKind.Recon, capped: true); return; }
            AwardXp(m, 2, XpKind.Recon, at: at);
        }
        void XpTrickle(CrewMember m, double dt)
        {
            m.XpTrickle += dt;
            if (m.XpTrickle < 6) return;
            m.XpTrickle = 0; AwardXp(m, 2, XpKind.Objective, capped: true);
        }
        /// <summary>AI 가 부순 블록의 개인 경험치 — TunnelSim.OnTileBroken 이 BreakSource 를 보고 호출한다.</summary>
        public void CreditBreak(CrewMember m, TileType type, Vec2 at)
        {
            if (m == null || !Members.Contains(m)) return;
            bool rare = type == TileType.Ore || type == TileType.Gem || type == TileType.Crys;
            AwardXp(m, type == TileType.Stone ? XpGate.XpStone : rare ? XpGate.XpRare : XpGate.XpDirt, XpKind.Dig, at: at);
        }

        // ───────────────────────────── 굴착
        bool DigAt(CrewMember m, int c, int r, double dt)
        {
            var t = CellOf(c, r);
            if (t == TileType.Empty || Unbreakable(t)) return false;
            var wp = WorldGrid.CellCenter(c, r);
            m.Aim = (wp - m.Position).Angle;
            if (Vec2.Distance(wp, m.Position) > 1.35) return false;
            if (!Geo.InReach(m.Position, c, r)) return false;
            m.Digging = true; m.Drill = 1;
            var n = Vec2.FromAngle(m.Aim);
            DamageWall(m, c, r, SimTuning.DrillDps * SimTuning.DrillDamageMul * m.Kit.DigMul * dt, n);
            if (Roll(dt * 5)) Burst(wp - n * .3, 2, "#E8CBA6", 70);
            return true;
        }

        bool FireBreaker(CrewMember m, int c, int r)
        {
            if (m.BreakerCd > 0) return false;
            var wp = WorldGrid.CellCenter(c, r);
            if (Vec2.Distance(wp, m.Position) > 7) return false;
            m.Breakers.Add(new CrewBreaker { At = wp, C = c, R = r, T = 2.0 });
            m.BreakerCd = m.Kit.BreakerCd;
            m.Aim = (wp - m.Position).Angle;
            Burst(m.Position + Vec2.FromAngle(m.Aim) * .4, 8, "#ff8d72", 170);
            return true;
        }
        void DetonateBreaker(CrewMember m, CrewBreaker b, bool early)
        {
            Ring(b.At, "#ff8d72", 1.8, 12); Burst(b.At, 24, "#ff6f45", 300);
            if (early) Text(b.At + new Vec2(0, .6), "조기 기폭", "#ffd36e", 15);
            int rad = m.Kit.BreakerRadius;
            for (int dr = -rad; dr <= rad; dr++) for (int dc = -rad; dc <= rad; dc++)
            {
                int c = b.C + dc, r = b.R + dr; var t = CellOf(c, r);
                if (t == TileType.Empty || Unbreakable(t) || !W.InInterior(c, r)) continue;
                DamageWall(m, c, r, W.MaxHp(t) * 1.15, new Vec2(dc != 0 ? dc : 1, dr));
            }
            double eRad = 1.8 + (rad - 1) * .65;
            foreach (var e in _sim.Enemies.Enemies)
            {
                if (!e.Alive) continue;
                double d = Vec2.Distance(e.Position, b.At);
                if (d >= eRad) continue;
                double fall = 1 - d / eRad;
                HurtEnemyBy(m, e, SimTuning.EnemyGunDamage * 2.2 * m.Kit.GunMul * (.55 + .75 * fall) * (early ? 1.25 : 1), (e.Position - b.At) / Math.Max(1e-6, d));
            }
        }
        void UpdateBreakers(CrewMember m, double dt)
        {
            m.BreakerCd = Math.Max(0, m.BreakerCd - dt);
            for (int i = m.Breakers.Count - 1; i >= 0; i--)
            {
                var b = m.Breakers[i]; b.T -= dt;
                if (b.T > 0)
                {
                    // 조기 기폭(사람 E) — 폭심에 적이 들어오면 지금 터뜨릴 수 있다. 매번 완벽하게 맞추지는 않는다
                    if (b.T < 1.7 && !b.NoEarly)
                    {
                        int hot = 0;
                        foreach (var e in _sim.Enemies.Enemies) if (e.Alive && Vec2.Distance(e.Position, b.At) < 1.5 + m.Kit.BreakerRadius * .6) hot++;
                        if (hot > 0 && Intent(m, "earlyDet", true, .05, .55, .5 + .12 * hot)) { m.Breakers.RemoveAt(i); DetonateBreaker(m, b, true); Say(m, "조기 기폭"); continue; }
                        if (hot == 0 && Roll(dt * .4)) b.NoEarly = true;
                    }
                    continue;
                }
                m.Breakers.RemoveAt(i);
                DetonateBreaker(m, b, false);
            }
        }

        // ───────────────────────────── 설치물 — 엔지니어 (AI 소유, 사람 전력망과 분리)
        bool PlaceTurret(CrewMember m, Vec2? spot)
        {
            var kit = m.Kit; var mine = new List<CrewTurret>(); foreach (var t in Turrets) if (t.Owner == m) mine.Add(t);
            if (mine.Count >= kit.MaxTurrets)
            {
                var old = mine[0]; foreach (var t in mine) if (t.Life < old.Life) old = t;   // 수명이 가장 적게 남은 것부터 회수
                Turrets.Remove(old); Text(old.Position + new Vec2(0, .4), "회수", "#C7A0FF", 12);
            }
            var p = spot ?? PickTurretSpot(m, null) ?? FreeSpotNear(m.Position, 1.2);
            Turrets.Add(new CrewTurret
            {
                Owner = m, Position = p, Aim = m.Aim, Cd = .2, Life = kit.TurretLife, MaxLife = kit.TurretLife,
                Ammo = kit.TurretMag, Mag = kit.TurretMag, Range = kit.TurretRange, Rate = kit.TurretRate, Power = kit.TurretPower,
            });
            m.TurretCd = kit.TurretCd;
            AwardXp(m, 2, XpKind.Support, capped: true, at: p);   // 설치 자체는 반복 생산 — 상한 적용
            Burst(p, 12, "#C7A0FF", 140); Ring(p, "#C7A0FF", 1.1, 6);
            Say(m, "센트리 설치");
            return true;
        }
        bool PlaceNode(CrewMember m)
        {
            var kit = m.Kit; var mine = new List<CrewNode>(); foreach (var n in Nodes) if (n.Owner == m) mine.Add(n);
            if (mine.Count >= kit.MaxNodes) { var old = mine[0]; Nodes.Remove(old); if (old.Light != null) _sim.Roles.Flares.Remove(old.Light); }
            var light = new Flare { Position = m.Position, Ttl = kit.NodeLife, MaxTtl = kit.NodeLife, LightRadius = SimTuning.PxCells(94) * .9, VisionRange = 3, IsEngineerNode = true };
            _sim.Roles.Flares.Add(light);   // 전력 노드는 주변을 밝힌다 — 팀 시야에 실제로 기여한다
            _sim.Los?.MarkDirty();
            Nodes.Add(new CrewNode { Owner = m, Position = m.Position, Life = kit.NodeLife, MaxLife = kit.NodeLife, Radius = kit.NodeRadius, Light = light });
            m.NodeCd = kit.NodeCd;
            AwardXp(m, 2, XpKind.Support, capped: true, at: m.Position);
            Ring(m.Position, "#7FEBD0", 1.3, 6);
            Say(m, "전력 노드");
            return true;
        }
        bool NodeCovers(Vec2 p) { foreach (var n in Nodes) if (Vec2.Distance(n.Position, p) <= n.Radius) return true; return false; }

        void UpdateInstallations(double dt)
        {
            UpdateCracks(dt); UpdateMarks(dt);
            for (int i = Nodes.Count - 1; i >= 0; i--) { var n = Nodes[i]; n.Life -= dt; if (n.Life <= 0) Nodes.RemoveAt(i); }
            for (int i = Turrets.Count - 1; i >= 0; i--)
            {
                var t = Turrets[i]; t.Life -= dt;
                if (t.Life <= 0) { Turrets.RemoveAt(i); Burst(t.Position, 8, "#685674", 90); continue; }
                t.Powered = NodeCovers(t.Position);
                t.Cd = Math.Max(0, t.Cd - dt);
                if (t.Reload > 0) { t.Reload = Math.Max(0, t.Reload - dt); if (t.Reload <= 0) t.Ammo = t.Mag; continue; }
                if (!t.Powered) continue;   // 급전 없으면 침묵 — 노드의 의미
                if (t.Ammo <= 0) { t.Reload = 2.2; continue; }
                if (t.Cd > 0) continue;
                EnemyState best = null; double bd = double.MaxValue;
                foreach (var e in _sim.Enemies.Enemies)
                {
                    if (!e.Alive) continue;
                    double d = Vec2.Distance(e.Position, t.Position);
                    if (d < bd && d <= t.Range && CanSee(t.Position, e.Position)) { bd = d; best = e; }
                }
                if (best == null) continue;
                t.Aim = (best.Position - t.Position).Angle;
                t.Ammo--; t.Cd = t.Rate > 0 ? t.Rate : .34;
                var dir = Vec2.FromAngle(t.Aim);
                _sim.Projectiles.Emit(new Projectile
                {
                    Position = t.Position + dir * .32, Velocity = dir * SimTuning.TeCells(ProjectileSystem.BaseSpeedPx("support")), Life = ProjectileSystem.BaseLife("support"),
                    Power = 1, Owner = t.Owner, AiMul = t.Power > 0 ? t.Power : .72, AiTurret = true, VisualId = "support",
                }, t.Aim);
            }
        }

        // ───────────────────────────── 피해 / 다운 / 구조
        public bool Hurt(CrewMember m, double dmg, Vec2 dir)
        {
            if (m == null || m.Down || m.IFrames > 0) return false;
            if (m.ShieldT > 0) dmg *= .35;
            m.Hp = Math.Max(0, m.Hp - dmg);
            m.IFrames = SimTuning.PlayerIFrame;
            m.Position -= dir * (SimTuning.EnemyKnock * .3);
            CollisionSystem.Resolve(W, ref m.Position, SimTuning.PlayerRadius);
            Text(m.Position + new Vec2(0, .6), "-" + Math.Round(dmg), "#FF6B6B", 14);
            if (m.Hp <= 0)
            {
                m.Down = true; m.DownT = 0; m.ReviveT = 0; m.Velocity = Vec2.Zero; m.DashT = 0; m.Digging = false;
                Ring(m.Position, "#FF557D", 1.4, 8);
                Toast?.Invoke($"AI {RoleKo[(int)m.Role]} 다운 — 접근해 구조");
            }
            return true;
        }
        /// <summary>좌표로 맞는 판정 — 적 투사체가 쓴다.</summary>
        public ICrewTarget HitTest(Vec2 at, double r)
        {
            foreach (var m in Members) if (!m.Down && Vec2.Distance(m.Position, at) < SimTuning.PlayerRadius + r) return m;
            return null;
        }
        /// <summary>보스탄 착탄 — 사람만 맞고 AI 는 안 맞으면 회피가 의미 없다.</summary>
        public void BossShotHit(Vec2 target, double radius, double dmg)
        {
            if (Members.Count == 0) return;
            if (dmg <= 0) dmg = Math.Max(9, Math.Round(SimTuning.EnemyDamage * 3.55));
            foreach (var m in Members)
            {
                if (m.Down) continue;
                var d = m.Position - target; double len = d.Length;
                if (len > radius) continue;
                Hurt(m, dmg, d / Math.Max(1e-6, len));
            }
        }

        void UpdateDown(CrewMember m, double dt)
        {
            m.DownT += dt;
            // 구조 — 사람이든 다른 AI 든 옆에 있으면 일으킨다 (§9.2). 기절한 사람은 구조자가 될 수 없고 치료 시간은 5초
            bool helper = !P.Downed && Vec2.Distance(P.Position, m.Position) < 1.6;
            if (!helper) foreach (var o in Members) if (o != m && !o.Down && Vec2.Distance(o.Position, m.Position) < 1.6) { helper = true; break; }
            if (helper)
            {
                m.ReviveT += dt;
                if (Roll(dt * 8)) Burst(m.Position + new Vec2(0, .2), 2, "#7FEBD0", 60);
                if (m.ReviveT >= 5)
                {
                    m.Down = false; m.Hp = m.HpMax * .5; m.IFrames = 1.4; m.ReviveT = 0;
                    Flash(m.Position, .8, "#7FEBD0");
                    Toast?.Invoke($"AI {RoleKo[(int)m.Role]} 구조 완료");
                }
            }
            else m.ReviveT = Math.Max(0, m.ReviveT - dt * .5);
        }

        /// <summary>사람이 쓰러졌을 때 구조할 수 있는 크루 수 (원본 reviveRescuersAlive).</summary>
        public int RescuersAlive() { int n = 0; foreach (var m in Members) if (!m.Down) n++; return n; }
        public int HelpersNear(Vec2 at, double range) { int n = 0; foreach (var m in Members) if (!m.Down && Vec2.Distance(m.Position, at) < range) n++; return n; }

        // ───────────────────────────── 보스 투사체 회피
        readonly List<(Vec2 target, double eta, double radius)> _bossThreats = new List<(Vec2, double, double)>();
        bool DodgeBossShot(CrewMember m, double dt)
        {
            if (_sim.Bosses == null) return false;
            _bossThreats.Clear(); _sim.Bosses.CollectShotThreats(_bossThreats);
            (Vec2 t, double d, double eta, double rad)? danger = null; double bestEta = 1e9;
            foreach (var (t, eta, rad) in _bossThreats)
            {
                if (eta > 1.8) continue;
                double d = Vec2.Distance(m.Position, t);
                if (d > rad * 1.15) continue;
                if (eta < bestEta) { bestEta = eta; danger = (t, d, eta, rad); }
            }
            if (danger == null) { m.Dodging = false; return false; }
            if (!m.Dodging) m.DashWant = Roll(.8);   // 회피 시작 시 1회 판정 — 80% 확률로만 대시
            m.Dodging = true;
            var dg = danger.Value;
            var n = m.Position - dg.t; double len = n.Length;
            n = len < .02 ? Vec2.FromAngle(Rnd(0, Math.PI * 2)) : n / len;
            double need = dg.rad * 1.3 - dg.d;
            Steer(m, m.Position + n * (need + 1), 1.2);
            if (dg.eta < .6 && m.DashCd <= 0 && need > .5 && m.DashWant) TryDash(m, n);
            Say(m, "회피");
            return true;
        }

        // ───────────────────────────── 전투 중 지원 행동 — 사격·이동과 경쟁하지 않고 같은 프레임에 같이 일어난다
        void CombatSupport(CrewMember m, CrewGoal goal)
        {
            var kit = m.Kit; var f = goal.Enemy != null ? goal.Enemy.Position : goal.At;
            switch (m.Role)
            {
                case RoleId.Engineer:
                {
                    var mine = new List<CrewTurret>(); foreach (var t in Turrets) if (t.Owner == m) mine.Add(t);
                    bool covers = false; foreach (var t in mine) if (Vec2.Distance(t.Position, f) < kit.TurretRange + 1.5) { covers = true; break; }
                    double want = mine.Count < kit.MaxTurrets ? .8 : (covers ? .12 : .6);
                    if (Intent(m, "turretFight", m.TurretCd <= 0, .35, 2.4, want)) { PlaceTurret(m, PickTurretSpot(m, f)); return; }
                    CrewTurret dead = null; foreach (var t in mine) if (!t.Powered) { dead = t; break; }
                    bool canFeed = dead != null && m.NodeCd <= 0 && Vec2.Distance(dead.Position, m.Position) < kit.NodeRadius * 1.2;
                    if (Intent(m, "nodeFight", canFeed, .3, 1.8, .82)) { PlaceNode(m); return; }
                    break;
                }
                case RoleId.Scout:
                    if (Intent(m, "flareFight", m.FlareCd <= 0 && !LitAt(f), .25, 1.9, .8)) { ThrowFlare(m, f, "조명 지원"); return; }
                    if (Intent(m, "pulseFight", m.PulseCd <= 0, .6, 3.5, .4)) { ScoutPulse(m); return; }
                    break;
                case RoleId.Gunner:
                {
                    double d = Vec2.Distance(f, m.Position);
                    bool hurt = m.Hp < m.HpMax * (.42 + .2 * m.Pers.Caution);
                    if (Intent(m, "shield", m.QCd <= 0 && (d < 3.2 || hurt), .1, 1.2, hurt ? .9 : .5))
                    {
                        m.ShieldT = 2.8; m.QCd = 8 * Rnd(.9, 1.25);
                        Ring(m.Position, "#7FEBD0", 1.4, 8); Say(m, "방어막"); return;
                    }
                    if (m.BreakerCd <= 0 && !CanSee(m.Position, f) && Intent(m, "breakerCover", true, .2, 1.6, .72))
                    {
                        var (c, r) = WorldGrid.ToCell((m.Position + f) / 2); var t = CellOf(c, r);
                        if (t != TileType.Empty && !Unbreakable(t)) { FireBreaker(m, c, r); Say(m, "차폐 제거"); return; }
                    }
                    if (m.BreakerCd <= 0 && kit.BreakerAtk)
                    {
                        var tgt = BreakerCluster(m, Threats(m));
                        if (tgt != null && Intent(m, "breakerAtk", true, .25, 2.0, .7))
                        {
                            var (c, r) = WorldGrid.ToCell(tgt.Position);
                            if (FireBreaker(m, c, r)) { Say(m, "파쇄탄"); return; }
                        }
                    }
                    break;
                }
            }
        }

        // ───────────────────────────── 재화 습득 (① 접촉 습득 ② 확률 회수)
        void CollectLoot(CrewMember m, double dt)
        {
            var items = _sim.Loot.Items; if (items.Count == 0) return;
            double pick = SimTuning.LootPickup, mag = SimTuning.LootMagnet * .55;
            for (int i = 0; i < items.Count; i++)
            {
                var q = items[i];
                if (q.Collected || !q.Landed) continue;
                double d = Vec2.Distance(q.Position, m.Position);
                if (d > mag) continue;
                if (d > pick)
                {
                    if (q.LandAge >= SimTuning.LootMagnetDelay) q.Position += (m.Position - q.Position) * Math.Min(1, dt * SimTuning.LootMagnetSpeed * .5);   // 약한 자석
                    continue;
                }
                int v = _sim.Loot.Collect(q);
                Ring(m.Position, q.Kind == ResourceKind.Bloom ? "#ff8db3" : "#ffd36e", (26 + v * 3) / 50.0, 10);
                AwardXp(m, Math.Max(1, v), XpKind.Loot, at: m.Position);
                m.LootGot += v;
            }
        }
        LootItem PickLoot(CrewMember m)
        {
            if (m.LootCd > 0) return null;
            LootItem best = null; double bs = -1e9;
            foreach (var q in _sim.Loot.Items)
            {
                if (q.Collected || !q.Landed) continue;
                double d = Vec2.Distance(q.Position, m.Position);
                if (d > 7) continue;
                if (Vec2.Distance(q.Position, P.Position) < 2.2) continue;   // 사람 코앞의 재화는 사람 몫
                double s = (q.Kind == ResourceKind.Bloom ? 6 : 3) + q.Value * 2 - d * 1.6;
                if (s > bs) { bs = s; best = q; }
            }
            return best;
        }

        // ───────────────────────────── 판단 — 역할별 행동 예산
        CrewGoal PickMine(CrewMember m, Vec2 anchor, int radius)
        {
            var (oc, orr) = WorldGrid.ToCell(anchor);
            var claims = new List<CrewGoal>(); foreach (var o in Members) if (o != m && o.MineTarget != null) claims.Add(o.MineTarget);
            CrewGoal best = null; double bs = -1e9;
            for (int dr = -radius; dr <= radius; dr++) for (int dc = -radius; dc <= radius; dc++)
            {
                int c = oc + dc, r = orr + dr;
                if (!W.InInterior(c, r)) continue;
                var t = W.At(c, r);
                if (t == TileType.Empty || Unbreakable(t)) continue;
                if (!Geo.CanMine(m.Position, c, r, Now)) continue;
                bool claimed = false; foreach (var q in claims) if (Math.Abs(q.C - c) <= 1 && Math.Abs(q.R - r) <= 1) { claimed = true; break; }
                if (claimed) continue;
                double ore = (t == TileType.Gem ? 28 : t == TileType.Crys ? 22 : t == TileType.Ore ? 16 : t == TileType.Stone ? 3 : 2) * m.Pers.OreBias;
                double s = ore - Math.Sqrt(dc * dc + dr * dr) * 2.4 + Rnd(-4, 4);
                if (s > bs) { bs = s; best = new CrewGoal { Kind = CrewGoalKind.Mine, C = c, R = r, At = WorldGrid.CellCenter(c, r), Label = "채굴" }; }
            }
            return best;
        }

        Vec2? DarkSpotAhead(CrewMember m)
        {
            Vec2? best = null; double bs = -1;
            for (int i = 0; i < 12; i++)
            {
                double a = i / 12.0 * Math.PI * 2;
                for (int d = 3; d <= 7; d++)
                {
                    var p = m.Position + Vec2.FromAngle(a) * d;
                    if (p.X < 1 || p.Y < 1 || p.X > W.Cols - 1 || p.Y > W.Rows - 1) break;
                    if (SolidAt(p)) break;
                    bool lit = false;
                    foreach (var l in _sim.Roles.Flares) if (Vec2.Distance(l.Position, p) < l.LightRadius) { lit = true; break; }
                    double s = d * (lit ? .2 : 1) * (SeenAt(p) ? 1 : 1.8) + Rnd(-.6, .6);
                    if (s > bs) { bs = s; best = p; }
                }
            }
            return bs > 0 ? best : null;
        }

        CrewGoal Decide(CrewMember m)
        {
            var kit = m.Kit; double ld = Vec2.Distance(P.Position, m.Position);

            // 0) 쓰러진 리더(사람) 구조가 그 무엇보다 우선
            if (P.Downed) return new CrewGoal { Kind = CrewGoalKind.Revive, At = P.Position, Label = "리더 구조" };
            // 0) 탈출 포트 — 요청되면 전원이 간다 (§8.4)
            var esc = _sim.Escape;
            if (esc.Phase == EscapePhase.Incoming || esc.Phase == EscapePhase.Ready) return new CrewGoal { Kind = CrewGoalKind.Escape, At = esc.Position, Label = "탈출 포트" };
            // 1) 쓰러진 동료 구조
            foreach (var o in Members) if (o != m && o.Down && Vec2.Distance(o.Position, m.Position) < 14) return new CrewGoal { Kind = CrewGoalKind.Revive, At = o.Position, ReviveTarget = o, Label = "구조" };
            // 2) 리더가 너무 멀면 붙는다
            int leash = m.Role == RoleId.Scout ? 13 : 9;
            if (ld > leash) return new CrewGoal { Kind = CrewGoalKind.Follow, At = P.Position, Label = "합류" };
            // 3) 전투 — 인지는 팀 단위, 요격 거리는 직업별
            if (_sim.Bosses != null && _sim.Bosses.Active && Vec2.Distance(_sim.Bosses.Boss.Body.Position, m.Position) < 16)
                return new CrewGoal { Kind = CrewGoalKind.Fight, At = _sim.Bosses.Boss.Body.Position, Enemy = _sim.Bosses.Boss.Body, Boss = true, Label = "보스 교전" };
            var foes = Threats(m);
            if (foes.Count > 0)
            {
                m.LastFoeDir = (foes[0].e.Position - P.Position).Angle;
                var t = foes.Count > 1 && Roll(.22 * (1.45 - m.Pers.Focus)) ? foes[1] : foes[0];
                double reach = kit.Intercept * (.7 + m.Pers.Aggression * .45) * m.Mood;
                bool leashOk = t.dp <= reach + 4 || t.d <= reach;
                bool busy = m.Goal != null && (m.Goal.Kind == CrewGoalKind.Mine || m.Goal.Kind == CrewGoalKind.Crack) && t.dp > 5 && t.d > 4.5;
                if (leashOk && !(busy && Roll(m.Pers.Focus * .55))) return new CrewGoal { Kind = CrewGoalKind.Fight, At = t.e.Position, Enemy = t.e, Label = "교전" };
            }
            // 4) 직업 고유 임무
            if (m.Role == RoleId.Engineer)
            {
                var mine = new List<CrewTurret>(); foreach (var t in Turrets) if (t.Owner == m) mine.Add(t);
                if (Intent(m, "turret", m.TurretCd <= 0 && mine.Count < kit.MaxTurrets, .6, 4.5, .72)) return new CrewGoal { Kind = CrewGoalKind.Turret, At = m.Position, Label = "센트리 설치" };
                bool stale = mine.Count > 0; foreach (var t in mine) if (Vec2.Distance(t.Position, P.Position) <= 8) { stale = false; break; }
                if (Intent(m, "turretFwd", m.TurretCd <= 0 && stale, 1.2, 6, .55)) return new CrewGoal { Kind = CrewGoalKind.Turret, At = m.Position, Label = "센트리 전진 배치" };
                int nodes = 0; foreach (var n in Nodes) if (n.Owner == m) nodes++;
                CrewTurret unpowered = null; foreach (var t in mine) if (!t.Powered) { unpowered = t; break; }
                if (Intent(m, "node", m.NodeCd <= 0 && (nodes < kit.MaxNodes || unpowered != null), .5, 4, unpowered != null ? .85 : .6))
                    return new CrewGoal { Kind = CrewGoalKind.Node, At = unpowered != null ? unpowered.Position : m.Position, Label = unpowered != null ? "센트리 급전" : "전력 노드" };
            }
            if (m.Role == RoleId.Scout)
            {
                if (Intent(m, "explore", m.ExploreCd <= 0, 1.5, 7, .5 * m.Pers.Curiosity))
                {
                    var spot = PickScoutSpot(m);
                    if (spot.HasValue) { m.ExploreCd = (kit.ExploreCd > 0 ? kit.ExploreCd : 11) * Rnd(.8, 1.6); return new CrewGoal { Kind = CrewGoalKind.Scout, At = spot.Value, Label = "정찰" }; }
                }
                if (Intent(m, "pulse", m.PulseCd <= 0, 1, 6, .45 * m.Pers.Curiosity)) return new CrewGoal { Kind = CrewGoalKind.Pulse, At = m.Position, Label = "정찰 펄스" };
                if (m.FlareCd <= 0)
                {
                    var dark = DarkSpotAhead(m);
                    if (dark.HasValue && Intent(m, "flare", true, .5, 3.5, .65)) return new CrewGoal { Kind = CrewGoalKind.Flare, At = dark.Value, Label = "플레어" };
                }
            }
            if (m.Role == RoleId.Driller && kit.Crack)
            {
                if (m.CrackTarget == null && Intent(m, "crack", true, 2.5, 9, .5))
                {
                    var b = PickBedrock(m, 7);
                    if (b != null) { m.CrackTarget = b; m.CrackUntil = Now + 24; }
                }
                if (m.CrackTarget != null)
                {
                    var t = CellOf(m.CrackTarget.C, m.CrackTarget.R);
                    if (t == TileType.Empty || !Unbreakable(t) || m.CrackUntil < Now) m.CrackTarget = null;
                    else return m.CrackTarget;
                }
            }
            // 4.4) 재화 회수 — 가끔
            if (m.LootCd <= 0)
            {
                var q = PickLoot(m);
                if (q != null && Roll(.28 * m.Pers.Greed * m.Mood)) { m.LootCd = 2.5 + Rnd(0, 2); return new CrewGoal { Kind = CrewGoalKind.Loot, At = q.Position, Res = q, Label = "재화 회수" }; }
                if (q == null) m.LootCd = 1.2;
            }
            // 4.5) 재장전 — 채우는 시점이 사람마다 다르고, 가끔 그냥 잊는다
            if (m.ReloadLeft <= 0 && m.Ammo < m.Mag * m.Pers.ReloadAt && Intent(m, "reload", true, .15, 1.6, .8)) { m.ReloadLeft = m.ReloadTime; Say(m, "재장전"); }
            // 5) 채굴 — 한 번 고른 벽은 부술 때까지 붙잡는다
            var anchor = (m.Position + P.Position) / 2;
            if (m.MineTarget != null)
            {
                var t = CellOf(m.MineTarget.C, m.MineTarget.R);
                bool far = Vec2.Distance(m.MineTarget.At, anchor) > leash + 4;
                if (t == TileType.Empty || Unbreakable(t) || far || m.MineUntil < Now || !Geo.CanMine(m.Position, m.MineTarget.C, m.MineTarget.R, Now)) m.MineTarget = null;
                else return m.MineTarget;
            }
            // 거너 — 파쇄탄이 식기 전엔 벽을 잡지 않는다. 통로 입구를 잡는다
            if (m.Role == RoleId.Gunner && m.BreakerCd > 2)
            {
                bool badPost = m.Watch.HasValue && SolidAt(m.Watch.Value);
                if (!m.Watch.HasValue || m.WatchT <= 0 || badPost) { m.Watch = PickWatchPost(m); m.WatchT = Rnd(3.5, 9.5); }
                if (m.Watch.HasValue) return new CrewGoal { Kind = CrewGoalKind.Watch, At = m.Watch.Value, Label = "구역 경계" };
                return new CrewGoal { Kind = CrewGoalKind.Guard, At = P.Position, Label = "대기" };
            }
            var pick = PickMine(m, anchor, Math.Max(4, leash - 2)) ?? PickMine(m, anchor, leash + 3) ?? PickMine(m, P.Position, leash + 6)
                       ?? Geo.Frontier(m.Position, Now, (c, r, t) => (t == TileType.Gem ? 28 : t == TileType.Crys ? 22 : t == TileType.Ore ? 16 : t == TileType.Stone ? 3 : 2) - Vec2.Distance(WorldGrid.CellCenter(c, r), m.Position) * 1.4);
            if (pick != null) { m.MineTarget = pick; m.MineUntil = Now + 14; return pick; }
            return new CrewGoal { Kind = CrewGoalKind.Guard, At = P.Position, Label = "대기" };
        }

        // ───────────────────────────── 실행
        void FollowStep(CrewMember m, CrewGoal goal, double dt)
        {
            int step = EnsurePath(m, goal.At);
            if (step < 0) { Steer(m, goal.At); AimTo(m, goal.At); return; }
            int c = step % W.Cols, r = step / W.Cols; var wp = WorldGrid.CellCenter(c, r);
            if (W.AtIndex(step) != TileType.Empty)
            {
                // 앞이 벽 — 직업의 방식으로 뚫는다
                if (m.Role == RoleId.Gunner)
                {
                    if (!FireBreaker(m, c, r)) { Steer(m, wp, .4); AimTo(m, wp); }
                    else MoveAwayFrom(m, wp, .8);
                    return;
                }
                Steer(m, wp, .55);
                if (!DigAt(m, c, r, dt)) AimTo(m, wp);
                return;
            }
            Steer(m, wp); AimTo(m, wp);
        }

        void AimTo(CrewMember m, Vec2 p)
        {
            double want = (p - m.Position).Angle, d = want - m.Aim;
            while (d > Math.PI) d -= Math.PI * 2;
            while (d < -Math.PI) d += Math.PI * 2;
            m.Aim += d * .25;
            m.Face = Math.Cos(m.Aim) < 0 ? -1 : 1;
        }

        void Act(CrewMember m, CrewGoal goal, double dt)
        {
            // 보스탄 예고는 어떤 행동보다 먼저다
            if (goal.Kind != CrewGoalKind.Fight && DodgeBossShot(m, dt)) return;

            switch (goal.Kind)
            {
                case CrewGoalKind.Fight:
                {
                    var e = goal.Enemy;
                    if (e == null || !e.Alive) { m.Goal = null; return; }
                    double d = Vec2.Distance(e.Position, m.Position);
                    double want = goal.Boss ? m.Kit.Engage + 1.6 : m.Kit.Engage;
                    AimTo(m, e.Position);
                    CombatSupport(m, goal);
                    if (DodgeBossShot(m, dt)) { if (CanSee(m.Position, e.Position)) Fire(m, e.Position); return; }
                    bool inRange = d <= Math.Min(FireRange, m.Kit.Range) && CanSee(m.Position, e.Position);
                    if (inRange) Fire(m, e.Position);
                    else { FollowStep(m, goal, dt); return; }
                    if (m.StrafeT <= 0) { m.StrafeT = Rnd(.8, 3.2); m.FarJit = Rnd(-.08, .16); if (Roll(.35)) m.Pers.Strafe *= -1; }
                    m.StrafeT -= dt;
                    double near = want * (.55 + .16 * m.Pers.Caution), far = want * (1.3 + m.FarJit);
                    if (d < near)
                    {
                        MoveAwayFrom(m, e.Position, .95);
                        bool panic = m.Hp < m.HpMax * (.32 + .24 * m.Pers.Caution);
                        if (d < 1.5 && m.DashCd <= 0 && panic && Intent(m, "kite", true, .05, .6, .72)) TryDash(m, m.Position - e.Position);
                    }
                    else if (d > far) FollowStep(m, goal, dt);
                    else
                    {
                        double a = (e.Position - m.Position).Angle + Math.PI / 2 * m.Pers.Strafe;
                        Steer(m, m.Position + Vec2.FromAngle(a), .55);
                    }
                    if (m.Kit.DrillMelee && d < 1.5) { m.Digging = true; m.Drill = 1; DrillMelee(m, dt); }
                    return;
                }
                case CrewGoalKind.Escape:
                {
                    double d = Vec2.Distance(goal.At, m.Position);
                    if (d > 1.0) FollowStep(m, goal, dt); else { m.Velocity *= .7; AimTo(m, goal.At); }
                    var foes = EnemiesNear(m, 7, true);
                    if (foes.Count > 0) { AimTo(m, foes[0].Position); Fire(m, foes[0].Position); }
                    return;
                }
                case CrewGoalKind.Revive:
                {
                    var at = goal.ReviveTarget != null ? goal.ReviveTarget.Position : P.Position; goal.At = at;
                    double d = Vec2.Distance(at, m.Position);
                    if (d > 1.2) FollowStep(m, goal, dt); else { m.Velocity *= .7; AimTo(m, at); }
                    return;
                }
                case CrewGoalKind.Turret: PlaceTurret(m, PickTurretSpot(m, null)); m.Goal = null; return;
                case CrewGoalKind.Node:
                {
                    if (Vec2.Distance(goal.At, m.Position) > 1.2) { FollowStep(m, goal, dt); return; }
                    PlaceNode(m); m.Goal = null; return;
                }
                case CrewGoalKind.Flare: ThrowFlare(m, goal.At, "플레어"); m.Goal = null; return;
                case CrewGoalKind.Mine:
                {
                    double d = Vec2.Distance(goal.At, m.Position);
                    bool gunner = m.Role == RoleId.Gunner;
                    bool touch = gunner ? d <= 1.25 : Geo.InReach(m.Position, goal.C, goal.R);
                    if (!touch) { FollowStep(m, goal, dt); Geo.ProgressReset(m); return; }
                    Steer(m, goal.At, .4);
                    if (gunner) { FireBreaker(m, goal.C, goal.R); return; }
                    if (m.Kit.BreachCd > 0 && m.BreachT <= 0 && m.BreachCd <= 0)
                    {
                        double wh = Geo.WallHp(goal.C, goal.R);
                        if (Intent(m, "breach", true, .5, 3.2, wh > 120 ? .75 : .4)) StartBreach(m);
                    }
                    if (IdleBeat(m, dt, false)) return;
                    if (!DigAt(m, goal.C, goal.R, dt) || !Geo.Progress(m, goal.C, goal.R, dt, 1.8))
                    {
                        Geo.Ban(goal.C, goal.R, Now, 14);
                        m.MineTarget = null; m.Goal = null; m.Path.Clear(); m.PathKey = ""; m.PathAge = 0;
                    }
                    return;
                }
                case CrewGoalKind.Crack:
                {
                    if (!Geo.InReach(m.Position, goal.C, goal.R)) { FollowStep(m, goal, dt); return; }
                    Steer(m, goal.At, .35); AimTo(m, goal.At);
                    CrackPressure(m, goal.C, goal.R, dt, false);
                    if (m.BreachCd <= 0 && Intent(m, "breachRock", true, .8, 4, .62)) StartBreach(m);
                    m.CrackT += dt;
                    if (m.CrackT > m.CrackPatience || Roll(dt * .02))
                    {
                        m.CrackT = 0; m.CrackPatience = Rnd(9, 20); m.CrackTarget = null; m.Goal = null;
                        if (Roll(.35)) Say(m, "이건 나중에");
                    }
                    return;
                }
                case CrewGoalKind.Pulse: ScoutPulse(m); m.Goal = null; return;
                case CrewGoalKind.Scout:
                {
                    double d = Vec2.Distance(goal.At, m.Position);
                    if (d > 3 && m.Kit.GrappleCd > 0 && Intent(m, "grapple", m.GrappleCd <= 0, .3, 2.4, .55)) TryGrapple(m, goal.At);
                    if (d > 1.3) { FollowStep(m, goal, dt); Potshot(m, dt); return; }
                    ReconAward(m, m.Position);
                    if (m.PulseCd <= 0 && Roll(.45)) ScoutPulse(m);
                    else if (Roll(.3)) Say(m, new[] { "여긴 비었어", "길 있다", "기록해 둔다" }[_rng.NextInt(3)]);
                    m.Goal = null; m.Velocity *= .7;
                    return;
                }
                case CrewGoalKind.Watch:
                {
                    double d = Vec2.Distance(goal.At, m.Position);
                    m.WatchT -= dt;
                    if (d > .9) { FollowStep(m, goal, dt); Potshot(m, dt); return; }
                    m.Velocity *= .86;
                    var near = EnemiesNear(m, Math.Min(m.Kit.Range, 9), true);
                    if (near.Count > 0) { AimTo(m, near[0].Position); Fire(m, near[0].Position); return; }
                    if (IdleBeat(m, dt, false)) return;
                    if (m.SweepT <= 0) { m.SweepT = Rnd(.5, 2.2); m.SweepA = m.Aim + Rnd(-1.5, 1.5); }
                    m.SweepT -= dt;
                    AimTo(m, m.Position + Vec2.FromAngle(m.SweepA) * 3);
                    return;
                }
                case CrewGoalKind.Loot:
                {
                    if (goal.Res == null || goal.Res.Collected) { m.Goal = null; return; }
                    goal.At = goal.Res.Position;
                    if (Vec2.Distance(goal.At, m.Position) > SimTuning.LootPickup * .9) FollowStep(m, goal, dt);
                    else { m.Velocity *= .7; m.Goal = null; }
                    return;
                }
                case CrewGoalKind.Follow: goal.At = P.Position; FollowStep(m, goal, dt); return;
                default:
                {
                    // guard — 리더 근처를 지키며 주변을 살핀다
                    goal.At = P.Position;
                    if (Vec2.Distance(P.Position, m.Position) > 3.2) FollowStep(m, goal, dt);
                    else if (!IdleBeat(m, dt))
                    {
                        m.Velocity *= .85;
                        if (m.SweepT <= 0) { m.SweepT = Rnd(.6, 2.4); m.SweepA = m.Aim + Rnd(-2, 2); }
                        m.SweepT -= dt;
                        AimTo(m, m.Position + Vec2.FromAngle(m.SweepA) * 3);
                    }
                    return;
                }
            }
        }

        // ───────────────────────────── 복구 — 파묻힘 탈출 · 워프
        void WarpToParty(CrewMember m)
        {
            Burst(m.Position, 10, "#C7A0FF", 120);
            m.Position = FreeSpotNear(P.Position, 1.6); m.Velocity = Vec2.Zero;
            m.Path.Clear(); m.PathKey = ""; m.PathAge = 0;
            m.BuriedT = 0; m.LostT = 0; m.StuckT = 0;
            Burst(m.Position, 10, "#C7A0FF", 120);
            Say(m, "재합류");
        }
        bool Recover(CrewMember m, double dt)
        {
            if (SolidAt(m.Position))
            {
                var (c, r) = WorldGrid.ToCell(m.Position); var t = CellOf(c, r);
                if (t != TileType.Empty && !Unbreakable(t))
                {
                    m.Digging = true; m.Drill = 1;
                    DamageWall(m, c, r, SimTuning.DrillDps * SimTuning.DrillDamageMul * Math.Max(1, m.Kit.DigMul) * 3 * dt, new Vec2(0, -1));
                }
                m.BuriedT += dt;
                if (m.BuriedT > 4) WarpToParty(m);
                m.Velocity *= .6;
                return true;
            }
            m.BuriedT = 0;
            m.LostT = m.StuckT > .8 ? m.LostT + dt : Math.Max(0, m.LostT - dt * .5);
            if (m.LostT > 6) WarpToParty(m);
            return false;
        }
        void WatchStuck(CrewMember m, double dt)
        {
            double moved = Vec2.Distance(m.Position, m.Last); m.Last = m.Position;
            bool wants = m.Velocity.Length > .16;
            if (wants && moved < dt * .2) m.StuckT += dt; else m.StuckT = Math.Max(0, m.StuckT - dt * 2);
            if (m.Jitter > 0) m.Jitter -= dt;
            if (m.StuckT > 1.0)
            {
                m.StuckT = 0; m.PathAge = 0; m.Path.Clear(); m.PathKey = "";
                m.Jitter = .5; m.JitterA = Rnd(0, Math.PI * 2);
                if (m.Goal != null && m.Goal.Kind == CrewGoalKind.Mine)
                {
                    if (m.MineTarget != null) Geo.Ban(m.MineTarget.C, m.MineTarget.R, Now, 10);
                    m.MineTarget = null; m.Goal = null;
                }
            }
        }

        // ───────────────────────────── 프레임
        public void Tick(double dt)
        {
            if (!Playing) return;
            UpdateInstallations(dt);
            foreach (var m in Members)
            {
                m.IFrames = Math.Max(0, m.IFrames - dt); m.GunCd = Math.Max(0, m.GunCd - dt);
                m.QCd = Math.Max(0, m.QCd - dt); m.ECd = Math.Max(0, m.ECd - dt); m.DashCd = Math.Max(0, m.DashCd - dt);
                m.ShieldT = Math.Max(0, m.ShieldT - dt); m.FlareCd = Math.Max(0, m.FlareCd - dt);
                m.TurretCd = Math.Max(0, m.TurretCd - dt); m.NodeCd = Math.Max(0, m.NodeCd - dt); m.LootCd = Math.Max(0, m.LootCd - dt);
                UpdateMood(m, dt);
                m.BreachCd = Math.Max(0, m.BreachCd - dt); m.PulseCd = Math.Max(0, m.PulseCd - dt);
                m.GrappleCd = Math.Max(0, m.GrappleCd - dt); m.ExploreCd = Math.Max(0, m.ExploreCd - dt);
                m.PathAge -= dt; m.SayT = Math.Max(0, m.SayT - dt);
                if (m.ReloadLeft > 0) { m.ReloadLeft = Math.Max(0, m.ReloadLeft - dt); if (m.ReloadLeft <= 0) m.Ammo = m.Mag; }
                UpdateBreakers(m, dt);
                UpdateBoarding(m, dt);
                XpTrickle(m, dt);
                CollectLoot(m, dt);

                if (m.Down) { UpdateDown(m, dt); continue; }

                m.Digging = false; m.Drill = 0;
                if (Recover(m, dt)) { ApplyMotion(m, dt); continue; }

                m.React -= dt;
                if (m.React <= 0 || m.Goal == null) { m.Goal = Decide(m); m.React = m.Pers.React * Rnd(.75, 1.4); }
                UpdateBreach(m, dt);
                DodgeEnemyShot(m);
                if (!m.Dashing && m.DashCd <= 0 && !m.Digging && m.Velocity.Length > SimTuning.MoveSpeed * .45 && Roll(dt * .03 * m.Pers.Aggression * m.Mood)) TryDash(m, m.Velocity);
                if (m.Goal != null) Act(m, m.Goal, dt);
                if (m.Goal != null && m.Goal.Kind != CrewGoalKind.Fight && m.Goal.Kind != CrewGoalKind.Escape) Potshot(m, dt);
                ApplyMotion(m, dt);
                WatchStuck(m, dt);
            }
        }

        // ───────────────────────────── 적의 타깃 선정 — 가장 가까운 크루, 0.6초 이력(hysteresis)
        readonly Dictionary<EnemyState, (ICrewTarget tgt, double until)> _tgt = new Dictionary<EnemyState, (ICrewTarget, double)>();
        public ICrewTarget TargetFor(EnemyState e)
        {
            if (!Enabled || Members.Count == 0) return P;
            if (_tgt.TryGetValue(e, out var cur) && Now < cur.until && cur.tgt != null && !cur.tgt.IsDowned && (cur.tgt is PlayerState || Members.Contains((CrewMember)cur.tgt))) return cur.tgt;
            ICrewTarget best = P; double bd = P.Downed ? double.PositiveInfinity : Vec2.Distance(P.Position, e.Position);
            foreach (var m in Members)
            {
                if (m.Down) continue;
                double d = Vec2.Distance(m.Position, e.Position);
                if (d < bd) { bd = d; best = m; }
            }
            _tgt[e] = (best, Now + .6);
            if (_tgt.Count > 256) { var dead = new List<EnemyState>(); foreach (var k in _tgt.Keys) if (!k.Alive) dead.Add(k); foreach (var k in dead) _tgt.Remove(k); }
            return best;
        }

        // ───────────────────────────── 탈출 — 생존자 전원 탑승 (§8.4-3·4)
        void UpdateBoarding(CrewMember m, double dt)
        {
            var esc = _sim.Escape;
            if (esc.Phase != EscapePhase.Ready || m.Down) { m.Boarded = false; m.BoardT = 0; return; }
            if (Vec2.Distance(m.Position, esc.Position) <= EscapeSystem.BoardRange)
            {
                m.BoardT += dt;
                if (m.BoardT >= EscapeSystem.BoardTime && !m.Boarded) { m.Boarded = true; Say(m, "탑승"); Toast?.Invoke($"AI {RoleKo[(int)m.Role]} 탑승"); }
            }
            else { m.BoardT = Math.Max(0, m.BoardT - dt * 2); m.Boarded = false; }
        }
        /// <summary>null = AI 크루 없음(기존 솔로 판정 그대로) · false = 아직 대기 · true = 전원 탑승.</summary>
        public bool? EscapeAllAboard()
        {
            if (!Enabled || Members.Count == 0) return null;
            foreach (var m in Members) { if (m.Down) continue; if (!m.Boarded) return false; }
            return true;
        }
        public (int boarded, int total)? EscapeCount()
        {
            if (Members.Count == 0) return null;
            int total = 1, on = 0;
            foreach (var m in Members) { if (m.Down) continue; total++; if (m.Boarded) on++; }
            return (on, total);
        }
    }
}
