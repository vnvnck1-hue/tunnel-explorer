using System.Collections.Generic;
using TunnelCrew.Sim;
using UnityEngine;
using TunnelCrew.Presentation.Visual;

namespace TunnelCrew.Presentation
{
    /// <summary>
    /// 투사체 · 적탄 · 설치물 · 데미지 텍스트. M3 그레이박스라 절차 생성 스프라이트를 쓴다.
    /// 트레일·머즐·폭발 파티클은 Shuriken 프리셋이 들어올 때 이 클래스에서 이벤트로 연결한다.
    /// </summary>
    public sealed class CombatView : MonoBehaviour
    {
        public int ProjectileRendererCount => _projPool.Count;
        public int ActiveProjectileRendererCount
        {
            get { int n = 0; foreach (var r in _projPool) if (r != null && r.gameObject.activeSelf) n++; return n; }
        }
        public Material SharedProjectileMaterial => _projectileMaterial;
        readonly List<SpriteRenderer> _projPool = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _shotPool = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _installPool = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _bossShotPool = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _bossRingPool = new List<SpriteRenderer>();
        LineRenderer _dashLine;
        SpriteRenderer _escRange, _escSpot, _escPod;
        readonly List<SpriteRenderer> _auxPool = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _objectivePool = new List<SpriteRenderer>();
        readonly List<DamageText> _texts = new List<DamageText>();
        readonly Stack<DamageText> _textPool = new Stack<DamageText>();
        Sprite _dot, _square, _ring;
        Material _projectileMaterial;
        Material _dashMaterial;
        VisualOptionsController _visualOptions;

        struct ProjectileLook
        {
            public Color Color;
            public float Length, Width;
            public ProjectileLook(Color color, float length, float width) { Color = color; Length = length; Width = width; }
        }

        sealed class DamageText
        {
            public GameObject Go; public TextMesh Mesh, Shadow;
            public Vector2 Pos, Vel; public float Life, Rot, VRot, PopMul = 1, SzMul = 1;
            public bool Big, Label; public Color Col;
        }
        // 원본 DEMO 기본값: 크기 25/34px · 팝 1.45(감쇠 3.4/s) · 스쿼시 .22 · 페이드 2.2 · 수명 1.3/.56 · 상승 120px/s · 중력 980
        const float DmgBase = 25f, DmgBig = 34f, DmgPop = 1.45f, DmgPopDec = 3.4f, DmgSquash = .22f, DmgFade = 2.2f;
        const float DmgLifeRate = 1.3f / .56f, DmgRise = 120f, DmgGravity = 980f, DmgDamp = .97f, DmgJx = 12f, DmgJy = 9f, DmgDrift = 40f, Px = 1f / 50f;

        static readonly Dictionary<string, ProjectileLook> ProjectileLooks = new Dictionary<string, ProjectileLook>
        {
            ["standard"] = new ProjectileLook(new Color(1f, .74f, .28f, .92f), .48f, .16f),
            ["multi"] = new ProjectileLook(new Color(1f, .48f, .16f, .94f), .40f, .19f),
            ["pierce"] = new ProjectileLook(new Color(.22f, .82f, 1f, .94f), .82f, .12f),
            ["ricochet"] = new ProjectileLook(new Color(.68f, .38f, 1f, .94f), .50f, .18f),
            ["explosive"] = new ProjectileLook(new Color(1f, .22f, .06f, .96f), .62f, .28f),
            ["rain"] = new ProjectileLook(new Color(1f, .12f, .03f, .98f), .74f, .30f),
            ["laser"] = new ProjectileLook(new Color(.05f, 1f, .86f, .96f), 1.18f, .12f),
            ["support"] = new ProjectileLook(new Color(.20f, 1f, .62f, .88f), .38f, .17f),
            ["shard"] = new ProjectileLook(new Color(.30f, 1f, .90f, .92f), .56f, .11f),
        };

        void Awake()
        {
            _dot = MakeCircle(16); _square = MakeSquare(); _ring = ProcSprites.Ring(64, .12f);
            _visualOptions = FindFirstObjectByType<VisualOptionsController>();
            var shader = Shader.Find("Tunnel Crew/Projectile-Energy");
            if (shader != null && shader.isSupported)
                _projectileMaterial = new Material(shader) { name = "Projectile-Energy (shared)", hideFlags = HideFlags.HideAndDontSave };
        }

