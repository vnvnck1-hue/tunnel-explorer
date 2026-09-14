using System;
using System.Collections.Generic;
using TunnelCrew.Presentation.CRT;
using TunnelCrew.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using GUI = TunnelCrew.Presentation.CRT.CrtGui;

namespace TunnelCrew.Presentation
{
    /// <summary>
    /// 「응답 없는 4번 갱도」. 본편 Sim을 그대로 쓰는 플레이어블 FTUE와
    /// 검정 배경의 순차 컷만화를 한 곳에서 조율한다.
    /// </summary>
    [DefaultExecutionOrder(-120)]
    public sealed class FtueDirector : MonoBehaviour, ICrtScreen
    {
        public const string CompletePref = "tc.ftue.completed.v1";
        public static bool HasCompleted => PlayerPrefs.GetInt(CompletePref, 0) == 1;

        public struct WorldLayout
        {
            public Vec2 Entry, FirstWall, Flare, RescueWall, Rescue, Arena, Blackbox;
            public int FirstWallCell, RescueWallCell;
        }

        sealed class ComicPanel
        {
            public string Resource, Speaker, Caption, Cue;
            public Rect Normalized;
            public Rect Uv;
            public float Rotation;
            public bool RevealPanel;
            [NonSerialized] public Texture2D Texture;
            [NonSerialized] public DynamicDialogueText.Script Script;
        }

        sealed class DialogueLine
        {
            public string Speaker, Caption, Cue;
            public float Seconds;
        }

        enum TransitionStyle { None, Impact, Signal, Hatch }

        readonly FtueProgressModel _progress = new FtueProgressModel();
        readonly List<ComicPanel> _comic = new List<ComicPanel>();
        readonly Queue<DialogueLine> _dialogueQueue = new Queue<DialogueLine>();
        readonly List<EnemyState> _wave = new List<EnemyState>();
        RunBootstrap _run;
        AudioDirector _audio;
        WorldLayout _layout;
        Action _comicDone;
        float _comicTime, _comicInputLock, _skipHeld, _stateDelay, _nextAid;
        int _comicBeat, _comicTutorialClicks, _combatKills;
        bool _comicForceComplete;
        RoleId _selectedRole = RoleId.Driller;
        Vec2 _moveOrigin;
        string _speaker = "", _caption = "";
        float _captionUntil, _captionStarted;
        DynamicDialogueText.Script _dialogueScript, _rolePromptScript;
        Texture2D _lockerArt;
        float _roleChoiceStarted, _transitionTime, _transitionDuration;
        TransitionStyle _transitionStyle;
        bool _pacingWarned, _finishing;
        Texture2D _titleLogo;
        GUIStyle _objectiveStyle, _promptStyle, _speakerStyle, _captionStyle, _roleStyle, _roleTagStyle;

        public bool Active => _progress.Current != FtueProgressModel.Stage.Inactive && _progress.Current != FtueProgressModel.Stage.Complete;
        public bool BlocksSimulation => Active && (FtueProgressModel.BlocksWorld(_progress.Current) || _progress.Current == FtueProgressModel.Stage.RoleChoice);
        public FtueProgressModel.Stage CurrentStage => _progress.Current;
        public FtueProgressModel.Hint CurrentHint => _progress.HintLevel;
        public string CurrentObjective => FtueProgressModel.Objective(_progress.Current);
        public int CurrentComicBeat => _comicBeat;
        public bool ComicTextComplete => _comic.Count == 0 || _comicForceComplete ||
            _comic[_comicBeat].Script == null || _comicTime >= _comic[_comicBeat].Script.Duration;

        public void Bind(RunBootstrap run)
        {
            _run = run;
            _audio = AudioDirector.Instance;
            _titleLogo = Resources.Load<Texture2D>("UI/title-logo");
            _lockerArt = Resources.Load<Texture2D>("FTUE/Art/equipment_locker");
            CrtSurface.Register(this, 80);
            _run.Sim.TileBroken += OnTileBroken;
            _run.Sim.EnemyHurt += OnEnemyHurt;
            _progress.Changed += OnStageChanged;
        }

        public void Begin()
        {
            _finishing = false;
            _comicTutorialClicks = 0;
            _selectedRole = RoleId.Driller;
            _progress.Begin(Time.unscaledTimeAsDouble);
            PlayCrashComic();
        }

        public TunnelCrew.Sim.PlayerInput FilterInput(TunnelCrew.Sim.PlayerInput input)
        {
            switch (_progress.Current)
            {
                case FtueProgressModel.Stage.Movement:
                    input.DrillHeld = input.FireHeld = input.PrimaryPressed = input.SecondaryPressed = false;
                    input.DashPressed = input.ReloadPressed = input.SkillQPressed = input.SkillEPressed = input.EscapePressed = false;
                    break;
                case FtueProgressModel.Stage.FirstDig:
                case FtueProgressModel.Stage.RescueDig:
                    input.FireHeld = input.SecondaryPressed = false;
                    input.DashPressed = input.ReloadPressed = input.SkillQPressed = input.SkillEPressed = input.EscapePressed = false;
                    break;
                case FtueProgressModel.Stage.Flare:
                case FtueProgressModel.Stage.BlackboxInteract:
                    input.DrillHeld = input.FireHeld = input.PrimaryPressed = input.SecondaryPressed = false;
                    input.DashPressed = input.ReloadPressed = input.SkillQPressed = input.SkillEPressed = input.EscapePressed = false;
                    break;
                case FtueProgressModel.Stage.CrashComic:
                case FtueProgressModel.Stage.RoleChoice:
                case FtueProgressModel.Stage.BlackboxComic:
                case FtueProgressModel.Stage.AwakeningComic:
                case FtueProgressModel.Stage.EscapeComic:
                    return new TunnelCrew.Sim.PlayerInput { AimWorld = input.AimWorld };
            }
            return input;
        }

