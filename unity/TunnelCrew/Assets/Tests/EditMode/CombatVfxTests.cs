using NUnit.Framework;
using TunnelCrew.Presentation;
using TunnelCrew.Presentation.Juice;
using TunnelCrew.Sim;
using UnityEditor;
using UnityEngine;

namespace TunnelCrew.Tests
{
    public sealed class CombatVfxTests
    {
        [Test]
        public void 투사체_에너지_셰이더는_컴파일_오류가_없다()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Presentation/World/ProjectileEnergy.shader");
            var trail = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Presentation/World/ProjectileTrail.shader");
            var smoke = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Presentation/World/ProjectileSmokeTrail.shader");
            Assert.That(shader, Is.Not.Null);
            Assert.That(trail, Is.Not.Null);
            Assert.That(smoke, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            Assert.That(ShaderUtil.ShaderHasError(trail), Is.False);
            Assert.That(ShaderUtil.ShaderHasError(smoke), Is.False);
        }

        [Test]
        public void 아홉_투사체는_색뿐_아니라_실루엣이_각각_다르다()
        {
            string[] ids = { "standard", "multi", "pierce", "ricochet", "explosive", "rain", "laser", "support", "shard" };
            var shapes = new System.Collections.Generic.HashSet<float>();
            var proportions = new System.Collections.Generic.HashSet<int>();
            foreach (string id in ids)
            {
                var p = ProjectileVfxProfiles.Get(id);
                shapes.Add(p.Shape);
                proportions.Add(Mathf.RoundToInt(p.Length / p.Width * 100f));
                Assert.That(p.TrailTime, Is.GreaterThan(.04f), id + " 궤적");
            }
            Assert.That(shapes.Count, Is.EqualTo(ids.Length));
            Assert.That(proportions.Count, Is.GreaterThanOrEqualTo(7));
        }

        [Test]
        public void 모든_투사체는_기본_실루엣보다_세배_길게_그려진다()
        {
            var source = ProjectileVfxProfiles.Get("standard");
            var rendered = ProjectileVfxProfiles.Compose("standard", ProjectileStyleFlags.None);
            Assert.That(ProjectileVfxProfiles.LengthScale, Is.EqualTo(3f));
            Assert.That(rendered.Length, Is.EqualTo(source.Length * 3f).Within(.0001f));
            Assert.That(rendered.Width, Is.EqualTo(source.Width).Within(.0001f));
        }

        [Test]
        public void 탄환_길이는_시드마다_최대_오십퍼센트_범위에서_달라진다()
        {
            Assert.That(ProjectileVfxRenderer.MaxSeededLengthScale - ProjectileVfxRenderer.MinSeededLengthScale,
                Is.EqualTo(.50f).Within(.0001f));

            float first = ProjectileVfxRenderer.SeededLengthScale(101u);
            Assert.That(ProjectileVfxRenderer.SeededLengthScale(101u), Is.EqualTo(first));

            var lengths = new System.Collections.Generic.HashSet<int>();
            for (uint seed = 1; seed <= 64; seed++)
            {
                float length = ProjectileVfxRenderer.SeededLengthScale(seed);
                Assert.That(length, Is.InRange(ProjectileVfxRenderer.MinSeededLengthScale,
                    ProjectileVfxRenderer.MaxSeededLengthScale));
                lengths.Add(Mathf.RoundToInt(length * 10000f));
            }
            Assert.That(lengths.Count, Is.GreaterThan(56), "탄환 시드가 눈에 띄게 다양한 길이를 만들어야 한다");
        }

        [Test]
        public void 벽과_몬스터_피해_숫자는_밝은_흰색이_아닌_어두운_팔레트를_쓴다()
        {
            Assert.That(CombatView.DamageColor.grayscale, Is.LessThan(.4f));
            Assert.That(CombatView.DamageBigColor.grayscale, Is.LessThan(.4f));
            Assert.That(CombatView.DamageHotColor.grayscale, Is.LessThan(.4f));
            Assert.That(CombatView.DamageHotBigColor.grayscale, Is.LessThan(.4f));
            Assert.That(CombatView.DamageShadowColor.grayscale, Is.LessThan(.1f));
        }

        [Test]
        public void 복합_특성은_대표_탄형에_추가_시각_문법을_겹친다()
        {
            var plain = ProjectileVfxProfiles.Get("ricochet");
            var combo = ProjectileVfxProfiles.Compose("ricochet",
                ProjectileStyleFlags.Ricochet | ProjectileStyleFlags.Pierce | ProjectileStyleFlags.Explosive);
            Assert.That(combo.Length, Is.GreaterThan(plain.Length));
            Assert.That(combo.Width, Is.GreaterThan(plain.Width * .9f));
            Assert.That(combo.LightPriority, Is.GreaterThan(plain.LightPriority));
            Assert.That(combo.Trail.r + combo.Trail.b, Is.Not.EqualTo(plain.Trail.r + plain.Trail.b));
        }

