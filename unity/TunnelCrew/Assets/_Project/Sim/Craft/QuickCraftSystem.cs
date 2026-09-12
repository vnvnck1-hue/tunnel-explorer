using System;
using System.Collections.Generic;

namespace TunnelCrew.Sim
{
    public enum CraftKind : byte { Place, Instant, Channel }
    public enum CraftPhase : byte { Closed, Wheel, Placing }

    public sealed class CraftRecipe
    {
        public string Id, Name, Desc, Effect, Icon; public CraftKind Kind; public int Pulp, Bloom, Max; public double Cd, Range; public int FootW = 1, FootH = 1;
    }
    public sealed class CraftTurret { public Vec2 At; public double A, Life, MaxLife, Cd; }
    public sealed class CraftBarricade { public Vec2 At; public double A, Life, MaxLife, Hp, MaxHp; }
    public sealed class CraftFlare { public Vec2 At; public double Life, MaxLife; public Flare Light; }
    public sealed class CraftCharge { public Vec2 At; public double A, Life, MaxLife; public bool Done; }
    public sealed class CraftPlacement { public CraftRecipe Recipe; public bool Valid; public Vec2? World; public int C, R, FootW = 1, FootH = 1; public double Angle; }
    public sealed class CraftChannel { public CraftRecipe Recipe; public double T, StartHp; }

    /// <summary>
    /// 현장 퀵크래프트 — 원본 <c>TC_CRAFT</c> (tcCraftingFeature, docs/crafting-quickwheel-functional-spec.md).
    /// 채굴한 PULP/BLOOM 을 런 도중 즉시 소비해 생존·탐색·전투·굴착 수단으로 바꾼다. 메뉴를 열어도 월드는 멈추지 않는다.
    /// 레시피·비용·효과·설치물은 Sim, 휠·상세 패널·배치 미리보기 그리기는 Presentation(TeamOverlay).
    /// </summary>
    public sealed class QuickCraftSystem
    {
        public static readonly CraftRecipe[] Recipes =
        {
            new CraftRecipe { Id = "shaped-charge", Name = "성형 폭약", Kind = CraftKind.Place, Pulp = 7, Bloom = 2, Max = 2, Desc = "2초 뒤 벽과 적에게 범위 피해", Effect = "벽 피해 180% · 적 피해 ×2.2", Icon = "icon-shaped-charge", Range = 2.2 },
            new CraftRecipe { Id = "auto-turret", Name = "자동 포탑", Kind = CraftKind.Place, Pulp = 8, Bloom = 2, Max = 1, Desc = "주변 적을 자동 사격하는 임시 포탑", Effect = "지속 45초 · 동시 설치 1개", Icon = "icon-auto-turret", Range = 2.8 },
            new CraftRecipe { Id = "coolant-capsule", Name = "냉각 캡슐", Kind = CraftKind.Instant, Pulp = 4, Bloom = 0, Cd = 12, Desc = "드릴 냉각수를 즉시 분사", Effect = "열 75% 제거 · 과열 잠금 해제", Icon = "icon-coolant-capsule" },
            new CraftRecipe { Id = "folding-barricade", Name = "접이식 방벽", Kind = CraftKind.Place, Pulp = 6, Bloom = 0, Max = 2, Desc = "적과 적 투사체를 막는 임시 방벽", Effect = "지속 35초 · 동시 설치 2개", Icon = "icon-folding-barricade", Range = 2.3, FootW = 2, FootH = 1 },
            new CraftRecipe { Id = "med-injector", Name = "응급 주사", Kind = CraftKind.Channel, Pulp = 5, Bloom = 1, Cd = 18, Desc = "응급 약물을 주입해 체력을 회복", Effect = "0.7초 주입 · 최대 체력 35% 회복", Icon = "icon-med-injector" },
            new CraftRecipe { Id = "flare-bundle", Name = "휴대 조명탄", Kind = CraftKind.Place, Pulp = 3, Bloom = 0, Max = 2, Desc = "주변을 밝히는 휴대용 조명탄", Effect = "지속 30초 · 동시 설치 2개", Icon = "icon-flare-bundle", Range = 2.6 },
        };
        public static CraftRecipe ById(string id) { foreach (var r in Recipes) if (r.Id == id) return r; return null; }

