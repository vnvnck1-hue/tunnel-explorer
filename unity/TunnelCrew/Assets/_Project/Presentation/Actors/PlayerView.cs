using System.Collections.Generic;
using TunnelCrew.Sim;
using UnityEngine;

namespace TunnelCrew.Presentation
{
    /// <summary>
    /// 플레이어 스프라이트. 원본은 좌측 5방향 시트만 만들고 se/e/ne 를 X 반전해 썼다.
    ///
    /// M1 은 프레임을 직접 골라 넣는다. Animator 블렌드 트리로 옮기는 것은
    /// 걷기 외 동작(드릴·대시·다운)이 붙는 M3 에서 한다(analysis-04 §4).
    /// </summary>
    public sealed class PlayerView : MonoBehaviour
    {
        [SerializeField] SpriteRenderer _renderer;
        [SerializeField] float _walkFps = 10f;

        /// <summary>방향 8종 → (시트 키, X반전).</summary>
        static readonly (string key, bool flip)[] Dir8 =
        {
            ("w",  true),   // 0 E  ← w 반전
            ("sw", true),   // 1 SE ← sw 반전
            ("s",  false),  // 2 S
            ("sw", false),  // 3 SW
            ("w",  false),  // 4 W
            ("nw", false),  // 5 NW
            ("n",  false),  // 6 N
            ("nw", true),   // 7 NE ← nw 반전
        };

        readonly Dictionary<string, Sprite[]> _walk = new Dictionary<string, Sprite[]>();
        float _animTime;

        /// <summary>에디터/부트스트랩이 방향별 걷기 프레임을 넣어 준다.</summary>
        public void SetWalkFrames(string dirKey, Sprite[] frames) => _walk[dirKey] = frames;

        public bool HasFrames => _walk.Count > 0;

        public void Render(PlayerState p, float dt)
        {
            transform.position = new Vector3((float)p.Position.X, (float)p.Position.Y, 0f);
            if (_renderer == null) return;

            bool moving = p.Velocity.Length > 0.05 || p.DashActive;
            // 방향은 이동 중이면 이동 방향, 아니면 조준 방향
            double angle = moving && p.Velocity.Length > 0.05 ? p.Velocity.Angle : p.Aim;

            int idx = Dir8Index(angle);
            var (key, flip) = Dir8[idx];
            _renderer.flipX = flip;

            if (!_walk.TryGetValue(key, out var frames) || frames == null || frames.Length == 0) return;

            if (moving) _animTime += dt * _walkFps;
            else _animTime = 0f;

            int f = frames.Length > 0 ? ((int)_animTime % frames.Length + frames.Length) % frames.Length : 0;
            _renderer.sprite = frames[f];

            // 손맛 변형 — 원본 FEEL.transform(). 피벗이 발밑이라 스케일은 발을 기준으로 먹는다.
            if (Feedback.Instance != null)
            {
                var sq = Feedback.Instance.PlayerSquash(new Vector2((float)p.Velocity.X, (float)p.Velocity.Y));
                transform.localScale = new Vector3(sq.ScaleX, sq.ScaleY, 1f);
                transform.position += new Vector3(sq.OffsetX, sq.OffsetY, 0f);
            }

            // 무적 프레임 깜빡임 · 기절 어둡게 · 다운 회색
            Color c = Color.white;
            if (p.Downed) c = new Color(0.45f, 0.45f, 0.5f);
            else if (p.StunTime > 0) c = new Color(0.75f, 0.7f, 0.8f);
            else if (p.IFrames > 0 && ((int)(Time.unscaledTime * 24) & 1) == 0) c = new Color(1f, 0.55f, 0.55f, 0.75f);
            _renderer.color = c;
        }

        /// <summary>
        /// 원본 minerDir8(a) = ['E','SE','S','SW','W','NW','N','NE'][round(a/(π/4)) &amp; 7].
        /// 원본은 y 가 아래로 증가하므로 각도 부호가 반대다. Unity 는 y 가 위로 증가하니 뒤집는다.
        /// </summary>
        static int Dir8Index(double angleRadians)
        {
            double a = -angleRadians;   // 화면 좌표계 → 원본 좌표계
            int i = JsMath.Round(a / (System.Math.PI / 4.0));
            return ((i % 8) + 8) % 8;
        }
    }
}
