using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TunnelCrew.EditorTools
{
    /// <summary>
    /// 알베도에서 Material Mask 를 굽는다 — <b>임시 경로</b>다.
    ///
    /// <b>왜 필요한가</b> — 마스크 채널 규약(2026-09-09 확정,
    /// `docs/unity-port/mask-channel-convention.md`)에 따라 `Additive with Mask` 가 B(습윤·결정),
    /// `Multiply with Mask` 가 G(광택)를 본다. 그런데 <b>레퍼런스 캘리브레이션 아트는 albedo 뿐</b>이라
    /// (10장 전부) 랩에서 마스크 라이팅을 실증할 수가 없다. 마스크가 없으면 `_MaskTex` 가 흰색으로
    /// 떨어져 "마스크 있는 애디티브" 가 그냥 애디티브와 같아지고, 채널이 하는 일이 화면에 보이지 않는다.
    ///
    /// 그래서 아트가 진짜 마스크를 납품할 때까지 알베도에서 근사값을 만든다.
    /// <b>아트가 오면 이 파일이 만든 것을 지우고 납품본을 쓴다</b> — 파일명이 같으므로 덮으면 끝난다.
    ///
    /// <b>굽는 규칙</b>
    /// <list type="bullet">
    /// <item><b>B(습윤·결정)</b> — 채도와 명도가 함께 높은 픽셀. 수정·광맥처럼 "스스로 빛나 보이는"
    ///       영역이 여기 걸린다. 회색 암석은 채도가 낮아 빠진다.</item>
    /// <item><b>G(광택)</b> — 알파 경계에서 안쪽으로 <see cref="RimPixels"/> 픽셀 대역. 규약이 요구하는
    ///       "가장자리 2~4px" 그대로다. 실루엣 림 라이트가 이 채널을 쓴다.</item>
    /// <item><b>R(금속)</b> — 0. 계약상 예약 채널이고 알베도로는 금속 여부를 알 수 없다.</item>
    /// <item><b>A(효과 강도)</b> — 255.</item>
    /// </list>
    /// </summary>
    public static class BakePlaceholderMasks
    {
        const string ArtDir = "Assets/Art/Visual/ReferenceCalibrationV1";

        /// <summary>광택(G) 대역 폭. 규약의 "가장자리 2~4px" 중간값.</summary>
        const int RimPixels = 3;

        /// <summary>이 알파 미만은 실루엣 밖으로 본다.</summary>
        const float AlphaCut = 0.35f;

        /// <summary>결정으로 볼 최소 채도·명도. 회색 암석을 걸러내는 문턱이다.</summary>
        const float CrystalSat = 0.35f, CrystalVal = 0.45f;

        [MenuItem("Tunnel Crew/비주얼 · 임시 마스크 굽기 (레퍼런스 아트)", priority = 27)]
        public static void Run()
        {
            // 결정·발광 후보와, 림(G)만 필요한 캐릭터를 함께 굽는다.
            var targets = new[]
            {
                "tr01_reference_crystal_a_albedo.png",
                "tr01_reference_lamp_a_albedo.png",
                "tr01_reference_driller_a_albedo.png",
            };

            var made = new List<string>();
            foreach (var file in targets)
            {
                string src = $"{ArtDir}/{file}";
                if (!File.Exists(Path.GetFullPath(src)))
                {
                    Debug.LogWarning($"[마스크] {src} 가 없다 — 건너뛴다.");
                    continue;
                }
                string dst = src.Replace("_albedo.png", "_mask.png");
                if (Bake(src, dst)) made.Add(Path.GetFileName(dst));
            }

            AssetDatabase.Refresh();
            foreach (var f in made) ConfigureMaskImport($"{ArtDir}/{f}");
            AssetDatabase.Refresh();

            Debug.Log($"[마스크] 임시 마스크 {made.Count}장 — {string.Join(" · ", made)}\n" +
                      $"규약: R 금속(0) · G 광택(경계 {RimPixels}px) · B 습윤·결정(채도≥{CrystalSat} 명도≥{CrystalVal}) · A 1");
        }

        static bool Bake(string srcPath, string dstPath)
        {
            var bytes = File.ReadAllBytes(Path.GetFullPath(srcPath));
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false, linear: false);
            if (!tex.LoadImage(bytes, markNonReadable: false))
            {
                Object.DestroyImmediate(tex);
                Debug.LogWarning($"[마스크] {srcPath} 디코드 실패");
                return false;
            }

            try
            {
                int w = tex.width, h = tex.height;
                var src = tex.GetPixels();
                var outp = new Color[src.Length];

                // 실루엣 안/밖을 먼저 굽는다 — G 대역 계산에 쓴다.
                var inside = new bool[src.Length];
                for (int i = 0; i < src.Length; i++) inside[i] = src[i].a >= AlphaCut;

                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (!inside[i]) { outp[i] = new Color(0, 0, 0, 0); continue; }

                    Color.RGBToHSV(src[i], out _, out float s, out float v);

                    // B — 채도·명도가 함께 높은 곳. 문턱 위에서 부드럽게 올린다.
                    float crystal = Mathf.Clamp01((s - CrystalSat) / (1f - CrystalSat)) *
                                    Mathf.Clamp01((v - CrystalVal) / (1f - CrystalVal));

                    // G — 경계에서 안쪽 RimPixels 대역. 바깥에 가까울수록 1.
                    float rim = 0f;
                    int dist = EdgeDistance(inside, w, h, x, y, RimPixels);
                    if (dist <= RimPixels) rim = 1f - (dist - 1) / (float)RimPixels;

                    outp[i] = new Color(0f, Mathf.Clamp01(rim), Mathf.Clamp01(crystal), 1f);
                }

                var mask = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: true);
                mask.SetPixels(outp);
                mask.Apply();
                File.WriteAllBytes(Path.GetFullPath(dstPath), mask.EncodeToPNG());
                Object.DestroyImmediate(mask);
                return true;
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }

        /// <summary>실루엣 밖까지의 체비셰프 거리(최대 <paramref name="max"/>+1).</summary>
        static int EdgeDistance(bool[] inside, int w, int h, int x, int y, int max)
        {
            for (int r = 1; r <= max; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) return r;   // 캔버스 밖 = 실루엣 밖
                    if (!inside[ny * w + nx]) return r;
                }
            }
            return max + 1;
        }

        /// <summary>마스크는 Linear·비압축·밉맵 끔 — 아트 계약 §5 의 채널 임포트 규칙.</summary>
        static void ConfigureMaskImport(string path)
        {
            var im = AssetImporter.GetAtPath(path) as TextureImporter;
            if (im == null) return;
            im.textureType = TextureImporterType.Default;
            im.sRGBTexture = false;
            im.mipmapEnabled = false;
            im.alphaIsTransparency = false;
            im.alphaSource = TextureImporterAlphaSource.FromInput;
            im.textureCompression = TextureImporterCompression.Uncompressed;
            im.wrapMode = TextureWrapMode.Clamp;
            im.filterMode = FilterMode.Bilinear;
            im.SaveAndReimport();
        }
    }
}
