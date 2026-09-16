using System.IO;
using UnityEditor;
using UnityEngine;

namespace TunnelCrew.EditorTools
{
    /// <summary>
    /// 원근 월드 스모크 — 플레이 진입 → 관전 출격 → 안정화 대기 → 원근 RenderTexture 저장까지
    /// 한 번에 돌린다. 도메인 리로드를 건너 진행 상태를 <see cref="SessionState"/> 에 둔다.
    ///
    /// 수치를 바꿀 때마다 같은 장면을 같은 순서로 다시 찍기 위한 개발 도구다. 빌드에는 들어가지 않는다.
    /// </summary>
    [InitializeOnLoad]
    public static class PerspectiveSmoke
    {
        const string StepKey = "tc.perspectiveSmoke.step";
        const string FrameKey = "tc.perspectiveSmoke.frames";
        const string PathKey = "tc.perspectiveSmoke.path";
        const string TuningKey = "tc.perspectiveSmoke.tuning";
        const string RoleKey = "tc.perspectiveSmoke.role";

        static PerspectiveSmoke()
        {
            EditorApplication.update -= EnsureDriver;
            EditorApplication.update += EnsureDriver;
        }

        /// <summary>
        /// 에디터 창이 포커스를 잃으면 <see cref="EditorApplication.update"/> 가 거의 돌지 않는다.
        /// 그래서 진행은 플레이 모드 안의 런타임 컴포넌트가 프레임마다 돌린다.
        /// </summary>
        static void EnsureDriver()
        {
            if (SessionState.GetInt(StepKey, 0) == 0 || !EditorApplication.isPlaying) return;
            if (Object.FindFirstObjectByType<SmokeDriver>() != null) return;
            var go = new GameObject("~PerspectiveSmokeDriver") { hideFlags = HideFlags.HideAndDontSave };
            go.AddComponent<SmokeDriver>();
        }

        /// <summary>플레이 모드 안에서 스모크 단계를 진행시키는 드라이버.</summary>
        sealed class SmokeDriver : MonoBehaviour
        {
            void Update() => Tick();

            /// <summary>
            /// 실제 화면(백버퍼)을 그대로 읽는다 — 카메라 렌더 + IMGUI 오버레이 + HUD 가 모두 합성된 결과다.
            /// 프레임 끝을 기다려야 하므로 코루틴으로 돈다.
            /// </summary>
            public System.Collections.IEnumerator CaptureScreen(string path)
            {
                yield return new WaitForEndOfFrame();
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Destroy(tex);
                Debug.Log($"[원근 스모크] 화면 저장: {path}");
            }
        }

        /// <summary>
        /// 스모크를 시작한다. <paramref name="tuning"/> 은 "gain=0.3;wrap=0.5;max=1.8" 형식(선택),
        /// <paramref name="role"/> 은 관전 출격의 리더 직업(0 드릴러 · 1 거너 · 2 스카우트 · 3 엔지니어)이다.
        /// </summary>
        public static void Begin(string outputPath, string tuning = null, int role = 0)
        {
            SessionState.SetString(PathKey, outputPath);
            SessionState.SetString(TuningKey, tuning ?? "");
            SessionState.SetInt(RoleKey, Mathf.Clamp(role, 0, 3));
            SessionState.SetInt(FrameKey, 0);
            SessionState.SetInt(StepKey, 1);
            if (!EditorApplication.isPlaying) EditorApplication.EnterPlaymode();
        }

        public static string Status()
        {
            return "step=" + SessionState.GetInt(StepKey, 0)
                + " frames=" + SessionState.GetInt(FrameKey, 0)
                + " path=" + SessionState.GetString(PathKey, "");
        }

