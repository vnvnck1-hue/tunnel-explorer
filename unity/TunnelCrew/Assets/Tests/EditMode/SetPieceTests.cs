using NUnit.Framework;
using TunnelCrew.EditorTools.ArtPipeline;
using TunnelCrew.Presentation.Visual;
using UnityEngine;

namespace TunnelCrew.Tests
{
    /// <summary>
    /// 세트피스 좌표 계약(기능명세서 §7.3 조명 소켓, §7.4 캐스터 윤곽, §8.7 세트피스).
    /// </summary>
    public class SetPieceTests
    {
        // ───────────────────────────── 소켓 좌표 변환

        [Test]
        public void 소켓_픽셀을_발점_기준_셀_오프셋으로_바꾼다()
        {
            // 실제 승인 아트: 작업등 256×256, 피벗 [128,248], 소켓 [128,82]
            ApprovedArtContract.SocketOffsetCells(128, 82, 128, 248, out float x, out float y);
            Assert.AreEqual(0f, x, 1e-4f, "피벗과 같은 X 면 오프셋 0");
            Assert.AreEqual(166f / 128f, y, 1e-4f, "램프 머리가 발점에서 1.3셀 위");
        }

        [Test]
        public void 피벗보다_아래에_있는_소켓은_음수_Y_가_된다()
        {
            // 경고등: 벽에 부착돼 피벗(마운트 지점)보다 발광부가 살짝 아래다.
            ApprovedArtContract.SocketOffsetCells(128, 139, 128, 128, out _, out float y);
            Assert.Less(y, 0f);
            Assert.AreEqual(-11f / 128f, y, 1e-4f);
        }

        [Test]
        public void 오른쪽에_있는_소켓은_양수_X_가_된다()
        {
            // 드릴 service 소켓: 피벗 [256,376], 소켓 [310,230]
            ApprovedArtContract.SocketOffsetCells(310, 230, 256, 376, out float x, out float y);
            Assert.AreEqual(54f / 128f, x, 1e-4f);
            Assert.AreEqual(146f / 128f, y, 1e-4f);
            Assert.Greater(x, 0f);
            Assert.Greater(y, 0f);
        }

        // ───────────────────────────── footprint 윤곽

        [Test]
        public void footprint_윤곽은_발점_아래_변_중앙을_원점으로_한다()
        {
            // 1×1 — 발점이 아래 변 중앙이므로 x 는 ±0.5, y 는 0~1
            var c = ApprovedArtContract.FootprintContourCells(1, 1);
            Assert.AreEqual(8, c.Length, "네 점 × (x,y)");
            Assert.AreEqual(-0.5f, c[0], 1e-4f);
            Assert.AreEqual(0f, c[1], 1e-4f);
            Assert.AreEqual(0.5f, c[2], 1e-4f);
            Assert.AreEqual(0f, c[3], 1e-4f);
            Assert.AreEqual(0.5f, c[4], 1e-4f);
            Assert.AreEqual(1f, c[5], 1e-4f);
            Assert.AreEqual(-0.5f, c[6], 1e-4f);
            Assert.AreEqual(1f, c[7], 1e-4f);
        }

        [Test]
        public void 여러_셀_footprint_는_폭과_깊이를_반영한다()
        {
            // 아치 5×1
            var arch = ApprovedArtContract.FootprintContourCells(5, 1);
            Assert.AreEqual(-2.5f, arch[0], 1e-4f);
            Assert.AreEqual(2.5f, arch[2], 1e-4f);
            Assert.AreEqual(1f, arch[5], 1e-4f);

            // 드릴 3×2
            var drill = ApprovedArtContract.FootprintContourCells(3, 2);
            Assert.AreEqual(-1.5f, drill[0], 1e-4f);
            Assert.AreEqual(1.5f, drill[2], 1e-4f);
            Assert.AreEqual(2f, drill[5], 1e-4f, "깊이 2셀");
        }

        [Test]
        public void footprint_윤곽은_반시계로_감긴다()
        {
            // WallContourTracer 와 같은 규약 — 구조물이 진행 방향의 왼쪽이다.
            var c = ApprovedArtContract.FootprintContourCells(3, 2);
            double area = 0;
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                area += c[i * 2] * c[j * 2 + 1] - c[j * 2] * c[i * 2 + 1];
            }
            Assert.Greater(area, 0, "반시계면 부호 면적이 양수다");
        }

        [Test]
        public void 잘못된_footprint_는_최소_1셀로_바꾼다()
        {
            var c = ApprovedArtContract.FootprintContourCells(0, -3);
            Assert.AreEqual(-0.5f, c[0], 1e-4f);
            Assert.AreEqual(1f, c[5], 1e-4f);
        }

