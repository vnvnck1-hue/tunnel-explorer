using UnityEngine;
using UnityEngine.InputSystem;

namespace TunnelCrew.Presentation.Prototype
{
    /// <summary>
    /// 독립 프로토타입용 거너 컨트롤러. 몸체 이동과 조준 리그를 분리하고,
    /// 머리/몸/손/무기 트윈은 Animator의 편집 가능한 클립에 맡긴다.
    /// </summary>
    public sealed class ModularGunnerController : MonoBehaviour
    {
        [SerializeField] float _moveSpeed = 4.8f;
        [SerializeField] float _shotsPerSecond = 7.5f;
        [SerializeField, Range(0f, 12f)] float _baseSpreadDegrees = 2.4f;
        [SerializeField, Range(0f, 8f)] float _movingSpreadBonusDegrees = 1.6f;
        [SerializeField] Rigidbody2D _body;
        [SerializeField] Animator _animator;
        [SerializeField] Transform _aimRig;
        [SerializeField] Transform _muzzle;
        [SerializeField] Transform _ejectionPort;
        [SerializeField] SpriteRenderer _bodyRenderer;
        [SerializeField] SpriteRenderer _headRenderer;
        [SerializeField] Sprite[] _bodyDirections;
        [SerializeField] Sprite[] _headDirections;
        [SerializeField] SpriteRenderer _weaponRenderer;
        [SerializeField] SpriteRenderer _mainHandRenderer;
        [SerializeField] SpriteRenderer _supportHandRenderer;
        [SerializeField] SpriteRenderer _muzzleFlashRenderer;
        [SerializeField] ModularGunnerProjectile _projectilePrefab;
        [SerializeField] Sprite[] _weaponRecoilFrames;
        [SerializeField] Transform _visualRoot;

        [Header("Weapon shake preset — 로드맵 4.11")]
        [Tooltip("이 총기의 발사 흔들림 세기. 총기를 늘릴 때 무기마다 다른 값을 준다.")]
        [SerializeField, Range(0f, 1f)] float _shakeStrength = 0.18f;
        [Tooltip("1에 가까울수록 잘게 떨고 0에 가까울수록 크게 밀린다. 미니건은 고주파다.")]
        [SerializeField, Range(0f, 1f)] float _shakeHighRatio = 0.95f;
        [Tooltip("총구 반대 방향으로 미는 카메라 킥의 세기.")]
        [SerializeField, Range(0f, 1f)] float _kickStrength = 0.12f;

        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int FireHash = Animator.StringToHash("Fire");

        // 승인 아트의 4프레임 반동 시트에서 2번이 총열이 가장 뒤로 밀린 포즈다.
        // 발사 순간 그 포즈로 튀었다가 두 단계에 걸쳐 정지 포즈로 돌아온다.
        static readonly int[] RecoilSequence = { 2, 1, 0 };
        const float RecoilFrameDuration = 0.035f;
        const float Ppu = 16f;
        const float InertiaPixels = 2f;
        const float InertiaRecovery = 14f;

        Vector2 _moveInput;
        Vector2 _aimDirection = Vector2.right;
        Camera _camera;
        float _nextShotTime;
        float _flashUntil;
        int _recoilStep = -1;
        float _nextRecoilStepAt;
        Vector2 _lastMoveInput;
        Vector2 _leanOffset;

        public Vector2 AimDirection => _aimDirection;

        public void EditorAssign(
            Rigidbody2D body,
            Animator animator,
            Transform aimRig,
            Transform muzzle,
            Transform ejectionPort,
            SpriteRenderer bodyRenderer,
            SpriteRenderer headRenderer,
            SpriteRenderer weaponRenderer,
            SpriteRenderer mainHandRenderer,
            SpriteRenderer supportHandRenderer,
            SpriteRenderer muzzleFlashRenderer,
            ModularGunnerProjectile projectilePrefab,
            Sprite[] bodyDirections,
            Sprite[] headDirections,
            Sprite[] weaponRecoilFrames,
            Transform visualRoot)
        {
            _visualRoot = visualRoot;
            _weaponRecoilFrames = weaponRecoilFrames;
            _bodyDirections = bodyDirections;
            _headDirections = headDirections;
            _body = body;
            _animator = animator;
            _aimRig = aimRig;
            _muzzle = muzzle;
            _ejectionPort = ejectionPort;
            _bodyRenderer = bodyRenderer;
            _headRenderer = headRenderer;
            _weaponRenderer = weaponRenderer;
            _mainHandRenderer = mainHandRenderer;
            _supportHandRenderer = supportHandRenderer;
            _muzzleFlashRenderer = muzzleFlashRenderer;
            _projectilePrefab = projectilePrefab;
        }

