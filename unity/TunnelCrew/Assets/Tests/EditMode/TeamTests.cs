using System;
using System.Linq;
using NUnit.Framework;
using TunnelCrew.Sim;

namespace TunnelCrew.Tests
{
    /// <summary>팀 핑 · 크루 채팅 · 퀵크래프트 — 원본 tc-ping.js / tc-chat.js / tcCraftingFeature 규칙.</summary>
    public class TeamTests
    {
        static TunnelSim NewSim(params RoleId[] roster)
        {
            var sim = new TunnelSim();
            foreach (var r in roster) sim.Crew.Roster.Add(r);
            sim.StartRun(RoleId.Driller);
            sim.EnterDepth(1, DungeonConfig.Runtime);
            var (pc, pr) = WorldGrid.ToCell(sim.Player.Position);
            for (int dr = -6; dr <= 6; dr++) for (int dc = -6; dc <= 6; dc++) { int c = pc + dc, r = pr + dr; if (sim.World.InInterior(c, r) && sim.World.IsSolid(c, r)) sim.World.ClearSilent(c, r); }
            sim.Los.MarkDirty();
            return sim;
        }
        static PlayerInput Idle(TunnelSim sim) => new PlayerInput { AimWorld = sim.Player.Position + new Vec2(1, 0) };
        static void Run(TunnelSim sim, double seconds) { int n = (int)Math.Ceiling(seconds / SimTuning.FixedDeltaTime); for (int i = 0; i < n; i++) sim.Tick(SimTuning.FixedDeltaTime, Idle(sim)); }
        static void Grant(TunnelSim sim, int pulp, int bloom)
        {
            // 재화는 수집으로만 오른다 — 아이템을 만들어 바로 줍는다
            for (int i = 0; i < pulp; i++) sim.Loot.Collect(new LootItem { Kind = ResourceKind.Pulp, Value = 1, Landed = true });
            for (int i = 0; i < bloom; i++) sim.Loot.Collect(new LootItem { Kind = ResourceKind.Bloom, Value = 1, Landed = true });
        }

        // ══ 핑
        [Test]
        public void Ping_Types_WheelOrder_AndCharges()
        {
            Assert.AreEqual(9, PingSystem.Types.Length);
            CollectionAssert.AreEqual(new[] { PingType.Go, PingType.Attack, PingType.Find, PingType.Mine, PingType.Retreat, PingType.Defend, PingType.Help, PingType.Danger }, PingSystem.Dirs, "↑가자 ↗공격 →발견 ↘채굴 ↓후퇴 ↙방어 ←도움 ↖위험");
            var sim = NewSim();
            var P = sim.Ping; var at = sim.Player.Position + new Vec2(2, 0);
            for (int i = 0; i < 4; i++) Assert.IsTrue(P.Send(PingType.Go, at + new Vec2(0, i * 2), true), "충전 4회");
            Assert.AreEqual(0, P.Charge, 1e-9);
            Assert.IsFalse(P.Send(PingType.Go, at + new Vec2(0, 9), true), "충전 0 → 전송 안 됨");
            Assert.IsTrue(P.Note.StartsWith("핑 충전"));
            Run(sim, 2.6);
            Assert.AreEqual(1, P.Charge, 1e-9, "2.5초마다 1개 회복");
            // 10초 안 6회 시도 → 5초 잠금
            for (int i = 0; i < 3; i++) P.Send(PingType.Here, at + new Vec2(0, 20 + i), true);
            Assert.IsTrue(P.Locked || P.Charge < 1);
        }

