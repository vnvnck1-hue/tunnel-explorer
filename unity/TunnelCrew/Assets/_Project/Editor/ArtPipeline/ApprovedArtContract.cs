using System.Text;

namespace TunnelCrew.EditorTools.ArtPipeline
{
    /// <summary>
    /// Codex 아트 트랙과 Claude 구현 트랙 사이의 파일 계약
    /// (docs/test-room-art-production-spec.md §3·§4·§5·§6·§7·§10).
    ///
    /// <b>순수 함수만 둔다.</b> 파일 시스템도 Unity 임포터도 건드리지 않으므로 규칙 자체를
    /// EditMode 테스트로 고정할 수 있다.
    /// </summary>
    public static class ApprovedArtContract
    {
        /// <summary>납품 밀도. 1셀 footprint = 128×128px, Unity PPU 128(아트 규격 §3).</summary>
        public const int DeliveryPixelsPerCell = 128;
        /// <summary>원화 최소 밀도. 납품은 여기서 축소한다.</summary>
        public const int MinSourcePixelsPerCell = 256;
        /// <summary>비타일링 자산의 기본 안전 패딩(px).</summary>
        public const int MinPaddingPixels = 8;
        /// <summary>Bloom 이 강한 Emission 의 패딩(px).</summary>
        public const int MinEmissionPaddingPixels = 16;

        /// <summary>이 패키지의 파일명 접두어.</summary>
        public const string FilePrefix = "tr01_";

        /// <summary>승인 상태. <c>approved</c> 만 Unity 로 임포트한다(§8-10).</summary>
        public const string StatusApproved = "approved";

        /// <summary>채널 접미어(아트 규격 §4).</summary>
        public static readonly string[] Channels = { "albedo", "normal", "emission", "mask", "ao" };

        /// <summary>색 공간이 Linear 여야 하는 데이터 맵. sRGB 로 임포트되면 값이 뒤틀린다(§5).</summary>
        public static readonly string[] LinearChannels = { "normal", "emission", "mask", "ao" };

        public static bool IsLinearChannel(string channel)
        {
            for (int i = 0; i < LinearChannels.Length; i++)
                if (LinearChannels[i] == channel) return true;
            return false;
        }

        public static bool IsKnownChannel(string channel)
        {
            for (int i = 0; i < Channels.Length; i++)
                if (Channels[i] == channel) return true;
            return false;
        }

        // ───────────────────────────── 파일명

        /// <summary>
        /// <c>tr01_&lt;category&gt;_&lt;name&gt;_&lt;variant&gt;_&lt;channel&gt;.png</c> 규칙을 검사한다.
        /// 소문자 영문·숫자·밑줄만 쓴다(아트 규격 §4).
        /// </summary>
        public static bool IsValidFileName(string fileName, out string channel, out string reason)
        {
            channel = null;
            reason = null;

            if (string.IsNullOrEmpty(fileName)) { reason = "파일명이 없다"; return false; }
            if (!fileName.EndsWith(".png")) { reason = "확장자가 .png 가 아니다"; return false; }

            string stem = fileName.Substring(0, fileName.Length - 4);

            foreach (char c in stem)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
                if (!ok)
                {
                    reason = $"허용되지 않은 문자 '{c}' — 소문자 영문·숫자·밑줄만 쓴다";
                    return false;
                }
            }

            if (!stem.StartsWith(FilePrefix))
            {
                reason = $"'{FilePrefix}' 로 시작해야 한다";
                return false;
            }

            var parts = stem.Split('_');
            // tr01 / category / name / variant / channel — 최소 5조각
            if (parts.Length < 5)
            {
                reason = "tr01_<category>_<name>_<variant>_<channel> 형식이 아니다";
                return false;
            }

            channel = parts[parts.Length - 1];
            if (IsKnownChannel(channel)) return true;

            // 개념 보드·배치 보드는 채널 대신 _concept · _layout 을 쓴다.
            if (channel == "concept" || channel == "layout") return true;

