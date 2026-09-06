using System;
using System.Collections.Generic;

namespace TunnelCrew.Sim
{
    public enum PingType : byte { Here = 0, Go, Attack, Find, Mine, Retreat, Defend, Help, Danger }

    public sealed class PingTypeDef
    {
        public PingType Type; public string Ko, Color, Glyph; public int Dir, Pri; public bool Cmd;
    }

    /// <summary>핑이 놓인 대상 (§4 컨텍스트). Kind: enemy / crew / escape / unknown / ore / wall / null(바닥).</summary>
    public sealed class PingContext
    {
        public string Kind, Name; public Vec2 At;
        public EnemyState Enemy; public CrewMember Member; public bool LeaderDown;
        public int C, R; public TileType Cell;
    }

    public sealed class PingMarker
    {
        public string Id; public PingType Type; public Vec2 At; public string Seat, Name; public PingContext Ctx;
        public double Born, Until, Pulse; public int Level = 1; public readonly HashSet<string> Agree = new HashSet<string>();
    }
    public sealed class PingLogLine { public string Key, Text, Color; public double T; public int N = 1; }
    public sealed class PingOrder
    {
        public PingType Type; public Vec2 At; public string Seat; public double Until, T0; public PingContext Ctx;
        public EnemyState Target; public int C, R;
        public readonly HashSet<int> Ids = new HashSet<int>(), Said = new HashSet<int>();
    }
    public sealed class PingZone { public Vec2 At; public double R, Until; }
    public struct PingEvent { public string Name; public PingType Type; public string Seat; }

    /// <summary>
    /// 팀 핑 — 원본 <c>TCPING</c> (ping/tc-ping.js · TEAM_PING_V1, docs/tunnel-crew-ping-system.md).
    /// 마커·로그·도배 제한·컨텍스트 판정·AI 명령 주입·AI 자발 핑을 Sim 에 두고, 휠 입력·그리기는 Presentation(TeamOverlay) 이 맡는다.
    /// 코옵 송수신은 M8 — 여기서는 로컬 좌석 p1 과 AI 좌석(ai{id})만 있다.
    /// </summary>
    public sealed class PingSystem
    {
        // ── 튠 상수 (기획서 §2.1 · §5 · §7)
        public const double Dur = 4, DurAlert = 5, TrackSec = 2.5, LabelSec = 1.2, PopSec = .16, RingSec = .45;
        public const double StackTiles = 1.5, AgreeExtend = 2, OffTiles = 30; public const int OffMax = 3;
        public const double LogSec = 3, LogMergeSec = 2; public const int LogMax = 3;
        public const int Charges = 4, BurstN = 6; public const double RegenSec = 2.5, BurstSec = 10, LockSec = 5, DownHelpSec = 6;
        public const double AiGoSec = 8, AiAttackSec = 10, AiMineSec = 6, AiRetreatSec = 5, AiHelpSec = 10, AiDangerSec = 5, AiDangerTiles = 3;

