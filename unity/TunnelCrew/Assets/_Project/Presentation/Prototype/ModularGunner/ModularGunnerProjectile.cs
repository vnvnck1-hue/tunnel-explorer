using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TunnelCrew.Presentation.Prototype
{
    /// <summary>
    /// 로드맵 4.6 — 탄종별 형태·팔레트 분리와 속도에 비례하는 트레이서.
    /// 탄두 중심광(Light2D)과 외곽선(트레이서 스프라이트)을 분리해 밝은 바닥과 어두운 바닥
    /// 양쪽에서 읽히게 한다.
    /// </summary>
    public sealed class ModularGunnerProjectile : MonoBehaviour
    {
        public enum Kind
        {
            Player,
            Enemy,
            Piercing,
            Explosive,
        }

        /// <summary>탄종 팔레트. 형태는 트레이서 길이·굵기로, 색은 여기서 나뉜다.</summary>
        readonly struct Palette
        {
            public readonly Color Core;
            public readonly Color Trail;
            public readonly float Width;
            public readonly float LengthScale;

            public Palette(Color core, Color trail, float width, float lengthScale)
            {
                Core = core;
                Trail = trail;
                Width = width;
                LengthScale = lengthScale;
            }
        }

        // 기준 속도. 이보다 빠르면 트레이서가 길어지고 느리면 짧아진다.
        const float ReferenceSpeed = 42f;

        [SerializeField] float _speed = 42f;
        [SerializeField] float _lifetime = 0.42f;
        [SerializeField] Rigidbody2D _body;
        [SerializeField] SpriteRenderer _renderer;
        [SerializeField] Kind _kind = Kind.Player;

        ModularGunnerController _owner;
        Light2D _coreLight;
        float _despawnAt;

        static Palette PaletteFor(Kind kind) => kind switch
        {
            // 플레이어탄: 흰 심 + 앰버 잔상. 적탄과 색상환에서 확실히 떨어뜨린다.
            Kind.Player => new Palette(new Color(1f, 0.96f, 0.78f), new Color(1f, 0.72f, 0.16f), 1f, 1f),
            // 적탄: 시안. 어두운 암반 위에서 플레이어탄과 헷갈리지 않는다.
            Kind.Enemy => new Palette(new Color(0.82f, 1f, 1f), new Color(0.24f, 0.78f, 1f), 1.15f, 0.8f),
            // 관통탄: 가늘고 길다.
            Kind.Piercing => new Palette(new Color(1f, 0.9f, 1f), new Color(0.78f, 0.42f, 1f), 0.7f, 1.6f),
            // 폭발탄: 굵고 짧다.
            _ => new Palette(new Color(1f, 0.88f, 0.62f), new Color(1f, 0.36f, 0.1f), 1.8f, 0.55f),
        };

        void Awake()
        {
            ModularGunnerPhysicsLayers.Assign(gameObject, ModularGunnerPhysicsLayers.Projectile);
        }

        public void EditorAssign(Rigidbody2D body, SpriteRenderer renderer)
        {
            _body = body;
            _renderer = renderer;
        }

        public void Launch(Vector2 direction, ModularGunnerController owner) => Launch(direction, owner, Kind.Player);

        public void Launch(Vector2 direction, ModularGunnerController owner, Kind kind)
        {
            ModularGunnerPhysicsLayers.Assign(gameObject, ModularGunnerPhysicsLayers.Projectile);
            _owner = owner;
            _kind = kind;
            _despawnAt = Time.time + _lifetime;
            if (_body == null) _body = GetComponent<Rigidbody2D>();
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            _body.linearVelocity = direction.normalized * _speed;
            // 잔상이 발사점과 탄착점을 잇도록 진행 방향과 정확히 맞춘다.
            transform.right = direction;

            Palette palette = PaletteFor(kind);
            if (_renderer != null)
            {
                _renderer.color = palette.Trail;
                // 속도에 비례하는 짧은 트레이서. 길이만 늘이고 굵기는 탄종이 정한다.
                float length = Mathf.Clamp(_speed / ReferenceSpeed, 0.45f, 2.2f) * palette.LengthScale;
                _renderer.transform.localScale = new Vector3(length, palette.Width, 1f);
            }

            // 탄두 중심광은 외곽선과 분리된 밝은 점으로 남는다.
            if (_coreLight == null)
            {
                _coreLight = gameObject.AddComponent<Light2D>();
                _coreLight.lightType = Light2D.LightType.Point;
                _coreLight.pointLightInnerAngle = 360f;
                _coreLight.pointLightOuterAngle = 360f;
            }
            _coreLight.color = palette.Core;
            _coreLight.intensity = ModularGunnerLighting.ProjectileIntensity;
            _coreLight.pointLightInnerRadius = 0.08f;
            _coreLight.pointLightOuterRadius = 1.05f;
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
                Vector2 direction = _body != null ? _body.linearVelocity.normalized : (Vector2)transform.right;
                enemy.TakeHit(direction);
                // 관통탄은 적을 지나간다.
                if (_kind != Kind.Piercing) Destroy(gameObject);
                return;
            }

            if (!other.isTrigger)
            {
                Vector2 direction = _body != null ? _body.linearVelocity.normalized : (Vector2)transform.right;
                ModularGunnerEffects.Instance?.WallImpact(transform.position, direction);
                Destroy(gameObject);
            }
        }
    }
}
