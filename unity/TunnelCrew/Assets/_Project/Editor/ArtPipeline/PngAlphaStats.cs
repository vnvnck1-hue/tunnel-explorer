using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace TunnelCrew.EditorTools.ArtPipeline
{
    /// <summary>
    /// PNG 알파 채널의 분포를 센다. 실루엣이 실제로 잘려 있는지 보는 유일한 방법이다.
    ///
    /// <b>왜 필요한가</b> — <see cref="PngInfo"/> 는 "알파 채널이 있는가" 만 본다. 그런데
    /// 배경 제거가 실패한 자산도 알파 채널은 멀쩡히 갖고 있다. 실제 승인 패키지에서
    /// 소품 9종이 <b>알파 17~159 의 밝은 회색 잔여물</b>을 캔버스 전체에 두른 채로 왔고,
    /// 화면에서는 소품 뒤에 창백한 사각형으로 보였다. 헤더 검사로는 절대 잡히지 않는다.
    ///
    /// <c>art-production/</c> 은 <c>Assets/</c> 밖이라 <c>Texture2D</c> 를 쓸 수 없으므로
    /// 직접 디코딩한다. 알파만 필요하지만 PNG 는 행 단위 필터를 쓰기 때문에 전체 행을
    /// 복원해야 한다.
    /// </summary>
    public struct PngAlphaStats
    {
        /// <summary>디코딩에 성공했는가. 8비트 RGBA/GrayAlpha 만 지원한다.</summary>
        public bool Valid;
        public string Error;

        public int Width, Height;

        /// <summary>알파가 정확히 0 인 픽셀 수.</summary>
        public int ZeroCount;
        /// <summary>알파가 정확히 255 인 픽셀 수.</summary>
        public int OpaqueCount;
        /// <summary>알파가 그 사이인 픽셀 수 — 부드러운 가장자리이거나 배경 잔여물이다.</summary>
        public int PartialCount;

        public int Total => Width * Height;
        public float ZeroFraction => Total > 0 ? (float)ZeroCount / Total : 0f;
        public float OpaqueFraction => Total > 0 ? (float)OpaqueCount / Total : 0f;
        public float PartialFraction => Total > 0 ? (float)PartialCount / Total : 0f;

        static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        public static PngAlphaStats Read(string path)
        {
            var s = new PngAlphaStats();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                s.Error = "파일이 없다";
                return s;
            }

            byte[] bytes;
            try { bytes = File.ReadAllBytes(path); }
            catch (IOException e) { s.Error = e.Message; return s; }

            for (int i = 0; i < Signature.Length; i++)
                if (bytes.Length <= i || bytes[i] != Signature[i]) { s.Error = "PNG 이 아니다"; return s; }

            int width = 0, height = 0, bitDepth = 0, colorType = 0, interlace = 0;
            var idat = new List<byte>(bytes.Length);

            int pos = 8;
            while (pos + 8 <= bytes.Length)
            {
                int len = ReadInt32(bytes, pos);
                if (len < 0 || pos + 12 + len > bytes.Length) break;
                string type = System.Text.Encoding.ASCII.GetString(bytes, pos + 4, 4);
                int data = pos + 8;

                if (type == "IHDR" && len >= 13)
                {
                    width = ReadInt32(bytes, data);
                    height = ReadInt32(bytes, data + 4);
                    bitDepth = bytes[data + 8];
                    colorType = bytes[data + 9];
                    interlace = bytes[data + 12];
                }
                else if (type == "IDAT")
                {
                    for (int i = 0; i < len; i++) idat.Add(bytes[data + i]);
                }
                else if (type == "IEND") break;

                pos = data + len + 4;
            }

            if (width <= 0 || height <= 0) { s.Error = "IHDR 을 읽지 못했다"; return s; }
            if (bitDepth != 8) { s.Error = $"비트 심도 {bitDepth} 는 지원하지 않는다(8만)"; return s; }
            if (interlace != 0) { s.Error = "인터레이스 PNG 는 지원하지 않는다"; return s; }
            if (colorType != 6 && colorType != 4) { s.Error = $"색 타입 {colorType} 에는 알파가 없다"; return s; }
            if (idat.Count < 3) { s.Error = "IDAT 가 없다"; return s; }

            int channels = colorType == 6 ? 4 : 2;
            int stride = width * channels;

            byte[] raw;
            try { raw = Inflate(idat.ToArray(), (stride + 1) * height); }
            catch (InvalidDataException e) { s.Error = "압축 해제 실패: " + e.Message; return s; }

            if (raw.Length < (stride + 1) * height) { s.Error = "픽셀 데이터가 모자란다"; return s; }

            var prev = new byte[stride];
            var line = new byte[stride];
            int zero = 0, opaque = 0, partial = 0;
            int p = 0;

            for (int y = 0; y < height; y++)
            {
                int filter = raw[p++];
                System.Array.Copy(raw, p, line, 0, stride);
                p += stride;
                Unfilter(filter, line, prev, channels, stride);

                for (int x = channels - 1; x < stride; x += channels)
                {
                    byte a = line[x];
                    if (a == 0) zero++;
                    else if (a == 255) opaque++;
                    else partial++;
                }

                var swap = prev; prev = line; line = swap;
            }

            s.Valid = true;
            s.Width = width;
            s.Height = height;
            s.ZeroCount = zero;
            s.OpaqueCount = opaque;
            s.PartialCount = partial;
            return s;
        }

        /// <summary>
        /// zlib 스트림을 푼다. <see cref="DeflateStream"/> 는 raw deflate 만 받으므로
        /// zlib 헤더 2바이트를 건너뛴다(.NET 의 ZLibStream 은 이 런타임에 없을 수 있다).
        /// </summary>
        static byte[] Inflate(byte[] zlib, int expected)
        {
            using (var input = new MemoryStream(zlib, 2, zlib.Length - 2))
            using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream(expected > 0 ? expected : 4096))
            {
                var buffer = new byte[64 * 1024];
                int read;
                while ((read = deflate.Read(buffer, 0, buffer.Length)) > 0)
                    output.Write(buffer, 0, read);
                return output.ToArray();
            }
        }

        /// <summary>PNG 행 필터를 되돌린다(필터 타입 0~4).</summary>
        static void Unfilter(int filter, byte[] line, byte[] prev, int bpp, int stride)
        {
            switch (filter)
            {
                case 0:
                    break;
                case 1:
                    for (int i = bpp; i < stride; i++)
                        line[i] = (byte)(line[i] + line[i - bpp]);
                    break;
                case 2:
                    for (int i = 0; i < stride; i++)
                        line[i] = (byte)(line[i] + prev[i]);
                    break;
                case 3:
                    for (int i = 0; i < stride; i++)
                    {
                        int a = i >= bpp ? line[i - bpp] : 0;
                        line[i] = (byte)(line[i] + ((a + prev[i]) >> 1));
                    }
                    break;
                case 4:
                    for (int i = 0; i < stride; i++)
                    {
                        int a = i >= bpp ? line[i - bpp] : 0;
                        int b = prev[i];
                        int c = i >= bpp ? prev[i - bpp] : 0;
                        int pp = a + b - c;
                        int pa = pp > a ? pp - a : a - pp;
                        int pb = pp > b ? pp - b : b - pp;
                        int pc = pp > c ? pp - c : c - pp;
                        int pr = (pa <= pb && pa <= pc) ? a : (pb <= pc ? b : c);
                        line[i] = (byte)(line[i] + pr);
                    }
                    break;
                default:
                    break;   // 알 수 없는 필터는 그대로 둔다 — 통계가 조금 틀려도 멈추지 않는다
            }
        }

        static int ReadInt32(byte[] b, int at)
            => (b[at] << 24) | (b[at + 1] << 16) | (b[at + 2] << 8) | b[at + 3];

        public override string ToString()
            => Valid
                ? $"{Width}x{Height} 투명 {ZeroFraction:P1} 불투명 {OpaqueFraction:P1} 반투명 {PartialFraction:P1}"
                : $"(무효: {Error})";
    }
}
