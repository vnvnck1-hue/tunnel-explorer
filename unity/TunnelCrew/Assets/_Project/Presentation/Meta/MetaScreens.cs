using System;
using System.Collections.Generic;
using System.Linq;
using TunnelCrew.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TunnelCrew.Presentation
{
    /// <summary>
    /// 런 바깥 화면 — 메인 메뉴 · 행성 지도 · 직업 선택 · 귀환 정산(요약 · 성장 지도 · 유물 보관고) · 일시정지.
    /// 원본 <c>#mainMenu · #infRoleModal · #infSettlementModal(3-view) · INF_PLANETS</c>. 허브는 메뉴형(§17.3-8).
    ///
    /// IMGUI 임시 구현 — HUD 와 같은 1080p 배율 규약. 정식 UGUI 배치는 M5 후반. 좌표는 편집기 값이 아니라 앵커 기준
    /// 적당한 위치(2026-09-06 결정). 성장 지도는 원본 GRID(1900×1620) 월드 좌표를 팬·줌으로 본다.
    /// </summary>
    public sealed class MetaScreens : MonoBehaviour
    {
        public enum Screen { MainMenu, Starmap, RoleSelect, Run, Settlement, Pause }
        public enum SettleView { Summary, Map, Relics }

        [SerializeField] RunBootstrap _run;
        [SerializeField] bool _startInMenu = true;

        public Screen Current { get; private set; } = Screen.MainMenu;
        SettleView _view = SettleView.Summary;
        bool _settleFromMenu;
        RoleId _selected = RoleId.Driller;
        int _planetIdx;

        // 성장 지도 카메라 (원본 nodeCamera)
        Vector2 _mapPan; float _mapZoom = 0.62f; bool _mapDragging; Vector2 _dragLast;
        NodeDef _hoverNode; string _fxNodeId; float _fxT; readonly List<string> _discovered = new List<string>();
        string _status = "";

        Texture2D _keyart, _bg, _white; readonly Dictionary<RoleId, Texture2D> _select = new Dictionary<RoleId, Texture2D>(), _badge = new Dictionary<RoleId, Texture2D>(), _portrait = new Dictionary<RoleId, Texture2D>();
        readonly Dictionary<string, Texture2D> _icons = new Dictionary<string, Texture2D>();
        float _k = 1f;

        // 원본 INF_PLANETS — 첫 행성만 열려 있다. 잠긴 10개는 장기 목표로 실루엣만 (§3.1)
        static readonly (string id, string name, string theme, bool locked, Color col)[] Planets =
        {
            ("purple", "퍼플 땅굴", "기본 광물 행성 · 위험 낮음 — 표층 붕괴 · 소형 군체 · 보스 암반 포식자", false, C("#8c5bd0")),
            ("brine", "청록 염굴", "염수 장판 · 부식 환경", true, C("#2e9a8b")),
            ("mantle", "적열 맨틀", "화산 지열 · 화염 생태", true, C("#e05a2b")),
            ("glacier", "백야 빙굴", "결빙 · 미끄러운 지반", true, C("#7fb8d8")),
            ("mycel", "균사 심림", "포자 군락 · 증식 지형", true, C("#6f9a3e")),
            ("dust", "황사 협곡", "유사(流沙) · 붕괴 협곡", true, C("#c89a4e")),
            ("storm", "뇌운 부유암", "정전기 폭풍 · 부유 지반", true, C("#7a86c8")),
            ("obsidian", "흑요 심연", "유리질 암반 · 반사 광선", true, C("#4a4a68")),
            ("coral", "산호 해저굴", "수압 · 발광 생태", true, C("#e8788a")),
            ("rust", "적철 폐광", "고대 기계 · 붕괴 갱도", true, C("#b8623a")),
            ("void", "공허 균열", "중력 이상 · 엔드게임", true, C("#3a2a5a")),
        };
        static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
        static readonly string[] RoleNames = { "드릴러", "거너", "스카우트", "엔지니어" };
        static readonly string[] RoleCodes = { "EXCAVATION", "FIRE SUPPORT", "RECON", "FORTIFICATION" };
        static readonly string[] RoleTags = { "길을 연다 · 기반암 균열 전문가", "적을 지운다 · 중화기와 계획 발파", "어둠을 연다 · 정찰과 기동", "공간을 만든다 · 설치물과 전력망" };
        static readonly string[] RoleGear = { "고압 중형 드릴 · 리벳 권총", "파쇄 발사기 · 벨트식 중화기", "휴대 절삭기 · 정찰 카빈", "공학 커터·전력 노드 · 센트리·서비스 총기" };
        static readonly (double dig, double gun, double dash)[] RoleStats = { (2, .65, 1), (0, 1.5, .9), (.35, 1, 1.4), (.75, .9, 1) };
        static readonly Color[] RoleColors = { C("#ffd36e"), C("#ff8d72"), C("#7febd0"), C("#c7a0ff") };

        void Start()
        {
            if (_run == null) _run = FindFirstObjectByType<RunBootstrap>();
            _white = Texture2D.whiteTexture;
            _keyart = Resources.Load<Texture2D>("UI/keyart-main");
            _bg = Resources.Load<Texture2D>("UI/bg-cavern");
            foreach (RoleId r in Enum.GetValues(typeof(RoleId)))
            {
                string n = r.ToString().ToLowerInvariant();
                _select[r] = Resources.Load<Texture2D>("UI/select-" + n);
                _badge[r] = Resources.Load<Texture2D>("UI/badge-" + n);
                _portrait[r] = Resources.Load<Texture2D>("UI/portrait-" + n);
            }
            foreach (var t in Resources.LoadAll<Texture2D>("UI/icons")) _icons[t.name] = t;

            if (_run != null)
            {
                _run.ResultDismissed += () => OpenSettlement(fromMenu: false);
                _run.PauseMenuRequested += () => Current = Screen.Pause;
                if (_startInMenu) { _run.SuspendRun(); Current = Screen.MainMenu; }
                else Current = Screen.Run;
            }
        }

        // ───────────────────────────── 화면 전환
        void GoMenu() { _run?.SuspendRun(); Current = Screen.MainMenu; MetaStore.Save(); }
        void GoStarmap() { Current = Screen.Starmap; _planetIdx = 0; }
        void GoRoleSelect() { Current = Screen.RoleSelect; }
        void Launch() { _run.LaunchRun(_selected); Current = Screen.Run; }
        void OpenSettlement(bool fromMenu)
        {
            _settleFromMenu = fromMenu; _view = fromMenu ? SettleView.Map : SettleView.Summary; Current = Screen.Settlement;
            _run?.SuspendRun(); _status = ""; _discovered.Clear();
            CenterMap();
        }
        void CenterMap() { _mapZoom = 0.62f; _mapPan = Vector2.zero; }

        // ───────────────────────────── 입력
        void Update()
        {
            var kb = Keyboard.current; if (kb == null) return;
            switch (Current)
            {
                case Screen.MainMenu:
                    if (kb.enterKey.wasPressedThisFrame || kb.digit1Key.wasPressedThisFrame) GoStarmap();
                    else if (kb.digit2Key.wasPressedThisFrame) OpenSettlement(fromMenu: true);
                    else if (kb.digit3Key.wasPressedThisFrame) { OpenSettlement(fromMenu: true); _view = SettleView.Relics; }
                    break;
                case Screen.Starmap:
                    if (kb.escapeKey.wasPressedThisFrame) GoMenu();
                    else if (kb.enterKey.wasPressedThisFrame && !Planets[_planetIdx].locked) GoRoleSelect();
                    else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) _planetIdx = (_planetIdx + Planets.Length - 1) % Planets.Length;
                    else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) _planetIdx = (_planetIdx + 1) % Planets.Length;
                    break;
                case Screen.RoleSelect:
                    if (kb.escapeKey.wasPressedThisFrame) GoStarmap();
                    else if (kb.digit1Key.wasPressedThisFrame) _selected = RoleId.Driller;
                    else if (kb.digit2Key.wasPressedThisFrame) _selected = RoleId.Gunner;
                    else if (kb.digit3Key.wasPressedThisFrame) _selected = RoleId.Scout;
                    else if (kb.digit4Key.wasPressedThisFrame) _selected = RoleId.Engineer;
                    else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) _selected = (RoleId)(((int)_selected + 3) % 4);
                    else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) _selected = (RoleId)(((int)_selected + 1) % 4);
                    else if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) Launch();
                    break;
                case Screen.Settlement:
                    if (kb.digit1Key.wasPressedThisFrame) _view = SettleView.Summary;
                    else if (kb.digit2Key.wasPressedThisFrame) _view = SettleView.Map;
                    else if (kb.digit3Key.wasPressedThisFrame) _view = SettleView.Relics;
                    else if (kb.escapeKey.wasPressedThisFrame)
                    {
                        // 원본 infSettleEscape — 요약으로, 요약에서는 메뉴로
                        if (_view != SettleView.Summary && !_settleFromMenu) _view = SettleView.Summary; else GoMenu();
                    }
                    else if (kb.enterKey.wasPressedThisFrame && _view == SettleView.Summary) GoMenu();
                    else if (kb.rKey.wasPressedThisFrame && _view == SettleView.Map) CenterMap();
                    if (_fxT > 0) _fxT -= Time.unscaledDeltaTime;
                    break;
                case Screen.Pause:
                    if (kb.escapeKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) { _run.SetPaused(false); Current = Screen.Run; }
                    else if (kb.mKey.wasPressedThisFrame) { _run.Sim.EndRun(false, "원정 포기"); _run.SetPaused(false); Current = Screen.Run; }
                    break;
                case Screen.Run:
                    if (_run != null && _run.Paused) Current = Screen.Pause;
                    break;
            }
        }

        // ───────────────────────────── 그리기
        Rect R(float x, float y, float w, float h) => new Rect(x * _k, y * _k, w * _k, h * _k);
        GUIStyle St(int px, FontStyle fs = FontStyle.Normal, TextAnchor a = TextAnchor.MiddleLeft, bool wrap = false, Color? col = null)
        {
            var s = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(px * _k), fontStyle = fs, alignment = a, richText = true, wordWrap = wrap };
            s.normal.textColor = col ?? new Color(.97f, .95f, .9f);
            return s;
        }
        void Fill(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, _white); GUI.color = Color.white; }
        void Panel(Rect r, float a = .62f) => Fill(r, new Color(.05f, .04f, .09f, a));
        bool Button(Rect r, string label, bool enabled = true, Color? accent = null)
        {
            var acc = accent ?? new Color(.78f, .63f, 1f);
            bool hover = r.Contains(Event.current.mousePosition);
            Fill(r, enabled ? (hover ? new Color(.22f, .18f, .32f) : new Color(.14f, .12f, .2f)) : new Color(.1f, .1f, .12f));
            Fill(new Rect(r.x, r.y, 5 * _k, r.height), enabled ? acc : new Color(.3f, .3f, .3f));
            GUI.Label(r, label, St(22, FontStyle.Bold, TextAnchor.MiddleCenter, false, enabled ? null : new Color(.5f, .5f, .55f)));
            return enabled && hover && Event.current.type == EventType.MouseDown && Event.current.button == 0;
        }

        void OnGUI()
        {
            if (Current == Screen.Run) return;
            _k = UnityEngine.Screen.height / 1080f;
            float W = UnityEngine.Screen.width / _k, H = 1080f;
            switch (Current)
            {
                case Screen.MainMenu: DrawMainMenu(W, H); break;
                case Screen.Starmap: DrawStarmap(W, H); break;
                case Screen.RoleSelect: DrawRoleSelect(W, H); break;
                case Screen.Settlement: DrawSettlement(W, H); break;
                case Screen.Pause: DrawPause(W, H); break;
            }
        }

        void DrawBackdrop(Texture2D tex, float W, float H, float dim)
        {
            if (tex != null) GUI.DrawTexture(new Rect(0, 0, UnityEngine.Screen.width, UnityEngine.Screen.height), tex, ScaleMode.ScaleAndCrop);
            Fill(new Rect(0, 0, UnityEngine.Screen.width, UnityEngine.Screen.height), new Color(0, 0, 0, dim));
        }

        // ── 메인 메뉴 (원본 #mainMenu: 키아트 + 모드 버튼 + 기록 한 줄)
        void DrawMainMenu(float W, float H)
        {
            DrawBackdrop(_keyart, W, H, .35f);
            var meta = MetaStore.Load();
            GUI.Label(R(80, 90, 900, 90), "<b>땅굴 크루</b>", St(72, FontStyle.Bold));
            GUI.Label(R(84, 176, 900, 36), "TUNNEL CREW · 무한 모드 · Unity 포팅 (M5 그레이박스)", St(20, FontStyle.Normal, TextAnchor.MiddleLeft, false, new Color(.8f, .78f, .85f)));
            float x = 80, y = 300, w = 560, h = 78, gap = 18;
            if (Button(R(x, y, w, h), "<size=" + Mathf.RoundToInt(15 * _k) + ">01</size>   출격 — 행성 지도   <color=#aaa>Enter</color>")) GoStarmap(); y += h + gap;
            if (Button(R(x, y, w, h), "<size=" + Mathf.RoundToInt(15 * _k) + ">02</size>   성장 지도 · 영구 노드   <color=#aaa>2</color>")) OpenSettlement(true); y += h + gap;
            if (Button(R(x, y, w, h), "<size=" + Mathf.RoundToInt(15 * _k) + ">03</size>   유물 보관고   <color=#aaa>3</color>")) { OpenSettlement(true); _view = SettleView.Relics; } y += h + gap;
            if (Button(R(x, y, w, h), "<size=" + Mathf.RoundToInt(15 * _k) + ">04</size>   종료", true, new Color(.6f, .6f, .65f))) Application.Quit(); y += h + gap;
            Panel(R(x, H - 120, 900, 70), .55f);
            int nodes = PermanentNodes.All.Count(n => meta.RankOf(n.Id) > 0);
            GUI.Label(R(x + 18, H - 116, 880, 62), $"최고 심층 <b>{(meta.bestDepth > 0 ? meta.bestDepth.ToString() : "-")}</b>   보관 코어 <b>{meta.bankedCores}</b>   영구 노드 <b>{nodes}</b>/80   유물 <b>{meta.relicOwned.Count}</b>/33   생환 {meta.escapes} · 보스 {meta.totalBosses}", St(19, FontStyle.Normal, TextAnchor.MiddleLeft, true));
        }

        // ── 행성 지도 (원본 INF_PLANETS 11개 — 열린 행성 1, 실루엣 10)
        void DrawStarmap(float W, float H)
        {
            DrawBackdrop(_bg, W, H, .6f);
            GUI.Label(R(80, 60, 900, 60), "<b>행성 지도</b>   <color=#aaa>← → 선택 · Enter 출격 · Esc 메뉴</color>", St(34, FontStyle.Bold));
            float cx = W * .5f, cy = H * .52f, rx = W * .38f, ry = 300;
            for (int i = 0; i < Planets.Length; i++)
            {
                var p = Planets[i];
                float a = Mathf.PI * 2 * i / Planets.Length - Mathf.PI * .5f;
                float px = cx + Mathf.Cos(a) * rx, py = cy + Mathf.Sin(a) * ry;
                float r = p.locked ? 30 : 52;
                bool sel = i == _planetIdx;
                var col = p.locked ? new Color(p.col.r, p.col.g, p.col.b, .35f) : p.col;
                if (sel) Fill(R(px - r - 8, py - r - 8, (r + 8) * 2, (r + 8) * 2), new Color(1f, .83f, .43f, .35f));
                Fill(R(px - r, py - r, r * 2, r * 2), col);
                GUI.Label(R(px - 120, py + r + 6, 240, 30), p.locked ? $"<color=#777>{p.name}</color>" : $"<b>{p.name}</b>", St(sel ? 20 : 17, FontStyle.Normal, TextAnchor.MiddleCenter));
                var rect = R(px - r, py - r, r * 2, r * 2);
                if (rect.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown) { _planetIdx = i; if (!p.locked && Event.current.clickCount >= 2) GoRoleSelect(); }
            }
            var cur = Planets[_planetIdx];
            Panel(R(cx - 420, H - 190, 840, 130), .7f);
            GUI.Label(R(cx - 400, H - 184, 800, 44), cur.locked ? $"<color=#999>{cur.name} — 잠김</color>" : $"<b>{cur.name}</b>", St(28, FontStyle.Bold, TextAnchor.MiddleCenter));
            GUI.Label(R(cx - 400, H - 140, 800, 70), cur.locked ? cur.theme + " · 이후 업데이트에서 열린다" : cur.theme + "\n<color=#ffd36e>Enter</color> 직업 선택으로", St(18, FontStyle.Normal, TextAnchor.MiddleCenter, true));
        }

        // ── 직업 선택 (원본 #infRoleModal — 4 카드: 코드 · 일러스트 · 이름 · 태그 · 장비 · 배율)
        void DrawRoleSelect(float W, float H)
        {
            DrawBackdrop(_bg, W, H, .55f);
            GUI.Label(R(80, 50, 1200, 60), "<b>무한 모드 · 직업 선택</b>   <color=#aaa>기본 장비와 역할 배율이 결정됩니다 · 런 도중 변경 불가</color>", St(30, FontStyle.Bold));
            float cw = 400, ch = 760, gap = 26, total = 4 * cw + 3 * gap, x0 = W * .5f - total * .5f, y0 = 140;
            var meta = MetaStore.Load();
            for (int i = 0; i < 4; i++)
            {
                var role = (RoleId)i; bool sel = role == _selected;
                var rc = R(x0 + i * (cw + gap), y0, cw, ch);
                Fill(rc, sel ? new Color(.16f, .13f, .24f, .96f) : new Color(.09f, .08f, .13f, .9f));
                if (sel) { Fill(new Rect(rc.x, rc.y, rc.width, 6 * _k), RoleColors[i]); Fill(new Rect(rc.x, rc.y, 6 * _k, rc.height), RoleColors[i]); }
                GUI.Label(new Rect(rc.x + 18 * _k, rc.y + 12 * _k, rc.width, 26 * _k), $"<color=#{ColorUtility.ToHtmlStringRGB(RoleColors[i])}>{RoleCodes[i]}</color>   <color=#888>[{i + 1}]</color>", St(15, FontStyle.Bold));
                if (_select.TryGetValue(role, out var art) && art != null)
                {
                    GUI.color = sel ? Color.white : new Color(.7f, .7f, .7f);
                    GUI.DrawTexture(new Rect(rc.x + 20 * _k, rc.y + 44 * _k, rc.width - 40 * _k, 430 * _k), art, ScaleMode.ScaleToFit);
                    GUI.color = Color.white;
                }
                float ty = rc.y + 486 * _k;
                GUI.Label(new Rect(rc.x + 18 * _k, ty, rc.width - 36 * _k, 40 * _k), $"<b>{RoleNames[i]}</b>", St(30, FontStyle.Bold));
                GUI.Label(new Rect(rc.x + 18 * _k, ty + 42 * _k, rc.width - 36 * _k, 30 * _k), RoleTags[i], St(15, FontStyle.Normal, TextAnchor.MiddleLeft, true, new Color(.8f, .78f, .85f)));
                GUI.Label(new Rect(rc.x + 18 * _k, ty + 80 * _k, rc.width - 36 * _k, 54 * _k), $"장비　{RoleGear[i]}", St(14, FontStyle.Normal, TextAnchor.UpperLeft, true));
                var st = RoleStats[i];
                GUI.Label(new Rect(rc.x + 18 * _k, ty + 140 * _k, rc.width - 36 * _k, 30 * _k), $"채굴 ×{st.dig:0.00}　전투 ×{st.gun:0.00}　기동 ×{st.dash:0.00}", St(15, FontStyle.Normal, TextAnchor.MiddleLeft, false, new Color(1f, .93f, .8f)));
                int ranks = PermanentNodes.All.Where(n => n.Owner == role.ToString().ToLowerInvariant()).Sum(n => meta.RankOf(n.Id));
                GUI.Label(new Rect(rc.x + 18 * _k, ty + 172 * _k, rc.width - 36 * _k, 30 * _k), $"<color=#aaa>영구 노드 랭크 {ranks} · 공용 {PermanentNodes.All.Where(n => n.Owner == "crew").Sum(n => meta.RankOf(n.Id))}</color>", St(14));
                if (rc.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown) { if (_selected == role && Event.current.clickCount >= 2) Launch(); _selected = role; }
                if (sel && Button(new Rect(rc.x + 18 * _k, rc.yMax - 70 * _k, rc.width - 36 * _k, 54 * _k), "선택 완료 · 출격   <color=#aaa>Enter</color>", true, RoleColors[i])) Launch();
            }
            GUI.Label(R(80, H - 60, 1200, 40), "<color=#aaa>Esc 행성 지도로</color>", St(16));
        }

        // ── 일시정지
        void DrawPause(float W, float H)
        {
            Fill(new Rect(0, 0, UnityEngine.Screen.width, UnityEngine.Screen.height), new Color(0, 0, 0, .55f));
            Panel(R(W * .5f - 300, H * .5f - 160, 600, 320), .8f);
            GUI.Label(R(W * .5f - 280, H * .5f - 140, 560, 50), "<b>일시정지</b>", St(34, FontStyle.Bold, TextAnchor.MiddleCenter));
            if (Button(R(W * .5f - 240, H * .5f - 60, 480, 64), "계속   <color=#aaa>Esc</color>")) { _run.SetPaused(false); Current = Screen.Run; }
            if (Button(R(W * .5f - 240, H * .5f + 20, 480, 64), "원정 포기 · 결과 화면   <color=#aaa>M</color>", true, new Color(1f, .44f, .54f))) { _run.Sim.EndRun(false, "원정 포기"); _run.SetPaused(false); Current = Screen.Run; }
            GUI.Label(R(W * .5f - 280, H * .5f + 100, 560, 40), $"<color=#aaa>{Planet.DepthLabel(_run.Sim.Depth)} · 장악도 {_run.Sim.Run.Dominance:P0} · 코어 {_run.Sim.Loot.Core}</color>", St(16, FontStyle.Normal, TextAnchor.MiddleCenter));
        }

        // ── 귀환 정산 (원본 #infSettlementModal 3-view)
        void DrawSettlement(float W, float H)
        {
            DrawBackdrop(_bg, W, H, .7f);
            var meta = MetaStore.Load();
            // 탭
            string[] tabs = { "1 요약", "2 성장 지도", "3 유물 보관고" };
            for (int i = 0; i < 3; i++)
            {
                bool on = (int)_view == i;
                if (Button(R(80 + i * 250, 40, 236, 52), on ? $"<b>{tabs[i]}</b>" : tabs[i], true, on ? new Color(1f, .83f, .43f) : new Color(.4f, .38f, .5f))) _view = (SettleView)i;
            }
            GUI.Label(R(W - 620, 40, 540, 52), $"보관 코어 <b><color=#7febd0>{meta.bankedCores}</color></b>   <color=#aaa>{(_settleFromMenu ? "Esc 메뉴" : "Esc 요약 · Enter 메뉴")}</color>", St(22, FontStyle.Normal, TextAnchor.MiddleRight));
            switch (_view)
            {
                case SettleView.Summary: DrawSummary(W, H, meta); break;
                case SettleView.Map: DrawNodeMap(W, H, meta); break;
                case SettleView.Relics: DrawRelicVault(W, H, meta); break;
            }
            if (!string.IsNullOrEmpty(_status)) GUI.Label(R(80, H - 50, W - 160, 36), _status, St(16, FontStyle.Normal, TextAnchor.MiddleLeft, false, new Color(1f, .83f, .43f)));
        }

        void DrawSummary(float W, float H, MetaState meta)
        {
            var sim = _run?.Sim;
            Panel(R(80, 120, W - 160, H - 220), .7f);
            float x = 120, y = 150;
            if (sim != null && !_settleFromMenu)
            {
                var st = sim.LastSettlement;
                GUI.Label(R(x, y, 1200, 60), sim.RunEscaped ? "<b>탈출 성공 — 귀환 정산</b>" : "<b>원정 종료 — 귀환 정산</b>", St(40, FontStyle.Bold)); y += 70;
                GUI.Label(R(x, y, 1400, 36), $"{sim.RunEndReason} · {Planet.DepthLabel(sim.Depth)} · {RoleNames[(int)_run.CurrentRole]}", St(20, FontStyle.Normal, TextAnchor.MiddleLeft, false, new Color(.8f, .78f, .85f))); y += 56;
                void Row(string k, string v) { GUI.Label(R(x, y, 420, 40), k, St(22)); GUI.Label(R(x + 420, y, 600, 40), v, St(22, FontStyle.Bold)); y += 44; }
                Row("도달 심층", sim.Depth.ToString());
                Row("처치 보스", sim.Run.BossesKilled.ToString());
                Row("파괴 블록", sim.World.BlocksBroken.ToString());
                Row("레벨 / 특성", $"{sim.Xp.Level} / {sim.Traits.PickLog.Count}장");
                Row(sim.RunEscaped ? "확보 코어" : "소실 코어", sim.RunEscaped ? $"<color=#7febd0>+{st.Returned}</color>" : $"<color=#ff8da8>-{st.Lost}</color>" + (st.Kept > 0 ? $"   <color=#7febd0>회수 보존 +{st.Kept}</color>" : "") + (st.Remote > 0 ? $"   <color=#ffd36e>원격 전송 {st.Remote}</color>" : ""));
                if (sim.LastUnlocks.Count > 0) Row("해금", string.Join(" · ", sim.LastUnlocks));
                y += 20;
                GUI.Label(R(x, y, 1400, 36), "<color=#aaa>런 성장(레벨·특성)은 다음 출격에서 초기화됩니다. 코어는 성장 지도(2)에서 영구 노드에 쓰세요.</color>", St(17)); y += 40;
            }
            else
            {
                GUI.Label(R(x, y, 1200, 60), "<b>기지 기록</b>", St(40, FontStyle.Bold)); y += 80;
            }
            GUI.Label(R(x, y, 1400, 36), $"최고 심층 {meta.bestDepth}   최고 파괴 {meta.bestBlocks}   누적 보스 {meta.totalBosses}   생환 {meta.escapes}   유물 {meta.relicOwned.Count}/33", St(19, FontStyle.Normal, TextAnchor.MiddleLeft, false, new Color(.8f, .78f, .85f)));
            if (Button(R(x, H - 190, 360, 64), "성장 지도로   <color=#aaa>2</color>")) _view = SettleView.Map;
            if (Button(R(x + 380, H - 190, 360, 64), "메인 메뉴   <color=#aaa>Enter</color>", true, new Color(.6f, .6f, .65f))) GoMenu();
        }

        // ── 성장 지도 (원본 SVG 노드 맵 — 팬·줌·발견·구매)
        void DrawNodeMap(float W, float H, MetaState meta)
        {
            var view = R(80, 110, W - 160, H - 180);
            Panel(view, .75f);
            GUI.BeginGroup(view);
            var ev = Event.current;
            var local = new Vector2(ev.mousePosition.x - view.x, ev.mousePosition.y - view.y);
            bool inside = local.x >= 0 && local.y >= 0 && local.x <= view.width && local.y <= view.height;
            // 카메라: 원본 월드 1900×1620 → 뷰. 기본 줌 .62, 드래그 팬, 휠 줌
            float sc = _mapZoom * _k;
            Vector2 origin = new Vector2(view.width * .5f - 950 * sc, view.height * .5f - 1000 * sc) + _mapPan * _k;
            Vector2 ToView(double x, double y) => origin + new Vector2((float)x, (float)y) * sc;
            if (inside && ev.type == EventType.ScrollWheel) { _mapZoom = Mathf.Clamp(_mapZoom - ev.delta.y * .04f, .35f, 1.4f); ev.Use(); }
            if (inside && ev.type == EventType.MouseDown && ev.button == 0 && _hoverNode == null) { _mapDragging = true; _dragLast = ev.mousePosition; }
            if (ev.type == EventType.MouseUp) _mapDragging = false;
            if (_mapDragging && ev.type == EventType.MouseDrag) { _mapPan += (ev.mousePosition - _dragLast) / _k; _dragLast = ev.mousePosition; }

            // 링크 (requires) — 보이는 노드 사이만
            foreach (var n in PermanentNodes.All)
            {
                if (!PermanentNodes.Visible(n, meta)) continue;
                foreach (var req in n.Requires)
                {
                    var p = PermanentNodes.ById(req); if (p == null || !PermanentNodes.Visible(p, meta)) continue;
                    bool lit = meta.RankOf(req) > 0;
                    DrawLine(ToView(p.X, p.Y), ToView(n.X, n.Y), lit ? new Color(.78f, .63f, 1f, .8f) : new Color(.4f, .38f, .5f, .5f), (lit ? 3f : 2f) * _k);
                }
            }
            // 직업 허브 뱃지
            foreach (var (role, hx, hy) in new[] { (RoleId.Driller, 675, 945), (RoleId.Gunner, 840, 835), (RoleId.Scout, 1060, 835), (RoleId.Engineer, 1225, 945) })
                if (_badge.TryGetValue(role, out var b) && b != null) { var c = ToView(hx, hy); float s = 72 * sc; GUI.DrawTexture(new Rect(c.x - s * .5f, c.y - s * .5f, s, s), b, ScaleMode.ScaleToFit); }
            // 노드
            _hoverNode = null;
            foreach (var n in PermanentNodes.All)
            {
                if (!PermanentNodes.Visible(n, meta)) continue;
                var c = ToView(n.X, n.Y);
                int rank = meta.RankOf(n.Id);
                bool can = PermanentNodes.CanBuy(n, meta), maxed = rank >= n.MaxRank, cap = n.SlotKey == "cap";
                float r = (cap ? 30 : n.SlotKey == "i" ? 26 : 22) * sc;
                var rect = new Rect(c.x - r, c.y - r, r * 2, r * 2);
                bool hover = inside && rect.Contains(ev.mousePosition);
                if (hover) _hoverNode = n;
                Color ring = maxed ? new Color(1f, .83f, .43f) : rank > 0 ? new Color(.78f, .63f, 1f) : can ? new Color(.5f, .92f, .82f) : new Color(.35f, .33f, .45f);
                if (_fxNodeId == n.Id && _fxT > 0) Fill(new Rect(c.x - r * 1.6f, c.y - r * 1.6f, r * 3.2f, r * 3.2f), new Color(1f, .83f, .43f, _fxT * .5f));
                Fill(new Rect(rect.x - 3 * _k, rect.y - 3 * _k, rect.width + 6 * _k, rect.height + 6 * _k), ring);
                Fill(rect, hover ? new Color(.25f, .2f, .35f) : new Color(.12f, .1f, .18f));
                if (_icons.TryGetValue("trait-icon-" + n.Icon + "-v1", out var ic)) { GUI.color = rank > 0 || can ? Color.white : new Color(.6f, .6f, .6f, .7f); GUI.DrawTexture(new Rect(rect.x + r * .25f, rect.y + r * .25f, r * 1.5f, r * 1.5f), ic, ScaleMode.ScaleToFit); GUI.color = Color.white; }
                // 랭크 핍
                for (int i = 0; i < n.MaxRank; i++) Fill(new Rect(c.x - (n.MaxRank * 8 * sc) * .5f + i * 8 * sc, c.y + r + 4 * sc, 6 * sc, 6 * sc), i < rank ? new Color(1f, .83f, .43f) : new Color(.3f, .3f, .35f));
                if (hover && ev.type == EventType.MouseDown && ev.button == 0)
                {
                    ev.Use();
                    if (MetaStore.TryBuyNode(n))
                    {
                        _fxNodeId = n.Id; _fxT = 1f;
                        _status = $"{n.Name} {rank + 1}랭크 — {n.EffectByRank[Math.Min(rank, n.EffectByRank.Length - 1)]}";
                        if (rank == 0) foreach (var rv in n.Reveal) _discovered.Add(rv);
                    }
                    else _status = !PermanentNodes.PrereqsMet(n, meta) ? "선행 노드가 필요합니다" : rank >= n.MaxRank ? "이미 최대 랭크입니다" : meta.bankedCores < n.CostFor(rank) ? $"코어가 부족합니다 (필요 {n.CostFor(rank)})" : "구매할 수 없습니다";
                }
            }
            GUI.EndGroup();
            // 툴팁
            if (_hoverNode != null)
            {
                var n = _hoverNode; int rank = meta.RankOf(n.Id);
                var tip = new Rect(Mathf.Min(ev.mousePosition.x + 18 * _k, UnityEngine.Screen.width - 460 * _k), Mathf.Min(ev.mousePosition.y + 18 * _k, UnityEngine.Screen.height - 220 * _k), 440 * _k, 200 * _k);
                Fill(tip, new Color(.06f, .05f, .1f, .96f));
                Fill(new Rect(tip.x, tip.y, tip.width, 4 * _k), new Color(.78f, .63f, 1f));
                GUILayout.BeginArea(new Rect(tip.x + 14 * _k, tip.y + 10 * _k, tip.width - 28 * _k, tip.height - 20 * _k));
                GUILayout.Label($"<color=#aaa>{n.Branch} · {n.Type}</color>   <color=#888>{(n.Owner == "crew" ? "크루 공용" : RoleNames[(int)(RoleId)Enum.Parse(typeof(RoleId), n.Owner, true)])}</color>", St(14));
                GUILayout.Label($"<b>{n.Name}</b>   {rank}/{n.MaxRank}", St(22, FontStyle.Bold));
                for (int i = 0; i < n.EffectByRank.Length; i++) GUILayout.Label((i < rank ? "<color=#ffd36e>● </color>" : "<color=#666>○ </color>") + n.EffectByRank[i], St(15, FontStyle.Normal, TextAnchor.MiddleLeft, true));
                GUILayout.Label(rank < n.MaxRank ? $"다음 랭크 <b>{n.CostFor(rank)}</b> 코어 · 클릭으로 구매" : "<color=#ffd36e>최대 랭크</color>", St(15));
                GUILayout.EndArea();
            }
            GUI.Label(R(100, H - 66, W - 200, 30), $"<color=#aaa>드래그 이동 · 휠 줌 · R 초기화 · 노드 클릭 구매   ·   보유 랭크 {PermanentNodes.All.Sum(n => meta.RankOf(n.Id))}/140</color>", St(15));
        }

        static Texture2D _lineTex;
        static void DrawLine(Vector2 a, Vector2 b, Color col, float width)
        {
            if (_lineTex == null) { _lineTex = new Texture2D(1, 1); _lineTex.SetPixel(0, 0, Color.white); _lineTex.Apply(); }
            var d = b - a; float len = d.magnitude; if (len < 1e-3f) return;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            var m = GUI.matrix; var c = GUI.color;
            GUIUtility.RotateAroundPivot(ang, a);
            GUI.color = col; GUI.DrawTexture(new Rect(a.x, a.y - width * .5f, len, width), _lineTex);
            GUI.color = c; GUI.matrix = m;
        }

        // ── 유물 보관고 (원본 relic vault — 소켓 5 + 보유 목록, 클릭 = 자동 장착/해제)
        void DrawRelicVault(float W, float H, MetaState meta)
        {
            Panel(R(80, 110, W - 160, H - 180), .75f);
            float x = 120, y = 140;
            GUI.Label(R(x, y, 1200, 44), "<b>유물 보관고</b>   <color=#aaa>클릭 = 장착/해제 · 소켓 4 (도굴왕의 왕관으로 5)</color>", St(28, FontStyle.Bold)); y += 60;
            int max = Relics.SocketMax(meta);
            for (int i = 0; i < 5; i++)
            {
                var rc = R(x + i * 190, y, 176, 90);
                bool open = i < max;
                Fill(rc, open ? new Color(.14f, .12f, .2f) : new Color(.08f, .08f, .1f));
                var id = meta.relicSockets[i];
                var rel = Relics.ById(id);
                if (rel != null)
                {
                    Fill(new Rect(rc.x, rc.y, rc.width, 5 * _k), TierColor(rel.Tier));
                    GUI.Label(new Rect(rc.x + 10 * _k, rc.y + 8 * _k, rc.width - 20 * _k, 40 * _k), $"<b>{rel.Name}</b>", St(17, FontStyle.Bold, TextAnchor.MiddleLeft, true));
                    GUI.Label(new Rect(rc.x + 10 * _k, rc.y + 50 * _k, rc.width - 20 * _k, 30 * _k), $"<color=#aaa>{rel.Kind}{(rel.Elem != RelicElem.None ? " · " + ElemName(rel.Elem) : "")}</color>", St(13));
                    if (rc.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown) { Relics.Unequip(meta, i); MetaStore.Save(); }
                }
                else GUI.Label(rc, open ? $"<color=#666>소켓 {i + 1}</color>" : "<color=#444>잠김 · 왕관</color>", St(16, FontStyle.Normal, TextAnchor.MiddleCenter));
            }
            y += 110;
            var res = Relics.Resonance(Relics.EquippedIds(meta));
            GUI.Label(R(x, y, 1400, 32), $"원소 공명   화염 {res[RelicElem.Fire]}   빙결 {res[RelicElem.Frost]}   뇌전 {res[RelicElem.Volt]}   대지 {res[RelicElem.Earth]}   <color=#aaa>(2개 → 1단 · 3개 → 2단 · 융합로 +1)</color>", St(17)); y += 50;
            // 보유 목록 — 등급별
            float cw = 300, ch = 120, gap = 14; int cols = Mathf.Max(1, (int)((W - 240) / (cw + gap)));
            int idx = 0;
            foreach (var rel in Relics.All.OrderByDescending(r => r.Tier).ThenBy(r => r.Elem))
            {
                bool owned = meta.relicOwned.Contains(rel.Id);
                bool equipped = Array.IndexOf(meta.relicSockets, rel.Id) >= 0;
                var rc = R(x + (idx % cols) * (cw + gap), y + (idx / cols) * (ch + gap), cw, ch);
                if (rc.yMax > (H - 90) * _k) break;
                Fill(rc, owned ? (equipped ? new Color(.2f, .16f, .3f) : new Color(.12f, .1f, .18f)) : new Color(.07f, .07f, .09f));
                Fill(new Rect(rc.x, rc.y, 5 * _k, rc.height), owned ? TierColor(rel.Tier) : new Color(.25f, .25f, .3f));
                GUI.Label(new Rect(rc.x + 14 * _k, rc.y + 6 * _k, rc.width - 28 * _k, 30 * _k), owned ? $"<b>{rel.Name}</b>{(equipped ? "  <color=#ffd36e>장착</color>" : "")}" : "<color=#555>??? — 미발굴</color>", St(17, FontStyle.Bold));
                GUI.Label(new Rect(rc.x + 14 * _k, rc.y + 34 * _k, rc.width - 28 * _k, 24 * _k), $"<color=#aaa>{TierName(rel.Tier)} · {rel.Kind}{(rel.Elem != RelicElem.None ? " · " + ElemName(rel.Elem) : "")}</color>", St(13));
                GUI.Label(new Rect(rc.x + 14 * _k, rc.y + 58 * _k, rc.width - 28 * _k, 58 * _k), owned ? rel.Desc : "<color=#555>광맥·보스·묻힌 유물에서 발굴</color>", St(13, FontStyle.Normal, TextAnchor.UpperLeft, true));
                if (owned && rc.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown) { Relics.EquipAuto(meta, rel.Id); MetaStore.Save(); _status = $"{rel.Name} {(Array.IndexOf(meta.relicSockets, rel.Id) >= 0 ? "장착" : "해제")}"; }
                idx++;
            }
        }
        static Color TierColor(int t) => t >= 4 ? new Color(1f, .83f, .43f) : t == 2 ? new Color(.78f, .63f, 1f) : new Color(.75f, .75f, .8f);
        static string TierName(int t) => t >= 4 ? "전설" : t == 2 ? "희귀" : "일반";
        static string ElemName(RelicElem e) => e switch { RelicElem.Fire => "화염", RelicElem.Frost => "빙결", RelicElem.Volt => "뇌전", RelicElem.Earth => "대지", _ => "" };
    }
}
