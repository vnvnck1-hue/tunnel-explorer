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
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
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
                Assert.That(view.SharedProjectileMaterial, Is.SameAs(shared));
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