        readonly TunnelSim _sim;
        public CraftPhase Phase { get; private set; } = CraftPhase.Closed;
        public string Selected = "auto-turret";
        public CraftPlacement Placement { get; private set; }
        public CraftChannel Using { get; private set; }
        public readonly Dictionary<string, double> Cooldowns = new Dictionary<string, double>();
        public readonly List<CraftTurret> Turrets = new List<CraftTurret>();
        public readonly List<CraftBarricade> Barricades = new List<CraftBarricade>();
        public readonly List<CraftFlare> Flares = new List<CraftFlare>();
        public readonly List<CraftCharge> Charges = new List<CraftCharge>();
        public event Action<CrewFxEvent> Fx;
        public event Action<string> Toast;
        public event Action<string> Sfx;

        public QuickCraftSystem(TunnelSim sim) { _sim = sim; }
        WorldGrid W => _sim.World;
        PlayerState P => _sim.Player;
        public bool Playing => W != null && _sim.Phase == GamePhase.Playing && !P.Downed;

        int LiveCount(CraftRecipe r) => r.Id switch { "auto-turret" => Turrets.Count, "folding-barricade" => Barricades.Count, "flare-bundle" => Flares.Count, "shaped-charge" => Charges.Count, _ => 0 };
        public double Remaining(CraftRecipe r) => Cooldowns.TryGetValue(r.Id, out var v) ? Math.Max(0, v) : 0;

        /// <summary>제작 불가 이유 (빈 문자열이면 가능). 원본 reason().</summary>
        public string Reason(CraftRecipe r)
        {
            if (!Playing) return "지금은 제작할 수 없음";
            if (_sim.Loot.Pulp < r.Pulp || _sim.Loot.Bloom < r.Bloom) return "재료 부족";
            double cd = Remaining(r); if (cd > 0) return "재사용 " + cd.ToString("F1") + "초";
            if (r.Max > 0 && LiveCount(r) >= r.Max) return $"설치 한도 {LiveCount(r)} / {r.Max}";
            if (r.Id == "coolant-capsule" && (!SimTuning.DrillHeatOn || P.DrillHeat < .1)) return "지금은 냉각이 필요 없음";
            if (r.Id == "med-injector" && P.Hp >= P.HpMax * .9) return "체력이 충분함";
            return "";
        }
        public bool Can(CraftRecipe r) => Reason(r).Length == 0;
        bool Spend(CraftRecipe r) { if (!Can(r)) return false; _sim.Loot.Spend(r.Pulp, r.Bloom); return true; }
        void Refund(CraftRecipe r) => _sim.Loot.Refund(r.Pulp, r.Bloom);
        void SetCooldown(CraftRecipe r) { if (r.Cd > 0) Cooldowns[r.Id] = r.Cd; }

        // ── 휠 상태
        public bool Open()
        {
            if (!Playing || Phase != CraftPhase.Closed) return false;
            Phase = CraftPhase.Wheel; Sfx?.Invoke("ui"); return true;
        }
        public void Close(bool silent)
        {
            if (Phase == CraftPhase.Closed) return;
            Phase = CraftPhase.Closed; Placement = null;
            if (!silent) Sfx?.Invoke("ui");
        }
        public void Select(string id, bool quiet = false)
        {
            if (ById(id) == null || Selected == id) return;
            Selected = id; if (!quiet) Sfx?.Invoke("tick");
        }
        /// <summary>휠 중심에서 방향각(라디안, 위=−π/2)으로 가장 가까운 60° 슬롯. 데드존은 호출자가 판정한다.</summary>
        public static int SlotFromAngle(double a)
        {
            int best = 0; double bd = 99;
            for (int i = 0; i < 6; i++) { double q = -Math.PI / 2 + i * Math.PI / 3, d = Math.Abs(Math.Atan2(Math.Sin(a - q), Math.Cos(a - q))); if (d < bd) { bd = d; best = i; } }
            return best;
        }

