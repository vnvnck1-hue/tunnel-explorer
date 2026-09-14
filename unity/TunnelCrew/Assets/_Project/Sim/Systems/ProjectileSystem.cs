using System;
using System.Collections.Generic;

namespace TunnelCrew.Sim
{
    [Flags]
    public enum ProjectileStyleFlags : ushort
    {
        None = 0, Multi = 1 << 0, Pierce = 1 << 1, Ricochet = 1 << 2,
        Explosive = 1 << 3, Laser = 1 << 4, Support = 1 << 5, Shard = 1 << 6,
    }

    /// <summary>플레이어 탄 한 발. 원본 <c>G.projectiles[]</c> 의 원소.</summary>
    public sealed class Projectile
    {
        public Vec2 Position, Velocity;
        public double Life;
        public int Pierce, Bounces;
        public bool Explosive, Laser;
        /// <summary>드릴+사격 동시 사용 배율.</summary>
        public double Power = 1.0;
        /// <summary>같은 타일에 두 번 피해를 주지 않기 위한 가드.</summary>
        public int LastCell = -1;
        /// <summary>연출용 분류. standard / multi / pierce / ricochet / explosive / rain / laser.</summary>
        public string VisualId = "standard";
        /// <summary>효과가 겹치는 빌드의 시각 조합. VisualId 는 대표 형태, 이 값은 보조 문법을 보존한다.</summary>
        public ProjectileStyleFlags VisualFlags;
        public double Age;
        /// <summary>AI 크루 탄 — 사람의 특성 배율을 타지 않고 AiMul 로만 계산한다 (원본 p.ai/aiMul/aiOwner). null 이면 사람 탄.</summary>
        public object Owner;
        public double AiMul;
        public bool AiTurret;
        /// <summary>탄마다 다른 셰이더 패턴·맥동·실루엣 변주를 만드는 결정론적 시드.</summary>
        public uint VisualSeed;
    }

    public struct ProjectileFiredEvent { public Vec2 Position; public double Angle; public string VisualId; public ProjectileStyleFlags VisualFlags; public int Count; public bool Ai; public uint VisualSeed; }
    public struct ProjectileEndedEvent { public Vec2 Position; public bool Exploded; public string VisualId; public ProjectileStyleFlags VisualFlags; }
    public enum ProjectileImpactKind : byte { Expire, Enemy, Wall, Bedrock, Ricochet }
    public struct ProjectileImpactEvent
    {
        public Vec2 Position, Direction;
        public string VisualId;
        public ProjectileStyleFlags VisualFlags;
        public ProjectileImpactKind Kind;
        public bool Terminal, Exploded, Killed;
        public double Power;
    }
    public struct ReloadEvent { public bool Started; public bool Manual; }

    /// <summary>
    /// 원본 <c>tryFireGun()</c> (1946~1967) 과 <c>updateProjectiles()</c> (1968~2015).
    /// 처리 순서가 곧 우선순위다: 적 충돌 → 타일 충돌 → 소멸 → 폭발.
    /// </summary>
    public sealed class ProjectileSystem
    {
        /// <summary>플레이어·크루·설치물이 공유하는 모든 아군 투사체의 전역 속도 배율.</summary>
        public const double SpeedScale = 4.0;
        /// <summary>투사체가 적에게 주는 넉백은 기존 충격량의 30%만 적용한다.</summary>
        public const double EnemyKnockbackScale = 0.30;
        /// <summary>정확도 0일 때 단발 탄착이 흔들릴 수 있는 최대 각도(라디안).</summary>
        public const double MaxInaccuracyRadians = 0.24;
        /// <summary>정확도 페널티가 있는 탄은 중심선 근처로 뭉치지 않도록 최소 편차도 보장한다.</summary>
        public const double MinInaccuracyFraction = 0.42;
        /// <summary>고속탄이 한 틱 사이에 적이나 벽을 건너뛰지 않도록 충돌을 검사하는 최대 이동 거리.</summary>
        public const double MaxCollisionTravel = 0.30;
        /// <summary>캐릭터 중심에서 실제 탄이 생성되는 거리. 기존 0.45셀에서 몸 바로 앞으로 당겼다.</summary>
        public const double CharacterMuzzleOffset = 0.18;

