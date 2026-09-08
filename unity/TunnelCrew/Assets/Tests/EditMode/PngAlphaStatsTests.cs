using System.IO;
using NUnit.Framework;
using TunnelCrew.EditorTools.ArtPipeline;
using UnityEngine;

namespace TunnelCrew.Tests
{
    /// <summary>
    /// 알파 분포 검사(기능명세서 §12.2).
    ///
    /// 실제 승인 패키지에서 소품 9종이 <b>배경 키잉 실패</b> 상태로 왔다 — 알파 채널은
    /// 있었지만 알파 17~159 의 옅은 회색 막이 캔버스 전체에 남아, 화면에서는 소품 뒤에
    /// 창백한 사각형으로 보였다. 헤더만 보는 검사로는 잡히지 않는 결함이라 픽셀을 센다.
    /// </summary>
    public class PngAlphaStatsTests
    {
        string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "tc-alpha-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }

        /// <summary>알파 값을 지정해 PNG 를 하나 만든다. Unity 인코더를 거치므로 실제 파일이다.</summary>
        string WritePng(string name, int size, System.Func<int, int, byte> alphaAt)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = new Color32(200, 200, 200, alphaAt(x, y));
            tex.SetPixels32(px);
            tex.Apply();

            string path = Path.Combine(_dir, name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return path;
        }

        // ───────────────────────────── 디코딩

        [Test]
        public void 완전_불투명_이미지를_읽는다()
        {
            var s = PngAlphaStats.Read(WritePng("opaque", 32, (x, y) => 255));
            Assert.IsTrue(s.Valid, s.Error);
            Assert.AreEqual(32, s.Width);
            Assert.AreEqual(1024, s.Total);
            Assert.AreEqual(1024, s.OpaqueCount);
            Assert.AreEqual(0, s.ZeroCount);
            Assert.AreEqual(0, s.PartialCount);
        }

        [Test]
        public void 절반_투명_이미지를_읽는다()
        {
            var s = PngAlphaStats.Read(WritePng("half", 32, (x, y) => (byte)(x < 16 ? 0 : 255)));
            Assert.IsTrue(s.Valid, s.Error);
            Assert.AreEqual(512, s.ZeroCount);
            Assert.AreEqual(512, s.OpaqueCount);
            Assert.AreEqual(0.5f, s.ZeroFraction, 1e-4f);
        }

        [Test]
        public void 반투명_픽셀을_따로_센다()
        {
            // 배경 잔여물의 형태 — 알파가 0 도 255 도 아닌 값으로 넓게 깔린다.
            var s = PngAlphaStats.Read(WritePng("residue", 32, (x, y) => (byte)(x < 16 ? 90 : 255)));
            Assert.IsTrue(s.Valid, s.Error);
            Assert.AreEqual(512, s.PartialCount);
            Assert.AreEqual(512, s.OpaqueCount);
            Assert.AreEqual(0, s.ZeroCount);
            Assert.AreEqual(0.5f, s.PartialFraction, 1e-4f);
        }

        [Test]
        public void 세로_변화도_필터를_거쳐_정확히_복원된다()
        {
            // PNG 는 행마다 다른 필터를 쓴다. 세로 그라디언트는 Up·Paeth 필터를 유도한다.
            var s = PngAlphaStats.Read(WritePng("grad", 64, (x, y) => (byte)(y * 4)));
            Assert.IsTrue(s.Valid, s.Error);
            Assert.AreEqual(64 * 64, s.Total);
            // y=0 한 줄만 알파 0, y=63 은 252 이므로 255 는 없다.
            Assert.AreEqual(64, s.ZeroCount);
            Assert.AreEqual(0, s.OpaqueCount);
            Assert.AreEqual(64 * 63, s.PartialCount);
        }

        [Test]
        public void 파일이_없으면_무효로_돌려준다()
        {
            var s = PngAlphaStats.Read(Path.Combine(_dir, "nope.png"));
            Assert.IsFalse(s.Valid);
            Assert.IsNotEmpty(s.Error);
        }

        [Test]
        public void PNG_가_아니면_무효로_돌려준다()
        {
            string p = Path.Combine(_dir, "not-a-png.png");
            File.WriteAllText(p, "이건 PNG 가 아니다");
            var s = PngAlphaStats.Read(p);
            Assert.IsFalse(s.Valid);
        }

        // ───────────────────────────── 판정 규칙

        [Test]
        public void 실제_오염된_자산의_수치를_잔여물로_판정한다()
        {
            // 승인 패키지 revision 16 실측값.
            Assert.IsTrue(ApprovedArtContract.BackgroundResidueSuspect(0.253f, 0.462f), "barrel");
            Assert.IsTrue(ApprovedArtContract.BackgroundResidueSuspect(0.258f, 0.454f), "hardware");
            Assert.IsTrue(ApprovedArtContract.BackgroundResidueSuspect(0.248f, 0.424f), "bucket");
            Assert.IsTrue(ApprovedArtContract.BackgroundResidueSuspect(0.407f, 0.258f), "cable_junction (경계에서 가장 가까움)");
        }

        [Test]
        public void 깨끗한_자산은_잔여물로_보지_않는다()
        {
            // 같은 패키지의 깨끗한 자산들 — 반투명이 부드러운 가장자리 한 겹뿐이다.
            Assert.IsFalse(ApprovedArtContract.BackgroundResidueSuspect(0.517f, 0.010f), "crate");
            Assert.IsFalse(ApprovedArtContract.BackgroundResidueSuspect(0.287f, 0.032f), "rail_junction (깨끗한 쪽 최대)");
            Assert.IsFalse(ApprovedArtContract.BackgroundResidueSuspect(0.460f, 0.020f), "arch");
            Assert.IsFalse(ApprovedArtContract.BackgroundResidueSuspect(1.000f, 0.000f), "벽 타일");
        }

        [Test]
        public void 그라디언트_데칼은_반투명이_넓어도_정상이다()
        {
            // 접촉 AO 데칼은 반투명 41.4% 인데 불투명 본체가 0% 다 — 부드러운 음영이
            // 전부인 자산이라 잔여물과 형태가 다르다.
            Assert.IsFalse(ApprovedArtContract.BackgroundResidueSuspect(0.000f, 0.414f),
                "불투명 본체가 없으면 잔여물 판정에서 빼야 한다");
        }

        [Test]
        public void 판정_임계값이_실측_공백_안에_있다()
        {
            // 깨끗한 쪽 최대 3.2% · 오염된 쪽 최소 25.8%. 임계값은 그 사이여야 한다.
            Assert.Greater(ApprovedArtContract.MaxPartialAlphaFraction, 0.032f);
            Assert.Less(ApprovedArtContract.MaxPartialAlphaFraction, 0.258f);
        }
    }
}
