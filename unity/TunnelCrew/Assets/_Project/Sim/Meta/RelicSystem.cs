using System;
using System.Collections.Generic;

namespace TunnelCrew.Sim
{
    public struct RelicFxEvent { public string Kind; public Vec2 At, To; public string Label; public double Radius; }
    public struct RelicGrantedEvent { public RelicDef Relic; public Vec2 At; public bool DuplicateFallback; }

    /// <summary>
    /// 유물 효과 33종 — 원본 <c>infApplyRelics · infRelicOnHit · infRelicOnKill · infRelicTick · infRelicEnemyTick ·
    /// infRelicAfterPlayerHurt · infRelicPlayerDamageMod · infRelicOnBlock · infRelicOnRare · infRelicOnDescend ·
    /// infRelicTryPhoenix · infRelicWallSlam · infRelicBossDrop · infRelicBuriedFind</c>(14338~14639).
    ///
    /// 장착 목록은 런 시작에 <see cref="Apply"/> 로 고정된다. 재귀 방지 잠금(<c>_hitLock</c>)은 원본 relicHitLock 과 같다.
    /// 연출은 <see cref="Fx"/> 이벤트로만 넘긴다.
    /// </summary>
    public sealed class RelicSystem
    {
        readonly Rng _rng;
        EnemySystem _enemies;
        PlayerState _player;
        PlayerBuild _build;
        WorldGrid _world;
        Action<int, int, double, double> _blast;   // TunnelSim.TraitBlast
        Action<Vec2, double, string, double> _fireSupport;   // 드론 사격: (from, angle, visualId, power)

        public readonly List<string> Ids = new List<string>();
        public Dictionary<RelicElem, int> Res = new Dictionary<RelicElem, int>();
        public bool Active => Ids.Count > 0;
        public bool Has(string id) => Ids.Contains(id);
        public int ResLv(RelicElem e) => Res.TryGetValue(e, out int v) ? v : 0;

        // 런 상태 (원본 INF.relic*)
        public double TimeStopT, HourglassCd, RodCd;
        public int VoltShots; double _prevReload;
        public double StillT; bool _lastStandOn;
        public int DepthStacks; double _coreFrac;
        public bool PhoenixUsed;
        double _bloodTick; int _pityBlocks; bool _hitLock;
        double _time;

        // 태엽 수호자 드론
        public bool HasDrone => Has("r_guardian");
        public Vec2 DronePos; public double DroneCd, DroneBob; public int DroneFace = 1; public double DroneAim;

        public event Action<RelicFxEvent> Fx;
        public event Action<RelicGrantedEvent> Granted;
        /// <summary>발굴 — 호출자(TunnelSim)가 MetaState 에 보관·저장한다.</summary>
        public Func<int, RelicDef> PickUnowned = _ => null;

        public RelicSystem(uint seed = 0x2E11C) { _rng = new Rng(seed); }

        public void Bind(WorldGrid world, EnemySystem enemies, PlayerState player, PlayerBuild build,
                         Action<int, int, double, double> blast, Action<Vec2, double, string, double> fireSupport)
        {
            _world = world; _enemies = enemies; _player = player; _build = build; _blast = blast; _fireSupport = fireSupport;
        }

        /// <summary>원본 infApplyRelics — 런 시작. 심연의 계약은 최대 HP 30% 감소.</summary>
        public void Apply(IList<string> equipped, PlayerState player)
        {
            _player = player;
            Ids.Clear(); Ids.AddRange(equipped);
            Res = Relics.Resonance(Ids);
            TimeStopT = HourglassCd = RodCd = 0; VoltShots = 0; _prevReload = 0; StillT = 0; _lastStandOn = false;
            DepthStacks = 0; _coreFrac = 0; PhoenixUsed = false; _bloodTick = 0; _pityBlocks = 0; _hitLock = false;
            if (!Active) return;
            if (Has("r_abyss"))
            {
                double cut = Math.Round(_player.HpMax * .3);
                _player.HpMax = Math.Max(30, _player.HpMax - cut);
                _player.Hp = Math.Min(_player.Hp, _player.HpMax);
            }
            if (HasDrone) { DronePos = _player.Position + new Vec2(0, .6); DroneCd = 1; }
        }

        public bool TimeStopped => TimeStopT > 0;
        /// <summary>대지 공명 1 — 넉백 ×1.5.</summary>
        public double KnockMul => ResLv(RelicElem.Earth) >= 1 ? 1.5 : 1.0;
        static double StatusMul(EnemyState e) => e.IsBoss ? .5 : 1;

