using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TunnelCrew.EditorTools.ArtPipeline
{
    /// <summary>
    /// 기능명세서 §12.2 2단계 — <b>임포트 후</b> 재검증.
    ///
    /// 1단계(<see cref="ApprovedArtValidator"/>)는 <c>art-production/</c> 의 원본 파일을
    /// 본다. 그건 "납품물이 규격에 맞는가" 를 묻는다. 이 2단계는 다른 것을 묻는다 —
    /// <b>Unity 가 실제로 무엇을 적용했는가.</b>
    ///
    /// 둘은 같지 않다. 원본이 완벽해도 임포터 설정이 어긋나면 화면이 틀린다. 특히
    /// 데이터 맵(Normal·Emission·Mask·AO)의 sRGB 가 켜지면 Unity 가 감마 변환을 걸어
    /// 노멀 방향과 AO 농도가 조용히 달라진다 — 콘솔에 아무 것도 남지 않는다.
    ///
    /// 이 검사가 잡아내는 것:
    /// <list type="bullet">
    /// <item>채널별 색공간·텍스처 타입·PPU·필터·랩·밉맵·압축</item>
    /// <item>스프라이트 피벗이 manifest 픽셀 피벗과 일치하는가</item>
    /// <item>임포트된 텍스처 크기가 manifest 와 일치하는가</item>
    /// <item>실루엣 자산의 GPU 포맷에 알파가 살아 있는가</item>
    /// <item>파일이 실제로 존재하는가(경로 오타·복사 실패)</item>
    /// </list>
    /// </summary>
    public static class PostImportValidator
    {
        /// <summary>피벗 허용 오차(정규화). 1/128 셀의 1/4 — 눈에 보이지 않는 수준.</summary>
        public const float PivotTolerance = 1f / 512f;

        public sealed class Result
        {
            public readonly List<ArtIssue> Issues = new List<ArtIssue>();
            public int Checked;
            public int TextureCount;
            public int ErrorCount { get; private set; }
            public int WarningCount { get; private set; }

            public bool Passed => ErrorCount == 0;

            public void Add(ArtIssueLevel level, string assetId, string path, string message)
            {
                Issues.Add(new ArtIssue { Level = level, AssetId = assetId, Path = path, Message = message });
                if (level == ArtIssueLevel.Error) ErrorCount++;
                else if (level == ArtIssueLevel.Warning) WarningCount++;
            }

            public string Describe()
            {
                var sb = new StringBuilder();
                sb.AppendLine("[비주얼] 임포트 후 재검증 (§12.2 2단계)");
                sb.AppendLine("원본이 아니라 Unity 가 실제로 적용한 설정을 되읽는다 — " +
                              "데이터 맵의 sRGB 가 켜지면 조용히 값이 달라진다");
                sb.AppendLine();
                sb.AppendLine($"자산 {Checked}개 · 텍스처 {TextureCount}장 · " +
                              $"오류 {ErrorCount} · 경고 {WarningCount}");
                sb.AppendLine(Passed ? "  통과." : "  임포트 설정이 계약과 다르다.");

                if (Issues.Count > 0)
                {
                    sb.AppendLine();
                    for (int level = 2; level >= 0; level--)
                        foreach (var i in Issues)
                            if ((int)i.Level == level) sb.AppendLine("  " + i);
                }
                return sb.ToString();
            }
        }

        [MenuItem("Tunnel Crew/비주얼 · 임포트 후 재검증 (§12.2)", priority = 42)]
        public static void Run()
        {
            string packageRoot = ArtPackageLocator.Resolve();
            var report = ApprovedArtValidator.Validate(packageRoot);
            var result = Validate(report);

            if (result.Passed) Debug.Log(result.Describe());
            else Debug.LogError(result.Describe());
        }

        /// <summary>
        /// 1단계 리포트의 <see cref="ArtValidationReport.Importable"/> 자산이 실제로
        /// 어떻게 임포트됐는지 확인한다.
        /// </summary>
        public static Result Validate(ArtValidationReport report)
        {
            var result = new Result();
            if (report == null || !report.ManifestFound) return result;

            foreach (var asset in report.Importable)
            {
                result.Checked++;

                foreach (var pair in asset.Channels)
                {
                    string channel = pair.Key;
                    if (!ApprovedArtContract.IsKnownChannel(channel)) continue;

                    string assetPath = ApprovedArtImporter.ImportedPathFor(pair.Value);
                    var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    if (ti == null)
                    {
                        // 원본은 통과했는데 프로젝트에 없다 — 복사가 실패했거나 경로가 다르다.
                        result.Add(ArtIssueLevel.Error, asset.AssetId, assetPath,
                            "임포트된 텍스처가 없다 — 임포트를 다시 실행할 것");
                        continue;
                    }
                    result.TextureCount++;

                    string mismatch = ImportExpectation.For(channel).DescribeMismatch(ti);
                    if (mismatch != null)
                        result.Add(ArtIssueLevel.Error, asset.AssetId, assetPath,
                            $"'{channel}' 임포트 설정 불일치: {mismatch}");

                    CheckTexture(result, asset, channel, assetPath);
                    if (channel == "albedo") CheckSpritePivot(result, asset, assetPath);
                }
            }

            return result;
        }

        /// <summary>임포트된 텍스처의 실제 크기와 포맷. GPU 에 올라간 상태를 본다.</summary>
        static void CheckTexture(Result result, ApprovedAsset asset, string channel, string assetPath)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex == null)
            {
                result.Add(ArtIssueLevel.Error, asset.AssetId, assetPath, "Texture2D 로 불러오지 못했다");
                return;
            }

            if (asset.Width > 0 && asset.Height > 0 &&
                (tex.width != asset.Width || tex.height != asset.Height))
                result.Add(ArtIssueLevel.Error, asset.AssetId, assetPath,
                    $"임포트된 크기 {tex.width}×{tex.height} 가 manifest {asset.Width}×{asset.Height} 와 다르다 " +
                    "— Max Size 로 축소됐을 수 있다");

            // 실루엣이 필요한 자산은 알파가 살아 있어야 한다. 압축 포맷으로 바뀌면
            // 알파가 뭉개지거나 사라진다(§12.2).
            if (channel == "albedo" && ApprovedArtContract.RequiresSilhouetteAlpha(asset.Slot)
                && !HasAlpha(tex.format))
                result.Add(ArtIssueLevel.Error, asset.AssetId, assetPath,
                    $"GPU 포맷 {tex.format} 에 알파가 없다 — 실루엣이 사라진다");
        }

        static bool HasAlpha(TextureFormat f)
        {
            switch (f)
            {
                case TextureFormat.Alpha8:
                case TextureFormat.ARGB4444:
                case TextureFormat.RGBA32:
                case TextureFormat.ARGB32:
                case TextureFormat.RGBA4444:
                case TextureFormat.BGRA32:
                case TextureFormat.RGBAHalf:
                case TextureFormat.RGBAFloat:
                case TextureFormat.DXT5:
                case TextureFormat.BC7:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 스프라이트 피벗이 manifest 픽셀 피벗과 같은가.
        ///
        /// 여기가 틀리면 발점이 어긋나 정렬(§6.5)과 접촉 AO(§7.4)가 전부 밀린다. 그런데
        /// 화면에서는 "조금 이상한데" 로만 보여 원인을 찾기 어렵다 — 그래서 숫자로 잡는다.
        /// </summary>
        static void CheckSpritePivot(Result result, ApprovedAsset asset, string assetPath)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                result.Add(ArtIssueLevel.Error, asset.AssetId, assetPath,
                    "Albedo 가 Sprite 로 임포트되지 않았다");
                return;
            }

            if (!Mathf.Approximately(sprite.pixelsPerUnit, ApprovedArtContract.DeliveryPixelsPerCell))
                result.Add(ArtIssueLevel.Error, asset.AssetId, assetPath,
                    $"스프라이트 PPU {sprite.pixelsPerUnit} — {ApprovedArtContract.DeliveryPixelsPerCell} 이어야 한다");

            if (asset.Width <= 0 || asset.Height <= 0) return;

            ApprovedArtContract.PivotToUnity(asset.PivotX, asset.PivotY,
                asset.Width, asset.Height, out float wantU, out float wantV);

            // Sprite.pivot 은 픽셀 단위다. rect 크기로 정규화해 비교한다.
            float gotU = sprite.rect.width > 0 ? sprite.pivot.x / sprite.rect.width : 0f;
            float gotV = sprite.rect.height > 0 ? sprite.pivot.y / sprite.rect.height : 0f;

            if (Mathf.Abs(gotU - wantU) > PivotTolerance || Mathf.Abs(gotV - wantV) > PivotTolerance)
                result.Add(ArtIssueLevel.Error, asset.AssetId, assetPath,
                    $"스프라이트 피벗 ({gotU:0.####}, {gotV:0.####}) 가 manifest " +
                    $"pivotPixels [{asset.PivotX}, {asset.PivotY}] → ({wantU:0.####}, {wantV:0.####}) 와 다르다");
        }
    }
}
