using System;
using System.Collections.Generic;

namespace TunnelCrew.Sim
{
    /// <summary>
    /// 한 층의 시뮬레이션 전체. Presentation 은 이 클래스 하나만 붙잡으면 된다.
    ///
    /// 원본 <c>update(dt)</c> (7096~7420행) 의 실행 순서를 지킨다.
    /// 고정 60Hz 로 돌며(계획 D6), 히트스톱은 Sim 밖에서 <c>Time.timeScale</c> 로 처리하므로
    /// 여기에는 들어오지 않는다.
    /// </summary>
    public sealed class TunnelSim
    {
        public WorldGrid World { get; private set; }
        public PlayerState Player { get; } = new PlayerState();
        public LootSystem Loot { get; }
        /// <summary>시야. 층마다 새로 만든다.</summary>
        public LosService Los { get; private set; }
        /// <summary>적. 층마다 새로 만든다.</summary>
        public EnemySystem Enemies { get; private set; }
        /// <summary>플레이어 탄.</summary>
        public ProjectileSystem Projectiles { get; private set; }
        /// <summary>직업 스킬·설치물.</summary>
        public RoleSystem Roles { get; private set; }
        /// <summary>보스 — 소환·패턴·벽 기믹·격파.</summary>
        public BossSystem Bosses { get; private set; }
        /// <summary>탈출 포트.</summary>
        public EscapeSystem Escape { get; private set; }
        /// <summary>런 상태. Playing 에서만 틱이 돈다. Rest/Result 전환은 Sim 이 정하고 화면은 Presentation 이 맡는다.</summary>
        public GamePhase Phase { get; private set; } = GamePhase.Playing;
        /// <summary>보스 격파 직후 — 연출이 끝나면 Presentation 이 <see cref="EnterRest"/> 를 부른다.</summary>
        public bool RestPending { get; private set; }
        public BossTier LastBossTier { get; private set; }
        /// <summary>런 종료 결과.</summary>
        public bool RunEscaped { get; private set; }
        public string RunEndReason { get; private set; } = "";
        /// <summary>특성 덱 — 레벨업 카드·휴식 전설.</summary>
        public TraitDeck Traits { get; } = new TraitDeck();
        TraitContext TraitCtx => new TraitContext { Build = Build, Player = Player, Roles = Roles };
        /// <summary>층 진행 — 장악도·위협·스폰 압력.</summary>
        public RunState Run { get; } = new RunState();
        /// <summary>경험치 단일 관문.</summary>
        public XpGate Xp { get; } = new XpGate();
        /// <summary>런 빌드 — 직업·특성이 바꾸는 수치.</summary>
        public PlayerBuild Build { get; } = new PlayerBuild();
        /// <summary>이번 층의 생성 결과. 랜턴·소품 배치를 Presentation 이 읽는다.</summary>
        public DungeonResult Generation { get; private set; }

        public int Depth { get; private set; }
        /// <summary>런 누적 시간(초). 히트스톱과 무관하다 (원본 G.rt).</summary>
        public double RunTime { get; private set; }
        /// <summary>층 누적 시간(초). 위협도 계산에 쓴다 (원본 INF.floorTime).</summary>
        public double FloorTime { get; private set; }

        double _accumulator;
        readonly List<VisionSource> _visionSources = new List<VisionSource>();

