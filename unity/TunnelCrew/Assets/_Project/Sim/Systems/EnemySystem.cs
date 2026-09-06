using System;
using System.Collections.Generic;

namespace TunnelCrew.Sim
{
    /// <summary>적이 쏜 탄. 원본 <c>G.eshots[]</c>.</summary>
    public sealed class EnemyShot
    {
        public Vec2 Position, Velocity;
        public double Life;
        public double Radius = SimTuning.EnemyShotRadius;
        public double Damage;
    }

    public struct EnemyHurtEvent
    {
        public EnemyState Enemy;
        public double Damage;
        public Vec2 HitDir;
        public bool Killed;
        public bool WasCritical;
        /// <summary>센트리 탄이 낸 피해인가 — XP 정산이 지원 채널로 간다.</summary>
        public bool ByTurret;
    }

    public struct EnemySpawnedEvent { public EnemyState Enemy; }
    public struct EnemyAlertEvent { public EnemyState Enemy; }
    public struct PlayerHurtEvent { public double Damage; public Vec2 HitDir; public bool Downed; }

    /// <summary>
    /// 적 전체를 소유한다. 스폰(6260) · 갱신 루프(6947) · 소프트 분리(6635) ·
    /// 피해 처리(6294, 6535) 를 담는다.
    ///
    /// 원본은 이 로직이 파티클·사운드·XP 와 뒤엉켜 있었다. 여기서는 규칙만 두고
    /// 나머지는 이벤트로 내보낸다.
    /// </summary>
    public sealed class EnemySystem
    {
        public readonly List<EnemyState> Enemies = new List<EnemyState>();
        public readonly List<EnemyShot> Shots = new List<EnemyShot>();

        /// <summary>지층별 적 체력 배율 (INF.enemyHpMul).</summary>
        public double EnemyHpMul { get; set; } = 1.0;
        /// <summary>위협도. 스폰 속도·체력·이동 속도에 함께 걸린다.</summary>
        public double Threat { get; set; } = 1.0;
        /// <summary>동시 존재 상한.</summary>
        public int Cap { get; set; } = SimTuning.EnemyMax;
        /// <summary>스폰 간격(초).</summary>
        public double SpawnInterval { get; set; } = SimTuning.EnemySpawnInterval;
        /// <summary>한 번에 나오는 수.</summary>
        public int SpawnBurst { get; set; } = 1;
        /// <summary>스폰 직전에 추가 마릿수를 돌려주는 훅 — RunState.TakeSpawnDebt (원본 6958행).</summary>
        public Func<int> ExtraSpawnCount = () => 0;

        /// <summary>보스 이동 훅 — BossSystem 이 등록한다. (개체, 플레이어, dt, 기준속도, 시간배율)</summary>
        public Action<EnemyState, PlayerState, double, double, double> BossMove;
        /// <summary>보스 이동 직후 벽 뭉개기 — BossSystem.CrushWalls.</summary>
        public Action BossCrush;

        /// <summary>외부(보스 시스템)가 만든 개체를 목록에 넣는다. 캡을 무시한다.</summary>
        public void AddExternal(EnemyState e) { Enemies.Add(e); Spawned?.Invoke(new EnemySpawnedEvent { Enemy = e }); }

        /// <summary>경감 없이 직접 피해 — 보스 붕괴탄 (원본 infBossProjectileHit 은 G.php 를 직접 깎았다).</summary>
        public void DamagePlayerDirect(PlayerState player, double dmg, Vec2 hitDir)
        {
            if (player.IFrames > 0 || player.Downed) return;
            dmg *= IncomingDamageMul();
            player.Hp -= dmg;
            player.IFrames = SimTuning.PlayerIFrame;
            bool downed = player.Hp <= 0;
            if (downed) { player.Hp = 0; player.Downed = true; }
            PlayerHurt?.Invoke(new PlayerHurtEvent { Damage = dmg, HitDir = hitDir, Downed = downed });
        }

