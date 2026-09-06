using System;
using System.Collections.Generic;

namespace TunnelCrew.Sim
{
    // ───────────────────────────── 설치물 · 투척물

    /// <summary>스카웃 플레어 / 엔지니어 노드 등불. 시야원 + 광원이다.</summary>
    public sealed class Flare
    {
        public Vec2 Position;
        public double Ttl, MaxTtl;
        public double LightRadius;
        public int VisionRange;
        public bool IsEngineerNode;
        /// <summary>엔지니어 노드의 빛이면 그 노드. 노드가 사라지면 함께 사라진다.</summary>
        public PowerNode Node;
    }

    /// <summary>엔지니어 전력 노드. 반경 안의 센트리에 급전한다.</summary>
    public sealed class PowerNode
    {
        public Vec2 Position;
        public double Life, MaxLife;
        public double Radius;
    }

    /// <summary>엔지니어 센트리.</summary>
    public sealed class Turret
    {
        public Vec2 Position;
        public double Life, MaxLife;
        public double Cooldown;
        public int Ammo, Mag;
        public bool Powered, PlayerPowered, NodePowered;
        public double Aim;
    }

    /// <summary>거너 파쇄탄. 벽에 붙어 신관 시간 뒤 터진다.</summary>
    public sealed class BreakerCharge
    {
        public Vec2 Start, Target;
        public int Col, Row;
        public double Travel, TravelMax;
        public double Fuse;
        public bool Stuck;
        public double Angle;
    }

    public struct SkillEvent { public RoleId Role; public bool IsQ; public Vec2 Position; public double Angle; }
    public struct BreakerExplodedEvent { public Vec2 Position; public int Radius; public bool Early; }
    public struct FoundationBrokenEvent { public int Col, Row; public TileType Type; }

    /// <summary>
    /// 4직업의 Q/E 스킬과 설치물. 원본 <c>crewSkillQ/E</c>(10562~10613) 와
    /// <c>infPlaceEngineerNode/Turret · infUpdateEngineer · infTryFireBreaker ·
    /// infDrillerPressure · infUseScoutGrapple</c> (12305~12477) 를 옮겼다.
    /// </summary>
    public sealed class RoleSystem
    {
        readonly WorldGrid _world;
        readonly EnemySystem _enemies;
        readonly ProjectileSystem _projectiles;

        public readonly List<Flare> Flares = new List<Flare>();
        public readonly List<PowerNode> Nodes = new List<PowerNode>();
        public readonly List<Turret> Turrets = new List<Turret>();
        public readonly List<BreakerCharge> Breakers = new List<BreakerCharge>();

        /// <summary>드릴러 균열 진행도. 키 = 셀 인덱스.</summary>
        public readonly Dictionary<int, CrackState> Cracks = new Dictionary<int, CrackState>();
        public sealed class CrackState { public double Progress; public double LastTouched; public TileType Type; }

        // 쿨다운 · 상태
        public double QCooldown, ECooldown;
        public double ShieldTime;          // 거너 Q
        public double BreachTime;          // 드릴러 Q
        public double BreakerCooldown;     // 거너 LMB
        public double GrappleFxTime;

        // 원본 INF 기본값
        public int EngineerMaxNodes = 2, EngineerMaxTurrets = 2;
        public double EngineerNodeLife = 50, EngineerNodeRadius = 4.0;
        public double EngineerTurretLife = 45, EngineerTurretRange = 5.5, EngineerTurretInterval = 0.34, EngineerTurretPower = 0.72;
        public int EngineerTurretMag = 18;
        public bool EngineerAutonomous = false;
        public double BreakerFuse = 2.0, BreakerMaxCd = 12.0;
        public int BreakerRadius = 1;
        public double ScoutGrappleRange = 5.0;
        public double ScoutFlareRadMul = 1.0, ScoutFlareLifeMul = 1.0;
        public int ScoutVisionBonus = 0;

