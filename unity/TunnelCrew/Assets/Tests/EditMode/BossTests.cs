using System;
using System.Linq;
using NUnit.Framework;
using TunnelCrew.Sim;

namespace TunnelCrew.Tests
{
    /// <summary>보스 소환·수치·패턴·격파 규칙. 원본 13245~13524 와 일치해야 한다.</summary>
    public class BossTests
    {
        static TunnelSim NewSim(int depth = 1)
        {
            var sim = new TunnelSim();
            sim.StartRun(RoleId.Gunner);
            sim.EnterDepth(depth, DungeonConfig.Runtime);
            sim.Enemies.SpawnInterval = 9999;
            return sim;
        }

        static PlayerInput Idle(TunnelSim sim) => new PlayerInput { AimWorld = sim.Player.Position + new Vec2(1, 0) };

        static void Run(TunnelSim sim, double seconds)
        {
            int n = (int)Math.Ceiling(seconds / SimTuning.FixedDeltaTime);
            for (int i = 0; i < n; i++) sim.Tick(SimTuning.FixedDeltaTime, Idle(sim));
        }

        [Test]
        public void Tier_ByDepth()
        {
            Assert.AreEqual(BossTier.Guardian, BossTierDef.TierForDepth(1));
            Assert.AreEqual(BossTier.Guardian, BossTierDef.TierForDepth(2));
            Assert.AreEqual(BossTier.Apex, BossTierDef.TierForDepth(3));
            Assert.AreEqual(BossTier.Variant, BossTierDef.TierForDepth(4));
        }

        [Test]
        public void Spawn_Stats_MatchOriginal()
        {
            var sim = NewSim(depth: 1);
            var b = sim.Bosses.Spawn(sim.Player, 1);
            // hp = 81 * 11.2 * enemyHpMul(1) * 10 * .38 (수호자)
            Assert.AreEqual(81 * 11.2 * 10 * .38, b.Body.HpMax, 1e-6);
            // r = 22px(0.44셀) * 3.65 * .62 * 2.5
            Assert.AreEqual(SimTuning.EnemyRadius * 3.65 * .62 * 2.5, b.Body.Radius, 1e-9);
            Assert.AreEqual(1.35 * 1.45, b.Body.SpeedMul, 1e-9);
            Assert.IsTrue(b.Body.IsBoss);
            Assert.AreEqual(0, b.ArmorCells.Count, "수호자는 장갑 없음");
            Assert.IsTrue(sim.Run.BossActive);
            Assert.GreaterOrEqual(Vec2.Distance(b.Body.Position, sim.Player.Position), 5.0, "플레이어에서 5셀 이상 떨어져 나온다");
            Assert.IsTrue(sim.Enemies.Enemies.Contains(b.Body));
        }

        [Test]
        public void Spawn_Apex_HasArmorRing()
        {
            var sim = NewSim(depth: 3);
            var b = sim.Bosses.Spawn(sim.Player, 3);
            Assert.AreEqual(BossTier.Apex, b.Tier);
            Assert.Greater(b.ArmorCells.Count, 0);
            Assert.LessOrEqual(b.ArmorCells.Count, 6);
            foreach (int k in b.ArmorCells) Assert.AreEqual(TileType.Stone, sim.World.AtIndex(k));
            // 장갑은 보스 몸통 밖
            foreach (int k in b.ArmorCells)
            {
                var p = WorldGrid.CellCenter(k % sim.World.Cols, k / sim.World.Cols);
                Assert.GreaterOrEqual(Vec2.Distance(p, b.Body.Position), b.Body.Radius + 0.7 - 0.75);
            }
        }

        [Test]
        public void Dominance_SpawnsBoss_NextTick()
        {
            var sim = NewSim(depth: 1);
            int need = (int)Math.Ceiling(sim.Run.TotalBreakable * 0.22) + 2;
            int broken = 0;
            for (int r = 1; r < sim.World.Rows - 1 && broken < need; r++)
                for (int c = 1; c < sim.World.Cols - 1 && broken < need; c++)
                {
                    var t = sim.World.At(c, r);
                    if (t == TileType.Empty || TileTypes.IsBedrock(t)) continue;
                    sim.World.ForceClear(c, r); broken++;
                }
            Assert.IsFalse(sim.Bosses.Active, "파괴 콜백 안에서는 소환하지 않는다");
            sim.Tick(SimTuning.FixedDeltaTime, Idle(sim));
            Assert.IsTrue(sim.Bosses.Active);
            Assert.AreEqual(BossTier.Guardian, sim.Bosses.Boss.Tier);
        }

