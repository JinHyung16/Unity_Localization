using System.Collections.Generic;
using ClosedXML.Excel;

namespace Translation.Excel
{
    /// <summary> 워크시트와 문자열 격자를 오간다 </summary>
    public static class ExcelGrid
    {
        /// <summary> 사용 중인 범위를 문자열 격자로 읽는다. 빈 시트면 빈 목록 </summary>
        public static List<string[]> Read(IXLWorksheet sheet)
        {
            var rows = new List<string[]>();
            if (sheet == null)
                return rows;

            var range = sheet.RangeUsed();
            if (range == null)
                return rows;

            var lastRow = range.LastRow().RowNumber();
            var lastColumn = range.LastColumn().ColumnNumber();

            for (var r = 1; r <= lastRow; r++)
            {
                var row = new string[lastColumn];
                for (var c = 1; c <= lastColumn; c++)
                    row[c - 1] = sheet.Cell(r, c).GetString();

                rows.Add(row);
            }

            return rows;
        }

        /// <summary> 시트를 비우고 격자를 문자열 셀로 쓴다 </summary>
        public static void Write(IXLWorksheet sheet, IReadOnlyList<string[]> rows)
        {
            sheet.Clear();
            for (var r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row == null)
                    continue;

                for (var c = 0; c < row.Length; c++)
                    sheet.Cell(r + 1, c + 1).SetValue(row[c] ?? string.Empty);
            }
        }
    }
}
