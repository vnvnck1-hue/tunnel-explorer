using System;

namespace TunnelCrew.Sim
{
    /// <summary>
    /// 원본 <c>collide(o, rad)</c> (6166~6183행) 을 셀 단위로 옮긴 것.
    ///
    /// 계획 D5 는 Physics2D 하이브리드로 개정됐지만, **이 밀어내기 자체는 Sim 에 남긴다.**
    /// 벽이 실시간으로 생겨나고 사라지는 게임이라 "이미 벽 안에 있는 경우"를 처리해야 하고,
    /// 그 규칙(7×7 이웃에서 가장 가까운 빈칸으로 35% 보간)은 이 게임 특유의 안전장치다.
    /// Physics2D 는 M1 이후 렌더·트리거·투사체 쿼리에 쓴다.
    /// </summary>
    public static class CollisionSystem
    {
        /// <summary>원-AABB 밀어내기 3회 반복 + 경계 클램프 + 벽 속 탈출.</summary>
        public static void Resolve(WorldGrid world, ref Vec2 pos, double radius)
        {
            for (int it = 0; it < 3; it++)
            {
                var (c0, r0) = WorldGrid.ToCell(pos);
                bool moved = false;

                for (int dr = -1; dr <= 1; dr++)
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        int c = c0 + dc, r = r0 + dr;
                        if (!world.IsSolid(c, r)) continue;

                        // 셀 (c,r) 은 [c, c+1] × [r, r+1] 사각형이다.
                        double nx = Math.Max(c, Math.Min(pos.X, c + 1.0));
                        double ny = Math.Max(r, Math.Min(pos.Y, r + 1.0));
                        double dx = pos.X - nx, dy = pos.Y - ny;
                        double d = Math.Sqrt(dx * dx + dy * dy);

                        if (d < radius)
                        {
                            if (d < 1e-6) { dx = 0; dy = -1; d = 1; }   // 원본: 정확히 겹치면 위로 뺀다
                            pos.X += dx / d * (radius - d);
                            pos.Y += dy / d * (radius - d);
                            moved = true;
                        }
                    }

                if (!moved) break;
            }

            // 월드 경계. 원본은 rad+2 픽셀 여유를 뒀다.
            double pad = radius + SimTuning.PxCells(2.0);
            pos.X = JsMath.Clamp(pos.X, pad, world.Cols - pad);
            pos.Y = JsMath.Clamp(pos.Y, pad, world.Rows - pad);

            // 그래도 벽 속이면 7×7 이웃 중 가장 가까운 빈칸으로 35% 보간해 빠져나온다.
            var (cc, rr) = WorldGrid.ToCell(pos);
            if (world.IsSolid(cc, rr))
            {
                bool found = false;
                double bestD = double.MaxValue;
                Vec2 best = default;
                for (int dr = -3; dr <= 3; dr++)
                    for (int dc = -3; dc <= 3; dc++)
                    {
                        int c = cc + dc, r = rr + dr;
                        if (!world.InBounds(c, r) || world.IsSolid(c, r)) continue;
                        double d = dc * dc + dr * dr;
                        if (d < bestD)
                        {
                            bestD = d;
                            best = WorldGrid.CellCenter(c, r);
                            found = true;
                        }
                    }
                if (found)
                {
                    pos.X += (best.X - pos.X) * 0.35;
                    pos.Y += (best.Y - pos.Y) * 0.35;
                }
            }
        }
    }
}