        public event Action<SkillEvent> SkillUsed;
        public event Action<BreakerExplodedEvent> BreakerExploded;
        public event Action<FoundationBrokenEvent> FoundationBroken;

        double _time;

        public RoleSystem(WorldGrid world, EnemySystem enemies, ProjectileSystem projectiles)
        {
            _world = world; _enemies = enemies; _projectiles = projectiles;
        }

        public static double QCooldownFor(RoleId r) => r switch
        {
            RoleId.Driller => 7, RoleId.Gunner => 8, RoleId.Scout => 5, RoleId.Engineer => 7, _ => 7,
        };
        public static double ECooldownFor(RoleId r) => r switch
        {
            RoleId.Gunner => 0, RoleId.Scout => 5, RoleId.Engineer => 10, _ => 0,
        };
        public static bool HasE(RoleId r) => r != RoleId.Driller;

        // ───────────────────────────── 입력
        public void UseQ(PlayerState p, PlayerBuild b, int depth)
        {
            if (QCooldown > 0 || !p.CanMove) return;
            switch (b.Role)
            {
                case RoleId.Driller:
                    BreachTime = 0.55;
                    break;
                case RoleId.Scout:
                {
                    var pos = p.Position + Vec2.FromAngle(p.Aim) * 2.6;
                    double rad = Math.Max(SimTuning.PxCells(94.0) * 1.45, 4.2) * ScoutFlareRadMul;
                    Flares.Add(new Flare
                    {
                        Position = pos, Ttl = 22 * ScoutFlareLifeMul, MaxTtl = 22 * ScoutFlareLifeMul,
                        LightRadius = rad, VisionRange = 5 + ScoutVisionBonus,
                    });
                    break;
                }
                case RoleId.Engineer:
                {
                    if (Nodes.Count >= EngineerMaxNodes) Nodes.RemoveAt(0);
                    var pos = PlaceInFront(p, 1.6);
                    var node = new PowerNode { Position = pos, Life = EngineerNodeLife, MaxLife = EngineerNodeLife, Radius = EngineerNodeRadius };
                    Nodes.Add(node);
                    Flares.Add(new Flare { Position = pos, Ttl = EngineerNodeLife, MaxTtl = EngineerNodeLife, LightRadius = 2.15, VisionRange = 3, IsEngineerNode = true, Node = node });
                    break;
                }
                case RoleId.Gunner:
                    ShieldTime = 2.8;
                    p.IFrames = Math.Max(p.IFrames, 2.8);
                    break;
            }
            QCooldown = QCooldownFor(b.Role);
            SkillUsed?.Invoke(new SkillEvent { Role = b.Role, IsQ = true, Position = p.Position, Angle = p.Aim });
        }

        public void UseE(PlayerState p, PlayerBuild b)
        {
            if (!HasE(b.Role) || ECooldown > 0 || !p.CanMove) return;
            switch (b.Role)
            {
                case RoleId.Gunner:
                    if (!DetonateBreaker()) return;
                    ECooldown = 0.2;
                    break;
                case RoleId.Scout:
                    if (!Grapple(p)) return;
                    ECooldown = ECooldownFor(b.Role);
                    break;
                case RoleId.Engineer:
                {
                    if (Turrets.Count >= EngineerMaxTurrets) Turrets.RemoveAt(0);
                    var pos = PlaceInFront(p, 1.6);
                    Turrets.Add(new Turret
                    {
                        Position = pos, Life = EngineerTurretLife, MaxLife = EngineerTurretLife,
                        Cooldown = 0.18, Ammo = EngineerTurretMag, Mag = EngineerTurretMag, Aim = p.Aim,
                    });
                    ECooldown = ECooldownFor(b.Role);
                    break;
                }
            }
            SkillUsed?.Invoke(new SkillEvent { Role = b.Role, IsQ = false, Position = p.Position, Angle = p.Aim });
        }