        public event Action<TileBrokenEvent> TileBroken;
        public event Action<TileDamagedEvent> TileDamaged;
        public event Action<PlayerDashedEvent> PlayerDashed;
        public event Action<DrillBouncedEvent> DrillBounced;
        public event Action<Vec2, double> DrillBeat;
        public event Action<ResourceCollectedEvent> ResourceCollected;
        public event Action<EnemyHurtEvent> EnemyHurt;
        public event Action<PlayerHurtEvent> PlayerHurt;
        public event Action<EnemySpawnedEvent> EnemySpawned;
        public event Action<ProjectileFiredEvent> ProjectileFired;
        public event Action<ProjectileEndedEvent> ProjectileEnded;
        public event Action<ReloadEvent> ReloadChanged;
        public event Action<SkillEvent> SkillUsed;
        public event Action<BreakerExplodedEvent> BreakerExploded;
        public event Action<FoundationBrokenEvent> FoundationBroken;
        public event Action<XpGainedEvent> XpGained;
        public event Action<LevelUpEvent> LeveledUp;
        /// <summary>장악도 목표 도달 — 보스 소환 시점.</summary>
        public event Action DominanceReached;
        public event Action<BossSpawnedEvent> BossSpawned;
        public event Action<BossPatternEvent> BossPattern;
        public event Action<BossWallEvent> BossWallRaised;
        public event Action<BossShotHitEvent> BossShotHit;
        public event Action<BossDefeatedEvent> BossDefeated;
        public event Action<EscapeEvent> EscapeChanged;
        public event Action<TraitOfferEvent> TraitOffered;
        public event Action<TraitPickedEvent> TraitPicked;
        /// <summary>런 종료 — (탈출 성공 여부, 사유).</summary>
        public event Action<bool, string> RunEnded;

        public TunnelSim(int fxSeed = 12345)
        {
            Loot = new LootSystem(fxSeed);
            Loot.Collected += e => ResourceCollected?.Invoke(e);

            // Run / Xp 는 런 내내 하나다 — 구독은 여기서 한 번만
            Run.DominanceReachedEvent += () =>
            {
                // 장악도 목표 → 보스 소환 (원본 12570 → infSpawnBoss). 다음 틱에 소환해 파괴 콜백 안에서 월드를 바꾸지 않는다.
                _spawnBossPending = true;
                DominanceReached?.Invoke();
            };
            Xp.Gained += e => XpGained?.Invoke(e);
            Xp.LeveledUp += e =>
            {
                // 원본 infOpenLevel — 풀이 비면 예비 보급 (체력 +25% · 코어 +1) 으로 대체하고 플레이를 끊지 않는다
                if (!Traits.OpenLevel(TraitCtx, e.Level))
                {
                    Player.Hp = Math.Min(Player.HpMax, Player.Hp + Player.HpMax * .25);
                    Loot.Core += 1;
                }
                LeveledUp?.Invoke(e);
            };
            Xp.LevelCheckBlocked = () => Run.BossActive || Traits.HasOffer;   // 카드가 열려 있으면 적립만
            Traits.Offered += e => TraitOffered?.Invoke(e);
            Traits.Picked += e =>
            {
                Xp.XpMul = Build.XpMul;
                TraitPicked?.Invoke(e);
                if (_legendPending && Phase == GamePhase.Rest && !Traits.HasOffer) OpenLegendsNow();
            };
        }

        /// <summary>층을 새로 생성하고 플레이어를 진입점에 놓는다. 원본 enterDepth().</summary>
        /// <summary>런 시작 — 직업을 정하고 빌드를 초기화한다.</summary>
        bool _spawnBossPending;
        int _blockCounter;
        bool _blastLock;
        double _endlessBurnTick;

        /// <summary>원본 infTraitBlast — 반경 안 벽에 최대 체력 × power × 감쇠 피해. 기반암 제외.</summary>
        void TraitBlast(int c0, int r0, double radius, double power)
        {
            int rad = Math.Max(1, (int)radius);
            _blastLock = true;
            for (int rr = -rad; rr <= rad; rr++) for (int cc = -rad; cc <= rad; cc++)
            {
                if (cc == 0 && rr == 0) continue;
                double dist = Math.Sqrt(cc * cc + rr * rr);
                if (dist > radius + .15) continue;
                int c = c0 + cc, r = r0 + rr;
                if (!World.InInterior(c, r) || !World.IsSolid(c, r) || World.IsBedrock(c, r)) continue;
                double mul = power * Math.Max(.38, 1 - dist / (radius + 1));
                World.Damage(c, r, World.MaxHp(World.At(c, r)) * mul, new Vec2(cc, rr).Normalized);
            }
            _blastLock = false;
        }

