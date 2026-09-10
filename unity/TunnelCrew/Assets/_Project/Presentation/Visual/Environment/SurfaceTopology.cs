namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 한 셀에서 만들어지는 시각 표면(기능명세서 §6.3 출력).
    ///
    /// 방위는 <b>화면 기준</b>이다. <c>ReferenceTopDown</c> 에서 시뮬레이션 Y 는 화면 위로
    /// 증가하므로 North = r+1(화면 위), South = r-1(화면 아래)이다.
    /// </summary>
    [System.Flags]
    public enum SurfaceMask : ushort
    {
        None = 0,

        /// <summary>빈칸 — 바닥 중심면.</summary>
        FloorBase = 1 << 0,
        /// <summary>빈칸 — 벽에 인접해 경계 블렌드가 필요하다.</summary>
        FloorEdge = 1 << 1,
        /// <summary>빈칸 + 북쪽이 고체 — 바닥-벽 접합 AO(§6.3, §7.4-1).</summary>
        ContactAo = 1 << 2,

        /// <summary>고체 — 벽 상단 cap. 프로파일의 lift 만큼 화면 위로 올려 그린다.</summary>
        WallTop = 1 << 3,
        /// <summary>
        /// 고체 + 북쪽이 빈칸 — 올려 그린 cap 이 북쪽 바닥의 캐릭터와 겹친다.
        /// 즉 이 cap 은 전경 오클루더이며 <c>FrontStructure</c> 레이어로 간다(§6.4, §6.6).
        /// </summary>
        ForegroundTop = 1 << 4,
        /// <summary>고체 + 북쪽이 빈칸 — cap 상단의 얇은 림(§8.6).</summary>
        TopRim = 1 << 5,

        /// <summary>
        /// 고체 + 남쪽이 빈칸 — 셀의 남쪽 경계선에 발점을 둔 벽 정면.
        /// 남쪽 방에서 보이는 "북쪽 벽 정면"이라 <c>BackStructure</c> 레이어로 간다(§6.4).
        /// </summary>
        FrontFace = 1 << 6,
        /// <summary>고체 + 서쪽이 빈칸 — 서쪽 측면.</summary>
        WestSide = 1 << 7,
        /// <summary>고체 + 동쪽이 빈칸 — 동쪽 측면.</summary>
        EastSide = 1 << 8,

        /// <summary>고체 + 사방이 고체 — 화면에 드러나지 않는 벽 내부. 컬링 대상.</summary>
        Buried = 1 << 9,

        /// <summary>그림자 캐스터 윤곽에 기여하는 고체 경계(§7.4).</summary>
        ShadowEdge = 1 << 10,

        /// <summary>
        /// 고체 + 보스 소환 벽(원본 INF.bossWallCells). cap·정면을 키트의 전용 보스 벽 아트로 갈아 끼운다
        /// (4차 아트 요청 §3). 게임플레이 정보라 지층 키트와 무관하게 항상 같은 것으로 읽혀야 한다.
        /// </summary>
        BossWall = 1 << 11,
    }

    /// <summary>
    /// 모서리(§6.3 "안쪽·바깥쪽 모서리"). 고체 셀에서만 채운다.
    /// Outer = 볼록(두 직교 이웃이 모두 빈칸), Inner = 오목(두 직교 이웃이 고체이고 대각만 빈칸).
    /// </summary>
    [System.Flags]
    public enum CornerMask : byte
    {
        None = 0,
        OuterSW = 1 << 0,
        OuterSE = 1 << 1,
        OuterNW = 1 << 2,
        OuterNE = 1 << 3,
        InnerSW = 1 << 4,
        InnerSE = 1 << 5,
        InnerNW = 1 << 6,
        InnerNE = 1 << 7,
    }

    /// <summary>셀 하나의 표면 선택 결과. 값 타입이라 배열 하나로 청크를 담는다.</summary>
    public struct CellSurface
    {
        public SurfaceMask Surfaces;
        public CornerMask Corners;

        /// <summary>벽 정면 높이 / cap lift. 1/16셀 단위(§8.6 0.75~1.5셀 = 12~24).</summary>
        public byte HeightQ;

        /// <summary>지층 밴드(0~3). 재질·팔레트 선택에만 쓴다.</summary>
        public byte Band;

        /// <summary>결정적으로 고른 모듈 번호. 디버그 오버레이가 그대로 표시한다(§12.4).</summary>
        public byte FloorModule;
        public byte TopModule;
        public byte FrontModule;
        public byte CornerModule;

        /// <summary>
        /// 접촉 AO 의 방향. 0=N · 1=E · 2=S · 3=W. <see cref="EnvironmentKit.contactAo"/> 의
        /// 배열 순서가 이 인덱스와 같은 것이 계약이다(BuildReferenceEnvironmentKit 의
        /// ContactAoByDirection 참고).
        /// </summary>
        public byte AoDir;

        public bool IsSolid => (Surfaces & (SurfaceMask.WallTop | SurfaceMask.Buried)) != 0;
        public bool IsFloor => (Surfaces & SurfaceMask.FloorBase) != 0;
        public bool IsForegroundOccluder => (Surfaces & SurfaceMask.ForegroundTop) != 0;

        /// <summary>cap lift 를 셀 단위 실수로.</summary>
        public float Height => HeightQ / 16f;
    }
}
