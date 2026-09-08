namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 기능명세서 §10.1·§10.2 의 카메라 거리 프로파일.
    ///
    /// 프로덕션 인증 투영은 <c>ReferenceTopDown</c> 하나이며(§6.2), 이 모드들은 그 투영
    /// 안에서의 카메라 거리만 바꾼다. 줌 전환 중 월드 PPU 와 후처리의 체감 크기가 급변하지
    /// 않아야 하므로 값은 모두 <see cref="WorldVisualProfile"/> 한곳에서 나온다.
    /// </summary>
    public enum CameraViewMode
    {
        /// <summary>기본 이동·탐색. §10.1 의 10.5~12.5셀 범위.</summary>
        Base = 0,
        /// <summary>솔로·좁은 전투의 근접 줌. 캐릭터가 크게 보인다.</summary>
        Combat = 1,
        /// <summary>크루가 벌어지거나 대형 보스가 등장했을 때의 줌 아웃.</summary>
        Coop = 2,
    }
}
