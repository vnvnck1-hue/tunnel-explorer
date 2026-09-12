using NUnit.Framework;
using TunnelCrew.Sim;

namespace TunnelCrew.Tests
{
    public sealed class ExpeditionContentTests
    {
        [Test]
        public void 기본_장비는_기존_빌드_수치를_보존한다()
        {
            var b = new PlayerBuild();
            b.Reset(RoleId.Gunner, EquipmentVariant.Standard);
            Assert.That(b.Shots, Is.EqualTo(1));
            Assert.That(b.MagSize, Is.EqualTo(12));
            Assert.That(b.RoleGunMul, Is.EqualTo(1.5).Within(.0001));
            Assert.That(b.ProjectileLifeMul, Is.EqualTo(1).Within(.0001));
        }

        [TestCase(RoleId.Driller)]
        [TestCase(RoleId.Gunner)]
        [TestCase(RoleId.Scout)]
        [TestCase(RoleId.Engineer)]
        public void 모든_직업의_대체_장비가_행동_프로필을_바꾼다(RoleId role)
        {
            var standard = new PlayerBuild(); standard.Reset(role, EquipmentVariant.Standard);
            var alt = new PlayerBuild(); alt.Reset(role, EquipmentVariant.Alternative);
            bool changed = alt.DrillWidth != standard.DrillWidth || alt.Shots != standard.Shots ||
                           alt.Pierce != standard.Pierce || alt.Bounces != standard.Bounces;
            Assert.That(changed, Is.True);
            Assert.That(RoleEquipment.For(role, EquipmentVariant.Alternative).Name, Is.Not.Empty);
        }

        [Test]
        public void 계약_실패는_보상을_차감하지_않고_무결점은_다음_지층에서_재도전된다()
        {
            var p = new PlayerState { Position = new Vec2(2, 2) };
            var c = new RiskContractSystem(RiskContractId.Untouched);
            int reward = 0; c.CompletedReward += n => reward += n;
            c.OnFloorInit(1, p);
            c.OnPlayerHurt(20);
            c.OnBossDefeated();
            Assert.That(c.Completed, Is.False);
            Assert.That(reward, Is.Zero);
            c.OnFloorInit(2, p);
            c.OnBossDefeated();
            Assert.That(c.Completed, Is.True);
            Assert.That(reward, Is.EqualTo(3));
        }

        [Test]
        public void 과열_계약은_플레이어의_고열_굴착만_센다()
        {
            var p = new PlayerState { DrillHeat = .8 };
            var c = new RiskContractSystem(RiskContractId.HotDrill);
            int reward = 0; c.CompletedReward += n => reward += n;
            for (int i = 0; i < 11; i++) c.OnTileBroken(p, true);
            c.OnTileBroken(p, false);
            Assert.That(c.Completed, Is.False);
            c.OnTileBroken(p, true);
            Assert.That(c.Completed, Is.True);
            Assert.That(reward, Is.EqualTo(2));
        }

        [Test]
        public void 도감은_같은_항목을_한_번만_알리고_모르는_ID를_보존한다()
        {
            var meta = new MetaState();
            meta.codexEntries.Add("future.mod.entry");
            var codex = new CodexSystem(meta);
            int discoveries = 0; codex.Discovered += _ => discoveries++;
            Assert.That(codex.Discover("geology.ore"), Is.True);
            Assert.That(codex.Discover("geology.ore"), Is.False);
            meta.Sanitize();
            Assert.That(discoveries, Is.EqualTo(1));
            Assert.That(meta.codexEntries, Does.Contain("future.mod.entry"));
        }

        [Test]
        public void 생물_도감은_누적_10회_처치에서_생태_기록을_연다()
        {
            var meta = new MetaState();
            var codex = new CodexSystem(meta);
            var enemy = new EnemyState { Kind = EnemyKind.Crawler, Hp = 0 };
            for (int i = 0; i < 10; i++) codex.RecordKill(enemy);
            Assert.That(meta.CodexCount("kills.crawler"), Is.EqualTo(10));
            Assert.That(meta.codexEntries, Does.Contain("creature.crawler.kill"));
            Assert.That(meta.codexEntries, Does.Contain("creature.crawler.veteran"));
        }
    }
}
