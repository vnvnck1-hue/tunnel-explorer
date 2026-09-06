using System;
using System.Collections.Generic;

namespace TunnelCrew.Sim
{
    /// <summary>
    /// 영구 노드 효과 누산기 — 원본 <c>infPermNewState</c>(13807). 노드는 이것에만 적립하고,
    /// <see cref="PermanentNodes.Commit"/> 이 <see cref="PermanentNodes.Caps"/> 로 한 번 잘라 낸 뒤 런 상태에 반영한다.
    /// </summary>
    public sealed class PermState
    {
        public readonly HashSet<string> CardKeys = new HashSet<string>();
        public int Reroll, StartCards, RemoteEvery, Revive;
        public double KeepRate, KeepMin;
        public string Title = "";
        public double DmgDrill, DmgGun, Move, Xp, CoreChance, Hp, Mag, ReloadCut, Shield, OreHeal, Pierce, Shards, Shock, Magnet;
        public double Foundation, CoreNeedCut, QPower, QCdCut, DrillerShock;
        public double BreakerCdCut, BreakerRadius, BreakerDmg, BreakerEarly;
        public double Flare, GrappleRange, GrappleCdCut, Vision, PulseRadius, ExploreXp;
        public double Turrets, Nodes, NodeRadius, TurretMag, TurretRate, TurretLife;
        public bool Autonomous;
        // 런 중 상태
        public double RemoteAcc; public int RemoteSent, ReviveUsed;
        public readonly List<string> Capped = new List<string>();
    }

    public sealed class NodeDef
    {
        public string Id, SlotKey, Cluster, Owner, Branch, Type, Name, Icon;
        public string[] EffectByRank;
        public int MaxRank; public int[] CostByRank;
        public string[] Requires, Reveal, Ties;
        public int RequiresCount;
        public string Title;
        public Action<PermState, int> Apply;
        /// <summary>지도 좌표 (원본 GRID 1900×1620 월드).</summary>
        public double X, Y;
        public bool Initial;
        public int CostFor(int rank) => rank < MaxRank ? CostByRank[rank] : int.MaxValue;
    }

    /// <summary>
    /// 영구 성장 노드 80개 — 원본 <c>INF_NODE_SLOTS · INF_NODE_CLUSTERS · INF_NODE_DEFS · INF_PERMANENT_NODES</c>(11618~11830).
    /// 직업 잠금 없는 단일 트리: 크루 공용 2군집(16) + 직업 8군집(64). 한 런에는 공용 16 + 그 직업 16 = 32 적용.
    /// 슬롯 골격 i → m1·m2 → m3·m4 → m5·m6 → cap (두 갈래를 모두 올려야 캡스톤).
    /// </summary>
    public static class PermanentNodes
    {
        // ── 원본 INF_PERM_CAPS — 공용 + 직업 합산 후 한 번만 적용
        public static readonly Dictionary<string, double> Caps = new Dictionary<string, double>
        {
            ["DmgDrill"] = .35, ["DmgGun"] = .35, ["Move"] = .12, ["Xp"] = .30, ["CoreChance"] = .30, ["Hp"] = 72, ["Mag"] = 8, ["ReloadCut"] = .25,
            ["Shield"] = .45, ["OreHeal"] = 4, ["Pierce"] = 2, ["Shards"] = 12, ["Shock"] = 2, ["Magnet"] = 1,
            ["Foundation"] = .45, ["CoreNeedCut"] = .25, ["QPower"] = .45, ["QCdCut"] = .30, ["DrillerShock"] = 2,
            ["BreakerCdCut"] = .35, ["BreakerRadius"] = 2, ["BreakerDmg"] = .40, ["BreakerEarly"] = .40,
            ["Flare"] = .45, ["GrappleRange"] = 3, ["GrappleCdCut"] = .35, ["Vision"] = 2.5, ["PulseRadius"] = 2.5, ["ExploreXp"] = 4,
            ["Turrets"] = 2, ["Nodes"] = 2, ["NodeRadius"] = 2, ["TurretMag"] = 9, ["TurretRate"] = .20, ["TurretLife"] = .40,
            ["KeepRate"] = .60, ["KeepMin"] = 8, ["Reroll"] = 3, ["StartCards"] = 2, ["Revive"] = 1,
        };

