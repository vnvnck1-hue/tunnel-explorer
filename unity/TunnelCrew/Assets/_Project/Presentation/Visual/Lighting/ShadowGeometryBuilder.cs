using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 대형 세트피스가 자기 그림자 윤곽을 직접 내놓는 확장 지점(기능명세서 §7.4
    /// "시각 높이가 큰 세트피스는 별도 캐스터 프로파일을 가진다").
    ///
    /// 이번 배치에서는 <b>인터페이스와 수집 경로만</b> 준비한다. 실제 세트피스 카탈로그와
    /// 아트별 윤곽은 승인 아트가 들어온 뒤 붙인다.
    /// </summary>
    public interface IShadowContourSource
    {
        /// <summary>
        /// 윤곽을 시뮬레이션 셀 좌표로 넣는다. 닫힌 고리여야 하고, 벽이 진행 방향의
        /// 왼쪽에 오도록(반시계) 감아야 한다 — <see cref="WallContourTracer"/> 와 같은 규약이다.
        /// </summary>
        void AppendContours(List<Vector2[]> into);

        /// <summary>윤곽이 바뀌었는지 판단하는 값. 바뀌지 않으면 캐스터를 다시 만들지 않는다.</summary>
        int ContourVersion { get; }
    }

    /// <summary>
    /// 기능명세서 §7.4-3 — 벽 footprint 윤곽에서 <see cref="ShadowCaster2D"/> 를 만든다.
    ///
    /// <b>기존 <c>WallShadowBuilder</c> 와의 차이</b>
    /// <list type="bullet">
    /// <item>셀 사각형 분할이 아니라 <see cref="WallContourTracer"/> 의 외곽선을 쓴다.
    ///       벽 셀 사이의 안쪽 경계 변이 아예 생기지 않는다.</item>
    /// <item>형태 해시를 내용에서 만든다. 기존 코드는 <c>Environment.TickCount</c> 를 섞어
    ///       같은 형태에서도 매번 값이 달라졌다.</item>
    /// <item>파괴 시 윤곽을 다시 추적하고 <b>해시가 바뀐 캐스터만</b> 다시 만든다.</item>
    /// </list>
    ///
    /// <b>안쪽 경계를 지운다는 것의 의미</b> — 벽과 벽 사이의 변을 만들지 않는다는 뜻이지,
    /// 부호 면적이 음수인 고리를 버린다는 뜻이 아니다. 절차 맵에서는 파낸 방을 제외한
    /// 고체 영역이 대부분 하나로 이어져 있어서, 음수 고리(= 방·통로의 경계)를 버리면
    /// 실내 벽 그림자가 전부 사라진다. 그래서 모든 고리를 캐스터로 만들고, 방향 규약만
    /// "벽이 왼쪽"으로 통일한다.
    ///
    /// 이 컴포넌트는 <see cref="WallShadowBuilder"/> 를 <b>대체하지 않는다.</b> 본선 씬은
    /// 아직 기존 빌더를 쓰고, 여기서 만든 것은 Visual Lab 에서 먼저 검증한다.
    /// </summary>
    public sealed class ShadowGeometryBuilder : MonoBehaviour
    {
        [Tooltip("한 프레임에 만들거나 지우는 캐스터 수 상한. 대량 파괴를 여러 프레임에 나눈다(§6.7).")]
        [SerializeField, Range(1, 256)] int _castersPerFrame = 32;

        [Tooltip("자기 그림자. 켜면 캐스터 다각형 안쪽이 그대로 어두워져 벽면이 검게 잠긴다.")]
        [SerializeField] bool _selfShadows;

        // ───────────────────────────── 리플렉션
        // URP 17.3 의 ShadowCaster2D 는 형태를 넣는 API 가 전부 internal 이고, 콜라이더
        // 자동 감지는 #if UNITY_EDITOR 안에 있어 런타임에서 동작하지 않는다.
        // 필드 이름이 바뀌면 Available 이 false 가 되고 그림자만 조용히 빠진다.
        static FieldInfo _fShapePath, _fShapePathHash, _fCastingSource, _fForceRebuild;
        static Type _castingSourceType;
        static bool _probed;

        public static bool Available
        {
            get { Probe(); return _fShapePath != null && _fCastingSource != null; }
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
                Debug.LogWarning("[비주얼] ShadowCaster2D 의 내부 필드를 찾지 못했다. " +
                                 "URP 버전이 바뀐 것으로 보인다. 윤곽 그림자를 건너뛴다.");
        }

        // ───────────────────────────── 상태

        WallContourTracer.WallAt _isWall;
        int _cols, _rows;
        Transform _root;

        readonly List<WallContourTracer.Contour> _contours = new List<WallContourTracer.Contour>(64);
        /// <summary>내용 해시 → 살아 있는 캐스터. 해시가 그대로면 다시 만들지 않는다.</summary>
        readonly Dictionary<int, GameObject> _live = new Dictionary<int, GameObject>(64);
        readonly List<int> _stale = new List<int>(32);
        readonly List<WallContourTracer.Contour> _pending = new List<WallContourTracer.Contour>(32);

        readonly List<IShadowContourSource> _setpieces = new List<IShadowContourSource>(8);
        readonly List<Vector2[]> _setpieceBuffer = new List<Vector2[]>(16);

        bool _dirty;

        /// <summary>살아 있는 캐스터 수. 디버그 오버레이와 테스트가 읽는다.</summary>
        public int CasterCount => _live.Count;
        /// <summary>추적된 윤곽. 디버그 오버레이가 선으로 그린다.</summary>
        public IReadOnlyList<WallContourTracer.Contour> Contours => _contours;
        public int PendingCasters => _pending.Count + _stale.Count;

        // ───────────────────────────── 구성

        /// <summary>
        /// 벽 판정자를 연결한다. <see cref="EnvironmentChunkRenderer"/> 의 표면 배열에서
        /// 넘기면 표면 생성기가 무엇을 벽으로 보는지와 그림자가 항상 일치한다.
        /// </summary>
        public void Bind(WallContourTracer.WallAt isWall, int cols, int rows)
        {
            Clear();
            _isWall = isWall;
            _cols = cols;
            _rows = rows;

            var rootGo = new GameObject("Wall Shadow Contours");
            rootGo.transform.SetParent(transform, false);
            // 자식 캐스터를 하나로 묶어 광원이 한 번에 처리하게 한다.
            rootGo.AddComponent<CompositeShadowCaster2D>();
            _root = rootGo.transform;

            _dirty = true;
            Rebuild();
            FlushPending();
        }

        public void RegisterSetpiece(IShadowContourSource source)
        {
            if (source != null && !_setpieces.Contains(source)) { _setpieces.Add(source); _dirty = true; }
        }

        public void UnregisterSetpiece(IShadowContourSource source)
        {
            if (source != null && _setpieces.Remove(source)) _dirty = true;
        }

        /// <summary>
        /// 벽이 바뀌었다. 셀 단위로 받아 두고 실제 추적은 프레임에 한 번만 한다 —
        /// 대량 파괴에서 이벤트마다 전체 추적을 돌리지 않기 위한 병합이다.
        /// </summary>
        public void MarkDirty() => _dirty = true;

        void Clear()
        {
            foreach (var go in _live.Values) if (go != null) DestroyGo(go);
            _live.Clear();
            _contours.Clear();
            _pending.Clear();
            _stale.Clear();
            if (_root != null) { DestroyGo(_root.gameObject); _root = null; }
        }

        void OnDestroy() => Clear();

        void DestroyGo(GameObject go)
        {
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
        }

        void LateUpdate()
        {
            if (_dirty) Rebuild();
            if (PendingCasters > 0) Step(_castersPerFrame);
        }

        // ───────────────────────────── 재생성

        /// <summary>윤곽을 다시 추적하고, 해시가 바뀐 것만 작업 목록에 올린다.</summary>
        void Rebuild()
        {
            _dirty = false;
            if (_isWall == null) return;

            WallContourTracer.Trace(_isWall, _cols, _rows, _contours);

            // 세트피스 윤곽을 같은 목록에 합친다. 확장 지점만 열어 둔 상태다.
            if (_setpieces.Count > 0)
            {
                _setpieceBuffer.Clear();
                for (int i = 0; i < _setpieces.Count; i++) _setpieces[i]?.AppendContours(_setpieceBuffer);
                for (int i = 0; i < _setpieceBuffer.Count; i++)
                {
                    var poly = _setpieceBuffer[i];
                    if (poly == null || poly.Length < 3) continue;
                    _contours.Add(FromPolygon(poly));
                }
            }

            // 새로 필요한 것 / 더 필요 없는 것을 가른다.
            _pending.Clear();
            _stale.Clear();

            var wanted = new HashSet<int>();
            for (int i = 0; i < _contours.Count; i++)
            {
                int h = _contours[i].ContentHash;
                if (!wanted.Add(h)) continue;             // 같은 형태가 두 번 나오면 하나로 족하다
                if (!_live.ContainsKey(h)) _pending.Add(_contours[i]);
            }

            foreach (var kv in _live)
                if (!wanted.Contains(kv.Key)) _stale.Add(kv.Key);
        }

        /// <summary>작업 목록을 예산만큼 처리한다.</summary>
        void Step(int budget)
        {
            while (budget > 0 && _stale.Count > 0)
            {
                int h = _stale[_stale.Count - 1];
                _stale.RemoveAt(_stale.Count - 1);
                if (_live.TryGetValue(h, out var go))
                {
                    if (go != null) DestroyGo(go);
                    _live.Remove(h);
                }
                budget--;
            }

            while (budget > 0 && _pending.Count > 0)
            {
                var contour = _pending[_pending.Count - 1];
                _pending.RemoveAt(_pending.Count - 1);
                if (!_live.ContainsKey(contour.ContentHash))
                {
                    var go = CreateCaster(contour);
                    if (go != null) _live[contour.ContentHash] = go;
                }
                budget--;
            }
        }

        /// <summary>남은 작업을 지금 전부 처리한다. 씬 진입·캡처 전에 쓴다.</summary>
        public void FlushPending()
        {
            if (_dirty) Rebuild();
            while (PendingCasters > 0) Step(int.MaxValue);
        }

        static WallContourTracer.Contour FromPolygon(Vector2[] poly)
        {
            var c = new WallContourTracer.Contour();
            for (int i = 0; i < poly.Length; i++)
                c.Points.Add(new WallContourTracer.Corner(
                    Mathf.RoundToInt(poly[i].x), Mathf.RoundToInt(poly[i].y)));
            c.DoubleSignedArea = WallContourTracer.DoubleSignedArea(c.Points);
            c.ContentHash = WallContourTracer.HashOf(c);
            return c;
        }

        GameObject CreateCaster(WallContourTracer.Contour contour)
        {
            if (!Available || _root == null) return null;

            var pts = contour.Points;
            // 캐스터 원점을 윤곽의 첫 정점에 두고 형태는 상대 좌표로 넣는다.
            // 그래야 큰 맵에서 좌표가 커져 정밀도가 떨어지지 않는다.
            var originCell = new Vector2(pts[0].X, pts[0].Y);
            var origin = IsometricProjection.ToRender(originCell);

            var go = new GameObject($"ShadowContour_{contour.StartCol}_{contour.StartRow}_{contour.StartSide}");
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(origin.x, origin.y, 0f);

            var caster = go.AddComponent<ShadowCaster2D>();
            // selfShadows 를 켜면 벽 다각형 안쪽이 그대로 잠겨 벽면이 검어진다.
            // 그림자는 벽 너머 바닥에만 떨어져야 한다.
            caster.selfShadows = _selfShadows;
            caster.castsShadows = true;

            var path = new Vector3[pts.Count];
            for (int i = 0; i < pts.Count; i++)
            {
                var p = IsometricProjection.ToRender(new Vector2(pts[i].X, pts[i].Y));
                path[i] = new Vector3(p.x - origin.x, p.y - origin.y, 0f);
            }

            _fShapePath.SetValue(caster, path);
            // 내용 해시를 그대로 넣는다 — 같은 형태면 같은 값이어야 한다.
            _fShapePathHash?.SetValue(caster, contour.ContentHash);
            // ShadowCastingSources.ShapeEditor == 1
            _fCastingSource.SetValue(caster, Enum.ToObject(_castingSourceType, 1));
            _fForceRebuild?.SetValue(caster, true);

            return go;
        }
    }
}