        /// <summary>원본 infRadialBurst — 파편 투사체 n발 (속도 teWorld 245, 수명 .7, 관통 1).</summary>
        void RadialBurst(Vec2 at, int n, double power)
        {
            int count = Math.Max(4, n);
            for (int i = 0; i < count; i++)
            {
                double a = Math.PI * 2 * i / count;
                Projectiles.Projectiles.Add(new Projectile
                {
                    Position = at, Velocity = Vec2.FromAngle(a) * SimTuning.TeCells(245), Life = .7, Pierce = 1,
                    Power = power, VisualId = "shard",
                });
            }
        }

        /// <summary>보스 격파 연출이 끝나면 휴식으로 — 월드 정지 (원본 CREW.phase=infiniteRest).</summary>
        public void EnterRest()
        {
            if (!RestPending) return;
            RestPending = false;
            Phase = GamePhase.Rest;
            // 전설 카드 3택 — 골라야 하강 버튼이 열린다 (원본 restChosen).
            // 보스전 중 쌓인 레벨업 카드가 열려 있으면 그것을 먼저 고르고, 그 다음 전설을 낸다.
            _legendPending = true;
            if (!Traits.HasOffer) OpenLegendsNow();
        }

        bool _legendPending;
        void OpenLegendsNow() { _legendPending = false; Traits.OpenLegends(Xp.Level); }

        /// <summary>휴식 화면에서 전설을 골랐는가 — 하강/귀환 조건.</summary>
        public bool RestChosen => Phase == GamePhase.Rest && !Traits.HasOffer && !_legendPending;

        /// <summary>카드 선택 (레벨업·전설 공통). 1/2/3.</summary>
        public bool PickTrait(int index) => Traits.Pick(index, TraitCtx);
        public bool RerollTraits() => Traits.Reroll(TraitCtx, Xp.Level);

        /// <summary>휴식 → 다음 층 (원본 infNextDepth). 탈출 포트는 두고 간다.</summary>
        public void Descend()
        {
            if (Phase != GamePhase.Rest && Phase != GamePhase.Playing) return;
            if (Phase == GamePhase.Rest && !RestChosen) return;   // 전설을 골라야 내려간다
            EnterDepth(Depth + 1, DungeonConfig.Runtime);
        }

        /// <summary>휴식 → 이 층에 남아 탈출 (원본 infEscapeReturnFromRest). 포트가 없으면 자동 요청.</summary>
        public void ReturnFromRest()
        {
            if (!RestChosen) return;
            Phase = GamePhase.Playing;
            if (!Escape.Active) Escape.AutoSummon(Player, Player.Position, Depth);
        }

        /// <summary>원본 infEndRun — 결과 화면. 솔로라 다운 = 종료 (상호 부활은 크루가 있는 M6 에서).</summary>
        public void EndRun(bool escaped, string reason)
        {
            if (Phase == GamePhase.Result) return;
            Phase = GamePhase.Result;
            RunEscaped = escaped;
            RunEndReason = reason;
            RunEnded?.Invoke(escaped, reason);
        }

        public void StartRun(RoleId role)
        {
            Build.Reset(role);
            Xp.Reset();
            Traits.Reset();
            Run.BossesKilled = 0;
            RunEscaped = false; RunEndReason = "";
            Phase = GamePhase.Playing;
        }