        /// <summary>선택 레시피 제작/배치 단계 진입 (원본 confirmSelection).</summary>
        public bool ConfirmSelection()
        {
            var r = ById(Selected); if (r == null) return false;
            if (!Can(r)) { Toast?.Invoke(Reason(r)); Sfx?.Invoke("tick"); return false; }
            if (r.Kind == CraftKind.Place) { BeginPlacement(r); return true; }
            if (r.Id == "coolant-capsule")
            {
                if (!Spend(r)) return false;
                P.DrillHeat = Math.Max(0, P.DrillHeat * .25); P.DrillHeatLock = 0; SetCooldown(r);
                Fx?.Invoke(new CrewFxEvent { Kind = CrewFxKind.Ring, At = P.Position, Color = "#7FEBD0", Radius = 1.05, Size = 9 });
                Fx?.Invoke(new CrewFxEvent { Kind = CrewFxKind.Burst, At = P.Position, Color = "#7FEBD0", Count = 12, Size = 105 });
                Close(true); Sfx?.Invoke("cache"); Toast?.Invoke("냉각 완료 · 드릴 열 -75%");
                return true;
            }
            if (r.Id == "med-injector")
            {
                if (!Spend(r)) return false;
                Using = new CraftChannel { Recipe = r, T = .7, StartHp = P.Hp };
                Close(true); Toast?.Invoke("응급 주사 투여 중…");
                return true;
            }
            return false;
        }

        void BeginPlacement(CraftRecipe r)
        {
            Phase = CraftPhase.Placing;
            Placement = new CraftPlacement { Recipe = r, Angle = P.Aim };
            Toast?.Invoke("카메라 줌 고정 · 타일 스냅 배치"); Sfx?.Invoke("tick");
        }
        public void CancelPlacement() { if (Phase != CraftPhase.Placing) return; Close(false); Toast?.Invoke("배치 취소"); }

        /// <summary>마우스 월드 좌표 → 타일 스냅 · 유효성 (원본 updatePlacement).</summary>
        public void UpdatePlacement(Vec2 aimWorld)
        {
            if (Phase != CraftPhase.Placing || Placement == null) return;
            var q = Placement; var r = q.Recipe;
            var (c, rr) = WorldGrid.ToCell(aimWorld); var pivot = WorldGrid.CellCenter(c, rr);
            double aim0 = (pivot - P.Position).Angle;
            q.Angle = r.Id == "folding-barricade" ? Math.Round((aim0 + Math.PI / 2) / (Math.PI / 2)) * (Math.PI / 2) : aim0;
            bool vertical = r.Id == "folding-barricade" && Math.Abs(Math.Sin(q.Angle)) > .5;
            int fw = vertical ? r.FootH : r.FootW, fh = vertical ? r.FootW : r.FootH;
            var world = new Vec2(pivot.X + (fw - 1) * .5, pivot.Y + (fh - 1) * .5);
            double d = Vec2.Distance(world, P.Position), limit = r.Range > 0 ? r.Range : 2.2;
            bool valid = d <= limit && d >= .55;
            for (int gy = 0; valid && gy < fh; gy++) for (int gx = 0; gx < fw; gx++)
            {
                int cc = c + gx, r2 = rr + gy;
                if (!W.InBounds(cc, r2) || W.IsSolid(cc, r2)) { valid = false; break; }
            }
            if (valid)
            {
                double minD = Math.Max(.72, Math.Max(fw, fh) * .55);
                foreach (var t in Turrets) if (Vec2.Distance(t.At, world) < minD) { valid = false; break; }
                if (valid) foreach (var b in Barricades) if (Vec2.Distance(b.At, world) < minD) { valid = false; break; }
                if (valid) foreach (var f in Flares) if (Vec2.Distance(f.At, world) < minD) { valid = false; break; }
                if (valid) foreach (var ch in Charges) if (Vec2.Distance(ch.At, world) < minD) { valid = false; break; }
            }
            q.C = c; q.R = rr; q.FootW = fw; q.FootH = fh; q.World = world; q.Valid = valid;
        }
        public string PlacementLabel => Placement == null ? "" : $"타일 {Placement.C},{Placement.R}{(Placement.FootW * Placement.FootH > 1 ? $" · {Placement.FootW}×{Placement.FootH}" : "")} · {(Placement.Valid ? "클릭하여 설치" : "설치 불가")}";

