using System.Collections.Generic;
using System.IO;
using TunnelCrew.Presentation.Visual;
using UnityEditor;
using UnityEngine;

namespace TunnelCrew.EditorTools.ArtPipeline
{
    /// <summary>
    /// 타일이 아닌 승인 자산(아치·기둥·조명 소품·영웅 설비)을
    /// <see cref="SetPieceCatalog"/> 로 만든다(기능명세서 §8.7·§12.1).
    ///
    /// <b>세트피스는 아틀라스로 묶지 않는다.</b> 타일은 Tilemap 하나에 머티리얼 하나를
    /// 쓰므로 자산별 채널 맵을 넣을 수 없어 아틀라스가 필요했다(구현 기록 §12). 세트피스는
    /// 개별 <c>SpriteRenderer</c> 라 자산마다 머티리얼을 가질 수 있고, 방당 몇 개뿐이므로
    /// (§8.4) 배칭 손실이 문제가 되지 않는다. 크기도 640×512, 512×384, 256×256 로 제각각이라
    /// 격자 패킹이 낭비가 크다.
    /// </summary>
    public static class SetPieceCatalogBuilder
    {
        const string DataDir = "Assets/_Project/Data/Visual";
        const string CatalogPath = DataDir + "/SetPieceCatalog_TestRoomV01.asset";
        const string MaterialDir = DataDir + "/SetPieceMaterials";

        public struct Result
        {
            public SetPieceCatalog Catalog;
            public int EntryCount;
            public int SocketCount;
        }

        /// <summary>
        /// 검사를 통과한 자산 중 <see cref="ApprovedArtContract.KitSlot.None"/> 인 것을
        /// 카탈로그로 만든다. VFX 는 스프라이트 애니메이션 처리가 달라 제외한다.
        /// </summary>
        public static Result Build(ArtValidationReport report, Dictionary<string, string> albedoByAsset,
            Dictionary<string, Dictionary<string, string>> channelByAsset)
        {
            var result = new Result();
            if (report == null) return result;

            Directory.CreateDirectory(MaterialDir);

            // assetId 순으로 담아 재현 가능하게 만든다.
            var sorted = new List<ApprovedAsset>(report.Importable);
            sorted.Sort((a, b) => string.CompareOrdinal(a.AssetId, b.AssetId));

            var entries = new List<SetPieceDef>();
            int sockets = 0;

            foreach (var asset in sorted)
            {
                if (asset.Slot != ApprovedArtContract.KitSlot.None) continue;   // 타일은 아틀라스로 간다
                if (IsExcluded(asset.AssetId)) continue;

                if (!albedoByAsset.TryGetValue(asset.AssetId, out string albedoPath))
                {
                    Debug.LogWarning($"[비주얼] {asset.AssetId} 의 Albedo 임포트 경로를 찾지 못했다.");
                    continue;
                }

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(albedoPath);
                if (sprite == null)
                {
                    Debug.LogWarning($"[비주얼] {albedoPath} 에서 Sprite 를 불러오지 못했다.");
                    continue;
                }

                var def = new SetPieceDef
                {
                    assetId = asset.AssetId,
                    sprite = sprite,
                    materials = BuildMaterialSet(asset, channelByAsset),
                    footprintCells = new Vector2Int(
                        Mathf.Max(1, asset.FootprintCols), Mathf.Max(1, asset.FootprintRows)),
                    visualHeightCells = asset.VisualHeightCells > 0f ? asset.VisualHeightCells : 1f,
                    sortingLayer = asset.SortingLayer,
                    localOrder = asset.LocalOrder,
                    occluderGroup = asset.OccluderGroup,
                    fadeTargetAlpha = asset.FadeTargetAlpha ?? 0f,
                    lightSockets = ClassifySockets(asset),
                    replacementAssetId = asset.ReplacementAssetId,
                    // 승인 패키지의 윤곽이 실루엣을 담고 있으면 그것을 쓴다.
                    // 사각형이거나 없으면 알파에서 근사 윤곽을 뽑는다(임시 경로 —
                    // SpriteAlphaContour 주석 참고, 2026-09-09).
                    shadowContourCells = ResolveShadowContour(asset, sprite, report),
                };

                sockets += def.lightSockets != null ? def.lightSockets.Length : 0;
                entries.Add(def);
            }

            var catalog = LoadOrCreate<SetPieceCatalog>(CatalogPath);
            catalog.EditorSetEntries(entries);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            result.Catalog = catalog;
            result.EntryCount = entries.Count;
            result.SocketCount = sockets;
            return result;
        }

