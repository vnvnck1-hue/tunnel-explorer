using System.Collections.Generic;
using TunnelCrew.Data;
using TunnelCrew.Sim;
using UnityEngine;

namespace TunnelCrew.Presentation
{
    /// <summary>
    /// AI 크루 렌더 — 원본 <c>AI.draw()</c> + <c>drawInstallations()</c>.
    /// 몸은 사람과 같은 8방향 시트(<see cref="PlayerView"/> 재사용), 머리 위에는 "AI 직업 · Lv" 라벨·체력 바·지금 하는 일.
    /// 설치물(센트리·전력 노드·급전선·균열 게이지·정찰 표식)은 절차 스프라이트 풀로 그린다.
    /// </summary>
    public sealed class CrewView : MonoBehaviour
    {
        sealed class Item
        {
            public GameObject Go; public PlayerView View; public PlayerState Proxy = new PlayerState();
            public TextMesh Label, State; public SpriteRenderer HpBg, HpBar, Shield;
        }
        readonly Dictionary<CrewMember, Item> _items = new Dictionary<CrewMember, Item>();
        readonly List<CrewMember> _gone = new List<CrewMember>();
        readonly Dictionary<RoleId, CharacterSheetAsset> _sheets = new Dictionary<RoleId, CharacterSheetAsset>();
        readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>();
        readonly List<LineRenderer> _lines = new List<LineRenderer>();
        Sprite _dot, _square, _ring;
        Font _font;

        static readonly Color[] RoleCol = { new Color(1f, .83f, .43f), new Color(1f, .55f, .45f), new Color(.5f, .92f, .82f), new Color(.78f, .63f, 1f) };

        public void Bind(Dictionary<RoleId, CharacterSheetAsset> sheets)
        {
            _sheets.Clear(); foreach (var kv in sheets) _sheets[kv.Key] = kv.Value;
            _dot = ProcSprites.Circle(24, .7f); _square = ProcSprites.Square(); _ring = ProcSprites.Ring(64, .11f);
            _font = Fonts.UIBold;   // Pretendard (원본 CSS 와 동일)
        }

        public void Render(AiCrewSystem crew, float dt)
        {
            foreach (var m in crew.Members)
            {
                if (!_items.TryGetValue(m, out var it)) _items[m] = it = Make(m);
                Draw(m, it, dt);
            }
            _gone.Clear();
            foreach (var kv in _items) if (!crew.Members.Contains(kv.Key)) _gone.Add(kv.Key);
            foreach (var m in _gone) { Destroy(_items[m].Go); _items.Remove(m); }
            DrawInstallations(crew);
        }

