using System.Collections.Generic;
using NUnit.Framework;
using TunnelCrew.Presentation.Visual;

namespace TunnelCrew.Tests
{
    /// <summary>
    /// 기능명세서 §16.1 — surface topology 의 직선·코너·섬·좁은 통로 fixture 와
    /// 같은 시드 결정성.
    ///
    /// 격자 문자열의 첫 줄이 가장 위(row = Rows-1)다. 화면 기준 방위이므로
    /// North = row+1(위), South = row-1(아래)이다.
    /// </summary>
    public class SurfaceTopologyTests
    {
        static SurfaceRules Rules => SurfaceRules.Default;

        static CellSurface At(ISolidField f, int c, int r) => SurfaceTopologyBuilder.Cell(f, Rules, c, r);

        // ───────────────────────────── 기본 분류

        [Test]
        public void 빈칸은_바닥면이고_고체는_벽상단이다()
        {
            var f = ArraySolidField.Parse(
                ".....",
                "..#..",
                ".....");

            var floor = At(f, 0, 1);
            Assert.IsTrue(floor.IsFloor, "빈칸은 FloorBase 여야 한다");
            Assert.AreEqual(0, floor.HeightQ, "바닥은 시각 높이가 없다");

            var wall = At(f, 2, 1);
            Assert.IsTrue((wall.Surfaces & SurfaceMask.WallTop) != 0, "고체는 WallTop 이어야 한다");
            Assert.IsTrue(wall.HeightQ > 0, "벽은 cap lift 가 있어야 한다");
        }

        [Test]
        public void 사방이_막힌_벽은_Buried_로_컬링된다()
        {
            var f = ArraySolidField.Parse(
                "###",
                "###",
                "###");

            var inner = At(f, 1, 1);
            Assert.IsTrue((inner.Surfaces & SurfaceMask.Buried) != 0, "여덟 이웃이 모두 고체일 때만 컬링한다");
            Assert.IsTrue((inner.Surfaces & SurfaceMask.WallTop) == 0, "드러나지 않는 면은 그리지 않는다");
            Assert.IsTrue((inner.Surfaces & SurfaceMask.FrontFace) == 0);
        }

        [Test]
        public void 범위_밖은_고체로_취급한다()
        {
            var f = ArraySolidField.Parse(
                "...",
                "...",
                "...");
            Assert.IsTrue(f.IsSolid(-1, 1));
            Assert.IsTrue(f.IsSolid(3, 1));
            Assert.IsTrue(f.IsSolid(1, -1));
            Assert.IsTrue(f.IsSolid(1, 3));
        }

        // ───────────────────────────── 벽 정면과 전경 (§6.4·§6.6)

        [Test]
        public void 남쪽이_열린_벽만_정면을_만든다()
        {
            // 가운데 행이 바닥, 위아래가 벽인 동서 통로
            var f = ArraySolidField.Parse(
                "#####",
                ".....",
                "#####");

            var north = At(f, 2, 2);   // 통로의 북쪽 벽 — 남쪽(통로)이 열려 있다
            var south = At(f, 2, 0);   // 통로의 남쪽 벽 — 북쪽(통로)이 열려 있다

            Assert.IsTrue((north.Surfaces & SurfaceMask.FrontFace) != 0,
                "남쪽이 열린 벽은 남쪽 방에서 보이는 정면을 가진다");
            Assert.IsTrue((north.Surfaces & SurfaceMask.ForegroundTop) == 0,
                "북쪽이 막힌 벽의 cap 은 전경 오클루더가 아니다");

            Assert.IsTrue((south.Surfaces & SurfaceMask.ForegroundTop) != 0,
                "북쪽이 열린 벽의 cap 은 올려 그리면 그 바닥의 캐릭터와 겹친다 → 전경");
            Assert.IsTrue((south.Surfaces & SurfaceMask.TopRim) != 0);
            Assert.IsTrue((south.Surfaces & SurfaceMask.FrontFace) == 0,
                "남쪽이 막혀 있으면 정면이 드러나지 않는다");
        }

        [Test]
        public void 한칸_두께_벽은_정면과_전경cap_을_동시에_가진다()
        {
            // 남북으로 방이 둘, 사이에 한 칸 두께 벽
            var f = ArraySolidField.Parse(
                ".....",
                "#####",
                ".....");

            var wall = At(f, 2, 1);
            Assert.IsTrue((wall.Surfaces & SurfaceMask.FrontFace) != 0);
            Assert.IsTrue((wall.Surfaces & SurfaceMask.ForegroundTop) != 0);
        }

