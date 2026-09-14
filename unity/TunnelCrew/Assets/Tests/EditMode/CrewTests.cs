using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TunnelCrew.Sim;

namespace TunnelCrew.Tests
{
    /// <summary>AI 크루 — 원본 crew-ai.js 의 편성·행동 예산·성장 분리·전원 탑승·상호 구조 규칙.</summary>
    public class CrewTests
    {
        static TunnelSim NewSim(params RoleId[] roster)
        {
            var sim = new TunnelSim();
            foreach (var r in roster) sim.Crew.Roster.Add(r);
            sim.StartRun(RoleId.Driller);
            sim.EnterDepth(1, DungeonConfig.Runtime);
            OpenRoom(sim, 6);
            return sim;
        }
        /// <summary>플레이어 주변을 빈 방으로 만든다 — 시야·이동이 지형에 좌우되지 않게.</summary>
        static void OpenRoom(TunnelSim sim, int radius)
        {
            var (pc, pr) = WorldGrid.ToCell(sim.Player.Position);
            for (int dr = -radius; dr <= radius; dr++) for (int dc = -radius; dc <= radius; dc++)
            {
                int c = pc + dc, r = pr + dr;
                if (sim.World.InInterior(c, r) && sim.World.IsSolid(c, r)) sim.World.ClearSilent(c, r);
            }
            sim.Los.MarkDirty();
        }
        static PlayerInput Idle(TunnelSim sim) => new PlayerInput { AimWorld = sim.Player.Position + new Vec2(1, 0) };
        static void Run(TunnelSim sim, double seconds) { int n = (int)Math.Ceiling(seconds / SimTuning.FixedDeltaTime); for (int i = 0; i < n; i++) sim.Tick(SimTuning.FixedDeltaTime, Idle(sim)); }
        static EnemyState Spawn(TunnelSim sim, Vec2 at, double hp = 10000)
        {
            var e = sim.Enemies.Spawn(sim.Player.Position);
            e.Position = at; e.Hp = e.HpMax = hp; e.IsApex = false; e.Ai = EnemyAi.Chase;
            return e;
        }

        [Test]
        public void Kit_MatchesOriginalBudgetTable()
        {
            var d = CrewKit.For(RoleId.Driller); var g = CrewKit.For(RoleId.Gunner); var s = CrewKit.For(RoleId.Scout); var e = CrewKit.For(RoleId.Engineer);
            Assert.AreEqual(1.35, d.DigMul); Assert.AreEqual(5, d.PathDigCost); Assert.AreEqual(12, d.Alert); Assert.AreEqual(5, d.Intercept); Assert.IsTrue(d.DrillMelee && d.Crack); Assert.AreEqual(210, d.Hp);
            Assert.AreEqual(.10, g.DigMul); Assert.AreEqual(1.55, g.GunMul); Assert.AreEqual(.70, g.Accuracy); Assert.AreEqual(30, g.PathDigCost); Assert.AreEqual(20, g.Alert); Assert.AreEqual(13, g.Intercept); Assert.AreEqual(9, g.BreakerCd); Assert.IsTrue(g.BreakerAtk);
            Assert.AreEqual(.42, s.DigMul); Assert.AreEqual(17, s.Alert); Assert.AreEqual(7, s.FlareCd); Assert.AreEqual(1.4, s.DashMul);
            Assert.AreEqual(.75, e.DigMul); Assert.AreEqual(2, e.MaxTurrets); Assert.AreEqual(12, e.TurretCd); Assert.AreEqual(14, e.TurretMag); Assert.AreEqual(4.2, e.NodeRadius);
            Assert.AreEqual(30, CrewTraits.All.Length, "AI 특성 풀 30장");
            Assert.AreEqual(4, CrewTraits.All.Select(t => t.Role).Distinct().Count());
        }

