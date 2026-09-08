using System;
using TunnelCrew.Presentation.Visual;
using TunnelCrew.Sim;
using UnityEngine;

namespace TunnelCrew.Presentation
{
    /// <summary>
    /// 원본 카메라(7364~7402행)를 그대로 옮긴 것. 데드존 → 룩어헤드 → 추종 → 클램프.
    ///
    /// **M1 한정 결정**: analysis-04 §5 는 Cinemachine 3 으로 대체하기로 했지만,
    /// M1 의 목표가 "원본과 같은 감각인가" 를 확인하는 것이라 먼저 원본 공식을 그대로 옮긴다.
    /// Cinemachine 이 실제로 필요해지는 시점(M3, Impulse · Target Group)에 이전한다.
    /// 그때 이 파일의 수치가 이전 기준이 된다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] float _zoomLerpRate = (float)SimTuning.ZoomLerpRate;

        /// <summary>
        /// 비주얼 오버홀 카메라 프로파일(§10). <b>비워 두면 기존 동작과 완전히 같다.</b>
        ///
        /// 본선 씬을 이번 배치에서 바꾸지 않기 위해 옵션으로 두었다. Visual Lab 에서
        /// 기준 줌·근접 줌·협동 줌을 비교한 뒤에 RunBootstrap 에서 꽂는다.
        /// </summary>
        [SerializeField] WorldVisualProfile _profile;

        /// <summary>프로파일이 있을 때 쓸 줌 모드. 없으면 무시된다.</summary>
        [SerializeField] CameraViewMode _viewMode = CameraViewMode.Base;

        Camera _cam;
        WorldGrid _world;
        Func<PlayerState> _player;

        /// <summary>원본 G.zDyn — 부드럽게 따라가는 줌.</summary>
        float _zoom;
        /// <summary>원본 G.camX/camY — 뷰 좌상단.</summary>
        Vector2 _camOrigin;
        bool _initialized;
        /// <summary>시네마틱이 잡고 있으면 (중심, 줌 배율) — 추종 상태는 건드리지 않고 최종 위치만 바꾼다 (원본 tcBossFx 의 G.camX/camY/G.Z 직접 제어).</summary>
        public Func<(Vector2 center, float zoomMul)?> CineOverride;
        /// <summary>관전 모드 크루 시점 — 값이 있으면 그 점을 중심으로 추종한다(데드존·룩어헤드 없이). 원본 OBS.camera 는 k=dt·5 로 미리 보간해 넘긴다.</summary>
        public Func<Vector2?> FollowOverride;

        /// <summary>비주얼 프로파일. null 이면 원본 공식(SimTuning)을 쓴다.</summary>
        public WorldVisualProfile Profile
        {
            get => _profile;
            set { _profile = value; _initialized = false; }
        }

        /// <summary>줌 모드. 프로파일이 있을 때만 뜻이 있다.</summary>
        public CameraViewMode ViewMode
        {
            get => _viewMode;
            set => _viewMode = value;
        }

        /// <summary>이번 프레임의 목표 가시 세로 셀 수. 디버그 오버레이가 읽는다.</summary>
        public float TargetViewCells { get; private set; }

        void Awake() => _cam = GetComponent<Camera>();

        // 투영이 바뀌면 _camOrigin 이 옛 좌표계 값이라 한 프레임 튄다. 추종을 처음부터 다시 잡는다.
        void OnEnable() => IsometricProjection.Changed += ResnapFollow;
        void OnDisable() => IsometricProjection.Changed -= ResnapFollow;
        void ResnapFollow() => _initialized = false;

        public void Bind(WorldGrid world, Func<PlayerState> player)
        {
            _world = world;
            _player = player;
            _initialized = false;
        }

