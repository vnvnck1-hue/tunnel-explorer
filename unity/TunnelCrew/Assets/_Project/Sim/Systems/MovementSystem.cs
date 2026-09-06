using System;

namespace TunnelCrew.Sim
{
    /// <summary>
    /// 원본 <c>update(dt)</c> 의 이동 구간(7108~7167행). 실행 순서를 그대로 지킨다.
    /// 조준 보간 → 대시 / 반동 / 일반 이동 중 택1 → 넉백 → 기절 감소.
    /// </summary>
    public static class MovementSystem
    {
        public static void Tick(WorldGrid world, PlayerState p, in PlayerInput input, double dt,
                                Action<PlayerDashedEvent> onDash = null,
                                PlayerBuild build = null)
        {
            double moveMul = build != null ? build.MoveMul : 1.0;
            // 굴진 가속 — 드릴을 쥔 동안 이동 배율 (원본 traitDrillMoveMul, 7119행)
            if (build != null && input.DrillHeld) moveMul *= build.DrillMoveMul;
            double dashMul = build != null ? build.RoleDashMul : 1.0;
            p.DashCooldown = Math.Max(0, p.DashCooldown - dt);
            p.StunTime = Math.Max(0, p.StunTime - dt);
            p.RockBounceCooldown = Math.Max(0, p.RockBounceCooldown - dt);

            // ── 조준 보간 (원본 TE.aimFollow * 60 * dt)
            if (!p.AimReady)
            {
                p.AimPoint = input.AimWorld;
                p.AimReady = true;
            }
            else
            {
                double t = Math.Min(1.0, SimTuning.AimFollow * 60.0 * dt);
                p.AimPoint += (input.AimWorld - p.AimPoint) * t;
            }
            var toAim = p.AimPoint - p.Position;
            if (toAim.SqrLength > 1e-9) p.Aim = toAim.Angle;

            // ── 이동 입력
            Vec2 move = p.CanMove ? input.Move : Vec2.Zero;
            if (move.Length > 0.2) p.LastMoveDir = move.Normalized;

            // ── 대시 시작 (원본 tryDash)
            if (input.DashPressed && p.CanMove && !p.DashActive && p.DashCooldown <= 0)
            {
                Vec2 dir = p.LastMoveDir;
                if (move.Length >= 0.2) dir = move.Normalized;
                else if (dir.Length < 0.2) dir = Vec2.FromAngle(p.Aim);

                // 원본 tryDash: 거리에 직업 배율, 배율이 1.2 를 넘으면(스카웃) 쿨도 짧아진다
                double speed = SimTuning.DashDistance * dashMul / Math.Max(0.04, SimTuning.DashDuration);
                p.DashActive = true;
                p.DashVelocity = dir * speed;
                p.DashTimeLeft = SimTuning.DashDuration;
                p.DashCooldown = SimTuning.DashCooldown / (dashMul > 1.2 ? 1.15 : 1.0);
                onDash?.Invoke(new PlayerDashedEvent { Position = p.Position, Direction = dir });
            }

            // ── 대시 / 암반 반동 / 일반 이동 (셋 중 하나만)
            if (p.DashActive)
            {
                double stepDt = Math.Min(dt, p.DashTimeLeft);
                // 원본: 4px(=0.08셀) 마다 서브스텝을 나눠 터널링을 막는다
                int slices = Math.Max(1, (int)Math.Ceiling(
                    p.DashVelocity.Length * stepDt / SimTuning.DashSliceLength));
                double sdt = stepDt / slices;

                for (int i = 0; i < slices; i++)
                {
                    Vec2 before = p.Position;
                    p.Position += p.DashVelocity * sdt;
                    CollisionSystem.Resolve(world, ref p.Position, SimTuning.PlayerRadius);

                    // 축이 막히면 그 축 속도만 죽인다
                    if (Math.Abs(p.Position.X - before.X) < 1e-9) p.DashVelocity.X = 0;
                    if (Math.Abs(p.Position.Y - before.Y) < 1e-9) p.DashVelocity.Y = 0;
                    if (p.DashVelocity.X == 0 && p.DashVelocity.Y == 0)
                    {
                        p.DashActive = false;
                        p.DashTimeLeft = 0;
                        break;
                    }
                }

                p.DashTimeLeft -= stepDt;
                if (p.DashTimeLeft <= 0)
                {
                    p.DashActive = false;
                    p.Velocity *= 0.35;
                }
            }
            else if (p.BounceActive && p.BounceTimeLeft > 0)
            {
                p.Velocity = p.BounceVelocity;
                p.Position += p.BounceVelocity * dt;
                CollisionSystem.Resolve(world, ref p.Position, SimTuning.PlayerRadius);

                p.BounceTimeLeft -= dt;
                double damp = Math.Pow(SimTuning.DrillBounceDamp, dt);
                p.BounceVelocity *= damp;
                if (p.BounceTimeLeft <= 0 || p.BounceVelocity.Length < SimTuning.DrillBounceStopSpeed)
                    p.BounceActive = false;
            }
            else
            {
                p.Velocity = move * (SimTuning.MoveSpeed * moveMul);
                p.Position += p.Velocity * dt;
                CollisionSystem.Resolve(world, ref p.Position, SimTuning.PlayerRadius);
            }

            // ── 넉백 (슬라이스 적분, 벽에 막힌 축만 죽인다)
            if (p.Knock.X != 0 || p.Knock.Y != 0)
            {
                double kv = p.Knock.Length;
                int slices = Math.Max(1, Math.Min(SimTuning.KnockMaxSlices,
                    (int)Math.Ceiling(kv * dt / SimTuning.KnockSliceLength)));
                double sdt = dt / slices;

                for (int i = 0; i < slices; i++)
                {
                    Vec2 before = p.Position;
                    p.Position += p.Knock * sdt;
                    CollisionSystem.Resolve(world, ref p.Position, SimTuning.PlayerRadius);
                    if (Math.Abs(p.Position.X - before.X) < 1e-9) p.Knock.X *= 0.2;
                    if (Math.Abs(p.Position.Y - before.Y) < 1e-9) p.Knock.Y *= 0.2;
                }

                double kd = Math.Exp(-SimTuning.KnockDrag * dt);
                p.Knock *= kd;
                if (p.Knock.Length < SimTuning.KnockStopSpeed) p.Knock = Vec2.Zero;
            }
        }

        /// <summary>외부(적 공격·보스 돌진)에서 넉백을 준다.</summary>
        public static void ApplyKnockback(PlayerState p, Vec2 impulse, double stunTime = 0)
        {
            p.Knock += impulse;
            if (stunTime > p.StunTime) p.StunTime = stunTime;
        }
    }
}
