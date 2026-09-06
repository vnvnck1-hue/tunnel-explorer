using System;
using System.Collections.Generic;

namespace TunnelCrew.Sim
{
    /// <summary>경험치 채널. 원본 INF_XP_WEIGHT 의 행 키.</summary>
    public enum XpKind : byte { Dig, Combat, Support, Recon, Loot, Objective }

    public struct XpGainedEvent { public int Amount; public XpKind Kind; public string Label; public Vec2? At; }
    public struct LevelUpEvent { public int Level; public int XpNeedNext; }

    /// <summary>
    /// 모든 개인 경험치가 통과하는 단일 관문 — 원본 <c>infAwardXp</c>(12643) 와
    /// <c>INF_XP_TABLE · INF_XP_WEIGHT · INF_XP_FLOOR_CAP · infXpNeedFor · infCheckLevel</c>.
    ///
    /// 획득처를 늘려도 가중치·상한·기록이 여기서만 유지된다. 레벨업은 이벤트로만 알리고
    /// 카드 선택(모달)은 Presentation/Run 흐름이 맡는다 — 보스 전투 중에는 레벨 체크를 미룬다(원본 규칙).
    /// </summary>
    public sealed class XpGate
    {
        // ── 원본 INF_XP_TABLE (12585)
        public const int XpDirt = 1, XpStone = 2, XpRare = 4, XpCrack = 8, XpCrackCore = 12;
        public const int XpKill = 4, XpKillBoss = 45, XpTurretKill = 5, XpGridUptime = 2;
        public const double EliteMul = 1.7, ApexMul = 2.4;
        /// <summary>층당 트리클(capped) 상한 — INF_XP_FLOOR_CAP.</summary>
        public const int FloorCap = 60;
        /// <summary>전력망 유지 XP 주기(초) — INF_XP_GRID_TICK.</summary>
        public const double GridTick = 6.0;

        // ── 원본 INF_CARDS.need (12938): xpNeed = 30 + 10L + 2.4L²
        public const double NeedBase = 30, NeedLinear = 10, NeedQuad = 2.4;
        public const int TargetLevel = 10;

        static readonly double[,] Weight =
        {
            //           드릴러  거너   스카우트 엔지니어
            /* dig     */ { 1.00, 0.70, 0.80, 0.80 },
            /* combat  */ { 0.65, 1.00, 0.85, 0.80 },
            /* support */ { 0.60, 0.75, 0.75, 1.00 },
            /* recon   */ { 0.65, 0.75, 1.00, 0.80 },
            /* loot    */ { 0.85, 0.80, 0.90, 0.95 },
            /* objective*/{ 1.00, 1.00, 1.00, 1.00 },
        };

        public int Level { get; private set; } = 1;
        public int Xp { get; private set; }
        public int XpNeed { get; private set; } = NeedFor(1);
        public double XpMul { get; set; } = 1.0;

        /// <summary>이 층에서 capped 지급으로 쓴 양.</summary>
        public int CappedThisFloor { get; private set; }
        double _gridTimer;

        public readonly Dictionary<XpKind, int> Log = new Dictionary<XpKind, int>();

        /// <summary>레벨업 판정을 미루는 조건 — 보스 전투 중, 카드 모달 열림 등.</summary>
        public Func<bool> LevelCheckBlocked = () => false;

        public event Action<XpGainedEvent> Gained;
        public event Action<LevelUpEvent> LeveledUp;

        public static int NeedFor(int level) => (int)Math.Round(NeedBase + NeedLinear * level + NeedQuad * level * level);

        public static double WeightFor(XpKind kind, RoleId role) => Weight[(int)kind, (int)role];

        public void Reset()
        {
            Level = 1; Xp = 0; XpNeed = NeedFor(1);
            CappedThisFloor = 0; _gridTimer = 0;
            Log.Clear();
        }

        /// <summary>원본 infInitFloor — 트리클 상한은 층마다 회복한다.</summary>
        public void OnFloorInit() { CappedThisFloor = 0; _gridTimer = 0; }

        /// <summary>원본 infAwardXp. 반환값은 실제 지급량.</summary>
        public int Award(double baseAmount, XpKind kind, RoleId role, bool capped = false, string label = null, Vec2? at = null, bool checkLevel = true)
        {
            if (baseAmount <= 0) return 0;
            if (capped && CappedThisFloor >= FloorCap) return 0;

            int gain = Math.Max(1, JsMath.Round(baseAmount * WeightFor(kind, role) * XpMul));
            Xp += gain;
            Log[kind] = (Log.TryGetValue(kind, out int v) ? v : 0) + gain;
            if (capped) CappedThisFloor += gain;

            Gained?.Invoke(new XpGainedEvent { Amount = gain, Kind = kind, Label = label, At = at });
            if (checkLevel) CheckLevel();
            return gain;
        }

        /// <summary>원본 infCheckLevel — 한 번에 한 레벨만 올린다 (카드를 고르고 다음 판정).</summary>
        public bool CheckLevel()
        {
            if (LevelCheckBlocked()) return false;
            if (Xp < XpNeed) return false;
            Xp -= XpNeed;
            Level++;
            XpNeed = NeedFor(Level);
            LeveledUp?.Invoke(new LevelUpEvent { Level = Level, XpNeedNext = XpNeed });
            return true;
        }

        // ── 획득처 (원본 infOnBlockBroken · infAwardEnemyKillXp · 12448~12453)

        public void OnBlockBroken(TileType t, RoleId role, Vec2 at)
        {
            bool rare = t == TileType.Ore || t == TileType.Gem || t == TileType.Crys;
            Award(t == TileType.Stone ? XpStone : rare ? XpRare : XpDirt, XpKind.Dig, role, checkLevel: false, at: at);
        }

        public void OnFoundationBroken(TileType t, RoleId role, Vec2 at)
            => Award(t == TileType.Core ? XpCrackCore : XpCrack, XpKind.Dig, role, label: "균열 파쇄", at: at);

        public void OnEnemyKilled(EnemyState e, RoleId role, bool byTurret)
        {
            if (e.IsBoss) { Award(XpKillBoss, XpKind.Combat, role, label: "보스 격파", at: e.Position, checkLevel: false); return; }
            double tier = e.IsApex ? ApexMul : 1.0;
            if (byTurret) Award(XpTurretKill * tier, XpKind.Support, role, label: "센트리 처치", at: e.Position);
            else Award(XpKill * tier, XpKind.Combat, role, label: "제압", at: e.Position);
        }

        /// <summary>전력망 유지 — 노드에 연결돼 급전 중인 센트리 수만큼 시간이 쌓인다 (최대 3배속).</summary>
        public void TickGrid(int nodePoweredTurrets, double dt, RoleId role, Vec2 at)
        {
            if (nodePoweredTurrets <= 0) return;
            _gridTimer += dt * Math.Min(3, nodePoweredTurrets);
            if (_gridTimer >= GridTick)
            {
                _gridTimer -= GridTick;
                Award(XpGridUptime, XpKind.Support, role, capped: true, label: "전력망 유지", at: at);
            }
        }
    }
}