        // ───────────────────────────── 전투 훅
        /// <summary>원본 infRelicOnHit — 피해 배율 + 상태이상 프록. hurtEnemy 진입 시 호출.</summary>
        public double OnHit(EnemyState e, double dmg, bool fromWeapon)
        {
            if (!Active || _hitLock) return dmg;
            double mul = 1; bool crit = false;
            if (Has("r_fang") && _rng.NextDouble() < .10) { mul *= 2; crit = true; }
            if (Has("r_banner") && _player.Hp <= _player.HpMax * .30) mul *= 1.4;
            if (Has("r_strata")) mul *= 1 + DepthStacks * .04;
            if (Has("r_abyss")) mul *= 1.5;
            if (Has("r_magma")) mul *= 1 + JsMath.Clamp(_player.DrillHeat, 0, 1) * .30;
            if (Has("r_permafrost") && e.SlowTime > 0) mul *= 1.25;
            if (ResLv(RelicElem.Volt) >= 2 && e.ShockT > 0) mul *= 1.2;
            dmg *= mul;
            if (crit) Fx?.Invoke(new RelicFxEvent { Kind = "crit", At = e.Position, Radius = e.Radius });

            // 상태이상 프록 — 드릴 틱 스팸 방지용 개체별 윈도 .32초
            if (_time - e.RelicProcAt >= .32)
            {
                e.RelicProcAt = _time;
                if (Has("r_venom") && _rng.NextDouble() < .35) ApplyPoison(e);
                if (Has("r_ember") && _rng.NextDouble() < .20) ApplyBurn(e, null);
                if (Has("r_frost")) ApplySlow(e);
                if (fromWeapon && VoltShots > 0) { VoltShots--; Shock(e, dmg, true); }
                else if (Has("r_coil") && _rng.NextDouble() < .25) Shock(e, dmg, false);
            }
            return dmg;
        }

        /// <summary>원본 infRelicOnKill.</summary>
        public void OnKill(EnemyState e)
        {
            if (!Active) return;
            if (Has("r_leech")) { _player.Hp = Math.Min(_player.HpMax, _player.Hp + 3); Fx?.Invoke(new RelicFxEvent { Kind = "heal", At = _player.Position, Label = "+3" }); }
            if (Has("r_bloodpact")) { _player.Hp = Math.Min(_player.HpMax, _player.Hp + 6); Fx?.Invoke(new RelicFxEvent { Kind = "heal", At = _player.Position, Label = "+6 피의 계약" }); }
            // 들불 — 화상 전염
            if (Has("r_wildfire") && e.BurnT > 0)
            {
                EnemyState best = null; double bd = 5.5;
                foreach (var o in _enemies.Enemies) { if (o == e || !o.Alive || o.BurnT > 0) continue; double d = Vec2.Distance(o.Position, e.Position); if (d < bd) { bd = d; best = o; } }
                if (best != null) { ApplyBurn(best, e.BurnDps); Fx?.Invoke(new RelicFxEvent { Kind = "arcFire", At = e.Position, To = best.Position, Label = "들불" }); }
            }
            // 빙결 공명 3 — 냉기 폭발
            if (ResLv(RelicElem.Frost) >= 2 && e.FrozenTime > 0 && !_hitLock)
            {
                _hitLock = true;
                foreach (var o in _enemies.Enemies)
                {
                    if (o == e || !o.Alive) continue;
                    var d = o.Position - e.Position; double dist = d.Length; if (dist > 2.6) continue;
                    _enemies.HurtEnemy(o, SimTuning.EnemyGunDamage * .8 * _build.GunMul, d / Math.Max(1e-6, dist), e.Position);
                    o.SlowTime = Math.Max(o.SlowTime, 2); o.SlowMul = .5;
                }
                _hitLock = false;
                Fx?.Invoke(new RelicFxEvent { Kind = "frostBurst", At = e.Position, Radius = 2.6 });
            }
            // 불안정한 심장 — 유폭
            if (Has("r_unstable") && !_hitLock)
            {
                _hitLock = true;
                foreach (var o in _enemies.Enemies)
                {
                    if (o == e || !o.Alive) continue;
                    var d = o.Position - e.Position; double dist = d.Length; if (dist > 2.2) continue;
                    _enemies.HurtEnemy(o, SimTuning.EnemyGunDamage * .65 * _build.GunMul, d / Math.Max(1e-6, dist), e.Position);
                }
                _hitLock = false;
                var (c, r) = WorldGrid.ToCell(e.Position);
                _blast?.Invoke(c, r, 1.4, .28);
                Fx?.Invoke(new RelicFxEvent { Kind = "unstable", At = e.Position, Radius = 1.4 });
            }
        }

