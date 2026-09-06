using System;
using System.Linq;
using NUnit.Framework;
using TunnelCrew.Sim;

namespace TunnelCrew.Tests
{
    /// <summary>특성 카드 표·뽑기·적용·유지. 원본 INF_TRAITS/INF_LEGENDS/infPickTraits 와 일치해야 한다.</summary>
    public class TraitTests
    {
        static TunnelSim NewSim(RoleId role)
        {
            var sim = new TunnelSim();
            sim.StartRun(role);
            sim.EnterDepth(1, DungeonConfig.Runtime);
            sim.Enemies.SpawnInterval = 9999;
            return sim;
        }

        [Test]
        public void Table_HasAllOriginalCards_UniqueIds()
        {
            Assert.AreEqual(72, TraitDeck.All.Length, "역할 32 + 공용 24 + 해금 16 (원본 INF_TRAITS)");
            Assert.AreEqual(6, TraitDeck.Legends.Length);
            var ids = TraitDeck.All.Select(t => t.Id).ToList();
            Assert.AreEqual(ids.Count, ids.Distinct().Count(), "id 중복 없음");
            foreach (RoleId r in Enum.GetValues(typeof(RoleId)))
                Assert.AreEqual(8, TraitDeck.All.Count(t => t.Role == r && t.Lock == null && !t.Id.StartsWith("u_")), $"{r} 고유 8장");
        }

        [Test]
        public void Roll_RespectsRole_DrillReq_AndLock()
        {
            var sim = NewSim(RoleId.Gunner);
            var ctx = new TraitContext { Build = sim.Build, Player = sim.Player, Roles = sim.Roles };
            for (int i = 0; i < 30; i++)
            {
                var cards = sim.Traits.Roll(ctx, 1);
                Assert.AreEqual(3, cards.Length);
                foreach (var c in cards)
                {
                    Assert.IsTrue(c.Role == null || c.Role == RoleId.Gunner, c.Id);
                    Assert.IsFalse(c.NeedsDrill, "거너는 드릴 특성을 받지 않는다: " + c.Id);
                    Assert.IsNull(c.Lock, "해금 계열은 잠겨 있다: " + c.Id);
                    Assert.IsFalse(c.NotPorted, "미이식 카드는 풀에 없다: " + c.Id);
                }
                Assert.AreEqual(3, cards.Select(c => c.Id).Distinct().Count(), "한 제시에 같은 카드 없음");
            }
        }

        [Test]
        public void Pity_ForcesHighTier_AfterFourLowRolls()
        {
            var sim = NewSim(RoleId.Driller);
            var ctx = new TraitContext { Build = sim.Build, Player = sim.Player, Roles = sim.Roles };
            sim.Traits.Pity = 4;
            var cards = sim.Traits.Roll(ctx, 1);
            Assert.GreaterOrEqual(cards[0].Tier, 3, "피티 4 → 첫 장은 3티어 이상");
            Assert.AreEqual(0, sim.Traits.Pity);
        }

        [Test]
        public void Pick_AppliesEffect_AndPersistsAcrossDepth()
        {
            var sim = NewSim(RoleId.Driller);
            var ctx = new TraitContext { Build = sim.Build, Player = sim.Player, Roles = sim.Roles };
            var motor = TraitDeck.All.First(t => t.Id == "d_motor");
            var resin = TraitDeck.All.First(t => t.Id == "d_resin");
            motor.Apply(ctx); resin.Apply(ctx);
            Assert.AreEqual(1.18, sim.Build.DrillMul, 1e-9);
            Assert.AreEqual(2.5 + 1.25, sim.Build.Roles.DrillerCrackHold, 1e-9);
            Assert.AreEqual(0.012 * 0.78, sim.Build.Roles.DrillerCrackDecay, 1e-9);

            sim.EnterDepth(2, DungeonConfig.Runtime);
            Assert.AreEqual(1.18, sim.Build.DrillMul, 1e-9, "특성은 층을 넘어 유지된다");
            Assert.AreEqual(3.75, sim.Roles.GetType().GetProperty("T", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) == null
                ? sim.Build.Roles.DrillerCrackHold : sim.Build.Roles.DrillerCrackHold, 1e-9);

            sim.StartRun(RoleId.Driller);
            Assert.AreEqual(1.0, sim.Build.DrillMul, "런을 새로 시작하면 초기화");
            Assert.AreEqual(2.5, sim.Build.Roles.DrillerCrackHold);
        }

