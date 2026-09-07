using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TunnelCrew.Data;
using UnityEditor;
using UnityEngine;

namespace TunnelCrew.EditorTools
{
    /// <summary>
    /// `tools/unity-export/import-art.mjs` 가 복사해 둔 PNG 에 임포트 설정을 입히고
    /// <see cref="TileSetAsset"/> 를 만든다.
    ///
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod TunnelCrew.EditorTools.BuildArtAssets.Run
    ///
    /// 슬라이스 규격의 정답지는 아트 폴더의 index JSON 이다. 여기에 숫자를 적어 두지 않는다.
    /// </summary>
    public static class BuildArtAssets
    {
        const string TileDir = "Assets/Art/Tiles/purple";
        const string CharDir = "Assets/Art/Characters";
        const string MonsterDir = "Assets/Art/Monsters";
        /// <summary>보스 레드 파이어 드래곤 — 원본 assets/red-fire-dragon/{idle,walking,fire-breath-a,death}-frames (BOSS_DRAGON_ANIMS, 480² · 10fps). MonsterSheets 에 "dragon_&lt;anim&gt;" 으로 들어간다.</summary>
        const string DragonDir = "Assets/Art/Dragon";
        /// <summary>보스 소환 벽 — 원본 BOSS_WALL_TILES(2525), assets/boss-walls 2종.</summary>
        const string BossWallDir = "Assets/Art/Tiles/boss-walls";
        const string OutDir = "Assets/_Project/Data/Resources";

        [Serializable] class TileEntry { public string file; public string type; public int band, surface, damage, variant, slot; }
        [Serializable] class TileIndex { public string biome; public int cellPixels, count; public string floorSheet; public int floorVariants; public TileEntry[] tiles; }

        [Serializable] class SheetMeta { public int cellWidth, cellHeight, columns, rows, frames; public float[] pivotInCell; }
        [Serializable] class SheetEntry { public string file, direction, action; public SheetMeta meta; }
        [Serializable] class SheetIndex { public string role; public SheetEntry[] sheets; }

        [MenuItem("Tunnel Crew/M1 · 아트 임포트 설정 + 타일셋 생성")]
        public static void Run()
        {
            Directory.CreateDirectory(OutDir);
            int tiles = ConfigureTiles();
            int chars = ConfigureCharacters();
            var set = BuildTileSet();
            int sheets = BuildCharacterSheets();
            int monsters = ConfigureMonsters();
            int dragon = ConfigureFrames(DragonDir);
            BuildMonsterSheets();
            ConfigureBossWalls();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[M1] 아트 설정 완료 — 타일 텍스처 {tiles}, 캐릭터 시트 {chars}, " +
                      $"타일셋 슬롯 {set.slots.Count(s => s != null)}/268, 바닥 {set.floorVariants.Count(s => s != null)}/3, 직업 시트 SO {sheets}, 몬스터 프레임 {monsters}, 드래곤 프레임 {dragon}");
        }

        /// <summary>
        /// 벽 타일 노멀맵 연결 (원본 wRim/wShade 대체 — 계획 §2.2 "타일 노멀맵 + Sprite-Lit").
        /// `Tiles/purple/normals/&lt;tile&gt;_n.png`(tools 로 생성한 베벨+요철 노멀)을 각 타일 텍스처의 세컨더리 텍스처 `_NormalMap` 으로 붙인다.
        /// 노멀 텍스처는 선형(sRGB 끔)·비압축·밉맵 끔. Light2D 는 normalMapQuality 가 켜져 있어야 읽는다(RunBootstrap).
        /// </summary>
        [MenuItem("Tunnel Crew/M7 · 타일 노멀맵 연결")]
        public static void RunTileNormals()
        {
            int n = LinkTileNormals();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[M7] 타일 노멀맵 {n}장 연결");
        }

        [Serializable] class AtlasRect { public int x, y, w, h; }
        [Serializable] class AtlasEntry { public string name; public AtlasRect r; }

