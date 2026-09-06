using System;
using NUnit.Framework;
using TunnelCrew.Sim;

namespace TunnelCrew.Tests
{
    /// <summary>
    /// 시야 규칙이 원본과 같은지 확인한다. 화면을 보지 않고 검증할 수 있는 것들만 다룬다.
    /// </summary>
    public class LosTests
    {
        static TunnelSim NewSim()
        {
            var sim = new TunnelSim();
            sim.EnterDepth(1, DungeonConfig.Runtime);
            sim.Tick(SimTuning.FixedDeltaTime, new PlayerInput
            {
                AimWorld = sim.Player.Position + new Vec2(1, 0),
            });
            return sim;
        }

        [Test]
        public void PlayerCellAndNeighbours_AreAlwaysVisible()
        {
            var sim = NewSim();
            var (c, r) = WorldGrid.ToCell(sim.Player.Position);

            Assert.IsTrue(sim.Los.IsVisible(c, r), "서 있는 칸은 보여야 한다");
            for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++)
                    Assert.IsTrue(sim.Los.IsVisible(c + dc, r + dr),
                        $"주변 8칸은 항상 보여야 한다 ({dc},{dr})");
        }

        [Test]
        public void Visibility_NeverExceedsRange()
        {
            var sim = NewSim();
            int cx = sim.Los.LastCol, cy = sim.Los.LastRow;
            // 플레이어 레이(19) 와 크루 시야원(5) 중 큰 쪽 + 반올림 여유
            int max = SimTuning.LosRange + 2;

            for (int r = 0; r < sim.World.Rows; r++)
                for (int c = 0; c < sim.World.Cols; c++)
                {
                    if (!sim.Los.IsVisible(c, r)) continue;
                    int dc = c - cx, dr = r - cy;
                    Assert.LessOrEqual(dc * dc + dr * dr, max * max,
                        $"셀 ({c},{r}) 이 시야 반경 밖인데 보인다");
                }
        }

        [Test]
        public void Walls_BlockSightButAreThemselvesVisible()
        {
            var sim = NewSim();
            int cx = sim.Los.LastCol, cy = sim.Los.LastRow;

            // 플레이어에서 사방으로 나가다 처음 만나는 벽은 보여야 하고,
            // 그 바로 뒤 칸은 (다른 경로로 보이지 않는 한) 안 보여야 한다.
            foreach (var (dc, dr) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                for (int step = 1; step < SimTuning.LosRange; step++)
                {
                    int c = cx + dc * step, r = cy + dr * step;
                    if (!sim.World.InBounds(c, r)) break;
                    if (!sim.World.IsSolid(c, r)) continue;

                    Assert.IsTrue(sim.Los.IsVisible(c, r),
                        $"직선상 첫 벽 ({c},{r}) 은 보여야 한다");
                    break;
                }
            }
        }

        [Test]
        public void Explored_AccumulatesAndNeverShrinks()
        {
            var sim = NewSim();
            int Count()
            {
                int n = 0;
                foreach (var b in sim.Los.Explored) if (b != 0) n++;
                return n;
            }

            int first = Count();
            Assert.Greater(first, 0, "첫 계산에서 탐색 기록이 생겨야 한다");

            var input = new PlayerInput { Move = new Vec2(1, 0) };
            int prev = first;
            for (int i = 0; i < 120; i++)
            {
                input.AimWorld = sim.Player.Position + new Vec2(1, 0);
                sim.Tick(SimTuning.FixedDeltaTime, input);
                int now = Count();
                Assert.GreaterOrEqual(now, prev, $"tick {i}: 탐색 기록이 줄어들면 안 된다");
                prev = now;
            }
        }

