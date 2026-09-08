using System.IO;
using TunnelCrew.Presentation.Visual;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TunnelCrew.EditorTools
{
    /// <summary>
    /// 기능명세서 §12.3 Visual Lab 씬과 §6.2·§12.1 데이터 자산을 만든다.
    ///
    /// 소팅 레이어 → 임시 아트·채널·재질 묶음 → 프로파일·규칙 → 씬 순서로 한 번에 세운다.
    /// 이미 있는 자산은 값을 덮어쓰지 않고 그대로 둔다 — 수동으로 조정한 수치가
    /// 도구를 다시 돌렸다고 되돌아가면 안 된다.
    ///
    /// 배치 모드:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod TunnelCrew.EditorTools.BuildVisualLab.Run
    /// </summary>
    public static class BuildVisualLab
    {
        const string DataDir = "Assets/_Project/Data/Visual";
        const string SceneDir = "Assets/_Project/Scenes";
        const string ScenePath = SceneDir + "/VisualLab.unity";

        [MenuItem("Tunnel Crew/비주얼 · Visual Lab 씬 생성 (§12.3)", priority = 21)]
        public static void Run()
        {
            BuildVisualLayers.Run();
            var art = BuildVisualPlaceholderArt.Run();

            Directory.CreateDirectory(DataDir);
            var profile = Ensure<WorldVisualProfile>($"{DataDir}/WorldVisualProfile_Stratum1.asset");
            var rules = Ensure<SurfaceRuleSet>($"{DataDir}/SurfaceRuleSet_Stratum1.asset");
            var atmo = Ensure<AtmosphereProfile>($"{DataDir}/AtmosphereProfile_Stratum1.asset");
            AssetDatabase.SaveAssets();

            Directory.CreateDirectory(SceneDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var go = new GameObject("Visual Lab");
            var lab = go.AddComponent<VisualLabController>();
            lab.EditorAssign(profile, rules, art.Kit, art.Floor, art.WallTop, art.WallFront,
                setPieces: null, atmosphere: atmo);
            lab.EditorResetSetPieceSockets();
            EditorUtility.SetDirty(lab);

            var overlayGo = new GameObject("Depth Debug Overlay");
            var overlay = overlayGo.AddComponent<DepthDebugOverlay>();
            overlay.EditorAssign(lab);
            EditorUtility.SetDirty(overlay);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            // 빌드 설정에는 넣지 않는다 — 검증 전용 씬이다.
            Debug.Log($"[비주얼] Visual Lab 준비 완료 · {ScenePath}\n" +
                "이동 WASD · 파괴 X · 복구 C · 전경 페이드 F · 접촉 AO G · 탐색광 L\n" +
                "채널 단독 보기 Z · 카메라 프로파일 V · 디버그 오버레이 F1/F2");
        }

        /// <summary>
        /// 열려 있는 Visual Lab 을 승인 아트 키트로 전환한다.
        ///
        /// 임시 아트 씬을 갈아엎지 않고 참조만 바꾼다 — 두 상태를 나란히 캡처해
        /// "임시 아트에서 세운 구조가 승인 아트에서도 그대로 읽히는가" 를 봐야 한다(§14 단계 B).
        /// </summary>
        [MenuItem("Tunnel Crew/비주얼 · Visual Lab 을 승인 아트로 전환", priority = 23)]
        public static void SwitchToApprovedArt()
        {
            var lab = Object.FindAnyObjectByType<VisualLabController>();
            if (lab == null)
            {
                Debug.LogError("[비주얼] 열려 있는 씬에 VisualLabController 가 없다. " +
                               "먼저 'Visual Lab 씬 생성' 을 실행할 것.");
                return;
            }

            var kit = AssetDatabase.LoadAssetAtPath<EnvironmentKit>(
                $"{DataDir}/EnvironmentKit_TestRoomV01.asset");
            if (kit == null || kit.IsEmpty)
            {
                Debug.LogError("[비주얼] 승인 아트 키트가 없거나 비어 있다. " +
                               "'승인 아트 임포트 → EnvironmentKit' 을 먼저 실행할 것.");
                return;
            }

            var profile = Ensure<WorldVisualProfile>($"{DataDir}/WorldVisualProfile_Stratum1.asset");
            var rules = Ensure<SurfaceRuleSet>($"{DataDir}/SurfaceRuleSet_Stratum1.asset");
            var atmo = Ensure<AtmosphereProfile>($"{DataDir}/AtmosphereProfile_Stratum1.asset");

            // 채널 아틀라스로 만든 재질 묶음을 연결한다(구현 기록 §11.4).
            // 없으면 경고만 남기고 Albedo 만으로 조립한다 — 그 상태는 최소광이 걸리지 않아
            // 어둡게 보이는 것이 정상이다.
            var floorSet = AssetDatabase.LoadAssetAtPath<SurfaceMaterialSet>(
                $"{DataDir}/SurfaceMaterialSet_TestRoom_floor.asset");
            var capSet = AssetDatabase.LoadAssetAtPath<SurfaceMaterialSet>(
                $"{DataDir}/SurfaceMaterialSet_TestRoom_walltop.asset");
            var frontSet = AssetDatabase.LoadAssetAtPath<SurfaceMaterialSet>(
                $"{DataDir}/SurfaceMaterialSet_TestRoom_wallfront.asset");

            if (floorSet == null || capSet == null || frontSet == null)
                Debug.LogWarning("[비주얼] 채널 아틀라스 재질 묶음이 없다 — " +
                                 "'승인 아트 임포트 → EnvironmentKit' 을 다시 실행할 것. " +
                                 "지금은 Albedo 만으로 조립한다(최소광 미적용 → 어둡게 보인다).");

            var setPieces = AssetDatabase.LoadAssetAtPath<SetPieceCatalog>(
                $"{DataDir}/SetPieceCatalog_TestRoomV01.asset");
            if (setPieces == null || setPieces.Count == 0)
                Debug.LogWarning("[비주얼] SetPieceCatalog 가 없거나 비어 있다 — 세트피스 없이 조립한다.");

            lab.EditorAssign(profile, rules, kit, floorSet, capSet, frontSet, setPieces, atmo);
            // 씬에 직렬화된 옛 배치가 코드의 새 배치를 덮어쓰지 않게 재동기화한다.
            lab.EditorResetSetPieceSockets();
            EditorUtility.SetDirty(lab);
            EditorSceneManager.MarkSceneDirty(lab.gameObject.scene);
            EditorSceneManager.SaveScene(lab.gameObject.scene);

            lab.Rebuild();
            lab.EditorTick();

            Debug.Log($"[비주얼] Visual Lab 을 승인 아트로 전환했다 — 키트 '{kit.name}'\n" +
                      $"  채널 재질: floor={(floorSet != null ? floorSet.name : "-")} " +
                      $"walltop={(capSet != null ? capSet.name : "-")} " +
                      $"wallfront={(frontSet != null ? frontSet.name : "-")}\n" +
                      $"  세트피스 {(setPieces != null ? setPieces.Count : 0)}종");
        }

        [MenuItem("Tunnel Crew/비주얼 · Visual Lab 을 임시 아트로 되돌리기", priority = 24)]
        public static void SwitchToPlaceholderArt()
        {
            var lab = Object.FindAnyObjectByType<VisualLabController>();
            if (lab == null) { Debug.LogError("[비주얼] VisualLabController 가 없다."); return; }

            var art = BuildVisualPlaceholderArt.Run();
            var profile = Ensure<WorldVisualProfile>($"{DataDir}/WorldVisualProfile_Stratum1.asset");
            var rules = Ensure<SurfaceRuleSet>($"{DataDir}/SurfaceRuleSet_Stratum1.asset");
            var atmo = Ensure<AtmosphereProfile>($"{DataDir}/AtmosphereProfile_Stratum1.asset");

            lab.EditorAssign(profile, rules, art.Kit, art.Floor, art.WallTop, art.WallFront,
                setPieces: null, atmosphere: atmo);
            lab.EditorResetSetPieceSockets();
            EditorUtility.SetDirty(lab);
            EditorSceneManager.MarkSceneDirty(lab.gameObject.scene);
            EditorSceneManager.SaveScene(lab.gameObject.scene);

            lab.Rebuild();
            lab.EditorTick();
            Debug.Log("[비주얼] Visual Lab 을 임시 아트로 되돌렸다.");
        }

        static T Ensure<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }
    }
}
