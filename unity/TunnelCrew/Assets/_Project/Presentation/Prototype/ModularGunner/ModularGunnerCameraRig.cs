using UnityEngine;

namespace TunnelCrew.Presentation.Prototype
{
    [RequireComponent(typeof(Camera))]
    public sealed class ModularGunnerCameraRig : MonoBehaviour
    {
        [SerializeField] ModularGunnerCameraProfile _profile;
        [SerializeField] Transform _target;
        [SerializeField] Rigidbody2D _targetBody;
        [SerializeField] ModularGunnerController _targetController;
        [SerializeField] Vector2 _arenaMin = new(-18f, -10f);
        [SerializeField] Vector2 _arenaMax = new(18f, 10f);

        Camera _camera;
        Vector2 _center;
        Vector2 _lookDirection = Vector2.right;
        float _currentSize;
        float _shakeHigh;
        float _shakeLow;
        float _shakeSeed;
        bool _initialized;
        Vector2 _kick;
        float _zoomPunch;

        public ModularGunnerCameraProfile Profile => _profile;

        public void EditorAssign(ModularGunnerCameraProfile profile, Transform target, Vector2 arenaMin, Vector2 arenaMax)
        {
            _profile = profile;
            _target = target;
            _targetBody = target != null ? target.GetComponent<Rigidbody2D>() : null;
            _targetController = target != null ? target.GetComponent<ModularGunnerController>() : null;
            _arenaMin = arenaMin;
            _arenaMax = arenaMax;
        }

        void Awake()
        {
            _camera = GetComponent<Camera>();
            _shakeSeed = Random.value * 1000f;
        }

        /// <summary>
        /// 피격 종류별 고주파/저주파 흔들림 분리(로드맵 4.11).
        /// highRatio 1 은 총구처럼 잘게 떠는 진동, 0 은 폭발처럼 크게 밀리는 진동이다.
        /// </summary>
        public void AddShake(float strength, float highRatio = 0.65f)
        {
            if (_profile == null) return;
            float amount = Mathf.Clamp01(strength) * _profile.shakeAmplitude;
            highRatio = Mathf.Clamp01(highRatio);
            _shakeHigh = Mathf.Max(_shakeHigh, amount * highRatio);
            _shakeLow = Mathf.Max(_shakeLow, amount * (1f - highRatio) * _profile.lowFrequencyGain);
        }

        /// <summary>충돌 법선 방향으로 카메라를 밀어내는 방향성 킥. 로드맵 4.3 · 4.11.</summary>
        public void AddKick(Vector2 direction, float strength)
        {
            if (_profile == null || direction.sqrMagnitude < 0.0001f) return;
            _kick += direction.normalized * (Mathf.Clamp01(strength) * _profile.kickDistance);
            if (_kick.magnitude > _profile.kickDistance)
                _kick = _kick.normalized * _profile.kickDistance;
        }

        /// <summary>강피격·폭발의 짧은 줌 펀치. 값이 클수록 순간적으로 더 당겨진다.</summary>
        public void AddZoomPunch(float strength)
        {
            if (_profile == null) return;
            _zoomPunch = Mathf.Max(_zoomPunch, Mathf.Clamp01(strength) * _profile.zoomPunch);
        }