        public void EnterDepth(int depth, DungeonConfig cfg)
        {
            Depth = depth;
            var gen = new DungeonGenerator(cfg).Generate(depth);
            Generation = gen;

            World = new WorldGrid(gen);
            Los = new LosService(World);
            Enemies = new EnemySystem(World);
            Run.InitFloor(depth, World);          // 배율·파괴 가능 블록 수 (원본 infInitFloor)
            Xp.OnFloorInit();
            Traits.OnFloorInit(depth);
            Enemies.EnemyHpMul = Run.EnemyHpMul;
            Enemies.ExtraSpawnCount = Run.TakeSpawnDebt;
            Enemies.EnemyHurt += e =>
            {
                // 보스 격파 집계(BossesKilled · BossActive)는 BossSystem.OnDefeated 가 맡는다
                if (e.Killed) Xp.OnEnemyKilled(e.Enemy, Build.Role, e.ByTurret);
                EnemyHurt?.Invoke(e);
            };
            Enemies.PlayerHurt += e => PlayerHurt?.Invoke(e);
            Enemies.Spawned += e => EnemySpawned?.Invoke(e);
            Projectiles = new ProjectileSystem(World, Enemies);
            Projectiles.Fired += e => ProjectileFired?.Invoke(e);
            Projectiles.Ended += e => ProjectileEnded?.Invoke(e);
            Projectiles.Reload += e => ReloadChanged?.Invoke(e);
            Bosses = new BossSystem(World, Enemies, Run);
            Enemies.BossCrush = Bosses.CrushWalls;
            Bosses.Spawned += e => BossSpawned?.Invoke(e);
            Bosses.Pattern += e => BossPattern?.Invoke(e);
            Bosses.WallRaised += e => { Los?.MarkDirty(); BossWallRaised?.Invoke(e); };
            Bosses.ShotHit += e => BossShotHit?.Invoke(e);
            Bosses.Defeated += e =>
            {
                Loot.Core += e.CoreReward;
                LastBossTier = e.Boss.Tier;
                RestPending = true;
                // §8.4-5 자동 탈출 요청은 중심부 보스·변종 전용. 수호자는 중간 목표라 원정이 계속된다.
                if (e.Boss.Tier != BossTier.Guardian) Escape.AutoSummon(Player, e.Boss.Body.Position, Depth);
                BossDefeated?.Invoke(e);
            };
            Escape = new EscapeSystem(World);
            Escape.Changed += e => EscapeChanged?.Invoke(e);
            Escape.Boarded += () => EndRun(escaped: true, "탈출 포트 탑승");
            Roles = new RoleSystem(World, Enemies, Projectiles, Build);
            Enemies.IncomingDamageMul = () => Roles.ShieldTime > 0 ? 0.35 : 1.0;
            Roles.SkillUsed += e => SkillUsed?.Invoke(e);
            Roles.BreakerExploded += e => BreakerExploded?.Invoke(e);
            Roles.FoundationBroken += e =>
            {
                Los?.MarkDirty();
                Xp.OnFoundationBroken(e.Type, Build.Role, WorldGrid.CellCenter(e.Col, e.Row));
                FoundationBroken?.Invoke(e);
            };
            World.TileBroken += OnTileBroken;
            World.TileDamaged += e => TileDamaged?.Invoke(e);

            Player.Position = World.EntryPosition;
            Player.Velocity = Vec2.Zero;
            Player.AimPoint = Player.Position + new Vec2(1, 0);
            Player.AimReady = false;
            Player.DashActive = false;
            Player.BounceActive = false;
            Player.Knock = Vec2.Zero;
            Player.StunTime = 0;
            Player.DrillWarm = 0;
            Player.DrillHeat = 0;
            Player.DrillHeatLock = 0;

            Loot.Clear();
            Enemies.Clear();
            Projectiles?.Clear();
            Roles?.Clear();
            _spawnBossPending = false;
            _blockCounter = 0;
            RestPending = false;
            Phase = GamePhase.Playing;
            Loot.DepthMul = 1.0 + 0.25 * (depth - 1);

            FloorTime = 0;
            _accumulator = 0;
        }

