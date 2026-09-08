using System.Collections.Generic;
using System.IO;
using TunnelCrew.Presentation.Visual;
using UnityEditor;
using UnityEngine;

namespace TunnelCrew.EditorTools.ArtPipeline
{
    /// <summary>
    /// Albedo·Normal·Emission·Mask·AO 를 <b>동일 배치</b>의 아틀라스로 묶고, Albedo
    /// 아틀라스에서 스프라이트를 잘라 <see cref="EnvironmentKit"/> 과
    /// <see cref="SurfaceMaterialSet"/> 에 연결한다(아트 규격 §8.1).
    ///
    /// <b>왜 필요한가</b> — 승인 아트는 자산마다 자기 128×128 채널 맵을 갖는다(바닥 6종이면
    /// 노멀도 6장). 그런데 <see cref="EnvironmentChunkRenderer"/> 는 표면 분류마다 머티리얼
    /// 하나를 Tilemap 에 씌우고, 머티리얼의 <c>_NormalMap</c> 은 하나뿐이다. 즉 바닥 6종의
    /// 서로 다른 노멀을 동시에 넣을 수 없다. 스프라이트가 아틀라스의 부분 사각형이 되면
    /// 그 UV 가 채널 아틀라스의 같은 자리를 가리키므로 머티리얼 하나로 끝난다.
    ///
    /// <b>왜 원본 PNG 를 읽는가</b> — 임포트된 텍스처를 읽으면 NormalMap 타입이 적용한
    /// 재인코딩(스위즐)이 섞이고, 압축·색공간 변환도 끼어든다. 패키지의 원본 PNG 를
    /// 바이트 그대로 읽어 그대로 쓰는 것이 유일하게 안전하다.
    /// </summary>
    public static class ChannelAtlasBuilder
    {
        /// <summary>아틀라스가 들어갈 곳.</summary>
        public const string AtlasDir = "Assets/Art/Visual/TestRoomV01/Atlas";
        const string DataDir = "Assets/_Project/Data/Visual";

        /// <summary>셀 사이 여백. 가장자리를 늘려 채워 바이리니어 블리딩을 막는다.</summary>
        const int Padding = 4;

        /// <summary>
        /// 아틀라스 묶음 하나. <see cref="EnvironmentChunkRenderer"/> 가 실제로 쓰는
        /// 머티리얼 단위와 같아야 한다 — 그래야 아틀라스 하나가 머티리얼 하나에 대응한다.
        /// </summary>
        sealed class Group
        {
            public string Name;
            public MinLightSlot MinLight;
            public ApprovedArtContract.KitSlot[] Slots;
            public readonly List<ApprovedAsset> Assets = new List<ApprovedAsset>();
        }

        static Group[] MakeGroups() => new[]
        {
            new Group
            {
                Name = "floor",
                MinLight = MinLightSlot.Floor,
                Slots = new[]
                {
                    ApprovedArtContract.KitSlot.FloorBase,
                    ApprovedArtContract.KitSlot.FloorEdge,
                },
            },
            new Group
            {
                Name = "walltop",
                MinLight = MinLightSlot.WallTop,
                Slots = new[]
                {
                    ApprovedArtContract.KitSlot.WallTop,
                    ApprovedArtContract.KitSlot.WallTopRim,
                    ApprovedArtContract.KitSlot.OuterCorner,
                    ApprovedArtContract.KitSlot.InnerCorner,
                },
            },
            new Group
            {
                Name = "wallfront",
                MinLight = MinLightSlot.WallFront,
                Slots = new[]
                {
                    ApprovedArtContract.KitSlot.WallFront,
                    ApprovedArtContract.KitSlot.WestSide,
                    ApprovedArtContract.KitSlot.EastSide,
                },
            },
        };

        public struct Result
        {
            public SurfaceMaterialSet Floor, WallTop, WallFront;
            public int AtlasCount;
            public int SpriteCount;
        }

