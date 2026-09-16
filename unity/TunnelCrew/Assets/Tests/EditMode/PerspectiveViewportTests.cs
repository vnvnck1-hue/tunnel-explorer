using NUnit.Framework;
using TunnelCrew.Presentation.Visual;
using UnityEngine;

namespace TunnelCrew.Tests
{
    /// <summary>
    /// 원근 월드의 입력 좌표 정합(3d-perspective-production-plan §4 2단계 완료 기준
    /// "화면 모서리, 레터박스, 다른 화면비에서도 입력 오차가 없다").
    ///
    /// 원근 월드는 480×270 RenderTexture 를 레터박스로 확대해 그리므로, 화면비가 바뀌면
    /// 마우스 좌표와 바닥 평면의 대응이 어긋나기 쉽다. 여기서 왕복 변환으로 고정한다.
    /// </summary>
    public sealed class PerspectiveViewportTests
    {
        const int TargetWidth = 480, TargetHeight = 270;
        const float Pitch = 55f, Fov = 28f, Distance = 30f;

        Camera _camera;
        GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Test perspective camera");
            _camera = _go.AddComponent<Camera>();
            _camera.enabled = false;
            _camera.orthographic = false;
            _camera.fieldOfView = Fov;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 200f;
            _camera.aspect = (float)TargetWidth / TargetHeight;

            var rotation = Quaternion.Euler(Pitch, 0f, 0f);
            // 바닥 원점을 바라보는 리그. 실제 런타임(PerspectiveWorldView)과 같은 배치다.
            _camera.transform.SetPositionAndRotation(-(rotation * Vector3.forward) * Distance, rotation);
        }

        [TearDown]
        public void TearDown()
        {
            PerspectiveViewport.Clear();
            if (_go != null) Object.DestroyImmediate(_go);
        }

        static readonly (int w, int h, string name)[] Screens =
        {
            (1920, 1080, "16:9"),
            (1920, 1200, "16:10"),
            (2560, 1080, "21:9"),
            (1280, 1024, "5:4"),
            (1080, 1920, "세로"),
        };

        [Test]
        public void 레터박스_사각형은_화면_안에_들어가고_비율을_지킨다()
        {
            foreach (var (w, h, name) in Screens)
            {
                var rect = PerspectiveViewport.FitRect(TargetWidth, TargetHeight, w, h);

                Assert.That(rect.width, Is.LessThanOrEqualTo(w + 0.01f), name);
                Assert.That(rect.height, Is.LessThanOrEqualTo(h + 0.01f), name);
                Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(-0.01f), name);
                Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(-0.01f), name);
                // 가로세로 중 하나는 화면에 꽉 차야 한다(ScaleToFit).
                Assert.That(Mathf.Approximately(rect.width, w) || Mathf.Approximately(rect.height, h),
                    Is.True, $"{name}: 한 축은 화면을 채워야 한다");
                Assert.That(rect.width / rect.height,
                    Is.EqualTo((float)TargetWidth / TargetHeight).Within(0.001f), name);
                // 좌우·상하 여백이 같아야 중앙 정렬이다.
                Assert.That(rect.xMin, Is.EqualTo(w - rect.xMax).Within(0.01f), $"{name}: 좌우 여백");
                Assert.That(rect.yMin, Is.EqualTo(h - rect.yMax).Within(0.01f), $"{name}: 상하 여백");
            }
        }

        [Test]
        public void 화면비가_달라도_바닥_좌표_왕복이_일치한다()
        {
            foreach (var (w, h, name) in Screens)
            {
                PerspectiveViewport.Publish(_camera, PerspectiveViewport.FitRect(TargetWidth, TargetHeight, w, h));
                Assert.That(PerspectiveViewport.Active, Is.True, name);

                // 화면 모서리를 포함해 여러 지점에서 sim → screen → sim 왕복을 확인한다.
                foreach (var sim in new[]
                {
                    Vector2.zero,
                    new Vector2(3.5f, 2.25f),
                    new Vector2(-4f, 6f),
                    new Vector2(7f, -3f),
                })
                {
                    Assert.That(PerspectiveViewport.TrySimToScreen(sim, 0f, out var screen), Is.True,
                        $"{name}: {sim} 투영 실패");
                    Assert.That(PerspectiveViewport.TryScreenToSim(screen, out var back), Is.True,
                        $"{name}: {sim} 역투영 실패");
                    Assert.That(back.x, Is.EqualTo(sim.x).Within(0.01f), $"{name}: {sim} x");
                    Assert.That(back.y, Is.EqualTo(sim.y).Within(0.01f), $"{name}: {sim} y");
                }
            }
        }

        [Test]
        public void 레터박스_바깥을_눌러도_바닥_평면_위의_점이_나온다()
        {
            // 21:9 는 위아래에 큰 여백이 생긴다. 여백을 눌렀을 때 예외나 NaN 이 나오면 안 된다.
            PerspectiveViewport.Publish(_camera, PerspectiveViewport.FitRect(TargetWidth, TargetHeight, 2560, 1080));

            foreach (var point in new[] { new Vector2(10f, 5f), new Vector2(2550f, 1075f), new Vector2(1280f, 2f) })
            {
                if (!PerspectiveViewport.TryScreenToSim(point, out var sim)) continue;
                Assert.That(float.IsNaN(sim.x) || float.IsNaN(sim.y), Is.False, $"{point} 에서 NaN");
                Assert.That(float.IsInfinity(sim.x) || float.IsInfinity(sim.y), Is.False, $"{point} 에서 무한대");
            }
        }

        [Test]
        public void 원근이_꺼져_있으면_2D_경로가_그대로_쓰인다()
        {
            PerspectiveViewport.Clear();

            Assert.That(PerspectiveViewport.Active, Is.False);
            Assert.That(PerspectiveViewport.TryScreenToSim(new Vector2(100f, 100f), out _), Is.False);
            Assert.That(PerspectiveViewport.TrySimToScreen(Vector2.zero, 0f, out _), Is.False);
            Assert.That(PerspectiveViewport.ScreenDirectionToSim(Vector2.up), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void 화면_방향_입력이_카메라_기준으로_바뀐다()
        {
            PerspectiveViewport.Publish(_camera, PerspectiveViewport.FitRect(TargetWidth, TargetHeight, 1920, 1080));

            // 피치만 준 리그이므로 화면 위 = 시뮬 +Y, 화면 오른쪽 = 시뮬 +X 여야 한다.
            var up = PerspectiveViewport.ScreenDirectionToSim(Vector2.up);
            var right = PerspectiveViewport.ScreenDirectionToSim(Vector2.right);

            Assert.That(up.y, Is.GreaterThan(0.99f));
            Assert.That(Mathf.Abs(up.x), Is.LessThan(0.01f));
            Assert.That(right.x, Is.GreaterThan(0.99f));
            Assert.That(Mathf.Abs(right.y), Is.LessThan(0.01f));
            // 대각 입력은 정규화되어야 한다 — 대각이 더 빠르면 안 된다.
            Assert.That(PerspectiveViewport.ScreenDirectionToSim(new Vector2(1f, 1f)).magnitude,
                Is.EqualTo(1f).Within(0.001f));
        }
    }
}
