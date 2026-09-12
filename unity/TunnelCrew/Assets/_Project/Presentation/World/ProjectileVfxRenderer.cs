using System.Collections.Generic;
using TunnelCrew.Presentation.Visual;
using TunnelCrew.Sim;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TunnelCrew.Presentation
{
    /// <summary>
    /// 아군 투사체 전용 렌더러. 한 쿼드의 색 교체 대신 외곽광·에너지 본체·핫 코어·문양·궤적을
    /// 한 노드로 묶고 실제 Light2D 는 제한된 풀에서 가장 중요한 탄에만 빌려 준다.
    /// 시뮬레이션의 Projectile 객체를 키로 삼아 리스트 삭제 뒤에도 궤적 정체성이 유지된다.
    /// </summary>
    public sealed class ProjectileVfxRenderer : MonoBehaviour
    {
        sealed class Node
        {
            public Projectile Projectile;
            public GameObject Root;
            public SpriteRenderer Aura, Body, Core, Accent;
            public TrailRenderer Trail;
            public ProjectileVfxProfile Profile;
            public string ProfileId;
            public ProjectileStyleFlags StyleFlags;
            public float Seed;
            public int SeenStamp, LightStamp;
            public MaterialPropertyBlock AuraBlock, BodyBlock, CoreBlock, AccentBlock;
            public Gradient TrailGradient;
            public GradientColorKey[] TrailColors;
            public GradientAlphaKey[] TrailAlphas;
        }

        static readonly int SecondaryColorId = Shader.PropertyToID("_SecondaryColor");
        static readonly int GlowId = Shader.PropertyToID("_Glow");
        static readonly int ShapeId = Shader.PropertyToID("_Shape");
        static readonly int AccentModeId = Shader.PropertyToID("_AccentMode");
        static readonly int SeedId = Shader.PropertyToID("_Seed");
        static readonly int PulseId = Shader.PropertyToID("_Pulse");

        readonly List<Node> _nodes = new List<Node>(96);
        readonly Stack<Node> _free = new Stack<Node>(96);
        readonly Dictionary<Projectile, Node> _active = new Dictionary<Projectile, Node>(96);
        readonly HashSet<Projectile> _visible = new HashSet<Projectile>();
        readonly List<Light2D> _lights = new List<Light2D>(12);
        Material _energyMaterial, _trailMaterial;
        Sprite _square, _circle, _ring, _star;
        int _stamp;

        public int RendererCount => _nodes.Count;
        public int ActiveRendererCount => _active.Count;
        public int ActiveTrailCount { get; private set; }
        public int ActiveLightCount { get; private set; }
        public int SpriteLayerCount => _nodes.Count * 4;
        public Material SharedEnergyMaterial => _energyMaterial;
        public Material SharedTrailMaterial => _trailMaterial;

        public void Initialize(Sprite square)
        {
            if (_energyMaterial != null) return;
            _square = square != null ? square : ProcSprites.Square();
            _circle = ProcSprites.Circle(32, .45f);
            _ring = ProcSprites.Ring(64, .12f);
            _star = ProcSprites.Star(64);

            var energy = Shader.Find("Tunnel Crew/Projectile-Energy");
            if (energy != null && energy.isSupported)
                _energyMaterial = new Material(energy) { name = "Projectile Energy (shared)", hideFlags = HideFlags.HideAndDontSave };
            var trail = Shader.Find("Tunnel Crew/Projectile-Trail");
            if (trail != null && trail.isSupported)
                _trailMaterial = new Material(trail) { name = "Projectile Trail (shared)", hideFlags = HideFlags.HideAndDontSave };
        }

        public void Render(IReadOnlyList<Projectile> projectiles, VisualQualityTier tier)
        {
            if (_energyMaterial == null) Initialize(_square);
            _stamp++;
            if (_stamp == int.MaxValue) { _stamp = 1; ResetStamps(); }

            int visualBudget = VisualQualityRules.ProjectileVisualBudget(tier);
            int count = Mathf.Min(visualBudget, projectiles != null ? projectiles.Count : 0);
            int start = (projectiles != null ? projectiles.Count : 0) - count;
            int trailBudget = Mathf.Min(count, VisualQualityRules.ProjectileTrailBudget(tier));
            ActiveTrailCount = 0;

            _visible.Clear();
            for (int i = 0; i < count; i++) _visible.Add(projectiles[start + i]);
            // 반납을 먼저 해야 같은 프레임에 탄 목록이 전부 교체돼도 풀이 2배로 자라지 않는다.
            for (int i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                if (node.Projectile != null && !_visible.Contains(node.Projectile)) Release(node);
            }

            for (int i = 0; i < count; i++)
            {
                var projectile = projectiles[start + i];
                if (!_active.TryGetValue(projectile, out var node))
                {
                    node = Rent(projectile);
                    _active.Add(projectile, node);
                }
                node.SeenStamp = _stamp;
                bool trailOn = i >= count - trailBudget && node.Profile.TrailTime > 0f;
                RenderNode(node, trailOn, tier);
                if (trailOn) ActiveTrailCount++;
            }

            AssignLights(tier);
        }

        Node Rent(Projectile projectile)
        {
            var node = _free.Count > 0 ? _free.Pop() : NewNode();
            node.Projectile = projectile;
            node.Seed = ((projectile.GetHashCode() & 0xffff) / 65535f) * 19.37f;
            var pos = IsometricProjection.ToRender3(projectile.Position, -.015f);
            node.Root.transform.position = pos;
            node.Root.SetActive(true);
            node.Trail.Clear();
            var flags = projectile.VisualFlags == ProjectileStyleFlags.None ? ProjectileSystem.InferStyleFlags(projectile.VisualId) : projectile.VisualFlags;
            BindProfile(node, ProjectileVfxProfiles.Compose(projectile.VisualId, flags), flags);
            return node;
        }

        void Release(Node node)
        {
            if (node.Projectile != null) _active.Remove(node.Projectile);
            node.Projectile = null;
            node.Trail.emitting = false;
            node.Trail.Clear();
            node.Root.SetActive(false);
            _free.Push(node);
        }

        Node NewNode()
        {
            int index = _nodes.Count;
            var root = new GameObject("projectile-vfx-" + index);
            root.transform.SetParent(transform, false);
            var node = new Node
            {
                Root = root,
                Aura = AddSprite(root.transform, "aura", 38),
                Body = AddSprite(root.transform, "body", 40),
                Core = AddSprite(root.transform, "core", 41),
                Accent = AddSprite(root.transform, "accent", 42),
                Trail = root.AddComponent<TrailRenderer>(),
                AuraBlock = new MaterialPropertyBlock(),
                BodyBlock = new MaterialPropertyBlock(),
                CoreBlock = new MaterialPropertyBlock(),
                AccentBlock = new MaterialPropertyBlock(),
                TrailGradient = new Gradient(),
                TrailColors = new GradientColorKey[3],
                TrailAlphas = new GradientAlphaKey[3],
            };
            node.Aura.sharedMaterial = node.Body.sharedMaterial = node.Core.sharedMaterial = _energyMaterial;
            node.Trail.sharedMaterial = _trailMaterial;
            node.Trail.autodestruct = false;
            node.Trail.emitting = false;
            node.Trail.time = .1f;
            node.Trail.minVertexDistance = .09f;
            node.Trail.numCapVertices = 1;
            node.Trail.numCornerVertices = 1;
            node.Trail.textureMode = LineTextureMode.Stretch;
            node.Trail.alignment = LineAlignment.View;
            node.Trail.sortingOrder = 37;
            if (VisualLayers.Exists(VisualLayers.WorldFX))
            {
                node.Trail.sortingLayerName = VisualLayers.WorldFX;
                node.Aura.sortingLayerName = node.Body.sortingLayerName = node.Core.sortingLayerName = node.Accent.sortingLayerName = VisualLayers.WorldFX;
            }
            _nodes.Add(node);
            return node;
        }

        static SpriteRenderer AddSprite(Transform parent, string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = order;
            return renderer;
        }

        void BindProfile(Node node, ProjectileVfxProfile profile, ProjectileStyleFlags flags)
        {
            if (node.ProfileId == profile.Id && node.StyleFlags == flags)
            {
                SetShaderSeed(node.Aura, node.AuraBlock, node.Seed + .7f);
                SetShaderSeed(node.Body, node.BodyBlock, node.Seed);
                SetShaderSeed(node.Core, node.CoreBlock, node.Seed + 1.9f);
                SetShaderSeed(node.Accent, node.AccentBlock, node.Seed + 3.1f);
                return;
            }
            node.Profile = profile;
            node.ProfileId = profile.Id;
            node.StyleFlags = flags;

            ApplyShader(node.Aura, node.AuraBlock, profile, profile.Body, profile.Core, .62f, node.Seed + .7f);
            ApplyShader(node.Body, node.BodyBlock, profile, profile.Body, profile.Core, 1.85f, node.Seed);
            ApplyShader(node.Core, node.CoreBlock, profile, profile.Core, Color.white, 2.45f, node.Seed + 1.9f);

            node.Aura.sprite = node.Body.sprite = node.Core.sprite = _square;
            node.Accent.sharedMaterial = _energyMaterial;
            node.Accent.sprite = AccentSprite(profile.AccentMode);
            ApplyShader(node.Accent, node.AccentBlock, profile, profile.Accent, profile.Core, 1.35f, node.Seed + 3.1f);
            node.AccentBlock.SetFloat(ShapeId, profile.AccentMode == 2 || profile.AccentMode == 3 || profile.AccentMode == 8 ? 3f : 1f);
            node.Accent.SetPropertyBlock(node.AccentBlock);

            node.Trail.time = profile.TrailTime;
            node.Trail.startWidth = profile.TrailWidth;
            node.Trail.endWidth = 0f;
            node.Trail.minVertexDistance = Mathf.Clamp(profile.Length * .16f, .07f, .18f);
            node.TrailColors[0] = new GradientColorKey(profile.Core, 0f);
            node.TrailColors[1] = new GradientColorKey(profile.Trail, .30f);
            node.TrailColors[2] = new GradientColorKey(profile.Trail, 1f);
            node.TrailAlphas[0] = new GradientAlphaKey(.92f, 0f);
            node.TrailAlphas[1] = new GradientAlphaKey(.48f, .30f);
            node.TrailAlphas[2] = new GradientAlphaKey(0f, 1f);
            node.TrailGradient.SetKeys(node.TrailColors, node.TrailAlphas);
            node.Trail.colorGradient = node.TrailGradient;
        }

        static void ApplyShader(SpriteRenderer renderer, MaterialPropertyBlock block, ProjectileVfxProfile profile, Color color, Color core, float glow, float seed)
        {
            renderer.color = color;
            block.Clear();
            block.SetColor(SecondaryColorId, core);
            block.SetFloat(GlowId, glow);
            block.SetFloat(ShapeId, profile.Shape);
            block.SetFloat(AccentModeId, profile.AccentMode);
            block.SetFloat(SeedId, seed);
            block.SetFloat(PulseId, profile.Pulse);
            renderer.SetPropertyBlock(block);
        }

        static void SetShaderSeed(SpriteRenderer renderer, MaterialPropertyBlock block, float seed)
        {
            block.SetFloat(SeedId, seed);
            renderer.SetPropertyBlock(block);
        }

        Sprite AccentSprite(float mode)
        {
            if (mode == 4 || mode == 5 || mode == 7) return _ring;
            if (mode == 6) return _star;
            if (mode == 1) return _circle;
            return _square;
        }

        void RenderNode(Node node, bool trailOn, VisualQualityTier tier)
        {
            var p = node.Projectile;
            var flags = p.VisualFlags == ProjectileStyleFlags.None ? ProjectileSystem.InferStyleFlags(p.VisualId) : p.VisualFlags;
            var profile = ProjectileVfxProfiles.Compose(p.VisualId, flags);
            if (node.ProfileId != profile.Id || node.StyleFlags != flags) BindProfile(node, profile, flags);

            float angle = IsometricProjection.AngleToRender(p.Velocity.Angle) * Mathf.Rad2Deg;
            float pulse = 1f + Mathf.Sin(Time.time * profile.Pulse + node.Seed) * .055f;
            node.Root.transform.position = IsometricProjection.ToRender3(p.Position, -.015f);
            node.Root.transform.rotation = Quaternion.Euler(0, 0, angle);

            bool auraOn = tier >= VisualQualityTier.Medium;
            bool accentOn = tier >= VisualQualityTier.High || profile.LightPriority >= 7;
            if (node.Aura.gameObject.activeSelf != auraOn) node.Aura.gameObject.SetActive(auraOn);
            if (node.Accent.gameObject.activeSelf != accentOn) node.Accent.gameObject.SetActive(accentOn);

            node.Aura.transform.localPosition = Vector3.zero;
            node.Aura.transform.localRotation = Quaternion.identity;
            node.Aura.transform.localScale = new Vector3(profile.Length * 1.34f * pulse, profile.Width * 1.72f * pulse, 1);
            node.Body.transform.localPosition = Vector3.zero;
            node.Body.transform.localRotation = Quaternion.identity;
            node.Body.transform.localScale = new Vector3(profile.Length * pulse, profile.Width / pulse, 1);
            node.Core.transform.localPosition = new Vector3(profile.Length * .09f, 0, 0);
            node.Core.transform.localRotation = Quaternion.identity;
            node.Core.transform.localScale = new Vector3(profile.Length * .64f, profile.Width * .42f, 1);

            float orbit = Time.time * profile.Spin + node.Seed * 37f;
            node.Accent.transform.localRotation = Quaternion.Euler(0, 0, orbit);
            node.Accent.transform.localPosition = profile.AccentMode == 6 ? new Vector3(profile.Length * .43f, 0, 0) : Vector3.zero;
            float accentSize = profile.AccentMode == 4 || profile.AccentMode == 5 ? profile.Width * 1.55f
                             : profile.AccentMode == 7 ? profile.Width * 1.15f
                             : profile.AccentMode == 6 ? profile.Width * 1.45f : profile.Width * .72f;
            node.Accent.transform.localScale = Vector3.one * accentSize * pulse;
            var accent = profile.Accent;
            accent.a = profile.AccentMode == 0 ? .42f : .82f;
            node.Accent.color = accent;

            node.Trail.emitting = trailOn && _trailMaterial != null;
        }

        void AssignLights(VisualQualityTier tier)
        {
            int budget = Mathf.Min(VisualQualityRules.ProjectileLightBudget(tier), _active.Count);
            while (_lights.Count < budget) _lights.Add(NewLight(_lights.Count));
            ActiveLightCount = 0;

            for (int slot = 0; slot < budget; slot++)
            {
                Node best = null;
                for (int i = 0; i < _nodes.Count; i++)
                {
                    var candidate = _nodes[i];
                    if (candidate.Projectile == null || candidate.SeenStamp != _stamp || candidate.LightStamp == _stamp) continue;
                    if (best == null || candidate.Profile.LightPriority > best.Profile.LightPriority
                        || (candidate.Profile.LightPriority == best.Profile.LightPriority && candidate.Projectile.Age < best.Projectile.Age))
                        best = candidate;
                }
                if (best == null) break;
                best.LightStamp = _stamp;
                var light = _lights[slot];
                light.gameObject.SetActive(true);
                light.transform.position = best.Root.transform.position + new Vector3(0, 0, .05f);
                light.color = best.Profile.Body;
                light.intensity = best.Profile.LightIntensity;
                light.pointLightInnerRadius = best.Profile.LightRadius * .10f;
                light.pointLightOuterRadius = best.Profile.LightRadius;
                ActiveLightCount++;
            }
            for (int i = ActiveLightCount; i < _lights.Count; i++)
                if (_lights[i].gameObject.activeSelf) _lights[i].gameObject.SetActive(false);
        }

        Light2D NewLight(int index)
        {
            var go = new GameObject("projectile-light-" + index);
            go.transform.SetParent(transform, false);
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.targetSortingLayers = VisualLayers.LitGroundWorldOnlyLayerIds();
            light.intensity = .5f;
            light.pointLightInnerRadius = .08f;
            light.pointLightOuterRadius = 1.4f;
            return light;
        }

        void ResetStamps()
        {
            for (int i = 0; i < _nodes.Count; i++) { _nodes[i].SeenStamp = 0; _nodes[i].LightStamp = 0; }
        }

        void OnDestroy()
        {
            if (_energyMaterial != null) Destroy(_energyMaterial);
            if (_trailMaterial != null) Destroy(_trailMaterial);
        }
    }
}
