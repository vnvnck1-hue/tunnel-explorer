using System;
using System.Linq;
using NUnit.Framework;
using TunnelCrew.Sim;

namespace TunnelCrew.Tests
{
    /// <summary>
    /// 사격·재장전·직업 스킬의 규칙. 화면 없이 판정 가능한 것만 다룬다.
    /// </summary>
    public class CombatTests
    {
        static TunnelSim NewSim(RoleId role)
        {
            var sim = new TunnelSim();
            sim.StartRun(role);
            sim.EnterDepth(1, DungeonConfig.Runtime);
            sim.Enemies.SpawnInterval = 9999;
            return sim;
        }

        static PlayerInput Aim(TunnelSim sim, double angle = 0) => new PlayerInput
        {
            AimWorld = sim.Player.Position + Vec2.FromAngle(angle) * 3,
        };

        static void Run(TunnelSim sim, PlayerInput input, double seconds)
        {
            int n = (int)Math.Ceiling(seconds / SimTuning.FixedDeltaTime);
            for (int i = 0; i < n; i++) sim.Tick(SimTuning.FixedDeltaTime, input);
        }

        /// <summary>플레이어에서 angle 방향으로 뻗어 가장 가까운 벽 셀.</summary>
        static (int c, int r)? FirstWall(TunnelSim sim, double angle, double max = 8)
        {
            var dir = Vec2.FromAngle(angle);
            for (double d = 0.7; d <= max; d += 0.1)
            {
                var (c, r) = WorldGrid.ToCell(sim.Player.Position + dir * d);
                if (!sim.World.InBounds(c, r)) return null;
                if (sim.World.IsSolid(c, r)) return (c, r);
            }
            return null;
        }

        /// <summary>벽이 있는 방향을 찾는다 (8방향 중 하나는 반드시 있다).</summary>
        static double WallAngle(TunnelSim sim)
        {
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4;
                if (FirstWall(sim, a) != null) return a;
            }
            Assert.Fail("주변에 벽이 없다");
            return 0;
        }

        // ───────────────────────────── 사격

        [Test]
        public void Fire_ConsumesAmmo_AndRespectsCooldown()
        {
            var sim = NewSim(RoleId.Gunner);
            int mag = sim.Build.MagSize;
            var input = Aim(sim); input.FireHeld = true;

            sim.Tick(SimTuning.FixedDeltaTime, input);
            Assert.AreEqual(mag - 1, sim.Build.Ammo, "첫 틱에 한 발");
            Assert.AreEqual(1, sim.Projectiles.Projectiles.Count);

            sim.Tick(SimTuning.FixedDeltaTime, input);
            Assert.AreEqual(mag - 1, sim.Build.Ammo, "쿨다운 중에는 발사되지 않는다");
        }

        [Test]
        public void Fire_EmptyMag_StartsAutoReload_AndRefills()
        {
            var sim = NewSim(RoleId.Gunner);
            var input = Aim(sim); input.FireHeld = true;
            bool reloadStarted = false, manual = true;
            sim.ReloadChanged += e => { if (e.Started) { reloadStarted = true; manual = e.Manual; } };

            // 조준점 착탄으로 투사체 수명이 짧아져도 재장전 경계 자체만 검증하도록,
            // 첫 자동 재장전이 시작되는 순간 발사 입력을 놓는다.
            for (int i = 0; i < 1000 && !reloadStarted; i++)
                sim.Tick(SimTuning.FixedDeltaTime, input);
            Assert.IsTrue(reloadStarted, "탄이 비면 자동 재장전이 시작된다");
            Assert.IsFalse(manual);

            Run(sim, Aim(sim), sim.Build.ReloadTime + 0.1);
            Assert.AreEqual(sim.Build.MagSize, sim.Build.Ammo, "재장전이 끝나면 탄창이 찬다");
        }

        [Test]
        public void ManualReload_OnlyWhenMagNotFull()
        {
            var sim = NewSim(RoleId.Driller);
            var input = Aim(sim); input.ReloadPressed = true;
            sim.Tick(SimTuning.FixedDeltaTime, input);
            Assert.IsFalse(sim.Build.IsReloading, "가득 찬 탄창은 재장전하지 않는다");

            var fire = Aim(sim); fire.FireHeld = true;
            sim.Tick(SimTuning.FixedDeltaTime, fire);
            sim.Tick(SimTuning.FixedDeltaTime, input);
            Assert.IsTrue(sim.Build.IsReloading);
        }

        [Test]
        public void Projectile_HitsWall_AndDamagesIt()
        {
            var sim = NewSim(RoleId.Gunner);
            double a = WallAngle(sim);
            var wall = FirstWall(sim, a).Value;
            int k = sim.World.Index(wall.c, wall.r);
            double hp0 = sim.World.HpAt(k);
            var t = sim.World.At(wall.c, wall.r);

            bool ended = false;
            sim.ProjectileEnded += e => ended = true;
            // 커서가 곧 탄착점이므로 실제 벽 셀을 조준해야 벽까지 날아간다.
            var input = new PlayerInput
            {
                AimWorld = WorldGrid.CellCenter(wall.c, wall.r),
                FireHeld = true,
            };
            Run(sim, input, 0.5);

            Assert.IsTrue(ended, "탄이 벽에 닿아 소멸한다");
            if (!TileTypes.IsBedrock(t))
                Assert.IsTrue(!sim.World.IsSolid(wall.c, wall.r) || sim.World.HpAt(k) < hp0,
                    "벽이 파괴 가능하면 체력이 깎이거나 완전히 파괴된다");
            else
                Assert.AreEqual(hp0, sim.World.HpAt(k), "기반암은 총으로 깎이지 않는다");
        }

