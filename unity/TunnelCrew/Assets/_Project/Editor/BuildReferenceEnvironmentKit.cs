using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TunnelCrew.Presentation.Visual;
using UnityEditor;
using UnityEngine;

namespace TunnelCrew.EditorTools
{
    /// <summary>
    /// 이주 3단계(docs/unity-port/reference-lab-migration-plan.md) — 레퍼런스 직결 아트로 환경 렌더러가
    /// 요구하는 자산을 만든다: <see cref="SurfaceMaterialSet"/> 3종(바닥·벽 윗면·벽 정면) +
    /// <see cref="EnvironmentKit"/> 1종.
    ///
    /// <b>바닥만 채우고 벽은 비워 둔다.</b> 레퍼런스 트랙에는 벽 cap/front 타일러블이 없다
    /// (`tr01_reference_wall_a` 는 384×384 세트피스 패널). 그 7장은
    /// <c>docs/codex-art-request-reference-wall-set.md</c> 로 요청했고, 도착하면 이 도구를 다시 돌려
    /// <c>wallTop</c>/<c>wallFront</c> 만 채운다 — 자산 파일은 이미 존재하므로 씬 참조가 끊기지 않는다.
    ///
    /// <b>채널은 전부 비워 둔다.</b> 레퍼런스 아트는 albedo 뿐이다. <see cref="SurfaceMaterialSet"/> 의
    /// normal/emission/mask/ao 는 nullable 이라 비어도 렌더러가 돌아간다(그 채널 없음 = 셰이더 기본값).
    /// 채널맵은 5단계 요청이고, 도착하면 인스펙터에서 꽂는다.
    ///
    /// <b>이미 있는 자산은 덮지 않는다.</b> 사용자가 인스펙터에서 조정한 값(normalStrength 등)을 보존한다.
    /// 단, 키트의 배열이 비어 있으면 채운다 — "아트가 도착해서 다시 돌린다" 가 이 도구의 정상 사용법이다.
    /// </summary>
    public static class BuildReferenceEnvironmentKit
    {
        const string ArtDir = "Assets/Art/Visual/ReferenceCalibrationV1";
        const string DataDir = "Assets/_Project/Data/Visual";
        const string KitPath = DataDir + "/EnvironmentKit_ReferenceV1.asset";
        const string FloorSetPath = DataDir + "/SurfaceMaterialSet_Reference_floor.asset";
        const string WallTopSetPath = DataDir + "/SurfaceMaterialSet_Reference_walltop.asset";
        const string WallFrontSetPath = DataDir + "/SurfaceMaterialSet_Reference_wallfront.asset";

        /// <summary>
        /// 바닥은 보드 승인 3장(a/b/c)만 쓴다 — 캘리브레이션 도구와 같은 제약이다.
        ///
        /// <b>d/e/f 를 넣지 않는 이유(2026-09-09 실측)</b> — 세 파일은 가장자리 4px 이 완전한 검정
        /// (휘도 0.0, 내부 74)이다. 캘리브레이션 보드 배경이 남은 미정규화 이력본이라
        /// (`reference-calibration-v1.md`: "기존 A~F 매크로 변형 검증은 이력으로만 보존, 현재 활성 바닥은 A/B/C")
        /// 타일맵에 깔면 셀마다 검은 테두리가 격자선으로 보인다. 처음 6장을 넣었을 때 실제로 그랬다.
        /// a/b/c 도 가장자리가 내부보다 어둡다(0.64~0.91) — 이건 아트의 몰타르 표현이라 그대로 둔다.
        /// </summary>
        static readonly string[] FloorFiles =
        {
            "tr01_reference_floor_a_albedo.png", "tr01_reference_floor_b_albedo.png",
            "tr01_reference_floor_c_albedo.png",
        };

        /// <summary>아트 요청서(§3·§4)의 파일명. 도착하면 자동으로 집는다. 변형 수는 유연하다(a~f).</summary>
        const string WallTopPattern = "tr01_reference_wall_top_{0}_albedo.png";
        const string WallTopRimPattern = "tr01_reference_wall_top_rim_{0}_albedo.png";
        const string WallFrontPattern = "tr01_reference_wall_front_{0}_albedo.png";
        const string WestSidePattern = "tr01_reference_wall_side_west_{0}_albedo.png";
        const string EastSidePattern = "tr01_reference_wall_side_east_{0}_albedo.png";
        const string OuterCornerPattern = "tr01_reference_wall_outer_corner_{0}_albedo.png";
        const string InnerCornerPattern = "tr01_reference_wall_inner_corner_{0}_albedo.png";
        const string FloorEdgePattern = "tr01_reference_floor_edge_{0}_albedo.png";
        static readonly string[] Variants = { "a", "b", "c", "d", "e", "f" };

