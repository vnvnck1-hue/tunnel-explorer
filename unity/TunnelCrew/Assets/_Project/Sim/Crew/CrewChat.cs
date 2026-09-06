using System;
using System.Collections.Generic;

namespace TunnelCrew.Sim
{
    public sealed class ChatMessage { public string Seat, Name, Text; public double T; public bool Local; }
    public sealed class ChatBubble { public string Text, Name; public double T; }

    /// <summary>
    /// 크루 채팅 — 원본 <c>TCCHAT</c> (chat/tc-chat.js · TEAM_CHAT_V1, docs/tunnel-crew-chat-system.md).
    /// 로그·말풍선·AI 크루 멘트(검수 10문장) 규칙을 Sim 에 두고, 입력창·그리기는 Presentation 이 맡는다. 코옵 전송은 M8.
    /// </summary>
    public sealed class CrewChat
    {
        public const int MaxLen = 80, IdleLines = 3, OpenLines = 8, HistMax = 80;
        public const double SendGapSec = .25, BubbleSec = 5, BubbleFade = .5, BubblePop = .14, IdleFadeSec = 10;
        // AI 크루 멘트 — 전체 15~30초에 한 줄(긴급은 6초), 같은 문장 한 판 2번, 같은 크루 20초, 한산함 20초/60초
        public const double AiGapMin = 15, AiGapMax = 30, AiGapUrgent = 6, AiMemberCd = 20, AiIdleSec = 20, AiIdleCd = 60; public const int AiPerLine = 2;
        public enum AiChatMode : byte { Always, Observer, Off }
        public AiChatMode AiChat = AiChatMode.Always;

        public readonly List<ChatMessage> Log = new List<ChatMessage>();
        public readonly Dictionary<string, ChatBubble> Bubbles = new Dictionary<string, ChatBubble>();
        public event Action<ChatMessage> Posted;

        readonly TunnelSim _sim; readonly Rng _rng; double _lastSend = -1e9;
        public const string MySeat = "p1", MyName = "나";
        public static string SeatColor(string seat) => seat != null && seat.StartsWith("ai:") ? "#C9C9D6" : seat == "p1" ? "#FFD36E" : seat == "p2" ? "#7FEBD0" : seat == "p3" ? "#FF8D72" : seat == "p4" ? "#C7A0FF" : "#f6efff";

        public CrewChat(TunnelSim sim, uint seed = 0xC4A7) { _sim = sim; _rng = new Rng(seed); }
        double Now => _sim.RunTime;

        public static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder(); bool space = false;
            foreach (var ch in s)
            {
                bool ws = ch < 0x20 || ch == 0x7f || char.IsWhiteSpace(ch);
                if (ws) { space = true; continue; }
                if (space && sb.Length > 0) sb.Append(' ');
                space = false; sb.Append(ch);
            }
            var t = sb.ToString();
            return t.Length > MaxLen ? t.Substring(0, MaxLen) : t;
        }

        public ChatMessage Post(string seat, string name, string text, bool local)
        {
            var msg = new ChatMessage { Seat = seat, Name = name, Text = text, T = Now, Local = local };
            Log.Add(msg); if (Log.Count > HistMax) Log.RemoveAt(0);
            Bubbles[seat] = new ChatBubble { Text = text, T = Now, Name = name };
            Posted?.Invoke(msg);
            return msg;
        }
        /// <summary>사람 전송 (원본 submit). 빈 문장·연속 전송 간격 미달이면 false.</summary>
        public bool Say(string raw)
        {
            var text = Clean(raw);
            if (text.Length == 0) return false;
            if (Now - _lastSend < SendGapSec) return false;
            _lastSend = Now;
            Post(MySeat, MyName, text, true);
            return true;
        }

        public void Tick(double dt)
        {
            var dead = new List<string>();
            foreach (var kv in Bubbles) if (Now - kv.Value.T >= BubbleSec) dead.Add(kv.Key);
            foreach (var k in dead) Bubbles.Remove(k);
            AiTick(dt);
        }
        public void Reset() { Log.Clear(); Bubbles.Clear(); _aiOn = false; }

        // ── AI 크루 멘트 — 상태 전이를 보고 승인된 10개 문장을 낸다
        public static readonly (string key, string text, bool urgent)[] AiLines =
        {
            ("ore", "여기 광맥 있다, 이쪽으로 와", false),
            ("reload", "총알 다 떨어졌어, 잠깐만", false),
            ("chased", "내 뒤에 벌레 붙었어 ㅋㅋ", false),
            ("hard", "이 벽은 단단하네… 좀 걸린다", false),
            ("lowhp", "잠깐, 나 피 없어", true),
            ("down", "야 누가 나 좀 일으켜줘", true),
            ("revived", "됐다, 살렸어", true),
            ("boss", "보스다… 다들 흩어져", true),
            ("escape", "탈출 포트 열렸어, 슬슬 가자", false),
            ("idle", "여긴 너무 조용한데, 더 내려갈까?", false),
        };
        sealed class AiMem { public double LastSay = -1e9, Chased = -1e9, Dig; public CrewGoal Mine; public bool Reload, Down, DownSaid, HardSaid, LowSaid; }
        bool _aiOn; double _aiNextAt, _aiLastSayAt = -1e9, _aiLastAct, _aiLastIdle; bool _bossSeen, _escapeSeen, _leaderDown;
        readonly Dictionary<string, int> _aiUsed = new Dictionary<string, int>(); readonly Dictionary<int, AiMem> _aiMem = new Dictionary<int, AiMem>();

