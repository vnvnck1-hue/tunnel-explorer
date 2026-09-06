using System;
using System.Linq;
using NUnit.Framework;
using TunnelCrew.Sim;

namespace TunnelCrew.Tests
{
    /// <summary>자동 굴착 서브시스템 특성 8종 — 보조 드릴·잔상·소용돌이·행성 파쇄기·대붕괴·군단·폭풍·위성.</summary>
    public class TraitSubsystemTests
    {
        static TunnelSim NewSim()
        {
            var sim = new TunnelSim();
            sim.StartRun(RoleId.Driller);
            sim.EnterDepth(1, DungeonConfig.Runtime);
            sim.Enemies.SpawnInterval = 9999;
            return sim;
        }
        static TraitContext Ctx(TunnelSim sim) => new TraitContext { Build = sim.Build, Player = sim.Player, Roles = sim.Roles };
        static TraitDef Card(string id) => TraitDeck.All.First(t => t.Id == id);
        static PlayerInput Idle(TunnelSim sim) => new PlayerInput { AimWorld = sim.Player.Position + new Vec2(1, 0) };
        static void Run(TunnelSim sim, double seconds, PlayerInput? input = null)
        {
            int n = (int)Math.Ceiling(seconds / SimTuning.FixedDeltaTime);
            for (int i = 0; i < n; i++) sim.Tick(SimTuning.FixedDeltaTime, input ?? Idle(sim));
        }
        /// <summary>플레이어를 파괴 가능한 벽 옆으로 옮기고 그 벽 좌표를 돌려준다.</summary>
        static (int c, int r) StandByWall(TunnelSim sim)
        {
            var pp = sim.Player.Position; double best = 1e9; int bc = -1, br = -1, ec = -1, er = -1;
            for (int r = 2; r < sim.World.Rows - 2; r++) for (int c = 2; c < sim.World.Cols - 2; c++)
            {
                var t = sim.World.At(c, r);
                if (t != TileType.Dirt && t != TileType.Stone) continue;
                foreach (var (dc, dr) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    if (sim.World.IsSolid(c + dc, r + dr)) continue;
                    double d = Vec2.Distance(WorldGrid.CellCenter(c + dc, r + dr), pp);
                    if (d < best) { best = d; bc = c; br = r; ec = c + dc; er = r + dr; }
                }
            }
            sim.Player.Position = WorldGrid.CellCenter(ec, er); sim.Player.Velocity = Vec2.Zero;
            return (bc, br);
        }

        [Test]
        public void AllEightCards_AreNowInThePool()
        {
            foreach (var id in new[] { "u_aux", "u_afterimage", "u_satellite", "u_vortex", "u_army", "u_planet_breaker", "u_grand_collapse", "u_storm" })
                Assert.IsFalse(Card(id).NotPorted, id);
            Assert.AreEqual(0, TraitDeck.All.Count(t => t.NotPorted));
        }

        [Test]
        public void AuxDrills_DamageNearbyWall_WithoutInput()
        {
            var sim = NewSim();
            Card("u_aux").Apply(Ctx(sim));
            Assert.AreEqual(1, sim.AuxDrillCount);
            var (c, r) = StandByWall(sim);
            int k = sim.World.Index(c, r);
            double hp0 = sim.World.HpAt(k);
            int hits = 0; sim.TraitFx += e => { if (e.Kind == "auxHit") hits++; };
            Run(sim, 1.0);
            Assert.Less(sim.World.HpAt(k), hp0, "입력 없이도 옆 벽이 깎인다");
            Assert.Greater(hits, 0);
        }

        [Test]
        public void Army_And_Storm_RaiseCount_AndSlowMove()
        {
            var sim = NewSim();
            Card("u_army").Apply(Ctx(sim));
            Assert.AreEqual(6, sim.AuxDrillCount);
            Assert.AreEqual(.9, sim.Build.MoveMul, 1e-9);
            Card("u_storm").Apply(Ctx(sim));
            Assert.AreEqual(6, sim.AuxDrillCount, "max(aux, storm)");
            Assert.AreEqual(4, sim.Build.DrillStorm);
            Assert.AreEqual(.9 * .88, sim.Build.MoveMul, 1e-9);
        }

        [Test]
        public void Afterimage_BlastsSameSpot_After032s()
        {
            var sim = NewSim();
            Card("u_afterimage").Apply(Ctx(sim));
            var (c, r) = StandByWall(sim);
            int blasts = 0; sim.TraitFx += e => { if (e.Kind == "afterBlast") blasts++; };
            sim.World.ForceClear(c, r);
            Run(sim, 0.2);
            Assert.AreEqual(0, blasts, "0.32초 전에는 터지지 않는다");
            Run(sim, 0.2);
            Assert.AreEqual(1, blasts);
        }