        static void Tick()
        {
            int step = SessionState.GetInt(StepKey, 0);
            if (step == 0) return;
            if (!EditorApplication.isPlaying) return;

            int frames = SessionState.GetInt(FrameKey, 0) + 1;
            SessionState.SetInt(FrameKey, frames);

            if (step == 1)
            {
                if (frames < 90) return;
                var screens = Object.FindFirstObjectByType<TunnelCrew.Presentation.MetaScreens>();
                if (screens == null) return;
                screens.LaunchObserver((TunnelCrew.Sim.RoleId)SessionState.GetInt(RoleKey, 0));
                ApplyTuning();
                SessionState.SetInt(FrameKey, 0);
                SessionState.SetInt(StepKey, 2);
                return;
            }

            if (step == 2)
            {
                if (frames < 240) return;
                ApplyTuning();   // 런 시작 뒤 새로 만들어진 버퍼에도 적용한다
                ApplyScenario();  // 지층 하강·보스 소환 같은 촬영 조건
                SessionState.SetInt(FrameKey, 0);
                SessionState.SetInt(StepKey, 3);
                return;
            }

            if (step == 3)
            {
                if (frames < 300) return;
                string path = SessionState.GetString(PathKey, "");
                Capture(path);

                // 같은 런·같은 위치에서 2D 본선도 찍는다. 이 둘을 나란히 놓아야
                // "현재 비주얼이 자연스럽게 이식되었는가" 를 눈으로 확인할 수 있다.
                var view = Object.FindFirstObjectByType<TunnelCrew.Presentation.Visual.PerspectiveWorldView>();
                if (view != null) view.SetMainlineMode(false);
                SessionState.SetInt(FrameKey, 0);
                SessionState.SetInt(StepKey, 4);
                return;
            }

            if (step == 4)
            {
                if (frames < 30) return;
                var rig = Object.FindFirstObjectByType<TunnelCrew.Presentation.CameraRig>();
                var cam = rig != null ? rig.GetComponent<Camera>() : Camera.main;
                TunnelCrew.Presentation.FrameCapture.Request(
                    cam, TwoDPath(SessionState.GetString(PathKey, "")), 480, 270);
                SessionState.SetInt(FrameKey, 0);
                SessionState.SetInt(StepKey, 5);
                return;
            }

            if (step == 5)
            {
                if (TunnelCrew.Presentation.FrameCapture.Busy && frames < 120) return;
                var view = Object.FindFirstObjectByType<TunnelCrew.Presentation.Visual.PerspectiveWorldView>();
                if (view != null) view.SetMainlineMode(true);
                Debug.Log("[원근 스모크] 2D 대조본 저장: " + TunnelCrew.Presentation.FrameCapture.LastSavedPath);
                SessionState.SetInt(StepKey, 0);
            }
        }

        static string TwoDPath(string path)
        {
            string dir = Path.GetDirectoryName(path);
            return Path.Combine(dir, Path.GetFileNameWithoutExtension(path) + "-2d.png");
        }

        static string ScreenPath(string path)
        {
            string dir = Path.GetDirectoryName(path);
            return Path.Combine(dir, Path.GetFileNameWithoutExtension(path) + "-screen.png");
        }

        /// <summary>
        /// 촬영 조건을 만든다(§4 8단계 QA). 튜닝 문자열의 <c>deep=N</c> 은 지층을 N 번 내려가고,
        /// <c>boss=1</c> 은 보스를 즉시 소환한다. 시뮬레이션의 공개 API 만 쓴다 — 규칙을 바꾸지 않는다.
        /// </summary>
        static void ApplyScenario()
        {
            string tuning = SessionState.GetString(TuningKey, "");
            if (string.IsNullOrEmpty(tuning)) return;
            var run = Object.FindFirstObjectByType<TunnelCrew.Presentation.RunBootstrap>();
            if (run == null || run.Sim == null) return;

            foreach (var entry in tuning.Split(';'))
            {
                var kv = entry.Split('=');
                if (kv.Length != 2 || !float.TryParse(kv[1], out float value)) continue;
                if (kv[0].Trim() == "deep")
                {
                    for (int i = 0; i < Mathf.RoundToInt(value); i++) run.DoDescend();
                    Debug.Log($"[원근 스모크] 지층 {run.Sim.Depth} 진입");
                }
                else if (kv[0].Trim() == "boss" && value > 0.5f)
                {
                    run.Sim.Bosses.Spawn(run.Sim.Player, run.Sim.Depth);
                    Debug.Log("[원근 스모크] 보스 소환");
                }
            }
        }

        static void ApplyTuning()
        {
            string tuning = SessionState.GetString(TuningKey, "");
            if (string.IsNullOrEmpty(tuning)) return;
            var buffer = Object.FindFirstObjectByType<TunnelCrew.Presentation.Visual.PerspectiveLightBuffer>();
            var view = Object.FindFirstObjectByType<TunnelCrew.Presentation.Visual.PerspectiveWorldView>();
            if (buffer == null) return;

            foreach (var entry in tuning.Split(';'))
            {
                var kv = entry.Split('=');
                if (kv.Length != 2 || !float.TryParse(kv[1], out float value)) continue;
                switch (kv[0].Trim())
                {
                    case "gain": buffer.intensityGain = value; break;
                    case "wrap": buffer.lambertWrap = value; break;
                    case "max": buffer.maxLight = value; break;
                    case "top": buffer.topAmbientScale = value; break;
                    case "hand": buffer.handheldHeight = value; break;
                    case "lamp": buffer.lampHeight = value; break;
                    case "pull": if (view != null) view.FramingPullback = value; break;
                    case "amb": buffer.ambientGain = value; break;
                }
            }
        }

        static void Capture(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            var preview = Object.FindFirstObjectByType<TunnelCrew.Presentation.Visual.PerspectiveWorldView>();
            if (preview == null) return;

            var field = typeof(TunnelCrew.Presentation.Visual.PerspectiveWorldView)
                .GetField("_target", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field == null || !(field.GetValue(preview) is RenderTexture rt)) return;

            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log($"[원근 스모크] 저장: {path}");
        }
    }
}
