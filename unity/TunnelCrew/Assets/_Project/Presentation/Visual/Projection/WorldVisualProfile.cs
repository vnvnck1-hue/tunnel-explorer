using UnityEngine;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 기능명세서 §6.2 — 제작 기준 값을 코드 여러 곳에 흩어놓지 않는 단일 프로파일.
    ///
    /// 프로덕션 인증 대상은 <see cref="ProjectionPreset.ReferenceTopDown"/> 하나다. 기존
    /// 다이메트릭·아이소메트릭 프리셋은 개발 비교용으로 남기지만 신규 2.5D 아트가 그쪽에서
    /// 정상 형태를 유지한다고 보장하지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "Tunnel Crew/World Visual Profile", fileName = "WorldVisualProfile")]
    public sealed class WorldVisualProfile : ScriptableObject
    {
        [Header("투영 (§6.2)")]
        [Tooltip("제작 기준 투영. 프로덕션은 ReferenceTopDown 만 인증한다.")]
        public ProjectionPreset authoredProjection = ProjectionPreset.ReferenceTopDown;

        [Tooltip("X 투영 기저 — 시뮬레이션 1셀이 화면에서 차지하는 벡터.")]
        public Vector2 projectionBasisX = new Vector2(1f, 0f);
        [Tooltip("Y 투영 기저. ReferenceTopDown 은 (0,1) 로 회전·압축이 없다.")]
        public Vector2 projectionBasisY = new Vector2(0f, 1f);

        [Header("해상도 (§8.1)")]
        [Tooltip("Unity PPU. 신규 월드 아트 기준은 128 이다(기존 50px 타일이 아니다).")]
        public int pixelsPerUnit = 128;
        [Tooltip("소스 아트의 셀당 픽셀. 원화는 256 이상, 납품은 128.")]
        public int sourcePixelsPerCell = 128;

        [Header("카메라 월드 높이 (§3.3, §10)")]
        [Tooltip("기본 카메라에서 보이는 세로 셀 수.")]
        [Range(6f, 24f)] public float baseViewCells = 11.5f;
        [Tooltip("전투 중 근접 줌.")]
        [Range(6f, 24f)] public float combatViewCells = 10.5f;
        [Tooltip("크루가 벌어질 때 동적 줌 최대.")]
        [Range(6f, 30f)] public float coopViewCells = 17f;
        [Tooltip("플레이어 세로 앵커 — 화면 아래에서의 비율(§10.1 52~60%).")]
        [Range(0.4f, 0.75f)] public float verticalAnchorFromBottom = 0.56f;

        [Header("피사체 비율 (§3.3)")]
        [Tooltip("일반 캐릭터의 화면 높이 목표 범위. 검증용 기준이다.")]
        public Vector2 characterScreenHeightRange = new Vector2(0.13f, 0.18f);
        [Tooltip("캐릭터 시각 배율 — 아트 원본 크기에 곱한다.")]
        public float characterVisualScale = 1f;

        [Header("벽 높이 (§8.6)")]
        [Tooltip("벽 cap 을 화면 위로 올리는 기본 높이(셀). 맵 전체에서 하나여야 seam 이 없다.")]
        [Range(SurfaceRules.MinLiftCells, SurfaceRules.MaxLiftCells)]
        public float wallLiftCells = 1f;
        [Tooltip("지층별 정면 높이 범위. 키트 단위 선택폭이며 셀 단위 변화가 아니다.")]
        public Vector2 wallLiftRangeByStratum = new Vector2(0.75f, 1.5f);

        [Header("깊이 정렬 (§6.5)")]
        [Tooltip("셀 하나를 몇 단계로 쪼개 sortingOrder 를 만들지.")]
        [Range(1, 64)] public int depthUnitsPerCell = DepthSort.DefaultUnitsPerCell;

        [Header("전경 오클루전 (§6.6)")]
        [Tooltip("전경이 플레이어를 가렸을 때의 목표 알파(0.28~0.45).")]
        [Range(0f, 1f)] public float foregroundFadeAlpha = 0.34f;
        [Tooltip("페이드 아웃 시간(초). §6.6 0.18~0.28.")]
        [Range(0.05f, 1f)] public float foregroundFadeOut = 0.22f;
        [Tooltip("복원 시간(초). §6.6 0.25~0.4.")]
        [Range(0.05f, 1f)] public float foregroundFadeIn = 0.32f;
        [Tooltip("관심 캐릭터 footprint 를 얼마나 넓혀 겹침을 판정할지(셀).")]
        [Range(0f, 3f)] public float foregroundFadeRadius = 0.9f;
        [Tooltip("적은 완전 투명 처리하지 않는다 — 위협 실루엣의 최소 알파.")]
        [Range(0f, 1f)] public float enemySilhouetteMinAlpha = 0.55f;

        [Header("그림자 (§7.4)")]
        [Tooltip("접촉 AO 반지름(셀). 방향성 투사 그림자를 대신하는 검은 타원이 아니다.")]
        [Range(0.05f, 1f)] public float contactShadowRadius = 0.34f;
        [Range(0f, 1f)] public float contactShadowOpacity = 0.45f;

        [Header("환경광 (§7.2)")]
        public Color ambientColor = new Color(0.42f, 0.46f, 0.62f);
        [Range(0f, 2f)] public float ambientIntensity = 0.34f;
        [Tooltip("벽 상단·벽 정면·바닥·캐릭터의 서로 다른 최소광 계수.")]
        public Vector4 minLightBySurface = new Vector4(0.22f, 0.16f, 0.12f, 0.30f);

        [Header("광원 예산 (§13)")]
        [Tooltip("동시 활성 Light2D 상한 — 일반 / 스트레스.")]
        public Vector2Int activeLightBudget = new Vector2Int(16, 24);
        [Tooltip("그림자를 드리우는 Light2D 상한 — 일반 / 스트레스.")]
        public Vector2Int shadowLightBudget = new Vector2Int(4, 8);

        [Header("지층 프로파일")]
        [Tooltip("지층별 Volume 프로파일 이름. Resources 에서 찾는다.")]
        public string[] volumeProfileByStratum = { "Volume_Stratum1", "Volume_Stratum2", "Volume_Stratum3", "Volume_Anomaly" };

        [Header("대기 원근 (§7.5)")]
        [Tooltip("Ground / Entity / Foreground 깊이 그룹의 색·대비 분리 세기.")]
        public Vector3 atmosphereSeparation = new Vector3(0.18f, 0.08f, 0.26f);
        [Tooltip("저층 안개 농도.")]
        [Range(0f, 1f)] public float lowFogDensity = 0.35f;
        [Tooltip("먼지 농도.")]
        [Range(0f, 1f)] public float dustDensity = 0.4f;

        // ───────────────────────────── 파생 값

        /// <summary>표면 생성기에 넘길 규칙. 프로파일이 유일한 출처다(§12.1).</summary>
        public SurfaceRules BuildSurfaceRules(SurfaceRuleSet kit = null)
        {
            var r = kit != null ? kit.Rules : SurfaceRules.Default;
            r.WallLiftCells = wallLiftCells;
            return r;
        }

        /// <summary>프로덕션 인증 투영인가. 아니면 신규 아트의 형태를 보장하지 않는다.</summary>
        public bool IsAuthoredProjection => IsometricProjection.Preset == authoredProjection;

        /// <summary>지면 Y → sortingOrder. 프로파일의 깊이 단위를 쓴다.</summary>
        public int OrderFor(float groundY) => DepthSort.OrderFor(groundY, depthUnitsPerCell);

        // ───────────────────────────── 카메라 (§10)

        /// <summary>
        /// 이 모드에서 화면에 보일 세로 셀 수(§10.1·§10.2).
        ///
        /// 순수 함수로 둔 이유는 두 곳이 같은 값을 써야 하기 때문이다 — 본선
        /// <c>CameraRig</c> 와 Visual Lab 의 줌 비교. 한쪽만 바뀌면 "Lab 에서는 맞는데
        /// 게임에서는 다른" 상태가 된다.
        /// </summary>
        public float ViewCellsFor(CameraViewMode mode) => mode switch
        {
            CameraViewMode.Combat => combatViewCells,
            CameraViewMode.Coop => coopViewCells,
            _ => baseViewCells,
        };

        /// <summary>직교 카메라의 orthographicSize. 가시 높이의 절반이다.</summary>
        public float OrthographicSizeFor(CameraViewMode mode) => ViewCellsFor(mode) * 0.5f;

        /// <summary>
        /// 카메라 중심이 화면 세로에서 차지할 위치(아래에서의 비율). 플레이어를 화면
        /// 아래쪽에 두어 진행 방향(북쪽)을 더 보여 준다(§10.1 52~60%).
        /// </summary>
        public float AnchorFromBottom => Mathf.Clamp(verticalAnchorFromBottom, 0.1f, 0.9f);

        /// <summary>
        /// 화면 높이 대비 캐릭터 비율이 §3.3 목표 범위에 들어오는가.
        /// 버티컬 슬라이스 검증과 캡처 리포트가 쓴다.
        /// </summary>
        public bool CharacterRatioInRange(float characterHeightCells, CameraViewMode mode)
        {
            float ratio = characterHeightCells / Mathf.Max(0.01f, ViewCellsFor(mode));
            return ratio >= characterScreenHeightRange.x - 1e-4f
                && ratio <= characterScreenHeightRange.y + 1e-4f;
        }

        /// <summary>주어진 모드에서 이 캐릭터 높이가 화면의 몇 %를 차지하는가.</summary>
        public float CharacterScreenRatio(float characterHeightCells, CameraViewMode mode)
            => characterHeightCells / Mathf.Max(0.01f, ViewCellsFor(mode));

        void OnValidate()
        {
            if (pixelsPerUnit < 1) pixelsPerUnit = 1;
            if (depthUnitsPerCell < 1) depthUnitsPerCell = 1;
            // 뷰 높이 순서가 뒤집히면 동적 줌이 튄다.
            if (combatViewCells > baseViewCells) combatViewCells = baseViewCells;
            if (coopViewCells < baseViewCells) coopViewCells = baseViewCells;
        }
    }
}
