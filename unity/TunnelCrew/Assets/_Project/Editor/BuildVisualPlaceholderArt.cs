using System.Collections.Generic;
using System.IO;
using TunnelCrew.Presentation.Visual;
using UnityEditor;
using UnityEngine;

namespace TunnelCrew.EditorTools
{
    /// <summary>
    /// 128px/셀 <b>임시</b> 환경 아트와 재질 채널을 만든다.
    ///
    /// 최종 아트는 Codex 아트 트랙이 <c>art-production/test-room-v01/approved/</c> 로 낸다
    /// (기능명세서 §0.1). 이 도구가 만드는 것은 그 전에 다층 렌더러·정렬·오클루전·조명·
    /// 채널 파이프라인 구조를 검증하기 위한 대역폭 자산이며, 승인 아트가 들어오면
    /// <c>Tunnel Crew/비주얼 · 승인 아트 임포트</c> 가 그대로 교체한다.
    ///
    /// 임시라도 <b>규격은 최종과 같게</b> 만든다 — 128 PPU, 정수 픽셀 캔버스, 벽 정면의
    /// 하단 중앙 피벗, Normal·Mask·AO 는 Linear 임포트. 그래야 아트를 갈아끼울 때 배치와
    /// 조명 반응이 흔들리지 않는다(docs/test-room-art-production-spec.md §3·§5·§6).
    ///
    /// <b>런타임 코드는 이 파일이 만드는 파일명이나 색에 의존하지 않는다.</b> 연결은 모두
    /// <see cref="EnvironmentKit"/> 과 <see cref="SurfaceMaterialSet"/> 자산의 슬롯을 거친다.
    /// </summary>
    public static class BuildVisualPlaceholderArt
    {
        const string Dir = "Assets/Art/Visual/Placeholder";
        const int Px = 128;                 // 셀당 픽셀 = PPU

        /// <summary>임시 패키지 한 벌. Visual Lab 이 이 참조들을 그대로 받는다.</summary>
        public struct Package
        {
            public EnvironmentKit Kit;
            public SurfaceMaterialSet Floor;
            public SurfaceMaterialSet WallTop;
            public SurfaceMaterialSet WallFront;
        }

        [MenuItem("Tunnel Crew/비주얼 · 임시 환경 아트 생성 (128px/셀)", priority = 20)]
        public static void RunMenu() => Run();