        public static readonly PingTypeDef[] Types =
        {
            new PingTypeDef { Type = PingType.Here, Ko = "여기", Color = "#5FB8FF", Glyph = "●", Dir = -1, Pri = 0 },
            new PingTypeDef { Type = PingType.Go, Ko = "가자", Color = "#5EE08A", Glyph = "▲", Dir = 0, Pri = 3, Cmd = true },
            new PingTypeDef { Type = PingType.Attack, Ko = "공격", Color = "#FF5A5A", Glyph = "✖", Dir = 1, Pri = 5, Cmd = true },
            new PingTypeDef { Type = PingType.Find, Ko = "발견", Color = "#FFD36E", Glyph = "◆", Dir = 2, Pri = 1 },
            new PingTypeDef { Type = PingType.Mine, Ko = "채굴", Color = "#FF9A3C", Glyph = "⛏", Dir = 3, Pri = 2, Cmd = true },
            new PingTypeDef { Type = PingType.Retreat, Ko = "후퇴", Color = "#FF4D6D", Glyph = "▼", Dir = 4, Pri = 6 },
            new PingTypeDef { Type = PingType.Defend, Ko = "방어", Color = "#B98CFF", Glyph = "⬢", Dir = 5, Pri = 4, Cmd = true },
            new PingTypeDef { Type = PingType.Help, Ko = "도움", Color = "#5FF5E0", Glyph = "✚", Dir = 6, Pri = 8 },
            new PingTypeDef { Type = PingType.Danger, Ko = "위험", Color = "#E8194B", Glyph = "⚠", Dir = 7, Pri = 7 },
        };
        /// <summary>휠 방향 — 0=↑ 부터 시계방향 45° 씩. 위=전진, 아래=후퇴, 왼쪽=도움, 오른쪽=발견.</summary>
        public static readonly PingType[] Dirs = { PingType.Go, PingType.Attack, PingType.Find, PingType.Mine, PingType.Retreat, PingType.Defend, PingType.Help, PingType.Danger };
        public static PingTypeDef Def(PingType t) => Types[(int)t];
        public const string MySeat = "p1", MyName = "나";

        readonly TunnelSim _sim; readonly Rng _rng;
        public readonly List<PingMarker> Markers = new List<PingMarker>();
        public readonly List<PingLogLine> Log = new List<PingLogLine>();
        public readonly List<PingOrder> Orders = new List<PingOrder>();
        public readonly List<PingZone> Zones = new List<PingZone>();
        public double Charge = Charges, ChargeT; readonly List<double> _attempts = new List<double>(); public double LockUntil = -1e9, LastDownHelp = -1e9;
        public string Note; public double NoteUntil;
        int _seq;
        /// <summary>완전 숨김 — 다른 크루의 핑을 표시하지 않는다 · 구조 요청은 유지 (§7.2).</summary>
        public bool HideOthers;
        public event Action<PingType, Vec2> Sound;
        public event Action<PingEvent> Emitted;

        public PingSystem(TunnelSim sim, uint seed = 0x9137) { _sim = sim; _rng = new Rng(seed); }

        double Now => _sim.RunTime;
        WorldGrid W => _sim.World;
        PlayerState P => _sim.Player;
        bool Playing => W != null && _sim.Phase == GamePhase.Playing;
        bool Seen(Vec2 p) { var (c, r) = WorldGrid.ToCell(p); return _sim.Los == null || _sim.Los.IsSeen(c, r); }
        Vec2 MapClamp(Vec2 p) => new Vec2(JsMath.Clamp(p.X, .5, W.Cols - .5), JsMath.Clamp(p.Y, .5, W.Rows - .5));
        void Emit(string name, PingType type, string seat) => Emitted?.Invoke(new PingEvent { Name = name, Type = type, Seat = seat });

        static readonly Dictionary<TileType, string> CellKo = new Dictionary<TileType, string>
        {
            [TileType.Ore] = "광맥", [TileType.Gem] = "보석 광맥", [TileType.Crys] = "수정 광맥", [TileType.Stone] = "단단한 암반", [TileType.Dirt] = "흙벽", [TileType.Rock] = "기반암", [TileType.Core] = "기반암",
        };
        public static string EnemyName(EnemyState e, TunnelSim sim)
        {
            if (e.IsBoss) return sim.Bosses != null && sim.Bosses.Boss != null && sim.Bosses.Boss.Body == e ? sim.Bosses.Boss.Def.Name : "보스";
            return (e.IsApex ? "정예 " : "") + (e.IsRanged ? "독침벌레" : "굴벌레");
        }