        /// <summary>스폰 쿨 상한 — 블록을 부수면 다음 스폰이 당겨진다 (원본 G.enemyCd=min(...)).</summary>
        public void ClampSpawnCooldown(double max) { if (_spawnTimer > max) _spawnTimer = max; }

        public event Action<EnemySpawnedEvent> Spawned;
        public event Action<EnemyAlertEvent> Alerted;
        public event Action<EnemyHurtEvent> EnemyHurt;
        public event Action<PlayerHurtEvent> PlayerHurt;

        readonly WorldGrid _world;
        Rng _rng;
        double _spawnTimer;

        public EnemySystem(WorldGrid world, uint seed = 0x5EED)
        {
            _world = world;
            _rng = new Rng(seed);
            _spawnTimer = SimTuning.EnemySpawnInterval;
        }

        // ───────────────────────────── 스폰
        /// <summary>원본 pickEnemySpawn() — 어그로 반경의 링 안, 빈 칸 중에서 고른다.</summary>
        public bool TryPickSpawn(Vec2 around, out Vec2 spot)
        {
            var ring = new List<Vec2>();
            var fallback = new List<Vec2>();
            double ag = SimTuning.EnemyAggro;

            for (int r = 2; r < _world.Rows - 2; r++)
                for (int c = 2; c < _world.Cols - 2; c++)
                {
                    if (_world.IsSolid(c, r)) continue;
                    var p = WorldGrid.CellCenter(c, r);
                    double d = Vec2.Distance(p, around);
                    if (d >= ag * SimTuning.EnemySpawnRingMin && d <= ag * SimTuning.EnemySpawnRingMax)
                        ring.Add(p);
                    else if (d > 4.0) fallback.Add(p);
                }

            var pool = ring.Count > 0 ? ring : fallback;
            if (pool.Count == 0) { spot = default; return false; }
            spot = pool[(int)(_rng.NextDouble() * pool.Count)];
            return true;
        }

        public EnemyState Spawn(Vec2 around, bool forceApex = false)
        {
            if (Enemies.Count >= Cap) return null;
            if (!TryPickSpawn(around, out var spot)) return null;

            bool apex = forceApex
                || _rng.NextDouble() < Math.Min(0.18, 0.025 + Math.Max(0, Threat - 1) * 0.022);

            double timeHp = 1 + Math.Max(0, Threat - 1) * 0.55;
            double hp = SimTuning.EnemyHp * EnemyHpMul * timeHp * (apex ? SimTuning.ApexHpMul : 1);

            // 광란종은 원거리가 되지 않는다
            bool ranged = !apex && _rng.NextDouble() < SimTuning.EnemyRangedRatio;
            EnemyKind kind = apex ? EnemyKind.BroodBeast
                           : ranged ? EnemyKind.Spitter
                           : (_rng.NextDouble() < 0.5 ? EnemyKind.Crawler : EnemyKind.Spitter);

            // 개체마다 덩치가 다르고, 덩치가 곧 속도다
            double sizeVar = apex ? 1.0 : (0.86 + _rng.NextDouble() * 0.34);
            double radius = SimTuning.EnemyRadius
                          * (apex ? SimTuning.ApexRadiusMul : (kind == EnemyKind.Crawler ? 0.86 : 1.0))
                          * sizeVar;

            var e = new EnemyState
            {
                Position = spot,
                Kind = kind,
                Radius = radius,
                Hp = hp,
                HpMax = hp,
                ThreatHpMul = timeHp,
                SpeedMul = SpeedFromSize(radius, apex, ranged, _rng),
                DamageMul = apex ? SimTuning.ApexDamageMul : 1.0,
                IsApex = apex,
                IsRanged = ranged,
                Home = spot,
                WanderTarget = spot,
                WanderTimer = 0.3 + _rng.NextDouble() * 1.8,
                FaceAngle = _rng.NextDouble() * Math.PI * 2,
                ScanDir = _rng.NextDouble() < 0.5 ? -1 : 1,
                StrafeDir = _rng.NextDouble() < 0.5 ? -1 : 1,
                SightCooldown = _rng.NextDouble() * 0.12,
                LastSeen = spot,
                AttackCooldown = 0.5 + _rng.NextDouble() * 1.1,
                AnimTime = _rng.NextDouble() * 6.28,
                Bob = _rng.NextDouble() * 6.28,
                BlinkCooldown = 2.2 + _rng.NextDouble() * 3.6,
                JumpCooldown = kind == EnemyKind.Crawler
                    ? SimTuning.EnemyJumpCooldownMin + _rng.NextDouble() * 5.5
                    : double.PositiveInfinity,
            };

            Enemies.Add(e);
            Spawned?.Invoke(new EnemySpawnedEvent { Enemy = e });
            return e;
        }