        [Test]
        public void Vortex_PullsLoot_EveryThirdBlock()
        {
            var sim = NewSim();
            Card("u_vortex").Apply(Ctx(sim));
            var (c, r) = StandByWall(sim);
            var at = WorldGrid.CellCenter(c, r);
            sim.Loot.SpawnBurst(at + new Vec2(3, 0), ResourceKind.Pulp, 1, 1, new Vec2(1, 0));
            var item = sim.Loot.Items.Last();
            item.Velocity = Vec2.Zero;
            int vortex = 0; sim.TraitFx += e => { if (e.Kind == "vortex") vortex++; };
            // 3번째 파괴에서 발동
            sim.World.ForceClear(c, r);
            Assert.AreEqual(0, vortex);
            int broken = 1;
            for (int rr = 2; rr < sim.World.Rows - 2 && broken < 3; rr++) for (int cc = 2; cc < sim.World.Cols - 2 && broken < 3; cc++)
            {
                var t = sim.World.At(cc, rr);
                if (t == TileType.Empty || TileTypes.IsBedrock(t)) continue;
                if (Vec2.Distance(WorldGrid.CellCenter(cc, rr), at) > 12) { sim.World.ForceClear(cc, rr); broken++; }
            }
            Assert.AreEqual(1, vortex, "3번째 파괴에서 발동 (위치는 그 블록)");
            // 끌림은 at 기준으로 직접 검증
            sim.Loot.Pull(at, 5.0, 1.5);
            Assert.Greater(item.Velocity.Length, 1.0, "5칸 안 전리품이 중심으로 끌린다");
            Assert.Less(item.Velocity.X, 0);
        }

        [Test]
        public void PlanetBreaker_SweepsBand_AndCostsHp()
        {
            var sim = NewSim();
            Card("u_planet_breaker").Apply(Ctx(sim));
            Assert.AreEqual(15, sim.Build.PlanetBreakerEvery);
            var (c, r) = StandByWall(sim);
            double hp0 = sim.Player.Hp;
            int fired = 0; sim.TraitFx += e => { if (e.Kind == "planetBreaker") fired++; };
            // 14번 다른 곳을 부수고 15번째를 벽 옆에서
            int broken = 0;
            for (int rr = 2; rr < sim.World.Rows - 2 && broken < 14; rr++) for (int cc = 2; cc < sim.World.Cols - 2 && broken < 14; cc++)
            {
                var t = sim.World.At(cc, rr);
                if (t == TileType.Empty || TileTypes.IsBedrock(t) || (cc == c && rr == r)) continue;
                if (Vec2.Distance(WorldGrid.CellCenter(cc, rr), sim.Player.Position) > 14) { sim.World.ForceClear(cc, rr); broken++; }
            }
            Assert.AreEqual(0, fired);
            sim.World.ForceClear(c, r);
            Assert.AreEqual(1, fired, "15번째 파괴에서 발동");
            Assert.AreEqual(hp0 - Math.Max(1, sim.Player.HpMax * .015), sim.Player.Hp, 1e-9, "최대 HP 1.5% 반동");
            // 띠(10×5) 안의 벽은 최대 체력의 37.5% 만큼 깎인다 — 손상된 칸이 있어야 한다
            int damaged = 0;
            for (int rr = r - 6; rr <= r + 6; rr++) for (int cc = c - 6; cc <= c + 6; cc++)
                if (sim.World.InBounds(cc, rr) && sim.World.IsSolid(cc, rr) && !sim.World.IsBedrock(cc, rr) && sim.World.HpAt(sim.World.Index(cc, rr)) < sim.World.MaxHp(sim.World.At(cc, rr)) - 1e-9) damaged++;
            Assert.Greater(damaged, 3, "띠 안의 벽이 함께 깎인다");
        }

        [Test]
        public void GrandCollapse_CostsHp_NotLethal()
        {
            var sim = NewSim();
            Card("u_grand_collapse").Apply(Ctx(sim));
            var (c, r) = StandByWall(sim);
            sim.Player.Hp = 2;
            int broken = 0;
            for (int rr = 2; rr < sim.World.Rows - 2 && broken < 11; rr++) for (int cc = 2; cc < sim.World.Cols - 2 && broken < 11; cc++)
            {
                var t = sim.World.At(cc, rr);
                if (t == TileType.Empty || TileTypes.IsBedrock(t) || (cc == c && rr == r)) continue;
                if (Vec2.Distance(WorldGrid.CellCenter(cc, rr), sim.Player.Position) > 14) { sim.World.ForceClear(cc, rr); broken++; }
            }
            int fired = 0; sim.TraitFx += e => { if (e.Kind == "grandCollapse") fired++; };
            sim.World.ForceClear(c, r);
            Assert.AreEqual(1, fired);
            Assert.AreEqual(1, sim.Player.Hp, "치명적이지 않은 대가는 HP 1 을 남긴다");
            Assert.IsFalse(sim.Player.Downed);
        }
    }
}
