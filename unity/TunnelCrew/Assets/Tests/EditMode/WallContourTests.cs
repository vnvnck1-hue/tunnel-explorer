using System.Collections.Generic;
using NUnit.Framework;
using TunnelCrew.Presentation.Visual;

namespace TunnelCrew.Tests
{
    /// <summary>
    /// 기능명세서 §16.1 "오클루더 윤곽 생성" 과 §7.4 의 벽 footprint 윤곽.
    ///
    /// 격자 문자열의 첫 줄이 가장 위(row = Rows-1)다. 화면 기준 방위이므로
    /// North = row+1(위), South = row-1(아래)이다.
    /// </summary>
    public class WallContourTests
    {
        /// <summary>문자 격자에서 벽 판정자를 만든다. 범위 밖은 벽이 아니다.</summary>
        static WallContourTracer.WallAt Field(out int cols, out int rows, params string[] top)
        {
            var f = ArraySolidField.Parse(top);
            cols = f.Cols; rows = f.Rows;
            // ArraySolidField 는 범위 밖을 고체로 보지만(충돌 규약), 윤곽 추적은
            // 범위 밖을 빈칸으로 봐야 맵 가장자리에서 고리가 닫힌다.
            int c0 = f.Cols, r0 = f.Rows;
            return (c, r) => c >= 0 && r >= 0 && c < c0 && r < r0 && f.IsSolid(c, r);
        }

        static List<WallContourTracer.Contour> Trace(params string[] top)
        {
            var isWall = Field(out int cols, out int rows, top);
            var list = new List<WallContourTracer.Contour>();
            WallContourTracer.Trace(isWall, cols, rows, list);
            return list;
        }

        // ───────────────────────────── 기본 형태

        [Test]
        public void 한칸_섬은_사각형_고리_하나다()
        {
            var contours = Trace(
                "...",
                ".#.",
                "...");

            Assert.AreEqual(1, contours.Count);
            Assert.AreEqual(4, contours[0].Points.Count, "일직선 정점이 정리돼 네 모서리만 남는다");
            Assert.IsTrue(contours[0].IsOuter, "벽 덩어리의 바깥 고리는 부호 면적이 양수다");
            Assert.AreEqual(2L, contours[0].DoubleSignedArea, "1×1 의 부호 면적×2 는 2 다");
        }

        [Test]
        public void 직선_벽은_안쪽_경계를_만들지_않는다()
        {
            // 가로 5칸 벽 한 줄 — 셀 사이의 변은 경계가 아니다.
            var contours = Trace(
                ".....",
                "#####",
                ".....");

            Assert.AreEqual(1, contours.Count);
            Assert.AreEqual(4, contours[0].Points.Count,
                "셀 5개를 이어도 정점은 사각형 네 개여야 한다 — 안쪽 이음새 변이 없다");
            Assert.AreEqual(2L * 5, contours[0].DoubleSignedArea);
        }

        [Test]
        public void L자_벽은_여섯_정점을_가진다()
        {
            var contours = Trace(
                "#..",
                "#..",
                "###");

            Assert.AreEqual(1, contours.Count);
            Assert.AreEqual(6, contours[0].Points.Count);
            Assert.IsTrue(contours[0].IsOuter);
        }

        [Test]
        public void 떨어진_두_덩어리는_두_고리다()
        {
            var contours = Trace(
                "#...#",
                ".....",
                "#...#");

            Assert.AreEqual(4, contours.Count);
            foreach (var c in contours) Assert.IsTrue(c.IsOuter);
        }

        // ───────────────────────────── 안쪽 고리 (방·통로)

        [Test]
        public void 벽에_둘러싸인_방은_음수_면적의_고리가_된다()
        {
            var contours = Trace(
                "#####",
                "#...#",
                "#...#",
                "#####");

            Assert.AreEqual(2, contours.Count, "바깥 테두리 고리와 방 경계 고리");

            var outer = contours.Find(c => c.IsOuter);
            var inner = contours.Find(c => !c.IsOuter);

            Assert.IsNotNull(outer, "맵 가장자리를 따르는 바깥 고리가 있어야 한다");
            Assert.IsNotNull(inner, "방을 둘러싸는 고리가 있어야 한다");

            Assert.AreEqual(2L * 5 * 4, outer.DoubleSignedArea, "5×4 사각형");
            Assert.AreEqual(-2L * 3 * 2, inner.DoubleSignedArea, "3×2 방은 시계 방향(음수)");
        }

