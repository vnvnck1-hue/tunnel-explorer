using System.Collections.Generic;
using TunnelCrew.Data;
using TunnelCrew.Sim;
using UnityEngine;

namespace TunnelCrew.Presentation
{
    /// <summary>
    /// 적 스프라이트 풀. 원본 <c>drawEnemies()</c>(7047~7093) 와 <c>enemySpriteFrame()</c>(2144).
    ///
    /// 프레임 규칙: blink 8~11 → idle 6~7 → walk 0~5 (광란종 질주 12~17).
    /// 단일 방향이라 반전하지 않는다. 크기는 반지름의 3.15배(광란종 3.55배).
    /// 피격 플래시는 스프라이트 색을 흰색으로 섞는다 (M3 셰이더 프로퍼티 전 단계).
    /// </summary>
    public sealed class EnemyView : MonoBehaviour
    {
        MonsterSheetAsset _sheets;
        readonly Dictionary<EnemyState, Item> _items = new Dictionary<EnemyState, Item>();
        readonly Stack<Item> _pool = new Stack<Item>();
        readonly List<EnemyState> _gone = new List<EnemyState>();

        sealed class Item
        {
            public GameObject Go;
            public SpriteRenderer Body, Shadow, HpBg, HpBar;
            public LineRenderer Windup;
        }

        static readonly Color ShadowColor = new Color(0, 0, 0, 0.32f);
        Sprite _dot;

        public void Bind(MonsterSheetAsset sheets)
        {
            _sheets = sheets;
            _dot = MakeCircle(16);
        }

        public void Render(IReadOnlyList<EnemyState> enemies, float dt)
        {
            foreach (var e in enemies)
            {
                if (!_items.TryGetValue(e, out var it)) _items[e] = it = Rent();
                Draw(e, it);
            }

            _gone.Clear();
            foreach (var kv in _items) if (!kv.Key.Alive) _gone.Add(kv.Key);
            foreach (var e in _gone) { Return(_items[e]); _items.Remove(e); }
        }

        void Draw(EnemyState e, Item it)
        {
            float x = (float)e.Position.X, y = (float)e.Position.Y;
            float r = (float)e.Radius;

            // 원본: bob 으로 위아래 살짝, 도약 중 lift
            float bob = Mathf.Sin((float)e.Bob) * r * 0.06f;
            float lift = e.IsJumping ? Mathf.Sin((float)(1 - e.JumpTime / System.Math.Max(1e-6, e.JumpDuration)) * Mathf.PI) * r * 0.9f : 0f;

            it.Go.transform.position = new Vector3(x, y, 0f);
            it.Body.transform.localPosition = new Vector3(0, bob + lift, 0);

            var frames = _sheets != null ? _sheets.FramesFor(KindId(e.Kind)) : null;
            if (frames != null && frames.Length >= 12)
            {
                bool moving = e.Velocity.Length > 0.15 || e.IsJumping;
                int idx;
                if (e.BlinkTime > 0) idx = 8 + Mathf.Clamp((int)((0.24 - e.BlinkTime) / 0.06), 0, 3);
                else if (!moving) idx = 6 + (((int)(e.AnimTime * 2)) & 1);
                else
                {
                    bool sprint = e.IsApex && e.Ai == EnemyAi.Chase && frames.Length >= 16;
                    idx = sprint ? 12 + ((int)(e.AnimTime * 12)) % 4 : ((int)(e.AnimTime * 10)) % 6;
                }
                it.Body.sprite = frames[Mathf.Clamp(idx, 0, frames.Length - 1)];

                // 원본 크기 e.r*3.15 (apex 3.55) — 스프라이트 원본 높이 대비 스케일
                float targetH = r * (e.IsApex ? 3.55f : 3.15f) * 2f;
                float spriteH = it.Body.sprite.bounds.size.y;
                float s = spriteH > 0 ? targetH / spriteH : 1f;
                it.Body.transform.localScale = new Vector3(s, s, 1f);
            }
            else
            {
                // 시트가 없으면 원으로 대체
                it.Body.sprite = _dot;
                it.Body.transform.localScale = Vector3.one * (r * 2f);
            }

            // 피격 플래시 + 상태 색
            Color tint = e.IsApex ? new Color(1f, 0.72f, 0.82f) : Color.white;
            if (e.FrozenTime > 0) tint = new Color(0.6f, 0.85f, 1f);
            if (e.Hurt > 0) tint = Color.Lerp(tint, Color.white, Mathf.Clamp01((float)e.Hurt / 0.18f) * 0.85f);
            it.Body.color = tint;

            it.Shadow.transform.localScale = new Vector3(r * 1.9f, r * 1.1f, 1f);
            it.Shadow.color = new Color(0, 0, 0, 0.32f * (1f - Mathf.Clamp01(lift / (r * 0.9f)) * 0.5f));

            // 체력바 — 광란종·보스 제외 (원본 규칙)
            bool showHp = !e.IsApex && !e.IsBoss && e.Hp < e.HpMax;
            it.HpBg.enabled = it.HpBar.enabled = showHp;
            if (showHp)
            {
                float w = r * 2.2f, h = 0.09f;
                float frac = Mathf.Clamp01((float)(e.Hp / e.HpMax));
                it.HpBg.transform.localPosition = new Vector3(0, r * 1.75f, 0);
                it.HpBg.transform.localScale = new Vector3(w, h, 1);
                it.HpBar.transform.localPosition = new Vector3(-w * 0.5f * (1 - frac), r * 1.75f, 0);
                it.HpBar.transform.localScale = new Vector3(w * frac, h, 1);
            }

            // 선딜 텔레그래프 — 원본 drawEnemyWindup: 근접은 부채꼴, 원거리는 조준선
            bool windup = e.Attack == AttackPhase.Windup;
            it.Windup.enabled = windup;
            if (windup)
            {
                float total = Mathf.Max(0.05f, (float)e.AttackWindupTotal);
                float p = Mathf.Clamp01(1f - (float)e.AttackTimer / total);
                var d = new Vector2((float)e.AttackDir.X, (float)e.AttackDir.Y);
                float inner = r * 0.92f;
                float outer = inner + r * (e.IsRanged ? 2.6f : 1.05f) * (0.35f + p * 0.65f);
                var col = e.IsRanged ? new Color(0.55f, 0.89f, 0.56f) : new Color(1f, 0.6f, 0.42f);
                col.a = 0.3f + p * 0.5f;
                it.Windup.startColor = it.Windup.endColor = col;
                it.Windup.startWidth = it.Windup.endWidth = e.IsRanged ? 0.06f : r * 0.9f;
                it.Windup.SetPosition(0, new Vector3(x + d.x * inner, y + d.y * inner, -0.05f));
                it.Windup.SetPosition(1, new Vector3(x + d.x * outer, y + d.y * outer, -0.05f));
            }
        }

