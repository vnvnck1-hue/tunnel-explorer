using System.Collections.Generic;
using TunnelCrew.Sim;
using UnityEngine;

namespace TunnelCrew.Presentation
{
    /// <summary>
    /// 떨어진 재화를 그린다. M1 그레이박스라 절차 생성한 원형 스프라이트를 쓴다.
    /// 실제 아이콘(assets/ui/currency/)은 HUD 와 함께 M4 에서 붙인다.
    ///
    /// Sim 의 가짜 z 는 스프라이트를 위로 띄우는 데만 쓴다. 그림자는 바닥에 남는다.
    /// </summary>
    public sealed class LootView : MonoBehaviour
    {
        readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _shadowPool = new List<SpriteRenderer>();
        Sprite _dot;

        static readonly Color PulpColor = new Color(0.45f, 0.85f, 0.42f);
        static readonly Color BloomColor = new Color(0.95f, 0.45f, 0.78f);

        void Awake() => _dot = MakeCircle(24);

        public void Render(LootSystem loot)
        {
            var items = loot.Items;
            EnsurePool(items.Count);

            for (int i = 0; i < _pool.Count; i++)
            {
                bool active = i < items.Count;
                _pool[i].gameObject.SetActive(active);
                _shadowPool[i].gameObject.SetActive(active);
                if (!active) continue;

                var q = items[i];
                float x = (float)q.Position.X, y = (float)q.Position.Y, z = (float)q.Z;
                var ground = IsometricProjection.ToRender(new Vector2(x, y));

                _pool[i].transform.position = new Vector3(ground.x, ground.y + z, 0f);
                _pool[i].color = q.Kind == ResourceKind.Pulp ? PulpColor : BloomColor;
                float s = q.Kind == ResourceKind.Pulp ? 0.22f : 0.26f;
                _pool[i].transform.localScale = Vector3.one * s;

                // 높이에 따라 그림자가 작아지고 옅어진다
                float t = Mathf.Clamp01(1f - z * 1.6f);
                _shadowPool[i].transform.position = new Vector3(ground.x, ground.y, 0f);
                _shadowPool[i].transform.localScale = Vector3.one * (s * 0.8f * Mathf.Lerp(0.5f, 1f, t));
                _shadowPool[i].color = new Color(0f, 0f, 0f, 0.35f * t);
            }
        }

        void EnsurePool(int n)
        {
            while (_pool.Count < n)
            {
                var shadow = New($"LootShadow{_pool.Count}", 24);
                var dot = New($"Loot{_pool.Count}", 26);
                _shadowPool.Add(shadow);
                _pool.Add(dot);
            }
        }

        SpriteRenderer New(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _dot;
            sr.sortingOrder = order;
            return sr;
        }

        static Sprite MakeCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r, dy = y + 0.5f - r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                    float a = Mathf.Clamp01(1f - Mathf.SmoothStep(0.75f, 1f, d));
                    tex.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
