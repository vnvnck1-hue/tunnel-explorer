using UnityEngine;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 기능명세서 §7.5·§12.1 — 대기 원근 프로파일. 지층마다 하나씩 만든다.
    ///
    /// <b>후처리는 형태를 대체하지 않는다.</b> §7.5 가 "이미 존재하는 깊이층의 분리를
    /// 강화하는 용도로만" 쓰라고 못 박았으므로 기본값을 낮게 둔다. 여기 값을 올려서
    /// 어두운 화면을 밝게 만들려 하면 안 된다 — 그건 알베도·최소광·조명의 일이다.
    /// </summary>
    [CreateAssetMenu(menuName = "Tunnel Crew/Atmosphere Profile", fileName = "AtmosphereProfile")]
    public sealed class AtmosphereProfile : ScriptableObject
    {
        [Header("저층 안개 (§7.5)")]
        [Tooltip("안개 색. 지층 주조색과 같은 계열로 둔다.")]
        public Color fogColor = new Color(0.18f, 0.20f, 0.30f);

        [Tooltip("안개 최대 농도. 화면 아래쪽(카메라에 가까운 저층)에서 가장 진하다.")]
        [Range(0f, 1f)] public float fogDensity = 0.22f;

        [Tooltip("안개가 닿는 화면 높이(0 = 화면 아래, 1 = 화면 위).")]
        [Range(0f, 1f)] public float fogHeight = 0.42f;

        [Tooltip("안개 경계의 부드러움. 작으면 띠처럼 끊긴다.")]
        [Range(0.01f, 1f)] public float fogSoftness = 0.45f;

        [Header("깊이 그룹 색 분리 (§7.5)")]
        [Tooltip("가까운 층(화면 아래)에 얹는 색.")]
        public Color nearTint = new Color(0.24f, 0.16f, 0.26f);

        [Tooltip("먼 층(화면 위)에 얹는 색.")]
        public Color farTint = new Color(0.14f, 0.20f, 0.30f);

        [Tooltip("색 분리 세기. 약해야 한다 — 층을 갈라 보이게만 하고 색을 덮지 않는다.")]
        [Range(0f, 0.5f)] public float depthSeparation = 0.12f;

        [Header("화면 가장자리 암부 (§7.5)")]
        public Color vignetteColor = new Color(0.02f, 0.02f, 0.04f);
        [Range(0f, 1f)] public float vignetteStrength = 0.35f;

        [Tooltip("암부가 시작하는 반경(화면 중심에서). 1 에 가까우면 가장자리만 어두워진다.")]
        [Range(0f, 1.5f)] public float vignetteInner = 0.55f;

        [Tooltip("암부가 최대에 이르는 반경.")]
        [Range(0f, 2f)] public float vignetteOuter = 1.15f;

        [Header("필름 그레인 (§7.5)")]
        [Tooltip("세기. 어두운 화면에서 밴딩을 깨는 정도면 충분하다.")]
        [Range(0f, 0.2f)] public float grainStrength = 0.018f;

        [Tooltip("기준 화면 높이당 그레인 점 수. 해상도가 바뀌어도 체감 크기가 같도록 화면 비율로 센다.")]
        [Range(64f, 2160f)] public float grainDensity = 540f;

        [Tooltip("그레인을 시간에 따라 흔든다. 자동 캡처(§16.3)는 재현성을 위해 끈 상태로 찍는다.")]
        public bool animateGrain = true;

        /// <summary>셰이더가 읽는 안개 모양 벡터.</summary>
        public Vector4 FogShape => new Vector4(fogHeight, Mathf.Max(0.01f, fogSoftness), 0f, 0f);

        /// <summary>셰이더가 읽는 암부 모양 벡터. inner 가 outer 보다 크면 뒤집어 준다.</summary>
        public Vector4 VignetteShape
        {
            get
            {
                float inner = vignetteInner, outer = vignetteOuter;
                if (outer <= inner) outer = inner + 0.01f;
                return new Vector4(inner, outer, vignetteStrength, 0f);
            }
        }
    }
}
