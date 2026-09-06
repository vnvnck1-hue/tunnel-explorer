using System;
using System.Collections.Generic;

namespace TunnelCrew.Sim
{
    public enum BossTier : byte { Guardian = 0, Apex, Variant }
    public enum BossDashPhase : byte { None = 0, Windup, Charge }
    public enum BossWanderPhase : byte { Idle = 0, Walk }

    /// <summary>원본 INF_BOSS_TIER(13229). 수호자(지층 1~2) · 암반 포식자(중심부) · 변종(이상지대).</summary>
    public struct BossTierDef
    {
        public string Name;
        public double HpMul, SizeMul, DmgMul, SpeedMul, ProjMul, SummonMul, DashBonus;
        public int CoreBase;

        public static BossTierDef For(BossTier t) => t switch
        {
            BossTier.Guardian => new BossTierDef { Name = "심층 수호자", HpMul = .38, SizeMul = .62, DmgMul = .65, SpeedMul = 1.45, ProjMul = .5, SummonMul = .5, DashBonus = .25, CoreBase = 2 },
            BossTier.Apex => new BossTierDef { Name = "암반 포식자", HpMul = 1, SizeMul = 1, DmgMul = 1, SpeedMul = 1, ProjMul = 1, SummonMul = 1, DashBonus = 0, CoreBase = 6 },
            _ => new BossTierDef { Name = "변종 포식자", HpMul = 1.12, SizeMul = 1, DmgMul = 1.15, SpeedMul = 1.05, ProjMul = 1.1, SummonMul = 1.15, DashBonus = .1, CoreBase = 8 },
        };

        /// <summary>원본 infBossTierForDepth — 최종 지층 전이면 수호자, 최종 지층이면 포식자, 그 뒤는 변종.</summary>
        public static BossTier TierForDepth(int depth)
            => depth < Planet.StratumCount ? BossTier.Guardian : depth == Planet.StratumCount ? BossTier.Apex : BossTier.Variant;
    }

    /// <summary>원본 BOSS_TUNE_FALLBACK(2114) 중 규칙에 쓰이는 값.</summary>
    public static class BossTune
    {
        public const double AttackIntervalMin = 4.2, AttackIntervalMax = 6.2;
        public const double AttackLeadMin = 1.1, AttackLeadMax = 1.8;
        public const double DashChance = .22, DashSpeedMul = 4.4, DashDuration = .55, DashDamageMul = 1.6;
        public const double DashCeilingChance = .2;
        public const double MuzzleOffsetX = .92, MuzzleOffsetY = .27;
        public const double DashKnockMinTiles = 2, DashKnockMaxTiles = 4, DashStunChance = .3, DashStunTime = .5;
        public const double Power = 10;           // INF_BOSS_POWER
        public const double SizeMul = 2.5;        // INF_BOSS_SIZE_MUL
        public const double WallHpMul = 5;        // INF_BOSS_WALL_HP_MUL
    }

    /// <summary>예고형 보스 탄 — 발사 지점에서 목표 지점까지 flight 초 동안 보간되고 착탄 원 안이면 피해.</summary>
    public sealed class BossShot
    {
        public Vec2 Start, Target;
        public double T, Flight, Radius, Power;
        public string VisualId;
        public double Progress => Math.Min(1, T / Math.Max(0.01, Flight));
    }

    /// <summary>보스 전용 상태. <see cref="EnemyState"/>(IsBoss) 에 붙는다.</summary>
    public sealed class BossState
    {
        public EnemyState Body;
        public BossTier Tier;
        public BossTierDef Def;
        public int Facing = -1;

        public readonly List<int> ArmorCells = new List<int>();
        public int ArmorStage;

        public double PatternCd, AttackCd, WallGimmickCd = double.NaN;
        public int ForcedRanged;
        public double FireBreathT;
        public string LastAttack = "";
        public double LastLead;

        // 돌진
        public BossDashPhase Dash;
        public double DashT, DashLeadTotal, DashDurationTotal;
        public Vec2 DashDir;
        public bool CeilingPending, CeilingTriggered;
        public double CeilingAt;
        public readonly HashSet<EnemyState> DashHits = new HashSet<EnemyState>();
        public bool DashHitPlayer;

        // 배회
        public BossWanderPhase Wander;
        public double WanderT;
        public Vec2 WanderTarget;
        public bool WanderStarted;

        public double HpRatio => Body.HpMax > 0 ? Body.Hp / Body.HpMax : 0;
        public double DashWindupProgress => Dash == BossDashPhase.Windup && DashLeadTotal > 0 ? 1 - DashT / DashLeadTotal : 0;
    }