        /// <summary>원본 enemySpeedFromSize() — 클수록 느리다.</summary>
        static double SpeedFromSize(double radius, bool apex, bool ranged, Rng rng)
        {
            double baseR = Math.Max(SimTuning.PxCells(6.0), SimTuning.EnemyRadius);
            double mul = Math.Pow(baseR / Math.Max(SimTuning.PxCells(6.0), radius),
                                  SimTuning.EnemySizeSpeedExponent);
            if (apex) mul *= SimTuning.ApexSpeedMul;
            if (ranged) mul *= 1.08;
            return JsMath.Clamp(mul * (0.94 + rng.NextDouble() * 0.14),
                                SimTuning.EnemySpeedMulMin, SimTuning.EnemySpeedMulMax);
        }

        // ───────────────────────────── 갱신
        public void Tick(PlayerState player, double dt)
        {
            if (TimeStopped()) { Enemies.RemoveAll(e => !e.Alive); return; }   // 정지된 모래시계
            TickSpawning(player.Position, dt);

            double baseSpeed = SimTuning.EnemySpeed;
            double timeSpeed = 1 + Math.Max(0, Threat - 1) * 0.13;

            foreach (var e in Enemies)
            {
                // 위협도가 오르면 살아 있는 적의 체력도 따라 오른다
                double want = 1 + Math.Max(0, Threat - 1) * 0.55;
                if (want > e.ThreatHpMul + 0.015)
                {
                    double f = want / e.ThreatHpMul;
                    e.Hp *= f; e.HpMax *= f; e.ThreatHpMul = want;
                }

                e.Hurt = Math.Max(0, e.Hurt - dt);
                e.Bob += dt * 6;
                e.AnimTime += dt;
                e.ContactCooldown = Math.Max(0, e.ContactCooldown - dt);

                if (e.BlinkTime > 0) e.BlinkTime = Math.Max(0, e.BlinkTime - dt);
                else if ((e.BlinkCooldown -= dt) <= 0)
                {
                    e.BlinkTime = 0.24;
                    e.BlinkCooldown = 2.4 + _rng.NextDouble() * 4.2;
                }

                ICrewTarget tgt = TargetOf?.Invoke(e);
                Vec2 targetPos = tgt != null ? tgt.Pos : player.Position;
                Vec2 delta = targetPos - e.Position;
                double dist = Math.Max(1e-6, delta.Length);

                e.JumpCooldown = Math.Max(0, e.JumpCooldown - dt);
                if (e.JumpTime > 0) e.JumpTime = Math.Max(0, e.JumpTime - dt);

                TryStartJump(e, delta, dist, dt);

                if (e.FrozenTime > 0)
                {
                    e.FrozenTime -= dt;
                    e.Velocity = Vec2.Zero;
                    e.Attack = AttackPhase.None;
                }
                else if (e.IsBoss)
                {
                    // 보스는 일반 AI 를 타지 않는다 — BossSystem 이 이동·돌진을 정한다
                    if (e.StunTime > 0) { e.StunTime -= dt; e.Velocity = Vec2.Zero; }
                    else BossMove?.Invoke(e, player, dt, baseSpeed, timeSpeed);
                }
                else if (e.IsJumping)
                {
                    e.Velocity = e.JumpVelocity;
                }
                else
                {
                    EnemyAiSystem.Tick(_world, e, targetPos, dt, baseSpeed, timeSpeed, _rng,
                        a => Alerted?.Invoke(new EnemyAlertEvent { Enemy = a }),
                        MeleeStrike(player, tgt),
                        FireShot);
                }

                if (e.SlowTime > 0) e.Velocity *= e.SlowMul;

                EnemyTick?.Invoke(e, dt);
                var prevPos = e.Position; double kbMag = e.Knock.Length;
                e.Position += (e.Velocity + e.Knock) * dt;
                if (e.IsBoss)
                {
                    // 보스는 벽에 막히지 않고 뭉개며 지나간다 (원본 7001행). 맵 가장자리에서만 멈춘다.
                    e.Position = new Vec2(JsMath.Clamp(e.Position.X, 2, _world.Cols - 2), JsMath.Clamp(e.Position.Y, 2, _world.Rows - 2));
                    BossCrush?.Invoke();
                }
                else
                {
                    var before = e.Position;
                    CollisionSystem.Resolve(_world, ref e.Position, e.Radius);
                    if (kbMag > 0 && WallSlam != null && Vec2.Distance(before, e.Position) > 1e-6) WallSlam(e, before, kbMag);
                }

                e.Velocity *= 0.86;
                double kbDrag = Math.Exp(-SimTuning.KnockDrag * dt);
                e.Knock *= kbDrag;

                if (e.SlowTime > 0) e.SlowTime -= dt;
            }

            Separate(dt);
            SeparateFromPlayer(player, dt);
            Enemies.RemoveAll(e => !e.Alive);
            TickShots(player, dt);
        }

