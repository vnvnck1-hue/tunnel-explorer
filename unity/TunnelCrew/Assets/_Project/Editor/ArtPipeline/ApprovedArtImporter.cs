using System.Collections.Generic;
using System.IO;
using TunnelCrew.Presentation.Visual;
using UnityEditor;
using UnityEngine;

namespace TunnelCrew.EditorTools.ArtPipeline
{
    /// <summary>
    /// 기능명세서 §12.2 · 아트 규격 §10 — 승인 패키지를 Unity 로 들여오고 EnvironmentKit 에 꽂는다.
    ///
    /// <b>approved/ 만 들여온다.</b> <c>status</c> 가 <c>approved</c> 가 아닌 자산과
    /// <c>concept/</c>·<c>source/</c>·<c>working/</c> 폴더는 복사도 임포트도 하지 않는다.
    /// 그 파일들은 체커보드 배경이 불투명 픽셀로 구워져 있고 캔버스·피벗도 정규화되지 않았다
    /// (manifest 의 knownIssues 가 그렇게 적고 있다).
    ///
    /// <b>원본을 건드리지 않는다.</b> <c>art-production/</c> 은 Codex 아트 트랙의 작업 공간이다.
    /// 여기서는 읽어서 <c>Assets/</c> 안으로 복사만 하고, 원본 파일은 수정하지 않는다.
    /// </summary>
    public static class ApprovedArtImporter
    {
        /// <summary>임포트된 승인 아트가 들어갈 곳.</summary>
        public const string ImportDir = "Assets/Art/Visual/TestRoomV01";

        /// <summary>
        /// 패키지 상대 경로 → 프로젝트 안의 임포트 경로.
        ///
        /// 승인 패키지는 채널·분류로 폴더를 나누지만 프로젝트에는 <b>파일명만</b> 남겨
        /// 평평하게 넣는다. 파일명 규칙이 이미 <c>tr01_&lt;분류&gt;_&lt;이름&gt;_&lt;채널&gt;</c> 로
        /// 유일하므로 폴더 구조를 복제할 이유가 없고, 아틀라스·카탈로그가 경로를 짧게 쓴다.
        ///
        /// 임포터와 <see cref="PostImportValidator"/> 가 같은 규칙을 봐야 하므로 여기 하나만 둔다.
        /// </summary>
        public static string ImportedPathFor(string packageRelativePath)
            => string.IsNullOrEmpty(packageRelativePath)
                ? null
                : $"{ImportDir}/{Path.GetFileName(packageRelativePath)}";
        const string DataDir = "Assets/_Project/Data/Visual";

        // ───────────────────────────── 메뉴

        [MenuItem("Tunnel Crew/비주얼 · 승인 아트 검사 (§12.2)", priority = 40)]
        public static void ValidateMenu()
        {
            string root = ArtPackageLocator.Resolve(out string source);
            Debug.Log($"[비주얼] 승인 패키지 경로: {root}  ({source})");
            Log(ApprovedArtValidator.Validate(root));
        }

        [MenuItem("Tunnel Crew/비주얼 · 승인 아트 임포트 → EnvironmentKit", priority = 41)]
        public static void ImportMenu()
        {
            string root = ArtPackageLocator.Resolve(out string source);
            Debug.Log($"[비주얼] 승인 패키지 경로: {root}  ({source})");
            var report = ApprovedArtValidator.Validate(root);
            Log(report);

            if (report.WaitingForArt)
            {
                Debug.Log("[비주얼] 승인 아트가 없어 임포트할 것이 없다. 아트 대기 상태를 유지한다.");
                return;
            }
            if (!report.CanImport)
            {
                Debug.LogError("[비주얼] 검사에서 오류가 있어 임포트하지 않는다. " +
                               "위 리포트의 오류 항목을 아트 트랙에 전달할 것.");
                return;
            }

            Import(root, report);
        }

        static void Log(ArtValidationReport report)
        {
            string text = report.Describe();
            if (report.ErrorCount > 0) Debug.LogError(text);
            else if (report.WarningCount > 0) Debug.LogWarning(text);
            else Debug.Log(text);
        }

        // ───────────────────────────── 임포트

        static void Import(string packageRoot, ArtValidationReport report)
        {
            Directory.CreateDirectory(ImportDir);

            // 자산별로 채널 파일을 복사하고 임포터 설정을 넣는다.
            var albedoByAsset = new Dictionary<string, string>();     // assetId → Assets 경로
            var channelByAsset = new Dictionary<string, Dictionary<string, string>>();

            foreach (var asset in report.Importable)
            {
                var copied = new Dictionary<string, string>();
                foreach (var kv in asset.Channels)
                {
                    string channel = kv.Key;
                    if (!ApprovedArtContract.IsKnownChannel(channel)) continue;

                    string src = Path.Combine(packageRoot, kv.Value);
                    string dst = ImportedPathFor(kv.Value);

                    File.Copy(src, dst, overwrite: true);
                    copied[channel] = dst;
                    if (channel == "albedo") albedoByAsset[asset.AssetId] = dst;
                }
                channelByAsset[asset.AssetId] = copied;
            }

            AssetDatabase.Refresh();

            // 임포터 설정. Albedo 만 스프라이트이고 나머지는 데이터 맵이다.
            foreach (var asset in report.Importable)
            {
                if (!channelByAsset.TryGetValue(asset.AssetId, out var copied)) continue;
                foreach (var kv in copied)
                    ConfigureImporter(kv.Value, kv.Key, asset);
            }

            AssetDatabase.Refresh();

            var kit = FillKit(report, albedoByAsset);
            AssetDatabase.SaveAssets();

            // 채널 아틀라스 — 자산별 채널 맵을 동일 배치로 묶어야 Tilemap 머티리얼 하나로
            // Normal·Emission·Mask·AO 를 걸 수 있다(아트 규격 §8.1, 구현 기록 §11.4).
            var profile = AssetDatabase.LoadAssetAtPath<WorldVisualProfile>(
                $"{DataDir}/WorldVisualProfile_Stratum1.asset");
            var atlas = ChannelAtlasBuilder.Build(report, packageRoot, kit, profile);

            // 세트피스 — 타일이 아닌 자산은 개별 렌더러라 아틀라스가 필요 없다.
            // 자산마다 자기 채널 묶음을 갖는다(구현 기록 §13).
            var setPieces = SetPieceCatalogBuilder.Build(report, albedoByAsset, channelByAsset);

            Debug.Log($"[비주얼] 승인 아트 {report.Approved.Count - report.ExcludedCount}개 임포트 완료(제외 {report.ExcludedCount}) · {ImportDir}\n" +
                      $"  EnvironmentKit: {AssetDatabase.GetAssetPath(kit)}\n" +
                      $"  채널 아틀라스 {atlas.AtlasCount}장 · 아틀라스 스프라이트 {atlas.SpriteCount}개\n" +
                      $"  SurfaceMaterialSet: floor={(atlas.Floor != null ? "O" : "-")} " +
                      $"walltop={(atlas.WallTop != null ? "O" : "-")} " +
                      $"wallfront={(atlas.WallFront != null ? "O" : "-")}\n" +
                      $"  SetPieceCatalog: {setPieces.EntryCount}종 · 조명 소켓 {setPieces.SocketCount}개");
        }

