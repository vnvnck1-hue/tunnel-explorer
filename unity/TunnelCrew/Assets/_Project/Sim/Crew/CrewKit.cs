using System;

namespace TunnelCrew.Sim
{
    /// <summary>적이 노릴 수 있는 대상 — 사람(<see cref="PlayerState"/>)과 AI 크루(<see cref="CrewMember"/>)가 같은 얼굴을 가진다.</summary>
    public interface ICrewTarget
    {
        Vec2 Pos { get; }
        bool IsDowned { get; }
        double HitRadius { get; }
    }

    /// <summary>
    /// 역할 행동 예산 — 원본 <c>KIT</c> (crew-ai.js 16650). "이 직업이 한 판에서 실제로 하는 일"을 수치로 적는다.
    /// 사람 플레이어의 INF 배율과 분리되어 있어 AI 를 넣어도 사람 밸런스가 흔들리지 않는다.
    /// 특성은 이 표의 **사본**을 바꾼다 (크루끼리도 서로 다른 빌드가 된다).
    /// </summary>
    public sealed class CrewKit
    {
        public RoleId Role;
        public double DigMul, GunMul, FireCd, Reload, Range;
        public int Mag;
        public bool DrillMelee, Crack, BreakerAtk;
        public double BreachCd, BreakerCd, FlareCd, PulseCd, GrappleCd, ExploreCd;
        public int BreakerRadius;
        public double Alert, Intercept, PathDigCost, Engage, Hp;
        public double MoveMul = 1, DashMul = 1;
        public int MaxTurrets, MaxNodes;
        public double TurretCd, TurretLife, TurretRange, TurretRate, TurretPower, NodeCd, NodeLife, NodeRadius;
        public int TurretMag;
        public string Job;

        public CrewKit Clone() => (CrewKit)MemberwiseClone();

        public static CrewKit For(RoleId role) => role switch
        {
            RoleId.Driller => new CrewKit
            {
                Role = role, DigMul = 1.35, GunMul = .55, FireCd = .30, Mag = 10, Reload = 1.7, Range = 6.5,
                DrillMelee = true, Crack = true, BreachCd = 8, Alert = 12, Intercept = 5, PathDigCost = 5, Engage = 3.6, Hp = 210, Job = "굴착",
            },
            RoleId.Gunner => new CrewKit
            {
                Role = role, DigMul = .10, GunMul = 1.55, FireCd = .15, Mag = 22, Reload = 1.9, Range = 9.5,
                PathDigCost = 30, Alert = 20, Intercept = 13, Engage = 5.2, Hp = 240,
                BreakerCd = 9, BreakerRadius = 1, BreakerAtk = true, Job = "화력 지원",
            },
            RoleId.Scout => new CrewKit
            {
                Role = role, DigMul = .42, GunMul = 1.0, FireCd = .22, Mag = 14, Reload = 1.5, Range = 8,
                PathDigCost = 12, Alert = 17, Intercept = 9, Engage = 4.6, Hp = 175,
                FlareCd = 7, PulseCd = 9, GrappleCd = 6, ExploreCd = 11, DashMul = 1.4, Job = "정찰",
            },
            _ => new CrewKit
            {
                Role = RoleId.Engineer, DigMul = .75, GunMul = .9, FireCd = .26, Mag = 16, Reload = 1.7, Range = 7.5,
                PathDigCost = 10, Alert = 14, Intercept = 7, Engage = 4.4, Hp = 200,
                MaxTurrets = 2, TurretCd = 12, TurretLife = 55, TurretMag = 14, TurretRange = 6.5, TurretRate = .34, TurretPower = .72,
                MaxNodes = 2, NodeCd = 16, NodeLife = 70, NodeRadius = 4.2, Job = "진지 구축",
            },
        };
    }

    /// <summary>AI_HUMANIZE_V1 성향 — 크루마다 다른 성실성·적극성·조준 흔들림·판단 주기·광맥 편애도.</summary>
    public sealed class CrewPersona
    {
        public double Eager, Discipline, Aggression, Caution, Greed, Curiosity, Focus, AimErr, ReloadAt, React, OreBias;
        public int Strafe;

        public static CrewPersona Roll(RoleId role, Rng rng)
        {
            var p = new CrewPersona
            {
                Eager = rng.Range(.60, 1.45), Discipline = rng.Range(.55, 1.00), Aggression = rng.Range(.60, 1.35),
                Caution = rng.Range(.55, 1.35), Greed = rng.Range(.35, 1.35), Curiosity = rng.Range(.50, 1.45),
                Focus = rng.Range(.55, 1.00), AimErr = rng.Range(.022, .085), ReloadAt = rng.Range(.26, .62),
                React = rng.Range(.11, .26), OreBias = rng.Range(.55, 1.50), Strafe = rng.NextDouble() < .5 ? 1 : -1,
            };
            // 직업마다 성향의 중심이 조금 다르다
            switch (role)
            {
                case RoleId.Gunner: p.Aggression *= 1.15; p.Curiosity *= .85; break;
                case RoleId.Scout: p.Curiosity *= 1.35; p.Caution *= 1.10; break;
                case RoleId.Driller: p.Discipline *= 1.10; p.Curiosity *= .90; break;
                case RoleId.Engineer: p.Discipline *= 1.15; p.Aggression *= .90; break;
            }
            return p;
        }
    }
}
