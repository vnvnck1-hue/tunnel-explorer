using System;
using System.Collections.Generic;
using System.Linq;
using TunnelCrew.Data;
using TunnelCrew.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using SimInput = TunnelCrew.Sim.PlayerInput;

namespace TunnelCrew.Presentation
{
    /// <summary>
    /// 그레이박스 진입점. 씬에 이 컴포넌트 하나만 두면 월드·플레이어·적·카메라·조명을 만들어 돌린다.
    ///
    /// 씬을 손으로 꾸미지 않고 코드로 세우는 이유는, 이 단계의 목표가 "원본과 같은 규칙·감각인가"
    /// 확인이라 재현 가능해야 하기 때문이다. 실제 씬 구성은 M4 이후 HUD 와 함께 잡는다.
    ///
    /// M3: 직업 선택(<see cref="_role"/>), Q/E 스킬, 우클릭 사격, R 재장전, 적·투사체 뷰,
    /// <see cref="Feedback"/> 로 히트스톱·킥·스쿼시. 숫자키 1~4 로 직업을 바꾸며 층을 다시 만든다.
    /// </summary>
    public sealed class RunBootstrap : MonoBehaviour
    {
        [Header("데이터")]
        [Tooltip("비워 두면 Resources 에서 찾는다.")]
        [SerializeField] TileSetAsset _tileSet;
        [SerializeField] MonsterSheetAsset _monsterSheets;
        [SerializeField] int _depth = 1;

        [Header("런")]
        [SerializeField] RoleId _role = RoleId.Driller;
        [Tooltip("시작 직후 적을 몇 마리 미리 깔아 두는가 (확인용). 0 이면 스포너만 쓴다.")]
        [SerializeField] int _prespawnEnemies = 4;

        [Header("조명")]
        [SerializeField, Range(0f, 1f)] float _ambientIntensity = 0.16f;
        [SerializeField] bool _flashlightOn = true;

        [Header("디버그")]
        [SerializeField] bool _showHud = true;

        public TunnelSim Sim { get; private set; }

        /// <summary>런이 진행 중인가. 메뉴·정산 화면(MetaScreens)이 떠 있으면 false — Sim 도 HUD 도 멈춘다.</summary>
        public bool RunActive { get; private set; } = true;
        /// <summary>Esc 일시정지 (계획 §2.3 신설).</summary>
        public bool Paused { get; private set; }
        /// <summary>결과 화면에서 Enter — MetaScreens 가 정산으로 넘긴다. 구독자가 없으면 같은 직업으로 재시작.</summary>
        public event Action ResultDismissed;
        public event Action PauseMenuRequested;

        /// <summary>메뉴에서 출격. 층을 만들고 HUD 를 켠다.</summary>
        public void LaunchRun(RoleId role) { SwitchRole(role); RunActive = true; Paused = false; }
        /// <summary>런을 내려놓고 메뉴로 — Sim 은 남겨 두고(배경으로 보인다) 틱과 HUD 만 멈춘다.</summary>
        public void SuspendRun() { RunActive = false; Paused = false; Time.timeScale = 1f; }
        public void SetPaused(bool on) { Paused = on; }
        public RoleId CurrentRole => _role;

        CameraRig _rig;
        Camera _cam;
        WorldRenderer _worldRenderer;
        PlayerView _playerView;
        LootView _lootView;
        EnemyView _enemyView;
        CrewView _crewView;
        CombatView _combatView;
        Feedback _feedback;
        FxSystem _fx;
        Texture2D _bossIcon; readonly Dictionary<RoleId, Texture2D> _portraits = new Dictionary<RoleId, Texture2D>();
        Texture2D _white;
        Light2D _globalLight, _flashlight, _playerHalo;
        readonly List<Light2D> _lamps = new List<Light2D>();
        readonly List<Light2D> _flareLights = new List<Light2D>();
        Light2D _bossLight;
        Transform _lightRoot;
        DarknessOverlay _darkness;
        Volume _volume;
        WallShadowBuilder _wallShadows;
        readonly Dictionary<RoleId, CharacterSheetAsset> _sheets = new Dictionary<RoleId, CharacterSheetAsset>();
        readonly List<string> _log = new List<string>();
        float _outroT;

        void Start()
        {
            if (_tileSet == null) _tileSet = Resources.Load<TileSetAsset>("TileSet_purple");
            if (_monsterSheets == null) _monsterSheets = Resources.Load<MonsterSheetAsset>("MonsterSheets");
            foreach (RoleId r in System.Enum.GetValues(typeof(RoleId)))
            {
                var sh = Resources.Load<CharacterSheetAsset>("Sheets_" + r.ToString().ToLowerInvariant());
                if (sh != null) _sheets[r] = sh;
            }

            if (_tileSet == null)
                Debug.LogError("[M1] TileSet 을 찾지 못했다. " +
                    "`Tunnel Crew/M1 · 아트 임포트 설정 + 타일셋 생성` 을 먼저 실행할 것.");

            Application.runInBackground = true;
            // 런 바깥 화면(메뉴·정산·일시정지). 씬을 손으로 꾸미지 않는 원칙대로 코드에서 붙인다.
            if (GetComponent<MetaScreens>() == null) gameObject.AddComponent<MetaScreens>();
            _bossIcon = Resources.Load<Texture2D>("UI/boss-icon");
            foreach (RoleId r in System.Enum.GetValues(typeof(RoleId)))
            {
                var tex = Resources.Load<Texture2D>("UI/portrait-" + r.ToString().ToLowerInvariant());
                if (tex != null) _portraits[r] = tex;
            }
            _white = Texture2D.whiteTexture;

            Sim = new TunnelSim();
            Sim.StartRun(_role, MetaStore.Load());
            Sim.EnterDepth(_depth, DungeonConfig.Runtime);
            SubscribeSim();

            BuildCamera();
            BuildWorld();
            BuildPlayer();
            BuildLighting();

            _rig.Bind(Sim.World, () => Sim.Player);
            Prespawn();
        }

        void OnDestroy() { Time.timeScale = 1f; }

