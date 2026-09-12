using System;
using System.Collections.Generic;

namespace TunnelCrew.Sim
{
    public enum RiskContractId : byte { None = 0, HotDrill, CloseQuarters, Pathfinder, Untouched }

    public readonly struct RiskContractDef
    {
        public readonly RiskContractId Id;
        public readonly string Name, Condition;
        public readonly int Need, Reward;
        public RiskContractDef(RiskContractId id, string name, string condition, int need, int reward)
        { Id = id; Name = name; Condition = condition; Need = need; Reward = reward; }
    }

    public static class RiskContracts
    {
        public static readonly RiskContractDef[] All =
        {
            new RiskContractDef(RiskContractId.None, "계약 없음", "기존 원정 규칙 그대로 출격합니다.", 0, 0),
            new RiskContractDef(RiskContractId.HotDrill, "과열선 작업", "열 70% 이상에서 벽 12개 파괴", 12, 2),
            new RiskContractDef(RiskContractId.CloseQuarters, "근접 교전", "2.2칸 안에서 적 16마리 직접 처치", 16, 2),
            new RiskContractDef(RiskContractId.Pathfinder, "선발 측량", "새로운 지질 구역 10곳 진입", 10, 2),
            new RiskContractDef(RiskContractId.Untouched, "무결점 장비", "한 지층에서 18 이상 단일 피해 없이 보스 격파", 1, 3),
        };

        public static RiskContractDef For(RiskContractId id)
        {
            foreach (var d in All) if (d.Id == id) return d;
            return All[0];
        }
    }

    public readonly struct ContractChangedEvent
    {
        public readonly RiskContractId Id;
        public readonly int Current, Need, Reward;
        public readonly bool Completed, FailedThisFloor;
        public ContractChangedEvent(RiskContractId id, int current, int need, int reward, bool completed, bool failedThisFloor)
        { Id = id; Current = current; Need = need; Reward = reward; Completed = completed; FailedThisFloor = failedThisFloor; }
    }

    /// <summary>
    /// 원정당 하나의 선택 계약. 실패는 코어·진행도를 깎지 않으며 완료할 때만 운반 코어를 더한다.
    /// 무결점 계약의 피격 실패는 현재 지층에만 적용되어 다음 지층에서 다시 도전할 수 있다.
    /// </summary>
    public sealed class RiskContractSystem
    {
        const double MajorHit = 18.0;
        const int SectorSize = 4;

        readonly HashSet<long> _visitedSectors = new HashSet<long>();
        bool _majorHitThisFloor;
        long _lastSector = long.MinValue;
        string _hudText;

        public RiskContractId Id { get; }
        public int Current { get; private set; }
        public bool Completed { get; private set; }
        public bool FailedThisFloor => Id == RiskContractId.Untouched && _majorHitThisFloor && !Completed;
        public RiskContractDef Def => RiskContracts.For(Id);
        public event Action<ContractChangedEvent> Changed;
        public event Action<int> CompletedReward;

        public RiskContractSystem(RiskContractId id) { Id = id; RefreshHud(); }

        public string HudText => _hudText;

        public void OnFloorInit(int depth, PlayerState player)
        {
            _majorHitThisFloor = false;
            _lastSector = SectorKey(depth, player?.Position ?? Vec2.Zero);
            RefreshHud();
        }

        public void Tick(int depth, PlayerState player)
        {
            if (Completed || Id != RiskContractId.Pathfinder || player == null || player.Downed) return;
            long key = SectorKey(depth, player.Position);
            if (key == _lastSector) return;
            _lastSector = key;
            if (_visitedSectors.Add(key)) Progress();
        }

        public void OnTileBroken(PlayerState player, bool byPlayer)
        {
            if (Completed || Id != RiskContractId.HotDrill || !byPlayer || player == null || player.DrillHeat < .7) return;
            Progress();
        }

        public void OnEnemyKilled(EnemyState enemy, Vec2 playerPosition, bool byPlayer)
        {
            if (Completed || Id != RiskContractId.CloseQuarters || !byPlayer || enemy == null) return;
            if (Vec2.Distance(enemy.Position, playerPosition) <= 2.2) Progress();
        }

        public void OnPlayerHurt(double damage)
        {
            if (!Completed && Id == RiskContractId.Untouched && damage >= MajorHit)
            {
                _majorHitThisFloor = true;
                Raise();
            }
        }

        public void OnBossDefeated()
        {
            if (!Completed && Id == RiskContractId.Untouched && !_majorHitThisFloor) Complete();
        }

        void Progress()
        {
            if (Completed) return;
            Current = Math.Min(Def.Need, Current + 1);
            if (Current >= Def.Need) Complete(); else Raise();
        }

        void Complete()
        {
            if (Completed || Id == RiskContractId.None) return;
            Completed = true;
            Current = Math.Max(Current, Def.Need);
            Raise();
            CompletedReward?.Invoke(Def.Reward);
        }

        void Raise()
        {
            RefreshHud();
            Changed?.Invoke(new ContractChangedEvent(Id, Current, Def.Need, Def.Reward, Completed, FailedThisFloor));
        }

        void RefreshHud()
        {
            if (Id == RiskContractId.None) _hudText = "";
            else if (Completed) _hudText = $"계약 {Def.Name} 완료 · 코어 +{Def.Reward}";
            else if (FailedThisFloor) _hudText = $"계약 {Def.Name} · 이번 지층 재도전 대기";
            else _hudText = Id == RiskContractId.Untouched ? $"계약 {Def.Name} · 손상 없음" : $"계약 {Def.Name} {Current}/{Def.Need}";
        }

        static long SectorKey(int depth, Vec2 p)
        {
            var (c, r) = WorldGrid.ToCell(p);
            int sx = Math.Max(0, c) / SectorSize, sy = Math.Max(0, r) / SectorSize;
            return ((long)depth << 40) | ((long)sx << 20) | (uint)sy;
        }
    }
}
