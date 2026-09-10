using UnityEngine;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 기능명세서 §8.3·§12.1 — 지층 하나의 환경 키트. 같은 타일의 색상 변경이 아니라
    /// 서로 다른 구조 언어를 담으므로 지층마다 별도 자산을 만든다.
    ///
    /// 배열 인덱스는 <see cref="SurfaceTopologyBuilder"/> 가 고른 모듈 번호다. 비어 있는
    /// 배열은 "그 표면을 그리지 않는다"로 취급한다 — 아트가 아직 없어도 나머지가 돌아간다.
    /// </summary>
    [CreateAssetMenu(menuName = "Tunnel Crew/Environment Kit", fileName = "EnvironmentKit")]
    public sealed class EnvironmentKit : ScriptableObject
    {
        [Header("바닥 (§8.5)")]
        [Tooltip("조용한 큰 바닥 면. 매크로 블록 단위로 고른다.")]
        public Sprite[] floorBase;
        [Tooltip("벽에 인접한 바닥의 경계 블렌드. 비면 floorBase 를 그대로 쓴다.")]
        public Sprite[] floorEdge;
        [Tooltip("바닥-벽 접합 AO. 광원이 꺼져도 경계가 읽히게 한다(§15.1).")]
        public Sprite[] contactAo;

        [Header("벽 상단 (§8.6)")]
        [Tooltip("cap 기본. 프로파일 lift 만큼 화면 위로 올려 그린다.")]
        public Sprite[] wallTop;
        [Tooltip("북쪽이 열려 상단 림이 보이는 cap. 비면 wallTop 을 쓴다.")]
        public Sprite[] wallTopRim;

        [Header("벽 정면 (§8.6)")]
        [Tooltip("1셀 폭 × lift 높이. 피벗은 하단 중앙 — 셀의 남쪽 경계선에 놓인다.")]
        public Sprite[] wallFront;

        [Header("측면·모서리")]
        [Tooltip("서쪽 면이 드러난 셀에 겹치는 조각. 비면 cap 아트가 처리한다고 본다.")]
        public Sprite[] westSide;
        public Sprite[] eastSide;
        [Tooltip("볼록 모서리. 단순 회전 복제가 아니라 빛 방향을 고려한 별도 제작이 원칙이다.")]
        public Sprite[] outerCorner;
        [Tooltip("오목 모서리.")]
        public Sprite[] innerCorner;

        [Header("상시 드롭섀도 (3차 아트 요청 ②)")]
        [Tooltip("순수 알파 마스크 4장. 인덱스가 계약이다 — 0 center · 1 edge(남쪽 페이드) · 2 corner_outer(남동) · 3 corner_inner. " +
                 "4장이 다 있을 때만 쓰고, 아니면 LabWallDropShadow 가 단색 셀로 되돌아간다.")]
        public Sprite[] wallShadow;

        [Header("벽 정면 균열 (3차 아트 요청 ③ · 채굴 피드백)")]
        [Tooltip("정면 위에 얹는 투명 오버레이 3단계. 인덱스 = 타격 단계-1. 피벗 하단 중앙(정면과 같다).")]
        public Sprite[] wallCrack;

        /// <summary><see cref="wallShadow"/> 인덱스 계약.</summary>
        public const int ShadowCenter = 0, ShadowEdge = 1, ShadowCornerOuter = 2, ShadowCornerInner = 3;
        public bool HasShadowSet => wallShadow != null && wallShadow.Length == 4
            && wallShadow[0] != null && wallShadow[1] != null && wallShadow[2] != null && wallShadow[3] != null;

        /// <summary>모듈 번호로 스프라이트를 고른다. 배열이 비면 null.</summary>
        public static Sprite Pick(Sprite[] a, int module)
        {
            if (a == null || a.Length == 0) return null;
            int i = module % a.Length;
            if (i < 0) i += a.Length;
            return a[i];
        }

        /// <summary>한 셀의 cap. 상단 림 여부에 따라 배열을 바꾼다.</summary>
        public Sprite CapFor(in CellSurface s)
        {
            bool rim = (s.Surfaces & SurfaceMask.TopRim) != 0;
            var sp = rim ? Pick(wallTopRim, s.TopModule) : null;
            return sp != null ? sp : Pick(wallTop, s.TopModule);
        }

        /// <summary>한 셀의 바닥. 경계면이면 전용 배열을 먼저 본다.</summary>
        public Sprite FloorFor(in CellSurface s)
        {
            bool edge = (s.Surfaces & SurfaceMask.FloorEdge) != 0;
            var sp = edge ? Pick(floorEdge, s.FloorModule) : null;
            return sp != null ? sp : Pick(floorBase, s.FloorModule);
        }

        /// <summary>아트가 하나도 없으면 렌더러가 경고 한 번만 남기고 조용히 지나간다.</summary>
        public bool IsEmpty =>
            (floorBase == null || floorBase.Length == 0) &&
            (wallTop == null || wallTop.Length == 0) &&
            (wallFront == null || wallFront.Length == 0);
    }
}
