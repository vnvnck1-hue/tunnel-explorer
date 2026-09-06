using System;

namespace TunnelCrew.Sim
{
    /// <summary>
    /// 원본 드릴 파이프라인(7203~7266행)과 예열·과열 곡선(2347~2361행).
    ///
    /// 규칙: 드릴 축을 따라 3점을 샘플해 **가장 가까운 파괴 가능 타일**을 고르고,
    /// 없으면 같은 샘플로 기반암을 찾아 반동한다.
    /// </summary>
    public static class MiningSystem
    {
        public static void Tick(WorldGrid world, PlayerState p, in PlayerInput input, double dt,
                                Action<DrillBouncedEvent> onBounce = null,
                                Action<Vec2, double> onDrillBeat = null)
        {
            p.IsDigging = false;

            bool wantDrill = input.DrillHeld && p.CanMove && p.CanDrill;
            UpdateWarmAndHeat(p, wantDrill, dt);

            if (!wantDrill)
            {
                p.DrillDamageAccum = 0;
                p.DrillTimeAccum = 0;
                p.DrillSpin = 0;
                return;
            }

            p.DrillSpin += dt * SimTuning.DrillDamageMul * DrillSpinMul(p);

            double ca = Math.Cos(p.Aim), sa = Math.Sin(p.Aim);

            // ── 파괴 가능한 타일 찾기 (3점 샘플 중 가장 가까운 것)
            int bestCell = -1;
            double bestD = double.MaxValue;
            Vec2 bestCenter = default;

            foreach (var (lenMul, _) in SimTuning.DrillSamples)
            {
                double len = SimTuning.DrillTip * lenMul;
                var sample = new Vec2(p.Position.X + ca * len, p.Position.Y + sa * len);
                if (!TryFindNearestBreakable(world, sample, out int cell, out Vec2 center, out double d)) continue;
                if (d < SimTuning.DrillReachCells && d < bestD)
                {
                    bestD = d; bestCell = cell; bestCenter = center;
                }
            }

            if (bestCell >= 0)
            {
                p.IsDigging = true;
                int c = bestCell % world.Cols, r = bestCell / world.Cols;

                Vec2 delta = bestCenter - p.Position;
                double dd = delta.Length;
                if (dd < 1e-9) dd = 1;
                Vec2 normal = delta / dd;

                double dv = SimTuning.DrillDps * SimTuning.DrillDamageMul * WarmMul(p) * dt;
                world.Damage(c, r, dv, normal);

                p.DrillDamageAccum += dv;
                p.DrillTimeAccum += dt;
                if (p.DrillTimeAccum >= HitInterval(p))
                {
                    onDrillBeat?.Invoke(bestCenter, p.DrillDamageAccum);
                    p.DrillDamageAccum = 0;
                    p.DrillTimeAccum = 0;
                }
                return;
            }

            // ── 파괴 불가 암반이면 반동
            int rockCell = -1;
            double rockD = double.MaxValue;
            Vec2 rockCenter = default;

            foreach (var (lenMul, _) in SimTuning.DrillSamples)
            {
                double len = SimTuning.DrillTip * lenMul;
                var sample = new Vec2(p.Position.X + ca * len, p.Position.Y + sa * len);
                var (sc, sr) = WorldGrid.ToCell(sample);
                if (!world.InBounds(sc, sr)) continue;
                if (!TileTypes.IsBedrock(world.At(sc, sr))) continue;

                Vec2 center = WorldGrid.CellCenter(sc, sr);
                double d = (center - sample).Length;
                if (d < SimTuning.DrillReachCells && d < rockD)
                {
                    rockD = d; rockCell = world.Index(sc, sr); rockCenter = center;
                }
            }

            if (rockCell >= 0 && p.RockBounceCooldown <= 0)
            {
                // 조준 반대 방향으로 튕긴다
                var n = new Vec2(-ca, -sa);
                p.BounceActive = true;
                p.BounceVelocity = n * SimTuning.DrillRockBounceSpeed;
                p.BounceTimeLeft = SimTuning.DrillRockBounceDuration;
                p.RockBounceCooldown = SimTuning.DrillRockBounceCooldown;

                p.Position += n * SimTuning.DrillRockBouncePush;
                CollisionSystem.Resolve(world, ref p.Position, SimTuning.PlayerRadius);

                onBounce?.Invoke(new DrillBouncedEvent { Position = rockCenter, Normal = n });
            }

            p.DrillDamageAccum = 0;
            p.DrillTimeAccum = 0;
        }

