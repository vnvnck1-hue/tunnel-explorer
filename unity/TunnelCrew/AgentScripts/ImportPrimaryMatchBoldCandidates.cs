var projectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, ".."));
var planPath = System.IO.Path.Combine(projectRoot, "AgentScripts", "primary-match-bold-import-plan.json");
var receiptPath = System.IO.Path.Combine(projectRoot, "AgentScripts", "primary-match-bold-stage-receipt.json");
if (!System.IO.File.Exists(planPath)) throw new System.Exception("Primary Match import plan missing: " + planPath);
if (!System.IO.File.Exists(receiptPath)) throw new System.Exception(
    "Candidate files are not staged. Run tools/art/stage-primary-match-bold-candidates.ps1 -Apply only after coordinating Editor access.");

var planRoot = TunnelCrew.EditorTools.ArtPipeline.MiniJson.AsMap(
    TunnelCrew.EditorTools.ArtPipeline.MiniJson.Parse(System.IO.File.ReadAllText(planPath), out var planError));
if (planRoot == null || planError != null) throw new System.Exception("Import plan parse failed: " + planError);
var receiptRoot = TunnelCrew.EditorTools.ArtPipeline.MiniJson.AsMap(
    TunnelCrew.EditorTools.ArtPipeline.MiniJson.Parse(System.IO.File.ReadAllText(receiptPath), out var receiptError));
if (receiptRoot == null || receiptError != null) throw new System.Exception("Stage receipt parse failed: " + receiptError);
if (TunnelCrew.EditorTools.ArtPipeline.MiniJson.GetString(planRoot, "status") != "dormant_candidate_plan_not_staged")
    throw new System.Exception("Unexpected import-plan status");
if (TunnelCrew.EditorTools.ArtPipeline.MiniJson.GetString(receiptRoot, "status") != "staged_candidate_assets_not_import_verified")
    throw new System.Exception("Unexpected stage-receipt status");

var assets = TunnelCrew.EditorTools.ArtPipeline.MiniJson.AsList(planRoot["assets"]);
if (assets == null || assets.Count != 39) throw new System.Exception("Expected 39 candidate assets");
var targetRoot = TunnelCrew.EditorTools.ArtPipeline.MiniJson.GetString(planRoot, "targetRoot");
var materialRoot = TunnelCrew.EditorTools.ArtPipeline.MiniJson.GetString(planRoot, "candidateMaterialRoot");
var catalogPath = TunnelCrew.EditorTools.ArtPipeline.MiniJson.GetString(planRoot, "candidateCatalogPath");
System.IO.Directory.CreateDirectory(targetRoot);
System.IO.Directory.CreateDirectory(materialRoot);
System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(catalogPath));

// The staging script has already copied exact hash-checked PNGs. Import them as a batch first.
foreach (var rawAsset in assets)
{
    var asset = TunnelCrew.EditorTools.ArtPipeline.MiniJson.AsMap(rawAsset);
    var targets = TunnelCrew.EditorTools.ArtPipeline.MiniJson.AsMap(asset["targetAssetPaths"]);
    foreach (var channel in new[] { "albedo", "normal", "ao", "emission", "mask" })
    {
        var path = TunnelCrew.EditorTools.ArtPipeline.MiniJson.GetString(targets, channel);
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(System.IO.Path.Combine(projectRoot, path)))
            throw new System.Exception("Staged candidate channel missing: " + path);
        UnityEditor.AssetDatabase.ImportAsset(path,
            UnityEditor.ImportAssetOptions.ForceSynchronousImport | UnityEditor.ImportAssetOptions.ForceUpdate);
    }
}

int importedChannels = 0;
foreach (var rawAsset in assets)
{
    var asset = TunnelCrew.EditorTools.ArtPipeline.MiniJson.AsMap(rawAsset);
    var targets = TunnelCrew.EditorTools.ArtPipeline.MiniJson.AsMap(asset["targetAssetPaths"]);
    TunnelCrew.EditorTools.ArtPipeline.MiniJson.TryGetFloat2(asset, "pivotNormalizedBottomOrigin", out var pivotX, out var pivotY);

    foreach (var channel in new[] { "albedo", "normal", "ao", "emission", "mask" })
    {
        var path = TunnelCrew.EditorTools.ArtPipeline.MiniJson.GetString(targets, channel);
        var importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
        if (importer == null) throw new System.Exception("TextureImporter missing: " + path);
        var expected = TunnelCrew.EditorTools.ArtPipeline.ImportExpectation.For(channel);
        expected.ApplyTo(importer);
        if (channel == "albedo")
        {
            var settings = new UnityEditor.TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)UnityEngine.SpriteAlignment.Custom;
            settings.spritePivot = new UnityEngine.Vector2(pivotX, pivotY);
            settings.spriteMeshType = UnityEngine.SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }
        importer.SaveAndReimport();
        importedChannels++;
    }
}