        public static Package Run()
        {
            Directory.CreateDirectory(Dir);
            PivotOf.Clear();
            LinearOf.Clear();

            float lift = 1f;
            int frontPx = Mathf.RoundToInt(Px * lift);

            // §2 팔레트: 차가운 회보라 구조 중간톤, 짙은 남청 암부, 앰버 림.
            var floorA = new Color32(0x3A, 0x38, 0x4A, 0xFF);
            var floorB = new Color32(0x33, 0x32, 0x44, 0xFF);
            var capTop = new Color32(0x6B, 0x64, 0x82, 0xFF);
            var capDark = new Color32(0x4E, 0x48, 0x63, 0xFF);
            var frontTop = new Color32(0x57, 0x4F, 0x6E, 0xFF);
            var frontBot = new Color32(0x24, 0x21, 0x33, 0xFF);
            var rim = new Color32(0xC8, 0x93, 0x4A, 0xFF);

            var paths = new List<string>();

            // ── Albedo (스프라이트)
            for (int i = 0; i < 4; i++)
            {
                float t = i / 3f;
                paths.Add(Save(Flat(Px, Px, Color32.Lerp(floorA, floorB, t), grid: true),
                    $"floor_{i}", 0.5f, 0.5f));
            }

            for (int i = 0; i < 2; i++)
            {
                var c = i == 0 ? capTop : capDark;
                paths.Add(Save(Flat(Px, Px, c, grid: true), $"walltop_{i}", 0.5f, 0.5f));
                paths.Add(Save(RimCap(Px, c, rim), $"walltoprim_{i}", 0.5f, 0.5f));
            }

            // 벽 정면 — 하단 접촉 AO, 상단 얇은 림(§8.6). 피벗은 하단 중앙.
            for (int i = 0; i < 2; i++)
                paths.Add(Save(FrontFace(Px, frontPx, frontTop, frontBot, rim, i),
                    $"wallfront_{i}", 0.5f, 0f));

            paths.Add(Save(ContactAo(Px, Px), "contact_ao_0", 0.5f, 0.5f));

            // ── 재질 채널 (데이터 맵 — Linear 임포트)
            string normalPath = SaveLinear(BevelNormal(Px, Px), "channel_normal_bevel", isNormalMap: true);
            string maskPath = SaveLinear(MaterialMask(Px, Px), "channel_mask", isNormalMap: false);
            string aoPath = SaveLinear(AoEdge(Px, Px), "channel_ao", isNormalMap: false);
            string emissionPath = SaveLinear(EmissionStrip(Px, Px), "channel_emission", isNormalMap: false);

            var channelPaths = new List<string> { normalPath, maskPath, aoPath, emissionPath };

            AssetDatabase.Refresh();
            foreach (var p in paths) ApplySpriteImport(p);
            foreach (var p in channelPaths) ApplyDataImport(p);
            AssetDatabase.Refresh();

            // ── EnvironmentKit
            var kit = LoadOrCreate<EnvironmentKit>($"{Dir}/EnvironmentKit_Placeholder.asset");
            kit.floorBase = Load(paths, "floor_");
            kit.floorEdge = null;
            kit.contactAo = Load(paths, "contact_ao_");
            kit.wallTop = Load(paths, "walltop_");
            kit.wallTopRim = Load(paths, "walltoprim_");
            kit.wallFront = Load(paths, "wallfront_");
            EditorUtility.SetDirty(kit);

            // ── SurfaceMaterialSet — 표면 분류마다 하나. 최소광 슬롯이 다르다(§7.2).
            var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            var maskTex = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
            var aoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(aoPath);
            var emissionTex = AssetDatabase.LoadAssetAtPath<Texture2D>(emissionPath);

            var floor = MakeSet("Floor", MinLightSlot.Floor, normalTex, null, maskTex, aoTex);
            var wallTop = MakeSet("WallTop", MinLightSlot.WallTop, normalTex, emissionTex, maskTex, aoTex);
            var wallFront = MakeSet("WallFront", MinLightSlot.WallFront, normalTex, null, maskTex, aoTex);

            AssetDatabase.SaveAssets();

            // 셰이더가 실제로 컴파일되는지 지금 확인한다 — 머티리얼을 만들어 보지 않으면
            // 셰이더 오류가 런타임까지 숨는다.
            VerifyShader(SurfaceMaterialSet.WorldShaderName);
            VerifyShader(SurfaceMaterialSet.CharacterShaderName);

            Debug.Log($"[비주얼] 임시 아트 {paths.Count + channelPaths.Count}장 생성 · " +
                      $"채널 4종(Normal/Mask/AO/Emission) · SurfaceMaterialSet 3종\n  {Dir}");

            return new Package { Kit = kit, Floor = floor, WallTop = wallTop, WallFront = wallFront };
        }

        static void VerifyShader(string name)
        {
            var shader = Shader.Find(name);
            if (shader == null)
            {
                Debug.LogError($"[비주얼] 셰이더 '{name}' 를 찾지 못했다.");
                return;
            }
            if (!shader.isSupported)
                Debug.LogError($"[비주얼] 셰이더 '{name}' 가 이 플랫폼에서 컴파일되지 않았다.");
        }

        static SurfaceMaterialSet MakeSet(string label, MinLightSlot slot,
            Texture2D normal, Texture2D emission, Texture2D mask, Texture2D ao)
        {
            var set = LoadOrCreate<SurfaceMaterialSet>($"{Dir}/SurfaceMaterialSet_{label}.asset");
            set.kind = SurfaceMaterialSet.Kind.World;
            set.minLightSlot = slot;
            set.normal = normal;
            set.emission = emission;
            set.materialMask = mask;
            set.ao = ao;
            set.normalStrength = 1f;
            set.aoStrength = 1f;
            set.emissionIntensity = emission != null ? 2f : 1f;
            set.minLightOverride = -1f;
            EditorUtility.SetDirty(set);
            return set;
        }

