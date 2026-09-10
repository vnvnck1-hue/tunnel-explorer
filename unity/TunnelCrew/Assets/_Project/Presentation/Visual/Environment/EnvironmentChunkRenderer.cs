using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 기능명세서 §6.3·§6.4·§6.7 — 다층 2.5D 환경 렌더러.
    ///
    /// 기존 <c>WorldRenderer</c> 는 Floor / Walls / CoreTop 세 Tilemap 을 같은 평면에
    /// 겹쳐 그렸다(§4.1). 여기서는 표면 종류마다 다른 Sorting Layer 와 다른 Y 오프셋을
    /// 준다. 그래서 벽이 실제로 솟아 보이고, 남쪽 벽이 캐릭터를 가린다.
    ///
    /// <b>왜 레이어마다 Tilemap 구성이 다른가</b> — 겹침 여부가 다르다.
    /// <list type="bullet">
    /// <item>cap 은 모두 같은 lift 만큼 올라가므로 서로 겹치지 않는다 → Chunk 모드로 배칭.</item>
    /// <item>벽 정면은 높이가 1셀을 넘으면 북쪽 정면과 겹친다 → Individual 모드 + TopLeft
    ///       정렬로 아래 행이 나중에(앞에) 그려지게 한다.</item>
    /// <item>전경 cap 은 청크마다 Tilemap 을 따로 둔다 → 페이드 그룹이 청크 단위가 된다.
    ///       그러면 하나의 벽 덩어리가 조각조각 사라지지 않는다(§6.6).</item>
    /// </list>
    ///
    /// 프로덕션 인증 투영은 <c>ReferenceTopDown</c> 하나다(§6.2). 다른 프리셋에서는
    /// lift 가 회전·압축과 맞지 않으므로 형태를 보장하지 않는다.
    /// </summary>
    public sealed class EnvironmentChunkRenderer : MonoBehaviour
    {
        [SerializeField] WorldVisualProfile _profile;
        [SerializeField] SurfaceRuleSet _ruleSet;
        [SerializeField] EnvironmentKit _kit;
        /// <summary>랩 드롭섀도가 같은 키트의 그림자 타일셋을 읽는다.</summary>
        public EnvironmentKit Kit => _kit;

        [Header("재질 채널 (§7.1)")]
        [Tooltip("바닥 레이어용 채널 묶음. 비면 URP 기본 스프라이트 머티리얼로 그린다.")]
        [SerializeField] SurfaceMaterialSet _floorMaterials;
        [Tooltip("벽 상단 cap 용 채널 묶음.")]
        [SerializeField] SurfaceMaterialSet _wallTopMaterials;
        [Tooltip("벽 정면·측면용 채널 묶음.")]
        [SerializeField] SurfaceMaterialSet _wallFrontMaterials;

        ISolidField _field;
        CellSurface[] _surfaces;
        SurfaceRules _rules;
        int _cols, _rows, _chunkCols, _chunkRows;

        Grid _grid;
        Tilemap _groundBase, _groundDetail, _groundDecal, _backStructure, _wallTop;
        Tilemap[] _frontStructure;              // 청크당 하나 — 페이드 그룹
        Tilemap _wallCorner;                    // cap 위 모서리 오버레이(localOrder 1)
        ForegroundOccluder[] _frontOccluders;
        // 청크마다 "전경 정면 타일이 실제로 놓인 셀" 마스크. 오클루더가 참조로 들고 있어서
        // 파괴·복구로 여기를 고치면 겹침 판정이 같은 프레임에 따라온다(§6.7).
        bool[][] _frontMask;

        readonly Dictionary<Sprite, Tile> _tiles = new Dictionary<Sprite, Tile>(128);
        readonly List<Material> _ownedMaterials = new List<Material>(4);
        readonly HashSet<int> _dirty = new HashSet<int>();
        readonly List<int> _dirtyOrder = new List<int>(32);

        /// <summary>한 프레임에 다시 만드는 청크 수 상한. 대량 파괴를 2~3프레임에 나눈다(§6.7).</summary>
        [SerializeField, Range(1, 64)] int _chunksPerFrame = 8;

        bool _warnedNoArt;

        public WorldVisualProfile Profile { get => _profile; set => _profile = value; }

        // ───────────────────────────── 구성

        /// <summary>
        /// 표면을 만들고 타일맵을 세운다. <paramref name="field"/> 는 절차 맵
        /// (<see cref="WorldGridSolidField"/>) 또는 Visual Lab 의 고정 방
        /// (<see cref="ArraySolidField"/>) 이다.
        /// </summary>
        public void Bind(ISolidField field)
        {
            _field = field;
            _cols = field.Cols;
            _rows = field.Rows;
            _chunkCols = SurfaceTopologyBuilder.ChunkCols(_cols);
            _chunkRows = SurfaceTopologyBuilder.ChunkRows(_rows);
            _rules = _profile != null
                ? _profile.BuildSurfaceRules(_ruleSet)
                : (_ruleSet != null ? _ruleSet.Rules : SurfaceRules.Default);

            _surfaces = new CellSurface[_cols * _rows];
            BuildLayers();
            SurfaceTopologyBuilder.Build(_field, _rules, _surfaces);
            SurfaceVersion++;
            RedrawAll();
        }

        /// <summary>한 셀이 바뀌었다. 표면·오클루더·그림자 윤곽이 같은 dirty 단위에서 갱신된다(§6.7).</summary>
        public void MarkCellDirty(int col, int row)
        {
            if (_surfaces == null) return;
            SurfaceTopologyBuilder.DirtyChunks(col, row, _cols, _rows, _dirty);
        }

        /// <summary>남은 dirty 청크를 전부 지금 처리한다. 씬 진입·시네마틱 전에 쓴다.</summary>
        public void FlushDirty()
        {
            while (_dirty.Count > 0) ProcessDirty(_dirty.Count);
        }

        void LateUpdate()
        {
            if (_dirty.Count > 0) ProcessDirty(_chunksPerFrame);
        }

        void ProcessDirty(int budget)
        {
            _dirtyOrder.Clear();
            foreach (int c in _dirty)
            {
                _dirtyOrder.Add(c);
                if (_dirtyOrder.Count >= budget) break;
            }

            for (int i = 0; i < _dirtyOrder.Count; i++)
            {
                int chunk = _dirtyOrder[i];
                _dirty.Remove(chunk);
                RebuildChunk(chunk);
            }
        }

        void RebuildChunk(int chunk)
        {
            int cc = chunk % _chunkCols, cr = chunk / _chunkCols;
            int c0 = cc * SurfaceTopologyBuilder.ChunkSize;
            int r0 = cr * SurfaceTopologyBuilder.ChunkSize;
            int w = Mathf.Min(SurfaceTopologyBuilder.ChunkSize, _cols - c0);
            int h = Mathf.Min(SurfaceTopologyBuilder.ChunkSize, _rows - r0);
            if (w <= 0 || h <= 0) return;

            SurfaceTopologyBuilder.BuildRegion(_field, _rules, c0, r0, w, h, _surfaces);
            SurfaceVersion++;
            for (int r = r0; r < r0 + h; r++)
                for (int c = c0; c < c0 + w; c++)
                    DrawCell(c, r);
        }

        // ───────────────────────────── 타일맵 구성

        void BuildLayers()
        {
            // 이전 구성을 지운다 — Visual Lab 이 방을 갈아끼울 때 두 번 쌓이지 않게.
            foreach (var m in _ownedMaterials) if (m != null) DestroyMaterial(m);
            _ownedMaterials.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }

            var gridGo = new GameObject("Environment Grid");
            gridGo.transform.SetParent(transform, false);
            _grid = gridGo.AddComponent<Grid>();
            _grid.cellSize = new Vector3(1, 1, 0);   // 1셀 = 1유닛

            float lift = LiftCells;

            // 채널 머티리얼은 표면 분류마다 하나만 만들어 모든 타일맵이 공유한다.
            var floorMat = TakeMaterial(_floorMaterials, "Floor");
            var capMat = TakeMaterial(_wallTopMaterials, "WallTop");
            var frontMat = TakeMaterial(_wallFrontMaterials, "WallFront");

            _groundBase = MakeMap(gridGo.transform, "GroundBase", VisualLayers.GroundBase, 0f,
                TilemapRenderer.Mode.Chunk, material: floorMat);
            _groundDetail = MakeMap(gridGo.transform, "GroundDetail", VisualLayers.GroundDetail, 0f,
                TilemapRenderer.Mode.Chunk, material: floorMat);
            // 접촉 AO 데칼은 알파 곱셈 음영이다. 채널 재질을 씌우면 최소광이 검정을 들어 올려
            // 음영이 사라지므로 기본 머티리얼로 둔다.
            _groundDecal = MakeMap(gridGo.transform, "GroundDecal", VisualLayers.GroundDecal, 0f,
                TilemapRenderer.Mode.Chunk);

            // 벽 정면 — 셀의 남쪽 경계선에 발점. 높이가 1셀을 넘으면 북쪽 정면과 겹치므로
            // Individual + TopLeft 로 위쪽 행을 먼저 그린다(= 아래 행이 앞).
            _backStructure = MakeMap(gridGo.transform, "BackStructure", VisualLayers.BackStructure, 0f,
                TilemapRenderer.Mode.Individual, TilemapRenderer.SortOrder.TopLeft, frontMat);
            _backStructure.tileAnchor = new Vector3(0.5f, 0f, 0f);

            // cap — 전부 같은 lift 라 서로 겹치지 않는다 → Chunk 모드로 한 배치.
            _wallTop = MakeMap(gridGo.transform, "WallTop", VisualLayers.WallTop, lift,
                TilemapRenderer.Mode.Chunk, material: capMat);

            // 모서리 오버레이 — manifest r19 의 TR01-REF-WOC/WIC-A 가 sortingLayerHint=WallTop,
            // localOrder=1 이다. cap 위에 얹는 조각이라 같은 층의 더 큰 order 에 둔다.
            _wallCorner = MakeMap(gridGo.transform, "WallCorner", VisualLayers.WallTop, lift,
                TilemapRenderer.Mode.Chunk, material: capMat);
            _wallCorner.GetComponent<TilemapRenderer>().sortingOrder = 1;

            // 전경 cap — 청크마다 Tilemap 을 따로 둬 페이드 그룹을 만든다.
            int chunks = _chunkCols * _chunkRows;
            _frontStructure = new Tilemap[chunks];
            _frontOccluders = new ForegroundOccluder[chunks];
            _frontMask = new bool[chunks][];
            for (int i = 0; i < chunks; i++)
            {
                var tm = MakeMap(gridGo.transform, $"FrontStructure {i}", VisualLayers.FrontStructure, lift,
                    TilemapRenderer.Mode.Chunk, material: capMat);
                _frontStructure[i] = tm;

                int cc = i % _chunkCols, cr = i / _chunkCols;
                var occ = tm.gameObject.AddComponent<ForegroundOccluder>();
                occ.fadeGroup = i;
                occ.tilemaps = new[] { tm };
                occ.footprintCells = new Rect(
                    cc * SurfaceTopologyBuilder.ChunkSize,
                    cr * SurfaceTopologyBuilder.ChunkSize,
                    SurfaceTopologyBuilder.ChunkSize,
                    SurfaceTopologyBuilder.ChunkSize);
                if (_profile != null) occ.fadeTargetAlpha = _profile.foregroundFadeAlpha;

                // 정면이 남쪽으로 늘어지는 높이 = 벽 정면 높이(§8.6). 사각형이 아니라
                // 실제 타일이 놓인 셀만 가린다.
                occ.capLiftCells = lift;
                int side = SurfaceTopologyBuilder.ChunkSize;
                _frontMask[i] = new bool[side * side];
                occ.SetCellMask(_frontMask[i], cc * side, cr * side, side, side);
                _frontOccluders[i] = occ;
            }
        }

        /// <summary>이 표면 분류의 머티리얼을 만든다. 채널 묶음이 없으면 null(기본 머티리얼).</summary>
        Material TakeMaterial(SurfaceMaterialSet set, string label)
        {
            if (set == null) return null;
            var m = set.CreateMaterial(_profile, $"{label}-{set.name}");
            if (m != null) _ownedMaterials.Add(m);
            return m;
        }

        Tilemap MakeMap(Transform parent, string name, string sortingLayer, float yOffset,
            TilemapRenderer.Mode mode,
            TilemapRenderer.SortOrder sortOrder = TilemapRenderer.SortOrder.TopLeft,
            Material material = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, yOffset, 0f);

            var tm = go.AddComponent<Tilemap>();
            var tr = go.AddComponent<TilemapRenderer>();
            tr.mode = mode;
            tr.sortOrder = sortOrder;
            if (VisualLayers.Exists(sortingLayer))
                tr.sortingLayerName = sortingLayer;
            else
                Debug.LogWarning($"[비주얼] Sorting Layer '{sortingLayer}' 가 없다 — " +
                    "메뉴 'Tunnel Crew/비주얼 · 소팅 레이어 생성 (§6.4)' 을 먼저 실행할 것.");
            tr.sortingOrder = 0;
            if (material != null) tr.sharedMaterial = material;
            return tm;
        }

        float LiftCells => _profile != null ? _profile.wallLiftCells : _rules.WallLiftCells;

        // ───────────────────────────── 그리기

        void RedrawAll()
        {
            _groundBase.ClearAllTiles();
            _groundDetail.ClearAllTiles();
            _groundDecal.ClearAllTiles();
            _backStructure.ClearAllTiles();
            _wallTop.ClearAllTiles();
            for (int i = 0; i < _frontStructure.Length; i++) _frontStructure[i].ClearAllTiles();
            if (_frontMask != null)
                for (int i = 0; i < _frontMask.Length; i++)
                    if (_frontMask[i] != null) System.Array.Clear(_frontMask[i], 0, _frontMask[i].Length);

            if (_kit == null || _kit.IsEmpty)
            {
                if (!_warnedNoArt)
                {
                    _warnedNoArt = true;
                    Debug.LogWarning("[비주얼] EnvironmentKit 아트가 비어 있다 — 표면 구조는 만들었지만 " +
                        "그릴 스프라이트가 없다. art-production 의 approved 패키지를 연결할 것.");
                }
                return;
            }

            for (int r = 0; r < _rows; r++)
                for (int c = 0; c < _cols; c++)
                    DrawCell(c, r);
        }

        void DrawCell(int c, int r)
        {
            if (_kit == null) return;

            var s = _surfaces[r * _cols + c];
            var pos = new Vector3Int(c, r, 0);

            // 바닥
            _groundBase.SetTile(pos, s.IsFloor ? TileFor(_kit.FloorFor(s)) : null);
            _groundDetail.SetTile(pos, null);
            // 접촉 AO — 배열 인덱스가 방향이다(N,E,S,W). 변형 해시로 고르면 남쪽 접점에
            // 북쪽 음영이 깔린다.
            _groundDecal.SetTile(pos,
                (s.Surfaces & SurfaceMask.ContactAo) != 0
                    ? TileFor(AoForDirection(s.AoDir))
                    : null);

            // 벽 정면 + 측면
            Tile front = null;
            if ((s.Surfaces & SurfaceMask.FrontFace) != 0)
                front = TileFor(EnvironmentKit.Pick(_kit.wallFront, s.FrontModule));
            // 서·동 측면은 그리지 않는다(개정 R2, 기획서 §8.6.2). 코어키퍼 실측에 해당 레이어가 없고
            // ReferenceTopDown(회전 0°)에서 측면이 보일 근거가 없다. 실제로 이 두 장이 조명을 받지 않는
            // 밝은 판으로 방에 붙은 벽 옆에 떠서 "벽 윗면이 통째로 보인다"고 읽혔다(2026-09-10 실측:
            // BackStructure 타일, 앰비언트 0 에서도 (240,204,241)). 마스크 비트는 남기고 소비만 끊는다.
            _backStructure.SetTile(pos, front);

            // cap — 북쪽이 열렸으면 전경 오클루더 타일맵으로, 아니면 WallTop 으로.
            bool hasCap = (s.Surfaces & SurfaceMask.WallTop) != 0;
            bool foreground = (s.Surfaces & SurfaceMask.ForegroundTop) != 0;
            var cap = hasCap ? TileFor(_kit.CapFor(s)) : null;

            // 모서리 — 위상은 계산되고 있었지만(SurfaceTopologyBuilder) 렌더 경로가 없어
            // 화면에 도달하지 못하고 있었다(2026-09-10). 전경 cap 위에는 얹지 않는다 —
            // 그쪽은 페이드 대상이라 오버레이가 같이 사라지지 않으면 조각이 남는다.
            _wallCorner.SetTile(pos, hasCap && !foreground ? CornerFor(s) : null);

            int chunk = SurfaceTopologyBuilder.ChunkIndex(c, r, _cols);
            _wallTop.SetTile(pos, foreground ? null : cap);
            if (chunk >= 0 && chunk < _frontStructure.Length)
            {
                bool placed = foreground && cap != null;
                _frontStructure[chunk].SetTile(pos, placed ? cap : null);

                // 겹침 마스크도 같은 자리에서 고친다 — 타일과 마스크가 갈라지면
                // 파괴 후에도 사라진 벽이 계속 캐릭터를 가린 것으로 판정된다.
                var mask = _frontMask != null && chunk < _frontMask.Length ? _frontMask[chunk] : null;
                if (mask != null)
                {
                    int side = SurfaceTopologyBuilder.ChunkSize;
                    int lc = c - (chunk % _chunkCols) * side;
                    int lr = r - (chunk / _chunkCols) * side;
                    if (lc >= 0 && lr >= 0 && lc < side && lr < side) mask[lr * side + lc] = placed;
                }
            }
        }

        Tile TileFor(Sprite sprite)
        {
            if (sprite == null) return null;
            if (_tiles.TryGetValue(sprite, out var t)) return t;
            t = ScriptableObject.CreateInstance<Tile>();
            t.sprite = sprite;
            t.colliderType = Tile.ColliderType.None;   // 충돌은 Sim 이 그리드로 직접 판정한다
            _tiles[sprite] = t;
            return t;
        }

        void OnDestroy()
        {
            foreach (var t in _tiles.Values) if (t != null) Destroy(t);
            _tiles.Clear();
            foreach (var m in _ownedMaterials) if (m != null) DestroyMaterial(m);
            _ownedMaterials.Clear();
        }

        void DestroyMaterial(Material m)
        {
            if (Application.isPlaying) Destroy(m); else DestroyImmediate(m);
        }

        // ───────────────────────────── 조회

        /// <summary>디버그 오버레이와 그림자 생성기가 읽는다.</summary>
        /// <summary>
        /// 이 셀의 모서리 조각. 볼록이 오목보다 먼저다 — 볼록 모서리가 덩어리의 실루엣을
        /// 만들고, 한 셀이 둘 다 가질 때 실루엣이 더 크게 읽힌다.
        ///
        /// <b>지금은 방향별 아트가 없다.</b> manifest r19 는 볼록·오목 각 1장이고 어느 방향을
        /// 위해 그렸는지 기록이 없다. 그래서 네 방향에 같은 장을 얹는다 — 광원 방향
        /// (지층1 좌상단 35°)과 어긋나는 모서리가 생긴다. 방향별 4장이 오면 여기서 고른다.
        /// </summary>
        Tile CornerFor(in CellSurface s)
        {
            if (s.Corners == CornerMask.None) return null;
            const CornerMask outer = CornerMask.OuterSW | CornerMask.OuterSE
                                   | CornerMask.OuterNW | CornerMask.OuterNE;
            if ((s.Corners & outer) != 0) return TileFor(EnvironmentKit.Pick(_kit.outerCorner, s.CornerModule));
            return TileFor(EnvironmentKit.Pick(_kit.innerCorner, s.CornerModule));
        }

        /// <summary>
        /// 방향별 접촉 AO. 그 방향 장이 없으면 첫 장(북쪽)으로 떨어진다 — 방향이 틀린 음영이라도
        /// 경계가 읽히는 편이 아무것도 없는 것보다 낫다.
        /// </summary>
        Sprite AoForDirection(byte dir)
        {
            var a = _kit.contactAo;
            if (a == null || a.Length == 0) return null;
            return dir < a.Length && a[dir] != null ? a[dir] : a[0];
        }

        public CellSurface SurfaceAt(int col, int row)
        {
            if (_surfaces == null || col < 0 || row < 0 || col >= _cols || row >= _rows)
                return default;
            return _surfaces[row * _cols + col];
        }

        public int PendingDirtyChunks => _dirty.Count;

        public int Cols => _cols;
        public int Rows => _rows;

        /// <summary>
        /// 그림자 윤곽 생성기에 넘길 벽 판정자(§7.4). 표면 배열에서 읽으므로 표면 생성기가
        /// 무엇을 벽으로 보는지와 그림자가 항상 일치한다.
        /// </summary>
        public bool IsWallCell(int col, int row)
        {
            if (_surfaces == null || col < 0 || row < 0 || col >= _cols || row >= _rows) return false;
            var s = _surfaces[row * _cols + col];
            return (s.Surfaces & (SurfaceMask.WallTop | SurfaceMask.Buried)) != 0;
        }

        /// <summary>표면 배열이 다시 만들어질 때마다 오르는 값. 그림자 쪽이 갱신 시점을 안다.</summary>
        public int SurfaceVersion { get; private set; }

        /// <summary>
        /// 참조를 직접 넣는다. 랩 빌더(에디터)와 본선 <c>RunBootstrap</c>(런타임, 이주 B 7단계) 둘 다 쓰므로
        /// 에디터 전용 가드를 뺐다(2026-09-10). <see cref="Bind"/> 전에 불러야 한다.
        /// </summary>
        public void Assign(WorldVisualProfile profile, SurfaceRuleSet rules, EnvironmentKit kit,
            SurfaceMaterialSet floor = null, SurfaceMaterialSet wallTop = null, SurfaceMaterialSet wallFront = null)
        {
            _profile = profile; _ruleSet = rules; _kit = kit;
            _floorMaterials = floor; _wallTopMaterials = wallTop; _wallFrontMaterials = wallFront;
        }

        /// <summary>기존 호출 이름 유지.</summary>
        public void EditorAssign(WorldVisualProfile profile, SurfaceRuleSet rules, EnvironmentKit kit,
            SurfaceMaterialSet floor = null, SurfaceMaterialSet wallTop = null, SurfaceMaterialSet wallFront = null)
            => Assign(profile, rules, kit, floor, wallTop, wallFront);

        /// <summary>정면(BackStructure) 타일맵 렌더러 — 균열 오버레이가 레이어·재질을 빌린다.</summary>
        public TilemapRenderer FrontFaceRenderer => _backStructure != null ? _backStructure.GetComponent<TilemapRenderer>() : null;
        /// <summary>벽 상단(WallTop) 타일맵 렌더러 — 보스 벽 틴트 오버레이가 레이어를 빌린다.</summary>
        public TilemapRenderer WallTopRenderer => _wallTop != null ? _wallTop.GetComponent<TilemapRenderer>() : null;
        /// <summary>타일맵들이 매달린 그리드.</summary>
        public Transform GridRoot => _grid != null ? _grid.transform : transform;
    }
}