        /// <summary>
        /// 채널별 임포터 설정. 여기가 §12.2 의 "Normal 이 sRGB 로 임포트됨" 을 실제로 막는 곳이다.
        /// PNG 파일 자체는 색 공간 강제를 담을 수 없으므로 검사기가 아니라 이 단계가 책임진다.
        /// </summary>
        static void ConfigureImporter(string assetPath, string channel, ApprovedAsset asset)
        {
            var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (ti == null)
            {
                Debug.LogWarning($"[비주얼] {assetPath} 의 TextureImporter 를 얻지 못했다.");
                return;
            }

            // 설정은 ImportExpectation 한 곳에서 온다 — 검증기가 되읽는 값과 같아야 한다.
            var expected = ImportExpectation.For(channel);
            expected.ApplyTo(ti);

            if (expected.IsSprite)
            {
                // manifest 의 픽셀 피벗 → Unity 정규화 피벗.
                // 좌표계 변환 규칙은 ApprovedArtContract.PivotToUnity 에 적어 두었다.
                ApprovedArtContract.PivotToUnity(asset.PivotX, asset.PivotY,
                    asset.Width, asset.Height, out float u, out float v);

                var settings = new TextureImporterSettings();
                ti.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(u, v);
                ti.SetTextureSettings(settings);
            }

            ti.SaveAndReimport();
        }

        static EnvironmentKit FillKit(ArtValidationReport report, Dictionary<string, string> albedoByAsset)
        {
            Directory.CreateDirectory(DataDir);
            string kitPath = $"{DataDir}/EnvironmentKit_TestRoomV01.asset";
            var kit = AssetDatabase.LoadAssetAtPath<EnvironmentKit>(kitPath);
            if (kit == null)
            {
                kit = ScriptableObject.CreateInstance<EnvironmentKit>();
                AssetDatabase.CreateAsset(kit, kitPath);
            }

            var buckets = new Dictionary<ApprovedArtContract.KitSlot, List<Sprite>>();

            // assetId 순으로 정렬해 슬롯 안의 순서를 재현 가능하게 만든다 —
            // 모듈 번호가 스프라이트 배열 인덱스이므로 순서가 흔들리면 배치가 달라진다.
            var sorted = new List<ApprovedAsset>(report.Importable);
            sorted.Sort((a, b) => string.CompareOrdinal(a.AssetId, b.AssetId));

            foreach (var asset in sorted)
            {
                if (asset.Slot == ApprovedArtContract.KitSlot.None) continue;
                if (!albedoByAsset.TryGetValue(asset.AssetId, out string path)) continue;

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    Debug.LogWarning($"[비주얼] {path} 에서 Sprite 를 불러오지 못했다.");
                    continue;
                }

                if (!buckets.TryGetValue(asset.Slot, out var list))
                    buckets[asset.Slot] = list = new List<Sprite>();
                list.Add(sprite);
            }

            Sprite[] Get(ApprovedArtContract.KitSlot slot)
                => buckets.TryGetValue(slot, out var l) ? l.ToArray() : null;

            // 승인본이 없는 슬롯은 비운다 — 임시 아트가 섞여 남지 않게 한다(§14 단계 D
            // "임시 아트와 신규 아트가 섞여 보이지 않는다").
            kit.floorBase = Get(ApprovedArtContract.KitSlot.FloorBase);
            kit.floorEdge = Get(ApprovedArtContract.KitSlot.FloorEdge);
            kit.contactAo = Get(ApprovedArtContract.KitSlot.ContactAo);
            kit.wallTop = Get(ApprovedArtContract.KitSlot.WallTop);
            kit.wallTopRim = Get(ApprovedArtContract.KitSlot.WallTopRim);
            kit.wallFront = Get(ApprovedArtContract.KitSlot.WallFront);
            kit.westSide = Get(ApprovedArtContract.KitSlot.WestSide);
            kit.eastSide = Get(ApprovedArtContract.KitSlot.EastSide);
            kit.outerCorner = Get(ApprovedArtContract.KitSlot.OuterCorner);
            kit.innerCorner = Get(ApprovedArtContract.KitSlot.InnerCorner);

            EditorUtility.SetDirty(kit);
            return kit;
        }
    }
}