        void OnDestroy()
        {
            if (_projectileMaterial != null) Destroy(_projectileMaterial);
            if (_dashMaterial != null) Destroy(_dashMaterial);
        }

        public void Render(TunnelSim sim, float dt)
        {
            int budget = VisualQualityRules.ProjectileVisualBudget(_visualOptions != null ? _visualOptions.Tier : VisualQualityTier.High);
            int visibleProjectiles = Mathf.Min(budget, sim.Projectiles.Projectiles.Count);
            int projectileStart = sim.Projectiles.Projectiles.Count - visibleProjectiles;
            EnsurePool(_projPool, visibleProjectiles, 40, _projectileMaterial, transform);
            for (int i = 0; i < _projPool.Count; i++)
            {
                bool on = i < visibleProjectiles;
                var sr = _projPool[i];
                if (sr.gameObject.activeSelf != on) sr.gameObject.SetActive(on);
                if (!on) continue;
                var p = sim.Projectiles.Projectiles[projectileStart + i];
                sr.transform.position = IsometricProjection.ToRender3(p.Position);
                var look = ProjectileLooks.TryGetValue(p.VisualId, out var found) ? found : ProjectileLooks["standard"];
                float pulse = 1f + .08f * Mathf.Sin((float)(p.Age * 38.0 + i * 1.7));
                sr.color = look.Color;
                sr.transform.localScale = new Vector3(look.Length * pulse, look.Width / pulse, 1);
                sr.transform.rotation = Quaternion.Euler(0, 0, IsometricProjection.AngleToRender(p.Velocity.Angle) * Mathf.Rad2Deg);
                sr.sprite = _square;
            }

            int enemyShots = sim.Enemies.Shots.Count;
            PreparePool(_shotPool, enemyShots, 39);
            for (int i = 0; i < enemyShots; i++)
            {
                var s = sim.Enemies.Shots[i];
                var sr = _shotPool[i];
                sr.transform.position = IsometricProjection.ToRender3(s.Position);
                sr.color = new Color(0.55f, 0.95f, 0.5f);
                sr.transform.localScale = Vector3.one * (float)(s.Radius * 2.2);
                sr.sprite = _dot;
            }

            // 설치물: 노드(청록 원) · 센트리(보라 사각) · 파쇄탄(주황 점) · 플레어(노랑)
            var roles = sim.Roles;
            int n = roles.Nodes.Count + roles.Turrets.Count + roles.Breakers.Count + roles.Flares.Count;
            PreparePool(_installPool, n, 28);
            for (int i = 0; i < n; i++)
            {
                var sr = _installPool[i];
                int k = i;
                if (k < roles.Nodes.Count) { var o = roles.Nodes[k]; Set(sr, o.Position, _dot, new Color(0.45f, 0.92f, 0.85f), 0.7f); continue; }
                k -= roles.Nodes.Count;
                if (k < roles.Turrets.Count) { var o = roles.Turrets[k]; Set(sr, o.Position, _square, o.Powered ? new Color(0.78f, 0.63f, 1f) : new Color(0.4f, 0.35f, 0.5f), 0.55f); sr.transform.rotation = Quaternion.Euler(0, 0, IsometricProjection.AngleToRender(o.Aim) * Mathf.Rad2Deg); continue; }
                k -= roles.Turrets.Count;
                if (k < roles.Breakers.Count)
                {
                    var o = roles.Breakers[k];
                    float t = o.Stuck ? 1f : 1f - (float)(o.Travel / o.TravelMax);
                    var pos = o.Stuck ? o.Target : Vec2Lerp(o.Start, o.Target, t);
                    float pulse = o.Stuck ? 0.75f + 0.25f * Mathf.Sin(Time.time * 18f) : 1f;
                    Set(sr, pos, _dot, new Color(1f, 0.55f, 0.3f) * pulse, 0.3f); continue;
                }
                k -= roles.Breakers.Count;
                var f = roles.Flares[k];
                Set(sr, f.Position, _dot, new Color(1f, 0.92f, 0.5f, 0.85f), 0.28f);
            }

            // 보스 예고탄 — 착탄 원(커지며 붉어짐) + 포물선으로 날아가는 점
            var shots = sim.Bosses != null ? sim.Bosses.Shots : null;
            int ns = shots?.Count ?? 0;
            PreparePool(_bossRingPool, ns, 26);
            for (int i = 0; i < ns; i++)
            {
                var s = shots[i]; var sr = _bossRingPool[i];
                float p = (float)s.Progress;
                Set(sr, s.Target, _dot, new Color(1f, 0.33f, 0.49f, 0.18f + 0.42f * p), (float)s.Radius * 2f * (0.55f + 0.45f * p));
            }
            PreparePool(_bossShotPool, ns, 41);
            for (int i = 0; i < ns; i++)
            {
                var s = shots[i]; var sr = _bossShotPool[i];
                float p = (float)s.Progress;
                var pos = Vec2Lerp(s.Start, s.Target, p);
                float arc = Mathf.Sin(p * Mathf.PI) * 2.2f;   // 원본 arcHeight 110px ≈ 2.2셀
                var renderPos = IsometricProjection.ToRender(pos);
                sr.transform.position = new Vector3(renderPos.x, renderPos.y + arc, 0);
                sr.transform.rotation = Quaternion.identity;
                sr.sprite = _dot; sr.color = new Color(1f, 0.45f, 0.35f);
                sr.transform.localScale = Vector3.one * (0.34f + 0.16f * (float)s.Power);
            }

            // 보조 드릴 — 플레이어 주위를 도는 작은 드릴 비트 (원본 infDrawMiningTraits, 최대 8)
            int aux = Mathf.Min(8, sim.AuxDrillCount);
            PreparePool(_auxPool, aux, 33);
            for (int i = 0; i < aux; i++)
            {
                float a = Time.time * (1.6f + (i % 2) * .28f) + Mathf.PI * 2 * i / aux;
                float rad = .75f + (i % 2) * .24f;
                var pos = sim.Player.Position + new Vec2(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad);
                var sr = _auxPool[i];
                sr.transform.position = IsometricProjection.ToRender3(pos, -0.03f);
                sr.transform.rotation = Quaternion.Euler(0, 0, IsometricProjection.AngleToRender(a) * Mathf.Rad2Deg + 90f);
                sr.sprite = _square; sr.color = new Color(1f, .83f, .43f);
                sr.transform.localScale = new Vector3(.16f, .34f, 1f);
            }

            // 임무 표식 — 정확한 위치는 시야에 들어온 뒤에만 선명해진다. 미탐색 표식은 약한 신호로 남겨
            // 탐색 방향을 잃게 하지는 않되 지형 전체를 미리 공개하지 않는다.
            int objectives = sim.Objective?.Targets.Count ?? 0;
            PreparePool(_objectivePool, objectives, 24);
            for (int i = 0; i < objectives; i++)
            {
                int k = sim.Objective.Targets[i], c = k % sim.World.Cols, r = k / sim.World.Cols;
                bool done = sim.Objective.IsDone(k);
                bool seen = sim.Los == null || sim.Los.IsSoftSeen(c, r, 2);
                float pulse = .92f + .10f * Mathf.Sin(Time.time * 4.5f + i * 2.1f);
                float progress = sim.Objective.Id == ExpeditionObjectiveId.Survey ? (float)sim.Objective.SurveyProgress(k) : 0f;
                Color col = done ? new Color(.35f, .65f, .55f, .20f)
                          : sim.Objective.Id == ExpeditionObjectiveId.CoreRecovery ? new Color(.48f, 1f, .85f, seen ? .88f : .20f)
                          : new Color(1f, .83f, .32f, seen ? .82f : .18f);
                Set(_objectivePool[i], WorldGrid.CellCenter(c, r), _ring, col, (1.0f + progress * .28f) * pulse);
            }

            // 탈출 포트 — 지정 범위 · 착륙 지점 · 도착한 포트
            var esc = sim.Escape;
            if (_escRange == null)
            {
                _escRange = NewSprite("EscapeRange", 24); _escSpot = NewSprite("EscapeSpot", 25); _escPod = NewSprite("EscapePod", 34);
            }
            bool placing = esc != null && esc.Phase == EscapePhase.Placing;
            _escRange.gameObject.SetActive(placing);
            _escSpot.gameObject.SetActive(esc != null && (esc.Phase == EscapePhase.Placing || esc.Phase == EscapePhase.Incoming));
            _escPod.gameObject.SetActive(esc != null && esc.Phase >= EscapePhase.Ready);
            if (placing) Set(_escRange, sim.Player.Position, _dot, new Color(1f, 0.55f, 0.66f, 0.08f), (float)EscapeSystem.PlaceRange * 2f);
            if (_escSpot.gameObject.activeSelf)
            {
                float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 6f);
                var c = esc.Phase == EscapePhase.Placing ? new Color(1f, 0.33f, 0.49f, 0.45f) : new Color(1f, 0.33f, 0.49f, 0.25f + 0.35f * (float)esc.ArrivalProgress);
                Set(_escSpot, esc.Position, _dot, c, (float)EscapeSystem.ClearRadius * 2f * pulse);
            }
            if (_escPod.gameObject.activeSelf)
            {
                var c = Color.Lerp(new Color(1f, 0.55f, 0.66f), new Color(1f, 0.83f, 0.43f), (float)esc.BoardProgress);
                Set(_escPod, esc.Position, _square, c, 1.6f);
            }