        void TickSpawning(Vec2 around, double dt)
        {
            _spawnTimer -= dt;
            if (_spawnTimer > 0) return;
            _spawnTimer = SpawnInterval;
            int count = SpawnBurst + ExtraSpawnCount();
            for (int i = 0; i < count; i++) Spawn(around);
        }

        void TryStartJump(EnemyState e, Vec2 delta, double dist, double dt)
        {
            bool smallCrawler = e.Kind == EnemyKind.Crawler && !e.IsApex && !e.IsBoss;
            if (!smallCrawler || e.Ai != EnemyAi.Chase || e.Attack != AttackPhase.None) return;
            if (e.JumpTime > 0 || e.JumpCooldown > 0) return;
            if (dist <= SimTuning.EnemyJumpMinDistance || dist >= SimTuning.EnemyAggro) return;
            if (_rng.NextDouble() >= dt * SimTuning.EnemyJumpChance) return;

            double speed = SimTuning.EnemyJumpSpeedMin
                + _rng.NextDouble() * (SimTuning.EnemyJumpSpeedMax - SimTuning.EnemyJumpSpeedMin);
            e.JumpTime = SimTuning.EnemyJumpDuration;
            e.JumpDuration = SimTuning.EnemyJumpDuration;
            e.JumpCooldown = SimTuning.EnemyJumpCooldownMin
                + _rng.NextDouble() * (SimTuning.EnemyJumpCooldownMax - SimTuning.EnemyJumpCooldownMin);
            e.JumpVelocity = (delta / dist) * speed;
        }

