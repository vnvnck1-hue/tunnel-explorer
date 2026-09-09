using System;
using System.Collections.Generic;
using System.IO;
using TunnelCrew.Presentation;
using TunnelCrew.Presentation.Visual;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TunnelCrew.EditorTools
{
    /// <summary>
    /// 라이팅 프리셋 랩을 만든다 — 레퍼런스 캘리브레이션 V1 의 아트로 <b>플레이 가능한</b> 씬을
    /// 세우고, <see cref="LightingPresetSwitcher"/> 로 프리셋을 즉시 갈아 끼우게 한다.
    ///
    /// <b>이주 1단계(2026-09-09, docs/unity-port/reference-lab-migration-plan.md)</b> —
    /// VisualLab 의 아트 의존 없는 시스템이 이 씬에 들어왔다:
    /// <list type="bullet">
    /// <item>정렬 레이어를 <c>Default</c> → <see cref="VisualLayers"/> 로 옮겼다. 접촉 그림자는
    ///       <c>GroundDecal</c>, 실루엣은 <c>WorldFX</c> 에 자기 자리가 있어야 한다.</item>
    /// <item>프롭·드릴러는 <see cref="VisualHeightAnchor"/> 로 발 위치 정렬을 받는다.
    ///       앵커가 트랜스폼을 지배하므로 좌표는 <c>groundPosition</c>(셀) 에 둔다.</item>
    /// <item>접촉 그림자는 정식 <see cref="ContactShadowRenderer"/>. 임시 <see cref="LabShadowBlob"/> 은
    ///       네거티브 라이팅(§B-2, 정식 시스템 없음)에만 남긴다.</item>
    /// <item>벽은 <see cref="ForegroundOccluder"/> 다 — 드릴러가 뒤로 가면 페이드하고 실루엣이 올라온다.
    ///       §6.6 대조군으로 관심 대상이 아닌 위협 더미를 벽 뒤에 둔다.</item>
    /// </list>
    ///
    /// <b>캘리브레이션 도구와의 관계</b> — <see cref="BuildReferenceCalibrationPreview"/> 는
    /// "아트가 규격에 맞는가"를 결정론적으로 한 장 뽑는 도구다. 그 씬은 매번 새로 만들어 같은
    /// 경로에 덮어쓰므로 조명을 잡아둘 수 없다. 그래서 <b>아트 디렉터리만 공유</b>하고 씬·도구를
    /// 분리했다. 캘리브레이션 쪽 파일은 이 도구가 건드리지 않는다.
    ///
    /// <b>4단계로 미룬 것</b> — <c>EnvironmentChunkRenderer</c>·<c>ShadowGeometryBuilder</c>·
    /// <c>SetPieceSpawner</c> 는 벽 cap/front 타일러블과 세트피스 카탈로그가 필요하다.
    /// <c>DepthDebugOverlay</c> 는 <c>VisualLabController</c> 에 묶여 있어 가져오지 않는다.
    /// </summary>
    public static class BuildLightingPresetLab
    {
        const string ArtDir = "Assets/Art/Visual/ReferenceCalibrationV1";
        const string ScenePath = "Assets/_Project/Scenes/LightingPresetLab.unity";
        const string PresetDir = "Assets/_Project/Data/Visual/LightingPresets";
        const string AtmoProfilePath = "Assets/_Project/Data/Visual/AtmosphereProfile_Stratum1.asset";
        const string WorldProfilePath = "Assets/_Project/Data/Visual/WorldVisualProfile_Stratum1.asset";
        const string RuleSetPath = "Assets/_Project/Data/Visual/SurfaceRuleSet_Stratum1.asset";
        // 본편 RunBootstrap.BuildVolume 이 Resources.Load 하는 것과 같은 자산.
        const string VolumeDir = "Assets/_Project/Data/Resources";
        const string DefaultVolumePath = VolumeDir + "/Volume_Stratum1_Surface.asset";
        const string Stratum2VolumePath = VolumeDir + "/Volume_Stratum2_Fracture.asset";
        const string Stratum3VolumePath = VolumeDir + "/Volume_Stratum3_Core.asset";
        const string AbyssVolumePath = VolumeDir + "/Volume_Abyss.asset";

        /// <summary>지층별 대기 프로파일 — `BuildAtmosphereProfiles` 가 만든다.</summary>
        const string AtmosphereDir = "Assets/_Project/Data/Visual";

        /// <summary>마스크가 붙은 랩 전용 머티리얼이 사는 곳. 랩만 쓴다(본선은 SurfaceMaterialSet).</summary>
        const string MaterialDir = "Assets/_Project/Data/Visual/LabMaterials";

        /// <summary>`Renderer2D.asset` 슬롯 3 = `Additive with Mask` = B(습윤·결정).
        /// 정답지는 `docs/unity-port/mask-channel-convention.md`.</summary>
        const int MaskAdditiveSlot = 3;
        const string KitPath = "Assets/_Project/Data/Visual/EnvironmentKit_ReferenceV1.asset";
        const string FloorSetPath = "Assets/_Project/Data/Visual/SurfaceMaterialSet_Reference_floor.asset";
        const string WallTopSetPath = "Assets/_Project/Data/Visual/SurfaceMaterialSet_Reference_walltop.asset";
        const string WallFrontSetPath = "Assets/_Project/Data/Visual/SurfaceMaterialSet_Reference_wallfront.asset";

        /// <summary>
        /// 랩의 월드 원점 오프셋(셀). 환경 렌더러의 필드는 (0,0) 에서 시작하므로, 원점 중심이던
        /// 기존 좌표를 전부 이만큼 옮겨 방 내부(cols 1..15, rows 1..8)에 맞춘다. 4단계 선행 배선.
        /// </summary>
        static readonly Vector2 O = new Vector2(8f, 5f);
        static Vector3 W(float x, float y, float z = 0f) => new Vector3(x + O.x, y + O.y, z);
        static Vector2 G(float x, float y) => new Vector2(x + O.x, y + O.y);

        /// <summary>
        /// 방 정의 — 테두리 1칸 고체, 내부 15×8 빈칸(= 기존 손배치 바닥 격자와 같은 영역).
        /// 테두리는 카메라(ortho 4, 중심 8.5,5) 밖이라 벽 아트가 없어도 화면이 깨지지 않고,
        /// ShadowGeometryBuilder 는 그 윤곽으로 캐스터를 만든다.
        /// </summary>
        static readonly string[] Room =
        {
            "#################",
            "#...............#",
            "#...............#",
            "#...............#",
            "#...............#",
            "#...............#",
            "#...............#",
            "#...............#",
            "#...............#",
            "#################",
        };
        const string ThreatSpritePath = "Assets/Art/Visual/Placeholder/lab_threat_capsule.png";
        const string LitMaterialPath =
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";

        /// <summary>확정된 컬러감. 프리셋 사이에서 바꾸지 않는다.</summary>
        static readonly Color AmbientHue = new Color(0.62f, 0.50f, 0.76f, 1f);
        static readonly Color Background = new Color(0.018f, 0.008f, 0.028f, 1f);

        /// <summary>보드 승인 비트맵 3종만 쓴다 — 캘리브레이션 도구와 같은 제약이다.</summary>
        static readonly string[] FloorFiles =
        {
            "tr01_reference_floor_a_albedo.png",
            "tr01_reference_floor_b_albedo.png",
            "tr01_reference_floor_c_albedo.png"
        };

        [MenuItem("Tunnel Crew/비주얼 · 라이팅 프리셋 랩 생성", priority = 26)]
        public static void Run()
        {
            // 플레이 모드에서 EditorSceneManager.NewScene 은 "This cannot be used during play mode" 로 던진다.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[라이팅] 플레이 모드에서는 랩을 만들 수 없다. 플레이를 멈추고 다시 실행할 것.");
                return;
            }

            // 순서 주의 — 프리셋 자산을 먼저 만들지만 <b>참조를 들고 다니지 않는다.</b>
            // LoadSprites 의 SaveAndReimport 가 에셋 리임포트를 일으키면 방금 만든
            // ScriptableObject 인스턴스가 교체되어, 들고 있던 참조로 씬에 넣으면 전부
            // null({fileID: 0})로 직렬화된다(2026-09-09 에 실제로 이렇게 깨졌다).
            var presetPaths = EnsurePresets();
            var art = LoadSprites();
            BuildScene(art, presetPaths);

            Debug.Log("[라이팅] 프리셋 랩 생성 완료 — Play 를 눌러 숫자키 1~9·0 또는 ←/→ 로 전환한다. F1 발점 디버그.\n" +
                      $"씬: {ScenePath}\n프리셋: {PresetDir}");
        }

        // ───────────────────────────── 프리셋 자산

        readonly struct Spec
        {
            public readonly string File, Label, Note;
            public readonly float Ambient, LightScale, Blob, Negative, Post;
            public readonly LabShadowMode Shadow;
            /// <summary>블룸 오버라이드. 음수 = Volume 프로파일 값 유지.</summary>
            public readonly float BloomIntensity, BloomThreshold;
            /// <summary>지층 팔레트용 Volume 프로파일 자산 경로. null = 기본(Stratum1).</summary>
            public readonly string VolumePath;
            /// <summary>지층 팔레트용 대기 프로파일 자산 경로. null = 기본(Stratum1).</summary>
            public readonly string AtmospherePath;

            public Spec(string file, string label, float ambient, float lightScale,
                        LabShadowMode shadow, float blob, float negative, float post, string note,
                        float bloomIntensity = -1f, float bloomThreshold = -1f, string volumePath = null,
                        string atmospherePath = null)
            {
                File = file; Label = label; Ambient = ambient; LightScale = lightScale;
                Shadow = shadow; Blob = blob; Negative = negative; Post = post; Note = note;
                BloomIntensity = bloomIntensity; BloomThreshold = bloomThreshold;
                VolumePath = volumePath; AtmospherePath = atmospherePath;
            }
        }

        static readonly Spec[] Specs =
        {
            new Spec("LP1_Baseline", "① 현재 (기준선)", 0.62f, 1.00f, LabShadowMode.None, 0.55f, 0.45f, 1f,
                "레퍼런스 캘리브레이션 씬 재현. 그림자·후처리 없음. 나머지와 비교할 출발점."),
            new Spec("LP2_GameAmbient", "② 본편 기준 어둠", 0.35f, 1.00f, LabShadowMode.Blob, 0.50f, 0.45f, 1f,
                "WorldVisualProfile_Stratum1 의 앰비언트 세기(0.35). 캘리브레이션 씬은 이보다 1.8배 밝았다."),
            new Spec("LP3_Deep", "③ 짙은 어둠", 0.16f, 1.10f, LabShadowMode.Blob, 0.55f, 0.45f, 1f,
                "어둠만 낮춘다. 광원이 초점을 잡아주는지, 형태가 그래도 읽히는지 본다."),
            new Spec("LP4_Shadow", "④ 그림자 강조", 0.28f, 1.00f, LabShadowMode.BlobAndNegative, 0.60f, 0.50f, 1.2f,
                "암부(네거티브 라이팅)를 켠다. 광원을 늘리지 않고 대비를 만드는 방식."),
            new Spec("LP5_PostMid", "⑤ 후처리 중간", 0.35f, 1.00f, LabShadowMode.Blob, 0.50f, 0.45f, 2.5f,
                "안개·깊이 색분리·비네트·그레인 ×2.5. 현재 프로파일은 사실상 후처리가 꺼져 있다."),
            new Spec("LP6_PostStrong", "⑥ 후처리 강함", 0.20f, 1.10f, LabShadowMode.Blob, 0.50f, 0.45f, 5f,
                "×5. Katana ZERO 모델 — 광원이 아니라 화면 필터가 무드를 만드는 쪽."),
            new Spec("LP7_Mood", "⑦ 무드 (조합)", 0.22f, 1.20f, LabShadowMode.BlobAndNegative, 0.60f, 0.45f, 3f,
                "어둠 + 암부 + 후처리를 함께. 실제 채택 후보 1순위."),
            new Spec("LP8_LightForward", "⑧ 광원 강조 (Ori 방향)", 0.30f, 1.70f, LabShadowMode.BlobAndNegative, 0.55f, 0.35f, 2f,
                "광원을 세게. 손칠 라이팅 없이 광원만으로 어디까지 가는지 확인하는 대조군."),

            // ── 리서치 결론 ②·③ 을 직접 당기는 두 장 (2026-09-09 추가)
            new Spec("LP9_Bloom", "⑨ 블룸 강조", 0.22f, 1.20f, LabShadowMode.BlobAndNegative, 0.60f, 0.45f, 3f,
                "⑦ 무드에 URP Bloom 만 세게(1.6 / thr 0.55, 본편 지층1 은 0.60 / 0.70). '빛나는 것이 빛나는가' — 리서치 2순위 지렛대.",
                bloomIntensity: 1.6f, bloomThreshold: 0.55f),
            new Spec("LP10_Stratum2Palette", "⑩ 지층2 팔레트", 0.24f, 1.20f, LabShadowMode.BlobAndNegative, 0.60f, 0.45f, 2.2f,
                "⑦ 무드 그대로, 팔레트만 지층2(Fracture: 대비 8·채도 2 / 안개 .30·비네트 .42·그레인 .026). 광원을 늘리지 않고 '다른 지층'을 만드는 Rain World 식.",
                volumePath: Stratum2VolumePath, atmospherePath: AtmosphereDir + "/AtmosphereProfile_Stratum2.asset"),

            // ── 지층 팔레트 축 완성 (2026-09-09). Volume 과 대기 프로파일을 <b>함께</b> 갈아끼운다 —
            // 전에는 ⑩ 이 Volume 만 바꿔서 안개·비네트·그레인은 지층 1 값 그대로였다.
            new Spec("LP11_Stratum3Palette", "⑪ 지층3 팔레트", 0.26f, 1.20f, LabShadowMode.BlobAndNegative, 0.62f, 0.50f, 1.8f,
                "중심부(Core: 대비 10·채도 −4 / 안개 .38·비네트 .50·그레인 .034). 위에서 내리누르는 압력 — 시야가 좁고 색이 빠진다. 후처리 배율은 1.8 — 프로파일이 이미 과감하므로 ×3 을 곱하면 그레인이 형태를 덮는다(2026-09-09 캡처로 확인).",
                volumePath: Stratum3VolumePath, atmospherePath: AtmosphereDir + "/AtmosphereProfile_Stratum3.asset"),
            new Spec("LP12_AbyssPalette", "⑫ 이상지대 팔레트", 0.24f, 1.25f, LabShadowMode.BlobAndNegative, 0.65f, 0.55f, 1.5f,
                "이상지대(Abyss: 대비 12·채도 −12 / 안개 .46·비네트 .58·그레인 .044). 색이 거의 빠지고 그레인이 화면 정체성이 된다 — Katana ZERO·SIGNALIS 대역.",
                volumePath: AbyssVolumePath, atmospherePath: AtmosphereDir + "/AtmosphereProfile_Abyss.asset"),
        };

        /// <summary>
        /// 프리셋 자산을 보장하고 <b>경로만</b> 돌려준다. 오브젝트 참조를 돌려주면 이후 에셋
        /// 리임포트가 인스턴스를 교체해 버려 씬에 null 이 박힌다.
        /// </summary>
        static string[] EnsurePresets()
        {
            Directory.CreateDirectory(AbsolutePath(PresetDir));

            var paths = new List<string>(Specs.Length);
            foreach (var spec in Specs)
            {
                string path = $"{PresetDir}/{spec.File}.asset";
                var preset = AssetDatabase.LoadAssetAtPath<LightingPreset>(path);
                bool created = preset == null;

                if (created)
                {
                    preset = ScriptableObject.CreateInstance<LightingPreset>();
                    AssetDatabase.CreateAsset(preset, path);
                }

                // 이미 있는 자산의 값은 덮지 않는다 — 사용자가 인스펙터에서 조정한 결과를
                // 랩을 다시 만들 때마다 잃으면 안 된다.
                if (created)
                {
                    preset.label = spec.Label;
                    preset.note = spec.Note;
                    preset.ambientColor = AmbientHue;
                    preset.ambientIntensity = spec.Ambient;
                    preset.lightScale = spec.LightScale;
                    preset.shadowMode = spec.Shadow;
                    preset.blobStrength = spec.Blob;
                    preset.negativeStrength = spec.Negative;
                    preset.postScale = spec.Post;
                    preset.bloomIntensity = spec.BloomIntensity;
                    preset.bloomThreshold = spec.BloomThreshold;
                    if (!string.IsNullOrEmpty(spec.VolumePath))
                    {
                        preset.volumeProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(spec.VolumePath);
                        if (preset.volumeProfile == null)
                            Debug.LogWarning($"[라이팅] Volume 프로파일이 없다: {spec.VolumePath} — " +
                                             "'Tunnel Crew/M2 · 지층별 Volume 프로파일 생성' 을 먼저 실행할 것.");
                    }
                    if (!string.IsNullOrEmpty(spec.AtmospherePath))
                    {
                        preset.atmosphereProfile =
                            AssetDatabase.LoadAssetAtPath<AtmosphereProfile>(spec.AtmospherePath);
                        if (preset.atmosphereProfile == null)
                            Debug.LogWarning($"[라이팅] 대기 프로파일이 없다: {spec.AtmospherePath} — " +
                                             "'Tunnel Crew/비주얼 · 지층별 대기 프로파일 생성' 을 먼저 실행할 것.");
                    }
                    EditorUtility.SetDirty(preset);
                }

                // 값은 덮지 않지만 비어 있는 참조는 채운다.
                //
                // 2026-09-09: 프리셋 10 에 대기 프로파일 축을 새로 추가했는데 자산이 이미 있어서
                // `if (created)` 블록을 타지 않았다. 결과가 "Volume 은 지층2, 안개는 지층1" 이라
                // 팔레트 전환이 반쪽만 됐다(캡처로 확인).
                //
                // 수치는 사용자가 인스펙터에서 조정한 결과이므로 계속 보존한다. 반면 null 참조는
                // 조정 결과가 아니라 "그 축이 아직 없던 시절의 흔적" 이라 채우는 것이 맞다.
                if (!string.IsNullOrEmpty(spec.AtmospherePath) && preset.atmosphereProfile == null)
                {
                    preset.atmosphereProfile =
                        AssetDatabase.LoadAssetAtPath<AtmosphereProfile>(spec.AtmospherePath);
                    if (preset.atmosphereProfile != null)
                    {
                        EditorUtility.SetDirty(preset);
                        Debug.Log($"[라이팅] {spec.File}: 비어 있던 대기 프로파일을 채웠다 - " +
                                  Path.GetFileNameWithoutExtension(spec.AtmospherePath));
                    }
                }
                if (!string.IsNullOrEmpty(spec.VolumePath) && preset.volumeProfile == null)
                {
                    preset.volumeProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(spec.VolumePath);
                    if (preset.volumeProfile != null)
                    {
                        EditorUtility.SetDirty(preset);
                        Debug.Log($"[라이팅] {spec.File}: 비어 있던 Volume 프로파일을 채웠다 - " +
                                  Path.GetFileNameWithoutExtension(spec.VolumePath));
                    }
                }

                paths.Add(path);
            }

            AssetDatabase.SaveAssets();
            return paths.ToArray();
        }

        /// <summary>
        /// 경로에서 프리셋을 다시 읽는다. 하나라도 없으면 던진다 — 예전 버전은 null 을 그대로
        /// 씬에 넣어 "버튼도 숫자키도 안 먹는" 상태를 조용히 만들었다.
        /// </summary>
        static LightingPreset[] ReloadPresets(string[] paths)
        {
            var presets = new LightingPreset[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                presets[i] = AssetDatabase.LoadAssetAtPath<LightingPreset>(paths[i]);
                if (presets[i] == null)
                    throw new InvalidOperationException($"LightingPreset 을 읽지 못했다: {paths[i]}");
            }
            return presets;
        }

        // ───────────────────────────── 아트

        sealed class Sprites
        {
            public Sprite[] Floors;
            public Sprite Wall, Crystal, Driller, Lamp, Threat;
        }

        static Sprites LoadSprites()
        {
            foreach (string f in FloorFiles) EnsureSprite($"{ArtDir}/{f}", new Vector2(0.5f, 0.5f), false);
            EnsureSprite($"{ArtDir}/tr01_reference_wall_a_albedo.png", new Vector2(0.5f, 0.02f), true);
            EnsureSprite($"{ArtDir}/tr01_reference_crystal_a_albedo.png", new Vector2(0.5f, 0.03f), true);
            EnsureSprite($"{ArtDir}/tr01_reference_driller_a_albedo.png", new Vector2(0.5f, 0.03f), true);
            EnsureSprite($"{ArtDir}/tr01_reference_lamp_a_albedo.png", new Vector2(0.5f, 0.03f), true);
            EnsureThreatSprite();

            return new Sprites
            {
                Floors = Array.ConvertAll(FloorFiles, f => Load($"{ArtDir}/{f}")),
                Wall = Load($"{ArtDir}/tr01_reference_wall_a_albedo.png"),
                Crystal = Load($"{ArtDir}/tr01_reference_crystal_a_albedo.png"),
                Driller = Load($"{ArtDir}/tr01_reference_driller_a_albedo.png"),
                Lamp = Load($"{ArtDir}/tr01_reference_lamp_a_albedo.png"),
                Threat = Load(ThreatSpritePath),
            };
        }

        /// <summary>
        /// 캘리브레이션 도구와 <b>같은</b> 임포트 설정을 보장한다. 이미 맞으면 다시 임포트하지
        /// 않는다 — 공유 에셋을 매번 건드리면 캘리브레이션 캡처와 픽셀이 어긋날 수 있다.
        /// </summary>
        static void EnsureSprite(string path, Vector2 pivot, bool alpha)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(path) as TextureImporter;
            }
            if (importer == null) throw new InvalidOperationException($"TextureImporter not found: {path}");

            bool ok = importer.textureType == TextureImporterType.Sprite
                      && Mathf.Approximately(importer.spritePixelsPerUnit, 128f)
                      && AssetDatabase.LoadAssetAtPath<Sprite>(path) != null;
            if (ok) return;

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

        /// <summary>
        /// §6.6 대조군용 위협 더미 스프라이트. 레퍼런스 아트에는 적이 없고, 메모리 텍스처는
        /// 씬 저장에서 사라지므로 Placeholder 폴더에 PNG 로 굽는다. 이미 있으면 다시 만들지 않는다.
        /// </summary>
        static void EnsureThreatSprite()
        {
            string abs = AbsolutePath(ThreatSpritePath);
            if (!File.Exists(abs))
            {
                const int w = 48, h = 96;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var px = new Color32[w * h];
                var body = new Color(0.72f, 0.30f, 0.32f);
                float r = w * 0.5f;
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float cx = x + 0.5f - r, cy = y + 0.5f;
                    // 캡슐: 위아래 반원 + 중간 직선.
                    float dy = cy < r ? cy - r : (cy > h - r ? cy - (h - r) : 0f);
                    bool inside = cx * cx + dy * dy <= r * r;
                    px[y * w + x] = inside
                        ? new Color32((byte)(body.r * 255), (byte)(body.g * 255), (byte)(body.b * 255), 255)
                        : new Color32(0, 0, 0, 0);
                }
                tex.SetPixels32(px);
                tex.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(abs)!);
                File.WriteAllBytes(abs, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(ThreatSpritePath, ImportAssetOptions.ForceSynchronousImport);
            }
            EnsureSprite(ThreatSpritePath, new Vector2(0.5f, 0f), true);
        }

        static Sprite Load(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException($"Sprite not found: {path}");
            return sprite;
        }

        // ───────────────────────────── 씬

        static void BuildScene(Sprites art, string[] presetPaths)
        {
            var existing = SceneManager.GetSceneByPath(ScenePath);
            if (existing.IsValid() && existing.isLoaded)
                EditorSceneManager.CloseScene(existing, true);

            var worldProfile = AssetDatabase.LoadAssetAtPath<WorldVisualProfile>(WorldProfilePath);
            if (worldProfile == null)
                throw new InvalidOperationException($"WorldVisualProfile not found: {WorldProfilePath}");
            var atmoProfile = AssetDatabase.LoadAssetAtPath<AtmosphereProfile>(AtmoProfilePath);
            if (atmoProfile == null)
                throw new InvalidOperationException($"AtmosphereProfile not found: {AtmoProfilePath}");

            // 앵커가 groundPosition(셀) 을 이 투영으로 화면에 놓는다. 정적 기본값은 2:1 마름모라
            // 여기서 제작 기준(ReferenceTopDown = 항등)으로 맞춘다 — 그래야 아래 좌표가 그대로 보인다.
            IsometricProjection.SetPreset(worldProfile.authoredProjection);

            // 정렬 레이어 12종이 있어야 한다(Tunnel Crew/비주얼 · 소팅 레이어 생성).
            foreach (string layer in VisualLayers.InOrder)
                if (!VisualLayers.Exists(layer))
                    throw new InvalidOperationException($"Sorting Layer '{layer}' 가 없다. 먼저 소팅 레이어를 생성할 것.");

            Directory.CreateDirectory(Path.GetDirectoryName(AbsolutePath(ScenePath))!);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "LightingPresetLab";

            // 카메라 — 캘리브레이션과 동일 프레이밍이라 같은 구도로 비교된다.
            var cameraGo = NewObject(scene, "Lab Camera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            // 방 내부(x 1..16, y 1..9)의 중심. 세로 8셀 = ortho 4 와 정확히 맞는다.
            cameraGo.transform.position = new Vector3(8.5f, 5f, -10f);
            cameraGo.AddComponent<AudioListener>();
            // URP 후처리(Bloom 등)는 카메라마다 켜야 한다. 이게 없으면 Volume 이 있어도 아무것도 안 보인다.
            var camData = cameraGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;

            // ── 전역 Volume — 본편 RunBootstrap.BuildVolume 과 같은 지층 프로파일. 스위처가 프리셋마다
            // 런타임 복제본을 얹으므로 여기 sharedProfile 은 기본값이자 "플레이 전 에디터에서 보이는 것"이다.
            var defaultVolume = AssetDatabase.LoadAssetAtPath<VolumeProfile>(DefaultVolumePath);
            if (defaultVolume == null)
                throw new InvalidOperationException(
                    $"Volume 프로파일이 없다: {DefaultVolumePath} — 'Tunnel Crew/M2 · 지층별 Volume 프로파일 생성' 을 먼저 실행할 것.");
            var volumeGo = NewObject(scene, "Global Volume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = defaultVolume;

            // ── 환경 렌더러(4단계 선행 배선). 바닥은 이제 손배치 격자가 아니라 키트 경로로 그린다.
            // 벽 배열이 비어 있으면 벽 타일만 빠진다 — 아트가 오면 키트만 채우고 다시 플레이한다.
            var ruleSet = AssetDatabase.LoadAssetAtPath<SurfaceRuleSet>(RuleSetPath);
            var kit = AssetDatabase.LoadAssetAtPath<EnvironmentKit>(KitPath);
            var floorSet = AssetDatabase.LoadAssetAtPath<SurfaceMaterialSet>(FloorSetPath);
            var wallTopSet = AssetDatabase.LoadAssetAtPath<SurfaceMaterialSet>(WallTopSetPath);
            var wallFrontSet = AssetDatabase.LoadAssetAtPath<SurfaceMaterialSet>(WallFrontSetPath);
            if (ruleSet == null || kit == null || floorSet == null)
                throw new InvalidOperationException(
                    "환경 키트 자산이 없다 — 먼저 'Tunnel Crew/비주얼 · 레퍼런스 환경 키트 생성 (3단계)' 을 실행할 것.");

            var envGo = NewObject(scene, "Environment");
            var env = envGo.AddComponent<EnvironmentChunkRenderer>();
            env.EditorAssign(worldProfile, ruleSet, kit, floorSet, wallTopSet, wallFrontSet);

            // ── 정식 깊이·그림자 시스템(1단계). 개체보다 먼저 있어야 OnEnable 등록이 잡힌다.
            var depthGo = NewObject(scene, "Depth");
            depthGo.AddComponent<FootpointSorter>().Profile = worldProfile;
            depthGo.AddComponent<ForegroundFadeController>().Profile = worldProfile;
            depthGo.AddComponent<OccludedSilhouetteRenderer>().Profile = worldProfile;

            var shadowGo = NewObject(scene, "Shadows");
            shadowGo.AddComponent<ContactShadowRenderer>().Profile = worldProfile;
            // 벽 윤곽 캐스터(§7.4-3) — 런타임에 LabEnvironment 가 env.IsWallCell 로 바인드한다.
            var wallShadows = shadowGo.AddComponent<ShadowGeometryBuilder>();

            var labEnv = envGo.AddComponent<LabEnvironment>();
            labEnv.EditorAssign(Room, env, wallShadows);

            // ── 프롭. 앵커가 발 위치를 잡는다 — 좌표는 groundPosition 에 둔다(트랜스폼은 앵커가 쓴다).
            var propRoot = NewObject(scene, "Props").transform;

            var wall = AddAnchored(propRoot, "Reference Wall", art.Wall, G(3.25f, 0.82f),
                VisualLayers.BackStructure, new Vector2(1.6f, 1f), 0.8f);
            // 벽은 전경 오클루더 — 드릴러가 뒤(더 큰 y)로 가면 페이드하고 실루엣이 올라온다(§6.6).
            var occluder = wall.gameObject.AddComponent<ForegroundOccluder>();
            occluder.footprintCells = new Rect(2.45f + O.x, 0.82f + O.y, 1.6f, 2.9f);   // 벽 패널이 화면에서 덮는 셀 영역
            occluder.fadeGroup = 1000; // 렌더러의 청크 페이드 그룹(0..N)과 겹치지 않게
            occluder.sprites = new[] { wall.GetComponentInChildren<SpriteRenderer>() };

            AddAnchored(propRoot, "Reference Crystal", art.Crystal, G(-4.15f, 0.7f),
                VisualLayers.BackStructure, new Vector2(1.2f, 1f), 0.6f);
            AddAnchored(propRoot, "Reference Lamp", art.Lamp, G(4.65f, -2.55f),
                VisualLayers.BackStructure, new Vector2(0.4f, 1f), 0.25f);

            // ── 움직이는 드릴러 — 관심 캐릭터. 광원이 움직여야 바닥이 어떻게 밝아지는지 보인다.
            var pawn = AddAnchored(propRoot, "Driller Pawn", art.Driller, G(-0.75f, -0.75f),
                VisualLayers.WorldEntity, Vector2.one, 0.45f,
                LabAnchoredEntity.Silhouette.Interest, new Color(0.42f, 0.82f, 1f));
            pawn.isLocalInterest = true;
            pawn.wantsSilhouette = true;
            var mover = pawn.gameObject.AddComponent<LightingLabPawn>();
            // 이동 범위 = 방 내부(테두리 1칸 안쪽). X/C 파괴는 앞 칸이므로 위쪽 여유를 둔다.
            var mso = new SerializedObject(mover);
            mso.FindProperty("_bounds").rectValue = new Rect(1.5f, 1.4f, 13f, 6.4f);
            mso.ApplyModifiedPropertiesWithoutUndo();

            // 탐색광 — 드릴러가 파는 쪽(오른쪽) 앞. 콘 각도의 방향 규약에 기대지 않고도
            // 헤드램프가 앞을 비추는 것으로 읽힌다. 앵커의 자식이라 함께 움직인다.
            var scout = AddLight(pawn.transform, "Scout (LightClass.Scout)",
                Light2D.LightType.Point, new Vector3(1.10f, 0.45f, -0.1f),
                new Color(1f, 0.94f, 0.86f, 1f), 1.45f, local: true);
            scout.pointLightInnerRadius = 0.25f;
            scout.pointLightOuterRadius = 3.1f;
            scout.falloffIntensity = 0.55f;
            AddSocket(scout, LightClass.Scout, 1.45f, 3.1f, 0.00f);

            // ── 위협 더미 — 관심 대상이 아니다. 벽 뒤에 두어 "적은 완전 투명 처리하지 않고 위협
            // 실루엣만 보장"(§6.6) 을 보는 대조군. 그 앞의 벽은 페이드하지 않으므로 실루엣이
            // 벽 위로 올라오는지가 유일한 확인 방법이다.
            var threat = AddAnchored(propRoot, "Threat Dummy", art.Threat, G(3.3f, 1.7f),
                VisualLayers.WorldEntity, Vector2.one, 0.4f,
                LabAnchoredEntity.Silhouette.Threat, new Color(1f, 0.34f, 0.30f));
            threat.isLocalInterest = false;
            threat.wantsSilhouette = true;

            // ── 광원 리그(2단계: 정식 소켓 파이프라인). 전역광만 소켓 밖이다 — VisualLab 과 같다.
            // LightSocketRenderer 가 LateUpdate 마다 분류별 깜빡임·노멀 품질·비추는 레이어·
            // 그림자 예산(§13)을 집행한다. 그래서 세기는 Light2D 가 아니라 LightSocket.baseIntensity 가
            // 진짜 값이고, 프리셋의 lightScale 도 그쪽을 민다.
            var lightRoot = NewObject(scene, "Lights").transform;
            var lightSockets = lightRoot.gameObject.AddComponent<LightSocketRenderer>();
            lightSockets.FreezeFlicker = false;

            var global = AddLight(lightRoot, "Global (ambient)", Light2D.LightType.Global,
                Vector3.zero, AmbientHue, 0.35f);

            var worklamp = AddLight(lightRoot, "Worklamp (LightClass.Worklamp)",
                Light2D.LightType.Point, W(4.65f, -1.52f, -0.1f),
                new Color(0.92f, 0.16f, 1f, 1f), 2.35f);
            worklamp.pointLightInnerRadius = 0.18f;
            worklamp.pointLightOuterRadius = 2.4f;
            worklamp.falloffIntensity = 0.48f;
            AddSocket(worklamp, LightClass.Worklamp, 2.35f, 2.4f, 0.31f);

            // 광물광은 <b>마스크 애디티브(슬롯 3 = B 습윤·결정)</b> — 마스크 채널 규약 확정(2026-09-09).
            // 라이트 하나가 화면 전체의 광맥·수정만 골라 발광시킨다. Katana ZERO 의 글로우 부품이고
            // 분류상 비그림자다. 전에는 슬롯 1(마스크 없는 애디티브)이라 닿는 모든 픽셀이 밝아졌다.
            var mineral = AddLight(lightRoot, "MineralGlow (LightClass.MineralGlow)",
                Light2D.LightType.Point, W(-4.15f, 1.05f, -0.1f),
                new Color(0.72f, 0.42f, 1f, 1f), 1.6f);
            mineral.pointLightInnerRadius = 0.1f;
            mineral.pointLightOuterRadius = 1.7f;
            mineral.falloffIntensity = 0.6f;
            mineral.blendStyleIndex = MaskAdditiveSlot;
            // 마스크가 골라내므로 반경을 넓혀도 회색 암석은 밝아지지 않는다 — 그게 이 채널의 값이다.
            mineral.pointLightOuterRadius = 3.2f;
            AddSocket(mineral, LightClass.MineralGlow, 1.6f, 3.2f, 0.67f);

            // 표시등 — 반지름 1.5칸·세기 0.4 이하라 LightClassRules 의 표시등 조건에 든다. 예산 제외.
            var indicator = AddLight(lightRoot, "Indicator (LightClass.Indicator)",
                Light2D.LightType.Point, W(3.05f, 1.55f, -0.1f),
                new Color(0.45f, 0.92f, 1f, 1f), 0.38f);
            indicator.pointLightInnerRadius = 0.05f;
            indicator.pointLightOuterRadius = 0.6f;
            indicator.blendStyleIndex = 1;
            AddSocket(indicator, LightClass.Indicator, 0.38f, 0.6f, 0.89f);

            // ── 네거티브 라이팅 — 아직 정식 시스템이 없는 격차(§B-2). 임시 블롭이 맡는다.
            // 접촉 그림자 블롭은 1단계에서 ContactShadowRenderer 로 대체되어 여기 없다.
            var negRoot = NewObject(scene, "Negative Lighting (freeform)").transform;
            // 사각형 암부 — 전과 같은 자리, 이제 다각형이다.
            AddNegativeVolume(negRoot, "Negative TopLeft", W(-5.6f, 3.0f),
                NegativeLightVolume.Rect(new Vector2(6.0f, 3.2f)));
            AddNegativeVolume(negRoot, "Negative TopRight", W(6.0f, 2.9f),
                NegativeLightVolume.Rect(new Vector2(5.4f, 3.4f)));
            // 갱도 입구 — 위로 좁아지는 사다리꼴. 타원 블롭으로는 만들 수 없던 형태이고,
            // 이 컴포넌트가 왜 있는지를 화면에서 보여 주는 자리다(§B-2).
            AddNegativeVolume(negRoot, "Negative Mouth BottomLeft", W(-6.2f, -3.0f),
                NegativeLightVolume.Mouth(5.2f, 2.8f, 0.42f));

            // ── 대기 원근. 카메라 자식이어야 Bind 가 카메라를 잡는다.
            var atmoGo = new GameObject("Atmosphere");
            atmoGo.transform.SetParent(cameraGo.transform, false);
            var atmosphere = atmoGo.AddComponent<AtmosphereDirector>();
            atmosphere.FreezeGrain = false;
            atmosphere.Bind(camera, atmoProfile);

            // ── 품질 단계·접근성(§13·§10.3). 광원 소켓 렌더러와 대기 감독을 묶는다.
            var optionsGo = NewObject(scene, "Visual Options");
            optionsGo.AddComponent<VisualOptionsController>().Bind(lightSockets, atmosphere);

            // ── 디버그 선(발점·전경 그룹). 스위처가 F1 로 켠다.
            NewObject(scene, "Debug Lines").AddComponent<VisualDebugLines>();

            // ── 프리셋 스위처. 참조는 전부 여기서 <b>지금</b> 디스크에서 다시 읽어 직접 넣는다.
            // SerializedObject 경로로 넣은 프로파일 참조가 null 로 직렬화된 일이 있어(2026-09-09)
            // 다른 시스템과 같은 직접 할당(EditorAssign)으로 통일했다.
            var presets = ReloadPresets(presetPaths);
            var atmoForSwitcher = AssetDatabase.LoadAssetAtPath<AtmosphereProfile>(AtmoProfilePath);
            var worldForSwitcher = AssetDatabase.LoadAssetAtPath<WorldVisualProfile>(WorldProfilePath);

            // ── 무드 축(깊이·위기). 지층 팔레트를 축 하나로 구동하고 인스펙터 스크럽을 준다.
            // 프리셋(⑩⑪⑫)이 팔레트를 손으로 고르는 길이고, 이쪽은 깊이로 자동 선택하는 길이다.
            // 둘 다 남긴다 — 랩에서는 손으로 비교하고, 본선에서는 깊이가 정한다.
            var moodGo = NewObject(scene, "Stratum Mood");
            var mood = moodGo.AddComponent<StratumMoodDirector>();
            mood.atmosphereByStratum = new[]
            {
                AssetDatabase.LoadAssetAtPath<AtmosphereProfile>(AtmosphereDir + "/AtmosphereProfile_Stratum1.asset"),
                AssetDatabase.LoadAssetAtPath<AtmosphereProfile>(AtmosphereDir + "/AtmosphereProfile_Stratum2.asset"),
                AssetDatabase.LoadAssetAtPath<AtmosphereProfile>(AtmosphereDir + "/AtmosphereProfile_Stratum3.asset"),
                AssetDatabase.LoadAssetAtPath<AtmosphereProfile>(AtmosphereDir + "/AtmosphereProfile_Abyss.asset"),
            };
            mood.volumeByStratum = new[]
            {
                AssetDatabase.LoadAssetAtPath<VolumeProfile>(DefaultVolumePath),
                AssetDatabase.LoadAssetAtPath<VolumeProfile>(Stratum2VolumePath),
                AssetDatabase.LoadAssetAtPath<VolumeProfile>(Stratum3VolumePath),
                AssetDatabase.LoadAssetAtPath<VolumeProfile>(AbyssVolumePath),
            };
            for (int i = 0; i < mood.atmosphereByStratum.Length; i++)
                if (mood.atmosphereByStratum[i] == null)
                    Debug.LogWarning($"[라이팅] 지층 {i + 1} 대기 프로파일이 없다 — " +
                                     "'Tunnel Crew/비주얼 · 지층별 대기 프로파일 생성' 을 먼저 실행할 것.");
            EditorUtility.SetDirty(mood);

            var labGo = NewObject(scene, "Lighting Lab");
            var switcher = labGo.AddComponent<LightingPresetSwitcher>();
            var volumeForSwitcher = AssetDatabase.LoadAssetAtPath<VolumeProfile>(DefaultVolumePath);
            switcher.EditorAssign(presets, global, atmosphere, atmoForSwitcher, worldForSwitcher,
                startIndex: 1, volume: volume, defaultVolumeProfile: volumeForSwitcher); // ② 본편 기준으로 시작
            EditorUtility.SetDirty(switcher);

            // 직렬화가 실제로 붙었는지 확인한다. 참조가 null 로 박히면 UI 가 아무것도 그리지
            // 않거나 축 하나가 조용히 죽는다 — 증상만 남고 원인이 보이지 않는다.
            var verifySo = new SerializedObject(switcher);
            var verify = verifySo.FindProperty("_presets");
            for (int i = 0; i < verify.arraySize; i++)
            {
                if (verify.GetArrayElementAtIndex(i).objectReferenceValue == null)
                    throw new InvalidOperationException($"프리셋 참조 {i} 번이 씬에 붙지 않았다.");
            }
            foreach (string field in new[] { "_globalLight", "_atmosphere", "_baseProfile", "_worldProfile", "_volume", "_defaultVolumeProfile" })
            {
                if (verifySo.FindProperty(field).objectReferenceValue == null)
                    throw new InvalidOperationException($"스위처 참조 {field} 가 null 로 직렬화됐다.");
            }

            // 에디터에서도 앵커 위치·정렬이 보이게 한 번 적용한다(플레이 전에는 LateUpdate 가 없다).
            FootpointSorter.ApplyAll(worldProfile.depthUnitsPerCell);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            // 씬을 열어 둔 채로 끝낸다 — 사용자가 바로 Play 를 누를 수 있어야 한다.
        }

        static GameObject NewObject(Scene scene, string name)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        static SpriteRenderer AddSprite(Transform parent, string name, Sprite sprite, Vector3 position, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            var lit = AssetDatabase.LoadAssetAtPath<Material>(LitMaterialPath);
            if (lit == null) throw new InvalidOperationException("URP Sprite-Lit-Default material was not found");
            sr.sharedMaterial = lit;
            AttachMaskIfAny(sr, sprite);
            return sr;
        }

        /// <summary>
        /// 스프라이트에 짝이 되는 `_mask` 텍스처가 있으면 <b>전용 머티리얼</b>에 붙인다.
        ///
        /// 공용 `Sprite-Lit-Default` 를 그대로 쓰면 마스크가 모든 스프라이트에 함께 걸린다.
        /// 그래서 마스크가 있는 것만 인스턴스를 만들어 씬에 함께 저장한다(랩 전용 자산).
        ///
        /// 마스크가 없으면 아무것도 하지 않는다 — `_MaskTex` 는 흰색으로 떨어지고, 마스크 라이트가
        /// 닿는 모든 픽셀이 밝아진다(= 마스크 없는 애디티브와 같다).
        /// </summary>
        static void AttachMaskIfAny(SpriteRenderer sr, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return;
            string albedo = AssetDatabase.GetAssetPath(sprite.texture);
            if (string.IsNullOrEmpty(albedo) || !albedo.EndsWith("_albedo.png", StringComparison.Ordinal)) return;

            string maskPath = albedo.Replace("_albedo.png", "_mask.png");
            var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
            if (mask == null) return;

            string matPath = $"{MaterialDir}/LabLit_{Path.GetFileNameWithoutExtension(albedo)}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(sr.sharedMaterial);
                Directory.CreateDirectory(AbsolutePath(MaterialDir));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.SetTexture("_MaskTex", mask);
            EditorUtility.SetDirty(mat);
            sr.sharedMaterial = mat;
        }

        /// <summary>
        /// 발 위치 앵커 + 몸통 스프라이트 + <see cref="LabAnchoredEntity"/>(접촉 그림자·실루엣을
        /// 런타임에 붙인다 — 그 두 클래스는 파일명과 달라 씬에 직렬화하면 missing script 가 된다).
        /// 스프라이트 피벗이 하단 중앙이라 몸통은 로컬 (0,0) 에 두면 발이 앵커에 닿는다.
        /// </summary>
        static VisualHeightAnchor AddAnchored(Transform parent, string name, Sprite sprite, Vector2 groundCell,
                                              string sortingLayer, Vector2 footprintCells, float footprintRadius,
                                              LabAnchoredEntity.Silhouette silhouette = LabAnchoredEntity.Silhouette.None,
                                              Color silhouetteColor = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(groundCell.x, groundCell.y, 0f);

            var body = AddSprite(go.transform, "Body", sprite, go.transform.position, 0);
            body.sortingLayerName = sortingLayer;

            var anchor = go.AddComponent<VisualHeightAnchor>();
            anchor.groundPosition = groundCell;
            anchor.footprintCells = footprintCells;
            anchor.footprintRadius = footprintRadius;
            anchor.sortingLayer = sortingLayer;
            anchor.body = body.transform;

            var entity = go.AddComponent<LabAnchoredEntity>();
            var eso = new SerializedObject(entity);
            eso.FindProperty("_silhouette").enumValueIndex = (int)silhouette;
            if (silhouette != LabAnchoredEntity.Silhouette.None)
                eso.FindProperty("_silhouetteColor").colorValue = silhouetteColor;
            eso.ApplyModifiedPropertiesWithoutUndo();

            return anchor;
        }

        static Light2D AddLight(Transform parent, string name, Light2D.LightType type,
                                Vector3 position, Color color, float intensity, bool local = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (local) go.transform.localPosition = position; else go.transform.position = position;
            var light = go.AddComponent<Light2D>();
            light.lightType = type;
            light.color = color;
            light.intensity = intensity;
            // §7.1 통합 월드 재질 — 바닥·벽·오브젝트가 같은 빛을 받는다. 이 목록은 Default 도
            // 포함하므로 이주 중간 상태(일부가 아직 Default 에 있어도)에서 안전하다.
            light.targetSortingLayers = VisualLayers.LitLayerIds();
            return light;
        }

        /// <summary>
        /// 광원을 §7.3 분류 파이프라인에 등록한다. <paramref name="phase"/> 는 깜빡임 시드 —
        /// 소켓마다 다르게 두어 방의 램프가 한 박자로 숨쉬지 않게 한다.
        ///
        /// <see cref="LightSocket"/> 을 직접 붙이지 않는다 — 파일명과 다른 클래스라 씬에 직렬화하면
        /// missing script 가 된다. <see cref="LabLightSocket"/> 이 Awake 에서 붙인다.
        /// </summary>
        static void AddSocket(Light2D light, LightClass lightClass, float baseIntensity,
                              float rangeCells, float phase)
        {
            var lab = light.gameObject.AddComponent<LabLightSocket>();
            var so = new SerializedObject(lab);
            so.FindProperty("_lightClass").enumValueIndex = (int)lightClass;
            so.FindProperty("_baseIntensity").floatValue = baseIntensity;
            so.FindProperty("_rangeCells").floatValue = rangeCells;
            so.FindProperty("_phase").floatValue = phase;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 정식 네거티브 라이팅(freeform 다각형). 임시 <see cref="LabShadowBlob"/> 을 대체한다 —
        /// 이주 계획 5단계의 "Negative → 정식 Freeform + Multiply + AlphaBlend 승격".
        /// </summary>
        static void AddNegativeVolume(Transform parent, string name, Vector3 position, Vector2[] path)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var light = go.AddComponent<Light2D>();
            var vol = go.AddComponent<NegativeLightVolume>();
            var so = new SerializedObject(vol);
            var arr = so.FindProperty("_path");
            arr.arraySize = path.Length;
            for (int i = 0; i < path.Length; i++)
                arr.GetArrayElementAtIndex(i).vector2Value = path[i];
            so.FindProperty("_strength").floatValue = 0.45f;
            so.FindProperty("_falloff").floatValue = 0.9f;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 씬에 직렬화된 상태도 런타임과 같게 맞춘다 — 에디터에서 열었을 때도 보인다.
            NegativeLightVolume.Configure(light, path, 0.9f);
            light.intensity = 0.45f;
        }

        static void AddNegative(Transform parent, string name, Vector3 position, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var light = go.AddComponent<Light2D>();
            light.targetSortingLayers = VisualLayers.LitLayerIds();

            var blob = go.AddComponent<LabShadowBlob>();
            var so = new SerializedObject(blob);
            so.FindProperty("_kind").enumValueIndex = (int)LabShadowBlob.Kind.Negative;
            so.FindProperty("_size").vector2Value = size;
            so.FindProperty("_weight").floatValue = 1f;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 씬에 직렬화된 상태도 런타임과 같게 맞춘다 — 에디터에서 씬을 열었을 때도 보인다.
            LabShadowBlob.Configure(light, LabShadowBlob.Kind.Negative, size);
            light.intensity = 0.45f;
        }

        static string AbsolutePath(string assetPath) =>
            Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, assetPath);
    }
}
