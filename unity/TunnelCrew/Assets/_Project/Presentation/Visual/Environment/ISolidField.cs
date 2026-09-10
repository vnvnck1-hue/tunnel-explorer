namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// <see cref="SurfaceTopologyBuilder"/> 가 읽는 최소 입력(기능명세서 §6.3).
    ///
    /// <c>WorldGrid</c> 를 직접 받지 않는 이유는 두 가지다.
    /// 첫째, 표면 생성기를 UnityEngine 과 시뮬레이션 양쪽에서 떼어내 EditMode fixture 로
    /// 검증하려면(§16.1 "직선·코너·섬·좁은 통로 fixture") 손으로 만든 격자가 필요하다.
    /// 둘째, Visual Lab 의 고정 방(§12.3)은 절차 생성 없이 방 하나만 넣는다.
    /// </summary>
    public interface ISolidField
    {
        int Cols { get; }
        int Rows { get; }

        /// <summary>범위 밖은 고체로 취급한다(<c>WorldGrid.IsSolid</c> 와 같은 규약).</summary>
        bool IsSolid(int col, int row);

        /// <summary>지층 밴드 0~3. 재질·팔레트 선택에만 쓰고 높이에는 쓰지 않는다.</summary>
        byte BandAt(int col, int row);

        /// <summary>표면 시드. 원본의 <c>dec</c> 값처럼 셀 고정 변형 재료로 쓴다.</summary>
        byte SurfaceSeedAt(int col, int row);

        /// <summary>보스 소환 벽인가(4차 아트 요청 §3). 고체 셀에서만 의미가 있다. 범위 밖은 false.</summary>
        bool IsBossWallAt(int col, int row);
    }
}
