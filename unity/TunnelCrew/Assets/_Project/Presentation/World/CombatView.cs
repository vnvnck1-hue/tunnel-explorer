using System.Collections.Generic;
using TunnelCrew.Sim;
using UnityEngine;

namespace TunnelCrew.Presentation
{
    /// <summary>
    /// 투사체 · 적탄 · 설치물 · 데미지 텍스트. M3 그레이박스라 절차 생성 스프라이트를 쓴다.
    /// 트레일·머즐·폭발 파티클은 Shuriken 프리셋이 들어올 때 이 클래스에서 이벤트로 연결한다.
    /// </summary>
    public sealed class CombatView : MonoBehaviour
    {
        readonly List<SpriteRenderer> _projPool = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _shotPool = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _installPool = new List<SpriteRenderer>();
        readonly List<DamageText> _texts = new List<DamageText>();
        readonly Stack<DamageText> _textPool = new Stack<DamageText>();
        Sprite _dot, _square;

        sealed class DamageText { public GameObject Go; public TextMesh Mesh; public Vector3 Vel; public float Life, MaxLife; }

        static readonly Dictionary<string, Color> ProjColor = new Dictionary<string, Color>
        {
            ["standard"] = new Color(1f, 0.93f, 0.7f), ["multi"] = new Color(1f, 0.85f, 0.6f),
            ["pierce"] = new Color(0.7f, 0.95f, 1f), ["ricochet"] = new Color(0.9f, 0.8f, 1f),
            ["explosive"] = new Color(1f, 0.55f, 0.3f), ["rain"] = new Color(1f, 0.5f, 0.25f),
            ["laser"] = new Color(0.6f, 1f, 0.95f), ["support"] = new Color(0.5f, 0.92f, 0.82f),
        };

        void Awake() { _dot = MakeCircle(16); _square = MakeSquare(); }

        public void Render(TunnelSim sim, float dt)
        {
            RenderList(_projPool, sim.Projectiles.Projectiles.Count, 40, i =>
            {
                var p = sim.Projectiles.Projectiles[i];
                var sr = _projPool[i];
                sr.transform.position = new Vector3((float)p.Position.X, (float)p.Position.Y, 0);
                sr.color = ProjColor.TryGetValue(p.VisualId, out var c) ? c : Color.white;
                float len = p.Laser ? 0.55f : 0.22f, wid = p.Laser ? 0.08f : 0.12f;
                sr.transform.localScale = new Vector3(len, wid, 1);
                sr.transform.rotation = Quaternion.Euler(0, 0, (float)p.Velocity.Angle * Mathf.Rad2Deg);
                sr.sprite = _square;
            });

            RenderList(_shotPool, sim.Enemies.Shots.Count, 39, i =>
            {
                var s = sim.Enemies.Shots[i];
                var sr = _shotPool[i];
                sr.transform.position = new Vector3((float)s.Position.X, (float)s.Position.Y, 0);
                sr.color = new Color(0.55f, 0.95f, 0.5f);
                sr.transform.localScale = Vector3.one * (float)(s.Radius * 2.2);
                sr.sprite = _dot;
            });

            // 설치물: 노드(청록 원) · 센트리(보라 사각) · 파쇄탄(주황 점) · 플레어(노랑)
            var roles = sim.Roles;
            int n = roles.Nodes.Count + roles.Turrets.Count + roles.Breakers.Count + roles.Flares.Count;
            RenderList(_installPool, n, 28, i =>
            {
                var sr = _installPool[i];
                int k = i;
                if (k < roles.Nodes.Count) { var o = roles.Nodes[k]; Set(sr, o.Position, _dot, new Color(0.45f, 0.92f, 0.85f), 0.7f); return; }
                k -= roles.Nodes.Count;
                if (k < roles.Turrets.Count) { var o = roles.Turrets[k]; Set(sr, o.Position, _square, o.Powered ? new Color(0.78f, 0.63f, 1f) : new Color(0.4f, 0.35f, 0.5f), 0.55f); sr.transform.rotation = Quaternion.Euler(0, 0, (float)o.Aim * Mathf.Rad2Deg); return; }
                k -= roles.Turrets.Count;
                if (k < roles.Breakers.Count)
                {
                    var o = roles.Breakers[k];
                    float t = o.Stuck ? 1f : 1f - (float)(o.Travel / o.TravelMax);
                    var pos = o.Stuck ? o.Target : Vec2Lerp(o.Start, o.Target, t);
                    float pulse = o.Stuck ? 0.75f + 0.25f * Mathf.Sin(Time.time * 18f) : 1f;
                    Set(sr, pos, _dot, new Color(1f, 0.55f, 0.3f) * pulse, 0.3f); return;
                }
                k -= roles.Breakers.Count;
                var f = roles.Flares[k];
                Set(sr, f.Position, _dot, new Color(1f, 0.92f, 0.5f, 0.85f), 0.28f);
            });

            // 데미지 텍스트
            for (int i = _texts.Count - 1; i >= 0; i--)
            {
                var t = _texts[i];
                t.Life -= dt;
                t.Go.transform.position += t.Vel * dt;
                t.Vel *= Mathf.Exp(-3.2f * dt);
                var c = t.Mesh.color; c.a = Mathf.Clamp01(t.Life / (t.MaxLife * 0.4f)); t.Mesh.color = c;
                if (t.Life <= 0) { t.Go.SetActive(false); _textPool.Push(t); _texts.RemoveAt(i); }
            }
        }

