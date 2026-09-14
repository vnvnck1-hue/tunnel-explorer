using System;
using System.Collections.Generic;
namespace TunnelCrew.Sim
{
    public enum RoleId : byte { Driller = 0, Gunner, Scout, Engineer }
    public enum EquipmentVariant : byte { Standard = 0, Alternative }

    public readonly struct EquipmentDef
    {
        public readonly string Name, Summary, Tradeoff;
        public EquipmentDef(string name, string summary, string tradeoff)
        { Name = name; Summary = summary; Tradeoff = tradeoff; }
    }

    /// <summary>각 직업의 기본/대체 장비. 대체품은 상위호환이 아니라 플레이 리듬을 바꾸는 사이드그레이드다.</summary>
    public static class RoleEquipment
    {
        static readonly EquipmentDef[,] Defs =
        {
            { new EquipmentDef("고압 중형 드릴", "정밀 굴착과 안정적인 열 관리", "균형형"), new EquipmentDef("충격 코어 드릴", "넓게 부수고 지형을 빠르게 연다", "굴착력 -18% · 열 +28%") },
            { new EquipmentDef("벨트식 중화기", "48발 탄띠와 2배 연사력으로 퍼붓는 지속 사격", "탄 피해 -20% · 명중률 -30%"), new EquipmentDef("파쇄 산탄총", "근거리 5발 확산과 빠른 재장전", "탄창 8 · 사거리 짧음") },
            { new EquipmentDef("정찰 카빈", "기동 중 안정적인 단발 사격", "균형형"), new EquipmentDef("레일 카빈", "고속 관통탄으로 일렬의 적을 꿰뚫는다", "탄창 6 · 발사 간격 증가") },
            { new EquipmentDef("서비스 총기", "설치물과 병행하는 안정 사격", "균형형"), new EquipmentDef("코일 리피터", "도탄탄 · 전력장 안에서 화력 상승", "전력장 밖 화력 -20%") },
        };

        public static EquipmentDef For(RoleId role, EquipmentVariant variant) => Defs[(int)role, (int)variant];
    }

    /// <summary>
    /// 한 런 동안 특성·유물·직업이 바꾸는 플레이어 수치. 원본 <c>INF</c> 평면 객체의
    /// 전투 관련 필드를 떼어 낸 것이다. 원본 <c>infResetBuild()</c>(12234행)가 초기값의 정답지다.
    ///
    /// 특성 72종은 이 필드들을 바꾸는 것으로 효과를 낸다 (M4).
    /// </summary>
    /// <summary>
    /// 직업 설치물·스킬의 조정값. 원본은 INF.* 에 흩어져 있었다. 특성 카드가 바꾸는 값이라
    /// 층마다 새로 만들어지는 RoleSystem 이 아니라 런 단위인 PlayerBuild 에 산다.
    /// </summary>
    public sealed class RoleTuning
    {
        // 엔지니어
        public int EngineerMaxNodes = 2, EngineerMaxTurrets = 2;
        public double EngineerNodeLife = 50, EngineerNodeRadius = 4.0;
        public double EngineerTurretLife = 45, EngineerTurretRange = 5.5, EngineerTurretInterval = 0.34, EngineerTurretPower = 0.72;
        public int EngineerTurretMag = 18;
        public bool EngineerAutonomous = false;
        public double EngineerTurretReload = 1.0;
        // 거너 파쇄탄
        public double BreakerFuse = 2.0, BreakerMaxCd = 12.0;
        public int BreakerRadius = 1;
        public double BreakerDamageMul = 1.0, BreakerEarlyMul = 1.0, BreakerEnemyMul = 1.0;
        // 스카우트
        public double ScoutGrappleRange = 5.0, ScoutGrappleCdMul = 1.0;
        public double ScoutFlareRadMul = 1.0, ScoutFlareLifeMul = 1.0;
        public double ScoutVisionBonus = 0;
        public double ScoutPulseRadius = 4.0, ScoutPulseMax = 2.4;
        public int ScoutExploreXp = 1;
        // 드릴러 균열·돌파
        public double DrillerFoundationMul = 1.0, DrillerCoreNeedMul = 1.0;
        public double DrillerCrackHold = 2.5, DrillerCrackDecay = 0.012;
        public double DrillerQMul = 1.0, DrillerQCdMul = 1.0;
        public int DrillerShockRadius = 0;
        public double DrillerBreachRange = 3.0;
        public bool DrillerWideQ = false;
    }