        /// <summary>
        /// 검사를 통과한 자산으로 아틀라스를 만들고 키트·재질을 연결한다.
        /// <paramref name="kit"/> 의 아틀라스 대상 슬롯만 덮어쓴다 — 접촉 AO 처럼
        /// 채널 재질을 쓰지 않는 슬롯은 개별 스프라이트를 그대로 둔다.
        /// </summary>
        public static Result Build(ArtValidationReport report, string packageRoot, EnvironmentKit kit,
            WorldVisualProfile profile)
        {
            var result = new Result();
            if (report == null || kit == null) return result;

            Directory.CreateDirectory(AtlasDir);

            var groups = MakeGroups();
            var bySlot = new Dictionary<ApprovedArtContract.KitSlot, Group>();
            foreach (var g in groups)
                foreach (var slot in g.Slots) bySlot[slot] = g;

            // assetId 순으로 담는다 — 모듈 번호가 배열 인덱스이므로 순서가 흔들리면
            // 같은 시드에서 다른 배치가 나온다(§16.1 결정성).
            var sorted = new List<ApprovedAsset>(report.Importable);
            sorted.Sort((a, b) => string.CompareOrdinal(a.AssetId, b.AssetId));

            foreach (var asset in sorted)
                if (bySlot.TryGetValue(asset.Slot, out var g)) g.Assets.Add(asset);

            foreach (var g in groups)
            {
                if (g.Assets.Count == 0) continue;

                var set = BuildGroup(g, packageRoot, kit, profile, out int sprites);
                result.SpriteCount += sprites;
                result.AtlasCount += ApprovedArtContract.Channels.Length;

                switch (g.Name)
                {
                    case "floor": result.Floor = set; break;
                    case "walltop": result.WallTop = set; break;
                    case "wallfront": result.WallFront = set; break;
                }
            }

            EditorUtility.SetDirty(kit);
            AssetDatabase.SaveAssets();
            return result;
        }

        // ───────────────────────────── 묶음 하나

        static SurfaceMaterialSet BuildGroup(Group group, string packageRoot, EnvironmentKit kit,
            WorldVisualProfile profile, out int spriteCount)
        {
            spriteCount = 0;

            // 1) 원본 Albedo 를 읽어 셀 크기를 정한다.
            var sizes = new List<Vector2Int>(group.Assets.Count);
            int cellW = 1, cellH = 1;
            foreach (var asset in group.Assets)
            {
                var size = new Vector2Int(asset.Width, asset.Height);
                sizes.Add(size);
                if (size.x > cellW) cellW = size.x;
                if (size.y > cellH) cellH = size.y;
            }

            var layout = AtlasLayout.Create(group.Assets.Count, cellW, cellH, Padding);

            // 2) 채널마다 아틀라스를 만든다. 배치는 동일하다.
            var atlasPaths = new Dictionary<string, string>();
            foreach (var channel in ApprovedArtContract.Channels)
            {
                var pixels = NewAtlasPixels(layout, channel);
                bool anyContent = false;

                for (int i = 0; i < group.Assets.Count; i++)
                {
                    var asset = group.Assets[i];
                    if (!asset.Channels.TryGetValue(channel, out string rel)) continue;

                    string full = Path.Combine(packageRoot, rel);
                    var src = ReadPng(full, out int sw, out int sh);
                    if (src == null)
                    {
                        Debug.LogWarning($"[비주얼] {rel} 를 읽지 못했다 — 이 자산의 '{channel}' 은 기본값으로 둔다.");
                        continue;
                    }

                    var cell = layout.RectFor(i, sw, sh);
                    Blit(pixels, layout.AtlasWidth, layout.AtlasHeight, src, sw, sh, cell.X, cell.Y);
                    Extrude(pixels, layout.AtlasWidth, layout.AtlasHeight, cell, layout.Padding);
                    anyContent = true;
                }

                // 아무 자산도 이 채널을 내지 않았으면 아틀라스를 만들지 않는다 —
                // 셰이더의 기본값(bump / black / white)이 "그 채널 없음" 을 뜻한다.
                if (!anyContent && channel != "albedo") continue;

                string path = $"{AtlasDir}/tr01_atlas_{group.Name}_{channel}.png";
                WritePng(path, pixels, layout.AtlasWidth, layout.AtlasHeight);
                atlasPaths[channel] = path;
            }

            AssetDatabase.Refresh();

            // 3) Albedo 아틀라스를 스프라이트로 자르고, 나머지는 데이터 맵으로 임포트한다.
            string albedoPath = atlasPaths["albedo"];
            SliceAlbedoAtlas(albedoPath, group, layout, sizes);

            foreach (var kv in atlasPaths)
            {
                if (kv.Key == "albedo") continue;
                ConfigureDataAtlas(kv.Value, kv.Key);
            }

            AssetDatabase.Refresh();

            // 4) 잘린 스프라이트를 슬롯별로 키트에 넣는다.
            var sprites = LoadSprites(albedoPath);
            var bySlot = new Dictionary<ApprovedArtContract.KitSlot, List<Sprite>>();

            foreach (var asset in group.Assets)
            {
                if (!sprites.TryGetValue(SpriteName(asset), out var sprite))
                {
                    Debug.LogWarning($"[비주얼] 아틀라스에서 스프라이트 '{SpriteName(asset)}' 를 찾지 못했다.");
                    continue;
                }
                if (!bySlot.TryGetValue(asset.Slot, out var list))
                    bySlot[asset.Slot] = list = new List<Sprite>();
                list.Add(sprite);
                spriteCount++;
            }

            ApplyToKit(kit, bySlot);

            // 5) 채널 아틀라스를 재질 묶음에 연결한다.
            var set = LoadOrCreate<SurfaceMaterialSet>($"{DataDir}/SurfaceMaterialSet_TestRoom_{group.Name}.asset");
            set.kind = SurfaceMaterialSet.Kind.World;
            set.minLightSlot = group.MinLight;
            set.normal = LoadTexture(atlasPaths, "normal");
            set.emission = LoadTexture(atlasPaths, "emission");
            set.materialMask = LoadTexture(atlasPaths, "mask");
            set.ao = LoadTexture(atlasPaths, "ao");
            set.normalStrength = 1f;
            set.aoStrength = 1f;
            set.emissionIntensity = 1f;
            set.minLightOverride = -1f;
            EditorUtility.SetDirty(set);

            Debug.Log($"[비주얼] 아틀라스 '{group.Name}' — {layout} · 채널 {atlasPaths.Count}종 · 스프라이트 {spriteCount}");
            return set;
        }