            // 돌진 예고선 — 선딜 동안 진로를 보여준다
            var boss = sim.Bosses?.Boss;
            bool dashTele = boss != null && boss.Dash == BossDashPhase.Windup;
            if (_dashLine == null)
            {
                var lg = new GameObject("BossDashTelegraph"); lg.transform.SetParent(transform, false);
                _dashLine = lg.AddComponent<LineRenderer>();
                _dashLine.positionCount = 2; _dashLine.useWorldSpace = true;
                _dashMaterial = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
                _dashLine.sharedMaterial = _dashMaterial;
                _dashLine.sortingOrder = 25;
            }
            _dashLine.enabled = dashTele;
            if (dashTele)
            {
                // 원본 7036~7038 — 붉은 예고 쐐기: 시작 r*.2, 길이 max(r*1.8, 속도*지속시간), 폭 r*2*1.25, 끝쪽이 5Hz 로 점멸
                var e = boss.Body;
                float r = (float)e.Radius;
                float len = Mathf.Max(r * 1.8f, (float)(SimTuning.EnemySpeed * System.Math.Max(.1, e.SpeedMul) * BossTune.DashSpeedMul * BossTune.DashDuration));
                var a = IsometricProjection.ToRender(e.Position + boss.DashDir * (r * .2f));
                var b = IsometricProjection.ToRender(e.Position + boss.DashDir * (r * .2f + len));
                const float alpha = .3f;   // bossTune dashTelegraphAlpha
                float pulse = .72f + .28f * Mathf.Sin(Time.time * 5f * 6.283f);
                _dashLine.startColor = new Color(1f, 38f / 255f, 62f / 255f, alpha * .36f);
                _dashLine.endColor = new Color(1f, 90f / 255f, 58f / 255f, alpha * pulse);
                _dashLine.startWidth = _dashLine.endWidth = r * 2f * 1.25f;
                _dashLine.SetPosition(0, new Vector3(a.x, a.y, -0.05f));
                _dashLine.SetPosition(1, new Vector3(b.x, b.y, -0.05f));
            }