        // ── 컨텍스트 판정 (§4)
        public PingContext ResolveContext(Vec2 w)
        {
            EnemyState best = null; double bd = 1e9;
            foreach (var e in _sim.Enemies.Enemies)
            {
                if (!e.Alive) continue;
                double d = Vec2.Distance(e.Position, w);
                if (d < Math.Max(e.Radius + .2, .9) && d < bd && Seen(e.Position)) { best = e; bd = d; }
            }
            if (best != null) return new PingContext { Kind = "enemy", Enemy = best, Name = EnemyName(best, _sim), At = best.Position };
            if (P.Downed && Vec2.Distance(P.Position, w) < 1.3) return new PingContext { Kind = "crew", LeaderDown = true, Name = "리더 구조", At = P.Position };
            foreach (var m in _sim.Crew.Members)
                if (m.Down && Vec2.Distance(m.Position, w) < 1.3) return new PingContext { Kind = "crew", Member = m, Name = $"AI {AiCrewSystem.NameOf(m.Role)} 구조", At = m.Position };
            var esc = _sim.Escape;
            if (esc.Active && esc.Phase != EscapePhase.Placing && Vec2.Distance(esc.Position, w) < 1.7) return new PingContext { Kind = "escape", Name = "탈출 포트", At = esc.Position };
            if (!Seen(w)) return new PingContext { Kind = "unknown", Name = "알 수 없는 위치", At = w };
            var (c, r) = WorldGrid.ToCell(w);
            if (W.InBounds(c, r))
            {
                var t = W.At(c, r);
                if (t != TileType.Empty && CellKo.TryGetValue(t, out var ko))
                    return new PingContext { Kind = t == TileType.Ore || t == TileType.Gem || t == TileType.Crys ? "ore" : "wall", Cell = t, C = c, R = r, Name = ko, At = WorldGrid.CellCenter(c, r) };
            }
            return null;
        }

        // ── 도배 제한 (§7.1)
        void TickCharges(double dt)
        {
            if (Charge < Charges) { ChargeT += dt; while (ChargeT >= RegenSec && Charge < Charges) { ChargeT -= RegenSec; Charge++; } }
            else ChargeT = 0;
        }
        (bool ok, string reason, double wait, bool downHelp) Gate(PingType type)
        {
            double t = Now;
            if (P.Downed)
            {
                if (type != PingType.Help) return (false, "down", 0, false);
                if (t - LastDownHelp < DownHelpSec) return (false, "down_cd", DownHelpSec - (t - LastDownHelp), false);
                return (true, null, 0, true);
            }
            if (t < LockUntil) return (false, "locked", LockUntil - t, false);
            _attempts.RemoveAll(a => t - a >= BurstSec); _attempts.Add(t);
            if (_attempts.Count >= BurstN) { LockUntil = t + LockSec; _attempts.Clear(); return (false, "burst", LockSec, false); }
            if (Charge < 1) return (false, "charge", RegenSec - ChargeT, false);
            return (true, null, 0, false);
        }
        public bool Locked => Now < LockUntil;
        public double LockLeft => Math.Max(0, LockUntil - Now);
        public double ChargeLeft => RegenSec - ChargeT;

