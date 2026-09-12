using System;

namespace TunnelCrew.Presentation
{
    /// <summary>
    /// 플레이어블 프롤로그의 진행만 담는 순수 상태 모델.
    /// 프레임 시간과 입력을 직접 읽지 않아 EditMode에서 페이싱·힌트 계약을 검증할 수 있다.
    /// </summary>
    public sealed class FtueProgressModel
    {
        public enum Stage : byte
        {
            Inactive,
            CrashComic,
            RoleChoice,
            Movement,
            FirstDig,
            Flare,
            RescueDig,
            Combat,
            BlackboxInteract,
            BlackboxComic,
            AwakeningComic,
            ReturnToLander,
            EscapeComic,
            Complete,
        }

        public enum Hint : byte { None, Input, Direction, Direct }

        public Stage Current { get; private set; } = Stage.Inactive;
        public Hint HintLevel { get; private set; }
        public double StageStartedAt { get; private set; }
        public double Elapsed(double now) => Math.Max(0, now - StageStartedAt);
        public event Action<Stage, Stage> Changed;

        public void Begin(double now) => Set(Stage.CrashComic, now);

        public bool Set(Stage next, double now)
        {
            if (Current == next) return false;
            var previous = Current;
            Current = next;
            StageStartedAt = now;
            HintLevel = Hint.None;
            Changed?.Invoke(previous, next);
            return true;
        }

        /// <summary>
        /// 3초에 기본 입력, 12초에 방향, 22초에 직접 힌트. 연출과 선택 화면은
        /// 스스로 템포를 가지므로 힌트를 띄우지 않는다.
        /// </summary>
        public Hint Tick(double now)
        {
            if (BlocksWorld(Current) || Current == Stage.RoleChoice || Current == Stage.Inactive || Current == Stage.Complete)
                return HintLevel = Hint.None;
            double t = Elapsed(now);
            return HintLevel = t >= 22 ? Hint.Direct : t >= 12 ? Hint.Direction : t >= 3 ? Hint.Input : Hint.None;
        }

        public static bool BlocksWorld(Stage stage)
            => stage == Stage.CrashComic || stage == Stage.BlackboxComic || stage == Stage.AwakeningComic || stage == Stage.EscapeComic;

        public static string Objective(Stage stage) => stage switch
        {
            Stage.Movement => "비상등 아래서 머무르지 말고 출구를 찾아라",
            Stage.FirstDig => "암반에 막힌 출구를 뚫어라",
            Stage.Flare => "선발대의 비상 플레어로 앞을 밝혀라",
            Stage.RescueDig => "벽 뒤의 두드림을 따라 생존자를 구하라",
            Stage.Combat => "접근하는 지하 생물을 제압하라",
            Stage.BlackboxInteract => "구조 신호의 발신지를 조사하라",
            Stage.ReturnToLander => "블랙박스를 가지고 강하선으로 복귀하라",
            _ => "",
        };

        public static string Prompt(Stage stage, Hint hint) => stage switch
        {
            Stage.Movement => hint >= Hint.Input ? "WASD  이동" : "",
            Stage.FirstDig => hint >= Hint.Input ? "좌클릭 홀드  굴착" : "",
            Stage.Flare => hint >= Hint.Input ? "Q  비상 플레어" : "",
            Stage.RescueDig => hint >= Hint.Input ? "두드림이 들리는 벽을 굴착" : "",
            Stage.Combat => hint >= Hint.Input ? "조준·사격·대시로 응전" : "",
            Stage.BlackboxInteract => hint >= Hint.Input ? "E  블랙박스 조사" : "",
            Stage.ReturnToLander => hint >= Hint.Input ? "비상등 방향으로 복귀" : "",
            _ => "",
        };
    }
}