        [Test]
        public void Roster_SpawnsMembersNearLeader_AndPersistsAcrossFloors()
        {
            var sim = NewSim(RoleId.Gunner, RoleId.Scout);
            Assert.AreEqual(2, sim.Crew.Members.Count);
            foreach (var m in sim.Crew.Members)
            {
                Assert.Less(Vec2.Distance(m.Position, sim.Player.Position), 2.5, "리더 1.5칸 안에 소환");
                Assert.IsFalse(sim.World.IsSolid(WorldGrid.ToCell(m.Position).c, WorldGrid.ToCell(m.Position).r), "빈 칸에 선다");
            }
            var gunner = sim.Crew.Members[0];
            gunner.Level = 3; gunner.Hp = 10;
            sim.EnterDepth(2, DungeonConfig.Runtime);   // 하강 — 같은 사람이 따라 내려온다
            Assert.AreEqual(2, sim.Crew.Members.Count);
            Assert.AreSame(gunner, sim.Crew.Members[0]);
            Assert.AreEqual(3, gunner.Level, "레벨·성향은 유지");
            Assert.GreaterOrEqual(gunner.Hp, gunner.HpMax * .6, "층 진입 시 최소 60% 회복");
            Assert.Less(Vec2.Distance(gunner.Position, sim.Player.Position), 2.5);

            Assert.IsFalse(sim.Crew.Add(RoleId.Driller) && sim.Crew.Add(RoleId.Driller), "최대 3명");
            Assert.AreEqual(3, sim.Crew.Roster.Count);
        }

        [Test]
        public void FindPath_DigsThroughDirt_ButNeverThroughBedrock()
        {
            var sim = NewSim(RoleId.Driller);
            var (pc, pr) = WorldGrid.ToCell(sim.Player.Position);
            // 3×3 암반 상자 안에 서서, 오른쪽 벽만 흙 → 유일한 길은 흙을 뚫는 것
            for (int dr = -1; dr <= 1; dr++) for (int dc = -1; dc <= 1; dc++) if (dc != 0 || dr != 0) sim.World.SetTile(pc + dc, pr + dr, TileType.Rock);
            sim.World.SetTile(pc + 1, pr, TileType.Dirt);
            sim.World.SetTile(pc + 2, pr, TileType.Empty);
            var kit = CrewKit.For(RoleId.Driller);
            var path = sim.Crew.FindPath(pc, pr, pc + 2, pr, kit, null);
            Assert.IsNotNull(path);
            CollectionAssert.AreEqual(new[] { sim.World.Index(pc + 1, pr), sim.World.Index(pc + 2, pr) }, path, "흙 한 칸을 뚫고 지나간다");
            var gpath = sim.Crew.FindPath(pc, pr, pc + 2, pr, CrewKit.For(RoleId.Gunner), null);
            Assert.IsNotNull(gpath, "거너도 다른 길이 없으면 뚫는다 (비용이 비쌀 뿐)");
            sim.World.SetTile(pc + 1, pr, TileType.Rock);
            Assert.IsNull(sim.Crew.FindPath(pc, pr, pc + 2, pr, kit, null), "기반암은 무한 비용");
        }

        [Test]
        public void Enemies_TargetNearestCrew_WithHysteresis()
        {
            var sim = NewSim(RoleId.Gunner);
            var m = sim.Crew.Members[0];
            m.Position = sim.Player.Position + new Vec2(4, 0);
            var e = Spawn(sim, sim.Player.Position + new Vec2(5, 0));
            Assert.AreSame(m, sim.Enemies.TargetOf(e), "가까운 크루를 노린다");
            e.Position = sim.Player.Position - new Vec2(1, 0);
            Assert.AreSame(m, sim.Enemies.TargetOf(e), "0.6초 이력 — 바로 바꾸지 않는다");
            Run(sim, .7);
            Assert.AreSame(sim.Player, sim.Enemies.TargetOf(e), "이력이 끝나면 더 가까운 사람으로");
            m.Down = true;
            e.Position = m.Position + new Vec2(.2, 0); Run(sim, .7);
            Assert.AreSame(sim.Player, sim.Enemies.TargetOf(e), "다운된 크루는 표적이 아니다");
        }

