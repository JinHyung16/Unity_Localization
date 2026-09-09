using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Translation
{
    /// <summary>
    /// 번들과 설정 파일만을 위한 최소 JSON 리더/라이터. 코어의 외부 의존성을 0으로 유지하기 위해 존재한다.
    /// 오브젝트는 Dictionary(string, object), 배열은 List(object), 숫자는 double로 읽는다
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            var index = 0;
            SkipWhitespace(text, ref index);
            var value = ParseValue(text, ref index);
            SkipWhitespace(text, ref index);
            if (index != text.Length)
                throw new FormatException("JSON 끝에 남은 문자가 있습니다. 위치 " + index);

            return value;
        }

        public static Dictionary<string, object> ParseObject(string text)
        {
            if (Parse(text) is Dictionary<string, object> map)
                return map;

            throw new FormatException("최상위가 JSON 오브젝트가 아닙니다.");
        }

        public static string Write(object value, bool indent = true)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value, indent, 0);
            return sb.ToString();
        }

        public static string GetString(Dictionary<string, object> map, string key, string fallback = null)
        {
            if (map != null && map.TryGetValue(key, out var value) && value != null)
                return value as string ?? Convert.ToString(value, CultureInfo.InvariantCulture);

            return fallback;
        }

        public static int GetInt(Dictionary<string, object> map, string key, int fallback = 0)
        {
            if (map != null && map.TryGetValue(key, out var value))
            {
                if (value is double d)
                    return (int)d;

                if (value is string s && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                    return parsed;
            }

            return fallback;
        }

        public static bool GetBool(Dictionary<string, object> map, string key, bool fallback = false)
        {
            if (map != null && map.TryGetValue(key, out var value))
            {
                if (value is bool b)
                    return b;

                if (value is string s && bool.TryParse(s, out var parsed))
                    return parsed;
            }

            return fallback;
        }

        public static List<object> GetArray(Dictionary<string, object> map, string key)
        {
            if (map != null && map.TryGetValue(key, out var value) && value is List<object> list)
                return list;

            return null;
        }

        public static Dictionary<string, object> GetObject(Dictionary<string, object> map, string key)
        {
            if (map != null && map.TryGetValue(key, out var value) && value is Dictionary<string, object> obj)
                return obj;

            return null;
        }

        private static object ParseValue(string text, ref int index)
        {
            if (index >= text.Length)
                throw new FormatException("JSON이 갑자기 끝났습니다.");

            var c = text[index];
            switch (c)
            {
                case '{':
                    return ParseObjectBody(text, ref index);
                case '[':
                    return ParseArrayBody(text, ref index);
                case '"':
                    return ParseString(text, ref index);
                case 't':
                    Expect(text, ref index, "true");
                    return true;
                case 'f':
                    Expect(text, ref index, "false");
                    return false;
                case 'n':
                    Expect(text, ref index, "null");
                    return null;
                default:
                    return ParseNumber(text, ref index);
            }
        }

        private static Dictionary<string, object> ParseObjectBody(string text, ref int index)
        {
            var map = new Dictionary<string, object>();
            index++;
            SkipWhitespace(text, ref index);

            if (index < text.Length && text[index] == '}')
            {
                index++;
                return map;
            }

            while (true)
            {
                SkipWhitespace(text, ref index);
                if (index >= text.Length || text[index] != '"')
                    throw new FormatException("오브젝트 키가 문자열이 아닙니다. 위치 " + index);

                var key = ParseString(text, ref index);
                SkipWhitespace(text, ref index);
                if (index >= text.Length || text[index] != ':')
                    throw new FormatException("키 뒤에 콜론이 없습니다. 위치 " + index);

                index++;
                SkipWhitespace(text, ref index);
                map[key] = ParseValue(text, ref index);
                SkipWhitespace(text, ref index);

                if (index >= text.Length)
                    throw new FormatException("오브젝트가 닫히지 않았습니다.");

                if (text[index] == ',')
                {
                    index++;
                    continue;
                }

                if (text[index] == '}')
                {
                    index++;
                    return map;
                }

                throw new FormatException("오브젝트에서 예상 못한 문자: " + text[index] + " 위치 " + index);
            }
        }

        private static List<object> ParseArrayBody(string text, ref int index)
        {
            var list = new List<object>();
            index++;
            SkipWhitespace(text, ref index);

            if (index < text.Length && text[index] == ']')
            {
                index++;
                return list;
            }

            while (true)
            {
                SkipWhitespace(text, ref index);
                list.Add(ParseValue(text, ref index));
                SkipWhitespace(text, ref index);

                if (index >= text.Length)
                    throw new FormatException("배열이 닫히지 않았습니다.");

                if (text[index] == ',')
                {
                    index++;
                    continue;
                }

                if (text[index] == ']')
                {
                    index++;
                    return list;
                }

                throw new FormatException("배열에서 예상 못한 문자: " + text[index] + " 위치 " + index);
            }
        }

        private static string ParseString(string text, ref int index)
        {
            index++;
            var sb = new StringBuilder();

            while (true)
            {
                if (index >= text.Length)
                    throw new FormatException("문자열이 닫히지 않았습니다.");

                var c = text[index++];
                if (c == '"')
                    return sb.ToString();

                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }

                if (index >= text.Length)
                    throw new FormatException("이스케이프가 끊겼습니다.");

                var esc = text[index++];
                switch (esc)
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
                        if (index + 4 > text.Length)
                            throw new FormatException("\\u 이스케이프가 끊겼습니다.");

                        var hex = text.Substring(index, 4);
                        index += 4;
                        sb.Append((char)ushort.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        break;
                    default:
                        throw new FormatException("알 수 없는 이스케이프: \\" + esc);
                }
            }
        }

        private static object ParseNumber(string text, ref int index)
        {
            var start = index;
            while (index < text.Length && IsNumberChar(text[index]))
                index++;

            if (start == index)
                throw new FormatException("숫자를 읽을 수 없습니다. 위치 " + index);

            var slice = text.Substring(start, index - start);
            if (!double.TryParse(slice, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                throw new FormatException("숫자 형식이 잘못됐습니다: " + slice);

            return value;
        }

        private static bool IsNumberChar(char c)
        {
            return (c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E';
        }

        private static void Expect(string text, ref int index, string literal)
        {
            if (index + literal.Length > text.Length
                || string.CompareOrdinal(text, index, literal, 0, literal.Length) != 0)
                throw new FormatException("리터럴 " + literal + "을 기대했습니다. 위치 " + index);

            index += literal.Length;
        }

        private static void SkipWhitespace(string text, ref int index)
        {
            while (index < text.Length)
            {
                var c = text[index];
                if (c != ' ' && c != '\t' && c != '\n' && c != '\r')
                    return;

                index++;
            }
        }

        private static void WriteValue(StringBuilder sb, object value, bool indent, int depth)
        {
            switch (value)
            {
                case null:
                    sb.Append("null");
                    return;
                case string s:
                    WriteString(sb, s);
                    return;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    return;
                case int i:
                    sb.Append(i.ToString(CultureInfo.InvariantCulture));
                    return;
                case long l:
                    sb.Append(l.ToString(CultureInfo.InvariantCulture));
                    return;
                case double d:
                    sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                    return;
                case IDictionary<string, object> map:
                    WriteObject(sb, map, indent, depth);
                    return;
                case IEnumerable<object> list:
                    WriteArray(sb, list, indent, depth);
                    return;
                default:
                    WriteString(sb, Convert.ToString(value, CultureInfo.InvariantCulture));
                    return;
            }
        }

        private static void WriteObject(StringBuilder sb, IDictionary<string, object> map, bool indent, int depth)
        {
            sb.Append('{');
            var first = true;
            foreach (var pair in map)
            {
                if (!first)
                    sb.Append(',');

                first = false;
                NewLine(sb, indent, depth + 1);
                WriteString(sb, pair.Key);
                sb.Append(':');
                if (indent)
                    sb.Append(' ');

                WriteValue(sb, pair.Value, indent, depth + 1);
            }

            if (!first)
                NewLine(sb, indent, depth);

            sb.Append('}');
        }

        private static void WriteArray(StringBuilder sb, IEnumerable<object> list, bool indent, int depth)
        {
            sb.Append('[');
            var first = true;
            foreach (var item in list)
            {
                if (!first)
                    sb.Append(',');

                first = false;
                NewLine(sb, indent, depth + 1);
                WriteValue(sb, item, indent, depth + 1);
            }

            if (!first)
                NewLine(sb, indent, depth);

            sb.Append(']');
        }

        private static void NewLine(StringBuilder sb, bool indent, int depth)
        {
            if (!indent)
                return;

            sb.Append('\n');
            sb.Append(' ', depth * 2);
        }

        private static void WriteString(StringBuilder sb, string value)
        {
            sb.Append('"');
            if (value == null)
            {
                sb.Append('"');
                return;
            }

            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }

            sb.Append('"');
        }
    }
}
