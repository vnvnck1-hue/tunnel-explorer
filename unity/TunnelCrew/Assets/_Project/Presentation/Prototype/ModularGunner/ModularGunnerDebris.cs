using UnityEngine;

namespace TunnelCrew.Presentation.Prototype
{
    /// <summary>
    /// 탑다운 평면 충돌과 별도의 높이/바운스를 결합한 짧은 수명의 픽셀 파편.
    /// 로드맵 4.8 — 풀에서 빌려 쓰며, 착지 바운스 횟수와 회전 감쇠를 제한하고,
    /// 벽에 부딪히면 방향을 반사하며 높이를 잃는다. 수명 끝에서는 어둡게 정착한 뒤 사라진다.
    /// </summary>
    public sealed class ModularGunnerDebris : MonoBehaviour
    {
        const float Gravity = 12f;
        const int MaxBounces = 3;
        const float SettleFraction = 0.42f;   // 수명의 마지막 이 비율 동안 어둡게 정착한다.
        const float FadeTail = 0.3f;

        Rigidbody2D _body;
        CircleCollider2D _collider;
        Transform _visual;
        SpriteRenderer _renderer;
        SpriteRenderer _shadow;

        Sprite _shadowSprite;
        float _height;
        float _verticalSpeed;
        float _bounce;
        float _life;
        float _age;
        float _spin;
        float _spinDamping;
        int _bounces;
        Color _color;
        float _scale;
        bool _active;

        static PhysicsMaterial2D _sharedBounceMaterial;

        static PhysicsMaterial2D SharedBounceMaterial
        {
            get
            {
                if (_sharedBounceMaterial != null) return _sharedBounceMaterial;
                _sharedBounceMaterial = new PhysicsMaterial2D("Prototype Pixel Debris Bounce")
                {
                    bounciness = 0.42f,
                    friction = 0.34f,
                };
                return _sharedBounceMaterial;
            }
        }

        /// <summary>풀이 한 번만 부르는 구성. 컴포넌트는 여기서만 만든다.</summary>
        public void PoolAwake()
        {
            ModularGunnerPhysicsLayers.Assign(gameObject, ModularGunnerPhysicsLayers.Debris);
            _body = gameObject.AddComponent<Rigidbody2D>();
            _body.gravityScale = 0f;
            _body.linearDamping = 2.8f;
            _body.angularDamping = 1.5f;
            _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            _collider = gameObject.AddComponent<CircleCollider2D>();
            _collider.sharedMaterial = SharedBounceMaterial;

            _visual = new GameObject("Pixel Visual").transform;
            _visual.SetParent(transform, false);
            _renderer = _visual.gameObject.AddComponent<SpriteRenderer>();

            // 공중 파편은 높이에 따라 그림자가 작아지고 옅어진다(로드맵 4.10).
            var shadowGo = new GameObject("Blob Shadow");
            shadowGo.transform.SetParent(transform, false);
            _shadow = shadowGo.AddComponent<SpriteRenderer>();
            _shadow.sortingOrder = 3;

            Release();
        }

        public void Prepare(Sprite sprite, Sprite shadowSprite, Material material, Color color, Vector2 planarVelocity,
            float verticalSpeed, float life, float scale, float spin, float bounce, int sortingOrder)
        {
            _renderer.sprite = sprite;
            _renderer.sharedMaterial = material;
            _renderer.color = color;
            _renderer.sortingOrder = sortingOrder;
            _renderer.enabled = true;
            _visual.localScale = Vector3.one * scale;
            _visual.localRotation = Quaternion.identity;
            _visual.localPosition = Vector3.zero;

            _shadowSprite = shadowSprite;
            _shadow.sprite = shadowSprite;
            _shadow.sharedMaterial = material;
            _shadow.enabled = shadowSprite != null;

            _collider.radius = Mathf.Max(0.035f, scale * 0.22f);
            _collider.enabled = true;
            _body.simulated = true;
            _body.linearVelocity = planarVelocity;

            _color = color;
            _scale = scale;
            _height = 0f;
            _verticalSpeed = verticalSpeed;
            _bounce = bounce;
            _life = life;
            _age = 0f;
            _spin = spin;
            _spinDamping = 2.2f;
            _bounces = 0;
            _active = true;
            gameObject.SetActive(true);
        }

        void Release()
        {
            _active = false;
            _body.linearVelocity = Vector2.zero;
            _body.simulated = false;
            _collider.enabled = false;
            _renderer.enabled = false;
            _shadow.enabled = false;
            gameObject.SetActive(false);
        }

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (!_active || collision.contactCount == 0) return;
            // 벽에 부딪히면 평면 속도를 법선으로 반사하고 높이를 잃는다.
            Vector2 normal = collision.GetContact(0).normal;
            _body.linearVelocity = Vector2.Reflect(_body.linearVelocity, normal) * 0.55f;
            _verticalSpeed *= 0.4f;
            _spin *= -0.6f;
        }

        void Update()
        {
            if (!_active) return;

            float dt = Time.deltaTime;
            _age += dt;
            _verticalSpeed -= Gravity * dt;
            _height += _verticalSpeed * dt;

            if (_height <= 0f)
            {
                _height = 0f;
                if (_bounces < MaxBounces && Mathf.Abs(_verticalSpeed) > 1.15f)
                {
                    _verticalSpeed = -_verticalSpeed * _bounce;
                    _bounces++;
                    _spin *= 0.55f;
                }
                else
                {
                    _verticalSpeed = 0f;
                    // 멈춘 뒤에는 회전이 빠르게 죽고 평면 속도도 멎는다.
                    _spin = Mathf.MoveTowards(_spin, 0f, 900f * dt);
                    _body.linearVelocity = Vector2.MoveTowards(_body.linearVelocity, Vector2.zero, 6f * dt);
                }
            }

            _spin = Mathf.Lerp(_spin, 0f, 1f - Mathf.Exp(-_spinDamping * dt));
            _visual.localPosition = new Vector3(0f, _height, 0f);
            _visual.Rotate(0f, 0f, _spin * dt);

            if (_shadow.enabled)
            {
                // 높이 1유닛에서 절반 크기, 절반 농도까지 줄인다.
                float t = Mathf.Clamp01(_height);
                float shadowScale = _scale * Mathf.Lerp(0.85f, 0.42f, t);
                _shadow.transform.localScale = new Vector3(shadowScale, shadowScale, 1f);
                _shadow.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.55f, 0.16f, t));
            }

            float remaining = _life - _age;
            Color c = _color;
            // 수명 후반에는 먼저 어둡게 정착하고, 마지막 0.3초에만 투명해진다.
            float settleStart = _life * (1f - SettleFraction);
            if (_age > settleStart)
            {
                float settle = Mathf.Clamp01((_age - settleStart) / Mathf.Max(0.001f, _life * SettleFraction));
                float dim = Mathf.Lerp(1f, 0.42f, settle);
                c = new Color(_color.r * dim, _color.g * dim, _color.b * dim, _color.a);
            }
            if (remaining < FadeTail) c.a *= Mathf.Clamp01(remaining / FadeTail);
            _renderer.color = c;

            if (_age >= _life) Release();
        }
    }
}
