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
        /// <summary>AI 크루 (원본 AICREW). 편성은 런 사이에도 유지된다.</summary>
        public AiCrewSystem Crew { get; }
        /// <summary>팀 핑 (원본 TCPING).</summary>
        public PingSystem Ping { get; }
        /// <summary>크루 채팅 (원본 TCCHAT).</summary>
        public CrewChat Chat { get; }
        /// <summary>현장 퀵크래프트 (원본 TC_CRAFT).</summary>
        public QuickCraftSystem Craft { get; }
        /// <summary>지금 벽을 깎는 주체. AI 크루면 굴착 XP 가 그 크루에게 간다 (원본 AI.breakSrc). null = 사람.</summary>
        public object BreakSource;
        bool _crewFresh; double _downT, _reviveT;
        /// <summary>사람이 쓰러져 동료의 구조를 기다리는 중 (원본 G.downed · v7.7.2c).</summary>
        public bool PlayerDownedWaiting => Player.Downed && _downT > 0;
        public double ReviveProgress => Math.Min(1, _reviveT / ReviveNeedSec);
        public const double ReviveNeedSec = 5, ReviveRange = 1.6, ReviveHpRatio = .5;
        public event Action PlayerDowned;
        /// <summary>런 상태. Playing 에서만 틱이 돈다. Rest/Result 전환은 Sim 이 정하고 화면은 Presentation 이 맡는다.</summary>
        public GamePhase Phase { get; private set; } = GamePhase.Playing;
        /// <summary>보스 격파 직후 — 연출이 끝나면 Presentation 이 <see cref="EnterRest"/> 를 부른다.</summary>
        public bool RestPending { get; private set; }
        public BossTier LastBossTier { get; private set; }
        /// <summary>런 종료 결과.</summary>
        public bool RunEscaped { get; private set; }
        public string RunEndReason { get; private set; } = "";
        /// <summary>유물 효과 — 런 시작에 장착 목록이 고정된다.</summary>
        public RelicSystem RelicFx { get; } = new RelicSystem();
        public event Action<RelicFxEvent> RelicEffect;
        public event Action<RelicGrantedEvent> RelicGranted;
        /// <summary>이 런에 적용된 영구 노드 누산기 (원본 INF.perm). StartRun(role, meta) 가 채운다.</summary>
        public PermState Perm { get; private set; } = new PermState();
        /// <summary>런 종료 정산 결과 (EndRun 이 채운다).</summary>
        public MetaState.Settlement LastSettlement { get; private set; }
        public System.Collections.Generic.List<string> LastUnlocks { get; private set; } = new System.Collections.Generic.List<string>();
        MetaState _meta;
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
        /// <summary>특성 서브시스템 연출 — 종류: auxHit · afterBlast · vortex · planetBreaker · grandCollapse · blast · risk.</summary>
        public event Action<TraitFxEvent> TraitFx;
        public event Action<TraitPickedEvent> TraitPicked;
        /// <summary>런 종료 — (탈출 성공 여부, 사유).</summary>
        public event Action<bool, string> RunEnded;

        public TunnelSim(int fxSeed = 12345)
        {
            Loot = new LootSystem(fxSeed);
            Crew = new AiCrewSystem(this);
            Ping = new PingSystem(this);
            Chat = new CrewChat(this);
            Craft = new QuickCraftSystem(this);
            Loot.Collected += e => ResourceCollected?.Invoke(e);

            // Run / Xp 는 런 내내 하나다 — 구독은 여기서 한 번만
            RelicFx.Fx += e => RelicEffect?.Invoke(e);
            RelicFx.Granted += e =>
            {
                if (e.Relic == null) { Loot.Core += 2; }   // 도감 완성 — 코어 +2
                else if (_meta != null) Relics.Grant(_meta, e.Relic.Id);
                RelicGranted?.Invoke(e);
            };
            RelicFx.PickUnowned = tier =>
            {
                // 원본 infRelicPickUnowned — 등급 순서로 미보유 풀에서 뽑는다
                int[] order = tier == 4 ? new[] { 4, 2, 1 } : tier == 2 ? new[] { 2, 1, 4 } : new[] { 1, 2, 4 };
                foreach (int t in order)
                {
                    var pool = new System.Collections.Generic.List<RelicDef>();
                    foreach (var r in Relics.All) if (r.Tier == t && (_meta == null || !_meta.relicOwned.Contains(r.Id))) pool.Add(r);
                    if (pool.Count > 0) return pool[(int)(_relicPickRng.NextDouble() * pool.Count)];
                }
                return null;
            };
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
        int _startCardsPending;
        readonly Rng _coreRng = new Rng(0xC0DE);
        readonly Rng _relicPickRng = new Rng(0x2E1C);
        int _blockCounter;
        bool _blastLock;
        double _endlessBurnTick;
        double _auxCd;
        readonly List<(Vec2 at, double t)> _afterHits = new List<(Vec2, double)>();
        double _riskTextCd;
        /// <summary>보조 드릴 개수 (원본 max(auxDrills, drillStorm)). 연출이 궤도 드릴을 그릴 때 쓴다.</summary>
        public int AuxDrillCount => Math.Max(Build.AuxDrills, Build.DrillStorm);

        /// <summary>원본 infTraitRiskDamage — 특성 발동 대가. lethal 이 아니면 HP 1 은 남긴다.</summary>
        void RiskDamage(double amount, string label, bool lethal)
        {
            double dmg = Math.Max(1, amount);
            Player.Hp = Math.Max(lethal ? 0 : 1, Player.Hp - dmg);
            if (_riskTextCd <= 0) { _riskTextCd = .55; TraitFx?.Invoke(new TraitFxEvent { Kind = "risk", At = Player.Position, Label = $"-{Math.Ceiling(dmg)} · {label}" }); }
            if (Player.Hp <= 0) { Player.Downed = true; }
        }

        /// <summary>원본 infUpdateAuxDrills — 캐릭터 주변(30% 축소 범위)의 가까운 벽을 자동으로 깎는다.</summary>
        void TickAuxDrills(double dt)
        {
            int count = AuxDrillCount;
            if (count <= 0) return;
            _auxCd = Math.Max(0, _auxCd - dt);
            if (_auxCd > 0) return;
            _auxCd = Math.Max(.12, .48 - count * .035);
            double range = Math.Max(1.25, (3.2 + Math.Min(3, count * .25)) * .3);
            var pp = Player.Position;
            var cand = new List<(int c, int r, double d)>();
            int c0 = Math.Max(1, (int)(pp.X - range) - 1), c1 = Math.Min(World.Cols - 2, (int)(pp.X + range) + 1);
            int r0 = Math.Max(1, (int)(pp.Y - range) - 1), r1 = Math.Min(World.Rows - 2, (int)(pp.Y + range) + 1);
            for (int r = r0; r <= r1; r++) for (int c = c0; c <= c1; c++)
            {
                if (!World.IsSolid(c, r) || World.IsBedrock(c, r)) continue;
                double d = Vec2.Distance(WorldGrid.CellCenter(c, r), pp);
                if (d <= range) cand.Add((c, r, d));
            }
            cand.Sort((a, b) => a.d.CompareTo(b.d));
            int n = Math.Min(count, cand.Count);
            for (int i = 0; i < n; i++)
            {
                var (c, r, d) = cand[i];
                var dir = (WorldGrid.CellCenter(c, r) - pp) / Math.Max(1e-6, d);
                World.Damage(c, r, SimTuning.DrillDps * SimTuning.DrillDamageMul * (Build.AuxDrillPower > 0 ? Build.AuxDrillPower : .102), dir);
                if (i < 3) TraitFx?.Invoke(new TraitFxEvent { Kind = "auxHit", At = WorldGrid.CellCenter(c, r), Dir = dir });
            }
        }

        /// <summary>원본 infPlanetBreaker — 조준 축 방향으로 10칸 × 5칸 띠를 쓸어낸다. 최대 HP 1.5% 반동.</summary>
        void PlanetBreaker(int c, int r)
        {
            double a = Player.Aim;
            int dc = Math.Abs(Math.Cos(a)) >= Math.Abs(Math.Sin(a)) ? (Math.Cos(a) >= 0 ? 1 : -1) : 0;
            int dr = dc != 0 ? 0 : (Math.Sin(a) >= 0 ? 1 : -1);
            int pc = -dr, pr = dc;
            _blastLock = true;
            for (int step = -1; step <= 8; step++) for (int side = -2; side <= 2; side++)
            {
                int cc = c + dc * step + pc * side, rr = r + dr * step + pr * side;
                if (!World.InInterior(cc, rr) || !World.IsSolid(cc, rr) || World.IsBedrock(cc, rr)) continue;
                World.Damage(cc, rr, World.MaxHp(World.At(cc, rr)) * .375, new Vec2(dc, dr));
            }
            _blastLock = false;
            RiskDamage(Player.HpMax * .015, "파쇄기 반동", false);
            TraitFx?.Invoke(new TraitFxEvent { Kind = "planetBreaker", At = WorldGrid.CellCenter(c, r), Dir = new Vec2(dc, dr), Radius = 4.8 });
        }

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
            TraitFx?.Invoke(new TraitFxEvent { Kind = "blast", At = WorldGrid.CellCenter(c0, r0), Radius = radius });
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
            RelicFx.OnDescend();
        }

        /// <summary>휴식 → 이 층에 남아 탈출 (원본 infEscapeReturnFromRest). 포트가 없으면 자동 요청.</summary>
        public void ReturnFromRest()
        {
            if (!RestChosen) return;
            Phase = GamePhase.Playing;
            if (!Escape.Active) Escape.AutoSummon(Player, Player.Position, Depth);
        }

        /// <summary>원본 infPermTryRevive — 원정당 한 번(영구 노드), HP 35% 로 다시 일어선다.</summary>
        bool TryPermRevive()
        {
            if (Perm.Revive <= 0 || Perm.ReviveUsed >= Perm.Revive) return false;
            Perm.ReviveUsed++;
            Player.Downed = false;
            Player.Hp = Math.Max(1, JsMath.Round(Player.HpMax * .35));
            Player.IFrames = Math.Max(Player.IFrames, 2.2);
            Roles.ShieldTime = Math.Max(Roles.ShieldTime, 2.2);
            Revived?.Invoke(Player.Position);
            return true;
        }
        public event Action<Vec2> Revived;

        /// <summary>원본 infPermRemoteTick — 코어를 N개 캘 때마다 1개를 기지로 전송 (쓰러져도 남는다).</summary>
        void RemoteTick(int gained)
        {
            if (Perm.RemoteEvery <= 0) return;
            Perm.RemoteAcc += gained;
            while (Perm.RemoteAcc >= Perm.RemoteEvery) { Perm.RemoteAcc -= Perm.RemoteEvery; Perm.RemoteSent++; TraitFx?.Invoke(new TraitFxEvent { Kind = "remote", At = Player.Position, Label = "원격 전송 +1" }); }
        }

        /// <summary>원본 infEndRun — 결과 화면 + 기록·정산. 솔로라 다운 = 종료 (상호 부활은 크루가 있는 M6 에서).</summary>
        public void EndRun(bool escaped, string reason)
        {
            if (Phase == GamePhase.Result) return;
            Phase = GamePhase.Result;
            RunEscaped = escaped;
            RunEndReason = reason;
            if (_meta != null)
            {
                LastUnlocks = _meta.RecordRun(Depth, World.BlocksBroken, Run.BossesKilled);
                LastSettlement = _meta.Settle(Loot.Core, escaped, Perm, relicKeepRate: RelicFx.Has("r_smuggler") ? .25 : 0);
            }
            else LastSettlement = new MetaState.Settlement { Returned = escaped ? Loot.Core : 0, Lost = escaped ? 0 : Loot.Core, Escaped = escaped };
            Crew.OnRunEnd(); Craft.Close(true);
            RunEnded?.Invoke(escaped, reason);
        }

        public void StartRun(RoleId role) => StartRun(role, null);

        /// <summary>런 시작 — 직업·빌드 초기화 후 영구 노드를 적용한다 (원본 infStartRun → infApplyPermanentNodes).</summary>
        public void StartRun(RoleId role, MetaState meta)
        {
            _meta = meta;
            Build.Reset(role);
            Xp.Reset();
            Traits.Reset();
            Player.HpMax = SimTuning.PlayerHp; Player.Hp = Player.HpMax; Player.Downed = false;
            Perm = meta != null ? PermanentNodes.Collect(meta, role) : new PermState();
            PermanentNodes.Commit(Perm, Build, Player, Traits);
            RelicFx.Apply(meta != null ? Relics.EquippedIds(meta) : new System.Collections.Generic.List<string>(), Player);
            Xp.XpMul = Build.XpMul;
            _startCardsPending = Perm.StartCards;
            Run.BossesKilled = 0;
            RunEscaped = false; RunEndReason = "";
            _crewFresh = true; _downT = _reviveT = 0;
            Ping.Reset(); Chat.Reset(); Craft.Reset();
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
            Enemies.EnemyDamageMod = (en, d, weapon) => RelicFx.OnHit(en, d, weapon);
            Enemies.KnockMul = () => RelicFx.KnockMul;
            Enemies.EnemyTick = (en, d) => RelicFx.EnemyTick(en, d);
            Enemies.WallSlam = (en, prev, kb) => RelicFx.WallSlam(en, prev, kb);
            Enemies.EnemyKilled = en => RelicFx.OnKill(en);
            Enemies.PlayerDamageMod = d => RelicFx.PlayerDamageMod(d);
            Enemies.AfterPlayerHurt = () => RelicFx.AfterPlayerHurt();
            Enemies.TimeStopped = () => RelicFx.TimeStopped;
            Enemies.EnemyHurt += e =>
            {
                // 보스 격파 집계(BossesKilled · BossActive)는 BossSystem.OnDefeated 가 맡는다
                if (e.Killed)
                {
                    if (Enemies.DamageSource is CrewMember cm) Crew.AwardKill(cm, e.Enemy, e.ByTurret);   // AI 처치 — AI 개인 XP (§9.6.7)
                    else Xp.OnEnemyKilled(e.Enemy, Build.Role, e.ByTurret);
                }
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
                RemoteTick(e.CoreReward);
                RelicFx.BossDrop(e.Boss.Tier, e.Boss.Body.Position);
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
            // AI 크루 훅 — 적 표적·크루 피격·AI 탄 굴착 크레딧·보스탄 착탄·전원 탑승
            Enemies.TargetOf = e => Crew.TargetFor(e);
            Enemies.HurtTarget = (t, dmg, dir) => { if (t is PlayerState) Enemies.ApplyPlayerDamage(Player, dmg, dir); else if (t is CrewMember m) Crew.Hurt(m, dmg, dir); };
            Enemies.HitTest = (at, r) => Crew.HitTest(at, r);
            Projectiles.BreakSourceSetter = o => BreakSource = o;
            Bosses.CrewShotHit = (at, r, dmg) => Crew.BossShotHit(at, r, dmg);
            Escape.CrewAllAboard = () => Crew.EscapeAllAboard() != false;
            RelicFx.Bind(World, Enemies, Player, Build,
                (c, r, rad, pow) => TraitBlast(c, r, rad, pow),
                (from, angle, vid, power) => Projectiles.Projectiles.Add(new Projectile { Position = from, Velocity = Vec2.FromAngle(angle) * SimTuning.TeCells(300), Life = .9, Power = power, VisualId = vid }));
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
            _afterHits.Clear(); _auxCd = 0;
            RestPending = false;
            Phase = GamePhase.Playing;
            Loot.DepthMul = 1.0 + 0.25 * (depth - 1);

            FloorTime = 0;
            _accumulator = 0;

            if (_crewFresh) { _crewFresh = false; Crew.OnRunStart(); } else { Crew.OnFloorInit(); Craft.OnFloorInit(); }
        }

        void OnTileBroken(TileBrokenEvent e)
        {
            // 벽이 사라지면 시야가 달라진다. 원본은 각 변경 지점이 LOS.markDirty() 를
            // 손으로 불러야 했지만, 여기서는 파괴 이벤트 한 곳에서 처리한다.
            Los?.MarkDirty();

            // 층 진행 — 장악도·스폰 압력·굴착 XP (원본 infOnBlockBroken)
            double cdCap = Run.OnBlockBroken();
            Enemies.ClampSpawnCooldown(cdCap);
            var at = WorldGrid.CellCenter(e.Col, e.Row);
            // AI 크루가 부순 블록: 장악도·코어는 팀에 기여하고 경험치는 그 크루 개인에게 간다. 사람의 특성 발동은 건드리지 않는다 (원본 AI.creditBreak)
            var crewSrc = BreakSource as CrewMember;
            if (crewSrc != null) Crew.CreditBreak(crewSrc, e.Type, at);
            else Xp.OnBlockBroken(e.Type, Build.Role, at);
            if (crewSrc == null)
            {
            _blockCounter++;

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
            // 잔상 드릴 — 0.32초 뒤 같은 자리에 작은 폭발 (원본 afterDrill)
            if (Build.AfterDrill && !_blastLock) _afterHits.Add((at, .32));
            // 붕괴 소용돌이 — 3블록마다 5칸 안 전리품을 끌어당기고 2.25 반경 폭발 (원본 vortexMining)
            if (Build.VortexMining && _blockCounter % 3 == 0 && !_blastLock)
            {
                Loot.Pull(at, 5.0, 75.0 / 50.0);
                TraitBlast(e.Col, e.Row, 2.25, .36);
                TraitFx?.Invoke(new TraitFxEvent { Kind = "vortex", At = at, Radius = 2.25 });
            }
            // 행성 파쇄기 · 대붕괴 — N블록마다, HP 대가
            if (Build.PlanetBreakerEvery > 0 && _blockCounter % Build.PlanetBreakerEvery == 0 && !_blastLock) PlanetBreaker(e.Col, e.Row);
            if (Build.GrandCollapseEvery > 0 && _blockCounter % Build.GrandCollapseEvery == 0 && !_blastLock)
            {
                RiskDamage(Player.HpMax * .025, "붕괴 충격", false);
                TraitBlast(e.Col, e.Row, 3.5, .3375);
                TraitFx?.Invoke(new TraitFxEvent { Kind = "grandCollapse", At = at, Radius = 3.5 });
            }

            }   // crewSrc == null

            // 희귀 광물 → 코어 +1 (+추가 코어 확률), 광석 회복, 원격 전송 (원본 infOnBlockBroken rare 분기)
            bool rare = e.Type == TileType.Ore || e.Type == TileType.Gem || e.Type == TileType.Crys;
            if (crewSrc == null) { RelicFx.OnBlock(at); if (e.HadBuriedRelic) RelicFx.BuriedFind(at); }
            if (rare)
            {
                int core = 1;
                if (crewSrc == null)
                {
                    core += _coreRng.NextDouble() < Build.CoreBonusChance ? 1 : 0;
                    core += RelicFx.OnRare(at, core, Depth);
                }
                Loot.Core += core;
                RemoteTick(core);
                if (crewSrc == null)
                {
                    if (Build.OreHeal > 0) Player.Hp = Math.Min(Player.HpMax, Player.Hp + Build.OreHeal);
                    Xp.Award(2, XpKind.Loot, Build.Role, label: "코어", at: at, checkLevel: false);
                }
                TraitFx?.Invoke(new TraitFxEvent { Kind = "core", At = at, Label = $"코어 +{core}" });
            }

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
        public void Tick(double dt, in PlayerInput inputIn)
        {
            RunTime += dt;
            FloorTime += dt;
            var input = inputIn;
            // 응급 주사 주입 중 — 드릴/사격/대시/스킬만 막고 이동은 45% (기획 §8.3)
            if (Craft.Using != null) { input.DrillHeld = false; input.FireHeld = false; input.DashPressed = false; input.SkillQPressed = false; input.SkillEPressed = false; input.Move *= .45; }

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

            // 특성 서브시스템 (원본 infUpdateMiningTraits / infUpdateAuxDrills)
            _riskTextCd = Math.Max(0, _riskTextCd - dt);
            TickAuxDrills(dt);
            for (int i = _afterHits.Count - 1; i >= 0; i--)
            {
                var (hitAt, t) = _afterHits[i];
                t -= dt;
                if (t <= 0)
                {
                    _afterHits.RemoveAt(i);
                    var (hc, hr) = WorldGrid.ToCell(hitAt);
                    TraitBlast(hc, hr, 1.25, .25);
                    TraitFx?.Invoke(new TraitFxEvent { Kind = "afterBlast", At = hitAt, Radius = 1.25 });
                }
                else _afterHits[i] = (hitAt, t);
            }

            // 무정지 과급 — 열 90% 이상이면 0.5초마다 최대 HP 0.6% 소모 (원본 12539행)
            if (Build.EndlessOverdrive && Player.DrillHeat >= .9 && _drillHeld)
            {
                _endlessBurnTick += dt;
                while (_endlessBurnTick >= .5) { _endlessBurnTick -= .5; Player.Hp = Math.Max(1, Player.Hp - Player.HpMax * .006); }
            }
            else _endlessBurnTick = 0;

            // 드릴 끝이 적에게 닿으면 피해를 준다 (원본 updateEnemies 7003~7010)
            TickDrillContact(dt);

            // 출격 프리셋(영구 노드) — 원정 시작 시 카드 N장을 먼저 고른다 (원본 6.3 start preset card)
            if (_startCardsPending > 0 && !Traits.HasOffer) { _startCardsPending--; Traits.OpenLevel(TraitCtx, Xp.Level); }

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
            Ping.Tick(dt);   // 팀 핑 — 충전·마커·AI 명령 주입 (원본은 AICREW.update 를 감쌌다)
            Crew.Tick(dt);   // AI 크루 — 판단·이동·사격·굴착·설치 (원본 AI.update)
            Chat.Tick(dt);   // 채팅 말풍선 수명·AI 멘트

            // 동적 위협 — 시간·진행도로 스포너 파라미터를 매 틱 갱신 (원본 12226~12229)
            RelicFx.Tick(dt, Build, Phase == GamePhase.Playing);
            Run.Tick(dt);
            Enemies.Threat = Run.Threat;
            Enemies.Cap = Run.EnemyCap;
            Enemies.SpawnInterval = Run.SpawnInterval;
            Enemies.SpawnBurst = Run.SpawnBurst;
            Enemies.Tick(Player, dt);
            if (_spawnBossPending) { _spawnBossPending = false; Bosses.Spawn(Player, Depth); }
            if (!RelicFx.TimeStopped) Bosses.Tick(Player, dt, Depth);
            Craft.Tick(dt);   // 퀵크래프트 설치물 — 방벽이 적을 밀어내므로 적 갱신 뒤

            // 전력망 유지 XP — 노드에 연결된 센트리만 인정 (원본 12448)
            int gridTurrets = 0;
            foreach (var t in Roles.Turrets) if (t.NodePowered) gridTurrets++;
            Xp.TickGrid(gridTurrets, dt, Build.Role, Player.Position);
            Xp.CheckLevel();

            if (Player.Downed)
            {
                if (_downT <= 0 && RelicFx.TryPhoenix(Roles)) { }
                else if (_downT <= 0 && TryPermRevive()) { }
                else if (Crew.RescuersAlive() > 0) TickDowned(dt);   // 구조할 동료가 있으면 기절 — 5초 치료 (원본 playerEnterDowned)
                else EndRun(escaped: false, _downT > 0 ? "쓰러짐 — 구조할 동료가 없었다" : Bosses.Active ? "보스에게 쓰러짐" : "적에게 쓰러짐");
            }
            else _downT = 0;

            // 시야는 이동·채굴이 끝난 뒤 마지막에 갱신한다 (원본 update 순서와 동일).
            RefreshVision();
        }

        /// <summary>시야 광원(크루·AI 크루·플레어/노드·보스)을 모아 LOS 를 계산한다. 시네마틱처럼 월드가 멈춘 동안에도 Presentation 이 부를 수 있다.</summary>
        public void RefreshVision()
        {
            if (Los == null) return;
            _visionSources.Clear();
            _visionSources.Add(VisionSource.Crew(Player.Position));
            foreach (var m in Crew.Members) if (!m.Down) _visionSources.Add(VisionSource.Crew(m.Position));   // AI 크루 시야 합산 (원본 AI.visionXY)
            foreach (var f in Roles.Flares) if (f.VisionRange > 0) _visionSources.Add(new VisionSource { Position = f.Position, Range = f.VisionRange, Rays = SimTuning.CrewVisionRays });   // 플레어·노드 visionRange
            // 보스 시야원 — 보스는 스스로 빛나 시야에 들어온다 (원본 LOS.bossSources: range = max(5, round(r×2.4)+2), 탐색 기록은 남기지 않는다)
            if (Bosses != null && Bosses.Active) _visionSources.Add(new VisionSource { Position = Bosses.Boss.Body.Position, Range = Math.Max(5, (int)Math.Round(Bosses.Boss.Body.Radius * 2.4) + 2), Rays = Math.Max(56, Math.Min(160, (int)(Bosses.Boss.Body.Radius * 2.4 * 16))), VisibleOnly = true });
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

        /// <summary>원본 infDownedTick — 기절 중 구조 진행. 동료가 1.6칸 안에 있으면 5초 뒤 HP 50% 로 부활, 없으면 게이지가 천천히 빠진다.</summary>
        void TickDowned(double dt)
        {
            if (_downT <= 0) PlayerDowned?.Invoke();
            _downT += dt;
            Player.StunTime = Math.Max(Player.StunTime, .4); Player.IFrames = Math.Max(Player.IFrames, .4);
            Player.Velocity = Vec2.Zero; Player.DashActive = false;
            if (Crew.HelpersNear(Player.Position, ReviveRange) > 0)
            {
                _reviveT += dt;
                if (_reviveT >= ReviveNeedSec)
                {
                    Player.Downed = false; Player.Hp = Math.Max(1, Math.Round(Player.HpMax * ReviveHpRatio));
                    Player.IFrames = Math.Max(Player.IFrames, 2.2); Player.StunTime = 0;
                    _reviveT = 0; _downT = 0;
                    Revived?.Invoke(Player.Position);
                }
            }
            else _reviveT = Math.Max(0, _reviveT - dt * .5);
        }

        /// <summary>보간용. 렌더가 틱 사이를 부드럽게 잇는 데 쓴다.</summary>
        public double InterpolationAlpha => _accumulator / SimTuning.FixedDeltaTime;
    }
}
