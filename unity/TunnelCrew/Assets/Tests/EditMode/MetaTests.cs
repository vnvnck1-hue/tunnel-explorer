using System;
using System.Linq;
using NUnit.Framework;
using TunnelCrew.Sim;

namespace TunnelCrew.Tests
{
    /// <summary>영구 노드·정산·유물 소켓 규칙. 원본 §6.4.9 (80노드 · 140랭크 · 코어 760 / 런 32노드 · 56랭크).</summary>
    public class MetaTests
    {
        [Test]
        public void NodeTable_MatchesOriginalTotals()
        {
            Assert.AreEqual(80, PermanentNodes.All.Length);
            Assert.AreEqual(140, PermanentNodes.RankMax());
            Assert.AreEqual(760, PermanentNodes.CostTotal());
            Assert.AreEqual(80, PermanentNodes.All.Select(n => n.Id).Distinct().Count());
            var run = PermanentNodes.RunNodes(RoleId.Driller).ToList();
            Assert.AreEqual(32, run.Count);
            Assert.AreEqual(56, PermanentNodes.RankMax(run));
        }

        [Test]
        public void Buy_RespectsPrereqs_Cost_AndRankCap()
        {
            var m = new MetaState { bankedCores = 10 };
            var i = PermanentNodes.ById("crew_haul_i");
            var m1 = PermanentNodes.ById("crew_haul_m1");
            Assert.IsFalse(PermanentNodes.CanBuy(m1, m), "선행 없이는 못 산다");
            Assert.IsTrue(PermanentNodes.Buy(i, m));       // 1
            Assert.AreEqual(9, m.bankedCores);
            Assert.IsTrue(PermanentNodes.Buy(i, m));       // 2
            Assert.IsTrue(PermanentNodes.Buy(i, m));       // 3
            Assert.AreEqual(4, m.bankedCores);
            Assert.IsFalse(PermanentNodes.Buy(i, m), "최대 랭크 3");
            Assert.IsTrue(PermanentNodes.Buy(m1, m));      // 3
            Assert.AreEqual(1, m.bankedCores);
            Assert.IsFalse(PermanentNodes.Buy(m1, m), "코어 부족 (2랭크 5)");
        }

        [Test]
        public void Capstone_NeedsBothBranches_AndVisibility()
        {
            var m = new MetaState { bankedCores = 999 };
            var cap = PermanentNodes.ById("driller_gear_cap");
            Assert.IsFalse(PermanentNodes.Visible(cap, m), "발견 전엔 안 보인다");
            foreach (var id in new[] { "driller_gear_i", "driller_gear_m1", "driller_gear_m3", "driller_gear_m5" })
                Assert.IsTrue(PermanentNodes.Buy(PermanentNodes.ById(id), m), id);
            Assert.IsTrue(PermanentNodes.Visible(cap, m), "m5 가 캡스톤을 공개한다");
            Assert.IsFalse(PermanentNodes.CanBuy(cap, m), "한 갈래만으로는 안 된다");
            foreach (var id in new[] { "driller_gear_m2", "driller_gear_m4", "driller_gear_m6" })
                Assert.IsTrue(PermanentNodes.Buy(PermanentNodes.ById(id), m), id);
            Assert.IsTrue(PermanentNodes.Buy(cap, m));
            var P = PermanentNodes.Collect(m, RoleId.Driller);
            Assert.IsTrue(P.CardKeys.Contains("deep"), "캡스톤이 카드 계열을 해금한다");
        }

        [Test]
        public void Collect_Clamp_Commit()
        {
            var m = new MetaState();
            // 굴착 위력을 상한 이상으로 쌓는다: i 3랭크(15%) + m2 2랭크(12%) + m4 2랭크(10%) + cap(12%) = 49% → 35%
            foreach (var (id, rank) in new[] { ("driller_gear_i", 3), ("driller_gear_m2", 2), ("driller_gear_m4", 2), ("driller_gear_cap", 1) }) m.SetRank(id, rank);
            var P = PermanentNodes.Collect(m, RoleId.Driller);
            Assert.AreEqual(.49, P.DmgDrill, 1e-9);
            var sim = new TunnelSim(); sim.StartRun(RoleId.Driller); sim.EnterDepth(1, DungeonConfig.Runtime);
            PermanentNodes.Commit(P, sim.Build, sim.Player, sim.Traits);
            Assert.AreEqual(1.35, sim.Build.DrillMul, 1e-9, "상한 35%");
            Assert.Contains("DmgDrill", P.Capped);
            Assert.IsTrue(sim.Traits.Unlocked.Contains("deep"));

            // 거너 런에는 드릴러 가지가 적용되지 않는다
            var Pg = PermanentNodes.Collect(m, RoleId.Gunner);
            Assert.AreEqual(0, Pg.DmgDrill);
        }

