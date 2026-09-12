using System;
using System.Collections.Generic;

namespace TunnelCrew.Sim
{
    public enum ExpeditionObjectiveId : byte { Breach = 0, CoreRecovery, Survey }

    public readonly struct ExpeditionObjectiveDef
    {
        public readonly ExpeditionObjectiveId Id;
        public readonly string Name, Short, Description;
        public ExpeditionObjectiveDef(ExpeditionObjectiveId id, string name, string shortText, string description)
        { Id = id; Name = name; Short = shortText; Description = description; }
    }

    public static class ExpeditionObjectives
    {
        public static readonly ExpeditionObjectiveDef[] All =
        {
            new ExpeditionObjectiveDef(ExpeditionObjectiveId.Breach, "심층 돌파", "암반 장악", "자유롭게 굴착해 지층을 장악하고 수호자를 끌어냅니다."),
            new ExpeditionObjectiveDef(ExpeditionObjectiveId.CoreRecovery, "공명 코어 회수", "공명 광맥", "측량기가 표시한 공명 광맥 3개를 파괴합니다."),
            new ExpeditionObjectiveDef(ExpeditionObjectiveId.Survey, "지질 측량", "측량 지점", "세 지점에서 1.25초씩 지층 데이터를 수집합니다."),
        };

        public static ExpeditionObjectiveDef For(ExpeditionObjectiveId id)
        {
            foreach (var d in All) if (d.Id == id) return d;
            return All[0];
        }
    }

    public readonly struct ObjectiveChangedEvent
    {
        public readonly ExpeditionObjectiveId Id;
        public readonly int Current, Need;
        public readonly bool Completed;
        public readonly int ChangedCell;
        public ObjectiveChangedEvent(ExpeditionObjectiveId id, int current, int need, bool completed, int changedCell = -1)
        { Id = id; Current = current; Need = need; Completed = completed; ChangedCell = changedCell; }
    }

    /// <summary>
    /// 지층 목표. 목표 지점은 기존 연결 동굴 안/가장자리에서만 고르며, 측량 진행은 이탈·피격 시
    /// 초기화하지 않는다. 목표 때문에 생기는 왕복과 대기 스트레스를 제한하는 것이 규칙이다.
    /// </summary>
    public sealed class ExpeditionObjectiveSystem
    {
        public const int TargetNeed = 3;
        public const double SurveySeconds = 1.25;
        public const double SurveyRadius = 1.35;

        readonly WorldGrid _world;
        readonly Rng _rng;
        readonly List<int> _targets = new List<int>();
        readonly HashSet<int> _done = new HashSet<int>();
        readonly Dictionary<int, double> _survey = new Dictionary<int, double>();
        readonly bool[] _reachableOpen;
        bool _completionSent;

        public ExpeditionObjectiveId Id { get; }
        public IReadOnlyList<int> Targets => _targets;
        public int Current => _done.Count;
        public int Need => Id == ExpeditionObjectiveId.Breach ? 1 : _targets.Count;
        public bool Completed { get; private set; }
        public double ProgressFraction(RunState run)
        {
            if (Completed) return 1;
            if (Id == ExpeditionObjectiveId.Breach) return Math.Min(1, run.Dominance / Math.Max(.001, run.DominanceTarget));
            if (Id == ExpeditionObjectiveId.Survey)
            {
                double total = _done.Count;
                foreach (var kv in _survey) if (!_done.Contains(kv.Key)) total += Math.Min(1, kv.Value / SurveySeconds);
                return Math.Min(1, total / Math.Max(1, Need));
            }
            return (double)Current / Math.Max(1, Need);
        }
        public event Action<ObjectiveChangedEvent> Changed;
        public event Action CompletedEvent;

        public ExpeditionObjectiveSystem(WorldGrid world, ExpeditionObjectiveId id, int depth, uint seed = 0x0B1EC71u)
        {
            _world = world;
            Id = id;
            _rng = new Rng(unchecked(seed ^ (uint)(depth * 104729) ^ (uint)id * 0x9E3779B9u));
            _reachableOpen = BuildReachableOpen();
            if (id == ExpeditionObjectiveId.CoreRecovery) PickCoreTargets();
            else if (id == ExpeditionObjectiveId.Survey) PickSurveyTargets();
        }

        public bool IsTarget(int cell) => _targets.Contains(cell);
        public bool IsDone(int cell) => _done.Contains(cell);
        public double SurveyProgress(int cell) => _survey.TryGetValue(cell, out var p) ? Math.Min(1, p / SurveySeconds) : 0;

        public string HudText(RunState run)
        {
            var d = ExpeditionObjectives.For(Id);
            if (Id == ExpeditionObjectiveId.Breach)
                return $"{d.Short} {run.Dominance:P0} / {run.DominanceTarget:P0}";
            return $"{d.Short} {Current} / {Math.Max(1, Need)}";
        }

        public void NotifyBreachComplete()
        {
            if (Id != ExpeditionObjectiveId.Breach) return;
            Complete(-1);
        }

        public void OnTileBroken(int cell)
        {
            if (Id != ExpeditionObjectiveId.CoreRecovery || !_targets.Contains(cell) || !_done.Add(cell)) return;
            Changed?.Invoke(new ObjectiveChangedEvent(Id, Current, Need, Current >= Need, cell));
            if (Current >= Need) Complete(cell);
        }

