using NUnit.Framework;
using TunnelCrew.Presentation;
using TunnelCrew.Sim;

namespace TunnelCrew.Tests
{
    public sealed class FtueTests
    {
        [Test]
        public void HintsEscalateWithoutInterruptingTheFirstThreeSeconds()
        {
            var model = new FtueProgressModel();
            model.Set(FtueProgressModel.Stage.Movement, 10);

            Assert.AreEqual(FtueProgressModel.Hint.None, model.Tick(12.99));
            Assert.AreEqual(FtueProgressModel.Hint.Input, model.Tick(13));
            Assert.AreEqual(FtueProgressModel.Hint.Direction, model.Tick(22));
            Assert.AreEqual(FtueProgressModel.Hint.Direct, model.Tick(32));
        }

        [Test]
        public void ComicAndChoiceStagesNeverAccumulateGameplayHints()
        {
            var model = new FtueProgressModel();
            model.Set(FtueProgressModel.Stage.CrashComic, 0);
            Assert.AreEqual(FtueProgressModel.Hint.None, model.Tick(99));
            model.Set(FtueProgressModel.Stage.RoleChoice, 100);
            Assert.AreEqual(FtueProgressModel.Hint.None, model.Tick(199));
            model.Set(FtueProgressModel.Stage.EscapeComic, 200);
            Assert.AreEqual(FtueProgressModel.Hint.None, model.Tick(299));
        }

        [Test]
        public void AuthoredWorldHasTwoSingleBreakableGatesAndReachableRooms()
        {
            var sim = new TunnelSim();
            sim.StartRun(RoleId.Driller);
            sim.EnterDepth(1, DungeonConfig.Runtime);

            var layout = FtueDirector.BuildAuthoredWorld(sim);
            var first = WorldGrid.ToCell(layout.FirstWall);
            var rescue = WorldGrid.ToCell(layout.RescueWall);
            var flare = WorldGrid.ToCell(layout.Flare);
            var arena = WorldGrid.ToCell(layout.Arena);
            var blackbox = WorldGrid.ToCell(layout.Blackbox);

            Assert.AreEqual(TileType.Ore, sim.World.At(first.c, first.r));
            Assert.AreEqual(TileType.Dirt, sim.World.At(rescue.c, rescue.r));
            Assert.AreEqual(TileType.Empty, sim.World.At(flare.c, flare.r));
            Assert.AreEqual(TileType.Empty, sim.World.At(arena.c, arena.r));
            Assert.AreEqual(TileType.Empty, sim.World.At(blackbox.c, blackbox.r));
            Assert.AreEqual(layout.Entry.X < layout.Blackbox.X, layout.FirstWall.X < layout.RescueWall.X);
            Assert.AreEqual(sim.World.Index(first.c, first.r), layout.FirstWallCell);
            Assert.AreEqual(sim.World.Index(rescue.c, rescue.r), layout.RescueWallCell);
        }
    }
}