        // ───────────────────────────── Sim → 연출 이벤트
        void SubscribeSim()
        {
            Sim.TileBroken += e =>
            {
                bool ore = e.Type == TileType.Ore || e.Type == TileType.Gem || e.Type == TileType.Crys;
                bool hard = e.Type == TileType.Stone || e.Type == TileType.Core;
                _feedback?.BlockBroken(ore, hard, V(e.HitDir));
                _fx?.TileBroken(V(WorldGrid.CellCenter(e.Col, e.Row)), e.Type, V(e.HitDir));
                if (e.OpenedExit) { _fx?.BigRing(V(WorldGrid.CellCenter(e.Col, e.Row)), new Color(.78f, .63f, 1f), 2.4f); _combatView?.Text(WorldGrid.CellCenter(e.Col, e.Row) + new Vec2(0, .7), "출구 개방", new Color(.78f, .63f, 1f), 22); }
                if (e.HadBuriedRelic) _combatView?.Text(WorldGrid.CellCenter(e.Col, e.Row) + new Vec2(0, .46), "묻힌 유물!", new Color(.5f, .92f, .82f), 19);
            };
            // 드릴 비트 — 원본 7233행: 누적 피해를 드릴 끝에 숫자로, 스파이크·돌덩이·불꽃, 킥 6
            Sim.DrillBeat += (tip, dmg) =>
            {
                var n = Vec2.FromAngle(Sim.Player.Aim);
                _feedback?.DrillBeat(V(n));
                _combatView?.Damage(tip + n * .32 + new Vec2(0, .30), dmg, false, (float)Sim.Player.DrillHeat);
                _fx?.DrillBeat(V(tip), V(Sim.Player.Position), V(n));
            };
            Sim.DrillBounced += e => { _feedback?.Kick(6f * 1.6f, V(e.Normal)); _fx?.DrillBounce(V(e.Position), V(e.Normal)); };
            Sim.PlayerDashed += e => _feedback?.Dash(V(e.Direction));
            Sim.EnemyHurt += e =>
            {
                bool big = e.Killed || e.Damage > 20;
                _feedback?.EnemyHit(e.Killed, e.Enemy.IsApex, e.Enemy.IsBoss, (float)e.Damage, V(e.HitDir));
                _combatView?.Damage(e.Enemy.Position + new Vec2(0, e.Enemy.Radius * .6), e.Damage, big);
                _fx?.EnemyHit(V(e.Enemy.Position), V(e.HitDir), e.Killed, big);
                if (e.Killed && e.Enemy.IsApex) _combatView?.Text(e.Enemy.Position + new Vec2(0, .8), "광란종 제압", new Color(1f, .55f, .45f), 17);
            };
            Sim.PlayerHurt += e =>
            {
                _feedback?.PlayerHurt((float)e.Damage, (float)Sim.Player.Hp, (float)Sim.Player.HpMax, V(e.HitDir));
                _combatView?.Text(Sim.Player.Position + new Vec2(0, .64), "-" + (int)System.Math.Round(e.Damage), new Color(1f, .33f, .49f), 20);
                _fx?.PlayerHurt(V(Sim.Player.Position), V(e.HitDir), _feedback != null ? _feedback.HurtLevel : 1);
                Log(e.Downed ? "다운!" : $"피격 -{e.Damage:F0}");
            };
            Sim.ProjectileFired += e => _feedback?.Shot((float)e.Angle, e.VisualId);
            Sim.ProjectileEnded += e =>
            {
                if (e.Exploded) { _feedback?.Kick(2.2f, Vector2.zero); _feedback?.Hitstop(18f); }
                _fx?.ProjectileEnd(V(e.Position), e.Exploded);
            };
            Sim.TraitFx += e =>
            {
                switch (e.Kind)
                {
                    case "auxHit": _fx?.Spikes(V(e.At), 3, new Color(1f, .83f, .43f, .82f), .22f, V(e.Dir)); break;
                    case "afterBlast": _fx?.Ring(V(e.At), new Color(.78f, .63f, 1f), .3f, (float)e.Radius + .35f); _fx?.Burst(V(e.At), 12, new[] { new Color(.78f, .63f, 1f), new Color(1f, .95f, .84f) }, 190f); break;
                    case "vortex": _fx?.Ring(V(e.At), new Color(.78f, .63f, 1f), .4f, (float)e.Radius + .35f); _feedback?.Kick(3f, Vector2.zero); break;
                    case "blast": break;   // 파괴 자체의 연출이 붙는다
                    case "planetBreaker":
                        _fx?.Ring(V(e.At), new Color(1f, .55f, .45f), .6f, (float)e.Radius); _feedback?.Kick(13f, V(e.Dir)); _feedback?.Hitstop(60f);
                        _combatView?.Text(Sim.Player.Position + new Vec2(0, .8), "행성 파쇄기", new Color(1f, .55f, .45f), 20); break;
                    case "grandCollapse":
                        _fx?.Ring(V(e.At), new Color(1f, .44f, .54f), .5f, (float)e.Radius + .3f); _fx?.Smoke(V(e.At), 8, new Color(.29f, .21f, .31f), 90f); _feedback?.Kick(9f, Vector2.zero); _feedback?.Hitstop(48f);
                        _combatView?.Text(Sim.Player.Position + new Vec2(0, .8), "대붕괴", new Color(1f, .44f, .54f), 20); break;
                    case "risk": _combatView?.Text(e.At + new Vec2(0, .68), e.Label, new Color(1f, .44f, .54f), 15); break;
                    case "core": _combatView?.Text(e.At + new Vec2(0, .44), e.Label, new Color(.5f, .92f, .82f), 16); _fx?.Burst(V(e.At), 6, new[] { new Color(.5f, .92f, .82f), Color.white }, 120f); break;
                    case "remote": _combatView?.Text(e.At + new Vec2(0, .84), e.Label, new Color(1f, .83f, .43f), 13); break;
                }
            };
            Sim.RelicEffect += e =>
            {
                var gold = new Color(1f, .83f, .43f); var ice = new Color(.75f, .91f, 1f); var volt = new Color(1f, .91f, .36f); var earth = new Color(.85f, .63f, .36f); var fire = new Color(1f, .55f, .36f);
                switch (e.Kind)
                {
                    case "crit": _combatView?.Text(e.At + new Vec2(0, e.Radius + .5), "치명!", gold, 18); _fx?.Star(V(e.At), gold, (float)e.Radius * 1.15f); _feedback?.Kick(2.2f, Vector2.zero); break;
                    case "heal": case "label": _combatView?.Text(e.At + new Vec2(0, .6), e.Label, e.Kind == "heal" ? new Color(.56f, .91f, .63f) : new Color(.78f, .63f, 1f), 13); break;
                    case "arcFire": _fx?.Burst(V(e.To), 8, new[] { fire, gold }, 140f); _combatView?.Text(e.To + new Vec2(0, .5), "들불", fire, 14); break;
                    case "arcVolt": _fx?.Spikes(V(e.To), 4, volt, .4f, V(e.To - e.At)); break;
                    case "frostBurst": _fx?.Ring(V(e.At), ice, .2f, (float)e.Radius); _fx?.Burst(V(e.At), 16, new[] { ice, Color.white }, 220f); break;
                    case "unstable": _fx?.Ring(V(e.At), fire, .2f, (float)e.Radius + .3f); _feedback?.Kick(3f, Vector2.zero); break;
                    case "freeze": _combatView?.Text(e.At + new Vec2(0, e.Radius + .4), e.Label, ice, 18); _fx?.Ring(V(e.At), ice, .1f, (float)e.Radius * 1.6f); break;
                    case "shock": _fx?.Spikes(V(e.At), 4, volt, .3f, Vector2.zero); if (e.Label != null) _combatView?.Text(e.At + new Vec2(0, e.Radius + .7), e.Label, volt, 13); break;
                    case "slam": _combatView?.Text(e.At + new Vec2(0, e.Radius + .4), e.Label, new Color(.94f, .86f, .7f), 19); _fx?.Chunks(V(e.At), 8, new[] { earth, new Color(.56f, .42f, .24f) }, 220f, Vector2.zero); _feedback?.Kick(3.4f, Vector2.zero); break;
                    case "rod": _fx?.Ring(V(e.At), volt, .3f, (float)e.Radius); _combatView?.Text(e.At + new Vec2(0, .9), e.Label, volt, 16); break;
                    case "timeStop": _fx?.Ring(V(e.At), new Color(.75f, .85f, 1f), .4f, (float)e.Radius); _combatView?.Text(e.At + new Vec2(0, 1.0), e.Label, new Color(.86f, .91f, 1f), 22); _feedback?.Kick(4f, Vector2.zero); _feedback?.Hitstop(150f); break;
                    case "timeResume": _combatView?.Text(e.At + new Vec2(0, .8), e.Label, new Color(.62f, .72f, .86f), 14); break;
                    case "phoenix": _fx?.BigRing(V(e.At), fire, 3f); _combatView?.Text(e.At + new Vec2(0, 1.0), e.Label, gold, 24); _feedback?.Kick(6f, Vector2.zero); _feedback?.Hitstop(160f); break;
                    case "resonstone": _fx?.Ring(V(e.At), earth, .2f, (float)e.Radius); _combatView?.Text(e.At + new Vec2(0, .6), e.Label, new Color(.94f, .86f, .7f), 14); _feedback?.Kick(3f, Vector2.zero); break;
                    case "stoneskin": _fx?.Ring(V(e.At), earth, .2f, (float)e.Radius); _combatView?.Text(e.At + new Vec2(0, .7), e.Label, new Color(.94f, .86f, .7f), 13); break;
                    case "banner": _fx?.Ring(V(e.At), new Color(1f, .44f, .54f), .2f, (float)e.Radius); _combatView?.Text(e.At + new Vec2(0, .9), e.Label, new Color(1f, .44f, .54f), 18); break;
                }
            };
            Sim.RelicGranted += e =>
            {
                if (e.Relic == null) { _combatView?.Text(e.At + new Vec2(0, .8), "코어 +2 (도감 완성)", new Color(.5f, .92f, .82f), 14); return; }
                var col = e.Relic.Tier >= 4 ? new Color(1f, .83f, .43f) : e.Relic.Tier == 2 ? new Color(.78f, .63f, 1f) : new Color(.85f, .85f, .85f);
                _fx?.BigRing(V(e.At), col, 2.4f); _fx?.Star(V(e.At), col, 1.1f);
                _combatView?.Text(e.At + new Vec2(0, .8), "유물 발굴!", col, 21);
                _feedback?.Kick(5f, Vector2.zero); _feedback?.Hitstop(120f);
                MetaStore.Save();
                Log($"[{(e.Relic.Tier >= 4 ? "전설" : e.Relic.Tier == 2 ? "희귀" : "일반")}] {e.Relic.Name} 발굴 — {e.Relic.Desc}");
            };
            Sim.ResourceCollected += e => _combatView?.Text(e.Position + new Vec2(0, .3), $"+{e.Amount}", e.Kind == ResourceKind.Pulp ? new Color(.45f, .85f, .42f) : new Color(.5f, .92f, .82f), 14);
            Sim.ReloadChanged += e => { if (e.Started) Log(e.Manual ? "재장전 (R)" : "탄창 비어 재장전"); };
            Sim.SkillUsed += e => { Log($"{e.Role} {(e.IsQ ? "Q" : "E")} 사용"); _feedback?.Kick(0.9f, Vector2.zero); };
            Sim.BreakerExploded += e => { _feedback?.Kick(3.0f, Vector2.zero); _feedback?.Hitstop(30f); Log(e.Early ? "파쇄탄 조기 폭발" : "파쇄탄 폭발"); };
            Sim.FoundationBroken += e => { _feedback?.Kick(4.5f, Vector2.zero); _feedback?.Hitstop(60f); Log("기반암 균열 파쇄"); };
            Sim.EnemySpawned += e => { if (e.Enemy.IsApex) Log("광란종 출현"); };
            Sim.DominanceReached += () => Log($"장악도 {Sim.Run.DominanceTarget:P0} 도달");
            Sim.BossSpawned += e => { _feedback?.Kick(9f, Vector2.zero); _feedback?.Hitstop(60f); _fx?.BigRing(V(e.Boss.Body.Position), new Color(1f, .33f, .49f), 3.2f); Log($"{e.Boss.Def.Name} 출현"); };
            Sim.BossPattern += e =>
            {
                switch (e.Pattern)
                {
                    case "dashCharge": _feedback?.Kick(3.2f, V(e.Boss.DashDir)); break;
                    case "dashEnd": _feedback?.Kick(6f, V(e.Boss.DashDir)); break;
                    case "wallField": case "wallWave": case "wallPrison": case "dashPrison": _feedback?.Kick(4f, Vector2.zero); Log(PatternName(e.Pattern)); break;
                    case "armor": Log("암반 장갑 생성"); break;
                    case "scatter": Log("산발 붕괴탄"); break;
                    case "barrage": Log("연속 포격"); break;
                    case "dashWindup": Log("돌진 예고"); break;
                }
            };
            Sim.BossWallRaised += e => { _feedback?.Kick(8.4f, Vector2.zero); _fx?.BossWall(V(WorldGrid.CellCenter(e.Col, e.Row)), e.Hard); if (e.Hard) _combatView?.Text(WorldGrid.CellCenter(e.Col, e.Row) + new Vec2(0, .6), "경화 암반", new Color(.56f, .64f, .91f), 13); };
            Sim.BossShotHit += e => { _feedback?.Kick(5f, Vector2.zero); if (e.HitPlayer) _feedback?.Hitstop(46f); _fx?.BossShotImpact(V(e.At), (float)e.Radius); };
            Sim.BossDefeated += e => { _feedback?.Kick(9f, Vector2.zero); _feedback?.Hitstop(90f); _fx?.BigRing(V(e.Boss.Body.Position), new Color(1f, .83f, .43f), (float)e.Boss.Body.Radius * 1.4f); _combatView?.Text(e.Boss.Body.Position + new Vec2(0, e.Boss.Body.Radius + .7), e.Boss.Tier == BossTier.Guardian ? "수호자 격퇴!" : e.Boss.Tier == BossTier.Apex ? "중심부 보스 격파!" : "변종 격파!", new Color(1f, .83f, .43f), 26); Log($"{e.Boss.Def.Name} 격파 · 코어 +{e.CoreReward}"); };
            Sim.BossPattern += e => { if (e.Pattern == "armor") _combatView?.Text(e.At + new Vec2(0, e.Boss.Body.Radius + .5), "암반 장갑 생성", new Color(1f, .83f, .43f), 17); else if (e.Pattern == "scatter") _combatView?.Text(e.At + new Vec2(0, e.Boss.Body.Radius + .5), "산발 붕괴탄", new Color(1f, .55f, .66f), 16); else if (e.Pattern == "barrage") _combatView?.Text(e.At + new Vec2(0, e.Boss.Body.Radius + .5), "연속 포격", new Color(1f, .55f, .66f), 16); else if (e.Pattern == "dashWindup") _combatView?.Text(e.At + new Vec2(0, e.Boss.Body.Radius + .5), "돌진 예고", new Color(1f, .83f, .43f), 17); };
            Sim.SkillUsed += e => _combatView?.Text(e.Position + new Vec2(0, .7), e.Role switch { RoleId.Driller => "돌파 파기", RoleId.Gunner => e.IsQ ? "방어막" : "조기 기폭", RoleId.Scout => e.IsQ ? "플레어" : "그래플", _ => e.IsQ ? "전력 노드" : "센트리" }, new Color(.5f, .92f, .82f), 15);
            Sim.ReloadChanged += e => { if (e.Started) _combatView?.Text(Sim.Player.Position + new Vec2(0, .64), e.Manual ? "전술 재장전" : "재장전", new Color(.5f, .92f, .82f), 15); else _combatView?.Text(Sim.Player.Position + new Vec2(0, .64), "장전 완료", new Color(1f, .83f, .43f), 14); };
            Sim.LeveledUp += e => { _feedback?.Kick(1.2f, Vector2.zero); Log($"LEVEL {e.Level}"); };
            Sim.TraitPicked += e => Log($"특성: {e.Card.Name}{(e.Stack > 1 ? $" ×{e.Stack}" : "")}");
            Sim.EscapeChanged += e =>
            {
                switch (e.Phase)
                {
                    case EscapePhase.Placing: Log("탈출 지점 지정 · 좌클릭 확정 · 우클릭/X 취소"); break;
                    case EscapePhase.Incoming: Log($"탈출 포트 도착까지 {Mathf.CeilToInt((float)e.Need)}초 · 지점을 사수하세요"); break;
                    case EscapePhase.Ready: _feedback?.Kick(6f, Vector2.zero); Log("탈출 포트 도착 · 탑승하세요"); break;
                    case EscapePhase.None: Log("탈출 요청 취소"); break;
                }
            };
            Sim.RunEnded += (escaped, reason) =>
            {
                Time.timeScale = 1f;
                bool saved = MetaStore.Save();
                Log(escaped ? "탈출 성공" : "런 종료 — " + reason);
                if (!saved) Log("<color=#ff6060>저장 실패 — 기록이 남지 않았다</color>");
                foreach (var u in Sim.LastUnlocks) Log($"해금: {u}");
            };
            // AI 크루 — 연출·토스트 (원본 J.ring/J.burst/J.text/toast 호출을 이벤트로 받는다)
            Sim.Crew.Fx += e =>
            {
                var col = Hex(e.Color);
                switch (e.Kind)
                {
                    case CrewFxKind.Ring: _fx?.Ring(V(e.At), col, .2f, (float)e.Radius); break;
                    case CrewFxKind.Burst: _fx?.Burst(V(e.At), e.Count, new[] { col, Color.white }, (float)e.Size); break;
                    case CrewFxKind.Flash: _fx?.Flash(V(e.At), (float)e.Radius, col); break;
                    case CrewFxKind.Text: _combatView?.Text(e.At, e.Label, col, (float)e.Size); break;
                    case CrewFxKind.Kick: _feedback?.Kick((float)e.Size, V(e.Dir)); break;
                    case CrewFxKind.Chunks: _fx?.Chunks(V(e.At), e.Count, new[] { col, Hex("#FFD36E"), Hex("#FFF3D6") }, (float)e.Size, V(e.Dir)); break;
                }
            };
            Sim.Crew.Toast += Log;
            Sim.PlayerDowned += () =>
            {
                _feedback?.Kick(6f, Vector2.zero);
                _fx?.Ring(V(Sim.Player.Position), new Color(1f, .33f, .49f), .3f, 2f);
                _combatView?.Text(Sim.Player.Position + new Vec2(0, .92), "기절!", new Color(1f, .55f, .66f), 20);
                Log("기절 — 동료가 곁에서 5초간 치료하면 체력 50%로 부활합니다");
            };
            Sim.Revived += at => { _feedback?.Kick(9f, Vector2.zero); _feedback?.Hitstop(60f); _fx?.BigRing(V(at), new Color(1f, .83f, .43f), 1.8f); _combatView?.Text(at + new Vec2(0, .9), "긴급 재기동", new Color(1f, .83f, .43f), 20); Log("긴급 재기동 — 체력 35%로 다시 일어섰다"); };
        }

