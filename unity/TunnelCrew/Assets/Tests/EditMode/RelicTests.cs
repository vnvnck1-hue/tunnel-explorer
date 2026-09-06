using System;
using System.Linq;
using NUnit.Framework;
using TunnelCrew.Sim;

namespace TunnelCrew.Tests
{
    /// <summary>유물 효과 — 원본 infRelic* 수치.</summary>
    public class RelicTests
    {
        static (TunnelSim sim, MetaState meta) NewSim(params string[] equipped)
        {
            var meta = new MetaState();
            foreach (var id in equipped) { Relics.Grant(meta, id); Relics.EquipAuto(meta, id); }
            var sim = new TunnelSim();
            sim.StartRun(RoleId.Gunner, meta);
            sim.EnterDepth(1, DungeonConfig.Runtime);
            sim.Enemies.SpawnInterval = 9999;
            return (sim, meta);
        }
        static EnemyState Spawn(TunnelSim sim, Vec2 offset)
        {
            var e = sim.Enemies.Spawn(sim.Player.Position);
            e.Position = sim.Player.Position + offset; e.Hp = e.HpMax = 10000; e.IsApex = false;
            return e;
        }
        static PlayerInput Idle(TunnelSim sim) => new PlayerInput { AimWorld = sim.Player.Position + new Vec2(1, 0) };
        static void Run(TunnelSim sim, double seconds) { int n = (int)Math.Ceiling(seconds / SimTuning.FixedDeltaTime); for (int i = 0; i < n; i++) sim.Tick(SimTuning.FixedDeltaTime, Idle(sim)); }

        [Test]
        public void Apply_ReadsEquipped_AndAbyssCutsHp()
        {
            var (sim, meta) = NewSim("r_abyss", "r_gel");
            Assert.IsTrue(sim.RelicFx.Has("r_abyss") && sim.RelicFx.Has("r_gel"));
            Assert.AreEqual(Math.Max(30, SimTuning.PlayerHp - Math.Round(SimTuning.PlayerHp * .3)), sim.Player.HpMax, 1e-9);
        }

        [Test]
        public void DamageMultipliers_Abyss_Strata_Permafrost()
        {
            var (sim, _) = NewSim("r_abyss", "r_strata", "r_permafrost");
            var e = Spawn(sim, new Vec2(2, 0));
            e.SlowTime = 2;
            sim.RelicFx.OnDescend(); // strata 1스택
            double dealt = 0; sim.EnemyHurt += ev => dealt = ev.Damage;
            sim.Enemies.HurtEnemy(e, 100, new Vec2(1, 0), sim.Player.Position);
            Assert.AreEqual(100 * 1.5 * 1.04 * 1.25, dealt, 1e-6);
        }

        [Test]
        public void Gel_And_Stoneskin_ReducePlayerDamage()
        {
            var (sim, _) = NewSim("r_gel", "r_stoneskin");
            sim.Player.IFrames = 0;
            double hp0 = sim.Player.Hp;
            sim.Enemies.ApplyPlayerDamage(sim.Player, 40, new Vec2(1, 0));
            // raw 40 → ×.85 round = 34 → gel ×.85 = 28.9 → round 29
            Assert.AreEqual(hp0 - 29, sim.Player.Hp, 1e-9);
            // 1초 정지 후 암반 피부 ×.7
            Run(sim, 1.2);
            Assert.GreaterOrEqual(sim.RelicFx.StillT, 1);
            sim.Player.IFrames = 0; hp0 = sim.Player.Hp;
            sim.Enemies.ApplyPlayerDamage(sim.Player, 40, new Vec2(1, 0));
            Assert.AreEqual(hp0 - Math.Round(34 * .85 * .7), sim.Player.Hp, 1e-9);
        }

        [Test]
        public void Frost_Slows_And_FlashFreeze_After5Hits()
        {
            var (sim, _) = NewSim("r_frost", "r_flashfreeze");
            var e = Spawn(sim, new Vec2(2, 0));
            for (int i = 0; i < 5; i++)
            {
                e.RelicProcAt = -99;   // 프록 윈도 우회
                sim.Enemies.HurtEnemy(e, 5, new Vec2(1, 0), sim.Player.Position);
            }
            Assert.Greater(e.SlowTime, 0);
            Assert.AreEqual(.5, e.SlowMul, 1e-9, "서리 파편 + 급속 냉동 = 빙결 2개 → 공명 1 → 50% 감속");
            Assert.AreEqual(2, e.FrozenTime, 1e-9, "5회 적중 → 2초 빙결");
        }

