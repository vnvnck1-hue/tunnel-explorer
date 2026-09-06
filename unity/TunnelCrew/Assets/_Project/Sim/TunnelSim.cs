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

        public TunnelSim(int fxSeed = 12345)
        {
            Loot = new LootSystem(fxSeed);
            Loot.Collected += e => ResourceCollected?.Invoke(e);
        }

        /// <summary>층을 새로 생성하고 플레이어를 진입점에 놓는다. 원본 enterDepth().</summary>
        public void EnterDepth(int depth, DungeonConfig cfg)
        {
            Depth = depth;
            var gen = new DungeonGenerator(cfg).Generate(depth);
            Generation = gen;

            World = new WorldGrid(gen);
            Los = new LosService(World);
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
            Loot.DepthMul = 1.0 + 0.25 * (depth - 1);

            FloorTime = 0;
            _accumulator = 0;
        }

        void OnTileBroken(TileBrokenEvent e)
        {
            // 벽이 사라지면 시야가 달라진다. 원본은 각 변경 지점이 LOS.markDirty() 를
            // 손으로 불러야 했지만, 여기서는 파괴 이벤트 한 곳에서 처리한다.
            Los?.MarkDirty();

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

            MovementSystem.Tick(World, Player, input, dt, e => PlayerDashed?.Invoke(e));
            MiningSystem.Tick(World, Player, input, dt,
                e => DrillBounced?.Invoke(e),
                (pos, dmg) => DrillBeat?.Invoke(pos, dmg));
            Loot.Tick(World, Player.Position, dt);

            // 시야는 이동·채굴이 끝난 뒤 마지막에 갱신한다 (원본 update 순서와 동일).
            _visionSources.Clear();
            _visionSources.Add(VisionSource.Crew(Player.Position));
            Los.Compute(Player.Position, _visionSources);
        }

        /// <summary>보간용. 렌더가 틱 사이를 부드럽게 잇는 데 쓴다.</summary>
        public double InterpolationAlpha => _accumulator / SimTuning.FixedDeltaTime;
    }
}
