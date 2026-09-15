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
        [SerializeField] Rigidbody2D _body;
        [SerializeField] Animator _animator;
        [SerializeField] Transform _aimRig;
        [SerializeField] Transform _muzzle;
        [SerializeField] SpriteRenderer _bodyRenderer;
        [SerializeField] SpriteRenderer _headRenderer;
        [SerializeField] Sprite _bodyDownLeftSprite;
        [SerializeField] Sprite _bodyUpLeftSprite;
        [SerializeField] Sprite _headDownLeftSprite;
        [SerializeField] Sprite _headUpLeftSprite;
        [SerializeField] SpriteRenderer _weaponRenderer;
        [SerializeField] SpriteRenderer _mainHandRenderer;
        [SerializeField] SpriteRenderer _supportHandRenderer;
        [SerializeField] SpriteRenderer _muzzleFlashRenderer;
        [SerializeField] ModularGunnerProjectile _projectilePrefab;

        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int FireHash = Animator.StringToHash("Fire");

        Vector2 _moveInput;
        Vector2 _aimDirection = Vector2.right;
        Camera _camera;
        float _nextShotTime;
        float _flashUntil;

        public Vector2 AimDirection => _aimDirection;

        public void EditorAssign(
            Rigidbody2D body,
            Animator animator,
            Transform aimRig,
            Transform muzzle,
            SpriteRenderer bodyRenderer,
            SpriteRenderer headRenderer,
            SpriteRenderer weaponRenderer,
            SpriteRenderer mainHandRenderer,
            SpriteRenderer supportHandRenderer,
            SpriteRenderer muzzleFlashRenderer,
            ModularGunnerProjectile projectilePrefab,
            Sprite bodyUpLeftSprite,
            Sprite headUpLeftSprite)
        {
            _body = body;
            _animator = animator;
            _aimRig = aimRig;
            _muzzle = muzzle;
            _bodyRenderer = bodyRenderer;
            _headRenderer = headRenderer;
            _bodyDownLeftSprite = bodyRenderer != null ? bodyRenderer.sprite : null;
            _bodyUpLeftSprite = bodyUpLeftSprite;
            _headDownLeftSprite = headRenderer != null ? headRenderer.sprite : null;
            _headUpLeftSprite = headUpLeftSprite;
            _weaponRenderer = weaponRenderer;
            _mainHandRenderer = mainHandRenderer;
            _supportHandRenderer = supportHandRenderer;
            _muzzleFlashRenderer = muzzleFlashRenderer;
            _projectilePrefab = projectilePrefab;
        }

        void Awake()
        {
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

            bool aimingUp = _aimDirection.y > 0.15f;
            if (_bodyRenderer != null && _bodyDownLeftSprite != null && _bodyUpLeftSprite != null)
                _bodyRenderer.sprite = aimingUp ? _bodyUpLeftSprite : _bodyDownLeftSprite;
            if (_headRenderer != null && _headDownLeftSprite != null && _headUpLeftSprite != null)
                _headRenderer.sprite = aimingUp ? _headUpLeftSprite : _headDownLeftSprite;

            int weaponOrder = aimingUp ? 4 : 30;
            if (_weaponRenderer != null) _weaponRenderer.sortingOrder = weaponOrder;
            if (_mainHandRenderer != null) _mainHandRenderer.sortingOrder = weaponOrder + 2;
            if (_supportHandRenderer != null) _supportHandRenderer.sortingOrder = weaponOrder + 1;
            if (_muzzleFlashRenderer != null) _muzzleFlashRenderer.sortingOrder = weaponOrder + 3;

            if (Mathf.Abs(_aimDirection.x) > 0.08f)
            {
                bool flipToRight = _aimDirection.x > 0f;
                if (_headRenderer != null) _headRenderer.flipX = flipToRight;
                // 생성 원본에서 머리는 좌향, 몸통은 우향으로 읽히므로 몸통은 반대 플립을 쓴다.
                if (_bodyRenderer != null) _bodyRenderer.flipX = !flipToRight;
            }
        }

        void Fire()
        {
            if (_projectilePrefab == null || _muzzle == null) return;

            _nextShotTime = Time.time + 1f / _shotsPerSecond;
            var shot = Instantiate(_projectilePrefab, _muzzle.position, Quaternion.identity);
            shot.Launch(_aimDirection, this);

            if (_animator != null) _animator.SetTrigger(FireHash);
            if (_muzzleFlashRenderer != null)
            {
                _muzzleFlashRenderer.enabled = true;
                _flashUntil = Time.time + 0.045f;
            }
        }
    }
}