        [Test]
        public void Ember_Burn_TicksDamage()
        {
            var (sim, _) = NewSim("r_ember");
            var e = Spawn(sim, new Vec2(2, 0));
            sim.RelicFx.ApplyBurn(e, null);
            Assert.AreEqual(3, e.BurnT, 1e-9);
            double hp0 = e.Hp;
            Run(sim, 1.05);
            Assert.Less(e.Hp, hp0, "화상 DoT");
        }

        [Test]
        public void Leech_HealsOnKill_And_Unstable_Explodes()
        {
            var (sim, _) = NewSim("r_leech", "r_unstable");
            var a = Spawn(sim, new Vec2(2, 0)); var b = Spawn(sim, new Vec2(3, 0));
            sim.Player.Hp = 50;
            double bHp = b.Hp;
            sim.Enemies.HurtEnemy(a, 1e9, new Vec2(1, 0), sim.Player.Position);
            Assert.AreEqual(53, sim.Player.Hp, 1e-9);
            Assert.Less(b.Hp, bHp, "유폭이 2.2칸 안 적을 때린다");
        }

        [Test]
        public void Rod_ZapsOnHurt_WithCooldown_And_Hourglass_StopsTime()
        {
            var (sim, _) = NewSim("r_rod", "r_hourglass");
            var e = Spawn(sim, new Vec2(2, 0));
            sim.Player.IFrames = 0;
            sim.Enemies.ApplyPlayerDamage(sim.Player, 10, new Vec2(1, 0));
            Assert.Greater(e.ShockT, 0); Assert.Greater(e.StunTime, 0);
            Assert.AreEqual(8, sim.RelicFx.RodCd, 1e-9);
            Assert.AreEqual(3, sim.RelicFx.TimeStopT, 1e-9);
            var pos = e.Position;
            Run(sim, 0.5);
            Assert.AreEqual(pos, e.Position, "시간 정지 중 적은 움직이지 않는다");
            Assert.IsTrue(sim.RelicFx.TimeStopped);
            Run(sim, 3);
            Assert.IsFalse(sim.RelicFx.TimeStopped);
        }

        [Test]
        public void Phoenix_RevivesOnce()
        {
            var (sim, _) = NewSim("r_phoenix");
            sim.Player.IFrames = 0;
            sim.Enemies.ApplyPlayerDamage(sim.Player, 1e6, new Vec2(1, 0));
            sim.Tick(SimTuning.FixedDeltaTime, Idle(sim));
            Assert.AreEqual(GamePhase.Playing, sim.Phase);
            Assert.AreEqual(Math.Round(sim.Player.HpMax * .4), sim.Player.Hp, 1e-9);
            Assert.IsTrue(sim.RelicFx.PhoenixUsed);
            sim.Player.IFrames = 0;
            sim.Enemies.ApplyPlayerDamage(sim.Player, 1e6, new Vec2(1, 0));
            sim.Tick(SimTuning.FixedDeltaTime, Idle(sim));
            Assert.AreEqual(GamePhase.Result, sim.Phase, "런당 1회");
        }

        [Test]
        public void Grant_AddsToMeta_And_DuplicateFallbackGivesCore()
        {
            var (sim, meta) = NewSim("r_detector");
            int owned0 = meta.relicOwned.Count;
            var mi = typeof(RelicSystem).GetMethod("Grant", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            mi.Invoke(sim.RelicFx, new object[] { Relics.ById("r_fang"), sim.Player.Position });
            Assert.AreEqual(owned0 + 1, meta.relicOwned.Count);
            Assert.IsTrue(meta.relicOwned.Contains("r_fang"));
            int core0 = sim.Loot.Core;
            mi.Invoke(sim.RelicFx, new object[] { null, sim.Player.Position });
            Assert.AreEqual(core0 + 2, sim.Loot.Core, "도감 완성이면 코어 +2");
        }

        [Test]
        public void Smuggler_RaisesKeepRate_AtSettlement()
        {
            var (sim, meta) = NewSim("r_smuggler");
            sim.Loot.Core = 10;
            sim.EndRun(false, "test");
            Assert.AreEqual(2, sim.LastSettlement.Kept, "floor(10 × .25) = 2");
            Assert.AreEqual(2, meta.bankedCores);
        }

        [Test]
        public void Guardian_DroneFires_AtEnemyInSight()
        {
            var (sim, _) = NewSim("r_guardian");
            Assert.IsTrue(sim.RelicFx.HasDrone);
            var e = Spawn(sim, new Vec2(2.5, 0));
            int before = sim.Projectiles.Projectiles.Count;
            Run(sim, 1.2);
            Assert.Greater(sim.Projectiles.Projectiles.Count + 0, before - 1);
            Assert.Less(e.Hp, 10000, "지원 탄이 적을 맞힌다");
        }
    }
}
