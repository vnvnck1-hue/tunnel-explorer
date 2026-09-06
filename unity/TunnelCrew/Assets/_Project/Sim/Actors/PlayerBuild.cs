namespace TunnelCrew.Sim
{
    public enum RoleId : byte { Driller = 0, Gunner, Scout, Engineer }

    /// <summary>
    /// 한 런 동안 특성·유물·직업이 바꾸는 플레이어 수치. 원본 <c>INF</c> 평면 객체의
    /// 전투 관련 필드를 떼어 낸 것이다. 원본 <c>infResetBuild()</c>(12234행)가 초기값의 정답지다.
    ///
    /// 특성 72종은 이 필드들을 바꾸는 것으로 효과를 낸다 (M4).
    /// </summary>
    public sealed class PlayerBuild
    {
        public RoleId Role = RoleId.Driller;

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

        // 탄창
        public int MagSize = 12;
        public int Ammo = 12;
        public double ReloadTime = 1.35;
        public double ReloadLeft = 0;
        public int ReloadCount = 0;
        public int ShotCounter = 0;

        public bool IsReloading => ReloadLeft > 0;

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

        /// <summary>원본 infResetBuild() — 런 시작 시 전부 기본값으로.</summary>
        public void Reset(RoleId role)
        {
            ApplyRole(role);
            DrillMul = GunMul = GunWallMul = FireRate = MoveMul = DrillReach = 1.0;
            Shots = 1; Pierce = 0; Bounces = 0; Explosive = false; LaserEvery = 0; SyncMul = 1.0;
            MagSize = 12; Ammo = 12; ReloadTime = 1.35; ReloadLeft = 0; ReloadCount = 0; ShotCounter = 0;
        }
    }
}
