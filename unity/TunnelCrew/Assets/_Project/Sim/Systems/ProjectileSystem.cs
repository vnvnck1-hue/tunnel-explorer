using System;
using System.Collections.Generic;

namespace TunnelCrew.Sim
{
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
        public double Age;
    }

    public struct ProjectileFiredEvent { public Vec2 Position; public double Angle; public string VisualId; public int Count; }
    public struct ProjectileEndedEvent { public Vec2 Position; public bool Exploded; public string VisualId; }
    public struct ReloadEvent { public bool Started; public bool Manual; }

    /// <summary>
    /// 원본 <c>tryFireGun()</c> (1946~1967) 과 <c>updateProjectiles()</c> (1968~2015).
    /// 처리 순서가 곧 우선순위다: 적 충돌 → 타일 충돌 → 소멸 → 폭발.
    /// </summary>
    public sealed class ProjectileSystem
    {
        public readonly List<Projectile> Projectiles = new List<Projectile>();

        public event Action<ProjectileFiredEvent> Fired;
        public event Action<ProjectileEndedEvent> Ended;
        public event Action<ReloadEvent> Reload;

        readonly WorldGrid _world;
        readonly EnemySystem _enemies;
        double _gunCd;

        public ProjectileSystem(WorldGrid world, EnemySystem enemies)
        {
            _world = world;
            _enemies = enemies;
        }

        /// <summary>발사 시도. 쿨·탄창·재장전 규칙을 여기서 판정한다.</summary>
        public bool TryFire(PlayerState player, PlayerBuild build, bool drillHeld)
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

            double speed = SimTuning.TeCells(laser ? 480 : 280) * (build.RoleGunMul > 1 ? 1.08 : 1.0);
            int shots = Math.Max(1, build.Shots);
            double spread = shots > 1 ? (laser ? 0.045 : 0.13) : 0;
            string visualId = VisualIdFor(build, laser, shots);

            double a = player.Aim;
            for (int i = 0; i < shots; i++)
            {
                double off = (i - (shots - 1) / 2.0) * spread;
                var dir = Vec2.FromAngle(a + off);
                Projectiles.Add(new Projectile
                {
                    Position = player.Position + dir * (SimTuning.PlayerRadius * 0.9),
                    Velocity = dir * speed,
                    Life = laser ? 0.72 : 1.2,
                    Pierce = build.Pierce + (laser ? 5 : 0),
                    Bounces = laser ? 0 : build.Bounces,
                    Explosive = build.Explosive || laser,
                    Laser = laser,
                    Power = sync,
                    VisualId = visualId,
                });
            }

            // 기본 0.22초, 거너 0.14초, 특성 fireRate 로 나눈다
            double cd = build.Role == RoleId.Gunner ? 0.14 : 0.22;
            _gunCd = cd / Math.Max(0.1, build.FireRate);

            Fired?.Invoke(new ProjectileFiredEvent { Position = player.Position, Angle = a, VisualId = visualId, Count = shots });
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

            double gunMul = build.RoleGunMul * build.GunMul;

            for (int i = Projectiles.Count - 1; i >= 0; i--)
            {
                var p = Projectiles[i];
                p.Position += p.Velocity * dt;
                p.Life -= dt;
                p.Age += dt;

                var n = p.Velocity.Normalized;
                bool hit = false;

                // ── 적 충돌
                foreach (var e in _enemies.Enemies)
                {
                    if (!e.Alive) continue;
                    if (Vec2.Distance(p.Position, e.Position) >= e.Radius + SimTuning.PxCells(6.0)) continue;

                    double dmg = SimTuning.EnemyGunDamage * gunMul * p.Power * (p.Laser ? 1.65 : 1.0);
                    _enemies.HurtEnemy(e, dmg, n, player.Position);

                    if (p.Pierce > 0) { p.Pierce--; p.Position += n * SimTuning.PxCells(12.0); }
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
                                double wallDmg = SimTuning.DrillDps * 0.28 * build.GunWallMul * p.Power * (p.Laser ? 1.8 : 1.0);
                                _world.Damage(c, r, wallDmg, n);
                                p.LastCell = k;
                            }

                            if (p.Pierce > 0) { p.Pierce--; p.Position += n * 0.72; }
                            else if (p.Bounces > 0)
                            {
                                // 축 반사: 셀 중심 기준으로 더 많이 벗어난 축을 뒤집는다
                                p.Bounces--;
                                var center = WorldGrid.CellCenter(c, r);
                                double dx = p.Position.X - center.X, dy = p.Position.Y - center.Y;
                                if (Math.Abs(dx) > Math.Abs(dy)) p.Velocity.X *= -1; else p.Velocity.Y *= -1;
                                p.Position += p.Velocity * (dt * 1.5);
                                p.LastCell = -1;
                            }
                            else hit = true;
                        }
                        else if (TileTypes.IsBedrock(t)) hit = true;
                    }
                }

                bool outOfWorld = p.Position.X < 0 || p.Position.Y < 0
                               || p.Position.X > _world.Cols || p.Position.Y > _world.Rows;
                bool ended = hit || p.Life <= 0 || outOfWorld;
                if (!ended) continue;

                if (p.Explosive) Burst(p.Position, build);
                Ended?.Invoke(new ProjectileEndedEvent { Position = p.Position, Exploded = p.Explosive, VisualId = p.VisualId });
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
                _enemies.HurtEnemy(e, SimTuning.EnemyGunDamage * 0.7 * gunMul, d / Math.Max(1e-6, dist), at);
            }
        }

        public void Clear() { Projectiles.Clear(); _gunCd = 0; }
    }
}
