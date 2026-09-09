using System;
using System.Collections.Generic;
using System.Text;

namespace Translation
{
    /// <summary>
    /// 빌드에 동봉하는 번역표 한 장. 열이 언어다 (Key,Korean,English,…).
    /// 패치 전에도 글자가 나와야 하는 자리를 위한 것이라 UI 문자열 키만 담는다
    /// </summary>
    public static class LocalKeyCsv
    {
        public const string DefaultFileName = "LocalKey.csv";
        public const string KeyColumn = "Key";

        /// <summary> 엔트리를 CSV 한 장으로 굽는다. 열 순서는 languageIds 순서다 </summary>
        public static string Write(IReadOnlyList<TranslateEntry> entries, LanguageSet languages,
            IReadOnlyList<string> languageIds, bool bakeFallback)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));
            if (languageIds == null)
                throw new ArgumentNullException(nameof(languageIds));

            var builder = new StringBuilder();
            builder.Append(KeyColumn);
            for (var i = 0; i < languageIds.Count; i++)
                builder.Append(',').Append(Escape(languageIds[i]));

            builder.Append('\n');

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Key.IsEmpty || entry.Key.IsStandalone == false)
                    continue;

                builder.Append(Escape(BundleKey.Encode(entry.Key)));
                for (var j = 0; j < languageIds.Count; j++)
                {
                    var value = bakeFallback && languages != null
                        ? entry.Resolve(languages, languageIds[j])
                        : entry.Get(languageIds[j]);

                    builder.Append(',').Append(Escape(value));
                }

                builder.Append('\n');
            }

            return builder.ToString();
        }

        /// <summary> CSV 에서 한 언어의 열만 뽑아 키에서 글자로 준다 </summary>
        public static Dictionary<string, string> Parse(string text, string languageId)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(languageId))
                return result;

            var rows = ReadRows(text);
            if (rows.Count == 0)
                return result;

            var header = rows[0];
            var column = -1;
            for (var i = 1; i < header.Count; i++)
            {
                if (string.Equals(header[i], languageId, StringComparison.OrdinalIgnoreCase))
                {
                    column = i;
                    break;
                }
            }

            if (column < 0)
                return result;

            for (var i = 1; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row.Count == 0 || row[0].Length == 0)
                    continue;
                if (column >= row.Count)
                    continue;

                var value = row[column];
                if (value.Length == 0)
                    continue;

                result[row[0]] = value;
            }

            return result;
        }

        /// <summary> 헤더에서 언어 열 이름들을 준다 (첫 칸 Key 는 뺀다) </summary>
        public static List<string> ReadLanguageIds(string text)
        {
            var result = new List<string>();
            var rows = ReadRows(text);
            if (rows.Count == 0)
                return result;

            for (var i = 1; i < rows[0].Count; i++)
                result.Add(rows[0][i]);

            return result;
        }

        private static List<List<string>> ReadRows(string text)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            var quoted = false;

            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (quoted)
                {
                    if (c != '"')
                    {
                        cell.Append(c);
                        continue;
                    }
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                        continue;
                    }
                    quoted = false;
                    continue;
                }

                if (c == '"')
                {
                    quoted = true;
                    continue;
                }
                if (c == ',')
                {
                    row.Add(cell.ToString());
                    cell.Length = 0;
                    continue;
                }
                if (c == '\n' || c == '\r')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        i++;

                    row.Add(cell.ToString());
                    cell.Length = 0;
                    if (row.Count > 1 || row[0].Length != 0)
                        rows.Add(row);

                    row = new List<string>();
                    continue;
                }

                cell.Append(c);
            }

            if (cell.Length != 0 || row.Count != 0)
            {
                row.Add(cell.ToString());
                if (row.Count > 1 || row[0].Length != 0)
                    rows.Add(row);
            }

            if (rows.Count != 0 && rows[0].Count != 0)
                rows[0][0] = rows[0][0].TrimStart('﻿');

            return rows;
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var needsQuote = value.IndexOf(',') >= 0
                || value.IndexOf('"') >= 0
                || value.IndexOf('\n') >= 0
                || value.IndexOf('\r') >= 0;

            if (needsQuote == false)
                return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
