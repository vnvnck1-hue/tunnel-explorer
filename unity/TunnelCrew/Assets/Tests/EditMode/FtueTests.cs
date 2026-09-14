using NUnit.Framework;
using TunnelCrew.Presentation;
using TunnelCrew.Presentation.CRT;
using TunnelCrew.Sim;
using UnityEngine;

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

        [Test]
        public void DynamicDialogueMarkupCompilesWithoutLeakingControlTags()
        {
            var script = DynamicDialogueText.Compile("신호 [wait=.2][tint=danger][jitter=.7]위험[/jitter][/tint] 끝");

            Assert.AreEqual("신호 위험 끝", script.PlainText);
            Assert.AreEqual(script.PlainText.Length, script.Glyphs.Length);
            Assert.That(script.Duration, Is.GreaterThan(.3f));
            int danger = script.PlainText.IndexOf('위');
            Assert.AreEqual(DynamicDialogueText.Tint.Danger, script.Glyphs[danger].Tint);
            Assert.That(script.Glyphs[danger].Motion.HasFlag(DynamicDialogueText.Motion.Jitter), Is.True);
        }

        [Test]
        public void DynamicDialogueKeepsFullLayoutWhileVisibilityAdvances()
        {
            var script = DynamicDialogueText.Compile("ABC", false);
            Assert.AreEqual(1, script.VisibleCharacters(0));
            Assert.That(script.VisibleCharacters(script.Duration + .01f), Is.EqualTo(3));
            Assert.AreEqual("ABC", script.PlainText);
        }

        [Test]
        public void FtueComicAndEquipmentArtAreProductionResolution()
        {
            foreach (string path in new[] { "FTUE/Art/sequence_crash", "FTUE/Art/sequence_record", "FTUE/Art/sequence_awake", "FTUE/Art/sequence_escape", "FTUE/Art/equipment_locker" })
            {
                var texture = Resources.Load<Texture2D>(path);
                Assert.That(texture, Is.Not.Null, path);
                Assert.That(texture.width, Is.GreaterThanOrEqualTo(1600), path);
                Assert.That(texture.height, Is.GreaterThanOrEqualTo(900), path);
            }
        }
    }
}
