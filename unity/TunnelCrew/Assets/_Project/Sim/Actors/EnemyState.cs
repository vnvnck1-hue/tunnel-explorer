namespace TunnelCrew.Sim
{
    public enum EnemyKind : byte { Crawler = 0, Spitter, BroodBeast }

    /// <summary>배회 → 추격. 놓치면 일정 시간 뒤 배회로 돌아간다.</summary>
    public enum EnemyAi : byte { Wander = 0, Chase }

    /// <summary>공격 모션 3단계. 이 중에는 이동하지 못한다.</summary>
    public enum AttackPhase : byte { None = 0, Windup, Strike, Recover }

    /// <summary>
    /// 원본 <c>G.enemies[]</c> 의 원소(6260~6293행). 잡몹·광란종·보스가 같은 구조를 쓴다.
    /// </summary>
    public sealed class EnemyState
    {
        public Vec2 Position;
        public Vec2 Velocity;
        /// <summary>넉백 속도. 자체 이동과 별도로 더해진다.</summary>
        public Vec2 Knock;

        public EnemyKind Kind;
        public double Radius;
        public double Hp, HpMax;

        /// <summary>덩치에서 계산된 이동 배율. 클수록 느리다.</summary>
        public double SpeedMul = 1.0;
        public double DamageMul = 1.0;

        /// <summary>위협도가 오르면 살아 있는 적의 체력도 비례해 오른다. 그때 쓰는 기준값.</summary>
        public double ThreatHpMul = 1.0;

        public bool IsApex;
        public bool IsBoss;
        /// <summary>침을 뱉는 원거리 개체인가. kind 와 별개 플래그다.</summary>
        public bool IsRanged;

        // ── AI
        public EnemyAi Ai = EnemyAi.Wander;
        /// <summary>자기 영역의 중심. 스폰 지점이거나 플레이어를 놓친 지점이다.</summary>
        public Vec2 Home;
        public Vec2 WanderTarget;
        public double WanderTimer;
        public bool WanderIdle = true;
        /// <summary>바라보는 각도. 시야 원뿔의 기준이다.</summary>
        public double FaceAngle;
        public int ScanDir = 1;
        public int StrafeDir = 1;

        public bool SeesTarget;
        public double LostTime = 99;
        public double SightCooldown;
        public Vec2 LastSeen;

        // ── 공격
        public AttackPhase Attack = AttackPhase.None;
        public double AttackTimer;
        public double AttackWindupTotal;
        public double AttackCooldown;
        public Vec2 AttackDir = new Vec2(1, 0);
        /// <summary>보스 접촉 피해 쿨다운 (원본 e.cd).</summary>
        public double ContactCooldown;

        // ── 도약 (crawler 전용)
        public double JumpTime, JumpDuration, JumpCooldown = double.PositiveInfinity;
        public Vec2 JumpVelocity;

        // ── 상태이상
        public double StunTime;
        public double FrozenTime;
        public double SlowTime;
        public double SlowMul = 0.7;

        // ── 연출용
        /// <summary>피격 플래시 타이머.</summary>
        public double Hurt;
        public double AnimTime;
        public double Bob;
        public double BlinkTime, BlinkCooldown;

        public bool Alive => Hp > 0;

        /// <summary>도약 중이면 조종 불가. 원본은 jumpT 동안 속도를 고정한다.</summary>
        public bool IsJumping => JumpTime > 0;

        /// <summary>원본 `reach = e.r + R_SHELLY + enemyReach`.</summary>
        public double Reach => Radius + SimTuning.PlayerRadius + SimTuning.EnemyReach;
    }
}