        Item Make(CrewMember m)
        {
            var go = new GameObject($"Crew_{m.Role}_{m.Id}"); go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sortingOrder = 29;
            var view = go.AddComponent<PlayerView>();
            typeof(PlayerView).GetField("_renderer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(view, sr);
            if (_sheets.TryGetValue(m.Role, out var sheet) || _sheets.TryGetValue(RoleId.Driller, out sheet))
                foreach (var d in sheet.directions) if (d.walk != null && d.walk.Length > 0) view.SetWalkFrames(d.direction, d.walk);
            var it = new Item { Go = go, View = view };
            // 부모(PlayerView)가 발을 시뮬 위치보다 FootDrop 만큼 아래에 두므로, 자식들은 그만큼 올려 몸 중심 기준 오프셋을 유지한다
            const float fd = PlayerView.FootDrop;
            it.Label = MakeText(go.transform, 1.62f + fd, 0.24f, 44);
            it.State = MakeText(go.transform, 1.98f + fd, 0.20f, 36);
            it.HpBg = MakeSprite(go.transform, _square, new Color(0, 0, 0, .55f), 1.42f + fd, .68f, .08f, 45);
            it.HpBar = MakeSprite(go.transform, _square, RoleCol[2], 1.42f + fd, .68f, .08f, 46);
            it.Shield = MakeSprite(go.transform, _ring, new Color(.5f, .92f, .82f, .6f), fd, 1.35f, 1.35f, 31);
            return it;
        }
        TextMesh MakeText(Transform parent, float y, float size, int order)
        {
            var go = new GameObject("t"); go.transform.SetParent(parent, false); go.transform.localPosition = new Vector3(0, y, 0);
            var tm = go.AddComponent<TextMesh>(); tm.font = _font; tm.fontSize = 48; tm.characterSize = size * .1f; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.fontStyle = FontStyle.Bold;
            var r = go.GetComponent<MeshRenderer>(); r.sortingOrder = order; if (_font != null) r.sharedMaterial = _font.material;
            return tm;
        }
        static SpriteRenderer MakeSprite(Transform parent, Sprite s, Color c, float y, float w, float h, int order)
        {
            var go = new GameObject("s"); go.transform.SetParent(parent, false); go.transform.localPosition = new Vector3(0, y, 0); go.transform.localScale = new Vector3(w, h, 1);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = s; sr.color = c; sr.sortingOrder = order; return sr;
        }

        void Draw(CrewMember m, Item it, float dt)
        {
            var p = it.Proxy;
            p.Position = m.Position; p.Velocity = m.Dashing ? m.DashVel : m.Velocity; p.Aim = m.Aim; p.Downed = m.Down; p.IFrames = m.IFrames; p.DashActive = m.Dashing;
            it.View.Render(p, dt);
            var col = RoleCol[(int)m.Role];
            it.Label.text = $"AI {AiCrewSystem.NameOf(m.Role)} · {m.Level}";
            it.Label.color = m.Down ? new Color(1f, .79f, .84f) : new Color(.94f, .9f, 1f);
            it.State.text = m.Down ? $"다운 {(m.ReviveT > 0 ? $"구조 {m.ReviveT / 5:P0}" : "")}" : m.StateLabel;
            it.State.color = m.Down ? new Color(1f, .55f, .66f) : new Color(.9f, .84f, 1f, .78f);
            float hp = Mathf.Clamp01((float)(m.Hp / m.HpMax));
            it.HpBar.transform.localScale = new Vector3(.68f * hp, .08f, 1);
            it.HpBar.transform.localPosition = new Vector3(-.34f * (1 - hp), 1.42f + PlayerView.FootDrop, 0);
            it.HpBar.color = m.Down ? new Color(1f, .33f, .49f) : hp > .5f ? new Color(.5f, .92f, .82f) : hp > .25f ? new Color(1f, .83f, .43f) : new Color(1f, .55f, .66f);
            it.Shield.enabled = m.ShieldT > 0;
            if (it.Shield.enabled) it.Shield.color = new Color(.5f, .92f, .82f, .5f + .3f * Mathf.Sin(Time.time * 9f));
        }

        // ── 설치물
        SpriteRenderer Rent(int i)
        {
            while (_pool.Count <= i)
            {
                var go = new GameObject("inst"); go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>(); _pool.Add(sr);
            }
            var s = _pool[i]; s.gameObject.SetActive(true); s.transform.rotation = Quaternion.identity; return s;
        }
        LineRenderer RentLine(int i)
        {
            while (_lines.Count <= i)
            {
                var go = new GameObject("link"); go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>(); lr.material = new Material(Shader.Find("Sprites/Default")); lr.positionCount = 2; lr.widthMultiplier = .05f; lr.sortingOrder = 27; lr.useWorldSpace = true;
                _lines.Add(lr);
            }
            var l = _lines[i]; l.gameObject.SetActive(true); return l;
        }
        static void Set(SpriteRenderer sr, Vec2 at, Sprite s, Color c, float scale, int order, float z = 0)
        {
            sr.transform.position = new Vector3((float)at.X, (float)at.Y, z); sr.sprite = s; sr.color = c; sr.transform.localScale = Vector3.one * scale; sr.sortingOrder = order;
        }

        void DrawInstallations(AiCrewSystem crew)
        {
            int i = 0, li = 0;
            foreach (var n in crew.Nodes)
            {
                float a = Mathf.Min(1, (float)(n.Life / 4));
                Set(Rent(i++), n.Position, _ring, new Color(.5f, .92f, .82f, .28f * a), (float)n.Radius * 2, 27);
                Set(Rent(i++), n.Position, _dot, new Color(.07f, .17f, .16f, a), .36f, 28);
                Set(Rent(i++), n.Position, _dot, new Color(.75f, 1f, .94f, a), .14f, 29);
            }
            foreach (var t in crew.Turrets)
            {
                var col = t.Powered ? new Color(.78f, .63f, 1f) : new Color(.41f, .34f, .45f);
                CrewNode src = null; foreach (var n in crew.Nodes) if (Vec2.Distance(n.Position, t.Position) <= n.Radius) { src = n; break; }
                if (src != null)
                {
                    var l = RentLine(li++);
                    l.SetPosition(0, new Vector3((float)src.Position.X, (float)src.Position.Y, 0)); l.SetPosition(1, new Vector3((float)t.Position.X, (float)t.Position.Y, 0));
                    var c = new Color(.5f, .92f, .82f, .3f + .15f * Mathf.Sin(Time.time * 8f)); l.startColor = l.endColor = c;
                }
                Set(Rent(i++), t.Position, _dot, new Color(.13f, .08f, .18f), .5f, 28);
                Set(Rent(i++), t.Position, _ring, col, .52f, 29);
                var barrel = Rent(i++); Set(barrel, t.Position + Vec2.FromAngle(t.Aim) * .18, _square, col, 1, 30);
                barrel.transform.localScale = new Vector3(.36f, .15f, 1); barrel.transform.rotation = Quaternion.Euler(0, 0, (float)t.Aim * Mathf.Rad2Deg);
                Set(Rent(i++), t.Position, _dot, new Color(.95f, .91f, 1f), .16f, 31);
                float ammo = t.Mag > 0 ? (float)t.Ammo / t.Mag : 0;
                Set(Rent(i++), t.Position, _ring, t.Powered ? new Color(.5f, .92f, .82f, .5f + .5f * ammo) : new Color(1f, .44f, .54f, .8f), .66f, 29);
            }
            foreach (var kv in crew.Cracks)
            {
                var v = kv.Value; var w = crewWorld;
                if (w == null) break;
                int c = kv.Key % w.Cols, r = kv.Key / w.Cols;
                Set(Rent(i++), WorldGrid.CellCenter(c, r), _ring, (v.P > .72 ? new Color(1f, .95f, .84f) : new Color(1f, .83f, .43f)) * new Color(1, 1, 1, .32f + .62f * (float)v.P), .34f + .34f * (float)v.P, 29);
            }
            foreach (var k in crew.Marks)
            {
                float a = Mathf.Min(1, (float)(k.Ttl / 1.5)) * .7f;
                if (k.Threat) { var x = Rent(i++); Set(x, k.At, _square, new Color(1f, .44f, .54f, a), 1, 29); x.transform.localScale = new Vector3(.36f, .08f, 1); x.transform.rotation = Quaternion.Euler(0, 0, 45); var y = Rent(i++); Set(y, k.At, _square, new Color(1f, .44f, .54f, a), 1, 29); y.transform.localScale = new Vector3(.36f, .08f, 1); y.transform.rotation = Quaternion.Euler(0, 0, -45); }
                else { Set(Rent(i++), k.At, _ring, new Color(1f, .83f, .43f, a), .6f, 29); Set(Rent(i++), k.At, _dot, new Color(1f, .83f, .43f, a), .1f, 29); }
            }
            for (; i < _pool.Count; i++) _pool[i].gameObject.SetActive(false);
            for (; li < _lines.Count; li++) _lines[li].gameObject.SetActive(false);
        }

        /// <summary>균열 키 → 칸 변환에 필요한 월드. RunBootstrap 이 층마다 넣어 준다.</summary>
        public WorldGrid crewWorld;
    }
}
