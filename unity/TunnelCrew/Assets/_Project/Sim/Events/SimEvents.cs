namespace TunnelCrew.Sim
{
    /// <summary>
    /// Sim 이 발행하고 Presentation 이 구독하는 이벤트.
    ///
    /// 원본 <c>damage()</c> 는 파괴 규칙·파티클·사운드·XP·장악도·유물 훅을 한 함수에
    /// 담고 있었다(analysis-01 §3.7). Sim 은 "무슨 일이 일어났는가"만 알리고,
    /// 파티클과 소리는 Presentation 이, XP·장악도는 Sim 내부 구독자가 맡는다.
    /// </summary>
    public struct TileBrokenEvent
    {
        public int Cell, Col, Row;
        public TileType Type;
        public Vec2 HitDir;
        public bool HadBuriedRelic;
        public bool OpenedExit;
    }

    public struct TileDamagedEvent
    {
        public int Cell, Col, Row;
        public TileType Type;
        /// <summary>0 = 멀쩡함, 1 = 파괴 직전.</summary>
        public double Progress;
        public Vec2 HitDir;
    }

    public struct ResourceCollectedEvent
    {
        public ResourceKind Kind;
        public int Amount;
        public Vec2 Position;
    }

    public struct PlayerDashedEvent
    {
        public Vec2 Position;
        public Vec2 Direction;
    }

    /// <summary>암반을 긁어 튕겨 나왔을 때.</summary>
    public struct DrillBouncedEvent
    {
        public Vec2 Position;
        public Vec2 Normal;
    }
}
