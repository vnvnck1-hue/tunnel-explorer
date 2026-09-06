using System;
using System.Collections.Generic;

namespace TunnelCrew.Sim
{
    public enum RelicElem : byte { None = 0, Fire, Frost, Volt, Earth }

    public sealed class RelicDef
    {
        public string Id, Kind, Name, Desc;
        public int Tier;            // 1 일반 · 2 희귀 · 4 전설
        public RelicElem Elem;
    }

    /// <summary>
    /// 유물 33종 — 원본 <c>INF_RELICS</c>(14227) 와 소켓·원소 공명 규칙(<c>infRelicMeta · infRelicEquipAt · infRelicEquipAuto ·
    /// infRelicResonanceOf</c>). 효과 구현은 <c>RelicSystem</c>(M5 후속) 이 이 표의 Id 로 분기한다.
    /// 소켓 4 + 왕관(r_crown)이 앞 4칸에 있으면 5번째가 열린다. 왕관은 자기가 여는 소켓에 못 들어간다.
    /// </summary>
    public static class Relics
    {
        public const string Crown = "r_crown";

        static RelicDef R(string id, int tier, string kind, string n, string d, RelicElem elem = RelicElem.None)
            => new RelicDef { Id = id, Tier = tier, Kind = kind, Name = n, Desc = d, Elem = elem };

        public static readonly RelicDef[] All =
        {
            // ── 일반 (비원소)
            R("r_fang", 1, "치명타", "사냥꾼의 송곳니", "공격이 10% 확률로 치명타가 되어 2배 피해를 입힙니다."),
            R("r_venom", 1, "맹독", "맹독샘", "명중 시 35% 확률로 적을 3초간 중독시킵니다 (초당 피해)."),
            R("r_leech", 1, "흡혈", "흡혈 부적", "적을 처치할 때마다 HP를 3 회복합니다."),
            R("r_lodestone", 1, "자철", "자철석 심장", "광맥에서 얻는 암반 코어가 20% 증가합니다."),
            R("r_watch", 1, "회피", "낡은 회중시계", "피격 후 무적 시간이 60% 길어집니다."),
            R("r_lunchbox", 1, "보급", "굴착단 도시락", "지층을 내려갈 때마다 HP를 12% 회복합니다."),
            R("r_detector", 1, "탐지", "고물 탐지기", "유물 발굴 확률이 2배가 됩니다."),
            // ── 희귀 (비원소)
            R("r_executioner", 2, "처형", "처형인의 인장", "HP 12% 이하의 일반 적을 즉시 처형합니다."),
            R("r_gel", 2, "방어", "충격 완화 젤", "받는 피해가 15% 감소합니다."),
            R("r_unstable", 2, "유폭", "불안정한 심장", "적이 죽을 때 폭발해 주변 적과 벽에 피해를 입힙니다."),
            R("r_banner", 2, "배수진", "배수진 깃발", "HP 30% 이하일 때 모든 피해 +40%, 이동 속도 +10%."),
            R("r_strata", 2, "누적", "지층의 기억", "지층을 내려갈 때마다 모든 피해 +4% (런 동안 누적)."),
            R("r_smuggler", 2, "밀수", "밀수꾼의 주머니", "쓰러져도 코어 보존율 +25%p (영구 노드와 별도 가산)."),
            // ── 화염
            R("r_ember", 1, "화염", "불씨 주머니", "명중 시 20% 확률로 화상 — 3초간 초당 피해를 입힙니다.", RelicElem.Fire),
            R("r_magma", 2, "화염", "용암 혈관", "드릴 과열 게이지가 높을수록 모든 피해 증가 (최대 +30%).", RelicElem.Fire),
            R("r_wildfire", 2, "화염", "들불", "화상 상태의 적이 죽으면 가장 가까운 적에게 화염이 옮겨붙습니다.", RelicElem.Fire),
            // ── 빙결
            R("r_frost", 1, "빙결", "서리 파편", "명중한 적의 이동 속도를 2초간 30% 감소시킵니다.", RelicElem.Frost),
            R("r_permafrost", 2, "빙결", "영구동토 심장", "슬로우 상태의 적에게 주는 피해 +25%.", RelicElem.Frost),
            R("r_flashfreeze", 2, "빙결", "급속 냉동", "같은 적에게 슬로우 5회 적중 시 2초간 빙결시킵니다.", RelicElem.Frost),
            // ── 뇌전
            R("r_coil", 1, "뇌전", "정전기 코일", "명중 시 25% 확률로 감전 — 주변 적 1체에 전이 피해.", RelicElem.Volt),
            R("r_rod", 2, "뇌전", "피뢰침", "피격 시 주변 모든 적을 감전시키고 1초 경직 (8초마다).", RelicElem.Volt),
            R("r_overload", 2, "뇌전", "과부하 회로", "재장전을 마치면 다음 6발이 감전탄이 됩니다.", RelicElem.Volt),
            // ── 대지
            R("r_resonstone", 1, "대지", "굴착 공명석", "벽 파괴 시 15% 확률로 주변 적을 밀쳐내고 파편 피해를 입힙니다.", RelicElem.Earth),
            R("r_stoneskin", 2, "대지", "암반 피부", "1초 이상 제자리에 있으면 받는 피해 -30% (움직이면 해제).", RelicElem.Earth),
            R("r_avalanche", 2, "대지", "사태 유발자", "넉백된 적이 벽에 충돌하면 추가 피해와 2초 경직을 입습니다.", RelicElem.Earth),
            // ── 전설
            R("r_ram", 4, "공성", "파성퇴 코어", "보스 장갑에 주는 피해 4배 · 보스가 소환한 벽은 1타에 부서집니다."),
            R("r_guardian", 4, "소환", "태엽 수호자", "고대 드론이 따라다니며 자동으로 사격합니다."),
            R("r_abyss", 4, "저주", "심연의 계약", "모든 피해 +50% — 대신 최대 HP가 30% 감소합니다."),
            R(Crown, 4, "왕관", "도굴왕의 왕관", "유물 소켓이 1개 늘어납니다 (총 5개)."),
            R("r_phoenix", 4, "부활", "불사조 깃털", "치명상을 입으면 사망 대신 HP 40% 회복 + 4초 무적 (런당 1회)."),
            R("r_reactor", 4, "융합", "원소 융합로", "장착 중인 모든 원소의 공명 단계가 1 오릅니다."),
            R("r_bloodpact", 4, "저주", "피의 계약", "적 처치 시 HP 6 회복 — 대신 HP가 초당 조금씩 마릅니다."),
            R("r_hourglass", 4, "시간", "정지된 모래시계", "피격 시 3초간 시간이 정지합니다 (30초마다)."),
        };

