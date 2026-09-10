using System.Collections.Generic;
using NUnit.Framework;
using TunnelCrew.Sim;

namespace TunnelCrew.Tests
{
    /// <summary>
    /// 개정 R2 의 주항인 타일 LOS(기획서 §7.6)를 격자 하나로 고정한다. 본편·조명 랩이 같은
    /// <see cref="LosService"/> 를 쓰므로(격자 접근은 델리게이트, 2026-09-10) 여기서 깨지면 둘 다 깨진다.
    ///
    /// 검증 항목은 §7.6.2 의 규칙 그대로다: 벽 타일은 <b>표시한 뒤 차단</b>한다(벽면은 보이고 그 너머는 안 보인다),
    /// 탐색 기록은 영구 누적, 기억 농도는 반경 밖에서도 바닥값(30%)을 지킨다.
    /// </summary>
    public class LosServiceTests
    {
        // 7×5. 'x' 벽, '.' 바닥. 뷰어는 (1,2). 열 4 의 벽이 그 너머(열 5·6)를 가린다.
        static readonly string[] Room =
        {
            "xxxxxxx",
            "x......",
            "x...x..",
            "x......",
            "xxxxxxx",
        };
        const int W = 7, H = 5;

        static bool Solid(int c, int r)
        {
            if (c < 0 || r < 0 || c >= W || r >= H) return true;
            return Room[H - 1 - r][c] == 'x';
        }

        static LosService NewLos(out int[] version)
        {
            var v = new[] { 0 };
            version = v;
            return new LosService(W, H, Solid, () => v[0]);
        }

        [Test]
        public void 벽_타일은_보이고_그_너머는_안_보인다()
        {
            var los = NewLos(out _);
            los.Compute(new Vec2(1.5, 2.5));

            Assert.IsTrue(los.IsVisible(1, 2), "뷰어 칸");
            Assert.IsTrue(los.IsVisible(4, 2), "벽 타일 자체는 표시된다 — 벽면이 보여야 한다");
            Assert.IsTrue(los.IsSolid(4, 2));
            // (5,2)·(6,2)는 (4,2) 벽에 정확히 가려진 그림자 — 같은 행에서만 판정한다(대각 레이가 비껴 들어올 수 있는 칸은 제외).
            Assert.IsFalse(los.IsVisible(5, 2), "벽 바로 뒤는 차단");
            Assert.IsFalse(los.IsVisible(6, 2), "벽 너머도 차단");
        }

        [Test]
        public void 범위_밖은_고체다()
        {
            var los = NewLos(out _);
            Assert.IsTrue(los.IsSolid(-1, 0));
            Assert.IsTrue(los.IsSolid(W, 0));
            Assert.IsFalse(los.IsVisible(-1, 0), "범위 밖은 보이지 않는다(인덱스 안전)");
        }

        [Test]
        public void 탐색_기록은_누적되고_시야는_새로_계산된다()
        {
            var los = NewLos(out _);
            los.Compute(new Vec2(1.5, 2.5));
            Assert.IsTrue(los.IsExplored(2, 2));

            // 벽 너머로 순간이동 — 예전 자리는 시야에서 빠지되 탐색 기록에는 남는다.
            los.Compute(new Vec2(5.5, 1.5));
            Assert.IsTrue(los.IsExplored(2, 2), "탐색 기록은 영구다");
            Assert.IsTrue(los.IsVisible(5, 1));
        }

        [Test]
        public void 기억_농도는_반경_밖에서도_바닥값을_지킨다()
        {
            var los = NewLos(out _);
            los.Compute(new Vec2(1.5, 2.5));
            los.Compute(new Vec2(5.5, 1.5));   // 기억 중심 이동

            int near = los.MemoryValue(5, 1);
            int far = los.MemoryValue(1, 2);
            int expMax = JsMath.Round(SimTuning.LosExplored * 255);
            Assert.AreEqual(expMax, near, "기억 중심의 농도는 최대");
            Assert.GreaterOrEqual(far, JsMath.Round(expMax * SimTuning.LosMemoryFloor) - 1,
                "멀어진 탐색 지역도 30% 바닥값 — 갑자기 완전 검정으로 잘리면 안 된다(§7.6.3)");
            Assert.Less(far, near);

            var fresh = NewLos(out _);
            Assert.AreEqual(0, fresh.MemoryValue(3, 2), "탐색하지 않은 칸은 0");
        }

        [Test]
        public void 격자_버전이_오르면_같은_자리에서도_다시_계산한다()
        {
            var los = NewLos(out int[] version);
            los.Compute(new Vec2(1.5, 2.5));
            int before = 0; foreach (var b in los.Visible) if (b != 0) before++;

            // 캐시: 같은 자리·같은 버전이면 건너뛴다(값 불변)
            los.Compute(new Vec2(1.5, 2.5));
            int same = 0; foreach (var b in los.Visible) if (b != 0) same++;
            Assert.AreEqual(before, same);

            // 버전만 올려도 재계산 경로를 탄다(결과는 같은 격자라 같아야 한다 — 크래시·누락이 없는지)
            version[0]++;
            los.Compute(new Vec2(1.5, 2.5));
            int after = 0; foreach (var b in los.Visible) if (b != 0) after++;
            Assert.AreEqual(before, after);
        }

        [Test]
        public void 추가_시야원은_탐색_기록을_남기고_보스는_남기지_않는다()
        {
            var los = NewLos(out _);
            var crew = new List<VisionSource> { VisionSource.Crew(new Vec2(5.5, 1.5)) };
            los.Compute(new Vec2(1.5, 2.5), crew);
            Assert.IsTrue(los.IsVisible(5, 1), "크루가 비춘 곳은 팀이 본다");
            Assert.IsTrue(los.IsExplored(5, 1));

            // 보스 시야원: 플레이어에게 가려진 (6,2)(벽 (4,2) 뒤)를 보스가 비추면 보이기는 하되 탐색 기록은 남지 않는다.
            var los2 = NewLos(out _);
            Assert.IsFalse(Solid(6, 2));
            var boss = new List<VisionSource> { new VisionSource { Position = new Vec2(6.5, 2.5), Range = 1, Rays = 16, VisibleOnly = true } };
            los2.Compute(new Vec2(1.5, 2.5), boss);
            Assert.IsTrue(los2.IsVisible(6, 2), "보스 주변은 보인다");
            Assert.IsFalse(los2.IsExplored(6, 2), "보스 시야원은 탐색 기록을 남기지 않는다(visOnly)");
        }
    }
}