    public sealed class PlayerBuild
    {
        /// <summary>직업 스킬·설치물 조정값 (특성 카드 대상).</summary>
        public RoleTuning Roles = new RoleTuning();
        public RoleId Role = RoleId.Driller;
        public EquipmentVariant Equipment = EquipmentVariant.Standard;

        // 직업 기본 배율 (INF_ROLES). 특성 배율과 곱해진다.
        public double RoleDigMul = 2.0;
        public double RoleGunMul = 0.65;
        public double RoleDashMul = 1.0;
        public bool RoleHasGun = true;

        // 특성이 올리는 배율
        public double DrillMul = 1.0;
        public double GunMul = 1.0;
        public double GunWallMul = 1.0;
        public double FireRate = 1.0;
        public double MoveMul = 1.0;
        public double DrillReach = 1.0;

        // 투사체 형태
        public int Shots = 1;
        public int Pierce = 0;
        public int Bounces = 0;
        public bool Explosive = false;
        /// <summary>0 이면 없음. N 이면 N 발마다 한 발이 레이저.</summary>
        public int LaserEvery = 0;
        /// <summary>드릴과 사격을 동시에 할 때의 배율 (거너 제외).</summary>
        public double SyncMul = 1.0;
        public double ProjectileSpeedMul = 1.0;
        public double ProjectileLifeMul = 1.0;
        public double ProjectileSpread = -1.0;
        /// <summary>1이면 정조준, 낮을수록 단발 조준선에 무작위 탄착 편차가 생긴다.</summary>
        public double Accuracy = 1.0;

        // 탄창
        public int MagSize = 12;
        public int Ammo = 12;
        public double ReloadTime = 1.35;
        public double ReloadLeft = 0;
        public int ReloadCount = 0;
        public int ShotCounter = 0;

        public bool IsReloading => ReloadLeft > 0;

        // ── 공용 특성이 바꾸는 값 (원본 INF.* — 효과 적용은 Mining/Loot/Movement 가 M4 후속으로 읽는다)
        /// <summary>한 번만 붙는 특성 표식 (원본 INF.traitFx).</summary>
        public HashSet<string> TraitFx = new HashSet<string>();
        public int DrillWidth = 0;            // 0 정면만 · 1 좌우 · 2 삼중
        public int DrillPenetration = 0;
        public bool FocusDrill = false;
        public double BreakShockRadius = 0, BreakShockPower = 0;
        public double LootMagnetMul = 1.0, LootPickupMul = 1.0;
        public double DrillMoveMul = 1.0;
        public double HeatBuildMul = 1.0;
        public int ShardBurst = 0;
        public double BreakShield = 0;
        public bool ChainCollapse = false;
        public int AutoDigEvery = 0;
        public bool EndlessOverdrive = false;
        public double XpMul = 1.0;
        // 자동 굴착 서브시스템 (원본 auxDrills/afterDrill/vortexMining/planetBreakerEvery/grandCollapseEvery/drillStorm)
        public int AuxDrills = 0;
        public double AuxDrillPower = 0;
        public bool AfterDrill = false;
        public bool VortexMining = false;
        public int PlanetBreakerEvery = 0;
        public int GrandCollapseEvery = 0;
        public int DrillStorm = 0;
        // 영구 노드·유물이 바꾸는 값 (원본 INF.coreBonusChance / oreHeal)
        public double CoreBonusChance = 0;
        public double OreHeal = 0;

