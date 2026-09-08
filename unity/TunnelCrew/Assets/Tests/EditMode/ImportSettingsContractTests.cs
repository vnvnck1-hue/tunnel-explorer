using NUnit.Framework;
using TunnelCrew.EditorTools.ArtPipeline;
using UnityEditor;
using UnityEngine;

namespace TunnelCrew.Tests
{
    /// <summary>
    /// 채널별 임포트 설정 계약(기능명세서 §12.2).
    ///
    /// 색공간이 핵심이다. Normal·Emission·Mask·AO 는 데이터라서 sRGB 를 켜면 Unity 가
    /// 감마 변환을 걸어 값이 달라지는데 <b>콘솔에 아무 것도 남지 않는다</b> — 노멀 방향과
    /// AO 농도가 조용히 틀어진다. 그래서 규칙을 테스트로 잠근다.
    /// </summary>
    public class ImportSettingsContractTests
    {
        [Test]
        public void 알베도만_스프라이트다()
        {
            Assert.IsTrue(ImportExpectation.For("albedo").IsSprite);
            Assert.IsFalse(ImportExpectation.For("normal").IsSprite);
            Assert.IsFalse(ImportExpectation.For("emission").IsSprite);
            Assert.IsFalse(ImportExpectation.For("mask").IsSprite);
            Assert.IsFalse(ImportExpectation.For("ao").IsSprite);
        }

        [Test]
        public void 알베도만_sRGB_다()
        {
            Assert.IsTrue(ImportExpectation.For("albedo").SRgb, "색이므로 감마 공간이 맞다");
            foreach (var ch in new[] { "normal", "emission", "mask", "ao" })
                Assert.IsFalse(ImportExpectation.For(ch).SRgb, $"'{ch}' 는 데이터다 — sRGB 를 끈다");
        }

        [Test]
        public void 알베도만_알파를_투명도로_쓴다()
        {
            Assert.IsTrue(ImportExpectation.For("albedo").AlphaIsTransparency);
            foreach (var ch in new[] { "normal", "emission", "mask", "ao" })
                Assert.IsFalse(ImportExpectation.For(ch).AlphaIsTransparency);
        }

        [Test]
        public void 노멀은_NormalMap_타입이다()
        {
            Assert.AreEqual(TextureImporterType.NormalMap, ImportExpectation.For("normal").TextureType);
            Assert.AreEqual(TextureImporterType.Sprite, ImportExpectation.For("albedo").TextureType);
            Assert.AreEqual(TextureImporterType.Default, ImportExpectation.For("ao").TextureType);
        }

        [Test]
        public void 모든_채널이_밉맵을_끄고_Clamp_로_감싼다()
        {
            // 셀 정렬 아트다 — 밉맵은 셀 경계를 흐리고, Repeat 는 아틀라스 이웃을 물어온다.
            foreach (var ch in new[] { "albedo", "normal", "emission", "mask", "ao" })
            {
                var e = ImportExpectation.For(ch);
                Assert.IsFalse(e.Mipmaps, ch);
                Assert.AreEqual(TextureWrapMode.Clamp, e.Wrap, ch);
                Assert.AreEqual(TextureImporterCompression.Uncompressed, e.Compression, ch);
                Assert.AreEqual(FilterMode.Bilinear, e.Filter, ch);
                Assert.AreEqual(ApprovedArtContract.DeliveryPixelsPerCell, e.PixelsPerUnit, ch);
            }
        }

        [Test]
        public void 모르는_채널은_데이터_맵으로_취급한다()
        {
            // 새 채널이 sRGB 켜진 채로 조용히 들어오는 것이 가장 위험하다.
            var e = ImportExpectation.For("roughness");
            Assert.IsFalse(e.SRgb);
            Assert.AreEqual(TextureImporterType.Default, e.TextureType);

            var n = ImportExpectation.For(null);
            Assert.IsFalse(n.SRgb);
        }

        // ───────────────────────────── 적용과 되읽기

        [Test]
        public void 적용한_설정을_되읽으면_불일치가_없다()
        {
            // 계약 → 적용 → 검증의 왕복이 맞는지 실제 임포터로 확인한다.
            // 이게 어긋나면 "검증은 통과하는데 화면이 틀린" 상태가 된다.
            const string dir = "Assets/Tests_TempImport";
            System.IO.Directory.CreateDirectory(dir);
            string path = dir + "/tc_import_roundtrip.png";
            try
            {
                var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

                foreach (var ch in new[] { "albedo", "normal", "ao" })
                {
                    var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                    Assert.IsNotNull(ti, "TextureImporter 를 얻지 못했다");

                    var expected = ImportExpectation.For(ch);
                    expected.ApplyTo(ti);
                    ti.SaveAndReimport();

                    ti = AssetImporter.GetAtPath(path) as TextureImporter;
                    Assert.IsNull(expected.DescribeMismatch(ti),
                        $"'{ch}' 적용 직후인데 불일치가 보고됐다: {expected.DescribeMismatch(ti)}");
                }
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.DeleteAsset(dir);
            }
        }

        [Test]
        public void 어긋난_설정은_차이를_말해_준다()
        {
            const string dir = "Assets/Tests_TempImport";
            System.IO.Directory.CreateDirectory(dir);
            string path = dir + "/tc_import_mismatch.png";
            try
            {
                var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

                // 데이터 맵으로 넣어야 하는데 sRGB 를 켜 둔 상태를 만든다.
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                ImportExpectation.For("ao").ApplyTo(ti);
                ti.sRGBTexture = true;
                ti.SaveAndReimport();

                ti = AssetImporter.GetAtPath(path) as TextureImporter;
                string mismatch = ImportExpectation.For("ao").DescribeMismatch(ti);
                Assert.IsNotNull(mismatch, "sRGB 가 켜졌는데 통과했다 — 검증기가 무용하다");
                Assert.IsTrue(mismatch.Contains("sRGB"), "차이 항목을 지목해야 한다: " + mismatch);
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.DeleteAsset(dir);
            }
        }

        [Test]
        public void 임포터가_없으면_그렇게_말한다()
        {
            Assert.IsNotNull(ImportExpectation.For("albedo").DescribeMismatch(null));
        }

        // ───────────────────────────── 임포트 경로 규칙

        [Test]
        public void 패키지_경로를_파일명만_남겨_평평하게_옮긴다()
        {
            Assert.AreEqual(ApprovedArtImporter.ImportDir + "/tr01_dec_barrel_a_albedo.png",
                ApprovedArtImporter.ImportedPathFor("approved/albedo/decoration/tr01_dec_barrel_a_albedo.png"));
            Assert.AreEqual(ApprovedArtImporter.ImportDir + "/tr01_rail_broken_a_ao.png",
                ApprovedArtImporter.ImportedPathFor("approved/ao/linear/tr01_rail_broken_a_ao.png"));
            Assert.IsNull(ApprovedArtImporter.ImportedPathFor(null));
            Assert.IsNull(ApprovedArtImporter.ImportedPathFor(""));
        }
    }
}