        // ───────────────────────────── Albedo 텍스처

        static Color32[] Flat(int w, int h, Color32 c, bool grid)
        {
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var o = c;
                    // 셀 경계를 아주 약하게만 표시한다 — 격자가 큰 형태보다 먼저 읽히면 안 된다(§5.2).
                    if (grid && (x == 0 || y == 0))
                        o = Color32.Lerp(c, new Color32(0, 0, 0, 255), 0.12f);
                    px[y * w + x] = o;
                }
            return px;
        }

        static Color32[] RimCap(int size, Color32 body, Color32 rim)
        {
            var px = Flat(size, size, body, grid: true);
            // 북쪽(위) 가장자리에 얇은 림. 텍스처 좌표는 아래에서 위로 증가한다.
            int rimPx = Mathf.Max(2, size / 24);
            for (int y = size - rimPx; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = Color32.Lerp(body, rim, 0.55f);
            return px;
        }

        static Color32[] FrontFace(int w, int h, Color32 top, Color32 bottom, Color32 rim, int variant)
        {
            var px = new Color32[w * h];
            int rimPx = Mathf.Max(2, h / 24);
            int aoPx = Mathf.Max(3, h / 8);

            for (int y = 0; y < h; y++)
            {
                float t = h > 1 ? y / (float)(h - 1) : 0f;
                var c = Color32.Lerp(bottom, top, t);

                if (y >= h - rimPx) c = Color32.Lerp(c, rim, 0.4f);
                if (y < aoPx)
                {
                    float k = 1f - y / (float)aoPx;
                    c = Color32.Lerp(c, new Color32(0, 0, 0, 255), 0.55f * k);
                }

                for (int x = 0; x < w; x++)
                {
                    var o = c;
                    if (variant == 1 && (x == w / 3 || x == 2 * w / 3))
                        o = Color32.Lerp(c, new Color32(0, 0, 0, 255), 0.25f);
                    px[y * w + x] = o;
                }
            }
            return px;
        }

        static Color32[] ContactAo(int w, int h)
        {
            var px = new Color32[w * h];
            int reach = Mathf.Max(4, h / 3);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = (h - 1 - y) / (float)reach;
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(1f - d) * 150f);
                    px[y * w + x] = new Color32(0, 0, 0, a);
                }
            return px;
        }

        // ───────────────────────────── 재질 채널 텍스처

        /// <summary>
        /// 가장자리가 기울어진 베벨 노멀. 평면 중립값은 (128,128,255)다(아트 규격 §5).
        /// 광원이 방향에 따라 다르게 반응하는지 보는 것이 목적이다.
        /// </summary>
        static Color32[] BevelNormal(int w, int h)
        {
            var px = new Color32[w * h];
            int bevel = Mathf.Max(4, w / 8);

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // 각 가장자리에서 안쪽으로 얼마나 들어왔는지 → 기울기
                    float nx = 0f, ny = 0f;
                    if (x < bevel) nx = -(1f - x / (float)bevel);
                    else if (x >= w - bevel) nx = 1f - (w - 1 - x) / (float)bevel;
                    if (y < bevel) ny = -(1f - y / (float)bevel);
                    else if (y >= h - bevel) ny = 1f - (h - 1 - y) / (float)bevel;

                    var n = new Vector3(nx * 0.75f, ny * 0.75f, 1f).normalized;
                    px[y * w + x] = new Color32(
                        (byte)Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f),
                        (byte)Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f),
                        (byte)Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f),
                        255);
                }
            return px;
        }

        /// <summary>R 금속 · G 광택 · B 습윤·결정 · A 효과 강도(아트 규격 §5).</summary>
        static Color32[] MaterialMask(int w, int h)
        {
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // R 은 Renderer2D 의 'Multiply with Mask' / 'Additive with Mask' 가
                    // 실제로 필터하는 채널이다. 왼쪽을 금속으로 두어 반응 차이를 본다.
                    byte metal = (byte)(x < w / 2 ? 255 : 0);
                    byte gloss = (byte)Mathf.RoundToInt(y / (float)(h - 1) * 255f);
                    px[y * w + x] = new Color32(metal, gloss, 0, 255);
                }
            return px;
        }

        /// <summary>흰색 = 차폐 없음. 위쪽 가장자리에만 약한 차폐를 둔다.</summary>
        static Color32[] AoEdge(int w, int h)
        {
            var px = new Color32[w * h];
            int reach = Mathf.Max(3, h / 6);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = (h - 1 - y) / (float)reach;
                    float occ = Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(d));
                    byte v = (byte)Mathf.RoundToInt(occ * 255f);
                    px[y * w + x] = new Color32(v, v, v, 255);
                }
            return px;
        }

        /// <summary>
        /// 비발광은 검정. 얇은 청록 띠 하나만 발광시켜 Emission 이 조명에 곱해지지 않는지
        /// (광원을 꺼도 그대로 보이는지) 확인한다.
        /// </summary>
        static Color32[] EmissionStrip(int w, int h)
        {
            var px = new Color32[w * h];
            int y0 = h / 2 - Mathf.Max(1, h / 32);
            int y1 = h / 2 + Mathf.Max(1, h / 32);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool on = y >= y0 && y <= y1 && x > w / 4 && x < w * 3 / 4;
                    px[y * w + x] = on
                        ? new Color32(0x3A, 0xD8, 0xC8, 255)     // 청록 — §2 의 주 광원색
                        : new Color32(0, 0, 0, 255);
                }
            return px;
        }

        // ───────────────────────────── 저장과 임포트

        static readonly Dictionary<string, Vector2> PivotOf = new Dictionary<string, Vector2>();
        static readonly Dictionary<string, bool> LinearOf = new Dictionary<string, bool>();

        static string Save(Color32[] px, string name, float pivotX, float pivotY)
        {
            string path = WritePng(px, name);
            PivotOf[path] = new Vector2(pivotX, pivotY);
            return path;
        }

        static string SaveLinear(Color32[] px, string name, bool isNormalMap)
        {
            string path = WritePng(px, name);
            LinearOf[path] = isNormalMap;
            return path;
        }

        static string WritePng(Color32[] px, string name)
        {
            // 모든 임시 자산은 1셀 폭이다. 높이만 lift 에 따라 달라진다.
            const int w = Px;
            int h = px.Length / w;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();

            string path = $"{Dir}/tc_ph_{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return path;
        }

        static void ApplySpriteImport(string path)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return;

            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = Px;               // 1셀 = 1유닛
            ti.filterMode = FilterMode.Bilinear;
            ti.mipmapEnabled = false;                  // 버티컬 슬라이스에서 확정한다(§8.1)
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.alphaIsTransparency = true;
            ti.sRGBTexture = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;

            if (PivotOf.TryGetValue(path, out var pivot))
            {
                var settings = new TextureImporterSettings();
                ti.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
                ti.SetTextureSettings(settings);
            }

            ti.SaveAndReimport();
        }

        /// <summary>
        /// 데이터 맵 임포트. sRGB 를 반드시 끈다 — 승인 아트에 적용할 규칙(§12.2)을
        /// 임시 아트에도 같게 적용해야 조명 반응이 나중에 달라지지 않는다.
        /// </summary>
        static void ApplyDataImport(string path)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return;

            bool isNormal = LinearOf.TryGetValue(path, out bool n) && n;

            ti.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = false;
            if (isNormal) ti.convertToNormalmap = false;   // 이미 탄젠트 공간 노멀이다
            ti.alphaIsTransparency = false;
            ti.filterMode = FilterMode.Bilinear;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }

        static Sprite[] Load(List<string> paths, string prefix)
        {
            var list = new List<Sprite>();
            foreach (var p in paths)
            {
                if (!Path.GetFileName(p).StartsWith("tc_ph_" + prefix)) continue;
                var s = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                if (s != null) list.Add(s);
            }
            return list.ToArray();
        }

        internal static T LoadOrCreate<T>(string path) where T : ScriptableObject
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