        // ── 마커 (§5.1 · §6.1)
        PingMarker FindStack(PingType type, Vec2 at) { foreach (var m in Markers) if (m.Type == type && Vec2.Distance(m.At, at) < StackTiles) return m; return null; }
        PingMarker FindAny(Vec2 at, string notSeat)
        {
            PingMarker best = null; double bd = 1e9;
            foreach (var m in Markers) { double d = Vec2.Distance(m.At, at); if (d < StackTiles && d < bd && m.Seat != notSeat) { best = m; bd = d; } }
            return best;
        }
        PingMarker AddMarker(string id, PingType type, Vec2 at, string seat, string name, PingContext ctx)
        {
            double t = Now;
            var stack = FindStack(type, at);
            if (stack != null)
            {
                stack.Level = Math.Min(3, stack.Level + 1); stack.Until = Math.Max(stack.Until, t + Math.Min(Dur, (stack.Until - t) + 1)); stack.Pulse = t;
                PushLog(seat, name, type, ctx); Sound?.Invoke(type, at); return stack;
            }
            double dur = type == PingType.Danger || type == PingType.Retreat ? DurAlert : Dur;
            var m = new PingMarker { Id = id, Type = type, At = at, Seat = seat, Name = name, Ctx = ctx, Born = t, Until = t + dur };
            Markers.Add(m);
            PushLog(seat, name, type, ctx); Sound?.Invoke(type, at);
            Emit("ping_show", type, seat);
            return m;
        }
        bool AgreeMarker(string id, string seat)
        {
            var m = Markers.Find(k => k.Id == id); if (m == null) return false;
            if (m.Agree.Add(seat)) { m.Until = Math.Min(m.Until + AgreeExtend, m.Born + DurAlert + AgreeExtend); m.Pulse = Now; }
            Sound?.Invoke(PingType.Here, m.At); return true;
        }
        public void RemoveMarker(string id) => Markers.RemoveAll(m => m.Id == id);
        void TickMarkers()
        {
            double t = Now;
            foreach (var m in Markers)
            {
                var c = m.Ctx; if (c == null) continue;
                if (c.Kind == "enemy" && c.Enemy != null)
                {
                    if (c.Enemy.Alive && t - m.Born < TrackSec) m.At = c.Enemy.Position;
                    else if (!c.Enemy.Alive) m.Until = Math.Min(m.Until, t + .6);   // 대상 사망 — 조기 종료
                }
                if (c.Kind == "crew")
                {
                    bool up = c.LeaderDown ? !P.Downed : c.Member != null && !c.Member.Down;
                    if (up) m.Until = Math.Min(m.Until, t + .6);
                }
            }
            Markers.RemoveAll(m => m.Until <= t);
            if (HideOthers) Markers.RemoveAll(m => m.Type != PingType.Help && m.Seat != MySeat);
        }

        // ── 로그 (§5.3)
        public static string LogText(string name, PingType type, PingContext ctx)
        {
            string ko = Def(type).Ko;
            return name + ": " + (ctx != null && ctx.Name != null && ctx.Kind != "unknown" ? ctx.Name + " " + ko : ctx != null && ctx.Kind == "unknown" ? "알 수 없는 위치 " + ko : ko);
        }
        void PushLog(string seat, string name, PingType type, PingContext ctx)
        {
            double t = Now; string key = seat + "|" + type;
            var last = Log.Count > 0 ? Log[Log.Count - 1] : null;
            if (last != null && last.Key == key && t - last.T < LogMergeSec) { last.N++; last.T = t; return; }
            Log.Add(new PingLogLine { Key = key, Text = LogText(name, type, ctx), T = t, Color = Def(type).Color });
            while (Log.Count > LogMax) Log.RemoveAt(0);
        }

        void RejectNote(string reason, double wait)
        {
            string w = wait > 0 ? Math.Max(.1, wait).ToString("F1") + "초" : "";
            Note = reason == "locked" || reason == "burst" ? "핑 잠금 " + w : reason == "charge" ? "핑 충전 " + w : reason == "down" ? "기절 중 — 도움 핑만" : reason == "down_cd" ? "도움 핑 " + w : "";
            NoteUntil = Now + 1.4;
            Emit("ping_rejected", PingType.Here, MySeat);
        }

        /// <summary>사람 핑 송신 (원본 sendPing). quick=true(V 키)면 취소·동의 판정을 건너뛴다.</summary>
        public bool Send(PingType type, Vec2 world, bool viaQuick)
        {
            if (!Playing) return false;
            var w = MapClamp(world);
            // 자기 마커 위 G 탭 = 취소 · 다른 크루 마커 위 기본 핑 = 동의 (충전을 쓰지 않는다)
            if (type == PingType.Here && !viaQuick)
            {
                var own = Markers.Find(m => m.Seat == MySeat && Vec2.Distance(m.At, w) < StackTiles);
                if (own != null) { RemoveMarker(own.Id); Emit("ping_cancel", type, MySeat); return true; }
                var other = FindAny(w, MySeat);
                if (other != null) { AgreeMarker(other.Id, MySeat); Emit("ping_agree", other.Type, MySeat); return true; }
            }
            var gt = Gate(type);
            if (!gt.ok) { RejectNote(gt.reason, gt.wait); return false; }
            if (gt.downHelp) LastDownHelp = Now; else Charge -= 1;
            var ctx = ResolveContext(w);
            var at = ctx != null ? ctx.At : w;
            string id = $"{MySeat}-{++_seq}";
            AddMarker(id, type, at, MySeat, MyName, ctx);
            AiOnPing(type, at, MySeat, ctx);
            Emit("ping_send", type, MySeat);
            return true;
        }

