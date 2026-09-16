using NUnit.Framework;
using TunnelCrew.Presentation.Visual;
using UnityEngine;

namespace TunnelCrew.Tests
{
    /// <summary>
    /// 품질 단계와 접근성 옵션(기능명세서 §13, §10.3, §14 단계 F).
    /// </summary>
    public class VisualQualityTests
    {
        // ───────────────────────────── 가독성은 내려가지 않는다 (§13)

        [Test]
        public void 가독성_필수_기능은_어떤_단계에서도_끌_수_없다()
        {
            // §13 — "가독성에 필요한 벽 정면, 전경 가림, 접촉 AO는 품질 단계에서 제거하지 않는다"
            Assert.IsFalse(VisualQualityRules.MayDisable(VisualQualityRules.Feature.WallFront));
            Assert.IsFalse(VisualQualityRules.MayDisable(VisualQualityRules.Feature.ForegroundOcclusion));
            Assert.IsFalse(VisualQualityRules.MayDisable(VisualQualityRules.Feature.ContactAo));
            // 가려진 캐릭터 실루엣도 §6.6·§15.2 가독성 장치다.
            Assert.IsFalse(VisualQualityRules.MayDisable(VisualQualityRules.Feature.OccludedSilhouette));
        }

        [Test]
        public void 형태에_필요없는_기능은_끌_수_있다()
        {
            Assert.IsTrue(VisualQualityRules.MayDisable(VisualQualityRules.Feature.Atmosphere));
            Assert.IsTrue(VisualQualityRules.MayDisable(VisualQualityRules.Feature.CastShadows));
            Assert.IsTrue(VisualQualityRules.MayDisable(VisualQualityRules.Feature.AmbientVfx));
        }

        // ───────────────────────────── 단계별 값

        [Test]
        public void Low_는_그림자_광원_2개다()
        {
            // §13 품질 단계 목록이 직접 지정한 값이다.
            Assert.AreEqual(2, VisualQualityRules.ShadowBudget(VisualQualityTier.Low));
        }

        [Test]
        public void 예산은_단계가_오를수록_늘어난다()
        {
            Assert.LessOrEqual(VisualQualityRules.ShadowBudget(VisualQualityTier.Low),
                               VisualQualityRules.ShadowBudget(VisualQualityTier.Medium));
            Assert.LessOrEqual(VisualQualityRules.ShadowBudget(VisualQualityTier.Medium),
                               VisualQualityRules.ShadowBudget(VisualQualityTier.High));
            Assert.LessOrEqual(VisualQualityRules.ShadowBudget(VisualQualityTier.High),
                               VisualQualityRules.ShadowBudget(VisualQualityTier.Ultra));
        }

        [Test]
        public void 예산은_성능_봉투_안에_있다()
        {
            // §13 표 — 일반 4개 이하 / 스트레스 8개 이하.
            Assert.LessOrEqual(VisualQualityRules.ShadowBudget(VisualQualityTier.High), 4);
            Assert.LessOrEqual(VisualQualityRules.ShadowBudget(VisualQualityTier.Ultra), 8);
        }

        [Test]
        public void Low_는_노멀을_줄이지만_끄지는_않는다()
        {
            // §13 은 "노멀 단순화" 다. 0 으로 끄면 벽 정면이 평평해져 가독성 조항과 충돌한다.
            float low = VisualQualityRules.NormalStrengthScale(VisualQualityTier.Low);
            Assert.Greater(low, 0f, "완전히 끄면 형태가 사라진다");
            Assert.Less(low, 1f, "줄어들어야 한다");
        }

        [Test]
        public void High_는_제작_기준이라_배율이_1_이다()
        {
            Assert.AreEqual(1f, VisualQualityRules.NormalStrengthScale(VisualQualityTier.High), 1e-6f);
            Assert.AreEqual(1f, VisualQualityRules.FogScale(VisualQualityTier.High), 1e-6f);
        }

        [Test]
        public void Low_는_저층_안개를_줄인다()
        {
            Assert.Less(VisualQualityRules.FogScale(VisualQualityTier.Low),
                        VisualQualityRules.FogScale(VisualQualityTier.High));
        }

        [Test]
        public void 주변_VFX_는_High_이상에서만_켠다()
        {
            Assert.IsFalse(VisualQualityRules.AmbientVfxEnabled(VisualQualityTier.Low));
            Assert.IsFalse(VisualQualityRules.AmbientVfxEnabled(VisualQualityTier.Medium));
            Assert.IsTrue(VisualQualityRules.AmbientVfxEnabled(VisualQualityTier.High));
            Assert.IsTrue(VisualQualityRules.AmbientVfxEnabled(VisualQualityTier.Ultra));
        }

