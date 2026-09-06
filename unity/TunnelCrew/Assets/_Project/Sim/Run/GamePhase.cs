namespace TunnelCrew.Sim
{
    /// <summary>
    /// 원본의 3중 상태(<c>SCENE</c> · <c>CREW.phase</c> · <c>INF.active</c>)를 하나로 합친 것.
    /// 원본은 <c>SCENE==='depths' &amp;&amp; CREW.phase==='play' &amp;&amp; INF.active</c> 같은
    /// 교차 검사가 곳곳에 반복됐다 (analysis-01 §9.7). 여기서는 단일 축으로 둔다.
    /// </summary>
    public enum GamePhase
    {
        Boot,
        MainMenu,
        Starmap,
        RoleSelect,
        Loading,
        /// <summary>인게임 플레이. 이 상태에서만 시뮬레이션이 진행된다.</summary>
        Playing,
        /// <summary>
        /// 레벨업 카드 선택. 원본 기획 §9.6.4 결정에 따라 <b>월드를 멈추지 않는다</b>.
        /// (AI 크루·코옵과 감각을 맞추기 위한 의도적 설계)
        /// </summary>
        LevelUp,
        /// <summary>보스 등장 시네마틱 4.65초. 월드 정지.</summary>
        BossIntro,
        /// <summary>보스 사망 시네마틱 3.5초. 월드 정지.</summary>
        BossDeath,
        /// <summary>보스 처치 후 휴식·전설 카드 3택. 월드 정지.</summary>
        Rest,
        /// <summary>일시정지. 원본에 없던 화면 (계획 §2.3).</summary>
        Paused,
        Result,
        Settlement,
    }

    public static class GamePhaseExtensions
    {
        /// <summary>
        /// 시뮬레이션 틱을 돌려야 하는 상태인가.
        /// 원본에서 시네마틱이 <c>CREW.phase</c> 를 바꿔 <c>update()</c> 를 멈추던 것을 대체한다.
        /// </summary>
        public static bool TicksSimulation(this GamePhase phase)
            => phase == GamePhase.Playing || phase == GamePhase.LevelUp;

        /// <summary>월드를 계속 렌더링해야 하는 상태인가 (정지 중에도 화면은 보인다).</summary>
        public static bool RendersWorld(this GamePhase phase)
            => phase == GamePhase.Playing
            || phase == GamePhase.LevelUp
            || phase == GamePhase.BossIntro
            || phase == GamePhase.BossDeath
            || phase == GamePhase.Rest
            || phase == GamePhase.Paused;
    }
}