            reason = $"마지막 조각 '{channel}' 이 채널 접미어가 아니다 " +
                     $"({string.Join(", ", Channels)}, concept, layout)";
            channel = null;
            return false;
        }

        /// <summary>파일명에서 카테고리 조각을 뽑는다(tr01 다음 조각).</summary>
        public static string CategoryOf(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            string stem = fileName.EndsWith(".png") ? fileName.Substring(0, fileName.Length - 4) : fileName;
            if (!stem.StartsWith(FilePrefix)) return null;
            var parts = stem.Split('_');
            return parts.Length >= 2 ? parts[1] : null;
        }

        // ───────────────────────────── EnvironmentKit 슬롯

        /// <summary>승인 아트가 들어갈 <c>EnvironmentKit</c> 슬롯.</summary>
        public enum KitSlot
        {
            /// <summary>이 배치에서 연결하지 않는다 — 세트피스·소품·VFX 는 SetPieceCatalog 배치로 미룬다.</summary>
            None = 0,
            FloorBase,
            FloorEdge,
            ContactAo,
            WallTop,
            WallTopRim,
            WallFront,
            WestSide,
            EastSide,
            OuterCorner,
            InnerCorner,
        }

        /// <summary>
        /// <c>assetId</c> 에서 슬롯을 정한다. <b>이것이 1순위 기준점이다.</b>
        ///
        /// 아트 규격 §7 이 ID 범위를 FLR·WTP·WFR·ARC·PIL·LIN·LGT·DEC·HERO·FGV·VFX 로
        /// 정의했으므로 <c>TR01-&lt;TOKEN&gt;-...</c> 의 토큰이 가장 안정적인 신호다.
        /// 파일명은 <c>tr01_wall_front_a</c> 처럼 카테고리가 두 조각으로 쪼개져 위치만으로는
        /// 구분되지 않는다(실제로 그 때문에 WTP·WFR·AO 자산이 전부 <see cref="KitSlot.None"/>
        /// 으로 떨어져 연결되지 않았다).
        /// </summary>
        public static KitSlot SlotForAssetId(string assetId)
        {
            if (string.IsNullOrEmpty(assetId)) return KitSlot.None;

            string id = assetId.ToUpperInvariant().Replace('_', '-');
            var parts = id.Split('-');
            if (parts.Length < 2) return KitSlot.None;

            string token = parts[1];
            bool Has(string s) => id.Contains(s);

            switch (token)
            {
                case "FLR":
                case "FLOOR":
                    if (Has("-EDGE")) return KitSlot.FloorEdge;
                    return KitSlot.FloorBase;

                case "WTP":
                case "WALLTOP":
                    if (Has("-RIM")) return KitSlot.WallTopRim;
                    if (Has("-INNER")) return KitSlot.InnerCorner;
                    if (Has("-OUTER") || Has("-CORNER")) return KitSlot.OuterCorner;
                    return KitSlot.WallTop;

                case "WFR":
                case "WALLFRONT":
                    if (Has("-WEST")) return KitSlot.WestSide;
                    if (Has("-EAST")) return KitSlot.EastSide;
                    return KitSlot.WallFront;

                // AO-CONTACT / AO-... — 바닥-벽 접합 AO 데칼
                case "AO":
                    return KitSlot.ContactAo;

                default:
                    return KitSlot.None;
            }
        }

