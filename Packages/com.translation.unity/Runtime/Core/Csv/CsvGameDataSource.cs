using System;
using System.Collections.Generic;
using System.IO;

namespace Translation
{
    /// <summary> 폴더 안의 {테이블}.csv 를 게임 DB 로 읽는다. 1행이 헤더다. 루트에 없으면 하위 폴더를 찾는다 </summary>
    public sealed class CsvGameDataSource : IGameDataSource
    {
        private readonly string _directory;

        private readonly Dictionary<string, string> _resolved =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string FileExtension { get; set; } = ".csv";

        /// <summary> 헤더 다음에 건너뛸 행 수 (타입 행 등) </summary>
        public int DataRowOffset { get; set; }

        public CsvGameDataSource(string directory)
        {
            if (string.IsNullOrEmpty(directory))
                throw new ArgumentException("directory는 필수입니다.", nameof(directory));

            if (!Directory.Exists(directory))
                throw new TranslationConfigException("게임 DB 폴더가 없습니다: " + directory);

            _directory = directory;
        }

        public string Label
        {
            get { return "csv:" + _directory; }
        }

        public bool TableExists(string table)
        {
            return PathFor(table) != null;
        }

        public IReadOnlyList<GameDataRow> ReadTable(string table)
        {
            var path = PathFor(table);
            return path == null ? new List<GameDataRow>() : GameDataGrid.ToRows(CsvFile.Read(path), DataRowOffset);
        }

        private string PathFor(string table)
        {
            if (string.IsNullOrEmpty(table))
                return null;

            var direct = Path.Combine(_directory, table + FileExtension);
            if (File.Exists(direct))
                return direct;

            if (_resolved.TryGetValue(table, out var cached))
                return cached;

            string found = null;
            var hits = Directory.GetFiles(_directory, table + FileExtension, SearchOption.AllDirectories);
            if (hits.Length > 0)
                found = hits[0];

            _resolved[table] = found;
            return found;
        }
    }
}
