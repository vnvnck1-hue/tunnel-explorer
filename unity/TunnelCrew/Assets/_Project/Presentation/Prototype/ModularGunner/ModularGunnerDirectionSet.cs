using UnityEngine;

namespace TunnelCrew.Presentation.Prototype
{
    /// <summary>
    /// 로드맵 4.2 — 조준 방향을 8방향 스프라이트로 바꾼다.
    ///
    /// 승인 아트는 S / SE / E / NE / N 다섯 방향만 있다. 서쪽 계열은 좌우 미러로 만든다.
    /// 미러를 쓰므로 정면(S)과 후면(N)은 뒤집어도 같아야 하고, 실제로 그렇게 그려져 있다.
    /// </summary>
    public static class ModularGunnerDirectionSet
    {
        public const int Count = 5;

        public const int South = 0;
        public const int SouthEast = 1;
        public const int East = 2;
        public const int NorthEast = 3;
        public const int North = 4;

        /// <summary>
        /// 조준 벡터에서 (프레임 번호, 좌우 반전) 을 고른다.
        /// 8방향 섹터로 나누고 서쪽 절반은 동쪽 프레임을 뒤집어 쓴다.
        /// </summary>
        public static void Resolve(Vector2 aim, out int frame, out bool flip)
        {
            if (aim.sqrMagnitude < 0.0001f)
            {
                frame = South;
                flip = false;
                return;
            }

            float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;   // -180..180, 0 이 동쪽
            flip = angle > 90f || angle < -90f;                        // 서쪽 절반은 미러
            float folded = flip ? Mathf.Sign(angle) * 180f - angle : angle;

            // folded 는 -90(남) ~ 0(동) ~ 90(북). 45도 섹터 네 개로 나눈다.
            if (folded < -67.5f) frame = South;
            else if (folded < -22.5f) frame = SouthEast;
            else if (folded < 22.5f) frame = East;
            else if (folded < 67.5f) frame = NorthEast;
            else frame = North;

            // 정면과 후면은 미러가 의미 없다. 뒤집지 않아야 헤드램프 위치가 튀지 않는다.
            if (frame == South || frame == North) flip = false;
        }

        /// <summary>
        /// 무기와 손을 몸통 앞에 그릴지 뒤에 그릴지 정한다.
        /// 위쪽을 조준하면 캐릭터가 등을 보이므로 총이 몸 뒤로 가야 한다.
        /// </summary>
        public static bool WeaponBehindBody(int frame) => frame == North || frame == NorthEast;
    }
}