        [Test]
        public void Boss_UsesAttacks_OverTime()
        {
            var sim = NewSim(depth: 3);
            var b = sim.Bosses.Spawn(sim.Player, 3);
            int patterns = 0, summons = 0;
            sim.BossPattern += e => { patterns++; if (e.Pattern == "summon") summons++; };
            sim.Player.Hp = 1e9; sim.Player.HpMax = 1e9;
            Run(sim, 20);
            Assert.Greater(patterns, 2, "20초 안에 패턴이 여러 번 나온다");
            Assert.Greater(summons, 0, "소환 주기 4.2초");
            Assert.Greater(sim.Enemies.Enemies.Count, 1, "소환된 잡몹이 있다");
        }

        [Test]
        public void Boss_CrushesWalls_NotCountedAsDominance()
        {
            var sim = NewSim(depth: 1);
            var b = sim.Bosses.Spawn(sim.Player, 1);
            int broken0 = sim.Run.FloorBroken;
            // 보스를 벽 한가운데로 옮기고 한 틱
            for (int r = 3; r < sim.World.Rows - 3; r++)
                for (int c = 3; c < sim.World.Cols - 3; c++)
                    if (sim.World.At(c, r) == TileType.Dirt) { b.Body.Position = WorldGrid.CellCenter(c, r); goto placed; }
            placed:
            var (bc, br) = WorldGrid.ToCell(b.Body.Position);
            sim.Bosses.CrushWalls();
            Assert.AreEqual(TileType.Empty, sim.World.At(bc, br), "몸통 아래 벽은 뭉개진다");
            Assert.AreEqual(broken0, sim.Run.FloorBroken, "뭉갠 벽은 장악도에 잡히지 않는다");
        }

        [Test]
        public void BossShot_HitsInsideRadius_BypassesReduction()
        {
            var sim = NewSim(depth: 1);
            var b = sim.Bosses.Spawn(sim.Player, 1);
            double hp0 = sim.Player.Hp;
            sim.Player.IFrames = 0;
            bool hit = false; double dealt = 0;
            sim.BossShotHit += e => { hit = e.HitPlayer; dealt = e.Damage; };
            // 직접 탄을 하나 넣어 플레이어 위에 떨어뜨린다
            sim.Bosses.Shots.Add(new BossShot { Start = b.Body.Position, Target = sim.Player.Position, Flight = 0.05, Radius = 1.0, Power = 1, VisualId = "bossScatter" });
            sim.Bosses.Tick(sim.Player, 0.1, 1);
            Assert.IsTrue(hit);
            // max(9, round(5.5 * 3.55)) = 20 — 경감 없이 그대로
            Assert.AreEqual(20, dealt, 1e-9);
            Assert.AreEqual(hp0 - 20, sim.Player.Hp, 1e-9);
        }

        [Test]
        public void Defeat_RewardsCore_HealsAndEndsBossPhase()
        {
            var sim = NewSim(depth: 1);
            var b = sim.Bosses.Spawn(sim.Player, 1);
            sim.Player.Hp = 50;
            int core0 = sim.Loot.Core;
            bool defeated = false;
            sim.BossDefeated += e => defeated = true;
            sim.Enemies.HurtEnemy(b.Body, 1e12, new Vec2(1, 0), sim.Player.Position);
            sim.Tick(SimTuning.FixedDeltaTime, Idle(sim));
            Assert.IsTrue(defeated);
            Assert.IsFalse(sim.Bosses.Active);
            Assert.IsFalse(sim.Run.BossActive);
            Assert.AreEqual(core0 + 2 + 1, sim.Loot.Core, "수호자 coreBase 2 + depth 1");
            Assert.AreEqual(50 + sim.Player.HpMax * .3, sim.Player.Hp, 1e-9);
            Assert.AreEqual(1, sim.Run.BossesKilled);
        }

        [Test]
        public void WallPrison_RaisesHardenedWalls_AroundPlayer()
        {
            var sim = NewSim(depth: 1);
            var b = sim.Bosses.Spawn(sim.Player, 1);
            int raised = 0;
            sim.BossWallRaised += e => raised++;
            // 플레이어를 넓은 빈 공간 중앙에 두고 기믹 강제
            var mi = typeof(BossSystem).GetMethod("WallPrison", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            mi.Invoke(sim.Bosses, new object[] { sim.Player });
            Run(sim, 1.0);
            Assert.Greater(raised, 4);
            Assert.Greater(sim.Bosses.WallCells.Count, 4);
            foreach (int k in sim.Bosses.WallCells)
            {
                var t = sim.World.AtIndex(k);
                Assert.IsTrue(t == TileType.Stone || t == TileType.Rock);
                if (t == TileType.Stone)
                    Assert.AreEqual(TileTypes.BaseHp(TileType.Stone) * 5, sim.World.HpAt(k), 1e-9, "보스 벽은 5배 경도");
            }
        }
    }
}