        static Vec2 Vec2Lerp(Vec2 a, Vec2 b, float t) => a + (b - a) * t;

        void Set(SpriteRenderer sr, Vec2 pos, Sprite sp, Color c, float size)
        {
            sr.transform.position = new Vector3((float)pos.X, (float)pos.Y, 0);
            sr.transform.rotation = Quaternion.identity;
            sr.sprite = sp; sr.color = c; sr.transform.localScale = Vector3.one * size;
        }

        /// <summary>원본 J.dmg / J.text — 위로 뜨며 사라지는 숫자.</summary>
        public void Text(Vec2 at, string text, Color color, float size = 0.32f)
        {
            DamageText t = _textPool.Count > 0 ? _textPool.Pop() : New();
            t.Go.SetActive(true);
            t.Go.transform.position = new Vector3((float)at.X, (float)at.Y + 0.4f, -0.1f);
            t.Mesh.text = text; t.Mesh.color = color; t.Mesh.characterSize = size * 0.35f;
            t.Vel = new Vector3(Random.Range(-0.4f, 0.4f), 1.8f, 0);
            t.Life = t.MaxLife = 0.85f;
            _texts.Add(t);

            DamageText New()
            {
                var go = new GameObject("DmgText"); go.transform.SetParent(transform, false);
                var tm = go.AddComponent<TextMesh>();
                tm.anchor = TextAnchor.MiddleCenter; tm.fontSize = 48; tm.fontStyle = FontStyle.Bold;
                go.GetComponent<MeshRenderer>().sortingOrder = 60;
                return new DamageText { Go = go, Mesh = tm };
            }
        }

        void RenderList(List<SpriteRenderer> pool, int count, int order, System.Action<int> draw)
        {
            while (pool.Count < count)
            {
                var go = new GameObject("fx"); go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>(); sr.sortingOrder = order;
                pool.Add(sr);
            }
            for (int i = 0; i < pool.Count; i++)
            {
                bool on = i < count;
                pool[i].gameObject.SetActive(on);
                if (on) draw(i);
            }
        }

        static Sprite MakeSquare()
        {
            var t = new Texture2D(2, 2); t.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white }); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2);
        }
        static Sprite MakeCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float rad = size * 0.5f;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - rad, dy = y + 0.5f - rad;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / rad;
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(1f - Mathf.SmoothStep(0.75f, 1f, d))));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
