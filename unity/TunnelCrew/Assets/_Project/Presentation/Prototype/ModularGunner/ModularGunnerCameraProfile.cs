using UnityEngine;

namespace TunnelCrew.Presentation.Prototype
{
    /// <summary>
    /// 모듈형 거너 테스트 씬의 영구 카메라 튜닝값.
    /// 씬 컴포넌트가 아니라 프로젝트 에셋에 저장하므로 Play Mode 중 수정해도 종료 후 유지된다.
    /// </summary>
    [CreateAssetMenu(menuName = "Tunnel Crew/Prototype/Modular Gunner Camera Profile")]
    public sealed class ModularGunnerCameraProfile : ScriptableObject
    {
        [Header("Framing")]
        [Tooltip("직교 카메라의 세로 반높이. HTML 테스트의 zoom에 대응한다.")]
        [Range(2.5f, 12f)] public float orthographicSize = 5.625f;
        [Tooltip("화면 아래쪽에서 캐릭터를 두는 비율. HTML은 위에서 42%, Unity에서는 아래에서 58%다.")]
        [Range(0.25f, 0.75f)] public float verticalAnchor = 0.58f;
        [Tooltip("맵 끝에서 카메라가 보여 줄 바깥 여백.")]
        [Range(0f, 3f)] public float boundsPadding = 0.45f;

        [Header("Follow — HTML camera test")]
        [Tooltip("HTML followSpeed. 프레임 독립 보간에서 60을 곱해 사용한다.")]
        [Range(0.01f, 1f)] public float followSpeed = 0.07f;
        [Tooltip("HTML lookAhead 25px를 테스트 기준 CELL/9로 환산한 월드 거리.")]
        [Range(0f, 8f)] public float aimLookAhead = 2.78f;
        [Tooltip("HTML deadzone 20px를 테스트 기준 CELL/9로 환산한 월드 반경.")]
        [Range(0f, 6f)] public float deadZone = 2.22f;
        [Tooltip("켜면 보간과 데드존 없이 목표 지점에 즉시 붙는다.")]
        public bool snap;
        [Tooltip("이동 방향이 카메라 선행에 기여하는 비율.")]
        [Range(0f, 1f)] public float movementLookWeight = 0.2f;
        [Tooltip("속도가 카메라 선행 거리에 더해지는 시간(초).")]
        [Range(0f, 0.8f)] public float velocityLookAheadTime = 0.12f;
        [Tooltip("룩어헤드 방향이 바뀌는 속도.")]
        [Range(1f, 40f)] public float lookDirectionSharpness = 12f;
        [Tooltip("카메라 줌 변경 보간 속도.")]
        [Range(0.1f, 20f)] public float zoomSharpness = 2.4f;

        [Header("Impact shake")]
        [Range(0f, 1f)] public float shakeAmplitude = 0.16f;
        [Range(1f, 80f)] public float shakeFrequency = 34f;
        [Range(1f, 30f)] public float shakeDecay = 13f;
        [Tooltip("저주파 흔들림의 진동수 비율. 0.22면 고주파의 22% 속도로 크게 출렁인다.")]
        [Range(0.05f, 1f)] public float lowFrequencyRatio = 0.22f;
        [Tooltip("저주파 성분에 곱하는 진폭 이득. 느린 만큼 더 크게 밀어야 무게가 읽힌다.")]
        [Range(0.5f, 3f)] public float lowFrequencyGain = 1.7f;

        [Header("Directional kick — roadmap 4.3 / 4.11")]
        [Tooltip("충돌 법선 방향 카메라 킥의 최대 이동 거리(월드 유닛).")]
        [Range(0f, 1.5f)] public float kickDistance = 0.34f;
        [Tooltip("킥이 원위치로 돌아오는 속도(유닛/초).")]
        [Range(0.2f, 12f)] public float kickRecovery = 2.6f;
        [Tooltip("강피격·폭발 줌 펀치의 최대 비율. 0.06이면 6% 당겨진다.")]
        [Range(0f, 0.25f)] public float zoomPunch = 0.055f;
        [Tooltip("줌 펀치가 풀리는 속도(비율/초).")]
        [Range(0.05f, 3f)] public float zoomPunchRecovery = 0.5f;
        [Tooltip("픽셀 퍼펙트 카메라에서 최종 위치를 1/PPU 단위로 맞춘다.")]
        public bool quantizeFinalPosition = true;
        [Min(1)] public int assetsPixelsPerUnit = 16;
    }
}