    public struct BossSpawnedEvent { public BossState Boss; }
    public struct BossPatternEvent { public BossState Boss; public string Pattern; public Vec2 At; }
    public struct BossWallEvent { public int Col, Row; public bool Hard; }
    public struct BossShotHitEvent { public Vec2 At; public double Radius; public bool HitPlayer; public double Damage; }
    public struct BossDefeatedEvent { public BossState Boss; public int CoreReward; }

    /// <summary>
    /// 보스 — 원본 <c>infSpawnBoss · infBossTick · infBossStartPattern · updateBossWander · bossDash* ·
    /// infBossWall* · infBossProjectile* · infBossDefeated</c>(13245~13524, 6335~6403, 6834~6885) 를 옮겼다.
    ///
    /// 보스는 <see cref="EnemySystem"/> 의 일반 AI·충돌을 타지 않는다. 대신 이 시스템이 이동·돌진·벽 파쇄를
    /// 맡고 EnemySystem 은 <see cref="EnemySystem.BossMove"/> 훅으로 위임한다.
    /// 연출(링·텍스트·먼지)은 전부 이벤트로 넘긴다.
    /// </summary>
    public sealed class BossSystem
    {
        readonly WorldGrid _world;
        readonly EnemySystem _enemies;
        readonly RunState _run;
        readonly Rng _rng;

        public BossState Boss { get; private set; }
        public bool Active => Boss != null && Boss.Body.Alive;

        public readonly List<BossShot> Shots = new List<BossShot>();
        readonly List<QueuedShot> _shotQueue = new List<QueuedShot>();
        readonly List<QueuedWall> _wallQueue = new List<QueuedWall>();
        /// <summary>보스가 소환한 벽 — 렌더에서 붉게 강조한다.</summary>
        public readonly HashSet<int> WallCells = new HashSet<int>();

        sealed class QueuedShot { public Vec2 Target; public double Delay, Flight, Radius, Power; public string VisualId; }
        sealed class Budget { public int Left; }
        sealed class QueuedWall { public int C, R; public double Delay; public Budget Budget; public bool Convert; }

        public event Action<BossSpawnedEvent> Spawned;
        public event Action<BossPatternEvent> Pattern;
        public event Action<BossWallEvent> WallRaised;
        public event Action<BossShotHitEvent> ShotHit;
        public event Action<BossDefeatedEvent> Defeated;

        public BossSystem(WorldGrid world, EnemySystem enemies, RunState run, uint seed = 0xB055)
        {
            _world = world; _enemies = enemies; _run = run;
            _rng = new Rng(seed);
            _enemies.BossMove = MoveBoss;
        }

        // ───────────────────────────── 소환 (원본 infSpawnBoss)
        public BossState Spawn(PlayerState player, int depth)
        {
            var tier = BossTierDef.TierForDepth(depth);
            var td = BossTierDef.For(tier);
            var spot = PickFarOpenSpot(player.Position, 5.0) ?? (player.Position + new Vec2(4, 0));

            double hp = SimTuning.EnemyHp * (11.2 + Math.Max(0, depth - 1) * 2.2 * Planet.GrowthScale)
                        * _run.EnemyHpMul * BossTune.Power * td.HpMul;
            var body = new EnemyState
            {
                Position = spot, Home = spot, Kind = EnemyKind.BroodBeast,
                Radius = SimTuning.EnemyRadius * 3.65 * td.SizeMul * BossTune.SizeMul,
                Hp = hp, HpMax = hp, ThreatHpMul = 1 + Math.Max(0, _run.Threat - 1) * 0.55,
                IsBoss = true, IsApex = false, Ai = EnemyAi.Chase, LostTime = 0,
                SpeedMul = 1.35 * td.SpeedMul, DamageMul = 1.55 * BossTune.Power * td.DmgMul,
                JumpCooldown = double.PositiveInfinity, BlinkCooldown = 2.5,
            };
            _enemies.AddExternal(body);

            Boss = new BossState
            {
                Body = body, Tier = tier, Def = td,
                PatternCd = 4.2,
                AttackCd = Range(BossTune.AttackIntervalMin, BossTune.AttackIntervalMax),
            };
            if (tier != BossTier.Guardian) SpawnArmor(6);   // 수호자는 장갑 없음 — §11.1 위계 차별화

            _run.BossSpawned = true;
            _run.BossActive = true;
            Spawned?.Invoke(new BossSpawnedEvent { Boss = Boss });
            return Boss;
        }