        static Vector2 V(Vec2 v) => new Vector2((float)v.X, (float)v.Y);
        static Color Hex(string hex) { if (string.IsNullOrEmpty(hex) || !ColorUtility.TryParseHtmlString(hex, out var c)) return Color.white; return c; }
        static string PatternName(string p) => p switch { "wallField" => "암벽 융기", "wallWave" => "지각 파동", "wallPrison" => "석화 감옥!", "dashPrison" => "협곡 돌진 — 갇혔다!", _ => p };

        void Log(string s)
        {
            _log.Add(s);
            if (_log.Count > 6) _log.RemoveAt(0);
        }

        // ───────────────────────────── 씬 구성
        void BuildCamera()
        {
            var camGo = Camera.main != null ? Camera.main.gameObject : new GameObject("Main Camera");
            camGo.tag = "MainCamera";

            // UnityEngine.Object 에는 `??` 를 쓰면 안 된다. GetComponent 가 돌려주는 "가짜 null"
            // 은 C# 기준으로는 null 이 아니라서 `??` 가 우변으로 넘어가지 않는다.
            if (!camGo.TryGetComponent(out _cam)) _cam = camGo.AddComponent<Camera>();

            _cam.orthographic = true;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.04f, 0.03f, 0.07f);
            if (!camGo.TryGetComponent<UniversalAdditionalCameraData>(out var camData))
                camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;   // Volume 오버라이드가 먹으려면 필요하다
            camData.antialiasing = AntialiasingMode.None;
            if (!camGo.TryGetComponent(out _rig)) _rig = camGo.AddComponent<CameraRig>();

