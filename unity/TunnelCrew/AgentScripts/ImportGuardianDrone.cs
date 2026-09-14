var path = "Assets/_Project/Data/Resources/Visual/guardian_drone.png";
UnityEditor.AssetDatabase.ImportAsset(path,
    UnityEditor.ImportAssetOptions.ForceSynchronousImport | UnityEditor.ImportAssetOptions.ForceUpdate);
var importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
if (importer == null) throw new System.Exception("guardian_drone TextureImporter missing");
importer.textureType = UnityEditor.TextureImporterType.Sprite;
importer.spriteImportMode = UnityEditor.SpriteImportMode.Single;
importer.spritePixelsPerUnit = 512f;
importer.spritePivot = new UnityEngine.Vector2(.5f, .5f);
importer.alphaSource = UnityEditor.TextureImporterAlphaSource.FromInput;
importer.alphaIsTransparency = true;
importer.sRGBTexture = true;
importer.filterMode = UnityEngine.FilterMode.Point;
importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
importer.mipmapEnabled = false;
importer.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed;
importer.maxTextureSize = 2048;
importer.SaveAndReimport();
var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(path);
if (sprite == null) throw new System.Exception("guardian_drone Sprite missing after import");
System.IO.File.WriteAllText(
    System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", "guardian-drone-import.txt"),
    "size=" + sprite.texture.width + "x" + sprite.texture.height +
    ";ppu=" + sprite.pixelsPerUnit + ";bounds=" + sprite.bounds.size +
    ";filter=" + importer.filterMode + ";alpha=" + importer.alphaSource);
