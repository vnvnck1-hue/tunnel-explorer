using System;
using System.Collections.Generic;

namespace TunnelCrew.Sim
{
    public enum CrewGoalKind : byte { Guard, Follow, Fight, Escape, Revive, Turret, Node, Flare, Mine, Crack, Pulse, Scout, Watch, Loot }

    /// <summary>decide() 가 돌려주는 목표. 원본 goal 객체.</summary>
    public sealed class CrewGoal
    {
        public CrewGoalKind Kind;
        public Vec2 At;
        public EnemyState Enemy;
        public bool Boss;
        public int C, R;
        public string Label;
        /// <summary>구조 대상 — 다른 크루(null 이면 리더).</summary>
        public CrewMember ReviveTarget;
        public LootItem Res;
    }

    public sealed class CrewTurret
    {
        public CrewMember Owner;
        public Vec2 Position;
        public double Aim, Cd, Life, MaxLife, Reload, Range, Rate, Power;
        public int Ammo, Mag;
        public bool Powered;
    }

    public sealed class CrewNode
    {
        public CrewMember Owner;
        public Vec2 Position;
        public double Life, MaxLife, Radius;
        public Flare Light;
    }

    public sealed class CrewBreaker { public Vec2 At; public int C, R; public double T; public bool NoEarly; }
    public sealed class CrewCrack { public double P, Last; public TileType Type; public int Stage; public CrewMember Owner; }
    public sealed class CrewMark { public Vec2 At; public bool Threat; public double Ttl; public EnemyState Enemy; }

    /// <summary>AI 크루 한 명 — 원본 spawnMember() 의 m 객체. 성향은 지층이 바뀌어도 유지된다 (같은 사람).</summary>
    public sealed class CrewMember : ICrewTarget
    {
        public int Id;
        public RoleId Role;
        public CrewKit Kit;
        public CrewPersona Pers;

        public Vec2 Position, Velocity;
        public double Aim; public int Face = 1;
        public double Hp, HpMax, IFrames;
        public bool Down; public double DownT, ReviveT;
        public bool Digging; public double Drill;
        public double GunCd, ReloadLeft, ReloadTime; public int Ammo, Mag;
        public double QCd, ECd, ShieldT, DashCd;
        public Vec2 DashVel; public double DashT; public bool Dashing => DashT > 0;
        public double FlareCd, TurretCd, NodeCd, BreakerCd;
        public readonly List<CrewBreaker> Breakers = new List<CrewBreaker>();
        public double LootCd; public int LootGot; public bool Dodging; public bool DashWant;
        public readonly Dictionary<string, double> Wait = new Dictionary<string, double>();
        public double Mood = 1, MoodT;
        public double BreachT, BreachCd, PulseCd, GrappleCd, ExploreCd;
        public CrewGoal CrackTarget; public double CrackT, CrackPatience;
        public Vec2? Watch; public double WatchT, SweepT, SweepA, FarJit;
        public double IdleT; public int IdleDir = 1; public double StrafeT; public double? LastFoeDir;
        public CrewGoal Goal; public readonly List<int> Path = new List<int>(); public double PathAge; public string PathKey = ""; public double React;
        public CrewGoal MineTarget; public double MineUntil, CrackUntil;
        public double StuckT; public Vec2 Last; public double Jitter, JitterA;
        public double BuriedT, LostT, MoveDustT;
        public string Say = ""; public double SayT;
        // 개인 성장 (§5.2)
        public int Level = 1, Xp, XpNeed = XpGate.NeedFor(1), XpCapped; public double XpTrickle;
        public readonly List<string> Traits = new List<string>(); public readonly HashSet<string> TraitIds = new HashSet<string>();
        public HashSet<int> Sectors;
        // 탈출 탑승
        public bool Boarded; public double BoardT;
        // 지형 진척 감시 (AIGEO progress)
        public int GeoC = -1, GeoR = -1; public double GeoT, GeoHp;

        Vec2 ICrewTarget.Pos => Position;
        bool ICrewTarget.IsDowned => Down;
        double ICrewTarget.HitRadius => SimTuning.PlayerRadius;

        public bool Alive => !Down;
        public string StateLabel => Down ? "다운 · 구조 필요" : SayT > 0 ? Say : (Goal != null ? Goal.Label : "—");
    }
}
