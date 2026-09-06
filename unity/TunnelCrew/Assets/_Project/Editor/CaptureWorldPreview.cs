using System.IO;
using System.Linq;
using TunnelCrew.Data;
using TunnelCrew.Presentation;
using TunnelCrew.Sim;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace TunnelCrew.EditorTools
{
    /// <summary>
    /// 배치 모드에서 월드를 만들어 카메라로 한 장 찍는다. 사람이 에디터를 열지 않아도
    /// "실제로 그려지는가" 를 확인할 수 있게 하는 용도다.
    ///
    ///   Unity.exe -batchmode -quit -projectPath . \
    ///     -executeMethod TunnelCrew.EditorTools.CaptureWorldPreview.Run -captureOut &lt;경로&gt;
    /// </summary>
    public static class CaptureWorldPreview
    {
        [MenuItem("Tunnel Crew/M1 · 월드 미리보기 캡처")]
        public static void Run()
        {
            string outPath = ArgValue("-captureOut") ?? "world-preview.png";
            int width = int.TryParse(ArgValue("-captureW"), out var w) ? w : 1600;
            int height = int.TryParse(ArgValue("-captureH"), out var h) ? h : 900;
            int depth = int.TryParse(ArgValue("-captureDepth"), out var d) ? d : 1;
            bool wholeMap = ArgValue("-captureWhole") != null;

            var tileSet = AssetDatabase.LoadAssetAtPath<TileSetAsset>(
                "Assets/_Project/Data/Resources/TileSet_purple.asset");
            if (tileSet == null) { Debug.LogError("[M1] TileSet 이 없다."); return; }

            var sim = new TunnelSim();
            sim.EnterDepth(depth, DungeonConfig.Runtime);

            var root = new GameObject("PreviewRoot");
            try
            {
                var gridGo = new GameObject("Grid");
                gridGo.transform.SetParent(root.transform);
                var grid = gridGo.AddComponent<Grid>();
                grid.cellSize = new Vector3(1, 1, 0);

                Tilemap Layer(string n, int order)
                {
                    var go = new GameObject(n);
                    go.transform.SetParent(gridGo.transform, false);
                    var tm = go.AddComponent<Tilemap>();
                    go.AddComponent<TilemapRenderer>().sortingOrder = order;
                    return tm;
                }
                var floor = Layer("Floor", 0);
                var walls = Layer("Walls", 10);
                var coreTop = Layer("CoreTop", 20);

                var wr = new GameObject("WorldRenderer");
                wr.transform.SetParent(root.transform);
                var renderer = wr.AddComponent<WorldRenderer>();
                renderer.EditorAssign(tileSet, floor, walls, coreTop);
                renderer.Bind(sim.World);

                // 플레이어 자리 표시 (M1 그레이박스: 흰 점)
                var marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
                marker.name = "PlayerMarker";
                marker.transform.SetParent(root.transform);
                marker.transform.position = new Vector3(
                    (float)sim.Player.Position.X, (float)sim.Player.Position.Y, -0.1f);
                marker.transform.localScale = Vector3.one * 1.0f;
                var mr = marker.GetComponent<MeshRenderer>();
                mr.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = Color.white };

                var lightGo = new GameObject("Global Light 2D");
                lightGo.transform.SetParent(root.transform);
                var l2d = lightGo.AddComponent<Light2D>();
                l2d.lightType = Light2D.LightType.Global;
                l2d.intensity = 1f;

                var camGo = new GameObject("PreviewCamera");
                camGo.transform.SetParent(root.transform);
                var cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<UniversalAdditionalCameraData>();
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.04f, 0.03f, 0.07f);

                if (wholeMap)
                {
                    cam.orthographicSize = sim.World.Rows * 0.5f;
                    cam.transform.position = new Vector3(sim.World.Cols * 0.5f, sim.World.Rows * 0.5f, -10f);
                }
                else
                {
                    // 원본 기본 줌과 같은 세로 셀 수
                    float cells = (float)(1080.0 / (SimTuning.BaseZoom * SimTuning.PxPerCell) / SimTuning.ZoomInMul);
                    cam.orthographicSize = cells * 0.5f;
                    cam.transform.position = new Vector3(
                        (float)sim.Player.Position.X, (float)sim.Player.Position.Y, -10f);
                }

                var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;

                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
                File.WriteAllBytes(outPath, tex.EncodeToPNG());

                int solid = 0, empty = 0;
                for (int i = 0; i < sim.World.CellCount; i++)
                    if (sim.World.AtIndex(i) == TileType.Empty) empty++; else solid++;

                Debug.Log($"[M1] 미리보기 저장: {Path.GetFullPath(outPath)} ({width}x{height}) " +
                          $"— 심층 {depth}, 벽 {solid} / 빈칸 {empty}, " +
                          $"진입 ({sim.World.EntryCol},{sim.World.EntryRow})");

                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static string ArgValue(string flag)
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, flag);
            if (i < 0) return null;
            if (i + 1 >= args.Length) return "";
            return args[i + 1].StartsWith("-") ? "" : args[i + 1];
        }
    }
}
