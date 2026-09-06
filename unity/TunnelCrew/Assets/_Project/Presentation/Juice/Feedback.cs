using System.Collections.Generic;
using UnityEngine;

namespace TunnelCrew.Presentation
{
    /// <summary>
    /// 손맛 컴포넌트 — 히트스톱 · 카메라 킥/흔들림 · 스쿼시. 원본 <c>FEEL</c>(4838~4864) 과
    /// <c>J.kick / J.hs / J.shake</c> 를 한곳에 모았다. 유료 에셋(Feel) 대신 직접 만든다는
    /// 결정(analysis-04 §3.2)의 구현체다.
    ///
    /// 게임플레이 코드는 이 클래스의 메서드만 부른다. <c>Time.timeScale</c> 이나 카메라를
    /// 직접 건드리지 않는다. 그래야 원본처럼 60곳에 하드코딩되는 일이 생기지 않는다.
    ///
    /// 히트스톱은 <c>Time.timeScale</c> 로 건다. Sim 의 고정 틱은 timeScale 을 따르므로
    /// 원본의 "시간 5.5%" 효과와 같고, 12ms 처럼 틱보다 짧은 멈춤도 살아난다(계획 D6 개정).
    /// </summary>
    public sealed class Feedback : MonoBehaviour
    {
        public static Feedback Instance { get; private set; }

        [Header("히트스톱")]
        [Tooltip("원본 J.hs — 멈춤 중 시간 배율")]
        [SerializeField] float _hitstopScale = 0.055f;
        [Tooltip("원본 FEEL.stop 상한 95ms (모션 감소 26ms)")]
        [SerializeField] float _hitstopCapMs = 95f;
        [SerializeField] bool _reducedMotion = false;

        [Header("카메라 킥")]
        [Tooltip("킥 1 단위가 카메라를 몇 셀 밀어내는가. 원본은 픽셀 단위 J.sdx 였다.")]
        [SerializeField] float _kickCellsPerUnit = 0.045f;
        [SerializeField] float _kickDecay = 9f;
        [SerializeField] float _shakeFrequency = 34f;

        float _hitstopLeft;      // 실시간 초
        Vector2 _kickVel;
        Vector2 _kickOffset;
        float _shakePhase;

        // 스쿼시 — 원본 FEEL.p
        float _recoil, _recoilA, _recoilT;
        float _hurtT, _hurtMax, _hurtLevel;
        float _dashT, _dashMax = 0.16f, _dashA;
        float _landT, _landMax = 0.12f;

        /// <summary>플레이어 스프라이트에 적용할 변형. 원본 FEEL.transform().</summary>
        public struct SquashTransform { public float ScaleX, ScaleY, Angle, OffsetX, OffsetY; }

        public Vector2 CameraOffset => _kickOffset;
        public bool ReducedMotion { get => _reducedMotion; set => _reducedMotion = value; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }
        void OnDestroy() { if (Instance == this) { Instance = null; Time.timeScale = 1f; } }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // 히트스톱 — 실시간으로 잰다. 끝나면 배율 복구.
            if (_hitstopLeft > 0)
            {
                _hitstopLeft -= dt;
                Time.timeScale = _hitstopLeft > 0 ? _hitstopScale : 1f;
            }

            // 카메라 킥 — 스프링 감쇠 + 진동. 원본 J.shake*cos(sph)*exp(-sph*.16)
            _shakePhase += dt * _shakeFrequency;
            _kickVel = Vector2.Lerp(_kickVel, Vector2.zero, 1f - Mathf.Exp(-_kickDecay * dt));
            _kickOffset = _kickVel * Mathf.Cos(_shakePhase);

            // 스쿼시 타이머
            float sdt = Time.deltaTime;
            _recoilT = Mathf.Max(0, _recoilT - sdt);
            _recoil *= Mathf.Pow(0.0008f, sdt);
            _hurtT = Mathf.Max(0, _hurtT - sdt);
            _dashT = Mathf.Max(0, _dashT - sdt);
            _landT = Mathf.Max(0, _landT - sdt);
        }

        // ───────────────────────────── 공개 API (게임플레이가 부르는 것)

        /// <summary>원본 FEEL.stop(ms).</summary>
        public void Hitstop(float ms)
        {
            float cap = _reducedMotion ? 26f : _hitstopCapMs;
            float sec = Mathf.Min(cap, Mathf.Max(0, ms)) / 1000f;
            _hitstopLeft = Mathf.Max(_hitstopLeft, sec);
            if (_hitstopLeft > 0) Time.timeScale = _hitstopScale;
        }

