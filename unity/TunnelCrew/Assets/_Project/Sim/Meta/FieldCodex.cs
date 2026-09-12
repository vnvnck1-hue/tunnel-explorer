using System;
using System.Collections.Generic;

namespace TunnelCrew.Sim
{
    public enum CodexCategory : byte { Creature = 0, Geology, Equipment, Expedition }

    public readonly struct CodexEntryDef
    {
        public readonly string Id, Name, Description;
        public readonly CodexCategory Category;
        public CodexEntryDef(string id, CodexCategory category, string name, string description)
        { Id = id; Category = category; Name = name; Description = description; }
    }

    public readonly struct CodexDiscoveredEvent
    {
        public readonly CodexEntryDef Entry;
        public CodexDiscoveredEvent(CodexEntryDef entry) { Entry = entry; }
    }

    /// <summary>필드 도감의 안정적인 ID 카탈로그. 저장은 ID만 하므로 설명을 고쳐도 세이브가 깨지지 않는다.</summary>
    public static class FieldCodex
    {
        public static readonly CodexEntryDef[] All =
        {
            new CodexEntryDef("creature.crawler.seen", CodexCategory.Creature, "굴착충 흔적", "돌가루를 밀어내며 접근하는 소형 군체 생물을 조우했다."),
            new CodexEntryDef("creature.crawler.kill", CodexCategory.Creature, "굴착충", "짧은 도약 뒤 빈틈이 생긴다. 측면 이동으로 공격을 흘릴 수 있다."),
            new CodexEntryDef("creature.crawler.veteran", CodexCategory.Creature, "굴착충 생태", "10개체 관찰: 진동이 끊기면 무리를 재정렬하므로 간헐 사격도 유효하다."),
            new CodexEntryDef("creature.spitter.seen", CodexCategory.Creature, "산란포자 흔적", "원거리에서 산성 포자를 준비하는 개체를 조우했다."),
            new CodexEntryDef("creature.spitter.kill", CodexCategory.Creature, "산란포자", "시야가 트인 곳에서 포격한다. 벽과 굴곡을 엄폐로 쓰면 안전하다."),
            new CodexEntryDef("creature.spitter.veteran", CodexCategory.Creature, "산란포자 생태", "10개체 관찰: 사격선이 막히면 측면으로 이동해 다시 각도를 만든다."),
            new CodexEntryDef("creature.broodbeast.seen", CodexCategory.Creature, "군체수 흔적", "두꺼운 갑각과 큰 체구를 가진 군체 생물을 조우했다."),
            new CodexEntryDef("creature.broodbeast.kill", CodexCategory.Creature, "군체수", "정면 압박은 강하지만 좁은 통로와 관통탄에 취약하다."),
            new CodexEntryDef("creature.broodbeast.veteran", CodexCategory.Creature, "군체수 생태", "10개체 관찰: 큰 몸집 때문에 일렬 통로에서 후속 개체의 진입을 막는다."),
            new CodexEntryDef("creature.apex.kill", CodexCategory.Creature, "광란종", "고위협 지층에서 변이한 개체. 일반종보다 넉백 저항이 높다."),
            new CodexEntryDef("creature.boss.kill", CodexCategory.Creature, "암반 수호자", "지층 목표를 달성한 침입자에게 반응하는 거대 군체 개체다."),

            new CodexEntryDef("geology.dirt", CodexCategory.Geology, "퇴적토", "빠르게 굴착되는 연질층. 펄프를 소량 품는다."),
            new CodexEntryDef("geology.stone", CodexCategory.Geology, "압착석", "단단하지만 펄프 함량이 높은 기본 암반."),
            new CodexEntryDef("geology.ore", CodexCategory.Geology, "블룸 광맥", "장비 제작에 쓰이는 블룸 결정이 섞인 광맥."),
            new CodexEntryDef("geology.gem", CodexCategory.Geology, "고밀도 보석층", "블룸이 응축된 희귀 지질. 짧은 우회 가치가 충분하다."),
            new CodexEntryDef("geology.crys", CodexCategory.Geology, "공명 결정", "주변 진동에 반응해 빛나는 결정. 공명 코어 임무의 표식으로도 쓰인다."),
            new CodexEntryDef("geology.core", CodexCategory.Geology, "기반암 핵", "일반 드릴로 파괴할 수 없는 지층 골격. 균열 기술이 필요하다."),

            new CodexEntryDef("equipment.driller.standard", CodexCategory.Equipment, "고압 중형 드릴", "안정적인 열 관리와 정밀 굴착을 제공하는 드릴러 표준 장비."),
            new CodexEntryDef("equipment.driller.alternative", CodexCategory.Equipment, "충격 코어 드릴", "굴착 폭을 넓히는 대신 발열과 단일 지점 효율을 감수한다."),
            new CodexEntryDef("equipment.gunner.standard", CodexCategory.Equipment, "벨트식 중화기", "긴 교전에서 꾸준한 화력을 유지하는 거너 표준 장비."),
            new CodexEntryDef("equipment.gunner.alternative", CodexCategory.Equipment, "파쇄 산탄총", "짧은 사거리 안에서 다섯 발의 파편을 집중한다."),
            new CodexEntryDef("equipment.scout.standard", CodexCategory.Equipment, "정찰 카빈", "이동 중에도 다루기 쉬운 균형형 카빈."),
            new CodexEntryDef("equipment.scout.alternative", CodexCategory.Equipment, "레일 카빈", "느린 고속탄 한 발이 적과 얇은 벽을 연속 관통한다."),
            new CodexEntryDef("equipment.engineer.standard", CodexCategory.Equipment, "서비스 총기", "설치물 운용을 방해하지 않는 공학용 보조 화기."),
            new CodexEntryDef("equipment.engineer.alternative", CodexCategory.Equipment, "코일 리피터", "전력장 안에서 증폭되고 벽에서 한 번 도탄하는 펄스 화기."),

            new CodexEntryDef("record.objective.breach", CodexCategory.Expedition, "심층 돌파 완료", "자유 굴착으로 지층 장악 목표를 달성했다."),
            new CodexEntryDef("record.objective.core_recovery", CodexCategory.Expedition, "공명 코어 회수 완료", "표시된 공명 광맥 세 곳을 회수했다."),
            new CodexEntryDef("record.objective.survey", CodexCategory.Expedition, "지질 측량 완료", "세 측량 지점의 데이터를 손실 없이 수집했다."),
            new CodexEntryDef("record.contract", CodexCategory.Expedition, "개인 계약 달성", "선택한 위험 조건을 완수하고 추가 코어를 확보했다."),
            new CodexEntryDef("record.boss", CodexCategory.Expedition, "수호자 격퇴 기록", "지층 수호자를 쓰러뜨리고 생환 경로를 열었다."),
        };

