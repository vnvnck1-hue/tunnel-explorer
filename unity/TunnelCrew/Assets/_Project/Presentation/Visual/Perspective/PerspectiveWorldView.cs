using System;
using System.Collections.Generic;
using TunnelCrew.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 메인 런의 실제 <see cref="WorldGrid"/> 를 3D 벽과 카메라 빌보드로 표시하는 프레젠테이션 레이어.
    /// 규칙·충돌·저장은 기존 시뮬레이션을 그대로 사용하며, 일련화 필드로 기존 2D 표시에 즉시 폴백할 수 있다.
    ///
    /// 구도는 2D 리그가 단일 출처다(<see cref="CameraDistanceNow"/>). 자산·조명·연출은
    /// <see cref="PerspectiveWorldRenderer"/> · <see cref="PerspectiveLightBuffer"/> ·
    /// <see cref="PerspectiveParticleMirror"/> 가 2D 본선과 같은 것을 쓴다.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class PerspectiveWorldView : MonoBehaviour
    {
        const int PreviewLayer = 31;
        const int HalfCols = 18;
        const int HalfRows = 15;
        const int TargetWidth = 480;
        const int TargetHeight = 270;
        const float WallHeight = 1.45f;
        /// <summary>리그가 없을 때의 기본 세로 가시 셀 수.</summary>
        const float DefaultViewCells = 13f;
        /// <summary>
        /// 2D 와 같은 캐릭터 크기에서 추가로 물러서는 배율. 1 이면 초점 위의 액터가 2D 와 정확히 같은 크기다.
        /// 원근에서는 카메라 쪽 액터가 더 크게 보이므로 조금 물러서야 2D 와 같은 인상이 된다.
        /// </summary>
        [SerializeField, Range(1f, 3f)] float _framingPullback = 1.8f;

        /// <summary>구도 튜닝용. 1 = 초점 위의 액터가 2D 본선과 정확히 같은 크기.</summary>
        public float FramingPullback
        {
            get => Mathf.Max(1f, _framingPullback);
            set => _framingPullback = Mathf.Clamp(value, 1f, 3f);
        }
        const float CameraFov = 28f;
        static readonly float[] CameraPitchPresets = { 55f, 60f, 65f };

        static readonly Color VoidColor = new Color(0.018f, 0.014f, 0.03f);

        readonly Dictionary<Sprite, Mesh> _spriteMeshes = new();
        readonly Dictionary<Texture, Material> _spriteMaterials = new();
        readonly Dictionary<Texture, Material> _silhouetteMaterials = new();
        readonly Dictionary<SpriteRenderer, BillboardProxy> _actorProxies = new();
        /// <summary>표현 계약(<see cref="PerspectiveActors"/>) 슬롯 → 빌보드. 플레이어·크루·적이 여기로 온다.</summary>
        readonly Dictionary<int, BillboardProxy> _contractProxies = new();
        /// <summary>이번 프레임에 계약으로 올라온 원본 SpriteRenderer — 레거시 미러가 이중으로 그리지 않게 제외한다.</summary>
        readonly HashSet<SpriteRenderer> _contractSources = new();
        /// <summary>선·궤적(대시 선·투사체 트레일·적 선딜)의 3D 대응. 바닥 평면에 눕혀 그린다.</summary>
        readonly Dictionary<Renderer, LineRenderer> _lineProxies = new();
        readonly List<Vector3> _linePoints = new(32);
        // 재스캔은 0.25~0.5초마다 돈다. 임시 컬렉션을 매번 새로 만들면 그만큼 주기적 GC 가 생긴다(§4 7단계).
        readonly HashSet<SpriteRenderer> _liveSprites = new();
        readonly List<SpriteRenderer> _staleSprites = new();
        readonly HashSet<Renderer> _liveLines = new();
        readonly List<Renderer> _staleLines = new();

        /// <summary>바닥·벽 기하. 2D 본선과 같은 환경 키트·재질 채널을 쓴다(§4 3단계).</summary>
        PerspectiveWorldRenderer _worldRenderer;
        /// <summary>기존 Light2D 데이터를 3D 셰이더 버퍼로 옮긴다(§4 4단계).</summary>
        PerspectiveLightBuffer _lightBuffer;
        /// <summary>2D 파티클 연출을 카메라를 마주보는 쿼드로 다시 그린다(§4 5단계).</summary>
        PerspectiveParticleMirror _particles;

        WorldGrid _world;
        Func<PlayerState> _player;
        SpriteRenderer _sourcePlayer;
        Camera _gameplayCamera;
        CameraRig _rig;
        Camera _previewCamera;
        Transform _actors;
        RenderTexture _target;
        int _gameplayMask;
        bool _visible;
        int _cameraPreset;
        float _nextActorScan, _nextLineScan;
        Material _lineMaterial;
        GUIStyle _infoStyle;
        MaterialPropertyBlock _actorProperties;

        public bool Visible => _visible;
        public int CameraPreset => _cameraPreset;
        public float CameraPitch => CameraPitchPresets[_cameraPreset];

        public void Bind(WorldGrid world, Func<PlayerState> player, SpriteRenderer sourcePlayer, Camera gameplayCamera)
        {
            _world = world;
            _player = player;
            _sourcePlayer = sourcePlayer;
            _gameplayCamera = gameplayCamera;
            _rig = gameplayCamera != null ? gameplayCamera.GetComponent<CameraRig>() : null;
            if (_gameplayCamera != null) _gameplayMask = _gameplayCamera.cullingMask;
            EnsureRig();
            SetVisible(false);
        }

        /// <summary>
        /// 2D 본선이 쓰는 환경 키트·표면 규칙·재질 채널을 그대로 받아 3D 표면에 물린다(§4 3단계).
        /// 지층이 바뀌어 키트가 교체되면 다시 호출한다.
        /// </summary>
        public void BindEnvironment(ISolidField field, in SurfaceRules rules, EnvironmentKit kit,
            SurfaceMaterialSet floorSet, SurfaceMaterialSet topSet, SurfaceMaterialSet frontSet,
            WorldVisualProfile profile, System.Func<int, int, bool> isOreFace)
        {
            EnsureRig();
            if (_worldRenderer == null || kit == null) return;
            _worldRenderer.IsOreFace = isOreFace;
            _worldRenderer.Bind(field, rules, kit, floorSet, topSet, frontSet, profile, PreviewLayer);
        }

        /// <summary>채굴·파괴로 셀이 바뀌었다. 해당 청크만 다시 만든다.</summary>
        public void MarkCellDirty(int col, int row) => _worldRenderer?.MarkCellDirty(col, row);

        /// <summary>본선 렌더러 활성화. false 면 기존 2D 런 화면로 완전히 폴백한다.</summary>
        public void SetMainlineMode(bool enabled)
        {
            SetVisible(enabled);
        }

        void EnsureRig()
        {
            if (_previewCamera != null) return;

            var root = new GameObject("Gungeon Perspective Preview Root");
            root.transform.SetParent(transform, false);
            SetLayer(root, PreviewLayer);

            var worldGo = new GameObject("World surfaces");
            worldGo.transform.SetParent(root.transform, false);
            worldGo.layer = PreviewLayer;
            _worldRenderer = worldGo.AddComponent<PerspectiveWorldRenderer>();

            _lightBuffer = root.AddComponent<PerspectiveLightBuffer>();

            var particleGo = new GameObject("Particle mirror");
            particleGo.layer = PreviewLayer;
            _particles = particleGo.AddComponent<PerspectiveParticleMirror>();
            _particles.Bind(root.transform, PreviewLayer);

            _actors = new GameObject("Live actor billboards").transform;
            _actors.SetParent(root.transform, false);
            SetLayer(_actors.gameObject, PreviewLayer);

            var camGo = new GameObject("Preview Camera");
            camGo.transform.SetParent(root.transform, false);
            SetLayer(camGo, PreviewLayer);
            _previewCamera = camGo.AddComponent<Camera>();
            _previewCamera.enabled = false;
            _previewCamera.orthographic = false;
            _previewCamera.fieldOfView = CameraFov;
            _previewCamera.nearClipPlane = 0.1f;
            _previewCamera.farClipPlane = 80f;
            _previewCamera.clearFlags = CameraClearFlags.SolidColor;
            _previewCamera.backgroundColor = VoidColor;
            _previewCamera.cullingMask = 1 << PreviewLayer;
            _previewCamera.allowHDR = false;
            _previewCamera.allowMSAA = false;

            _target = new RenderTexture(TargetWidth, TargetHeight, 24, RenderTextureFormat.ARGB32)
            {
                name = "Perspective World Target",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
            };
            _target.Create();
            _previewCamera.targetTexture = _target;

            root.SetActive(false);
        }

        void Update()
        {
            var kb = Keyboard.current;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (kb != null && kb.f3Key.wasPressedThisFrame) SetVisible(!_visible);
#endif
            if (!_visible || kb == null) return;
            if (kb.rightBracketKey.wasPressedThisFrame) SetCameraPreset(_cameraPreset + 1);
            else if (kb.leftBracketKey.wasPressedThisFrame) SetCameraPreset(_cameraPreset - 1);
        }

        public void SetCameraPreset(int preset)
        {
            int count = CameraPitchPresets.Length;
            _cameraPreset = (preset % count + count) % count;
        }

        void LateUpdate()
        {
            if (!_visible || _world == null || _previewCamera == null) return;
            var player = _player?.Invoke();
            if (player == null) return;

            // 구도는 기존 2D 리그가 단일 출처다. 데드존·룩어헤드·흔들림·킥·보스 시네마틱·관전 추적이
            // 이미 그 중심에 들어 있으므로, 화면 중심만 시뮬 좌표로 되돌려 원근 카메라에 그대로 넘긴다.
            var focus = FocusPoint(player);
            _worldRenderer.UpdateStreaming(focus);
            if (_lightBuffer != null) _lightBuffer.Focus = focus;

            var ground = new Vector3(focus.x, 0.025f, focus.y);
            var rotation = Quaternion.Euler(CameraPitch, 0f, 0f);
            _previewCamera.transform.SetPositionAndRotation(
                ground + Vector3.up * 0.65f - rotation * Vector3.forward * CameraDistanceNow(),
                rotation);

            PerspectiveViewport.Publish(_previewCamera,
                PerspectiveViewport.FitRect(TargetWidth, TargetHeight, Screen.width, Screen.height));
            // 이번 프레임의 월드 정보는 뷰들이 Update 에서 쌓아 두었다 — OnGUI 의 Repaint 가 그린 뒤 비운다.

            SyncContractBillboards(ground, rotation);
            SyncLegacyBillboards(ground, rotation);
            SyncLines(ground);
            if (_particles != null) _particles.Sync(focus, rotation);
            _previewCamera.Render();
        }

        /// <summary>2D 리그의 화면 중심을 시뮬레이션 좌표로 되돌린 구도 기준점. 리그가 없으면 플레이어를 본다.</summary>
        Vector2 FocusPoint(PlayerState player)
        {
            if (_gameplayCamera != null)
            {
                var render = (Vector2)(Vector3)_gameplayCamera.transform.position;
                return IsometricProjection.ToWorld(render);
            }
            return new Vector2((float)player.Position.X, (float)player.Position.Y);
        }

        /// <summary>
        /// 원근 카메라 거리.
        ///
        /// 기준은 <b>캐릭터가 2D 본선과 같은 크기로 읽히는가</b>이지 바닥이 몇 칸 보이는가가 아니다.
        /// 액터는 카메라를 마주보는 빌보드라 초점 거리 d 에서 화면 세로에 차지하는 비율이
        /// H / (2·d·tan(fov/2)) 이다. 2D 직교에서는 H / N(가시 셀 수) 이므로
        /// 둘을 같게 두면 d = N / (2·tan(fov/2)) 가 된다.
        ///
        /// 여기에 <see cref="FramingPullback"/> 을 곱해 조금 더 물러선다 — 원근에서는 카메라에 가까운
        /// 액터가 초점보다 커 보이므로, 같은 값이면 화면이 캐릭터로 꽉 찬 인상이 된다(사용자 지적 2026-09-16).
        ///
        /// 바닥에 sin(θ) 를 넣어 맞추던 이전 식은 화면을 1/sin(55°)≈1.22 배 좁혀 캐릭터를 그만큼 키웠다.
        /// </summary>
        float CameraDistanceNow()
        {
            float cells = _rig != null && _rig.TargetViewCells > 1f ? _rig.TargetViewCells : DefaultViewCells;
            float distance = cells * 0.5f / Mathf.Tan(CameraFov * 0.5f * Mathf.Deg2Rad);
            return Mathf.Clamp(distance * FramingPullback / ZoomMultiplier(), 6f, 90f);
        }

        /// <summary>
        /// 2D 리그가 적용 중인 줌 배율. 보스 인트로 같은 시네마틱 줌이 원근 카메라 거리에 그대로 반영된다.
        /// </summary>
        float ZoomMultiplier()
        {
            if (_rig == null || _gameplayCamera == null || _gameplayCamera.orthographicSize < 0.01f) return 1f;
            float baseSize = _rig.TargetViewCells * 0.5f;
            if (baseSize < 0.01f) return 1f;
            return Mathf.Clamp(baseSize / _gameplayCamera.orthographicSize, 0.25f, 4f);
        }

        void SetVisible(bool visible)
        {
            _visible = visible;
            if (_previewCamera == null) return;
            _previewCamera.transform.parent.gameObject.SetActive(visible);
            if (_gameplayCamera != null)
                _gameplayCamera.cullingMask = visible ? _gameplayMask & ~(1 << PreviewLayer) : _gameplayMask;
            if (visible) _nextActorScan = _nextLineScan = 0f;
            else PerspectiveViewport.Clear();
        }

        /// <summary>
        /// 1단계 표현 계약 소비. 액터 뷰가 제출한 시뮬레이션 좌표·높이·프레임을 그대로 빌보드에 옮긴다.
        /// 뷰가 제출을 멈춘 슬롯은 자동으로 숨으므로 생사·풀링을 여기서 추적하지 않는다.
        /// </summary>
        void SyncContractBillboards(Vector3 playerGround, Quaternion cameraRotation)
        {
            _contractSources.Clear();
            int slots = PerspectiveActors.SlotCount;
            for (int slot = 0; slot < slots; slot++)
            {
                if (!PerspectiveActors.TryRead(slot, out var sample))
                {
                    if (_contractProxies.TryGetValue(slot, out var idle)) idle.GameObject.SetActive(false);
                    continue;
                }

                if (sample.Source != null) _contractSources.Add(sample.Source);

                if (!_contractProxies.TryGetValue(slot, out var proxy))
                    _contractProxies[slot] = proxy = CreateProxy($"Actor · {PerspectiveActors.NameOf(slot)}",
                        sample.Group == PerspectiveActorGroup.Actor);

                float dx = sample.Ground.x - playerGround.x;
                float dz = sample.Ground.y - playerGround.z;
                if (Mathf.Abs(dx) > HalfCols + 2f || Mathf.Abs(dz) > HalfRows + 2f)
                {
                    proxy.GameObject.SetActive(false);
                    continue;
                }

                proxy.GameObject.SetActive(true);
                if (proxy.Sprite != sample.Sprite || proxy.Filter.sharedMesh == null)
                {
                    proxy.Sprite = sample.Sprite;
                    proxy.Filter.sharedMesh = SpriteMesh(sample.Sprite);
                    proxy.Renderer.sharedMaterial = SpriteMaterial(sample.Sprite.texture);
                    if (proxy.SilhouetteFilter != null)
                    {
                        proxy.SilhouetteFilter.sharedMesh = proxy.Filter.sharedMesh;
                        var sil = SilhouetteMaterial(sample.Sprite.texture);
                        proxy.SilhouetteRenderer.sharedMaterial = sil;
                    }
                }

                // 바닥 대역(그림자·설치 표식·텔레그래프)은 카메라를 향해 세우지 않고 바닥에 눕힌다.
                var rotation = sample.Group == PerspectiveActorGroup.Ground
                    ? Quaternion.AngleAxis(-sample.Roll, Vector3.up) * Quaternion.Euler(-90f, 0f, 0f)
                    : cameraRotation * Quaternion.Euler(0f, 0f, sample.Roll);
                var position = new Vector3(sample.Ground.x, sample.Height, sample.Ground.y)
                    - cameraRotation * Vector3.forward * sample.DepthBias;
                if (sample.AlignFeet && sample.Group != PerspectiveActorGroup.Ground)
                {
                    // 스프라이트 아랫변을 바닥에 붙인다. 발 피벗 시트는 min.y 가 0 이라 보정이 0 이 된다.
                    float lift = -proxy.Filter.sharedMesh.bounds.min.y * sample.Scale.y;
                    position += rotation * Vector3.up * lift;
                }
                proxy.Transform.SetPositionAndRotation(position, rotation);
                proxy.Transform.localScale = new Vector3(
                    sample.Scale.x * (sample.FlipX ? -1f : 1f),
                    sample.Scale.y * (sample.FlipY ? -1f : 1f), 1f);
                ApplyTint(proxy, sample.Tint);

                if (proxy.SilhouetteRenderer != null)
                    proxy.SilhouetteRenderer.enabled = proxy.SilhouetteRenderer.sharedMaterial != null
                        && IsOccluded(sample.Ground, sample.Height);
            }
        }

        void SyncLegacyBillboards(Vector3 playerGround, Quaternion cameraRotation)
        {
            if (Time.unscaledTime >= _nextActorScan)
            {
                _nextActorScan = Time.unscaledTime + 0.25f;
                RefreshActorSources();
            }

            foreach (var pair in _actorProxies)
            {
                var source = pair.Key;
                var proxy = pair.Value;
                if (_contractSources.Contains(source) || !IsEligibleActor(source))
                {
                    proxy.GameObject.SetActive(false);
                    continue;
                }

                Vector2 logical = IsometricProjection.ToWorld(source.transform.position);
                float dx = logical.x - playerGround.x;
                float dz = logical.y - playerGround.z;
                if (Mathf.Abs(dx) > HalfCols + 2f || Mathf.Abs(dz) > HalfRows + 2f)
                {
                    proxy.GameObject.SetActive(false);
                    continue;
                }

                var sprite = source.sprite;
                proxy.GameObject.SetActive(true);
                if (proxy.Filter.sharedMesh == null || proxy.Sprite != sprite)
                {
                    proxy.Sprite = sprite;
                    proxy.Filter.sharedMesh = SpriteMesh(sprite);
                    proxy.Renderer.sharedMaterial = SpriteMaterial(sprite.texture);
                }

                float height = source.sortingLayerName == VisualLayers.WallTop ? WallHeight : 0.025f;
                // Enemy/Body 의 로컬 Y 는 지면 좌표가 아니라 바운스·노크백 연출 높이다.
                if (source.name == "Body" && source.transform.parent != null && source.transform.parent.name == "Enemy")
                {
                    logical = IsometricProjection.ToWorld(source.transform.parent.position);
                    height += Mathf.Max(0f, source.transform.localPosition.y);
                }

                float depthNudge = Mathf.Clamp(source.sortingOrder, -10000, 10000) * 0.00002f;
                var position = new Vector3(logical.x, height, logical.y) - cameraRotation * Vector3.forward * depthNudge;
                float zRotation = source.transform.eulerAngles.z;
                proxy.Transform.SetPositionAndRotation(position, cameraRotation * Quaternion.Euler(0f, 0f, zRotation));
                Vector3 sourceScale = source.transform.lossyScale;
                proxy.Transform.localScale = new Vector3(
                    Mathf.Abs(sourceScale.x) * (source.flipX ? -1f : 1f),
                    Mathf.Abs(sourceScale.y) * (source.flipY ? -1f : 1f), 1f);

                ApplyTint(proxy, source.color);
            }
        }

        /// <summary>
        /// 선·궤적 연출을 3D 로 옮긴다(§4 5단계). 대시 선·투사체 트레일·적 선딜 텔레그래프는
        /// 전부 바닥에 그려지는 정보이므로 바닥 평면에 그대로 눕힌다.
        /// </summary>
        void SyncLines(Vector3 playerGround)
        {
            if (Time.unscaledTime >= _nextLineScan)
            {
                _nextLineScan = Time.unscaledTime + 0.5f;
                RefreshLineSources();
            }

            foreach (var pair in _lineProxies)
            {
                var source = pair.Key;
                var proxy = pair.Value;
                if (source == null || !source.enabled || !source.gameObject.activeInHierarchy)
                {
                    proxy.enabled = false;
                    continue;
                }

                _linePoints.Clear();
                if (source is LineRenderer line)
                {
                    for (int i = 0; i < line.positionCount; i++)
                    {
                        var p = line.GetPosition(i);
                        if (!line.useWorldSpace) p = line.transform.TransformPoint(p);
                        _linePoints.Add(ToFloor(p));
                    }
                    proxy.startColor = line.startColor;
                    proxy.endColor = line.endColor;
                    proxy.startWidth = line.startWidth;
                    proxy.endWidth = line.endWidth;
                }
                else if (source is TrailRenderer trail)
                {
                    int count = trail.positionCount;
                    for (int i = 0; i < count; i++) _linePoints.Add(ToFloor(trail.GetPosition(i)));
                    proxy.startColor = trail.startColor;
                    proxy.endColor = trail.endColor;
                    proxy.startWidth = trail.startWidth;
                    proxy.endWidth = trail.endWidth;
                }

                if (_linePoints.Count < 2)
                {
                    proxy.enabled = false;
                    continue;
                }

                proxy.enabled = true;
                proxy.positionCount = _linePoints.Count;
                for (int i = 0; i < _linePoints.Count; i++) proxy.SetPosition(i, _linePoints[i]);
                if (proxy.sharedMaterial == null) proxy.sharedMaterial = LineMaterial();
            }
        }

        /// <summary>렌더 좌표의 한 점을 바닥 평면 위의 3D 점으로.</summary>
        static Vector3 ToFloor(Vector3 renderPoint)
        {
            var sim = IsometricProjection.ToWorld(new Vector2(renderPoint.x, renderPoint.y));
            return new Vector3(sim.x, 0.04f, sim.y);
        }

        void RefreshLineSources()
        {
            var lines = FindObjectsByType<LineRenderer>(FindObjectsSortMode.None);
            var trails = FindObjectsByType<TrailRenderer>(FindObjectsSortMode.None);
            var live = _liveLines;
            live.Clear();

            foreach (var line in lines)
            {
                if (line.transform.IsChildOf(_actors)) continue;
                live.Add(line);
                if (!_lineProxies.ContainsKey(line)) _lineProxies.Add(line, CreateLineProxy(line.name));
            }
            foreach (var trail in trails)
            {
                if (trail.transform.IsChildOf(_actors)) continue;
                live.Add(trail);
                if (!_lineProxies.ContainsKey(trail)) _lineProxies.Add(trail, CreateLineProxy(trail.name));
            }

            var stale = _staleLines;
            stale.Clear();
            foreach (var pair in _lineProxies) if (pair.Key == null || !live.Contains(pair.Key)) stale.Add(pair.Key);
            foreach (var key in stale)
            {
                if (_lineProxies.TryGetValue(key, out var proxy) && proxy != null) Destroy(proxy.gameObject);
                _lineProxies.Remove(key);
            }
        }

        LineRenderer CreateLineProxy(string sourceName)
        {
            var go = new GameObject($"Line · {sourceName}");
            go.transform.SetParent(_actors, false);
            SetLayer(go, PreviewLayer);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.alignment = LineAlignment.TransformZ;
            line.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // 바닥에 눕힌 띠
            line.sharedMaterial = LineMaterial();
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        Material LineMaterial()
        {
            if (_lineMaterial != null) return _lineMaterial;
            // 정점 색(그라디언트)을 존중하는 오버레이 셰이더. URP/Unlit 은 정점 색을 무시해 흰 띠가 된다.
            var shader = Shader.Find("Tunnel Crew/PerspectiveOverlay")
                ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            _lineMaterial = new Material(shader) { name = "3D line" };
            _lineMaterial.renderQueue = 3000;
            return _lineMaterial;
        }

        void ApplyTint(BillboardProxy proxy, Color tint)
        {
            _actorProperties ??= new MaterialPropertyBlock();
            proxy.Renderer.GetPropertyBlock(_actorProperties);
            _actorProperties.SetColor("_BaseColor", tint);
            _actorProperties.SetColor("_Color", tint);
            proxy.Renderer.SetPropertyBlock(_actorProperties);
        }

        BillboardProxy CreateProxy(string objectName, bool withSilhouette = false)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(_actors, false);
            SetLayer(go, PreviewLayer);
            var proxy = new BillboardProxy
            {
                GameObject = go,
                Transform = go.transform,
                Filter = go.AddComponent<MeshFilter>(),
                Renderer = go.AddComponent<MeshRenderer>(),
            };
            if (!withSilhouette) return proxy;

            var sil = new GameObject("Occluded silhouette");
            sil.transform.SetParent(go.transform, false);
            SetLayer(sil, PreviewLayer);
            proxy.SilhouetteFilter = sil.AddComponent<MeshFilter>();
            proxy.SilhouetteRenderer = sil.AddComponent<MeshRenderer>();
            proxy.SilhouetteRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            proxy.SilhouetteRenderer.receiveShadows = false;
            return proxy;
        }

        /// <summary>
        /// 액터가 카메라와의 사이에 있는 벽에 가리는가(§4 6단계). 깊이 버퍼 대신 격자를 직접 훑는다 —
        /// URP 2D 경로의 깊이 쓰기를 믿을 수 없기 때문이며, 판정이 시뮬레이션 격자와 항상 일치한다.
        /// </summary>
        bool IsOccluded(Vector2 ground, float height)
        {
            if (_worldRenderer == null || !_worldRenderer.Ready || _previewCamera == null) return false;

            // 가슴 높이에서 카메라로 향하는 시선 하나만 본다. 발끝까지 보이지 않아도 개체 위치는 읽힌다.
            var from = new Vector3(ground.x, height + 0.55f, ground.y);
            var to = _previewCamera.transform.position;
            float distance = Vector3.Distance(from, to);
            if (distance < 0.5f) return false;

            int steps = Mathf.Clamp(Mathf.CeilToInt(distance * 3f), 4, 96);
            for (int i = 1; i < steps; i++)
            {
                var p = Vector3.Lerp(from, to, i / (float)steps);
                float blocking = _worldRenderer.BlockingHeight(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z));
                if (blocking > 0f && p.y < blocking) return true;
            }
            return false;
        }

        /// <summary>벽에 가렸을 때 덧그리는 실루엣 재질. 알파만 쓰므로 스프라이트 아틀라스를 공유한다.</summary>
        Material SilhouetteMaterial(Texture texture)
        {
            if (_silhouetteMaterials.TryGetValue(texture, out var material) && material != null) return material;
            var shader = Shader.Find("Tunnel Crew/PerspectiveSilhouette");
            if (shader == null) return null;
            material = new Material(shader) { name = $"Silhouette · {texture.name}" };
            material.SetTexture("_MainTex", texture);
            _silhouetteMaterials[texture] = material;
            return material;
        }

        void RefreshActorSources()
        {
            var found = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
            var live = _liveSprites;
            live.Clear();
            foreach (var source in found)
            {
                if (_contractSources.Contains(source) || !IsEligibleActor(source)) continue;
                live.Add(source);
                if (_actorProxies.ContainsKey(source)) continue;
                _actorProxies.Add(source, CreateProxy($"Billboard · {source.name}"));
            }

            var stale = _staleSprites;
            stale.Clear();
            foreach (var pair in _actorProxies)
                if (pair.Key == null || !live.Contains(pair.Key)) stale.Add(pair.Key);
            foreach (var source in stale)
            {
                if (_actorProxies.TryGetValue(source, out var proxy) && proxy.GameObject != null)
                    Destroy(proxy.GameObject);
                _actorProxies.Remove(source);
            }
        }

        bool IsEligibleActor(SpriteRenderer source)
        {
            if (source == null || !source.enabled || !source.gameObject.activeInHierarchy || source.sprite == null || source == _sourcePlayer) return false;
            if (_previewCamera != null && source.transform.IsChildOf(_previewCamera.transform.parent)) return false;
            // 2D 전용 장치는 3D 로 옮기지 않는다 — 전경 실루엣은 벽 오클루전(6단계)이 대신한다.
            if (source.name is "Shadow" or "HpBg" or "HpBar") return false;
            if (source.name.StartsWith("Silhouette")) return false;
            string layer = source.sortingLayerName;
            return layer == VisualLayers.UnlayeredDefault
                || layer == VisualLayers.BackStructure
                || layer == VisualLayers.WallTop
                || layer == VisualLayers.WorldEntity
                || layer == VisualLayers.FrontStructure
                || layer == VisualLayers.WorldFX;
        }

        Mesh SpriteMesh(Sprite sprite)
        {
            if (_spriteMeshes.TryGetValue(sprite, out var mesh)) return mesh;
            mesh = BuildSpriteBillboardMesh(sprite);
            _spriteMeshes.Add(sprite, mesh);
            return mesh;
        }

        Material SpriteMaterial(Texture texture)
        {
            if (_spriteMaterials.TryGetValue(texture, out var material)) return material;
            material = CreateBillboardMaterial($"3D Billboard · {texture.name}");
            // _MainTex 가 실제 샘플링 대상이다. _BaseMap 만 넣으면 흰 사각형이 된다(2026-09-16 실측).
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            material.mainTexture = texture;
            _spriteMaterials.Add(texture, material);
            return material;
        }

        /// <summary>
        /// 액터 빌보드 재질. 월드 표면과 같은 광원 버퍼로 조명을 받아 캐릭터가 방 조명에 묻히지 않는다.
        /// 셰이더가 없으면 Unlit 으로 떨어져 최소한 형태는 보인다.
        /// </summary>
        Material CreateBillboardMaterial(string materialName = "3D Billboard")
        {
            var shader = Shader.Find("Tunnel Crew/PerspectiveBillboard")
                ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader) { name = materialName, color = Color.white };
            material.renderQueue = 2450;
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.EnableKeyword("_ALPHATEST_ON");
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 1f);
            if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", 0.35f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            return material;
        }

        static Mesh BuildSpriteBillboardMesh(Sprite sprite)
        {
            var mesh = new Mesh { name = $"3D billboard · {sprite.name}" };
            var size = sprite.bounds.size;
            float pivotX = sprite.pivot.x / Mathf.Max(1f, sprite.rect.width);
            float pivotY = sprite.pivot.y / Mathf.Max(1f, sprite.rect.height);
            float x0 = -size.x * pivotX, x1 = size.x * (1f - pivotX);
            float y0 = -size.y * pivotY, y1 = size.y * (1f - pivotY);
            mesh.vertices = new[]
            {
                new Vector3(x0, y0, 0f), new Vector3(x1, y0, 0f),
                new Vector3(x1, y1, 0f), new Vector3(x0, y1, 0f),
            };
            var r = sprite.textureRect;
            float tw = sprite.texture.width, th = sprite.texture.height;
            mesh.uv = new[]
            {
                new Vector2(r.xMin / tw, r.yMin / th), new Vector2(r.xMax / tw, r.yMin / th),
                new Vector2(r.xMax / tw, r.yMax / th), new Vector2(r.xMin / tw, r.yMax / th),
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }

        static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++) SetLayer(go.transform.GetChild(i).gameObject, layer);
        }

        void OnGUI()
        {
            if (!_visible || _target == null) return;
            // 메인 화면의 배경으로만 그려서 기존 HUD/FTUE IMGUI 가 위에 남도록 한다.
            GUI.depth = 1000;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            // 입력·라벨이 쓰는 PerspectiveViewport.ScreenRect 와 같은 사각형이어야 한다(Screen 좌표계는 y 가 위).
            var fit = PerspectiveViewport.FitRect(TargetWidth, TargetHeight, Screen.width, Screen.height);
            GUI.DrawTexture(new Rect(fit.x, Screen.height - fit.y - fit.height, fit.width, fit.height), _target,
                ScaleMode.StretchToFill, false);

            DrawWorldInfo();
        }

        /// <summary>
        /// 머리 위 월드 정보를 화면 해상도로 다시 그린다(§4 5단계). 저해상도 RT 를 확대한 위에
        /// 얹으므로 글자·바가 뭉개지지 않고, 개체가 벽에 가려도 정보는 남는다.
        /// </summary>
        void DrawWorldInfo()
        {
            var entries = PerspectiveWorldInfo.All;
            if (entries.Count == 0) return;
            // OnGUI 는 Layout/Repaint 로 여러 번 불린다. 그리기와 비우기는 Repaint 한 번에서만 한다.
            if (Event.current.type != EventType.Repaint) return;

            _infoStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                richText = false,
            };
            float uiScale = Mathf.Max(1f, Screen.height / 540f);

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (!PerspectiveViewport.TrySimToScreen(entry.Ground, entry.Height, out var screen)) continue;
                float x = screen.x, y = Screen.height - screen.y;

                if (entry.Kind == PerspectiveWorldInfo.Kind.Text)
                {
                    _infoStyle.fontSize = Mathf.RoundToInt(12f * uiScale * entry.Scale);
                    var content = new GUIContent(entry.Text);
                    var size = _infoStyle.CalcSize(content);
                    var rect = new Rect(x - size.x * 0.5f, y - size.y * 0.5f, size.x, size.y);

                    var shadow = _infoStyle.normal.textColor;
                    _infoStyle.normal.textColor = new Color(0f, 0f, 0f, entry.Color.a * 0.8f);
                    GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), content, _infoStyle);
                    _infoStyle.normal.textColor = entry.Color;
                    GUI.Label(rect, content, _infoStyle);
                    _infoStyle.normal.textColor = shadow;
                    continue;
                }

                // 바는 셀 폭을 화면 폭으로 환산한다 — 같은 거리의 개체끼리 같은 크기로 읽힌다.
                if (!PerspectiveViewport.TrySimToScreen(entry.Ground + new Vector2(entry.Width, 0f), entry.Height, out var edge))
                    continue;
                float width = Mathf.Abs(edge.x - screen.x);
                float height = Mathf.Max(2f, 3f * uiScale);
                var back = new Rect(x - width * 0.5f, y - height * 0.5f, width, height);

                var prevColor = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.6f * entry.Color.a);
                GUI.DrawTexture(new Rect(back.x - 1f, back.y - 1f, back.width + 2f, back.height + 2f), Texture2D.whiteTexture);
                GUI.color = entry.Color;
                GUI.DrawTexture(new Rect(back.x, back.y, back.width * entry.Fill, back.height), Texture2D.whiteTexture);
                GUI.color = prevColor;
            }

            PerspectiveWorldInfo.Clear();
        }

        void OnDestroy()
        {
            PerspectiveViewport.Clear();
            if (_gameplayCamera != null) _gameplayCamera.cullingMask = _gameplayMask;
            foreach (var mesh in _spriteMeshes.Values) if (mesh != null) Destroy(mesh);
            foreach (var material in _spriteMaterials.Values) if (material != null) Destroy(material);
            foreach (var material in _silhouetteMaterials.Values) if (material != null) Destroy(material);
            _silhouetteMaterials.Clear();
            if (_lineMaterial != null) Destroy(_lineMaterial);
            _actorProxies.Clear();
            _lineProxies.Clear();
            _contractProxies.Clear();
            _contractSources.Clear();
            _spriteMeshes.Clear();
            _spriteMaterials.Clear();
            if (_target != null)
            {
                _target.Release();
                Destroy(_target);
            }
        }

        sealed class BillboardProxy
        {
            public GameObject GameObject;
            public Transform Transform;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public Sprite Sprite;
            /// <summary>벽에 가렸을 때만 보이는 실루엣(§4 6단계). 액터 대역에만 붙는다.</summary>
            public MeshFilter SilhouetteFilter;
            public MeshRenderer SilhouetteRenderer;
        }
    }
}
