using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TunnelCrew.EditorTools
{
    /// <summary>
    /// 스프라이트 알파에서 그림자 캐스터 윤곽을 뽑는다 — <b>임시 경로</b>다.
    ///
    /// <b>왜 임시인가</b> — 기능명세서 §7.4 와 아트 인계서 §3 은 그림자 윤곽을 manifest 의
    /// <c>shadowCasterFootprintCells</c> 로 받기로 했다. 그게 결정적이고, 아트가 "그림자로
    /// 보일 부분" 을 직접 정할 수 있어서다(아치라면 지붕이 아니라 두 다리).
    ///
    /// 그런데 승인 패키지(revision 17)의 실제 상태가 이렇다:
    /// <list type="bullet">
    /// <item>51종 중 43종은 캐스터 데이터가 없다 → footprint 사각형으로 떨어졌다.</item>
    /// <item>있는 8종도 전부 축 정렬 사각형이다(아치 5×1 → [0,0][5,0][5,1][0,1]).</item>
    /// </list>
    /// 그래서 화면에서 "모든 물체의 그림자가 네모" 로 보였다(2026-09-09 피드백).
    /// 아트가 실루엣 윤곽을 납품할 때까지 알파에서 근사 윤곽을 만들어 그 자리를 채운다.
    ///
    /// <b>아트 데이터가 오면 그쪽이 이긴다</b> — <see cref="SetPieceCatalogBuilder"/> 는
    /// manifest 윤곽이 사각형이 아닐 때 그것을 쓰고, 사각형이거나 없을 때만 이 경로로 온다.
    /// </summary>
    public static class SpriteAlphaContour
    {
        /// <summary>
        /// 윤곽 점 수 상한. 그림자는 실루엣의 <b>큰 형태</b>만 필요하다(§5.2 "큰 형태 우선").
        /// </summary>
        public const int MaxPoints = 24;

        /// <summary>이 알파 미만은 없는 것으로 본다.</summary>
        const float AlphaCut = 0.35f;

        /// <summary>
        /// 발점(피벗) 기준 셀 좌표 윤곽. 실패하면 null 이다.
        ///
        /// 납품 밀도가 128px/셀이고 임포트 PPU 도 128 이므로 스프라이트 로컬 유닛이 곧 셀이다.
        ///
        /// <b>PNG 를 직접 디코드한다</b> — 임포터의 <c>isReadable</c> 을 켜서 읽는 방법을
        /// 먼저 썼는데, 임포트 진행 중에는 Unity 가 meta 파일을 쓰지 못해
        /// "Cannot open file ….meta for write" 로 실패했다(2026-09-09). 임포트 설정을
        /// 건드리지 않는 이 방법이 아트 패키지 규약(§2)도 지킨다.
        /// </summary>
        public static float[] FromSprite(Sprite sprite, int footprintCols, int footprintRows)
        {
            if (sprite == null || sprite.texture == null) return null;

            var tex = DecodeSource(sprite);
            if (tex == null) return null;

            try
            {
                var poly = Trace(sprite, tex, Mathf.Max(1, footprintCols), Mathf.Max(1, footprintRows));
                if (poly == null || poly.Count < 3) return null;

                var flat = new float[poly.Count * 2];
                for (int i = 0; i < poly.Count; i++)
                {
                    flat[i * 2] = poly[i].x;
                    flat[i * 2 + 1] = poly[i].y;
                }
                return flat;
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }

        /// <summary>원본 PNG 를 읽기 가능한 임시 텍스처로 디코드한다. 호출자가 파괴한다.</summary>
        static Texture2D DecodeSource(Sprite sprite)
        {
            string path = AssetDatabase.GetAssetPath(sprite.texture);
            if (string.IsNullOrEmpty(path)) return null;

            string full = Path.GetFullPath(path);
            if (!File.Exists(full)) return null;

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false, linear: false);
            if (!tex.LoadImage(File.ReadAllBytes(full), markNonReadable: false))
            {
                Object.DestroyImmediate(tex);
                return null;
            }
            return tex;
        }

        /// <summary>
        /// 실루엣의 <b>아래쪽 띠</b>만 스캔해 각 행의 좌우 끝을 잇는다.
        ///
        /// 마칭 스퀘어로 전체 외곽선을 따지 않는 이유 — 그림자는 지면에 눕는 형상이라
        /// 위쪽(아치의 지붕, 드릴의 팔)까지 윤곽에 넣으면 그림자가 실제보다 훨씬 길어진다.
        /// 발밑 footprint 깊이만큼만 보는 것이 사각형보다 정확하고(기둥의 좁은 밑동,
        /// 아치의 두 다리 사이 빈 곳) 계산이 결정적이다.
        /// </summary>
        static List<Vector2> Trace(Sprite sprite, Texture2D src, int cols, int rows)
        {
            var rect = sprite.textureRect;
            float ppu = sprite.pixelsPerUnit > 0f ? sprite.pixelsPerUnit : 128f;

            // 임포터가 텍스처를 줄였으면 원본 PNG 와 스프라이트 rect 의 축척이 다르다.
            float scale = rect.width > 0.5f ? src.width / (float)sprite.texture.width : 1f;
            if (scale <= 0f) scale = 1f;

            int w = Mathf.RoundToInt(rect.width * scale);
            int h = Mathf.RoundToInt(rect.height * scale);
            if (w <= 2 || h <= 2) return null;

            int x0 = Mathf.Clamp(Mathf.RoundToInt(rect.x * scale), 0, Mathf.Max(0, src.width - w));
            int y0 = Mathf.Clamp(Mathf.RoundToInt(rect.y * scale), 0, Mathf.Max(0, src.height - h));
            var pixels = src.GetPixels(x0, y0, w, h);

            // 픽셀 → 셀 환산. 원본 PNG 기준이므로 축척을 함께 나눈다.
            float pxPerCell = ppu * scale;
            Vector2 pivotPx = sprite.pivot * scale;   // rect 안 픽셀 좌표(좌하단 원점)

            int bandPx = Mathf.Clamp(Mathf.RoundToInt(rows * pxPerCell), 2, h);
            int bandBottom = Mathf.Clamp(Mathf.RoundToInt(pivotPx.y - pxPerCell * 0.15f), 0, h - 1);
            int bandTop = Mathf.Min(h - 1, bandBottom + bandPx);

            int steps = Mathf.Max(2, MaxPoints / 2);
            int rowSpan = Mathf.Max(1, (bandTop - bandBottom) / steps);
            var left = new List<Vector2>(steps);
            var right = new List<Vector2>(steps);

            for (int s = 0; s < steps; s++)
            {
                float t = steps == 1 ? 0f : (float)s / (steps - 1);
                int y = Mathf.RoundToInt(Mathf.Lerp(bandBottom, bandTop, t));
                int minX = int.MaxValue, maxX = int.MinValue;

                // 한 줄만 보면 알파 구멍에 걸린다 — 묶음 안의 여러 줄을 합쳐 본다.
                for (int dy = 0; dy < rowSpan; dy++)
                {
                    int rowBase = Mathf.Clamp(y + dy, 0, h - 1) * w;
                    for (int x = 0; x < w; x++)
                    {
                        if (pixels[rowBase + x].a < AlphaCut) continue;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                    }
                }
                if (minX > maxX) continue;

                float cy = (y - pivotPx.y) / pxPerCell;
                left.Add(new Vector2((minX - pivotPx.x) / pxPerCell, cy));
                right.Add(new Vector2((maxX + 1 - pivotPx.x) / pxPerCell, cy));
            }

            if (left.Count < 2) return null;

            // 반시계 — 아래 왼쪽 → 아래 오른쪽 → 오른쪽 위로 → 왼쪽 아래로.
            // 감는 방향 규약은 WallContourTracer 와 같다(구조물이 진행 방향의 왼쪽).
            var poly = new List<Vector2>(left.Count * 2 + 2);
            poly.Add(new Vector2(left[0].x, 0f));
            poly.Add(new Vector2(right[0].x, 0f));
            for (int i = 1; i < right.Count; i++) poly.Add(right[i]);
            for (int i = left.Count - 1; i >= 1; i--) poly.Add(left[i]);

            // 알파 패딩이 넓은 자산에서 그림자가 발판보다 커 보이지 않게 가둔다.
            float halfW = cols * 0.5f + 0.35f;
            float maxDepth = rows + 0.35f;
            for (int i = 0; i < poly.Count; i++)
                poly[i] = new Vector2(Mathf.Clamp(poly[i].x, -halfW, halfW),
                                      Mathf.Clamp(poly[i].y, 0f, maxDepth));

            return Simplify(poly);
        }

        /// <summary>거의 일직선인 점을 걷어낸다(수직 거리 기준).</summary>
        static List<Vector2> Simplify(List<Vector2> src, float tolCells = 0.05f)
        {
            if (src.Count <= 4) return src;
            var outp = new List<Vector2> { src[0] };
            for (int i = 1; i < src.Count - 1; i++)
            {
                var a = outp[outp.Count - 1];
                var b = src[i];
                var c = src[i + 1];
                var ac = c - a;
                float len = ac.magnitude;
                float dist = len < 1e-5f
                    ? Vector2.Distance(a, b)
                    : Mathf.Abs(ac.x * (a.y - b.y) - (a.x - b.x) * ac.y) / len;
                if (dist > tolCells) outp.Add(b);
            }
            outp.Add(src[src.Count - 1]);
            return outp;
        }

        /// <summary>manifest 가 준 윤곽이 결국 footprint 사각형인가(= 실루엣 정보가 없다).</summary>
        public static bool IsPlainRectangle(float[] flat, int footprintCols, int footprintRows)
        {
            if (flat == null) return true;
            if (flat.Length != 8) return false;
            float halfW = Mathf.Max(1, footprintCols) * 0.5f;
            float depth = Mathf.Max(1, footprintRows);
            var want = new[] { -halfW, 0f, halfW, 0f, halfW, depth, -halfW, depth };
            for (int i = 0; i < 8; i++)
                if (Mathf.Abs(flat[i] - want[i]) > 0.001f) return false;
            return true;
        }
    }
}