        Vec2? PickFarOpenSpot(Vec2 from, double minDist)
        {
            Vec2? best = null; double bd = -1;
            for (int tries = 0; tries < 500; tries++)
            {
                int c = 2 + (int)(_rng.NextDouble() * (_world.Cols - 4)), r = 2 + (int)(_rng.NextDouble() * (_world.Rows - 4));
                if (_world.IsSolid(c, r)) continue;
                var p = WorldGrid.CellCenter(c, r);
                double d = Vec2.Distance(p, from);
                if (d < minDist) continue;
                if (d > bd) { bd = d; best = p; }
            }
            if (best.HasValue) return best;
            for (int r = 2; r < _world.Rows - 2; r++) for (int c = 2; c < _world.Cols - 2; c++)
            {
                if (_world.IsSolid(c, r)) continue;
                var p = WorldGrid.CellCenter(c, r);
                double d = Vec2.Distance(p, from);
                if (d > bd) { bd = d; best = p; }
            }
            return best;
        }

        // ───────────────────────────── 틱 (원본 infBossTick)
        public void Tick(PlayerState player, double dt, int depth)
        {
            TickShotQueue(player, dt);
            TickWallQueue(player, dt);

            var b = Boss;
            if (b == null) return;
            if (!b.Body.Alive) { OnDefeated(player, depth); return; }

            bool wasBreathing = b.FireBreathT > 0;
            b.FireBreathT = Math.Max(0, b.FireBreathT - dt);

            b.PatternCd -= dt;
            b.AttackCd -= dt;
            if (double.IsNaN(b.WallGimmickCd)) b.WallGimmickCd = 6 + _rng.NextDouble() * 4;
            b.WallGimmickCd -= dt;
            double ratio = b.HpRatio;

            // 장갑 페이즈 — 중심부 보스·변종 전용
            if (b.Tier != BossTier.Guardian)
            {
                if (ratio <= .66 && b.ArmorStage < 1) { b.ArmorStage = 1; SpawnArmor(6); Summon(2); }
                else if (ratio <= .33 && b.ArmorStage < 2) { b.ArmorStage = 2; SpawnArmor(8); Summon(2); }
            }

            // 소환
            if (b.PatternCd <= 0)
            {
                b.PatternCd = Math.Max(2.4, 5.96 - Math.Max(0, depth - 1) * .24 * Planet.GrowthScale);
                int count = Math.Max(1, JsMath.Round(Math.Min(5, 1 + Math.Floor((depth + 1) / 2.0)) * b.Def.SummonMul));
                Summon(count);
                Pattern?.Invoke(new BossPatternEvent { Boss = b, Pattern = "summon", At = b.Body.Position });
            }

            bool wallBusy = _wallQueue.Count > 0;
            if (b.WallGimmickCd <= 0 && b.FireBreathT <= 0 && b.Dash == BossDashPhase.None && !wallBusy && b.ForcedRanged <= 0)
            {
                b.WallGimmickCd = 9 + _rng.NextDouble() * 6;
                StartWallGimmick(player);
                return;
            }
            if (b.AttackCd <= 0 && b.FireBreathT <= 0 && b.Dash == BossDashPhase.None)
            {
                if (b.ForcedRanged > 0)
                {
                    b.ForcedRanged--;
                    b.AttackCd = Math.Max(.1, 1.0 + _rng.NextDouble() * .25);
                    StartPattern(player, depth, forced: "ranged");
                }
                else
                {
                    b.AttackCd = Math.Max(.1, Range(BossTune.AttackIntervalMin, BossTune.AttackIntervalMax));
                    StartPattern(player, depth, forced: null);
                }
            }
        }

        void Summon(int n) { for (int i = 0; i < n; i++) _enemies.Spawn(Boss.Body.Position); }

        double Range(double a, double b) => a + _rng.NextDouble() * (b - a);

        // ───────────────────────────── 패턴 (원본 infBossStartPattern)
        void StartPattern(PlayerState player, int depth, string forced)
        {
            var b = Boss; var e = b.Body;
            double lead = Math.Max(.05, Range(BossTune.AttackLeadMin, BossTune.AttackLeadMax));
            bool dash = forced == "dash" || (forced != "ranged" && _rng.NextDouble() < BossTune.DashChance + b.Def.DashBonus);
            b.LastLead = lead; b.LastAttack = dash ? "dash" : "ranged";
            b.FireBreathT = 0; b.Dash = BossDashPhase.None; e.Velocity = Vec2.Zero;

            if (dash) { BeginDash(player, lead); return; }

            // 예고 사격 — 플레이어의 0.45초 뒤 위치를 노린다
            const double targetLead = .45;
            var aim = player.Position + player.Velocity * targetLead;
            b.Facing = aim.X >= e.Position.X ? 1 : -1;
            const double baseLead = 1.45, baseDuration = 2.6;
            double animRate = baseLead / lead;
            b.FireBreathT = baseDuration / animRate;

            // 기 모으기 — 예고가 길수록 크고 세다 (최대 1.25)
            double chargePow = Math.Max(.6, Math.Min(1.25, lead / BossTune.AttackLeadMin));
            bool scatter = _rng.NextDouble() < .48;
            int count = Math.Max(2, JsMath.Round((scatter ? Math.Min(10, 5 + depth) : Math.Min(8, 3 + depth)) * b.Def.ProjMul));

            if (scatter)
            {
                for (int i = 0; i < count; i++)
                {
                    double a = _rng.NextDouble() * Math.PI * 2, r = .45 + _rng.NextDouble() * 2.45;
                    QueueShot(aim + Vec2.FromAngle(a) * r, lead + _rng.NextDouble() * .22, 1.8 + _rng.NextDouble() * .38,
                              (.95 + _rng.NextDouble() * .28) * chargePow, "bossScatter", chargePow);
                }
                Pattern?.Invoke(new BossPatternEvent { Boss = b, Pattern = "scatter", At = e.Position });
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    double side = (i - (count - 1) / 2.0) * .32;
                    QueueShot(aim + new Vec2(side, (_rng.NextDouble() - .5) * .55), lead + i * .30, 1.72 + _rng.NextDouble() * .22,
                              1.08 * chargePow, "bossBarrage", chargePow);
                }
                Pattern?.Invoke(new BossPatternEvent { Boss = b, Pattern = "barrage", At = e.Position });
            }
        }