        public readonly List<Projectile> Projectiles = new List<Projectile>();

        public event Action<ProjectileFiredEvent> Fired;
        public event Action<ProjectileImpactEvent> Impacted;
        public event Action<ProjectileEndedEvent> Ended;
        public event Action<ReloadEvent> Reload;
        /// <summary>AI 탄이 벽을 깎을 때 굴착 크레딧 주인을 TunnelSim 에 알린다.</summary>
        public Action<object> BreakSourceSetter;

        /// <summary>외부(AI 크루·센트리)가 만든 탄을 넣고 발사 이벤트를 낸다.</summary>
        public void Emit(Projectile p, double angle, Vec2? muzzleOrigin = null)
        {
            if (p.VisualFlags == ProjectileStyleFlags.None) p.VisualFlags = InferStyleFlags(p.VisualId);
            if (p.VisualSeed == 0) p.VisualSeed = NextVisualSeed();
            Projectiles.Add(p);
            Fired?.Invoke(new ProjectileFiredEvent
            {
                Position = muzzleOrigin ?? p.Position, Angle = angle, VisualId = p.VisualId,
                VisualFlags = p.VisualFlags, Count = 1, Ai = p.Owner != null || p.VisualId == "support",
                VisualSeed = p.VisualSeed,
            });
        }

        readonly WorldGrid _world;
        readonly EnemySystem _enemies;
        readonly Rng _shotRng = new Rng(0x47554E4Eu);
        readonly Rng _visualSeedRng = new Rng(0x56465831u);
        double _gunCd;

        public ProjectileSystem(WorldGrid world, EnemySystem enemies)
        {
            _world = world;
            _enemies = enemies;
        }

        uint NextVisualSeed()
        {
            _visualSeedRng.NextDouble();
            uint seed = _visualSeedRng.State;
            return seed == 0 ? 1u : seed;
        }

        /// <summary>정확도 페널티를 실제 각도 편차로 바꾼다. 중심선에 다시 뭉치지 않도록 최소 편차를 보장한다.</summary>
        public static double RollInaccuracy(Rng rng, double accuracy)
        {
            double inaccuracy = (1.0 - JsMath.Clamp(accuracy, 0.0, 1.0)) * MaxInaccuracyRadians;
            if (inaccuracy <= 0 || rng == null) return 0.0;
            double sign = rng.NextDouble() < .5 ? -1.0 : 1.0;
            double magnitude = inaccuracy * (MinInaccuracyFraction
                + (1.0 - MinInaccuracyFraction) * rng.NextDouble());
            return sign * magnitude;
        }

        /// <summary>발사 시도. 쿨·탄창·재장전 규칙을 여기서 판정한다.</summary>
        public bool TryFire(PlayerState player, PlayerBuild build, bool drillHeld, double equipmentPower = 1.0)
        {
            if (_gunCd > 0) return false;
            if (!build.RoleHasGun) return false;
            if (build.IsReloading) return false;
            if (build.Ammo <= 0) { StartReload(build, false); return false; }

            build.Ammo--;
            build.ShotCounter++;

            // 거너를 제외한 직업은 드릴과 사격을 동시에 쓸 때 배율을 받는다
            double sync = build.Role != RoleId.Gunner && drillHeld ? build.SyncMul : 1.0;
            bool laser = build.LaserEvery > 0 && build.ShotCounter % build.LaserEvery == 0;

            int shots = Math.Max(1, build.Shots);
            double spread = shots > 1 ? (laser ? 0.045 : build.ProjectileSpread >= 0 ? build.ProjectileSpread : 0.13) : 0;
            string visualId = VisualIdFor(build, laser, shots);
            double speed = SimTuning.TeCells(BaseSpeedPx(visualId)) * (build.RoleGunMul > 1 ? 1.08 : 1.0) * build.ProjectileSpeedMul;

            double a = player.Aim;
            uint firedVisualSeed = 0;
            for (int i = 0; i < shots; i++)
            {
                double jitter = RollInaccuracy(_shotRng, build.Accuracy);
                double off = (i - (shots - 1) / 2.0) * spread + jitter;
                var dir = Vec2.FromAngle(a + off);
                uint visualSeed = NextVisualSeed();
                if (firedVisualSeed == 0) firedVisualSeed = visualSeed;
                Projectiles.Add(new Projectile
                {
                    Position = player.Position + dir * CharacterMuzzleOffset,
                    Velocity = dir * speed,
                    Life = BaseLife(visualId) * build.ProjectileLifeMul,
                    Pierce = build.Pierce + (laser ? 5 : 0),
                    Bounces = laser ? 0 : build.Bounces,
                    Explosive = build.Explosive || laser,
                    Laser = laser,
                    Power = sync * equipmentPower,
                    VisualId = visualId,
                    VisualFlags = StyleFlagsFor(build, laser, shots),
                    VisualSeed = visualSeed,
                });
            }

            // 기본 0.22초, 거너 0.14초, 특성 fireRate 로 나눈다
            double cd = build.Role == RoleId.Gunner ? 0.14 : 0.22;
            _gunCd = cd / Math.Max(0.1, build.FireRate);

            Fired?.Invoke(new ProjectileFiredEvent
            {
                Position = player.Position, Angle = a, VisualId = visualId,
                VisualFlags = StyleFlagsFor(build, laser, shots), Count = shots, VisualSeed = firedVisualSeed,
            });
            return true;
        }

