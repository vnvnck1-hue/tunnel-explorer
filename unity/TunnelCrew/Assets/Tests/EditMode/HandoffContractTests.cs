using NUnit.Framework;
using TunnelCrew.EditorTools.ArtPipeline;
using TunnelCrew.Presentation.Visual;
using UnityEditor;
using UnityEngine;

namespace TunnelCrew.Tests
{
    /// <summary>
    /// 아트 → Unity 인계서 계약
    /// (`art-production/test-room-v01/process/unity-handoff.md`).
    ///
    /// 기능명세서 §14 단계 B 가 이 문서를 "Claude 의 실제 Unity 조립 기준" 으로 지목한다.
    /// 여기 있는 항목은 전부 그 문서의 조항이며, 어긋나면 화면에 바로 드러난다.
    /// </summary>
    public class HandoffContractTests
    {
        // ───────────────────────────── §2 스프라이트 메시 Full Rect

        [Test]
        public void 알베도는_Full_Rect_메시로_임포트한다()
        {
            // 인계서 §2 — "알파 트리밍으로 피벗·채널 정렬을 바꾸지 않는다".
            // Unity 기본값 Tight 는 알파를 따라 메시를 깎는다.
            Assert.AreEqual(SpriteMeshType.FullRect, ImportExpectation.For("albedo").MeshType);
        }

        // ───────────────────────────── §4-3 벽 상단 매크로 변형

        [Test]
        public void 같은_구역_안의_벽_상단은_같은_변형을_쓴다()
        {
            // 인계서 §4-3 — "구역별 매크로 변형으로 배치해 한 화면에서 모든 패턴을
            // 균등 반복하지 않는다". 셀별 해시로 뽑으면 이 단정이 깨진다.
            var rules = SurfaceRules.Default;
            Assert.Greater(rules.WallMacroCells, 1, "구역 크기가 1이면 셀별 해시와 같다");

            int m = rules.WallMacroCells;
            var field = MakeWallRow(cols: m * 4 + 2, rows: 6);
            var surfaces = new CellSurface[field.Cols * field.Rows];
            SurfaceTopologyBuilder.Build(field, rules, surfaces);

            // 같은 구역(열 0..m-1)의 벽 상단은 모두 같은 모듈이어야 한다.
            int first = -1;
            for (int c = 0; c < m; c++)
            {
                var s = surfaces[3 * field.Cols + c];   // 벽 행
                if (!((s.Surfaces & SurfaceMask.WallTop) != 0)) continue;
                if (first < 0) first = s.TopModule;
                Assert.AreEqual(first, s.TopModule, $"열 {c} 가 같은 구역인데 다른 변형이다");
            }
        }

        [Test]
        public void 다른_구역은_변형이_달라질_수_있다()
        {
            var rules = SurfaceRules.Default;
            int m = rules.WallMacroCells;
            var field = MakeWallRow(cols: m * 8 + 2, rows: 6);
            var surfaces = new CellSurface[field.Cols * field.Rows];
            SurfaceTopologyBuilder.Build(field, rules, surfaces);

            // 구역이 여러 개면 최소 두 가지 변형이 나와야 한다 —
            // 전부 같으면 매크로 구역이 너무 커서 벽이 통째로 한 패턴이 된다.
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int c = 1; c < field.Cols - 1; c++)
            {
                var s = surfaces[3 * field.Cols + c];
                if (((s.Surfaces & SurfaceMask.WallTop) != 0)) seen.Add(s.TopModule);
            }
            Assert.Greater(seen.Count, 1, "구역이 8개인데 변형이 하나뿐이다");
        }

        [Test]
        public void 상단과_정면은_서로_다른_변형_흐름을_쓴다()
        {
            // 같은 구역에서 상단과 정면이 항상 같은 인덱스면 조합이 단조로워진다.
            var rules = SurfaceRules.Default;
            var field = MakeWallRow(cols: rules.WallMacroCells * 8 + 2, rows: 6);
            var surfaces = new CellSurface[field.Cols * field.Rows];
            SurfaceTopologyBuilder.Build(field, rules, surfaces);

            bool anyDifferent = false;
            for (int c = 1; c < field.Cols - 1 && !anyDifferent; c++)
            {
                var s = surfaces[3 * field.Cols + c];
                if (((s.Surfaces & SurfaceMask.WallTop) != 0) && s.TopModule != s.FrontModule) anyDifferent = true;
            }
            Assert.IsTrue(anyDifferent, "상단과 정면 모듈이 모든 칸에서 같다");
        }

        /// <summary>가로로 벽 한 줄(행 3)이 있는 판. 위쪽이 열려 상단 cap 이 생긴다.</summary>
        static ArraySolidField MakeWallRow(int cols, int rows)
        {
            var lines = new string[rows];
            for (int r = 0; r < rows; r++)
            {
                // 배열 첫 줄이 화면 위(row = rows-1)다. 행 3 만 벽으로 만든다.
                int row = rows - 1 - r;
                lines[r] = new string(row == 3 ? '#' : '.', cols);
            }
            return ArraySolidField.Parse(lines);
        }

        // ───────────────────────────── §3 연결 포트

        [Test]
        public void 네_방향만_연결_변으로_인정한다()
        {
            foreach (var e in new[] { "north", "south", "east", "west" })
                Assert.IsTrue(ApprovedArtValidator.IsKnownEdge(e), e);
            foreach (var e in new[] { "up", "North", "", null })
                Assert.IsFalse(ApprovedArtValidator.IsKnownEdge(e), e ?? "null");
        }

        [Test]
        public void 연결_포트는_해당_변에_붙어_있어야_한다()
        {
            // 좌표계는 피벗과 같다 — 좌상단 원점, Y 아래로 증가. north 는 y=0 쪽이다.
            // 실제 레일 곡선: north [128,0], east [256,128] (캔버스 256×256)
            Assert.IsTrue(ApprovedArtValidator.PortSitsOnEdge("north", 128, 0, 256, 256));
            Assert.IsTrue(ApprovedArtValidator.PortSitsOnEdge("east", 256, 128, 256, 256));
            Assert.IsTrue(ApprovedArtValidator.PortSitsOnEdge("south", 128, 256, 256, 256));
            Assert.IsTrue(ApprovedArtValidator.PortSitsOnEdge("west", 0, 128, 256, 256));

            // 변 이름과 좌표가 어긋나면 이어붙일 때 틀어진다.
            Assert.IsFalse(ApprovedArtValidator.PortSitsOnEdge("north", 128, 128, 256, 256));
            Assert.IsFalse(ApprovedArtValidator.PortSitsOnEdge("west", 128, 128, 256, 256));
        }

        // ───────────────────────────── §2 피벗 교차 확인

        [Test]
        public void pivotNormalized_는_하단_원점이라_pivotPixels_와_Y_가_뒤집힌다()
        {
            // 실제 드릴: 캔버스 512×384 · pivotPixels [256,376] · pivotNormalized [0.5, 0.0208333]
            ApprovedArtContract.PivotToUnity(256, 376, 512, 384, out float u, out float v);
            Assert.AreEqual(0.5f, u, ApprovedArtValidator.PivotAgreement);
            Assert.AreEqual(0.020833333f, v, ApprovedArtValidator.PivotAgreement);
        }

        [Test]
        public void 피벗_허용_오차는_한_픽셀보다_훨씬_작다()
        {
            // 128px 셀에서 오차가 1픽셀에 달하면 발점이 눈에 보이게 밀린다.
            Assert.Less(ApprovedArtValidator.PivotAgreement,
                1f / ApprovedArtContract.DeliveryPixelsPerCell);
        }
    }
}
