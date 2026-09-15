var sourcePath = "Assets/_Project/Data/Visual/SetPieceCatalog_TestRoomV01.asset";
var candidatePath = "Assets/_Project/Data/Visual/SetPieceCatalog_PrimaryMatchBoldCandidate.asset";
var runtimePath = "Assets/_Project/Data/Resources/Visual/SetPieceCatalog_PrimaryMatchRuntime.asset";

var source = UnityEditor.AssetDatabase.LoadAssetAtPath<TunnelCrew.Presentation.Visual.SetPieceCatalog>(sourcePath);
var candidate = UnityEditor.AssetDatabase.LoadAssetAtPath<TunnelCrew.Presentation.Visual.SetPieceCatalog>(candidatePath);
if (candidate == null || candidate.Count != 38)
    throw new System.Exception("Primary Match candidate catalog must contain 38 safe entries before promotion.");

var merged = new System.Collections.Generic.List<TunnelCrew.Presentation.Visual.SetPieceDef>();
var byId = new System.Collections.Generic.Dictionary<string, TunnelCrew.Presentation.Visual.SetPieceDef>(
    System.StringComparer.OrdinalIgnoreCase);
if (source != null)
    foreach (var entry in source.Entries)
        if (entry != null && !string.IsNullOrEmpty(entry.assetId)) byId[entry.assetId] = entry;
foreach (var entry in candidate.Entries)
    if (entry != null && !string.IsNullOrEmpty(entry.assetId)) byId[entry.assetId] = entry;
foreach (var entry in byId.Values) merged.Add(entry);
merged.Sort((a, b) => string.Compare(a.assetId, b.assetId, System.StringComparison.OrdinalIgnoreCase));

System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(runtimePath));
var runtime = UnityEditor.AssetDatabase.LoadAssetAtPath<TunnelCrew.Presentation.Visual.SetPieceCatalog>(runtimePath);
if (runtime == null)
{
    runtime = UnityEngine.ScriptableObject.CreateInstance<TunnelCrew.Presentation.Visual.SetPieceCatalog>();
    UnityEditor.AssetDatabase.CreateAsset(runtime, runtimePath);
}
runtime.EditorSetEntries(merged);
UnityEditor.EditorUtility.SetDirty(runtime);
UnityEditor.AssetDatabase.SaveAssets();

var projectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, ".."));
System.IO.File.WriteAllLines(System.IO.Path.Combine(projectRoot, "primary-match-bold-runtime-promotion.txt"), new[]
{
    "status=promoted_to_runtime_catalog",
    "candidateEntries=" + candidate.Count,
    "legacyEntries=" + (source != null ? source.Count : 0),
    "runtimeEntries=" + runtime.Count,
    "runtimeCatalog=" + runtimePath,
    "sceneChanged=false",
    "prefabChanged=false",
});
UnityEngine.Debug.Log("[Primary Match] 런타임 카탈로그 승격 완료: " + runtime.Count + " entries.");