        // ───────────────────────────── 공격
        Action<EnemyState> MeleeStrike(PlayerState player, ICrewTarget tgt) => e =>
        {
            // 타격 순간 자기 자신이 앞으로 밀린다
            e.Knock += e.AttackDir * SimTuning.EnemyLungeSpeed;

            bool crew = tgt != null && !(tgt is PlayerState);
            Vec2 delta = (crew ? tgt.Pos : player.Position) - e.Position;
            double d = Math.Max(1e-6, delta.Length);
            double reach = e.Radius + SimTuning.PlayerRadius
                         + SimTuning.EnemyReach * SimTuning.EnemyStrikeReachMul;
            if (d > reach) return;

            // 옆으로 빠지면 헛스윙한다
            double dot = (delta.X / d) * e.AttackDir.X + (delta.Y / d) * e.AttackDir.Y;
            if (dot < SimTuning.EnemyStrikeDot) return;

            if (crew) HurtTarget?.Invoke(tgt, SimTuning.EnemyDamage * e.DamageMul, delta / d);
            else ApplyPlayerDamage(player, SimTuning.EnemyDamage * e.DamageMul, delta / d);
        };

        void FireShot(EnemyState e)
        {
            // 표적 방향에 약간의 흔들림을 준다
            double a = e.AttackDir.Angle + (_rng.NextDouble() - 0.5) * 0.11;
            var dir = Vec2.FromAngle(a);
            Shots.Add(new EnemyShot
            {
                Position = e.Position + dir * (e.Radius * 0.85),
                Velocity = dir * SimTuning.EnemyShotSpeed,
                Life = SimTuning.EnemyShotLife,
                Damage = SimTuning.EnemyDamage * SimTuning.EnemyShotDamageMul * e.DamageMul,
            });
        }

        void TickShots(PlayerState player, double dt)
        {
            for (int i = Shots.Count - 1; i >= 0; i--)
            {
                var s = Shots[i];
                s.Life -= dt;
                s.Position += s.Velocity * dt;

                var (c, r) = WorldGrid.ToCell(s.Position);
                bool hitWall = !_world.InBounds(c, r) || _world.IsSolid(c, r);
                bool hitPlayer = Vec2.Distance(s.Position, player.Position)
                               < SimTuning.PlayerRadius + s.Radius;

                if (hitPlayer)
                {
                    var dir = (player.Position - s.Position).Normalized;
                    ApplyPlayerDamage(player, s.Damage, dir);
                }

                ICrewTarget crewHit = null;
                if (!hitPlayer && HitTest != null)
                {
                    crewHit = HitTest(s.Position, s.Radius);
                    if (crewHit != null) HurtTarget?.Invoke(crewHit, s.Damage, (crewHit.Pos - s.Position).Normalized);
                }

                if (s.Life <= 0 || hitWall || hitPlayer || crewHit != null) Shots.RemoveAt(i);
            }
        }

        // ───────────────────────────── 피해
        /// <summary>
        /// 원본 <c>hurtEnemy()</c> (6294~6334). 넉백 공식과 즉시 각성 규칙을 그대로 옮겼다.
        /// </summary>
        public void HurtEnemy(EnemyState e, double damage, Vec2 hitDir, Vec2 sourcePosition, bool byTurret = false)
        {
            if (!e.Alive) return;

            // 맞으면 즉시 각성한다 (보스 제외)
            if (!e.IsBoss)
            {
                e.Ai = EnemyAi.Chase;
                e.LastSeen = sourcePosition;
                e.LostTime = 0;
            }

            damage = EnemyDamageMod(e, damage, !byTurret);
            e.Hp -= damage;
            e.Hurt = 0.18;

            if (!e.IsBoss)
            {
                // 넉백: 피해가 클수록, 가까이서 맞을수록 멀리 밀린다
                double hitPower = Math.Pow(damage / SimTuning.EnemyGunDamage, 0.72);
                double playerDist = Vec2.Distance(e.Position, sourcePosition);
                double rangePower = JsMath.Clamp(1.42 - playerDist / 7.4, 0.12, 1.36);
                double resist = e.IsApex ? SimTuning.ApexKnockResist : 1.0;

                double impulse = JsMath.Clamp(
                    SimTuning.TeCells(92.0) * hitPower * rangePower * resist,
                    SimTuning.TeCells(3.0), SimTuning.TeCells(290.0));

                e.Knock += hitDir * (impulse * KnockMul());
            }

            bool killed = e.Hp <= 0;
            if (killed) EnemyKilled?.Invoke(e);
            EnemyHurt?.Invoke(new EnemyHurtEvent
            {
                Enemy = e, Damage = damage, HitDir = hitDir, Killed = killed, ByTurret = byTurret,
            });
        }

