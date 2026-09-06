using System;
using System.Collections.Generic;

namespace TunnelCrew.Sim
{
    /// <summary>특성 카드가 손대는 것들. 원본은 INF · G · CREW 전역이었다.</summary>
    public sealed class TraitContext
    {
        public PlayerBuild Build;
        public PlayerState Player;
        public RoleSystem Roles;
        public RoleTuning T => Build.Roles;
    }

    /// <summary>
    /// 특성 카드 한 장. 원본 INF_TRAITS 원소 <c>{role,kind,id,tier,n,d,ok,a}</c> 와 같다.
    /// <see cref="Ok"/> 는 "더 뽑을 수 있는가", <see cref="Apply"/> 는 효과.
    /// </summary>
    public sealed class TraitDef
    {
        public string Id, Name, Desc, Kind;
        /// <summary>null = 전 직업(원본 'all').</summary>
        public RoleId? Role;
        public int Tier;
        /// <summary>드릴이 있는 직업만 (원본 req:'drill'). 거너 제외.</summary>
        public bool NeedsDrill;
        /// <summary>영구 노드로만 열리는 계열 (원본 lock). M5 메타에서 해금 상태를 준다.</summary>
        public string Lock;
        /// <summary>효과가 별도 서브시스템(자동 드릴·행성 파쇄기 등)이라 아직 이식되지 않음 — 풀에서 제외.</summary>
        public bool NotPorted;
        public Func<TraitContext, bool> Ok = _ => true;
        public Action<TraitContext> Apply = _ => { };
    }

    public struct TraitOfferEvent { public TraitDef[] Cards; public int Level; public bool Legend; }
    public struct TraitPickedEvent { public TraitDef Card; public int Stack; }

    /// <summary>
    /// 특성 덱 — 원본 <c>INF_TRAITS · INF_LEGENDS · infPickTraits · infRollTier · infApplyTraitChoice</c>
    /// (12025~12232, 12855~12900). 효과는 <see cref="PlayerBuild"/> / <see cref="RoleTuning"/> 필드 변경으로 옮겼다.
    ///
    /// 등급 가중치는 절대 레벨이 아니라 행성 목표 레벨(10) 대비 진행도로 계산한다(§4.3).
    /// 피티: 3장 중 3티어 이상이 없으면 +1, 4 이상이거나 5의 배수 레벨이면 첫 장을 고등급으로 강제.
    /// </summary>
    public sealed class TraitDeck
    {
        readonly Rng _rng;
        public readonly Dictionary<string, int> Stacks = new Dictionary<string, int>();
        public readonly List<TraitDef> PickLog = new List<TraitDef>();
        public readonly HashSet<string> Fx = new HashSet<string>();   // 원본 INF.traitFx
        public readonly HashSet<string> Unlocked = new HashSet<string>();   // 영구 노드 해금 계열
        public int Pity;
        public int Rerolls;

        /// <summary>대기 중인 제시. Presentation 이 고르면 <see cref="Pick"/>.</summary>
        public TraitDef[] Offer { get; private set; }
        public bool OfferIsLegend { get; private set; }
        public bool HasOffer => Offer != null && Offer.Length > 0;

        public event Action<TraitOfferEvent> Offered;
        public event Action<TraitPickedEvent> Picked;

        // 원본 INF_CARDS 리롤
        public const int RerollStart = 1, RerollPerStratum = 1, RerollCap = 2;

        public TraitDeck(uint seed = 0x7EA7) { _rng = new Rng(seed); Rerolls = RerollStart; }

        public void Reset() { Stacks.Clear(); PickLog.Clear(); Fx.Clear(); Pity = 0; Rerolls = RerollStart; Offer = null; }

        /// <summary>원본 infInitFloor — 리롤은 지층마다 +1, 상한 2.</summary>
        public void OnFloorInit(int depth)
        {
            Rerolls = depth <= 1 ? RerollStart : Math.Min(RerollCap, Rerolls + RerollPerStratum);
        }

        public int StackOf(string id) => Stacks.TryGetValue(id, out int n) ? n : 0;

        bool RoleOk(TraitDef t, TraitContext ctx)
        {
            if (t.NotPorted) return false;
            if (t.Role.HasValue && t.Role.Value != ctx.Build.Role) return false;
            if (t.NeedsDrill && ctx.Build.RoleDigMul <= 0) return false;
            if (t.Lock != null && !Unlocked.Contains(t.Lock)) return false;
            return true;
        }

