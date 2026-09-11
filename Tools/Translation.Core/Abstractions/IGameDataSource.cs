using System;
using System.Collections.Generic;
using System.Globalization;

namespace Translation
{
    /// <summary> 게임 DB 한 행. 셀은 문자열이거나 문자열 배열이다 </summary>
    public sealed class GameDataRow
    {
        private readonly Dictionary<string, object> _cells =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public int Count
        {
            get { return _cells.Count; }
        }

        public void Set(string column, string value)
        {
            if (string.IsNullOrEmpty(column))
                return;

            _cells[column] = value ?? string.Empty;
        }

        public void Set(string column, IReadOnlyList<string> values)
        {
            if (string.IsNullOrEmpty(column))
                return;

            _cells[column] = values ?? new string[0];
        }

        public bool Has(string column)
        {
            return !string.IsNullOrEmpty(column) && _cells.ContainsKey(column);
        }

        public bool IsArray(string column)
        {
            return Has(column) && _cells[column] is IReadOnlyList<string>;
        }

        /// <summary> 셀을 문자열로. 배열이면 arraySeparator 로 잇는다 (없으면 개행) </summary>
        public string GetText(string column, string arraySeparator = null)
        {
            if (!Has(column))
                return string.Empty;

            var value = _cells[column];
            if (value is string text)
                return text;

            if (value is IReadOnlyList<string> list)
                return string.Join(string.IsNullOrEmpty(arraySeparator) ? "\n" : arraySeparator, list);

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }
    }

    /// <summary> 게임 DB 에서 원문을 읽는다. sync 가 시트에 넣을 원문의 출처다 </summary>
    public interface IGameDataSource
    {
        string Label { get; }

        bool TableExists(string table);

        /// <summary> 테이블의 행 전부. 없으면 빈 목록 </summary>
        IReadOnlyList<GameDataRow> ReadTable(string table);
    }

    /// <summary> 헤더 있는 셀 격자를 게임 DB 행으로 바꾼다. CSV·엑셀 게임 DB 가 같이 쓴다 </summary>
    public static class GameDataGrid
    {
        public static List<GameDataRow> ToRows(IReadOnlyList<string[]> grid, int dataRowOffset)
        {
            var rows = new List<GameDataRow>();
            if (grid == null || grid.Count == 0)
                return rows;

            var header = grid[0];
            for (var r = 1 + Math.Max(0, dataRowOffset); r < grid.Count; r++)
            {
                var cells = grid[r];
                if (cells == null)
                    continue;

                var row = new GameDataRow();
                var any = false;
                for (var c = 0; c < header.Length && c < cells.Length; c++)
                {
                    var name = (header[c] ?? string.Empty).Trim();
                    if (name.Length == 0)
                        continue;

                    row.Set(name, cells[c]);
                    if (!string.IsNullOrEmpty(cells[c]))
                        any = true;
                }

                if (any)
                    rows.Add(row);
            }

            return rows;
        }
    }
}
