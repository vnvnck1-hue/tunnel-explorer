using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace TunnelCrew.EditorTools.ArtPipeline
{
    /// <summary>검사 항목 하나의 결과.</summary>
    public enum ArtIssueLevel
    {
        /// <summary>참고 사항. 임포트를 막지 않는다.</summary>
        Info = 0,
        /// <summary>고쳐야 하지만 임포트는 가능하다.</summary>
        Warning = 1,
        /// <summary>임포트를 막는다.</summary>
        Error = 2,
    }

    public struct ArtIssue
    {
        public ArtIssueLevel Level;
        public string AssetId;
        public string Path;
        public string Message;

        public override string ToString()
        {
            string tag = Level switch
            {
                ArtIssueLevel.Error => "오류",
                ArtIssueLevel.Warning => "경고",
                _ => "참고",
            };
            string where = string.IsNullOrEmpty(AssetId) ? Path : $"{AssetId} · {Path}";
            return string.IsNullOrEmpty(where) ? $"[{tag}] {Message}" : $"[{tag}] {where} — {Message}";
        }
    }

    /// <summary>승인 아트 하나가 임포트될 때 필요한 정보. 검사를 통과한 것만 채워진다.</summary>
    public sealed class ApprovedAsset
    {
        public string AssetId;
        public int Revision;
        public int FootprintCols = 1, FootprintRows = 1;
        public float VisualHeightCells;
        public int PivotX, PivotY;
        /// <summary>피벗을 manifest 에서 읽었는가. false 면 슬롯에서 유추한 값이다.</summary>
        public bool PivotFromManifest;
        public string SortingLayer;
        public int LocalOrder;
        public string OccluderGroup;
        public float? FadeTargetAlpha;
        public ApprovedArtContract.KitSlot Slot;

        /// <summary>조명 소켓(§7.3·§8.2). 발점 기준 셀 오프셋으로 변환해 담는다.</summary>
        public readonly List<TunnelCrew.Presentation.Visual.LightSocketDef> LightSockets
            = new List<TunnelCrew.Presentation.Visual.LightSocketDef>();

        /// <summary>manifest 의 shadowCasterPath. 없으면 footprint 사각형을 쓴다.</summary>
        public string ShadowCasterPath;

        /// <summary>
        /// manifest 의 <c>shadowCasterFootprintCells</c> 를 발점 기준으로 옮긴 윤곽
        /// (x0,y0,x1,y1,…). 없으면 null 이고 런타임이 footprint 사각형을 쓴다.
        /// </summary>
        public float[] ShadowContourCells;

        /// <summary>전경 오클루더인가(manifest <c>foregroundOccluder</c>).</summary>
        public bool ForegroundOccluder;

        /// <summary>전경 페이드 마스크 경로(manifest <c>fadeMaskPath</c>).</summary>
        public string FadeMaskPath;

        /// <summary>시각 높이를 manifest 에서 읽었는가. false 면 발점 피벗에서 복원한 값이다.</summary>
        public bool VisualHeightFromManifest;

        /// <summary>VFX 오버레이 자산인가(§11.2).</summary>
        public bool IsVfx;

        /// <summary>manifest 의 <c>emissionMode</c>. §7.3 광원 분류를 유도하는 데 쓴다.</summary>
        public string EmissionMode;

        /// <summary>
        /// 이 자산에 오류가 있어 임포트에서 제외되는가. 나머지 자산은 그대로 임포트한다 —
        /// 소품 하나가 깨졌다고 패키지 전체를 막으면 검증이 진행되지 않는다.
        /// </summary>
        public bool HasError;

        /// <summary>파괴 후 대체 자산 ID(§8.2).</summary>
        public string ReplacementAssetId;

        /// <summary>채널 이름 → 패키지 루트 기준 상대 경로.</summary>
        public readonly Dictionary<string, string> Channels = new Dictionary<string, string>();

        /// <summary>Albedo 캔버스 크기. 다른 채널은 이것과 같아야 한다.</summary>
        public int Width, Height;
    }

    /// <summary>패키지 전체 검사 결과.</summary>
    public sealed class ArtValidationReport
    {
        public readonly List<ArtIssue> Issues = new List<ArtIssue>();
        public readonly List<ApprovedAsset> Approved = new List<ApprovedAsset>();

        /// <summary>manifest 에 있지만 status 가 approved 가 아닌 자산 수.</summary>
        public int PendingCount;
        /// <summary>manifest 를 읽었는가.</summary>
        public bool ManifestFound;

        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }

        /// <summary>
        /// 아트 대기 상태 — manifest 는 있지만 승인본이 하나도 없다.
        /// <b>오류가 아니다.</b> 이 배치의 정상 상태다.
        /// </summary>
        public bool WaitingForArt => ManifestFound && Approved.Count == 0 && ErrorCount == 0;

        /// <summary>임포트 대상 — 자체 오류가 없는 승인 자산.</summary>
        public IEnumerable<ApprovedAsset> Importable
        {
            get
            {
                for (int i = 0; i < Approved.Count; i++)
                    if (!Approved[i].HasError) yield return Approved[i];
            }
        }

        /// <summary>임포트에서 제외되는 자산 수.</summary>
        public int ExcludedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Approved.Count; i++) if (Approved[i].HasError) n++;
                return n;
            }
        }

        /// <summary>
        /// 자산 하나가 깨졌다고 패키지 전체를 막지 않는다. 깨진 것만 빼고 나머지를
        /// 임포트해야 나머지 검증이 진행된다 — 다만 제외 사실을 리포트에 크게 남긴다.
        /// </summary>
        public bool CanImport => ManifestFound && ErrorCount == ExcludedCount && ExcludedCount < Approved.Count;

        public void Add(ArtIssueLevel level, string assetId, string path, string message)
        {
            Issues.Add(new ArtIssue { Level = level, AssetId = assetId, Path = path, Message = message });
            if (level == ArtIssueLevel.Error) ErrorCount++;
            else if (level == ArtIssueLevel.Warning) WarningCount++;
        }

        public string Describe()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[비주얼] 승인 아트 검사 (§12.2)");
            sb.AppendLine(ApprovedArtContract.Summary());
            sb.AppendLine();

            if (!ManifestFound)
            {
                sb.AppendLine("manifest.json 을 읽지 못했다.");
            }
            else if (WaitingForArt)
            {
                sb.AppendLine($"■ 아트 대기 상태 — approved 자산 0개, 진행 중 {PendingCount}개.");
                sb.AppendLine("  오류가 아니다. Codex 아트 트랙의 승인 패키지를 기다리는 정상 상태다.");
            }
            else
            {
                sb.AppendLine($"승인 {Approved.Count}개 · 진행 중 {PendingCount}개 · " +
                              $"오류 {ErrorCount} · 경고 {WarningCount}");
                if (ExcludedCount > 0)
                    sb.AppendLine($"  ■ 자산 {ExcludedCount}개는 오류로 임포트에서 제외된다 — " +
                                  "아트 트랙이 고쳐 다시 납품해야 한다.");
                sb.AppendLine(CanImport
                    ? $"  나머지 {Approved.Count - ExcludedCount}개는 임포트 가능하다."
                    : "  오류를 해결해야 임포트할 수 있다.");
            }

            if (Issues.Count > 0)
            {
                sb.AppendLine();
                // 오류 → 경고 → 참고 순. 같은 메시지가 여러 자산에서 나오면 한 줄로 묶는다 —
                // 51개 파일 패키지에서 채널당 같은 경고가 11번 반복되면 리포트를 읽을 수 없다.
                for (int level = 2; level >= 0; level--) AppendGrouped(sb, (ArtIssueLevel)level);
            }

            return sb.ToString();
        }

        void AppendGrouped(StringBuilder sb, ArtIssueLevel level)
        {
            var order = new List<string>();
            var byMessage = new Dictionary<string, List<ArtIssue>>();

            for (int i = 0; i < Issues.Count; i++)
            {
                if (Issues[i].Level != level) continue;
                string key = Issues[i].Message ?? string.Empty;
                if (!byMessage.TryGetValue(key, out var list))
                {
                    byMessage[key] = list = new List<ArtIssue>();
                    order.Add(key);
                }
                list.Add(Issues[i]);
            }

            string tag = level switch
            {
                ArtIssueLevel.Error => "오류",
                ArtIssueLevel.Warning => "경고",
                _ => "참고",
            };

            foreach (var key in order)
            {
                var list = byMessage[key];
                if (list.Count == 1)
                {
                    sb.AppendLine("  " + list[0]);
                    continue;
                }

                // 같은 메시지 묶음 — 대상 자산을 최대 4개까지 보여 준다.
                var ids = new List<string>();
                for (int i = 0; i < list.Count; i++)
                {
                    string id = string.IsNullOrEmpty(list[i].AssetId) ? list[i].Path : list[i].AssetId;
                    if (!string.IsNullOrEmpty(id) && !ids.Contains(id)) ids.Add(id);
                }

                string shown = string.Join(", ", ids.GetRange(0, System.Math.Min(4, ids.Count)));
                string more = ids.Count > 4 ? $" 외 {ids.Count - 4}개" : string.Empty;
                sb.AppendLine($"  [{tag}] ×{list.Count} {key}");
                sb.AppendLine($"         대상: {shown}{more}");
            }
        }
    }

    /// <summary>
    /// 기능명세서 §12.2 — 임포트 검사기. 승인 아트가 빌드 전에 계약을 지키는지 확인한다.
    ///
    /// <b>검사는 임포트 전에 끝난다.</b> <c>art-production/</c> 은 <c>Assets/</c> 밖이라
    /// Unity 자산이 아니고, 잘못된 파일을 프로젝트에 들여놓지 않는 것이 목적이다. PNG 헤더를
    /// 직접 읽어 크기·알파를 보고, 나머지는 manifest 와 파일명으로 판단한다.
    ///
    /// 색 공간(Normal·Mask·AO 는 Linear)은 PNG 파일이 담을 수 없는 정보다. 이 단계에서는
    /// sRGB 청크 유무만 참고로 보고하고, 실제 강제는 임포트 단계
    /// (<see cref="ApprovedArtImporter"/>)가 한다. 임포트 뒤 재검사에서 확인한다.
    ///
    /// <b>순수 함수에 가깝게 유지한다</b> — 파일 읽기 외에 Unity API 를 쓰지 않아
    /// EditMode 테스트가 임시 폴더로 검사 전체를 돌릴 수 있다.
    /// </summary>
    public static class ApprovedArtValidator
    {
        /// <summary>저장소 루트 기준 패키지 경로.</summary>
        public const string DefaultPackageRoot = "art-production/test-room-v01";

        public static ArtValidationReport Validate(string packageRoot)
        {
            var report = new ArtValidationReport();

            string manifestPath = Path.Combine(packageRoot, "metadata", "manifest.json");
            if (!File.Exists(manifestPath))
            {
                report.Add(ArtIssueLevel.Error, null, manifestPath, "manifest.json 이 없다");
                return report;
            }

            string text;
            try { text = File.ReadAllText(manifestPath); }
            catch (System.Exception e)
            {
                report.Add(ArtIssueLevel.Error, null, manifestPath, $"읽기 실패: {e.Message}");
                return report;
            }

            var root = MiniJson.AsMap(MiniJson.Parse(text, out string parseError));
            if (root == null)
            {
                report.Add(ArtIssueLevel.Error, null, manifestPath,
                    $"JSON 파싱 실패: {parseError ?? "최상위가 객체가 아니다"}");
                return report;
            }
            report.ManifestFound = true;

            ValidatePackageHeader(root, report, manifestPath);

            var assets = MiniJson.AsList(root.TryGetValue("assets", out var a) ? a : null);
            if (assets == null)
            {
                report.Add(ArtIssueLevel.Error, null, manifestPath, "assets 배열이 없다");
                return report;
            }

            var seenIds = new HashSet<string>();
            foreach (var entry in assets)
            {
                var map = MiniJson.AsMap(entry);
                if (map == null)
                {
                    report.Add(ArtIssueLevel.Error, null, manifestPath, "assets 항목이 객체가 아니다");
                    continue;
                }

                string assetId = MiniJson.GetString(map, "assetId");
                string status = MiniJson.GetString(map, "status");

                if (string.IsNullOrEmpty(assetId))
                {
                    report.Add(ArtIssueLevel.Error, null, manifestPath, "assetId 가 없는 항목이 있다");
                    continue;
                }
                if (!seenIds.Add(assetId))
                    report.Add(ArtIssueLevel.Warning, assetId, manifestPath, "assetId 가 중복된다");

                if (status != ApprovedArtContract.StatusApproved)
                {
                    report.PendingCount++;
                    // status 가 approved 가 아닌 것은 검사·임포트 대상이 아니다.
                    // source/working/concept 를 Unity 로 들이지 않는 것이 계약이다.
                    report.Add(ArtIssueLevel.Info, assetId, MiniJson.GetString(map, "path"),
                        $"status '{status ?? "없음"}' — 임포트 대상이 아니다");
                    continue;
                }

                ValidateApproved(packageRoot, map, assetId, report);
            }

            return report;
        }

        /// <summary>
        /// <c>lightSockets</c> 를 발점 기준 셀 오프셋으로 바꿔 담는다.
        ///
        /// 소켓 픽셀은 <c>pivotPixels</c> 와 같은 좌표계(좌상단 원점, Y 아래로 증가)라
        /// 피벗을 먼저 확정한 뒤에 불러야 한다.
        /// </summary>
        static void ParseLightSockets(Dictionary<string, object> map, ApprovedAsset asset,
            ArtValidationReport report)
        {
            var list = MiniJson.AsList(map.TryGetValue("lightSockets", out var v) ? v : null);
            if (list == null || list.Count == 0) return;

            foreach (var entry in list)
            {
                var socket = MiniJson.AsMap(entry);
                if (socket == null)
                {
                    report.Add(ArtIssueLevel.Warning, asset.AssetId, null, "lightSockets 항목이 객체가 아니다");
                    continue;
                }

                string id = MiniJson.GetString(socket, "id", "socket");

                if (!MiniJson.TryGetInt2(socket, "pixel", out int sx, out int sy))
                {
                    report.Add(ArtIssueLevel.Warning, asset.AssetId, null,
                        $"lightSockets['{id}'] 에 pixel 이 없다 — 소켓을 건너뛴다");
                    continue;
                }

                if (sx < 0 || sy < 0 || sx > asset.Width || sy > asset.Height)
                    report.Add(ArtIssueLevel.Warning, asset.AssetId, null,
                        $"lightSockets['{id}'] pixel ({sx},{sy}) 이 캔버스 " +
                        $"{asset.Width}×{asset.Height} 밖이다");

                ApprovedArtContract.SocketOffsetCells(sx, sy, asset.PivotX, asset.PivotY,
                    out float ox, out float oy);

                string hex = MiniJson.GetString(socket, "color", "#ffffff");
                if (!ColorUtility.TryParseHtmlString(hex, out var color))
                {
                    report.Add(ArtIssueLevel.Warning, asset.AssetId, null,
                        $"lightSockets['{id}'] color '{hex}' 를 읽지 못했다 — 흰색으로 둔다");
                    color = Color.white;
                }

                MiniJson.TryGetFloat(socket, "rangeCells", out float range);
                MiniJson.TryGetFloat(socket, "intensity", out float intensity);
                if (range <= 0f)
                {
                    range = 2f;
                    report.Add(ArtIssueLevel.Warning, asset.AssetId, null,
                        $"lightSockets['{id}'] rangeCells 가 없다 — 2셀로 둔다");
                }

                asset.LightSockets.Add(new TunnelCrew.Presentation.Visual.LightSocketDef
                {
                    id = id,
                    offsetCells = new Vector2(ox, oy),
                    color = color,
                    rangeCells = range,
                    intensity = intensity > 0f ? intensity : 1f,
                });
            }
        }

        static void ValidatePackageHeader(Dictionary<string, object> root, ArtValidationReport report,
            string manifestPath)
        {
            string projection = MiniJson.GetString(root, "productionProjection");
            if (projection != "ReferenceTopDown")
                report.Add(ArtIssueLevel.Error, null, manifestPath,
                    $"productionProjection 이 '{projection}' 이다 — 프로덕션 인증 투영은 ReferenceTopDown 하나다(§6.2)");

            if (MiniJson.TryGetInt(root, "deliveryPixelsPerCell", out int delivery))
            {
                if (delivery != ApprovedArtContract.DeliveryPixelsPerCell)
                    report.Add(ArtIssueLevel.Error, null, manifestPath,
                        $"deliveryPixelsPerCell 이 {delivery} 다 — 계약은 " +
                        $"{ApprovedArtContract.DeliveryPixelsPerCell}(PPU {ApprovedArtContract.DeliveryPixelsPerCell})");
            }
            else report.Add(ArtIssueLevel.Warning, null, manifestPath, "deliveryPixelsPerCell 이 없다");

            if (MiniJson.TryGetInt(root, "sourcePixelsPerCell", out int source))
            {
                if (source < ApprovedArtContract.MinSourcePixelsPerCell)
                    report.Add(ArtIssueLevel.Warning, null, manifestPath,
                        $"sourcePixelsPerCell 이 {source} 다 — 원화는 " +
                        $"{ApprovedArtContract.MinSourcePixelsPerCell} 이상이어야 한다");
            }
        }

        static void ValidateApproved(string packageRoot, Dictionary<string, object> map,
            string assetId, ArtValidationReport report)
        {
            var asset = new ApprovedAsset { AssetId = assetId };
            if (MiniJson.TryGetInt(map, "revision", out int rev)) asset.Revision = rev;

            // ── footprint · 시각 높이 · 피벗
            if (MiniJson.TryGetInt2(map, "footprintCells", out int fc, out int fr))
            {
                asset.FootprintCols = fc;
                asset.FootprintRows = fr;
                if (fc < 1 || fr < 1)
                    report.Add(ArtIssueLevel.Error, assetId, null, $"footprintCells [{fc},{fr}] 에 0 이하가 있다");
            }
            else report.Add(ArtIssueLevel.Error, assetId, null, "footprintCells 가 없다");

            bool haveVisualHeight = MiniJson.TryGetFloat(map, "visualHeightCells", out float vh);
            if (haveVisualHeight) asset.VisualHeightCells = vh;
            asset.VisualHeightFromManifest = haveVisualHeight;
            asset.IsVfx = ApprovedArtContract.IsVfx(assetId);

            bool havePivot = MiniJson.TryGetInt2(map, "pivotPixels", out int px, out int py);
            asset.PivotFromManifest = havePivot;
            if (havePivot) { asset.PivotX = px; asset.PivotY = py; }
            else
                // 오류로 막지 않는다 — 슬롯에서 유추한 피벗으로도 조립 검증은 진행할 수 있고,
                // 막아 버리면 아트 트랙이 나머지 검사 결과를 볼 수 없다. 다만 최종 승인 전에는
                // 반드시 채워야 한다(아트 규격 §10).
                report.Add(ArtIssueLevel.Warning, assetId, null,
                    "pivotPixels 가 없다 — 슬롯 기본값으로 유추한다. 최종 납품에는 반드시 넣을 것 " +
                    "(좌상단 원점·Y 아래로 증가)");

            string hint = MiniJson.GetString(map, "sortingLayerHint");
            if (MiniJson.TryGetInt(map, "localOrder", out int lo)) asset.LocalOrder = lo;

            asset.OccluderGroup = MiniJson.GetString(map, "occluderGroup");
            asset.ForegroundOccluder = MiniJson.GetBool(map, "foregroundOccluder");
            asset.EmissionMode = MiniJson.GetString(map, "emissionMode");
            asset.FadeMaskPath = MiniJson.GetString(map, "fadeMaskPath");
            if (MiniJson.TryGetFloat(map, "fadeTargetAlpha", out float fa)) asset.FadeTargetAlpha = fa;

            // ── 채널
            var channels = MiniJson.AsMap(map.TryGetValue("channels", out var ch) ? ch : null);
            if (channels == null || channels.Count == 0)
            {
                report.Add(ArtIssueLevel.Error, assetId, null, "channels 가 없다");
                return;
            }

            // 슬롯을 먼저 정한다 — 캔버스 규칙(타일은 정확 일치)과 알파 요구가 슬롯에 따라
            // 달라지므로 채널 검사보다 앞서야 한다.
            string albedoName = null;
            if (channels.TryGetValue("albedo", out var albedoRaw) && albedoRaw is string albedoPath)
                albedoName = System.IO.Path.GetFileName(albedoPath);
            asset.Slot = ApprovedArtContract.ResolveSlot(assetId, albedoName);
            bool tiling = ApprovedArtContract.IsTilingSlot(asset.Slot);

            PngInfo albedoInfo = default;
            bool albedoSeen = false;

            // Albedo 를 먼저 처리해 다른 채널의 기준 캔버스를 잡는다.
            var ordered = new List<string> { "albedo" };
            foreach (var key in channels.Keys) if (key != "albedo") ordered.Add(key);

            foreach (var channel in ordered)
            {
                if (!channels.TryGetValue(channel, out var rawPath)) continue;
                string rel = rawPath as string;
                if (string.IsNullOrEmpty(rel))
                {
                    report.Add(ArtIssueLevel.Error, assetId, null, $"channels.{channel} 경로가 문자열이 아니다");
                    continue;
                }

                if (!ApprovedArtContract.IsKnownChannel(channel))
                    report.Add(ArtIssueLevel.Warning, assetId, rel,
                        $"알 수 없는 채널 이름 '{channel}' — " +
                        $"{string.Join("/", ApprovedArtContract.Channels)} 만 임포트한다");

                // approved/ 밖의 파일을 채널로 적었으면 임포트 대상이 아니다.
                if (!rel.Replace('\\', '/').StartsWith("approved/"))
                {
                    report.Add(ArtIssueLevel.Error, assetId, rel,
                        "승인 자산의 채널이 approved/ 밖을 가리킨다 — source/working/concept 는 임포트하지 않는다");
                    continue;
                }

                string fileName = Path.GetFileName(rel);
                if (!ApprovedArtContract.IsValidFileName(fileName, out string nameChannel, out string reason))
                    report.Add(ArtIssueLevel.Error, assetId, rel, $"파일명 규칙 위반: {reason}");
                else if (nameChannel != channel)
                    report.Add(ArtIssueLevel.Error, assetId, rel,
                        $"파일명 접미어 '{nameChannel}' 가 channels 키 '{channel}' 와 다르다");

                string full = Path.Combine(packageRoot, rel);
                var info = PngInfo.Read(full);
                if (!info.Valid)
                {
                    report.Add(ArtIssueLevel.Error, assetId, rel, $"PNG 를 읽지 못했다: {info.Error}");
                    continue;
                }

                if (channel == "albedo")
                {
                    albedoInfo = info;
                    albedoSeen = true;
                    asset.Width = info.Width;
                    asset.Height = info.Height;

                    // 알파는 실루엣이 필요한 자산에만 필수다. 셀을 꽉 채우는 불투명 바닥·벽
                    // 타일은 알파 채널이 없어도 정상이며, 이를 오류로 막으면 재수출만 강요한다.
                    if (!info.HasAlpha)
                    {
                        if (ApprovedArtContract.RequiresSilhouetteAlpha(asset.Slot))
                            report.Add(ArtIssueLevel.Error, assetId, rel,
                                "Albedo 에 알파 채널이 없다 — 이 자산은 실루엣이 필요하다(아트 규격 §5)");
                        else
                            report.Add(ArtIssueLevel.Info, assetId, rel,
                                "Albedo 에 알파 채널이 없다 — 셀을 꽉 채우는 타일이라 문제가 아니다");
                    }
                    else if (ApprovedArtContract.RequiresSilhouetteAlpha(asset.Slot))
                    {
                        // 알파 채널이 있는 것만으로는 부족하다 — 배경이 실제로 지워졌는지 본다.
                        var alpha = PngAlphaStats.Read(full);
                        if (!alpha.Valid)
                            report.Add(ArtIssueLevel.Info, assetId, rel,
                                "알파 분포를 읽지 못했다 — " + alpha.Error);
                        else if (ApprovedArtContract.BackgroundResidueSuspect(
                                     alpha.OpaqueFraction, alpha.PartialFraction))
                        {
                            asset.HasError = true;
                            report.Add(ArtIssueLevel.Error, assetId, rel,
                                $"배경 잔여물 — 반투명 픽셀이 {alpha.PartialFraction:P1} 다" +
                                $"(불투명 {alpha.OpaqueFraction:P1} · 투명 {alpha.ZeroFraction:P1}). " +
                                "부드러운 가장자리는 3% 안쪽이다. 배경 제거가 덜 된 상태로, " +
                                "화면에서는 소품 뒤에 창백한 사각형으로 보인다. 재키잉이 필요하다");
                        }
                    }

                    if (!ApprovedArtContract.CanvasMatchesFootprint(info.Width, info.Height,
                            asset.FootprintCols, asset.FootprintRows, asset.VisualHeightCells,
                            asset.Slot, out string canvasReason))
                        report.Add(ArtIssueLevel.Error, assetId, rel, $"캔버스와 footprint 불일치: {canvasReason}");
                    // VFX 오버레이는 반복 재생되므로 패딩을 넣으면 이음새가 보인다 — 타일과 같다.
                    else if (!tiling && !asset.IsVfx && !ApprovedArtContract.HasPadding(info.Width, info.Height,
                                 asset.FootprintCols, asset.FootprintRows, asset.VisualHeightCells,
                                 ApprovedArtContract.MinPaddingPixels))
                        report.Add(ArtIssueLevel.Warning, assetId, rel,
                            $"비타일 자산에 알파 패딩이 없다 — Bloom·필터링에서 가장자리가 잘릴 수 있다" +
                            $"(권장 {ApprovedArtContract.MinPaddingPixels}px, Emission {ApprovedArtContract.MinEmissionPaddingPixels}px)");

                    // 피벗 — manifest 에 없으면 슬롯에서 유추하고 경고를 남긴다.
                    if (havePivot)
                    {
                        if (!ApprovedArtContract.PivotIsValid(asset.PivotX, asset.PivotY,
                                info.Width, info.Height, out string pivotReason))
                            report.Add(ArtIssueLevel.Error, assetId, rel, pivotReason);
                    }
                    else
                    {
                        ApprovedArtContract.DefaultPivotPixels(asset.Slot, info.Width, info.Height,
                            out int px2, out int py2);
                        asset.PivotX = px2;
                        asset.PivotY = py2;
                    }
                }
                else if (albedoSeen)
                {
                    // 모든 채널은 같은 캔버스여야 한다(아트 규격 §5).
                    if (info.Width != albedoInfo.Width || info.Height != albedoInfo.Height)
                        report.Add(ArtIssueLevel.Error, assetId, rel,
                            $"캔버스 {info.Width}×{info.Height} 가 Albedo " +
                            $"{albedoInfo.Width}×{albedoInfo.Height} 와 다르다");

                    // Normal·Mask·AO 는 Linear 데이터 맵이다. PNG 의 sRGB 청크는
                    // 임포트 기본값을 sRGB 로 밀어붙이는 신호라 참고로 알린다.
                    if (ApprovedArtContract.IsLinearChannel(channel) && info.HasSrgbChunk)
                        report.Add(ArtIssueLevel.Info, assetId, rel,
                            $"'{channel}' PNG 에 sRGB 청크가 있다 — 임포터가 sRGB 를 끄므로 그대로 두면 된다");

                    if (channel == "normal" && info.ColorType == 3)
                        report.Add(ArtIssueLevel.Error, assetId, rel,
                            "Normal 이 팔레트 PNG 다 — 손실 없이 방향을 담을 수 없다");
                }
                else
                {
                    report.Add(ArtIssueLevel.Error, assetId, rel,
                        "Albedo 채널이 없어 캔버스 정합성을 확인할 수 없다");
                }

                asset.Channels[channel] = rel;
            }

            if (!albedoSeen)
            {
                report.Add(ArtIssueLevel.Error, assetId, null, "channels.albedo 가 없다");
                return;
            }

            // ── 슬롯 결과 보고
            if (asset.Slot == ApprovedArtContract.KitSlot.None)
                report.Add(ArtIssueLevel.Info, assetId, null,
                    "EnvironmentKit 타일 슬롯이 아니다 — 세트피스·소품·VFX 는 SetPieceCatalog 배치에서 붙인다");

            // ── 정렬 레이어. 타일 슬롯에서는 렌더러가 표면 토폴로지로 레이어를 정하므로
            // 힌트가 없어도 문제가 아니다(같은 cap 이 북쪽 개방 여부에 따라 레이어가 달라진다).
            if (string.IsNullOrEmpty(hint))
            {
                asset.SortingLayer = ApprovedArtContract.DefaultLayerForSlot(asset.Slot);
                report.Add(ArtIssueLevel.Info, assetId, null,
                    tiling
                        ? $"sortingLayerHint 가 없다 — 타일 슬롯이라 렌더러가 레이어를 정한다(참고값 '{asset.SortingLayer}')"
                        : $"sortingLayerHint 가 없다 — 슬롯에서 '{asset.SortingLayer}' 로 유추한다");
            }
            else
            {
                asset.SortingLayer = ApprovedArtContract.SortingLayerForHint(hint, out var hintMatch);
                if (hintMatch == ApprovedArtContract.HintMatch.Alias)
                    report.Add(ArtIssueLevel.Info, assetId, null,
                        $"sortingLayerHint '{hint}' 은 아트 쪽 별칭이다 — §6.4 의 '{asset.SortingLayer}' 로 옮긴다");
                else if (hintMatch == ApprovedArtContract.HintMatch.Unknown)
                    report.Add(ArtIssueLevel.Warning, assetId, null,
                        $"sortingLayerHint '{hint}' 을 §6.4 레이어로 매핑할 수 없다 — '{asset.SortingLayer}' 로 떨어진다");
            }

            // ── 시각 높이
            //
            // 값이 없는 것을 곧바로 결함으로 보지 않는다. 승인 패키지는 피벗 위치로 자산
            // 성격을 가른다 — 가운데 피벗은 바닥에 눕는 평면 자산(바닥·레일·cap·VFX)이라
            // 시각 높이가 뜻이 없고, 아래 피벗(발점)은 솟는 자산이라 pivotY 가 곧 높이다.
            // 그러니 되묻지 않고 여기서 복원한다.
            if (!haveVisualHeight && !asset.IsVfx && ApprovedArtContract.VisualHeightMatters(asset.Slot))
            {
                bool foot = asset.Height > 0 && ApprovedArtContract.PivotIsFootPoint(
                    asset.Height, asset.PivotY, ApprovedArtContract.DeliveryPixelsPerCell);
                if (foot)
                {
                    asset.VisualHeightCells = ApprovedArtContract.VisualHeightFromPivot(
                        asset.PivotY, ApprovedArtContract.DeliveryPixelsPerCell);
                    report.Add(ArtIssueLevel.Info, assetId, null,
                        $"visualHeightCells 가 없다 — 발점 피벗에서 {asset.VisualHeightCells:0.###}셀로 복원했다");
                }
                else if (asset.Height > 0)
                {
                    // 가운데 피벗이면 평면 자산이다 — 값이 없는 것이 정상이다.
                    report.Add(ArtIssueLevel.Info, assetId, null,
                        "visualHeightCells 가 없다 — 피벗이 캔버스 중앙이라 바닥에 눕는 평면 자산으로 본다");
                }
            }

            // ── 전경 페이드 그룹
            //
            // 승인 패키지는 그룹 이름 대신 foregroundOccluder(bool) + fadeMaskPath 를 준다.
            // 전경 소품은 서로 떨어져 있어 자산마다 독립 그룹이 맞다 — 한 그룹으로 묶으면
            // 하나에 닿았을 때 넷이 함께 사라진다. 그래서 assetId 를 그룹으로 쓴다.
            // §6.6 이 그룹을 요구하는 취지는 "벽 덩어리가 조각조각 사라지지 않는 것" 이고,
            // 그건 벽 타일맵 청크가 이미 담당한다.
            if ((asset.ForegroundOccluder || asset.SortingLayer == "FrontStructure")
                && string.IsNullOrEmpty(asset.OccluderGroup))
            {
                asset.OccluderGroup = assetId;
                report.Add(ArtIssueLevel.Info, assetId, null,
                    "occluderGroup 이 없다 — 전경 소품은 독립 페이드가 맞으므로 assetId 를 그룹으로 쓴다(§6.6)");
            }

            // ── 조명 소켓
            ParseLightSockets(map, asset, report);

            asset.ReplacementAssetId = MiniJson.GetString(map, "replacementAssetId");

            // ── 그림자 캐스터 윤곽
            //
            // 승인 패키지는 이미지 경로가 아니라 셀 좌표 다각형으로 준다
            // (shadowCasterFootprintCells, footprint 좌하단 원점). 그게 더 낫다 —
            // 이미지를 다시 윤곽 추적할 필요가 없고 정수 셀이라 결정적이다.
            string casterPath = MiniJson.GetString(map, "shadowCasterPath");
            asset.ShadowCasterPath = casterPath;

            var casterCells = MiniJson.GetFloatPairs(map, "shadowCasterFootprintCells");
            if (casterCells != null)
            {
                asset.ShadowContourCells =
                    ApprovedArtContract.CasterContourToFootPoint(casterCells, asset.FootprintCols);
                if (asset.ShadowContourCells == null)
                    report.Add(ArtIssueLevel.Warning, assetId, null,
                        "shadowCasterFootprintCells 의 점이 3개 미만이다 — footprint 사각형으로 떨어진다");
            }
            else if (asset.VisualHeightCells >= 1.5f && string.IsNullOrEmpty(casterPath)
                     && asset.FootprintCols * asset.FootprintRows > 1)
                // 1×1 자산은 제외한다. 한 셀 footprint 에서는 사각형이 곧 정확한 윤곽이라
                // 아트가 따로 줄 수 있는 것이 없다 — 높이만 보고 경고하면 상자·양동이까지
                // 전부 걸린다. 실제로 여러 셀 자산(아치 5×1·기둥·드릴)은 모두 윤곽을 넣어 왔다.
                report.Add(ArtIssueLevel.Warning, assetId, null,
                    $"footprint {asset.FootprintCols}×{asset.FootprintRows} · 시각 높이 " +
                    $"{asset.VisualHeightCells:0.##}셀인데 캐스터 윤곽이 없다 — " +
                    "여러 셀 세트피스는 별도 윤곽이 필요하다(§7.4). footprint 사각형으로 떨어진다");

            report.Approved.Add(asset);
        }
    }
}