        [Test]
        public void Ping_Stack_Agree_Cancel_AndContext()
        {
            var sim = NewSim(RoleId.Gunner);
            var P = sim.Ping; var m = sim.Crew.Members[0];
            var e = sim.Enemies.Spawn(sim.Player.Position); e.Position = sim.Player.Position + new Vec2(3, 0); e.Hp = e.HpMax = 5000; e.FrozenTime = 99;
            Run(sim, .05);   // 시야는 틱 끝에 계산된다 — 적이 보이는 상태로
            Assert.IsTrue(P.Send(PingType.Attack, e.Position + new Vec2(.2, 0), true));
            var mk = P.Markers[0];
            Assert.AreEqual("enemy", mk.Ctx.Kind); Assert.AreEqual("굴벌레", mk.Ctx.Name);
            Assert.AreEqual(e.Position, mk.At, "컨텍스트 대상 위치로 스냅");
            Assert.AreEqual(1, P.Log.Count); Assert.IsTrue(P.Log[0].Text.Contains("굴벌레 공격"));
            // 같은 자리 같은 타입 → 스택
            P.Send(PingType.Attack, e.Position, true);
            Assert.AreEqual(1, P.Markers.Count); Assert.AreEqual(2, mk.Level);
            // 적이 죽으면 조기 종료(0.6초)
            e.Hp = 0; Run(sim, .7);
            Assert.AreEqual(0, P.Markers.Count);
            // 자기 마커 위 G 탭 = 취소
            var here = sim.Player.Position + new Vec2(-2, 0);
            P.Send(PingType.Here, here, false); Assert.AreEqual(1, P.Markers.Count);
            double ch = P.Charge;
            P.Send(PingType.Here, here, false); Assert.AreEqual(0, P.Markers.Count, "취소"); Assert.AreEqual(ch, P.Charge, "취소는 충전을 쓰지 않는다");
            // 미탐사 좌표는 '알 수 없는 위치'
            var far = new Vec2(sim.World.Cols - 3, sim.World.Rows - 3);
            P.Send(PingType.Find, far, true);
            Assert.AreEqual("unknown", P.Markers[P.Markers.Count - 1].Ctx.Kind);
        }

        [Test]
        public void Ping_Orders_DriveCrew_AndDangerZoneBans()
        {
            var sim = NewSim(RoleId.Driller, RoleId.Gunner);
            var P = sim.Ping; var driller = sim.Crew.Members[0]; var gunner = sim.Crew.Members[1];
            var target = sim.Player.Position + new Vec2(5, 0);
            Assert.IsTrue(P.Send(PingType.Go, target, true));
            Run(sim, .5);
            Assert.IsTrue(sim.Crew.Members.All(m => m.Goal != null && m.Goal.Label == "핑 · 가자"), "가자 — 전원 집결");
            Assert.IsTrue(sim.Crew.Members.All(m => m.Say.StartsWith("✓")), "수락 표시 1회");
            // 채굴 — 드릴러 우선, 벽 지정
            var (pc, pr) = WorldGrid.ToCell(sim.Player.Position);
            int wc = pc + 7, wr = pr; sim.World.SetTile(wc, wr, TileType.Stone);
            Assert.IsTrue(P.Send(PingType.Mine, WorldGrid.CellCenter(wc, wr), true));
            Run(sim, .4);
            Assert.AreEqual("핑 · 채굴", driller.Goal.Label);
            Assert.AreEqual(wc, driller.Goal.C);
            Assert.AreNotEqual("핑 · 채굴", gunner.Goal?.Label, "거너는 채굴 명령을 받지 않는다");
            // 같은 좌석의 새 명령형 핑은 이전 명령을 교체한다
            Assert.AreEqual(1, P.Orders.Count(o => PingSystem.Def(o.Type).Cmd));
            // 위험 — 3칸 5초 회피 구역 + 지형 봉인
            var dz = sim.Player.Position + new Vec2(-4, 0);
            P.Send(PingType.Danger, dz, true);
            Assert.AreEqual(1, P.Zones.Count);
            var (zc, zr) = WorldGrid.ToCell(dz);
            Assert.IsTrue(sim.Crew.Geo.Banned(zc, zr, sim.RunTime));
            Run(sim, 5.2);
            Assert.AreEqual(0, P.Zones.Count, "5초 뒤 해제");
        }

