using UnityEngine;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 기능명세서 §12.1 — 표면 생성 규칙을 담는 데이터 자산.
    /// 아트 경로·수치·소팅 규칙을 런타임 코드에 하드코딩하지 않기 위한 경계다.
    /// 지층 키트마다 하나씩 만들고 <see cref="salt"/> 를 다르게 준다.
    /// </summary>
    [CreateAssetMenu(menuName = "Tunnel Crew/Surface Rule Set", fileName = "SurfaceRuleSet")]
    public sealed class SurfaceRuleSet : ScriptableObject
    {
        [Tooltip("바닥 매크로 변형 주기(셀). §8.5 의 2×2·3×3·4×4 반복 끊기.")]
        [Range(1, 8)] public int floorMacroCells = 4;

        [Tooltip("벽 상단·정면 변형의 구역 크기(셀). 인계서 §4-3 의 매크로 변형 배치.")]
        [Range(1, 8)] public int wallMacroCells = 3;

        [Header("모듈 변형 수 (§8.4)")]
        [Tooltip("바닥 큰 면 기본 종수. 최소 6.")]
        [Range(0, 32)] public int floorVariants = 6;
        [Tooltip("벽 상단 종수.")]
        [Range(0, 32)] public int topVariants = 6;
        [Tooltip("벽 정면 종수.")]
        [Range(0, 32)] public int frontVariants = 6;
        [Tooltip("모서리 종수. 단순 회전 복제가 아니라 별도 제작이 원칙이다(§8.6).")]
        [Range(0, 32)] public int cornerVariants = 4;

        [Tooltip("같은 시드에서 같은 배치가 나오게 하는 소금값. 키트마다 다르게 준다.")]
        public int salt;

        public SurfaceRules Rules => new SurfaceRules
        {
            WallLiftCells = 1f,   // 프로파일이 덮어쓴다 — WorldVisualProfile.BuildSurfaceRules()
            FloorMacroCells = Mathf.Max(1, floorMacroCells),
            WallMacroCells = Mathf.Max(1, wallMacroCells),
            FloorVariants = (byte)Mathf.Clamp(floorVariants, 0, 255),
            TopVariants = (byte)Mathf.Clamp(topVariants, 0, 255),
            FrontVariants = (byte)Mathf.Clamp(frontVariants, 0, 255),
            CornerVariants = (byte)Mathf.Clamp(cornerVariants, 0, 255),
            Salt = salt,
        };
    }
}