        // ───────────────────────────── 상태이상 (원본 infRelicApply*)
        public void ApplyBurn(EnemyState e, double? carryDps)
        {
            double m = StatusMul(e); int res = ResLv(RelicElem.Fire);
            double dps = (carryDps ?? Math.Max(3, SimTuning.EnemyGunDamage * .30 * _build.GunMul)) * m;
            if (res >= 2 && e.BurnT > 0) e.BurnDps += dps * .5;   // 공명 3 — 화상 중첩
            else e.BurnDps = Math.Max(e.BurnDps, dps);
            e.BurnT = Math.Max(e.BurnT, 3 * (res >= 1 ? 2 : 1) * m);
        }
        public void ApplyPoison(EnemyState e)
        {
            double m = StatusMul(e);
            e.PoisDps = Math.Max(e.PoisDps, Math.Max(2, SimTuning.EnemyGunDamage * .25 * _build.GunMul) * m);
            e.PoisT = Math.Max(e.PoisT, 3 * m);
        }
        public void ApplySlow(EnemyState e)
        {
            int res = ResLv(RelicElem.Frost); double str = (res >= 1 ? .5 : .3) * StatusMul(e);
            e.SlowTime = Math.Max(e.SlowTime, 2); e.SlowMul = 1 - str;
            if (Has("r_flashfreeze") && !e.IsBoss && e.FreezeImmuneT <= 0 && e.FrozenTime <= 0)
            {
                e.SlowHits++;
                if (e.SlowHits >= 5) { e.SlowHits = 0; e.FrozenTime = 2; e.Velocity = Vec2.Zero; Fx?.Invoke(new RelicFxEvent { Kind = "freeze", At = e.Position, Radius = e.Radius, Label = "빙결!" }); }
            }
        }
        public void Shock(EnemyState e, double dmg, bool forced)
        {
            int res = ResLv(RelicElem.Volt);
            e.ShockT = Math.Max(e.ShockT, 1.6);
            int targets = 1 + (res >= 1 ? 1 : 0);
            double baseDmg = Math.Max(4, (dmg > 0 ? dmg : SimTuning.EnemyGunDamage) * .5);
            var cands = new List<(EnemyState o, double d)>();
            foreach (var o in _enemies.Enemies) { if (o == e || !o.Alive) continue; double d = Vec2.Distance(o.Position, e.Position); if (d < 4.5) cands.Add((o, d)); }
            cands.Sort((a, b) => a.d.CompareTo(b.d));
            _hitLock = true;
            for (int i = 0; i < Math.Min(targets, cands.Count); i++)
            {
                var (o, d) = cands[i];
                o.ShockT = Math.Max(o.ShockT, 1.2);
                Fx?.Invoke(new RelicFxEvent { Kind = "arcVolt", At = e.Position, To = o.Position });
                _enemies.HurtEnemy(o, baseDmg, (o.Position - e.Position) / Math.Max(1e-6, d), e.Position);
            }
            _hitLock = false;
            Fx?.Invoke(new RelicFxEvent { Kind = "shock", At = e.Position, Radius = e.Radius, Label = forced ? "감전탄" : null });
        }