        struct Slot { public string Type; public bool Initial; public int MaxRank; public int[] Cost; public string[] Requires, Reveal, Ties; public int RequiresCount; }
        static readonly (string key, Slot s)[] Slots =
        {
            ("i",   new Slot { Type = "기초 강화", Initial = true, MaxRank = 3, Cost = new[] { 1, 2, 3 }, Requires = new string[0], Reveal = new[] { "m1", "m2" }, Ties = new string[0] }),
            ("m1",  new Slot { Type = "전문화", MaxRank = 2, Cost = new[] { 3, 5 }, Requires = new[] { "i" }, Reveal = new[] { "m3" }, Ties = new[] { "m2" } }),
            ("m2",  new Slot { Type = "전문화", MaxRank = 2, Cost = new[] { 3, 5 }, Requires = new[] { "i" }, Reveal = new[] { "m4" }, Ties = new string[0] }),
            ("m3",  new Slot { Type = "전문화", MaxRank = 2, Cost = new[] { 4, 7 }, Requires = new[] { "m1" }, Reveal = new[] { "m5" }, Ties = new[] { "m4" } }),
            ("m4",  new Slot { Type = "전문화", MaxRank = 2, Cost = new[] { 4, 7 }, Requires = new[] { "m2" }, Reveal = new[] { "m6" }, Ties = new string[0] }),
            ("m5",  new Slot { Type = "행동 변형", MaxRank = 1, Cost = new[] { 9 }, Requires = new[] { "m3" }, Reveal = new[] { "cap" }, Ties = new[] { "m6" } }),
            ("m6",  new Slot { Type = "행동 변형", MaxRank = 1, Cost = new[] { 9 }, Requires = new[] { "m4" }, Reveal = new[] { "cap" }, Ties = new string[0] }),
            ("cap", new Slot { Type = "캡스톤", MaxRank = 1, Cost = new[] { 14 }, Requires = new[] { "m5", "m6" }, Reveal = new string[0], Ties = new string[0], RequiresCount = 2 }),
        };

        struct Cluster { public string Key, Owner, Ori, Branch; public double Ax, Ay; }
        static readonly Cluster[] Clusters =
        {
            new Cluster { Key = "crew_haul", Owner = "crew", Ori = "sw", Ax = 850, Ay = 1200, Branch = "물류·회수" },
            new Cluster { Key = "crew_cmd", Owner = "crew", Ori = "se", Ax = 1050, Ay = 1200, Branch = "원정 지휘" },
            new Cluster { Key = "driller_gear", Owner = "driller", Ori = "nw", Ax = 585, Ay = 855, Branch = "역할 장비" },
            new Cluster { Key = "driller_surv", Owner = "driller", Ori = "sw", Ax = 585, Ay = 1035, Branch = "생존·기동" },
            new Cluster { Key = "gunner_gear", Owner = "gunner", Ori = "nw", Ax = 750, Ay = 745, Branch = "역할 장비" },
            new Cluster { Key = "gunner_surv", Owner = "gunner", Ori = "n", Ax = 870, Ay = 705, Branch = "생존·기동" },
            new Cluster { Key = "scout_gear", Owner = "scout", Ori = "n", Ax = 1030, Ay = 705, Branch = "역할 장비" },
            new Cluster { Key = "scout_surv", Owner = "scout", Ori = "ne", Ax = 1150, Ay = 745, Branch = "생존·기동" },
            new Cluster { Key = "engineer_gear", Owner = "engineer", Ori = "ne", Ax = 1315, Ay = 855, Branch = "역할 장비" },
            new Cluster { Key = "engineer_surv", Owner = "engineer", Ori = "se", Ax = 1315, Ay = 1035, Branch = "생존·기동" },
        };
        const double GridCx = 88, GridCy = 88;

        static (double x, double y) Offset(string ori, string slot)
        {
            double dx = ori.Contains("e") ? 1 : ori.Contains("w") ? -1 : 0;
            double dy = ori.Contains("s") ? 1 : ori.Contains("n") ? -1 : 0;
            double len = Math.Sqrt(dx * dx + dy * dy); if (len == 0) len = 1; dx /= len; dy /= len;
            double px = -dy, py = dx; const double spread = .48;
            (double, double) At(int step, int side) => (dx * step + px * spread * side, dy * step + py * spread * side);
            return slot switch
            {
                "i" => (0, 0), "m1" => At(1, 1), "m2" => At(1, -1), "m3" => At(2, 1), "m4" => At(2, -1),
                "m5" => At(3, 1), "m6" => At(3, -1), _ => (dx * 4, dy * 4),
            };
        }

        sealed class Def { public string N, Icon, Type, Title; public string[] Eff; public Action<PermState, int> A; }
        static Def N(string n, string icon, string[] eff, Action<PermState, int> a, string type = null, string title = null)
            => new Def { N = n, Icon = icon, Eff = eff, A = a, Type = type, Title = title };

