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
    /// M1 그레이박스 진입점. 씬에 이 컴포넌트 하나만 두면 월드·플레이어·카메라를 만들어 돌린다.
    ///
    /// 씬을 손으로 꾸미지 않고 코드로 세우는 이유는, 이 단계의 목표가 "원본과 같은 규칙·감각인가"
    /// 확인이라 재현 가능해야 하기 때문이다. 실제 씬 구성은 M2 이후 조명·HUD 와 함께 잡는다.
    /// </summary>
    public sealed class RunBootstrap : MonoBehaviour
    {
        [Header("데이터")]
        [Tooltip("비워 두면 Resources 에서 찾는다.")]
        [SerializeField] TileSetAsset _tileSet;
        [SerializeField] CharacterSheetAsset _drillerSheets;
        [SerializeField] int _depth = 1;

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
        Light2D _globalLight, _flashlight, _playerHalo;
        readonly List<Light2D> _lamps = new List<Light2D>();
        DarknessOverlay _darkness;
        Volume _volume;
        WallShadowBuilder _wallShadows;

        void Start()
        {
            if (_tileSet == null) _tileSet = Resources.Load<TileSetAsset>("TileSet_purple");
            if (_drillerSheets == null) _drillerSheets = Resources.Load<CharacterSheetAsset>("Sheets_driller");

            if (_tileSet == null)
                Debug.LogError("[M1] TileSet 을 찾지 못했다. " +
                    "`Tunnel Crew/M1 · 아트 임포트 설정 + 타일셋 생성` 을 먼저 실행할 것.");

            Sim = new TunnelSim();
            Sim.EnterDepth(_depth, DungeonConfig.Runtime);

            BuildCamera();
            BuildWorld();
            BuildPlayer();
            BuildLighting();

            _rig.Bind(Sim.World, () => Sim.Player);
        }

        // ───────────────────────────── 씬 구성
        void BuildCamera()
        {
            var camGo = Camera.main != null ? Camera.main.gameObject : new GameObject("Main Camera");
            camGo.tag = "MainCamera";

            // UnityEngine.Object 에는 `??` 를 쓰면 안 된다. GetComponent 가 돌려주는 "가짜 null"
            // 은 C# 기준으로는 null 이 아니라서 `??` 가 우변으로 넘어가지 않는다.
            // 그러면 컴포넌트가 실제로 붙지 않은 채 진행돼 MissingComponentException 이 난다.
            if (!camGo.TryGetComponent(out _cam)) _cam = camGo.AddComponent<Camera>();

            _cam.orthographic = true;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.04f, 0.03f, 0.07f);
            if (!camGo.TryGetComponent<UniversalAdditionalCameraData>(out var camData))
                camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;   // Volume 오버라이드가 먹으려면 필요하다
            camData.antialiasing = AntialiasingMode.None;
            if (!camGo.TryGetComponent(out _rig)) _rig = camGo.AddComponent<CameraRig>();
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

            LoadDrillerFrames();

            _lootView = new GameObject("Loot").AddComponent<LootView>();
        }

        void BuildLighting()
        {
            var root = new GameObject("Lighting");

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
            _flashlight.pointLightInnerAngle = 40f;    // 원본 원뿔 반각 28° → 전체 56°
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
                    // 원본 DEMO.lampRadius 94px = 1.88셀. 빛은 그보다 넓게 퍼진다.
                    l.pointLightOuterRadius = 5.2f;
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

        /// <summary>
        /// 지층별 Volume 프로파일. 원본 LX 4레이어(contrast · zone · core)를 대신한다.
        /// 지층이 바뀌면 프로파일을 교체한다 (M4 에서 연결).
        /// </summary>
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
        }

        void LoadDrillerFrames()
        {
            if (_drillerSheets == null) return;
            foreach (var d in _drillerSheets.directions)
                if (d.walk != null && d.walk.Length > 0)
                    _playerView.SetWalkFrames(d.direction, d.walk);
        }

        // ───────────────────────────── 루프
        void Update()
        {
            if (Sim?.World == null) return;
            Sim.Advance(Time.deltaTime, ReadInput());
            _playerView.Render(Sim.Player, Time.deltaTime);
            _lootView.Render(Sim.Loot);
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
                if (kb.fKey.wasPressedThisFrame) _flashlightOn = !_flashlightOn;   // 원본 F 토글
            }

            if (mouse != null && _rig != null)
            {
                var w = _rig.ScreenToWorld(mouse.position.ReadValue());
                input.AimWorld = new Vec2(w.x, w.y);
                input.DrillHeld = mouse.leftButton.isPressed;
                input.FireHeld = mouse.rightButton.isPressed;
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

        // ───────────────────────────── 임시 HUD
        void OnGUI()
        {
            if (!_showHud || Sim?.World == null) return;
            var p = Sim.Player;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };

            GUILayout.BeginArea(new Rect(12, 12, 460, 260), GUI.skin.box);
            GUILayout.Label($"<b>심층 {Sim.Depth}</b>  ·  {Sim.World.Cols}×{Sim.World.Rows}  " +
                            $"·  {Application.targetFrameRate}  {(1f / Mathf.Max(0.0001f, Time.smoothDeltaTime)):F0} fps", style);
            GUILayout.Label($"위치 ({p.Position.X:F2}, {p.Position.Y:F2})   속도 {p.Velocity.Length:F2} 셀/s", style);
            GUILayout.Label($"부순 블록 {Sim.World.BlocksBroken}   PULP {Sim.Loot.Pulp}   BLOOM {Sim.Loot.Bloom}", style);
            GUILayout.Label($"드릴 예열 {p.DrillWarm:P0} (배율 {MiningSystem.WarmMul(p):F2})   " +
                            $"과열 {p.DrillHeat:P0}{(p.DrillHeatLock > 0 ? $"  잠금 {p.DrillHeatLock:F1}s" : "")}", style);
            GUILayout.Label($"대시 {(p.DashActive ? "진행" : p.DashCooldown > 0 ? $"쿨 {p.DashCooldown:F2}s" : "준비")}" +
                            $"   {(p.IsDigging ? "채굴 중" : "")}{(p.BounceActive ? "  암반 반동" : "")}", style);
            GUILayout.Label($"전리품 {Sim.Loot.Items.Count}개   출구 {(Sim.World.ExitOpen ? "열림" : "미개방")}", style);
            GUILayout.Space(6);
            GUILayout.Label($"손전등 {(_flashlightOn ? "켜짐" : "꺼짐")}   랜턴 {_lamps.Count}개   " +
                            $"보이는 칸 {VisibleCellCount()}", style);
            GUILayout.Label("WASD 이동 · 마우스 조준 · 좌클릭 드릴 · Space 대시 · F 손전등", style);
            GUILayout.EndArea();
        }
    }
}
