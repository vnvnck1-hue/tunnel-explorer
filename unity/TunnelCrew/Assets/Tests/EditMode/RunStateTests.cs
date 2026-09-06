using System;
using NUnit.Framework;
using TunnelCrew.Sim;

namespace TunnelCrew.Tests
{
    /// <summary>층 진행(장악도·위협·스폰 압력)과 경험치 관문의 규칙. 원본 수치와 일치해야 한다.</summary>
    public class RunStateTests
    {
        static TunnelSim NewSim(RoleId role = RoleId.Driller, int depth = 1)
        {
            var sim = new TunnelSim();
            sim.StartRun(role);
            sim.EnterDepth(depth, DungeonConfig.Runtime);
            return sim;
        }

        [Test]
        public void Planet_MultipliersMatchOriginal()
        {
            Assert.AreEqual(1.0, Planet.WallHpMulFor(1));
            Assert.AreEqual(1.35, Planet.WallHpMulFor(2));
            Assert.AreEqual(1.8, Planet.WallHpMulFor(3));
            Assert.AreEqual(1.8 * 1.6, Planet.WallHpMulFor(4), 1e-9);
            Assert.AreEqual(1.9 * Math.Pow(1.55, 0.5), Planet.EnemyHpMulFor(4), 1e-9);
            Assert.AreEqual(0.22, Planet.DominanceTargetFor(1));
            Assert.AreEqual(0.30, Planet.DominanceTargetFor(3));
            Assert.AreEqual(0.34, Planet.DominanceTargetFor(7));
            Assert.IsTrue(Planet.IsAbyss(4));
            Assert.AreEqual("이상지대 1", Planet.DepthLabel(4));
        }

        [Test]
        public void Threat_Formulas_MatchOriginal()
        {
            var r = new RunState();
            // 초기: threat 1, cap 18+floor(1.5)=19, interval 5.2, burst 1
            Assert.AreEqual(1.0, r.Threat, 1e-9);
            Assert.AreEqual(19, r.EnemyCap);
            Assert.AreEqual(5.2, r.SpawnInterval, 1e-9);
            Assert.AreEqual(1, r.SpawnBurst);

            // 위협 상한 9, 캡 64, 간격 하한 0.62, 버스트 5
            for (int i = 0; i < 5000; i++) r.OnBlockBroken();
            r.Tick(100000);
            Assert.AreEqual(9.0, r.Threat, 1e-9);
            Assert.AreEqual(64, r.EnemyCap);
            Assert.AreEqual(5.2 / (1 + 8 * 0.55), r.SpawnInterval, 1e-9);   // 위협 9 에서도 하한 0.62 에는 닿지 않는다
            Assert.AreEqual(5, r.SpawnBurst);
        }

        [Test]
        public void EnterDepth_SetsWallHpMul_AndCountsBreakable()
        {
            var sim = NewSim(depth: 2);
            Assert.AreEqual(1.35, sim.World.WallHpMul);
            Assert.AreEqual(1.35, sim.Run.WallHpMul);
            Assert.AreEqual(1.4, sim.Enemies.EnemyHpMul);
            Assert.Greater(sim.Run.TotalBreakable, 100);
            Assert.AreEqual(0, sim.Run.FloorBroken);
        }

        [Test]
        public void BlockBroken_AddsSpawnDebt_AndFiresDominance()
        {
            var r = new RunState();
            double cap = r.OnBlockBroken();
            Assert.AreEqual(1, r.FloorBroken);
            Assert.AreEqual(0.30 + 0.006, r.SpawnDebt, 1e-9);
            Assert.AreEqual(2.1 - 0.018, cap, 1e-9);

            // 빚 정산: 정수 부분만, 최대 4
            for (int i = 0; i < 20; i++) r.OnBlockBroken();
            Assert.Greater(r.SpawnDebt, 4);
            double before = r.SpawnDebt;
            Assert.AreEqual(4, r.TakeSpawnDebt());
            Assert.AreEqual(before - 4, r.SpawnDebt, 1e-9);
        }

        [Test]
        public void Dominance_TriggersOnce_AtStratumTarget()
        {
            var sim = NewSim(depth: 1);
            int fired = 0;
            sim.DominanceReached += () => fired++;

            int need = (int)Math.Ceiling(sim.Run.TotalBreakable * 0.22);
            int broken = 0;
            for (int r = 1; r < sim.World.Rows - 1 && broken < need + 5; r++)
                for (int c = 1; c < sim.World.Cols - 1 && broken < need + 5; c++)
                {
                    var t = sim.World.At(c, r);
                    if (t == TileType.Empty || TileTypes.IsBedrock(t)) continue;
                    sim.World.ForceClear(c, r);
                    broken++;
                }
            Assert.IsTrue(sim.Run.DominanceReached);
            Assert.AreEqual(1, fired, "목표 도달은 한 번만 알린다");
            Assert.IsTrue(sim.Run.BossSpawned);
        }

