using System;
using System.Collections.Generic;
using TunnelCrew.Sim;
using UnityEngine;

namespace TunnelCrew.Presentation
{
    /// <summary>투영 프리셋. 눈으로 비교하기 위한 것으로, 시뮬레이션에는 아무 영향이 없다.</summary>
    public enum ProjectionPreset
    {
        /// <summary>2:1 마름모. 카메라 피치 30°(화면상 변 26.57°). 픽셀 계단이 2:1 로 맞아 도트의 표준.</summary>
        Dimetric2To1 = 0,
        /// <summary>1.732:1 마름모. 카메라 피치 35.264°, 3축 모두 120° — 수학적 정통 아이소메트릭.</summary>
        TrueIsometric = 1,
        /// <summary>1:1 마름모(밀리터리/플랜 오블리크). 윗면이 왜곡 없는 정사각형, 벽이 가장 두껍게 보인다.</summary>
        Military1To1 = 2,
        /// <summary>45° 회전을 없앤 3/4 톱다운. 타일이 축 정렬 사각형으로 남는다(젤다·스타듀 계열).</summary>
        ThreeQuarter = 3,
        /// <summary>레퍼런스 영상형 직교 톱다운. 회전과 세로 압축 없이 바닥 XY를 화면 XY에 1:1로 둔다.</summary>
        ReferenceTopDown = 4,
    }

    /// <summary>
    /// 시뮬레이션의 직교 XY 좌표를 화면 좌표로 바꾼다.
    /// 게임 규칙·충돌·길찾기는 원래 좌표를 유지하고 Presentation 에서만 이 변환을 쓴다.
    ///
    /// 2026-09-08: 손맛을 눈으로 비교하려고 프리셋 5종을 넣었다(<see cref="ProjectionPreset"/>).
    /// 기본값은 기존과 완전히 같은 2:1 이고, 프리셋을 바꾸면 <see cref="Changed"/> 로 알린다.
    /// 매 프레임 <see cref="ToRender(Vec2, float)"/> 를 호출하는 뷰들은 그대로 따라오고,
    /// 한 번만 계산해 두는 쪽(타일 그리드 Transform · 벽 그림자 · 어둠 셰이더)만 이 이벤트로 다시 만든다.
    /// </summary>
    public static class IsometricProjection
    {
        /// <summary>타일 반너비(월드 유닛). 프리셋과 무관하게 1셀 = 1유닛 폭을 유지한다.</summary>
        public static float HalfTileWidth { get; private set; } = 0.5f;
        /// <summary>타일 반높이. 이 값이 마름모의 납작함(= 카메라 기울기)을 결정한다.</summary>
        public static float HalfTileHeight { get; private set; } = 0.25f;

        /// <summary>현재 프리셋.</summary>
        public static ProjectionPreset Preset { get; private set; } = ProjectionPreset.Dimetric2To1;

        /// <summary>프리셋이 바뀌었다. 캐시해 둔 렌더 좌표를 다시 만들어야 하는 쪽이 구독한다.</summary>
        public static event Action Changed;

        /// <summary>45° 회전(마름모) 계열인가. false 면 타일이 축 정렬 사각형으로 남는다.</summary>
        public static bool IsDiamond => Preset != ProjectionPreset.ThreeQuarter
            && Preset != ProjectionPreset.ReferenceTopDown;

        /// <summary>F8/F9 순환과 화면 라벨이 공유하는 전체 프리셋 수.</summary>
        public static int PresetCount => System.Enum.GetValues(typeof(ProjectionPreset)).Length;

        /// <summary>발밑 그림자·원형 이펙트를 눌러야 하는 비율. 마름모의 납작함과 같다.</summary>
        public static float ShadowSquash => HalfTileHeight / HalfTileWidth;

        const float Sqrt2 = 1.41421356f;

        /// <summary>프리셋별 (반너비, 반높이). 반너비는 1셀=1유닛으로 고정하고 반높이만 바꾼다.</summary>
        static (float hw, float hh) Metrics(ProjectionPreset p) => p switch
        {
            ProjectionPreset.TrueIsometric => (0.5f, 0.5f / 1.73205081f),   // 1:√3 — 3축 120°
            ProjectionPreset.Military1To1 => (0.5f, 0.5f),                  // 윗면이 진짜 정사각형
            ProjectionPreset.ThreeQuarter => (0.5f, 0.3f),                  // 회전 없음, 세로만 0.6배
            ProjectionPreset.ReferenceTopDown => (0.5f, 0.5f),             // 회전·압축 없음, 바닥 XY를 화면 XY에 1:1 대응
            _ => (0.5f, 0.25f),                                             // 2:1 (기본)
        };

        public static void SetPreset(ProjectionPreset preset)
        {
            if (preset == Preset) return;
            Preset = preset;
            var (hw, hh) = Metrics(preset);
            HalfTileWidth = hw;
            HalfTileHeight = hh;
            ApplyGrids();
            Changed?.Invoke();
        }

        /// <summary>프리셋을 순서대로 넘긴다(마지막 다음은 처음).</summary>
        public static ProjectionPreset Next(int step = 1)
        {
            int n = PresetCount;
            return (ProjectionPreset)(((int)Preset + step % n + n) % n);
        }