        static readonly Dictionary<string, CodexEntryDef> ById = BuildIndex();
        static Dictionary<string, CodexEntryDef> BuildIndex()
        {
            var map = new Dictionary<string, CodexEntryDef>(StringComparer.Ordinal);
            foreach (var e in All) map[e.Id] = e;
            return map;
        }

        public static bool TryGet(string id, out CodexEntryDef entry) => ById.TryGetValue(id, out entry);
        public static string EquipmentId(RoleId role, EquipmentVariant variant)
            => $"equipment.{role.ToString().ToLowerInvariant()}.{variant.ToString().ToLowerInvariant()}";
        public static string ObjectiveId(ExpeditionObjectiveId objective) => objective switch
        {
            ExpeditionObjectiveId.CoreRecovery => "record.objective.core_recovery",
            ExpeditionObjectiveId.Survey => "record.objective.survey",
            _ => "record.objective.breach",
        };
        public static string CreatureId(EnemyKind kind, bool killed)
            => $"creature.{kind.ToString().ToLowerInvariant()}.{(killed ? "kill" : "seen")}";
        public static string GeologyId(TileType type) => type switch
        {
            TileType.Stone => "geology.stone", TileType.Ore => "geology.ore", TileType.Gem => "geology.gem",
            TileType.Crys => "geology.crys", TileType.Core => "geology.core", _ => "geology.dirt",
        };
    }

    public sealed class CodexSystem
    {
        readonly MetaState _meta;
        readonly HashSet<string> _known;
        public event Action<CodexDiscoveredEvent> Discovered;
        public CodexSystem(MetaState meta)
        {
            _meta = meta;
            _known = new HashSet<string>(meta?.codexEntries ?? new List<string>(), StringComparer.Ordinal);
        }

        public bool Discover(string id)
        {
            if (_meta == null || string.IsNullOrEmpty(id) || !_known.Add(id)) return false;
            _meta.codexEntries.Add(id); // 알 수 없는 미래 ID도 MetaState.Sanitize가 보존한다.
            if (FieldCodex.TryGet(id, out var entry)) Discovered?.Invoke(new CodexDiscoveredEvent(entry));
            return true;
        }

        public void RecordKill(EnemyState enemy)
        {
            if (_meta == null || enemy == null || enemy.IsBoss) return;
            string kind = enemy.Kind.ToString().ToLowerInvariant();
            Discover(FieldCodex.CreatureId(enemy.Kind, true));
            int count = _meta.IncrementCodexCount("kills." + kind);
            if (count >= 10) Discover("creature." + kind + ".veteran");
        }
    }
}