        public Vec2 MouthPosition(BossState b)
            => b.Body.Position + new Vec2(b.Facing * b.Body.Radius * BossTune.MuzzleOffsetX, b.Body.Radius * BossTune.MuzzleOffsetY);

        void QueueShot(Vec2 target, double delay, double flight, double rad, string visualId, double power)
        {
            target = new Vec2(JsMath.Clamp(target.X, 1, _world.Cols - 1), JsMath.Clamp(target.Y, 1, _world.Rows - 1));
            _shotQueue.Add(new QueuedShot { Target = target, Delay = Math.Max(0, delay), Flight = flight, Radius = rad, VisualId = visualId, Power = Math.Max(.5, power) });
        }

        void TickShotQueue(PlayerState player, double dt)
        {
            for (int i = _shotQueue.Count - 1; i >= 0; i--)
            {
                var q = _shotQueue[i];
                q.Delay -= dt;
                if (q.Delay > 0) continue;
                _shotQueue.RemoveAt(i);
                if (!Active) continue;
                Shots.Add(new BossShot { Start = MouthPosition(Boss), Target = q.Target, Flight = q.Flight, Radius = q.Radius, VisualId = q.VisualId, Power = q.Power });
            }
            for (int i = Shots.Count - 1; i >= 0; i--)
            {
                var s = Shots[i];
                s.T += dt;
                if (s.T < s.Flight) continue;
                Shots.RemoveAt(i);
                ShotImpact(player, s);
            }
        }

        /// <summary>원본 infBossProjectileHit — 착탄 원 안이면 피해. 원본은 ×0.85 경감을 거치지 않는다.</summary>
        void ShotImpact(PlayerState player, BossShot s)
        {
            double d = Vec2.Distance(player.Position, s.Target);
            bool hit = d <= s.Radius && player.IFrames <= 0 && !player.Downed;
            double dmg = 0;
            if (hit)
            {
                dmg = Math.Max(9, JsMath.Round(SimTuning.EnemyDamage * (3.55 + Math.Max(0, _run.Depth - 1) * .35 * Planet.GrowthScale)));
                dmg = JsMath.Round(dmg * s.Power);
                var dir = (s.Target - s.Start).Normalized;
                _enemies.DamagePlayerDirect(player, dmg, dir);
            }
            ShotHit?.Invoke(new BossShotHitEvent { At = s.Target, Radius = s.Radius, HitPlayer = hit, Damage = dmg });
        }

        // ───────────────────────────── 장갑 (원본 infBossSpawnArmor)
        void SpawnArmor(int want)
        {
            var b = Boss; var e = b.Body;
            var (c, r) = WorldGrid.ToCell(e.Position);
            double ringMin = Math.Max(1.7, e.Radius + .7), ringMax = ringMin + 1.55;
            int span = (int)Math.Ceiling(ringMax);
            var cand = new List<(int dc, int dr)>();
            for (int dr = -span; dr <= span; dr++) for (int dc = -span; dc <= span; dc++)
            {
                double d = Math.Sqrt(dc * dc + dr * dr);
                if (d >= ringMin && d <= ringMax) cand.Add((dc, dr));
            }
            Shuffle(cand);
            int made = 0;
            foreach (var (dc, dr) in cand)
            {
                if (made >= want) break;
                int cc = c + dc, rr = r + dr;
                if (!_world.InInterior(cc, rr)) continue;
                int k = _world.Index(cc, rr);
                if (_world.IsSolid(cc, rr) || b.ArmorCells.Contains(k)) continue;
                _world.SetTile(cc, rr, TileType.Stone);
                b.ArmorCells.Add(k);
                made++;
            }
            Pattern?.Invoke(new BossPatternEvent { Boss = b, Pattern = "armor", At = e.Position });
        }

