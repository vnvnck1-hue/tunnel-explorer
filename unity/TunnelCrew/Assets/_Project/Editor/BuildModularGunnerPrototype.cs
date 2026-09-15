using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TunnelCrew.Presentation.Prototype;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering;
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
        const string CasingPath = GeneratedDir + "/casing.png";
        const string StoneShardPath = GeneratedDir + "/stone_shard.png";
        const string EnemyShardPath = GeneratedDir + "/enemy_shard.png";
        const string SparkPath = GeneratedDir + "/spark.png";
        const string BurstPath = GeneratedDir + "/impact_burst.png";
        const string LampPath = GeneratedDir + "/floor_lamp.png";
        const string WallFacePath = GeneratedDir + "/wall_face.png";
        const string WallShadowPath = GeneratedDir + "/wall_contact_shadow.png";

        const string ControllerPath = AnimationDir + "/ModularGunner.controller";
        const string PlayerPrefabPath = PrefabDir + "/ModularGunner_Player.prefab";
        const string ProjectilePrefabPath = PrefabDir + "/ModularGunner_Projectile.prefab";
        const string EnemyPrefabPath = PrefabDir + "/ModularGunner_TargetEnemy.prefab";
        const string LitMaterialPath = GeneratedDir + "/PrototypeSpriteLit.mat";
        const string UnlitMaterialPath = GeneratedDir + "/PrototypeSpriteUnlit.mat";
        const string CameraProfilePath = Root + "/ModularGunnerCameraProfile.asset";

        // 승인 아트(art-production/test-room-v01/approved/modular-gunner-gungeon)에서 옮겨 온 자산.
        const string EnvironmentDir = Root + "/Art/Environment";
        const string DecalDir = Root + "/Art/Decals";
        const string WeaponRecoilPath = SpriteDir + "/gunner_weapon_recoil.png";
        const string EnemyFramesPath = SpriteDir + "/enemy_rock_frames.png";
        const string TileDir = GeneratedDir + "/Tiles";
        const string WallRimCapPath = GeneratedDir + "/wall_rim_cap.png";
        const string WallRimFacePath = GeneratedDir + "/wall_rim_face.png";
        const string WallRimTopPath = GeneratedDir + "/wall_rim_top.png";
        const int RimThickness = 3;
        const string DustMotePath = GeneratedDir + "/dust_mote.png";
        const string VolumeProfilePath = Root + "/ModularGunnerPostProcess.asset";

        const int FloorVariants = 9;
        const int WallVariants = 4;
        const int RubbleVariants = 9;
        const int WeaponRecoilFrameCount = 4;
        const int EnemyFrameCount = 6;

        // 바닥 변형 가중치. 0번 무지 자갈이 대부분을 채우고 나머지는 저빈도로만 섞인다.
        // 8번(배관)은 이웃 셀과 이어지지 않아 무작위 배치에서 제외한다.
        static readonly int[] FloorWeights = { 46, 14, 8, 6, 14, 6, 3, 2, 0 };
        // 벽 정면 2·3번은 결정이 박힌 변형이라 드물게만 쓰고 약한 점광원을 붙인다.
        static readonly int[] WallFaceWeights = { 38, 38, 12, 12 };

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
            Material litMaterial = GetOrCreateMaterial(LitMaterialPath, "Universal Render Pipeline/2D/Sprite-Lit-Default");
            Material unlitMaterial = GetOrCreateMaterial(UnlitMaterialPath, "Universal Render Pipeline/2D/Sprite-Unlit-Default");
            ModularGunnerCameraProfile cameraProfile = GetOrCreateCameraProfile();

            var projectilePrefab = CreateProjectilePrefab(unlitMaterial);
            CreatePlayerPrefab(controller, projectilePrefab, litMaterial);
            CreateEnemyPrefab(litMaterial);
            CreateScene(litMaterial, unlitMaterial, cameraProfile);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Modular Gunner] Build complete: {ScenePath}");
        }

        static void EnsureFolders()
        {
            foreach (string path in new[] { Root, GeneratedDir, AnimationDir, PrefabDir, EnvironmentDir, DecalDir, TileDir })
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
                // 외곽선은 노출된 전면에서만 만든다. 셀마다 테두리를 넣으면 벽 덩어리
                // 내부에도 가로 줄이 반복되어 계단처럼 보인다.
                byte step = (byte)(((x / 4 + y / 4) & 1) * 7);
                return new Color32((byte)(82 + step), (byte)(89 + step), (byte)(108 + step), 255);
            });
            WritePng(WallFacePath, 16, 24, (x, y) =>
            {
                if (y == 23) return new Color32(76, 82, 101, 255);
                if (y == 0) return new Color32(29, 31, 42, 255);
                if (x == 0 || x == 15) return new Color32(39, 43, 57, 255);
                if (y == 8 || y == 16) return new Color32(43, 47, 61, 255);
                if ((x == 7 && y < 8) || (x == 3 && y is > 8 and < 16) || (x == 11 && y > 16))
                    return new Color32(47, 51, 66, 255);
                byte step = (byte)((y / 6) * 3);
                return new Color32((byte)(50 + step), (byte)(55 + step), (byte)(71 + step), 255);
            });
            // 벽 덩어리 외곽 림. 승인 타일에는 코너·끝단 변형이 없어 덩어리가 평평한 판으로
            // 읽힌다. 노출된 모서리 안쪽만 어둡게 눌러 암반 실루엣을 세운다(로드맵 4.5).
            WritePng(WallRimCapPath, RimThickness, 16, (x, y) =>
                new Color32(6, 6, 11, (byte)Mathf.Lerp(132f, 0f, x / (float)(RimThickness - 1))));
            WritePng(WallRimFacePath, RimThickness, 24, (x, y) =>
                new Color32(4, 4, 8, (byte)Mathf.Lerp(150f, 0f, x / (float)(RimThickness - 1))));
            WritePng(WallRimTopPath, 16, RimThickness, (x, y) =>
                new Color32(6, 6, 11, (byte)Mathf.Lerp(0f, 118f, y / (float)(RimThickness - 1))));
            WritePng(WallShadowPath, 16, 6, (x, y) =>
            {
                if ((x == 0 || x == 15) && y <= 1) return new Color32(0, 0, 0, 0);
                byte alpha = (byte)Mathf.Lerp(28f, 112f, y / 5f);
                return new Color32(8, 7, 13, alpha);
            });
            // 먼지 입자는 1픽셀 사각형이면 충분하다(로드맵 4.9).
            WritePng(DustMotePath, 2, 2, (x, y) => new Color32(255, 255, 255, 255));
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
            WritePng(CasingPath, 5, 3, (x, y) =>
            {
                if ((x == 0 || x == 4) && y != 1) return new Color32(0, 0, 0, 0);
                if (y == 2) return new Color32(255, 222, 86, 255);
                if (x == 4) return new Color32(104, 62, 12, 255);
                return new Color32(207, 141, 28, 255);
            });
            WritePng(StoneShardPath, 4, 4, (x, y) =>
                x + y is >= 2 and <= 5 && !(x == 0 && y == 3)
                    ? new Color32((byte)(150 + y * 14), (byte)(145 + y * 12), (byte)(137 + y * 10), 255)
                    : new Color32(0, 0, 0, 0));
            WritePng(EnemyShardPath, 4, 4, (x, y) =>
                (x == 1 || x == 2 || y == 1) && !(x == 0 && y == 1)
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0));
            WritePng(SparkPath, 5, 5, (x, y) =>
                x == 2 || y == 2
                    ? new Color32(255, 255, 255, (byte)((x == 2 && y == 2) ? 255 : 205))
                    : new Color32(0, 0, 0, 0));
            WritePng(BurstPath, 16, 16, (x, y) =>
            {
                int dx = x - 8, dy = y - 8;
                int d = dx * dx + dy * dy;
                bool ray = (Mathf.Abs(dx) <= 1 || Mathf.Abs(dy) <= 1 || Mathf.Abs(Mathf.Abs(dx) - Mathf.Abs(dy)) <= 1) && d < 58;
                bool core = d < 11;
                return ray || core ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            });
            WritePng(LampPath, 8, 10, (x, y) =>
            {
                if (y <= 1 && x is >= 1 and <= 6) return new Color32(34, 30, 24, 255);
                if (y is >= 2 and <= 6 && x is >= 3 and <= 4) return new Color32(82, 68, 42, 255);
                if (y is >= 6 and <= 8 && x is >= 1 and <= 6) return new Color32(255, 190, 48, 255);
                return new Color32(0, 0, 0, 0);
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
            ConfigureSprite(WallFacePath, new Vector2(0.5f, 1f));
            ConfigureSprite(WallShadowPath, new Vector2(0.5f, 1f));
            // 림은 셀 경계에 붙이므로 바깥쪽 모서리가 피벗이다.
            ConfigureSprite(WallRimCapPath, new Vector2(0f, 0f));
            ConfigureSprite(WallRimFacePath, new Vector2(0f, 1f));
            ConfigureSprite(WallRimTopPath, new Vector2(0f, 1f));
            ConfigureSprite(DustMotePath, new Vector2(0.5f, 0.5f));
            ConfigureSprite(GeneratedDir + "/shadow.png", new Vector2(0.5f, 0.5f));
            ConfigureSprite(GeneratedDir + "/target_enemy.png", new Vector2(0.5f, 0.08f));
            ConfigureSprite(CasingPath, new Vector2(0.5f, 0.5f));
            ConfigureSprite(StoneShardPath, new Vector2(0.5f, 0.5f));
            ConfigureSprite(EnemyShardPath, new Vector2(0.5f, 0.5f));
            ConfigureSprite(SparkPath, new Vector2(0.5f, 0.5f));
            ConfigureSprite(BurstPath, new Vector2(0.5f, 0.5f));
            ConfigureSprite(LampPath, new Vector2(0.5f, 0f));

            // 승인 환경 타일. 바닥과 벽 상단 캡은 격자 중앙, 벽 정면은 위쪽 피벗을 쓴다.
            for (int i = 0; i < FloorVariants; i++)
                ConfigureSprite($"{EnvironmentDir}/env_floor_{i:00}.png", new Vector2(0.5f, 0.5f));
            for (int i = 0; i < WallVariants; i++)
            {
                ConfigureSprite($"{EnvironmentDir}/env_wall_cap_{i:00}.png", new Vector2(0.5f, 0.5f));
                ConfigureSprite($"{EnvironmentDir}/env_wall_face_{i:00}.png", new Vector2(0.5f, 1f));
            }
            for (int i = 0; i < RubbleVariants; i++)
                ConfigureSprite($"{EnvironmentDir}/env_rubble_{i:00}.png", new Vector2(0.5f, 0.12f));

            // 데칼은 회전해 찍으므로 전부 중앙 피벗이다.
            foreach (string path in DecalPaths())
                ConfigureSprite(path, new Vector2(0.5f, 0.5f));

            // 무기 반동 4프레임: 총구가 +X 를 향하도록 뒤집혀 있고, 손잡이가 피벗이다.
            // 피벗 x=0.22 는 기존 무기와 같은 25px 총구 거리를 유지한다.
            SliceSheet(WeaponRecoilPath, 32, 12, WeaponRecoilFrameCount, new Vector2(0.22f, 0.5f));
            // 적 6프레임: 무손상·경피격·균열·붕괴·파쇄·잔해. 접지점을 프레임 하단 6px 로 맞춰 두었다.
            SliceSheet(EnemyFramesPath, 32, 32, EnemyFrameCount, new Vector2(0.5f, 0.1875f));
        }

        static IEnumerable<string> DecalPaths()
        {
            foreach (string prefix in new[] { "wall", "floor", "splat" })
            {
                int count = prefix == "splat" ? 5 : 4;
                for (int i = 0; i < count; i++)
                {
                    string path = $"{DecalDir}/decal_{prefix}_{i:00}.png";
                    if (File.Exists(AbsolutePath(path))) yield return path;
                }
            }
        }

        /// <summary>가로로 이어 붙인 프레임 시트를 균등 슬라이스하고 공통 피벗을 박아 넣는다.</summary>
        static void SliceSheet(string assetPath, int frameWidth, int frameHeight, int frameCount, Vector2 pivot)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            if (importer == null) throw new InvalidOperationException($"Missing texture importer: {assetPath}");

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = Ppu;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;

            string baseName = Path.GetFileNameWithoutExtension(assetPath);
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            var rects = new SpriteRect[frameCount];
            var names = new List<string>(frameCount);
            var ids = new List<string>(frameCount);
            for (int i = 0; i < frameCount; i++)
            {
                var id = GUID.Generate();
                rects[i] = new SpriteRect
                {
                    name = $"{baseName}_{i}",
                    spriteID = id,
                    rect = new Rect(i * frameWidth, 0, frameWidth, frameHeight),
                    alignment = SpriteAlignment.Custom,
                    pivot = pivot,
                    border = Vector4.zero,
                };
                names.Add(rects[i].name);
                ids.Add(id.ToString());
            }
            provider.SetSpriteRects(rects);

            // Unity 6 에서는 이름↔ID 매핑을 따로 넣어야 슬라이스가 재임포트 후에도 유지된다.
            var nameIdProvider = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameIdProvider != null)
            {
                var pairs = new List<SpriteNameFileIdPair>(frameCount);
                for (int i = 0; i < frameCount; i++)
                    pairs.Add(new SpriteNameFileIdPair(names[i], new GUID(ids[i])));
                nameIdProvider.SetNameFileIdPairs(pairs);
            }

            provider.Apply();
            importer.SaveAndReimport();
        }

        /// <summary>시트에서 잘라 낸 스프라이트를 프레임 순서대로 돌려준다.</summary>
        static Sprite[] LoadSheet(string assetPath, int frameCount)
        {
            string baseName = Path.GetFileNameWithoutExtension(assetPath);
            var frames = new Sprite[frameCount];
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (asset is not Sprite sprite) continue;
                string suffix = sprite.name.Substring(baseName.Length + 1);
                if (int.TryParse(suffix, out int index) && index >= 0 && index < frameCount)
                    frames[index] = sprite;
            }
            for (int i = 0; i < frameCount; i++)
                if (frames[i] == null)
                    throw new InvalidOperationException($"Sheet frame missing: {assetPath}#{i}");
            return frames;
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

        static ModularGunnerProjectile CreateProjectilePrefab(Material unlitMaterial)
        {
            var go = new GameObject("ModularGunner_Projectile");
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = LoadSprite(TracerPath);
            renderer.sharedMaterial = unlitMaterial;
            renderer.sortingOrder = 300;
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var collider = go.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(1.35f, 0.12f);
            collider.offset = new Vector2(0.66f, 0f);
            var projectile = go.AddComponent<ModularGunnerProjectile>();
            projectile.EditorAssign(body, renderer);
            PrefabUtility.SaveAsPrefabAsset(go, ProjectilePrefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            AssetDatabase.ImportAsset(ProjectilePrefabPath, ImportAssetOptions.ForceSynchronousImport);
            var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath);
            return prefabRoot.GetComponent<ModularGunnerProjectile>();
        }

        static ModularGunnerController CreatePlayerPrefab(AnimatorController controller, ModularGunnerProjectile projectilePrefab, Material litMaterial)
        {
            var root = new GameObject("ModularGunner_Player");
            root.AddComponent<SortingGroup>().sortingOrder = ModularGunnerDepthSorter.OrderFor(0f);
            root.AddComponent<ModularGunnerDepthSorter>();
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
            Sprite[] weaponFrames = LoadSheet(WeaponRecoilPath, WeaponRecoilFrameCount);
            var weapon = Child(weaponKick, "Weapon", Vector3.zero);
            var weaponRenderer = AddSprite(weapon, weaponFrames[0], 30);

            var mainHand = Child(weaponKick, "MainHand", new Vector3(0.03f, -0.04f, 0));
            mainHand.localScale = Vector3.one * 0.58f;
            var mainHandRenderer = AddSprite(mainHand, LoadSprite(RightHandPath), 32);
            var supportHand = Child(weaponKick, "SupportHand", new Vector3(0.62f, -0.02f, 0));
            supportHand.localScale = Vector3.one * 0.54f;
            var supportHandRenderer = AddSprite(supportHand, LoadSprite(LeftHandPath), 31);

            var muzzle = Child(weaponKick, "Muzzle", new Vector3(1.56f, 0, 0));
            var ejectionPort = Child(weaponKick, "EjectionPort", new Vector3(0.45f, 0.12f, 0));
            var flash = Child(muzzle, "MuzzleFlash", new Vector3(0.02f, 0, 0));
            flash.localScale = Vector3.one * 0.72f;
            var flashRenderer = AddSprite(flash, LoadSprite(MuzzleFlashPath), 33);
            flashRenderer.enabled = false;

            var controllerComponent = root.AddComponent<ModularGunnerController>();
            controllerComponent.EditorAssign(body, animator, aimRig, muzzle, ejectionPort, bodyRenderer, headRenderer,
                weaponRenderer, mainHandRenderer, supportHandRenderer, flashRenderer, projectilePrefab,
                LoadSprite(BodyUpLeftPath), LoadSprite(HeadUpLeftPath), weaponFrames);

            foreach (var spriteRenderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                spriteRenderer.sharedMaterial = litMaterial;
            flashRenderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.ImportAsset(PlayerPrefabPath, ImportAssetOptions.ForceSynchronousImport);
            var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            return prefabRoot.GetComponent<ModularGunnerController>();
        }

        static ModularGunnerEnemy CreateEnemyPrefab(Material litMaterial)
        {
            var root = new GameObject("ModularGunner_TargetEnemy");
            root.AddComponent<SortingGroup>().sortingOrder = ModularGunnerDepthSorter.OrderFor(0f);
            root.AddComponent<ModularGunnerDepthSorter>();
            Sprite[] enemyFrames = LoadSheet(EnemyFramesPath, EnemyFrameCount);

            // 접촉 그림자. 플레이어와 같은 블롭을 써야 두 캐릭터의 접지감이 같게 읽힌다(로드맵 4.10).
            var enemyShadow = Child(root.transform, "Shadow", new Vector3(0f, 0.1f, 0f));
            enemyShadow.localScale = new Vector3(0.78f, 0.78f, 1f);
            var enemyShadowRenderer = enemyShadow.gameObject.AddComponent<SpriteRenderer>();
            enemyShadowRenderer.sprite = LoadSprite(GeneratedDir + "/shadow.png");
            enemyShadowRenderer.sharedMaterial = litMaterial;
            enemyShadowRenderer.sortingOrder = 1;
            enemyShadowRenderer.color = new Color(1f, 1f, 1f, 0.78f);

            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = enemyFrames[0];
            renderer.sharedMaterial = litMaterial;
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
            enemy.EditorAssign(body, renderer, enemyFrames);
            PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.ImportAsset(EnemyPrefabPath, ImportAssetOptions.ForceSynchronousImport);
            var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            return prefabRoot.GetComponent<ModularGunnerEnemy>();
        }

        static void CreateScene(Material litMaterial, Material unlitMaterial, ModularGunnerCameraProfile cameraProfile)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraGo = new GameObject("Main Camera");
            SceneManager.MoveGameObjectToScene(cameraGo, scene);
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(-1f, -0.8f, -10f);
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = cameraProfile.orthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.026f, 0.038f);
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.allowDynamicResolution = false;
            camera.transparencySortMode = TransparencySortMode.CustomAxis;
            camera.transparencySortAxis = Vector3.down;
            var cameraData = cameraGo.AddComponent<UniversalAdditionalCameraData>();
            cameraData.antialiasing = AntialiasingMode.None;
            var pixelPerfect = cameraGo.AddComponent<PixelPerfectCamera>();
            pixelPerfect.assetsPPU = Ppu;
            pixelPerfect.refResolutionX = 320;
            pixelPerfect.refResolutionY = 180;
            pixelPerfect.gridSnapping = PixelPerfectCamera.GridSnapping.PixelSnapping;
            pixelPerfect.cropFrame = PixelPerfectCamera.CropFrame.Windowbox;

            var globalLightGo = new GameObject("Cool Ambient · Global Light 2D");
            SceneManager.MoveGameObjectToScene(globalLightGo, scene);
            var globalLight = globalLightGo.AddComponent<Light2D>();
            globalLight.lightType = Light2D.LightType.Global;
            globalLight.color = new Color(0.43f, 0.48f, 0.68f);
            globalLight.intensity = ModularGunnerLighting.AmbientIntensity;

            var gridGo = new GameObject("Monotone Test Room");
            SceneManager.MoveGameObjectToScene(gridGo, scene);
            gridGo.AddComponent<Grid>();
            var floor = CreateTilemap(gridGo.transform, "Floor", 0, litMaterial);
            var wallShadows = CreateTilemap(gridGo.transform, "Wall Contact Shadows", 2, unlitMaterial, true);
            wallShadows.transform.localPosition = new Vector3(0.08f, -0.56f, 0f);
            var wallFaces = new GameObject("Wall Front Faces").transform;
            wallFaces.SetParent(gridGo.transform, false);
            var walls = CreateTilemap(gridGo.transform, "Wall Top Caps", 9, litMaterial, true);
            // 승인 환경 키트 타일. 바닥 9종·벽 캡 4종을 변형으로 섞어 반복 무늬를 지운다.
            var floorTiles = new Tile[FloorVariants];
            for (int i = 0; i < FloorVariants; i++)
                floorTiles[i] = GetOrCreateTile($"{TileDir}/Floor_{i:00}.asset",
                    LoadSprite($"{EnvironmentDir}/env_floor_{i:00}.png"));

            var wallCapTiles = new Tile[WallVariants];
            for (int i = 0; i < WallVariants; i++)
            {
                wallCapTiles[i] = GetOrCreateTile($"{TileDir}/WallCap_{i:00}.asset",
                    LoadSprite($"{EnvironmentDir}/env_wall_cap_{i:00}.png"));
                wallCapTiles[i].colliderType = Tile.ColliderType.Grid;
                EditorUtility.SetDirty(wallCapTiles[i]);
            }
            var wallFaceSprites = new Sprite[WallVariants];
            for (int i = 0; i < WallVariants; i++)
                wallFaceSprites[i] = LoadSprite($"{EnvironmentDir}/env_wall_face_{i:00}.png");

            Tile wallShadowTile = GetOrCreateTile(GeneratedDir + "/WallContactShadow.asset", LoadSprite(WallShadowPath));
            wallShadowTile.colliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(wallShadowTile);

            // 배치는 전부 고정 시드다. 같은 커밋이면 항상 같은 방이 나온다.
            var environmentRandom = new System.Random(0x7A11E5);

            // 18×10 -> 36×20. 두 축을 각각 2배로 늘려 정확히 4배 면적이다.
            const int minX = -18, maxX = 17, minY = -10, maxY = 9;
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
                floor.SetTile(new Vector3Int(x, y, 0), floorTiles[WeightedPick(FloorWeights, environmentRandom)]);

            var occupied = new HashSet<Vector2Int>();
            for (int x = minX - 1; x <= maxX + 1; x++)
            {
                SetWall(walls, wallCapTiles, environmentRandom, occupied, x, minY - 1);
                SetWall(walls, wallCapTiles, environmentRandom, occupied, x, maxY + 1);
            }
            for (int y = minY; y <= maxY; y++)
            {
                SetWall(walls, wallCapTiles, environmentRandom, occupied, minX - 1, y);
                SetWall(walls, wallCapTiles, environmentRandom, occupied, maxX + 1, y);
            }

            // 고정 시드 랜덤 워크: 독립 점이 아니라 6~18칸짜리 덩어리 벽을 만든다.
            var random = new System.Random(0x47A11);
            for (int cluster = 0; cluster < 22; cluster++)
            {
                int cx = random.Next(minX + 2, maxX - 1);
                int cy = random.Next(minY + 2, maxY - 1);
                int cells = random.Next(6, 19);
                for (int step = 0; step < cells; step++)
                {
                    if (new Vector2(cx + 1f, cy + 0.8f).sqrMagnitude > 18f)
                        SetWall(walls, wallCapTiles, environmentRandom, occupied, cx, cy);
                    switch (random.Next(4))
                    {
                        case 0: cx++; break;
                        case 1: cx--; break;
                        case 2: cy++; break;
                        default: cy--; break;
                    }
                    cx = Mathf.Clamp(cx, minX + 1, maxX - 1);
                    cy = Mathf.Clamp(cy, minY + 1, maxY - 1);
                }
            }

            // 덩어리의 아래쪽 외곽에만 수직 전면과 접촉 그림자를 둔다.
            // 내부 셀에는 상단 캡만 남겨 하나의 연속된 벽 덩어리로 읽히게 한다.
            foreach (Vector2Int cell in occupied)
            {
                if (occupied.Contains(cell + Vector2Int.down)) continue;
                var position = new Vector3Int(cell.x, cell.y, 0);
                int faceVariant = WeightedPick(WallFaceWeights, environmentRandom);
                AddWallFace(wallFaces, cell, wallFaceSprites[faceVariant], litMaterial, faceVariant >= 2);
                wallShadows.SetTile(position, wallShadowTile);
            }
            walls.gameObject.AddComponent<TilemapCollider2D>();

            AddWallRims(gridGo.transform, unlitMaterial, occupied);

            ScatterRubble(gridGo.transform, litMaterial, occupied, environmentRandom, minX, maxX, minY, maxY);

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            var playerObject = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            var player = playerObject.GetComponent<ModularGunnerController>();
            player.transform.position = new Vector3(-1f, -0.8f, 0);
            foreach (Vector2 position in new[] { new Vector2(4, 2), new Vector2(7, -3), new Vector2(-7, 3.5f), new Vector2(12, 5f) })
            {
                var enemyObject = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab, scene);
                var enemy = enemyObject.GetComponent<ModularGunnerEnemy>();
                enemy.transform.position = position;
            }

            var cameraRig = cameraGo.AddComponent<ModularGunnerCameraRig>();
            cameraRig.EditorAssign(cameraProfile, player.transform, new Vector2(minX, minY), new Vector2(maxX + 1, maxY + 1));

            var arenaGo = new GameObject("Prototype Arena Controller");
            SceneManager.MoveGameObjectToScene(arenaGo, scene);
            var arena = arenaGo.AddComponent<ModularGunnerArena>();
            arena.EditorAssign(enemyPrefab.GetComponent<ModularGunnerEnemy>(), player.transform,
                new Vector2(minX + 1, minY + 1), new Vector2(maxX, maxY));

            var effectsGo = new GameObject("Gungeon-style Pixel Effects");
            SceneManager.MoveGameObjectToScene(effectsGo, scene);
            effectsGo.AddComponent<ModularGunnerEffects>().EditorAssign(
                LoadSprite(CasingPath), LoadSprite(StoneShardPath), LoadSprite(EnemyShardPath),
                LoadSprite(SparkPath), LoadSprite(BurstPath), LoadSprite(GeneratedDir + "/shadow.png"),
                litMaterial, unlitMaterial, cameraRig);
            effectsGo.AddComponent<ModularGunnerHitStop>();
            effectsGo.AddComponent<ModularGunnerDecals>().EditorAssign(
                LoadDecals("wall"), LoadDecals("floor"), LoadDecals("splat"), litMaterial);
            effectsGo.AddComponent<ModularGunnerFxPool>();
            effectsGo.AddComponent<ModularGunnerWallOcclusion>().EditorAssign(walls, unlitMaterial);

            var lamps = new List<Light2D>();
            foreach (Vector2 lampPosition in new[]
                     {
                         new Vector2(-13f, -6f), new Vector2(-7f, 6f), new Vector2(4f, -6f),
                         new Vector2(10f, 5f), new Vector2(15f, -2f), new Vector2(-1f, 7f),
                     })
                lamps.Add(AddLamp(scene, lampPosition, LoadSprite(LampPath), litMaterial));
            effectsGo.AddComponent<ModularGunnerAmbience>().EditorAssign(
                LoadSprite(DustMotePath), unlitMaterial, lamps.ToArray());

            var hud = new GameObject("Prototype HUD");
            SceneManager.MoveGameObjectToScene(hud, scene);
            hud.AddComponent<ModularGunnerHud>();
            hud.AddComponent<ModularGunnerCombatHud>();
            cameraGo.AddComponent<ModularGunnerCrtTuning>();

            AddPostProcessing(scene, cameraData);

            QualitySettings.antiAliasing = 0;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeObject = player;
        }

        /// <summary>
        /// 로드맵 4.12 — 포스트 프로세싱 마감.
        /// 픽셀 경계를 지우지 않도록 전부 보수적으로 잡는다. Bloom 은 임계값을 높여
        /// 총구 섬광과 피격 버스트만 걸리게 하고, 톤매핑은 끄고, AA·MSAA 는 비활성 상태를 유지한다.
        /// </summary>
        static void AddPostProcessing(Scene scene, UniversalAdditionalCameraData cameraData)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile != null) AssetDatabase.DeleteAsset(VolumeProfilePath);
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "ModularGunnerPostProcess";
            AssetDatabase.CreateAsset(profile, VolumeProfilePath);

            var bloom = AddOverride<Bloom>(profile);
            bloom.threshold.Override(0.82f);   // HDR 을 끈 상태라 1 이상은 존재하지 않는다.
            bloom.intensity.Override(0.34f);
            bloom.scatter.Override(0.42f);
            bloom.tint.Override(new Color(1f, 0.86f, 0.66f));

            // 검보라 암부 · 앰버 하이라이트. 시안은 결정과 적탄이 담당한다.
            // 색조만 살짝 기울인다. 세게 걸면 암반 바닥이 통째로 파랗게 죽는다.
            var smh = AddOverride<ShadowsMidtonesHighlights>(profile);
            smh.shadows.Override(new Vector4(0.95f, 0.96f, 1.07f, 0f));
            smh.midtones.Override(new Vector4(1f, 0.995f, 0.99f, 0f));
            smh.highlights.Override(new Vector4(1.06f, 1.01f, 0.9f, 0f));

            var grade = AddOverride<ColorAdjustments>(profile);
            // 대비를 올리면 어두운 암반이 먼저 뭉개진다. 노출을 조금 올려 상쇄한다.
            grade.postExposure.Override(0.16f);
            grade.contrast.Override(4f);
            grade.saturation.Override(5f);
            grade.colorFilter.Override(new Color(1f, 0.99f, 0.97f));

            var vignette = AddOverride<Vignette>(profile);
            vignette.intensity.Override(0.15f);
            vignette.smoothness.Override(0.5f);
            vignette.color.Override(new Color(0.05f, 0.04f, 0.09f));

            // 톤매핑은 끈다. 제한 팔레트를 그대로 통과시켜야 픽셀 색이 흔들리지 않는다.
            var tonemapping = AddOverride<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.None);

            EditorUtility.SetDirty(profile);

            var volumeGo = new GameObject("Post Process Volume");
            SceneManager.MoveGameObjectToScene(volumeGo, scene);
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;

            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.None;
        }

        static T AddOverride<T>(VolumeProfile profile) where T : VolumeComponent
        {
            T component = profile.Add<T>(true);
            component.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        static Tilemap CreateTilemap(Transform parent, string name, int sortingOrder, Material material, bool individual = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var map = go.AddComponent<Tilemap>();
            var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;
            renderer.sharedMaterial = material;
            renderer.mode = individual ? TilemapRenderer.Mode.Individual : TilemapRenderer.Mode.Chunk;
            return map;
        }

        static void SetWall(Tilemap walls, Tile[] capTiles, System.Random random, HashSet<Vector2Int> occupied, int x, int y)
        {
            var cell = new Vector2Int(x, y);
            if (!occupied.Add(cell)) return;
            var position = new Vector3Int(x, y, 0);
            walls.SetTile(position, capTiles[random.Next(capTiles.Length)]);
        }

        /// <summary>가중치 목록에서 하나를 고른다. 0 가중치 항목은 절대 뽑히지 않는다.</summary>
        static int WeightedPick(int[] weights, System.Random random)
        {
            int total = 0;
            for (int i = 0; i < weights.Length; i++) total += weights[i];
            int roll = random.Next(total);
            for (int i = 0; i < weights.Length; i++)
            {
                roll -= weights[i];
                if (roll < 0) return i;
            }
            return 0;
        }

        static Light2D AddLamp(Scene scene, Vector2 position, Sprite sprite, Material material)
        {
            var go = new GameObject("Warm Floor Lamp");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = 8;
            go.AddComponent<SortingGroup>().sortingOrder = ModularGunnerDepthSorter.OrderFor(position.y);
            go.AddComponent<ModularGunnerDepthSorter>();

            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = new Color(1f, 0.58f, 0.18f);
            light.intensity = ModularGunnerLighting.LampIntensity;
            light.pointLightInnerRadius = 0.8f;
            light.pointLightOuterRadius = 5.4f;
            light.pointLightInnerAngle = 360f;
            light.pointLightOuterAngle = 360f;
            return light;
        }

        static void AddWallFace(Transform parent, Vector2Int cell, Sprite sprite, Material material, bool crystal)
        {
            GameObject face = AddWallFace(parent, cell, sprite, material);
            if (!crystal) return;

            // 결정이 박힌 정면 변형은 아주 약한 점광원을 달아 암부에서 위치가 읽히게 한다.
            var glow = new GameObject($"Crystal Glow {cell.x},{cell.y}");
            glow.transform.SetParent(face.transform, false);
            glow.transform.localPosition = new Vector3(0f, -0.75f, 0f);
            var light = glow.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = new Color(0.72f, 0.42f, 1f);
            light.intensity = ModularGunnerLighting.CrystalIntensity;
            light.pointLightInnerRadius = 0.15f;
            light.pointLightOuterRadius = 1.9f;
            light.pointLightInnerAngle = 360f;
            light.pointLightOuterAngle = 360f;
        }

        static GameObject AddWallFace(Transform parent, Vector2Int cell, Sprite sprite, Material material)
        {
            var go = new GameObject($"Wall Face {cell.x},{cell.y}");
            go.transform.SetParent(parent, false);
            // Grid 셀의 아래쪽 경계가 벽과 캐릭터가 앞뒤를 바꾸는 기준선이다.
            go.transform.localPosition = new Vector3(cell.x + 0.5f, cell.y, 0f);
            ModularGunnerPhysicsLayers.Assign(go, ModularGunnerPhysicsLayers.World);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = ModularGunnerDepthSorter.OrderFor(go.transform.position.y);

            // 24px 전면 전체를 실제 벽으로 취급한다. 피벗이 위쪽이므로 콜라이더도
            // 바닥 접점에서 아래로 1.5유닛 내려가며, 보이는 옆면과 정확히 일치한다.
            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1f, 1.5f);
            collider.offset = new Vector2(0f, -0.75f);
            return go;
        }

        /// <summary>
        /// 벽 덩어리의 노출된 모서리에만 림을 붙인다. 덩어리 내부에는 아무것도 넣지 않으므로
        /// 이어 붙은 벽이 하나의 암반으로 읽히고, 바깥 경계에서만 실루엣이 생긴다.
        /// </summary>
        static void AddWallRims(Transform parent, Material material, HashSet<Vector2Int> occupied)
        {
            Sprite capRim = LoadSprite(WallRimCapPath);
            Sprite faceRim = LoadSprite(WallRimFacePath);
            Sprite topRim = LoadSprite(WallRimTopPath);

            var root = new GameObject("Wall Rims").transform;
            root.SetParent(parent, false);

            foreach (Vector2Int cell in occupied)
            {
                bool left = !occupied.Contains(cell + Vector2Int.left);
                bool right = !occupied.Contains(cell + Vector2Int.right);
                bool up = !occupied.Contains(cell + Vector2Int.up);
                bool hasFace = !occupied.Contains(cell + Vector2Int.down);

                if (up) AddRim(root, material, topRim, new Vector2(cell.x, cell.y + 1f), false, 10);
                if (left) AddRim(root, material, capRim, new Vector2(cell.x, cell.y), false, 10);
                if (right) AddRim(root, material, capRim, new Vector2(cell.x + 1f, cell.y), true, 10);
                if (!hasFace) continue;

                // 정면이 걸린 셀은 옆면도 1.5셀만큼 이어서 눌러 준다.
                int faceOrder = ModularGunnerDepthSorter.OrderFor(cell.y) + 1;
                if (left) AddRim(root, material, faceRim, new Vector2(cell.x, cell.y), false, faceOrder);
                if (right) AddRim(root, material, faceRim, new Vector2(cell.x + 1f, cell.y), true, faceOrder);
            }
        }

        static void AddRim(Transform parent, Material material, Sprite sprite, Vector2 position, bool flip, int sortingOrder)
        {
            var go = new GameObject("Rim");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.flipX = flip;
            renderer.sortingOrder = sortingOrder;
        }

        /// <summary>
        /// 벽에서 떨어져 나온 잔석·결정 소품을 열린 바닥에만 뿌린다(로드맵 4.1).
        /// 콜라이더 없이 발 위치 소팅만 따르므로 이동과 사격을 막지 않는다.
        /// </summary>
        static void ScatterRubble(Transform parent, Material material, HashSet<Vector2Int> occupied,
            System.Random random, int minX, int maxX, int minY, int maxY)
        {
            var props = new Sprite[RubbleVariants];
            for (int i = 0; i < RubbleVariants; i++)
                props[i] = LoadSprite($"{EnvironmentDir}/env_rubble_{i:00}.png");

            var root = new GameObject("Floor Rubble").transform;
            root.SetParent(parent, false);

            int placed = 0;
            for (int attempt = 0; attempt < 400 && placed < 46; attempt++)
            {
                int x = random.Next(minX, maxX + 1);
                int y = random.Next(minY, maxY + 1);
                var cell = new Vector2Int(x, y);
                if (occupied.Contains(cell)) continue;
                // 벽 바로 아래 칸은 정면 스프라이트가 덮으므로 비워 둔다.
                if (occupied.Contains(cell + Vector2Int.up)) continue;
                // 플레이어 시작 지점 주변은 깨끗하게 남긴다.
                if (new Vector2(x + 1f, y + 0.8f).sqrMagnitude < 12f) continue;

                var go = new GameObject($"Rubble {x},{y}");
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(
                    x + 0.5f + (float)(random.NextDouble() - 0.5) * 0.5f,
                    y + 0.5f + (float)(random.NextDouble() - 0.5) * 0.5f, 0f);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = props[random.Next(props.Length)];
                renderer.sharedMaterial = material;
                renderer.flipX = random.Next(2) == 0;
                renderer.sortingOrder = 6;
                go.AddComponent<SortingGroup>().sortingOrder = ModularGunnerDepthSorter.OrderFor(go.transform.position.y);
                placed++;
            }
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

        static Material GetOrCreateMaterial(string path, string shaderName)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException($"Missing shader: {shaderName}");
            if (material == null)
            {
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            else material.shader = shader;
            EditorUtility.SetDirty(material);
            return material;
        }

        static ModularGunnerCameraProfile GetOrCreateCameraProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<ModularGunnerCameraProfile>(CameraProfilePath);
            if (profile != null) return profile;
            profile = ScriptableObject.CreateInstance<ModularGunnerCameraProfile>();
            AssetDatabase.CreateAsset(profile, CameraProfilePath);
            return profile;
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

        static Sprite[] LoadDecals(string kind) =>
            DecalPaths()
                .Where(path => Path.GetFileName(path).StartsWith($"decal_{kind}_", StringComparison.Ordinal))
                .Select(LoadSprite)
                .ToArray();

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
