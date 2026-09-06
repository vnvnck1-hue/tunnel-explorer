using System;
using System.Collections.Generic;
using System.Reflection;
using TunnelCrew.Sim;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TunnelCrew.Presentation
{
    /// <summary>
    /// 벽에서 빛을 가리는 <see cref="ShadowCaster2D"/> 를 만든다.
    ///
    /// 왜 리플렉션인가: URP 17.3 의 <c>ShadowCaster2D</c> 는 형태를 넣는 API 가 전부 internal 이다.
    /// 콜라이더에서 형태를 얻는 자동 감지(<c>TryGetDefaultShadowShapeProviderSource</c>)는
    /// <c>#if UNITY_EDITOR</c> 안에 있어 런타임에서는 동작하지 않는다.
    /// 그래서 직렬화 필드 3개를 직접 채운다. 필드 이름이 바뀌면 <see cref="Available"/> 가
    /// false 가 되고 그림자만 조용히 빠진다. 게임은 계속 돌아간다.
    ///
    /// 형태는 **탐욕적 사각형 분할**로 만든다. 벽 셀을 가로로 이어 붙이고 세로로 합쳐
    /// 사각형 목록을 얻는다. 윤곽선 추적보다 단순하고, 사각형 네 변이 그림자를 만들기에 충분하다.
    /// </summary>
    public sealed class WallShadowBuilder : MonoBehaviour
    {
        /// <summary>청크 한 변의 셀 수. 벽이 부서지면 이 단위로만 다시 만든다.</summary>
        public const int ChunkSize = 16;
        /// <summary>한 프레임에 다시 만들 청크 수. 대량 파괴에서 끊기지 않게 나눠 처리한다.</summary>
        const int RebuildsPerFrame = 2;

        // ───────────────────────────── 리플렉션
        static FieldInfo _fShapePath, _fShapePathHash, _fCastingSource, _fForceRebuild;
        static Type _castingSourceType;
        static bool _probed;

        /// <summary>리플렉션이 성립하는가. false 면 그림자를 만들지 않는다.</summary>
        public static bool Available
        {
            get
            {
                Probe();
                return _fShapePath != null && _fCastingSource != null;
            }
        }

        static void Probe()
        {
            if (_probed) return;
            _probed = true;
            const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
            var t = typeof(ShadowCaster2D);
            _fShapePath = t.GetField("m_ShapePath", F);
            _fShapePathHash = t.GetField("m_ShapePathHash", F);
            _fCastingSource = t.GetField("m_ShadowCastingSource", F);
            _fForceRebuild = t.GetField("m_ForceShadowMeshRebuild", F);
            _castingSourceType = _fCastingSource?.FieldType;

            if (_fShapePath == null || _fCastingSource == null)
                Debug.LogWarning("[M2] ShadowCaster2D 의 내부 필드를 찾지 못했다. " +
                                 "URP 버전이 바뀐 것으로 보인다. 벽 그림자를 건너뛴다.");
        }

        static void ApplyShape(ShadowCaster2D caster, Vector3[] path)
        {
            _fShapePath.SetValue(caster, path);
            _fShapePathHash?.SetValue(caster, path.GetHashCode() ^ Environment.TickCount);
            // ShadowCastingSources.ShapeEditor == 1
            _fCastingSource.SetValue(caster, Enum.ToObject(_castingSourceType, 1));
            _fForceRebuild?.SetValue(caster, true);
        }

        // ───────────────────────────── 상태
        WorldGrid _world;
        Transform _root;
        int _chunksX, _chunksY;
        List<GameObject>[] _chunks;
        readonly Queue<int> _dirty = new Queue<int>();
        readonly HashSet<int> _dirtySet = new HashSet<int>();

        public int CasterCount { get; private set; }

        public void Bind(WorldGrid world)
        {
            _world = world;
            _chunksX = Mathf.CeilToInt(world.Cols / (float)ChunkSize);
            _chunksY = Mathf.CeilToInt(world.Rows / (float)ChunkSize);
            _chunks = new List<GameObject>[_chunksX * _chunksY];

            var rootGo = new GameObject("WallShadows");
            rootGo.transform.SetParent(transform, false);
            // 자식 캐스터들을 하나로 묶어 광원이 한 번에 처리하게 한다
            rootGo.AddComponent<CompositeShadowCaster2D>();
            _root = rootGo.transform;

            if (!Available) return;

            for (int i = 0; i < _chunks.Length; i++) RebuildChunk(i);
            _world.TileBroken += OnTileBroken;
            // 보스 장갑·소환 벽·뭉개기 — 파괴 이벤트가 아니어도 그림자는 다시 만든다
            _world.TileChanged += k => OnTileBroken(new TileBrokenEvent { Col = k % _world.Cols, Row = k / _world.Cols });
        }

        void OnTileBroken(TileBrokenEvent e)
        {
            int cx = e.Col / ChunkSize, cy = e.Row / ChunkSize;
            // 청크 경계의 벽은 이웃 청크의 사각형에도 걸쳐 있을 수 있다
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = cx + dx, y = cy + dy;
                    if (x < 0 || y < 0 || x >= _chunksX || y >= _chunksY) continue;
                    int id = y * _chunksX + x;
                    if (_dirtySet.Add(id)) _dirty.Enqueue(id);
                }
        }

        void LateUpdate()
        {
            if (!Available) return;
            for (int i = 0; i < RebuildsPerFrame && _dirty.Count > 0; i++)
            {
                int id = _dirty.Dequeue();
                _dirtySet.Remove(id);
                RebuildChunk(id);
            }
        }

        // ───────────────────────────── 청크 재생성
        void RebuildChunk(int chunkId)
        {
            var list = _chunks[chunkId];
            if (list != null)
            {
                foreach (var go in list) if (go != null) Destroy(go);
                list.Clear();
            }
            else list = _chunks[chunkId] = new List<GameObject>();

            int cx = chunkId % _chunksX, cy = chunkId / _chunksX;
            int x0 = cx * ChunkSize, y0 = cy * ChunkSize;
            int x1 = Mathf.Min(x0 + ChunkSize, _world.Cols);
            int y1 = Mathf.Min(y0 + ChunkSize, _world.Rows);

            foreach (var rect in DecomposeRects(x0, y0, x1, y1))
                list.Add(CreateCaster(rect));

            CasterCount = 0;
            foreach (var l in _chunks) if (l != null) CasterCount += l.Count;
        }

        struct RectI { public int X0, Y0, X1, Y1; }   // [X0,X1) x [Y0,Y1)

        /// <summary>
        /// 탐욕적 사각형 분할. 한 행에서 벽이 이어지는 구간을 찾고,
        /// 아래 행이 정확히 같은 구간이면 세로로 합친다.
        /// </summary>
        List<RectI> DecomposeRects(int x0, int y0, int x1, int y1)
        {
            var rects = new List<RectI>();
            int w = x1 - x0, h = y1 - y0;
            if (w <= 0 || h <= 0) return rects;

            var used = new bool[w * h];
            bool Solid(int x, int y) => _world.IsSolid(x, y);

            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    int li = (y - y0) * w + (x - x0);
                    if (used[li] || !Solid(x, y)) continue;

                    // 가로로 뻗는다
                    int runEnd = x;
                    while (runEnd + 1 < x1 && Solid(runEnd + 1, y) && !used[(y - y0) * w + (runEnd + 1 - x0)])
                        runEnd++;

                    // 같은 구간이 계속되는 동안 세로로 뻗는다
                    int colEnd = y;
                    while (colEnd + 1 < y1)
                    {
                        bool ok = true;
                        for (int xx = x; xx <= runEnd; xx++)
                            if (!Solid(xx, colEnd + 1) || used[(colEnd + 1 - y0) * w + (xx - x0)]) { ok = false; break; }
                        if (!ok) break;
                        colEnd++;
                    }

                    for (int yy = y; yy <= colEnd; yy++)
                        for (int xx = x; xx <= runEnd; xx++)
                            used[(yy - y0) * w + (xx - x0)] = true;

                    rects.Add(new RectI { X0 = x, Y0 = y, X1 = runEnd + 1, Y1 = colEnd + 1 });
                }

            return rects;
        }

        GameObject CreateCaster(RectI r)
        {
            float cxw = (r.X0 + r.X1) * 0.5f;
            float cyw = (r.Y0 + r.Y1) * 0.5f;
            float hw = (r.X1 - r.X0) * 0.5f;
            float hh = (r.Y1 - r.Y0) * 0.5f;

            var go = new GameObject($"ShadowRect_{r.X0}_{r.Y0}");
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(cxw, cyw, 0f);

            var caster = go.AddComponent<ShadowCaster2D>();
            caster.selfShadows = true;
            caster.castsShadows = true;

            ApplyShape(caster, new[]
            {
                new Vector3(-hw, -hh, 0f),
                new Vector3( hw, -hh, 0f),
                new Vector3( hw,  hh, 0f),
                new Vector3(-hw,  hh, 0f),
            });

            return go;
        }
    }
}
