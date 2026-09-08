using System.Collections.Generic;
using NUnit.Framework;
using TunnelCrew.EditorTools.ArtPipeline;

namespace TunnelCrew.Tests
{
    /// <summary>
    /// 채널 아틀라스 배치(아트 규격 §8.1 "재질 채널별 동일 배치").
    ///
    /// 배치가 어긋나면 노멀·발광이 엉뚱한 타일에 붙는데, 그건 화면을 봐도 원인을 찾기
    /// 어렵다. 그래서 배치 규칙 자체를 여기서 고정한다.
    /// </summary>
    public class AtlasLayoutTests
    {
        [Test]
        public void 자산_하나면_셀_하나에_패딩만_두른다()
        {
            var layout = AtlasLayout.Create(1, 128, 128, 4);

            Assert.AreEqual(1, layout.Columns);
            Assert.AreEqual(1, layout.Rows);
            Assert.AreEqual(128 + 8, layout.AtlasWidth);
            Assert.AreEqual(128 + 8, layout.AtlasHeight);

            var cell = layout.RectFor(0, 128, 128);
            Assert.AreEqual(4, cell.X);
            Assert.AreEqual(4, cell.Y);
            Assert.AreEqual(128, cell.Width);
            Assert.AreEqual(128, cell.Height);
        }

        [Test]
        public void 여섯_자산은_정사각에_가까운_격자로_놓인다()
        {
            // 실제 승인 아트의 바닥 6종 — 3×2 격자, 408×272.
            var layout = AtlasLayout.Create(6, 128, 128, 4);

            Assert.AreEqual(3, layout.Columns);
            Assert.AreEqual(2, layout.Rows);
            Assert.AreEqual(3 * 136, layout.AtlasWidth);
            Assert.AreEqual(2 * 136, layout.AtlasHeight);
        }

        [Test]
        public void 셀은_서로_겹치지_않는다()
        {
            var layout = AtlasLayout.Create(6, 128, 128, 4);
            var rects = new List<AtlasLayout.Cell>();
            for (int i = 0; i < 6; i++) rects.Add(layout.RectFor(i, 128, 128));

            for (int a = 0; a < rects.Count; a++)
                for (int b = a + 1; b < rects.Count; b++)
                {
                    bool overlap =
                        rects[a].X < rects[b].X + rects[b].Width &&
                        rects[b].X < rects[a].X + rects[a].Width &&
                        rects[a].Y < rects[b].Y + rects[b].Height &&
                        rects[b].Y < rects[a].Y + rects[a].Height;
                    Assert.IsFalse(overlap, $"{a}번과 {b}번 셀이 겹친다");
                }
        }

        [Test]
        public void 셀_사이_간격이_패딩_두_쪽만큼_벌어진다()
        {
            var layout = AtlasLayout.Create(3, 128, 128, 4);

            var a = layout.RectFor(0, 128, 128);
            var b = layout.RectFor(1, 128, 128);

            // 자산 사이 빈 폭 = 패딩 × 2. 가장자리를 늘려 채우는 공간이다.
            int gap = b.X - (a.X + a.Width);
            Assert.AreEqual(8, gap, "블리딩을 막을 여백이 양쪽 4px 씩 있어야 한다");
        }

        [Test]
        public void 모든_셀이_아틀라스_안에_들어간다()
        {
            var layout = AtlasLayout.Create(7, 128, 192, 4);
            for (int i = 0; i < 7; i++)
            {
                var c = layout.RectFor(i, 128, 192);
                Assert.GreaterOrEqual(c.X - layout.Padding, 0, $"{i}번 셀의 왼쪽 패딩이 밖으로 나간다");
                Assert.GreaterOrEqual(c.Y - layout.Padding, 0, $"{i}번 셀의 아래쪽 패딩이 밖으로 나간다");
                Assert.LessOrEqual(c.X + c.Width + layout.Padding, layout.AtlasWidth,
                    $"{i}번 셀의 오른쪽 패딩이 밖으로 나간다");
                Assert.LessOrEqual(c.Y + c.Height + layout.Padding, layout.AtlasHeight,
                    $"{i}번 셀의 위쪽 패딩이 밖으로 나간다");
            }
        }

        [Test]
        public void 자산이_셀보다_작으면_아래쪽에_붙인다()
        {
            // 벽 정면은 0.75~1.5셀로 높이가 다르다(§8.6). 발점이 아래인 자산의 기준이
            // 흔들리지 않도록 셀 안에서 아래로 붙여야 한다.
            var layout = AtlasLayout.Create(2, 128, 192, 4);

            var tall = layout.RectFor(0, 128, 192);
            var shortOne = layout.RectFor(1, 128, 96);

            Assert.AreEqual(tall.Y, shortOne.Y, "두 자산의 아래 변이 같은 높이에 놓인다");
            Assert.AreEqual(96, shortOne.Height);
        }

        [Test]
        public void 자산이_셀보다_크면_셀_크기로_자른다()
        {
            var layout = AtlasLayout.Create(1, 128, 128, 4);
            var cell = layout.RectFor(0, 999, 999);
            Assert.AreEqual(128, cell.Width);
            Assert.AreEqual(128, cell.Height);
        }

        [Test]
        public void 같은_배치는_채널마다_재현된다()
        {
            // 채널 다섯 장이 같은 배치여야 스프라이트 UV 가 모든 채널의 같은 자리를 가리킨다.
            // 같은 인수로 두 번 만들면 완전히 같아야 한다.
            var a = AtlasLayout.Create(6, 128, 128, 4);
            var b = AtlasLayout.Create(6, 128, 128, 4);

            Assert.AreEqual(a.AtlasWidth, b.AtlasWidth);
            Assert.AreEqual(a.AtlasHeight, b.AtlasHeight);
            for (int i = 0; i < 6; i++)
            {
                var ra = a.RectFor(i, 128, 128);
                var rb = b.RectFor(i, 128, 128);
                Assert.AreEqual(ra.X, rb.X, $"{i}번 X");
                Assert.AreEqual(ra.Y, rb.Y, $"{i}번 Y");
            }
        }

        [Test]
        public void 패딩_0_도_받아들인다()
        {
            var layout = AtlasLayout.Create(4, 128, 128, 0);
            Assert.AreEqual(2, layout.Columns);
            Assert.AreEqual(256, layout.AtlasWidth);
            var cell = layout.RectFor(3, 128, 128);
            Assert.AreEqual(128, cell.X);
            Assert.AreEqual(128, cell.Y);
        }

        [Test]
        public void 잘못된_인수를_안전한_값으로_바꾼다()
        {
            var layout = AtlasLayout.Create(0, 0, 0, -5);
            Assert.AreEqual(1, layout.Count);
            Assert.GreaterOrEqual(layout.AtlasWidth, 1);
            Assert.GreaterOrEqual(layout.AtlasHeight, 1);
            Assert.AreEqual(0, layout.Padding);
        }
    }
}
