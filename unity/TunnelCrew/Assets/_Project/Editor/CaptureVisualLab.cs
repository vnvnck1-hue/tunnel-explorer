using System.Collections.Generic;
using System.IO;
using TunnelCrew.Presentation;
using TunnelCrew.Presentation.Visual;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TunnelCrew.EditorTools
{
    /// <summary>
    /// 기능명세서 §12.3·§16.3 — Visual Lab 을 고정 위치에서 자동 캡처한다.
    ///
    /// 플레이 모드에 들어가지 않고 렌더한다. 그래서 사람이 에디터를 만지지 않아도
    /// "정렬·오클루전·그림자 윤곽·접촉 AO·채널이 실제로 그렇게 그려지는가" 를 파일로
    /// 확인할 수 있다. 픽셀 완전 일치는 요구하지 않고, 누락 레이어·잘못된 정렬·조명 소실을
    /// 보는 용도다.
    ///
    /// 디버그 선은 <see cref="VisualDebugLines"/> 가 씬 지오메트리로 그리므로
    /// <c>Camera.Render</c> 에 그대로 들어온다 — <c>OnGUI</c> 는 에디터 캡처에 남지 않는다.
    ///
    /// <b>파일명</b> — 배치 1 캡처는 <c>visual-lab-*.png</c>, 이 배치는
    /// <c>visual-lab-b2-*.png</c> 다. 이름이 고정이라 다시 돌리면 같은 파일을 덮어쓰고,
    /// 배치별로 나란히 비교할 수 있다.
    ///
    /// 배치 모드:
    ///   Unity.exe -batchmode -quit -projectPath . \
    ///     -executeMethod TunnelCrew.EditorTools.CaptureVisualLab.Run -captureOut &lt;폴더&gt;
    /// </summary>
    public static class CaptureVisualLab
    {
        const string ScenePath = "Assets/_Project/Scenes/VisualLab.unity";

        /// <summary>한 장면의 설정. 캡처 순서가 곧 파일 목록이라 여기서만 바꾼다.</summary>
        struct Shot
        {
            public string Name;
            public Vector2 Dummy;
            public CameraViewMode Camera;
            public ChannelView Channel;
            public VisualLabController.DebugLines Lines;
            public bool Torch;
            public bool FitWholeRoom;
            /// <summary>대기 원근(§7.5)을 켤 것인가. 기본 false — 기존 캡처와 회귀 비교가 가능해야 한다.</summary>
            public bool Atmosphere;
            /// <summary>대기 원근 층 단독 보기. Composite 가 기본이다.</summary>
            public AtmosphereChannel AtmoLayer;
            /// <summary>가려진 캐릭터 실루엣·림(§6.6). 기본 false — 기존 캡처와 회귀 비교가 가능해야 한다.</summary>
            public bool Silhouette;
            /// <summary>품질 단계(§13). 기본 High — 제작 기준이다.</summary>
            public VisualQualityTier Tier;
            /// <summary>광과민 옵션(§10.3).</summary>
            public bool ReducePhotosensitivity;
            /// <summary>Low 단계를 <b>일부러</b> 골랐는가. 기본값 0(Low)과 구분하기 위한 표식이다.</summary>
            public bool LowTierIntended;
            /// <summary>이 장면 <b>전에</b> 이 칸을 부순다. 파괴 후 상태가 이후 장면에 이어진다.</summary>
            public Vector2Int? BreakCell;
        }

        [MenuItem("Tunnel Crew/비주얼 · Visual Lab 캡처 세트", priority = 22)]
        public static void Run()
        {
            string outDir = ArgValue("-captureOut") ?? ResolveDocsDir();
            int w = int.TryParse(ArgValue("-captureW"), out var pw) ? pw : 1920;
            int h = int.TryParse(ArgValue("-captureH"), out var ph) ? ph : 1080;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid()) { Debug.LogError($"[비주얼] {ScenePath} 를 열지 못했다."); return; }

            var lab = Object.FindAnyObjectByType<VisualLabController>();
            if (lab == null) { Debug.LogError("[비주얼] VisualLabController 가 없다."); return; }

            lab.Rebuild();
            lab.EditorTick();

            // 파일명 태그 — 임시 아트 캡처(b2)와 승인 아트 캡처(b3-art)를 나란히 남긴다.
            // 덮어쓰면 "임시와 승인이 어떻게 다른가" 를 비교할 수 없다(§14 단계 B).
            string tag = ArgValue("-captureTag")
                ?? (lab.UsingPlaceholderArt ? "b2" : "b3-art");

            var cam = Camera.main;
            if (cam == null) { Debug.LogError("[비주얼] 카메라가 없다."); return; }

            Directory.CreateDirectory(outDir);

            var field = lab.Field;
            var center = new Vector2(field.Cols * 0.5f, field.Rows * 0.5f);
            var mid = new Vector2(center.x, field.Rows * 0.42f);
            const VisualLabController.DebugLines NoLines = VisualLabController.DebugLines.None;

            var shots = new List<Shot>
            {
                // ── 구조 (배치 1 에서 확인한 것을 새 시스템과 함께 다시 본다)
                new Shot { Name = "room-overview", Dummy = mid, FitWholeRoom = true, Lines = NoLines },

                // ── 그림자 캐스터 윤곽 (§7.4)
                new Shot { Name = "shadow-contours", Dummy = mid, FitWholeRoom = true,
                           Lines = VisualLabController.DebugLines.ShadowContours },
                new Shot { Name = "shadow-torch", Dummy = mid, Torch = true,
                           Camera = CameraViewMode.Coop, Lines = NoLines },
                new Shot { Name = "shadow-torch-contours", Dummy = mid, Torch = true,
                           Camera = CameraViewMode.Coop,
                           Lines = VisualLabController.DebugLines.ShadowContours },

                // ── 접촉 AO 와 발 위치·시각 높이 (§7.4-1·§6.5)
                new Shot { Name = "contact-shadow", Dummy = mid, Torch = true,
                           Camera = CameraViewMode.Combat, Lines = NoLines },
                new Shot { Name = "footpoint-and-height", Dummy = mid, Torch = true,
                           Camera = CameraViewMode.Combat,
                           Lines = VisualLabController.DebugLines.Footpoints },

                // ── 재질 채널 단독 보기 (§7.1·§12.3)
                new Shot { Name = "channel-albedo", Dummy = mid, Channel = ChannelView.Albedo },
                new Shot { Name = "channel-normal", Dummy = mid, Channel = ChannelView.Normal },
                new Shot { Name = "channel-emission", Dummy = mid, Channel = ChannelView.Emission },
                new Shot { Name = "channel-mask", Dummy = mid, Channel = ChannelView.MaterialMask },
                new Shot { Name = "channel-ao", Dummy = mid, Channel = ChannelView.AmbientOcclusion },
                new Shot { Name = "channel-lighting-only", Dummy = mid, Torch = true,
                           Channel = ChannelView.LightingOnly },

                // ── 전경 페이드 그룹 (§6.6)
                new Shot { Name = "behind-foreground", Dummy = new Vector2(center.x, 2.35f),
                           Lines = NoLines },
                new Shot { Name = "foreground-groups", Dummy = new Vector2(center.x, 2.35f),
                           FitWholeRoom = true,
                           Lines = VisualLabController.DebugLines.ForegroundGroups },

                // ── 카메라 프로파일 (§10.2)
                new Shot { Name = "camera-base", Dummy = mid, Camera = CameraViewMode.Base },
                new Shot { Name = "camera-combat", Dummy = mid, Camera = CameraViewMode.Combat },
                new Shot { Name = "camera-coop", Dummy = mid, Camera = CameraViewMode.Coop },

                // ── 가려진 캐릭터 실루엣·림 (§6.6)
                // 더미를 남쪽 벽 뒤로 보낸다. 적 더미는 그보다 더 안쪽에 고정돼 있다.
                new Shot { Name = "silhouette-off", Dummy = new Vector2(center.x, 2.35f),
                           Lines = NoLines },
                new Shot { Name = "silhouette-on", Dummy = new Vector2(center.x, 2.35f),
                           Lines = NoLines, Silhouette = true },
                new Shot { Name = "silhouette-threat", Dummy = mid,
                           Lines = NoLines, Silhouette = true },

                // ── 품질 단계 (§13). 그림자 예산·안개·노멀 세기가 함께 내려간다.
                new Shot { Name = "quality-low", Dummy = mid, Torch = true, FitWholeRoom = true,
                           Tier = VisualQualityTier.Low, LowTierIntended = true },
                new Shot { Name = "quality-ultra", Dummy = mid, Torch = true, FitWholeRoom = true,
                           Tier = VisualQualityTier.Ultra },
                new Shot { Name = "quality-photosensitive", Dummy = mid, Torch = true, FitWholeRoom = true,
                           Tier = VisualQualityTier.High, ReducePhotosensitivity = true,
                           Atmosphere = true },

                // ── 대기 원근 (§7.5). 파괴 장면 전에 찍어야 방이 온전한 상태로 남는다.
                new Shot { Name = "atmosphere-off", Dummy = mid, Torch = true, FitWholeRoom = true },
                new Shot { Name = "atmosphere-on", Dummy = mid, Torch = true, FitWholeRoom = true,
                           Atmosphere = true },
                new Shot { Name = "atmosphere-fog", Dummy = mid, Torch = true, FitWholeRoom = true,
                           Atmosphere = true, AtmoLayer = AtmosphereChannel.FogOnly },
                new Shot { Name = "atmosphere-depth", Dummy = mid, Torch = true, FitWholeRoom = true,
                           Atmosphere = true, AtmoLayer = AtmosphereChannel.DepthOnly },
                new Shot { Name = "atmosphere-vignette", Dummy = mid, Torch = true, FitWholeRoom = true,
                           Atmosphere = true, AtmoLayer = AtmosphereChannel.VignetteOnly },
                new Shot { Name = "atmosphere-grain", Dummy = mid, Torch = true, FitWholeRoom = true,
                           Atmosphere = true, AtmoLayer = AtmosphereChannel.GrainOnly },

                // ── 파괴 전후 윤곽 갱신 (§6.7)
                // 방 정의 기준: 왼쪽 기둥 = 열 5~7 / 행 5~6, 오른쪽 기둥 = 열 9~12 / 행 8~9,
                // 북쪽 벽 = 행 12~13.
                new Shot { Name = "break-before", Dummy = mid, FitWholeRoom = true,
                           Lines = VisualLabController.DebugLines.ShadowContours },
                new Shot { Name = "break-pillar", Dummy = mid, FitWholeRoom = true,
                           Lines = VisualLabController.DebugLines.ShadowContours,
                           BreakCell = new Vector2Int(10, 9) },
                new Shot { Name = "break-doorway", Dummy = mid, FitWholeRoom = true,
                           Lines = VisualLabController.DebugLines.ShadowContours,
                           BreakCell = new Vector2Int(10, 12) },
            };

            var saved = new List<string>();

            foreach (var shot in shots)
            {
                if (shot.BreakCell.HasValue && lab.Field is ArraySolidField arr)
                {
                    var c = shot.BreakCell.Value;
                    arr.SetSolid(c.x, c.y, false);
                    lab.Environment.MarkCellDirty(c.x, c.y);
                    lab.Shadows.MarkDirty();
                }

                VisualChannelDebug.SetView(shot.Channel);
                if (lab.Torch != null) lab.Torch.enabled = shot.Torch;

                lab.Dummy.groundPosition = shot.Dummy;
                lab.ViewMode = shot.Camera;
                lab.ActiveDebugLines = shot.Lines;
                if (lab.Options != null)
                {
                    // Shot 의 기본값 0 은 Low 다. 캡처 기본은 제작 기준(High)이어야 하므로
                    // 명시하지 않은 장면은 High 로 올린다 — 기존 캡처와 비교가 유지된다.
                    lab.Options.Tier = shot.Tier == VisualQualityTier.Low && !shot.LowTierIntended
                        ? VisualQualityTier.High
                        : shot.Tier;
                    lab.Options.ReducePhotosensitivity = shot.ReducePhotosensitivity;
                }
                if (lab.Silhouettes != null)
                {
                    // 상태를 매번 초기화한다 — 보간이 남아 있으면 장면 순서에 결과가 의존한다.
                    lab.Silhouettes.ResetSilhouettes();
                    lab.Silhouettes.Enabled = shot.Silhouette;
                }
                if (lab.Atmosphere != null)
                {
                    lab.Atmosphere.Isolate = shot.AtmoLayer;
                    lab.Atmosphere.Active = shot.Atmosphere;
                }
                lab.EditorTick();

                if (shot.FitWholeRoom)
                {
                    cam.orthographicSize = (field.Rows + 1f) * 0.5f;
                    var p = IsometricProjection.ToRender(center);
                    cam.transform.position = new Vector3(p.x, p.y, -10f);
                }

                string path = Path.Combine(outDir, $"visual-lab-{tag}-{shot.Name}.png");
                Capture(cam, w, h, path);
                saved.Add(Path.GetFileName(path));
            }

            VisualChannelDebug.Reset();
            lab.ActiveDebugLines = VisualLabController.DebugLines.None;

            Debug.Log($"[비주얼] Visual Lab 캡처 {saved.Count}장 · 태그 '{tag}' · 키트 '{lab.KitName}'\n  {outDir}\n  " +
                      string.Join("\n  ", saved));
        }

        /// <summary>
        /// 저장소의 docs 폴더. 정션으로 열린 프로젝트에서는 상대 경로가 저장소를 벗어나므로
        /// 아트 패키지와 같은 방식으로 찾는다(<see cref="ArtPipeline.ArtPackageLocator"/> 참고).
        /// </summary>
        static string ResolveDocsDir()
        {
            const string rel = "docs/unity-port/img/visual-lab";

            var dir = new DirectoryInfo(Application.dataPath);
            for (int up = 0; up < 8 && dir != null; up++, dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, rel);
                if (Directory.Exists(candidate)) return candidate;
            }

            // 아트 패키지 경로가 설정돼 있으면 그 저장소 루트를 쓴다.
            string configured = ArtPipeline.ArtPackageLocator.ConfiguredRoot;
            if (!string.IsNullOrEmpty(configured))
            {
                var repo = new DirectoryInfo(configured).Parent?.Parent;
                if (repo != null) return Path.Combine(repo.FullName, rel);
            }

            return Path.Combine("..", "..", rel);
        }

        static void Capture(Camera cam, int w, int h, string path)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllBytes(path, tex.EncodeToPNG());

            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
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