        [Test]
        public void 방_경계_고리를_버리면_실내_벽_그림자가_사라진다()
        {
            // 절차 맵의 현실: 파낸 방을 뺀 고체 영역이 하나로 이어져 있다.
            // 음수 고리를 버리는 구현이면 이 방의 벽면이 그림자를 만들지 못한다.
            var contours = Trace(
                "#######",
                "#.....#",
                "#.###.#",
                "#.###.#",
                "#.....#",
                "#######");

            int outer = 0, inner = 0;
            foreach (var c in contours) { if (c.IsOuter) outer++; else inner++; }

            Assert.AreEqual(1, inner, "방 경계 고리 하나");
            Assert.AreEqual(2, outer, "맵 테두리 고리 + 방 안 기둥 덩어리 고리");
            Assert.AreEqual(3, contours.Count,
                "세 고리를 모두 캐스터로 만들어야 실내 벽과 기둥이 빛을 가린다");
        }

        // ───────────────────────────── 대각 접점

        [Test]
        public void 대각으로만_닿은_두_벽은_각자_고리를_가진다()
        {
            var contours = Trace(
                "#..",
                ".#.",
                "..#");

            Assert.AreEqual(3, contours.Count, "대각으로 닿은 셀은 서로 다른 덩어리다");
            foreach (var c in contours)
            {
                Assert.AreEqual(4, c.Points.Count);
                Assert.IsTrue(c.IsOuter);
            }
        }

        // ───────────────────────────── 결정성

        [Test]
        public void 같은_입력이면_고리_수_순서_정점이_모두_같다()
        {
            var a = Trace("#####", "#.#.#", "#...#", "#####");
            var b = Trace("#####", "#.#.#", "#...#", "#####");

            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].ContentHash, b[i].ContentHash, $"{i}번 고리 해시");
                Assert.AreEqual(a[i].Points.Count, b[i].Points.Count, $"{i}번 고리 정점 수");
                Assert.AreEqual(a[i].StartCol, b[i].StartCol, $"{i}번 고리 시작 열");
                Assert.AreEqual(a[i].StartRow, b[i].StartRow, $"{i}번 고리 시작 행");
                Assert.AreEqual(a[i].StartSide, b[i].StartSide, $"{i}번 고리 시작 변");

