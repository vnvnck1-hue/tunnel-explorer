using TunnelCrew.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 랩의 방을 <see cref="ArraySolidField"/> 로 정의하고 환경 렌더러·벽 윤곽 그림자·콜라이더·
    /// <b>타일 LOS 어둠</b>을 런타임에 묶는다.
    ///
    /// <b>개정 R2 (2026-09-10)</b> — 방이 "바닥 평면 + 벽 섬"에서 <b>"고체 채움 + 굴착 통로"</b> 로
    /// 뒤집혔다(기획서 §0.2·§5.4). 그와 함께 본편의 <see cref="LosService"/> ·
    /// <see cref="DarknessOverlay"/> 를 그대로 가져와 <b>벽 너머를 완전 암흑</b>으로 만든다.
    /// 랩 전용으로 새로 짠 것이 아니라 본편 코드다 — 수치(<c>SimTuning.Los*</c>)도 원본 HTML 승계값.
    ///
    /// <b>왜 런타임 Bind 인가</b> — <see cref="EnvironmentChunkRenderer.Bind"/> 는 타일맵 자식을 만든다.
    /// 에디터에서 바인드하면 타일맵이 씬에 직렬화되어 키트가 바뀔 때마다 씬을 다시 생성해야 한다.
    ///
    /// 조작:
    /// <c>마우스 왼쪽</c> 손 닿는 거리(1.6셀)의 벽을 판다 — 표면·그림자·콜라이더·LOS 가 한 번에 갱신된다.
    /// <c>마우스 오른쪽</c> 빈칸을 다시 벽으로.
    /// <c>X</c>/<c>C</c> 폰 북쪽 칸을 벽/빈칸으로(예전 조작, 유지).
    /// </summary>
    public sealed class LabEnvironment : MonoBehaviour
    {
        [Tooltip("첫 줄이 가장 위(row = Rows-1). '#' 고체, '.' 빈칸.")]
        [SerializeField] string[] _room = System.Array.Empty<string>();

        [SerializeField] EnvironmentChunkRenderer _renderer;
        [SerializeField] ShadowGeometryBuilder _shadows;
        [Tooltip("벽 덩어리가 바닥에 던지는 상시 드롭섀도우. 없으면 건너뛴다.")]
        [SerializeField] LabWallDropShadow _dropShadow;
        [Tooltip("벽 물리 콜라이더(랩 전용). 없으면 건너뛴다.")]
        [SerializeField] LabWallCollision _collision;

        [Header("타일 LOS 어둠 (개정 R2)")]
        [SerializeField] bool _losEnabled = true;
        [Tooltip("미탐색 영역의 색. 기획서 §7.6.5 — 지층 주조색(퍼플)이 사는 자리.")]
        [SerializeField] Color _darkColor = new Color(0.020f, 0.010f, 0.045f, 1f);
        [Tooltip("탐색했지만 지금은 안 보이는 영역의 색.")]
        [SerializeField] Color _memoryColor = new Color(0.16f, 0.09f, 0.30f, 1f);
        [Tooltip("손이 닿는 채굴 거리(셀). 몸통 중심에서 대상 셀 중심까지.")]
        [SerializeField] float _mineReach = 1.6f;

        ArraySolidField _field;
        LabPlayer _player;
        LabCrewLights _crew;
        Camera _cam;

        // ── 채굴 단계(3차 아트 ③). 벽은 3타에 부서지고 1·2타에 균열이 정면에 얹힌다 — 코어키퍼 wallCrackFront.
        // 본편은 WorldGrid.DamageStage(0~3)가 같은 역할을 한다; 랩은 Sim 이 없어 타격 수를 직접 센다.
        [Header("채굴 피드백")]
        [Tooltip("벽이 부서지기까지의 타격 수. 1 이면 즉시 파괴(균열 없음).")]
        [SerializeField, Range(1, 5)] int _hitsToBreak = 3;
        readonly System.Collections.Generic.Dictionary<int, int> _hits = new();
        UnityEngine.Tilemaps.Tilemap _crackMap;
        UnityEngine.Tilemaps.Tile[] _crackTiles;
        LosService _los;
        DarknessOverlay _darkness;
        int _edits;
        /// <summary>타일이 바뀐 횟수. LOS 캐시 무효화의 기준(WorldGrid.Version 역할).</summary>
        int _fieldVersion;

        public ArraySolidField Field => _field;
        public int Cols => _field?.Cols ?? 0;
        public int Rows => _field?.Rows ?? 0;
        public int Edits => _edits;
        public LosService Los => _los;
        public DarknessOverlay Darkness => _darkness;
        public int CasterCount => _shadows != null ? _shadows.transform.childCount > 0
            ? _shadows.transform.GetChild(0).childCount : 0 : 0;

        /// <summary>빌더가 참조를 직접 넣는다(프로젝트 관례: EditorAssign).</summary>
        public void EditorAssign(string[] room, EnvironmentChunkRenderer renderer, ShadowGeometryBuilder shadows,
                                 LabWallDropShadow dropShadow = null, LabWallCollision collision = null)
        {
            _room = room ?? System.Array.Empty<string>();
            _renderer = renderer;
            _shadows = shadows;
            _dropShadow = dropShadow;
            _collision = collision;
        }

        /// <summary>스위처가 V 로 켜고 끈다.</summary>
        public LabWallDropShadow DropShadow => _dropShadow;

        /// <summary>스위처가 N 으로 켜고 끈다(벽을 통과해 보고 싶을 때).</summary>
        public LabWallCollision Collision => _collision;

        /// <summary>LOS 어둠 토글(스위처 L). 끄면 R1 처럼 방 전체가 보인다 — 비교용.</summary>
        public bool LosEnabled
        {
            get => _losEnabled;
            set
            {
                _losEnabled = value;
                if (_darkness != null) _darkness.gameObject.SetActive(value);
            }
        }

        void Awake()
        {
            if (_room == null || _room.Length == 0 || _renderer == null)
            {
                Debug.LogWarning("[비주얼] LabEnvironment 에 방 또는 렌더러가 없다 — 환경 렌더러 경로가 꺼진다.");
                return;
            }

            _field = ArraySolidField.Parse(_room);
            _renderer.Bind(_field);

            // 벽 윤곽 캐스터 — 표면 생성기가 무엇을 벽으로 보는지 그대로 쓴다(§7.4-3).
            if (_shadows != null) _shadows.Bind(_renderer.IsWallCell, _renderer.Cols, _renderer.Rows);

            // 상시 드롭섀도우 — 벽 실루엣을 오프셋해 바닥에 얹는다.
            if (_dropShadow != null) _dropShadow.Resync(_field);

            // 물리 콜라이더 — 걸어서 벽에 막히는 감각. 랩 전용이다.
            if (_collision != null) _collision.Resync(_field);

            // 타일 LOS — 본편 LosService 를 손 격자에 물린다. 범위 밖은 고체(ISolidField 규약과 같다).
            _los = new LosService(_field.Cols, _field.Rows, _field.IsSolid, () => _fieldVersion);
        }

        void Start()
        {
            _player = FindAnyObjectByType<LabPlayer>();
            _cam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
            BuildDarkness();
        }

        /// <summary>
        /// 본편 <see cref="DarknessOverlay"/> 를 그대로 쓴다. 다른 점은 정렬 레이어 하나 —
        /// 랩은 12종 소팅 레이어를 쓰므로 <c>VisionAndGrade</c>("LOS 어둠, 안개, 깊이 색보정")로 올린다.
        /// <c>Default</c> 에 두면 가장 뒤라 바닥 타일이 어둠 위에 올라온다.
        /// </summary>
        void BuildDarkness()
        {
            if (_los == null || _cam == null) return;
            var go = new GameObject("Darkness (tile LOS)");
            go.transform.SetParent(transform, false);
            _darkness = go.AddComponent<DarknessOverlay>();
            // supersample 은 1 로 되돌렸다(2026-09-10 저녁). 4× 로 죄었더니 "암흑이 네모네모로 딱딱하고 시야가 너무 빨리 스왑돼
            // 정신없다"는 피드백 — 원본 HTML 은 셀 해상도 + 1.35칸 9탭 필터 + 경계 노이즈로 부드럽게 갔고(Darkness.shader),
            // 그 조합을 셰이더에 그대로 옮겼다. 경계 폭은 셰이더가 만들므로 텍스처는 셀 해상도면 충분하다.
            _darkness.Bind(_los, _field.Cols, _field.Rows, _cam, supersample: 1);
            _darkness.SetSorting(VisualLayers.Exists(VisualLayers.VisionAndGrade)
                ? VisualLayers.VisionAndGrade : "Default", 0);
            _darkness.SetColors(_darkColor, _memoryColor);
            go.SetActive(_losEnabled);

            // 프리셋 스위처는 Start 에서 이미 Apply 를 끝냈고 그때 어둠 쿼드가 없었다 — 어둠 축을 다시 밀어 준다.
            FindAnyObjectByType<LightingPresetSwitcher>()?.Reapply();
        }

        void Update()
        {
            if (_field == null) return;
            if (_player == null) _player = FindAnyObjectByType<LabPlayer>();

            // LOS 는 플레이어 셀이 바뀌었거나 타일이 바뀌었을 때만 실제로 다시 던진다(LosService 캐시).
            // 크루 손전등은 시야원으로 합산한다 — 동료가 비춘 곳을 팀이 함께 본다(원본 LOS · 코옵 감각).
            if (_los != null && _player != null)
            {
                var p = _player.CellPosition;
                if (_crew == null) _crew = FindAnyObjectByType<LabCrewLights>();
                _los.Compute(new Vec2(p.x, p.y), _crew != null ? _crew.VisionSources : null);
            }

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.xKey.wasPressedThisFrame) SetCellAhead(true);
                if (kb.cKey.wasPressedThisFrame) SetCellAhead(false);
            }

            // 채굴 — 마우스가 가리키는 셀이 손 닿는 거리면 판다. 파야 벽 구조가 맞게 잡혔는지
            // (정면·림·AO·콜라이더·그림자·LOS 가 함께 따라오는지) 확인할 수 있다.
            var mouse = Mouse.current;
            if (mouse != null && _player != null)
            {
                if (mouse.leftButton.wasPressedThisFrame) MineAtCursor(mouse, solid: false);
                if (mouse.rightButton.wasPressedThisFrame) MineAtCursor(mouse, solid: true);
            }
        }

        void MineAtCursor(Mouse mouse, bool solid)
        {
            if (_cam == null) _cam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
            if (_cam == null) return;

            var w = _cam.ScreenToWorldPoint(mouse.position.ReadValue());
            var cell = IsometricProjection.ToWorld(new Vector2(w.x, w.y));
            int c = Mathf.FloorToInt(cell.x), r = Mathf.FloorToInt(cell.y);
            if (c < 0 || r < 0 || c >= _field.Cols || r >= _field.Rows) return;

            // 손 닿는 거리 — 몸통 중심에서 대상 셀 중심까지.
            var me = _player.CellPosition;
            var target = new Vector2(c + 0.5f, r + 0.5f);
            if ((target - me).magnitude > _mineReach) return;

            // 자기 발밑을 벽으로 만들면 갇힌다. 플레이어 셀은 채우지 않는다.
            if (solid && Mathf.FloorToInt(me.x) == c && Mathf.FloorToInt(me.y) == r) return;

            // 테두리 1칸은 방의 경계 — 파면 카메라 밖 공극이 열린다. 막는다.
            if (c == 0 || r == 0 || c == _field.Cols - 1 || r == _field.Rows - 1) return;

            if (solid) { SetCell(c, r, true); return; }
            if (!_field.IsSolid(c, r)) return;

            // 채굴 — 타격을 세고, 단계마다 균열을 정면에 얹는다. 마지막 타격에 부서진다.
            int key = r * _field.Cols + c;
            _hits.TryGetValue(key, out int hits);
            hits++;
            if (hits >= _hitsToBreak)
            {
                _hits.Remove(key);
                SetCrack(c, r, 0);
                SetCell(c, r, false);
                return;
            }
            _hits[key] = hits;
            SetCrack(c, r, hits);
        }

        /// <summary>
        /// 균열 오버레이. 정면과 같은 레이어(BackStructure)에서 한 칸 위 order 로 그린다 — 정면 위에 얹히되
        /// 개체(WorldEntity) 아래다. 재질은 정면 타일맵의 것을 그대로 빌려 같은 빛을 받게 한다.
        /// </summary>
        void SetCrack(int c, int r, int stage)
        {
            var kit = _renderer != null ? _renderer.Kit : null;
            if (kit == null || kit.wallCrack == null || kit.wallCrack.Length == 0) return;
            if (_crackMap == null)
            {
                // 정면 타일맵은 렌더러의 직계 자식이 아닐 수 있다(그리드 아래) — 이름으로 전역 검색까지 간다.
                // 못 찾으면 Sprite-Lit-Default 로 떨어지는데 그 재질은 _MaskTex 기본이 white 라 수정광에 날아간다
                // (SurfaceMaterialSet 주석). 그래서 재질을 <b>반드시</b> 정면 것에서 빌린다.
                var host = _renderer.transform.Find("BackStructure");
                if (host == null) { var h = GameObject.Find("BackStructure"); host = h != null ? h.transform : null; }
                var go = new GameObject("Crack Map");
                go.transform.SetParent(host != null ? host.parent : _renderer.transform, false);
                if (go.GetComponentInParent<Grid>() == null) { var grid = go.AddComponent<Grid>(); grid.cellSize = new Vector3(1f, 1f, 0f); }
                _crackMap = go.AddComponent<UnityEngine.Tilemaps.Tilemap>();
                var tr = go.AddComponent<UnityEngine.Tilemaps.TilemapRenderer>();
                tr.mode = UnityEngine.Tilemaps.TilemapRenderer.Mode.Individual;
                var hostTr = host != null ? host.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>() : null;
                if (hostTr != null) { tr.sortingLayerName = hostTr.sortingLayerName; tr.sortingOrder = hostTr.sortingOrder + 1; tr.sharedMaterial = hostTr.sharedMaterial; }
                else if (VisualLayers.Exists(VisualLayers.BackStructure)) tr.sortingLayerName = VisualLayers.BackStructure;

                _crackTiles = new UnityEngine.Tilemaps.Tile[kit.wallCrack.Length];
                for (int i = 0; i < _crackTiles.Length; i++)
                {
                    var t = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
                    t.sprite = kit.wallCrack[i];
                    t.colliderType = UnityEngine.Tilemaps.Tile.ColliderType.None;
                    _crackTiles[i] = t;
                }
            }
            var pos = new Vector3Int(c, r, 0);
            if (stage <= 0) { _crackMap.SetTile(pos, null); return; }
            int idx = Mathf.Clamp(stage - 1, 0, _crackTiles.Length - 1);
            _crackMap.SetTile(pos, _crackTiles[idx]);
        }

        /// <summary>HUD 용 — 지금 균열이 남아 있는 벽 수.</summary>
        public int CrackedCells => _hits.Count;
        public int HitsToBreak => _hitsToBreak;

        /// <summary>폰 앞(북쪽) 칸을 바꾼다(예전 조작).</summary>
        void SetCellAhead(bool solid)
        {
            if (_player == null) return;
            var at = _player.CellPosition;
            int c = Mathf.FloorToInt(at.x);
            int r = Mathf.FloorToInt(at.y) + 1;
            if (c < 0 || r < 0 || c >= _field.Cols || r >= _field.Rows) return;
            SetCell(c, r, solid);
        }

        /// <summary>
        /// 셀 하나를 바꾸고 표면·그림자 윤곽·드롭섀도·콜라이더·LOS 를 <b>같은 프레임</b>에 갱신한다
        /// (§6.7-5, §15.2 "채굴 뒤 표면·오클루더·그림자·LOS 가 함께 갱신된다").
        /// </summary>
        public void SetCell(int c, int r, bool solid)
        {
            if (_field.IsSolid(c, r) == solid) return;

            // 셀이 바뀌면 그 칸의 타격 기록·균열은 무효다(메우기든 X/C 든).
            _hits.Remove(r * _field.Cols + c);
            SetCrack(c, r, 0);

            _field.SetSolid(c, r, solid);
            _fieldVersion++;
            _renderer.MarkCellDirty(c, r);
            _renderer.FlushDirty();
            _shadows?.MarkDirty();
            _shadows?.FlushPending();
            _dropShadow?.Resync(null);
            _collision?.Resync(null);
            _los?.MarkDirty();
            _edits++;
        }
    }
}
