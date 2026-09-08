using UnityEditor;
using UnityEngine;

namespace TunnelCrew.EditorTools.ArtPipeline
{
    /// <summary>
    /// 채널별 텍스처 임포트 설정의 <b>단일 출처</b>(기능명세서 §12.2).
    ///
    /// <b>왜 따로 두는가</b> — 설정을 적용하는 코드와 검증하는 코드가 각자 규칙을 들고
    /// 있으면 둘이 조용히 갈라진다. 그러면 "검증은 통과하는데 화면이 틀린" 상태가 되고,
    /// 그건 검증이 없는 것보다 나쁘다. 그래서 <see cref="ApprovedArtImporter"/> 가 적용하고
    /// <see cref="PostImportValidator"/> 가 되읽는 값을 같은 구조체 하나에서 가져온다.
    ///
    /// 색공간이 핵심이다. Normal·Emission·Mask·AO 는 <b>데이터</b>라서 sRGB 를 켜면
    /// Unity 가 감마 변환을 걸어 값이 달라진다. 노멀은 방향이 틀어지고 AO 는 농도가 틀어진다.
    /// </summary>
    public struct ImportExpectation
    {
        public TextureImporterType TextureType;
        public bool SRgb;
        public bool AlphaIsTransparency;
        public bool Mipmaps;
        public TextureWrapMode Wrap;
        public FilterMode Filter;
        public TextureImporterCompression Compression;
        public int PixelsPerUnit;

        /// <summary>
        /// 스프라이트 메시 종류. 인계서 §2 — "스프라이트 메시: Full Rect.
        /// 알파 트리밍으로 피벗·채널 정렬을 바꾸지 않는다".
        ///
        /// Unity 기본값은 <c>Tight</c> 라서 알파를 따라 메시를 깎는다. 그러면 채널 맵과
        /// 알베도의 UV 범위가 어긋날 수 있고, 아틀라스 조각의 여백 계산도 달라진다.
        /// </summary>
        public SpriteMeshType MeshType;

        /// <summary>스프라이트로 임포트되는 채널인가. Albedo 만 참이다.</summary>
        public bool IsSprite => TextureType == TextureImporterType.Sprite;

        /// <summary>
        /// 채널 이름 → 기대 설정.
        ///
        /// 알 수 없는 채널은 데이터 맵으로 취급한다 — 새 채널이 생겼을 때 sRGB 가
        /// 켜진 채로 조용히 들어오는 것이 가장 위험하다.
        /// </summary>
        public static ImportExpectation For(string channel)
        {
            var e = new ImportExpectation
            {
                Mipmaps = false,                 // 셀 정렬 아트다 — 밉맵은 셀 경계를 흐린다
                Wrap = TextureWrapMode.Clamp,    // 아틀라스 이웃을 물지 않게
                Filter = FilterMode.Bilinear,
                Compression = TextureImporterCompression.Uncompressed,
                PixelsPerUnit = ApprovedArtContract.DeliveryPixelsPerCell,
                MeshType = SpriteMeshType.FullRect,
            };

            switch (channel)
            {
                case "albedo":
                    e.TextureType = TextureImporterType.Sprite;
                    e.SRgb = true;               // 색이다 — 감마 공간이 맞다
                    e.AlphaIsTransparency = true;
                    return e;

                case "normal":
                    // NormalMap 타입이면 Unity 가 선형으로 다룬다.
                    e.TextureType = TextureImporterType.NormalMap;
                    e.SRgb = false;
                    e.AlphaIsTransparency = false;
                    return e;

                default:
                    // Emission · Mask · AO · 그 밖의 데이터 맵
                    e.TextureType = TextureImporterType.Default;
                    e.SRgb = false;
                    e.AlphaIsTransparency = false;
                    return e;
            }
        }

        /// <summary>이 기대치를 임포터에 적용한다. 스프라이트 피벗은 호출자가 따로 넣는다.</summary>
        public void ApplyTo(TextureImporter ti)
        {
            if (ti == null) return;
            ti.textureType = TextureType;
            if (IsSprite) ti.spriteImportMode = SpriteImportMode.Single;
            ti.sRGBTexture = SRgb;
            ti.alphaIsTransparency = AlphaIsTransparency;
            ti.mipmapEnabled = Mipmaps;
            ti.wrapMode = Wrap;
            ti.filterMode = Filter;
            ti.textureCompression = Compression;
            ti.spritePixelsPerUnit = PixelsPerUnit;
            if (TextureType == TextureImporterType.NormalMap)
                ti.convertToNormalmap = false;   // 이미 탄젠트 공간 노멀이다

            if (IsSprite)
            {
                // 메시 종류는 TextureImporter 프로퍼티가 아니라 설정 구조체에만 있다.
                var settings = new TextureImporterSettings();
                ti.ReadTextureSettings(settings);
                settings.spriteMeshType = MeshType;
                ti.SetTextureSettings(settings);
            }
        }

        /// <summary>
        /// 임포터의 <b>현재</b> 상태가 기대치와 같은가. 다르면 사람이 읽을 수 있는
        /// 차이 목록을 돌려준다(§12.2 2단계).
        /// </summary>
        public string DescribeMismatch(TextureImporter ti)
        {
            if (ti == null) return "TextureImporter 를 얻지 못했다";

            var sb = new System.Text.StringBuilder();
            void Diff(string what, object expected, object actual)
            {
                if (Equals(expected, actual)) return;
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append($"{what} {actual}(기대 {expected})");
            }

            Diff("textureType", TextureType, ti.textureType);
            Diff("sRGB", SRgb, ti.sRGBTexture);
            Diff("alphaIsTransparency", AlphaIsTransparency, ti.alphaIsTransparency);
            Diff("mipmap", Mipmaps, ti.mipmapEnabled);
            Diff("wrap", Wrap, ti.wrapMode);
            Diff("filter", Filter, ti.filterMode);
            Diff("compression", Compression, ti.textureCompression);
            if (IsSprite)
            {
                Diff("spriteMode", SpriteImportMode.Single, ti.spriteImportMode);
                Diff("PPU", (float)PixelsPerUnit, ti.spritePixelsPerUnit);
            }
            if (TextureType == TextureImporterType.NormalMap)
                Diff("convertToNormalmap", false, ti.convertToNormalmap);

            if (IsSprite)
            {
                var settings = new TextureImporterSettings();
                ti.ReadTextureSettings(settings);
                Diff("spriteMeshType", MeshType, settings.spriteMeshType);
            }

            return sb.Length == 0 ? null : sb.ToString();
        }
    }
}
