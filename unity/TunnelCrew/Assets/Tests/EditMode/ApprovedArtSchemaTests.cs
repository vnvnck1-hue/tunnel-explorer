using NUnit.Framework;
using TunnelCrew.EditorTools.ArtPipeline;

namespace TunnelCrew.Tests
{
    /// <summary>
    /// 실제 승인 패키지(revision 16)가 쓰는 manifest 스키마와의 정합(§12.2).
    ///
    /// 배치 3 에서 얻은 교훈을 잠근다 — 검사기가 계약과 어긋나면 오류 31개 중 29개가
    /// 검사기 쪽 잘못이었다. 여기 값은 전부 실제 매니페스트에서 가져온 것이다.
    /// </summary>
    public class ApprovedArtSchemaTests
    {
        // ───────────────────────────── sortingLayerHint

        [Test]
        public void 아트가_쓰는_Ground_는_GroundBase_별칭이다()
        {
            var layer = ApprovedArtContract.SortingLayerForHint("Ground", out var match);
            Assert.AreEqual("GroundBase", layer);
            Assert.AreEqual(ApprovedArtContract.HintMatch.Alias, match,
                "별칭은 참고여야 한다 — 경고로 올리면 바닥 6종이 매번 리포트를 채운다");
        }

        // ───────────────────────────── 피벗으로 자산 성격 판별

        [Test]
        public void 아래_피벗은_발점으로_본다()
        {
            // 실제: 장식·전경 자산 256px 캔버스, 피벗 y=248 (아래에서 8px)
            Assert.IsTrue(ApprovedArtContract.PivotIsFootPoint(256, 248, 128));
            // 아치 512px 캔버스, 피벗 y=504
            Assert.IsTrue(ApprovedArtContract.PivotIsFootPoint(512, 504, 128));
        }

        [Test]
        public void 가운데_피벗은_발점이_아니다()
        {
            // 실제: 바닥 128px 피벗 64 · 레일 256px 피벗 128 · 벽 상단 cap 128px 피벗 64
            Assert.IsFalse(ApprovedArtContract.PivotIsFootPoint(128, 64, 128));
            Assert.IsFalse(ApprovedArtContract.PivotIsFootPoint(256, 128, 128));
            Assert.IsFalse(ApprovedArtContract.PivotIsFootPoint(1024, 128, 128));
        }

        [Test]
        public void 발점_피벗에서_시각_높이를_복원한다()
        {
            // 피벗 좌표계가 좌상단 원점·Y 아래로 증가이므로 pivotY 가 곧 발점 위 픽셀 수다.
            Assert.AreEqual(1.9375f, ApprovedArtContract.VisualHeightFromPivot(248, 128), 1e-5f);
            Assert.AreEqual(3.9375f, ApprovedArtContract.VisualHeightFromPivot(504, 128), 1e-5f);
            Assert.AreEqual(0f, ApprovedArtContract.VisualHeightFromPivot(248, 0), 1e-5f,
                "0 으로 나누지 않는다");
        }

        [Test]
        public void 복원한_높이는_매니페스트가_준_값과_거의_같다()
        {
            // 작업등: manifest visualHeightCells 1.875 · pivotPixels [128, 248] · 256 높이
            // 아트가 소수 둘째 자리에서 반올림해 적어도 복원값과 한 셀의 1/16 안에 든다.
            float derived = ApprovedArtContract.VisualHeightFromPivot(248, 128);
            Assert.AreEqual(1.875f, derived, 0.0625f);

            // 기둥: manifest 3.875 · pivot [128, 504] · 512 높이
            Assert.AreEqual(3.875f, ApprovedArtContract.VisualHeightFromPivot(504, 128), 0.0625f);
        }

        // ───────────────────────────── VFX

        [Test]
        public void VFX_자산을_ID_로_알아본다()
        {
            Assert.IsTrue(ApprovedArtContract.IsVfx("TR01-VFX-FOG-A"));
            Assert.IsTrue(ApprovedArtContract.IsVfx("TR01-VFX-RAYS-A"));
            Assert.IsFalse(ApprovedArtContract.IsVfx("TR01-FLR-BASE-A"));
            Assert.IsFalse(ApprovedArtContract.IsVfx(null));
        }

