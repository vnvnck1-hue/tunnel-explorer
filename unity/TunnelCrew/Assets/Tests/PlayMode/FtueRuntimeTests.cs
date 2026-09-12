using System.Collections;
using NUnit.Framework;
using TunnelCrew.Presentation;
using TunnelCrew.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TunnelCrew.Tests
{
    public sealed class FtueRuntimeTests
    {
        [UnitySetUp]
        public IEnumerator Setup()
        {
            Assert.That(Application.productName, Does.Contain("CRT Validation"), "Run only in isolated QA project to preserve player saves");
            yield return SceneManager.LoadSceneAsync("Assets/_Project/Scenes/Run.unity");
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            Time.timeScale = 1;
            yield return null;
        }

        [UnityTest]
        public IEnumerator LaunchBeginsComicAndKeepsSimulationFrozen()
        {
            var run = Object.FindFirstObjectByType<RunBootstrap>();
            Assert.That(run, Is.Not.Null);
            run.LaunchFtue();
            yield return null;

            var ftue = Object.FindFirstObjectByType<FtueDirector>();
            Assert.That(ftue, Is.Not.Null);
            Assert.That(ftue.Active, Is.True);
            Assert.That(ftue.CurrentStage, Is.EqualTo(FtueProgressModel.Stage.CrashComic));
            Assert.That(ftue.BlocksSimulation, Is.True);
            Assert.That(AudioDirector.Instance.Current, Is.EqualTo(AudioDirector.Route.Tunnel));

            double before = run.Sim.RunTime;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(run.Sim.RunTime, Is.EqualTo(before).Within(.0001));
        }

        [UnityTest]
        public IEnumerator GunnerGetsAuthoredRouteAndBreakerCompatibleGates()
        {
            var run = Object.FindFirstObjectByType<RunBootstrap>();
            var layout = run.PrepareFtueRole(RoleId.Gunner);
            yield return null;

            Assert.That(run.Sim.Build.Role, Is.EqualTo(RoleId.Gunner));
            Assert.That(run.Sim.Build.RoleDigMul, Is.Zero, "Gunner must keep the real breaker kit, not a tutorial-only drill");
            Assert.That(run.Sim.Enemies.Enemies.Count, Is.Zero);
            Assert.That(run.Sim.Enemies.IncomingDamageMul(), Is.EqualTo(.45).Within(.001));
            var first = WorldGrid.ToCell(layout.FirstWall);
            var rescue = WorldGrid.ToCell(layout.RescueWall);
            Assert.That(run.Sim.World.At(first.c, first.r), Is.EqualTo(TileType.Ore));
            Assert.That(run.Sim.World.At(rescue.c, rescue.r), Is.EqualTo(TileType.Dirt));
        }

        [Test]
        public void AllFourComicPagesLoadAtProductionResolution()
        {
            foreach (string path in new[] { "FTUE/Art/sequence_crash", "FTUE/Art/sequence_record", "FTUE/Art/sequence_awake", "FTUE/Art/sequence_escape" })
            {
                var texture = Resources.Load<Texture2D>(path);
                Assert.That(texture, Is.Not.Null, path);
                Assert.That(texture.width, Is.GreaterThanOrEqualTo(1600), path);
                Assert.That(texture.height, Is.GreaterThanOrEqualTo(900), path);
            }
        }
    }
}