        void OnTileBroken(TileBrokenEvent e)
        {
            // 벽이 사라지면 시야가 달라진다. 원본은 각 변경 지점이 LOS.markDirty() 를
            // 손으로 불러야 했지만, 여기서는 파괴 이벤트 한 곳에서 처리한다.
            Los?.MarkDirty();

            // 층 진행 — 장악도·스폰 압력·굴착 XP (원본 infOnBlockBroken)
            double cdCap = Run.OnBlockBroken();
            Enemies.ClampSpawnCooldown(cdCap);
            Xp.OnBlockBroken(e.Type, Build.Role, WorldGrid.CellCenter(e.Col, e.Row));
            _blockCounter++;
            var at = WorldGrid.CellCenter(e.Col, e.Row);

            // 굴착 보호막 — 벽을 부술 때마다 짧은 방어막 (원본 breakShield)
            if (Build.BreakShield > 0)
            {
                Roles.ShieldTime = Math.Max(Roles.ShieldTime, Build.BreakShield);
                Player.IFrames = Math.Max(Player.IFrames, Math.Min(.28, Build.BreakShield * .45));
            }
            // 파쇄 충격파 · 폭발 드릴 · 연쇄 붕괴 — 주변 벽에 비율 피해 (원본 infTraitBlast). 재귀 방지 잠금
            if (!_blastLock)
            {
                double rad = Build.BreakShockRadius, pow = Build.BreakShockPower;
                if (Build.ChainCollapse) { rad += .5; pow = Math.Max(pow, .37); }
                if (rad > 0) TraitBlast(e.Col, e.Row, rad, pow);
            }
            // 파편 탄환 — 5블록마다 사방으로 (원본 shardBurst)
            if (Build.ShardBurst > 0 && _blockCounter % 5 == 0) RadialBurst(at, Build.ShardBurst, .35);
            // 자동 굴착탄 — N블록마다 10발 (원본 autoDigEvery)
            if (Build.AutoDigEvery > 0 && _blockCounter % Build.AutoDigEvery == 0) RadialBurst(at, 10, .35);

            var (kind, amount) = TileTypes.Yield(e.Type);
            if (amount > 0)
            {
                int n = SimTuning.LootCount
                      + (e.Type == TileType.Gem || e.Type == TileType.Crys ? SimTuning.LootGemBonus : 0);
                Loot.SpawnBurst(WorldGrid.CellCenter(e.Col, e.Row), kind, amount, n, e.HitDir);
            }
            TileBroken?.Invoke(e);
        }

        /// <summary>
        /// 실제 경과 시간을 넣으면 고정 틱으로 나눠 돌린다.
        /// 프레임이 크게 밀려도 <see cref="SimTuning.MaxTicksPerFrame"/> 이상은 따라잡지 않는다.
        /// </summary>
        public void Advance(double realDt, in PlayerInput input)
        {
            if (World == null) return;
            if (!Phase.TicksSimulation()) { _accumulator = 0; return; }

            _accumulator += realDt;
            int ticks = 0;
            while (_accumulator >= SimTuning.FixedDeltaTime && ticks < SimTuning.MaxTicksPerFrame)
            {
                Tick(SimTuning.FixedDeltaTime, input);
                _accumulator -= SimTuning.FixedDeltaTime;
                ticks++;
            }
            // 너무 밀렸으면 남은 시간을 버린다 (스파이럴 방지)
            if (_accumulator > SimTuning.FixedDeltaTime * SimTuning.MaxTicksPerFrame)
                _accumulator = 0;
        }