        // ══ 채팅
        [Test]
        public void Chat_Say_Clean_Bubble_AndAiLines()
        {
            var sim = NewSim(RoleId.Scout);
            var C = sim.Chat;
            C.AiChat = CrewChat.AiChatMode.Off;   // 자동 트리거를 끄고 규칙만 본다
            Assert.IsTrue(C.Say("  wasd\t안녕  크루!  "));
            Assert.AreEqual("wasd 안녕 크루!", C.Log[0].Text, "제어문자·연속 공백 정리");
            Assert.AreEqual("p1", C.Log[0].Seat); Assert.IsTrue(C.Bubbles.ContainsKey("p1"));
            Assert.IsFalse(C.Say("바로 또"), "250ms 연속 전송 간격");
            Assert.IsFalse(C.Say("   "), "빈 문장");
            Assert.AreEqual(CrewChat.MaxLen, CrewChat.Clean(new string('a', 200)).Length);
            Run(sim, 5.1);
            Assert.IsFalse(C.Bubbles.ContainsKey("p1"), "말풍선 5초");
            Assert.AreEqual(10, CrewChat.AiLines.Length, "검수 완료 10문장");
            // AI 멘트 — 규칙: 전체 간격 안이면 긴급만, 같은 문장 2회
            var m = sim.Crew.Members[0];
            Run(sim, 13);   // 첫 발화 대기(6~12초) 통과
            Assert.IsTrue(C.AiSay(m, "ore"));
            Assert.IsFalse(C.AiSay(m, "reload"), "15~30초 간격 안 — 비긴급 거절");
            Run(sim, 6.1);
            Assert.IsTrue(C.AiSay(m, "lowhp"), "긴급은 마지막 발화 6초 뒤 허용");
            Assert.IsTrue(C.Log.Last().Seat.StartsWith("ai:")); Assert.AreEqual("AI 스카우트", C.Log.Last().Name);
        }

        // ══ 퀵크래프트
        [Test]
        public void Craft_Recipes_Reasons_AndInstant()
        {
            Assert.AreEqual(6, QuickCraftSystem.Recipes.Length);
            CollectionAssert.AreEqual(new[] { "shaped-charge", "auto-turret", "coolant-capsule", "folding-barricade", "med-injector", "flare-bundle" }, QuickCraftSystem.Recipes.Select(r => r.Id).ToArray(), "위부터 시계방향 슬롯 순서");
            var sim = NewSim(); var Cf = sim.Craft;
            var cool = QuickCraftSystem.ById("coolant-capsule"); var med = QuickCraftSystem.ById("med-injector");
            Assert.AreEqual("재료 부족", Cf.Reason(cool));
            Grant(sim, 30, 5);
            Assert.AreEqual("지금은 냉각이 필요 없음", Cf.Reason(cool));
            Assert.AreEqual("체력이 충분함", Cf.Reason(med));
            sim.Player.DrillHeat = .8; sim.Player.DrillHeatLock = 1.2;
            Assert.IsTrue(Cf.Open()); Cf.Select("coolant-capsule");
            Assert.IsTrue(Cf.ConfirmSelection());
            Assert.AreEqual(.2, sim.Player.DrillHeat, 1e-9, "열 75% 제거"); Assert.AreEqual(0, sim.Player.DrillHeatLock);
            Assert.AreEqual(26, sim.Loot.Pulp); Assert.AreEqual(CraftPhase.Closed, Cf.Phase);
            Assert.IsTrue(Cf.Reason(cool).StartsWith("재사용"), "쿨다운 12초");
            // 응급 주사 — 0.7초 주입 후 35%
            sim.Player.Hp = 50;
            Cf.Open(); Cf.Select("med-injector"); Assert.IsTrue(Cf.ConfirmSelection());
            Assert.IsNotNull(Cf.Using); Assert.AreEqual(21, sim.Loot.Pulp); Assert.AreEqual(4, sim.Loot.Bloom);
            Run(sim, .8);
            Assert.IsNull(Cf.Using);
            Assert.AreEqual(50 + sim.Player.HpMax * .35, sim.Player.Hp, 1e-6);
            // 피격 시 취소 + 환불
            sim.Player.Hp = 50; Cf.Cooldowns.Remove("med-injector");
            Cf.Open(); Cf.Select("med-injector"); Assert.IsTrue(Cf.ConfirmSelection());
            sim.Player.Hp -= 5; Run(sim, .1);
            Assert.IsNull(Cf.Using); Assert.AreEqual(21, sim.Loot.Pulp, "환불"); Assert.AreEqual(4, sim.Loot.Bloom);
        }