        // ───────────────────────────── 캐스터 윤곽 좌표 변환

        [Test]
        public void 캐스터_윤곽을_footprint_좌하단에서_발점_기준으로_옮긴다()
        {
            // 실제 아치: footprint 5×1, shadowCasterFootprintCells [[0,0],[5,0],[5,1],[0,1]]
            var moved = ApprovedArtContract.CasterContourToFootPoint(
                new[] { 0f, 0f, 5f, 0f, 5f, 1f, 0f, 1f }, 5);

            Assert.IsNotNull(moved);
            Assert.AreEqual(8, moved.Length);
            // 발점은 아래 변 중앙이므로 X 가 ±2.5 로 벌어진다.
            Assert.AreEqual(-2.5f, moved[0], 1e-5f);
            Assert.AreEqual(0f, moved[1], 1e-5f);
            Assert.AreEqual(2.5f, moved[2], 1e-5f);
            Assert.AreEqual(1f, moved[5], 1e-5f, "Y 는 그대로다");
        }

        [Test]
        public void 옮긴_윤곽은_footprint_사각형_규약과_일치한다()
        {
            // 사각형 윤곽을 준 자산은 내부 기본값과 결과가 같아야 한다 —
            // 그래야 아트가 값을 넣고 빼도 화면이 바뀌지 않는다.
            var moved = ApprovedArtContract.CasterContourToFootPoint(
                new[] { 0f, 0f, 1f, 0f, 1f, 1f, 0f, 1f }, 1);
            var fallback = ApprovedArtContract.FootprintContourCells(1, 1);

            Assert.AreEqual(fallback.Length, moved.Length);
            for (int i = 0; i < fallback.Length; i++)
                Assert.AreEqual(fallback[i], moved[i], 1e-5f, $"점 {i}");
        }

        [Test]
        public void 점이_모자란_윤곽은_null_로_떨어진다()
        {
            Assert.IsNull(ApprovedArtContract.CasterContourToFootPoint(new[] { 0f, 0f, 1f, 1f }, 1),
                "두 점으로는 다각형이 되지 않는다 — 런타임이 footprint 사각형을 쓴다");
            Assert.IsNull(ApprovedArtContract.CasterContourToFootPoint(null, 1));
            Assert.IsNull(ApprovedArtContract.CasterContourToFootPoint(new[] { 0f, 0f, 1f, 0f, 1f }, 1),
                "좌표 수가 홀수면 버린다");
        }

        // ───────────────────────────── 슬롯 판정 (실제 assetId)

        [Test]
        public void 실제_assetId_가_올바른_키트_슬롯으로_간다()
        {
            Assert.AreEqual(ApprovedArtContract.KitSlot.FloorBase,
                ApprovedArtContract.SlotForAssetId("TR01-FLR-BASE-D"));
            Assert.AreEqual(ApprovedArtContract.KitSlot.WallTop,
                ApprovedArtContract.SlotForAssetId("TR01-WTP-BASE-A"));
            Assert.AreEqual(ApprovedArtContract.KitSlot.WallTopRim,
                ApprovedArtContract.SlotForAssetId("TR01-WTP-RIM-A"));
            Assert.AreEqual(ApprovedArtContract.KitSlot.WallFront,
                ApprovedArtContract.SlotForAssetId("TR01-WFR-BASE-C"));
            Assert.AreEqual(ApprovedArtContract.KitSlot.ContactAo,
                ApprovedArtContract.SlotForAssetId("TR01-AO-CONTACT-A"));

            // 세트피스·소품·VFX 는 키트 슬롯이 아니다 — SetPieceCatalog 가 받는다.
            Assert.AreEqual(ApprovedArtContract.KitSlot.None,
                ApprovedArtContract.SlotForAssetId("TR01-DEC-CRATE-A"));
            Assert.AreEqual(ApprovedArtContract.KitSlot.None,
                ApprovedArtContract.SlotForAssetId("TR01-VFX-FOG-A"));
        }
    }
}
