using System.IO;
using NUnit.Framework;
using TunnelCrew.EditorTools.ArtPipeline;
using UnityEngine;

namespace TunnelCrew.Tests
{
    /// <summary>
    /// 기능명세서 §12.2 임포트 검사기. 임시 폴더에 가짜 패키지를 만들어 검사 전체를 돌린다.
    ///
    /// 실제 PNG 를 써야 의미가 있으므로 <see cref="Texture2D.EncodeToPNG"/> 로 파일을 만든다 —
    /// 검사기가 PNG 헤더를 직접 읽기 때문에 바이트가 실제로 맞아야 한다.
    /// </summary>
    public class ApprovedArtValidatorTests
    {
        string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "tc-art-test-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_root, "metadata"));
            Directory.CreateDirectory(Path.Combine(_root, "approved"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // ───────────────────────────── 도우미

        void WriteManifest(string json)
            => File.WriteAllText(Path.Combine(_root, "metadata", "manifest.json"), json);

        /// <summary>알파가 있는 PNG 한 장. 검사기가 헤더에서 크기와 알파를 읽는다.</summary>
        void WritePng(string relative, int w, int h, bool alpha = true)
        {
            string full = Path.Combine(_root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(full));

            var tex = new Texture2D(w, h, alpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, false);
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(80, 80, 100, alpha ? (byte)255 : (byte)255);
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(full, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        /// <summary>1×1셀 자산 하나가 규격을 지키는 manifest.</summary>
        static string GoodManifest(string extraChannels = "")
        {
            string channels = "\"albedo\": \"approved/tr01_floor_quiet_a_albedo.png\"" + extraChannels;
            return @"{
  ""packageId"": ""t"", ""revision"": 1,
  ""productionProjection"": ""ReferenceTopDown"",
  ""sourcePixelsPerCell"": 256, ""deliveryPixelsPerCell"": 128,
  ""assets"": [
    {
      ""assetId"": ""TR01-FLR-001"", ""revision"": 1, ""status"": ""approved"",
      ""footprintCells"": [1, 1], ""visualHeightCells"": 1.0,
      ""pivotPixels"": [72, 72], ""sortingLayerHint"": ""GroundBase"", ""localOrder"": 0,
      ""channels"": { " + channels + @" }
    }
  ]
}";
        }

        /// <summary>
        /// 1셀 타일의 정확한 크기. 타일링 자산에는 패딩을 넣지 않는다 —
        /// 넣으면 인접 타일 사이에 이음새가 생긴다.
        /// </summary>
        const int GoodSize = 128;

        // ───────────────────────────── 대기 상태

        [Test]
        public void approved_가_없으면_오류가_아니라_대기_상태다()
        {
            WriteManifest(@"{
  ""productionProjection"": ""ReferenceTopDown"", ""deliveryPixelsPerCell"": 128,
  ""assets"": [
    { ""assetId"": ""A"", ""status"": ""working"", ""path"": ""working/a.png"" },
    { ""assetId"": ""B"", ""status"": ""concept"", ""path"": ""concept/b.png"" }
  ]
}");
            var report = ApprovedArtValidator.Validate(_root);

            Assert.IsTrue(report.ManifestFound);
            Assert.AreEqual(0, report.ErrorCount, "진행 중 자산은 오류가 아니다");
            Assert.AreEqual(2, report.PendingCount);
            Assert.IsTrue(report.WaitingForArt, "아트 대기 상태여야 한다");
            Assert.IsFalse(report.CanImport);
        }

        [Test]
        public void 빈_assets_배열도_대기_상태다()
        {
            WriteManifest(@"{ ""productionProjection"": ""ReferenceTopDown"",
                              ""deliveryPixelsPerCell"": 128, ""assets"": [] }");
            var report = ApprovedArtValidator.Validate(_root);
            Assert.IsTrue(report.WaitingForArt);
            Assert.AreEqual(0, report.ErrorCount);
        }

        [Test]
        public void manifest_가_없으면_오류다()
        {
            var report = ApprovedArtValidator.Validate(_root);
            Assert.IsFalse(report.ManifestFound);
            Assert.Greater(report.ErrorCount, 0);
            Assert.IsFalse(report.WaitingForArt, "manifest 자체가 없는 것은 대기 상태가 아니다");
        }

        [Test]
        public void 깨진_JSON_은_오류로_보고하고_죽지_않는다()
        {
            WriteManifest("{ \"assets\": [ { \"assetId\": ");
            var report = ApprovedArtValidator.Validate(_root);
            Assert.Greater(report.ErrorCount, 0);
        }

        // ───────────────────────────── 통과 경로

        [Test]
        public void 규격을_지킨_승인본은_통과하고_임포트_가능하다()
        {
            WriteManifest(GoodManifest());
            WritePng("approved/tr01_floor_quiet_a_albedo.png", GoodSize, GoodSize);

            var report = ApprovedArtValidator.Validate(_root);

            Assert.AreEqual(0, report.ErrorCount, report.Describe());
            Assert.AreEqual(1, report.Approved.Count);
            Assert.IsTrue(report.CanImport);
            Assert.IsFalse(report.WaitingForArt);

            var asset = report.Approved[0];
            Assert.AreEqual(GoodSize, asset.Width);
            Assert.AreEqual(ApprovedArtContract.KitSlot.FloorBase, asset.Slot);
            Assert.AreEqual("GroundBase", asset.SortingLayer);
        }

        // ───────────────────────────── 검사 항목별 탐지

        [Test]
        public void 파일이_없으면_탐지한다()
        {
            WriteManifest(GoodManifest());
            // PNG 를 만들지 않는다.
            var report = ApprovedArtValidator.Validate(_root);
            Assert.Greater(report.ErrorCount, 0);
            Assert.IsFalse(report.CanImport);
        }

        [Test]
        public void 타일_자산에_패딩이_들어가면_탐지한다()
        {
            WriteManifest(GoodManifest());
            WritePng("approved/tr01_floor_quiet_a_albedo.png", 144, 144);   // 128 + 패딩 8×2

            var report = ApprovedArtValidator.Validate(_root);
            Assert.Greater(report.ErrorCount, 0);
            Assert.IsTrue(report.Describe().Contains("이음새"), report.Describe());
        }

        [Test]
        public void 캔버스가_footprint_보다_작으면_탐지한다()
        {
            WriteManifest(GoodManifest());
            WritePng("approved/tr01_floor_quiet_a_albedo.png", 64, 64);   // 1셀에 못 미친다

            var report = ApprovedArtValidator.Validate(_root);
            Assert.Greater(report.ErrorCount, 0);
            Assert.IsTrue(report.Describe().Contains("footprint"), report.Describe());
        }

        [Test]
        public void 실루엣이_필요한_자산의_알파_누락을_탐지한다()
        {
            // 세트피스(ARC)는 부분만 덮으므로 알파가 없으면 형태를 만들 수 없다.
            WriteManifest(@"{
  ""productionProjection"": ""ReferenceTopDown"", ""deliveryPixelsPerCell"": 128,
  ""assets"": [ {
      ""assetId"": ""TR01-ARC-001"", ""status"": ""approved"",
      ""footprintCells"": [1,1], ""visualHeightCells"": 1.0, ""pivotPixels"": [64,128],
      ""channels"": { ""albedo"": ""approved/tr01_arch_gate_a_albedo.png"" }
  } ] }");
            WritePng("approved/tr01_arch_gate_a_albedo.png", GoodSize, GoodSize, alpha: false);

            var report = ApprovedArtValidator.Validate(_root);
            Assert.Greater(report.ErrorCount, 0, report.Describe());
            Assert.IsTrue(report.Describe().Contains("실루엣"), report.Describe());
        }

        [Test]
        public void 꽉_채우는_타일의_알파_누락은_오류가_아니다()
        {
            // 셀을 꽉 채우는 불투명 바닥 타일은 알파 채널이 없어도 정상이다.
            // 이것을 오류로 막으면 아트에 무의미한 재수출만 강요한다.
            WriteManifest(GoodManifest());
            WritePng("approved/tr01_floor_quiet_a_albedo.png", GoodSize, GoodSize, alpha: false);

            var report = ApprovedArtValidator.Validate(_root);
            Assert.AreEqual(0, report.ErrorCount, report.Describe());
            Assert.IsTrue(report.CanImport);
        }

        [Test]
        public void 채널_캔버스_불일치를_탐지한다()
        {
            WriteManifest(GoodManifest(
                ", \"normal\": \"approved/tr01_floor_quiet_a_normal.png\""));
            WritePng("approved/tr01_floor_quiet_a_albedo.png", GoodSize, GoodSize);
            WritePng("approved/tr01_floor_quiet_a_normal.png", GoodSize / 2, GoodSize);   // 가로가 다르다

            var report = ApprovedArtValidator.Validate(_root);
            Assert.Greater(report.ErrorCount, 0);
            Assert.IsTrue(report.Describe().Contains("Albedo"), report.Describe());
        }

        [Test]
        public void Albedo_채널_누락을_탐지한다()
        {
            WriteManifest(@"{
  ""productionProjection"": ""ReferenceTopDown"", ""deliveryPixelsPerCell"": 128,
  ""assets"": [ {
      ""assetId"": ""TR01-FLR-001"", ""status"": ""approved"",
      ""footprintCells"": [1,1], ""pivotPixels"": [72,72],
      ""channels"": { ""normal"": ""approved/tr01_floor_quiet_a_normal.png"" }
  } ] }");
            WritePng("approved/tr01_floor_quiet_a_normal.png", GoodSize, GoodSize);

            var report = ApprovedArtValidator.Validate(_root);
            Assert.Greater(report.ErrorCount, 0);
            Assert.AreEqual(0, report.Approved.Count);
        }

        [Test]
        public void 파일명_규칙_위반을_탐지한다()
        {
            WriteManifest(@"{
  ""productionProjection"": ""ReferenceTopDown"", ""deliveryPixelsPerCell"": 128,
  ""assets"": [ {
      ""assetId"": ""TR01-FLR-001"", ""status"": ""approved"",
      ""footprintCells"": [1,1], ""pivotPixels"": [72,72],
      ""channels"": { ""albedo"": ""approved/TR01-Floor-A.png"" }
  } ] }");
            WritePng("approved/TR01-Floor-A.png", GoodSize, GoodSize);

            var report = ApprovedArtValidator.Validate(_root);
            Assert.Greater(report.ErrorCount, 0);
            Assert.IsTrue(report.Describe().Contains("파일명"), report.Describe());
        }

        [Test]
        public void approved_밖을_가리키는_채널을_탐지한다()
        {
            WriteManifest(@"{
  ""productionProjection"": ""ReferenceTopDown"", ""deliveryPixelsPerCell"": 128,
  ""assets"": [ {
      ""assetId"": ""TR01-ARC-001"", ""status"": ""approved"",
      ""footprintCells"": [1,1], ""pivotPixels"": [72,72],
      ""channels"": { ""albedo"": ""working/tr01_arch_gate_a_albedo.png"" }
  } ] }");
            WritePng("working/tr01_arch_gate_a_albedo.png", GoodSize, GoodSize);

            var report = ApprovedArtValidator.Validate(_root);
            Assert.Greater(report.ErrorCount, 0);
            Assert.IsTrue(report.Describe().Contains("approved/"), report.Describe());
        }

        [Test]
        public void 잘못된_deliveryPixelsPerCell_을_탐지한다()
        {
            WriteManifest(GoodManifest().Replace("\"deliveryPixelsPerCell\": 128",
                                                 "\"deliveryPixelsPerCell\": 64"));
            WritePng("approved/tr01_floor_quiet_a_albedo.png", GoodSize, GoodSize);

            var report = ApprovedArtValidator.Validate(_root);
            Assert.Greater(report.ErrorCount, 0);
            Assert.IsTrue(report.Describe().Contains("deliveryPixelsPerCell"), report.Describe());
        }

        [Test]
        public void 잘못된_투영을_탐지한다()
        {
            WriteManifest(GoodManifest().Replace("ReferenceTopDown", "Dimetric2To1"));
            WritePng("approved/tr01_floor_quiet_a_albedo.png", GoodSize, GoodSize);

            var report = ApprovedArtValidator.Validate(_root);
            Assert.Greater(report.ErrorCount, 0);
            Assert.IsTrue(report.Describe().Contains("ReferenceTopDown"), report.Describe());
        }

        [Test]
        public void 캔버스_밖_피벗을_탐지한다()
        {
            WriteManifest(GoodManifest().Replace("\"pivotPixels\": [72, 72]",
                                                 "\"pivotPixels\": [9999, 72]"));
            WritePng("approved/tr01_floor_quiet_a_albedo.png", GoodSize, GoodSize);

            var report = ApprovedArtValidator.Validate(_root);
            Assert.Greater(report.ErrorCount, 0);
            Assert.IsTrue(report.Describe().Contains("피벗"), report.Describe());
        }

        [Test]
        public void 알_수_없는_sortingLayerHint_는_경고로_알리고_기본값으로_넘긴다()
        {
            WriteManifest(GoodManifest().Replace("\"sortingLayerHint\": \"GroundBase\"",
                                                 "\"sortingLayerHint\": \"WorldStructure\""));
            WritePng("approved/tr01_floor_quiet_a_albedo.png", GoodSize, GoodSize);

            var report = ApprovedArtValidator.Validate(_root);
            // WorldStructure 는 아트 쪽 표현이고 §6.4 표에는 없다 — 매핑은 하되 알린다.
            Assert.AreEqual(0, report.ErrorCount, report.Describe());
            Assert.AreEqual("BackStructure", report.Approved[0].SortingLayer);
        }
    }
}
