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

        CameraRig _rig;
        Camera _cam;
        WorldRenderer _worldRenderer;
        PlayerView _playerView;
        LootView _lootView;
        EnemyView _enemyView;
        CombatView _combatView;
        Feedback _feedback;
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

            Sim = new TunnelSim();
            Sim.StartRun(_role);
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
            };
            Sim.DrillBeat += (pos, dmg) => _feedback?.DrillBeat(V(Vec2.FromAngle(Sim.Player.Aim)));
            Sim.PlayerDashed += e => _feedback?.Dash(V(e.Direction));
            Sim.EnemyHurt += e =>
            {
                _feedback?.EnemyHit(e.Killed, e.Enemy.IsApex, e.Enemy.IsBoss, (float)e.Damage, V(e.HitDir));
                var col = e.WasCritical ? new Color(1f, 0.85f, 0.3f) : e.Killed ? new Color(1f, 0.5f, 0.4f) : Color.white;
                _combatView?.Text(e.Enemy.Position, ((int)System.Math.Round(e.Damage)).ToString(), col, e.Killed ? 0.42f : 0.32f);
            };
            Sim.PlayerHurt += e =>
            {
                _feedback?.PlayerHurt((float)e.Damage, (float)Sim.Player.Hp, (float)Sim.Player.HpMax, V(e.HitDir));
                _combatView?.Text(Sim.Player.Position, "-" + (int)System.Math.Round(e.Damage), new Color(1f, 0.35f, 0.35f), 0.4f);
                Log(e.Downed ? "다운!" : $"피격 -{e.Damage:F0}");
            };
            Sim.ProjectileFired += e => _feedback?.Shot((float)e.Angle, e.VisualId);
            Sim.ProjectileEnded += e => { if (e.Exploded) { _feedback?.Kick(2.2f, Vector2.zero); _feedback?.Hitstop(18f); } };
            Sim.ReloadChanged += e => { if (e.Started) Log(e.Manual ? "재장전 (R)" : "탄창 비어 재장전"); };
            Sim.SkillUsed += e => { Log($"{e.Role} {(e.IsQ ? "Q" : "E")} 사용"); _feedback?.Kick(0.9f, Vector2.zero); };
            Sim.BreakerExploded += e => { _feedback?.Kick(3.0f, Vector2.zero); _feedback?.Hitstop(30f); Log(e.Early ? "파쇄탄 조기 폭발" : "파쇄탄 폭발"); };
            Sim.FoundationBroken += e => { _feedback?.Kick(4.5f, Vector2.zero); _feedback?.Hitstop(60f); Log("기반암 균열 파쇄"); };
            Sim.EnemySpawned += e => { if (e.Enemy.IsApex) Log("광란종 출현"); };
            Sim.DominanceReached += () => Log($"장악도 {Sim.Run.DominanceTarget:P0} 도달");
            Sim.BossSpawned += e => { _feedback?.Kick(9f, Vector2.zero); _feedback?.Hitstop(60f); Log($"{e.Boss.Def.Name} 출현"); };
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
            Sim.BossWallRaised += e => _feedback?.Kick(0.6f, Vector2.zero);
            Sim.BossShotHit += e => { _feedback?.Kick(e.HitPlayer ? 5f : 2f, Vector2.zero); if (e.HitPlayer) _feedback?.Hitstop(46f); };
            Sim.BossDefeated += e => { _feedback?.Kick(9f, Vector2.zero); _feedback?.Hitstop(90f); Log($"{e.Boss.Def.Name} 격파 · 코어 +{e.CoreReward}"); };
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
            Sim.RunEnded += (escaped, reason) => { Time.timeScale = 1f; Log(escaped ? "탈출 성공" : "런 종료 — " + reason); };
        }

        static Vector2 V(Vec2 v) => new Vector2((float)v.X, (float)v.Y);
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
            _enemyView.BossFacing = e => Sim.Bosses?.Boss != null && Sim.Bosses.Boss.Body == e ? Sim.Bosses.Boss.Facing : -1;
            _combatView = new GameObject("Combat").AddComponent<CombatView>();
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
            Time.timeScale = 1f;
        }

        /// <summary>직업을 바꾸고 층을 다시 만든다 (숫자키 1~4). 결과 화면의 Enter · 스모크 테스트도 이 경로.</summary>
        public void SwitchRole(RoleId role)
        {
            _role = role;
            Sim.StartRun(role);
            Sim.EnterDepth(_depth, DungeonConfig.Runtime);
            RebindWorld();
            LoadRoleFrames(role);
            Prespawn();
            Log($"직업 → {role}");
        }

        // ───────────────────────────── 루프
        void Update()
        {
            if (Sim?.World == null) return;

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
            if (Sim.Phase == GamePhase.Result && kb != null && kb.enterKey.wasPressedThisFrame) SwitchRole(_role);

            Sim.Advance(Time.deltaTime, ReadInput());
            _playerView.Render(Sim.Player, Time.deltaTime);
            _lootView.Render(Sim.Loot);
            _enemyView.Render(Sim.Enemies.Enemies, Time.deltaTime);
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

        // ───────────────────────────── 임시 HUD (IMGUI — M4 에서 UI Toolkit 으로)
        void OnGUI()
        {
            if (!_showHud || Sim?.World == null) return;
            var p = Sim.Player;
            var b = Sim.Build;
            var roles = Sim.Roles;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };

            GUILayout.BeginArea(new Rect(12, 12, 560, 360), GUI.skin.box);
            GUILayout.Label($"<b>{RoleName(b.Role)}</b>  ·  심층 {Sim.Depth}  ·  {Sim.World.Cols}×{Sim.World.Rows}  " +
                            $"·  {(1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime)):F0} fps", style);
            GUILayout.Label($"<b>HP {p.Hp:F0}/{p.HpMax:F0}</b>{(p.Downed ? "  <color=#ff6060>다운</color>" : p.StunTime > 0 ? "  기절" : "")}" +
                            $"   위협 {Sim.Enemies.Threat:F2}   적 {Sim.Enemies.Enemies.Count}마리", style);
            GUILayout.Label($"{Planet.DepthLabel(Sim.Depth)}   장악도 <b>{Sim.Run.Dominance:P1}</b> / {Sim.Run.DominanceTarget:P0}   위협 {Sim.Run.Threat:F2}   캡 {Sim.Run.EnemyCap}   스폰 {Sim.Run.SpawnInterval:F1}s×{Sim.Run.SpawnBurst}", style);
            GUILayout.Label($"Lv {Sim.Xp.Level}   XP {Sim.Xp.Xp}/{Sim.Xp.XpNeed}   코어 {Sim.Loot.Core}   PULP {Sim.Loot.Pulp}   BLOOM {Sim.Loot.Bloom}   부순 블록 {Sim.World.BlocksBroken}", style);
            if (Sim.Bosses.Active)
            {
                var bb = Sim.Bosses.Boss;
                GUILayout.Label($"<color=#ff557d><b>{bb.Def.Name}</b>  HP {bb.Body.Hp:F0}/{bb.Body.HpMax:F0}  장갑 {Sim.Bosses.ArmorAlive()}  {(bb.Dash != BossDashPhase.None ? bb.Dash.ToString() : bb.LastAttack)}</color>", style);
            }

            string ammo = b.RoleHasGun
                ? (b.IsReloading ? $"재장전 {b.ReloadLeft:F2}s" : $"탄 {b.Ammo}/{b.MagSize}")
                : "총 없음";
            GUILayout.Label($"{ammo}   드릴 예열 {p.DrillWarm:P0}   과열 {p.DrillHeat:P0}{(p.DrillHeatLock > 0 ? $" 잠금 {p.DrillHeatLock:F1}s" : "")}", style);

            string q = roles.QCooldown > 0 ? $"{roles.QCooldown:F1}s" : "준비";
            string e = RoleSystem.HasE(b.Role) ? (roles.ECooldown > 0 ? $"{roles.ECooldown:F1}s" : "준비") : "—";
            GUILayout.Label($"Q {QName(b.Role)} [{q}]   E {EName(b.Role)} [{e}]   " +
                            $"대시 {(p.DashActive ? "진행" : p.DashCooldown > 0 ? $"{p.DashCooldown:F1}s" : "준비")}", style);

            string extra = b.Role switch
            {
                RoleId.Gunner => $"방어막 {roles.ShieldTime:F1}s   파쇄탄 쿨 {roles.BreakerCooldown:F1}s   장착 {roles.Breakers.Count}",
                RoleId.Driller => $"돌파 {roles.BreachTime:F1}s   균열 {roles.Cracks.Count}칸",
                RoleId.Scout => $"플레어 {roles.Flares.Count(f => !f.IsEngineerNode)}개",
                RoleId.Engineer => $"노드 {roles.Nodes.Count}/{b.Roles.EngineerMaxNodes}   센트리 {roles.Turrets.Count}/{b.Roles.EngineerMaxTurrets}",
                _ => "",
            };
            GUILayout.Label(extra, style);
            if (Sim.Escape.Active)
            {
                string esc = Sim.Escape.Phase switch
                {
                    EscapePhase.Placing => "지점 지정 중",
                    EscapePhase.Incoming => $"포트 도착까지 {Mathf.CeilToInt((float)(Sim.Escape.Need - Sim.Escape.Elapsed))}초",
                    EscapePhase.Ready => $"포트 도착 — 탑승 {Sim.Escape.BoardProgress:P0}",
                    _ => "탑승 완료",
                };
                GUILayout.Label($"<color=#ff8da8>탈출 포트: {esc}</color>", style);
            }
            GUILayout.Space(4);
            GUILayout.Label($"손전등 {(_flashlightOn ? "켜짐" : "꺼짐")}   보이는 칸 {VisibleCellCount()}   timeScale {Time.timeScale:F2}", style);
            GUILayout.Label("WASD 이동 · 마우스 조준 · 좌클릭 드릴(거너: 파쇄탄) · 우클릭 사격 · R 재장전", style);
            GUILayout.Label("Q/E 스킬 · Space 대시 · F 손전등 · 1~4 직업 교체 · F5 층 재생성", style);
            GUILayout.Space(4);
            foreach (var line in _log) GUILayout.Label("<color=#ffd080>· " + line + "</color>", style);
            GUILayout.EndArea();

            // 휴식 / 결과 오버레이 (IMGUI 임시 — M5 에서 UI Toolkit)
            if (Sim.Phase == GamePhase.Rest || Sim.Phase == GamePhase.Result)
            {
                GUI.color = new Color(0, 0, 0, 0.62f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
                var big = new GUIStyle(GUI.skin.label) { fontSize = 26, richText = true, alignment = TextAnchor.MiddleCenter };
                var mid = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true, alignment = TextAnchor.MiddleCenter };
                var rect = new Rect(Screen.width * 0.5f - 320, Screen.height * 0.5f - 120, 640, 240);
                GUILayout.BeginArea(rect, GUI.skin.box);
                if (Sim.Phase == GamePhase.Rest)
                {
                    bool guardian = Sim.LastBossTier == BossTier.Guardian;
                    GUILayout.Label(guardian ? $"{Planet.DepthLabel(Sim.Depth)} — 수호자 격퇴" : Sim.LastBossTier == BossTier.Apex ? "중심부 보스 격파" : $"{Planet.DepthLabel(Sim.Depth)} — 변종 격파", big);
                    GUILayout.Label((guardian ? "더 깊은 곳에서 거대한 기척이 느껴진다 · " : "탈출 포트가 자동 요청되었다 · ") + $"체력 30% 회복 · 코어 {Sim.Loot.Core} 운반 중", mid);
                    GUILayout.Space(10);
                    if (Sim.Traits.HasOffer) GUILayout.Label("심층 보상 — 전설 카드를 하나 고르세요 (1 · 2 · 3)", mid);
                    else GUILayout.Label(guardian ? "<b>Enter</b> 다음 지층으로" : "<b>Enter</b> 이상지대로 (엔드게임)   ·   <b>Esc</b> 이 층에 남아 탈출", mid);
                }
                else
                {
                    GUILayout.Label(Sim.RunEscaped ? "탈출 성공" : "런 종료", big);
                    GUILayout.Label($"{Sim.RunEndReason} · 심층 {Sim.Depth}", mid);
                    GUILayout.Label($"도달 심층 {Sim.Depth}   처치 보스 {Sim.Run.BossesKilled}   파괴 블록 {Sim.World.BlocksBroken}   " +
                                    (Sim.RunEscaped ? $"확보 코어 {Sim.Loot.Core}" : $"<color=#ff8da8>소실 코어 {Sim.Loot.Core}</color>"), mid);
                    GUILayout.Space(10);
                    GUILayout.Label("<b>Enter</b> 같은 직업으로 다시", mid);
                }
                GUILayout.EndArea();
            }

            // 특성 카드 3택 — 원본 §9.6.4: 월드는 멈추지 않고 1·2·3 으로 고른다
            if (Sim.Traits.HasOffer)
            {
                var cards = Sim.Traits.Offer;
                float cw = 240, ch = 150, gap = 14;
                float total = cards.Length * cw + (cards.Length - 1) * gap;
                float x0 = Screen.width * 0.5f - total * 0.5f, y0 = Screen.height - ch - 36;
                var title = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true, fontStyle = FontStyle.Bold, wordWrap = true };
                var body = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true, wordWrap = true };
                string head = Sim.Traits.OfferIsLegend ? "심층 보상 · 전설" : $"LEVEL {Sim.Xp.Level} · {RoleName(Sim.Build.Role)} 특성";
                GUI.Label(new Rect(x0, y0 - 26, total, 22), $"<b>{head}</b>" + (Sim.Traits.OfferIsLegend ? "" : $"   <color=#aaa>Tab 다시 뽑기 ({Sim.Traits.Rerolls})</color>"), title);
                for (int i = 0; i < cards.Length; i++)
                {
                    var c = cards[i];
                    var rc = new Rect(x0 + i * (cw + gap), y0, cw, ch);
                    GUI.Box(rc, GUIContent.none);
                    GUILayout.BeginArea(new Rect(rc.x + 10, rc.y + 8, rc.width - 20, rc.height - 16));
                    string tierCol = c.Tier >= 4 ? "#ffd36e" : c.Tier == 3 ? "#c7a0ff" : c.Tier == 2 ? "#7febd0" : "#dddddd";
                    GUILayout.Label($"<color={tierCol}>[{i + 1}] T{c.Tier} · {c.Kind}</color>", body);
                    GUILayout.Label(c.Name, title);
                    GUILayout.Label(c.Desc, body);
                    int st = Sim.Traits.StackOf(c.Id);
                    if (st > 0) GUILayout.Label($"<color=#aaa>보유 ×{st}</color>", body);
                    GUILayout.EndArea();
                }
            }

            // 피격 비네트 — Feedback.HurtLevel
            if (_feedback != null && _feedback.HurtLevel > 0)
            {
                float a = 0.12f * _feedback.HurtLevel * _feedback.HurtProgress;
                GUI.color = new Color(1f, 0.15f, 0.1f, a);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
        }

        static string RoleName(RoleId r) => r switch { RoleId.Driller => "드릴러", RoleId.Gunner => "거너", RoleId.Scout => "스카우트", _ => "엔지니어" };
        static string QName(RoleId r) => r switch { RoleId.Driller => "돌파", RoleId.Gunner => "방어막", RoleId.Scout => "플레어", _ => "전력 노드" };
        static string EName(RoleId r) => r switch { RoleId.Gunner => "파쇄탄 기폭", RoleId.Scout => "그래플", RoleId.Engineer => "센트리", _ => "—" };
    }
}
