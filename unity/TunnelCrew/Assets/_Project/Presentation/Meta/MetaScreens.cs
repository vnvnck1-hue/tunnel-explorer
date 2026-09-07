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
        public enum Screen { MainMenu, Starmap, RoleSelect, Run, Settlement, Pause, Settings }
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

        // 화면 전환 와이프 — 원본 TCFX.wipe: 시트가 가로로 쓸고 지나간다. cover 260 · hold 40 · reveal 340 ms, 뒤로 가기는 반대 방향
        const float WipeCover = .26f, WipeHold = .04f, WipeReveal = .34f;
        float _wipeT = -1f; bool _wipeBack; Action _wipeMid; bool _wipeMidDone; bool _wipeLoading;
        Texture2D _loadingArt; float _loadingSpin;
        // 설정 (PlayerPrefs)
        int _resIdx; bool _fullscreen; bool _reducedMotion; float _bgm = .8f, _sfx = .9f; Resolution[] _resolutions;
        // 게임패드 리매핑 — 캡처 중인 액션(-1 이면 없음). 캡처 중엔 다음에 눌리는 배정 가능 버튼을 그 액션에 묶는다
        int _gpCapture = -1; float _gpCaptureT;

        Texture2D _keyart, _title, _bg, _white; readonly Dictionary<RoleId, Texture2D> _select = new Dictionary<RoleId, Texture2D>(), _badge = new Dictionary<RoleId, Texture2D>(), _portrait = new Dictionary<RoleId, Texture2D>();
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
            // 원본 v7.9.2 메인 메뉴가 쓰는 최신 리소스: 키아트 hero-tunnel-crew-keyart-v5(.webp → png 2048) · 로고 title-tunnel-crew-v2.
            // 런 밖 다른 화면(직업 선택·행성 지도·정산·설정)은 원본이 메뉴 위에 반투명 모달을 얹는 구조라 같은 키아트를 더 어둡게 깐다 (레거시 bg-tunnel-cavern 은 폐기).
            _keyart = Resources.Load<Texture2D>("UI/keyart-main");
            _title = Resources.Load<Texture2D>("UI/title-logo");
            _bg = _keyart;
            foreach (RoleId r in Enum.GetValues(typeof(RoleId)))
            {
                string n = r.ToString().ToLowerInvariant();
                _select[r] = Resources.Load<Texture2D>("UI/select-" + n);
                _badge[r] = Resources.Load<Texture2D>("UI/badge-" + n);
                _portrait[r] = Resources.Load<Texture2D>("UI/portrait-" + n);
            }
            foreach (var t in Resources.LoadAll<Texture2D>("UI/icons")) _icons[t.name] = t;
            _loadingArt = Resources.Load<Texture2D>("UI/loading-drill");
            LoadSettings();

            if (_run != null)
            {
                _run.ResultDismissed += () => OpenSettlement(fromMenu: false);
                _run.PauseMenuRequested += () => Current = Screen.Pause;
                if (_startInMenu) { _run.SuspendRun(); Current = Screen.MainMenu; }
                else Current = Screen.Run;
            }
        }

        // ───────────────────────────── 화면 전환
        /// <summary>디버그·스모크용 — 화면을 직접 연다 (키 입력 없이).</summary>
        public void Show(Screen s, SettleView view = SettleView.Summary)
        {
            switch (s)
            {
                case Screen.MainMenu: GoMenu(); break;
                case Screen.Starmap: GoStarmap(); break;
                case Screen.RoleSelect: GoRoleSelect(); break;
                case Screen.Settlement: OpenSettlement(fromMenu: true, view); break;
                case Screen.Settings: GoSettings(); break;
                case Screen.Run: Launch(); break;
                default: Current = s; break;
            }
        }
        /// <summary>와이프 뒤에 화면을 바꾼다. 감속 모드·이미 전환 중이면 즉시 실행 (원본 TCFX.wipe 규칙).</summary>
        void Wipe(Action mid, bool back = false, bool loading = false)
        {
            if (_reducedMotion || _wipeT >= 0) { mid(); return; }
            _wipeT = 0; _wipeBack = back; _wipeMid = mid; _wipeMidDone = false; _wipeLoading = loading;
        }
        void GoMenu() => Wipe(() => { _run?.SuspendRun(); Current = Screen.MainMenu; MetaStore.Save(); }, back: true);
        void GoStarmap() => Wipe(() => { Current = Screen.Starmap; _planetIdx = 0; });
        void GoRoleSelect() => Wipe(() => Current = Screen.RoleSelect);
        void GoSettings() => Wipe(() => Current = Screen.Settings);
        void Launch() => Wipe(() => { AudioDirector.Instance?.Deploy(); _run.LaunchRun(_selected); Current = Screen.Run; }, loading: true);
        /// <summary>관전 출격 — 선택 직업이 AI 리더, 나머지 3직업이 AI 크루 (원본 OBS.enter → infLaunchFromRoleSelect).</summary>
        void LaunchObserver() => LaunchObserver(_selected);
        /// <summary>리더 직업을 지정해 관전 출격 (스모크·외부 진입용). 출격은 항상 이 직업으로 나가야 편성(나머지 3직업)과 어긋나지 않는다.</summary>
        public void LaunchObserver(RoleId leader)
        {
            var obs = _run != null ? _run.Observer : null;
            if (obs == null) { _run?.Log("관전 모드를 시작할 수 없습니다"); return; }
            _selected = leader;
            obs.Arm(leader);
            Launch();
        }
        void OpenSettlement(bool fromMenu, SettleView? view = null) => Wipe(() =>
        {
            _settleFromMenu = fromMenu; _view = view ?? (fromMenu ? SettleView.Map : SettleView.Summary); Current = Screen.Settlement;
            _run?.SuspendRun(); _status = ""; _discovered.Clear();
            CenterMap();
        }, back: !fromMenu);
        void CenterMap() { _mapZoom = 0.62f; _mapPan = Vector2.zero; }

        // ───────────────────────────── 입력
        void Update()
        {
            // 와이프 진행 — 실시간. cover 끝에 화면을 바꾸고, hold 동안 로딩을 보여주고, reveal 로 걷어낸다
            if (_wipeT >= 0)
            {
                _wipeT += Time.unscaledDeltaTime;
                if (!_wipeMidDone && _wipeT >= WipeCover) { _wipeMidDone = true; _wipeMid?.Invoke(); }
                if (_wipeT >= WipeCover + WipeHold + WipeReveal) { _wipeT = -1; _wipeLoading = false; }
                _loadingSpin += Time.unscaledDeltaTime * 240f;
                return;   // 전환 중에는 입력을 받지 않는다
            }
            var kb = Keyboard.current; if (kb == null) return;
            // 런 밖 화면은 로비 앰비언스 (원본 BGM_ROUTE.useLobby)
            if (Current != Screen.Run && Current != Screen.Pause && AudioDirector.Instance != null && AudioDirector.Instance.Current != AudioDirector.Route.Lobby) AudioDirector.Instance.UseLobby();
            // 게임패드 — A 확인 · B 뒤로 · D패드/왼스틱 좌우 선택 (원본에는 없던 M7 항목)
            var gp = Gamepad.current;
            bool gA = gp != null && gp.buttonSouth.wasPressedThisFrame, gB = gp != null && gp.buttonEast.wasPressedThisFrame;
            bool gL = gp != null && (gp.dpad.left.wasPressedThisFrame || (gp.leftStick.left.wasPressedThisFrame)), gR = gp != null && (gp.dpad.right.wasPressedThisFrame || gp.leftStick.right.wasPressedThisFrame);
            switch (Current)
            {
                case Screen.MainMenu:
                    if (kb.enterKey.wasPressedThisFrame || kb.digit1Key.wasPressedThisFrame || gA) GoStarmap();
                    else if (kb.digit2Key.wasPressedThisFrame) GoRoleSelect();   // 무한 모드 직행
                    else if (kb.digit3Key.wasPressedThisFrame) OpenSettlement(fromMenu: true);
                    else if (kb.digit4Key.wasPressedThisFrame) OpenSettlement(fromMenu: true, SettleView.Relics);
                    else if (kb.digit5Key.wasPressedThisFrame) GoSettings();
                    break;
                case Screen.Settings:
                    if (kb.escapeKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || gA || gB) { SaveSettings(); GoMenu(); }
                    break;
                case Screen.Starmap:
                    if (kb.escapeKey.wasPressedThisFrame || gB) { AudioDirector.Instance?.Back(); GoMenu(); }
                    else if ((kb.enterKey.wasPressedThisFrame || gA) && !Planets[_planetIdx].locked) GoRoleSelect();
                    else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame || gL) { _planetIdx = (_planetIdx + Planets.Length - 1) % Planets.Length; AudioDirector.Instance?.Tick(); }
                    else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame || gR) { _planetIdx = (_planetIdx + 1) % Planets.Length; AudioDirector.Instance?.Tick(); }
                    break;
                case Screen.RoleSelect:
                    if (kb.escapeKey.wasPressedThisFrame || gB) { AudioDirector.Instance?.Back(); GoStarmap(); }
                    else if (kb.digit1Key.wasPressedThisFrame) _selected = RoleId.Driller;
                    else if (kb.digit2Key.wasPressedThisFrame) _selected = RoleId.Gunner;
                    else if (kb.digit3Key.wasPressedThisFrame) _selected = RoleId.Scout;
                    else if (kb.digit4Key.wasPressedThisFrame) _selected = RoleId.Engineer;
                    else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame || gL) { _selected = (RoleId)(((int)_selected + 3) % 4); AudioDirector.Instance?.Pick(); }
                    else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame || gR) { _selected = (RoleId)(((int)_selected + 1) % 4); AudioDirector.Instance?.Pick(); }
                    else if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || gA) Launch();
                    break;
                case Screen.Settlement:
                    if (kb.digit1Key.wasPressedThisFrame) _view = SettleView.Summary;
                    else if (kb.digit2Key.wasPressedThisFrame) _view = SettleView.Map;
                    else if (kb.digit3Key.wasPressedThisFrame) _view = SettleView.Relics;
                    else if (kb.escapeKey.wasPressedThisFrame || gB)
                    {
                        AudioDirector.Instance?.Back();
                        // 원본 infSettleEscape — 요약으로, 요약에서는 메뉴로
                        if (_view != SettleView.Summary && !_settleFromMenu) _view = SettleView.Summary; else GoMenu();
                    }
                    else if ((kb.enterKey.wasPressedThisFrame || gA) && _view == SettleView.Summary) GoMenu();
                    else if (kb.rKey.wasPressedThisFrame && _view == SettleView.Map) CenterMap();
                    if (_fxT > 0) _fxT -= Time.unscaledDeltaTime;
                    break;
                case Screen.Pause:
                    if (kb.escapeKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || gA || gB) { _run.SetPaused(false); Current = Screen.Run; }
                    else if (kb.mKey.wasPressedThisFrame) { _run.Sim.EndRun(false, "원정 포기"); _run.SetPaused(false); Current = Screen.Run; }
                    break;
                case Screen.Run:
                    if (_run != null && _run.Paused) Current = Screen.Pause;
                    else if (gp != null && GamepadMap.Pressed(gp, GamepadMap.Action.Pause) && _run != null && _run.Sim.Phase == GamePhase.Playing) { _run.SetPaused(true); Current = Screen.Pause; }
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
            bool click = enabled && hover && Event.current.type == EventType.MouseDown && Event.current.button == 0;
            if (click) AudioDirector.Instance?.MenuClick();
            if (hover && enabled && Event.current.type == EventType.Repaint && _hoverRect != r) { _hoverRect = r; AudioDirector.Instance?.Hover(); }
            return click;
        }
        Rect _hoverRect;

        void OnGUI()
        {
            Fonts.ApplySkin();   // Pretendard — 스킨 폰트를 바꾸면 라벨·버튼·텍스트필드 전부 따라온다
            if (Current == Screen.Run) { DrawWipe(); return; }
            _k = UnityEngine.Screen.height / 1080f;
            float W = UnityEngine.Screen.width / _k, H = 1080f;
            switch (Current)
            {
                case Screen.MainMenu: DrawMainMenu(W, H); break;
                case Screen.Starmap: DrawStarmap(W, H); break;
                case Screen.RoleSelect: DrawRoleSelect(W, H); break;
                case Screen.Settlement: DrawSettlement(W, H); break;
                case Screen.Pause: DrawPause(W, H); break;
                case Screen.Settings: DrawSettings(W, H); break;
            }
            DrawWipe();
        }

        /// <summary>화면 전환 시트. 런 화면 위에서도 그려야 하므로 OnGUI 의 Run 조기 반환 앞에서도 호출한다.</summary>
        void DrawWipe()
        {
            if (_wipeT < 0) return;
            float sw = UnityEngine.Screen.width, sh = UnityEngine.Screen.height, k = sh / 1080f;
            // 시트 앞머리 0→1 (cover) · 1 (hold) · 뒷머리 0→1 (reveal). 뒤로 가기는 좌우 반전
            float head, tail;
            if (_wipeT < WipeCover) { head = Ease(_wipeT / WipeCover); tail = 0; }
            else if (_wipeT < WipeCover + WipeHold) { head = 1; tail = 0; }
            else { head = 1; tail = Ease((_wipeT - WipeCover - WipeHold) / WipeReveal); }
            float skew = sw * .12f;
            var col = new Color(.03f, .02f, .06f, 1f);
            const int rows = 24;
            for (int i = 0; i < rows; i++)
            {
                float y0 = sh * i / rows, y1 = sh * (i + 1) / rows, f = (i + .5f) / rows;
                float x0 = tail * (sw + skew) - skew * (1 - f), x1 = head * (sw + skew) - skew * f;
                x0 = Mathf.Clamp(x0, 0, sw); x1 = Mathf.Clamp(x1, 0, sw);
                if (_wipeBack) { float a = sw - x1, b = sw - x0; x0 = a; x1 = b; }
                if (x1 > x0) Fill(new Rect(x0, y0, x1 - x0, y1 - y0 + 1), col);
            }
            // 로딩 — 원본 assets/loading 드릴 아이콘 회전 + 문구. 시트가 화면을 다 덮은 구간에만
            if (_wipeLoading && _wipeT >= WipeCover * .85f && _wipeT < WipeCover + WipeHold + WipeReveal * .3f)
            {
                float s = 180 * k;
                if (_loadingArt != null)
                {
                    var m = GUI.matrix;
                    GUIUtility.RotateAroundPivot(_loadingSpin, new Vector2(sw * .5f, sh * .5f));
                    GUI.DrawTexture(new Rect(sw * .5f - s * .5f, sh * .5f - s * .5f, s, s), _loadingArt, ScaleMode.ScaleToFit);
                    GUI.matrix = m;
                }
                var st = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(24 * k), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
                st.normal.textColor = new Color(.9f, .92f, 1f);
                GUI.Label(new Rect(0, sh * .5f + s * .62f, sw, 44 * k), $"{RoleNames[(int)_selected]} 출격 — 지층 생성 중", st);
            }
        }
        static float Ease(float t) { t = Mathf.Clamp01(t); return t < .5f ? 2 * t * t : 1 - Mathf.Pow(-2 * t + 2, 2) * .5f; }

        // ── 설정 (신설 — 계획 §2.3): 해상도 · 전체화면 · 감속 모드 · 볼륨(M7 오디오가 읽는다). PlayerPrefs 저장
        public static bool ReducedMotionPref => PlayerPrefs.GetInt("tc.reducedMotion", 0) == 1;
        public static float BgmVolume => PlayerPrefs.GetFloat("tc.bgm", .8f);
        public static float SfxVolume => PlayerPrefs.GetFloat("tc.sfx", .9f);
        void LoadSettings()
        {
            _resolutions = UnityEngine.Screen.resolutions;
            if (_resolutions == null || _resolutions.Length == 0) _resolutions = new[] { new Resolution { width = UnityEngine.Screen.width, height = UnityEngine.Screen.height } };
            _fullscreen = PlayerPrefs.GetInt("tc.fullscreen", UnityEngine.Screen.fullScreen ? 1 : 0) == 1;
            _reducedMotion = ReducedMotionPref;
            _bgm = BgmVolume; _sfx = SfxVolume;
            int w = PlayerPrefs.GetInt("tc.resW", UnityEngine.Screen.width), h = PlayerPrefs.GetInt("tc.resH", UnityEngine.Screen.height);
            _resIdx = Array.FindIndex(_resolutions, r => r.width == w && r.height == h);
            if (_resIdx < 0) _resIdx = _resolutions.Length - 1;
            ApplyMotion();
        }
        void SaveSettings()
        {
            PlayerPrefs.SetInt("tc.fullscreen", _fullscreen ? 1 : 0); PlayerPrefs.SetInt("tc.reducedMotion", _reducedMotion ? 1 : 0);
            PlayerPrefs.SetFloat("tc.bgm", _bgm); PlayerPrefs.SetFloat("tc.sfx", _sfx);
            var r = _resolutions[Mathf.Clamp(_resIdx, 0, _resolutions.Length - 1)];
            PlayerPrefs.SetInt("tc.resW", r.width); PlayerPrefs.SetInt("tc.resH", r.height);
            PlayerPrefs.Save();
        }
        void ApplyResolution()
        {
            var r = _resolutions[Mathf.Clamp(_resIdx, 0, _resolutions.Length - 1)];
            UnityEngine.Screen.SetResolution(r.width, r.height, _fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }
        void ApplyMotion() { var fb = FindFirstObjectByType<Feedback>(); if (fb != null) fb.ReducedMotion = _reducedMotion; }

        void DrawSettings(float W, float H)
        {
            DrawBackdrop(_bg, W, H, .6f);
            GUI.Label(R(80, 60, 900, 60), "<b>설정</b>   <color=#aaa>Esc / Enter 저장 후 메뉴</color>", St(34, FontStyle.Bold));
            float x = 120, y = 170, w = 900;
            Panel(R(x - 20, y - 20, w + 40, 540), .7f);
            var cur = _resolutions[Mathf.Clamp(_resIdx, 0, _resolutions.Length - 1)];
            GUI.Label(R(x, y, 260, 50), "해상도", St(22));
            if (Button(R(x + 280, y, 60, 50), "◀")) { _resIdx = Mathf.Max(0, _resIdx - 1); ApplyResolution(); }
            GUI.Label(R(x + 350, y, 300, 50), $"<b>{cur.width} × {cur.height}</b>", St(22, FontStyle.Bold, TextAnchor.MiddleCenter));
            if (Button(R(x + 660, y, 60, 50), "▶")) { _resIdx = Mathf.Min(_resolutions.Length - 1, _resIdx + 1); ApplyResolution(); }
            y += 70;
            GUI.Label(R(x, y, 260, 50), "전체 화면", St(22));
            if (Button(R(x + 280, y, 440, 50), _fullscreen ? "<b>켬</b>   <color=#aaa>클릭: 창 모드</color>" : "<b>끔</b>   <color=#aaa>클릭: 전체 화면</color>")) { _fullscreen = !_fullscreen; ApplyResolution(); }
            y += 70;
            GUI.Label(R(x, y, 260, 50), "감속 모드", St(22));
            if (Button(R(x + 280, y, 440, 50), _reducedMotion ? "<b>켬</b>   <color=#aaa>흔들림 35% · 히트스톱 26ms · 와이프 생략</color>" : "<b>끔</b>")) { _reducedMotion = !_reducedMotion; ApplyMotion(); }
            y += 70;
            GUI.Label(R(x, y, 260, 50), "BGM", St(22));
            _bgm = GUI.HorizontalSlider(R(x + 280, y + 18, 380, 20), _bgm, 0f, 1f); GUI.Label(R(x + 680, y, 80, 50), $"{_bgm:P0}", St(20));
            y += 70;
            GUI.Label(R(x, y, 260, 50), "효과음", St(22));
            _sfx = GUI.HorizontalSlider(R(x + 280, y + 18, 380, 20), _sfx, 0f, 1f); GUI.Label(R(x + 680, y, 80, 50), $"{_sfx:P0}", St(20));
            y += 80;
            GUI.Label(R(x, y, w, 60), "<color=#aaa>볼륨은 M7 오디오가 읽습니다. 설정: PlayerPrefs · 세이브: " + MetaStore.Path + "</color>", St(14, FontStyle.Normal, TextAnchor.UpperLeft));
            if (Button(R(x, y + 70, 300, 56), "저장 · 메뉴로   <color=#aaa>Enter</color>")) { SaveSettings(); GoMenu(); }

            DrawGamepadRemap(1120, 170, 700);
        }

        /// <summary>게임패드 버튼 리매핑 (M7 잔여). 액션 줄을 클릭하면 캡처 모드 → 패드 버튼 하나를 누르면 배정. 같은 버튼을 다른 액션이 쓰고 있으면 그쪽을 기본값으로 되돌린다.</summary>
        void DrawGamepadRemap(float x, float y, float w)
        {
            var gp = Gamepad.current;
            var actions = GamepadMap.All;
            Panel(R(x - 20, y - 20, w + 40, 60 + actions.Length * 46 + 140), .7f);
            GUI.Label(R(x, y, w, 50), $"<b>게임패드</b>   <color=#aaa>{(gp != null ? gp.displayName : "연결 안 됨 · 연결하면 바로 인식")}</color>", St(22, FontStyle.Bold));
            y += 56;

            // 캡처 — 배정 가능 버튼이 눌리면 묶고, 6초 지나면 취소. B 는 취소로 쓰지 않는다(B 도 배정 대상).
            if (_gpCapture >= 0)
            {
                _gpCaptureT -= Time.unscaledDeltaTime;
                var hit = GamepadMap.AnyPressed(gp);
                if (hit.HasValue)
                {
                    var target = actions[_gpCapture];
                    foreach (var other in actions) if (other != target && GamepadMap.Get(other) == hit.Value) GamepadMap.Set(other, GamepadMap.Default(other));
                    GamepadMap.Set(target, hit.Value); GamepadMap.Save();
                    AudioDirector.Instance?.Ui();
                    _gpCapture = -1;
                }
                else if (_gpCaptureT <= 0) _gpCapture = -1;
            }

            for (int i = 0; i < actions.Length; i++)
            {
                var a = actions[i];
                bool capturing = _gpCapture == i;
                GUI.Label(R(x, y, 260, 40), GamepadMap.Label(a), St(19));
                string cur = capturing
                    ? "<color=#ffd56b>버튼을 누르세요…</color>"
                    : $"<b>{GamepadMap.Label(GamepadMap.Get(a))}</b>" + (GamepadMap.IsDefault(a) ? "" : $"   <color=#aaa>기본 {GamepadMap.Label(GamepadMap.Default(a))}</color>");
                if (Button(R(x + 270, y, w - 270, 40), cur, accent: capturing ? new Color(.55f, .42f, .12f) : (Color?)null))
                {
                    if (capturing) _gpCapture = -1;
                    else { _gpCapture = i; _gpCaptureT = 6f; }
                }
                y += 46;
            }
            y += 10;
            GUI.Label(R(x, y, w, 44), "<color=#aaa>스틱(이동·조준)과 메뉴의 A 확인 · B 뒤로는 고정입니다. 트리거는 40% 이상 당기면 눌림.</color>", St(14, FontStyle.Normal, TextAnchor.UpperLeft, wrap: true));
            y += 50;
            if (Button(R(x, y, 260, 48), "기본값 복원")) { GamepadMap.ResetAll(); GamepadMap.Save(); _gpCapture = -1; }
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
            // 로고 (원본 .menuLogo title-tunnel-crew-v2 672×364) · 키커 "Dig · Descend · Return". 로고가 없으면 텍스트 타이틀
            if (_title != null) GUI.DrawTexture(R(72, 56, 440, 238), _title, ScaleMode.ScaleToFit);
            else GUI.Label(R(80, 90, 900, 90), "<b>땅굴 크루</b>", St(72, FontStyle.Bold));
            GUI.Label(R(84, 296, 900, 36), "DIG · DESCEND · RETURN   <color=#aaa>4직업 크루 · 지층 돌파 · 이상지대 무한 하강</color>", St(18, FontStyle.Normal, TextAnchor.MiddleLeft, false, new Color(.8f, .78f, .85f)));
            float x = 80, y = 346, w = 560, h = 64, gap = 12;   // 7개 버튼이 하단 기록 패널(H-120) 위에서 끝나도록
            string N(int n) => "<size=" + Mathf.RoundToInt(15 * _k) + ">" + n.ToString("00") + "</size>   ";
            if (Button(R(x, y, w, h), N(1) + "출격 — 행성 지도   <color=#aaa>Enter</color>")) GoStarmap(); y += h + gap;
            // 무한 모드 직행 — 원본 #menuInfinite (tcLaunchInfScene → 직업 선택 → 바로 런). 행성 지도를 건너뛴다는 점만 다르고 규칙은 같다
            if (Button(R(x, y, w, h), N(2) + "무한 모드 — 바로 출격   <color=#aaa>2</color>")) GoRoleSelect(); y += h + gap;
            if (Button(R(x, y, w, h), N(3) + "성장 지도 · 영구 노드   <color=#aaa>3</color>")) OpenSettlement(true); y += h + gap;
            if (Button(R(x, y, w, h), N(4) + "유물 보관고   <color=#aaa>4</color>")) { OpenSettlement(true); _view = SettleView.Relics; } y += h + gap;
            if (Button(R(x, y, w, h), N(5) + "설정   <color=#aaa>5</color>")) GoSettings(); y += h + gap;
            // 관전 모드 — 원본 #menuObserver (observer.js 가 .modeGrid 에 주입, 원클릭 출격). 리더는 마지막으로 고른 직업(기본 드릴러)
            if (Button(R(x, y, w, h), N(6) + "관전 모드   <color=#7febd0>4직업 완전 자동</color>", true, new Color(.22f, .45f, .4f))) LaunchObserver(); y += h + gap;
            if (Button(R(x, y, w, h), N(7) + "종료", true, new Color(.6f, .6f, .65f))) Application.Quit(); y += h + gap;
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
                // 카드 우측 상단 [− n + AI] — AI 크루 편성 (원본 .aiCrewChip). 카드 선택 클릭과 섞이지 않게 칩 영역은 제외한다
                var crew = _run.Sim.Crew; int nAi = crew.Count(role);
                var chip = new Rect(rc.xMax - 172 * _k, rc.y + 8 * _k, 160 * _k, 34 * _k);
                Fill(chip, new Color(.05f, .03f, .08f, .86f));
                if (Button(new Rect(chip.x, chip.y, 40 * _k, chip.height), "−", nAi > 0)) crew.Remove(role);
                GUI.Label(new Rect(chip.x + 42 * _k, chip.y, 30 * _k, chip.height), $"<b>{nAi}</b>", St(16, FontStyle.Bold, TextAnchor.MiddleCenter, false, nAi > 0 ? RoleColors[i] : new Color(.43f, .38f, .5f)));
                if (Button(new Rect(chip.x + 74 * _k, chip.y, 86 * _k, chip.height), "+ AI", crew.Roster.Count < AiCrewSystem.Max, RoleColors[i])) crew.Add(role);
                if (rc.Contains(Event.current.mousePosition) && !chip.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown) { if (_selected == role && Event.current.clickCount >= 2) Launch(); _selected = role; }
                if (sel && Button(new Rect(rc.x + 18 * _k, rc.yMax - 70 * _k, rc.width - 36 * _k, 54 * _k), "선택 완료 · 출격   <color=#aaa>Enter</color>", true, RoleColors[i])) Launch();
            }
            // 카드 그리드 아래의 편성 요약 (원본 .aiCrewBar)
            {
                var crew = _run.Sim.Crew;
                float by = y0 + ch + 14, bw = total, bx = x0;
                Panel(R(bx, by, bw, 46), .5f);
                var sb = new System.Text.StringBuilder("<b>크루 편성</b>   <color=#ffe6a8>[ 나 ]</color>");
                foreach (var r in crew.Roster) sb.Append($"  <color=#{ColorUtility.ToHtmlStringRGB(RoleColors[(int)r])}>[ AI {AiCrewSystem.NameOf(r)} ]</color>");
                for (int k = crew.Roster.Count; k < AiCrewSystem.Max; k++) sb.Append("  <color=#5f5473>[ 빈 자리 ]</color>");
                sb.Append("   <color=#7b6f8f>카드 우측 상단 + AI 로 동료를 넣는다 · 같은 세계·같은 적을 공유하는 로컬 동료</color>");
                GUI.Label(R(bx + 16, by, bw - 220, 46), sb.ToString(), St(15));
                if (crew.Roster.Count > 0 && Button(R(bx + bw - 190, by + 6, 176, 34), "모두 비우기", true, new Color(.6f, .6f, .65f))) crew.Clear();
                // 관전 모드 진입 (원본 #obsStartBtn — 직업 선택 화면 하단) — 선택한 직업이 AI 리더
                if (Button(R(bx, by + 60, bw, 52), "👁  관전 모드 — 4직업 완전 자동 진행   <color=#7ea99f>선택한 직업이 AI 리더 · 나머지 3직업 AI 크루 · Tab 시점 전환 · Esc 해제</color>", true, new Color(.22f, .45f, .4f))) LaunchObserver();
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