        /// <summary>
        /// 상시 드롭섀도 타일셋(3차 요청 ②). 변형이 아니라 <b>역할</b>이고 순서가 계약이다 —
        /// <c>EnvironmentKit.ShadowCenter/Edge/CornerOuter/CornerInner</c>. 순수 알파 마스크.
        /// </summary>
        /// <summary>벽 정면 균열 3단계(3차 요청 ③). 순서 = 타격 단계. 투명 오버레이, 피벗 하단 중앙.</summary>
        static readonly string[] WallCrackByStage =
        {
            "tr01_reference_wall_crack_1_albedo.png",
            "tr01_reference_wall_crack_2_albedo.png",
            "tr01_reference_wall_crack_3_albedo.png",
        };

        static readonly string[] WallShadowByRole =
        {
            "tr01_reference_wall_shadow_center.png",
            "tr01_reference_wall_shadow_edge.png",
            "tr01_reference_wall_shadow_corner_outer.png",
            "tr01_reference_wall_shadow_corner_inner.png",
        };

        /// <summary>
        /// 접점 AO 는 <b>변형이 아니라 방향</b>이다 — n/e/s/w 각각이 그 방향 접점의 음영이다.
        /// 변형 패턴(a..f)으로 긁으면 <c>_e</c> 가 "변형 e" 로 잡히고 <c>_s</c>·<c>_w</c> 는
        /// 목록에 없어 빠진다(2026-09-10). 방향은 순서가 계약이다 — N,E,S,W.
        /// </summary>
        static readonly string[] ContactAoByDirection =
        {
            "tr01_reference_contact_ao_a.png",   // N (1차 납품, 북쪽 접점)
            "tr01_reference_contact_ao_e.png",   // E
            "tr01_reference_contact_ao_s.png",   // S
            "tr01_reference_contact_ao_w.png",   // W
        };

        [MenuItem("Tunnel Crew/비주얼 · 레퍼런스 환경 키트 생성 (3단계)", priority = 27)]
        public static void Run()
        {
            var report = new StringBuilder("[비주얼] 레퍼런스 환경 키트\n");

            // ── 표면 머티리얼 셋 3종. 채널 없음 — albedo 는 스프라이트에서 온다.
            var floorSet = EnsureSet(FloorSetPath, MinLightSlot.Floor, report);
            var wallTopSet = EnsureSet(WallTopSetPath, MinLightSlot.WallTop, report);
            var wallFrontSet = EnsureSet(WallFrontSetPath, MinLightSlot.WallFront, report);

            // ── 바닥 6장. 캘리브레이션 도구와 같은 임포트 설정(128 PPU · 중앙 피벗 · 불투명).
            var floors = new List<Sprite>(FloorFiles.Length);
            foreach (string f in FloorFiles)
            {
                string path = $"{ArtDir}/{f}";
                EnsureSprite(path, new Vector2(0.5f, 0.5f), alpha: false);
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sp == null) { report.Append($"  ✕ 바닥 누락: {f}\n"); continue; }
                if (sp.texture.width != 128 || sp.texture.height != 128)
                    report.Append($"  ⚠ {f} 는 {sp.texture.width}×{sp.texture.height} — 계약은 128×128\n");
                floors.Add(sp);
            }

            // ── 임포트 정합을 먼저 전부 끝낸다(PrepareImports 주석 참고).
            PrepareImports();

            // ── 벽 타일러블 — 아트 요청서의 파일명으로 있는 만큼 집는다.
            var wallTop = Collect(WallTopPattern, new Vector2(0.5f, 0.5f), alpha: false);
            var wallTopRim = Collect(WallTopRimPattern, new Vector2(0.5f, 0.5f), alpha: false);
            var wallFront = Collect(WallFrontPattern, new Vector2(0.5f, 0.0f), alpha: false);   // 하단 중앙
            var westSide = Collect(WestSidePattern, new Vector2(0.5f, 0.0f), alpha: false);   // 하단 중앙
            var eastSide = Collect(EastSidePattern, new Vector2(0.5f, 0.0f), alpha: false);
            var outerCorner = Collect(OuterCornerPattern, new Vector2(0.5f, 0.5f), alpha: false);
            var innerCorner = Collect(InnerCornerPattern, new Vector2(0.5f, 0.5f), alpha: false);
            var floorEdge = Collect(FloorEdgePattern, new Vector2(0.5f, 0.5f), alpha: false);

