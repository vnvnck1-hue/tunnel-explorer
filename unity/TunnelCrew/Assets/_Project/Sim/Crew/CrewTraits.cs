using System;

namespace TunnelCrew.Sim
{
    /// <summary>AI 전용 특성 풀 — 원본 <c>AI_TRAITS</c> 30장. 본편 INF_TRAITS 의 미러로, 효과는 그 크루의 KIT 사본에만 적용된다.</summary>
    public sealed class CrewTraitDef
    {
        public RoleId Role; public string Id, Name, Desc; public int Tier;
        public Func<CrewMember, bool> Ok;
        public Action<CrewMember, AiCrewSystem> Apply;
    }

    public static class CrewTraits
    {
        static CrewTraitDef T(RoleId role, string id, int tier, string n, string d, Action<CrewMember, AiCrewSystem> a, Func<CrewMember, bool> ok = null)
            => new CrewTraitDef { Role = role, Id = id, Tier = tier, Name = n, Desc = d, Apply = a, Ok = ok };

        public static readonly CrewTraitDef[] All =
        {
            // ── 드릴러: 길을 연다
            T(RoleId.Driller, "d_motor", 1, "토크 증폭 모터", "굴착 +18%", (m, _) => m.Kit.DigMul *= 1.18),
            T(RoleId.Driller, "d_rivet", 1, "리벳 강화 약실", "사격 +22%", (m, _) => m.Kit.GunMul *= 1.22),
            T(RoleId.Driller, "d_frame", 1, "경량 프레임", "이동 +8% · 체력 +30", (m, _) => { m.Kit.MoveMul *= 1.08; m.HpMax += 30; m.Hp += 30; }),
            T(RoleId.Driller, "d_fault", 2, "단층 추적 비트", "굴착 +25% · 통로 개척 적극", (m, _) => { m.Kit.DigMul *= 1.25; m.Kit.PathDigCost *= .8; }),
            T(RoleId.Driller, "d_guard", 2, "작업 구역 방호", "체력 +55 · 근접 교전 허용", (m, _) => { m.HpMax += 55; m.Hp += 55; m.Kit.Engage = Math.Max(2.4, m.Kit.Engage - .4); }),
            T(RoleId.Driller, "d_seismic", 3, "지진 공진축", "굴착 +32% · 드릴 접촉 피해 증가", (m, _) => m.Kit.DigMul *= 1.32),
            T(RoleId.Driller, "d_mantle", 4, "맨틀 천공 키", "굴착 +45% · 이동 +10%", (m, _) => { m.Kit.DigMul *= 1.45; m.Kit.MoveMul *= 1.10; }),
            // ── 거너: 적을 지운다
            T(RoleId.Gunner, "g_cycler", 1, "고속 약실 순환기", "연사 +18%", (m, _) => m.Kit.FireCd *= .85),
            T(RoleId.Gunner, "g_belt", 1, "확장 급탄 벨트", "탄창 +6", (m, _) => m.Kit.Mag += 6, m => m.Kit.Mag < 40),
            T(RoleId.Gunner, "g_liner", 1, "텅스텐 라이너", "화력 +20%", (m, _) => m.Kit.GunMul *= 1.20),
            T(RoleId.Gunner, "g_optic", 2, "전술 조준경", "인지 +4칸 · 요격 +3칸", (m, _) => { m.Kit.Alert += 4; m.Kit.Intercept += 3; }),
            T(RoleId.Gunner, "g_radius", 2, "파쇄 확장 슬리브", "파쇄 반경 +1칸", (m, _) => m.Kit.BreakerRadius = 2, m => m.Kit.BreakerRadius < 2),
            T(RoleId.Gunner, "g_quick", 2, "속사 재장전", "재장전 -25%", (m, _) => m.Kit.Reload *= .75),
            T(RoleId.Gunner, "g_fuse", 3, "고속 신관", "파쇄탄 재사용 -30%", (m, _) => m.Kit.BreakerCd *= .7),
            T(RoleId.Gunner, "g_storm", 4, "지속 사격 교리", "연사 +20% · 화력 +25%", (m, _) => { m.Kit.FireCd *= .8; m.Kit.GunMul *= 1.25; }),
            // ── 스카우트: 어둠을 연다
            T(RoleId.Scout, "s_boots", 1, "경량 부츠", "이동 +14%", (m, _) => m.Kit.MoveMul *= 1.14),
            T(RoleId.Scout, "s_flare", 1, "플레어 증설", "플레어 주기 -30%", (m, _) => m.Kit.FlareCd *= .7),
            T(RoleId.Scout, "s_carbine", 1, "카빈 총열 개조", "사격 +20%", (m, _) => m.Kit.GunMul *= 1.20),
            T(RoleId.Scout, "s_optic", 2, "야간 광학", "인지 +5칸", (m, _) => m.Kit.Alert += 5),
            T(RoleId.Scout, "s_cutter", 2, "절삭기 출력 증폭", "굴착 +35%", (m, _) => { m.Kit.DigMul *= 1.35; m.Kit.PathDigCost *= .85; }),
            T(RoleId.Scout, "s_evade", 3, "회피 기동", "대시 +20% · 이동 +10%", (m, _) => { m.Kit.DashMul *= 1.2; m.Kit.MoveMul *= 1.10; }),
            T(RoleId.Scout, "s_beacon", 4, "지속 조명탄", "플레어 주기 -40% · 인지 +5칸", (m, _) => { m.Kit.FlareCd *= .6; m.Kit.Alert += 5; }),
            // ── 엔지니어: 공간을 만든다
            T(RoleId.Engineer, "e_mag", 1, "확장 탄약 호퍼", "센트리 탄창 +8", (m, ai) => { m.Kit.TurretMag += 8; foreach (var t in ai.Turrets) if (t.Owner == m) { t.Mag += 8; t.Ammo += 8; } }),
            T(RoleId.Engineer, "e_cutter", 1, "공학 커터 증폭", "굴착 +28%", (m, _) => m.Kit.DigMul *= 1.28),
            T(RoleId.Engineer, "e_grid", 1, "전력망 확장", "노드 반경 +1.2칸", (m, _) => m.Kit.NodeRadius += 1.2),
            T(RoleId.Engineer, "e_rate", 2, "센트리 사격 통제", "센트리 연사 +25% · 위력 +20%", (m, _) => { m.Kit.TurretRate *= .8; m.Kit.TurretPower *= 1.2; }),
            T(RoleId.Engineer, "e_fast", 2, "신속 설치 키트", "설치 대기 -30%", (m, _) => { m.Kit.TurretCd *= .7; m.Kit.NodeCd *= .7; }),
            T(RoleId.Engineer, "e_third", 3, "3번 슬롯 개방", "센트리 최대 +1기", (m, _) => m.Kit.MaxTurrets = 3, m => m.Kit.MaxTurrets < 3),
            T(RoleId.Engineer, "e_range", 3, "장거리 사통 장치", "센트리 사거리 +2칸 · 수명 +25", (m, _) => { m.Kit.TurretRange += 2; m.Kit.TurretLife += 25; }),
            T(RoleId.Engineer, "e_fortress", 4, "이동 요새 교리", "센트리 위력 +35% · 노드 반경 +1.5칸", (m, _) => { m.Kit.TurretPower *= 1.35; m.Kit.NodeRadius += 1.5; }),
        };

        /// <summary>풀이 마르면 성장이 멈추지 않게 기본 숙련으로 대체한다 (§17.4-6).</summary>
        public static readonly CrewTraitDef Basic = new CrewTraitDef
        {
            Id = "basic", Name = "숙련", Desc = "굴착·사격 +6% · 체력 +12",
            Apply = (m, _) => { m.Kit.DigMul *= 1.06; m.Kit.GunMul *= 1.06; m.HpMax += 12; m.Hp += 12; },
        };

        /// <summary>레벨이 오를수록 상위 티어가 열린다 — 사람의 카드 곡선과 같은 감각.</summary>
        public static int MaxTier(int level) => level < 3 ? 1 : level < 6 ? 2 : level < 9 ? 3 : 4;
    }
}
