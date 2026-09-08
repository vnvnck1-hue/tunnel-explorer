using UnityEngine;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 기능명세서 §6.5 발 위치 정렬의 커널. 스프라이트 중심이 아니라 지면 접점 Y 로 정렬한다.
    ///
    /// 화면 좌표에서 Y 가 작을수록 카메라에 가깝다(아래쪽). 따라서 지면 Y 가 작은 쪽이
    /// 나중에 그려져야 하므로 order 는 <c>-groundY</c> 에 비례한다.
    ///
    /// 순수 함수만 두어 EditMode 테스트로 검증한다(§16.1). 모노비헤이비어는
    /// <see cref="FootpointSorter"/> 와 <see cref="VisualHeightAnchor"/> 쪽에 있다.
    /// </summary>
    public static class DepthSort
    {
        /// <summary>셀 하나를 몇 단계로 쪼갤지. 프로파일의 "깊이 정렬 단위"(§6.2) 기본값.</summary>
        public const int DefaultUnitsPerCell = 16;

        /// <summary>Renderer.sortingOrder 가 담을 수 있는 범위. 넘어가면 정렬이 뒤집힌다.</summary>
        public const int MinOrder = -32768;
        public const int MaxOrder = 32767;

        /// <summary>
        /// 지면 접점 Y(시뮬레이션 셀 단위) → sortingOrder.
        /// 같은 레이어 안에서 Y 가 작은(= 화면 아래 = 카메라에 가까운) 쪽이 앞에 온다.
        /// </summary>
        public static int OrderFor(float groundY, int unitsPerCell = DefaultUnitsPerCell)
        {
            if (unitsPerCell < 1) unitsPerCell = 1;
            // 음수에서도 .5 가 한쪽으로만 몰리지 않도록 Floor 가 아니라 Round 를 쓴다.
            float raw = -groundY * unitsPerCell;
            if (raw <= MinOrder) return MinOrder;
            if (raw >= MaxOrder) return MaxOrder;
            return Mathf.RoundToInt(raw);
        }

        /// <summary>여러 셀을 차지하는 구조물은 가장 앞쪽(Y 가 작은) footprint 경계로 정렬한다(§6.5).</summary>
        public static int OrderForFootprint(float groundYMin, int unitsPerCell = DefaultUnitsPerCell)
            => OrderFor(groundYMin, unitsPerCell);

        /// <summary>정렬에 쓰이는 지면 Y 가 order 로 구분될 수 있는 최소 간격.</summary>
        public static float Resolution(int unitsPerCell = DefaultUnitsPerCell)
            => 1f / Mathf.Max(1, unitsPerCell);

        /// <summary>a 가 b 보다 앞(카메라에 가까움)인가. 같은 order 면 false.</summary>
        public static bool IsInFrontOf(float groundYa, float groundYb, int unitsPerCell = DefaultUnitsPerCell)
            => OrderFor(groundYa, unitsPerCell) > OrderFor(groundYb, unitsPerCell);
    }
}