        /// <summary>원본 infRelicEnemyTick — 화상·중독 DoT, 감전·슬로우 감쇠.</summary>
        public void EnemyTick(EnemyState e, double dt)
        {
            if (e.SlamCd > 0) e.SlamCd = Math.Max(0, e.SlamCd - dt);
            if (e.FreezeImmuneT > 0) e.FreezeImmuneT = Math.Max(0, e.FreezeImmuneT - dt);
            if (e.ShockT > 0) e.ShockT = Math.Max(0, e.ShockT - dt);
            if (e.SlowTime <= 0) e.SlowHits = 0;
            if (e.BurnT > 0 && e.Alive)
            {
                e.BurnT -= dt; e.BurnTick += dt;
                while (e.BurnTick >= .5 && e.Alive) { e.BurnTick -= .5; _hitLock = true; _enemies.HurtEnemy(e, (e.BurnDps > 0 ? e.BurnDps : 3) * .5, Vec2.Zero, e.Position); _hitLock = false; }
                if (e.BurnT <= 0) { e.BurnT = 0; e.BurnDps = 0; }
            }
            if (e.PoisT > 0 && e.Alive)
            {
                e.PoisT -= dt; e.PoisTick += dt;
                while (e.PoisTick >= .5 && e.Alive) { e.PoisTick -= .5; _hitLock = true; _enemies.HurtEnemy(e, (e.PoisDps > 0 ? e.PoisDps : 2) * .5, Vec2.Zero, e.Position); _hitLock = false; }
                if (e.PoisT <= 0) { e.PoisT = 0; e.PoisDps = 0; }
            }
        }

        /// <summary>원본 infRelicWallSlam — 사태 유발자: 넉백 중 벽에 부딪히면 추가 피해 + 2초 경직.</summary>
        public void WallSlam(EnemyState e, Vec2 prev, double kbMag)
        {
            if (!Has("r_avalanche") || e.IsBoss || !e.Alive || e.SlamCd > 0) return;
            if (kbMag < SimTuning.TeCells(150)) return;
            double pushed = Vec2.Distance(e.Position, prev);
            if (pushed < 2.0 / 50) return;   // collide 가 실제로 벽에서 밀어냈을 때만 (원본 2px)
            e.SlamCd = 1; e.StunTime = Math.Max(e.StunTime, 2); e.Knock *= .15;
            var n = (e.Position - prev) / Math.Max(1e-6, pushed);
            _hitLock = true; _enemies.HurtEnemy(e, SimTuning.EnemyGunDamage * 1.1 * _build.GunMul, n, prev); _hitLock = false;
            Fx?.Invoke(new RelicFxEvent { Kind = "slam", At = e.Position, Radius = e.Radius, Label = "쿵!" });
            if (ResLv(RelicElem.Earth) >= 2)
            {
                var (c, r) = WorldGrid.ToCell(e.Position);
                _blast?.Invoke(c, r, 1.6, .3);
                _hitLock = true;
                foreach (var o in _enemies.Enemies) { if (o == e || !o.Alive) continue; var d = o.Position - e.Position; double dist = d.Length; if (dist > 2.2) continue; _enemies.HurtEnemy(o, SimTuning.EnemyGunDamage * .5 * _build.GunMul, d / Math.Max(1e-6, dist), e.Position); }
                _hitLock = false;
            }
        }

        // ───────────────────────────── 플레이어 훅
        /// <summary>원본 infRelicPlayerDamageMod — 충격 완화 젤 ×.85, 암반 피부(정지 1초) ×.7.</summary>
        public double PlayerDamageMod(double dmg)
        {
            if (!Active) return dmg;
            if (Has("r_gel")) dmg *= .85;
            if (Has("r_stoneskin") && StillT >= 1) dmg *= .7;
            return Math.Max(1, JsMath.Round(dmg));
        }

        /// <summary>원본 infRelicAfterPlayerHurt — 회중시계·피뢰침·모래시계.</summary>
        public void AfterPlayerHurt()
        {
            if (!Active) return;
            if (Has("r_watch")) _player.IFrames = Math.Max(_player.IFrames, SimTuning.PlayerIFrame * 1.6);
            if (Has("r_rod") && RodCd <= 0)
            {
                RodCd = 8; int zapped = 0; _hitLock = true;
                foreach (var o in _enemies.Enemies)
                {
                    if (!o.Alive) continue;
                    var d = o.Position - _player.Position; double dist = d.Length; if (dist > 4) continue;
                    o.ShockT = Math.Max(o.ShockT, 1.6); if (!o.IsBoss) o.StunTime = Math.Max(o.StunTime, 1); zapped++;
                    Fx?.Invoke(new RelicFxEvent { Kind = "arcVolt", At = _player.Position, To = o.Position });
                    _enemies.HurtEnemy(o, SimTuning.EnemyGunDamage * .5 * _build.GunMul, d / Math.Max(1e-6, dist), _player.Position);
                }
                _hitLock = false;
                if (zapped > 0) Fx?.Invoke(new RelicFxEvent { Kind = "rod", At = _player.Position, Radius = 4, Label = "피뢰침 방전" });
            }
            if (Has("r_hourglass") && HourglassCd <= 0)
            {
                HourglassCd = 30; TimeStopT = 3;
                Fx?.Invoke(new RelicFxEvent { Kind = "timeStop", At = _player.Position, Radius = 5, Label = "시간 정지" });
            }
        }