            if (!camGo.TryGetComponent(out _feedback)) _feedback = camGo.AddComponent<Feedback>();
        }

        void BuildWorld()
        {
            var gridGo = new GameObject("Grid");
            var grid = gridGo.AddComponent<Grid>();
            grid.cellSize = new Vector3(1, 1, 0);   // 1셀 = 1유닛

            Tilemap Layer(string name, int order)
            {
                var go = new GameObject(name);
                go.transform.SetParent(gridGo.transform, false);
                var tm = go.AddComponent<Tilemap>();
                var tr = go.AddComponent<TilemapRenderer>();
                tr.sortingOrder = order;
                return tm;
            }

            var floor = Layer("Floor", 0);
            var walls = Layer("Walls", 10);
            var coreTop = Layer("CoreTop", 20);

            var wrGo = new GameObject("WorldRenderer");
            _worldRenderer = wrGo.AddComponent<WorldRenderer>();
#if UNITY_EDITOR
            _worldRenderer.EditorAssign(_tileSet, floor, walls, coreTop);
#endif
            _worldRenderer.Bind(Sim.World);
        }

        void BuildPlayer()
        {
            var go = new GameObject("Player");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 30;
            _playerView = go.AddComponent<PlayerView>();

            // 인스펙터 직렬화 없이 붙였으므로 렌더러를 직접 연결한다
            var f = typeof(PlayerView).GetField("_renderer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            f?.SetValue(_playerView, sr);

            LoadRoleFrames(_role);

            _lootView = new GameObject("Loot").AddComponent<LootView>();
            _enemyView = new GameObject("Enemies").AddComponent<EnemyView>();
            _enemyView.Bind(_monsterSheets);
            _crewView = new GameObject("Crew").AddComponent<CrewView>();
            _crewView.Bind(_sheets);
            _enemyView.BossFacing = e => Sim.Bosses?.Boss != null && Sim.Bosses.Boss.Body == e ? Sim.Bosses.Boss.Facing : -1;
            _combatView = new GameObject("Combat").AddComponent<CombatView>();
            _fx = new GameObject("Fx").AddComponent<FxSystem>();
        }

        void BuildLighting()
        {
            var root = new GameObject("Lighting");
            _lightRoot = root.transform;

            // 전역광 — 원본 TE.ambient. 아무것도 없는 곳도 완전 검정은 아니다.
            var globalGo = new GameObject("Global Light 2D");
            globalGo.transform.SetParent(root.transform, false);
            _globalLight = globalGo.AddComponent<Light2D>();
            _globalLight.lightType = Light2D.LightType.Global;
            _globalLight.intensity = _ambientIntensity;
            _globalLight.color = new Color(0.62f, 0.58f, 0.80f);

            // 손전등 — 원본 halfAngle 28°, flashRange 468px = 9.36셀. F 로 켜고 끈다.
            var flashGo = new GameObject("Flashlight");
            flashGo.transform.SetParent(root.transform, false);
            _flashlight = flashGo.AddComponent<Light2D>();
            _flashlight.lightType = Light2D.LightType.Point;
            _flashlight.pointLightInnerAngle = 40f;
            _flashlight.pointLightOuterAngle = 56f;
            _flashlight.pointLightInnerRadius = 0.6f;
            _flashlight.pointLightOuterRadius = 9.36f;
            _flashlight.intensity = 1.35f;
            _flashlight.color = new Color(1f, 0.94f, 0.80f);

            // 플레이어를 감싸는 약한 원 — 손전등을 꺼도 발밑은 보인다
            var haloGo = new GameObject("Player Halo");
            haloGo.transform.SetParent(root.transform, false);
            _playerHalo = haloGo.AddComponent<Light2D>();
            _playerHalo.lightType = Light2D.LightType.Point;
            _playerHalo.pointLightInnerAngle = 360f;
            _playerHalo.pointLightOuterAngle = 360f;
            _playerHalo.pointLightInnerRadius = 0.2f;
            _playerHalo.pointLightOuterRadius = 2.6f;
            _playerHalo.intensity = 0.9f;
            _playerHalo.color = new Color(0.95f, 0.88f, 0.78f);

            // 던전 랜턴 — 생성기가 배치한 자리 그대로
            var lamps = Sim.Generation?.Lamps;
            if (lamps != null)
            {
                foreach (var (col, row) in lamps)
                {
                    var go = new GameObject($"Lamp_{col}_{row}");
                    go.transform.SetParent(root.transform, false);
                    go.transform.position = new Vector3(col + 0.5f, row + 0.5f, 0f);
                    var l = go.AddComponent<Light2D>();
                    l.lightType = Light2D.LightType.Point;
                    l.pointLightInnerAngle = 360f;
                    l.pointLightOuterAngle = 360f;
                    l.pointLightInnerRadius = 0.4f;
                    l.pointLightOuterRadius = 5.2f;   // 원본 DEMO.lampRadius 94px = 1.88셀. 빛은 더 넓게 퍼진다.
                    l.intensity = 1.1f;
                    l.color = new Color(1f, 0.69f, 0.28f);   // 원본 hue '#FFB048'
                    _lamps.Add(l);
                }
            }

            // 시야 밖 어둠 — 조명 위에 덮인다
            var darkGo = new GameObject("Darkness");
            darkGo.transform.SetParent(_cam.transform, false);
            darkGo.transform.localPosition = new Vector3(0, 0, 1f);
            _darkness = darkGo.AddComponent<DarknessOverlay>();
            _darkness.Bind(Sim.Los, Sim.World.Cols, Sim.World.Rows, _cam);

            BuildVolume(root);

            // 벽이 빛을 가리게 한다. 리플렉션이 안 되면 조용히 건너뛴다.
            _wallShadows = root.AddComponent<WallShadowBuilder>();
            _wallShadows.Bind(Sim.World);
        }

        /// <summary>지층별 Volume 프로파일. 원본 LX 4레이어(contrast · zone · core)를 대신한다.</summary>
        void BuildVolume(GameObject root)
        {
            string[] names = { "Stratum1_Surface", "Stratum2_Fracture", "Stratum3_Core", "Abyss" };
            int idx = Mathf.Clamp(Sim.Depth - 1, 0, names.Length - 1);
            var profile = Resources.Load<VolumeProfile>("Volume_" + names[idx]);
            if (profile == null)
            {
                Debug.LogWarning("[M2] Volume 프로파일을 찾지 못했다. " +
                    "메뉴 'Tunnel Crew/M2 · 지층별 Volume 프로파일 생성' 을 실행할 것.");
                return;
            }

            var go = new GameObject("Global Volume");
            go.transform.SetParent(root.transform, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 0f;
            _volume.sharedProfile = profile;
        }

        void UpdateLighting()
        {
            var p = Sim.Player;
            var pos = new Vector3((float)p.Position.X, (float)p.Position.Y, 0f);

            _playerHalo.transform.position = pos;
            _flashlight.transform.position = pos;
            // Light2D 스팟은 위쪽(+Y)이 기준이라 90도를 뺀다
            _flashlight.transform.rotation =
                Quaternion.Euler(0, 0, (float)(p.Aim * Mathf.Rad2Deg) - 90f);
            _flashlight.enabled = _flashlightOn;

            // 보스 조명 — 원본 BOSS_TUNE lightRadiusMul 2.4 · intensity .5 (맥동 4%)
            var boss = Sim.Bosses?.Boss;
            if (_bossLight == null)
            {
                var bg = new GameObject("Boss Light"); bg.transform.SetParent(_lightRoot, false);
                _bossLight = bg.AddComponent<Light2D>();
                _bossLight.lightType = Light2D.LightType.Point;
                _bossLight.pointLightInnerAngle = 360f; _bossLight.pointLightOuterAngle = 360f;
                _bossLight.pointLightInnerRadius = 0.5f;
                _bossLight.color = new Color(1f, 0.45f, 0.55f);
            }
            _bossLight.enabled = boss != null && boss.Body.Alive;
            if (_bossLight.enabled)
            {
                float r = (float)boss.Body.Radius;
                _bossLight.transform.position = new Vector3((float)boss.Body.Position.X, (float)boss.Body.Position.Y, 0);
                _bossLight.pointLightOuterRadius = r * 2.4f;
                _bossLight.intensity = 0.5f * (1f + 0.04f * Mathf.Sin(Time.time * 1.15f * Mathf.PI * 2f));
            }

            // 플레어 · 엔지니어 노드 조명 — 개수만큼 Light2D 를 재사용
            var flares = Sim.Roles.Flares;
            while (_flareLights.Count < flares.Count)
            {
                var go = new GameObject("Flare Light");
                go.transform.SetParent(_lightRoot, false);
                var l = go.AddComponent<Light2D>();
                l.lightType = Light2D.LightType.Point;
                l.pointLightInnerAngle = 360f; l.pointLightOuterAngle = 360f;
                l.pointLightInnerRadius = 0.3f;
                _flareLights.Add(l);
            }
            for (int i = 0; i < _flareLights.Count; i++)
            {
                bool on = i < flares.Count;
                _flareLights[i].enabled = on;
                if (!on) continue;
                var f = flares[i];
                float life = (float)(f.Ttl / System.Math.Max(0.01, f.MaxTtl));
                _flareLights[i].transform.position = new Vector3((float)f.Position.X, (float)f.Position.Y, 0);
                _flareLights[i].pointLightOuterRadius = (float)f.LightRadius;
                _flareLights[i].intensity = (f.IsEngineerNode ? 0.9f : 1.3f) * Mathf.Clamp01(life * 3f);
                _flareLights[i].color = f.IsEngineerNode ? new Color(0.5f, 0.95f, 0.85f) : new Color(1f, 0.85f, 0.45f);
            }
        }

        void LoadRoleFrames(RoleId role)
        {
            if (!_sheets.TryGetValue(role, out var sheet) && !_sheets.TryGetValue(RoleId.Driller, out sheet)) return;
            foreach (var d in sheet.directions)
                if (d.walk != null && d.walk.Length > 0)
                    _playerView.SetWalkFrames(d.direction, d.walk);
        }

        /// <summary>
        /// 확인용 — 시작 직후 플레이어 주변에 적을 깐다.
        /// 스포너의 스폰 링은 어그로 반경(35셀) 기준이라 20셀 밖에 떨어진다. 그대로 두면
        /// 배회 상태로 멀리 있어 한참 못 만나므로, 여기서는 3.5~7셀 안의 빈 칸에 놓고 추격 상태로 시작시킨다.
        /// </summary>
        void Prespawn()
        {
            var world = Sim.World;
            var pp = Sim.Player.Position;
            var spots = new List<Vec2>();
            for (int r = 2; r < world.Rows - 2; r++)
                for (int c = 2; c < world.Cols - 2; c++)
                {
                    if (world.IsSolid(c, r)) continue;
                    var q = WorldGrid.CellCenter(c, r);
                    double d = Vec2.Distance(q, pp);
                    if (d >= 3.5 && d <= 7.0) spots.Add(q);
                }
            var rng = new System.Random(Sim.Depth * 7919 + 17);
            for (int i = 0; i < _prespawnEnemies && spots.Count > 0; i++)
            {
                var e = Sim.Enemies.Spawn(pp);
                if (e == null) break;
                int k = rng.Next(spots.Count);
                e.Position = e.Home = spots[k];
                spots.RemoveAt(k);
                e.Ai = EnemyAi.Chase;
                e.LastSeen = pp;
                e.LostTime = 0;   // 기본값 99 라 그대로면 첫 틱에 배회로 떨어진다
                e.FaceAngle = (pp - e.Position).Angle;
            }
        }

        /// <summary>층이 바뀌면 월드에 붙은 뷰를 새 WorldGrid 에 다시 묶는다.</summary>
        void RebindWorld()
        {
            _worldRenderer.Bind(Sim.World);
            _darkness.Bind(Sim.Los, Sim.World.Cols, Sim.World.Rows, _cam);
            _wallShadows.Bind(Sim.World);
            _rig.Bind(Sim.World, () => Sim.Player);
            if (_crewView != null) _crewView.crewWorld = Sim.World;
            Time.timeScale = 1f;
        }

        /// <summary>직업을 바꾸고 층을 다시 만든다 (숫자키 1~4). 결과 화면의 Enter · 스모크 테스트도 이 경로.</summary>
        public void SwitchRole(RoleId role)
        {
            _role = role;
            Sim.StartRun(role, MetaStore.Load());
            Sim.EnterDepth(_depth, DungeonConfig.Runtime);
            RebindWorld();
            LoadRoleFrames(role);
            Prespawn();
            Log($"직업 → {role}");
        }

        // ───────────────────────────── 루프
        void Update()
        {
            if (Sim?.World == null || !RunActive) return;
            var kbEsc = Keyboard.current;
            if (kbEsc != null && kbEsc.escapeKey.wasPressedThisFrame && Sim.Phase == GamePhase.Playing && !Sim.Traits.HasOffer)
            {
                Paused = !Paused;
                if (Paused) PauseMenuRequested?.Invoke();
            }
            if (Paused) return;

            var kb = Keyboard.current;
            if (kb != null && Sim.Traits.HasOffer)
            {
                if (kb.digit1Key.wasPressedThisFrame) Sim.PickTrait(0);
                else if (kb.digit2Key.wasPressedThisFrame) Sim.PickTrait(1);
                else if (kb.digit3Key.wasPressedThisFrame) Sim.PickTrait(2);
                else if (kb.tabKey.wasPressedThisFrame && Sim.RerollTraits()) Log($"다시 뽑기 (남은 {Sim.Traits.Rerolls})");
            }
            else if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) SwitchRole(RoleId.Driller);
                else if (kb.digit2Key.wasPressedThisFrame) SwitchRole(RoleId.Gunner);
                else if (kb.digit3Key.wasPressedThisFrame) SwitchRole(RoleId.Scout);
                else if (kb.digit4Key.wasPressedThisFrame) SwitchRole(RoleId.Engineer);
                else if (kb.f5Key.wasPressedThisFrame) SwitchRole(_role);
            }

            // 보스 격파 연출(2.6초) 뒤 휴식 화면 — 원본 infBossOutroTick
            if (Sim.RestPending)
            {
                _outroT += Time.unscaledDeltaTime;
                if (_outroT >= 2.6f) { _outroT = 0; Sim.EnterRest(); }
            }
            if (Sim.Phase == GamePhase.Rest && kb != null)
            {
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) { Sim.Descend(); RebindWorld(); Prespawn(); Log(Planet.DepthLabel(Sim.Depth) + " 진입"); }
                else if (kb.escapeKey.wasPressedThisFrame && Sim.LastBossTier != BossTier.Guardian) { Sim.ReturnFromRest(); Log("이 층에 남아 탈출한다"); }
            }
            if (Sim.Phase == GamePhase.Result && kb != null && kb.enterKey.wasPressedThisFrame)
            {
                if (ResultDismissed != null) ResultDismissed.Invoke(); else SwitchRole(_role);
            }