        /// <summary>원본 infRollTier.</summary>
        int RollTier(List<TraitDef> pool, bool forceHigh, int level)
        {
            var available = new List<int>();
            for (int t = 1; t <= 4; t++) if (pool.Exists(x => x.Tier == t)) available.Add(t);
            if (available.Count == 0) return 1;
            if (forceHigh)
            {
                var hi = available.FindAll(t => t >= 3);
                if (hi.Count > 0) return hi[_rng.NextDouble() < .86 ? 0 : Math.Min(1, hi.Count - 1)];
            }
            double p = Math.Min(1.4, level / (double)XpGate.TargetLevel);
            double W(int t) => t == 1 ? Math.Max(24, 62 - 34 * p) : t == 2 ? 28 : t == 3 ? 8 + 16 * p : 2 + 8 * p;
            double total = 0; foreach (int t in available) total += W(t);
            double r = _rng.NextDouble() * total;
            foreach (int t in available) { r -= W(t); if (r <= 0) return t; }
            return available[0];
        }

        /// <summary>원본 infPickTraits — 3장. 풀이 비면 빈 배열(예비 보급은 호출자가).</summary>
        public TraitDef[] Roll(TraitContext ctx, int level)
        {
            var pool = new List<TraitDef>();
            foreach (var t in All) if (RoleOk(t, ctx) && t.Ok(ctx)) pool.Add(t);
            var out_ = new List<TraitDef>();
            bool forceHigh = Pity >= 4 || level % 5 == 0;
            for (int i = 0; i < 3 && pool.Count > 0; i++)
            {
                int tier = RollTier(pool, forceHigh && i == 0, level);
                var same = pool.FindAll(t => t.Tier == tier);
                var pick = same.Count > 0 ? same[(int)(_rng.NextDouble() * same.Count)] : pool[0];
                out_.Add(pick);
                pool.Remove(pick);
            }
            int best = 1; foreach (var t in out_) best = Math.Max(best, t.Tier);
            Pity = best >= 3 ? 0 : Pity + 1;
            return out_.ToArray();
        }

        /// <summary>레벨업 제시. 풀 소진이면 false (호출자가 예비 보급).</summary>
        public bool OpenLevel(TraitContext ctx, int level)
        {
            var cards = Roll(ctx, level);
            if (cards.Length == 0) { Offer = null; return false; }
            Offer = cards; OfferIsLegend = false;
            Offered?.Invoke(new TraitOfferEvent { Cards = cards, Level = level, Legend = false });
            return true;
        }

        /// <summary>휴식 화면 — 전설 3택 (원본 infBossShowRestUI).</summary>
        public void OpenLegends(int level)
        {
            var list = new List<TraitDef>(Legends);
            for (int i = list.Count - 1; i > 0; i--) { int j = (int)(_rng.NextDouble() * (i + 1)); (list[i], list[j]) = (list[j], list[i]); }
            Offer = list.GetRange(0, Math.Min(3, list.Count)).ToArray(); OfferIsLegend = true;
            Offered?.Invoke(new TraitOfferEvent { Cards = Offer, Level = level, Legend = true });
        }

        public bool Reroll(TraitContext ctx, int level)
        {
            if (!HasOffer || OfferIsLegend || Rerolls <= 0) return false;
            Rerolls--;
            return OpenLevel(ctx, level);
        }

        /// <summary>원본 infApplyTraitChoice.</summary>
        public bool Pick(int index, TraitContext ctx)
        {
            if (!HasOffer || index < 0 || index >= Offer.Length) return false;
            var t = Offer[index];
            Offer = null;
            Stacks[t.Id] = StackOf(t.Id) + 1;
            PickLog.Add(t);
            t.Apply(ctx);
            Picked?.Invoke(new TraitPickedEvent { Card = t, Stack = Stacks[t.Id] });
            return true;
        }