        [Test]
        public void Crew_FiresAtVisibleEnemy_WithOwnedProjectiles_AndKillXpGoesToCrew()
        {
            var sim = NewSim(RoleId.Gunner);
            var m = sim.Crew.Members[0];
            m.Position = sim.Player.Position + new Vec2(-1.5, 0);
            var e = Spawn(sim, sim.Player.Position + new Vec2(3, 0));
            e.SpeedMul = 0; e.FrozenTime = 99;   // 제자리
            int humanXp = sim.Xp.Xp;
            bool owned = false; sim.ProjectileFired += _ => { foreach (var p in sim.Projectiles.Projectiles) if (p.Owner == m) owned = true; };
            Run(sim, 2.5);
            Assert.IsTrue(owned, "AI 탄에는 Owner 가 달린다");
            Assert.Less(e.Hp, 10000, "적을 맞힌다");
            Assert.AreEqual(humanXp, sim.Xp.Xp, "AI 사격은 사람 경험치를 올리지 않는다");

            // 처치 — 피해 출처가 크루면 크루가 XP 를 받는다
            int crewXp = m.Xp; humanXp = sim.Xp.Xp;
            sim.Enemies.DamageSource = m;
            sim.Enemies.HurtEnemy(e, 1e9, new Vec2(1, 0), m.Position);
            sim.Enemies.DamageSource = null;
            Assert.Greater(m.Xp + (m.Level - 1) * 1000, crewXp, "크루 처치 XP");
            Assert.AreEqual(humanXp, sim.Xp.Xp);
        }

        [Test]
        public void 관전_거너도_조정된_탄착편차와_서로_다른_탄환시드를_사용한다()
        {
            var sim = NewSim(RoleId.Gunner);
            var m = sim.Crew.Members[0];
            m.Position = sim.Player.Position + new Vec2(-1.5, 0);
            var enemy = Spawn(sim, sim.Player.Position + new Vec2(3, 0));
            enemy.SpeedMul = 0; enemy.FrozenTime = 99;
            var deviations = new List<double>();
            var seeds = new HashSet<uint>();
            var startDistances = new List<double>();

            sim.ProjectileFired += fired =>
            {
                if (!fired.Ai || fired.VisualId != "standard") return;
                var projectile = sim.Projectiles.Projectiles.FirstOrDefault(p => p.Owner == m && p.VisualSeed == fired.VisualSeed);
                if (projectile == null) return;
                double targetAngle = (enemy.Position - m.Position).Angle;
                double delta = Math.Atan2(Math.Sin(fired.Angle - targetAngle), Math.Cos(fired.Angle - targetAngle));
                deviations.Add(Math.Abs(delta));
                seeds.Add(fired.VisualSeed);
                startDistances.Add(Vec2.Distance(projectile.Position, m.Position));
                Assert.That(Vec2.Distance(fired.Position, m.Position), Is.LessThan(1e-9), "머즐 이벤트 원점은 캐릭터 중심");
            };

            Run(sim, 2.5);

            Assert.That(deviations.Count, Is.GreaterThanOrEqualTo(6));
            double maxDeviation = (1.0 - m.Kit.Accuracy) * ProjectileSystem.MaxInaccuracyRadians;
            double minDeviation = maxDeviation * ProjectileSystem.MinInaccuracyFraction;
            Assert.That(deviations.All(v => v >= minDeviation - 1e-9 && v <= maxDeviation + 1e-9), Is.True);
            Assert.That(seeds.Count, Is.EqualTo(deviations.Count), "모든 탄환의 시드가 달라야 한다");
            Assert.That(startDistances.All(d => Math.Abs(d - ProjectileSystem.CharacterMuzzleOffset) < 1e-9), Is.True);
        }

        [Test]
        public void CrewBreak_CreditsCrewXp_NotHuman_ButCountsDominance()
        {
            var sim = NewSim(RoleId.Driller);
            var m = sim.Crew.Members[0];
            var (pc, pr) = WorldGrid.ToCell(sim.Player.Position);
            int c = pc + 7, r = pr; sim.World.SetTile(c, r, TileType.Stone);
            int humanXp = sim.Xp.Xp, crewXp = m.Xp, broken = sim.Run.FloorBroken;
            sim.BreakSource = m;
            sim.World.Damage(c, r, 1e6, new Vec2(1, 0));
            sim.BreakSource = null;
            Assert.IsFalse(sim.World.IsSolid(c, r));
            Assert.AreEqual(humanXp, sim.Xp.Xp, "사람 XP 불변 (§9.6.7)");
            Assert.AreEqual(crewXp + XpGate.XpStone, m.Xp, "돌 2 × 드릴러 굴착 가중치 1.0");
            Assert.AreEqual(broken + 1, sim.Run.FloorBroken, "장악도에는 기여한다");
        }

