using TunnelCrew.Data;
using TunnelCrew.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 본편 캐릭터를 랩으로 그대로 가져온다 — 임시 스프라이트가 아니라 실제
    /// <see cref="PlayerView"/> · <see cref="PlayerState"/> · 손전등 · 후광이다.
    ///
    /// <b>왜 그대로 가져오는가</b> — 조명을 판단하려면 판단 대상이 진짜여야 한다. 임시 판때기는
    /// 8방향 시트도, 걸음 들썩임도, 발밑 그림자 캐스터도, 손전등 원뿔도 없다. 그 넷이 전부
    /// 화면의 빛을 바꾼다.
    ///
    /// <b>본편과 다른 것은 이동뿐이다.</b> 본편은 <c>TunnelSim</c> 이 <c>World.IsSolid</c> 로 충돌을
    /// 계산한다. 랩에는 Sim 이 없으므로 <see cref="LabWallCollision"/> 의 물리 콜라이더에 부딪히게
    /// 하고, 그 결과 위치를 <see cref="PlayerState.Position"/> 에 되써서 렌더·조명이 본편과 같은
    /// 경로를 타게 한다.
    ///
    /// 조작: WASD 이동 · 마우스 조준 · F 손전등.
    /// </summary>
    public sealed class LabPlayer : MonoBehaviour
    {
        [Tooltip("초당 셀. 본편 SimTuning 의 이동 속도 대역.")]
        [SerializeField, Range(1f, 12f)] float _speed = 4.2f;

        [SerializeField] bool _flashlightOn = true;

        [Tooltip("본편 RunBootstrap.BuildPlayer 와 같은 정렬 순서.")]
        [SerializeField] int _sortingOrder = 30;

        PlayerView _view;
        PlayerState _state;
        Rigidbody2D _body;
        Light2D _flashlight, _halo;
        Camera _cam;

        public PlayerState State => _state;
        public bool FlashlightOn { get => _flashlightOn; set => _flashlightOn = value; }
        public Vector2 CellPosition => new Vector2((float)_state.Position.X, (float)_state.Position.Y);

        /// <summary>빌더가 시작 위치(셀)를 넣는다.</summary>
        [SerializeField] Vector2 _startCell = new Vector2(8f, 5f);
        public void EditorAssign(Vector2 startCell) => _startCell = startCell;

        void Awake()
        {
            _state = new PlayerState
            {
                Position = new Vec2(_startCell.x, _startCell.y),
                Velocity = Vec2.Zero,
                Aim = 0.0,
            };

            BuildBody();
            BuildView();
            BuildLights();

            // 카메라는 <b>몸통</b>을 따라간다. 이 컴포넌트의 루트는 움직이지 않는다 —
            // 루트를 움직이면 자식인 물리 몸통의 월드 위치까지 끌려가 물리가 어긋난다.
            var follow = FindAnyObjectByType<LabCameraFollow>();
            if (follow != null)
            {
                follow.Target = _body.transform;
                follow.SnapToTarget();
            }
        }

        /// <summary>카메라·파괴 판정이 따라갈 실제 위치.</summary>
        public Transform FollowTarget => _body != null ? _body.transform : transform;

        /// <summary>
        /// 물리 몸통. 렌더와 분리한다 — <see cref="PlayerView.Render"/> 가 자기 트랜스폼을
        /// 상태에서 다시 쓰므로, 같은 오브젝트에 리지드바디를 붙이면 매 프레임 서로 덮어쓴다.
        /// </summary>
        void BuildBody()
        {
            var go = new GameObject("Player Body");
            go.transform.SetParent(transform, false);
            go.transform.position = IsometricProjection.ToRender3(_state.Position);

            _body = go.AddComponent<Rigidbody2D>();
            _body.bodyType = RigidbodyType2D.Dynamic;
            _body.gravityScale = 0f;
            _body.freezeRotation = true;
            // 관성으로 미끄러지지 않게 — 입력이 곧 속도다(본편 이동 감각에 맞춘다).
            _body.linearDamping = 0f;
            _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _body.interpolation = RigidbodyInterpolation2D.Interpolate;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = (float)SimTuning.PlayerRadius;
        }

        void BuildView()
        {
            var go = new GameObject("Player");
            go.transform.SetParent(transform, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = _sortingOrder;
            if (VisualLayers.Exists(VisualLayers.WorldEntity))
                sr.sortingLayerName = VisualLayers.WorldEntity;

            _view = go.AddComponent<PlayerView>();
            // 인스펙터 직렬화 없이 붙였으므로 렌더러를 직접 연결한다(본편과 같은 방식).
            var f = typeof(PlayerView).GetField("_renderer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            f?.SetValue(_view, sr);

            LoadRoleFrames(RoleId.Driller);
        }

        /// <summary>본편 RunBootstrap.LoadRoleFrames 와 같은 자산·같은 경로.</summary>
        void LoadRoleFrames(RoleId role)
        {
            var sheet = Resources.Load<CharacterSheetAsset>("Sheets_" + role.ToString().ToLowerInvariant());
            if (sheet == null)
            {
                Debug.LogWarning($"[랩] Sheets_{role.ToString().ToLowerInvariant()} 를 찾지 못했다 — 캐릭터가 보이지 않는다.");
                return;
            }
            foreach (var d in sheet.directions)
                if (d.walk != null && d.walk.Length > 0)
                    _view.SetWalkFrames(d.direction, d.walk);
        }

        /// <summary>
        /// 손전등과 후광 — 본편 <c>RunBootstrap.BuildLighting</c> 의 수치를 그대로 옮긴다.
        /// 노멀맵 품질·광원 높이·비추는 레이어까지 같은 함수(<see cref="LightSocketRenderer"/>)를 쓴다.
        /// </summary>
        void BuildLights()
        {
            var root = new GameObject("Player Lights");
            root.transform.SetParent(transform, false);

            var flashGo = new GameObject("Flashlight");
            flashGo.transform.SetParent(root.transform, false);
            _flashlight = flashGo.AddComponent<Light2D>();
            _flashlight.lightType = Light2D.LightType.Point;
            _flashlight.pointLightInnerAngle = 40f;
            _flashlight.pointLightOuterAngle = 56f;
            _flashlight.pointLightInnerRadius = 0.6f;
            _flashlight.pointLightOuterRadius = 9.36f;
            _flashlight.intensity = 2.6f;
            _flashlight.color = new Color(1f, 0.94f, 0.80f);
            // 본편 값은 0.9 / 0.35. 랩(개정 R2)에서는 벽 너머 암흑을 LOS 가 맡으므로 손전등 그림자는
            // 방향만 말하면 된다 — 진하고 딱딱하면 검정 띠가 바닥을 가른다(2026-09-10 지적). 분류 규칙과 맞춘다.
            _flashlight.shadowIntensity = LightClassRules.ShadowIntensity(LightClass.Scout);
            _flashlight.shadowSoftness = LightClassRules.ShadowSoftness(LightClass.Scout);
            UseNormalMaps(_flashlight);

            var haloGo = new GameObject("Player Halo");
            haloGo.transform.SetParent(root.transform, false);
            _halo = haloGo.AddComponent<Light2D>();
            _halo.lightType = Light2D.LightType.Point;
            _halo.pointLightInnerAngle = 360f;
            _halo.pointLightOuterAngle = 360f;
            _halo.pointLightInnerRadius = 0.2f;
            _halo.pointLightOuterRadius = 2.6f;
            _halo.intensity = 1.4f;
            _halo.color = new Color(0.95f, 0.88f, 0.78f);
            _halo.shadowIntensity = 0f;
            UseNormalMaps(_halo);
        }

        /// <summary>
        /// 본편 <c>RunBootstrap.UseNormalMaps</c> 와 같은 처리. URP 17 은 normalMapQuality·
        /// Distance 가 읽기 전용이라 직렬화 필드에 직접 쓴다.
        /// </summary>
        static System.Reflection.FieldInfo _fNmQuality, _fNmDistance;
        static bool _nmProbed;
        static void UseNormalMaps(Light2D l)
        {
            if (!_nmProbed)
            {
                _nmProbed = true;
                const System.Reflection.BindingFlags F =
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                _fNmQuality = typeof(Light2D).GetField("m_NormalMapQuality", F);
                _fNmDistance = typeof(Light2D).GetField("m_NormalMapDistance", F);
                if (_fNmQuality == null)
                    Debug.LogWarning("[랩] Light2D.m_NormalMapQuality 를 찾지 못했다 — 캐릭터 광원이 노멀맵을 읽지 않는다.");
            }
            _fNmQuality?.SetValue(l, Light2D.NormalMapQuality.Accurate);
            _fNmDistance?.SetValue(l, LightSocketRenderer.NormalMapHeightCells);

            // 캐릭터 광원은 <b>지면 높이</b>다 — 벽 윗면(WallTop)·전경 cap(FrontStructure)을 비추면
            // 벽에 높이가 없다는 뜻이 된다. 바닥·벽 정면·개체만 비춘다.
            // (LightSocketRenderer.ApplyLitLayers 는 윗면까지 포함하는 전체 목록이라 쓰지 않는다.)
            l.targetSortingLayers = VisualLayers.LitGroundLevelLayerIds();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.fKey.wasPressedThisFrame) _flashlightOn = !_flashlightOn;

            // 조준 — 본편은 Sim 이 마우스에서 Aim 을 만든다. 랩도 같은 뜻으로 화면 좌표를 쓴다.
            // 랩 카메라에는 MainCamera 태그가 없으므로 Camera.main 만 믿으면 조준이 죽는다.
            if (_cam == null) _cam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
            var mouse = Mouse.current;
            if (_cam != null && mouse != null)
            {
                var w = _cam.ScreenToWorldPoint(mouse.position.ReadValue());
                var cell = IsometricProjection.ToWorld(new Vector2(w.x, w.y));
                var d = new Vector2(cell.x - (float)_state.Position.X, cell.y - (float)_state.Position.Y);
                if (d.sqrMagnitude > 0.0004f) _state.Aim = Mathf.Atan2(d.y, d.x);
            }

            _view?.Render(_state, Time.deltaTime);
            UpdateLights();
        }

        void FixedUpdate()
        {
            var kb = Keyboard.current;
            var dir = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) dir.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) dir.y -= 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) dir.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir.x += 1f;
            }
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            _body.linearVelocity = dir * _speed;

            // 물리가 벽에 막고 미끄러뜨린 결과를 상태로 되쓴다 — 이후 렌더·조명은 본편과 같은 경로.
            var p = _body.position;
            var cell = IsometricProjection.ToWorld(p);
            _state.Position = new Vec2(cell.x, cell.y);
            _state.Velocity = new Vec2(_body.linearVelocity.x, _body.linearVelocity.y);
            if (dir.sqrMagnitude > 0.0001f) _state.LastMoveDir = new Vec2(dir.x, dir.y);
        }

        /// <summary>본편 <c>RunBootstrap.UpdateLighting</c> 의 플레이어 부분과 같다.</summary>
        void UpdateLights()
        {
            var pos = IsometricProjection.ToRender3(_state.Position);
            if (_halo != null) _halo.transform.position = pos;
            if (_flashlight == null) return;

            _flashlight.transform.position = pos;
            // Light2D 스팟은 위쪽(+Y)이 기준이라 90도를 뺀다.
            _flashlight.transform.rotation = Quaternion.Euler(
                0f, 0f, IsometricProjection.AngleToRender(_state.Aim) * Mathf.Rad2Deg - 90f);
            _flashlight.enabled = _flashlightOn;
        }
    }
}
