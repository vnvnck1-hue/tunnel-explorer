using System;

namespace TunnelCrew.Sim
{
    /// <summary>
    /// 원본 픽셀 수치를 **셀 단위**로 정규화한 튜닝값. 1셀 = 1 Unity 유닛이다.
    ///
    /// 원본은 <c>CELL = 50</c> 픽셀 기준이고 튜닝값은 <c>teWorld(v) = v * CELL / 9</c> 로
    /// 환산됐다. 즉 셀 단위로는 <c>v / 9</c> 다. CELL 이 이동속도·투사체속도·사거리에까지
    /// 스며 있던 숨은 결합(analysis-02 §0)을 여기서 끊는다.
    ///
    /// 값의 출처는 <c>Assets/_Project/Data/raw/DEMO.json</c> · <c>TE.json</c> 이고,
    /// <c>TuningParityTests</c> 가 그 JSON 과 자동 대조한다. 손으로 고치지 말 것.
    /// </summary>
    public static class SimTuning
    {
        /// <summary>원본 픽셀 → 셀. `teWorld(v)/CELL` 과 같다.</summary>
        public const double PxPerCell = 50.0;
        public const double TeCellRef = 9.0;
        public static double TeCells(double v) => v / TeCellRef;
        public static double PxCells(double px) => px / PxPerCell;

        // ───────────────────────────── 플레이어
        /// <summary>R_SHELLY 25px.</summary>
        public const double PlayerRadius = 0.5;
        public const double PlayerHp = 181.0;
        public const double PlayerIFrame = 0.59;

        /// <summary>teMovePx() = teWorld(67) * (1.15 / max(0.85, sqrt(6.01))).</summary>
        public static readonly double MoveSpeed =
            TeCells(67.0) * (1.15 / Math.Max(0.85, Math.Sqrt(6.01)));

        public const double AimFollow = 0.18;

        // 대시
        public static readonly double DashDistance = TeCells(10.0);
        public const double DashDuration = 0.11;
        public const double DashCooldown = 0.91;
        /// <summary>원본은 4px 마다 서브스텝을 나눈다.</summary>
        public static readonly double DashSliceLength = PxCells(4.0);

        // 넉백
        public const double KnockDrag = 7.2;
        public static readonly double KnockSliceLength = PxCells(6.0);
        public static readonly double KnockStopSpeed = PxCells(8.0);
        public const int KnockMaxSlices = 14;

        // ───────────────────────────── 카메라
        /// <summary>teZoomZ() = 6.01 * (9/50). 원본 화면 배율의 기준.</summary>
        public const double BaseZoom = 6.01 * (TeCellRef / PxPerCell);
        public const double ZoomInMul = 1.35;
        public const double ZoomLerpRate = 2.4;
        public const double FollowSpeed = 0.07;
        public static readonly double DeadZone = TeCells(20.0);
        public static readonly double LookAhead = TeCells(25.0);
        /// <summary>카메라 중심은 화면 세로 42% 지점 (상하 비대칭).</summary>
        public const double VerticalAnchor = 0.42;
        /// <summary>월드 가장자리 여유: min(vw*0.18, teWorld(110)).</summary>
        public static readonly double CameraPadMax = TeCells(110.0);
        public const double CameraPadRatio = 0.18;

        // ───────────────────────────── 드릴
        /// <summary>shelDps() = 88 + 12*SAVE.lv.mine. 레거시 업그레이드는 폐기하므로 88 고정.</summary>
        public const double DrillDps = 88.0;
        public const double DrillDamageMul = 2.0;     // DEMO.drillDmg
        public const double DrillHitInterval = 0.06;  // DEMO.drillHitInt
        /// <summary>DRILL_TIP = R_SHELLY * 2.55.</summary>
        public const double DrillTip = PlayerRadius * 2.55;
        /// <summary>드릴 축 3점 샘플의 길이 배율과 가중치.</summary>
        public static readonly (double lenMul, double weight)[] DrillSamples =
        {
            (1.00, 1.00), (0.72, 0.85), (0.45, 0.55),
        };
        /// <summary>샘플점에서 이 거리 안의 타일만 채굴 대상. 원본 CELL*0.62.</summary>
        public const double DrillReachCells = 0.62;

        // 예열
        public const bool DrillWarmOn = true;
        public const double DrillWarmTime = 1.44;
        public const double DrillWarmMin = 0.2;
        public const double DrillWarmCurve = 1.35;
        public const double DrillWarmDecay = 3.49;

        // 과열
        public const bool DrillHeatOn = true;
        public const double DrillHeatBuild = 0.13;
        public const double DrillHeatCool = 0.45;
        public const double DrillHeatLock = 2.43;