        /// <summary>거너 좌클릭 — 파쇄탄. 조준선의 첫 벽에 부착한다.</summary>
        public bool TryFireBreaker(PlayerState p)
        {
            if (BreakerCooldown > 0) return false;
            var dir = Vec2.FromAngle(p.Aim);
            double max = 8.0;
            for (double d = SimTuning.PlayerRadius + 0.2; d <= max; d += 0.14)
            {
                var pt = p.Position + dir * d;
                var (c, r) = WorldGrid.ToCell(pt);
                if (!_world.InBounds(c, r)) break;
                if (!_world.IsSolid(c, r)) continue;
                Breakers.Add(new BreakerCharge
                {
                    Start = p.Position + dir * (SimTuning.PlayerRadius * 0.8),
                    Target = WorldGrid.CellCenter(c, r), Col = c, Row = r,
                    Travel = 0.18, TravelMax = 0.18, Fuse = BreakerFuse, Angle = p.Aim,
                });
                BreakerCooldown = BreakerMaxCd;
                return true;
            }
            return false;
        }

        bool DetonateBreaker()
        {
            for (int i = 0; i < Breakers.Count; i++)
            {
                if (!Breakers[i].Stuck) continue;
                var ch = Breakers[i];
                Breakers.RemoveAt(i);
                Explode(ch, early: true);
                return true;
            }
            return false;
        }

        /// <summary>원본 infExplodeBreaker() — 정사각 반경, 중심 1.12 · 직교 0.72 · 대각 0.55 감쇠.</summary>
        void Explode(BreakerCharge ch, bool early, double gunMul = 1.0)
        {
            int rad = Math.Max(1, BreakerRadius);
            var dir = Vec2.FromAngle(ch.Angle);
            for (int dr = -rad; dr <= rad; dr++)
                for (int dc = -rad; dc <= rad; dc++)
                {
                    int ring = Math.Max(Math.Abs(dc), Math.Abs(dr));
                    int c = ch.Col + dc, r = ch.Row + dr;
                    if (!_world.InBounds(c, r)) continue;
                    var t = _world.At(c, r);
                    if (t == TileType.Empty || TileTypes.IsBedrock(t)) continue;
                    double baseMul = (dc == 0 && dr == 0) ? 1.12 : (dc != 0 && dr != 0 ? 0.55 : 0.72);
                    double mul = baseMul * Math.Max(0.42, 1 - (ring - 1) * 0.28);
                    double full = _world.MaxHp(t);
                    var hit = new Vec2(dc != 0 ? dc : dir.X, dr != 0 ? dr : dir.Y);
                    _world.Damage(c, r, full * mul, hit);
                }

            double enemyRad = 1.6 + (rad - 1) * 0.65;
            foreach (var e in _enemies.Enemies)
            {
                if (!e.Alive) continue;
                var d = e.Position - ch.Target;
                double dist = d.Length;
                if (dist >= enemyRad) continue;
                double fall = 1 - dist / enemyRad;
                _enemies.HurtEnemy(e, SimTuning.EnemyGunDamage * (0.28 + 0.34 * fall) * gunMul, d / Math.Max(1e-6, dist), ch.Target);
            }
            BreakerExploded?.Invoke(new BreakerExplodedEvent { Position = ch.Target, Radius = rad, Early = early });
        }

        /// <summary>스카웃 E — 조준선을 따라 시야 안의 빈 공간까지 0.22초 돌진.</summary>
        bool Grapple(PlayerState p)
        {
            if (p.DashActive) return false;
            var dir = Vec2.FromAngle(p.Aim);
            double max = ScoutGrappleRange;
            Vec2 best = p.Position;
            for (double d = 0.24; d <= max; d += 0.16)
            {
                var pt = p.Position + dir * d;
                var (c, r) = WorldGrid.ToCell(pt);
                if (!_world.InBounds(c, r) || _world.IsSolid(c, r)) break;
                best = pt;
            }
            double dist = Vec2.Distance(best, p.Position);
            if (dist < 0.72) return false;
            const double dur = 0.22;
            p.DashActive = true;
            p.DashVelocity = dir * (dist / dur);
            p.DashTimeLeft = dur;
            GrappleFxTime = 0.34;
            return true;
        }

