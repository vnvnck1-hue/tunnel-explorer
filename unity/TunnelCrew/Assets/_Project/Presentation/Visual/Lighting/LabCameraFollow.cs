using UnityEngine;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 랩 카메라가 폰을 따라간다 (2026-09-09, 방을 4배로 키우면서 신설).
    ///
    /// <b>왜 필요해졌는가</b> — 랩 카메라는 방 중앙에 고정돼 있었다. 방이 17×10 일 때는
    /// 화면(약 11.5×8칸)이 방을 거의 덮어서 문제가 없었지만, 34×20 으로 키우면 화면이 방의
    /// 1/9 밖에 안 된다. 고정 카메라로는 걸어 나가는 순간 아무것도 안 보인다.
    ///
    /// <b>본선 <see cref="CameraRig"/> 를 쓰지 않는 이유</b> — 그쪽은 Sim(플레이어·위협·코옵)에
    /// 묶여 있다. 랩은 Sim 없이 도는 씬이라 필요한 것만 여기 둔다: 추적 · 데드존 · 방 경계 클램프.
    /// 값은 본선 감각과 같은 대역으로 맞췄다(followSpeed 0.07×60dt · 데드존 있음).
    /// </summary>
    public sealed class LabCameraFollow : MonoBehaviour
    {
        [Tooltip("따라갈 대상. 비면 LabPlayer 의 물리 몸통을 찾는다.")]
        [SerializeField] Transform _target;

        [Tooltip("이 반경(칸) 안에서는 카메라가 움직이지 않는다. 걸음마다 화면이 흔들리는 것을 막는다.")]
        [SerializeField, Range(0f, 4f)] float _deadzoneCells = 1.6f;

        [Tooltip("추적 속도. 본선 CameraRig 와 같은 대역(0.07 × 60dt).")]
        [SerializeField, Range(0.01f, 0.5f)] float _followSpeed = 0.07f;

        [Tooltip("방 경계(칸). 카메라가 방 밖의 빈 공간을 비추지 않게 가둔다.")]
        [SerializeField] Rect _roomBounds = new Rect(1f, 1f, 32f, 18f);

        Camera _camera;

        public Transform Target { get => _target; set => _target = value; }

        /// <summary>빌더가 방 크기에서 계산해 넣는다.</summary>
        public void EditorAssign(Transform target, Rect roomBounds)
        {
            _target = target;
            _roomBounds = roomBounds;
        }

        void OnEnable()
        {
            _camera = GetComponent<Camera>();
            if (_target == null)
            {
                var player = FindAnyObjectByType<LabPlayer>();
                if (player != null) _target = player.FollowTarget;
            }
            SnapToTarget();
        }

        void LateUpdate()
        {
            if (_target == null) return;

            var cam = transform.position;
            Vector2 want = _target.position;
            Vector2 now = new Vector2(cam.x, cam.y);

            // 데드존 — 대상이 이 반경을 벗어난 만큼만 목표를 옮긴다.
            var delta = want - now;
            float dist = delta.magnitude;
            if (dist > _deadzoneCells)
                now += delta.normalized * (dist - _deadzoneCells);

            float t = Mathf.Clamp01(_followSpeed * 60f * Time.unscaledDeltaTime);
            var next = Vector2.Lerp(new Vector2(cam.x, cam.y), now, t);
            transform.position = new Vector3(Clamped(next).x, Clamped(next).y, cam.z);
        }

        /// <summary>방 밖을 비추지 않게 가둔다. 화면이 방보다 크면 방 중앙에 고정한다.</summary>
        Vector2 Clamped(Vector2 p)
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            float halfY = _camera != null ? _camera.orthographicSize : 4f;
            float halfX = _camera != null ? halfY * _camera.aspect : halfY * 1.78f;

            float minX = _roomBounds.xMin + halfX, maxX = _roomBounds.xMax - halfX;
            float minY = _roomBounds.yMin + halfY, maxY = _roomBounds.yMax - halfY;

            return new Vector2(
                minX <= maxX ? Mathf.Clamp(p.x, minX, maxX) : _roomBounds.center.x,
                minY <= maxY ? Mathf.Clamp(p.y, minY, maxY) : _roomBounds.center.y);
        }

        public void SnapToTarget()
        {
            if (_target == null) return;
            var z = transform.position.z;
            var p = Clamped(_target.position);
            transform.position = new Vector3(p.x, p.y, z);
        }
    }
}