        public int ArmorAlive()
        {
            if (Boss == null) return 0;
            Boss.ArmorCells.RemoveAll(k => _world.AtIndex(k) == TileType.Empty);
            return Boss.ArmorCells.Count;
        }

        void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = (int)(_rng.NextDouble() * (i + 1));
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // ───────────────────────────── 벽 기믹 (원본 infBossWall*)
        void StartWallGimmick(PlayerState player)
        {
            double roll = _rng.NextDouble();
            if (roll < .40) WallField(player);
            else if (roll < .75) WallWave(player);
            else WallPrison(player);
        }

        void CastAnim(double dur)
        {
            var b = Boss;
            b.FireBreathT = dur;
            b.AttackCd = Math.Max(b.AttackCd, dur + .12);
        }

        /// <summary>패턴 1 — 주변 두꺼운 벽 뭉치 + 원거리 3연발.</summary>
        void WallField(PlayerState player)
        {
            var b = Boss; var e = b.Body;
            var budget = new Budget { Left = 1 + JsMath.Round((1 - b.HpRatio) * 3) };
            var (bc, br) = WorldGrid.ToCell(e.Position);
            int clusters = 2 + (int)(_rng.NextDouble() * 3);
            for (int n = 0; n < clusters; n++)
            {
                double a = _rng.NextDouble() * Math.PI * 2, dist = e.Radius + .8 + _rng.NextDouble() * 3.2;
                int oc = bc + JsMath.Round(Math.Cos(a) * dist), or = br + JsMath.Round(Math.Sin(a) * dist);
                double rad = 1.15 + _rng.NextDouble() * .75;
                for (int dr = -2; dr <= 2; dr++) for (int dc = -2; dc <= 2; dc++)
                {
                    double d = Math.Sqrt(dc * dc + dr * dr) + (_rng.NextDouble() - .5) * .6;
                    if (d > rad) continue;
                    if (_rng.NextDouble() < .18) continue;
                    QueueWall(oc + dc, or + dr, .06 + n * .07 + _rng.NextDouble() * .15, budget, false);
                }
            }
            CastAnim(.58);
            b.ForcedRanged = 3;
            b.Facing = player.Position.X >= e.Position.X ? 1 : -1;
            Pattern?.Invoke(new BossPatternEvent { Boss = b, Pattern = "wallField", At = e.Position });
        }

        /// <summary>패턴 2 — 파도 장벽 (1초에 훑음) + 원거리 1회.</summary>
        void WallWave(PlayerState player)
        {
            var b = Boss; var e = b.Body;
            var budget = new Budget { Left = 1 + JsMath.Round((1 - b.HpRatio) * 3) };
            double ang = (player.Position - e.Position).Angle;
            int startD = (int)Math.Ceiling(e.Radius) + 1;
            double lenToPlayer = Vec2.Distance(player.Position, e.Position);
            int len = Math.Max(22, Math.Min(48, JsMath.Round((lenToPlayer + 5) * 2)));
            var (bc, br) = WorldGrid.ToCell(e.Position);
            double cosA = Math.Cos(ang), sinA = Math.Sin(ang), pxA = -sinA, pyA = cosA;
            int steps = len - startD;
            const double WaveTotal = 1.0;
            for (int i = 0; i < steps; i++)
            {
                double t = steps > 1 ? (double)i / (steps - 1) : 0, delay = .05 + WaveTotal * t;
                double drift = (_rng.NextDouble() - .5) * .9;
                double cx0 = bc + cosA * (startD + i) + pxA * drift, cy0 = br + sinA * (startD + i) + pyA * drift;
                foreach (double off in new[] { -.55, .55 })
                {
                    if (_rng.NextDouble() < .10) continue;
                    int cc = JsMath.Round(cx0 + pxA * off + (_rng.NextDouble() - .5) * .4), rr = JsMath.Round(cy0 + pyA * off + (_rng.NextDouble() - .5) * .4);
                    QueueWall(cc, rr, delay + _rng.NextDouble() * .06, budget, convert: true);
                }
            }
            CastAnim(WaveTotal + .12);
            b.ForcedRanged = 1;
            b.Facing = player.Position.X >= e.Position.X ? 1 : -1;
            Pattern?.Invoke(new BossPatternEvent { Boss = b, Pattern = "wallWave", At = e.Position });
        }