        /// <summary>원본 infRelicTryPhoenix — 런당 1회, HP 40% + 4초 무적.</summary>
        public bool TryPhoenix(RoleSystem roles)
        {
            if (!Active || !Has("r_phoenix") || PhoenixUsed) return false;
            PhoenixUsed = true;
            _player.Downed = false;
            _player.Hp = Math.Max(1, JsMath.Round(_player.HpMax * .4));
            _player.IFrames = Math.Max(_player.IFrames, 4);
            if (roles != null) roles.ShieldTime = Math.Max(roles.ShieldTime, 4);
            Fx?.Invoke(new RelicFxEvent { Kind = "phoenix", At = _player.Position, Radius = 3, Label = "불사조 깃털!" });
            return true;
        }

        // ───────────────────────────── 월드 훅
        /// <summary>원본 infRelicOnBlock — 굴착 공명석(15%) + 발굴 피티 카운트.</summary>
        public void OnBlock(Vec2 at)
        {
            if (!Active) return;
            _pityBlocks++;
            if (Has("r_resonstone") && _rng.NextDouble() < .15 && !_hitLock)
            {
                _hitLock = true; bool hitAny = false;
                foreach (var e in _enemies.Enemies)
                {
                    if (!e.Alive || e.IsBoss) continue;
                    var d = e.Position - at; double dist = d.Length; if (dist > 2.4) continue;
                    hitAny = true; var n = d / Math.Max(1e-6, dist);
                    e.Knock += n * (2.2 * SimTuning.KnockDrag * .35);
                    _enemies.HurtEnemy(e, SimTuning.EnemyGunDamage * .4 * _build.GunMul, n, at);
                }
                _hitLock = false;
                if (hitAny) Fx?.Invoke(new RelicFxEvent { Kind = "resonstone", At = at, Radius = 2.4, Label = "공명 진동" });
            }
        }

        /// <summary>원본 infRelicOnRare — 자철석 심장(+20% 소수 누적) + 발굴 판정 (피티 260블록).</summary>
        public int OnRare(Vec2 at, int coreGained, int depth)
        {
            int extra = 0;
            if (Has("r_lodestone"))
            {
                _coreFrac += coreGained * .2;
                while (_coreFrac >= 1) { _coreFrac -= 1; extra++; Fx?.Invoke(new RelicFxEvent { Kind = "label", At = at, Label = "자철석 +1" }); }
            }
            bool forced = _pityBlocks >= 260;
            if (forced || _rng.NextDouble() < DropChance(depth)) Grant(PickUnowned(RollRarity(false)), at);
            return extra;
        }

        public double DropChance(int depth) { double b = .015 + Math.Min(.02, Math.Max(0, depth - 1) * .002); return b * (Has("r_detector") ? 2 : 1); }
        public int RollRarity(bool minRare) { double r = _rng.NextDouble(); if (minRare) return r < .15 ? 4 : 2; return r < .05 ? 4 : r < .30 ? 2 : 1; }

        /// <summary>원본 infRelicBossDrop — 수호자 35% · 그 외 60%, 탐지기 ×1.6, 상한 95%.</summary>
        public void BossDrop(BossTier tier, Vec2 at)
        {
            double chance = (tier == BossTier.Guardian ? .35 : .6) * (Has("r_detector") ? 1.6 : 1);
            if (_rng.NextDouble() < Math.Min(.95, chance)) Grant(PickUnowned(RollRarity(true)), at);
        }
        /// <summary>원본 infRelicBuriedFind — 묻힌 유물 50% (탐지기 90%).</summary>
        public void BuriedFind(Vec2 at)
        {
            if (_rng.NextDouble() < (Has("r_detector") ? .9 : .5)) Grant(PickUnowned(RollRarity(false)), at);
        }
        /// <summary>도감 완성이면 코어 +2 로 대체 (원본 infRelicGrant null 분기). 반환값 = 코어 보상.</summary>
        void Grant(RelicDef relic, Vec2 at)
        {
            if (relic == null) { Granted?.Invoke(new RelicGrantedEvent { Relic = null, At = at, DuplicateFallback = true }); return; }
            _pityBlocks = 0;
            Granted?.Invoke(new RelicGrantedEvent { Relic = relic, At = at });
        }

