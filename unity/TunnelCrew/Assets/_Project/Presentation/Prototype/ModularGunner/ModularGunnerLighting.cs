namespace TunnelCrew.Presentation.Prototype
{
    /// <summary>
    /// 로드맵 4.5 — 조명 계층의 밝기 우선순위를 한 곳에 모은다.
    /// 값이 여러 파일에 흩어져 있으면 효과를 하나 밝게 고칠 때마다 다른 층이 묻힌다.
    /// 아래 순서(환경광 &lt; 탐색광 &lt; 램프 &lt; 피격광 &lt; 총구 섬광)를 깨지 않는 선에서만 조정한다.
    /// </summary>
    public static class ModularGunnerLighting
    {
        /// <summary>환경광. 형태만 겨우 읽히는 최저 바닥값.</summary>
        public const float AmbientIntensity = 0.34f;

        /// <summary>고정 램프. 공간의 기준 밝기를 만든다.</summary>
        public const float LampIntensity = 1.15f;

        /// <summary>결정 같은 장식 발광. 위치만 알리고 바닥을 밝히지 않는다.</summary>
        public const float CrystalIntensity = 0.55f;

        /// <summary>탄두 중심광. 이동 궤적이 읽히는 정도까지만.</summary>
        public const float ProjectileIntensity = 0.85f;

        /// <summary>일반 피격광. 램프보다 밝지만 한 프레임짜리다.</summary>
        public const float ImpactIntensity = 0.6f;

        /// <summary>치명타·사망 피격광.</summary>
        public const float LethalImpactIntensity = 1.15f;

        /// <summary>총구 섬광. 이 씬에서 가장 밝고 가장 짧다.</summary>
        public const float MuzzleIntensity = 1.35f;

        /// <summary>
        /// 밝은 효과가 캐릭터 실루엣을 지우지 않도록 스프라이트 알파에 두는 상한.
        /// 버스트는 이 값을 넘지 않으므로 뒤에 선 캐릭터의 외곽선이 항상 남는다.
        /// </summary>
        public const float MaxFlashAlpha = 0.95f;
    }
}