        static Texture2D LoadTexture(Dictionary<string, string> paths, string channel)
            => paths.TryGetValue(channel, out string p) ? AssetDatabase.LoadAssetAtPath<Texture2D>(p) : null;

        static void ApplyToKit(EnvironmentKit kit, Dictionary<ApprovedArtContract.KitSlot, List<Sprite>> bySlot)
        {
            Sprite[] Get(ApprovedArtContract.KitSlot slot)
                => bySlot.TryGetValue(slot, out var l) ? l.ToArray() : null;

            // 이 묶음이 담당하는 슬롯만 덮어쓴다. 담당하지 않는 슬롯(예: contactAo)은
            // 개별 임포트 결과를 그대로 둔다.
            foreach (var kv in bySlot)
            {
                switch (kv.Key)
                {
                    case ApprovedArtContract.KitSlot.FloorBase: kit.floorBase = Get(kv.Key); break;
                    case ApprovedArtContract.KitSlot.FloorEdge: kit.floorEdge = Get(kv.Key); break;
                    case ApprovedArtContract.KitSlot.WallTop: kit.wallTop = Get(kv.Key); break;
                    case ApprovedArtContract.KitSlot.WallTopRim: kit.wallTopRim = Get(kv.Key); break;
                    case ApprovedArtContract.KitSlot.WallFront: kit.wallFront = Get(kv.Key); break;
                    case ApprovedArtContract.KitSlot.WestSide: kit.westSide = Get(kv.Key); break;
                    case ApprovedArtContract.KitSlot.EastSide: kit.eastSide = Get(kv.Key); break;
                    case ApprovedArtContract.KitSlot.OuterCorner: kit.outerCorner = Get(kv.Key); break;
                    case ApprovedArtContract.KitSlot.InnerCorner: kit.innerCorner = Get(kv.Key); break;
                }
            }
        }

        // ───────────────────────────── 픽셀 조작

