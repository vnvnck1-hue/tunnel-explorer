using UnityEngine;
using UnityEngine.Tilemaps;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 기능명세서 §6.6 — 캐릭터 앞에 그려지는 전경 구조물 하나. 남쪽 벽·천장 립·기둥이
    /// 여기에 해당한다.
    ///
    /// 자기 알파를 직접 바꾸지 않고 <see cref="ForegroundFadeController"/> 가 목표값을
    /// 준다. 그래야 여러 관심 캐릭터의 겹침을 한곳에서 합칠 수 있다.
    /// </summary>
    /// <remarks><c>ExecuteAlways</c> — 등록만 하는 컴포넌트다. 자세한 이유는
    /// <see cref="VisualHeightAnchor"/> 의 같은 주석을 볼 것.</remarks>
    [ExecuteAlways]
    public sealed class ForegroundOccluder : MonoBehaviour
    {
        [Tooltip("이 오클루더가 덮는 시뮬레이션 셀 영역. 관심 캐릭터 footprint 와의 겹침 판정에 쓴다.")]
        public Rect footprintCells = new Rect(0, 0, 1, 1);

        [Tooltip("같은 값끼리 함께 페이드한다. 하나의 벽 덩어리·문틀이 조각조각 사라지지 않게 한다.")]
        public int fadeGroup;

        [Tooltip("가려졌을 때의 목표 알파. 0 이하면 프로파일 값을 쓴다(§6.6 0.28~0.45).")]
        [Range(0f, 1f)] public float fadeTargetAlpha;

        [Tooltip("알파를 적용할 스프라이트들.")]
        public SpriteRenderer[] sprites;

        [Tooltip("알파를 적용할 타일맵들. TilemapRenderer 대신 Tilemap.color 를 쓴다 — 머티리얼 사본이 생기지 않는다.")]
        public Tilemap[] tilemaps;

        /// <summary>현재 알파. 컨트롤러가 보간한다.</summary>
        public float Alpha { get; private set; } = 1f;

        float _applied = -1f;

        void OnEnable()
        {
            ForegroundFadeController.Register(this);
            Alpha = 1f;
            _applied = -1f;
            ApplyAlpha(1f);
        }

        void OnDisable() => ForegroundFadeController.Unregister(this);

        [Tooltip("전경 cap 타일맵이 위로 올라간 높이(셀). EnvironmentChunkRenderer 의 벽 리프트와 같은 값이다.")]
        public float capLiftCells = 1f;

        bool[] _cellMask;
        int _maskCols, _maskRows, _maskOriginCol, _maskOriginRow;

        /// <summary>
        /// 실제로 전경 타일이 놓인 셀만 표시한 마스크를 건다.
        ///
        /// <b>사각형만으로는 안 된다.</b> 전경 타일맵은 청크 단위로 나뉘어 있어서 사각형은
        /// 청크 전체(16×16)가 된다. 그러면 방 한가운데 서 있어도 <see cref="Overlaps"/> 가
        /// 참이 되어 남쪽 벽이 <b>영구히</b> 페이드한다 — 실제로 그렇게 동작하고 있었다.
        ///
        /// 배열은 복사하지 않고 참조로 들고 있는다. 렌더러가 파괴·복구할 때 제자리에서
        /// 고치면 오클루더가 자동으로 최신 상태를 본다(§6.7 의 같은 dirty 단위).
        /// </summary>
        public void SetCellMask(bool[] mask, int originCol, int originRow, int cols, int rows)
        {
            _cellMask = mask;
            _maskOriginCol = originCol;
            _maskOriginRow = originRow;
            _maskCols = cols;
            _maskRows = rows;
        }

        /// <summary>셀 마스크가 걸려 있는가. 없으면 사각형 판정만 한다.</summary>
        public bool HasCellMask => _cellMask != null && _maskCols > 0 && _maskRows > 0;

        /// <summary>
        /// 관심 캐릭터의 원이 이 오클루더가 실제로 가리는 영역과 겹치는가.
        ///
        /// 전경 cap 타일맵은 벽 리프트만큼 <b>위(북쪽)로</b> 올라가 있다
        /// (<c>localPosition.y = +lift</c>). 그래서 셀 r 에 놓인 cap 은 화면에서
        /// <c>[r + lift, r + 1 + lift]</c> 를 덮고, 그 앞(북쪽) 바닥에 선 캐릭터를 가린다.
        /// 따라서 발점 y 의 캐릭터를 가리는 cap 셀은 y 보다 <b>남쪽</b>에 있다.
        /// </summary>
        public bool Overlaps(Vector2 ground, float radius)
        {
            float reach = Mathf.Max(0f, capLiftCells);

            // 빠른 반려 — 청크 사각형과도 안 겹치면 볼 것이 없다.
            float cx = Mathf.Clamp(ground.x, footprintCells.xMin, footprintCells.xMax);
            float cy = Mathf.Clamp(ground.y, footprintCells.yMin, footprintCells.yMax + reach);
            float dx = ground.x - cx, dy = ground.y - cy;
            if (dx * dx + dy * dy > radius * radius) return false;

            if (!HasCellMask) return true;

            int c0 = Mathf.FloorToInt(ground.x - radius) - _maskOriginCol;
            int c1 = Mathf.CeilToInt(ground.x + radius) - _maskOriginCol;
            // cap 이 위로 올라가 있으므로 후보 셀은 캐릭터보다 남쪽에서 찾는다.
            int r0 = Mathf.FloorToInt(ground.y - radius - reach) - _maskOriginRow;
            int r1 = Mathf.CeilToInt(ground.y + radius) - _maskOriginRow;

            if (c0 < 0) c0 = 0;
            if (r0 < 0) r0 = 0;
            if (c1 >= _maskCols) c1 = _maskCols - 1;
            if (r1 >= _maskRows) r1 = _maskRows - 1;

            // 후보 셀마다 <b>정확히</b> 판정한다. 행 범위만 보고 통과시키면 floor/ceil 여유
            // 때문에 한 칸이 딸려 들어와, 가릴 수 없는 위치의 cap 이 "가렸다" 로 잡힌다
            // (방 한가운데에서 기둥이 걸리는 현상으로 나타났다).
            float r2 = radius * radius;
            for (int r = r0; r <= r1; r++)
                for (int c = c0; c <= c1; c++)
                {
                    if (!_cellMask[r * _maskCols + c]) continue;

                    // 이 cap 셀이 실제로 덮는 화면 영역 — 리프트만큼 위로 올라간 한 칸.
                    float x0 = _maskOriginCol + c, x1 = x0 + 1f;
                    float y0 = _maskOriginRow + r + reach, y1 = y0 + 1f;

                    float qx = Mathf.Clamp(ground.x, x0, x1);
                    float qy = Mathf.Clamp(ground.y, y0, y1);
                    float ex = ground.x - qx, ey = ground.y - qy;
                    if (ex * ex + ey * ey <= r2) return true;
                }

            return false;
        }

        public void ApplyAlpha(float a)
        {
            Alpha = a;
            // 알파 0.004 미만 차이는 화면에서 구분되지 않는다 — 매 프레임 색을 다시 쓰지 않는다.
            if (Mathf.Abs(a - _applied) < 1f / 255f) return;
            _applied = a;

            if (sprites != null)
                for (int i = 0; i < sprites.Length; i++)
                {
                    var sr = sprites[i];
                    if (sr == null) continue;
                    var c = sr.color; c.a = a; sr.color = c;
                }

            if (tilemaps != null)
                for (int i = 0; i < tilemaps.Length; i++)
                {
                    var tm = tilemaps[i];
                    if (tm == null) continue;
                    var c = tm.color; c.a = a; tm.color = c;
                }
        }
    }
}
