using System;
using System.IO;
using System.Linq;
using TunnelCrew.Presentation.Prototype;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace TunnelCrew.EditorTools
{
    /// <summary>
    /// 기존 게임과 격리된 모듈형 거너 세로 슬라이스를 결정론적으로 재생성한다.
    /// PNG 임포트, 애니메이션 클립, 프리팹, 모노톤 타일 테스트 씬을 한 번에 만든다.
    /// </summary>
    public static class BuildModularGunnerPrototype
    {
        const int Ppu = 16;
        const string Root = "Assets/_Project/Prototype/ModularGunner";
        const string SpriteDir = Root + "/Art/Sprites";
        const string GeneratedDir = Root + "/Art/Generated";
        const string AnimationDir = Root + "/Animations";
        const string PrefabDir = Root + "/Prefabs";
        const string ScenePath = "Assets/_Project/Scenes/ModularGunnerPrototype.unity";

        const string HeadDownLeftPath = SpriteDir + "/gunner_head_down_left.png";
        const string HeadUpLeftPath = SpriteDir + "/gunner_head_up_left.png";
        const string BodyDownLeftPath = SpriteDir + "/gunner_body_down_left.png";
        const string BodyUpLeftPath = SpriteDir + "/gunner_body_up_left.png";
        const string LeftHandPath = SpriteDir + "/gunner_hand_left.png";
        const string RightHandPath = SpriteDir + "/gunner_hand_right.png";
        const string WeaponPath = SpriteDir + "/gunner_weapon.png";
        const string MuzzleFlashPath = SpriteDir + "/gunner_muzzle_flash.png";
        const string TracerPath = GeneratedDir + "/gunner_tracer.png";

        const string ControllerPath = AnimationDir + "/ModularGunner.controller";
        const string PlayerPrefabPath = PrefabDir + "/ModularGunner_Player.prefab";
        const string ProjectilePrefabPath = PrefabDir + "/ModularGunner_Projectile.prefab";
        const string EnemyPrefabPath = PrefabDir + "/ModularGunner_TargetEnemy.prefab";

        [MenuItem("Tunnel Crew/Prototype/Build Modular Gunner Test Scene")]
        public static void Build()
        {
            EnsureFolders();
            GenerateMonotoneArt();
            ImportSprites();

            AnimationClip idle = CreateIdleClip();
            AnimationClip move = CreateMoveClip();
            AnimationClip fire = CreateFireClip();
            AnimatorController controller = CreateController(idle, move, fire);

            var projectilePrefab = CreateProjectilePrefab();
            CreatePlayerPrefab(controller, projectilePrefab);
            CreateEnemyPrefab();
            CreateScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Modular Gunner] Build complete: {ScenePath}");
        }

        static void EnsureFolders()
        {
            foreach (string path in new[] { Root, GeneratedDir, AnimationDir, PrefabDir })
                Directory.CreateDirectory(AbsolutePath(path));
        }

        static void GenerateMonotoneArt()
        {
            WritePng(GeneratedDir + "/floor_a.png", 16, 16, (x, y) =>
            {
                byte c = (byte)(54 + ((x + y) % 7 == 0 ? 5 : 0));
                return new Color32(c, c, c, 255);
            });
            WritePng(GeneratedDir + "/floor_b.png", 16, 16, (x, y) =>
            {
                byte c = (byte)(62 + ((x * 3 + y * 5) % 11 == 0 ? 6 : 0));
                return new Color32(c, c, c, 255);
            });
            WritePng(GeneratedDir + "/wall.png", 16, 16, (x, y) =>
            {
                bool rim = x == 0 || y == 0 || x == 15 || y == 15;
                byte c = rim ? (byte)103 : (byte)(78 + ((x / 4 + y / 4) % 2) * 6);
                return new Color32(c, c, c, 255);
            });
            WritePng(GeneratedDir + "/shadow.png", 16, 8, (x, y) =>
            {
                float nx = (x - 7.5f) / 7.5f;
                float ny = (y - 3.5f) / 3.5f;
                return nx * nx + ny * ny <= 1f
                    ? new Color32(8, 8, 8, 145)
                    : new Color32(0, 0, 0, 0);
            });
            WritePng(GeneratedDir + "/target_enemy.png", 16, 16, (x, y) =>
            {
                int dx = x - 8;
                int dy = y - 8;
                int d = dx * dx + dy * dy;
                if (d > 55) return new Color32(0, 0, 0, 0);
                if (d > 38) return new Color32(18, 18, 18, 255);
                if ((x is >= 4 and <= 6 || x is >= 10 and <= 12) && y is >= 5 and <= 7)
                    return new Color32(242, 197, 48, 255);
                if (y is >= 10 and <= 11 && x is >= 5 and <= 11)
                    return new Color32(28, 28, 28, 255);
                byte c = (byte)(105 + ((x + y) % 3) * 12);
                return new Color32(c, c, c, 255);
            });
            WritePng(TracerPath, 24, 3, (x, y) =>
            {
                if (x >= 22) return new Color32(255, 245, 176, (byte)((24 - x) * 110));
                if (y == 1) return new Color32(255, 255, 238, 255);
                return x < 18
                    ? new Color32(255, 202, 38, 220)
                    : new Color32(255, 229, 91, (byte)((24 - x) * 36));
            });
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        static void WritePng(string assetPath, int width, int height, Func<int, int, Color32> sample)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = sample(x, y);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(AbsolutePath(assetPath), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        static void ImportSprites()
        {
            ConfigureSprite(HeadDownLeftPath, new Vector2(0.5f, 0.04f));
            ConfigureSprite(HeadUpLeftPath, new Vector2(0.5f, 0.04f));
            ConfigureSprite(BodyDownLeftPath, new Vector2(0.5f, 0.04f));
            ConfigureSprite(BodyUpLeftPath, new Vector2(0.5f, 0.04f));
            ConfigureSprite(LeftHandPath, new Vector2(0.5f, 0.5f));
            ConfigureSprite(RightHandPath, new Vector2(0.5f, 0.5f));
            ConfigureSprite(WeaponPath, new Vector2(0.18f, 0.5f));
            ConfigureSprite(MuzzleFlashPath, new Vector2(0.08f, 0.5f));
            ConfigureSprite(TracerPath, new Vector2(0.04f, 0.5f));
            ConfigureSprite(GeneratedDir + "/floor_a.png", new Vector2(0.5f, 0.5f));
            ConfigureSprite(GeneratedDir + "/floor_b.png", new Vector2(0.5f, 0.5f));
            ConfigureSprite(GeneratedDir + "/wall.png", new Vector2(0.5f, 0.5f));
            ConfigureSprite(GeneratedDir + "/shadow.png", new Vector2(0.5f, 0.5f));
            ConfigureSprite(GeneratedDir + "/target_enemy.png", new Vector2(0.5f, 0.08f));
        }

        static void ConfigureSprite(string assetPath, Vector2 pivot)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            if (importer == null) throw new InvalidOperationException($"Missing texture importer: {assetPath}");

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = Ppu;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        static AnimationClip CreateIdleClip()
        {
            var clip = NewClip(AnimationDir + "/Gunner_Idle.anim", loop: true);
            SetScale(clip, "Visual/Body", Keys(0, 1, 0.3f, 1.035f, 0.6f, 1), Keys(0, 1, 0.3f, 0.965f, 0.6f, 1));
            SetPositionY(clip, "Visual/Body", Keys(0, 0, 0.3f, 0.018f, 0.6f, 0));
            SetPositionY(clip, "Visual/HeadAnchor", Keys(0, 0.94f, 0.18f, 0.955f, 0.36f, 0.925f, 0.6f, 0.94f));
            SetScale(clip, "Visual/HeadAnchor/Head", Keys(0, 1, 0.22f, 0.985f, 0.42f, 1.025f, 0.6f, 1), Keys(0, 1, 0.22f, 1.025f, 0.42f, 0.98f, 0.6f, 1));
            SetScale(clip, "Visual/AimRig/WeaponKick/MainHand", Keys(0, 0.58f, 0.3f, 0.6f, 0.6f, 0.58f), Keys(0, 0.58f, 0.3f, 0.56f, 0.6f, 0.58f));
            SetScale(clip, "Visual/AimRig/WeaponKick/SupportHand", Keys(0, 0.54f, 0.3f, 0.52f, 0.6f, 0.54f), Keys(0, 0.54f, 0.3f, 0.56f, 0.6f, 0.54f));
            SetScale(clip, "Visual/AimRig/WeaponKick", Keys(0, 1, 0.3f, 1.008f, 0.6f, 1), Keys(0, 1, 0.3f, 0.992f, 0.6f, 1));
            SaveClip(clip);
            return clip;
        }

        static AnimationClip CreateMoveClip()
        {
            var clip = NewClip(AnimationDir + "/Gunner_Move.anim", loop: true);
            SetScale(clip, "Visual/Body", Keys(0, 1.08f, 0.1f, 0.95f, 0.2f, 1.06f, 0.3f, 0.96f, 0.4f, 1.08f), Keys(0, 0.92f, 0.1f, 1.08f, 0.2f, 0.94f, 0.3f, 1.07f, 0.4f, 0.92f));
            SetPositionY(clip, "Visual/Body", Keys(0, 0, 0.1f, 0.065f, 0.2f, 0, 0.3f, 0.06f, 0.4f, 0));
            SetPositionY(clip, "Visual/HeadAnchor", Keys(0, 0.93f, 0.06f, 0.99f, 0.14f, 0.94f, 0.24f, 1.0f, 0.34f, 0.93f, 0.4f, 0.93f));
            SetScale(clip, "Visual/HeadAnchor/Head", Keys(0, 0.96f, 0.08f, 1.045f, 0.2f, 0.97f, 0.28f, 1.04f, 0.4f, 0.96f), Keys(0, 1.04f, 0.08f, 0.96f, 0.2f, 1.035f, 0.28f, 0.965f, 0.4f, 1.04f));
            SetScale(clip, "Visual/AimRig/WeaponKick", Keys(0, 1.02f, 0.1f, 0.98f, 0.2f, 1.02f, 0.3f, 0.98f, 0.4f, 1.02f), Keys(0, 0.98f, 0.1f, 1.02f, 0.2f, 0.98f, 0.3f, 1.02f, 0.4f, 0.98f));
            SetScale(clip, "Visual/AimRig/WeaponKick/MainHand", Keys(0, 0.6f, 0.1f, 0.55f, 0.2f, 0.6f, 0.3f, 0.55f, 0.4f, 0.6f), Keys(0, 0.54f, 0.1f, 0.61f, 0.2f, 0.54f, 0.3f, 0.61f, 0.4f, 0.54f));
            SetScale(clip, "Visual/AimRig/WeaponKick/SupportHand", Keys(0, 0.52f, 0.1f, 0.58f, 0.2f, 0.52f, 0.3f, 0.58f, 0.4f, 0.52f), Keys(0, 0.58f, 0.1f, 0.52f, 0.2f, 0.58f, 0.3f, 0.52f, 0.4f, 0.58f));
            SaveClip(clip);
            return clip;
        }

        static AnimationClip CreateFireClip()
        {
            var clip = NewClip(AnimationDir + "/Gunner_Fire.anim", loop: false);
            SetPositionX(clip, "Visual/AimRig/WeaponKick", Keys(0, 0, 0.025f, -0.18f, 0.075f, -0.07f, 0.13f, 0));
            SetScale(clip, "Visual/AimRig/WeaponKick", Keys(0, 1, 0.025f, 0.88f, 0.075f, 1.06f, 0.13f, 1), Keys(0, 1, 0.025f, 1.16f, 0.075f, 0.96f, 0.13f, 1));
            SetScale(clip, "Visual/AimRig/WeaponKick/MainHand", Keys(0, 0.58f, 0.025f, 0.48f, 0.075f, 0.62f, 0.13f, 0.58f), Keys(0, 0.58f, 0.025f, 0.68f, 0.075f, 0.54f, 0.13f, 0.58f));
            SetScale(clip, "Visual/AimRig/WeaponKick/SupportHand", Keys(0, 0.54f, 0.025f, 0.46f, 0.075f, 0.58f, 0.13f, 0.54f), Keys(0, 0.54f, 0.025f, 0.64f, 0.075f, 0.5f, 0.13f, 0.54f));
            SetScale(clip, "Visual/Body", Keys(0, 1, 0.035f, 1.055f, 0.09f, 0.98f, 0.13f, 1), Keys(0, 1, 0.035f, 0.95f, 0.09f, 1.03f, 0.13f, 1));
            SetPositionY(clip, "Visual/HeadAnchor", Keys(0, 0.94f, 0.035f, 0.985f, 0.09f, 0.925f, 0.13f, 0.94f));
            SetScale(clip, "Visual/HeadAnchor/Head", Keys(0, 1, 0.035f, 1.05f, 0.09f, 0.97f, 0.13f, 1), Keys(0, 1, 0.035f, 0.94f, 0.09f, 1.035f, 0.13f, 1));
            SaveClip(clip);
            return clip;
        }

        static AnimatorController CreateController(AnimationClip idle, AnimationClip move, AnimationClip fire)
        {
            DeleteIfExists(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Fire", AnimatorControllerParameterType.Trigger);

            var baseMachine = controller.layers[0].stateMachine;
            var idleState = baseMachine.AddState("Idle");
            idleState.motion = idle;
            var moveState = baseMachine.AddState("Move");
            moveState.motion = move;
            baseMachine.defaultState = idleState;

            var toMove = idleState.AddTransition(moveState);
            toMove.hasExitTime = false;
            toMove.duration = 0.06f;
            toMove.AddCondition(AnimatorConditionMode.Greater, 0.12f, "Speed");
            var toIdle = moveState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.08f;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.12f, "Speed");

            var actionMachine = new AnimatorStateMachine { name = "Action" };
            AssetDatabase.AddObjectToAsset(actionMachine, controller);
            var emptyClip = NewClip(AnimationDir + "/Gunner_ActionEmpty.anim", loop: true);
            SaveClip(emptyClip);
            var emptyState = actionMachine.AddState("Empty");
            emptyState.motion = emptyClip;
            actionMachine.defaultState = emptyState;
            var fireState = actionMachine.AddState("Fire");
            fireState.motion = fire;
            var enterFire = actionMachine.AddAnyStateTransition(fireState);
            enterFire.hasExitTime = false;
            enterFire.duration = 0.01f;
            enterFire.AddCondition(AnimatorConditionMode.If, 0, "Fire");
            var leaveFire = fireState.AddTransition(emptyState);
            leaveFire.hasExitTime = true;
            leaveFire.exitTime = 1f;
            leaveFire.duration = 0.025f;

            controller.AddLayer(new AnimatorControllerLayer
            {
                name = "Action",
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Override,
                stateMachine = actionMachine,
            });
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        static ModularGunnerProjectile CreateProjectilePrefab()
        {
            var go = new GameObject("ModularGunner_Projectile");
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = LoadSprite(TracerPath);
            renderer.sortingOrder = 45;
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var collider = go.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(1.35f, 0.12f);
            collider.offset = new Vector2(0.66f, 0f);
            var projectile = go.AddComponent<ModularGunnerProjectile>();
            projectile.EditorAssign(body);
            PrefabUtility.SaveAsPrefabAsset(go, ProjectilePrefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            AssetDatabase.ImportAsset(ProjectilePrefabPath, ImportAssetOptions.ForceSynchronousImport);
            var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath);
            return prefabRoot.GetComponent<ModularGunnerProjectile>();
        }

        static ModularGunnerController CreatePlayerPrefab(AnimatorController controller, ModularGunnerProjectile projectilePrefab)
        {
            var root = new GameObject("ModularGunner_Player");
            var body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0;
            body.freezeRotation = true;
            body.linearDamping = 8f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var collider = root.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(0.72f, 0.68f);
            collider.offset = new Vector2(0, 0.34f);
            collider.direction = CapsuleDirection2D.Vertical;
            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            var shadow = Child(root.transform, "Shadow", new Vector3(0, 0.08f, 0));
            var shadowRenderer = shadow.gameObject.AddComponent<SpriteRenderer>();
            shadowRenderer.sprite = LoadSprite(GeneratedDir + "/shadow.png");
            shadowRenderer.sortingOrder = 1;

            var visual = Child(root.transform, "Visual", Vector3.zero);
            var bodyPart = Child(visual, "Body", Vector3.zero);
            var bodyRenderer = AddSprite(bodyPart, LoadSprite(BodyDownLeftPath), 10);

            var headAnchor = Child(visual, "HeadAnchor", new Vector3(0, 0.94f, 0));
            var head = Child(headAnchor, "Head", Vector3.zero);
            var headRenderer = AddSprite(head, LoadSprite(HeadDownLeftPath), 20);

            var aimRig = Child(visual, "AimRig", new Vector3(0, 0.64f, 0));
            var weaponKick = Child(aimRig, "WeaponKick", Vector3.zero);
            var weapon = Child(weaponKick, "Weapon", Vector3.zero);
            var weaponRenderer = AddSprite(weapon, LoadSprite(WeaponPath), 30);

            var mainHand = Child(weaponKick, "MainHand", new Vector3(0.03f, -0.04f, 0));
            mainHand.localScale = Vector3.one * 0.58f;
            var mainHandRenderer = AddSprite(mainHand, LoadSprite(RightHandPath), 32);
            var supportHand = Child(weaponKick, "SupportHand", new Vector3(0.62f, -0.02f, 0));
            supportHand.localScale = Vector3.one * 0.54f;
            var supportHandRenderer = AddSprite(supportHand, LoadSprite(LeftHandPath), 31);

            var muzzle = Child(weaponKick, "Muzzle", new Vector3(1.56f, 0, 0));
            var flash = Child(muzzle, "MuzzleFlash", new Vector3(0.02f, 0, 0));
            flash.localScale = Vector3.one * 0.72f;
            var flashRenderer = AddSprite(flash, LoadSprite(MuzzleFlashPath), 33);
            flashRenderer.enabled = false;

            var controllerComponent = root.AddComponent<ModularGunnerController>();
            controllerComponent.EditorAssign(body, animator, aimRig, muzzle, bodyRenderer, headRenderer,
                weaponRenderer, mainHandRenderer, supportHandRenderer, flashRenderer, projectilePrefab,
                LoadSprite(BodyUpLeftPath), LoadSprite(HeadUpLeftPath));

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.ImportAsset(PlayerPrefabPath, ImportAssetOptions.ForceSynchronousImport);
            var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            return prefabRoot.GetComponent<ModularGunnerController>();
        }

        static ModularGunnerEnemy CreateEnemyPrefab()
        {
            var root = new GameObject("ModularGunner_TargetEnemy");
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = LoadSprite(GeneratedDir + "/target_enemy.png");
            renderer.sortingOrder = 12;
            var body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0;
            body.freezeRotation = true;
            body.linearDamping = 5f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var collider = root.AddComponent<CircleCollider2D>();
            collider.radius = 0.43f;
            collider.offset = new Vector2(0, 0.38f);
            var enemy = root.AddComponent<ModularGunnerEnemy>();
            enemy.EditorAssign(body, renderer);
            PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.ImportAsset(EnemyPrefabPath, ImportAssetOptions.ForceSynchronousImport);
            var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            return prefabRoot.GetComponent<ModularGunnerEnemy>();
        }

        static void CreateScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraGo = new GameObject("Main Camera");
            SceneManager.MoveGameObjectToScene(cameraGo, scene);
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(-0.5f, -0.5f, -10f);
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.625f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.055f, 0.065f);
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.allowDynamicResolution = false;
            cameraGo.AddComponent<UniversalAdditionalCameraData>();
            var pixelPerfect = cameraGo.AddComponent<PixelPerfectCamera>();
            pixelPerfect.assetsPPU = Ppu;
            pixelPerfect.refResolutionX = 320;
            pixelPerfect.refResolutionY = 180;
            pixelPerfect.gridSnapping = PixelPerfectCamera.GridSnapping.PixelSnapping;
            pixelPerfect.cropFrame = PixelPerfectCamera.CropFrame.Windowbox;

            var lightGo = new GameObject("Main Directional Light");
            SceneManager.MoveGameObjectToScene(lightGo, scene);
            lightGo.transform.rotation = Quaternion.Euler(50, -30, 0);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.35f;

            var gridGo = new GameObject("Monotone Test Room");
            SceneManager.MoveGameObjectToScene(gridGo, scene);
            gridGo.AddComponent<Grid>();
            var floor = CreateTilemap(gridGo.transform, "Floor", 0);
            var walls = CreateTilemap(gridGo.transform, "Walls", 3);
            Tile floorA = GetOrCreateTile(GeneratedDir + "/FloorA.asset", LoadSprite(GeneratedDir + "/floor_a.png"));
            Tile floorB = GetOrCreateTile(GeneratedDir + "/FloorB.asset", LoadSprite(GeneratedDir + "/floor_b.png"));
            Tile wallTile = GetOrCreateTile(GeneratedDir + "/Wall.asset", LoadSprite(GeneratedDir + "/wall.png"));

            for (int y = -5; y <= 4; y++)
            for (int x = -9; x <= 8; x++)
                floor.SetTile(new Vector3Int(x, y, 0), ((x + y) & 1) == 0 ? floorA : floorB);

            for (int x = -10; x <= 9; x++)
            {
                walls.SetTile(new Vector3Int(x, -6, 0), wallTile);
                walls.SetTile(new Vector3Int(x, 5, 0), wallTile);
            }
            for (int y = -5; y <= 4; y++)
            {
                walls.SetTile(new Vector3Int(-10, y, 0), wallTile);
                walls.SetTile(new Vector3Int(9, y, 0), wallTile);
            }

            AddWallCollider(gridGo.transform, "Wall_Left", new Vector2(-9.55f, -0.5f), new Vector2(0.9f, 11f));
            AddWallCollider(gridGo.transform, "Wall_Right", new Vector2(8.55f, -0.5f), new Vector2(0.9f, 11f));
            AddWallCollider(gridGo.transform, "Wall_Bottom", new Vector2(-0.5f, -5.55f), new Vector2(18f, 0.9f));
            AddWallCollider(gridGo.transform, "Wall_Top", new Vector2(-0.5f, 4.55f), new Vector2(18f, 0.9f));
            AddObstacle(walls, wallTile, gridGo.transform, new Vector3Int(-3, 1, 0), new Vector2Int(2, 2));
            AddObstacle(walls, wallTile, gridGo.transform, new Vector3Int(3, -3, 0), new Vector2Int(2, 2));

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            var playerObject = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            var player = playerObject.GetComponent<ModularGunnerController>();
            player.transform.position = new Vector3(-1f, -0.8f, 0);
            foreach (Vector2 position in new[] { new Vector2(4, 2), new Vector2(5, -2), new Vector2(-6, 2.5f) })
            {
                var enemyObject = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab, scene);
                var enemy = enemyObject.GetComponent<ModularGunnerEnemy>();
                enemy.transform.position = position;
            }

            var hud = new GameObject("Prototype HUD");
            SceneManager.MoveGameObjectToScene(hud, scene);
            hud.AddComponent<ModularGunnerHud>();

            QualitySettings.antiAliasing = 0;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeObject = player;
        }

        static Tilemap CreateTilemap(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var map = go.AddComponent<Tilemap>();
            var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;
            return map;
        }

        static Tile GetOrCreateTile(string path, Sprite sprite)
        {
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.sprite = sprite;
            tile.color = Color.white;
            tile.colliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(tile);
            return tile;
        }

        static void AddWallCollider(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = size;
        }

        static void AddObstacle(Tilemap walls, Tile wallTile, Transform parent, Vector3Int origin, Vector2Int size)
        {
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
                walls.SetTile(origin + new Vector3Int(x, y, 0), wallTile);

            Vector2 center = new Vector2(origin.x + (size.x - 1) * 0.5f, origin.y + (size.y - 1) * 0.5f);
            AddWallCollider(parent, $"Obstacle_{origin.x}_{origin.y}", center, size);
        }

        static SpriteRenderer AddSprite(Transform target, Sprite sprite, int sortingOrder)
        {
            var renderer = target.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        static Transform Child(Transform parent, string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        static AnimationClip NewClip(string path, bool loop)
        {
            DeleteIfExists(path);
            var clip = new AnimationClip { name = Path.GetFileNameWithoutExtension(path), frameRate = 60 };
            AssetDatabase.CreateAsset(clip, path);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            return clip;
        }

        static void SaveClip(AnimationClip clip)
        {
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssetIfDirty(clip);
        }

        static AnimationCurve Keys(params float[] timeValuePairs)
        {
            if ((timeValuePairs.Length & 1) != 0) throw new ArgumentException("Expected time/value pairs");
            var keys = Enumerable.Range(0, timeValuePairs.Length / 2)
                .Select(i => new Keyframe(timeValuePairs[i * 2], timeValuePairs[i * 2 + 1]))
                .ToArray();
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
            }
            return curve;
        }

        static void SetScale(AnimationClip clip, string path, AnimationCurve x, AnimationCurve y)
        {
            SetCurve(clip, path, "m_LocalScale.x", x);
            SetCurve(clip, path, "m_LocalScale.y", y);
            SetCurve(clip, path, "m_LocalScale.z", AnimationCurve.Constant(0, Mathf.Max(x.keys[^1].time, y.keys[^1].time), 1));
        }

        static void SetPositionX(AnimationClip clip, string path, AnimationCurve curve) => SetCurve(clip, path, "m_LocalPosition.x", curve);
        static void SetPositionY(AnimationClip clip, string path, AnimationCurve curve) => SetCurve(clip, path, "m_LocalPosition.y", curve);

        static void SetCurve(AnimationClip clip, string path, string property, AnimationCurve curve)
        {
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
        }

        static Sprite LoadSprite(string path) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new InvalidOperationException($"Missing sprite: {path}");

        static void DeleteIfExists(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
        }

        static string AbsolutePath(string assetPath) =>
            Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }
}
