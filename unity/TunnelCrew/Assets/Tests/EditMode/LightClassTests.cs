using NUnit.Framework;
using TunnelCrew.Presentation.Visual;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TunnelCrew.Tests
{
    /// <summary>
    /// 광원 분류와 그림자 예산(기능명세서 §7.3, §13).
    /// </summary>
    public class LightClassTests
    {
        // ───────────────────────────── 분류 유도 (실제 manifest 값)

        [Test]
        public void 작업등과_경고등은_작업등_분류다()
        {
            // 승인 패키지 revision 16 실측값.
            Assert.AreEqual(LightClass.Worklamp,
                LightClassRules.Classify("amber_glass", 3.0f, 0.75f), "작업등");
            Assert.AreEqual(LightClass.Worklamp,
                LightClassRules.Classify("magenta_warning_glass", 2.25f, 0.6f), "경고등");
        }

        [Test]
        public void 결정과_광물은_생체_광물광이다()
        {
            Assert.AreEqual(LightClass.MineralGlow,
                LightClassRules.Classify("cyan_crystal", 3.5f, 0.8f));
            Assert.AreEqual(LightClass.MineralGlow,
                LightClassRules.Classify("magenta_mineral", 3f, 0.7f));
        }

        [Test]
        public void 작고_약한_소켓은_표시등이다()
        {
            // 드릴의 두 소켓 — 방을 밝히는 광원이 아니라 계기 표시등이다.
            Assert.AreEqual(LightClass.Indicator,
                LightClassRules.Classify("amber_cyan_indicators", 1.25f, 0.35f), "service");
            Assert.AreEqual(LightClass.Indicator,
                LightClassRules.Classify("amber_cyan_indicators", 1.0f, 0.25f), "status");
        }

        [Test]
        public void 규모가_먼저다()
        {
            // 결정이라도 아주 작고 약하면 표시등으로 본다 — 그림자 예산을 지키기 위해서다.
            Assert.AreEqual(LightClass.Indicator,
                LightClassRules.Classify("cyan_crystal", 1.0f, 0.2f));
            // 반대로 크면 결정 그대로다.
            Assert.AreEqual(LightClass.MineralGlow,
                LightClassRules.Classify("cyan_crystal", 3.5f, 0.8f));
        }

        [Test]
        public void 모르는_emissionMode_는_작업등으로_떨어진다()
        {
            Assert.AreEqual(LightClass.Worklamp, LightClassRules.Classify(null, 3f, 0.7f));
            Assert.AreEqual(LightClass.Worklamp, LightClassRules.Classify("", 3f, 0.7f));
            Assert.AreEqual(LightClass.Worklamp, LightClassRules.Classify("무언가", 3f, 0.7f));
        }

        // ───────────────────────────── 그림자 자격과 세기

        [Test]
        public void 탐색광과_작업등만_그림자_자격이_있다()
        {
            Assert.IsTrue(LightClassRules.WantsShadow(LightClass.Scout));
            Assert.IsTrue(LightClassRules.WantsShadow(LightClass.Worklamp));
            // §7.3 — 생체·광물광은 "저비용 비그림자 Light2D"
            Assert.IsFalse(LightClassRules.WantsShadow(LightClass.MineralGlow));
            Assert.IsFalse(LightClassRules.WantsShadow(LightClass.Indicator));
            Assert.IsFalse(LightClassRules.WantsShadow(LightClass.Combat));
        }

        [Test]
        public void 탐색광_그림자가_가장_진하다()
        {
            Assert.Greater(LightClassRules.ShadowIntensity(LightClass.Scout),
                LightClassRules.ShadowIntensity(LightClass.Worklamp));
            Assert.AreEqual(0f, LightClassRules.ShadowIntensity(LightClass.MineralGlow));
            Assert.AreEqual(0f, LightClassRules.ShadowIntensity(LightClass.Indicator));
        }

        [Test]
        public void 탐색광은_언제나_그림자_우선순위가_가장_높다()
        {
            // 아무리 크고 센 작업등이라도 플레이어 손전등보다 앞설 수 없다.
            float scout = LightClassRules.ShadowPriority(LightClass.Scout, 0.5f, 0.1f);
            float lamp = LightClassRules.ShadowPriority(LightClass.Worklamp, 20f, 8f);
            Assert.Greater(scout, lamp);
        }

        [Test]
        public void 그림자_자격이_없으면_우선순위가_음수다()
        {
            Assert.Less(LightClassRules.ShadowPriority(LightClass.MineralGlow, 10f, 5f), 0f);
            Assert.Less(LightClassRules.ShadowPriority(LightClass.Indicator, 10f, 5f), 0f);
        }

        [Test]
        public void 넓고_센_작업등이_먼저_그림자를_받는다()
        {
            Assert.Greater(LightClassRules.ShadowPriority(LightClass.Worklamp, 3f, 0.75f),
                           LightClassRules.ShadowPriority(LightClass.Worklamp, 2.25f, 0.6f));
        }

        // ───────────────────────────── 깜빡임 결정성

        [Test]
        public void 시간_0_이면_깜빡이지_않는다()
        {
            // 자동 캡처(§16.3)가 프레임마다 달라지면 회귀 비교가 불가능해진다.
            foreach (LightClass c in System.Enum.GetValues(typeof(LightClass)))
                Assert.AreEqual(1f, LightClassRules.FlickerAt(c, 0f, 0.3f), 1e-6f, c.ToString());
        }

        [Test]
        public void 깜빡이지_않는_분류는_항상_1_이다()
        {
            foreach (var c in new[] { LightClass.Scout, LightClass.Indicator, LightClass.Combat })
            {
                Assert.AreEqual(1f, LightClassRules.FlickerAt(c, 1.7f, 0.2f), 1e-6f, c.ToString());
                Assert.AreEqual(0f, LightClassRules.FlickerAmplitude(c), c.ToString());
            }
        }

        [Test]
        public void 작업등_깜빡임은_미세하고_항상_양수다()
        {
            // §7.3 은 "미세 깜빡임" 을 요구한다. 크게 흔들면 광과민 옵션(§14 F)에 걸린다.
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < 2000; i++)
            {
                float k = LightClassRules.FlickerAt(LightClass.Worklamp, i * 0.01f, 0.13f);
                min = Mathf.Min(min, k);
                max = Mathf.Max(max, k);
            }
            Assert.Greater(min, 0.9f, "가장 어두울 때도 10% 이상 줄지 않는다");
            Assert.Less(max, 1.1f, "가장 밝을 때도 10% 이상 늘지 않는다");
        }

        [Test]
        public void 광물광은_작업등보다_훨씬_느리게_숨쉰다()
        {
            Assert.Less(LightClassRules.FlickerHz(LightClass.MineralGlow),
                        LightClassRules.FlickerHz(LightClass.Worklamp));
        }

        [Test]
        public void 위상이_다르면_같은_시각에_다른_값이_나온다()
        {
            // 램프가 여러 개일 때 한꺼번에 흔들리면 방 전체가 맥박처럼 보인다.
            float a = LightClassRules.FlickerAt(LightClass.Worklamp, 2.5f, 0.0f);
            float b = LightClassRules.FlickerAt(LightClass.Worklamp, 2.5f, 0.5f);
            Assert.AreNotEqual(a, b);
        }

        // ───────────────────────────── 노멀맵 품질 (§7.1·§7.3)

        [Test]
        public void 탐색광은_노멀을_정확히_읽는다()
        {
            // §7.3 이 탐색광에 "노멀 반응" 을 명시한다.
            Assert.AreEqual(LightNormalQuality.Accurate,
                LightClassRules.NormalQuality(LightClass.Scout, VisualQualityTier.High));
        }

        [Test]
        public void 작업등도_노멀을_정확히_읽는다()
        {
            // 2026-09-09 — Fast 에서 Accurate 로 올렸다. 랜턴이 방을 실제로 밝히는 광원이라
            // 요철이 여기서 가장 잘 보인다("노멀맵이 조명에 더 강하게 반응해야 한다" 피드백).
            Assert.AreEqual(LightNormalQuality.Accurate,
                LightClassRules.NormalQuality(LightClass.Worklamp, VisualQualityTier.High));
        }

        [Test]
        public void 광물광과_전투광도_노멀을_읽는다()
        {
            // 2026-09-09 — 수정광이 Disabled 라서 방을 물들이는 마젠타·시안 광원이 요철을
            // 전혀 세우지 않았다. §7.3 의 "저비용 비그림자" 는 그림자 예산 조항이고
            // 노멀은 광원당 드로우가 아니라 노멀 버퍼 샘플이라 비용 성격이 다르다.
            foreach (var c in new[] { LightClass.MineralGlow, LightClass.Combat })
                Assert.AreEqual(LightNormalQuality.Fast,
                    LightClassRules.NormalQuality(c, VisualQualityTier.Ultra), c.ToString());
        }

        [Test]
        public void 표시등은_노멀을_읽지_않는다()
        {
            // 계기판 크기(반지름 1칸 내외)라 요철이 읽히지 않는다.
            Assert.AreEqual(LightNormalQuality.Disabled,
                LightClassRules.NormalQuality(LightClass.Indicator, VisualQualityTier.Ultra));
        }

        [Test]
        public void Low_단계는_노멀을_한_단계_낮추고_끄지는_않는다()
        {
            // §13 "Low: 노멀 단순화" — 끄면 벽 정면이 평평해져 가독성 조항과 충돌한다.
            Assert.AreEqual(LightNormalQuality.Fast,
                LightClassRules.NormalQuality(LightClass.Scout, VisualQualityTier.Low));
            Assert.AreEqual(LightNormalQuality.Fast,
                LightClassRules.NormalQuality(LightClass.Worklamp, VisualQualityTier.Low));
            // Fast 인 분류는 더 내려가지 않는다 — 끄면 형태가 사라진다.
            Assert.AreEqual(LightNormalQuality.Fast,
                LightClassRules.NormalQuality(LightClass.MineralGlow, VisualQualityTier.Low));
        }

        [Test]
        public void 노멀을_읽는_분류는_어떤_단계에서도_꺼지지_않는다()
        {
            foreach (VisualQualityTier tier in System.Enum.GetValues(typeof(VisualQualityTier)))
            {
                Assert.AreNotEqual(LightNormalQuality.Disabled,
                    LightClassRules.NormalQuality(LightClass.Scout, tier), tier.ToString());
                Assert.AreNotEqual(LightNormalQuality.Disabled,
                    LightClassRules.NormalQuality(LightClass.Worklamp, tier), tier.ToString());
            }
        }

        [Test]
        public void 열거값_이름이_URP_와_같아야_한다()
        {
            // 렌더러가 이름으로 URP 열거값을 파싱한다. 이름이 갈라지면 런타임에 터진다.
            foreach (var n in System.Enum.GetNames(typeof(LightNormalQuality)))
                Assert.IsTrue(System.Enum.IsDefined(
                    typeof(UnityEngine.Rendering.Universal.Light2D.NormalMapQuality), n),
                    $"URP 에 '{n}' 이 없다");
        }

        // ───────────────────────────── 예산 (§13)

        [Test]
        public void 예산은_품질_단계가_정한다()
        {
            // §13 품질 단계 목록의 "Low: 그림자 광원 2개" 를 지킨다.
            // 표의 "일반 4 / 스트레스 8" 은 성능 봉투이고 단계별 값이 아니다.
            Assert.AreEqual(2, LightClassRules.ShadowBudget(VisualQualityTier.Low));
            Assert.AreEqual(4, LightClassRules.ShadowBudget(VisualQualityTier.Medium));
            Assert.AreEqual(4, LightClassRules.ShadowBudget(VisualQualityTier.High));
            Assert.AreEqual(8, LightClassRules.ShadowBudget(VisualQualityTier.Ultra));
        }

        [Test]
        public void 예산을_넘는_광원은_빛은_내고_그림자만_끈다()
        {
            var root = new GameObject("LightBudgetTest");
            try
            {
                var mgr = root.AddComponent<LightSocketRenderer>();
                mgr.Tier = VisualQualityTier.Medium;     // 예산 4

                // 그림자 자격이 있는 작업등 6개 — 예산보다 둘 많다.
                var made = new LightSocket[6];
                for (int i = 0; i < made.Length; i++)
                    made[i] = NewSocket(root.transform, LightClass.Worklamp,
                        intensity: 0.5f + i * 0.1f, range: 3f);

                mgr.Apply(0f);

                Assert.AreEqual(4, mgr.ShadowCount, "예산만큼만 그림자를 켠다");

                int on = 0;
                for (int i = 0; i < made.Length; i++)
                {
                    if (made[i].ShadowOn) on++;
                    Assert.Greater(made[i].Light.intensity, 0f,
                        "예산을 넘겨도 빛은 그대로 낸다 — 광원을 끄면 방이 어두워진다");
                }
                Assert.AreEqual(4, on);

                // 가장 센 광원이 그림자를 받았는가 — 세기 0.5+i*0.1 이므로 마지막 넷이다.
                Assert.IsTrue(made[5].ShadowOn && made[4].ShadowOn && made[3].ShadowOn && made[2].ShadowOn);
                Assert.IsFalse(made[0].ShadowOn || made[1].ShadowOn);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void 그림자_자격이_없는_광원은_예산을_먹지_않는다()
        {
            var root = new GameObject("LightBudgetTest2");
            try
            {
                var mgr = root.AddComponent<LightSocketRenderer>();
                mgr.Tier = VisualQualityTier.Medium;     // 예산 4

                // 표시등·광물광을 잔뜩 두고 작업등을 둘만 둔다.
                for (int i = 0; i < 8; i++)
                    NewSocket(root.transform, LightClass.Indicator, 0.3f, 1f);
                for (int i = 0; i < 4; i++)
                    NewSocket(root.transform, LightClass.MineralGlow, 0.8f, 3.5f);
                var lampA = NewSocket(root.transform, LightClass.Worklamp, 0.75f, 3f);
                var lampB = NewSocket(root.transform, LightClass.Worklamp, 0.6f, 2.25f);

                mgr.Apply(0f);

                Assert.AreEqual(2, mgr.ShadowCount, "표시등 8개가 예산을 먹으면 랜턴에 그림자가 없어진다");
                Assert.IsTrue(lampA.ShadowOn);
                Assert.IsTrue(lampB.ShadowOn);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static LightSocket NewSocket(Transform parent, LightClass c, float intensity, float range)
        {
            var go = new GameObject($"Light {c}");
            go.transform.SetParent(parent, false);
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.pointLightOuterRadius = range;
            light.intensity = intensity;

            var s = go.AddComponent<LightSocket>();
            s.lightClass = c;
            s.baseIntensity = intensity;
            s.rangeCells = range;
            return s;
        }
    }
}