        /// <summary>원본 <c>applyPlayerDamage()</c> (6535~6562).</summary>
        /// <summary>거너 방어막 등 외부 피해 배율. 원본 shieldT>0 이면 ×0.35.</summary>
        public Func<double> IncomingDamageMul = () => 1.0;
        /// <summary>플레이어가 받는 피해의 마지막 보정 (유물: 젤·암반 피부). 원본 infRelicPlayerDamageMod.</summary>
        public Func<double, double> PlayerDamageMod = d => d;
        /// <summary>플레이어 피격 직후 훅 (유물: 회중시계·피뢰침·모래시계).</summary>
        public Action AfterPlayerHurt;
        /// <summary>적이 받는 피해 보정 + 상태이상 프록 (유물). (개체, 피해, 무기인가) → 피해.</summary>
        public Func<EnemyState, double, bool, double> EnemyDamageMod = (e, d, w) => d;
        /// <summary>넉백 배율 (대지 공명 ×1.5).</summary>
        public Func<double> KnockMul = () => 1.0;
        /// <summary>개체 틱 훅 — 유물 DoT.</summary>
        public Action<EnemyState, double> EnemyTick;
        /// <summary>넉백 중 벽 충돌 훅 (개체, 충돌 전 위치, 넉백 크기).</summary>
        public Action<EnemyState, Vec2, double> WallSlam;
        /// <summary>처치 훅 (유물).</summary>
        public Action<EnemyState> EnemyKilled;
        /// <summary>시간 정지 — 적·적탄이 멈춘다 (모래시계).</summary>
        public Func<bool> TimeStopped = () => false;
        /// <summary>적이 노릴 대상 — AI 크루가 있으면 가장 가까운 크루 (원본 AI_TGT). null 이면 사람.</summary>
        public Func<EnemyState, ICrewTarget> TargetOf;
        /// <summary>사람이 아닌 표적(AI 크루)에게 피해를 준다.</summary>
        public Action<ICrewTarget, double, Vec2> HurtTarget;
        /// <summary>적 투사체가 크루에 맞았는가 (원본 AI.hitTest).</summary>
        public Func<Vec2, double, ICrewTarget> HitTest;
        /// <summary>지금 들어오는 피해의 주인 — AI 크루면 처치 XP 가 그 크루에게 간다. 호출자가 설정·해제한다.</summary>
        public object DamageSource;

        public void ApplyPlayerDamage(PlayerState player, double raw, Vec2 hitDir)
        {
            if (player.IFrames > 0 || player.Downed) return;

            // 플레이 중에는 15% 경감 + 최소 4, 방어막이면 추가 ×0.35
            double dmg = Math.Max(4, JsMath.Round(raw * 0.85));
            dmg *= IncomingDamageMul();

            dmg = PlayerDamageMod(dmg);
            player.Hp -= dmg;
            player.IFrames = SimTuning.PlayerIFrame;
            AfterPlayerHurt?.Invoke();

            // 넉백 — 원본은 위치를 직접 밀고 충돌을 푼다
            player.Position -= hitDir * (SimTuning.EnemyKnock * 0.35);
            CollisionSystem.Resolve(_world, ref player.Position, SimTuning.PlayerRadius);

            bool downed = player.Hp <= 0;
            if (downed) { player.Hp = 0; player.Downed = true; }

            PlayerHurt?.Invoke(new PlayerHurtEvent { Damage = dmg, HitDir = hitDir, Downed = downed });
        }

