using System.Collections.Generic;
using UnityEngine;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 머리 위 월드 정보(이름표·상태·체력 바)를 원근 화면에 다시 투영하기 위한 한 프레임짜리 목록
    /// (3d-perspective-production-plan §4 5단계 "스크린 공간 오버레이로 다시 투영한다").
    ///
    /// 2D 본선에서는 <c>TextMesh</c>·<c>SpriteRenderer</c> 자식으로 월드에 떠 있지만,
    /// 원근 월드는 저해상도 RenderTexture 를 확대해 그리므로 그대로 빌보드로 옮기면 글자가 뭉개진다.
    /// 그래서 위치만 3D 에서 받고 그리기는 화면 해상도로 한다.
    ///
    /// 목록은 매 프레임 비운다 — 소비자(<c>PerspectiveWorldView.OnGUI</c>)가 그린 뒤 <see cref="Clear"/>.
    /// </summary>
    public static class PerspectiveWorldInfo
    {
        public enum Kind { Text = 0, Bar = 1 }

        public struct Entry
        {
            /// <summary>시뮬레이션 XY.</summary>
            public Vector2 Ground;
            /// <summary>바닥에서 띄운 높이(셀).</summary>
            public float Height;
            public Kind Kind;
            public string Text;
            public Color Color;
            /// <summary>Bar 전용 — 0~1 채움 비율.</summary>
            public float Fill;
            /// <summary>Bar 전용 — 셀 단위 가로 폭.</summary>
            public float Width;
            /// <summary>Text 전용 — 화면 픽셀 기준 글자 크기 배율.</summary>
            public float Scale;
        }

        static readonly List<Entry> Entries = new List<Entry>(64);

        public static IReadOnlyList<Entry> All => Entries;

        /// <summary>원근 월드가 화면을 소유할 때만 쌓는다 — 2D 본선에서는 비용이 0 이다.</summary>
        public static bool Wanted => PerspectiveViewport.Active;

        public static void Text(Vector2 ground, float height, string text, Color color, float scale = 1f)
        {
            if (!Wanted || string.IsNullOrEmpty(text)) return;
            Entries.Add(new Entry
            {
                Ground = ground, Height = height, Kind = Kind.Text,
                Text = text, Color = color, Scale = scale,
            });
        }

        public static void Bar(Vector2 ground, float height, float fill, float width, Color color)
        {
            if (!Wanted) return;
            Entries.Add(new Entry
            {
                Ground = ground, Height = height, Kind = Kind.Bar,
                Fill = Mathf.Clamp01(fill), Width = width, Color = color,
            });
        }

        public static void Clear() => Entries.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Entries.Clear();
    }
}