        [Test]
        public void Projectile_HitsEnemy_UsesRoleGunMul()
        {
            double DamageFor(RoleId role)
            {
                var sim = NewSim(role);
                sim.Build.RoleHasGun = true;
                var e = sim.Enemies.Spawn(sim.Player.Position);
                Assert.NotNull(e);
                e.Position = sim.Player.Position + new Vec2(1.6, 0);
                e.Hp = e.HpMax = 100000;
                double dealt = 0;
                sim.EnemyHurt += ev => dealt += ev.Damage;
                var input = Aim(sim, 0); input.FireHeld = true;
                // 첫 발만: 한 틱 발사 후 탄이 날아갈 시간
                sim.Tick(SimTuning.FixedDeltaTime, input);
                Run(sim, Aim(sim, 0), 0.3);
                return dealt;
            }

            double gunner = DamageFor(RoleId.Gunner);
            double driller = DamageFor(RoleId.Driller);
            Assert.Greater(gunner, 0);
            Assert.Greater(gunner, driller, "거너의 총 배율이 드릴러보다 높다");
        }

        // ───────────────────────────── 직업 Q/E

        [Test]
        public void Cooldowns_MatchOriginal()
        {
            Assert.AreEqual(7, RoleSystem.QCooldownFor(RoleId.Driller));
            Assert.AreEqual(8, RoleSystem.QCooldownFor(RoleId.Gunner));
            Assert.AreEqual(5, RoleSystem.QCooldownFor(RoleId.Scout));
            Assert.AreEqual(7, RoleSystem.QCooldownFor(RoleId.Engineer));
            Assert.IsFalse(RoleSystem.HasE(RoleId.Driller));
            Assert.AreEqual(5, RoleSystem.ECooldownFor(RoleId.Scout));
            Assert.AreEqual(10, RoleSystem.ECooldownFor(RoleId.Engineer));
        }

        [Test]
        public void Gunner_Q_Shield_ReducesIncomingDamage()
        {
            var sim = NewSim(RoleId.Gunner);
            var input = Aim(sim); input.SkillQPressed = true;
            sim.Tick(SimTuning.FixedDeltaTime, input);
            Assert.Greater(sim.Roles.ShieldTime, 0);
            Assert.AreEqual(8, sim.Roles.QCooldown, 0.05);

            // 방어막 중에는 무적 프레임이 걸려 피해가 0
            double hp0 = sim.Player.Hp;
            sim.Enemies.ApplyPlayerDamage(sim.Player, 40, new Vec2(1, 0));
            Assert.AreEqual(hp0, sim.Player.Hp);

            // 무적이 끝나고 방어막만 남은 상태를 흉내 — 0.35 배
            sim.Player.IFrames = 0;
            sim.Enemies.ApplyPlayerDamage(sim.Player, 40, new Vec2(1, 0));
            double expected = Math.Max(4, JsMath.Round(40 * 0.85)) * 0.35;
            Assert.AreEqual(hp0 - expected, sim.Player.Hp, 0.01);
        }

        [Test]
        public void Scout_Q_PlacesFlare_E_GrappleNeedsWall()
        {
            var sim = NewSim(RoleId.Scout);
            var input = Aim(sim); input.SkillQPressed = true;
            sim.Tick(SimTuning.FixedDeltaTime, input);
            Assert.AreEqual(1, sim.Roles.Flares.Count);
            Assert.Greater(sim.Roles.Flares[0].LightRadius, 4.0);

            // 재사용은 쿨이 끝나야 한다
            sim.Tick(SimTuning.FixedDeltaTime, input);
            Assert.AreEqual(1, sim.Roles.Flares.Count);

            double a = WallAngle(sim);
            var before = sim.Player.Position;
            var grapple = Aim(sim, a); grapple.SkillEPressed = true;
            sim.Tick(SimTuning.FixedDeltaTime, grapple);
            Run(sim, Aim(sim, a), 0.5);
            Assert.Greater(Vec2.Distance(before, sim.Player.Position), 0.3, "벽을 향한 그래플은 플레이어를 끌어당긴다");
        }

        [Test]
        public void Engineer_Q_Node_E_Turret_RespectCaps()
        {
            var sim = NewSim(RoleId.Engineer);
            for (int i = 0; i < 4; i++)
            {
                sim.Roles.QCooldown = 0; sim.Roles.ECooldown = 0;
                var input = Aim(sim); input.SkillQPressed = true; input.SkillEPressed = true;
                sim.Tick(SimTuning.FixedDeltaTime, input);
            }
            Assert.AreEqual(sim.Build.Roles.EngineerMaxNodes, sim.Roles.Nodes.Count);
            Assert.AreEqual(sim.Build.Roles.EngineerMaxTurrets, sim.Roles.Turrets.Count);
            Assert.AreEqual(sim.Build.Roles.EngineerMaxNodes, sim.Roles.Flares.Count(f => f.IsEngineerNode), "노드마다 빛 하나");
        }

