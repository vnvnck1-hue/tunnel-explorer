using System;

namespace TunnelCrew.Sim
{
    /// <summary>
    /// 원본 <c>updateEnemyAI()</c> (6719~6815행).
    ///
    /// 보스를 뺀 모든 적은 자기 영역을 배회하다가, 시야 원뿔 안에서 벽에 가리지 않은
    /// 표적을 발견하면 추격으로 바꾼다. 놓친 뒤 5초가 지나면 그 자리를 새 영역 중심으로
    /// 삼아 다시 배회한다.
    ///
    /// 표적은 <see cref="ITargetProvider"/> 로 받는다. 원본이 <c>AI_TGT(e)</c> 전역 함수를
    /// 경유해 사람·AI 크루·코옵 피어를 같은 목록으로 다루던 구조를 인터페이스로 올린 것이다
    /// (계획 §3.1-5).
    /// </summary>
    public static class EnemyAiSystem
    {
        public static void Tick(WorldGrid world, EnemyState e, Vec2 target, double dt,
                                double baseSpeed, double timeSpeedMul, Rng rng,
                                Action<EnemyState> onAlert = null,
                                Action<EnemyState> onMeleeStrike = null,
                                Action<EnemyState> onFireShot = null)
        {
            Vec2 delta = target - e.Position;
            double dist = Math.Max(1e-6, delta.Length);

            // 기절 — 넉백만 남고 아무것도 못 한다
            if (e.StunTime > 0)
            {
                e.StunTime -= dt;
                e.Velocity = Vec2.Zero;
                e.Attack = AttackPhase.None;
                e.SightCooldown = Math.Max(e.SightCooldown, 0.06);
                return;
            }

            double mul = Math.Min(2.0, e.SpeedMul * timeSpeedMul);

            void Move(Vec2 dir, double scale)
            {
                e.Velocity = dir * (baseSpeed * mul * scale);
                if (scale > 0.05) e.FaceAngle = dir.Angle;
            }

            // ── 시야는 0.1초 간격으로만 다시 본다
            e.SightCooldown -= dt;
            if (e.SightCooldown <= 0)
            {
                e.SightCooldown = SimTuning.EnemySightInterval + rng.NextDouble() * 0.05;
                e.SeesTarget = SeesTarget(world, e, target);
            }

            if (e.SeesTarget)
            {
                e.LostTime = 0;
                e.LastSeen = target;
                if (e.Ai != EnemyAi.Chase)
                {
                    e.Ai = EnemyAi.Chase;
                    onAlert?.Invoke(e);
                }
            }
            else
            {
                e.LostTime += dt;
                if (e.Ai == EnemyAi.Chase && e.LostTime >= SimTuning.EnemyLoseTime)
                {
                    e.Ai = EnemyAi.Wander;
                    e.Home = e.Position;
                    e.WanderIdle = true;
                    e.WanderTimer = 0.4 + rng.NextDouble() * 1.2;
                    e.Attack = AttackPhase.None;
                }
            }

            e.AttackCooldown = Math.Max(0, e.AttackCooldown - dt);

            // ── 공격 모션 중에는 이동 불가
            if (e.Attack != AttackPhase.None)
            {
                e.Velocity = Vec2.Zero;
                e.AttackTimer -= dt;

                if (e.Attack == AttackPhase.Windup)
                {
                    // 선딜 동안 조준만 따라간다
                    e.AttackDir = delta / dist;
                    e.FaceAngle = delta.Angle;
                    if (e.AttackTimer <= 0)
                    {
                        e.Attack = AttackPhase.Strike;
                        e.AttackTimer = SimTuning.EnemyStrike;
                        if (e.IsRanged) onFireShot?.Invoke(e);
                        else onMeleeStrike?.Invoke(e);
                    }
                }
                else if (e.Attack == AttackPhase.Strike)
                {
                    if (e.AttackTimer <= 0)
                    {
                        e.Attack = AttackPhase.Recover;
                        e.AttackTimer = SimTuning.EnemyRecover;
                    }
                }
                else if (e.AttackTimer <= 0)
                {
                    e.Attack = AttackPhase.None;
                    double baseCd = e.IsRanged ? SimTuning.EnemyShotCooldown : SimTuning.EnemyAttackCooldown;
                    e.AttackCooldown = baseCd * (0.82 + rng.NextDouble() * 0.42);
                }
                return;
            }

            if (e.Ai == EnemyAi.Chase)
            {
                Vec2 n = delta / dist;

                if (e.IsRanged)
                {
                    double keepMin = SimTuning.EnemyKeepMin, keepMax = SimTuning.EnemyKeepMax;

                    // 도망 중에도 쿨이 돌면 멈춰서 한 발 뱉는다
                    if (e.SeesTarget && e.AttackCooldown <= 0 && dist > keepMin * 0.5)
                    {
                        BeginAttack(e, delta, rng);
                        return;
                    }
                    if (!e.SeesTarget) { MoveToLastSeen(e, Move, 0.8); return; }

                    if (dist < keepMin)
                    {
                        var away = FleeDirection(world, e, n * -1);
                        Move(away, SimTuning.EnemyFleeSpeed);
                        e.FaceAngle = n.Angle;   // 물러나면서도 시선은 표적에
                    }
                    else if (dist > keepMax) Move(n, 1.0);
                    else
                    {
                        // 사거리 유지 — 옆으로 돌며 틈을 본다
                        Move(new Vec2(-n.Y * e.StrafeDir, n.X * e.StrafeDir), 0.5);
                        e.FaceAngle = n.Angle;
                    }
                    return;
                }

                double reach = e.Reach;
                if (e.SeesTarget && dist <= reach && e.AttackCooldown <= 0)
                {
                    BeginAttack(e, delta, rng);
                    return;
                }
                if (dist <= reach * 0.9)
                {
                    // 쿨 대기 중엔 붙어서 노려본다
                    e.Velocity *= 0.82;
                    e.FaceAngle = n.Angle;
                    return;
                }
                if (!e.SeesTarget) { MoveToLastSeen(e, Move, 0.9); return; }
                Move(n, 1.0);
                return;
            }

            // ── 배회
            e.WanderTimer -= dt;
            if (e.WanderIdle)
            {
                e.Velocity *= 0.86;
                e.FaceAngle += dt * e.ScanDir * 0.9;
                if (e.WanderTimer <= 0)
                {
                    if (PickWanderTarget(world, e, rng))
                    {
                        e.WanderIdle = false;
                        e.WanderTimer = 2.2 + rng.NextDouble() * 2.8;
                    }
                    else
                    {
                        e.WanderTimer = 0.6 + rng.NextDouble() * 0.9;
                        e.ScanDir = rng.NextDouble() < 0.5 ? -1 : 1;
                    }
                }
                return;
            }

            Vec2 toWander = e.WanderTarget - e.Position;
            double wd = toWander.Length;
            if (wd < 0.5 || e.WanderTimer <= 0)
            {
                e.WanderIdle = true;
                e.WanderTimer = 1.1 + rng.NextDouble() * 2.4;
                e.ScanDir = rng.NextDouble() < 0.5 ? -1 : 1;
                e.Velocity *= 0.7;
                return;
            }
            Move(toWander / wd, SimTuning.EnemyWanderSpeed);
        }