        /// <summary>
        /// 벽 아틀라스 임포트 — tools(Pillow)가 만든 `atlas/purple_walls_atlas(.png|_n.png|.json)`.
        /// 268 타일을 한 텍스처(54px 셀, 2px 가장자리 복제)로 묶고 같은 배치의 노멀 아틀라스를 둔다.
        /// 왜: 임포트 세컨더리 텍스처(_NormalMap)는 URP 17 의 타일맵·스프라이트 렌더에서 조명에 반영되지 않았고(합성 노멀로 확인),
        /// 머티리얼 레벨 _NormalMap 은 확실히 동작했다. 벽이 한 텍스처면 머티리얼 하나의 _NormalMap 으로 전 타일을 덮을 수 있다.
        /// </summary>
        [MenuItem("Tunnel Crew/M7 · 벽 아틀라스 + 노멀맵")]
        public static void RunWallAtlas()
        {
            int n = BuildWallAtlas();
            var set = BuildTileSet();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[M7] 벽 아틀라스 서브 스프라이트 {n} · 타일셋 슬롯 {set.slots.Count(s => s != null)}/268 · 노멀 아틀라스 {(set.wallNormalAtlas != null ? "연결" : "없음")}");
        }

        static int BuildWallAtlas()
        {
            string dir = $"{TileDir}/atlas";
            string png = $"{dir}/purple_walls_atlas.png", npng = $"{dir}/purple_walls_atlas_n.png", json = $"{dir}/purple_walls_atlas.json";
            if (!File.Exists(png) || !File.Exists(json)) return 0;

            // JSON: {"cell":54,"pad":2,"cols":17,"rows":16,"width":918,"height":864,"sprites":{"name":{"x":..,"y":..,"w":50,"h":50},...}}
            // JsonUtility 는 사전을 못 읽으므로 간단히 직접 파싱한다.
            string text = File.ReadAllText(json);
            int height = int.Parse(System.Text.RegularExpressions.Regex.Match(text, "\"height\":\\s*(\\d+)").Groups[1].Value);
            var metas = new List<SpriteMetaData>();
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, "\"([A-Za-z0-9_]+)\":\\s*\\{\\s*\"x\":\\s*(\\d+),\\s*\"y\":\\s*(\\d+),\\s*\"w\":\\s*(\\d+),\\s*\"h\":\\s*(\\d+)\\s*\\}"))
            {
                int x = int.Parse(m.Groups[2].Value), y = int.Parse(m.Groups[3].Value), w = int.Parse(m.Groups[4].Value), h = int.Parse(m.Groups[5].Value);
                // 이미지 좌표(위→아래) → Unity Rect(아래→위)
                metas.Add(new SpriteMetaData { name = m.Groups[1].Value, rect = new Rect(x, height - y - h, w, h), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(.5f, .5f) });
            }

            var im = (TextureImporter)AssetImporter.GetAtPath(png);
            im.textureType = TextureImporterType.Sprite;
            im.spriteImportMode = SpriteImportMode.Multiple;
            im.spritePixelsPerUnit = 50f;
            im.filterMode = FilterMode.Bilinear;
            im.mipmapEnabled = false;
            im.alphaIsTransparency = true;
            im.textureCompression = TextureImporterCompression.Uncompressed;
            im.maxTextureSize = 2048;
#pragma warning disable CS0618
            im.spritesheet = metas.ToArray();
