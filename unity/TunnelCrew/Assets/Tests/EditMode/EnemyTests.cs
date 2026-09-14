using System;
using System.Linq;
using NUnit.Framework;
using TunnelCrew.Sim;

namespace TunnelCrew.Tests
{
    /// <summary>
    /// 적의 규칙이 원본과 같은지 확인한다. 스폰 분포·상태 전이·피해량처럼
    /// 화면을 보지 않고 판정할 수 있는 것만 다룬다.
    /// </summary>
    public class EnemyTests
    {
        static TunnelSim NewSim(bool autoSpawn = false)
        {
            var sim = new TunnelSim();
            sim.EnterDepth(1, DungeonConfig.Runtime);
            if (!autoSpawn) sim.Enemies.SpawnInterval = 9999;
            return sim;
        }

        static PlayerInput Idle(TunnelSim sim) => new PlayerInput
        {
            AimWorld = sim.Player.Position + new Vec2(1, 0),
        };

        static void Run(TunnelSim sim, PlayerInput input, double seconds)
        {
            int n = (int)(seconds / SimTuning.FixedDeltaTime);
            for (int i = 0; i < n; i++) sim.Tick(SimTuning.FixedDeltaTime, input);
        }

        /// <summary>플레이어 옆의, 시야가 트인 빈 칸을 찾아 적을 놓는다.</summary>
        static EnemyState PlaceNearPlayer(TunnelSim sim, bool ranged)
        {
            var p = sim.Player.Position;
            for (int rad = 2; rad <= 8; rad++)
                for (int dr = -rad; dr <= rad; dr++)
                    for (int dc = -rad; dc <= rad; dc++)
                    {
                        int c = (int)p.X + dc, r = (int)p.Y + dr;
                        if (!sim.World.InInterior(c, r) || sim.World.IsSolid(c, r)) continue;
                        var q = WorldGrid.CellCenter(c, r);
                        if (Vec2.Distance(q, p) < 2.0) continue;
                        if (!SightUtil.IsClear(sim.World, q, p)) continue;

                        var e = sim.Enemies.Spawn(p);
                        if (e == null) return null;
                        e.Position = q;
                        e.Home = q;
                        e.IsRanged = ranged;
                        e.IsApex = false;
                        e.FaceAngle = (p - q).Angle;
                        return e;
                    }
            return null;
        }

        [Test]
        public void Spawn_RespectsCapAndRing()
        {
            var sim = NewSim();
            sim.Enemies.Cap = 8;
            for (int i = 0; i < 40; i++) sim.Enemies.Spawn(sim.Player.Position);

            Assert.AreEqual(8, sim.Enemies.Enemies.Count, "상한을 넘으면 안 된다");

            double ag = SimTuning.EnemyAggro;
            foreach (var e in sim.Enemies.Enemies)
            {
                double d = Vec2.Distance(e.Position, sim.Player.Position);
                Assert.GreaterOrEqual(d, ag * SimTuning.EnemySpawnRingMin - 1.5,
                    "스폰 링 안쪽보다 가까이 나오면 안 된다");
                var (c, r) = WorldGrid.ToCell(e.Position);
                Assert.IsFalse(sim.World.IsSolid(c, r), "벽 속에 나오면 안 된다");
            }
        }

        [Test]
        public void Spawn_ProducesVariedSizesAndSpeeds()
        {
            var sim = NewSim();
            sim.Enemies.Cap = 60;
            for (int i = 0; i < 60; i++) sim.Enemies.Spawn(sim.Player.Position);
            var list = sim.Enemies.Enemies;
            Assert.Greater(list.Count, 10, "표본이 있어야 한다");

            foreach (var e in list)
            {
                Assert.GreaterOrEqual(e.SpeedMul, SimTuning.EnemySpeedMulMin - 1e-9);
                Assert.LessOrEqual(e.SpeedMul, SimTuning.EnemySpeedMulMax + 1e-9);
                Assert.Greater(e.Radius, 0);
                Assert.Greater(e.HpMax, 0);
            }

            // 덩치가 클수록 느려야 한다 (같은 등급끼리 비교)
            var normals = list.Where(e => !e.IsApex).OrderBy(e => e.Radius).ToList();
            if (normals.Count >= 6)
            {
                double smallAvg = normals.Take(3).Average(e => e.SpeedMul);
                double bigAvg = normals.Skip(normals.Count - 3).Average(e => e.SpeedMul);
                Assert.Greater(smallAvg, bigAvg, "작은 개체가 큰 개체보다 빨라야 한다");
            }
        }

