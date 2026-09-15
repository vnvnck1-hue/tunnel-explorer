using UnityEngine;

namespace TunnelCrew.Presentation.Prototype
{
    public sealed class ModularGunnerProjectile : MonoBehaviour
    {
        [SerializeField] float _speed = 42f;
        [SerializeField] float _lifetime = 0.42f;
        [SerializeField] Rigidbody2D _body;

        ModularGunnerController _owner;
        float _despawnAt;

        public void EditorAssign(Rigidbody2D body) => _body = body;

        public void Launch(Vector2 direction, ModularGunnerController owner)
        {
            _owner = owner;
            _despawnAt = Time.time + _lifetime;
            if (_body == null) _body = GetComponent<Rigidbody2D>();
            _body.linearVelocity = direction.normalized * _speed;
            transform.right = direction;
        }

        void Update()
        {
            if (_despawnAt > 0f && Time.time >= _despawnAt) Destroy(gameObject);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_owner != null && other.GetComponentInParent<ModularGunnerController>() == _owner) return;

            var enemy = other.GetComponentInParent<ModularGunnerEnemy>();
            if (enemy != null)
            {
                enemy.TakeHit(_body != null ? _body.linearVelocity.normalized : (Vector2)transform.right);
                Destroy(gameObject);
                return;
            }

            if (!other.isTrigger) Destroy(gameObject);
        }
    }
}