#pragma warning restore CS0618
            im.SaveAndReimport();

            if (File.Exists(npng))
            {
                var nim = (TextureImporter)AssetImporter.GetAtPath(npng);
                nim.textureType = TextureImporterType.Default;
                nim.sRGBTexture = false;
                nim.mipmapEnabled = false;
                nim.filterMode = FilterMode.Bilinear;
                nim.textureCompression = TextureImporterCompression.Uncompressed;
                nim.maxTextureSize = 2048;
                nim.SaveAndReimport();
            }
            return metas.Count;
        }

        static int LinkTileNormals()
        {
            string normalDir = TileDir + "/normals";
            if (!Directory.Exists(normalDir)) return 0;
            int n = 0;
            foreach (string path in AssetDatabase.FindAssets("t:Texture2D", new[] { TileDir }).Select(AssetDatabase.GUIDToAssetPath))
            {
                if (path.Contains("/normals/") || path.Contains("/overlays/") || path.EndsWith("_floor_sheet_150x50.png", StringComparison.Ordinal)) continue;
                string nPath = $"{normalDir}/{Path.GetFileNameWithoutExtension(path)}_n.png";
                if (!File.Exists(nPath)) continue;
                var nim = (TextureImporter)AssetImporter.GetAtPath(nPath);
                if (nim != null && (nim.sRGBTexture || nim.textureType != TextureImporterType.Default || nim.mipmapEnabled || nim.textureCompression != TextureImporterCompression.Uncompressed))
                {
                    nim.textureType = TextureImporterType.Default;
                    nim.sRGBTexture = false;
                    nim.mipmapEnabled = false;
                    nim.filterMode = FilterMode.Bilinear;
                    nim.textureCompression = TextureImporterCompression.Uncompressed;
                    nim.SaveAndReimport();
                }
                var ntex = AssetDatabase.LoadAssetAtPath<Texture2D>(nPath);
                var im = (TextureImporter)AssetImporter.GetAtPath(path);
                if (im == null || ntex == null) continue;
                var cur = im.secondarySpriteTextures;
                if (cur != null && cur.Length == 1 && cur[0].name == "_NormalMap" && cur[0].texture == ntex) { n++; continue; }
                im.secondarySpriteTextures = new[] { new SecondarySpriteTexture { name = "_NormalMap", texture = ntex } };
                im.SaveAndReimport();
                n++;
            }
            return n;
        }

        /// <summary>
        /// 보스 소환 벽 타일 임포트 + 타일셋 연결 (원본 BOSS_WALL_TILES).
        /// 원본은 폭을 CELL 에 맞추고 바닥을 셀 하단에 붙여 위로 솟게 그린다(8172~8181) —
        /// PPU = 텍스처 폭(폭 1유닛), 피벗은 바닥에서 반 셀 위(= 타일맵 앵커인 셀 중앙에 오도록).
        /// </summary>
        [MenuItem("Tunnel Crew/M8 · 보스 벽 타일 연결")]
        public static void RunBossWalls()
        {
            int n = ConfigureBossWalls();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[M8] 보스 벽 타일 {n}/2 연결");
        }

        static int ConfigureBossWalls()
        {
            string[] files = { "boss-wall-block.png", "boss-wall-crystal.png" };
            var sprites = new Sprite[2];
            int n = 0;
            for (int i = 0; i < files.Length; i++)
            {
                string path = $"{BossWallDir}/{files[i]}";
                var im = (TextureImporter)AssetImporter.GetAtPath(path);
                if (im == null) continue;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                float w = tex != null ? tex.width : 50f, h = tex != null ? tex.height : 50f;
                im.textureType = TextureImporterType.Sprite;
                im.spriteImportMode = SpriteImportMode.Single;
                im.spritePixelsPerUnit = w;            // 폭 = 1셀
                im.filterMode = FilterMode.Bilinear;
                im.mipmapEnabled = false;
                im.wrapMode = TextureWrapMode.Clamp;
                im.alphaIsTransparency = true;
                im.textureCompression = TextureImporterCompression.Uncompressed;
                var ts = new TextureImporterSettings();
                im.ReadTextureSettings(ts);
                ts.spriteAlignment = (int)SpriteAlignment.Custom;
                ts.spritePivot = new Vector2(.5f, .5f * w / h);
                im.SetTextureSettings(ts);
                im.SaveAndReimport();
                sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprites[i] != null) n++;
            }
            var set = AssetDatabase.LoadAssetAtPath<TileSetAsset>($"{OutDir}/TileSet_purple.asset");
            if (set != null) { set.bossWalls = sprites; EditorUtility.SetDirty(set); }
            return n;
        }

        /// <summary>드래곤 프레임만 다시 임포트하고 MonsterSheets 를 갱신한다 (타일·캐릭터는 건드리지 않는다).</summary>
        [MenuItem("Tunnel Crew/M7 · 보스 드래곤 시트 생성")]
        public static void RunDragon()
        {
            int dragon = ConfigureFrames(DragonDir);
            BuildMonsterSheets();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[M7] 드래곤 프레임 {dragon} 임포트 · MonsterSheets 갱신");
        }

        // ───────────────────────────── 타일: 1장 = 1 스프라이트, PPU 50
        static int ConfigureTiles()
        {
            int n = 0;
            foreach (string path in AssetDatabase.FindAssets("t:Texture2D", new[] { TileDir })
                                                 .Select(AssetDatabase.GUIDToAssetPath))
            {
                var im = (TextureImporter)AssetImporter.GetAtPath(path);
                if (im == null) continue;

                bool isFloorSheet = path.EndsWith("_floor_sheet_150x50.png", StringComparison.Ordinal);
                im.textureType = TextureImporterType.Sprite;
                im.spriteImportMode = isFloorSheet ? SpriteImportMode.Multiple : SpriteImportMode.Single;
                im.spritePixelsPerUnit = 50f;          // 1셀 = 1유닛
                im.filterMode = FilterMode.Bilinear;   // 원본이 부드럽게 확대되던 인상 유지
                im.mipmapEnabled = false;
                im.wrapMode = TextureWrapMode.Clamp;
                im.alphaIsTransparency = true;
                im.textureCompression = TextureImporterCompression.Uncompressed;
                if (!isFloorSheet) im.spritePivot = new Vector2(0.5f, 0.5f);

                if (isFloorSheet)
                {
                    // 150x50 가로 3칸
                    var meta = new List<SpriteMetaData>();
                    for (int i = 0; i < 3; i++)
                        meta.Add(new SpriteMetaData
                        {
                            name = $"floor_{i}",
                            rect = new Rect(i * 50, 0, 50, 50),
                            alignment = (int)SpriteAlignment.Center,
                            pivot = new Vector2(0.5f, 0.5f),
                        });
#pragma warning disable CS0618
                    im.spritesheet = meta.ToArray();
#pragma warning restore CS0618
                }

                im.SaveAndReimport();
                n++;
            }
            return n;
        }

        // ───────────────────────────── 캐릭터: 224x224 셀, 8열 x 2행
        static int ConfigureCharacters()
        {
            int n = 0;
            foreach (string roleDir in Directory.GetDirectories(CharDir))
            {
                string indexPath = Path.Combine(roleDir, "sheet-index.json").Replace('\\', '/');
                if (!File.Exists(indexPath)) continue;
                var index = JsonUtility.FromJson<SheetIndex>(File.ReadAllText(indexPath));

                foreach (var sheet in index.sheets)
                {
                    string path = Path.Combine(roleDir, sheet.file).Replace('\\', '/');
                    var im = (TextureImporter)AssetImporter.GetAtPath(path);
                    if (im == null) continue;

                    var m = sheet.meta;
                    int cw = m?.cellWidth > 0 ? m.cellWidth : 224;
                    int ch = m?.cellHeight > 0 ? m.cellHeight : 224;
                    int cols = m?.columns > 0 ? m.columns : 8;
                    int rows = m?.rows > 0 ? m.rows : 2;
                    int frames = m?.frames > 0 ? m.frames : cols * rows;

                    // 원본은 캐릭터를 반지름의 5.525배 크기로 그린다.
                    // R_SHELLY=0.5셀 이므로 화면 높이 2.7625셀. 224px 셀이 그 크기가 되려면:
                    float ppu = ch / 2.7625f;

                    // 피벗: report 의 pivot_in_cell 은 좌상단 기준 픽셀. Unity 는 좌하단 기준 0~1.
                    Vector2 pivot = new Vector2(0.5f, 0.06f);
                    if (m?.pivotInCell != null && m.pivotInCell.Length >= 2)
                        pivot = new Vector2(m.pivotInCell[0] / cw, 1f - m.pivotInCell[1] / ch);

                    im.textureType = TextureImporterType.Sprite;
                    im.spriteImportMode = SpriteImportMode.Multiple;
                    im.spritePixelsPerUnit = ppu;
                    im.filterMode = FilterMode.Bilinear;
                    im.mipmapEnabled = false;
                    im.alphaIsTransparency = true;
                    im.textureCompression = TextureImporterCompression.Uncompressed;

                    var meta = new List<SpriteMetaData>();
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    int texH = tex != null ? tex.height : rows * ch;
                    for (int f = 0; f < frames; f++)
                    {
                        int cx = f % cols, cy = f / cols;
                        // 이미지 좌표는 위에서 아래, Unity Rect 는 아래에서 위
                        float ry = texH - (cy + 1) * ch;
                        meta.Add(new SpriteMetaData
                        {
                            name = $"{Path.GetFileNameWithoutExtension(sheet.file)}_{f:00}",
                            rect = new Rect(cx * cw, ry, cw, ch),
                            alignment = (int)SpriteAlignment.Custom,
                            pivot = pivot,
                        });
                    }
#pragma warning disable CS0618
                    im.spritesheet = meta.ToArray();
#pragma warning restore CS0618
                    im.SaveAndReimport();
                    n++;
                }
            }
            return n;
        }


        // ───────────────────────────── 몬스터: 256x256 단일 프레임 16장 × 3종
        // 실제 화면 크기는 EnemyView 가 반지름(e.r*3.15)에 맞춰 스케일하므로 PPU 는 기준값만 준다.
        static int ConfigureMonsters() => ConfigureFrames(MonsterDir);
        static int ConfigureFrames(string dir)
        {
            if (!Directory.Exists(dir)) return 0;
            int n = 0;
            foreach (string path in AssetDatabase.FindAssets("t:Texture2D", new[] { dir })
                                                 .Select(AssetDatabase.GUIDToAssetPath))
            {
                var im = (TextureImporter)AssetImporter.GetAtPath(path);
                if (im == null) continue;
                im.textureType = TextureImporterType.Sprite;
                im.spriteImportMode = SpriteImportMode.Single;
                im.spritePixelsPerUnit = 128f;      // 256px → 2셀 기준
                im.spritePivot = new Vector2(0.5f, 0.5f);
                im.filterMode = FilterMode.Bilinear;
                im.mipmapEnabled = false;
                im.alphaIsTransparency = true;
                im.textureCompression = TextureImporterCompression.Uncompressed;
                im.SaveAndReimport();
                n++;
            }
            return n;
        }

        static void BuildMonsterSheets()
        {
            if (!Directory.Exists(MonsterDir)) return;
            string assetPath = $"{OutDir}/MonsterSheets.asset";
            var so = AssetDatabase.LoadAssetAtPath<MonsterSheetAsset>(assetPath);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<MonsterSheetAsset>();
                AssetDatabase.CreateAsset(so, assetPath);
            }
            var kinds = new List<MonsterSheetAsset.Kind>();
            var dirs = Directory.GetDirectories(MonsterDir).Select(d => (d, id: Path.GetFileName(d))).ToList();
            if (Directory.Exists(DragonDir)) dirs.AddRange(Directory.GetDirectories(DragonDir).Select(d => (d, id: "dragon_" + Path.GetFileName(d))));
            foreach (var (dir, id) in dirs)
            {
                var frames = Directory.GetFiles(dir, "frame_*.png")
                    .OrderBy(f => f, StringComparer.Ordinal)
                    .Select(f => AssetDatabase.LoadAssetAtPath<Sprite>(f.Replace('\\', '/')))
                    .Where(s => s != null).ToArray();
                if (frames.Length > 0) kinds.Add(new MonsterSheetAsset.Kind { id = id, frames = frames });
            }
            so.kinds = kinds.ToArray();
            EditorUtility.SetDirty(so);
        }

        // ───────────────────────────── 캐릭터 시트 SO (런타임 조회용)
        static int BuildCharacterSheets()
        {
            int made = 0;
            foreach (string roleDir in Directory.GetDirectories(CharDir))
            {
                string indexPath = Path.Combine(roleDir, "sheet-index.json").Replace('\\', '/');
                if (!File.Exists(indexPath)) continue;
                var index = JsonUtility.FromJson<SheetIndex>(File.ReadAllText(indexPath));

                string assetPath = $"{OutDir}/Sheets_{index.role}.asset";
                var so = AssetDatabase.LoadAssetAtPath<CharacterSheetAsset>(assetPath);
                if (so == null)
                {
                    so = ScriptableObject.CreateInstance<CharacterSheetAsset>();
                    AssetDatabase.CreateAsset(so, assetPath);
                }
                so.role = index.role;

                var byDir = new Dictionary<string, CharacterSheetAsset.DirectionSet>();
                foreach (var sheet in index.sheets)
                {
                    string path = Path.Combine(roleDir, sheet.file).Replace('\\', '/');
                    var frames = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                        .OrderBy(x => x.name, StringComparer.Ordinal).ToArray();
                    if (!byDir.TryGetValue(sheet.direction, out var d))
                        byDir[sheet.direction] = d = new CharacterSheetAsset.DirectionSet { direction = sheet.direction };
                    if (sheet.action == "walk") d.walk = frames; else d.fall = frames;
                }
                so.directions = byDir.Values.ToArray();
                EditorUtility.SetDirty(so);
                made++;
            }
            return made;
        }

        // ───────────────────────────── 타일셋 SO
        static TileSetAsset BuildTileSet()
        {
            string indexPath = $"{TileDir}/tile-index.json";
            var index = JsonUtility.FromJson<TileIndex>(File.ReadAllText(indexPath));

            string assetPath = $"{OutDir}/TileSet_purple.asset";
            var set = AssetDatabase.LoadAssetAtPath<TileSetAsset>(assetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<TileSetAsset>();
                AssetDatabase.CreateAsset(set, assetPath);
            }
            set.slots = new Sprite[268];

            // 벽 아틀라스가 있으면 아틀라스 서브 스프라이트를 우선 — 벽 전체가 한 텍스처가 되어 Chunk 배칭 + 머티리얼 레벨 노멀맵이 가능하다
            var atlasSprites = new Dictionary<string, Sprite>();
            string atlasPath = $"{TileDir}/atlas/purple_walls_atlas.png";
            if (File.Exists(atlasPath))
                foreach (var s in AssetDatabase.LoadAllAssetsAtPath(atlasPath).OfType<Sprite>()) atlasSprites[s.name] = s;
            set.wallNormalAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TileDir}/atlas/purple_walls_atlas_n.png");

            foreach (var t in index.tiles)
            {
                if (t.slot < 0 || t.slot >= 268) continue;
                Sprite sp;
                if (!atlasSprites.TryGetValue(Path.GetFileNameWithoutExtension(t.file), out sp) || sp == null)
                    sp = AssetDatabase.LoadAssetAtPath<Sprite>($"{TileDir}/{t.file}");
                if (sp != null) set.slots[t.slot] = sp;
            }

            // 바닥 3변형 — 시트에서 잘라낸 서브 스프라이트
            if (!string.IsNullOrEmpty(index.floorSheet))
            {
                var subs = AssetDatabase.LoadAllAssetsAtPath($"{TileDir}/{index.floorSheet}")
                                        .OfType<Sprite>()
                                        .OrderBy(s => s.name)
                                        .ToArray();
                set.floorVariants = new Sprite[3];
                for (int i = 0; i < Math.Min(3, subs.Length); i++) set.floorVariants[i] = subs[i];
            }

            // 오버레이
            Sprite Ov(string file) => AssetDatabase.LoadAssetAtPath<Sprite>($"{TileDir}/overlays/{file}");
            set.coreBottomShadow = Ov("purple_core_bottom_shadow_50x11.png");
            set.coreSideByBand = new Sprite[4];
            for (int b = 0; b < 4; b++) set.coreSideByBand[b] = Ov($"purple_core_side_b{b}_50x13.png");
            set.seams = new Sprite[3];
            for (int i = 0; i < 3; i++) set.seams[i] = Ov($"purple_seam_{i}_50x6.png");

            EditorUtility.SetDirty(set);
            return set;
        }
    }
}