        /// <summary>원본 J.kick(strength, dirX, dirY). 방향이 없으면 무작위.</summary>
        public void Kick(float strength, Vector2 dir)
        {
            if (_reducedMotion) strength *= 0.35f;
            if (dir.sqrMagnitude < 1e-6f) dir = Random.insideUnitCircle.normalized;
            _kickVel += -dir.normalized * (strength * _kickCellsPerUnit);
        }

        /// <summary>원본 FEEL.shot — 사격 반동.</summary>
        public void Shot(float angle, string visualId)
        {
            bool strong = visualId == "laser" || visualId == "explosive" || visualId == "rain";
            _recoilA = angle; _recoil = strong ? 7f : 4f; _recoilT = strong ? 0.15f : 0.10f;
            Kick(strong ? 1.4f : 0.65f, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
        }

        /// <summary>원본 FEEL.enemyHit — 피격 플래시·히트스톱·킥.</summary>
        public void EnemyHit(bool dead, bool apex, bool boss, float dmg, Vector2 dir)
        {
            bool big = dead || dmg > 20f;
            Hitstop(dead ? (apex ? 58f : 42f) : (boss ? 12f : 20f));
            if (!dead) Kick(boss ? 0.45f : (big ? 1.35f : 0.75f), dir);
        }

        /// <summary>원본 FEEL.hurt — 3단계 피격.</summary>
        public void PlayerHurt(float dmg, float hp, float hpMax, Vector2 dir)
        {
            float ratio = dmg / Mathf.Max(1f, hpMax);
            int level = hp <= hpMax * 0.22f || ratio >= 0.28f ? 3 : ratio >= 0.14f ? 2 : 1;
            _hurtLevel = level;
            _hurtT = _hurtMax = level == 3 ? 0.34f : level == 2 ? 0.25f : 0.18f;
            Hitstop(level == 3 ? 68f : level == 2 ? 46f : 28f);
            Kick(level == 3 ? 3.2f : level == 2 ? 2.0f : 1.1f, -dir);
            HurtLevel = level;
        }

        /// <summary>원본 FEEL.dash / 착지.</summary>
        public void Dash(Vector2 dir)
        {
            _dashA = Mathf.Atan2(dir.y, dir.x);
            _dashT = _dashMax; _landT = 0;
            Kick(1.1f, dir);
        }
        public void DashLand() { _landT = _landMax; Kick(0.75f, new Vector2(Mathf.Cos(_dashA), Mathf.Sin(_dashA))); }

        /// <summary>블록 파괴 킥. 원본 brkKickSoft/Hard/Ore.</summary>
        public void BlockBroken(bool ore, bool hard, Vector2 dir)
        {
            Kick(ore ? 7.6f : hard ? 6.2f : 18.2f, dir);
            Hitstop(ore ? 78f : hard ? 56f : 40f);
        }

        /// <summary>드릴 타격 비트. 원본 drillKick 6.</summary>
        public void DrillBeat(Vector2 dir) => Kick(6f * 0.25f, dir);

        /// <summary>HUD 가 읽는 피격 단계 (0=없음).</summary>
        public int HurtLevel { get; private set; }
        public float HurtProgress => _hurtMax > 0 ? _hurtT / _hurtMax : 0f;

        /// <summary>원본 FEEL.transform() — 플레이어 스프라이트 스쿼시·리코일.</summary>
        public SquashTransform PlayerSquash(Vector2 velocity)
        {
            float sx = 1, sy = 1, a = _dashA, ox = 0, oy = 0;
            if (_dashT > 0) { float u = 1 - _dashT / _dashMax, s = Mathf.Sin(Mathf.Min(1, u) * Mathf.PI); sx *= 0.86f + s * 0.30f; sy *= 1.12f - s * 0.18f; }
            if (_landT > 0) { float s = Mathf.Sin((_landT / _landMax) * Mathf.PI); sx *= 1 + s * 0.10f; sy *= 1 - s * 0.13f; }
            if (_hurtT > 0) { float s = Mathf.Sin((_hurtT / _hurtMax) * Mathf.PI); sx *= 1 + s * 0.08f * _hurtLevel; sy *= 1 - s * 0.055f * _hurtLevel; a = Mathf.Atan2(velocity.y, velocity.x); }
            if (_recoilT > 0 || _recoil > 0.05f) { ox -= Mathf.Cos(_recoilA) * _recoil * 0.02f; oy -= Mathf.Sin(_recoilA) * _recoil * 0.02f; a = _recoilA; }
            if (_hurtT <= 0) HurtLevel = 0;
            return new SquashTransform { ScaleX = sx, ScaleY = sy, Angle = a, OffsetX = ox, OffsetY = oy };
        }
    }
}