        /// <summary>채널마다 "없음" 을 뜻하는 기본값으로 채운다.</summary>
        static Color32[] NewAtlasPixels(AtlasLayout layout, string channel)
        {
            Color32 fill = channel switch
            {
                "albedo" => new Color32(0, 0, 0, 0),            // 투명
                "normal" => new Color32(128, 128, 255, 255),    // 평면 중립값
                "emission" => new Color32(0, 0, 0, 255),        // 비발광
                _ => new Color32(255, 255, 255, 255),           // mask·ao — 차폐 없음 / 마스크 통과
            };

            var px = new Color32[layout.AtlasWidth * layout.AtlasHeight];
            for (int i = 0; i < px.Length; i++) px[i] = fill;
            return px;
        }

        static void Blit(Color32[] dst, int dw, int dh, Color32[] src, int sw, int sh, int x0, int y0)
        {
            for (int y = 0; y < sh; y++)
            {
                int dy = y0 + y;
                if (dy < 0 || dy >= dh) continue;
                for (int x = 0; x < sw; x++)
                {
                    int dx = x0 + x;
                    if (dx < 0 || dx >= dw) continue;
                    dst[dy * dw + dx] = src[y * sw + x];
                }
            }
        }

        /// <summary>
        /// 자산 가장자리를 패딩 폭만큼 늘려 채운다.
        ///
        /// 바이리니어 필터링은 스프라이트 사각형 바로 밖의 텍셀까지 닿는다. 그 자리가
        /// 기본값(투명·흰색)이면 타일 경계에 밝거나 어두운 선이 생긴다. 가장자리 픽셀을
        /// 복제해 두면 그 샘플이 자기 가장자리와 같아져 선이 사라진다.
        /// </summary>
        static void Extrude(Color32[] px, int w, int h, AtlasLayout.Cell cell, int pad)
        {
            if (pad <= 0) return;

            int x0 = cell.X, y0 = cell.Y, cw = cell.Width, chh = cell.Height;

            // 좌우
            for (int y = 0; y < chh; y++)
            {
                int sy = y0 + y;
                if (sy < 0 || sy >= h) continue;
                var left = px[sy * w + Clamp(x0, 0, w - 1)];
                var right = px[sy * w + Clamp(x0 + cw - 1, 0, w - 1)];
                for (int p = 1; p <= pad; p++)
                {
                    int lx = x0 - p, rx = x0 + cw - 1 + p;
                    if (lx >= 0) px[sy * w + lx] = left;
                    if (rx < w) px[sy * w + rx] = right;
                }
            }

            // 위아래 (모서리까지 포함해 늘린 좌우 영역을 함께 복제한다)
            for (int x = -pad; x < cw + pad; x++)
            {
                int sx = x0 + x;
                if (sx < 0 || sx >= w) continue;
                var bottom = px[Clamp(y0, 0, h - 1) * w + sx];
                var top = px[Clamp(y0 + chh - 1, 0, h - 1) * w + sx];
                for (int p = 1; p <= pad; p++)
                {
                    int by = y0 - p, ty = y0 + chh - 1 + p;
                    if (by >= 0) px[by * w + sx] = bottom;
                    if (ty < h) px[ty * w + sx] = top;
                }
            }
        }

        static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);

