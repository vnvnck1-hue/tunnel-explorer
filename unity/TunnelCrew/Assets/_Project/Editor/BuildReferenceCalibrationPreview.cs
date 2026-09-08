using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TunnelCrew.EditorTools
{
    /// <summary>
    /// One-shot, non-destructive assembly check for the reference-style minimum art set.
    /// The open user scene remains open; the preview is built additively, captured, and closed.
    /// </summary>
    [InitializeOnLoad]
    public static class BuildReferenceCalibrationPreview
    {
        const string ArtDir = "Assets/Art/Visual/ReferenceCalibrationV1";
        const string ScenePath = "Assets/_Project/Scenes/ReferenceCalibrationV1.unity";
        const string RequestFile = "Temp/reference-calibration-v1.request";
        static readonly string[] FloorFiles =
        {
            "tr01_reference_floor_a_albedo.png",
            "tr01_reference_floor_b_albedo.png",
            "tr01_reference_floor_c_albedo.png",
            "tr01_reference_floor_d_albedo.png",
            "tr01_reference_floor_e_albedo.png",
            "tr01_reference_floor_f_albedo.png"
        };
        static bool s_running;

        static BuildReferenceCalibrationPreview()
        {
            EditorApplication.delayCall += TryRunRequestedBuild;
        }

        [MenuItem("Tunnel Crew/비주얼 · 레퍼런스 캘리브레이션 V1 캡처", priority = 25)]
        public static void RunFromMenu() => BuildAndCapture();

        static void TryRunRequestedBuild()
        {
            string request = Path.Combine(ProjectRoot(), RequestFile);
            if (s_running || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(request))
                return;

            s_running = true;
            try
            {
                BuildAndCapture();
                File.Delete(request);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                s_running = false;
            }
        }

        public static void BuildAndCapture()
        {
            foreach (string floorFile in FloorFiles)
                ConfigureSprite(floorFile, new Vector2(0.5f, 0.5f), false);
            ConfigureSprite("tr01_reference_wall_a_albedo.png", new Vector2(0.5f, 0.02f), true);
            ConfigureSprite("tr01_reference_crystal_a_albedo.png", new Vector2(0.5f, 0.03f), true);
            ConfigureSprite("tr01_reference_driller_a_albedo.png", new Vector2(0.5f, 0.03f), true);

            var floors = Array.ConvertAll(FloorFiles, LoadSprite);
            var wall = LoadSprite("tr01_reference_wall_a_albedo.png");
            var crystal = LoadSprite("tr01_reference_crystal_a_albedo.png");
            var driller = LoadSprite("tr01_reference_driller_a_albedo.png");

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            // Batch mode starts with an unsaved untitled scene, which Unity refuses to
            // accompany with an additive scene. Interactive use stays additive so the
            // user's currently open scene remains untouched.
            var mode = Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive;
            Scene preview = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            preview.name = "ReferenceCalibrationV1";

            var cameraGo = NewObject(preview, "Reference Camera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.018f, 0.008f, 0.028f, 1f);
            camera.transform.position = new Vector3(0f, 0f, -10f);

            for (int y = -4; y <= 3; y++)
            for (int x = -7; x <= 7; x++)
            {
                int floorIndex = Mathf.Abs(x * 17 + y * 31 + x * y * 7) % floors.Length;
                var sr = AddSprite(preview, $"Floor {x},{y} [{FloorFiles[floorIndex]}]", floors[floorIndex], new Vector3(x + 0.5f, y + 0.5f, 0f), 0);
                int turn = Mathf.Abs(x * 11 + y * 19) % 4;
                sr.transform.rotation = Quaternion.Euler(0f, 0f, turn * 90f);
                if (((x + y) & 1) != 0)
                    sr.transform.localScale = new Vector3(-1f, 1f, 1f);
                float tint = 0.68f + (Mathf.Abs(x * 7 + y * 13) % 3) * 0.025f;
                sr.color = new Color(tint, tint * 0.90f, tint * 1.08f, 1f);
            }

            var rear = AddSprite(preview, "Reference Wall", wall, new Vector3(3.25f, 0.82f, 0f), 10);
            rear.color = new Color(0.78f, 0.72f, 0.88f, 1f);

            var mineral = AddSprite(preview, "Reference Crystal", crystal, new Vector3(-4.15f, 0.7f, 0f), 20);
            mineral.transform.localScale = Vector3.one * 1.12f;

            var hero = AddSprite(preview, "Reference Driller", driller, new Vector3(-0.75f, -0.75f, 0f), 30);
            hero.transform.localScale = Vector3.one * 1.12f;

            // Persist only project-backed sprites. The soft contact shadows below are
            // temporary capture aids with in-memory textures.
            EditorSceneManager.SaveScene(preview, ScenePath);

            AddShadow(preview, new Vector3(-0.9f, -0.82f, 0f), new Vector2(2.3f, 0.48f), 22);
            AddShadow(preview, new Vector3(-4.15f, 0.62f, 0f), new Vector2(1.7f, 0.36f), 15);

            Capture(camera, ResolveQaPath());
            EditorSceneManager.CloseScene(preview, true);
            AssetDatabase.Refresh();

            Debug.Log("[비주얼] 레퍼런스 캘리브레이션 V1 조립·캡처 완료\n" + ResolveQaPath());
        }

        static void ConfigureSprite(string file, Vector2 pivot, bool alpha)
        {
            string path = $"{ArtDir}/{file}";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException($"TextureImporter not found: {path}");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 128f;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = alpha;
            importer.SaveAndReimport();
        }

        static Sprite LoadSprite(string file)
        {
            string path = $"{ArtDir}/{file}";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException($"Sprite not found: {path}");
            return sprite;
        }

        static GameObject NewObject(Scene scene, string name)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        static SpriteRenderer AddSprite(Scene scene, string name, Sprite sprite, Vector3 position, int order)
        {
            var go = NewObject(scene, name);
            go.transform.position = position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        static void AddShadow(Scene scene, Vector3 position, Vector2 size, int order)
        {
            var texture = new Texture2D(64, 24, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear
            };
            for (int y = 0; y < texture.height; y++)
            for (int x = 0; x < texture.width; x++)
            {
                float nx = (x + 0.5f) / texture.width * 2f - 1f;
                float ny = (y + 0.5f) / texture.height * 2f - 1f;
                float alpha = Mathf.Clamp01(1f - nx * nx - ny * ny) * 0.48f;
                texture.SetPixel(x, y, new Color(0.015f, 0.005f, 0.025f, alpha));
            }
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 64f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            var sr = AddSprite(scene, "Contact Shadow", sprite, position, order);
            sr.transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        static void Capture(Camera camera, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            camera.targetTexture = rt;
            camera.Render();
            camera.targetTexture = null;

            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var image = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(rt);
        }

        static string ResolveQaPath()
        {
            var directory = new DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && directory != null; i++, directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, "art-production/test-room-v01");
                if (Directory.Exists(candidate))
                    return Path.Combine(candidate, "qa/reference-calibration-floor-variants-unity.png");
            }
            throw new DirectoryNotFoundException("art-production/test-room-v01 was not found above Unity project");
        }

        static string ProjectRoot() => Directory.GetParent(Application.dataPath)!.FullName;
    }
}
