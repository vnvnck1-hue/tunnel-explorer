using UnityEngine;
using UnityEngine.InputSystem;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 기능명세서 §12.4 디버그 오버레이. F1 로 켜고 F2 로 표시 항목을 넘긴다.
    ///
    /// 조명 수치를 반복 조정하며 구조 문제를 가리는 것이 이번 작업의 대표 위험이다(§17).
    /// 그래서 "무엇이 어느 레이어로 갔는가", "그림자 윤곽이 어디인가" 를 화면에서 바로
    /// 확인할 수 있어야 한다.
    /// </summary>
    public sealed class DepthDebugOverlay : MonoBehaviour
    {
        public enum View
        {
            Off = 0,
            /// <summary>셀 footprint 와 선택된 모듈 ID.</summary>
            Topology,
            /// <summary>Sorting Layer 와 order.</summary>
            Sorting,
            /// <summary>전경 fade 그룹과 현재 알파.</summary>
            Foreground,
            /// <summary>벽 footprint 윤곽과 캐스터 수(§7.4).</summary>
            ShadowContours,
            /// <summary>접촉 AO 와 발 위치·시각 높이(§7.4-1·§6.5).</summary>
            ContactAndHeight,
            /// <summary>재질 채널 단독 보기와 카메라 프로파일(§7.1·§10).</summary>
            ChannelsAndCamera,
        }

        const int ViewCount = 6;   // Off 를 제외한 개수

        [SerializeField] VisualLabController _lab;
        [SerializeField] View _view = View.Off;

        GUIStyle _label, _small;
        Material _lineMaterial;

        void OnDestroy()
        {
            if (_lineMaterial != null) Destroy(_lineMaterial);
        }

#if UNITY_EDITOR
        public void EditorAssign(VisualLabController lab) => _lab = lab;
#endif

        /// <summary>캡처 도구가 특정 뷰를 강제할 때 쓴다.</summary>
        public View Current
        {
            get => _view;
            set => _view = value;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f1Key.wasPressedThisFrame)
                _view = _view == View.Off ? View.Topology : View.Off;
            else if (kb.f2Key.wasPressedThisFrame && _view != View.Off)
                _view = (View)(((int)_view % ViewCount) + 1);
        }

        void OnGUI()
        {
            if (_view == View.Off) return;

            float k = Screen.height / 1080f;
            _label ??= new GUIStyle(GUI.skin.label) { richText = true };
            _label.fontSize = Mathf.RoundToInt(15 * k);
            _small ??= new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.MiddleCenter };
            _small.fontSize = Mathf.RoundToInt(10 * k);

            DrawHeader(k);

            switch (_view)
            {
                case View.Topology: DrawTopology(); break;
                case View.Sorting: DrawSorting(); break;
                case View.Foreground: DrawForeground(); break;
                case View.ShadowContours: DrawShadowContours(); break;
                case View.ContactAndHeight: DrawContactAndHeight(); break;
                case View.ChannelsAndCamera: DrawChannelsAndCamera(); break;
            }
        }

        void DrawHeader(float k)
        {
            var box = new Rect(12 * k, 12 * k, 620 * k, 122 * k);
            GUI.color = new Color(.05f, .04f, .09f, .74f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;

            float y = box.y + 6 * k;
            void Line(string text)
            {
                GUI.Label(new Rect(box.x + 10 * k, y, box.width - 16 * k, 24 * k), text, _label);
                y += 24 * k;
            }

            Line($"<b>깊이 디버그 · {_view}</b>   <color=#888>F1 끄기 · F2 다음</color>");
            Line($"투영 {IsometricProjection.Label(IsometricProjection.Preset)}");

            var env = _lab != null ? _lab.Environment : null;
            var shadows = _lab != null ? _lab.Shadows : null;
            var contact = _lab != null ? _lab.ContactShadows : null;

            Line($"앵커 {FootpointSorter.All.Count} · dirty 청크 {(env != null ? env.PendingDirtyChunks : 0)}" +
                 $" · 윤곽 {(shadows != null ? shadows.Contours.Count : 0)}" +
                 $" · 캐스터 {(shadows != null ? shadows.CasterCount : 0)}" +
                 $" · 접촉 AO {(contact != null ? contact.ActiveCount : 0)}");

            string channel = VisualChannelDebug.View == ChannelView.Composite
                ? "합성"
                : VisualChannelDebug.Label(VisualChannelDebug.View);
            Line($"채널 <b>{channel}</b> <color=#888>(Z)</color>" +
                 $"   카메라 <b>{(_lab != null ? _lab.ViewMode.ToString() : "-")}</b> " +
                 $"{(_lab != null ? _lab.ViewCells : 0f):0.0}셀 <color=#888>(V)</color>");
        }

        void DrawTopology()
        {
            if (_lab == null || _lab.Field == null || _lab.Environment == null) return;
            var cam = Camera.main;
            if (cam == null) return;

            var field = _lab.Field;
            for (int r = 0; r < field.Rows; r++)
                for (int c = 0; c < field.Cols; c++)
                {
                    var s = _lab.Environment.SurfaceAt(c, r);
                    if (s.Surfaces == SurfaceMask.None) continue;

                    // 셀 중심의 지면점을 화면으로. 세로 오프셋은 넣지 않는다 —
                    // 여기서 보고 싶은 것은 지면 격자이지 올려 그린 cap 이 아니다.
                    var world = IsometricProjection.ToRender(new Vector2(c + 0.5f, r + 0.5f));
                    var sp = cam.WorldToScreenPoint(new Vector3(world.x, world.y, 0f));
                    if (sp.x < 0 || sp.y < 0 || sp.x > Screen.width || sp.y > Screen.height) continue;

                    string tag = (s.Surfaces & SurfaceMask.Buried) != 0 ? "·"
                        : (s.Surfaces & SurfaceMask.ForegroundTop) != 0 ? $"F{s.TopModule}"
                        : (s.Surfaces & SurfaceMask.WallTop) != 0 ? $"T{s.TopModule}"
                        : $"{s.FloorModule}";

                    GUI.color = (s.Surfaces & SurfaceMask.ForegroundTop) != 0
                        ? new Color(1f, .55f, .35f)
                        : (s.Surfaces & SurfaceMask.FrontFace) != 0
                            ? new Color(.55f, .85f, 1f)
                            : new Color(.8f, .8f, .8f, .7f);
                    GUI.Label(new Rect(sp.x - 18, Screen.height - sp.y - 9, 36, 18), tag, _small);
                }
            GUI.color = Color.white;
        }

        void DrawSorting()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var anchors = FootpointSorter.All;
            for (int i = 0; i < anchors.Count; i++)
            {
                var a = anchors[i];
                if (a == null) continue;
                var world = IsometricProjection.ToRender(a.groundPosition);
                var sp = cam.WorldToScreenPoint(new Vector3(world.x, world.y, 0f));
                GUI.color = a.isLocalInterest ? new Color(1f, .85f, .4f) : Color.white;
                GUI.Label(new Rect(sp.x - 90, Screen.height - sp.y - 10, 180, 20),
                    $"{a.sortingLayer} / {DepthSort.OrderFor(a.SortGroundY)}", _small);
            }
            GUI.color = Color.white;
        }

        void DrawForeground()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var occluders = Object.FindObjectsByType<ForegroundOccluder>(FindObjectsSortMode.None);
            foreach (var o in occluders)
            {
                if (o == null) continue;
                var center = new Vector2(o.footprintCells.center.x, o.footprintCells.center.y);
                var world = IsometricProjection.ToRender(center);
                var sp = cam.WorldToScreenPoint(new Vector3(world.x, world.y, 0f));
                GUI.color = o.Alpha < 0.999f ? new Color(1f, .55f, .35f) : new Color(.7f, .7f, .7f, .6f);
                GUI.Label(new Rect(sp.x - 60, Screen.height - sp.y - 10, 120, 20),
                    $"g{o.fadeGroup} α{o.Alpha:0.00}", _small);
            }
            GUI.color = Color.white;
        }

        /// <summary>
        /// 벽 footprint 윤곽을 선으로 그린다(§12.4 "그림자 캐스터 윤곽").
        /// 바깥 고리와 방·통로 고리를 색으로 구분한다.
        /// </summary>
        void DrawShadowContours()
        {
            var cam = Camera.main;
            var shadows = _lab != null ? _lab.Shadows : null;
            if (cam == null || shadows == null) return;

            var contours = shadows.Contours;
            for (int i = 0; i < contours.Count; i++)
            {
                var contour = contours[i];
                var pts = contour.Points;
                if (pts.Count < 2) continue;

                var color = contour.IsOuter
                    ? new Color(1f, .45f, .25f, .9f)      // 벽 덩어리의 바깥 고리
                    : new Color(.35f, .9f, 1f, .9f);      // 방·통로 쪽 고리

                for (int j = 0; j < pts.Count; j++)
                {
                    var a = pts[j];
                    var b = pts[(j + 1) % pts.Count];
                    DrawWorldLine(cam, new Vector2(a.X, a.Y), new Vector2(b.X, b.Y), color);
                }
            }
        }

        void DrawContactAndHeight()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var anchors = FootpointSorter.All;
            for (int i = 0; i < anchors.Count; i++)
            {
                var a = anchors[i];
                if (a == null) continue;

                var ground = IsometricProjection.ToRender(a.groundPosition);
                var sp = cam.WorldToScreenPoint(new Vector3(ground.x, ground.y, 0f));

                // 발 위치 십자
                DrawWorldLine(cam, a.groundPosition + new Vector2(-0.25f, 0f),
                    a.groundPosition + new Vector2(0.25f, 0f), new Color(1f, .85f, .4f));
                DrawWorldLine(cam, a.groundPosition + new Vector2(0f, -0.25f),
                    a.groundPosition + new Vector2(0f, 0.25f), new Color(1f, .85f, .4f));

                // 시각 높이 — 지면에서 본체까지
                if (a.visualHeight > 0.001f)
                {
                    var top = new Vector3(ground.x, ground.y + a.visualHeight, 0f);
                    DrawScreenLine(cam.WorldToScreenPoint(new Vector3(ground.x, ground.y, 0f)),
                        cam.WorldToScreenPoint(top), new Color(.5f, 1f, .6f));
                }

                GUI.color = Color.white;
                GUI.Label(new Rect(sp.x - 80, Screen.height - sp.y + 6, 160, 20),
                    $"발 ({a.groundPosition.x:0.0},{a.groundPosition.y:0.0}) h{a.visualHeight:0.00}", _small);
            }
            GUI.color = Color.white;
        }

        void DrawChannelsAndCamera()
        {
            float k = Screen.height / 1080f;
            var box = new Rect(12 * k, 142 * k, 620 * k, 118 * k);
            GUI.color = new Color(.05f, .04f, .09f, .74f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;

            float y = box.y + 6 * k;
            void Line(string s)
            {
                GUI.Label(new Rect(box.x + 10 * k, y, box.width - 16 * k, 24 * k), s, _label);
                y += 24 * k;
            }

            Line($"Normal {OnOff(VisualChannelDebug.UseNormal)}  " +
                 $"Emission {OnOff(VisualChannelDebug.UseEmission)}  " +
                 $"Mask {OnOff(VisualChannelDebug.UseMask)}  " +
                 $"AO {OnOff(VisualChannelDebug.UseAo)}");

            var profile = _lab != null ? _lab.Profile : null;
            if (profile != null)
            {
                Line($"기본 {profile.baseViewCells:0.0}셀 · 근접 {profile.combatViewCells:0.0}셀 · " +
                     $"협동 {profile.coopViewCells:0.0}셀 · 앵커 {profile.AnchorFromBottom:0.00}");

                // §3.3 목표 범위 안에 들어오는지 — 캐릭터 1.35셀 기준
                const float charCells = 1.35f;
                float ratio = profile.CharacterScreenRatio(charCells, _lab.ViewMode);
                bool ok = profile.CharacterRatioInRange(charCells, _lab.ViewMode);
                Line($"캐릭터 화면 비율 {ratio * 100f:0.0}% " +
                     $"(목표 {profile.characterScreenHeightRange.x * 100f:0}~" +
                     $"{profile.characterScreenHeightRange.y * 100f:0}%) " +
                     (ok ? "<color=#7fdc7f>범위 안</color>" : "<color=#ff9b6b>범위 밖</color>"));
                Line($"최소광 상단 {profile.minLightBySurface.x:0.00} · 정면 {profile.minLightBySurface.y:0.00} · " +
                     $"바닥 {profile.minLightBySurface.z:0.00} · 캐릭터 {profile.minLightBySurface.w:0.00}");
            }
            else Line("<color=#ff9b6b>프로파일이 연결되지 않았다</color>");
        }

        static string OnOff(bool on) => on ? "<color=#7fdc7f>on</color>" : "<color=#ff9b6b>off</color>";

        // ───────────────────────────── 선 그리기

        void DrawWorldLine(Camera cam, Vector2 aCell, Vector2 bCell, Color color)
        {
            var a = IsometricProjection.ToRender(aCell);
            var b = IsometricProjection.ToRender(bCell);
            DrawScreenLine(cam.WorldToScreenPoint(new Vector3(a.x, a.y, 0f)),
                cam.WorldToScreenPoint(new Vector3(b.x, b.y, 0f)), color);
        }

        /// <summary>
        /// OnGUI 안에서 선을 그린다. GL 로 직접 그리는 편이 정확하지만 OnGUI 의 좌표계와
        /// 섞으면 프레임에 따라 어긋난다. 회전한 사각형 텍스처가 오버레이 용도로 충분하다.
        /// </summary>
        void DrawScreenLine(Vector3 aScreen, Vector3 bScreen, Color color)
        {
            var a = new Vector2(aScreen.x, Screen.height - aScreen.y);
            var b = new Vector2(bScreen.x, Screen.height - bScreen.y);

            var delta = b - a;
            float len = delta.magnitude;
            if (len < 0.5f || len > Screen.width * 4f) return;

            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            float thickness = Mathf.Max(1.5f, Screen.height / 720f);

            var prev = GUI.matrix;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.DrawTexture(new Rect(a.x, a.y - thickness * 0.5f, len, thickness), Texture2D.whiteTexture);
            GUI.matrix = prev;
            GUI.color = Color.white;
        }
    }
}