        bool AiEnabled()
        {
            if (AiChat == AiChatMode.Off || _sim.World == null || _sim.Phase != GamePhase.Playing) return false;
            if (_sim.Crew.Members.Count == 0) return false;
            if (AiChat == AiChatMode.Observer) return false;   // 관전 모드는 미포팅
            return true;
        }
        AiMem Mem(CrewMember m) { if (!_aiMem.TryGetValue(m.Id, out var st)) _aiMem[m.Id] = st = new AiMem(); return st; }
        void AiReset()
        {
            _aiNextAt = Now + _rng.Range(6, 12); _aiLastSayAt = -1e9; _aiUsed.Clear(); _aiLastAct = Now; _aiLastIdle = Now;
            _bossSeen = false; _escapeSeen = false; _leaderDown = _sim.Player.Downed; _aiMem.Clear();
        }
        public bool AiSay(CrewMember m, string key)
        {
            int li = Array.FindIndex(AiLines, l => l.key == key); if (li < 0) return false;
            var L = AiLines[li]; double t = Now;
            _aiUsed.TryGetValue(key, out int used); if (used >= AiPerLine) return false;
            if (t < _aiNextAt && !(L.urgent && t - _aiLastSayAt >= AiGapUrgent)) return false;
            var st = Mem(m); if (t - st.LastSay < AiMemberCd && !L.urgent) return false;
            _aiUsed[key] = used + 1; _aiNextAt = t + _rng.Range(AiGapMin, AiGapMax); _aiLastSayAt = t; st.LastSay = t;
            Post("ai:" + m.Id, "AI " + AiCrewSystem.NameOf(m.Role), L.text, false);
            return true;
        }
        CrewMember NearestAi(Vec2 at, double r, CrewMember except)
        {
            CrewMember best = null; double bd = r;
            foreach (var m in _sim.Crew.Members) { if (m == except || m.Down) continue; double d = Vec2.Distance(m.Position, at); if (d < bd) { bd = d; best = m; } }
            return best;
        }
        void AiTick(double dt)
        {
            bool on = AiEnabled();
            if (on != _aiOn) { _aiOn = on; if (on) AiReset(); }
            if (!on) return;
            double t = Now; var M = _sim.Crew.Members;
            var alive = new List<CrewMember>(); foreach (var m in M) if (!m.Down) alive.Add(m);
            CrewMember Pick() => alive.Count > 0 ? alive[_rng.NextInt(alive.Count)] : null;
            bool action = false;
            // 8) 보스 등장
            bool boss = _sim.Bosses != null && _sim.Bosses.Active;
            if (boss && !_bossSeen) { var m = Pick(); if (m != null && AiSay(m, "boss")) _bossSeen = true; }
            if (!boss) _bossSeen = false;
            // 9) 탈출 포트
            bool esc = _sim.Escape.Active && _sim.Escape.Phase != EscapePhase.Placing;
            if (esc && !_escapeSeen) { var m = Pick(); if (m != null && AiSay(m, "escape")) _escapeSeen = true; }
            if (!esc) _escapeSeen = false;
            // 7) 리더 부활 — 가장 가까운 AI 가 말한다
            bool ld = _sim.Player.Downed;
            if (_leaderDown && !ld) { var m = NearestAi(_sim.Player.Position, 2.5, null); if (m != null) AiSay(m, "revived"); }
            _leaderDown = ld;
            foreach (var m in M)
            {
                var st = Mem(m);
                if (m.Down) { if (!st.DownSaid && AiSay(m, "down")) st.DownSaid = true; st.Down = true; }
                else if (st.Down) { st.Down = false; st.DownSaid = false; var r = NearestAi(m.Position, 2.5, m); if (r != null) AiSay(r, "revived"); }
                if (m.Down) continue;
                // 1) 새 광맥 채굴 시작
                var mt = m.MineTarget;
                if (mt != null && mt != st.Mine && AiSay(m, "ore")) st.Mine = mt;
                if (mt == null) st.Mine = null;
                // 2) 재장전 시작
                bool rl = m.ReloadLeft > 0;
                if (rl && !st.Reload && (m.Role == RoleId.Gunner || m.Role == RoleId.Scout)) AiSay(m, "reload");
                st.Reload = rl;
                // 3) 적 2마리 이상 근접
                int near = 0; foreach (var e in _sim.Enemies.Enemies) if (e.Alive && Vec2.Distance(e.Position, m.Position) < 3.5) near++;
                if (near >= 2 && t - st.Chased > 30 && AiSay(m, "chased")) st.Chased = t;
                if (near > 0) action = true;
                // 4) 단단한 벽 3초 이상
                if (m.Digging) { st.Dig += dt; action = true; if (st.Dig >= 3 && !st.HardSaid && AiSay(m, "hard")) st.HardSaid = true; }
                else { st.Dig = 0; st.HardSaid = false; }
                // 5) 저체력 진입
                bool low = m.HpMax > 0 && m.Hp / m.HpMax < .3;
                if (low && !st.LowSaid && AiSay(m, "lowhp")) st.LowSaid = true;
                if (!low) st.LowSaid = false;
                if (m.GunCd > 0) action = true;
            }
            // 10) 한산함
            if (action || boss) _aiLastAct = t;
            if (t - _aiLastAct > AiIdleSec && t - _aiLastIdle > AiIdleCd) { _aiLastIdle = t; var m = Pick(); if (m != null) AiSay(m, "idle"); }
        }
    }
}
