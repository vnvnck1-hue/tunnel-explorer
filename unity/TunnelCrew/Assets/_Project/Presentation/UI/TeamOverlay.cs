using System;
using System.Collections.Generic;
using TunnelCrew.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using GUI = TunnelCrew.Presentation.CRT.CrtGui;
using GUIUtility = TunnelCrew.Presentation.CRT.CrtGuiUtility;
using Event = TunnelCrew.Presentation.CRT.CrtPointerEvent;

namespace TunnelCrew.Presentation
{
    /// <summary>
    /// 팀 핑 · 크루 채팅 · 퀵크래프트의 입력과 화면 표현 (원본 TCPING/TCCHAT/TC_CRAFT 의 DOM·캔버스 부분).
    /// 규칙·상태는 Sim(<see cref="PingSystem"/>, <see cref="CrewChat"/>, <see cref="QuickCraftSystem"/>)에 있고, 여기서는
    /// 키·마우스를 읽어 Sim 을 호출하고 CRT CameraSpace UGUI로 그린다. RunBootstrap 은 <see cref="BlocksMouse"/>·<see cref="ChatOpen"/> 등을 보고 월드 입력을 막는다.
    /// </summary>
    public sealed class TeamOverlay : MonoBehaviour, CRT.ICrtScreen
    {
        TunnelSim _sim; Camera _cam; Func<bool> _active;
        public Action<string> Log;
        float _k = 1f;
        Texture2D _white;
        readonly Dictionary<string, Texture2D> _tex = new Dictionary<string, Texture2D>();
        readonly Dictionary<string, CRT.DynamicDialogueText.Script> _bubbleScripts = new Dictionary<string, CRT.DynamicDialogueText.Script>();
        Font _font;

        // ── 핑 휠 상태 (원본 P.hold)
        sealed class Hold { public double T0; public Vector2 S, C; public float Moved; public bool Open; public double OpenAt; public int? Hovered; }
        Hold _hold;
        const float TapMs = 120, TapMovePx = 18, DeadPx = 24, RadiusPx = 96;
        // ── 채팅
        public bool ChatOpen { get; private set; }
        string _draft = "", _input = ""; bool _focusPending;
        // ── 크래프트
        double _cDownAt; bool _cHeld;

        public bool PingHold => _hold != null;
        public bool CraftWheelOpen => _sim != null && _sim.Craft.Phase == CraftPhase.Wheel;
        public bool CraftPlacing => _sim != null && _sim.Craft.Phase == CraftPhase.Placing;
        /// <summary>좌·우클릭이 장비 입력으로 새지 않아야 하는 상태.</summary>
        public bool BlocksMouse => ChatOpen || PingHold || CraftWheelOpen || CraftPlacing;
        /// <summary>Space 가 대시가 아닌 다른 뜻인 상태 (크래프트 휠 제작 확인 · 주입 중).</summary>
        public bool BlocksSpace => ChatOpen || CraftWheelOpen || (_sim != null && _sim.Craft.Using != null);

        public void Bind(TunnelSim sim, Camera cam, Func<bool> active)
        {
            CRT.CrtSurface.Register(this, 10);
            _sim = sim; _cam = cam; _active = active;
            _white = Texture2D.whiteTexture;
            _font = Fonts.UIBold;   // Pretendard (원본 CSS 와 동일)
            foreach (var t in Resources.LoadAll<Texture2D>("UI/crafting")) _tex[t.name] = t;
            _craftView = new GameObject("CraftObjects").AddComponent<CraftView>();
            _craftView.transform.SetParent(transform, false);
            _craftView.Bind(_tex);
        }
        CraftView _craftView;