        /// <summary>
        /// 원본 <c>targets(x,y,1)[0]</c> — 파괴 가능한 타일 중 가장 가까운 것.
        /// 원본은 전 셀을 정렬했지만 결과는 최솟값 하나다. JS sort 가 안정 정렬이라
        /// 거리가 같으면 스캔 순서(행→열)가 이기므로, 여기서도 엄격한 `&lt;` 비교로 같게 만든다.
        /// </summary>
        static bool TryFindNearestBreakable(WorldGrid world, Vec2 from,
                                            out int cell, out Vec2 center, out double dist)
        {
            cell = -1; center = default; dist = double.MaxValue;
            double bestSqr = double.MaxValue;

            for (int r = 1; r < world.Rows - 1; r++)
                for (int c = 1; c < world.Cols - 1; c++)
                {
                    var t = world.At(c, r);
                    if (t == TileType.Empty || TileTypes.IsBedrock(t)) continue;
                    Vec2 mid = WorldGrid.CellCenter(c, r);
                    double dx = mid.X - from.X, dy = mid.Y - from.Y;
                    double sqr = dx * dx + dy * dy;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        cell = world.Index(c, r);
                        center = mid;
                    }
                }

            if (cell < 0) return false;
            dist = Math.Sqrt(bestSqr);
            return true;
        }

        // ───────────────────────────── 예열 · 과열
        static void UpdateWarmAndHeat(PlayerState p, bool drilling, double dt)
        {
            if (SimTuning.DrillWarmOn)
            {
                if (drilling) p.DrillWarm = Math.Min(1.0, p.DrillWarm + dt / SimTuning.DrillWarmTime);
                else p.DrillWarm = Math.Max(0.0, p.DrillWarm - dt * SimTuning.DrillWarmDecay);
            }

            if (!SimTuning.DrillHeatOn) return;

            if (p.DrillHeatLock > 0)
            {
                p.DrillHeatLock = Math.Max(0, p.DrillHeatLock - dt);
                p.DrillHeat = Math.Max(0, p.DrillHeat - dt * SimTuning.DrillHeatCool);
                return;
            }

            if (drilling)
            {
                p.DrillHeat += dt * SimTuning.DrillHeatBuild;
                if (p.DrillHeat >= 1.0)
                {
                    p.DrillHeat = 1.0;
                    p.DrillHeatLock = SimTuning.DrillHeatLock;
                    p.DrillWarm *= 0.35;   // 원본: 과열 잠금 시 예열도 깎인다
                }
            }
            else
            {
                p.DrillHeat = Math.Max(0, p.DrillHeat - dt * SimTuning.DrillHeatCool);
            }
        }

        /// <summary>원본 drillWarmMul() — min + (1-min) * t^curve.</summary>
        public static double WarmMul(PlayerState p)
        {
            if (!SimTuning.DrillWarmOn) return 1.0;
            double t = JsMath.Clamp(p.DrillWarm, 0, 1);
            return SimTuning.DrillWarmMin
                 + (1 - SimTuning.DrillWarmMin) * Math.Pow(t, SimTuning.DrillWarmCurve);
        }

        /// <summary>원본 drillSpinMul() = DRILL_SPD * drillWarmMul().</summary>
        public static double DrillSpinMul(PlayerState p) => 4.0 * WarmMul(p);

        /// <summary>원본 drillHitInterval() = 0.06 / max(0.08, warmMul).</summary>
        public static double HitInterval(PlayerState p)
            => SimTuning.DrillHitInterval / Math.Max(0.08, WarmMul(p));
    }
}