        /// <summary>
        /// 파일명에서 슬롯을 정한다. <see cref="SlotForAssetId"/> 가 결론을 내지 못했을 때의
        /// 보조 수단이다.
        ///
        /// 위치가 아니라 <b>줄기 문자열 포함</b>으로 판단한다 — 카테고리가
        /// <c>wall_front</c>·<c>wall_top_rim</c>·<c>contact_ao</c> 처럼 여러 조각인 경우가 있다.
        /// </summary>
        public static KitSlot SlotFor(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return KitSlot.None;

            string stem = fileName.EndsWith(".png")
                ? fileName.Substring(0, fileName.Length - 4)
                : fileName;
            stem = stem.ToLowerInvariant();

            // 긴 이름을 먼저 본다 — wall_top_rim 이 wall_top 보다 앞이어야 한다.
            if (stem.Contains("_contact_ao") || stem.Contains("_ao_contact")) return KitSlot.ContactAo;

            if (stem.Contains("_wall_top") || stem.Contains("_walltop") || stem.Contains("_wtp"))
            {
                if (stem.Contains("_rim")) return KitSlot.WallTopRim;
                if (stem.Contains("_inner")) return KitSlot.InnerCorner;
                if (stem.Contains("_outer") || stem.Contains("_corner")) return KitSlot.OuterCorner;
                return KitSlot.WallTop;
            }

            if (stem.Contains("_wall_front") || stem.Contains("_wallfront") || stem.Contains("_wfr"))
            {
                if (stem.Contains("_west")) return KitSlot.WestSide;
                if (stem.Contains("_east")) return KitSlot.EastSide;
                return KitSlot.WallFront;
            }

            if (stem.Contains("_floor") || stem.Contains("_flr"))
            {
                if (stem.Contains("_edge")) return KitSlot.FloorEdge;
                return KitSlot.FloorBase;
            }

            return KitSlot.None;
        }

        /// <summary>assetId 를 먼저 보고, 결론이 없으면 파일명으로 넘어간다.</summary>
        public static KitSlot ResolveSlot(string assetId, string fileName)
        {
            var slot = SlotForAssetId(assetId);
            return slot != KitSlot.None ? slot : SlotFor(fileName);
        }

        /// <summary>
        /// 이 슬롯이 Tilemap 에 깔리는 <b>타일링</b> 자산인가.
        ///
        /// 타일링 자산은 캔버스가 셀 크기와 <b>정확히</b> 맞아야 한다. 패딩을 넣으면 인접
        /// 타일 사이에 이음새가 생긴다 — 패딩은 알파 여백이 필요한 아틀라스 자산의 규칙이고
        /// 타일에 적용하면 오히려 깨진다.
        /// </summary>
        public static bool IsTilingSlot(KitSlot slot) => slot != KitSlot.None;

        /// <summary>
        /// 이 슬롯이 Albedo 알파로 실루엣을 만들어야 하는가.
        ///
        /// 셀을 꽉 채우는 불투명 바닥·벽 타일은 알파 채널이 없어도 정상이다. 반면 접촉 AO
        /// 데칼과 세트피스·소품·전경은 알파가 없으면 형태를 만들 수 없다.
        /// </summary>
        public static bool RequiresSilhouetteAlpha(KitSlot slot)
        {
            switch (slot)
            {
                case KitSlot.FloorBase:
                case KitSlot.FloorEdge:
                case KitSlot.WallTop:
                case KitSlot.WallTopRim:
                case KitSlot.WallFront:
                case KitSlot.WestSide:
                case KitSlot.EastSide:
                    return false;   // 셀을 꽉 채우는 불투명 타일이어도 된다

                case KitSlot.ContactAo:
                case KitSlot.OuterCorner:
                case KitSlot.InnerCorner:
                    return true;    // 부분만 덮는 데칼·조각

                default:
                    return true;    // 세트피스·소품·VFX
            }
        }