        void Awake()
        {
            ModularGunnerPhysicsLayers.Assign(gameObject, ModularGunnerPhysicsLayers.Player);
            ModularGunnerPhysicsLayers.ConfigureCollisionMatrix();
            Application.targetFrameRate = 60;
            Time.fixedDeltaTime = 1f / 60f;
            _camera = Camera.main;
            if (_muzzleFlashRenderer != null) _muzzleFlashRenderer.enabled = false;
        }

        void Update()
        {
            ReadMovement();
            ReadAim();
            UpdateAimRig();

            if (_animator != null) _animator.SetFloat(SpeedHash, _moveInput.magnitude);

            bool wantsFire = Mouse.current?.leftButton.isPressed == true ||
                             Keyboard.current?.spaceKey.isPressed == true ||
                             Gamepad.current?.rightTrigger.isPressed == true;
            if (wantsFire && Time.time >= _nextShotTime) Fire();

            if (_muzzleFlashRenderer != null && _muzzleFlashRenderer.enabled && Time.time >= _flashUntil)
                _muzzleFlashRenderer.enabled = false;

            AdvanceRecoil();
            UpdateInertia();
        }

        /// <summary>
        /// 로드맵 4.2 — 급정지와 방향 전환에서 1~2픽셀 관성.
        /// 입력이 꺾인 순간 몸을 이전 진행 방향으로 살짝 남겨 두었다가 되돌린다.
        /// 16 PPU 기준 2픽셀(0.125유닛)을 넘지 않게 묶어 픽셀 격자를 흐리지 않는다.
        /// </summary>
        void UpdateInertia()
        {
            if (_visualRoot == null) return;

            Vector2 delta = _moveInput - _lastMoveInput;
            if (delta.sqrMagnitude > 0.02f)
            {
                // 입력이 바뀐 만큼 이전 방향으로 끌린다. 급정지면 delta 가 곧 이전 방향이다.
                _leanOffset -= delta * InertiaPixels / Ppu;
                _leanOffset = Vector2.ClampMagnitude(_leanOffset, InertiaPixels / Ppu);
            }
            _lastMoveInput = _moveInput;

            _leanOffset = Vector2.Lerp(_leanOffset, Vector2.zero, 1f - Mathf.Exp(-InertiaRecovery * Time.deltaTime));
            // 최종 위치는 픽셀 격자에 맞춘다.
            _visualRoot.localPosition = new Vector3(
                Mathf.Round(_leanOffset.x * Ppu) / Ppu,
                Mathf.Round(_leanOffset.y * Ppu) / Ppu, 0f);
        }

        /// <summary>발사 후 무기 스프라이트를 반동 포즈에서 정지 포즈로 되돌린다.</summary>
        void AdvanceRecoil()
        {
            if (_recoilStep < 0 || _weaponRenderer == null || _weaponRecoilFrames == null || _weaponRecoilFrames.Length == 0)
                return;
            if (Time.time < _nextRecoilStepAt) return;

            _recoilStep++;
            if (_recoilStep >= RecoilSequence.Length)
            {
                _recoilStep = -1;
                _weaponRenderer.sprite = _weaponRecoilFrames[0];
                return;
            }
            SetRecoilFrame(RecoilSequence[_recoilStep]);
        }

        void SetRecoilFrame(int frame)
        {
            if (_weaponRenderer == null || _weaponRecoilFrames == null || _weaponRecoilFrames.Length == 0) return;
            _weaponRenderer.sprite = _weaponRecoilFrames[Mathf.Clamp(frame, 0, _weaponRecoilFrames.Length - 1)];
            _nextRecoilStepAt = Time.time + RecoilFrameDuration;
        }

        void FixedUpdate()
        {
            if (_body != null) _body.linearVelocity = _moveInput * _moveSpeed;
        }