        /// <summary>패턴 3 — 플레이어를 가두는 원형 방벽 + 원거리 1회.</summary>
        void WallPrison(PlayerState player)
        {
            var b = Boss; var e = b.Body;
            var budget = new Budget { Left = 1 + JsMath.Round((1 - b.HpRatio) * 2) };
            var (pc, pr) = WorldGrid.ToCell(player.Position);
            const double radius = 2.8;
            int segs = JsMath.Round(Math.PI * 2 * radius * 1.4);
            double a0 = _rng.NextDouble() * Math.PI * 2;
            var seen = new HashSet<(int, int)>();
            for (int i = 0; i < segs; i++)
            {
                double a = a0 + Math.PI * 2 * i / segs;
                double rj = radius + (_rng.NextDouble() - .5) * .7;
                int cc = pc + JsMath.Round(Math.Cos(a) * rj), rr = pr + JsMath.Round(Math.Sin(a) * rj);
                if (!seen.Add((cc, rr))) continue;
                if (_rng.NextDouble() < .08) continue;
                if (Math.Sqrt((cc - pc) * (cc - pc) + (rr - pr) * (rr - pr)) < 1.9) continue;
                QueueWall(cc, rr, .08 + (double)i / segs * .28 + _rng.NextDouble() * .04, budget, false);
            }
            CastAnim(.6);
            b.ForcedRanged = 1;
            b.Facing = player.Position.X >= e.Position.X ? 1 : -1;
            Pattern?.Invoke(new BossPatternEvent { Boss = b, Pattern = "wallPrison", At = player.Position });
        }

        /// <summary>체력 30% 이하 돌진 — 진로 양쪽에 협곡 벽을 세워 가둔다 (원본 infBossDashPrison).</summary>
        void DashPrison(PlayerState player, double lead)
        {
            var b = Boss; var e = b.Body;
            if (b.HpRatio > .3) return;
            var budget = new Budget { Left = 2 };
            var dir = b.DashDir;
            if (dir.SqrLength < 1e-9) return;
            double pxA = -dir.Y, pyA = dir.X;
            var (bc, br) = WorldGrid.ToCell(e.Position);
            int startD = (int)Math.Ceiling(e.Radius) + 1;
            double distToPlayer = Vec2.Distance(player.Position, e.Position);
            int len = Math.Max(startD + 5, Math.Min(28, JsMath.Round(distToPlayer + 4)));
            double halfW = Math.Max(2.2, e.Radius * .92 + 1.4);
            double total = Math.Max(.15, Math.Min(.6, lead * .55));
            for (int i = startD; i <= len; i++)
            {
                double t = (double)(i - startD) / Math.Max(1, len - startD);
                foreach (int side in new[] { -1, 1 })
                {
                    double off = halfW * side + (_rng.NextDouble() - .5) * .5;
                    int cc = JsMath.Round(bc + dir.X * i + pxA * off), rr = JsMath.Round(br + dir.Y * i + pyA * off);
                    if (_rng.NextDouble() < .06) continue;
                    QueueWall(cc, rr, .05 + total * t + _rng.NextDouble() * .05, budget, false);
                }
            }
            Pattern?.Invoke(new BossPatternEvent { Boss = b, Pattern = "dashPrison", At = player.Position });
        }

        void QueueWall(int c, int r, double delay, Budget budget, bool convert)
            => _wallQueue.Add(new QueuedWall { C = c, R = r, Delay = Math.Max(0, delay), Budget = budget, Convert = convert });

        void TickWallQueue(PlayerState player, double dt)
        {
            for (int i = 0; i < _wallQueue.Count; i++)
            {
                var w = _wallQueue[i];
                w.Delay -= dt;
                if (w.Delay > 0) continue;
                _wallQueue.RemoveAt(i); i--;
                if (!Active) continue;
                Materialize(player, w);
            }
        }

        double HardChance() => .05 + (1 - (Boss?.HpRatio ?? 1)) * .20;

        /// <summary>원본 infBossWallMaterialize — 규칙 5: 있는 벽은 건드리지 않음(파동만 전환), 플레이어·보스 몸 위 금지.</summary>
        void Materialize(PlayerState player, QueuedWall w)
        {
            if (!_world.InInterior(w.C, w.R)) return;
            int k = _world.Index(w.C, w.R);
            var t0 = _world.AtIndex(k);
            if (t0 != TileType.Empty)
            {
                if (!w.Convert) return;
                if (TileTypes.IsBedrock(t0) || t0 == TileType.Ore || t0 == TileType.Gem || t0 == TileType.Crys) return;
                if (Boss.ArmorCells.Contains(k)) return;
            }
            var p = WorldGrid.CellCenter(w.C, w.R);
            if (Vec2.Distance(player.Position, p) < 1.05) return;
            var e = Boss.Body;
            if (Vec2.Distance(e.Position, p) < e.Radius + .55) return;

            bool hard = false;
            if (w.Budget != null && w.Budget.Left > 0 && _rng.NextDouble() < HardChance()) { hard = true; w.Budget.Left--; }

            if (hard) _world.SetTile(w.C, w.R, TileType.Rock);
            else _world.SetTile(w.C, w.R, TileType.Stone, TileTypes.BaseHp(TileType.Stone) * _run.WallHpMul * BossTune.WallHpMul);
            WallCells.Add(k);
            WallRaised?.Invoke(new BossWallEvent { Col = w.C, Row = w.R, Hard = hard });
        }