        public bool ConfirmPlacement()
        {
            var q = Placement;
            if (Phase != CraftPhase.Placing || q == null || !q.Valid || q.World == null) { Sfx?.Invoke("tick"); return false; }
            var r = q.Recipe;
            if (!Spend(r)) { Toast?.Invoke(Reason(r)); Close(true); return false; }
            var at = q.World.Value; double a = q.Angle;
            switch (r.Id)
            {
                case "auto-turret": Turrets.Add(new CraftTurret { At = at, A = a, Life = 45, MaxLife = 45, Cd = .15 }); break;
                case "folding-barricade": Barricades.Add(new CraftBarricade { At = at, A = a, Life = 35, MaxLife = 35, Hp = P.HpMax * 1.6, MaxHp = P.HpMax * 1.6 }); break;
                case "flare-bundle":
                {
                    var light = new Flare { Position = at, Ttl = 30, MaxTtl = 30, LightRadius = SimTuning.PxCells(94), VisionRange = 3, IsCraft = true };
                    _sim.Roles.Flares.Add(light); _sim.Los?.MarkDirty();
                    Flares.Add(new CraftFlare { At = at, Life = 30, MaxLife = 30, Light = light });
                    break;
                }
                case "shaped-charge": Charges.Add(new CraftCharge { At = at, A = a, Life = 2, MaxLife = 2 }); break;
            }
            Close(true); Sfx?.Invoke("cache");
            Fx?.Invoke(new CrewFxEvent { Kind = CrewFxKind.Ring, At = at, Color = "#FFD36E", Radius = .8, Size = 6 });
            Fx?.Invoke(new CrewFxEvent { Kind = CrewFxKind.Burst, At = at, Color = "#FFD36E", Count = 8, Size = 90 });
            Toast?.Invoke(r.Name + " 설치");
            return true;
        }

        void ExplodeCharge(CraftCharge q)
        {
            const double rad = 1.6;
            Fx?.Invoke(new CrewFxEvent { Kind = CrewFxKind.Flash, At = q.At, Color = "#FFAE48", Radius = 1.16 });
            Fx?.Invoke(new CrewFxEvent { Kind = CrewFxKind.Ring, At = q.At, Color = "#FF8D48", Radius = rad, Size = 12 });
            Fx?.Invoke(new CrewFxEvent { Kind = CrewFxKind.Burst, At = q.At, Color = "#FF8D48", Count = 20, Size = 210 });
            Fx?.Invoke(new CrewFxEvent { Kind = CrewFxKind.Kick, Size = 5 });
            Sfx?.Invoke("brk");
            var (cc, cr) = WorldGrid.ToCell(q.At);
            for (int dr = -2; dr <= 2; dr++) for (int dc = -2; dc <= 2; dc++)
            {
                int c = cc + dc, r = cr + dr;
                if (!W.InInterior(c, r) || Math.Sqrt(dc * dc + dr * dr) > 1.75 || !W.IsSolid(c, r)) continue;
                var t = W.At(c, r); if (TileTypes.IsBedrock(t)) continue;   // 기반암에는 20% 압력만 — 원본 damage() 도 기반암을 깎지 않는다
                var d = WorldGrid.CellCenter(c, r) - q.At; double len = Math.Max(1e-6, d.Length);
                W.Damage(c, r, W.MaxHp(t) * 1.8, d / len);
            }
            foreach (var e in _sim.Enemies.Enemies)
            {
                if (!e.Alive) continue;
                double d = Vec2.Distance(e.Position, q.At); if (d > rad) continue;
                _sim.Enemies.HurtEnemy(e, SimTuning.EnemyGunDamage * 2.2, (e.Position - q.At) / rad, q.At);
            }
        }