            Sim.Advance(Time.deltaTime, ReadInput());
            _playerView.Render(Sim.Player, Time.deltaTime);
            _lootView.Render(Sim.Loot);
            _enemyView.Render(Sim.Enemies.Enemies, Time.deltaTime);
            _crewView.Render(Sim.Crew, Time.deltaTime);
            _combatView.Render(Sim, Time.deltaTime);
            UpdateLighting();
        }

        SimInput ReadInput()
        {
            var input = new SimInput();
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            if (kb != null)
            {
                float x = (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0);
                // 원본은 y 가 아래로 증가한다. Unity 는 위로 증가하므로 W 가 +y 다.
                float y = (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0);
                var move = new Vector2(x, y);
                if (move.sqrMagnitude > 0.0001f) move.Normalize();
                input.Move = new Vec2(move.x, move.y);
                input.DashPressed = kb.spaceKey.wasPressedThisFrame;
                input.ReloadPressed = kb.rKey.wasPressedThisFrame;
                input.SkillQPressed = kb.qKey.wasPressedThisFrame;
                input.SkillEPressed = kb.eKey.wasPressedThisFrame;
                input.EscapePressed = kb.xKey.wasPressedThisFrame;
                if (kb.fKey.wasPressedThisFrame) _flashlightOn = !_flashlightOn;   // 원본 F 토글
            }

            if (mouse != null && _rig != null)
            {
                var w = _rig.ScreenToWorld(mouse.position.ReadValue());
                input.AimWorld = new Vec2(w.x, w.y);
                input.DrillHeld = mouse.leftButton.isPressed;
                input.FireHeld = mouse.rightButton.isPressed;
                input.PrimaryPressed = mouse.leftButton.wasPressedThisFrame;
                input.SecondaryPressed = mouse.rightButton.wasPressedThisFrame;
            }
            else input.AimWorld = Sim.Player.Position + new Vec2(1, 0);

            return input;
        }

        int VisibleCellCount()
        {
            if (Sim?.Los == null) return 0;
            int n = 0;
            foreach (var b in Sim.Los.Visible) if (b != 0) n++;
            return n;
        }

        // ───────────────────────────── HUD (IMGUI — 1080p 기준으로 스케일. M5 에서 UGUI 로)
        // 원본 무한 HUD 배치: 상단 중앙 장악도 레일(크루 아이콘 → 보스 아이콘) + 위협, 좌하단 바이탈(초상화·HP·탄창),
        // 우하단 스킬 슬롯, 하단 엣지 XP 바, 좌측 깊이 라벨. 좌표는 편집기 값이 아니라 앵커 기준 적당한 위치(2026-09-06 결정).
        GUIStyle _sBig, _sMid, _sSmall, _sTitle, _sCard, _sCardBody;
        float _k = 1f, _styleK = -1f;
        /// <summary>1080p 기준 좌표 → 실제 픽셀. GUI.matrix 로 늘리면 글자가 뭉개져서 폰트를 k 배로 굽고 좌표를 곱한다.</summary>
        Rect R(float x, float y, float w, float h) => new Rect(x * _k, y * _k, w * _k, h * _k);
        GUIStyle Sz(GUIStyle baseStyle, int px1080, TextAnchor? align = null, bool? wrap = null)
        {
            var st = new GUIStyle(baseStyle) { fontSize = Mathf.RoundToInt(px1080 * _k) };
            if (align.HasValue) st.alignment = align.Value;
            if (wrap.HasValue) st.wordWrap = wrap.Value;
            return st;
        }
        void EnsureStyles()
        {
            if (_sBig != null && Mathf.Abs(_styleK - _k) < 0.001f) return;
            _styleK = _k;
            int F(int px) => Mathf.RoundToInt(px * _k);
            _sBig = new GUIStyle(GUI.skin.label) { fontSize = F(34), fontStyle = FontStyle.Bold, richText = true, alignment = TextAnchor.MiddleCenter };
            _sMid = new GUIStyle(GUI.skin.label) { fontSize = F(22), richText = true, alignment = TextAnchor.MiddleLeft };
            _sSmall = new GUIStyle(GUI.skin.label) { fontSize = F(17), richText = true, alignment = TextAnchor.MiddleLeft };
            _sTitle = new GUIStyle(GUI.skin.label) { fontSize = F(26), fontStyle = FontStyle.Bold, richText = true, alignment = TextAnchor.MiddleLeft, wordWrap = true };
            _sCard = new GUIStyle(GUI.skin.label) { fontSize = F(24), fontStyle = FontStyle.Bold, richText = true, wordWrap = true };
            _sCardBody = new GUIStyle(GUI.skin.label) { fontSize = F(18), richText = true, wordWrap = true };
            foreach (var st in new[] { _sBig, _sMid, _sSmall, _sTitle, _sCard, _sCardBody }) st.normal.textColor = new Color(.97f, .95f, .9f);
        }

        void Bar(Rect r, float frac, Color back, Color fill, Color? edge = null)
        {
            GUI.color = back; GUI.DrawTexture(r, _white);
            GUI.color = fill; GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(frac), r.height), _white);
            if (edge.HasValue) { GUI.color = edge.Value; GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2 * _k), _white); GUI.DrawTexture(new Rect(r.x, r.yMax - 2 * _k, r.width, 2 * _k), _white); }
            GUI.color = Color.white;
        }
        void Panel(Rect r, float alpha = .55f) { GUI.color = new Color(.05f, .04f, .09f, alpha); GUI.DrawTexture(r, _white); GUI.color = Color.white; }

        void OnGUI()
        {
            if (!_showHud || Sim?.World == null || !RunActive) return;
            // 1920×1080 기준 좌표계 — 창 크기가 달라도 비율이 유지된다. R()/Sz() 가 k 를 곱한다.
            _k = Screen.height / 1080f;
            EnsureStyles();
            float W = Screen.width / _k, H = 1080f;

            var p = Sim.Player; var b = Sim.Build; var roles = Sim.Roles;

            // ── 상단 중앙: 장악도 레일 (원본 #infDomRail) — 크루 아이콘이 보스 아이콘으로 접근
            {
                float railW = 640, railH = 18, x = W * .5f - railW * .5f, y = 34;
                Panel(R(x - 70, y - 22, railW + 140, 80), .5f);
                float frac = (float)(Sim.Run.Dominance / Sim.Run.DominanceTarget);
                Bar(R(x, y, railW, railH), Mathf.Clamp01(frac), new Color(.2f, .16f, .28f), new Color(.78f, .63f, 1f), new Color(.35f, .3f, .5f));
                // 크루 아이콘 — 직업 초상화
                if (_portraits.TryGetValue(b.Role, out var por))
                {
                    float px = x + railW * Mathf.Clamp01(frac);
                    GUI.DrawTexture(R(px - 26, y - 20, 52, 52), por, ScaleMode.ScaleToFit);
                }
                if (_bossIcon != null)
                {
                    GUI.color = Sim.Bosses.Active ? new Color(1f, .33f, .49f) : new Color(1f, 1f, 1f, .9f);
                    GUI.DrawTexture(R(x + railW - 4, y - 30, 64, 64), _bossIcon, ScaleMode.ScaleToFit);
                    GUI.color = Color.white;
                }
                GUI.Label(R(x - 60, y + 22, railW + 120, 30), $"<b>장악도 {Sim.Run.Dominance:P1}</b> / {Sim.Run.DominanceTarget:P0}   ·   위협 {Sim.Run.Threat:F2}   ·   {Planet.DepthLabel(Sim.Depth)}", new GUIStyle(_sSmall) { alignment = TextAnchor.MiddleCenter });
                if (Sim.Bosses.Active)
                {
                    var bb = Sim.Bosses.Boss;
                    Bar(R(x, y + 58, railW, 14), (float)bb.HpRatio, new Color(.25f, .08f, .12f), new Color(1f, .33f, .49f));
                    GUI.Label(R(x, y + 72, railW, 26), $"<color=#ff557d><b>{bb.Def.Name}</b></color>  {bb.Body.Hp:F0} / {bb.Body.HpMax:F0}   장갑 {Sim.Bosses.ArmorAlive()}", new GUIStyle(_sSmall) { alignment = TextAnchor.MiddleCenter });
                }
            }

            // ── 좌하단: 바이탈 — 초상화 · HP · 탄창 · 드릴 열
            {
                float x = 28, y = H - 200, w = 520, h = 170;
                Panel(R(x, y, w, h));
                if (_portraits.TryGetValue(b.Role, out var por)) GUI.DrawTexture(R(x + 12, y + 12, 146, 146), por, ScaleMode.ScaleToFit);
                float cx = x + 172, cw = w - 190;
                GUI.Label(R(cx, y + 10, cw, 30), $"<b>{RoleName(b.Role)}</b>  <color=#aaa>Lv {Sim.Xp.Level}</color>{(p.Downed ? "  <color=#ff6060>다운</color>" : p.StunTime > 0 ? "  <color=#ffd36e>기절</color>" : "")}", _sMid);
                Bar(R(cx, y + 46, cw, 22), (float)(p.Hp / p.HpMax), new Color(.25f, .08f, .1f), p.Hp / p.HpMax < .22 ? new Color(1f, .3f, .3f) : new Color(.95f, .45f, .45f));
                GUI.Label(R(cx, y + 44, cw, 26), $"  HP {p.Hp:F0} / {p.HpMax:F0}", _sSmall);
                if (b.RoleHasGun)
                {
                    float af = b.IsReloading ? 1f - (float)(b.ReloadLeft / b.ReloadTime) : (float)b.Ammo / b.MagSize;
                    Bar(R(cx, y + 78, cw, 16), af, new Color(.1f, .18f, .2f), b.IsReloading ? new Color(.5f, .92f, .82f) : new Color(1f, .83f, .43f));
                    GUI.Label(R(cx, y + 74, cw, 24), b.IsReloading ? $"  재장전 {b.ReloadLeft:F1}s" : $"  탄 {b.Ammo} / {b.MagSize}", _sSmall);
                }
                if (b.RoleDigMul > 0)
                {
                    Bar(R(cx, y + 104, cw, 12), (float)p.DrillHeat, new Color(.15f, .12f, .12f), p.DrillHeatLock > 0 ? new Color(1f, .3f, .2f) : Color.Lerp(new Color(.9f, .7f, .3f), new Color(1f, .35f, .2f), (float)p.DrillHeat));
                    GUI.Label(R(cx, y + 118, cw, 24), $"드릴 열 {p.DrillHeat:P0}{(p.DrillHeatLock > 0 ? $"  <color=#ff6060>과열 {p.DrillHeatLock:F1}s</color>" : "")}   예열 {p.DrillWarm:P0}", _sSmall);
                }
                GUI.Label(R(cx, y + 140, cw, 24), $"코어 <b>{Sim.Loot.Core}</b>   PULP {Sim.Loot.Pulp}   BLOOM {Sim.Loot.Bloom}", _sSmall);
            }

            // ── 우하단: 스킬 슬롯 Q / E / Space
            {
                float slot = 96, gap = 14, n = 3;
                float x = W - 28 - (slot * n + gap * (n - 1)), y = H - 200;
                Panel(R(x - 14, y, slot * n + gap * (n - 1) + 28, 170));
                void Slot(int i, string key, string name, double cd, double cdMax, bool available)
                {
                    float sx = x + i * (slot + gap), sy = y + 14;
                    var r = R(sx, sy, slot, slot);
                    GUI.color = available ? new Color(.16f, .14f, .24f) : new Color(.1f, .1f, .12f); GUI.DrawTexture(r, _white);
                    if (cd > 0 && cdMax > 0) { GUI.color = new Color(0, 0, 0, .6f); GUI.DrawTexture(new Rect(r.x, r.y, r.width, r.height * (float)Math.Min(1, cd / cdMax)), _white); }
                    GUI.color = Color.white;
                    GUI.Label(R(sx + 6, sy + 2, 40, 28), $"<b>{key}</b>", _sMid);
                    GUI.Label(R(sx, sy + slot - 30, slot, 28), cd > 0 ? $"{cd:F1}s" : available ? "준비" : "—", Sz(_sSmall, 17, TextAnchor.MiddleCenter));
                    GUI.Label(R(sx, sy + slot + 4, slot, 44), name, Sz(_sSmall, 15, TextAnchor.UpperCenter, true));
                }
                Slot(0, "Q", QName(b.Role), roles.QCooldown, RoleSystem.QCooldownFor(b.Role), true);
                Slot(1, "E", EName(b.Role), roles.ECooldown, Math.Max(0.2, RoleSystem.ECooldownFor(b.Role)), RoleSystem.HasE(b.Role));
                Slot(2, "␣", "대시", p.DashCooldown, SimTuning.DashCooldown, true);
            }

            // ── 하단 엣지: XP 바
            {
                Bar(R(0, H - 8, W, 8), (float)Sim.Xp.Xp / Math.Max(1, Sim.Xp.XpNeed), new Color(.1f, .1f, .14f), new Color(1f, .83f, .43f));
                GUI.Label(R(W * .5f - 200, H - 40, 400, 28), $"XP {Sim.Xp.Xp} / {Sim.Xp.XpNeed}", new GUIStyle(_sSmall) { alignment = TextAnchor.MiddleCenter });
            }

            // ── 좌상단: 로그 · 탈출 · 키 가이드
            {
                float x = 28, y = 28;
                Panel(R(x, y, 560, 40 + _log.Count * 26 + (Sim.Escape.Active ? 30 : 0)), .4f);
                GUI.Label(R(x + 12, y + 6, 540, 28), "WASD 이동 · 좌클릭 드릴 · 우클릭 사격 · R 재장전 · Q/E 스킬 · Space 대시 · X 탈출 · F 손전등", new GUIStyle(_sSmall) { fontSize = Mathf.RoundToInt(14 * _k), normal = { textColor = new Color(.7f, .68f, .75f) } });
                float ly = y + 34;
                if (Sim.Escape.Active)
                {
                    string esc = Sim.Escape.Phase switch
                    {
                        EscapePhase.Placing => "탈출 지점 지정 중 — 좌클릭 확정 · 우클릭 취소",
                        EscapePhase.Incoming => $"탈출 포트 도착까지 {Mathf.CeilToInt((float)(Sim.Escape.Need - Sim.Escape.Elapsed))}초 — 지점을 사수하세요",
                        EscapePhase.Ready => $"탈출 포트 도착 — 탑승 {Sim.Escape.BoardProgress:P0}" + (Sim.Crew.EscapeCount() is var ec && ec.HasValue ? $"   생존자 탑승 {ec.Value.boarded + (Sim.Escape.BoardProgress >= 1 ? 1 : 0)} / {ec.Value.total}" : ""),
                        _ => "탑승 완료",
                    };
                    GUI.Label(R(x + 12, ly, 540, 28), $"<color=#ff8da8><b>{esc}</b></color>", _sSmall); ly += 30;
                }
                foreach (var line in _log) { GUI.Label(R(x + 12, ly, 540, 26), "<color=#ffd080>· " + line + "</color>", _sSmall); ly += 26; }
            }

            // ── 우상단: AI 크루 — 각 AI 가 지금 뭘 하는지 (원본 .aiHud). 플레이테스트에서 이게 제일 중요하다
            if (Sim.Crew.Members.Count > 0)
            {
                float rw = 420, rh = 44, x = W - rw - 28, y = 130;
                for (int i = 0; i < Sim.Crew.Members.Count; i++)
                {
                    var m = Sim.Crew.Members[i];
                    var col = Hex(AiCrewSystem.ColorOf(m.Role));
                    var rc = R(x, y + i * (rh + 6), rw, rh);
                    Panel(rc, .62f);
                    GUI.color = col; GUI.DrawTexture(R(x + 12, y + i * (rh + 6) + 16, 12, 12), _white); GUI.color = Color.white;
                    GUI.Label(R(x + 32, y + i * (rh + 6), 110, rh), $"<b><color=#{ColorUtility.ToHtmlStringRGB(col)}>{AiCrewSystem.NameOf(m.Role)}</color></b> <color=#ffd36e>Lv{m.Level}</color>", _sSmall);
                    float hp = Mathf.Clamp01((float)(m.Hp / m.HpMax));
                    Bar(R(x + 150, y + i * (rh + 6) + 12, 70, 10), hp, new Color(.2f, .12f, .16f), m.Down ? new Color(1f, .33f, .49f) : hp > .5f ? new Color(.5f, .92f, .82f) : hp > .25f ? new Color(1f, .83f, .43f) : new Color(1f, .55f, .66f));
                    Bar(R(x + 150, y + i * (rh + 6) + 26, 70, 5), Mathf.Clamp01((float)m.Xp / Mathf.Max(1, m.XpNeed)), new Color(.16f, .14f, .22f), new Color(.78f, .63f, 1f));
                    GUI.Label(R(x + 232, y + i * (rh + 6), rw - 244, rh), m.Down ? "<color=#ff8da8>다운 · 구조 필요</color>" : $"<color=#b8a9d4>{m.StateLabel}</color>", Sz(_sSmall, 15, TextAnchor.MiddleLeft));
                }
            }

            // ── 기절 — 동료의 구조를 기다린다 (원본 playerEnterDowned · infDownedTick)
            if (Sim.PlayerDownedWaiting)
            {
                Panel(R(W * .5f - 360, H * .5f - 70, 720, 120), .7f);
                GUI.Label(R(W * .5f - 340, H * .5f - 60, 680, 44), "<color=#ff8da8><b>기절</b></color>  —  동료가 곁에서 5초간 치료하면 체력 50%로 부활", new GUIStyle(_sMid) { alignment = TextAnchor.MiddleCenter });
                Bar(R(W * .5f - 300, H * .5f - 8, 600, 18), (float)Sim.ReviveProgress, new Color(.2f, .12f, .16f), new Color(.5f, .92f, .82f));
                GUI.Label(R(W * .5f - 300, H * .5f + 14, 600, 28), Sim.Crew.HelpersNear(Sim.Player.Position, TunnelSim.ReviveRange) > 0 ? $"치료 중 {Sim.ReviveProgress:P0}" : "구조자가 오는 중…", new GUIStyle(_sSmall) { alignment = TextAnchor.MiddleCenter });
            }

            // ── 특성 카드 3택 — 화면 중앙 하단, 크게
            if (Sim.Traits.HasOffer)
            {
                var cards = Sim.Traits.Offer;
                float cw = 400, ch = 250, gap = 24;
                float total = cards.Length * cw + (cards.Length - 1) * gap;
                float x0 = W * .5f - total * .5f, y0 = H * .5f - ch * .5f + 40;
                string head = Sim.Traits.OfferIsLegend ? "심층 보상 · 전설 카드" : $"LEVEL {Sim.Xp.Level} · {RoleName(Sim.Build.Role)} 특성";
                Panel(R(x0 - 30, y0 - 90, total + 60, ch + 130), .72f);
                GUI.Label(R(x0, y0 - 80, total, 44), $"<b>{head}</b>", _sBig);
                GUI.Label(R(x0, y0 - 38, total, 30), Sim.Traits.OfferIsLegend ? "1 · 2 · 3 으로 선택" : $"1 · 2 · 3 으로 선택   ·   Tab 다시 뽑기 ({Sim.Traits.Rerolls})", new GUIStyle(_sSmall) { alignment = TextAnchor.MiddleCenter });
                for (int i = 0; i < cards.Length; i++)
                {
                    var c = cards[i];
                    var rc = R(x0 + i * (cw + gap), y0, cw, ch);
                    Color tier = c.Tier >= 4 ? new Color(1f, .83f, .43f) : c.Tier == 3 ? new Color(.78f, .63f, 1f) : c.Tier == 2 ? new Color(.5f, .92f, .82f) : new Color(.85f, .85f, .85f);
                    GUI.color = new Color(.12f, .1f, .18f, .96f); GUI.DrawTexture(rc, _white);
                    GUI.color = tier; GUI.DrawTexture(new Rect(rc.x, rc.y, rc.width, 6 * _k), _white); GUI.DrawTexture(new Rect(rc.x, rc.y, 6 * _k, rc.height), _white);
                    GUI.color = Color.white;
                    if (_portraits.TryGetValue(Sim.Build.Role, out var por) && c.Role != null) { GUI.color = new Color(1, 1, 1, .18f); GUI.DrawTexture(new Rect(rc.xMax - 150 * _k, rc.yMax - 150 * _k, 150 * _k, 150 * _k), por, ScaleMode.ScaleToFit); GUI.color = Color.white; }
                    GUILayout.BeginArea(new Rect(rc.x + 18 * _k, rc.y + 14 * _k, rc.width - 36 * _k, rc.height - 28 * _k));
                    GUILayout.Label($"<color=#{ColorUtility.ToHtmlStringRGB(tier)}>[{i + 1}]  T{c.Tier} · {c.Kind}</color>", _sSmall);
                    GUILayout.Label(c.Name, _sCard);
                    GUILayout.Label(c.Desc, _sCardBody);
                    int st = Sim.Traits.StackOf(c.Id);
                    if (st > 0) GUILayout.Label($"<color=#aaa>보유 ×{st}</color>", _sCardBody);
                    GUILayout.EndArea();
                }
            }

            // ── 휴식 / 결과 오버레이
            if (Sim.Phase == GamePhase.Rest || Sim.Phase == GamePhase.Result)
            {
                GUI.color = new Color(0, 0, 0, .62f); GUI.DrawTexture(R(0, 0, W, H), _white); GUI.color = Color.white;
                var rect = R(W * .5f - 420, 120, 840, 200);
                Panel(rect, .7f);
                var mid = new GUIStyle(_sMid) { alignment = TextAnchor.MiddleCenter };
                GUILayout.BeginArea(new Rect(rect.x + 20 * _k, rect.y + 16 * _k, rect.width - 40 * _k, rect.height - 32 * _k));
                if (Sim.Phase == GamePhase.Rest)
                {
                    bool guardian = Sim.LastBossTier == BossTier.Guardian;
                    GUILayout.Label(guardian ? $"{Planet.DepthLabel(Sim.Depth)} — 수호자 격퇴" : Sim.LastBossTier == BossTier.Apex ? "중심부 보스 격파" : $"{Planet.DepthLabel(Sim.Depth)} — 변종 격파", _sBig);
                    GUILayout.Label((guardian ? "더 깊은 곳에서 거대한 기척이 느껴진다 · " : "탈출 포트가 자동 요청되었다 · ") + $"체력 30% 회복 · 코어 {Sim.Loot.Core} 운반 중", mid);
                    GUILayout.Space(8);
                    if (!Sim.RestChosen) GUILayout.Label("카드를 고르면 하강 버튼이 열린다", mid);
                    else GUILayout.Label(guardian ? "<b>Enter</b> 다음 지층으로" : "<b>Enter</b> 이상지대로 (엔드게임)   ·   <b>Esc</b> 이 층에 남아 탈출", mid);
                }
                else
                {
                    GUILayout.Label(Sim.RunEscaped ? "탈출 성공" : "런 종료", _sBig);
                    GUILayout.Label($"{Sim.RunEndReason} · 심층 {Sim.Depth}", mid);
                    var st = Sim.LastSettlement; var meta = MetaStore.Load();
                    GUILayout.Label($"도달 심층 {Sim.Depth}   처치 보스 {Sim.Run.BossesKilled}   파괴 블록 {Sim.World.BlocksBroken}   " +
                        (Sim.RunEscaped ? $"확보 코어 <b>{st.Returned}</b>" : $"<color=#ff8da8>소실 코어 {st.Lost}</color>" + (st.Kept > 0 ? $"   <color=#7febd0>회수 보존 {st.Kept}</color>" : "")), mid);
                    GUILayout.Label($"기지 보관 코어 <b>{meta.bankedCores}</b>   최고 심층 {meta.bestDepth}   누적 보스 {meta.totalBosses}   생환 {meta.escapes}", mid);
                    GUILayout.Space(8);
                    GUILayout.Label(ResultDismissed != null ? "<b>Enter</b> 귀환 정산으로" : "<b>Enter</b> 같은 직업으로 다시", mid);
                }
                GUILayout.EndArea();
            }

            // 피격 비네트 — Feedback.HurtLevel
            if (_feedback != null && _feedback.HurtLevel > 0)
            {
                float a = 0.12f * _feedback.HurtLevel * _feedback.HurtProgress;
                GUI.color = new Color(1f, 0.15f, 0.1f, a);
                GUI.DrawTexture(R(0, 0, W, H), _white);
                GUI.color = Color.white;
            }
        }

        static string RoleName(RoleId r) => r switch { RoleId.Driller => "드릴러", RoleId.Gunner => "거너", RoleId.Scout => "스카우트", _ => "엔지니어" };
        static string QName(RoleId r) => r switch { RoleId.Driller => "돌파 파기", RoleId.Gunner => "방어막", RoleId.Scout => "장거리 플레어", _ => "전력 노드" };
        static string EName(RoleId r) => r switch { RoleId.Gunner => "조기 기폭", RoleId.Scout => "그래플 훅", RoleId.Engineer => "센트리 터렛", _ => "—" };
    }
}