        // 암반 반동
        public static readonly double DrillRockBounceSpeed = PxCells(340.0);
        public const double DrillRockBounceDuration = 0.16;
        public const double DrillRockBounceCooldown = 0.38;
        /// <summary>반동 시 즉시 밀리는 거리. 원본 CELL*0.22.</summary>
        public const double DrillRockBouncePush = 0.22;
        /// <summary>반동 감쇠 pow(0.08, dt), 속도가 이 값 밑이면 종료 (원본 12px/s).</summary>
        public const double DrillBounceDamp = 0.08;
        public static readonly double DrillBounceStopSpeed = PxCells(12.0);

        // ───────────────────────────── 전리품
        public const int LootCount = 1;
        public const int LootGemBonus = 1;
        public static readonly double LootScatter = PxCells(150.0);
        public static readonly double LootScatterRand = PxCells(90.0);
        public const double LootSpread = 3.3;
        public static readonly double LootPopZ = PxCells(21.0);
        public static readonly double LootPopVz = PxCells(170.0);
        public static readonly double LootGravity = PxCells(895.0);
        public static readonly double LootPickup = PxCells(28.0);
        public static readonly double LootMagnet = PxCells(72.0);
        public const double LootMagnetDelay = 0.8;
        public const double LootMagnetSpeed = 7.5;
        public const int ResourceMax = 480;

        // ───────────────────────────── 시야 (LOS)
        /// <summary>플레이어 시야 반경(타일). DEMO.losRange.</summary>
        public const int LosRange = 19;
        /// <summary>레이 개수. DEMO.losRays.</summary>
        public const int LosRays = 360;
        /// <summary>탐색 기억이 남는 반경(타일). DEMO.losMemory.</summary>
        public const int LosMemory = 11;
        /// <summary>탐색 지역의 최대 농도(0~1). DEMO.losExplored → 255 배 하면 74.</summary>
        public const double LosExplored = 0.29;
        /// <summary>
        /// 멀어진 탐색 지역이 유지하는 최소 농도 비율. 원본 주석: "갑자기 완전 검정으로
        /// 잘리지 않게 한다". 실제 식은 exp * (0.30 + 0.70 * fade²).
        /// </summary>
        public const double LosMemoryFloor = 0.30;
        /// <summary>크루 각자가 주변을 밝히는 반경·레이. 원본 crewRange/crewRays.</summary>
        public const int CrewVisionRange = 5;
        public const int CrewVisionRays = 72;

        // ───────────────────────────── 적
        //
        // 주의: 원본은 세 가지 단위를 섞어 쓴다. 옮길 때 하나씩 구분해야 한다.
        //   teWorld(v)  = v/9 셀   — 속도·사거리 계열
        //   CELL*v      = v 셀     — 이미 셀 단위인 값
        //   원시 픽셀   = v/50 셀  — 반지름·접촉거리 계열 (원본 주석에 "teWorld 스케일 금지")

        public const double EnemyHp = 81.0;
        public const double EnemyDamage = 5.5;
        public const double EnemyGunDamage = 22.0;   // 플레이어 탄이 적에게 주는 기준 피해
        public const double EnemyDrillMul = 1.15;

        /// <summary>DEMO.enemySpeed 9.8 → teWorld. 모든 적 속도의 기준.</summary>
        public static readonly double EnemySpeed = TeCells(9.8);
        /// <summary>DEMO.enemyAggro 320 → teWorld. 35.6셀로 상당히 넓다.</summary>
        public static readonly double EnemyAggro = TeCells(320.0);
        /// <summary>DEMO.enemyRadius 22 — 원시 픽셀.</summary>
        public static readonly double EnemyRadius = PxCells(22.0);
        /// <summary>DEMO.enemyTouch 12 — 원시 픽셀. 원본 주석이 teWorld 금지를 명시한다.</summary>
        public static readonly double EnemyTouch = PxCells(12.0);
        /// <summary>DEMO.enemyReach 16 — 원시 픽셀. `e.r + R_SHELLY + reach` 로 쓴다.</summary>
        public static readonly double EnemyReach = PxCells(16.0);
        /// <summary>DEMO.enemyKnock 70 — 원시 픽셀.</summary>
        public static readonly double EnemyKnock = PxCells(70.0);

        // 스폰
        public const double EnemySpawnInterval = 7.5;
        public const double EnemySpawnChance = 0.64;
        public const int EnemyMax = 16;
        /// <summary>스폰 링. 어그로 반경의 이 비율 사이에만 나온다.</summary>
        public const double EnemySpawnRingMin = 0.55;
        public const double EnemySpawnRingMax = 1.85;
        /// <summary>원거리 개체 비율. 광란종은 제외된다.</summary>
        public const double EnemyRangedRatio = 0.2;