        /// <summary>매 틱 — 쿨다운·주입·설치물 (원본 updateObjects). Enemies.Tick 뒤에 호출한다 (방벽이 적을 밀어낸다).</summary>
        public void Tick(double dt)
        {
            if (W == null) return;
            if (!Playing) { if (Phase != CraftPhase.Closed) Close(true); if (_sim.Phase != GamePhase.Playing) return; }
            var keys = new List<string>(Cooldowns.Keys);
            foreach (var k in keys) { double n = Cooldowns[k] - dt; if (n <= 0) Cooldowns.Remove(k); else Cooldowns[k] = n; }
            if (Using != null)
            {
                Using.T -= dt;
                if (P.Hp < Using.StartHp - .01) { Refund(Using.Recipe); Using = null; Toast?.Invoke("피격 · 응급 주사 취소"); }
                else if (Using.T <= 0)
                {
                    var r = Using.Recipe; P.Hp = Math.Min(P.HpMax, P.Hp + P.HpMax * .35); SetCooldown(r); Using = null;
                    Fx?.Invoke(new CrewFxEvent { Kind = CrewFxKind.Ring, At = P.Position, Color = "#86EA76", Radius = .9, Size = 9 });
                    Fx?.Invoke(new CrewFxEvent { Kind = CrewFxKind.Text, At = P.Position + new Vec2(0, .6), Label = "+" + Math.Round(P.HpMax * .35), Color = "#9CFF8A", Size = 18 });
                    Sfx?.Invoke("cache"); Toast?.Invoke("응급 처치 완료");
                }
            }
            // 자동 포탑 — 사거리 190px(3.8칸) · 0.48초 · 위력 .62. 엔지니어 센트리보다 약하고 전력망 보너스가 없다
            foreach (var t in Turrets)
            {
                t.Life -= dt; t.Cd -= dt;
                if (t.Life <= 0 || t.Cd > 0) continue;
                EnemyState target = null; double bd = SimTuning.PxCells(190);
                foreach (var e in _sim.Enemies.Enemies) { if (!e.Alive) continue; double d = Vec2.Distance(e.Position, t.At); if (d < bd) { bd = d; target = e; } }
                if (target == null) continue;
                var dir = (target.Position - t.At).Normalized; t.A = dir.Angle;
                _sim.Projectiles.Emit(new Projectile { Position = t.At + dir * .18, Velocity = dir * SimTuning.TeCells(ProjectileSystem.BaseSpeedPx("support")), Life = ProjectileSystem.BaseLife("support"), Power = .62, VisualId = "support" }, t.A);
                t.Cd = .48;
            }
            Turrets.RemoveAll(t => t.Life <= 0);
            // 접이식 방벽 — 적 이동·적 투사체만 막는다 (플레이어·아군 탄은 통과)
            foreach (var b in Barricades)
            {
                b.Life -= dt;
                double ca = Math.Cos(-b.A), sa = Math.Sin(-b.A);
                foreach (var e in _sim.Enemies.Enemies)
                {
                    if (!e.Alive || e.IsBoss) continue;
                    var d = e.Position - b.At; double lx = d.X * ca - d.Y * sa, ly = d.X * sa + d.Y * ca;
                    if (Math.Abs(lx) < .85 && Math.Abs(ly) < .36)
                    {
                        double push = Math.Sign(ly == 0 ? 1 : ly) * SimTuning.PxCells(55) * dt;
                        e.Position += new Vec2(-Math.Sin(b.A) * push, Math.Cos(b.A) * push);
                        b.Hp -= 18 * dt;
                    }
                }
                for (int i = _sim.Enemies.Shots.Count - 1; i >= 0; i--)
                {
                    var s = _sim.Enemies.Shots[i]; var d = s.Position - b.At; double lx = d.X * ca - d.Y * sa, ly = d.X * sa + d.Y * ca;
                    if (Math.Abs(lx) < .9 && Math.Abs(ly) < .38) { _sim.Enemies.Shots.RemoveAt(i); b.Hp -= 9; }
                }
            }
            Barricades.RemoveAll(b => b.Life <= 0 || b.Hp <= 0);
            foreach (var f in Flares) f.Life -= dt;
            Flares.RemoveAll(f => f.Life <= 0 || f.Light == null || f.Light.Ttl <= 0);
            foreach (var q in Charges) { q.Life -= dt; if (q.Life <= 0 && !q.Done) { q.Done = true; ExplodeCharge(q); } }
            Charges.RemoveAll(q => q.Done);
        }

        public void Reset()
        {
            Close(true); Cooldowns.Clear(); Using = null;
            Turrets.Clear(); Barricades.Clear(); Flares.Clear(); Charges.Clear();
        }
        /// <summary>층이 바뀌면 설치물은 두고 간다 — 원본 reset() 은 런 시작에만 불린다. 목록만 비운다(월드가 새로 생겼다).</summary>
        public void OnFloorInit() { Turrets.Clear(); Barricades.Clear(); Flares.Clear(); Charges.Clear(); Close(true); }
    }
}
