using NUnit.Framework;
using TunnelCrew.Presentation;
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
            Assert.That(shader, Is.Not.Null);
            Assert.That(trail, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            Assert.That(ShaderUtil.ShaderHasError(trail), Is.False);
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
            }
            finally { Object.DestroyImmediate(go); }
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