        // ── 원본 INF_NODE_DEFS — 한 줄씩 옮김
        static readonly Dictionary<string, Dictionary<string, Def>> Defs = new Dictionary<string, Dictionary<string, Def>>
        {
            ["crew_haul"] = new Dictionary<string, Def>
            {
                ["i"] = N("광석 호퍼", "ore-refinery", new[] { "추가 코어 확률 +4%", "추가 코어 확률 +8%", "추가 코어 확률 +12%" }, (P, r) => P.CoreChance += .04 * r),
                ["m1"] = N("적재 베어링", "piercing-round", new[] { "탄창 +2", "탄창 +4" }, (P, r) => P.Mag += 2 * r),
                ["m2"] = N("자동 급탄기", "rapid-fire-turbo", new[] { "재장전 -6%", "재장전 -12%" }, (P, r) => P.ReloadCut += .06 * r),
                ["m3"] = N("안전 주머니", "core-bedrock", new[] { "쓰러져도 코어 20% 회수", "쓰러져도 코어 35% 회수" }, (P, r) => P.KeepRate = Math.Max(P.KeepRate, r == 1 ? .20 : .35), "회수 강화"),
                ["m4"] = N("충격 완충 화물칸", "core-bedrock", new[] { "최소 보존 3코어", "최소 보존 6코어" }, (P, r) => P.KeepMin = Math.Max(P.KeepMin, 3 * r), "회수 강화"),
                ["m5"] = N("원격 전송 앵커", "control-network", new[] { "코어를 5개 캘 때마다 1개를 기지로 즉시 전송합니다 — 쓰러져도 남습니다" }, (P, r) => P.RemoteEvery = P.RemoteEvery > 0 ? Math.Min(P.RemoteEvery, 5) : 5),
                ["m6"] = N("자원 흡입기", "ore-refinery", new[] { "자원이 먼 거리에서도 끌려오고 광석 회수 시 체력 +2" }, (P, r) => { P.Magnet = Math.Max(P.Magnet, 1); P.OreHeal += 2; }),
                ["cap"] = N("원정 물류 숙련", "ore-refinery", new[] { "회수율 +15%p · 전송 주기 3 · 추가 코어 +8% · 탄창 +2" }, (P, r) => { P.KeepRate += .15; P.RemoteEvery = P.RemoteEvery > 0 ? Math.Min(P.RemoteEvery, 3) : 3; P.CoreChance += .08; P.Mag += 2; }),
            },
            ["crew_cmd"] = new Dictionary<string, Def>
            {
                ["i"] = N("원정 교범", "control-network", new[] { "역할 행동 경험치 +5%", "역할 행동 경험치 +10%", "역할 행동 경험치 +15%" }, (P, r) => P.Xp += .05 * r),
                ["m1"] = N("작전 기록", "scanner-radar", new[] { "역할 경험치 +4%", "역할 경험치 +8%" }, (P, r) => P.Xp += .04 * r),
                ["m2"] = N("예비 설계도", "scanner-radar", new[] { "런 특성 리롤 +1", "런 특성 리롤 +2" }, (P, r) => P.Reroll += r, "전문 분야"),
                ["m3"] = N("현장 분석반", "control-network", new[] { "역할 경험치 +3%", "역할 경험치 +6%" }, (P, r) => P.Xp += .03 * r),
                ["m4"] = N("보급 창고", "survival-bulkhead", new[] { "최대 체력 +12", "최대 체력 +24" }, (P, r) => P.Hp += 12 * r),
                ["m5"] = N("출격 프리셋", "dual-power", new[] { "원정 시작 시 특성 카드 1장을 먼저 고릅니다" }, (P, r) => P.StartCards += 1),
                ["m6"] = N("정예 편성", "dual-power", new[] { "원정 시작 카드 +1 (합계 2장)" }, (P, r) => P.StartCards += 1),
                ["cap"] = N("원정 지휘 숙련", "dual-power", new[] { "역할 경험치 +6% · 리롤 +1 · 최대 체력 +14" }, (P, r) => { P.Xp += .06; P.Reroll += 1; P.Hp += 14; }),
            },
            ["driller_gear"] = new Dictionary<string, Def>
            {
                ["i"] = N("토크 보정기", "driller-torque-motor", new[] { "굴착 위력 +5%", "굴착 위력 +10%", "굴착 위력 +15%" }, (P, r) => P.DmgDrill += .05 * r),
                ["m1"] = N("단층 추적 비트", "crack-fissure", new[] { "기반암 균열 +10%", "기반암 균열 +20%" }, (P, r) => P.Foundation += .10 * r),
                ["m2"] = N("내열 구동축", "rapid-fire-turbo", new[] { "굴착 위력 +6%", "굴착 위력 +12%" }, (P, r) => P.DmgDrill += .06 * r),
                ["m3"] = N("맨틀 천공 키", "core-bedrock", new[] { "코어 암반 필요 압력 -8%", "코어 암반 필요 압력 -16%" }, (P, r) => P.CoreNeedCut += .08 * r),
                ["m4"] = N("공진 헤드", "seismic-shockwave", new[] { "굴착 +5% · 기반암 +8%", "굴착 +10% · 기반암 +16%" }, (P, r) => { P.DmgDrill += .05 * r; P.Foundation += .08 * r; }),
                ["m5"] = N("돌파 파기 개조", "seismic-shockwave", new[] { "Q 압력 +30% · Q 대기 -20% · 기반암을 뚫으면 파쇄 충격파가 퍼집니다" }, (P, r) => { P.QPower += .30; P.QCdCut += .20; P.DrillerShock = Math.Max(P.DrillerShock, 1); }),
                ["m6"] = N("연쇄 파쇄 코어", "explosive-charge", new[] { "벽을 부수면 주변 벽에 충격파가 퍼지고 파편이 날아갑니다" }, (P, r) => { P.Shock = Math.Max(P.Shock, 1); P.Shards = Math.Max(P.Shards, 8); }),
                ["cap"] = N("기반암 지배자", "dual-power", new[] { "굴착 +12% · 기반암 +15% · 코어 암반 압력 -8% · 런 특성 심층 시추 계열 해금" }, (P, r) => { P.DmgDrill += .12; P.Foundation += .15; P.CoreNeedCut += .08; P.CardKeys.Add("deep"); }),
            },
            ["driller_surv"] = new Dictionary<string, Def>
            {
                ["i"] = N("압력 라이닝", "survival-bulkhead", new[] { "최대 체력 +10", "최대 체력 +20", "최대 체력 +30" }, (P, r) => P.Hp += 10 * r),
                ["m1"] = N("비상 격벽", "survival-bulkhead", new[] { "최대 체력 +10", "최대 체력 +20" }, (P, r) => P.Hp += 10 * r),
                ["m2"] = N("보행 서보", "ricochet", new[] { "이동 +2%", "이동 +4%" }, (P, r) => P.Move += .02 * r),
                ["m3"] = N("충격 흡수 골격", "survival-bulkhead", new[] { "체력 +8 · 이동 +2%", "체력 +16 · 이동 +4%" }, (P, r) => { P.Hp += 8 * r; P.Move += .02 * r; }),
                ["m4"] = N("광석 정제 회로", "ore-refinery", new[] { "광석 회수 시 체력 +1", "광석 회수 시 체력 +2" }, (P, r) => P.OreHeal += r),
                ["m5"] = N("파쇄 보호막", "survival-bulkhead", new[] { "벽을 부술 때마다 짧은 보호막을 얻습니다" }, (P, r) => P.Shield = Math.Max(P.Shield, .28)),
                ["m6"] = N("긴급 재기동", "dual-power", new[] { "원정당 한 번, 쓰러져도 체력 35%로 다시 일어섭니다" }, (P, r) => P.Revive += 1),
                ["cap"] = N("불굴의 굴착자", "dual-power", new[] { "체력 +18 · 이동 +4% · 런 특성 기반암 공학 계열 해금 · 칭호 「기반암 파쇄자」" }, (P, r) => { P.Hp += 18; P.Move += .04; P.CardKeys.Add("bedrock"); }, title: "기반암 파쇄자"),
            },
            ["gunner_gear"] = new Dictionary<string, Def>
            {
                ["i"] = N("총열 정렬", "gunner-chamber-cycler", new[] { "전투 위력 +5%", "전투 위력 +10%", "전투 위력 +15%" }, (P, r) => P.DmgGun += .05 * r),
                ["m1"] = N("약실 순환기", "rapid-fire-turbo", new[] { "파쇄탄 재사용 -8%", "파쇄탄 재사용 -15%" }, (P, r) => P.BreakerCdCut += r == 1 ? .08 : .15),
                ["m2"] = N("강선 가공", "piercing-round", new[] { "전투 위력 +6%", "전투 위력 +12%" }, (P, r) => P.DmgGun += .06 * r),
                ["m3"] = N("관통 탄자", "piercing-round", new[] { "전투 +4%", "전투 +8% · 관통 +1" }, (P, r) => { P.DmgGun += .04 * r; if (r >= 2) P.Pierce += 1; }),
                ["m4"] = N("조기 기폭 회로", "fuse-detonator", new[] { "조기 기폭 위력 +15%", "조기 기폭 위력 +30%" }, (P, r) => P.BreakerEarly += .15 * r),
                ["m5"] = N("성형 작약 개조", "explosive-charge", new[] { "파쇄탄 반경 +1 · 벽 피해 +20%" }, (P, r) => { P.BreakerRadius += 1; P.BreakerDmg += .20; }),
                ["m6"] = N("비산 탄자 장전", "shrapnel", new[] { "벽을 부수면 파편이 분출되고 파쇄탄 벽 피해 +15%" }, (P, r) => { P.Shards = Math.Max(P.Shards, 10); P.BreakerDmg += .15; }),
                ["cap"] = N("제압 사격 교리", "dual-power", new[] { "전투 +12% · 파쇄탄 재사용 -12% · 관통 +1 · 런 특성 파편 운용 계열 해금" }, (P, r) => { P.DmgGun += .12; P.BreakerCdCut += .12; P.Pierce += 1; P.CardKeys.Add("shrapnel"); }),
            },
            ["gunner_surv"] = new Dictionary<string, Def>
            {
                ["i"] = N("방탄 라이닝", "survival-bulkhead", new[] { "최대 체력 +10", "최대 체력 +20", "최대 체력 +30" }, (P, r) => P.Hp += 10 * r),
                ["m1"] = N("중장갑 흉갑", "survival-bulkhead", new[] { "최대 체력 +10", "최대 체력 +20" }, (P, r) => P.Hp += 10 * r),
                ["m2"] = N("보조 추진기", "ricochet", new[] { "이동 +2%", "이동 +4%" }, (P, r) => P.Move += .02 * r),
                ["m3"] = N("전장 정비", "survival-bulkhead", new[] { "체력 +8 · 이동 +2%", "체력 +16 · 이동 +4%" }, (P, r) => { P.Hp += 8 * r; P.Move += .02 * r; }),
                ["m4"] = N("충격 흡수 패드", "ore-refinery", new[] { "광석 회수 시 체력 +1", "광석 회수 시 체력 +2" }, (P, r) => P.OreHeal += r),
                ["m5"] = N("전개형 방어막", "survival-bulkhead", new[] { "벽을 부술 때마다 짧은 보호막을 얻습니다" }, (P, r) => P.Shield = Math.Max(P.Shield, .28)),
                ["m6"] = N("긴급 재기동", "dual-power", new[] { "원정당 한 번, 쓰러져도 체력 35%로 다시 일어섭니다" }, (P, r) => P.Revive += 1),
                ["cap"] = N("전선 지휘관", "dual-power", new[] { "체력 +18 · 이동 +4% · 런 특성 포화 사격 계열 해금 · 칭호 「전선의 방벽」" }, (P, r) => { P.Hp += 18; P.Move += .04; P.CardKeys.Add("barrage"); }, title: "전선의 방벽"),
            },
            ["scout_gear"] = new Dictionary<string, Def>
            {
                ["i"] = N("절삭기 조율", "crack-fissure", new[] { "절삭·사격 +4%", "절삭·사격 +8%", "절삭·사격 +12%" }, (P, r) => { P.DmgDrill += .04 * r; P.DmgGun += .04 * r; }),
                ["m1"] = N("고광도 연소제", "scout-high-luminosity-fuel", new[] { "플레어 반경·지속 +12%", "플레어 반경·지속 +24%" }, (P, r) => P.Flare += .12 * r),
                ["m2"] = N("카빈 정밀 조정", "piercing-round", new[] { "전투 위력 +6%", "전투 위력 +12%" }, (P, r) => P.DmgGun += .06 * r),
                ["m3"] = N("광학 증폭기", "scanner-radar", new[] { "독립 시야 +0.6칸", "독립 시야 +1.2칸" }, (P, r) => P.Vision += .6 * r),
                ["m4"] = N("반향 측량기", "scanner-radar", new[] { "정찰 반경 +0.8칸 · 탐사 경험치 +1", "정찰 반경 +1.6칸 · 탐사 경험치 +2" }, (P, r) => { P.PulseRadius += .8 * r; P.ExploreXp += r; }),
                ["m5"] = N("그래플 윈치 개조", "grapple-winch", new[] { "그래플 사거리 +2칸 · 재사용 -25%" }, (P, r) => { P.GrappleRange += 2; P.GrappleCdCut += .25; }),
                ["m6"] = N("섬광 파쇄", "shrapnel", new[] { "벽을 부수면 파편이 분출되고 절삭 위력 +10%" }, (P, r) => { P.Shards = Math.Max(P.Shards, 8); P.DmgDrill += .10; }),
                ["cap"] = N("길잡이 프로토콜", "dual-power", new[] { "독립 시야 +1칸 · 플레어 +15% · 절삭 +10% · 런 특성 심층 정찰 계열 해금" }, (P, r) => { P.Vision += 1; P.Flare += .15; P.DmgDrill += .10; P.CardKeys.Add("recon"); }),
            },
            ["scout_surv"] = new Dictionary<string, Def>
            {
                ["i"] = N("경량 방호복", "survival-bulkhead", new[] { "체력 +7 · 이동 +1%", "체력 +14 · 이동 +2%", "체력 +21 · 이동 +3%" }, (P, r) => { P.Hp += 7 * r; P.Move += .01 * r; }),
                ["m1"] = N("주자 강화", "survival-bulkhead", new[] { "최대 체력 +8", "최대 체력 +16" }, (P, r) => P.Hp += 8 * r),
                ["m2"] = N("가속 부츠", "ricochet", new[] { "이동 +2.5%", "이동 +5%" }, (P, r) => P.Move += .025 * r),
                ["m3"] = N("지구력 훈련", "survival-bulkhead", new[] { "체력 +6 · 이동 +1.5%", "체력 +12 · 이동 +3%" }, (P, r) => { P.Hp += 6 * r; P.Move += .015 * r; }),
                ["m4"] = N("생체 회복 팩", "ore-refinery", new[] { "광석 회수 시 체력 +1", "광석 회수 시 체력 +2" }, (P, r) => P.OreHeal += r),
                ["m5"] = N("회피 기동", "survival-bulkhead", new[] { "벽을 부술 때마다 짧은 보호막을 얻습니다" }, (P, r) => P.Shield = Math.Max(P.Shield, .32)),
                ["m6"] = N("긴급 재기동", "dual-power", new[] { "원정당 한 번, 쓰러져도 체력 35%로 다시 일어섭니다" }, (P, r) => P.Revive += 1),
                ["cap"] = N("그림자 보행", "dual-power", new[] { "이동 +4% · 체력 +14 · 런 특성 개척 항로 계열 해금 · 칭호 「어둠을 여는 자」" }, (P, r) => { P.Move += .04; P.Hp += 14; P.CardKeys.Add("pathfind"); }, title: "어둠을 여는 자"),
            },
            ["engineer_gear"] = new Dictionary<string, Def>
            {
                ["i"] = N("공학 커터 조율", "engineer-high-density-battery", new[] { "커터·사격 +4%", "커터·사격 +8%", "커터·사격 +12%" }, (P, r) => { P.DmgDrill += .04 * r; P.DmgGun += .04 * r; }),
                ["m1"] = N("고밀도 전지", "engineer-high-density-battery", new[] { "센트리 탄창 +3 · 연사 +4%", "센트리 탄창 +6 · 연사 +8%" }, (P, r) => { P.TurretMag += 3 * r; P.TurretRate += .04 * r; }),
                ["m2"] = N("서비스 총기 조정", "piercing-round", new[] { "전투 위력 +6%", "전투 위력 +12%" }, (P, r) => P.DmgGun += .06 * r),
                ["m3"] = N("내구 프레임", "sentry-turret", new[] { "센트리 지속 +12%", "센트리 지속 +24%" }, (P, r) => P.TurretLife += .12 * r),
                ["m4"] = N("전력망 중계기", "control-network", new[] { "전력 노드 최대 +1 · 반경 +1칸", "전력 노드 최대 +1 · 반경 +2칸" }, (P, r) => { P.Nodes = Math.Max(P.Nodes, 1); P.NodeRadius += r; }),
                ["m5"] = N("자율 가동 회로", "sentry-turret", new[] { "센트리 최대 +1 · 센트리가 스스로 표적을 찾습니다" }, (P, r) => { P.Turrets += 1; P.Autonomous = true; }),
                ["m6"] = N("과부하 방전", "dual-power", new[] { "벽을 부수면 주변 벽에 충격파가 퍼지고 커터 위력 +10%" }, (P, r) => { P.Shock = Math.Max(P.Shock, 1); P.DmgDrill += .10; }),
                ["cap"] = N("심층 요새 교리", "sentry-turret", new[] { "센트리 +1 · 지속 +20% · 전력 노드 반경 +1칸 · 런 특성 전력망 확장 계열 해금" }, (P, r) => { P.Turrets += 1; P.TurretLife += .20; P.NodeRadius += 1; P.CardKeys.Add("grid"); }),
            },
            ["engineer_surv"] = new Dictionary<string, Def>
            {
                ["i"] = N("외골격 프레임", "survival-bulkhead", new[] { "최대 체력 +10", "최대 체력 +20", "최대 체력 +30" }, (P, r) => P.Hp += 10 * r),
                ["m1"] = N("보강 장갑판", "survival-bulkhead", new[] { "최대 체력 +10", "최대 체력 +20" }, (P, r) => P.Hp += 10 * r),
                ["m2"] = N("보조 구동계", "ricochet", new[] { "이동 +2%", "이동 +4%" }, (P, r) => P.Move += .02 * r),
                ["m3"] = N("구조 공학", "survival-bulkhead", new[] { "체력 +8 · 이동 +2%", "체력 +16 · 이동 +4%" }, (P, r) => { P.Hp += 8 * r; P.Move += .02 * r; }),
                ["m4"] = N("정비 나노팩", "ore-refinery", new[] { "광석 회수 시 체력 +1", "광석 회수 시 체력 +2" }, (P, r) => P.OreHeal += r),
                ["m5"] = N("현장 용접 보호막", "survival-bulkhead", new[] { "벽을 부술 때마다 짧은 보호막을 얻습니다" }, (P, r) => P.Shield = Math.Max(P.Shield, .28)),
                ["m6"] = N("긴급 재기동", "dual-power", new[] { "원정당 한 번, 쓰러져도 체력 35%로 다시 일어섭니다" }, (P, r) => P.Revive += 1),
                ["cap"] = N("불괴의 설계자", "dual-power", new[] { "체력 +18 · 이동 +4% · 런 특성 요새 설계 계열 해금 · 칭호 「공간을 만드는 자」" }, (P, r) => { P.Hp += 18; P.Move += .04; P.CardKeys.Add("fortress"); }, title: "공간을 만드는 자"),
            },
        };