        void LateUpdate()
        {
            if (_profile == null || _target == null) return;
            if (_camera == null) _camera = GetComponent<Camera>();

            // 히트 스톱 동안에도 킥·흔들림·줌 펀치는 계속 살아 있어야 타격이 읽힌다.
            float unscaled = Time.unscaledDeltaTime;

            float zoomT = 1f - Mathf.Exp(-_profile.zoomSharpness * Time.deltaTime);
            if (!_initialized) _currentSize = _profile.orthographicSize;
            else _currentSize = Mathf.Lerp(_currentSize, _profile.orthographicSize, zoomT);
            _zoomPunch = Mathf.MoveTowards(_zoomPunch, 0f, _profile.zoomPunchRecovery * unscaled);
            _camera.orthographicSize = _currentSize * (1f - _zoomPunch);

            Vector2 aim = _targetController != null ? _targetController.AimDirection : Vector2.right;
            Vector2 movement = _targetBody != null ? _targetBody.linearVelocity : Vector2.zero;
            Vector2 desiredDirection = aim + (movement.sqrMagnitude > 0.01f ? movement.normalized * _profile.movementLookWeight : Vector2.zero);
            if (desiredDirection.sqrMagnitude < 0.001f) desiredDirection = _lookDirection;
            desiredDirection.Normalize();
            float lookT = 1f - Mathf.Exp(-_profile.lookDirectionSharpness * Time.deltaTime);
            _lookDirection = Vector2.Lerp(_lookDirection, desiredDirection, lookT).normalized;

            Vector2 targetPoint = (Vector2)_target.position + _lookDirection * _profile.aimLookAhead;
            if (_targetBody != null) targetPoint += _targetBody.linearVelocity * _profile.velocityLookAheadTime;

            float height = _currentSize * 2f;
            float width = height * _camera.aspect;
            Vector2 anchorOffset = new(0f, (_profile.verticalAnchor - 0.5f) * height);
            Vector2 wantedCenter = targetPoint - anchorOffset;

            if (!_initialized || _profile.snap)
            {
                _center = wantedCenter;
            }
            else
            {
                Vector2 delta = wantedCenter - _center;
                float distance = delta.magnitude;
                if (distance > _profile.deadZone)
                {
                    Vector2 pulled = _center + delta.normalized * (distance - _profile.deadZone);
                    float followT = Mathf.Min(1f, _profile.followSpeed * 60f * Time.deltaTime);
                    _center = Vector2.Lerp(_center, pulled, followT);
                }
            }

            float pad = _profile.boundsPadding;
            float minX = _arenaMin.x + width * 0.5f - pad;
            float maxX = _arenaMax.x - width * 0.5f + pad;
            float minY = _arenaMin.y + height * 0.5f - pad;
            float maxY = _arenaMax.y - height * 0.5f + pad;
            _center.x = minX <= maxX ? Mathf.Clamp(_center.x, minX, maxX) : (_arenaMin.x + _arenaMax.x) * 0.5f;
            _center.y = minY <= maxY ? Mathf.Clamp(_center.y, minY, maxY) : (_arenaMin.y + _arenaMax.y) * 0.5f;

            float decay = _profile.shakeDecay * unscaled * Mathf.Max(_profile.shakeAmplitude, 0.001f);
            _shakeHigh = Mathf.MoveTowards(_shakeHigh, 0f, decay);
            // 저주파 성분은 더 오래 끌어야 묵직하게 읽힌다.
            _shakeLow = Mathf.MoveTowards(_shakeLow, 0f, decay * 0.45f);
            _kick = Vector2.MoveTowards(_kick, Vector2.zero, _profile.kickRecovery * unscaled);

            float now = Time.unscaledTime + _shakeSeed;
            float highPhase = now * _profile.shakeFrequency;
            float lowPhase = now * _profile.shakeFrequency * _profile.lowFrequencyRatio;
            Vector2 high = new(Mathf.PerlinNoise(highPhase, 0.17f) - 0.5f, Mathf.PerlinNoise(0.37f, highPhase) - 0.5f);
            Vector2 low = new(Mathf.PerlinNoise(lowPhase, 5.11f) - 0.5f, Mathf.PerlinNoise(7.23f, lowPhase) - 0.5f);
            Vector2 shakeOffset = high * (_shakeHigh * 2f) + low * (_shakeLow * 2f);
            Vector2 final = _center + shakeOffset + _kick;
            if (_profile.quantizeFinalPosition)
            {
                float ppu = Mathf.Max(1, _profile.assetsPixelsPerUnit);
                final.x = Mathf.Round(final.x * ppu) / ppu;
                final.y = Mathf.Round(final.y * ppu) / ppu;
            }
            transform.position = new Vector3(final.x, final.y, -10f);
            _initialized = true;
        }
    }
}