        [Test]
        public void Ok_Gates_StopRepeatOffers()
        {
            var sim = NewSim(RoleId.Gunner);
            var ctx = new TraitContext { Build = sim.Build, Player = sim.Player, Roles = sim.Roles };
            var radius = TraitDeck.All.First(t => t.Id == "g_radius");
            Assert.IsTrue(radius.Ok(ctx));
            radius.Apply(ctx);
            Assert.AreEqual(2, sim.Build.Roles.BreakerRadius);
            Assert.IsFalse(radius.Ok(ctx), "반경 2면 더 안 나온다");
            var cluster = TraitDeck.All.First(t => t.Id == "g_cluster");
            Assert.IsFalse(cluster.Ok(ctx), "군집 작약도 같은 조건");
        }

        [Test]
        public void LevelUp_OpensOffer_BlocksNextLevel_PickResumes()
        {
            var sim = NewSim(RoleId.Scout);
            int offers = 0, picks = 0;
            sim.TraitOffered += e => offers++;
            sim.TraitPicked += e => picks++;

            sim.Xp.Award(1000, XpKind.Dig, RoleId.Scout);
            Assert.AreEqual(2, sim.Xp.Level);
            Assert.AreEqual(1, offers);
            Assert.IsTrue(sim.Traits.HasOffer);

            sim.Xp.CheckLevel();
            Assert.AreEqual(2, sim.Xp.Level, "카드가 열려 있으면 다음 레벨은 적립만");

            Assert.IsTrue(sim.PickTrait(1));
            Assert.AreEqual(1, picks);
            Assert.IsFalse(sim.Traits.HasOffer, "선택 직후에는 제시가 닫힌다");
            sim.Xp.CheckLevel();
            Assert.AreEqual(3, sim.Xp.Level, "고르고 나면 다음 레벨이 열린다");
            Assert.AreEqual(2, offers);
        }

        [Test]
        public void Reroll_ConsumesStock_AndRefillsPerStratum()
        {
            var sim = NewSim(RoleId.Engineer);
            Assert.AreEqual(1, sim.Traits.Rerolls);
            sim.Xp.Award(1000, XpKind.Dig, RoleId.Engineer);
            var first = sim.Traits.Offer.Select(c => c.Id).ToArray();
            Assert.IsTrue(sim.RerollTraits());
            Assert.AreEqual(0, sim.Traits.Rerolls);
            Assert.IsFalse(sim.RerollTraits(), "재고 0");
            Assert.IsTrue(sim.Traits.HasOffer);
            sim.PickTrait(0);

            sim.EnterDepth(2, DungeonConfig.Runtime);
            Assert.AreEqual(1, sim.Traits.Rerolls, "지층마다 +1");
            sim.EnterDepth(3, DungeonConfig.Runtime);
            Assert.AreEqual(2, sim.Traits.Rerolls, "상한 2");
        }

        [Test]
        public void Rest_OffersLegends_AndGatesDescend()
        {
            var sim = NewSim(RoleId.Gunner);
            var b = sim.Bosses.Spawn(sim.Player, 1);
            sim.Enemies.HurtEnemy(b.Body, 1e12, new Vec2(1, 0), sim.Player.Position);
            sim.Tick(SimTuning.FixedDeltaTime, new PlayerInput { AimWorld = sim.Player.Position + new Vec2(1, 0) });
            sim.EnterRest();
            // 보스 격파 XP(45) 로 열린 레벨업 카드가 먼저, 그 다음 전설
            Assert.IsTrue(sim.Traits.HasOffer && !sim.Traits.OfferIsLegend, "보스전 중 쌓인 레벨업 카드부터");
            sim.PickTrait(0);
            Assert.IsTrue(sim.Traits.HasOffer && sim.Traits.OfferIsLegend);
            Assert.AreEqual(3, sim.Traits.Offer.Length);
            Assert.IsFalse(sim.RestChosen);

            sim.Descend();
            Assert.AreEqual(1, sim.Depth, "전설을 고르기 전엔 내려가지 않는다");

            double gun0 = sim.Build.GunMul;
            int idx = Array.FindIndex(sim.Traits.Offer, c => c.Id == "l_dual");
            sim.PickTrait(idx >= 0 ? idx : 0);
            Assert.IsTrue(sim.RestChosen);
            if (idx >= 0) Assert.AreEqual(gun0 * 1.175, sim.Build.GunMul, 1e-9);
            sim.Descend();
            Assert.AreEqual(2, sim.Depth);
        }

        [Test]
        public void Legend_SetMag_AdjustReload_Clamp()
        {
            var b = new PlayerBuild();
            b.Reset(RoleId.Gunner);
            b.SetMag(2);
            Assert.AreEqual(14, b.MagSize); Assert.AreEqual(14, b.Ammo, "늘어난 만큼 즉시 채운다");
            b.SetMag(100); Assert.AreEqual(40, b.MagSize);
            b.AdjustReload(10); Assert.AreEqual(3.5, b.ReloadTime);
            b.AdjustReload(0.01); Assert.AreEqual(0.42, b.ReloadTime);
        }
    }
}
