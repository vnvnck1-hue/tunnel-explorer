using UnityEngine;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// §7.2 의 "서로 다른 최소광 계수" 를 받는 표면 분류.
    /// <see cref="WorldVisualProfile.minLightBySurface"/> 의 xyzw 순서와 같다.
    /// </summary>
    public enum MinLightSlot
    {
        WallTop = 0,
        WallFront = 1,
        Floor = 2,
        Character = 3,
    }

    /// <summary>
    /// 기능명세서 §7.1·§12.1 — 표면 한 종류의 재질 채널 묶음.
    ///
    /// Albedo 는 스프라이트(또는 아틀라스)에서 오므로 여기에 두지 않는다. Normal·Emission·
    /// Material Mask·AO 는 <b>Albedo 아틀라스와 같은 배치</b>의 텍스처여야 한다(아트 규격 §8.1
    /// "월드 아틀라스는 최대 4096×4096 단위로 재질 채널별 동일 배치를 유지한다").
    ///
    /// 임포트 세컨더리 텍스처 방식은 URP 17 타일맵·스프라이트 조명에 반영되지 않는 것을
    /// 확인했으므로(2026-09-07 기록) 머티리얼 프로퍼티로 직접 넣는다.
    /// </summary>
    [CreateAssetMenu(menuName = "Tunnel Crew/Surface Material Set", fileName = "SurfaceMaterialSet")]
    public sealed class SurfaceMaterialSet : ScriptableObject
    {
        public enum Kind
        {
            /// <summary>바닥·벽·소품. Tilemap 청크 메시를 위해 탄젠트를 강제하는 셰이더를 쓴다.</summary>
            World = 0,
            /// <summary>캐릭터·적. 스프라이트의 탄젠트를 그대로 쓰고 최저 조도가 높다.</summary>
            Character = 1,
        }

        [Header("셰이더 선택")]
        public Kind kind = Kind.World;

        [Tooltip("§7.2 의 표면별 최소광. 프로파일의 minLightBySurface 에서 이 슬롯을 읽는다.")]
        public MinLightSlot minLightSlot = MinLightSlot.Floor;

        [Header("채널 (Albedo 는 스프라이트에서 온다)")]
        [Tooltip("탄젠트 공간 노멀. Linear 로 임포트해야 한다 — sRGB 로 들어오면 굴곡이 뒤틀린다.")]
        public Texture2D normal;
        [Tooltip("발광. 비발광은 검정, 광원 중심은 고유 발광색.")]
        public Texture2D emission;
        [Tooltip("R 금속 · G 광택 · B 습윤·결정 · A 효과 강도. Linear 데이터 맵이다.")]
        public Texture2D materialMask;
        [Tooltip("흰색 = 차폐 없음. Linear 데이터 맵이다.")]
        public Texture2D ao;

        [Header("반응")]
        [Range(0f, 3f)] public float normalStrength = 1f;
        [Range(0f, 1f)] public float aoStrength = 1f;
        public Color emissionTint = Color.white;
        [Range(0f, 8f)] public float emissionIntensity = 1f;

        [Tooltip("0 이상이면 프로파일의 표면별 최소광을 무시하고 이 값을 쓴다.")]
        public float minLightOverride = -1f;

        public const string WorldShaderName = "Tunnel Crew/WorldLit";
        public const string CharacterShaderName = "Tunnel Crew/CharacterLit";

        public string ShaderName => kind == Kind.Character ? CharacterShaderName : WorldShaderName;

        static readonly int IdNormal = Shader.PropertyToID("_NormalMap");
        static readonly int IdEmission = Shader.PropertyToID("_EmissionMap");
        static readonly int IdMask = Shader.PropertyToID("_MaskTex");
        static readonly int IdAo = Shader.PropertyToID("_AOMap");
        static readonly int IdNormalStrength = Shader.PropertyToID("_NormalStrength");
        static readonly int IdAoStrength = Shader.PropertyToID("_AOStrength");
        static readonly int IdEmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly int IdEmissionIntensity = Shader.PropertyToID("_EmissionIntensity");
        static readonly int IdMinLight = Shader.PropertyToID("_MinLight");

        /// <summary>
        /// 이 채널 묶음으로 머티리얼을 만든다. 셰이더를 찾지 못하면 null 을 돌려주고
        /// 호출한 쪽이 기본 머티리얼을 그대로 쓴다 — 조용히 검은 화면이 되지 않게 한다.
        /// </summary>
        public Material CreateMaterial(WorldVisualProfile profile, string materialName = null)
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[비주얼] 셰이더 '{ShaderName}' 를 찾지 못했다 — 기본 머티리얼로 그린다.");
                return null;
            }

            var m = new Material(shader) { name = materialName ?? $"{name} ({kind})" };
            Apply(m, profile);
            return m;
        }

        /// <summary>이미 있는 머티리얼에 채널과 수치를 넣는다.</summary>
        public void Apply(Material m, WorldVisualProfile profile)
        {
            if (m == null) return;

            // 텍스처가 없으면 프로퍼티를 건드리지 않는다 — 셰이더의 기본값
            // (bump / black / white) 이 "그 채널 없음" 을 뜻한다.
            if (normal != null) m.SetTexture(IdNormal, normal);
            if (emission != null) m.SetTexture(IdEmission, emission);
            if (ao != null) m.SetTexture(IdAo, ao);

            // 마스크만은 예외다(2026-09-10 실측). _MaskTex 가 비면 셰이더 기본이 <b>white</b> 라 마스크 채널이
            // 전부 1 이 되고, "Additive with Mask" 슬롯의 수정광이 마스크 없는 벽 정면을 통째로 흰색으로
            // 날렸다(앰비언트 0 에서도 (240,204,241)). "마스크 없음" = "마스크 광원에 반응하지 않음" 이어야
            // 하므로 검정을 명시한다. 마스크 채널 계약: docs/unity-port/mask-channel-convention.md.
            m.SetTexture(IdMask, materialMask != null ? materialMask : Texture2D.blackTexture);

            m.SetFloat(IdNormalStrength, normalStrength);
            m.SetFloat(IdAoStrength, aoStrength);
            m.SetColor(IdEmissionColor, emissionTint);
            m.SetFloat(IdEmissionIntensity, emissionIntensity);
            m.SetFloat(IdMinLight, MinLightFor(profile));
        }

        public float MinLightFor(WorldVisualProfile profile)
        {
            if (minLightOverride >= 0f) return minLightOverride;
            if (profile == null) return kind == Kind.Character ? 0.30f : 0.16f;

            var v = profile.minLightBySurface;
            return minLightSlot switch
            {
                MinLightSlot.WallTop => v.x,
                MinLightSlot.WallFront => v.y,
                MinLightSlot.Floor => v.z,
                _ => v.w,
            };
        }

        /// <summary>어떤 채널이 실제로 들어왔는지. 검사기와 디버그 오버레이가 읽는다.</summary>
        public bool HasNormal => normal != null;
        public bool HasEmission => emission != null;
        public bool HasMask => materialMask != null;
        public bool HasAo => ao != null;
    }
}
