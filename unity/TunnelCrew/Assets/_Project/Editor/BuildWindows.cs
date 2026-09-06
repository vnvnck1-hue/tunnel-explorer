using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace TunnelCrew.EditorTools
{
    /// <summary>
    /// Windows 64 빌드 — 제품명 Tunnel Crew, 아이콘 app-icon-dragon, 씬 Boot/Menu/Run. 산출물은 <c>unity/Build/Windows/TunnelCrew.exe</c>.
    /// 검증 체크리스트(계획 M7): 빌드 로그 에러 0 · exe 실행 · 메인 메뉴 → 출격 → 런 1분 · 종료.
    /// </summary>
    public static class BuildWindows
    {
        public const string OutDir = "../Build/Windows";

        [MenuItem("Tunnel Crew/M7 · Windows 빌드")]
        public static void Build() => BuildTo(Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutDir)));

        public static BuildReport BuildTo(string dir)
        {
            Directory.CreateDirectory(dir);
            PlayerSettings.productName = "Tunnel Crew";
            PlayerSettings.companyName = "Tunnel Crew Team";
            PlayerSettings.defaultScreenWidth = 1920; PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.runInBackground = true;
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Data/Icons/app-icon-dragon.png");
            if (icon != null)
            {
                var sizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Application);
                var icons = new Texture2D[sizes.Length];
                for (int i = 0; i < icons.Length; i++) icons[i] = icon;
                PlayerSettings.SetIcons(NamedBuildTarget.Standalone, icons, IconKind.Application);
            }
            var scenes = new[] { "Assets/_Project/Scenes/Boot.unity", "Assets/_Project/Scenes/Menu.unity", "Assets/_Project/Scenes/Run.unity" };
            var opts = new BuildPlayerOptions
            {
                scenes = scenes, locationPathName = Path.Combine(dir, "TunnelCrew.exe"), target = BuildTarget.StandaloneWindows64, options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[Build] {report.summary.result} · {report.summary.totalSize / (1024 * 1024)} MB · errors {report.summary.totalErrors} · {report.summary.totalTime.TotalSeconds:F0}s → {opts.locationPathName}");
            return report;
        }
    }
    public sealed class BuildReport { public UnityEditor.Build.Reporting.BuildSummary summary; public static implicit operator BuildReport(UnityEditor.Build.Reporting.BuildReport r) => new BuildReport { summary = r.summary }; }
}
