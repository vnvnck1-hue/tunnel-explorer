namespace TunnelCrew.Sim
{
    /// <summary>
    /// 원본 <c>G.sh</c> + 흩어져 있던 플레이어 관련 <c>G.*</c> 필드를 한곳에 모은 것.
    /// (G.dash / G.knock / G.stunT / G.drillWarm / G.drillHeat / G.php …)
    /// </summary>
    public sealed class PlayerState
    {
        public Vec2 Position;
        public Vec2 Velocity;

        /// <summary>조준 각도(라디안). 원본 sh.aim.</summary>
        public double Aim;
        /// <summary>조준점의 보간 위치. 원본 G.aimX/aimY.</summary>
        public Vec2 AimPoint;
        public bool AimReady;

        /// <summary>마지막으로 이동한 방향. 정지 중 대시 방향에 쓴다. 원본 G.dirx/diry.</summary>
        public Vec2 LastMoveDir = new Vec2(0, 1);

        /// <summary>드릴 회전 게이지(0~1). 렌더 전용. 원본 sh.drill.</summary>
        public double DrillSpin;

        // 체력
        public double Hp = SimTuning.PlayerHp;
        public double HpMax = SimTuning.PlayerHp;
        public double IFrames;
        public bool Downed;

        // 대시
        public bool DashActive;
        public Vec2 DashVelocity;
        public double DashTimeLeft;
        public double DashCooldown;

        // 넉백 · 기절
        public Vec2 Knock;
        public double StunTime;

        // 암반 반동
        public bool BounceActive;
        public Vec2 BounceVelocity;
        public double BounceTimeLeft;
        public double RockBounceCooldown;

        // 드릴 예열 · 과열
        public double DrillWarm;
        public double DrillHeat;
        public double DrillHeatLock;

        /// <summary>드릴 타격 누적 — drillHitInterval 마다 한 번씩 연출을 낸다.</summary>
        public double DrillDamageAccum;
        public double DrillTimeAccum;

        /// <summary>이번 틱에 실제로 벽을 파고 있었는가.</summary>
        public bool IsDigging;
        /// <summary>압쇄 비트 — 같은 벽을 깎은 시간 (특성).</summary>
        public int FocusCell = -1;
        public double FocusTime;

        public bool CanMove => StunTime <= 0 && !Downed;

        /// <summary>원본 drillCanUse() — 과열 잠금 중에는 드릴이 안 돈다.</summary>
        public bool CanDrill => !(SimTuning.DrillHeatOn && DrillHeatLock > 0);
    }

    /// <summary>한 틱 분량의 플레이어 입력. Presentation 이 채워서 Sim 에 넘긴다.</summary>
    public struct PlayerInput
    {
        /// <summary>정규화된 이동 방향. 입력이 없으면 Zero.</summary>
        public Vec2 Move;
        /// <summary>마우스가 가리키는 월드 좌표(셀 단위).</summary>
        public Vec2 AimWorld;
        public bool DrillHeld;
        public bool FireHeld;
        public bool DashPressed;
        public bool ReloadPressed;
        public bool SkillQPressed;
        public bool SkillEPressed;
        /// <summary>X — 탈출 포트 지정 시작/취소.</summary>
        public bool EscapePressed;
        /// <summary>좌클릭 눌린 프레임 — 탈출 지점 확정 등 단발 판정.</summary>
        public bool PrimaryPressed;
        /// <summary>우클릭 눌린 프레임 — 지정 취소.</summary>
        public bool SecondaryPressed;
    }
}