            // 데미지 숫자 — 원본 J.step(dm) + 그리기: 중력·감속·회전, 팝·스쿼시·페이드
            for (int i = _texts.Count - 1; i >= 0; i--)
            {
                var t = _texts[i];
                if (t.Label)
                {
                    // J.text — 위로 뜨며 사라지는 라벨 (vy*=.945, life 1.3/s)
                    t.Vel *= Mathf.Pow(.945f, dt * 60f);
                    t.Pos += t.Vel * dt;
                    t.Life -= dt * 1.3f;
                }
                else
                {
                    t.Vel.y -= DmgGravity * Px * dt;
                    float drag = Mathf.Pow(DmgDamp, dt * 60f);
                    t.Vel *= drag;
                    t.Pos += t.Vel * dt;
                    t.Rot += t.VRot * dt;
                    t.Life -= dt * DmgLifeRate;
                }
                if (t.Life <= 0) { t.Go.SetActive(false); _textPool.Push(t); _texts.RemoveAt(i); continue; }

                float age = 1 - t.Life;
                float popN = DmgPop * t.PopMul;
                float pop = t.Label ? 1f : 1 + (popN - 1) * Mathf.Max(0, 1 - age * DmgPopDec);
                float sqY = t.Label ? 1f : 1 + DmgSquash * Mathf.Max(0, 1 - age * 5) * t.PopMul;
                float sqX = 1 / Mathf.Max(.55f, sqY);
                float px = (t.Big ? DmgBig : DmgBase) * pop * t.SzMul;          // 화면 px (원본 기준 줌 1)
                float cellH = px * Px * 1.15f;                                   // 1셀 = 50px
                float a = Mathf.Min(1, t.Life * DmgFade);
                t.Go.transform.position = new Vector3(t.Pos.x, t.Pos.y, -0.1f);
                t.Go.transform.rotation = Quaternion.Euler(0, 0, t.Rot * Mathf.Rad2Deg);
                t.Go.transform.localScale = new Vector3(sqX, sqY, 1) * (cellH / 6.4f);   // fontSize 64 · characterSize 1 = 6.4 유닛
                var c = t.Col; c.a = a; t.Mesh.color = c;
                t.Shadow.color = new Color(20 / 255f, 10 / 255f, 4 / 255f, .30f * a + .35f * a);
            }
        }

