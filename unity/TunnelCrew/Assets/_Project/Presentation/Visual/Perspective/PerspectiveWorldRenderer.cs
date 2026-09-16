using System.Collections.Generic;
using UnityEngine;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 원근 월드의 바닥·벽 기하(3d-perspective-production-plan §4 3단계).
    ///
    /// 2D 본선과 <b>같은 자산·같은 규칙</b>을 쓴다. <see cref="SurfaceTopologyBuilder"/> 가 고른
    /// 셀 표면(<see cref="CellSurface"/>)을 그대로 읽어 <see cref="EnvironmentKit"/> 의 바닥·cap·정면
    /// 스프라이트를 3D 면의 UV 로 옮기고, <see cref="SurfaceMaterialSet"/> 의 Normal·Emission·AO 를
    /// 같은 UV 로 물린다. 즉 지층 키트를 바꾸면 3D 월드도 같이 바뀐다.
    ///
    /// 청크 단위로 캐시하고, 채굴로 바뀐 셀이 속한 청크만 다시 만든다(§4 3단계 마지막 항).
    /// </summary>
    public sealed class PerspectiveWorldRenderer : MonoBehaviour
    {
        /// <summary>시야에 들어올 수 있는 청크 반경(청크 수). 원근이라 먼 쪽이 넓게 보인다.</summary>
        const int ChunkRadius = 2;
        /// <summary>한 프레임에 새로 만드는 청크 수 상한. 대량 파괴를 여러 프레임에 나눈다.</summary>
        const int ChunksPerFrame = 2;
        /// <summary>정면 오버레이(광맥)를 벽에서 띄우는 거리. z-fighting 방지.</summary>
        const float OverlayOffset = 0.004f;
        /// <summary>벽 정면 아트가 없을 때의 기본 높이(셀).</summary>
        const float FallbackHeight = 1f;

        ISolidField _field;
        SurfaceRules _rules;
        EnvironmentKit _kit;
        SurfaceMaterialSet _floorSet, _topSet, _frontSet;
        WorldVisualProfile _profile;
        CellSurface[] _surfaces;
        int _cols, _rows, _chunkCols, _chunkRows;
        int _layer;

        /// <summary>
        /// 이 셀의 남향 정면에 광맥 아트를 얹을 것인가. 2D 의 <see cref="WallOreOverlay"/> 와
        /// 같은 판정을 <c>RunBootstrap</c> 이 넣어 준다(Ore·Gem·Crys 이고 남쪽이 열린 셀).
        /// </summary>
        public System.Func<int, int, bool> IsOreFace;

        readonly Dictionary<int, Chunk> _chunks = new Dictionary<int, Chunk>();
        readonly HashSet<int> _dirty = new HashSet<int>();
        readonly List<int> _pending = new List<int>(16);
        readonly List<int> _evict = new List<int>(16);
        readonly Dictionary<MaterialKey, Material> _materials = new Dictionary<MaterialKey, Material>();
        readonly Dictionary<MaterialKey, Batch> _batches = new Dictionary<MaterialKey, Batch>();

        sealed class Chunk
        {
            public GameObject Go;
            public readonly List<Mesh> Meshes = new List<Mesh>(4);
        }

        enum SurfaceClass { Floor = 0, WallTop = 1, WallFront = 2, Overlay = 3 }

        readonly struct MaterialKey : System.IEquatable<MaterialKey>
        {
            public readonly SurfaceClass Class;
            public readonly Texture Texture;
            public MaterialKey(SurfaceClass c, Texture t) { Class = c; Texture = t; }
            public bool Equals(MaterialKey other) => Class == other.Class && Texture == other.Texture;
            public override bool Equals(object obj) => obj is MaterialKey k && Equals(k);
            public override int GetHashCode() => ((int)Class * 397) ^ (Texture != null ? Texture.GetHashCode() : 0);
        }

        sealed class Batch
        {
            public readonly List<Vector3> Vertices = new List<Vector3>(256);
            public readonly List<Vector3> Normals = new List<Vector3>(256);
            public readonly List<Vector4> Tangents = new List<Vector4>(256);
            public readonly List<Vector2> Uv = new List<Vector2>(256);
            public readonly List<Color32> Colors = new List<Color32>(256);
            public readonly List<int> Triangles = new List<int>(384);

            public void Clear()
            {
                Vertices.Clear(); Normals.Clear(); Tangents.Clear(); Uv.Clear(); Colors.Clear(); Triangles.Clear();
            }

            static readonly Color32 TintWhite = new Color32(255, 255, 255, 255);

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                Vector3 normal, Vector4 tangent, Rect uv)
            {
                int start = Vertices.Count;
                Vertices.Add(a); Vertices.Add(b); Vertices.Add(c); Vertices.Add(d);
                // 정점 색은 반드시 채운다 — 비워 두면 COLOR 시맨틱이 (0,0,0,0) 으로 들어와
                // 알파 컷아웃이 전부 잘린다(2026-09-16 실측: 화면 전체가 비었다).
                for (int i = 0; i < 4; i++) { Normals.Add(normal); Tangents.Add(tangent); Colors.Add(TintWhite); }
                Uv.Add(new Vector2(uv.xMin, uv.yMin));
                Uv.Add(new Vector2(uv.xMax, uv.yMin));
                Uv.Add(new Vector2(uv.xMax, uv.yMax));
                Uv.Add(new Vector2(uv.xMin, uv.yMax));
                Triangles.Add(start); Triangles.Add(start + 2); Triangles.Add(start + 1);
                Triangles.Add(start); Triangles.Add(start + 3); Triangles.Add(start + 2);
            }
        }

        // ───────────────────────────── 구성

        public void Bind(ISolidField field, in SurfaceRules rules, EnvironmentKit kit,
            SurfaceMaterialSet floorSet, SurfaceMaterialSet topSet, SurfaceMaterialSet frontSet,
            WorldVisualProfile profile, int layer)
        {
            ClearChunks();
            _field = field;
            _rules = rules;
            _kit = kit;
            _floorSet = floorSet;
            _topSet = topSet;
            _frontSet = frontSet;
            _profile = profile;
            _layer = layer;

            _cols = field.Cols;
            _rows = field.Rows;
            _chunkCols = SurfaceTopologyBuilder.ChunkCols(_cols);
            _chunkRows = SurfaceTopologyBuilder.ChunkRows(_rows);
            _surfaces = new CellSurface[_cols * _rows];
            SurfaceTopologyBuilder.Build(_field, _rules, _surfaces);
        }

        public bool Ready => _field != null && _surfaces != null && _kit != null;

        /// <summary>이 셀이 시야를 막는 높이. 빈칸이면 0 이다(오클루전 판정에 쓴다).</summary>
        public float BlockingHeight(int col, int row)
        {
            if (_surfaces == null || col < 0 || row < 0 || col >= _cols || row >= _rows) return 0f;
            var s = _surfaces[row * _cols + col];
            if ((s.Surfaces & (SurfaceMask.WallTop | SurfaceMask.Buried)) == 0) return 0f;
            return s.Height > 0.01f ? s.Height : FallbackHeight;
        }

        /// <summary>셀이 바뀌었다(채굴·파괴). 표면을 다시 계산하고 그 청크만 다시 만든다.</summary>
        public void MarkCellDirty(int col, int row)
        {
            if (_surfaces == null) return;
            // 표면 선택은 이웃을 읽으므로 한 칸 넓혀 다시 만든다.
            SurfaceTopologyBuilder.BuildRegion(_field, _rules, col - 1, row - 1, 3, 3, _surfaces);
            SurfaceTopologyBuilder.DirtyChunks(col, row, _cols, _rows, _dirty);
        }

        /// <summary>구도 중심(시뮬 좌표) 주변의 청크를 만들고 먼 것은 버린다.</summary>
        public void UpdateStreaming(Vector2 focus)
        {
            if (!Ready) return;

            int cc = Mathf.Clamp(Mathf.FloorToInt(focus.x) / SurfaceTopologyBuilder.ChunkSize, 0, _chunkCols - 1);
            int cr = Mathf.Clamp(Mathf.FloorToInt(focus.y) / SurfaceTopologyBuilder.ChunkSize, 0, _chunkRows - 1);

            _evict.Clear();
            foreach (var pair in _chunks)
            {
                int c = pair.Key % _chunkCols, r = pair.Key / _chunkCols;
                if (Mathf.Abs(c - cc) > ChunkRadius + 1 || Mathf.Abs(r - cr) > ChunkRadius + 1) _evict.Add(pair.Key);
            }
            foreach (int key in _evict) DestroyChunk(key);

            _pending.Clear();
            for (int r = cr - ChunkRadius; r <= cr + ChunkRadius; r++)
            {
                if (r < 0 || r >= _chunkRows) continue;
                for (int c = cc - ChunkRadius; c <= cc + ChunkRadius; c++)
                {
                    if (c < 0 || c >= _chunkCols) continue;
                    int key = r * _chunkCols + c;
                    if (_dirty.Contains(key)) { _pending.Add(key); continue; }
                    if (!_chunks.ContainsKey(key)) _pending.Add(key);
                }
            }

            int budget = ChunksPerFrame;
            for (int i = 0; i < _pending.Count && budget > 0; i++)
            {
                int key = _pending[i];
                _dirty.Remove(key);
                BuildChunk(key);
                budget--;
            }
        }

        /// <summary>대기 중인 청크를 전부 지금 만든다. 씬 진입·시네마틱 직전에 쓴다.</summary>
        public void FlushStreaming(Vector2 focus)
        {
            for (int i = 0; i < 32; i++)
            {
                int before = _chunks.Count;
                UpdateStreaming(focus);
                if (_chunks.Count == before && _dirty.Count == 0) break;
            }
        }

        // ───────────────────────────── 기하

        void BuildChunk(int key)
        {
            DestroyChunk(key);

            int chunkC = key % _chunkCols, chunkR = key / _chunkCols;
            int col0 = chunkC * SurfaceTopologyBuilder.ChunkSize;
            int row0 = chunkR * SurfaceTopologyBuilder.ChunkSize;
            int col1 = Mathf.Min(_cols - 1, col0 + SurfaceTopologyBuilder.ChunkSize - 1);
            int row1 = Mathf.Min(_rows - 1, row0 + SurfaceTopologyBuilder.ChunkSize - 1);

            foreach (var batch in _batches.Values) batch.Clear();

            for (int r = row0; r <= row1; r++)
                for (int c = col0; c <= col1; c++)
                    EmitCell(c, r);

            var chunk = new Chunk { Go = new GameObject($"Chunk {chunkC},{chunkR}") };
            chunk.Go.transform.SetParent(transform, false);
            chunk.Go.layer = _layer;

            foreach (var pair in _batches)
            {
                var batch = pair.Value;
                if (batch.Triangles.Count == 0) continue;

                var mesh = new Mesh { name = $"Chunk {chunkC},{chunkR} · {pair.Key.Class}" };
                if (batch.Vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(batch.Vertices);
                mesh.SetNormals(batch.Normals);
                mesh.SetTangents(batch.Tangents);
                mesh.SetUVs(0, batch.Uv);
                mesh.SetColors(batch.Colors);
                mesh.SetTriangles(batch.Triangles, 0);
                mesh.RecalculateBounds();
                chunk.Meshes.Add(mesh);

                var go = new GameObject(pair.Key.Class.ToString());
                go.transform.SetParent(chunk.Go.transform, false);
                go.layer = _layer;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = MaterialFor(pair.Key);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            _chunks[key] = chunk;
        }

        void EmitCell(int c, int r)
        {
            var s = _surfaces[r * _cols + c];
            if ((s.Surfaces & SurfaceMask.Buried) != 0) return;

            if (s.IsFloor)
            {
                var sprite = _kit.FloorFor(s);
                if (sprite != null)
                    BatchFor(SurfaceClass.Floor, sprite.texture).Quad(
                        new Vector3(c, 0f, r), new Vector3(c + 1f, 0f, r),
                        new Vector3(c + 1f, 0f, r + 1f), new Vector3(c, 0f, r + 1f),
                        Vector3.up, new Vector4(1f, 0f, 0f, -1f), UvRect(sprite));
                return;
            }

            if ((s.Surfaces & SurfaceMask.WallTop) == 0) return;

            float h = s.Height > 0.01f ? s.Height : FallbackHeight;

            var cap = _kit.CapFor(s);
            if (cap != null)
                BatchFor(SurfaceClass.WallTop, cap.texture).Quad(
                    new Vector3(c, h, r), new Vector3(c + 1f, h, r),
                    new Vector3(c + 1f, h, r + 1f), new Vector3(c, h, r + 1f),
                    Vector3.up, new Vector4(1f, 0f, 0f, -1f), UvRect(cap));

            var front = _kit.FrontFor(s);
            if (front == null) return;
            var frontUv = UvRect(front);
            var side = BatchFor(SurfaceClass.WallFront, front.texture);

            // 남쪽 — 2D 본선이 실제로 그리던 그 면이다.
            if ((s.Surfaces & SurfaceMask.FrontFace) != 0)
                side.Quad(
                    new Vector3(c, 0f, r), new Vector3(c + 1f, 0f, r),
                    new Vector3(c + 1f, h, r), new Vector3(c, h, r),
                    new Vector3(0f, 0f, -1f), new Vector4(1f, 0f, 0f, -1f), frontUv);

            // 북쪽 — 2D 에서는 cap 이 가려 보이지 않던 면. 3D 에서는 아래쪽 방에서 뒷벽으로 보인다.
            if (!_field.IsSolid(c, r + 1))
                side.Quad(
                    new Vector3(c + 1f, 0f, r + 1f), new Vector3(c, 0f, r + 1f),
                    new Vector3(c, h, r + 1f), new Vector3(c + 1f, h, r + 1f),
                    new Vector3(0f, 0f, 1f), new Vector4(-1f, 0f, 0f, -1f), frontUv);

            if ((s.Surfaces & SurfaceMask.WestSide) != 0)
            {
                var sprite = PickSide(_kit.westSide, s.FrontModule) ?? front;
                BatchFor(SurfaceClass.WallFront, sprite.texture).Quad(
                    new Vector3(c, 0f, r + 1f), new Vector3(c, 0f, r),
                    new Vector3(c, h, r), new Vector3(c, h, r + 1f),
                    new Vector3(-1f, 0f, 0f), new Vector4(0f, 0f, 1f, 1f), UvRect(sprite));
            }

            if ((s.Surfaces & SurfaceMask.EastSide) != 0)
            {
                var sprite = PickSide(_kit.eastSide, s.FrontModule) ?? front;
                BatchFor(SurfaceClass.WallFront, sprite.texture).Quad(
                    new Vector3(c + 1f, 0f, r), new Vector3(c + 1f, 0f, r + 1f),
                    new Vector3(c + 1f, h, r + 1f), new Vector3(c + 1f, h, r),
                    new Vector3(1f, 0f, 0f), new Vector4(0f, 0f, -1f, 1f), UvRect(sprite));
            }

            EmitOreOverlay(c, r, s, h);
        }

        /// <summary>채굴 대상 셀의 남향 정면에 얹는 광맥. 2D 의 <see cref="WallOreOverlay"/> 와 같은 아트다.</summary>
        void EmitOreOverlay(int c, int r, in CellSurface s, float h)
        {
            if ((s.Surfaces & SurfaceMask.FrontFace) == 0) return;
            if (_kit.oreFront == null || _kit.oreFront.Length == 0) return;
            if (IsOreFace == null || !IsOreFace(c, r)) return;

            int index = (int)(SurfaceTopologyBuilder.Hash(c, r, 0x0BE5) % (uint)_kit.oreFront.Length);
            var sprite = _kit.oreFront[index];
            if (sprite == null) return;

            float z = r - OverlayOffset;
            BatchFor(SurfaceClass.Overlay, sprite.texture).Quad(
                new Vector3(c, 0f, z), new Vector3(c + 1f, 0f, z),
                new Vector3(c + 1f, h, z), new Vector3(c, h, z),
                new Vector3(0f, 0f, -1f), new Vector4(1f, 0f, 0f, -1f), UvRect(sprite));
        }

        static Sprite PickSide(Sprite[] a, int module) => EnvironmentKit.Pick(a, module);

        Batch BatchFor(SurfaceClass surfaceClass, Texture texture)
        {
            var key = new MaterialKey(surfaceClass, texture);
            if (!_batches.TryGetValue(key, out var batch))
            {
                batch = new Batch();
                _batches.Add(key, batch);
            }
            return batch;
        }

        static Rect UvRect(Sprite sprite)
        {
            var rect = sprite.textureRect;
            float w = sprite.texture.width, h = sprite.texture.height;
            return new Rect(rect.xMin / w, rect.yMin / h, rect.width / w, rect.height / h);
        }

        // ───────────────────────────── 재질

        Material MaterialFor(MaterialKey key)
        {
            if (_materials.TryGetValue(key, out var material) && material != null) return material;

            var shader = Shader.Find("Tunnel Crew/PerspectiveWorld");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            material = new Material(shader) { name = $"Perspective {key.Class}" };

            var set = key.Class switch
            {
                SurfaceClass.Floor => _floorSet,
                SurfaceClass.WallTop => _topSet,
                _ => _frontSet,
            };
            // 2D 본선이 쓰던 채널·최저 조도를 그대로 가져온다. 셰이더에 없는 프로퍼티는 무시된다.
            set?.Apply(material, _profile);

            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", key.Texture);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", key.Texture);
            material.mainTexture = key.Texture;
            if (material.HasProperty("_UseTopAmbient"))
                material.SetFloat("_UseTopAmbient", key.Class == SurfaceClass.WallTop ? 1f : 0f);
            if (key.Class == SurfaceClass.Overlay && material.HasProperty("_Cutoff"))
                material.SetFloat("_Cutoff", 0.15f);

            _materials[key] = material;
            return material;
        }

        // ───────────────────────────── 정리

        void DestroyChunk(int key)
        {
            if (!_chunks.TryGetValue(key, out var chunk)) return;
            if (chunk.Go != null) Destroy(chunk.Go);
            foreach (var mesh in chunk.Meshes) if (mesh != null) Destroy(mesh);
            _chunks.Remove(key);
        }

        void ClearChunks()
        {
            foreach (var pair in _chunks)
            {
                if (pair.Value.Go != null) Destroy(pair.Value.Go);
                foreach (var mesh in pair.Value.Meshes) if (mesh != null) Destroy(mesh);
            }
            _chunks.Clear();
            _dirty.Clear();
        }

        void OnDestroy()
        {
            ClearChunks();
            foreach (var material in _materials.Values) if (material != null) Destroy(material);
            _materials.Clear();
        }
    }
}
