using UnityEngine;
using UnityEngine.InputSystem;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 랩에서 조명을 판단하려면 광원이 움직여야 한다 — 고정된 탐색광은 바닥의 어느 부분이
    /// 어떻게 밝아지는지 보여주지 못한다. WASD 로 폰을 옮기고, 자식으로 달린 탐색광이 따라간다.
    ///
    /// <b>이주 1단계(2026-09-09)</b> — 같은 오브젝트에 <see cref="VisualHeightAnchor"/> 가 있으면
    /// <c>transform.position</c> 이 아니라 <see cref="VisualHeightAnchor.groundPosition"/>(셀 좌표)을
    /// 움직인다. 앵커의 <c>Apply</c> 가 LateUpdate 마다 트랜스폼을 <c>groundPosition</c> 으로
    /// 되돌리므로 트랜스폼을 직접 밀면 매 프레임 튕긴다. 앵커가 없으면 예전처럼 트랜스폼을 민다.
    ///
    /// 게임플레이 이동이 아니다 — 충돌도 가속도도 없다. 본선 이동은 <c>Sim</c> 쪽 몫이고
    /// 이 컴포넌트는 랩 전용이다.
    /// </summary>
    public sealed class LightingLabPawn : MonoBehaviour
    {
        [Tooltip("초당 셀(= 월드 유닛, 제작 기준 투영은 항등).")]
        [SerializeField, Range(0.5f, 12f)] float _speed = 3.2f;

        [Tooltip("이동 가능 영역(셀). 카메라 밖으로 나가 버리는 것을 막는다.")]
        [SerializeField] Rect _bounds = new Rect(-6.5f, -3.4f, 13f, 6.6f);

        VisualHeightAnchor _anchor;

        void Awake() => TryGetComponent(out _anchor);

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            var dir = Vector2.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) dir.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) dir.y -= 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) dir.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir.x += 1f;

            if (dir.sqrMagnitude < 0.0001f) return;

            var step = dir.normalized * (_speed * Time.deltaTime);

            if (_anchor != null)
            {
                var g = _anchor.groundPosition + step;
                _anchor.groundPosition = new Vector2(
                    Mathf.Clamp(g.x, _bounds.xMin, _bounds.xMax),
                    Mathf.Clamp(g.y, _bounds.yMin, _bounds.yMax));
                return;
            }

            var p = transform.position;
            var next = (Vector2)p + step;
            transform.position = new Vector3(
                Mathf.Clamp(next.x, _bounds.xMin, _bounds.xMax),
                Mathf.Clamp(next.y, _bounds.yMin, _bounds.yMax),
                p.z);
        }
    }
}