        // ── AI 반응 (§6.2) — 명령형 핑은 orders 에 쌓이고, Crew.Tick 앞에서 각 멤버의 goal 로 주입된다
        void AiOnPing(PingType type, Vec2 at, string seat, PingContext ctx)
        {
            var crew = _sim.Crew; if (crew.Members.Count == 0) return;
            double t = Now;
            if (Def(type).Cmd) Orders.RemoveAll(o => o.Seat == seat && Def(o.Type).Cmd);   // 같은 좌석의 이전 명령형 핑을 교체
            var o = new PingOrder { Type = type, At = at, Seat = seat, T0 = t, Ctx = ctx };
            switch (type)
            {
                case PingType.Go: o.Until = t + AiGoSec; Orders.Add(o); break;
                case PingType.Attack:
                    if (ctx != null && ctx.Kind == "enemy" && ctx.Enemy != null) { o.Target = ctx.Enemy; o.Until = t + AiAttackSec; Orders.Add(o); } else AiSayAll("대상 없음");
                    break;
                case PingType.Mine:
                {
                    int c, r;
                    if (ctx != null && (ctx.Kind == "ore" || ctx.Kind == "wall")) { c = ctx.C; r = ctx.R; } else (c, r) = WorldGrid.ToCell(at);
                    var tp = W.InBounds(c, r) ? W.At(c, r) : TileType.Rock;
                    if (tp != TileType.Empty && !TileTypes.IsBedrock(tp)) { o.C = c; o.R = r; o.At = WorldGrid.CellCenter(c, r); o.Until = t + AiMineSec; Orders.Add(o); }
                    else AiSayAll("대상 없음");
                    break;
                }
                case PingType.Retreat: o.Until = t + AiRetreatSec; Orders.Add(o); break;
                case PingType.Defend: o.Until = t + AiGoSec; Orders.Add(o); break;
                case PingType.Find: o.Until = t + AiMineSec; Orders.Add(o); break;
                case PingType.Help: o.Until = t + AiHelpSec; Orders.Add(o); break;
                case PingType.Danger:
                    Zones.Add(new PingZone { At = at, R = AiDangerTiles, Until = t + AiDangerSec });
                    var (c0, r0) = WorldGrid.ToCell(at);
                    for (int dr = -(int)AiDangerTiles; dr <= AiDangerTiles; dr++) for (int dc = -(int)AiDangerTiles; dc <= AiDangerTiles; dc++)
                        if (Math.Sqrt(dc * dc + dr * dr) <= AiDangerTiles && W.InBounds(c0 + dc, r0 + dr)) crew.Geo.Ban(c0 + dc, r0 + dr, t, AiDangerSec);
                    break;
            }
        }
        void AiSayAll(string text) { foreach (var m in _sim.Crew.Members) if (!m.Down) { m.Say = text; m.SayT = 1.6; } }
        PingZone InZone(Vec2 p) { double t = Now; foreach (var z in Zones) if (z.Until > t && Vec2.Distance(z.At, p) < z.R) return z; return null; }

