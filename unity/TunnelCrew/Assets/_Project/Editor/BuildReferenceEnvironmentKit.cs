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
        const string ContactAoPattern = "tr01_reference_contact_ao_{0}.png";
        const string OuterCornerPattern = "tr01_reference_wall_outer_corner_{0}_albedo.png";
        const string InnerCornerPattern = "tr01_reference_wall_inner_corner_{0}_albedo.png";
        const string FloorEdgePattern = "tr01_reference_floor_edge_{0}_albedo.png";
        static readonly string[] Variants = { "a", "b", "c", "d", "e", "f" };

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

            // ── 벽 타일러블 — 아트 요청서의 파일명으로 있는 만큼 집는다. 지금은 0 이 정상이다.
            var wallTop = Collect(WallTopPattern, new Vector2(0.5f, 0.5f), alpha: false);
            var wallTopRim = Collect(WallTopRimPattern, new Vector2(0.5f, 0.5f), alpha: false);
            var wallFront = Collect(WallFrontPattern, new Vector2(0.5f, 0.0f), alpha: false);   // 하단 중앙
            var contactAo = Collect(ContactAoPattern, new Vector2(0.5f, 0.5f), alpha: true);
            var outerCorner = Collect(OuterCornerPattern, new Vector2(0.5f, 0.5f), alpha: false);
            var innerCorner = Collect(InnerCornerPattern, new Vector2(0.5f, 0.5f), alpha: false);
            var floorEdge = Collect(FloorEdgePattern, new Vector2(0.5f, 0.5f), alpha: false);

            // ── 키트. 없으면 만들고, 있으면 비어 있는 배열만 채운다.
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
            FillIfEmpty(ref kit.floorEdge, floorEdge, "floorEdge", report);
            FillIfEmpty(ref kit.contactAo, contactAo, "contactAo", report);
            FillIfEmpty(ref kit.wallTop, wallTop, "wallTop", report);
            FillIfEmpty(ref kit.wallTopRim, wallTopRim, "wallTopRim", report);
            FillIfEmpty(ref kit.wallFront, wallFront, "wallFront", report);
            FillIfEmpty(ref kit.outerCorner, outerCorner, "outerCorner", report);
            FillIfEmpty(ref kit.innerCorner, innerCorner, "innerCorner", report);
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

        static void FillIfEmpty(ref Sprite[] target, List<Sprite> found, string label, StringBuilder report)
        {
            if (target != null && target.Length > 0)
            {
                report.Append($"  = {label}: 기존 {target.Length}장 유지\n");
                return;
            }
            target = found.ToArray();
            report.Append(found.Count > 0
                ? $"  + {label}: {found.Count}장 채움\n"
                : $"  ○ {label}: 비어 있음\n");
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

            bool ok = importer.textureType == TextureImporterType.Sprite
                      && Mathf.Approximately(importer.spritePixelsPerUnit, 128f)
                      && AssetDatabase.LoadAssetAtPath<Sprite>(path) != null;
            if (ok) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 128f;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = alpha;
            importer.SaveAndReimport();
        }

        static string AbsolutePath(string assetPath) =>
            Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, assetPath);
    }
}
