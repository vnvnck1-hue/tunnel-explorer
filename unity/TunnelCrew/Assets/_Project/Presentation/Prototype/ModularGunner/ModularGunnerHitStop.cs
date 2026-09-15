using UnityEngine;

namespace TunnelCrew.Presentation.Prototype
{
    /// <summary>
    /// 로드맵 4.3 의 20~55ms 히트 스톱. 타임스케일을 잠깐 0 으로 눌렀다가 되돌린다.
    /// 여러 요청이 겹치면 가장 긴 시간만 남기고, 카메라와 HUD 는 언스케일 시간을 쓴다.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class ModularGunnerHitStop : MonoBehaviour
    {
        public static ModularGunnerHitStop Instance { get; private set; }

        float _releaseAt;
        bool _holding;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            if (_holding) Time.timeScale = 1f;
        }

        /// <summary>초 단위 정지 요청. 이미 더 긴 정지가 걸려 있으면 무시한다.</summary>
        public void Hold(float seconds)
        {
            if (seconds <= 0f) return;
            float until = Time.unscaledTime + Mathf.Clamp(seconds, 0.01f, 0.12f);
            if (until <= _releaseAt) return;
            _releaseAt = until;
            if (_holding) return;
            _holding = true;
            Time.timeScale = 0f;
        }

        void Update()
        {
            if (!_holding || Time.unscaledTime < _releaseAt) return;
            _holding = false;
            Time.timeScale = 1f;
        }
    }
}