        // ───────────────────────────── SetPieceDef 윤곽

        [Test]
        public void 윤곽이_없으면_footprint_사각형을_쓴다()
        {
            var def = new SetPieceDef
            {
                assetId = "TEST",
                footprintCells = new Vector2Int(3, 2),
                shadowContourCells = null,
            };

            var pts = new System.Collections.Generic.List<Vector2>();
            def.AppendContour(new Vector2(10f, 5f), pts);

            Assert.AreEqual(4, pts.Count);
            Assert.AreEqual(new Vector2(8.5f, 5f), pts[0]);
            Assert.AreEqual(new Vector2(11.5f, 5f), pts[1]);
            Assert.AreEqual(new Vector2(11.5f, 7f), pts[2]);
            Assert.AreEqual(new Vector2(8.5f, 7f), pts[3]);
        }

        [Test]
        public void 윤곽이_있으면_발점만큼_옮겨_쓴다()
        {
            var def = new SetPieceDef
            {
                assetId = "TEST",
                footprintCells = Vector2Int.one,
                shadowContourCells = new[] { -1f, 0f, 1f, 0f, 1f, 2f, -1f, 2f },
            };

            var pts = new System.Collections.Generic.List<Vector2>();
            def.AppendContour(new Vector2(4f, 3f), pts);

            Assert.AreEqual(4, pts.Count);
            Assert.AreEqual(new Vector2(3f, 3f), pts[0]);
            Assert.AreEqual(new Vector2(5f, 5f), pts[2]);
        }

        [Test]
        public void 점이_모자란_윤곽은_footprint_로_대체한다()
        {
            var def = new SetPieceDef
            {
                assetId = "TEST",
                footprintCells = Vector2Int.one,
                shadowContourCells = new[] { 0f, 0f },   // 점 하나뿐
            };

            var pts = new System.Collections.Generic.List<Vector2>();
            def.AppendContour(Vector2.zero, pts);
            Assert.AreEqual(4, pts.Count, "footprint 사각형으로 떨어진다");
        }

        // ───────────────────────────── 전경 판정과 접촉 반지름

        [Test]
        public void 전경_레이어나_오클루더_그룹이_있으면_페이드_대상이다()
        {
            Assert.IsTrue(new SetPieceDef { sortingLayer = VisualLayers.FrontStructure }.IsForeground);
            Assert.IsTrue(new SetPieceDef { sortingLayer = VisualLayers.WorldEntity, occluderGroup = "front-rail" }.IsForeground);
            Assert.IsFalse(new SetPieceDef { sortingLayer = VisualLayers.BackStructure }.IsForeground);
            Assert.IsFalse(new SetPieceDef { sortingLayer = VisualLayers.WorldEntity }.IsForeground);
        }

        [Test]
        public void 접촉_반지름은_지정이_없으면_footprint_폭에서_유추한다()
        {
            Assert.AreEqual(0.34f,
                new SetPieceDef { footprintCells = new Vector2Int(1, 1) }.ResolvedContactRadius, 1e-4f);
            Assert.AreEqual(3 * 0.34f,
                new SetPieceDef { footprintCells = new Vector2Int(3, 2) }.ResolvedContactRadius, 1e-4f);
            Assert.AreEqual(0.8f,
                new SetPieceDef { footprintCells = new Vector2Int(5, 1), contactShadowRadius = 0.8f }
                    .ResolvedContactRadius, 1e-4f, "지정값이 이긴다");
        }

        // ───────────────────────────── 카탈로그 조회

        [Test]
        public void 카탈로그를_ID_와_접두어로_찾는다()
        {
            var catalog = ScriptableObject.CreateInstance<SetPieceCatalog>();
            try
            {
                catalog.EditorSetEntries(new System.Collections.Generic.List<SetPieceDef>
                {
                    new SetPieceDef { assetId = "TR01-ARC-001" },
                    new SetPieceDef { assetId = "TR01-LGT-WORKLAMP-A" },
                    new SetPieceDef { assetId = "TR01-LGT-CRYSTAL-A" },
                });

                Assert.AreEqual(3, catalog.Count);
                Assert.IsNotNull(catalog.Find("TR01-ARC-001"));
                Assert.IsNull(catalog.Find("TR01-NOPE"));
                Assert.IsNull(catalog.Find(null));

                var found = new System.Collections.Generic.List<SetPieceDef>();
                catalog.FindByPrefix("TR01-LGT", found);
                Assert.AreEqual(2, found.Count, "조명 소품 둘만 걸린다");

                catalog.FindByPrefix("TR01", found);
                Assert.AreEqual(3, found.Count);
            }
            finally { Object.DestroyImmediate(catalog); }
        }
    }
}