        /// <summary>탄창 3~80, 늘어난 만큼 즉시 채운다. 48발 거너 기본 탄띠도 특성 적용 시 줄어들지 않는다.</summary>
        public void SetMag(int delta)
        {
            int old = MagSize;
            MagSize = Math.Max(3, Math.Min(80, (int)Math.Round(MagSize + (double)delta)));
            Ammo = Math.Max(0, Math.Min(MagSize, Ammo + Math.Max(0, MagSize - old)));
        }
        /// <summary>원본 infAdjustReload — 0.42~3.5초.</summary>
        public void AdjustReload(double mult) => ReloadTime = Math.Max(.42, Math.Min(3.5, ReloadTime * mult));

        public void ApplyRole(RoleId role)
        {
            Role = role;
            switch (role)
            {
                case RoleId.Driller:  RoleDigMul = 2.00; RoleGunMul = 0.65; RoleDashMul = 1.0; RoleHasGun = true; break;
                case RoleId.Gunner:   RoleDigMul = 0.00; RoleGunMul = 1.50; RoleDashMul = 0.9; RoleHasGun = true; break;
                case RoleId.Scout:    RoleDigMul = 0.35; RoleGunMul = 1.00; RoleDashMul = 1.4; RoleHasGun = true; break;
                case RoleId.Engineer: RoleDigMul = 0.75; RoleGunMul = 0.90; RoleDashMul = 1.0; RoleHasGun = true; break;
            }
        }

        /// <summary>
        /// 관전 중 크루와 교대할 때 쓰는 직업 전환. 런에서 얻은 특성 배율·추가 탄창은 유지하고,
        /// 기존 장비의 기본 보정만 제거한 뒤 새 직업 장비의 기본 보정을 적용한다.
        /// </summary>
        public void SwitchRolePreservingProgression(RoleId role, EquipmentVariant equipment = EquipmentVariant.Standard)
        {
            var oldBase = new PlayerBuild(); oldBase.Reset(Role, Equipment);
            var newBase = new PlayerBuild(); newBase.Reset(role, equipment);

            double gunProgress = Ratio(GunMul, oldBase.GunMul);
            double fireRateProgress = Ratio(FireRate, oldBase.FireRate);
            double reloadProgress = Ratio(ReloadTime, oldBase.ReloadTime);
            double speedProgress = Ratio(ProjectileSpeedMul, oldBase.ProjectileSpeedMul);
            double lifeProgress = Ratio(ProjectileLifeMul, oldBase.ProjectileLifeMul);
            double heatProgress = Ratio(HeatBuildMul, oldBase.HeatBuildMul);
            int shotBonus = Shots - oldBase.Shots;
            int pierceBonus = Pierce - oldBase.Pierce;
            int bounceBonus = Bounces - oldBase.Bounces;
            int magBonus = MagSize - oldBase.MagSize;
            int drillWidthBonus = DrillWidth - oldBase.DrillWidth;
            bool explosiveBonus = Explosive && !oldBase.Explosive;
            bool customSpread = Math.Abs(ProjectileSpread - oldBase.ProjectileSpread) > 1e-9;
            double preservedSpread = ProjectileSpread;

            Role = role;
            Equipment = equipment;
            RoleDigMul = newBase.RoleDigMul;
            RoleGunMul = newBase.RoleGunMul;
            RoleDashMul = newBase.RoleDashMul;
            RoleHasGun = newBase.RoleHasGun;
            GunMul = newBase.GunMul * gunProgress;
            FireRate = newBase.FireRate * fireRateProgress;
            ReloadTime = newBase.ReloadTime * reloadProgress;
            ProjectileSpeedMul = newBase.ProjectileSpeedMul * speedProgress;
            ProjectileLifeMul = newBase.ProjectileLifeMul * lifeProgress;
            HeatBuildMul = newBase.HeatBuildMul * heatProgress;
            Shots = Math.Max(1, newBase.Shots + shotBonus);
            Pierce = Math.Max(0, newBase.Pierce + pierceBonus);
            Bounces = Math.Max(0, newBase.Bounces + bounceBonus);
            Explosive = newBase.Explosive || explosiveBonus;
            DrillWidth = Math.Max(0, newBase.DrillWidth + drillWidthBonus);
            ProjectileSpread = customSpread ? preservedSpread : newBase.ProjectileSpread;
            Accuracy = newBase.Accuracy;
            MagSize = Math.Max(3, Math.Min(80, newBase.MagSize + magBonus));
            Ammo = MagSize;
            ReloadLeft = 0;
        }

