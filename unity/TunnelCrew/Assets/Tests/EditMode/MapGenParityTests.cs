using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TunnelCrew.Sim;
using UnityEngine;

namespace TunnelCrew.Tests
{
    /// <summary>
    /// C# <see cref="DungeonGenerator"/> 가 원본 <c>genTunnel()</c> 과 한 칸도 다르지 않은지 확인한다.
    /// 픽스처는 <c>tools/unity-export/dump-mapgen-fixture.mjs</c> 가 원본을 격리 실행해 만든 것이다.
    ///
    /// 이 테스트가 M1 의 게이트다. 실패하면 어느 단계에서 갈라졌는지부터 찾을 것.
    /// </summary>
    public class MapGenParityTests
    {
        const string FixtureDir = "Tests/EditMode/Fixtures";

        [Serializable] class CellRC { public int c; public int r; }
        [Serializable] class CacheEntry { public int c; public int r; public string kind; public int val; }
        [Serializable] class LampEntry { public int c; public int r; }
        [Serializable] class PropEntry { public string kind; public int c; public int r; }
        [Serializable] class MetaEntry { public int rooms; public int exitD; public int maxD; public int interior; }

        [Serializable]
        class Fixture
        {
            public int depth;
            public string seed;
            public int cols, rows, cell;
            public CellRC entryCell;
            public int exit;
            public bool exitOpen;
            public string[] cells;
            public int[] band;
            public int[] dec;
            public int[] relic;
            public CacheEntry[] cache;
            public LampEntry[] lamps;
            public PropEntry[] props;
            public MetaEntry tunMeta;
        }

        static Fixture Load(int depth)
        {
            string path = Path.Combine(Application.dataPath, FixtureDir, $"map-tunnel-891730050-d{depth}.json");
            Assert.IsTrue(File.Exists(path), $"픽스처가 없다: {path}\n" +
                "먼저 `node tools/unity-export/dump-mapgen-fixture.mjs` 를 돌릴 것.");
            return JsonUtility.FromJson<Fixture>(File.ReadAllText(path));
        }

