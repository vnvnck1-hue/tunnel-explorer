using UnityEngine;

namespace TunnelCrew.Presentation.Prototype
{
    /// <summary>
    /// 로드맵 4.7 — 탄흔·바닥 데칼 풀.
    /// 고정 용량 링 버퍼라 오래된 흔적부터 회수되고, 연속 사격 중에도 할당이 없다.
    /// </summary>
    public sealed class ModularGunnerDecals : MonoBehaviour
    {
        public enum Surface
        {
            Wall,
            Floor,
            Splat,
        }

        const int Capacity = 96;
        const float FadeTail = 1.4f;

        [SerializeField] Sprite[] _wallSprites;
        [SerializeField] Sprite[] _floorSprites;
        [SerializeField] Sprite[] _splatSprites;
        [SerializeField] Material _litMaterial;
        [SerializeField, Range(2f, 40f)] float _lifetime = 16f;

        public static ModularGunnerDecals Instance { get; private set; }

        Transform _root;
        SpriteRenderer[] _pool;
        float[] _spawnedAt;
        Color[] _tint;
        int _next;

        public void EditorAssign(Sprite[] wall, Sprite[] floor, Sprite[] splat, Material litMaterial)
        {
            _wallSprites = wall;
            _floorSprites = floor;
            _splatSprites = splat;
            _litMaterial = litMaterial;
        }

        void Awake()
        {
            Instance = this;
            _root = new GameObject("Decal Pool").transform;
            _root.SetParent(transform, false);
            _pool = new SpriteRenderer[Capacity];
            _spawnedAt = new float[Capacity];
            _tint = new Color[Capacity];
            for (int i = 0; i < Capacity; i++)
            {
                var go = new GameObject("Decal");
                go.transform.SetParent(_root, false);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sharedMaterial = _litMaterial;
                renderer.enabled = false;
                _pool[i] = renderer;
                _spawnedAt[i] = float.NegativeInfinity;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        Sprite[] SpritesFor(Surface surface) => surface switch
        {
            Surface.Wall => _wallSprites,
            Surface.Floor => _floorSprites,
            _ => _splatSprites,
        };

        /// <summary>
        /// 흔적 하나를 남긴다. 벽 흔적은 벽 정면 위에, 바닥 흔적은 캐릭터 아래에 깔린다.
        /// </summary>
        public void Spawn(Surface surface, Vector2 position, Color tint, float scale = 1f)
        {
            Sprite[] sprites = SpritesFor(surface);
            if (sprites == null || sprites.Length == 0 || _pool == null) return;

            // 가장 오래된 슬롯부터 재사용한다. 링 버퍼라 탐색이 없다.
            int index = _next;
            _next = (_next + 1) % Capacity;

            SpriteRenderer renderer = _pool[index];
            renderer.sprite = sprites[Random.Range(0, sprites.Length)];
            renderer.enabled = true;
            renderer.color = tint;
            renderer.flipX = Random.value < 0.5f;
            renderer.transform.SetPositionAndRotation(
                position,
                Quaternion.Euler(0f, 0f, surface == Surface.Wall ? 0f : Random.Range(0f, 360f)));
            renderer.transform.localScale = Vector3.one * scale;
            renderer.sortingOrder = surface == Surface.Wall
                ? ModularGunnerDepthSorter.OrderFor(position.y) + 1
                : 4;
            _spawnedAt[index] = Time.time;
            _tint[index] = tint;
        }

        void Update()
        {
            if (_pool == null) return;
            float now = Time.time;
            for (int i = 0; i < _pool.Length; i++)
            {
                SpriteRenderer renderer = _pool[i];
                if (!renderer.enabled) continue;
                float age = now - _spawnedAt[i];
                if (age >= _lifetime)
                {
                    renderer.enabled = false;
                    continue;
                }
                // 수명 끝에서만 서서히 지운다. 그 전에는 흔적이 또렷하게 남는다.
                float remaining = _lifetime - age;
                if (remaining >= FadeTail) continue;
                Color c = _tint[i];
                c.a *= remaining / FadeTail;
                renderer.color = c;
            }
        }
    }
}