        [Test]
        public void Engineer_Turret_ShootsEnemyInRange()
        {
            var sim = NewSim(RoleId.Engineer);
            var input = Aim(sim); input.SkillEPressed = true;
            sim.Tick(SimTuning.FixedDeltaTime, input);
            Assert.AreEqual(1, sim.Roles.Turrets.Count);
            var t = sim.Roles.Turrets[0];

            var e = sim.Enemies.Spawn(sim.Player.Position);
            Assert.NotNull(e);
            e.Position = t.Position + new Vec2(1.5, 0);
            e.Hp = e.HpMax = 100000;

            Run(sim, Aim(sim), 1.5);
            Assert.Less(t.Ammo, t.Mag, "센트리가 사격했다");
        }

        [Test]
        public void Gunner_Breaker_AttachesToWall_ThenExplodes()
        {
            var sim = NewSim(RoleId.Gunner);
            double a = WallAngle(sim);
            var wall = FirstWall(sim, a).Value;
            var t = sim.World.At(wall.c, wall.r);

            bool exploded = false;
            sim.BreakerExploded += e => exploded = true;
            var input = Aim(sim, a); input.DrillHeld = true;   // 거너 좌클릭 = 파쇄탄
            sim.Tick(SimTuning.FixedDeltaTime, input);
            Assert.AreEqual(1, sim.Roles.Breakers.Count);
            Assert.AreEqual(sim.Build.Roles.BreakerMaxCd, sim.Roles.BreakerCooldown, 0.05);

            Run(sim, Aim(sim, a), sim.Build.Roles.BreakerFuse + 0.5);
            Assert.IsTrue(exploded, "도화선이 끝나면 폭발한다");
            Assert.AreEqual(0, sim.Roles.Breakers.Count);
            if (!TileTypes.IsBedrock(t))
                Assert.IsTrue(sim.World.At(wall.c, wall.r) == TileType.Empty || sim.World.HpAt(sim.World.Index(wall.c, wall.r)) < sim.World.MaxHp(t),
                    "붙어 있던 벽이 부서지거나 손상된다");
        }

        [Test]
        public void Gunner_E_DetonatesEarly()
        {
            var sim = NewSim(RoleId.Gunner);
            double a = WallAngle(sim);
            bool early = false;
            sim.BreakerExploded += e => early = e.Early;
            var input = Aim(sim, a); input.DrillHeld = true;
            sim.Tick(SimTuning.FixedDeltaTime, input);
            Run(sim, Aim(sim, a), 0.3);
            var det = Aim(sim, a); det.SkillEPressed = true;
            sim.Tick(SimTuning.FixedDeltaTime, det);
            Assert.IsTrue(early);
        }

        [Test]
        public void Driller_Pressure_CracksBedrock()
        {
            var sim = NewSim(RoleId.Driller);
            // 기반암 셀을 하나 골라 플레이어를 그 옆에 놓는다
            int bc = -1, br = -1;
            for (int r = 2; r < sim.World.Rows - 2 && bc < 0; r++)
                for (int c = 2; c < sim.World.Cols - 2; c++)
                    if (sim.World.At(c, r) == TileType.Core && !sim.World.IsSolid(c - 1, r)) { bc = c; br = r; break; }
            if (bc < 0) Assert.Ignore("이 층에는 접근 가능한 Core 가 없다");

            sim.Player.Position = WorldGrid.CellCenter(bc - 1, br);
            sim.Player.Velocity = Vec2.Zero;
            bool broken = false;
            sim.FoundationBroken += e => broken = true;

            int k = sim.World.Index(bc, br);
            var input = new PlayerInput { AimWorld = WorldGrid.CellCenter(bc, br), DrillHeld = true };
            Run(sim, input, 1.0);
            Assert.IsTrue(sim.Roles.Cracks.ContainsKey(k) || broken, "기반암을 드릴하면 균열이 쌓인다");

            Run(sim, input, 40.0);
            Assert.IsTrue(broken, "압력이 차면 기반암이 뚫린다");
            Assert.AreEqual(TileType.Empty, sim.World.At(bc, br));
        }

        [Test]
        public void EnterDepth_ClearsRoleState()
        {
            var sim = NewSim(RoleId.Engineer);
            var input = Aim(sim); input.SkillQPressed = true; input.SkillEPressed = true;
            sim.Tick(SimTuning.FixedDeltaTime, input);
            Assert.AreEqual(1, sim.Roles.Nodes.Count);
            sim.EnterDepth(2, DungeonConfig.Runtime);
            Assert.AreEqual(0, sim.Roles.Nodes.Count);
            Assert.AreEqual(0, sim.Roles.Turrets.Count);
            Assert.AreEqual(0, sim.Roles.Flares.Count);
        }
    }
}
