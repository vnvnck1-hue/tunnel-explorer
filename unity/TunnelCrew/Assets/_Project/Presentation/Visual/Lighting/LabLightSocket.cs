using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 랩 광원에 <see cref="LightSocket"/> 을 <b>런타임에</b> 붙인다.
    ///
    /// <see cref="LightSocket"/> 은 <c>LightSocketRenderer.cs</c> 안의 두 번째 MonoBehaviour 라
    /// 씬에 직렬화하면 <c>m_Script</c> 가 로컬 fileID 로 박혀 로드 때 missing script 가 된다 —
    /// <see cref="LabAnchoredEntity"/> 가 접촉 그림자·실루엣에 대해 겪은 것과 같은 문제다
    /// (2026-09-09, 2단계에서 실제로 4개가 깨졌다). VisualLab 은 <c>BuildLighting</c> 에서 런타임
    /// 생성으로 피한다. 값은 여기 직렬화하고 소켓은 Awake 에서 만든다.
    /// </summary>
    [RequireComponent(typeof(Light2D))]
    public sealed class LabLightSocket : MonoBehaviour
    {
        [SerializeField] LightClass _lightClass = LightClass.Worklamp;
        [Tooltip("깜빡임 전 세기. LightSocketRenderer 가 매 프레임 이 값 × 깜빡임을 Light2D 에 쓴다.")]
        [SerializeField] float _baseIntensity = 1f;
        [Tooltip("그림자 우선순위 계산용 반지름(셀).")]
        [SerializeField] float _rangeCells = 1f;
        [Tooltip("깜빡임 시드. 소켓마다 다르게 두어 한 박자로 숨쉬지 않게 한다.")]
        [SerializeField] float _phase;
        [Tooltip("설치 높이(셀). 0.75 이상이면 벽 윗면·전경 cap 까지 비춘다(조명 소팅 정밀화).")]
        [SerializeField] float _mountHeightCells;

        void Awake()
        {
            if (TryGetComponent<LightSocket>(out _)) return;
            var socket = gameObject.AddComponent<LightSocket>();
            socket.lightClass = _lightClass;
            socket.baseIntensity = _baseIntensity;
            socket.rangeCells = _rangeCells;
            socket.phase = _phase;
            socket.mountHeightCells = _mountHeightCells;
        }
    }
}
