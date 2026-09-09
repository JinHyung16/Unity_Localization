using System;
using System.Collections.Generic;

namespace Translation.Excel
{
    /// <summary> 엑셀을 게임 DB 로 읽는다. 폴더면 {테이블}.xlsx, 파일이면 워크시트 이름이 테이블이다. 1행이 헤더다 </summary>
    public sealed class ExcelGameDataSource : IGameDataSource, IDisposable
    {
        private readonly ExcelWorkbookSet _workbooks;

        /// <summary> 헤더 다음에 건너뛸 행 수 (타입 행 등) </summary>
        public int DataRowOffset { get; set; }

        public ExcelGameDataSource(string path)
        {
            _workbooks = new ExcelWorkbookSet(path);
        }

        public string Label
        {
            get { return _workbooks.Label; }
        }

        public bool TableExists(string table)
        {
            return !string.IsNullOrEmpty(table) && _workbooks.SheetExists(table);
        }

        public IReadOnlyList<GameDataRow> ReadTable(string table)
        {
            var sheet = TableExists(table) ? _workbooks.TryGetWorksheet(table) : null;
            return sheet == null
                ? new List<GameDataRow>()
                : GameDataGrid.ToRows(ExcelGrid.Read(sheet), DataRowOffset);
        }

        public void Dispose()
        {
            _workbooks.Dispose();
        }
    }
}