        // ───────────────────────────── 돌진 (원본 bossBeginDash · updateBossWander · bossDashImpact)
        void BeginDash(PlayerState player, double lead)
        {
            var b = Boss; var e = b.Body;
            var d = player.Position - e.Position;
            double dist = Math.Max(1e-6, d.Length);
            b.Dash = BossDashPhase.Windup;
            b.DashT = Math.Max(.05, lead); b.DashLeadTotal = b.DashT;
            b.DashDir = d / dist;
            b.CeilingPending = _rng.NextDouble() < BossTune.DashCeilingChance;
            b.CeilingTriggered = false;
            b.CeilingAt = .18 + _rng.NextDouble() * .5;
            b.Facing = b.DashDir.X >= 0 ? 1 : -1;
            e.Velocity = Vec2.Zero;
            b.Wander = BossWanderPhase.Idle;
            b.DashHits.Clear(); b.DashHitPlayer = false;
            DashPrison(player, lead);
            Pattern?.Invoke(new BossPatternEvent { Boss = b, Pattern = "dashWindup", At = e.Position });
        }

        /// <summary>EnemySystem 이 보스 개체에 대해 부르는 이동 훅. 일반 AI·충돌 대신 이것이 속도를 정한다.</summary>
        void MoveBoss(EnemyState e, PlayerState player, double dt, double baseSpeed, double timeSpeed)
        {
            var b = Boss;
            if (b == null || b.Body != e) return;

            if (b.Dash == BossDashPhase.Windup)
            {
                e.Velocity = Vec2.Zero;
                b.DashT -= dt;
                if (b.DashT <= 0)
                {
                    b.Dash = BossDashPhase.Charge;
                    b.DashT = Math.Max(.08, BossTune.DashDuration);
                    b.DashDurationTotal = b.DashT;
                    Pattern?.Invoke(new BossPatternEvent { Boss = b, Pattern = "dashCharge", At = e.Position });
                }
                return;
            }
            if (b.Dash == BossDashPhase.Charge)
            {
                b.DashT -= dt;
                double mul = Math.Max(.1, e.SpeedMul) * BossTune.DashSpeedMul;
                e.Velocity = b.DashDir * (baseSpeed * mul);
                DashImpact(player);
                if (b.DashT <= 0)
                {
                    b.Dash = BossDashPhase.None;
                    Pattern?.Invoke(new BossPatternEvent { Boss = b, Pattern = "dashEnd", At = e.Position });
                    BeginWanderIdle();
                }
                return;
            }
            if (b.FireBreathT > 0) { e.Velocity = Vec2.Zero; ContactDamage(player); return; }

            // 배회 — 원본 bossBeginWanderIdle / Walk
            if (!b.WanderStarted) { b.WanderStarted = true; BeginWanderIdle(); }
            b.WanderT -= dt;
            if (b.Wander == BossWanderPhase.Idle)
            {
                e.Velocity = Vec2.Zero;
                if (b.WanderT <= 0) BeginWanderWalk();
                ContactDamage(player);
                return;
            }
            var d = b.WanderTarget - e.Position;
            double dist = d.Length;
            if (b.WanderT <= 0 || dist < .45) { BeginWanderIdle(); return; }
            double unit = Math.Min(1.65, .76 * e.SpeedMul * timeSpeed);
            e.Velocity = d / Math.Max(1e-6, dist) * (baseSpeed * unit);
            if (Math.Abs(e.Velocity.X) > .4 / 9) b.Facing = e.Velocity.X > 0 ? 1 : -1;
            ContactDamage(player);
        }

        void BeginWanderIdle()
        {
            var b = Boss;
            b.Wander = BossWanderPhase.Idle;
            b.WanderT = 2.2 + _rng.NextDouble() * 3.2;
            b.Body.Velocity = Vec2.Zero;
        }
        void BeginWanderWalk()
        {
            var b = Boss; var e = b.Body;
            const double margin = 3;
            double a = _rng.NextDouble() * Math.PI * 2, dist = 2.5 + _rng.NextDouble() * 5;
            b.Wander = BossWanderPhase.Walk;
            b.WanderT = 2.6 + _rng.NextDouble() * 3.8;
            b.WanderTarget = new Vec2(
                JsMath.Clamp(e.Position.X + Math.Cos(a) * dist, margin, _world.Cols - margin),
                JsMath.Clamp(e.Position.Y + Math.Sin(a) * dist, margin, _world.Rows - margin));
        }