        /// <summary>
        /// PNG 를 바이트 그대로 읽는다. <c>linear: true</c> 로 만들어 색공간 변환이
        /// 끼어들지 않게 한다 — 데이터 맵을 감마 보정하면 값이 뒤틀린다.
        /// </summary>
        static Color32[] ReadPng(string fullPath, out int w, out int h)
        {
            w = h = 0;
            if (!File.Exists(fullPath)) return null;

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false, linear: true);
            try
            {
                if (!tex.LoadImage(File.ReadAllBytes(fullPath), markNonReadable: false)) return null;
                w = tex.width;
                h = tex.height;
                return tex.GetPixels32();
            }
            finally { Object.DestroyImmediate(tex); }
        }

        static void WritePng(string assetPath, Color32[] px, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: true);
            try
            {
                tex.SetPixels32(px);
                tex.Apply();
                File.WriteAllBytes(assetPath, tex.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(tex); }
        }

        // ───────────────────────────── 임포트

        static string SpriteName(ApprovedAsset asset)
            => asset.AssetId.ToLowerInvariant().Replace('-', '_');

        /// <summary>Albedo 아틀라스를 자산별 스프라이트로 자른다. 피벗은 자산 사각형 기준이다.</summary>
        static void SliceAlbedoAtlas(string assetPath, Group group, AtlasLayout layout,
            List<Vector2Int> sizes)
        {
            var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (ti == null)
            {
                Debug.LogError($"[비주얼] {assetPath} 의 TextureImporter 를 얻지 못했다.");
                return;
            }

            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Multiple;
            ti.spritePixelsPerUnit = ApprovedArtContract.DeliveryPixelsPerCell;
            ti.filterMode = FilterMode.Bilinear;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.alphaIsTransparency = true;
            ti.sRGBTexture = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.maxTextureSize = 4096;

            ti.SaveAndReimport();   // Multiple 모드를 먼저 확정해야 데이터 프로바이더가 열린다

            // 스프라이트 사각형은 ISpriteEditorDataProvider 로 넣는다.
            // TextureImporter.spritesheet 는 "support has been removed" 로 표시된 경로다 —
            // 지금은 동작하지만 조용히 멈출 수 있어 쓰지 않는다.
            var factories = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(ti);
            if (provider == null)
            {
                Debug.LogError($"[비주얼] {assetPath} 의 스프라이트 데이터 프로바이더를 얻지 못했다.");
                return;
            }
            provider.InitSpriteEditorDataProvider();

            var rects = new List<SpriteRect>(group.Assets.Count);
            for (int i = 0; i < group.Assets.Count; i++)
            {
                var asset = group.Assets[i];
                var cell = layout.RectFor(i, sizes[i].x, sizes[i].y);

                ApprovedArtContract.PivotToUnity(asset.PivotX, asset.PivotY,
                    cell.Width, cell.Height, out float u, out float v);

                rects.Add(new SpriteRect
                {
                    name = SpriteName(asset),
                    rect = new Rect(cell.X, cell.Y, cell.Width, cell.Height),
                    alignment = SpriteAlignment.Custom,
                    pivot = new Vector2(u, v),
                    border = Vector4.zero,
                    // 이름에서 만든 고정 GUID — 다시 임포트해도 같은 값이라
                    // EnvironmentKit 이 잡고 있는 참조가 끊기지 않는다.
                    spriteID = StableSpriteId(SpriteName(asset)),
                });
            }

            provider.SetSpriteRects(rects.ToArray());

            // 이름↔파일ID 표도 함께 넣어야 참조가 재임포트를 넘어 살아남는다.
            var nameIds = provider.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteNameFileIdDataProvider>();
            if (nameIds != null)
            {
                var pairs = new List<SpriteNameFileIdPair>(rects.Count);
                foreach (var r in rects)
                    pairs.Add(new SpriteNameFileIdPair(r.name, r.spriteID));
                nameIds.SetNameFileIdPairs(pairs);
            }

            provider.Apply();
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>
        /// 스프라이트 이름에서 만든 결정적 GUID. 매번 새로 만들면 재임포트마다 참조가
        /// 끊어져 <see cref="EnvironmentKit"/> 슬롯이 비어 버린다.
        /// </summary>
        static GUID StableSpriteId(string name)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes("tc.sprite." + name));
            var sb = new System.Text.StringBuilder(32);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return new GUID(sb.ToString());
        }

        static void ConfigureDataAtlas(string assetPath, string channel)
        {
            var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (ti == null) return;

            bool isNormal = channel == "normal";
            ti.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = false;                 // 데이터 맵은 Linear (§12.2)
            if (isNormal) ti.convertToNormalmap = false;
            ti.alphaIsTransparency = false;
            ti.filterMode = FilterMode.Bilinear;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.maxTextureSize = 4096;
            ti.SaveAndReimport();
        }

        static Dictionary<string, Sprite> LoadSprites(string assetPath)
        {
            var map = new Dictionary<string, Sprite>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                if (o is Sprite s) map[s.name] = s;
            return map;
        }

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
