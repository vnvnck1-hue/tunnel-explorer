using System.Collections.Generic;
using TunnelCrew.Sim;
using UnityEngine;

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
        const float Px = 1f / 50f;

        ParticleSystem _chunks, _burst, _spikes, _smoke;
        readonly List<Shape> _shapes = new List<Shape>();
        readonly Stack<SpriteRenderer> _shapePool = new Stack<SpriteRenderer>();
        Sprite _ring, _dot, _square, _star;

        sealed class Shape { public SpriteRenderer Sr; public float Life, Decay, R0, R1; public Color Col; public bool Ring; }

        // 타일 팔레트 — 원본 P.* 근사 (흙·돌·광물·기반암)
        static readonly Color[] PalDirt = { C("#8F7B5C"), C("#C9B79A"), C("#6B5A40") };
        static readonly Color[] PalStone = { C("#6E7896"), C("#A3ACC8"), C("#3E4460") };
        static readonly Color[] PalOre = { C("#FFD36E"), C("#FFF3D6"), C("#C79A6B") };
        static readonly Color[] PalGem = { C("#7FEBD0"), C("#FFFFFF"), C("#3BA88F") };
        static readonly Color[] PalCrys = { C("#C7A0FF"), C("#F2ECFF"), C("#6A4FA8") };
        static readonly Color[] PalRock = { C("#C8B8E8"), C("#F2ECFF"), C("#4A3A68") };
        static readonly Color[] PalDrill = { C("#E8C89A"), C("#C79A6B"), C("#FFF3D6") };
        static readonly Color[] PalBlood = { C("#FF557D"), C("#FFB0B8"), C("#3A0D1A") };

        static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        void Awake()
        {
            _dot = ProcSprites.Circle(24, 0.7f);
            _ring = ProcSprites.Ring(64, 0.11f);
            _square = ProcSprites.Square();
            _star = ProcSprites.Star(64);

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
            main.loop = true; main.playOnAwake = true; main.maxParticles = 800;
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
            r.material = new Material(Shader.Find("Sprites/Default"));
            r.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            if (stretch) { r.velocityScale = 0.05f; r.lengthScale = 0.6f; }
            r.sortingOrder = order2;
            var tsa = ps.textureSheetAnimation; tsa.enabled = true; tsa.mode = ParticleSystemAnimationMode.Sprites; tsa.AddSprite(sprite);
            ps.Play();
            return ps;
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
            var sr = _shapePool.Count > 0 ? _shapePool.Pop() : NewShapeRenderer();
            sr.gameObject.SetActive(true);
            sr.sprite = sp; sr.color = col;
            sr.transform.position = new Vector3(at.x, at.y, -0.02f);
            sr.transform.rotation = Quaternion.Euler(0, 0, sp == _square ? Random.value * 90f : 0f);
            _shapes.Add(new Shape { Sr = sr, Life = 1, Decay = decay, R0 = r0, R1 = r1, Col = col, Ring = ring });
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
                if (s.Life <= 0) { s.Sr.gameObject.SetActive(false); _shapePool.Push(s.Sr); _shapes.RemoveAt(i); continue; }
                float u = 1 - s.Life;
                float r = s.Ring ? Mathf.Lerp(s.R0, s.R1, 1 - (1 - u) * (1 - u)) : Mathf.Lerp(s.R0, s.R1, u);
                s.Sr.transform.localScale = Vector3.one * (r * 2f);
                var c = s.Col; c.a *= s.Ring ? s.Life : Mathf.Min(1, s.Life * 1.4f);
                s.Sr.color = c;
            }
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
            Chunks(at, 22, ore ? new[] { oreCol, pal[2], pal[0] } : pal, 320f, hitDir);
            Burst(at, 26, new[] { pal[1], pal[2], new Color(1f, .92f, .8f, .7f) }, 190f);
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
            Burst(player + n * (30 * Px), 14, new[] { C("#E8CBA6"), C("#FFF3D6") }, 327f);
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
            Burst(at, dead ? 18 : big ? 10 : 6, PalBlood, dead ? 230f : 160f);
            if (dead) { Ring(at, C("#FF557D"), .2f, 1.2f); Smoke(at, 3, C("#3A0D1A"), 60f); }
            else Spikes(at, 4, C("#FFB0B8"), .25f, dir);
        }

        public void PlayerHurt(Vector2 at, Vector2 dir, int level)
        {
            Ring(at, new Color(1f, .3f, .3f, .8f), .3f, .9f + level * .3f);
            Burst(at, 6 + level * 4, new[] { C("#FF6060"), C("#FFD0D0") }, 180f);
        }

        public void ProjectileEnd(Vector2 at, bool exploded)
        {
            if (exploded) { Ring(at, C("#FF8D72"), .2f, 1.6f); Burst(at, 18, new[] { C("#FF8D72"), C("#FFD36E"), Color.white }, 260f); Smoke(at, 3, C("#4A3550"), 80f); }
            else Burst(at, 3, new[] { C("#FFEBB4"), Color.white }, 120f);
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
