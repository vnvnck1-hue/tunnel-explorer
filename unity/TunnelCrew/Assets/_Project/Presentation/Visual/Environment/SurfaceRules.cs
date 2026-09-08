namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 표면 생성 규칙(기능명세서 §6.3 입력 중 아트 계약에 해당하는 부분).
    /// ScriptableObject <c>SurfaceRuleSet</c> 이 이 값을 담아 런타임에 넘긴다 — 수치를
    /// 생성기 코드에 하드코딩하지 않기 위한 경계다(§12.1).
    /// </summary>
    public struct SurfaceRules
    {
        /// <summary>
        /// 벽 cap 을 화면 위로 올리는 높이(셀). 이 값은 <b>맵 전체에서 하나</b>다.
        ///
        /// 셀마다 다르게 하면 연결된 벽 덩어리의 cap 이 어긋나 seam 이 생긴다. §8.6 의
        /// 0.75~1.5셀 범위는 지층·키트 단위 선택폭이고, 셀 단위 변화가 아니다. 3셀 이상
        /// 영웅 구조물은 footprint 를 예약하는 세트피스로 따로 처리한다(§8.7).
        /// </summary>
        public float WallLiftCells;

        /// <summary>바닥 매크로 변형 주기(셀). §8.5 의 2×2·3×3·4×4 반복 끊기.</summary>
        public int FloorMacroCells;

        /// <summary>모듈 변형 수. 0 이면 그 표면의 모듈 번호는 항상 0 이다.</summary>
        public byte FloorVariants;
        public byte TopVariants;
        public byte FrontVariants;
        public byte CornerVariants;

        /// <summary>같은 시드에서 같은 배치가 나오도록 하는 소금값. 키트마다 다르게 준다.</summary>
        public int Salt;

        /// <summary>§8.6 하한(0.75셀)에 맞춘 개발 기본값.</summary>
        public static SurfaceRules Default => new SurfaceRules
        {
            WallLiftCells = 1.0f,
            FloorMacroCells = 4,
            FloorVariants = 6,
            TopVariants = 6,
            FrontVariants = 6,
            CornerVariants = 4,
            Salt = 0,
        };

        /// <summary>§8.6 의 정면 높이 범위. 이 밖의 값은 생성기가 자른다.</summary>
        public const float MinLiftCells = 0.75f;
        public const float MaxLiftCells = 1.5f;
    }
}
