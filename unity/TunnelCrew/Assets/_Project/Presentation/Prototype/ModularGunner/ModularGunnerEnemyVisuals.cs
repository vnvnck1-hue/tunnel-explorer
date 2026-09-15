using UnityEngine;

namespace TunnelCrew.Presentation.Prototype
{
    /// <summary>
    /// 로드맵 4.13 — 적별 시각 언어.
    /// 승인 아트의 예고 포즈 3종, 체력 단계 4종, 엘리트 변형 3종을 한 곳에서 고른다.
    /// 프레임 선택 규칙만 담고 전투 로직은 <see cref="ModularGunnerEnemy"/> 가 쥔다.
    /// </summary>
    public sealed class ModularGunnerEnemyVisuals : MonoBehaviour
    {
        public enum Telegraph
        {
            None = -1,
            Charge = 0,
            Ranged = 1,
            Slam = 2,
        }

        [SerializeField] Sprite[] _telegraph;   // charge / ranged / slam
        [SerializeField] Sprite[] _stages;      // 무손상 → 치명
        [SerializeField] Sprite[] _elite;       // idle / charge / slam
        [SerializeField] SpriteRenderer _renderer;
        [SerializeField] bool _isElite;

        public bool IsElite => _isElite;
        public int StageCount => _stages != null ? _stages.Length : 0;

        public void EditorAssign(SpriteRenderer renderer, Sprite[] telegraph, Sprite[] stages, Sprite[] elite)
        {
            _renderer = renderer;
            _telegraph = telegraph;
            _stages = stages;
            _elite = elite;
        }

        /// <summary>엘리트로 승격한다. 팔레트와 외곽광이 통째로 바뀐다.</summary>
        public void MakeElite()
        {
            _isElite = true;
            ApplyStage(0);
        }

        /// <summary>
        /// 체력 비율(1 = 만피, 0 = 사망 직전)에 맞는 손상 프레임을 고른다.
        /// 엘리트는 손상 단계 아트가 따로 없어 대기 프레임을 쓰고 색으로만 구분한다.
        /// </summary>
        public void ApplyHealth(float ratio)
        {
            if (_stages == null || _stages.Length == 0) return;
            int last = _stages.Length - 1;
            int stage = Mathf.Clamp(Mathf.RoundToInt((1f - Mathf.Clamp01(ratio)) * last), 0, last);
            ApplyStage(stage);
        }

        void ApplyStage(int stage)
        {
            if (_renderer == null) return;
            if (_isElite && _elite != null && _elite.Length > 0)
            {
                _renderer.sprite = _elite[0];
                return;
            }
            if (_stages != null && _stages.Length > 0)
                _renderer.sprite = _stages[Mathf.Clamp(stage, 0, _stages.Length - 1)];
        }

        /// <summary>공격 예고 포즈로 바꾼다. <see cref="Telegraph.None"/> 이면 원래 프레임으로 돌린다.</summary>
        public void ShowTelegraph(Telegraph pose, float healthRatio)
        {
            if (_renderer == null) return;
            if (pose == Telegraph.None)
            {
                ApplyHealth(healthRatio);
                return;
            }

            if (_isElite && _elite != null && _elite.Length >= 3)
            {
                // 엘리트 예고는 돌진과 내려찍기 두 종류만 있다.
                _renderer.sprite = pose == Telegraph.Slam ? _elite[2] : _elite[1];
                return;
            }
            if (_telegraph != null && _telegraph.Length > 0)
                _renderer.sprite = _telegraph[Mathf.Clamp((int)pose, 0, _telegraph.Length - 1)];
        }
    }
}
