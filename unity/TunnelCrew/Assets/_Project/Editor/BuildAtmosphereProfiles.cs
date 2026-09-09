using System.IO;
using TunnelCrew.Presentation.Visual;
using UnityEditor;
using UnityEngine;

namespace TunnelCrew.EditorTools
{
    /// <summary>
    /// 지층별 <see cref="AtmosphereProfile"/> 4종을 만든다 — "지층별 제한 팔레트"(확정 2026-09-09).
    ///
    /// <b>왜 필요한가</b> — 대기 프로파일이 <c>Stratum1</c> 하나뿐이라 층이 깊어져도 안개·틴트·
    /// 비네트가 같았다. Volume 프로파일은 이미 지층 4종(<c>BuildVolumeProfiles</c>)인데 그 짝이
    /// 없던 것이다. 명세서 수용 기준 §15.1 은 "HUD 없이 스크린샷만 보고 지층을 구분할 수 있다" 를
    /// 요구한다.
    ///
    /// <b>축은 색이 아니라 대비와 밀도다</b> — 퍼플 주조색(<c>#9E80C2</c> 앰비언트)은 전 지층 고정이
    /// 계약이다(이주 계획 §4 "퍼플 톤은 축이 아니다"). 그래서 여기서 움직이는 것은
    /// 안개 밀도 · 근/원경 분리 · 비네트 · 그레인이고, 색은 <b>같은 계열 안에서</b>만 옮긴다.
    /// Rain World·Katana ZERO 가 색 3~4개로 구역을 통일한 전략을 이 축에 얹는다.
    ///
    /// <b>값의 근거</b> — <see cref="BuildVolumeProfiles"/> 의 지층 곡선을 그대로 따라간다
    /// (대비 6→8→10→12 · 채도 6→2→−4→−12 · 비네트 .20→.24→.28→.32 · 그레인 .14→.17→.20→.24).
    /// 두 프로파일이 같은 방향으로 움직여야 화면이 한 몸으로 읽힌다.
    ///
    /// 재실행 가능하다 — 자산이 있으면 값만 덮고 GUID 를 유지하므로 씬·프리셋 참조가 끊기지 않는다.
    /// </summary>
    public static class BuildAtmosphereProfiles
    {
        const string OutDir = "Assets/_Project/Data/Visual";

        struct Spec
        {
            public string Name;
            public Color FogColor, NearTint, FarTint, VignetteColor;
            public float FogDensity, FogHeight, FogSoftness;
            public float DepthSeparation;
            public float VignetteStrength, VignetteInner, VignetteOuter;
            /// <summary>
            /// 그레인은 이제 <b>URP Volume 의 Film Grain 한 곳</b>에서만 나온다 (2026-09-09 정리).
            ///
            /// 전에는 이 대기 셰이더의 해시 그레인과 Film Grain 이 <b>둘 다</b> 걸려 있었다. 랩을
            /// 4K 로 캡처해 보니 화면 전체가 노이즈였다 — density 540~600 이 4K 에서 픽셀보다
            /// 촘촘해져 알베도·조명이 다 묻혔다. Film Grain 은 룩업 텍스처라 해상도에 덜 흔들린다.
            ///
            /// 셰이더 경로는 지우지 않았다 — <c>AtmosphereChannel.GrainOnly</c> 디버그 채널이 쓰고,
            /// 지층별로 세기를 되살릴 여지도 남겨 둔다. 값만 0 이다.
            /// </summary>
            public float GrainStrength, GrainDensity;
        }