        public static string Label(ProjectionPreset p) => p switch
        {
            ProjectionPreset.TrueIsometric => "트루 아이소메트릭 (1.732:1 마름모 · 카메라 35.26°)",
            ProjectionPreset.Military1To1 => "밀리터리 1:1 (윗면 정사각형 · 벽이 가장 두껍다)",
            ProjectionPreset.ThreeQuarter => "3/4 톱다운 (회전 없음 · 타일 사각형)",
            ProjectionPreset.ReferenceTopDown => "레퍼런스 3/4 직교 (회전·압축 없음 · 영상 기준)",
            _ => "2:1 다이메트릭 (기본 · 픽셀 정합)",
        };

        public static Vector2 ToRender(Vector2 world) => IsDiamond
            ? new Vector2((world.x - world.y) * HalfTileWidth, (world.x + world.y) * HalfTileHeight)
            : new Vector2(world.x * (HalfTileWidth * 2f), world.y * (HalfTileHeight * 2f));

        public static Vector2 ToRender(Vec2 world) => ToRender(new Vector2((float)world.X, (float)world.Y));

        public static Vector3 ToRender3(Vec2 world, float z = 0f)
        {
            var p = ToRender(world);
            return new Vector3(p.x, p.y, z);
        }

        public static Vector3 ToRender3(Vector2 world, float z = 0f)
        {
            var p = ToRender(world);
            return new Vector3(p.x, p.y, z);
        }

        /// <summary>렌더 좌표를 시뮬레이션 XY 좌표로 되돌린다.</summary>
        public static Vector2 ToWorld(Vector2 render)
        {
            if (!IsDiamond)
                return new Vector2(render.x / (HalfTileWidth * 2f), render.y / (HalfTileHeight * 2f));

            float a = render.x / HalfTileWidth;   // = x - y
            float b = render.y / HalfTileHeight;  // = x + y
            return new Vector2((a + b) * 0.5f, (b - a) * 0.5f);
        }

        /// <summary>
        /// 렌더 좌표 → 시뮬레이션 좌표 변환 행렬을 (a, b, c, d) 로 준다.
        /// sim = (a·rx + b·ry, c·rx + d·ry). 셰이더에서 역변환이 필요할 때 쓴다(Darkness.shader).
        /// </summary>
        public static Vector4 InverseRow()
        {
            if (!IsDiamond)
                return new Vector4(1f / (HalfTileWidth * 2f), 0f, 0f, 1f / (HalfTileHeight * 2f));

            float ix = 0.5f / HalfTileWidth, iy = 0.5f / HalfTileHeight;
            return new Vector4(ix, iy, -ix, iy);
        }

        public static Vector2 DirectionToRender(double angleRadians) => ToRender(new Vector2(
            Mathf.Cos((float)angleRadians), Mathf.Sin((float)angleRadians)));

        public static float AngleToRender(double angleRadians)
        {
            var d = DirectionToRender(angleRadians);
            return Mathf.Atan2(d.y, d.x);
        }

        /// <summary>화면 기준 이동 입력을 시뮬레이션 방향으로 바꾼다.</summary>
        public static Vector2 ScreenDirectionToWorld(Vector2 screenDirection)
        {
            var world = ToWorld(screenDirection);
            return world.sqrMagnitude > 0.0001f ? world.normalized : Vector2.zero;
        }

        /// <summary>직사각형 시뮬레이션 맵을 투영했을 때의 화면축 정렬 경계.</summary>
        public static void Bounds(int cols, int rows, out Vector2 min, out Vector2 max)
        {
            if (!IsDiamond)
            {
                min = Vector2.zero;
                max = new Vector2(cols * HalfTileWidth * 2f, rows * HalfTileHeight * 2f);
                return;
            }

            min = new Vector2(-rows * HalfTileWidth, 0f);
            max = new Vector2(cols * HalfTileWidth, (cols + rows) * HalfTileHeight);
        }

        // ───────────────────────────── 타일 그리드 Transform
        /// <summary>프리셋을 바꿀 때 다시 맞춰야 하는 그리드들. 씬 전환으로 파괴된 것은 그때 걸러낸다.</summary>
        static readonly List<(Transform root, Transform grid)> Grids = new List<(Transform, Transform)>();

        /// <summary>
        /// 정사각 Tilemap 전체를 동적 오브젝트와 같은 방식으로 투영한다.
        /// 부모의 비균등 스케일이 회전 뒤에 적용되도록 Transform 을 두 단계로 나눈다.
        /// </summary>
        public static Transform ConfigureTileGrid(Transform grid)
        {
            var parent = grid.parent;
            var root = new GameObject("Isometric Projection").transform;
            root.SetParent(parent, false);
            grid.SetParent(root, false);
            Grids.Add((root, grid));
            ApplyGrid(root, grid);
            return root;
        }

        static void ApplyGrid(Transform root, Transform grid)
        {
            if (IsDiamond)
            {
                // 1x1 셀을 45° 돌리면 대각 폭이 √2 가 된다. 여기에 hw·√2 를 곱해 폭 2·hw 로 맞춘다.
                root.localScale = new Vector3(HalfTileWidth * Sqrt2, HalfTileHeight * Sqrt2, 1f);
                grid.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }
            else
            {
                root.localScale = new Vector3(HalfTileWidth * 2f, HalfTileHeight * 2f, 1f);
                grid.localRotation = Quaternion.identity;
            }
        }

        static void ApplyGrids()
        {
            for (int i = Grids.Count - 1; i >= 0; i--)
            {
                var (root, grid) = Grids[i];
                if (root == null || grid == null) { Grids.RemoveAt(i); continue; }
                ApplyGrid(root, grid);
            }
        }
    }
}