        /// <summary>핑보다 우선하는 기존 최상위 판단 — 이 조건이면 decide 에 맡긴다.</summary>
        bool AiBusy(CrewMember m, PingOrder o)
        {
            if (m.Down) return true;
            if (P.Downed && o.Type != PingType.Help) return true;
            var esc = _sim.Escape; if (esc.Phase == EscapePhase.Incoming || esc.Phase == EscapePhase.Ready) return true;
            foreach (var x in _sim.Crew.Members) if (x != m && x.Down && Vec2.Distance(x.Position, m.Position) < 14 && o.Type != PingType.Help) return true;
            if (o.Type != PingType.Attack && o.Type != PingType.Retreat)
                foreach (var e in _sim.Enemies.Enemies) if (e.Alive && Vec2.Distance(e.Position, m.Position) < 4) return true;
            return false;
        }
        List<CrewMember> PickMembers(PingOrder o)
        {
            var alive = new List<CrewMember>(); foreach (var m in _sim.Crew.Members) if (!m.Down) alive.Add(m);
            var out_ = new List<CrewMember>();
            if (alive.Count == 0) return out_;
            List<CrewMember> Near() { var l = new List<CrewMember>(alive); l.Sort((a, b) => Vec2.Distance(a.Position, o.At).CompareTo(Vec2.Distance(b.Position, o.At))); return l; }
            switch (o.Type)
            {
                case PingType.Go: case PingType.Retreat: case PingType.Attack: return alive;
                case PingType.Mine:
                {
                    var d = alive.Find(m => m.Role == RoleId.Driller);
                    if (d != null) { out_.Add(d); return out_; }
                    var n = Near(); var nd = n.Find(m => m.Role != RoleId.Gunner) ?? n[0]; out_.Add(nd); return out_;
                }
                case PingType.Defend:
                {
                    foreach (var m in alive) if (m.Role == RoleId.Gunner || m.Role == RoleId.Engineer) out_.Add(m);
                    if (out_.Count == 0) out_.Add(Near()[0]); return out_;
                }
                case PingType.Find: out_.Add(Near()[0]); return out_;
                case PingType.Help: { var n = Near(); for (int i = 0; i < Math.Min(n.Count, alive.Count >= 3 ? 2 : 1); i++) out_.Add(n[i]); return out_; }
            }
            return out_;
        }
        CrewGoal OrderGoal(CrewMember m, PingOrder o)
        {
            string label = "핑 · " + Def(o.Type).Ko;
            switch (o.Type)
            {
                case PingType.Go: case PingType.Defend: case PingType.Find:
                    if (Vec2.Distance(o.At, m.Position) < 1.2) return null;   // 도착 — 해제
                    return new CrewGoal { Kind = CrewGoalKind.Follow, At = o.At, Label = label };
                case PingType.Attack:
                    if (o.Target == null || !o.Target.Alive || !_sim.Enemies.Enemies.Contains(o.Target)) { o.Until = 0; return null; }
                    return new CrewGoal { Kind = CrewGoalKind.Fight, At = o.Target.Position, Enemy = o.Target, Boss = o.Target.IsBoss, Label = label };
                case PingType.Mine:
                {
                    var tp = W.At(o.C, o.R);
                    if (tp == TileType.Empty || TileTypes.IsBedrock(tp)) { o.Until = 0; return null; }
                    var g = new CrewGoal { Kind = CrewGoalKind.Mine, At = o.At, C = o.C, R = o.R, Label = label };
                    m.MineTarget = g; m.MineUntil = Now + 6;
                    return g;
                }
                case PingType.Retreat:
                {
                    // 핑 반대편 — 리더 쪽으로 3칸 이탈
                    var d = P.Position - o.At; double L = Math.Max(1e-6, d.Length); d /= L;
                    var t = new Vec2(JsMath.Clamp(P.Position.X + d.X * 3, 1, W.Cols - 1), JsMath.Clamp(P.Position.Y + d.Y * 3, 1, W.Rows - 1));
                    if (Vec2.Distance(o.At, m.Position) > 6) return null;
                    return new CrewGoal { Kind = CrewGoalKind.Follow, At = t, Label = label };
                }
                case PingType.Help:
                {
                    var c = o.Ctx; Vec2 at = o.At; bool down = true; CrewMember target = null;
                    if (c != null && c.Kind == "crew")
                    {
                        if (c.LeaderDown) { at = P.Position; down = P.Downed; }
                        else if (c.Member != null) { at = c.Member.Position; down = c.Member.Down; target = c.Member; }
                    }
                    else if (P.Downed) at = P.Position;
                    if (!down) { o.Until = 0; return null; }
                    return new CrewGoal { Kind = CrewGoalKind.Revive, At = at, ReviveTarget = target, Label = label };
                }
            }
            return null;
        }