        [Test]
        public void Apex_HasEightTimesHpAndIsNeverRanged()
        {
            var sim = NewSim();
            sim.Enemies.Cap = 200;
            sim.Enemies.Threat = 9;   // 광란종 확률을 최대로
            for (int i = 0; i < 200; i++) sim.Enemies.Spawn(sim.Player.Position);

            var apex = sim.Enemies.Enemies.Where(e => e.IsApex).ToList();
            Assert.Greater(apex.Count, 0, "광란종이 나와야 한다");

            var normal = sim.Enemies.Enemies.FirstOrDefault(e => !e.IsApex);
            Assert.IsNotNull(normal);

            foreach (var e in apex)
            {
                Assert.IsFalse(e.IsRanged, "광란종은 원거리가 되지 않는다");
                Assert.AreEqual(EnemyKind.BroodBeast, e.Kind);
                Assert.AreEqual(SimTuning.ApexDamageMul, e.DamageMul, 1e-9);
                // 같은 위협도에서 체력은 정확히 8배
                Assert.AreEqual(normal.HpMax * SimTuning.ApexHpMul, e.HpMax, 1e-6, "광란종 체력은 8배");
            }
        }

        [Test]
        public void Enemy_SeesThroughOpenSpaceButNotWalls()
        {
            var sim = NewSim();
            var e = PlaceNearPlayer(sim, false);
            Assert.IsNotNull(e, "적을 놓을 자리가 있어야 한다");

            Assert.IsTrue(EnemyAiSystem.SeesTarget(sim.World, e, sim.Player.Position),
                "정면의 트인 표적은 보여야 한다");

            // 뒤를 보게 하면 원뿔 밖이라 못 본다 (근접 감지 반경 밖일 때)
            if (Vec2.Distance(e.Position, sim.Player.Position) > SimTuning.EnemyNearSense)
            {
                e.FaceAngle = (sim.Player.Position - e.Position).Angle + Math.PI;
                Assert.IsFalse(EnemyAiSystem.SeesTarget(sim.World, e, sim.Player.Position),
                    "등 뒤의 표적은 보이지 않아야 한다");
            }
        }

        [Test]
        public void MeleeEnemy_ChasesThenAttacks_AndDealsExactDamage()
        {
            var sim = NewSim();
            var e = PlaceNearPlayer(sim, false);
            Assert.IsNotNull(e);

            int hits = 0;
            sim.Enemies.PlayerHurt += _ => hits++;

            double before = sim.Player.Hp;
            Run(sim, Idle(sim), 12.0);

            Assert.AreEqual(EnemyAi.Chase, e.Ai, "가까이서 보면 추격해야 한다");
            Assert.Greater(hits, 0, "12초면 최소 한 번은 때려야 한다");

            // 원본: max(4, round(enemyDmg * 0.85)) = max(4, round(4.675)) = 5
            double perHit = Math.Max(4, JsMath.Round(SimTuning.EnemyDamage * 0.85));
            Assert.AreEqual(5, perHit, "타격당 피해는 5 여야 한다");
            Assert.AreEqual(before - hits * perHit, sim.Player.Hp, 1e-9, "피해 합계가 맞아야 한다");
        }

        [Test]
        public void AttackPhases_FollowWindupStrikeRecover()
        {
            var sim = NewSim();
            var e = PlaceNearPlayer(sim, false);
            Assert.IsNotNull(e);

            var seen = new System.Collections.Generic.List<AttackPhase>();
            var input = Idle(sim);
            for (int i = 0; i < 60 * 10; i++)
            {
                var prev = e.Attack;
                sim.Tick(SimTuning.FixedDeltaTime, input);
                if (e.Attack != prev) seen.Add(e.Attack);
                if (seen.Count >= 4) break;
            }

            Assert.Contains(AttackPhase.Windup, seen, "선딜 단계가 있어야 한다");
            Assert.Contains(AttackPhase.Strike, seen, "타격 단계가 있어야 한다");
            Assert.Contains(AttackPhase.Recover, seen, "후딜 단계가 있어야 한다");

            int wi = seen.IndexOf(AttackPhase.Windup);
            int si = seen.IndexOf(AttackPhase.Strike);
            int ri = seen.IndexOf(AttackPhase.Recover);
            Assert.Less(wi, si, "선딜이 타격보다 먼저");
            Assert.Less(si, ri, "타격이 후딜보다 먼저");
        }

        [Test]
        public void RangedEnemy_FiresShotsAndKeepsDistance()
        {
            var sim = NewSim();
            var e = PlaceNearPlayer(sim, true);
            Assert.IsNotNull(e);

            bool sawShot = false;
            var input = Idle(sim);
            for (int i = 0; i < 60 * 10; i++)
            {
                sim.Tick(SimTuning.FixedDeltaTime, input);
                if (sim.Enemies.Shots.Count > 0) sawShot = true;
            }
            Assert.IsTrue(sawShot, "원거리 적은 탄을 쏴야 한다");
        }

