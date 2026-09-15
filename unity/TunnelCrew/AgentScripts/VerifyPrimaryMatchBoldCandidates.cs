var verifyProjectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, ".."));
var verifyPlanPath = System.IO.Path.Combine(verifyProjectRoot, "AgentScripts", "primary-match-bold-import-plan.json");
var verifyPlan = TunnelCrew.EditorTools.ArtPipeline.MiniJson.AsMap(
    TunnelCrew.EditorTools.ArtPipeline.MiniJson.Parse(System.IO.File.ReadAllText(verifyPlanPath), out var verifyPlanError));
if (verifyPlan == null || verifyPlanError != null) throw new System.Exception("Import plan parse failed: " + verifyPlanError);
var verifyAssets = TunnelCrew.EditorTools.ArtPipeline.MiniJson.AsList(verifyPlan["assets"]);
if (verifyAssets == null || verifyAssets.Count != 39) throw new System.Exception("Expected 39 planned assets");

var verifyLines = new System.Collections.Generic.List<string>();
int verifiedChannels = 0;
int verifiedSprites = 0;
foreach (var rawVerifyAsset in verifyAssets)
{
    var asset = TunnelCrew.EditorTools.ArtPipeline.MiniJson.AsMap(rawVerifyAsset);
    var assetId = TunnelCrew.EditorTools.ArtPipeline.MiniJson.GetString(asset, "assetId");
    var targets = TunnelCrew.EditorTools.ArtPipeline.MiniJson.AsMap(asset["targetAssetPaths"]);
    TunnelCrew.EditorTools.ArtPipeline.MiniJson.TryGetInt2(asset, "dimensionsPixels", out var expectedWidth, out var expectedHeight);
    TunnelCrew.EditorTools.ArtPipeline.MiniJson.TryGetFloat2(asset, "pivotNormalizedBottomOrigin", out var expectedPivotX, out var expectedPivotY);

    foreach (var channel in new[] { "albedo", "normal", "ao", "emission", "mask" })
    {
        var path = TunnelCrew.EditorTools.ArtPipeline.MiniJson.GetString(targets, channel);
        var importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
        var texture = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(path);
        if (importer == null || texture == null) throw new System.Exception("Imported channel missing: " + path);
        var mismatch = TunnelCrew.EditorTools.ArtPipeline.ImportExpectation.For(channel).DescribeMismatch(importer);
        if (!string.IsNullOrEmpty(mismatch)) throw new System.Exception(path + " import mismatch: " + mismatch);
        if (texture.width != expectedWidth || texture.height != expectedHeight)
            throw new System.Exception(path + " dimensions " + texture.width + "x" + texture.height +
                                       " expected " + expectedWidth + "x" + expectedHeight);
        verifiedChannels++;
    }

    var albedoPath = TunnelCrew.EditorTools.ArtPipeline.MiniJson.GetString(targets, "albedo");
    var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(albedoPath);
    if (sprite == null) throw new System.Exception("Sprite missing: " + albedoPath);
    var actualPivot = new UnityEngine.Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
    if (UnityEngine.Vector2.Distance(actualPivot, new UnityEngine.Vector2(expectedPivotX, expectedPivotY)) > 0.005f)
        throw new System.Exception(assetId + " pivot mismatch: " + actualPivot +
                                   " expected " + new UnityEngine.Vector2(expectedPivotX, expectedPivotY));
    verifiedSprites++;
}

var verifyCatalogPath = TunnelCrew.EditorTools.ArtPipeline.MiniJson.GetString(verifyPlan, "candidateCatalogPath");
var verifyCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<TunnelCrew.Presentation.Visual.SetPieceCatalog>(verifyCatalogPath);
if (verifyCatalog == null) throw new System.Exception("Candidate catalog missing: " + verifyCatalogPath);
if (verifyCatalog.Count != 38) throw new System.Exception("Candidate catalog count " + verifyCatalog.Count + " expected 38");
int verifySockets = 0;
int verifyContours = 0;
foreach (var entry in verifyCatalog.Entries)
{
    if (entry == null || entry.sprite == null || entry.materials == null)
        throw new System.Exception("Candidate catalog has an incomplete entry");
    verifySockets += entry.lightSockets != null ? entry.lightSockets.Length : 0;
    if (entry.shadowContourCells != null && entry.shadowContourCells.Length >= 6) verifyContours++;
}
if (verifySockets != 11) throw new System.Exception("Candidate light socket count " + verifySockets + " expected 11");
if (verifyContours != 14) throw new System.Exception("Candidate contour count " + verifyContours + " expected 14");

int activeAssignments = 0;
foreach (var spawner in UnityEngine.Resources.FindObjectsOfTypeAll<TunnelCrew.Presentation.Visual.SetPieceSpawner>())
    if (spawner != null && spawner.Catalog == verifyCatalog) activeAssignments++;
if (activeAssignments != 0) throw new System.Exception(
    "Candidate catalog was assigned to " + activeAssignments + " spawner(s); import verification must not change active runtime");

verifyLines.Add("status=verified_candidate_import_not_runtime_connected");
verifyLines.Add("channels=" + verifiedChannels);
verifyLines.Add("sprites=" + verifiedSprites);
verifyLines.Add("catalogEntries=" + verifyCatalog.Count);
verifyLines.Add("lightSockets=" + verifySockets);
verifyLines.Add("shadowContours=" + verifyContours);
verifyLines.Add("activeRuntimeAssignments=" + activeAssignments);
System.IO.File.WriteAllLines(
    System.IO.Path.Combine(verifyProjectRoot, "primary-match-bold-import-verify.txt"), verifyLines);
UnityEngine.Debug.Log("[Primary Match Bold] Candidate import verified: " + verifiedChannels +
                      " channels, " + verifiedSprites + " sprites, " + verifyCatalog.Count +
                      " catalog entries; active runtime assignments 0.");
