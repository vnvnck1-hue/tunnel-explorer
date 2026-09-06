using System;
using NUnit.Framework;
using TunnelCrew.Sim;

namespace TunnelCrew.Tests
{
    /// <summary>
    /// 시뮬레이션을 헤드리스로 돌려 M1 코어 루프가 실제로 성립하는지 본다.
    /// 사람이 화면을 보지 않고도 확인할 수 있는 것들만 검사한다.
    /// </summary>
    public class SimSmokeTests
    {
        static TunnelSim NewSim(int depth = 1)
        {
            var sim = new TunnelSim();
            sim.EnterDepth(depth, DungeonConfig.Runtime);
            return sim;
        }

        static PlayerInput Idle(TunnelSim sim) => new PlayerInput
        {
            Move = Vec2.Zero,
            AimWorld = sim.Player.Position + new Vec2(1, 0),
        };

        static void Run(TunnelSim sim, PlayerInput input, double seconds)
        {
            int ticks = (int)(seconds / SimTuning.FixedDeltaTime);
            for (int i = 0; i < ticks; i++) sim.Tick(SimTuning.FixedDeltaTime, input);
        }

        [Test]
        public void PlayerSpawnsInsideOpenSpace()
        {
            var sim = NewSim();
            var (c, r) = WorldGrid.ToCell(sim.Player.Position);
            Assert.IsFalse(sim.World.IsSolid(c, r), "진입점이 벽 속이면 안 된다");
            Assert.AreEqual(sim.World.EntryCol, c, "진입 열");
            Assert.AreEqual(sim.World.EntryRow, r, "진입 행");
        }

        [Test]
        public void Idle_DoesNotDrift()
        {
            var sim = NewSim();
            var start = sim.Player.Position;
            Run(sim, Idle(sim), 2.0);
            Assert.Less(Vec2.Distance(start, sim.Player.Position), 1e-6, "입력이 없으면 움직이지 않아야 한다");
        }

        [Test]
        public void Movement_ReachesExpectedSpeed()
        {
            var sim = NewSim();
            // 진입점 주변은 ensurePath 로 반경 2.1칸이 열려 있다. 한 방향으로 짧게 이동한다.
            var input = Idle(sim);
            input.Move = new Vec2(1, 0);

            var start = sim.Player.Position;
            Run(sim, input, 0.25);
            double moved = sim.Player.Position.X - start.X;

            // 벽에 막히지 않았다면 속도 × 시간에 근접해야 한다
            double expected = SimTuning.MoveSpeed * 0.25;
            Assert.Greater(moved, 0.0, "오른쪽으로 이동해야 한다");
            Assert.LessOrEqual(moved, expected + 1e-6, "이동 속도가 상한을 넘으면 안 된다");
        }

        [Test]
        public void Player_NeverEndsInsideWall()
        {
            var sim = NewSim();
            var input = Idle(sim);
            var rng = new Random(7);

            // 무작위 방향으로 20초를 돌아다녀도 벽 속에 갇히면 안 된다
            for (int i = 0; i < 20 * 60; i++)
            {
                if (i % 30 == 0)
                {
                    double a = rng.NextDouble() * Math.PI * 2;
                    input.Move = Vec2.FromAngle(a);
                    input.DashPressed = rng.NextDouble() < 0.05;
                }
                else input.DashPressed = false;

                input.AimWorld = sim.Player.Position + input.Move;
                sim.Tick(SimTuning.FixedDeltaTime, input);

                var (c, r) = WorldGrid.ToCell(sim.Player.Position);
                Assert.IsFalse(sim.World.IsSolid(c, r),
                    $"tick {i}: 플레이어가 벽 속에 들어갔다 (셀 {c},{r})");
            }
        }