        double Now => Time.unscaledTimeAsDouble;
        static Color Hex(string hex) { if (string.IsNullOrEmpty(hex) || !ColorUtility.TryParseHtmlString(hex, out var c)) return Color.white; return c; }
        Vector2 WorldToGui(Vec2 w)
        {
            // 원근 월드가 켜져 있으면 그쪽 카메라·레터박스 기준으로 다시 투영한다(§4 2단계).
            if (TunnelCrew.Presentation.Visual.PerspectiveViewport.TrySimToScreen(
                    new Vector2((float)w.X, (float)w.Y), 0f, out var ps))
                return new Vector2(ps.x, Screen.height - ps.y);
            var s = _cam.WorldToScreenPoint(IsometricProjection.ToRender3(w));
            return new Vector2(s.x, Screen.height - s.y);
        }
        Vec2 ScreenToWorld(Vector2 screen)
        {
            if (TunnelCrew.Presentation.Visual.PerspectiveViewport.TryScreenToSim(screen, out var hit))
                return new Vec2(hit.x, hit.y);
            var w = _cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_cam.transform.position.z));
            var sim = IsometricProjection.ToWorld(new Vector2(w.x, w.y));
            return new Vec2(sim.x, sim.y);
        }
        bool Playing => _sim != null && _sim.World != null && _active() && _sim.Phase == GamePhase.Playing;

        /// <summary>Esc 를 여기서 먹으면 true — 채팅 닫기 · 핑 휠 취소 · 크래프트 취소.</summary>
        public bool HandleEscape()
        {
            if (ChatOpen) { CloseChat(true); return true; }
            if (_hold != null) { EndHold(false); return true; }
            if (_sim.Craft.Phase == CraftPhase.Placing) { _sim.Craft.CancelPlacement(); return true; }
            if (_sim.Craft.Phase == CraftPhase.Wheel) { _sim.Craft.Close(false); return true; }
            return false;
        }

        // ═════════════════════ 입력
        void Update()
        {
            if (CRT.CRTDisplayController.Instance != null && CRT.CRTDisplayController.Instance.ConsumesInput) return;
            if (_sim == null || _sim.World == null) return;
            var kb = Keyboard.current; var mouse = Mouse.current;
            if (kb == null || mouse == null) return;
            var mp = mouse.position.ReadValue();
            bool playing = Playing;

            if (!playing)
            {
                if (_hold != null) EndHold(false);
                if (ChatOpen) CloseChat(true);
                return;
            }

            // ── 채팅 — Enter 로 열기 (다른 휠이 열려 있지 않을 때). 입력 자체는 OnGUI 의 TextField 가 받는다
            if (!ChatOpen)
            {
                bool wheelBusy = _hold != null || _sim.Craft.Phase != CraftPhase.Closed || _sim.Escape.Phase == EscapePhase.Placing;
                if ((kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && !wheelBusy && !_sim.Traits.HasOffer) OpenChat();
            }
            if (ChatOpen) return;   // 채팅 중엔 G·V·C 를 글자로 쓴다

            // ── 퀵크래프트 (원본 C 홀드/탭)
            var craft = _sim.Craft;
            if (kb.cKey.wasPressedThisFrame)
            {
                if (craft.Phase == CraftPhase.Closed && _hold == null && _sim.Escape.Phase != EscapePhase.Placing) { _cDownAt = Now; _cHeld = true; craft.Open(); }
                else if (craft.Phase == CraftPhase.Wheel && !_cHeld) craft.Close(false);
            }
            if (kb.cKey.wasReleasedThisFrame && _cHeld)
            {
                _cHeld = false;
                if (craft.Phase == CraftPhase.Wheel && Now - _cDownAt >= .18) craft.Close(false);   // 0.18초 이상 홀드 → 떼면 닫힘. 짧은 탭은 열린 채 유지
            }
            if (craft.Phase == CraftPhase.Wheel)
            {
                // 마우스 방향으로 60° 슬롯 선택 (데드존 54px)
                var center = WheelCenterScreen();
                var d = GUI.GlassToContentScreen(mp) - center;
                if (d.magnitude >= 54 * _k) craft.Select(QuickCraftSystem.Recipes[QuickCraftSystem.SlotFromAngle(Mathf.Atan2(-d.y, d.x))].Id);
                for (int i = 0; i < 6; i++)
                    if (DigitPressed(kb, i + 1)) { var id = QuickCraftSystem.Recipes[i].Id; if (craft.Selected == id) craft.ConfirmSelection(); else craft.Select(id); }
                if (kb.spaceKey.wasPressedThisFrame || mouse.leftButton.wasPressedThisFrame) craft.ConfirmSelection();
                else if (mouse.rightButton.wasPressedThisFrame) craft.Close(false);
                return;
            }
            if (craft.Phase == CraftPhase.Placing)
            {
                craft.UpdatePlacement(ScreenToWorld(mp));
                if (mouse.leftButton.wasPressedThisFrame) craft.ConfirmPlacement();
                else if (mouse.rightButton.wasPressedThisFrame) craft.CancelPlacement();
                return;
            }

            // ── 팀 핑 (원본 G 탭/홀드 · V)
            bool pingOk = _sim.Escape.Phase != EscapePhase.Placing && !_sim.Traits.HasOffer;
            if (kb.gKey.wasPressedThisFrame && pingOk && _hold == null) _hold = new Hold { T0 = Now, S = mp, C = mp };
            if (_hold != null)
            {
                var h = _hold; h.C = mp; h.Moved = Mathf.Max(h.Moved, Vector2.Distance(mp, h.S));
                if (!h.Open && (h.Moved >= TapMovePx * _k || (Now - h.T0) * 1000 >= TapMs)) { h.Open = true; h.OpenAt = Now; }
                h.Hovered = HoverIndex(h);
                if (mouse.rightButton.wasPressedThisFrame) EndHold(false);
                else if (kb.gKey.wasReleasedThisFrame) EndHold(true);
            }
            else if (kb.vKey.wasPressedThisFrame && pingOk) _sim.Ping.Send(PingType.Danger, ScreenToWorld(mp), true);
        }
        static bool DigitPressed(Keyboard kb, int n) => n switch { 1 => kb.digit1Key.wasPressedThisFrame, 2 => kb.digit2Key.wasPressedThisFrame, 3 => kb.digit3Key.wasPressedThisFrame, 4 => kb.digit4Key.wasPressedThisFrame, 5 => kb.digit5Key.wasPressedThisFrame, 6 => kb.digit6Key.wasPressedThisFrame, _ => false };

        int? HoverIndex(Hold h)
        {
            var d = h.C - h.S; if (d.magnitude < DeadPx * _k) return null;
            // 원본은 y 가 아래로 증가: 0=↑ 부터 시계방향. Unity 화면 y 는 위로 증가하니 뒤집는다
            float a = Mathf.Atan2(-d.y, d.x);
            return ((Mathf.RoundToInt((a + Mathf.PI / 2) / (Mathf.PI / 4)) % 8) + 8) % 8;
        }
        void EndHold(bool commit)
        {
            var h = _hold; if (h == null) return; _hold = null;
            if (!commit) return;
            double dt = (Now - h.T0) * 1000;
            if (!h.Open && dt < TapMs && h.Moved < TapMovePx * _k) { _sim.Ping.Send(PingType.Here, ScreenToWorld(h.C), false); return; }
            var i = HoverIndex(h);
            if (i == null) return;   // 중앙에서 떼면 취소
            _sim.Ping.Send(PingSystem.Dirs[i.Value], ScreenToWorld(h.C), false);
        }

        double _chatOpenedAt;
        void OpenChat() { ChatOpen = true; _input = _draft; _focusPending = true; _chatOpenedAt = Now; CRT.CRTDisplayController.Instance?.SetUiFocus(true); }
        void CloseChat(bool keepDraft) { _draft = keepDraft ? _input : ""; ChatOpen = false; GUI.FocusControl(null); CRT.CRTDisplayController.Instance?.SetUiFocus(false); }
        void SubmitChat()
        {
            var text = _input; _input = "";
            if (_sim.Chat.Say(text)) { }
            CloseChat(false);
        }

        Vector2 WheelCenterScreen() => new Vector2(Screen.width * .5f, Screen.height * (1 - .42f));   // 원본 left:50% top:42%

        // ═════════════════════ 그리기
        GUIStyle _label, _small, _bold;
        void EnsureStyles()
        {
            if (_label != null && Mathf.Abs(_lastK - _k) < .001f) return;
            _lastK = _k;
            _label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(15 * _k), richText = true, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            _small = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(13 * _k), richText = true, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            _bold = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(17 * _k), fontStyle = FontStyle.Bold, richText = true, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            foreach (var s in new[] { _label, _small, _bold }) s.normal.textColor = new Color(.96f, .94f, 1f);
        }
        float _lastK = -1;
        void Fill(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, _white); GUI.color = Color.white; }
        void Disc(Vector2 c, float r, Color col) { GUI.color = col; GUI.DrawTexture(new Rect(c.x - r, c.y - r, r * 2, r * 2), _dotTex); GUI.color = Color.white; }
        void RingGui(Vector2 c, float r, Color col) { GUI.color = col; GUI.DrawTexture(new Rect(c.x - r, c.y - r, r * 2, r * 2), _ringTex); GUI.color = Color.white; }
        Texture2D _dotTex, _ringTex;
        void EnsureShapes()
        {
            if (_dotTex != null) return;
            _dotTex = ProcSprites.Circle(64, .9f).texture;
            _ringTex = ProcSprites.Ring(128, .08f).texture;
        }
        GUIStyle Sz(GUIStyle b, int px, TextAnchor? a = null, Color? col = null, FontStyle? fs = null)
        {
            var s = new GUIStyle(b) { fontSize = Mathf.RoundToInt(px * _k) };
            if (a.HasValue) s.alignment = a.Value; if (col.HasValue) s.normal.textColor = col.Value; if (fs.HasValue) s.fontStyle = fs.Value;
            return s;
        }

        public void DrawCrt()
        {
            if (_sim == null || _sim.World == null || !_active()) return;
            _k = Screen.height / 1080f;
            EnsureStyles(); EnsureShapes();
            DrawPingMarkers(); DrawPingLog(); DrawPingWheel();
            DrawChatBubbles(); DrawChat();
            DrawCraft();
        }

        // ── 핑 (§5)
        void DrawGlyph(Vector2 c, PingTypeDef T, float r, float alpha)
        {
            Disc(c, r, new Color(.04f, .03f, .07f, .88f * alpha));
            RingGui(c, r, Hex(T.Color) * new Color(1, 1, 1, alpha));
            GUI.Label(new Rect(c.x - r, c.y - r, r * 2, r * 2), T.Glyph, Sz(_bold, Mathf.RoundToInt(r * 1.05f / _k), TextAnchor.MiddleCenter, Hex(T.Color) * new Color(1, 1, 1, alpha)));
        }
        void DrawPingMarkers()
        {
            var P = _sim.Ping; double t = _sim.RunTime; bool reduced = Feedback.Instance != null && Feedback.Instance.ReducedMotion;
            float left = 52 * _k, right = Screen.width - 52 * _k, top = 120 * _k, bottom = Screen.height - 92 * _k;
            var off = new List<PingMarker>();
            float cellPx = (float)(Screen.height / (_cam.orthographicSize * 2));
            foreach (var m in P.Markers)
            {
                var T = PingSystem.Def(m.Type); double age = t - m.Born, leftT = m.Until - t;
                var s = WorldToGui(m.At);
                if (!(s.x >= left && s.x <= right && s.y >= top && s.y <= bottom)) { off.Add(m); continue; }
                float alpha = leftT < .6 ? Mathf.Max(0, (float)(leftT / .6)) : 1;
                float pop = reduced ? 1 : age < PingSystem.PopSec ? .4f + .6f * Mathf.Sin((float)(age / PingSystem.PopSec) * Mathf.PI / 2) : 1;
                float baseR = (13 + (m.Level - 1) * 2) * _k;
                double ringAge = m.Pulse > 0 ? Math.Min(age, t - m.Pulse) : age;
                if (!reduced && ringAge < PingSystem.RingSec)
                {
                    float kk = (float)(ringAge / PingSystem.RingSec);
                    RingGui(s, cellPx * (.25f + 1.3f * kk), Hex(T.Color) * new Color(1, 1, 1, alpha * (1 - kk) * .8f));
                }
                Disc(s, 4 * _k, Hex(T.Color) * new Color(1, 1, 1, alpha * .9f));
                Fill(new Rect(s.x - 1 * _k, s.y - 18 * _k * pop, 2 * _k, 15 * _k * pop), Hex(T.Color) * new Color(1, 1, 1, alpha * .9f));
                float iy = s.y - 18 * _k * pop - baseR * pop;
                DrawGlyph(new Vector2(s.x, iy), T, baseR * pop, alpha * .95f);
                if (m.Level > 1) GUI.Label(new Rect(s.x + baseR + 3 * _k, iy - baseR * .6f - 8 * _k, 40 * _k, 16 * _k), "×" + m.Level, Sz(_small, 10, TextAnchor.MiddleLeft, Color.white * alpha, FontStyle.Bold));
                if (m.Agree.Count > 0) GUI.Label(new Rect(s.x + baseR + 3 * _k, iy + baseR * .7f - 8 * _k, 40 * _k, 16 * _k), "+" + m.Agree.Count, Sz(_small, 10, TextAnchor.MiddleLeft, Hex("#5FF5E0") * alpha, FontStyle.Bold));
                float la = age < PingSystem.LabelSec ? 1 : Mathf.Max(0, 1 - (float)((age - PingSystem.LabelSec) / .25));
                if (la > 0)
                {
                    string txt = m.Name + " · " + T.Ko + (m.Ctx != null && m.Ctx.Name != null && m.Ctx.Kind != "unknown" ? " · " + m.Ctx.Name : m.Ctx != null && m.Ctx.Kind == "unknown" ? " · 알 수 없는 위치" : "");
                    var st = Sz(_small, 12, TextAnchor.MiddleCenter, new Color(.96f, .94f, 1f, alpha * la), FontStyle.Bold);
                    float tw = st.CalcSize(new GUIContent(txt)).x + 14 * _k, ly = iy - baseR - 14 * _k;
                    Fill(new Rect(s.x - tw / 2, ly - 9 * _k, tw, 18 * _k), new Color(.04f, .03f, .07f, .9f * alpha * la));
                    GUI.Label(new Rect(s.x - tw / 2, ly - 9 * _k, tw, 18 * _k), txt, st);
                }
            }
            // 화면 밖 — 중요도순 최대 3. 위험·도움·후퇴는 거리 무관, 나머지 30칸 이내
            off.Sort((a, b) => PingSystem.Def(b.Type).Pri.CompareTo(PingSystem.Def(a.Type).Pri));
            int n = 0; var me = _sim.Player.Position;
            foreach (var m in off)
            {
                double dist = Vec2.Distance(m.At, me);
                bool always = m.Type == PingType.Danger || m.Type == PingType.Help || m.Type == PingType.Retreat;
                if (!always && dist > PingSystem.OffTiles) continue;
                if (n++ >= PingSystem.OffMax) break;
                var T = PingSystem.Def(m.Type); var s = WorldToGui(m.At);
                float mx = Screen.width * .5f, my = Screen.height * .5f, dx = s.x - mx, dy = s.y - my;
                float tx = float.PositiveInfinity, ty = float.PositiveInfinity;
                if (Mathf.Abs(dx) > .001f) tx = (dx > 0 ? right - mx : left - mx) / dx;
                if (Mathf.Abs(dy) > .001f) ty = (dy > 0 ? bottom - my : top - my) / dy;
                float kk = Mathf.Max(0, Mathf.Min(tx > 0 ? tx : float.PositiveInfinity, ty > 0 ? ty : float.PositiveInfinity));
                var p = new Vector2(mx + dx * kk, my + dy * kk); float a = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                float alpha = Mathf.Clamp01((float)((m.Until - t) / .6));
                var mtx = GUI.matrix; GUIUtility.RotateAroundPivot(a, p);
                Fill(new Rect(p.x + 14 * _k, p.y - 3 * _k, 10 * _k, 6 * _k), Hex(T.Color) * new Color(1, 1, 1, alpha));
                GUI.matrix = mtx;
                DrawGlyph(p, T, 13 * _k, alpha);
                GUI.Label(new Rect(p.x - 30 * _k, p.y + 14 * _k, 60 * _k, 16 * _k), Mathf.Max(1, Mathf.RoundToInt((float)dist)) + "칸", Sz(_small, 10, TextAnchor.MiddleCenter, new Color(.96f, .94f, 1f, alpha), FontStyle.Bold));
            }
        }
        void DrawPingLog()
        {
            var P = _sim.Ping; double t = _sim.RunTime;
            if (P.Log.Count == 0 && P.Note == null) return;
            float x = 62 * _k, y0 = 200 * _k; int i = 0;   // 좌상단 로그 패널 아래
            var st = Sz(_label, 13, TextAnchor.MiddleLeft, null, FontStyle.Bold);
            foreach (var l in P.Log)
            {
                float a = Mathf.Min(1, (float)((PingSystem.LogSec - (t - l.T)) / .4));
                string txt = l.Text + (l.N > 1 ? " ×" + l.N : ""); float tw = st.CalcSize(new GUIContent(txt)).x + 22 * _k, y = y0 + i * 22 * _k;
                Fill(new Rect(x, y - 10 * _k, tw, 20 * _k), new Color(.05f, .03f, .09f, .86f * a));
                Fill(new Rect(x, y - 10 * _k, 3 * _k, 20 * _k), Hex(l.Color) * new Color(1, 1, 1, a));
                st.normal.textColor = new Color(.96f, .94f, 1f, a);
                GUI.Label(new Rect(x + 11 * _k, y - 10 * _k, tw, 20 * _k), txt, st);
                i++;
            }
            if (P.Note != null && P.NoteUntil > t && P.Note.Length > 0)
            {
                float y = y0 + i * 22 * _k, tw = st.CalcSize(new GUIContent(P.Note)).x + 22 * _k;
                Fill(new Rect(x, y - 10 * _k, tw, 20 * _k), new Color(.16f, .04f, .08f, .86f));
                st.normal.textColor = new Color(1f, .7f, .76f);
                GUI.Label(new Rect(x + 11 * _k, y - 10 * _k, tw, 20 * _k), P.Note, st);
            }
        }
        void DrawPingWheel()
        {
            var h = _hold; if (h == null || !h.Open) return;
            var P = _sim.Ping; float R = RadiusPx * _k, dead = DeadPx * _k;
            var c = new Vector2(h.S.x, Screen.height - h.S.y);
            bool reduced = Feedback.Instance != null && Feedback.Instance.ReducedMotion;
            float kk = reduced ? 1 : Mathf.Min(1, (float)((Now - h.OpenAt) / .08));
            Disc(c, (R + 24 * _k) * kk, new Color(.05f, .03f, .09f, .78f));
            Disc(c, (dead + 4 * _k) * kk, new Color(.05f, .03f, .09f, .5f));
            for (int i = 0; i < 8; i++)
            {
                var T = PingSystem.Def(PingSystem.Dirs[i]); float a = -Mathf.PI / 2 + i * Mathf.PI / 4; bool hov = h.Hovered == i;
                var ip = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (R - 8 * _k) * kk;
                if (hov) Disc(ip, 30 * _k, Hex(T.Color) * new Color(1, 1, 1, .28f));
                DrawGlyph(ip + new Vector2(0, -8 * _k), T, (hov ? 16 : 13) * _k, hov ? 1 : .85f);
                GUI.Label(new Rect(ip.x - 40 * _k, ip.y + 6 * _k, 80 * _k, 18 * _k), T.Ko, Sz(_small, hov ? 13 : 12, TextAnchor.MiddleCenter, hov ? Color.white : new Color(.85f, .8f, .91f), FontStyle.Bold));
            }
            bool locked = P.Locked, empty = P.Charge < 1 && !_sim.Player.Downed;
            Disc(c, dead + 2 * _k, h.Hovered == null ? new Color(.37f, .72f, 1f, .25f) : new Color(.05f, .03f, .09f, .6f));
            GUI.Label(new Rect(c.x - 40 * _k, c.y - 10 * _k, 80 * _k, 20 * _k), locked ? P.LockLeft.ToString("F1") + "s" : empty ? P.ChargeLeft.ToString("F1") + "s" : "취소", Sz(_small, 10, TextAnchor.MiddleCenter, locked || empty ? new Color(1f, .7f, .76f) : new Color(.96f, .94f, 1f), FontStyle.Bold));
            for (int i = 0; i < PingSystem.Charges; i++) Disc(c + new Vector2((-15 + i * 10) * _k, (R + 36 * _k)), 3 * _k, i < P.Charge ? Hex("#5FB8FF") : new Color(1, 1, 1, .18f));
            // 커서 십자 — 실제 핑 위치
            var cur = new Vector2(h.C.x, Screen.height - h.C.y); var cc = h.Hovered != null ? Hex(PingSystem.Def(PingSystem.Dirs[h.Hovered.Value]).Color) : Hex("#5FB8FF");
            Fill(new Rect(cur.x - 8 * _k, cur.y - 1, 16 * _k, 2), cc); Fill(new Rect(cur.x - 1, cur.y - 8 * _k, 2, 16 * _k), cc);
        }

        // ── 채팅 (LOL 식 좌하단 채팅창 + 말풍선)
        void DrawChat()
        {
            var C = _sim.Chat; double t = _sim.RunTime;
            var safe=CRT.CrtSafeArea.Calculate(Screen.width,Screen.height,Screen.safeArea,CRT.CRTDisplayController.Instance!=null?CRT.CRTDisplayController.Instance.Effective.safeAreaInset:.055f);
            float x = safe.xMin, w = 600 * _k, bottom = Screen.height-safe.yMin-230*_k, lh = 28 * _k;
            int n = ChatOpen ? CrewChat.OpenLines : CrewChat.IdleLines;
            int start = Math.Max(0, C.Log.Count - n);
            var lines = new List<ChatMessage>(); for (int i = start; i < C.Log.Count; i++) lines.Add(C.Log[i]);
            if (ChatOpen)
            {
                float h = n * lh + 12 * _k;
                Fill(new Rect(x, bottom - h - 34 * _k, w, h), new Color(.05f, .03f, .09f, .82f));
                Fill(new Rect(x, bottom - h - 34 * _k, w, 1), new Color(1f, .83f, .43f, .28f));
                if (lines.Count == 0) GUI.Label(new Rect(x + 8 * _k, bottom - 34 * _k - lh, w-16*_k, lh), "<color=#8a8095>크루에게 한마디 건네 보세요.</color>", Sz(_label, 18));
            }
            float chatY=bottom-34*_k;
            for (int i = lines.Count-1; i >=0; i--)
            {
                var m = lines[i];
                float a = ChatOpen ? 1 : Mathf.Clamp01((float)((CrewChat.IdleFadeSec + .9 - (t - m.T)) / .9));
                if (a <= 0) continue;
                string txt = $"<color=#{ColorUtility.ToHtmlStringRGB(Hex(CrewChat.SeatColor(m.Seat)))}><b>{m.Name}</b></color>  {m.Text}";
                var st = Sz(_label, 18, TextAnchor.MiddleLeft, new Color(.96f, .94f, 1f, a));st.wordWrap=true;
                float lineHeight=Mathf.Max(lh,st.CalcHeight(new GUIContent(txt),w-16*_k)+6*_k);
                chatY-=lineHeight;if(chatY<Screen.height-safe.yMax+90*_k)break;
                Fill(new Rect(x, chatY, w, lineHeight), new Color(.05f, .03f, .09f, ChatOpen ? .9f : .62f*a));
                GUI.Label(new Rect(x + 8 * _k, chatY, w - 16 * _k, lineHeight), txt, st);
            }
            if (ChatOpen)
            {
                // UGUI submits and deactivates during EventSystem.Update, before this LateUpdate
                // presenter. Preserve the submitted text even though isFocused is already false.
                if (GUI.TryConsumeTextSubmit("tcChatInput", out var submitted))
                {
                    _input=submitted;
                    if (Now-_chatOpenedAt>.15) { SubmitChat(); return; }
                    _focusPending=true;
                }
                var r = new Rect(x, bottom - 34 * _k, w, 34 * _k);
                Fill(r, new Color(.05f, .03f, .09f, .88f)); Fill(new Rect(r.x, r.y, r.width, 1), new Color(1f, .83f, .43f, .14f));
                GUI.Label(new Rect(r.x + 8 * _k, r.y, 44 * _k, r.height), "<color=#ffd36e><b>[팀]</b></color>", Sz(_label, 12));
                var ev = Event.current;
                if (ev.type == EventType.KeyDown && GUI.GetNameOfFocusedControl() == "tcChatInput")
                {
                    if ((ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter) && Now - _chatOpenedAt > .15) { SubmitChat(); ev.Use(); return; }   // 채팅을 연 Enter 가 곧바로 전송으로 새지 않게
                    if (ev.keyCode == KeyCode.Escape) { CloseChat(true); ev.Use(); return; }
                }
                var tfStyle = new GUIStyle(GUI.skin.textField) { fontSize = Mathf.RoundToInt(14 * _k), alignment = TextAnchor.MiddleLeft };
                tfStyle.normal.background = tfStyle.focused.background = tfStyle.active.background = tfStyle.hover.background = null;
                tfStyle.normal.textColor = tfStyle.focused.textColor = tfStyle.active.textColor = tfStyle.hover.textColor = new Color(.96f, .94f, 1f);
                GUI.SetNextControlName("tcChatInput");
                _input = GUI.TextField(new Rect(r.x + 54 * _k, r.y + 3 * _k, r.width - 130 * _k, r.height - 6 * _k), _input, CrewChat.MaxLen, tfStyle);
                if (_focusPending) { GUI.FocusControl("tcChatInput"); _focusPending = false; }
                GUI.Label(new Rect(r.xMax - 72 * _k, r.y, 64 * _k, r.height), $"<color=#7a7088>{_input.Length}/{CrewChat.MaxLen}</color>", Sz(_label, 11, TextAnchor.MiddleRight));
                if (_input.Length == 0) GUI.Label(new Rect(r.x + 58 * _k, r.y, r.width - 140 * _k, r.height), "<color=#66607a>메시지 입력… (Enter 전송 · Esc 취소)</color>", Sz(_label, 12));
            }
        }
        void DrawChatBubbles()
        {
            var C = _sim.Chat; double t = _sim.RunTime; bool reduced = Feedback.Instance != null && Feedback.Instance.ReducedMotion;
            foreach (var kv in C.Bubbles)
            {
                var b = kv.Value; double age = t - b.T, left = CrewChat.BubbleSec - age; if (left <= 0) continue;
                Vec2? pos = null; bool mine = false, ai = kv.Key.StartsWith("ai:");
                if (ai) { foreach (var m in _sim.Crew.Members) if ("ai:" + m.Id == kv.Key) { pos = m.Position; break; } }
                else if (kv.Key == CrewChat.MySeat) { pos = _sim.Player.Position; mine = true; }
                if (pos == null) continue;
                if (!mine) { var (c, r) = WorldGrid.ToCell(pos.Value); if (_sim.Los != null && !_sim.Los.IsSeen(c, r)) continue; }   // 안개 속 동료 위치 노출 금지
                var s = WorldToGui(pos.Value);
                if (s.x < -40 || s.x > Screen.width + 40 || s.y < 0 || s.y > Screen.height + 40) continue;
                float alpha = left < CrewChat.BubbleFade ? Mathf.Max(0, (float)(left / CrewChat.BubbleFade)) : 1;
                float pop = reduced ? 1 : age < CrewChat.BubblePop ? .6f + .4f * Mathf.Sin((float)(age / CrewChat.BubblePop) * Mathf.PI / 2) : 1;
                var st = Sz(_small, 13, TextAnchor.MiddleCenter, new Color(.23f, .14f, .09f, alpha), FontStyle.Bold); st.wordWrap = true;
                float maxW = 210 * _k;
                float w = Mathf.Min(maxW, st.CalcSize(new GUIContent(b.Text)).x) + 22 * _k;
                float h = st.CalcHeight(new GUIContent(b.Text), w - 22 * _k) + 12 * _k;
                float cellPx = (float)(Screen.height / (_cam.orthographicSize * 2));
                float tailY = s.y - cellPx * (.5f + 1.6f + (ai ? .35f : 0));
                float bx = Mathf.Clamp(s.x - w / 2, 8 * _k, Screen.width - w - 8 * _k), by = Mathf.Max(8 * _k, tailY - 9 * _k - h);
                var m0 = GUI.matrix; GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), new Vector2(s.x, tailY));
                Fill(new Rect(bx + 2, by + 3, w, h), new Color(.08f, .04f, .06f, .45f * alpha));
                Fill(new Rect(bx, by, w, h), new Color(1f, .96f, .88f, alpha));
                Fill(new Rect(bx, by, w, 2 * _k), Hex(CrewChat.SeatColor(kv.Key)) * new Color(1, 1, 1, alpha * .9f));
                Fill(new Rect(s.x - 5 * _k, by + h, 10 * _k, 8 * _k), new Color(1f, .96f, .88f, alpha));   // 꼬리
                if (!_bubbleScripts.TryGetValue(b.Text, out var script))
                {
                    script = CRT.DynamicDialogueText.Compile(b.Text, false);
                    _bubbleScripts[b.Text] = script;
                }
                GUI.DynamicLabel(new Rect(bx + 11 * _k, by + 6 * _k, w - 22 * _k, h - 12 * _k), script, st, (float)age, false, reduced);
                GUI.matrix = m0;
            }
        }

        // ── 퀵크래프트 (원형 제작 휠 · 상세 카드 · 배치 미리보기)
        Texture2D Tex(string n) => _tex.TryGetValue(n, out var t) ? t : null;
        void NineSlice(Rect r, Texture2D tex, float border, float scale)
        {
            if (tex == null) { Fill(r, new Color(.08f, .06f, .12f, .92f)); return; }
            float b = border * scale; float u = border / tex.width, v = border / tex.height;
            void D(Rect dst, Rect uv) => GUI.DrawTextureWithTexCoords(dst, tex, uv);
            D(new Rect(r.x, r.y, b, b), new Rect(0, 1 - v, u, v));
            D(new Rect(r.x + b, r.y, r.width - 2 * b, b), new Rect(u, 1 - v, 1 - 2 * u, v));
            D(new Rect(r.xMax - b, r.y, b, b), new Rect(1 - u, 1 - v, u, v));
            D(new Rect(r.x, r.y + b, b, r.height - 2 * b), new Rect(0, v, u, 1 - 2 * v));
            D(new Rect(r.x + b, r.y + b, r.width - 2 * b, r.height - 2 * b), new Rect(u, v, 1 - 2 * u, 1 - 2 * v));
            D(new Rect(r.xMax - b, r.y + b, b, r.height - 2 * b), new Rect(1 - u, v, u, 1 - 2 * v));
            D(new Rect(r.x, r.yMax - b, b, b), new Rect(0, 0, u, v));
            D(new Rect(r.x + b, r.yMax - b, r.width - 2 * b, b), new Rect(u, 0, 1 - 2 * u, v));
            D(new Rect(r.xMax - b, r.yMax - b, b, b), new Rect(1 - u, 0, u, v));
        }
        string CostText(CraftRecipe r, bool large)
        {
            var L = _sim.Loot; var sb = new System.Text.StringBuilder();
            if (r.Pulp > 0) sb.Append(L.Pulp < r.Pulp ? $"<color=#ff718a>PULP {(large ? L.Pulp + " / " : "")}{r.Pulp}</color>" : $"PULP {(large ? L.Pulp + " / " : "")}{r.Pulp}");
            if (r.Bloom > 0) sb.Append((sb.Length > 0 ? "   " : "") + (L.Bloom < r.Bloom ? $"<color=#ff718a>BLOOM {(large ? L.Bloom + " / " : "")}{r.Bloom}</color>" : $"BLOOM {(large ? L.Bloom + " / " : "")}{r.Bloom}"));
            return sb.ToString();
        }
        void DrawCraft()
        {
            var Cf = _sim.Craft;
            if (Cf.Using != null)
            {
                var s = WorldToGui(_sim.Player.Position);
                Fill(new Rect(s.x - 40 * _k, s.y + 36 * _k, 80 * _k, 6 * _k), new Color(0, 0, 0, .55f));
                Fill(new Rect(s.x - 40 * _k, s.y + 36 * _k, 80 * _k * (1 - (float)(Cf.Using.T / .7)), 6 * _k), new Color(.6f, .95f, .55f));
            }
            if (Cf.Phase == CraftPhase.Placing && Cf.Placement != null)
            {
                var q = Cf.Placement;
                if (q.World != null)
                {
                    var s = WorldToGui(q.World.Value);
                    float cellPx = (float)(Screen.height / (_cam.orthographicSize * 2));
                    float w = cellPx * q.FootW * .7071f, h = cellPx * q.FootH * .3536f;
                    var m0 = GUI.matrix; GUIUtility.RotateAroundPivot(-IsometricProjection.AngleToRender(q.Angle) * Mathf.Rad2Deg, s);
                    Fill(new Rect(s.x - w / 2, s.y - h / 2, w, h), q.Valid ? new Color(.5f, .92f, .82f, .22f) : new Color(1f, .44f, .54f, .22f));
                    GUI.matrix = m0;
                    var icon = Tex(q.Recipe.Icon);
                    if (icon != null) { GUI.color = new Color(1, 1, 1, q.Valid ? .95f : .55f); GUI.DrawTexture(new Rect(s.x - cellPx * .45f, s.y - cellPx * .45f, cellPx * .9f, cellPx * .9f), icon, ScaleMode.ScaleToFit); GUI.color = Color.white; }
                    var st = Sz(_small, 12, TextAnchor.MiddleCenter, q.Valid ? new Color(.6f, .95f, .85f) : new Color(1f, .6f, .68f), FontStyle.Bold);
                    GUI.Label(new Rect(s.x - 120 * _k, s.y + h / 2 + 4 * _k, 240 * _k, 20 * _k), Cf.PlacementLabel, st);
                }
                return;
            }
            if (Cf.Phase != CraftPhase.Wheel) return;
            // 배경막 — HUD 는 가리지 않고 월드만 살짝 낮춘다
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(.02f, .01f, .04f, .28f));
            float size = Mathf.Clamp(Screen.height * .56f, 390 * _k, 560 * _k);
            var c = new Vector2(Screen.width * .5f, Screen.height * .42f);
            for(int i=0;i<6;i++)
            {
                var original=GUI.matrix;GUIUtility.RotateAroundPivot(-90+i*60,c);
                Fill(new Rect(c.x+35*_k,c.y-1*_k,size*.34f-35*_k,2*_k),CRT.UiThemeProfile.Dim);GUI.matrix=original;
            }
            GUI.Panel(new Rect(c.x-36*_k,c.y-36*_k,72*_k,72*_k),CRT.UiThemeProfile.Panel,CRT.UiThemeProfile.Frame);
            GUI.Label(new Rect(c.x-36*_k,c.y-36*_k,72*_k,72*_k),"C",Sz(_bold,26));
            float slot = Mathf.Clamp(Screen.height * .14f, 108 * _k, 132 * _k);
            var sel = QuickCraftSystem.ById(Cf.Selected) ?? QuickCraftSystem.Recipes[0];
            for (int i = 0; i < 6; i++)
            {
                var r = QuickCraftSystem.Recipes[i]; float a = -Mathf.PI / 2 + i * Mathf.PI / 3;
                var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * size * .34f;
                bool isSel = r.Id == sel.Id, ok = Cf.Can(r); float sc = isSel ? 1.045f : 1f;
                var rc = new Rect(p.x - slot * sc / 2, p.y - slot * sc / 2, slot * sc, slot * sc);
                GUI.Panel(rc,CRT.UiThemeProfile.Panel,isSel?CRT.UiThemeProfile.Amber:CRT.UiThemeProfile.Dim,isSel?2:1);
                var icon = Tex(r.Icon);
                if (icon != null)
                {
                    GUI.color = ok ? Color.white : new Color(.45f, .42f, .5f, .58f);
                    GUI.DrawTexture(new Rect(rc.x + rc.width * .18f, rc.y + rc.height * .17f, rc.width * .64f, rc.height * .64f), icon, ScaleMode.ScaleToFit);
                    GUI.color = Color.white;
                }
                if(isSel)GUI.Panel(new Rect(rc.x+16*_k,rc.y+7*_k,rc.width-32*_k,3*_k),CRT.UiThemeProfile.Amber,Color.clear,0,false);
                float costX=rc.x+16*_k;
                void CostIcon(string name,int amount,int owned)
                {
                    if(amount<=0)return;var tex=Tex(name);
                    if(tex!=null)GUI.DrawTexture(new Rect(costX,rc.yMax-26*_k,20*_k,20*_k),tex,ScaleMode.ScaleToFit);
                    GUI.Label(new Rect(costX+22*_k,rc.yMax-28*_k,36*_k,24*_k),amount.ToString(),Sz(_small,18,TextAnchor.MiddleLeft,owned>=amount?CRT.UiThemeProfile.Amber:CRT.UiThemeProfile.Critical));costX+=56*_k;
                }
                CostIcon("currency-pulp",r.Pulp,_sim.Loot.Pulp);CostIcon("currency-bloom",r.Bloom,_sim.Loot.Bloom);
                GUI.Label(new Rect(rc.x - 20 * _k, rc.y - 2 * _k, 30 * _k, 18 * _k), $"<color=#aaa>{i + 1}</color>", Sz(_small, 11, TextAnchor.MiddleCenter));
            }
            // 상세 카드 — 휠 오른쪽 24px, 280×374
            float pw = 380 * _k, ph = 440 * _k; var pr = new Rect(c.x + size / 2 + 24 * _k, c.y - ph / 2, pw, ph);
            var safe=CRT.CrtSafeArea.Calculate(Screen.width,Screen.height,Screen.safeArea,CRT.CRTDisplayController.Instance!=null?CRT.CRTDisplayController.Instance.Effective.safeAreaInset:.055f);
            if (pr.xMax > safe.xMax) pr.x = safe.xMax - pw;
            GUI.Panel(pr,CRT.UiThemeProfile.Panel,CRT.UiThemeProfile.Frame);
            float px = pr.x + 28 * _k, py = pr.y + 26 * _k, iw = pr.width - 56 * _k;
            var dIcon = Tex(sel.Icon); if (dIcon != null) GUI.DrawTexture(new Rect(px, py, 64 * _k, 64 * _k), dIcon, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(px + 72 * _k, py, iw - 72 * _k, 28 * _k), $"<b>{sel.Name}</b>", Sz(_label, 19, TextAnchor.MiddleLeft));
            var descSt = Sz(_label, 18, TextAnchor.UpperLeft,CRT.UiThemeProfile.Frame); descSt.wordWrap = true;
            GUI.Label(new Rect(px + 72 * _k, py + 34 * _k, iw - 72 * _k, 96 * _k), sel.Desc, descSt);
            GUI.Label(new Rect(px, py + 134 * _k, iw, 54 * _k), sel.Effect, descSt);
            GUI.Label(new Rect(px, py + 194 * _k, iw, 28 * _k), CostText(sel, true), Sz(_label, 18, null, null, FontStyle.Bold));
            string why = Cf.Reason(sel); bool can = why.Length == 0;
            GUI.Label(new Rect(px, py + 226 * _k, iw, 28 * _k), can ? "<color=#7febd0>제작 가능</color>" : $"<color=#ff8da8>{why}</color>", Sz(_label, 18, null, null, FontStyle.Bold));
            var br = new Rect(px, pr.yMax - 132 * _k, iw, 52 * _k);
            GUI.Panel(br,CRT.UiThemeProfile.Panel,can?CRT.UiThemeProfile.Amber:CRT.UiThemeProfile.Dim);
            GUI.Label(br, sel.Kind == CraftKind.Place ? "배치 준비" : "제작하기", Sz(_bold, 22, TextAnchor.MiddleCenter,can?CRT.UiThemeProfile.Amber:CRT.UiThemeProfile.Dim));
            GUI.color = Color.white;
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && br.Contains(Event.current.mousePosition)) { Cf.ConfirmSelection(); Event.current.Use(); }
            GUI.Label(new Rect(px, pr.yMax - 64 * _k, iw, 50 * _k), "C 닫기 · 1~6 / 마우스 선택\n클릭 / SPACE 제작", Sz(_small, 18, TextAnchor.MiddleCenter));
        }

        void LateUpdate() { if (_craftView != null && _sim != null && _sim.World != null) _craftView.Render(_sim.Craft, _active()); }
    }

    /// <summary>퀵크래프트 설치물 월드 표현 — 아이콘 스프라이트 + 수명/내구 링 (원본 drawObjects).</summary>
    public sealed class CraftView : MonoBehaviour
    {
        readonly Dictionary<string, Sprite> _icons = new Dictionary<string, Sprite>();
        readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>();
        Sprite _ring;
        public void Bind(Dictionary<string, Texture2D> tex)
        {
            foreach (var kv in tex) if (kv.Key.StartsWith("icon-")) _icons[kv.Key] = Sprite.Create(kv.Value, new Rect(0, 0, kv.Value.width, kv.Value.height), new Vector2(.5f, .5f), kv.Value.width);
            _ring = ProcSprites.Ring(64, .11f);
        }
        SpriteRenderer Rent(int i)
        {
            while (_pool.Count <= i) { var go = new GameObject("craft"); go.transform.SetParent(transform, false); _pool.Add(go.AddComponent<SpriteRenderer>()); }
            var s = _pool[i]; s.gameObject.SetActive(true); s.transform.rotation = Quaternion.identity; return s;
        }
        void Set(SpriteRenderer sr, Vec2 at, Sprite s, Color c, float w, float h, float rotDeg, int order)
        {
            sr.transform.position = IsometricProjection.ToRender3(at); sr.sprite = s; sr.color = c; sr.transform.localScale = new Vector3(w, h, 1); sr.transform.rotation = Quaternion.Euler(0, 0, rotDeg); sr.sortingOrder = order;
        }
        Sprite Icon(string n) => _icons.TryGetValue(n, out var s) ? s : null;
        public void Render(QuickCraftSystem cf, bool active)
        {
            int i = 0;
            if (active)
            {
                foreach (var t in cf.Turrets)
                {
                    Set(Rent(i++), t.At, Icon("icon-auto-turret"), new Color(1, 1, 1, .96f), 1.08f, 1.08f, 0, 28);
                    Set(Rent(i++), t.At, _ring, new Color(1f, .83f, .43f, .35f + .65f * (float)(t.Life / t.MaxLife)), 1.28f, 1.28f, 0, 29);
                }
                foreach (var b in cf.Barricades)
                {
                    Set(Rent(i++), b.At, Icon("icon-folding-barricade"), new Color(1, 1, 1, .96f), 1.58f, .98f, IsometricProjection.AngleToRender(b.A) * Mathf.Rad2Deg, 28);
                    Set(Rent(i++), b.At, _ring, (b.Hp < b.MaxHp * .3 ? new Color(1f, .44f, .54f) : new Color(1f, .83f, .43f)) * new Color(1, 1, 1, .3f + .7f * (float)(b.Hp / b.MaxHp)), 1.16f, 1.16f, 0, 29);
                }
                foreach (var f in cf.Flares) Set(Rent(i++), f.At, Icon("icon-flare-bundle"), new Color(1, 1, 1, .88f), .64f, .64f, 0, 28);
                foreach (var q in cf.Charges)
                {
                    Set(Rent(i++), q.At, Icon("icon-shaped-charge"), new Color(1, 1, 1, .95f), .72f, .72f, 0, 28);
                    if (Mathf.Sin((float)q.Life * 18f) > 0) Set(Rent(i++), q.At + new Vec2(0, .26), ProcSprites.Circle(16, .8f), new Color(1f, .31f, .28f, .8f), .24f, .24f, 0, 30);
                }
            }
            for (; i < _pool.Count; i++) _pool[i].gameObject.SetActive(false);
        }
    }
}