        static void MoveToLastSeen(EnemyState e, Action<Vec2, double> move, double scale)
        {
            Vec2 toLast = e.LastSeen - e.Position;
            double ld = Math.Max(1e-6, toLast.Length);
            if (ld < 0.6) { e.Velocity *= 0.86; return; }
            move(toLast / ld, scale);
        }

        /// <summary>
        /// 원본 <c>enemySeesPlayer()</c> — 근접 반경은 전방위, 그 밖은 바라보는 원뿔 안에서만.
        /// 마지막에 벽 차단을 본다.
        /// </summary>
        public static bool SeesTarget(WorldGrid world, EnemyState e, Vec2 target)
        {
            Vec2 delta = target - e.Position;
            double dist = Math.Max(1e-6, delta.Length);
            double range = SimTuning.EnemyAggro * SimTuning.EnemyVisionMul;
            if (dist > range) return false;

            if (dist > SimTuning.EnemyNearSense)
            {
                double cosFov = Math.Cos(Math.Max(5.0, SimTuning.EnemyVisionFov) * Math.PI / 180.0);
                double fa = e.FaceAngle;
                double dot = (delta.X / dist) * Math.Cos(fa) + (delta.Y / dist) * Math.Sin(fa);
                if (dot < cosFov) return false;
            }
            return SightUtil.IsClear(world, e.Position, target);
        }

        static void BeginAttack(EnemyState e, Vec2 delta, Rng rng)
        {
            double d = Math.Max(1e-6, delta.Length);
            e.Attack = AttackPhase.Windup;
            double windup = e.IsRanged
                ? SimTuning.EnemyWindup * SimTuning.EnemyRangedWindupMul
                : SimTuning.EnemyWindup;
            e.AttackTimer = windup * (0.88 + rng.NextDouble() * 0.24);
            e.AttackWindupTotal = e.AttackTimer;
            e.AttackDir = delta / d;
            e.FaceAngle = delta.Angle;
            e.Velocity = Vec2.Zero;
        }

        /// <summary>원본 <c>enemyFleeDir()</c> — 벽에 막히지 않는 후퇴 방향을 부채꼴로 찾는다.</summary>
        static Vec2 FleeDirection(WorldGrid world, EnemyState e, Vec2 want)
        {
            const double probe = 1.6;   // CELL*1.6
            double a0 = want.Angle;
            for (int i = 0; i < 5; i++)
            {
                double a = a0 + (i == 0 ? 0 : (i % 2 == 1 ? 1 : -1) * 0.62 * Math.Ceiling(i / 2.0));
                var dir = Vec2.FromAngle(a);
                if (SightUtil.IsClear(world, e.Position, e.Position + dir * probe)) return dir;
            }
            return want;
        }

        /// <summary>원본 <c>enemyPickWander()</c> — 영역 안에서 갈 수 있는 지점을 8번까지 찾는다.</summary>
        static bool PickWanderTarget(WorldGrid world, EnemyState e, Rng rng)
        {
            double rad = SimTuning.EnemyWanderRadius;
            for (int i = 0; i < 8; i++)
            {
                double a = rng.NextDouble() * Math.PI * 2;
                double d = rad * (0.3 + rng.NextDouble() * 0.7);
                double x = JsMath.Clamp(e.Home.X + Math.Cos(a) * d, 1.5, world.Cols - 1.5);
                double y = JsMath.Clamp(e.Home.Y + Math.Sin(a) * d, 1.5, world.Rows - 1.5);
                int c = (int)Math.Floor(x), r = (int)Math.Floor(y);
                if (!world.InBounds(c, r) || world.IsSolid(c, r)) continue;
                var p = new Vec2(x, y);
                if (!SightUtil.IsClear(world, e.Position, p)) continue;
                e.WanderTarget = p;
                return true;
            }
            return false;
        }
    }
}