        /// <summary>원본 infRelicOnDescend — 도시락 12% 회복, 지층의 기억 스택.</summary>
        public void OnDescend()
        {
            if (!Active) return;
            if (Has("r_lunchbox")) { int heal = JsMath.Round(_player.HpMax * .12); _player.Hp = Math.Min(_player.HpMax, _player.Hp + heal); Fx?.Invoke(new RelicFxEvent { Kind = "heal", At = _player.Position, Label = $"도시락 +{heal}" }); }
            if (Has("r_strata")) { DepthStacks++; Fx?.Invoke(new RelicFxEvent { Kind = "label", At = _player.Position, Label = $"지층의 기억 ×{DepthStacks} (+{DepthStacks * 4}%)" }); }
        }

        // ───────────────────────────── 틱 (원본 infRelicTick)
        public void Tick(double dt, PlayerBuild build, bool playing)
        {
            _time += dt;
            if (TimeStopT > 0) { TimeStopT = Math.Max(0, TimeStopT - dt); if (TimeStopT <= 0) Fx?.Invoke(new RelicFxEvent { Kind = "timeResume", At = _player.Position, Radius = 3, Label = "시간 재개" }); }
            HourglassCd = Math.Max(0, HourglassCd - dt);
            RodCd = Math.Max(0, RodCd - dt);
            if (!Active) return;

            // 과부하 회로 — 재장전 완료 감지
            if (Has("r_overload"))
            {
                if (_prevReload > 0 && build.ReloadLeft <= 0) { VoltShots = 6; Fx?.Invoke(new RelicFxEvent { Kind = "label", At = _player.Position, Label = "과부하 회로 · 감전탄 6발" }); }
                _prevReload = build.ReloadLeft;
            }
            // 암반 피부 — 정지 시간
            if (Has("r_stoneskin"))
            {
                bool moving = _player.Velocity.Length > 4.0 / 50 || _player.DashActive;
                bool was = StillT >= 1;
                StillT = moving ? 0 : StillT + dt;
                if (!was && StillT >= 1) Fx?.Invoke(new RelicFxEvent { Kind = "stoneskin", At = _player.Position, Radius = .9, Label = "암반 피부" });
            }
            // 배수진 깃발 — 저체력 이동 보너스 토글
            if (Has("r_banner"))
            {
                bool on = _player.Hp <= _player.HpMax * .30;
                if (on != _lastStandOn) { build.MoveMul *= on ? 1.10 : 1 / 1.10; _lastStandOn = on; if (on) Fx?.Invoke(new RelicFxEvent { Kind = "banner", At = _player.Position, Radius = 1.4, Label = "배수진!" }); }
            }
            // 피의 계약 — 초당 0.8% 출혈 (HP 1 은 남긴다)
            if (Has("r_bloodpact") && playing)
            {
                _bloodTick += dt;
                while (_bloodTick >= 1) { _bloodTick -= 1; double drain = Math.Max(1, JsMath.Round(_player.HpMax * .008)); if (_player.Hp > 1) _player.Hp = Math.Max(1, _player.Hp - drain); }
            }
            // 태엽 수호자 — 드론
            if (HasDrone)
            {
                DroneBob += dt * 4;
                int face = _player.Velocity.X < -0.01 ? -1 : _player.Velocity.X > 0.01 ? 1 : DroneFace;
                var target = _player.Position + new Vec2(-face * 26.0 / 50, 40.0 / 50);
                DronePos += (target - DronePos) * Math.Min(1, dt * 4.2);
                DroneCd = Math.Max(0, DroneCd - dt);
                if (DroneCd <= 0 && !TimeStopped)
                {
                    EnemyState best = null; double bd = 6.5;
                    foreach (var o in _enemies.Enemies) { if (!o.Alive) continue; double d = Vec2.Distance(o.Position, DronePos); if (d < bd && SightUtil.IsClear(_world, DronePos, o.Position)) { bd = d; best = o; } }
                    if (best != null)
                    {
                        double a = (best.Position - DronePos).Angle;
                        DroneFace = Math.Cos(a) >= 0 ? 1 : -1; DroneAim = a; DroneCd = .55;
                        _fireSupport?.Invoke(DronePos + Vec2.FromAngle(a) * (12.0 / 50), a, "support", .55);
                    }
                }
            }
        }
    }
}