        [Test]
        public void 전투_VFX_예산은_저사양에서만_줄고_가독성은_남는다()
        {
            Assert.AreEqual(.5f, VisualQualityRules.CombatParticleScale(VisualQualityTier.Low), 1e-6f);
            Assert.AreEqual(.75f, VisualQualityRules.CombatParticleScale(VisualQualityTier.Medium), 1e-6f);
            Assert.AreEqual(1f, VisualQualityRules.CombatParticleScale(VisualQualityTier.High), 1e-6f);
            Assert.GreaterOrEqual(VisualQualityRules.ProjectileVisualBudget(VisualQualityTier.Low), 64,
                "저사양에서도 탄막 판독에 필요한 투사체는 충분히 남긴다");
            Assert.Less(VisualQualityRules.ProjectileVisualBudget(VisualQualityTier.Low),
                        VisualQualityRules.ProjectileVisualBudget(VisualQualityTier.High));
            Assert.LessOrEqual(VisualQualityRules.ProjectileVisualBudget(VisualQualityTier.Ultra), 96);
        }

        // ───────────────────────────── 접근성 (§10.3)

        [Test]
        public void 광과민_옵션은_플래시를_줄이지_않고_0_으로_만든다()
        {
            // "조금 깜빡임" 은 광과민에 안전하지 않다.
            Assert.AreEqual(0f, VisualQualityRules.FlashScale(reducePhotosensitivity: true));
            Assert.AreEqual(1f, VisualQualityRules.FlashScale(reducePhotosensitivity: false));
        }

        [Test]
        public void 멀미_옵션은_카메라_흔들림을_0_으로_만든다()
        {
            Assert.AreEqual(0f, VisualQualityRules.ShakeScale(reduceMotion: true));
            Assert.AreEqual(1f, VisualQualityRules.ShakeScale(reduceMotion: false));
        }

        [Test]
        public void 광과민_옵션에서_광물광_호흡이_멈춘다()
        {
            float scale = VisualQualityRules.FlashScale(reducePhotosensitivity: true);
            for (int i = 0; i < 200; i++)
                Assert.AreEqual(1f,
                    LightClassRules.FlickerAt(LightClass.MineralGlow, i * 0.05f, 0.2f, scale), 1e-6f);
        }

        // ───────────────────────────── 컨트롤러

        [Test]
        public void 단계_순환은_네_단계를_돌아_처음으로_온다()
        {
            var go = new GameObject("OptionsTest");
            try
            {
                var c = go.AddComponent<VisualOptionsController>();
                c.Tier = VisualQualityTier.Low;
                Assert.AreEqual(VisualQualityTier.Medium, c.CycleTier());
                Assert.AreEqual(VisualQualityTier.High, c.CycleTier());
                Assert.AreEqual(VisualQualityTier.Ultra, c.CycleTier());
                Assert.AreEqual(VisualQualityTier.Low, c.CycleTier());
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void 컨트롤러가_조명과_대기에_단계를_전달한다()
        {
            var go = new GameObject("OptionsTest2");
            try
            {
                var lights = go.AddComponent<LightSocketRenderer>();
                var c = go.AddComponent<VisualOptionsController>();
                c.Bind(lights, null);

                c.Tier = VisualQualityTier.Low;
                Assert.AreEqual(VisualQualityTier.Low, lights.Tier);

                c.ReducePhotosensitivity = true;
                Assert.IsTrue(lights.ReducePhotosensitivity);
                // 두 옵션은 서로 독립이다 — 광과민을 켜도 멀미 옵션은 건드리지 않는다.
                Assert.AreEqual(1f, c.ShakeScale, 1e-6f, "광과민만 켰으므로 흔들림 배율은 그대로 1 이다");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void 멀미_옵션이_흔들림_배율을_바꾼다()
        {
            var go = new GameObject("OptionsTest3");
            try
            {
                var c = go.AddComponent<VisualOptionsController>();
                Assert.AreEqual(1f, c.ShakeScale, 1e-6f);
                c.ReduceMotion = true;
                Assert.AreEqual(0f, c.ShakeScale, 1e-6f);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void 전경_투명도_옵션은_더_잘_보이게만_한다()
        {
            var go = new GameObject("OptionsTest4");
            try
            {
                var c = go.AddComponent<VisualOptionsController>();

                // 0 은 "프로파일 값을 쓴다" 다.
                c.ForegroundAlphaOverride = 0f;
                Assert.AreEqual(0f, ForegroundFadeController.AlphaOverride, 1e-6f);

                c.ForegroundAlphaOverride = 0.15f;
                Assert.AreEqual(0.15f, ForegroundFadeController.AlphaOverride, 1e-6f);
            }
            finally
            {
                ForegroundFadeController.AlphaOverride = 0f;
                Object.DestroyImmediate(go);
            }
        }
    }
}