        [Test]
        public void 벽_발밑_바닥에_접촉AO_가_깔린다()
        {
            // 접점 AO 는 2026-09-10 부터 <b>네 방향</b>이다(아트 n/e/s/w 4장, 우선순위 N>S>E>W). 그래서 "열린 칸"은
            // 사방이 전부 바닥이어야 한다 — 격자 가장자리는 범위 밖이 고체(ISolidField 규약)라 AO 가 깔린다.
            var f = ArraySolidField.Parse(
                "#####",
                ".....",
                ".....",
                ".....");

            var underWall = At(f, 2, 2);   // 북쪽이 벽 → AO, 방향 N(0)
            var open = At(f, 2, 1);        // 사방이 바닥
            var bottom = At(f, 2, 0);      // 남쪽이 범위 밖(고체) → AO, 방향 S(2)

            Assert.IsTrue((underWall.Surfaces & SurfaceMask.ContactAo) != 0,
                "벽이 서 있는 발밑에는 접촉 AO 가 필요하다");
            Assert.AreEqual(0, underWall.AoDir, "북쪽 접점은 방향 0");
            Assert.IsTrue((open.Surfaces & SurfaceMask.ContactAo) == 0, "사방이 바닥이면 AO 없음");
            Assert.IsTrue((bottom.Surfaces & SurfaceMask.ContactAo) != 0, "남쪽 접점도 AO 를 받는다(4방향)");
            Assert.AreEqual(2, bottom.AoDir, "남쪽 접점은 방향 2");
        }

        // ───────────────────────────── 모서리 (§6.3)

        [Test]
        public void 섬은_네_볼록모서리를_모두_가진다()
        {
            var f = ArraySolidField.Parse(
                ".....",
                "..#..",
                ".....");

            var island = At(f, 2, 1);
            Assert.AreEqual(
                CornerMask.OuterSW | CornerMask.OuterSE | CornerMask.OuterNW | CornerMask.OuterNE,
                island.Corners);
            Assert.IsTrue((island.Surfaces & SurfaceMask.WestSide) != 0);
            Assert.IsTrue((island.Surfaces & SurfaceMask.EastSide) != 0);
        }

        [Test]
        public void 오목모서리는_두_직교이웃이_고체이고_대각만_빈칸일_때다()
        {
            //   0 1 2
            // 2 # # .
            // 1 # . .     ← (1,1) 이 빈칸
            // 0 # # #
            var f = ArraySolidField.Parse(
                "##.",
                "#..",
                "###");

            // (0,1): 북(0,2)=고체, 동(1,1)=빈칸 → InnerNE 아님
            // (1,2): 남(1,1)=빈칸 → 오목 아님, 볼록 SE/SW 후보
            // (1,0): 북(1,1)=빈칸 → ForegroundTop
            var c10 = At(f, 1, 0);
            Assert.IsTrue((c10.Surfaces & SurfaceMask.ForegroundTop) != 0);

            // (0,0): 직교 넷은 모두 고체(서·남은 범위 밖)인데 대각 NE(1,1)만 빈칸이다.
            // 직교만 보고 컬링하면 이 오목 귀퉁이에 한 조각짜리 구멍이 남는다.
            var c00 = At(f, 0, 0);
            Assert.IsTrue((c00.Surfaces & SurfaceMask.Buried) == 0,
                "대각이 열려 있으면 묻힌 셀이 아니다 — cap 귀퉁이가 드러난다");
            Assert.IsTrue((c00.Surfaces & SurfaceMask.WallTop) != 0);
            Assert.IsTrue((c00.Corners & CornerMask.InnerNE) != 0, "대각만 빈칸이면 오목 모서리다");
        }

        [Test]
        public void 좁은_통로의_양쪽_벽이_측면을_만든다()
        {
            // 남북 1칸 폭 통로
            var f = ArraySolidField.Parse(
                "#.#",
                "#.#",
                "#.#");

            var west = At(f, 0, 1);
            var east = At(f, 2, 1);
            Assert.IsTrue((west.Surfaces & SurfaceMask.EastSide) != 0, "서쪽 벽은 동쪽 면이 드러난다");
            Assert.IsTrue((east.Surfaces & SurfaceMask.WestSide) != 0, "동쪽 벽은 서쪽 면이 드러난다");
            Assert.IsTrue((west.Surfaces & SurfaceMask.FrontFace) == 0, "남북이 막혀 정면은 없다");
        }

        // ───────────────────────────── 파괴 전후 (§16.1)

        [Test]
        public void 셀_파괴가_이웃의_표면선택을_바꾼다()
        {
            var f = ArraySolidField.Parse(
                "###",
                "###",
                "###",
                "...");

            var before = At(f, 1, 2);
            Assert.IsTrue((before.Surfaces & SurfaceMask.Buried) != 0, "파괴 전에는 묻힌 셀");

            f.SetSolid(1, 1, false);   // 남쪽 이웃 하나를 부순다

            var after = At(f, 1, 2);
            Assert.IsTrue((after.Surfaces & SurfaceMask.Buried) == 0);
            Assert.IsTrue((after.Surfaces & SurfaceMask.FrontFace) != 0,
                "새로 열린 남쪽 이웃 쪽으로 정면이 생긴다");
        }

        // ───────────────────────────── 결정성 (§15.2·§16.1)

