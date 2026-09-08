using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 기능명세서 §12.3 — 전용 검증 씬.
    ///
    /// 절차 생성과 시뮬레이션 없이 고정 방 하나만 세운다. 다층 렌더러·발 위치 정렬·전경
    /// 오클루전·그림자 윤곽·접촉 AO·재질 채널·카메라 프로파일이 실제로 그렇게 동작하는지
    /// 눈으로 확인하는 것이 목적이며, 여기서 통과한 구조만 절차 맵으로 확장한다
    /// (§19 "고정 방이 레퍼런스 수준에 도달하기 전에는 전 지층 타일 양산을 시작하지 않는다").
    ///
    /// 조작
    /// <list type="bullet">
    /// <item>이동 WASD / 방향키</item>
    /// <item>파괴 X · 복구 C — 표면·오클루더·그림자 윤곽이 함께 갱신되는지 본다(§6.7)</item>
    /// <item>F — 전경 페이드 켜기/끄기(§6.6)</item>
    /// <item>G — 접촉 AO 켜기/끄기(§7.4-1)</item>
    /// <item>L — 탐색광 켜기/끄기 · 위치는 마우스(§7.3)</item>
    /// <item>Z — 채널 단독 보기 순환(§12.3)</item>
    /// <item>V — 카메라 프로파일 순환: 기본 / 근접 / 협동(§10.2)</item>
    /// <item>B — 대기 원근 켜기/끄기 · N — 대기 층 단독 보기 순환(§7.5)</item>
    /// <item>H — 가려진 캐릭터 실루엣·림 켜기/끄기(§6.6)</item>
    /// <item>J — 품질 단계 순환: Low / Medium / High / Ultra(§13)</item>
    /// <item>K — 광과민 옵션 켜기/끄기 — 깜빡임·그레인 정지(§10.3)</item>
    /// <item>F1 / F2 — 디버그 오버레이(§12.4)</item>
    /// </list>
    /// </summary>
    public sealed class VisualLabController : MonoBehaviour
    {
        [SerializeField] WorldVisualProfile _profile;
        [SerializeField] SurfaceRuleSet _ruleSet;
        [SerializeField] EnvironmentKit _kit;

        [Header("재질 채널 (§7.1)")]
        [SerializeField] SurfaceMaterialSet _floorMaterials;
        [SerializeField] SurfaceMaterialSet _wallTopMaterials;
        [SerializeField] SurfaceMaterialSet _wallFrontMaterials;

        [Header("세트피스 (§8.7)")]
        [SerializeField] SetPieceCatalog _setPieces;

        [Header("대기 원근 (§7.5)")]
        [SerializeField] AtmosphereProfile _atmosphere;

        /// <summary>
        /// 세트피스 소켓 — §8.7 이 정한 자리에 맞춘다.
        /// 방 후면 중심에 아치, 측면에 기둥, 바닥에 조명 소품과 영웅 설비.
        /// 좌표는 발점(셀)이며 방 정의가 20×14 인 것을 전제로 한다.
        /// </summary>
        [SerializeField]
        SetPieceSocket[] _setPieceSockets = (SetPieceSocket[])DefaultSetPieceSockets.Clone();

        /// <summary>
        /// 코드가 아는 기본 배치. 씬에는 이 배열이 <b>직렬화된 사본</b>으로 들어가므로,
        /// 여기를 고쳐도 이미 저장된 씬은 옛 값을 계속 쓴다. 그래서 재동기화 경로
        /// (<see cref="EditorResetSetPieceSockets"/>)를 따로 둔다.
        /// </summary>
        public static readonly SetPieceSocket[] DefaultSetPieceSockets =
        {
            // 방 후면 중심 — 아치(footprint 5×1). 북쪽 벽 바로 앞.
            new SetPieceSocket { assetId = "TR01-ARC-001", groundCell = new Vector2(10f, 11f) },
            // 방 측면 — 기둥 둘
            new SetPieceSocket { assetId = "TR01-PIL-INTACT-A", groundCell = new Vector2(3.5f, 9f) },
            new SetPieceSocket { assetId = "TR01-PIL-BROKEN-A", groundCell = new Vector2(16.5f, 9f) },
            // 영웅 설비 — 보조 초점(footprint 3×2)
            new SetPieceSocket { assetId = "TR01-HERO-DRILL-A", groundCell = new Vector2(15f, 4f) },
            // 조명 소품 — 작업등·경고등·결정등
            new SetPieceSocket { assetId = "TR01-LGT-WORKLAMP-A", groundCell = new Vector2(6.5f, 4f) },
            new SetPieceSocket { assetId = "TR01-LGT-CRYSTAL-A", groundCell = new Vector2(12.5f, 7f) },
            new SetPieceSocket { assetId = "TR01-LGT-WARNING-A", groundCell = new Vector2(4.5f, 11.5f) },

            // ── 전경 오클루더 4종(§6.6·§8.6 "남쪽 벽은 전경 오클루더 자산을 추가로 제공한다").
            // 남쪽 벽 바로 앞 행 2 에 세운다. footprint 2×1 이고 1.94셀 솟으므로 행 2~4 를
            // 덮는다 — 캐릭터가 행 2~4 를 지나면 뒤로 들어가 페이드와 실루엣이 걸린다.
            new SetPieceSocket { assetId = "TR01-FGV-RAILING-A", groundCell = new Vector2(3f, 2f) },
            new SetPieceSocket { assetId = "TR01-FGV-ROCK-LIP-A", groundCell = new Vector2(7f, 2f) },
            new SetPieceSocket { assetId = "TR01-FGV-PIPE-FRAME-A", groundCell = new Vector2(11f, 2f) },
            new SetPieceSocket { assetId = "TR01-FGV-HANGING-CABLE-A", groundCell = new Vector2(15f, 2f) },

            // ── 레일·배관 6종(§8.4 "직선·곡선·분기·단절"). 바닥에 눕는 평면 자산이라
            // 피벗이 캔버스 중앙이고 GroundDetail 로 간다. 한 줄로 늘어놓아 모듈이
            // 서로 이어지는지 눈으로 본다.
            new SetPieceSocket { assetId = "TR01-LIN-RAIL-STRAIGHT-A", groundCell = new Vector2(13f, 6f) },
            new SetPieceSocket { assetId = "TR01-LIN-RAIL-CURVE-A", groundCell = new Vector2(14f, 6f) },
            new SetPieceSocket { assetId = "TR01-LIN-RAIL-JUNCTION-A", groundCell = new Vector2(15f, 6f) },
            new SetPieceSocket { assetId = "TR01-LIN-RAIL-BROKEN-A", groundCell = new Vector2(16f, 6f) },
            new SetPieceSocket { assetId = "TR01-LIN-PIPE-ELBOW-A", groundCell = new Vector2(17f, 6f) },
            new SetPieceSocket { assetId = "TR01-LIN-CABLE-JUNCTION-A", groundCell = new Vector2(18f, 6f) },

            // ── 장식 소품 10종(§8.4 "원거리에서 서로 다른 덩어리로 읽힘").
            // 기둥(열 9~12 행 8~9 · 열 5~7 행 5~6)과 기존 세트피스를 피해 흩어 놓는다.
            new SetPieceSocket { assetId = "TR01-DEC-CRATE-A", groundCell = new Vector2(2f, 4f) },
            new SetPieceSocket { assetId = "TR01-DEC-TOOLBOX-A", groundCell = new Vector2(2f, 7f) },
            new SetPieceSocket { assetId = "TR01-DEC-BARREL-A", groundCell = new Vector2(2f, 10f) },
            new SetPieceSocket { assetId = "TR01-DEC-ROCK-A", groundCell = new Vector2(7f, 11f) },
            new SetPieceSocket { assetId = "TR01-DEC-MINERAL-A", groundCell = new Vector2(13f, 11f) },
            new SetPieceSocket { assetId = "TR01-DEC-PAPERS-A", groundCell = new Vector2(17f, 11f) },
            new SetPieceSocket { assetId = "TR01-DEC-BUCKET-A", groundCell = new Vector2(18f, 4f) },
            new SetPieceSocket { assetId = "TR01-DEC-CABLE-SPOOL-A", groundCell = new Vector2(18f, 8f) },
            new SetPieceSocket { assetId = "TR01-DEC-BEAMS-A", groundCell = new Vector2(8f, 6f) },
            new SetPieceSocket { assetId = "TR01-DEC-HARDWARE-A", groundCell = new Vector2(14f, 7f) },
        };

        /// <summary>배치할 세트피스 하나.</summary>
        [System.Serializable]
        public struct SetPieceSocket
        {
            public string assetId;
            [Tooltip("발점(셀 좌표).")]
            public Vector2 groundCell;
        }

        [Tooltip("고정 방. 첫 줄이 화면 위(row = Rows-1)다. '#' 고체, 그 밖은 빈칸.")]
        [SerializeField]
        string[] _room =
        {
            "####################",
            "####################",
            "#..................#",
            "#..................#",
            "#........####......#",
            "#........####......#",
            "#..................#",
            "#....###...........#",
            "#....###...........#",
            "#..................#",
            "#..................#",
            "#..................#",
            "####################",
            "####################",
        };

        [SerializeField] float _moveSpeed = 5f;

        ArraySolidField _field;
        EnvironmentChunkRenderer _env;
        ShadowGeometryBuilder _shadows;
        ContactShadowRenderer _contact;
        VisualHeightAnchor _dummy;
        Camera _cam;
        ForegroundFadeController _fade;
        Light2D _globalLight;
        Light2D _torch;
        VisualDebugLines _lines;
        AtmosphereDirector _atmo;
        OccludedSilhouetteRenderer _silhouettes;
        LightSocketRenderer _lightSockets;
        VisualOptionsController _options;
        VisualHeightAnchor _enemy;
        SetPieceSpawner _setPieceSpawner;
        CameraViewMode _viewMode = CameraViewMode.Base;

        /// <summary>디버그 선에 무엇을 그릴지(§12.4). 캡처 도구가 장면마다 바꾼다.</summary>
        [System.Flags]
        public enum DebugLines
        {
            None = 0,
            ShadowContours = 1 << 0,
            Footpoints = 1 << 1,
            ForegroundGroups = 1 << 2,
        }

        DebugLines _debugLines = DebugLines.None;

        public EnvironmentChunkRenderer Environment => _env;
        public ShadowGeometryBuilder Shadows => _shadows;
        public ContactShadowRenderer ContactShadows => _contact;
        public ForegroundFadeController Fade => _fade;
        public ISolidField Field => _field;
        public VisualHeightAnchor Dummy => _dummy;
        public WorldVisualProfile Profile => _profile;
        public Light2D Torch => _torch;

        /// <summary>현재 연결된 환경 키트 이름. 캡처 파일명 태그를 정하는 데 쓴다.</summary>
        public string KitName => _kit != null ? _kit.name : "(없음)";

        /// <summary>임시 아트로 돌아가고 있는가. 승인 아트로 전환하면 false 다.</summary>
        public bool UsingPlaceholderArt =>
            _kit == null || _kit.name.IndexOf("Placeholder", System.StringComparison.OrdinalIgnoreCase) >= 0;
        public VisualDebugLines Lines => _lines;
        public SetPieceSpawner SetPieces => _setPieceSpawner;
        public AtmosphereDirector Atmosphere => _atmo;
        public OccludedSilhouetteRenderer Silhouettes => _silhouettes;
        public LightSocketRenderer LightSockets => _lightSockets;
        public VisualOptionsController Options => _options;
        public VisualHeightAnchor Enemy => _enemy;

        public DebugLines ActiveDebugLines
        {
            get => _debugLines;
            set { _debugLines = value; RefreshDebugLines(); }
        }

        public CameraViewMode ViewMode
        {
            get => _viewMode;
            set { _viewMode = value; ApplyCamera(); }
        }

        /// <summary>현재 카메라 프로파일의 가시 세로 셀 수(§10.1).</summary>
        public float ViewCells => _profile != null ? _profile.ViewCellsFor(_viewMode) : 11.5f;

        void Start() => Rebuild();

        public void Rebuild()
        {
            // 이전에 만든 것을 먼저 지운다.
            //
            // Rebuild 는 Start 에서 한 번만 불리는 것이 아니라 캡처 도구와 메뉴에서도
            // 불린다. 지우지 않으면 FootpointSorter·ContactShadowRenderer·ShadowGeometryBuilder
            // 와 더미 캐릭터가 겹쳐 쌓여 앵커 수가 2배, 3배로 늘고 캡처가 실제 상태를
            // 보여 주지 않는다(등록 목록이 정적이라 더 눈에 띈다).
            ClearChildren();

            // 제작 기준 투영으로 강제한다 — 신규 아트는 이 프리셋에서만 형태를 보장한다(§6.2).
            var authored = _profile != null ? _profile.authoredProjection : ProjectionPreset.ReferenceTopDown;
            IsometricProjection.SetPreset(authored);
            VisualChannelDebug.Reset();

            _field = ArraySolidField.Parse(_room);

            var envGo = new GameObject("Environment");
            envGo.transform.SetParent(transform, false);
            _env = envGo.AddComponent<EnvironmentChunkRenderer>();
#if UNITY_EDITOR
            _env.EditorAssign(_profile, _ruleSet, _kit,
                _floorMaterials, _wallTopMaterials, _wallFrontMaterials);
#endif
            _env.Bind(_field);

            BuildCamera();
            BuildLighting();
            BuildDepthSystems();
            BuildShadowSystems();
            BuildDummy();
            BuildEnemy();
            BuildSetPieces();
            BuildAtmosphere();
            BuildOptions();
            BuildDebugLines();
            ApplyCamera();
            RefreshDebugLines();
        }

        /// <summary>
        /// 자기가 만든 자식을 모두 지운다. 카메라는 <c>Camera.main</c> 이라 이 계층 밖에 있고
        /// 그대로 재사용한다.
        /// </summary>
        void BuildSetPieces()
        {
            var go = new GameObject("Set Pieces");
            go.transform.SetParent(transform, false);
            _setPieceSpawner = go.AddComponent<SetPieceSpawner>();
#if UNITY_EDITOR
            _setPieceSpawner.EditorAssign(_setPieces, _profile);
#else
            _setPieceSpawner.Catalog = _setPieces;
            _setPieceSpawner.Profile = _profile;
#endif

            if (_setPieces == null || _setPieceSockets == null) return;

            // 그림자 빌더에 등록해 세트피스도 빛을 가리게 한다(§7.4).
            for (int i = 0; i < _setPieceSockets.Length; i++)
                _setPieceSpawner.Spawn(_setPieceSockets[i].assetId,
                    _setPieceSockets[i].groundCell, _shadows);

            // 세트피스 윤곽이 들어왔으므로 캐스터를 다시 만든다.
            _shadows?.MarkDirty();
            _shadows?.FlushPending();
        }

        void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }

            _env = null;
            _shadows = null;
            _contact = null;
            _dummy = null;
            _fade = null;
            _lines = null;
            _atmo = null;
            _silhouettes = null;
            _lightSockets = null;
            _options = null;
            _enemy = null;
            _setPieceSpawner = null;
            _globalLight = null;
            _torch = null;
        }

        /// <summary>
        /// §7.5 대기 원근. 카메라 자식이 아니라 Lab 자식으로 둔다 — 그래야
        /// <see cref="ClearChildren"/> 이 같이 지워서 Rebuild 가 몇 번 불려도 쿼드가 쌓이지 않는다.
        /// 위치는 <c>AtmosphereDirector</c> 가 매 틱 카메라를 따라간다.
        /// </summary>
        void BuildAtmosphere()
        {
            var go = new GameObject("Atmosphere");
            go.transform.SetParent(transform, false);
            _atmo = go.AddComponent<AtmosphereDirector>();
            // 캡처는 항상 그레인이 고정된 상태로 찍힌다(§16.3).
            _atmo.FreezeGrain = !Application.isPlaying;
            _atmo.Bind(_cam, _atmosphere);
        }

        /// <summary>
        /// §13 품질 단계와 §10.3 접근성을 한곳에서 적용한다. 조명·대기 매니저가
        /// 이미 만들어진 뒤에 붙여야 연결이 채워진다.
        /// </summary>
        void BuildOptions()
        {
            var go = new GameObject("Visual Options");
            go.transform.SetParent(transform, false);
            _options = go.AddComponent<VisualOptionsController>();
            _options.Bind(_lightSockets, _atmo);
        }

        void BuildCamera()
        {
            var go = Camera.main != null ? Camera.main.gameObject : new GameObject("Main Camera");
            go.tag = "MainCamera";
            if (!go.TryGetComponent(out _cam)) _cam = go.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.03f, 0.025f, 0.05f);

            if (!go.TryGetComponent<UniversalAdditionalCameraData>(out var data))
                data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
        }

        /// <summary>
        /// §10 의 카메라 프로파일을 적용한다. 값은 <see cref="WorldVisualProfile"/> 한곳에서
        /// 나오고 본선 <c>CameraRig</c> 도 같은 함수를 쓴다 — 한쪽만 바뀌는 일을 막는다.
        /// </summary>
        void ApplyCamera()
        {
            if (_cam == null || _field == null) return;

            float cells = ViewCells;
            _cam.orthographicSize = cells * 0.5f;

            // 플레이어를 화면 아래쪽에 두어 진행 방향(북쪽)을 더 보여 준다(§10.1).
            float anchor = _profile != null ? _profile.AnchorFromBottom : 0.56f;
            var focus = _dummy != null
                ? _dummy.groundPosition
                : new Vector2(_field.Cols * 0.5f, _field.Rows * 0.42f);

            var p = IsometricProjection.ToRender(focus);
            float centerY = p.y + cells * (anchor - 0.5f);
            _cam.transform.position = new Vector3(p.x, centerY, -10f);
        }

        void BuildLighting()
        {
            var root = new GameObject("Lighting");
            root.transform.SetParent(transform, false);

            var go = new GameObject("Global Light 2D");
            go.transform.SetParent(root.transform, false);
            _globalLight = go.AddComponent<Light2D>();
            _globalLight.lightType = Light2D.LightType.Global;
            _globalLight.color = _profile != null ? _profile.ambientColor : new Color(0.42f, 0.46f, 0.62f);
            // Lab 은 구조 검증이 먼저다. §15.1 "광원이 꺼져도 알베도와 AO 만으로 주요 형태가
            // 읽힌다" 를 확인할 수 있도록 환경광을 넉넉히 준다. 본선 수치는 여기서 정하지 않는다.
            _globalLight.intensity = 0.85f;

            // 탐색광 — 노멀 반응과 그림자 윤곽을 보는 광원(§7.3 탐색광).
            var torchGo = new GameObject("Torch (Point)");
            torchGo.transform.SetParent(root.transform, false);
            _torch = torchGo.AddComponent<Light2D>();
            _torch.lightType = Light2D.LightType.Point;
            _torch.pointLightInnerAngle = 360f;
            _torch.pointLightOuterAngle = 360f;
            _torch.pointLightInnerRadius = 0.5f;
            _torch.pointLightOuterRadius = 8f;
            _torch.intensity = 2.2f;
            _torch.color = new Color(1f, 0.92f, 0.76f);
            _torch.shadowIntensity = 0.85f;
            _torch.shadowSoftness = 0.3f;
            torchGo.transform.position = new Vector3(_field.Cols * 0.5f, _field.Rows * 0.6f, 0f);

            // §7.3 분류 적용 + §13 그림자 예산 집행. 예산은 전역이므로 매니저가 든다.
            _lightSockets = root.AddComponent<LightSocketRenderer>();
            // 캡처는 항상 깜빡임이 고정된 상태로 찍는다(§16.3).
            _lightSockets.FreezeFlicker = !Application.isPlaying;

            // 탐색광도 같은 규칙을 받는다 — 예산 계산에서 빠지면 예산이 의미가 없다.
            var torchSocket = torchGo.AddComponent<LightSocket>();
            torchSocket.lightClass = LightClass.Scout;
            torchSocket.baseIntensity = _torch.intensity;
            torchSocket.rangeCells = _torch.pointLightOuterRadius;
        }

        void BuildDepthSystems()
        {
            var go = new GameObject("Depth");
            go.transform.SetParent(transform, false);
            var sorter = go.AddComponent<FootpointSorter>();
            sorter.Profile = _profile;
            _fade = go.AddComponent<ForegroundFadeController>();
            _fade.Profile = _profile;

            // 가려진 캐릭터 실루엣·림(§6.6). 앵커의 SortingGroup 밖이어야 WorldFX 로 갈 수 있다.
            _silhouettes = go.AddComponent<OccludedSilhouetteRenderer>();
            _silhouettes.Profile = _profile;
        }

        void BuildShadowSystems()
        {
            var go = new GameObject("Shadows");
            go.transform.SetParent(transform, false);

            // 벽 footprint 윤곽 캐스터(§7.4-3). 표면 생성기가 무엇을 벽으로 보는지 그대로 쓴다.
            _shadows = go.AddComponent<ShadowGeometryBuilder>();
            _shadows.Bind(_env.IsWallCell, _env.Cols, _env.Rows);

            // 접촉 AO(§7.4-1). GroundDecal 로 간다 — 개체의 SortingGroup 밖에 있어야
            // 자기 Sorting Layer 를 지킬 수 있다.
            _contact = go.AddComponent<ContactShadowRenderer>();
            _contact.Profile = _profile;
        }

        void BuildDummy()
        {
            var go = new GameObject("Dummy Character");
            go.transform.SetParent(transform, false);

            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(go.transform, false);
            var sr = bodyGo.AddComponent<SpriteRenderer>();
            // 1x1 이 아니라 해상도 있는 판이어야 실루엣 림(§6.6)이 텍셀 단위로 잡힌다.
            // 색과 모양은 이전과 같아서 기존 캡처와 비교가 계속 가능하다.
            sr.sprite = SolidSprite(new Color(0.95f, 0.72f, 0.35f), 64, 128);
            sr.sortingLayerName = VisualLayers.WorldEntity;
            // 캐릭터 기준 높이 1.2~1.5셀, 발점이 아래(테스트 방 아트 규격 §3)
            bodyGo.transform.localScale = new Vector3(0.62f, 1.35f, 1f);
            bodyGo.transform.localPosition = new Vector3(0f, 0.675f, 0f);

            _dummy = go.AddComponent<VisualHeightAnchor>();
            _dummy.groundPosition = new Vector2(_field.Cols * 0.5f, _field.Rows * 0.35f);
            _dummy.isLocalInterest = true;
            _dummy.sortingLayer = VisualLayers.WorldEntity;
            _dummy.footprintRadius = 0.45f;

            // 접촉 그림자 — 실루엣이 아니라 발 위치 중심의 타원 하나(§7.4-1).
            // 0 은 "프로파일 값을 쓴다" 는 뜻이다.
            var shadow = go.AddComponent<ContactShadow>();
            shadow.radius = 0f;
            shadow.opacity = 0f;

            // 가림 보정 — 관심 캐릭터는 벽이 이미 페이드했으므로 얇은 팀 색 림만 얹는다(§6.6).
            var sil = go.AddComponent<OccludedSilhouette>();
            sil.body = sr;
            sil.mode = SilhouetteMode.Interest;
            sil.color = new Color(0.42f, 0.82f, 1f);
            _dummy.wantsSilhouette = true;
        }

        /// <summary>
        /// 적 더미. §6.6 "적은 완전 투명 처리하지 않고 위협 실루엣만 보장한다" 를 보려면
        /// <b>관심 대상이 아닌</b> 개체가 전경 뒤에 하나 있어야 한다 — 그 앞의 벽은
        /// 페이드하지 않으므로 실루엣이 벽 위로 올라오는지가 유일한 확인 방법이다.
        /// </summary>
        void BuildEnemy()
        {
            var go = new GameObject("Enemy Dummy");
            go.transform.SetParent(transform, false);

            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(go.transform, false);
            var sr = bodyGo.AddComponent<SpriteRenderer>();
            sr.sprite = CapsuleSprite(new Color(0.72f, 0.30f, 0.32f), 48, 96);
            sr.sortingLayerName = VisualLayers.WorldEntity;
            bodyGo.transform.localScale = new Vector3(1.1f, 1.15f, 1f);
            bodyGo.transform.localPosition = new Vector3(0f, 0.575f, 0f);

            _enemy = go.AddComponent<VisualHeightAnchor>();
            _enemy.groundPosition = new Vector2(_field.Cols * 0.5f - 3.5f, 2.15f);
            _enemy.isLocalInterest = false;      // 적은 전경 페이드를 유발하지 않는다
            _enemy.wantsSilhouette = true;
            _enemy.sortingLayer = VisualLayers.WorldEntity;
            _enemy.footprintRadius = 0.4f;

            go.AddComponent<ContactShadow>();

            var sil = go.AddComponent<OccludedSilhouette>();
            sil.body = sr;
            sil.mode = SilhouetteMode.Threat;
            sil.color = new Color(1f, 0.34f, 0.30f);
        }

        void BuildDebugLines()
        {
            var go = new GameObject("Debug Lines");
            go.transform.SetParent(transform, false);
            _lines = go.AddComponent<VisualDebugLines>();
        }

        /// <summary>디버그 선을 다시 만든다. 상태가 바뀔 때만 부른다 — 매 프레임이 아니다.</summary>
        public void RefreshDebugLines()
        {
            if (_lines == null) return;

            _lines.Visible = _debugLines != DebugLines.None;
            _lines.Begin();

            if ((_debugLines & DebugLines.ShadowContours) != 0)
                _lines.DrawShadowContours(_shadows,
                    new Color(1f, 0.45f, 0.25f), new Color(0.35f, 0.9f, 1f));

            if ((_debugLines & DebugLines.Footpoints) != 0)
                _lines.DrawFootpoints(new Color(1f, 0.85f, 0.4f), new Color(0.5f, 1f, 0.6f));

            if ((_debugLines & DebugLines.ForegroundGroups) != 0)
                _lines.DrawForegroundGroups(new Color(1f, 0.55f, 0.35f), new Color(0.5f, 0.5f, 0.6f, 0.5f));

            _lines.End();
        }

        /// <summary>불투명한 판. 한 변이 1 유닛이 되도록 PPU 를 텍스처 높이에 맞춘다.</summary>
        static Sprite SolidSprite(Color c, int w = 1, int h = 1)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            var c32 = (Color32)c;
            for (int i = 0; i < px.Length; i++) px[i] = c32;
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), h);
        }

        /// <summary>
        /// 알파 모양이 있는 캡슐. 위협 실루엣(§6.6)이 실제로 형태를 보여 주는지 보려면
        /// 사각형이 아닌 실루엣이 하나는 있어야 한다. 승인 캐릭터 아트가 오면 대체된다.
        /// </summary>
        static Sprite CapsuleSprite(Color c, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var px = new Color32[w * h];
            float rx = (w - 1) * 0.5f;
            float head = h * 0.30f;              // 머리 반지름 중심 높이
            float headR = w * 0.42f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - rx) / Mathf.Max(1f, rx);
                    bool inside;
                    if (y > h - head)
                    {
                        float dy = (y - (h - head)) / Mathf.Max(1f, headR);
                        inside = dx * dx + dy * dy <= 1f;
                    }
                    else
                    {
                        // 아래로 갈수록 살짝 넓어지는 몸통
                        float t = 1f - (float)y / h;
                        inside = Mathf.Abs(dx) <= Mathf.Lerp(0.62f, 0.86f, t);
                    }
                    px[y * w + x] = inside ? (Color32)c : new Color32(0, 0, 0, 0);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), h);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || _dummy == null) return;

            var d = Vector2.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) d.y += 1;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) d.y -= 1;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) d.x -= 1;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) d.x += 1;
            if (d != Vector2.zero)
            {
                _dummy.groundPosition += d.normalized * (_moveSpeed * Time.deltaTime);
                ApplyCamera();
            }

            // 파괴·복구 — §6.7 의 dirty 갱신에서 시각 틈이나 옛 그림자가 남는지 본다.
            if (kb.xKey.wasPressedThisFrame) SetCell(false);
            if (kb.cKey.wasPressedThisFrame) SetCell(true);

            if (kb.fKey.wasPressedThisFrame && _fade != null) _fade.Enabled = !_fade.Enabled;
            if (kb.gKey.wasPressedThisFrame && _contact != null) _contact.Enabled = !_contact.Enabled;
            if (kb.lKey.wasPressedThisFrame && _torch != null) _torch.enabled = !_torch.enabled;
            if (kb.zKey.wasPressedThisFrame) VisualChannelDebug.NextView();
            if (kb.bKey.wasPressedThisFrame && _atmo != null) _atmo.Active = !_atmo.Active;
            if (kb.nKey.wasPressedThisFrame && _atmo != null) _atmo.CycleIsolate();
            if (kb.hKey.wasPressedThisFrame && _silhouettes != null)
                _silhouettes.Enabled = !_silhouettes.Enabled;
            if (kb.jKey.wasPressedThisFrame && _options != null)
                _options?.CycleTier();
            if (kb.kKey.wasPressedThisFrame && _options != null)
                _options.ReducePhotosensitivity = !_options.ReducePhotosensitivity;
            if (kb.vKey.wasPressedThisFrame)
                ViewMode = (CameraViewMode)(((int)_viewMode + 1) % 3);

            // 탐색광을 마우스로 옮긴다 — 노멀 반응과 그림자 방향을 흔들어 본다.
            var mouse = Mouse.current;
            if (mouse != null && _torch != null && _torch.enabled && _cam != null)
            {
                var w = _cam.ScreenToWorldPoint(mouse.position.ReadValue());
                _torch.transform.position = new Vector3(w.x, w.y, 0f);
            }
        }

        void SetCell(bool solid)
        {
            int c = Mathf.FloorToInt(_dummy.groundPosition.x);
            int r = Mathf.FloorToInt(_dummy.groundPosition.y) + 1;   // 앞(북쪽) 칸
            if (c < 0 || r < 0 || c >= _field.Cols || r >= _field.Rows) return;
            _field.SetSolid(c, r, solid);
            _env.MarkCellDirty(c, r);
            // 표면과 그림자 윤곽이 같은 dirty 단위에서 갱신돼야 한다(§6.7-5).
            _shadows?.MarkDirty();
            _shadows?.FlushPending();
            RefreshDebugLines();
        }

