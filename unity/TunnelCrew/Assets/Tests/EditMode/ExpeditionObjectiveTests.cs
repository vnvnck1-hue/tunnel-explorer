using NUnit.Framework;
using TunnelCrew.Sim;

namespace TunnelCrew.Tests
{
    public sealed class ExpeditionObjectiveTests
    {
        static TunnelSim Sim(ExpeditionObjectiveId id)
        {
            var sim = new TunnelSim();
            sim.StartRun(RoleId.Driller, null, id);
            sim.EnterDepth(1, DungeonConfig.Runtime);
            return sim;
        }

        [TestCase(ExpeditionObjectiveId.CoreRecovery)]
        [TestCase(ExpeditionObjectiveId.Survey)]
        public void TargetObjectivesAlwaysCreateThreeReachableCandidates(ExpeditionObjectiveId id)
        {
            var sim = Sim(id);
            Assert.AreEqual(3, sim.Objective.Targets.Count);
            foreach (int k in sim.Objective.Targets)
            {
                int c = k % sim.World.Cols, r = k / sim.World.Cols;
                Assert.Greater(Vec2.Distance(WorldGrid.CellCenter(c, r), sim.World.EntryPosition), 4.9);
                if (id == ExpeditionObjectiveId.CoreRecovery)
                {
                    Assert.AreEqual(TileType.Crys, sim.World.At(c, r));
                    Assert.IsTrue(!sim.World.IsSolid(c + 1, r) || !sim.World.IsSolid(c - 1, r) ||
                                  !sim.World.IsSolid(c, r + 1) || !sim.World.IsSolid(c, r - 1));
                }
                else Assert.IsFalse(sim.World.IsSolid(c, r));
            }
        }

        [Test]
        public void CoreRecoveryCompletesWithoutDominanceAndRequestsBossOnce()
        {
            var sim = Sim(ExpeditionObjectiveId.CoreRecovery);
            int bossGate = 0; sim.DominanceReached += () => bossGate++;
            foreach (int k in sim.Objective.Targets)
                sim.World.ForceClear(k % sim.World.Cols, k / sim.World.Cols);
            Assert.IsTrue(sim.Objective.Completed);
            Assert.IsTrue(sim.Run.BossSpawned);
            Assert.AreEqual(1, bossGate);
            Assert.Less(sim.Run.Dominance, sim.Run.DominanceTarget);
        }

        [Test]
        public void SurveyProgressPersistsWhenPlayerLeaves()
        {
            var sim = Sim(ExpeditionObjectiveId.Survey);
            int k = sim.Objective.Targets[0];
            sim.Player.Position = WorldGrid.CellCenter(k % sim.World.Cols, k / sim.World.Cols);
            sim.Objective.Tick(sim.Player, .6);
            double before = sim.Objective.SurveyProgress(k);
            sim.Player.Position = sim.World.EntryPosition;
            sim.Objective.Tick(sim.Player, 5);
            Assert.AreEqual(before, sim.Objective.SurveyProgress(k), 1e-9, "이탈해도 측량 진척은 리셋하지 않는다");
        }

        [Test]
        public void AlternateObjectiveDoesNotSpawnBossFromOrdinaryDominance()
        {
            var sim = Sim(ExpeditionObjectiveId.Survey);
            int need = (int)System.Math.Ceiling(sim.Run.TotalBreakable * sim.Run.DominanceTarget);
            for (int r = 1; r < sim.World.Rows - 1 && sim.Run.FloorBroken < need + 2; r++)
                for (int c = 1; c < sim.World.Cols - 1 && sim.Run.FloorBroken < need + 2; c++)
                    if (sim.World.At(c, r) != TileType.Empty && !TileTypes.IsBedrock(sim.World.At(c, r))) sim.World.ForceClear(c, r);
            Assert.IsTrue(sim.Run.DominanceReached);
            Assert.IsFalse(sim.Run.BossSpawned, "선택한 목표를 장악도로 우회할 수 없다");
        }
    }
}