        /// <summary>
        /// 소켓마다 §7.3 광원 분류를 채운다.
        ///
        /// manifest 가 <c>lightClass</c> 를 직접 선언해 주는 것이 옳고, 그때까지는
        /// <c>emissionMode</c> 와 소켓 규모에서 유도한다 —
        /// 규칙은 <see cref="LightClassRules.Classify"/> 한곳에 있다.
        /// </summary>
        static LightSocketDef[] ClassifySockets(ApprovedAsset asset)
        {
            var arr = asset.LightSockets.ToArray();
            for (int i = 0; i < arr.Length; i++)
                arr[i].lightClass = LightClassRules.Classify(
                    asset.EmissionMode, arr[i].rangeCells, arr[i].intensity);
            return arr;
        }

        /// <summary>
        /// VFX 는 카탈로그에 넣지 않는다 — 루프 애니메이션과 파티클 배선이 달라
        /// 별도 시스템(§11.2 지속 환경 VFX)이 필요하다.
        /// </summary>
        static bool IsExcluded(string assetId)
        {
            if (string.IsNullOrEmpty(assetId)) return true;
            string id = assetId.ToUpperInvariant();
            return id.Contains("-VFX-") || id.Contains("-CONCEPT-");
        }

        /// <summary>
        /// 그림자 캐스터 윤곽을 정한다 — 승인 데이터가 실루엣을 담고 있으면 그것,
        /// 아니면 알파에서 뽑은 근사 윤곽(임시 경로).
        ///
        /// 승인 패키지 revision 17 은 캐스터를 8종만 주고 그것도 전부 축 정렬 사각형이라
        /// 모든 물체의 그림자가 네모로 보였다. 아트가 §7.4 윤곽을 납품하면 이 함수가
        /// 자동으로 그쪽을 택하므로, 그때 지울 코드는 <see cref="SpriteAlphaContour"/> 뿐이다.
        /// </summary>
        static float[] ResolveShadowContour(ApprovedAsset asset, Sprite sprite,
                                            ArtValidationReport report)
        {
            int cols = System.Math.Max(1, asset.FootprintCols);
            int rows = System.Math.Max(1, asset.FootprintRows);

            // 아트가 준 윤곽이 사각형이 아니면 그대로 믿는다.
            if (!SpriteAlphaContour.IsPlainRectangle(asset.ShadowContourCells, cols, rows))
                return asset.ShadowContourCells;

            var traced = SpriteAlphaContour.FromSprite(sprite, cols, rows);
            if (traced == null)
            {
                report?.Add(ArtIssueLevel.Warning, asset.AssetId, null,
                    "알파에서 그림자 윤곽을 뽑지 못했다 — footprint 사각형으로 떨어진다");
                return asset.ShadowContourCells;
            }

            report?.Add(ArtIssueLevel.Info, asset.AssetId, null,
                $"그림자 윤곽을 알파에서 만들었다(점 {traced.Length / 2}개, 임시 경로) — " +
                "§7.4 는 manifest 의 shadowCasterFootprintCells 를 요구한다");
            return traced;
        }

        /// <summary>세트피스 자산 하나의 채널 묶음. 자산마다 하나씩 만든다.</summary>
        static SurfaceMaterialSet BuildMaterialSet(ApprovedAsset asset,
            Dictionary<string, Dictionary<string, string>> channelByAsset)
        {
            string safeId = asset.AssetId.ToLowerInvariant().Replace('-', '_');
            var set = LoadOrCreate<SurfaceMaterialSet>($"{MaterialDir}/SurfaceMaterialSet_{safeId}.asset");

            set.kind = SurfaceMaterialSet.Kind.World;
            // 세트피스는 솟은 구조물이다 — 벽 정면과 같은 최소광 계수를 쓴다(§7.2).
            set.minLightSlot = MinLightSlot.WallFront;
            set.normal = null;
            set.emission = null;
            set.materialMask = null;
            set.ao = null;

            if (channelByAsset != null && channelByAsset.TryGetValue(asset.AssetId, out var copied))
            {
                set.normal = Load(copied, "normal");
                set.emission = Load(copied, "emission");
                set.materialMask = Load(copied, "mask");
                set.ao = Load(copied, "ao");
            }

            set.normalStrength = 1f;
            set.aoStrength = 1f;
            set.emissionIntensity = 1f;
            set.minLightOverride = -1f;
            EditorUtility.SetDirty(set);
            return set;
        }

        static Texture2D Load(Dictionary<string, string> copied, string channel)
            => copied.TryGetValue(channel, out string p) ? AssetDatabase.LoadAssetAtPath<Texture2D>(p) : null;

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(a, path);
            return a;
        }
    }
}
