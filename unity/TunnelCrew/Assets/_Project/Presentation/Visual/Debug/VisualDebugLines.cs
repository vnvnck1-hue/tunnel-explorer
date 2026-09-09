using System.Collections.Generic;
using UnityEngine;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 디버그 선을 <b>실제 씬 지오메트리</b>로 그린다(§12.4 "그림자 캐스터 윤곽",
    /// "visual height 와 pivot", "dirty chunk").
    ///
    /// <b>왜 OnGUI 가 아닌가</b> — 자동 캡처는 플레이 모드에 들어가지 않는다(§12.3·§16.3).
    /// 에디터에서는 <c>OnGUI</c> 가 불리지 않고, 불러도 화면 GUI 로 가서 렌더 텍스처에
    /// 남지 않는다. 선을 메시로 그리면 <c>Camera.Render</c> 에 그대로 들어오므로 캡처와
    /// 플레이 모드가 같은 화면을 낸다.
    ///
    /// 텍스트 읽을거리는 <see cref="DepthDebugOverlay"/> 의 GUI 가 계속 담당한다.
    /// </summary>
    public sealed class VisualDebugLines : MonoBehaviour
    {
        [Tooltip("선을 올릴 Sorting Layer. §6.4 의 WorldOverlay 를 쓴다.")]
        [SerializeField] string _sortingLayer = VisualLayers.WorldOverlay;

        readonly List<Vector3> _verts = new List<Vector3>(1024);
        readonly List<Color> _colors = new List<Color>(1024);
        readonly List<int> _indices = new List<int>(1024);

        Mesh _mesh;
        MeshRenderer _renderer;
        Material _material;

        /// <summary>그려진 선분 수. 테스트와 오버레이가 읽는다.</summary>
        public int SegmentCount => _indices.Count / 2;

        public bool Visible
        {
            get => _renderer != null && _renderer.enabled;
            set { if (_renderer != null) _renderer.enabled = value; }
        }

        void Ensure()
        {
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "Visual Debug Lines" };
                _mesh.MarkDynamic();
            }

            if (!TryGetComponent(out MeshFilter filter)) filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = _mesh;

            if (!TryGetComponent(out _renderer)) _renderer = gameObject.AddComponent<MeshRenderer>();

            if (_material == null)
            {
                // 정점 색을 쓰는 가장 단순한 스프라이트 셰이더. 조명을 받지 않아야 한다.
                var shader = Shader.Find("Sprites/Default");
                _material = shader != null
                    ? new Material(shader) { name = "Visual Debug Lines" }
                    : null;
                if (_material != null) _material.mainTexture = Texture2D.whiteTexture;
            }
            if (_material != null) _renderer.sharedMaterial = _material;

            if (VisualLayers.Exists(_sortingLayer)) _renderer.sortingLayerName = _sortingLayer;
            _renderer.sortingOrder = 32000;      // 오버레이는 늘 맨 앞
        }

        public void Begin()
        {
            Ensure();
            _verts.Clear();
            _colors.Clear();
            _indices.Clear();
        }

        /// <summary>시뮬레이션 셀 좌표로 선분을 넣는다.</summary>
        public void Line(Vector2 aCell, Vector2 bCell, Color color)
        {
            var a = IsometricProjection.ToRender(aCell);
            var b = IsometricProjection.ToRender(bCell);
            _indices.Add(_verts.Count);
            _verts.Add(new Vector3(a.x, a.y, 0f));
            _colors.Add(color);
            _indices.Add(_verts.Count);
            _verts.Add(new Vector3(b.x, b.y, 0f));
            _colors.Add(color);
        }

        /// <summary>닫힌 고리.</summary>
        public void Loop(IReadOnlyList<Vector2> cells, Color color)
        {
            if (cells == null || cells.Count < 2) return;
            for (int i = 0; i < cells.Count; i++)
                Line(cells[i], cells[(i + 1) % cells.Count], color);
        }

        public void Cross(Vector2 cell, float radius, Color color)
        {
            Line(cell + new Vector2(-radius, 0f), cell + new Vector2(radius, 0f), color);
            Line(cell + new Vector2(0f, -radius), cell + new Vector2(0f, radius), color);
        }

        public void Rect(Rect cells, Color color)
        {
            var a = new Vector2(cells.xMin, cells.yMin);
            var b = new Vector2(cells.xMax, cells.yMin);
            var c = new Vector2(cells.xMax, cells.yMax);
            var d = new Vector2(cells.xMin, cells.yMax);
            Line(a, b, color); Line(b, c, color); Line(c, d, color); Line(d, a, color);
        }

        public void End()
        {
            if (_mesh == null) return;
            _mesh.Clear();
            if (_verts.Count == 0) return;

            _mesh.SetVertices(_verts);
            _mesh.SetColors(_colors);
            _mesh.SetIndices(_indices, MeshTopology.Lines, 0, calculateBounds: true);
        }

        void OnDestroy()
        {
            if (_mesh != null)
            {
                if (Application.isPlaying) Destroy(_mesh); else DestroyImmediate(_mesh);
            }
            if (_material != null)
            {
                if (Application.isPlaying) Destroy(_material); else DestroyImmediate(_material);
            }
        }

        // ───────────────────────────── 자주 쓰는 조합

        /// <summary>벽 footprint 윤곽. 바깥 고리와 방·통로 고리를 색으로 구분한다(§7.4).</summary>
        public void DrawShadowContours(ShadowGeometryBuilder shadows,
            Color outerColor, Color innerColor)
        {
            if (shadows == null) return;
            var contours = shadows.Contours;
            var buffer = new List<Vector2>(64);

            for (int i = 0; i < contours.Count; i++)
            {
                var contour = contours[i];
                buffer.Clear();
                for (int j = 0; j < contour.PointCount; j++)
                {
                    contour.PointAt(j, out float x, out float y);
                    buffer.Add(new Vector2(x, y));
                }
                Loop(buffer, contour.IsOuter ? outerColor : innerColor);
            }
        }

        /// <summary>발 위치와 시각 높이(§6.5).</summary>
        public void DrawFootpoints(Color footColor, Color heightColor)
        {
            var anchors = FootpointSorter.All;
            for (int i = 0; i < anchors.Count; i++)
            {
                var a = anchors[i];
                if (a == null) continue;

                Cross(a.groundPosition, 0.3f, footColor);

                if (a.visualHeight > 0.001f)
                {
                    // 시각 높이는 화면 위 방향의 월드 유닛이라 셀 좌표로 되돌려 그린다.
                    var ground = IsometricProjection.ToRender(a.groundPosition);
                    var top = IsometricProjection.ToWorld(new Vector2(ground.x, ground.y + a.visualHeight));
                    Line(a.groundPosition, top, heightColor);
                }

                // 전경 페이드 판정 반경
                DrawCircle(a.groundPosition, a.footprintRadius, new Color(footColor.r, footColor.g, footColor.b, 0.5f));
            }
        }

        public void DrawCircle(Vector2 cell, float radius, Color color, int segments = 20)
        {
            if (radius <= 0f) return;
            Vector2 prev = cell + new Vector2(radius, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments * Mathf.PI * 2f;
                var next = cell + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * radius;
                Line(prev, next, color);
                prev = next;
            }
        }

        /// <summary>전경 오클루더의 페이드 그룹 경계(§6.6).</summary>
        public void DrawForegroundGroups(Color fadedColor, Color idleColor)
        {
            var occluders = Object.FindObjectsByType<ForegroundOccluder>(FindObjectsSortMode.None);
            foreach (var o in occluders)
            {
                if (o == null) continue;
                Rect(o.footprintCells, o.Alpha < 0.999f ? fadedColor : idleColor);
            }
        }
    }
}