        /// <summary>
        /// VFX 오버레이 자산인가(§11.2).
        ///
        /// 안개·광선·먼지는 화면에 겹치는 오버레이 텍스처다. 캔버스를 꽉 채우고 반복
        /// 재생되므로 <b>알파 패딩을 넣으면 반복 이음새가 보인다</b> — 타일과 같은 이유로
        /// 패딩 요구에서 빼야 한다. 발점·시각 높이·EnvironmentKit 슬롯도 해당이 없다.
        /// </summary>
        public static bool IsVfx(string assetId)
            => !string.IsNullOrEmpty(assetId) && assetId.IndexOf("-VFX-", System.StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>
        /// 피벗이 캔버스 아래에 붙어 있는가 — 즉 <b>발점</b>인가.
        ///
        /// 승인 패키지는 이 하나로 자산 성격을 가른다. 가운데 피벗은 바닥에 눕는 평면
        /// 자산(바닥·레일·벽 상단 cap·VFX)이고, 아래 피벗은 지면에서 솟는 자산이다.
        /// 그래서 <c>visualHeightCells</c> 가 없는 것이 결함인지 정상인지도 여기서 갈린다.
        /// </summary>
        public static bool PivotIsFootPoint(int canvasHeight, int pivotY, int pixelsPerCell)
        {
            if (canvasHeight <= 0) return false;
            // 아트 규격은 발점 위 8px 여백을 둔다. 셀의 1/8 안쪽이면 발점으로 본다.
            int slack = System.Math.Max(4, pixelsPerCell / 8);
            return canvasHeight - pivotY <= slack;
        }

        /// <summary>
        /// 발점 피벗에서 시각 높이를 유도한다.
        ///
        /// 피벗 좌표계는 좌상단 원점·Y 아래로 증가(<c>pivotPixelsCoordinateSystem</c>)이므로
        /// <c>pivotY</c> 가 곧 <b>발점 위쪽 픽셀 수</b> 다. 그것을 셀로 나누면 시각 높이다.
        ///
        /// manifest 가 값을 생략했을 때 아트에 되묻지 않고 여기서 정확히 복원한다 —
        /// 실제 승인 패키지가 솟는 자산 14종에서 이 값을 생략했고, 전부 피벗으로 복원된다.
        /// </summary>
        public static float VisualHeightFromPivot(int pivotY, int pixelsPerCell)
            => pixelsPerCell <= 0 ? 0f : (float)pivotY / pixelsPerCell;

        /// <summary>
        /// manifest 의 <c>shadowCasterFootprintCells</c> 를 발점 기준 윤곽으로 옮긴다.
        ///
        /// 아트는 footprint <b>좌하단 원점</b>으로 준다(아치 5×1 → [0,0] [5,0] [5,1] [0,1]).
        /// 런타임 <see cref="TunnelCrew.Presentation.Visual.SetPieceDef.shadowContourCells"/> 는
        /// <b>발점(아래 변 중앙)</b> 기준이므로 X 에서 폭의 절반을 뺀다. Y 는 그대로다.
        /// 결과 규약은 <see cref="FootprintContourCells"/> 와 같다.
        /// </summary>
        public static float[] CasterContourToFootPoint(float[] flatCells, int footprintCols)
        {
            if (flatCells == null || flatCells.Length < 6 || flatCells.Length % 2 != 0) return null;
            float halfWidth = System.Math.Max(1, footprintCols) * 0.5f;
            var outp = new float[flatCells.Length];
            for (int i = 0; i < flatCells.Length; i += 2)
            {
                outp[i] = flatCells[i] - halfWidth;
                outp[i + 1] = flatCells[i + 1];
            }
            return outp;
        }

        /// <summary>
        /// 반투명 픽셀이 이 비율을 넘으면 배경 잔여물로 본다.
        ///
        /// 실측으로 정한 값이다. 승인 패키지 revision 16 에서 깨끗한 자산은 반투명이
        /// 0.9~3.2% 였고(부드러운 가장자리 한 겹), 배경 키잉이 실패한 자산은 25.8~46.2%
        /// 였다. 그 사이가 크게 비어 있어 10% 는 어느 쪽에도 가깝지 않다.
        /// </summary>
        public const float MaxPartialAlphaFraction = 0.10f;

        /// <summary>
        /// 잔여물 판정에 필요한 최소 불투명 비율.
        ///
        /// 접촉 AO 데칼처럼 <b>부드러운 그라디언트만</b> 있는 자산은 반투명이 41% 여도
        /// 정상이다(불투명 0%). 불투명한 본체가 있으면서 반투명이 넓게 깔린 것이
        /// 배경 제거 실패의 형태다.
        /// </summary>
        public const float MinOpaqueFractionForResidue = 0.05f;

        /// <summary>
        /// 알베도에 배경 잔여물이 남아 있는가(§12.2).
        ///
        /// 알파 채널이 <b>있는지</b> 만 봐서는 잡히지 않는 결함이다. 배경 제거가 실패하면
        /// 알파 17~159 의 옅은 막이 캔버스 전체에 남고, 화면에서는 소품 뒤에 창백한
        /// 사각형으로 나타난다. 실제로 소품 9종이 그 상태로 왔다.
        /// </summary>
        public static bool BackgroundResidueSuspect(float opaqueFraction, float partialFraction)
            => opaqueFraction >= MinOpaqueFractionForResidue
               && partialFraction > MaxPartialAlphaFraction;

        /// <summary>
        /// 이 슬롯에서 <c>visualHeightCells</c> 가 뜻을 갖는가.
        /// 바닥과 바닥 데칼은 솟지 않으므로 값이 없어도 문제가 아니다.
        /// </summary>
        public static bool VisualHeightMatters(KitSlot slot)
        {
            switch (slot)
            {
                case KitSlot.FloorBase:
                case KitSlot.FloorEdge:
                case KitSlot.ContactAo:
                    return false;
                default:
                    return true;
            }
        }

        // ───────────────────────────── 캔버스와 피벗

        /// <summary>
        /// footprint 와 캔버스 크기가 맞는가. 슬롯에서 규칙을 유도하는 편의 오버로드다.
        /// </summary>
        public static bool CanvasMatchesFootprint(int width, int height,
            int footprintCols, int footprintRows, float visualHeightCells,
            KitSlot slot, out string reason)
            => CanvasMatchesFootprint(width, height, footprintCols, footprintRows, visualHeightCells,
                IsTilingSlot(slot), VisualHeightMatters(slot), out reason);

        /// <summary>
        /// footprint 와 캔버스 크기가 맞는가.
        ///
        /// <paramref name="tiling"/> 이 참이면 <b>정확히</b> 일치해야 한다(패딩 금지 — 이음새).
        /// 거짓이면 이상이면 되고 패딩을 허용한다.
        ///
        /// <paramref name="heightFromVisualHeight"/> 는 세로 기준을 무엇으로 볼지 정한다.
        /// <b>footprint 깊이와 스프라이트 높이는 다른 축이다.</b> 벽 정면은 지면에서 1셀
        /// 경계에 서 있어도(footprint 1) 그려지는 높이가 0.75셀(96px)일 수 있다. 두 값을
        /// <c>max</c> 로 뭉개면 그 자산을 거부하게 된다.
        /// </summary>
        public static bool CanvasMatchesFootprint(int width, int height,
            int footprintCols, int footprintRows, float visualHeightCells,
            bool tiling, bool heightFromVisualHeight, out string reason)
        {
            reason = null;

            int cell = DeliveryPixelsPerCell;
            int wantWidth = footprintCols * cell;

            float cellsTall;
            if (tiling)
            {
                // 솟는 자산은 선언한 시각 높이가 곧 스프라이트 높이다.
                // 바닥·데칼은 시각 높이가 뜻이 없으므로 footprint 깊이를 쓴다.
                cellsTall = heightFromVisualHeight && visualHeightCells > 0f
                    ? visualHeightCells
                    : footprintRows;
            }
            else
            {
                // 세트피스는 footprint 도 담고 솟은 부분도 담아야 한다.
                cellsTall = visualHeightCells > footprintRows ? visualHeightCells : footprintRows;
            }
            if (cellsTall <= 0f) cellsTall = footprintRows > 0 ? footprintRows : 1;

            if (tiling)
            {
                if (width != wantWidth)
                {
                    reason = $"타일링 자산의 가로가 {width}px 다 — footprint {footprintCols}셀이면 " +
                             $"정확히 {wantWidth}px 여야 한다(패딩을 넣으면 타일 이음새가 생긴다)";
                    return false;
                }

                // 세로는 "셀 배수" 가 아니라 "셀 높이 × 128 과 정확히 일치" 다.
                // §8.6 의 벽 정면 높이 0.75~1.5셀은 96px·192px 이고 둘 다 128 의 배수가 아니다.
                int wantExactHeight = (int)System.Math.Round(cellsTall * cell);
                if (height != wantExactHeight)
                {
                    reason = $"타일링 자산의 세로가 {height}px 다 — 높이 {cellsTall:0.##}셀이면 " +
                             $"정확히 {wantExactHeight}px 여야 한다(패딩을 넣으면 타일 이음새가 생긴다)";
                    return false;
                }
                return true;
            }

            if (width < wantWidth)
            {
                reason = $"가로 {width}px 이 footprint {footprintCols}셀 = {wantWidth}px 보다 작다";
                return false;
            }

            int wantHeight = (int)System.Math.Ceiling(cellsTall * cell);
            if (height < wantHeight)
            {
                reason = $"세로 {height}px 이 높이 {cellsTall:0.##}셀 = {wantHeight}px 보다 작다";
                return false;
            }

            return true;
        }

        /// <summary>비타일링 자산이 패딩을 확보했는가. 없으면 경고 대상이다(오류는 아니다).</summary>
        public static bool HasPadding(int width, int height,
            int footprintCols, int footprintRows, float visualHeightCells, int padding)
        {
            int cell = DeliveryPixelsPerCell;
            float cellsTall = visualHeightCells > footprintRows ? visualHeightCells : footprintRows;
            if (cellsTall <= 0f) cellsTall = footprintRows > 0 ? footprintRows : 1;

            return width >= footprintCols * cell + padding * 2
                && height >= (int)System.Math.Ceiling(cellsTall * cell) + padding * 2;
        }

        /// <summary>
        /// 피벗이 정수 픽셀인가(아트 규격 §3 "모든 자산은 정수 픽셀 캔버스와 정수 픽셀 피벗을 사용한다").
        /// manifest 는 픽셀 정수로 적으므로, 여기서는 범위와 부호만 본다.
        /// </summary>
        public static bool PivotIsValid(int pivotX, int pivotY, int width, int height, out string reason)
        {
            reason = null;
            if (pivotX < 0 || pivotY < 0)
            {
                reason = $"피벗 ({pivotX},{pivotY}) 에 음수가 있다";
                return false;
            }
            if (pivotX > width || pivotY > height)
            {
                reason = $"피벗 ({pivotX},{pivotY}) 이 캔버스 {width}×{height} 밖이다";
                return false;
            }
            return true;
        }

        /// <summary>
        /// manifest 의 픽셀 피벗 → Unity 임포터의 정규화 피벗.
        ///
        /// <b>좌표계 계약</b>(아트 규격 §10 이 "구현 담당이 정하고 문서에 역기록" 하라고 한 부분):
        /// manifest 의 <c>pivotPixels</c> 는 <b>이미지 좌상단 원점, Y 아래로 증가</b>다.
        /// PNG 픽셀 순서와 같아 아트 도구에서 읽은 값을 그대로 적을 수 있기 때문이다.
        /// Unity 스프라이트 피벗은 <b>좌하단 원점, Y 위로 증가, 0~1 정규화</b>다.
        /// 따라서 Y 를 뒤집는다.
        /// </summary>
        public static void PivotToUnity(int pivotX, int pivotY, int width, int height,
            out float u, out float v)
        {
            u = width > 0 ? pivotX / (float)width : 0.5f;
            v = height > 0 ? 1f - pivotY / (float)height : 0f;
        }

        /// <summary>
        /// <c>pivotPixels</c> 가 없을 때 슬롯에서 유추하는 기본 피벗(좌상단 원점, Y 아래로 증가).
        ///
        /// 아트 규격 §6: 바닥은 footprint 사각형의 기하학적 중심, 벽·기둥·문·소품은 지면과
        /// 닿는 실루엣의 중앙 하단(발점). 임포터가 이 값을 쓸 때는 반드시 경고를 남긴다 —
        /// 추측이지 계약이 아니다.
        /// </summary>
        public static void DefaultPivotPixels(KitSlot slot, int width, int height,
            out int pivotX, out int pivotY)
        {
            pivotX = width / 2;

            switch (slot)
            {
                case KitSlot.FloorBase:
                case KitSlot.FloorEdge:
                case KitSlot.ContactAo:
                    pivotY = height / 2;    // 바닥·데칼은 중심
                    break;
                default:
                    pivotY = height;        // 벽·구조물은 하단(발점)
                    break;
            }
        }

        // ───────────────────────────── 세트피스 좌표

        /// <summary>
        /// manifest 의 <c>lightSockets[].pixel</c> → 발점 기준 셀 오프셋.
        ///
        /// 소켓 픽셀은 <c>pivotPixels</c> 와 같은 좌표계다(이미지 좌상단 원점, Y 아래로 증가).
        /// 런타임은 발점을 원점으로 하고 화면 위가 +Y 이므로 Y 를 뒤집는다.
        ///
        /// 예: 작업등 256×256, 피벗 [128,248], 소켓 [128,82]
        ///   → (0, (248-82)/128) = (0, 1.297셀) — 램프 머리가 발점에서 1.3셀 위.
        /// </summary>
        public static void SocketOffsetCells(int socketX, int socketY, int pivotX, int pivotY,
            out float offsetX, out float offsetY)
        {
            offsetX = (socketX - pivotX) / (float)DeliveryPixelsPerCell;
            offsetY = (pivotY - socketY) / (float)DeliveryPixelsPerCell;
        }

        /// <summary>
        /// footprint 사각형에서 만든 그림자 캐스터 윤곽. 발점을 원점으로 한 셀 좌표다.
        ///
        /// <c>shadowCasterPath</c> 가 없을 때의 대체값이다(§7.4 는 별도 윤곽을 요구하고
        /// 검사기가 경고를 남긴다). 감는 방향은 <see cref="WallContourTracer"/> 와 같은
        /// 규약 — 반시계, 즉 구조물이 진행 방향의 왼쪽이다.
        ///
        /// 발점은 footprint 의 <b>아래 변 중앙</b>으로 본다(아트 규격 §6: 벽·기둥·문·소품의
        /// 피벗은 지면과 닿는 실루엣의 중앙 하단).
        /// </summary>
        public static float[] FootprintContourCells(int footprintCols, int footprintRows)
        {
            if (footprintCols < 1) footprintCols = 1;
            if (footprintRows < 1) footprintRows = 1;

            float halfW = footprintCols * 0.5f;
            float depth = footprintRows;

            // 반시계: (좌하) → (우하) → (우상) → (좌상)
            return new[]
            {
                -halfW, 0f,
                 halfW, 0f,
                 halfW, depth,
                -halfW, depth,
            };
        }

        // ───────────────────────────── 정렬 힌트

        /// <summary>정렬 힌트가 §6.4 레이어로 어떻게 이어졌는가.</summary>
        public enum HintMatch
        {
            /// <summary>§6.4 표의 이름을 그대로 썼다.</summary>
            Exact = 0,
            /// <summary>아트 쪽 별칭이며 결정적으로 매핑됐다. 참고로만 알린다.</summary>
            Alias = 1,
            /// <summary>매핑할 수 없어 기본 레이어로 떨어졌다. 반드시 경고한다.</summary>
            Unknown = 2,
            /// <summary>힌트가 비어 있어 슬롯에서 기본값을 유추했다.</summary>
            DerivedFromSlot = 3,
        }

        /// <summary>
        /// manifest 의 <c>sortingLayerHint</c> → §6.4 의 실제 Sorting Layer 이름.
        ///
        /// <b>세 상태로 나누는 이유</b> — 별칭까지 경고로 올리면 아트가 그 표현을 계속 쓰는
        /// 동안 리포트가 경고로 가득 찬다. 반대로 매핑 불가를 조용히 넘기면 렌더러가 소리
        /// 없이 기본 레이어로 떨어져 화면에서 원인을 찾기 어렵다. 별칭은 참고, 불가는 경고다.
        /// </summary>
        public static string SortingLayerForHint(string hint, out HintMatch match)
        {
            switch (hint)
            {
                case null:
                case "":
                    match = HintMatch.Unknown;
                    return "WorldEntity";

                // §6.4 표의 이름은 그대로 통과
                case "WorldVoid":
                case "GroundBase":
                case "GroundDetail":
                case "GroundDecal":
                case "BackStructure":
                case "WallTop":
                case "WorldEntity":
                case "FrontStructure":
                case "WorldFX":
                case "VisionAndGrade":
                case "WorldOverlay":
                case "UI":
                    match = HintMatch.Exact;
                    return hint;

                // 아트 쪽 표현 → 구현 레이어
                case "WorldStructure": match = HintMatch.Alias; return "BackStructure";
                case "WorldForeground": match = HintMatch.Alias; return "FrontStructure";
                case "Ground":
                case "WorldGround": match = HintMatch.Alias; return "GroundBase";
                case "GroundAO":
                case "GroundAo":
                case "WorldGroundDecal": match = HintMatch.Alias; return "GroundDecal";

                default:
                    match = HintMatch.Unknown;
                    return "WorldEntity";
            }
        }

        /// <summary>
        /// 힌트가 비어 있을 때 슬롯에서 유추하는 레이어.
        ///
        /// <b>타일 슬롯에서는 이 값이 실제 렌더링에 쓰이지 않는다.</b>
        /// <c>EnvironmentChunkRenderer</c> 가 표면 토폴로지로 레이어를 정하기 때문이다
        /// (예: 같은 cap 이 북쪽이 열렸으면 <c>FrontStructure</c>, 막혔으면 <c>WallTop</c>).
        /// 그래서 타일 자산에 힌트가 없는 것은 문제가 아니다 — 참고로만 알린다.
        /// </summary>
        public static string DefaultLayerForSlot(KitSlot slot)
        {
            switch (slot)
            {
                case KitSlot.FloorBase: return "GroundBase";
                case KitSlot.FloorEdge: return "GroundDetail";
                case KitSlot.ContactAo: return "GroundDecal";
                case KitSlot.WallTop:
                case KitSlot.WallTopRim:
                case KitSlot.OuterCorner:
                case KitSlot.InnerCorner: return "WallTop";
                case KitSlot.WallFront:
                case KitSlot.WestSide:
                case KitSlot.EastSide: return "BackStructure";
                default: return "WorldEntity";
            }
        }

        /// <summary>사람이 읽는 계약 요약. 검사 리포트 머리에 붙인다.</summary>
        public static string Summary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"납품 밀도 {DeliveryPixelsPerCell}px/셀 (PPU {DeliveryPixelsPerCell}) · " +
                          $"원화 최소 {MinSourcePixelsPerCell}px/셀");
            sb.AppendLine($"타일 자산: 캔버스가 셀 배수와 정확히 일치해야 한다(패딩 금지 — 이음새가 생긴다)");
            sb.AppendLine($"비타일 자산: 셀 배수 이상 + 패딩 {MinPaddingPixels}px 권장 " +
                          $"(Emission {MinEmissionPaddingPixels}px)");
            sb.AppendLine($"파일명 {FilePrefix}<category>_<name>_<variant>_<channel>.png · " +
                          $"채널 {string.Join("/", Channels)}");
            sb.AppendLine($"Linear 로 임포트할 채널: {string.Join("/", LinearChannels)}");
            sb.Append($"status 가 '{StatusApproved}' 인 자산만 임포트한다 · 슬롯은 assetId 의 ID 범위로 정한다");
            return sb.ToString();
        }
    }
}
