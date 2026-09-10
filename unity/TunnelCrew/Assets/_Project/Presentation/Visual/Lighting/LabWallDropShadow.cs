using UnityEngine;
using UnityEngine.Tilemaps;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 벽 덩어리가 바닥에 드리우는 <b>고정 드롭섀도우</b>. 톱다운에서 높이를 파는 것은 이것이다.
    ///
    /// <b>왜 필요한가</b> — cap 과 정면을 다 그려도 벽이 바닥에 그림자를 안 던지면 둘이 같은
    /// 평면의 무늬 두 종류로 읽힌다(2026-09-10 랩 캡처에서 확인). 실루엣이 바닥 위로 어긋나
    /// 얹혀야 "이 덩어리는 바닥보다 위에 있다" 가 성립한다.
    ///
    /// <b>규칙 — 벽 높이와 무관하게 길이가 같다.</b> SLYNYRD 의 톱다운 타일 규칙이고,
    /// 사실성보다 읽히는 것을 택한 것이다. 그래서 <c>wallLiftCells</c> 를 보지 않는다.
    /// 방향은 지층 광원 방향에서 나온다 — 지층1 은 좌상단 35° 이므로 그림자는 우하단으로 진다
    /// (아트 계약 §5-1).
    ///
    /// <b>동적 그림자와 다르다.</b> <see cref="ShadowGeometryBuilder"/> 가 만드는 것은 탐색광·
    /// 작업등이 실시간으로 던지는 그림자다. 이쪽은 지층 키라이트가 만드는 <b>상시</b> 그림자라
    /// 언릿이고 흔들리지 않는다. Ori·Stardew 가 쓰는 "상시 접지 그림자 + 국지 동적 그림자" 조합이다.
    ///
    /// 구현은 벽 실루엣을 그대로 복제해 오프셋만 준다 — 타일 하나하나 계산하지 않으므로
    /// 모서리·복잡한 덩어리 모양이 공짜로 따라온다.
    /// </summary>
    public sealed class LabWallDropShadow : MonoBehaviour
    {
        [Tooltip("그림자가 지는 방향과 길이(셀). 지층1 광원이 좌상단 35° 라 우하단으로 진다.")]
        [SerializeField] Vector2 _offsetCells = new Vector2(0.50f, -0.46f);

        [Tooltip("그림자 진하기. 바닥이 이미 어두우므로 약하면 보이지 않는다 — 0.55 는 눈에 띄지 않았다(2026-09-10).")]
        [SerializeField, Range(0f, 1f)] float _opacity = 0.85f;

        [Tooltip("그림자 색. 완전한 검정은 지층 색을 죽인다 — 지층 주조색 계열의 아주 어두운 값.")]
        [SerializeField] Color _color = new Color(0.02f, 0.01f, 0.05f, 1f);

        [Header("상단 림 — 키라이트를 받는 벽 윗면 가장자리")]
        [Tooltip("북쪽이 열린 벽 셀의 cap 위쪽 가장자리를 밝힌다. " +
                 "<b>기본은 꺼짐</b> — 절차 생성한 균일한 띠라 화면에서 흰 막대로 읽혔다(2026-09-10). " +
                 "제대로 하려면 cap 아트에 그려진 림(요청서 2차 ⑤)이 필요하다. B 로 켜서 비교만 한다.")]
        [SerializeField] bool _rimEnabled;

        [Tooltip("림 색. 지층 키라이트 색 계열의 밝은 값.")]
        [SerializeField] Color _rimColor = new Color(0.82f, 0.76f, 0.95f, 1f);

        [SerializeField, Range(0f, 1f)] float _rimOpacity = 0.5f;

        [SerializeField] EnvironmentChunkRenderer _renderer;
        [SerializeField] bool _enabled = true;

        Tilemap _map, _rimMap;
        TilemapRenderer _tmr, _rimTmr;
        Tile _tile, _rimTile;
        ISolidField _field;
        int _cols, _rows;

        public bool Active
        {
            get => _enabled;
            set { _enabled = value; if (_tmr != null) _tmr.enabled = value; }
        }

        public bool RimActive
        {
            get => _rimEnabled;
            set { _rimEnabled = value; if (_rimTmr != null) _rimTmr.enabled = value; }
        }

        public int Cells { get; private set; }
        public int RimCells { get; private set; }

        /// <summary>빌더가 참조를 직접 넣는다(프로젝트 관례).</summary>
        public void EditorAssign(EnvironmentChunkRenderer renderer, Vector2 offsetCells, float opacity)
        {
            _renderer = renderer;
            _offsetCells = offsetCells;
            _opacity = opacity;
        }

        /// <summary>
        /// 벽 실루엣을 읽어 그림자 층을 다시 만든다. 파괴로 벽이 바뀌면
        /// <see cref="LabEnvironment"/> 가 다시 부른다.
        /// </summary>
        public void Resync(ISolidField field)
        {
            if (field != null) _field = field;
            if (_field == null || _renderer == null) return;

            EnsureMap();
            _cols = _field.Cols;
            _rows = _field.Rows;

            _map.ClearAllTiles();
            EnsureRoleTiles();
            int n = 0;
            for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
            {
                if (!_field.IsSolid(c, r)) continue;
                // 사방이 벽인 안쪽 칸은 그림자를 그려도 위의 cap 에 완전히 가린다. 건너뛴다.
                if (Buried(c, r)) continue;
                var pos = new Vector3Int(c, r, 0);
                if (_roleTiles == null) { _map.SetTile(pos, _tile); n++; continue; }

                // 아트 타일셋(3차 요청 ②). 그림자는 우하단(+0.5, −0.46)으로 지므로 페이드가 필요한 가장자리는
                // 남쪽·동쪽이다. 남쪽 열림 → edge(남쪽 페이드) · 동쪽 열림 → edge 를 90° 돌려 동쪽 페이드 ·
                // 둘 다 → corner_outer · 둘 다 막혔는데 남동 대각만 열림 → corner_inner · 그 밖 → center.
                bool s = !_field.IsSolid(c, r - 1), e = !_field.IsSolid(c + 1, r), se = !_field.IsSolid(c + 1, r - 1);
                Tile tile; Quaternion rot = Quaternion.identity;
                if (s && e) tile = _roleTiles[EnvironmentKit.ShadowCornerOuter];
                else if (s) tile = _roleTiles[EnvironmentKit.ShadowEdge];
                else if (e) { tile = _roleTiles[EnvironmentKit.ShadowEdge]; rot = Quaternion.Euler(0f, 0f, 90f); }
                else if (se) tile = _roleTiles[EnvironmentKit.ShadowCornerInner];
                else tile = _roleTiles[EnvironmentKit.ShadowCenter];
                _map.SetTile(pos, tile);
                _map.SetTransformMatrix(pos, Matrix4x4.Rotate(rot));
                n++;
            }
            Cells = n;

            _map.color = new Color(_color.r, _color.g, _color.b, Mathf.Clamp01(_opacity));
            _map.transform.localPosition = new Vector3(_offsetCells.x, _offsetCells.y, 0f);
            _tmr.enabled = _enabled;

            ResyncRim();
        }

        /// <summary>
        /// 북쪽이 열린 벽 셀의 cap 위쪽 가장자리를 밝힌다.
        ///
        /// cap 은 <c>wallLiftCells</c> 만큼 화면 위로 올라가 그려지므로 림도 같은 만큼 올린다 —
        /// 그러지 않으면 밝은 띠가 벽 아래 바닥에 뜬다.
        /// </summary>
        void ResyncRim()
        {
            if (_rimMap == null) return;

            float lift = _renderer != null && _renderer.Profile != null
                ? _renderer.Profile.wallLiftCells : 1f;

            _rimMap.ClearAllTiles();
            int n = 0;
            for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
            {
                if (!_field.IsSolid(c, r)) continue;
                // 북쪽이 열린 셀만 — 그 위쪽 가장자리가 하늘(=키라이트)을 본다.
                if (_field.IsSolid(c, r + 1)) continue;
                _rimMap.SetTile(new Vector3Int(c, r, 0), _rimTile);
                n++;
            }
            RimCells = n;

            _rimMap.color = new Color(_rimColor.r, _rimColor.g, _rimColor.b, Mathf.Clamp01(_rimOpacity));
            _rimMap.transform.localPosition = new Vector3(0f, lift, 0f);
            _rimTmr.enabled = _rimEnabled;
        }

        /// <summary>
        /// 키트에 그림자 타일셋 4장이 다 있으면 역할별 타일을 만든다. 없으면 null — 단색 셀로 되돌아간다.
        /// 타일 색은 흰색으로 두고 진하기·색은 타일맵 color 가 곱한다(아트는 순수 알파 마스크).
        /// </summary>
        void EnsureRoleTiles()
        {
            var kit = _renderer != null ? _renderer.Kit : null;
            if (kit == null || !kit.HasShadowSet) { _roleTiles = null; UsingArtTiles = false; return; }
            if (_roleTiles != null && _roleTilesFrom == kit) return;
            _roleTiles = new Tile[4];
            for (int i = 0; i < 4; i++)
            {
                var t = ScriptableObject.CreateInstance<Tile>();
                t.sprite = kit.wallShadow[i];
                t.colliderType = Tile.ColliderType.None;
                _roleTiles[i] = t;
            }
            _roleTilesFrom = kit;
            UsingArtTiles = true;
        }

        Tile[] _roleTiles;
        EnvironmentKit _roleTilesFrom;
        /// <summary>HUD 표시용 — 아트 타일셋으로 그리는 중인가.</summary>
        public bool UsingArtTiles { get; private set; }

        /// <summary>이웃 8칸이 전부 벽인가. 가장자리는 바깥을 벽으로 본다(ISolidField 규약).</summary>
        bool Buried(int c, int r)
        {
            for (int dr = -1; dr <= 1; dr++)
            for (int dc = -1; dc <= 1; dc++)
            {
                if (dc == 0 && dr == 0) continue;
                if (!_field.IsSolid(c + dc, r + dr)) return false;
            }
            return true;
        }

        void EnsureMap()
        {
            if (_map != null) return;

            var grid = GetComponent<Grid>();
            if (grid == null) grid = gameObject.AddComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f);

            var go = new GameObject("Shadow Map");
            go.transform.SetParent(transform, false);
            _map = go.AddComponent<Tilemap>();
            _tmr = go.AddComponent<TilemapRenderer>();
            _tmr.mode = TilemapRenderer.Mode.Chunk;

            // §6.4 접촉 AO 와 같은 층. AO 보다 아래에 깔아 두 어둠이 겹쳐도 AO 가 살아 있게 한다.
            if (VisualLayers.Exists(VisualLayers.GroundDecal))
                _tmr.sortingLayerName = VisualLayers.GroundDecal;
            _tmr.sortingOrder = -50;

            // 언릿 — 상시 그림자라 광원에 반응하면 안 된다. 채널 재질을 씌우면 최소광이
            // 검정을 들어 올려 그림자가 사라진다(GroundDecal 이 기본 재질을 쓰는 것과 같은 이유).
            var unlit = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (unlit != null) _tmr.sharedMaterial = new Material(unlit) { name = "WallDropShadow" };

            _tile = ScriptableObject.CreateInstance<Tile>();
            _tile.sprite = SolidCellSprite();
            _tile.colliderType = Tile.ColliderType.None;

            // ── 상단 림. cap 위에 얹혀야 하므로 WallTop 층의 더 큰 order 에 둔다.
            var rimGo = new GameObject("Rim Map");
            rimGo.transform.SetParent(transform, false);
            _rimMap = rimGo.AddComponent<Tilemap>();
            _rimTmr = rimGo.AddComponent<TilemapRenderer>();
            _rimTmr.mode = TilemapRenderer.Mode.Chunk;
            if (VisualLayers.Exists(VisualLayers.WallTop))
                _rimTmr.sortingLayerName = VisualLayers.WallTop;
            _rimTmr.sortingOrder = 50;
            if (unlit != null) _rimTmr.sharedMaterial = new Material(unlit) { name = "WallTopRim" };

            _rimTile = ScriptableObject.CreateInstance<Tile>();
            _rimTile.sprite = TopEdgeGradientSprite();
            _rimTile.colliderType = Tile.ColliderType.None;
        }

        /// <summary>
        /// 셀 위쪽에서 아래로 사라지는 그라디언트. 가장자리 띠만 밝히고 cap 의 무늬를 덮지 않는다.
        /// </summary>
        static Sprite s_topEdge;
        static Sprite TopEdgeGradientSprite()
        {
            if (s_topEdge != null) return s_topEdge;
            // 가로도 세로와 같아야 한다 — Sprite 는 축마다 다른 PPU 를 못 준다.
            // w=8 로 두면 8/64 = 0.125 유닛짜리 세로 실오라기가 되어 셀을 덮지 못한다.
            const int w = 64, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "LabWallTopRim",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                // y = h-1 이 셀의 위쪽. 위 25% 안에서만 밝고 그 아래는 0.
                float t = (h - 1 - y) / (h * 0.25f);
                float a = Mathf.Clamp01(1f - t);
                a *= a;                              // 가장자리에 몰아준다
                byte alpha = (byte)(a * 255f);
                for (int x = 0; x < w; x++) px[y * w + x] = new Color32(255, 255, 255, alpha);
            }
            tex.SetPixels32(px);
            tex.Apply();
            s_topEdge = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0f, 0f), h);
            s_topEdge.name = "LabWallTopRim";
            s_topEdge.hideFlags = HideFlags.HideAndDontSave;
            return s_topEdge;
        }

        /// <summary>1셀을 꽉 채우는 불투명 사각 스프라이트. 그림자는 실루엣이라 무늬가 없어야 한다.</summary>
        static Sprite s_cell;
        public static Sprite SolidCellSprite()
        {
            if (s_cell != null) return s_cell;
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false)
            {
                name = "LabWallDropShadowCell",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var px = new Color32[64];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply();
            // pixelsPerUnit = 변 길이 → 스프라이트가 정확히 1유닛(=1셀)이 된다.
            s_cell = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0f, 0f), 8f);
            s_cell.name = "LabWallDropShadowCell";
            s_cell.hideFlags = HideFlags.HideAndDontSave;
            return s_cell;
        }
    }
}
