using UnityEngine;

namespace TunnelCrew.Presentation.Prototype
{
    /// <summary>사격 감각을 확인하기 위한 단순 추적 표적.</summary>
    public sealed class ModularGunnerEnemy : MonoBehaviour
    {
        [SerializeField] int _health = 4;
        [SerializeField] float _moveSpeed = 1.15f;
        [SerializeField] Rigidbody2D _body;
        [SerializeField] SpriteRenderer _renderer;

        ModularGunnerController _target;
        Vector3 _baseScale;
        Color _baseColor;
        float _flashUntil;

        public void EditorAssign(Rigidbody2D body, SpriteRenderer renderer)
        {
            _body = body;
            _renderer = renderer;
        }

        void Awake()
        {
            _baseScale = transform.localScale;
            if (_renderer != null) _baseColor = _renderer.color;
            _target = FindFirstObjectByType<ModularGunnerController>();
        }

        void FixedUpdate()
        {
            if (_target == null) _target = FindFirstObjectByType<ModularGunnerController>();
            if (_target == null || _body == null) return;
            Vector2 delta = (Vector2)_target.transform.position - _body.position;
            _body.linearVelocity = delta.sqrMagnitude > 2.5f ? delta.normalized * _moveSpeed : Vector2.zero;
        }

        void Update()
        {
            transform.localScale = Vector3.Lerp(transform.localScale, _baseScale, 1f - Mathf.Exp(-18f * Time.deltaTime));
            if (_renderer != null && Time.time >= _flashUntil) _renderer.color = _baseColor;
        }

        public void TakeHit(Vector2 direction)
        {
            _health--;
            transform.localScale = new Vector3(_baseScale.x * 1.22f, _baseScale.y * 0.78f, 1f);
            if (_renderer != null)
            {
                _renderer.color = Color.white;
                _flashUntil = Time.time + 0.055f;
            }
            if (_body != null) _body.AddForce(direction * 2.5f, ForceMode2D.Impulse);
            if (_health <= 0) Destroy(gameObject);
        }
    }
}
