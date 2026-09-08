namespace TunnelCrew.EditorTools.ArtPipeline
{
    /// <summary>
    /// 채널 아틀라스의 격자 배치를 계산한다.
    ///
    /// <b>순수 함수만 둔다.</b> Unity 타입도 파일 시스템도 쓰지 않으므로 배치 규칙 자체를
    /// EditMode 테스트로 고정할 수 있다. 아틀라스가 어긋나면 노멀·발광이 엉뚱한 타일에
    /// 붙는데, 그건 화면을 봐도 원인을 찾기 어렵다.
    ///
    /// <b>왜 격자인가</b> — 타일 자산은 폭이 모두 1셀(128px)이고 높이만 0.75~1.5셀로
    /// 다르다(§8.6). 자유 패킹이 필요할 만큼 형태가 다양하지 않고, 격자는 채널 아틀라스
    /// 다섯 장이 <b>같은 배치</b>임을 보장하기 쉽다(아트 규격 §8.1).
    /// </summary>
    public struct AtlasLayout
    {
        /// <summary>셀 하나가 담을 최대 자산 크기.</summary>
        public int CellWidth, CellHeight;
        /// <summary>셀 사이 여백. 가장자리 픽셀을 이 폭만큼 늘려 채워 블리딩을 막는다.</summary>
        public int Padding;
        public int Columns, Rows;
        public int AtlasWidth, AtlasHeight;
        public int Count;

        /// <summary>
        /// 아틀라스 안에서 자산이 놓이는 사각형. 좌표계는 <b>좌하단 원점</b>이다
        /// (Unity 스프라이트 rect 와 <c>GetPixels32</c> 의 행 순서가 그렇다).
        /// </summary>
        public struct Cell
        {
            /// <summary>자산 내용이 놓이는 위치(패딩 안쪽).</summary>
            public int X, Y;
            /// <summary>실제 자산 크기. 셀보다 작을 수 있다.</summary>
            public int Width, Height;
        }

        /// <summary>
        /// 자산 <paramref name="count"/> 개를 담는 배치를 만든다.
        /// 열 수는 정사각에 가깝게 잡아 아틀라스가 한쪽으로 길어지지 않게 한다.
        /// </summary>
        public static AtlasLayout Create(int count, int cellWidth, int cellHeight, int padding)
        {
            if (count < 1) count = 1;
            if (cellWidth < 1) cellWidth = 1;
            if (cellHeight < 1) cellHeight = 1;
            if (padding < 0) padding = 0;

            int columns = (int)System.Math.Ceiling(System.Math.Sqrt(count));
            if (columns < 1) columns = 1;
            int rows = (count + columns - 1) / columns;

            int strideX = cellWidth + padding * 2;
            int strideY = cellHeight + padding * 2;

            return new AtlasLayout
            {
                CellWidth = cellWidth,
                CellHeight = cellHeight,
                Padding = padding,
                Columns = columns,
                Rows = rows,
                AtlasWidth = columns * strideX,
                AtlasHeight = rows * strideY,
                Count = count,
            };
        }

        /// <summary>
        /// <paramref name="index"/> 번째 자산의 사각형. 자산 크기가 셀보다 작으면 셀의
        /// <b>아래쪽</b>에 붙인다 — 벽 정면처럼 발점이 아래인 자산의 기준이 흔들리지 않게 한다.
        /// </summary>
        public Cell RectFor(int index, int assetWidth, int assetHeight)
        {
            if (assetWidth > CellWidth) assetWidth = CellWidth;
            if (assetHeight > CellHeight) assetHeight = CellHeight;

            int col = index % Columns;
            int row = index / Columns;

            int strideX = CellWidth + Padding * 2;
            int strideY = CellHeight + Padding * 2;

            return new Cell
            {
                X = col * strideX + Padding,
                Y = row * strideY + Padding,
                Width = assetWidth,
                Height = assetHeight,
            };
        }

        public override string ToString() =>
            $"{AtlasWidth}×{AtlasHeight} · {Columns}×{Rows} 셀 · " +
            $"셀 {CellWidth}×{CellHeight} · 패딩 {Padding} · 자산 {Count}";
    }
}
