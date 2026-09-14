using NUnit.Framework;
using TunnelCrew.Presentation.Visual;
using UnityEngine;

namespace TunnelCrew.Tests
{
    /// <summary>
    /// 가려진 캐릭터 실루엣·림 패스(기능명세서 §6.6, §7.1).
    /// </summary>
    public class OccludedSilhouetteTests
    {
        // ───────────────────────────── 표시 세기 보간

        [Test]
        public void 덮이면_지정_시간_안에_최대까지_올라간다()
        {
            // §6.6 — 가려질 때 0.18~0.28초. 0.22초를 4스텝에 나눠도 총합이 시간을 지켜야 한다.
            float v = 0f;
            for (int i = 0; i < 4; i++)
                v = OccludedSilhouetteRenderer.StepVisibility(v, true, 0.22f, 0.32f, 0.055f);
            Assert.AreEqual(1f, v, 1e-4f, "0.22초가 지나면 완전히 나타난다");
        }

        [Test]
        public void 절반_시간에는_절반만_올라간다()
        {
            float v = OccludedSilhouetteRenderer.StepVisibility(0f, true, 0.22f, 0.32f, 0.11f);
            Assert.AreEqual(0.5f, v, 1e-4f);
        }

        [Test]
        public void 벗어나면_복원_시간을_따른다()
        {
            // 복원은 0.32초 — 나타나는 쪽보다 느려야 깜빡이지 않는다.
            float v = OccludedSilhouetteRenderer.StepVisibility(1f, false, 0.22f, 0.32f, 0.16f);
            Assert.AreEqual(0.5f, v, 1e-4f);
            v = OccludedSilhouetteRenderer.StepVisibility(v, false, 0.22f, 0.32f, 0.16f);
            Assert.AreEqual(0f, v, 1e-4f);
        }

        [Test]
        public void 세기는_0과_1_밖으로_나가지_않는다()
        {
            Assert.AreEqual(1f, OccludedSilhouetteRenderer.StepVisibility(0.9f, true, 0.22f, 0.32f, 10f));
            Assert.AreEqual(0f, OccludedSilhouetteRenderer.StepVisibility(0.1f, false, 0.22f, 0.32f, 10f));
        }

        [Test]
        public void 시간이_0_이면_즉시_도달한다()
        {
            // 캡처 경로처럼 시간이 흐르지 않는 곳에서 보간이 멈춰 있으면 안 된다.
            Assert.AreEqual(1f, OccludedSilhouetteRenderer.StepVisibility(0f, true, 0f, 0f, 0.016f));
        }

        // ───────────────────────────── 모드별 최대 알파

        [Test]
        public void 적은_완전_투명_처리되지_않는다()
        {
            // §6.6 — 위협 실루엣의 하한은 프로파일의 enemySilhouetteMinAlpha 다.
            float a = OccludedSilhouetteRenderer.MaxAlphaFor(SilhouetteMode.Threat, 0f, 0.55f);
            Assert.AreEqual(0.55f, a, 1e-4f);
            Assert.Greater(a, 0f);
        }

        [Test]
        public void 관심_캐릭터는_고정_기본값을_쓴다()
        {
            // 관심 캐릭터는 불투명한 벽 위에 외곽 림만 그리므로 적 하한과 무관하다.
            Assert.AreEqual(0.85f,
                OccludedSilhouetteRenderer.MaxAlphaFor(SilhouetteMode.Interest, 0f, 0.55f), 1e-4f);
            Assert.AreEqual(0.85f,
                OccludedSilhouetteRenderer.MaxAlphaFor(SilhouetteMode.Interest, 0f, 0.1f), 1e-4f);
        }

        [Test]
        public void 컴포넌트가_선언한_값이_이긴다()
        {
            Assert.AreEqual(0.4f,
                OccludedSilhouetteRenderer.MaxAlphaFor(SilhouetteMode.Threat, 0.4f, 0.55f), 1e-4f);
            Assert.AreEqual(1f,
                OccludedSilhouetteRenderer.MaxAlphaFor(SilhouetteMode.Interest, 3f, 0.55f), 1e-4f,
                "범위 밖 값은 잘린다");
        }

