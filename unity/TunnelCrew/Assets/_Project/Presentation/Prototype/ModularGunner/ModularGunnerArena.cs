using UnityEngine;

namespace TunnelCrew.Presentation.Prototype
{
    public sealed class ModularGunnerArena : MonoBehaviour
    {
        [SerializeField] ModularGunnerEnemy _enemyPrefab;
        [SerializeField] Transform _player;
        [SerializeField] Vector2 _min = new(-17.5f, -9.5f);
        [SerializeField] Vector2 _max = new(17.5f, 9.5f);
        [SerializeField, Range(0.15f, 5f)] float _spawnInterval = 0.8f;
        [SerializeField, Range(1, 80)] int _maxEnemies = 28;
        [SerializeField] bool _continuousSpawn;
        float _nextSpawn;

        public bool ContinuousSpawn => _continuousSpawn;
        public int AliveEnemies => FindObjectsByType<ModularGunnerEnemy>(FindObjectsSortMode.None).Length;

        void Awake()
        {
            ModularGunnerPhysicsLayers.ConfigureCollisionMatrix();
            var colliders = FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider != null && collider.attachedRigidbody == null && !collider.isTrigger)
                    ModularGunnerPhysicsLayers.Assign(collider.gameObject, ModularGunnerPhysicsLayers.World);
            }
        }

        public void EditorAssign(ModularGunnerEnemy enemyPrefab, Transform player, Vector2 min, Vector2 max)
        {
            _enemyPrefab = enemyPrefab;
            _player = player;
            _min = min;
            _max = max;
        }

        public void ToggleContinuousSpawn()
        {
            _continuousSpawn = !_continuousSpawn;
            _nextSpawn = Time.time;
        }

        public bool SpawnOne()
        {
            if (_enemyPrefab == null || AliveEnemies >= _maxEnemies) return false;
            for (int attempt = 0; attempt < 32; attempt++)
            {
                Vector2 point = new(Random.Range(_min.x, _max.x), Random.Range(_min.y, _max.y));
                if (_player != null && Vector2.Distance(point, _player.position) < 5f) continue;
                if (Physics2D.OverlapCircle(point, 0.7f, ModularGunnerPhysicsLayers.SpawnBlockingMask) != null) continue;
                Instantiate(_enemyPrefab, point, Quaternion.identity);
                return true;
            }
            return false;
        }

        void Update()
        {
            if (!_continuousSpawn || Time.time < _nextSpawn) return;
            _nextSpawn = Time.time + _spawnInterval;
            SpawnOne();
        }
    }
}