        public static readonly NodeDef[] All;
        static readonly Dictionary<string, NodeDef> _byId = new Dictionary<string, NodeDef>();

        static PermanentNodes()
        {
            var list = new List<NodeDef>();
            foreach (var c in Clusters)
            {
                var defs = Defs[c.Key];
                foreach (var (sKey, s) in Slots)
                {
                    if (!defs.TryGetValue(sKey, out var d)) continue;
                    string Full(string k) => c.Key + "_" + k;
                    var (ox, oy) = Offset(c.Ori, sKey);
                    var node = new NodeDef
                    {
                        Id = Full(sKey), SlotKey = sKey, Cluster = c.Key, Owner = c.Owner, Branch = c.Branch,
                        Type = d.Type ?? s.Type, Name = d.N, Icon = d.Icon, EffectByRank = d.Eff, Apply = d.A, Title = d.Title,
                        MaxRank = s.MaxRank, CostByRank = s.Cost, Initial = s.Initial, RequiresCount = s.RequiresCount,
                        Requires = Array.ConvertAll(s.Requires, Full), Reveal = Array.ConvertAll(s.Reveal, Full), Ties = Array.ConvertAll(s.Ties, Full),
                        X = c.Ax + ox * GridCx, Y = c.Ay + oy * GridCy,
                    };
                    list.Add(node); _byId[node.Id] = node;
                }
            }
            All = list.ToArray();
        }

