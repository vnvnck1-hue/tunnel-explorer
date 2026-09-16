using UnityEngine;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 원근 월드가 화면에 차지하는 영역과 좌표 변환의 단일 출처(3d-perspective-production-plan §4 2단계).
    ///
    /// 원근 월드는 저해상도 RenderTexture 를 레터박스로 확대해 그리므로, 마우스 좌표를
    /// 그대로 카메라에 넣으면 어긋난다. 입력·월드 라벨은 반드시 여기를 거친다.
    /// 비활성일 때는 <see cref="Active"/> 가 false 이고 기존 2D 경로가 그대로 쓰인다.
    /// </summary>
    public static class PerspectiveViewport
    {
        /// <summary>원근 월드가 화면을 소유하고 있는가.</summary>
        public static bool Active { get; private set; }
        /// <summary>원근 카메라. 비활성이면 null.</summary>
        public static Camera Camera { get; private set; }
        /// <summary>RenderTexture 가 실제로 그려지는 화면 사각형(픽셀, y 가 위로 증가하는 Screen 좌표계).</summary>
        public static Rect ScreenRect { get; private set; }

        static readonly Plane Floor = new Plane(Vector3.up, 0f);

        public static void Publish(Camera camera, Rect screenRect)
        {
            Camera = camera;
            ScreenRect = screenRect;
            Active = camera != null && screenRect.width > 1f && screenRect.height > 1f;
        }

        public static void Clear()
        {
            Active = false;
            Camera = null;
        }

        /// <summary>화면 좌표(Screen 좌표계) → 시뮬레이션 XY. 바닥 평면 레이캐스트다.</summary>
        public static bool TryScreenToSim(Vector2 screen, out Vector2 sim)
        {
            sim = default;
            if (!Active) return false;

            var rect = ScreenRect;
            var viewport = new Vector3(
                (screen.x - rect.x) / rect.width,
                (screen.y - rect.y) / rect.height, 0f);
            var ray = Camera.ViewportPointToRay(viewport);
            if (!Floor.Raycast(ray, out float distance)) return false;

            var hit = ray.GetPoint(distance);
            sim = new Vector2(hit.x, hit.z);
            return true;
        }

        /// <summary>시뮬레이션 XY + 높이 → 화면 좌표(Screen 좌표계). 카메라 뒤면 false.</summary>
        public static bool TrySimToScreen(Vector2 sim, float height, out Vector2 screen)
        {
            screen = default;
            if (!Active) return false;

            var viewport = Camera.WorldToViewportPoint(new Vector3(sim.x, height, sim.y));
            if (viewport.z <= 0f) return false;

            var rect = ScreenRect;
            screen = new Vector2(rect.x + viewport.x * rect.width, rect.y + viewport.y * rect.height);
            return true;
        }

        /// <summary>화면 방향 입력(WASD·스틱) → 시뮬레이션 방향. 카메라가 바라보는 쪽이 화면 위다.</summary>
        public static Vector2 ScreenDirectionToSim(Vector2 screenDirection)
        {
            if (!Active || screenDirection.sqrMagnitude < 1e-6f) return Vector2.zero;

            // 카메라의 전방·우측을 바닥 평면에 눕힌다. 피치만 준 리그라 우측은 그대로 월드 X 다.
            var forward = Camera.transform.forward;
            var flatForward = new Vector2(forward.x, forward.z);
            if (flatForward.sqrMagnitude < 1e-6f) flatForward = Vector2.up;
            flatForward.Normalize();
            var flatRight = new Vector2(flatForward.y, -flatForward.x);

            var sim = flatRight * screenDirection.x + flatForward * screenDirection.y;
            return sim.sqrMagnitude > 1e-6f ? sim.normalized : Vector2.zero;
        }

        /// <summary>RenderTexture 를 ScaleToFit 로 확대했을 때의 화면 사각형.</summary>
        public static Rect FitRect(int textureWidth, int textureHeight, int screenWidth, int screenHeight)
        {
            float scale = Mathf.Min((float)screenWidth / textureWidth, (float)screenHeight / textureHeight);
            float w = textureWidth * scale, h = textureHeight * scale;
            return new Rect((screenWidth - w) * 0.5f, (screenHeight - h) * 0.5f, w, h);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Clear();
    }
}