        static Vec2 Vec2Lerp(Vec2 a, Vec2 b, float t) => a + (b - a) * t;

        SpriteRenderer NewSprite(string name, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sortingOrder = order;
            return sr;
        }

        void Set(SpriteRenderer sr, Vec2 pos, Sprite sp, Color c, float size)
        {
            sr.transform.position = IsometricProjection.ToRender3(pos);
            sr.transform.rotation = Quaternion.identity;
            sr.sprite = sp; sr.color = c; sr.transform.localScale = Vector3.one * size;
        }

        /// <summary>원본 J.dmg(x,y,v,big) — 위로 튀어 오르며 떨어지는 숫자. 드릴 열(hfx)은 M7 과열 연출과 함께.</summary>
        public void Damage(Vec2 at, double value, bool big, float heat = 0f)
        {
            if (value < 1) return;
            float hfx = Mathf.Clamp01(heat);
            float jx = DmgJx * (1 + hfx * .85f), jy = DmgJy * (1 + hfx * .85f);
            float spd0 = DmgRise * (1 + hfx * 1.05f);
            float lr = Mathf.Min(1, .45f + hfx * .35f);
            float drift = DmgDrift * (1 + hfx * 1.1f);
            float spreadDeg = Mathf.Min(360, 90 + 270 * hfx * .92f + hfx * 40);
            float half = spreadDeg * Mathf.Deg2Rad * .5f;
            float ang = Mathf.PI * .5f + (Random.value * 2 - 1) * half;   // 원본은 -π/2 (y 아래 증가) → Unity 는 +π/2
            float spd = spd0 * (1 - lr * .5f + Random.value * lr) * (big ? 1.18f : 1) * (1 + hfx * .25f);
            var vel = new Vector2(Mathf.Cos(ang) * spd + (Random.value - .5f) * drift * .35f, Mathf.Sin(ang) * spd + (Random.value - .5f) * drift * .15f) * Px;

            var col0 = new Color(1f, .99f, .96f); var colB0 = new Color(1f, .92f, .71f);
            var hot = new Color(1f, .16f, .16f); var hotB = new Color(1f, .35f, .23f);
            var t = Rent();
            t.Label = false; t.Big = big;
            t.Pos = IsometricProjection.ToRender(at) + new Vector2((Random.value - .5f) * jx * Px, (Random.value - .5f) * jy * Px);
            t.Vel = vel; t.Life = 1;
            t.Rot = big ? (Random.value - .5f) * 18f * (1 + hfx) * Mathf.Deg2Rad : (Random.value - .5f) * .2f;
            t.VRot = (Random.value - .5f) * 1.2f * (1 + hfx * 1.35f) * (big ? 1.6f : 1);
            t.SzMul = 1 + hfx * .95f; t.PopMul = 1 + hfx * .75f;
            t.Col = Color.Lerp(big ? colB0 : col0, big ? hotB : hot, hfx);
            t.Mesh.text = t.Shadow.text = Mathf.RoundToInt((float)value).ToString();
            SetFont(t, Fonts.Damage);   // 피해 숫자 — ARCO (원본 @font-face 'ARCO')
            t.Mesh.fontStyle = t.Shadow.fontStyle = FontStyle.Bold;
            _texts.Add(t);
            if (_texts.Count > 44) { var old = _texts[0]; old.Go.SetActive(false); _textPool.Push(old); _texts.RemoveAt(0); }
        }