                for (int j = 0; j < a[i].Points.Count; j++)
                {
                    Assert.AreEqual(a[i].Points[j].X, b[i].Points[j].X, $"{i}/{j} X");
                    Assert.AreEqual(a[i].Points[j].Y, b[i].Points[j].Y, $"{i}/{j} Y");
                }
            }
        }

        [Test]
        public void 내용_해시는_형태에만_의존한다()
        {
            var a = Trace("...", ".#.", "...");
            var b = Trace("...", ".#.", "...");
            Assert.AreEqual(a[0].ContentHash, b[0].ContentHash,
                "같은 형태는 같은 해시여야 바뀐 캐스터만 다시 만들 수 있다");

            var c = Trace("...", "##.", "...");
            Assert.AreNotEqual(a[0].ContentHash, c[0].ContentHash, "형태가 다르면 해시도 달라야 한다");
        }

        // ───────────────────────────── 파괴 전후 (검증 조건)

        [Test]
        public void 벽_안쪽_칸을_부수면_구멍_고리가_생긴다()
        {
            // 구멍이 생기려면 사방이 벽인 내부 셀이 있어야 한다 → 3×3.
            // 3×2 덩어리에는 내부 셀이 없어 가운데를 부숴도 위쪽 변의 홈이 될 뿐이다.
            var f = ArraySolidField.Parse(
                ".....",
                ".###.",
                ".###.",
                ".###.",
                ".....");
            int cols = f.Cols, rows = f.Rows;
            WallContourTracer.WallAt isWall =
                (c, r) => c >= 0 && r >= 0 && c < cols && r < rows && f.IsSolid(c, r);

            var before = new List<WallContourTracer.Contour>();
            WallContourTracer.Trace(isWall, cols, rows, before);
            Assert.AreEqual(1, before.Count);
            Assert.AreEqual(4, before[0].Points.Count, "3×3 덩어리는 사각형이다");
            Assert.AreEqual(2L * 3 * 3, before[0].DoubleSignedArea);
            int beforeHash = before[0].ContentHash;

            // 내부 셀을 부순다 — 덩어리 안에 구멍이 생긴다.
            f.SetSolid(2, 2, false);

            var after = new List<WallContourTracer.Contour>();
            WallContourTracer.Trace(isWall, cols, rows, after);

            Assert.AreEqual(2, after.Count, "바깥 고리 + 새로 생긴 구멍 고리");
            var outer = after.Find(c => c.IsOuter);
            var hole = after.Find(c => !c.IsOuter);
            Assert.IsNotNull(hole, "부순 칸의 경계가 새 고리로 나와야 그 벽면이 빛을 가린다");
            Assert.AreEqual(-2L, hole.DoubleSignedArea, "1×1 구멍은 시계 방향(음수)");

            Assert.AreEqual(beforeHash, outer.ContentHash,
                "바깥 형태는 그대로다 — 해시가 같으면 그 캐스터를 다시 만들지 않는다");
        }

        [Test]
        public void 변에_붙은_칸을_부수면_구멍이_아니라_홈이_된다()
        {
            // 내부 셀이 없는 얇은 덩어리에서는 파괴가 바깥 윤곽을 파고든다.
            var f = ArraySolidField.Parse(
                ".....",
                ".###.",
                ".###.",
                ".....");
            int cols = f.Cols, rows = f.Rows;
            WallContourTracer.WallAt isWall =
                (c, r) => c >= 0 && r >= 0 && c < cols && r < rows && f.IsSolid(c, r);

            var before = new List<WallContourTracer.Contour>();
            WallContourTracer.Trace(isWall, cols, rows, before);
            int beforeHash = before[0].ContentHash;

            f.SetSolid(2, 2, false);   // 위쪽 변의 가운데

            var after = new List<WallContourTracer.Contour>();
            WallContourTracer.Trace(isWall, cols, rows, after);

            Assert.AreEqual(1, after.Count, "구멍이 아니라 홈이므로 고리는 하나다");
            Assert.AreEqual(8, after[0].Points.Count, "ㄷ 자로 파여 정점이 여덟이다");
            Assert.AreEqual(2L * 5, after[0].DoubleSignedArea, "6칸에서 1칸이 빠졌다");
            Assert.AreNotEqual(beforeHash, after[0].ContentHash, "형태가 바뀌면 해시도 바뀐다");
        }

        [Test]
        public void 모서리_칸을_부수면_바깥_윤곽이_바뀐다()
        {
            var f = ArraySolidField.Parse(
                ".....",
                ".###.",
                ".###.",
                ".....");
            int cols = f.Cols, rows = f.Rows;
            WallContourTracer.WallAt isWall =
                (c, r) => c >= 0 && r >= 0 && c < cols && r < rows && f.IsSolid(c, r);

            var before = new List<WallContourTracer.Contour>();
            WallContourTracer.Trace(isWall, cols, rows, before);
            int beforeHash = before[0].ContentHash;

            f.SetSolid(1, 1, false);   // 남서 모서리

            var after = new List<WallContourTracer.Contour>();
            WallContourTracer.Trace(isWall, cols, rows, after);

            Assert.AreEqual(1, after.Count, "모서리를 떼면 고리는 여전히 하나다");
            Assert.AreEqual(6, after[0].Points.Count, "L 자가 되어 정점이 여섯이다");
            Assert.AreNotEqual(beforeHash, after[0].ContentHash,
                "형태가 바뀌었으므로 해시도 바뀌어야 캐스터가 다시 만들어진다");
        }

        // ───────────────────────────── 경계 조건

        [Test]
        public void 맵을_꽉_채운_벽도_고리가_닫힌다()
        {
            var contours = Trace(
                "###",
                "###",
                "###");

            Assert.AreEqual(1, contours.Count);
            Assert.AreEqual(4, contours[0].Points.Count);
            Assert.AreEqual(2L * 9, contours[0].DoubleSignedArea);
        }

        [Test]
        public void 벽이_없으면_고리도_없다()
        {
            var contours = Trace(
                "...",
                "...",
                "...");
            Assert.AreEqual(0, contours.Count);
        }

        [Test]
        public void 한칸_폭_통로의_양쪽_벽은_각자_고리다()
        {
            var contours = Trace(
                "#.#",
                "#.#",
                "#.#");

            Assert.AreEqual(2, contours.Count);
            foreach (var c in contours)
            {
                Assert.AreEqual(4, c.Points.Count);
                Assert.AreEqual(2L * 3, c.DoubleSignedArea);
            }
        }

        [Test]
        public void 일직선_정리를_끄면_셀마다_정점이_남는다()
        {
            var isWall = Field(out int cols, out int rows,
                ".....",
                "#####",
                ".....");

            var raw = new List<WallContourTracer.Contour>();
            WallContourTracer.Trace(isWall, cols, rows, raw, simplifyCollinear: false);

            Assert.AreEqual(1, raw.Count);
            Assert.AreEqual(12, raw[0].Points.Count,
                "5칸 벽의 경계 변은 아래 5 + 오른쪽 1 + 위 5 + 왼쪽 1 = 12 개다");
        }
    }
}