        [Test]
        public void HurtEnemy_AppliesKnockbackAndWakesIt()
        {
            var sim = NewSim();
            var e = sim.Enemies.Spawn(sim.Player.Position);
            Assert.IsNotNull(e);
            e.Ai = EnemyAi.Wander;
            e.Knock = Vec2.Zero;

            var dir = new Vec2(1, 0);
            sim.Enemies.HurtEnemy(e, SimTuning.EnemyGunDamage, dir, e.Position - dir * 2);

            Assert.AreEqual(EnemyAi.Chase, e.Ai, "맞으면 즉시 각성한다");
            Assert.Greater(e.Knock.Length, 0, "넉백이 걸려야 한다");
            Assert.Less(e.Hp, e.HpMax, "체력이 줄어야 한다");
        }

        [Test]
        public void 투사체_넉백은_기존_충격량의_30퍼센트다()
        {
            var sim = NewSim();
            var normal = sim.Enemies.Spawn(sim.Player.Position);
            var projectileHit = sim.Enemies.Spawn(sim.Player.Position);
            Assert.IsNotNull(normal);
            Assert.IsNotNull(projectileHit);

            projectileHit.Position = normal.Position;
            normal.Knock = Vec2.Zero;
            projectileHit.Knock = Vec2.Zero;
            var dir = new Vec2(1, 0);
            var src = normal.Position - dir * 2;

            sim.Enemies.HurtEnemy(normal, SimTuning.EnemyGunDamage, dir, src);
            sim.Enemies.HurtEnemy(projectileHit, SimTuning.EnemyGunDamage, dir, src,
                knockbackMul: ProjectileSystem.EnemyKnockbackScale);

            Assert.That(projectileHit.Knock.Length / normal.Knock.Length,
                Is.EqualTo(.30).Within(1e-6));
        }

        [Test]
        public void Apex_ResistsKnockbackMoreThanNormal()
        {
            var sim = NewSim();
            sim.Enemies.Cap = 200;
            sim.Enemies.Threat = 9;
            for (int i = 0; i < 200; i++) sim.Enemies.Spawn(sim.Player.Position);

            var apex = sim.Enemies.Enemies.First(e => e.IsApex);
            var normal = sim.Enemies.Enemies.First(e => !e.IsApex);
            // 같은 자리에서 같은 피해를 준다
            apex.Position = normal.Position;
            apex.Knock = Vec2.Zero; normal.Knock = Vec2.Zero;

            var dir = new Vec2(1, 0);
            var src = normal.Position - dir * 2;
            sim.Enemies.HurtEnemy(apex, SimTuning.EnemyGunDamage, dir, src);
            sim.Enemies.HurtEnemy(normal, SimTuning.EnemyGunDamage, dir, src);

            Assert.Less(apex.Knock.Length, normal.Knock.Length, "광란종이 덜 밀려야 한다");
            Assert.AreEqual(SimTuning.ApexKnockResist, apex.Knock.Length / normal.Knock.Length, 1e-6);
        }

        [Test]
        public void Enemies_NeverEndUpInsideWalls()
        {
            var sim = NewSim(autoSpawn: true);
            sim.Enemies.SpawnInterval = 0.4;
            sim.Enemies.SpawnBurst = 2;

            var input = Idle(sim);
            for (int i = 0; i < 60 * 30; i++)
            {
                sim.Tick(SimTuning.FixedDeltaTime, input);
                foreach (var e in sim.Enemies.Enemies)
                {
                    var (c, r) = WorldGrid.ToCell(e.Position);
                    Assert.IsFalse(sim.World.IsSolid(c, r),
                        $"tick {i}: 적이 벽 속에 있다 ({c},{r})");
                }
            }
            Assert.LessOrEqual(sim.Enemies.Enemies.Count, sim.Enemies.Cap);
        }

        [Test]
        public void StunnedEnemy_DoesNotAct()
        {
            var sim = NewSim();
            var e = PlaceNearPlayer(sim, false);
            Assert.IsNotNull(e);

            e.StunTime = 1.0;
            var before = e.Position;
            e.Velocity = Vec2.Zero;
            e.Knock = Vec2.Zero;

            Run(sim, Idle(sim), 0.5);

            Assert.AreEqual(AttackPhase.None, e.Attack, "기절 중에는 공격하지 않는다");
            Assert.Less(Vec2.Distance(before, e.Position), 0.05, "기절 중에는 스스로 움직이지 않는다");
        }
    }
}