        void ReadMovement()
        {
            Vector2 input = Vector2.zero;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
            }

            var gamepad = Gamepad.current;
            if (gamepad != null && gamepad.leftStick.ReadValue().sqrMagnitude > input.sqrMagnitude)
                input = gamepad.leftStick.ReadValue();

            _moveInput = Vector2.ClampMagnitude(input, 1f);
        }

        void ReadAim()
        {
            var gamepad = Gamepad.current;
            Vector2 stick = gamepad != null ? gamepad.rightStick.ReadValue() : Vector2.zero;
            if (stick.sqrMagnitude > 0.08f)
            {
                _aimDirection = stick.normalized;
                return;
            }

            if (_camera == null) _camera = Camera.main;
            var mouse = Mouse.current;
            if (_camera == null || mouse == null) return;

            Vector3 world = _camera.ScreenToWorldPoint(mouse.position.ReadValue());
            Vector2 delta = (Vector2)world - _body.position;
            if (delta.sqrMagnitude > 0.001f) _aimDirection = delta.normalized;
        }

        void UpdateAimRig()
        {
            if (_aimRig == null) return;
            float angle = Mathf.Atan2(_aimDirection.y, _aimDirection.x) * Mathf.Rad2Deg;
            _aimRig.localRotation = Quaternion.Euler(0f, 0f, angle);
            _aimRig.localScale = new Vector3(1f, _aimDirection.x < 0f ? -1f : 1f, 1f);

            ModularGunnerDirectionSet.Resolve(_aimDirection, out int dirFrame, out bool dirFlip);
            if (_bodyRenderer != null && _bodyDirections != null && _bodyDirections.Length == ModularGunnerDirectionSet.Count)
            {
                _bodyRenderer.sprite = _bodyDirections[dirFrame];
                _bodyRenderer.flipX = dirFlip;
            }
            if (_headRenderer != null && _headDirections != null && _headDirections.Length == ModularGunnerDirectionSet.Count)
            {
                _headRenderer.sprite = _headDirections[dirFrame];
                _headRenderer.flipX = dirFlip;
            }

            int weaponOrder = ModularGunnerDirectionSet.WeaponBehindBody(dirFrame) ? 4 : 30;
            if (_weaponRenderer != null) _weaponRenderer.sortingOrder = weaponOrder;
            if (_mainHandRenderer != null) _mainHandRenderer.sortingOrder = weaponOrder + 2;
            if (_supportHandRenderer != null) _supportHandRenderer.sortingOrder = weaponOrder + 1;
            if (_muzzleFlashRenderer != null) _muzzleFlashRenderer.sortingOrder = weaponOrder + 3;

        }

        void Fire()
        {
            if (_projectilePrefab == null || _muzzle == null) return;

            _nextShotTime = Time.time + 1f / _shotsPerSecond;
            Vector2 shotDirection = SampleShotDirection(_aimDirection);
            var shot = Instantiate(_projectilePrefab, _muzzle.position, Quaternion.identity);
            shot.Launch(shotDirection, this);
            ModularGunnerEffects.Instance?.Muzzle(
                _ejectionPort != null ? _ejectionPort.position : _muzzle.position,
                _aimDirection, _shakeStrength, _shakeHighRatio, _kickStrength);

            if (_animator != null) _animator.SetTrigger(FireHash);
            _recoilStep = 0;
            SetRecoilFrame(RecoilSequence[0]);
            if (_muzzleFlashRenderer != null)
            {
                _muzzleFlashRenderer.enabled = true;
                _flashUntil = Time.time + 0.045f;
            }
        }

        Vector2 SampleShotDirection(Vector2 aim)
        {
            float moveRatio = Mathf.Clamp01(_moveInput.magnitude);
            float maxSpread = _baseSpreadDegrees + _movingSpreadBonusDegrees * moveRatio;

            // 세 번의 균등 샘플을 합쳐 중앙 밀도가 높은 종 모양 탄착군을 만든다.
            float centered = Random.value + Random.value + Random.value - 1.5f;
            float angle = centered / 1.5f * maxSpread;
            return Quaternion.Euler(0f, 0f, angle) * aim.normalized;
        }
    }
}
