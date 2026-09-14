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

        // Unity 본편 비주얼 스테이징. 0이면 원본 패리티 경로를 그대로 쓴다.
        // 모든 난수 소비가 끝난 뒤 진입점 주변만 넓혀, 생성 픽스처와 랜덤 스트림을 건드리지 않는다.
        public int PresentationEntryHalfWidth;
        public int PresentationEntryHalfHeight;
        public bool PresentationEntryLamps;

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

            PresentationEntryHalfWidth = 0,
            PresentationEntryHalfHeight = 0,
            PresentationEntryLamps = false,
        };

        /// <summary>
        /// Unity 본편용 화면 구성. <see cref="Runtime"/>의 생성 규칙과 난수열은 유지하면서
        /// 진입부만 16:9에 가까운 넓은 무대로 후처리한다.
        /// </summary>
        public static DungeonConfig UnityVisual
        {
            get
            {
                var cfg = Runtime;
                // 기본 카메라는 약 20.2×11.4셀을 본다. 카메라 데드존 때문에 진입점이
                // 화면 왼쪽에 놓여도 우측 끝까지 바닥이 이어지도록 가로를 27셀로 잡는다.
                // 세로는 9셀을 유지해 위쪽 둘레 벽과 연결 설비가 프레임 안에 남게 한다.
                cfg.PresentationEntryHalfWidth = 13;
                cfg.PresentationEntryHalfHeight = 4;
                cfg.PresentationEntryLamps = true;
                return cfg;
            }
        }

        /// <summary>원본 `tunRooms(c,r)` — 내부 셀 68칸당 방 1개.</summary>
        public int RoomTarget => System.Math.Max(5, JsMath.Round((Cols - 2) * (Rows - 2) / 68.0));

        /// <summary>원본 `BANDROWS = ceil(ROWS / P.bands.length)`.</summary>
        public int BandRows => (int)System.Math.Ceiling(Rows / (double)BandCount);
    }
}
