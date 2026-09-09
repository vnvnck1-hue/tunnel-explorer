using UnityEngine;
using UnityEngine.InputSystem;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 랩의 방을 <see cref="ArraySolidField"/> 로 정의하고 환경 렌더러·벽 윤곽 그림자를 런타임에 묶는다
    /// (이주 4단계 선행 배선, 2026-09-09).
    ///
    /// <b>왜 런타임 Bind 인가</b> — <see cref="EnvironmentChunkRenderer.Bind"/> 는 타일맵 자식을 만든다.
    /// 에디터에서 바인드하면 타일맵이 씬에 직렬화되어 키트가 바뀔 때마다 씬을 다시 생성해야 한다.
    /// VisualLab 처럼 Awake 에서 바인드하면 씬은 참조만 들고, 아트가 도착해 키트 배열이 채워지면
    /// **씬을 다시 만들지 않고도** 다음 플레이에서 벽이 나온다.
    ///
    /// <b>벽 아트가 없는 지금</b> — 테두리 <c>#</c> 셀은 필드에서 고체지만 <c>wallTop</c>/<c>wallFront</c> 가
    /// 비어 있어 타일이 그려지지 않는다. 대신 <see cref="ShadowGeometryBuilder"/> 가 그 윤곽으로
    /// <c>ShadowCaster2D</c> 를 만들므로 **벽 그림자 배선은 지금 검증할 수 있다.** 테두리는 카메라 밖이라
    /// 보이지 않는 벽의 그림자가 화면을 어지럽히지 않는다. X 로 폰 앞 칸을 파면 안쪽 고체가 생겨
    /// 파괴 dirty 갱신과 윤곽 재추적을 눈으로 볼 수 있다.
    ///
    /// 조작: <c>X</c> 폰 앞(북쪽) 칸을 고체로 · <c>C</c> 다시 빈칸으로.
    /// </summary>
    public sealed class LabEnvironment : MonoBehaviour
    {
        [Tooltip("첫 줄이 가장 위(row = Rows-1). '#' 고체, '.' 빈칸.")]
        [SerializeField] string[] _room = System.Array.Empty<string>();

        [SerializeField] EnvironmentChunkRenderer _renderer;
        [SerializeField] ShadowGeometryBuilder _shadows;

        ArraySolidField _field;
        VisualHeightAnchor _pawn;
        int _edits;

        public ArraySolidField Field => _field;
        public int Cols => _field?.Cols ?? 0;
        public int Rows => _field?.Rows ?? 0;
        public int Edits => _edits;
        public int CasterCount => _shadows != null ? _shadows.transform.childCount > 0
            ? _shadows.transform.GetChild(0).childCount : 0 : 0;

        /// <summary>빌더가 참조를 직접 넣는다(프로젝트 관례: EditorAssign).</summary>
        public void EditorAssign(string[] room, EnvironmentChunkRenderer renderer, ShadowGeometryBuilder shadows)
        {
            _room = room ?? System.Array.Empty<string>();
            _renderer = renderer;
            _shadows = shadows;
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
        }

        void Start()
        {
            var pawnMover = FindAnyObjectByType<LightingLabPawn>();
            if (pawnMover != null) pawnMover.TryGetComponent(out _pawn);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || _field == null) return;
            if (kb.xKey.wasPressedThisFrame) SetCellAhead(true);
            if (kb.cKey.wasPressedThisFrame) SetCellAhead(false);
        }

        /// <summary>
        /// 폰 앞(북쪽) 칸을 바꾼다. 표면과 그림자 윤곽이 같은 dirty 단위에서 갱신돼야 한다(§6.7-5) —
        /// VisualLabController.SetCell 과 같은 순서다.
        /// </summary>
        void SetCellAhead(bool solid)
        {
            if (_pawn == null) return;
            int c = Mathf.FloorToInt(_pawn.groundPosition.x);
            int r = Mathf.FloorToInt(_pawn.groundPosition.y) + 1;
            if (c < 0 || r < 0 || c >= _field.Cols || r >= _field.Rows) return;
            if (_field.IsSolid(c, r) == solid) return;

            _field.SetSolid(c, r, solid);
            _renderer.MarkCellDirty(c, r);
            _renderer.FlushDirty();
            _shadows?.MarkDirty();
            _shadows?.FlushPending();
            _edits++;
        }
    }
}