        /// <summary>드릴러 좌클릭 기반암 — 압력을 쌓아 need 에 도달하면 부순다.</summary>
        public bool ApplyDrillerPressure(PlayerState p, PlayerBuild b, int c, int r, double dt, int depth, bool boost)
        {
            if (b.Role != RoleId.Driller || !_world.InBounds(c, r)) return false;
            var t = _world.At(c, r);
            if (!TileTypes.IsBedrock(t)) return false;
            int k = _world.Index(c, r);

            double depthScale = 1 + Math.Max(0, depth - 1) * 0.22;
            double need = (t == TileType.Core ? 22.0 : 14.0) * depthScale;
            double warm = 0.35 + 0.65 * MiningSystem.WarmMul(p);
            double gain = Math.Max(0, dt) * warm * b.DrillMul * b.RoleDigMul * (boost ? 1.75 : 1) / need;

            if (!Cracks.TryGetValue(k, out var cs)) Cracks[k] = cs = new CrackState { Type = t };
            cs.Progress = Math.Min(1, cs.Progress + gain);
            cs.LastTouched = _time;

            if (cs.Progress >= 1)
            {
                BreakFoundation(c, r, t);
                return true;
            }
            return true;
        }

        void BreakFoundation(int c, int r, TileType t)
        {
            int k = _world.Index(c, r);
            _world.ForceClear(c, r);
            Cracks.Remove(k);
            FoundationBroken?.Invoke(new FoundationBrokenEvent { Col = c, Row = r, Type = t });
        }

        // ───────────────────────────── 틱
        public void Tick(PlayerState p, PlayerBuild b, double dt, int depth)
        {
            _time += dt;
            QCooldown = Math.Max(0, QCooldown - dt);
            ECooldown = Math.Max(0, ECooldown - dt);
            ShieldTime = Math.Max(0, ShieldTime - dt);
            BreakerCooldown = Math.Max(0, BreakerCooldown - dt);
            GrappleFxTime = Math.Max(0, GrappleFxTime - dt);

            TickBreach(p, b, dt, depth);
            TickCracks(dt);
            TickFlares(dt);
            TickBreakers(b, dt);
            TickEngineer(p, dt);
        }

        /// <summary>드릴러 Q — 0.55초 동안 전방 3칸을 관통 굴착.</summary>
        void TickBreach(PlayerState p, PlayerBuild b, double dt, int depth)
        {
            if (BreachTime <= 0) return;
            BreachTime -= dt;
            var dir = Vec2.FromAngle(p.Aim);
            double range = 3.0, step = 0.5;
            var seen = new HashSet<int>();
            for (double len = step; len <= range + 0.1; len += step)
            {
                var pt = p.Position + dir * len;
                var (c, r) = WorldGrid.ToCell(pt);
                if (!_world.InBounds(c, r) || !_world.IsSolid(c, r)) continue;
                int k = _world.Index(c, r);
                if (!seen.Add(k)) continue;
                var t = _world.At(c, r);
                if (TileTypes.IsBedrock(t)) ApplyDrillerPressure(p, b, c, r, dt * 2.2, depth, boost: true);
                else _world.Damage(c, r, SimTuning.DrillDps * SimTuning.DrillDamageMul * 3.0 * dt, dir);
            }
        }

