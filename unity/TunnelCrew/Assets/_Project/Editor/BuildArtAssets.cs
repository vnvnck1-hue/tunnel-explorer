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
            BuildMonsterSheets();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[M1] 아트 설정 완료 — 타일 텍스처 {tiles}, 캐릭터 시트 {chars}, " +
                      $"타일셋 슬롯 {set.slots.Count(s => s != null)}/268, 바닥 {set.floorVariants.Count(s => s != null)}/3, 직업 시트 SO {sheets}, 몬스터 프레임 {monsters}");
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
        static int ConfigureMonsters()
        {
            if (!Directory.Exists(MonsterDir)) return 0;
            int n = 0;
            foreach (string path in AssetDatabase.FindAssets("t:Texture2D", new[] { MonsterDir })
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
            foreach (string dir in Directory.GetDirectories(MonsterDir))
            {
                string id = Path.GetFileName(dir);
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

            foreach (var t in index.tiles)
            {
                if (t.slot < 0 || t.slot >= 268) continue;
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>($"{TileDir}/{t.file}");
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