        [Test]
        public void 전경_벽은_기본적으로_불투명하다()
        {
            var go = new GameObject("OpaqueForegroundPolicy");
            try
            {
                var fade = go.AddComponent<ForegroundFadeController>();
                Assert.IsFalse(fade.Enabled,
                    "캐릭터 가시성은 벽 투명화가 아니라 OccludedSilhouetteRenderer가 보장한다");
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ───────────────────────────── 덮임 판정

        [Test]
        public void 오클루더에_겹치면_덮인_것으로_보고_가장_낮은_알파를_준다()
        {
            var a = NewOccluder(new Rect(0, 0, 4, 2), alpha: 0.34f);
            var b = NewOccluder(new Rect(0, 0, 4, 2), alpha: 0.8f);
            try
            {
                bool covered = ForegroundFadeController.CoveredAt(new Vector2(2f, 1f), 0.4f, out float low);
                Assert.IsTrue(covered);
                Assert.AreEqual(0.34f, low, 1e-4f, "둘이 겹치면 더 많이 사라진 쪽을 따른다");
            }
            finally { Kill(a); Kill(b); }
        }

        [Test]
        public void 멀리_있는_오클루더는_덮지_않는다()
        {
            var o = NewOccluder(new Rect(0, 0, 2, 2), alpha: 0.34f);
            try
            {
                bool covered = ForegroundFadeController.CoveredAt(new Vector2(10f, 10f), 0.4f, out float low);
                Assert.IsFalse(covered);
                Assert.AreEqual(1f, low, 1e-4f, "덮인 게 없으면 알파는 1 이다");
            }
            finally { Kill(o); }
        }

        [Test]
        public void 페이드하지_않은_불투명한_벽도_덮은_것이다()
        {
            // 적 앞의 벽은 페이드하지 않는다(적은 관심 대상이 아니다). 그래도 "가려졌다" 는
            // 참이어야 위협 실루엣이 나타난다 — 이게 관심/위협 두 모드가 갈리는 지점이다.
            var o = NewOccluder(new Rect(0, 0, 4, 2), alpha: 1f);
            try
            {
                Assert.IsTrue(ForegroundFadeController.CoveredAt(new Vector2(2f, 1f), 0.4f, out float low));
                Assert.AreEqual(1f, low, 1e-4f);
            }
            finally { Kill(o); }
        }

        // ───────────────────────────── 셀 마스크 (§6.6 겹침 판정)

        [Test]
        public void 셀_마스크가_있으면_전경_타일이_있는_곳만_가린다()
        {
            // 청크 사각형은 16×16 이지만 실제 전경 정면은 남쪽 두 줄뿐인 상황.
            var o = NewOccluder(new Rect(0, 0, 16, 16), alpha: 1f);
            try
            {
                var mask = new bool[16 * 16];
                for (int c = 0; c < 16; c++) { mask[0 * 16 + c] = true; mask[1 * 16 + c] = true; }
                o.SetCellMask(mask, 0, 0, 16, 16);
                o.capLiftCells = 1f;

                // cap 이 리프트만큼 올라가므로 행 0~1 의 cap 은 화면 행 1~3 을 덮는다.
                Assert.IsTrue(o.Overlaps(new Vector2(8f, 2f), 0.45f), "cap 바로 앞 바닥은 가려진다");
                Assert.IsFalse(o.Overlaps(new Vector2(8f, 8f), 0.45f),
                    "방 한가운데는 가려지지 않는다 — 사각형만 쓰면 여기서 참이 되어 남쪽 벽이 영구히 페이드했다");
            }
            finally { Kill(o); }
        }

        [Test]
        public void cap_은_리프트만큼_올라가_그려지므로_남쪽_행까지_찾는다()
        {
            var o = NewOccluder(new Rect(0, 0, 16, 16), alpha: 1f);
            try
            {
                var mask = new bool[16 * 16];
                mask[5 * 16 + 8] = true;          // cap 셀 하나: (8, 5)
                o.SetCellMask(mask, 0, 0, 16, 16);
                o.capLiftCells = 1.5f;

                // 셀 5 의 cap 은 화면 행 6.5~7.5 를 덮는다 — 그 앞(북쪽) 바닥이 가려진다.
                Assert.IsTrue(o.Overlaps(new Vector2(8f, 7f), 0.3f));
                // cap 이 올라간 자리보다 더 북쪽은 가리지 않는다.
                Assert.IsFalse(o.Overlaps(new Vector2(8f, 10f), 0.3f));
                // cap 셀 자신보다 남쪽도 가리지 않는다 — cap 은 위로 올라가 있다.
                Assert.IsFalse(o.Overlaps(new Vector2(8f, 3f), 0.3f));
            }
            finally { Kill(o); }
        }

        [Test]
        public void 마스크가_없으면_사각형_판정을_그대로_쓴다()
        {
            // 세트피스처럼 직접 사각형을 선언하는 오클루더의 동작이 바뀌면 안 된다.
            var o = NewOccluder(new Rect(0, 0, 4, 2), alpha: 1f);
            try
            {
                Assert.IsFalse(o.HasCellMask);
                Assert.IsTrue(o.Overlaps(new Vector2(2f, 1f), 0.4f));
                Assert.IsFalse(o.Overlaps(new Vector2(20f, 20f), 0.4f));
            }
            finally { Kill(o); }
        }

        [Test]
        public void 마스크는_참조라_제자리_수정이_바로_반영된다()
        {
            // 벽을 부수면 렌더러가 마스크를 제자리에서 고친다(§6.7 같은 dirty 단위).
            var o = NewOccluder(new Rect(0, 0, 16, 16), alpha: 1f);
            try
            {
                var mask = new bool[16 * 16];
                mask[1 * 16 + 8] = true;
                o.SetCellMask(mask, 0, 0, 16, 16);
                o.capLiftCells = 1f;
                Assert.IsTrue(o.Overlaps(new Vector2(8f, 2.5f), 0.4f));

                mask[1 * 16 + 8] = false;         // 파괴
                Assert.IsFalse(o.Overlaps(new Vector2(8f, 2.5f), 0.4f),
                    "사라진 벽이 계속 가린 것으로 판정되면 페이드가 풀리지 않는다");
            }
            finally { Kill(o); }
        }

        // ───────────────────────────── 레이어 계약

        [Test]
        public void 실루엣_레이어는_전경보다_위_LOS_어둠보다_아래다()
        {
            int front = System.Array.IndexOf(VisualLayers.InOrder, VisualLayers.FrontStructure);
            int fx = System.Array.IndexOf(VisualLayers.InOrder, VisualLayers.WorldFX);
            int grade = System.Array.IndexOf(VisualLayers.InOrder, VisualLayers.VisionAndGrade);

            Assert.Greater(fx, front, "전경 벽 위로 올라오지 못하면 위협 실루엣이 보이지 않는다");
            Assert.Less(fx, grade,
                "LOS 어둠보다 위에 있으면 시야 밖 적의 위치가 노출된다(§6.6 마지막 항)");
        }

        [Test]
        public void 셰이더가_프로젝트에_존재하고_지원된다()
        {
            var shader = Shader.Find("TunnelCrew/OccludedSilhouette");
            Assert.IsNotNull(shader, "TunnelCrew/OccludedSilhouette 셰이더를 찾지 못했다");
            Assert.IsTrue(shader.isSupported, "현재 그래픽스 API 에서 컴파일되지 않았다");
        }

        // ───────────────────────────── 도우미

        static ForegroundOccluder NewOccluder(Rect cells, float alpha)
        {
            var go = new GameObject("Occluder");
            var o = go.AddComponent<ForegroundOccluder>();
            o.footprintCells = cells;
            o.ApplyAlpha(alpha);
            return o;
        }

        static void Kill(ForegroundOccluder o)
        {
            if (o != null) Object.DestroyImmediate(o.gameObject);
        }
    }
}