        // 시야
        public const double EnemyVisionMul = 1.0;
        /// <summary>시야 원뿔 반각(도).</summary>
        public const double EnemyVisionFov = 62.0;
        /// <summary>이 거리 안은 전방위로 감지한다. CELL 배수 = 셀 단위.</summary>
        public const double EnemyNearSense = 1.5;
        /// <summary>놓친 뒤 이 시간이 지나면 배회로 돌아간다.</summary>
        public const double EnemyLoseTime = 5.0;
        /// <summary>시야 재계산 주기. 매 프레임 하지 않는다.</summary>
        public const double EnemySightInterval = 0.1;

        // 근접 모션
        public const double EnemyWindup = 0.42;
        public const double EnemyStrike = 0.12;
        public const double EnemyRecover = 0.34;
        public const double EnemyAttackCooldown = 1.5;
        /// <summary>타격 시 자기 자신이 앞으로 밀리는 양. teWorld(22).</summary>
        public static readonly double EnemyLungeSpeed = TeCells(22.0);
        /// <summary>타격 판정 사거리 배율. reach × 이 값.</summary>
        public const double EnemyStrikeReachMul = 1.7;
        /// <summary>타격 방향과의 내적이 이 값 미만이면 헛스윙.</summary>
        public const double EnemyStrikeDot = 0.32;

        // 원거리
        public static readonly double EnemyShotSpeed = TeCells(60.0);
        public const double EnemyShotDamageMul = 0.72;
        public const double EnemyShotCooldown = 2.6;
        public const double EnemyShotLife = 2.6;
        public static readonly double EnemyShotRadius = PxCells(6.5);
        /// <summary>사거리 유지 구간. CELL 배수 = 셀 단위.</summary>
        public const double EnemyKeepMin = 3.0;
        public const double EnemyKeepMax = 5.6;
        public const double EnemyFleeSpeed = 1.18;
        public const double EnemyRangedWindupMul = 1.2;

        // 배회
        public const double EnemyWanderRadius = 5.2;
        public const double EnemyWanderSpeed = 0.42;

        // 도약 (crawler 전용)
        public const double EnemyJumpChance = 0.04;
        public const double EnemyJumpDuration = 0.30;
        public static readonly double EnemyJumpSpeedMin = TeCells(42.0);
        public static readonly double EnemyJumpSpeedMax = TeCells(52.0);
        public const double EnemyJumpCooldownMin = 4.8;
        public const double EnemyJumpCooldownMax = 10.3;
        /// <summary>이 거리보다 가까우면 도약하지 않는다. CELL 배수.</summary>
        public const double EnemyJumpMinDistance = 2.4;

        // 소프트 분리
        /// <summary>원본 0.86 — 사장님 피드백(2026-09-06)으로 1.0. 반지름이 닿는 순간부터 밀어낸다.</summary>
        public const double EnemySepRatio = 1.0;
        /// <summary>적이 플레이어 위로 올라오지 않게 하는 여유. 반지름 합의 이 비율까지 밀어낸다.</summary>
        public const double EnemyPlayerSepRatio = 0.92;
        public const double EnemySepRate = 5.2;
        public static readonly double EnemySepMaxPush = PxCells(240.0);
        public const double EnemySepBossRate = 24.0;

        // 광란종(apex)
        public const double ApexHpMul = 8.0;
        public const double ApexDamageMul = 2.35;
        public const double ApexRadiusMul = 1.28;
        public const double ApexSpeedMul = 1.18;
        /// <summary>넉백 저항. 원본 resist 0.52.</summary>
        public const double ApexKnockResist = 0.52;

        /// <summary>덩치가 곧 속도. `(base/r)^1.35`, 0.55~1.7 로 자른다.</summary>
        public const double EnemySizeSpeedExponent = 1.35;
        public const double EnemySpeedMulMin = 0.55;
        public const double EnemySpeedMulMax = 1.7;

        /// <summary>시야·투사체가 벽을 통과하는지 볼 때의 표본 간격. CELL 배수.</summary>
        public const double SightSampleStep = 0.42;
        public const int SightMaxSamples = 72;

        // ───────────────────────────── 시뮬레이션 루프
        /// <summary>고정 60Hz 틱 (계획 D6).</summary>
        public const double FixedDeltaTime = 1.0 / 60.0;
        /// <summary>한 프레임에 몰아 처리할 최대 틱 수. 스파이럴 방지.</summary>
        public const int MaxTicksPerFrame = 5;
    }
}