        static readonly Spec[] Specs =
        {
            // 지층 1 · 표층 — 현재 승인 상태를 그대로 옮긴다(회귀 방지). 아치 너머 외광이 있는 층.
            new Spec
            {
                Name = "Stratum1",
                FogColor = new Color(0.18f, 0.20f, 0.30f), FogDensity = 0.22f, FogHeight = 0.42f, FogSoftness = 0.45f,
                NearTint = new Color(0.24f, 0.16f, 0.26f), FarTint = new Color(0.14f, 0.20f, 0.30f),
                DepthSeparation = 0.12f,
                VignetteColor = new Color(0.02f, 0.02f, 0.04f), VignetteStrength = 0.35f,
                VignetteInner = 0.55f, VignetteOuter = 1.15f,
                GrainStrength = 0f, GrainDensity = 540f,
            },
            // 지층 2 · 균열대 — 균열에서 스며드는 빛. 안개가 낮게 더 깔리고 근/원경 분리가 커진다.
            new Spec
            {
                Name = "Stratum2",
                FogColor = new Color(0.16f, 0.15f, 0.28f), FogDensity = 0.30f, FogHeight = 0.50f, FogSoftness = 0.42f,
                NearTint = new Color(0.26f, 0.15f, 0.28f), FarTint = new Color(0.12f, 0.17f, 0.31f),
                DepthSeparation = 0.17f,
                VignetteColor = new Color(0.02f, 0.01f, 0.04f), VignetteStrength = 0.42f,
                VignetteInner = 0.50f, VignetteOuter = 1.10f,
                GrainStrength = 0f, GrainDensity = 560f,
            },
            // 지층 3 · core — 위에서 내리누르는 압력. 안개가 두껍고 시야가 좁다. 색이 빠지기 시작한다.
            new Spec
            {
                Name = "Stratum3",
                FogColor = new Color(0.14f, 0.12f, 0.24f), FogDensity = 0.38f, FogHeight = 0.58f, FogSoftness = 0.38f,
                NearTint = new Color(0.27f, 0.14f, 0.27f), FarTint = new Color(0.10f, 0.13f, 0.28f),
                DepthSeparation = 0.22f,
                VignetteColor = new Color(0.01f, 0.01f, 0.03f), VignetteStrength = 0.50f,
                VignetteInner = 0.44f, VignetteOuter = 1.04f,
                GrainStrength = 0f, GrainDensity = 580f,
            },
            // 이상지대 · abyss — 바닥 균열에서 빛이 온다(광원 방향 계약 §5-1). 색이 거의 빠지고
            // 거친 질감이 화면 정체성이 된다(Katana ZERO·SIGNALIS 대역) — 그 질감은 Film Grain 담당.
            new Spec
            {
                Name = "Abyss",
                FogColor = new Color(0.11f, 0.10f, 0.22f), FogDensity = 0.46f, FogHeight = 0.66f, FogSoftness = 0.34f,
                NearTint = new Color(0.28f, 0.13f, 0.29f), FarTint = new Color(0.08f, 0.10f, 0.26f),
                DepthSeparation = 0.28f,
                VignetteColor = new Color(0.01f, 0.00f, 0.02f), VignetteStrength = 0.58f,
                VignetteInner = 0.38f, VignetteOuter = 0.98f,
                GrainStrength = 0f, GrainDensity = 600f,
            },
        };

        [MenuItem("Tunnel Crew/비주얼 · 지층별 대기 프로파일 생성", priority = 26)]
        public static void Run()
        {
            Directory.CreateDirectory(OutDir);
            int made = 0, updated = 0;

            foreach (var spec in Specs)
            {
                string path = $"{OutDir}/AtmosphereProfile_{spec.Name}.asset";
                var p = AssetDatabase.LoadAssetAtPath<AtmosphereProfile>(path);
                if (p == null)
                {
                    p = ScriptableObject.CreateInstance<AtmosphereProfile>();
                    AssetDatabase.CreateAsset(p, path);
                    made++;
                }
                else updated++;

                p.fogColor = spec.FogColor;
                p.fogDensity = spec.FogDensity;
                p.fogHeight = spec.FogHeight;
                p.fogSoftness = spec.FogSoftness;
                p.nearTint = spec.NearTint;
                p.farTint = spec.FarTint;
                p.depthSeparation = spec.DepthSeparation;
                p.vignetteColor = spec.VignetteColor;
                p.vignetteStrength = spec.VignetteStrength;
                p.vignetteInner = spec.VignetteInner;
                p.vignetteOuter = spec.VignetteOuter;
                p.grainStrength = spec.GrainStrength;
                p.grainDensity = spec.GrainDensity;
                p.animateGrain = true;
                EditorUtility.SetDirty(p);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[비주얼] 지층별 대기 프로파일 — 신규 {made} · 갱신 {updated} " +
                      $"(안개 0.22→0.46 · 깊이분리 0.12→0.28 · 비네트 0.35→0.58 · 그레인 0 = Film Grain 단일화)");
        }
    }
}