        public static NodeDef ById(string id) => id != null && _byId.TryGetValue(id, out var n) ? n : null;

        /// <summary>한 런에 적용되는 노드 — 공용 전부 + 그 직업의 가지 (§6.2 개정).</summary>
        public static IEnumerable<NodeDef> RunNodes(RoleId role)
        {
            string owner = role.ToString().ToLowerInvariant();
            foreach (var n in All) if (n.Owner == "crew" || n.Owner == owner) yield return n;
        }

        public static int RankMax(IEnumerable<NodeDef> list = null) { int s = 0; foreach (var n in list ?? All) s += n.MaxRank; return s; }
        public static int CostTotal() { int s = 0; foreach (var n in All) foreach (var c in n.CostByRank) s += c; return s; }

        /// <summary>원본 infPermanentPrereqsMet — requires 중 requiresCount(기본 전부) 개가 랭크 ≥ 1.</summary>
        public static bool PrereqsMet(NodeDef node, MetaState meta)
        {
            if (node.Requires.Length == 0) return true;
            int cnt = 0; foreach (var id in node.Requires) if (meta.RankOf(id) > 0) cnt++;
            return cnt >= (node.RequiresCount > 0 ? node.RequiresCount : node.Requires.Length);
        }

        /// <summary>§6.4.3 — 발견된 노드만 지도에 보인다: 초기 노드, 랭크가 있는 노드, 랭크 있는 노드가 reveal 하는 노드.</summary>
        public static bool Visible(NodeDef node, MetaState meta)
        {
            if (node.Initial || meta.RankOf(node.Id) > 0) return true;
            foreach (var n in All) if (meta.RankOf(n.Id) > 0 && Array.IndexOf(n.Reveal, node.Id) >= 0) return true;
            return false;
        }