        void TickCracks(double dt)
        {
            const double hold = 2.5, decay = 0.012;
            var dead = new List<int>();
            foreach (var kv in Cracks)
            {
                int k = kv.Key;
                if (!TileTypes.IsBedrock(_world.AtIndex(k))) { dead.Add(k); continue; }
                if (_time - kv.Value.LastTouched < 0.14) continue;
                if (_time - kv.Value.LastTouched > hold)
                {
                    kv.Value.Progress = Math.Max(0, kv.Value.Progress - dt * decay);
                    if (kv.Value.Progress <= 0) dead.Add(k);
                }
            }
            foreach (var k in dead) Cracks.Remove(k);
        }

        void TickFlares(double dt)
        {
            for (int i = Flares.Count - 1; i >= 0; i--)
            {
                Flares[i].Ttl -= dt;
                if (Flares[i].Ttl <= 0) Flares.RemoveAt(i);
            }
            for (int i = Nodes.Count - 1; i >= 0; i--)
            {
                Nodes[i].Life -= dt;
                if (Nodes[i].Life <= 0) Nodes.RemoveAt(i);
            }
            // 교체·수명 종료로 사라진 노드의 빛 정리
            Flares.RemoveAll(f => f.Node != null && !Nodes.Contains(f.Node));
        }

        void TickBreakers(PlayerBuild b, double dt)
        {
            for (int i = Breakers.Count - 1; i >= 0; i--)
            {
                var ch = Breakers[i];
                if (!ch.Stuck)
                {
                    ch.Travel -= dt;
                    if (ch.Travel <= 0) ch.Stuck = true;
                    continue;
                }
                ch.Fuse -= dt;
                if (ch.Fuse <= 0)
                {
                    Breakers.RemoveAt(i);
                    Explode(ch, early: false, gunMul: b.GunMul);
                }
            }
        }

        /// <summary>원본 infUpdateEngineer() — 급전 판정 후 사격.</summary>
        void TickEngineer(PlayerState p, double dt)
        {
            for (int i = Turrets.Count - 1; i >= 0; i--)
            {
                var t = Turrets[i];
                t.Life -= dt;
                if (t.Life <= 0) { Turrets.RemoveAt(i); continue; }
                t.Cooldown = Math.Max(0, t.Cooldown - dt);

                PowerNode source = null; double bd = double.MaxValue;
                foreach (var n in Nodes)
                {
                    double d = Vec2.Distance(n.Position, t.Position);
                    if (d <= n.Radius && d < bd) { bd = d; source = n; }
                }
                bool playerPower = Vec2.Distance(p.Position, t.Position) <= 2.25;
                bool autonomous = source == null && !playerPower && EngineerAutonomous;
                t.NodePowered = source != null;
                t.PlayerPowered = source == null && playerPower;
                t.Powered = t.NodePowered || playerPower || autonomous;
                if (!t.Powered || t.Cooldown > 0 || t.Ammo <= 0) continue;

                EnemyState best = null; double bestD = EngineerTurretRange;
                foreach (var e in _enemies.Enemies)
                {
                    if (!e.Alive) continue;
                    double d = Vec2.Distance(e.Position, t.Position);
                    if (d < bestD && SightUtil.IsClear(_world, t.Position, e.Position)) { best = e; bestD = d; }
                }
                if (best == null) continue;

                var dir = (best.Position - t.Position).Normalized;
                t.Aim = dir.Angle;
                t.Ammo--;
                t.Cooldown = EngineerTurretInterval;
                _projectiles.Projectiles.Add(new Projectile
                {
                    Position = t.Position + dir * 0.24,
                    Velocity = dir * SimTuning.TeCells(285),
                    Life = 1.05,
                    Power = EngineerTurretPower * (autonomous ? 0.5 : 1),
                    VisualId = "support",
                });
            }
        }

        static Vec2 PlaceInFront(PlayerState p, double dist) => p.Position + Vec2.FromAngle(p.Aim) * dist;

        public void Clear()
        {
            Flares.Clear(); Nodes.Clear(); Turrets.Clear(); Breakers.Clear(); Cracks.Clear();
            QCooldown = ECooldown = ShieldTime = BreachTime = BreakerCooldown = 0;
        }
    }
}
