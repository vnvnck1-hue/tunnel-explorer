using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TunnelCrew.EditorTools.ArtPipeline
{
    /// <summary>
    /// manifest.json 을 읽기 위한 최소 JSON 파서.
    ///
    /// <b>왜 직접 쓰는가</b> — <c>JsonUtility</c> 는 사전(<c>channels</c> 같은 임의 키 객체)을
    /// 다루지 못한다. Newtonsoft 는 PackageCache 에 있지만 <c>Packages/manifest.json</c> 의
    /// 직접 의존이 아니라 간접 의존이다. 검사 도구가 간접 의존에 걸려 컴파일이 깨지면
    /// 아트 인계 자체가 막히므로, 의존 없는 파서를 둔다.
    ///
    /// 파싱 실패는 예외가 아니라 <see cref="Error"/> 로 돌려준다 — 검사기는 잘못된 manifest 도
    /// "오류 항목" 으로 보고해야 하고, 도중에 죽어서는 안 된다.
    /// </summary>
    public static class MiniJson
    {
        /// <summary>파싱 결과. 객체는 <c>Dictionary&lt;string, object&gt;</c>, 배열은 <c>List&lt;object&gt;</c>,
        /// 수는 <c>double</c>, 그 밖에는 <c>string</c>·<c>bool</c>·<c>null</c> 이다.</summary>
        public static object Parse(string text, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(text)) { error = "빈 문서"; return null; }

            int i = 0;
            try
            {
                var value = ParseValue(text, ref i);
                SkipWhitespace(text, ref i);
                if (i < text.Length) error = $"{i}번째 문자 뒤에 남은 내용이 있다";
                return value;
            }
            catch (JsonError e)
            {
                error = e.Message;
                return null;
            }
        }

        sealed class JsonError : System.Exception
        {
            public JsonError(string message) : base(message) { }
        }

        static object ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length) throw new JsonError("값이 오기 전에 문서가 끝났다");

            char c = s[i];
            switch (c)
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return ParseString(s, ref i);
                case 't': Expect(s, ref i, "true"); return true;
                case 'f': Expect(s, ref i, "false"); return false;
                case 'n': Expect(s, ref i, "null"); return null;
                default: return ParseNumber(s, ref i);
            }
        }

        static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            var map = new Dictionary<string, object>();
            i++;                                    // '{'
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return map; }

            while (true)
            {
                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != '"') throw new JsonError($"{i}번째: 키 문자열이 필요하다");
                string key = ParseString(s, ref i);

                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new JsonError($"{i}번째: ':' 가 필요하다");
                i++;

                map[key] = ParseValue(s, ref i);

                SkipWhitespace(s, ref i);
                if (i >= s.Length) throw new JsonError("객체가 닫히지 않았다");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return map; }
                throw new JsonError($"{i}번째: ',' 또는 '}}' 가 필요하다");
            }
        }

        static List<object> ParseArray(string s, ref int i)
        {
            var list = new List<object>();
            i++;                                    // '['
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return list; }

            while (true)
            {
                list.Add(ParseValue(s, ref i));
                SkipWhitespace(s, ref i);
                if (i >= s.Length) throw new JsonError("배열이 닫히지 않았다");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return list; }
                throw new JsonError($"{i}번째: ',' 또는 ']' 가 필요하다");
            }
        }

        static string ParseString(string s, ref int i)
        {
            i++;                                    // 여는 '"'
            var sb = new StringBuilder();
            while (true)
            {
                if (i >= s.Length) throw new JsonError("문자열이 닫히지 않았다");
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }

                if (i >= s.Length) throw new JsonError("이스케이프가 끊겼다");
                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw new JsonError("\\u 뒤에 4자리가 없다");
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber,
                            CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: throw new JsonError($"알 수 없는 이스케이프 \\{e}");
                }
            }
        }

        static double ParseNumber(string s, ref int i)
        {
            int start = i;
            if (i < s.Length && (s[i] == '-' || s[i] == '+')) i++;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == 'e' || s[i] == 'E'
                                    || s[i] == '-' || s[i] == '+')) i++;
            var span = s.Substring(start, i - start);
            if (!double.TryParse(span, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                throw new JsonError($"{start}번째: 수를 읽을 수 없다 ('{span}')");
            return d;
        }

        static void Expect(string s, ref int i, string literal)
        {
            if (i + literal.Length > s.Length || s.Substring(i, literal.Length) != literal)
                throw new JsonError($"{i}번째: '{literal}' 가 필요하다");
            i += literal.Length;
        }

        static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++;
        }

        // ───────────────────────────── 조회 도우미

        public static Dictionary<string, object> AsMap(object o) => o as Dictionary<string, object>;
        public static List<object> AsList(object o) => o as List<object>;

        public static string GetString(Dictionary<string, object> map, string key, string fallback = null)
            => map != null && map.TryGetValue(key, out var v) && v is string s ? s : fallback;

        public static bool TryGetInt(Dictionary<string, object> map, string key, out int value)
        {
            value = 0;
            if (map == null || !map.TryGetValue(key, out var v) || !(v is double d)) return false;
            value = (int)System.Math.Round(d);
            return true;
        }

        public static bool TryGetFloat(Dictionary<string, object> map, string key, out float value)
        {
            value = 0f;
            if (map == null || !map.TryGetValue(key, out var v) || !(v is double d)) return false;
            value = (float)d;
            return true;
        }

        /// <summary>정수 2개 배열을 읽는다(footprintCells, pivotPixels, dimensionsPixels).</summary>
        public static bool TryGetInt2(Dictionary<string, object> map, string key, out int a, out int b)
        {
            a = b = 0;
            if (map == null || !map.TryGetValue(key, out var v)) return false;
            var list = AsList(v);
            if (list == null || list.Count < 2) return false;
            if (!(list[0] is double x) || !(list[1] is double y)) return false;
            a = (int)System.Math.Round(x);
            b = (int)System.Math.Round(y);
            return true;
        }

        /// <summary>실수 2개 배열을 읽는다(pivotNormalized).</summary>
        public static bool TryGetFloat2(Dictionary<string, object> map, string key, out float a, out float b)
        {
            a = b = 0f;
            if (map == null || !map.TryGetValue(key, out var v)) return false;
            var list = AsList(v);
            if (list == null || list.Count < 2) return false;
            if (!(list[0] is double x) || !(list[1] is double y)) return false;
            a = (float)x;
            b = (float)y;
            return true;
        }

        /// <summary>불리언을 읽는다(foregroundOccluder). 값이 없으면 <paramref name="fallback"/>.</summary>
        public static bool GetBool(Dictionary<string, object> map, string key, bool fallback = false)
            => map != null && map.TryGetValue(key, out var v) && v is bool b ? b : fallback;

        /// <summary>
        /// <c>[[x,y],[x,y],…]</c> 형태의 점 목록을 평평한 배열로 읽는다
        /// (shadowCasterFootprintCells). 점이 하나도 없으면 null.
        /// </summary>
        public static float[] GetFloatPairs(Dictionary<string, object> map, string key)
        {
            if (map == null || !map.TryGetValue(key, out var v)) return null;
            var list = AsList(v);
            if (list == null || list.Count == 0) return null;

            var outp = new float[list.Count * 2];
            for (int i = 0; i < list.Count; i++)
            {
                var pair = AsList(list[i]);
                if (pair == null || pair.Count < 2) return null;
                if (!(pair[0] is double x) || !(pair[1] is double y)) return null;
                outp[i * 2] = (float)x;
                outp[i * 2 + 1] = (float)y;
            }
            return outp;
        }
    }
}