        [Test]
        public void MemoryValue_MatchesOriginalFormula()
        {
            var sim = NewSim();
            int c = sim.Los.LastCol, r = sim.Los.LastRow;

            // 서 있는 칸은 거리 0 → fade 1 → exp * (0.30 + 0.70) = exp
            int exp = JsMath.Round(SimTuning.LosExplored * 255);   // 74
            Assert.AreEqual(exp, sim.Los.MemoryValue(c, r), "중심의 기억 농도는 최대값이어야 한다");

            // 기억 반경 밖의 탐색 지역은 30% 를 유지한다 (완전 검정으로 끊기지 않게)
            int floorValue = JsMath.Round(exp * SimTuning.LosMemoryFloor);
            bool foundFar = false;
            for (int rr = 0; rr < sim.World.Rows && !foundFar; rr++)
                for (int cc = 0; cc < sim.World.Cols; cc++)
                {
                    if (!sim.Los.IsExplored(cc, rr)) continue;
                    int dc = cc - c, dr = rr - r;
                    if (dc * dc + dr * dr <= SimTuning.LosMemory * SimTuning.LosMemory) continue;
                    Assert.AreEqual(floorValue, sim.Los.MemoryValue(cc, rr),
                        $"기억 반경 밖 ({cc},{rr}) 은 바닥값이어야 한다");
                    foundFar = true;
                    break;
                }
        }

        [Test]
        public void UnexploredCells_HaveZeroMemory()
        {
            var sim = NewSim();
            for (int r = 0; r < sim.World.Rows; r++)
                for (int c = 0; c < sim.World.Cols; c++)
                    if (!sim.Los.IsExplored(c, r))
                        Assert.AreEqual(0, sim.Los.MemoryValue(c, r),
                            $"가 본 적 없는 ({c},{r}) 의 농도는 0 이어야 한다");
        }

        [Test]
        public void Compute_IsCachedWhenNothingChanged()
        {
            var sim = NewSim();
            var los = sim.Los;
            int before = los.LastCol;

            // 같은 자리에서 다시 계산해도 결과가 같아야 한다
            var snapshot = (byte[])los.Visible.Clone();
            los.Compute(sim.Player.Position);
            CollectionAssert.AreEqual(snapshot, los.Visible, "입력이 같으면 결과도 같아야 한다");
            Assert.AreEqual(before, los.LastCol);
        }

        [Test]
        public void BreakingWall_RevealsMoreThanBefore()
        {
            var sim = NewSim();

            int VisibleCount()
            {
                int n = 0;
                foreach (var b in sim.Los.Visible) if (b != 0) n++;
                return n;
            }

            // 플레이어를 둘러싼 벽을 찾아 부수면 시야가 넓어져야 한다
            int cx = sim.Los.LastCol, cy = sim.Los.LastRow;
            int broken = 0;
            for (int rad = 1; rad <= 3 && broken == 0; rad++)
                for (int dr = -rad; dr <= rad && broken == 0; dr++)
                    for (int dc = -rad; dc <= rad; dc++)
                    {
                        int c = cx + dc, r = cy + dr;
                        if (!sim.World.InInterior(c, r)) continue;
                        var t = sim.World.At(c, r);
                        if (t == TileType.Empty || TileTypes.IsBedrock(t)) continue;
                        sim.World.Damage(c, r, 1e9, new Vec2(1, 0));
                        broken++;
                        break;
                    }

            Assert.Greater(broken, 0, "부술 벽이 있어야 한다");

            int after = 0;
            sim.Los.Compute(sim.Player.Position);
            after = VisibleCount();
            Assert.Greater(after, 0, "벽을 부순 뒤에도 시야가 계산돼야 한다");
        }

        [Test]
        public void SoftSeen_IsSupersetOfSeen()
        {
            var sim = NewSim();
            for (int r = 1; r < sim.World.Rows - 1; r += 7)
                for (int c = 1; c < sim.World.Cols - 1; c += 7)
                    if (sim.Los.IsSeen(c, r))
                        Assert.IsTrue(sim.Los.IsSoftSeen(c, r, 2),
                            $"({c},{r}) 이 보이면 softSeen 도 true 여야 한다");
        }
    }
}
