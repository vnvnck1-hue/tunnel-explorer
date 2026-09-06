using System.Collections.Generic;
using System.IO;
using TunnelCrew.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TunnelCrew.EditorTools
{
    /// <summary>
    /// M0 골격: Boot / Menu / Run 세 씬을 만들고 빌드 설정에 등록한다.
    /// 배치 모드에서 실행할 수 있다:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod TunnelCrew.EditorTools.CreateCoreScenes.Run
    /// </summary>
    public static class CreateCoreScenes
    {
        const string Dir = "Assets/_Project/Scenes";

        [MenuItem("Tunnel Crew/M0 · 코어 씬 생성")]
        public static void Run()
        {
            Directory.CreateDirectory(Dir);
            var paths = new List<string>
            {
                CreateBoot(),
                CreateMenu(),
                CreateRun(),
            };

            EditorBuildSettings.scenes = paths
                .ConvertAll(p => new EditorBuildSettingsScene(p, true))
                .ToArray();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[M0] 코어 씬 {paths.Count}개 생성 및 빌드 설정 등록 완료: {string.Join(", ", paths)}");
        }

        /// <summary>Boot — GameFlow 만 들고 있는 진입 씬. 여기서 Menu 로 넘어간다.</summary>
        static string CreateBoot()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("GameFlow");
            go.AddComponent<GameFlow>();
            return Save(scene, "Boot");
        }

        /// <summary>Menu — 메인 메뉴·행성 지도·직업 선택 UI 가 들어갈 씬.</summary>
        static string CreateMenu()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera("UI Camera", orthographicSize: 5f);
            new GameObject("UI Root");
            return Save(scene, "Menu");
        }

        /// <summary>Run — 인게임 씬. 월드·조명·HUD 가 들어간다.</summary>
        static string CreateRun()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1셀 = 1유닛(PPU 50). 원본 카메라의 세로 42% 앵커·동적 줌은 M1 에서
            // Cinemachine 으로 붙인다. 여기서는 자리만 잡는다.
            CreateCamera("Main Camera", orthographicSize: 9f, isMain: true);

            new GameObject("World");      // Tilemap 2~3장이 들어갈 자리 (M1)
            new GameObject("Actors");     // 플레이어·AI 크루·적 (M1/M3)
            new GameObject("Lighting");   // Global Light 2D · 손전등 · 랜턴 (M2)
            new GameObject("VFX");        // 파티클 풀 (M3)
            new GameObject("HUD");        // UGUI 캔버스 (M4)

            return Save(scene, "Run");
        }

        static Camera CreateCamera(string name, float orthographicSize, bool isMain = false)
        {
            var go = new GameObject(name);
            if (isMain) go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = orthographicSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.transform.position = new Vector3(0f, 0f, -10f);

            // URP 2D: 카메라에 Universal Additional Camera Data 가 필요하다.
            go.AddComponent<UniversalAdditionalCameraData>();
            return cam;
        }

        static string Save(Scene scene, string name)
        {
            string path = $"{Dir}/{name}.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }
    }
}