        /// <summary>보스 몸에 닿으면 접촉 피해 (원본 touch 판정 + e.cd).</summary>
        void ContactDamage(PlayerState player)
        {
            var e = Boss.Body;
            if (e.ContactCooldown > 0) return;
            var d = player.Position - e.Position;
            if (d.Length >= e.Radius + SimTuning.PlayerRadius) return;
            e.ContactCooldown = 0.9;
            _enemies.ApplyPlayerDamage(player, SimTuning.EnemyDamage * e.DamageMul, d.Normalized);
        }

        /// <summary>원본 bossDashImpact — 돌진 한 번에 같은 대상은 한 번만 맞는다.</summary>
        void DashImpact(PlayerState player)
        {
            var b = Boss; var e = b.Body;
            Vec2 Blend(Vec2 o, double d)
            {
                var n = d < 1 ? b.DashDir : o / d;
                n = n * .55 + b.DashDir * .75;
                return n.Normalized;
            }
            foreach (var o in _enemies.Enemies)
            {
                if (o == e || o.IsBoss || !o.Alive || b.DashHits.Contains(o)) continue;
                var od = o.Position - e.Position; double d = od.Length;
                if (d > e.Radius + o.Radius) continue;
                b.DashHits.Add(o);
                LaunchEnemy(o, Blend(od, d));
            }
            if (!b.DashHitPlayer)
            {
                var pd = player.Position - e.Position; double d = pd.Length;
                if (d < e.Radius + SimTuning.PlayerRadius)
                {
                    b.DashHitPlayer = true;
                    var hitDir = d < 1 ? b.DashDir : pd / d;
                    _enemies.ApplyPlayerDamage(player, SimTuning.EnemyDamage * e.DamageMul * BossTune.DashDamageMul, hitDir);
                    LaunchPlayer(player, Blend(pd, d));
                }
            }
        }

        double LaunchSpeed() => (BossTune.DashKnockMinTiles + _rng.NextDouble() * (BossTune.DashKnockMaxTiles - BossTune.DashKnockMinTiles)) * SimTuning.KnockDrag;
        double LaunchStun() => _rng.NextDouble() < BossTune.DashStunChance ? BossTune.DashStunTime : 0;

        void LaunchEnemy(EnemyState o, Vec2 n)
        {
            o.Knock = n * LaunchSpeed();
            o.Velocity = Vec2.Zero; o.Attack = AttackPhase.None; o.JumpTime = 0;
            double stun = LaunchStun();
            if (stun > 0) o.StunTime = Math.Max(o.StunTime, stun);
        }
        void LaunchPlayer(PlayerState p, Vec2 n)
        {
            p.Knock = n * LaunchSpeed();
            double stun = LaunchStun();
            if (stun > 0) p.StunTime = Math.Max(p.StunTime, stun);
        }

        /// <summary>원본 bossCrushWalls — 몸통 반경 안의 벽을 소리 없이 부순다 (장악도·XP 에 안 잡힌다).</summary>
        public void CrushWalls()
        {
            var b = Boss; if (b == null) return; var e = b.Body;
            double radius = Math.Max(.8, e.Radius * .92);
            int minC = Math.Max(2, (int)Math.Floor(e.Position.X - radius)), maxC = Math.Min(_world.Cols - 3, (int)Math.Floor(e.Position.X + radius));
            int minR = Math.Max(2, (int)Math.Floor(e.Position.Y - radius)), maxR = Math.Min(_world.Rows - 3, (int)Math.Floor(e.Position.Y + radius));
            for (int r = minR; r <= maxR; r++) for (int c = minC; c <= maxC; c++)
            {
                int k = _world.Index(c, r);
                if (_world.AtIndex(k) == TileType.Empty || b.ArmorCells.Contains(k)) continue;
                if (Vec2.Distance(WorldGrid.CellCenter(c, r), e.Position) > radius + .55) continue;
                _world.ClearSilent(c, r);
                WallCells.Remove(k);
            }
        }

        // ───────────────────────────── 격파 (원본 infBossDefeated)
        void OnDefeated(PlayerState player, int depth)
        {
            var b = Boss;
            Boss = null;
            Shots.Clear(); _shotQueue.Clear(); _wallQueue.Clear();
            _run.BossActive = false;
            _run.BossesKilled++;
            int core = b.Def.CoreBase + depth;
            player.Hp = Math.Min(player.HpMax, player.Hp + player.HpMax * .3);
            Defeated?.Invoke(new BossDefeatedEvent { Boss = b, CoreReward = core });
        }

        public void Clear()
        {
            Boss = null;
            Shots.Clear(); _shotQueue.Clear(); _wallQueue.Clear(); WallCells.Clear();
        }
    }
}