        static double Ratio(double current, double baseline) =>
            Math.Abs(baseline) > 1e-9 ? current / baseline : current;

        /// <summary>원본 infResetBuild() — 런 시작 시 전부 기본값으로.</summary>
        public void Reset(RoleId role, EquipmentVariant equipment = EquipmentVariant.Standard)
        {
            ApplyRole(role);
            Equipment = equipment;
            Roles = new RoleTuning();
            DrillMul = GunMul = GunWallMul = FireRate = MoveMul = DrillReach = 1.0;
            Shots = 1; Pierce = 0; Bounces = 0; Explosive = false; LaserEvery = 0; SyncMul = 1.0;
            ProjectileSpeedMul = ProjectileLifeMul = Accuracy = 1.0; ProjectileSpread = -1.0;
            MagSize = 12; Ammo = 12; ReloadTime = 1.35; ReloadLeft = 0; ReloadCount = 0; ShotCounter = 0;
            TraitFx = new HashSet<string>();
            DrillWidth = 0; DrillPenetration = 0; FocusDrill = false; BreakShockRadius = 0; BreakShockPower = 0;
            LootMagnetMul = 1.0; LootPickupMul = 1.0; DrillMoveMul = 1.0; HeatBuildMul = 1.0; ShardBurst = 0; BreakShield = 0;
            ChainCollapse = false; AutoDigEvery = 0; EndlessOverdrive = false; XpMul = 1.0;
            AuxDrills = 0; AuxDrillPower = 0; AfterDrill = false; VortexMining = false; PlanetBreakerEvery = 0; GrandCollapseEvery = 0; DrillStorm = 0;
            CoreBonusChance = 0; OreHeal = 0;
            ApplyEquipment();
            ApplyStandardEquipmentTuning();
        }

        void ApplyStandardEquipmentTuning()
        {
            if (Role != RoleId.Gunner || Equipment != EquipmentVariant.Standard) return;

            MagSize = Ammo = 48;
            GunMul *= .80;
            FireRate *= 2.0;
            Accuracy = .70;
        }

        void ApplyEquipment()
        {
            if (Equipment != EquipmentVariant.Alternative) return;
            switch (Role)
            {
                case RoleId.Driller:
                    RoleDigMul *= .82;
                    DrillWidth = Math.Max(1, DrillWidth);
                    HeatBuildMul *= 1.28;
                    break;
                case RoleId.Gunner:
                    Shots = 5;
                    GunMul *= .34;       // 5발 전탄 명중 시 기본보다 강하지만 중거리부터 급감
                    FireRate *= .82;
                    MagSize = Ammo = 8;
                    ReloadTime = 1.05;
                    ProjectileLifeMul = .48;
                    ProjectileSpread = .16;
                    break;
                case RoleId.Scout:
                    Pierce += 3;
                    GunMul *= 1.42;
                    FireRate *= .56;
                    MagSize = Ammo = 6;
                    ProjectileSpeedMul = 1.55;
                    ProjectileLifeMul = 1.18;
                    break;
                case RoleId.Engineer:
                    Bounces += 1;
                    FireRate *= 1.1;
                    break;
            }
        }
    }
}
