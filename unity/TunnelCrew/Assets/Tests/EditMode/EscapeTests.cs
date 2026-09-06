using System;
using NUnit.Framework;
using TunnelCrew.Sim;

namespace TunnelCrew.Tests
{
    /// <summary>탈출 포트와 런 흐름(휴식 → 하강 / 탈출 → 결과). 원본 INF_ESCAPE 수치와 일치해야 한다.</summary>
    public class EscapeTests
    {
        static TunnelSim NewSim(int depth = 1)
        {
            var sim = new TunnelSim();
            sim.StartRun(RoleId.Scout);
            sim.EnterDepth(depth, DungeonConfig.Runtime);
            sim.Enemies.SpawnInterval = 9999;
            return sim;
        }

        static PlayerInput Idle(TunnelSim sim) => new PlayerInput { AimWorld = sim.Player.Position + new Vec2(1, 0) };

        static void Run(TunnelSim sim, double seconds, Func<PlayerInput> input = null)
        {
            int n = (int)Math.Ceiling(seconds / SimTuning.FixedDeltaTime);
            for (int i = 0; i < n; i++) sim.Tick(SimTuning.FixedDeltaTime, input != null ? input() : Idle(sim));
        }

        [Test]
        public void SummonNeed_ByDepth()
        {
            Assert.AreEqual(20, EscapeSystem.SummonNeedFor(1));
            Assert.AreEqual(27, EscapeSystem.SummonNeedFor(2));
            Assert.AreEqual(60, EscapeSystem.SummonNeedFor(10), "상한 60초");
        }

        [Test]
        public void Placement_ClampsToRange_AndConfirms()
        {
            var sim = NewSim();
            var pp = sim.Player.Position;
            var press = Idle(sim); press.EscapePressed = true;
            sim.Tick(SimTuning.FixedDeltaTime, press);
            Assert.AreEqual(EscapePhase.Placing, sim.Escape.Phase);

            // 너무 먼 지점은 반경 6칸으로 당겨진다
            var far = new PlayerInput { AimWorld = pp + new Vec2(30, 0), PrimaryPressed = true, DrillHeld = true };
            int broken0 = sim.World.BlocksBroken;
            sim.Tick(SimTuning.FixedDeltaTime, far);
            Assert.AreEqual(EscapePhase.Incoming, sim.Escape.Phase);
            Assert.LessOrEqual(Vec2.Distance(sim.Escape.Position, pp), EscapeSystem.PlaceRange + 1e-6);
            Assert.AreEqual(broken0, sim.World.BlocksBroken, "확정 클릭은 드릴로 가지 않는다");
            Assert.AreEqual(20, sim.Escape.Need);
        }

        [Test]
        public void Toggle_CancelsPlacement_AndIgnoresWhenRequested()
        {
            var sim = NewSim();
            Assert.IsTrue(sim.Escape.TogglePlacement(sim.Player, 1));
            Assert.IsTrue(sim.Escape.TogglePlacement(sim.Player, 1));   // 취소
            Assert.AreEqual(EscapePhase.None, sim.Escape.Phase);
            sim.Escape.AutoSummon(sim.Player, sim.Player.Position, 1);
            Assert.IsFalse(sim.Escape.TogglePlacement(sim.Player, 1), "이미 요청된 포트가 있으면 무시");
        }

        [Test]
        public void Arrival_ClearsLandingTerrain_ThenBoardingEndsRun()
        {
            var sim = NewSim();
            // 벽 속 지점에 포트를 요청해 착륙 파괴를 확인
            Vec2? wallSpot = null;
            var pp = sim.Player.Position;
            for (int r = 2; r < sim.World.Rows - 2 && wallSpot == null; r++)
                for (int c = 2; c < sim.World.Cols - 2; c++)
                {
                    var q = WorldGrid.CellCenter(c, r);
                    if (sim.World.At(c, r) == TileType.Dirt && Vec2.Distance(q, pp) <= 5.5 && Vec2.Distance(q, pp) >= 2.5) { wallSpot = q; break; }
                }
            if (wallSpot == null) Assert.Ignore("근처에 흙벽이 없다");
            sim.Escape.AutoSummon(sim.Player, wallSpot.Value, 1);

            bool ended = false, escaped = false;
            sim.RunEnded += (esc, reason) => { ended = true; escaped = esc; };

            Run(sim, 20.1);
            Assert.AreEqual(EscapePhase.Ready, sim.Escape.Phase);
            var (c0, r0) = WorldGrid.ToCell(sim.Escape.Position);
            Assert.AreEqual(TileType.Empty, sim.World.At(c0, r0), "착륙 지점이 뚫린다");
            Assert.AreEqual(TileType.Empty, sim.World.At(c0 + 1, r0));

            // 탑승: 포트 위에 서서 1.2초
            sim.Player.Position = sim.Escape.Position;
            sim.Player.Velocity = Vec2.Zero;
            Run(sim, 0.6, () => new PlayerInput { AimWorld = sim.Player.Position + new Vec2(1, 0) });
            Assert.IsFalse(ended, "1.2초 채널링 전에는 끝나지 않는다");
            Run(sim, 0.8, () => new PlayerInput { AimWorld = sim.Player.Position + new Vec2(1, 0) });
            Assert.IsTrue(ended);
            Assert.IsTrue(escaped);
            Assert.AreEqual(GamePhase.Result, sim.Phase);
            Assert.IsTrue(sim.RunEscaped);
        }