        static string KindId(EnemyKind k) => k switch
        {
            EnemyKind.Crawler => "crawler", EnemyKind.Spitter => "spitter", _ => "broodBeast",
        };

        Item Rent()
        {
            if (_pool.Count > 0) { var p = _pool.Pop(); p.Go.SetActive(true); return p; }

            var go = new GameObject("Enemy");
            go.transform.SetParent(transform, false);

            SpriteRenderer Make(string n, int order, Sprite sp, Color c)
            {
                var g = new GameObject(n); g.transform.SetParent(go.transform, false);
                var sr = g.AddComponent<SpriteRenderer>(); sr.sprite = sp; sr.color = c; sr.sortingOrder = order;
                return sr;
            }
            var it = new Item { Go = go };
            it.Shadow = Make("Shadow", 27, _dot, ShadowColor);
            it.Body = Make("Body", 29, null, Color.white);
            it.HpBg = Make("HpBg", 31, MakeSquare(), new Color(0, 0, 0, 0.6f));
            it.HpBar = Make("HpBar", 32, MakeSquare(), new Color(0.95f, 0.35f, 0.35f));
            var lg = new GameObject("Windup"); lg.transform.SetParent(go.transform, false);
            it.Windup = lg.AddComponent<LineRenderer>();
            it.Windup.positionCount = 2; it.Windup.useWorldSpace = true;
            it.Windup.material = new Material(Shader.Find("Sprites/Default"));
            it.Windup.sortingOrder = 28;
            return it;
        }
        void Return(Item it) { it.Go.SetActive(false); _pool.Push(it); }

        static Sprite _square;
        static Sprite MakeSquare()
        {
            if (_square != null) return _square;
            var t = new Texture2D(2, 2); t.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white }); t.Apply();
            return _square = Sprite.Create(t, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2);
        }
        static Sprite MakeCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float rad = size * 0.5f;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - rad, dy = y + 0.5f - rad;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / rad;
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(1f - Mathf.SmoothStep(0.8f, 1f, d))));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