        void Update()
        {
            if (!Active || _run == null || _run.Sim == null) return;
            if (_transitionTime > 0) _transitionTime = Mathf.Max(0, _transitionTime - Time.unscaledDeltaTime);
            TickDialogue();
            double now = Time.unscaledTimeAsDouble;
            var previousHint = _progress.HintLevel;
            _progress.Tick(now);
            if (_progress.HintLevel != previousHint) _audio?.FtueCue(_progress.HintLevel == FtueProgressModel.Hint.Direct ? "knock" : "panel");

            if (FtueProgressModel.BlocksWorld(_progress.Current))
            {
                TickComic(Time.unscaledDeltaTime);
                return;
            }
            if (_progress.Current == FtueProgressModel.Stage.RoleChoice)
            {
                TickRoleChoice();
                return;
            }

            _stateDelay = Mathf.Max(0, _stateDelay - Time.unscaledDeltaTime);
            TickPacingAid(now);
            var p = _run.Sim.Player;
            var kb = Keyboard.current;
            var gp = Gamepad.current;

            switch (_progress.Current)
            {
                case FtueProgressModel.Stage.Movement:
                    if (Vec2.Distance(p.Position, _moveOrigin) >= .85)
                        _progress.Set(FtueProgressModel.Stage.FirstDig, now);
                    break;
                case FtueProgressModel.Stage.Flare:
                    if ((kb != null && kb.qKey.wasPressedThisFrame) || (gp != null && GamepadMap.Pressed(gp, GamepadMap.Action.SkillQ)))
                    {
                        var dir = Vec2.FromAngle(p.Aim);
                        var at = p.Position + dir * 3.2;
                        _run.Sim.Roles.Flares.Add(new TunnelCrew.Sim.Flare { Position = at, Ttl = 40, MaxTtl = 40, LightRadius = 5.4, VisionRange = 6 });
                        _run.Sim.Los?.MarkDirty();
                        _audio?.Rescue();
                        Say("모래", "빛을 앞에 던져. 네 몸 말고.", "morae", 3.2f);
                        _progress.Set(FtueProgressModel.Stage.RescueDig, now);
                    }
                    break;
                case FtueProgressModel.Stage.Combat:
                    if (_stateDelay <= 0 && _wave.Count == 0) SpawnTutorialWave(false);
                    break;
                case FtueProgressModel.Stage.BlackboxInteract:
                    bool near = Vec2.Distance(p.Position, _layout.Blackbox) <= 2.0;
                    bool use = kb != null && kb.eKey.wasPressedThisFrame;
                    use |= gp != null && GamepadMap.Pressed(gp, GamepadMap.Action.SkillE);
                    if (near && use) PlayBlackboxComic();
                    break;
                case FtueProgressModel.Stage.ReturnToLander:
                    if (_stateDelay <= 0 && _wave.Count == 0) SpawnTutorialWave(true);
                    if (Vec2.Distance(p.Position, _layout.Entry) <= 2.0) PlayEscapeComic();
                    break;
            }
        }

        void TickPacingAid(double now)
        {
            double elapsed = _progress.Elapsed(now);
            if (!_pacingWarned && elapsed > Budget(_progress.Current))
            {
                _pacingWarned = true;
                Debug.LogWarning($"[FTUE pacing] {_progress.Current} {elapsed:F1}초 정체 — 힌트 {_progress.HintLevel} 적용");
            }
            if (_progress.HintLevel != FtueProgressModel.Hint.Direct || now < _nextAid) return;
            _nextAid = (float)now + 3.2f;
            if (_progress.Current == FtueProgressModel.Stage.FirstDig || _progress.Current == FtueProgressModel.Stage.RescueDig)
                _audio?.FtueCue("knock");
            if (_progress.Current == FtueProgressModel.Stage.Combat)
            {
                foreach (var e in _wave) if (e != null && e.Alive) e.Hp = Math.Min(e.Hp, Math.Max(1, e.HpMax * .35));
                if (_run.Sim.Player.Hp < _run.Sim.Player.HpMax * .45) _run.Sim.Player.Hp = _run.Sim.Player.HpMax * .45;
            }
        }

        static double Budget(FtueProgressModel.Stage stage) => stage switch
        {
            FtueProgressModel.Stage.Movement => 20,
            FtueProgressModel.Stage.FirstDig => 35,
            FtueProgressModel.Stage.Flare => 22,
            FtueProgressModel.Stage.RescueDig => 40,
            FtueProgressModel.Stage.Combat => 45,
            FtueProgressModel.Stage.BlackboxInteract => 25,
            FtueProgressModel.Stage.ReturnToLander => 45,
            _ => 60,
        };