        [Test]
        public void Boarding_DecaysWhenLeaving()
        {
            var sim = NewSim();
            sim.Escape.AutoSummon(sim.Player, sim.Player.Position, 1);
            Run(sim, 20.1);
            sim.Player.Position = sim.Escape.Position;
            Run(sim, 0.5);
            Assert.Greater(sim.Escape.Board, 0.4);
            sim.Player.Position = sim.Escape.Position + new Vec2(5, 0);
            Run(sim, 0.5);
            Assert.AreEqual(0, sim.Escape.Board, 1e-6, "떠나면 2배속으로 줄어 0");
        }

        [Test]
        public void GuardianDefeat_RestThenDescend_KeepsRunGrowth()
        {
            var sim = NewSim(depth: 1);
            var b = sim.Bosses.Spawn(sim.Player, 1);
            sim.Xp.Award(5, XpKind.Dig, RoleId.Scout);
            sim.Enemies.HurtEnemy(b.Body, 1e12, new Vec2(1, 0), sim.Player.Position);
            sim.Tick(SimTuning.FixedDeltaTime, Idle(sim));
            int xp0 = sim.Xp.Xp, level0 = sim.Xp.Level;
            Assert.AreEqual(2, level0, "보스 격파 XP 45×0.85 + 4 = 42 → 레벨 2");
            Assert.IsTrue(sim.RestPending);
            Assert.AreEqual(EscapePhase.None, sim.Escape.Phase, "수호자는 자동 탈출 요청이 없다");

            sim.EnterRest();
            Assert.AreEqual(GamePhase.Rest, sim.Phase);
            var pos = sim.Player.Position;
            sim.Advance(1.0, Idle(sim));
            Assert.AreEqual(pos, sim.Player.Position, "휴식 중에는 월드가 멈춘다");

            // 보스전 중 쌓인 레벨업 카드 → 전설 카드 순으로 고른다
            for (int i = 0; i < 3 && !sim.RestChosen; i++) sim.PickTrait(0);
            Assert.IsTrue(sim.RestChosen);
            sim.Descend();
            Assert.AreEqual(2, sim.Depth);
            Assert.AreEqual(GamePhase.Playing, sim.Phase);
            Assert.AreEqual(1.35, sim.Run.WallHpMul);
            Assert.AreEqual(level0, sim.Xp.Level, "런 성장은 층을 넘어 유지된다");
            Assert.AreEqual(1, sim.Run.BossesKilled);
            Assert.IsFalse(sim.Run.BossSpawned);
        }

        [Test]
        public void ApexDefeat_AutoSummonsEscape()
        {
            var sim = NewSim(depth: 3);
            var b = sim.Bosses.Spawn(sim.Player, 3);
            sim.Enemies.HurtEnemy(b.Body, 1e12, new Vec2(1, 0), sim.Player.Position);
            sim.Tick(SimTuning.FixedDeltaTime, Idle(sim));
            Assert.AreEqual(EscapePhase.Incoming, sim.Escape.Phase);
            Assert.AreEqual(20 + 7 * 2, sim.Escape.Need);
            Assert.AreEqual(BossTier.Apex, sim.LastBossTier);
        }

        [Test]
        public void PlayerDowned_EndsRun()
        {
            var sim = NewSim();
            bool ended = false;
            sim.RunEnded += (esc, reason) => ended = !esc;
            sim.Player.IFrames = 0;
            sim.Enemies.ApplyPlayerDamage(sim.Player, 1e6, new Vec2(1, 0));
            sim.Tick(SimTuning.FixedDeltaTime, Idle(sim));
            Assert.IsTrue(ended);
            Assert.AreEqual(GamePhase.Result, sim.Phase);
        }
    }
}