        [Test]
        public void 투사체_100개에서도_렌더러는_품질_예산_안에서_재사용된다()
        {
            var sim = new TunnelSim();
            sim.StartRun(RoleId.Gunner); sim.EnterDepth(1, DungeonConfig.Runtime);
            for (int i = 0; i < 100; i++) sim.Projectiles.Projectiles.Add(new Projectile
            {
                Position = sim.Player.Position, Velocity = new Vec2(1, 0), Life = 2, VisualId = i % 2 == 0 ? "multi" : "laser"
            });
            var go = new GameObject("combat-vfx-test");
            try
            {
                var view = go.AddComponent<CombatView>();
                view.Render(sim, 1f / 60f);
                int first = view.ProjectileRendererCount;
                var shared = view.SharedProjectileMaterial;
                view.Render(sim, 1f / 60f);
                Assert.That(first, Is.EqualTo(96));
                Assert.That(view.ProjectileRendererCount, Is.EqualTo(first));
                Assert.That(view.ActiveProjectileRendererCount, Is.EqualTo(96));
                Assert.That(view.ActiveProjectileTrailCount, Is.EqualTo(64));
                Assert.That(view.ActiveProjectileLightCount, Is.EqualTo(8));
                Assert.That(view.ActiveProjectileSmokeTrailCount, Is.EqualTo(40));
                Assert.That(view.SharedProjectileMaterial, Is.SameAs(shared));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void 저사양은_탄체를_숨기지_않고_궤적과_실제_조명만_줄인다()
        {
            Assert.That(TunnelCrew.Presentation.Visual.VisualQualityRules.ProjectileVisualBudget(TunnelCrew.Presentation.Visual.VisualQualityTier.Low), Is.GreaterThanOrEqualTo(64));
            Assert.That(TunnelCrew.Presentation.Visual.VisualQualityRules.ProjectileTrailBudget(TunnelCrew.Presentation.Visual.VisualQualityTier.Low), Is.LessThan(64));
            Assert.That(TunnelCrew.Presentation.Visual.VisualQualityRules.ProjectileLightBudget(TunnelCrew.Presentation.Visual.VisualQualityTier.Low), Is.Zero);
            Assert.That(TunnelCrew.Presentation.Visual.VisualQualityRules.ProjectileLightBudget(TunnelCrew.Presentation.Visual.VisualQualityTier.Ultra), Is.LessThanOrEqualTo(12));
        }

        [Test]
        public void 투사체가_매_프레임_교체돼도_풀이_동시_표현량보다_커지지_않는다()
        {
            var go = new GameObject("projectile-churn-test");
            try
            {
                var renderer = go.AddComponent<ProjectileVfxRenderer>();
                var shots = new System.Collections.Generic.List<Projectile>();
                for (int frame = 0; frame < 12; frame++)
                {
                    shots.Clear();
                    for (int i = 0; i < 96; i++) shots.Add(new Projectile
                    {
                        Position = new Vec2(i * .01, frame * .01), Velocity = new Vec2(1, 0),
                        Life = 1, VisualId = (i & 1) == 0 ? "laser" : "ricochet",
                    });
                    renderer.Render(shots, TunnelCrew.Presentation.Visual.VisualQualityTier.Ultra);
                }
                Assert.That(renderer.RendererCount, Is.EqualTo(96));
                Assert.That(renderer.ActiveRendererCount, Is.EqualTo(96));
                Assert.That(renderer.SmokeRendererCount, Is.LessThanOrEqualTo(112));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void 발사와_착탄은_실제_환경광과_탄피를_만든다()
        {
            var go = new GameObject("combat-light-casing-test");
            try
            {
                var fx = go.AddComponent<FxSystem>();
                fx.ProjectileMuzzle(Vector2.zero, Vector2.right, "standard", ProjectileStyleFlags.None, 1, false);
                Assert.That(fx.ActiveTransientLightCount, Is.EqualTo(1));
                Assert.That(fx.ActiveCasingCount, Is.EqualTo(1));
                Assert.That(go.transform.Find("Casings"), Is.Null, "기존 중력 전용 탄피 ParticleSystem은 제거되어야 한다");

                fx.ProjectileImpact(Vector2.right, Vector2.left, "standard", ProjectileImpactKind.Wall,
                    ProjectileStyleFlags.None, true, false, false, 1f);
                Assert.That(fx.ActiveTransientLightCount, Is.EqualTo(2));
                Assert.That(fx.ActiveGroundDebrisCount, Is.GreaterThanOrEqualTo(5));
                Assert.That(fx.ActiveForgeSparkCount, Is.InRange(4, 7));
                Assert.That(fx.ActiveHeatMarkCount, Is.EqualTo(1));
                foreach (var trail in go.GetComponentsInChildren<TrailRenderer>(true))
                    Assert.That(Vector2.Distance(trail.transform.position, Vector2.right), Is.LessThan(.25f),
                        "재사용된 불꽃 Trail은 이전 피격점에서 새 피격점까지 긴 선을 만들면 안 된다");

                int wallDebris = fx.ActiveGroundDebrisCount;
                fx.EnemyHit(Vector2.zero, Vector2.right, dead: true, big: true);
                Assert.That(fx.ActiveGroundDebrisCount, Is.GreaterThan(wallDebris + 10));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void 발사와_착탄_파동은_짧고_초반_충격이_강하다()
        {
            Assert.That(ImpactWaveDirector.ShotDuration, Is.LessThan(.12f));
            Assert.That(ImpactWaveDirector.ProjectileImpactDuration, Is.LessThanOrEqualTo(.15f));
            Assert.That(ImpactWaveDirector.ShotStrengthBoost, Is.GreaterThan(1f));
            Assert.That(ImpactWaveDirector.ProjectileImpactStrengthBoost, Is.GreaterThan(1f));
        }

        [Test]
        public void 절차형_충격_도형은_96개를_넘지_않는다()
        {
            var go = new GameObject("impact-vfx-test");
            try
            {
                var fx = go.AddComponent<FxSystem>();
                for (int i = 0; i < 140; i++) fx.Ring(Vector2.zero, Color.white, .1f, 1f);
                Assert.That(fx.ActiveShapeCount, Is.EqualTo(96));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
