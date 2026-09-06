using System;

namespace TunnelCrew.Sim
{
    /// <summary>
    /// 두 점 사이에 벽이 없는지 본다. 원본 <c>sightClear()</c> (6420행).
    /// 적 시야·투사체·도주 방향이 모두 이걸 쓴다.
    ///
    /// <see cref="LosService"/> 의 타일 레이캐스트와는 목적이 다르다. 저쪽은
    /// 화면에 보이는 범위를 만들고, 이쪽은 "지금 이 선분이 뚫려 있나" 한 번만 묻는다.
    /// </summary>
    public static class SightUtil
    {
        public static bool IsClear(WorldGrid world, Vec2 from, Vec2 to)
        {
            double dx = to.X - from.X, dy = to.Y - from.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            // 원본은 픽셀 기준 `dist < 1` 이었다. 셀 단위로는 0.02셀.
            if (dist < SimTuning.PxCells(1.0)) return true;

            int n = Math.Min(SimTuning.SightMaxSamples,
                    Math.Max(2, (int)Math.Ceiling(dist / SimTuning.SightSampleStep)));

            for (int i = 1; i < n; i++)
            {
                double t = i / (double)n;
                int c = (int)Math.Floor(from.X + dx * t);
                int r = (int)Math.Floor(from.Y + dy * t);
                if (!world.InBounds(c, r) || world.IsSolid(c, r)) return false;
            }
            return true;
        }
    }
}