        void AiApply()
        {
            var crew = _sim.Crew; if (crew.Members.Count == 0 || !crew.Enabled) return;
            double t = Now;
            Orders.RemoveAll(o => o.Until <= t); Zones.RemoveAll(z => z.Until <= t);
            foreach (var o in Orders)
            {
                foreach (var m in PickMembers(o))
                {
                    if (AiBusy(m, o)) continue;
                    // 이미 다른 명령을 수행 중인 멤버는 더 중요한 핑만 가로챈다
                    bool held = false; foreach (var q in Orders) if (q != o && q.Ids.Contains(m.Id) && Def(q.Type).Pri >= Def(o.Type).Pri) { held = true; break; }
                    if (held) continue;
                    if (o.Ids.Contains(m.Id) && m.Goal == null && o.Type == PingType.Mine) { o.Until = 0; m.Say = "대상 없음"; m.SayT = 1.6; Emit("ai_ping_fail", o.Type, o.Seat); break; }
                    var g = OrderGoal(m, o);
                    if (g == null) { if (o.Ids.Remove(m.Id)) Emit("ai_ping_done", o.Type, o.Seat); continue; }
                    if (o.Said.Add(m.Id)) { m.Say = "✓ " + Def(o.Type).Ko; m.SayT = 1.8; Emit("ai_ping_accept", o.Type, o.Seat); }
                    o.Ids.Add(m.Id);
                    m.Goal = g; m.React = .3;   // decide 를 건너뛰고 act 가 이 goal 을 수행한다
                }
            }
            // 위험 구역 안의 멤버는 밖으로 — 전투 중이 아닐 때만
            foreach (var m in crew.Members)
            {
                if (m.Down) continue;
                var z = InZone(m.Position);
                if (z == null || (m.Goal != null && m.Goal.Kind == CrewGoalKind.Fight)) continue;
                var d = m.Position - z.At; double L = Math.Max(1e-6, d.Length); d /= L;
                m.Goal = new CrewGoal { Kind = CrewGoalKind.Follow, At = new Vec2(JsMath.Clamp(z.At.X + d.X * (z.R + 1), 1, W.Cols - 1), JsMath.Clamp(z.At.Y + d.Y * (z.R + 1), 1, W.Rows - 1)), Label = "핑 · 위험 회피" };
                m.React = .3; m.MineTarget = null;
            }
        }

