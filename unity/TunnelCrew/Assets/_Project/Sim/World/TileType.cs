namespace TunnelCrew.Sim
{
    /// <summary>
    /// 원본 <c>G.cell[]</c> 의 문자열 타일 타입. <c>null</c> 은 <see cref="Empty"/> 다.
    /// 값은 저장 포맷이 아니므로 순서를 바꿔도 되지만, 픽스처 대조에 쓰는
    /// <see cref="TileTypes.ToOriginalName"/> 의 문자열은 원본과 같아야 한다.
    /// </summary>
    public enum TileType : byte
    {
        Empty = 0,
        Dirt,
        Stone,
        Ore,
        Gem,
        Crys,
        /// <summary>맵 외곽 경계. 파괴 불가.</summary>
        Rock,
        /// <summary>맵 내부의 파괴 불가 암반 덩어리. 드릴러의 균열 대상.</summary>
        Core,
    }

    public static class TileTypes
    {
        /// <summary>원본 `HPT` — 타일 기본 체력. rock/core 는 1e9 (사실상 무한).</summary>
        public static float BaseHp(TileType t) => t switch
        {
            TileType.Dirt => 80f,
            TileType.Stone => 140f,
            TileType.Ore => 100f,
            TileType.Gem => 100f,
            TileType.Crys => 100f,
            _ => 1e9f,
        };

        /// <summary>원본 `SOLIDX(t)` — 파괴 불가(기반암) 여부.</summary>
        public static bool IsBedrock(TileType t) => t == TileType.Rock || t == TileType.Core;

        /// <summary>빈칸이 아니면 통행 불가.</summary>
        public static bool IsSolid(TileType t) => t != TileType.Empty;

        /// <summary>원본 `YIELD` — 채굴 산출. 산출이 없으면 amount 0.</summary>
        public static (ResourceKind kind, int amount) Yield(TileType t) => t switch
        {
            TileType.Dirt => (ResourceKind.Pulp, 1),
            TileType.Stone => (ResourceKind.Pulp, 2),
            TileType.Ore => (ResourceKind.Bloom, 1),
            TileType.Gem => (ResourceKind.Bloom, 3),
            TileType.Crys => (ResourceKind.Bloom, 2),
            _ => (ResourceKind.Pulp, 0),
        };

        /// <summary>픽스처 JSON 과 대조하기 위한 원본 문자열. 빈칸은 "".</summary>
        public static string ToOriginalName(TileType t) => t switch
        {
            TileType.Empty => "",
            TileType.Dirt => "dirt",
            TileType.Stone => "stone",
            TileType.Ore => "ore",
            TileType.Gem => "gem",
            TileType.Crys => "crys",
            TileType.Rock => "rock",
            TileType.Core => "core",
            _ => "",
        };

        public static TileType FromOriginalName(string s) => s switch
        {
            "" or null => TileType.Empty,
            "dirt" => TileType.Dirt,
            "stone" => TileType.Stone,
            "ore" => TileType.Ore,
            "gem" => TileType.Gem,
            "crys" => TileType.Crys,
            "rock" => TileType.Rock,
            "core" => TileType.Core,
            _ => TileType.Empty,
        };
    }

    public enum ResourceKind : byte { Pulp = 0, Bloom }
}