        static string VisualIdFor(PlayerBuild b, bool laser, int shots)
        {
            if (laser) return "laser";
            if (b.Explosive) return shots >= 3 ? "rain" : "explosive";
            if (b.Bounces > 0) return "ricochet";
            if (b.Pierce > 0) return "pierce";
            if (shots > 1) return "multi";
            return "standard";
        }

        static ProjectileStyleFlags StyleFlagsFor(PlayerBuild b, bool laser, int shots)
        {
            ProjectileStyleFlags flags = shots > 1 ? ProjectileStyleFlags.Multi : ProjectileStyleFlags.None;
            if (b.Pierce > 0 || laser) flags |= ProjectileStyleFlags.Pierce;
            if (b.Bounces > 0) flags |= ProjectileStyleFlags.Ricochet;
            if (b.Explosive || laser) flags |= ProjectileStyleFlags.Explosive;
            if (laser) flags |= ProjectileStyleFlags.Laser;
            return flags;
        }

        public static ProjectileStyleFlags InferStyleFlags(string visualId)
        {
            switch (visualId)
            {
                case "multi": return ProjectileStyleFlags.Multi;
                case "pierce": return ProjectileStyleFlags.Pierce;
                case "ricochet": return ProjectileStyleFlags.Ricochet;
                case "explosive": return ProjectileStyleFlags.Explosive;
                case "rain": return ProjectileStyleFlags.Multi | ProjectileStyleFlags.Explosive;
                case "laser": return ProjectileStyleFlags.Laser | ProjectileStyleFlags.Pierce | ProjectileStyleFlags.Explosive;
                case "support": return ProjectileStyleFlags.Support;
                case "shard": return ProjectileStyleFlags.Shard | ProjectileStyleFlags.Pierce;
                default: return ProjectileStyleFlags.None;
            }
        }

        /// <summary>시각적 속도 언어와 실제 이동 속도를 일치시킨다. 값은 원본처럼 px/s, 1셀=50px.</summary>
        public static double BaseSpeedPx(string visualId)
        {
            double speed;
            switch (visualId)
            {
                case "multi": speed = 300; break;
                case "pierce": speed = 500; break;
                case "ricochet": speed = 365; break;
                case "explosive": speed = 250; break;
                case "rain": speed = 315; break;
                case "laser": speed = 640; break;
                case "support": speed = 360; break;
                case "shard": speed = 390; break;
                default: speed = 340; break;
            }
            return speed * SpeedScale;
        }

        public static double BaseLife(string visualId)
        {
            switch (visualId)
            {
                case "multi": return .72;
                case "pierce": return .86;
                case "ricochet": return 1.35;
                case "explosive": return 1.05;
                case "rain": return .88;
                case "laser": return .58;
                default: return 1.05;
            }
        }

