using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TunnelCrew.Presentation.Prototype
{
    /// <summary>
    /// 로드맵 4.4 — 전경 벽 오클루전.
    ///
    /// 벽 상단 캡은 타일맵 한 장이라 셀마다 캐릭터와 앞뒤를 바꿀 수 없다.
    /// 그래서 캐릭터를 실제로 가리는 셀에만 같은 캡 스프라이트를 **반투명 컷어웨이 오버레이**로
    /// 덧그린다. 오버레이의 소팅은 캐릭터와 같은 발 위치 규칙(<see cref="ModularGunnerDepthSorter"/>)을
    /// 쓰므로 소팅 경계와 콜라이더 경계가 어긋나지 않는다.
    ///
    /// 벽 정면(16×24)은 콜라이더가 그 영역 전체를 막아 캐릭터가 절벽 면 안으로 들어갈 수 없다.
    /// 절벽 아래에 선 캐릭터는 정면보다 앞에 그려지는 것이 맞으므로 정면은 건드리지 않는다.
    /// </summary>
    [DefaultExecutionOrder(950)]
    public sealed class ModularGunnerWallOcclusion : MonoBehaviour
    {
        const int OverlayCapacity = 64;
        const float ActorHalfWidth = 0.45f;
        const float ActorHeight = 1.6f;
        const float CutawayAlpha = 0.30f;
        const float EnemyScanInterval = 0.25f;
        // 발끝만 걸친 셀까지 실루엣을 띄우면 벽을 스칠 때마다 적이 주황색으로 번쩍인다.
        // 실제로 몸이 가려질 때만 켜지도록 세로 겹침 높이에 하한을 둔다.
        const float SilhouetteMinCoverage = 0.5f;

        [SerializeField] Tilemap _wallCaps;
        [SerializeField] Material _unlitMaterial;
        [SerializeField, Range(0f, 1f)] float _silhouetteAlpha = 0.8f;

        SpriteRenderer[] _overlays;
        int _usedOverlays;
        readonly HashSet<Vector3Int> _marked = new();
        readonly Dictionary<ModularGunnerEnemy, SpriteRenderer> _silhouettes = new();
        readonly List<ModularGunnerEnemy> _stale = new();

        ModularGunnerController _player;
        ModularGunnerEnemy[] _enemies = System.Array.Empty<ModularGunnerEnemy>();
        float _nextEnemyScan;

        public void EditorAssign(Tilemap wallCaps, Material unlitMaterial)
        {
            _wallCaps = wallCaps;
            _unlitMaterial = unlitMaterial;
        }

        void Awake()
        {
            var root = new GameObject("Cutaway Overlays").transform;
            root.SetParent(transform, false);
            _overlays = new SpriteRenderer[OverlayCapacity];
            for (int i = 0; i < OverlayCapacity; i++)
            {
                var go = new GameObject("Cutaway");
                go.transform.SetParent(root, false);
                _overlays[i] = go.AddComponent<SpriteRenderer>();
                _overlays[i].sharedMaterial = _unlitMaterial;
                _overlays[i].enabled = false;
            }
        }

        void LateUpdate()
        {
            if (_wallCaps == null) return;
            if (_player == null) _player = FindFirstObjectByType<ModularGunnerController>();
            if (Time.time >= _nextEnemyScan)
            {
                _enemies = FindObjectsByType<ModularGunnerEnemy>(FindObjectsSortMode.None);
                _nextEnemyScan = Time.time + EnemyScanInterval;
            }

            _usedOverlays = 0;
            _marked.Clear();

            if (_player != null) Process(_player.transform.position, null);
            for (int i = 0; i < _enemies.Length; i++)
            {
                ModularGunnerEnemy enemy = _enemies[i];
                if (enemy != null) Process(enemy.transform.position, enemy);
            }

            for (int i = _usedOverlays; i < _overlays.Length; i++)
            {
                if (!_overlays[i].enabled) break;
                _overlays[i].enabled = false;
            }

            PruneSilhouettes();
        }

        /// <summary>
        /// 배우를 가리는 벽 셀을 찾는다. 캡이 배우 앞에 오는 조건은 캡 셀의 Y 가 배우의 Y 보다
        /// 작을 때이고, 겹치는 조건은 캡 셀 [cy, cy+1] 이 배우 [ay, ay+ActorHeight] 와 만날 때다.
        /// </summary>
        void Process(Vector2 actor, ModularGunnerEnemy enemy)
        {
            float coverage = 0f;
            int minX = Mathf.FloorToInt(actor.x - ActorHalfWidth);
            int maxX = Mathf.FloorToInt(actor.x + ActorHalfWidth);
            int minY = Mathf.FloorToInt(actor.y - 1f);
            int maxY = Mathf.CeilToInt(actor.y);

            for (int cy = minY; cy <= maxY; cy++)
            {
                if (cy >= actor.y) continue;            // 배우 뒤에 그려진다.
                float overlap = cy + 1f - actor.y;      // 배우를 덮는 세로 높이.
                if (overlap <= 0f) continue;
                for (int cx = minX; cx <= maxX; cx++)
                {
                    var cell = new Vector3Int(cx, cy, 0);
                    Sprite sprite = _wallCaps.GetSprite(cell);
                    if (sprite == null) continue;
                    coverage = Mathf.Max(coverage, overlap);
                    if (!_marked.Add(cell)) continue;
                    PlaceOverlay(cell, sprite);
                }
            }

            if (enemy == null) return;
            if (coverage >= SilhouetteMinCoverage) ShowSilhouette(enemy);
            else HideSilhouette(enemy);
        }

        void PlaceOverlay(Vector3Int cell, Sprite sprite)
        {
            if (_usedOverlays >= _overlays.Length) return;
            SpriteRenderer renderer = _overlays[_usedOverlays++];
            renderer.sprite = sprite;
            renderer.enabled = true;
            renderer.transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
            renderer.sortingOrder = ModularGunnerDepthSorter.OrderFor(cell.y);
            renderer.color = new Color(1f, 1f, 1f, CutawayAlpha);
        }

        void ShowSilhouette(ModularGunnerEnemy enemy)
        {
            var source = enemy.GetComponent<SpriteRenderer>();
            if (source == null) return;

            if (!_silhouettes.TryGetValue(enemy, out SpriteRenderer silhouette) || silhouette == null)
            {
                var go = new GameObject("Occlusion Silhouette");
                go.transform.SetParent(enemy.transform, false);
                silhouette = go.AddComponent<SpriteRenderer>();
                silhouette.sharedMaterial = _unlitMaterial;
                _silhouettes[enemy] = silhouette;
            }

            silhouette.enabled = true;
            silhouette.sprite = source.sprite;
            silhouette.flipX = source.flipX;
            // 컷어웨이 오버레이보다 한 단계 앞. 벽 뒤 적의 위치만 알려 주고 형태는 뭉갠다.
            silhouette.sortingOrder = ModularGunnerDepthSorter.OrderFor(enemy.transform.position.y) + 60;
            silhouette.color = new Color(1f, 0.6f, 0.2f, _silhouetteAlpha);
        }

        void HideSilhouette(ModularGunnerEnemy enemy)
        {
            if (_silhouettes.TryGetValue(enemy, out SpriteRenderer silhouette) && silhouette != null)
                silhouette.enabled = false;
        }

        void PruneSilhouettes()
        {
            _stale.Clear();
            foreach (var pair in _silhouettes)
                if (pair.Key == null) _stale.Add(pair.Key);
            for (int i = 0; i < _stale.Count; i++) _silhouettes.Remove(_stale[i]);
        }
    }
}