        // ═════════════════════════════ 카드 표 — 원본 INF_TRAITS 를 한 줄씩 옮김
        static TraitDef R(RoleId role, string kind, string id, int tier, string n, string d, Func<TraitContext, bool> ok, Action<TraitContext> a, string lock_ = null)
            => new TraitDef { Role = role, Kind = kind, Id = id, Tier = tier, Name = n, Desc = d, Ok = ok, Apply = a, Lock = lock_ };
        static TraitDef U(string kind, string id, int tier, string n, string d, Action<TraitContext> a, bool drill = false, bool notPorted = false)
            => new TraitDef { Role = null, Kind = kind, Id = id, Tier = tier, Name = n, Desc = d, NeedsDrill = drill, NotPorted = notPorted,
                              Ok = c => !FxHas(c, id), Apply = c => { FxAdd(c, id); a(c); } };

        // Fx 는 덱 인스턴스에 있지만 표는 static — ctx 를 통해 접근하기 위해 Build 에 플래그 집합을 둔다
        static bool FxHas(TraitContext c, string id) => c.Build.TraitFx.Contains(id);
        static void FxAdd(TraitContext c, string id) => c.Build.TraitFx.Add(id);

        public static readonly TraitDef[] All =
        {
            // ── 드릴러
            R(RoleId.Driller, "고압 드릴", "d_motor", 1, "토크 증폭 모터", "일반 채굴과 기반암 균열 속도 +18%", c => true, c => c.Build.DrillMul *= 1.18),
            R(RoleId.Driller, "균열 유지", "d_resin", 1, "균열 고착 수지", "균열 유지 유예 +1.25초 · 소실 속도 -22%", c => c.T.DrillerCrackDecay > .002, c => { c.T.DrillerCrackHold += 1.25; c.T.DrillerCrackDecay *= .78; }),
            R(RoleId.Driller, "기반암", "d_fault", 2, "단층 추적 비트", "기반암과 코어 암반 균열 속도 +25%", c => c.T.DrillerFoundationMul < 2.5, c => c.T.DrillerFoundationMul *= 1.25),
            R(RoleId.Driller, "돌파 파기", "d_pressure", 2, "과압 실린더", "Q의 균열 압력 +25% · 재사용 대기시간 -15%", c => c.T.DrillerQMul < 2, c => { c.T.DrillerQMul *= 1.25; c.T.DrillerQCdMul *= .85; }),
            R(RoleId.Driller, "지층 붕괴", "d_seismic", 3, "지진 공진축", "기반암 돌파 시 주변 일반 벽에 파쇄 충격파", c => c.T.DrillerShockRadius < 2, c => { c.T.DrillerShockRadius++; c.Build.DrillMul *= 1.15; }),
            R(RoleId.Driller, "균열 유지", "d_recirc", 3, "열회수 균열기", "기반암 균열 +20% · 균열 소실 속도 절반", c => c.T.DrillerCrackDecay > .001, c => { c.T.DrillerFoundationMul *= 1.2; c.T.DrillerCrackDecay *= .5; }),
            R(RoleId.Driller, "코어 암반", "d_mantle", 4, "맨틀 천공 키", "코어 암반 필요 압력 -19% · 모든 기반암 균열 +12.5%", c => c.T.DrillerCoreNeedMul > .66, c => { c.T.DrillerCoreNeedMul *= .81; c.T.DrillerFoundationMul *= 1.125; }),
            R(RoleId.Driller, "기반암", "d_eternal", 4, "장기 단층 각인", "균열 유지시간 2배 · 소실 속도 -50% · 균열 속도 +20%", c => !FxHas(c, "d_eternal"), c => { FxAdd(c, "d_eternal"); c.T.DrillerCrackDecay *= .5; c.T.DrillerCrackHold *= 2; c.T.DrillerFoundationMul *= 1.2; }),

            // ── 거너
            R(RoleId.Gunner, "파쇄 발사기", "g_cycler", 1, "고속 약실 순환기", "파쇄탄 재사용 대기시간 -14%", c => c.T.BreakerMaxCd > 5, c => c.T.BreakerMaxCd = Math.Max(5, c.T.BreakerMaxCd * .86)),
            R(RoleId.Gunner, "성형 작약", "g_liner", 1, "텅스텐 라이너", "파쇄탄의 벽 피해 +20%", c => c.T.BreakerDamageMul < 3, c => c.T.BreakerDamageMul *= 1.2),
            R(RoleId.Gunner, "신관", "g_fuse", 2, "가변 지연 신관", "자동 기폭 시간 -28% · 부착 즉시 조기 기폭 가능", c => c.T.BreakerFuse > .72, c => c.T.BreakerFuse = Math.Max(.7, c.T.BreakerFuse * .72)),
            R(RoleId.Gunner, "폭파 범위", "g_radius", 2, "파쇄 확장 슬리브", "파쇄 반경 +1칸 · 외곽 벽 피해 보정", c => c.T.BreakerRadius < 2, c => c.T.BreakerRadius = 2),
            R(RoleId.Gunner, "조기 기폭", "g_remote", 3, "원격 과충전 기폭기", "E 조기 기폭 시 파쇄 위력 +75%", c => c.T.BreakerEarlyMul < 1.7, c => c.T.BreakerEarlyMul = 1.75),
            R(RoleId.Gunner, "대인 파쇄", "g_hunter", 3, "대인 파편 재킷", "파쇄탄의 적 피해 +65% · 중화기 피해 +12%", c => c.T.BreakerEnemyMul < 1.6, c => { c.T.BreakerEnemyMul = 1.65; c.Build.GunMul *= 1.12; }),
            R(RoleId.Gunner, "광역 폭파", "g_cluster", 4, "군집 파쇄 작약", "파쇄 반경 2칸 · 벽 피해 +17.5%", c => c.T.BreakerRadius < 2, c => { c.T.BreakerRadius = 2; c.T.BreakerDamageMul *= 1.175; }),
            R(RoleId.Gunner, "파쇄 발사기", "g_zero", 4, "무정지 발사 사이클", "파쇄탄 재사용 대기시간 -22.5% · 벽 피해 +12.5%", c => c.T.BreakerMaxCd > 7.3, c => { c.T.BreakerMaxCd = Math.Max(7.2, c.T.BreakerMaxCd * .775); c.T.BreakerDamageMul *= 1.125; }),

            // ── 스카우트
            R(RoleId.Scout, "장거리 플레어", "s_flare", 1, "고광도 연소제", "플레어 조명 반경과 지속시간 +18%", c => c.T.ScoutFlareRadMul < 2.2, c => { c.T.ScoutFlareRadMul *= 1.18; c.T.ScoutFlareLifeMul *= 1.18; }),
            R(RoleId.Scout, "그래플 훅", "s_cable", 1, "경량 인장 케이블", "그래플 최대 거리 +1칸 · 이동 속도 +5%", c => c.T.ScoutGrappleRange < 9, c => { c.T.ScoutGrappleRange++; c.Build.MoveMul *= 1.05; }),
            R(RoleId.Scout, "그래플 훅", "s_winch", 2, "회생 제동 윈치", "그래플 재사용 대기시간 -28%", c => c.T.ScoutGrappleCdMul > .5, c => c.T.ScoutGrappleCdMul *= .72),
            R(RoleId.Scout, "정찰 펄스", "s_spectrum", 2, "다중 스펙트럼 스캐너", "정찰 반경 +1칸 · 신규 구역 탐사 경험치 +1", c => c.T.ScoutPulseRadius < 8, c => { c.T.ScoutPulseRadius++; c.T.ScoutExploreXp++; }),
            R(RoleId.Scout, "독립 시야", "s_watch", 3, "감시자 광학계", "플레어 독립 시야 +2칸 · 정찰 표식 지속 +1.2초", c => c.T.ScoutVisionBonus < 4, c => { c.T.ScoutVisionBonus += 2; c.T.ScoutPulseMax += 1.2; }),
            R(RoleId.Scout, "구역 탐사", "s_relay", 3, "원격 측량 릴레이", "플레어 지속시간 +35% · 탐사 경험치 +2", c => c.T.ScoutExploreXp < 8, c => { c.T.ScoutFlareLifeMul *= 1.35; c.T.ScoutExploreXp += 2; }),
            R(RoleId.Scout, "그래플 훅", "s_horizon", 4, "수평선 견인 장치", "그래플 최대 거리 7칸 · 재사용 대기시간 -20% · 이동 +6%", c => c.T.ScoutGrappleRange < 7 || c.T.ScoutGrappleCdMul > .81, c => { c.T.ScoutGrappleRange = 7; c.T.ScoutGrappleCdMul *= .8; c.Build.MoveMul *= 1.06; }),
            R(RoleId.Scout, "장거리 플레어", "s_sun", 4, "인공 태양 플레어", "플레어 반경·지속 +25% · 독립 시야 +1.5칸 · 정찰 반경 6.5칸", c => c.T.ScoutVisionBonus < 3.1, c => { c.T.ScoutFlareRadMul *= 1.25; c.T.ScoutFlareLifeMul *= 1.25; c.T.ScoutVisionBonus += 1.5; c.T.ScoutPulseRadius = Math.Max(6.5, c.T.ScoutPulseRadius); }),

            // ── 엔지니어
            R(RoleId.Engineer, "전력 노드", "e_cap", 1, "고밀도 축전지", "전력 노드 지속시간 +25% · 공급 반경 +0.5칸", c => c.T.EngineerNodeRadius < 7, c => { c.T.EngineerNodeLife *= 1.25; c.T.EngineerNodeRadius += .5; }),
            R(RoleId.Engineer, "센트리", "e_mag", 1, "확장 탄약 호퍼", "새 센트리 탄창 +6 · 설치된 센트리도 즉시 보급", c => c.T.EngineerTurretMag < 42, c => { c.T.EngineerTurretMag += 6; if (c.Roles != null) foreach (var t in c.Roles.Turrets) { t.Mag += 6; t.Ammo += 6; } }),
            R(RoleId.Engineer, "센트리", "e_cooling", 2, "능동 냉각 재킷", "센트리 발사 간격 -22%", c => c.T.EngineerTurretInterval > .16, c => c.T.EngineerTurretInterval *= .78),
            R(RoleId.Engineer, "전력망", "e_grid", 2, "분산 전력 프로토콜", "전력 노드 최대 +1 · 공급 반경 +0.75칸", c => c.T.EngineerMaxNodes < 4, c => { c.T.EngineerMaxNodes++; c.T.EngineerNodeRadius += .75; }),
            R(RoleId.Engineer, "설치 한도", "e_twin", 3, "쌍중 센트리 설계", "센트리 최대 설치 수 +1 · 지속시간 +25%", c => c.T.EngineerMaxTurrets < 4, c => { c.T.EngineerMaxTurrets++; c.T.EngineerTurretLife *= 1.25; }),
            R(RoleId.Engineer, "중 센트리", "e_heavy", 3, "중량 레일 센트리", "센트리 피해 +45% · 사거리 +1칸 · 탄창 +6, 연사력 -11%", c => c.T.EngineerTurretPower < 1.4, c => { c.T.EngineerTurretPower *= 1.45; c.T.EngineerTurretRange++; c.T.EngineerTurretMag += 6; c.T.EngineerTurretInterval *= 1.12; }),
            R(RoleId.Engineer, "자율 전력", "e_auto", 4, "폐쇄형 자율 동력로", "센트리가 전력망 밖에서 절반 출력으로 가동 · 재장전 시간 -15%", c => !c.T.EngineerAutonomous, c => { c.T.EngineerAutonomous = true; c.T.EngineerTurretReload *= .85; }),
            R(RoleId.Engineer, "요새화", "e_fortress", 4, "심층 요새 네트워크", "센트리 최대 3, 공급 반경 5칸 · 센트리 탄창 +6·연사 +10%", c => c.T.EngineerNodeRadius < 5 || c.T.EngineerMaxTurrets < 3, c => { c.T.EngineerMaxTurrets = Math.Max(3, c.T.EngineerMaxTurrets); c.T.EngineerNodeRadius = Math.Max(5, c.T.EngineerNodeRadius); c.T.EngineerTurretMag += 6; c.T.EngineerTurretInterval *= .9; }),

            // ── 공용 1티어
            U("굴착 확장", "u_large_bit", 1, "대형 비트", "드릴이 좌우 벽까지 함께 깎습니다.", c => c.Build.DrillWidth = Math.Max(1, c.Build.DrillWidth), drill: true),
            U("집중 굴착", "u_pierce_bit", 1, "압쇄 비트", "같은 벽을 계속 깎으면 드릴 피해가 점점 강해집니다.", c => c.Build.FocusDrill = true, drill: true),
            U("파쇄", "u_shockwave", 1, "파쇄 충격파", "벽을 부수면 주변 벽에도 충격이 퍼집니다.", c => { c.Build.BreakShockRadius = Math.Max(1, c.Build.BreakShockRadius); c.Build.BreakShockPower = Math.Max(.16, c.Build.BreakShockPower); }),
            U("고속 회전", "u_fast_spin", 1, "고속 회전", "드릴이 더 빠르게 회전하고 벽을 빠르게 깎습니다.", c => c.Build.DrillMul *= 1.09, drill: true),
            U("자원 회수", "u_magnet", 1, "자원 흡입기", "떨어진 자원이 먼 거리에서도 빠르게 끌려옵니다.", c => { c.Build.LootMagnetMul *= 1.5; c.Build.LootPickupMul *= 1.175; }),
            U("굴진 이동", "u_drive", 1, "굴진 가속", "드릴을 사용하는 동안 이동이 빨라집니다.", c => c.Build.DrillMoveMul *= 1.14),

            // ── 공용 2티어
            U("굴착 확장", "u_triple", 2, "삼중 드릴", "정면과 양옆의 벽을 동시에 깎습니다.", c => c.Build.DrillWidth = Math.Max(2, c.Build.DrillWidth), drill: true),
            U("자동 굴착", "u_aux", 2, "보조 드릴", "작은 드릴이 가까운 벽을 자동으로 깎습니다.", c => { }, notPorted: true),
            U("폭발 굴착", "u_explosive", 2, "폭발 드릴", "벽을 부술 때마다 작은 굴착 폭발이 발생합니다.", c => { c.Build.BreakShockRadius = Math.Max(1.55, c.Build.BreakShockRadius); c.Build.BreakShockPower = Math.Max(.31, c.Build.BreakShockPower); }),
            U("반복 굴착", "u_afterimage", 2, "잔상 드릴", "벽을 깎은 자리를 잠시 후 한 번 더 파냅니다.", c => { }, notPorted: true),
            U("파편 굴착", "u_shards", 2, "파편 탄환", "벽을 부수면 굴착 파편이 사방으로 날아갑니다.", c => c.Build.ShardBurst = Math.Max(6, c.Build.ShardBurst)),
            U("굴착 방어", "u_guard", 2, "굴착 보호막", "벽을 부술 때마다 짧은 보호막을 얻습니다.", c => c.Build.BreakShield = Math.Max(.225, c.Build.BreakShield)),

            // ── 공용 3티어
            U("연쇄 파괴", "u_chain", 3, "연쇄 붕괴", "폭발로 부서진 벽에서도 새로운 폭발이 일어납니다.", c => c.Build.ChainCollapse = true),
            U("자동 굴착", "u_satellite", 3, "드릴 위성", "두 개의 드릴이 주위를 돌며 벽을 자동으로 깎습니다.", c => { }, notPorted: true),
            U("관통 굴착", "u_laser", 3, "파쇄 레이저", "드릴이 굵은 관통 광선으로 뒤쪽 벽까지 깎습니다.", c => { c.Build.DrillPenetration = Math.Max(4, c.Build.DrillPenetration); c.Build.DrillWidth = Math.Max(1, c.Build.DrillWidth); }, drill: true),
            U("자동 포격", "u_auto_shell", 3, "자동 굴착탄", "벽을 연속으로 부수면 굴착탄이 사방으로 발사됩니다.", c => c.Build.AutoDigEvery = 8),
            U("광역 파쇄", "u_vortex", 3, "붕괴 소용돌이", "벽을 부수면 주변 자원과 벽을 끌어당기는 폭발이 생깁니다.", c => { }, notPorted: true),
            R(RoleId.Driller, "돌파 파기", "u_wide_q", 3, "광역 천공", "Q가 지나가는 넓은 통로를 한꺼번에 뚫습니다.", c => !c.T.DrillerWideQ, c => c.T.DrillerWideQ = true),

            // ── 공용 4티어 (대부분 서브시스템 — 이식 보류)
            U("자동 굴착", "u_army", 4, "무한 굴착 군단", "여러 자동 드릴이 벽을 파괴하지만 중량 때문에 이동 속도가 10% 감소합니다.", c => c.Build.MoveMul *= .9, notPorted: true),
            U("초대형 굴착", "u_planet_breaker", 4, "행성 파쇄기", "거대 드릴이 화면을 쓸어내지만 발동 충격으로 최대 HP의 1.5%를 잃습니다.", c => { }, notPorted: true),
            U("대붕괴", "u_grand_collapse", 4, "대붕괴", "넓은 지역을 무너뜨리지만 발동 충격으로 최대 HP의 2.5%를 잃습니다.", c => { }, notPorted: true),
            U("초대형 굴착", "u_giant_bit", 4, "초거대 비트", "2칸 폭·1칸 깊이로 갈아버리지만 드릴 열이 25% 빠르게 쌓입니다.", c => { c.Build.DrillWidth = Math.Max(2, c.Build.DrillWidth); c.Build.DrillPenetration = Math.Max(1, c.Build.DrillPenetration); c.Build.DrillReach = Math.Max(1.105, c.Build.DrillReach); c.Build.HeatBuildMul *= 1.25; }, drill: true),
            U("드릴 폭풍", "u_storm", 4, "드릴 폭풍", "회전 드릴들이 벽을 깎지만 제어 부담으로 이동 속도가 12% 감소합니다.", c => c.Build.MoveMul *= .88, notPorted: true),
            U("무정지 과급", "u_endless", 4, "무정지 과급", "과열 중 멈추지 않지만 출력이 50%가 되고 HP가 계속 감소합니다.", c => c.Build.EndlessOverdrive = true, drill: true),

            // ── 영구 노드 해금 계열 (lock)
            R(RoleId.Driller, "심층 시추", "d_deep_head", 3, "심층 시추 헤드", "코어 암반 필요 압력 -15% · 기반암 균열 +15%", c => c.T.DrillerCoreNeedMul > .6, c => { c.T.DrillerCoreNeedMul *= .85; c.T.DrillerFoundationMul *= 1.15; }, "deep"),
            R(RoleId.Driller, "심층 시추", "d_deep_pillar", 4, "지주 붕괴", "기반암 돌파 충격파 반경 +1 · 드릴 위력 +15%", c => c.T.DrillerShockRadius < 3, c => { c.T.DrillerShockRadius++; c.Build.DrillMul *= 1.15; }, "deep"),
            R(RoleId.Driller, "기반암 공학", "d_bed_mantle", 3, "맨틀 시추 헤드", "코어 암반 필요 압력 -18%", c => c.T.DrillerCoreNeedMul > .55, c => c.T.DrillerCoreNeedMul *= .82, "bedrock"),
            R(RoleId.Driller, "기반암 공학", "d_bed_quake", 4, "지각 붕괴", "기반암 돌파 충격파 반경 +2 · 굴착 +10%", c => c.T.DrillerShockRadius < 4, c => { c.T.DrillerShockRadius += 2; c.Build.DrillMul *= 1.10; }, "bedrock"),
            R(RoleId.Gunner, "파편 운용", "g_lock_flechette", 3, "비산 탄자", "벽을 부수면 파편이 분출되고 파쇄탄 벽 피해 +15%", c => c.Build.ShardBurst < 10, c => { c.Build.ShardBurst = Math.Max(8, c.Build.ShardBurst); c.T.BreakerDamageMul *= 1.15; }, "shrapnel"),
            R(RoleId.Gunner, "파편 운용", "g_lock_saturation", 4, "포화 사격", "파쇄탄 반경 +1 · 재사용 대기시간 -18%", c => c.T.BreakerRadius < 3, c => { c.T.BreakerRadius++; c.T.BreakerMaxCd = Math.Max(5, c.T.BreakerMaxCd * .82); }, "shrapnel"),
            R(RoleId.Gunner, "포화 사격", "g_bar_volley", 3, "연속 포격", "파쇄탄 재사용 대기시간 -20%", c => c.T.BreakerMaxCd > 6, c => c.T.BreakerMaxCd = Math.Max(5, c.T.BreakerMaxCd * .8), "barrage"),
            R(RoleId.Gunner, "포화 사격", "g_bar_siege", 4, "공성 사격", "파쇄탄 반경 +1 · 벽 피해 +25%", c => c.T.BreakerRadius < 4, c => { c.T.BreakerRadius++; c.T.BreakerDamageMul *= 1.25; }, "barrage"),
            R(RoleId.Scout, "심층 정찰", "s_lock_echo", 3, "반향 측량", "정찰 반경 +1.5칸 · 탐사 경험치 +3", c => c.T.ScoutPulseRadius < 9, c => { c.T.ScoutPulseRadius += 1.5; c.T.ScoutExploreXp += 3; }, "recon"),
            R(RoleId.Scout, "심층 정찰", "s_lock_ghost", 4, "유령 보행", "이동 +14% · 그래플 재사용 -30%", c => c.T.ScoutGrappleCdMul > .4, c => { c.Build.MoveMul *= 1.14; c.T.ScoutGrappleCdMul *= .7; }, "recon"),
            R(RoleId.Scout, "개척 항로", "s_path_line", 3, "개척 항로", "그래플 사거리 +2칸 · 이동 +8%", c => c.T.ScoutGrappleRange < 12, c => { c.T.ScoutGrappleRange += 2; c.Build.MoveMul *= 1.08; }, "pathfind"),
            R(RoleId.Scout, "개척 항로", "s_path_beacon", 4, "항로 표지", "독립 시야 +2칸 · 정찰 반경 +2칸", c => c.T.ScoutVisionBonus < 5, c => { c.T.ScoutVisionBonus += 2; c.T.ScoutPulseRadius += 2; }, "pathfind"),
            R(RoleId.Engineer, "전력망 확장", "e_lock_relay", 3, "중계 전력망", "전력 노드 최대 +1 · 공급 반경 +1칸", c => c.T.EngineerMaxNodes < 5, c => { c.T.EngineerMaxNodes++; c.T.EngineerNodeRadius++; }, "grid"),
            R(RoleId.Engineer, "전력망 확장", "e_lock_arsenal", 4, "자동 병기고", "센트리 최대 +1 · 연사 +15%", c => c.T.EngineerMaxTurrets < 5, c => { c.T.EngineerMaxTurrets++; c.T.EngineerTurretInterval *= .85; }, "grid"),
            R(RoleId.Engineer, "요새 설계", "e_for_bastion", 3, "보루 설계", "센트리 지속 +40% · 탄창 +6", c => c.T.EngineerTurretLife < 120, c => { c.T.EngineerTurretLife *= 1.4; c.T.EngineerTurretMag += 6; }, "fortress"),
            R(RoleId.Engineer, "요새 설계", "e_for_citadel", 4, "요새 도시", "센트리 최대 +1 · 전력 노드 최대 +1", c => c.T.EngineerMaxTurrets < 6, c => { c.T.EngineerMaxTurrets++; c.T.EngineerMaxNodes++; }, "fortress"),
        };