        /// <summary>구매 가능 여부 — 랭크 여유 · 선행 · 코어.</summary>
        public static bool CanBuy(NodeDef node, MetaState meta)
        {
            int rank = meta.RankOf(node.Id);
            return rank < node.MaxRank && PrereqsMet(node, meta) && meta.bankedCores >= node.CostFor(rank);
        }

        /// <summary>원본 infBuyPermanentNode 의 규칙 부분 — 차감·랭크 증가. 저장 실패 롤백은 호출자(MetaStore)가 한다.</summary>
        public static bool Buy(NodeDef node, MetaState meta)
        {
            if (!CanBuy(node, meta)) return false;
            int rank = meta.RankOf(node.Id);
            meta.bankedCores -= node.CostFor(rank);
            meta.SetRank(node.Id, rank + 1);
            return true;
        }

        /// <summary>원본 infPermCollect — 런 노드의 효과를 누산기에 적립.</summary>
        public static PermState Collect(MetaState meta, RoleId role)
        {
            var P = new PermState();
            foreach (var node in RunNodes(role))
            {
                int rank = meta.RankOf(node.Id);
                if (rank <= 0) continue;
                node.Apply(P, rank);
                if (node.Title != null) P.Title = node.Title;
            }
            return P;
        }

        /// <summary>원본 infPermClamp — 상한 적용, 잘린 항목은 Capped 에 기록.</summary>
        public static void Clamp(PermState P)
        {
            void C(ref double v, string key) { if (v > Caps[key]) { v = Caps[key]; P.Capped.Add(key); } }
            C(ref P.DmgDrill, "DmgDrill"); C(ref P.DmgGun, "DmgGun"); C(ref P.Move, "Move"); C(ref P.Xp, "Xp"); C(ref P.CoreChance, "CoreChance");
            C(ref P.Hp, "Hp"); C(ref P.Mag, "Mag"); C(ref P.ReloadCut, "ReloadCut"); C(ref P.Shield, "Shield"); C(ref P.OreHeal, "OreHeal");
            C(ref P.Pierce, "Pierce"); C(ref P.Shards, "Shards"); C(ref P.Shock, "Shock"); C(ref P.Magnet, "Magnet");
            C(ref P.Foundation, "Foundation"); C(ref P.CoreNeedCut, "CoreNeedCut"); C(ref P.QPower, "QPower"); C(ref P.QCdCut, "QCdCut"); C(ref P.DrillerShock, "DrillerShock");
            C(ref P.BreakerCdCut, "BreakerCdCut"); C(ref P.BreakerRadius, "BreakerRadius"); C(ref P.BreakerDmg, "BreakerDmg"); C(ref P.BreakerEarly, "BreakerEarly");
            C(ref P.Flare, "Flare"); C(ref P.GrappleRange, "GrappleRange"); C(ref P.GrappleCdCut, "GrappleCdCut"); C(ref P.Vision, "Vision"); C(ref P.PulseRadius, "PulseRadius"); C(ref P.ExploreXp, "ExploreXp");
            C(ref P.Turrets, "Turrets"); C(ref P.Nodes, "Nodes"); C(ref P.NodeRadius, "NodeRadius"); C(ref P.TurretMag, "TurretMag"); C(ref P.TurretRate, "TurretRate"); C(ref P.TurretLife, "TurretLife");
            C(ref P.KeepRate, "KeepRate"); C(ref P.KeepMin, "KeepMin");
            if (P.Reroll > Caps["Reroll"]) { P.Reroll = (int)Caps["Reroll"]; P.Capped.Add("Reroll"); }
            if (P.StartCards > Caps["StartCards"]) { P.StartCards = (int)Caps["StartCards"]; P.Capped.Add("StartCards"); }
            if (P.Revive > Caps["Revive"]) { P.Revive = (int)Caps["Revive"]; P.Capped.Add("Revive"); }
        }

