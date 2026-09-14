using System;
using NUnit.Framework;
using TunnelCrew.Sim;

namespace TunnelCrew.Tests
{
    public sealed class ProjectileVfxSimulationTests
    {
        [Test]
        public void 탄종별_실제_속도가_보이는_무게와_일치한다()
        {
            Assert.That(ProjectileSystem.SpeedScale, Is.EqualTo(4.0));
            Assert.That(ProjectileSystem.BaseSpeedPx("standard"), Is.EqualTo(1360));
            Assert.That(ProjectileSystem.BaseSpeedPx("laser"), Is.EqualTo(2560));
            Assert.That(ProjectileSystem.BaseSpeedPx("laser"), Is.GreaterThan(ProjectileSystem.BaseSpeedPx("pierce")));
            Assert.That(ProjectileSystem.BaseSpeedPx("pierce"), Is.GreaterThan(ProjectileSystem.BaseSpeedPx("standard")));
            Assert.That(ProjectileSystem.BaseSpeedPx("standard"), Is.GreaterThan(ProjectileSystem.BaseSpeedPx("multi")));
            Assert.That(ProjectileSystem.BaseSpeedPx("multi"), Is.GreaterThan(ProjectileSystem.BaseSpeedPx("explosive")));
            Assert.That(ProjectileSystem.BaseLife("ricochet"), Is.GreaterThan(ProjectileSystem.BaseLife("multi")));
        }

        [Test]
        public void 거너_기본총은_정확도_감소만큼_조준선에서_흔들린다()
        {
            var sim = new TunnelSim();
            sim.StartRun(RoleId.Gunner);
            sim.EnterDepth(1, DungeonConfig.Runtime);
            sim.Player.Aim = 0;

            Assert.That(sim.Projectiles.TryFire(sim.Player, sim.Build, false), Is.True);
            var projectile = sim.Projectiles.Projectiles[0];
            var velocity = projectile.Velocity;
            double angle = Math.Atan2(velocity.Y, velocity.X);
            double maxDeviation = (1.0 - sim.Build.Accuracy) * ProjectileSystem.MaxInaccuracyRadians;
            double minDeviation = maxDeviation * ProjectileSystem.MinInaccuracyFraction;

            Assert.That(ProjectileSystem.MaxInaccuracyRadians, Is.EqualTo(.24).Within(1e-9));
            Assert.That(Math.Abs(angle), Is.GreaterThanOrEqualTo(minDeviation - 1e-9));
            Assert.That(Math.Abs(angle), Is.LessThanOrEqualTo(maxDeviation + 1e-9));
            Assert.That(Vec2.Distance(projectile.Position, sim.Player.Position),
                Is.EqualTo(ProjectileSystem.CharacterMuzzleOffset).Within(1e-9));
            Assert.That(projectile.VisualSeed, Is.Not.Zero);
        }

        [Test]
        public void 거너_기본총은_초당_약_열네발로_연사한다()
        {
            var sim = new TunnelSim();
            sim.StartRun(RoleId.Gunner);
            sim.EnterDepth(1, DungeonConfig.Runtime);

            Assert.That(sim.Projectiles.TryFire(sim.Player, sim.Build, false), Is.True);
            uint firstSeed = sim.Projectiles.Projectiles[0].VisualSeed;
            sim.Projectiles.Tick(sim.Player, sim.Build, .069);
            Assert.That(sim.Projectiles.TryFire(sim.Player, sim.Build, false), Is.False);
            sim.Projectiles.Tick(sim.Player, sim.Build, .002);
            Assert.That(sim.Projectiles.TryFire(sim.Player, sim.Build, false), Is.True);
            uint secondSeed = sim.Projectiles.Projectiles[sim.Projectiles.Projectiles.Count - 1].VisualSeed;
            Assert.That(secondSeed, Is.Not.EqualTo(firstSeed));
        }

        [Test]
        public void 초고속_투사체도_한_틱_사이에_벽을_건너뛰지_않는다()
        {
            var sim = new TunnelSim();
            sim.StartRun(RoleId.Gunner);
            sim.EnterDepth(1, DungeonConfig.Runtime);
            sim.Enemies.SpawnInterval = 9999;
            Assert.That(FindOpenTowardWall(sim, out var start, out var dir), Is.True);

            ProjectileImpactEvent? impact = null;
            sim.ProjectileImpacted += e => { if (e.Kind == ProjectileImpactKind.Wall) impact = e; };
            sim.Projectiles.Projectiles.Add(new Projectile
            {
                Position = start,
                Velocity = dir * 200.0,
                Life = 1,
                VisualId = "standard",
            });

            sim.Projectiles.Tick(sim.Player, sim.Build, .02);
            Assert.That(impact.HasValue, Is.True);
            Assert.That(impact.Value.Terminal, Is.True);
        }

        [TestCase(false, ProjectileImpactKind.Wall, true)]
        [TestCase(true, ProjectileImpactKind.Ricochet, false)]
        public void 벽_충돌은_소멸과_도탄을_구분해_연출_이벤트를_낸다(bool bounce, ProjectileImpactKind expected, bool terminal)
        {
            var sim = new TunnelSim();
            sim.StartRun(RoleId.Gunner);
            sim.EnterDepth(1, DungeonConfig.Runtime);
            sim.Enemies.SpawnInterval = 9999;

            Assert.That(FindOpenTowardWall(sim, out var start, out var dir), Is.True, "벽에 인접한 열린 셀이 필요하다");
            ProjectileImpactEvent? impact = null;
            sim.ProjectileImpacted += e => { if (e.Kind != ProjectileImpactKind.Expire) impact = e; };
            sim.Projectiles.Projectiles.Add(new Projectile
            {
                Position = start,
                Velocity = dir * 6.0,
                Life = 1,
                Bounces = bounce ? 1 : 0,
                VisualId = bounce ? "ricochet" : "standard",
            });

            for (int i = 0; i < 20 && impact == null; i++) sim.Tick(SimTuning.FixedDeltaTime, default);
            Assert.That(impact.HasValue, Is.True);
            Assert.That(impact.Value.Kind, Is.EqualTo(expected));
            Assert.That(impact.Value.Terminal, Is.EqualTo(terminal));
        }

        static bool FindOpenTowardWall(TunnelSim sim, out Vec2 start, out Vec2 dir)
        {
            int[] dc = { 1, -1, 0, 0 }, dr = { 0, 0, 1, -1 };
            for (int r = 1; r < sim.World.Rows - 1; r++)
                for (int c = 1; c < sim.World.Cols - 1; c++)
                {
                    if (sim.World.At(c, r) != TileType.Empty) continue;
                    for (int i = 0; i < 4; i++)
                    {
                        var tile = sim.World.At(c + dc[i], r + dr[i]);
                        if (tile == TileType.Empty || TileTypes.IsBedrock(tile)) continue;
                        start = WorldGrid.CellCenter(c, r);
                        dir = new Vec2(dc[i], dr[i]);
                        return true;
                    }
                }
            start = Vec2.Zero; dir = Vec2.Zero;
            return false;
        }
    }
}