        /// <summary>원본 J.text(x,y,t,col,sz) — 라벨. sz 는 화면 px (기본 20, ×1.15).</summary>
        public void Text(Vec2 at, string text, Color color, float sizePx = 20f)
        {
            var t = Rent();
            t.Label = true; t.Big = false;
            t.Pos = IsometricProjection.ToRender(at);
            t.Vel = new Vector2(0, 38f * Px); t.Life = 1; t.Rot = 0; t.VRot = 0;
            t.SzMul = sizePx / DmgBase; t.PopMul = 1;
            t.Col = color;
            t.Mesh.text = t.Shadow.text = text;
            // 숫자만 있는 팝업(피격 -N · 재화 +N · 크루 +N)도 피해 숫자와 같은 ARCO. 한글·문장 라벨은 Pretendard(ARCO 에 한글 없음)
            SetFont(t, IsNumeric(text) ? Fonts.Damage : Fonts.UIBold);
            t.Mesh.fontStyle = t.Shadow.fontStyle = FontStyle.Bold;
            _texts.Add(t);
        }

        /// <summary>"+12" · "-5" · "37" · "x2" 처럼 숫자·부호·x·%·소수점만으로 된 문자열인가.</summary>
        static bool IsNumeric(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            bool digit = false;
            foreach (char c in s)
            {
                if (char.IsDigit(c)) { digit = true; continue; }
                if (c == '+' || c == '-' || c == '.' || c == 'x' || c == '×' || c == '%' || c == ' ') continue;
                return false;
            }
            return digit;
        }

        /// <summary>TextMesh 는 폰트를 바꾸면 머티리얼도 그 폰트 아틀라스로 바꿔야 글자가 나온다. 풀에서 재사용하므로 매번 맞춘다.</summary>
        static void SetFont(DamageText t, Font f)
        {
            if (f == null || t.Mesh.font == f) return;
            t.Mesh.font = f; t.Shadow.font = f;
            t.Mesh.GetComponent<MeshRenderer>().sharedMaterial = f.material;
            t.Shadow.GetComponent<MeshRenderer>().sharedMaterial = f.material;
        }

        DamageText Rent()
        {
            DamageText t = _textPool.Count > 0 ? _textPool.Pop() : New();
            t.Go.SetActive(true);
            return t;

            DamageText New()
            {
                var go = new GameObject("DmgText"); go.transform.SetParent(transform, false);
                var sh = new GameObject("shadow"); sh.transform.SetParent(go.transform, false);
                sh.transform.localPosition = new Vector3(0.18f, -0.18f, 0.01f);   // 외곽선 대용 그림자
                var tmS = sh.AddComponent<TextMesh>();
                tmS.anchor = TextAnchor.MiddleCenter; tmS.fontSize = 64; tmS.characterSize = 1f;
                TunnelCrew.Presentation.Visual.VisualLayers.ApplyInfoSorting(sh.GetComponent<MeshRenderer>(), 59);
                var tm = go.AddComponent<TextMesh>();
                tm.anchor = TextAnchor.MiddleCenter; tm.fontSize = 64; tm.characterSize = 1f;
                TunnelCrew.Presentation.Visual.VisualLayers.ApplyInfoSorting(go.GetComponent<MeshRenderer>(), 60);
                return new DamageText { Go = go, Mesh = tm, Shadow = tmS };
            }
        }

        void PreparePool(List<SpriteRenderer> pool, int count, int order)
        {
            while (pool.Count < count)
            {
                var go = new GameObject("fx"); go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>(); sr.sortingOrder = order;
                pool.Add(sr);
            }
            for (int i = 0; i < pool.Count; i++)
            {
                bool on = i < count;
                if (pool[i].gameObject.activeSelf != on) pool[i].gameObject.SetActive(on);
            }
        }

        static void EnsurePool(List<SpriteRenderer> pool, int count, int order, Material material, Transform parent)
        {
            while (pool.Count < count)
            {
                var go = new GameObject("projectile-fx");
                go.transform.SetParent(parent, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = order;
                if (material != null) sr.sharedMaterial = material;
                pool.Add(sr);
            }
        }

        static Sprite MakeSquare()
        {
            var t = new Texture2D(2, 2); t.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white }); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2);
        }
        static Sprite MakeCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float rad = size * 0.5f;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - rad, dy = y + 0.5f - rad;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / rad;
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(1f - Mathf.SmoothStep(0.75f, 1f, d))));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