        [Test]
        public void 같은_입력이면_모듈번호까지_같다()
        {
            var a = ArraySolidField.Parse("###", "#.#", "###");
            var b = ArraySolidField.Parse("###", "#.#", "###");

            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                {
                    var x = At(a, c, r);
                    var y = At(b, c, r);
                    Assert.AreEqual(x.Surfaces, y.Surfaces, $"({c},{r}) surfaces");
                    Assert.AreEqual(x.Corners, y.Corners, $"({c},{r}) corners");
                    Assert.AreEqual(x.TopModule, y.TopModule, $"({c},{r}) topModule");
                    Assert.AreEqual(x.FrontModule, y.FrontModule, $"({c},{r}) frontModule");
                    Assert.AreEqual(x.FloorModule, y.FloorModule, $"({c},{r}) floorModule");
                }
        }

        [Test]
        public void 호출순서와_무관하게_같은_결과가_나온다()
        {
            var f = ArraySolidField.Parse(
                "#####",
                "#.#.#",
                "#...#",
                "#####");

            var whole = new CellSurface[f.Cols * f.Rows];
            SurfaceTopologyBuilder.Build(f, Rules, whole);

            // 청크를 쪼개 반대 순서로 다시 만든다
            var piecemeal = new CellSurface[f.Cols * f.Rows];
            for (int r = f.Rows - 1; r >= 0; r--)
                SurfaceTopologyBuilder.BuildRegion(f, Rules, 0, r, f.Cols, 1, piecemeal);

            for (int i = 0; i < whole.Length; i++)
            {
                Assert.AreEqual(whole[i].Surfaces, piecemeal[i].Surfaces, $"index {i}");
                Assert.AreEqual(whole[i].TopModule, piecemeal[i].TopModule, $"index {i} module");
            }
        }

        [Test]
        public void 바닥_매크로해시는_블록_안에서_같고_블록끼리_다를_수_있다()
        {
            const int macro = 4;
            uint a = SurfaceTopologyBuilder.MacroHash(0, 0, macro, 0);
            uint b = SurfaceTopologyBuilder.MacroHash(3, 3, macro, 0);
            uint c = SurfaceTopologyBuilder.MacroHash(4, 0, macro, 0);

            Assert.AreEqual(a, b, "같은 4×4 블록 안은 같은 값 — 셀마다 무늬가 튀지 않는다");
            Assert.AreNotEqual(a, c, "이웃 블록은 다른 값 — 큰 패턴 반복을 끊는다");
        }

        [Test]
        public void 매크로해시는_음수좌표에서도_블록경계가_어긋나지_않는다()
        {
            const int macro = 4;
            Assert.AreEqual(
                SurfaceTopologyBuilder.MacroHash(-4, 0, macro, 0),
                SurfaceTopologyBuilder.MacroHash(-1, 0, macro, 0));
            Assert.AreNotEqual(
                SurfaceTopologyBuilder.MacroHash(-1, 0, macro, 0),
                SurfaceTopologyBuilder.MacroHash(0, 0, macro, 0));
        }

        // ───────────────────────────── 보스 소환 벽 (4차 아트 요청 §3)

        [Test]
        public void 보스_벽_표시는_고체_셀에만_붙고_이웃에_퍼지지_않는다()
        {
            var f = ArraySolidField.Parse(
                ".....",
                ".###.",
                ".....");
            f.SetBossWall(2, 1, true);
            f.SetBossWall(0, 1, true);   // 빈칸 — 무시돼야 한다

            var boss = At(f, 2, 1);
            Assert.IsTrue((boss.Surfaces & SurfaceMask.BossWall) != 0, "표시된 고체 셀은 BossWall");
            Assert.IsTrue((boss.Surfaces & SurfaceMask.FrontFace) != 0, "보스 벽도 남쪽이 열리면 정면을 가진다");

            Assert.IsTrue((At(f, 1, 1).Surfaces & SurfaceMask.BossWall) == 0, "옆 벽은 보스 벽이 아니다");
            Assert.IsTrue((At(f, 3, 1).Surfaces & SurfaceMask.BossWall) == 0);
            Assert.IsTrue((At(f, 0, 1).Surfaces & SurfaceMask.BossWall) == 0, "빈칸에 붙인 표시는 무시된다");
            Assert.IsTrue(At(f, 0, 1).IsFloor);
        }

        // ───────────────────────────── 청크 dirty (§6.7)

        [Test]
        public void 청크_경계셀은_이웃_청크도_dirty_로_만든다()
        {
            const int cols = 48, rows = 48;
            int cc = SurfaceTopologyBuilder.ChunkCols(cols);

            var set = new HashSet<int>();
            SurfaceTopologyBuilder.DirtyChunks(8, 8, cols, rows, set);
            Assert.AreEqual(1, set.Count, "청크 안쪽 셀은 자기 청크만 다시 만든다");

            set.Clear();
            SurfaceTopologyBuilder.DirtyChunks(16, 16, cols, rows, set);
            Assert.AreEqual(4, set.Count, "청크 모퉁이 셀은 네 청크의 표면 선택에 영향을 준다");
            Assert.IsTrue(set.Contains(0 * cc + 0));
            Assert.IsTrue(set.Contains(1 * cc + 1));

            set.Clear();
            SurfaceTopologyBuilder.DirtyChunks(0, 0, cols, rows, set);
            Assert.AreEqual(1, set.Count, "맵 모서리에서는 범위 밖 청크를 넣지 않는다");
        }
    }
}
