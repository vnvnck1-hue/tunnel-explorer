using System.IO;
using UnityEngine;

namespace TunnelCrew.Presentation
{
    /// <summary>
    /// 게임 화면을 파일로 저장한다. **포스트프로세싱이 적용된 최종 화면**을 얻기 위한 것이다.
    ///
    /// 왜 이게 필요한가: URP 에서 <c>Camera.Render()</c> 를 직접 부르면 포스트프로세싱 결과가
    /// 비어 나온다(측정값 — 포스트 끔 밝기합 1,046만 / 켬 222). 정상 렌더 루프를 한 프레임
    /// 거쳐야 한다. 그래서 이 컴포넌트는 targetTexture 를 걸어 두고 **다음 프레임에** 읽는다.
    ///
    /// 개발 중 화면 확인용이다. 빌드에는 들어가지 않아도 된다.
    /// </summary>
    public sealed class FrameCapture : MonoBehaviour
    {
        Camera _camera;
        RenderTexture _rt;
        string _path;
        int _framesLeft = -1;

        /// <summary>캡처가 끝나면 이 값이 파일 경로가 된다. 실패하면 빈 문자열.</summary>
        public static string LastSavedPath { get; private set; } = "";
        public static bool Busy { get; private set; }

        /// <summary>
        /// 캡처를 요청한다. 실제 저장은 1~2 프레임 뒤다.
        /// </summary>
        public static FrameCapture Request(Camera cam, string absolutePath, int width, int height)
        {
            if (cam == null) return null;

            var go = new GameObject("~FrameCapture");
            var fc = go.AddComponent<FrameCapture>();
            fc._camera = cam;
            fc._path = absolutePath;
            fc._rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "FrameCaptureRT",
                antiAliasing = 1,
            };
            fc._rt.Create();

            // 이 프레임 렌더부터 RT 로 들어간다. 파이프라인이 정상 경로로 그리므로
            // 포스트프로세싱이 그대로 적용된다.
            cam.targetTexture = fc._rt;
            fc._framesLeft = 2;
            Busy = true;
            LastSavedPath = "";
            return fc;
        }

        void LateUpdate()
        {
            if (_framesLeft < 0) return;
            if (_framesLeft-- > 0) return;   // 렌더가 한 번은 돌아야 한다

            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(_rt.width, _rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            _camera.targetTexture = null;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                File.WriteAllBytes(_path, tex.EncodeToPNG());
                LastSavedPath = _path;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[FrameCapture] 저장 실패: {e.Message}");
                LastSavedPath = "";
            }

            Destroy(tex);
            _rt.Release();
            Destroy(_rt);
            Busy = false;
            Destroy(gameObject);
        }
    }
}
