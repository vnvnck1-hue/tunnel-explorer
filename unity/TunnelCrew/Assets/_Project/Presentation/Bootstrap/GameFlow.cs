using System;
using TunnelCrew.Sim;
using UnityEngine;

namespace TunnelCrew.Presentation
{
    /// <summary>
    /// 게임 전체 흐름의 단일 상태 머신. 씬·UI·시뮬레이션이 전부 이 상태를 구독한다.
    ///
    /// 원본은 <c>SCENE</c>, <c>CREW.phase</c>, <c>INF.active</c> 세 값이 교차 검사되고
    /// 시네마틱이 <c>CREW.phase</c> 를 덮어써 월드를 멈췄다. 여기서는 상태 전이를
    /// 이 클래스만 수행하고, 나머지는 <see cref="PhaseChanged"/> 를 구독한다.
    ///
    /// M0 시점에는 골격만 있다. 각 상태의 진입·퇴장 동작은 마일스톤에서 채운다.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameFlow : MonoBehaviour
    {
        public static GameFlow Instance { get; private set; }

        [SerializeField] GamePhase _phase = GamePhase.Boot;

        /// <summary>(이전 상태, 새 상태)</summary>
        public event Action<GamePhase, GamePhase> PhaseChanged;

        public GamePhase Phase => _phase;

        /// <summary>시뮬레이션을 이번 프레임에 돌려야 하는가.</summary>
        public bool SimulationRunning => _phase.TicksSimulation();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SetPhase(GamePhase next)
        {
            if (_phase == next) return;
            var prev = _phase;
            _phase = next;
            PhaseChanged?.Invoke(prev, next);
        }

        // TODO(M1): Boot -> MainMenu 자동 전이, 씬 로드 연결
        // TODO(M4): 보스 소환 이벤트 -> BossIntro, Timeline 재생 후 Playing 복귀
        // TODO(M5): Paused 진입/복귀, Loading 진행도 표시
    }
}
