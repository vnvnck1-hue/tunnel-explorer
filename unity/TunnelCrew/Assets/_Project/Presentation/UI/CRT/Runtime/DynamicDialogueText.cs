using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace TunnelCrew.Presentation.CRT
{
    /// <summary>
    /// Compiles lightweight narrative markup and animates the already-laid-out uGUI glyph mesh.
    /// Keeping the full plain string in Text means typing never changes wrapping or bubble size.
    /// </summary>
    public sealed class DynamicDialogueText : BaseMeshEffect
    {
        [Flags]
        public enum Motion : byte { None = 0, Jitter = 1, Wave = 2, Slam = 4, Echo = 8 }
        public enum Tint : byte { None, Signal, Danger, Morae, Whisper }

        public readonly struct Glyph
        {
            public readonly float RevealAt, Strength;
            public readonly Motion Motion;
            public readonly Tint Tint;
            public Glyph(float revealAt, Motion motion, Tint tint, float strength)
            { RevealAt = revealAt; Motion = motion; Tint = tint; Strength = strength; }
        }

        public sealed class Script
        {
            public readonly string PlainText;
            public readonly Glyph[] Glyphs;
            public readonly float Duration;
            internal Script(string text, Glyph[] glyphs, float duration)
            { PlainText = text; Glyphs = glyphs; Duration = duration; }

            public int VisibleCharacters(float elapsed)
            {
                int count = 0;
                while (count < Glyphs.Length && elapsed >= Glyphs[count].RevealAt) count++;
                return count;
            }
        }

        struct State
        {
            public float Speed, Strength;
            public Motion Motion;
            public Tint Tint;
        }

        Script _script;
        float _elapsed;
        bool _complete, _reduced;

        public static Script Compile(string source, bool enableMarkup = true)
        {
            source ??= string.Empty;
            var text = new StringBuilder(source.Length);
            var glyphs = new List<Glyph>(source.Length);
            var stack = new Stack<State>();
            var state = new State { Speed = 1, Strength = 1 };
            float reveal = 0;

            for (int i = 0; i < source.Length;)
            {
                if (enableMarkup && source[i] == '[')
                {
                    int close = source.IndexOf(']', i + 1);
                    if (close > i && ApplyTag(source.Substring(i + 1, close - i - 1), ref state, stack, ref reveal))
                    { i = close + 1; continue; }
                }

                char ch = source[i++];
                text.Append(ch);
                glyphs.Add(new Glyph(reveal, state.Motion, state.Tint, state.Strength));
                if (!char.IsWhiteSpace(ch))
                {
                    reveal += 1f / (34f * Mathf.Max(.15f, state.Speed));
                    if (ch == '.' || ch == '!' || ch == '?' || ch == '。') reveal += .13f;
                    else if (ch == ',' || ch == '…' || ch == ':' || ch == ';') reveal += .07f;
                }
            }
            return new Script(text.ToString(), glyphs.ToArray(), reveal);
        }

        static bool ApplyTag(string raw, ref State state, Stack<State> stack, ref float reveal)
        {
            string tag = raw.Trim().ToLowerInvariant();
            if (tag.StartsWith("wait="))
            {
                if (TryFloat(tag.Substring(5), out float wait)) reveal += Mathf.Clamp(wait, 0, 2);
                return true;
            }
            if (tag.Length > 1 && tag[0] == '/')
            {
                if (stack.Count > 0) state = stack.Pop();
                return true;
            }

            State before = state;
            if (tag.StartsWith("speed="))
            {
                stack.Push(before);
                if (TryFloat(tag.Substring(6), out float speed)) state.Speed = Mathf.Clamp(speed, .2f, 4f);
                return true;
            }
            if (tag.StartsWith("tint="))
            {
                stack.Push(before);
                Enum.TryParse(tag.Substring(5), true, out state.Tint);
                return true;
            }
            if (tag.StartsWith("jitter"))
            {
                stack.Push(before); state.Motion |= Motion.Jitter;
                if (ValueAfterEquals(tag, out float strength)) state.Strength = Mathf.Clamp(strength, .1f, 2f);
                return true;
            }
            if (tag.StartsWith("wave"))
            {
                stack.Push(before); state.Motion |= Motion.Wave;
                if (ValueAfterEquals(tag, out float strength)) state.Strength = Mathf.Clamp(strength, .1f, 2f);
                return true;
            }
            if (tag == "slam") { stack.Push(before); state.Motion |= Motion.Slam; return true; }
            if (tag == "echo") { stack.Push(before); state.Motion |= Motion.Echo; return true; }
            if (tag.StartsWith("erase=")) return true; // Authored compatibility; visual erase is intentionally non-destructive.
            return false;
        }

        static bool ValueAfterEquals(string value, out float result)
        {
            result = 0f;
            int at = value.IndexOf('=');
            return at >= 0 && TryFloat(value.Substring(at + 1), out result);
        }

        static bool TryFloat(string value, out float result) =>
            float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

        public void Configure(Script script, float elapsed, bool complete, bool reduced)
        {
            _script = script;
            _elapsed = Mathf.Max(0, elapsed);
            _complete = complete;
            _reduced = reduced;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || _script == null || vh.currentVertCount == 0) return;
            var stream = new List<UIVertex>();
            vh.GetUIVertexStream(stream);
            var echoes = new List<UIVertex>();
            int rendered = 0;
            for (int ci = 0; ci < _script.PlainText.Length; ci++)
            {
                char ch = _script.PlainText[ci];
                if (char.IsWhiteSpace(ch)) continue;
                int start = rendered * 6;
                if (start + 5 >= stream.Count) break;
                rendered++;
                Glyph glyph = _script.Glyphs[ci];
                bool visible = _complete || _elapsed >= glyph.RevealAt;
                float age = _complete ? 1 : Mathf.Max(0, _elapsed - glyph.RevealAt);
                Vector2 offset = Vector2.zero;
                if (!_reduced && visible)
                {
                    if ((glyph.Motion & Motion.Wave) != 0) offset.y += Mathf.Sin(_elapsed * 8 + ci * .72f) * 2.2f * glyph.Strength;
                    if ((glyph.Motion & Motion.Jitter) != 0)
                    {
                        int tick = Mathf.FloorToInt(_elapsed * 24);
                        offset += new Vector2(Hash(ci * 31 + tick) - .5f, Hash(ci * 73 + tick * 3) - .5f) * 3.2f * glyph.Strength;
                    }
                }
                float scale = !_reduced && (glyph.Motion & Motion.Slam) != 0 && age < .18f
                    ? Mathf.Lerp(1.55f, 1f, Mathf.Clamp01(age / .18f)) : 1;
                Vector2 center = Vector2.zero;
                for (int v = 0; v < 6; v++) center += (Vector2)stream[start + v].position;
                center /= 6;
                Color32 tint = TintColor(glyph.Tint);
                for (int v = 0; v < 6; v++)
                {
                    UIVertex vert = stream[start + v];
                    Vector2 p = vert.position;
                    vert.position = center + (p - center) * scale + offset;
                    Color32 c = vert.color;
                    if (glyph.Tint != Tint.None) { c.r = tint.r; c.g = tint.g; c.b = tint.b; }
                    if (!visible) c.a = 0;
                    vert.color = c;
                    stream[start + v] = vert;
                    if (!_reduced && visible && (glyph.Motion & Motion.Echo) != 0)
                    {
                        vert.position += new Vector3(3, -2, 0);
                        vert.color = new Color32(105, 214, 232, 70);
                        echoes.Add(vert);
                    }
                }
            }
            if (echoes.Count > 0) stream.InsertRange(0, echoes);
            vh.Clear();
            vh.AddUIVertexTriangleStream(stream);
        }

        static float Hash(int x)
        {
            unchecked { uint n = (uint)x; n = (n ^ 61u) ^ (n >> 16); n *= 9u; n ^= n >> 4; n *= 0x27d4eb2du; n ^= n >> 15; return (n & 1023) / 1023f; }
        }

        static Color32 TintColor(Tint tint) => tint switch
        {
            Tint.Signal => new Color32(30, 150, 165, 255),
            Tint.Danger => new Color32(210, 45, 52, 255),
            Tint.Morae => new Color32(185, 110, 20, 255),
            Tint.Whisper => new Color32(118, 80, 150, 255),
            _ => new Color32(255, 255, 255, 255),
        };
    }
}