        // ───────────────────────────── 소프트 분리
        /// <summary>
        /// 원본 <c>separateEnemies()</c> — 반지름 합의 86%까지는 겹침을 허용하고
        /// 초과분만 조금씩 푼다. 덩치가 클수록 덜 밀린다.
        /// </summary>
        void Separate(double dt)
        {
            int n = Enemies.Count;
            if (n < 2) return;

            double ratio = SimTuning.EnemySepRatio;
            double step = Math.Min(0.5, Math.Max(0.02, SimTuning.EnemySepRate * dt));
            double cap = SimTuning.EnemySepMaxPush * dt;
            double bossStep = Math.Min(1, Math.Max(0.05, SimTuning.EnemySepBossRate * dt));

            for (int i = 0; i < n; i++)
            {
                var a = Enemies[i];
                if (!a.Alive) continue;
                for (int j = i + 1; j < n; j++)
                {
                    var b = Enemies[j];
                    if (!b.Alive) continue;

                    double want = (a.Radius + b.Radius) * ratio;
                    double dx = b.Position.X - a.Position.X;
                    double dy = b.Position.Y - a.Position.Y;
                    if (dx > want || dx < -want || dy > want || dy < -want) continue;

                    double d = Math.Sqrt(dx * dx + dy * dy);
                    if (d >= want) continue;

                    if (d < 1e-4)
                    {
                        double ang = i * 2.39996 + j * 1.13;
                        dx = Math.Cos(ang); dy = Math.Sin(ang);
                    }
                    else { dx /= d; dy /= d; }

                    // 보스는 밀리지 않고 상대만 즉시 밀어낸다
                    double ia = a.IsBoss ? 0 : 1.0 / Math.Max(SimTuning.PxCells(4.0), a.Radius);
                    double ib = b.IsBoss ? 0 : 1.0 / Math.Max(SimTuning.PxCells(4.0), b.Radius);
                    double tot = ia + ib;
                    if (tot <= 0) continue;

                    bool heavy = a.IsBoss || b.IsBoss;
                    double push = heavy ? (want - d) * bossStep : Math.Min((want - d) * step, cap);
                    if (push <= 0) continue;

                    double pa = push * (ia / tot), pb = push * (ib / tot);
                    if (pa > 0) { a.Position.X -= dx * pa; a.Position.Y -= dy * pa; }
                    if (pb > 0) { b.Position.X += dx * pb; b.Position.Y += dy * pb; }
                }
            }

            foreach (var e in Enemies)
                if (!e.IsBoss) CollisionSystem.Resolve(_world, ref e.Position, e.Radius);
        }

        /// <summary>
        /// 적이 플레이어 위에 올라서지 않게 한다. 도약 착지·넉백으로 겹친 개체를 반지름 합 바깥으로
        /// 부드럽게 밀어낸다(적만 밀린다). 원본에는 없던 규칙 — 사장님 피드백(2026-09-06).
        /// </summary>
        void SeparateFromPlayer(PlayerState player, double dt)
        {
            double step = Math.Min(0.6, Math.Max(0.03, SimTuning.EnemySepRate * 1.5 * dt));
            double cap = SimTuning.EnemySepMaxPush * dt;
            foreach (var e in Enemies)
            {
                if (!e.Alive || e.IsBoss || e.IsJumping) continue;
                double want = (e.Radius + SimTuning.PlayerRadius) * SimTuning.EnemyPlayerSepRatio;
                var d = e.Position - player.Position;
                double dist = d.Length;
                if (dist >= want) continue;
                Vec2 n = dist < 1e-4 ? Vec2.FromAngle(e.FaceAngle + Math.PI) : d / dist;
                double push = Math.Min((want - dist) * step, cap);
                e.Position += n * push;
                CollisionSystem.Resolve(_world, ref e.Position, e.Radius);
            }
        }

        public void Clear()
        {
            Enemies.Clear();
            Shots.Clear();
            _spawnTimer = SpawnInterval;
        }
    }
}