        public void StartReload(PlayerBuild build, bool manual)
        {
            if (build.IsReloading || build.Ammo >= build.MagSize) return;
            build.ReloadLeft = build.ReloadTime;
            build.ReloadCount++;
            Reload?.Invoke(new ReloadEvent { Started = true, Manual = manual });
        }

        public void Tick(PlayerState player, PlayerBuild build, double dt)
        {
            _gunCd = Math.Max(0, _gunCd - dt);

            if (build.ReloadLeft > 0)
            {
                build.ReloadLeft = Math.Max(0, build.ReloadLeft - dt);
                if (build.ReloadLeft <= 0)
                {
                    build.Ammo = build.MagSize;
                    Reload?.Invoke(new ReloadEvent { Started = false });
                }
            }

            double maxSpeed = 0.0;
            for (int i = 0; i < Projectiles.Count; i++)
                maxSpeed = Math.Max(maxSpeed, Projectiles[i].Velocity.Length);
            int steps = Math.Max(1, (int)Math.Ceiling(maxSpeed * Math.Max(0.0, dt) / MaxCollisionTravel));
            double stepDt = dt / steps;
            for (int step = 0; step < steps; step++) TickProjectiles(player, build, stepDt);
        }

        void TickProjectiles(PlayerState player, PlayerBuild build, double dt)
        {
            double gunMul = build.RoleGunMul * build.GunMul;

            for (int i = Projectiles.Count - 1; i >= 0; i--)
            {
                var p = Projectiles[i];
                p.Position += p.Velocity * dt;
                p.Life -= dt;
                p.Age += dt;

                var n = p.Velocity.Normalized;
                bool hit = false;
                bool impactEmitted = false;

                // ── 적 충돌
                foreach (var e in _enemies.Enemies)
                {
                    if (!e.Alive) continue;
                    if (Vec2.Distance(p.Position, e.Position) >= e.Radius + SimTuning.PxCells(6.0)) continue;

                    bool ai = p.Owner != null;
                    double dmg = ai ? SimTuning.EnemyGunDamage * p.AiMul * p.Power
                                    : SimTuning.EnemyGunDamage * gunMul * p.Power * (p.Laser ? 1.65 : 1.0);
                    if (ai) _enemies.DamageSource = p.Owner;
                    _enemies.HurtEnemy(e, dmg, n, p.Owner is ICrewTarget ct ? ct.Pos : player.Position,
                        byTurret: p.AiTurret || p.VisualId == "support", knockbackMul: EnemyKnockbackScale);
                    if (ai) _enemies.DamageSource = null;

                    bool terminal = p.Pierce <= 0;
                    Impacted?.Invoke(new ProjectileImpactEvent
                    {
                        Position = p.Position, Direction = n, VisualId = p.VisualId,
                        VisualFlags = p.VisualFlags == ProjectileStyleFlags.None ? InferStyleFlags(p.VisualId) : p.VisualFlags,
                        Kind = ProjectileImpactKind.Enemy, Terminal = terminal,
                        Exploded = p.Explosive && terminal, Killed = !e.Alive, Power = p.Power,
                    });
                    impactEmitted = true;
                    if (!terminal) { p.Pierce--; p.Position += n * SimTuning.PxCells(12.0); }
                    else hit = true;
                    break;
                }

                // ── 타일 충돌
                if (!hit)
                {
                    var (c, r) = WorldGrid.ToCell(p.Position);
                    if (_world.InBounds(c, r))
                    {
                        var t = _world.At(c, r);
                        if (t != TileType.Empty && !TileTypes.IsBedrock(t))
                        {
                            int k = _world.Index(c, r);
                            if (k != p.LastCell)
                            {
                                double wallDmg = p.Owner != null ? SimTuning.DrillDps * 0.28 * p.Power
                                               : SimTuning.DrillDps * 0.28 * build.GunWallMul * p.Power * (p.Laser ? 1.8 : 1.0);
                                if (p.Owner != null) BreakSourceSetter?.Invoke(p.Owner);
                                _world.Damage(c, r, wallDmg, n);
                                if (p.Owner != null) BreakSourceSetter?.Invoke(null);
                                p.LastCell = k;
                            }

                            bool terminal;
                            ProjectileImpactKind impactKind;
                            if (p.Pierce > 0)
                            {
                                p.Pierce--; p.Position += n * 0.72;
                                terminal = false; impactKind = ProjectileImpactKind.Wall;
                            }
                            else if (p.Bounces > 0)
                            {
                                // 축 반사: 셀 중심 기준으로 더 많이 벗어난 축을 뒤집는다
                                p.Bounces--;
                                var center = WorldGrid.CellCenter(c, r);
                                double dx = p.Position.X - center.X, dy = p.Position.Y - center.Y;
                                if (Math.Abs(dx) > Math.Abs(dy)) p.Velocity.X *= -1; else p.Velocity.Y *= -1;
                                p.Position += p.Velocity * (dt * 1.5);
                                p.LastCell = -1;
                                terminal = false; impactKind = ProjectileImpactKind.Ricochet;
                            }
                            else { hit = true; terminal = true; impactKind = ProjectileImpactKind.Wall; }
                            Impacted?.Invoke(new ProjectileImpactEvent
                            {
                                Position = p.Position, Direction = n, VisualId = p.VisualId,
                                VisualFlags = p.VisualFlags == ProjectileStyleFlags.None ? InferStyleFlags(p.VisualId) : p.VisualFlags,
                                Kind = impactKind, Terminal = terminal,
                                Exploded = p.Explosive && terminal, Power = p.Power,
                            });
                            impactEmitted = true;
                        }
                        else if (TileTypes.IsBedrock(t))
                        {
                            hit = true;
                            Impacted?.Invoke(new ProjectileImpactEvent
                            {
                                Position = p.Position, Direction = n, VisualId = p.VisualId,
                                VisualFlags = p.VisualFlags == ProjectileStyleFlags.None ? InferStyleFlags(p.VisualId) : p.VisualFlags,
                                Kind = ProjectileImpactKind.Bedrock, Terminal = true,
                                Exploded = p.Explosive, Power = p.Power,
                            });
                            impactEmitted = true;
                        }
                    }
                }

                bool outOfWorld = p.Position.X < 0 || p.Position.Y < 0
                               || p.Position.X > _world.Cols || p.Position.Y > _world.Rows;
                bool ended = hit || p.Life <= 0 || outOfWorld;
                if (!ended) continue;

                if (!impactEmitted)
                {
                    Impacted?.Invoke(new ProjectileImpactEvent
                    {
                        Position = p.Position, Direction = n, VisualId = p.VisualId,
                        VisualFlags = p.VisualFlags == ProjectileStyleFlags.None ? InferStyleFlags(p.VisualId) : p.VisualFlags,
                        Kind = ProjectileImpactKind.Expire, Terminal = true,
                        Exploded = p.Explosive && !outOfWorld, Power = p.Power,
                    });
                }
                if (p.Explosive) Burst(p.Position, build);
                Ended?.Invoke(new ProjectileEndedEvent { Position = p.Position, Exploded = p.Explosive, VisualId = p.VisualId, VisualFlags = p.VisualFlags });
                Projectiles.RemoveAt(i);
            }
        }

        /// <summary>원본 infProjectileBurst() — 3x3 벽 피해 + 반경 1.35셀 적 피해.</summary>
        void Burst(Vec2 at, PlayerBuild build)
        {
            var (c, r) = WorldGrid.ToCell(at);
            double gunMul = build.GunMul;
            for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++)
                {
                    if (dc == 0 && dr == 0) continue;
                    int cc = c + dc, rr = r + dr;
                    if (!_world.InBounds(cc, rr) || !_world.IsSolid(cc, rr)) continue;
                    if (TileTypes.IsBedrock(_world.At(cc, rr))) continue;
                    _world.Damage(cc, rr, SimTuning.DrillDps * 0.42 * gunMul, new Vec2(dc, dr));
                }

            foreach (var e in _enemies.Enemies)
            {
                var d = e.Position - at;
                double dist = d.Length;
                if (dist >= 1.35) continue;
                _enemies.HurtEnemy(e, SimTuning.EnemyGunDamage * 0.7 * gunMul,
                    d / Math.Max(1e-6, dist), at, knockbackMul: EnemyKnockbackScale);
            }
        }

        public void Clear() { Projectiles.Clear(); _gunCd = 0; }
    }
}