        void LateUpdate()
        {
            if (_world == null || _player == null) return;
            var p = _player();
            if (p == null) return;

            // ── 줌. 원본은 화면 픽셀 배율이지만, 여기서는 직교 카메라 높이(셀)로 다룬다.
            // 원본 G.Z 는 "1 월드픽셀 → 몇 화면픽셀" 이라 셀 단위로는 화면 높이 = LH / (Z*CELL).
            // 기준 줌(baseZoom)에서 세로로 보이는 셀 수를 화면 비율과 무관하게 고정한다.
            // 프로파일이 있으면 §10 의 가시 셀 수를 그대로 쓴다. 없으면 원본 공식.
            float targetCells;
            if (_profile != null)
            {
                targetCells = _profile.ViewCellsFor(_viewMode);
            }
            else
            {
                float baseCells = (float)(1080.0 / (SimTuning.BaseZoom * SimTuning.PxPerCell));
                targetCells = baseCells / (float)SimTuning.ZoomInMul;
            }
            TargetViewCells = targetCells;

            if (!_initialized) _zoom = targetCells;
            else _zoom = Mathf.Lerp(_zoom, targetCells, Mathf.Min(1f, Time.deltaTime * _zoomLerpRate));

            _cam.orthographicSize = _zoom * 0.5f;
            var cine = CineOverride?.Invoke();
            if (cine.HasValue)
            {
                _cam.orthographicSize = _zoom * 0.5f / Mathf.Max(.2f, cine.Value.zoomMul);
                transform.position = new Vector3(cine.Value.center.x, cine.Value.center.y, -10f);
                return;
            }

            float vh = _zoom;
            float vw = vh * _cam.aspect;

            // ── 룩어헤드: 조준 방향으로 앞서 본다
            var pos = IsometricProjection.ToRender(p.Position);
            var look = IsometricProjection.DirectionToRender(p.Aim).normalized * (float)SimTuning.LookAhead;
            Vector2 target = pos + look;

            // ── 데드존 + 추종. 카메라 중심은 화면 세로 42% 지점(상하 비대칭).
            // 원본은 y 가 아래로 증가하므로 위에서 42%, Unity 는 위로 증가하므로 아래에서 58%.
            float anchorFromBottom = _profile != null
                ? _profile.AnchorFromBottom
                : 1f - (float)SimTuning.VerticalAnchor;
            Vector2 center = _initialized
                ? _camOrigin + new Vector2(vw * 0.5f, vh * anchorFromBottom)
                : target;

            var follow = FollowOverride?.Invoke();
            if (follow.HasValue) center = _initialized ? follow.Value : follow.Value;
            else if (_initialized)
            {
                Vector2 d = target - center;
                float dist = d.magnitude;
                float dead = (float)SimTuning.DeadZone;
                if (dist > dead)
                {
                    Vector2 n = d / dist;
                    Vector2 aim = center + n * (dist - dead);
                    float t = Mathf.Min(1f, (float)SimTuning.FollowSpeed * 60f * Time.deltaTime);
                    center += (aim - center) * t;
                }
            }

            // ── 월드 경계 클램프. 원본 tcClampCamera 의 여유값.
            Vector2 origin = center - new Vector2(vw * 0.5f, vh * anchorFromBottom);
            float padX = Mathf.Min(vw * (float)SimTuning.CameraPadRatio, (float)SimTuning.CameraPadMax);
            float padY = Mathf.Min(vh * (float)SimTuning.CameraPadRatio, (float)SimTuning.CameraPadMax);
            IsometricProjection.Bounds(_world.Cols, _world.Rows, out var worldMin, out var worldMax);
            origin.x = Mathf.Clamp(origin.x, worldMin.x - padX, Mathf.Max(worldMin.x - padX, worldMax.x - vw + padX));
            origin.y = Mathf.Clamp(origin.y, worldMin.y - padY, Mathf.Max(worldMin.y - padY, worldMax.y - vh + padY));

            _camOrigin = origin;
            _initialized = true;

            Vector2 finalCenter = origin + new Vector2(vw * 0.5f, vh * anchorFromBottom);
            // 킥·흔들림은 추종 상태(_camOrigin)에는 넣지 않고 최종 위치에만 더한다.
            // 그래야 흔들림이 끝나면 정확히 원래 자리로 돌아온다.
            if (Feedback.Instance != null) finalCenter += Feedback.Instance.CameraOffset;
            transform.position = new Vector3(finalCenter.x, finalCenter.y, -10f);
        }

        /// <summary>화면 좌표 → 월드 좌표(셀 단위).</summary>
        public Vector2 ScreenToWorld(Vector3 screen)
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            var w = _cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_cam.transform.position.z));
            return IsometricProjection.ToWorld(new Vector2(w.x, w.y));
        }
    }
}
