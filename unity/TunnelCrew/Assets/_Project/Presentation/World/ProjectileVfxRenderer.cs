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
            public float Seed, LengthJitter, WidthJitter, TrailJitter;
            public int SeenStamp, LightStamp;
            public MaterialPropertyBlock AuraBlock, BodyBlock, CoreBlock, AccentBlock;
            public Gradient TrailGradient;
            public GradientColorKey[] TrailColors;
            public GradientAlphaKey[] TrailAlphas;
            public SmokeRibbon Smoke;
        }

        sealed class SmokeRibbon
        {
            public GameObject Root;
            public TrailRenderer Trail;
            public MaterialPropertyBlock Block;
            public float Remaining, Duration;
            public bool Lingering;
        }

        static readonly int SecondaryColorId = Shader.PropertyToID("_SecondaryColor");
        static readonly int GlowId = Shader.PropertyToID("_Glow");
        static readonly int ShapeId = Shader.PropertyToID("_Shape");
        static readonly int AccentModeId = Shader.PropertyToID("_AccentMode");
        static readonly int SeedId = Shader.PropertyToID("_Seed");
        static readonly int PulseId = Shader.PropertyToID("_Pulse");
        static readonly int FadeId = Shader.PropertyToID("_Fade");
        const int MaxSmokeRibbons = 112;
        /// <summary>탄환별 시드 길이 변주 범위. 기준 길이 대비 75~125%라 전체 차이는 최대 50%다.</summary>
        public const float MinSeededLengthScale = .75f;
        public const float MaxSeededLengthScale = 1.25f;

        readonly List<Node> _nodes = new List<Node>(96);
        readonly Stack<Node> _free = new Stack<Node>(96);
        readonly Dictionary<Projectile, Node> _active = new Dictionary<Projectile, Node>(96);
        readonly HashSet<Projectile> _visible = new HashSet<Projectile>();
        readonly List<Light2D> _lights = new List<Light2D>(12);
        readonly List<SmokeRibbon> _smokeRibbons = new List<SmokeRibbon>(MaxSmokeRibbons);
        readonly Stack<SmokeRibbon> _smokeFree = new Stack<SmokeRibbon>(MaxSmokeRibbons);
        Material _energyMaterial, _trailMaterial, _smokeMaterial;
        Sprite _square, _circle, _ring, _star;
        int _stamp;

        public int RendererCount => _nodes.Count;
        public int ActiveRendererCount => _active.Count;
        public int ActiveTrailCount { get; private set; }
        public int ActiveLightCount { get; private set; }
        public int ActiveSmokeTrailCount { get; private set; }
        public int SmokeRendererCount => _smokeRibbons.Count;
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
            var smoke = Shader.Find("Tunnel Crew/Projectile-Smoke-Trail");
            if (smoke != null && smoke.isSupported)
                _smokeMaterial = new Material(smoke) { name = "Projectile Smoke Trail (shared)", hideFlags = HideFlags.HideAndDontSave };
        }

        public void Render(IReadOnlyList<Projectile> projectiles, VisualQualityTier tier)
        {
            if (_energyMaterial == null) Initialize(_square);
            UpdateSmokeRibbons(Time.deltaTime);
            _stamp++;
            if (_stamp == int.MaxValue) { _stamp = 1; ResetStamps(); }

            int visualBudget = VisualQualityRules.ProjectileVisualBudget(tier);
            int count = Mathf.Min(visualBudget, projectiles != null ? projectiles.Count : 0);
            int start = (projectiles != null ? projectiles.Count : 0) - count;
            int trailBudget = Mathf.Min(count, VisualQualityRules.ProjectileTrailBudget(tier));
            int smokeBudget = Mathf.Min(count, VisualQualityRules.ProjectileSmokeTrailBudget(tier));
            ActiveTrailCount = 0;
            ActiveSmokeTrailCount = 0;

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
                bool smokeOn = i >= count - smokeBudget;
                RenderNode(node, trailOn, smokeOn, tier);
                if (trailOn) ActiveTrailCount++;
                if (node.Smoke != null) ActiveSmokeTrailCount++;
            }

            AssignLights(tier);
        }

        Node Rent(Projectile projectile)
        {
            var node = _free.Count > 0 ? _free.Pop() : NewNode();
            node.Projectile = projectile;
            uint visualSeed = projectile.VisualSeed != 0 ? projectile.VisualSeed : unchecked((uint)projectile.GetHashCode());
            node.Seed = Seed01(visualSeed) * 19.37f;
            node.LengthJitter = SeededLengthScale(visualSeed);
            node.WidthJitter = Mathf.Lerp(.92f, 1.10f, Seed01(visualSeed ^ 0x85EBCA6Bu));
            node.TrailJitter = Mathf.Lerp(.88f, 1.14f, Seed01(visualSeed ^ 0xC2B2AE35u));
            var pos = IsometricProjection.ToRender3(projectile.Position, -.015f);
            node.Root.transform.position = pos;
            node.Root.SetActive(true);
            node.Trail.Clear();
            var flags = projectile.VisualFlags == ProjectileStyleFlags.None ? ProjectileSystem.InferStyleFlags(projectile.VisualId) : projectile.VisualFlags;
            BindProfile(node, ProjectileVfxProfiles.Compose(projectile.VisualId, flags), flags);
            return node;
        }

        /// <summary>동일한 탄환 시드는 언제나 동일한 길이 배율을 돌려준다.</summary>
        public static float SeededLengthScale(uint visualSeed) =>
            Mathf.Lerp(MinSeededLengthScale, MaxSeededLengthScale, Seed01(visualSeed ^ 0x9E3779B9u));

        static float Seed01(uint seed)
        {
            seed ^= seed >> 16;
            seed *= 0x7FEB352Du;
            seed ^= seed >> 15;
            seed *= 0x846CA68Bu;
            seed ^= seed >> 16;
            return (seed & 0x00FFFFFFu) / 16777215f;
        }

        void Release(Node node)
        {
            if (node.Projectile != null) _active.Remove(node.Projectile);
            node.Projectile = null;
            node.Trail.emitting = false;
            node.Trail.Clear();
            RetireSmoke(node);
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
                node.Trail.time = profile.TrailTime * node.TrailJitter;
                node.Trail.startWidth = profile.TrailWidth * node.WidthJitter;
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

            node.Trail.time = profile.TrailTime * node.TrailJitter;
            node.Trail.startWidth = profile.TrailWidth * node.WidthJitter;
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

        void RenderNode(Node node, bool trailOn, bool smokeOn, VisualQualityTier tier)
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
            float length = profile.Length * node.LengthJitter;
            float width = profile.Width * node.WidthJitter;
            node.Aura.transform.localScale = new Vector3(length * 1.34f * pulse, width * 1.72f * pulse, 1);
            node.Body.transform.localPosition = Vector3.zero;
            node.Body.transform.localRotation = Quaternion.identity;
            node.Body.transform.localScale = new Vector3(length * pulse, width / pulse, 1);
            node.Core.transform.localPosition = new Vector3(length * .09f, 0, 0);
            node.Core.transform.localRotation = Quaternion.identity;
            node.Core.transform.localScale = new Vector3(length * .64f, width * .42f, 1);

            float orbit = Time.time * profile.Spin + node.Seed * 37f;
            node.Accent.transform.localRotation = Quaternion.Euler(0, 0, orbit);
            node.Accent.transform.localPosition = profile.AccentMode == 6 ? new Vector3(length * .43f, 0, 0) : Vector3.zero;
            float accentSize = profile.AccentMode == 4 || profile.AccentMode == 5 ? width * 1.55f
                             : profile.AccentMode == 7 ? width * 1.15f
                             : profile.AccentMode == 6 ? width * 1.45f : width * .72f;
            node.Accent.transform.localScale = Vector3.one * accentSize * pulse;
            var accent = profile.Accent;
            accent.a = profile.AccentMode == 0 ? .42f : .82f;
            node.Accent.color = accent;

            node.Trail.emitting = trailOn && _trailMaterial != null;
            if (smokeOn && _smokeMaterial != null)
            {
                EnsureSmoke(node);
                if (node.Smoke != null)
                {
                    node.Smoke.Root.transform.position = node.Root.transform.position + new Vector3(0, 0, .012f);
                    node.Smoke.Block.SetFloat(FadeId, 1f);
                    node.Smoke.Trail.SetPropertyBlock(node.Smoke.Block);
                }
            }
            else RetireSmoke(node);
        }

        void EnsureSmoke(Node node)
        {
            if (node.Smoke != null) return;
            var smoke = RentSmoke();
            if (smoke == null) return;
            smoke.Root.SetActive(true);
            smoke.Root.transform.position = node.Root.transform.position + new Vector3(0, 0, .012f);
            smoke.Trail.Clear();
            smoke.Trail.emitting = true;
            smoke.Lingering = false;
            smoke.Remaining = smoke.Duration = 0f;
            smoke.Block.SetFloat(FadeId, 1f);
            smoke.Trail.SetPropertyBlock(smoke.Block);
            node.Smoke = smoke;
        }

        void RetireSmoke(Node node)
        {
            var smoke = node.Smoke;
            if (smoke == null) return;
            smoke.Trail.emitting = false;
            smoke.Lingering = true;
            smoke.Duration = smoke.Remaining = smoke.Trail.time + .34f;
            node.Smoke = null;
        }

        SmokeRibbon RentSmoke()
        {
            if (_smokeFree.Count > 0) return _smokeFree.Pop();
            if (_smokeRibbons.Count < MaxSmokeRibbons) return NewSmokeRibbon();

            SmokeRibbon oldest = null;
            for (int i = 0; i < _smokeRibbons.Count; i++)
            {
                var candidate = _smokeRibbons[i];
                if (!candidate.Lingering) continue;
                if (oldest == null || candidate.Remaining < oldest.Remaining) oldest = candidate;
            }
            if (oldest != null)
            {
                oldest.Trail.Clear();
                oldest.Lingering = false;
            }
            return oldest;
        }

        SmokeRibbon NewSmokeRibbon()
        {
            var go = new GameObject("projectile-smoke-" + _smokeRibbons.Count);
            go.transform.SetParent(transform, false);
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = _smokeMaterial;
            trail.autodestruct = false;
            trail.emitting = false;
            trail.time = .82f;
            trail.startWidth = .10f;
            trail.endWidth = .36f;
            trail.minVertexDistance = .11f;
            trail.numCapVertices = 2;
            trail.numCornerVertices = 2;
            trail.textureMode = LineTextureMode.Tile;
            trail.alignment = LineAlignment.View;
            trail.sortingOrder = 36;
            if (VisualLayers.Exists(VisualLayers.WorldFX)) trail.sortingLayerName = VisualLayers.WorldFX;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(.40f, .36f, .42f), 0f), new GradientColorKey(new Color(.19f, .17f, .23f), 1f) },
                new[] { new GradientAlphaKey(.05f, 0f), new GradientAlphaKey(.19f, .18f), new GradientAlphaKey(.10f, .70f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            var smoke = new SmokeRibbon { Root = go, Trail = trail, Block = new MaterialPropertyBlock() };
            _smokeRibbons.Add(smoke);
            return smoke;
        }

        void UpdateSmokeRibbons(float dt)
        {
            for (int i = 0; i < _smokeRibbons.Count; i++)
            {
                var smoke = _smokeRibbons[i];
                if (!smoke.Lingering) continue;
                smoke.Remaining -= Mathf.Max(0f, dt);
                float fade = smoke.Duration > 0f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(smoke.Remaining / smoke.Duration)) : 0f;
                smoke.Block.SetFloat(FadeId, fade);
                smoke.Trail.SetPropertyBlock(smoke.Block);
                if (smoke.Remaining > 0f) continue;
                smoke.Trail.Clear();
                smoke.Root.SetActive(false);
                smoke.Lingering = false;
                _smokeFree.Push(smoke);
            }
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
                // 프로필 값은 탄 자체의 글로우 기준이라 노멀맵 환경에서는 주변광이 너무 약했다.
                // 실제 필드광은 별도 배율과 조금 넓은 반경을 써 바닥·벽의 방향성이 비행 중에도 읽히게 한다.
                light.intensity = best.Profile.LightIntensity * 3.4f;
                light.pointLightInnerRadius = best.Profile.LightRadius * .12f;
                light.pointLightOuterRadius = best.Profile.LightRadius * 1.18f;
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
            light.targetSortingLayers = VisualLayers.LitWorldOnlyLayerIds();
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
            if (_smokeMaterial != null) Destroy(_smokeMaterial);
        }
    }
}
