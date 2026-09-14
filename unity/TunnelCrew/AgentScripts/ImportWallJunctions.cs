var assetPath = "Assets/_Project/Art/Environment/PrimaryMatchV2/wall_junctions.png";
UnityEditor.AssetDatabase.ImportAsset(assetPath,
    UnityEditor.ImportAssetOptions.ForceSynchronousImport | UnityEditor.ImportAssetOptions.ForceUpdate);

var importer = UnityEditor.AssetImporter.GetAtPath(assetPath) as UnityEditor.TextureImporter;
if (importer == null) throw new System.Exception("wall_junctions TextureImporter was not found");
importer.textureType = UnityEditor.TextureImporterType.Sprite;
importer.spriteImportMode = UnityEditor.SpriteImportMode.Multiple;
importer.spritePixelsPerUnit = 418f;
importer.filterMode = UnityEngine.FilterMode.Bilinear;
importer.mipmapEnabled = false;
importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
importer.alphaIsTransparency = true;
importer.sRGBTexture = true;
importer.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed;
importer.maxTextureSize = 2048;
importer.SaveAndReimport();

var factory = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
factory.Init();
var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
if (provider == null) throw new System.Exception("wall_junctions Sprite Editor data provider was not found");
provider.InitSpriteEditorDataProvider();

var editCapability = provider.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteFrameEditCapability>();
if (editCapability == null)
    throw new System.Exception("wall_junctions edit capability is unavailable; slicing aborted");
var capability = editCapability.GetEditCapability();
var required = new[]
{
    UnityEditor.U2D.Sprites.EEditCapability.CreateAndDeleteSprite,
    UnityEditor.U2D.Sprites.EEditCapability.EditSpriteName,
    UnityEditor.U2D.Sprites.EEditCapability.EditSpriteRect,
    UnityEditor.U2D.Sprites.EEditCapability.EditPivot,
};
foreach (var item in required)
    if (!capability.HasCapability(item))
        throw new System.Exception("wall_junctions capability missing: " + item + "; slicing aborted");

var names = new[]
{
    "wall_junction_magenta_left", "wall_junction_magenta_vertical", "wall_junction_magenta_right",
    "wall_junction_cyan_left", "wall_junction_cyan_vertical", "wall_junction_cyan_right",
    "wall_junction_amber_left", "wall_junction_amber_vertical", "wall_junction_amber_right",
};
var existing = provider.GetSpriteRects();
var rects = new UnityEditor.SpriteRect[9];
for (int row = 0; row < 3; row++)
for (int col = 0; col < 3; col++)
{
    int index = row * 3 + col;
    var id = UnityEditor.GUID.Generate();
    foreach (var oldRect in existing)
        if (oldRect.name == names[index]) { id = oldRect.spriteID; break; }
    rects[index] = new UnityEditor.SpriteRect
    {
        name = names[index],
        rect = new UnityEngine.Rect(col * 418, (2 - row) * 418, 418, 418),
        alignment = UnityEngine.SpriteAlignment.Custom,
        pivot = new UnityEngine.Vector2(.5f, .5f),
        border = UnityEngine.Vector4.zero,
        spriteID = id,
    };
}
provider.SetSpriteRects(rects);
var nameIds = provider.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteNameFileIdDataProvider>();
if (nameIds != null)
{
    var pairs = new System.Collections.Generic.List<UnityEditor.SpriteNameFileIdPair>(rects.Length);
    foreach (var rect in rects)
        pairs.Add(new UnityEditor.SpriteNameFileIdPair(rect.name, rect.spriteID));
    nameIds.SetNameFileIdPairs(pairs);
}
provider.Apply();
importer.SaveAndReimport();

var loaded = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath);
var sprites = new UnityEngine.Sprite[9];
foreach (var loadedObject in loaded)
{
    var sprite = loadedObject as UnityEngine.Sprite;
    if (sprite == null) continue;
    int index = System.Array.IndexOf(names, sprite.name);
    if (index >= 0) sprites[index] = sprite;
}
for (int i = 0; i < sprites.Length; i++)
    if (sprites[i] == null) throw new System.Exception("wall_junctions sprite missing after import: " + names[i]);

var style = UnityEngine.Resources.Load<TunnelCrew.Presentation.Visual.OrganicEnvironmentStyle>(
    "Visual/OrganicEnvironmentStyle");
if (style == null) throw new System.Exception("OrganicEnvironmentStyle was not found");
style.wallJunctions = sprites;
UnityEditor.EditorUtility.SetDirty(style);
UnityEditor.AssetDatabase.SaveAssets();

UnityEngine.Debug.Log("wallJunctions=9; ppu=" + importer.spritePixelsPerUnit +
    "; filter=" + importer.filterMode + "; mipmaps=" + importer.mipmapEnabled +
    "; compression=" + importer.textureCompression);
