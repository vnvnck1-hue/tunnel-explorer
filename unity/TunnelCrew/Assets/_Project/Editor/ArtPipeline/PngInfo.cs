namespace TunnelCrew.EditorTools.ArtPipeline
{
    /// <summary>
    /// PNG 헤더만 읽어 크기·색 타입·알파 여부를 알아낸다.
    ///
    /// <b>왜 Texture2D 로 불러오지 않는가</b> — <c>art-production/</c> 은 저장소 루트에 있고
    /// <c>Assets/</c> 밖이다. Unity 자산이 아니므로 임포터도 <c>Texture2D</c> 도 없다. 검사는
    /// 임포트 <b>전에</b> 끝나야 하고, 잘못된 파일을 프로젝트에 들여놓지 않는 것이 목적이다.
    ///
    /// 헤더만 보므로 4KB 짜리 읽기로 충분하고, 수백 장을 검사해도 빠르다.
    /// </summary>
    public struct PngInfo
    {
        public bool Valid;
        public int Width;
        public int Height;
        public int BitDepth;
        /// <summary>PNG 색 타입: 0 회색, 2 트루컬러, 3 팔레트, 4 회색+알파, 6 트루컬러+알파.</summary>
        public int ColorType;
        /// <summary>알파 채널이 있는가. 팔레트(3)는 tRNS 청크가 있으면 참이다.</summary>
        public bool HasAlpha;
        /// <summary>sRGB 청크가 있는가. 데이터 맵에는 없어야 한다.</summary>
        public bool HasSrgbChunk;
        public string Error;

        static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        /// <summary>파일에서 읽는다. 파일이 없거나 PNG 가 아니면 <see cref="Valid"/> 가 false 다.</summary>
        public static PngInfo Read(string path)
        {
            var info = new PngInfo();
            if (!System.IO.File.Exists(path))
            {
                info.Error = "파일이 없다";
                return info;
            }

            try
            {
                using var fs = System.IO.File.OpenRead(path);
                return Read(fs);
            }
            catch (System.Exception e)
            {
                info.Error = e.Message;
                return info;
            }
        }

        static PngInfo Read(System.IO.Stream stream)
        {
            var info = new PngInfo();

            var sig = new byte[8];
            if (stream.Read(sig, 0, 8) != 8) { info.Error = "8바이트 서명을 읽지 못했다"; return info; }
            for (int i = 0; i < 8; i++)
                if (sig[i] != Signature[i]) { info.Error = "PNG 서명이 아니다"; return info; }

            var len = new byte[4];
            var type = new byte[4];

            while (true)
            {
                if (stream.Read(len, 0, 4) != 4) break;
                if (stream.Read(type, 0, 4) != 4) break;

                int length = (len[0] << 24) | (len[1] << 16) | (len[2] << 8) | len[3];
                string name = System.Text.Encoding.ASCII.GetString(type);

                if (name == "IHDR")
                {
                    var ihdr = new byte[13];
                    if (stream.Read(ihdr, 0, 13) != 13) { info.Error = "IHDR 이 짧다"; return info; }
                    info.Width = (ihdr[0] << 24) | (ihdr[1] << 16) | (ihdr[2] << 8) | ihdr[3];
                    info.Height = (ihdr[4] << 24) | (ihdr[5] << 16) | (ihdr[6] << 8) | ihdr[7];
                    info.BitDepth = ihdr[8];
                    info.ColorType = ihdr[9];
                    info.HasAlpha = info.ColorType == 4 || info.ColorType == 6;
                    info.Valid = true;
                    Skip(stream, length - 13 + 4);          // 남은 데이터 + CRC
                    continue;
                }

                if (name == "sRGB") { info.HasSrgbChunk = true; Skip(stream, length + 4); continue; }
                if (name == "tRNS") { info.HasAlpha = true; Skip(stream, length + 4); continue; }
                if (name == "IDAT" || name == "IEND") break;   // 헤더 구간이 끝났다

                Skip(stream, length + 4);
            }

            if (!info.Valid && info.Error == null) info.Error = "IHDR 을 찾지 못했다";
            return info;
        }

        static void Skip(System.IO.Stream s, int count)
        {
            if (count <= 0) return;
            if (s.CanSeek) { s.Seek(count, System.IO.SeekOrigin.Current); return; }
            var buf = new byte[count];
            s.Read(buf, 0, count);
        }

        public override string ToString() =>
            Valid ? $"{Width}×{Height} · bit {BitDepth} · colorType {ColorType}" +
                    $"{(HasAlpha ? " · 알파 있음" : " · 알파 없음")}"
                  : $"읽기 실패: {Error}";
    }
}