        // ── AI 크루의 자발적 핑 (관전 연출) — 표시·로그·소리만. 사람의 충전을 쓰지 않고 다른 AI 에게 명령으로 작동하지 않는다
        public double AiGap = 4, AiCdMin = 10, AiCdMax = 22, AiPerSec = .35;
        double _aiGlobalNext; int _aiRot; readonly Dictionary<int, double> _aiCd = new Dictionary<int, double>(); readonly Dictionary<int, string> _aiOre = new Dictionary<int, string>();
        PingContext CtxForCell(int c, int r)
        {
            if (!W.InBounds(c, r)) return null;
            var t = W.At(c, r); if (t == TileType.Empty || !CellKo.TryGetValue(t, out var ko)) return null;
            return new PingContext { Kind = t == TileType.Ore || t == TileType.Gem || t == TileType.Crys ? "ore" : "wall", Cell = t, C = c, R = r, Name = ko, At = WorldGrid.CellCenter(c, r) };
        }
        void AiSend(CrewMember m, PingType type, Vec2 at, PingContext ctx, double t)
        {
            _aiCd[m.Id] = t + AiCdMin + _rng.NextDouble() * (AiCdMax - AiCdMin);
            _aiGlobalNext = t + AiGap;
            if (HideOthers && type != PingType.Help) return;
            string seat = "ai" + m.Id, name = "AI " + AiCrewSystem.NameOf(m.Role);
            AddMarker($"{seat}-{++_seq}", type, MapClamp(at), seat, name, ctx);
            Emit("ai_ping", type, seat);
        }
        void AiAutoPing(double t, double dt)
        {
            var M = _sim.Crew.Members;
            if (M.Count == 0 || t < _aiGlobalNext || !Playing) return;
            int N = M.Count; _aiRot = (_aiRot + 1) % Math.Max(1, N);
            for (int k = 0; k < N; k++)
            {
                var m = M[(k + _aiRot) % N];
                if (!_aiCd.TryGetValue(m.Id, out double cd)) { cd = t + 6 + _rng.NextDouble() * 8; _aiCd[m.Id] = cd; }   // 합류 직후엔 잠시 조용히
                if (t < cd) continue;
                if (m.Down) { AiSend(m, PingType.Help, m.Position, new PingContext { Kind = "crew", Member = m, Name = $"AI {AiCrewSystem.NameOf(m.Role)} 구조", At = m.Position }, t); _aiCd[m.Id] = t + 6; return; }
                var g = m.Goal; if (g == null || (g.Label != null && g.Label.StartsWith("핑 ·"))) continue;
                (PingType type, Vec2 at, PingContext ctx)? pick = null;
                if (g.Kind == CrewGoalKind.Fight && g.Enemy != null && g.Enemy.Alive)
                {
                    var e = g.Enemy;
                    pick = (e.IsBoss || e.IsApex ? PingType.Danger : PingType.Attack, e.Position, new PingContext { Kind = "enemy", Enemy = e, Name = EnemyName(e, _sim), At = e.Position });
                }
                else if (g.Kind == CrewGoalKind.Mine)
                {
                    var ctx = CtxForCell(g.C, g.R); string key = g.C + "," + g.R;
                    if (ctx != null && ctx.Kind == "ore") pick = (_aiOre.TryGetValue(m.Id, out var prev) && prev == key ? PingType.Mine : PingType.Find, ctx.At, ctx);
                    else if (ctx != null && _rng.NextDouble() < .3) pick = (PingType.Mine, ctx.At, ctx);
                    if (pick != null) _aiOre[m.Id] = key;
                }
                else if (g.Kind == CrewGoalKind.Flare) pick = (PingType.Here, g.At, null);
                else if (g.Kind == CrewGoalKind.Turret || g.Kind == CrewGoalKind.Node) pick = (PingType.Defend, m.Position, null);
                else if (g.Kind == CrewGoalKind.Revive) pick = (PingType.Help, g.At, ResolveContext(g.At));
                else if (g.Kind == CrewGoalKind.Escape) pick = (PingType.Go, g.At, new PingContext { Kind = "escape", Name = "탈출 포트", At = g.At });
                else if (g.Kind == CrewGoalKind.Follow && Vec2.Distance(g.At, m.Position) > 6) pick = (PingType.Go, g.At, ResolveContext(g.At));
                if (pick == null) continue;
                if (_rng.NextDouble() > AiPerSec * dt) continue;   // "가끔" — 상황이 이어지는 동안 초당 35%
                AiSend(m, pick.Value.type, pick.Value.at, pick.Value.ctx, t);
                return;
            }
        }

        /// <summary>매 틱 — 충전·마커·AI 명령 주입·AI 자발 핑. Crew.Tick 직전에 호출한다.</summary>
        public void Tick(double dt)
        {
            if (W == null) return;
            TickCharges(dt); TickMarkers();
            Log.RemoveAll(l => Now - l.T >= LogSec);
            if (Note != null && NoteUntil <= Now) Note = null;
            if (!Playing) return;
            AiApply();
            AiAutoPing(Now, dt);
        }

        public void Reset() { Markers.Clear(); Log.Clear(); Orders.Clear(); Zones.Clear(); Charge = Charges; ChargeT = 0; LockUntil = -1e9; _attempts.Clear(); Note = null; _aiCd.Clear(); _aiOre.Clear(); _aiGlobalNext = 0; }
    }
}