        /// <summary>원본 infPermCommit — 상한 적용 후 런 상태(PlayerBuild·RoleTuning·Player)에 반영.</summary>
        public static void Commit(PermState P, PlayerBuild b, PlayerState p, TraitDeck traits)
        {
            Clamp(P);
            var T = b.Roles;
            b.DrillMul *= 1 + P.DmgDrill; b.GunMul *= 1 + P.DmgGun; b.MoveMul *= 1 + P.Move; b.XpMul *= 1 + P.Xp;
            b.CoreBonusChance = Math.Min(1, b.CoreBonusChance + P.CoreChance);
            if (P.Hp > 0) { p.HpMax += P.Hp; p.Hp += P.Hp; }
            if (P.Mag > 0) b.SetMag((int)P.Mag);
            if (P.ReloadCut > 0) b.AdjustReload(1 - P.ReloadCut);
            if (P.Shield > 0) b.BreakShield = Math.Max(b.BreakShield, P.Shield);
            if (P.OreHeal > 0) b.OreHeal += P.OreHeal;
            if (P.Pierce > 0) b.Pierce += (int)P.Pierce;
            if (P.Shards > 0) b.ShardBurst = Math.Max(b.ShardBurst, (int)P.Shards);
            if (P.Magnet > 0) { b.LootMagnetMul *= 1.5; b.LootPickupMul *= 1.15; }
            if (P.Shock > 0) { b.BreakShockRadius = Math.Max(b.BreakShockRadius, P.Shock); b.BreakShockPower = Math.Max(b.BreakShockPower, .16); }
            if (P.Foundation > 0) T.DrillerFoundationMul *= 1 + P.Foundation;
            if (P.CoreNeedCut > 0) T.DrillerCoreNeedMul *= 1 - P.CoreNeedCut;
            if (P.QPower > 0) T.DrillerQMul *= 1 + P.QPower;
            if (P.QCdCut > 0) T.DrillerQCdMul *= 1 - P.QCdCut;
            if (P.DrillerShock > 0) T.DrillerShockRadius = Math.Max(T.DrillerShockRadius, (int)P.DrillerShock);
            if (P.BreakerCdCut > 0) T.BreakerMaxCd = Math.Max(5, T.BreakerMaxCd * (1 - P.BreakerCdCut));
            if (P.BreakerRadius > 0) T.BreakerRadius += (int)P.BreakerRadius;
            if (P.BreakerDmg > 0) T.BreakerDamageMul *= 1 + P.BreakerDmg;
            if (P.BreakerEarly > 0) T.BreakerEarlyMul *= 1 + P.BreakerEarly;
            if (P.Flare > 0) { T.ScoutFlareRadMul *= 1 + P.Flare; T.ScoutFlareLifeMul *= 1 + P.Flare; }
            if (P.GrappleRange > 0) T.ScoutGrappleRange += P.GrappleRange;
            if (P.GrappleCdCut > 0) T.ScoutGrappleCdMul *= 1 - P.GrappleCdCut;
            if (P.Vision > 0) T.ScoutVisionBonus += P.Vision;
            if (P.PulseRadius > 0) T.ScoutPulseRadius += P.PulseRadius;
            if (P.ExploreXp > 0) T.ScoutExploreXp += (int)P.ExploreXp;
            if (P.Turrets > 0) T.EngineerMaxTurrets += (int)P.Turrets;
            if (P.Nodes > 0) T.EngineerMaxNodes += (int)P.Nodes;
            if (P.NodeRadius > 0) T.EngineerNodeRadius += P.NodeRadius;
            if (P.TurretMag > 0) T.EngineerTurretMag += (int)P.TurretMag;
            if (P.TurretRate > 0) T.EngineerTurretInterval *= 1 - P.TurretRate;
            if (P.TurretLife > 0) T.EngineerTurretLife *= 1 + P.TurretLife;
            if (P.Autonomous) T.EngineerAutonomous = true;
            // 카드 계열 해금 · 리롤 재고
            if (traits != null) { foreach (var k in P.CardKeys) traits.Unlocked.Add(k); traits.Rerolls += P.Reroll; }
        }
    }
}
