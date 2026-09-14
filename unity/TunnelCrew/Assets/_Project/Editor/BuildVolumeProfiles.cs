using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TunnelCrew.EditorTools
{
    /// <summary>
    /// 지층별 Volume 프로파일을 만든다.
    ///
    /// 원본의 LX 4레이어 합성(analysis-01 §4.2)을 대체한다.
    ///   contrast 레이어 → Color Adjustments + Tonemapping
    ///   core(핫코어) 레이어 → Bloom
    ///   zone(구역 틴트) 레이어 → 프로파일 자체를 지층마다 교체
    ///   Bayer 디더 → Film Grain
    ///
    ///   Unity.exe -batchmode -quit -projectPath . \
    ///     -executeMethod TunnelCrew.EditorTools.BuildVolumeProfiles.Run
    /// </summary>
    public static class BuildVolumeProfiles
    {
        const string OutDir = "Assets/_Project/Data/Resources";

        /// <summary>지층 프리셋. 원본 INF_PLANET 의 지층 3 + 이상지대에 대응한다.</summary>
        struct Preset
        {
            public string name;
            public Color filter;        // 구역 틴트
            public float exposure;
            public float contrast;
            public float saturation;
            public float bloomIntensity;
            public float bloomThreshold;
            public float vignette;
            public float grain;
        }

        static readonly Preset[] Presets =
        {
            new Preset
            {
                name = "Stratum1_Surface",
                filter = new Color(1.00f, 0.97f, 1.00f),
                exposure = 1.45f, contrast = 2f, saturation = -5f,
                bloomIntensity = 0.46f, bloomThreshold = 0.78f,
                vignette = 0.10f, grain = 0.040f,
            },
            new Preset
            {
                name = "Stratum2_Fracture",
                filter = new Color(0.98f, 0.94f, 1.00f),
                exposure = 1.30f, contrast = 4f, saturation = -7f,
                bloomIntensity = 0.55f, bloomThreshold = 0.75f,
                vignette = 0.15f, grain = 0.055f,
            },
            new Preset
            {
                name = "Stratum3_Core",
                filter = new Color(1.00f, 0.92f, 0.95f),
                exposure = 1.15f, contrast = 6f, saturation = -10f,
                bloomIntensity = 0.65f, bloomThreshold = 0.72f,
                vignette = 0.20f, grain = 0.070f,
            },
            new Preset
            {
                name = "Abyss",
                filter = new Color(0.92f, 0.90f, 1.00f),
                exposure = 1.00f, contrast = 8f, saturation = -16f,
                bloomIntensity = 0.75f, bloomThreshold = 0.68f,
                vignette = 0.25f, grain = 0.085f,
            },
        };

        [MenuItem("Tunnel Crew/M2 · 지층별 Volume 프로파일 생성")]
        public static void Run()
        {
            Directory.CreateDirectory(OutDir);
            int n = 0;
            foreach (var p in Presets) { Build(p); n++; }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[M2] Volume 프로파일 {n}개 생성 → {OutDir}");
        }

        static void Build(Preset p)
        {
            string path = $"{OutDir}/Volume_{p.name}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            else
            {
                // 다시 만들 때는 기존 오버라이드를 비운다.
                // 에셋의 하위 객체이므로 파일에서도 지워야 한다.
                foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (sub is VolumeComponent) Object.DestroyImmediate(sub, true);
                profile.components.Clear();
            }

            // 대비·채도·구역 틴트 — 원본 LX contrast / zone 레이어
            var color = profile.Add<ColorAdjustments>(true);
            color.contrast.overrideState = true; color.contrast.value = p.contrast;
            color.saturation.overrideState = true; color.saturation.value = p.saturation;
            color.colorFilter.overrideState = true; color.colorFilter.value = p.filter;
            color.postExposure.overrideState = true; color.postExposure.value = p.exposure;

            // 밝은 부분을 눌러 어둠과의 대비를 만든다
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.overrideState = true; tone.mode.value = TonemappingMode.Neutral;   // ACES 는 어두운 구간을 너무 눌러 동굴이 뭉갠다

            // 광원 중심 과노출 — 원본 LX core 레이어
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.overrideState = true; bloom.intensity.value = p.bloomIntensity;
            bloom.threshold.overrideState = true; bloom.threshold.value = p.bloomThreshold;
            bloom.scatter.overrideState = true; bloom.scatter.value = 0.72f;
            bloom.tint.overrideState = true; bloom.tint.value = new Color(1f, 0.92f, 0.82f);

            // 화면 가장자리를 눌러 땅속의 답답함을 만든다
            var vig = profile.Add<Vignette>(true);
            vig.intensity.overrideState = true; vig.intensity.value = p.vignette;
            vig.smoothness.overrideState = true; vig.smoothness.value = 0.55f;
            vig.color.overrideState = true; vig.color.value = new Color(0.02f, 0.01f, 0.04f);

            // 원본의 Bayer 4x4 디더가 만들던 거친 질감 — 이제 게임의 유일한 그레인이다
            // (2026-09-09). 대기 셰이더의 해시 그레인과 이중으로 걸려 4K 에서 화면이 노이즈로
            // 뭉갰다. 대기 쪽을 0 으로 내리고 이쪽 세기도 절반 아래로 낮췄다.
            var grain = profile.Add<FilmGrain>(true);
            grain.type.overrideState = true; grain.type.value = FilmGrainLookup.Medium1;
            grain.intensity.overrideState = true; grain.intensity.value = p.grain;
            grain.response.overrideState = true; grain.response.value = 0.8f;

            // VolumeProfile.Add<T>() 는 컴포넌트를 만들어 리스트에 넣기만 한다.
            // 에셋의 하위 객체로 등록하지 않으면 저장되지 않고 다음 로드에서 0개가 된다.
            foreach (var c in profile.components)
            {
                c.hideFlags = HideFlags.HideInHierarchy;
                if (!AssetDatabase.Contains(c)) AssetDatabase.AddObjectToAsset(c, profile);
            }

            EditorUtility.SetDirty(profile);
        }
    }
}