        static readonly Dictionary<string, RelicDef> _byId = new Dictionary<string, RelicDef>();
        static Relics() { foreach (var r in All) _byId[r.Id] = r; }
        public static RelicDef ById(string id) => id != null && _byId.TryGetValue(id, out var r) ? r : null;

        // ── 소켓 (MetaState.relicSockets 조작)
        public static int SocketMax(MetaState m)
        {
            for (int i = 0; i < 4; i++) if (m.relicSockets[i] == Crown) return 5;
            return 4;
        }

        public static List<string> EquippedIds(MetaState m)
        {
            var list = new List<string>(); int max = SocketMax(m);
            for (int i = 0; i < max; i++) if (!string.IsNullOrEmpty(m.relicSockets[i])) list.Add(m.relicSockets[i]);
            return list;
        }

        public static bool EquipAt(MetaState m, string id, int slot)
        {
            int max = SocketMax(m);
            if (ById(id) == null || !m.relicOwned.Contains(id) || slot < 0 || slot >= max) return false;
            if (id == Crown && slot == 4) return false;
            int prev = Array.IndexOf(m.relicSockets, id); if (prev >= 0) m.relicSockets[prev] = null;
            m.relicSockets[slot] = id; m.relicAge[slot] = ++m.relicSeq;
            m.Sanitize();
            return true;
        }

        /// <summary>원본 infRelicEquipAuto — 장착 중이면 해제, 빈 칸이 없으면 가장 오래된 칸을 교체.</summary>
        public static bool EquipAuto(MetaState m, string id)
        {
            int cur = Array.IndexOf(m.relicSockets, id);
            if (cur >= 0) return Unequip(m, cur);
            int max = SocketMax(m), limit = id == Crown ? 4 : max;
            for (int i = 0; i < limit; i++) if (string.IsNullOrEmpty(m.relicSockets[i])) return EquipAt(m, id, i);
            int oldest = -1, best = int.MaxValue;
            for (int i = 0; i < limit; i++) if (m.relicAge[i] < best) { best = m.relicAge[i]; oldest = i; }
            return oldest >= 0 && EquipAt(m, id, oldest);
        }

        public static bool Unequip(MetaState m, int slot)
        {
            if (slot < 0 || slot >= 5 || string.IsNullOrEmpty(m.relicSockets[slot])) return false;
            m.relicSockets[slot] = null; m.Sanitize();
            return true;
        }

        /// <summary>원본 infRelicGrant — 보관고에 추가 (중복 없음). true = 새 유물.</summary>
        public static bool Grant(MetaState m, string id)
        {
            if (ById(id) == null || m.relicOwned.Contains(id)) return false;
            m.relicOwned.Add(id); return true;
        }

        /// <summary>원본 infRelicResonanceOf — 원소별 0~2 단계 (2개 → 1, 3개 → 2, 융합로 +1).</summary>
        public static Dictionary<RelicElem, int> Resonance(IList<string> ids)
        {
            var count = new Dictionary<RelicElem, int> { [RelicElem.Fire] = 0, [RelicElem.Frost] = 0, [RelicElem.Volt] = 0, [RelicElem.Earth] = 0 };
            foreach (var id in ids) { var r = ById(id); if (r != null && r.Elem != RelicElem.None) count[r.Elem]++; }
            int boost = ids.Contains("r_reactor") ? 1 : 0;
            var lv = new Dictionary<RelicElem, int>();
            foreach (var kv in count) lv[kv.Key] = kv.Value > 0 ? Math.Min(2, (kv.Value >= 3 ? 2 : kv.Value >= 2 ? 1 : 0) + boost) : 0;
            return lv;
        }
    }
}
