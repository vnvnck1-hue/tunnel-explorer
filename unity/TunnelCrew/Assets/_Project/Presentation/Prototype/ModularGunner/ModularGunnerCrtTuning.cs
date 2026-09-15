using TunnelCrew.Presentation.CRT;
using UnityEngine;

namespace TunnelCrew.Presentation.Prototype
{
    /// <summary>거너 테스트 필드에서만 CRT를 얇은 픽셀 디스플레이 질감으로 제한한다.</summary>
    public sealed class ModularGunnerCrtTuning : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] float _strength = 0.58f;
        [SerializeField, Range(0f, 1f)] float _scanlines = 0.42f;
        [SerializeField, Range(0f, 1f)] float _curvature = 0.28f;
        [SerializeField, Range(0f, 1f)] float _noise = 0.18f;
        [SerializeField, Range(0f, 1f)] float _aberration = 0.28f;
        [SerializeField, Range(0f, 1f)] float _vignette = 0.32f;

        CRTDisplayController _controller;
        int _oldProfile;
        int _oldCurvatureProfile;
        float _oldStrength;
        float _oldScanlines;
        float _oldCurvature;
        float _oldNoise;
        float _oldAberration;
        float _oldVignette;
        bool _applied;

        void Awake()
        {
            var worldCamera = GetComponent<Camera>();
            if (worldCamera == null) return;
            worldCamera.transparencySortMode = TransparencySortMode.CustomAxis;
            // 탑다운에서는 화면 아래쪽(작은 Y)의 발 위치가 더 앞에 그려져야 한다.
            worldCamera.transparencySortAxis = Vector3.down;
        }

        void Update()
        {
            if (!_applied && CRTDisplayController.Instance != null) Apply(CRTDisplayController.Instance);
        }

        void Apply(CRTDisplayController controller)
        {
            _controller = controller;
            _oldProfile = controller.ProfileIndex;
            _oldCurvatureProfile = controller.CurvatureIndex;
            _oldStrength = controller.Strength;
            _oldScanlines = controller.Scanlines;
            _oldCurvature = controller.Curvature;
            _oldNoise = controller.Noise;
            _oldAberration = controller.Aberration;
            _oldVignette = controller.Vignette;

            controller.SetProfile(0);
            controller.SetCurvatureProfile((int)CrtMonitor.Broadcast);
            controller.Strength = _strength;
            controller.Scanlines = _scanlines;
            controller.Curvature = _curvature;
            controller.Noise = _noise;
            controller.Aberration = _aberration;
            controller.Vignette = _vignette;
            _applied = true;
        }

        void OnDestroy()
        {
            if (!_applied || _controller == null) return;
            _controller.SetProfile(_oldProfile);
            _controller.SetCurvatureProfile(_oldCurvatureProfile);
            _controller.Strength = _oldStrength;
            _controller.Scanlines = _oldScanlines;
            _controller.Curvature = _oldCurvature;
            _controller.Noise = _oldNoise;
            _controller.Aberration = _oldAberration;
            _controller.Vignette = _oldVignette;
        }
    }
}