            // 접점 AO — 방향 순서(N,E,S,W)가 계약이다. 빠진 방향은 N 으로 메워 배열 길이를 4로 유지한다
            // (렌더러가 방향을 인덱스로 찾으므로 길이가 줄면 방향이 어긋난다).
            var contactAo = new List<Sprite>(4);
            foreach (string file in ContactAoByDirection)
            {
                string path = $"{ArtDir}/{file}";
                if (!File.Exists(AbsolutePath(path))) { report.Append($"  ⚠ 접점 AO 누락: {file}\n"); continue; }
                EnsureSprite(path, new Vector2(0.5f, 0.5f), alpha: true);
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sp != null) contactAo.Add(sp);
            }

            // 드롭섀도 4장 — 역할 순서가 계약. 하나라도 빠지면 배열을 비워 렌더러가 단색 셀로 되돌아가게 한다.
            var wallShadow = new List<Sprite>(4);
            foreach (string file in WallShadowByRole)
            {
                string path = $"{ArtDir}/{file}";
                if (!File.Exists(AbsolutePath(path))) { report.Append($"  ⚠ 드롭섀도 누락: {file}\n"); continue; }
                EnsureSprite(path, new Vector2(0.5f, 0.5f), alpha: true);
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sp != null) wallShadow.Add(sp);
            }
            if (wallShadow.Count != WallShadowByRole.Length) wallShadow.Clear();

            // 균열 3단계 — 순서 = 단계. 정면과 같은 피벗(하단 중앙).
            var wallCrack = new List<Sprite>(3);
            foreach (string file in WallCrackByStage)
            {
                string path = $"{ArtDir}/{file}";
                if (!File.Exists(AbsolutePath(path))) { report.Append($"  ⚠ 균열 누락: {file}\n"); continue; }
                EnsureSprite(path, new Vector2(0.5f, 0.0f), alpha: true);
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sp != null) wallCrack.Add(sp);
            }

            // ── 키트. 없으면 만들고, 스프라이트 목록은 도구가 갈아치운다.
            var kit = AssetDatabase.LoadAssetAtPath<EnvironmentKit>(KitPath);
            bool created = kit == null;
            if (created)
            {
                kit = ScriptableObject.CreateInstance<EnvironmentKit>();
                AssetDatabase.CreateAsset(kit, KitPath);
            }

            // 바닥은 도구가 항상 다시 쓴다 — 승인 목록이 바뀌면(이번엔 6장→3장) 자산이 따라와야 한다.
            // 나머지 배열은 비어 있을 때만 채운다(아트 도착 시 재실행이 정상 사용법).
            kit.floorBase = floors.ToArray();
            report.Append($"  ↻ floorBase: {floors.Count}장으로 갱신(승인 a/b/c)\n");
            Replace(ref kit.floorEdge, floorEdge, "floorEdge", report);
            Replace(ref kit.contactAo, contactAo, "contactAo", report);
            Replace(ref kit.wallTop, wallTop, "wallTop", report);
            Replace(ref kit.wallTopRim, wallTopRim, "wallTopRim", report);
            Replace(ref kit.wallFront, wallFront, "wallFront", report);
            Replace(ref kit.westSide, westSide, "westSide", report);
            Replace(ref kit.eastSide, eastSide, "eastSide", report);
            Replace(ref kit.outerCorner, outerCorner, "outerCorner", report);
            Replace(ref kit.innerCorner, innerCorner, "innerCorner", report);
            Replace(ref kit.wallShadow, wallShadow, "wallShadow(center/edge/outer/inner)", report);
            Replace(ref kit.wallCrack, wallCrack, "wallCrack(1/2/3)", report);
            EditorUtility.SetDirty(kit);
            AssetDatabase.SaveAssets();

            // ── 4단계 진입 가능 여부. 환경 렌더러는 floorBase + wallTop + wallFront 가 있어야 방을 그린다.
            bool ready = Len(kit.floorBase) > 0 && Len(kit.wallTop) > 0 && Len(kit.wallFront) > 0;
            report.Append(ready
                ? "\n▶ 4단계 진입 가능 — floorBase·wallTop·wallFront 전부 채워졌다.\n"
                : "\n■ 4단계 대기 — 비어 있는 슬롯: " +
                  string.Join(", ", Missing(kit)) +
                  "\n  → docs/codex-art-request-reference-wall-set.md 의 7장이 도착하면 이 메뉴를 다시 실행한다.\n");
            report.Append($"자산: {KitPath}\n     {FloorSetPath}\n     {WallTopSetPath}\n     {WallFrontSetPath}");

            if (ready) Debug.Log(report.ToString()); else Debug.LogWarning(report.ToString());
        }

        // ───────────────────────────── 자산

        static SurfaceMaterialSet EnsureSet(string path, MinLightSlot slot, StringBuilder report)
        {
            var set = AssetDatabase.LoadAssetAtPath<SurfaceMaterialSet>(path);
            if (set != null) { report.Append($"  = 유지 {Path.GetFileName(path)}\n"); return set; }

            set = ScriptableObject.CreateInstance<SurfaceMaterialSet>();
            set.kind = SurfaceMaterialSet.Kind.World;
            set.minLightSlot = slot;
            // 구 트랙 승인값과 같게 둔다 — 채널이 오면 그 세기로 바로 비교할 수 있다.
            set.normalStrength = 1.6f;
            set.aoStrength = 1f;
            set.emissionTint = Color.white;
            set.emissionIntensity = 1f;
            set.minLightOverride = -1f;
            AssetDatabase.CreateAsset(set, path);
            report.Append($"  + 생성 {Path.GetFileName(path)} (채널 없음 · albedo 전용)\n");
            return set;
        }

        /// <summary>
        /// 도구가 소유하는 슬롯 — 찾은 것으로 갈아치운다.
        ///
        /// 전에는 "비어 있을 때만" 채우는 <c>FillIfEmpty</c> 였는데, 그 규칙이 벽 아트가 도착한
        /// 날(2026-09-09) 두 번 발목을 잡았다.
        /// <list type="number">
        /// <item>요소가 전부 <c>null</c> 인 길이 3 배열을 "기존 자산" 으로 오해해 유지했다.</item>
        /// <item>3장 중 1장만 잡힌 부분 상태에서 멈춰 나머지 2장이 영구히 빠졌다.</item>
        /// </list>
        /// 아트가 갱신되면 전체를 다시 잡는 것이 맞다 — 사람이 인스펙터에서 조정하는 값은
        /// 스프라이트 목록이 아니라 <see cref="SurfaceMaterialSet"/> 쪽이다.
        /// </summary>
        static void Replace(ref Sprite[] target, List<Sprite> found, string label, StringBuilder report)
        {
            int before = 0;
            if (target != null)
                foreach (var s in target)
                    if (s != null) before++;

            target = found.ToArray();
            report.Append(found.Count > 0
                ? $"  ↻ {label}: {found.Count}장 (이전 {before}장)\n"
                : $"  ○ {label}: 비어 있음\n");
        }

        /// <summary>
        /// 수집 전에 대상 파일의 임포트 설정을 <b>모두 먼저</b> 맞춘다.
        ///
        /// 리임포트가 걸린 파일은 그 프레임에 서브 에셋이 없어 <c>LoadAssetAtPath&lt;Sprite&gt;</c> 가
        /// null 이다. 정합과 수집을 같은 패스에서 하면 방금 고친 파일이 조용히 빠진다 —
        /// 벽 아트 7장이 스프라이트 시트로 들어온 날 실제로 0장이 수집됐다(2026-09-09).
        /// </summary>
        static void PrepareImports()
        {
            var specs = new (string pattern, Vector2 pivot, bool alpha)[]
            {
                (WallTopPattern, new Vector2(0.5f, 0.5f), false),
                (WallTopRimPattern, new Vector2(0.5f, 0.5f), false),
                (WallFrontPattern, new Vector2(0.5f, 0.0f), false),   // 하단 중앙
                (WestSidePattern, new Vector2(0.5f, 0.0f), false),
                (EastSidePattern, new Vector2(0.5f, 0.0f), false),
                (OuterCornerPattern, new Vector2(0.5f, 0.5f), false),
                (InnerCornerPattern, new Vector2(0.5f, 0.5f), false),
                (FloorEdgePattern, new Vector2(0.5f, 0.5f), false),
            };

            bool touched = false;
            foreach (var (pattern, pivot, alpha) in specs)
                foreach (string v in Variants)
                {
                    string path = $"{ArtDir}/{string.Format(pattern, v)}";
                    if (!File.Exists(AbsolutePath(path))) continue;
                    EnsureSprite(path, pivot, alpha);
                    touched = true;
                }

            // 접점 AO 는 방향 파일명이라 변형 패턴 루프를 타지 않는다.
            foreach (string file in ContactAoByDirection)
            {
                string path = $"{ArtDir}/{file}";
                if (!File.Exists(AbsolutePath(path))) continue;
                EnsureSprite(path, new Vector2(0.5f, 0.5f), alpha: true);
                touched = true;
            }

            foreach (string file in WallShadowByRole)
            {
                string path = $"{ArtDir}/{file}";
                if (!File.Exists(AbsolutePath(path))) continue;
                EnsureSprite(path, new Vector2(0.5f, 0.5f), alpha: true);
                touched = true;
            }
            foreach (string file in WallCrackByStage)
            {
                string path = $"{ArtDir}/{file}";
                if (!File.Exists(AbsolutePath(path))) continue;
                EnsureSprite(path, new Vector2(0.5f, 0.0f), alpha: true);
                touched = true;
            }

            if (touched) AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        static List<Sprite> Collect(string pattern, Vector2 pivot, bool alpha)
        {
            var list = new List<Sprite>();
            foreach (string v in Variants)
            {
                string path = $"{ArtDir}/{string.Format(pattern, v)}";
                if (!File.Exists(AbsolutePath(path))) continue;
                EnsureSprite(path, pivot, alpha);
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sp == null)
                {
                    // 정합이 방금 리임포트를 걸었다면 한 번 더 확정하고 읽는다.
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
                if (sp != null) list.Add(sp);
            }
            return list;
        }

        static int Len(Sprite[] a) => a == null ? 0 : a.Length;

        static IEnumerable<string> Missing(EnvironmentKit kit)
        {
            if (Len(kit.floorBase) == 0) yield return "floorBase";
            if (Len(kit.wallTop) == 0) yield return "wallTop";
            if (Len(kit.wallFront) == 0) yield return "wallFront";
        }

        // ───────────────────────────── 임포트

        /// <summary>
        /// 캘리브레이션·프리셋 랩 도구와 <b>같은</b> 임포트 설정. 이미 맞으면 다시 임포트하지 않는다 —
        /// 공유 에셋을 건드리면 캘리브레이션 QA 캡처와 픽셀이 어긋날 수 있다.
        /// </summary>
        static void EnsureSprite(string path, Vector2 pivot, bool alpha)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(path) as TextureImporter;
            }
            if (importer == null) throw new InvalidOperationException($"TextureImporter not found: {path}");

            // 슬라이스 모드도 조건에 넣는다. 벽 아트가 스프라이트 시트(Multiple)로 들어온 날
            // (2026-09-09) 이 조건이 없어서 "type=Sprite · PPU=128 · 스프라이트 로드 성공(서브 `_0`)"
            // 이 전부 참이 되어 그냥 통과했다. 결과가 잘린 조각 하나와 먹지 않는 피벗이었다.
            bool ok = importer.textureType == TextureImporterType.Sprite
                      && importer.spriteImportMode == SpriteImportMode.Single
                      && Mathf.Approximately(importer.spritePixelsPerUnit, 128f)
                      && AssetDatabase.LoadAssetAtPath<Sprite>(path) != null;
            if (ok) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 128f;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = alpha;

            // 피벗은 TextureImporterSettings 로만 넣을 수 있다.
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();

            // 슬라이스 모드는 <b>그 뒤에 프로퍼티로 다시</b> 확정한다.
            //
            // ReadTextureSettings 가 디스크의 현재 설정을 통째로 읽어 오므로(spriteMode 포함),
            // SetTextureSettings 앞에서 spriteImportMode 를 넣으면 되돌아간다. settings.spriteMode
            // 를 직접 넣어도 Multiple 이 유지됐다(실측: 6장 중 5장). 프로퍼티 설정 + 잘린 조각
            // 목록 비우기 + 두 번째 리임포트 조합이 실제로 통한다.
            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
#pragma warning disable CS0618
                importer.spritesheet = new SpriteMetaData[0];
#pragma warning restore CS0618
                importer.SaveAndReimport();
            }
        }

        static string AbsolutePath(string assetPath) =>
            Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, assetPath);
    }
}
