using System;

namespace TunnelCrew.Sim
{
    public enum EscapePhase : byte { None = 0, Placing, Incoming, Ready, Boarded }

    public struct EscapeEvent { public EscapePhase Phase; public Vec2 At; public double Need; }

    /// <summary>
    /// 탈출 포트 — 런의 성공 종료. 원본 <c>INF_ESCAPE</c>(13554) 와 <c>infToggleEscapePlacement ·
    /// infConfirmEscapePlacement · infAutoSummonEscape · infEscapeArrive · infUpdateEscape</c>.
    ///
    /// X → 지점 지정(플레이어 반경 6칸) → 도착 대기(20초 + 심층당 7초, 상한 60초) → 착륙 시 반경 1.45칸
    /// 지형 파괴 → 1.25칸 안에서 1.2초 채널링 → 탑승. 보스(수호자 제외) 처치 시 자동 요청.
    /// 솔로 런타임이라 탑승 = 즉시 생환. 코옵 전원 탑승 집계는 M8 에서 이 클래스를 확장한다.
    /// </summary>
    public sealed class EscapeSystem
    {
        public const double PlaceRange = 6, SummonBase = 20, SummonPerDepth = 7, SummonMax = 60;
        public const double ClearRadius = 1.45, BoardRange = 1.25, BoardTime = 1.2;

        readonly WorldGrid _world;

        public EscapePhase Phase { get; private set; }
        public Vec2 Position { get; private set; }
        /// <summary>도착 대기 경과/필요 (초).</summary>
        public double Elapsed { get; private set; }
        public double Need { get; private set; }
        /// <summary>탑승 채널링 진행 (초).</summary>
        public double Board { get; private set; }

        public bool Active => Phase != EscapePhase.None;
        public double ArrivalProgress => Phase == EscapePhase.Incoming && Need > 0 ? Math.Min(1, Elapsed / Need) : Phase >= EscapePhase.Ready ? 1 : 0;
        public double BoardProgress => Math.Min(1, Board / BoardTime);

        public event Action<EscapeEvent> Changed;
        /// <summary>탑승 완료 — 런 성공. 호출자가 결과 화면으로 넘긴다.</summary>
        public event Action Boarded;
        /// <summary>AI 크루가 있으면 그들도 태워야 뜬다 (§8.4-3·4). 기본은 솔로 판정(항상 참).</summary>
        public Func<bool> CrewAllAboard = () => true;

        public EscapeSystem(WorldGrid world) { _world = world; }

        public static double SummonNeedFor(int depth) => Math.Min(SummonMax, SummonBase + Math.Max(0, depth - 1) * SummonPerDepth);

        /// <summary>원본 infEscapeClampTarget — 플레이어 반경 6칸, 맵 가장자리 2칸 안.</summary>
        public Vec2 ClampTarget(Vec2 player, Vec2 want)
        {
            var d = want - player;
            double dist = d.Length;
            if (dist > PlaceRange) want = player + d / dist * PlaceRange;
            return new Vec2(JsMath.Clamp(want.X, 2, _world.Cols - 2), JsMath.Clamp(want.Y, 2, _world.Rows - 2));
        }

        /// <summary>X 키 — 지정 시작/취소. 이미 요청된 포트가 있으면 무시(false).</summary>
        public bool TogglePlacement(PlayerState p, int depth)
        {
            if (Phase == EscapePhase.Placing) { Cancel(); return true; }
            if (Phase != EscapePhase.None) return false;
            Phase = EscapePhase.Placing;
            Position = p.Position;
            Elapsed = 0; Board = 0;
            Need = SummonNeedFor(depth);
            Changed?.Invoke(new EscapeEvent { Phase = Phase, At = Position, Need = Need });
            return true;
        }

        public void Cancel()
        {
            if (Phase != EscapePhase.Placing) return;
            Phase = EscapePhase.None;
            Changed?.Invoke(new EscapeEvent { Phase = Phase, At = Position, Need = Need });
        }

        /// <summary>지정 중 조준점 갱신 — 미리보기와 확정이 같은 지점을 쓴다.</summary>
        public void UpdatePlacement(PlayerState p, Vec2 aimWorld)
        {
            if (Phase != EscapePhase.Placing) return;
            Position = ClampTarget(p.Position, aimWorld);
        }

        /// <summary>좌클릭 — 확정. 원본 infConfirmEscapePlacement.</summary>
        public bool Confirm(PlayerState p, Vec2 aimWorld, int depth)
        {
            if (Phase != EscapePhase.Placing) return false;
            Position = ClampTarget(p.Position, aimWorld);
            Phase = EscapePhase.Incoming;
            Elapsed = 0; Need = SummonNeedFor(depth);
            Changed?.Invoke(new EscapeEvent { Phase = Phase, At = Position, Need = Need });
            return true;
        }

        /// <summary>보스 처치 시 자동 요청 — 이미 요청된 포트가 있으면 그대로 둔다 (원본 infAutoSummonEscape).</summary>
        public void AutoSummon(PlayerState p, Vec2 at, int depth)
        {
            if (Phase != EscapePhase.None && Phase != EscapePhase.Placing) return;
            Position = ClampTarget(p.Position, at);
            Phase = EscapePhase.Incoming;
            Elapsed = 0; Board = 0; Need = SummonNeedFor(depth);
            Changed?.Invoke(new EscapeEvent { Phase = Phase, At = Position, Need = Need });
        }

        public void Tick(PlayerState p, double dt)
        {
            switch (Phase)
            {
                case EscapePhase.Incoming:
                    Elapsed += dt;
                    if (Elapsed >= Need) Arrive();
                    break;
                case EscapePhase.Ready:
                    if (p.Downed) { Board = Math.Max(0, Board - dt * 2); break; }
                    if (Vec2.Distance(p.Position, Position) <= BoardRange)
                    {
                        Board += dt;
                        if (Board >= BoardTime && !CrewAllAboard()) Board = BoardTime;   // 사람만 타면 대기
                        else if (Board >= BoardTime)
                        {
                            Phase = EscapePhase.Boarded;
                            Changed?.Invoke(new EscapeEvent { Phase = Phase, At = Position, Need = Need });
                            Boarded?.Invoke();
                        }
                    }
                    else Board = Math.Max(0, Board - dt * 2);   // 스치기만 해선 안 된다
                    break;
            }
        }

        /// <summary>원본 infEscapeArrive — 포트는 지상에서 굴착해 내려온다. 착륙 지형을 뚫는다.</summary>
        void Arrive()
        {
            Phase = EscapePhase.Ready;
            Board = 0;
            var (c0, r0) = WorldGrid.ToCell(Position);
            int R = (int)Math.Ceiling(ClearRadius);
            for (int dr = -R; dr <= R; dr++) for (int dc = -R; dc <= R; dc++)
            {
                if (Math.Sqrt(dc * dc + dr * dr) > ClearRadius + .25) continue;
                int c = c0 + dc, r = r0 + dr;
                if (!_world.InInterior(c, r)) continue;
                _world.ClearSilent(c, r);
            }
            Changed?.Invoke(new EscapeEvent { Phase = Phase, At = Position, Need = Need });
        }

        public void Clear()
        {
            // 하강하면 요청했던 포트는 두고 간다 (원본 infInitFloor: INF.escape=null)
            Phase = EscapePhase.None; Elapsed = 0; Board = 0;
        }
    }
}
