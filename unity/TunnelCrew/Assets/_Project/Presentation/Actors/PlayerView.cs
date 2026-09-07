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
        float _animTime, _walkT;

        /// <summary>
        /// 발(피벗)을 시뮬 위치보다 얼마나 아래에 두는가. 원본 drawDrillerSprite 는
        /// `translate(x,y)` 뒤 `LIT.spr(..., -w*.5, r - pivot*scale, ...)` 로 그려서 피벗 픽셀이
        /// 정확히 y+r(충돌원 바닥)에 놓인다. 시트 피벗은 이미 발밑이므로 그만큼만 내린다.
        /// 크루의 라벨·HP바 등 자식 오프셋은 이 값을 더해 세계 좌표를 유지한다(CrewView).
        /// </summary>
        public const float FootDrop = (float)SimTuning.PlayerRadius;

        /// <summary>발밑 그림자 캐스터 — 벽(WallShadowBuilder)과 같은 Light2D 그림자. 크루도 이 뷰를 쓰므로 함께 붙는다.</summary>
        void Awake()
        {
            // 발(피벗)이 transform 원점이므로 그림자 캐스터는 발 둘레에. 고정 접지 그림자는 두지 않는다 — 조명 각도에 따라 변하는 그림자만 (사용자 결정 2026-09-07)
            // 캐스터는 몸 폭에 맞춘다(반지름 0.9배 · 세로 0.55) — 손전등을 받으면 몸 폭만큼의 그림자 줄기가 뒤로 뻗는다
            WallShadowBuilder.AttachActorCaster(transform, (float)SimTuning.PlayerRadius * .9f, IsometricProjection.ShadowSquash * 1.1f);
        }

        /// <summary>에디터/부트스트랩이 방향별 걷기 프레임을 넣어 준다.</summary>
        public void SetWalkFrames(string dirKey, Sprite[] frames) => _walk[dirKey] = frames;

        public bool HasFrames => _walk.Count > 0;

        public void Render(PlayerState p, float dt)
        {
            bool moving = p.Velocity.Length > 0.05 || p.DashActive;
            _walkT += dt;

            // 원본: 걷는 동안 sin(t*16)*r*.035 만큼 위아래로 들썩임 (화면 y 는 아래가 +, Unity 는 위가 + 라 부호 반전)
            float bob = moving ? -Mathf.Sin(_walkT * 16f) * FootDrop * .035f : 0f;
            var renderPos = IsometricProjection.ToRender(p.Position);
            transform.position = new Vector3(renderPos.x, renderPos.y - FootDrop + bob, 0f);
            if (_renderer == null) return;
            // 방향은 이동 중이면 이동 방향, 아니면 조준 방향
            double worldAngle = moving && p.Velocity.Length > 0.05 ? p.Velocity.Angle : p.Aim;
            double angle = IsometricProjection.AngleToRender(worldAngle);

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
                var sq = Feedback.Instance.PlayerSquash(IsometricProjection.ToRender(p.Velocity));
                // dt=0(정지 프레임)에서 스쿼시 타이머가 0/0 이 될 수 있다 — NaN 이면 그 프레임은 건드리지 않는다
                if (!float.IsNaN(sq.ScaleX) && !float.IsNaN(sq.ScaleY))
                {
                    transform.localScale = new Vector3(sq.ScaleX, sq.ScaleY, 1f);
                    if (!float.IsNaN(sq.OffsetX) && !float.IsNaN(sq.OffsetY)) transform.position += new Vector3(sq.OffsetX, sq.OffsetY, 0f);
                }
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