var defs = new System.Collections.Generic.List<TunnelCrew.Presentation.Visual.SetPieceDef>();
int lightSocketCount = 0;
int contourCount = 0;
foreach (var rawAsset in assets)
{
    var asset = TunnelCrew.EditorTools.ArtPipeline.MiniJson.AsMap(rawAsset);
    if (!TunnelCrew.EditorTools.ArtPipeline.MiniJson.GetBool(asset, "catalogEligible")) continue;

    var assetId = TunnelCrew.EditorTools.ArtPipeline.MiniJson.GetString(asset, "assetId");
    var targets = TunnelCrew.EditorTools.ArtPipeline.MiniJson.AsMap(asset["targetAssetPaths"]);
    var runtime = TunnelCrew.EditorTools.ArtPipeline.MiniJson.AsMap(asset["runtimeCandidate"]);
    var albedoPath = TunnelCrew.EditorTools.ArtPipeline.MiniJson.GetString(targets, "albedo");
    var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(albedoPath);
    if (sprite == null) throw new System.Exception("Candidate sprite missing after import: " + albedoPath);

    var safeId = assetId.ToLowerInvariant().Replace('-', '_');
    var setPath = materialRoot + "/SurfaceMaterialSet_" + safeId + ".asset";
    var materialSet = UnityEditor.AssetDatabase.LoadAssetAtPath<TunnelCrew.Presentation.Visual.SurfaceMaterialSet>(setPath);
    if (materialSet == null)
    {
        materialSet = UnityEngine.ScriptableObject.CreateInstance<TunnelCrew.Presentation.Visual.SurfaceMaterialSet>();
        UnityEditor.AssetDatabase.CreateAsset(materialSet, setPath);
    }
    materialSet.kind = TunnelCrew.Presentation.Visual.SurfaceMaterialSet.Kind.World;
    materialSet.minLightSlot = TunnelCrew.Presentation.Visual.MinLightSlot.WallFront;
    materialSet.normal = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(
        TunnelCrew.EditorTools.ArtPipeline.MiniJson.GetString(targets, "normal"));
    materialSet.ao = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(
        TunnelCrew.EditorTools.ArtPipeline.MiniJson.GetString(targets, "ao"));
    materialSet.emission = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(
        TunnelCrew.EditorTools.ArtPipeline.MiniJson.GetString(targets, "emission"));
    materialSet.materialMask = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(
        TunnelCrew.EditorTools.ArtPipeline.MiniJson.GetString(targets, "mask"));
    materialSet.normalStrength = 1f;
    materialSet.aoStrength = 1f;
    materialSet.emissionIntensity = 1f;
    materialSet.minLightOverride = -1f;
    UnityEditor.EditorUtility.SetDirty(materialSet);

    TunnelCrew.EditorTools.ArtPipeline.MiniJson.TryGetInt2(runtime, "footprintCells", out var footprintX, out var footprintY);
    TunnelCrew.EditorTools.ArtPipeline.MiniJson.TryGetFloat(runtime, "visualHeightCells", out var visualHeight);
    TunnelCrew.EditorTools.ArtPipeline.MiniJson.TryGetInt(runtime, "localOrder", out var localOrder);
    TunnelCrew.EditorTools.ArtPipeline.MiniJson.TryGetFloat(runtime, "fadeTargetAlpha", out var fadeAlpha);
    TunnelCrew.EditorTools.ArtPipeline.MiniJson.TryGetFloat(runtime, "contactShadowRadius", out var contactRadius);

    var sockets = new System.Collections.Generic.List<TunnelCrew.Presentation.Visual.LightSocketDef>();
    var rawSockets = TunnelCrew.EditorTools.ArtPipeline.MiniJson.AsList(runtime["lightSockets"]);
    if (rawSockets != null)
    {
        foreach (var rawSocket in rawSockets)
        {
            var socket = TunnelCrew.EditorTools.ArtPipeline.MiniJson.AsMap(rawSocket);
            TunnelCrew.EditorTools.ArtPipeline.MiniJson.TryGetFloat2(socket, "offsetCells", out var offsetX, out var offsetY);
            TunnelCrew.EditorTools.ArtPipeline.MiniJson.TryGetFloat(socket, "rangeCells", out var range);
            TunnelCrew.EditorTools.ArtPipeline.MiniJson.TryGetFloat(socket, "intensity", out var intensity);
            var colorValues = TunnelCrew.EditorTools.ArtPipeline.MiniJson.AsList(socket["color"]);
            var color = colorValues != null && colorValues.Count >= 3
                ? new UnityEngine.Color(System.Convert.ToSingle(colorValues[0]), System.Convert.ToSingle(colorValues[1]),
                                        System.Convert.ToSingle(colorValues[2]), colorValues.Count >= 4 ? System.Convert.ToSingle(colorValues[3]) : 1f)
                : UnityEngine.Color.white;
            var className = TunnelCrew.EditorTools.ArtPipeline.MiniJson.GetString(socket, "lightClass", "Indicator");
            if (!System.Enum.TryParse<TunnelCrew.Presentation.Visual.LightClass>(className, out var lightClass))
                lightClass = TunnelCrew.Presentation.Visual.LightClass.Indicator;
            sockets.Add(new TunnelCrew.Presentation.Visual.LightSocketDef
            {
                id = TunnelCrew.EditorTools.ArtPipeline.MiniJson.GetString(socket, "id", "socket"),
                offsetCells = new UnityEngine.Vector2(offsetX, offsetY),
                color = color,
                rangeCells = range,
                intensity = intensity,
                lightClass = lightClass,
            });
        }
    }

    float[] contour = null;
    var contourValues = TunnelCrew.EditorTools.ArtPipeline.MiniJson.AsList(runtime["shadowContourCells"]);
    if (contourValues != null && contourValues.Count >= 6 && contourValues.Count % 2 == 0)
    {
        contour = new float[contourValues.Count];
        for (var contourIndex = 0; contourIndex < contourValues.Count; contourIndex++)
            contour[contourIndex] = System.Convert.ToSingle(contourValues[contourIndex]);
    }
    if (contour != null && contour.Length >= 6) contourCount++;
    lightSocketCount += sockets.Count;
    defs.Add(new TunnelCrew.Presentation.Visual.SetPieceDef
    {
        assetId = assetId,
        sprite = sprite,
        materials = materialSet,
        footprintCells = new UnityEngine.Vector2Int(System.Math.Max(1, footprintX), System.Math.Max(1, footprintY)),
        visualHeightCells = visualHeight,
        sortingLayer = TunnelCrew.EditorTools.ArtPipeline.MiniJson.GetString(runtime, "sortingLayer", "WorldEntity"),
        localOrder = localOrder,
        occluderGroup = TunnelCrew.EditorTools.ArtPipeline.MiniJson.GetString(runtime, "occluderGroup"),
        fadeTargetAlpha = fadeAlpha,
        lightSockets = sockets.ToArray(),
        contactShadowRadius = contactRadius,
        shadowContourCells = contour,
        replacementAssetId = TunnelCrew.EditorTools.ArtPipeline.MiniJson.GetString(runtime, "replacementAssetId"),
    });
}

