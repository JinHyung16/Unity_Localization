using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Translation
{
    /// <summary> RFC 4180 CSV 읽기. 따옴표 안의 콤마와 개행을 올바르게 다룬다 </summary>
    public static class CsvFile
    {
        public static List<string[]> Read(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("CSV 파일이 없습니다: " + path, path);

            return Parse(File.ReadAllText(path, Encoding.UTF8));
        }

        public static List<string[]> Parse(string text)
        {
            var rows = new List<string[]>();
            if (string.IsNullOrEmpty(text))
                return rows;

            if (text.Length > 0 && text[0] == '\uFEFF')
                text = text.Substring(1);

            var row = new List<string>();
            var cell = new StringBuilder();
            var inQuotes = false;
            var i = 0;

            while (i < text.Length)
            {
                var c = text[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            cell.Append('"');
                            i += 2;
                            continue;
                        }

                        inQuotes = false;
                        i++;
                        continue;
                    }

                    cell.Append(c);
                    i++;
                    continue;
                }

                switch (c)
                {
                    case '"':
                        inQuotes = true;
                        i++;
                        continue;

                    case ',':
                        row.Add(cell.ToString());
                        cell.Length = 0;
                        i++;
                        continue;

                    case '\r':
                        i++;
                        continue;

                    case '\n':
                        row.Add(cell.ToString());
                        cell.Length = 0;
                        rows.Add(row.ToArray());
                        row.Clear();
                        i++;
                        continue;

                    default:
                        cell.Append(c);
                        i++;
                        continue;
                }
            }

            if (cell.Length > 0 || row.Count > 0)
            {
                row.Add(cell.ToString());
                rows.Add(row.ToArray());
            }

            return rows;
        }

        /// <summary> 격자를 RFC 4180 CSV 로 쓴다. UTF-8, BOM 없음, LF </summary>
        public static void Write(string path, IReadOnlyList<string[]> rows)
        {
            if (rows == null)
                throw new ArgumentNullException(nameof(rows));

            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var sb = new StringBuilder();
            foreach (var row in rows)
            {
                if (row != null)
                {
                    for (var i = 0; i < row.Length; i++)
                    {
                        if (i > 0)
                            sb.Append(',');

                        sb.Append(Escape(row[i]));
                    }
                }

                sb.Append('\n');
            }

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        public static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0)
                return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        /// <summary> 헤더 행에서 컬럼 이름의 인덱스를 찾는다. 없으면 -1 </summary>
        public static int IndexOf(IReadOnlyList<string> header, string name)
        {
            if (header == null || string.IsNullOrEmpty(name))
                return -1;

            for (var i = 0; i < header.Count; i++)
            {
                if (string.Equals((header[i] ?? string.Empty).Trim(), name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }
    }
}
