using System.Collections.Generic;
using System.Linq;
using TunnelCrew.Data;
using TunnelCrew.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
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

        [Header("디버그")]
        [SerializeField] bool _showHud = true;

        public TunnelSim Sim { get; private set; }

        CameraRig _rig;
        Camera _cam;
        WorldRenderer _worldRenderer;
        PlayerView _playerView;
        LootView _lootView;

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
            if (!camGo.TryGetComponent<UniversalAdditionalCameraData>(out _))
                camGo.AddComponent<UniversalAdditionalCameraData>();
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
            // M2 에서 제대로 만든다. 지금은 타일이 보이도록 전역광만 켠다.
            var go = new GameObject("Global Light 2D");
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
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
            GUILayout.Label("WASD 이동 · 마우스 조준 · 좌클릭 드릴 · Space 대시", style);
            GUILayout.EndArea();
        }
    }
}