        /// <summary>한 틱. 순서는 원본 update(dt) 를 따른다.</summary>
        public void Tick(double dt, in PlayerInput input)
        {
            RunTime += dt;
            FloorTime += dt;

            Player.IFrames = Math.Max(0, Player.IFrames - dt);

            // 탈출 지점 지정 중에는 좌클릭이 확정 버튼이라 드릴로 가지 않는다
            var mineInput = input;
            if (Escape.Phase == EscapePhase.Placing) mineInput.DrillHeld = false;
            _drillHeld = mineInput.DrillHeld;
            MovementSystem.Tick(World, Player, input, dt, e => PlayerDashed?.Invoke(e), Build);
            MiningSystem.Tick(World, Player, mineInput, dt,
                e => DrillBounced?.Invoke(e),
                (pos, dmg) => DrillBeat?.Invoke(pos, dmg),
                Build,
                Build.Role == RoleId.Driller
                    ? (c, r, d) => Roles.ApplyDrillerPressure(Player, Build, c, r, d, Depth, boost: false)
                    : (Func<int, int, double, bool>)null);
            Loot.MagnetMul = Build.LootMagnetMul; Loot.PickupMul = Build.LootPickupMul;
            Loot.Tick(World, Player.Position, dt);

            // 무정지 과급 — 열 90% 이상이면 0.5초마다 최대 HP 0.6% 소모 (원본 12539행)
            if (Build.EndlessOverdrive && Player.DrillHeat >= .9 && _drillHeld)
            {
                _endlessBurnTick += dt;
                while (_endlessBurnTick >= .5) { _endlessBurnTick -= .5; Player.Hp = Math.Max(1, Player.Hp - Player.HpMax * .006); }
            }
            else _endlessBurnTick = 0;

            // 드릴 끝이 적에게 닿으면 피해를 준다 (원본 updateEnemies 7003~7010)
            TickDrillContact(dt);

            // 탈출 포트 — X 지정, 좌클릭 확정, 우클릭 취소. 지정 중에는 좌클릭이 드릴로 가지 않는다.
            bool placing = Escape.Phase == EscapePhase.Placing;
            if (input.EscapePressed) Escape.TogglePlacement(Player, Depth);
            if (placing)
            {
                Escape.UpdatePlacement(Player, input.AimWorld);
                if (input.PrimaryPressed) Escape.Confirm(Player, input.AimWorld, Depth);
                else if (input.SecondaryPressed) Escape.Cancel();
            }
            Escape.Tick(Player, dt);
            if (Phase != GamePhase.Playing) return;

            // 스킬
            if (input.SkillQPressed) Roles.UseQ(Player, Build, Depth);
            if (input.SkillEPressed) Roles.UseE(Player, Build);

            // 거너 좌클릭은 파쇄탄, 나머지 직업은 드릴. 우클릭은 전원 사격.
            if (Build.Role == RoleId.Gunner && input.DrillHeld && Player.CanMove) Roles.TryFireBreaker(Player);
            if (input.FireHeld && Player.CanMove) Projectiles.TryFire(Player, Build, input.DrillHeld);

            Roles.Tick(Player, Build, dt, Depth);
            if (input.ReloadPressed) Projectiles.StartReload(Build, true);
            Projectiles.Tick(Player, Build, dt);

            // 동적 위협 — 시간·진행도로 스포너 파라미터를 매 틱 갱신 (원본 12226~12229)
            Run.Tick(dt);
            Enemies.Threat = Run.Threat;
            Enemies.Cap = Run.EnemyCap;
            Enemies.SpawnInterval = Run.SpawnInterval;
            Enemies.SpawnBurst = Run.SpawnBurst;
            Enemies.Tick(Player, dt);
            if (_spawnBossPending) { _spawnBossPending = false; Bosses.Spawn(Player, Depth); }
            Bosses.Tick(Player, dt, Depth);

            // 전력망 유지 XP — 노드에 연결된 센트리만 인정 (원본 12448)
            int gridTurrets = 0;
            foreach (var t in Roles.Turrets) if (t.NodePowered) gridTurrets++;
            Xp.TickGrid(gridTurrets, dt, Build.Role, Player.Position);
            Xp.CheckLevel();

            if (Player.Downed) EndRun(escaped: false, Bosses.Active ? "보스에게 쓰러짐" : "적에게 쓰러짐");

            // 시야는 이동·채굴이 끝난 뒤 마지막에 갱신한다 (원본 update 순서와 동일).
            _visionSources.Clear();
            _visionSources.Add(VisionSource.Crew(Player.Position));
            Los.Compute(Player.Position, _visionSources);
        }

        /// <summary>
        /// 드릴 접촉 피해. 원본은 이 판정이 적 갱신 루프 안에 있었다.
        /// 드릴 끝(DRILL_TIP)이 적 반경 + 8px 안에 들어오면 매 틱 피해를 준다.
        /// </summary>
        void TickDrillContact(double dt)
        {
            if (!Player.IsDigging && !_drillHeld) return;
            var tip = Player.Position + Vec2.FromAngle(Player.Aim) * SimTuning.DrillTip;
            double margin = SimTuning.PxCells(8.0);

            foreach (var e in Enemies.Enemies)
            {
                if (!e.Alive) continue;
                if (Vec2.Distance(tip, e.Position) >= e.Radius + margin) continue;
                double dmg = SimTuning.DrillDps * SimTuning.EnemyDrillMul
                           * SimTuning.DrillDamageMul * MiningSystem.WarmMul(Player) * dt;
                Enemies.HurtEnemy(e, dmg, Vec2.FromAngle(Player.Aim), Player.Position);
            }
        }

        bool _drillHeld;

        /// <summary>보간용. 렌더가 틱 사이를 부드럽게 잇는 데 쓴다.</summary>
        public double InterpolationAlpha => _accumulator / SimTuning.FixedDeltaTime;
    }
}
