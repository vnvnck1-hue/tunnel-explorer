using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 배치된 세트피스 하나. 자기 그림자 윤곽을 <see cref="ShadowGeometryBuilder"/> 에
    /// 내놓는다(§7.4 "시각 높이가 큰 세트피스는 별도 캐스터 프로파일을 가진다").
    /// </summary>
    [ExecuteAlways]
    public sealed class SetPieceInstance : MonoBehaviour, IShadowContourSource
    {
        [SerializeField] string _assetId;
        [SerializeField] Vector2 _groundCell;
        [SerializeField, Min(0.01f)] float _visualScale = 1f;

        SetPieceDef _def;
        int _version;
        readonly List<Vector2> _buffer = new List<Vector2>(16);

        public string AssetId => _assetId;
        public SetPieceDef Def => _def;

        /// <summary>발 위치(셀). 바꾸면 윤곽 버전이 올라 캐스터가 다시 만들어진다.</summary>
        public Vector2 GroundCell
        {
            get => _groundCell;
            set
            {
                if (_groundCell == value) return;
                _groundCell = value;
                _version++;
            }
        }

        public int ContourVersion => _version;

        public void Bind(SetPieceDef def, Vector2 groundCell, float visualScale = 1f)
        {
            _def = def;
            _assetId = def != null ? def.assetId : null;
            _groundCell = groundCell;
            _visualScale = Mathf.Max(0.01f, visualScale);
            _version++;
        }

        public void AppendContours(List<Vector2[]> into)
        {
            if (_def == null || into == null) return;
            _def.AppendContour(_groundCell, _buffer);
            if (!Mathf.Approximately(_visualScale, 1f))
            {
                for (int i = 0; i < _buffer.Count; i++)
                    _buffer[i] = _groundCell + (_buffer[i] - _groundCell) * _visualScale;
            }
            if (_buffer.Count >= 3) into.Add(_buffer.ToArray());
        }
    }

    /// <summary>
    /// 기능명세서 §6.3·§8.7 — 세트피스를 소켓 위치에 세운다.
    ///
    /// 한곳에서 조립하는 이유는 배선이 여러 시스템에 걸쳐 있기 때문이다.
    /// <list type="bullet">
    /// <item><see cref="VisualHeightAnchor"/> — 발 위치 정렬(§6.5)</item>
    /// <item><see cref="ForegroundOccluder"/> — 전경 페이드(§6.6)</item>
    /// <item><see cref="ContactShadow"/> — 접촉 AO(§7.4-1)</item>
    /// <item><see cref="Light2D"/> — 조명 소켓(§7.3)</item>
    /// <item><see cref="SetPieceInstance"/> — 그림자 윤곽(§7.4)</item>
    /// </list>
    ///
    /// <b>스프라이트 피벗이 발점이므로 본체를 올리지 않는다.</b> 아트가 이미 발점 위로
    /// 솟은 픽셀을 담고 있어서, 앵커 원점에 그대로 두면 지면에 서 있는 것으로 읽힌다.
    /// <see cref="VisualHeightAnchor.visualHeight"/> 는 0 으로 둔다 — 그 값은 공중에 뜬
    /// 물체(정렬은 지면, 본체만 위로)를 위한 것이고, 접촉 AO 축소에도 쓰인다.
    /// </summary>
    public sealed class SetPieceSpawner : MonoBehaviour
    {
        [SerializeField] SetPieceCatalog _catalog;
        [SerializeField] WorldVisualProfile _profile;

        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<SetPieceInstance> _instances = new List<SetPieceInstance>();

        public SetPieceCatalog Catalog { get => _catalog; set => _catalog = value; }
        public WorldVisualProfile Profile { get => _profile; set => _profile = value; }

        public IReadOnlyList<SetPieceInstance> Instances => _instances;
        public int SpawnedCount => _spawned.Count;

        /// <summary>배치한 세트피스를 전부 지운다.</summary>
        public void Clear(ShadowGeometryBuilder shadows = null)
        {
            for (int i = 0; i < _instances.Count; i++)
                if (_instances[i] != null) shadows?.UnregisterSetpiece(_instances[i]);
            _instances.Clear();

            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] == null) continue;
                if (Application.isPlaying) Destroy(_spawned[i]);
                else DestroyImmediate(_spawned[i]);
            }
            _spawned.Clear();
        }

        /// <summary>
        /// 카탈로그의 자산을 발 위치에 세운다. 없는 ID 면 null 을 돌려주고 경고를 남긴다 —
        /// 조용히 빠지면 방이 비어 보이는 원인을 찾기 어렵다.
        /// </summary>
        public SetPieceInstance Spawn(string assetId, Vector2 groundCell,
            ShadowGeometryBuilder shadows = null, float visualScale = 1f)
        {
            if (_catalog == null)
            {
                Debug.LogWarning("[비주얼] SetPieceCatalog 가 연결되지 않았다.");
                return null;
            }

            var def = _catalog.Find(assetId);
            if (def == null)
            {
                Debug.LogWarning($"[비주얼] 카탈로그에 '{assetId}' 가 없다 — 세트피스를 건너뛴다.");
                return null;
            }
            if (def.sprite == null)
            {
                Debug.LogWarning($"[비주얼] '{assetId}' 에 스프라이트가 없다 — 세트피스를 건너뛴다.");
                return null;
            }

            var go = new GameObject($"SetPiece {def.assetId}");
            go.transform.SetParent(transform, false);
            visualScale = Mathf.Max(0.01f, visualScale);
            go.transform.localScale = new Vector3(visualScale, visualScale, 1f);
            _spawned.Add(go);

            // ── 본체
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(go.transform, false);
            bodyGo.transform.localPosition = Vector3.zero;   // 피벗이 발점이다
            var sr = bodyGo.AddComponent<SpriteRenderer>();
            sr.sprite = def.sprite;

            if (def.materials != null)
            {
                var mat = def.materials.CreateMaterial(_profile, $"{def.assetId}-lit");
                if (mat != null) sr.sharedMaterial = mat;
            }

            // ── 발 위치 정렬
            var anchor = go.AddComponent<VisualHeightAnchor>();
            anchor.groundPosition = groundCell;
            anchor.footprintCells = new Vector2(
                Mathf.Max(1, def.footprintCells.x), Mathf.Max(1, def.footprintCells.y));
            anchor.visualHeight = 0f;
            anchor.sortingLayer = VisualLayers.Exists(def.sortingLayer)
                ? def.sortingLayer
                : VisualLayers.WorldEntity;
            anchor.localOrder = def.localOrder;
            anchor.footprintRadius = def.ResolvedContactRadius;

            // ── 전경 페이드
            if (def.IsForeground)
            {
                var occ = go.AddComponent<ForegroundOccluder>();
                occ.sprites = new[] { sr };
                occ.fadeGroup = string.IsNullOrEmpty(def.occluderGroup)
                    ? def.assetId.GetHashCode()
                    : def.occluderGroup.GetHashCode();
                occ.fadeTargetAlpha = def.fadeTargetAlpha;

                float halfW = Mathf.Max(1, def.footprintCells.x) * 0.5f * visualScale;
                float rows = Mathf.Max(1, def.footprintCells.y) * visualScale;
                occ.footprintCells = new Rect(
                    groundCell.x - halfW, groundCell.y,
                    Mathf.Max(1, def.footprintCells.x) * visualScale, rows);

                // 소품의 스프라이트는 발점에서 <b>위(북쪽)로</b> 시각 높이만큼 솟는다. 그래서
                // 이 소품이 실제로 가리는 화면 영역은 [발점, 발점 + 시각 높이] 이고, footprint
                // 깊이를 넘는 나머지가 오클루더의 남쪽 도달 거리다. 기본값 1셀로 두면
                // 1.94셀 전경 난간이 캐릭터를 가리는데도 페이드가 걸리지 않는다.
                occ.capLiftCells = Mathf.Max(0f, def.visualHeightCells * visualScale - rows);
            }

            // ── 접촉 AO
            var shadow = go.AddComponent<ContactShadow>();
            shadow.radius = def.ResolvedContactRadius * visualScale;
            shadow.scaleWithVisualHeight = false;   // 지면에 서 있다

            // ── 조명 소켓
            if (def.lightSockets != null)
            {
                for (int i = 0; i < def.lightSockets.Length; i++)
                    AttachSocketLight(go.transform, def, def.lightSockets[i]);
            }

            // ── 그림자 윤곽
            var instance = go.AddComponent<SetPieceInstance>();
            instance.Bind(def, groundCell, visualScale);
            _instances.Add(instance);
            shadows?.RegisterSetpiece(instance);

            return instance;
        }

        void AttachSocketLight(Transform parent, SetPieceDef def, LightSocketDef socket)
        {
            var go = new GameObject($"Light {(!string.IsNullOrEmpty(socket.id) ? socket.id : "socket")}");
            go.transform.SetParent(parent, false);

            // 오프셋은 발점 기준 셀 좌표다. 투영을 거쳐 화면 좌표로 옮긴다 —
            // 원점(발점)과의 차이를 써야 마름모 프리셋에서도 어긋나지 않는다.
            var at = IsometricProjection.ToRender(socket.offsetCells);
            var origin = IsometricProjection.ToRender(Vector2.zero);
            go.transform.localPosition = new Vector3(at.x - origin.x, at.y - origin.y, 0f);

            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.pointLightInnerAngle = 360f;
            light.pointLightOuterAngle = 360f;
            light.pointLightInnerRadius = 0f;
            // 아트 값은 자산 기준 상대값이다. 방 스케일·환경광에 맞추는 배율은 프로파일이 든다.
            float rangeScale = _profile != null ? Mathf.Max(0.01f, _profile.socketLightRangeScale) : 1f;
            float intensityScale = _profile != null ? Mathf.Max(0.01f, _profile.socketLightIntensityScale) : 1f;

            light.pointLightOuterRadius = Mathf.Max(0.1f, socket.rangeCells * rangeScale);
            light.color = socket.color;
            light.intensity = Mathf.Max(0f, socket.intensity * intensityScale);

            // 그림자는 여기서 정하지 않는다. §13 의 그림자 광원 예산은 전역 결정이라
            // LightSocketRenderer 가 방 전체를 우선순위로 줄 세워 켠다 — 광원 하나가
            // 자기만 보고 결정하면 램프가 늘어날 때 예산이 무너진다.
            light.shadowIntensity = 0f;

            var slot = go.AddComponent<LightSocket>();
            slot.lightClass = socket.lightClass;
            // 소켓 컴포넌트도 배율을 먹은 값을 든다 — 깜빡임과 그림자 우선순위가 실제 광원과
            // 같은 수치를 봐야 한다.
            slot.baseIntensity = Mathf.Max(0f, socket.intensity * intensityScale);
            slot.rangeCells = Mathf.Max(0.1f, socket.rangeCells * rangeScale);
            // 위상을 ID 에서 유도해 같은 종류의 램프가 한꺼번에 흔들리지 않게 한다.
            // 해시라서 실행마다 같다.
            unchecked
            {
                int h = (def.assetId != null ? def.assetId.GetHashCode() : 0)
                        ^ (socket.id != null ? socket.id.GetHashCode() * 31 : 0);
                slot.phase = (h & 0xFFFF) / 65535f;
            }
        }

#if UNITY_EDITOR
        public void EditorAssign(SetPieceCatalog catalog, WorldVisualProfile profile)
        {
            _catalog = catalog;
            _profile = profile;
        }
#endif
    }
}