        void TickRoleChoice()
        {
            var kb = Keyboard.current;
            var gp = Gamepad.current;
            if (Mouse.current != null)
            {
                var m = Mouse.current.position.ReadValue();
                var top = new Vector2(m.x, Screen.height - m.y);
                for (int i = 0; i < 4; i++) if (RoleBayRect(i).Contains(top)) _selectedRole = (RoleId)i;
            }
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) { _selectedRole = RoleId.Driller; ConfirmRole(); return; }
                else if (kb.digit2Key.wasPressedThisFrame) { _selectedRole = RoleId.Gunner; ConfirmRole(); return; }
                else if (kb.digit3Key.wasPressedThisFrame) { _selectedRole = RoleId.Scout; ConfirmRole(); return; }
                else if (kb.digit4Key.wasPressedThisFrame) { _selectedRole = RoleId.Engineer; ConfirmRole(); return; }
                else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) _selectedRole = (RoleId)(((int)_selectedRole + 3) % 4);
                else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) _selectedRole = (RoleId)(((int)_selectedRole + 1) % 4);
                if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) ConfirmRole();
            }
            if (gp != null)
            {
                if (gp.dpad.left.wasPressedThisFrame) _selectedRole = (RoleId)(((int)_selectedRole + 3) % 4);
                else if (gp.dpad.right.wasPressedThisFrame) _selectedRole = (RoleId)(((int)_selectedRole + 1) % 4);
                if (gp.buttonSouth.wasPressedThisFrame) ConfirmRole();
            }
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                var m = Mouse.current.position.ReadValue();
                var top = new Vector2(m.x, Screen.height - m.y);
                for (int i = 0; i < 4; i++)
                {
                    var r = RoleBayRect(i);
                    if (!r.Contains(top)) continue;
                    _selectedRole = (RoleId)i;
                    ConfirmRole();
                    break;
                }
            }
        }

        void ConfirmRole()
        {
            _audio?.Deploy();
            _layout = _run.PrepareFtueRole(_selectedRole);
            _moveOrigin = _run.Sim.Player.Position;
            _progress.Set(FtueProgressModel.Stage.Movement, Time.unscaledTimeAsDouble);
            StartTransition(TransitionStyle.Hatch, .55f);
            string line = _selectedRole switch
            {
                RoleId.Driller => "드릴 챙겼네. [tint=morae]막힌 길은 네가 열어.[/tint]",
                RoleId.Gunner => "파쇄기야. 벽에도 붙어. [slam]두 번 물러서.[/slam]",
                RoleId.Scout => "조명탄은 아껴. [tint=signal]이 아래엔 빛을 싫어하는 게 있어.[/tint]",
                _ => "전력 제어기. 길이 없으면 [wave=.45]공간부터 만들어.[/wave]",
            };
            Say("모래", line, "morae", 4.2f);
        }

        void OnTileBroken(TileBrokenEvent e)
        {
            if (!Active) return;
            double now = Time.unscaledTimeAsDouble;
            if (_progress.Current == FtueProgressModel.Stage.FirstDig && e.Cell == _layout.FirstWallCell)
            {
                Say("관제", "강하선 외부 통로 확보. 구조 신호는 동쪽이다.", "control", 3.4f);
                _progress.Set(FtueProgressModel.Stage.Flare, now);
            }
            else if (_progress.Current == FtueProgressModel.Stage.RescueDig && e.Cell == _layout.RescueWallCell)
            {
                _run.Sim.Crew.Clear();
                _run.Sim.Crew.Add(RoleId.Scout);
                if (_run.Sim.Crew.Members.Count > 0) _run.Sim.Crew.Members[0].Position = _layout.Rescue;
                _audio?.Rescue();
                Say("관제", "스카우트 모래 확인. [tint=signal]생존자 한 명.[/tint] 잔여 반응 둘.", "control", 4.1f);
                Say("모래", "여기 한 명 살아 있어. [slam]나.[/slam] 나머지 둘은 사람이 아니야.", "morae", 4.3f);
                _stateDelay = 1.4f;
                _progress.Set(FtueProgressModel.Stage.Combat, now);
            }
        }

        void OnEnemyHurt(EnemyHurtEvent e)
        {
            if (!Active || !e.Killed || !_wave.Contains(e.Enemy)) return;
            _combatKills++;
            if (_progress.Current == FtueProgressModel.Stage.Combat && _combatKills >= 3)
            {
                _wave.Clear();
                _audio?.FtueCue("beacon");
                Say("관제", "적성체 셋의 맥박이 [tint=signal][jitter=.55]구조 신호와 동기화됐다.[/jitter][/tint]", "control", 4.2f);
                Say("모래", "놈들이 신호를 내는 게 아니야. [tint=danger]신호가 놈들을 움직여.[/tint]", "morae", 4.0f);
                _progress.Set(FtueProgressModel.Stage.BlackboxInteract, Time.unscaledTimeAsDouble);
            }
        }

        void SpawnTutorialWave(bool chase)
        {
            _wave.Clear();
            _combatKills = 0;
            var sim = _run.Sim;
            sim.Enemies.Cap = chase ? 5 : 3;
            sim.Enemies.SpawnInterval = 999;
            var outward = (_layout.Blackbox - _layout.Entry).Normalized;
            var origin = chase ? sim.Player.Position + outward * 3.4 : _layout.Arena;
            int count = chase ? 4 : 3;
            for (int i = 0; i < count; i++)
            {
                double a = (i - (count - 1) * .5) * .48;
                var p = origin + Vec2.FromAngle(a) * (1.4 + .45 * i);
                var e = new EnemyState
                {
                    Position = p, Home = p, WanderTarget = p, LastSeen = sim.Player.Position,
                    Kind = i == count - 1 ? EnemyKind.Spitter : EnemyKind.Crawler,
                    Radius = SimTuning.EnemyRadius * (i == 0 ? .88 : 1),
                    Hp = SimTuning.EnemyHp * (chase ? .62 : .7), HpMax = SimTuning.EnemyHp * (chase ? .62 : .7),
                    Ai = EnemyAi.Chase, LostTime = 0, AttackCooldown = .8 + i * .18,
                    FaceAngle = (sim.Player.Position - p).Angle,
                };
                _wave.Add(e);
                sim.Enemies.AddExternal(e);
            }
            if (!chase)
            {
                Say("모래", "잠깐. [jitter=.45]저건 돌이 아니야.[/jitter]", "morae", 3.0f);
                Say("관제", "구조 반응 둘, 빠르게 접근 중.", "control", 3.0f);
            }
        }

        void OnStageChanged(FtueProgressModel.Stage previous, FtueProgressModel.Stage next)
        {
            _pacingWarned = false;
            _nextAid = 0;
            if (next == FtueProgressModel.Stage.FirstDig) _audio?.FtueCue("knock");
            if (next == FtueProgressModel.Stage.RescueDig) _audio?.FtueCue("knock");
        }

        void Say(string speaker, string text, string cue, float seconds)
        {
            var line = new DialogueLine { Speaker = speaker, Caption = text, Cue = cue, Seconds = seconds };
            if (string.IsNullOrEmpty(_caption) || Time.unscaledTime > _captionUntil) BeginDialogue(line);
            else _dialogueQueue.Enqueue(line);
        }

        void TickDialogue()
        {
            if (string.IsNullOrEmpty(_caption) || Time.unscaledTime <= _captionUntil) return;
            if (_dialogueQueue.Count > 0) BeginDialogue(_dialogueQueue.Dequeue());
            else
            {
                _speaker = _caption = string.Empty;
                _dialogueScript = null;
                _audio?.NarrativeDuck(false);
            }
        }

        void BeginDialogue(DialogueLine line)
        {
            _speaker = line.Speaker;
            _caption = line.Caption;
            _dialogueScript = DynamicDialogueText.Compile(line.Caption);
            _captionStarted = Time.unscaledTime;
            _captionUntil = _captionStarted + Mathf.Max(line.Seconds, _dialogueScript.Duration + .75f);
            _audio?.NarrativeDuck(true);
            _audio?.FtueCue(line.Cue);
        }

        void ClearDialogue()
        {
            _dialogueQueue.Clear();
            _speaker = _caption = string.Empty;
            _dialogueScript = null;
            _captionUntil = 0;
        }

        void PlayCrashComic()
        {
            _audio?.UseTunnel();
            BeginComic(FtueProgressModel.Stage.CrashComic, new[]
            {
                P("FTUE/Art/sequence_crash", .04f,.09f,.42f,.35f, "관제", "4번 갱도에서 [tint=signal][echo]구조 신호 셋[/echo][/tint]. 마지막 기록은 71시간 전.", "control", -1.2f, new Rect(0,.5f,.5f,.5f)),
                D("관제", "생체 패턴은 서로 다른데… 좌표가 [jitter=.45]완전히 겹친다.[/jitter]", "beacon"),
                P("FTUE/Art/sequence_crash", .51f,.09f,.45f,.35f, "모래", "사흘 묵은 신호를 셋이나? [speed=.72]기분 나쁜데.[/speed]", "morae", 1.0f, new Rect(.5f,.5f,.5f,.5f)),
                D("관제", "회수 우선. 폭풍 전선까지 11분. 착륙 좌표 전송한다.", "control"),
                P("FTUE/Art/sequence_crash", .09f,.50f,.82f,.42f, "", "", "impact", 0f, new Rect(0,0,1,.5f)),
                D("모래", "[slam][tint=danger]충격 온다![/tint][/slam] 손잡이 잡아!", "impact"),
            }, () =>
            {
                _progress.Set(FtueProgressModel.Stage.RoleChoice, Time.unscaledTimeAsDouble);
                _roleChoiceStarted = Time.unscaledTime;
                _rolePromptScript = DynamicDialogueText.Compile("모래: 선반 고정이 풀렸어. [tint=morae]살아 나갈 장비 하나[/tint] 골라.");
                StartTransition(TransitionStyle.Impact, .72f);
            });
        }

        void PlayBlackboxComic()
        {
            _progress.Set(FtueProgressModel.Stage.BlackboxComic, Time.unscaledTimeAsDouble);
            _audio?.UseTunnel();
            BeginComic(FtueProgressModel.Stage.BlackboxComic, new[]
            {
                P("FTUE/Art/sequence_record", .04f,.06f,.53f,.42f, "선발대장", "본부, 광맥이 예상보다 크다. 장비가 자꾸 같은 좌표를 찍는다.", "captain", -.7f, new Rect(0,.5f,.6f,.5f)),
                D("관제 기록", "신호원은 몇 명인가?", "control"),
                P("FTUE/Art/sequence_record", .61f,.06f,.35f,.42f, "선발대원", "셋입니다. 그런데 광맥이… [wave=.7]움직입니다.[/wave]", "captain", .9f, new Rect(.6f,.5f,.4f,.5f)),
                P("FTUE/Art/sequence_record", .04f,.52f,.34f,.40f, "선발대원", "벽 안에 빈 공간이 있어요. 엄청 커요. 안에서 우리 호출 부호가 들립니다.", "beacon", -.8f, new Rect(0,0,.3333f,.5f)),
                D("???", "[echo][tint=signal]선발대. 응답하라.[/tint][/echo]", "mimic"),
                P("FTUE/Art/sequence_record", .41f,.52f,.26f,.40f, "선발대장", "신호를 끄고 올라간다. [tint=danger]아무도 내려보내지 마.[/tint]", "captain", .6f, new Rect(.3333f,0,.3334f,.5f)),
                P("FTUE/Art/sequence_record", .70f,.52f,.26f,.40f, "선발대장", "저건 심장이 아니야. 저건 [jitter=.8]우리 목소리를—[/jitter]", "mimic", -.4f, new Rect(.6667f,0,.3333f,.5f)),
            }, PlayAwakeningComic);
            StartTransition(TransitionStyle.Signal, .58f);
        }

        void PlayAwakeningComic()
        {
            _progress.Set(FtueProgressModel.Stage.AwakeningComic, Time.unscaledTimeAsDouble);
            BeginComic(FtueProgressModel.Stage.AwakeningComic, new[]
            {
                P("FTUE/Art/sequence_awake", .04f,.07f,.60f,.86f, "", "", "beacon", -.6f, new Rect(0,0,.67f,1)),
                D("???", "[speed=.55][echo][tint=signal]여기… 한 명 살아 있어.[/tint][/echo][/speed]", "mimic"),
                P("FTUE/Art/sequence_awake", .67f,.07f,.29f,.86f, "모래", "방금 건 내 목소리였어. 내가 한 말이 아니야.", "morae", .7f, new Rect(.67f,0,.33f,1)),
                D("모래", "저 신호가 우릴 부른 거야. [slam][tint=danger]뛰어.[/tint][/slam]", "morae"),
            }, () =>
            {
                _audio?.UseTunnel();
                _stateDelay = 1.2f;
                _wave.Clear();
                _progress.Set(FtueProgressModel.Stage.ReturnToLander, Time.unscaledTimeAsDouble);
                StartTransition(TransitionStyle.Signal, .55f);
            });
        }

        void PlayEscapeComic()
        {
            if (_progress.Current == FtueProgressModel.Stage.EscapeComic) return;
            _progress.Set(FtueProgressModel.Stage.EscapeComic, Time.unscaledTimeAsDouble);
            _audio?.UseTunnel();
            BeginComic(FtueProgressModel.Stage.EscapeComic, new[]
            {
                P("FTUE/Art/sequence_escape", .04f,.07f,.45f,.40f, "관제", "강하선 도킹. 생존자 신호… [jitter=.45]넷[/jitter]을 확인했다.", "door", -1f, new Rect(0,.5f,.5f,.5f)),
                D("모래", "넷? 여기엔 우리 둘뿐이야.", "morae"),
                P("FTUE/Art/sequence_escape", .52f,.07f,.44f,.40f, "???", "[speed=.55][echo][tint=signal]여기… 한 명 살아 있어.[/tint][/echo][/speed]", "mimic", .8f, new Rect(.5f,.5f,.5f,.5f)),
                P("FTUE/Art/sequence_escape", .04f,.51f,.44f,.41f, "모래", "저거… [jitter=.75]내 목소리야.[/jitter]", "morae", -.5f, new Rect(0,0,.5f,.5f)),
                D("관제", "4번 갱도 신호가 지상으로 이동한다.", "control"),
                P("FTUE/Art/sequence_escape", .51f,.51f,.45f,.41f, "", "", "title", .5f, new Rect(.5f,0,.5f,.5f)),
            }, Finish);
            StartTransition(TransitionStyle.Hatch, .62f);
        }

        static ComicPanel P(string resource, float x, float y, float w, float h, string speaker, string caption, string cue, float rot, Rect uv)
            => new ComicPanel { Resource = resource, Normalized = new Rect(x,y,w,h), Uv = uv, Speaker = speaker, Caption = caption, Cue = cue, Rotation = rot, RevealPanel = true };

        static ComicPanel D(string speaker, string caption, string cue)
            => new ComicPanel { Speaker = speaker, Caption = caption, Cue = cue, RevealPanel = false };

        void BeginComic(FtueProgressModel.Stage stage, ComicPanel[] panels, Action done)
        {
            _progress.Set(stage, Time.unscaledTimeAsDouble);
            _comic.Clear();
            _comic.AddRange(panels);
            _comicTime = 0;
            _comicBeat = 0;
            _comicInputLock = .18f;
            _comicForceComplete = false;
            _skipHeld = 0;
            _comicDone = done;
            ClearDialogue();
            foreach (var p in _comic)
            {
                if (!string.IsNullOrEmpty(p.Resource)) p.Texture = Resources.Load<Texture2D>(p.Resource);
                p.Script = DynamicDialogueText.Compile(p.Caption);
            }
            _audio?.NarrativeDuck(true);
            FireComicBeat();
        }

        void TickComic(float dt)
        {
            _comicTime += dt;
            _comicInputLock = Mathf.Max(0, _comicInputLock - dt);

            var kb = Keyboard.current;
            var gp = Gamepad.current;
            bool advance = kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame);
            advance |= gp != null && gp.buttonSouth.wasPressedThisFrame;
            advance |= Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            if (advance) AdvanceComic();

            bool skip = kb != null && kb.escapeKey.isPressed;
            skip |= gp != null && gp.buttonEast.isPressed;
            _skipHeld = skip ? _skipHeld + dt : 0;
            if (_skipHeld >= .85f) FinishComic();
        }

        public bool AdvanceComic()
        {
            if (_comic.Count == 0 || _comicInputLock > 0) return false;
            var beat = _comic[_comicBeat];
            bool ready = ComicTextComplete && (!string.IsNullOrEmpty(beat.Caption) || _comicTime >= .45f);
            if (!ready)
            {
                _comicForceComplete = true;
                _comicInputLock = .12f;
                _comicTutorialClicks++;
                return true;
            }
            _comicTutorialClicks++;
            if (_comicBeat + 1 >= _comic.Count) { FinishComic(); return true; }
            _comicBeat++;
            _comicTime = 0;
            _comicForceComplete = false;
            _comicInputLock = .18f;
            FireComicBeat();
            return true;
        }

        void FireComicBeat()
        {
            if (_comic.Count == 0) return;
            var p = _comic[_comicBeat];
            _audio?.FtueCue(string.IsNullOrEmpty(p.Cue) ? "panel" : p.Cue);
        }

        void FinishComic()
        {
            var done = _comicDone;
            _comicDone = null;
            _comic.Clear();
            _audio?.NarrativeDuck(false);
            done?.Invoke();
        }

        void Finish()
        {
            if (_finishing) return;
            _finishing = true;
            PlayerPrefs.SetInt(CompletePref, 1);
            PlayerPrefs.Save();
            _progress.Set(FtueProgressModel.Stage.Complete, Time.unscaledTimeAsDouble);
            _audio?.NarrativeDuck(false);
            _run.FinishFtue();
        }

        public void DrawCrt()
        {
            if (!Active) return;
            GUI.OriginalPalette = true;
            try
            {
                EnsureStyles();
                if (FtueProgressModel.BlocksWorld(_progress.Current)) DrawComic();
                else if (_progress.Current == FtueProgressModel.Stage.RoleChoice) DrawRoleChoice();
                else DrawGameplayGuide();
                DrawTransition();
            }
            finally { GUI.OriginalPalette = false; }
        }

        void EnsureStyles()
        {
            float k = Screen.height / 1080f;
            int S(float v) => Mathf.RoundToInt(v * k);
            _objectiveStyle ??= Style(S(22), FontStyle.Bold, TextAnchor.MiddleLeft, true, new Color(1f,.9f,.66f));
            _promptStyle ??= Style(S(25), FontStyle.Bold, TextAnchor.MiddleCenter, false, new Color(1f,.94f,.78f));
            _speakerStyle ??= Style(S(17), FontStyle.Bold, TextAnchor.MiddleLeft, false, new Color(.24f,.17f,.08f));
            _captionStyle ??= Style(S(24), FontStyle.Bold, TextAnchor.MiddleLeft, true, new Color(.06f,.045f,.03f));
            _roleStyle ??= Style(S(28), FontStyle.Bold, TextAnchor.MiddleCenter, false, Color.white);
            _roleTagStyle ??= Style(S(17), FontStyle.Normal, TextAnchor.MiddleCenter, true, new Color(.82f,.79f,.86f));
        }

        static GUIStyle Style(int size, FontStyle fs, TextAnchor align, bool wrap, Color color)
        {
            var s = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = fs, alignment = align, wordWrap = wrap, richText = true };
            s.normal.textColor = color;
            return s;
        }

        void DrawComic()
        {
            float W = Screen.width, H = Screen.height;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0,0,W,H), Texture2D.whiteTexture);
            GUI.color = Color.white;
            for (int i = 0; i <= _comicBeat && i < _comic.Count; i++)
            {
                var p = _comic[i];
                if (!p.RevealPanel) continue;
                float enter = i == _comicBeat ? Mathf.Clamp01(_comicTime / .2f) : 1;
                enter = enter * enter * (3 - 2 * enter);
                var r = new Rect(p.Normalized.x*W, p.Normalized.y*H, p.Normalized.width*W, p.Normalized.height*H);
                var c = r.center;
                float scale = Mathf.Lerp(.91f, 1f, enter);
                r.width *= scale; r.height *= scale; r.center = c;
                GUI.color = new Color(1,1,1,enter);
                if (p.Texture != null)
                {
                    CrtGuiUtility.RotateAroundPivot(p.Rotation * enter, r.center);
                    DrawTextureCrop(r, p.Texture, p.Uv);
                    CrtGuiUtility.RotateAroundPivot(-p.Rotation * enter, r.center);
                }
                else
                {
                    GUI.color = new Color(.08f,.055f,.12f,enter);
                    GUI.DrawTexture(r, Texture2D.whiteTexture);
                }
                GUI.color = new Color(.88f,.78f,.62f,enter);
                DrawBorder(r, Mathf.Max(2, H/360f));
                GUI.color = Color.white;
            }
            if (_progress.Current == FtueProgressModel.Stage.EscapeComic && _comicBeat >= _comic.Count - 1 && _titleLogo != null)
            {
                float enter = Mathf.Clamp01(_comicTime / .35f);
                var panel = _comic[_comic.Count - 1].Normalized;
                var r = new Rect((panel.x + .04f) * W, (panel.y + .035f) * H, (panel.width - .08f) * W, panel.height * .48f * H);
                GUI.color = new Color(1, 1, 1, enter);
                GUI.DrawTexture(r, _titleLogo, ScaleMode.ScaleToFit);
                GUI.color = Color.white;
            }
            if (_comic.Count > 0)
            {
                var beat = _comic[_comicBeat];
                if (!string.IsNullOrEmpty(beat.Caption)) DrawSpeechBubble(beat.Speaker, beat.Script, _comicTime, _comicForceComplete);
            }
            var help = Style(Mathf.RoundToInt(18*H/1080f), FontStyle.Bold, TextAnchor.MiddleCenter, false, new Color(.8f,.76f,.82f));
            string primary = Gamepad.current != null ? "A" : Mouse.current != null ? "클릭" : "Enter";
            string hint = _comicTutorialClicks == 0 ? $"{primary}하면 문장 완성" :
                _comicTutorialClicks == 1 ? $"{primary}하여 다음 컷" : $"{primary} 다음  ·  Esc/B 길게 건너뛰기";
            GUI.Label(new Rect(W*.5f-235*H/1080f,H-42*H/1080f,470*H/1080f,28*H/1080f), hint, help);
        }

        static void DrawBorder(Rect r, float w)
        {
            GUI.DrawTexture(new Rect(r.x-w,r.y-w,r.width+w*2,w),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x-w,r.yMax,r.width+w*2,w),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x-w,r.y,w,r.height),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax,r.y,w,r.height),Texture2D.whiteTexture);
        }

        static void DrawTextureCrop(Rect destination, Texture2D texture, Rect uv)
        {
            float sourceAspect = texture.width * uv.width / Mathf.Max(1f, texture.height * uv.height);
            float destinationAspect = destination.width / Mathf.Max(1f, destination.height);
            if (sourceAspect > destinationAspect)
            {
                float keep = destinationAspect / sourceAspect;
                float old = uv.width;
                uv.width *= keep;
                uv.x += (old - uv.width) * .5f;
            }
            else
            {
                float keep = sourceAspect / destinationAspect;
                float old = uv.height;
                uv.height *= keep;
                uv.y += (old - uv.height) * .5f;
            }
            GUI.DrawTextureWithTexCoords(destination, texture, uv);
        }

        void DrawSpeechBubble(string speaker, DynamicDialogueText.Script script, float elapsed, bool complete)
        {
            float k = Screen.height/1080f, w = Mathf.Min(Screen.width*.62f, 980*k), h = 126*k;
            var r = new Rect(Screen.width*.5f-w*.5f,Screen.height-176*k,w,h);
            GUI.color = new Color(.97f,.92f,.79f,.98f);
            GUI.DrawTexture(r,Texture2D.whiteTexture);
            GUI.color = new Color(.13f,.08f,.035f,1);
            DrawBorder(r,2.2f*k);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x+24*k,r.y+11*k,r.width-48*k,24*k),speaker,_speakerStyle);
            GUI.DynamicLabel(new Rect(r.x+24*k,r.y+38*k,r.width-48*k,r.height-45*k),script,_captionStyle,elapsed,complete,ReducedMotion);
        }

        void DrawRoleChoice()
        {
            float k=Screen.height/1080f;
            GUI.color=Color.black;GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=Color.white;
            if (_lockerArt != null) GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),_lockerArt,ScaleMode.ScaleAndCrop);
            GUI.color=new Color(.025f,.014f,.035f,.28f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=Color.white;
            var title=Style(Mathf.RoundToInt(28*k),FontStyle.Bold,TextAnchor.MiddleCenter,false,new Color(1f,.9f,.66f));
            var titleRect=new Rect(Screen.width*.18f,42*k,Screen.width*.64f,58*k);
            GUI.color=new Color(.025f,.014f,.035f,.8f);GUI.DrawTexture(titleRect,Texture2D.whiteTexture);GUI.color=Color.white;
            GUI.DynamicLabel(titleRect,_rolePromptScript,title,Time.unscaledTime-_roleChoiceStarted,false,ReducedMotion);
            string[] names={"드릴러","거너","스카우트","엔지니어"};
            string[] tags={"드릴 헤드 · 길을 연다","파쇄 발사기 · 적과 벽을 부순다","조명탄 장치 · 어둠을 밝힌다","전력 제어기 · 공간을 만든다"};
            Color[] colors={new Color(1f,.77f,.28f),new Color(1f,.42f,.34f),new Color(.34f,.9f,.75f),new Color(.68f,.47f,1f)};
            for(int i=0;i<4;i++)
            {
                var r=RoleBayRect(i);bool on=(int)_selectedRole==i;
                GUI.color=on?new Color(colors[i].r,colors[i].g,colors[i].b,.12f):new Color(0,0,0,.06f);
                GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=on?colors[i]:new Color(.66f,.58f,.5f,.58f);DrawBorder(r,on?5*k:1.5f*k);GUI.color=Color.white;
                var badge=Resources.Load<Texture2D>("UI/badge-"+((RoleId)i).ToString().ToLowerInvariant());
                var info=new Rect(r.x-7*k,r.yMax-112*k,r.width+14*k,112*k);
                GUI.color=new Color(.025f,.014f,.035f,on ? .9f : .72f);GUI.DrawTexture(info,Texture2D.whiteTexture);GUI.color=Color.white;
                if(badge!=null)GUI.DrawTexture(new Rect(info.x+10*k,info.y+13*k,44*k,44*k),badge,ScaleMode.ScaleToFit);
                GUI.Label(new Rect(info.x+54*k,info.y+8*k,info.width-60*k,45*k),$"{i+1}  {names[i]}",_roleStyle);
                GUI.Label(new Rect(info.x+12*k,info.y+49*k,info.width-24*k,34*k),tags[i],_roleTagStyle);
                if(on)GUI.Label(new Rect(info.x,info.y+81*k,info.width,26*k),"클릭 / Enter  장착",_roleTagStyle);
            }
        }

        static Rect RoleBayRect(int index)
        {
            float[] centers={.305f,.455f,.596f,.735f};
            float[] widths={.17f,.16f,.145f,.15f};
            float w=Screen.width*widths[Mathf.Clamp(index,0,3)];
            return new Rect(Screen.width*centers[Mathf.Clamp(index,0,3)]-w*.5f,Screen.height*.14f,w,Screen.height*.61f);
        }

        void DrawGameplayGuide()
        {
            float k=Screen.height/1080f;
            string objective=CurrentObjective;
            if(!string.IsNullOrEmpty(objective))
            {
                var r=new Rect(54*k,54*k,690*k,58*k);
                GUI.color=new Color(.02f,.012f,.025f,.82f);GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=Color.white;
                GUI.Label(new Rect(r.x+18*k,r.y,r.width-30*k,r.height),objective,_objectiveStyle);
            }
            string prompt=FtueProgressModel.Prompt(_progress.Current,_progress.HintLevel);
            if (_progress.HintLevel >= FtueProgressModel.Hint.Input &&
                (_progress.Current == FtueProgressModel.Stage.FirstDig || _progress.Current == FtueProgressModel.Stage.RescueDig))
                prompt = _selectedRole == RoleId.Gunner ? "좌클릭  파쇄탄 부착 · 2초 뒤 폭파" : "좌클릭 홀드  굴착";
            else if (_progress.HintLevel >= FtueProgressModel.Hint.Input && _progress.Current == FtueProgressModel.Stage.Combat)
                prompt = "우클릭 홀드  사격  ·  Space  대시";
            if(!string.IsNullOrEmpty(prompt))
            {
                var r=new Rect(Screen.width*.5f-300*k,Screen.height-128*k,600*k,62*k);
                GUI.color=new Color(.02f,.012f,.025f,.86f);GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=Color.white;
                GUI.Label(r,prompt,_promptStyle);
            }
            if(_progress.HintLevel>=FtueProgressModel.Hint.Direction&&TryTarget(out var target)) DrawWaypoint(target);
            if(!string.IsNullOrEmpty(_caption)&&Time.unscaledTime<=_captionUntil)
                DrawSpeechBubble(_speaker,_dialogueScript,Time.unscaledTime-_captionStarted,false);
            else if(Time.unscaledTime>_captionUntil)_audio?.NarrativeDuck(false);
        }

        bool ReducedMotion => MetaScreens.ReducedMotionPref || (Feedback.Instance != null && Feedback.Instance.ReducedMotion) ||
            CRTDisplayController.Instance?.Accessibility == CrtAccessibility.Photosensitive;

        void StartTransition(TransitionStyle style, float duration)
        {
            _transitionStyle=style;
            _transitionDuration=ReducedMotion?Mathf.Min(.22f,duration):duration;
            _transitionTime=_transitionDuration;
        }

        void DrawTransition()
        {
            if (_transitionTime<=0 || _transitionDuration<=0) return;
            float W=Screen.width,H=Screen.height;
            float p=1-_transitionTime/_transitionDuration;
            float remain=1-Mathf.SmoothStep(0,1,p);
            if (_transitionStyle==TransitionStyle.Impact)
            {
                Color c=ReducedMotion?new Color(.02f,.01f,.03f,remain):new Color(1f,.82f,.54f,remain*.82f);
                GUI.color=c;GUI.DrawTexture(new Rect(0,0,W,H),Texture2D.whiteTexture);GUI.color=Color.white;
            }
            else if (_transitionStyle==TransitionStyle.Hatch)
            {
                float cover=H*.5f*remain;
                GUI.color=new Color(.018f,.01f,.026f,.98f);
                GUI.DrawTexture(new Rect(0,0,W,cover),Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0,H-cover,W,cover),Texture2D.whiteTexture);GUI.color=Color.white;
            }
            else if (_transitionStyle==TransitionStyle.Signal)
            {
                int bands=ReducedMotion?1:12;
                GUI.color=new Color(.025f,.01f,.04f,.94f);
                for(int i=0;i<bands;i++)
                {
                    float y=H*i/bands;
                    float stagger=ReducedMotion?remain:Mathf.Clamp01(remain+(i%3-1)*.08f);
                    GUI.DrawTexture(new Rect((1-stagger)*W,y,stagger*W,H/bands+1),Texture2D.whiteTexture);
                }
                GUI.color=Color.white;
            }
        }

        bool TryTarget(out Vec2 target)
        {
            target=_progress.Current switch
            {
                FtueProgressModel.Stage.FirstDig=>_layout.FirstWall,
                FtueProgressModel.Stage.RescueDig=>_layout.RescueWall,
                FtueProgressModel.Stage.Combat=>_layout.Arena,
                FtueProgressModel.Stage.BlackboxInteract=>_layout.Blackbox,
                FtueProgressModel.Stage.ReturnToLander=>_layout.Entry,
                _=>default,
            };
            return _progress.Current!=FtueProgressModel.Stage.Movement&&_progress.Current!=FtueProgressModel.Stage.Flare;
        }

        void DrawWaypoint(Vec2 target)
        {
            var cam=Camera.main;if(cam==null)return;
            var rw=IsometricProjection.ToRender(target);
            var s=cam.WorldToScreenPoint(new Vector3(rw.x,rw.y,0));
            float k=Screen.height/1080f;
            float x=Mathf.Clamp(s.x,38*k,Screen.width-38*k),y=Mathf.Clamp(Screen.height-s.y,150*k,Screen.height-170*k);
            float pulse=1+.12f*Mathf.Sin(Time.unscaledTime*5.2f);
            var r=new Rect(x-13*k*pulse,y-13*k*pulse,26*k*pulse,26*k*pulse);
            GUI.color=new Color(1f,.79f,.31f,.88f);GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=Color.white;
        }

        /// <summary>진입점 주변에 페이싱을 보장하는 작은 고정 갱도를 조각한다.</summary>
        public static WorldLayout BuildAuthoredWorld(TunnelSim sim)
        {
            var w=sim.World;
            w.WallHpMul=.6;
            int ex=w.EntryCol,ey=Math.Max(8,Math.Min(w.Rows-9,w.EntryRow));
            int dir=ex<w.Cols/2?1:-1;
            int X(int step)=>ex+dir*step;
            bool Safe(int c,int r)=>w.InInterior(c,r);
            for(int step=-2;step<=27;step++)for(int dy=-7;dy<=7;dy++)if(Safe(X(step),ey+dy))w.SetTile(X(step),ey+dy,TileType.Rock);
            void ClearRect(int a,int b,int y0,int y1){for(int step=a;step<=b;step++)for(int dy=y0;dy<=y1;dy++)if(Safe(X(step),ey+dy))w.ClearSilent(X(step),ey+dy);}
            ClearRect(0,4,-2,2);
            ClearRect(6,10,-1,1);
            ClearRect(10,14,-3,3);
            ClearRect(16,18,-2,2);
            ClearRect(19,25,-4,4);
            w.SetTile(X(5),ey,TileType.Ore);
            w.SetTile(X(15),ey,TileType.Dirt);
            sim.Player.Position=WorldGrid.CellCenter(ex,ey);
            sim.Player.Velocity=Vec2.Zero;
            if(sim.Generation?.Lamps!=null)
            {
                sim.Generation.Lamps.Clear();
                sim.Generation.Lamps.Add((ex,ey));
                sim.Generation.Lamps.Add((X(21),ey+2));
            }
            sim.Los?.MarkDirty();
            sim.RefreshVision();
            return new WorldLayout
            {
                Entry=WorldGrid.CellCenter(ex,ey),FirstWall=WorldGrid.CellCenter(X(5),ey),Flare=WorldGrid.CellCenter(X(11),ey),
                RescueWall=WorldGrid.CellCenter(X(15),ey),Rescue=WorldGrid.CellCenter(X(16),ey),Arena=WorldGrid.CellCenter(X(21),ey),
                Blackbox=WorldGrid.CellCenter(X(24),ey),FirstWallCell=w.Index(X(5),ey),RescueWallCell=w.Index(X(15),ey),
            };
        }
    }
}
