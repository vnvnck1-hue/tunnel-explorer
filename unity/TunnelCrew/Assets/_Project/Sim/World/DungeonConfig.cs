namespace TunnelCrew.Sim
{
    /// <summary>
    /// 던전 생성 설정. 원본은 `DUNGEN` 리터럴을 `applyDemoToDungen()` 이 `DEMO` 값으로
    /// 덮어쓴 뒤 쓴다. <see cref="Runtime"/> 이 그 **덮어쓴 뒤의 실제 값**이다.
    ///
    /// 주의: `docs/unity-port/analysis-01-core-sim.md` §3.5 의 표는 덮어쓰기 **전** 리터럴이라
    /// 실제 런타임 값과 다르다. 특히 `ca` 는 0 이 아니라 5 이고(세포 자동자가 실제로 돈다),
    /// `core` 는 14 가 아니라 25, `ore` 는 8 이 아니라 2, `cache` 는 13 이 아니라 6 이다.
    /// </summary>
    public struct DungeonConfig
    {
        // 맵 크기 — 땅굴 모드는 심층과 무관하게 항상 고정 (원본 setLayerSize)
        public int Cols;
        public int Rows;

        public string Seed;

        // 방
        public int RoomMin;      // DUNGEN.rmin
        public int RoomMax;      // DUNGEN.rmax
        public int BlobPercent;  // DUNGEN.blob — 타원+랜덤워크 방이 될 확률(%)

        // 통로
        public int CorridorWidth;   // DUNGEN.cw
        public int JitterPercent;   // DUNGEN.jit — 통로가 옆길로 새는 확률(%)
        public int LoopPercent;     // DUNGEN.loop — MST 위에 얹는 우회 간선 비율(%)

        // 세포 자동자
        public int CaPasses;   // DUNGEN.ca
        public int CaBirth;    // DEMO.birth
        public int CaSurvive;  // DEMO.survive

        // 배치 비율 (내부 셀 대비 %)
        public int CorePercent;  // DUNGEN.core
        public int OrePercent;   // DUNGEN.ore

        // POI 개수
        public int BuriedRelics;  // DUNGEN.bur
        public int Caches;        // DUNGEN.cache
        public int LampCount;     // DEMO.lampCount

        // 타일 확정
        public int EdgeThickness;  // DEMO.edge — 외곽 rock 두께
        public int SoftPercent;    // DEMO.softPct — dirt 로 남을 비율
        public int MedPercent;     // DEMO.medPct — 나머지 중 stone 이 될 비율

        public bool EnsurePath;   // DEMO.ensurePath — 진입점 주변 강제 개방
        public int BandCount;     // P.bands.length — 밴드 구간 수

        /// <summary>
        /// v7.9.2 의 실제 런타임 값. `applyDemoToDungen()` 적용 후 기준이며
        /// `tools/unity-export/dump-tuning.mjs` 출력과 대조해 굳혔다.
        /// </summary>
        public static DungeonConfig Runtime => new DungeonConfig
        {
            Cols = 80,
            Rows = 72,
            Seed = "tunnel-891730050",

            RoomMin = 2,
            RoomMax = 5,
            BlobPercent = 100,   // min(100, DEMO.blob 97 + max(0, DEMO.fill 55 - 47))

            CorridorWidth = 1,
            JitterPercent = 47,
            LoopPercent = 30,

            CaPasses = 5,
            CaBirth = 4,
            CaSurvive = 4,

            CorePercent = 25,
            OrePercent = 2,

            BuriedRelics = 5,
            Caches = 6,
            LampCount = 6,

            EdgeThickness = 2,
            SoftPercent = 55,
            MedPercent = 22,

            EnsurePath = true,
            BandCount = 4,
        };

        /// <summary>원본 `tunRooms(c,r)` — 내부 셀 68칸당 방 1개.</summary>
        public int RoomTarget => System.Math.Max(5, JsMath.Round((Cols - 2) * (Rows - 2) / 68.0));

        /// <summary>원본 `BANDROWS = ceil(ROWS / P.bands.length)`.</summary>
        public int BandRows => (int)System.Math.Ceiling(Rows / (double)BandCount);
    }
}
