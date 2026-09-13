using System.Collections.Generic;
using System.IO;
using TunnelCrew.Presentation;
using TunnelCrew.Presentation.Visual;
using TunnelCrew.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TunnelCrew.EditorTools
{
    /// <summary>아홉 탄종과 복합 특성을 실제 프로젝트 셰이더로 렌더해 비교하는 회귀 프리뷰.</summary>
    public static class ProjectileVfxPreview
    {
        public const string OutputPath = "../../docs/design/projectile-vfx-preview.png";

        [MenuItem("Tunnel Crew/VFX/투사체 9종 프리뷰 렌더")]
        public static void RenderPreviewBatch()
        {
            var oldScene = SceneManager.GetActiveScene();
            bool preserveOldScene = oldScene.IsValid() && !string.IsNullOrEmpty(oldScene.path);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                preserveOldScene ? NewSceneMode.Additive : NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            GameObject root = null, cameraGo = null;
            RenderTexture target = null;
            Texture2D image = null;
            var oldActive = RenderTexture.active;
            try
            {
                root = new GameObject("Projectile VFX Preview");
                var renderer = root.AddComponent<ProjectileVfxRenderer>();
                renderer.Initialize(ProcSprites.Square());
                var impactRoot = new GameObject("Muzzle and impact preview");
                impactRoot.transform.SetParent(root.transform, false);
                var fx = impactRoot.AddComponent<FxSystem>();
                var projectiles = new List<Projectile>();
                string[] ids = { "standard", "multi", "pierce", "ricochet", "explosive", "rain", "laser", "support", "shard" };
                var renderDir = Vector2.right;
                var worldDir = IsometricProjection.ToWorld(renderDir).normalized;

                for (int i = 0; i < ids.Length; i++)
                {
                    int col = i % 3, row = i / 3;
                    var desired = new Vector2(-4.5f + col * 4.5f, 3.0f - row * 3.0f);
                    var world = IsometricProjection.ToWorld(desired);
                    var flags = ProjectileSystem.InferStyleFlags(ids[i]);
                    if (ids[i] == "ricochet") flags |= ProjectileStyleFlags.Pierce | ProjectileStyleFlags.Explosive;
                    projectiles.Add(new Projectile
                    {
                        Position = new Vec2(world.x, world.y), Velocity = new Vec2(worldDir.x, worldDir.y) * 8,
                        Life = 2, Age = i * .031, VisualId = ids[i], VisualFlags = flags,
                    });
                    var muzzle = desired + Vector2.left * 1.35f;
                    var impact = desired + Vector2.right * 1.38f;
                    bool explosive = ids[i] == "explosive" || ids[i] == "rain" || ids[i] == "laser";
                    var impactKind = ids[i] == "ricochet" ? ProjectileImpactKind.Ricochet : ProjectileImpactKind.Enemy;
                    fx.ProjectileMuzzle(muzzle, Vector2.right, ids[i], flags, ids[i] == "multi" ? 5 : 1, false);
                    fx.ProjectileImpact(impact, Vector2.right, ids[i], impactKind, flags,
                        ids[i] != "ricochet", explosive, false, 1f);
                    AddCard(root.transform, desired, ids[i], i);
                }
                renderer.Render(projectiles, VisualQualityTier.Ultra);
                fx.SendMessage("Update", SendMessageOptions.DontRequireReceiver);

                foreach (var trail in root.GetComponentsInChildren<TrailRenderer>(true))
                {
                    trail.Clear();
                    var end = trail.transform.position;
                    for (int k = 14; k >= 0; k--) trail.AddPosition(end - Vector3.right * (k * .12f));
                }

                cameraGo = new GameObject("Preview Camera");
                var camera = cameraGo.AddComponent<Camera>();
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true;
                camera.orthographicSize = 5.35f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.018f, .028f, .055f, 1);
                camera.allowHDR = true;
                target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { name = "ProjectileVfxPreviewRT" };
                camera.targetTexture = target;
                camera.Render();

                RenderTexture.active = target;
                image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                string absolute = Path.GetFullPath(OutputPath);
                Directory.CreateDirectory(Path.GetDirectoryName(absolute));
                File.WriteAllBytes(absolute, image.EncodeToPNG());
                Debug.Log("[ProjectileVfxPreview] " + absolute);
            }
            finally
            {
                RenderTexture.active = oldActive;
                if (image != null) Object.DestroyImmediate(image);
                if (cameraGo != null)
                {
                    var previewCamera = cameraGo.GetComponent<Camera>();
                    if (previewCamera != null) previewCamera.targetTexture = null;
                }
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                if (cameraGo != null) Object.DestroyImmediate(cameraGo);
                if (root != null) Object.DestroyImmediate(root);
                if (preserveOldScene)
                {
                    EditorSceneManager.CloseScene(scene, true);
                    if (oldScene.IsValid()) SceneManager.SetActiveScene(oldScene);
                }
            }
        }

        static void AddCard(Transform parent, Vector2 center, string label, int index)
        {
            var card = new GameObject("card-" + label);
            card.transform.SetParent(parent, false);
            card.transform.position = new Vector3(center.x, center.y, .20f);
            card.transform.localScale = new Vector3(4.1f, 2.45f, 1);
            var sprite = card.AddComponent<SpriteRenderer>();
            sprite.sprite = ProcSprites.Square();
            sprite.color = index % 2 == 0 ? new Color(.035f, .065f, .11f, 1) : new Color(.045f, .045f, .095f, 1);
            sprite.sortingOrder = -10;

            var textGo = new GameObject("label");
            textGo.transform.SetParent(parent, false);
            textGo.transform.position = new Vector3(center.x, center.y - .82f, -.05f);
            var text = textGo.AddComponent<TextMesh>();
            text.text = label.ToUpperInvariant();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 42;
            text.characterSize = .045f;
            text.color = new Color(.63f, .73f, .86f, 1);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            text.GetComponent<MeshRenderer>().sortingOrder = 60;
        }
    }
}
