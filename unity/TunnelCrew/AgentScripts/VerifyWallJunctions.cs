var verifyAssetPath = "Assets/_Project/Art/Environment/PrimaryMatchV2/wall_junctions.png";
var verifyImporter = UnityEditor.AssetImporter.GetAtPath(verifyAssetPath) as UnityEditor.TextureImporter;
if (verifyImporter == null) throw new System.Exception("verification importer missing");
var verifyFactory = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
verifyFactory.Init();
var verifyProvider = verifyFactory.GetSpriteEditorDataProviderFromObject(verifyImporter);
if (verifyProvider == null) throw new System.Exception("verification provider missing");
verifyProvider.InitSpriteEditorDataProvider();
var verifyRects = verifyProvider.GetSpriteRects();
var verifyStyle = UnityEngine.Resources.Load<TunnelCrew.Presentation.Visual.OrganicEnvironmentStyle>(
    "Visual/OrganicEnvironmentStyle");
if (verifyStyle == null) throw new System.Exception("verification style missing");
var verifyPipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
var verifyNames = new System.Collections.Generic.List<string>();
foreach (var verifySprite in verifyStyle.wallJunctions)
    verifyNames.Add(verifySprite == null ? "NULL" : verifySprite.name);
var verifyLines = new System.Collections.Generic.List<string>
{
    "pipeline=" + (verifyPipeline == null ? "BuiltIn" : verifyPipeline.GetType().FullName),
    "rects=" + verifyRects.Length,
    "style=" + verifyStyle.wallJunctions.Length,
    "names=" + string.Join("|", verifyNames),
    "ppu=" + verifyImporter.spritePixelsPerUnit,
    "filter=" + verifyImporter.filterMode,
    "mipmaps=" + verifyImporter.mipmapEnabled,
    "compression=" + verifyImporter.textureCompression,
};
foreach (var verifyRect in verifyRects)
    verifyLines.Add(verifyRect.name + "=" + verifyRect.rect + ";pivot=" + verifyRect.pivot +
        ";alignment=" + verifyRect.alignment);
System.IO.File.WriteAllLines(
    System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", "wall-junction-verify.txt"),
    verifyLines);