#if UNITY_EDITOR
        /// <summary>에디터 도구가 참조를 꽂는다. SerializedObject 우회 없이 직접 대입한다.</summary>
        public void EditorAssign(WorldVisualProfile profile, SurfaceRuleSet rules, EnvironmentKit kit,
            SurfaceMaterialSet floor = null, SurfaceMaterialSet wallTop = null,
            SurfaceMaterialSet wallFront = null, SetPieceCatalog setPieces = null,
            AtmosphereProfile atmosphere = null)
        {
            _profile = profile; _ruleSet = rules; _kit = kit;
            _floorMaterials = floor; _wallTopMaterials = wallTop; _wallFrontMaterials = wallFront;
            _setPieces = setPieces;
            _atmosphere = atmosphere;
        }

        /// <summary>
        /// 세트피스 배치를 코드 기본값으로 되돌린다.
        ///
        /// 씬이 옛 배열을 직렬화해 들고 있으면 코드의 새 배치가 무시된다 — 실제로
        /// 소품 20종을 추가했는데 씬에는 7종만 남아 있었다.
        /// </summary>
        public void EditorResetSetPieceSockets()
        {
            _setPieceSockets = (SetPieceSocket[])DefaultSetPieceSockets.Clone();
        }

        /// <summary>
        /// 캡처 도구가 한 프레임 분량의 갱신을 플레이 모드 없이 돌린다.
        /// 에디터에서는 LateUpdate 가 오지 않으므로 각 시스템을 순서대로 직접 부른다.
        /// </summary>
        public void EditorTick()
        {
            int units = _profile != null ? _profile.depthUnitsPerCell : DepthSort.DefaultUnitsPerCell;

            PokeLights();
            // 등록된 앵커 전부 — 더미만 적용하면 세트피스가 Default 레이어에 남는다.
            FootpointSorter.ApplyAll(units);
            _env?.FlushDirty();
            _shadows?.FlushPending();
            _contact?.Apply();
            _fade?.EditorTick();
            // 페이드 다음 — 같은 프레임의 오클루더 알파를 읽어야 한다.
            _silhouettes?.Apply(10f);
            // 시간 0 — 깜빡임을 멈춰 캡처를 결정적으로 유지한다(§16.3).
            _lightSockets?.Apply(0f);
            _options?.EditorTick();
            ApplyCamera();
            // 카메라를 옮긴 뒤에 쿼드를 맞춘다 — 순서가 뒤바뀌면 한 틱 늦게 따라온다.
            _atmo?.EditorTick();
            RefreshDebugLines();
        }

        /// <summary>
        /// Light2D 의 메시를 강제로 만든다.
        ///
        /// URP 17.3 의 <c>Light2D</c> 는 <c>LateUpdate</c> 에서 메시를 만든다(전역광은 예외).
        /// 에디터에서 <c>Camera.Render</c> 를 한 번 부르는 캡처 경로에서는 그 LateUpdate 가
        /// 오지 않아 광원이 메시 없이 남고, 결과적으로 조명이 화면에 전혀 기여하지 않는다.
        /// <c>UpdateMesh</c>·<c>UpdateBoundingSphere</c> 가 internal 이라 리플렉션으로 부른다 —
        /// <c>ShadowGeometryBuilder</c> 가 <c>ShadowCaster2D</c> 를 다루는 것과 같은 방식이다.
        ///
        /// 실패하면 경고 한 번만 남기고 넘어간다. 그때 캡처는 최저 조도(§7.2)만 보이는
        /// 화면이 되는데, 그것도 §15.1 "광원이 꺼져도 형태가 읽힌다" 를 확인하는 데는 쓸 수 있다.
        /// </summary>
        void PokeLights()
        {
            var lights = GetComponentsInChildren<Light2D>(includeInactive: false);
            for (int i = 0; i < lights.Length; i++) LightMeshPoker.Poke(lights[i]);
        }

        static class LightMeshPoker
        {
            static System.Reflection.MethodInfo _updateMesh, _updateSphere, _cacheValues;
            static bool _probed, _warned;

            public static void Poke(Light2D light)
            {
                if (light == null || !light.enabled) return;
                Probe();
                if (_updateMesh == null)
                {
                    if (!_warned)
                    {
                        _warned = true;
                        UnityEngine.Debug.LogWarning(
                            "[비주얼] Light2D.UpdateMesh 를 찾지 못했다 — 에디터 캡처는 최저 조도만 보인다.");
                    }
                    return;
                }

                try
                {
                    _cacheValues?.Invoke(light, null);
                    if (light.lightType != Light2D.LightType.Global)
                    {
                        _updateMesh.Invoke(light, new object[] { true });
                        _updateSphere?.Invoke(light, null);
                    }
                }
                catch (System.Exception e)
                {
                    if (!_warned) { _warned = true; UnityEngine.Debug.LogWarning($"[비주얼] Light2D 갱신 실패: {e.Message}"); }
                }
            }

            static void Probe()
            {
                if (_probed) return;
                _probed = true;
                const System.Reflection.BindingFlags F =
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var t = typeof(Light2D);
                _updateMesh = t.GetMethod("UpdateMesh", F);
                _updateSphere = t.GetMethod("UpdateBoundingSphere", F);
                _cacheValues = t.GetMethod("CacheValues", F);
            }
        }

        /// <summary>캡처 도구가 방을 갈아끼울 때 쓴다.</summary>
        public void EditorSetRoom(string[] room)
        {
            if (room != null && room.Length > 0) _room = room;
        }
#endif
    }
}
