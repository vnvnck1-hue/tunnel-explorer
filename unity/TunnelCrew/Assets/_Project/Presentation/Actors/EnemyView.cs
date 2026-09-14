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
            /// <summary>보스 드래곤 애니 상태 (원본 e.bossAnimKey / bossAnimT).</summary>
            public string AnimKey; public float AnimT;
        }
        /// <summary>격파된 보스 — 원본 bossDying: 몸을 남겨 death 24프레임(2.4s)을 끝까지 보여준다.</summary>
        sealed class Dying { public Item It; public float T, R; public bool Flip; }
        readonly List<Dying> _dying = new List<Dying>();

        /// <summary>보스 드래곤 애니 선택 재료 (fireBreath 중인가, 배속). RunBootstrap 이 BossSystem 에서 연결한다.</summary>
        public System.Func<EnemyState, (bool fire, double rate)> BossAnimInfo;
        const float DragonFps = 10f;   // BOSS_DRAGON_ANIMS 공통

        static readonly Color ShadowColor = new Color(0.055f, 0.012f, 0.075f, 0.42f);
        Sprite _dot;

        /// <summary>보스 시선 방향(+1 오른쪽). RunBootstrap 이 BossSystem 에서 연결한다.</summary>
        public System.Func<EnemyState, int> BossFacing;

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
                Draw(e, it, dt);
            }

            _gone.Clear();
            foreach (var kv in _items) if (!kv.Key.Alive) _gone.Add(kv.Key);
            foreach (var e in _gone)
            {
                var it = _items[e]; _items.Remove(e);
                var death = _sheets?.FramesFor("dragon_death");
                if (e.IsBoss && death != null && death.Length > 0)
                {   // 보스는 바로 지우지 않고 death 애니를 끝까지 (원본 bossDying · bossFade)
                    it.HpBg.enabled = it.HpBar.enabled = it.Windup.enabled = false;
                    _dying.Add(new Dying { It = it, T = 0, R = (float)e.Radius, Flip = it.Body.flipX });
                }
                else Return(it);
            }

            for (int i = _dying.Count - 1; i >= 0; i--)
            {
                var d = _dying[i]; d.T += dt;
                var death = _sheets.FramesFor("dragon_death");
                int f = Mathf.Min(death.Length - 1, (int)(d.T * DragonFps));
                d.It.Body.sprite = death[f];
                float targetH = d.R * 3.3f, sh = d.It.Body.sprite.bounds.size.y;
                d.It.Body.transform.localScale = Vector3.one * (sh > 0 ? targetH / sh : 1f);
                d.It.Body.color = new Color(1f, .62f, .68f, Mathf.Clamp01(1.4f - d.T * .45f));
                d.It.Shadow.color = new Color(0, 0, 0, .32f * Mathf.Clamp01(1f - d.T / 2.4f));
                if (d.T >= death.Length / DragonFps) { Return(d.It); _dying.RemoveAt(i); }
            }
        }

        /// <summary>층 전환·런 재시작 때 남아 있던 시체를 정리한다.</summary>
        public void ClearDying() { foreach (var d in _dying) Return(d.It); _dying.Clear(); }

        /// <summary>보스 드래곤 — 원본 bossDragonAnimation: fireBreath(예고 중) > moving ? walking : idle. 키가 바뀌면 t=0, 배속 e.bossAnimRate.</summary>
        bool DrawDragon(EnemyState e, Item it, float dt, bool moving, float r)
        {
            var idle = _sheets?.FramesFor("dragon_idle");
            if (idle == null || idle.Length == 0) return false;
            var info = BossAnimInfo != null ? BossAnimInfo(e) : (false, 1.0);
            string key = info.Item1 ? "dragon_fireBreath" : moving ? "dragon_walking" : "dragon_idle";
            var frames = _sheets.FramesFor(key) ?? idle;
            if (it.AnimKey != key) { it.AnimKey = key; it.AnimT = 0; }
            it.AnimT += dt * (float)info.Item2;
            bool loop = key != "dragon_fireBreath";
            int raw = (int)(it.AnimT * DragonFps);
            int f = loop ? raw % frames.Length : Mathf.Min(frames.Length - 1, raw);
            it.Body.sprite = frames[f];
            float targetH = r * 3.3f, sh = it.Body.sprite.bounds.size.y;
            it.Body.transform.localScale = Vector3.one * (sh > 0 ? targetH / sh : 1f);
            it.Body.flipX = BossFacing != null && BossFacing(e) > 0;
            return true;
        }

        void Draw(EnemyState e, Item it, float dt)
        {
            var renderPos = IsometricProjection.ToRender(e.Position);
            float x = renderPos.x, y = renderPos.y;
            float r = (float)e.Radius;

            // 원본: bob 으로 위아래 살짝, 도약 중 lift
            float bob = Mathf.Sin((float)e.Bob) * r * 0.06f;
            float lift = e.IsJumping ? Mathf.Sin((float)(1 - e.JumpTime / System.Math.Max(1e-6, e.JumpDuration)) * Mathf.PI) * r * 0.9f : 0f;

            it.Go.transform.position = new Vector3(x, y, 0f);
            it.Body.transform.localPosition = new Vector3(0, bob + lift, 0);

            var frames = _sheets != null ? _sheets.FramesFor(KindId(e.Kind)) : null;
            bool movingNow = e.Velocity.Length > 0.15 || e.IsJumping;
            if (e.IsBoss && DrawDragon(e, it, dt, movingNow, r)) { }
            else if (frames != null && frames.Length >= 12)
            {
                bool moving = movingNow;
                int idx;
                if (e.BlinkTime > 0) idx = 8 + Mathf.Clamp((int)((0.24 - e.BlinkTime) / 0.06), 0, 3);
                else if (!moving) idx = 6 + (((int)(e.AnimTime * 2)) & 1);
                else
                {
                    bool sprint = e.IsApex && e.Ai == EnemyAi.Chase && frames.Length >= 16;
                    idx = sprint ? 12 + ((int)(e.AnimTime * 12)) % 4 : ((int)(e.AnimTime * 10)) % 6;
                }
                it.Body.sprite = frames[Mathf.Clamp(idx, 0, frames.Length - 1)];

                // 원본 크기 size = e.r*3.15 (apex 3.55) — 이 값이 스프라이트의 전체 폭·높이다 (반지름 아님)
                float targetH = r * (e.IsBoss ? 3.3f : e.IsApex ? 3.55f : 3.15f);
                float spriteH = it.Body.sprite.bounds.size.y;
                float s = spriteH > 0 ? targetH / spriteH : 1f;
                // 보스만 좌우를 본다 (원본 e.facing) — 잡몹은 단일 방향
                it.Body.flipX = e.IsBoss && BossFacing != null && BossFacing(e) > 0;
                it.Body.transform.localScale = new Vector3(s, s, 1f);
            }
            else
            {
                // 시트가 없으면 원으로 대체
                it.Body.sprite = _dot;
                it.Body.transform.localScale = Vector3.one * (r * 2f);
            }

            // 피격 플래시 + 상태 색
            // 드래곤 프레임이 있으면 원색(원본은 드래곤 시트에 틴트를 주지 않는다), 몬스터 대체 시에만 붉은 틴트
            Color tint = e.IsBoss ? (it.AnimKey != null ? Color.white : new Color(1f, 0.62f, 0.68f)) : e.IsApex ? new Color(1f, 0.72f, 0.82f) : Color.white;
            if (e.FrozenTime > 0) tint = new Color(0.6f, 0.85f, 1f);
            if (e.Hurt > 0) tint = Color.Lerp(tint, Color.white, Mathf.Clamp01((float)e.Hurt / 0.18f) * 0.85f);
            it.Body.color = tint;

            // 지상에서는 짧은 우하향 그림자, 도약 중에는 몸과 바닥 그림자의 간격이 벌어진다.
            // 레퍼런스의 비행 드론처럼 높이를 실루엣 크기보다 "분리 거리"로 먼저 읽게 한다.
            float airborne = Mathf.Clamp01(lift / Mathf.Max(.01f, r * .9f));
            it.Shadow.transform.localPosition = new Vector3(
                r * (.18f + airborne * .35f), -r * (.18f + airborne * .30f), 0f);
            it.Shadow.transform.localScale = new Vector3(
                r * (1.85f - airborne * .25f), r * (.68f - airborne * .10f), 1f);
            it.Shadow.transform.localRotation = Quaternion.Euler(0f, 0f, -9f);
            var shadow = ShadowColor;
            shadow.a *= 1f - airborne * 0.58f;
            it.Shadow.color = shadow;

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
                float inner = r * 0.92f;
                float outer = inner + r * (e.IsRanged ? 2.6f : 1.05f) * (0.35f + p * 0.65f);
                var col = e.IsRanged ? new Color(0.55f, 0.89f, 0.56f) : new Color(1f, 0.6f, 0.42f);
                col.a = 0.3f + p * 0.5f;
                it.Windup.startColor = it.Windup.endColor = col;
                it.Windup.startWidth = it.Windup.endWidth = e.IsRanged ? 0.06f : r * 0.9f;
                it.Windup.SetPosition(0, IsometricProjection.ToRender3(e.Position + e.AttackDir * inner, -0.05f));
                it.Windup.SetPosition(1, IsometricProjection.ToRender3(e.Position + e.AttackDir * outer, -0.05f));
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
            // 체력바만 정보 층으로 올린다 — 몸체·그림자는 개체 대역에 남아야 벽과의 앞뒤가 맞다.
            TunnelCrew.Presentation.Visual.VisualLayers.ApplyInfoSorting(it.HpBg, 31);
            TunnelCrew.Presentation.Visual.VisualLayers.ApplyInfoSorting(it.HpBar, 32);
            var lg = new GameObject("Windup"); lg.transform.SetParent(go.transform, false);
            it.Windup = lg.AddComponent<LineRenderer>();
            it.Windup.positionCount = 2; it.Windup.useWorldSpace = true;
            it.Windup.material = new Material(Shader.Find("Sprites/Default"));
            it.Windup.sortingOrder = 28;
            return it;
        }
        void Return(Item it) { it.Go.SetActive(false); it.AnimKey = null; it.AnimT = 0; it.Body.flipX = false; it.Body.color = Color.white; _pool.Push(it); }

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
