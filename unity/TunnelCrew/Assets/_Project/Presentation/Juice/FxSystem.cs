using System.Collections.Generic;
using TunnelCrew.Sim;
using UnityEngine;
using TunnelCrew.Presentation.Visual;
using UnityEngine.Rendering.Universal;

namespace TunnelCrew.Presentation
{
    /// <summary>
    /// 월드 파티클 — 원본 <c>J.chunks / J.burst / J.spikes / J.ring / J.flash / J.smoke / J.square / J.star</c>(4878~5127).
    /// 유니티 Shuriken 3개(돌덩이·불꽃·스파이크)와 절차 스프라이트(링·플래시·사각·별)로 옮겼다.
    ///
    /// 수치는 원본 DEMO 를 그대로 쓴다: 파괴 = 돌덩이 22(320px/s) · 불꽃 26(190) · 스파이크 · 링 · 플래시 ·
    /// 드릴 비트 = 스파이크 13 · 돌덩이 10(412) · 불꽃 14(327). 1셀 = 50px.
    /// </summary>
    public sealed class FxSystem : MonoBehaviour
    {
        public const float CharacterMuzzleFlashOffset = .16f;
        public const float LaserMuzzleFlashOffset = .24f;
        public int ActiveShapeCount => _shapes.Count;
        public int ActiveTransientLightCount => _transientLights.Count;
        public int ActiveCasingCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _groundDebris.Count; i++) if (_groundDebris[i].Casing) count++;
                return count;
            }
        }
        public int ActiveGroundDebrisCount => _groundDebris.Count;
        public int ActiveForgeSparkCount => _forgeSparks.Count;
        public int ActiveHeatMarkCount => _heatMarks.Count;
        const float Px = 1f / 50f;

        ParticleSystem _chunks, _burst, _spikes, _smoke;
        readonly List<Shape> _shapes = new List<Shape>();
        readonly Stack<SpriteRenderer> _shapePool = new Stack<SpriteRenderer>();
        readonly Stack<Shape> _shapeStatePool = new Stack<Shape>();
        readonly List<Material> _materials = new List<Material>();
        Sprite _ring, _dot, _square, _star, _casing;
        Material _forgeSparkMaterial, _forgeCoreMaterial;
        VisualOptionsController _visualOptions;
        bool _initialized;

        sealed class Shape { public SpriteRenderer Sr; public float Life, Decay, R0, R1; public Color Col; public bool Ring; }
        sealed class GroundDebris
        {
            public SpriteRenderer Sr;
            public Vector2 Ground, PlanarVelocity;
            public float Height, VerticalVelocity, Gravity, Age, Life, Spin, Scale;
            public int Bounces;
            public Color Color, HotColor;
            public bool Splat, Casing;
        }
        readonly List<GroundDebris> _groundDebris = new List<GroundDebris>(128);
        readonly Stack<GroundDebris> _groundDebrisPool = new Stack<GroundDebris>(128);
        sealed class ForgeSpark
        {
            public SpriteRenderer Core;
            public TrailRenderer Trail;
            public Vector2 Ground, PlanarVelocity;
            public float Height, VerticalVelocity, Gravity, Age, Life, Scale;
            public int Bounces;
        }
        readonly List<ForgeSpark> _forgeSparks = new List<ForgeSpark>(96);
        readonly Stack<ForgeSpark> _forgeSparkPool = new Stack<ForgeSpark>(96);
        sealed class HeatMark
        {
            public GameObject Root;
            public SpriteRenderer Rim, Hole;
            public float Age, CoolTime, Life, Radius;
        }
        readonly List<HeatMark> _heatMarks = new List<HeatMark>(48);
        readonly Stack<HeatMark> _heatMarkPool = new Stack<HeatMark>(48);
        sealed class TransientLight
        {
            public Light2D Light;
            public float Age, Duration, Radius, Peak, Seed;
        }
        readonly List<TransientLight> _transientLights = new List<TransientLight>(16);
        readonly Stack<TransientLight> _transientLightPool = new Stack<TransientLight>(16);

        // 타일 팔레트 — 원본 P.* 근사 (흙·돌·광물·기반암)
        static readonly Color[] PalDirt = { C("#8F7B5C"), C("#C9B79A"), C("#6B5A40") };
        static readonly Color[] PalStone = { C("#6E7896"), C("#A3ACC8"), C("#3E4460") };
        static readonly Color[] PalOre = { C("#FFD36E"), C("#FFF3D6"), C("#C79A6B") };
        static readonly Color[] PalGem = { C("#7FEBD0"), C("#FFFFFF"), C("#3BA88F") };
        static readonly Color[] PalCrys = { C("#C7A0FF"), C("#F2ECFF"), C("#6A4FA8") };
        static readonly Color[] PalRock = { C("#C8B8E8"), C("#F2ECFF"), C("#4A3A68") };
        static readonly Color[] PalDrill = { C("#E8C89A"), C("#C79A6B"), C("#FFF3D6") };
        static readonly Color[] PalBlood = { C("#FF557D"), C("#FFB0B8"), C("#3A0D1A") };
        static readonly Color[] PalDrillSpark = { C("#E8CBA6"), C("#FFF3D6") };
        static readonly Color[] PalPlayerHurt = { C("#FF6060"), C("#FFD0D0") };
        static readonly Color[] PalImpactStandard = { C("#FFCF67"), Color.white };
        static readonly Color[] PalImpactPierce = { C("#6EDFFF"), Color.white };
        static readonly Color[] PalImpactRicochet = { C("#B27AFF"), Color.white };
        static readonly Color[] PalImpactSupport = { C("#58F0BD"), Color.white };
        static readonly Color[] PalImpactExplosive = { C("#FF8D42"), C("#FFD36E"), Color.white };
        static readonly Color[] PalImpactRain = { C("#FF5533"), C("#FFD36E"), Color.white };
        static readonly Color[] PalImpactLaser = { C("#48FFE1"), C("#FFD36E"), Color.white };

        static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        void Awake()
        {
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            _visualOptions = FindFirstObjectByType<VisualOptionsController>();
            _dot = ProcSprites.Circle(24, 0.7f);
            _ring = ProcSprites.Ring(64, 0.11f);
            _square = ProcSprites.Square();
            _star = ProcSprites.Star(64);
            _casing = MakeCasingSprite();

            var sparkShader = Shader.Find("Tunnel Crew/Projectile-Trail");
            if (sparkShader != null && sparkShader.isSupported)
            {
                _forgeSparkMaterial = new Material(sparkShader)
                {
                    name = "Forge Sparks (shared)",
                    hideFlags = HideFlags.HideAndDontSave,
                };
                _materials.Add(_forgeSparkMaterial);
            }
            _forgeCoreMaterial = OverlayMaterials.Unlit("Forge Spark Core (shared)");
            if (_forgeCoreMaterial != null)
            {
                _forgeCoreMaterial.hideFlags = HideFlags.HideAndDontSave;
                _materials.Add(_forgeCoreMaterial);
            }

            // 돌덩이 — 중력·회전·감속 (원본 ch: vx*=.93, rot, life 1/1.45s)
            _chunks = MakeSystem("Chunks", 36, _square, gravity: 0.9f, drag: 2.5f, rotate: true, stretch: false, order2: 44);
            // 불꽃 — 작은 점, 중력 900px/s² (원본 p: vy+=900, life 1/1.7s)
            _burst = MakeSystem("Burst", 42, _dot, gravity: 1.8f, drag: 0f, rotate: false, stretch: false, order2: 45);
            // 스파이크 — 속도 방향으로 늘어난 선, 아주 짧다 (원본 sp: life 1/11s)
            _spikes = MakeSystem("Spikes", 43, _dot, gravity: 0f, drag: 0f, rotate: false, stretch: true, order2: 46);
            // 연기 — 커지며 사라진다
            _smoke = MakeSystem("Smoke", 30, _dot, gravity: -0.15f, drag: 4f, rotate: false, stretch: false, order2: 41);
        }

        ParticleSystem MakeSystem(string name, int order, Sprite sprite, float gravity, float drag, bool rotate, bool stretch, int order2)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            // 방출은 Emit() 로만 한다. 시스템은 항상 재생 상태여야 방출된 입자가 시뮬레이션된다.
            main.loop = true; main.playOnAwake = true;
            main.maxParticles = name == "Chunks" ? 420 : name == "Burst" ? 560 : name == "Spikes" ? 420 : name == "Casings" ? 96 : 240;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.startLifetime = 0.7f; main.startSpeed = 0f; main.startSize = 0.1f;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = false;
            if (drag > 0) { var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.drag = drag; lim.multiplyDragByParticleSize = false; }
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.55f), new GradientAlphaKey(0, 1) });
            col.color = g;
            if (rotate) { var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-9f, 9f); }
            var r = go.GetComponent<ParticleSystemRenderer>();
            var material = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
            _materials.Add(material);
            r.sharedMaterial = material;
            r.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            if (stretch) { r.velocityScale = 0.05f; r.lengthScale = 0.6f; }
            r.sortingOrder = order2;
            if (VisualLayers.Exists(VisualLayers.WorldFX)) r.sortingLayerName = VisualLayers.WorldFX;
            var tsa = ps.textureSheetAnimation; tsa.enabled = true; tsa.mode = ParticleSystemAnimationMode.Sprites; tsa.AddSprite(sprite);
            ps.Play();
            return ps;
        }

        static Sprite MakeCasingSprite()
        {
            const int w = 24, h = 10;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, name = "Procedural Casing" };
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float nx = (x + .5f) / w, ny = Mathf.Abs((y + .5f) / h - .5f) * 2f;
                float end = Mathf.Min(nx, 1f - nx) * w / h;
                float edge = Mathf.Min(1f - ny, end);
                float alpha = Mathf.SmoothStep(0f, .20f, edge);
                float gleam = .62f + .38f * Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(ny - .28f) * 2.8f), 3f);
                var col = Color.Lerp(new Color(.42f, .19f, .055f), new Color(1f, .72f, .20f), gleam);
                col.a = alpha;
                tex.SetPixel(x, y, col);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 50f);
        }

        static Color[] Pal(TileType t) => t switch
        {
            TileType.Stone => PalStone, TileType.Ore => PalOre, TileType.Gem => PalGem, TileType.Crys => PalCrys,
            TileType.Rock => PalRock, TileType.Core => PalRock, _ => PalDirt,
        };
        static bool IsOre(TileType t) => t == TileType.Ore || t == TileType.Gem || t == TileType.Crys;

        // ───────────────────────────── 원본 J.* 대응
        /// <summary>J.chunks(x,y,n,cols,sp,dx,dy) — 방향이 있으면 그쪽으로 ±1.0rad 부채꼴.</summary>
        public void Chunks(Vector2 at, int n, Color[] cols, float speedPx, Vector2 dir)
        {
            EnsureInitialized();
            bool bias = dir.sqrMagnitude > 1e-6f;
            float baseA = bias ? Mathf.Atan2(dir.y, dir.x) : 0f;
            for (int i = 0; i < n; i++)
            {
                float a = bias ? baseA + (Random.value - .5f) * 2.0f : Random.value * 6.283f;
                float s = speedPx * (.45f + Random.value * 1.05f) * Px;
                bool big = Random.value < .34f;
                var ep = new ParticleSystem.EmitParams
                {
                    position = at, velocity = new Vector3(Mathf.Cos(a) * s, Mathf.Sin(a) * s, 0),
                    startSize = (big ? 7 + Random.value * 7 : 3.4f + Random.value * 4.2f) * Px * 1.1f,
                    startLifetime = 1f / 1.45f, startColor = cols[Random.Range(0, cols.Length)],
                    rotation = Random.value * 360f,
                };
                _chunks.Emit(ep, 1);
            }
        }

        /// <summary>J.burst(x,y,n,col,sp) — 사방 점 불꽃.</summary>
        public void Burst(Vector2 at, int n, Color[] cols, float speedPx)
        {
            EnsureInitialized();
            n = ScaledCount(n);
            for (int i = 0; i < n; i++)
            {
                float a = Random.value * 6.283f, s = speedPx * (.4f + Random.value * .9f) * Px;
                var ep = new ParticleSystem.EmitParams
                {
                    position = at, velocity = new Vector3(Mathf.Cos(a) * s, Mathf.Sin(a) * s + 40 * Px, 0),
                    startSize = (2 + Random.value * 4) * Px * 2f, startLifetime = 1f / 1.7f,
                    startColor = cols[i % cols.Length],
                };
                _burst.Emit(ep, 1);
            }
        }

        /// <summary>J.spikes(x,y,n,col,len,dx,dy) — 방향으로 뻗는 짧은 선.</summary>
        public void Spikes(Vector2 at, int n, Color col, float lenCells, Vector2 dir)
        {
            EnsureInitialized();
            float baseA = dir.sqrMagnitude > 1e-6f ? Mathf.Atan2(dir.y, dir.x) : Random.value * 6.283f;
            for (int i = 0; i < n; i++)
            {
                float a = baseA + (Random.value - .5f) * 1.6f;
                float s = lenCells * (6f + Random.value * 8f);
                var ep = new ParticleSystem.EmitParams
                {
                    position = at, velocity = new Vector3(Mathf.Cos(a) * s, Mathf.Sin(a) * s, 0),
                    startSize = 0.06f, startLifetime = 1f / 11f, startColor = col,
                };
                _spikes.Emit(ep, 1);
            }
        }

        /// <summary>J.smoke — 커지며 사라지는 연기.</summary>
        public void Smoke(Vector2 at, int n, Color col, float speedPx, float life = 1f / 2.2f)
        {
            EnsureInitialized();
            n = ScaledCount(n);
            for (int i = 0; i < n; i++)
            {
                float a = Random.value * 6.283f, s = speedPx * (.3f + Random.value * .8f) * Px;
                var c = col; c.a = .38f;
                var ep = new ParticleSystem.EmitParams
                {
                    position = at + new Vector2((Random.value - .5f) * .4f, .15f), velocity = new Vector3(Mathf.Cos(a) * s, Mathf.Sin(a) * s + .2f, 0),
                    startSize = (9 + Random.value * 13) * Px * 2.4f, startLifetime = life, startColor = c,
                };
                _smoke.Emit(ep, 1);
            }
        }

        /// <summary>J.ring(x,y,col,r0,r1,w) — 커지며 사라지는 링. r 은 셀.</summary>
        public void Ring(Vector2 at, Color col, float r0, float r1, float decay = 3.6f) => AddShape(_ring, at, col, r0, r1, decay, true);
        /// <summary>J.flash(x,y,r,col) — 순간 원.</summary>
        public void Flash(Vector2 at, float r, Color col) => AddShape(_dot, at, col, r, r * 1.15f, 9f, false);
        /// <summary>J.square — 회전 사각 링 근사.</summary>
        public void Square(Vector2 at, Color col, float r0, float r1) => AddShape(_square, at, col, r0, r1, 4.4f, true);
        /// <summary>J.star — 4각 별.</summary>
        public void Star(Vector2 at, Color col, float r) => AddShape(_star, at, col, r * .3f, r, 5.2f, false);

        void AddShape(Sprite sp, Vector2 at, Color col, float r0, float r1, float decay, bool ring)
        {
            EnsureInitialized();
            if (sp == null) sp = ring ? _ring : _dot;
            Shape state;
            if (_shapeStatePool.Count > 0) state = _shapeStatePool.Pop();
            else if (_shapes.Count >= 96) { state = _shapes[0]; _shapes.RemoveAt(0); }
            else state = new Shape();
            var sr = state.Sr != null ? state.Sr : _shapePool.Count > 0 ? _shapePool.Pop() : NewShapeRenderer();
            sr.gameObject.SetActive(true);
            sr.sprite = sp; sr.color = col;
            sr.transform.position = new Vector3(at.x, at.y, -0.02f);
            sr.transform.rotation = Quaternion.Euler(0, 0, sp == _square ? Random.value * 90f : 0f);
            state.Sr = sr; state.Life = 1; state.Decay = decay; state.R0 = r0; state.R1 = r1; state.Col = col; state.Ring = ring;
            _shapes.Add(state);
        }
        SpriteRenderer NewShapeRenderer()
        {
            var go = new GameObject("shape"); go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sortingOrder = 47;
            return sr;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = _shapes.Count - 1; i >= 0; i--)
            {
                var s = _shapes[i];
                s.Life -= dt * s.Decay;
                if (s.Life <= 0) { s.Sr.gameObject.SetActive(false); _shapes.RemoveAt(i); _shapeStatePool.Push(s); continue; }
                float u = 1 - s.Life;
                float r = s.Ring ? Mathf.Lerp(s.R0, s.R1, 1 - (1 - u) * (1 - u)) : Mathf.Lerp(s.R0, s.R1, u);
                s.Sr.transform.localScale = Vector3.one * (r * 2f);
                var c = s.Col; c.a *= s.Ring ? s.Life : Mathf.Min(1, s.Life * 1.4f);
                s.Sr.color = c;
            }

            for (int i = _transientLights.Count - 1; i >= 0; i--)
            {
                var pulse = _transientLights[i];
                pulse.Age += dt;
                float t = Mathf.Clamp01(pulse.Age / pulse.Duration);
                float attack = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / .10f));
                float decay = (1f - t) * (1f - t);
                float flutter = .91f + .09f * Mathf.Sin(pulse.Seed + pulse.Age * 92f);
                pulse.Light.intensity = pulse.Peak * attack * decay * flutter;
                pulse.Light.pointLightInnerRadius = pulse.Radius * Mathf.Lerp(.08f, .20f, t);
                pulse.Light.pointLightOuterRadius = pulse.Radius * Mathf.Lerp(.58f, 1f, Mathf.Sqrt(t));
                if (t < 1f) continue;
                pulse.Light.gameObject.SetActive(false);
                _transientLights.RemoveAt(i);
                _transientLightPool.Push(pulse);
            }

            // 피격 직후 붉게 달아오른 테두리가 최대 2초 동안 어두운 탄흔으로 식는다.
            for (int i = _heatMarks.Count - 1; i >= 0; i--)
            {
                var mark = _heatMarks[i];
                mark.Age += dt;
                if (mark.Age >= mark.Life)
                {
                    mark.Root.SetActive(false);
                    _heatMarks.RemoveAt(i);
                    _heatMarkPool.Push(mark);
                    continue;
                }

                float coolT = Mathf.Clamp01(mark.Age / mark.CoolTime);
                float heat = Mathf.Pow(1f - coolT, 1.45f);
                float fade = mark.Age <= mark.CoolTime
                    ? 1f
                    : 1f - Mathf.SmoothStep(mark.CoolTime, mark.Life, mark.Age);
                var rim = Color.Lerp(new Color(.085f, .055f, .045f, .72f), new Color(1f, .09f, .012f, .96f), heat);
                var hole = Color.Lerp(new Color(.012f, .010f, .009f, .92f), new Color(.50f, .018f, .004f, .96f), heat);
                rim.a *= fade;
                hole.a *= fade;
                mark.Rim.color = rim;
                mark.Hole.color = hole;
                float settle = Mathf.Lerp(1.08f, 1f, 1f - heat);
                mark.Root.transform.localScale = new Vector3(mark.Radius * 2f * settle, mark.Radius * 1.30f * settle, 1f);
            }

            // 파편은 화면 Y로 단순 낙하시키지 않는다. 바닥 평면 이동과 높이를 따로 적분한 뒤
            // 마지막에 합쳐, 솟구침 → 바닥 충돌 → 짧은 미끄러짐이 분명하게 읽히게 한다.
            for (int i = _groundDebris.Count - 1; i >= 0; i--)
            {
                var d = _groundDebris[i];
                d.Age += dt;
                d.Ground += d.PlanarVelocity * dt;
                if (d.Height > 0f || d.VerticalVelocity > 0f)
                {
                    d.Height += d.VerticalVelocity * dt;
                    d.VerticalVelocity -= d.Gravity * dt;
                    if (d.Height <= 0f)
                    {
                        d.Height = 0f;
                        if (d.Bounces > 0 && Mathf.Abs(d.VerticalVelocity) > .55f)
                        {
                            d.VerticalVelocity = -d.VerticalVelocity * Random.Range(.24f, .39f);
                            d.PlanarVelocity *= Random.Range(d.Casing ? .72f : .62f, d.Casing ? .91f : .78f);
                            if (d.Casing)
                            {
                                float scatter = Random.Range(-.58f, .58f);
                                float cs = Mathf.Cos(scatter), sn = Mathf.Sin(scatter);
                                d.PlanarVelocity = new Vector2(
                                    d.PlanarVelocity.x * cs - d.PlanarVelocity.y * sn,
                                    d.PlanarVelocity.x * sn + d.PlanarVelocity.y * cs);
                                d.Spin = -d.Spin * Random.Range(.58f, .92f);
                            }
                            d.Bounces--;
                        }
                        else d.VerticalVelocity = 0f;
                    }
                }
                else d.PlanarVelocity = Vector2.MoveTowards(d.PlanarVelocity, Vector2.zero,
                    dt * (d.Splat ? 1.4f : d.Casing ? 1.65f : 3.8f));

                float remain = 1f - d.Age / d.Life;
                if (remain <= 0f)
                {
                    d.Sr.gameObject.SetActive(false);
                    _groundDebris.RemoveAt(i);
                    _groundDebrisPool.Push(d);
                    continue;
                }

                d.Sr.transform.position = new Vector3(d.Ground.x, d.Ground.y + d.Height, -.035f);
                d.Sr.transform.Rotate(0f, 0f, d.Spin * dt);
                if (d.Height <= 0f) d.Spin = Mathf.MoveTowards(d.Spin, 0f, dt * (d.Casing ? 115f : 420f));
                float shadowSquash = d.Splat ? Mathf.Lerp(.22f, .38f, IsometricProjection.ShadowSquash) : 1f;
                float tumble = d.Casing ? Mathf.Lerp(.30f, 1f, Mathf.Abs(Mathf.Sin(d.Age * 19f + d.Spin * .013f))) : 1f;
                d.Sr.transform.localScale = new Vector3(d.Scale * (d.Splat ? 1.65f : 1f), d.Scale * shadowSquash * tumble, 1f);
                float heat = d.Casing ? Mathf.Pow(1f - Mathf.Clamp01(d.Age / .95f), 2.2f) : 0f;
                var c = d.Casing ? Color.Lerp(d.Color, d.HotColor, heat) : d.Color;
                c.a *= Mathf.Clamp01(remain * (d.Splat ? 2.2f : 3.3f));
                d.Sr.color = c;
            }

            for (int i = _forgeSparks.Count - 1; i >= 0; i--)
            {
                var s = _forgeSparks[i];
                s.Age += dt;
                s.Ground += s.PlanarVelocity * dt;
                s.Height += s.VerticalVelocity * dt;
                s.VerticalVelocity -= s.Gravity * dt;
                if (s.Height <= 0f)
                {
                    s.Height = 0f;
                    if (s.Bounces-- > 0 && Mathf.Abs(s.VerticalVelocity) > .24f)
                    {
                        s.VerticalVelocity = -s.VerticalVelocity * Random.Range(.38f, .52f);
                        s.PlanarVelocity *= Random.Range(.62f, .78f);
                        float scatter = Random.Range(-.24f, .24f);
                        float cs = Mathf.Cos(scatter), sn = Mathf.Sin(scatter);
                        s.PlanarVelocity = new Vector2(
                            s.PlanarVelocity.x * cs - s.PlanarVelocity.y * sn,
                            s.PlanarVelocity.x * sn + s.PlanarVelocity.y * cs);
                    }
                    else s.VerticalVelocity = 0f;
                }

                float remain = 1f - s.Age / s.Life;
                if (remain <= 0f)
                {
                    s.Trail.emitting = false;
                    s.Trail.Clear();
                    s.Core.gameObject.SetActive(false);
                    _forgeSparks.RemoveAt(i);
                    _forgeSparkPool.Push(s);
                    continue;
                }

                s.Core.transform.position = new Vector3(s.Ground.x, s.Ground.y + s.Height, -.055f);
                float hot = Mathf.Clamp01(remain * 1.7f);
                var color = Color.Lerp(new Color(.82f, .055f, .008f, 1f), new Color(1f, .62f, .16f, 1f), hot * hot);
                s.Core.color = color;
                s.Core.transform.localScale = Vector3.one * s.Scale * Mathf.Lerp(.55f, 1f, remain);
                s.Trail.startColor = color;
                s.Trail.endColor = new Color(.48f, .012f, .002f, 0f);
                s.Trail.widthMultiplier = s.Scale * Mathf.Lerp(.09f, .14f, hot);
            }
        }

        /// <summary>대장간 쇠불꽃처럼 밝은 코어와 긴 가산 잔광을 남기며 바닥에 한두 번 튄다.</summary>
        void ForgeSparks(Vector2 at, Vector2 reboundDir, int count, float power, bool hard)
        {
            EnsureInitialized();
            count = ScaledCount(count);
            reboundDir = reboundDir.sqrMagnitude > 1e-5f ? reboundDir.normalized : Vector2.up;
            float baseAngle = Mathf.Atan2(reboundDir.y, reboundDir.x);
            for (int i = 0; i < count; i++)
            {
                ForgeSpark s;
                if (_forgeSparkPool.Count > 0) s = _forgeSparkPool.Pop();
                else if (_forgeSparks.Count >= 96)
                {
                    s = _forgeSparks[0];
                    _forgeSparks.RemoveAt(0);
                    s.Trail.Clear();
                }
                else s = NewForgeSpark();

                float a = baseAngle + Random.Range(-1.28f, 1.28f);
                // 직전 상한에서 다시 80%를 줄여, 가장 강한 탄도 반 칸조차 벗어나지 않게 한다.
                float speed = Random.Range(hard ? .099f : .075f, hard ? .213f : .174f) * Mathf.Sqrt(power);
                s.Ground = at + Random.insideUnitCircle * .035f;
                s.PlanarVelocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed;
                s.Height = Random.Range(.01f, .045f);
                s.VerticalVelocity = Random.Range(.9f, hard ? 1.4f : 1.25f);
                s.Gravity = Random.Range(12f, 14f);
                s.Age = 0f;
                s.Life = Random.Range(.42f, hard ? .62f : .56f);
                s.Bounces = Random.Range(1, 3);
                s.Scale = Random.Range(.065f, .105f) * Mathf.Lerp(.9f, 1.18f, power - .55f);
                s.Core.sprite = _dot;
                s.Core.color = new Color(1f, .62f, .16f, 1f);
                // 풀에서 꺼낸 Trail이 이전 피격점과 새 피격점을 긴 선으로 잇지 않게
                // 방출을 끈 채 새 위치로 먼저 옮긴 다음 기록을 비우고 재개한다.
                s.Trail.emitting = false;
                s.Trail.Clear();
                s.Core.transform.position = new Vector3(s.Ground.x, s.Ground.y + s.Height, -.055f);
                s.Core.gameObject.SetActive(true);
                s.Trail.emitting = true;
                _forgeSparks.Add(s);
            }

        }

        ForgeSpark NewForgeSpark()
        {
            var go = new GameObject("forge-spark");
            go.transform.SetParent(transform, false);
            var core = go.AddComponent<SpriteRenderer>();
            core.sortingOrder = 49;
            if (VisualLayers.Exists(VisualLayers.WorldFX)) core.sortingLayerName = VisualLayers.WorldFX;
            if (_forgeCoreMaterial != null) core.sharedMaterial = _forgeCoreMaterial;
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = _forgeSparkMaterial;
            trail.time = .012f;
            trail.minVertexDistance = .025f;
            trail.numCornerVertices = 2;
            trail.numCapVertices = 2;
            trail.textureMode = LineTextureMode.Stretch;
            trail.sortingOrder = 48;
            if (VisualLayers.Exists(VisualLayers.WorldFX)) trail.sortingLayerName = VisualLayers.WorldFX;
            return new ForgeSpark { Core = core, Trail = trail };
        }

        void HeatImpactDecal(Vector2 at, float radius)
        {
            EnsureInitialized();
            HeatMark mark;
            if (_heatMarkPool.Count > 0) mark = _heatMarkPool.Pop();
            else if (_heatMarks.Count >= 48)
            {
                mark = _heatMarks[0];
                _heatMarks.RemoveAt(0);
            }
            else mark = NewHeatMark();

            mark.Age = 0f;
            mark.CoolTime = Random.Range(.90f, 2.0001f);
            mark.Life = mark.CoolTime + Random.Range(2f, 4.5f);
            mark.Radius = radius * Random.Range(.88f, 1.12f);
            mark.Root.transform.position = new Vector3(at.x, at.y, -.028f);
            mark.Root.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            mark.Root.transform.localScale = new Vector3(mark.Radius * 2.16f, mark.Radius * 1.40f, 1f);
            mark.Rim.color = new Color(1f, .09f, .012f, .96f);
            mark.Hole.color = new Color(.50f, .018f, .004f, .96f);
            mark.Root.SetActive(true);
            _heatMarks.Add(mark);
        }

        HeatMark NewHeatMark()
        {
            var root = new GameObject("impact-heat-decal");
            root.transform.SetParent(transform, false);
            var rim = root.AddComponent<SpriteRenderer>();
            rim.sprite = _ring;
            rim.sortingOrder = 42;
            if (VisualLayers.Exists(VisualLayers.WorldFX)) rim.sortingLayerName = VisualLayers.WorldFX;
            if (_forgeCoreMaterial != null) rim.sharedMaterial = _forgeCoreMaterial;

            var holeGo = new GameObject("bullet-hole");
            holeGo.transform.SetParent(root.transform, false);
            holeGo.transform.localScale = new Vector3(.43f, .43f, 1f);
            var hole = holeGo.AddComponent<SpriteRenderer>();
            hole.sprite = _dot;
            hole.sortingOrder = 43;
            if (VisualLayers.Exists(VisualLayers.WorldFX)) hole.sortingLayerName = VisualLayers.WorldFX;
            if (_forgeCoreMaterial != null) hole.sharedMaterial = _forgeCoreMaterial;
            return new HeatMark { Root = root, Rim = rim, Hole = hole };
        }

        /// <summary>
        /// 바닥 평면과 높이를 분리해 적분하는 착탄/사망 파편. 일부는 멀리 튀지만 대부분은 발생점 근처에 착지한다.
        /// </summary>
        void GroundDebrisBurst(Vector2 at, Vector2 reboundDir, int count, Color[] palette,
            float minSpeed, float maxSpeed, float minSize, float maxSize, bool body)
        {
            EnsureInitialized();
            count = ScaledCount(count);
            bool biased = reboundDir.sqrMagnitude > 1e-5f;
            float baseAngle = biased ? Mathf.Atan2(reboundDir.y, reboundDir.x) : 0f;
            for (int i = 0; i < count; i++)
            {
                GroundDebris d;
                if (_groundDebrisPool.Count > 0) d = _groundDebrisPool.Pop();
                else if (_groundDebris.Count >= 140)
                {
                    d = _groundDebris[0];
                    _groundDebris.RemoveAt(0);
                }
                else d = new GroundDebris();

                if (d.Sr == null) d.Sr = NewGroundDebrisRenderer();
                float a = biased ? baseAngle + Random.Range(-1.18f, 1.18f) : Random.value * Mathf.PI * 2f;
                float farMul = Random.value < (body ? .28f : .14f) ? Random.Range(1.55f, 2.25f) : 1f;
                float speed = Random.Range(minSpeed, maxSpeed) * farMul;
                d.Ground = at + new Vector2(Random.Range(-.08f, .08f), Random.Range(-.05f, .05f));
                d.PlanarVelocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed;
                d.Height = Random.Range(.02f, .12f);
                d.VerticalVelocity = Random.Range(body ? 1.8f : 1.15f, body ? 4.2f : 3.0f);
                d.Gravity = Random.Range(7.8f, 10.8f);
                d.Age = 0f;
                d.Life = Random.Range(body ? 1.55f : 1.05f, body ? 2.45f : 1.75f);
                d.Bounces = Random.Range(1, 3);
                d.Spin = Random.Range(-760f, 760f);
                d.Scale = Random.Range(minSize, maxSize);
                d.Color = palette[Random.Range(0, palette.Length)];
                d.HotColor = d.Color;
                d.Splat = false;
                d.Casing = false;
                d.Sr.sprite = _square;
                d.Sr.color = d.Color;
                d.Sr.sortingOrder = 44;
                d.Sr.gameObject.SetActive(true);
                _groundDebris.Add(d);
            }
        }

        void FloorFluidSplats(Vector2 at, int count)
        {
            count = ScaledCount(count);
            for (int i = 0; i < count; i++)
            {
                GroundDebris d;
                if (_groundDebrisPool.Count > 0) d = _groundDebrisPool.Pop();
                else if (_groundDebris.Count >= 140) { d = _groundDebris[0]; _groundDebris.RemoveAt(0); }
                else d = new GroundDebris();
                if (d.Sr == null) d.Sr = NewGroundDebrisRenderer();
                float a = Random.value * Mathf.PI * 2f;
                float radius = Random.Range(.18f, 1.15f);
                d.Ground = at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                d.PlanarVelocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(.08f, .38f);
                d.Height = d.VerticalVelocity = 0f;
                d.Gravity = 0f;
                d.Age = 0f;
                d.Life = Random.Range(1.5f, 2.6f);
                d.Bounces = 0;
                d.Spin = Random.Range(-35f, 35f);
                d.Scale = Random.Range(.13f, .34f);
                d.Color = Color.Lerp(PalBlood[0], PalBlood[2], Random.value * .75f);
                d.Color.a = Random.Range(.68f, .94f);
                d.HotColor = d.Color;
                d.Splat = true;
                d.Casing = false;
                d.Sr.sprite = _dot;
                d.Sr.color = d.Color;
                d.Sr.sortingOrder = 42;
                d.Sr.gameObject.SetActive(true);
                _groundDebris.Add(d);
            }
        }

        SpriteRenderer NewGroundDebrisRenderer()
        {
            var go = new GameObject("ground-debris");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 44;
            if (VisualLayers.Exists(VisualLayers.WorldFX)) sr.sortingLayerName = VisualLayers.WorldFX;
            return sr;
        }

        void PulseLight(Vector2 at, Color color, float radius, float peak, float duration)
        {
            EnsureInitialized();
            TransientLight pulse;
            if (_transientLightPool.Count > 0) pulse = _transientLightPool.Pop();
            else if (_transientLights.Count < 16) pulse = NewTransientLight(_transientLights.Count);
            else
            {
                pulse = _transientLights[0];
                _transientLights.RemoveAt(0);
            }
            pulse.Age = 0f;
            pulse.Duration = Mathf.Max(.04f, duration);
            pulse.Radius = radius;
            pulse.Peak = peak;
            pulse.Seed = Random.value * 31f;
            pulse.Light.transform.position = new Vector3(at.x, at.y, .05f);
            pulse.Light.color = color;
            pulse.Light.pointLightInnerAngle = pulse.Light.pointLightOuterAngle = 360f;
            pulse.Light.pointLightInnerRadius = radius * .08f;
            pulse.Light.pointLightOuterRadius = radius * .58f;
            pulse.Light.intensity = peak * .35f;
            pulse.Light.gameObject.SetActive(true);
            _transientLights.Add(pulse);
        }

        TransientLight NewTransientLight(int index)
        {
            var go = new GameObject("combat-light-" + index);
            go.transform.SetParent(transform, false);
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.targetSortingLayers = VisualLayers.LitWorldOnlyLayerIds();
            light.shadowIntensity = 0f;
            return new TransientLight { Light = light };
        }

        void EjectCasing(Vector2 origin, Vector2 dir, bool ai, string visualId)
        {
            if (visualId == "laser" || visualId == "support" || visualId == "shard") return;
            if (ai && Random.value > .35f) return;
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
            var side = new Vector2(-dir.y, dir.x);
            if (side.y < 0f) side = -side;
            GroundDebris d;
            if (_groundDebrisPool.Count > 0) d = _groundDebrisPool.Pop();
            else if (_groundDebris.Count >= 140) { d = _groundDebris[0]; _groundDebris.RemoveAt(0); }
            else d = new GroundDebris();
            if (d.Sr == null) d.Sr = NewGroundDebrisRenderer();

            float speed = Random.Range(.38f, .82f) * (ai ? .76f : 1f);
            d.Ground = origin - dir * .10f + side * .16f;
            d.PlanarVelocity = side * speed - dir * Random.Range(.15f, .72f)
                             + Random.insideUnitCircle * Random.Range(.04f, .16f);
            d.Height = Random.Range(.05f, .12f);
            d.VerticalVelocity = Random.Range(1.05f, 1.78f) * (ai ? .82f : 1f);
            d.Gravity = Random.Range(8.8f, 10.8f);
            d.Age = 0f;
            d.Life = Random.Range(2.4f, 3.5f);
            d.Bounces = Random.Range(2, 5);
            d.Spin = Random.Range(-760f, 760f);
            // 기존 Shuriken 탄피보다 화면상 약 2배 크게 잡아, 튀고 구르는 실루엣을 놓치지 않게 한다.
            d.Scale = Random.Range(.76f, .98f) * (ai ? .86f : 1f);
            d.Color = C("#D39A32");
            d.HotColor = new Color(2.05f, .20f, .018f, 1f);
            d.Splat = false;
            d.Casing = true;
            d.Sr.sprite = _casing;
            d.Sr.color = d.HotColor;
            d.Sr.sortingOrder = 47;
            d.Sr.gameObject.SetActive(true);
            _groundDebris.Add(d);

        }

        // ───────────────────────────── 게임 이벤트 → 원본 연출 조합

        /// <summary>원본 damage() 파괴 분기 (6088~6123).</summary>
        public void TileBroken(Vector2 at, TileType t, Vector2 hitDir)
        {
            var pal = Pal(t); bool ore = IsOre(t);
            Color oreCol = ore ? pal[0] : default;
            Flash(at, 1.2f * .5f, ore ? new Color(1, 1, 1, .95f) : new Color(1f, .91f, .77f, .85f));
            Ring(at, ore ? oreCol : new Color(1f, .88f, .71f, .95f), .16f, 1.0f);
            if (ore) Ring(at, new Color(1, 1, 1, .8f), .1f, .66f);
            Chunks(at, 22, pal, 320f, hitDir);
            Burst(at, 26, pal, 190f);
            Smoke(at, 4, ore ? C("#FFE9C9") : pal[2], 70f);
            Spikes(at, 8, ore ? Color.white : new Color(1f, .89f, .71f, .95f), .52f, hitDir);
            Square(at, ore ? new Color(1, 1, 1, .92f) : new Color(1f, .9f, .73f, .8f), .32f, .9f);
            Star(at, ore ? oreCol : C("#FFF3D6"), .62f);
        }

        /// <summary>원본 damage() 손상 분기 (6155~6161) — 매 틱은 아니고 드릴 비트마다 부른다.</summary>
        public void TileDamaged(Vector2 at, TileType t, Vector2 hitDir, bool ore)
        {
            var pal = Pal(t);
            Chunks(at - hitDir * .3f, 2, pal, 140f, hitDir);
            Spikes(at - hitDir * .24f, 3, new Color(1f, .89f, .71f, .8f), .2f, hitDir);
            if (ore) Burst(at, 2, new[] { pal[0], Color.white }, 90f);
        }

        /// <summary>원본 드릴 비트 (7233~7241) — 스파이크 13 · 돌덩이 10 · 불꽃 14.</summary>
        public void DrillBeat(Vector2 tip, Vector2 player, Vector2 n)
        {
            Spikes(tip - n * .30f, 13, new Color(1f, .91f, .75f, .9f), .30f, n);
            Chunks(player + n * (30 * Px), 10, PalDrill, 412f, n);
            Burst(player + n * (30 * Px), 14, PalDrillSpark, 327f);
        }

        /// <summary>암반 반동 (7257~7262).</summary>
        public void DrillBounce(Vector2 rock, Vector2 n)
        {
            Spikes(rock - n * .1f, 7, new Color(.75f, .69f, .9f, .95f), .28f, -n);
            Burst(rock, 6, PalRock, 110f);
        }

        /// <summary>적 피격 — 피 튀김 + 링.</summary>
        public void EnemyHit(Vector2 at, Vector2 dir, bool dead, bool big)
        {
            Burst(at, dead ? 34 : big ? 10 : 6, PalBlood, dead ? 285f : 160f);
            if (dead)
            {
                // 몸 파편은 공중 높이와 바닥 미끄러짐을 따로 계산한다. 체액은 납작한 바닥 얼룩으로 남아
                // 단순히 화면 아래로 떨어지는 파티클보다 사망 위치와 주변 공간을 또렷하게 보여 준다.
                GroundDebrisBurst(at, dir.sqrMagnitude > 1e-5f ? dir : Vector2.zero,
                    13, PalBlood, .75f, 2.75f, .11f, .28f, body: true);
                FloorFluidSplats(at, 11);
                Ring(at, C("#FF557D"), .2f, 1.45f);
                Smoke(at, 7, C("#3A0D1A"), 95f, .72f);
                Spikes(at, 12, C("#FFB0B8"), .62f, dir);
            }
            else Spikes(at, 4, C("#FFB0B8"), .25f, dir);
        }

        public void PlayerHurt(Vector2 at, Vector2 dir, int level)
        {
            Ring(at, new Color(1f, .3f, .3f, .8f), .3f, .9f + level * .3f);
            Burst(at, 6 + level * 4, PalPlayerHurt, 180f);
        }

        /// <summary>무기별 발사 순간. 탄체와 같은 색·실루엣을 사용해 총구에서 비행으로 시선이 이어진다.</summary>
        public void ProjectileMuzzle(Vector2 origin, Vector2 dir, string visualId, ProjectileStyleFlags flags, int count, bool ai)
        {
            EnsureInitialized();
            if (flags == ProjectileStyleFlags.None) flags = ProjectileSystem.InferStyleFlags(visualId);
            var p = ProjectileVfxProfiles.Compose(visualId, flags);
            float aiMul = ai ? .62f : 1f;
            Vector2 at = origin + dir.normalized * (visualId == "laser" ? LaserMuzzleFlashOffset : CharacterMuzzleFlashOffset);
            Flash(at, p.Width * 1.75f * aiMul, new Color(p.Body.r, p.Body.g, p.Body.b, .68f));
            Ring(at, new Color(p.Body.r, p.Body.g, p.Body.b, .38f * aiMul), .015f, Mathf.Lerp(.24f, .46f, p.LightPriority / 10f), 11f);
            PulseLight(at, Color.Lerp(p.Body, p.Core, .28f), p.LightRadius * (ai ? 1.28f : 1.85f),
                Mathf.Lerp(4.3f, 7.8f, p.LightPriority / 10f) * aiMul, visualId == "laser" ? .16f : .115f);
            EjectCasing(origin, dir, ai, visualId);

            switch (visualId)
            {
                case "multi":
                    Spikes(at, Mathf.Min(9, 3 + count), p.Accent, .32f, dir);
                    Burst(at, 4, PalImpactExplosive, 135f);
                    break;
                case "pierce":
                    Spikes(at, 5, Color.white, .55f, dir);
                    Ring(at, p.Body, .03f, .42f, 8.5f);
                    break;
                case "ricochet":
                    Square(at, p.Accent, .04f, .38f);
                    Spikes(at, 4, p.Core, .30f, dir);
                    break;
                case "explosive":
                case "rain":
                    Ring(at, p.Accent, .05f, .50f, 7f);
                    Star(at, p.Core, .36f);
                    Smoke(at - dir * .12f, 2, C("#4A2630"), 55f, .28f);
                    break;
                case "laser":
                    Star(at, Color.white, .52f);
                    Ring(at, p.Body, .05f, .66f, 9f);
                    Spikes(at, 7, p.Core, .66f, dir);
                    break;
                case "support":
                    Ring(at, p.Body, .03f, .28f, 9f);
                    break;
                case "shard":
                    Spikes(at, 4, p.Core, .40f, dir);
                    break;
                default:
                    Star(at, p.Core, .25f);
                    Spikes(at, 3, p.Accent, .24f, dir);
                    break;
            }
        }

        /// <summary>관통·도탄·벽·적·수명 종료를 구분한 착탄. 같은 탄의 머즐/비행 프로필과 색을 공유한다.</summary>
        public void ProjectileImpact(Vector2 at, Vector2 dir, string visualId, ProjectileImpactKind kind,
            ProjectileStyleFlags flags, bool terminal, bool exploded, bool killed, float power)
        {
            EnsureInitialized();
            if (flags == ProjectileStyleFlags.None) flags = ProjectileSystem.InferStyleFlags(visualId);
            var p = ProjectileVfxProfiles.Compose(visualId, flags);
            power = Mathf.Clamp(power, .55f, 1.65f);
            if (kind != ProjectileImpactKind.Expire)
            {
                float blastMul = exploded ? 1.65f : 1f;
                float hitPeak = Mathf.Lerp(4.0f, 7.2f, p.LightPriority / 10f) * power * blastMul;
                float hitRadius = p.LightRadius * (exploded ? 2.20f : 1.55f) * Mathf.Sqrt(power);
                PulseLight(at, Color.Lerp(p.Body, p.Core, .46f), hitRadius, hitPeak, exploded ? .25f : .15f);
            }
            if (kind == ProjectileImpactKind.Expire && !exploded)
            {
                Flash(at, p.Width * .70f, new Color(p.Body.r, p.Body.g, p.Body.b, .28f));
                if (visualId == "laser" || visualId == "support") Ring(at, p.Body, .02f, .24f, 10f);
                return;
            }

            if (exploded)
            {
                float radius = (visualId == "laser" ? 2.15f : visualId == "rain" ? 1.85f : 1.65f) * power;
                var palette = visualId == "laser" ? PalImpactLaser : visualId == "rain" ? PalImpactRain : PalImpactExplosive;
                Ring(at, p.Body, .14f, radius, 3.1f);
                Ring(at, p.Core, .06f, radius * .68f, 5.5f);
                Flash(at, radius * .43f, new Color(p.Core.r, p.Core.g, p.Core.b, .82f));
                Star(at, p.Accent, radius * .62f);
                Burst(at, visualId == "laser" ? 19 : 24, palette, 315f * power);
                Spikes(at, visualId == "laser" ? 12 : 9, p.Core, radius * .45f, dir);
                Smoke(at, visualId == "laser" ? 1 : 4, C("#432A42"), 90f * power);
                return;
            }

            bool hard = kind == ProjectileImpactKind.Bedrock;
            bool bounce = kind == ProjectileImpactKind.Ricochet;
            bool ground = kind == ProjectileImpactKind.Ground;
            int sparks = visualId == "pierce" || visualId == "laser" ? 9
                       : visualId == "support" ? 3 : visualId == "multi" ? 5 : 6;
            var palette2 = visualId == "pierce" || visualId == "laser" ? PalImpactPierce
                        : visualId == "ricochet" ? PalImpactRicochet
                        : visualId == "support" || visualId == "shard" ? PalImpactSupport : PalImpactStandard;
            if (kind == ProjectileImpactKind.Enemy)
                Burst(at, sparks, palette2, 175f * power);

            if (kind == ProjectileImpactKind.Wall || hard || bounce)
            {
                var stonePalette = hard ? PalRock : PalStone;
                // 벽 표면에서 사수 쪽으로 반발한 뒤 높이를 잃고 벽 가까운 바닥에 착지한다.
                GroundDebrisBurst(at, -dir, hard ? 11 : 8, stonePalette,
                    .38f, hard ? 2.45f : 1.95f, .055f, hard ? .18f : .145f, body: false);
                Smoke(at - dir.normalized * .06f, hard ? 5 : 3, hard ? C("#34394B") : C("#4A4640"),
                    hard ? 92f : 68f, hard ? .82f : .58f);
                ForgeSparks(at, -dir, hard ? 7 : 5, power, hard);
                HeatImpactDecal(at, hard ? .19f : .15f);
            }
            else if (ground)
            {
                // 조준점까지 열린 공간을 날아온 탄은 바닥 먼지와 낮은 파편으로 종착점을 표시한다.
                GroundDebrisBurst(at, -dir, 5, PalDirt, .22f, 1.05f, .045f, .11f, body: false);
                Smoke(at, 2, C("#61594E"), 45f, .42f);
                ForgeSparks(at, -dir, 4, power, hard: false);
                HeatImpactDecal(at, .13f);
                Ring(at, new Color(p.Body.r, p.Body.g, p.Body.b, .52f), .02f, .28f, 10f);
            }

            if (bounce)
            {
                Square(at, p.Body, .05f, .58f);
                Ring(at, p.Accent, .03f, .40f, 8f);
            }
            else if (kind == ProjectileImpactKind.Wall || hard || ground)
            {
                Flash(at, hard ? .34f : .24f, new Color(1f, .58f, .16f, .82f));
                Ring(at, new Color(p.Body.r, p.Body.g, p.Body.b, .55f), .025f, hard ? .58f : .38f, 9f);
            }
            else if (visualId == "pierce" || visualId == "laser")
            {
                Flash(at, .34f * power, new Color(p.Core.r, p.Core.g, p.Core.b, .80f));
                Ring(at, p.Body, .035f, terminal ? .72f : .46f, 8f);
                Spikes(at, terminal ? 9 : 5, p.Core, terminal ? .68f : .42f, dir);
            }
            else if (visualId == "support" || visualId == "shard")
            {
                Ring(at, p.Body, .025f, terminal ? .42f : .26f, 9f);
                if (visualId == "shard") Spikes(at, 5, p.Core, .38f, dir);
            }
            else
            {
                Star(at, p.Core, terminal ? .38f : .24f);
                Spikes(at, hard ? 7 : 4, p.Accent, hard ? .46f : .30f, -dir);
            }

            if (killed) Ring(at, p.Accent, .10f, .92f, 5.8f);
        }

        /// <summary>구 API 호환. 새 코드는 충돌 종류가 포함된 <see cref="ProjectileImpact"/>를 사용한다.</summary>
        public void ProjectileEnd(Vector2 at, string visualId, bool exploded)
            => ProjectileImpact(at, Vector2.zero, visualId, ProjectileImpactKind.Wall,
                ProjectileSystem.InferStyleFlags(visualId), true, exploded, false, 1f);

        int ScaledCount(int count)
        {
            float scale = VisualQualityRules.CombatParticleScale(_visualOptions != null ? _visualOptions.Tier : VisualQualityTier.High);
            return Mathf.Max(1, Mathf.RoundToInt(count * scale));
        }

        public void BossShotImpact(Vector2 at, float radius)
        {
            Ring(at, C("#FF557D"), radius * .3f, radius * 1.35f);
            Burst(at, 18, PalBlood, 230f);
        }

        public void BossWall(Vector2 at, bool hard)
        {
            Smoke(at, 3, hard ? C("#3A4462") : C("#4A4034"), 55f, 2f + Random.value * 2f);
            Chunks(at + new Vector2(0, .2f), 4, hard ? new[] { C("#6A7EB0"), C("#4A5578"), C("#2E3650") } : new[] { C("#C9B79A"), C("#8F7B5C"), C("#6B5A40") }, 165f, new Vector2(0, 1.4f));
            Ring(at, hard ? C("#8FA2E8") : C("#D8C49A"), .06f, .6f);
        }

        public void BigRing(Vector2 at, Color col, float r1) { Ring(at, col, .3f, r1, 2.4f); Burst(at, 30, new[] { col, C("#C7A0FF"), Color.white }, 300f); }

        void OnDestroy()
        {
            foreach (var material in _materials) if (material != null) Destroy(material);
        }
    }

    /// <summary>절차 생성 스프라이트 — 연출·그레이박스 공용.</summary>
    public static class ProcSprites
    {
        static Sprite _square;
        public static Sprite Square()
        {
            if (_square != null) return _square;
            var t = new Texture2D(2, 2); t.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white }); t.Apply();
            return _square = Sprite.Create(t, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2);
        }
        public static Sprite Circle(int size, float hardness)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float rad = size * 0.5f;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x + .5f - rad) * (x + .5f - rad) + (y + .5f - rad) * (y + .5f - rad)) / rad;
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(1f - Mathf.SmoothStep(hardness, 1f, d))));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
        public static Sprite Ring(int size, float width)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float rad = size * 0.5f;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x + .5f - rad) * (x + .5f - rad) + (y + .5f - rad) * (y + .5f - rad)) / rad;
                float a = 1f - Mathf.Clamp01(Mathf.Abs(d - (1f - width * .5f - .04f)) / (width * .5f));
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
        public static Sprite Star(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float c = size * 0.5f;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x + .5f - c) / c, dy = Mathf.Abs(y + .5f - c) / c;
                // 4각 별: |x|^0.5 + |y|^0.5 <= 1
                float v = Mathf.Sqrt(dx) + Mathf.Sqrt(dy);
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01((1.05f - v) * 6f)));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
