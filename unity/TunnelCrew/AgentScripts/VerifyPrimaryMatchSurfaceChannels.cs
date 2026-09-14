var verifyChannelRoot = "Assets/_Project/Art/Environment/PrimaryMatchV2/Channels/";
var verifyChannelNames = new[]
{
    "floor_normal", "floor_ao", "floor_emission",
    "wall_top_normal", "wall_top_ao", "wall_top_emission",
    "wall_front_normal", "wall_front_ao", "wall_front_emission",
    "wall_rim_normal", "wall_rim_ao", "wall_rim_emission",
};
var verifyChannelLines = new System.Collections.Generic.List<string>();
var verifyChannelStyle = UnityEngine.Resources.Load<TunnelCrew.Presentation.Visual.OrganicEnvironmentStyle>(
    "Visual/OrganicEnvironmentStyle");
if (verifyChannelStyle == null) throw new System.Exception("OrganicEnvironmentStyle missing");
foreach (var verifyChannelName in verifyChannelNames)
{
    var verifyChannelPath = verifyChannelRoot + verifyChannelName + ".png";
    var verifyChannelImporter = UnityEditor.AssetImporter.GetAtPath(verifyChannelPath) as UnityEditor.TextureImporter;
    var verifyChannelTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(verifyChannelPath);
    if (verifyChannelImporter == null || verifyChannelTexture == null)
        throw new System.Exception("Channel verification failed: " + verifyChannelName);
    verifyChannelLines.Add(
        verifyChannelName + ";size=" + verifyChannelTexture.width + "x" + verifyChannelTexture.height +
        ";type=" + verifyChannelImporter.textureType +
        ";sRGB=" + verifyChannelImporter.sRGBTexture +
        ";alpha=" + verifyChannelImporter.alphaSource +
        ";filter=" + verifyChannelImporter.filterMode +
        ";compression=" + verifyChannelImporter.textureCompression);
}
var verifyChannelRefs = new[]
{
    verifyChannelStyle.floorNormal, verifyChannelStyle.floorAo, verifyChannelStyle.floorEmission,
    verifyChannelStyle.wallTopNormal, verifyChannelStyle.wallTopAo, verifyChannelStyle.wallTopEmission,
    verifyChannelStyle.wallFrontNormal, verifyChannelStyle.wallFrontAo, verifyChannelStyle.wallFrontEmission,
    verifyChannelStyle.wallRimNormal, verifyChannelStyle.wallRimAo, verifyChannelStyle.wallRimEmission,
};
verifyChannelLines.Insert(0,
    "pipeline=" + UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.GetType().FullName +
    ";refs=" + System.Linq.Enumerable.Count(verifyChannelRefs, texture => texture != null) + "/12" +
    ";floorAo=" + verifyChannelStyle.floorAoStrength +
    ";wallAo=" + verifyChannelStyle.wallAoStrength);
System.IO.File.WriteAllLines(
    System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", "surface-channel-verify.txt"),
    verifyChannelLines);
