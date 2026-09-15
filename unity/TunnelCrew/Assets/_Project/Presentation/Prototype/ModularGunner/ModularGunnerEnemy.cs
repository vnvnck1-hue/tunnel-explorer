using UnityEngine;

namespace TunnelCrew.Presentation.Prototype
{
    /// <summary>
    /// 사격 감각을 확인하기 위한 단순 추적 표적.
    /// 승인 아트의 6프레임(무손상 · 경피격 · 균열 · 붕괴 · 파쇄 · 잔해)으로
    /// 체력 단계별 손상과 사망 연출을 표시한다.
    /// </summary>
    public sealed class ModularGunnerEnemy : MonoBehaviour
    {
        // 시트 프레임 순서. 0~2 는 생존 상태, 3~5 는 사망 재생 구간이다.
        const int FrameIntact = 0;
        const int FrameLightHit = 1;
        const int FrameCracked = 2;
        const int FrameDeathStart = 3;

        const float HitPoseDuration = 0.085f;
        const float DeathFrameDuration = 0.055f;

        [SerializeField] int _maxHealth = 4;
        [SerializeField] float _moveSpeed = 1.15f;
        [SerializeField] Rigidbody2D _body;
        [SerializeField] SpriteRenderer _renderer;
        [SerializeField] Sprite[] _frames;

        ModularGunnerController _target;
        Vector3 _baseScale;
        Color _baseColor;
        int _health;
        float _flashUntil;
        float _hitPoseUntil;
        bool _dying;
        int _deathFrame;
        float _nextDeathFrameAt;

        public void EditorAssign(Rigidbody2D body, SpriteRenderer renderer, Sprite[] frames)
        {
            _body = body;
            _renderer = renderer;
            _frames = frames;
        }

        void Awake()
        {
            ModularGunnerPhysicsLayers.Assign(gameObject, ModularGunnerPhysicsLayers.Enemy);
            _baseScale = transform.localScale;
            _health = _maxHealth;
            if (_renderer != null)
            {
                _baseColor = _renderer.color;
                SetFrame(FrameIntact);
            }
            _target = FindFirstObjectByType<ModularGunnerController>();
        }

        void FixedUpdate()
        {
            if (_dying) return;
            if (_target == null) _target = FindFirstObjectByType<ModularGunnerController>();
            if (_target == null || _body == null) return;
            Vector2 delta = (Vector2)_target.transform.position - _body.position;
            _body.linearVelocity = delta.sqrMagnitude > 2.5f ? delta.normalized * _moveSpeed : Vector2.zero;
        }

        void Update()
        {
            if (_dying)
            {
                AdvanceDeath();
                return;
            }

            transform.localScale = Vector3.Lerp(transform.localScale, _baseScale, 1f - Mathf.Exp(-18f * Time.deltaTime));
            // 백색 플래시는 즉시 꺼지지 않고 곡선을 그리며 원래 색으로 돌아온다(로드맵 4.3).
            if (_renderer != null && Time.time >= _flashUntil)
                _renderer.color = Color.Lerp(_renderer.color, _baseColor, 1f - Mathf.Exp(-22f * Time.deltaTime));
            if (Time.time >= _hitPoseUntil) SetFrame(SurvivingFrame());
        }

        /// <summary>체력이 절반 아래로 내려가면 균열 프레임을 계속 유지한다.</summary>
        int SurvivingFrame() => _health * 2 <= _maxHealth ? FrameCracked : FrameIntact;

        void SetFrame(int index)
        {
            if (_renderer == null || _frames == null || _frames.Length == 0) return;
            _renderer.sprite = _frames[Mathf.Clamp(index, 0, _frames.Length - 1)];
        }

        void AdvanceDeath()
        {
            if (Time.time < _nextDeathFrameAt) return;
            _deathFrame++;
            int frame = FrameDeathStart + _deathFrame;
            if (_frames == null || frame >= _frames.Length)
            {
                Destroy(gameObject);
                return;
            }
            SetFrame(frame);
            _nextDeathFrameAt = Time.time + DeathFrameDuration;
        }

        public void TakeHit(Vector2 direction)
        {
            if (_dying) return;

            _health--;
            bool killed = _health <= 0;
            transform.localScale = new Vector3(_baseScale.x * 1.22f, _baseScale.y * 0.78f, 1f);
            if (_renderer != null)
            {
                _renderer.color = Color.white;
                _flashUntil = Time.time + 0.055f;
            }
            if (_body != null) _body.AddForce(direction * 2.5f, ForceMode2D.Impulse);
            ModularGunnerEffects.Instance?.EnemyImpact(transform.position, direction, ClassifyReaction(killed));

            if (killed)
            {
                BeginDeath();
                return;
            }

            SetFrame(FrameLightHit);
            _hitPoseUntil = Time.time + HitPoseDuration;
        }

        /// <summary>
        /// 약/강/치명타/사망 프리셋 선택(로드맵 4.3).
        /// 균열 단계로 넘어간 표적은 강피격으로 읽히고, 일부 타격은 치명타로 뽑는다.
        /// </summary>
        ModularGunnerEffects.Reaction ClassifyReaction(bool killed)
        {
            if (killed) return ModularGunnerEffects.Reaction.Death;
            if (Random.value < 0.12f) return ModularGunnerEffects.Reaction.Critical;
            return _health * 2 <= _maxHealth
                ? ModularGunnerEffects.Reaction.Heavy
                : ModularGunnerEffects.Reaction.Light;
        }

        void BeginDeath()
        {
            _dying = true;
            _deathFrame = -1;
            _nextDeathFrameAt = 0f;
            // 사망 재생 동안에는 충돌과 추적을 멈춘다. 잔해만 남아 프레임을 소화한다.
            foreach (var collider in GetComponentsInChildren<Collider2D>()) collider.enabled = false;
            if (_body != null)
            {
                _body.linearVelocity = Vector2.zero;
                _body.simulated = false;
            }
            transform.localScale = _baseScale;
            if (_renderer != null) _renderer.color = _baseColor;
            AdvanceDeath();
        }
    }
}
