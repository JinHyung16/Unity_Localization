using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Translation
{
    /// <summary>
    /// 폴더 안의 {테이블}.json 을 읽는다. 루트가 오브젝트 배열이거나
    /// 오브젝트 안에 배열 하나가 든 꼴을 받는다. 루트에 없으면 하위 폴더를 찾는다
    /// </summary>
    public sealed class JsonGameDataSource : IGameDataSource
    {
        private readonly string _directory;

        private readonly Dictionary<string, string> _resolved =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string Extension { get; set; } = ".json";

        public JsonGameDataSource(string directory)
        {
            if (string.IsNullOrEmpty(directory))
                throw new ArgumentException("directory는 필수입니다.", nameof(directory));

            if (!Directory.Exists(directory))
                throw new TranslationConfigException("게임 DB 폴더가 없습니다: " + directory);

            _directory = directory;
        }

        public string Label
        {
            get { return "json:" + _directory; }
        }

        public bool TableExists(string table)
        {
            return PathFor(table) != null;
        }

        public IReadOnlyList<GameDataRow> ReadTable(string table)
        {
            var rows = new List<GameDataRow>();
            var path = PathFor(table);
            if (path == null)
                return rows;

            object root;
            try
            {
                root = MiniJson.Parse(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                throw new TranslationConfigException("게임 DB JSON 을 읽을 수 없습니다: " + path + " — " + e.Message);
            }

            var items = root as List<object>;
            if (items == null && root is Dictionary<string, object> map)
            {
                foreach (var pair in map)
                {
                    if (pair.Value is List<object> list)
                    {
                        items = list;
                        break;
                    }
                }
            }

            if (items == null)
                throw new TranslationConfigException("게임 DB JSON 의 루트가 배열이 아닙니다: " + path);

            foreach (var item in items)
            {
                if (!(item is Dictionary<string, object> cells))
                    continue;

                var row = new GameDataRow();
                foreach (var pair in cells)
                {
                    if (pair.Value is List<object> array)
                    {
                        var values = new List<string>(array.Count);
                        foreach (var element in array)
                        {
                            if (element is Dictionary<string, object> || element is List<object>)
                                continue;

                            values.Add(Text(element));
                        }

                        row.Set(pair.Key, values);
                        continue;
                    }

                    if (pair.Value is Dictionary<string, object>)
                        continue;

                    row.Set(pair.Key, Text(pair.Value));
                }

                rows.Add(row);
            }

            return rows;
        }

        private static string Text(object value)
        {
            if (value == null)
                return string.Empty;
            if (value is string s)
                return s;
            if (value is bool b)
                return b ? "true" : "false";

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private string PathFor(string table)
        {
            if (string.IsNullOrEmpty(table))
                return null;

            var direct = Path.Combine(_directory, table + Extension);
            if (File.Exists(direct))
                return direct;

            if (_resolved.TryGetValue(table, out var cached))
                return cached;

            string found = null;
            try
            {
                var hits = Directory.GetFiles(_directory, table + Extension, SearchOption.AllDirectories);
                if (hits.Length > 0)
                    found = hits[0];
            }
            catch (DirectoryNotFoundException)
            {
            }

            _resolved[table] = found;
            return found;
        }
    }
}
