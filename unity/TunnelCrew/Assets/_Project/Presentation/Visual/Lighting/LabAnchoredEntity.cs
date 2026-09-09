using UnityEngine;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 랩 개체에 접촉 그림자와 가림 실루엣을 <b>런타임에</b> 붙인다.
    ///
    /// <b>왜 빌더가 직접 붙이지 않는가</b> — <see cref="ContactShadow"/> 는
    /// <c>ContactShadowRenderer.cs</c> 안에, <see cref="OccludedSilhouette"/> 는
    /// <c>OccludedSilhouetteRenderer.cs</c> 안에 정의된 두 번째 MonoBehaviour 다. Unity 는
    /// 파일명과 다른 클래스를 스크립트 에셋에 연결하지 못하므로, 씬에 직렬화하면 <c>m_Script</c> 가
    /// 로컬 fileID 로 박혀 로드 때 "The referenced script (Unknown) on this Behaviour is missing" 이
    /// 된다(2026-09-09 프리셋 랩에서 실제로 그랬다). VisualLab 은 <c>Rebuild</c> 에서 런타임 생성으로
    /// 이 문제를 피한다 — 이 컴포넌트는 그 방식을 개체 하나 단위로 옮긴 것이다.
    ///
    /// 프로덕션 파일을 분리하는 대신 감싸는 쪽을 택한 이유: 본선이 그 두 클래스를 런타임으로만
    /// 쓰고 있어서, 파일 분리는 이 랩을 위해 공유 코드를 건드리는 일이 된다.
    /// </summary>
    [RequireComponent(typeof(VisualHeightAnchor))]
    public sealed class LabAnchoredEntity : MonoBehaviour
    {
        public enum Silhouette { None, Interest, Threat }

        [Tooltip("가림 실루엣 종류. None 이면 접촉 그림자만 붙인다.")]
        [SerializeField] Silhouette _silhouette = Silhouette.None;

        [Tooltip("실루엣 색. Interest 는 팀 색 림, Threat 는 위협 채움.")]
        [SerializeField] Color _silhouetteColor = new Color(0.42f, 0.82f, 1f);

        void Awake()
        {
            // 0 = "프로파일 값을 쓴다"(ContactShadowRenderer.Apply). 프리셋이 프로파일 복제본의
            // contactShadowOpacity 를 밀면 여기로 반영된다.
            if (!TryGetComponent<ContactShadow>(out _))
            {
                var shadow = gameObject.AddComponent<ContactShadow>();
                shadow.radius = 0f;
                shadow.opacity = 0f;
            }

            if (_silhouette == Silhouette.None) return;
            if (TryGetComponent<OccludedSilhouette>(out _)) return;

            var body = GetComponentInChildren<SpriteRenderer>();
            if (body == null) return;

            var sil = gameObject.AddComponent<OccludedSilhouette>();
            sil.body = body;
            sil.mode = _silhouette == Silhouette.Threat ? SilhouetteMode.Threat : SilhouetteMode.Interest;
            sil.color = _silhouetteColor;
        }
    }
}