        public void Tick(PlayerState player, double dt)
        {
            if (Completed || Id != ExpeditionObjectiveId.Survey || player == null || player.Downed) return;
            int active = -1;
            double best = SurveyRadius;
            foreach (int cell in _targets)
            {
                if (_done.Contains(cell)) continue;
                int c = cell % _world.Cols, r = cell / _world.Cols;
                double d = Vec2.Distance(player.Position, WorldGrid.CellCenter(c, r));
                if (d <= best) { best = d; active = cell; }
            }
            if (active < 0) return;
            _survey.TryGetValue(active, out double progress);
            progress = Math.Min(SurveySeconds, progress + dt);
            _survey[active] = progress;
            if (progress < SurveySeconds) return;
            _done.Add(active);
            Changed?.Invoke(new ObjectiveChangedEvent(Id, Current, Need, Current >= Need, active));
            if (Current >= Need) Complete(active);
        }

        void Complete(int changedCell)
        {
            if (_completionSent) return;
            _completionSent = true;
            Completed = true;
            Changed?.Invoke(new ObjectiveChangedEvent(Id, Id == ExpeditionObjectiveId.Breach ? 1 : Current, Need, true, changedCell));
            CompletedEvent?.Invoke();
        }

        void PickCoreTargets()
        {
            var preferred = new List<int>();
            var fallback = new List<int>();
            for (int r = 2; r < _world.Rows - 2; r++) for (int c = 2; c < _world.Cols - 2; c++)
            {
                var t = _world.At(c, r);
                if (t == TileType.Empty || TileTypes.IsBedrock(t) || !AdjacentReachableOpen(c, r)) continue;
                int k = _world.Index(c, r);
                if (Vec2.Distance(WorldGrid.CellCenter(c, r), _world.EntryPosition) < 5) continue;
                if (t == TileType.Ore || t == TileType.Gem || t == TileType.Crys) preferred.Add(k);
                else fallback.Add(k);
            }
            Shuffle(preferred); Shuffle(fallback);
            AddSpread(preferred, TargetNeed, 5.0);
            AddSpread(fallback, TargetNeed, 4.0);
            AddSpread(preferred, TargetNeed, 0);
            AddSpread(fallback, TargetNeed, 0);
            // A generated floor should always have candidates. If art/resource RNG did not place enough rare veins,
            // promote accessible ordinary walls so the objective can never soft-lock.
            foreach (int k in _targets)
            {
                int c = k % _world.Cols, r = k / _world.Cols;
                if (_world.At(c, r) != TileType.Crys) _world.SetTile(c, r, TileType.Crys);
            }
        }

        void PickSurveyTargets()
        {
            var candidates = new List<int>();
            for (int r = 2; r < _world.Rows - 2; r += 2) for (int c = 2; c < _world.Cols - 2; c += 2)
            {
                if (_world.IsSolid(c, r) || !_reachableOpen[_world.Index(c, r)]) continue;
                if (Vec2.Distance(WorldGrid.CellCenter(c, r), _world.EntryPosition) < 5) continue;
                candidates.Add(_world.Index(c, r));
            }
            Shuffle(candidates);
            AddSpread(candidates, TargetNeed, 7.0);
            AddSpread(candidates, TargetNeed, 3.0);
            AddSpread(candidates, TargetNeed, 0);
            if (_targets.Count < TargetNeed)
            {
                // 드문 생성 변형에서 2칸 간격 표본이 부족해도 빈 동굴 전체를 다시 훑어 목표 수를 보장한다.
                candidates.Clear();
                for (int r = 1; r < _world.Rows - 1; r++) for (int c = 1; c < _world.Cols - 1; c++)
                    if (!_world.IsSolid(c, r) && _reachableOpen[_world.Index(c, r)] && Vec2.Distance(WorldGrid.CellCenter(c, r), _world.EntryPosition) >= 3)
                        candidates.Add(_world.Index(c, r));
                Shuffle(candidates);
                AddSpread(candidates, TargetNeed, 0);
            }
        }

        bool AdjacentReachableOpen(int c, int r)
            => Reachable(c + 1, r) || Reachable(c - 1, r) || Reachable(c, r + 1) || Reachable(c, r - 1);

        bool Reachable(int c, int r) => _world.InBounds(c, r) && _reachableOpen[_world.Index(c, r)];

        bool[] BuildReachableOpen()
        {
            var seen = new bool[_world.CellCount];
            var queue = new Queue<int>();
            int start = _world.Index(_world.EntryCol, _world.EntryRow);
            if (_world.IsSolid(_world.EntryCol, _world.EntryRow)) return seen;
            seen[start] = true; queue.Enqueue(start);
            int[] dc = { 1, -1, 0, 0 }, dr = { 0, 0, 1, -1 };
            while (queue.Count > 0)
            {
                int k = queue.Dequeue(), c = k % _world.Cols, r = k / _world.Cols;
                for (int i = 0; i < 4; i++)
                {
                    int cc = c + dc[i], rr = r + dr[i];
                    if (!_world.InBounds(cc, rr) || _world.IsSolid(cc, rr)) continue;
                    int nk = _world.Index(cc, rr);
                    if (seen[nk]) continue;
                    seen[nk] = true; queue.Enqueue(nk);
                }
            }
            return seen;
        }

        void AddSpread(List<int> source, int wanted, double minDistance)
        {
            foreach (int k in source)
            {
                if (_targets.Count >= wanted) break;
                if (_targets.Contains(k)) continue;
                int c = k % _world.Cols, r = k / _world.Cols;
                var p = WorldGrid.CellCenter(c, r);
                bool far = true;
                foreach (int old in _targets)
                {
                    var q = WorldGrid.CellCenter(old % _world.Cols, old / _world.Cols);
                    if (Vec2.Distance(p, q) < minDistance) { far = false; break; }
                }
                if (far) _targets.Add(k);
            }
        }

        void Shuffle(List<int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.NextInt(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
