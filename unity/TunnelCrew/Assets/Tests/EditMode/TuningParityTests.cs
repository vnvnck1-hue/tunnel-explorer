using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TunnelCrew.Sim;
using UnityEngine;

namespace TunnelCrew.Tests
{
    /// <summary>
    /// 계획 §5 M0 의 "SO 데이터가 원본 값과 일치(스팟체크 20개)" 를 자동화한 것.
    /// <see cref="SimTuning"/> 의 상수가 원본에서 뽑은 raw JSON 과 어긋나면 실패한다.
    ///
    /// 원본은 픽셀, 우리는 셀 단위이므로 변환식을 테스트에 명시해 둔다.
    /// 변환식이 틀리면 여기서 잡힌다.
    /// </summary>
    public class TuningParityTests
    {
        const string RawDir = "_Project/Data/raw";

        /// <summary>
        /// raw JSON 은 한 겹짜리 숫자·불리언 맵이라 정규식으로 충분하다.
        /// (JsonUtility 는 미리 정의한 클래스가 필요해 190필드에는 맞지 않는다)
        /// </summary>
        static Dictionary<string, double> LoadNumbers(string file)
        {
            string path = Path.Combine(Application.dataPath, RawDir, file);
            Assert.IsTrue(File.Exists(path), $"raw JSON 이 없다: {path}\n" +
                "먼저 `node tools/unity-export/dump-tuning.mjs` 를 돌릴 것.");

            var map = new Dictionary<string, double>();
            foreach (Match m in Regex.Matches(File.ReadAllText(path),
                     @"""(?<k>[A-Za-z0-9_]+)""\s*:\s*(?<v>-?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?|true|false)"))
            {
                string v = m.Groups["v"].Value;
                double d = v == "true" ? 1 : v == "false" ? 0
                         : double.Parse(v, CultureInfo.InvariantCulture);
                map[m.Groups["k"].Value] = d;
            }
            return map;
        }

        static Dictionary<string, double> _demo, _te;
        static Dictionary<string, double> Demo => _demo ??= LoadNumbers("DEMO.json");
        static Dictionary<string, double> Te => _te ??= LoadNumbers("TE.json");

        const double Eps = 1e-9;

        static void SameCells(double originalPx, double ourCells, string what)
            => Assert.AreEqual(originalPx / SimTuning.PxPerCell, ourCells, Eps, $"{what} (픽셀→셀 변환)");

        static void SameTeCells(double originalTe, double ourCells, string what)
            => Assert.AreEqual(originalTe / SimTuning.TeCellRef, ourCells, Eps, $"{what} (teWorld→셀 변환)");

        [Test]
        public void Player_MatchesOriginal()
        {
            Assert.AreEqual(Demo["playerHp"], SimTuning.PlayerHp, Eps, "플레이어 체력");
            Assert.AreEqual(Demo["playerIFrame"], SimTuning.PlayerIFrame, Eps, "무적 시간");
            SameCells(25.0, SimTuning.PlayerRadius, "R_SHELLY");

            // teMovePx() = teWorld(moveSpeed) * (1.15 / max(0.85, sqrt(zoom)))
            double expected = (Te["moveSpeed"] / SimTuning.TeCellRef)
                            * (1.15 / Math.Max(0.85, Math.Sqrt(Te["zoom"])));
            Assert.AreEqual(expected, SimTuning.MoveSpeed, Eps, "이동 속도");
        }

        [Test]
        public void Dash_MatchesOriginal()
        {
            SameTeCells(Te["dashDist"], SimTuning.DashDistance, "대시 거리");
            Assert.AreEqual(Te["dashDur"], SimTuning.DashDuration, Eps, "대시 지속");
            Assert.AreEqual(Te["dashCd"], SimTuning.DashCooldown, Eps, "대시 쿨다운");
            SameCells(4.0, SimTuning.DashSliceLength, "대시 서브스텝");
        }

        [Test]
        public void Camera_MatchesOriginal()
        {
            SameTeCells(Te["deadzone"], SimTuning.DeadZone, "데드존");
            SameTeCells(Te["lookAhead"], SimTuning.LookAhead, "룩어헤드");
            Assert.AreEqual(Te["followSpeed"], SimTuning.FollowSpeed, Eps, "추종 속도");
            Assert.AreEqual(Te["aimFollow"], SimTuning.AimFollow, Eps, "조준 보간");
            // teZoomZ() = zoom * (TE_CELL_REF / CELL)
            Assert.AreEqual(Te["zoom"] * (SimTuning.TeCellRef / SimTuning.PxPerCell),
                            SimTuning.BaseZoom, Eps, "기본 줌");
            SameTeCells(110.0, SimTuning.CameraPadMax, "카메라 여유");
        }

        [Test]
        public void Drill_MatchesOriginal()
        {
            Assert.AreEqual(Demo["drillDmg"], SimTuning.DrillDamageMul, Eps, "드릴 피해 배율");
            Assert.AreEqual(Demo["drillHitInt"], SimTuning.DrillHitInterval, Eps, "타격 간격");
            Assert.AreEqual(Demo["drillWarmTime"], SimTuning.DrillWarmTime, Eps, "예열 시간");
            Assert.AreEqual(Demo["drillWarmMin"], SimTuning.DrillWarmMin, Eps, "예열 최소");
            Assert.AreEqual(Demo["drillWarmCurve"], SimTuning.DrillWarmCurve, Eps, "예열 곡선");
            Assert.AreEqual(Demo["drillWarmDecay"], SimTuning.DrillWarmDecay, Eps, "예열 감쇠");
            Assert.AreEqual(Demo["drillHeatBuild"], SimTuning.DrillHeatBuild, Eps, "발열");
            Assert.AreEqual(Demo["drillHeatCool"], SimTuning.DrillHeatCool, Eps, "냉각");
            Assert.AreEqual(Demo["drillHeatLock"], SimTuning.DrillHeatLock, Eps, "과열 잠금");
            SameCells(Demo["drillRockBounce"], SimTuning.DrillRockBounceSpeed, "암반 반동 속도");
            Assert.AreEqual(Demo["drillRockBounceDur"], SimTuning.DrillRockBounceDuration, Eps, "반동 지속");
            Assert.AreEqual(Demo["drillRockBounceCd"], SimTuning.DrillRockBounceCooldown, Eps, "반동 쿨");
            SameCells(25.0 * 2.55, SimTuning.DrillTip, "DRILL_TIP");
        }

        [Test]
        public void Loot_MatchesOriginal()
        {
            Assert.AreEqual((int)Demo["lootCount"], SimTuning.LootCount, "전리품 개수");
            Assert.AreEqual((int)Demo["lootGemBonus"], SimTuning.LootGemBonus, "보석 보너스");
            SameCells(Demo["lootScatter"], SimTuning.LootScatter, "산포 속도");
            SameCells(Demo["lootScatterRand"], SimTuning.LootScatterRand, "산포 편차");
            Assert.AreEqual(Demo["lootSpread"], SimTuning.LootSpread, Eps, "산포 각");
            SameCells(Demo["lootPopZ"], SimTuning.LootPopZ, "튀는 높이");
            SameCells(Demo["lootPopVz"], SimTuning.LootPopVz, "튀는 속도");
            SameCells(Demo["lootGravity"], SimTuning.LootGravity, "중력");
            SameCells(Demo["lootPickup"], SimTuning.LootPickup, "획득 반경");
            SameCells(Demo["lootMagnet"], SimTuning.LootMagnet, "자석 반경");
            Assert.AreEqual(Demo["lootMagnetDelay"], SimTuning.LootMagnetDelay, Eps, "자석 지연");
            Assert.AreEqual(Demo["lootMagnetSpd"], SimTuning.LootMagnetSpeed, Eps, "자석 속도");
        }

        [Test]
        public void DungeonConfig_MatchesAppliedRuntimeValues()
        {
            var cfg = DungeonConfig.Runtime;
            Assert.AreEqual((int)Demo["mapW"], cfg.Cols, "맵 너비");
            Assert.AreEqual((int)Demo["mapH"], cfg.Rows, "맵 높이");
            Assert.AreEqual((int)Demo["edge"], cfg.EdgeThickness, "외곽 두께");
            Assert.AreEqual((int)Demo["softPct"], cfg.SoftPercent, "흙 비율");
            Assert.AreEqual((int)Demo["medPct"], cfg.MedPercent, "돌 비율");
            Assert.AreEqual((int)Demo["lampCount"], cfg.LampCount, "랜턴 수");
            Assert.AreEqual((int)Demo["birth"], cfg.CaBirth, "CA birth");
            Assert.AreEqual((int)Demo["survive"], cfg.CaSurvive, "CA survive");

            // applyDemoToDungen() 이 덮어쓰는 값들
            Assert.AreEqual((int)Demo["rmin"], cfg.RoomMin, "방 최소");
            Assert.AreEqual((int)Math.Max(Demo["rmin"], Demo["rmax"]), cfg.RoomMax, "방 최대");
            Assert.AreEqual((int)Math.Min(100, Demo["blob"] + Math.Max(0, Demo["fill"] - 47)),
                            cfg.BlobPercent, "blob 확률");
            Assert.AreEqual((int)Demo["cw"], cfg.CorridorWidth, "통로 폭");
            Assert.AreEqual((int)Demo["jit"], cfg.JitterPercent, "지터");
            Assert.AreEqual((int)Demo["loop"], cfg.LoopPercent, "우회 간선");
            Assert.AreEqual((int)Demo["ca"], cfg.CaPasses, "CA 패스 수");
            Assert.AreEqual((int)Demo["core"], cfg.CorePercent, "암반 비율");
            Assert.AreEqual((int)Demo["ore"], cfg.OrePercent, "광맥 비율");
            Assert.AreEqual((int)Demo["cache"], cfg.Caches, "보급품 수");

            Assert.AreEqual(80, cfg.RoomTarget, "방 목표 수 tunRooms(80,72)");
            Assert.AreEqual(18, cfg.BandRows, "밴드 행 수 ceil(72/4)");
        }

        [Test]
        public void TileHpAndYield_MatchOriginal()
        {
            var hpt = LoadNumbers("HPT.json");
            Assert.AreEqual(hpt["dirt"], TileTypes.BaseHp(TileType.Dirt), Eps);
            Assert.AreEqual(hpt["stone"], TileTypes.BaseHp(TileType.Stone), Eps);
            Assert.AreEqual(hpt["ore"], TileTypes.BaseHp(TileType.Ore), Eps);
            Assert.AreEqual(hpt["gem"], TileTypes.BaseHp(TileType.Gem), Eps);
            Assert.AreEqual(hpt["crys"], TileTypes.BaseHp(TileType.Crys), Eps);

            Assert.AreEqual((ResourceKind.Pulp, 1), TileTypes.Yield(TileType.Dirt));
            Assert.AreEqual((ResourceKind.Pulp, 2), TileTypes.Yield(TileType.Stone));
            Assert.AreEqual((ResourceKind.Bloom, 1), TileTypes.Yield(TileType.Ore));
            Assert.AreEqual((ResourceKind.Bloom, 3), TileTypes.Yield(TileType.Gem));
            Assert.AreEqual((ResourceKind.Bloom, 2), TileTypes.Yield(TileType.Crys));
        }

        [Test]
        public void DrillWarmCurve_MatchesOriginalFormula()
        {
            var p = new PlayerState();
            // t=0 이면 최소 배율, t=1 이면 1.0
            p.DrillWarm = 0;
            Assert.AreEqual(SimTuning.DrillWarmMin, MiningSystem.WarmMul(p), Eps, "예열 0");
            p.DrillWarm = 1;
            Assert.AreEqual(1.0, MiningSystem.WarmMul(p), Eps, "예열 만렙");

            // 중간값은 원본 공식 그대로
            p.DrillWarm = 0.5;
            double expected = SimTuning.DrillWarmMin
                + (1 - SimTuning.DrillWarmMin) * Math.Pow(0.5, SimTuning.DrillWarmCurve);
            Assert.AreEqual(expected, MiningSystem.WarmMul(p), Eps, "예열 중간");

            // 타격 간격 = 0.06 / max(0.08, warm)
            p.DrillWarm = 1;
            Assert.AreEqual(SimTuning.DrillHitInterval / 1.0, MiningSystem.HitInterval(p), Eps);
        }
    }
}