        [Test]
        public void Drilling_BreaksBlocksAndDropsLoot()
        {
            var sim = NewSim();

            // 진입점에서 가장 가까운 파괴 가능한 벽을 찾아 그쪽을 조준한다
            Vec2 target = default;
            bool found = false;
            double best = double.MaxValue;
            for (int r = 1; r < sim.World.Rows - 1 && !found; r++)
                for (int c = 1; c < sim.World.Cols - 1; c++)
                {
                    var t = sim.World.At(c, r);
                    if (t == TileType.Empty || TileTypes.IsBedrock(t)) continue;
                    var mid = WorldGrid.CellCenter(c, r);
                    double d = Vec2.Distance(mid, sim.Player.Position);
                    if (d < best) { best = d; target = mid; found = true; }
                }
            Assert.IsTrue(found, "파괴 가능한 벽이 있어야 한다");

            var input = Idle(sim);
            input.AimWorld = target;
            input.Move = (target - sim.Player.Position).Normalized;   // 벽 쪽으로 붙는다
            input.DrillHeld = true;

            Run(sim, input, 6.0);

            Assert.Greater(sim.World.BlocksBroken, 0, "6초 드릴이면 블록이 부서져야 한다");
            Assert.Greater(sim.Loot.Pulp + sim.Loot.Bloom, 0, "부순 블록에서 재화가 나와 자석으로 들어와야 한다");
        }

        [Test]
        public void DrillHeat_LocksAndRecovers()
        {
            var sim = NewSim();
            var p = sim.Player;
            var input = Idle(sim);
            input.DrillHeld = true;

            // 발열 0.13/s → 1.0 도달까지 약 7.7초
            Run(sim, input, 9.0);
            Assert.Greater(p.DrillHeatLock, 0, "계속 드릴하면 과열 잠금이 걸려야 한다");
            Assert.IsFalse(p.CanDrill, "잠금 중에는 드릴을 쓸 수 없다");

            input.DrillHeld = false;
            Run(sim, input, 3.0);
            Assert.AreEqual(0, p.DrillHeatLock, 1e-9, "잠금은 2.43초 뒤 풀린다");
            Assert.IsTrue(p.CanDrill, "잠금이 풀리면 다시 쓸 수 있다");
        }

        [Test]
        public void Dash_MovesFartherThanWalking()
        {
            var sim = NewSim();
            var input = Idle(sim);
            input.Move = new Vec2(1, 0);
            input.AimWorld = sim.Player.Position + new Vec2(1, 0);

            var start = sim.Player.Position;
            input.DashPressed = true;
            sim.Tick(SimTuning.FixedDeltaTime, input);
            input.DashPressed = false;
            Run(sim, input, SimTuning.DashDuration);

            double moved = Vec2.Distance(start, sim.Player.Position);
            double walkOnly = SimTuning.MoveSpeed * (SimTuning.DashDuration + SimTuning.FixedDeltaTime);
            Assert.Greater(moved, walkOnly, "대시는 걷기보다 멀리 가야 한다");
            Assert.Greater(sim.Player.DashCooldown, 0, "대시 후 쿨다운이 걸려야 한다");
        }

        [Test]
        public void FixedTimestep_IsFrameRateIndependent()
        {
            // 같은 입력이면 프레임 분할이 달라도 결과가 같아야 한다 (고정 틱의 존재 이유)
            var a = NewSim();
            var b = NewSim();
            var input = Idle(a);
            input.Move = new Vec2(1, 0);

            for (int i = 0; i < 60; i++) a.Advance(1.0 / 60.0, input);   // 60fps
            for (int i = 0; i < 30; i++) b.Advance(2.0 / 60.0, input);   // 30fps

            Assert.AreEqual(a.Player.Position.X, b.Player.Position.X, 1e-9, "프레임률과 무관해야 한다");
            Assert.AreEqual(a.Player.Position.Y, b.Player.Position.Y, 1e-9);
        }

        [Test]
        public void LongRun_DoesNotThrow()
        {
            var sim = NewSim();
            var input = Idle(sim);
            var rng = new Random(99);

            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 60 * 60; i++)   // 60초
                {
                    if (i % 20 == 0)
                    {
                        input.Move = Vec2.FromAngle(rng.NextDouble() * Math.PI * 2);
                        input.DrillHeld = rng.NextDouble() < 0.6;
                    }
                    input.AimWorld = sim.Player.Position + input.Move;
                    sim.Tick(SimTuning.FixedDeltaTime, input);
                }
            });

            Assert.LessOrEqual(sim.Loot.Items.Count, SimTuning.ResourceMax, "전리품 상한을 지켜야 한다");
        }
    }
}