var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<TunnelCrew.Presentation.Visual.SetPieceCatalog>(catalogPath);
if (catalog == null)
{
    catalog = UnityEngine.ScriptableObject.CreateInstance<TunnelCrew.Presentation.Visual.SetPieceCatalog>();
    UnityEditor.AssetDatabase.CreateAsset(catalog, catalogPath);
}
catalog.EditorSetEntries(defs);
UnityEditor.EditorUtility.SetDirty(catalog);
UnityEditor.AssetDatabase.SaveAssets();

// Deliberately do not assign this catalog to SetPieceSpawner or change any scene/prefab.
var reportPath = System.IO.Path.Combine(projectRoot, "primary-match-bold-import-report.txt");
System.IO.File.WriteAllLines(reportPath, new[]
{
    "status=imported_candidate_not_runtime_connected",
    "channels=" + importedChannels,
    "catalogEntries=" + defs.Count,
    "lightSockets=" + lightSocketCount,
    "shadowContours=" + contourCount,
    "catalog=" + catalogPath,
    "sceneChanged=false",
    "prefabChanged=false",
    "activeRuntimeCatalogChanged=false",
});
UnityEngine.Debug.Log("[Primary Match Bold] Candidate import complete: " + importedChannels +
                      " channels, " + defs.Count + " catalog entries, " + lightSocketCount +
                      " light sockets, " + contourCount + " contours. Active runtime remains unchanged.");
