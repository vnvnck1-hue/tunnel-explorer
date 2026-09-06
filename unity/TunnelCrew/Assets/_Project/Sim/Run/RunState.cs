using System;

namespace TunnelCrew.Sim
{
    /// <summary>
    /// 행성 정의 — 원본 <c>INF_PLANET</c>(12908) 의 지층 3 + 이상지대.
    /// 층이 깊어질수록 벽·적 체력 배율과 장악 목표가 오른다.
    /// </summary>
    public static class Planet
    {
        public struct Stratum { public string Name; public double WallHp, EnemyHp, Dominance; }

        public static readonly Stratum[] Strata =
        {
            new Stratum { Name = "표층 지대", WallHp = 1.0,  EnemyHp = 1.0, Dominance = 0.22 },
            new Stratum { Name = "균열 지대", WallHp = 1.35, EnemyHp = 1.4, Dominance = 0.26 },
            new Stratum { Name = "중심부",    WallHp = 1.8,  EnemyHp = 1.9, Dominance = 0.30 },
        };
        public const double AbyssWallHpGrowth = 1.6;
        public const double AbyssEnemyHpGrowth = 1.55;
        public const double AbyssDominance = 0.34;
        /// <summary>원본 INF_GROWTH_SCALE — 성장 곡선 전체를 반으로 누른 값.</summary>
        public const double GrowthScale = 0.5;

        public static int StratumCount => Strata.Length;
        public static bool IsAbyss(int depth) => depth > StratumCount;
        public static Stratum StratumFor(int depth) => Strata[Math.Clamp(depth, 1, StratumCount) - 1];

        /// <summary>원본 infWallHpMulFor.</summary>
        public static double WallHpMulFor(int depth)
            => !IsAbyss(depth) ? StratumFor(depth).WallHp
                : Strata[StratumCount - 1].WallHp * Math.Pow(AbyssWallHpGrowth, depth - StratumCount);

        /// <summary>원본 infEnemyHpMulFor.</summary>
        public static double EnemyHpMulFor(int depth)
            => !IsAbyss(depth) ? StratumFor(depth).EnemyHp
                : Strata[StratumCount - 1].EnemyHp * Math.Pow(AbyssEnemyHpGrowth, (depth - StratumCount) * GrowthScale);

        /// <summary>원본 infDominanceTarget — 이 비율만큼 부수면 보스가 나온다.</summary>
        public static double DominanceTargetFor(int depth)
            => IsAbyss(depth) ? AbyssDominance : StratumFor(depth).Dominance;

        /// <summary>원본 infDepthLabel.</summary>
        public static string DepthLabel(int depth)
            => IsAbyss(depth) ? $"이상지대 {depth - StratumCount}" : $"지층 {depth}/{StratumCount} · {StratumFor(depth).Name}";
    }

    /// <summary>
    /// 한 층의 진행 상태 — 장악도·위협·스폰 압력. 원본 <c>INF.floorBroken / totalBreakable /
    /// floorTime / spawnDebt</c> 와 <c>infThreatValue · infEnemyCap · infSpawnInterval · infSpawnBurst</c>
    /// (12226~12229) 를 한곳에 모았다.
    ///
    /// 핵심 긴장 루프: **파면 팔수록 적이 빨리 몰려온다.** 블록마다 spawnDebt 가 쌓이고 스폰 쿨이 줄어든다.
    /// </summary>
    public sealed class RunState
    {
        public int Depth { get; private set; } = 1;
        public double FloorTime { get; private set; }
        public int FloorBroken { get; private set; }
        public int TotalBreakable { get; private set; } = 1;
        public int BossesKilled { get; set; }

        /// <summary>블록 파괴로 쌓이는 추가 스폰 빚. 1 이상이면 다음 스폰에 정수 부분(최대 4)이 더해진다.</summary>
        public double SpawnDebt { get; set; }

        public bool BossSpawned { get; set; }
        public bool BossActive { get; set; }

        public double WallHpMul { get; private set; } = 1.0;
        public double EnemyHpMul { get; private set; } = 1.0;

        /// <summary>장악도 0~1.</summary>
        public double Dominance => (double)FloorBroken / Math.Max(1, TotalBreakable);
        public double DominanceTarget => Planet.DominanceTargetFor(Depth);
        public bool DominanceReached => Dominance >= DominanceTarget;

        /// <summary>장악도가 목표에 닿은 순간 한 번.</summary>
        public event Action DominanceReachedEvent;

        // ── 동적 위협 (원본 12226~12229)
        public double Threat => Math.Min(9, 1 + Planet.GrowthScale * (Math.Max(0, Depth - 1) * 0.35 + FloorTime / 90.0 + FloorBroken / 28.0));
        public int EnemyCap => (int)Math.Min(64, 18 + Math.Floor(Planet.GrowthScale * (FloorBroken / 3.0 + FloorTime / 15.0) + Threat * 1.5));
        public double SpawnInterval => Math.Max(0.62, 5.2 / (1 + Math.Max(0, Threat - 1) * 0.55));
        public int SpawnBurst => (int)Math.Min(5, 1 + Math.Floor(Math.Max(0, Threat - 1) / 1.75));

        /// <summary>원본 infInitFloor — 층 진입. 파괴 가능 블록 수를 세고 배율을 정한다.</summary>
        public void InitFloor(int depth, WorldGrid world)
        {
            Depth = depth;
            FloorTime = 0;
            FloorBroken = 0;
            SpawnDebt = 0;
            BossSpawned = false;
            BossActive = false;
            WallHpMul = Planet.WallHpMulFor(depth);
            EnemyHpMul = Planet.EnemyHpMulFor(depth);

            int n = 0;
            for (int k = 0; k < world.CellCount; k++)
            {
                var t = world.AtIndex(k);
                if (t != TileType.Empty && !TileTypes.IsBedrock(t)) n++;
            }
            TotalBreakable = Math.Max(1, n);
            world.WallHpMul = WallHpMul;
        }

        public void Tick(double dt) => FloorTime += dt;

        /// <summary>
        /// 원본 infOnBlockBroken 의 진행 부분. 반환값은 스폰 쿨 상한 — 호출자가
        /// <c>enemyCd = min(enemyCd, 반환값)</c> 으로 적용한다.
        /// </summary>
        public double OnBlockBroken()
        {
            FloorBroken++;
            SpawnDebt += 0.30 + Math.Min(0.42, FloorBroken * 0.006);
            if (!BossSpawned && DominanceReached)
            {
                BossSpawned = true;
                DominanceReachedEvent?.Invoke();
            }
            return Math.Max(0.35, 2.1 - FloorBroken * 0.018);
        }

        /// <summary>스폰 시점에 빚을 정산한다 — 원본 6958행. 정수 부분만, 최대 4.</summary>
        public int TakeSpawnDebt()
        {
            if (SpawnDebt < 1) return 0;
            int debt = (int)Math.Min(4, Math.Floor(SpawnDebt));
            SpawnDebt -= debt;
            return debt;
        }
    }
}
