using TunnelCrew.Sim;
using UnityEngine;

namespace TunnelCrew.Data
{
    /// <summary>
    /// 타일 스프라이트 조회표. 원본 <c>tileAtlasIndex(type, damage, band, surface)</c> 와
    /// 같은 슬롯 번호로 색인한다(0~267).
    ///
    /// 원본은 셀 크기마다 스프라이트를 다시 굽는 <c>_BS</c> 캐시를 썼다. Unity 는 Tilemap 이
    /// 배칭·컬링을 하므로 그 캐시는 옮기지 않는다(analysis-04 §2.5).
    /// </summary>
    [CreateAssetMenu(menuName = "Tunnel Crew/Tile Set", fileName = "TileSet")]
    public sealed class TileSetAsset : ScriptableObject
    {
        [Tooltip("원본 tileAtlasIndex() 슬롯 번호로 색인한다. 268칸.")]
        public Sprite[] slots = new Sprite[268];

        [Tooltip("바닥 변형 3종. 원본은 (col*17 + row*31) & 7 해시로 고른다.")]
        public Sprite[] floorVariants = new Sprite[3];

        [Header("암반 오버레이")]
        public Sprite coreBottomShadow;
        public Sprite[] coreSideByBand = new Sprite[4];

        [Header("밴드 경계 시임")]
        public Sprite[] seams = new Sprite[3];

        [Header("벽 아틀라스 노멀맵")]
        [Tooltip("slots 의 스프라이트가 전부 한 아틀라스(purple_walls_atlas)에서 잘린 경우, 같은 배치의 노멀 아틀라스. 런타임이 벽 타일맵 머티리얼의 _NormalMap 에 넣는다 (세컨더리 텍스처 바인딩은 URP 17 타일맵/스프라이트에서 조명에 반영되지 않았음).")]
        public Texture2D wallNormalAtlas;

        /// <summary>원본 tileAtlasIndex() 와 동일한 슬롯 계산.</summary>
        public static int SlotOf(TileType type, int damage, int band, int surface)
        {
            band = Mathf.Clamp(band, 0, 3);
            if (type == TileType.Rock) return 240 + band;
            if (type == TileType.Core) return 244 + band * 6 + (surface % 6 + 6) % 6;

            int ti = type switch
            {
                TileType.Dirt => 0,
                TileType.Stone => 1,
                TileType.Ore => 2,
                TileType.Gem => 3,
                TileType.Crys => 4,
                _ => -1,
            };
            if (ti < 0) return -1;
            return ti * 48 + band * 12 + ((surface % 3 + 3) % 3) * 4 + Mathf.Clamp(damage, 0, 3);
        }

        public Sprite Get(TileType type, int damage, int band, int surface)
        {
            int slot = SlotOf(type, damage, band, surface);
            if (slot < 0 || slot >= slots.Length) return null;
            return slots[slot];
        }

        /// <summary>원본의 바닥 변형 해시: ((col*17 + row*31) &amp; 7) / 7 → 3종 중 하나.</summary>
        public Sprite FloorAt(int col, int row)
        {
            if (floorVariants == null || floorVariants.Length == 0) return null;
            double h = ((col * 17 + row * 31) & 7) / 7.0;
            // 원본 배분: floor_dark 25% / floor_base 25% / floor_rim 50%
            int i = h < 0.25 ? 0 : h < 0.5 ? 1 : 2;
            return floorVariants[Mathf.Clamp(i, 0, floorVariants.Length - 1)];
        }
    }
}