        [Test]
        public void Settle_EscapedBanksAll_DownedKeepsByNodes()
        {
            var m = new MetaState();
            var r = m.Settle(10, escaped: true, perm: new PermState());
            Assert.AreEqual(10, m.bankedCores); Assert.AreEqual(10, r.Returned); Assert.AreEqual(1, m.escapes);

            var m2 = new MetaState();
            var r2 = m2.Settle(10, escaped: false, perm: new PermState());
            Assert.AreEqual(0, m2.bankedCores); Assert.AreEqual(10, r2.Lost);

            var m3 = new MetaState();
            var r3 = m3.Settle(10, escaped: false, perm: new PermState { KeepRate = .35, KeepMin = 6 });
            Assert.AreEqual(6, r3.Kept, "max(floor(10×.35)=3, min(keepMin 6, 10)) = 6");
            Assert.AreEqual(4, r3.Lost); Assert.AreEqual(6, m3.bankedCores);

            var m4 = new MetaState();
            var r4 = m4.Settle(10, escaped: false, perm: new PermState { RemoteSent = 8 });
            Assert.AreEqual(8, r4.Kept, "원격 전송분은 쓰러져도 남는다");

            var m5 = new MetaState();
            var r5 = m5.Settle(10, escaped: false, perm: new PermState { KeepRate = .35 }, relicKeepRate: .25);
            Assert.AreEqual(6, r5.Kept, "밀수꾼 주머니 +25%p → 60%");
        }

        [Test]
        public void RecordRun_Unlocks()
        {
            var m = new MetaState();
            var newly = m.RecordRun(depth: 3, totalBlocks: 400, bosses: 3);
            CollectionAssert.AreEquivalent(new[] { "도탄 탄두", "발파 탄두", "돌파 규격" }, newly);
            Assert.AreEqual(0, m.RecordRun(3, 100, 0).Count, "두 번 해금되지 않는다");
            Assert.AreEqual(400, m.bestBlocks);
        }

        [Test]
        public void Relics_Sockets_Crown_Resonance()
        {
            Assert.AreEqual(33, Relics.All.Length);
            var m = new MetaState();
            foreach (var id in new[] { "r_fang", "r_ember", "r_magma", "r_wildfire", "r_frost", "r_crown", "r_reactor" }) Relics.Grant(m, id);
            Assert.AreEqual(4, Relics.SocketMax(m));
            Assert.IsTrue(Relics.EquipAuto(m, "r_fang"));
            Assert.IsTrue(Relics.EquipAuto(m, "r_ember"));
            Assert.IsTrue(Relics.EquipAuto(m, "r_magma"));
            Assert.IsTrue(Relics.EquipAuto(m, "r_frost"));
            Assert.IsFalse(Relics.EquipAt(m, "r_wildfire", 4), "왕관 없이는 5번 소켓 잠금");
            Assert.IsTrue(Relics.EquipAuto(m, "r_crown"), "가장 오래된 칸(r_fang)을 교체");
            Assert.AreEqual(5, Relics.SocketMax(m));
            Assert.IsTrue(Relics.EquipAt(m, "r_wildfire", 4));
            var ids = Relics.EquippedIds(m);
            Assert.AreEqual(5, ids.Count);
            var res = Relics.Resonance(ids);
            Assert.AreEqual(2, res[RelicElem.Fire], "화염 3개 → 2단계");
            Assert.AreEqual(0, res[RelicElem.Frost]);
            Relics.EquipAt(m, "r_reactor", 1);   // r_ember 자리 — 왕관(0번)은 그대로
            res = Relics.Resonance(Relics.EquippedIds(m));
            Assert.AreEqual(2, res[RelicElem.Fire], "화염 2개(마그마·들불) → 1단 + 융합로 = 2");
            Assert.AreEqual(1, res[RelicElem.Frost], "융합로 +1");

            // 왕관을 빼면 5번 소켓이 비워진다
            Assert.IsTrue(Relics.EquipAuto(m, "r_crown"));   // 장착 중 → 해제
            Assert.AreEqual(4, Relics.SocketMax(m));
            Assert.IsNull(m.relicSockets[4]);
        }

        [Test]
        public void Sanitize_DropsUnknown_AndClampsRank()
        {
            var m = new MetaState();
            m.SetRank("nope", 3); m.SetRank("crew_haul_i", 99);
            m.relicOwned.Add("r_fang"); m.relicOwned.Add("r_fang"); m.relicOwned.Add("ghost");
            m.relicSockets[0] = "r_venom";   // 미보유
            m.Sanitize();
            Assert.AreEqual(0, m.RankOf("nope"));
            Assert.AreEqual(3, m.RankOf("crew_haul_i"));
            Assert.AreEqual(1, m.relicOwned.Count);
            Assert.IsNull(m.relicSockets[0]);
        }
    }
}