        [Test]
        public void Escape_WaitsForAllSurvivors_ThenLifts()
        {
            var sim = NewSim(RoleId.Scout);
            var m = sim.Crew.Members[0];
            var pod = sim.Player.Position + new Vec2(2, 0);
            sim.Escape.AutoSummon(sim.Player, pod, 1);
            Run(sim, EscapeSystem.SummonNeedFor(1) + .1);
            Assert.AreEqual(EscapePhase.Ready, sim.Escape.Phase);
            // 사람만 탄다 — 크루가 멀리 있으면 대기 (2/2 가 아니다)
            m.Position = sim.Player.Position + new Vec2(-30, 0); m.Boarded = false; m.BoardT = 0;
            sim.Player.Position = pod;
            for (int i = 0; i < 90; i++) { sim.Player.Position = pod; sim.Tick(SimTuning.FixedDeltaTime, Idle(sim)); m.Position = sim.Player.Position + new Vec2(-30, 0); }
            Assert.AreEqual(EscapePhase.Ready, sim.Escape.Phase, "생존자 전원이 타야 뜬다");
            Assert.AreEqual(GamePhase.Playing, sim.Phase);
            var cnt = sim.Crew.EscapeCount().Value; Assert.AreEqual(2, cnt.total); Assert.AreEqual(0, cnt.boarded);
            // 크루가 도착해 탑승 → 이륙
            for (int i = 0; i < 100; i++) { sim.Player.Position = pod; m.Position = pod + new Vec2(.4, 0); sim.Tick(SimTuning.FixedDeltaTime, Idle(sim)); if (sim.Phase == GamePhase.Result) break; }
            Assert.IsTrue(m.Boarded);
            Assert.AreEqual(GamePhase.Result, sim.Phase);
            Assert.IsTrue(sim.RunEscaped);
        }

        [Test]
        public void DownedLeader_IsRevivedByCrew_At50Percent()
        {
            var sim = NewSim(RoleId.Engineer);
            var m = sim.Crew.Members[0];
            sim.Player.IFrames = 0; sim.Player.Hp = 1;
            sim.Enemies.ApplyPlayerDamage(sim.Player, 50, new Vec2(1, 0));
            Assert.IsTrue(sim.Player.Downed);
            sim.Tick(SimTuning.FixedDeltaTime, Idle(sim));
            Assert.AreEqual(GamePhase.Playing, sim.Phase, "구조할 동료가 있으면 런이 끝나지 않는다");
            Assert.IsTrue(sim.PlayerDownedWaiting);
            // 크루가 곁에서 5초 치료
            for (int i = 0; i < 60 * 5.5; i++) { m.Position = sim.Player.Position + new Vec2(.8, 0); m.Down = false; sim.Tick(SimTuning.FixedDeltaTime, Idle(sim)); if (!sim.Player.Downed) break; }
            Assert.IsFalse(sim.Player.Downed);
            Assert.AreEqual(Math.Round(sim.Player.HpMax * .5), sim.Player.Hp, 1e-9);
            Assert.GreaterOrEqual(sim.Player.IFrames, 2.0);

            // 구조자가 모두 쓰러져 있으면 즉시 종료
            m.Down = true; sim.Player.IFrames = 0; sim.Player.Hp = 1;
            sim.Enemies.ApplyPlayerDamage(sim.Player, 50, new Vec2(1, 0));
            sim.Tick(SimTuning.FixedDeltaTime, Idle(sim));
            Assert.AreEqual(GamePhase.Result, sim.Phase);
        }

        [Test]
        public void CrewMember_TakesEnemyDamage_AndGoesDown_ThenRescued()
        {
            var sim = NewSim(RoleId.Scout);
            var m = sim.Crew.Members[0];
            sim.Crew.Hurt(m, 40, new Vec2(1, 0));
            Assert.AreEqual(m.HpMax - 40, m.Hp, 1e-9);
            Assert.Greater(m.IFrames, 0);
            m.IFrames = 0;
            sim.Crew.Hurt(m, 1e6, new Vec2(1, 0));
            Assert.IsTrue(m.Down);
            Assert.AreEqual(0, sim.Crew.RescuersAlive());
            // 사람이 곁에 서서 5초
            for (int i = 0; i < 60 * 5.5; i++) { sim.Player.Position = m.Position + new Vec2(.8, 0); sim.Tick(SimTuning.FixedDeltaTime, Idle(sim)); if (!m.Down) break; }
            Assert.IsFalse(m.Down);
            Assert.AreEqual(m.HpMax * .5, m.Hp, 1e-9);
        }
    }
}
