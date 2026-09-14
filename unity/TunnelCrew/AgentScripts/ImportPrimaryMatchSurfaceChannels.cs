var floorAlbedoPath = "Assets/_Project/Art/Environment/PrimaryMatchV2/floor_macro.png";
UnityEditor.AssetDatabase.ImportAsset(floorAlbedoPath,
    UnityEditor.ImportAssetOptions.ForceSynchronousImport | UnityEditor.ImportAssetOptions.ForceUpdate);

var channelRoot = "Assets/_Project/Art/Environment/PrimaryMatchV2/Channels/";
var channelNames = new[]
{
    "floor_normal", "floor_ao", "floor_emission",
    "wall_top_normal", "wall_top_ao", "wall_top_emission",
    "wall_front_normal", "wall_front_ao", "wall_front_emission",
    "wall_rim_normal", "wall_rim_ao", "wall_rim_emission",
};

foreach (var channelName in channelNames)
{
    var channelPath = channelRoot + channelName + ".png";
    UnityEditor.AssetDatabase.ImportAsset(channelPath,
        UnityEditor.ImportAssetOptions.ForceSynchronousImport | UnityEditor.ImportAssetOptions.ForceUpdate);
    var channelImporter = UnityEditor.AssetImporter.GetAtPath(channelPath) as UnityEditor.TextureImporter;
    if (channelImporter == null) throw new System.Exception("TextureImporter missing: " + channelPath);
    var isNormal = channelName.EndsWith("_normal", System.StringComparison.Ordinal);
    var isEmission = channelName.EndsWith("_emission", System.StringComparison.Ordinal);
    channelImporter.textureType = isNormal
        ? UnityEditor.TextureImporterType.NormalMap
        : UnityEditor.TextureImporterType.Default;
    channelImporter.sRGBTexture = isEmission;
    channelImporter.alphaSource = isEmission
        ? UnityEditor.TextureImporterAlphaSource.FromInput
        : UnityEditor.TextureImporterAlphaSource.None;
    channelImporter.alphaIsTransparency = false;
    channelImporter.filterMode = UnityEngine.FilterMode.Bilinear;
    channelImporter.wrapMode = UnityEngine.TextureWrapMode.Repeat;
    channelImporter.mipmapEnabled = false;
    channelImporter.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed;
    channelImporter.maxTextureSize = 2048;
    channelImporter.SaveAndReimport();
}

var channelStyle = UnityEngine.Resources.Load<TunnelCrew.Presentation.Visual.OrganicEnvironmentStyle>(
    "Visual/OrganicEnvironmentStyle");
if (channelStyle == null) throw new System.Exception("OrganicEnvironmentStyle was not found");
UnityEngine.Texture2D LoadChannel(string name)
{
    var texture = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(channelRoot + name + ".png");
    if (texture == null) throw new System.Exception("Channel texture missing after import: " + name);
    return texture;
}
channelStyle.floorNormal = LoadChannel("floor_normal");
channelStyle.floorAo = LoadChannel("floor_ao");
channelStyle.floorEmission = LoadChannel("floor_emission");
channelStyle.wallTopNormal = LoadChannel("wall_top_normal");
channelStyle.wallTopAo = LoadChannel("wall_top_ao");
channelStyle.wallTopEmission = LoadChannel("wall_top_emission");
channelStyle.wallFrontNormal = LoadChannel("wall_front_normal");
channelStyle.wallFrontAo = LoadChannel("wall_front_ao");
channelStyle.wallFrontEmission = LoadChannel("wall_front_emission");
channelStyle.wallRimNormal = LoadChannel("wall_rim_normal");
channelStyle.wallRimAo = LoadChannel("wall_rim_ao");
channelStyle.wallRimEmission = LoadChannel("wall_rim_emission");
channelStyle.floorMacroSizeCells = 18f;
channelStyle.floorAoStrength = 0.48f;
channelStyle.wallAoStrength = 0.62f;
UnityEditor.EditorUtility.SetDirty(channelStyle);
UnityEditor.AssetDatabase.SaveAssets();