        [Test]
        public void Craft_Placement_Snap_Range_Limit_AndTurretFires()
        {
            var sim = NewSim(); var Cf = sim.Craft; Grant(sim, 40, 10);
            Cf.Open(); Cf.Select("auto-turret"); Assert.IsTrue(Cf.ConfirmSelection());
            Assert.AreEqual(CraftPhase.Placing, Cf.Phase);
            Cf.UpdatePlacement(sim.Player.Position + new Vec2(6, 0));
            Assert.IsFalse(Cf.Placement.Valid, "사거리 2.8칸 밖");
            Cf.UpdatePlacement(sim.Player.Position + new Vec2(.2, 0));
            Assert.IsFalse(Cf.Placement.Valid, "0.55칸 안 — 발밑 금지");
            var spot = sim.Player.Position + new Vec2(2, 0);
            Cf.UpdatePlacement(spot);
            Assert.IsTrue(Cf.Placement.Valid);
            var (c, r) = WorldGrid.ToCell(spot);
            Assert.AreEqual(WorldGrid.CellCenter(c, r), Cf.Placement.World.Value, "타일 스냅");
            Assert.IsTrue(Cf.ConfirmPlacement());
            Assert.AreEqual(1, Cf.Turrets.Count); Assert.AreEqual(32, sim.Loot.Pulp); Assert.AreEqual(8, sim.Loot.Bloom);
            Assert.AreEqual("설치 한도 1 / 1", Cf.Reason(QuickCraftSystem.ById("auto-turret")));
            // 포탑 사격 — 3.8칸 안 적
            var e = sim.Enemies.Spawn(sim.Player.Position); e.Position = spot + new Vec2(2, 0); e.Hp = e.HpMax = 5000; e.FrozenTime = 99;
            Run(sim, 1.0);
            Assert.Less(e.Hp, 5000, "자동 포탑이 적을 맞힌다");
            // 취소는 차감 없음
            Cf.Open(); Cf.Select("folding-barricade"); Cf.ConfirmSelection(); Cf.UpdatePlacement(sim.Player.Position + new Vec2(-2, 0));
            int pulp = sim.Loot.Pulp; Cf.CancelPlacement();
            Assert.AreEqual(pulp, sim.Loot.Pulp); Assert.AreEqual(CraftPhase.Closed, Cf.Phase);
        }

        [Test]
        public void Craft_ShapedCharge_Explodes_AndBarricadeBlocksShots()
        {
            var sim = NewSim(); var Cf = sim.Craft; Grant(sim, 40, 10);
            var (pc, pr) = WorldGrid.ToCell(sim.Player.Position);
            int wc = pc + 3, wr = pr; sim.World.SetTile(wc, wr, TileType.Dirt);
            Cf.Open(); Cf.Select("shaped-charge"); Cf.ConfirmSelection();
            Cf.UpdatePlacement(WorldGrid.CellCenter(pc + 2, pr)); Assert.IsTrue(Cf.Placement.Valid);
            Assert.IsTrue(Cf.ConfirmPlacement());
            var e = sim.Enemies.Spawn(sim.Player.Position); e.Position = WorldGrid.CellCenter(pc + 2, pr) + new Vec2(0, 1); e.Hp = e.HpMax = 5000; e.FrozenTime = 99; e.ThreatHpMul = 99;   // 위협도 체력 보정 제외
            Run(sim, 2.1);
            Assert.AreEqual(0, Cf.Charges.Count);
            Assert.IsFalse(sim.World.IsSolid(wc, wr), "벽 체력 180% 피해");
            Assert.AreEqual(5000 - SimTuning.EnemyGunDamage * 2.2, e.Hp, 1, "적 피해 ×2.2 (관통 1.6칸)");
            // 방벽 — 적 투사체를 삼킨다
            Cf.Open(); Cf.Select("folding-barricade"); Cf.ConfirmSelection();
            Cf.UpdatePlacement(sim.Player.Position + new Vec2(2, 0)); Assert.IsTrue(Cf.Placement.Valid); Assert.IsTrue(Cf.ConfirmPlacement());
            var b = Cf.Barricades[0];
            sim.Enemies.Shots.Add(new EnemyShot { Position = b.At, Velocity = new Vec2(-1, 0), Life = 2, Damage = 5 });
            double hp = b.Hp; sim.Tick(SimTuning.FixedDeltaTime, Idle(sim));
            Assert.AreEqual(0, sim.Enemies.Shots.Count, "탄 소멸");
            Assert.AreEqual(hp - 9, b.Hp, 1e-9);
        }
    }
}
