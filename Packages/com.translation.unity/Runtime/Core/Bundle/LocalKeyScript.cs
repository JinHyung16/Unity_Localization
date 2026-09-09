using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Translation
{
    /// <summary>
    /// UI 문자열 키를 C# 코드로 굽는다. 인스펙터에서 드롭다운으로 고르고 코드에서 자동완성으로 쓰기 위한 것이다.
    /// LocalKey.cs 는 export 마다 다시 쓰고, LocalizeText.cs 는 없을 때만 만든다 (프로젝트가 고쳐 써도 된다).
    /// enum 값은 키 이름의 해시라 시트에 키를 끼워 넣거나 순서를 바꿔도 기존 프리팹의 값이 바뀌지 않는다
    /// </summary>
    public static class LocalKeyScript
    {
        public const string EnumFileName = "LocalKey.cs";
        public const string ComponentFileName = "LocalizeText.cs";
        public const string DefaultNamespace = "Game";
        public const string EnumName = "LocalKey";
        public const string NoneMember = "None";

        private const int CommentMaxLength = 48;

        private static readonly HashSet<string> Keywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const",
            "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit",
            "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int",
            "interface", "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out",
            "override", "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
            "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try",
            "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
        };

        /// <summary> 폴더에 LocalKey.cs 를 쓰고, LocalizeText.cs 가 없으면 만든다. 쓴 파일 경로를 돌려준다 </summary>
        public static List<string> WriteFiles(
            IReadOnlyList<TranslateEntry> entries,
            string sourceLanguageId,
            string ns,
            string directory,
            IList<string> warnings)
        {
            if (string.IsNullOrEmpty(directory))
                throw new ArgumentException("directory는 필수입니다.", nameof(directory));

            Directory.CreateDirectory(directory);
            var written = new List<string>(2);

            var enumPath = Path.Combine(directory, EnumFileName);
            File.WriteAllText(enumPath, WriteEnum(entries, sourceLanguageId, ns, warnings), new UTF8Encoding(false));
            written.Add(enumPath);

            var componentPath = Path.Combine(directory, ComponentFileName);
            if (!File.Exists(componentPath))
            {
                File.WriteAllText(componentPath, WriteComponent(ns), new UTF8Encoding(false));
                written.Add(componentPath);
            }

            return written;
        }

        /// <summary> LocalKey enum 과 Localization 파사드가 든 파일 내용 </summary>
        public static string WriteEnum(
            IReadOnlyList<TranslateEntry> entries,
            string sourceLanguageId,
            string ns,
            IList<string> warnings)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            ns = string.IsNullOrWhiteSpace(ns) ? DefaultNamespace : ns.Trim();

            var names = new List<string>();
            var comments = new List<string>();
            var values = new List<int>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var byValue = new Dictionary<int, string>();

            foreach (var entry in entries)
            {
                if (entry.Key.IsEmpty || !entry.Key.IsStandalone)
                    continue;

                var name = entry.Key.Id;
                if (!seen.Add(name))
                    continue;

                if (!IsIdentifier(name))
                {
                    warnings?.Add("LocalKey.cs: C# 이름으로 쓸 수 없어 뺐습니다: " + name
                                  + " (영문 · 숫자 · _ 만, 숫자로 시작 불가)");
                    continue;
                }

                if (string.Equals(name, NoneMember, StringComparison.Ordinal))
                {
                    warnings?.Add("LocalKey.cs: " + NoneMember + " 은 '없음' 자리라 뺐습니다. 키 이름을 바꾸세요.");
                    continue;
                }

                var value = StableHash(name);
                if (value == 0)
                    throw new TranslationBundleException(
                        "LocalKey.cs: 키 " + name + " 의 해시가 0 이라 " + NoneMember + " 과 겹칩니다. 키 이름을 바꾸세요.");

                if (byValue.TryGetValue(value, out var other))
                    throw new TranslationBundleException(
                        "LocalKey.cs: 키 " + name + " 과 " + other + " 의 해시가 같습니다. 둘 중 하나의 이름을 바꾸세요.");

                byValue[value] = name;
                names.Add(name);
                values.Add(value);
                comments.Add(OneLine(entry.Get(sourceLanguageId)));
            }

            var sb = new StringBuilder();
            sb.Append("// <auto-generated>\n");
            sb.Append("//   Translation export 가 UI 문자열 시트에서 만든 파일. 손으로 고치지 말 것 (export 마다 다시 쓴다).\n");
            sb.Append("//   값은 키 이름의 해시라 시트에 키를 끼워 넣어도 기존 프리팹의 값이 바뀌지 않는다.\n");
            sb.Append("// </auto-generated>\n");
            sb.Append("using System;\n");
            sb.Append("using System.Collections.Generic;\n");
            sb.Append("using Translation.Unity;\n");
            sb.Append("\n");
            sb.Append("namespace ").Append(ns).Append("\n{\n");

            sb.Append("    /// <summary> UI 문자열 키. 시트의 키 이름과 같다 </summary>\n");
            sb.Append("    public enum ").Append(EnumName).Append("\n    {\n");
            sb.Append("        ").Append(NoneMember).Append(" = 0,\n");
            for (var i = 0; i < names.Count; i++)
            {
                sb.Append("        ").Append(Member(names[i])).Append(" = ")
                    .Append(values[i].ToString(CultureInfo.InvariantCulture)).Append(',');
                if (comments[i].Length != 0)
                    sb.Append("  // ").Append(comments[i]);

                sb.Append('\n');
            }

            sb.Append("    }\n\n");

            sb.Append("    /// <summary> ").Append(EnumName).Append(" 와 시트 키 문자열을 오간다</summary>\n");
            sb.Append("    public static partial class ").Append(EnumName).Append("s\n    {\n");
            sb.Append("        public const int Count = ").Append(names.Count.ToString(CultureInfo.InvariantCulture)).Append(";\n\n");

            sb.Append("        private static readonly Dictionary<int, string> Names = new Dictionary<int, string>(Count)\n        {\n");
            for (var i = 0; i < names.Count; i++)
            {
                sb.Append("            { ").Append(values[i].ToString(CultureInfo.InvariantCulture))
                    .Append(", \"").Append(names[i]).Append("\" },\n");
            }

            sb.Append("        };\n\n");

            sb.Append("        private static Dictionary<string, ").Append(EnumName).Append("> _byName;\n\n");

            sb.Append("        /// <summary> 시트 키 이름. enum 에 없는 값이면 null </summary>\n");
            sb.Append("        public static string Name(").Append(EnumName).Append(" key)\n        {\n");
            sb.Append("            return Names.TryGetValue((int)key, out var name) ? name : null;\n");
            sb.Append("        }\n\n");

            sb.Append("        /// <summary> 시트 키 이름으로 찾는다. 없으면 false 와 ").Append(NoneMember).Append(" </summary>\n");
            sb.Append("        public static bool TryParse(string name, out ").Append(EnumName).Append(" key)\n        {\n");
            sb.Append("            if (_byName == null)\n            {\n");
            sb.Append("                var map = new Dictionary<string, ").Append(EnumName).Append(">(Count, StringComparer.Ordinal);\n");
            sb.Append("                foreach (var pair in Names)\n");
            sb.Append("                    map[pair.Value] = (").Append(EnumName).Append(")pair.Key;\n");
            sb.Append("                _byName = map;\n            }\n\n");
            sb.Append("            if (name != null && _byName.TryGetValue(name, out key))\n                return true;\n\n");
            sb.Append("            key = ").Append(EnumName).Append('.').Append(NoneMember).Append(";\n            return false;\n");
            sb.Append("        }\n\n");

            sb.Append("        /// <summary> 전체 키. 순서는 시트 순서다 </summary>\n");
            sb.Append("        public static IEnumerable<").Append(EnumName).Append("> All\n        {\n");
            sb.Append("            get\n            {\n");
            sb.Append("                foreach (var pair in Names)\n");
            sb.Append("                    yield return (").Append(EnumName).Append(")pair.Key;\n");
            sb.Append("            }\n        }\n");
            sb.Append("    }\n\n");

            sb.Append("    /// <summary> 글자를 꺼내는 자리. TranslationRuntime 을 ").Append(EnumName).Append(" 로 감싼 것이다</summary>\n");
            sb.Append("    public static partial class Localization\n    {\n");
            sb.Append("        public static string GetString(").Append(EnumName).Append(" key)\n        {\n");
            sb.Append("            if (key == ").Append(EnumName).Append('.').Append(NoneMember).Append(")\n                return string.Empty;\n\n");
            sb.Append("            return TranslationRuntime.Get(KeyName(key));\n");
            sb.Append("        }\n\n");

            sb.Append("        /// <summary> {0} {1} 자리에 args 를 넣는다 </summary>\n");
            sb.Append("        public static string GetString(").Append(EnumName).Append(" key, params object[] args)\n        {\n");
            sb.Append("            if (key == ").Append(EnumName).Append('.').Append(NoneMember).Append(")\n                return string.Empty;\n\n");
            sb.Append("            return TranslationRuntime.Format(KeyName(key), args);\n");
            sb.Append("        }\n\n");

            sb.Append("        public static string GetString(string key)\n        {\n");
            sb.Append("            return TranslationRuntime.Get(key);\n");
            sb.Append("        }\n\n");

            sb.Append("        public static string GetString(string key, params object[] args)\n        {\n");
            sb.Append("            return TranslationRuntime.Format(key, args);\n");
            sb.Append("        }\n\n");

            sb.Append("        public static bool TryGetString(").Append(EnumName).Append(" key, out string text)\n        {\n");
            sb.Append("            return TranslationRuntime.TryGet(KeyName(key), out text);\n");
            sb.Append("        }\n\n");

            sb.Append("        public static bool TryGetString(string key, out string text)\n        {\n");
            sb.Append("            return TranslationRuntime.TryGet(key, out text);\n");
            sb.Append("        }\n\n");

            sb.Append("        /// <summary> 다른 enum 값을 \"Enum_값이름\" 키로 찾는다 </summary>\n");
            sb.Append("        public static string GetStringByEnum<T>(T value) where T : Enum\n        {\n");
            sb.Append("            return TranslationRuntime.Get(\"Enum_\" + value);\n");
            sb.Append("        }\n\n");

            sb.Append("        /// <summary> enum 에 없는 값이면 숫자를 키로 넘겨 없는 키 처리(@표시)를 받는다 </summary>\n");
            sb.Append("        private static string KeyName(").Append(EnumName).Append(" key)\n        {\n");
            sb.Append("            return ").Append(EnumName).Append("s.Name(key) ?? ((int)key).ToString();\n");
            sb.Append("        }\n");
            sb.Append("    }\n");
            sb.Append("}\n");

            return sb.ToString();
        }

        /// <summary> TMP 텍스트에 붙이는 컴포넌트 파일 내용. 프로젝트가 고쳐 써도 된다 </summary>
        public static string WriteComponent(string ns)
        {
            ns = string.IsNullOrWhiteSpace(ns) ? DefaultNamespace : ns.Trim();

            var sb = new StringBuilder();
            sb.Append("// Translation export 가 처음 한 번 만든 파일. 이 파일은 다시 쓰지 않으니 프로젝트에 맞게 고쳐도 된다.\n");
            sb.Append("using TMPro;\n");
            sb.Append("using Translation.Unity;\n");
            sb.Append("using UnityEngine;\n");
            sb.Append("\n");
            sb.Append("namespace ").Append(ns).Append("\n{\n");
            sb.Append("    /// <summary>\n");
            sb.Append("    /// TMP 텍스트에 붙여 Key 를 고르면 지금 언어의 글자를 넣는다. 언어가 바뀌면 다시 넣는다.\n");
            sb.Append("    /// Key 가 None 이면 아무것도 하지 않아 손으로 적은 글자가 남는다\n");
            sb.Append("    /// </summary>\n");
            sb.Append("    [RequireComponent(typeof(TMP_Text))]\n");
            sb.Append("    [AddComponentMenu(\"Translation/Localize Text\")]\n");
            sb.Append("    public class LocalizeText : MonoBehaviour\n    {\n");
            sb.Append("        [SearchableEnum] public ").Append(EnumName).Append(" Key;\n\n");
            sb.Append("        private TMP_Text _text;\n");
            sb.Append("        private object[] _args;\n\n");

            sb.Append("        public TMP_Text Text\n        {\n");
            sb.Append("            get\n            {\n");
            sb.Append("                if (_text == null)\n                    _text = GetComponent<TMP_Text>();\n\n");
            sb.Append("                return _text;\n");
            sb.Append("            }\n        }\n\n");

            sb.Append("        private void OnEnable()\n        {\n");
            sb.Append("            TranslationRuntime.OnLanguageChanged += OnLanguageChanged;\n");
            sb.Append("            Refresh();\n");
            sb.Append("        }\n\n");

            sb.Append("        private void OnDisable()\n        {\n");
            sb.Append("            TranslationRuntime.OnLanguageChanged -= OnLanguageChanged;\n");
            sb.Append("        }\n\n");

            sb.Append("        /// <summary> 키를 바꾸고 바로 넣는다. args 는 {0} {1} 자리에 들어간다 </summary>\n");
            sb.Append("        public void SetKey(").Append(EnumName).Append(" key, params object[] args)\n        {\n");
            sb.Append("            Key = key;\n");
            sb.Append("            _args = args;\n");
            sb.Append("            Refresh();\n");
            sb.Append("        }\n\n");

            sb.Append("        /// <summary> {0} {1} 자리 값만 바꾼다 </summary>\n");
            sb.Append("        public void SetArgs(params object[] args)\n        {\n");
            sb.Append("            _args = args;\n");
            sb.Append("            Refresh();\n");
            sb.Append("        }\n\n");

            sb.Append("        public void Refresh()\n        {\n");
            sb.Append("            if (Key == ").Append(EnumName).Append(".None || Text == null)\n                return;\n\n");
            sb.Append("            Text.text = _args == null || _args.Length == 0\n");
            sb.Append("                ? Localization.GetString(Key)\n");
            sb.Append("                : Localization.GetString(Key, _args);\n");
            sb.Append("        }\n\n");

            sb.Append("        private void OnLanguageChanged(string languageId)\n        {\n");
            sb.Append("            Refresh();\n");
            sb.Append("        }\n");
            sb.Append("    }\n");
            sb.Append("}\n");

            return sb.ToString();
        }

        /// <summary> 키 이름의 FNV-1a 32비트 해시. 같은 이름이면 언제 어디서 돌려도 같은 값이다 </summary>
        public static int StableHash(string name)
        {
            unchecked
            {
                var hash = 2166136261u;
                for (var i = 0; i < name.Length; i++)
                {
                    hash ^= name[i];
                    hash *= 16777619u;
                }

                return (int)hash;
            }
        }

        /// <summary> C# 이름으로 쓸 수 있는지. 영문 · 숫자 · _ 만, 숫자로 시작 불가 </summary>
        public static bool IsIdentifier(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            for (var i = 0; i < name.Length; i++)
            {
                var c = name[i];
                var letter = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_';
                var digit = c >= '0' && c <= '9';
                if (i == 0 ? !letter : !(letter || digit))
                    return false;
            }

            return true;
        }

        private static string Member(string name)
        {
            return Keywords.Contains(name) ? "@" + name : name;
        }

        private static string OneLine(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var sb = new StringBuilder(Math.Min(text.Length, CommentMaxLength + 1));
            foreach (var c in text)
            {
                if (sb.Length >= CommentMaxLength)
                {
                    sb.Append('…');
                    break;
                }

                sb.Append(c == '\r' || c == '\n' || c == '\t' ? ' ' : c);
            }

            return sb.ToString().Trim();
        }
    }
}
