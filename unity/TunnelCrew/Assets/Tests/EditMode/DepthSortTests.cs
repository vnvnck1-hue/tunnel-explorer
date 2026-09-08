using NUnit.Framework;
using TunnelCrew.Presentation.Visual;

namespace TunnelCrew.Tests
{
    /// <summary>
    /// 기능명세서 §16.1 "footprint 기반 sorting key" 와 §15.2 "발 위치 정렬이 모든 이동
    /// 방향에서 뒤집히지 않는다".
    ///
    /// 화면 좌표에서 Y 가 작을수록 카메라에 가깝다. 따라서 지면 Y 가 작은 쪽이 앞이다.
    /// </summary>
    public class DepthSortTests
    {
        [Test]
        public void 지면Y가_작은_쪽이_앞에_온다()
        {
            Assert.Greater(DepthSort.OrderFor(3f), DepthSort.OrderFor(4f),
                "화면 아래(작은 Y)가 카메라에 가깝다");
            Assert.IsTrue(DepthSort.IsInFrontOf(3f, 4f));
            Assert.IsFalse(DepthSort.IsInFrontOf(4f, 3f));
        }

        [Test]
        public void 캐릭터가_벽_남쪽에_서면_벽보다_앞이다()
        {
            // 벽 셀 r=10 의 지면선은 y=10. 캐릭터가 남쪽 바닥 y=9.5 에 서면 앞에 보인다.
            Assert.IsTrue(DepthSort.IsInFrontOf(9.5f, 10f));
            // 북쪽 바닥 y=11 에 서면 벽에 가려진다.
            Assert.IsFalse(DepthSort.IsInFrontOf(11f, 10f));
        }

        [Test]
        public void 여러셀_구조물은_가장_앞쪽_footprint_경계로_정렬한다()
        {
            // 3셀 높이 구조물의 중심이 y=10 이면 앞쪽 경계는 y=9
            int structure = DepthSort.OrderForFootprint(9f);
            // 그 앞(y=8.5)에 선 캐릭터는 구조물보다 앞이어야 한다
            Assert.Greater(DepthSort.OrderFor(8.5f), structure);
            // 구조물 footprint 안(y=9.5)의 캐릭터는 뒤로 간다
            Assert.Less(DepthSort.OrderFor(9.5f), structure);
        }

        [Test]
        public void 셀_안에서도_구분되는_해상도를_가진다()
        {
            Assert.AreNotEqual(DepthSort.OrderFor(10f), DepthSort.OrderFor(10.1f),
                "한 셀 안에서 두 캐릭터가 겹칠 때도 앞뒤가 결정돼야 한다");
            Assert.AreEqual(1f / DepthSort.DefaultUnitsPerCell, DepthSort.Resolution(), 1e-6f);
        }

        [Test]
        public void order_는_short_범위를_넘지_않는다()
        {
            Assert.AreEqual(DepthSort.MinOrder, DepthSort.OrderFor(1e9f));
            Assert.AreEqual(DepthSort.MaxOrder, DepthSort.OrderFor(-1e9f));
            // 실제 맵 크기(수백 셀)에서는 자르기가 일어나지 않아야 한다
            int far = DepthSort.OrderFor(2000f);
            Assert.Greater(far, DepthSort.MinOrder, "2000셀까지는 자르기 없이 표현된다");
        }

        [Test]
        public void 정렬_단위가_커지면_해상도가_올라간다()
        {
            Assert.AreNotEqual(DepthSort.OrderFor(10f, 64), DepthSort.OrderFor(10.02f, 64));
            Assert.AreEqual(DepthSort.OrderFor(10f, 4), DepthSort.OrderFor(10.02f, 4),
                "낮은 단위에서는 같은 칸으로 뭉친다");
        }

        [Test]
        public void 남북_이동_중_같은_Y에서만_동일_order_가_나온다()
        {
            // 캐릭터가 y=5 → 15 로 지나가는 동안 order 는 단조 감소해야 한다
            int prev = int.MaxValue;
            for (float y = 5f; y <= 15f; y += 0.05f)
            {
                int o = DepthSort.OrderFor(y);
                Assert.LessOrEqual(o, prev, $"y={y} 에서 정렬이 뒤집혔다");
                prev = o;
            }
        }
    }
}