        [Test]
        public void Xp_Curve_AndWeights()
        {
            Assert.AreEqual(42, XpGate.NeedFor(1));   // 30+10+2.4 = 42.4 → 42
            Assert.AreEqual(370, XpGate.NeedFor(10)); // 30+100+240
            Assert.AreEqual(1.0, XpGate.WeightFor(XpKind.Dig, RoleId.Driller));
            Assert.AreEqual(0.65, XpGate.WeightFor(XpKind.Combat, RoleId.Driller));
            Assert.AreEqual(1.0, XpGate.WeightFor(XpKind.Combat, RoleId.Gunner));
            Assert.AreEqual(0.70, XpGate.WeightFor(XpKind.Dig, RoleId.Gunner));
        }

        [Test]
        public void Xp_LevelsUp_OneAtATime_AndBlocksDuringBoss()
        {
            var sim = NewSim(RoleId.Driller);
            int ups = 0;
            sim.LeveledUp += e => ups++;

            // 흙 1 XP × 드릴러 dig 1.0 → 42개면 레벨 2
            for (int i = 0; i < 42; i++) sim.Xp.Award(1, XpKind.Dig, RoleId.Driller, checkLevel: false);
            Assert.AreEqual(0, ups);
            sim.Xp.CheckLevel();
            Assert.AreEqual(1, ups);
            Assert.AreEqual(2, sim.Xp.Level);
            Assert.AreEqual(XpGate.NeedFor(2), sim.Xp.XpNeed);

            // 보스 중에는 적립만
            sim.Run.BossActive = true;
            sim.Xp.Award(1000, XpKind.Dig, RoleId.Driller);
            Assert.AreEqual(2, sim.Xp.Level);
            sim.Run.BossActive = false;
            sim.Xp.CheckLevel();
            Assert.AreEqual(2, sim.Xp.Level, "카드가 열려 있으면 적립만");
            sim.PickTrait(0);
            sim.Xp.CheckLevel();
            Assert.AreEqual(3, sim.Xp.Level, "한 번에 한 레벨만");
        }

        [Test]
        public void Xp_FloorCap_LimitsCappedGrants_ResetsOnFloor()
        {
            var g = new XpGate();
            int total = 0;
            for (int i = 0; i < 100; i++) total += g.Award(2, XpKind.Support, RoleId.Engineer, capped: true);
            Assert.AreEqual(XpGate.FloorCap, total);
            g.OnFloorInit();
            Assert.Greater(g.Award(2, XpKind.Support, RoleId.Engineer, capped: true), 0);
        }

        [Test]
        public void Xp_KillAwards_ByRoleAndTurret()
        {
            var g = new XpGate();
            var e = new EnemyState { Hp = 0, HpMax = 10 };
            g.OnEnemyKilled(e, RoleId.Gunner, byTurret: false);
            Assert.AreEqual(4, g.Xp);                        // kill 4 × combat gunner 1.0
            var g2 = new XpGate();
            g2.OnEnemyKilled(e, RoleId.Engineer, byTurret: true);
            Assert.AreEqual(5, g2.Xp);                       // turretKill 5 × support engineer 1.0
            var g3 = new XpGate();
            e.IsApex = true;
            g3.OnEnemyKilled(e, RoleId.Driller, byTurret: false);
            Assert.AreEqual(JsMath.Round(4 * 2.4 * 0.65), g3.Xp);
        }

        [Test]
        public void Sim_BlockBreak_FeedsRunAndXp()
        {
            var sim = NewSim(RoleId.Driller);
            int gained = 0;
            sim.XpGained += e => gained += e.Amount;
            // 파괴 가능한 첫 블록을 강제 제거
            for (int r = 1; r < sim.World.Rows - 1; r++)
                for (int c = 1; c < sim.World.Cols - 1; c++)
                {
                    var t = sim.World.At(c, r);
                    if (t == TileType.Empty || TileTypes.IsBedrock(t)) continue;
                    sim.World.ForceClear(c, r);
                    goto done;
                }
            done:
            Assert.AreEqual(1, sim.Run.FloorBroken);
            Assert.Greater(sim.Run.SpawnDebt, 0);
            Assert.Greater(gained, 0);
        }
    }
}
