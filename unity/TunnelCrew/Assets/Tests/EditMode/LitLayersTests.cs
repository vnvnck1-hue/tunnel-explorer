using NUnit.Framework;
using TunnelCrew.EditorTools;
using TunnelCrew.Presentation.Visual;
using UnityEngine;

namespace TunnelCrew.Tests
{
    /// <summary>
    /// 2026-09-09 시각 피드백에서 드러난 결함을 고정한다.
    ///
    /// 그때의 증상과 원인:
    /// <list type="number">
    /// <item>"오브젝트가 조명에 반응하지 않는다" — 광원의 대상 Sorting Layer 목록에
    ///       <c>WorldEntity</c>·<c>FrontStructure</c> 가 빠져 있었다.</item>
    /// <item>"모든 그림자가 네모" — 승인 manifest 의 캐스터 윤곽이 footprint 사각형이었고
    ///       43종은 아예 없었다.</item>
    /// </list>
    /// 둘 다 "값이 조용히 빠져서" 생긴 문제라 눈으로만 확인하면 다시 새어 나간다.
    /// </summary>
    public class LitLayersTests
    {
        // ───────────────────────────── 조명 대상 레이어 (§7.1)

        [Test]
        public void 조명은_오브젝트와_전경까지_비춘다()
        {
            // 이 두 레이어가 빠져서 수정·상자·드릴·난간이 빛을 받지 못했다.
            Assert.Contains(VisualLayers.WorldEntity, VisualLayers.Lit,
                "오브젝트가 빛을 받지 못하면 바닥만 밝은 화면이 된다");
            Assert.Contains(VisualLayers.FrontStructure, VisualLayers.Lit,
                "전경 암반·난간도 같은 빛을 받아야 공간이 하나로 읽힌다");
        }

        [Test]
        public void 조명은_본선이_실제로_쓰는_Default_레이어를_비춘다()
        {
            // 본선 Run 씬은 아직 이 레이어 체계를 쓰지 않는다 — 바닥·벽 타일맵과 캐릭터가
            // 전부 Default 에 있다. 2026-09-09 에 Lit 을 만들며 Default 를 빼는 바람에
            // 램프·손전등·플레이어 후광이 아무것도 비추지 못했고 게임이 전역광만으로
            // 평평해졌다. 본선을 레이어 체계로 옮기기 전까지 이 항목을 빼면 안 된다.
            Assert.Contains(VisualLayers.UnlayeredDefault, VisualLayers.Lit,
                "Default 가 빠지면 본선의 모든 광원이 아무것도 비추지 못한다");
        }

        [Test]
        public void 조명은_바닥과_벽을_비춘다()
        {
            Assert.Contains(VisualLayers.GroundBase, VisualLayers.Lit);
            Assert.Contains(VisualLayers.GroundDetail, VisualLayers.Lit);
            Assert.Contains(VisualLayers.GroundDecal, VisualLayers.Lit);
            Assert.Contains(VisualLayers.BackStructure, VisualLayers.Lit);
            Assert.Contains(VisualLayers.WallTop, VisualLayers.Lit);
        }

        [Test]
        public void 발광_후처리_UI_레이어는_조명을_받지_않는다()
        {
            // 2D 광원을 곱하면 안 되는 레이어들이다.
            foreach (var name in new[]
                     {
                         VisualLayers.WorldFX, VisualLayers.VisionAndGrade,
                         VisualLayers.WorldOverlay, VisualLayers.UI, VisualLayers.WorldVoid,
                     })
                Assert.That(VisualLayers.Lit, Has.No.Member(name), name);
        }

        [Test]
        public void 조명_레이어는_깊이_밴드를_전부_담는다()
        {
            // 깊이 밴드는 서로 앞뒤로 겹쳐 보이는 레이어다. 그중 하나만 빛을 못 받으면
            // 같은 자리에서 앞뒤 물체의 밝기가 어긋난다.
            foreach (var band in VisualLayers.DepthBand)
                Assert.Contains(band, VisualLayers.Lit, band);
        }

        [Test]
        public void 조명_레이어_ID_는_프로젝트에_있는_것만_돌려준다()
        {
            var ids = VisualLayers.LitLayerIds();
            Assert.AreEqual(VisualLayers.Lit.Length, ids.Length,
                "§6.4 소팅 레이어가 프로젝트에 다 있어야 한다 — 없으면 메뉴로 생성할 것");
            foreach (var id in ids)
                Assert.IsTrue(SortingLayer.IsValid(id));
        }

        // ───────────────────────────── 그림자 윤곽 (§7.4)

        [Test]
        public void footprint_사각형_윤곽은_실루엣_정보가_없는_것으로_본다()
        {
            // 아치 5×1 이 준 값 — 발점 기준으로 옮기면 정확히 footprint 사각형이다.
            var rect = new[] { -2.5f, 0f, 2.5f, 0f, 2.5f, 1f, -2.5f, 1f };
            Assert.IsTrue(SpriteAlphaContour.IsPlainRectangle(rect, 5, 1));
        }

        [Test]
        public void 윤곽이_없으면_실루엣_정보가_없는_것으로_본다()
        {
            Assert.IsTrue(SpriteAlphaContour.IsPlainRectangle(null, 3, 2));
        }

        [Test]
        public void 사각형이_아닌_윤곽은_그대로_믿는다()
        {
            // 밑동이 좁아지는 기둥 — 아트가 이런 값을 주면 알파 추적을 하지 않는다.
            var taper = new[] { -0.2f, 0f, 0.2f, 0f, 0.5f, 0.5f, 0.5f, 1f, -0.5f, 1f, -0.5f, 0.5f };
            Assert.IsFalse(SpriteAlphaContour.IsPlainRectangle(taper, 1, 1));
        }

        [Test]
        public void 다른_footprint_의_사각형은_이_자산의_사각형이_아니다()
        {
            // 같은 8개 값이라도 footprint 가 다르면 사각형 판정이 달라야 한다 —
            // 그러지 않으면 아트가 준 진짜 윤곽을 사각형으로 오해해 덮어쓴다.
            var rect5x1 = new[] { -2.5f, 0f, 2.5f, 0f, 2.5f, 1f, -2.5f, 1f };
            Assert.IsFalse(SpriteAlphaContour.IsPlainRectangle(rect5x1, 1, 1));
        }
    }
}
