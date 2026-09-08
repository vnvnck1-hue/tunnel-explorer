using System.IO;
using UnityEditor;
using UnityEngine;

namespace TunnelCrew.EditorTools.ArtPipeline
{
    /// <summary>
    /// 승인 아트 패키지(<c>art-production/test-room-v01</c>)가 디스크에서 어디인지 찾는다.
    ///
    /// <b>왜 자동 탐색만으로는 안 되는가</b> — 이 프로젝트는 유니티 MCP 인식 문제(한글 경로
    /// cp949 버그) 때문에 정션 <c>C:\Users\Loadcomplete\TunnelCrew</c> 로 열도록 돼 있다.
    /// 그때 <c>Application.dataPath</c> 와 현재 작업 폴더는 모두 정션 경로가 되고,
    /// 위로 올라가도 저장소 루트에 닿지 않는다(<c>C:\Users\Loadcomplete</c> 에는 저장소가 없다).
    /// .NET Standard 2.1 에는 재파스 포인트를 따라가는 API(<c>ResolveLinkTarget</c>)가 없다.
    ///
    /// 그래서 순서를 이렇게 둔다.
    /// <list type="number">
    /// <item>커맨드라인 <c>-artPackageRoot &lt;경로&gt;</c> — 배치 모드·CI 용</item>
    /// <item>EditorPrefs 에 저장된 경로 — 정션으로 여는 개발 환경의 1회 설정</item>
    /// <item><c>Application.dataPath</c> 에서 위로 탐색 — 실제 경로로 열었을 때</item>
    /// <item>현재 작업 폴더에서 위로 탐색</item>
    /// </list>
    ///
    /// 어느 것도 찾지 못하면 기대 위치를 돌려주고, 검사기가 "manifest 없음" 으로 보고한다.
    /// 조용히 통과하지 않는 것이 중요하다.
    /// </summary>
    public static class ArtPackageLocator
    {
        const string PrefKey = "tc.visual.artPackageRoot";
        const string Marker = "metadata/manifest.json";

        /// <summary>EditorPrefs 에 저장된 경로. 비어 있으면 자동 탐색만 쓴다.</summary>
        public static string ConfiguredRoot
        {
            get => EditorPrefs.GetString(PrefKey, string.Empty);
            set => EditorPrefs.SetString(PrefKey, value ?? string.Empty);
        }

        /// <summary>패키지 경로를 찾는다. 어디서 찾았는지도 함께 알려 준다.</summary>
        public static string Resolve(out string source)
        {
            string fromArg = ArgValue("-artPackageRoot");
            if (Looks(fromArg)) { source = "커맨드라인 -artPackageRoot"; return fromArg; }

            string configured = ConfiguredRoot;
            if (Looks(configured)) { source = "EditorPrefs 설정"; return configured; }

            string found = SearchUp(Application.dataPath);
            if (found != null) { source = "Application.dataPath 상위 탐색"; return found; }

            found = SearchUp(Directory.GetCurrentDirectory());
            if (found != null) { source = "작업 폴더 상위 탐색"; return found; }

            source = "찾지 못함 (기대 위치)";
            var assets = new DirectoryInfo(Application.dataPath);
            string guessRoot = assets.Parent?.Parent?.Parent?.FullName ?? assets.FullName;
            return Path.Combine(guessRoot, ApprovedArtValidator.DefaultPackageRoot);
        }

        public static string Resolve() => Resolve(out _);

        /// <summary>manifest 가 실제로 있는 폴더인가.</summary>
        public static bool Looks(string root)
            => !string.IsNullOrEmpty(root) && File.Exists(Path.Combine(root, Marker));

        static string SearchUp(string start)
        {
            var dir = new DirectoryInfo(start);
            for (int up = 0; up < 8 && dir != null; up++, dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, ApprovedArtValidator.DefaultPackageRoot);
                if (Looks(candidate)) return candidate;
            }
            return null;
        }

        static string ArgValue(string flag)
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, flag);
            if (i < 0 || i + 1 >= args.Length) return null;
            return args[i + 1].StartsWith("-") ? null : args[i + 1];
        }

        // ───────────────────────────── 메뉴

        [MenuItem("Tunnel Crew/비주얼 · 승인 패키지 경로 지정…", priority = 39)]
        public static void PickFolder()
        {
            string start = Looks(ConfiguredRoot) ? ConfiguredRoot : Application.dataPath;
            string picked = EditorUtility.OpenFolderPanel(
                "art-production/test-room-v01 폴더를 고른다", start, string.Empty);
            if (string.IsNullOrEmpty(picked)) return;

            if (!Looks(picked))
            {
                Debug.LogError($"[비주얼] {picked} 에 {Marker} 가 없다 — 패키지 폴더가 아니다.");
                return;
            }

            ConfiguredRoot = picked;
            Debug.Log($"[비주얼] 승인 패키지 경로를 저장했다: {picked}");
        }
    }
}