        /// <summary>원본 INF_LEGENDS — 휴식 화면 전설 카드.</summary>
        public static readonly TraitDef[] Legends =
        {
            new TraitDef { Id = "l_dual", Tier = 4, Kind = "쌍동력", Name = "쌍동력 융합", Desc = "드릴·총기 피해 +17.5% · 재장전 시간 +12%.", Apply = c => { c.Build.DrillMul *= 1.175; c.Build.GunMul *= 1.175; c.Build.AdjustReload(1.12); } },
            new TraitDef { Id = "l_barrage", Tier = 4, Kind = "광역 폭파", Name = "파쇄 탄막", Desc = "폭발탄과 벽 피해 +30% · 재장전 시간 +18%.", Apply = c => { c.Build.Explosive = true; c.Build.GunWallMul *= 1.3; c.Build.AdjustReload(1.18); } },
            new TraitDef { Id = "l_feed", Tier = 4, Kind = "다중 급탄", Name = "다중 급탄", Desc = "투사체 +1, 연사력 +7.5%, 탄창 +2 · 재장전 +10%.", Apply = c => { c.Build.Shots = Math.Min(5, c.Build.Shots + 1); c.Build.FireRate *= 1.075; c.Build.SetMag(2); c.Build.AdjustReload(1.10); } },
            new TraitDef { Id = "l_ricochet", Tier = 4, Kind = "도탄", Name = "무한 도탄", Desc = "도탄 +1 · 재장전 시간 +12%.", Apply = c => { c.Build.Bounces += 1; c.Build.AdjustReload(1.12); } },
            new TraitDef { Id = "l_overdrive", Tier = 4, Kind = "굴진 과급", Name = "굴진 과급", Desc = "드릴 피해 +22.5%, 이동 속도 +6%.", Apply = c => { c.Build.DrillMul *= 1.225; c.Build.MoveMul *= 1.06; } },
            new TraitDef { Id = "l_survival", Tier = 4, Kind = "생존 강화", Name = "생존 격벽", Desc = "최대 체력 +20, 체력을 추가 회복한다.", Apply = c => { c.Player.HpMax += 20; c.Player.Hp = Math.Min(c.Player.HpMax, c.Player.Hp + 20); } },
        };
    }
}
