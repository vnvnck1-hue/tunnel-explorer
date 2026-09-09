using NUnit.Framework;
using TunnelCrew.Presentation.Visual;

namespace TunnelCrew.Tests
{
    /// <summary>
    /// 기능명세서 §7.4 — 세트피스 그림자 윤곽은 실루엣이어야 하고 사각형이면 안 된다.
    ///
    /// 2026-09-09 회귀 방지. <c>ShadowGeometryBuilder.FromPolygon</c> 이 실루엣 정점을
    /// <c>Mathf.RoundToInt</c> 로 셀 모서리에 스냅해서, 12 점짜리 통이 네 모서리로 뭉개져
    /// 모든 세트피스 그림자가 사각형으로 나왔다. 여기서 고정하는 것은 그 스냅을 없앤 뒤의
    /// 계약이다 — 셀 안쪽 좌표가 그대로 남고, 반올림하면 같아지는 두 형태가 서로 다른 해시를
    /// 가진다.
    /// </summary>
    public class FineContourTests
    {
        static WallContourTracer.Contour Fine(params float[] xy)
        {
            var c = new WallContourTracer.Contour { FineXY = xy };
            c.ContentHash = WallContourTracer.HashOf(c);
            return c;
        }

        [Test]
        public void 실수_윤곽은_셀_안쪽_좌표를_그대로_돌려준다()
        {
            var c = Fine(-0.023f, 0f, 0.85f, 0.211f, 0.422f, 0.305f);

            Assert.AreEqual(3, c.PointCount);
            c.PointAt(1, out float x, out float y);
            Assert.AreEqual(0.85f, x, 1e-5f, "정수로 반올림되면 1 이 된다");
            Assert.AreEqual(0.211f, y, 1e-5f, "정수로 반올림되면 0 이 된다");
        }

        [Test]
        public void 반올림하면_같아지는_두_실루엣이_서로_다른_해시를_가진다()
        {
            // 둘 다 정수로 스냅하면 (0,0)·(1,0)·(1,1) 로 같아진다 — 그것이 옛 버그였다.
            var a = Fine(0f, 0f, 0.85f, 0.21f, 0.62f, 0.94f);
            var b = Fine(0f, 0f, 1.10f, -0.20f, 1.35f, 0.71f);

            Assert.AreNotEqual(a.ContentHash, b.ContentHash,
                "형태가 다르면 해시도 달라야 캐스터가 따로 만들어진다");
        }

        [Test]
        public void 같은_실수_형태는_같은_해시다()
        {
            var a = Fine(0f, 0f, 0.85f, 0.21f, 0.62f, 0.94f);
            var b = Fine(0f, 0f, 0.85f, 0.21f, 0.62f, 0.94f);

            Assert.AreEqual(a.ContentHash, b.ContentHash, "같은 형태면 캐스터를 재사용한다");
        }

        [Test]
        public void 감기_방향은_부호_면적으로_읽힌다()
        {
            // 반시계 사각형 — 양수
            float[] ccw = { 0f, 0f, 1f, 0f, 1f, 1f, 0f, 1f };
            Assert.Greater(WallContourTracer.DoubleSignedArea(ccw), 0.0);

            // 뒤집으면 음수
            float[] cw = { 0f, 1f, 1f, 1f, 1f, 0f, 0f, 0f };
            Assert.Less(WallContourTracer.DoubleSignedArea(cw), 0.0);
        }

        [Test]
        public void 실수_윤곽이_없으면_정수_정점을_읽는다()
        {
            var c = new WallContourTracer.Contour();
            c.Points.Add(new WallContourTracer.Corner(2, 3));
            c.Points.Add(new WallContourTracer.Corner(5, 3));

            Assert.AreEqual(2, c.PointCount);
            c.PointAt(0, out float x, out float y);
            Assert.AreEqual(2f, x, 1e-5f);
            Assert.AreEqual(3f, y, 1e-5f);
        }
    }
}