        static DungeonResult Generate(int depth)
            => new DungeonGenerator(DungeonConfig.Runtime).Generate(depth);

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Tiles_MatchOriginalExactly(int depth)
        {
            var fx = Load(depth);
            var got = Generate(depth);

            Assert.AreEqual(fx.cols, got.Cols, "열 수");
            Assert.AreEqual(fx.rows, got.Rows, "행 수");
            Assert.AreEqual(fx.cells.Length, got.Tiles.Length, "셀 개수");

            // 첫 불일치를 찾아 좌표까지 보고한다. 5,760칸을 하나씩 Assert 하면 느리다.
            int mismatches = 0;
            int firstIdx = -1;
            for (int i = 0; i < fx.cells.Length; i++)
            {
                string expected = fx.cells[i] ?? "";
                string actual = TileTypes.ToOriginalName(got.Tiles[i]);
                if (expected != actual)
                {
                    if (firstIdx < 0) firstIdx = i;
                    mismatches++;
                }
            }

            if (mismatches > 0)
            {
                int c = firstIdx % got.Cols, r = firstIdx / got.Cols;
                Assert.Fail($"타일 {mismatches}칸 불일치 (전체 {fx.cells.Length}). " +
                            $"첫 불일치 idx={firstIdx} (col {c}, row {r}): " +
                            $"원본 '{fx.cells[firstIdx]}' vs 이식 '{TileTypes.ToOriginalName(got.Tiles[firstIdx])}'");
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void EntryExitAndMeta_MatchOriginal(int depth)
        {
            var fx = Load(depth);
            var got = Generate(depth);

            Assert.AreEqual(fx.entryCell.c, got.EntryCol, "진입점 열");
            Assert.AreEqual(fx.entryCell.r, got.EntryRow, "진입점 행");
            Assert.AreEqual(fx.exit, got.Exit, "출구 셀 인덱스");
            Assert.AreEqual(fx.exitOpen, got.ExitOpen, "출구 개방 여부");
            Assert.AreEqual(fx.tunMeta.rooms, got.RoomCount, "방 개수");
            Assert.AreEqual(fx.tunMeta.maxD, got.MaxDist, "최대 거리");
            Assert.AreEqual(fx.tunMeta.interior, got.Interior, "내부 셀 수");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void BandAndDecal_MatchOriginal(int depth)
        {
            var fx = Load(depth);
            var got = Generate(depth);

            for (int i = 0; i < fx.band.Length; i++)
                if (fx.band[i] != got.Band[i])
                    Assert.Fail($"밴드 불일치 idx={i}: 원본 {fx.band[i]} vs 이식 {got.Band[i]}");

            for (int i = 0; i < fx.dec.Length; i++)
                if (fx.dec[i] != got.Dec[i])
                    Assert.Fail($"데칼 불일치 idx={i}: 원본 {fx.dec[i]} vs 이식 {got.Dec[i]}");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Poi_MatchOriginal(int depth)
        {
            var fx = Load(depth);
            var got = Generate(depth);

            CollectionAssert.AreEqual(fx.relic.OrderBy(v => v).ToList(),
                                      got.Relics.OrderBy(v => v).ToList(), "묻힌 유물 셀");

            Assert.AreEqual(fx.cache.Length, got.Caches.Count, "보급품 개수");
            for (int i = 0; i < fx.cache.Length; i++)
            {
                Assert.AreEqual(fx.cache[i].c, got.Caches[i].Col, $"보급품[{i}] 열");
                Assert.AreEqual(fx.cache[i].r, got.Caches[i].Row, $"보급품[{i}] 행");
                Assert.AreEqual(fx.cache[i].kind, got.Caches[i].IsShard ? "shard" : "pouch", $"보급품[{i}] 종류");
                Assert.AreEqual(fx.cache[i].val, got.Caches[i].Value, $"보급품[{i}] 값");
            }

            Assert.AreEqual(fx.lamps.Length, got.Lamps.Count, "랜턴 개수");
            for (int i = 0; i < fx.lamps.Length; i++)
            {
                Assert.AreEqual(fx.lamps[i].c, got.Lamps[i].col, $"랜턴[{i}] 열");
                Assert.AreEqual(fx.lamps[i].r, got.Lamps[i].row, $"랜턴[{i}] 행");
            }

            Assert.AreEqual(fx.props.Length, got.Props.Count, "소품 개수");
            for (int i = 0; i < fx.props.Length; i++)
            {
                Assert.AreEqual(fx.props[i].kind, got.Props[i].Kind, $"소품[{i}] 종류");
                Assert.AreEqual(fx.props[i].c, got.Props[i].Col, $"소품[{i}] 열");
                Assert.AreEqual(fx.props[i].r, got.Props[i].Row, $"소품[{i}] 행");
            }
        }

        [Test]
        public void Generation_IsDeterministic()
        {
            var a = Generate(1);
            var b = Generate(1);
            CollectionAssert.AreEqual(a.Tiles, b.Tiles, "같은 시드는 같은 맵이어야 한다");
            Assert.AreEqual(a.Exit, b.Exit);
        }

        [Test]
        public void DifferentDepths_ProduceDifferentMaps()
        {
            var d1 = Generate(1);
            var d2 = Generate(2);
            CollectionAssert.AreNotEqual(d1.Tiles, d2.Tiles, "심층이 다르면 맵이 달라야 한다");
        }

        [Test]
        public void UnityVisual_AddsWideEntryCompositionWithoutChangingRuntimeContract()
        {
            var parity = DungeonConfig.Runtime;
            var visual = DungeonConfig.UnityVisual;
            Assert.Zero(parity.PresentationEntryHalfWidth);
            Assert.Zero(parity.PresentationEntryHalfHeight);
            Assert.IsFalse(parity.PresentationEntryLamps);

            var got = new DungeonGenerator(visual).Generate(1);
            Assert.AreEqual(3, got.PresentationLamps.Count);
            foreach (var lamp in got.PresentationLamps)
            {
                Assert.AreEqual(TileType.Empty, got.Tiles[lamp.row * got.Cols + lamp.col]);
                CollectionAssert.Contains(got.Lamps, lamp);
            }

            int horizontalOpen = 0;
            for (int dc = -visual.PresentationEntryHalfWidth + 1;
                 dc <= visual.PresentationEntryHalfWidth - 1; dc++)
                if (got.Tiles[got.EntryRow * got.Cols + got.EntryCol + dc] == TileType.Empty)
                    horizontalOpen++;
            Assert.GreaterOrEqual(horizontalOpen, 23, "진입부는 카메라 데드존 뒤에도 16:9 첫 화면을 채워야 한다");

            int upperOpen = 0;
            for (int dc = -visual.PresentationEntryHalfWidth + 1;
                 dc <= visual.PresentationEntryHalfWidth - 1; dc++)
                if (got.Tiles[(got.EntryRow + visual.PresentationEntryHalfHeight) * got.Cols + got.EntryCol + dc]
                    == TileType.Empty) upperOpen++;
            Assert.GreaterOrEqual(upperOpen, 11,
                "둥근 직사각 무대의 위쪽도 길게 열려 수평 설비 벽이 끊기지 않아야 한다");

            int interiorCore = 0;
            for (int dr = -visual.PresentationEntryHalfHeight; dr <= visual.PresentationEntryHalfHeight; dr++)
                for (int dc = -visual.PresentationEntryHalfWidth; dc <= visual.PresentationEntryHalfWidth; dc++)
                {
                    double nx = System.Math.Abs(dc / (visual.PresentationEntryHalfWidth + .15));
                    double ny = System.Math.Abs(dr / (visual.PresentationEntryHalfHeight + .15));
                    if (System.Math.Pow(nx, 4) + System.Math.Pow(ny, 4) > 1.0) continue;
                    if (got.Tiles[(got.EntryRow + dr) * got.Cols + got.EntryCol + dc] == TileType.Core)
                        interiorCore++;
                }
            Assert.Zero(interiorCore, "Unity 전용 작업실 내부에는 독립된 검은 Core 기둥이 남지 않아야 한다");
        }

        [Test]
        public void UnityVisual_PersistsAcrossDescend()
        {
            var sim = new TunnelSim();
            sim.StartRun(RoleId.Driller);
            sim.EnterDepth(1, DungeonConfig.UnityVisual);
            sim.Descend();

            Assert.AreEqual(2, sim.Depth);
            Assert.That(sim.Generation.PresentationLamps, Is.Not.Null);
            Assert.AreEqual(3, sim.Generation.PresentationLamps.Count);
            sim.RefreshVision();
            foreach (var lamp in sim.Generation.PresentationLamps)
            {
                if (!sim.Generation.Lamps.Contains(lamp)) continue;
                Assert.AreEqual(1, sim.Los.Visible[lamp.row * sim.World.Cols + lamp.col],
                    "프레젠테이션 램프는 탐색 기록 없이 자기 위치를 밝혀야 한다");
            }
        }
    }
}
